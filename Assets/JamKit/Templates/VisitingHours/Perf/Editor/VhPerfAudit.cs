using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Audits of the open scene (docs/redesign/perf.md "Audits"), each a list of findings a builder can act on:
    /// <list type="bullet">
    /// <item><see cref="Materials"/>: materials that break the SRP Batcher (a shader without a UnityPerMaterial buffer,
    /// a renderer with a MaterialPropertyBlock), repeated mesh+material pairs (instancing / merge candidates), near-duplicate
    /// materials (same shader and textures, separate assets).</item>
    /// <item><see cref="Lights"/>: realtime lights that should be baked or emissive-only, shadowed point lights (six shadow
    /// maps each), soft-shadow quality, realtime reflection probes.</item>
    /// <item><see cref="Memory"/>: mesh and texture memory, read/write meshes, uncompressed / unstreamed / oversized
    /// textures.</item>
    /// </list>
    /// Menu: JamKit > Perf > Audit Open Scene (writes out/perf-audit/&lt;scene&gt;.txt).
    /// </summary>
    public static class VhPerfAudit
    {
        public sealed class Report
        {
            public readonly List<string> findings = new List<string>();
            public readonly List<string> notes = new List<string>();
            public void Find(string s) => findings.Add(s);
            public void Note(string s) => notes.Add(s);
        }

        static readonly Dictionary<Shader, bool> SrpCache = new Dictionary<Shader, bool>();

        /// <summary>True when the shader declares its material properties in a UnityPerMaterial constant buffer (URP's own
        /// and Shader Graph shaders do; hand-written ones must).</summary>
        public static bool SrpBatcherCompatible(Shader shader)
        {
            if (shader == null) return false;
            if (SrpCache.TryGetValue(shader, out bool ok)) return ok;
            string name = shader.name;
            if (name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) || name.StartsWith("Shader Graphs/", StringComparison.Ordinal)) ok = true;
            else
            {
                string path = AssetDatabase.GetAssetPath(shader);
                ok = false;
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                    ok = File.ReadAllText(path).Contains("UnityPerMaterial");
                else if (!string.IsNullOrEmpty(path) && path.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase)) ok = true;
            }
            SrpCache[shader] = ok;
            return ok;
        }

        public static Report Materials(IList<Renderer> renderers)
        {
            var rep = new Report();
            var bad = new Dictionary<Shader, int>();
            int mpb = 0;
            var pairs = new Dictionary<string, int>();
            var mats = new HashSet<Material>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.HasPropertyBlock()) mpb++;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    mats.Add(m);
                    if (!SrpBatcherCompatible(m.shader)) bad[m.shader] = (bad.TryGetValue(m.shader, out int n) ? n : 0) + 1;
                }
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && r.sharedMaterial != null)
                {
                    string key = mf.sharedMesh.name + " | " + r.sharedMaterial.name;
                    pairs[key] = (pairs.TryGetValue(key, out int c) ? c : 0) + 1;
                }
            }
            foreach (var kv in bad)
                rep.Find("SRP Batcher: shader \"" + kv.Key.name + "\" has no UnityPerMaterial buffer (" + kv.Value + " renderer slots): each draw sets its own state. Put its properties in CBUFFER_START(UnityPerMaterial).");
            if (mpb > 0) rep.Find("SRP Batcher: " + mpb + " renderers carry a MaterialPropertyBlock, which takes them out of the SRP Batcher. Prefer a material per variant (shared), or per-instance data the shader reads another way.");
            var repeated = new List<KeyValuePair<string, int>>();
            foreach (var kv in pairs) if (kv.Value >= 12) repeated.Add(kv);
            repeated.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in repeated)
                rep.Find("Repeated: " + kv.Value + " x \"" + kv.Key + "\" as separate renderers: merge them per chunk (VhPerfBatching.MergeByMaterial) or give them a LOD/cull (VhPerfLod.CullBeyond).");
            // Near-duplicate materials: same shader, same main texture and colour, different assets.
            var sig = new Dictionary<string, List<Material>>();
            foreach (var m in mats)
            {
                string tex = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "-";
                string col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-";
                string k = m.shader.name + "|" + tex + "|" + col + "|" + string.Join(",", m.shaderKeywords);
                if (!sig.TryGetValue(k, out var list)) sig[k] = list = new List<Material>();
                list.Add(m);
            }
            foreach (var kv in sig)
                if (kv.Value.Count >= 3)
                    rep.Find("Duplicate materials: " + kv.Value.Count + " materials look identical (" + kv.Value[0].name + ", " + kv.Value[1].name + ", ...): share one.");
            rep.Note(mats.Count + " materials on " + renderers.Count + " renderers; " + bad.Count + " shader(s) outside the SRP Batcher.");
            return rep;
        }

        public static Report Lights(IList<Light> lights, IList<ReflectionProbe> probes)
        {
            var rep = new Report();
            int realtime = 0, shadowedPoint = 0, shadowedSpot = 0, highSoft = 0;
            foreach (var l in lights)
            {
                if (l == null || !l.isActiveAndEnabled) continue;
                bool baked = l.lightmapBakeType == LightmapBakeType.Baked;
                if (!baked) realtime++;
                if (l.type == LightType.Point && l.shadows != LightShadows.None && !baked) shadowedPoint++;
                if (l.type == LightType.Spot && l.shadows != LightShadows.None && !baked) shadowedSpot++;
                if (l.shadows != LightShadows.None && l.TryGetComponent<UniversalAdditionalLightData>(out var d) && d.softShadowQuality == SoftShadowQuality.High) highSoft++;
                if (l.type != LightType.Directional && !baked && l.intensity * l.range < 2f)
                    rep.Find("Weak realtime light \"" + Path(l.transform) + "\" (intensity " + l.intensity.ToString("0.##") + ", range " + l.range.ToString("0.#") + " m): bake it, or let an emissive material do it.");
            }
            if (shadowedPoint > 2) rep.Find(shadowedPoint + " realtime point lights cast shadows (six shadow maps each): keep at most 1-2 near the player (VhPerfShadows.LimitShadowedLights), the rest shadowless or baked.");
            if (shadowedSpot > 4) rep.Find(shadowedSpot + " realtime spot lights cast shadows: keep the few that matter.");
            if (highSoft > 0) rep.Find(highSoft + " light(s) ask for High soft shadows: the tier caps them (Medium on High, Low on Low).");
            if (realtime > 24) rep.Find(realtime + " realtime lights: lamps that never change should be Baked (with an emissive bulb), Mixed only where they light moving people.");
            int rtProbes = 0;
            foreach (var p in probes)
            {
                if (p == null || !p.isActiveAndEnabled) continue;
                if (p.mode == ReflectionProbeMode.Realtime)
                {
                    rtProbes++;
                    if (p.refreshMode == ReflectionProbeRefreshMode.EveryFrame) rep.Find("Reflection probe \"" + Path(p.transform) + "\" renders every frame (six cameras): bake it, or OnAwake.");
                }
            }
            if (rtProbes > 0) rep.Find(rtProbes + " realtime reflection probes: realtime probes are off in the quality levels (QualitySettings.realtimeReflectionProbes 0), so they show nothing. Bake them (Look's bake does) or set them Baked.");
            rep.Note(lights.Count + " lights (" + realtime + " realtime), " + probes.Count + " reflection probes.");
            return rep;
        }

        public static Report Memory(IList<Renderer> renderers)
        {
            var rep = new Report();
            var meshes = new HashSet<Mesh>();
            var textures = new HashSet<Texture>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) meshes.Add(smr.sharedMesh);
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    foreach (var id in m.GetTexturePropertyNameIDs())
                    {
                        var t = m.GetTexture(id);
                        if (t != null) textures.Add(t);
                    }
                }
            }
            long meshBytes = 0, readableBytes = 0, fat = 0;
            int readable = 0, embedded = 0;
            foreach (var m in meshes)
            {
                long b = VhPerfMesh.Bytes(m);
                meshBytes += b;
                if (m.isReadable) { readable++; readableBytes += b; }
                if (!EditorUtility.IsPersistent(m)) embedded++;
                for (int s = 0; s < m.vertexBufferCount; s++) if (m.GetVertexBufferStride(s) >= 48) { fat += b; break; }
            }
            rep.Note(meshes.Count + " meshes, " + Mb(meshBytes) + " (GPU copy; the same again on the CPU for the " + readable + " readable ones: " + Mb(readableBytes) + ").");
            if (readableBytes > 32L << 20) rep.Find("Read/write meshes: " + readable + " meshes, " + Mb(readableBytes) + " kept on the CPU in a build too. Mark generated meshes not readable (VhPerfMesh.MarkNotReadable / Save).");
            if (fat > 32L << 20) rep.Find("Wide vertices: " + Mb(fat) + " of meshes at 48+ bytes a vertex: VhPerfMesh.Compact before saving (36 bytes).");
            if (embedded > 50) rep.Find(embedded + " meshes live inside the scene file (not assets): the scene's text grows and loads slowly; save them as assets.");
            long texBytes = 0;
            int unstreamed = 0, uncompressed = 0, big = 0, readTex = 0;
            foreach (var t in textures)
            {
                long b = Profiler.GetRuntimeMemorySizeLong(t);
                texBytes += b;
                if (!(t is Texture2D t2)) continue;
                string path = AssetDatabase.GetAssetPath(t2);
                var imp = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) { if (t2.isReadable) readTex++; continue; }
                if (!imp.streamingMipmaps && imp.mipmapEnabled) unstreamed++;
                if (imp.textureCompression == TextureImporterCompression.Uncompressed) uncompressed++;
                if (Mathf.Max(t2.width, t2.height) > 2048) big++;
                if (imp.isReadable) readTex++;
            }
            rep.Note(textures.Count + " textures, about " + Mb(texBytes) + " (editor size; a build's is similar for compressed ones).");
            if (unstreamed > 0) rep.Find(unstreamed + " textures without mipmap streaming: VhPerfSetup.ApplyTextureBudgets turns it on (far surfaces then load small mips).");
            if (uncompressed > 0) rep.Find(uncompressed + " uncompressed textures (4-8x the memory of BC1/BC7).");
            if (big > 0) rep.Find(big + " textures over 2048: 2K for hero surfaces only.");
            if (readTex > 0) rep.Find(readTex + " readable textures (a CPU copy too): generated ones should Apply(true, true) or Compress then Apply(false, true).");
            return rep;
        }

        public static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null && sb.Length < 120; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        public static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";

        public static string Write(string title, params Report[] reports)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            foreach (var r in reports)
            {
                foreach (var n in r.notes) sb.AppendLine("  " + n);
                foreach (var f in r.findings) sb.AppendLine("  - " + f);
            }
            return sb.ToString();
        }

        [MenuItem("JamKit/Perf/Audit Open Scene")]
        static void MenuAudit()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string text = Write("Perf audit: " + scene, Materials(renderers), Lights(lights, probes), Memory(renderers));
            Directory.CreateDirectory("out/perf-audit");
            File.WriteAllText("out/perf-audit/" + scene + ".txt", text);
            Debug.Log(text);
        }
    }
}
