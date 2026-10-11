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

        static readonly Vector3 TableCentre = new Vector3(-6f, 0f, 77.5f);

        // Matched by the start of the piece's name (DreamHouse.Build's names).
        static readonly (string prefix, Dress dress)[] Plan =
        {
            ("Counter and stove", new Dress { models = new[] { "kitchenCabinet", "kitchenStove", "kitchenCabinet", "kitchenSink", "kitchenCabinet", "kitchenCabinet", "kitchenCabinet" }, facing = Vector3.right }),
            ("Fridge", new Dress { models = new[] { "kitchenFridgeLarge" }, facing = Vector3.right }),
            ("Dining table", new Dress { models = new[] { "table" } }),
            ("Chair", new Dress { models = new[] { "chair" }, faceTablePoint = true, height = 0.95f }),
            ("Sofa", new Dress { models = new[] { "loungeSofa" }, facing = Vector3.forward }),
            ("Theo's bed", new Dress { models = new[] { "bedSingle" }, facing = Vector3.back }),
            ("Theo's desk", new Dress { models = new[] { "desk" }, facing = Vector3.back }),
            ("Study desk", new Dress { models = new[] { "desk" }, facing = Vector3.back }),
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

        static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.z)) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        /// <summary>The quarter turn (degrees) that makes the model's front face <paramref name="facing"/>, or NaN when the
        /// model has no clear back. The front is found from the mesh: the tallest part (a chair's back, a sofa's back, a
        /// bed's headboard, a stove's panel) is at the back.</summary>
        static float TurnFor(GameObject prefab, Vector3 facing)
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

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>The model's own material colour, made pastel, in the dream's mesh look (one material per colour name).</summary>
        static Material Pastelise(Material src)
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
            var pastel = Color.HSVToRGB(h, s * 0.62f, Mathf.Lerp(v, 1f, 0.38f));
            var m = DreamEscapeBuilder.Mat("Mesh_" + key, "JamKit/Dream/PastelMesh");
            m.SetColor("_Color", pastel);
            m.SetVector("_SunDir", DreamEscapeBuilder.SunDir);
            m.SetFloat("_OutlineWidth", 0.012f);
            return m;
        }

        /// <summary>Wallpaper or floorboards on a Pastel material (a brightness pattern; the colour stays).</summary>
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
