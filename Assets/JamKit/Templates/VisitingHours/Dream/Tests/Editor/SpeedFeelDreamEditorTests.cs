using JamKit.DreamEscape.EditorTools;
using JamKit.Movement;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>The speed visuals are wired in the dream escape (docs/SPEED-FEEL.md "Speed feel (built)"): everything the
    /// lab has, in the dream's pastel look (confetti and petals, blooms, warm footprints), plus V7, the dream answering
    /// (the grade on the full-screen pass, the flowers, the static's visual recede). Run "JamKit/Rebuild Shell Scenes"
    /// if the scene is stale.</summary>
    public class SpeedFeelDreamEditorTests
    {
        [Test]
        public void TheDreamPlayerHasEverySpeedVisualWiredAndTheDreamAnswers()
        {
            var scene = EditorSceneManager.OpenScene(DreamEscapeBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                MovementController body = null;
                DreamEscapeGame game = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                    if (game == null) game = go.GetComponentInChildren<DreamEscapeGame>(true);
                }
                Assert.That(body, Is.Not.Null);
                Assert.That(game, Is.Not.Null);
                AssertWired(body);
                var cam = body.cam;
                var streaks = cam.GetComponent<SpeedStreaks>();
                Assert.That(streaks.material.GetVector("_Shapes").y, Is.EqualTo(2f), "confetti and petals");
                var answer = cam.GetComponent<DreamSpeedAnswer>();
                Assert.That(answer, Is.Not.Null, "V7 the dream answers");
                Assert.That(answer.feel, Is.SameAs(cam.GetComponent<SpeedFeel>()));
                Assert.That(answer.screen, Is.SameAs(cam.GetComponent<SpeedScreenFx>()), "the grade");
                Assert.That(answer.staticView, Is.SameAs(game.staticView), "the static's visual recede");
                Assert.That(answer.flowerMaterial, Is.Not.Null, "flowers");
                Assert.That(answer.flowerMaterial.shader.name, Is.EqualTo("JamKit/Movement/SpeedMotes"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>The same checks as the Movement Lab's SpeedFeelLabEditorTests (test assemblies can't share code).</summary>
        static void AssertWired(MovementController body)
        {
            var cam = body.cam;
            Assert.That(cam, Is.Not.Null);
            var feel = cam.GetComponent<SpeedFeel>();
            Assert.That(feel, Is.Not.Null, "the speed tiers' hub on the camera");
            Assert.That(feel.body, Is.SameAs(body));
            Assert.That(body.GetComponentInChildren<MovementCamera>(true).speedFeel, Is.SameAs(feel), "V1 the boom's FOV punch");
            var screen = cam.GetComponent<SpeedScreenFx>();
            Assert.That(screen, Is.Not.Null, "V1 ring and chromatic pulse, V6 edge blur");
            Assert.That(screen.feel, Is.SameAs(feel));
            Assert.That(screen.material, Is.Not.Null);
            Assert.That(screen.material.shader.name, Is.EqualTo("JamKit/Movement/SpeedScreen"));
            var streaks = cam.GetComponent<SpeedStreaks>();
            Assert.That(streaks, Is.Not.Null, "V2 world-space streaks");
            Assert.That(streaks.feel, Is.SameAs(feel));
            Assert.That(streaks.material, Is.Not.Null);
            Assert.That(streaks.material.shader.name, Is.EqualTo("JamKit/Movement/SpeedMotes"));
            Assert.That(streaks.palette.Length, Is.GreaterThan(0));
            Assert.That(streaks.material.GetVector("_CenterClear").x, Is.GreaterThan(0f), "streaks keep the centre clear");
            var lines = cam.GetComponent<SpeedLines>();
            Assert.That(lines, Is.Not.Null, "V3 speed lines");
            Assert.That(lines.feel, Is.SameAs(feel));
            Assert.That(lines.material.HasProperty("_CoreColor"), Is.True, "the contrast-proof lines shader");
            var feet = cam.GetComponent<SpeedFootfalls>();
            Assert.That(feet, Is.Not.Null, "V5 puffs and footprints");
            Assert.That(feet.feel, Is.SameAs(feel));
            Assert.That(feet.puffMaterial, Is.Not.Null);
            Assert.That(feet.footprintMaterial, Is.Not.Null);
            Assert.That(feet.footprintMaterial.GetFloat("_DstBlend"), Is.EqualTo((float)UnityEngine.Rendering.BlendMode.One), "footprints glow (additive)");
            Assert.That(body.tuning.speedFeel, Is.Not.Null, "the numbers live in the tuning asset");
            Assert.That(body.tuning.speedTiers, Is.Not.Null);
        }
    }
}
