using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ink.UnityIntegration;
using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JamKit.Narrative.EditorTools
{
    /// <summary>
    /// The story check, everywhere it matters: the menu (JamKit > Story > Validate Ink), every time an
    /// .ink file in the story's folder is saved (errors go to the Console at once), and before a build
    /// of the story (the build stops on an error, so a typo never reaches the jam build). It runs the
    /// ink compiler's own errors plus <see cref="StoryValidation"/>: unknown tags, ids that don't
    /// exist, expressions a character doesn't have, broken hotspot data, unreachable knots.
    /// </summary>
    public static class StoryInkCheck
    {
        public static StoryConfig LoadConfig() => AssetDatabase.LoadAssetAtPath<StoryConfig>(StoryContent.ConfigPath);

        /// <summary>Every .ink file in the main story file's folder (and below), as (file name, text).</summary>
        public static List<KeyValuePair<string, string>> InkFiles(StoryConfig config)
        {
            var files = new List<KeyValuePair<string, string>>();
            if (config == null || config.story == null) return files;
            string main = AssetDatabase.GetAssetPath(config.story);
            if (string.IsNullOrEmpty(main) || !File.Exists(main)) return files;
            string dir = Path.GetDirectoryName(main);
            foreach (var f in Directory.GetFiles(dir, "*.ink", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
                files.Add(new KeyValuePair<string, string>(Path.GetFileName(f), File.ReadAllText(f)));
            return files;
        }

        /// <summary>The full check: the ink compiler's errors, then the story validator.</summary>
        public static StoryProblems Check(StoryConfig config)
        {
            var audio = AssetDatabase.LoadAssetAtPath<AudioRegistry>(SceneBuilder.Root + "/Audio/AudioRegistry.asset");
            var problems = StoryValidation.Check(config, audio, InkFiles(config));
            if (config != null && config.story != null)
            {
                foreach (var e in config.story.errors) problems.Error(e.relativeFilePath, e.lineNumber, "ink: " + e.content);
                if (!config.story.isCompiled && config.story.errors.Count == 0) problems.Error(AssetDatabase.GetAssetPath(config.story), 0, "the ink story isn't compiled (JamKit > Rebuild Shell Scenes compiles it)");
            }
            return problems;
        }

        /// <summary>Checks, refreshes the knot list (debug Jump to knot) and logs. <paramref name="verbose"/>
        /// also logs warnings and "all good". Returns true when there are no errors.</summary>
        public static bool Run(StoryConfig config, bool verbose)
        {
            if (config == null) { Debug.LogError("Story check: no StoryConfig at " + StoryContent.ConfigPath + ". Run JamKit > Rebuild Shell Scenes."); return false; }
            var problems = Check(config);
            UpdateKnotIndex(config);
            if (problems.ErrorCount > 0)
                Debug.LogError("Story check: " + problems.ErrorCount + " problem(s) in the story. Fix these before the build:\n" + string.Join("\n", problems.Errors()));
            if (verbose && problems.WarningCount > 0)
                Debug.LogWarning("Story check: " + problems.WarningCount + " warning(s):\n" + string.Join("\n", problems.Warnings()));
            if (verbose && problems.Ok)
                Debug.Log("Story check: no problems (" + InkFiles(config).Count + " ink files, " + config.knotIndex.Count + " knots).");
            return problems.Ok;
        }

        public static void UpdateKnotIndex(StoryConfig config)
        {
            var scan = InkScan.Read(InkFiles(config));
            var knots = new List<string>();
            foreach (var k in scan.knots) if (!k.isFunction) knots.Add(k.name);
            if (config.knotIndex != null && config.knotIndex.SequenceEqual(knots)) return;
            config.knotIndex = knots;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }

        [MenuItem("JamKit/Story/Validate Ink")]
        static void ValidateMenu() => Run(LoadConfig(), true);

        [MenuItem("JamKit/Story/Open Main Ink File")]
        static void OpenMenu()
        {
            var c = LoadConfig();
            if (c == null || c.story == null) return;
            EditorUtility.OpenWithDefaultApp(Path.GetFullPath(AssetDatabase.GetAssetPath(c.story)));
        }

        [MenuItem("JamKit/Story/Forget All Playthroughs (editor saves)")]
        static void ForgetMenu()
        {
            if (Application.isPlaying) { Debug.LogWarning("Use the debug menu's \"Story: reset memory\" while playing."); return; }
            StoryStorage.DeleteAll();
            Debug.Log("Story: memory and save slots deleted (" + Application.persistentDataPath + ").");
        }
    }

    /// <summary>Checks the story whenever one of its .ink files changes.</summary>
    sealed class StoryInkPostprocessor : AssetPostprocessor
    {
        static bool _queued;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_queued || Application.isBatchMode) return;
            bool ink = false;
            foreach (var p in imported) if (p.EndsWith(".ink", StringComparison.OrdinalIgnoreCase) && p.StartsWith(StoryBuilder.Root, StringComparison.Ordinal)) ink = true;
            if (!ink) return;
            _queued = true;
            EditorApplication.delayCall += () =>
            {
                _queued = false;
                var config = StoryInkCheck.LoadConfig();
                if (config != null) StoryInkCheck.Run(config, false);
            };
        }
    }

    /// <summary>Stops a build of the story when the check finds errors.</summary>
    sealed class StoryBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var config = StoryInkCheck.LoadConfig();
            var game = AssetDatabase.LoadAssetAtPath<GameConfig>(TemplateSwitch.ActiveConfigPath);
            if (config == null || game == null || game.firstGameplayScene != config.sceneName) return;
            var problems = StoryInkCheck.Check(config);
            if (!problems.Ok)
                throw new BuildFailedException("Story check: " + problems.ErrorCount + " problem(s) in the story (JamKit > Story > Validate Ink):\n" + string.Join("\n", problems.Errors()));
        }
    }
}
