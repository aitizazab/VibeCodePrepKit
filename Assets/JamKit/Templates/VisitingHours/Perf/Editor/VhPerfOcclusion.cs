using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// Occlusion culling for the hill and the house (docs/redesign/perf.md "Occlusion"): mark occluders and occludees
    /// (<see cref="VhPerfBatching.MarkStatic"/> does it for a root), then bake. The hill's terraces and rows hide most of the
    /// town from street level; inside the house, rooms hide rooms. The bake is CPU-only and runs on the saved scene:
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfOcclusion.BakeFromCommandLine
    ///     -perfScene Assets/JamKit/Templates/VisitingHours/Dream/Scenes/DreamStreet.unity [-perfSmallestOccluder 3]
    ///     [-perfSmallestHole 0.25] [-perfBackfaceThreshold 100] -logFile out/perf/occlusion.log -quit
    /// </code>
    /// Logs JAMKIT_PERF_OCCLUSION_OK / _FAIL. Moving things (the shutter, the house's escape rooms that roll) must not be
    /// occluder-static.
    /// </summary>
    public static class VhPerfOcclusion
    {
        public static bool Bake(string scenePath, float smallestOccluder, float smallestHole, float backfaceThreshold)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            StaticOcclusionCulling.smallestOccluder = smallestOccluder;
            StaticOcclusionCulling.smallestHole = smallestHole;
            StaticOcclusionCulling.backfaceThreshold = backfaceThreshold;
            int occluders = 0, occludees = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var f = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((f & StaticEditorFlags.OccluderStatic) != 0) occluders++;
                if ((f & StaticEditorFlags.OccludeeStatic) != 0) occludees++;
            }
            Debug.Log("JAMKIT_PERF occlusion: " + occluders + " occluders, " + occludees + " occludees in " + scenePath);
            if (occluders == 0) { Debug.LogWarning("JAMKIT_PERF occlusion: no occluder-static renderers (call VhPerfBatching.MarkStatic in the builder)"); return false; }
            bool ok = StaticOcclusionCulling.Compute();
            if (ok) EditorSceneManager.SaveScene(scene);
            return ok;
        }

        public static void BakeFromCommandLine()
        {
            int code = 1;
            try
            {
                string scene = VhPerfArgs.Get("-perfScene");
                if (string.IsNullOrEmpty(scene)) throw new ArgumentException("-perfScene <scene path> is required");
                bool ok = Bake(scene, VhPerfArgs.Float("-perfSmallestOccluder", 3f), VhPerfArgs.Float("-perfSmallestHole", 0.25f), VhPerfArgs.Float("-perfBackfaceThreshold", 100f));
                Debug.Log((ok ? "JAMKIT_PERF_OCCLUSION_OK " : "JAMKIT_PERF_OCCLUSION_FAIL ") + scene);
                code = ok ? 0 : 2;
            }
            catch (Exception e) { Debug.LogException(e); Debug.Log("JAMKIT_PERF_OCCLUSION_FAIL " + e.Message); }
            EditorApplication.Exit(code);
        }
    }

    /// <summary>Command-line arguments for the Perf editor methods.</summary>
    public static class VhPerfArgs
    {
        public static string Get(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1].Trim('"');
            return null;
        }

        public static bool Has(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        public static float Float(string name, float fallback) =>
            float.TryParse(Get(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }
}
