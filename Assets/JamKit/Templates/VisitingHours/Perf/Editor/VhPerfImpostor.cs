using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Cross-billboard impostors for far trees and backdrop houses, captured from the real mesh under the scene's own light
    /// (docs/redesign/perf.md "Impostors"): the object is rendered from <c>planes</c> directions (orthographic, post off, on
    /// black and on white to recover its alpha), the views packed side by side into one texture, and a mesh of that many
    /// crossed quads made to carry them. The material is URP Unlit with alpha clip (the light is in the picture; the haze pass
    /// still fogs it by depth), double-sided, casting no shadow. One draw call and 4-6 triangles for a whole tree.
    /// <code>
    /// var imp = VhPerfImpostor.Capture(treeGo, "Assets/.../Generated/Impostors/maple_a", 256, 3);
    /// VhPerfLod.AddLods(treeGo, new[] { new VhPerfLod.Level(50f, 0.3f) }, cullAt: 0f, farRenderers: new[] { imp.Attach(treeGo.transform) });
    /// // Many trees of one kind: capture once, then imp.Attach(otherTree.transform) for each (same scale and yaw), or emit
    /// // its quads into a merged mesh with imp.cells (the atlas rects) and imp.size.
    /// </code>
    /// Needs a graphics device (not -nographics); returns null otherwise.
    /// </summary>
    public static class VhPerfImpostor
    {
        public const int CaptureLayer = 31;

        public sealed class Result
        {
            public Material material;
            public Texture2D texture;
            /// <summary>The quads in the source's local space (centred on its bounds).</summary>
            public Mesh mesh;
            /// <summary>Each plane's rect in the texture (UV space).</summary>
            public Rect[] cells;
            /// <summary>The source's bounds in its local space: the quads span size.x (diameter) by size.y.</summary>
            public Vector3 center, size;
            public int planes;

            /// <summary>A child carrying the impostor (shadowless, unlit) under <paramref name="parent"/>.</summary>
            public Renderer Attach(Transform parent, string name = "Impostor")
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return r;
            }
        }

        /// <summary>
        /// Captures <paramref name="source"/> (as lit in the open scene) into "<paramref name="assetBase"/>.png / .mat /
        /// _mesh.asset". <paramref name="cellSize"/> pixels a view; <paramref name="planes"/> crossed quads (2 or 3).
        /// </summary>
        public static Result Capture(GameObject source, string assetBase, int cellSize = 256, int planes = 3)
        {
            if (source == null) return null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("JAMKIT_PERF impostor capture needs a graphics device (run Unity without -nographics): " + source.name);
                return null;
            }
            planes = Mathf.Clamp(planes, 1, 4);
            var renderers = source.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            var tr = source.transform;
            // Bounds in the source's local space.
            bool any = false;
            var lb = new Bounds();
            foreach (var r in renderers)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var l = tr.InverseTransformPoint(c);
                    if (!any) { lb = new Bounds(l, Vector3.zero); any = true; } else lb.Encapsulate(l);
                }
            }
            float diameter = Mathf.Max(lb.size.x, lb.size.z) * 1.02f, height = lb.size.y * 1.02f;
            int cw = cellSize, ch = Mathf.Clamp(Mathf.RoundToInt(cellSize * height / Mathf.Max(0.01f, diameter)), 16, cellSize * 4);

            var layers = new Dictionary<GameObject, int>();
            foreach (var r in renderers) { layers[r.gameObject] = r.gameObject.layer; r.gameObject.layer = CaptureLayer; }
            var camGo = new GameObject("Impostor capture camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            cam.orthographic = true;
            cam.cullingMask = 1 << CaptureLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            var rt = new RenderTexture(cw, ch, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var atlas = new Texture2D(cw * planes, ch, TextureFormat.RGBA32, true);
            var black = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            var white = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            var cells = new Rect[planes];
            try
            {
                for (int k = 0; k < planes; k++)
                {
                    float yaw = k * 180f / planes;
                    var rot = tr.rotation * Quaternion.Euler(0f, yaw, 0f);
                    var centre = tr.TransformPoint(lb.center);
                    float dist = Mathf.Max(diameter, height) * 2f + 1f;
                    cam.transform.SetPositionAndRotation(centre - rot * Vector3.forward * dist, rot);
                    cam.orthographicSize = height * 0.5f * MaxScale(tr);
                    cam.aspect = diameter / Mathf.Max(0.01f, height);
                    cam.nearClipPlane = 0.05f;
                    cam.farClipPlane = dist * 2f + diameter * MaxScale(tr);
                    Shoot(cam, rt, Color.black, black);
                    Shoot(cam, rt, Color.white, white);
                    var pb = black.GetPixels32();
                    var pw = white.GetPixels32();
                    var outPx = new Color32[pb.Length];
                    for (int i = 0; i < pb.Length; i++)
                    {
                        // On black: c*a. On white: c*a + (1 - a). So a = 1 - (white - black).
                        float a = 1f - ((pw[i].r - pb[i].r) + (pw[i].g - pb[i].g) + (pw[i].b - pb[i].b)) / (3f * 255f);
                        a = Mathf.Clamp01(a);
                        if (a < 0.02f) { outPx[i] = new Color32(0, 0, 0, 0); continue; }
                        outPx[i] = new Color32((byte)Mathf.Min(255, pb[i].r / a), (byte)Mathf.Min(255, pb[i].g / a), (byte)Mathf.Min(255, pb[i].b / a), (byte)(a * 255f));
                    }
                    Dilate(outPx, cw, ch, 4);
                    atlas.SetPixels32(k * cw, 0, cw, ch, outPx);
                    cells[k] = new Rect(k / (float)planes, 0f, 1f / planes, 1f);
                }
                atlas.Apply(true);
            }
            finally
            {
                foreach (var kv in layers) if (kv.Key != null) kv.Key.layer = kv.Value;
                RenderTexture.active = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(black);
                Object.DestroyImmediate(white);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(assetBase + ".png")));
            File.WriteAllBytes(assetBase + ".png", atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(assetBase + ".png", ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetBase + ".png") is TextureImporter ti)
            {
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(cw * planes, ch));
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.streamingMipmaps = true;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetBase + ".png");

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetBase + ".mat");
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, assetBase + ".mat"); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            var mesh = Quads(lb.center, diameter, height, planes, cells);
            mesh.name = Path.GetFileName(assetBase) + " (impostor)";
            var meshPath = assetBase + "_mesh.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            VhPerfMesh.Save(mesh, meshPath);
            AssetDatabase.SaveAssets();
            return new Result { material = mat, texture = tex, mesh = mesh, cells = cells, center = lb.center, size = new Vector3(diameter, height, diameter), planes = planes };
        }

        static float MaxScale(Transform t)
        {
            var s = t.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        static void Shoot(Camera cam, RenderTexture rt, Color background, Texture2D into)
        {
            cam.backgroundColor = background;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            into.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            into.Apply(false);
            RenderTexture.active = prev;
        }

        /// <summary>Spreads the edge colours into the transparent pixels (no dark fringe in the mips).</summary>
        static void Dilate(Color32[] px, int w, int h, int passes)
        {
            var copy = new Color32[px.Length];
            for (int p = 0; p < passes; p++)
            {
                System.Array.Copy(px, copy, px.Length);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (copy[i].a != 0) continue;
                        int r = 0, g = 0, b = 0, n = 0;
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            var c = copy[ny * w + nx];
                            if (c.a == 0 && (c.r | c.g | c.b) == 0) continue;
                            r += c.r; g += c.g; b += c.b; n++;
                        }
                        if (n > 0) px[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                    }
            }
        }

        /// <summary><paramref name="planes"/> crossed, double-sided quads about <paramref name="center"/> (local space).</summary>
        public static Mesh Quads(Vector3 center, float diameter, float height, int planes, Rect[] cells)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int k = 0; k < planes; k++)
            {
                var rot = Quaternion.Euler(0f, k * 180f / planes, 0f);
                var right = rot * Vector3.right * (diameter * 0.5f);
                var up = Vector3.up * (height * 0.5f);
                var normal = rot * Vector3.back;
                var c = cells[k];
                int b0 = v.Count;
                v.Add(center - right - up); uv.Add(new Vector2(c.xMin, c.yMin));
                v.Add(center - right + up); uv.Add(new Vector2(c.xMin, c.yMax));
                v.Add(center + right + up); uv.Add(new Vector2(c.xMax, c.yMax));
                v.Add(center + right - up); uv.Add(new Vector2(c.xMax, c.yMin));
                for (int i = 0; i < 4; i++) n.Add(normal);
                t.Add(b0); t.Add(b0 + 1); t.Add(b0 + 2);
                t.Add(b0); t.Add(b0 + 2); t.Add(b0 + 3);
            }
            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
