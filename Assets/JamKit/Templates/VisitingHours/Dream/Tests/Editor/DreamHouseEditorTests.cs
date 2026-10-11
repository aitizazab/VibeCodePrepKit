using System.Collections.Generic;
using System.IO;
using JamKit.DreamEscape.EditorTools;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Night 2's house in its scene: the game is wired (the player with the stealth kit, Theo as a hide-and-seek
    /// seeker on the house's walking map, the hiding spots, the light, five memories under one counter, Lena, the hallway
    /// that stretches, the house folding shut, the umbrella at the front door); the chapters and the attract script; the
    /// route fits the house's real tuning and controller; the scene and preset exist and the scene is in Build Settings.
    /// Run "JamKit/Rebuild Shell Scenes" if the scene is missing.</summary>
    public class DreamHouseEditorTests
    {
        static List<string> Registered()
        {
            var r = new ChapterRegistry();
            DreamHousePresenter.Register(r, null, new object());
            var idx = new List<int>();
            r.InGroup(DreamHousePresenter.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            return names;
        }

        [Test]
        public void TheChaptersAndTheAttractScript()
        {
            var names = Registered();
            Assert.That(names, Is.EqualTo(DreamHousePresenter.Chapters));
            var h = DreamHouse.Build();
            foreach (var n in new[] { DreamHouse.ChapterArrival, DreamHouse.ChapterHideAndSeek, DreamHouse.ChapterTips, DreamHouse.ChapterFrontDoor })
                Assert.That(h.CheckpointIndex(n), Is.GreaterThanOrEqualTo(0), n + " is a checkpoint");
            var script = DreamHousePresenter.Attract();
            Assert.That(script.scene, Is.EqualTo(DreamHousePresenter.Scene));
            var problems = script.Validate(names, DreamHousePresenter.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(script.TotalSeconds, Is.InRange(30f, 120f));
        }

        [Test]
        public void TheSceneAndPresetExistAndTheSceneIsInBuildSettings()
        {
            Assert.That(File.Exists(DreamHouseBuilder.ScenePath), Is.True, DreamHouseBuilder.ScenePath);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameConfig>(DreamHouseBuilder.PresetPath), Is.Not.Null);
            bool found = false;
            foreach (var s in EditorBuildSettings.scenes) found |= s.path == DreamHouseBuilder.ScenePath && s.enabled;
            Assert.That(found, Is.True, "Rebuild Shell Scenes puts the house in Build Settings");
            foreach (var art in new[] { DreamArt.Lena, DreamArt.Theo })
            {
                string real = DreamArt.RealPath(art);
                Assert.That(real != null || File.Exists(DreamArt.GeneratedDir + "/" + art + ".png"), Is.True, art + ": real art or its placeholder");
            }
        }

        [Test]
        public void TheHouseIsWired()
        {
            var h = DreamHouse.Build();
            var scene = EditorSceneManager.OpenScene(DreamHouseBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                DreamHouseGame game = null;
                MovementController body = null;
                int spots = 0, zones = 0, items = 0;
                var names = new HashSet<string>();
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (game == null) game = go.GetComponentInChildren<DreamHouseGame>(true);
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    spots += go.GetComponentsInChildren<HidingSpotView>(true).Length;
                    zones += go.GetComponentsInChildren<StealthLightZone>(true).Length;
                    items += go.GetComponentsInChildren<Collectible>(true).Length;
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
                }
                Assert.That(game, Is.Not.Null);
                Assert.That(game.player, Is.SameAs(body));
                Assert.That(body.GetComponent<StealthPlayer>(), Is.Not.Null, "she can be found");
                Assert.That(body.onlyMarkedWalls, Is.True, "only the striped walls run");

                Assert.That(game.theo, Is.Not.Null, "Theo");
                Assert.That(game.theo.mode, Is.EqualTo(SeekerMode.HideAndSeek));
                Assert.That(game.theo.waypoints.childCount, Is.EqualTo(h.nodes.Count));
                Assert.That(game.theo.GetComponentsInChildren<Collider>(true), Is.Empty, "never in the way");
                Assert.That(game.theo.GetComponent<SeekerOverlay>(), Is.Not.Null, "his cone and meter");
                Assert.That(game.theo.GetComponentInChildren<DreamFaceCamera>(true), Is.Not.Null, "his cut-out");
                Assert.That(Vector3.Distance(game.theo.transform.position, new Vector3(h.theoStart.X, h.theoStart.Y, h.theoStart.Z)), Is.LessThan(0.01f), "at the table");

                Assert.That(game.memories, Is.Not.Null);
                Assert.That(items, Is.EqualTo(h.memories.Count), "five memories");
                Assert.That(game.memories.GetComponentsInChildren<Collectible>(true).Length, Is.EqualTo(h.memories.Count), "under the one counter");
                Assert.That(spots, Is.EqualTo(h.spots.Count));
                Assert.That(zones, Is.EqualTo(h.lights.Count));

                Assert.That(game.hallwayEnd, Is.Not.Null);
                Assert.That(game.hallwayEnd.position.z, Is.EqualTo(h.stretchFrom).Within(0.01f), "the hallway starts short");
                Assert.That(game.hallwayEnd.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(game.fold, Is.Not.Null);
                Assert.That(game.fold.gameObject.activeSelf, Is.False, "the house folds shut only in the escape");
                Assert.That(game.fold.GetComponentsInChildren<Collider>(true), Is.Empty);

                Assert.That(game.umbrella, Is.Not.Null, "the umbrella at the front door");
                Assert.That(game.umbrella.host, Is.SameAs(game));
                Assert.That(Vector3.Distance(game.umbrella.prop.position, new Vector3(h.rain.umbrella.X, h.rain.umbrella.Y, h.rain.umbrella.Z)), Is.LessThan(0.01f));
                Assert.That(names, Does.Contain("Lena (cut-out)"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void OnlyTheBattlesCondemnsStanceIsCondemns()
        {
            Assert.That(DreamHouseGame.OutcomeOf(JamKit.Argument.ArgumentStance.Condemns), Is.EqualTo(ArgumentOutcome.Condemns));
            Assert.That(DreamHouseGame.OutcomeOf(JamKit.Argument.ArgumentStance.Unsure), Is.EqualTo(ArgumentOutcome.Unsure));
            Assert.That(DreamHouseGame.OutcomeOf(JamKit.Argument.ArgumentStance.Amenable), Is.EqualTo(ArgumentOutcome.Unsure));
            Assert.That(DreamHouseGame.OutcomeOf(JamKit.Argument.ArgumentStance.None), Is.EqualTo(ArgumentOutcome.Unsure));
        }

        [Test]
        public void TheRouteFitsTheHousesTuningAndController()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(DreamEscapeBuilder.TuningPath);
            Assert.That(tuning, Is.Not.Null);
            var scene = EditorSceneManager.OpenScene(DreamHouseBuilder.ScenePath, OpenSceneMode.Additive);
            float skin, step;
            try
            {
                MovementController body = null;
                foreach (var go in scene.GetRootGameObjects()) if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                skin = body.skinWidth;
                step = body.crouchStepOffset;
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            var problems = DreamHouse.Build().CheckReach(tuning.def, skin, step);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
