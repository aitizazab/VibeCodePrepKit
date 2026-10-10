using JamKit.EditorTools;
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
            var course = DreamToolCourse.Build().course; // the old course (free roam), the Gauntlet, then the dream tool chapter
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
                Assert.That(pieces, Is.EqualTo(DreamToolCourse.Build().course.pieces.Count), "one view per course piece");
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
    
        // ---------- Nadia's body ----------

        [Test]
        public void NadiaIsTheFirstPersonBody()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MovementLabBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                MovementController body = null;
                Light sun = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    foreach (var l in go.GetComponentsInChildren<Light>(true)) if (l.type == LightType.Directional) sun = l;
                }
                Assert.That(body, Is.Not.Null);
                var mb = body.GetComponentInChildren<MovementBody>(true);
                Assert.That(mb, Is.Not.Null);
                Assert.That(mb.feelCamera, Is.Not.Null, "the body takes the landing after the camera");
                var rig = mb.rig as MixamoBodyRig;
                Assert.That(rig, Is.Not.Null, "the builder wires Nadia's model as the body");
                Assert.That(rig.transform.IsChildOf(body.transform), Is.True, "under the player");
                Assert.That(rig.GetComponentsInChildren<Collider>(true), Is.Empty, "no colliders: the movement is unchanged");
                foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(r.gameObject.layer, Is.EqualTo(2), "Ignore Raycast: the wall and ledge casts skip her");
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.That(m, Is.Not.Null);
                        Assert.That(m.shader.name, Is.EqualTo("JamKit/VisitingHours/Waking"), "the style-E waking material");
                        Assert.That(m.GetTexture("_BaseMap"), Is.Not.Null, "her colour texture");
                    }
                }
                Assert.That(sun, Is.Not.Null);
                Assert.That(sun.shadows, Is.Not.EqualTo(LightShadows.None), "her shadow grounds her");
                var copy = Object.Instantiate(rig.gameObject);
                try
                {
                    var bound = copy.GetComponent<MixamoBodyRig>();
                    Assert.That(bound.Bind(), Is.True, "Mixamo bones found (arms, legs, head)");
                    Assert.That(bound.HandPosition(0), Is.Not.EqualTo(bound.HandPosition(1)));
                }
                finally { Object.DestroyImmediate(copy); }
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NadiaImportsAtHerHeight()
        {
            var imp = AssetImporter.GetAtPath(MovementLabBuilder.NadiaPath) as ModelImporter;
            Assert.That(imp, Is.Not.Null, MovementLabBuilder.NadiaPath);
            Assert.That(imp.globalScale, Is.EqualTo(1.69f).Within(0.01f), "Tripo exports her 0.98 m tall: x1.69 is about 1.65 m");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MovementLabBuilder.NadiaPath);
            float h = 0f;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) h = Mathf.Max(h, r.bounds.size.y);
            TestContext.WriteLine("Nadia's height as imported: " + h.ToString("0.00") + " m");
        }

        [Test]
        public void WithoutAModelThePlaceholderStays()
        {
            var go = new GameObject("body test");
            try
            {
                var mb = go.AddComponent<MovementBody>();
                var mat = new Material(Shader.Find("Hidden/InternalErrorShader"));
                Assert.That(MovementBodySetup.Wire(mb, go.transform, "Assets/Nowhere/NoModel.fbx", mat), Is.Null);
                Assert.That(mb.rig, Is.Null);
                Assert.That(mb.UsingRig, Is.False, "the capsules draw");
                Object.DestroyImmediate(mat);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
