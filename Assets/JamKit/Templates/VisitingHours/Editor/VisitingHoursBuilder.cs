using System;
using System.Collections.Generic;
using System.IO;
using JamKit.Doomlike;
using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// Builds the Visiting Hours slice: the art (real files from Art/, neutral stand-ins for the
    /// missing ones), the data (only what's missing), its GameConfig preset and the scene (the
    /// first-person body and camera; the places are built in code when they start). Runs as a step
    /// of "JamKit/Rebuild Shell Scenes" (batch: SceneBuilder.RebuildFromCommandLine), so the scene is
    /// always in Build Settings. Delete the Templates/VisitingHours folder and it all disappears.
    /// </summary>
    public static class VisitingHoursBuilder
    {
        public const string Root = "Assets/JamKit/Templates/VisitingHours";
        public const string SceneName = "VisitingHours";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/VisitingHoursGameConfig.asset";
        public const string StylePath = Root + "/UI/VisitingHours.uss";

        [JamKitBuildStep(230)]
        static void Build(SceneBuilder.Context ctx)
        {
            var scene = BuildEverything(ctx, false);
            if (string.IsNullOrEmpty(scene)) return;
            // The active template's scene goes right after Boot and Title.
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        public static string BuildEverything(SceneBuilder.Context ctx, bool resetContent)
        {
            foreach (var d in new[] { Root + "/Data", Root + "/Scenes", Root + "/Generated", Root + "/Audio" }) EnsureFolder(d);
            SceneBuilder.CompileInk();
            var art = VisitingHoursArt.MakeAll();
            var config = VisitingHoursContent.EnsureAll(art, resetContent);
            AssetDatabase.SaveAssets();
            MakePreset(ctx);
            UseOwnCredits(ctx);
            string path = BuildScene();
            // A new single scene unloads unused assets, which leaves earlier references "destroyed": reload.
            config = AssetDatabase.LoadAssetAtPath<VisitingHoursConfig>(VisitingHoursContent.ConfigPath);
            LogProblems(config);
            Debug.Log("JAMKIT_VH_OK " + path);
            return path;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static bool LogProblems(VisitingHoursConfig config)
        {
            var problems = VisitingHoursValidation.Problems(config);
            foreach (var p in problems) Debug.LogError("Visiting Hours: " + p);
            return problems.Count == 0;
        }

        [MenuItem("JamKit/Visiting Hours/Validate Content")]
        static void ValidateMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<VisitingHoursConfig>(VisitingHoursContent.ConfigPath);
            if (LogProblems(config)) Debug.Log("Visiting Hours content: no problems found.");
        }

        [MenuItem("JamKit/Visiting Hours/Reset Content to Defaults")]
        static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset Visiting Hours content?",
                    "The config (journal entries, pairs, Walt's argument, examinables, walking), the portraits and the opening go back to the slice's defaults. Your changes to them are lost. (The ink, the art and the voices are not touched.)",
                    "Reset", "Cancel")) return;
            var art = VisitingHoursArt.MakeAll();
            LogProblems(VisitingHoursContent.EnsureAll(art, true));
            AssetDatabase.SaveAssets();
        }

        // ---------- Preset ----------

        /// <summary>The GameConfig values "Use Template > Visiting Hours" copies into the active config. Made once; edit it freely.</summary>
        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Visiting Hours";
            p.tagline = "";
            p.firstGameplayScene = SceneName;
            p.titleMusic = "music_vh_sad_pad";
            p.gameMusic = "";
            p.showHowToPlayOnFirstRun = false; // Mags explains the rig; Day 1 shows a controls line
            p.showPixelLookSetting = false;    // paintings, not pixels
            p.showTextSettings = true;
            p.showActionSettings = true;       // mouse sensitivity, invert Y, the arguments' difficulty
            p.showBestTime = false;
            p.chooseDifficultyOnPlay = false;  // the mood starts at once; difficulty is in Settings
            p.showControlLegend = false;
            p.credits = ctx != null && ctx.config != null ? ctx.config.credits : null;
            p.audio = ctx != null && ctx.config != null ? ctx.config.audio : null;
            p.howToPlay = new List<HowToPlayPage>
            {
                new HowToPlayPage
                {
                    title = "Walk and look",
                    body = "WASD or the left stick to walk, the mouse or the right stick to look. Shift or a click of the left stick (L3) to hurry. Hold Q or F (LB or RB) to peek round a corner.\nE (X) looks at something properly, or talks to someone.",
                },
                new HowToPlayPage
                {
                    title = "The journal",
                    body = "J, Tab or (View) opens Nadia's notebook. Click an entry to read and pick it; pick two and Combine: if they fit, they make something new.",
                },
                new HowToPlayPage
                {
                    title = "Arguments",
                    body = "Move the small light with WASD or the stick and keep it out of the way. Things you've worked out drift into the box: touch one to say it.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        public const string CreditsPath = Root + "/Credits.txt";
        const string SharedCreditsPath = "Assets/JamKit/Credits.txt";

        /// <summary>Visiting Hours credits its own downloads (Kenney's furniture...): its preset uses its own
        /// credits file, and so does the active config while Visiting Hours is the template (unless the team
        /// already pointed it at a file of their own).</summary>
        static void UseOwnCredits(SceneBuilder.Context ctx)
        {
            var credits = AssetDatabase.LoadAssetAtPath<TextAsset>(CreditsPath);
            if (credits == null) return;
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath);
            if (preset != null && preset.credits != credits)
            {
                preset.credits = credits;
                EditorUtility.SetDirty(preset);
            }
            var active = ctx != null ? ctx.config : null;
            if (active != null && active.firstGameplayScene == SceneName && (active.credits == null || AssetDatabase.GetAssetPath(active.credits) == SharedCreditsPath))
            {
                active.credits = credits;
                EditorUtility.SetDirty(active);
            }
        }

        // ---------- Scene ----------

        static string BuildScene()
        {
            var scene = SceneBuilder.NewScene(Color.black, out var cam);
            var config = AssetDatabase.LoadAssetAtPath<VisitingHoursConfig>(VisitingHoursContent.ConfigPath);

            // The body (yaw) > head (pitch) > rig (bob) > camera, like the DOOM-like's, without the pixel look.
            var playerGo = new GameObject("Nadia");
            var cc = playerGo.AddComponent<CharacterController>();
            var player = playerGo.AddComponent<DoomPlayer>();
            player.controller = cc;
            var head = new GameObject("Head").transform;
            head.SetParent(playerGo.transform, false);
            head.localPosition = new Vector3(0f, config != null ? config.player.eyeHeight : 1.5f, 0f);
            var rig = new GameObject("Rig").transform;
            rig.SetParent(head, false);
            cam.transform.SetParent(rig, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.orthographic = false;
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var shaker = cam.gameObject.AddComponent<CameraShaker>();
            shaker.strength = 0.12f;
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = false;
            player.head = head;
            player.rig = rig;
            player.cam = cam;

            var world = new GameObject("World").transform;
            var game = new GameObject("Visiting Hours").AddComponent<VisitingHoursGame>();
            game.config = config;
            game.style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (game.style == null) Debug.LogError("JamKit: " + StylePath + " is missing.");
            game.player = player;
            game.cam = cam;
            game.worldRoot = world;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }
    }
}
