using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Doomlike
{
    // Levels: building and clearing them, doors, pickups, map cells (exit, secrets, ambushes,
    // slime), the tally and the draft, death and victory, debug skips and the smoke run.
    public sealed partial class DoomGame
    {
        BuiltLevel _level;
        int _levelIndex;
        readonly List<DoomEnemy> _enemies = new List<DoomEnemy>(64);
        readonly PlayerArsenal _arsenal = new PlayerArsenal();
        readonly StatBlock _stats = new StatBlock();
        UpgradeInventory _inventory;
        UpgradeDraft _draft;
        UpgradeDef _fallback;
        readonly List<UpgradeDef> _pool = new List<UpgradeDef>();
        readonly List<UpgradeDef> _offer = new List<UpgradeDef>();
        readonly Dictionary<string, DoomUpgradeAsset> _upgradeAssets = new Dictionary<string, DoomUpgradeAsset>();
        readonly LevelTally _tally = new LevelTally();
        readonly List<LevelTally> _finished = new List<LevelTally>();
        Rng _rng, _draftRng;
        int _score, _nextEnemyId, _draftsTaken;
        bool[] _cellDone = new bool[0];
        float _lockedMessageAt = -10f;
        DoomEnemy _boss;

        // ---------- Read-only state (tests, debug, HUD) ----------

        public BuiltLevel Level => _level;
        public int LevelIndex => _levelIndex;
        public IReadOnlyList<DoomEnemy> Enemies => _enemies;
        public PlayerArsenal Arsenal => _arsenal;
        public StatBlock Stats => _stats;
        public UpgradeInventory Inventory => _inventory;
        public LevelTally Tally => _tally;
        public int Score => _score;
        public int DraftsTaken => _draftsTaken;
        public int LevelsCleared => _finished.Count;
        public DoomUpgradeAsset AssetFor(string id) => id != null && _upgradeAssets.TryGetValue(id, out var a) ? a : null;

        // ---------- Run ----------

        void SetupRun()
        {
            _arsenal.Setup(config);
            _inventory = new UpgradeInventory(_stats);
            foreach (var a in config.upgrades)
            {
                if (a == null || a.def == null || string.IsNullOrEmpty(a.def.id)) continue;
                _pool.Add(a.def);
                _upgradeAssets[a.def.id] = a;
            }
            _fallback = config.fallbackUpgrade != null ? config.fallbackUpgrade.def : null;
            if (_fallback != null && !string.IsNullOrEmpty(_fallback.id)) _upgradeAssets[_fallback.id] = config.fallbackUpgrade;
            _draft = new UpgradeDraft(_fallback);
        }

        void BeginRun()
        {
            _seed = _seedOverride != 0 ? _seedOverride : Rng.TimeSeed();
            _rng = new Rng(_seed);
            _draftRng = new Rng(_seed ^ 0xD00D5EEDUL);
            _inventory.Clear();
            _stats.ClearModifiers();
            DoomStats.SetBase(_stats, config.player.maxHealth);
            _arsenal.NewRun(config);
            player.NewRun(_stats.Get(DoomStats.MaxHealth));
            player.Health.DamageTakenScale = GameRoot.Settings.damageTakenScale;
            ApplyStats(false);
            _score = 0;
            _draftsTaken = 0;
            _finished.Clear();
            GameRoot.Run.score = 0;
            _hud.SetUpgrades(_inventory, AssetFor);
            LoadLevel(Mathf.Clamp(_startLevel, 0, Mathf.Max(0, config.LevelCount - 1)));
        }

        /// <summary>Builds level <paramref name="index"/> and puts the player at its start.</summary>
        public void LoadLevel(int index)
        {
            ClearLevel();
            _phase = Phase.Loading;
            _levelIndex = index;
            var problems = new MapProblems();
            var map = config.ParseLevel(index, problems);
            if (map == null || !problems.Ok)
            {
                Debug.LogError("DoomGame: level " + (index + 1) + " has problems:\n" + problems + "\nRun JamKit > DOOM-like > Validate Content.");
                if (map == null) return;
            }
            _level = LevelBuilder.Build(map, config, levelRoot, true);
            _cellDone = new bool[map.width * map.height];

            player.ResetView();
            player.Spawn(_level.startPosition + Vector3.up * 0.05f, _level.startYaw);
            if (config.healthFloorPerLevel > 0f && player.Health.Current < config.healthFloorPerLevel)
                player.Health.Heal(config.healthFloorPerLevel - player.Health.Current);

            _boss = null;
            foreach (var spawn in _level.enemySpawns)
            {
                var e = DoomEnemy.Create(spawn, this, _level.thingsRoot, ++_nextEnemyId, _rng.Fork());
                _enemies.Add(e);
                if (spawn.asset.isBoss && _boss == null) _boss = e;
            }

            _tally.Reset(map.name, _enemies.Count, _level.totalSecrets, _level.totalItems, map.parSeconds);
            var theme = _level.theme;
            cam.backgroundColor = theme.fogColor;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Shader.SetGlobalColor(FogColorId, theme.fogColor);
            Shader.SetGlobalVector(FogParamsId, new Vector4(theme.fogStart, theme.fogEnd, theme.fogStrength, 0f));

            string music = !string.IsNullOrEmpty(map.music) ? map.music : _level.hasBoss ? config.bossMusic : config.levelMusic;
            if (!string.IsNullOrEmpty(music)) GameRoot.Audio.PlayMusic(music);
            string sub = !string.IsNullOrEmpty(map.subtitle) ? map.subtitle : "Find the exit";
            _hud.ShowCard(map.name, "Level " + (index + 1) + " of " + config.LevelCount + "  -  " + sub, 3.2f);
            GameRoot.Run.Set("Level", (index + 1) + " / " + config.LevelCount);
            if (_view != null) _view.SetWeapon(_arsenal.CurrentAsset);
            _phase = Phase.Playing;
            _phaseTime = 0f;
        }

        void ClearLevel()
        {
            _enemies.Clear();
            projectiles.Clear();
            fx.Clear();
            _noiseCount = 0;
            if (_level == null) return;
            // Off at once (its NavMesh and colliders go now), destroyed at the end of the frame.
            _level.DestroyGeneratedAssets();
            _level.gameObject.SetActive(false);
            Destroy(_level.gameObject);
            _level = null;
        }

        // ---------- Doors ----------

        void TickDoors(float dt)
        {
            var doors = _level.doors;
            Vector3 pp = player.transform.position;
            float cs = _level.cellSize;
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i];
                d.Tick(dt);
                if (d.IsOpening || d.kind == DoomDoor.DoorKind.Closet) continue;
                Vector3 c = _level.CellCenter(d.cellX, d.cellY);
                float pd = Flat(pp - c);
                switch (d.kind)
                {
                    case DoomDoor.DoorKind.Normal:
                        if ((player.Alive && pd < cs * 0.5f + 1.3f) || EnemyNear(c, cs * 0.5f + 1.2f)) OpenDoor(d);
                        break;
                    case DoomDoor.DoorKind.Locked:
                        if (player.Alive && pd < cs * 0.5f + 0.8f) TryLockedDoor(d);
                        break;
                    case DoomDoor.DoorKind.Secret:
                        // Walk into it (pushing toward the wall, not sliding along it) or press E.
                        if (player.Alive && pd < cs * 0.5f + player.Radius + 0.15f && PushingToward(c)) OpenDoor(d);
                        break;
                }
            }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        bool PushingToward(Vector3 c)
        {
            var w = player.Wish;
            if (w.sqrMagnitude < 0.25f) return false;
            var to = new Vector2(c.x - player.transform.position.x, c.z - player.transform.position.z);
            return to.sqrMagnitude > 1e-4f && Vector2.Dot(w.normalized, to.normalized) > 0.7f;
        }

        bool EnemyNear(Vector3 c, float r)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e.Alive && e.Brain.IsAwake && Flat(e.transform.position - c) < r) return true;
            }
            return false;
        }

        void OpenDoor(DoomDoor d)
        {
            if (!d.Open()) return;
            PlayAt("door", _level.CellCenter(d.cellX, d.cellY), 1f);
        }

        void TryLockedDoor(DoomDoor d)
        {
            if (player.HasKey(d.keyColor))
            {
                OpenDoor(d);
                GameRoot.Hud.Message("The " + MapLegend.KeyName(d.keyColor) + " door opens.", 1.5f);
                return;
            }
            if (Time.unscaledTime - _lockedMessageAt < 2f) return;
            _lockedMessageAt = Time.unscaledTime;
            GameRoot.Hud.Message("You need the " + MapLegend.KeyName(d.keyColor) + " key.", 2.2f);
            PlaySound("blip", 0.8f);
        }

        /// <summary>E / Space: opens the door or secret wall you're looking at.</summary>
        void Use()
        {
            Vector3 pp = player.transform.position;
            Vector3 fwd = player.transform.forward;
            float reach = config.player.useRange + _level.cellSize * 0.5f;
            DoomDoor best = null;
            float bestD = float.MaxValue;
            foreach (var d in _level.doors)
            {
                if (d.IsOpening || d.kind == DoomDoor.DoorKind.Closet) continue;
                Vector3 to = _level.CellCenter(d.cellX, d.cellY) - pp;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > reach || (dist > 0.1f && Vector3.Dot(to / dist, fwd) < 0.4f)) continue;
                if (dist < bestD) { bestD = dist; best = d; }
            }
            if (best == null) { PlaySound("blip", 0.3f); return; }
            if (best.kind == DoomDoor.DoorKind.Locked) { _lockedMessageAt = -10f; TryLockedDoor(best); }
            else OpenDoor(best);
        }

        // ---------- Pickups ----------

        void TickItems(float dt)
        {
            var items = _level.items;
            Vector3 pp = player.transform.position;
            float yaw = CameraYaw;
            float t = Time.time;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.taken) continue;
                it.billboard.transform.position = it.basePosition + Vector3.up * (0.06f + 0.06f * Mathf.Sin(t * 3f + it.bobPhase));
                it.billboard.Face(yaw);
                if (player.Alive && _phase == Phase.Playing && Flat(pp - it.basePosition) < player.Radius + 0.55f) TryPickUp(it);
            }
        }

        void TryPickUp(LevelItem it)
        {
            float bonus = _stats.Get(DoomStats.PickupBonus);
            bool took = false;
            string message = "";
            string sound = "item";
            switch (it.kind)
            {
                case LevelItem.ItemKind.Key:
                    player.GiveKey(it.keyColor);
                    took = true;
                    message = "Picked up the " + MapLegend.KeyName(it.keyColor) + " key.";
                    sound = "key";
                    break;
                case LevelItem.ItemKind.Weapon:
                    bool isNew = !_arsenal.Owns(it.weapon);
                    took = _arsenal.Give(it.weapon, bonus);
                    message = isNew ? "You got the " + it.weapon.displayName + "!  (key " + it.weapon.slot + ")" : "Picked up some ammo.";
                    sound = isNew ? "weapon_up" : "item";
                    break;
                case LevelItem.ItemKind.Pickup:
                {
                    var p = it.pickup;
                    if (p.kind == PickupKind.Health)
                    {
                        if (player.Health.Current >= player.Health.Max) break;
                        player.Health.Heal(p.amount * bonus);
                        took = true;
                    }
                    else
                    {
                        int idx = _arsenal.Ammo.IndexOf(p.ammoType);
                        took = _arsenal.Ammo.Add(idx, Mathf.RoundToInt(p.amount * bonus)) > 0;
                    }
                    message = p.message;
                    sound = p.sound;
                    break;
                }
            }
            if (!took) return;
            it.taken = true;
            it.billboard.SetVisible(false);
            if (it.counts) _tally.items++;
            if (!string.IsNullOrEmpty(message)) GameRoot.Hud.Message(message, 2f);
            PlaySound(sound, 0.9f);
            GameRoot.Juice.Flash(new Color(1f, 0.9f, 0.5f, 0.12f), 0.15f);
        }

        void SpawnDrops(DoomEnemy enemy)
        {
            var drops = enemy.asset.drops;
            if (drops == null || _level == null) return;
            Vector3 at = enemy.transform.position;
            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                if (d == null || (d.pickup == null && d.weapon == null) || !_rng.Chance(d.chance)) continue;
                var offset = new Vector3(_rng.Range(-0.4f, 0.4f), 0f, _rng.Range(-0.4f, 0.4f));
                Billboard bb;
                LevelItem item;
                if (d.weapon != null)
                {
                    bb = Billboard.Create(d.weapon.displayName, _level.thingsRoot, at + offset, Aspect(d.weapon.pickupTexture) * d.weapon.pickupSize, d.weapon.pickupSize,
                        config.spriteMaterial, d.weapon.pickupTexture, Color.white);
                    item = new LevelItem { kind = LevelItem.ItemKind.Weapon, weapon = d.weapon };
                }
                else
                {
                    bb = Billboard.Create(d.pickup.displayName, _level.thingsRoot, at + offset, Aspect(d.pickup.texture) * d.pickup.size, d.pickup.size,
                        config.spriteMaterial, d.pickup.texture, d.pickup.tint);
                    item = new LevelItem { kind = LevelItem.ItemKind.Pickup, pickup = d.pickup };
                }
                item.billboard = bb;
                item.basePosition = at + offset;
                item.bobPhase = i;
                _level.items.Add(item);
            }
        }

        static float Aspect(Texture t) => t == null || t.height == 0 ? 1f : t.width / (float)t.height;

        // ---------- Map cells ----------

        void TickCells(float dt)
        {
            var map = _level.map;
            if (!_level.WorldToCell(player.transform.position, out int x, out int y)) return;
            var kind = map.KindAt(x, y);
            float hazard = player.HazardTick(dt, kind == CellKind.Hazard, config.player.hazardDamagePerSecond);
            if (hazard > 0f) DamagePlayer(hazard, player.transform.position, JamKit.Combat.DamageKind.Hazard);
            int cell = y * map.width + x;
            switch (kind)
            {
                case CellKind.Exit:
                    CompleteLevel();
                    break;
                case CellKind.Secret:
                    if (_cellDone[cell]) break;
                    MarkArea(x, y, CellKind.Secret);
                    _tally.secrets++;
                    GameRoot.Hud.Message("A secret!", 2f);
                    PlaySound("secret", 1f);
                    break;
                case CellKind.Trigger:
                    if (_cellDone[cell]) break;
                    _cellDone[cell] = true;
                    SpringAmbush(x, y);
                    break;
            }
        }

        /// <summary>Marks a whole patch of touching secret cells as found (one secret = one patch).</summary>
        void MarkArea(int x, int y, CellKind kind)
        {
            var map = _level.map;
            var stack = new Stack<int>();
            stack.Push(y * map.width + x);
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                if (_cellDone[c]) continue;
                _cellDone[c] = true;
                int cx = c % map.width, cy = c / map.width;
                if (map.KindAt(cx + 1, cy) == kind) stack.Push(c + 1);
                if (map.KindAt(cx - 1, cy) == kind) stack.Push(c - 1);
                if (map.KindAt(cx, cy + 1) == kind) stack.Push(c + map.width);
                if (map.KindAt(cx, cy - 1) == kind) stack.Push(c - map.width);
            }
        }

        /// <summary>An ambush: nearby closet walls drop and nearby enemies wake up.</summary>
        public void SpringAmbush(int x, int y)
        {
            int r = Mathf.Max(1, config.ambushRadius);
            bool any = false;
            foreach (var d in _level.doors)
            {
                if (d.kind != DoomDoor.DoorKind.Closet || d.IsOpening) continue;
                if (Mathf.Abs(d.cellX - x) > r || Mathf.Abs(d.cellY - y) > r) continue;
                d.Open();
                any = true;
            }
            foreach (var e in _enemies)
            {
                if (!e.Alive || !_level.WorldToCell(e.transform.position, out int ex, out int ey)) continue;
                if (Mathf.Abs(ex - x) <= r && Mathf.Abs(ey - y) <= r) e.Alert();
            }
            if (any) PlaySound("door", 1f);
        }

        void TickBoss()
        {
            bool show = _boss != null && _boss.Alive && _boss.Brain.IsAwake;
            _hud.SetBoss(_boss != null ? (string.IsNullOrEmpty(_boss.asset.bossTitle) ? _boss.asset.displayName : _boss.asset.bossTitle) : "",
                _boss != null ? _boss.Health.Fraction01 : 0f, show);
        }

        // ---------- End of a level ----------

        void CompleteLevel()
        {
            if (_phase != Phase.Playing) return;
            CountKills();
            _phase = Phase.Tally;
            _phaseTime = 0f;
            projectiles.Clear();
            _finished.Add(_tally.Clone());
            PlaySound("level_done", 1f);
            GameRoot.Hud.ClearMessage();
            _hud.HideCard();
            bool last = _levelIndex >= config.LevelCount - 1;
            GameRoot.Run.score = _score;
            _hud.ShowTally(_tally, "LEVEL COMPLETE", last ? "Finish" : "Continue", OnTallyContinue);
        }

        void OnTallyContinue()
        {
            if (_phase != Phase.Tally) return;
            if (_levelIndex >= config.LevelCount - 1) { Victory(); return; }
            if (config.upgradeDraft && (_pool.Count > 0 || _fallback != null)) OpenDraft();
            else LoadLevel(_levelIndex + 1);
        }

        void OpenDraft()
        {
            _phase = Phase.Draft;
            _draft.Offer(_pool, _inventory, Mathf.Max(1, config.draftSize), _draftRng, _offer);
            _choice.Begin("Choose an upgrade", "Next: level " + (_levelIndex + 2) + " of " + config.LevelCount);
            for (int i = 0; i < _offer.Count; i++)
            {
                var d = _offer[i];
                var a = AssetFor(d.id);
                var def = d;
                _choice.AddCard(d.rarity.ToString().ToUpperInvariant(), RarityColor(d.rarity), d.displayName, d.description,
                    a != null ? a.iconText : "", a != null ? a.iconColor : Color.white, () => Pick(def));
            }
            _choice.AddFooter("Skip", () => { _choice.Close(); LoadLevel(_levelIndex + 1); });
            _choice.Show();
        }

        static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.38f, 0.68f, 1f);
                case Rarity.Epic: return new Color(0.78f, 0.5f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.79f, 0.29f);
                default: return new Color(0.82f, 0.85f, 0.9f);
            }
        }

        void Pick(UpgradeDef def)
        {
            if (_phase != Phase.Draft) return;
            if (!_inventory.Take(def)) _inventory.ForceTake(def);
            _draftsTaken++;
            if (def.effects != null)
                foreach (var effect in def.effects)
                {
                    if (effect == DoomStats.HealFull) player.Health.Heal(player.Health.Max);
                    else if (effect == DoomStats.FillAmmo) _arsenal.Ammo.FillAll();
                    else if (effect.StartsWith(DoomStats.HealPrefix, System.StringComparison.Ordinal)) player.Health.Heal(DoomStats.HealAmount(effect));
                }
            ApplyStats(true);
            _hud.SetUpgrades(_inventory, AssetFor);
            _choice.Close();
            GameRoot.Run.Set("Upgrades", _inventory.Owned.Count.ToString());
            LoadLevel(_levelIndex + 1);
        }

        /// <summary>Pushes the stat block into the player and guns. A higher max health also heals
        /// the difference.</summary>
        void ApplyStats(bool healGain)
        {
            float max = _stats.Get(DoomStats.MaxHealth);
            float before = player.Health.Max;
            player.Health.SetMax(max);
            if (healGain && max > before) player.Health.Heal(max - before);
            _arsenal.SetFireRate(_stats.Get(DoomStats.FireRate));
            _arsenal.Ammo.CapacityScale = _stats.Get(DoomStats.AmmoCapacity);
            player.SetSpeedScale(_stats.Get(DoomStats.MoveSpeed));
        }

        void Victory()
        {
            if (_phase == Phase.Finished) return;
            _phase = Phase.Finished;
            _hud.HideTally();
            RecordRun(true);
            GameRoot.Win();
        }

        void RecordRun(bool won)
        {
            var total = new LevelTally();
            foreach (var t in _finished) total.Add(t);
            if (!won && _phase != Phase.Tally) total.Add(_tally);
            GameRoot.Run.score = _score;
            GameRoot.Run.Set("Levels cleared", _finished.Count + " / " + config.LevelCount);
            GameRoot.Run.Set("Kills", total.kills + " / " + total.totalEnemies + " (" + total.KillPercent + "%)");
            GameRoot.Run.Set("Secrets", total.secrets + " / " + total.totalSecrets);
            GameRoot.Run.Set("Upgrades", _inventory.Owned.Count.ToString());
        }

        void OnPlayerDied()
        {
            if (_phase != Phase.Playing) return;
            _phase = Phase.Dying;
            _phaseTime = 0f;
            CountKills();
            PlaySound("lose", 0.6f);
            GameRoot.Hud.Message("You died.", 2f);
        }

        void FinishDeath()
        {
            if (_phase != Phase.Dying) return;
            _phase = Phase.Finished;
            RecordRun(false);
            GameRoot.Run.Set("Reached", "Level " + (_levelIndex + 1) + ": " + (_level != null ? _level.map.name : ""));
            GameRoot.Lose();
        }

        // ---------- Debug and smoke ----------

        /// <summary>Debug skip ("Next scene" in the debug menu): while playing, jumps to the exit and
        /// finishes the level; on the tally, continues; in the draft, takes the first card.
        /// Returns false when there's nothing to skip.</summary>
        public bool DebugSkip()
        {
            switch (_phase)
            {
                case Phase.Playing:
                    if (_level == null) return false;
                    player.Teleport(_level.ExitPosition + Vector3.up * 0.05f);
                    CompleteLevel();
                    return true;
                case Phase.Tally:
                    return _hud.ContinueTally();
                case Phase.Draft:
                    return _choice.Choose(0) || _choice.ChooseFooter(0);
            }
            return false;
        }

        /// <summary>Every weapon, a full stock of ammo and all three keys (debug menu).</summary>
        public void GiveAll()
        {
            if (_level == null) return;
            _arsenal.GiveAll();
            player.GiveAllKeys();
            if (player.Health != null) player.Health.Heal(player.Health.Max);
            GameRoot.Hud.Message("All weapons, keys and ammo.", 2f);
        }

        /// <summary>Spawns <paramref name="count"/> enemies (the first type unless given) in a ring around
        /// a point, facing its centre, awake unless <paramref name="alert"/> is false (tests, stress
        /// checks). Returns how many were placed.</summary>
        public int DebugSpawnEnemies(int count, Vector3 around, float radius, EnemyAsset asset = null, bool alert = true)
        {
            if (_level == null) return 0;
            asset = asset != null ? asset : config.enemies.Count > 0 ? config.enemies[0] : null;
            if (asset == null) return 0;
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var p = around + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, Mathf.Max(1f, radius), UnityEngine.AI.NavMesh.AllAreas)) continue;
                var e = DoomEnemy.Create(new EnemySpawn { asset = asset, position = hit.position, facing = a + Mathf.PI }, this, _level.thingsRoot, ++_nextEnemyId, _rng.Fork());
                if (alert) e.Alert();
                _enemies.Add(e);
                _tally.totalEnemies++;
                placed++;
            }
            return placed;
        }

        IEnumerator SmokeDrive()
        {
            bool god = DebugMenu.GodMode;
            DebugMenu.GodMode = true;
            Debug.Log("JAMKIT_SMOKE doomlike seed " + _seed + ", level " + (_levelIndex + 1) + " of " + config.LevelCount
                + ", " + _enemies.Count + " enemies, navmesh " + (_level != null && _level.navMeshBuilt));
            // Walk forward a moment: the controller moves.
            player.UseScriptedMove = true;
            player.ScriptedMove = new Vector2(0f, 1f);
            float walkEnd = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < walkEnd) yield return null;
            player.UseScriptedMove = false;
            Debug.Log("JAMKIT_SMOKE doomlike walked " + player.DistanceMoved.ToString("0.0") + " m");

            float end = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < end && GameRoot.Flow.Current != GameState.Victory && _phase != Phase.Finished)
            {
                if (GameRoot.Flow.Current == GameState.Playing) DebugSkip();
                float wait = Time.realtimeSinceStartup + 0.25f;
                while (Time.realtimeSinceStartup < wait) yield return null;
            }
            Debug.Log("JAMKIT_SMOKE doomlike reached " + (GameRoot.Flow.Current == GameState.Victory ? "victory" : "level " + (_levelIndex + 1) + " (" + _phase + ")")
                + " after " + _finished.Count + " level(s), " + _draftsTaken + " draft(s)");
            DebugMenu.GodMode = god;
        }
    }
}
