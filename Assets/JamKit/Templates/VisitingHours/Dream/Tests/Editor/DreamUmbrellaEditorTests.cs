using JamKit.DreamEscape.EditorTools;
using JamKit.Movement;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>The front door into the rain in the escape's scene: the umbrella component is wired to the escape, the
    /// closed umbrella and its glow stand by the door with no collider, the open one rides on her body (hidden at first,
    /// off the raycast layer), the rain covers the path past the door, the tool's beam is there. Run "JamKit/Rebuild
    /// Shell Scenes" if the scene is missing.</summary>
    public class DreamUmbrellaEditorTests
    {
        [Test]
        public void TheUmbrellaAndTheRainAreWired()
        {
            var d = DreamEscapeCourse.Build();
            var scene = EditorSceneManager.OpenScene(DreamEscapeBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                DreamUmbrella u = null;
                MovementController body = null;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (u == null) u = go.GetComponentInChildren<DreamUmbrella>(true);
                    if (body == null) body = go.GetComponentInChildren<MovementController>(true);
                }
                Assert.That(u, Is.Not.Null, "the umbrella's run");
                Assert.That(u.host, Is.SameAs(u.GetComponent<DreamEscapeGame>()), "the escape is its host");
                Assert.That(((IDreamRainHost)u.host).Player, Is.SameAs(body));
                Assert.That(u.autoFire, Is.True);
                Assert.That(u.tool.conePad, Is.GreaterThan(u.tool.coneMouse), "the dream tool's aim, stronger on a pad");
                Assert.That(u.rainDef.soakedSpeed, Is.InRange(0.6f, 0.9f), "a modest cost");

                Assert.That(u.prop, Is.Not.Null);
                Assert.That(Vector3.Distance(u.prop.position, new Vector3(d.umbrella.X, d.umbrella.Y, d.umbrella.Z)), Is.LessThan(0.01f), "by the door");
                Assert.That(u.prop.GetComponentsInChildren<Collider>(true), Is.Empty, "never in the way");
                Assert.That(u.glow, Is.Not.Null, "highlighted");
                Assert.That(u.glow.GetComponentsInChildren<Collider>(true), Is.Empty);

                Assert.That(u.canopy, Is.Not.Null);
                Assert.That(u.canopy.activeSelf, Is.False, "closed until she takes it");
                Assert.That(u.canopy.transform.IsChildOf(body.transform), Is.True, "it rides on her");
                Assert.That(u.canopy.GetComponentsInChildren<Collider>(true), Is.Empty);
                foreach (var t in u.canopy.GetComponentsInChildren<Transform>(true)) Assert.That(t.gameObject.layer, Is.EqualTo(2), t.name + " is off the raycast layer");

                Assert.That(u.rainFx, Is.Not.Null, "the rain");
                Assert.That(u.rainFx.main.playOnAwake, Is.False, "it starts near the door");
                var shape = u.rainFx.shape;
                float z0 = u.rainFx.transform.position.z - shape.scale.y * 0.5f, z1 = u.rainFx.transform.position.z + shape.scale.y * 0.5f;
                Assert.That(z0, Is.LessThan(d.rainStartS), "the rain starts at the door");
                Assert.That(z1, Is.GreaterThan(d.rainEndS), "and covers the path");
                Assert.That(u.beam, Is.Not.Null);
                Assert.That(u.beam.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
