using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JamKit.EditorTools
{
    /// <summary>
    /// One build path for the menu and for tools/build.ps1 (batch mode). Logs JAMKIT_BUILD_OK or
    /// JAMKIT_BUILD_FAIL so the script can tell a real success from Unity exiting 0 anyway.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("JamKit/Build/Windows")]
        static void MenuWindows() => Build("Windows", Path.Combine("Builds", "Windows"), false);

        [MenuItem("JamKit/Build/WebGL")]
        static void MenuWebGL() => Build("WebGL", Path.Combine("Builds", "WebGL"), false);

        /// <summary>Entry point for -executeMethod. Args: -jamTarget Windows|WebGL, -jamOut dir, -jamDev.</summary>
        public static void BuildFromCommandLine()
        {
            string target = Arg("-jamTarget") ?? "Windows";
            string outDir = Arg("-jamOut") ?? Path.Combine("Builds", target);
            bool ok;
            try { ok = Build(target, outDir, HasArg("-jamDev")); }
            catch (Exception e) { Debug.LogError("JAMKIT_BUILD_FAIL " + e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Build(string target, string outDir, bool development)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("JAMKIT_BUILD_FAIL no enabled scenes in Build Settings");
                return false;
            }

            BuildTarget buildTarget;
            string location;
            switch (target)
            {
                case "Windows":
                    buildTarget = BuildTarget.StandaloneWindows64;
                    location = Path.Combine(outDir, PlayerSettings.productName + ".exe");
                    break;
                case "WebGL":
                    buildTarget = BuildTarget.WebGL;
                    location = outDir;
                    // itch.io and most quick hosts don't send Content-Encoding headers;
                    // the fallback lets the compressed build load anyway.
                    PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
                    PlayerSettings.WebGL.decompressionFallback = true;
                    break;
                default:
                    Debug.LogError("JAMKIT_BUILD_FAIL unknown target " + target);
                    return false;
            }

            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = buildTarget,
                targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget),
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"JAMKIT_BUILD_OK {target} {summary.totalSize / (1024f * 1024f):0.0} MB in {summary.totalTime.TotalSeconds:0} s");
                return true;
            }
            Debug.LogError($"JAMKIT_BUILD_FAIL {target} {summary.result} errors={summary.totalErrors}");
            return false;
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1].Trim('"');
            return null;
        }

        static bool HasArg(string name) => Environment.GetCommandLineArgs().Contains(name);
    }
}
