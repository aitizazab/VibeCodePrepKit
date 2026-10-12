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
            for (int pi = 0; pi < h.parts.Count; pi++)
            {
                var part = h.parts[pi];
                if (!withOutside && HouseGeometry.IsOutsidePart(h, part)) continue;
                var p = part.piece;
                Vector3 mn = U(p.Min), mx = U(p.Max);
                CurrentPart = pi;
                built.Owner = p.name; built.OwnerPart = pi;
                built.Kind = part.look == HouseLook.Picture ? DrawnKind.Owned : DrawnKind.Owned;
                switch (part.look)
                {
                    case HouseLook.Furniture: Furnish(part, furniture, built, mn, mx); break;
                    case HouseLook.Picture: Picture(part.model, part.yaw, furniture, built, mn, mx); break;
                    case HouseLook.Cloth:
                        if (!HouseProps.Cloth(part.model, built, mn, mx, WallSide(h, mn, mx))) Cloth(part.model, built, mn, mx);
                        break;
                    case HouseLook.Rug: Rug(part.model, furniture, built, mn, mx); break;
                    case HouseLook.Trampoline: Trampoline(furniture, mn, mx); break;
                    case HouseLook.Tree: if (part.model == "yard.tree") Oak(furniture, mn, mx); else Trunk(built, mn, mx); break;
                }
            }
            CurrentPart = -1;
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
        /// <summary>Which side of a thin box a wall stands against (-1 its min side, +1 its max side across its thin axis, 0
        /// none): curtains and coats hang from it, facing away.</summary>
        static int WallSide(DreamHouse h, Vector3 mn, Vector3 mx)
        {
            var size = mx - mn;
            bool thinZ = size.x >= size.z;
            foreach (var part in h.parts)
            {
                if (!part.piece.solid || part.look != HouseLook.Wall && part.look != HouseLook.Chimney) continue;
                var q = part.piece;
                bool overlapsAlong = thinZ ? q.Min.X < mx.x && q.Max.X > mn.x : q.Min.Z < mx.z && q.Max.Z > mn.z;
                bool overlapsUp = q.Min.Y < mx.y && q.Max.Y > mn.y;
                if (!overlapsAlong || !overlapsUp) continue;
                float lo = thinZ ? mn.z : mn.x, hi = thinZ ? mx.z : mx.x, qlo = thinZ ? q.Min.Z : q.Min.X, qhi = thinZ ? q.Max.Z : q.Max.X;
                if (Mathf.Abs(qhi - lo) < 0.05f) return -1;
                if (Mathf.Abs(qlo - hi) < 0.05f) return 1;
            }
            return 0;
        }

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
        static bool BuiltFirst(string role) => role == "kitchen.counter" || role == "kitchen.sink_counter" || role == "kitchen.island" || role == "kitchen.cabinet_upper"
            // Kenney's low-poly blocks read as grey boxes at eye height (house-1.md): built here properly instead.
            || role == "kitchen.fridge" || role == "bath.sink" || role == "bath.bathtub" || role == "bedroom.single_bed" || role == "bedroom.double_bed";

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
            /// <summary>Its part in the plan (-1: none).</summary>
            public int owner = -1;
        }

        public static readonly List<FittedModel> Fitted = new List<FittedModel>();

        /// <summary>The part being furnished (its index in the plan), for the lint's records; -1 outside the loop.</summary>
        public static int CurrentPart = -1;

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
            Fitted.Add(new FittedModel { go = holder, id = id, name = name, boxMin = mn, boxMax = mx, space = parent, spec = spec, modelSize = Vector3.Scale(modelSize, Vector3.one * Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z))), scale = scale, hangs = hangs, owner = CurrentPart });
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
            // Kenney's flat colours as clean painted / glazed surfaces (on the plaster they read as cloud noise).
            return HouseEscapeLook.Clean("Kenney_" + m.name.Replace(" ", "_"), c, 0.55f);
        }

        // =====================================================================================================
        // Built in place (no model)
        // =====================================================================================================

        static Material Wood => HouseMaterials.TrimWalnut();
        static Material Paint(string v, Color c, float smoothness = 1.25f) => HouseMaterials.Paint(v, c, smoothness);

        static void Built(string role, string name, MeshBuilder b, Vector3 mn, Vector3 mx, float yaw)
        {
            if (HouseProps.Build(role, b, mn, mx, yaw)) return;
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
            // A real trampoline (it was three primitive cylinders): the padded rim on its round frame, the black mat a
            // little lower, six legs bent into W-feet, springs all round.
            var b = new MeshBuilder { Owner = "Trampoline", OwnerPart = CurrentPart, Kind = DrawnKind.Owned };
            var c = (mn + mx) * 0.5f;
            float r = Mathf.Min(mx.x - mn.x, mx.z - mn.z) * 0.5f - 0.04f, top = mx.y - 0.05f;
            var steel = HouseEscapeLook.Clean("TrampolineSteel", new Color(0.62f, 0.64f, 0.66f), 0.6f, 1f);
            const int seg = 36;
            var ring = new Vector3[seg + 1];
            for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; ring[i] = new Vector3(c.x + Mathf.Cos(a) * r, top, c.z + Mathf.Sin(a) * r); }
            HouseShapes.Tube(b, ring, 0.035f, steel, 8);
            var pad = new Vector3[seg + 1];
            for (int i = 0; i <= seg; i++) pad[i] = ring[i] + Vector3.up * 0.04f;
            HouseShapes.Tube(b, pad, 0.045f, HouseEscapeLook.Clean("TrampolinePad", new Color(0.2f, 0.42f, 0.7f), 0.25f), 8);
            HouseShapes.Lathe(b, new Vector3(c.x, top - 0.06f, c.z), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r - 0.22f, 0f), new Vector2(r - 0.22f, 0.006f), new Vector2(0f, 0.006f) }, HouseEscapeLook.Clean("TrampolineMat", new Color(0.06f, 0.06f, 0.07f), 0.3f), seg);
            for (int i = 0; i < seg; i += 2)
            {
                float a = i * Mathf.PI * 2f / seg;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                HouseShapes.Rod(b, new Vector3(c.x, top - 0.055f, c.z) + d * (r - 0.22f), new Vector3(c.x, top, c.z) + d * (r - 0.03f), 0.008f, steel, 5);
            }
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var t = new Vector3(c.x, top, c.z) + d * (r - 0.01f);
                HouseShapes.Tube(b, new[] { t, new Vector3(t.x, mn.y + 0.08f, t.z), new Vector3(t.x, mn.y + 0.02f, t.z) + d * 0.08f }, 0.022f, steel, 8);
            }
            b.Build("Trampoline", parent);
        }

        /// <summary>The oak: Look's oak, whole (trunk, limbs and crown, its own LODs), its colliders gone (the part's box is
        /// its collider). The old one was a primitive pole with the crown lifted off it, nothing joining them (house-1.md).</summary>
        static void Oak(Transform parent, Vector3 mn, Vector3 mx)
        {
            var c = (mn + mx) * 0.5f;
            var tree = VhLookTrees.Place(parent, new Vector3(c.x, mn.y, c.z), VhLookTrees.Species.Oak, 1, 13f, false, 30f);
            if (tree == null) return;
            tree.name = "The oak";
            foreach (var col in tree.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
        }

        static void Trunk(MeshBuilder b, Vector3 mn, Vector3 mx) { }

        // =====================================================================================================
        // Lamps
        // =====================================================================================================

        public static void Lamps(DreamHouse h, Transform root)
        {
            var lamps = new GameObject("Lamps").transform;
            lamps.SetParent(root, false);
            // Fixtures: what each light hangs in (a ceiling dome, a bare bulb on its socket), drawn without shadows (a
            // shade casting its own bulb's shadow drew a dark band round every room: the recorded run).
            var fixtures = new MeshBuilder { Kind = DrawnKind.Mounted, Owner = "Lamp fixtures" };
            var warmGlow = HouseEscapeLook.Glow("HouseLampGlow", new Color(1f, 0.82f, 0.55f), 2.2f);
            var brass = HouseEscapeLook.Brass();
            var bulbModel = HouseMaterials.Model("basement.bulb", out string bulbId);
            foreach (var l in h.lamps)
            {
                var pos = U(l.position);
                var room = h.RoomAt(l.position + new NVec3(0f, -0.3f, 0f));
                float ceiling = room != null ? room.ceilingY : pos.y + 0.45f;
                bool roomLight = l.kind == "pendant" || l.kind == "bulb" || l.kind == "fairy";
                var go = new GameObject("Lamp: " + l.name + " (" + l.kind + ")" + (roomLight ? " (main)" : ""));
                go.transform.SetParent(lamps, false);
                var light = go.AddComponent<Light>();
                light.renderMode = LightRenderMode.ForcePixel;
                light.shadows = LightShadows.None;   // HouseLightBudget gives the two nearest "(main)" lights their shadows
                if (roomLight)
                {
                    // A room light: a spot pointing down from just under its fixture (it no longer blasts the ceiling
                    // above it: house-1.md's blown hotspots), wide enough to light the room's walls.
                    float hang = PendantBottom(h, pos, ceiling);
                    bool hasPendant = hang < ceiling - 0.3f;
                    float y = hasPendant ? hang - 0.05f : ceiling - 0.22f;
                    go.transform.localPosition = new Vector3(pos.x, y, pos.z);
                    go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    light.type = LightType.Spot;
                    light.spotAngle = 150f;
                    light.innerSpotAngle = 70f;
                    float span = room != null ? Mathf.Max(3f, Mathf.Sqrt(room.Area)) : 4f;
                    light.range = Mathf.Max(5f, (y - (room != null ? room.floorY : y - 2.5f)) + span * 0.8f);
                    light.color = l.kind == "bulb" ? new Color(1f, 0.8f, 0.56f) : new Color(1f, 0.76f, 0.52f);
                    light.intensity = (l.kind == "fairy" ? 1.4f : 1.7f) + 1.3f * Mathf.Clamp01(l.intensity);
                    if (!hasPendant)
                    {
                        if (l.kind == "bulb" && bulbModel != null)
                        {
                            Fit(bulbModel, bulbId, lamps, new Vector3(pos.x - 0.06f, ceiling - 0.17f, pos.z - 0.06f), new Vector3(pos.x + 0.06f, ceiling, pos.z + 0.06f), 0f, "Bulb socket: " + l.name, true);
                            HouseShapes.Lathe(fixtures, new Vector3(pos.x, ceiling - 0.3f, pos.z), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.03f, 0.02f), new Vector2(0.032f, 0.06f), new Vector2(0.015f, 0.11f), new Vector2(0f, 0.12f) }, warmGlow, 12);
                        }
                        else
                        {
                            // A flush dome: a brass rim on the ceiling, the glowing glass under it.
                            HouseShapes.Lathe(fixtures, new Vector3(pos.x, ceiling - 0.02f, pos.z), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.21f, 0.02f), new Vector2(0f, 0.02f) }, brass, 20);
                            HouseShapes.Lathe(fixtures, new Vector3(pos.x, ceiling - 0.14f, pos.z), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.09f, 0.01f), new Vector2(0.17f, 0.06f), new Vector2(0.19f, 0.12f), new Vector2(0f, 0.12f) }, warmGlow, 20);
                        }
                    }
                    continue;
                }
                go.transform.localPosition = pos;
                light.type = LightType.Point;
                light.range = l.range;
                switch (l.kind)
                {
                    case "moon": light.color = new Color(0.62f, 0.72f, 1f); light.intensity = 1.0f * l.intensity + 0.4f; break;
                    case "fire": light.color = new Color(1f, 0.55f, 0.25f); light.intensity = 2.6f * l.intensity; break;
                    case "porch":
                        light.type = LightType.Spot; light.spotAngle = 140f; light.innerSpotAngle = 50f; light.range = 7f;
                        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        light.color = new Color(1f, 0.8f, 0.55f); light.intensity = 2.2f;
                        break;
                    default: light.color = new Color(1f, 0.74f, 0.48f); light.intensity = 1.3f * Mathf.Max(0.6f, l.intensity) + 0.4f; break;   // table lamps, 2700 K
                }
            }
            // Lena's reading lamp on her chart table (the observatory read murky under the moon alone: house-1.md).
            {
                var go = new GameObject("Lamp: Lena's reading lamp (table)");
                go.transform.SetParent(lamps, false);
                go.transform.localPosition = new Vector3(-4.0f, DreamHouse.Attic + 1.15f, 7.1f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point; light.range = 4.5f; light.intensity = 1.6f; light.color = new Color(1f, 0.78f, 0.5f);
                light.shadows = LightShadows.None;
            }
            fixtures.Build("Lamp fixtures", lamps, false);
            // Reflection probes, baked (realtime probes never render: realtime probes are off in every quality level,
            // perf.md): a storey each and the hall's stairwell. Look's bake (or the probes' own) fills them; until then
            // they fall back to the sky.
            foreach (var (name, center, size) in new[]
            {
                ("ground", new Vector3(0f, 1.45f, 5.5f), new Vector3(15.5f, 2.9f, 11.5f)),
                ("upper", new Vector3(0f, 4.5f, 5.5f), new Vector3(15.5f, 2.6f, 11.5f)),
                ("basement", new Vector3(0f, -1.55f, 5.5f), new Vector3(15.5f, 2.5f, 11.5f)),
                ("attic", new Vector3(0f, 7.4f, 5.5f), new Vector3(15.5f, 3.4f, 11.5f)),
            })
            {
                var pg = new GameObject("Reflections (" + name + ")");
                pg.transform.SetParent(lamps, false);
                pg.transform.localPosition = center;
                var probe = pg.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.size = size;
                probe.boxProjection = true;
                probe.resolution = 128;
                probe.hdr = true;
                probe.blendDistance = 1f;
            }
        }

        /// <summary>The bottom of a pendant model's box hanging over (x, z) under this ceiling, or the ceiling.</summary>
        static float PendantBottom(DreamHouse h, Vector3 at, float ceiling)
        {
            foreach (var part in h.parts)
            {
                if (part.model == null || !part.model.Contains("pendant")) continue;
                var p = part.piece;
                if (System.Math.Abs(p.Max.Y - ceiling) > 0.05f) continue;
                if (at.x < p.Min.X - 0.6f || at.x > p.Max.X + 0.6f || at.z < p.Min.Z - 0.6f || at.z > p.Max.Z + 0.6f) continue;
                return p.Min.Y;
            }
            return ceiling;
        }

        // =====================================================================================================
        // The memories
        // =====================================================================================================

        public static HouseMemoryView[] Memories(DreamHouse h, Transform root)
        {
            var parent = new GameObject("Memories").transform;
            parent.SetParent(root, false);
            var glow = GlowMaterial();
            var views = new List<HouseMemoryView>();
            foreach (var m in h.memories)
            {
                var go = new GameObject("Memory: " + m.name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = U(m.position);
                // Square to its wall, facing where she stands to take it (the game does the same when it moves it).
                float dx = m.standAt.X - m.position.X, dz = m.standAt.Z - m.position.Z;
                float yaw = dx * dx + dz * dz > 1e-4f ? Mathf.Round(Mathf.Atan2(dx, dz) * Mathf.Rad2Deg / 90f) * 90f : 0f;
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var view = go.AddComponent<HouseMemoryView>();
                view.id = m.id;
                Texture2D tex = m.picture != null ? DreamArt.Get(m.picture, out bool ph) : null;
                if (m.id == DreamHouseStory.MemHeightMarks) tex = Generated("house_height_marks", HeightMarks);
                if (m.id == DreamHouseStory.MemSaturdayPaper) tex = Generated("house_paper_hat", PaperHat);
                if (m.id == DreamHouseStory.MemLenaThursdays) tex = Generated("house_star_wheel", StarWheel);
                view.picture = tex;
                var thing = new GameObject("Thing").transform;
                thing.SetParent(go.transform, false);
                float size = Thing(m, thing, tex);
                // The glow: a soft camera-facing disc round the thing (depth-faded: no hard edge where it meets the wall or
                // the bookcase), a little in front of it; and a small warm light that lights the thing itself.
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.name = "Glow";
                g.transform.SetParent(go.transform, false);
                g.transform.localPosition = new Vector3(0f, 0f, 0.06f);
                g.transform.localScale = Vector3.one * size;
                var gr = g.GetComponent<MeshRenderer>();
                gr.sharedMaterial = glow;
                gr.shadowCastingMode = ShadowCastingMode.Off;
                gr.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(g, 0);
                view.glow = g.transform;
                var lamp = new GameObject("Glow light").AddComponent<Light>();
                lamp.transform.SetParent(go.transform, false);
                lamp.transform.localPosition = new Vector3(0f, 0.12f, 0.4f);
                lamp.type = LightType.Point;
                lamp.range = 1.8f;
                lamp.intensity = 0.55f;
                lamp.color = new Color(1f, 0.82f, 0.55f);
                lamp.shadows = LightShadows.None;
                view.lamp = lamp;
                views.Add(view);
            }
            return views.ToArray();
        }

        /// <summary>The memories' glow material (the soft disc shader in Dream/House/Shaders).</summary>
        public static Material GlowMaterial() => GlowMaterial("Memory", new Color(1f, 0.82f, 0.5f), 0.85f);

        /// <summary>A soft glow material (JamKit/House/MemoryGlow) in a colour.</summary>
        public static Material GlowMaterial(string name, Color colour, float intensity)
        {
            string path = HouseMaterials.MatDir + "/VHH_" + name + "Glow.mat";
            DreamEscapeBuilder.EnsureFolder(HouseMaterials.MatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("JamKit/House/MemoryGlow");
            if (m == null) { m = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            if (shader != null) m.shader = shader;
            m.SetColor("_Color", colour);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Softness", 0.4f);
            m.renderQueue = 3010;
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject PictureQuad(Transform t, string name, Material mat, Vector3 pos, Vector2 size, Quaternion? rot = null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(t, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = rot ?? Quaternion.Euler(0f, 180f, 0f);   // a quad shows its -z side: turned to face +z
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return q;
        }

        /// <summary>The thing a memory is, readable, in its local space (it faces +z, toward where she stands; its back
        /// against its wall or its foot on its table). Returns the glow's size.</summary>
        static float Thing(HouseMemory m, Transform t, Texture2D tex)
        {
            Material Pic(string key) => PictureMaterial(key, tex);
            var b = new MeshBuilder { Kind = DrawnKind.Free, Owner = m.name, Record = null };
            var paper = HouseMaterials.Paint("MemoryPaper", new Color(0.97f, 0.95f, 0.9f), 0.6f);
            float glow = 0.8f;
            switch (m.id)
            {
                case DreamHouseStory.MemRadio:
                {
                    var model = HouseMaterials.Model("living.radio", out string id);
                    if (model != null)
                    {
                        var radio = Fit(model, id, t, new Vector3(-0.17f, 0f, -0.08f), new Vector3(0.17f, 0.2f, 0.08f), 0f, "The radio");
                        foreach (var tr in radio.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(tr.gameObject, 0);
                        Fitted.RemoveAt(Fitted.Count - 1);
                    }
                    glow = 0.75f;
                    break;
                }
                case DreamHouseStory.MemSaturdayPaper:
                {
                    // Today's paper folded into a hat: two newsprint triangles leaning together on a brim band.
                    var news = Pic("house_paper_hat");
                    const float w = 0.16f, ht = 0.17f, dd = 0.045f;
                    b.Box(new Vector3(-w, 0f, -dd - 0.004f), new Vector3(w, 0.035f, dd + 0.004f), paper);
                    b.Owner = m.name;
                    var apexF = new Vector3(0f, ht, 0.004f); var apexB = new Vector3(0f, ht, -0.004f);
                    b.Tri(new Vector3(-w, 0.035f, dd), new Vector3(w, 0.035f, dd), apexF, news);
                    b.Tri(new Vector3(w, 0.035f, dd), new Vector3(-w, 0.035f, dd), apexF, news);
                    b.Tri(new Vector3(w, 0.035f, -dd), new Vector3(-w, 0.035f, -dd), apexB, news);
                    b.Tri(new Vector3(-w, 0.035f, -dd), new Vector3(w, 0.035f, -dd), apexB, news);
                    glow = 0.6f;
                    break;
                }
                case DreamHouseStory.MemLenaThursdays:
                {
                    // Lena's planisphere: a card star wheel on its brass-rimmed base, tilted up on a little easel to be read.
                    var brass = HouseMaterials.Paint("Brass", new Color(0.78f, 0.6f, 0.3f), 1.7f);
                    b.Box(new Vector3(-0.15f, 0f, -0.06f), new Vector3(0.15f, 0.012f, 0.1f), HouseMaterials.TrimWalnut());
                    b.Box(new Vector3(-0.02f, 0.012f, -0.07f), new Vector3(0.02f, 0.14f, -0.05f), brass);
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(disc.GetComponent<Collider>());
                    disc.name = "The star wheel";
                    disc.transform.SetParent(t, false);
                    disc.transform.localPosition = new Vector3(0f, 0.15f, 0.0f);
                    disc.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
                    disc.transform.localScale = new Vector3(0.28f, 0.004f, 0.28f);
                    disc.GetComponent<MeshRenderer>().sharedMaterial = Pic("house_star_wheel");
                    glow = 0.7f;
                    break;
                }
                case DreamHouseStory.MemPlaceCard:
                {
                    // A folded place card standing on the table: her name side toward her chair.
                    var card = Pic(m.picture);
                    PictureQuad(t, "Place card (front)", card, new Vector3(0f, 0.037f, 0.012f), new Vector2(0.12f, 0.075f), Quaternion.Euler(-18f, 180f, 0f));
                    PictureQuad(t, "Place card (back)", paper, new Vector3(0f, 0.037f, -0.012f), new Vector2(0.12f, 0.075f), Quaternion.Euler(18f, 0f, 0f));
                    b.Box(new Vector3(-0.06f, 0f, -0.013f), new Vector3(0.06f, 0.002f, 0.013f), paper);
                    glow = 0.45f;
                    break;
                }
                case DreamHouseStory.MemHeightMarks:
                {
                    // Pencil marks up the door frame (on the casing's face).
                    PictureQuad(t, "Height marks", Pic("house_height_marks"), new Vector3(0f, 0f, 0.012f), new Vector2(0.07f, 1.3f));
                    glow = 0.9f;
                    break;
                }
                case DreamHouseStory.MemBadDream:
                {
                    // THE BAD DREAM pinned to the wall over his desk: the paper, its curl, two red pins.
                    PictureQuad(t, "THE BAD DREAM", Pic(m.picture), new Vector3(0f, 0f, 0.006f), new Vector2(0.42f, 0.3f));
                    b.Box(new Vector3(-0.215f, -0.155f, 0f), new Vector3(0.215f, 0.155f, 0.004f), paper);
                    var pin = HouseMaterials.Paint("Pin", new Color(0.8f, 0.15f, 0.12f), 1.5f);
                    b.Box(new Vector3(-0.19f, 0.12f, 0.004f), new Vector3(-0.17f, 0.14f, 0.016f), pin);
                    b.Box(new Vector3(0.17f, 0.12f, 0.004f), new Vector3(0.19f, 0.14f, 0.016f), pin);
                    glow = 0.8f;
                    break;
                }
                case DreamHouseStory.MemCalendar:
                {
                    // The calendar on the fridge door: a card back, the month, a ring binding, a magnet clip.
                    b.Box(new Vector3(-0.16f, -0.24f, 0f), new Vector3(0.16f, 0.24f, 0.005f), paper);
                    PictureQuad(t, "The calendar", Pic(m.picture), new Vector3(0f, -0.02f, 0.0065f), new Vector2(0.3f, 0.42f));
                    var ring = HouseMaterials.Metal();
                    b.Box(new Vector3(-0.14f, 0.2f, 0.005f), new Vector3(0.14f, 0.212f, 0.012f), ring);
                    b.Box(new Vector3(-0.03f, 0.215f, 0f), new Vector3(0.03f, 0.25f, 0.018f), HouseMaterials.Paint("Magnet", new Color(0.86f, 0.3f, 0.22f), 1.4f));
                    glow = 0.95f;
                    break;
                }
                default:
                {
                    // The photo of the four of them at the lake: framed, on the wall.
                    var wood = HouseMaterials.TrimWalnut();
                    const float fw = 0.25f, fh = 0.2f, bw = 0.035f;
                    b.Box(new Vector3(-fw, -fh, 0f), new Vector3(fw, fh, 0.008f), paper);
                    b.Box(new Vector3(-fw, fh - bw, 0f), new Vector3(fw, fh, 0.022f), wood);
                    b.Box(new Vector3(-fw, -fh, 0f), new Vector3(fw, -fh + bw, 0.022f), wood);
                    b.Box(new Vector3(-fw, -fh + bw, 0f), new Vector3(-fw + bw, fh - bw, 0.022f), wood);
                    b.Box(new Vector3(fw - bw, -fh + bw, 0f), new Vector3(fw, fh - bw, 0.022f), wood);
                    PictureQuad(t, "The photo", Pic(m.picture ?? m.id), new Vector3(0f, 0f, 0.0095f), new Vector2(fw * 2f - bw * 2f - 0.03f, fh * 2f - bw * 2f - 0.03f));
                    glow = 0.9f;
                    break;
                }
            }
            b.Record = null;
            if (!b.Empty)
            {
                var go = b.Build("Made", t, false);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
            }
            return glow;
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
