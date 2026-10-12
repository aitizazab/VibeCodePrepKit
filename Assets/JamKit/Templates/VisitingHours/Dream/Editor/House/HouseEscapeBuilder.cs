using System.Collections.Generic;
using JamKit.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The escape (core <see cref="HouseEscape"/>), built upright at its origin: each room in what its surfaces used to be
    /// (a room on its side walks on its old wall, the old floor's boards run up one side and the old ceiling the other;
    /// upside down, the ceiling is underfoot), every wall and ceiling solid (a collider for every piece, the side walls
    /// runnable), the corridor's bays the same as each other (pictures, doors, lamps at the same places, so a stretch can't
    /// be seen), the door at its end, the house folding shut behind her, a night sky for the attic, lamps in every room.
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

        public static Result Build(HouseEscape e, Transform root)
        {
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
            var rest = new MeshBuilder();
            for (int i = 0; i < e.course.pieces.Count; i++)
            {
                var p = e.course.pieces[i];
                var look = e.looks[i];
                int r = Mathf.Max(0, e.RoomAt(p.center.Z));
                var room = e.rooms[r];
                var b = builders[r];
                if (i == e.corridorDoor)
                {
                    res.corridorDoor = CorridorDoor(p, root, out res.corridorDoorLeaf);
                    continue;
                }
                Piece(e, p, look, room, b);
                if (p.solid) Collider(p, colliders, look);
            }
            for (int r = 0; r < e.rooms.Count; r++)
            {
                Dress(e, e.rooms[r], builders[r], groups[r]);
                builders[r].Build("Room", groups[r]);
                RoomLamps(e, e.rooms[r], groups[r]);
            }
            rest.Build("Escape (between rooms)", root);
            res.rooms = groups;
            res.fold = Fold(root);
            res.theoAtTheDoor = TheoAtTheDoor(e, root);
            return res;
        }

        // ---------- The pieces, by what they used to be ----------

        static Material Wallpaper => HouseMaterials.Wallpaper();
        static Material Boards => HouseMaterials.Surface("wood_floor", null, null, 0.42f);
        static Material Plaster => HouseMaterials.Ceiling();
        static Material Wainscot => HouseMaterials.Surface("wooden_panels", "Walnut", new Color(0.62f, 0.42f, 0.3f), 0.4f);

        static void Piece(HouseEscape e, LabPiece p, HouseLook look, EscapeRoom room, MeshBuilder b)
        {
            Vector3 mn = U(p.Min), mx = U(p.Max);
            switch (look)
            {
                case HouseLook.EscWallpaperFloor: Top(b, p, room.roll == 180f ? Plaster : Wallpaper, Plaster); break;
                case HouseLook.EscCeiling:
                    if (p.kind == LabPieceKind.Ramp) Ramp(b, p, Plaster, Plaster);
                    else if (p.Max.Y <= room.floorTop + 0.5f) Top(b, p, Plaster, Plaster);   // a ceiling underfoot (upside down)
                    else Under(b, p, room.roll == 180f ? Boards : room.roll != 0f ? Wallpaper : Plaster);
                    break;
                case HouseLook.EscFloorboardWall: SideWall(b, p, room); break;
                case HouseLook.EscCorridorWall: CorridorWall(b, p, room); break;
                case HouseLook.EscCorridorFloor: Ramp(b, p, HouseMaterials.Fabric("CorridorRunner", new Color(0.36f, 0.42f, 0.55f)), Plaster); break;
                case HouseLook.EscTable: Ramp(b, p, HouseMaterials.TrimWalnut(), HouseMaterials.TrimWalnut()); break;
                case HouseLook.EscStairs: StepsOnRamp(b, p); break;
                case HouseLook.EscDoor: b.Box(mn, mx, HouseMaterials.Paint("EscapeDoor", new Color(0.93f, 0.91f, 0.86f))); break;
                case HouseLook.EscFurniture: b.Box(mn, mx, HouseMaterials.TrimWalnut()); break;
                case HouseLook.EscSkyBoards:
                case HouseLook.EscAtticBoards: b.Box(mn, mx, HouseMaterials.Planks()); break;
                case HouseLook.EscLastHall: Top(b, p, Boards, Plaster); break;
                case HouseLook.EscRain: b.Box(mn, mx, HouseMaterials.Paving()); break;
                default: b.Box(mn, mx, Wallpaper); break;
            }
        }

        static void Top(MeshBuilder b, LabPiece p, Material top, Material sides)
        {
            b.Box(U(p.Min), U(p.Max), new[] { sides, sides, sides, top, sides, sides });
        }

        static void Under(MeshBuilder b, LabPiece p, Material under)
        {
            var dark = HouseMaterials.Planks();
            b.Box(U(p.Min), U(p.Max), new[] { dark, dark, under, dark, dark, dark });
        }

        static void Ramp(MeshBuilder b, LabPiece p, Material top, Material under)
        {
            var o = HouseLint.Of(p, false);
            var edge = HouseMaterials.TrimWalnut();
            b.Obb(U(o.c), U(o.u0) * o.h.X, U(o.u1) * o.h.Y, U(o.u2) * o.h.Z, new[] { edge, edge, edge, edge, under, top });
        }

        static void StepsOnRamp(MeshBuilder b, LabPiece p)
        {
            // The staircase flattening into a slide: the treads drawn shallow, almost flat, on the slope.
            Ramp(b, p, HouseMaterials.Fabric("StairRunner", new Color(0.36f, 0.42f, 0.55f)), HouseMaterials.TrimWhite());
            float len = p.size.Z;
            int n = Mathf.RoundToInt(len / 0.7f);
            for (int k = 0; k < n; k++)
            {
                float z0 = p.Min.Z + len * k / n, t = (k + 0.5f) / n;
                float top = p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * t;
                b.Box(new Vector3(p.Min.X, top, z0), new Vector3(p.Max.X, top + 0.012f, z0 + 0.06f), HouseMaterials.TrimWalnut());
            }
        }

        /// <summary>A room's side wall: only its inner face shows (what the room's turn makes it), with the old room's things
        /// on it.</summary>
        static void SideWall(MeshBuilder b, LabPiece p, EscapeRoom room)
        {
            bool left = p.center.X < 0f;
            Material inner;
            if (room.roll == 0f) inner = Wallpaper;
            else if (room.roll == 180f) inner = Wallpaper;
            else inner = (room.roll > 0f) == left ? Plaster : Boards;   // on its side: the old floor one side, the old ceiling the other
            var back = HouseMaterials.Planks();
            var faces = new[] { left ? back : inner, left ? inner : back, back, back, back, back };
            b.Box(U(p.Min), U(p.Max), faces);
            if (room.roll == 0f && room.halfWidth >= 2f)
            {
                // The wainscot band at the right way up.
                float x = left ? p.Max.X : p.Min.X, d = left ? 0.012f : -0.012f;
                b.Box(new Vector3(Mathf.Min(x, x + d), room.floorTop, p.Min.Z), new Vector3(Mathf.Max(x, x + d), room.floorTop + 0.9f, p.Max.Z), Wainscot);
            }
        }

        static void CorridorWall(MeshBuilder b, LabPiece p, EscapeRoom room)
        {
            bool runs = p.runnable;
            b.Box(U(p.Min), U(p.Max), Wallpaper);
            if (!runs) return;
            // The wainscot along the corridor (it slopes with the floor: drawn in short steps).
            bool left = p.center.X < 0f;
            float x = left ? p.Max.X : p.Min.X, d = left ? 0.014f : -0.014f;
            for (float z = p.Min.Z; z < p.Max.Z - 0.01f; z += 2f)
            {
                float z1 = Mathf.Min(p.Max.Z, z + 2f);
                float floor = room.floorTop - (z + 1f - room.s0) * 0.04f;
                b.Box(new Vector3(Mathf.Min(x, x + d), floor - 0.05f, z), new Vector3(Mathf.Max(x, x + d), floor + 0.95f, z1), Wainscot);
            }
        }

        static void Collider(LabPiece p, Transform parent, HouseLook look)
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
            // The leaf on its hinge (the left jamb): red, panelled, light round its edges.
            leaf = new GameObject("Leaf (hinge)").transform;
            leaf.SetParent(frame, false);
            var b = new MeshBuilder();
            var red = HouseMaterials.Paint("FrontDoorRed", new Color(0.66f, 0.16f, 0.14f), 1.4f);
            b.Box(new Vector3(0f, 0f, -0.12f), new Vector3(p.size.X, p.size.Y, 0f), red);
            for (int k = 0; k < 2; k++)
                for (int j = 0; j < 2; j++)
                {
                    float x0 = 0.25f + k * (p.size.X * 0.5f - 0.1f), y0 = 0.3f + j * (p.size.Y * 0.5f - 0.1f);
                    b.Box(new Vector3(x0, y0, -0.135f), new Vector3(x0 + p.size.X * 0.5f - 0.4f, y0 + p.size.Y * 0.5f - 0.45f, -0.12f), HouseMaterials.Paint("FrontDoorPanel", new Color(0.58f, 0.13f, 0.12f), 1.4f));
                }
            b.Build("The leaf", leaf);
            // The light round its edges (the front door's daylight): thin glowing strips.
            var glow = DreamEscapeBuilder.Glow("CorridorDoorLight", new Color(1f, 0.9f, 0.7f), 1.4f, 0f, 0.2f);
            DreamEscapeBuilder.Box("Light under the door", frame, frame.TransformPoint(new Vector3(p.size.X * 0.5f, 0.015f, 0.02f)), new Vector3(p.size.X, 0.02f, 0.02f), glow);
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
            b.Box(new Vector3(-6f, -6f, -0.4f), new Vector3(6f, 14f, 0f), new[] { Wallpaper, Wallpaper, Plaster, Plaster, Wallpaper, Wallpaper });
            b.Box(new Vector3(-1.3f, 0f, 0f), new Vector3(1.3f, 3.4f, 0.12f), HouseMaterials.TrimWhite());
            for (int k = 0; k < 6; k++)
            {
                float y = -4f + k * 3f;
                b.Box(new Vector3(-6.2f, y, -0.6f), new Vector3(6.2f, y + 0.2f, 0.1f), HouseMaterials.TrimWalnut());
            }
            b.Build("Folding wall", fold);
            fold.gameObject.SetActive(false);
            return fold;
        }

        // ---------- Dressing and lamps ----------

        /// <summary>Each room's things: picture frames on its walls the way they hang now (on their sides, upside down,
        /// lying on the floor that was a wall), the corridor's bays the same as each other.</summary>
        static void Dress(HouseEscape e, EscapeRoom room, MeshBuilder b, Transform group)
        {
            var frame = HouseMaterials.TrimWalnut();
            float hw = room.halfWidth;
            if (room.theme == "corridor")
            {
                var c = e.corridor;
                // From the door back, a bay at a time: on each side a door and a photo, a wall lamp between.
                for (float z = c.doorS - c.bay; z > c.startS + 2f; z -= c.bay)
                {
                    float floor = room.floorTop - (z - c.startS) * c.slope;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * hw;
                        float d = -side * 0.03f;
                        // A closed door (a panel and its casing) at the same place in every bay.
                        b.Box(new Vector3(Mathf.Min(x, x + d), floor, z + 4f), new Vector3(Mathf.Max(x, x + d), floor + 2.1f, z + 5f), HouseMaterials.Paint("CorridorDoors", new Color(0.92f, 0.9f, 0.84f)));
                        // A photo: the same photo in every bay.
                        b.Box(new Vector3(Mathf.Min(x, x + d * 0.6f), floor + 1.4f, z + 11f), new Vector3(Mathf.Max(x, x + d * 0.6f), floor + 1.9f, z + 11.6f), frame);
                        var pic = HouseFurnishing.PictureMaterial("dream_pic_photo_3", DreamArt.Get("dream_pic_photo_3", out _));
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        Object.DestroyImmediate(q.GetComponent<Collider>());
                        q.transform.SetParent(group, false);
                        q.transform.localPosition = new Vector3(x + d * 0.6f - side * 0.002f, floor + 1.65f, z + 11.3f);
                        q.transform.localRotation = Quaternion.LookRotation(new Vector3(side, 0f, 0f), Vector3.up);
                        q.transform.localScale = new Vector3(0.52f, 0.42f, 1f);
                        q.GetComponent<MeshRenderer>().sharedMaterial = pic;
                    }
                }
                return;
            }
            if (room.open) return;
            // Frames along the walls every 6 m, hung by the room's turn.
            for (float z = room.s0 + 3f; z < room.s1 - 2f; z += 6f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * hw, d = -side * 0.03f;
                    float y = room.floorTop + (room.roll == 180f ? 4.2f : 1.5f);
                    float w = room.roll == 0f || room.roll == 180f ? 0.6f : 0.45f, h = room.roll == 0f || room.roll == 180f ? 0.45f : 0.6f;
                    b.Box(new Vector3(Mathf.Min(x, x + d), y, z), new Vector3(Mathf.Max(x, x + d), y + h, z + w), frame);
                }
            }
        }

        static void RoomLamps(HouseEscape e, EscapeRoom room, Transform group)
        {
            var c = new Color(room.ambient.X, room.ambient.Y, room.ambient.Z);
            float end = room.s1 > room.s0 ? room.s1 : room.s0 + 30f;
            // The corridor's lamps keep its bays' rhythm (one a bay, at the same place in each), so a stretch can't be seen.
            bool corridor = room.theme == "corridor";
            float step = corridor ? e.corridor.bay : 12f;
            float startZ = corridor ? e.corridor.doorS - e.corridor.bay * 0.5f : room.s0 + 5f;
            if (corridor) while (startZ - step > room.s0 + 2f) startZ -= step;
            for (float z = startZ; z < end - 2f; z += step)
            {
                if (corridor)
                {
                    var cl = new GameObject("Lamp (corridor)");
                    cl.transform.SetParent(group, false);
                    cl.transform.localPosition = new Vector3(0f, room.floorTop - (z - e.corridor.startS) * e.corridor.slope + 2.6f, z);
                    var lc = cl.AddComponent<Light>();
                    lc.type = LightType.Point; lc.range = 9f; lc.intensity = 2.0f; lc.color = new Color(1f, 0.78f, 0.55f); lc.shadows = LightShadows.None;
                    continue;
                }
                var go = new GameObject("Lamp");
                go.transform.SetParent(group, false);
                go.transform.localPosition = new Vector3(0f, room.floorTop + (room.open ? 4f : Mathf.Min(3.2f, room.ceilingY - room.floorTop - 0.6f)), z);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = room.open ? 16f : 11f;
                l.intensity = room.open ? 1.6f : 2.2f;
                l.color = Color.Lerp(new Color(1f, 0.76f, 0.5f), c, 0.4f);
                l.shadows = LightShadows.None;
            }
        }

        /// <summary>A night sky for the attic open to the stars: a big inverted sphere on the camera, drawn behind
        /// everything (the game shows it only there).</summary>
        public static GameObject NightSky(Camera cam)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Night sky (the attic open to the stars)";
            go.transform.SetParent(cam.transform, false);
            go.transform.localScale = Vector3.one * 500f;
            var mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            var tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3) { int t = tris[i]; tris[i] = tris[i + 1]; tris[i + 1] = t; }
            mesh.triangles = tris;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            string dir = DreamEscapeBuilder.Root + "/Generated/House";
            DreamEscapeBuilder.EnsureFolder(dir);
            string texPath = dir + "/house_night_sky.png";
            if (!System.IO.File.Exists(texPath))
            {
                const int n = 1024;
                var t = new Texture2D(n, n / 2, TextureFormat.RGB24, false);
                var r = new System.Random(11);
                for (int y = 0; y < n / 2; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float k = y / (float)(n / 2);
                        var c = Color.Lerp(new Color(0.12f, 0.1f, 0.24f), new Color(0.03f, 0.04f, 0.12f), k);
                        if (r.NextDouble() < 0.0025) c = Color.Lerp(c, new Color(1f, 0.95f, 0.85f), 0.6f + (float)r.NextDouble() * 0.4f);
                        t.SetPixel(x, y, c);
                    }
                t.Apply();
                System.IO.File.WriteAllBytes(texPath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(texPath);
            }
            var mat = DreamEscapeBuilder.Mat("House_NightSky", "Universal Render Pipeline/Unlit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.renderQueue = 1000;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            return go;
        }
    }
}
