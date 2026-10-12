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
    /// <summary>Larch Hill in its scene (docs/redesign/world.md): the game is wired (Nadia, Walt's collider, the cast, the
    /// loop, the memories, the shutter, the wave, the bedside's plank and lit hospital, the house with its window, door and
    /// porch light); every solid piece of the hill has a collider; every wall runs (onlyMarkedWalls off); nothing drawn is
    /// left on the flat fallback texture; the chapters; the scene and preset exist and the scene is in Build Settings. Run
    /// "JamKit/Rebuild Shell Scenes" (or DreamStreetBuilder.BuildFromCommandLine) if the scene is missing.</summary>
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
            foreach (var route in h.night1) Assert.That(names, Does.Contain(route.chapter), route.name + " has a chapter");
        }

        [Test]
        public void EveryMaterialIdHasItsTextures()
        {
            var missing = new List<string>();
            foreach (var s in HillMat.All)
            {
                bool found = false;
                foreach (var ext in new[] { ".png", ".jpg" })
                    if (File.Exists(HillMaterials.TexRoot + "/" + s.pack + "/" + s.pack + "_color" + ext)) found = true;
                if (!found) missing.Add(s.id + " (" + s.pack + ")");
            }
            Assert.That(missing, Is.Empty, "texture packs missing: " + string.Join(", ", missing));
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
                int colliders = 0, fallback = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (game == null) game = go.GetComponentInChildren<DreamStreetGame>(true);
                    colliders += go.GetComponentsInChildren<Collider>(true).Length;
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                        foreach (var m in r.sharedMaterials)
                        {
                            // Only materials that have a colour map: reading mainTexture on a shader without one (the house
                            // builder's JamKit/Dream/Glow, e.g. Glow_PorchLantern) logs an error, which fails the test.
                            var tex = m == null ? null : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                            if (tex != null && tex.name == "gen_noise_color") fallback++;
                        }
                }
                Assert.That(game, Is.Not.Null);
                Assert.That(game.player, Is.Not.Null);
                Assert.That(game.player.onlyMarkedWalls, Is.False, "every wall runs unless it says it doesn't");
                Assert.That(game.waltGate, Is.Not.Null, "Walt's collider on his wall");
                foreach (var t in new Object[] { game.walt, game.neighbour, game.dog, game.sprinklerJet, game.ball, game.paper, game.ledger, game.nail, game.treehouseMemory,
                    game.shutter, game.wave, game.creekPlank, game.hospitalLit, game.eliasOutside, game.eliasInside, game.theoInside, game.lenaInside, game.frontDoor,
                    game.porchLight, game.porchGlow, game.houseRoot })
                    Assert.That(t, Is.Not.Null);
                int solid = 0;
                foreach (var p in h.pieces) if (p.solid) solid++;
                Assert.That(colliders, Is.GreaterThanOrEqualTo(solid), "every solid piece of the hill has a collider");
                Assert.That(fallback, Is.EqualTo(0), "no surface is left on the noise texture that stands in for a missing pack");
                Assert.That(Vector3.Distance(game.houseRoot.position, new Vector3(h.houseOrigin.X, h.houseOrigin.Y, h.houseOrigin.Z)), Is.LessThan(0.01f), "at its anchor");
                Assert.That(Vector3.Distance(game.player.transform.position, new Vector3(h.arrival.X, h.arrival.Y, h.arrival.Z)), Is.LessThan(0.1f), "Nadia on the far bank's rise");
                Assert.That(game.wave.GetComponentInChildren<Collider>(true), Is.Null, "the wave is light, not a wall");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
