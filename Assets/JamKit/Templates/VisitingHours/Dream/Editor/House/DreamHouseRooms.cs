using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Night 2's house, finished room by room (the architecture pass, 2026-10-12): every wall face gets its room's own
    /// wallpaper over wainscoting, a chair rail, a skirting board and (in the taller rooms) a picture rail; the windows
    /// (the bay, the kitchen's, the bedrooms'); the attic stairs drawn as steps; the hatch; the attic's roof with its
    /// rafters and ridge beam, the dormer and the observatory's round window full of stars. All visual: the gameplay boxes
    /// come from core <see cref="DreamHouse"/> and keep their colliders.
    /// </summary>
    public static class DreamHouseRooms
    {
        const float F = DreamHouse.Ground;
        const float Wainscot = 0.9f;

        /// <summary>A room's finish: its wallpaper (a colour tile, tinted by <see cref="paper"/>: white is the tile as drawn),
        /// the wainscot's colour, and whether the wainscot is tiled.</summary>
        sealed class Finish
        {
            public Color paper = Color.white, wainscot = Walnut;
            public bool tiled;
        }

        /// <summary>The creator's picks (round 4, from the finish options): the oxblood regency stripe in every room with a
        /// walnut wainscot (the kitchen's and the bath's tiles stay), exposed beams, the stepped crown, painted boards in
        /// the attic. DreamCaptureVariants still shows the other options.</summary>
        public const string WallTile = "dream_wall_oxblood-stripe";
        public const float WallTileMetres = 0.5f;
        static readonly Color Walnut = new Color(88f / 255f, 60f / 255f, 44f / 255f);

        static readonly Dictionary<string, Finish> Finishes = new Dictionary<string, Finish>
        {
            { "Kitchen and dining", new Finish { wainscot = new Color(0.97f, 0.97f, 0.95f), tiled = true } },
            { "Living room", new Finish() },
            { "Bay window", new Finish() },
            { "Hall", new Finish() },
            { "Pantry", new Finish() },
            { "Parents' room", new Finish() },
            { "Bath", new Finish { wainscot = new Color(0.97f, 0.98f, 0.98f), tiled = true } },
            { "Attic stairs", new Finish() },
            { "Theo's room", new Finish() },
        };

        static Transform _root;

        public static void Build(DreamHouse h, Transform root)
        {
            _root = new GameObject("Rooms (finish)").transform;
            _root.SetParent(root, false);
            WallFaces(h);
            BayBulkhead(h);
            Beams(h);
            Windows();
            Stairs();
            Hatch();
            Attic();
        }

        // ---------- The walls ----------

        static void WallFaces(DreamHouse h)
        {
            var t = Group("Walls");
            for (int i = 0; i < h.course.pieces.Count; i++)
            {
                var look = h.looks[i];
                if (look != HouseLook.Wall && look != HouseLook.Header && look != HouseLook.Chimney) continue;
                var p = h.course.pieces[i];
                if (!p.solid || p.Max.Z < DreamHouse.HubZ0 - 1.5f || p.Min.Z > DreamHouse.HubZ1 + 0.5f || p.Min.Y > DreamHouse.AtticFloor - 0.1f) continue;
                Vector3 mn = new Vector3(p.Min.X, p.Min.Y, p.Min.Z), mx = new Vector3(p.Max.X, p.Max.Y, p.Max.Z);
                // The four vertical faces: (normal, the face's centre line).
                Face(h, t, p.name, mn, mx, Vector3.right, look);
                Face(h, t, p.name, mn, mx, Vector3.left, look);
                Face(h, t, p.name, mn, mx, Vector3.forward, look);
                Face(h, t, p.name, mn, mx, Vector3.back, look);
            }
        }

        static void Face(DreamHouse h, Transform t, string name, Vector3 mn, Vector3 mx, Vector3 n, HouseLook look)
        {
            bool alongZ = Mathf.Abs(n.x) > 0.5f;   // the face runs along z (its normal is x)
            float width = alongZ ? mx.z - mn.z : mx.x - mn.x;
            if (width < 0.12f) return;
            float planeCoord = alongZ ? (n.x > 0f ? mx.x : mn.x) : (n.z > 0f ? mx.z : mn.z);
            // Which rooms does it face? A long wall can face several (the hall's north wall faces the parents' room, the
            // bath and Theo's room from the other side): each room gets its own stretch of the face, in its own finish.
            float probe = planeCoord + (alongZ ? n.x : n.z) * 0.15f;
            foreach (var room in h.rooms)
            {
                if (room.name == "Attic" || !Finishes.TryGetValue(room.name, out var fin)) continue;
                bool across = alongZ ? probe > room.x0 && probe < room.x1 : probe > room.z0 && probe < room.z1;
                if (across) FaceIn(t, name, mn, mx, n, look, alongZ, planeCoord, room, fin);
            }
        }

        static void FaceIn(Transform t, string name, Vector3 mn, Vector3 mx, Vector3 n, HouseLook look, bool alongZ, float planeCoord, HouseRoom room, Finish fin)
        {
            float ceiling = room.floorY + room.ceiling;
            float y0 = Mathf.Max(mn.y, room.floorY), y1 = Mathf.Min(mx.y, ceiling);
            if (y1 - y0 < 0.05f) return;
            // The face clipped to the room along its length (a wall can run past the room's ends).
            float a0 = alongZ ? Mathf.Max(mn.z, room.z0) : Mathf.Max(mn.x, room.x0);
            float a1 = alongZ ? Mathf.Min(mx.z, room.z1) : Mathf.Min(mx.x, room.x1);
            if (a1 - a0 < 0.1f) return;
            float along = (a0 + a1) * 0.5f, len = a1 - a0;
            Vector3 At(float y, float off) => alongZ ? new Vector3(planeCoord + n.x * off, y, along) : new Vector3(along, y, planeCoord + n.z * off);
            Vector3 Size(float sx, float sy, float depth) => alongZ ? new Vector3(depth, sy, sx) : new Vector3(sx, sy, depth);

            float wTop = room.floorY + Wainscot;
            var paper = Paper(room.name, fin);
            var trim = DreamEscapeBuilder.Pastel("RoomTrim", new Color(0.99f, 0.97f, 0.93f), 0.02f);
            if (look == HouseLook.Chimney)
            {
                // The chimney breast: brick courses, no wallpaper.
                var brick = DreamEscapeBuilder.Pastel("DecorBrick", new Color(0.86f, 0.55f, 0.5f), 0f);
                DreamEscapeBuilder.Quad(name + " (brick)", t, At((y0 + y1) * 0.5f, 0.012f), n, len, y1 - y0, brick);
                var mortar = DreamEscapeBuilder.Pastel("DecorMortar", new Color(0.95f, 0.8f, 0.74f), 0f);
                for (float y = y0 + 0.24f; y < y1 - 0.05f; y += 0.24f)
                    DreamEscapeBuilder.Box("Brick course", t, At(y, 0.02f), Size(len, 0.02f, 0.02f), mortar);
                return;
            }
            // The wallpaper above the wainscot (or the whole face if it starts higher: a header over a door).
            float py0 = Mathf.Max(y0, wTop);
            if (y1 - py0 > 0.05f)
                DreamEscapeBuilder.Quad(name + " (paper)", t, At((py0 + y1) * 0.5f, 0.012f), n, len, y1 - py0, paper);
            if (y0 < wTop - 0.05f)
            {
                float wy1 = Mathf.Min(wTop, y1);
                var panel = fin.tiled
                    ? DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("RoomTiles_" + Key(room.name), fin.wainscot, 0f), DreamHouseDressing.PatternTiles, 0.3f, 0.35f)
                    : DreamEscapeBuilder.Pastel("RoomWainscot_" + Key(room.name), fin.wainscot, 0f);
                DreamEscapeBuilder.Quad(name + " (wainscot)", t, At((y0 + wy1) * 0.5f, 0.012f), n, len, wy1 - y0, panel);
                if (!fin.tiled)   // panel mouldings: thin uprights every 0.6 m
                    for (float a = a0 + 0.3f; a < a1 - 0.15f; a += 0.6f)
                    {
                        var pos = alongZ ? new Vector3(planeCoord + n.x * 0.02f, (y0 + wy1) * 0.5f + 0.05f, a) : new Vector3(a, (y0 + wy1) * 0.5f + 0.05f, planeCoord + n.z * 0.02f);
                        DreamEscapeBuilder.Box("Panel moulding", t, pos, Size(0.03f, wy1 - y0 - 0.3f, 0.02f), trim);
                    }
                DreamEscapeBuilder.Box("Chair rail", t, At(wTop, 0.025f), Size(len, 0.05f, 0.05f), trim);
                DreamEscapeBuilder.Box("Skirting", t, At(y0 + 0.07f, 0.02f), Size(len, 0.14f, 0.04f), trim);
            }
            if (room.ceiling >= 2.6f && y1 >= ceiling - 0.05f)
                DreamEscapeBuilder.Box("Picture rail", t, At(ceiling - 0.42f, 0.02f), Size(len, 0.04f, 0.04f), trim);
            if (y1 >= ceiling - 0.05f)
            {
                // The stepped crown (the creator's pick): three steps out from the wall under the ceiling.
                DreamEscapeBuilder.Box($"Cornice ({Mathf.RoundToInt(n.x)},{Mathf.RoundToInt(n.z)})", t, At(ceiling - 0.11f, 0.0175f), Size(len, 0.22f, 0.035f), trim);
                DreamEscapeBuilder.Box("Cornice step", t, At(ceiling - 0.065f, 0.0375f), Size(len, 0.13f, 0.075f), trim);
                DreamEscapeBuilder.Box("Cornice step", t, At(ceiling - 0.03f, 0.0575f), Size(len, 0.06f, 0.115f), trim);
            }
        }

        /// <summary>Over the bay's mouth, the step down from the living room's ceiling to the bay's lower one: papered like
        /// the walls, with a trim at its foot.</summary>
        static void BayBulkhead(DreamHouse h)
        {
            var living = h.rooms.Find(r => r.name == "Living room");
            var bay = h.rooms.Find(r => r.name == "Bay window");
            if (living == null || bay == null) return;
            var t = Group("Bay bulkhead");
            float y0 = F + bay.ceiling, y1 = F + living.ceiling, x0 = DreamHouse.BayX0 - 0.2f, x1 = DreamHouse.BayX1 + 0.2f;
            if (y1 - y0 < 0.05f) return;
            DreamEscapeBuilder.Quad("Bay bulkhead (paper)", t, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, DreamHouse.HubZ0 + 0.012f), Vector3.forward, x1 - x0, y1 - y0, Paper("Living room", Finishes["Living room"]));
            DreamEscapeBuilder.Box("Bay bulkhead (trim)", t, new Vector3((x0 + x1) * 0.5f, y0 + 0.04f, DreamHouse.HubZ0 + 0.03f), new Vector3(x1 - x0 + 0.1f, 0.08f, 0.06f),
                DreamEscapeBuilder.Pastel("RoomTrim", new Color(0.99f, 0.97f, 0.93f), 0.02f));
        }

        /// <summary>Exposed beams across each room's short span, every 0.9 m (the creator's pick for the ceilings).</summary>
        static void Beams(DreamHouse h)
        {
            var t = Group("Ceiling beams");
            var wood = DreamEscapeBuilder.Pastel("CeilingBeam", new Color(0.56f, 0.4f, 0.32f), 0.03f);
            foreach (var room in h.rooms)
            {
                if (room.name == "Attic" || room.name == "Attic stairs") continue;
                float w = room.x1 - room.x0, d = room.z1 - room.z0, top = room.floorY + room.ceiling;
                bool acrossX = w <= d;
                float len = acrossX ? w : d, run = acrossX ? d : w;
                int n = Mathf.Max(1, Mathf.FloorToInt(run / 0.9f));
                for (int i = 0; i < n; i++)
                {
                    float a = (acrossX ? room.z0 : room.x0) + run * (i + 0.5f) / n;
                    if (room.name == "Hall" && !acrossX && a > DreamHouse.HatchX0 - 0.2f && a < DreamHouse.HatchX1 + 0.2f) continue;   // the hatch stays open
                    var at = acrossX ? new Vector3((room.x0 + room.x1) * 0.5f, top - 0.08f, a) : new Vector3(a, top - 0.08f, (room.z0 + room.z1) * 0.5f);
                    DreamEscapeBuilder.Box("Ceiling beam", t, at, acrossX ? new Vector3(len, 0.16f, 0.14f) : new Vector3(0.14f, 0.16f, len), wood);
                }
            }
        }

        /// <summary>The attic's boards: painted tongue and groove (the creator's pick).</summary>
        internal static Material AtticBoards(float outline = 0.02f) =>
            DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("AtticBoards", new Color(0.93f, 0.9f, 0.84f), outline), DreamHouseDressing.PatternFloorboards, 1.6f, 0.3f);

        static string Key(string room) => room.Replace(" ", "").Replace("'", "");

        static Material Paper(string room, Finish fin) =>
            DreamHouseDressing.ColourPattern(DreamEscapeBuilder.Pastel("RoomPaper_" + Key(room), fin.paper, 0f), WallTile, WallTileMetres);

        // ---------- Windows ----------

        /// <summary>The house's windows (round 4: taller, for the higher rooms), on the rooms' wall faces: the centre (its
        /// height above the floor), the face's normal into the room, the width and height. DreamHouseExterior shows the same
        /// windows from outside.</summary>
        internal static readonly (Vector3 c, Vector3 n, float w, float h)[] HouseWindows =
        {
            // The kitchen: over the counter (west and south walls).
            (new Vector3(DreamHouse.HubX0, 1.75f, 73.9f), Vector3.right, 1.5f, 1.2f),
            (new Vector3(-5.2f, 1.75f, DreamHouse.HubZ0), Vector3.forward, 1.5f, 1.2f),
            // The living room's bay: a wide window in front, narrow ones in its sides.
            (new Vector3((DreamHouse.BayX0 + DreamHouse.BayX1) * 0.5f, 1.6f, DreamHouse.BayZ0), Vector3.forward, 2.0f, 1.8f),
            (new Vector3(DreamHouse.BayX0, 1.6f, (DreamHouse.BayZ0 + DreamHouse.HubZ0) * 0.5f - 0.1f), Vector3.right, 0.5f, 1.7f),
            (new Vector3(DreamHouse.BayX1, 1.6f, (DreamHouse.BayZ0 + DreamHouse.HubZ0) * 0.5f - 0.1f), Vector3.left, 0.5f, 1.7f),
            // The bedrooms and the bath, to the north (and the parents' room's west window).
            (new Vector3(-3.0f, 1.65f, DreamHouse.HubZ1), Vector3.back, 1.4f, 1.4f),
            (new Vector3(DreamHouse.HubX0, 1.65f, 82.6f), Vector3.right, 1.0f, 1.4f),
            (new Vector3(0.1f, 2.05f, DreamHouse.HubZ1), Vector3.back, 0.7f, 0.5f),
            (new Vector3(4.2f, 1.65f, DreamHouse.HubZ1), Vector3.back, 1.0f, 1.2f),
            // The living room's east wall (the house's front), either side of the chimney breast: two tall, narrow windows.
            (new Vector3(DreamHouse.HubX1, 1.75f, 72.9f), Vector3.left, 0.7f, 2.0f),
            (new Vector3(DreamHouse.HubX1, 1.75f, 77.0f), Vector3.left, 0.7f, 2.0f),
            // Theo's room: a small high window over his desk.
            (new Vector3(DreamHouse.HubX1, 2.35f, 82.65f), Vector3.left, 0.6f, 0.4f),
        };

        static void Windows()
        {
            var t = Group("Windows");
            foreach (var (c, n, w, h) in HouseWindows)
                Window(t, new Vector3(c.x, F + c.y, c.z) + n * 0.01f, n, w, h);
            FrontDoor();
        }

        /// <summary>The front door at the hall's east end (round 4: the outside matches the inside, its porch lines up with
        /// the hall): the house's red double door, shut, its frame, a fanlight of daylight over it, knobs, a mat.</summary>
        static void FrontDoor()
        {
            var t = Group("The front door (inside)");
            float x = DreamHouse.HubX1 - 0.01f, z = (DreamHouse.HallZ0 + DreamHouse.HallZ1) * 0.5f, w = 1.5f, h = 1.95f;
            var paint = DreamEscapeBuilder.Pastel("HouseFrontDoor", new Color(0.88f, 0.45f, 0.44f), 0.03f);
            var panel = DreamEscapeBuilder.Pastel("HouseFrontDoorPanel", new Color(0.95f, 0.6f, 0.56f), 0.02f);
            var frame = DreamEscapeBuilder.Pastel("HouseWindowFrame", DreamEscapeBuilder.Palette[0], 0.02f);
            var brass = DreamEscapeBuilder.Pastel("DecorBrassKnob", new Color(0.95f, 0.78f, 0.38f), 0.01f);
            foreach (float side in new[] { -1f, 1f })
            {
                float lz = z + side * w * 0.25f;
                DreamEscapeBuilder.Box("Front door (leaf)", t, new Vector3(x - 0.025f, F + h * 0.5f, lz), new Vector3(0.05f, h, w * 0.5f - 0.02f), paint);
                foreach (float py in new[] { 0.55f, 1.4f })
                    DreamEscapeBuilder.Box("Door panel", t, new Vector3(x - 0.06f, F + py, lz), new Vector3(0.02f, 0.6f, w * 0.5f - 0.24f), panel);
                DreamEscapeBuilder.Part("Door knob", PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.07f, brass).transform.position = new Vector3(x - 0.09f, F + 1.0f, z + side * 0.1f);
            }
            DreamEscapeBuilder.Box("Door frame", t, new Vector3(x - 0.04f, F + h + 0.05f, z), new Vector3(0.08f, 0.1f, w + 0.2f), frame);
            DreamEscapeBuilder.Box("Door frame", t, new Vector3(x - 0.04f, F + h * 0.5f, z - w * 0.5f - 0.05f), new Vector3(0.08f, h, 0.1f), frame);
            DreamEscapeBuilder.Box("Door frame", t, new Vector3(x - 0.04f, F + h * 0.5f, z + w * 0.5f + 0.05f), new Vector3(0.08f, h, 0.1f), frame);
            DreamEscapeBuilder.Quad("Fanlight", t, new Vector3(x - 0.012f, F + h + 0.2f, z), Vector3.left, w, 0.2f, DreamEscapeBuilder.Pastel("HouseWindowPane", new Color(0.8f, 0.88f, 1f), 0f, 0.55f));
            DreamEscapeBuilder.Box("Door frame", t, new Vector3(x - 0.04f, F + h + 0.31f, z), new Vector3(0.08f, 0.05f, w + 0.2f), frame);
            DreamEscapeBuilder.Box("Doormat", t, new Vector3(x - 0.45f, F + 0.012f, z), new Vector3(0.6f, 0.024f, 1.2f), DreamEscapeBuilder.Pastel("HouseDoormat", new Color(0.66f, 0.5f, 0.36f), 0.01f));
        }

        /// <summary>A window: a pane of soft daylight (unless <paramref name="pane"/> is false: the frame only), a white
        /// frame, glazing bars, a sill.</summary>
        internal static void Window(Transform t, Vector3 c, Vector3 n, float w, float hgt, bool pane = true)
        {
            var frame = DreamEscapeBuilder.Pastel("HouseWindowFrame", DreamEscapeBuilder.Palette[0], 0.02f);
            if (pane)
                DreamEscapeBuilder.Quad("Window", t, c + n * 0.006f, n, w, hgt, DreamEscapeBuilder.Pastel("HouseWindowPane", new Color(0.8f, 0.88f, 1f), 0f, 0.55f));
            bool alongZ = Mathf.Abs(n.x) > 0.5f;
            Vector3 across = alongZ ? Vector3.forward : Vector3.right;
            Vector3 S(float a, float u, float d) => alongZ ? new Vector3(d, u, a) : new Vector3(a, u, d);
            Vector3 o = c + n * 0.03f;
            const float bar = 0.08f;
            DreamEscapeBuilder.Box("Window frame", t, o + Vector3.up * (hgt * 0.5f), S(w + bar, bar, 0.06f), frame);
            DreamEscapeBuilder.Box("Window frame", t, o - Vector3.up * (hgt * 0.5f), S(w + bar, bar, 0.06f), frame);
            DreamEscapeBuilder.Box("Window frame", t, o + across * (w * 0.5f), S(bar, hgt + bar, 0.06f), frame);
            DreamEscapeBuilder.Box("Window frame", t, o - across * (w * 0.5f), S(bar, hgt + bar, 0.06f), frame);
            DreamEscapeBuilder.Box("Glazing bar", t, o, S(w, 0.04f, 0.04f), frame);
            DreamEscapeBuilder.Box("Glazing bar", t, o, S(0.04f, hgt, 0.04f), frame);
            DreamEscapeBuilder.Box("Window sill", t, c + n * 0.09f - Vector3.up * (hgt * 0.5f + 0.06f), S(w + 0.24f, 0.05f, 0.18f), frame);
        }

        // ---------- The attic stairs and the hatch ----------

        static void Stairs()
        {
            var t = Group("The attic stairs (steps)");
            float x0 = DreamHouse.StairX0, x1 = DreamHouse.StairX1, zTop = DreamHouse.StairZ0, zFoot = DreamHouse.StairZ1;
            float rise = DreamHouse.AtticFloor - F, run = zFoot - zTop;
            const int steps = 13;
            var tread = DreamEscapeBuilder.Pastel("StairTread", new Color(0.78f, 0.58f, 0.46f), 0.02f);
            var riser = DreamEscapeBuilder.Pastel("StairRiser", new Color(0.97f, 0.95f, 0.9f), 0.02f);
            for (int i = 0; i < steps; i++)
            {
                // Step i (from the foot): its top at the ramp's height at its far (south) edge, a hair over the slope.
                float zFar = zFoot - run * (i + 1) / steps, zNear = zFoot - run * i / steps;
                float top = F + rise * (i + 1) / steps;
                float midZ = (zFar + zNear) * 0.5f, depth = zNear - zFar;
                DreamEscapeBuilder.Box("Tread", t, new Vector3((x0 + x1) * 0.5f, top - 0.025f, midZ), new Vector3(x1 - x0, 0.05f, depth + 0.02f), tread);
                DreamEscapeBuilder.Box("Riser", t, new Vector3((x0 + x1) * 0.5f, top - rise / steps * 0.5f, zNear - 0.01f), new Vector3(x1 - x0, rise / steps, 0.02f), riser);
            }
            // A handrail on the east side.
            var rail = DreamEscapeBuilder.Pastel("StairRail", new Color(0.62f, 0.46f, 0.38f), 0.01f);
            var r = DreamEscapeBuilder.Box("Handrail", t, new Vector3(x1 - 0.08f, F + rise * 0.5f + 0.9f, (zTop + zFoot) * 0.5f), new Vector3(0.05f, 0.05f, Mathf.Sqrt(run * run + rise * rise)), rail);
            r.transform.rotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0f, 0f);
            // A bare bulb at the top, its warm light leading up the stairs.
            DreamEscapeBuilder.Part("Bulb (the top of the attic stairs)", PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.2f,
                DreamEscapeBuilder.Glow("StairBulb", new Color(1f, 0.86f, 0.6f), 1.0f, 0f, 0.1f)).transform.position = new Vector3((x0 + x1) * 0.5f, DreamHouse.AtticFloor + 2.0f, zTop + 0.3f);
        }

        static void Hatch()
        {
            var t = Group("The hatch (the attic's way down)");
            float x0 = DreamHouse.HatchX0, x1 = DreamHouse.HatchX1, z0 = DreamHouse.HatchZ0, z1 = DreamHouse.HatchZ1;
            var frame = DreamEscapeBuilder.Pastel("RoomTrim", new Color(0.99f, 0.97f, 0.93f), 0.02f);
            float y = F + 2.4f + DreamHouse.CeilingRaise;
            DreamEscapeBuilder.Box("Hatch frame", t, new Vector3((x0 + x1) * 0.5f, y - 0.03f, z0), new Vector3(x1 - x0 + 0.12f, 0.06f, 0.06f), frame);
            DreamEscapeBuilder.Box("Hatch frame", t, new Vector3((x0 + x1) * 0.5f, y - 0.03f, z1), new Vector3(x1 - x0 + 0.12f, 0.06f, 0.06f), frame);
            DreamEscapeBuilder.Box("Hatch frame", t, new Vector3(x0, y - 0.03f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.06f, z1 - z0), frame);
            DreamEscapeBuilder.Box("Hatch frame", t, new Vector3(x1, y - 0.03f, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.06f, z1 - z0), frame);
            // Its rim on the attic's side.
            float ya = DreamHouse.AtticFloor + 0.02f;
            DreamEscapeBuilder.Box("Hatch rim", t, new Vector3((x0 + x1) * 0.5f, ya, z0 - 0.04f), new Vector3(x1 - x0 + 0.16f, 0.04f, 0.08f), frame);
            DreamEscapeBuilder.Box("Hatch rim", t, new Vector3((x0 + x1) * 0.5f, ya, z1 + 0.04f), new Vector3(x1 - x0 + 0.16f, 0.04f, 0.08f), frame);
            DreamEscapeBuilder.Box("Hatch rim", t, new Vector3(x0 - 0.04f, ya, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.04f, z1 - z0), frame);
            DreamEscapeBuilder.Box("Hatch rim", t, new Vector3(x1 + 0.04f, ya, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.04f, z1 - z0), frame);
            // The lid, swung up against the attic's floor beside it.
            var lid = DreamEscapeBuilder.Box("Hatch lid (open)", t, Vector3.zero, new Vector3(x1 - x0, 0.05f, z1 - z0), DreamEscapeBuilder.Pastel("StairTread", new Color(0.78f, 0.58f, 0.46f), 0.02f));
            lid.transform.position = new Vector3(x0 - 0.02f, DreamHouse.AtticFloor + (z1 - z0) * 0.5f, (z0 + z1) * 0.5f);
            lid.transform.rotation = Quaternion.Euler(0f, 0f, 80f);
            // Starlight falling through it into the hall: the attic teases from below.
            DreamEscapeBuilder.Quad("Starlight (through the hatch)", t, new Vector3((x0 + x1) * 0.5f, F + 0.012f, (z0 + z1) * 0.5f), Vector3.up, 1.2f, 1.2f,
                DreamEscapeBuilder.Glow("HatchStarlight", new Color(0.7f, 0.76f, 1f), 0.6f, 0f, 0.35f));
        }

        // ---------- The attic ----------

        static void Attic()
        {
            var t = Group("The attic (roof, rafters, windows)");
            float FA = DreamHouse.AtticFloor, x0 = DreamHouse.HubX0, x1 = DreamHouse.HubX1, z0 = DreamHouse.HubZ0, z1 = DreamHouse.HubZ1, rz = DreamHouse.RidgeZ;
            float rise = DreamHouse.RidgeHeight - DreamHouse.KneeWall, half = rz - z0;
            float slope = Mathf.Atan2(rise, half) * Mathf.Rad2Deg, length = Mathf.Sqrt(rise * rise + half * half);
            var boards = AtticBoards();
            var beam = DreamEscapeBuilder.Pastel("AtticBeam", new Color(0.6f, 0.44f, 0.36f), 0.03f);
            // The two slopes (the north one around the dormer), east of the observatory (its roof is cut and raised).
            float ox = DreamHouse.ObsX1;
            Slope(t, "Roof (south)", ox, x1, true, slope, length, boards);
            Slope(t, "Roof (north, west of the dormer)", ox, DreamHouse.DormerX0 - 0.2f, false, slope, length, boards);
            Slope(t, "Roof (north, east of the dormer)", DreamHouse.DormerX1 + 0.2f, x1, false, slope, length, boards);
            // Over the dormer, the north slope runs only from the ridge down to the dormer's roof.
            float dz = DreamHouse.DormerZ0 - rz, dh = DreamHouse.RoofAt(rz) - DreamHouse.RoofAt(DreamHouse.DormerZ0);
            var cap = DreamEscapeBuilder.Box("Roof (north, over the dormer)", t, Vector3.zero, new Vector3(DreamHouse.DormerX1 - DreamHouse.DormerX0 + 0.4f, 0.06f, Mathf.Sqrt(dz * dz + dh * dh)), boards);
            cap.transform.position = new Vector3((DreamHouse.DormerX0 + DreamHouse.DormerX1) * 0.5f, FA + (DreamHouse.RoofAt(rz) + DreamHouse.RoofAt(DreamHouse.DormerZ0)) * 0.5f + 0.03f, (rz + DreamHouse.DormerZ0) * 0.5f);
            cap.transform.rotation = Quaternion.Euler(slope, 0f, 0f);
            // The rafters (every 1.2 m), the ridge beam, two collar ties.
            for (float x = ox + 0.6f; x < x1; x += 1.2f)
                foreach (bool south in new[] { true, false })
                {
                    if (!south && x > DreamHouse.DormerX0 - 0.2f && x < DreamHouse.DormerX1 + 0.2f) continue;
                    var r = DreamEscapeBuilder.Box("Rafter", t, Vector3.zero, new Vector3(0.12f, 0.18f, length), beam);
                    r.transform.position = new Vector3(x, FA + (DreamHouse.KneeWall + DreamHouse.RidgeHeight) * 0.5f - 0.1f, south ? z0 + half * 0.5f : z1 - half * 0.5f);
                    r.transform.rotation = Quaternion.Euler(south ? -slope : slope, 0f, 0f);
                }
            DreamEscapeBuilder.Box("Ridge beam", t, new Vector3((ox + x1) * 0.5f, FA + DreamHouse.RidgeHeight - 0.12f, rz), new Vector3(x1 - ox, 0.22f, 0.2f), beam);
            foreach (float x in new[] { 0.4f, 4.8f })
                DreamEscapeBuilder.Box("Collar tie", t, new Vector3(x, FA + 2.55f, rz), new Vector3(0.12f, 0.14f, 4.2f), beam);
            // The floorboards' look on the attic floor (the ceilings' tops), rough and plain, open over the hatch and the
            // stairwell (cut into cells round the two holes; the pattern is laid in world space, so the cells match).
            var floorMat = DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("AtticFloor", new Color(0.78f, 0.62f, 0.5f), 0f), DreamHouseDressing.PatternFloorboards, 2.4f, 0.4f);
            var holes = new[]
            {
                new Rect(DreamHouse.HatchX0, DreamHouse.HatchZ0, DreamHouse.HatchX1 - DreamHouse.HatchX0, DreamHouse.HatchZ1 - DreamHouse.HatchZ0),
                new Rect(DreamHouse.StairX0, DreamHouse.StairZ0, DreamHouse.StairX1 - DreamHouse.StairX0, DreamHouse.StairOpenZ1 - DreamHouse.StairZ0),
            };
            var xs = new List<float> { x0, x1 };
            var zs = new List<float> { z0, z1 };
            foreach (var hole in holes) { xs.Add(hole.xMin); xs.Add(hole.xMax); zs.Add(hole.yMin); zs.Add(hole.yMax); }
            xs.Sort(); zs.Sort();
            for (int i = 0; i + 1 < xs.Count; i++)
                for (int j = 0; j + 1 < zs.Count; j++)
                {
                    float cw = xs[i + 1] - xs[i], cd = zs[j + 1] - zs[j];
                    if (cw < 0.01f || cd < 0.01f) continue;
                    var mid = new Vector2(xs[i] + cw * 0.5f, zs[j] + cd * 0.5f);
                    bool open = false;
                    foreach (var hole in holes) open |= hole.Contains(mid);
                    if (!open)
                        DreamEscapeBuilder.Quad("Attic floorboards", t, new Vector3(mid.x, FA + 0.004f, mid.y), Vector3.up, cw, cd, floorMat);
                }
            Observatory(t, boards, beam);
            // The dormer's cheeks, from the slope up to its ceiling (no walls under the slope: the attic runs on).
            float dTop = FA + DreamHouse.DormerHeight, dMeet = DreamHouse.RidgeZ + (1f - (DreamHouse.DormerHeight - DreamHouse.KneeWall) / (DreamHouse.RidgeHeight - DreamHouse.KneeWall)) * (z1 - DreamHouse.RidgeZ);
            foreach (float x in new[] { DreamHouse.DormerX0 - 0.1f, DreamHouse.DormerX1 + 0.1f })
                Poly(t, "Dormer cheek", boards, new Vector3(x, dTop, dMeet), new Vector3(x, dTop, z1), new Vector3(x, FA + DreamHouse.KneeWall, z1));
            // The observatory's round window in the west gable, full of stars (it's always night in Lena's attic).
            Stars(t, new Vector3(x0 + 0.01f, FA + 1.85f, rz), Vector3.right, 2.1f, round: true);
            // The dormer: its window to the north, also night.
            Stars(t, new Vector3((DreamHouse.DormerX0 + DreamHouse.DormerX1) * 0.5f, FA + 1.55f, z1 - 0.32f), Vector3.back, 1.8f, round: false);
        }

        /// <summary>Lena's observatory (round 4): the roof cut and raised over the attic's west end. Its walls rise full height
        /// to a flat boarded ceiling with a skylight over the telescope; clerestory windows high in its long walls; where it
        /// meets the sloped roof, a wall from the slopes up to its ceiling (the "cut-off").</summary>
        static void Observatory(Transform t, Material boards, Material beam)
        {
            float FA = DreamHouse.AtticFloor, x0 = DreamHouse.HubX0, ox = DreamHouse.ObsX1, z0 = DreamHouse.HubZ0, z1 = DreamHouse.HubZ1;
            float top = FA + DreamHouse.ObsHeight;
            // The ceiling, open over the skylight.
            var sky = new Rect(DreamHouse.SkyX0, DreamHouse.SkyZ0, DreamHouse.SkyX1 - DreamHouse.SkyX0, DreamHouse.SkyZ1 - DreamHouse.SkyZ0);
            float[] xs = { x0, sky.xMin, sky.xMax, ox }, zs = { z0, sky.yMin, sky.yMax, z1 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    if (i == 1 && j == 1) continue;
                    DreamEscapeBuilder.Quad("Observatory ceiling", t, new Vector3((xs[i] + xs[i + 1]) * 0.5f, top - 0.004f, (zs[j] + zs[j + 1]) * 0.5f), Vector3.down, xs[i + 1] - xs[i], zs[j + 1] - zs[j], boards);
                }
            // Ceiling beams across, and a wall plate along each long wall.
            for (float x = x0 + 0.7f; x < ox - 0.2f; x += 1.0f)
                if (x < sky.xMin - 0.1f || x > sky.xMax + 0.1f)
                    DreamEscapeBuilder.Box("Observatory beam", t, new Vector3(x, top - 0.09f, (z0 + z1) * 0.5f), new Vector3(0.12f, 0.18f, z1 - z0 - 0.6f), beam);
            foreach (float z in new[] { z0 + 0.36f, z1 - 0.36f })
                DreamEscapeBuilder.Box("Wall plate", t, new Vector3((x0 + ox) * 0.5f, top - 0.1f, z), new Vector3(ox - x0, 0.2f, 0.12f), beam);
            // The skylight: a short light well up to a pane of night sky, its frame, stars.
            var night = DreamEscapeBuilder.Pastel("AtticNight", new Color(0.18f, 0.2f, 0.42f), 0f, 0.8f);
            float well = 0.35f;
            DreamEscapeBuilder.Quad("Skylight (night)", t, new Vector3(sky.center.x, top + well, sky.center.y), Vector3.down, sky.width, sky.height, night);
            DreamEscapeBuilder.Quad("Skylight well", t, new Vector3(sky.xMin, top + well * 0.5f, sky.center.y), Vector3.right, sky.height, well, boards);
            DreamEscapeBuilder.Quad("Skylight well", t, new Vector3(sky.xMax, top + well * 0.5f, sky.center.y), Vector3.left, sky.height, well, boards);
            DreamEscapeBuilder.Quad("Skylight well", t, new Vector3(sky.center.x, top + well * 0.5f, sky.yMin), Vector3.forward, sky.width, well, boards);
            DreamEscapeBuilder.Quad("Skylight well", t, new Vector3(sky.center.x, top + well * 0.5f, sky.yMax), Vector3.back, sky.width, well, boards);
            DreamEscapeBuilder.Box("Skylight bar", t, new Vector3(sky.center.x, top + well - 0.03f, sky.center.y), new Vector3(sky.width, 0.05f, 0.05f), beam);
            DreamEscapeBuilder.Box("Skylight bar", t, new Vector3(sky.center.x, top + well - 0.03f, sky.center.y), new Vector3(0.05f, 0.05f, sky.height), beam);
            var star = DreamEscapeBuilder.Glow("AtticStar", new Color(1f, 0.96f, 0.82f), 1.4f, 0f, 0.2f);
            var rnd = new System.Random(23);
            for (int i = 0; i < 18; i++)
                DreamEscapeBuilder.Part("Star", PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * (0.02f + 0.04f * (float)rnd.NextDouble()), star).transform.position =
                    new Vector3(sky.xMin + sky.width * (float)rnd.NextDouble(), top + well - 0.02f, sky.yMin + sky.height * (float)rnd.NextDouble());
            // Clerestory windows high in the long walls: more night sky.
            foreach (float x in new[] { -6.0f, -4.2f })
            {
                Stars(t, new Vector3(x, FA + 3.0f, z0 + 0.31f), Vector3.forward, 1.1f, round: false);
                Stars(t, new Vector3(x, FA + 3.0f, z1 - 0.31f), Vector3.back, 1.1f, round: false);
            }
            // The cut-off: the observatory's east wall, from the slopes up to its ceiling (both faces).
            float k = DreamHouse.KneeWall, rh = DreamHouse.RidgeHeight, rz = DreamHouse.RidgeZ, H = DreamHouse.ObsHeight;
            Poly(t, "Observatory wall over the slopes (south half)", boards, new Vector3(ox, FA + k, z0), new Vector3(ox, FA + rh, rz), new Vector3(ox, FA + H, rz), new Vector3(ox, FA + H, z0));
            Poly(t, "Observatory wall over the slopes (north half)", boards, new Vector3(ox, FA + rh, rz), new Vector3(ox, FA + k, z1), new Vector3(ox, FA + H, z1), new Vector3(ox, FA + H, rz));
            DreamEscapeBuilder.Box("Observatory beam (over the way in)", t, new Vector3(ox - 0.06f, FA + rh + 0.08f, rz), new Vector3(0.14f, 0.16f, 7.2f), beam);
        }

        /// <summary>A flat convex polygon (a fan), seen from both sides: two one-sided meshes back to back (one double-sided
        /// mesh drew its ink outline over itself), neither taking shadows (the dream's flats don't).</summary>
        internal static void Poly(Transform t, string name, Material mat, params Vector3[] pts)
        {
            mat = Flat(mat);
            OneSided(t, name, mat, pts);
            var back = (Vector3[])pts.Clone();
            System.Array.Reverse(back);
            OneSided(t, name + " (back)", mat, back);
        }

        /// <summary>The same look with no ink outline: the outline's inverted hull only works on closed shapes, and on a flat
        /// it covered the face in ink (the front gable read dark).</summary>
        static Material Flat(Material mat)
        {
            string key = mat.name.StartsWith("Pastel_") ? mat.name.Substring(7) : mat.name;
            var flat = DreamEscapeBuilder.Pastel(key + "_Flat", mat.GetColor("_Color"), 0f);
            foreach (var prop in new[] { "_TexScale", "_TexMean", "_TexStrength" })
                if (mat.HasProperty(prop)) flat.SetFloat(prop, mat.GetFloat(prop));
            if (mat.HasProperty("_MainTex")) flat.SetTexture("_MainTex", mat.GetTexture("_MainTex"));
            return flat;
        }

        static void OneSided(Transform t, string name, Material mat, Vector3[] pts)
        {
            var mesh = new Mesh { name = name };
            var tris = new List<int>();
            for (int i = 1; i + 1 < pts.Length; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            mesh.SetVertices(new List<Vector3>(pts));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static void Slope(Transform t, string name, float xa, float xb, bool south, float slope, float length, Material mat)
        {
            float FA = DreamHouse.AtticFloor, half = DreamHouse.RidgeZ - DreamHouse.HubZ0;
            var s = DreamEscapeBuilder.Box(name, t, Vector3.zero, new Vector3(xb - xa, 0.06f, length), mat);
            s.transform.position = new Vector3((xa + xb) * 0.5f, FA + (DreamHouse.KneeWall + DreamHouse.RidgeHeight) * 0.5f + 0.03f,
                south ? DreamHouse.HubZ0 + half * 0.5f : DreamHouse.HubZ1 - half * 0.5f);
            s.transform.rotation = Quaternion.Euler(south ? -slope : slope, 0f, 0f);
        }

        /// <summary>A window onto a night sky: deep blue glass, stars, a frame (round for the observatory's window).</summary>
        static void Stars(Transform t, Vector3 c, Vector3 n, float size, bool round)
        {
            var night = DreamEscapeBuilder.Pastel("AtticNight", new Color(0.18f, 0.2f, 0.42f), 0f, 0.8f);
            var star = DreamEscapeBuilder.Glow("AtticStar", new Color(1f, 0.96f, 0.82f), 1.4f, 0f, 0.2f);
            var frame = DreamEscapeBuilder.Pastel("AtticBeam", new Color(0.6f, 0.44f, 0.36f), 0.03f);
            Vector3 across = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
            if (round)
            {
                var disc = DreamEscapeBuilder.Part("Round window (night)", PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(size, 0.01f, size), night);
                disc.transform.position = c + n * 0.02f;
                disc.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
                var ring = DreamEscapeBuilder.Part("Round window (frame)", PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(size + 0.24f, 0.008f, size + 0.24f), frame);
                ring.transform.position = c + n * 0.01f;
                ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
                DreamEscapeBuilder.Box("Window bar", t, c + n * 0.05f, Mathf.Abs(n.x) > 0.5f ? new Vector3(0.05f, 0.06f, size) : new Vector3(size, 0.06f, 0.05f), frame);
                DreamEscapeBuilder.Box("Window bar", t, c + n * 0.05f, Mathf.Abs(n.x) > 0.5f ? new Vector3(0.05f, size, 0.06f) : new Vector3(0.06f, size, 0.05f), frame);
            }
            else
            {
                DreamEscapeBuilder.Quad("Dormer window (night)", t, c + n * 0.006f, n, size, size * 0.75f, night);
                Window(t, c, n, size, size * 0.75f, pane: false);
            }
            var rnd = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rnd.NextDouble() - 0.5f, b = (float)rnd.NextDouble() - 0.5f;
                if (round && a * a + b * b > 0.2f) continue;
                if (!round && Mathf.Abs(b) > 0.36f) continue;
                float s = 0.02f + 0.04f * (float)rnd.NextDouble();
                DreamEscapeBuilder.Part("Star", PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * s, star).transform.position = c + n * 0.035f + across * a * size + Vector3.up * b * size;
            }
        }

        static Transform Group(string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(_root, false);
            return g;
        }
    }
}
