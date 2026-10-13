using UnityEditor.SceneManagement;
using UnityEngine;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>Builds the enemy gallery scene: each bot idle, alert and attacking (Task 15). For now M0's empty-but-working content: a light, a 40 x 40 m
    /// floor and a camera with the audio listener.</summary>
    public static class SkylineGalleryBuilder
    {
        public static void Build(string path)
        {
            var scene = SkylineScenes.NewScene();

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

            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
