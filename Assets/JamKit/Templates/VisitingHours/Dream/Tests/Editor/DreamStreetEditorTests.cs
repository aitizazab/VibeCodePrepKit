using System.Collections.Generic;
using System.IO;
using JamKit.DeepBlock;
using JamKit.DreamEscape.EditorTools;
using JamKit.Movement;
using JamKit.Presenting;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Night 1's street in its scene (docs/LARCH-HILL.md): the game is wired (Nadia, Walt's gate with its collider and
    /// no renderer, Walt, the neighbours' loop, the two memories, the house with its window, door and porch light, the
    /// beacon); every solid box of the hill has a collider; the house's outside comes from the house builder and its far
    /// lawns are gone; the chapters; the scene and preset exist and the scene is in Build Settings. Run "JamKit/Rebuild
    /// Shell Scenes" if the scene is missing.</summary>
    public class DreamStreetEditorTests
    {
        [Test]
        public void TheChapters()
        {
            var r = new ChapterRegistry();
            DreamStreetPresenter.Register(r, null, new object());
            var idx = new List<int>();
            r.InGroup(DreamStreetPresenter.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            Assert.That(names, Is.EqualTo(DreamStreetPresenter.Chapters));
            var h = LarchHill.Build();
            foreach (var route in h.routes) Assert.That(names, Does.Contain(route.chapter), route.name + " has a chapter");
        }

        [Test]
        public void TheSceneIsWiredFromTheHill()
        {
            Assert.That(File.Exists(DreamStreetBuilder.ScenePath), Is.True, "built (Rebuild Shell Scenes)");
            Assert.That(AssetDatabase.LoadAssetAtPath<Shell.GameConfig>(DreamStreetBuilder.PresetPath), Is.Not.Null, "the preset");
            bool listed = false;
            foreach (var s in EditorBuildSettings.scenes) if (s.path == DreamStreetBuilder.ScenePath && s.enabled) listed = true;
            Assert.That(listed, Is.True, "in Build Settings (the chain looks for DreamStreet)");

            var h = LarchHill.Build();
            var scene = EditorSceneManager.OpenScene(DreamStreetBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                DreamStreetGame game = null;
                int colliders = 0, lawns = 0, exterior = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (game == null) game = go.GetComponentInChildren<DreamStreetGame>(true);
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("Lawn (beyond")) lawns++;
                        if (t.name == "The house (outside)") exterior++;
                    }
                    colliders += go.GetComponentsInChildren<BoxCollider>(true).Length;
                }
                Assert.That(game, Is.Not.Null);
                Assert.That(game.player, Is.Not.Null);
                Assert.That(game.player.GetComponent<CharacterController>(), Is.Not.Null);
                Assert.That(game.waltGate, Is.Not.Null, "Walt's gate");
                Assert.That(game.waltGate.GetComponent<Collider>(), Is.Not.Null, "it shuts the way up");
                Assert.That(game.waltGate.GetComponent<MeshRenderer>(), Is.Null, "its leaf is what's drawn");
                foreach (var t in new Object[] { game.walt, game.gateLeaf, game.neighbour, game.dog, game.sprinklerJet, game.ball, game.paper, game.ledger,
                    game.eliasOutside, game.eliasInside, game.theoInside, game.lenaInside, game.frontDoor, game.porchLight, game.porchGlow, game.beacon, game.houseRoot })
                    Assert.That(t, Is.Not.Null);
                int solid = 0;
                foreach (var p in h.pieces) if (p.solid) solid++;
                Assert.That(colliders, Is.GreaterThanOrEqualTo(solid), "every solid box of the hill has a collider");
                Assert.That(exterior, Is.EqualTo(1), "the house's outside, from the house builder");
                Assert.That(lawns, Is.EqualTo(0), "its far lawns removed (they'd float over the hill)");
                Assert.That(Vector3.Distance(game.houseRoot.position, new Vector3(h.houseOrigin.X, h.houseOrigin.Y, h.houseOrigin.Z)), Is.LessThan(0.01f), "on the knoll");
                Assert.That(game.beacon.GetComponent<Collider>(), Is.Null, "the beacon is light, not a wall");
                Assert.That(Vector3.Distance(game.player.transform.position, new Vector3(h.arrival.X, h.arrival.Y, h.arrival.Z)), Is.LessThan(0.1f), "Nadia on the far bank");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
