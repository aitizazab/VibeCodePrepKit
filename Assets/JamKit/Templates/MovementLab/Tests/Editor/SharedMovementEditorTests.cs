using System.IO;
using JamKit.Movement;
using JamKit.MovementLab.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.MovementLab.Tests
{
    /// <summary>One shared movement tuning (docs/MOVEMENT.md "One shared tuning"): the asset loads by path and from
    /// Resources (<see cref="MovementTuning.Shared"/>), carries the daytime profile, and every scene that uses the
    /// movement (the Movement Lab, the dream escape and house, the Stealth Lab, the Deep Block) points its player at that
    /// same asset, in the dream profile. Scenes not built yet are skipped (run "JamKit/Rebuild Shell Scenes").</summary>
    public class SharedMovementEditorTests
    {
        static readonly string[] Scenes =
        {
            "Assets/JamKit/Templates/MovementLab/Scenes/MovementLab.unity",
            "Assets/JamKit/Templates/VisitingHours/Dream/Scenes/DreamEscape.unity",
            "Assets/JamKit/Templates/VisitingHours/Dream/Scenes/DreamHouse.unity",
            "Assets/JamKit/Templates/StealthLab/Scenes/StealthLab.unity",
            "Assets/JamKit/Templates/DeepBlock/Scenes/DeepBlock.unity",
        };

        [Test]
        public void TheSharedTuningLoadsAndCarriesTheDaytimeProfile()
        {
            var byPath = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementTuning.SharedAssetPath);
            Assert.That(byPath, Is.Not.Null, MovementTuning.SharedAssetPath);
            Assert.That(MovementTuning.Shared, Is.SameAs(byPath), "Resources finds the same asset");
            Assert.That(byPath.defaultsVersion, Is.EqualTo(MovementTuning.CurrentDefaults));
            Assert.That(byPath.daytime, Is.Not.Null);
            Assert.That(byPath.daytime.doubleJump || byPath.daytime.wallrun || byPath.daytime.slide, Is.False);
            Assert.That(byPath.daytime.airStrafeScale, Is.LessThan(1f));
            Assert.That(AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementLabBuilder.TuningPath), Is.SameAs(byPath), "the lab's builder uses it");
        }

        [Test]
        public void EveryMovementSceneUsesTheSharedTuning()
        {
            var shared = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementTuning.SharedAssetPath);
            int checkedScenes = 0;
            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    int bodies = 0;
                    foreach (var go in scene.GetRootGameObjects())
                        foreach (var body in go.GetComponentsInChildren<MovementController>(true))
                        {
                            bodies++;
                            Assert.That(body.tuning, Is.SameAs(shared), path + ": " + body.name + " uses the shared tuning");
                            Assert.That(body.profile, Is.EqualTo(MoveProfileKind.Dream), path + ": the dream's movement");
                        }
                    Assert.That(bodies, Is.GreaterThan(0), path + " has a movement player");
                    checkedScenes++;
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            Assert.That(checkedScenes, Is.GreaterThanOrEqualTo(1));
            TestContext.Out.WriteLine(checkedScenes + " movement scenes use the shared tuning");
        }
    }
}
