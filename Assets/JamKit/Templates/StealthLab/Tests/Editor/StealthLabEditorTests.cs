using JamKit.Movement;
using JamKit.Stealth;
using JamKit.StealthLab.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.StealthLab.Tests
{
    /// <summary>The Stealth Lab's scene is wired (run "JamKit/Rebuild Shell Scenes" if it's missing): the player with the
    /// movement controller and StealthPlayer; the seeker with its waypoints, patrol, tuning and overlay materials on the
    /// overlay shader; a hiding spot, light zone and collectible for each in the yard's data; solid boxes have colliders
    /// and see-through ones (bushes, canopy, curtain) don't; the game is wired; the scene is in Build Settings.</summary>
    public class StealthLabEditorTests
    {
        [Test]
        public void TheSceneIsWiredFromTheYard()
        {
            var yard = StealthYard.Build();
            var scene = EditorSceneManager.OpenScene(StealthLabBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                StealthLabGame game = null;
                SeekerAgent seeker = null;
                SeekerOverlay overlay = null;
                StealthPlayer player = null;
                int spots = 0, zones = 0, items = 0;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (game == null) game = go.GetComponentInChildren<StealthLabGame>(true);
                    if (seeker == null) seeker = go.GetComponentInChildren<SeekerAgent>(true);
                    if (overlay == null) overlay = go.GetComponentInChildren<SeekerOverlay>(true);
                    if (player == null) player = go.GetComponentInChildren<StealthPlayer>(true);
                    spots += go.GetComponentsInChildren<HidingSpotView>(true).Length;
                    zones += go.GetComponentsInChildren<StealthLightZone>(true).Length;
                    items += go.GetComponentsInChildren<Collectible>(true).Length;
                }
                Assert.That(game, Is.Not.Null);
                Assert.That(game.player, Is.Not.Null);
                Assert.That(game.seeker, Is.SameAs(seeker));
                Assert.That(game.collectibles, Is.Not.Null);
                Assert.That(player, Is.Not.Null);
                Assert.That(player.body, Is.SameAs(game.player));
                Assert.That(player.GetComponent<MovementController>().tuning, Is.Not.Null);
                Assert.That(seeker.tuning, Is.Not.Null);
                Assert.That(seeker.mode, Is.EqualTo(SeekerMode.HideAndSeek));
                Assert.That(seeker.waypoints.childCount, Is.EqualTo(yard.nodes.Count));
                Assert.That(seeker.patrol, Is.EqualTo(yard.patrol));
                Assert.That(seeker.GetComponentsInChildren<Collider>(true), Is.Empty, "the seeker never blocks sight or the player");
                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay.coneMaterial.shader.name, Is.EqualTo(StealthLabBuilder.OverlayShader));
                Assert.That(overlay.meterMaterial.shader.name, Is.EqualTo(StealthLabBuilder.OverlayShader));
                Assert.That(overlay.meterMaterial.GetFloat("_ZTestMode"), Is.EqualTo(8f), "the meter shows through walls");
                Assert.That(spots, Is.EqualTo(yard.spots.Count));
                Assert.That(zones, Is.EqualTo(yard.lights.Count));
                Assert.That(items, Is.EqualTo(yard.collectibles.Count));
                Transform yardRoot = null;
                foreach (var go in scene.GetRootGameObjects()) if (go.name == "Yard") yardRoot = go.transform;
                Assert.That(yardRoot, Is.Not.Null);
                foreach (var p in yard.pieces)
                {
                    var go = yardRoot.Find(p.name);
                    Assert.That(go, Is.Not.Null, p.name);
                    Assert.That(go.GetComponent<Collider>() != null, Is.EqualTo(p.Solid), p.name + (p.Solid ? " is solid" : " is see-through"));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheAnswersToFastMovementAreWired()
        {
            var yard = StealthYard.Build();
            var scene = EditorSceneManager.OpenScene(StealthLabBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                StealthLabGame game = null;
                foreach (var go in scene.GetRootGameObjects()) if (game == null) game = go.GetComponentInChildren<StealthLabGame>(true);
                Assert.That(game, Is.Not.Null);
                Assert.That(game.extraGuards.Count, Is.EqualTo(2), "guards 2 and 3");
                Assert.That(game.extraGuards[0].patrol, Is.EqualTo(yard.patrol2));
                Assert.That(game.extraGuards[1].patrol, Is.EqualTo(yard.patrol3));
                foreach (var g in game.extraGuards)
                {
                    Assert.That(g.gameObject.activeSelf, Is.False, "off until the three-guard option");
                    Assert.That(g.mode, Is.EqualTo(SeekerMode.Patrol));
                    Assert.That(g.waypoints, Is.SameAs(game.seeker.waypoints), "the same walking map");
                    Assert.That(g.GetComponent<SeekerOverlay>(), Is.Not.Null);
                    Assert.That(g.GetComponentsInChildren<Collider>(true), Is.Empty);
                }
                var rings = game.player.GetComponent<NoiseRings>();
                Assert.That(rings, Is.Not.Null, "the noise made visible");
                Assert.That(rings.material.shader.name, Is.EqualTo(StealthLabBuilder.OverlayShader));
                Assert.That(game.player.GetComponent<StealthPlayerRules>(), Is.Not.Null, "the daytime section and the lucidity fade");
                Assert.That(game.onCaught, Is.EqualTo(CaughtOutcome.RestartCheckpoint), "a catch restarts at the checkpoint");
                Assert.That(game.checkpoints.Count, Is.EqualTo(yard.checkpoints.Count), "the checkpoints");
                for (int i = 0; i < yard.checkpoints.Count; i++)
                {
                    var z = game.checkpoints[i];
                    Assert.That(z, Is.Not.Null);
                    Assert.That(z.label, Is.EqualTo(yard.checkpoints[i].name));
                    Assert.That(Vector3.Distance(z.transform.position, new Vector3(yard.checkpoints[i].spawn.X, yard.checkpoints[i].spawn.Y, yard.checkpoints[i].spawn.Z)), Is.LessThan(0.01f));
                    Assert.That(z.GetComponentsInChildren<Collider>(true), Is.Empty, "a pad, not an obstacle");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheOverlayShaderCompilesAndTheLabIsInBuildSettings()
        {
            var shader = Shader.Find(StealthLabBuilder.OverlayShader);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            bool listed = false;
            foreach (var s in EditorBuildSettings.scenes) if (s.path == StealthLabBuilder.ScenePath) listed = true;
            Assert.That(listed, Is.True, "Rebuild Shell Scenes adds the lab to Build Settings");
        }
    }
}
