using System.Collections.Generic;
using System.IO;
using JamKit.Look.EditorTools;
using JamKit.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The house lived in: every furniture part's model (Dream/House's CC0 models, Kenney's kit) fitted inside its box (the
    /// box is its collider, so nothing pokes out), or built from parts in real materials where there's no model (counters,
    /// wardrobes, the telescope, the pancakes); framed pictures; hanging cloth; rugs; the trampoline and the oak; the lamps;
    /// the memories; Lena and Theo (the cast's paper cut-outs, People's <see cref="DreamCutoutView"/>).
    /// </summary>
    public static class HouseFurnishing
    {
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);
        static readonly List<string> _missing = new List<string>();

        public static void Build(DreamHouse h, Transform root, bool withOutside)
        {
            _missing.Clear();
            var furniture = new GameObject("Furniture").transform;
            furniture.SetParent(root, false);
            var built = new MeshBuilder();
            foreach (var part in h.parts)
            {
                if (!withOutside && HouseGeometry.IsOutsidePart(h, part)) continue;
                var p = part.piece;
                Vector3 mn = U(p.Min), mx = U(p.Max);
                switch (part.look)
                {
                    case HouseLook.Furniture: Furnish(part, furniture, built, mn, mx); break;
                    case HouseLook.Picture: Picture(part.model, part.yaw, furniture, built, mn, mx); break;
                    case HouseLook.Cloth: Cloth(part.model, built, mn, mx); break;
                    case HouseLook.Rug: Rug(part.model, furniture, built, mn, mx); break;
                    case HouseLook.Trampoline: Trampoline(furniture, mn, mx); break;
                    case HouseLook.Tree: if (part.model == "yard.tree") Oak(furniture, mn, mx); else Trunk(built, mn, mx); break;
                }
            }
            built.Build("Built furniture", furniture);
            if (_missing.Count > 0) Debug.Log("JAMKIT_HOUSE built in place of a model: " + string.Join(", ", _missing));
        }

        // =====================================================================================================
        // Models, fitted into their boxes
        // =====================================================================================================

        static void Furnish(HousePart part, Transform parent, MeshBuilder built, Vector3 mn, Vector3 mx)
        {
            var model = HouseMaterials.Model(part.model, out string id);
            if (model != null && !BuiltFirst(part.model))
            {
                bool hangs = part.model.Contains("pendant");
                var go = Fit(model, id, parent, mn, mx, part.yaw, part.piece.name, hangs);
                // A lamp's own shade never casts its light's shadow: a bulb inside a shade threw the shade's shadow over
                // every wall above the rim (the dark translucent band at head height in the recorded run).
                if (part.model.Contains("lamp"))
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                return;
            }
            if (!_missing.Contains(part.model)) _missing.Add(part.model);
            Built(part.model, part.piece.name, built, mn, mx, part.yaw);
        }

        /// <summary>A piece of furniture for a role (house-assets.json's model fitted in, or built here where there's none),
        /// under its own holder in <paramref name="parent"/>'s frame, box <paramref name="mn"/>-<paramref name="mx"/>, facing
        /// <paramref name="yaw"/> (0 = +z). For other builders (the escape dresses its rooms with the house's own things).
        /// Static flags are set for the house; clear them on anything that moves.</summary>
        public static GameObject Prop(string role, Transform parent, Vector3 mn, Vector3 mx, float yaw, string name)
        {
            var model = HouseMaterials.Model(role, out string id);
            if (model != null && !BuiltFirst(role)) return Fit(model, id, parent, mn, mx, yaw, name);
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var b = new MeshBuilder();
            Built(role, name, b, mn, mx, yaw);
            b.Build("Built", holder.transform);
            return holder;
        }

        /// <summary>Roles drawn here even when a model exists (they need the exact shape: counters run to the walls).</summary>
        static bool BuiltFirst(string role) => role == "kitchen.counter" || role == "kitchen.sink_counter" || role == "kitchen.island" || role == "kitchen.cabinet_upper";

        /// <summary>A model fitted into its box, kept for the mesh lint (<see cref="HouseMeshLint"/>).</summary>
        public sealed class FittedModel
        {
            public GameObject go;
            public string id, name;
            /// <summary>The box in <see cref="space"/>'s frame (the house's).</summary>
            public Vector3 boxMin, boxMax;
            public Transform space;
            public HouseMaterials.FitSpec spec;
            /// <summary>The model's size in its own frame (upright, front +z) after the fit, for the upright check.</summary>
            public Vector3 modelSize;
            /// <summary>The scale the fit used (per axis of the box).</summary>
            public Vector3 scale;
            /// <summary>It hangs (a pendant, a mirror, a picture): it rests on nothing, it touches its wall or ceiling.</summary>
            public bool hangs;
        }

        public static readonly List<FittedModel> Fitted = new List<FittedModel>();

        /// <summary>A model fitted into the box: a holder at the box (the house's axes), the model turned to
        /// <paramref name="yaw"/> under it, the import's own transform kept (Poly Haven's FBX root carries Blender's
        /// axis conversion: overwriting it laid the single-mesh models on their sides, house-1.md blocker 1), the
        /// manifest's per-model turn (<see cref="HouseMaterials.FitSpec"/>), scaled uniformly to the box with each
        /// axis allowed <see cref="HouseMaterials.FitSpec.stretch"/> more where the box has room, standing on the box's
        /// floor, centred. Its colliders go (the box is its collider) and its materials are made PBR.</summary>
        public static GameObject Fit(GameObject prefab, string id, Transform parent, Vector3 mn, Vector3 mx, float yaw, string name, bool hangs = false)
        {
            var spec = HouseMaterials.Fit(id);
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var turn = new GameObject("Turned").transform;
            turn.SetParent(holder.transform, false);
            turn.localRotation = Quaternion.Euler(0f, yaw + spec.yaw, 0f);
            var fix = turn;
            if (spec.rotate != Vector3.zero)
            {
                fix = new GameObject("Upright").transform;
                fix.SetParent(turn, false);
                fix.localRotation = Quaternion.Euler(spec.rotate);
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(fix, false);   // its own local rotation and scale stay: the import's axis conversion
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var an in inst.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(an);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Pbr(mats[i], id);
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            var b = LocalBounds(holder.transform, inst);
            var modelSize = LocalBounds(turn, inst).size;
            var size = mx - mn;
            var scale = Vector3.one;
            if (b.size.x > 1e-4f && b.size.y > 1e-4f && b.size.z > 1e-4f)
            {
                float sx = size.x / b.size.x * 0.995f, sy = size.y / b.size.y * 0.995f, sz = size.z / b.size.z * 0.995f;
                float s = Mathf.Min(sx, Mathf.Min(sy, sz));
                scale = Vector3.one * s;
                float turned = Mathf.Repeat(yaw + spec.yaw, 90f);
                bool square = turned < 0.5f || turned > 89.5f;
                if (square && spec.stretch > 0f)
                {
                    float cap = s * (1f + spec.stretch);
                    scale = new Vector3(Mathf.Min(sx, cap), Mathf.Min(sy, cap), Mathf.Min(sz, cap));
                }
            }
            holder.transform.localScale = scale;
            var bottom = new Vector3(b.center.x, hangs ? b.max.y : b.min.y, b.center.z);
            var target = new Vector3((mn.x + mx.x) * 0.5f, hangs ? mx.y : mn.y, (mn.z + mx.z) * 0.5f);
            holder.transform.localPosition = target - Vector3.Scale(bottom, scale);
            GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            Fitted.Add(new FittedModel { go = holder, id = id, name = name, boxMin = mn, boxMax = mx, space = parent, spec = spec, modelSize = Vector3.Scale(modelSize, Vector3.one * Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z))), scale = scale, hangs = hangs });
            return holder;
        }

        /// <summary>The bounds of every mesh under <paramref name="of"/>, in <paramref name="space"/>'s frame (the eight
        /// corners of each mesh's own bounds, so a turned model is measured in the box's axes, not the world's).</summary>
        public static Bounds LocalBounds(Transform space, GameObject of)
        {
            bool any = false;
            var b = new Bounds();
            var toSpace = space.worldToLocalMatrix;
            foreach (var r in of.GetComponentsInChildren<Renderer>(true))
            {
                Bounds mb;
                if (r is SkinnedMeshRenderer sk && sk.sharedMesh != null) mb = sk.sharedMesh.bounds;
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    mb = mf.sharedMesh.bounds;
                }
                var m = toSpace * r.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var c = new Vector3((k & 1) == 0 ? mb.min.x : mb.max.x, (k & 2) == 0 ? mb.min.y : mb.max.y, (k & 4) == 0 ? mb.min.z : mb.max.z);
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>A model's material made URP Lit PBR: a Poly Haven model's own maps, or a Kenney colour as a painted
        /// surface (its flat colours on the library's plaster, so it has a surface).</summary>
        static Material Pbr(Material m, string id)
        {
            if (m == null) return HouseMaterials.TrimWhite();
            if (id != null && !id.StartsWith("kenney:"))
            {
                var own = HouseMaterials.ModelMaterial(id, m.name);
                if (own != null) return own;
            }
            if (m.shader != null && m.shader.name == "Universal Render Pipeline/Lit" && m.GetTexture("_BaseMap") != null) return m;
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            return HouseMaterials.Paint("Kenney_" + m.name.Replace(" ", "_"), c, 0.9f);
        }

        // =====================================================================================================
        // Built in place (no model)
        // =====================================================================================================

        static Material Wood => HouseMaterials.TrimWalnut();
        static Material Paint(string v, Color c, float smoothness = 1.25f) => HouseMaterials.Paint(v, c, smoothness);

        static void Built(string role, string name, MeshBuilder b, Vector3 mn, Vector3 mx, float yaw)
        {
            var size = mx - mn;
            var cab = Paint("Cabinets", new Color(0.66f, 0.74f, 0.66f));
            var top = HouseMaterials.Surface("wood_floor", "Butcher", new Color(0.86f, 0.7f, 0.52f), 0.5f);
            switch (role)
            {
                case "kitchen.counter":
                case "kitchen.sink_counter":
                case "kitchen.island":
                    // A plinth (set back), the carcass, door seams, the worktop.
                    b.Box(mn, new Vector3(mx.x, mn.y + 0.1f, mx.z), Paint("Plinth", new Color(0.25f, 0.24f, 0.22f)));
                    b.Box(new Vector3(mn.x, mn.y + 0.1f, mn.z), new Vector3(mx.x, mx.y - 0.04f, mx.z), cab);
                    b.Box(new Vector3(mn.x, mx.y - 0.04f, mn.z), mx, top);
                    if (role == "kitchen.sink_counter")
                    {
                        var steel = HouseMaterials.Metal();
                        float cx = (mn.x + mx.x) * 0.5f, cz = (mn.z + mx.z) * 0.5f;
                        b.Box(new Vector3(cx - 0.35f, mx.y - 0.005f, cz - 0.2f), new Vector3(cx + 0.35f, mx.y + 0.005f, cz + 0.2f), Paint("SinkDark", new Color(0.3f, 0.32f, 0.34f)));
                        b.Box(new Vector3(cx - 0.02f, mx.y, mx.z - 0.1f), new Vector3(cx + 0.02f, mx.y + 0.28f, mx.z - 0.06f), steel);
                        b.Box(new Vector3(cx - 0.02f, mx.y + 0.24f, cz + 0.02f), new Vector3(cx + 0.02f, mx.y + 0.28f, mx.z - 0.06f), steel);
                    }
                    break;
                case "kitchen.cabinet_upper":
                    b.Box(mn, mx, cab);
                    break;
                case "living.window_seat":
                    b.Box(mn, new Vector3(mx.x, mx.y - 0.1f, mx.z), Wood);
                    b.Box(new Vector3(mn.x + 0.02f, mx.y - 0.1f, mn.z + 0.02f), new Vector3(mx.x - 0.02f, mx.y, mx.z - 0.02f), HouseMaterials.Fabric("Teal", new Color(0.26f, 0.48f, 0.5f)));
                    break;
                case "living.mantel":
                    b.Box(mn, mx, Wood);
                    break;
                case "dining.pancakes":
                    Plate(b, mn, mx);
                    break;
                case "theo.bookcase":
                case "pantry.shelves":
                case "closet.shelves":
                case "basement.shelves":
                    Shelves(b, mn, mx, yaw, role == "theo.bookcase" || role == "closet.shelves");
                    break;
                case "wardrobe.side":
                case "wardrobe.top":
                    b.Box(mn, mx, Wood);
                    break;
                case "observatory.platform":
                    b.Box(mn, mx, HouseMaterials.Deck());
                    break;
                case "observatory.telescope":
                    Telescope(b, mn, mx);
                    break;
                case "observatory.chart_table":
                    Table(b, mn, mx, Wood);
                    break;
                case "attic.crib":
                    Crib(b, mn, mx);
                    break;
                case "rec.pingpong":
                    Table(b, mn, mx, Paint("PingPong", new Color(0.12f, 0.36f, 0.26f)));
                    b.Box(new Vector3(mn.x, mx.y, (mn.z + mx.z) * 0.5f - 0.01f), new Vector3(mx.x, mx.y + 0.15f, (mn.z + mx.z) * 0.5f + 0.01f), Paint("Net", new Color(0.92f, 0.92f, 0.9f)));
                    break;
                case "furnace.furnace":
                    b.Box(mn, mx, HouseMaterials.Metal());
                    b.Box(new Vector3(mn.x + 0.3f, mx.y, mn.z + 0.3f), new Vector3(mn.x + 0.55f, mx.y + 0.2f, mn.z + 0.55f), HouseMaterials.Metal());
                    break;
                case "furnace.heater":
                case "garage.freezer":
                case "laundry.washer":
                case "laundry.dryer":
                    b.Box(mn, mx, Paint("Appliance", new Color(0.93f, 0.93f, 0.9f)));
                    if (role.StartsWith("laundry"))
                    {
                        float cx = (mn.x + mx.x) * 0.5f, cy = mn.y + size.y * 0.5f;
                        b.Box(new Vector3(cx - 0.18f, cy - 0.18f, mn.z - 0.01f), new Vector3(cx + 0.18f, cy + 0.18f, mn.z), Paint("Porthole", new Color(0.3f, 0.36f, 0.42f)));
                    }
                    break;
                case "laundry.basket":
                    b.Box(mn, mx, HouseMaterials.Surface("wooden_panels", "Wicker", new Color(0.86f, 0.74f, 0.5f), 0.2f));
                    break;
                case "workshop.pegboard":
                    b.Box(mn, mx, HouseMaterials.Surface("wooden_panels", "Pegboard", new Color(0.82f, 0.68f, 0.5f), 0.2f));
                    break;
                case "hall.umbrella_stand":
                    b.Box(mn, mx, Paint("UmbrellaStand", new Color(0.3f, 0.35f, 0.42f)));
                    b.Box(new Vector3(mn.x + 0.05f, mx.y, mn.z + 0.06f), new Vector3(mn.x + 0.08f, mx.y + 0.3f, mn.z + 0.09f), Paint("Umbrella2", new Color(0.2f, 0.25f, 0.5f)));
                    break;
                case "yard.grill":
                    Grill(b, mn, mx);
                    break;
                default:
                    // Anything else: a solid piece in walnut, bevelled by its trim (never untextured).
                    b.Box(mn, mx, Wood);
                    break;
            }
        }

        static void Table(MeshBuilder b, Vector3 mn, Vector3 mx, Material top)
        {
            b.Box(new Vector3(mn.x, mx.y - 0.04f, mn.z), mx, top);
            const float l = 0.06f;
            foreach (var (x, z) in new[] { (mn.x, mn.z), (mx.x - l, mn.z), (mn.x, mx.z - l), (mx.x - l, mx.z - l) })
                b.Box(new Vector3(x, mn.y, z), new Vector3(x + l, mx.y - 0.04f, z + l), Wood);
        }

        static void Shelves(MeshBuilder b, Vector3 mn, Vector3 mx, float yaw, bool books)
        {
            const float t = 0.025f;
            var w = Wood;
            b.Box(mn, new Vector3(mx.x, mn.y + 0.08f, mx.z), w);
            b.Box(new Vector3(mn.x, mx.y - t, mn.z), mx, w);
            // Sides and back (the back toward the wall: the box's far side from where it faces).
            bool alongX = (mx.x - mn.x) >= (mx.z - mn.z);
            if (alongX)
            {
                b.Box(mn, new Vector3(mn.x + t, mx.y, mx.z), w);
                b.Box(new Vector3(mx.x - t, mn.y, mn.z), mx, w);
            }
            else
            {
                b.Box(mn, new Vector3(mx.x, mx.y, mn.z + t), w);
                b.Box(new Vector3(mn.x, mn.y, mx.z - t), mx, w);
            }
            int shelves = Mathf.Max(2, Mathf.RoundToInt((mx.y - mn.y) / 0.38f));
            var rnd = new System.Random((int)(mn.x * 100f + mn.z * 37f));
            Color[] spines = { new Color(0.62f, 0.18f, 0.16f), new Color(0.2f, 0.32f, 0.52f), new Color(0.86f, 0.74f, 0.4f), new Color(0.24f, 0.44f, 0.3f), new Color(0.9f, 0.88f, 0.82f), new Color(0.42f, 0.26f, 0.4f) };
            for (int k = 1; k < shelves; k++)
            {
                float y = mn.y + 0.08f + (mx.y - mn.y - 0.08f) * k / shelves;
                if (alongX) b.Box(new Vector3(mn.x + t, y - t, mn.z), new Vector3(mx.x - t, y, mx.z), w);
                else b.Box(new Vector3(mn.x, y - t, mn.z + t), new Vector3(mx.x, y, mx.z - t), w);
                if (!books) continue;
                // A row of book spines standing on the shelf below.
                float y0 = mn.y + 0.08f + (mx.y - mn.y - 0.08f) * (k - 1) / shelves + (k == 1 ? 0f : 0f);
                float a0 = alongX ? mn.x + t : mn.z + t, a1 = alongX ? mx.x - t : mx.z - t;
                for (float a = a0 + 0.01f; a < a1 - 0.05f;)
                {
                    float wdt = 0.025f + (float)rnd.NextDouble() * 0.03f, hgt = 0.18f + (float)rnd.NextDouble() * 0.12f;
                    if (a + wdt > a1 - 0.01f) break;
                    var m = Paint("Spine" + rnd.Next(spines.Length), spines[rnd.Next(spines.Length)]);
                    float yb = y0 + (k == 1 ? 0f : t);
                    if (alongX) b.Box(new Vector3(a, yb, mn.z + 0.04f), new Vector3(a + wdt, Mathf.Min(yb + hgt, y - t - 0.01f), mx.z - 0.03f), m);
                    else b.Box(new Vector3(mn.x + 0.04f, yb, a), new Vector3(mx.x - 0.03f, Mathf.Min(yb + hgt, y - t - 0.01f), a + wdt), m);
                    a += wdt + 0.003f;
                }
            }
        }

        static void Plate(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            var plate = Paint("Plate", new Color(0.97f, 0.96f, 0.93f));
            var cake = HouseMaterials.Surface("wood_floor", "Pancake", new Color(1f, 0.78f, 0.45f), 0.35f, 0.3f);
            b.Box(mn, new Vector3(mx.x, mn.y + 0.012f, mx.z), plate);
            float inset = (mx.x - mn.x) * 0.15f;
            for (int k = 0; k < 4; k++)
            {
                float y0 = mn.y + 0.012f + k * 0.016f;
                float j = (k % 2) * 0.004f;
                b.Box(new Vector3(mn.x + inset + j, y0, mn.z + inset - j), new Vector3(mx.x - inset + j, y0 + 0.015f, mx.z - inset - j), cake);
            }
            b.Box(new Vector3(mn.x + inset * 2f, mn.y + 0.076f, mn.z + inset * 2f), new Vector3(mn.x + inset * 2.6f, mn.y + 0.084f, mn.z + inset * 2.6f), Paint("Butter", new Color(1f, 0.93f, 0.6f)));
        }

        static void Telescope(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            var brass = Paint("Brass", new Color(0.78f, 0.6f, 0.3f), 1.7f);
            var white = Paint("TelescopeWhite", new Color(0.92f, 0.9f, 0.86f), 1.4f);
            float cx = (mn.x + mx.x) * 0.5f, cz = (mn.z + mx.z) * 0.5f, h = mx.y - mn.y;
            // Three legs, the mount, the tube pointing up at the round window (north) and the sky.
            foreach (var (dx, dz) in new[] { (-0.3f, -0.2f), (0.3f, -0.2f), (0f, 0.32f) })
                b.Box(new Vector3(cx + dx - 0.02f, mn.y, cz + dz - 0.02f), new Vector3(cx + dx * 0.2f + 0.02f, mn.y + h * 0.62f, cz + dz * 0.2f + 0.02f), brass);
            b.Box(new Vector3(cx - 0.06f, mn.y + h * 0.6f, cz - 0.06f), new Vector3(cx + 0.06f, mn.y + h * 0.7f, cz + 0.06f), brass);
            for (int k = 0; k < 6; k++)
            {
                float t = k / 5f;
                float y = mn.y + h * (0.68f + 0.3f * t), z = cz - 0.25f + 0.55f * t;
                b.Box(new Vector3(cx - 0.07f, y - 0.06f, z - 0.06f), new Vector3(cx + 0.07f, y + 0.06f, z + 0.06f), k == 5 ? brass : white);
            }
        }

        static void Crib(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            var w = Paint("CribWhite", new Color(0.93f, 0.91f, 0.86f));
            b.Box(new Vector3(mn.x, mn.y + 0.3f, mn.z), new Vector3(mx.x, mn.y + 0.36f, mx.z), w);
            for (float x = mn.x; x <= mx.x - 0.03f; x += 0.1f)
            {
                b.Box(new Vector3(x, mn.y, mn.z), new Vector3(x + 0.03f, mx.y, mn.z + 0.03f), w);
                b.Box(new Vector3(x, mn.y, mx.z - 0.03f), new Vector3(x + 0.03f, mx.y, mx.z), w);
            }
            b.Box(new Vector3(mn.x, mx.y - 0.04f, mn.z), new Vector3(mx.x, mx.y, mn.z + 0.04f), w);
            b.Box(new Vector3(mn.x, mx.y - 0.04f, mx.z - 0.04f), new Vector3(mx.x, mx.y, mx.z), w);
        }

        static void Grill(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            var black = Paint("GrillBlack", new Color(0.12f, 0.12f, 0.13f), 1.5f);
            float cx = (mn.x + mx.x) * 0.5f, cz = (mn.z + mx.z) * 0.5f;
            foreach (var (dx, dz) in new[] { (-0.28f, -0.2f), (0.28f, -0.2f), (0f, 0.3f) })
                b.Box(new Vector3(cx + dx - 0.02f, mn.y, cz + dz - 0.02f), new Vector3(cx + dx + 0.02f, mn.y + 0.62f, cz + dz + 0.02f), black);
            b.Box(new Vector3(cx - 0.32f, mn.y + 0.58f, cz - 0.32f), new Vector3(cx + 0.32f, mn.y + 0.78f, cz + 0.32f), black);
            b.Box(new Vector3(cx - 0.28f, mn.y + 0.78f, cz - 0.28f), new Vector3(cx + 0.28f, mx.y - 0.04f, cz + 0.28f), black);
            b.Box(new Vector3(cx - 0.04f, mx.y - 0.04f, cz - 0.04f), new Vector3(cx + 0.04f, mx.y, cz + 0.04f), black);
        }

        // =====================================================================================================
        // Pictures, cloth, rugs, the trampoline, the oak
        // =====================================================================================================

        static void Picture(string art, float yaw, Transform parent, MeshBuilder frames, Vector3 mn, Vector3 mx)
        {
            // The frame (dark wood) is the box; the picture is a quad just proud of its front.
            frames.Box(mn, mx, HouseMaterials.TrimWalnut());
            var tex = DreamArt.Get(art, out _);
            var mat = PictureMaterial(art, tex);
            var size = mx - mn;
            bool alongX = size.x >= size.z;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Picture: " + art;
            q.transform.SetParent(parent, false);
            var f = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var c = (mn + mx) * 0.5f + f * ((alongX ? size.z : size.x) * 0.5f + 0.002f);
            q.transform.position = c;
            q.transform.rotation = Quaternion.LookRotation(-f, Vector3.up);
            float w = (alongX ? size.x : size.z) - 0.06f, h = size.y - 0.06f;
            q.transform.localScale = new Vector3(w, h, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public static Material PictureMaterial(string art, Texture tex)
        {
            string path = HouseMaterials.MatDir + "/VHH_Picture_" + art + ".mat";
            DreamEscapeBuilder.EnsureFolder(HouseMaterials.MatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Cloth(string role, MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            Color c;
            switch (role)
            {
                case "backhall.coats": Coats(b, mn, mx); return;
                case "den.curtains": c = new Color(0.82f, 0.62f, 0.32f); break;
                case "bath.curtain": c = new Color(0.94f, 0.94f, 0.9f); break;
                case "closet.clothes": Coats(b, mn, mx); return;
                case "laundry.washing":
                case "yard.sheets": c = new Color(0.96f, 0.95f, 0.9f); break;
                default: c = new Color(0.8f, 0.7f, 0.6f); break;
            }
            var m = HouseMaterials.Fabric("Cloth_" + role.Replace(".", "_"), c);
            // Hanging folds: narrow panels, alternately set back.
            bool alongX = (mx.x - mn.x) >= (mx.z - mn.z);
            float a0 = alongX ? mn.x : mn.z, a1 = alongX ? mx.x : mx.z, d = alongX ? mx.z - mn.z : mx.x - mn.x;
            int n = Mathf.Max(2, Mathf.RoundToInt((a1 - a0) / 0.18f));
            for (int k = 0; k < n; k++)
            {
                float s0 = a0 + (a1 - a0) * k / n, s1 = a0 + (a1 - a0) * (k + 1) / n;
                float off = (k % 2) * d * 0.4f;
                if (alongX) b.Box(new Vector3(s0, mn.y, mn.z + off), new Vector3(s1, mx.y, mn.z + off + d * 0.5f), m);
                else b.Box(new Vector3(mn.x + off, mn.y, s0), new Vector3(mn.x + off + d * 0.5f, mx.y, s1), m);
            }
        }

        static void Coats(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            Color[] cs = { new Color(0.24f, 0.32f, 0.5f), new Color(0.62f, 0.3f, 0.22f), new Color(0.84f, 0.76f, 0.5f), new Color(0.3f, 0.42f, 0.32f), new Color(0.88f, 0.4f, 0.42f) };
            bool alongX = (mx.x - mn.x) >= (mx.z - mn.z);
            float a0 = alongX ? mn.x : mn.z, a1 = alongX ? mx.x : mx.z;
            int n = Mathf.Max(2, Mathf.RoundToInt((a1 - a0) / 0.3f));
            b.Box(alongX ? new Vector3(mn.x, mx.y - 0.04f, mn.z) : new Vector3(mn.x, mx.y - 0.04f, mn.z), alongX ? new Vector3(mx.x, mx.y, mn.z + 0.05f) : new Vector3(mn.x + 0.05f, mx.y, mx.z), HouseMaterials.TrimWalnut());
            for (int k = 0; k < n; k++)
            {
                float s0 = a0 + (a1 - a0) * k / n + 0.02f, s1 = a0 + (a1 - a0) * (k + 1) / n - 0.02f;
                float y0 = mn.y + (k % 3) * 0.12f;
                var m = HouseMaterials.Fabric("Coat" + (k % cs.Length), cs[k % cs.Length]);
                if (alongX) b.Box(new Vector3(s0, y0, mn.z), new Vector3(s1, mx.y - 0.05f, mx.z), m);
                else b.Box(new Vector3(mn.x, y0, s0), new Vector3(mx.x, mx.y - 0.05f, s1), m);
            }
        }

        static void Rug(string role, Transform parent, MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            Color c = role == "hall.doormat" ? new Color(0.5f, 0.38f, 0.26f) : role == "living.rug_round" ? new Color(0.8f, 0.62f, 0.3f) : new Color(0.62f, 0.26f, 0.22f);
            var m = HouseMaterials.Fabric("Rug_" + role.Replace(".", "_"), c);
            b.Box(mn, mx, new[] { m, m, null, m, m, m });
            // A border band.
            var band = HouseMaterials.Fabric("RugBorder", new Color(0.86f, 0.78f, 0.6f));
            float w = Mathf.Min(0.12f, (mx.x - mn.x) * 0.1f);
            b.Box(new Vector3(mn.x + w, mx.y, mn.z + w), new Vector3(mx.x - w, mx.y + 0.002f, mn.z + w * 1.6f), band);
            b.Box(new Vector3(mn.x + w, mx.y, mx.z - w * 1.6f), new Vector3(mx.x - w, mx.y + 0.002f, mx.z - w), band);
        }

        static void Trampoline(Transform parent, Vector3 mn, Vector3 mx)
        {
            var go = new GameObject("Trampoline");
            go.transform.SetParent(parent, false);
            var c = (mn + mx) * 0.5f;
            float r = (mx.x - mn.x) * 0.5f;
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(frame.GetComponent<Collider>());
            frame.name = "Frame and mat";
            frame.transform.SetParent(go.transform, false);
            frame.transform.position = new Vector3(c.x, mx.y - 0.05f, c.z);
            frame.transform.localScale = new Vector3(r * 2f, 0.05f, r * 2f);
            frame.GetComponent<MeshRenderer>().sharedMaterial = HouseMaterials.Paint("TrampolineMat", new Color(0.1f, 0.1f, 0.12f), 0.6f);
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.name = "Pad";
            pad.transform.SetParent(go.transform, false);
            pad.transform.position = new Vector3(c.x, mx.y - 0.04f, c.z);
            pad.transform.localScale = new Vector3(r * 2.02f, 0.045f, r * 2.02f);
            pad.GetComponent<MeshRenderer>().sharedMaterial = HouseMaterials.Paint("TrampolinePad", new Color(0.2f, 0.42f, 0.7f));
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(leg.GetComponent<Collider>());
                leg.transform.SetParent(go.transform, false);
                leg.transform.position = new Vector3(c.x + Mathf.Cos(a) * (r - 0.06f), mn.y + (mx.y - mn.y) * 0.45f, c.z + Mathf.Sin(a) * (r - 0.06f));
                leg.transform.localScale = new Vector3(0.05f, (mx.y - mn.y) * 0.45f, 0.05f);
                leg.GetComponent<MeshRenderer>().sharedMaterial = HouseMaterials.Metal();
            }
        }

        /// <summary>The oak: a round trunk (its collider is the part's box), the Look library's leaf crown lifted clear of
        /// the treehouse's roof.</summary>
        static void Oak(Transform parent, Vector3 mn, Vector3 mx)
        {
            var c = (mn + mx) * 0.5f;
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            trunk.name = "The oak (trunk)";
            trunk.transform.SetParent(parent, false);
            float top = DreamHouse.TreeFloor + 6.0f;
            trunk.transform.position = new Vector3(c.x, (mn.y + top) * 0.5f, c.z);
            trunk.transform.localScale = new Vector3(mx.x - mn.x, (top - mn.y) * 0.5f, mx.z - mn.z);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = HouseMaterials.Bark();
            var tree = VhLookTrees.Place(parent, new Vector3(c.x, mn.y, c.z), 3, 13f);
            if (tree == null) return;
            tree.name = "The oak (crown)";
            foreach (var col in tree.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            var t = tree.transform.Find("Trunk");
            if (t != null) Object.DestroyImmediate(t.gameObject);
            var crown = tree.transform.Find("Crown");
            if (crown != null)
            {
                var r = crown.GetComponent<Renderer>();
                float clear = DreamHouse.TreeFloor + 2.3f;
                if (r != null && r.bounds.min.y < clear) crown.position += Vector3.up * (clear - r.bounds.min.y);
            }
        }

        static void Trunk(MeshBuilder b, Vector3 mn, Vector3 mx) { }

        // =====================================================================================================
        // Lamps
        // =====================================================================================================

        public static void Lamps(DreamHouse h, Transform root)
        {
            var lamps = new GameObject("Lamps").transform;
            lamps.SetParent(root, false);
            int shadowed = 0;
            foreach (var l in h.lamps)
            {
                var go = new GameObject("Lamp: " + l.name + " (" + l.kind + ")");
                go.transform.SetParent(lamps, false);
                go.transform.localPosition = U(l.position);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = l.range;
                switch (l.kind)
                {
                    case "moon": light.color = new Color(0.62f, 0.72f, 1f); light.intensity = 1.2f * l.intensity; break;
                    case "fire": light.color = new Color(1f, 0.55f, 0.25f); light.intensity = 2.6f * l.intensity; break;
                    case "bulb": light.color = new Color(1f, 0.82f, 0.58f); light.intensity = 1.8f; break;
                    case "fairy": light.color = new Color(1f, 0.78f, 0.5f); light.intensity = 1.1f; break;
                    default: light.color = new Color(1f, 0.74f, 0.48f); light.intensity = 2.4f; break;   // 2700 K
                }
                bool main = l.kind == "pendant" && (l.name.StartsWith("Living") || l.name.StartsWith("Dining") || l.name.StartsWith("Kitchen") || l.name.StartsWith("Theo") || l.name.StartsWith("Front hall") || l.name.StartsWith("Upper hall"));
                light.shadows = main && shadowed < 8 ? LightShadows.Soft : LightShadows.None;
                if (light.shadows != LightShadows.None) shadowed++;
                light.renderMode = LightRenderMode.ForcePixel;
            }
            // Reflection probes, a storey each (the rooms' gleam on the floors and the glass).
            foreach (var (name, y, hgt) in new[] { ("ground", 1.45f, 2.9f), ("upper", 4.5f, 2.6f), ("basement", -1.55f, 2.5f) })
            {
                var pg = new GameObject("Reflections (" + name + ")");
                pg.transform.SetParent(lamps, false);
                pg.transform.localPosition = new Vector3(0f, y, 5.5f);
                var probe = pg.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.size = new Vector3(15.5f, hgt, 11.5f);
                probe.boxProjection = true;
                probe.resolution = 128;
            }
        }

        // =====================================================================================================
        // The memories
        // =====================================================================================================

        public static HouseMemoryView[] Memories(DreamHouse h, Transform root)
        {
            var parent = new GameObject("Memories").transform;
            parent.SetParent(root, false);
            var glow = DreamEscapeBuilder.Glow("MemoryGlow", new Color(1f, 0.84f, 0.5f), 0.55f, 0f, 0.3f);
            var views = new List<HouseMemoryView>();
            foreach (var m in h.memories)
            {
                var go = new GameObject("Memory: " + m.name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = U(m.position);
                var view = go.AddComponent<HouseMemoryView>();
                view.id = m.id;
                Texture2D tex = m.picture != null ? DreamArt.Get(m.picture, out bool ph) : null;
                if (m.id == DreamHouseStory.MemHeightMarks) tex = Generated("house_height_marks", HeightMarks);
                if (m.id == DreamHouseStory.MemSaturdayPaper) tex = Generated("house_paper_hat", PaperHat);
                if (m.id == DreamHouseStory.MemLenaThursdays) tex = Generated("house_star_wheel", StarWheel);
                view.picture = tex;
                Thing(m, go.transform, tex);
                var g = DreamEscapeBuilder.Part("Glow", PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.55f, glow);
                view.glow = g.transform;
                var lamp = new GameObject("Glow light").AddComponent<Light>();
                lamp.transform.SetParent(go.transform, false);
                lamp.type = LightType.Point;
                lamp.range = 1.6f;
                lamp.intensity = 0.4f;
                lamp.color = new Color(1f, 0.82f, 0.5f);
                lamp.shadows = LightShadows.None;
                view.lamp = lamp;
                views.Add(view);
            }
            return views.ToArray();
        }

        /// <summary>The thing a memory is, in its local space (it faces +z; the game turns it into its room).</summary>
        static void Thing(HouseMemory m, Transform t, Texture2D tex)
        {
            Material Pic(string key) => PictureMaterial(key, tex);
            switch (m.id)
            {
                case DreamHouseStory.MemRadio:
                {
                    var model = HouseMaterials.Model("living.radio", out string id);
                    if (model != null) Fit(model, id, t, new Vector3(-0.17f, 0f, -0.08f), new Vector3(0.17f, 0.2f, 0.08f), 180f, "The radio");
                    return;
                }
                case DreamHouseStory.MemSaturdayPaper:
                {
                    // A paper hat: two leaning sheets of newsprint.
                    var mat = Pic("house_paper_hat");
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        Object.DestroyImmediate(q.GetComponent<Collider>());
                        q.transform.SetParent(t, false);
                        q.transform.localPosition = new Vector3(0f, 0.08f, s * 0.04f);
                        q.transform.localRotation = Quaternion.Euler(s * 28f, s > 0 ? 180f : 0f, 0f);
                        q.transform.localScale = new Vector3(0.3f, 0.18f, 1f);
                        q.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    }
                    return;
                }
                case DreamHouseStory.MemLenaThursdays:
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(disc.GetComponent<Collider>());
                    disc.name = "The star wheel";
                    disc.transform.SetParent(t, false);
                    disc.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                    disc.transform.localScale = new Vector3(0.26f, 0.006f, 0.26f);
                    disc.GetComponent<MeshRenderer>().sharedMaterial = Pic("house_star_wheel");
                    return;
                }
                case DreamHouseStory.MemBadDream:
                case DreamHouseStory.MemPlaceCard:
                {
                    // Lying flat (the drawing on the bookcase) or standing folded (the place card).
                    bool card = m.id == DreamHouseStory.MemPlaceCard;
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(t, false);
                    q.transform.localPosition = card ? new Vector3(0f, 0.04f, 0f) : new Vector3(0f, 0.004f, 0f);
                    q.transform.localRotation = card ? Quaternion.Euler(-12f, 180f, 0f) : Quaternion.Euler(90f, 0f, 0f);
                    q.transform.localScale = card ? new Vector3(0.12f, 0.075f, 1f) : new Vector3(0.32f, 0.24f, 1f);
                    q.GetComponent<MeshRenderer>().sharedMaterial = Pic(m.picture);
                    return;
                }
                default:
                {
                    // On a wall: the calendar, the photo, the height marks (a picture just off the wall, facing +z).
                    float w = m.id == DreamHouseStory.MemHeightMarks ? 0.12f : m.id == DreamHouseStory.MemCalendar ? 0.32f : 0.42f;
                    float hgt = m.id == DreamHouseStory.MemHeightMarks ? 1.3f : m.id == DreamHouseStory.MemCalendar ? 0.46f : 0.32f;
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(q.GetComponent<Collider>());
                    q.transform.SetParent(t, false);
                    q.transform.localPosition = new Vector3(0f, 0f, 0.004f);
                    q.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    q.transform.localScale = new Vector3(w, hgt, 1f);
                    q.GetComponent<MeshRenderer>().sharedMaterial = Pic(m.picture ?? m.id);
                    return;
                }
            }
        }

        // ---------- Generated pictures (until the art pipeline draws them) ----------

        static Texture2D Generated(string name, System.Func<Texture2D> make)
        {
            string dir = DreamEscapeBuilder.Root + "/Generated/House";
            DreamEscapeBuilder.EnsureFolder(dir);
            string path = dir + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var t = make();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D HeightMarks()
        {
            var t = new Texture2D(64, 640, TextureFormat.RGBA32, false);
            var px = new Color[64 * 640];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.95f, 0.93f, 0.88f, 1f);
            // Pencil lines, a year apart, stopping at ten.
            for (int k = 0; k < 10; k++)
            {
                int y = 40 + k * 52;
                for (int x = 6; x < 52; x++) for (int d = 0; d < 3; d++) px[(y + d) * 64 + x] = new Color(0.3f, 0.3f, 0.34f, 1f);
                for (int x = 40; x < 58; x++) for (int d = 6; d < 12; d++) if ((x + d) % 3 == 0) px[(y + d) * 64 + x] = new Color(0.36f, 0.36f, 0.42f, 1f);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        static Texture2D PaperHat()
        {
            var t = new Texture2D(256, 160, TextureFormat.RGBA32, false);
            var r = new System.Random(4);
            for (int y = 0; y < 160; y++)
                for (int x = 0; x < 256; x++)
                {
                    bool headline = y > 120 && y < 148 && x > 20 && x < 236 && ((x / 9 + y / 7) % 3 != 0);
                    bool col = y < 110 && (x % 64) > 6 && (x % 64) < 58 && (y % 6) < 3 && r.NextDouble() < 0.8;
                    float v = headline ? 0.15f : col ? 0.45f : 0.9f;
                    t.SetPixel(x, y, new Color(v, v, v * 0.96f, 1f));
                }
            t.Apply();
            return t;
        }

        static Texture2D StarWheel()
        {
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var r = new System.Random(9);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f), d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = d > 0.96f ? new Color(0.75f, 0.62f, 0.34f) : d > 0.8f ? new Color(0.92f, 0.88f, 0.76f) : new Color(0.1f, 0.12f, 0.26f);
                    if (d < 0.8f && r.NextDouble() < 0.012) c = new Color(1f, 0.96f, 0.8f);
                    if (d > 0.8f && d < 0.96f && (Mathf.Atan2(dy, dx) * 30f % 3f) < 0.2f) c = new Color(0.3f, 0.26f, 0.2f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        // =====================================================================================================
        // The cast: Lena and Theo as the cast's paper cut-outs (People's DreamCutoutView)
        // =====================================================================================================

        public const float LenaEye = 1.6f, LenaEyeFromTop = 0.186f, TheoEye = 1.2f, TheoEyeFromTop = 0.262f;
        public const float LenaCardHeight = LenaEye / (1f - LenaEyeFromTop), TheoCardHeight = TheoEye / (1f - TheoEyeFromTop);

        public static GameObject CutOut(string name, string art, Transform parent, Vector3 feet, float height)
        {
            var tex = DreamArt.Get(art, out bool placeholder);
            var mat = DreamEscapeBuilder.Mat("Cutout_" + art, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", tex);
            float aspect = tex != null && tex.height > 0 ? tex.width / (float)tex.height : 0.5f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var cardGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cardGo.name = "Card";
            Object.DestroyImmediate(cardGo.GetComponent<Collider>());
            cardGo.transform.SetParent(go.transform, false);
            cardGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            cardGo.transform.localScale = new Vector3(height * aspect, height, 1f);
            cardGo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            var r = cardGo.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
            var view = go.AddComponent<DreamCutoutView>();
            view.card = cardGo.transform;
            view.noticeDistance = 40f;
            view.noticeDelay = 0.15f;
            return go;
        }

        /// <summary>Theo: a body that follows his brain (<see cref="TheoView"/>), his cut-out inside it, the meter over him.</summary>
        public static TheoView Theo(DreamHouse h, Transform root)
        {
            var go = new GameObject("Theo (seeking)");
            go.transform.SetParent(root, false);
            go.transform.localPosition = U(h.theoStart);
            var view = go.AddComponent<TheoView>();
            var figure = CutOut("Theo (cut-out)", DreamArt.Theo, go.transform, go.transform.position, TheoCardHeight);
            view.figure = figure.transform;
            var meter = new GameObject("Meter").transform;
            meter.SetParent(go.transform, false);
            meter.localPosition = new Vector3(0f, TheoCardHeight + 0.25f, 0f);
            var back = DreamEscapeBuilder.Part("Meter (back)", PrimitiveType.Quad, meter, Vector3.zero, new Vector3(0.52f, 0.09f, 1f), DreamEscapeBuilder.Glow("TheoMeterBack", new Color(0.1f, 0.08f, 0.12f), 0.3f, 0f, 0f));
            var fill = DreamEscapeBuilder.Part("Meter (fill)", PrimitiveType.Quad, meter, new Vector3(0f, 0f, -0.002f), new Vector3(0.5f, 0.07f, 1f), DreamEscapeBuilder.Glow("TheoMeterFill", new Color(1f, 0.82f, 0.4f), 1f, 0f, 0f));
            view.meterBack = back.transform;
            view.meterFill = fill.transform;
            view.meterRenderer = fill.GetComponent<MeshRenderer>();
            return view;
        }
    }
}
