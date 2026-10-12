using System.Text;
using JamKit.DreamEscape.EditorTools;
using JamKit.Movement;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>The geometry lint (JamKit/QA/Geometry Lint) on two tiny scenes built in memory: a faulty one where each of
    /// the five checks has exactly the fault it looks for, and a clean one where none reports anything.</summary>
    public class DreamGeometryLintTests
    {
        static GameObject Box(string name, Vector3 centre, Vector3 size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.position = centre;
            g.transform.localScale = size;
            return g;
        }

        static void Player(bool onlyMarkedWalls)
        {
            var g = new GameObject("Player");
            g.layer = 2;
            g.transform.position = new Vector3(0f, 1f, -5f);
            g.AddComponent<MovementController>().onlyMarkedWalls = onlyMarkedWalls;
        }

        static string Dump(LintReport r)
        {
            var sb = new StringBuilder();
            foreach (var f in r.findings) sb.Append('\n').Append(f.check).Append(": ").Append(f.message);
            return sb.ToString();
        }

        [Test]
        public void EachCheckFindsItsFault()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Player(true);
            // A 30 x 30 m floor (top at y 0) in four pieces around a 1 x 1 m hole at x, z in [-9, -8]; nothing under it,
            // so its outer edge drops out of the world.
            Box("Floor west", new Vector3(-12f, -0.5f, 0f), new Vector3(6f, 1f, 30f));
            Box("Floor east", new Vector3(3.5f, -0.5f, 0f), new Vector3(23f, 1f, 30f));
            Box("Floor south strip", new Vector3(-8.5f, -0.5f, -12f), new Vector3(1f, 1f, 6f));
            Box("Floor north strip", new Vector3(-8.5f, -0.5f, 3.5f), new Vector3(1f, 1f, 23f));
            // Two crates overlapping by 0.3 m.
            Box("Crate A", new Vector3(5f, 1f, 5f), Vector3.one * 2f);
            Box("Crate B", new Vector3(6.7f, 1f, 5f), Vector3.one * 2f);
            // A visible crate with no collider.
            Object.DestroyImmediate(Box("Ghost crate", new Vector3(-5f, 0.5f, 5f), Vector3.one).GetComponent<Collider>());
            // A 6 m x 4 m wall marked not runnable, 1.6 m from the floor's edge (a drop into nothing).
            Box("Wall by the drop", new Vector3(13.1f, 2f, 0f), new Vector3(0.5f, 4f, 6f)).AddComponent<MovementSurface>().runnable = false;

            var r = DreamGeometryLint.Run(new LintOptions());
            string all = Dump(r);
            Assert.That(r.Count(DreamGeometryLint.ButtingIn), Is.EqualTo(1), "one overlapping pair" + all);
            var pair = r.Of(DreamGeometryLint.ButtingIn)[0];
            Assert.That(pair.value, Is.EqualTo(0.3f).Within(0.02f), "its depth");
            Assert.That(pair.paths.Length, Is.EqualTo(2));

            var ghosts = r.Of(DreamGeometryLint.NoCollider);
            Assert.That(ghosts.Count, Is.EqualTo(1), "the ghost crate" + all);
            Assert.That(ghosts[0].paths[0], Does.EndWith("Ghost crate"));

            var holes = r.Of(DreamGeometryLint.Holes);
            Assert.That(holes.Count, Is.EqualTo(1), "the 1 x 1 m hole" + all);
            Assert.That(holes[0].value, Is.EqualTo(1f).Within(0.01f), "its area");
            Assert.That(holes[0].position.x, Is.EqualTo(-8.5f).Within(0.3f));

            var walls = r.Of(DreamGeometryLint.NotRunnable);
            Assert.That(walls.Count, Is.EqualTo(1), "the wall's face toward the drop" + all);
            Assert.That(walls[0].value, Is.EqualTo(6f).Within(0.01f), "length");
            Assert.That(walls[0].value2, Is.EqualTo(4f).Within(0.01f), "height");

            Assert.That(r.Count(DreamGeometryLint.NoFallVolume), Is.GreaterThanOrEqualTo(1), "the floor's edge" + all);
        }

        [Test]
        public void ACleanSceneReportsNothing()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Player(true);
            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1f, 20f));
            Box("Crate", new Vector3(3f, 1f, 3f), Vector3.one * 2f);
            Box("Runnable wall", new Vector3(8.1f, 2f, 0f), new Vector3(0.5f, 4f, 6f)).AddComponent<MovementSurface>().runnable = true;
            // The void under the level catches every fall.
            var fall = new GameObject("Fall volume");
            fall.transform.position = new Vector3(0f, -20f, 0f);
            var box = fall.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(60f, 10f, 60f);
            fall.AddComponent<DreamFallVolume>();

            var r = DreamGeometryLint.Run(new LintOptions());
            Assert.That(r.Total, Is.EqualTo(0), "no findings" + Dump(r));
            Assert.That(r.counts.Count, Is.EqualTo(DreamGeometryLint.Checks.Length));
            Assert.That(r.onlyMarkedWalls, Is.True, "read from the scene's MovementController");
        }
    }
}
