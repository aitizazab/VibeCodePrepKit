using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Movement
{
    /// <summary>What a dream-tool target does when it's hit: a glass tile pops, a socket unfolds a platform or a wall,
    /// a switch flips which colour is solid.</summary>
    public enum DreamTargetKind { Tile, Socket, Switch }

    /// <summary>How a dynamic piece decides whether it's solid: a tile until its target is hit, a built piece once it is,
    /// a pink or blue piece while its colour is the active one.</summary>
    public enum DreamPieceRole { Tile, Built, Pink, Blue }

    /// <summary>Something the dream tool can hit (aim at its <see cref="center"/>; <see cref="radius"/> is its size).</summary>
    public sealed class DreamToolTarget
    {
        public string name;
        public DreamTargetKind kind;
        public Vector3 center;
        public float radius = 0.7f;
        public int section;
        /// <summary>The optional expert shot (a shortcut): never needed to finish.</summary>
        public bool expert;
        /// <summary>A respawn at or before this puts the target back (the nearest of the target and the pieces it changes).</summary>
        public float resetS;
        public float S => center.Z;
    }

    /// <summary>A piece whose solidity the dream tool changes (built by the lab as its own view, not a <see cref="LabPiece"/>).</summary>
    public sealed class DreamToolPiece
    {
        public string name;
        public DreamPieceRole role;
        /// <summary>The tile or socket target that changes it (-1 for the colour pieces).</summary>
        public int target = -1;
        public Vector3 min, max;
        public bool runnable;
        /// <summary>How a built piece appears: false unfolds along +Z from its near end (a bridge), true rises from below (a wall).</summary>
        public bool rises;
        public int section;

        public Vector3 Center => (min + max) * 0.5f;
        public Vector3 Size => max - min;
    }

    /// <summary>When the course autopilot fires the tool: at <see cref="s"/> along the route, at <see cref="target"/>
    /// (a switch only if the active colour isn't <see cref="colour"/> yet).</summary>
    public sealed class DreamToolCue
    {
        public int target;
        public float s;
        public int colour = -1;
    }

    /// <summary>
    /// The Movement Lab's dream tool chapter (docs/MOVEMENT.md "Dream tool prototype"): a one-button tool on top of the
    /// movement. Nadia points and a pulse of light pops glass, unfolds the dream or flips it ("the dream bends to her
    /// will"). Three timed sections after the Gauntlet, each a few short beats with a safe line (stop, then shoot) and a
    /// fast line (shoot on the move): <b>Clear the way</b> (pastel glass tiles fill the doorways ahead: shoot them on a
    /// grounded sprint, at the top of a jump, during a long wallrun glide); <b>Build the way</b> (sockets unfold a bridge
    /// and a runnable wall over gaps nothing else crosses, plus the expert's optional shot during a wallrun that raises a
    /// second wall past the mantle stair); <b>Running puzzle</b> (pink and blue platforms, only one colour solid: switches
    /// flip it, like Night 3's badge flip; flip on the deck, again mid-glide, again at the top of a drop).
    /// Required shots come where the movement asks nothing else of you (a shot replaces an input instead of stacking on
    /// one), never during a wall kick or air strafing.
    /// It appends to the Gauntlet's course (one route along +Z, s = z) from <see cref="Origin"/>, with its own timed run
    /// (<see cref="run"/>: a <see cref="GauntletCourse.Chapter"/> that a <see cref="GauntletClock"/> runs: gate, splits,
    /// finish, best). Tiles, built pieces and the coloured platforms are <see cref="pieces"/>, solid or not by
    /// <see cref="DreamToolState"/>; the course autopilot fires the tool from <see cref="pilotCues"/>.
    /// </summary>
    public sealed class DreamToolCourse
    {
        /// <summary>The gate. The chapter's stretch starts at <see cref="GauntletCourse.End"/> (a walkway from the Gauntlet's finish).</summary>
        public const float Origin = GauntletCourse.End + 24f;
        public const string Chapter = "Dream tool";
        public const int Pink = 0, Blue = 1;
        /// <summary>The course autopilot's clean run (the expert line) at 60 fps in the dotnet sim: a test driver's time, for
        /// tests and logs only (the board shows the developer's real best from <see cref="DeveloperRecords"/>),
        /// pinned by a test: change it when the course changes.</summary>
        public const float AutopilotTime = 44.0f;

        public readonly GauntletCourse gauntlet;
        /// <summary>The chapter's own timed run (no touch markers): its gate, sections and finish.</summary>
        public readonly GauntletCourse run;
        public LabCourse course => gauntlet.course;
        public readonly List<DreamToolTarget> targets = new List<DreamToolTarget>();
        public readonly List<DreamToolPiece> pieces = new List<DreamToolPiece>();
        public readonly List<DreamToolCue> pilotCues = new List<DreamToolCue>();
        public readonly List<CourseBeat> beats = new List<CourseBeat>();
        /// <summary>Each socket's gap as it is before it's built (target index, metres): nothing but the build crosses it.</summary>
        public readonly List<KeyValuePair<int, float>> unbuiltGaps = new List<KeyValuePair<int, float>>();
        /// <summary>True: the autopilot takes the expert's optional shot and its wall (the default). False: the main line.</summary>
        public readonly bool expertLine;
        public int startCheckpoint, firstCheckpoint;
        /// <summary>The first course piece of the chapter (the frames, floors and walls; the Gauntlet's come before).</summary>
        public int firstPiece;
        public float startFloorY, finishFloorY, lowestFloor;
        readonly List<int> _checkpointColour = new List<int>();

        DreamToolCourse(GauntletCourse g, bool expert)
        {
            gauntlet = g;
            run = GauntletCourse.Chapter(g.course);
            expertLine = expert;
        }

        /// <summary>The lab's whole course: the old course, the Gauntlet, then the dream tool chapter.</summary>
        public static DreamToolCourse Build(bool expertLine = true)
        {
            var d = new DreamToolCourse(GauntletCourse.Build(), expertLine);
            d.Make();
            return d;
        }

        public static bool InChapter(float s) => s >= GauntletCourse.End;

        /// <summary>The colour that's solid when you're put at this checkpoint (pink everywhere but the puzzle's later checkpoints).</summary>
        public int ColourAt(int checkpoint)
        {
            int i = checkpoint - firstCheckpoint;
            return i >= 0 && i < _checkpointColour.Count ? _checkpointColour[i] : Pink;
        }

        public int RequiredCount
        {
            get { int n = 0; foreach (var t in targets) if (!t.expert) n++; return n; }
        }

        public List<string> CheckReach(MoveDef def, float skin = 0.04f, float crouchStepOffset = 0f) => CourseReach.Check(beats, def, skin, crouchStepOffset);

        // ---------- Building ----------

        const float W = 8f;
        const float Deep = -24f;
        static float Z(float rel) => Origin + rel;
        int _section;

        LabPiece Add(string name, LabPieceKind kind, Vector3 min, Vector3 max, bool runnable = false)
        {
            var p = new LabPiece { name = name, kind = kind, center = (min + max) * 0.5f, size = max - min, runnable = runnable, section = "Dream tool" };
            course.pieces.Add(p);
            return p;
        }

        LabPiece Floor(string name, float z0, float z1, float top, float x0 = -W, float x1 = W) =>
            Add(name, LabPieceKind.Floor, new Vector3(x0, top - 2f, Z(z0)), new Vector3(x1, top, Z(z1)));

        LabPiece Block(string name, float z0, float z1, float top, float x0 = -W, float x1 = W) =>
            Add(name, LabPieceKind.Ledge, new Vector3(x0, Deep, Z(z0)), new Vector3(x1, top, Z(z1)));

        LabPiece Stone(string name, float z0, float z1, float top, float x0, float x1) =>
            Add(name, LabPieceKind.Floor, new Vector3(x0, Deep, Z(z0)), new Vector3(x1, top, Z(z1)));

        LabPiece Wall(string name, float faceX, float z0, float z1, float top)
        {
            float x0 = faceX > 0f ? faceX : faceX - 1f;
            return Add(name, LabPieceKind.Wall, new Vector3(x0, Deep, Z(z0)), new Vector3(x0 + 1f, top, Z(z1)), true);
        }

        void Cue(LabCueKind kind, float s, float sEnd = 0f, float x = 0f) =>
            course.cues.Add(new LabCue { kind = kind, s = Z(s), sEnd = sEnd != 0f ? Z(sEnd) : 0f, x = x });

        void Lane(float s, float x) => course.lane.Add(new Vector2(Z(s), x));

        void Beat(CourseBeatKind kind, string name, float s, float size, float height = 0f) =>
            beats.Add(new CourseBeat { kind = kind, name = name, s = Z(s), size = size, height = height });

        void Checkpoint(string name, float rel, float floorTop, int colour, string chapter = "")
        {
            course.checkpoints.Add(new LabCheckpoint { name = name, chapter = chapter, position = new Vector3(course.LaneAt(Z(rel)), floorTop + 0.02f, Z(rel)), yaw = 0f, s = Z(rel) });
            _checkpointColour.Add(colour);
        }

        void Section(string name, float startRel, float floorTop, int colour, string safe, string fast)
        {
            if (run.sections.Count > 0) run.sections[run.sections.Count - 1].endS = Z(startRel);
            int cp = course.checkpoints.Count;
            Checkpoint("Dream tool: " + name, startRel, floorTop, colour);
            run.sections.Add(new GauntletSection { name = name, startS = Z(startRel), checkpoint = cp, safeLine = safe, fastLine = fast });
            course.sections.Add(new KeyValuePair<float, string>(Z(startRel), "Dream tool: " + name));
            _section = run.sections.Count - 1;
        }

        int Target(string name, DreamTargetKind kind, Vector3 center, float radius, bool expert = false)
        {
            targets.Add(new DreamToolTarget { name = name, kind = kind, center = center, radius = radius, section = _section, expert = expert, resetS = center.Z });
            return targets.Count - 1;
        }

        DreamToolPiece Piece(string name, DreamPieceRole role, int target, Vector3 min, Vector3 max, bool runnable = false, bool rises = false)
        {
            var p = new DreamToolPiece { name = name, role = role, target = target, min = min, max = max, runnable = runnable, rises = rises, section = _section };
            pieces.Add(p);
            if (target >= 0 && min.Z < targets[target].resetS) targets[target].resetS = min.Z;
            return p;
        }

        /// <summary>The autopilot's shot at <paramref name="target"/> from <paramref name="s"/> (relative).</summary>
        void Shoot(int target, float s, int colour = -1) => pilotCues.Add(new DreamToolCue { target = target, s = Z(s), colour = colour });

        /// <summary>A wall across the lane with a doorway (x -2..2, 3.6 m high) filled by a pastel glass tile: shoot it out.</summary>
        int Doorway(string name, float rel, float floorTop)
        {
            float z0 = Z(rel) - 0.3f, z1 = Z(rel) + 0.3f;
            const float door = 2f, high = 3.6f, top = 7f;
            Add("D " + name + " (frame, left)", LabPieceKind.Fold, new Vector3(-W - 1.5f, floorTop - 0.5f, z0), new Vector3(-door, floorTop + top, z1));
            Add("D " + name + " (frame, right)", LabPieceKind.Fold, new Vector3(door, floorTop - 0.5f, z0), new Vector3(W + 1.5f, floorTop + top, z1));
            Add("D " + name + " (frame, lintel)", LabPieceKind.Fold, new Vector3(-door, floorTop + high, z0), new Vector3(door, floorTop + top, z1));
            int t = Target(name, DreamTargetKind.Tile, new Vector3(0f, floorTop + high * 0.5f, Z(rel)), 1.9f);
            Piece(name, DreamPieceRole.Tile, t, new Vector3(-door, floorTop, Z(rel) - 0.2f), new Vector3(door, floorTop + high, Z(rel) + 0.2f));
            return t;
        }

        /// <summary>A coloured slab (pink or blue), solid only while its colour is the active one.</summary>
        void Coloured(string name, int colour, float z0, float z1, float top, float x0 = -6f, float x1 = 6f) =>
            Piece(name, colour == Pink ? DreamPieceRole.Pink : DreamPieceRole.Blue, -1, new Vector3(x0, top - 1f, Z(z0)), new Vector3(x1, top, Z(z1)));

        void Make()
        {
            var c = course;
            var g = gauntlet;
            firstPiece = c.pieces.Count;
            firstCheckpoint = c.checkpoints.Count;
            c.route = new PathLine(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, Origin + 600f) }); // s = z

            // The levels: the Gauntlet's finish height, a metre down after each wallrun (3.6 m after the long wall, so the expert
            // wall has air under it), back up the mantle stair to the puzzle deck.
            float y0 = g.finishFloorY, y1 = y0 - 1f, y2 = y1 - 1f, y3 = y2 - 3.6f, yD = y3 + 3.6f;
            startFloorY = y0;
            float gEnd = GauntletCourse.End - 20f;
            foreach (var p in c.pieces) if (p.name == "G Finish platform") gEnd = p.Max.Z;

            // ===== The walkway from the Gauntlet's finish and the start pad; the gate starts the chapter's clock.
            Lane(Math.Min(gEnd, -30f + Origin) - Origin, 0f);
            c.sections.Add(new KeyValuePair<float, string>(GauntletCourse.End, "Dream tool: start pad"));
            Checkpoint(Chapter, -8f, y0, Pink, Chapter);
            startCheckpoint = c.checkpoints.Count - 1;
            run.startCheckpoint = startCheckpoint;
            run.gateS = Z(0f);
            run.startFloorY = y0;
            Floor("D Walkway and start pad", Math.Min(gEnd - Origin, -30f), 52f, y0);

            // ----- 1. Clear the way: glass tiles fill the doorways. Shoot each on a calm moment: a grounded sprint (seen
            // from the start pad), the top of a jump, a long wallrun glide. Safe line: stop at the glass and shoot it.
            run.sections.Add(new GauntletSection { name = "Clear the way", startS = run.gateS, checkpoint = startCheckpoint,
                safeLine = "Stop at each pane and shoot it point-blank", fastLine = "Shoot each pane from 20 m out without breaking stride: on the sprint, at the top of the jump, during the wallrun glide" });
            _section = 0;
            c.sections.Add(new KeyValuePair<float, string>(run.gateS, "Dream tool: Clear the way"));
            int pane1 = Doorway("Glass doorway 1", 36f, y0);
            Shoot(pane1, 14f);                                             // a grounded sprint, 22 m out
            Cue(LabCueKind.Jump, 51.2f);
            Beat(CourseBeatKind.Gap, "The jump before the second pane", 51.2f, 5.5f);
            Floor("D Clear landing", 57.5f, 96f, y0);
            int pane2 = Doorway("Glass doorway 2", 72f, y0);
            Shoot(pane2, 54f);                                             // the top of the jump, 18 m out
            Checkpoint("Dream tool: the glide", 76f, y0, Pink);
            Lane(74f, 0f); Lane(84f, 3.6f);
            Cue(LabCueKind.Jump, 95.2f);
            Wall("D Glide wall", 4.5f, 99f, 113f, y0 + 6f);
            Cue(LabCueKind.WallJump, 108.5f);
            Beat(CourseBeatKind.Wallrun, "The glide wall", 95.2f, 14f, 6f);
            Lane(110f, 3.6f); Lane(116f, 0f);
            Stone("D Glide stone 1", 100f, 102.5f, y0, -W, -4f);
            Stone("D Glide stone 2", 106f, 108.5f, y0, -W, -4f);
            Stone("D Glide stone 3", 112f, 114f, y0, -W, -4f);
            Floor("D Glide landing", 114f, 150f, y1);
            int pane3 = Doorway("Glass doorway 3", 128f, y1);
            Shoot(pane3, 103f);                                            // mid-glide on the wall, 25 m out

            // ----- 2. Build the way: a socket unfolds a bridge over a chasm, another raises a runnable wall over a pit
            // (neither crossable otherwise: shoot while you run up to it). Then the expert's optional shot, during a
            // wallrun: a second wall that carries you over the mantle stair.
            Section("Build the way", 134f, y1, Pink, "Stop at each edge, shoot the socket, then cross; take the mantle stair after the long wall",
                "Shoot each socket from the run-up; experts shoot the far socket during the long wallrun and kick across to the wall it raises");
            int bridge = Target("Bridge socket", DreamTargetKind.Socket, new Vector3(0f, y1 + 4f, Z(168.4f)), 0.75f);
            Piece("Bridge", DreamPieceRole.Built, bridge, new Vector3(-3f, y1 - 0.5f, Z(150f)), new Vector3(3f, y1, Z(168f)));
            unbuiltGaps.Add(new KeyValuePair<int, float>(bridge, 168f - 150f));
            Shoot(bridge, 140f);                                           // the run-up, 28 m out
            Floor("D Bridge far side", 168f, 196f, y1);
            Checkpoint("Dream tool: the rising wall", 170f, y1, Pink);
            Lane(174f, 0f); Lane(184f, 3.6f);
            int wallSocket = Target("Wall socket", DreamTargetKind.Socket, new Vector3(5f, y1 + 3f, Z(198.2f)), 0.75f);
            Piece("Rising wall", DreamPieceRole.Built, wallSocket, new Vector3(4.5f, Deep, Z(199f)), new Vector3(5.5f, y1 + 6f, Z(213f)), true, true);
            unbuiltGaps.Add(new KeyValuePair<int, float>(wallSocket, 214f - 196f));
            Shoot(wallSocket, 178f);                                       // the run-up, 20 m out
            Cue(LabCueKind.Jump, 195.2f);
            Cue(LabCueKind.WallJump, 208.5f);
            Beat(CourseBeatKind.Wallrun, "The rising wall", 195.2f, 14f, 6f);
            Lane(210f, 3.6f); Lane(216f, 0f);
            Floor("D Rising wall landing", 214f, 238f, y2);
            Checkpoint("Dream tool: the long wall", 218f, y2, Pink);
            // The long wall, then (main line) a landing and the mantle stair, or (experts) the raised wall over them.
            Lane(226f, 3.6f);
            Cue(LabCueKind.Jump, 237.2f);
            Wall("D Long wall", 4.5f, 241f, 255f, y2 + 6f);
            Cue(LabCueKind.WallJump, 250.5f);
            Beat(CourseBeatKind.Wallrun, "The long wall", 237.2f, 14f, 6f);
            int expert = Target("Expert socket", DreamTargetKind.Socket, new Vector3(0f, y2 + 6.8f, Z(272.6f)), 0.75f, true); // over the far end of the wall it raises
            Piece("Expert wall", DreamPieceRole.Built, expert, new Vector3(-0.5f, Deep, Z(254f)), new Vector3(0.5f, y2 + 6f, Z(272f)), true, true);
            Floor("D Long wall landing", 256f, 274f, y3);
            Block("D Mantle step 1 (1.2 m)", 274f, 280f, y3 + 1.2f);
            Block("D Mantle step 2 (1.2 m)", 280f, 286f, y3 + 2.4f);
            Beat(CourseBeatKind.Mantle, "The second mantle step", 280f, 0f, 1.2f);
            Beat(CourseBeatKind.Mantle, "The mantle step", 274f, 0f, 1.2f);
            Block("D Puzzle deck", 286f, 318f, yD);
            Beat(CourseBeatKind.Mantle, "Up onto the deck", 286f, 0f, 1.2f);
            if (expertLine)
            {
                Shoot(expert, 244f);                                       // on the long wall: the expert's stacked shot
                Lane(252f, 3.6f); Lane(256f, 1.05f); Lane(268f, 1.05f); Lane(274f, 0f);
                Cue(LabCueKind.WallJump, 268f);
                Cue(LabCueKind.DoubleJumpAtApex, 268.5f);
            }
            else
            {
                Lane(252f, 3.6f); Lane(258f, 0f);
            }
            Beat(CourseBeatKind.WallToWall, "The expert's walls", 250.5f, 4f);
            Beat(CourseBeatKind.Wallrun, "The expert wall", 250.5f, 18f, 6f);

            // ----- 3. Running puzzle: pink and blue platforms, one colour solid at a time; a switch flips it. Flip on the
            // deck (the pink barrier goes, the blue steps come), again mid-glide on the neutral wall (the pink landing
            // comes), again at the top of the drop (the blue landing). Grey is always solid: the safe line flips there.
            Section("Running puzzle", 288f, yD, Pink, "Flip while standing on grey: the deck, the stones beside the wall, the stone beside the drop",
                "Flip on the move: on the deck, mid-glide on the wall, at the top of the drop");
            Piece("Pink barrier", DreamPieceRole.Pink, -1, new Vector3(-W - 1.5f, yD - 0.5f, Z(307.7f)), new Vector3(W + 1.5f, yD + 7f, Z(308.3f)));
            int flip1 = Target("Switch 1", DreamTargetKind.Switch, new Vector3(0f, yD + 5f, Z(305f)), 0.9f); // in front of the barrier, above the lane
            Shoot(flip1, 292f, Blue);                                      // a grounded sprint on the deck, 18 m out
            Cue(LabCueKind.Jump, 317.2f);
            Beat(CourseBeatKind.Gap, "Onto the blue", 317.2f, 4f);
            Coloured("Blue step 1", Blue, 322f, 338f, yD);
            Checkpoint("Dream tool: on blue", 326f, yD, Blue);
            Cue(LabCueKind.Jump, 337.2f);
            Beat(CourseBeatKind.Gap, "The second blue step", 337.2f, 4f);
            Coloured("Blue step 2", Blue, 342f, 356f, yD);
            Lane(334f, 0f); Lane(344f, 3.6f);
            Cue(LabCueKind.Jump, 355.2f);
            Wall("D Puzzle wall", 4.5f, 359f, 373f, yD + 6f);
            Cue(LabCueKind.WallJump, 368.5f);
            Beat(CourseBeatKind.Wallrun, "The puzzle wall", 355.2f, 14f, 6f);
            Lane(370f, 3.6f); Lane(376f, 0f);
            Stone("D Puzzle stone 1", 360f, 362.5f, yD, -W, -4f);
            Stone("D Puzzle stone 2", 366f, 368.5f, yD, -W, -4f);
            Stone("D Puzzle stone 3", 372f, 374f, yD, -W, -4f);
            int flip2 = Target("Switch 2", DreamTargetKind.Switch, new Vector3(1.5f, yD + 3.5f, Z(380f)), 0.9f);
            Shoot(flip2, 362f, Pink);                                      // mid-glide on the neutral wall, 18 m out
            Coloured("Pink landing", Pink, 374f, 396f, yD - 1f);
            Checkpoint("Dream tool: on pink", 378f, yD - 1f, Pink);
            Cue(LabCueKind.Jump, 395.2f);
            Beat(CourseBeatKind.Gap, "The drop to blue", 395.2f, 5.5f);
            Stone("D Drop stone", 396.5f, 400f, yD - 2f, -W - 1f, -6.5f);
            int flip3 = Target("Switch 3", DreamTargetKind.Switch, new Vector3(0f, yD + 1.5f, Z(414f)), 0.9f);
            Shoot(flip3, 397f, Blue);                                      // the top of the drop, 17 m out
            Coloured("Blue landing", Blue, 401.5f, 424f, yD - 3f);
            finishFloorY = yD - 3f;
            Block("D Finish platform", 424f, 460f, finishFloorY, -W - 1f, W + 1f);
            run.finishS = Z(432f);
            run.finishFloorY = finishFloorY;
            run.sections[run.sections.Count - 1].endS = run.finishS;
            c.sections.Add(new KeyValuePair<float, string>(run.finishS, "Dream tool: finished"));
            lowestFloor = Math.Min(finishFloorY, y3);
            beats.Sort((a, b) => a.s.CompareTo(b.s));
        }
    }
}
