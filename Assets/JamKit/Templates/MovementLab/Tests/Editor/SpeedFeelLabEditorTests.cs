using JamKit.Movement;
using JamKit.MovementLab.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The speed visuals are wired in the Movement Lab (docs/SPEED-FEEL.md "Speed feel (built)"): the hub on the
    /// player's camera; the boom in the camera feel; the full-screen pass, the streaks, the puffs and footprints with
    /// their materials on the right shaders; the speed lines scaled by the hub. Run "JamKit/Rebuild Shell Scenes" if
    /// the scene is stale.</summary>
    public class SpeedFeelLabEditorTests
    {
        [Test]
        public void TheLabPlayerHasEverySpeedVisualWired()
        {
            var scene = EditorSceneManager.OpenScene(MovementLabBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                MovementController body = null;
                foreach (var go in scene.GetRootGameObjects())
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                Assert.That(body, Is.Not.Null);
                AssertWired(body);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>The wiring checks (the dream escape's SpeedFeelDreamEditorTests has a copy).</summary>
        public static void AssertWired(MovementController body)
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
