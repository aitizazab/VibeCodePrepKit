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

namespace JamKit.Doomlike.EditorTools
{
    /// <summary>
    /// Builds the DOOM-like template: placeholder art, materials, the default content (only what's
    /// missing), its GameConfig preset and the Doomlike scene (player, camera, effects; levels are
    /// built from their maps when they start). Runs as a step of "JamKit/Rebuild Shell Scenes"
    /// (batch: SceneBuilder.RebuildFromCommandLine), so the scene is always in Build Settings.
    /// Delete the Templates/Doomlike folder and it all disappears.
    /// </summary>
    public static class DoomlikeBuilder
    {
        public const string Root = "Assets/JamKit/Templates/Doomlike";
        public const string SceneName = "Doomlike";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/DoomlikeGameConfig.asset";
        public const string MaterialDir = Root + "/Art";
        public const string StylePath = Root + "/UI/Doomlike.uss";

        [JamKitBuildStep(210)]
        static void Build(SceneBuilder.Context ctx)
        {
            var scene = BuildEverything(ctx, false);
            if (string.IsNullOrEmpty(scene)) return;
            // The active template's scene goes right after Boot and Title.
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/DOOM-like/Reset Content to Defaults")]
        static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset DOOM-like content?",
                    "Every weapon, enemy, pickup, decoration and upgrade asset in Templates/Doomlike/Data, and the config, go back to the template's defaults. Your changes to them are lost. (Map files are not touched.)",
                    "Reset", "Cancel")) return;
            ResetContent();
        }

        /// <summary>Batch: <c>-executeMethod JamKit.Doomlike.EditorTools.DoomlikeBuilder.ResetContentFromCommandLine</c>.</summary>
        public static void ResetContentFromCommandLine()
        {
            bool ok;
            try { ok = ResetContent(); }
            catch (Exception e) { Debug.LogError("JAMKIT_DOOMLIKE_FAIL " + e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool ResetContent()
        {
            EnsureFolder(Root + "/Data");
            var art = DoomlikeArt.MakeAll();
            var mats = MakeMaterials(art);
            var config = DoomlikeContent.EnsureAll(art, mats, true);
            AssetDatabase.SaveAssets();
            bool ok = DoomlikeValidation.LogProblems(config);
            Debug.Log("JAMKIT_DOOMLIKE content reset" + (ok ? "" : " (with problems, see above)"));
            return ok;
        }

        [MenuItem("JamKit/DOOM-like/Validate Content")]
        static void ValidateMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<DoomlikeConfig>(DoomlikeContent.ConfigPath);
            if (DoomlikeValidation.LogProblems(config)) Debug.Log("DOOM-like content: no problems found (" + config.LevelCount + " levels).");
        }

        [MenuItem("JamKit/DOOM-like/Preview Level 1 in Scene")] static void Preview1() => Preview(0);
        [MenuItem("JamKit/DOOM-like/Preview Level 2 in Scene")] static void Preview2() => Preview(1);
        [MenuItem("JamKit/DOOM-like/Preview Level 3 in Scene")] static void Preview3() => Preview(2);

        /// <summary>Builds a level's geometry and things into the open scene to look at (not saved
        /// with the scene; enemies show as their sprites). Play builds levels by itself.</summary>
        public static GameObject Preview(int index)
        {
            ClearPreview();
            var config = AssetDatabase.LoadAssetAtPath<DoomlikeConfig>(DoomlikeContent.ConfigPath);
            if (config == null) { Debug.LogError("No DoomlikeConfig. Run JamKit > Rebuild Shell Scenes."); return null; }
            var problems = new MapProblems();
            var map = config.ParseLevel(index, problems);
            if (map == null || !problems.Ok) { Debug.LogError("Level " + (index + 1) + ":\n" + problems); return null; }
            var parent = new GameObject("Level preview (not saved)");
            var level = LevelBuilder.Build(map, config, parent.transform, false);
            foreach (var s in level.enemySpawns)
            {
                var a = s.asset;
                var bb = Billboard.Create(a.displayName, level.thingsRoot, s.position + Vector3.up * a.hoverHeight, a.height * (a.sprite != null ? a.sprite.CellAspect : 1f), a.height,
                    config.spriteMaterial, a.sprite != null ? a.sprite.texture : null, a.tint);
                bb.SetFrame(SpriteSheet.Cell(0, 0, false));
                bb.Face(0f);
            }
            foreach (var it in level.items) it.billboard.Face(0f);
            foreach (var b in level.scenery) b.Face(0f);
            foreach (var t in parent.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            Selection.activeGameObject = parent;
            Debug.Log("Previewing level " + (index + 1) + ": " + map.name + ". JamKit > DOOM-like > Clear Level Preview removes it.");
            return parent;
        }

        [MenuItem("JamKit/DOOM-like/Clear Level Preview")]
        static void ClearPreview()
        {
            var old = GameObject.Find("Level preview (not saved)");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
        }

        /// <summary>Art, materials, content (missing assets only, or all when <paramref name="resetContent"/>),
        /// preset and scene. Returns the scene path.</summary>
        public static string BuildEverything(SceneBuilder.Context ctx, bool resetContent)
        {
            foreach (var d in new[] { Root + "/Art/Generated", Root + "/Data", Root + "/Scenes" }) EnsureFolder(d);
            var art = DoomlikeArt.MakeAll();
            var mats = MakeMaterials(art);
            var config = DoomlikeContent.EnsureAll(art, mats, resetContent);
            AssetDatabase.SaveAssets();
            MakePreset(ctx, art);
            string path = BuildScene(config);
            // A new single scene unloads unused assets, which leaves earlier references "destroyed": reload.
            config = AssetDatabase.LoadAssetAtPath<DoomlikeConfig>(DoomlikeContent.ConfigPath);
            DoomlikeValidation.LogProblems(config);
            Debug.Log("JAMKIT_DOOMLIKE_OK " + path);
            return path;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------- Materials ----------

        static DoomlikeContent.Materials MakeMaterials(DoomlikeArt.Set art)
        {
            var m = new DoomlikeContent.Materials();
            string[] wallNames = { "Wall1_Brick", "Wall2_Metal", "Wall3_Tech", "Wall4_Rock" };
            for (int i = 0; i < 4; i++) m.walls.Add(World(wallNames[i], art.walls[i]));
            m.floors.Add(World("Floor1_Tiles", art.floors[0]));
            m.floors.Add(World("Floor2_Grate", art.floors[1]));
            m.ceiling = World("Ceiling", art.ceiling);
            m.slime = World("Slime", art.slime, mat => mat.SetVector("_Scroll", new Vector4(0.06f, 0.035f, 0f, 0f)));
            m.exit = World("ExitPad", art.exitPad, mat => mat.SetFloat("_Glow", 0.85f));
            m.door = World("Door", art.door);
            m.lockedDoor = World("LockedDoor", art.lockedDoor, mat => mat.SetFloat("_Glow", 0.25f));
            m.sprite = Sprite("Sprite", null, mat => { });
            m.weapon = Sprite("Weapon", null, mat =>
            {
                mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_FogScale", 0f);
                mat.renderQueue = 4000;
            });
            m.line = Sprite("AimLine", null, mat =>
            {
                mat.SetFloat("_Glow", 1f);
                mat.SetFloat("_FogScale", 0.3f);
                mat.SetFloat("_Cutoff", 0f);
            });
            var screenShader = Shader.Find("JamKit/Doomlike/Screen");
            if (screenShader == null) throw new Exception("Shader JamKit/Doomlike/Screen not found (Templates/Doomlike/Shaders).");
            m.screen = Mat("Screen", screenShader, null, mat => { });
            return m;
        }

        static Material World(string name, Texture tex, Action<Material> tweak = null)
        {
            var shader = Shader.Find("JamKit/Doomlike/World");
            if (shader == null) throw new Exception("Shader JamKit/Doomlike/World not found (Templates/Doomlike/Shaders).");
            return Mat(name, shader, tex, tweak);
        }

        static Material Sprite(string name, Texture tex, Action<Material> tweak)
        {
            var shader = Shader.Find("JamKit/Doomlike/Sprite");
            if (shader == null) throw new Exception("Shader JamKit/Doomlike/Sprite not found (Templates/Doomlike/Shaders).");
            return Mat(name, shader, tex, tweak);
        }

        static Material Mat(string name, Shader shader, Texture tex, Action<Material> tweak)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (tex != null) mat.mainTexture = tex;
            tweak?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Preset ----------

        /// <summary>The GameConfig values "Use Template > DOOM-like" copies into the active config.
        /// Created once; edit it freely (title, tagline, How to Play pages).</summary>
        static void MakePreset(SceneBuilder.Context ctx, DoomlikeArt.Set art)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Corecrawler";
            p.tagline = "Three levels down. Find the keys. Find the exit.";
            p.firstGameplayScene = SceneName;
            p.titleMusic = "music_title";
            p.gameMusic = "music_game";
            p.showHowToPlayOnFirstRun = true;
            p.showPixelLookSetting = true;
            p.credits = ctx.config != null ? ctx.config.credits : null;
            p.audio = ctx.config != null ? ctx.config.audio : null;
            p.howToPlay = new List<HowToPlayPage>
            {
                new HowToPlayPage
                {
                    title = "Move and look",
                    body = "WASD or the left stick to move: you run all the time (hold Shift or LB to walk). Mouse or the right stick to look.\nClick the game first so it can hold the mouse.",
                    image = art.howMove,
                },
                new HowToPlayPage
                {
                    title = "Shoot",
                    body = "Left click or RT fires. Keys 1-5, the mouse wheel or Y swap guns.\nGuns that fire the same ammo share it: grab boxes to refill. Watch for the red aim line: get out of sight before it fires.",
                    image = art.howShoot,
                },
                new HowToPlayPage
                {
                    title = "Keys and doors",
                    body = "Doors open as you walk up. A coloured door needs the key card of its colour.\nOdd-looking walls can hide secrets: walk into them or press E / Space.",
                    image = art.howKeys,
                },
                new HowToPlayPage
                {
                    title = "Find the exit",
                    body = "Each level ends on the glowing EXIT pad. Between levels, pick 1 of 3 upgrades.\nFinish level 3 to win. Escape or Start pauses.",
                    image = art.howExit,
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        // ---------- Scene ----------

        static string BuildScene(DoomlikeConfig config)
        {
            var scene = SceneBuilder.NewScene(Color.black, out var cam);
            var reloaded = AssetDatabase.LoadAssetAtPath<DoomlikeConfig>(DoomlikeContent.ConfigPath);
            if (reloaded != null) config = reloaded;

            // The player: body (yaw) > head (pitch) > rig (bob) > camera (shake, pixel look).
            var playerGo = new GameObject("Player");
            var cc = playerGo.AddComponent<CharacterController>();
            var player = playerGo.AddComponent<DoomPlayer>();
            player.controller = cc;
            var head = new GameObject("Head").transform;
            head.SetParent(playerGo.transform, false);
            head.localPosition = new Vector3(0f, config.player.eyeHeight, 0f);
            var rig = new GameObject("Rig").transform;
            rig.SetParent(head, false);
            cam.transform.SetParent(rig, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.orthographic = false;
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 160f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowMSAA = false;
            var shaker = cam.gameObject.AddComponent<CameraShaker>();
            shaker.strength = 0.18f;
            var retro = cam.gameObject.AddComponent<RetroCamera>();
            retro.screenMaterial = config.screenMaterial;
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = false;
            player.head = head;
            player.rig = rig;
            player.cam = cam;

            var projectiles = new GameObject("Projectiles").AddComponent<ProjectileField>();
            var fx = new GameObject("Effects").AddComponent<FxField>();
            var levels = new GameObject("Levels").transform;

            var game = new GameObject("Doomlike Game").AddComponent<DoomGame>();
            game.config = config;
            game.hudStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (game.hudStyle == null) Debug.LogError("JamKit: " + StylePath + " is missing.");
            game.player = player;
            game.cam = cam;
            game.retro = retro;
            game.projectiles = projectiles;
            game.fx = fx;
            game.levelRoot = levels;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }
    }
}
