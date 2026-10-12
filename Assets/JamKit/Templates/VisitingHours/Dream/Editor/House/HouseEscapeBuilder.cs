using JamKit.Movement;
using UnityEditor;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The escape (core <see cref="HouseEscape"/>), built upright at its origin: each room in what its surfaces used to be,
    /// in its own palette (<see cref="HouseEscapeLook"/>: a room on its side walks on its old wall, the old floor's boards run
    /// up one side and the old ceiling the other; upside down, the ceiling is underfoot), furnished and dressed as the room it
    /// was, its things tipping into place as she comes (<see cref="HouseEscapeDressing"/>), every wall and ceiling solid (a
    /// collider for every piece, the side walls runnable), the corridor's bays the same as each other (a landmark set in every
    /// bay, so a stretch can be read) and its warm front door, the house folding shut behind her, the night sky over the
    /// attic, the last hall's door onto a warm glow. Nothing that moves is static; nothing here is occluder-static (the
    /// rooms' things tip and the fold moves).
    /// </summary>
    public static class HouseEscapeBuilder
    {
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        public sealed class Result
        {
            public Collider corridorDoor;
            public Transform corridorDoorLeaf, fold;
            public GameObject theoAtTheDoor, nightSky;
            public Transform[] rooms;
        }

        static GameObject _lastSky;

        public static Result Build(HouseEscape e, Transform root)
        {
            HouseEscapeLook.Clear();
            HouseEscapeDressing.Begin();
            var res = new Result();
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(root, false);
            var groups = new Transform[e.rooms.Count];
            var builders = new MeshBuilder[e.rooms.Count];
            for (int r = 0; r < e.rooms.Count; r++)
            {
                groups[r] = new GameObject("Escape room: " + e.rooms[r].name).transform;
                groups[r].SetParent(root, false);
                builders[r] = new MeshBuilder();
            }
            for (int i = 0; i < e.course.pieces.Count; i++)
            {
                var p = e.course.pieces[i];
                var look = e.looks[i];
                int r = Mathf.Max(0, e.RoomAt(p.center.Z));
                var room = e.rooms[r];
                if (i == e.corridorDoor)
                {
                    res.corridorDoor = CorridorDoor(p, root, out res.corridorDoorLeaf);
                    continue;
                }
                Piece(e, p, look, room, builders[r]);
                if (p.solid) Collider(p, colliders);
            }
            for (int r = 0; r < e.rooms.Count; r++)
            {
                HouseEscapeDressing.Dress(e, r, groups[r], root, builders[r]);
                builders[r].Build("Room", groups[r]);
            }
            res.rooms = groups;
            res.fold = Fold(root);
            res.theoAtTheDoor = TheoAtTheDoor(e, root);
            res.nightSky = HouseEscapeDressing.NightSky(e, root);
            _lastSky = res.nightSky;
            // Nothing in the escape is occluder-static (the rooms' things tip, the fold moves): batching and occludee only.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((f & StaticEditorFlags.OccluderStatic) != 0) GameObjectUtility.SetStaticEditorFlags(t.gameObject, f & ~StaticEditorFlags.OccluderStatic);
            }
            HouseEscapeDressing.End();
            return res;
        }

        /// <summary>The night sky for the attic open to the stars: the dome <see cref="Build"/> made round the attic (the game
        /// shows it only there). Called after <see cref="Build"/>; with no build before it, it makes one.</summary>
        public static GameObject NightSky(Camera cam)
        {
            if (_lastSky != null) return _lastSky;
            var root = cam != null ? cam.transform.root : null;
            return HouseEscapeDressing.NightSky(HouseEscape.Build(), root);
        }

        // ---------- The pieces, by what they used to be ----------

        static Material Planks => HouseMaterials.Planks();
        static Material Trim => HouseEscapeLook.Clean("TrimWhite", new Color(0.93f, 0.91f, 0.86f), 0.45f);

        static bool UpsideDown(EscapeRoom r) => Mathf.Abs(r.roll) > 179f;

        static void Piece(HouseEscape e, LabPiece p, HouseLook look, EscapeRoom room, MeshBuilder b)
        {
            var pal = HouseEscapeLook.For(room);
            Vector3 mn = U(p.Min), mx = U(p.Max);
            switch (look)
            {
                case HouseLook.EscWallpaperFloor:
                    Top(b, p, room.roll == 0f ? pal.oldFloor : UpsideDown(room) ? pal.oldCeiling : pal.wall, pal.edge);
                    break;
                case HouseLook.EscCeiling:
                    if (p.kind == LabPieceKind.Ramp) Ramp(b, p, Planks, pal.oldCeiling);
                    else if (p.Max.Y <= room.floorTop + 0.5f) Top(b, p, pal.oldCeiling, pal.edge);   // a ceiling underfoot (upside down)
                    else Under(b, p, room.roll == 0f ? pal.oldCeiling : UpsideDown(room) ? pal.oldFloor : pal.wall);
                    break;
                case HouseLook.EscFloorboardWall: SideWall(b, p, room, pal); break;
                case HouseLook.EscCorridorWall:
                    if (p.runnable) b.Box(mn, mx, new[] { p.center.X < 0f ? Planks : pal.wall, p.center.X < 0f ? pal.wall : Planks, Planks, Planks, Planks, Planks });
                    else EndWall(e, b, p);
                    break;
                case HouseLook.EscCorridorFloor: Ramp(b, p, pal.oldFloor, Planks); break;
                case HouseLook.EscTable: Table(b, p); break;
                case HouseLook.EscStairs: StepsOnRamp(b, p, pal); break;
                case HouseLook.EscDoor:
                    if (p.kind == LabPieceKind.Bar) DoorBar(b, p);
                    else Doorframe(b, p, pal);
                    break;
                case HouseLook.EscFurniture:
                    if (p.kind == LabPieceKind.Bar) FurnitureBar(b, p, room);
                    else Top(b, p, UpsideDown(room) ? pal.oldCeiling : pal.oldFloor, pal.edge);   // Theo's things: a floor now
                    break;
                case HouseLook.EscSkyBoards:
                case HouseLook.EscAtticBoards: b.Box(mn, mx, Planks); break;
                case HouseLook.EscLastHall: Top(b, p, pal.oldFloor, pal.edge); break;
                case HouseLook.EscRain: b.Box(mn, mx, HouseMaterials.Paving()); break;
                default: b.Box(mn, mx, pal.wall); break;
            }
        }

        static void Top(MeshBuilder b, LabPiece p, Material top, Material sides)
        {
            b.Box(U(p.Min), U(p.Max), new[] { sides, sides, sides, top, sides, sides });
        }

        static void Under(MeshBuilder b, LabPiece p, Material under)
        {
            var dark = Planks;
            b.Box(U(p.Min), U(p.Max), new[] { dark, dark, under, dark, dark, dark });
        }

        static void Ramp(MeshBuilder b, LabPiece p, Material top, Material under)
        {
            var o = HouseLint.Of(p, false);
            var edge = HouseMaterials.TrimWalnut();
            b.Obb(U(o.c), U(o.u0) * o.h.X, U(o.u1) * o.h.Y, U(o.u2) * o.h.Z, new[] { edge, edge, edge, edge, under, top });
        }

        /// <summary>The table (a slide now): its top in walnut, an apron under its long edges, its legs hanging under it.</summary>
        static void Table(MeshBuilder b, LabPiece p)
        {
            var wood = HouseMaterials.TrimWalnut();
            Ramp(b, p, wood, wood);
            float x0 = p.Min.X, x1 = p.Max.X;
            foreach (float zf in new[] { 0.06f, 0.94f })
            {
                float z = p.Min.Z + p.size.Z * zf;
                float top = p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * zf - HouseLint.RampThickness;
                foreach (float x in new[] { x0 + 0.12f, x1 - 0.2f })
                    b.Box(new Vector3(x, top - 0.72f, z - 0.04f), new Vector3(x + 0.08f, top, z + 0.04f), wood);
            }
            // The apron: a board under each long edge, along the slope.
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s < 0 ? x0 + 0.1f : x1 - 0.1f;
                float y0 = p.rampTopStart - HouseLint.RampThickness, y1 = p.rampTopEnd - HouseLint.RampThickness;
                b.Obb(new Vector3(x, (y0 + y1) * 0.5f - 0.08f, p.center.Z), new Vector3(0.02f, 0f, 0f), new Vector3(0f, (y1 - y0) * 0.5f, p.size.Z * 0.47f), new Vector3(0f, 0.08f, 0f), new[] { wood, wood, wood, wood, wood, wood });
            }
        }

        /// <summary>The staircase flattening into a slide: the boards, the treads drawn shallow (almost flat) on the slope,
        /// brass stair rods, the runner down its middle.</summary>
        static void StepsOnRamp(MeshBuilder b, LabPiece p, HouseEscapeLook.Palette pal)
        {
            Ramp(b, p, pal.oldFloor, Trim);
            float len = p.size.Z;
            int n = Mathf.RoundToInt(len / 0.7f);
            var brass = HouseEscapeLook.Brass();
            for (int k = 0; k < n; k++)
            {
                float z0 = p.Min.Z + len * k / n, t = (k + 0.5f) / n;
                float top = p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * t;
                b.Box(new Vector3(p.Min.X, top, z0), new Vector3(p.Max.X, top + 0.012f, z0 + 0.06f), HouseMaterials.TrimWalnut());
                b.Box(new Vector3(-1.05f, top + 0.012f, z0 + 0.07f), new Vector3(1.05f, top + 0.026f, z0 + 0.09f), brass);
            }
            var o = HouseLint.Of(p, false);
            var c = U(o.c) + U(o.u2) * (o.h.Z + 0.004f);
            var rug = pal.rug;
            b.Obb(c, new Vector3(1.0f, 0f, 0f), U(o.u1) * o.h.Y, U(o.u2) * 0.004f, new[] { rug, rug, rug, rug, rug, rug });
        }

        /// <summary>A room's side wall: only its inner face shows (what the room's turn makes it).</summary>
        static void SideWall(MeshBuilder b, LabPiece p, EscapeRoom room, HouseEscapeLook.Palette pal)
        {
            bool left = p.center.X < 0f;
            Material inner;
            if (room.roll == 0f || UpsideDown(room)) inner = pal.wall;
            else inner = (room.roll > 0f) == left ? pal.oldCeiling : pal.oldFloor;   // on its side: the old floor one side, the old ceiling the other
            var back = Planks;
            b.Box(U(p.Min), U(p.Max), new[] { left ? back : inner, left ? inner : back, back, back, back, back });
        }

        /// <summary>A wall across the route with a doorway: each face in the paper of the room it faces; the doorway cased both
        /// sides.</summary>
        static void EndWall(HouseEscape e, MeshBuilder b, LabPiece p)
        {
            int owner = -1;
            int colon = p.name.IndexOf(':');
            if (colon > 0)
            {
                string n = p.name.Substring(0, colon);
                for (int i = 0; i < e.rooms.Count; i++) if (e.rooms[i].name == n) owner = i;
            }
            var before = owner >= 0 ? e.rooms[owner] : e.rooms[Mathf.Max(0, e.RoomAt(p.Min.Z - 0.01f))];
            var after = owner >= 0 ? e.rooms[Mathf.Min(e.rooms.Count - 1, owner + 1)] : e.rooms[Mathf.Max(0, e.RoomAt(p.Max.Z + 0.01f))];
            var pb = HouseEscapeLook.For(before);
            var pa = HouseEscapeLook.For(after);
            Material front = before.open ? Planks : pb.wall, back = after.open ? Planks : pa.wall;
            b.Box(U(p.Min), U(p.Max), new[] { pb.edge, pb.edge, pb.edge, Planks, front, back });
            if (!p.name.Contains("over the doorway")) return;
            // The casing round the doorway (under this piece and up its jambs, both faces).
            float x0 = p.Min.X, x1 = p.Max.X, y = p.Min.Y, foot = y - 3.6f;
            foreach (float z in new[] { p.Min.Z - 0.025f, p.Max.Z })
            {
                b.Box(new Vector3(x0 - 0.1f, y, z), new Vector3(x1 + 0.1f, y + 0.12f, z + 0.025f), Trim);
                b.Box(new Vector3(x0 - 0.1f, foot, z), new Vector3(x0, y, z + 0.025f), Trim);
                b.Box(new Vector3(x1, foot, z), new Vector3(x1 + 0.1f, y, z + 0.025f), Trim);
            }
        }

        /// <summary>The hall's doorframes (free-standing walls she kicks between): papered, a dado, door casings along them.</summary>
        static void Doorframe(MeshBuilder b, LabPiece p, HouseEscapeLook.Palette pal)
        {
            Vector3 mn = U(p.Min), mx = U(p.Max);
            b.Box(mn, mx, new[] { pal.wall, pal.wall, pal.edge, pal.edge, pal.edge, pal.edge });
            var door = HouseEscapeLook.Clean("HallDoors", new Color(0.9f, 0.88f, 0.82f), 0.5f);
            float t = mx.y - 6f;   // the floor they stood on
            for (int side = -1; side <= 1; side += 2)
            {
                float x0 = side < 0 ? mn.x - 0.025f : mx.x, x1 = x0 + 0.025f;
                b.Box(new Vector3(x0, t + 0.88f, mn.z), new Vector3(x1, t + 0.94f, mx.z), pal.rail);
                for (float z = mn.z + 1.2f; z < mx.z - 1.4f; z += 4f)
                {
                    float d0 = side < 0 ? x0 + 0.01f : x0, d1 = side < 0 ? x1 : x1 - 0.01f;
                    b.Box(new Vector3(d0, t, z), new Vector3(d1, t + 2.1f, z + 0.95f), door);
                    b.Box(new Vector3(x0, t, z - 0.08f), new Vector3(x1, t + 2.18f, z), Trim);
                    b.Box(new Vector3(x0, t, z + 0.95f), new Vector3(x1, t + 2.18f, z + 1.03f), Trim);
                    b.Box(new Vector3(x0, t + 2.1f, z), new Vector3(x1, t + 2.18f, z + 0.95f), Trim);
                }
            }
        }

        /// <summary>The same door, half open, hanging across the corridor (slide under it): a panelled leaf with a brass knob.</summary>
        static void DoorBar(MeshBuilder b, LabPiece p)
        {
            Vector3 mn = U(p.Min), mx = U(p.Max);
            var paint = HouseEscapeLook.Clean("CorridorDoors", new Color(0.9f, 0.88f, 0.82f), 0.5f);
            b.Box(mn, mx, paint);
            float w = mx.x - mn.x, h = mx.y - mn.y;
            foreach (float z in new[] { mn.z - 0.012f, mx.z })
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        float x0 = mn.x + 0.15f + i * (w * 0.5f), y0 = mn.y + 0.15f + j * (h * 0.5f);
                        b.Box(new Vector3(x0, y0, z), new Vector3(x0 + w * 0.5f - 0.3f, y0 + h * 0.5f - 0.3f, z + 0.012f), Trim);
                    }
            b.Box(new Vector3(mn.x + w * 0.85f, mn.y + h * 0.3f, mn.z - 0.06f), new Vector3(mn.x + w * 0.85f + 0.06f, mn.y + h * 0.3f + 0.06f, mx.z + 0.06f), HouseEscapeLook.Brass());
        }

        /// <summary>The things she slides under: the sideboard hanging off the dining room's wall (its doors and handles),
        /// Theo's bed (slats, mattress, the duvet hanging over, its posts up to the floor that's now the ceiling), the
        /// kitchen's fridge on its side (enamel, the door's seam, chrome handles, the calendar).</summary>
        static void FurnitureBar(MeshBuilder b, LabPiece p, EscapeRoom room)
        {
            Vector3 mn = U(p.Min), mx = U(p.Max);
            float h = mx.y - mn.y;
            if (p.name.Contains("bed"))
            {
                var frame = HouseMaterials.TrimWalnut();
                var sheet = HouseEscapeLook.Clean("Sheets", new Color(0.92f, 0.92f, 0.88f), 0.15f);
                var duvet = HouseMaterials.Fabric("TheoDuvet", new Color(0.92f, 0.6f, 0.26f));
                b.Box(mn, new Vector3(mx.x, mn.y + 0.3f, mx.z), frame);
                b.Box(new Vector3(mn.x, mn.y + 0.3f, mn.z + 0.05f), new Vector3(mx.x, mn.y + 0.75f, mx.z - 0.05f), sheet);
                b.Box(new Vector3(mn.x, mn.y + 0.75f, mn.z - 0.04f), new Vector3(mx.x, mx.y, mx.z + 0.04f), duvet);
                for (float x = mn.x + 0.3f; x < mx.x - 0.2f; x += 0.45f)
                    b.Box(new Vector3(x, mn.y - 0.03f, mn.z + 0.1f), new Vector3(x + 0.12f, mn.y, mx.z - 0.1f), frame);
                foreach (float x in new[] { mn.x + 0.05f, mx.x - 0.17f })
                    foreach (float z in new[] { mn.z + 0.02f, mx.z - 0.14f })
                        b.Box(new Vector3(x, mx.y, z), new Vector3(x + 0.12f, room.ceilingY, z + 0.12f), frame);
                return;
            }
            if (p.name.Contains("fridge"))
            {
                var enamel = HouseEscapeLook.Enamel("Fridge", new Color(0.93f, 0.92f, 0.86f));
                var chrome = HouseEscapeLook.Chrome();
                var seam = HouseEscapeLook.Clean("Seam", new Color(0.2f, 0.2f, 0.22f), 0.2f);
                b.Box(mn, mx, enamel);
                float ys = mn.y + h * 0.36f;
                b.Box(new Vector3(mn.x + 0.05f, ys - 0.012f, mn.z - 0.006f), new Vector3(mx.x - 0.05f, ys + 0.012f, mn.z), seam);
                b.Box(new Vector3(mn.x + 0.4f, ys + 0.15f, mn.z - 0.07f), new Vector3(mx.x - 0.4f, ys + 0.21f, mn.z - 0.02f), chrome);
                b.Box(new Vector3(mn.x + 0.4f, ys - 0.21f, mn.z - 0.07f), new Vector3(mx.x - 0.4f, ys - 0.15f, mn.z - 0.02f), chrome);
                b.Box(new Vector3(mn.x + 0.2f, mn.y, mn.z - 0.01f), new Vector3(mx.x - 0.2f, mn.y + 0.12f, mn.z), HouseEscapeLook.Clean("Grille", new Color(0.3f, 0.3f, 0.32f), 0.4f));
                var paper = HouseEscapeLook.Clean("CalendarPaper", new Color(0.97f, 0.95f, 0.88f), 0.1f);
                b.Box(new Vector3(-0.3f, ys + 0.35f, mn.z - 0.008f), new Vector3(0.3f, ys + 1.15f, mn.z - 0.002f), paper);
                b.Box(new Vector3(-0.3f, ys + 1.05f, mn.z - 0.01f), new Vector3(0.3f, ys + 1.15f, mn.z - 0.008f), HouseEscapeLook.Clean("CalendarRed", new Color(0.7f, 0.18f, 0.16f), 0.2f));
                return;
            }
            // The sideboard: a walnut carcass, a moulded top and plinth, a row of panelled doors and their brass handles.
            var wood = HouseMaterials.TrimWalnut();
            var panel = HouseEscapeLook.Clean("SideboardPanels", new Color(0.36f, 0.42f, 0.34f), 0.5f);
            var brass = HouseEscapeLook.Brass();
            b.Box(mn, mx, wood);
            b.Box(new Vector3(mn.x, mx.y - 0.08f, mn.z - 0.05f), new Vector3(mx.x, mx.y, mx.z + 0.05f), wood);
            b.Box(new Vector3(mn.x, mn.y, mn.z - 0.05f), new Vector3(mx.x, mn.y + 0.08f, mx.z + 0.05f), wood);
            foreach (float z in new[] { mn.z - 0.012f, mx.z })
                for (float x = mn.x + 0.2f; x < mx.x - 1.2f; x += 1.5f)
                {
                    b.Box(new Vector3(x, mn.y + 0.2f, z), new Vector3(x + 1.3f, mx.y - 0.25f, z + 0.012f), panel);
                    b.Box(new Vector3(x + 1.1f, mn.y + h * 0.45f, z - 0.03f), new Vector3(x + 1.16f, mn.y + h * 0.45f + 0.16f, z + 0.042f), brass);
                }
        }

        static void Collider(LabPiece p, Transform parent)
        {
            var go = new GameObject("Collider: " + p.name);
            go.transform.SetParent(parent, false);
            if (p.kind == LabPieceKind.Ramp)
            {
                var o = HouseLint.Of(p, false);
                go.transform.localPosition = U(o.c);
                go.transform.localRotation = Quaternion.LookRotation(U(o.u1), U(o.u2));
                go.AddComponent<BoxCollider>().size = new Vector3(o.h.X * 2f, o.h.Z * 2f, o.h.Y * 2f);
            }
            else
            {
                go.transform.localPosition = U(p.center);
                go.AddComponent<BoxCollider>().size = U(p.size);
            }
            go.AddComponent<MovementSurface>().runnable = p.runnable;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }

        // ---------- The corridor's door, Theo holding it, the fold ----------

        static Collider CorridorDoor(LabPiece p, Transform root, out Transform leaf)
        {
            var frame = new GameObject("The door at the end of the corridor").transform;
            frame.SetParent(root, false);
            frame.localPosition = new Vector3(p.Min.X, p.Min.Y, p.Max.Z);
            var col = frame.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(p.size.X * 0.5f, p.size.Y * 0.5f, -p.size.Z * 0.5f);
            col.size = U(p.size);
            frame.gameObject.AddComponent<MovementSurface>().runnable = false;
            // The lure: a warm lit front door; the game swings its leaves (EscapeDoubleDoor) when she bursts through.
            leaf = HouseEscapeDressing.FrontDoor(p, frame, out _);
            return col;
        }

        static GameObject TheoAtTheDoor(HouseEscape e, Transform root)
        {
            var c = e.corridor;
            float floor = e.rooms[3].floorTop - (c.doorS - c.startS) * c.slope;
            var theo = HouseFurnishing.CutOut("Theo (holding the door)", DreamArt.Theo, root, root.TransformPoint(new Vector3(-0.9f, floor, c.doorS - 0.6f)), HouseFurnishing.TheoCardHeight);
            theo.SetActive(false);
            return theo;
        }

        static Transform Fold(Transform root)
        {
            var fold = new GameObject("The house folding shut (pursuer)").transform;
            fold.SetParent(root, false);
            var b = new MeshBuilder();
            var paper = HouseEscapeLook.Oxblood();
            var plaster = HouseMaterials.Ceiling();
            b.Box(new Vector3(-6f, -6f, -0.4f), new Vector3(6f, 14f, 0f), new[] { paper, paper, plaster, plaster, paper, paper });
            b.Box(new Vector3(-1.3f, 0f, 0f), new Vector3(1.3f, 3.4f, 0.12f), Trim);
            for (int k = 0; k < 6; k++)
            {
                float y = -4f + k * 3f;
                b.Box(new Vector3(-6.2f, y, -0.6f), new Vector3(6.2f, y + 0.2f, 0.1f), HouseMaterials.TrimWalnut());
            }
            b.Build("Folding wall", fold);
            HouseEscapeDressing.Unstatic(fold.gameObject);   // it moves: never static-batched
            fold.gameObject.SetActive(false);
            return fold;
        }
    }
}
