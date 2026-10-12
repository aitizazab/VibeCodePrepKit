using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.DreamEscape
{
    /// <summary>A room of the escape: where it runs along the route, how it's turned (what used to be the floor), its
    /// light and sound.</summary>
    public sealed class EscapeRoom
    {
        public string name, sound, theme;
        public float s0, s1;
        /// <summary>How the room is turned: 0 the right way up, 90 / -90 on its side (the old floor is the right / left
        /// wall), 180 upside down. The collision is always upright; the dressing is turned.</summary>
        public float roll;
        /// <summary>Its floor at the start, its ceiling's height, its half width; open to the sky (no walls, no ceiling).</summary>
        public float floorTop, ceilingY, halfWidth = HouseEscape.W;
        public bool open;
        public Vector3 ambient, fog;
    }

    /// <summary>A cinematic beat of the escape: slow motion, a camera roll, a look back, a title, a shake.</summary>
    public sealed class EscapeBeat
    {
        public string name, kind;
        public float s, value, seconds;
    }

    /// <summary>The corridor that keeps getting a bit longer as she gets close to its end (the creator's beat), pure. The
    /// last two bays before its door are the same; when she crosses <see cref="triggerS"/> too slowly the game moves her
    /// (and everything chasing her) back one bay, under a dolly zoom: the door has pulled away. Fast enough (a slide into
    /// it, a wall kick, slide hops) and the door bursts open. The first time always stretches: that's the reveal.</summary>
    public sealed class StretchCorridor
    {
        public enum Result { None, Stretch, BreakThrough }

        public float startS, doorS, triggerS, bay, slope, halfWidth, height;
        /// <summary>Speed along the corridor needed at the trigger (m/s): faster than a sprint (9.5): slide into it.</summary>
        public float threshold = 11f;
        /// <summary>How much nearer the house's fold gets each time it stretches.</summary>
        public float foldGain = 2.5f;
        /// <summary>Lena unsure: after this many stretches Theo holds the door (any speed gets through). 0: never.</summary>
        public int theoHoldsAfter;
        int _stretches;
        bool _open;

        public int Stretches => _stretches;
        public bool Open => _open;
        /// <summary>Theo is holding the door open.</summary>
        public bool TheoHolds => theoHoldsAfter > 0 && _stretches >= theoHoldsAfter;
        /// <summary>The fold's gap after the stretches so far: each one lets it <see cref="foldGain"/> nearer (it moves back
        /// with her, but keeps what it gained), down to a metre.</summary>
        public float FoldGapAfterStretches(float baseGap) => Math.Max(1f, baseGap - _stretches * foldGain);

        /// <summary>The move a stretch makes (back one bay, up the slope).</summary>
        public Vector3 Shift => new Vector3(0f, bay * slope, -bay);

        public void Reset()
        {
            _stretches = 0;
            _open = false;
        }

        /// <summary>She moved from <paramref name="prevS"/> to <paramref name="s"/> at <paramref name="speed"/> along the
        /// corridor: did she cross the trigger, and what happens.</summary>
        public Result Check(float prevS, float s, float speed)
        {
            if (_open || prevS >= triggerS || s < triggerS) return Result.None;
            bool fast = _stretches > 0 && (speed >= threshold || TheoHolds);
            if (fast)
            {
                _open = true;
                return Result.BreakThrough;
            }
            _stretches++;
            return Result.Stretch;
        }

        /// <summary>Lena's outcome changes it: unsure, Theo holds the door after two stretches; condemning, it wants more
        /// speed and the fold gains faster.</summary>
        public void ApplyOutcome(ArgumentOutcome o)
        {
            threshold = o == ArgumentOutcome.Condemns ? 11.5f : 11f;
            foldGain = o == ArgumentOutcome.Condemns ? 4f : 2.5f;
            theoHoldsAfter = o == ArgumentOutcome.Unsure ? 3 : 0;
        }
    }

    /// <summary>The house folding shut behind her (the escape's pursuer): rubber-banded along the route like the Movement
    /// Lab's <see cref="Pursuer"/> (it keeps <see cref="targetGap"/> behind a player who keeps moving and only catches one
    /// who stalls), plus <see cref="Shift"/>: when the corridor stretches it moves back with her, but not all the way.</summary>
    public sealed class HouseFold
    {
        public float targetGap = 12f, k = 0.6f, minSpeed = 6f, maxSpeed = 11.5f, catchGap = 2f, catchTime = 0.75f, averageTime = 1f;
        float _s, _speed, _avg, _close;

        public float S => _s;
        public float Speed => _speed;
        public float Gap(float playerS) => playerS - _s;
        /// <summary>0..1: how close it is (sound and screen).</summary>
        public float Threat(float playerS) => Math.Max(0f, Math.Min(1f, 1f - (Gap(playerS) - catchGap) / Math.Max(0.01f, targetGap - catchGap)));

        public void Reset(float playerS, float playerSpeed = 0f)
        {
            _s = playerS - targetGap;
            _avg = _speed = playerSpeed;
            _close = 0f;
        }

        /// <summary>Moves it along the route (the corridor's stretch moves it back with her).</summary>
        public void Shift(float ds) => _s += ds;

        public bool Step(float playerS, float playerSpeedAlong, float dt)
        {
            if (!(dt > 0f)) return false;
            float a = averageTime > 1e-3f ? 1f - (float)Math.Exp(-dt / averageTime) : 1f;
            _avg += (Math.Max(0f, playerSpeedAlong) - _avg) * a;
            float v = _avg + k * (playerS - _s - targetGap);
            _speed = v < minSpeed ? minSpeed : v > maxSpeed ? maxSpeed : v;
            _s += _speed * dt;
            if (playerS - _s < catchGap) _close += dt; else _close = 0f;
            if (_close < catchTime) return false;
            _close = 0f;
            return true;
        }
    }

    /// <summary>
    /// The house throws her out (docs/redesign/house.md "The escape"): a fever-dream run through the house as it can't be,
    /// far longer than the house could hold, along one straight route (+Z, s = z) in its own space, on the Movement Lab's
    /// <see cref="LabCourse"/> (so the course autopilot and the reach checks work on it), chased by the house folding shut.
    /// Every room is closed in: solid walls on both sides the whole way (every one runnable), a ceiling, end walls with
    /// doorways; gaps open onto the void under the house: falling in, or being caught by the fold, throws her out of the
    /// dream. The rooms, in order:
    /// <list type="number">
    /// <item>The dining room tips: run the table as it falls away, slide under the hanging sideboard.</item>
    /// <item>The hall turns over: run the wall over the gap, then the doorframes, wall to wall.</item>
    /// <item>The stairs flatten into a slide, launching her over the stairwell (slow motion at the lip).</item>
    /// <item><b>The corridor that keeps getting longer</b> (<see cref="StretchCorridor"/>).</item>
    /// <item>Theo's room, upside down: over his things, under his bed.</item>
    /// <item>The kitchen, come round again: the table the long way, under the hanging fridge, over the sink's pit; then the
    /// hall a third time, wall to wall.</item>
    /// <item>The attic, open to the stars: floorboards in the night, a double jump to the telescope, the round window.</item>
    /// <item>The last hall, the right way up: the umbrella by the front door; out onto the hill, in the rain.</item>
    /// </list>
    /// </summary>
    public sealed class HouseEscape
    {
        /// <summary>The rooms' half width (their walls' faces) and the corridor's.</summary>
        public const float W = 4.5f, CorridorW = 1.6f, WallT = 0.5f, VoidBottom = -70f;
        /// <summary>A room's ceiling over its first floor.</summary>
        public const float RoomHeight = 7.5f, CorridorHeight = 3.4f;
        /// <summary>Where the escape sits in the house's frame (far below the house, out of sight).</summary>
        public static readonly Vector3 Origin = new Vector3(0f, -260f, 400f);

        public readonly LabCourse course = new LabCourse();
        public readonly List<HouseLook> looks = new List<HouseLook>();
        public readonly List<CourseBeat> reach = new List<CourseBeat>();
        public readonly List<EscapeRoom> rooms = new List<EscapeRoom>();
        public readonly List<EscapeBeat> beats = new List<EscapeBeat>();
        public readonly StretchCorridor corridor = new StretchCorridor();
        /// <summary>The corridor's door (a piece: solid until it bursts open).</summary>
        public int corridorDoor = -1;
        public float frontDoorS, frontDoorFloor;
        public DreamRainSpot rain;
        public int startCheckpoint, corridorCheckpoint, frontDoorCheckpoint;
        /// <summary>Under this she has fallen out of the dream.</summary>
        public float FallY => course.killY;

        public static HouseEscape Build()
        {
            var e = new HouseEscape();
            e.Make();
            return e;
        }

        // ---------- The kit ----------

        int Add(string name, LabPieceKind kind, HouseLook look, Vector3 min, Vector3 max, bool solid = true, bool runnable = false)
        {
            course.pieces.Add(new LabPiece { name = name, kind = kind, center = (min + max) * 0.5f, size = max - min, runnable = runnable, solid = solid });
            looks.Add(look);
            return course.pieces.Count - 1;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        void Floor(string name, HouseLook look, float z0, float z1, float top, float halfWidth = W) =>
            Add(name, LabPieceKind.Floor, look, V(-halfWidth, top - 2f, z0), V(halfWidth, top, z1));

        int Ramp(string name, HouseLook look, float x0, float x1, float z0, float z1, float top0, float top1)
        {
            int i = Add(name, LabPieceKind.Ramp, look, V(x0, Math.Min(top0, top1) - HouseLint.RampThickness, z0), V(x1, Math.Max(top0, top1), z1));
            course.pieces[i].rampTopStart = top0;
            course.pieces[i].rampTopEnd = top1;
            return i;
        }

        void Bar(string name, HouseLook look, float z, float depth, float floorTop, float halfWidth, float upTo) =>
            Add(name, LabPieceKind.Bar, look, V(-halfWidth, floorTop + 1.15f, z), V(halfWidth, upTo, z + depth));

        void Cue(LabCueKind kind, float s, float sEnd = 0f) => course.cues.Add(new LabCue { kind = kind, s = s, sEnd = sEnd });
        void Lane(float s, float x) => course.lane.Add(new Vector2(s, x));
        void Reach(CourseBeatKind kind, string name, float s, float size, float height = 0f) => reach.Add(new CourseBeat { kind = kind, name = name, s = s, size = size, height = height });
        void Beat(string kind, string name, float s, float value, float seconds) => beats.Add(new EscapeBeat { kind = kind, name = name, s = s, value = value, seconds = seconds });

        int Checkpoint(string name, float z, float top, string chapter = "")
        {
            course.checkpoints.Add(new LabCheckpoint { name = name, chapter = chapter, position = V(course.LaneAt(z), top + 0.02f, z), yaw = 0f, s = z });
            return course.checkpoints.Count - 1;
        }

        EscapeRoom _open;
        float _minFloor = 0f;

        /// <summary>Starts a room at <paramref name="s"/> with its floor at <paramref name="top"/>.</summary>
        EscapeRoom Room(string name, float s, float top, float roll, string theme, Vector3 ambient, Vector3 fog, string sound, bool open = false, float halfWidth = W, float height = RoomHeight)
        {
            var r = new EscapeRoom { name = name, s0 = s, floorTop = top, ceilingY = top + height, roll = roll, theme = theme, ambient = ambient, fog = fog, sound = sound, open = open, halfWidth = halfWidth };
            rooms.Add(r);
            course.sections.Add(new KeyValuePair<float, string>(s, name));
            _open = r;
            return r;
        }

        /// <summary>Ends the open room at <paramref name="s"/> (on a floor at <paramref name="top"/>): its side walls from
        /// the void to its ceiling (runnable), its ceiling, and the wall at its end with a doorway onto the next room
        /// (<paramref name="doorHalf"/> wide each side, 3.6 m high).</summary>
        void EndRoom(float s, float top, float nextCeiling, float doorHalf = 2.2f, bool endWall = true)
        {
            var r = _open;
            r.s1 = s;
            if (!r.open)
            {
                float hw = r.halfWidth, c = r.ceilingY;
                Add(r.name + ": wall (left)", LabPieceKind.Wall, HouseLook.EscFloorboardWall, V(-hw - WallT, VoidBottom, r.s0), V(-hw, c + 0.3f, s), true, true);
                Add(r.name + ": wall (right)", LabPieceKind.Wall, HouseLook.EscFloorboardWall, V(hw, VoidBottom, r.s0), V(hw + WallT, c + 0.3f, s), true, true);
                Add(r.name + ": ceiling", LabPieceKind.Floor, HouseLook.EscCeiling, V(-hw, c, r.s0 + 0.3f), V(hw, c + 0.3f, s));
            }
            if (endWall)
            {
                float hw = Math.Max(r.halfWidth, W), wallTop = Math.Max(r.open ? top + 4f : r.ceilingY, nextCeiling) + 0.3f;
                Add(r.name + ": end wall (left)", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(-hw, top, s), V(-doorHalf, wallTop, s + 0.3f), true, false);
                Add(r.name + ": end wall (right)", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(doorHalf, top, s), V(hw, wallTop, s + 0.3f), true, false);
                Add(r.name + ": over the doorway", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(-doorHalf, top + 3.6f, s), V(doorHalf, wallTop, s + 0.3f), true, false);
            }
        }

        /// <summary>The floor ends at <paramref name="floorEnd"/>: jump to the wall on one side (<paramref name="side"/>
        /// +1 right, -1 left), run it over the gap, land 1 m lower at the returned z (the proven pattern).</summary>
        float WallrunAcross(string name, float floorEnd, float side)
        {
            Lane(floorEnd - 16f, 0f);
            Lane(floorEnd - 12f, 3.6f * side);
            Cue(LabCueKind.Jump, floorEnd - 0.8f);
            Cue(LabCueKind.WallJump, floorEnd + 12.5f);
            Reach(CourseBeatKind.Wallrun, name, floorEnd - 0.8f, 14f, 6f);
            Lane(floorEnd + 14f, 3.6f * side);
            Lane(floorEnd + 20f, 0f);
            return floorEnd + 18f;
        }

        /// <summary>The floor ends at <paramref name="e"/>: two doorframes (free-standing walls) to kick between over the
        /// drop, landing 1 m lower at the returned z (the proven pattern).</summary>
        float Doorframes(float e, float t)
        {
            Lane(e - 8f, -1.95f);
            Add("Doorframe (left)", LabPieceKind.Wall, HouseLook.EscDoor, V(-3.5f, VoidBottom, e + 1f), V(-2.5f, t + 6f, e + 13f), true, true);
            Add("Doorframe (right)", LabPieceKind.Wall, HouseLook.EscDoor, V(2.5f, VoidBottom, e + 7f), V(3.5f, t + 6f, e + 25f), true, true);
            Cue(LabCueKind.Jump, e - 0.8f);
            Cue(LabCueKind.WallJump, e + 1.6f);
            Cue(LabCueKind.WallJump, e + 20.5f);
            Cue(LabCueKind.DoubleJumpAtApex, e + 21f);
            Lane(e + 6f, -1.95f); Lane(e + 10f, 2.1f); Lane(e + 22f, 2.1f); Lane(e + 28f, 0f);
            Reach(CourseBeatKind.WallToWall, "The doorframes", e - 0.8f, 5f);
            Reach(CourseBeatKind.Wallrun, "The right doorframe", e + 1.6f, 18f, 6f);
            return e + 26f;
        }

        static Vector3 C(float r, float g, float b) => new Vector3(r, g, b);

        // =====================================================================================================

        void Make()
        {
            var c = course;
            float z = 0f, t = 0f;
            Lane(0f, 0f);

            // ---- 0. The dining room tips (on its side: the old wall is the floor). The table falls away; slide under the
            // sideboard hanging from what was the wall.
            Room("The dining room tips", z, t, 90f, "dining", C(1.0f, 0.86f, 0.72f), C(0.95f, 0.82f, 0.75f), "mv_collapse");
            Floor("The tipped dining room (its wall is the floor)", HouseLook.EscWallpaperFloor, z, z + 14f, t);
            startCheckpoint = Checkpoint("The house tips", 4f, t, DreamHouse.ChapterTips);
            c.chaseStartS = 4f;
            Beat("slowmo", "Thrown", 4.2f, 0.35f, 0.6f);
            Beat("title", "The house tips", 4.2f, 0f, 2.2f);
            Ramp("The table (falling away)", HouseLook.EscTable, -1.3f, 1.3f, z + 14f, z + 30f, t, t - 1.4f);
            Reach(CourseBeatKind.SlideRamp, "The falling table", z + 14f, 16f, 1.4f);
            z += 30f; t -= 1.4f;
            Floor("The tipped dining room (far end)", HouseLook.EscWallpaperFloor, z, z + 12f, t);
            Bar("The sideboard (hanging from the wall)", HouseLook.EscFurniture, z + 4f, 1.4f, t, W, t + 3.5f);
            Cue(LabCueKind.Crouch, z, z + 7f);
            Reach(CourseBeatKind.Slide, "Under the sideboard", z + 4f, 1.4f, 1.15f);
            z += 12f;
            EndRoom(z - 4f, t, t + RoomHeight);

            // ---- 1. The hall turns over while she runs it: the floor is gone, run its wall; then the doorframes.
            Room("The hall turns over", z - 4f, t, 90f, "hall", C(0.95f, 0.8f, 0.82f), C(0.9f, 0.78f, 0.85f), "glitch");
            Beat("roll", "The hall turns over", z - 3f, 22f, 1.4f);
            Floor("The hall (on its side)", HouseLook.EscWallpaperFloor, z, z + 24f, t);
            Checkpoint("The hall turns over", z + 3f, t);
            z = WallrunAcross("The hall's floor (now a wall)", z + 24f, 1f);
            t -= 1f;
            Floor("The hall's far end", HouseLook.EscWallpaperFloor, z, z + 20f, t);
            Checkpoint("The doorframes", z + 4f, t);
            z = Doorframes(z + 20f, t);
            t -= 1f;
            Floor("The landing", HouseLook.EscWallpaperFloor, z, z + 10f, t);
            z += 10f;
            EndRoom(z - 4f, t, t + RoomHeight);

            // ---- 2. The stairs flatten into a slide: down it, fast, and off its lip over the stairwell (slow motion).
            Room("The stairs flatten", z - 4f, t, 0f, "stairs", C(0.9f, 0.84f, 0.9f), C(0.85f, 0.82f, 0.92f), "mv_collapse");
            Floor("The top of the stairs", HouseLook.EscWallpaperFloor, z, z + 6f, t);
            Checkpoint("The stairs", z + 1f, t);
            Ramp("The staircase (now a slide)", HouseLook.EscStairs, -W, W, z + 6f, z + 26f, t, t - 5f);
            Cue(LabCueKind.Crouch, z + 4f, z + 26.5f);
            Reach(CourseBeatKind.SlideRamp, "The staircase", z + 6f, 20f, 5f);
            z += 26f; t -= 5f;
            Cue(LabCueKind.Jump, z - 0.8f);
            Cue(LabCueKind.DoubleJumpAtApex, z + 0.6f);
            Reach(CourseBeatKind.DoubleGap, "Over the stairwell", z - 0.8f, 8f);
            Beat("slowmo", "Off the stairs' lip", z - 0.2f, 0.3f, 0.5f);
            z += 8f; t -= 1f;
            Floor("The foot of the stairs", HouseLook.EscWallpaperFloor, z, z + 15f, t);
            z += 15f;
            EndRoom(z, t, t + CorridorHeight, CorridorW);

            // ---- 3. The corridor that keeps getting longer: a long upstairs hall, narrow and low, the front door's light
            // at its end. It slopes down (slides keep their speed); the same half-open door three times on the way; then
            // the last two bays, the same as each other, and the trigger: too slow and the door pulls away.
            float cs = z;
            const float slope = 0.04f;
            var cr = Room("The corridor", cs, t, 0f, "corridor", C(0.92f, 0.86f, 0.8f), C(0.7f, 0.62f, 0.66f), "stretch", false, CorridorW, CorridorHeight);
            corridorCheckpoint = Checkpoint("The corridor", cs + 3f, t);
            Beat("title", "The corridor", cs + 3f, 0f, 2f);
            const float runUp = 46f, bay = 20f;
            float doorS = cs + runUp + 2f * bay;
            float t0 = t, tEnd = t - (doorS - cs) * slope;
            Ramp("The corridor's floor (a runner, sloping away)", HouseLook.EscCorridorFloor, -CorridorW, CorridorW, cs, doorS, t0, tEnd);
            for (int k = 0; k < 3; k++)
            {
                float d = cs + 10f + k * 12f, ft = t0 - (d - cs) * slope;
                Add("The same door, half open (" + (k + 1) + ")", LabPieceKind.Bar, HouseLook.EscDoor, V(-CorridorW, ft + 1.15f, d), V(CorridorW, ft + CorridorHeight - 0.05f, d + 0.5f));
                Cue(LabCueKind.Crouch, d - 4f, d + 1.5f);
                Reach(CourseBeatKind.Slide, "The same door " + (k + 1), d, 0.5f, 1.15f);
            }
            corridor.startS = cs;
            corridor.doorS = doorS;
            corridor.bay = bay;
            corridor.slope = slope;
            corridor.halfWidth = CorridorW;
            corridor.height = CorridorHeight;
            corridor.triggerS = doorS - 10f;
            // Into the trigger in a slide (its boost on the slope): fast enough to burst through.
            Cue(LabCueKind.Crouch, corridor.triggerS - 6f, corridor.triggerS + 1f);
            Reach(CourseBeatKind.SlideRamp, "The corridor's last bays", corridor.triggerS - 6f, 7f, 0.3f);
            // Its walls and its ceiling (a ramp parallel to the floor), the door.
            float ctop = t0 + CorridorHeight + 0.3f;
            Add("The corridor: wall (left)", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(-CorridorW - WallT, VoidBottom, cs + 0.3f), V(-CorridorW, ctop, doorS), true, true);
            Add("The corridor: wall (right)", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(CorridorW, VoidBottom, cs + 0.3f), V(CorridorW + WallT, ctop, doorS), true, true);
            Ramp("The corridor: ceiling", HouseLook.EscCeiling, -CorridorW, CorridorW, cs + 0.3f, doorS, t0 + CorridorHeight + HouseLint.RampThickness, tEnd + CorridorHeight + HouseLint.RampThickness);
            corridorDoor = Add("The door at the end of the corridor", LabPieceKind.Wall, HouseLook.EscFrontDoor, V(-CorridorW, tEnd + 0.01f, doorS - 0.15f), V(CorridorW, tEnd + CorridorHeight, doorS));
            cr.s1 = doorS;
            z = doorS; t = tEnd;
            course.sections.Add(new KeyValuePair<float, string>(corridor.triggerS, "The corridor's end"));
            Beat("slowmo", "Through the door", doorS + 0.5f, 0.3f, 0.6f);

            // ---- 4. Theo's room, upside down (its ceiling is the floor): over his things, under his bed.
            Room("Theo's room, upside down", z, t, 180f, "theo", C(0.82f, 0.9f, 1.0f), C(0.76f, 0.84f, 0.95f), "chime");
            Add("Theo's room: the wall round the door (left)", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(-W, t, z), V(-CorridorW, t + RoomHeight + 0.3f, z + 0.3f));
            Add("Theo's room: the wall round the door (right)", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(CorridorW, t, z), V(W, t + RoomHeight + 0.3f, z + 0.3f));
            Add("Theo's room: over the door", LabPieceKind.Wall, HouseLook.EscCorridorWall, V(-CorridorW, t + CorridorHeight, z), V(CorridorW, t + RoomHeight + 0.3f, z + 0.3f));
            Floor("Theo's ceiling (the floor now: his glow stars)", HouseLook.EscCeiling, z, z + 12f, t);
            Checkpoint("Theo's room", z + 3f, t);
            z += 12f;
            foreach (var (gap, len) in new[] { (4f, 8f), (5f, 14f) })
            {
                Cue(LabCueKind.Jump, z - 0.8f);
                Reach(CourseBeatKind.Gap, "Over Theo's things", z - 0.8f, gap);
                Floor("Theo's things (a floor now)", HouseLook.EscFurniture, z + gap, z + gap + len, t);
                z += gap + len;
            }
            Bar("Theo's bed (hanging: slide under it)", HouseLook.EscFurniture, z - 8f, 1.6f, t, W, t + 3.4f);
            Cue(LabCueKind.Crouch, z - 12f, z - 5.5f);
            Reach(CourseBeatKind.Slide, "Under Theo's bed", z - 8f, 1.6f, 1.15f);
            EndRoom(z - 3f, t, t + RoomHeight);

            // ---- 5. The kitchen, come round again (turned the other way): its table the long way, under the hanging
            // fridge, over the sink's pit; then the hall a third time, left wall, right wall.
            Room("The kitchen, come round again", z - 3f, t, -90f, "kitchen", C(1.0f, 0.88f, 0.7f), C(0.95f, 0.85f, 0.7f), "mv_collapse");
            Floor("The kitchen (turned)", HouseLook.EscWallpaperFloor, z, z + 10f, t);
            Checkpoint("The kitchen, again", z + 3f, t);
            Ramp("The table again (the long way)", HouseLook.EscTable, -1.3f, 1.3f, z + 10f, z + 26f, t, t - 1.4f);
            z += 26f; t -= 1.4f;
            Floor("Under the fridge", HouseLook.EscWallpaperFloor, z, z + 16f, t);
            Bar("The fridge (hanging over the floor)", HouseLook.EscFurniture, z + 6f, 1.5f, t, W, t + 3.6f);
            Cue(LabCueKind.Crouch, z + 2f, z + 9.5f);
            Reach(CourseBeatKind.Slide, "Under the fridge", z + 6f, 1.5f, 1.15f);
            z += 16f;
            Cue(LabCueKind.Jump, z - 0.8f);
            Reach(CourseBeatKind.Gap, "The sink's pit", z - 0.8f, 5f);
            z += 5f;
            Floor("The counter's far end", HouseLook.EscWallpaperFloor, z, z + 22f, t);
            Checkpoint("The hall, a third time", z + 4f, t);
            z = WallrunAcross("The third hall: the left wall", z + 22f, -1f);
            t -= 1f;
            Floor("Between the walls", HouseLook.EscWallpaperFloor, z, z + 20f, t);
            z = WallrunAcross("The third hall: the right wall", z + 20f, 1f);
            t -= 1f;
            Floor("The attic stairs' foot", HouseLook.EscWallpaperFloor, z, z + 12f, t);
            z += 12f;
            EndRoom(z - 4f, t, t + 4f, 2.2f);

            // ---- 6. The attic, open to the stars: no walls, no roof; floorboards hanging in the night; a double jump to
            // the telescope's platform (slow motion at the top); then run the round window's frame.
            Room("The attic, open to the stars", z - 4f, t, 0f, "attic", C(0.55f, 0.6f, 0.95f), C(0.3f, 0.32f, 0.6f), "chime", true);
            Floor("The attic's first boards", HouseLook.EscSkyBoards, z, z + 10f, t, 2.4f);
            Checkpoint("The attic", z + 3f, t);
            z += 10f;
            foreach (var (gap, len) in new[] { (3.5f, 7f), (4f, 7f), (4.5f, 8f) })
            {
                Cue(LabCueKind.Jump, z - 0.8f);
                Reach(CourseBeatKind.Gap, "Over the sky", z - 0.8f, gap);
                Floor("Floorboards over the sky", HouseLook.EscSkyBoards, z + gap, z + gap + len, t, 2.4f);
                z += gap + len;
            }
            Cue(LabCueKind.Jump, z - 0.8f);
            Cue(LabCueKind.DoubleJumpAtApex, z + 0.8f);
            Reach(CourseBeatKind.DoubleGap, "To the telescope", z - 0.8f, 7.5f);
            Beat("slowmo", "Over the stars", z + 2.2f, 0.35f, 0.6f);
            z += 7.5f;
            Floor("The telescope's platform", HouseLook.EscAtticBoards, z, z + 16f, t, 3.2f);
            Checkpoint("The telescope", z + 4f, t);
            // The round window's frame: a lone wall over the sky on the right; run it to the last hall.
            Lane(z + 16f - 16f, 0f); Lane(z + 16f - 12f, 3.6f);
            Add("The round window's frame", LabPieceKind.Wall, HouseLook.EscAtticBoards, V(4.5f, VoidBottom, z + 16f + 3f), V(5.5f, t + 6f, z + 16f + 17f), true, true);
            Cue(LabCueKind.Jump, z + 16f - 0.8f);
            Cue(LabCueKind.WallJump, z + 16f + 12.5f);
            Reach(CourseBeatKind.Wallrun, "The round window", z + 16f - 0.8f, 14f, 6f);
            Lane(z + 16f + 14f, 3.6f); Lane(z + 16f + 20f, 0f);
            z += 16f + 18f; t -= 1f;
            rooms[rooms.Count - 1].s1 = z;

            // ---- 7. The last hall, the right way up, warm: the front door, the umbrella beside it.
            Room("The last hall", z, t, 0f, "lasthall", C(0.98f, 0.92f, 0.86f), C(0.9f, 0.86f, 0.84f), "chime", false, 2.6f, 4.2f);
            Floor("The last hall (the right way up)", HouseLook.EscLastHall, z, z + 22f, t, 2.6f);
            frontDoorCheckpoint = Checkpoint("The last hall", z + 4f, t, DreamHouse.ChapterFrontDoor);
            float fd = z + 22f;
            EndRoom(fd, t, t + 4.2f, 0.75f, true);
            frontDoorS = fd;
            frontDoorFloor = t;
            Beat("slowmo", "The front door", fd - 0.5f, 0.4f, 0.5f);
            c.chaseEndS = fd - 4f;
            // Past the door: the hill (the game moves her there through the door); here, for the course and the tests, the
            // rain path runs on straight.
            Floor("The path in the rain", HouseLook.EscRain, fd, fd + 36f, t, 2.6f);
            rain = new DreamRainSpot
            {
                umbrella = V(1.9f, t + 0.55f, fd - 1.0f),
                umbrellaRadius = 0.6f,
                rainStartS = fd,
                despairS = fd + DreamHouseStory.DespairDepth,
                rainEndS = fd + 33f,
                resetS = fd - 18f,
                floorY = t,
            };

            _minFloor = t;
            foreach (var p in c.pieces) if (p.kind == LabPieceKind.Floor || p.kind == LabPieceKind.Ramp) _minFloor = Math.Min(_minFloor, p.Max.Y);
            c.killY = _minFloor - 12f;
            c.pursuer = new Pursuer { targetGap = 12f, k = 0.6f, minSpeed = 6f, maxSpeed = 11.5f };
            c.route = new PathLine(new[] { V(0f, 0f, 0f), V(0f, 0f, fd + 60f) });
            c.finishS = rain.rainEndS;
            reach.Sort((a, b) => a.s.CompareTo(b.s));
        }

        /// <summary>The room at <paramref name="s"/> (its index), or -1.</summary>
        public int RoomAt(float s)
        {
            int at = -1;
            for (int i = 0; i < rooms.Count; i++) if (s >= rooms[i].s0) at = i;
            return at;
        }
    }
}
