using System.Collections;
using System.Collections.Generic;
using JamKit.Movement;
using JamKit.Skyline;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The arena's scripted run (docs/SKYLINE-PLAN-M1.md Task 16), only under <see cref="SkylineRunArgs.Demo"/>: it drives
    /// <see cref="SkylinePlayer.ScriptedInput"/> like a player's hands, with perfect aim (<see cref="AimAssist.Angles"/> from
    /// the eye), so the recording and the frame-time probe see the arena busy. Hit stop is off (its schedule runs on
    /// unscaled time). Each step logs <c>SKYLINE_DEMO &lt;step&gt;</c>.
    /// <list type="number">
    /// <item>The warm-up: walk to 10 m from the practice bot; three Writ shots at its weak point (a spot opens), one at the
    /// spot; swap to the Breacher; walk to 0.8 m short of the kiosk's south face, facing it; jump, double jump at the jump's
    /// apex, at the double jump's apex pitch the view down as far as it goes (85 degrees) and fire (the boost), holding
    /// forward throughout (the mantle); take the cache on top; swap back to the Writ; drop off the north side.</item>
    /// <item>Through the doorway into the hall, then each encounter in order (food-court, skylight, facade): walk into its
    /// zone (the food court's from the doorway; every later one's in the entry lane, back where the fight was balanced),
    /// then fight until it is cleared. The fight: shoot the nearest bot in sight (its spot, else its weak point,
    /// else its body) at the Writ's fire rate, reloading when dry or when nothing is in sight; while something is in sight,
    /// strafe and reverse every 0.4-0.7 s; with nothing in sight for 2.5 s, advance toward the nearest live bot; under 30
    /// plating with something in sight, fall back to the encounter's hide (inside the information booth's U for the food court and the facade,
    /// under the mezzanine deck for the skylight); strip a fresh wreck within 8 m when the plating has room; jump (and double
    /// jump) a floor-care unit's sweep when one telegraphs within reach. Walking goes round the hall's cover boxes
    /// (FightSim's detour to the best corner). Advancing is sticky until something is in sight (no thrash at the hide). A
    /// per-fight watchdog (<c>SKYLINE_DEMO watchdog &lt;id&gt; fight-time|no-damage</c>): a fight not cleared in 90 s, or no
    /// enemy damaged for 20 s, sends it to the nearest floor point with a clear line to the nearest live enemy (spotlights and
    /// gondolas included) until it damages something.</item>
    /// <item>If the Unit is wrecked the director restarts the arena at the warm-up; the run starts again from the start,
    /// skipping the lessons it has done (logs <c>SKYLINE_DEMO restart</c>).</item>
    /// </list>
    /// It doesn't need to win; whether it reaches the skylight or the facade inside the recording depends on the fight.
    /// </summary>
    public sealed class SkylineArenaDemo : MonoBehaviour
    {
        const float RecorderWait = 8f, PracticeDistance = 10f, KioskStandOff = 0.8f, ShotGap = 0.5f, SpotWait = 1.8f;
        const float ArriveRadius = 0.12f, CoarseArrive = 0.6f, CoverMargin = 0.75f, CornerMargin = 0.9f;
        const float LowPlating = 30f, AdvanceAfter = 2.5f, StripRange = 8f, MaxSight = 45f, StuckSeconds = 1f;
        /// <summary>The per-fight watchdog: a fight not cleared in this long, or no enemy damaged for this long, sends the
        /// run to a firing position with a clear line to the nearest live enemy.</summary>
        const float FightLimit = 90f, NoDamageLimit = 20f, WatchRepick = 3f, WatchRearm = 30f;

        public SkylinePlayer player;
        public SkylineArenaDirector director;
        public SkylineScrapCache cache;

        // held input
        Vector2 _move;
        bool _sprint;
        // one-frame presses
        bool _jump, _fire, _swap, _reload;
        // aim
        bool _aimFixed;
        float _aimYaw, _aimPitch;
        Vector3? _aimPoint;

        int _life;
        bool _lessonsDone, _fighting, _djArmed, _djTakeoff;
        float _acidJumpAt = -1f, _stuckSince = -1f, _sideStepUntil = -1f, _sideSign = 1f;
        Vector3 _stuckFrom;
        System.Random _rng = new System.Random(1616);
        SkylineWreck[] _wrecks = new SkylineWreck[0];
        float _wrecksAt;
        static readonly List<Rect> _cover = new List<Rect>();

        static readonly Vector3 BoothHide = new Vector3(0f, 0f, -12.2f);
        static readonly Vector3 DeckHide = new Vector3(0f, 0f, 15.5f);
        /// <summary>The middle of the entry lane, where every fight after the food court starts (GalleriaArena.EntryZone*).</summary>
        static readonly Vector3 EntryPoint = new Vector3(
            (GalleriaArena.EntryZoneMinX + GalleriaArena.EntryZoneMaxX) * 0.5f, 0f, (GalleriaArena.EntryZoneMinZ + GalleriaArena.EntryZoneMaxZ) * 0.5f);

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
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (director != null) director.Restarted -= OnRestarted;
            SkylineBot.StateEntered -= OnBotState;
            if (player != null && player.ScriptedInput == (System.Func<SkylineInputFrame>)NextFrame) player.ScriptedInput = null;
        }

        static void Log(string step) => Debug.Log("SKYLINE_DEMO " + step);

        static void BuildCover()
        {
            _cover.Clear();
            foreach (var b in GalleriaArena.HallCover()) _cover.Add(Rect.MinMaxRect(b.min.X, b.min.Z, b.max.X, b.max.Z));
            var k = GalleriaArena.BoostLedgeTop;
            float h = GalleriaArena.BoostLedgeSize * 0.5f;
            _cover.Add(Rect.MinMaxRect(k.X - h, k.Z - h, k.X + h, k.Z + h));
        }

        // ------------------------------------------------------------------ input

        SkylineInputFrame NextFrame()
        {
            var f = new SkylineInputFrame
            {
                move = _move,
                sprintHeld = _sprint,
                jumpPressed = _jump,
                jumpHeld = _jump,
                firePressed = _fire,
                fireHeld = _fire,
                swapPressed = _swap,
                reloadPressed = _reload,
            };
            _jump = _fire = _swap = _reload = false;
            if (_aimFixed) player.SetAim(_aimYaw, _aimPitch);
            else if (_aimPoint.HasValue)
            {
                AimAssist.Angles(N(player.EyePosition), N(_aimPoint.Value), out float yaw, out float pitch);
                player.SetAim(yaw, pitch);
            }
            return f;
        }

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
            if (flat > bot.Def.sweepRadius + 1.5f || _acidJumpAt >= 0f || _djArmed) return;
            _acidJumpAt = Time.time + Mathf.Max(0f, bot.Def.telegraphSeconds - 0.15f);
        }

        void OnRestarted()
        {
            StopAllCoroutines();
            _move = Vector2.zero;
            _sprint = false;
            _jump = _fire = _swap = _reload = false;
            _aimFixed = false;
            _aimPoint = null;
            _fighting = false;
            _djArmed = false;
            _acidJumpAt = -1f;
            _life++;
            StartCoroutine(Run());
        }

        // ------------------------------------------------------------------ the run

        static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }

        IEnumerator Run()
        {
            if (_life == 0)
            {
                if (SkylineRunArgs.Has("-skylineOut"))
                {
                    float giveUp = Time.unscaledTime + RecorderWait;
                    while (!(Time.captureDeltaTime > 0f) && Time.unscaledTime < giveUp) yield return null;
                }
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
            Log("done");
            _move = Vector2.zero;
            _sprint = false;
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
                for (int s = 0; s < 3; s++)
                {
                    _aimPoint = WeakPoint(bot);
                    yield return null;
                    Log("writ-shot " + (s + 1));
                    _fire = true;
                    yield return Wait(ShotGap);
                }
                float giveUp = Time.unscaledTime + SpotWait;
                while (bot.Spots.Count == 0 && Time.unscaledTime < giveUp) yield return null;
                if (bot.Spots.Count > 0)
                {
                    var sp = bot.Spots.All[bot.Spots.Count - 1];
                    _aimPoint = bot.SpotWorld(sp);
                    yield return null;
                    _aimPoint = bot.SpotWorld(sp);
                    Log("writ-spot-shot");
                    _fire = true;
                    yield return Wait(ShotGap);
                }
                else Log("writ-no-spot");
            }

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
            float settle = Time.unscaledTime + 1f;
            while (Time.unscaledTime < settle && Sim != null && new Vector2(Sim.Velocity.X, Sim.Velocity.Z).magnitude > 0.05f) yield return null;
            yield return Wait(0.2f);

            // jump, double jump at the apex, the boost at the double jump's apex; forward held throughout (the mantle)
            _move = new Vector2(0f, 1f);
            _sprint = true;
            Log("jump");
            _jump = true;
            yield return UntilAirborne(0.5f);
            yield return UntilApex(2f);
            Log("double-jump");
            _jump = true;
            yield return UntilRising(0.4f);
            yield return UntilApex(2f);
            _aimPitch = SkylinePlayer.MaxPitch;
            yield return null;                                     // the pitch is set on the frame before the shot
            Log("boost-fire");
            _fire = true;
            yield return null;
            Log("mantle");
            float top = k.y;
            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until && !(Grounded && Feet().y > top - 0.2f)) yield return null;
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

            Log("swap-writ");
            _swap = true;
            yield return null;
            Log("drop-off");
            yield return MoveTo(new Vector3(k.x, Feet().y, k.z + half + 1.5f), false, CoarseArrive, 3f);
            yield return Wait(0.5f);
            Log("run-to-doorway");
        }

        IEnumerator UntilAirborne(float max)
        {
            float until = Time.unscaledTime + max;
            while (Grounded && Time.unscaledTime < until) yield return null;
        }

        IEnumerator UntilRising(float max)
        {
            float until = Time.unscaledTime + max;
            while (VelY <= 0f && Time.unscaledTime < until) yield return null;
        }

        IEnumerator UntilApex(float max)
        {
            float until = Time.unscaledTime + max;
            while (!Grounded && VelY > 0f && Time.unscaledTime < until) yield return null;
        }

        // ------------------------------------------------------------------ the fights

        IEnumerator Fight(int i)
        {
            string id = director.IdOf(i);
            Log("fight " + id);
            // the food court starts on coming through the doorway; every later fight in the entry lane, where it was balanced
            Vector3 entry = i == 0 ? BoothHide : EntryPoint;
            Vector3 hide = i == 1 ? DeckHide : BoothHide;
            while (director.PhaseOf(i) == SkylineArenaDirector.Phase.Waiting) yield return null;
            _fighting = true;
            if (director.PhaseOf(i) == SkylineArenaDirector.Phase.Armed) Log("walk-to-entry " + id);
            // into the zone, however long it takes (a stuck walk side-steps); anything in sight on the way is shot
            while (director.PhaseOf(i) == SkylineArenaDirector.Phase.Armed)
            {
                Steer(i, entry, true);
                yield return null;
            }
            string mode = "";
            float start = Time.unscaledTime, lastSeen = start, lastDamage = start, nextShot = 0f, nextFlip = 0f, strafe = 1f;
            float health = Health(i), watchStart = 0f, watchRepick = 0f, limitFrom = start;
            bool advancing = false, watchdog = false;
            Vector3 watchPos = Feet();
            while (director.PhaseOf(i) == SkylineArenaDirector.Phase.Fighting)
            {
                float now = Time.unscaledTime;
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
                    Log("watchdog " + id + (now - limitFrom > FightLimit ? " fight-time" : " no-damage"));
                }
                if (watchdog && lastDamage > watchStart)
                {
                    watchdog = false;
                    limitFrom = now - FightLimit + WatchRearm;   // the fight-time check comes back in 30 s
                    Log(id + " watchdog-cleared");
                }
                if (watchdog && now >= watchRepick)
                {
                    watchRepick = now + WatchRepick;
                    watchPos = FiringSpot(i, feet);
                }

                var target = PickTarget(i, out Vector3 aim);
                var armor = player.Armor;
                var wreck = !watchdog && armor.Plating < armor.PlatingMax - 10f ? NearestFreshWreck(feet) : null;
                string want;
                Vector3 goal = feet;
                if (target != null) { lastSeen = now; advancing = false; }
                if (watchdog) { want = "watchdog-move"; goal = watchPos; }
                else if (wreck != null) { want = "strip"; goal = wreck.transform.position; }
                // fall back only while something is shooting at it: at the hide with nothing in sight it advances, and
                // stays advancing (hysteresis) until something is in sight again
                else if (target != null && armor.Plating < LowPlating && Flat(feet, hide) > 1f) { want = "fall-back"; goal = hide; }
                else if (target != null) { want = "engage"; }
                else if (advancing || now - lastSeen > AdvanceAfter) { want = "advance"; advancing = true; goal = NearestLiveBot(i, feet); }
                else { want = "hold"; }
                if (want != mode)
                {
                    mode = want;
                    Log(id + " " + mode);
                }

                // aim, fire and reload
                var gun = player.Loadout.Active;
                if (player.Loadout.ActiveIndex != 0) _swap = true;
                if (target != null)
                {
                    _aimFixed = false;
                    _aimPoint = aim;
                    if (now >= nextShot && !gun.State.IsReloading)
                    {
                        _fire = true;                              // dry: the press starts the from-empty reload
                        nextShot = now + gun.Def.weapon.fireInterval + 0.06f;
                    }
                }
                else
                {
                    _aimPoint = null;
                    if (gun.Ammo < gun.Def.weapon.magazineSize && !gun.State.IsReloading) _reload = true;
                }

                // move: toward the goal, strafing while something is in sight (not while stripping or on the watchdog's walk)
                Vector3 dir = Vector3.zero;
                if (Flat(feet, goal) > CoarseArrive) dir = PathDirection(feet, goal);
                if (target != null && mode != "strip" && !(watchdog && dir.sqrMagnitude > 0f))
                {
                    if (now >= nextFlip)
                    {
                        strafe = -strafe;
                        nextFlip = now + 0.4f + (float)_rng.NextDouble() * 0.3f;
                    }
                    Vector3 toT = aim - feet;
                    toT.y = 0f;
                    Vector3 side = toT.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, toT.normalized) : Vector3.right;
                    dir = (dir * 0.8f + side * strafe).normalized;
                }
                ApplyMove(dir, mode == "advance" || mode == "fall-back" || mode == "watchdog-move");
                yield return null;
            }
            _fighting = false;
            _move = Vector2.zero;
            _aimPoint = null;
            Log(id + " over (" + director.PhaseOf(i) + ")");
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

        /// <summary>The nearest live bot of encounter <paramref name="i"/> in sight, and where to shoot it: its newest spot,
        /// else its weak point, else its centre.</summary>
        SkylineBot PickTarget(int i, out Vector3 aim)
        {
            aim = Vector3.zero;
            Vector3 eye = player.EyePosition;
            SkylineBot best = null;
            float bestD = float.PositiveInfinity;
            var bots = director.BotsOf(i);
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive) continue;
                Vector3 p = AimPoint(b);
                Vector3 d = p - eye;
                float dist = d.magnitude;
                if (dist > MaxSight || dist >= bestD || dist < 1e-3f) continue;
                if (SkylineBot.CastClear(eye, d / dist, dist - 0.05f, b.transform, out _))
                {
                    // the aim point is hidden; the centre may not be
                    p = b.Center;
                    d = p - eye;
                    dist = d.magnitude;
                    if (dist < 1e-3f || SkylineBot.CastClear(eye, d / dist, dist - 0.05f, b.transform, out _)) continue;
                }
                best = b;
                bestD = dist;
                aim = p;
            }
            return best;
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

        Vector3 NearestLiveBot(int i, Vector3 feet)
        {
            var bots = director.BotsOf(i);
            Vector3 best = feet;
            float bestD = float.PositiveInfinity;
            for (int k = 0; k < bots.Count; k++)
            {
                var b = bots[k];
                if (b == null || !b.Alive) continue;
                Vector3 p = b.transform.position;
                float d = Flat(feet, p);
                if (d < bestD) { bestD = d; best = p; }
            }
            // stop short: close enough to see it round most cover
            if (bestD < float.PositiveInfinity && bestD > 6f) return Vector3.MoveTowards(new Vector3(feet.x, 0f, feet.z), new Vector3(best.x, 0f, best.z), bestD - 6f);
            return feet;
        }

        SkylineWreck NearestFreshWreck(Vector3 feet)
        {
            if (Time.unscaledTime >= _wrecksAt)
            {
                _wrecksAt = Time.unscaledTime + 0.25f;   // the wrecks change slowly; don't search the scene every frame
                _wrecks = FindObjectsByType<SkylineWreck>(FindObjectsSortMode.None);
            }
            SkylineWreck best = null;
            float bestD = StripRange;
            foreach (var w in _wrecks)
            {
                if (w == null || w.Data == null || w.Data.stripped || !w.Fresh) continue;
                float d = Flat(feet, w.transform.position);
                if (d < bestD && Mathf.Abs(w.transform.position.y - feet.y) < 2f) { bestD = d; best = w; }
            }
            return best;
        }

        // ------------------------------------------------------------------ walking

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Walks to <paramref name="goal"/> (round the cover), aiming at <paramref name="aimAt"/> if given, else along
        /// the way; stops within <paramref name="arrive"/> m or after <paramref name="max"/> s.</summary>
        IEnumerator MoveTo(Vector3 goal, bool sprint, float arrive, float max, System.Func<Vector3> aimAt = null)
        {
            float until = Time.unscaledTime + max;
            while (Time.unscaledTime < until)
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

        /// <summary>One frame of walking toward <paramref name="goal"/> (the approach to fight <paramref name="i"/>).</summary>
        void Steer(int i, Vector3 goal, bool sprint)
        {
            Vector3 feet = Feet();
            var target = PickTarget(i, out Vector3 aim);
            if (target != null) { _aimFixed = false; _aimPoint = aim; _fire = true; }
            else _aimPoint = null;
            ApplyMove(Flat(feet, goal) > CoarseArrive ? PathDirection(feet, goal) : Vector3.zero, sprint);
        }

        /// <summary>World direction (flat, length up to 1) into the player's move axes, with a side-step when stuck.</summary>
        void ApplyMove(Vector3 world, bool sprint)
        {
            float now = Time.unscaledTime;
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

        /// <summary>The way to <paramref name="goal"/> round the cover boxes: straight if clear, else toward the best corner of
        /// the first box in the way (as FightSim's floor-care detour), a few boxes deep.</summary>
        static Vector3 PathDirection(Vector3 from, Vector3 goal)
        {
            Vector2 a = new Vector2(from.x, from.z), g = new Vector2(goal.x, goal.z);
            Vector2 next = NextWaypoint(a, g, 3);
            Vector2 d = next - a;
            return d.sqrMagnitude > 1e-6f ? new Vector3(d.x, 0f, d.y).normalized : Vector3.zero;
        }

        static Vector2 NextWaypoint(Vector2 a, Vector2 g, int depth)
        {
            int box = FirstBlocking(a, g, -1);
            if (box < 0 || depth <= 0) return g;
            var r = _cover[box];
            float m = CornerMargin;
            Vector2[] corners =
            {
                new Vector2(r.xMin - m, r.yMin - m), new Vector2(r.xMax + m, r.yMin - m),
                new Vector2(r.xMin - m, r.yMax + m), new Vector2(r.xMax + m, r.yMax + m),
            };
            Vector2 best = g;
            float bestCost = float.PositiveInfinity;
            foreach (var c in corners)
            {
                if ((c - a).sqrMagnitude < 0.04f) continue;
                if (Blocks(_cover[box], a, c)) continue;
                float cost = (c - a).magnitude + (g - c).magnitude;
                if (cost < bestCost) { bestCost = cost; best = c; }
            }
            if (float.IsInfinity(bestCost)) return g;
            return NextWaypoint(a, best, depth - 1);
        }

        static int FirstBlocking(Vector2 a, Vector2 b, int skip)
        {
            int best = -1;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < _cover.Count; i++)
            {
                if (i == skip || !Blocks(_cover[i], a, b)) continue;
                float d = (_cover[i].center - a).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>The segment a-b passes through the box grown by the cover margin (a start inside it doesn't count).</summary>
        static bool Blocks(Rect r, Vector2 a, Vector2 b)
        {
            var g = Rect.MinMaxRect(r.xMin - CoverMargin, r.yMin - CoverMargin, r.xMax + CoverMargin, r.yMax + CoverMargin);
            if (g.Contains(a)) return false;
            // slab test
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                float p = axis == 0 ? a.x : a.y, dv = axis == 0 ? d.x : d.y;
                float lo = axis == 0 ? g.xMin : g.yMin, hi = axis == 0 ? g.xMax : g.yMax;
                if (Mathf.Abs(dv) < 1e-6f)
                {
                    if (p <= lo || p >= hi) return false;
                    continue;
                }
                float u0 = (lo - p) / dv, u1 = (hi - p) / dv;
                if (u0 > u1) { float t = u0; u0 = u1; u1 = t; }
                t0 = Mathf.Max(t0, u0);
                t1 = Mathf.Min(t1, u1);
                if (t0 >= t1) return false;
            }
            return true;
        }
    }
}
