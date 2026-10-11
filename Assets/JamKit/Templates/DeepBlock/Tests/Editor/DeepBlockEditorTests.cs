using JamKit.DeepBlock;
using JamKit.DeepBlockLab.EditorTools;
using JamKit.Movement;
using JamKit.Stealth;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Block = JamKit.DeepBlock.DeepBlock;

namespace JamKit.DeepBlockLab.Tests
{
    /// <summary>The Deep Block lab's scene is wired from core <see cref="Block"/> (run "JamKit/Rebuild Shell Scenes" if it's
    /// missing): a game object per piece (solid ones with colliders, roofs as prisms with colliders), every switch prop
    /// under "Switches" with its switch, gold on usable pieces, the hide-and-seek zone (Theo with his walking points and
    /// trampoline lookout, eight hiding places, six memories), the player, and the lab in Build Settings.</summary>
    public class DeepBlockEditorTests
    {
        [Test]
        public void TheSceneIsWiredFromTheBlock()
        {
            var block = Block.Build();
            var scene = EditorSceneManager.OpenScene(DeepBlockBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                DeepBlockGame game = null;
                Transform root = null;
                int switches = 0, spots = 0, memories = 0, roofs = 0, gold = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (game == null) game = go.GetComponentInChildren<DeepBlockGame>(true);
                    if (go.name == "The deep block") root = go.transform;
                    switches += go.GetComponentsInChildren<BlockSwitchProp>(true).Length;
                    spots += go.GetComponentsInChildren<HidingSpotView>(true).Length;
                    memories += go.GetComponentsInChildren<Collectible>(true).Length;
                    foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true)) if (mc.name.EndsWith(" roof")) roofs++;
                    if (go.name == "Gold (usable)") gold = go.transform.childCount;
                }
                Assert.That(game, Is.Not.Null);
                Assert.That(game.player, Is.Not.Null);
                Assert.That(game.player.GetComponent<StealthPlayer>(), Is.Not.Null);
                Assert.That(game.player.tuning, Is.Not.Null);
                Assert.That(game.theo, Is.Not.Null);
                Assert.That(game.theo.lookout, Is.Not.Null, "the trampoline");
                Assert.That(game.theo.waypoints.childCount, Is.EqualTo(block.theoNodes.Count));
                Assert.That(game.theo.tuning.def.verticalHalfAngle, Is.EqualTo(15f).Within(0.01f), "Theo doesn't look up");
                Assert.That(game.theo.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(game.memories, Is.Not.Null);
                Assert.That(game.switchRoot, Is.Not.Null);
                int switchPieces = 0;
                foreach (var p in block.pieces) if (p.switchIndex >= 0) switchPieces++;
                Assert.That(switches, Is.EqualTo(switchPieces));
                Assert.That(spots, Is.EqualTo(8));
                Assert.That(memories, Is.EqualTo(6));
                Assert.That(roofs, Is.EqualTo(5), "five houses, five pitched roofs");
                Assert.That(gold, Is.GreaterThan(40), "gold on what you can use");
                Assert.That(root, Is.Not.Null);
                foreach (var p in block.pieces)
                {
                    if (p.kind == BlockKind.Roof || p.switchIndex >= 0) continue;
                    var t = root.Find(p.name);
                    Assert.That(t, Is.Not.Null, p.name);
                    Assert.That(t.GetComponent<Collider>() != null, Is.EqualTo(p.solid), p.name);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheLabIsInBuildSettings()
        {
            bool listed = false;
            foreach (var s in EditorBuildSettings.scenes) if (s.path == DeepBlockBuilder.ScenePath) listed = true;
            Assert.That(listed, Is.True, "Rebuild Shell Scenes adds the lab to Build Settings");
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(DeepBlockBuilder.PrismPath), Is.Not.Null);
        }
    }
}
