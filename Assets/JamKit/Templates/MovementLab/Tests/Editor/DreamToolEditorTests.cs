using System.Collections.Generic;
using JamKit.Movement;
using JamKit.MovementLab.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The dream tool chapter in the Movement Lab's scene: the tool and its run loop are wired to the lab, one
    /// view per tool piece (collider at its full size, a visual child) and per target, in place; the gate and finish
    /// arches stand; the materials compile; the chapter is a presenter chapter with a checkpoint; the route fits the lab's
    /// real tuning and controller with the built pieces in place; the generated sounds are there. Run "JamKit/Rebuild
    /// Shell Scenes" if the scene is missing.</summary>
    public class DreamToolEditorTests
    {
        [Test]
        public void TheSceneHasTheDreamToolWired()
        {
            var d = DreamToolCourse.Build();
            var scene = EditorSceneManager.OpenScene(MovementLabBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                DreamTool tool = null;
                DreamToolRun run = null;
                MovementController body = null;
                var names = new HashSet<string>();
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (tool == null) tool = go.GetComponentInChildren<DreamTool>(true);
                    if (run == null) run = go.GetComponentInChildren<DreamToolRun>(true);
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) names.Add(t.name);
                }
                Assert.That(tool, Is.Not.Null, "the tool");
                Assert.That(run, Is.Not.Null, "the run loop");
                Assert.That(tool.game, Is.Not.Null);
                Assert.That(tool.game.player, Is.SameAs(body));
                Assert.That(tool.run, Is.SameAs(run));
                Assert.That(run.tool, Is.SameAs(tool));
                Assert.That(run.game, Is.SameAs(tool.game));
                Assert.That(tool.beam, Is.Not.Null);
                Assert.That(tool.pulse, Is.Not.Null);
                Assert.That(tool.beam.GetComponentsInChildren<Collider>(true), Is.Empty, "the beam is never in the way");
                Assert.That(tool.body, Is.Not.Null, "the pulse starts at her hand");
                Assert.That(tool.autoFire, Is.True);
                Assert.That(tool.def.coneMouse, Is.InRange(5f, 9f));
                Assert.That(tool.def.conePad, Is.GreaterThan(tool.def.coneMouse), "stronger on a pad");
                Assert.That(tool.def.cooldown, Is.InRange(0.15f, 0.35f));

                Assert.That(run.pieceViews.Length, Is.EqualTo(d.pieces.Count), "one view per tool piece");
                for (int i = 0; i < run.pieceViews.Length; i++)
                {
                    var v = run.pieceViews[i];
                    var p = d.pieces[i];
                    Assert.That(v, Is.Not.Null, p.name);
                    Assert.That(v.index, Is.EqualTo(i));
                    Assert.That(v.role, Is.EqualTo(p.role), p.name);
                    Assert.That(v.solidCollider, Is.Not.Null, p.name);
                    var box = (BoxCollider)v.solidCollider;
                    Assert.That(Vector3.Distance(Vector3.Scale(box.size, v.transform.lossyScale), new Vector3(p.Size.X, p.Size.Y, p.Size.Z)), Is.LessThan(0.01f), p.name + " collider at full size");
                    Assert.That(Vector3.Distance(v.transform.position, new Vector3(p.Center.X, p.Center.Y, p.Center.Z)), Is.LessThan(0.01f), p.name + " in place");
                    Assert.That(v.visual, Is.Not.Null, p.name);
                    Assert.That(v.visual.GetComponentsInChildren<Collider>(true), Is.Empty, p.name + ": only the piece itself collides");
                    Assert.That(v.renderers, Is.Not.Empty, p.name);
                    Assert.That(v.GetComponent<MovementSurface>().runnable, Is.EqualTo(p.runnable), p.name);
                    if (p.role == DreamPieceRole.Pink || p.role == DreamPieceRole.Blue)
                    {
                        Assert.That(v.solidMaterial, Is.Not.Null, p.name);
                        Assert.That(v.ghostMaterial, Is.Not.Null, p.name);
                    }
                }
                Assert.That(run.targetViews.Length, Is.EqualTo(d.targets.Count), "one view per target");
                for (int i = 0; i < run.targetViews.Length; i++)
                {
                    var v = run.targetViews[i];
                    var t = d.targets[i];
                    Assert.That(v, Is.Not.Null, t.name);
                    Assert.That(v.index, Is.EqualTo(i));
                    Assert.That(v.kind, Is.EqualTo(t.kind), t.name);
                    Assert.That(v.GetComponentsInChildren<Collider>(true), Is.Empty, t.name + ": targets don't block the aim or the run");
                    Assert.That(Vector3.Distance(v.transform.position, new Vector3(t.center.X, t.center.Y, t.center.Z)), Is.LessThan(0.01f), t.name);
                    Assert.That(v.lockFrame, Is.Not.Null, t.name + " shows when locked");
                    if (t.kind == DreamTargetKind.Switch) Assert.That(v.switchMaterials.Length, Is.EqualTo(2), t.name);
                    if (t.kind != DreamTargetKind.Tile) Assert.That(v.visual, Is.Not.Null, t.name);
                }
                Assert.That(names, Does.Contain("Dream tool gate (line)"));
                Assert.That(names, Does.Contain("Dream tool finish (line)"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheDreamToolMaterialsCompile()
        {
            foreach (var name in new[] { "Dream_Glass_Lilac", "Dream_Glass_Mint", "Dream_Glass_Peach", "Grid_DreamBuilt", "Grid_DreamPink", "Grid_DreamBlue",
                "Dream_PinkGhost", "Dream_BlueGhost", "Grid_DreamSocket", "Grid_DreamSwitchPink", "Grid_DreamSwitchBlue", "Dream_Halo", "Dream_Beam", "Dream_Pulse" })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MovementLabBuilder.Root + "/Materials/" + name + ".mat");
                Assert.That(mat, Is.Not.Null, name);
                Assert.That(mat.shader.isSupported, Is.True, name + " shader " + mat.shader.name);
                Assert.That(mat.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), name);
            }
        }

        [Test]
        public void TheChapterIsAPresenterChapterWithACheckpointAndAnAttractLeg()
        {
            Assert.That(MovementLabPresenter.Chapters, Does.Contain(DreamToolCourse.Chapter));
            var d = DreamToolCourse.Build();
            Assert.That(d.course.CheckpointIndex(DreamToolCourse.Chapter), Is.EqualTo(d.startCheckpoint));
            Assert.That(d.ColourAt(d.startCheckpoint), Is.EqualTo(DreamToolCourse.Pink));
            var script = MovementLabPresenter.Attract();
            Assert.That(script.TotalSeconds, Is.InRange(30f, 180f));
        }

        [Test]
        public void TheRouteFitsTheLabsTuningAndControllerWithTheBuildsInPlace()
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
            var problems = DreamToolCourse.Build().CheckReach(tuning.def, skin, step);
            Assert.That(problems, Is.Empty, "the lab's tuning or controller was changed beyond the dream tool chapter's sizes:\n" + string.Join("\n", problems));
        }

        [Test]
        public void TheToolsSoundsAreMadeAndClean()
        {
            foreach (var id in DreamToolSounds.Ids)
            {
                var data = DreamToolSounds.Make(id);
                Assert.That(data, Is.Not.Null.And.Not.Empty, id);
                var clip = AudioClip.Create("test_" + id, data.Length, 1, DreamToolSounds.Rate, false);
                Assert.That(clip.SetData(data, 0), Is.True, id);
                Assert.That(clip.length, Is.GreaterThan(0.05f), id);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
