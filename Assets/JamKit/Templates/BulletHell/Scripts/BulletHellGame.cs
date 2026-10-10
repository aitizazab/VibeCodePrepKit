using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Dialogue;
using JamKit.Flow;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The bullet-hell roguelite: one run of 3 floors (core RunGenerator from the config's RunConfig),
    /// room by room. Battle and Elite rooms play waves, then a draft from their reward table; Treasure
    /// is a free draft; Rest heals or trains; Shop sells upgrades, lives and bombs; Mystery plays an ink
    /// event whose tags change the run; Boss is the floor's boss, then a guaranteed choice of epics.
    /// The encounter rooms (Forge, Altar, Wager, Trial board, Trader) are in BulletHellGame.Encounters.cs,
    /// the companion in BulletHellGame.Companion.cs. Doors appear when the plan forks. Clearing the last
    /// boss wins (GameRoot.Win), losing every life loses (GameRoot.Lose) and leaves a remnant.
    /// This component owns the per-frame order: player, waves, enemies, boss, bullets, collisions.
    /// Debug "Next scene" (and the smoke run) skip the current room, kill the boss or take a choice.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed partial class BulletHellGame : MonoBehaviour
    {
        public enum RoomPhase { Starting, Intro, Waves, Boss, Cleared, Choosing, Dialogue, Finished }

        [Header("Data")]
        public BulletHellConfig config;
        [Tooltip("Templates/BulletHell/UI/BulletHell.uss")]
        public StyleSheet hudStyle;

        [Header("Scene (set by the builder)")]
        public BulletField field;
        public FxField fx;
        public CoinField coins;
        public ShmupPlayer player;
        public ShmupBoss boss;
        public SpriteRenderer playfield;
        public Transform enemyRoot;
        public Material spriteMaterial;
        public Camera cam;

        static ulong _seedOverride;
        static BulletHellGame _current;

        /// <summary>Seed for the next run (tests, bug reports). 0 = a new random seed each run.</summary>
        public static ulong SeedOverride
        {
            get => _seedOverride;
            set => _seedOverride = value;
        }

        /// <summary>The running game, if a bullet-hell scene is loaded.</summary>
        public static BulletHellGame Current => _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _seedOverride = 0;
            _current = null;
        }

        struct Pending
        {
            public float time;
            public EnemyDef def;
            public Vector2 from, anchor;
        }

        enum ShopKind { Upgrade, Life, Bomb }

        sealed class ShopItem
        {
            public ShopKind kind;
            public UpgradeDef def;
            public int price;
            public bool sold;
        }

        RunState _run;
        readonly ShmupLoadout _loadout = new ShmupLoadout();
        RewardDraw _draft, _shopDraft;
        UpgradeDef _fallback;
        Rng _draftRng, _roomRng;
        readonly List<UpgradeDef> _pool = new List<UpgradeDef>();
        readonly Dictionary<string, UpgradeAsset> _assets = new Dictionary<string, UpgradeAsset>();
        readonly List<UpgradeDef> _offer = new List<UpgradeDef>();
        readonly List<ShopItem> _shop = new List<ShopItem>();
        PlayfieldBounds _bounds;

        RoomPhase _phase = RoomPhase.Starting;
        float _phaseTime;
        RoomType _roomType;
        int _lastFloor = -1;

        RoomDef _room;
        int _waveIndex;
        float _waveTime, _waveGap;
        bool _wavesDone;
        readonly List<Pending> _pending = new List<Pending>(64);
        readonly List<ShmupEnemy> _enemies = new List<ShmupEnemy>(64);
        readonly Stack<ShmupEnemy> _enemyPool = new Stack<ShmupEnemy>();
        int _nextEnemyId = 1;

        HitTarget[] _targets = new HitTarget[64];
        ShmupEnemy[] _owners = new ShmupEnemy[64];
        readonly List<BulletHit> _hits = new List<BulletHit>(128);

        int _score, _grazes, _rerolls, _kills, _bossesBeaten, _draftsTaken, _hitsTaken, _roomsCleared;
        float _grazeHeal, _bombTimer, _sfxGraze, _sfxHit, _sfxCoin, _scoreTextClock;
        bool _scoreDirty;

        RewardTable _draftTable;
        RewardSource _draftSource;
        readonly RewardTable _filteredTable = new RewardTable();
        float _draftLuck;
        string _draftHeading, _draftSub;
        Action _afterChoice;
        bool _eventActive, _pendingDraft, _firstDraftOfRun;
        Rarity _pendingDraftMin;

        // Reward screens waiting after a room (a trial's epic, then the room's own draft).
        struct PendingReward
        {
            public RewardSource source;
            public string heading, sub;
        }
        readonly List<PendingReward> _rewardQueue = new List<PendingReward>();

        // Trials and wagers in progress; boosts; remnants of earlier runs.
        readonly List<TrialTracker> _trials = new List<TrialTracker>();
        readonly PowerBoosts _boosts = new PowerBoosts();
        int _grazesLastFrame;
        float _trialHudClock;
        RemnantBook _remnantBook = new RemnantBook();
        /// <summary>Save entry the remnants live under (SaveData.entries).</summary>
        public const string RemnantSaveKey = "bh_remnants";
        readonly List<string> _encounterLog = new List<string>();
        readonly List<RewardSource> _draftSources = new List<RewardSource>();

        BulletHellHud _hud;
        ChoicePanel _choice;
        Func<bool> _skipHandler;
        Func<IEnumerator> _smokeDriver;
        Action<Ink.Runtime.Story> _bindInk;

        // Story hooks (ink knots looked up by name; see docs/TEMPLATE-BULLETHELL.md "Story hooks").
        Ink.Runtime.Story _hookProbe;
        Func<string, bool> _hasKnot;
        Action _afterLine;
        bool _lineActive, _runStartPending;
        string _killer = "";
        readonly List<string> _storyKnotsPlayed = new List<string>();

        // The first-run Sprint hint (saved as a flag once shown).
        public const string SprintHintFlag = "bh_hint_sprint_seen";
        readonly FirstTimeHint _sprintHint = new FirstTimeHint();

        // ---------- Read-only state (tests, debug, HUD) ----------

        public RunState Run => _run;
        public ShmupLoadout Loadout => _loadout;
        public ChoicePanel Choice => _choice;
        public BulletHellHud Hud => _hud;
        public RoomPhase Phase => _phase;
        public RoomType RoomType => _roomType;
        public int Score => _score;
        public int Grazes => _grazes;
        public int Kills => _kills;
        public int BossesDefeated => _bossesBeaten;
        public int DraftsTaken => _draftsTaken;
        public int RoomsCleared => _roomsCleared;
        public int EnemiesAlive => CountAlive();
        public PlayfieldBounds Bounds => _bounds;
        public UpgradeAsset AssetFor(string id) => id != null && _assets.TryGetValue(id, out var a) ? a : null;
        /// <summary>A story-hook line (boss, rest, run start) is on screen.</summary>
        public bool InStoryLine => _lineActive;
        /// <summary>The run-start line hasn't been tried yet (it waits for the scene's fade-in).</summary>
        public bool StoryLinePending => _runStartPending;
        /// <summary>Every story-hook knot played this run, in order (tests, the smoke log).</summary>
        public IReadOnlyList<string> StoryKnotsPlayed => _storyKnotsPlayed;
        public FirstTimeHint SprintHint => _sprintHint;
        /// <summary>Who ended the run, as the death hooks see it: the boss's story id in a boss fight, else "elite" or "battle".</summary>
        public string Killer => _killer;
        /// <summary>Trials and wagers running now (cleared when they pass or fail).</summary>
        public IReadOnlyList<TrialTracker> Trials => _trials;
        /// <summary>The remnants saved from earlier runs (newest last).</summary>
        public RemnantBook SavedRemnants => _remnantBook;
        /// <summary>What happened in encounter rooms this run, in order ("forge: pushed Sharp Rounds +1"): tests, the smoke log.</summary>
        public IReadOnlyList<string> EncounterLog => _encounterLog;
        /// <summary>The reward source of every draft opened this run, in order.</summary>
        public IReadOnlyList<RewardSource> DraftSources => _draftSources;
        /// <summary>The source of the draft on screen (or the last one).</summary>
        public RewardSource CurrentDraftSource => _draftSource;
        /// <summary>The cards of the draft or shop on screen.</summary>
        public IReadOnlyList<UpgradeDef> Offer => _offer;
        public PowerBoosts Boosts => _boosts;

        // ---------- Setup ----------

        void Start()
        {
            if (!GameRoot.Exists || config == null || field == null || player == null || boss == null)
            {
                Debug.LogError("BulletHellGame: missing config or scene parts. Rebuild with JamKit > Rebuild Shell Scenes.");
                enabled = false;
                return;
            }
            _current = this;
            // A 2D shmup steers with the d-pad as well as the stick (gameplay Move has no d-pad by default).
            GameRoot.Input.Bindings.SetMoveDpad(true);
            var half = config.playfieldHalfSize;
            _bounds = PlayfieldBounds.Centered(half.x, half.y);
            field.Configure(config.bulletStyles, _bounds);
            if (coins != null) coins.color = config.coinColor;
            if (fx != null) fx.SetStarArea(new Vector2(half.x, half.y), new Color(0.6f, 0.75f, 1f, 0.5f), 1f);
            if (cam == null) cam = Camera.main;

            foreach (var a in config.upgrades)
            {
                if (a == null || a.def == null || string.IsNullOrEmpty(a.def.id)) continue;
                _assets[a.def.id] = a;
                // Companion upgrades only exist when the companion does.
                if (!CompanionOn && a.def.tags != null && a.def.tags.Contains(CompanionTag)) continue;
                _pool.Add(a.def);
            }
            _fallback = config.fallbackUpgrade != null ? config.fallbackUpgrade.def : null;
            if (_fallback != null && !string.IsNullOrEmpty(_fallback.id)) _assets[_fallback.id] = config.fallbackUpgrade;
            _draft = new RewardDraw(_fallback);
            _shopDraft = new RewardDraw(null);
            LoadRemnants();

            GameRoot.Hud.Show();
            _hud = new BulletHellHud(GameRoot.Hud.Root, hudStyle, GameRoot.Config.showControlLegend);
            _choice = new ChoicePanel(_hud.Root);

            // One parsed copy of the ink, only to ask which hook knots exist.
            if (config.events != null && config.events.isCompiled)
            {
                try { _hookProbe = new Ink.Runtime.Story(config.events.storyJson); }
                catch (Exception e) { Debug.LogWarning("BulletHellGame: story hooks off, the ink didn't load (" + e.Message + ")."); }
            }
            _hasKnot = HasKnot;
            _bindInk = BindInk;

            GameRoot.Dialogue.EventRaised += OnDialogueEvent;
            GameRoot.Dialogue.Ended += OnDialogueEnded;
            GameRoot.Dialogue.StoryCreated += _bindInk;
            boss.PhaseChanged += OnBossPhase;
            boss.Dying += OnBossDying;
            boss.Defeated += OnBossDefeated;
            _skipHandler = DebugSkip;
            DebugMenu.SkipHandler = _skipHandler;
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            SetupPresenter();
            CreateCompanion();

            BeginRun();
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            if (!GameRoot.Exists) return;
            GameRoot.Input.Bindings.SetMoveDpad(false);
            GameRoot.Dialogue.EventRaised -= OnDialogueEvent;
            GameRoot.Dialogue.Ended -= OnDialogueEnded;
            GameRoot.Dialogue.StoryCreated -= _bindInk;
            if (DebugMenu.SkipHandler == _skipHandler) DebugMenu.SkipHandler = null;
            if (SmokeRunner.GameplayDriver == _smokeDriver) SmokeRunner.GameplayDriver = null;
            TeardownPresenter();
            _hud?.Remove();
        }

        void BeginRun()
        {
            ulong seed = _seedOverride != 0 ? _seedOverride : Rng.TimeSeed();
            _run = RunState.Begin(config.run, seed);
            _draftRng = new Rng(seed ^ 0x5EEDD4AF7UL);
            _loadout.Reset();
            var def = config.player;
            if (def.baseStats != null)
                foreach (var s in def.baseStats)
                    if (s != null && !string.IsNullOrEmpty(s.stat)) _loadout.Stats.SetBase(s.stat, s.value);
            int lives = def.lives + Mathf.Max(0, GameRoot.Settings.extraLives);
            player.BeginRun(def, _loadout, lives);
            player.Health.Died += OnPlayerDied;
            _score = _grazes = _rerolls = _kills = _bossesBeaten = _draftsTaken = _hitsTaken = _roomsCleared = 0;
            _grazeHeal = 0f;
            _grazesLastFrame = 0;
            _trials.Clear();
            _rewardQueue.Clear();
            _encounterLog.Clear();
            _draftSources.Clear();
            _boosts.Reset();
            EpicsBeginRun();
            _firstDraftOfRun = true;
            _hud.SetTrials(_trials);
            BeginCompanion();
            _lastFloor = -1;
            GameRoot.Run.score = 0;
            _scoreDirty = true;
            _hud.SetUpgrades(_loadout.Inventory.Owned, _loadout.Inventory, AssetFor, config);
            _storyKnotsPlayed.Clear();
            _lineActive = false;
            _afterLine = null;
            _killer = "";
            _sprintHint.Reset();
            _hud.SetSprintHint(0f);
            _runStartPending = true; // played on the first playing frame, after the fade-in
            EnterRoom();
        }

        // ---------- Frame ----------

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            _hud?.Tick(udt);
            _choice?.Tick(udt);
            if (_run == null || _phase == RoomPhase.Finished || !GameRoot.Exists) return;
            if (GameRoot.Flow.Current != GameState.Playing || GameRoot.IsLoadingScene) return;
            if (_runStartPending)
            {
                _runStartPending = false;
                if (PlayStoryLine(StoryHooks.RunStart(GameRoot.Save.Data.runsStarted), null)) return;
            }
            float dt = Time.deltaTime;
            _run.Tick(dt);
            _phaseTime += dt;

            bool fighting = _phase == RoomPhase.Intro || _phase == RoomPhase.Waves || _phase == RoomPhase.Boss || _phase == RoomPhase.Cleared;
            player.ControlsEnabled = fighting && !_choice.IsOpen;
            // The moment-to-moment epics (Ember Hunger's heat, Still Point's stillness, Last Gambit on the last
            // life) set the gun's boost.
            _boosts.Tick(dt, config.boosts, _loadout.Effect(ShmupEffects.GrazePower), _loadout.Effect(ShmupEffects.LastStand),
                _grazesLastFrame, player.Health.Current <= 1.01f, _loadout.Effect(ShmupEffects.StillPoint), player.Moving || !player.ControlsEnabled);
            _grazesLastFrame = 0;
            _loadout.DamageBoost = _boosts.DamageMultiplier;
            _loadout.FireRateBoost = _boosts.FireRateMultiplier;
            player.Tick(dt, field, _bounds);
            var sprint = player.Sprint;
            if (player.Sprinting)
                for (int i = 0; i < _trials.Count; i++) _trials[i].Sprinted();
            if (sprint.StaminaEmptiedThisTick || sprint.BudgetEmptiedThisTick)
            {
                // Out of breath (or out of this room's sprint): flash the meter, a soft "empty" sound.
                _hud.FlashSprint(sprint.BudgetEmpty);
                GameRoot.Audio.Play("ui_back", 0.5f);
            }
            if (_sprintHint.IsVisible)
            {
                _sprintHint.Tick(dt, player.Sprinting);
                _hud.SetSprintHint(_sprintHint.Alpha);
            }
            var input = GameRoot.Input;
            // Bombs only while there's something to bomb (and never on the press that closed a menu).
            bool inFight = _phase == RoomPhase.Waves || _phase == RoomPhase.Boss;
            bool canBomb = inFight && player.ControlsEnabled;
            if (canBomb && (input.Pressed(input.AltFire) || input.Pressed(input.Dash)) && player.TryUseBomb())
                TriggerBomb(false);
            // The Bomb Bay recharges while fighting.
            if (player.TickBombs(dt, inFight)) GameRoot.Audio.Play("coin", 0.5f);
            if (_phase == RoomPhase.Waves)
                for (int i = 0; i < _trials.Count; i++) _trials[i].Tick(dt);

            switch (_phase)
            {
                case RoomPhase.Intro:
                    if (_phaseTime >= config.roomIntroSeconds) StartRoomContent();
                    break;
                case RoomPhase.Waves:
                    TickWaves(dt);
                    break;
                case RoomPhase.Cleared:
                    if (_phaseTime >= config.roomClearedSeconds) FinishCleared();
                    break;
            }

            Vector2 pp = player.Position;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.Alive) continue;
                e.Tick(dt, pp, field, _bounds);
                if (e.IsGone) e.Despawn();
            }
            if (boss.IsActive) boss.Tick(dt, pp);

            int targets = FillTargets();
            TickCompanion(dt, pp, targets);
            TickEpics(dt, pp, targets);
            field.Player.SteerHoming(_targets, targets, config.player.homingTurnRate, dt);
            field.Tick(dt);
            // Time Dilation: the bullets close to the ship move slower.
            if (_loadout.Effect(ShmupEffects.FocusSlow) > 0)
                field.Enemy.SlowNear(new System.Numerics.Vector2(pp.x, pp.y), config.boosts.slowRadius, config.boosts.slowScale, dt);
            Collide(targets, pp, dt);
            Recycle();

            if (_bombTimer > 0f)
            {
                _bombTimer -= dt;
                ClearEnemyBullets(2);
            }
            if (coins != null)
            {
                int got = coins.Tick(dt, pp, _loadout.MagnetScale, _bounds.minY);
                if (got > 0) AddCoins(got, false);
            }
            UpdateHud(udt);
        }

        int CountAlive()
        {
            int n = 0;
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Alive) n++;
            return n;
        }

        int FillTargets()
        {
            int needed = _enemies.Count + 1;
            if (_targets.Length < needed)
            {
                Array.Resize(ref _targets, needed * 2);
                Array.Resize(ref _owners, needed * 2);
            }
            int n = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.Alive) continue;
                var p = e.Position;
                _targets[n] = new HitTarget
                {
                    position = new System.Numerics.Vector2(p.x, p.y),
                    radius = e.Radius,
                    id = e.Id,
                    active = _bounds.Contains(new System.Numerics.Vector2(p.x, p.y), 0.2f),
                };
                _owners[n] = e;
                n++;
            }
            if (boss.Current == ShmupBoss.State.Fighting)
            {
                var b = boss.Position;
                _targets[n] = new HitTarget { position = new System.Numerics.Vector2(b.x, b.y), radius = boss.Radius, id = -1, active = true };
                _owners[n] = null;
                n++;
            }
            return n;
        }

        void Collide(int targetCount, Vector2 pp, float dt)
        {
            // Player shots against enemies and the boss.
            field.Player.CollideTargets(_targets, targetCount, _hits);
            _sfxHit -= dt;
            for (int i = 0; i < _hits.Count; i++)
            {
                var h = _hits[i];
                var owner = _owners[h.targetIndex];
                float damage = HitDamage(h);
                if (owner == null)
                {
                    bool hurt = boss.CanBeHurt;
                    boss.Damage(damage);
                    if (!hurt && fx != null) fx.Spark(new Vector2(h.position.X, h.position.Y), new Color(0.6f, 0.6f, 0.7f, 0.7f), new Vector2(0f, 2f), 0.08f, 0.15f);
                }
                else if (owner.Damage(damage)) KillEnemy(owner);
                AfterHit(h, damage, targetCount);
                if (fx != null && (i & 1) == 0) fx.Spark(new Vector2(h.position.X, h.position.Y), new Color(1f, 1f, 0.8f, 0.9f), new Vector2(0f, 3f), 0.1f, 0.12f);
                if (_sfxHit <= 0f)
                {
                    _sfxHit = 0.07f;
                    GameRoot.Audio.Play("enemy_hit", 0.35f);
                }
            }

            // Enemy bullets and bodies against the player.
            float hitR = player.HitboxRadius;
            var ppn = new System.Numerics.Vector2(pp.x, pp.y);
            int grazed = field.Enemy.Graze(ppn, player.GrazeRadius, hitR);
            if (grazed > 0) OnGraze(grazed, pp, dt);
            if (player.IsInvulnerable || DebugMenu.PlayerInvincible || _bombTimer > 0f) return;
            // Ghost Step: during a phase (the start of a sprint), bullets pass through (bodies still hurt).
            bool ghost = _loadout.Effect(ShmupEffects.GhostStep) > 0 && _ghostPhase.Phasing;
            bool hit = !ghost && field.Enemy.FirstOverlap(ppn, hitR) >= 0;
            if (!hit)
                for (int i = 0; i < _enemies.Count && !hit; i++)
                {
                    var e = _enemies[i];
                    if (!e.Alive || !e.Def.hurtsOnContact) continue;
                    float r = e.Radius * 0.8f + hitR;
                    if ((e.Position - pp).sqrMagnitude < r * r) hit = true;
                }
            if (!hit && boss.Current == ShmupBoss.State.Fighting)
            {
                float r = boss.Radius * 0.8f + hitR;
                if ((boss.Position - pp).sqrMagnitude < r * r) hit = true;
            }
            if (hit) OnPlayerHit();
        }

        void Recycle()
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                var e = _enemies[i];
                if (e.Alive) continue;
                _enemies.RemoveAt(i);
                _enemyPool.Push(e);
            }
        }

        void UpdateHud(float udt)
        {
            var h = player.Health;
            _hud.SetLives(h.Current, h.Max);
            _hud.SetBombs(player.Bombs, player.MaxBombs, player.Bay.RechargeFraction);
            _hud.SetCoins(_run.currency);
            _hud.SetGraze(_grazes);
            _hud.SetSprint(player.Sprint);
            UpdateAbilityHud();
            _hud.SetGuard(CompanionActive, _companion.Down ? 0f : _companion.GuardFraction, _companion.GuardReady && !_companion.Down);
            _trialHudClock -= udt;
            if (_trials.Count > 0 && _trialHudClock <= 0f)
            {
                _trialHudClock = 0.25f;
                _hud.SetTrials(_trials);
            }
            if (boss.IsActive) _hud.SetBossHealth(boss.HealthFraction);
            _scoreTextClock -= udt;
            if (_scoreDirty && _scoreTextClock <= 0f)
            {
                _scoreDirty = false;
                _scoreTextClock = 0.1f;
                GameRoot.Hud.SetScoreText("Score " + _score);
            }
        }

        // ---------- Rooms ----------

        FloorDef Floor => config.Floor(_run.floor);

        void EnterRoom()
        {
            _choice.Close();
            _roomType = _run.CurrentRoom;
            _phase = RoomPhase.Intro;
            _phaseTime = 0f;
            _room = null;
            _pending.Clear();
            _wavesDone = false;
            _roomRng = new Rng(_run.RoomSeed());
            // Each fight room has its own sprint budget (the HUD meter fills back up).
            if (RoomText.IsFight(_roomType)) { player.RefillSprint(); EpicsRoomStarted(); }
            if (_roomType == RoomType.Combat || _roomType == RoomType.Elite)
                for (int i = 0; i < _trials.Count; i++) _trials[i].RoomStarted();
            field.Enemy.Clear();
            if (coins != null) coins.attractAll = false;

            var floor = Floor;
            if (_run.floor != _lastFloor)
            {
                _lastFloor = _run.floor;
                if (cam != null) cam.backgroundColor = config.sideColor;
                if (playfield != null && floor != null) playfield.color = floor.background;
                if (fx != null && floor != null) fx.SetStarColor(new Color(floor.accent.r, floor.accent.g, floor.accent.b, 0.45f));
                if (floor != null) _hud.NameCard("FLOOR " + (_run.floor + 1), floor.name, 2.2f);
                if (_run.floor > 0) player.FillBombs();
            }
            _hud.SetMap(_run, config);
            string where = "Floor " + (_run.floor + 1) + " - Room " + (_run.step + 1) + ": " + RoomText.Name(_roomType);
            GameRoot.Hud.Message(_roomType == RoomType.Boss ? where + "   Get ready!" : where, config.roomIntroSeconds + 0.6f);
            GameRoot.Audio.PlayMusic(_roomType == RoomType.Boss ? config.bossMusic : GameRoot.Config.gameMusic);
        }

        void StartRoomContent()
        {
            _phaseTime = 0f;
            switch (_roomType)
            {
                case RoomType.Combat:
                case RoomType.Elite:
                    _room = PickRoom(_roomType);
                    _phase = RoomPhase.Waves;
                    _waveIndex = -1;
                    _waveGap = 0f;
                    NextWave();
                    StartSprintHint();
                    break;
                case RoomType.Boss:
                    StartBoss();
                    break;
                case RoomType.Treasure:
                    OpenDraft("Treasure!", "Pick one upgrade to keep for this run.", RewardSource.Treasure, Leave);
                    break;
                case RoomType.Rest:
                    // The companion's room: a line first (rest_floor_<n>, rest), then the choice. Resting
                    // revives a companion that's down.
                    if (CompanionActive && _companion.Down) _companion.Revive();
                    _phase = RoomPhase.Dialogue;
                    if (!PlayStoryLine(StoryHooks.Rest(_run.floor + 1), OpenRest)) OpenRest();
                    break;
                case RoomType.Shop:
                    OpenShop();
                    break;
                case RoomType.Event:
                    StartEvent();
                    break;
                case RoomType.Forge:
                case RoomType.Altar:
                case RoomType.Wager:
                case RoomType.Trial:
                case RoomType.Trader:
                    StartEncounter(_roomType);
                    break;
                default:
                    Leave();
                    break;
            }
        }

        RoomDef PickRoom(RoomType type)
        {
            var floor = Floor;
            if (floor == null) return null;
            var pool = type == RoomType.Elite ? floor.eliteRooms : floor.combatRooms;
            if (pool == null || pool.Count == 0) pool = type == RoomType.Elite ? floor.combatRooms : floor.eliteRooms;
            if (pool == null || pool.Count == 0) return null;
            return _roomRng.Pick(pool);
        }

        void NextWave()
        {
            while (true)
            {
                _waveIndex++;
                _waveTime = 0f;
                _pending.Clear();
                if (_room == null || _room.waves == null || _waveIndex >= _room.waves.Count)
                {
                    _wavesDone = true;
                    return;
                }
                var wave = _room.waves[_waveIndex];
                if (wave == null || wave.spawns == null) continue;
                foreach (var s in wave.spawns)
                {
                    if (s == null || s.enemy == null) continue;
                    int count = Mathf.Max(1, s.count);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 anchor = s.position + s.spacing * (i - (count - 1) * 0.5f);
                        float t = s.time + i * Mathf.Max(0f, s.interval);
                        Queue(t, s.enemy, s.from, anchor);
                        if (s.mirror) Queue(t, s.enemy, Mirror(s.from), new Vector2(-anchor.x, anchor.y));
                    }
                }
                return;
            }
        }

        void Queue(float time, EnemyDef def, EnterSide side, Vector2 anchor)
        {
            _pending.Add(new Pending { time = time, def = def, anchor = anchor, from = SpawnPoint(side, anchor, def.movement) });
        }

        static EnterSide Mirror(EnterSide s)
        {
            switch (s)
            {
                case EnterSide.Left: return EnterSide.Right;
                case EnterSide.Right: return EnterSide.Left;
                case EnterSide.TopLeft: return EnterSide.TopRight;
                case EnterSide.TopRight: return EnterSide.TopLeft;
                default: return s;
            }
        }

        Vector2 SpawnPoint(EnterSide side, Vector2 anchor, EnemyMoveDef move)
        {
            float top = _bounds.maxY + 1f, left = _bounds.minX - 1f, right = _bounds.maxX + 1f;
            var kind = move != null ? move.kind : EnemyMoveKind.Hover;
            if (kind == EnemyMoveKind.Drift) return new Vector2(anchor.x, top);
            if (kind == EnemyMoveKind.Sweep && (side == EnterSide.Top || side == EnterSide.TopLeft || side == EnterSide.TopRight))
                side = side == EnterSide.TopLeft ? EnterSide.Left : side == EnterSide.TopRight ? EnterSide.Right : (anchor.x <= 0f ? EnterSide.Left : EnterSide.Right);
            switch (side)
            {
                case EnterSide.Left: return new Vector2(left, anchor.y);
                case EnterSide.Right: return new Vector2(right, anchor.y);
                case EnterSide.TopLeft: return new Vector2(left, top);
                case EnterSide.TopRight: return new Vector2(right, top);
                default: return new Vector2(anchor.x, top);
            }
        }

        void TickWaves(float dt)
        {
            _waveTime += dt;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].time > _waveTime) continue;
                var p = _pending[i];
                _pending.RemoveAt(i);
                SpawnEnemy(p);
            }
            int alive = CountAlive();
            if (!_wavesDone && _pending.Count == 0)
            {
                var wave = _room != null && _waveIndex < _room.waves.Count ? _room.waves[_waveIndex] : null;
                bool timeUp = wave != null && wave.maxDuration > 0f && _waveTime >= wave.maxDuration;
                if (alive == 0)
                {
                    _waveGap += dt;
                    if (_waveGap >= config.waveGapSeconds) { _waveGap = 0f; NextWave(); }
                }
                else if (timeUp) NextWave();
            }
            if (_wavesDone && _pending.Count == 0 && alive == 0) RoomCleared();
        }

        void SpawnEnemy(Pending p)
        {
            var e = _enemyPool.Count > 0 ? _enemyPool.Pop() : ShmupEnemy.Create(enemyRoot != null ? enemyRoot : transform, spriteMaterial);
            var floor = Floor;
            e.Spawn(p.def, p.from, p.anchor, floor != null ? floor.hpScale : 1f, _nextEnemyId++, _roomRng, GameRoot.Settings);
            _enemies.Add(e);
        }

        void KillEnemy(ShmupEnemy e)
        {
            if (!e.Alive) return;
            var def = e.Def;
            var p = e.Position;
            _kills++;
            AddScore(def.score);
            if (coins != null) coins.Spawn(p, CoinsFor(def.coins));
            if (fx != null)
            {
                fx.Burst(p, Color.Lerp(def.color, Color.white, 0.3f), def.big ? 26 : 10, def.big ? 7f : 4.5f, def.big ? 0.18f : 0.12f, 0.4f);
                fx.Ring(p, def.color, 0.1f, def.size * (def.big ? 1.6f : 0.9f), 0.3f);
            }
            GameRoot.Audio.Play("enemy_die", def.big ? 1f : 0.6f);
            GambitBlast(p);
            if (def.big)
            {
                GameRoot.Juice.Shake(0.3f);
                GameRoot.Juice.FloatingText(p, "+" + def.score, new Color(1f, 0.9f, 0.5f));
            }
            e.Despawn();
        }

        int CoinsFor(int n)
        {
            var floor = Floor;
            return Mathf.Max(0, Mathf.RoundToInt(n * (floor != null ? floor.coinScale : 1f) * _loadout.CoinGain));
        }

        void RoomCleared()
        {
            if (_phase == RoomPhase.Cleared) return;
            _phase = RoomPhase.Cleared;
            _phaseTime = 0f;
            _roomsCleared++;
            if (coins != null) coins.attractAll = true;
            int bonus = _room != null ? CoinsFor(_room.bonusCoins) : 0;
            if (bonus > 0) AddCoins(bonus, true);
            GameRoot.Hud.Message(_roomType == RoomType.Boss ? "Boss defeated!" : "Room clear!", config.roomClearedSeconds);
            GameRoot.Audio.Play("powerup", 0.6f);
        }

        void FinishCleared()
        {
            if (coins != null)
            {
                if (coins.Count > 0) AddCoins(coins.Count, false);
                coins.Clear();
                coins.attractAll = false;
            }
            _rewardQueue.Clear();
            if (_roomType == RoomType.Combat || _roomType == RoomType.Elite) EvaluateTrials();
            var rewards = config.rewards;
            switch (_roomType)
            {
                case RoomType.Combat:
                    if (rewards.battle != null && rewards.battle.enabled) QueueReward(RewardSource.Battle, "Room clear!", "Pick one upgrade.");
                    break;
                case RoomType.Elite:
                    QueueReward(RewardSource.Elite, "Elite defeated!", "Pick one upgrade.");
                    break;
                case RoomType.Boss:
                    if (_run.IsLastFloor) { Victory(); return; }
                    QueueReward(RewardSource.Boss, "Boss reward", "Choose an epic. The next floor is harder.");
                    break;
            }
            NextReward();
        }

        void QueueReward(RewardSource source, string heading, string sub)
        {
            var t = config.rewards.Get(source);
            if (t == null || !t.enabled) return;
            _rewardQueue.Add(new PendingReward { source = source, heading = heading, sub = sub });
        }

        /// <summary>Opens the next queued reward screen, or leaves the room when there are none.</summary>
        void NextReward()
        {
            if (_phase == RoomPhase.Finished) return;
            if (_rewardQueue.Count == 0) { Leave(); return; }
            var r = _rewardQueue[0];
            _rewardQueue.RemoveAt(0);
            OpenDraft(r.heading, r.sub, r.source, NextReward);
        }

        /// <summary>A Battle or Elite room was cleared: each running trial checks its condition. Passed ones
        /// queue their reward (an epic choice); failed ones say why. Wager curses lift either way.</summary>
        void EvaluateTrials()
        {
            for (int i = _trials.Count - 1; i >= 0; i--)
            {
                var t = _trials[i];
                var state = t.RoomCleared(_loadout.Inventory);
                if (state == TrialState.Running) continue;
                EndTrial(t);
                _trials.RemoveAt(i);
                if (state == TrialState.Passed)
                {
                    QueueReward(t.reward, t.Def.displayName + ": done!", "You met the condition. Choose your reward.");
                    _encounterLog.Add("trial passed: " + t.Def.id);
                    GameRoot.Audio.Play("powerup");
                }
                else
                {
                    GameRoot.Hud.Message(t.Def.displayName + " failed. " + t.FailReason, 2.4f);
                    _encounterLog.Add("trial failed: " + t.Def.id);
                }
            }
            _hud.SetTrials(_trials);
        }

        void EndTrial(TrialTracker t)
        {
            if (!string.IsNullOrEmpty(t.curseSource)) _loadout.Stats.RemoveBySource(t.curseSource);
        }

        /// <summary>After a room: doors if the path forks, else straight on; the end of the last floor wins.</summary>
        void Leave()
        {
            _choice.Close();
            var next = _run.NextOptions;
            if (next.Count == 0)
            {
                Victory();
                return;
            }
            if (next.Count >= 2) OpenDoors(next);
            else Advance(0);
        }

        void Advance(int door)
        {
            _choice.Close();
            if (!_run.Advance(door)) { Victory(); return; }
            if (_run.IsComplete) { Victory(); return; }
            EnterRoom();
        }

        // ---------- Boss ----------

        void StartBoss()
        {
            var floor = Floor;
            if (floor == null || floor.boss == null) { RoomCleared(); return; }
            _phase = RoomPhase.Boss;
            ClearEnemyBulletsQuietly();
            boss.Begin(floor.boss, floor.hpScale, new Rng(_run.RoomSeed() ^ 0xB055UL), GameRoot.Settings, field, fx);
            _hud.NameCard(floor.boss.displayName.ToUpperInvariant(), floor.boss.title, 2.6f);
            _hud.ShowBoss(floor.boss.displayName, floor.boss.phases);
            // Before the fight: boss_<id>_intro, boss_intro. The boss is still flying in (it can't shoot yet).
            PlayStoryLine(StoryHooks.BossIntro(floor.boss.StoryId), AfterBossLine);
        }

        void OnBossPhase(int phase)
        {
            var def = boss.Def;
            if (def != null && phase < def.phases.Count) GameRoot.Hud.Message(def.phases[phase].name, 1.6f);
            // The boss has just cleared its bullets; a line here is the phase's emotional beat.
            if (def != null) PlayStoryLine(StoryHooks.BossPhase(def.StoryId, phase + 1), AfterBossLine);
        }

        /// <summary>After any boss line: nothing that was in the air before it can hit the player, and
        /// the player gets a moment to find their bearings.</summary>
        void AfterBossLine()
        {
            ClearEnemyBulletsQuietly();
            player.Health.GrantInvulnerability(1f);
        }

        void ClearEnemyBulletsQuietly()
        {
            if (field != null) field.Enemy.Clear();
        }

        void OnBossDying()
        {
            var def = boss.Def;
            if (def == null) return;
            AddScore(def.score);
            if (coins != null) coins.Spawn(boss.Position, CoinsFor(def.coins));
            GameRoot.Juice.FloatingText(boss.Position, "+" + def.score, new Color(1f, 0.9f, 0.5f));
        }

        void OnBossDefeated()
        {
            _bossesBeaten++;
            _hud.HideBoss();
            GameRoot.Audio.PlayMusic(GameRoot.Config.gameMusic);
            ClearEnemyBulletsQuietly();
            // Last words: boss_<id>_defeat, boss_defeat. The room counts as cleared after them.
            var def = boss.Def;
            if (def == null || !PlayStoryLine(StoryHooks.BossDefeat(def.StoryId), BossLineThenCleared)) RoomCleared();
        }

        void BossLineThenCleared()
        {
            AfterBossLine();
            RoomCleared();
        }

        // ---------- Player ----------

        void OnGraze(int n, Vector2 pp, float dt)
        {
            _grazes += n;
            _grazesLastFrame += n;
            for (int i = 0; i < _trials.Count; i++) _trials[i].Grazed(n);
            AddScore(20 * n);
            _sfxGraze -= dt;
            if (_sfxGraze <= 0f)
            {
                _sfxGraze = 0.06f;
                GameRoot.Audio.Play("graze", 0.5f);
            }
            if (fx != null) fx.Spark(pp + UnityEngine.Random.insideUnitCircle * 0.3f, new Color(0.8f, 0.95f, 1f, 0.9f), UnityEngine.Random.insideUnitCircle * 2f, 0.08f, 0.25f);
            int stacks = _loadout.Effect(ShmupEffects.GrazeHeal);
            if (stacks <= 0) return;
            _grazeHeal += n;
            float every = Mathf.Max(10f, config.economy.grazeHealEvery / (float)stacks);
            if (_grazeHeal < every) return;
            _grazeHeal -= every;
            if (player.Heal(0.5f) > 0f) GameRoot.Juice.FloatingText(pp, "+1/2 life", new Color(0.5f, 1f, 0.6f));
            else if (player.AddBombs(1) > 0) GameRoot.Juice.FloatingText(pp, "+1 bomb", new Color(1f, 0.85f, 0.4f));
            else AddCoins(10, true);
            GameRoot.Audio.Play("powerup", 0.5f);
        }

        void OnPlayerHit()
        {
            Vector2 pp = player.Position;
            // The companion takes the bullet if its guard is ready; then an auto-bomb; then a life.
            if (TryCompanionGuard(pp)) return;
            if (TryGuardian(pp)) return;
            var h = player.Health;
            h.DamageTakenScale = GameRoot.Settings.damageTakenScale;
            var r = h.Damage(new DamageInfo(1f, DamageKind.Bullet));
            if (r.applied <= 0f && !r.killed) return;
            _hitsTaken++;
            for (int i = 0; i < _trials.Count; i++) _trials[i].PlayerHit();
            GameRoot.Audio.Play("hurt");
            GameRoot.Juice.HitStop(0.15f);
            GameRoot.Juice.Shake(0.6f);
            GameRoot.Juice.Flash(new Color(1f, 0.25f, 0.3f, 0.45f), 0.3f);
            if (player.flash != null) player.flash.Flash(new Color(1f, 0.3f, 0.3f), 0.2f);
            if (fx != null) fx.Ring(pp, new Color(1f, 0.4f, 0.4f, 0.8f), 0.2f, 3f, 0.4f);
            if (r.killed) return; // OnPlayerDied ends the run
            field.Enemy.ClearInCircle(new System.Numerics.Vector2(pp.x, pp.y), 3f);
            player.RefillBombs();
            if (h.Current <= 1.01f) Bark("low_life", null);
        }

        void OnPlayerDied(DamageInfo info)
        {
            if (_phase == RoomPhase.Finished) return;
            _phase = RoomPhase.Finished;
            _choice.Close();
            _hud.SetSprintHint(0f);
            RecordRun(false);
            GameRoot.Run.Set("Reached", "Floor " + (_run.floor + 1) + ", room " + (_run.step + 1));
            ChooseDeathLine();
            LeaveRemnant();
            GameRoot.Lose();
        }

        // ---------- Remnants (docs/DESIGN-ROGUELITE.md, section 5) ----------

        void LoadRemnants()
        {
            _remnantBook = new RemnantBook();
            string json = GameRoot.Save.Data.GetEntry(RemnantSaveKey);
            if (string.IsNullOrEmpty(json)) return;
            try { JsonUtility.FromJsonOverwrite(json, _remnantBook); }
            catch (Exception e) { Debug.LogWarning("BulletHellGame: the saved remnants didn't load (" + e.Message + ")."); _remnantBook = new RemnantBook(); }
            if (_remnantBook.remnants == null) _remnantBook.remnants = new List<RemnantRecord>();
        }

        void SaveRemnants()
        {
            GameRoot.Save.Data.SetEntry(RemnantSaveKey, JsonUtility.ToJson(_remnantBook));
            GameRoot.Save.Save();
        }

        /// <summary>The dead run leaves a remnant: its best upgrade, weakened, plus a stat from how it died.
        /// Saved at once; later drafts can offer it (marked REMNANT).</summary>
        void LeaveRemnant()
        {
            var r = JamKit.BulletHell.Remnants.Build(_loadout.Inventory, _killer, _run.floor + 1, Mathf.Max(1, GameRoot.Save.Data.runsStarted), config.remnants);
            if (r == null) return;
            _remnantBook.Remove(r.id);
            _remnantBook.Add(r, config.remnants.maxKept);
            SaveRemnants();
            GameRoot.Run.Set("Remnant left", r.displayName);
        }

        /// <summary>The death screen's line, from ink: death_&lt;killer&gt;, death_floor_&lt;n&gt;, death. Read without
        /// showing a dialogue (the Game Over screen shows it). No knot: no line.</summary>
        void ChooseDeathLine()
        {
            var def = boss.Def;
            _killer = _roomType == RoomType.Boss && def != null && boss.Current != ShmupBoss.State.Inactive ? def.StoryId
                : _roomType == RoomType.Elite ? "elite" : "battle";
            string knot = _hookProbe != null ? StoryHooks.Resolve(StoryHooks.Death(_killer, _run.floor + 1), _hasKnot) : null;
            if (knot == null) return;
            if (GameRoot.Dialogue.TryReadLine(config.events.storyJson, knot, out string speaker, out string line, _bindInk))
            {
                GameRoot.Run.SetQuote(speaker, line);
                _storyKnotsPlayed.Add(knot);
            }
        }

        // ---------- Story hooks ----------

        bool HasKnot(string knot) => DialogueService.PathExists(_hookProbe, knot);

        /// <summary>The ink functions the hook knots (and Mystery rooms) can call, on top of the shell's
        /// has_flag/set_flag/run_count(): current_floor() (1, 2, 3) and killer() (on the death screen).</summary>
        void BindInk(Ink.Runtime.Story story)
        {
            story.BindExternalFunction("current_floor", () => (object)(_run != null ? _run.floor + 1 : 1), true);
            story.BindExternalFunction("killer", () => (object)(_killer ?? ""), true);
            // The companion's battles feed its conversations (the Rest room's line can mention them).
            story.BindExternalFunction("companion_name", () => (object)CompanionName, true);
            story.BindExternalFunction("companion_guards", () => (object)(_companion != null ? _companion.Guards : 0), true);
        }

        /// <summary>Plays the first knot of <paramref name="candidates"/> the ink has, then runs
        /// <paramref name="after"/>. Game time stops while it shows (GameFlow's Dialogue state).
        /// Returns false, having done nothing, when no knot exists or a dialogue is already up.</summary>
        bool PlayStoryLine(string[] candidates, Action after)
        {
            if (_hookProbe == null || _phase == RoomPhase.Finished || GameRoot.Dialogue.IsActive) return false;
            if (GameRoot.Flow.Current != GameState.Playing) return false;
            string knot = StoryHooks.Resolve(candidates, _hasKnot);
            if (knot == null) return false;
            _lineActive = true;
            _afterLine = after;
            if (!GameRoot.Dialogue.Play(config.events, knot))
            {
                _lineActive = false;
                _afterLine = null;
                return false;
            }
            _storyKnotsPlayed.Add(knot);
            return true;
        }

        void StartSprintHint()
        {
            if (_sprintHint.State != HintState.Waiting) return;
            var save = GameRoot.Save.Data;
            if (!_sprintHint.Start(save.HasFlag(SprintHintFlag))) return;
            // First run only: remembered as soon as it shows.
            save.SetFlag(SprintHintFlag);
            GameRoot.Save.Save();
            _hud.SetSprintHint(_sprintHint.Alpha);
        }

        /// <summary>Sets off a bomb if the player has one (what the bomb button does). For tests and debug.</summary>
        public bool Bomb()
        {
            if (_phase == RoomPhase.Finished || !player.TryUseBomb()) return false;
            TriggerBomb(false);
            return true;
        }

        void TriggerBomb(bool auto, float powerScale = 1f)
        {
            for (int i = 0; i < _trials.Count; i++) _trials[i].BombUsed();
            var def = config.player;
            Vector2 pp = player.Position;
            float power = _loadout.BombPower * powerScale;
            float radius = def.bombRadius * power;
            float damage = def.bombDamage * power;
            ClearEnemyBullets(1);
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.Alive || (e.Position - pp).magnitude > radius + e.Radius) continue;
                if (e.Damage(damage)) KillEnemy(e);
            }
            if (boss.CanBeHurt && (boss.Position - pp).magnitude <= radius + boss.Radius) boss.Damage(damage * 1.5f);
            player.Health.GrantInvulnerability(def.bombInvulnerable);
            _bombTimer = def.bombDuration;
            if (fx != null)
            {
                fx.Ring(pp, new Color(0.7f, 0.95f, 1f, 0.9f), 0.3f, radius, 0.55f);
                fx.Ring(pp, new Color(1f, 1f, 1f, 0.6f), 0.1f, radius * 0.6f, 0.4f);
            }
            GameRoot.Juice.Flash(new Color(0.8f, 0.95f, 1f, auto ? 0.4f : 0.55f), 0.35f);
            GameRoot.Juice.Shake(0.7f);
            GameRoot.Juice.HitStop(0.06f);
            GameRoot.Audio.Play("bomb");
        }

        /// <summary>Erases every enemy bullet for score; every <paramref name="sparkEvery"/>-th leaves a spark.</summary>
        void ClearEnemyBullets(int sparkEvery)
        {
            var pool = field.Enemy;
            int n = pool.Count;
            if (n == 0) return;
            if (fx != null)
            {
                var items = pool.Items;
                int step = Mathf.Max(1, sparkEvery);
                for (int i = 0; i < n; i += step)
                    fx.Spark(new Vector2(items[i].position.X, items[i].position.Y), new Color(0.75f, 0.95f, 1f, 0.9f), new Vector2(0f, 2f), 0.12f, 0.35f);
            }
            pool.Clear();
            AddScore(5 * n);
        }

        // ---------- Score and coins ----------

        void AddScore(int n)
        {
            if (n == 0) return;
            _score += n;
            GameRoot.Run.score = _score;
            _scoreDirty = true;
        }

        void AddCoins(int n, bool popup)
        {
            _run.currency = Mathf.Max(0, _run.currency + n);
            _sfxCoin -= Time.unscaledDeltaTime;
            if (_sfxCoin <= 0f && n > 0)
            {
                _sfxCoin = 0.08f;
                GameRoot.Audio.Play("coin", 0.4f);
            }
            if (popup && n != 0) GameRoot.Juice.FloatingText(player.Position + Vector2.up * 0.6f, (n > 0 ? "+" : "") + n + " coins", config.coinColor);
        }

        // ---------- Drafts, doors, shop, rest ----------

        /// <summary>A reward screen from <paramref name="source"/>'s table (BulletHellConfig > Rewards): rarities,
        /// the small epic chance and maybe a remnant. <paramref name="minRarity"/> narrows it (an event's "draft:rare").</summary>
        void OpenDraft(string heading, string sub, RewardSource source, Action after, Rarity minRarity = Rarity.Common)
        {
            _phase = RoomPhase.Choosing;
            _afterChoice = after;
            _draftHeading = heading;
            _draftSub = sub;
            _draftSource = source;
            _draftSources.Add(source);
            var table = config.rewards.Get(source) ?? new RewardTable();
            if (minRarity > Rarity.Common)
            {
                _filteredTable.commonWeight = minRarity <= Rarity.Common ? table.commonWeight : 0f;
                _filteredTable.rareWeight = minRarity <= Rarity.Rare ? Mathf.Max(table.rareWeight, 35f) : 0f;
                _filteredTable.epicWeight = minRarity <= Rarity.Epic ? Mathf.Max(table.epicWeight, minRarity == Rarity.Epic ? 100f : 0f) : 0f;
                _filteredTable.legendaryWeight = table.legendaryWeight;
                _filteredTable.epicChance = table.epicChance;
                _filteredTable.cards = table.cards;
                _filteredTable.luckBonus = table.luckBonus;
                _filteredTable.includeRemnants = table.includeRemnants;
                table = _filteredTable;
            }
            _draftTable = table;
            float bonus = source == RewardSource.Elite ? config.economy.eliteLuck : source == RewardSource.Boss ? config.economy.bossLuck : 0f;
            _draftLuck = _loadout.Luck + bonus;
            DrawDraft(false);
            BuildDraft();
        }

        void DrawDraft(bool reroll)
        {
            int cards = _draftTable.cards > 0 ? _draftTable.cards : Mathf.Max(1, config.economy.draftSize);
            _draft.Offer(_draftTable, DraftPool(), _loadout.Inventory, cards, _draftRng, _offer, _draftLuck, reroll);
            // A remnant of an earlier run may take a card (always on the first draft of a run).
            if (_draftTable.includeRemnants)
                JamKit.BulletHell.Remnants.MaybeOffer(_remnantBook, config.remnants, _loadout.Inventory, _offer, _fallback, _draftRng,
                    _firstDraftOfRun && config.remnants.firstDraftGuaranteed && !reroll);
            _firstDraftOfRun = false;
        }

        static readonly Color RemnantColor = new Color(0.65f, 0.95f, 0.85f);
        readonly List<UpgradeDef> _poolWithoutCompanion = new List<UpgradeDef>();

        /// <summary>The upgrade pool, minus the companion's upgrades until it has joined the run.</summary>
        List<UpgradeDef> DraftPool()
        {
            if (CompanionActive || !CompanionOn) return _pool;
            _poolWithoutCompanion.Clear();
            foreach (var d in _pool)
                if (d.tags == null || !d.tags.Contains(CompanionTag)) _poolWithoutCompanion.Add(d);
            return _poolWithoutCompanion;
        }

        void BuildDraft()
        {
            _choice.Begin(ChoicePanel.Kind.Draft, _draftHeading, _draftSub);
            foreach (var def in _offer)
            {
                var d = def;
                int owned = _loadout.Inventory.Count(d.id);
                string body = d.description;
                if (owned > 0) body += d.Unlimited ? "\n(owned: " + owned + ")" : "\n(owned: " + owned + " / " + d.maxStacks + ")";
                var asset = AssetFor(d.id);
                bool remnant = JamKit.BulletHell.Remnants.IsRemnant(d);
                _choice.AddCard(remnant ? "REMNANT" : d.rarity.ToString().ToUpperInvariant(), remnant ? RemnantColor : config.RarityColor(d.rarity),
                    d.displayName, body, "", true, () => PickUpgrade(d), asset != null ? asset.icon : null);
            }
            int cost = config.economy.RerollCost(_rerolls);
            _choice.AddFooter("Reroll (" + cost + " coins)", _run.currency >= cost, Reroll);
            _choice.AddFooter("Skip", true, FinishChoice);
            _choice.Show();
        }

        void Reroll()
        {
            int cost = config.economy.RerollCost(_rerolls);
            if (!_run.TrySpend(cost)) return;
            _rerolls++;
            DrawDraft(true);
            BuildDraft();
        }

        void PickUpgrade(UpgradeDef def)
        {
            TakeUpgrade(def);
            _draftsTaken++;
            // A remnant is used up when taken.
            if (JamKit.BulletHell.Remnants.IsRemnant(def) && _remnantBook.Remove(def.id)) SaveRemnants();
            FinishChoice();
        }

        /// <summary>Adds an upgrade (and applies its instant effects: lives, bombs, coins).</summary>
        public void TakeUpgrade(UpgradeDef def)
        {
            if (def == null) return;
            if (!_loadout.Inventory.Take(def)) _loadout.Inventory.ForceTake(def);
            if (def.effects != null)
                foreach (var e in def.effects)
                {
                    if (e == ShmupEffects.LifeUp) player.AddLives(1f);
                    else if (ShmupEffects.TryParseAmount(e, ShmupEffects.Coins, out int n)) AddCoins(n, true);
                }
            InventoryChanged();
            GameRoot.Audio.Play("powerup");
            GameRoot.Juice.FloatingText(player.Position + Vector2.up * 0.8f, def.displayName, config.RarityColor(def.rarity));
        }

        /// <summary>After any upgrade comes or goes (a pick, a sacrifice, a forge push or break): bombs follow
        /// the Bay, the HUD redraws its icons.</summary>
        void InventoryChanged()
        {
            player.RefreshBombs();
            _hud.SetUpgrades(_loadout.Inventory.Owned, _loadout.Inventory, AssetFor, config);
            GameRoot.Run.Set("Upgrades", _loadout.Inventory.Owned.Count.ToString());
        }

        void FinishChoice()
        {
            _choice.Close();
            var next = _afterChoice;
            _afterChoice = null;
            if (next != null) next();
            else Leave();
        }

        void OpenDoors(IReadOnlyList<RoomType> options)
        {
            _phase = RoomPhase.Choosing;
            _afterChoice = null;
            _choice.Begin(ChoicePanel.Kind.Doors, "Choose a door", "Floor " + (_run.floor + 1) + ", room " + (_run.step + 2));
            for (int i = 0; i < options.Count; i++)
            {
                int door = i;
                var t = options[i];
                _choice.AddCard("DOOR " + (i + 1), config.RoomColor(t), RoomText.Name(t), RoomText.Describe(t), "", true, () => Advance(door));
            }
            _choice.Show();
        }

        void OpenShop()
        {
            _phase = RoomPhase.Choosing;
            _afterChoice = Leave;
            _shop.Clear();
            _shopDraft.Offer(config.rewards.shop, DraftPool(), _loadout.Inventory, Mathf.Max(0, config.economy.shopUpgrades), _roomRng, _offer, _loadout.Luck);
            foreach (var d in _offer) _shop.Add(new ShopItem { kind = ShopKind.Upgrade, def = d, price = config.economy.PriceOf(d.rarity) });
            _shop.Add(new ShopItem { kind = ShopKind.Life, price = config.economy.lifePrice });
            // Bombs only for a ship with a Bomb Bay.
            if (player.MaxBombs > 0) _shop.Add(new ShopItem { kind = ShopKind.Bomb, price = config.economy.bombPrice });
            BuildShop();
        }

        void BuildShop()
        {
            _choice.Begin(ChoicePanel.Kind.Shop, "Shop", "You have " + _run.currency + " coins.");
            for (int i = 0; i < _shop.Count; i++)
            {
                var item = _shop[i];
                int index = i;
                bool can = !item.sold && _run.currency >= item.price && (item.kind != ShopKind.Bomb || player.Bombs < player.MaxBombs);
                string price = item.sold ? "Sold" : item.price + " coins";
                switch (item.kind)
                {
                    case ShopKind.Upgrade:
                    {
                        var asset = AssetFor(item.def.id);
                        _choice.AddCard(item.def.rarity.ToString().ToUpperInvariant(), config.RarityColor(item.def.rarity), item.def.displayName,
                            item.def.description, price, can, () => Buy(index), asset != null ? asset.icon : null);
                        break;
                    }
                    case ShopKind.Life:
                        _choice.AddCard("LIFE", new Color(1f, 0.45f, 0.5f), "Extra life", "+1 life (and room for it).", price, can, () => Buy(index));
                        break;
                    default:
                        _choice.AddCard("BOMB", new Color(1f, 0.85f, 0.4f), "Bomb", "+1 bomb.", price, can, () => Buy(index));
                        break;
                }
            }
            _choice.AddFooter("Leave the shop", true, FinishChoice);
            _choice.Show();
        }

        void Buy(int index)
        {
            var item = _shop[index];
            if (item.sold || !_run.TrySpend(item.price)) return;
            item.sold = true;
            switch (item.kind)
            {
                case ShopKind.Upgrade: TakeUpgrade(item.def); break;
                case ShopKind.Life: player.AddLives(1f); GameRoot.Audio.Play("powerup"); break;
                default: player.AddBombs(1); GameRoot.Audio.Play("powerup"); break;
            }
            GameRoot.Audio.Play("coin");
            BuildShop();
        }

        void OpenRest()
        {
            _phase = RoomPhase.Choosing;
            _afterChoice = Leave;
            var h = player.Health;
            bool hurt = h.Current < h.Max - 0.01f;
            _choice.Begin(ChoicePanel.Kind.Rest, "Rest site", "Catch your breath before the boss.");
            bool bombRoom = player.Bombs < player.MaxBombs;
            _choice.AddCard("REST", new Color(0.5f, 1f, 0.6f), hurt ? "Heal" : "Stock up",
                hurt ? "Restore 1 life." : bombRoom ? "Your lives are full: take a bomb instead." : "Your lives are full: take 20 coins instead.", "", true, RestHeal);
            _choice.AddCard("TRAIN", new Color(0.45f, 0.75f, 1f), "Train", "Pick one upgrade.", "", true, () =>
            {
                _choice.Close();
                OpenDraft("Training", "Pick one upgrade.", RewardSource.Rest, Leave);
            });
            _choice.Show();
        }

        void RestHeal()
        {
            if (CompanionActive && _companion.Down) _companion.Revive();
            if (player.Heal(1f) <= 0f && player.AddBombs(1) <= 0) AddCoins(20, true);
            GameRoot.Audio.Play("powerup");
            FinishChoice();
        }

        // ---------- Mystery rooms (ink) ----------

        void StartEvent()
        {
            _phase = RoomPhase.Dialogue;
            _pendingDraft = false;
            _pendingDraftMin = Rarity.Common;
            if (config.events == null || config.eventKnots == null || config.eventKnots.Count == 0) { Leave(); return; }
            string knot = _roomRng.Pick(config.eventKnots);
            _eventActive = true;
            if (!GameRoot.Dialogue.Play(config.events, knot))
            {
                _eventActive = false;
                Leave();
            }
        }

        void OnDialogueEvent(string name)
        {
            if (!_eventActive || !RoomEventEffect.TryParse(name, out var e)) return;
            Vector2 pp = player.Position + Vector2.up * 0.6f;
            switch (e.kind)
            {
                case RoomEventKind.Coins:
                    AddCoins(e.amount, true);
                    break;
                case RoomEventKind.Lives:
                    if (e.amount > 0) { player.AddLives(e.amount); GameRoot.Juice.FloatingText(pp, "+" + e.amount + " life", new Color(0.5f, 1f, 0.6f)); }
                    break;
                case RoomEventKind.Bombs:
                    if (e.amount <= 0) break;
                    int added = player.AddBombs(e.amount);
                    if (added > 0) GameRoot.Juice.FloatingText(pp, "+" + added + " bomb", new Color(1f, 0.85f, 0.4f));
                    else AddCoins(15 * e.amount, true); // no Bomb Bay (or it's full): coins instead
                    break;
                case RoomEventKind.Score:
                    AddScore(e.amount);
                    break;
                case RoomEventKind.Draft:
                    _pendingDraft = true;
                    _pendingDraftMin = e.minRarity;
                    break;
                case RoomEventKind.PayForDraft:
                    if (_run.TrySpend(e.amount))
                    {
                        _pendingDraft = true;
                        GameRoot.Juice.FloatingText(pp, "-" + e.amount + " coins", config.coinColor);
                    }
                    else GameRoot.Hud.Message("Not enough coins.", 2f);
                    break;
            }
        }

        void OnDialogueEnded(string knot)
        {
            if (_lineActive)
            {
                _lineActive = false;
                var next = _afterLine;
                _afterLine = null;
                if (_phase != RoomPhase.Finished) next?.Invoke();
                return;
            }
            if (!_eventActive) return;
            _eventActive = false;
            if (_phase == RoomPhase.Finished) return;
            if (_pendingDraft) OpenDraft("A gift", "Pick one upgrade.", RewardSource.Event, Leave, _pendingDraftMin);
            else Leave();
        }

        // ---------- End of the run ----------

        void Victory()
        {
            if (_phase == RoomPhase.Finished) return;
            _phase = RoomPhase.Finished;
            _choice.Close();
            RecordRun(true);
            GameRoot.Win();
        }

        void RecordRun(bool won)
        {
            int floors = _run.Plan != null ? _run.Plan.floors.Count : 0;
            GameRoot.Run.score = _score;
            GameRoot.Run.Set("Floors cleared", _bossesBeaten + " / " + floors);
            GameRoot.Run.Set("Upgrades", _loadout.Inventory.Owned.Count.ToString());
            GameRoot.Run.Set("Enemies destroyed", _kills.ToString());
            GameRoot.Run.Set("Grazes", _grazes.ToString());
            var save = GameRoot.Save.Data;
            int reached = won ? floors : _run.floor + 1;
            if (reached > save.bestFloor) save.bestFloor = reached;
        }

        // ---------- Debug and smoke ----------

        /// <summary>Debug skip ("Next scene" in the debug menu): ends the current step of the room.
        /// Waves: kills everything (with rewards). Boss: kills it. Choice: takes the first option
        /// (leaves a shop). Dialogue: ends it. Returns false when there's nothing to skip.</summary>
        public bool DebugSkip()
        {
            if (_run == null || _phase == RoomPhase.Finished) return false;
            if (GameRoot.Dialogue.IsActive)
            {
                GameRoot.Dialogue.Stop(true);
                return true;
            }
            switch (_phase)
            {
                case RoomPhase.Intro:
                    StartRoomContent();
                    return true;
                case RoomPhase.Waves:
                    _pending.Clear();
                    for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Alive) KillEnemy(_enemies[i]);
                    ClearEnemyBullets(4);
                    _wavesDone = true;
                    RoomCleared();
                    return true;
                case RoomPhase.Boss:
                    boss.DebugKill();
                    return true;
                case RoomPhase.Cleared:
                    FinishCleared();
                    return true;
                case RoomPhase.Choosing:
                    // Shops and encounter rooms: skipping walks out (nothing risked, nothing bought).
                    if (_choice.Current == ChoicePanel.Kind.Shop || IsEncounterPanel(_choice.Current)) { FinishChoice(); return true; }
                    int first = _choice.FirstEnabledCard();
                    if (first >= 0) return _choice.Choose(first);
                    FinishChoice();
                    return true;
                case RoomPhase.Dialogue:
                    if (_eventActive) OnDialogueEnded("");
                    else Leave();
                    return true;
            }
            return false;
        }

        static bool IsEncounterPanel(ChoicePanel.Kind k) =>
            k == ChoicePanel.Kind.Forge || k == ChoicePanel.Kind.Altar || k == ChoicePanel.Kind.Wager
            || k == ChoicePanel.Kind.Trial || k == ChoicePanel.Kind.Trader || k == ChoicePanel.Kind.Pick;

        /// <summary>
        /// Debug and tests: turn the current room into <paramref name="type"/> right now (enemies, bullets
        /// and any boss go quietly; the run's position doesn't change, so leaving it advances as usual).
        /// Returns false when the run is over.
        /// </summary>
        public bool DebugEnterRoom(RoomType type)
        {
            if (_run == null || _phase == RoomPhase.Finished) return false;
            // Whatever was on screen goes without its follow-up (a line's panel, an event's draft).
            _lineActive = false;
            _afterLine = null;
            _eventActive = false;
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop(true);
            _choice.Close();
            _afterChoice = null;
            _rewardQueue.Clear();
            _pending.Clear();
            for (int i = 0; i < _enemies.Count; i++) if (_enemies[i].Alive) _enemies[i].Despawn();
            field.Enemy.Clear();
            if (boss.IsActive) boss.Hide();
            _hud.HideBoss();
            _roomType = type;
            _room = null;
            _wavesDone = false;
            _phaseTime = 0f;
            _roomRng = new Rng(_run.RoomSeed() ^ (ulong)(int)type * 0x9E37UL);
            if (RoomText.IsFight(type)) { player.RefillSprint(); EpicsRoomStarted(); }
            if (type == RoomType.Combat || type == RoomType.Elite)
                for (int i = 0; i < _trials.Count; i++) _trials[i].RoomStarted();
            StartRoomContent();
            return true;
        }

        IEnumerator SmokeDrive()
        {
            float previous = DebugMenu.TimeScale;
            DebugMenu.TimeScale = 3f;
            float end = Time.realtimeSinceStartup + 5f;
            int steps = 0;
            string encounter = null;
            Debug.Log("JAMKIT_SMOKE bullet hell seed " + (_run != null ? _run.seed : 0UL));
            while (Time.realtimeSinceStartup < end && steps < 8 && GameRoot.Flow.InRun && _phase != RoomPhase.Finished)
            {
                if (GameRoot.Flow.Current == GameState.Playing)
                {
                    if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                    else if (_choice.IsOpen && _choice.Current == ChoicePanel.Kind.Draft) { SmokeLogDraft(); _choice.Choose(0); steps++; }
                    else if (_choice.IsOpen && IsEncounterPanel(_choice.Current) && encounter == null)
                    {
                        encounter = SmokeTakeEncounter();
                        steps++;
                    }
                    else if (DebugSkip()) steps++;
                }
                else if (GameRoot.Flow.Current == GameState.Dialogue) GameRoot.Dialogue.AutoStep();
                float wait = Time.realtimeSinceStartup + 0.3f;
                while (Time.realtimeSinceStartup < wait) yield return null;
            }
            // The run's doors may not have offered an encounter or a draft in 5 s: make one of each happen.
            if (_draftsTaken == 0 && GameRoot.Flow.InRun && _phase != RoomPhase.Finished && DebugEnterRoom(RoomType.Treasure))
            {
                for (int i = 0; i < 10 && !(_choice.IsOpen && _choice.Current == ChoicePanel.Kind.Draft); i++) yield return null;
                if (_choice.IsOpen && _choice.Current == ChoicePanel.Kind.Draft) { SmokeLogDraft(); _choice.Choose(0); }
            }
            if (encounter == null && GameRoot.Flow.InRun && _phase != RoomPhase.Finished && DebugEnterRoom(RoomType.Forge))
            {
                for (int i = 0; i < 120 && !(_choice.IsOpen && IsEncounterPanel(_choice.Current)); i++)
                {
                    if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.AutoStep();
                    yield return null;
                }
                encounter = SmokeTakeEncounter();
                for (int i = 0; i < 10 && _choice.IsOpen && IsEncounterPanel(_choice.Current); i++)
                {
                    DebugSkip(); // walk out
                    yield return null;
                }
            }
            Debug.Log("JAMKIT_SMOKE bullet hell reached floor " + (_run.floor + 1) + " room " + (_run.step + 1)
                + ", " + _draftsTaken + " draft(s), " + field.Enemy.Count + " enemy bullets");
            Debug.Log("JAMKIT_SMOKE bullet hell encounter: " + (encounter ?? "none") + "; log: "
                + (_encounterLog.Count > 0 ? string.Join(" | ", _encounterLog) : "empty")
                + "; companion " + (CompanionOn ? "on, " + _companionShots + " shots" : "off") + "; remnants saved " + _remnantBook.Count);
            Debug.Log("JAMKIT_SMOKE bullet hell story lines: " + (_storyKnotsPlayed.Count > 0 ? string.Join(", ", _storyKnotsPlayed) : "none")
                + "; sprint hint " + _sprintHint.State + ", sprint left in room " + player.Sprint.RoomBudget.ToString("0.0") + " s" + "; difficulty " + DifficultyText.Name(GameRoot.Settings.difficulty)
                + "; control legend " + (_hud.LegendShown ? "on" : "off"));
            DebugMenu.TimeScale = previous;
        }

        void SmokeLogDraft()
        {
            var names = new List<string>();
            for (int i = 0; i < _offer.Count; i++) names.Add(_offer[i].displayName + " (" + (JamKit.BulletHell.Remnants.IsRemnant(_offer[i]) ? "remnant" : _offer[i].rarity.ToString()) + ")");
            Debug.Log("JAMKIT_SMOKE bullet hell draft from " + _draftSource + ": " + string.Join(", ", names) + "; taking the first");
        }

        /// <summary>Smoke run: takes the first enabled card of an encounter screen (and the first card of any
        /// pick list it opens), and says what happened.</summary>
        string SmokeTakeEncounter()
        {
            var kind = _choice.Current;
            string what = kind + " / " + _choice.Heading;
            int card = _choice.FirstEnabledCard();
            if (card >= 0)
            {
                what += " -> " + _choice.CardTitle(card);
                _choice.Choose(card);
                if (_choice.IsOpen && _choice.Current == ChoicePanel.Kind.Pick)
                {
                    int pick = _choice.FirstEnabledCard();
                    if (pick >= 0) { what += " -> " + _choice.CardTitle(pick); _choice.Choose(pick); }
                }
            }
            Debug.Log("JAMKIT_SMOKE bullet hell encounter " + what);
            return what;
        }
    }
}
