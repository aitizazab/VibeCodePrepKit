using System.Collections.Generic;
using System.IO;
using JamKit.DreamEscape.EditorTools;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Visiting Hours' first escape in the editor: the built scene, its assets and Build Settings entry;
    /// the player's movement parts; one view per course piece, the cast, the static and the music; the route fits
    /// the reach of the movement tuning the scene really uses; checkpoints are ordered and carry the presenter
    /// chapters. Run "JamKit/Rebuild Shell Scenes" if the assets are missing.</summary>
    public class DreamEscapeEditorTests
    {
        static List<string> Registered()
        {
            var r = new ChapterRegistry();
            DreamEscapePresenter.Register(r, null, new object());
            var idx = new List<int>();
            r.InGroup(DreamEscapePresenter.Scene, idx);
            var names = new List<string>();
            foreach (int i in idx) names.Add(r.NameAt(i));
            return names;
        }

        [Test]
        public void TheThreeChaptersAreRegisteredInOrderAndAreCheckpoints()
        {
            var names = Registered();
            Assert.That(names, Is.EqualTo(new[] { "Escape start", "Rooftops", "Final run" }));
            var course = DreamEscapeCourse.Build().course;
            int last = -1;
            foreach (var n in names)
            {
                int i = course.CheckpointIndex(n);
                Assert.That(i, Is.GreaterThan(last), n + " is a checkpoint, in route order");
                last = i;
            }
        }

        [Test]
        public void TheAttractScriptPlaysOnlyRealChaptersAndSegments()
        {
            var script = DreamEscapePresenter.Attract();
            Assert.That(script.scene, Is.EqualTo(DreamEscapePresenter.Scene));
            var problems = script.Validate(Registered(), DreamEscapePresenter.Segments);
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(script.TotalSeconds, Is.InRange(30f, 120f));
        }

        [Test]
        public void TheSceneTuningPresetAndMaterialsExist()
        {
            Assert.That(File.Exists(DreamEscapeBuilder.ScenePath), Is.True, DreamEscapeBuilder.ScenePath);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(DreamEscapeBuilder.ScenePath), Is.Not.Null);
            Assert.That(DreamEscapeBuilder.ScenePath, Does.EndWith(DreamEscapePresenter.Scene + ".unity"));
            var preset = AssetDatabase.LoadAssetAtPath<GameConfig>(DreamEscapeBuilder.PresetPath);
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.firstGameplayScene, Is.EqualTo(DreamEscapeBuilder.SceneName));
            Assert.That(preset.howToPlay.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var name in new[] { "Pastel_Lawn", "Pastel_GoStripe", "Pastel_RoofTiles0_Gable", "DreamStatic", "DreamWasp", "DreamSky", "Glow_RoutePool", "Glow_Pillar", "SpeedLines", "Cutout_dream_walt", "Cutout_dream_dog", "Nadia_Sleeve" })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(DreamEscapeBuilder.MaterialsDir + "/" + name + ".mat");
                Assert.That(mat, Is.Not.Null, name);
                Assert.That(mat.shader.isSupported, Is.True, name + " shader " + mat.shader.name);
                Assert.That(mat.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), name);
            }
            foreach (var name in new[] { "Cutout_dream_walt", "Cutout_dream_neighbour_roses", "Cutout_dream_neighbour_grill", "Cutout_dream_dog", "DreamWasp" })
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(DreamEscapeBuilder.MaterialsDir + "/" + name + ".mat").GetTexture("_MainTex"), Is.Not.Null, name + " has its picture (real art or the placeholder)");
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(DreamEscapeBuilder.GablePath), Is.Not.Null);
        }

        [Test]
        public void TheEscapeIsInBuildSettings()
        {
            bool found = false;
            foreach (var s in EditorBuildSettings.scenes) found |= s.path == DreamEscapeBuilder.ScenePath && s.enabled;
            Assert.That(found, Is.True, "Rebuild Shell Scenes puts the dream escape in Build Settings");
        }

        [Test]
        public void TheRouteFitsTheReachOfTheTuningTheSceneUses()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(DreamEscapeBuilder.TuningPath);
            Assert.That(tuning, Is.Not.Null);
            var dream = DreamEscapeCourse.Build();
            var problems = dream.CheckReach(tuning.def);
            Assert.That(problems, Is.Empty, "the dream escape's tuning was changed beyond the route's sizes:\n" + string.Join("\n", problems));
        }

        [Test]
        public void CheckpointsAreOrderedAndEveryCollapseHasAWave()
        {
            var dream = DreamEscapeCourse.Build();
            var cps = dream.course.checkpoints;
            Assert.That(cps.Count, Is.InRange(3, 4));
            for (int i = 1; i < cps.Count; i++) Assert.That(cps[i].s, Is.GreaterThan(cps[i - 1].s), cps[i].name);
            Assert.That(dream.waves.Count, Is.EqualTo(dream.course.collapses.Count));
            Assert.That(dream.course.collapses.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheSceneHasThePlayerTheStreetTheCastTheStaticAndTheMusic()
        {
            var scene = EditorSceneManager.OpenScene(DreamEscapeBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                MovementController body = null;
                DreamEscapeGame game = null;
                var views = new List<DreamPieceView>();
                var cast = new List<DreamCutoutView>();
                var surfaces = new List<MovementSurface>();
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    if (game == null) game = go.GetComponentInChildren<DreamEscapeGame>(true);
                    views.AddRange(go.GetComponentsInChildren<DreamPieceView>(true));
                    cast.AddRange(go.GetComponentsInChildren<DreamCutoutView>(true));
                    surfaces.AddRange(go.GetComponentsInChildren<MovementSurface>(true));
                }
                Assert.That(body, Is.Not.Null);
                Assert.That(body.head, Is.Not.Null);
                Assert.That(body.cam, Is.Not.Null);
                Assert.That(body.tuning, Is.Not.Null);
                Assert.That(body.onlyMarkedWalls, Is.True, "only the gold-striped walls run");
                Assert.That(body.GetComponentInChildren<MovementCamera>(true), Is.Not.Null, "FOV, roll, landing dip");
                Assert.That(body.GetComponentInChildren<MovementBody>(true), Is.Not.Null, "the arms and the lean-in hand");
                Assert.That(body.GetComponentInChildren<SpeedLines>(true).material, Is.Not.Null);
                Assert.That(body.GetComponent<MovementAudio>(), Is.Not.Null);

                var dream = DreamEscapeCourse.Build();
                Assert.That(views.Count, Is.EqualTo(dream.course.pieces.Count), "one view per course piece");
                var seen = new HashSet<int>();
                foreach (var v in views) Assert.That(seen.Add(v.index), Is.True, "piece " + v.index + " built once");
                int runnable = 0, marked = 0;
                foreach (var p in dream.course.pieces) if (p.runnable) runnable++;
                foreach (var s in surfaces) if (s.runnable) marked++;
                Assert.That(marked, Is.EqualTo(runnable), "every runnable wall, and only those, is marked runnable");
                Assert.That(cast.Count, Is.EqualTo(dream.billboards.Count), "the cast as cut-outs");
                foreach (var c in cast) Assert.That(c.card, Is.Not.Null);

                Assert.That(game, Is.Not.Null);
                Assert.That(game.player, Is.SameAs(body));
                Assert.That(game.courseRoot, Is.Not.Null);
                Assert.That(game.skyDome, Is.Not.Null);
                Assert.That(game.exitLight, Is.Not.Null);
                Assert.That(game.staticView, Is.Not.Null);
                Assert.That(game.staticView.wall.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(game.staticView.wasps.Length, Is.GreaterThan(game.staticView.scouts));
                Assert.That(game.staticView.hiss, Is.Not.Null);
                Assert.That(game.staticView.buzz, Is.Not.Null);
                Assert.That(game.music, Is.Not.Null);
                Assert.That(game.music.pad, Is.Not.Null);
                Assert.That(game.music.pulse, Is.Not.Null);
                Assert.That(game.music.box, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheChaseMusicLoopsAreOneLengthAndNotSilent()
        {
            var clips = new[] { DreamSynth.Pad, DreamSynth.Pulse, DreamSynth.MusicBox };
            foreach (var c in clips)
            {
                Assert.That(c, Is.Not.Null);
                Assert.That(c.samples, Is.EqualTo(DreamSynth.LoopSamples), c.name + " loops in sync with the others");
                var data = new float[c.samples];
                c.GetData(data, 0);
                float peak = 0f;
                foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
                Assert.That(peak, Is.InRange(0.2f, 1f), c.name);
            }
            Assert.That(DreamSynth.Buzz.length, Is.GreaterThan(0.5f));
        }
    }
}
