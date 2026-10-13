using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>SkylinePass stage A (docs/SKYLINE-DESIGN.md 9.4): one batch-mode editor job that runs Skyline's EditMode
    /// tests synchronously and builds the pipeline's scenes, then writes editor-batch.json and exits.
    /// <c>Unity.exe -batchmode -projectPath . -executeMethod JamKit.SkylineRepo.EditorTools.SkylinePassBatch.RunFromCommandLine -skylineOut out/skyline/&lt;stamp&gt;</c></summary>
    public static class SkylinePassBatch
    {
        static readonly string[] TestAssemblies = { "JamKit.Skyline.Tests" };

        sealed class Collector : ICallbacks
        {
            public int passed, failed, skipped;
            public readonly List<string> failures = new List<string>();
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                switch (result.TestStatus)
                {
                    case TestStatus.Passed: passed++; break;
                    case TestStatus.Failed: failed++; failures.Add(result.FullName + ": " + result.Message); break;
                    default: skipped++; break;
                }
            }
        }

        public static void RunFromCommandLine()
        {
            string outDir = Arg("-skylineOut") ?? Path.Combine("out", "skyline", "manual");
            Directory.CreateDirectory(outDir);
            var c = new Collector();
            string error = null;
            string[] scenes = Array.Empty<string>();
            try
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(c);
                var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = TestAssemblies };
                api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
                scenes = SkylineScenes.BuildAll();
            }
            catch (Exception e)
            {
                error = e.ToString();
            }

            bool ok = error == null && c.failed == 0 && c.passed > 0 && scenes.Length == SkylineScenes.All.Length;
            var sb = new StringBuilder();
            sb.Append("{\"ok\":").Append(ok ? "true" : "false");
            sb.Append(",\"tests\":{\"passed\":").Append(c.passed).Append(",\"failed\":").Append(c.failed).Append(",\"skipped\":").Append(c.skipped);
            sb.Append(",\"failures\":[");
            for (int i = 0; i < c.failures.Count; i++) sb.Append(i > 0 ? "," : "").Append(Quote(c.failures[i]));
            sb.Append("]},\"scenes\":[");
            for (int i = 0; i < scenes.Length; i++) sb.Append(i > 0 ? "," : "").Append(Quote(scenes[i]));
            sb.Append("],\"error\":").Append(error == null ? "null" : Quote(error)).Append('}');
            File.WriteAllText(Path.Combine(outDir, "editor-batch.json"), sb.ToString());
            Debug.Log("SKYLINE_BATCH " + (ok ? "OK" : "FAILED") + " passed=" + c.passed + " failed=" + c.failed);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The value after <paramref name="name"/> on the command line, or null. Used by SkylineBuild and SkylineCapture too.</summary>
        public static string Arg(string name) => SkylineRunArgs.Value(name);

        /// <summary>A JSON string literal (the editor has no JSON writer for loose values).</summary>
        public static string Quote(string s) =>
            "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
    }
}
