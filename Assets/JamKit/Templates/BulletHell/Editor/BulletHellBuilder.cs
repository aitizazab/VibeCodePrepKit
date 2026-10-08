using System;
using System.Collections.Generic;
using System.IO;
using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.BulletHell.EditorTools
{
    /// <summary>
    /// Builds the bullet-hell template: placeholder art, the bullet material, the default content
    /// (only what's missing), its GameConfig preset and the BulletHell scene. Runs as a step of
    /// "JamKit/Rebuild Shell Scenes" (batch: SceneBuilder.RebuildFromCommandLine), so the scene is
    /// always in Build Settings. Delete the Templates/BulletHell folder and it all disappears.
    /// </summary>
    public static class BulletHellBuilder
    {
        public const string Root = "Assets/JamKit/Templates/BulletHell";
        public const string SceneName = "BulletHell";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/BulletHellGameConfig.asset";
        public const string MaterialPath = Root + "/Art/BulletInstanced.mat";
        public const string RingMaterialPath = Root + "/Art/RingInstanced.mat";

        [JamKitBuildStep(200)]
        static void Build(SceneBuilder.Context ctx)
        {
            var scene = BuildEverything(ctx, false);
            if (string.IsNullOrEmpty(scene)) return;
            // The active template's scene goes right after Boot and Title.
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Bullet Hell/Reset Content to Defaults")]
        static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset bullet-hell content?",
                    "Every enemy, wave, room, boss and upgrade asset in Templates/BulletHell/Data goes back to the template's defaults. Your changes to them are lost.",
                    "Reset", "Cancel")) return;
            ResetContent();
        }

        /// <summary>Batch: <c>-executeMethod JamKit.BulletHell.EditorTools.BulletHellBuilder.ResetContentFromCommandLine</c>.</summary>
        public static void ResetContentFromCommandLine()
        {
            bool ok;
            try { ok = ResetContent(); }
            catch (Exception e) { Debug.LogError("JAMKIT_BULLETHELL_FAIL " + e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool ResetContent()
        {
            EnsureFolder(Root + "/Data");
            EnsureFolder(BulletHellArt.Dir);
            var art = BulletHellArt.MakeAll();
            var config = BulletHellContent.EnsureAll(art, true);
            AssetDatabase.SaveAssets();
            bool ok = BulletHellValidation.LogProblems(config);
            Debug.Log("JAMKIT_BULLETHELL content reset" + (ok ? "" : " (with problems, see above)"));
            return ok;
        }

        [MenuItem("JamKit/Bullet Hell/Validate Content")]
        static void ValidateMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<BulletHellConfig>(BulletHellContent.ConfigPath);
            if (BulletHellValidation.LogProblems(config)) Debug.Log("Bullet hell content: no problems found.");
        }

        /// <summary>Art, material, content (missing assets only, or all when <paramref name="resetContent"/>),
        /// preset and scene. Returns the scene path.</summary>
        public static string BuildEverything(SceneBuilder.Context ctx, bool resetContent)
        {
            foreach (var d in new[] { Root + "/Art/Generated", Root + "/Data", Root + "/Scenes" }) EnsureFolder(d);
            var art = BulletHellArt.MakeAll();
            var material = MakeMaterial(MaterialPath, art.bullet.texture, 0.85f);
            var ringMaterial = MakeMaterial(RingMaterialPath, art.ringFx.texture, 0f);
            var config = BulletHellContent.EnsureAll(art, resetContent);
            AssetDatabase.SaveAssets();
            MakePreset(ctx, art);
            string path = BuildScene(ctx, config, art, material, ringMaterial);
            // A new single scene unloads unused assets, which leaves earlier references "destroyed": reload.
            config = AssetDatabase.LoadAssetAtPath<BulletHellConfig>(BulletHellContent.ConfigPath);
            BulletHellValidation.LogProblems(config);
            Debug.Log("JAMKIT_BULLETHELL_OK " + path);
            return path;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material MakeMaterial(string path, Texture texture, float core)
        {
            var shader = Shader.Find("JamKit/BulletInstanced");
            if (shader == null) throw new Exception("Shader JamKit/BulletInstanced not found (Templates/BulletHell/Shaders).");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.mainTexture = texture;
            mat.SetFloat("_Core", core);
            mat.enableInstancing = true;
            mat.renderQueue = -1; // the shader's Transparent queue: sorts with the sprites by depth (z)
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The first How to Play card: firing is automatic, and Shift is a sprint (the creator's first
        /// playtest held Shift expecting a sprint, so Shift now is one). Tests check the preset says both.</summary>
        public const string FlyCardText =
            "Move with WASD, the arrow keys or the left stick.\nYou fire automatically.\n" +
            "Hold Shift (LB) to sprint: limited stamina per room (the SPRINT bar on the left).";

        /// <summary>The Bomb card: bombs are an epic now (docs/DESIGN-ROGUELITE.md, section 2).</summary>
        public const string BombCardText =
            "Bombs come from the BOMB BAY, an epic upgrade. Once you have it, K, right click or Space (A on a gamepad) " +
            "wipes every bullet and keeps you safe for a moment; the bay recharges while you fight.";

        public const string RunCardText =
            "Clear rooms and pick 1 of 3 upgrades. Bosses and trials offer epics. Between battles: shops, forges, altars, wagers and traders.\n" +
            "Beat the boss of each of the 3 floors to win. Escape or Start pauses.";

        /// <summary>The GameConfig values "Use Template > Bullet Hell" copies into the active config.
        /// Created once; edit it freely (title, tagline, How to Play pages).</summary>
        static void MakePreset(SceneBuilder.Context ctx, BulletHellArt.Set art)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Starfall Gauntlet";
            p.tagline = "Three floors. Three bosses. One tiny hitbox.";
            p.firstGameplayScene = SceneName;
            p.titleMusic = "music_title";
            p.gameMusic = "music_game";
            p.showHowToPlayOnFirstRun = true;
            p.credits = ctx.config != null ? ctx.config.credits : null;
            p.audio = ctx.config != null ? ctx.config.audio : null;
            p.howToPlay = new List<HowToPlayPage>
            {
                new HowToPlayPage
                {
                    title = "Fly",
                    body = FlyCardText,
                    image = art.howMove,
                },
                new HowToPlayPage
                {
                    title = "Dodge",
                    body = "Only the small dot in the middle of your ship can be hit.\nSlip past bullets to GRAZE them for points.",
                    image = art.howDodge,
                },
                new HowToPlayPage
                {
                    title = "Bomb",
                    body = BombCardText,
                    image = art.howBomb,
                },
                new HowToPlayPage
                {
                    title = "The run",
                    body = RunCardText,
                    image = art.howRun,
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        static string BuildScene(SceneBuilder.Context ctx, BulletHellConfig config, BulletHellArt.Set art, Material bulletMat, Material ringMat)
        {
            var scene = SceneBuilder.NewScene(config.sideColor, out var cam, 5.6f);
            var reloaded = AssetDatabase.LoadAssetAtPath<BulletHellConfig>(BulletHellContent.ConfigPath);
            if (reloaded != null) config = reloaded;
            cam.gameObject.AddComponent<CameraShaker>();
            var half = config.playfieldHalfSize;
            var floor = config.Floor(0);
            var flashMat = ctx.flashMaterial;

            // Playfield: a coloured box (behind everything) with a thin border.
            var world = new GameObject("Playfield").transform;
            var field = SceneBuilder.SpriteObject(ctx, "Background", ctx.square, floor != null ? floor.background : Color.black,
                new Vector3(0f, 0f, 10f), new Vector3(half.x * 2f, half.y * 2f, 1f), 0, world);
            var border = new Color(1f, 1f, 1f, 0.18f);
            SceneBuilder.SpriteObject(ctx, "Border Left", ctx.square, border, new Vector3(-half.x - 0.04f, 0f, 8f), new Vector3(0.08f, half.y * 2f + 0.16f, 1f), 0, world);
            SceneBuilder.SpriteObject(ctx, "Border Right", ctx.square, border, new Vector3(half.x + 0.04f, 0f, 8f), new Vector3(0.08f, half.y * 2f + 0.16f, 1f), 0, world);
            SceneBuilder.SpriteObject(ctx, "Border Top", ctx.square, border, new Vector3(0f, half.y + 0.04f, 8f), new Vector3(half.x * 2f + 0.16f, 0.08f, 1f), 0, world);
            SceneBuilder.SpriteObject(ctx, "Border Bottom", ctx.square, border, new Vector3(0f, -half.y - 0.04f, 8f), new Vector3(half.x * 2f + 0.16f, 0.08f, 1f), 0, world);
            // Side panels: cover anything that strays outside the playfield (sparks, entering enemies).
            float sideW = 12f;
            SceneBuilder.SpriteObject(ctx, "Side Left", ctx.square, config.sideColor, new Vector3(-half.x - 0.08f - sideW * 0.5f, 0f, -3f), new Vector3(sideW, 20f, 1f), 0, world);
            SceneBuilder.SpriteObject(ctx, "Side Right", ctx.square, config.sideColor, new Vector3(half.x + 0.08f + sideW * 0.5f, 0f, -3f), new Vector3(sideW, 20f, 1f), 0, world);
            SceneBuilder.SpriteObject(ctx, "Side Top", ctx.square, config.sideColor, new Vector3(0f, half.y + 0.08f + 2.5f, -3f), new Vector3(40f, 5f, 1f), 0, world);
            SceneBuilder.SpriteObject(ctx, "Side Bottom", ctx.square, config.sideColor, new Vector3(0f, -half.y - 0.08f - 2.5f, -3f), new Vector3(40f, 5f, 1f), 0, world);

            var bulletsGo = new GameObject("Bullets");
            var bullets = bulletsGo.AddComponent<BulletField>();
            bullets.material = bulletMat;

            var fxGo = new GameObject("Effects");
            var fx = fxGo.AddComponent<FxField>();
            fx.material = bulletMat;
            fx.ringMaterial = ringMat;

            var coinsGo = new GameObject("Coins");
            var coins = coinsGo.AddComponent<CoinField>();
            coins.material = bulletMat;
            coins.color = config.coinColor;

            // Player: body, focus aura, graze ring and the hitbox dot drawn above the bullets.
            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, -3.6f, 0f);
            var player = playerGo.AddComponent<ShmupPlayer>();
            var aura = SceneBuilder.SpriteObject(ctx, "Focus Aura", art.aura, new Color(0.55f, 0.95f, 1f, 0.35f), new Vector3(0f, -3.6f, 0.1f), new Vector3(1.8f, 1.8f, 1f), 0, playerGo.transform);
            var body = SceneBuilder.SpriteObject(ctx, "Ship", art.ship, config.player != null ? config.player.color : Color.cyan, new Vector3(0f, -3.6f, 0f), Vector3.one * 0.75f, 0, playerGo.transform);
            var ring = SceneBuilder.SpriteObject(ctx, "Graze Ring", art.ring, new Color(0.7f, 0.9f, 1f, 0.15f), new Vector3(0f, -3.6f, -0.1f), Vector3.one * 1.2f, 0, playerGo.transform);
            var hitbox = SceneBuilder.SpriteObject(ctx, "Hitbox", art.hitbox, Color.white, new Vector3(0f, -3.6f, -2.5f), Vector3.one * 0.18f, 0, playerGo.transform);
            player.body = body.GetComponent<SpriteRenderer>();
            player.flash = body.AddComponent<SpriteFlash>();
            player.hitboxDot = hitbox.GetComponent<SpriteRenderer>();
            player.grazeRing = ring.GetComponent<SpriteRenderer>();
            player.focusAura = aura.GetComponent<SpriteRenderer>();

            var enemies = new GameObject("Enemies").transform;
            enemies.position = new Vector3(0f, 0f, 1f);

            var bossGo = SceneBuilder.SpriteObject(ctx, "Boss", art.bossGate, Color.white, new Vector3(0f, 8f, 1.2f), Vector3.one * 2.4f, 0);
            var boss = bossGo.AddComponent<ShmupBoss>();
            boss.body = bossGo.GetComponent<SpriteRenderer>();
            boss.flash = bossGo.AddComponent<SpriteFlash>();
            var halo = SceneBuilder.SpriteObject(ctx, "Halo", art.ring, new Color(1f, 1f, 1f, 0.35f), new Vector3(0f, 8f, 1.25f), Vector3.one * 2.4f * 1.3f, 0, bossGo.transform);
            halo.transform.localScale = Vector3.one * 1.3f;
            boss.halo = halo.GetComponent<SpriteRenderer>();
            bossGo.SetActive(false);

            var game = new GameObject("Bullet Hell Game").AddComponent<BulletHellGame>();
            game.config = config;
            game.hudStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "/UI/BulletHell.uss");
            if (game.hudStyle == null) Debug.LogError("JamKit: " + Root + "/UI/BulletHell.uss is missing.");
            game.field = bullets;
            game.fx = fx;
            game.coins = coins;
            game.player = player;
            game.boss = boss;
            game.playfield = field.GetComponent<SpriteRenderer>();
            game.enemyRoot = enemies;
            game.spriteMaterial = flashMat;
            game.cam = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }
    }
}
