using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>Builds the pipeline's scenes from code each pass (they're generated, not committed): the firing range
    /// and the enemy gallery. In M0 both are empty-but-working: a floor, a light, a camera with an audio listener, and
    /// the test tone in the firing range; both carry the SkylineSceneGuard that keeps the shared GameRoot out.</summary>
    public static class SkylineScenes
    {
        public const string Folder = "Assets/JamKit/Templates/SkylineRepo/Generated";
        public const string FiringRange = Folder + "/SkylineFiringRange.unity";
        public const string Gallery = Folder + "/SkylineGallery.unity";

        public static string[] BuildAll()
        {
            Directory.CreateDirectory(Folder);
            Build(FiringRange, withTone: true);
            Build(Gallery, withTone: false);
            AssetDatabase.Refresh();
            return new[] { FiringRange, Gallery };
        }

        static void Build(string path, bool withTone)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Visiting Hours' shared shell (GameRoot) would start a run and save to the shared save.json in any scene; the guard
            // removes it in its Awake, before any other script of the scene (see SkylineSceneGuard).
            new GameObject("SkylineSceneGuard").AddComponent<JamKit.SkylineRepo.SkylineSceneGuard>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f);

            var cam = new GameObject("Camera");
            cam.tag = "MainCamera";
            cam.AddComponent<Camera>();
            cam.AddComponent<AudioListener>();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -8f), Quaternion.Euler(8f, 0f, 0f));

            if (withTone)
            {
                var tone = new GameObject("TestTone");
                tone.AddComponent<AudioSource>();
                tone.AddComponent<JamKit.SkylineRepo.SkylineTestTone>();
            }

            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
