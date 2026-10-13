using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>SkylinePass stage C: a Windows build of Skyline's own scenes only (the shared build settings are not
    /// touched). <c>-executeMethod JamKit.SkylineRepo.EditorTools.SkylineBuild.BuildFromCommandLine -skylineOut &lt;dir&gt;</c>
    /// build.json is always written (ok:false and an error if anything throws).</summary>
    public static class SkylineBuild
    {
        public static void BuildFromCommandLine()
        {
            string outDir = SkylinePassBatch.Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual");
            string exe = Path.GetFullPath(Path.Combine(outDir, "build", "SkylineRepo.exe"));
            bool ok = false;
            string error = null;
            int seconds = 0, errors = 0;
            try
            {
                Directory.CreateDirectory(outDir);
                if (File.Exists(exe)) File.Delete(exe);               // a stale exe must never pass for this build
                SkylineScenes.BuildAll();                             // always regenerate: no stale generated scenes
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = SkylineScenes.All,                     // the arena first, so the player opens on it
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                ok = report.summary.result == BuildResult.Succeeded;
                seconds = (int)report.summary.totalTime.TotalSeconds;
                errors = report.summary.totalErrors;
            }
            catch (Exception e)
            {
                error = e.ToString();
            }
            try
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "build.json"),
                    "{\"ok\":" + (ok ? "true" : "false") + ",\"exe\":" + SkylinePassBatch.Quote(exe) +
                    ",\"seconds\":" + seconds + ",\"errors\":" + errors +
                    ",\"error\":" + (error == null ? "null" : SkylinePassBatch.Quote(error)) + "}");
            }
            catch (Exception e)
            {
                Debug.LogError("SKYLINE_BUILD could not write build.json: " + e.Message);
                ok = false;
            }
            Debug.Log("SKYLINE_BUILD " + (ok ? "OK" : "FAILED"));
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
