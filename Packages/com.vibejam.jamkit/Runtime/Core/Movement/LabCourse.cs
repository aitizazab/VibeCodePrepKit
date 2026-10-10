using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Movement
{
    public enum LabPieceKind { Floor, Ramp, Wall, Bar, Ledge, Fold, Sign }

    /// <summary>One grey-box piece: an axis-aligned box (a ramp's top slopes along +Z from
    /// <see cref="rampTopStart"/> to <see cref="rampTopEnd"/>).</summary>
    public sealed class LabPiece
    {
        public string name;
        public LabPieceKind kind;
        public Vector3 center, size;
        public bool runnable;
        /// <summary>-1, or the collapse that drops it.</summary>
        public int collapse = -1;
        /// <summary>False for decoration (a fold wall has no collider).</summary>
        public bool solid = true;
        public float rampTopStart, rampTopEnd;
        public string section;

        public Vector3 Min => center - size * 0.5f;
        public Vector3 Max => center + size * 0.5f;
    }

    public sealed class LabCheckpoint
    {
        public string name;
        /// <summary>The presenter chapter that starts here ("" if none).</summary>
        public string chapter = "";
        public Vector3 position;
        public float yaw;
        public float s;
    }

    /// <summary>A scripted collapse: when the player passes <see cref="triggerS"/>, the pieces with this index fall.</summary>
    public sealed class LabCollapse
    {
        public string name;
        public float triggerS;
    }

    public enum LabCueKind { Jump, Crouch, DoubleJumpAtApex, WallJump, Zigzag }

    /// <summary>An autopilot instruction keyed by distance along the route.</summary>
    public sealed class LabCue
    {
        public LabCueKind kind;
        public float s, sEnd;
        /// <summary>Zigzag: the lateral offset of the target near each wall.</summary>
        public float x;
    }

    /// <summary>
    /// The Movement Lab's course as pure data: the grey-box pieces, the route, checkpoints (and the
    /// presenter chapters on them), the chase (pursuer, two collapses), and the cues and lane the
    /// autopilot follows. The Unity builder turns the pieces into boxes; the dotnet tests run the
    /// autopilot through the same pieces in a fake world, so the course is proven finishable before
    /// Unity opens. Gap and wall sizes follow docs/RESEARCH-MOVEMENT.md 4.3 (single jump about 6 m,
    /// double about 10 m, wallrun walls up to 21 m). The route runs along +Z, so s is z.
    /// </summary>
    public sealed class LabCourse
    {
        public const string ChapterStart = "Course start";
        public const string ChapterWallruns = "Wallrun section";
        public const string ChapterChain = "Momentum chain";
        public const string ChapterChimney = "Chimney";
        public const string ChapterChase = "Chase";

        public readonly List<LabPiece> pieces = new List<LabPiece>();
        public readonly List<LabCheckpoint> checkpoints = new List<LabCheckpoint>();
        public readonly List<LabCollapse> collapses = new List<LabCollapse>();
        public readonly List<LabCue> cues = new List<LabCue>();
        /// <summary>The autopilot's lane: (s, lateral x) points, linear between.</summary>
        public readonly List<Vector2> lane = new List<Vector2>();
        /// <summary>Section names by where they start (s), for the HUD.</summary>
        public readonly List<KeyValuePair<float, string>> sections = new List<KeyValuePair<float, string>>();
        public PathLine route;
        public float chaseStartS, chaseEndS, finishS;
        public float killY = -22f;
        public Pursuer pursuer = new Pursuer();

        public const float Half = 4f; // the main line is 8 m wide

        // ---------- Queries ----------

        public float LaneAt(float s)
        {
            if (lane.Count == 0) return 0f;
            if (s <= lane[0].X) return lane[0].Y;
            for (int i = 0; i + 1 < lane.Count; i++)
            {
                if (s > lane[i + 1].X) continue;
                float t = (s - lane[i].X) / Math.Max(1e-4f, lane[i + 1].X - lane[i].X);
                return lane[i].Y + (lane[i + 1].Y - lane[i].Y) * t;
            }
            return lane[lane.Count - 1].Y;
        }

        public string SectionAt(float s)
        {
            string name = sections.Count > 0 ? sections[0].Value : "";
            for (int i = 0; i < sections.Count; i++)
                if (s >= sections[i].Key) name = sections[i].Value;
            return name;
        }

        public int CheckpointIndex(string chapter)
        {
            for (int i = 0; i < checkpoints.Count; i++)
                if (checkpoints[i].chapter == chapter) return i;
            return -1;
        }

        /// <summary>The last checkpoint at or before <paramref name="s"/>.</summary>
        public int CheckpointBefore(float s)
        {
            int best = 0;
            for (int i = 0; i < checkpoints.Count; i++)
                if (checkpoints[i].s <= s + 0.01f) best = i;
            return best;
        }

        // ---------- Building ----------

        string _section = "";

        void Section(float s, string name)
        {
            _section = name;
            sections.Add(new KeyValuePair<float, string>(s, name));
        }

        LabPiece Add(string name, LabPieceKind kind, Vector3 min, Vector3 max, bool runnable = false)
        {
            var p = new LabPiece { name = name, kind = kind, center = (min + max) * 0.5f, size = max - min, runnable = runnable, section = _section };
            pieces.Add(p);
            return p;
        }

        /// <summary>A floor slab from z0 to z1 with its top at <paramref name="top"/>, 2 m thick.</summary>
        LabPiece Floor(string name, float z0, float z1, float top, float x0 = -Half, float x1 = Half) =>
            Add(name, LabPieceKind.Floor, new Vector3(x0, top - 2f, z0), new Vector3(x1, top, z1));

        /// <summary>A block whose top is a ledge (its face goes down to <paramref name="bottom"/>).</summary>
        LabPiece Block(string name, float z0, float z1, float top, float bottom, LabPieceKind kind = LabPieceKind.Ledge) =>
            Add(name, kind, new Vector3(-Half, bottom, z0), new Vector3(Half, top, z1));

        /// <summary>A runnable wall, 1 m thick, its face at x = <paramref name="faceX"/> (on the right when faceX &gt; 0).</summary>
        LabPiece Wall(string name, float faceX, float z0, float z1, float bottom, float top)
        {
            float x0 = faceX > 0f ? faceX : faceX - 1f;
            return Add(name, LabPieceKind.Wall, new Vector3(x0, bottom, z0), new Vector3(x0 + 1f, top, z1), true);
        }

        LabPiece Ramp(string name, float z0, float z1, float top0, float top1)
        {
            var p = Add(name, LabPieceKind.Ramp, new Vector3(-Half, Math.Min(top0, top1) - 2f, z0), new Vector3(Half, Math.Max(top0, top1), z1));
            p.rampTopStart = top0;
            p.rampTopEnd = top1;
            return p;
        }

        /// <summary>A bar across the route you can only slide under (its underside 1.15 m above the floor).</summary>
        LabPiece Bar(string name, float z, float floorTop) =>
            Add(name, LabPieceKind.Bar, new Vector3(-Half - 0.5f, floorTop + 1.15f, z - 0.4f), new Vector3(Half + 0.5f, floorTop + 2.2f, z + 0.4f));

        void Checkpoint(string name, float z, float floorTop, string chapter = "", float x = 0f)
        {
            checkpoints.Add(new LabCheckpoint { name = name, chapter = chapter, position = new Vector3(x, floorTop + 0.02f, z), yaw = 0f, s = z });
        }

        void Cue(LabCueKind kind, float s, float sEnd = 0f, float x = 0f) => cues.Add(new LabCue { kind = kind, s = s, sEnd = sEnd, x = x });

        void Lane(float s, float x) => lane.Add(new Vector2(s, x));

        /// <summary>The course. Positions in metres; the player starts at z 2 on y 0.</summary>
        public static LabCourse Build()
        {
            var c = new LabCourse();
            c.Make();
            return c;
        }

        void Make()
        {
            const float low = -3f;     // the floor level after the first slide ramp
            const float deep = -18f;   // bottom of walls over pits
            Lane(0f, 0f);

            // ---- A. Run-up and slide: 40 m to reach sprint, a ramp to slide down, a bar to slide under.
            Section(-6f, "Run-up and slide");
            Floor("Start", -6f, 44f, 0f);
            Checkpoint("Start", 2f, 0f, ChapterStart);
            Ramp("Slide ramp", 44f, 60f, 0f, low);
            Floor("Bar floor", 60f, 74f, low);
            Bar("Slide bar", 67f, low);
            Cue(LabCueKind.Crouch, 42f, 69f);

            // ---- B. Gaps: one jump (5.5 m), then jump + double jump (8.5 m).
            Section(73f, "Gaps");
            Cue(LabCueKind.Jump, 73.2f);
            Floor("Gap landing 1", 79.5f, 94f, low);
            Cue(LabCueKind.Jump, 93.2f);
            Cue(LabCueKind.DoubleJumpAtApex, 94f);
            Floor("Gap landing 2", 102.5f, 138f, low);

            // ---- W. Wallruns of rising length: 8 m over solid floor (safe), 14 m over a pit, 21 m over a pit.
            Section(104f, "Wallruns");
            Checkpoint("Wallruns", 106f, low, ChapterWallruns);
            Lane(106f, 0f); Lane(116f, 3.6f);
            Wall("Wall 8 m (safe)", 4.5f, 124f, 132f, low, low + 6f);
            Cue(LabCueKind.Jump, 121.5f);
            Wall("Wall 14 m", 4.5f, 141f, 155f, deep, low + 6f);
            Cue(LabCueKind.Jump, 137.2f);
            Cue(LabCueKind.WallJump, 150.5f);
            Lane(152f, 3.6f); Lane(158f, 0f);
            Floor("Wall landing 1", 156f, 182f, low);
            Lane(160f, 0f); Lane(172f, -3.6f);
            Wall("Wall 21 m", -4.5f, 183f, 204f, deep, low + 6f);
            Cue(LabCueKind.Jump, 180.6f);
            Cue(LabCueKind.WallJump, 195f);
            Cue(LabCueKind.DoubleJumpAtApex, 195.5f);
            Lane(197f, -3.6f); Lane(203f, 0f);

            // ---- C. The momentum chain: slide -> slide jump -> wallrun -> wall kick -> double jump.
            Section(206f, "Momentum chain");
            Floor("Chain floor", 206f, 224f, low);
            Checkpoint("Chain", 208f, low, ChapterChain, 2.5f);
            Lane(204f, 0.5f); Lane(211f, 3.8f);
            Cue(LabCueKind.Crouch, 212f, 218.6f);
            Cue(LabCueKind.Jump, 218.6f);
            Wall("Kick wall", 4.5f, 225f, 236f, deep, low + 6f);
            Cue(LabCueKind.WallJump, 225.6f);
            Cue(LabCueKind.DoubleJumpAtApex, 226f);
            Lane(228f, 3.8f); Lane(234f, 0f);
            Floor("Chain landing", 238f, 254f, low);

            // ---- M. The chimney: wall jump from side to side to climb 4 m, over a pit.
            Section(242f, "Chimney");
            Checkpoint("Chimney", 242f, low, ChapterChimney);
            Lane(244f, 0f); Lane(250f, -1.1f);
            Wall("Chimney left", -1.75f, 254f, 282f, deep, 4f);
            Wall("Chimney right", 1.75f, 254f, 282f, deep, 4f);
            Cue(LabCueKind.Jump, 253.2f);
            Cue(LabCueKind.Zigzag, 254f, 283f, 1.15f);
            Block("Chimney top", 282f, 290f, 1f, deep);
            Lane(283f, 0f);

            // ---- L. Mantle ledges: 1.0, 1.5 and 1.85 m, run straight into them.
            Section(286f, "Ledges");
            Checkpoint("Ledges", 285f, 1f);
            Block("Ledge 1.0 m", 290f, 296f, 2f, deep);
            Block("Ledge 1.5 m", 296f, 302f, 3.5f, deep);
            const float yc = 5.35f; // chase level
            Block("Ledge 1.85 m", 302f, 328f, yc, deep).collapse = 0;

            // ---- CH. The chase: the dream-static follows; two collapses fire as you pass.
            Section(309f, "Chase");
            chaseStartS = 310f;
            Checkpoint("Chase", 310f, yc, ChapterChase);
            collapses.Add(new LabCollapse { name = "The ledges fall", triggerS = 334f });
            Floor("Chase run", 328f, 348f, yc);
            Ramp("Chase ramp", 348f, 364f, yc, yc - 3f);
            float yr = yc - 3f;
            Floor("Chase bar floor", 364f, 380f, yr);
            Bar("Chase bar", 370f, yr);
            Cue(LabCueKind.Crouch, 346f, 372f);
            Cue(LabCueKind.Jump, 379.2f);
            Floor("Chase landing 1", 385.5f, 400f, yr).collapse = 1;
            Checkpoint("Chase middle", 388f, yr);
            Lane(390f, 0f); Lane(397f, 3.6f);
            Wall("Chase wall", 4.5f, 401f, 416f, deep, yr + 6f).collapse = 1;
            Cue(LabCueKind.Jump, 398.6f);
            Cue(LabCueKind.WallJump, 410f);
            Lane(413f, 3.6f); Lane(418f, 0f);
            Floor("Chase landing 2", 418f, 438f, yr);
            collapses.Add(new LabCollapse { name = "The walls fold in", triggerS = 422f });
            var fl = Add("Fold left", LabPieceKind.Fold, new Vector3(-7f, yr - 2f, 420f), new Vector3(-6f, yr + 9f, 436f));
            fl.solid = false; fl.collapse = 1;
            var fr = Add("Fold right", LabPieceKind.Fold, new Vector3(6f, yr - 2f, 420f), new Vector3(7f, yr + 9f, 436f));
            fr.solid = false; fr.collapse = 1;
            Cue(LabCueKind.Jump, 437.2f);
            Cue(LabCueKind.DoubleJumpAtApex, 438f);
            Floor("Chase landing 3", 446.5f, 464f, yr);
            // The finale: kick from a left wall to a right wall, run it, kick off and double jump to the finish.
            Lane(448f, 0f); Lane(456f, -1.95f); Lane(470f, -1.95f); Lane(474f, 2.1f); Lane(486f, 2.1f); Lane(492f, 0f);
            Wall("Finale left", -2.5f, 465f, 477f, deep, yr + 6f);
            Wall("Finale right", 2.5f, 471f, 489f, deep, yr + 6f);
            Cue(LabCueKind.Jump, 463.2f);
            Cue(LabCueKind.WallJump, 465.6f);
            Cue(LabCueKind.WallJump, 484.5f);
            Cue(LabCueKind.DoubleJumpAtApex, 485f);
            Floor("Finish", 490f, 524f, yr);
            chaseEndS = 500f;
            finishS = 500f;
            Section(490f, "Finish");

            pursuer = new Pursuer { targetGap = 12f, k = 0.6f, minSpeed = 6f, maxSpeed = 11.5f };
            route = new PathLine(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 530f) }); // s = z
        }
    }
}
