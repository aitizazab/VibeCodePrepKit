using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>A touch marker: a glowing plate the route must pass over (the Gauntlet's targets, without guns).
    /// Missing one adds <see cref="GauntletCourse.MissPenalty"/> seconds.</summary>
    public sealed class GauntletMarker
    {
        public string name;
        /// <summary>The volume that counts as touching it (feet inside, grown by the body's radius).</summary>
        public Vector3 min, max;
        public int section;
        public float S => (min.Z + max.Z) * 0.5f;

        public bool Touches(Vector3 feet, float radius, float height)
        {
            return feet.X >= min.X - radius && feet.X <= max.X + radius
                && feet.Z >= min.Z - radius && feet.Z <= max.Z + radius
                && feet.Y + height >= min.Y && feet.Y <= max.Y;
        }
    }

    /// <summary>One timed section of the Gauntlet: it starts at a checkpoint (where a fall respawns you) and its end is a split.</summary>
    public sealed class GauntletSection
    {
        public string name;
        public float startS, endS;
        public int checkpoint;
        /// <summary>The safe line and the fast, riskier line, in words (RESEARCH-MOVEMENT 8.4 rule 4).</summary>
        public string safeLine, fastLine;
    }

    /// <summary>
    /// The Movement Lab's Gauntlet (after Titanfall 2's Pilot's Gauntlet; docs/MOVEMENT.md "The Gauntlet"): a timed
    /// obstacle course. A start gate starts the clock, nine sections each end in a split, touch markers must be
    /// passed (2 s each missed, as in Titanfall 2), a finish line stops it. The first half teaches one move per
    /// beat (the sprint kick at a vault, the slide under a bar and down a ramp, single and double jumps, a wallrun,
    /// wall-to-wall kicks, the chimney beside a three-height mantle stair); the second half chains them
    /// (wallrun, kick, wallrun, slide, mantle; slide-hops down steps with a momentum skip above them; the long wall
    /// into the finish). Every section has a safe line (stepping stones, the mantle stair, mantling the vault)
    /// beside the fast line the autopilot runs.
    /// It's appended to the old <see cref="LabCourse"/> (kept as free roam, unchanged at z 0..530) at
    /// z = <see cref="Origin"/> along the same straight route, so one course drives the lab: the autopilot, the
    /// respawns and the dotnet tests' fake world all work on it. s = z.
    /// </summary>
    public sealed class GauntletCourse
    {
        public const float Origin = 600f;
        public const string ChapterStart = "Gauntlet start";
        public const string ChapterTeaching = "Gauntlet: teaching half";
        public const string ChapterCombo = "Gauntlet: combo half";
        public const string ChapterFinish = "Gauntlet: finish";
        /// <summary>Seconds added for each marker you didn't touch (Titanfall 2 adds 2 s per target left).</summary>
        public const float MissPenalty = 2f;
        /// <summary>The course autopilot's run from the gate to the finish in the dotnet sim at 60 fps: a test driver's time,
        /// for tests and logs only (pinned by a test: change it when the course changes). The board shows the developer's
        /// real best from <see cref="DeveloperRecords"/> (the creator beat this by 10 s, 2026-10-10).</summary>
        public const float AutopilotTime = 41.3f; // the Mild hop-cost preset (Off 40.9, Current 41.75; 2026-10-11)

        public readonly LabCourse course;
        public readonly List<GauntletMarker> markers = new List<GauntletMarker>();
        public readonly List<GauntletSection> sections = new List<GauntletSection>();
        public readonly List<CourseBeat> beats = new List<CourseBeat>();
        public float gateS, finishS;
        public int startCheckpoint;
        /// <summary>The first course piece that belongs to the Gauntlet (the old course's come before).</summary>
        public int firstPiece;
        /// <summary>Where the start pad's floor is and the finish platform's (for the gate and finish arches).</summary>
        public float startFloorY, finishFloorY;
        /// <summary>The lowest floor of the Gauntlet.</summary>
        public float lowestFloor;

        GauntletCourse(LabCourse c) { course = c; }

        /// <summary>An empty timed run on an existing course, for another chapter's own clock (the dream tool's): its builder
        /// fills in the gate, the sections, any markers and the finish, and a <see cref="GauntletClock"/> runs it.</summary>
        public static GauntletCourse Chapter(LabCourse course) => new GauntletCourse(course);

        /// <summary>The lab's whole course: the old course (free roam) and the Gauntlet after it.</summary>
        public static GauntletCourse Build()
        {
            var g = new GauntletCourse(LabCourse.Build());
            g.Make();
            return g;
        }

        /// <summary>The section at <paramref name="s"/>, or -1 outside the Gauntlet.</summary>
        public int SectionAt(float s)
        {
            if (s < gateS || s > finishS + 5f) return -1;
            for (int i = sections.Count - 1; i >= 0; i--)
                if (s >= sections[i].startS) return i;
            return 0;
        }

        /// <summary>Where the Gauntlet's stretch of the lab ends: the dream tool chapter (<see cref="DreamToolCourse"/>) comes
        /// after it with its own clock, so the Gauntlet's HUD and restart stop here.</summary>
        public const float End = Origin + 520f;

        public bool InGauntlet(float s) => s >= Origin - 20f && s < End;

        public List<string> CheckReach(MoveDef def, float skin = 0.04f, float crouchStepOffset = 0f) => CourseReach.Check(beats, def, skin, crouchStepOffset);

        // ---------- Building ----------

        const float W = 8f; // the floors are 16 m wide: room for a safe line beside the fast one
        const float Deep = -24f;

        static float Z(float rel) => Origin + rel;

        LabPiece Add(string name, LabPieceKind kind, Vector3 min, Vector3 max, bool runnable = false, string section = "")
        {
            var p = new LabPiece { name = name, kind = kind, center = (min + max) * 0.5f, size = max - min, runnable = runnable, section = section };
            course.pieces.Add(p);
            return p;
        }

        LabPiece Floor(string name, float z0, float z1, float top, float x0 = -W, float x1 = W) =>
            Add(name, LabPieceKind.Floor, new Vector3(x0, top - 2f, Z(z0)), new Vector3(x1, top, Z(z1)));

        /// <summary>A block from deep below up to a ledge top (mantle onto it, or land on it).</summary>
        LabPiece Block(string name, float z0, float z1, float top, float x0 = -W, float x1 = W, float bottom = Deep) =>
            Add(name, LabPieceKind.Ledge, new Vector3(x0, bottom, Z(z0)), new Vector3(x1, top, Z(z1)));

        /// <summary>A stepping stone of the safe line.</summary>
        LabPiece Stone(string name, float z0, float z1, float top, float x0, float x1) =>
            Add(name, LabPieceKind.Floor, new Vector3(x0, Deep, Z(z0)), new Vector3(x1, top, Z(z1)));

        LabPiece Wall(string name, float faceX, float z0, float z1, float top)
        {
            float x0 = faceX > 0f ? faceX : faceX - 1f;
            return Add(name, LabPieceKind.Wall, new Vector3(x0, Deep, Z(z0)), new Vector3(x0 + 1f, top, Z(z1)), true);
        }

        LabPiece Ramp(string name, float z0, float z1, float top0, float top1, float x0 = -W, float x1 = W)
        {
            var p = Add(name, LabPieceKind.Ramp, new Vector3(x0, Math.Min(top0, top1) - 2f, Z(z0)), new Vector3(x1, Math.Max(top0, top1), Z(z1)));
            p.rampTopStart = top0;
            p.rampTopEnd = top1;
            return p;
        }

        void Cue(LabCueKind kind, float s, float sEnd = 0f, float x = 0f) =>
            course.cues.Add(new LabCue { kind = kind, s = Z(s), sEnd = sEnd != 0f ? Z(sEnd) : 0f, x = x });

        void Lane(float s, float x) => course.lane.Add(new Vector2(Z(s), x));

        void Beat(CourseBeatKind kind, string name, float s, float size, float height = 0f) =>
            beats.Add(new CourseBeat { kind = kind, name = name, s = Z(s), size = size, height = height });

        void Marker(string name, float z, float floorTop, float x0 = -2.5f, float x1 = 2.5f, float height = 3.5f)
        {
            markers.Add(new GauntletMarker { name = name, min = new Vector3(x0, floorTop, Z(z) - 1f), max = new Vector3(x1, floorTop + height, Z(z) + 1f), section = sections.Count - 1 });
        }

        void Section(string name, float startRel, float floorTop, string safe, string fast, string chapter = "")
        {
            if (sections.Count > 0) sections[sections.Count - 1].endS = Z(startRel);
            course.checkpoints.Add(new LabCheckpoint { name = "Gauntlet: " + name, chapter = chapter, position = new Vector3(course.LaneAt(Z(startRel)), floorTop + 0.02f, Z(startRel)), yaw = 0f, s = Z(startRel) });
            sections.Add(new GauntletSection { name = name, startS = Z(startRel), checkpoint = course.checkpoints.Count - 1, safeLine = safe, fastLine = fast });
            course.sections.Add(new KeyValuePair<float, string>(Z(startRel), "Gauntlet: " + name));
        }

        void Make()
        {
            var c = course;
            firstPiece = c.pieces.Count;
            c.route = new PathLine(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, Origin + 700f) }); // s = z
            const float y0 = 0f, y1 = -3f, y2 = -4f, y3 = -5f;
            const float top = y3 + 1.0f + 1.35f + 1.65f;      // the climb's top (both lines, 4 m like the lab's chimney): mantles of 1.0, 1.35, 1.65 m
            startFloorY = y0;

            // ===== Start: the pad, the gate (the clock starts as you cross it).
            Lane(-14f, 0f);
            c.sections.Add(new KeyValuePair<float, string>(Z(-14f), "Gauntlet: start pad"));
            c.checkpoints.Add(new LabCheckpoint { name = ChapterStart, chapter = ChapterStart, position = new Vector3(0f, y0 + 0.02f, Z(-8f)), yaw = 0f, s = Z(-8f) });
            startCheckpoint = c.checkpoints.Count - 1;
            gateS = Z(0f);
            Floor("G Start pad", -14f, 46f, y0);

            // ----- 1. Sprint and the kick: a waist-high vault. Hop it (fast) or mantle it and sprint-kick away (safe).
            sections.Add(new GauntletSection { name = "Sprint and kick", startS = gateS, checkpoint = startCheckpoint, safeLine = "Mantle the vault, then sprint: the kick pushes you straight back to speed", fastLine = "Hop the vault without breaking stride" });
            Block("G Vault (1.0 m)", 14f, 14.6f, y0 + 1f, -W, W, y0 - 0.5f);
            Cue(LabCueKind.Jump, 11.2f);
            Beat(CourseBeatKind.Vault, "The vault", 11.2f, 0.6f, 1f);
            Marker("After the vault", 24f, y0);

            // ----- 2. Slide: under a bar, then down a ramp (slide it to speed up).
            Section("Slide", 30f, y0, "Crouch-walk under the bar and run down the ramp", "Slide under the bar and down the ramp");
            Add("G Slide bar", LabPieceKind.Bar, new Vector3(-W - 0.5f, y0 + 1.15f, Z(39.6f)), new Vector3(W + 0.5f, y0 + 2.2f, Z(40.4f)));
            markers.Add(new GauntletMarker { name = "Under the bar", min = new Vector3(-W, y0, Z(39.2f)), max = new Vector3(W, y0 + 1.1f, Z(40.8f)), section = sections.Count - 1 });
            Cue(LabCueKind.Crouch, 35f, 64f);
            Beat(CourseBeatKind.Slide, "Under the slide bar", 35f, 0.8f, 1.15f);
            Ramp("G Slide ramp", 46f, 62f, y0, y1);
            Beat(CourseBeatKind.SlideRamp, "Down the slide ramp", 46f, 16f, y0 - y1);
            Floor("G Ramp foot", 62f, 82f, y1);

            // ----- 3. Jumps: a single gap, then a double (or two hops by the stepping stone on the right).
            Section("Jumps", 72f, y1, "Two short hops by the stepping stone on the right", "A single jump, then jump and double jump straight over");
            Cue(LabCueKind.Jump, 81.2f);
            Beat(CourseBeatKind.Gap, "The single gap", 81.2f, 5.5f);
            Floor("G Jump landing 1", 87.5f, 100f, y1);
            Cue(LabCueKind.Jump, 99.2f);
            Cue(LabCueKind.DoubleJumpAtApex, 100f);
            Beat(CourseBeatKind.DoubleGap, "The double gap", 99.2f, 8.5f);
            Stone("G Stepping stone (double gap)", 103f, 105.5f, y1, 3f, 7f);
            Floor("G Jump landing 2", 108.5f, 136f, y1);
            Marker("Over the gaps", 112f, y1);

            // ----- 4. Wallrun: the wall on the right over a pit (safe: stepping stones on the left).
            Section("Wallrun", 114f, y1, "Hop the stepping stones on the left", "Wallrun the striped wall on the right and kick off it", ChapterTeaching);
            Lane(114f, 0f); Lane(124f, 3.6f);
            Wall("G Wallrun wall", 4.5f, 139f, 153f, y1 + 6f);
            Cue(LabCueKind.Jump, 135.2f);
            Cue(LabCueKind.WallJump, 148.5f);
            Beat(CourseBeatKind.Wallrun, "The wallrun wall", 135.2f, 14f, 6f);
            Lane(150f, 3.6f); Lane(156f, 0f);
            Stone("G Wallrun stone 1", 140f, 142.5f, y1, -W, -4f);
            Stone("G Wallrun stone 2", 146f, 148.5f, y1, -W, -4f);
            Stone("G Wallrun stone 3", 152f, 154f, y1, -W, -4f);
            Floor("G Wallrun landing", 154f, 176f, y2);
            Marker("Off the wall", 158f, y2);

            // ----- 5. Wall to wall: kick from a left wall to a right wall over a pit (safe: stones on the far left).
            const float e = 176f;
            Section("Wall to wall", 160f, y2, "Hop the stepping stones on the far left", "Kick off the left wall to the right one, run it, kick and double jump");
            Lane(e - 16f, 0f); Lane(e - 8f, -1.95f);
            Wall("G Alley wall (left)", -2.5f, e + 1f, e + 13f, y2 + 6f);
            Wall("G Alley wall (right)", 2.5f, e + 7f, e + 25f, y2 + 6f);
            Cue(LabCueKind.Jump, e - 0.8f);
            Cue(LabCueKind.WallJump, e + 1.6f);
            Cue(LabCueKind.WallJump, e + 20.5f);
            Cue(LabCueKind.DoubleJumpAtApex, e + 21f);
            Lane(e + 6f, -1.95f); Lane(e + 10f, 2.1f); Lane(e + 22f, 2.1f); Lane(e + 28f, 0f);
            Beat(CourseBeatKind.WallToWall, "The alley", e - 0.8f, 5f);
            Beat(CourseBeatKind.Wallrun, "The alley's right wall", e + 1.6f, 18f, 6f);
            Stone("G Alley stone 1", 179f, 182f, y2, -W - 1f, -6f);
            Stone("G Alley stone 2", 186f, 189f, y2, -W - 1f, -6f);
            Stone("G Alley stone 3", 193f, 196f, y2, -W - 1f, -6f);
            Stone("G Alley stone 4", 199f, 201f, y2, -W - 1f, -6f);
            Floor("G Alley landing", 202f, 222f, y3);
            Marker("Through the alley", 206f, y3);

            // ----- 6. The climb: the chimney (fast) beside a mantle stair of three heights (safe).
            const float ch = 222f;
            Section("The climb", 208f, y3, "The mantle stair on the left: 1.0, 1.35 and 1.65 m", "Kick up the chimney between the two walls");
            Lane(ch - 10f, 0f); Lane(ch - 4f, -1.1f);
            Wall("G Chimney left", -1.75f, ch, ch + 28f, y3 + 7.4f);
            Wall("G Chimney right", 1.75f, ch, ch + 28f, y3 + 7.4f);
            Cue(LabCueKind.Jump, ch - 0.8f);
            Cue(LabCueKind.Zigzag, ch, ch + 29f, 1.15f);
            Lane(ch + 29f, 0f);
            Beat(CourseBeatKind.Chimney, "The chimney", ch - 0.8f, 3.5f, top - y3);
            Block("G Mantle stair 1.0 m", ch, ch + 6f, y3 + 1f, -9.5f, -4f);
            Block("G Mantle stair 1.35 m", ch + 6f, ch + 12f, y3 + 2.35f, -9.5f, -4f);
            Block("G Mantle stair 1.65 m", ch + 12f, ch + 36f, top, -9.5f, -4f);
            Beat(CourseBeatKind.Mantle, "Mantle stair, 1.0 m", ch, 0f, 1f);
            Beat(CourseBeatKind.Mantle, "Mantle stair, 1.35 m", ch + 6f, 0f, 1.35f);
            Beat(CourseBeatKind.Mantle, "Mantle stair, 1.65 m", ch + 12f, 0f, 1.65f);
            Block("G Top deck", ch + 28f, 284f, top);
            Marker("On top", 258f, top, -3.5f, 3.5f);

            // The top deck runs 10 m longer than the chimney needs: you can leave the chimney at the 22 m/s cap, and need room to line up.
            const float o = 10f;
            // ----- 7. Combo: wallrun, kick, wallrun, slide, mantle (safe: stones on the far left, then the same slide).
            const float e3 = (274f + o);
            Section("Combo: walls, slide, mantle", (262f + o), top, "Hop the stones on the far left, then slide down and mantle", "Right wall, kick to the left wall, kick, land sliding down the ramp, mantle the ledge", ChapterCombo);
            // Mirrored from section 5 (right wall first): the chimney throws you out fast and to the right, so the first wall is where you land.
            Lane(e3 - 16f, 0f); Lane(e3 - 8f, 1.95f);
            Wall("G Combo wall (right)", 2.5f, e3 + 1f, e3 + 13f, top + 6f);
            Wall("G Combo wall (left)", -2.5f, e3 + 7f, e3 + 25f, top + 6f);
            Cue(LabCueKind.Jump, e3 - 0.8f);
            Cue(LabCueKind.WallJump, e3 + 1.6f);
            Cue(LabCueKind.WallJump, e3 + 20.5f);
            Cue(LabCueKind.DoubleJumpAtApex, e3 + 21f);
            Lane(e3 + 6f, 1.95f); Lane(e3 + 10f, -2.1f); Lane(e3 + 22f, -2.1f); Lane(e3 + 28f, 0f);
            Beat(CourseBeatKind.WallToWall, "The combo walls", e3 - 0.8f, 5f);
            Stone("G Combo stone 1", (277f + o), (280.5f + o), top, -W - 1.5f, -6f);
            Stone("G Combo stone 2", (284f + o), (287.5f + o), top, -W - 1.5f, -6f);
            Stone("G Combo stone 3", (291f + o), (294.5f + o), top, -W - 1.5f, -6f);
            float r0 = top - 1f, r1 = top - 4f;
            Ramp("G Combo slide ramp", (300f + o), (316f + o), r0, r1, -W - 1.5f, W);
            Cue(LabCueKind.Crouch, (299.5f + o), (318f + o));
            Beat(CourseBeatKind.SlideRamp, "The combo slide ramp", (300f + o), 16f, r0 - r1);
            Floor("G Combo ramp foot", (316f + o), (330f + o), r1);
            float ledge = r1 + 1.35f;
            Block("G Combo ledge (1.35 m)", (330f + o), (352f + o), ledge);
            Beat(CourseBeatKind.Mantle, "The combo ledge", (330f + o), 0f, 1.35f);
            Marker("On the ledge", (336f + o), ledge);

            // ----- 8. Combo: slide-hops down three steps, two mantles back up; above them, the skip for momentum.
            Section("Combo: slide-hops (or the skip)", (338f + o), ledge, "Slide down the steps (or run them), mantle back up", "Slide-hop the steps without leaving the slide; experts take the skip on the right: a 12.5 m gap that only a slide boost, slide-hops or air strafing clear");
            float h0 = ledge - 3f, h1 = h0 - 0.5f, h2 = h1 - 0.5f;
            Ramp("G Hop ramp", (352f + o), (368f + o), ledge, h0, -W, 3f);
            Floor("G Hop step 1", (368f + o), (372f + o), h0, -W, 3f);
            Floor("G Hop step 2", (376f + o), (384f + o), h1, -W, 3f);
            Floor("G Hop step 3", (388f + o), (398f + o), h2, -W, 3f);
            Cue(LabCueKind.Crouch, (350f + o), (396f + o));
            Cue(LabCueKind.Jump, (371.2f + o));
            Cue(LabCueKind.Jump, (383.2f + o));
            Beat(CourseBeatKind.SlideRamp, "The hop ramp", (352f + o), 16f, 3f);
            Beat(CourseBeatKind.Skip, "The skip", (352f + o), 12.5f);
            Beat(CourseBeatKind.SlideHop, "Slide-hop 1", (371.2f + o), 4f);
            Beat(CourseBeatKind.SlideHop, "Slide-hop 2", (383.2f + o), 4f);
            float m1 = h2 + 1.35f, merge = m1 + 1.35f;
            Block("G Hop mantle 1 (1.35 m)", (398f + o), (404f + o), m1);
            Beat(CourseBeatKind.Mantle, "Back up, 1.35 m", (398f + o), 0f, 1.35f);
            Block("G Merge deck", (404f + o), (433f + o), merge);
            Beat(CourseBeatKind.Mantle, "Back up again, 1.35 m", (404f + o), 0f, 1.35f);
            Block("G Skip deck (experts)", (364.5f + o), (404f + o), ledge, 4f, W + 1f);
            Marker("Back on the deck", (412f + o), merge, -3f, W);

            // ----- 9. The finale: the long wall on the left, kick and double jump down onto the finish (safe: stones on the right).
            const float wl = (434f + o);
            Section("Finale", (416f + o), merge, "Hop the stones on the right", "Run the long wall, kick off and double jump down to the finish", ChapterFinish);
            Lane(wl - 23f, 0f); Lane(wl - 11f, -3.6f);
            Wall("G Long wall", -4.5f, wl, wl + 21f, merge + 6.5f);
            Cue(LabCueKind.Jump, wl - 2.4f);
            Cue(LabCueKind.WallJump, wl + 12f);
            Cue(LabCueKind.DoubleJumpAtApex, wl + 12.5f);
            Lane(wl + 14f, -3.6f); Lane(wl + 20f, 0f);
            Beat(CourseBeatKind.Wallrun, "The long wall", wl - 2.4f, 21f, 6.5f);
            Stone("G Finale stone 1", (437f + o), (440.5f + o), merge, 5f, W + 1f);
            Stone("G Finale stone 2", (444f + o), (447.5f + o), merge, 5f, W + 1f);
            Stone("G Finale stone 3", (451f + o), (454.5f + o), merge, 5f, W + 1f);
            float fin = merge - 1f;
            finishFloorY = fin;
            Block("G Finish platform", (456.5f + o), (490f + o), fin, -W - 1f, W + 1f);
            Marker("Down from the wall", (459f + o), fin, -4f, 4f);
            finishS = Z((463f + o));
            sections[sections.Count - 1].endS = finishS;
            c.sections.Add(new KeyValuePair<float, string>(finishS, "Gauntlet: finished"));
            lowestFloor = h2;
        }
    }
}
