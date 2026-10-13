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
    /// <item><b>Encounters</b>, from <see cref="GalleriaArena.Encounters"/> in order (food-court, skylight, facade): each has
    /// its <see cref="SkylineEncounterZone"/>; the first is armed at the start, each next one once the one before is cleared.
    /// The food court's zone is the hall south of z 5; every later fight's zone is the entry lane round the anchor
    /// (<see cref="GalleriaArena.EntryZoneMinX"/>..), so each fight starts from where its balance was measured. While a later
    /// fight is armed and not started, the <see cref="entryMarker"/> on the floor there is shown and CONCIERGE says the
    /// next-fight notice (a repeating beat).
    /// An armed zone that holds the player's feet starts its encounter: every spawn through <see cref="SkylineBot.Spawn"/>
    /// (seeded by encounter and spawn index; target the player; the player's <see cref="WritTuning"/>; group
    /// encounter index x 100 + the data's group, so no two encounters share a group; a gondola on its rail facing away from
    /// its wall, yaw 90 on the west rail and -90 on the east; every other bot facing the hall's entry, the encounters'
    /// anchor). Logs <c>SKYLINE_ARENA encounter &lt;id&gt; start</c> and, when its last bot is wrecked,
    /// <c>SKYLINE_ARENA encounter &lt;id&gt; cleared</c>.</item>
    /// <item><b>The ledger</b>: <see cref="LedgerRun.BeginCombat"/> at an encounter's first alert,
    /// <see cref="LedgerRun.EndCombat"/> when it is cleared, and <see cref="LedgerRun.Tick"/> every frame (the player leaves
    /// it to the arena), with <see cref="WallReach"/> against the player's movement world.</item>
    /// <item><b>Teaching beats</b> (<see cref="TeachingBeats.M1"/>): each fires once (the next-fight notice every time it
    /// comes); they go through a <see cref="TeachingQueue"/>: each line lasts its reading time (1.5-4 s; the babble is cut to
    /// it and the subtitle stays up for it), 0.5 s between lines, combat notices ahead of pending warm-up hints, shared
    /// triggers in the data's order, and a warm-up hint that has waited over 20 s once the player is in the main hall is
    /// dropped (<c>SKYLINE_LINE &lt;id&gt; dropped-stale</c>). Each line goes to <see cref="SkylineHud.Say"/>, is spoken with
    /// <see cref="SkylineBabble.Speak"/> from the 2D <see cref="voice"/>, and logs <c>SKYLINE_LINE &lt;id&gt;</c>. Triggers:
    /// WarmupStart 1 s after the start (in the editor capture: after the Recorder takes over the clock); PracticeBotSeen with
    /// the practice bot within 25 m, on screen and in sight (checked only once WarmupStart has fired, so Nan opens); FirstSpotCreated on <see cref="SkylineBot.SpotCreated"/>;
    /// NearBoostLedge within 6 m (flat) of the kiosk's footprint; BoostLedgeReached from the <see cref="SkylineScrapCache"/>;
    /// FirstPlatingBreak once <see cref="PlayerArmor.PlatingBreaks"/> reaches 1; FirstSeen per enemy id the first time an
    /// encounter bot of that id goes Alert (the practice bot never counts), CONCIERGE's gondola notice included.</item>
    /// <item><b>Wrecked</b>: the Unit's wreck restarts the arena at the warm-up (Task 14's <see cref="SkylinePlayer.Wrecked"/>
    /// and <see cref="SkylinePlayer.Restart"/>): every encounter's bots, wrecks and rails are removed (and bolts in flight), the food court is armed
    /// again, the ledger is reset, the scrap cache is put back and the player stands at <see cref="GalleriaArena.PlayerStart"/>
    /// facing +Z. Beats that have played stay played. Logs <c>SKYLINE_ARENA wrecked restart life=&lt;n&gt;</c>.</item>
    /// </list>
    /// </summary>
    public sealed class SkylineArenaDirector : MonoBehaviour
    {
        public const float BeatGap = 0.5f, WarmupDelay = 1f, PracticeSeenRange = 25f, NearLedgeRange = 6f;
        const float RecorderWait = 8f, ChestHeight = 1.25f;

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
        [Tooltip("2D source the teaching lines are spoken from.")]
        public AudioSource voice;
        [Tooltip("Where the encounters' bots are spawned (one child per encounter).")]
        public Transform botsRoot;
        [Tooltip("The floor marker at the entry lane, shown while a later fight is armed and waiting for the player.")]
        public GameObject entryMarker;

        public SkylineBot PracticeBot { get; private set; }
        public int EncounterCount => _encounters.Count;
        /// <summary>How many times the Unit has been wrecked and the arena restarted.</summary>
        public int Restarts { get; private set; }
        public event Action Restarted;
        public event Action<int> EncounterStarted, EncounterCleared;

        readonly List<Encounter> _encounters = new List<Encounter>();
        readonly Dictionary<SkylineBot, int> _botEncounter = new Dictionary<SkylineBot, int>();
        readonly HashSet<string> _beatUsed = new HashSet<string>();
        readonly TeachingQueue _lines = new TeachingQueue();
        readonly List<TeachingBeat> _dropped = new List<TeachingBeat>();
        TeachingBeat[] _beats = new TeachingBeat[0];
        float _warmupAt = -1f;
        bool _started, _warmupTriggered;

        public Phase PhaseOf(int i) => i >= 0 && i < _encounters.Count ? _encounters[i].phase : Phase.Waiting;
        public string IdOf(int i) => i >= 0 && i < _encounters.Count ? _encounters[i].def.id : "";
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
            var defs = GalleriaArena.Encounters();
            for (int i = 0; i < defs.Length; i++)
            {
                var e = new Encounter { def = defs[i], phase = Phase.Waiting };
                foreach (var z in zones) if (z != null && z.encounterIndex == i) e.zone = z;
                if (e.zone == null) Debug.LogWarning("SKYLINE_ARENA no zone for encounter " + defs[i].id);
                _encounters.Add(e);
            }
            if (_encounters.Count > 0) _encounters[0].phase = Phase.Armed;
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
            // In the editor capture the Recorder starts about a second into Play mode; count the warm-up line from there, so
            // Nan's line is in the recording.
            if (SkylineRunArgs.Has("-skylineOut"))
            {
                float giveUp = Time.unscaledTime + RecorderWait;
                while (!(Time.captureDeltaTime > 0f) && Time.unscaledTime < giveUp) yield return null;
            }
            _warmupAt = Time.unscaledTime + WarmupDelay;
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
            float now = Time.unscaledTime;

            if (_warmupAt >= 0f && now >= _warmupAt)
            {
                _warmupAt = -1f;
                _warmupTriggered = true;
                Trigger(BeatTrigger.WarmupStart, null);
            }
            // the warm-up's own lessons wait for Nan's opener (the practice bot is in view from the very first frame)
            if (_warmupTriggered && !Fired(BeatTrigger.PracticeBotSeen) && PracticeBotInView()) Trigger(BeatTrigger.PracticeBotSeen, null);
            if (_warmupTriggered && !Fired(BeatTrigger.NearBoostLedge) && NearKiosk()) Trigger(BeatTrigger.NearBoostLedge, null);
            if (!Fired(BeatTrigger.FirstPlatingBreak) && player.Armor != null && player.Armor.PlatingBreaks >= 1) Trigger(BeatTrigger.FirstPlatingBreak, null);

            Vector3 feet = player.transform.position;
            for (int i = 0; i < _encounters.Count; i++)
            {
                var e = _encounters[i];
                if (e.phase == Phase.Armed && e.zone != null && e.zone.Contains(feet)) StartEncounter(i);
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

        bool PracticeBotInView()
        {
            var b = PracticeBot;
            var cam = player.Camera;
            if (b == null || !b.Alive || cam == null) return false;
            Vector3 eye = player.EyePosition, c = b.Center;
            Vector3 d = c - eye;
            float dist = d.magnitude;
            if (dist > PracticeSeenRange || dist < 1e-3f) return false;
            Vector3 vp = cam.WorldToViewportPoint(c);
            if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) return false;
            return !SkylineBot.CastClear(eye, d / dist, dist - 0.05f, b.transform, out _);
        }

        bool NearKiosk()
        {
            Vector3 f = player.transform.position;
            Vector3 c = U(GalleriaArena.BoostLedgeTop);
            float h = GalleriaArena.BoostLedgeSize * 0.5f;
            float dx = Mathf.Max(0f, Mathf.Abs(f.x - c.x) - h), dz = Mathf.Max(0f, Mathf.Abs(f.z - c.z) - h);
            return dx * dx + dz * dz <= NearLedgeRange * NearLedgeRange;
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

        void OnSpot(SkylineBot bot) => Trigger(BeatTrigger.FirstSpotCreated, null);

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
                _encounters[i + 1].phase = Phase.Armed;
                ShowEntryMarker(true);
                Trigger(BeatTrigger.NextFightArmed, null);
            }
            EncounterCleared?.Invoke(i);
            if (i + 1 >= _encounters.Count) Debug.Log("SKYLINE_ARENA all cleared");
        }

        void ShowEntryMarker(bool on)
        {
            if (entryMarker != null && entryMarker.activeSelf != on) entryMarker.SetActive(on);
        }

        void OnCacheTaken() => Trigger(BeatTrigger.BoostLedgeReached, null);

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
            foreach (var bolt in FindObjectsByType<SkylineBolt>(FindObjectsSortMode.None)) if (bolt != null) Destroy(bolt.gameObject);
            ShowEntryMarker(false);
            player.Ledger.Reset();
            player.Restart(U(GalleriaArena.PlayerStart), 0f);
            if (cache != null) cache.ReArm();
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
                if (b.repeats)
                {
                    if (!_lines.Contains(b.id)) _lines.Enqueue(b, Time.unscaledTime);
                }
                else if (_beatUsed.Add(b.id)) _lines.Enqueue(b, Time.unscaledTime);
            }
        }

        void PumpLines(float now)
        {
            _dropped.Clear();
            var b = _lines.Next(now, MomentPassed, _dropped);
            foreach (var d in _dropped) Debug.Log("SKYLINE_LINE " + d.id + " dropped-stale");
            if (b == null) return;
            float seconds = SkylineBabble.LineSeconds(b.text);
            var hud = SkylineHud.Instance;
            if (hud != null) hud.Say(b.speaker, b.text, seconds);
            SkylineBabble.Speak(b.speaker, b.text, voice);
            Debug.Log("SKYLINE_LINE " + b.id);
        }

        /// <summary>A warm-up hint's moment is over once the player is in the main hall.</summary>
        bool MomentPassed(TeachingBeat b) => player != null && player.transform.position.z > GalleriaArena.WarmupMaxZ;
    }
}
