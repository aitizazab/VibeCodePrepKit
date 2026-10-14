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
    /// tests synchronously and builds the pipeline's scenes, lints the arena (SkylineLint; its problems go in editor-batch.json's
    /// "lint" block and never change "ok"), then writes editor-batch.json and exits.
    /// <c>Unity.exe -batchmode -projectPath . -executeMethod JamKit.SkylineRepo.EditorTools.SkylinePassBatch.RunFromCommandLine -skylineOut out/skyline/&lt;stamp&gt;</c></summary>
    public static class SkylinePassBatch
    {
        // JamKit.SkylineRepo.Tests (Editor/Tests): the comic-ink shader, renderer feature and converter (docs/SKYLINE-STYLE.md).
        static readonly string[] TestAssemblies = { "JamKit.Skyline.Tests", "JamKit.SkylineRepo.Tests" };

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

        /// <summary>A full-name regex matching every test except the [Explicit] ones (sweeps and design aids that print and
        /// assert nothing): Unity's runner runs Explicit tests when no test is named, and the fight sweep outgrew its 180 s
        /// timeout on 2026-10-14. Null when there are none.</summary>
        static string NotExplicitPattern()
        {
            var names = new List<string>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (Array.IndexOf(TestAssemblies, asm.GetName().Name) < 0) continue;
                foreach (var type in asm.GetTypes())
                {
                    bool typeExplicit = HasExplicit(type.GetCustomAttributes(true));
                    if (typeExplicit) { names.Add(type.FullName); continue; }
                    foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                        if (HasExplicit(m.GetCustomAttributes(true))) names.Add(type.FullName + "." + m.Name);
                }
            }
            if (names.Count == 0) return null;
            var alternatives = new StringBuilder();
            foreach (var n in names) alternatives.Append(alternatives.Length > 0 ? "|" : "").Append(System.Text.RegularExpressions.Regex.Escape(n));
            return "^(?!(" + alternatives + ")($|[.(]))";
        }

        static bool HasExplicit(object[] attributes)
        {
            foreach (var a in attributes) if (a.GetType().Name == "ExplicitAttribute") return true;
            return false;
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
                string notExplicit = NotExplicitPattern();
                if (notExplicit != null) filter.groupNames = new[] { notExplicit };
                api.Execute(new ExecutionSettings(filter) { runSynchronously = true });
                scenes = SkylineScenes.BuildAll();
            }
            catch (Exception e)
            {
                error = e.ToString();
            }

            // The arena's lint (Task 16): reported, never part of ok (untextured stand-ins are expected until Task 21).
            var lint = new List<string>();
            if (Array.IndexOf(scenes, SkylineScenes.Arena) >= 0)
            {
                try { lint = SkylineLint.Run(SkylineScenes.Arena); }
                catch (Exception e) { lint.Add("lint failed: " + e.Message); }
            }
            Debug.Log("SKYLINE_LINT " + lint.Count + " problems");

            bool ok = error == null && c.failed == 0 && c.passed > 0 && scenes.Length == SkylineScenes.All.Length;
            var sb = new StringBuilder();
            sb.Append("{\"ok\":").Append(ok ? "true" : "false");
            sb.Append(",\"tests\":{\"passed\":").Append(c.passed).Append(",\"failed\":").Append(c.failed).Append(",\"skipped\":").Append(c.skipped);
            sb.Append(",\"failures\":[");
            for (int i = 0; i < c.failures.Count; i++) sb.Append(i > 0 ? "," : "").Append(Quote(c.failures[i]));
            sb.Append("]},\"scenes\":[");
            for (int i = 0; i < scenes.Length; i++) sb.Append(i > 0 ? "," : "").Append(Quote(scenes[i]));
            sb.Append("],\"lint\":{\"count\":").Append(lint.Count).Append(",\"problems\":[");
            for (int i = 0; i < lint.Count; i++) sb.Append(i > 0 ? "," : "").Append(Quote(lint[i]));
            sb.Append("]},\"error\":").Append(error == null ? "null" : Quote(error)).Append('}');
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
