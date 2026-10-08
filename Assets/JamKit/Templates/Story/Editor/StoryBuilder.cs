using System;
using System.Collections.Generic;
using System.IO;
using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace JamKit.Narrative.EditorTools
{
    /// <summary>
    /// Builds the story template: placeholder art, the sample data (only what's missing), its
    /// GameConfig preset and the Story scene. Runs as a step of "JamKit/Rebuild Shell Scenes"
    /// (batch: SceneBuilder.RebuildFromCommandLine), so the scene is always in Build Settings.
    /// Delete the Templates/Story folder and it all disappears.
    /// </summary>
    public static class StoryBuilder
    {
        public const string Root = "Assets/JamKit/Templates/Story";
        public const string SceneName = "Story";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/StoryGameConfig.asset";
        public const string StylePath = Root + "/UI/Story.uss";

        [JamKitBuildStep(220)]
        static void Build(SceneBuilder.Context ctx)
        {
            var scene = BuildEverything(ctx, false);
            if (string.IsNullOrEmpty(scene)) return;
            // The active template's scene goes right after Boot and Title.
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        /// <summary>Art, content (missing assets only, or all when <paramref name="resetContent"/>), preset and scene. Returns the scene path.</summary>
        public static string BuildEverything(SceneBuilder.Context ctx, bool resetContent)
        {
            foreach (var d in new[] { Root + "/Art/Generated", Root + "/Data", Root + "/Scenes", Root + "/Resources/JamKit" }) EnsureFolder(d);
            SceneBuilder.CompileInk();
            var art = StoryArt.MakeAll();
            var config = StoryContent.EnsureAll(art, resetContent);
            AssetDatabase.SaveAssets();
            MakePreset(ctx);
            string path = BuildScene();
            // A new single scene unloads unused assets, which leaves earlier references "destroyed": reload.
            config = AssetDatabase.LoadAssetAtPath<StoryConfig>(StoryContent.ConfigPath);
            StoryInkCheck.Run(config, false);
            Debug.Log("JAMKIT_STORY_OK " + path);
            return path;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("JamKit/Story/Reset Content to Defaults")]
        static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset story content?",
                    "The story config, the two characters and the office hotspot scene go back to the sample's values. Your changes to them are lost. (The .ink files are not touched.)",
                    "Reset", "Cancel")) return;
            ResetContent();
        }

        /// <summary>Batch: <c>-executeMethod JamKit.Narrative.EditorTools.StoryBuilder.ResetContentFromCommandLine</c>.</summary>
        public static void ResetContentFromCommandLine()
        {
            bool ok;
            try { ok = ResetContent(); }
            catch (Exception e) { Debug.LogError("JAMKIT_STORY_FAIL " + e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool ResetContent()
        {
            var art = StoryArt.MakeAll();
            var config = StoryContent.EnsureAll(art, true);
            AssetDatabase.SaveAssets();
            return StoryInkCheck.Run(config, true);
        }

        // ---------- Preset ----------

        /// <summary>The GameConfig values "Use Template > Story" copies into the active config. Made once; edit it freely.</summary>
        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Last Train Home";
            p.tagline = "A short story about what we leave behind.";
            p.firstGameplayScene = SceneName;
            p.titleMusic = "music_story_calm";
            p.gameMusic = "";
            p.showHowToPlayOnFirstRun = true;
            p.showPixelLookSetting = false;
            p.showTextSettings = true;
            p.showActionSettings = false;
            p.showBestTime = false;
            p.chooseDifficultyOnPlay = false; // the story has its own text settings; no run-start picker
            p.credits = ctx.config != null ? ctx.config.credits : null;
            p.audio = ctx.config != null ? ctx.config.audio : null;
            p.howToPlay = new List<HowToPlayPage>
            {
                new HowToPlayPage
                {
                    title = "Read and choose",
                    body = "Click, Space, Enter or (A) to go on (a click while text types shows it all).\nChoices: click one, press its number, or use the arrow keys and Enter.",
                },
                new HowToPlayPage
                {
                    title = "Look around",
                    body = "Sometimes you can look around a room: click the glowing markers, or use the arrow keys and Enter.\nItems you find appear at the bottom left: click one, then click where to use it.",
                },
                new HowToPlayPage
                {
                    title = "Your story is remembered",
                    body = "L or the mouse wheel shows what was said. A: auto, S or hold Ctrl: skip what you've read, H: hide the text. F5 / F9: quick save and load. Escape: menu.\nThe game remembers how your story ended. Play it more than once.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        // ---------- Scene ----------

        static string BuildScene()
        {
            var scene = SceneBuilder.NewScene(Color.black, out var cam);
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = false;
            var config = AssetDatabase.LoadAssetAtPath<StoryConfig>(StoryContent.ConfigPath);
            var game = new GameObject("Story Game").AddComponent<StoryGame>();
            game.config = config;
            game.style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (game.style == null) Debug.LogError("JamKit: " + StylePath + " is missing.");
            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }
    }
}
