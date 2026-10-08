using System;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The template's HUD, added inside the shell's HUD (so it hides with it): the bottom bar
    /// (health, ammo, weapon slots, keys, ammo pools), crosshair and hit markers, damage direction
    /// hints, the level name card, the boss bar, owned upgrades, the "click to play" overlay and the
    /// end-of-level tally. The shell's own timer and score line stay top right; its message line
    /// shows pickups and "You need the blue key". Labels change only when their value does.
    /// Styles: Templates/Doomlike/UI/Doomlike.uss (it uses the shell's theme variables).
    /// </summary>
    public sealed class DoomHud
    {
        public readonly VisualElement Root;
        readonly Label _health, _ammo, _ammoType, _weapon;
        readonly VisualElement _healthBox, _slots, _keys, _pools, _upgrades;
        readonly List<Label> _slotLabels = new List<Label>();
        readonly List<VisualElement> _keyIcons = new List<VisualElement>();
        readonly List<Label> _poolLabels = new List<Label>();
        readonly VisualElement _cross, _hit, _boss, _bossFill, _card, _click, _tally, _tallyRows;
        readonly VisualElement[] _dmg = new VisualElement[4];
        readonly float[] _dmgAlpha = new float[4];
        readonly Label _cardTitle, _cardSub, _bossName, _tallyHeading, _tallySub;
        readonly Button _tallyContinue;
        Action _onContinue;
        float _cardLeft, _hitLeft;
        int _shownHealth = int.MinValue, _shownAmmo = int.MinValue, _shownSlotMask = -1, _shownCurrentSlot = -1, _shownKeys = -1;
        WeaponAsset _shownWeapon;
        readonly int[] _shownPools = new int[8];
        readonly int[] _shownPoolMax = new int[8];
        float _shownBoss = -1f;

        public bool CardShown => _cardLeft > 0f;
        public bool TallyShown => _tally.style.display == DisplayStyle.Flex;
        public bool ClickOverlayShown => _click.style.display == DisplayStyle.Flex;
        public bool BossShown => _boss.style.display == DisplayStyle.Flex;
        public string HealthText => _health.text;
        public string AmmoText => _ammo.text;
        public string WeaponText => _weapon.text;
        public int HitMarkers;

        public DoomHud(VisualElement parent, StyleSheet style)
        {
            Root = El("dl-hud");
            Root.name = "dl-hud";
            if (style != null) Root.styleSheets.Add(style);
            parent.Add(Root);

            _cross = El("dl-cross", Root);
            El("dl-cross-h", _cross);
            El("dl-cross-v", _cross);
            _hit = El("dl-hit", Root);
            El("dl-hit-a", _hit);
            El("dl-hit-b", _hit);
            _hit.style.opacity = 0f;

            string[] sides = { "dl-dmg--left", "dl-dmg--right", "dl-dmg--top", "dl-dmg--bottom" };
            for (int i = 0; i < 4; i++)
            {
                _dmg[i] = El("dl-dmg", Root);
                _dmg[i].AddToClassList(sides[i]);
                _dmg[i].style.opacity = 0f;
            }

            var bar = El("dl-bar", Root);
            _healthBox = Section(bar, "HEALTH");
            _health = Big(_healthBox);
            var ammoBox = Section(bar, "AMMO");
            _ammo = Big(ammoBox);
            _ammoType = Small(ammoBox);
            var weaponBox = Section(bar, "WEAPON");
            weaponBox.AddToClassList("dl-section--wide");
            _weapon = Small(weaponBox);
            _weapon.AddToClassList("dl-weapon-name");
            _slots = El("dl-slots", weaponBox);
            var keyBox = Section(bar, "KEYS");
            _keys = El("dl-keys", keyBox);
            var poolBox = Section(bar, "AMMO STOCK");
            _pools = El("dl-pools", poolBox);

            _upgrades = El("dl-upgrades", Root);

            _boss = El("dl-boss", Root);
            _bossName = new Label { pickingMode = PickingMode.Ignore };
            _bossName.AddToClassList("dl-boss-name");
            _boss.Add(_bossName);
            var bossBar = El("dl-boss-bar", _boss);
            _bossFill = El("dl-boss-fill", bossBar);
            _boss.style.display = DisplayStyle.None;

            _card = El("dl-card-name", Root);
            _cardTitle = new Label { pickingMode = PickingMode.Ignore };
            _cardTitle.AddToClassList("dl-card-name-title");
            _cardSub = new Label { pickingMode = PickingMode.Ignore };
            _cardSub.AddToClassList("dl-card-name-sub");
            _card.Add(_cardTitle);
            _card.Add(_cardSub);
            _card.style.display = DisplayStyle.None;

            _click = El("dl-click", Root);
            var clickText = new Label("CLICK TO PLAY") { pickingMode = PickingMode.Ignore };
            clickText.AddToClassList("dl-click-title");
            var clickSub = new Label("Mouse to look, WASD to move, left click to shoot. Esc pauses.") { pickingMode = PickingMode.Ignore };
            clickSub.AddToClassList("dl-click-sub");
            _click.Add(clickText);
            _click.Add(clickSub);
            _click.style.display = DisplayStyle.None;

            _tally = new VisualElement { name = "dl-tally", pickingMode = PickingMode.Position };
            _tally.AddToClassList("dl-tally");
            Root.Add(_tally);
            var tallyPanel = new VisualElement();
            tallyPanel.AddToClassList("jk-panel");
            tallyPanel.AddToClassList("dl-tally-panel");
            _tally.Add(tallyPanel);
            _tallyHeading = new Label();
            _tallyHeading.AddToClassList("jk-heading");
            _tallySub = new Label();
            _tallySub.AddToClassList("jk-text");
            _tallySub.AddToClassList("jk-dim");
            _tallyRows = new VisualElement();
            _tallyRows.AddToClassList("dl-tally-rows");
            _tallyContinue = new Button { text = "Continue" };
            _tallyContinue.AddToClassList("jk-button");
            _tallyContinue.AddToClassList("jk-button--primary");
            _tallyContinue.clicked += () =>
            {
                if (_tallyBlock > 0f) return;
                ContinueTally();
            };
            tallyPanel.Add(_tallyHeading);
            tallyPanel.Add(_tallySub);
            tallyPanel.Add(_tallyRows);
            tallyPanel.Add(_tallyContinue);
            _tally.style.display = DisplayStyle.None;
        }

        float _tallyBlock;

        static VisualElement El(string cls, VisualElement parent = null)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent?.Add(e);
            return e;
        }

        static VisualElement Section(VisualElement bar, string title)
        {
            var s = El("dl-section", bar);
            var k = new Label(title) { pickingMode = PickingMode.Ignore };
            k.AddToClassList("dl-kicker");
            s.Add(k);
            return s;
        }

        static Label Big(VisualElement parent)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("dl-big");
            parent.Add(l);
            return l;
        }

        static Label Small(VisualElement parent)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("dl-small");
            parent.Add(l);
            return l;
        }

        public void Remove() => Root.RemoveFromHierarchy();

        // ---------- Values ----------

        public void Refresh(DoomPlayer player, PlayerArsenal arsenal, DoomlikeConfig config)
        {
            int hp = player.Health != null ? Mathf.CeilToInt(player.Health.Current) : 0;
            if (hp != _shownHealth)
            {
                _shownHealth = hp;
                _health.text = hp.ToString();
                float max = player.Health != null ? player.Health.Max : 100f;
                _health.EnableInClassList("dl-big--low", hp <= max * 0.25f);
            }

            var gun = arsenal.Current;
            var w = gun != null ? gun.asset : null;
            int ammo = gun == null ? 0 : gun.ammo < 0 ? -1 : arsenal.Ammo.Amount(gun.ammo);
            if (ammo != _shownAmmo || w != _shownWeapon)
            {
                _shownAmmo = ammo;
                _ammo.text = ammo < 0 ? "--" : ammo.ToString();
                _ammoType.text = gun != null && gun.ammo >= 0 ? arsenal.Ammo.Type(gun.ammo).displayName : "";
                _ammo.EnableInClassList("dl-big--low", ammo >= 0 && ammo <= Mathf.Max(1, gun.live.ammoPerShot) * 3);
            }
            if (w != _shownWeapon)
            {
                _shownWeapon = w;
                _weapon.text = w != null ? w.displayName : "";
            }

            int mask = 0, current = w != null ? w.slot : -1;
            var guns = arsenal.Guns;
            for (int i = 0; i < guns.Count; i++) if (guns[i].owned) mask |= 1 << Mathf.Clamp(guns[i].asset.slot, 0, 30);
            if (mask != _shownSlotMask || current != _shownCurrentSlot)
            {
                _shownSlotMask = mask;
                _shownCurrentSlot = current;
                BuildSlots(guns, mask, current);
            }

            if (player.Keys != _shownKeys)
            {
                _shownKeys = player.Keys;
                BuildKeys(player.Keys, config);
            }

            var pools = arsenal.Ammo;
            if (_poolLabels.Count != pools.Count) BuildPools(pools);
            for (int i = 0; i < pools.Count && i < _shownPools.Length; i++)
            {
                int a = pools.Amount(i), m = pools.Max(i);
                if (a == _shownPools[i] && m == _shownPoolMax[i]) continue;
                _shownPools[i] = a;
                _shownPoolMax[i] = m;
                _poolLabels[i].text = pools.Type(i).shortName + "  " + a + " / " + m;
            }
        }

        void BuildSlots(IReadOnlyList<PlayerArsenal.Gun> guns, int mask, int current)
        {
            _slots.Clear();
            _slotLabels.Clear();
            var seen = new HashSet<int>();
            for (int i = 0; i < guns.Count; i++)
            {
                int slot = guns[i].asset.slot;
                if (!seen.Add(slot)) continue;
                var l = new Label(slot.ToString()) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("dl-slot");
                if ((mask & (1 << Mathf.Clamp(slot, 0, 30))) != 0) l.AddToClassList("dl-slot--owned");
                if (slot == current) l.AddToClassList("dl-slot--current");
                _slots.Add(l);
                _slotLabels.Add(l);
            }
        }

        void BuildKeys(int keys, DoomlikeConfig config)
        {
            _keys.Clear();
            _keyIcons.Clear();
            for (int c = 0; c < 3; c++)
            {
                var k = El("dl-key", _keys);
                Color col = config.KeyColor(c);
                bool has = (keys & (1 << c)) != 0;
                k.style.backgroundColor = has ? col : new Color(0f, 0f, 0f, 0.35f);
                k.style.borderTopColor = col;
                k.style.borderBottomColor = col;
                k.style.borderLeftColor = col;
                k.style.borderRightColor = col;
                _keyIcons.Add(k);
            }
        }

        void BuildPools(AmmoPools pools)
        {
            _pools.Clear();
            _poolLabels.Clear();
            for (int i = 0; i < pools.Count; i++)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.AddToClassList("dl-pool");
                _pools.Add(l);
                _poolLabels.Add(l);
                if (i < _shownPools.Length) _shownPools[i] = int.MinValue;
            }
        }

        public void SetUpgrades(UpgradeInventory inventory, Func<string, DoomUpgradeAsset> assetFor)
        {
            _upgrades.Clear();
            if (inventory == null) return;
            foreach (var def in inventory.Owned)
            {
                var a = assetFor(def.id);
                var icon = new Label(a != null ? a.iconText : def.id.Substring(0, Math.Min(2, def.id.Length)).ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("dl-upgrade");
                Color c = a != null ? a.iconColor : Color.white;
                icon.style.color = c;
                icon.style.borderTopColor = c;
                icon.style.borderBottomColor = c;
                icon.style.borderLeftColor = c;
                icon.style.borderRightColor = c;
                int n = inventory.Count(def.id);
                if (n > 1)
                {
                    var count = new Label("x" + n) { pickingMode = PickingMode.Ignore };
                    count.AddToClassList("dl-upgrade-count");
                    icon.Add(count);
                }
                _upgrades.Add(icon);
            }
        }

        // ---------- Feedback ----------

        public void HitMarker(bool kill)
        {
            HitMarkers++;
            _hitLeft = kill ? 0.28f : 0.12f;
            _hit.EnableInClassList("dl-hit--kill", kill);
            _hit.style.opacity = 1f;
        }

        /// <summary>A red edge toward where the hit came from: <paramref name="relAngle"/> is the
        /// source's angle from the view direction (radians, positive = to the left).</summary>
        public void DamageFrom(float relAngle, float strength)
        {
            float a = Mathf.Clamp01(strength);
            float deg = relAngle * Mathf.Rad2Deg;
            int side;
            if (deg > 45f && deg < 135f) side = 0;
            else if (deg < -45f && deg > -135f) side = 1;
            else if (Mathf.Abs(deg) <= 45f) side = 2;
            else side = 3;
            _dmgAlpha[side] = Mathf.Max(_dmgAlpha[side], a);
        }

        public void ShowCard(string title, string sub, float seconds)
        {
            _cardTitle.text = title ?? "";
            _cardSub.text = sub ?? "";
            _cardSub.style.display = string.IsNullOrEmpty(sub) ? DisplayStyle.None : DisplayStyle.Flex;
            _cardLeft = seconds;
            _card.style.display = DisplayStyle.Flex;
            _card.style.opacity = 1f;
        }

        public void HideCard()
        {
            _cardLeft = 0f;
            _card.style.display = DisplayStyle.None;
        }

        public void SetBoss(string name, float fraction, bool shown)
        {
            if (!shown)
            {
                if (_boss.style.display != DisplayStyle.None) _boss.style.display = DisplayStyle.None;
                _shownBoss = -1f;
                return;
            }
            if (_boss.style.display != DisplayStyle.Flex)
            {
                _boss.style.display = DisplayStyle.Flex;
                _bossName.text = name ?? "";
            }
            float f = Mathf.Clamp01(fraction);
            if (Mathf.Abs(f - _shownBoss) < 0.002f) return;
            _shownBoss = f;
            _bossFill.style.width = Length.Percent(f * 100f);
        }

        public void SetClickOverlay(bool shown)
        {
            var d = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (_click.style.display != d) _click.style.display = d;
        }

        public void SetCrosshair(bool shown)
        {
            var d = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (_cross.style.display != d) _cross.style.display = d;
        }

        // ---------- Tally ----------

        public void ShowTally(LevelTally t, string heading, string continueText, Action onContinue)
        {
            _onContinue = onContinue;
            _tallyHeading.text = heading;
            _tallySub.text = t.levelName;
            _tallyRows.Clear();
            Row("Time", RunStats.FormatTime(t.time) + (t.parSeconds > 0f ? "   (par " + RunStats.FormatTime(t.parSeconds) + ")" : ""), t.UnderPar);
            Row("Kills", t.KillPercent + "%   " + t.kills + " / " + t.totalEnemies, t.KillPercent >= 100);
            Row("Secrets", t.SecretPercent + "%   " + t.secrets + " / " + t.totalSecrets, t.totalSecrets > 0 && t.SecretPercent >= 100);
            Row("Items", t.ItemPercent + "%   " + t.items + " / " + t.totalItems, t.totalItems > 0 && t.ItemPercent >= 100);
            _tallyContinue.text = continueText;
            _tally.style.display = DisplayStyle.Flex;
            _tally.BringToFront();
            _tallyBlock = 0.4f;
            _tallyContinue.Focus();
        }

        void Row(string label, string value, bool good)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("dl-tally-row");
            var l = new Label(label) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("dl-tally-label");
            var v = new Label(value) { pickingMode = PickingMode.Ignore };
            v.AddToClassList("dl-tally-value");
            if (good) v.AddToClassList("dl-tally-value--good");
            row.Add(l);
            row.Add(v);
            _tallyRows.Add(row);
        }

        /// <summary>Presses Continue (tests, the smoke run, debug skip).</summary>
        public bool ContinueTally()
        {
            if (!TallyShown) return false;
            _tally.style.display = DisplayStyle.None;
            _tallyContinue.Blur();
            var next = _onContinue;
            _onContinue = null;
            GameRoot.Audio.Play("ui_select");
            next?.Invoke();
            return true;
        }

        public void HideTally()
        {
            _tally.style.display = DisplayStyle.None;
            _onContinue = null;
        }

        public void Tick(float unscaledDt)
        {
            if (_cardLeft > 0f)
            {
                _cardLeft -= unscaledDt;
                _card.style.opacity = Mathf.Clamp01(_cardLeft / 0.6f);
                if (_cardLeft <= 0f) _card.style.display = DisplayStyle.None;
            }
            if (_hitLeft > 0f)
            {
                _hitLeft -= unscaledDt;
                if (_hitLeft <= 0f) _hit.style.opacity = 0f;
            }
            for (int i = 0; i < 4; i++)
            {
                if (_dmgAlpha[i] <= 0f) continue;
                _dmgAlpha[i] = Mathf.Max(0f, _dmgAlpha[i] - unscaledDt * 1.6f);
                _dmg[i].style.opacity = _dmgAlpha[i];
            }
            if (_tallyBlock > 0f) _tallyBlock -= unscaledDt;
            if (TallyShown && GameRoot.Flow.Current == GameState.Playing && GameRoot.Ui.Top == null)
            {
                var f = _tally.focusController?.focusedElement;
                if (f != _tallyContinue) _tallyContinue.Focus();
            }
        }
    }
}
