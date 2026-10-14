using System.Collections;
using System.Collections.Generic;
using JamKit.Movement;
using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The arena's scripted run (docs/SKYLINE-PLAN-M1.md Task 16, made to win in Task 17, made to play like a person in the
    /// fix round of 2026-10-14), only under <see cref="SkylineRunArgs.Demo"/>: it drives <see cref="SkylinePlayer.ScriptedInput"/>
    /// like a player's hands, so the recording and the frame-time probe see the arena busy and cleared. Hit stop is off, so the
    /// time scale stays 1; every wait and timer runs on <c>Time.time</c>, which under the Recorder's capture step is the
    /// recording's own clock (the guns' too). In the editor capture it starts once the Recorder's step is set
    /// (<see cref="SkylineRecorderWait"/>). Each step logs <c>SKYLINE_DEMO &lt;step&gt;</c>. Seeded: a recording repeats.
    /// <list type="number">
    /// <item><b>The hands.</b> The view turns like a wrist on a mouse (<see cref="HumanAim"/>: a short reaction beat on a new
    /// target, a fast turn that overshoots a little and settles, a capped turn rate, a small lag behind anything moving); it
    /// never snaps onto a target. The fire button is pressed only while the crosshair's own ray lands on the bot it is shooting
    /// at, so a turn still under way, or a drone that strafed out of the lag, costs it the shot, as it would a player.</item>
    /// <item>The warm-up: walk to 10 m from the practice bot; three Writ shots at its weak point (a spot opens), one at the
    /// spot, each once the aim has settled; swap to the Breacher; walk to 0.8 m short of the kiosk's south face and turn to
    /// face it; jump, double jump at the jump's apex, at the double jump's apex the view is down at 85 degrees (it starts
    /// pitching down at the first apex) and it fires (the boost), holding forward throughout (the mantle); take the cache on
    /// top; swap back to the Writ; drop off the north side.</item>
    /// <item><b>Judge round 2.</b> The warm-up also climbs the practice counter (jump, double jump, mantle) and runs the west
    /// wall onto the lockers (run, kick off toward them, double jump, mantle: <c>wall-run</c>, <c>wall-kick</c>,
    /// <c>on-lockers</c>), and each lesson waits for its line to be said before doing the thing (<c>SKYLINE_LINE</c> read
    /// from the log). In the fights it <b>watches</b> each kind of bot once before killing it (<c>watch &lt;kind&gt;</c>): a
    /// drone strafing, the gondola on its rail, a spotlight's beam from the spotlight's end until it locks, a floor-care
    /// unit's sweep; and it <b>swaps to the Breacher</b> for bots within 5 m (<c>breacher-on</c> / <c>breacher-off</c>). After
    /// the lift it turns back to the cleared hall (<c>vista</c>) and pans slowly until the recording ends.</item>
    /// <item>Through the doorway into the hall, then each encounter in order along the route (food-court by the entrance,
    /// facade in the middle, skylight by the mezzanine; judge round 1): walk into its zone, the lane round its own anchor
    /// (the food court's to the information booth's U; the facade's to the two staggered stall backs; the skylight's under
    /// the mezzanine), shooting anything in its line of fire on the way, then fight until it is cleared, then salvage (strip
    /// the fresh wrecks it can reach in time while the plating has room) and top up the magazine. Then the far goal: to the
    /// service lift, whose shutter rolls up once the hall is cleared, and a slide under it (<c>to-lift</c>,
    /// <c>slide-under-shutter</c>, <c>lift</c>).</item>
    /// <item><b>The eyes</b> (judge round 1: a third of the old recording was a wall at a metre): on a target, the target; in
    /// cover, the corner it will peek from (past the cover's edge, where the threat will show), never the bot behind the
    /// wall; running, the way ahead; otherwise the nearest live bot only along a line that is open for at least 6 m (else
    /// the open bearing nearest it). Looks without a target turn at a calm 200 degrees a second at most; the boost's look
    /// down starts at the jump and is spread over the climb.</item>
    /// <item><b>The fight</b> is a cover cycle, as a player plays a shooter. It picks a <b>cover point</b> (a spot beside a
    /// piece of cover that no live drone or spotlight sees, even after a couple of metres of their patrol, with a peek spot
    /// next to it that has a line of fire; near it, and toward the bots), runs there (sprinting, now and then sliding or
    /// hopping on the way), <b>ducks</b> (reloads there if half the magazine is gone), then <b>peeks</b>: steps out to the
    /// peek spot and shoots, strafing side to side (direction changes every 0.5-1.2 s, a hop now and then), for 1.8-3.2 s or
    /// until the magazine is dry or it loses 15 plating, then ducks back. After two or three peeks, or when a drone's patrol
    /// finds the cover point, it moves to another. <b>Target:</b> the bot whose shot line is really open (a ray from the eye
    /// with the gun's own mask must land on that bot first); spotlights first, then a floor-care unit within 7 m, then the
    /// nearest. <b>Aim point:</b> its newest Writ spot, else its weak point, else its centre, else any body part, a plate
    /// last. <b>Reload:</b> when dry, in cover with half the magazine gone, or when nothing has been in sight for 0.8 s with
    /// half gone. With no cover point to be had it plays the open game: strafe while shooting, advance to a firing spot when
    /// nothing is in sight, fall back to the booth when hurt. It strips a fresh wreck within 8 m (16 m with nothing in sight)
    /// when the plating has 10 of room; it jumps (and double jumps) a floor-care unit's sweep when one telegraphs within
    /// reach. Walking goes round the hall's cover boxes (FightSim's detour to the best corner). A per-fight watchdog
    /// (<c>SKYLINE_DEMO watchdog &lt;id&gt; fight-time|no-damage</c>): a fight not cleared in 90 s, or no enemy damaged for
    /// 20 s, sends it to a firing spot until it damages something. Mode lines: <c>SKYLINE_DEMO &lt;id&gt;
    /// to-hide|move-cover|duck|engage|strip|fall-back|advance|hold|watchdog-move</c> (engage is a peek).</item>
    /// <item>If the Unit is wrecked the director restarts the arena at the warm-up; the run starts again from the start,
    /// skipping the lessons it has done (logs <c>SKYLINE_DEMO restart</c>). No invulnerability: the enemy data, the
    /// layout and the Unit's armour are the game's.</item>
    /// </list>
    /// </summary>
    public sealed class SkylineArenaDemo : MonoBehaviour
    {
        const float PracticeDistance = 10f, KioskStandOff = 0.8f, ShotGap = 0.5f, SpotWait = 1.8f;
        const float ArriveRadius = 0.12f, CoarseArrive = 0.6f, CoverMargin = 0.75f, CornerMargin = 0.9f;
        const float LowPlating = 30f, AdvanceAfter = 2.5f, HurtAdvanceAfter = 5f, StripRange = 8f, FarStripRange = 16f, MaxSight = 45f, StuckSeconds = 1f;
        const float SightGrace = 0.4f, IdleReloadAfter = 0.8f, StripRoom = 10f, RunSpeed = 6.5f, SalvageLimit = 6f, AdvanceRepick = 1.5f;
        const float HideRadius = 2.5f, ToHideLimit = 6f;
        /// <summary>The per-fight watchdog: a fight not cleared in this long, or no enemy damaged for this long, sends the
        /// run to a firing position with a clear line to the nearest live enemy.</summary>
        const float FightLimit = 90f, NoDamageLimit = 20f, WatchRepick = 3f, WatchRearm = 30f;
        /// <summary>The cover cycle: how far from the piece a cover point stands (m), how far it looks for one (m), the slack a
        /// drone's patrol gets when a cover point is judged unseen (m), the peek's length (s), the duck's (s), and the plating
        /// lost in one peek that sends it back into cover.</summary>
        const float CoverOffset = 1.0f, CoverSearch = 16f, PatrolSlack = 1.5f, PeekMin = 1.8f, PeekMax = 3.2f, DuckMin = 0.35f, DuckMax = 0.8f, PeekHurt = 15f;
        /// <summary>A strafe holds its direction this long (s) before it changes.</summary>
        const float StrafeMin = 0.5f, StrafeMax = 1.2f;
        /// <summary>The settle a shot at the practice bot waits for: the view within this many degrees of the point, turning
        /// slower than this (degrees a second).</summary>
        const float SettledDegrees = 0.6f, SettledRate = 25f;

        public SkylinePlayer player;
        public SkylineArenaDirector director;
        public SkylineScrapCache cache;

        // held input
        Vector2 _move;
        bool _sprint;
        float _crouchUntil = -1f;
        // one-frame presses
        bool _jump, _fire, _swap, _reload, _crouchPress;
        /// <summary>The bot the fight wants shot this frame: fire is pressed only while the crosshair's ray lands on it.</summary>
        SkylineBot _fireAt;
        // aim: what the hands want to look at, and the hands
        bool _aimFixed, _snapAim;
        float _aimYaw, _aimPitch, _wantYaw, _wantPitch;
        Vector3? _aimPoint;
        readonly HumanAim _hand = new HumanAim();
        bool _handReady;

        int _life;
        bool _lessonsDone, _fighting, _djArmed, _djTakeoff;
        float _acidJumpAt = -1f, _stuckSince = -1f, _sideStepUntil = -1f, _sideSign = 1f;
        // A floor-care unit telegraphs again on the same beat the pilot lands, so jumping every sweep looped forever (the
        // canister is never hit from mid-air; the creator saw it in the pass, 2026-10-14). After two jumps over the same unit
        // in a row, the next sweep is met by backing out of its reach on the ground, still shooting.
        SkylineBot _acidBot;
        int _acidJumpsInARow;
        float _backOffUntil = -1f;
        const int AcidJumpsBeforeBackOff = 2;
        Vector3 _stuckFrom;
        /// <summary>Every choice the run makes by chance (strafe timing, peek lengths, slides, hops, reaction beats) draws from
        /// this one seeded stream, so a recording repeats.</summary>
        System.Random _rng = new System.Random(1616);
        static readonly List<Rect> _cover = new List<Rect>();

        // the cover cycle
        enum Tactic { None, Move, Duck, Peek }
        Tactic _tac;
        Vector3 _duck, _peek, _lastDuck;
        bool _hasLastDuck, _slideTried;
        float _tacUntil, _tacRetryAt, _moveGiveUp, _nextDuckCheck, _platingAtPeek, _peekNoSight, _nextHop;
        int _peeksLeft;
        Vector3 _nextPeek;
        bool _hasNextPeek;
        float _nextPeekLook;
        readonly List<Vector3> _candidates = new List<Vector3>();
        readonly List<float> _scores = new List<float>();

        static readonly Vector3 BoothHide = new Vector3(0f, 0f, -12.2f);
        /// <summary>Each fight's pocket, where it opens (and falls back to when hurt in the open game): the booth's U for the
        /// food court, between the two staggered stall backs for the facade, under the mezzanine for the skylight.</summary>
        static Vector3 HideOf(string id)
        {
            switch (id)
            {
                case "food-court": return BoothHide;
                case "facade": return SkylineArenaDirector.U(GalleriaArena.FacadeAnchor);
                case "skylight": return new Vector3(-6.5f, 0f, 14f);
                default: return BoothHide;
            }
        }

        /// <summary>The calm turn rate (degrees a second) of a look with nothing to shoot; a target gets the hands' full rate.</summary>
        const float CalmRate = 200f, FullRate = 650f;
        bool _calm;

        // judge round 2: lines heard (from the log), bots watched once per kind, the Breacher at close range
        readonly Dictionary<string, float> _linesAt = new Dictionary<string, float>();
        readonly HashSet<string> _watched = new HashSet<string>();
        SkylineBot _watch;
        float _watchUntil;
        string _watchKind;
        bool _breacher;
        float _breacherOffAt;
        const float BreacherRange = 5f, WatchDrone = 2f, WatchGondola = 2.5f, WatchSpotlight = 3f, WatchSweep = 1.6f;

        void OnLog(string message, string stack, LogType type)
        {
            if (message == null || !message.StartsWith("SKYLINE_LINE ")) return;
            string rest = message.Substring("SKYLINE_LINE ".Length);
            if (rest.IndexOf(' ') >= 0) return;   // "dropped", "cut": not said
            _linesAt[rest] = Time.time;
        }

        /// <summary>Waits (at most <paramref name="max"/> s) for line <paramref name="id"/> to start, then for its reading time:
        /// a player hears the line, then does the thing.</summary>
        IEnumerator AwaitLine(string id, float max)
        {
            float until = Time.time + max;
            while (!_linesAt.ContainsKey(id) && Time.time < until) yield return null;
            if (!_linesAt.TryGetValue(id, out float at)) yield break;
            string text = null;
            foreach (var b in TeachingBeats.M1()) if (b.id == id) text = b.text;
            float end = at + (text != null ? SkylineBabble.LineSeconds(text) : 3f) + 0.2f;
            while (Time.time < end && Time.time < until + 6f) yield return null;
        }

        void Start()
        {
            if (!SkylineRunArgs.Demo || player == null || director == null)
            {
                enabled = false;
                return;
            }
            BuildCover();
            player.HitStopEnabled = false;
            player.ScriptedInput = NextFrame;
            director.Restarted += OnRestarted;
            SkylineBot.StateEntered += OnBotState;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (director != null) director.Restarted -= OnRestarted;
            SkylineBot.StateEntered -= OnBotState;
            Application.logMessageReceived -= OnLog;
            if (player != null && player.ScriptedInput == (System.Func<SkylineInputFrame>)NextFrame) player.ScriptedInput = null;
        }

        static void Log(string step) => Debug.Log("SKYLINE_DEMO " + step);

        /// <summary>The Breacher (slot 1) has a round in the magazine or one in the special reserve to reload.</summary>
        bool BreacherHasShots()
        {
            var lo = player.Loadout;
            if (lo == null || lo.Count < 2) return false;
            var g = lo[1];
            return g != null && (g.Ammo > 0 || !g.Def.special || g.State.Reserve > 0);
        }

        static void BuildCover()
        {
            _cover.Clear();
            foreach (var b in GalleriaArena.HallCover()) _cover.Add(Rect.MinMaxRect(b.min.X, b.min.Z, b.max.X, b.max.Z));
            var lobby = GalleriaArena.LiftLobby;
            _cover.Add(Rect.MinMaxRect(lobby.min.X, lobby.min.Z, lobby.max.X, lobby.max.Z));
            var k = GalleriaArena.BoostLedgeTop;
            float h = GalleriaArena.BoostLedgeSize * 0.5f;
            _cover.Add(Rect.MinMaxRect(k.X - h, k.Z - h, k.X + h, k.Z + h));
            var pc = GalleriaArena.PracticeLedgeTop;
            float ph = GalleriaArena.PracticeLedgeSize * 0.5f;
            _cover.Add(Rect.MinMaxRect(pc.X - ph, pc.Z - ph, pc.X + ph, pc.Z + ph));
            var wr = GalleriaArena.WallRunLedge;
            _cover.Add(Rect.MinMaxRect(wr.min.X, wr.min.Z, wr.max.X, wr.max.Z));
            // the walker's shortest way round them (fix "stuck": the old one-corner detour walked into the booth's screens)
            var boxes = new List<(System.Numerics.Vector2 min, System.Numerics.Vector2 max)>(_cover.Count);
            foreach (var r in _cover) boxes.Add((new System.Numerics.Vector2(r.xMin, r.yMin), new System.Numerics.Vector2(r.xMax, r.yMax)));
            _path = new CoverPath(boxes, CoverMargin, CornerMargin);
        }

        static CoverPath _path;

        float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ input

        SkylineInputFrame NextFrame()
        {
            float now = Time.time;
            var f = new SkylineInputFrame
            {
                move = _move,
                sprintHeld = _sprint,
                jumpPressed = _jump && now >= _backOffUntil,
                jumpHeld = _jump && now >= _backOffUntil,
                crouchPressed = _crouchPress,
                crouchHeld = _crouchPress || now < _crouchUntil,
                swapPressed = _swap,
                reloadPressed = _reload,
            };
            if (now < _backOffUntil) { f.move = new Vector2(0f, -1f); f.sprintHeld = false; }   // backpedal out of the sweep, facing it

            // the hands: turn toward what is wanted like a wrist, never a snap (bar the one scripted settle at the boost)
            if (!_handReady)
            {
                _hand.Snap(player.Yaw, player.Pitch);
                _handReady = true;
            }
            if (_aimFixed) { _wantYaw = _aimYaw; _wantPitch = _aimPitch; }
            else if (_aimPoint.HasValue)
            {
                AimAssist.Angles(N(player.EyePosition), N(_aimPoint.Value), out float yaw, out float pitch);
                _wantYaw = yaw;
                _wantPitch = pitch;
            }
            else { _wantYaw = _hand.Yaw; _wantPitch = _hand.Pitch; }
            _wantPitch = Mathf.Clamp(_wantPitch, -SkylinePlayer.MaxPitch, SkylinePlayer.MaxPitch);
            _hand.maxRate = _calm ? CalmRate : FullRate;
            if (_snapAim) _hand.Snap(_wantYaw, _wantPitch);
            else _hand.Step(Time.deltaTime, _wantYaw, _wantPitch);
            player.SetAim(_hand.Yaw, _hand.Pitch);

            // the trigger: a scripted press, or the fight's target under the crosshair (the shot goes along this very aim)
            bool fire = _fire || (_fireAt != null && _fireAt.Alive && OnTarget(_fireAt));
            f.firePressed = fire;
            f.fireHeld = fire;

            _jump = _fire = _swap = _reload = _crouchPress = _snapAim = false;
            _fireAt = null;
            return f;
        }

        /// <summary>The crosshair's own ray (the eye along the aim, the guns' mask) lands on one of <paramref name="b"/>'s hit zones first.</summary>
        bool OnTarget(SkylineBot b)
        {
            if (!Physics.Raycast(player.EyePosition, player.AimDirection, out var hit, MaxSight + 5f, SkylineShooting.ShotMask, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.TryGetComponent<SkylineHitZone>(out var z) && z.owner == b;
        }

        /// <summary>The view is on what it wants and has stopped turning (a careful shot).</summary>
        bool Settled => _hand.ErrorTo(_wantYaw, _wantPitch) <= SettledDegrees && _hand.Rate <= SettledRate && _hand.Holding <= 0f;

        static NVec3 N(Vector3 v) => new NVec3(v.x, v.y, v.z);

        MoveSim Sim => player.Movement != null ? player.Movement.Sim : null;
        Vector3 Feet() { var s = Sim; return s != null ? new Vector3(s.Position.X, s.Position.Y, s.Position.Z) : player.transform.position; }
        bool Grounded => Sim == null || Sim.Grounded;
        float VelY => Sim != null ? Sim.Velocity.Y : 0f;

        void Update()
        {
            // the acid jump: jump just before the sweep, double jump at the jump's apex
            var sim = Sim;
            if (sim == null) return;
            if (_acidJumpAt >= 0f && Time.time >= _acidJumpAt)
            {
                _acidJumpAt = -1f;
                if (_fighting && sim.Grounded)
                {
                    _jump = true;
                    _djArmed = true;
                    _djTakeoff = false;
                }
            }
            if (_djArmed)
            {
                if (!sim.Grounded) _djTakeoff = true;
                if (_djTakeoff && sim.Grounded) _djArmed = false;
                else if (_djTakeoff && sim.Velocity.Y <= 0f)
                {
                    _jump = true;
                    _djArmed = false;
                }
            }
        }

        void OnBotState(SkylineBot bot, BotState state)
        {
            if (!_fighting || bot == null || bot.practice || state != BotState.Telegraph || bot.Def == null || !(bot.Def.moveSpeed > 0f)) return;
            Vector3 p = bot.transform.position, f = Feet();
            float flat = new Vector2(p.x - f.x, p.z - f.z).magnitude;
            if (flat > bot.Def.sweepRadius + 1.5f || _acidJumpAt >= 0f || _djArmed || Time.time < _backOffUntil) return;
            _acidJumpsInARow = bot == _acidBot ? _acidJumpsInARow + 1 : 1;
            _acidBot = bot;
            if (_acidJumpsInARow > AcidJumpsBeforeBackOff)
            {
                _acidJumpsInARow = 0;
                _backOffUntil = Time.time + bot.Def.telegraphSeconds + 0.6f;
                Log("acid-back-off");
                return;
            }
            _acidJumpAt = Time.time + Mathf.Max(0f, bot.Def.telegraphSeconds - 0.15f);
        }

        void OnRestarted()
        {
            StopAllCoroutines();
            _move = Vector2.zero;
            _sprint = false;
            _jump = _fire = _swap = _reload = _crouchPress = false;
            _crouchUntil = -1f;
            _fireAt = null;
            _aimFixed = false;
            _aimPoint = null;
            _handReady = false;   // the restart puts the view somewhere new: the hands start from there
            _fighting = false;
            _djArmed = false;
            _acidJumpAt = -1f;
            _acidBot = null;
            _acidJumpsInARow = 0;
            _backOffUntil = -1f;
            _tac = Tactic.None;
            _hasLastDuck = false;
            _watch = null;
            _breacher = false;
            _life++;
            StartCoroutine(Run());
        }

        // ------------------------------------------------------------------ the run

        static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        IEnumerator Run()
        {
            if (_life == 0)
            {
                if (SkylineRunArgs.Has("-skylineOut")) yield return SkylineRecorderWait.Wait("SKYLINE_DEMO");
                Log("start");
            }
            else
            {
                Log("restart life=" + (_life + 1));
                yield return Wait(0.3f);
            }

            if (!_lessonsDone)
            {
                yield return Lessons();
                _lessonsDone = true;
            }
            else
            {
                // round the kiosk, east of it
                Log("run-to-doorway");
                yield return MoveTo(new Vector3(3.5f, 0f, -40f), true, CoarseArrive, 4f);
                yield return MoveTo(new Vector3(3.5f, 0f, -28f), true, CoarseArrive, 4f);
            }
            if (player.Loadout.ActiveIndex != 0)
            {
                _swap = true;
                yield return null;
            }
            yield return MoveTo(new Vector3(0f, 0f, -23f), true, CoarseArrive, 5f);
            Log("doorway");

            for (int i = 0; i < director.EncounterCount; i++) yield return Fight(i);
            yield return ToTheLift();
            Log("done");
            _move = Vector2.zero;
            _sprint = false;
        }

        /// <summary>Aims at <paramref name="point"/> (on <paramref name="bot"/>) and fires once the view has settled on it (a
        /// careful shot), or after <paramref name="maxWait"/> s whatever the aim; logs <paramref name="log"/> at the shot.</summary>
        IEnumerator CarefulShot(SkylineBot bot, System.Func<Vector3> point, string log, float maxWait)
        {
            _aimFixed = false;
            float until = Time.time + maxWait;
            _aimPoint = point();
            yield return null;
            while (Time.time < until && !(Settled && OnTarget(bot)))
            {
                _aimPoint = point();
                yield return null;
            }
            _aimPoint = point();
            Log(log);
            _fire = true;
            yield return null;
        }

        IEnumerator Lessons()
        {
            var bot = director.PracticeBot;
            Vector3 start = Feet();
            if (bot != null)
            {
                Log("walk-to-practice-bot");
                Vector3 c = bot.transform.position;
                Vector3 away = new Vector3(start.x - c.x, 0f, start.z - c.z);
                if (away.sqrMagnitude < 1e-4f) away = Vector3.back;
                Vector3 spot = new Vector3(c.x, 0f, c.z) + away.normalized * PracticeDistance;
                _aimPoint = WeakPoint(bot);
                yield return MoveTo(spot, false, CoarseArrive, 4f, () => WeakPoint(bot));
                _move = Vector2.zero;
                yield return Wait(0.4f);
                // Dot's lesson first: then the shots
                yield return AwaitLine("dot-writ-spots", 6f);
                for (int s = 0; s < 3; s++)
                {
                    yield return CarefulShot(bot, () => WeakPoint(bot), "writ-shot " + (s + 1), 1.2f);
                    yield return Wait(ShotGap);
                }
                float giveUp = Time.time + SpotWait;
                while (bot.Spots.Count == 0 && Time.time < giveUp) yield return null;
                if (bot.Spots.Count > 0)
                {
                    var sp = bot.Spots.All[bot.Spots.Count - 1];
                    yield return CarefulShot(bot, () => bot.SpotWorld(sp), "writ-spot-shot", 1.2f);
                    yield return Wait(ShotGap);
                }
                else Log("writ-no-spot");
            }

            yield return PracticeCounter();

            Log("swap-breacher");
            _aimPoint = null;
            _swap = true;
            yield return null;
            yield return Wait(player.Loadout.Active.Def.Clip(HandlingClipKind.Equip).seconds + 0.2f);

            // to 0.8 m short of the kiosk's south face, round its west side if need be
            Vector3 k = SkylineArenaDirector.U(GalleriaArena.BoostLedgeTop);
            float half = GalleriaArena.BoostLedgeSize * 0.5f;
            Vector3 stand = new Vector3(k.x, 0f, k.z - half - KioskStandOff);
            Log("walk-to-kiosk");
            yield return MoveTo(stand, false, ArriveRadius, 6f);
            _move = Vector2.zero;
            _aimFixed = true;
            _aimYaw = 0f;
            _aimPitch = 0f;
            // stop, and turn to face the kiosk (the hands turn; the jump waits until they have)
            float settle = Time.time + 1.2f;
            while (Time.time < settle && ((Sim != null && new Vector2(Sim.Velocity.X, Sim.Velocity.Z).magnitude > 0.05f) || !Settled)) yield return null;
            // look up at the kiosk's top while Dot explains the boost, then back level: then the move
            _aimPitch = -25f;
            _calm = true;
            yield return AwaitLine("dot-breacher-boost", 7f);
            _aimPitch = 0f;
            float level = Time.time + 0.8f;
            while (Time.time < level && !Settled) yield return null;
            _calm = false;
            yield return Wait(0.2f);

            // jump, double jump at the apex, the boost at the double jump's apex; forward held throughout (the mantle). The
            // view starts down toward the feet at the first apex, so it is there (85 degrees) by the shot.
            _move = new Vector2(0f, 1f);
            _sprint = true;
            Log("jump");
            _jump = true;
            yield return UntilAirborne(0.5f);
            // the look down starts with the climb and is spread over it (a readable tilt, not a whip at the apex)
            _calm = true;
            _aimPitch = 55f;
            yield return UntilApex(2f);
            _aimPitch = SkylinePlayer.MaxPitch;
            Log("double-jump");
            _jump = true;
            yield return UntilRising(0.4f);
            yield return UntilApex(2f);
            _snapAim = true;                                       // the last few degrees of the look down, on the frame before the shot
            yield return null;
            Log("boost-fire");
            _fire = true;
            yield return null;
            Log("mantle");
            _aimPitch = 10f;
            float top = k.y;
            float until = Time.time + 3f;
            while (Time.time < until && !(Grounded && Feet().y > top - 0.2f)) yield return null;
            _aimPitch = 0f;
            if (Feet().y > top - 0.2f)
            {
                Log("on-ledge");
                yield return MoveTo(new Vector3(k.x, top, k.z), false, 0.4f, 2f);
                if (cache != null && cache.IsTaken) Log("take-cache");
            }
            else Log("boost-missed");
            _move = Vector2.zero;
            _sprint = false;
            _aimFixed = false;
            _calm = false;

            Log("swap-writ");
            _swap = true;
            yield return null;
            Log("drop-off");
            yield return MoveTo(new Vector3(k.x, Feet().y, k.z + half + 1.5f), false, CoarseArrive, 3f);
            yield return Wait(0.3f);
            yield return Lockers();
            Log("run-to-doorway");
        }

        /// <summary>Nan's lesson: 0.8 m north of the practice counter, facing it; jump, double jump at the apex, forward into
        /// the mantle; then off its north side.</summary>
        IEnumerator PracticeCounter()
        {
            var pc = SkylineArenaDirector.U(GalleriaArena.PracticeLedgeTop);
            float ph = GalleriaArena.PracticeLedgeSize * 0.5f;
            Log("practice-counter");
            yield return MoveTo(new Vector3(pc.x, 0f, pc.z + ph + 0.8f), false, ArriveRadius, 7f);
            _move = Vector2.zero;
            _aimFixed = true;
            _aimYaw = 180f;
            _aimPitch = -15f;
            _calm = true;
            float settle = Time.time + 1.5f;
            while (Time.time < settle && ((Sim != null && new Vector2(Sim.Velocity.X, Sim.Velocity.Z).magnitude > 0.05f) || !Settled)) yield return null;
            yield return AwaitLine("nan-double-jump-mantle", 3f);
            _aimPitch = 0f;
            _move = new Vector2(0f, 1f);
            _sprint = true;
            Log("counter-jump");
            _jump = true;
            yield return UntilAirborne(0.5f);
            yield return UntilApex(2f);
            Log("counter-double-jump");
            _jump = true;
            float until = Time.time + 3f;
            while (Time.time < until && !(Grounded && Feet().y > pc.y - 0.3f)) yield return null;
            Log(Feet().y > pc.y - 0.3f ? "on-counter" : "counter-missed");
            _move = Vector2.zero;
            _sprint = false;
            yield return Wait(0.5f);
            _aimFixed = false;
            yield return MoveTo(new Vector3(pc.x, Feet().y, pc.z + ph + 1.5f), false, CoarseArrive, 3f);
            _calm = false;
        }

        /// <summary>The wall-run lesson (WallRunLedgeTests' sequence): from the start by the west wall facing north, sprint with
        /// the stick a little into the wall; jump 6 m short of the lockers; on the wall, 3 m short, kick off it with the stick
        /// 45 degrees toward them; double jump at that jump's apex; forward into the mantle; take the cache; off the east side.</summary>
        IEnumerator Lockers()
        {
            var wr = GalleriaArena.WallRunLedge;
            Vector3 start = SkylineArenaDirector.U(GalleriaArena.WallRunStart);
            float face = wr.min.Z;
            Log("walk-to-wall-run");
            yield return MoveTo(start, true, 0.3f, 8f);
            _move = Vector2.zero;
            _aimFixed = true;
            _aimYaw = 0f;
            _aimPitch = -8f;
            _calm = true;
            float settle = Time.time + 1.2f;
            while (Time.time < settle && ((Sim != null && new Vector2(Sim.Velocity.X, Sim.Velocity.Z).magnitude > 0.05f) || !Settled)) yield return null;
            Log("wall-run");
            bool jumped = false, kicked = false, dj = false;
            float until = Time.time + 6f;
            float k = 45f * Mathf.Deg2Rad;
            while (Time.time < until)
            {
                var sim = Sim;
                if (sim == null) break;
                Vector3 feet = Feet();
                _sprint = true;
                _move = kicked ? new Vector2(Mathf.Sin(k), Mathf.Cos(k)) : new Vector2(-0.2f, 1f);
                if (!jumped && feet.z >= face - 6f) { _jump = true; jumped = true; }
                else if (jumped && !kicked && sim.Mode == MoveMode.Wallrun && feet.z >= face - 3f)
                {
                    _move = new Vector2(Mathf.Sin(k), Mathf.Cos(k));
                    _jump = true;
                    kicked = true;
                    Log("wall-kick");
                }
                else if (kicked && !dj && !sim.Grounded && sim.Mode == MoveMode.Air && sim.Velocity.Y <= 0f)
                {
                    _jump = true;
                    dj = true;
                    Log("wall-double-jump");
                }
                if (sim.Grounded && feet.y > 1f) break;
                if (jumped && sim.Grounded && feet.y < 0.1f && Time.time > until - 4.5f) break;
                yield return null;
            }
            _move = Vector2.zero;
            _sprint = false;
            bool on = Feet().y > wr.max.Y - 0.3f;
            Log(on ? "on-lockers" : "lockers-missed");
            if (on)
            {
                yield return MoveTo(new Vector3((wr.min.X + wr.max.X) * 0.5f, Feet().y, (wr.min.Z + wr.max.Z) * 0.5f), false, 0.4f, 2f);
                yield return Wait(0.4f);
                Log("drop-off-lockers");
                yield return MoveTo(new Vector3(wr.max.X + 1.5f, Feet().y, (wr.min.Z + wr.max.Z) * 0.5f), false, CoarseArrive, 3f);
            }
            _aimFixed = false;
            _calm = false;
        }

        /// <summary>The far goal: once the shutter is up, run at the lift lobby, slide under the shutter (crouch pressed 3.5 m
        /// short of it, held) and stop inside, looking at the lift's doors.</summary>
        IEnumerator ToTheLift()
        {
            float wait = Time.time + 6f;
            while (!director.LiftOpen && Time.time < wait) yield return null;
            if (!director.LiftOpen) { Log("lift-shut"); yield break; }
            var l = GalleriaArena.LiftLobby;
            float cx = (l.min.X + l.max.X) * 0.5f;
            Vector3 start = new Vector3(cx, 0f, l.min.Z - 9f), doors = new Vector3(cx, 1.6f, l.max.Z);
            Log("to-lift");
            _calm = true;
            yield return MoveTo(start, true, CoarseArrive, 12f, () => doors);
            _aimFixed = false;
            _aimPoint = doors;
            float settle = Time.time + 1.2f;
            while (Time.time < settle && Mathf.Abs(Mathf.DeltaAngle(player.Yaw, 0f)) > 4f) yield return null;
            Log("slide-under-shutter");
            float until = Time.time + 5f;
            bool pressed = false;
            while (Time.time < until && !director.ExitReached)
            {
                Vector3 feet = Feet();
                ApplyMove(new Vector3(cx - feet.x, 0f, 6f).normalized, true);
                if (feet.z > l.min.Z - 3.5f)
                {
                    if (!pressed) { _crouchPress = true; pressed = true; }
                    _crouchUntil = Time.time + 0.2f;
                }
                yield return null;
            }
            _move = Vector2.zero;
            _sprint = false;
            Log(director.ExitReached ? "lift" : "lift-missed");
            yield return Wait(1f);
            // the ending: turn back to the cleared hall (the shutter rolls right up once the Unit is in) and take it in
            Log("vista");
            _aimFixed = true;
            _calm = true;
            _aimYaw = 180f;
            _aimPitch = -6f;
            yield return Wait(3f);
            _aimYaw = 160f;
            yield return Wait(3f);
            _aimYaw = 200f;
            _aimPitch = -14f;
            yield return Wait(3f);
            StartCoroutine(Linger());
        }

        /// <summary>After the run: a slow look across the hall, back and forth, until the recording ends.</summary>
        IEnumerator Linger()
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                _aimFixed = true;
                _calm = true;
                _aimYaw = 180f + 28f * Mathf.Sin(t * 0.35f);
                _aimPitch = -8f - 6f * Mathf.Sin(t * 0.21f);
                yield return null;
            }
        }

        IEnumerator UntilAirborne(float max)
        {
            float until = Time.time + max;
            while (Grounded && Time.time < until) yield return null;
        }

        IEnumerator UntilRising(float max)
        {
            float until = Time.time + max;
            while (VelY <= 0f && Time.time < until) yield return null;
        }

        IEnumerator UntilApex(float max)
        {
            float until = Time.time + max;
            while (!Grounded && VelY > 0f && Time.time < until) yield return null;
        }

        // ------------------------------------------------------------------ the fights

        IEnumerator Fight(int i)
        {
            string id = director.IdOf(i);
            Log("fight " + id);
            // each fight starts in the lane round its own anchor along the route and opens by getting into its own pocket
            Vector3 hide = HideOf(id);
            Vector3 entry = i == 0 ? hide : SkylineArenaDirector.U(director.AnchorOf(i));
            while (director.PhaseOf(i) == SkylineArenaDirector.Phase.Waiting) yield return null;
            _fighting = true;
            if (director.PhaseOf(i) == SkylineArenaDirector.Phase.Armed) Log("walk-to-entry " + id);
            // into the zone, however long it takes (a stuck walk side-steps); anything in the line of fire on the way is shot
            while (director.PhaseOf(i) == SkylineArenaDirector.Phase.Armed)
            {
                Steer(i, entry, true);
                yield return null;
            }
            string mode = "";
            float start = Time.time, lastSeen = start, lastDamage = start, nextFlip = 0f, strafe = 1f, advanceRepick = 0f;
            float health = Health(i), watchStart = 0f, watchRepick = 0f, limitFrom = start, survivorsLogAt = 0f;
            bool advancing = false, watchdog = false, reachedHide = false;
            Vector3 watchPos = Feet(), advancePos = Feet(), lastAim = Vector3.zero;
            SkylineBot lastTarget = null;
            _tac = Tactic.None;
            _tacRetryAt = 0f;
            _hasLastDuck = false;
            _nextHop = start + Range(3f, 6f);
            while (director.PhaseOf(i) == SkylineArenaDirector.Phase.Fighting)
            {
                float now = Time.time;
                Vector3 feet = Feet();
                float h = Health(i);
                if (h < health - 0.01f) lastDamage = now;
                health = h;

                // the watchdog: too long, or nothing damaged for a while
                if (!watchdog && (now - limitFrom > FightLimit || now - lastDamage > NoDamageLimit))
                {
                    watchdog = true;
                    watchStart = now;
                    watchRepick = 0f;
                    _tac = Tactic.None;
                    Log("watchdog " + id + (now - limitFrom > FightLimit ? " fight-time" : " no-damage"));
                    LogSurvivors(i, id, feet);
                    survivorsLogAt = now + SurvivorsEvery;
                }
                else if (watchdog && now >= survivorsLogAt)
                {
                    LogSurvivors(i, id, feet);
                    survivorsLogAt = now + SurvivorsEvery;
                }
                if (watchdog && lastDamage > watchStart)
                {
                    watchdog = false;
                    limitFrom = now - FightLimit + WatchRearm;   // the fight-time check comes back in 30 s
                    _tacRetryAt = now + 4f;                       // shoot from where the watchdog put it a little first
                    Log(id + " watchdog-cleared");
                }
                if (watchdog && now >= watchRepick)
                {
                    watchRepick = now + WatchRepick;
                    watchPos = FiringSpot(i, feet);
                }

                var target = PickTarget(i, out Vector3 aim);
                // watch each kind of bot once doing its thing before killing it (judge round 2: the bots never showed)
                if (_watch != null && (now > _watchUntil || !_watch.Alive || (_watchKind == "spotlight" && _watch.Beam != null && _watch.Beam.Locked && now > _watchUntil - WatchSpotlight + 0.8f)))
                    _watch = null;
                if (_watch == null && !watchdog) _watch = PickWatch(i, now);
                bool watching = _watch != null;
                if (watching) { target = _watch; aim = _watch.Center; }
                // the Breacher for bots within 5 m in the line of fire
                // ... only while it has shots: a special gun has no reserve until kills feed it, so an empty Breacher can't be
                // reloaded (the pilot sat on a dry Breacher pressing reload and hopping a floor-care's sweeps; the creator, 2026-10-14)
                bool close = target != null && Flat(feet, target.transform.position) <= BreacherRange && BreacherHasShots();
                if (close && !_breacher) { _breacher = true; Log(id + " breacher-on"); }
                if (close) _breacherOffAt = now + 1.5f;
                else if (_breacher && (now > _breacherOffAt || !BreacherHasShots())) { _breacher = false; Log(id + " breacher-off"); }
                if (target != null)
                {
                    if (target != lastTarget) _hand.React(Range(0.05f, 0.12f));   // a new target: a beat before the wrist moves
                    lastSeen = now; advancing = false; lastTarget = target; lastAim = aim;
                }
                // a one-frame loss of the line (a strafe past a cover edge, a shed plate flying through it) isn't a lost target
                bool grace = target == null && lastTarget != null && lastTarget.Alive && now - lastSeen < SightGrace;
                bool inSight = target != null || grace;
                var armor = player.Armor;
                bool hurt = armor.Plating < LowPlating;
                bool atHide = Flat(feet, hide) <= HideRadius;
                if (atHide) reachedHide = true;
                var gun = player.Loadout.Active;
                bool reloading = gun.State.IsReloading;
                int mag = gun.Def.weapon.magazineSize;
                var wreck = !watchdog && armor.Plating < armor.PlatingMax - StripRoom ? NearestFreshWreck(feet, inSight ? StripRange : FarStripRange) : null;
                string want;
                Vector3 goal = feet;
                bool sprintTo = false, strafeHere = false, tacticMove = false;
                if (watchdog) { want = "watchdog-move"; goal = watchPos; }
                // every fight opens by getting into cover (the zone starts the fight before the hide is reached)
                else if (!reachedHide && now - start < ToHideLimit) { want = "to-hide"; goal = hide; }
                else if (wreck != null) { want = "strip"; goal = wreck.transform.position; if (_tac == Tactic.Peek || _tac == Tactic.Duck) _tac = Tactic.None; }
                else
                {
                    if (_tac == Tactic.None && now >= _tacRetryAt) StartCover(i, feet, now);
                    if (_tac != Tactic.None)
                    {
                        want = CoverStep(i, feet, now, inSight, hurt, gun, reloading, mag, armor.Plating, out goal, out sprintTo, out strafeHere);
                        tacticMove = true;
                        advancing = false;
                    }
                    // the open game, when no cover point is to be had
                    else if (inSight && hurt && !atHide) { want = "fall-back"; goal = hide; }
                    else if (inSight) { want = "engage"; strafeHere = true; }
                    else if (advancing || now - lastSeen > (hurt && atHide ? HurtAdvanceAfter : AdvanceAfter))
                    {
                        want = "advance";
                        if (!advancing || now >= advanceRepick)
                        {
                            advancePos = FiringSpot(i, feet);
                            advanceRepick = now + AdvanceRepick;
                        }
                        advancing = true;
                        goal = advancePos;
                    }
                    else { want = "hold"; }
                }
                if (want != mode)
                {
                    mode = want;
                    Log(id + " " + mode);
                }

                // aim, fire and reload: the trigger is pulled while the crosshair is on the target (NextFrame checks the ray)
                if (player.Loadout.ActiveIndex != (_breacher ? 1 : 0)) _swap = true;
                _aimFixed = false;
                _calm = target == null && !grace;
                if (target != null)
                {
                    _aimPoint = aim;
                    if (gun.Ammo <= 0) { if (!reloading) _reload = true; }
                    else if (!reloading && !watching) _fireAt = target;
                }
                else
                {
                    _aimPoint = grace ? lastAim : LookWhileNoTarget(i, feet, goal);
                    bool inCover = _tac == Tactic.Duck;
                    if (!reloading && (gun.Ammo <= 0 || (inCover && gun.Ammo <= mag / 2) ||
                        (!grace && now - lastSeen > IdleReloadAfter && gun.Ammo <= mag / 2)))
                        _reload = true;
                }

                // move: toward the goal; strafing while something is in sight (peeking, or the open game)
                Vector3 dir = Vector3.zero;
                if (Flat(feet, goal) > (tacticMove ? 0.35f : CoarseArrive)) dir = PathDirection(feet, goal);
                if (now >= nextFlip)
                {
                    strafe = -strafe;
                    nextFlip = now + Range(StrafeMin, StrafeMax);
                }
                if (strafeHere && inSight)
                {
                    Vector3 toT = lastAim - feet;
                    toT.y = 0f;
                    Vector3 side = toT.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, toT.normalized) : Vector3.right;
                    if (_tac == Tactic.Peek)
                    {
                        // strafe across the peek spot, held within a metre of it
                        Vector3 back = _peek - feet;
                        back.y = 0f;
                        dir = side * strafe * 0.85f + back * 0.7f;
                        if (dir.sqrMagnitude > 1f) dir.Normalize();
                    }
                    else dir = (dir * 0.8f + side * strafe).normalized;
                    // a hop now and then while shooting
                    if (now >= _nextHop && Grounded && !_djArmed && _acidJumpAt < 0f)
                    {
                        _jump = true;
                        _nextHop = now + Range(4f, 7f);
                    }
                }
                bool sprint = sprintTo || mode == "advance" || mode == "fall-back" || mode == "watchdog-move" || mode == "strip" || mode == "to-hide";
                ApplyMove(dir, sprint);
                yield return null;
            }
            _fighting = false;
            _calm = true;
            _watch = null;
            if (_breacher) { _breacher = false; Log(id + " breacher-off"); }
            _tac = Tactic.None;
            _move = Vector2.zero;
            _aimPoint = null;
            Log(id + " over (" + director.PhaseOf(i) + ")");
            if (director.PhaseOf(i) == SkylineArenaDirector.Phase.Cleared) yield return Salvage();
        }

        // ------------------------------------------------------------------ the cover cycle

        /// <summary>Picks a cover point and sets off for it (or ducks at once if it is already there); with none to be had, the
        /// open game runs for 2 s before it looks again.</summary>
        void StartCover(int i, Vector3 feet, float now)
        {
            if (!FindCover(i, feet, out _duck))
            {
                _tac = Tactic.None;
                _tacRetryAt = now + 2f;
                return;
            }
            _peeksLeft = 2 + (_rng.NextDouble() < 0.5 ? 1 : 0);
            _slideTried = false;
            float d = Flat(feet, _duck);
            if (d > 0.8f)
            {
                _tac = Tactic.Move;
                _moveGiveUp = now + d / 4f + 3f;
            }
            else
            {
                _tac = Tactic.Duck;
                _tacUntil = now + Range(DuckMin, DuckMax);
            }
            _nextDuckCheck = now + 0.5f;
        }

        /// <summary>One frame of the cover cycle: where to go, and the mode's name for the log.</summary>
        string CoverStep(int i, Vector3 feet, float now, bool inSight, bool hurt, SkylineGun gun, bool reloading, int mag, float plating,
            out Vector3 goal, out bool sprint, out bool strafeHere)
        {
            goal = feet;
            sprint = false;
            strafeHere = false;
            switch (_tac)
            {
                case Tactic.Move:
                {
                    goal = _duck;
                    float d = Flat(feet, _duck);
                    sprint = d > 2.5f;
                    var sim = Sim;
                    // now and then a slide into cover (from a sprint) or a hop on the way
                    if (!_slideTried && d > 5f && sim != null && sim.Grounded && sim.HorizontalSpeed >= 6.6f && !_djArmed && _acidJumpAt < 0f)
                    {
                        _slideTried = true;
                        double roll = _rng.NextDouble();
                        if (roll < 0.55) { _crouchPress = true; _crouchUntil = now + 0.5f; }
                        else if (roll < 0.85) _jump = true;
                    }
                    if (d <= 0.7f)
                    {
                        _tac = Tactic.Duck;
                        _tacUntil = now + Range(DuckMin, DuckMax);
                    }
                    else if (now > _moveGiveUp) { _tac = Tactic.None; _tacRetryAt = now + 1f; }
                    return "move-cover";
                }
                case Tactic.Duck:
                {
                    goal = _duck;
                    if (now >= _nextDuckCheck)
                    {
                        _nextDuckCheck = now + 0.5f;
                        // a drone's patrol (or a search) found the cover point: go elsewhere
                        if (!Covered(i, _duck, 0f)) { Leave(now); return "duck"; }
                    }
                    if (!_hasNextPeek && now >= _nextPeekLook)
                    {
                        _nextPeekLook = now + 0.5f;
                        _hasNextPeek = FindPeek(i, _duck, out _nextPeek);
                    }
                    if (now >= _tacUntil && !reloading && gun.Ammo > mag / 2)
                    {
                        _hasNextPeek = false;
                        if (_peeksLeft <= 0 || !FindPeek(i, _duck, out _peek)) { Leave(now); return "duck"; }
                        _tac = Tactic.Peek;
                        float len = Range(PeekMin, PeekMax) * (hurt ? 0.6f : 1f);
                        _tacUntil = now + len;
                        _platingAtPeek = plating;
                        _peekNoSight = 0f;
                        _peeksLeft--;
                    }
                    return "duck";
                }
                default:   // Peek
                {
                    goal = _peek;
                    strafeHere = true;
                    bool there = Flat(feet, _peek) < 1.2f;
                    if (there && !inSight) _peekNoSight += Time.deltaTime;
                    else if (inSight) _peekNoSight = 0f;
                    if (now >= _tacUntil || gun.Ammo <= 0 || _platingAtPeek - plating >= PeekHurt || _peekNoSight > 1f)
                    {
                        _nextPeek = _peek;   // back in cover, it watches the corner it just peeked from
                        _hasNextPeek = true;
                        _tac = Tactic.Duck;
                        _tacUntil = now + Range(DuckMin, DuckMax) + (hurt ? 0.6f : 0f);
                        _nextDuckCheck = now + 0.3f;
                    }
                    return "engage";
                }
            }
        }

        /// <summary>Done with this cover point: the next one is looked for at once, elsewhere.</summary>
        void Leave(float now)
        {
            _hasNextPeek = false;
            _lastDuck = _duck;
            _hasLastDuck = true;
            _tac = Tactic.None;
            _tacRetryAt = now;
        }

        /// <summary>Is a drone or a spotlight of fight <paramref name="i"/> able to see a player standing at <paramref name="p"/>
        /// (its eye), from where it is or from <paramref name="slack"/> m either way of it (a drone's patrol)?</summary>
        bool Covered(int i, Vector3 p, float slack)
        {
            Vector3 eye = new Vector3(p.x, Feet().y + 1.6f, p.z);
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive || b.Def == null || !Shooter(b.Def)) continue;
                Vector3 c = b.Center;
                if (Sees(b, c, eye)) return false;
                if (!(slack > 0f) || b.Def.patrolLeash <= 0f) continue;
                if (Sees(b, c + new Vector3(slack, 0f, 0f), eye) || Sees(b, c - new Vector3(slack, 0f, 0f), eye) ||
                    Sees(b, c + new Vector3(0f, 0f, slack), eye) || Sees(b, c - new Vector3(0f, 0f, slack), eye)) return false;
            }
            return true;
        }

        static bool Shooter(EnemyDef d) => d.aim == AimModel.LeadsYou || d.aim == AimModel.FiresAtWhereYouAre;

        static bool Sees(SkylineBot b, Vector3 from, Vector3 eye)
        {
            Vector3 d = eye - from;
            float len = d.magnitude;
            if (len > b.Def.range || len < 1e-3f) return len < 1e-3f;
            return !SkylineBot.CastClear(from, d / len, len, b.transform, out _);
        }

        /// <summary>The cover point to use: a spot 1 m off a piece of cover (or the booth's U), inside the hall and within 16 m,
        /// that no drone or spotlight sees (with 1.5 m of patrol slack) and that has a peek spot with a line of fire; nearest
        /// first, leaning toward the bots, and not the one it just left.</summary>
        bool FindCover(int i, Vector3 feet, out Vector3 best)
        {
            best = feet;
            Vector3 nearest = feet;
            float nd = float.PositiveInfinity;
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive) continue;
                float d = Flat(feet, b.transform.position);
                if (d < nd) { nd = d; nearest = b.transform.position; }
            }
            if (float.IsPositiveInfinity(nd)) return false;
            _candidates.Clear();
            _scores.Clear();
            AddCandidate(BoothHide, feet, nearest);
            for (int c = 0; c < _cover.Count; c++)
            {
                var r = _cover[c];
                float x0 = r.xMin - CoverOffset, x1 = r.xMax + CoverOffset, z0 = r.yMin - CoverOffset, z1 = r.yMax + CoverOffset;
                float w = x1 - x0, l = z1 - z0;
                int nx = Mathf.Max(1, Mathf.RoundToInt(w / 1.5f)), nz = Mathf.Max(1, Mathf.RoundToInt(l / 1.5f));
                for (int s = 0; s <= nx; s++)
                {
                    float x = x0 + w * s / nx;
                    AddCandidate(new Vector3(x, 0f, z0), feet, nearest);
                    AddCandidate(new Vector3(x, 0f, z1), feet, nearest);
                }
                for (int s = 1; s < nz; s++)
                {
                    float z = z0 + l * s / nz;
                    AddCandidate(new Vector3(x0, 0f, z), feet, nearest);
                    AddCandidate(new Vector3(x1, 0f, z), feet, nearest);
                }
            }
            // nearest-scored first; only the first few that pass the cheap tests get the ray tests
            int tested = 0;
            while (_candidates.Count > 0 && tested < 12)
            {
                int bi = 0;
                for (int k = 1; k < _scores.Count; k++) if (_scores[k] < _scores[bi]) bi = k;
                Vector3 p = _candidates[bi];
                _candidates.RemoveAt(bi);
                _scores.RemoveAt(bi);
                tested++;
                if (!Covered(i, p, PatrolSlack) || !FindPeek(i, p, out _)) continue;
                best = p;
                return true;
            }
            return false;
        }

        void AddCandidate(Vector3 p, Vector3 feet, Vector3 nearestBot)
        {
            if (p.x < GalleriaArena.HallMinX + 1.2f || p.x > GalleriaArena.HallMaxX - 1.2f || p.z < GalleriaArena.HallMinZ + 1.2f || p.z > GalleriaArena.HallMaxZ - 1.2f) return;
            if (InCover(p)) return;
            float d = Flat(feet, p);
            if (d > CoverSearch) return;
            if (_hasLastDuck && Flat(p, _lastDuck) < 2.5f) return;
            _candidates.Add(p);
            _scores.Add(d + 0.5f * Flat(p, nearestBot));
        }

        /// <summary>A spot 1.4-2.4 m from cover point <paramref name="duck"/>, walkable straight from it, from which a live bot of
        /// fight <paramref name="i"/> has a line of fire; the one the fewest drones and spotlights see.</summary>
        bool FindPeek(int i, Vector3 duck, out Vector3 peek)
        {
            peek = duck;
            int bestSeen = int.MaxValue;
            float bestD = float.PositiveInfinity;
            var bots = director.BotsOf(i);
            float eyeY = Feet().y + 1.6f;
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI / 4f;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                for (int s = 0; s < 2; s++)
                {
                    float dist = s == 0 ? 1.4f : 2.4f;
                    Vector3 q = duck + dir * dist;
                    if (q.x < GalleriaArena.HallMinX + 1f || q.x > GalleriaArena.HallMaxX - 1f || q.z < GalleriaArena.HallMinZ + 1f || q.z > GalleriaArena.HallMaxZ - 1f) continue;
                    if (InCover(q) || !_path.Clear(new System.Numerics.Vector2(duck.x, duck.z), new System.Numerics.Vector2(q.x, q.z))) continue;
                    Vector3 eye = new Vector3(q.x, eyeY, q.z);
                    bool shot = false;
                    int seen = 0;
                    for (int k = 0; k < bots.Count; k++)
                    {
                        var b = bots[k];
                        if (b == null || !b.Alive || b.Def == null) continue;
                        if (Vector3.Distance(eye, b.Center) > MaxSight) continue;
                        if (!shot && Reaches(eye, b.Center, b, out _)) shot = true;   // one ray: the bot's centre is in the line of fire
                        if (Shooter(b.Def) && Sees(b, b.Center, eye)) seen++;
                    }
                    if (!shot) continue;
                    if (seen < bestSeen || (seen == bestSeen && dist < bestD))
                    {
                        bestSeen = seen;
                        bestD = dist;
                        peek = q;
                    }
                }
            }
            return bestSeen < int.MaxValue;
        }

        /// <summary>Where the eyes go with nothing to shoot: along the way while running, else toward the nearest live bot (a
        /// player pre-aims where the enemy will show).</summary>
        Vector3? LookWhileNoTarget(int i, Vector3 feet, Vector3 goal)
        {
            Vector3 eye = player.EyePosition;
            SkylineBot threat = NearestShooterOrBot(i, eye);
            if (Flat(feet, goal) > 2f && (_tac == Tactic.Move || _tac == Tactic.None))
            {
                Vector3 d = PathDirection(feet, goal);
                // running: the way ahead, unless that turns its back on the fight (then it runs looking at the threat)
                bool backToFight = threat != null && Vector3.Angle(new Vector3(d.x, 0f, d.z), new Vector3(threat.Center.x - eye.x, 0f, threat.Center.z - eye.z)) > 100f;
                if (d.sqrMagnitude > 1e-4f && !backToFight) return eye + d * 10f;
            }
            if (threat != null) return LookToward(eye, threat.Center);
            SkylineBot nearest = null;
            float nd = float.PositiveInfinity;
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive) continue;
                float d = Vector3.Distance(eye, b.Center);
                if (d < nd) { nd = d; nearest = b; }
            }
            // in cover: the corner it will peek from, past the cover's edge, at the threat's height
            if (_tac == Tactic.Duck && _hasNextPeek)
            {
                Vector3 out_ = new Vector3(_nextPeek.x - feet.x, 0f, _nextPeek.z - feet.z);
                if (out_.sqrMagnitude > 0.04f)
                {
                    Vector3 past = _nextPeek + out_.normalized * 6f;
                    float y = nearest != null ? Mathf.Lerp(eye.y, nearest.Center.y, 0.5f) : eye.y;
                    if (nearest != null)
                    {
                        // lean the look from the corner toward the threat beyond it
                        Vector3 toBot = new Vector3(nearest.Center.x - _nextPeek.x, 0f, nearest.Center.z - _nextPeek.z);
                        if (toBot.sqrMagnitude > 1e-4f) past = _nextPeek + (out_.normalized + toBot.normalized).normalized * 6f;
                    }
                    return OpenLook(eye, new Vector3(past.x, y, past.z));
                }
            }
            return nearest != null ? OpenLook(eye, nearest.Center) : (Vector3?)null;
        }

        /// <summary>A bot of a kind not yet watched, in the line of fire and doing its thing: a drone awake within 25 m (its
        /// strafe), the gondola awake on its rail (its mines), a spotlight whose beam is up (watched until it locks), a floor-care
        /// unit telegraphing or sweeping within 10 m. Starts the watch and logs it.</summary>
        SkylineBot PickWatch(int i, float now)
        {
            Vector3 eye = player.EyePosition, feet = Feet();
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive || b.Def == null || b.practice) continue;
                string kind = b.Def.id;
                if (_watched.Contains(kind)) continue;
                float d = Flat(feet, b.transform.position), len;
                bool doing;
                if (kind == SkylineEnemies.DroneId) { doing = b.IsAwake && d <= 25f; len = WatchDrone; }
                else if (kind == SkylineEnemies.GondolaId) { doing = b.IsAwake; len = WatchGondola; }
                else if (kind == SkylineEnemies.SpotlightId) { doing = b.Beam != null && b.Beam.Active && !b.Beam.Locked; len = WatchSpotlight; }
                else { doing = d <= 10f && b.Brain != null && (b.Brain.State == BotState.Telegraph || b.Brain.State == BotState.Recover); len = WatchSweep; }
                if (!doing || !LineOfFire(b, eye, out _)) continue;
                _watched.Add(kind);
                _watchKind = kind;
                _watchUntil = now + len;
                Log("watch " + kind);
                return b;
            }
            return null;
        }

        /// <summary>Looks toward <paramref name="threat"/> along the nearest bearing (up to 100 degrees either way, in 15 degree
        /// steps) whose line is open for 3 m: facing the fight, never a wall at a metre, never the way it came.</summary>
        static Vector3 LookToward(Vector3 eye, Vector3 threat)
        {
            Vector3 d = threat - eye;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude < 1e-4f) return threat;
            float pitchY = Mathf.Clamp(d.y / flat.magnitude, -0.4f, 0.6f);
            flat.Normalize();
            for (int step = 0; step <= 7; step++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    if (step == 0 && sign > 0) continue;
                    Vector3 r = Quaternion.AngleAxis(sign * step * 15f, Vector3.up) * flat;
                    Vector3 dir = (r + Vector3.up * pitchY).normalized;
                    if (Open(eye, dir, 3f)) return eye + dir * 10f;
                }
            return threat;
        }

        /// <summary>The nearest live drone or spotlight of fight <paramref name="i"/>, else the nearest live bot.</summary>
        SkylineBot NearestShooterOrBot(int i, Vector3 eye)
        {
            SkylineBot best = null, any = null;
            float bd = float.PositiveInfinity, ad = float.PositiveInfinity;
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive || b.Def == null) continue;
                float d = Vector3.Distance(eye, b.Center);
                if (d < ad) { ad = d; any = b; }
                if (Shooter(b.Def) && d < bd) { bd = d; best = b; }
            }
            return best ?? any;
        }

        /// <summary>A point to look at toward <paramref name="want"/>: itself if the line there is open for 6 m, else the
        /// nearest bearing (turning either way in 20 degree steps) whose line is open for 6 m: never a wall at a metre.</summary>
        static Vector3 OpenLook(Vector3 eye, Vector3 want)
        {
            Vector3 d = want - eye;
            float len = d.magnitude;
            if (len < 1e-3f) return want;
            Vector3 dir = d / len;
            if (Open(eye, dir, Mathf.Min(6f, len))) return want;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-4f) return want;
            flat.Normalize();
            for (int step = 1; step <= 8; step++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 r = Quaternion.AngleAxis(sign * step * 20f, Vector3.up) * flat;
                    Vector3 tilt = (r + Vector3.up * dir.y).normalized;
                    if (Open(eye, tilt, 6f)) return eye + tilt * 10f;
                }
            return want;
        }

        static bool Open(Vector3 eye, Vector3 dir, float dist) =>
            !Physics.Raycast(eye, dir, dist, SkylineShooting.ShotMask, QueryTriggerInteraction.Ignore);

        /// <summary>After a fight: strip the fresh wrecks it can reach before they go stale while the plating has room, then
        /// top up the magazine.</summary>
        IEnumerator Salvage()
        {
            float until = Time.time + SalvageLimit;
            bool logged = false;
            while (Time.time < until)
            {
                var armor = player.Armor;
                if (armor.Plating >= armor.PlatingMax - StripRoom) break;
                Vector3 feet = Feet();
                var w = NearestFreshWreck(feet, 30f);
                if (w == null) break;
                if (!logged) { Log("salvage"); logged = true; }
                _aimPoint = null;
                Vector3 dir = PathDirection(feet, w.transform.position);
                if (dir.sqrMagnitude > 1e-4f) _aimPoint = player.EyePosition + dir * 10f;
                ApplyMove(dir, true);
                yield return null;
            }
            _move = Vector2.zero;
            _sprint = false;
            var gun = player.Loadout.Active;
            if (gun.Ammo < gun.Def.weapon.magazineSize && !gun.State.IsReloading)
            {
                _reload = true;
                yield return null;
            }
        }

        /// <summary>How often (s) the survivors are logged again while the watchdog is on.</summary>
        const float SurvivorsEvery = 15f;

        /// <summary>Fix "stuck" (2026-10-14): names every live bot of encounter <paramref name="i"/> when the watchdog fires (and
        /// every <see cref="SurvivorsEvery"/> s while it stays on), so a stalled fight is diagnosable from the log alone:
        /// <c>SKYLINE_DEMO survivors &lt;id&gt; n=&lt;count&gt; demo (x, y, z) | &lt;bot&gt; (x, y, z) awake|asleep
        /// line|no-line &lt;metres&gt; m chassis &lt;hp&gt; ...</c> (line: a shot from the demo's eye would land on it now).</summary>
        void LogSurvivors(int i, string id, Vector3 feet)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string P(Vector3 v) => "(" + v.x.ToString("F1", inv) + ", " + v.y.ToString("F1", inv) + ", " + v.z.ToString("F1", inv) + ")";
            var sb = new System.Text.StringBuilder(256);
            Vector3 eye = player.EyePosition;
            var bots = director.BotsOf(i);
            int n = 0;
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive) continue;
                n++;
                bool line = LineOfFire(b, eye, out _);
                sb.Append(" | ").Append(b.Id).Append(' ').Append(P(b.Center))
                  .Append(b.IsAwake ? " awake" : " asleep").Append(line ? " line " : " no-line ")
                  .Append(Vector3.Distance(eye, b.Center).ToString("F1", inv)).Append(" m chassis ")
                  .Append(b.Body.Chassis.ToString("F0", inv));
            }
            Log("survivors " + id + " n=" + n + " demo " + P(feet) + sb);
        }

        /// <summary>What is left of encounter <paramref name="i"/>'s live bots (chassis and plates): it drops when any is damaged
        /// or wrecked.</summary>
        float Health(int i)
        {
            float sum = 0f;
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive) continue;
                sum += b.Body.Chassis;
                for (int p = 0; p < b.Body.PlateCount; p++) sum += b.Body.PlateHp(p);
            }
            return sum;
        }

        /// <summary>The floor point nearest <paramref name="feet"/> (on rings 4-18 m round the nearest live enemy of encounter
        /// <paramref name="i"/>, spotlights and gondolas included; in the hall, clear of cover) whose eye has a clear line to
        /// the enemy; the enemy's own spot if none does.</summary>
        Vector3 FiringSpot(int i, Vector3 feet)
        {
            SkylineBot nearest = null;
            float nd = float.PositiveInfinity;
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive) continue;
                float d = Flat(feet, b.transform.position);
                if (d < nd) { nd = d; nearest = b; }
            }
            if (nearest == null) return feet;
            Vector3 c = nearest.Center, aim = AimPoint(nearest);
            Vector3 best = new Vector3(c.x, 0f, c.z);
            float bestD = float.PositiveInfinity;
            for (float r = 4f; r <= 18f; r += 2f)
                for (int a = 0; a < 24; a++)
                {
                    float ang = a * Mathf.PI * 2f / 24f;
                    var p = new Vector3(c.x + Mathf.Sin(ang) * r, 0f, c.z + Mathf.Cos(ang) * r);
                    if (p.x < GalleriaArena.HallMinX + 1f || p.x > GalleriaArena.HallMaxX - 1f || p.z < GalleriaArena.HallMinZ + 1f || p.z > GalleriaArena.HallMaxZ - 1f) continue;
                    float d = Flat(feet, p);
                    if (d >= bestD || InCover(p)) continue;
                    Vector3 eye = p + Vector3.up * 1.6f;
                    Vector3 to = aim - eye;
                    float len = to.magnitude;
                    if (len < 1e-3f || SkylineBot.CastClear(eye, to / len, len - 0.05f, nearest.transform, out _)) continue;
                    bestD = d;
                    best = p;
                }
            return best;
        }

        static bool InCover(Vector3 p)
        {
            foreach (var r in _cover)
                if (p.x > r.xMin - 0.6f && p.x < r.xMax + 0.6f && p.z > r.yMin - 0.6f && p.z < r.yMax + 0.6f) return true;
            return false;
        }

        /// <summary>The live bot of encounter <paramref name="i"/> to shoot, and where: only a bot a shot would really reach
        /// (<see cref="LineOfFire"/>); spotlights first, then a floor-care unit within 7 m, then the nearest.</summary>
        SkylineBot PickTarget(int i, out Vector3 aim)
        {
            aim = Vector3.zero;
            Vector3 eye = player.EyePosition, feet = Feet();
            SkylineBot best = null;
            float bestScore = float.PositiveInfinity;
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive || b.Def == null) continue;
                float dist = Vector3.Distance(eye, b.Center);
                if (dist > MaxSight) continue;
                float score = dist;
                if (b.Def.id == SkylineEnemies.SpotlightId) score -= 100f;
                else if (b.Def.moveSpeed > 0f && Flat(feet, b.transform.position) < 7f) score -= 60f;
                else if (b.Def.layer == ThreatLayer.Walls) score -= 8f;   // the gondola on its rail: on screen, not left for last
                if (score >= bestScore) continue;
                if (!LineOfFire(b, eye, out Vector3 p)) continue;
                best = b;
                bestScore = score;
                aim = p;
            }
            return best;
        }

        /// <summary>Where a shot from <paramref name="eye"/> lands on <paramref name="b"/> first: its newest spot or its weak
        /// point (the weak-point zone itself), else its centre, else any part of its body, a plate last.</summary>
        static bool LineOfFire(SkylineBot b, Vector3 eye, out Vector3 aim)
        {
            aim = Vector3.zero;
            Vector3 plate = Vector3.zero;
            bool hasPlate = false;
            HitZone zone;
            if (b.Spots != null && b.Spots.Count > 0)
            {
                Vector3 s = b.SpotWorld(b.Spots.All[b.Spots.Count - 1]);
                if (Reaches(eye, s, b, out zone) && zone == HitZone.WeakPoint) { aim = s; return true; }
            }
            var wc = b.Parts != null && b.Parts.weakPoint != null ? b.WeakPointCollider : null;
            if (wc != null && wc.enabled && wc.gameObject.activeInHierarchy)
            {
                Vector3 w = wc.bounds.center;
                if (Reaches(eye, w, b, out zone) && zone == HitZone.WeakPoint) { aim = w; return true; }
            }
            Vector3 c = b.Center;
            if (Reaches(eye, c, b, out zone))
            {
                if (zone != HitZone.Plate) { aim = c; return true; }
                plate = c;
                hasPlate = true;
            }
            var parts = b.Parts != null ? b.Parts.bodyRenderers : null;
            if (parts != null)
                for (int k = 0; k < parts.Count && k < 8; k++)
                {
                    var r = parts[k];
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    Vector3 p = r.bounds.center;
                    if (!Reaches(eye, p, b, out zone)) continue;
                    if (zone != HitZone.Plate) { aim = p; return true; }
                    if (!hasPlate) { plate = p; hasPlate = true; }
                }
            if (hasPlate) { aim = plate; return true; }
            return false;
        }

        /// <summary>A ray from <paramref name="eye"/> at <paramref name="point"/> with the guns' own mask lands on one of
        /// <paramref name="b"/>'s hit zones first (cover, shed parts and other bots block it); <paramref name="zone"/> is the
        /// zone it lands on.</summary>
        static bool Reaches(Vector3 eye, Vector3 point, SkylineBot b, out HitZone zone)
        {
            zone = HitZone.Body;
            Vector3 d = point - eye;
            float len = d.magnitude;
            if (!(len > 1e-3f)) return false;
            if (!Physics.Raycast(eye, d / len, out var hit, len + 0.75f, SkylineShooting.ShotMask, QueryTriggerInteraction.Ignore)) return false;
            if (!hit.collider.TryGetComponent<SkylineHitZone>(out var z) || z.owner != b) return false;
            zone = z.zone;
            return true;
        }

        static Vector3 AimPoint(SkylineBot b)
        {
            if (b.Spots != null && b.Spots.Count > 0) return b.SpotWorld(b.Spots.All[b.Spots.Count - 1]);
            return WeakPoint(b);
        }

        static Vector3 WeakPoint(SkylineBot b)
        {
            var c = b.Parts != null && b.Parts.weakPoint != null ? b.WeakPointCollider : null;
            return c != null && c.enabled && c.gameObject.activeInHierarchy ? c.bounds.center : b.Center;
        }

        /// <summary>The nearest fresh, unstripped wreck within <paramref name="range"/> m (flat, within 2 m of the feet's height)
        /// that a run reaches before it goes stale.</summary>
        SkylineWreck NearestFreshWreck(Vector3 feet, float range)
        {
            float now = Time.time;
            SkylineWreck best = null;
            float bestD = range;
            var all = SkylineWreck.All;
            for (int k = 0; k < all.Count; k++)
            {
                var w = all[k];
                if (w == null || w.Data == null || w.Data.stripped || !w.Data.Fresh(now)) continue;
                Vector3 p = w.transform.position;
                float d = Flat(feet, p);
                if (d >= bestD || Mathf.Abs(p.y - feet.y) >= 2f) continue;
                float left = w.Data.wreckedAt + w.Data.freshSeconds - now;
                if (Mathf.Max(0f, d - SkylineWreck.StripRadius) / RunSpeed > left) continue;   // stale before it gets there
                bestD = d;
                best = w;
            }
            return best;
        }

        // ------------------------------------------------------------------ walking

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Walks to <paramref name="goal"/> (round the cover), aiming at <paramref name="aimAt"/> if given, else along
        /// the way; stops within <paramref name="arrive"/> m or after <paramref name="max"/> s.</summary>
        IEnumerator MoveTo(Vector3 goal, bool sprint, float arrive, float max, System.Func<Vector3> aimAt = null)
        {
            float until = Time.time + max;
            while (Time.time < until)
            {
                Vector3 feet = Feet();
                float d = Flat(feet, goal);
                if (d <= arrive) break;
                if (aimAt != null) { _aimFixed = false; _aimPoint = aimAt(); }
                Vector3 dir = PathDirection(feet, goal);
                // slow down for a precise stop
                float speed = arrive < CoarseArrive ? Mathf.Clamp(d * 1.2f, 0.2f, 1f) : 1f;
                ApplyMove(dir * speed, sprint && d > 3f);
                if (aimAt == null && !_aimFixed && dir.sqrMagnitude > 1e-4f)
                {
                    _aimPoint = player.EyePosition + new Vector3(dir.x, 0f, dir.z).normalized * 10f;
                }
                yield return null;
            }
            _move = Vector2.zero;
            _sprint = false;
        }

        /// <summary>One frame of walking toward <paramref name="goal"/> (the approach to fight <paramref name="i"/>), shooting
        /// anything in the line of fire on the way.</summary>
        void Steer(int i, Vector3 goal, bool sprint)
        {
            Vector3 feet = Feet();
            var target = PickTarget(i, out Vector3 aim);
            var gun = player.Loadout.Active;
            if (player.Loadout.ActiveIndex != 0) _swap = true;
            Vector3 dir = Flat(feet, goal) > CoarseArrive ? PathDirection(feet, goal) : Vector3.zero;
            _aimFixed = false;
            _calm = target == null;
            if (target != null)
            {
                _aimPoint = aim;
                if (gun.Ammo <= 0) { if (!gun.State.IsReloading) _reload = true; }
                else if (!gun.State.IsReloading) _fireAt = target;
            }
            else _aimPoint = dir.sqrMagnitude > 1e-4f ? player.EyePosition + dir * 10f : (Vector3?)null;
            ApplyMove(dir, sprint);
        }

        /// <summary>World direction (flat, length up to 1) into the player's move axes, with a side-step when stuck.</summary>
        void ApplyMove(Vector3 world, bool sprint)
        {
            float now = Time.time;
            Vector3 feet = Feet();
            bool wants = world.sqrMagnitude > 0.01f;
            if (wants)
            {
                if (_stuckSince < 0f) { _stuckSince = now; _stuckFrom = feet; }
                else if (now - _stuckSince > StuckSeconds)
                {
                    if (Flat(feet, _stuckFrom) < 0.3f)
                    {
                        _sideStepUntil = now + 0.6f;
                        _sideSign = -_sideSign;
                    }
                    _stuckSince = now;
                    _stuckFrom = feet;
                }
            }
            else _stuckSince = -1f;
            if (now < _sideStepUntil && wants)
            {
                Vector3 side = Vector3.Cross(Vector3.up, world.normalized) * _sideSign;
                world = (world.normalized * 0.3f + side).normalized * world.magnitude;
            }
            float yaw = player.Yaw * Mathf.Deg2Rad;
            Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw)), fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var m = new Vector2(Vector3.Dot(world, right), Vector3.Dot(world, fwd));
            _move = m.sqrMagnitude > 1f ? m.normalized : m;
            _sprint = sprint;
        }

        /// <summary>The way to <paramref name="goal"/> round the cover boxes: straight if clear, else toward the next corner of
        /// the shortest way round (<see cref="CoverPath"/>; fix "stuck", 2026-10-14: the old greedy one-corner detour ignored a
        /// box whose margin the demo stood in and couldn't leave the booth's U, so passes 0700 and 0735 walked into the
        /// booth's north screen for the rest of the food court).</summary>
        static Vector3 PathDirection(Vector3 from, Vector3 goal)
        {
            if (_path == null) BuildCover();
            var next = _path.Next(new System.Numerics.Vector2(from.x, from.z), new System.Numerics.Vector2(goal.x, goal.z));
            Vector2 d = new Vector2(next.X - from.x, next.Y - from.z);
            return d.sqrMagnitude > 1e-6f ? new Vector3(d.x, 0f, d.y).normalized : Vector3.zero;
        }
    }
}
