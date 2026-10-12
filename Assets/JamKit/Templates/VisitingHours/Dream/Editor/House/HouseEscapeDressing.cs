using System.Collections.Generic;
using JamKit.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The escape's rooms dressed as the rooms they were (house-1.md: "one recoloured corridor box"; "nothing reads as
    /// tipped, rolled, turning or flattening"). Each room has its own palette (<see cref="HouseEscapeLook"/>) and its own
    /// things, the house's real furniture (<see cref="HouseFurnishing.Prop"/>) placed by the room's turn: on its side the old
    /// floor is a side wall (its chairs, cabinet and lamp stand out of it), the old ceiling the other side wall (the pendants
    /// hang out of it), the old walls are the floor and the ceiling (the wainscot, the dado rail, the skirting, the pictures
    /// and a window lie on them); upside down, Theo's bed, desk and bookcase hang from the ceiling and his glow stars are
    /// underfoot. The furniture and lamps of a turned room are in a group that tips into place as she comes to it
    /// (<see cref="EscapeRoomTip"/>); pendants swing (<see cref="EscapeSwing"/>). The corridor has the same landmarks in every
    /// bay (a threshold strip in the runner, a closed side door, a picture, a pair of sconces and a ceiling lamp) so a
    /// stretch can be read, a continuous sloped dado; its end is a warm front door. The attic floats under a night sky; the
    /// last hall is the house's hall, its front door open onto a warm glow. Nothing here has a collider (the collision is
    /// <see cref="HouseEscape"/>'s, tested in dotnet), and nothing is placed where it would cross a solid of the course or
    /// the runner's line (<see cref="Clear"/>: skipped things are logged).
    /// </summary>
    public static class HouseEscapeDressing
    {
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>One room's build state: its palette, its groups, the turn (old room frame to the escape's).</summary>
        sealed class Ctx
        {
            public HouseEscape e;
            public EscapeRoom room;
            public int ri;
            public HouseEscapeLook.Palette pal;
            public Transform group, tips, escapeRoot;
            /// <summary>Static (on surfaces that don't move) and tipping (furniture, lamps) meshes.</summary>
            public MeshBuilder b, tb;
            public HouseEscapeKit kb, tkb;
            /// <summary>The tipping group's pivot (the route's axis at the room's mid-height) and its turn.</summary>
            public Vector3 pivot;
            public Quaternion rot = Quaternion.identity;
            /// <summary>The old room's half extents in its own frame: floor at y -A, ceiling at +A, walls at x ±B.</summary>
            public float A, B;
            public int lights;
        }

        static int _skipped;
        static readonly List<string> _skips = new List<string>();

        public static void Begin() { _skipped = 0; _skips.Clear(); }

        public static void End()
        {
            if (_skipped > 0) Debug.Log("JAMKIT_HOUSE_ESCAPE dressing left out " + _skipped + " things that would cross the course or her line: " + string.Join("; ", _skips));
        }

        // =====================================================================================================
        // Entry
        // =====================================================================================================

        /// <summary>Dresses room <paramref name="ri"/> into <paramref name="group"/> (its static things into
        /// <paramref name="b"/>, the room's own mesh).</summary>
        public static void Dress(HouseEscape e, int ri, Transform group, Transform escapeRoot, MeshBuilder b)
        {
            var room = e.rooms[ri];
            var c = new Ctx { e = e, room = room, ri = ri, pal = HouseEscapeLook.For(room), group = group, escapeRoot = escapeRoot, b = b, tb = new MeshBuilder(), kb = new HouseEscapeKit(), tkb = new HouseEscapeKit() };
            bool turned = room.roll != 0f;
            c.tips = new GameObject(turned ? "Its things (they tip over as she comes)" : "Its things").transform;
            c.tips.SetParent(group, false);
            if (turned)
            {
                bool side = Mathf.Abs(Mathf.Abs(room.roll) - 90f) < 1f;
                float half = (room.ceilingY - room.floorTop) * 0.5f;
                c.A = side ? room.halfWidth : half;
                c.B = side ? half : room.halfWidth;
                c.pivot = new Vector3(0f, room.floorTop + half, 0f);
                c.rot = Quaternion.Euler(0f, 0f, room.roll);
                c.tips.localPosition = c.pivot;
            }
            switch (room.theme)
            {
                case "dining": Dining(c); break;
                case "hall": Hall(c); break;
                case "stairs": Stairs(c); break;
                case "corridor": Corridor(c); break;
                case "theo": Theo(c); break;
                case "kitchen": Kitchen(c); break;
                case "attic": Attic(c); break;
                case "lasthall": LastHall(c); break;
            }
            Fill(c);
            // The tipping group's meshes, then its final turn (the stills show the room as it lies; the game tips it in).
            c.tb.Build("Things (built)", c.tips);
            c.tkb.Build("Things (round)", c.tips);
            c.kb.Build("Trim (round)", group);
            if (turned)
            {
                c.tips.localRotation = c.rot;
                var tip = c.tips.gameObject.AddComponent<EscapeRoomTip>();
                tip.space = escapeRoot;
                tip.roll = room.roll;
                tip.fromRoll = room.roll - Mathf.Sign(room.roll) * 40f;
                tip.triggerZ = room.s0 - 6f;
                tip.snapZ = room.s0 + 10f;
            }
            Unstatic(c.tips.gameObject);
        }

        /// <summary>Things that move are never static (static batching would leave them where they were built).</summary>
        public static void Unstatic(GameObject go)
        {
            if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        // =====================================================================================================
        // Placing: the old room's frame, clearance, props, lamps
        // =====================================================================================================

        /// <summary>A point of the old room (the tipping group's frame) where it ends up in the escape's frame.</summary>
        static Vector3 E(Ctx c, Vector3 old) => c.pivot + c.rot * old;

        static Bounds Map(Ctx c, Vector3 mn, Vector3 mx)
        {
            var bb = new Bounds(E(c, mn), Vector3.zero);
            for (int k = 1; k < 8; k++)
                bb.Encapsulate(E(c, new Vector3((k & 1) == 0 ? mn.x : mx.x, (k & 2) == 0 ? mn.y : mx.y, (k & 4) == 0 ? mn.z : mx.z)));
            return bb;
        }

        /// <summary>The top of the floor under her line at <paramref name="z"/> (or the last one before it, over a gap).</summary>
        static float FloorAt(HouseEscape e, float x, float z)
        {
            for (float back = 0f; back < 40f; back += 1f)
            {
                float zz = z - back, best = float.NaN;
                foreach (var p in e.course.pieces)
                {
                    if (!p.solid || (p.kind != LabPieceKind.Floor && p.kind != LabPieceKind.Ramp)) continue;
                    var mn = p.Min; var mx = p.Max;
                    if (x < mn.X - 0.3f || x > mx.X + 0.3f || zz < mn.Z || zz > mx.Z) continue;
                    float top = p.kind == LabPieceKind.Ramp ? p.rampTopStart + (p.rampTopEnd - p.rampTopStart) * Mathf.Clamp01((zz - mn.Z) / Mathf.Max(1e-3f, p.size.Z)) : mx.Y;
                    if (top > 30f) continue;
                    if (float.IsNaN(best) || top > best) best = top;
                }
                if (!float.IsNaN(best)) return best;
            }
            return 0f;
        }

        /// <summary>Whether a box (escape frame) is clear of every solid of the course (a touch is fine) and of her line
        /// (a body 1.2 m wide and 2.4 m tall over the floor under the lane, 3.4 m on a wall run), 2 m either side of it
        /// along the route.</summary>
        static bool Clear(Ctx c, Bounds bb, string what)
        {
            const float touch = 0.04f;
            var e = c.e;
            for (int i = 0; i < e.course.pieces.Count; i++)
            {
                var p = e.course.pieces[i];
                if (!p.solid) continue;
                Vector3 mn, mx;
                if (p.kind == LabPieceKind.Ramp) { var o = HouseLint.Of(p, false); mn = U(o.Min); mx = U(o.Max); }
                else { mn = U(p.Min); mx = U(p.Max); }
                if (bb.max.x - touch > mn.x && bb.min.x + touch < mx.x && bb.max.y - touch > mn.y && bb.min.y + touch < mx.y && bb.max.z - touch > mn.z && bb.min.z + touch < mx.z)
                {
                    Skip(what + " (crosses " + p.name + ")");
                    return false;
                }
            }
            for (float z = bb.min.z - 2f; z <= bb.max.z + 2f; z += 0.5f)
            {
                float lx = e.course.LaneAt(z);
                float fy = FloorAt(e, lx, z);
                float top = Mathf.Abs(lx) > 2.5f ? 3.6f : 2.5f;
                if (bb.max.x > lx - 0.65f && bb.min.x < lx + 0.65f && bb.max.y > fy - 0.05f && bb.min.y < fy + top)
                {
                    Skip(what + " (in her line at " + z.ToString("0") + ")");
                    return false;
                }
            }
            return true;
        }

        static void Skip(string what)
        {
            _skipped++;
            if (_skips.Count < 40) _skips.Add(what);
        }

        /// <summary>A piece of the house's furniture standing in the old room (old frame box), if it's clear.</summary>
        static GameObject Thing(Ctx c, string role, Vector3 mn, Vector3 mx, float yaw, string name)
        {
            if (!Clear(c, Map(c, mn, mx), name)) return null;
            var go = HouseFurnishing.Prop(role, c.tips, mn, mx, yaw, name);
            Unstatic(go);
            return go;
        }

        /// <summary>A box on a surface that doesn't move (the old walls: the escape's floor and ceiling; trims), given in the
        /// old room's frame.</summary>
        static void SBox(Ctx c, Vector3 mn, Vector3 mx, Material m)
        {
            var bb = Map(c, mn, mx);
            c.b.Box(bb.min, bb.max, m);
        }

        static Light Lamp(Ctx c, Transform parent, Vector3 local, float range, float intensity, Color colour, string name)
        {
            var go = new GameObject("Lamp: " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = colour;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto;
            c.lights++;
            return l;
        }

        /// <summary>A pendant: its rose at <paramref name="rose"/> (the tipping group's frame for a turned room), its cord
        /// <paramref name="drop"/> long, the house's ceiling lamp at its end, lit, swinging on its cord.</summary>
        static void Pendant(Ctx c, Vector3 rose, float drop, string name, float phase)
        {
            // Hanging "down" in the old room's frame.
            var lo = rose - new Vector3(0.3f, drop + 0.05f, 0.3f);
            var hi = rose + new Vector3(0.3f, 0f, 0.3f);
            if (!Clear(c, Map(c, lo, hi), name)) return;
            var swing = new GameObject("Pendant (swinging): " + name).transform;
            swing.SetParent(c.tips, false);
            swing.localPosition = rose;
            var sw = swing.gameObject.AddComponent<EscapeSwing>();
            sw.axis = Vector3.right;
            sw.amplitude = 6f + phase * 4f;
            sw.period = 2.4f + phase;
            sw.phase = phase;
            var k = new HouseEscapeKit();
            k.Rod(Vector3.zero, new Vector3(0f, -(drop - 0.9f), 0f), 0.008f, HouseEscapeLook.Iron(), 6);
            k.Lathe(Vector3.zero, Vector3.up, new[] { new Vector2(0.001f, -0.03f), new Vector2(0.07f, -0.03f), new Vector2(0.06f, 0f) }, HouseEscapeLook.Brass(), 14);
            k.Ball(new Vector3(0f, -drop + 0.22f, 0f), 0.05f, HouseEscapeLook.Glow("Bulb", new Color(1f, 0.82f, 0.56f), 6f), 6);
            k.Build("Cord and bulb", swing, false, false);
            var lamp = HouseFurnishing.Prop("dining.pendant_lamp", swing, new Vector3(-0.22f, -drop, -0.22f), new Vector3(0.22f, -drop + 0.95f, 0.22f), 0f, "Lamp");
            Unstatic(lamp);
            if (lamp != null) foreach (var r in lamp.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            Lamp(c, swing, new Vector3(0f, -drop + 0.15f, 0f), 8f, 1.7f, c.pal.lamp, name);
        }

        /// <summary>A framed picture: a frame (on <paramref name="b"/>) and the picture on its face (a quad), on a face at
        /// <paramref name="face"/> looking along <paramref name="normal"/> (an axis), <paramref name="w"/> x
        /// <paramref name="h"/>; <paramref name="up"/> is the picture's up on that face.</summary>
        static void Picture(Ctx c, Transform parent, MeshBuilder b, string art, Vector3 face, Vector3 normal, Vector3 up, float w, float h, string name)
        {
            var right = Vector3.Cross(up, normal).normalized;
            const float depth = 0.03f, rim = 0.045f;
            // The frame: four bars round the picture, proud of the face.
            var trim = HouseMaterials.TrimWalnut();
            void Bar(Vector3 center, Vector3 half)
            {
                var a = center - half; var z = center + half;
                b.Box(Vector3.Min(a, z), Vector3.Max(a, z), trim);
            }
            var cc = face + normal * (depth * 0.5f);
            var hn = normal * (depth * 0.5f);
            Bar(cc + up * (h * 0.5f - rim * 0.5f), right * (w * 0.5f) + up * (rim * 0.5f) + hn);
            Bar(cc - up * (h * 0.5f - rim * 0.5f), right * (w * 0.5f) + up * (rim * 0.5f) + hn);
            Bar(cc + right * (w * 0.5f - rim * 0.5f), right * (rim * 0.5f) + up * (h * 0.5f - rim) + hn);
            Bar(cc - right * (w * 0.5f - rim * 0.5f), right * (rim * 0.5f) + up * (h * 0.5f - rim) + hn);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Picture: " + name;
            q.transform.SetParent(parent, false);
            q.transform.localPosition = face + normal * 0.006f;
            q.transform.localRotation = Quaternion.LookRotation(-normal, up);
            q.transform.localScale = new Vector3(w - rim * 2f + 0.01f, h - rim * 2f + 0.01f, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = HouseFurnishing.PictureMaterial(art, DreamArt.Get(art, out _));
            q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static readonly string[] Photos = { "dream_pic_photo_1", "dream_pic_photo_2", "dream_pic_photo_3", "dream_pic_photo_4", "dream_pic_photo_5", "dream_pic_photo_6", "dream_pic_childhood_1", "dream_pic_childhood_2", "dream_pic_childhood_3", "dream_pic_childhood_4", "dream_pic_siblings" };

        /// <summary>The room's floors (pieces whose tops are under her: the old walls on its side, the old ceiling upside
        /// down) and its ceiling piece.</summary>
        static List<LabPiece> Floors(Ctx c)
        {
            var list = new List<LabPiece>();
            for (int i = 0; i < c.e.course.pieces.Count; i++)
            {
                var p = c.e.course.pieces[i];
                if (p.kind != LabPieceKind.Floor || !p.solid) continue;
                var look = c.e.looks[i];
                if (look == HouseLook.EscRain) continue;
                if (look == HouseLook.EscCeiling && p.Max.Y > c.room.floorTop + 1f) continue;   // a ceiling over her
                if (c.e.RoomAt(p.center.Z) != c.ri) continue;
                list.Add(p);
            }
            return list;
        }

        static LabPiece Ceiling(Ctx c)
        {
            for (int i = 0; i < c.e.course.pieces.Count; i++)
            {
                var p = c.e.course.pieces[i];
                if (p.kind == LabPieceKind.Floor && c.e.looks[i] == HouseLook.EscCeiling && p.Max.Y > c.room.floorTop + 1f && p.name.StartsWith(c.room.name)) return p;
            }
            return null;
        }

        /// <summary>The old walls' pattern on a turned room's floors and ceiling: the wainscot under its dado rail by the old
        /// floor, the skirting at the old floor's edge, the cornice at the old ceiling's (each lies on every floor piece and on
        /// the ceiling: the old wall continues there).</summary>
        static void OldWallBands(Ctx c)
        {
            var pal = c.pal;
            float hw = c.room.halfWidth;
            float sgn = c.room.roll > 0f ? 1f : -1f;           // the old floor's side (+x at 90, -x at -90)
            float floorX = sgn * hw, ceilX = -sgn * hw;
            void Strip(float x0, float x1, float y0, float y1, float z0, float z1, Material m)
            {
                if (m == null || z1 - z0 < 0.05f) return;
                c.b.Box(new Vector3(Mathf.Min(x0, x1), y0, z0), new Vector3(Mathf.Max(x0, x1), y1, z1), m);
            }
            void Bands(float y, int up, float z0, float z1)
            {
                // up +1: on a floor's top; -1: under the ceiling.
                float a = y, d = up;
                if (pal.wainscot != null) Strip(floorX, floorX - sgn * pal.wainscotHeight, Mathf.Min(a, a + d * 0.004f), Mathf.Max(a, a + d * 0.004f), z0, z1, pal.wainscot);
                if (pal.rail != null && pal.wainscot != null) Strip(floorX - sgn * (pal.wainscotHeight - 0.03f), floorX - sgn * (pal.wainscotHeight + 0.03f), Mathf.Min(a, a + d * 0.022f), Mathf.Max(a, a + d * 0.022f), z0, z1, pal.rail);
                Strip(floorX, floorX - sgn * 0.13f, Mathf.Min(a, a + d * 0.018f), Mathf.Max(a, a + d * 0.018f), z0, z1, pal.skirting);
                Strip(ceilX, ceilX + sgn * 0.11f, Mathf.Min(a, a + d * 0.07f), Mathf.Max(a, a + d * 0.07f), z0, z1, pal.cornice);
            }
            foreach (var p in Floors(c)) Bands(p.Max.Y, 1, p.Min.Z, p.Max.Z);
            var ceil = Ceiling(c);
            if (ceil != null) Bands(ceil.Min.Y, -1, ceil.Min.Z, ceil.Max.Z);
        }

        /// <summary>Pictures lying on a turned room's floors and hanging on its ceiling (the old walls' photos), at
        /// <paramref name="x"/> across, every <paramref name="every"/> metres.</summary>
        static void OldWallPictures(Ctx c, float x, float every, float w, float h, int seed)
        {
            int k = seed;
            foreach (var p in Floors(c))
                for (float z = p.Min.Z + 1.2f; z < p.Max.Z - 1.2f; z += every)
                {
                    var face = new Vector3(x, p.Max.Y, z);
                    // Flat on the floor: she runs over them.
                    Picture(c, c.group, c.b, Photos[k++ % Photos.Length], face, Vector3.up, Vector3.right * Mathf.Sign(c.room.roll), w, h, "on the floor");
                }
            var ceil = Ceiling(c);
            if (ceil == null) return;
            for (float z = ceil.Min.Z + 2f; z < ceil.Max.Z - 2f; z += every * 0.8f)
                Picture(c, c.group, c.b, Photos[k++ % Photos.Length], new Vector3(x * 0.92f, ceil.Min.Y, z + 1.3f), Vector3.down, Vector3.right * Mathf.Sign(c.room.roll), w, h, "on the ceiling");
        }

        /// <summary>A window in an old wall that's now the ceiling: frame, glazing bars, and the evening sky outside it.</summary>
        static void CeilingWindow(Ctx c, float x0, float x1, float z0, float z1)
        {
            var ceil = Ceiling(c);
            if (ceil == null) return;
            float y = ceil.Min.Y;
            var sky = HouseEscapeLook.Glow("WindowSky", new Color(1f, 0.78f, 0.6f), 1.6f);
            var trim = HouseEscapeLook.Clean("TrimWhite", new Color(0.93f, 0.91f, 0.86f), 0.45f);
            c.b.Box(new Vector3(x0, y - 0.006f, z0), new Vector3(x1, y - 0.002f, z1), new[] { null, null, sky, null, null, null });
            const float t = 0.08f;
            c.b.Box(new Vector3(x0 - t, y - 0.05f, z0 - t), new Vector3(x1 + t, y, z0), trim);
            c.b.Box(new Vector3(x0 - t, y - 0.05f, z1), new Vector3(x1 + t, y, z1 + t), trim);
            c.b.Box(new Vector3(x0 - t, y - 0.05f, z0), new Vector3(x0, y, z1), trim);
            c.b.Box(new Vector3(x1, y - 0.05f, z0), new Vector3(x1 + t, y, z1), trim);
            float xm = (x0 + x1) * 0.5f, zm = (z0 + z1) * 0.5f;
            c.b.Box(new Vector3(xm - 0.015f, y - 0.03f, z0), new Vector3(xm + 0.015f, y, z1), trim);
            c.b.Box(new Vector3(x0, y - 0.03f, zm - 0.015f), new Vector3(x1, y, zm + 0.015f), trim);
            Lamp(c, c.group, new Vector3(xm, y - 1.2f, zm), 6f, 0.8f, new Color(1f, 0.8f, 0.62f), "the window's light");
        }

        /// <summary>A rug flat on an old floor that's now a side wall (static: it lies on the wall).</summary>
        static void WallRug(Ctx c, float y0, float y1, float z0, float z1)
        {
            float sgn = c.room.roll > 0f ? 1f : -1f, x = sgn * c.room.halfWidth;
            c.b.Box(new Vector3(Mathf.Min(x, x - sgn * 0.012f), y0, z0), new Vector3(Mathf.Max(x, x - sgn * 0.012f), y1, z1), c.pal.rug);
            float bw = 0.12f;
            c.b.Box(new Vector3(Mathf.Min(x, x - sgn * 0.014f), y0 + bw, z0 + bw), new Vector3(Mathf.Max(x, x - sgn * 0.014f), y0 + bw * 1.5f, z1 - bw), c.pal.rugBorder);
            c.b.Box(new Vector3(Mathf.Min(x, x - sgn * 0.014f), y1 - bw * 1.5f, z0 + bw), new Vector3(Mathf.Max(x, x - sgn * 0.014f), y1 - bw, z1 - bw), c.pal.rugBorder);
        }

        /// <summary>Room-filling light: soft, shadowless, warm, every 12 m along the middle (no pure black corners).</summary>
        static void Fill(Ctx c)
        {
            var r = c.room;
            if (r.theme == "corridor") return;   // its lamps keep the bays' rhythm (Corridor)
            float end = r.s1 > r.s0 ? r.s1 : r.s0 + 30f;
            var amb = new Color(r.ambient.X, r.ambient.Y, r.ambient.Z);
            var col = Color.Lerp(c.pal.lamp, amb, 0.35f);
            float h = r.open ? 4f : Mathf.Min(4.2f, (r.ceilingY - r.floorTop) * 0.55f);
            for (float z = r.s0 + 6f; z < end - 2f; z += 16f)
            {
                float y = FloorAt(c.e, 0f, z) + h;
                Lamp(c, c.group, new Vector3(0f, y, z), r.open ? 22f : 14f, r.open ? 0.7f : 1.25f, col, "fill");
            }
        }

        // =====================================================================================================
        // The rooms
        // =====================================================================================================

        /// <summary>The dining room tips (on its side: the old floor is the right wall): the chairs fallen against it, the
        /// china cabinet, a lamp and a plant; the pendants out of the left wall; sage stripe over walnut underfoot and
        /// overhead; the photos; a window in the ceiling.</summary>
        static void Dining(Ctx c)
        {
            float A = c.A, B = c.B, s0 = c.room.s0, s1 = c.room.s1;
            OldWallBands(c);
            OldWallPictures(c, Mathf.Sign(c.room.roll) * (c.room.halfWidth - 1.65f), 4.5f, 0.55f, 0.42f, 0);
            CeilingWindow(c, Mathf.Sign(c.room.roll) * 2.2f - 0.7f, Mathf.Sign(c.room.roll) * 2.2f + 0.7f, s0 + 11f, s0 + 12.6f);
            WallRug(c, c.pivot.y - 1.6f, c.pivot.y + 1.6f, s0 + 4f, s0 + 10.5f);
            // The four chairs, fallen two and two against what was the floor.
            foreach (float z in new[] { s0 + 5.5f, s0 + 8.5f })
            {
                Thing(c, "dining.chair", new Vector3(-1.25f, -A, z), new Vector3(-0.7f, -A + 0.96f, z + 0.45f), 90f, "Chair");
                Thing(c, "dining.chair", new Vector3(0.7f, -A, z), new Vector3(1.25f, -A + 0.96f, z + 0.45f), -90f, "Chair");
            }
            Thing(c, "dining.sideboard", new Vector3(B - 0.62f, -A, s0 + 17f), new Vector3(B, -A + 1.18f, s0 + 18.2f), -90f, "The china cabinet");
            Thing(c, "living.floor_lamp", new Vector3(-B + 0.3f, -A, s0 + 2f), new Vector3(-B + 0.65f, -A + 1.6f, s0 + 2.35f), 0f, "Floor lamp");
            Thing(c, "living.plant.3", new Vector3(B - 0.6f, -A, s0 + 24f), new Vector3(B - 0.1f, -A + 0.9f, s0 + 24.5f), 0f, "Plant");
            Thing(c, "dining.chair", new Vector3(-2.6f, -A, s1 - 4f), new Vector3(-2.05f, -A + 0.96f, s1 - 3.55f), 120f, "Chair (slid away)");
            Pendant(c, new Vector3(0f, A, s0 + 7f), 1.0f, "dining, over where the table was", 0f);
            Pendant(c, new Vector3(0f, A, s0 + 24f), 1.0f, "dining, far end", 0.4f);
        }

        /// <summary>The hall turns over (on its side): the photo wall underfoot and overhead, the console, the coat stand and
        /// the umbrella stand on the right wall (the old floor), the pendants out of the left; the doorframes papered and cased.</summary>
        static void Hall(Ctx c)
        {
            float A = c.A, B = c.B, s0 = c.room.s0, s1 = c.room.s1;
            OldWallBands(c);
            OldWallPictures(c, Mathf.Sign(c.room.roll) * (c.room.halfWidth - 1.9f), 2.6f, 0.4f, 0.5f, 3);
            OldWallPictures(c, Mathf.Sign(c.room.roll) * (c.room.halfWidth - 2.9f), 3.4f, 0.5f, 0.38f, 7);
            WallRug(c, c.pivot.y - 0.7f, c.pivot.y + 0.7f, s0 + 3f, s0 + 14f);
            Thing(c, "hall.console", new Vector3(1.0f, -A, s0 + 6f), new Vector3(1.38f, -A + 0.76f, s0 + 6.38f), 90f, "Hall console");
            Thing(c, "hall.coat_rack", new Vector3(2.2f, -A, s0 + 9f), new Vector3(2.6f, -A + 1.8f, s0 + 9.4f), 0f, "Coat stand");
            Thing(c, "living.plant.3", new Vector3(-0.5f, -A, s0 + 12f), new Vector3(0f, -A + 0.9f, s0 + 12.5f), 0f, "Plant");
            for (float z = s0 + 30f; z < s1 - 6f; z += 16f)
                Thing(c, "hall.console", new Vector3(2.0f, -A, z), new Vector3(2.38f, -A + 0.76f, z + 0.38f), 90f, "Hall console (again)");
            for (float z = s0 + 8f; z < s1 - 4f; z += 22f)
                Pendant(c, new Vector3(0.5f, A, z), 0.95f, "hall", (z * 0.13f) % 1f);
        }

        /// <summary>The stairs flatten: dusty blue over white panelling, the runner down the slide, balusters and a handrail
        /// both sides following the slope, pictures stepping down, a long-corded lamp from the high ceiling.</summary>
        static void Stairs(Ctx c)
        {
            var e = c.e; var r = c.room; var pal = c.pal;
            float hw = r.halfWidth, s0 = r.s0, s1 = r.s1;
            // Find the slide (the ramp) and the floors.
            LabPiece ramp = null;
            for (int i = 0; i < e.course.pieces.Count; i++) if (e.looks[i] == HouseLook.EscStairs) ramp = e.course.pieces[i];
            // Wainscot, dado and skirting along both walls: level on the floors, sloped down the slide.
            foreach (var p in Floors(c))
                for (int side = -1; side <= 1; side += 2) WallBand(c, side, p.Min.Z, p.Max.Z, p.Max.Y, p.Max.Y);
            if (ramp != null)
            {
                for (int side = -1; side <= 1; side += 2) WallBand(c, side, ramp.Min.Z, ramp.Max.Z, ramp.rampTopStart, ramp.rampTopEnd);
                // The banisters: a handrail on balusters each side, following the slide, newels at the ends.
                var white = HouseEscapeLook.Clean("Balusters", new Color(0.93f, 0.92f, 0.88f), 0.5f);
                var rail = HouseMaterials.TrimWalnut();
                foreach (float x in new[] { -hw + 0.55f, hw - 0.55f })
                {
                    float z0 = ramp.Min.Z, z1 = ramp.Max.Z, y0 = ramp.rampTopStart, y1 = ramp.rampTopEnd;
                    c.kb.Rod(new Vector3(x, y0 + 0.92f, z0), new Vector3(x, y1 + 0.92f, z1), 0.035f, rail, 10);
                    for (float z = z0 + 0.12f; z < z1; z += 0.26f)
                    {
                        float y = y0 + (y1 - y0) * (z - z0) / (z1 - z0);
                        c.kb.Rod(new Vector3(x, y, z), new Vector3(x, y + 0.9f, z), 0.016f, 0.02f, white, 6);
                    }
                    foreach (var (z, y) in new[] { (z0, y0), (z1, y1) })
                    {
                        c.b.Box(new Vector3(x - 0.06f, y, z - 0.06f), new Vector3(x + 0.06f, y + 1.1f, z + 0.06f), rail);
                        c.kb.Ball(new Vector3(x, y + 1.17f, z), 0.07f, rail, 8);
                    }
                }
                // Pictures stepping down the left wall.
                int k = 2;
                for (float z = ramp.Min.Z + 1.5f; z < ramp.Max.Z - 1f; z += 3.2f)
                {
                    float y = ramp.rampTopStart + (ramp.rampTopEnd - ramp.rampTopStart) * (z - ramp.Min.Z) / ramp.size.Z;
                    Picture(c, c.group, c.b, Photos[k++ % Photos.Length], new Vector3(-hw, y + 1.75f, z), Vector3.right, Vector3.up, 0.5f, 0.62f, "stairs");
                }
            }
            for (float z = s0 + 6f; z < s1 - 4f; z += 14f)
            {
                float fy = FloorAt(e, 0f, z);
                Pendant(c, new Vector3(0f, r.ceilingY, z), r.ceilingY - fy - 3.7f, "the stairs' lamp", 0.3f);
            }
            // A side table and a lamp on the landing.
            Thing(c, "living.side_table", new Vector3(-hw + 0.05f, r.floorTop, s0 + 4.6f), new Vector3(-hw + 0.6f, r.floorTop + 0.55f, s0 + 5.15f), 90f, "Landing table");
            Thing(c, "bedroom.table_lamp", new Vector3(-hw + 0.17f, r.floorTop + 0.55f, s0 + 4.72f), new Vector3(-hw + 0.47f, r.floorTop + 1.05f, s0 + 5.02f), 0f, "Landing lamp");
            Lamp(c, c.group, new Vector3(-hw + 0.4f, r.floorTop + 1.0f, s0 + 4.9f), 5f, 1.2f, pal.lamp, "landing lamp");
        }

        /// <summary>Wainscot, dado rail and skirting along a side wall of an upright room, from z0 (floor y0) to z1 (floor
        /// y1): level, or sloped with the floor (one sheared box each: continuous, no steps at joints).</summary>
        static void WallBand(Ctx c, int side, float z0, float z1, float y0, float y1)
        {
            var pal = c.pal;
            float x = side * c.room.halfWidth;
            void Slab(float off, float depth, float hFrom, float hTo, Material m)
            {
                if (m == null || z1 - z0 < 0.05f) return;
                float xi = x - side * (off + depth * 0.5f);
                float ym = (y0 + y1) * 0.5f + (hFrom + hTo) * 0.5f;
                var center = new Vector3(xi, ym, (z0 + z1) * 0.5f);
                var u = new Vector3(depth * 0.5f, 0f, 0f);
                var v = new Vector3(0f, (y1 - y0) * 0.5f, (z1 - z0) * 0.5f);
                var w = new Vector3(0f, (hTo - hFrom) * 0.5f, 0f);
                c.b.Obb(center, u, v, w, new[] { m, m, m, m, m, m });
            }
            if (pal.wainscot != null)
            {
                Slab(0f, 0.012f, 0f, pal.wainscotHeight, pal.wainscot);
                Slab(0.012f, 0.025f, pal.wainscotHeight - 0.03f, pal.wainscotHeight + 0.03f, pal.rail);
            }
            Slab(0.012f, 0.018f, 0f, 0.13f, pal.skirting);
        }

        /// <summary>Theo's room, upside down: his glow stars underfoot (his old ceiling), his bed, desk, bookcase and toy chest
        /// hanging from the ceiling (his old floor), the sky-and-stars paper with its panelling at the top, his drawings upside
        /// down, his ceiling lamp standing up out of the floor.</summary>
        static void Theo(Ctx c)
        {
            var e = c.e; var r = c.room; var pal = c.pal;
            float A = c.A, B = c.B, s0 = r.s0, s1 = r.s1, hw = r.halfWidth;
            // Glow stars on every floor piece (the ceiling he stuck them on).
            var stars = HouseEscapeLook.GlowStars();
            foreach (var p in Floors(c))
            {
                float y = p.Max.Y + 0.004f;
                c.b.Quad(new Vector3(p.Min.X, y, p.Min.Z), new Vector3(p.Min.X, y, p.Max.Z), new Vector3(p.Max.X, y, p.Max.Z), new Vector3(p.Max.X, y, p.Min.Z), stars);
            }
            // His panelling and skirting, upside down at the top of the walls; the cornice at the floor's edge.
            var ceil = Ceiling(c);
            float cy = ceil != null ? ceil.Min.Y : r.ceilingY;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * hw;
                if (pal.wainscot != null) c.b.Box(new Vector3(Mathf.Min(x, x - side * 0.012f), cy - pal.wainscotHeight, s0 + 0.3f), new Vector3(Mathf.Max(x, x - side * 0.012f), cy, s1), pal.wainscot);
                c.b.Box(new Vector3(Mathf.Min(x, x - side * 0.035f), cy - pal.wainscotHeight - 0.03f, s0 + 0.3f), new Vector3(Mathf.Max(x, x - side * 0.035f), cy - pal.wainscotHeight + 0.03f, s1), pal.rail);
                c.b.Box(new Vector3(Mathf.Min(x, x - side * 0.07f), r.floorTop, s0 + 0.3f), new Vector3(Mathf.Max(x, x - side * 0.07f), r.floorTop + 0.11f, s1), pal.cornice);
            }
            // His drawings, upside down on the walls.
            string[] art = { "dream_pic_kids_drawing_1", "dream_pic_kids_drawing_2", "dream_pic_theo_drawing", "dream_pic_siblings" };
            int k = 0;
            for (float z = s0 + 3f; z < s1 - 2f; z += 5.5f)
                for (int side = -1; side <= 1; side += 2)
                    Picture(c, c.group, c.b, art[k++ % art.Length], new Vector3(side * hw, cy - 2.0f, z), Vector3.right * -side, Vector3.down, 0.5f, 0.38f, "Theo's drawing");
            // His rug and his things hanging from the ceiling (his old floor: y -A in the old frame).
            if (ceil != null)
            {
                c.b.Box(new Vector3(-2.4f, cy - 0.012f, s0 + 2f), new Vector3(1.6f, cy, s0 + 6f), pal.rug);
                c.b.Box(new Vector3(-2.2f, cy - 0.014f, s0 + 2.3f), new Vector3(1.4f, cy - 0.012f, s0 + 2.5f), pal.rugBorder);
            }
            Thing(c, "bedroom.single_bed", new Vector3(2.6f, -A, s0 + 4f), new Vector3(3.6f, -A + 0.6f, s0 + 6.05f), 0f, "Theo's bed");
            Thing(c, "bedroom.kid_desk", new Vector3(-B, -A, s0 + 12f), new Vector3(-B + 0.71f, -A + 0.75f, s0 + 12.55f), 90f, "Theo's desk");
            Thing(c, "bedroom.kid_chair", new Vector3(-B + 0.8f, -A, s0 + 12f), new Vector3(-B + 1.45f, -A + 1.0f, s0 + 12.6f), -90f, "Theo's chair");
            Thing(c, "theo.bookcase", new Vector3(-B, -A, s0 + 20f), new Vector3(-B + 0.4f, -A + 1.9f, s0 + 21f), 90f, "Theo's bookcase");
            Thing(c, "bedroom.toy_chest", new Vector3(B - 0.45f, -A, s0 + 26f), new Vector3(B, -A + 0.35f, s0 + 26.85f), -90f, "Toy chest");
            Thing(c, "bedroom.teddy_bear", new Vector3(2.9f, -A + 0.6f, s0 + 5.4f), new Vector3(3.2f, -A + 0.95f, s0 + 5.7f), 0f, "Teddy");
            Thing(c, "bedroom.dresser", new Vector3(-B, -A, s0 + 32f), new Vector3(-B + 0.49f, -A + 1.88f, s0 + 33.14f), 90f, "Theo's drawers");
            // His ceiling lamps, standing up out of the floor (they hang from the old ceiling: y +A).
            Pendant(c, new Vector3(-3.0f, A, s0 + 8f), 0.95f, "Theo's ceiling lamp", 0.2f);
            Pendant(c, new Vector3(3.0f, A, s0 + 30f), 0.95f, "Theo's ceiling lamp", 0.7f);
            Lamp(c, c.group, new Vector3(0f, r.floorTop + 1.2f, s0 + 18f), 9f, 0.9f, new Color(0.7f, 1f, 0.66f), "the stars' glow");
        }

        /// <summary>The kitchen, come round again (on its side the other way: the old floor is the left wall): counters along
        /// both old walls with the stove and the upper cupboards, the fridge, the kettle; butter plaster and tiles underfoot and
        /// overhead; the pendants out of the right wall; a window in the ceiling.</summary>
        static void Kitchen(Ctx c)
        {
            float A = c.A, B = c.B, s0 = c.room.s0, s1 = c.room.s1;
            OldWallBands(c);
            OldWallPictures(c, Mathf.Sign(c.room.roll) * (c.room.halfWidth - 1.9f), 9f, 0.36f, 0.48f, 4);
            CeilingWindow(c, Mathf.Sign(c.room.roll) * 2.9f - 0.7f, Mathf.Sign(c.room.roll) * 2.9f + 0.7f, s0 + 5f, s0 + 6.6f);
            // Counters on the old wall that's now the floor's corner (old x +B) and the one that's the ceiling's (old x -B).
            for (float z = s0 + 2f; z < s1 - 3f; z += 12f)
            {
                Thing(c, "kitchen.counter", new Vector3(B - 0.6f, -A, z), new Vector3(B, -A + 0.92f, z + 1.8f), -90f, "Counter");
                Thing(c, "kitchen.cabinet_upper", new Vector3(B - 0.35f, -A + 1.45f, z), new Vector3(B, -A + 2.2f, z + 1.8f), -90f, "Upper cupboard");
                Thing(c, "kitchen.counter", new Vector3(-B, -A, z + 3f), new Vector3(-B + 0.6f, -A + 0.92f, z + 4.8f), 90f, "Counter");
                Thing(c, "kitchen.kettle", new Vector3(B - 0.45f, -A + 0.92f, z + 0.4f), new Vector3(B - 0.13f, -A + 1.22f, z + 0.65f), -90f, "Kettle");
            }
            Thing(c, "kitchen.fridge", new Vector3(-B, -A, s0 + 9f), new Vector3(-B + 0.75f, -A + 1.85f, s0 + 9.9f), 90f, "Fridge");
            Thing(c, "kitchen.stove", new Vector3(B - 0.6f, -A, s0 + 15f), new Vector3(B, -A + 0.92f, s0 + 15.75f), -90f, "Stove");
            Thing(c, "kitchen.pot", new Vector3(B - 0.45f, -A + 0.92f, s0 + 15.2f), new Vector3(B - 0.19f, -A + 1.1f, s0 + 15.45f), -90f, "Pan");
            Thing(c, "dining.chair", new Vector3(-0.3f, -A, s0 + 22f), new Vector3(0.25f, -A + 0.96f, s0 + 22.45f), 180f, "Kitchen chair");
            for (float z = s0 + 7f; z < s1 - 4f; z += 20f)
                Pendant(c, new Vector3(0f, A, z), 0.95f, "kitchen", (z * 0.07f) % 1f);
        }

        /// <summary>The attic, open to the stars: joists under the floating boards, the family's things drifting round them
        /// (boxes, the crib, the rocking chair, the trunk, an oil lamp), the telescope on its platform, the round window's
        /// frame, moonlight; the night sky round it (<see cref="NightSky"/>).</summary>
        static void Attic(Ctx c)
        {
            var e = c.e; var r = c.room;
            var dark = HouseMaterials.TrimWalnut();
            // Joists under every board (the floor torn out of the house).
            for (int i = 0; i < e.course.pieces.Count; i++)
            {
                var p = e.course.pieces[i];
                if (e.RoomAt(p.center.Z) != c.ri || (e.looks[i] != HouseLook.EscSkyBoards && e.looks[i] != HouseLook.EscAtticBoards) || p.kind != LabPieceKind.Floor) continue;
                for (float x = p.Min.X + 0.4f; x < p.Max.X - 0.2f; x += 1.2f)
                    c.b.Box(new Vector3(x - 0.07f, p.Min.Y - 0.25f, p.Min.Z - 0.15f), new Vector3(x + 0.07f, p.Min.Y, p.Max.Z + 0.15f), dark);
            }
            // Things drifting round the boards (each on its own slow turn and bob).
            var things = new (string role, Vector3 at, Vector3 size, float yaw)[]
            {
                ("attic.box", new Vector3(-4.2f, 0.6f, r.s0 + 8f), new Vector3(0.52f, 0.68f, 0.52f), 20f),
                ("attic.box", new Vector3(4.6f, -1.2f, r.s0 + 14f), new Vector3(0.52f, 0.68f, 0.52f), -30f),
                ("attic.crib", new Vector3(-5.2f, 1.6f, r.s0 + 22f), new Vector3(1.4f, 0.9f, 0.7f), 15f),
                ("attic.rocking_chair", new Vector3(4.8f, 1.0f, r.s0 + 30f), new Vector3(0.71f, 0.99f, 0.83f), -60f),
                ("attic.trunk", new Vector3(-4.6f, -1.5f, r.s0 + 36f), new Vector3(0.82f, 0.35f, 0.41f), 40f),
                ("attic.box", new Vector3(-6.0f, 2.8f, r.s0 + 42f), new Vector3(0.52f, 0.68f, 0.52f), 70f),
                ("attic.rocking_chair", new Vector3(-5.0f, 0.4f, r.s0 + 52f), new Vector3(0.71f, 0.99f, 0.83f), 110f),
                ("attic.box", new Vector3(6.2f, 2.2f, r.s0 + 58f), new Vector3(0.52f, 0.68f, 0.52f), 10f),
            };
            int n = 0;
            foreach (var (role, at, size, yaw) in things)
            {
                float fy = FloorAt(e, 0f, at.z);
                var pos = new Vector3(at.x, fy + at.y, at.z);
                var bb = new Bounds(pos + Vector3.up * size.y * 0.5f, size * 1.6f);
                if (!Clear(c, bb, "drifting " + role)) continue;
                var drift = new GameObject("Drifting: " + role).transform;
                drift.SetParent(c.tips, false);
                drift.localPosition = pos;
                drift.localRotation = Quaternion.Euler(n * 13f, yaw, n * 7f - 10f);
                var sw = drift.gameObject.AddComponent<EscapeSwing>();
                sw.axis = new Vector3(1f, 0f, 0.4f);
                sw.amplitude = 5f;
                sw.period = 5f + n;
                sw.phase = n * 0.17f;
                sw.spin = new Vector3(0f, 4f + n, 0f);
                sw.bob = 0.25f;
                var go = HouseFurnishing.Prop(role, drift, new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f), new Vector3(size.x * 0.5f, size.y, size.z * 0.5f), 0f, role);
                Unstatic(go);
                n++;
            }
            // The oil lamp on the first boards, lit; the telescope on its platform.
            LabPiece platform = null;
            for (int i = 0; i < e.course.pieces.Count; i++) if (e.looks[i] == HouseLook.EscAtticBoards && e.course.pieces[i].kind == LabPieceKind.Floor) platform = e.course.pieces[i];
            float t0 = FloorAt(e, 0f, r.s0 + 4f);
            Thing(c, "attic.lamp", new Vector3(-2.2f, t0, r.s0 + 6f), new Vector3(-1.98f, t0 + 0.8f, r.s0 + 6.22f), 0f, "Oil lamp");
            Lamp(c, c.group, new Vector3(-2.1f, t0 + 0.6f, r.s0 + 6.1f), 7f, 1.6f, new Color(1f, 0.72f, 0.42f), "oil lamp");
            if (platform != null)
            {
                float py = platform.Max.Y, pz = platform.center.Z;
                Thing(c, "observatory.telescope", new Vector3(-2.9f, py, pz - 0.4f), new Vector3(-2.1f, py + 1.55f, pz + 0.4f), 45f, "Lena's telescope");
                Thing(c, "attic.lamp", new Vector3(-2.9f, py, pz + 2.6f), new Vector3(-2.68f, py + 0.8f, pz + 2.82f), 0f, "Oil lamp (platform)");
                Lamp(c, c.group, new Vector3(-2.8f, py + 0.6f, pz + 2.7f), 7f, 1.5f, new Color(1f, 0.72f, 0.42f), "platform lamp");
            }
            // The round window's frame: a big round window on the lone wall she runs.
            for (int i = 0; i < e.course.pieces.Count; i++)
            {
                var p = e.course.pieces[i];
                if (e.looks[i] != HouseLook.EscAtticBoards || p.kind != LabPieceKind.Wall) continue;
                float fy = FloorAt(e, 0f, p.Min.Z - 1f);
                var center = new Vector3(p.Min.X - 0.01f, fy + 3.4f, p.center.Z);
                var brassy = HouseMaterials.TrimWalnut();
                c.kb.Ring(center, Vector3.left, 2.6f, 0.09f, brassy, 40);
                c.kb.Ring(center, Vector3.left, 2.35f, 0.05f, brassy, 40);
                c.kb.Disc(center + Vector3.left * 0.01f, Vector3.left, 2.35f, HouseEscapeLook.Glow("MoonGlass", new Color(0.45f, 0.55f, 0.9f), 0.9f), 40);
                c.kb.Rod(center + new Vector3(-0.03f, -2.35f, 0f), center + new Vector3(-0.03f, 2.35f, 0f), 0.04f, brassy, 8);
                c.kb.Rod(center + new Vector3(-0.03f, 0f, -2.35f), center + new Vector3(-0.03f, 0f, 2.35f), 0.04f, brassy, 8);
            }
            // Moonlight: cool, broad, soft (no sun down here).
            for (float z = r.s0 + 10f; z < r.s1; z += 26f)
                Lamp(c, c.group, new Vector3(3f, FloorAt(e, 0f, z) + 9f, z), 28f, 1.1f, new Color(0.62f, 0.72f, 1f), "moonlight");
        }

        /// <summary>The last hall, the right way up: the house's hall (oxblood over walnut, the runner, the console and mirror,
        /// the coat stand, the photos, two pendants), the front door standing open onto a warm glow.</summary>
        static void LastHall(Ctx c)
        {
            var e = c.e; var r = c.room; var pal = c.pal;
            float hw = r.halfWidth, s0 = r.s0, fd = e.frontDoorS, t = r.floorTop;
            for (int side = -1; side <= 1; side += 2) WallBand(c, side, s0 + 0.3f, fd, t, t);
            // Crown moulding at the ceiling.
            float cy = r.ceilingY;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * hw;
                c.b.Box(new Vector3(Mathf.Min(x, x - side * 0.08f), cy - 0.12f, s0 + 0.3f), new Vector3(Mathf.Max(x, x - side * 0.08f), cy, fd), pal.cornice);
            }
            // The runner, its border.
            c.b.Box(new Vector3(-0.7f, t, s0 + 1.5f), new Vector3(0.7f, t + 0.008f, fd - 0.6f), pal.rug);
            c.b.Box(new Vector3(-0.6f, t + 0.008f, s0 + 1.6f), new Vector3(-0.5f, t + 0.01f, fd - 0.7f), pal.rugBorder);
            c.b.Box(new Vector3(0.5f, t + 0.008f, s0 + 1.6f), new Vector3(0.6f, t + 0.01f, fd - 0.7f), pal.rugBorder);
            // The console and its mirror, the coat stand, photos (left wall; the umbrella stands right of the door).
            Thing(c, "hall.console", new Vector3(-hw, t, s0 + 8f), new Vector3(-hw + 0.38f, t + 0.76f, s0 + 8.38f), 90f, "Hall console");
            Thing(c, "hall.mirror", new Vector3(-hw, t + 1.1f, s0 + 7.95f), new Vector3(-hw + 0.03f, t + 1.84f, s0 + 8.44f), 90f, "Hall mirror");
            Thing(c, "living.vase.1", new Vector3(-hw + 0.08f, t + 0.76f, s0 + 8.08f), new Vector3(-hw + 0.3f, t + 1.07f, s0 + 8.3f), 0f, "Vase");
            Thing(c, "hall.coat_rack", new Vector3(-hw + 0.1f, t, fd - 3.2f), new Vector3(-hw + 0.5f, t + 1.8f, fd - 2.8f), 0f, "Coat stand");
            int k = 5;
            for (float z = s0 + 3f; z < fd - 4f; z += 3.6f)
                Picture(c, c.group, c.b, Photos[k++ % Photos.Length], new Vector3(hw, t + 1.6f, z), Vector3.left, Vector3.up, 0.42f, 0.52f, "last hall");
            for (float z = s0 + 6f; z < fd - 3f; z += 9f)
                Pendant(c, new Vector3(0f, cy, z), 1.1f, "the last hall", 0.15f);
            FrontDoorOpen(c);
        }

        /// <summary>The last hall's front door: its casing, the red leaf standing open against the left, the fanlight, and
        /// outside a short porch of warm white light (the rain-lit street beyond is the hill's: the game takes her there as
        /// she steps through), so nothing of the escape's outside shows through it.</summary>
        static void FrontDoorOpen(Ctx c)
        {
            var e = c.e; var r = c.room;
            float fd = e.frontDoorS, t = r.floorTop;
            const float half = 0.75f, head = 2.45f;
            var trim = HouseEscapeLook.Clean("TrimWhite", new Color(0.93f, 0.91f, 0.86f), 0.45f);
            // Casing on the hall side.
            c.b.Box(new Vector3(-half - 0.12f, t, fd - 0.03f), new Vector3(-half, t + head + 0.12f, fd), trim);
            c.b.Box(new Vector3(half, t, fd - 0.03f), new Vector3(half + 0.12f, t + head + 0.12f, fd), trim);
            c.b.Box(new Vector3(-half - 0.12f, t + head, fd - 0.03f), new Vector3(half + 0.12f, t + head + 0.14f, fd), trim);
            // The fanlight over the door (the opening is taller than a door: glass to its head).
            var warm = HouseEscapeLook.Glow("DoorLight", new Color(1f, 0.9f, 0.74f), 3.2f);
            c.b.Box(new Vector3(-half, t + head + 0.14f, fd + 0.12f), new Vector3(half, t + 3.6f, fd + 0.15f), new[] { null, null, null, null, warm, null });
            for (int k = 1; k < 5; k++)
            {
                float x = -half + k * (half * 2f / 5f);
                c.b.Box(new Vector3(x - 0.015f, t + head + 0.14f, fd + 0.08f), new Vector3(x + 0.015f, t + 3.6f, fd + 0.12f), trim);
            }
            // The leaf, open (on its hinge at the left jamb, swung back into the hall).
            var leaf = new MeshBuilder();
            var red = HouseEscapeLook.DoorRed();
            leaf.Box(new Vector3(0f, 0f, -0.05f), new Vector3(half * 2f - 0.04f, head - 0.02f, 0f), red);
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++)
                {
                    float x0 = 0.12f + i * (half - 0.04f), y0 = 0.15f + j * (head * 0.5f - 0.05f);
                    leaf.Box(new Vector3(x0, y0, -0.065f), new Vector3(x0 + half - 0.2f, y0 + head * 0.5f - 0.3f, -0.05f), HouseEscapeLook.Clean("FrontDoorPanel", new Color(0.48f, 0.1f, 0.09f), 0.6f));
                }
            var hinge = new GameObject("The front door (open)").transform;
            hinge.SetParent(c.group, false);
            hinge.localPosition = new Vector3(-half, t, fd - 0.02f);
            hinge.localRotation = Quaternion.Euler(0f, 100f, 0f);
            leaf.Build("Leaf", hinge);
            var knob = new HouseEscapeKit();
            knob.Ball(new Vector3(half * 2f - 0.14f, 1.0f, -0.09f), 0.035f, HouseEscapeLook.Brass(), 8);
            knob.Build("Knob", hinge, false);
            // Outside: a porch of light (no colliders: she's on the hill as she passes the door).
            float zo = fd + 0.3f, zb = fd + 3.0f;
            var walls = HouseEscapeLook.Clean("PorchWalls", new Color(0.7f, 0.66f, 0.62f), 0.3f);
            c.b.Box(new Vector3(-1.4f, t - 0.3f, zo), new Vector3(-1.2f, t + 3.9f, zb), walls);
            c.b.Box(new Vector3(1.2f, t - 0.3f, zo), new Vector3(1.4f, t + 3.9f, zb), walls);
            c.b.Box(new Vector3(-1.4f, t + 3.9f, zo), new Vector3(1.4f, t + 4.1f, zb), walls);
            c.b.Box(new Vector3(-1.2f, t - 0.3f, zb), new Vector3(1.2f, t + 3.9f, zb + 0.1f), new[] { null, null, null, null, HouseEscapeLook.Glow("Outside", new Color(1f, 0.93f, 0.82f), 4f), null });
            Lamp(c, c.group, new Vector3(0f, t + 1.8f, fd + 1.4f), 9f, 2.4f, new Color(1f, 0.88f, 0.7f), "the light through the front door");
        }

        // =====================================================================================================
        // The corridor
        // =====================================================================================================

        /// <summary>The corridor's dressing: the continuous sloped wainscot, dado and skirting, the cornice under the sloping
        /// ceiling, the runner over the boards; then the same landmarks in every bay counted back from the door (a brass
        /// threshold strip where the runner breaks, a closed side door on the left, a picture on the right, a pair of sconces
        /// and a ceiling lamp), so every stretch shows the same bay again and the eye can count them.</summary>
        static void Corridor(Ctx c)
        {
            var e = c.e; var r = c.room; var pal = c.pal; var cor = e.corridor;
            float hw = cor.halfWidth, cs = cor.startS, ds = cor.doorS, slope = cor.slope, t0 = r.floorTop;
            float Floor(float z) => t0 - (z - cs) * slope;
            // Wainscot, dado, skirting, both walls, one sloped slab each the whole length.
            for (int side = -1; side <= 1; side += 2) WallBand(c, side, cs + 0.3f, ds - 0.15f, Floor(cs + 0.3f), Floor(ds - 0.15f));
            // The cornice under the ceiling (parallel to the floor).
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * hw;
                float y0 = Floor(cs + 0.3f) + cor.height, y1 = Floor(ds) + cor.height;
                c.b.Obb(new Vector3(x - side * 0.04f, (y0 + y1) * 0.5f - 0.05f, (cs + 0.3f + ds) * 0.5f), new Vector3(0.04f, 0f, 0f), new Vector3(0f, (y1 - y0) * 0.5f, (ds - cs - 0.3f) * 0.5f), new Vector3(0f, 0.05f, 0f), new[] { pal.cornice, pal.cornice, pal.cornice, pal.cornice, pal.cornice, pal.cornice });
            }
            var brass = HouseEscapeLook.Brass();
            var doorPaint = HouseEscapeLook.Clean("CorridorDoors", new Color(0.9f, 0.88f, 0.82f), 0.5f);
            var trim = HouseEscapeLook.Clean("TrimWhite", new Color(0.93f, 0.91f, 0.86f), 0.45f);
            var shade = HouseEscapeLook.Glow("SconceShade", new Color(1f, 0.8f, 0.55f), 3f);
            // The runner, broken at each bay's threshold.
            float bay = cor.bay;
            var breaks = new List<float>();
            for (float zb = ds - bay; zb > cs + 1f; zb -= bay) breaks.Add(zb);
            breaks.Sort();
            float from = cs + 1.2f;
            foreach (float zb in breaks) { Runner(c, from, zb - 0.25f, Floor); from = zb + 0.25f; }
            Runner(c, from, ds - 0.6f, Floor);
            int k = 0;
            foreach (float zb in breaks)
            {
                float f = Floor(zb);
                // The threshold strip (brass) across the floor where the runner breaks.
                c.b.Box(new Vector3(-hw, f - 0.004f, zb - 0.05f), new Vector3(hw, f + 0.012f, zb + 0.05f), brass);
                // A closed side door on the left (panel, casing, a knob) at 4-5 m into the bay.
                float dz0 = zb + 4f, dz1 = zb + 4.95f, fd0 = Floor(dz1);
                float x = -hw;
                c.b.Box(new Vector3(x, fd0, dz0), new Vector3(x + 0.03f, fd0 + 2.05f, dz1), doorPaint);
                c.b.Box(new Vector3(x, fd0, dz0 - 0.08f), new Vector3(x + 0.045f, fd0 + 2.13f, dz0), trim);
                c.b.Box(new Vector3(x, fd0, dz1), new Vector3(x + 0.045f, fd0 + 2.13f, dz1 + 0.08f), trim);
                c.b.Box(new Vector3(x, fd0 + 2.05f, dz0 - 0.08f), new Vector3(x + 0.045f, fd0 + 2.13f, dz1 + 0.08f), trim);
                for (int j = 0; j < 2; j++)
                    c.b.Box(new Vector3(x + 0.03f, fd0 + 0.25f + j * 0.95f, dz0 + 0.15f), new Vector3(x + 0.04f, fd0 + 0.95f + j * 0.95f, dz1 - 0.15f), trim);
                c.kb.Ball(new Vector3(x + 0.08f, fd0 + 1.0f, dz1 - 0.12f), 0.03f, brass, 8);
                // A picture on the right at 6-6.7 m, the same one each bay.
                float pf = Floor(zb + 6.35f);
                Picture(c, c.group, c.b, "dream_pic_photo_3", new Vector3(hw, pf + 1.65f, zb + 6.35f), Vector3.left, Vector3.up, 0.62f, 0.5f, "the same photo");
                // The sconces at 10 m, a pair; a ceiling lamp between them.
                float sz = zb + 10f, sf = Floor(sz);
                for (int side = -1; side <= 1; side += 2)
                {
                    float sx = side * hw;
                    var plate = new Vector3(sx - side * 0.01f, sf + 1.95f, sz);
                    c.b.Box(new Vector3(Mathf.Min(sx, sx - side * 0.02f), sf + 1.85f, sz - 0.07f), new Vector3(Mathf.Max(sx, sx - side * 0.02f), sf + 2.05f, sz + 0.07f), brass);
                    c.kb.Rod(plate, plate + new Vector3(-side * 0.16f, 0.06f, 0f), 0.012f, brass, 6);
                    c.kb.Lathe(plate + new Vector3(-side * 0.16f, 0.02f, 0f), Vector3.up, new[] { new Vector2(0.03f, 0f), new Vector2(0.075f, 0.1f), new Vector2(0.09f, 0.17f) }, shade, 14, true);
                }
                c.kb.Lathe(new Vector3(0f, sf + cor.height - 0.02f, sz), Vector3.up, new[] { new Vector2(0.001f, -0.09f), new Vector2(0.16f, -0.06f), new Vector2(0.2f, 0f) }, shade, 18, true);
                Lamp(c, c.group, new Vector3(0f, sf + cor.height - 0.4f, sz), 9f, 1.9f, pal.lamp, "corridor bay " + (++k));
                Lamp(c, c.group, new Vector3(0f, sf + 1.9f, sz + 0.3f), 4.5f, 0.7f, pal.lamp, "corridor sconces " + k);
                // A second picture on the left at 14 m.
                float lf = Floor(zb + 14.5f);
                Picture(c, c.group, c.b, "dream_pic_childhood_2", new Vector3(-hw, lf + 1.6f, zb + 14.5f), Vector3.right, Vector3.up, 0.48f, 0.6f, "the same drawing");
            }
        }

        static void Runner(Ctx c, float z0, float z1, System.Func<float, float> floor)
        {
            if (z1 - z0 < 0.3f) return;
            float y0 = floor(z0), y1 = floor(z1);
            var m = c.pal.rug;
            var border = c.pal.rugBorder;
            c.b.Obb(new Vector3(0f, (y0 + y1) * 0.5f + 0.004f, (z0 + z1) * 0.5f), new Vector3(0.75f, 0f, 0f), new Vector3(0f, (y1 - y0) * 0.5f, (z1 - z0) * 0.5f), new Vector3(0f, 0.004f, 0f), new[] { m, m, m, m, m, m });
            foreach (float x in new[] { -0.62f, 0.62f })
                c.b.Obb(new Vector3(x, (y0 + y1) * 0.5f + 0.0085f, (z0 + z1) * 0.5f), new Vector3(0.05f, 0f, 0f), new Vector3(0f, (y1 - y0) * 0.5f, (z1 - z0) * 0.5f - 0.1f), new Vector3(0f, 0.001f, 0f), new[] { border, border, border, border, border, border });
        }

        // =====================================================================================================
        // The corridor's front door, the night sky
        // =====================================================================================================

        /// <summary>The door at the end of the corridor (the lure): a pair of red panelled leaves with brass knobs in a white
        /// casing, reeded sidelights and a fanlight glowing with the light behind it, warm light spilling under it and round
        /// it, a lantern over it. Returns the transform the game swings (with <see cref="EscapeDoubleDoor"/>).</summary>
        public static Transform FrontDoor(LabPiece p, Transform frame, out Light behind)
        {
            float w = p.size.X, h = p.size.Y;
            float leafW = Mathf.Min(1.0f, w * 0.32f), leafH = Mathf.Min(2.3f, h - 0.9f);
            float mid = w * 0.5f;
            var trim = HouseEscapeLook.Clean("TrimWhite", new Color(0.93f, 0.91f, 0.86f), 0.45f);
            var red = HouseEscapeLook.DoorRed();
            var panel = HouseEscapeLook.Clean("FrontDoorPanel", new Color(0.48f, 0.1f, 0.09f), 0.6f);
            var glass = HouseEscapeLook.Glow("DoorGlass", new Color(1f, 0.86f, 0.62f), 2.6f);
            var spill = HouseEscapeLook.Glow("DoorSpill", new Color(1f, 0.88f, 0.66f), 5f);
            // The fixed part (in the frame's space: x 0..w, y 0..h, the face at z 0, the door's depth to -p.size.z).
            var b = new MeshBuilder();
            const float d = -0.12f;
            // Panelled infill either side of the sidelights and over the fanlight.
            float sl = mid - leafW - 0.38f, sr = mid + leafW + 0.38f;
            b.Box(new Vector3(0f, 0f, d - 0.02f), new Vector3(sl, h, 0f), trim);
            b.Box(new Vector3(sr, 0f, d - 0.02f), new Vector3(w, h, 0f), trim);
            b.Box(new Vector3(sl, leafH + 0.75f, d - 0.02f), new Vector3(sr, h, 0f), trim);
            // Sidelights (glowing, with glazing bars) and the fanlight.
            foreach (var (x0, x1) in new[] { (sl, mid - leafW - 0.06f), (mid + leafW + 0.06f, sr) })
            {
                b.Box(new Vector3(x0, 0.35f, d), new Vector3(x1, leafH, d + 0.01f), new[] { null, null, null, null, glass, null });
                for (float y = 0.75f; y < leafH; y += 0.45f) b.Box(new Vector3(x0, y - 0.015f, d - 0.03f), new Vector3(x1, y + 0.015f, d), trim);
                b.Box(new Vector3(x0, 0f, d - 0.02f), new Vector3(x1, 0.35f, 0f), panel);
            }
            b.Box(new Vector3(sl, leafH + 0.06f, d), new Vector3(sr, leafH + 0.7f, d + 0.01f), new[] { null, null, null, null, glass, null });
            for (int k = 1; k < 6; k++)
            {
                float x = sl + (sr - sl) * k / 6f;
                b.Box(new Vector3(x - 0.015f, leafH + 0.06f, d - 0.03f), new Vector3(x + 0.015f, leafH + 0.7f, d), trim);
            }
            // Casing round the leaves (jambs between leaves and sidelights, the transom).
            b.Box(new Vector3(mid - leafW - 0.06f, 0f, d - 0.05f), new Vector3(mid - leafW, leafH + 0.06f, 0f), trim);
            b.Box(new Vector3(mid + leafW, 0f, d - 0.05f), new Vector3(mid + leafW + 0.06f, leafH + 0.06f, 0f), trim);
            b.Box(new Vector3(sl - 0.08f, leafH, d - 0.06f), new Vector3(sr + 0.08f, leafH + 0.06f, 0f), trim);
            // The light under the doors.
            b.Box(new Vector3(mid - leafW, 0.002f, d - 0.012f), new Vector3(mid + leafW, 0.02f, d - 0.002f), spill);
            b.Build("The door's frame, sidelights and fanlight", frame);
            // The lantern over it.
            var k2 = new HouseEscapeKit();
            var top = new Vector3(mid, leafH + 0.85f, d - 0.08f);
            k2.Rod(top + new Vector3(0f, 0.32f, 0.06f), top + new Vector3(0f, 0.32f, -0.14f), 0.012f, HouseEscapeLook.Iron(), 6);
            k2.Lathe(top + new Vector3(0f, -0.05f, -0.2f), Vector3.up, new[] { new Vector2(0.06f, 0f), new Vector2(0.09f, 0.08f), new Vector2(0.09f, 0.26f), new Vector2(0.03f, 0.34f) }, glass, 12, true);
            k2.Build("Lantern", frame, false);
            // The two leaves on their hinges (the game swings them: EscapeDoubleDoor).
            var leaves = new GameObject("Leaves (hinged)").transform;
            leaves.SetParent(frame, false);
            var pair = leaves.gameObject.AddComponent<EscapeDoubleDoor>();
            for (int s = -1; s <= 1; s += 2)
            {
                var hinge = new GameObject(s < 0 ? "Leaf (left)" : "Leaf (right)").transform;
                hinge.SetParent(leaves, false);
                hinge.localPosition = new Vector3(mid + s * leafW, 0f, d);
                var lb = new MeshBuilder();
                float x0 = s < 0 ? 0f : -leafW, x1 = s < 0 ? leafW : 0f;
                lb.Box(new Vector3(x0 + 0.005f, 0.01f, -0.0f), new Vector3(x1 - 0.005f, leafH - 0.01f, 0.05f), red);
                for (int j = 0; j < 3; j++)
                {
                    float y0 = 0.18f + j * (leafH - 0.3f) / 3f, y1 = y0 + (leafH - 0.3f) / 3f - 0.14f;
                    lb.Box(new Vector3(x0 + 0.13f, y0, -0.015f), new Vector3(x1 - 0.13f, y1, 0f), j == 2 ? null : panel);
                    if (j == 2) lb.Box(new Vector3(x0 + 0.13f, y0, -0.01f), new Vector3(x1 - 0.13f, y1, 0f), new[] { null, null, null, null, glass, null });
                }
                lb.Build("Leaf", hinge);
                var kn = new HouseEscapeKit();
                kn.Ball(new Vector3(s < 0 ? leafW - 0.09f : -leafW + 0.09f, 1.02f, -0.05f), 0.035f, HouseEscapeLook.Brass(), 8);
                kn.Build("Knob", hinge, false);
                if (s < 0) pair.left = hinge; else pair.right = hinge;
            }
            // The light behind the door (the front door's daylight): it lights the corridor's end through the glass.
            var lgo = new GameObject("Lamp: the light behind the door");
            lgo.transform.SetParent(frame, false);
            lgo.transform.localPosition = new Vector3(mid, leafH * 0.6f, 0.9f);
            behind = lgo.AddComponent<Light>();
            behind.type = LightType.Point;
            behind.range = 10f;
            behind.intensity = 2.2f;
            behind.color = new Color(1f, 0.84f, 0.6f);
            behind.shadows = LightShadows.None;
            var lantern = new GameObject("Lamp: the lantern over the door");
            lantern.transform.SetParent(frame, false);
            lantern.transform.localPosition = top + new Vector3(0f, 0.1f, -0.3f);
            var ll = lantern.AddComponent<Light>();
            ll.type = LightType.Point; ll.range = 7f; ll.intensity = 1.6f; ll.color = new Color(1f, 0.78f, 0.5f); ll.shadows = LightShadows.None;
            Unstatic(frame.gameObject);
            return leaves;
        }

        /// <summary>The night over the attic: a dome round the attic room (its own frame, not the camera's: the capture's
        /// camera and the player's both see it), the procedural JamKit/House/NightSky (stars, the Milky Way, a deep
        /// gradient), drawn behind everything near; the game shows it only in the attic.</summary>
        public static GameObject NightSky(HouseEscape e, Transform escapeRoot)
        {
            EscapeRoom attic = null;
            foreach (var r in e.rooms) if (r.open) attic = r;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Night sky (the attic open to the stars)";
            go.transform.SetParent(escapeRoot, false);
            float mid = attic != null ? (attic.s0 + attic.s1) * 0.5f : 480f, y = attic != null ? attic.floorTop : 0f;
            go.transform.localPosition = new Vector3(0f, y, mid);
            go.transform.localScale = Vector3.one * 320f;   // radius 160 m: inside the camera's far clip
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = HouseEscapeLook.NightSky();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            go.SetActive(false);
            return go;
        }
    }
}
