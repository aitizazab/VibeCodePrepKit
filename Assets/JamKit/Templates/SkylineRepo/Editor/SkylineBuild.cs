using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>SkylinePass stage C: a Windows build of Skyline's own scenes only (the shared build settings are not
    /// touched). <c>-executeMethod JamKit.SkylineRepo.EditorTools.SkylineBuild.BuildFromCommandLine -skylineOut &lt;dir&gt;</c></summary>
    public static class SkylineBuild
    {
        public static void BuildFromCommandLine()
        {
            string outDir = SkylinePassBatch.Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual");
            Directory.CreateDirectory(outDir);
            string exe = Path.GetFullPath(Path.Combine(outDir, "build", "SkylineRepo.exe"));
            if (!File.Exists(SkylineScenes.FiringRange) || !File.Exists(SkylineScenes.Gallery)) SkylineScenes.BuildAll();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SkylineScenes.FiringRange, SkylineScenes.Gallery },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            bool ok = report.summary.result == BuildResult.Succeeded;
            File.WriteAllText(Path.Combine(outDir, "build.json"),
                "{\"ok\":" + (ok ? "true" : "false") + ",\"exe\":" + SkylinePassBatch.Quote(exe) +
                ",\"seconds\":" + (int)report.summary.totalTime.TotalSeconds + ",\"errors\":" + report.summary.totalErrors + "}");
            Debug.Log("SKYLINE_BUILD " + (ok ? "OK" : "FAILED"));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
