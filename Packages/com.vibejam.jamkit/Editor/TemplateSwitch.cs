using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JamKit.Shell;
using UnityEditor;
using UnityEngine;

namespace JamKit.EditorTools
{
    /// <summary>
    /// Points the game at one template: copies the template's GameConfig preset (title, tagline,
    /// first gameplay scene, music, How to Play cards) into the active GameConfig and puts its scene
    /// right after Boot and Title in Build Settings. Everything else in the active config (audio,
    /// portraits, credits, debug option) is left alone. Each template adds its own menu item under
    /// "JamKit/Use Template/..." and a batch method that calls <see cref="Use"/>.
    /// </summary>
    public static class TemplateSwitch
    {
        public const string ActiveConfigPath = SceneBuilder.ResourcesDir + "/GameConfig.asset";

        /// <summary>Applies <paramref name="preset"/> and the scene order. Logs JAMKIT_TEMPLATE_OK on success.</summary>
        public static bool Use(string templateName, GameConfig preset, string scenePath)
        {
            var active = AssetDatabase.LoadAssetAtPath<GameConfig>(ActiveConfigPath);
            if (active == null) { Debug.LogError("JAMKIT_TEMPLATE_FAIL no GameConfig at " + ActiveConfigPath + ". Run JamKit > Rebuild Shell Scenes."); return false; }
            if (preset == null) { Debug.LogError("JAMKIT_TEMPLATE_FAIL no preset for " + templateName + ". Run JamKit > Rebuild Shell Scenes."); return false; }
            if (!File.Exists(scenePath)) { Debug.LogError("JAMKIT_TEMPLATE_FAIL scene " + scenePath + " doesn't exist. Run JamKit > Rebuild Shell Scenes."); return false; }

            CopyPreset(preset, active);
            active.firstGameplayScene = Path.GetFileNameWithoutExtension(scenePath);
            EditorUtility.SetDirty(active);

            var scenes = new List<EditorBuildSettingsScene>();
            string boot = ScenePathOf(active.bootScene), title = ScenePathOf(active.titleScene);
            foreach (var path in new[] { boot, title, scenePath })
                if (!string.IsNullOrEmpty(path) && File.Exists(path) && scenes.All(s => s.path != path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (scenes.All(x => x.path != s.path) && File.Exists(s.path)) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("JAMKIT_TEMPLATE_OK " + templateName + ": first gameplay scene " + active.firstGameplayScene
                + "; build order " + string.Join(", ", scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
            return true;
        }

        /// <summary>For batch methods: run <paramref name="body"/> and exit with 0 (true) or 1.</summary>
        public static void RunAndExit(Func<bool> body)
        {
            bool ok;
            try { ok = body(); }
            catch (Exception e) { Debug.LogError("JAMKIT_TEMPLATE_FAIL " + e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Saves the active config's template fields as a preset (once), e.g. the sandbox's own.</summary>
        public static GameConfig EnsurePresetFromActive(string presetPath, string expectedScene)
        {
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(presetPath);
            if (preset != null) return preset;
            var active = AssetDatabase.LoadAssetAtPath<GameConfig>(ActiveConfigPath);
            if (active == null || active.firstGameplayScene != expectedScene) return null;
            preset = ScriptableObject.CreateInstance<GameConfig>();
            CopyPreset(active, preset);
            preset.firstGameplayScene = expectedScene;
            preset.audio = active.audio;
            preset.credits = active.credits;
            AssetDatabase.CreateAsset(preset, presetPath);
            AssetDatabase.SaveAssets();
            return preset;
        }

        static void CopyPreset(GameConfig from, GameConfig to)
        {
            to.gameTitle = from.gameTitle;
            to.tagline = from.tagline;
            to.firstGameplayScene = from.firstGameplayScene;
            to.titleMusic = from.titleMusic;
            to.gameMusic = from.gameMusic;
            to.showHowToPlayOnFirstRun = from.showHowToPlayOnFirstRun;
            to.showPixelLookSetting = from.showPixelLookSetting;
            to.showTextSettings = from.showTextSettings;
            to.showActionSettings = from.showActionSettings;
            to.showBestTime = from.showBestTime;
            to.chooseDifficultyOnPlay = from.chooseDifficultyOnPlay;
            to.showControlLegend = from.showControlLegend;
            to.howToPlay = new List<HowToPlayPage>();
            if (from.howToPlay != null)
                foreach (var p in from.howToPlay)
                    if (p != null) to.howToPlay.Add(new HowToPlayPage { title = p.title, body = p.body, image = p.image });
        }

        static string ScenePathOf(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            foreach (var s in EditorBuildSettings.scenes)
                if (Path.GetFileNameWithoutExtension(s.path) == sceneName) return s.path;
            string guess = SceneBuilder.ScenesDir + "/" + sceneName + ".unity";
            return File.Exists(guess) ? guess : null;
        }
    }
}
