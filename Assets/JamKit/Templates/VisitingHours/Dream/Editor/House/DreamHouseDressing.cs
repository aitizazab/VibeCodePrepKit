using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Night 2's house dressed over its blockout (art pass 2, 2026-10-11). Visual only: the gameplay boxes keep their
    /// colliders, names, sizes and places (the routes, Theo's cover and the tests depend on them); a box that gets a
    /// real model only has its renderer switched off.
    /// <list type="bullet">
    /// <item>Furniture: Kenney's Furniture Kit (CC0, <c>ThirdParty/Kenney/FurnitureKit</c>) fitted into each box, in the
    /// dream's <c>JamKit/Dream/PastelMesh</c> look (the model's own colours made pastel, a pencil outline).</item>
    /// <item>Wallpaper and floorboards: a pattern on the Pastel walls and floors (<c>Art/Dream/dream_pattern_*.png</c>).</item>
    /// </list>
    /// A missing model or pattern leaves the plain pastel box, and the build log says so ("JAMKIT_HOUSE art: ...").
    /// </summary>
    public static class DreamHouseDressing
    {
        public const string KenneyDir = "Assets/JamKit/Templates/VisitingHours/ThirdParty/Kenney/FurnitureKit";
        public const string PatternWallpaper = "dream_pattern_wallpaper";
        public const string PatternFloorboards = "dream_pattern_floorboards";
        public const string PatternTiles = "dream_pattern_tiles";

        /// <summary>How a piece is dressed: its models (tiled along its long side when there are several), and which way
        /// the model's front faces (a world direction; zero for "doesn't matter", or a point to face for chairs).</summary>
        sealed class Dress
        {
            public string[] models;
            public Vector3 facing;
            public bool faceTablePoint;
            /// <summary>Above 0: keep the model's proportions at this height (m), centred on the box (a chair's box is
            /// only its seat, the cover Theo can't see over).</summary>
            public float height;
        }

        static readonly Vector3 TableCentre = new Vector3(-3.4f, 0f, 75.7f);

        // Matched by the start of the piece's name (DreamHouse.Build's names).
        static readonly (string prefix, Dress dress)[] Plan =
        {
            // The kitchen (the architecture pass, 2026-10-12): the south run with the sink under the window and the stove
            // where Lena stands, the west run, the fridge.
            ("Counter and stove", new Dress { models = new[] { "kitchenCabinet", "kitchenSink", "kitchenStove", "kitchenCabinet" }, facing = Vector3.forward }),
            ("Counter (west)", new Dress { models = new[] { "kitchenCabinet", "kitchenCabinet", "kitchenCabinet" }, facing = Vector3.right }),
            ("Fridge", new Dress { models = new[] { "kitchenFridgeLarge" }, facing = Vector3.right }),
            ("Dining table", new Dress { models = new[] { "table" } }),
            ("Chair", new Dress { models = new[] { "chair" }, faceTablePoint = true, height = 0.95f }),
            ("Pantry shelves", new Dress { models = new[] { "bookcaseOpen", "bookcaseOpen", "bookcaseOpen" }, facing = Vector3.right }),
            // The living room: the sofa faces the fireplace (east); the armchair turned to it; the coffee table; the bookcase.
            ("Sofa", new Dress { models = new[] { "loungeSofa" }, facing = Vector3.right }),
            ("Armchair", new Dress { models = new[] { "loungeChair" }, facing = Vector3.back }),
            ("Coffee table", new Dress { models = new[] { "tableCoffee" } }),
            ("Bookcase (living room)", new Dress { models = new[] { "bookcaseOpen" }, facing = Vector3.right }),
            // The bedrooms and the bath.
            ("Parents' bed", new Dress { models = new[] { "bedDouble" }, facing = Vector3.back }),
            ("Bathtub", new Dress { models = new[] { "bathtub" }, facing = Vector3.back }),
            ("Theo's bunk bed", new Dress { models = new[] { "bedBunk" }, facing = Vector3.back }),
            ("Theo's desk", new Dress { models = new[] { "desk" }, facing = Vector3.left }),
            ("Bookcase (Theo's room)", new Dress { models = new[] { "bookcaseOpen" }, facing = Vector3.left }),
        };

        static readonly List<string> _missing = new List<string>();
        static readonly List<string> _used = new List<string>();

        public static void Begin() { _missing.Clear(); _used.Clear(); }

        public static void Report()
        {
            Debug.Log("JAMKIT_HOUSE art: furniture models used: " + (_used.Count > 0 ? string.Join(", ", _used) : "none")
                + (_missing.Count > 0 ? "; missing (plain boxes kept): " + string.Join(", ", _missing) : ""));
        }

        /// <summary>Puts real furniture in a gameplay box when the plan has some for it. The box keeps its collider.</summary>
        public static void Furnish(string pieceName, GameObject box, Transform root)
        {
            if (pieceName.StartsWith("Curtains") || pieceName.StartsWith("Pantry curtain")) { Pleats(box); return; }
            if (pieceName.StartsWith("Coats")) { Coats(box); return; }
            // The attic's stored life (the architecture pass, second round).
            if (pieceName.StartsWith("Boxes") || pieceName.StartsWith("Crates")) { BoxStack(box, root); return; }
            if (pieceName.StartsWith("Old wardrobe") || pieceName.StartsWith("Old sofa")) { DustSheet(box, root); return; }
            if (pieceName.StartsWith("Theo's old crib")) { Crib(box, root); return; }
            if (pieceName.StartsWith("Trunk")) { Trunk(box, root); return; }
            if (pieceName.StartsWith("Wardrobe"))   // Theo's wardrobe: painted wood (its doors and clothes: DreamHouseDecor)
            {
                box.GetComponent<MeshRenderer>().sharedMaterial = DreamEscapeBuilder.Pastel("HouseWardrobe", new Color(0.62f, 0.78f, 0.86f), 0.04f);
                return;
            }
            Dress dress = null;
            foreach (var (prefix, d) in Plan)
                if (pieceName.StartsWith(prefix)) { dress = d; break; }
            if (dress == null) return;

            var prefabs = new List<GameObject>();
            foreach (var m in dress.models)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyDir + "/" + m + ".fbx");
                if (p == null) { if (!_missing.Contains(m)) _missing.Add(m); return; }
                prefabs.Add(p);
            }

            var b = new Bounds(box.transform.position, box.transform.lossyScale);
            Vector3 facing = dress.faceTablePoint ? FlatDir(TableCentre - b.center) : dress.facing;
            var parent = new GameObject(pieceName + " (furniture)").transform;
            parent.SetParent(root, false);

            // Several models tile the box's long horizontal side, in order. A row turns as one: the turn comes from the
            // first model with a clear back (a plain cabinet has none; the stove's panel does).
            float rowTurn = float.NaN;
            if (facing != Vector3.zero)
                foreach (var pf in prefabs)
                {
                    rowTurn = TurnFor(pf, facing);
                    if (!float.IsNaN(rowTurn)) break;
                }
            bool alongZ = b.size.z >= b.size.x;
            int n = prefabs.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 size = b.size, centre = b.center;
                if (n > 1)
                {
                    float len = (alongZ ? b.size.z : b.size.x) / n;
                    float start = (alongZ ? b.min.z : b.min.x) + len * (i + 0.5f);
                    if (alongZ) { size.z = len; centre.z = start; } else { size.x = len; centre.x = start; }
                }
                Fit(prefabs[i], parent, new Bounds(centre, size), float.IsNaN(rowTurn) ? 0f : rowTurn, dress.height);
                if (!_used.Contains(prefabs[i].name)) _used.Add(prefabs[i].name);
            }
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>A curtain hung in soft folds (art pass 4): the box hidden, its colour drawn as pleats along its long side.</summary>
        static void Pleats(GameObject box)
        {
            var b = new Bounds(box.transform.position, box.transform.lossyScale);
            var mat = box.GetComponent<MeshRenderer>().sharedMaterial;
            var parent = new GameObject(box.name + " (folds)").transform;
            parent.SetParent(box.transform.parent, false);
            bool alongZ = b.size.z >= b.size.x;
            float len = alongZ ? b.size.z : b.size.x, depth = alongZ ? b.size.x : b.size.z;
            int n = Mathf.Max(3, Mathf.RoundToInt(len / 0.32f));
            float w = len / n;
            for (int i = 0; i < n; i++)
            {
                float along = (alongZ ? b.min.z : b.min.x) + w * (i + 0.5f);
                float push = (i % 2 == 0 ? 0.25f : -0.25f) * Mathf.Min(depth, 0.3f);
                var c = alongZ ? new Vector3(b.center.x + push, b.center.y, along) : new Vector3(along, b.center.y, b.center.z + push);
                var s = alongZ ? new Vector3(Mathf.Min(depth, 0.3f) * 0.6f, b.size.y, w * 1.05f) : new Vector3(w * 1.05f, b.size.y, Mathf.Min(depth, 0.3f) * 0.6f);
                DreamEscapeBuilder.Box("Fold", parent, c, s, mat);
            }
            DreamEscapeBuilder.Box("Curtain rail", parent, new Vector3(b.center.x, b.max.y + 0.04f, b.center.z), alongZ ? new Vector3(0.06f, 0.06f, b.size.z + 0.2f) : new Vector3(b.size.x + 0.2f, 0.06f, 0.06f),
                DreamEscapeBuilder.Pastel("HouseRail", new Color(0.62f, 0.46f, 0.38f), 0.01f));
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>A stored-things box filled with stacked cardboard boxes (rows along its long side, layers of about
        /// 0.4 m, a slight turn each), the box hidden; it keeps its collider, so the stack is what she stands on.</summary>
        static void BoxStack(GameObject box, Transform root)
        {
            var closed = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyDir + "/cardboardBoxClosed.fbx");
            var open = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyDir + "/cardboardBoxOpen.fbx");
            if (closed == null || open == null) { if (!_missing.Contains("cardboardBoxClosed")) _missing.Add("cardboardBoxClosed"); return; }
            var b = new Bounds(box.transform.position, box.transform.lossyScale);
            var parent = new GameObject(box.name + " (boxes)").transform;
            parent.SetParent(root, false);
            bool alongZ = b.size.z >= b.size.x;
            float len = alongZ ? b.size.z : b.size.x, depth = alongZ ? b.size.x : b.size.z;
            int cols = Mathf.Max(1, Mathf.RoundToInt(len / 0.6f)), rows = Mathf.Max(1, Mathf.RoundToInt(depth / 0.7f));
            int layers = Mathf.Max(1, Mathf.RoundToInt(b.size.y / 0.42f));
            float cw = len / cols, rd = depth / rows, lh = b.size.y / layers;
            var rnd = new System.Random(StableSeed(box.name));
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    for (int l = 0; l < layers; l++)
                    {
                        float shrink = 0.86f + 0.12f * (float)rnd.NextDouble();
                        float a = (alongZ ? b.min.z : b.min.x) + cw * (c + 0.5f), d = (alongZ ? b.min.x : b.min.z) + rd * (r + 0.5f);
                        var centre = alongZ ? new Vector3(d, b.min.y + lh * l + lh * 0.5f, a) : new Vector3(a, b.min.y + lh * l + lh * 0.5f, d);
                        var size = alongZ ? new Vector3(rd * shrink, lh * 0.98f, cw * shrink) : new Vector3(cw * shrink, lh * 0.98f, rd * shrink);
                        var model = l == layers - 1 && rnd.NextDouble() < 0.3 ? open : closed;
                        Fit(model, parent, new Bounds(centre, size), rnd.Next(4) * 90f + (float)(rnd.NextDouble() - 0.5) * 10f, 0f);
                    }
            box.GetComponent<MeshRenderer>().enabled = false;
            if (!_used.Contains("cardboardBoxClosed")) _used.Add("cardboardBoxClosed");
        }

        static int StableSeed(string s)
        {
            int h = 17;
            foreach (char ch in s) h = h * 31 + ch;
            return h;
        }

        /// <summary>Something stored under a dust sheet (the old wardrobe, Theo's crib): a soft rounded sheet over the box,
        /// a fold down each long side, its legs showing under the hem.</summary>
        static void DustSheet(GameObject box, Transform root)
        {
            var b = new Bounds(box.transform.position, box.transform.lossyScale);
            var parent = new GameObject(box.name + " (dust sheet)").transform;
            parent.SetParent(root, false);
            float hem = 0.08f, h = b.size.y - hem;
            var sheet = DreamProps.RoundedBox("Dust sheet", new Vector3(b.size.x + 0.08f, h, b.size.z + 0.08f), 0.07f);
            var go = DreamProps.Show("Dust sheet", sheet, DreamProps.MeshLook("DustSheet", new Color(0.96f, 0.93f, 0.86f), 0.01f), parent, Vector3.zero, Quaternion.identity);
            go.transform.position = new Vector3(b.center.x, b.min.y + hem + h * 0.5f, b.center.z);
            bool alongZ = b.size.z >= b.size.x;
            var fold = DreamEscapeBuilder.Pastel("DustSheetFold", new Color(0.9f, 0.86f, 0.78f), 0.01f);
            foreach (float s in new[] { -1f, 1f })
                foreach (float f in new[] { -0.22f, 0.18f })
                {
                    float along = f * (alongZ ? b.size.z : b.size.x), y = b.min.y + hem + h * 0.45f;
                    var pos = alongZ ? new Vector3(b.center.x + s * (b.size.x * 0.5f + 0.045f), y, b.center.z + along)
                                     : new Vector3(b.center.x + along, y, b.center.z + s * (b.size.z * 0.5f + 0.045f));
                    DreamEscapeBuilder.Box("Sheet fold", parent, pos, alongZ ? new Vector3(0.03f, h * 0.9f, 0.08f) : new Vector3(0.08f, h * 0.9f, 0.03f), fold);
                }
            var wood = DreamEscapeBuilder.Pastel("HouseRail", new Color(0.62f, 0.46f, 0.38f), 0.01f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    DreamEscapeBuilder.Box("Leg", parent, new Vector3(b.center.x + sx * (b.size.x * 0.5f - 0.06f), b.min.y + hem * 0.5f, b.center.z + sz * (b.size.z * 0.5f - 0.06f)), new Vector3(0.06f, hem, 0.06f), wood);
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>Theo's old crib: painted posts, top rails, slats down the long sides, a little mattress and a folded
        /// blanket; the box hidden (its top is the rails she can stand on).</summary>
        static void Crib(GameObject box, Transform root)
        {
            var b = new Bounds(box.transform.position, box.transform.lossyScale);
            var parent = new GameObject(box.name + " (crib)").transform;
            parent.SetParent(root, false);
            var paint = DreamEscapeBuilder.Pastel("AtticCrib", new Color(0.98f, 0.95f, 0.88f), 0.03f);
            bool alongX = b.size.x >= b.size.z;
            float len = alongX ? b.size.x : b.size.z, wid = alongX ? b.size.z : b.size.x;
            Vector3 P(float a, float y, float c) => alongX ? new Vector3(b.center.x + a, b.min.y + y, b.center.z + c) : new Vector3(b.center.x + c, b.min.y + y, b.center.z + a);
            Vector3 S(float a, float y, float c) => alongX ? new Vector3(a, y, c) : new Vector3(c, y, a);
            float hgt = b.size.y;
            foreach (float sa in new[] { -1f, 1f })
                foreach (float sc in new[] { -1f, 1f })
                    DreamEscapeBuilder.Box("Crib post", parent, P(sa * (len * 0.5f - 0.03f), hgt * 0.5f, sc * (wid * 0.5f - 0.03f)), S(0.06f, hgt, 0.06f), paint);
            foreach (float sc in new[] { -1f, 1f })
            {
                DreamEscapeBuilder.Box("Crib rail", parent, P(0f, hgt - 0.03f, sc * (wid * 0.5f - 0.03f)), S(len, 0.05f, 0.06f), paint);
                DreamEscapeBuilder.Box("Crib rail", parent, P(0f, 0.3f, sc * (wid * 0.5f - 0.03f)), S(len, 0.05f, 0.05f), paint);
                for (float a = -len * 0.5f + 0.14f; a < len * 0.5f - 0.08f; a += 0.1f)
                    DreamEscapeBuilder.Box("Crib slat", parent, P(a, (hgt + 0.3f) * 0.5f, sc * (wid * 0.5f - 0.03f)), S(0.025f, hgt - 0.3f, 0.025f), paint);
            }
            foreach (float sa in new[] { -1f, 1f })
                DreamEscapeBuilder.Box("Crib end", parent, P(sa * (len * 0.5f - 0.03f), (hgt + 0.3f) * 0.5f, 0f), S(0.03f, hgt - 0.3f, wid - 0.06f), paint);
            DreamEscapeBuilder.Box("Crib mattress", parent, P(0f, 0.38f, 0f), S(len - 0.1f, 0.12f, wid - 0.1f), DreamEscapeBuilder.Pastel("AtticCribMattress", new Color(0.78f, 0.88f, 0.96f), 0.02f));
            DreamEscapeBuilder.Box("Folded blanket", parent, P(len * 0.22f, 0.48f, 0f), S(0.3f, 0.08f, wid * 0.55f), DreamEscapeBuilder.Pastel("AtticCribBlanket", new Color(0.98f, 0.8f, 0.45f), 0.02f));
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>An old travelling trunk: its own colour, two leather straps, a lid line, brass corners.</summary>
        static void Trunk(GameObject box, Transform root)
        {
            var b = new Bounds(box.transform.position, box.transform.lossyScale);
            box.GetComponent<MeshRenderer>().sharedMaterial = DreamEscapeBuilder.Pastel("AtticTrunk", new Color(0.36f, 0.5f, 0.62f), 0.04f);
            var parent = new GameObject(box.name + " (straps)").transform;
            parent.SetParent(root, false);
            var leather = DreamEscapeBuilder.Pastel("AtticTrunkStrap", new Color(0.55f, 0.36f, 0.28f), 0.01f);
            var brass = DreamEscapeBuilder.Pastel("DecorBrassKnob", new Color(0.95f, 0.78f, 0.38f), 0.01f);
            bool alongX = b.size.x >= b.size.z;
            foreach (float f in new[] { -0.28f, 0.28f })
            {
                var pos = alongX ? new Vector3(b.center.x + f * b.size.x, b.center.y, b.center.z) : new Vector3(b.center.x, b.center.y, b.center.z + f * b.size.z);
                DreamEscapeBuilder.Box("Trunk strap", parent, pos, alongX ? new Vector3(0.08f, b.size.y + 0.02f, b.size.z + 0.02f) : new Vector3(b.size.x + 0.02f, b.size.y + 0.02f, 0.08f), leather);
            }
            DreamEscapeBuilder.Box("Trunk lid line", parent, new Vector3(b.center.x, b.max.y - 0.14f, b.center.z), new Vector3(b.size.x + 0.015f, 0.025f, b.size.z + 0.015f), leather);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    DreamEscapeBuilder.Box("Brass corner", parent, new Vector3(b.center.x + sx * b.size.x * 0.5f, b.max.y - 0.04f, b.center.z + sz * b.size.z * 0.5f), Vector3.one * 0.1f, brass);
        }

        /// <summary>Coats on a row of hooks (art pass 4): soft rounded coats in the family's colours, the box hidden.</summary>
        static void Coats(GameObject box)
        {
            var b = new Bounds(box.transform.position, box.transform.lossyScale);
            var parent = new GameObject(box.name + " (coats)").transform;
            parent.SetParent(box.transform.parent, false);
            bool alongZ = b.size.z >= b.size.x;
            float len = alongZ ? b.size.z : b.size.x;
            var colours = new[] { new Color(0.32f, 0.33f, 0.38f), new Color(0.6f, 0.17f, 0.2f), new Color(0.88f, 0.7f, 0.3f), new Color(0.85f, 0.45f, 0.42f) };
            int n = Mathf.Max(2, Mathf.FloorToInt(len / 0.55f));
            float w = len / n;
            var coat = DreamProps.RoundedBox("Coat", new Vector3(0.24f, 1.15f, 0.48f), 0.1f);
            for (int i = 0; i < n; i++)
            {
                float along = (alongZ ? b.min.z : b.min.x) + w * (i + 0.5f);
                var c = alongZ ? new Vector3(b.center.x, b.max.y - 0.75f, along) : new Vector3(along, b.max.y - 0.75f, b.center.z);
                var rot = alongZ ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                var go = DreamProps.Show("Coat", coat, DreamProps.MeshLook("Coat" + (i % colours.Length), colours[i % colours.Length], 0.01f), parent, Vector3.zero, rot);
                go.transform.position = c;
            }
            DreamEscapeBuilder.Box("Coat rail", parent, new Vector3(b.center.x, b.max.y - 0.12f, b.center.z), alongZ ? new Vector3(0.06f, 0.06f, b.size.z) : new Vector3(b.size.x, 0.06f, 0.06f),
                DreamEscapeBuilder.Pastel("HouseRail", new Color(0.62f, 0.46f, 0.38f), 0.01f));
            box.GetComponent<MeshRenderer>().enabled = false;
        }

        static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.z)) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        /// <summary>The quarter turn (degrees) that makes the model's front face <paramref name="facing"/>, or NaN when the
        /// model has no clear back. The front is found from the mesh: the tallest part (a chair's back, a sofa's back, a
        /// bed's headboard, a stove's panel) is at the back.</summary>
        internal static float TurnFor(GameObject prefab, Vector3 facing)
        {
            var probe = Object.Instantiate(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Vector3 back = BackOf(probe);
            Object.DestroyImmediate(probe);
            if (back == Vector3.zero) return float.NaN;
            float have = Mathf.Atan2(-back.x, -back.z) * Mathf.Rad2Deg;   // the way its front faces now
            float want = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            return Mathf.Round((want - have) / 90f) * 90f;
        }

        /// <summary>One model squeezed into <paramref name="box"/> (its bottom on the box's floor), turned by
        /// <paramref name="turn"/> degrees first; with <paramref name="height"/> above 0 it keeps its proportions instead.</summary>
        static void Fit(GameObject prefab, Transform parent, Bounds box, float turn, float height)
        {
            var pivot = new GameObject(prefab.name).transform;
            pivot.SetParent(parent, false);
            var model = Object.Instantiate(prefab);   // unlinked: its colliders and animators can be removed
            model.name = prefab.name;
            model.transform.SetParent(pivot, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);

            model.transform.localRotation = Quaternion.Euler(0f, turn, 0f);

            var mb = WorldBounds(model);
            if (mb.size.x < 1e-4f || mb.size.y < 1e-4f || mb.size.z < 1e-4f) return;
            pivot.localScale = height > 0f ? Vector3.one * (height / mb.size.y)
                : new Vector3(box.size.x / mb.size.x, box.size.y / mb.size.y, box.size.z / mb.size.z);
            mb = WorldBounds(model);
            pivot.position += new Vector3(box.center.x - mb.center.x, box.min.y - mb.min.y, box.center.z - mb.center.z);

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = Pastelise(mats[k]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            GameObjectUtility.SetStaticEditorFlags(model, 0);
        }

        /// <summary>The direction (flat, a world axis) from the model's middle to its tallest part.</summary>
        static Vector3 BackOf(GameObject model)
        {
            var points = new List<Vector3>();
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                foreach (var v in f.sharedMesh.vertices) points.Add(f.transform.TransformPoint(v));
            }
            if (points.Count == 0) return Vector3.zero;
            var b = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) b.Encapsulate(p);
            float top = b.max.y - b.size.y * 0.06f;
            Vector3 sum = Vector3.zero; int count = 0;
            foreach (var p in points) if (p.y >= top) { sum += p; count++; }
            if (count == 0) return Vector3.zero;
            Vector3 d = sum / count - b.center;
            d.y = 0f;
            if (d.magnitude < b.size.magnitude * 0.05f) return Vector3.zero;   // symmetric: no clear back
            return FlatDir(d);
        }

        internal static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>The model's own material colour, made pastel, in the dream's mesh look (one material per colour name).</summary>
        internal static Material Pastelise(Material src)
        {
            Color c = Color.white;
            string key = "Kenney";
            if (src != null)
            {
                key = src.name;
                if (src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                else if (src.HasProperty("_Color")) c = src.GetColor("_Color");
            }
            key += "_" + ColorUtility.ToHtmlStringRGB(c);   // the same name can come in more than one colour
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var pastel = Color.HSVToRGB(h, s * 0.85f, Mathf.Lerp(v, 1f, 0.15f));   // softened, not washed out
            var m = DreamEscapeBuilder.Mat("Mesh_" + key, "JamKit/Dream/PastelMesh");
            m.SetColor("_Color", pastel);
            m.SetVector("_SunDir", DreamEscapeBuilder.SunDir);
            m.SetFloat("_OutlineWidth", 0.012f);
            return m;
        }

        /// <summary>Wallpaper or floorboards on a Pastel material (a brightness pattern; the colour stays).</summary>
        /// <summary>A wallpaper drawn in colour (tools/art/vh_house_patterns.py): the tile is the paper's colours, tinted by
        /// the material's colour (white: as drawn), laid in world space every <paramref name="tileMetres"/>.</summary>
        public static Material ColourPattern(Material m, string pattern, float tileMetres)
        {
            string path = null;
            foreach (var dir in new[] { DreamArt.ArtDir, DreamArt.LocalArtDir })
                if (System.IO.File.Exists(dir + "/" + pattern + ".png")) { path = dir + "/" + pattern + ".png"; break; }
            if (path == null)
            {
                if (!_missing.Contains(pattern)) _missing.Add(pattern);
                m.SetFloat("_TexStrength", 0f);
                return m;
            }
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.wrapMode != TextureWrapMode.Repeat || !imp.sRGBTexture || !imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.sRGBTexture = true;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            m.SetFloat("_TexScale", tileMetres);
            m.SetFloat("_TexMean", 1f);
            m.SetFloat("_TexStrength", 1f);
            if (!_used.Contains(pattern)) _used.Add(pattern);
            return m;
        }

        public static Material Pattern(Material m, string pattern, float tileMetres, float strength)
        {
            string path = null;
            foreach (var dir in new[] { DreamArt.ArtDir, DreamArt.LocalArtDir })
                if (System.IO.File.Exists(dir + "/" + pattern + ".png")) { path = dir + "/" + pattern + ".png"; break; }
            if (path == null)
            {
                if (!_missing.Contains(pattern)) _missing.Add(pattern);
                m.SetFloat("_TexStrength", 0f);
                return m;
            }
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.wrapMode != TextureWrapMode.Repeat || imp.sRGBTexture || !imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Default;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.sRGBTexture = false;   // a brightness pattern (mean 0.5), not a colour
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            m.SetFloat("_TexScale", tileMetres);
            m.SetFloat("_TexMean", 0.5f);
            m.SetFloat("_TexStrength", strength);
            if (!_used.Contains(pattern)) _used.Add(pattern);
            return m;
        }
    }
}
