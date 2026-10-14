using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Movement;
using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The arena's rules at run time (docs/SKYLINE-PLAN-M1.md Task 16).
    /// <list type="bullet">
    /// <item><b>Encounters</b>, from <see cref="GalleriaArena.Encounters"/> in order along the route through the hall
    /// (food-court by the entrance, facade in the middle, skylight by the mezzanine; judge round 1): each has its
    /// <see cref="SkylineEncounterZone"/>, the lane round its own anchor (<see cref="GalleriaArena.ZoneOf"/>), so each fight
    /// starts from where its balance was measured; the first is armed at the start, each next one once the one before is
    /// cleared. While a later fight is armed and not started, its floor marker (<see cref="zoneMarkers"/>) is shown and
    /// CONCIERGE says the next-fight notice (a repeating beat).
    /// An armed zone that holds the player's feet starts its encounter: every spawn through <see cref="SkylineBot.Spawn"/>
    /// (seeded by encounter and spawn index; target the player; the player's <see cref="WritTuning"/>; group
    /// encounter index x 100 + the data's group, so no two encounters share a group; a gondola on its rail facing away from
    /// its wall, yaw 90 on the west rail and -90 on the east; every other bot facing the hall's entry, the encounters'
    /// anchor). Logs <c>SKYLINE_ARENA encounter &lt;id&gt; start</c> and, when its last bot is wrecked,
    /// <c>SKYLINE_ARENA encounter &lt;id&gt; cleared</c>.</item>
    /// <item><b>The ledger</b>: <see cref="LedgerRun.BeginCombat"/> at an encounter's first alert,
    /// <see cref="LedgerRun.EndCombat"/> when it is cleared, and <see cref="LedgerRun.Tick"/> every frame (the player leaves
    /// it to the arena), with <see cref="WallReach"/> against the player's movement world.</item>
    /// <item><b>Teaching beats</b> (<see cref="TeachingBeats.M1"/>; judge round 1: each fires at its moment or not at all):
    /// each fires once (the next-fight notice every time it comes); they go through a <see cref="TeachingQueue"/> (reading
    /// time at 15 characters a second; reactions, then notices, then hints; a reaction cuts in). A line is queued only while
    /// its moment holds (<see cref="MomentPassed"/>) and dropped from the queue as soon as it has passed; a dropped line the
    /// player hasn't learned yet is re-armed for its next moment (<see cref="Learned"/>). Logs <c>SKYLINE_LINE &lt;id&gt;</c>,
    /// <c>SKYLINE_LINE &lt;id&gt; dropped [rearmed]</c> and <c>SKYLINE_LINE &lt;id&gt; cut</c>. Each line goes to
    /// <see cref="SkylineHud.Say"/> and is spoken with <see cref="SkylineBabble.Speak"/> from the 2D <see cref="voice"/>,
    /// CONCIERGE's after its PA chime. Triggers: WarmupStart 1 s after the start (in the editor capture: after the Recorder
    /// takes over the clock); PracticeBotSeen with the practice bot within 25 m, on screen and in sight (once WarmupStart has
    /// fired, so Nan opens); FirstSpotCreated on <see cref="SkylineBot.SpotCreated"/>; NearBoostLedge within 6 m (flat) of the
    /// kiosk's footprint (Nan's double jump and mantle, then Dot's boost); BoostLedgeReached from the
    /// <see cref="SkylineScrapCache"/>; FirstPlatingBreak each time <see cref="PlayerArmor.PlatingBreaks"/> goes up until the
    /// line is said; FirstSeen per enemy id when an encounter bot of that id goes Alert (the practice bot never counts).
    /// Each encounter's start is sold by <see cref="SkylineDamageFeedback.FightStart"/>.</item>
    /// <item><b>Wrecked</b>: the Unit's wreck restarts the arena at the warm-up (Task 14's <see cref="SkylinePlayer.Wrecked"/>
    /// and <see cref="SkylinePlayer.Restart"/>): every encounter's bots, wrecks and rails are removed (and bolts in flight), the food court is armed
    /// again, the ledger is reset, the scrap cache is put back and the player stands at <see cref="GalleriaArena.PlayerStart"/>
    /// facing +Z. Beats that have played stay played. Logs <c>SKYLINE_ARENA wrecked restart life=&lt;n&gt;</c>.</item>
    /// <item><b>The far goal</b>: once the last fight is cleared the service lift's shutter (<see cref="liftShutter"/>) rolls
    /// up to <see cref="GalleriaArena.ShutterGap"/> (<c>SKYLINE_ARENA lift open</c>); the Unit slides under it, and its feet
    /// in <see cref="liftZone"/> end the floor (<c>SKYLINE_ARENA exit reached</c>). A restart rolls it down again.</item>
    /// </list>
    /// </summary>
    public sealed class SkylineArenaDirector : MonoBehaviour
    {
        public const float BeatGap = 0.5f, WarmupDelay = 1f, PracticeSeenRange = 25f, NearLedgeRange = 6f;
        const float ChestHeight = 1.25f;

        public enum Phase { Waiting, Armed, Fighting, Cleared }

        sealed class Encounter
        {
            public EncounterDef def;
            public SkylineEncounterZone zone;
            public Phase phase;
            public bool inCombat;
            public Transform root;
            public readonly List<SkylineBot> bots = new List<SkylineBot>();
        }

        [Header("The builder fills these")]
        public SkylinePlayer player;
        [Tooltip("One per encounter; matched by encounterIndex.")]
        public SkylineEncounterZone[] zones = new SkylineEncounterZone[0];
        public SkylineScrapCache cache;
        [Tooltip("The second cache, on the wall-run lockers.")]
        public SkylineScrapCache wallRunCache;
        [Tooltip("2D source the teaching lines are spoken from.")]
        public AudioSource voice;
        [Tooltip("Where the encounters' bots are spawned (one child per encounter).")]
        public Transform botsRoot;
        [Tooltip("The second fight's floor marker (the look dresses it); also in zoneMarkers.")]
        public GameObject entryMarker;
        [Tooltip("Per encounter, the floor marker of its lane (none for the first), shown while it is armed and waiting.")]
        public GameObject[] zoneMarkers = new GameObject[0];
        [Tooltip("The service lift's roll-up shutter: down in every fight, up to GalleriaArena.ShutterGap once the hall is cleared.")]
        public Transform liftShutter;
        [Tooltip("Inside the lift lobby: the Unit's feet here end the floor.")]
        public SkylineEncounterZone liftZone;
        [Tooltip("A glowing (URP Unlit) material saved in the scene: the Writ's cracks and the sparks copy it (SkylineBotLook.GlowTemplate).")]
        public Material glowTemplate;

        public SkylineBot PracticeBot { get; private set; }
        public int EncounterCount => _encounters.Count;
        /// <summary>How many times the Unit has been wrecked and the arena restarted.</summary>
        public int Restarts { get; private set; }
        public event Action Restarted;
        public event Action<int> EncounterStarted, EncounterCleared;
        /// <summary>The shutter is up: the far goal is open.</summary>
        public bool LiftOpen { get; private set; }
        public bool ExitReached { get; private set; }
        Vector3 _shutterPos, _shutterScale;
        bool _shutterKnown;
        Coroutine _shutterRoll;

        readonly List<Encounter> _encounters = new List<Encounter>();
        readonly Dictionary<SkylineBot, int> _botEncounter = new Dictionary<SkylineBot, int>();
        readonly HashSet<string> _beatUsed = new HashSet<string>();
        readonly TeachingQueue _lines = new TeachingQueue();
        readonly List<TeachingBeat> _dropped = new List<TeachingBeat>();
        readonly List<TeachingBeat> _cut = new List<TeachingBeat>();
        TeachingBeat[] _beats = new TeachingBeat[0];
        SkylineBot _spotBot;
        int _spotsMade, _platingBreaksSeen;
        bool _ledgeReached, _counterReached, _liftSeen;
        float _warmupAt = -1f;
        bool _started, _warmupTriggered;
        float _nextSightCheck;

        public Phase PhaseOf(int i) => i >= 0 && i < _encounters.Count ? _encounters[i].phase : Phase.Waiting;
        public string IdOf(int i) => i >= 0 && i < _encounters.Count ? _encounters[i].def.id : "";
        /// <summary>Where encounter <paramref name="i"/> starts (its anchor, the middle of its zone).</summary>
        public NVec3 AnchorOf(int i) => i >= 0 && i < _encounters.Count ? _encounters[i].def.anchor : GalleriaArena.HallEntry;
        public IReadOnlyList<SkylineBot> BotsOf(int i) => i >= 0 && i < _encounters.Count ? _encounters[i].bots : (IReadOnlyList<SkylineBot>)new SkylineBot[0];
        public bool AnyLinesWaiting => _lines.Count > 0;

        public static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        void Start()
        {
            if (player == null)
            {
                Debug.LogWarning("SKYLINE_ARENA no player; the director is off");
                enabled = false;
                return;
            }
            _beats = TeachingBeats.M1();
            // what a fight would otherwise make on first use, made now (Task 17): pools, materials, faces, and two frames of
            // each runtime drawing in front of the camera; the lines and the next fight's barks are synthesized one per frame
            if (glowTemplate != null) SkylineBotLook.GlowTemplate = glowTemplate;
            SkylinePrewarm.Pools();
            StartCoroutine(SkylinePrewarm.ShowOnce(player.Camera));
            // The Relay (docs/SKYLINE-RELAY.md) runs the arena itself (SkylineRelayDirector): no encounters, practice bot or
            // teaching lines. This director stays off; the pools and the glow template above are shared.
            if (SkylineRelayMode.Active)
            {
                ShowEntryMarker(false);
                enabled = false;
                return;
            }
            StartCoroutine(PrepareLines());
            var defs = GalleriaArena.Encounters();
            for (int i = 0; i < defs.Length; i++)
            {
                var e = new Encounter { def = defs[i], phase = Phase.Waiting };
                foreach (var z in zones) if (z != null && z.encounterIndex == i) e.zone = z;
                if (e.zone == null) Debug.LogWarning("SKYLINE_ARENA no zone for encounter " + defs[i].id);
                _encounters.Add(e);
            }
            if (_encounters.Count > 0) Arm(0);
            ShowEntryMarker(false);

            SkylineBot.StateEntered += OnState;
            SkylineBot.SpotCreated += OnSpot;
            SkylineBot.Wrecked += OnBotWrecked;
            player.Wrecked += OnPlayerWrecked;
            if (cache != null) cache.Taken += OnCacheTaken;

            SpawnPracticeBot();
            _started = true;
            StartCoroutine(StartClock());
        }

        void OnDestroy()
        {
            SkylineBot.StateEntered -= OnState;
            SkylineBot.SpotCreated -= OnSpot;
            SkylineBot.Wrecked -= OnBotWrecked;
            if (player != null) player.Wrecked -= OnPlayerWrecked;
            if (cache != null) cache.Taken -= OnCacheTaken;
        }

        IEnumerator StartClock()
        {
            // In the editor capture the Recorder starts some time into Play mode; count the warm-up line from there, so
            // Nan's line is in the recording. Every scripted time here is Time.time: under the capture step it is the
            // recording's own clock (SkylineRecorderWait).
            if (SkylineRunArgs.Has("-skylineOut"))
                yield return SkylineRecorderWait.Wait("SKYLINE_ARENA");
            _warmupAt = Time.time + WarmupDelay;
        }

        void SpawnPracticeBot()
        {
            Vector3 at = U(GalleriaArena.PracticeBot), start = U(GalleriaArena.PlayerStart);
            float yaw = Mathf.Atan2(start.x - at.x, start.z - at.z) * Mathf.Rad2Deg;
            PracticeBot = SkylineBot.Spawn(SkylineEnemies.DroneId, at, yaw, 7UL, player, player.Writ, transform);
            if (PracticeBot != null)
            {
                PracticeBot.practice = true;
                PracticeBot.gameObject.name = "Practice " + PracticeBot.gameObject.name;
            }
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (!_started || player == null) return;
            float now = Time.time;

            if (_warmupAt >= 0f && now >= _warmupAt)
            {
                _warmupAt = -1f;
                _warmupTriggered = true;
                Trigger(BeatTrigger.WarmupStart, null);
            }
            // the warm-up's lessons start with the clock (judge round 2: they outrank Nan's opener, which no longer gates them)
            bool clock = _warmupAt >= 0f || _warmupTriggered;
            if (clock && !Fired(BeatTrigger.PracticeBotSeen) && PracticeBotInView()) Trigger(BeatTrigger.PracticeBotSeen, null);
            if (clock && !Fired(BeatTrigger.NearBoostLedge) && NearKiosk()) Trigger(BeatTrigger.NearBoostLedge, null);
            if (!_counterReached && OnCounter()) _counterReached = true;
            if (clock && !Fired(BeatTrigger.NearPracticeLedge) && NearCounter(NearLedgeRange)) Trigger(BeatTrigger.NearPracticeLedge, null);
            if (now >= _nextSightCheck)
            {
                _nextSightCheck = now + SightCheckEvery;
                CheckFirstSight();
            }
            if (LiftOpen && !_liftSeen)
            {
                _liftSeen = true;
                Trigger(BeatTrigger.LiftOpened, null);
            }
            int breaks = player.Armor != null ? player.Armor.PlatingBreaks : 0;
            if (breaks < _platingBreaksSeen) _platingBreaksSeen = breaks;   // a fresh Unit after a wreck
            if (breaks > _platingBreaksSeen)
            {
                _platingBreaksSeen = breaks;
                if (!Fired(BeatTrigger.FirstPlatingBreak)) Trigger(BeatTrigger.FirstPlatingBreak, null);
            }

            Vector3 feet = player.transform.position;
            for (int i = 0; i < _encounters.Count; i++)
            {
                var e = _encounters[i];
                if (e.phase == Phase.Armed && e.zone != null && e.zone.Contains(feet)) StartEncounter(i);
            }
            if (LiftOpen && !ExitReached && liftZone != null && liftZone.Contains(feet))
            {
                ExitReached = true;
                Debug.Log("SKYLINE_ARENA exit reached");
                // the floor is done: the shutter rolls right up, so the Unit can look back over the hall it cleared
                if (liftShutter != null && _shutterKnown)
                {
                    if (_shutterRoll != null) StopCoroutine(_shutterRoll);
                    _shutterRoll = StartCoroutine(RollUp(_shutterScale.y - 0.05f));
                }
            }

            TickLedger(Time.deltaTime);
            PumpLines(now);
        }

        void TickLedger(float dt)
        {
            var move = player.Movement;
            var sim = move != null ? move.Sim : null;
            if (sim == null || player.Ledger == null) return;
            bool reach = false;
            if (player.Ledger.InCombat)
            {
                bool airOrSprint = !sim.Grounded || sim.Mode == MoveMode.Sprint;
                reach = WallReach.InReach(move, sim.Position, ChestHeight, airOrSprint);
            }
            player.Ledger.Tick(dt, sim.Mode, reach);
        }

        bool PracticeBotInView() => InView(PracticeBot, PracticeSeenRange);

        const float SightCheckEvery = 0.1f, NoticeSightRange = 45f;

        /// <summary>CONCIERGE's safety notice for a kind of bot fires the first time the player sees one (on screen, within
        /// 45 m, nothing in between), or when one goes Alert, whichever comes first (judge round 2: the spotlight's notice came
        /// after its beam).</summary>
        void CheckFirstSight()
        {
            foreach (var e in _encounters)
            {
                if (e.phase != Phase.Fighting) continue;
                foreach (var bot in e.bots)
                {
                    if (bot == null || !bot.Alive || bot.Def == null || !NoticeArmed(bot.Def.id)) continue;
                    if (InView(bot, NoticeSightRange)) Trigger(BeatTrigger.FirstSeen, bot.Def.id);
                }
            }
        }

        bool NoticeArmed(string enemyId)
        {
            foreach (var b in _beats)
                if (b.trigger == BeatTrigger.FirstSeen && b.enemy == enemyId && !_beatUsed.Contains(b.id)) return true;
            return false;
        }

        bool InView(SkylineBot b, float range)
        {
            var cam = player.Camera;
            if (b == null || !b.Alive || cam == null) return false;
            Vector3 eye = player.EyePosition, c = b.Center;
            Vector3 d = c - eye;
            float dist = d.magnitude;
            if (dist > range || dist < 1e-3f) return false;
            Vector3 vp = cam.WorldToViewportPoint(c);
            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return false;
            return !SkylineBot.CastClear(eye, d / dist, dist - 0.05f, b.transform, out _);
        }

        bool NearKiosk() => NearKiosk(NearLedgeRange);

        /// <summary>Within <paramref name="range"/> m (flat) of the warm-up's practice counter's footprint.</summary>
        bool NearCounter(float range)
        {
            Vector3 f = player.transform.position, c = U(GalleriaArena.PracticeLedgeTop);
            float h = GalleriaArena.PracticeLedgeSize * 0.5f;
            float dx = Mathf.Max(0f, Mathf.Abs(f.x - c.x) - h), dz = Mathf.Max(0f, Mathf.Abs(f.z - c.z) - h);
            return dx * dx + dz * dz <= range * range;
        }

        /// <summary>The Unit's feet are on the practice counter's top.</summary>
        bool OnCounter() => NearCounter(0.3f) && player.transform.position.y > GalleriaArena.PracticeLedgeTop.Y - 0.3f;

        bool NearKiosk(float range)
        {
            Vector3 f = player.transform.position;
            Vector3 c = U(GalleriaArena.BoostLedgeTop);
            float h = GalleriaArena.BoostLedgeSize * 0.5f;
            float dx = Mathf.Max(0f, Mathf.Abs(f.x - c.x) - h), dz = Mathf.Max(0f, Mathf.Abs(f.z - c.z) - h);
            return dx * dx + dz * dz <= range * range;
        }

        // ------------------------------------------------------------------ encounters

        void StartEncounter(int i)
        {
            var e = _encounters[i];
            e.phase = Phase.Fighting;
            e.inCombat = false;
            ShowEntryMarker(false);
            e.bots.Clear();
            e.root = new GameObject("Encounter " + e.def.id).transform;
            if (botsRoot != null) e.root.SetParent(botsRoot, false);
            Debug.Log("SKYLINE_ARENA encounter " + e.def.id + " start");
            SkylineDamageFeedback.FightStart(player);
            Vector3 anchor = U(e.def.anchor);
            for (int k = 0; k < e.def.spawns.Length; k++)
            {
                var s = e.def.spawns[k];
                Vector3 at = U(s.position);
                var def = SkylineEnemies.Find(s.enemy);
                float yaw;
                if (def != null && def.layer == ThreatLayer.Walls) yaw = at.x < 0f ? 90f : -90f;   // away from its wall
                else yaw = Mathf.Atan2(anchor.x - at.x, anchor.z - at.z) * Mathf.Rad2Deg;
                ulong seed = (ulong)((i + 1) * 1000 + k + 1);
                var bot = SkylineBot.Spawn(s.enemy, at, yaw, seed, player, player.Writ, e.root, i * 100 + s.group, U(s.railEnd));
                if (bot == null) continue;
                e.bots.Add(bot);
                _botEncounter[bot] = i;
            }
            EncounterStarted?.Invoke(i);
            if (e.bots.Count == 0) Clear(i);
        }

        /// <summary>Arms encounter <paramref name="i"/> and synthesizes its bots' barks ahead, one per frame (each bot barks in
        /// its own seeded voice: the seed is the one <see cref="StartEncounter"/> gives it).</summary>
        void Arm(int i)
        {
            _encounters[i].phase = Phase.Armed;
            StartCoroutine(PrepareBarks(i));
        }

        IEnumerator PrepareBarks(int i)
        {
            var spawns = _encounters[i].def.spawns;
            for (int k = 0; k < spawns.Length; k++)
            {
                int seed = (int)((ulong)((i + 1) * 1000 + k + 1) & 0x7FFFFFFFUL);
                foreach (var bark in SkylineBarks.Ids)
                    if (SkylineBarks.Prepare(spawns[k].enemy, bark, seed)) yield return null;
            }
        }

        IEnumerator PrepareLines()
        {
            yield return null;
            foreach (var b in _beats)
            {
                SkylineBabble.Prepare(b.speaker, b.text);
                yield return null;
            }
        }

        void OnState(SkylineBot bot, BotState state)
        {
            if (bot == null || state != BotState.Alert || bot.practice) return;
            if (bot.Def != null) Trigger(BeatTrigger.FirstSeen, bot.Def.id);
            if (_botEncounter.TryGetValue(bot, out int i))
            {
                var e = _encounters[i];
                if (e.phase == Phase.Fighting && !e.inCombat)
                {
                    e.inCombat = true;
                    player.Ledger.BeginCombat();
                }
            }
        }

        void OnSpot(SkylineBot bot)
        {
            _spotBot = bot;
            _spotsMade++;
            Trigger(BeatTrigger.FirstSpotCreated, null);
        }

        void OnBotWrecked(SkylineBot bot)
        {
            if (bot == null || !_botEncounter.TryGetValue(bot, out int i)) return;
            var e = _encounters[i];
            if (e.phase != Phase.Fighting) return;
            foreach (var b in e.bots) if (b != null && b.Alive) return;
            Clear(i);
        }

        void Clear(int i)
        {
            var e = _encounters[i];
            e.phase = Phase.Cleared;
            if (e.inCombat) player.Ledger.EndCombat();
            e.inCombat = false;
            Debug.Log("SKYLINE_ARENA encounter " + e.def.id + " cleared");
            if (i + 1 < _encounters.Count && _encounters[i + 1].phase == Phase.Waiting)
            {
                Arm(i + 1);
                ShowMarker(i + 1);
                Trigger(BeatTrigger.NextFightArmed, null);
            }
            EncounterCleared?.Invoke(i);
            if (i + 1 >= _encounters.Count)
            {
                Debug.Log("SKYLINE_ARENA all cleared");
                RollShutter(true);
            }
        }

        void ShowEntryMarker(bool on)
        {
            if (!on) ShowMarker(-1);
        }

        /// <summary>Shows encounter <paramref name="index"/>'s floor marker and hides every other (-1: none).</summary>
        void ShowMarker(int index)
        {
            for (int k = 0; k < zoneMarkers.Length; k++)
            {
                var m = zoneMarkers[k];
                if (m != null && m.activeSelf != (k == index)) m.SetActive(k == index);
            }
            if (zoneMarkers.Length == 0 && entryMarker != null && entryMarker.activeSelf != (index > 0)) entryMarker.SetActive(index > 0);
        }

        /// <summary>Rolls the lift's shutter up to <see cref="GalleriaArena.ShutterGap"/> over 1.5 s (or straight down).</summary>
        void RollShutter(bool up)
        {
            LiftOpen = up;
            if (!up) ExitReached = false;
            if (liftShutter == null) return;
            if (!_shutterKnown)
            {
                _shutterKnown = true;
                _shutterPos = liftShutter.localPosition;
                _shutterScale = liftShutter.localScale;
            }
            if (_shutterRoll != null) StopCoroutine(_shutterRoll);
            _shutterRoll = null;
            if (up)
            {
                Debug.Log("SKYLINE_ARENA lift open");
                _shutterRoll = StartCoroutine(RollUp(-1f));
            }
            else
            {
                liftShutter.localPosition = _shutterPos;
                liftShutter.localScale = _shutterScale;
            }
        }

        /// <param name="toLocal">How far (local units) the bottom edge ends up; negative: <see cref="GalleriaArena.ShutterGap"/>.</param>
        System.Collections.IEnumerator RollUp(float toLocal)
        {
            // the shutter is a box from the floor to the fascia: rolling up keeps its top and lifts its bottom edge
            float height = _shutterScale.y, gap = GalleriaArena.ShutterGap, t = 0f;
            Vector3 axisUp = liftShutter.parent != null ? liftShutter.parent.InverseTransformVector(Vector3.up) : Vector3.up;
            float unit = axisUp.magnitude > 1e-4f ? 1f / axisUp.magnitude : 1f;   // world metres per local unit along up
            float localGap = toLocal >= 0f ? toLocal : gap / unit;
            float from = _shutterScale.y - liftShutter.localScale.y;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 1.5f);
                float lift = Mathf.Lerp(from, localGap, Mathf.SmoothStep(0f, 1f, t));
                liftShutter.localScale = new Vector3(_shutterScale.x, height - lift, _shutterScale.z);
                liftShutter.localPosition = _shutterPos + Vector3.up * (lift * 0.5f);
                yield return null;
            }
            _shutterRoll = null;
        }

        void OnCacheTaken()
        {
            _ledgeReached = true;
            Trigger(BeatTrigger.BoostLedgeReached, null);
        }

        void OnPlayerWrecked()
        {
            Restarts++;
            Debug.Log("SKYLINE_ARENA wrecked restart life=" + (Restarts + 1));
            for (int i = 0; i < _encounters.Count; i++)
            {
                var e = _encounters[i];
                foreach (var b in e.bots) if (b != null) _botEncounter.Remove(b);
                e.bots.Clear();
                if (e.root != null) Destroy(e.root.gameObject);   // bots, their wrecks and rail bars live under it
                e.root = null;
                e.inCombat = false;
                e.phase = i == 0 ? Phase.Armed : Phase.Waiting;
            }
            _botEncounter.Clear();
            SkylineBolt.ClearAll();
            ShowEntryMarker(false);
            RollShutter(false);
            player.Ledger.Reset();
            player.Restart(U(GalleriaArena.PlayerStart), 0f);
            if (cache != null) cache.ReArm();
            if (wallRunCache != null) wallRunCache.ReArm();
            Restarted?.Invoke();
        }

        // ------------------------------------------------------------------ teaching beats

        bool Fired(BeatTrigger t)
        {
            foreach (var b in _beats) if (b.trigger == t && !b.repeats && !_beatUsed.Contains(b.id)) return false;
            return true;
        }

        void Trigger(BeatTrigger t, string enemy)
        {
            foreach (var b in _beats)
            {
                if (b.trigger != t) continue;
                if (t == BeatTrigger.FirstSeen && b.enemy != enemy) continue;
                if (MomentPassed(b)) continue;   // its moment is already over: not queued, and it stays armed
                if (b.repeats)
                {
                    if (!_lines.Contains(b.id)) _lines.Enqueue(b, Time.time);
                }
                else if (_beatUsed.Add(b.id)) _lines.Enqueue(b, Time.time);
            }
        }

        void PumpLines(float now)
        {
            _dropped.Clear();
            _cut.Clear();
            var b = _lines.Next(now, MomentPassed, _dropped, _cut, LineOrder);
            foreach (var d in _dropped)
            {
                bool rearm = !d.repeats && !Learned(d);
                if (rearm) _beatUsed.Remove(d.id);
                Debug.Log("SKYLINE_LINE " + d.id + (rearm ? " dropped rearmed" : " dropped"));
            }
            foreach (var c in _cut) Debug.Log("SKYLINE_LINE " + c.id + " cut");
            if (b == null)
            {
                if (_cut.Count > 0)
                {
                    // the line's moment ended mid-line and nothing replaces it: take it down
                    var h = SkylineHud.Instance;
                    if (h != null) h.EndSay();
                    if (voice != null) voice.Stop();
                }
                return;
            }
            // a cut line resumes where it was cut (judge round 3: Nan's opener restarted five times): the subtitle shows the
            // whole line for what was left of it, and the voice picks up from there, with no second chime
            float offset = _lines.StartOffset;
            var hud = SkylineHud.Instance;
            if (hud != null) hud.Say(b.speaker, b.text, _lines.CurrentSeconds);
            float delay = b.speaker == Speaker.Concierge && offset <= 0f ? SkylineDamageFeedback.ConciergeChime() : 0f;
            SkylineBabble.Speak(b.speaker, b.text, voice, delay, offset);
            Debug.Log("SKYLINE_LINE " + b.id + (offset > 0f ? " resumed" : ""));
        }

        /// <summary>Ranks lines of one kind: a safety notice by how far the nearest live bot of its kind is (the closer
        /// threat is warned about first, and cuts a notice about one 8 m or more farther); every other line 0.</summary>
        float LineOrder(TeachingBeat b)
        {
            if (b.trigger != BeatTrigger.FirstSeen || player == null) return 0f;
            float best = 999f;
            Vector3 eye = player.EyePosition;
            foreach (var e in _encounters)
            {
                if (e.phase != Phase.Fighting) continue;
                foreach (var bot in e.bots)
                    if (bot != null && bot.Alive && bot.Def != null && bot.Def.id == b.enemy)
                        best = Mathf.Min(best, Vector3.Distance(eye, bot.Center));
            }
            return best;
        }

        /// <summary>Whether a teaching line's moment is over (spec 6.8's table; judge round 1): Nan's opener, while the player
        /// is in the warm-up hall and no fight has started; Dot's Writ lesson, while the practice bot is within 30 m and no
        /// spot has been made yet; the kiosk lessons, while the player is within 10 m of the kiosk and hasn't reached its top;
        /// "that's the spot", while the bot that grew it still has one; a CONCIERGE safety notice, while a bot of its kind is
        /// alive in a fight; the next-fight notice, while the next fight waits for the player. The other reactions (the ledge
        /// reached, the plating broken) have only the queue's 3 s limit.</summary>
        bool MomentPassed(TeachingBeat b)
        {
            if (player == null) return true;
            bool inWarmup = player.transform.position.z <= GalleriaArena.WarmupMaxZ && !AnyFighting();
            switch (b.trigger)
            {
                case BeatTrigger.WarmupStart:
                    return !inWarmup;
                case BeatTrigger.PracticeBotSeen:
                    if (!inWarmup || _spotsMade > 0 || PracticeBot == null || !PracticeBot.Alive) return true;
                    return (PracticeBot.Center - player.EyePosition).sqrMagnitude > LessonRange * LessonRange;
                case BeatTrigger.FirstSpotCreated:
                    return _spotBot == null || !_spotBot.Alive || _spotBot.Spots == null || _spotBot.Spots.Count == 0;
                case BeatTrigger.NearBoostLedge:
                    return !inWarmup || _ledgeReached || !NearKiosk(NearLedgeRange + 4f);
                case BeatTrigger.NearPracticeLedge:
                    return !inWarmup || _counterReached || !NearCounter(NearLedgeRange + 4f);
                case BeatTrigger.LiftOpened:
                    return !LiftOpen || ExitReached;
                case BeatTrigger.FirstSeen:
                    return !AnyLive(b.enemy);
                case BeatTrigger.NextFightArmed:
                    return !NextFightWaiting();
                default:
                    return false;
            }
        }

        /// <summary>A dropped line the player has already learned is not re-armed: Nan's opener and the ledge reaction play
        /// once; the Writ lesson is learned once a spot is made, the kiosk lessons once the ledge is reached.</summary>
        bool Learned(TeachingBeat b)
        {
            if (_lines.WasSaid(b.id)) return true;   // judge round 3: a line once said never comes back
            switch (b.trigger)
            {
                case BeatTrigger.WarmupStart:
                case BeatTrigger.BoostLedgeReached:
                    return true;
                case BeatTrigger.PracticeBotSeen:
                    return _spotsMade > 0;
                case BeatTrigger.NearBoostLedge:
                    return _ledgeReached;
                case BeatTrigger.NearPracticeLedge:
                    return _counterReached;
                case BeatTrigger.LiftOpened:
                    return ExitReached;
                default:
                    return false;
            }
        }

        const float LessonRange = PracticeSeenRange + 5f;

        bool AnyFighting()
        {
            foreach (var e in _encounters) if (e.phase == Phase.Fighting) return true;
            return false;
        }

        bool AnyLive(string enemyId)
        {
            foreach (var e in _encounters)
            {
                if (e.phase != Phase.Fighting) continue;
                foreach (var bot in e.bots)
                    if (bot != null && bot.Alive && bot.Def != null && bot.Def.id == enemyId) return true;
            }
            return false;
        }

        bool NextFightWaiting()
        {
            for (int i = 1; i < _encounters.Count; i++) if (_encounters[i].phase == Phase.Armed) return true;
            return false;
        }
    }
}
