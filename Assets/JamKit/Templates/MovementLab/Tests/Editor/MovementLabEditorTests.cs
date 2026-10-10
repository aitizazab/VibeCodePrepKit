using System.Collections.Generic;
using System.IO;
using JamKit.Movement;
using JamKit.MovementLab.EditorTools;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The Movement Lab's presenter chapters and attract script, its built scene and assets.
    /// Run "JamKit/Rebuild Shell Scenes" if the assets are missing.</summary>
    public class MovementLabEditorTests
    {
        static List<string> Registered()
        {
            var r = new ChapterRegistry();
            MovementLabPresenter.Register(r, null, new object());
            var idx = new List<int>();
            r.InGroup(MovementLabPresenter.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            return names;
        }

        [Test]
        public void EveryDeclaredChapterIsRegisteredInOrderAndIsACheckpoint()
        {
            var names = Registered();
            Assert.That(names, Is.EqualTo(MovementLabPresenter.Chapters));
            foreach (var required in new[] { LabCourse.ChapterStart, LabCourse.ChapterWallruns, LabCourse.ChapterChase })
                Assert.That(names, Does.Contain(required));
            var course = LabCourse.Build();
            foreach (var n in names) Assert.That(course.CheckpointIndex(n), Is.GreaterThanOrEqualTo(0), n + " has a checkpoint");
        }

        [Test]
        public void TheAttractScriptPlaysOnlyRealChaptersAndSegments()
        {
            var script = MovementLabPresenter.Attract();
            Assert.That(script.scene, Is.EqualTo(MovementLabPresenter.Scene));
            var problems = script.Validate(Registered(), MovementLabPresenter.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(script.TotalSeconds, Is.InRange(30f, 180f));
        }

        [Test]
        public void TheSceneTuningPresetAndMaterialsExist()
        {
            Assert.That(File.Exists(MovementLabBuilder.ScenePath), Is.True, MovementLabBuilder.ScenePath);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(MovementLabBuilder.ScenePath), Is.Not.Null);
            Assert.That(MovementLabBuilder.ScenePath, Does.EndWith(MovementLabPresenter.Scene + ".unity"));
            var tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementLabBuilder.TuningPath);
            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.def.tickRate, Is.GreaterThanOrEqualTo(60f));
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(MovementLabBuilder.PresetPath);
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.firstGameplayScene, Is.EqualTo(MovementLabBuilder.SceneName));
            foreach (var name in new[] { "Grid_Floor", "Grid_RunWall", "SpeedLines", "DreamStatic", "Body_Sleeve" })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MovementLabBuilder.Root + "/Materials/" + name + ".mat");
                Assert.That(mat, Is.Not.Null, name);
                Assert.That(mat.shader.isSupported, Is.True, name + " shader " + mat.shader.name);
                Assert.That(mat.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), name);
            }
        }

        [Test]
        public void TheLabSceneIsInBuildSettings()
        {
            bool found = false;
            foreach (var s in EditorBuildSettings.scenes) found |= s.path == MovementLabBuilder.ScenePath && s.enabled;
            Assert.That(found, Is.True, "Rebuild Shell Scenes puts the lab in Build Settings");
        }

        [Test]
        public void ThePlayerHasEveryMovementPart()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MovementLabBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                MovementController body = null;
                MovementLabGame game = null;
                int pieces = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    if (game == null) game = go.GetComponentInChildren<MovementLabGame>(true);
                    pieces += go.GetComponentsInChildren<LabPieceView>(true).Length;
                }
                Assert.That(body, Is.Not.Null);
                Assert.That(body.head, Is.Not.Null);
                Assert.That(body.cam, Is.Not.Null);
                Assert.That(body.tuning, Is.Not.Null);
                Assert.That(body.GetComponentInChildren<MovementCamera>(true), Is.Not.Null);
                Assert.That(body.GetComponentInChildren<MovementBody>(true), Is.Not.Null);
                Assert.That(body.GetComponentInChildren<SpeedLines>(true).material, Is.Not.Null);
                Assert.That(body.GetComponent<MovementAudio>(), Is.Not.Null);
                Assert.That(game, Is.Not.Null);
                Assert.That(game.pursuerView, Is.Not.Null);
                Assert.That(pieces, Is.EqualTo(LabCourse.Build().pieces.Count), "one view per course piece");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SprintIsOptInWithShiftLbAndAnL3Toggle()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementLabBuilder.TuningPath);
            Assert.That(tuning, Is.Not.Null);
            Assert.That(tuning.def.autoSprint, Is.False, "you walk unless you sprint (the creator, 2026-10-10)");
            Assert.That(tuning.defaultsVersion, Is.EqualTo(MovementTuning.CurrentDefaults), "the builder brought the asset up to the current defaults");
            Assert.That(new MoveDef().autoSprint, Is.False);
            var controls = DefaultControls.Create();
            try
            {
                var sprint = controls.FindAction("Gameplay/Sprint", false);
                Assert.That(sprint, Is.Not.Null);
                var paths = new List<string>();
                foreach (var b in sprint.bindings) paths.Add(b.path);
                Assert.That(paths, Does.Contain("<Keyboard>/leftShift").And.Contain("<Gamepad>/leftShoulder"), "Shift and LB hold");
                var toggle = controls.FindAction("Gameplay/SprintToggle", false);
                Assert.That(toggle, Is.Not.Null);
                Assert.That(toggle.bindings[0].path, Is.EqualTo("<Gamepad>/leftStickPress"), "L3 toggles sprint");
                var crouchToggle = controls.FindAction("Gameplay/CrouchToggle", false);
                Assert.That(crouchToggle, Is.Not.Null);
                Assert.That(crouchToggle.bindings[0].path, Is.EqualTo("<Gamepad>/rightStickPress"), "R3 toggles crouch");
            }
            finally { Object.DestroyImmediate(controls); }
        }

        [Test]
        public void TheShellHasTheMovementSoundsAndACrouchBinding()
        {
            foreach (var id in new[] { "mv_wind", "mv_slide", "mv_wallrun", "mv_step", "mv_jump", "mv_djump", "mv_walljump", "mv_catch", "mv_land", "mv_static" })
            {
                Assert.That(ProceduralAudio.Knows(id), Is.True, id);
                var clip = ProceduralAudio.Get(id);
                Assert.That(clip, Is.Not.Null, id);
                Assert.That(clip.length, Is.GreaterThan(0.03f), id);
            }
            var controls = DefaultControls.Create();
            try
            {
                var crouch = controls.FindAction("Gameplay/Crouch", false);
                Assert.That(crouch, Is.Not.Null);
                Assert.That(crouch.bindings.Count, Is.GreaterThanOrEqualTo(3), "Ctrl, C and the pad's B");
            }
            finally { Object.DestroyImmediate(controls); }
        }
    }
}
