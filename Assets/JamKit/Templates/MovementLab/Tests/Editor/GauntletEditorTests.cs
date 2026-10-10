using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Movement;
using JamKit.MovementLab.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The Gauntlet in the Movement Lab's scene: the run loop is wired (the lab, the ghost, one view per
    /// marker), the start gate and finish arch stand at the course's gate and finish, every runnable wall has its
    /// gold stripe, the materials compile, the route fits the reach of the lab's real tuning and controller, and
    /// the best record (time, splits, ghost) round-trips through the shell's save. Run "JamKit/Rebuild Shell Scenes"
    /// if the scene is missing.</summary>
    public class GauntletEditorTests
    {
        [Test]
        public void TheSceneHasTheGauntletWired()
        {
            var g = GauntletCourse.Build();
            var scene = EditorSceneManager.OpenScene(MovementLabBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                GauntletRun run = null;
                MovementController body = null;
                var names = new HashSet<string>();
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (run == null) run = go.GetComponentInChildren<GauntletRun>(true);
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
                }
                Assert.That(run, Is.Not.Null, "the run loop");
                Assert.That(run.game, Is.Not.Null);
                Assert.That(run.game.player, Is.SameAs(body));
                Assert.That(run.ghost, Is.Not.Null, "the ghost");
                Assert.That(run.ghost.GetComponentsInChildren<Collider>(true), Is.Empty, "the ghost is never in the way");
                Assert.That(run.ghost.GetComponentInChildren<MeshRenderer>(true).sharedMaterial.shader.name, Is.EqualTo("JamKit/MovementLab/Ghost"));
                Assert.That(run.markerViews.Length, Is.EqualTo(g.markers.Count), "one plate per touch marker");
                for (int i = 0; i < run.markerViews.Length; i++)
                {
                    var v = run.markerViews[i];
                    Assert.That(v, Is.Not.Null, "marker " + i);
                    Assert.That(v.index, Is.EqualTo(i));
                    Assert.That(v.plate, Is.Not.Null);
                    Assert.That(v.GetComponent<Collider>(), Is.Null, "a marker is walked over, not into");
                    Assert.That(v.transform.position.z, Is.EqualTo(g.markers[i].S).Within(0.01f));
                }
                Assert.That(names, Does.Contain("Start gate (line)"));
                Assert.That(names, Does.Contain("Finish line (line)"));
                int runnable = 0, stripes = 0;
                for (int i = g.firstPiece; i < g.course.pieces.Count; i++) if (g.course.pieces[i].runnable) runnable++;
                foreach (var n in names) if (n.StartsWith("G ") && n.EndsWith(" stripe")) stripes++;
                Assert.That(stripes, Is.EqualTo(runnable), "a gold stripe on every runnable wall of the Gauntlet");
                var start = g.course.checkpoints[g.startCheckpoint].position;
                Assert.That(Vector3.Distance(body.transform.position, new Vector3(start.X, start.Y, start.Z)), Is.LessThan(0.5f), "the lab opens on the start pad");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheGauntletMaterialsCompile()
        {
            foreach (var name in new[] { "Grid_Gold", "Grid_Touch", "Gauntlet_Ghost" })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MovementLabBuilder.Root + "/Materials/" + name + ".mat");
                Assert.That(mat, Is.Not.Null, name);
                Assert.That(mat.shader.isSupported, Is.True, name + " shader " + mat.shader.name);
                Assert.That(mat.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), name);
            }
        }

        [Test]
        public void TheRouteFitsTheLabsTuningAndController()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementLabBuilder.TuningPath);
            Assert.That(tuning, Is.Not.Null);
            var scene = EditorSceneManager.OpenScene(MovementLabBuilder.ScenePath, OpenSceneMode.Additive);
            float skin, step;
            try
            {
                MovementController body = null;
                foreach (var go in scene.GetRootGameObjects()) if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                Assert.That(body, Is.Not.Null);
                skin = body.skinWidth;
                step = body.crouchStepOffset;
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            var problems = GauntletCourse.Build().CheckReach(tuning.def, skin, step);
            Assert.That(problems, Is.Empty, "the lab's tuning or controller was changed beyond the Gauntlet's sizes:\n" + string.Join("\n", problems));
        }

        [Test]
        public void TheBestRecordRoundTripsThroughTheSave()
        {
            var rec = new GauntletRecord { best = 41.25f, bestMissed = 1, runs = 7, ghostRate = 20f };
            rec.bestSplits.AddRange(new[] { 3.1f, 7.2f, 11.4f });
            for (int i = 0; i < 400; i++) rec.ghost.Add(i * 0.25f);
            var save = new SaveData();
            save.SetEntry(GauntletClock.SaveKey, GauntletRun.ToJson(rec));
            var json = JsonUtility.ToJson(save);
            var loaded = JsonUtility.FromJson<SaveData>(json);
            var back = GauntletRun.FromJson(loaded.GetEntry(GauntletClock.SaveKey));
            Assert.That(back.best, Is.EqualTo(41.25f));
            Assert.That(back.bestMissed, Is.EqualTo(1));
            Assert.That(back.runs, Is.EqualTo(7));
            Assert.That(back.bestSplits, Is.EqualTo(rec.bestSplits));
            Assert.That(back.ghost.Count, Is.EqualTo(400));
            Assert.That(back.ghost[399], Is.EqualTo(99.75f));
            var track = new GhostTrack();
            track.LoadFrom(back.ghost, back.ghostRate);
            Assert.That(track.Count, Is.EqualTo(100), "x, y, z, yaw per sample");
        }

        [Test]
        public void TheDevelopersRecordsShipWithTheLab()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DeveloperRecordsAsset.AssetPath);
            Assert.That(asset, Is.Not.Null, DeveloperRecordsAsset.AssetPath);
            var gauntlet = DeveloperRecordsAsset.Gauntlet(asset);
            Assert.That(gauntlet, Is.Not.Null);
            Assert.That(gauntlet.best, Is.EqualTo(31.42f).Within(0.01f), "the creator's best: 29.42 on the clock + 2 s for one missed plate");
            Assert.That(gauntlet.bestMissed, Is.EqualTo(1));
            Assert.That(gauntlet.bestSplits.Count, Is.EqualTo(GauntletCourse.Build().sections.Count));
            Assert.That(gauntlet.ghost.Count % 4, Is.EqualTo(0));
            var ghost = new GhostTrack();
            ghost.LoadFrom(gauntlet.ghost, gauntlet.ghostRate);
            Assert.That(ghost.Count, Is.EqualTo(589), "the creator's run at 20 Hz");
            Assert.That(ghost.Duration, Is.EqualTo(29.4f).Within(0.1f), "it covers the run on the clock");
            Assert.That(ghost.Sample(0f, out var p0, out _), Is.True);
            Assert.That(p0.Z, Is.EqualTo(GauntletCourse.Origin).Within(1f), "it starts at the gate");
            var dream = DeveloperRecordsAsset.DreamTool(asset);
            Assert.That(dream, Is.Not.Null);
            Assert.That(dream.best, Is.EqualTo(28.17f).Within(0.01f));
            Assert.That(DeveloperRecords.MagsTimeFor(gauntlet.best), Is.EqualTo(47.5f), "Mags: 1.5x, to the nearest 2.5 s");
            Assert.That(DeveloperRecords.MagsTimeFor(dream.best), Is.EqualTo(42.5f));
        }

        [Test]
        public void BothRunsGetTheRecordsAndAGhost()
        {
            var scene = EditorSceneManager.OpenScene(MovementLabBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                GauntletRun run = null;
                DreamToolRun dream = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (run == null) run = go.GetComponentInChildren<GauntletRun>(true);
                    if (dream == null) dream = go.GetComponentInChildren<DreamToolRun>(true);
                }
                Assert.That(run, Is.Not.Null);
                Assert.That(run.developerRecords, Is.Not.Null, "the Gauntlet reads the developer's records");
                Assert.That(dream, Is.Not.Null);
                Assert.That(dream.developerRecords, Is.Not.Null, "so does the dream tool chapter");
                Assert.That(dream.ghost, Is.Not.Null, "the dream tool chapter has its own ghost figure");
                Assert.That(dream.ghost, Is.Not.SameAs(run.ghost));
                Assert.That(dream.ghost.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
