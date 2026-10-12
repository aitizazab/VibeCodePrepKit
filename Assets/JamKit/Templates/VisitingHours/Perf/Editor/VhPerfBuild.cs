using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JamKit.Perf.EditorTools
{
    /// <summary>
    /// The build the perf probe runs in (docs/redesign/perf.md "Unity steps"): VhPerfSetup first (tier assets, quality
    /// levels; textures with -perfTextures), then the shell's Windows build with Frame Timing Stats on for the build only
    /// (so a release build reports CPU and GPU time; the project setting is put back afterwards).
    /// <code>
    /// Unity.exe -batchmode -projectPath . -executeMethod JamKit.Perf.EditorTools.VhPerfBuild.BuildFromCommandLine
    ///     -jamOut Builds/Perf [-perfDev] [-perfTextures] -logFile out/perf/build.log -quit
    /// </code>
    /// -perfDev makes a development build: the probe then also gets draw calls, SetPass calls, triangles and shadow casters
    /// (a development build runs slightly slower; use the release build's frame times for the bar). Logs JAMKIT_BUILD_OK.
    /// </summary>
    public static class VhPerfBuild
    {
        public static void BuildFromCommandLine()
        {
            bool ok = false;
            bool before = PlayerSettings.enableFrameTimingStats;
            try
            {
                VhPerfSetup.Apply(VhPerfArgs.Has("-perfTextures"));
                PlayerSettings.enableFrameTimingStats = true;
                string outDir = VhPerfArgs.Get("-jamOut") ?? Path.Combine("Builds", "Perf");
                ok = JamKit.EditorTools.BuildScript.Build("Windows", outDir, VhPerfArgs.Has("-perfDev"));
            }
            catch (Exception e) { Debug.LogError("JAMKIT_BUILD_FAIL " + e); }
            finally
            {
                PlayerSettings.enableFrameTimingStats = before;
                AssetDatabase.SaveAssets();
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
