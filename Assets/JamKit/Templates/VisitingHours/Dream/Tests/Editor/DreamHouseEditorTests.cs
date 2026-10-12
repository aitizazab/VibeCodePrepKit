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
    /// <summary>Night 2's house in its scene (docs/redesign/house.md): the game is wired (the player with the stealth kit
    /// and only marked walls running, Theo's cut-out, every memory's thing, Lena and Lena at the tower's window, the
    /// corridor's door, the fold, the night sky, the umbrella, the way out onto Chapel Row); every solid part of the house
    /// has a collider she collides with and the long walls run; the geometry lint finds no overlap; the chapters and the
    /// attract script; the scene and preset exist and the scene is in Build Settings. Run "JamKit/Dream House/Rebuild
    /// Scene" if it's missing.</summary>
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
        }

        [Test]
        public void TheHouseHasNoOverlaps()
        {
            var h = DreamHouse.Build();
            var hits = HouseLint.Check(h.parts);
            var lines = new List<string>();
            foreach (var hit in hits) lines.Add(HouseLint.Describe(h.parts, hit));
            Assert.That(hits, Is.Empty, string.Join("\n", lines));
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
                int surfaces = 0, runnable = 0, cutouts = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (game == null) game = go.GetComponentInChildren<DreamHouseGame>(true);
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    foreach (var s in go.GetComponentsInChildren<MovementSurface>(true)) { surfaces++; if (s.runnable) runnable++; }
                    cutouts += go.GetComponentsInChildren<DreamCutoutView>(true).Length;
                }
                Assert.That(game, Is.Not.Null);
                Assert.That(game.player, Is.SameAs(body));
                Assert.That(body.GetComponent<StealthPlayer>(), Is.Not.Null, "Theo can find her");
                Assert.That(body.onlyMarkedWalls, Is.True, "the walls marked runnable run");
                Assert.That(game.houseRoot, Is.Not.Null);
                Assert.That(game.escapeRoot, Is.Not.Null);
                Assert.That(game.cameraFx, Is.Not.Null);

                Assert.That(game.theo, Is.Not.Null, "Theo");
                Assert.That(game.theo.figure, Is.Not.Null, "his cut-out");
                Assert.That(game.theo.GetComponentInChildren<DreamCutoutView>(true), Is.Not.Null, "a cut-out through People's view");
                Assert.That(game.theo.GetComponentsInChildren<Collider>(true), Is.Empty, "never in the way");
                Assert.That(game.lena, Is.Not.Null);
                Assert.That(game.lenaInWindow, Is.Not.Null);
                Assert.That(game.lenaInWindow.activeSelf, Is.False, "at the window only once she's out");
                Assert.That(cutouts, Is.GreaterThanOrEqualTo(3), "Lena, Lena at the window, Theo");

                Assert.That(game.memoryViews, Is.Not.Null);
                Assert.That(game.memoryViews.Length, Is.EqualTo(h.memories.Count), "every memory has its thing in the house");
                Assert.That(h.memories.Count, Is.GreaterThanOrEqualTo(5));
                var ids = new HashSet<string>();
                foreach (var v in game.memoryViews)
                {
                    Assert.That(v, Is.Not.Null);
                    Assert.That(h.Memory(v.id), Is.Not.Null, v.id);
                    Assert.That(ids.Add(v.id), Is.True, "once: " + v.id);
                    Assert.That(v.GetComponentsInChildren<Collider>(true), Is.Empty, v.id + " is never in the way");
                }

                Assert.That(game.corridorDoor, Is.Not.Null, "the corridor's door");
                Assert.That(game.corridorDoorLeaf, Is.Not.Null);
                Assert.That(game.theoAtTheDoor, Is.Not.Null);
                Assert.That(game.fold, Is.Not.Null);
                Assert.That(game.fold.gameObject.activeSelf, Is.False, "the house folds shut only in the escape");
                Assert.That(game.fold.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(game.nightSky, Is.Not.Null);
                Assert.That(game.escapeRoomGroups, Is.Not.Null.And.Not.Empty);
                Assert.That(game.exitDoor, Is.Not.Null, "where she comes out");
                Assert.That(game.nadiaChair, Is.Not.Null, "the fourth chair");

                Assert.That(game.umbrella, Is.Not.Null, "the umbrella by the last hall's door");
                Assert.That(game.umbrella.host, Is.SameAs(game));

                Assert.That(surfaces, Is.GreaterThan(200), "every solid part has its collider");
                Assert.That(runnable, Is.GreaterThan(30), "the long walls run");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EverySolidPartHasAColliderSheCollidesWith()
        {
            var h = DreamHouse.Build();
            int solid = 0;
            foreach (var p in h.parts) if (p.piece.solid) solid++;
            var scene = EditorSceneManager.OpenScene(DreamHouseBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform colliders = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    var game = go.GetComponentInChildren<DreamHouseGame>(true);
                    if (game != null && game.houseRoot != null) colliders = game.houseRoot.Find("Colliders");
                }
                Assert.That(colliders, Is.Not.Null, "the house's colliders");
                var boxes = colliders.GetComponentsInChildren<BoxCollider>(true);
                Assert.That(boxes.Length, Is.GreaterThanOrEqualTo(solid), "one box per solid part");
                MovementController body = null;
                foreach (var go in scene.GetRootGameObjects()) if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                foreach (var c in boxes)
                    Assert.That((body.worldMask.value & (1 << c.gameObject.layer)) != 0, Is.True, c.name + ": on a layer she collides with");
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
        public void TheRecorderReadsItsArguments()
        {
            Assert.That(DreamHouseGame.RecordArgs(new[] { "Unity.exe", "-houseRecord", "out/runs/x", "-houseRecordRun", "escape" }, out var dir, out var run), Is.True);
            Assert.That(dir, Is.EqualTo("out/runs/x"));
            Assert.That(run, Is.EqualTo("escape"));
            Assert.That(DreamHouseGame.RecordArgs(new[] { "Unity.exe", "-houseRecord", "out/runs/y" }, out _, out run), Is.True);
            Assert.That(run, Is.EqualTo("all"));
            Assert.That(DreamHouseGame.RecordArgs(new[] { "Unity.exe" }, out _, out _), Is.False);
        }

        /// <summary>The built player's flag (no editor window): -house-record [folder] [-house-record-run ...].</summary>
        [Test]
        public void ThePlayersRecordFlagReadsItsArguments()
        {
            Assert.That(HouseRecordFlag.Parse(new[] { "VisitingHours.exe", "-house-record", "C:/out/house-runs/x", "-house-record-run", "hideandseek" }, out var dir, out var run), Is.True);
            Assert.That(dir, Is.EqualTo("C:/out/house-runs/x"));
            Assert.That(run, Is.EqualTo("hideandseek"));
            Assert.That(HouseRecordFlag.Parse(new[] { "VisitingHours.exe", "-house-record", "-house-record-run", "nonsense" }, out dir, out run), Is.True);
            Assert.That(dir, Is.EqualTo("house-run"));
            Assert.That(run, Is.EqualTo("all"));
            Assert.That(HouseRecordFlag.Parse(new[] { "VisitingHours.exe", "-day-run" }, out _, out _), Is.False);
        }
    }
}
