using Ink.UnityIntegration;
using JamKit.EditorTools;
using JamKit.Shell;
using UnityEditor;
using UnityEngine;

namespace JamKit.Sandbox.EditorTools
{
    /// <summary>Builds Assets/JamKit/Scenes/Sandbox.unity as part of "JamKit/Rebuild Shell Scenes".
    /// Delete the Sandbox folder and this step (and the scene) simply disappear.</summary>
    public static class SandboxSceneBuilder
    {
        [JamKitBuildStep(100)]
        static void Build(SceneBuilder.Context ctx)
        {
            var scene = SceneBuilder.NewScene(new Color(0.06f, 0.07f, 0.12f), out var cam);
            cam.gameObject.AddComponent<CameraShaker>();

            var world = new GameObject("World").transform;
            SceneBuilder.SpriteObject(ctx, "Floor", ctx.square, new Color(0.13f, 0.15f, 0.24f), new Vector3(0f, 0f, 1f), new Vector3(18.4f, 10.4f, 1f), -10, world);
            var wall = new Color(0.32f, 0.36f, 0.55f);
            SceneBuilder.SpriteObject(ctx, "Wall Top", ctx.square, wall, new Vector3(0f, 5.25f, 0f), new Vector3(18.6f, 0.2f, 1f), -5, world);
            SceneBuilder.SpriteObject(ctx, "Wall Bottom", ctx.square, wall, new Vector3(0f, -5.25f, 0f), new Vector3(18.6f, 0.2f, 1f), -5, world);
            SceneBuilder.SpriteObject(ctx, "Wall Left", ctx.square, wall, new Vector3(-9.25f, 0f, 0f), new Vector3(0.2f, 10.6f, 1f), -5, world);
            SceneBuilder.SpriteObject(ctx, "Wall Right", ctx.square, wall, new Vector3(9.25f, 0f, 0f), new Vector3(0.2f, 10.6f, 1f), -5, world);

            // The sign: a board on a post, near the start.
            var sign = SceneBuilder.SpriteObject(ctx, "Sign", ctx.roundedSquare, new Color(0.72f, 0.52f, 0.3f), new Vector3(-6.5f, 2.6f, 0f), new Vector3(1.1f, 0.7f, 1f), 0, world);
            SceneBuilder.SpriteObject(ctx, "Sign Post", ctx.square, new Color(0.45f, 0.32f, 0.2f), new Vector3(-6.5f, 2.0f, 0.1f), new Vector3(0.15f, 0.6f, 1f), -1, world);

            var player = SceneBuilder.SpriteObject(ctx, "Player", ctx.roundedSquare, new Color(0.45f, 0.85f, 1f), new Vector3(-7f, -0.5f, 0f), new Vector3(0.8f, 0.8f, 1f), 5);
            player.AddComponent<SpriteFlash>();
            var dmg = player.AddComponent<Damageable>();
            dmg.maxHealth = 3f;
            dmg.invulnerableSeconds = 1.2f;
            dmg.isPlayer = true;
            var sp = player.AddComponent<SandboxPlayer>();

            var coinColor = new Color(1f, 0.83f, 0.25f);
            var coins = new GameObject("Coins").transform;
            foreach (var pos in new[] { new Vector3(0.5f, 3f, 0f), new Vector3(4.5f, -3f, 0f), new Vector3(7.2f, 2.4f, 0f) })
            {
                var coin = SceneBuilder.SpriteObject(ctx, "Coin", ctx.circle, coinColor, pos, new Vector3(0.55f, 0.55f, 1f), 2, coins);
                coin.AddComponent<SandboxCoin>();
            }

            var hazard = SceneBuilder.SpriteObject(ctx, "Hazard", ctx.square, new Color(1f, 0.28f, 0.3f), new Vector3(-2f, 0f, 0f), new Vector3(1f, 1f, 1f), 3);
            var hz = hazard.AddComponent<SandboxHazard>();
            hz.pointA = new Vector2(-2f, 3.6f);
            hz.pointB = new Vector2(-2f, -3.6f);

            var game = new GameObject("Sandbox Game").AddComponent<SandboxGame>();
            game.player = sp;
            game.sign = sign.transform;
            game.story = AssetDatabase.LoadAssetAtPath<InkFile>(SceneBuilder.Root + "/Dialogue/Sandbox.ink");
            if (game.story == null) Debug.LogError("JamKit: Sandbox.ink not found or not compiled.");

            ctx.scenes.Add(SceneBuilder.Save(scene, "Sandbox"));
        }
    }
}
