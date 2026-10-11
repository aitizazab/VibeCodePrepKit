using System.IO;
using JamKit.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Argument.EditorTools
{
    /// <summary>
    /// Builds the argument prototype's scene (docs/ARGUMENT-PROTOTYPE.md): a placeholder kitchen for Dream-Lena
    /// (breakfast for four at a round-ish table, the pancake stack in the middle, the fourth chair pulled out a
    /// little), a fixed camera, a warm light, and the ArgumentPrototype component. Runs as a step of
    /// "JamKit/Rebuild Shell Scenes" (it adds the scene to Build Settings); "JamKit/Argument Prototype/Rebuild
    /// Scene" rebuilds only this. Everything is placeholder art.
    /// </summary>
    public static class ArgumentPrototypeBuilder
    {
        public const string Root = "Assets/JamKit/Templates/VisitingHours/Argument";
        public const string ScenePath = Root + "/Scenes/" + ArgumentPrototype.Scene + ".unity";
        public const string MaterialsDir = Root + "/Materials";

        [JamKitBuildStep(250)]
        static void Build(SceneBuilder.Context ctx)
        {
            string scene = BuildScene();
            if (!string.IsNullOrEmpty(scene)) ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Argument Prototype/Rebuild Scene")]
        static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        public static string BuildScene()
        {
            Ensure(Root + "/Scenes");
            Ensure(MaterialsDir);
            var scene = SceneBuilder.NewScene(new Color(0.98f, 0.86f, 0.74f), out var cam);
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 2.6f, -4.2f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

            var light = new GameObject("Kitchen light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.9f, 0.76f);
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.56f, 0.6f);

            var wood = Mat("Table", new Color(0.72f, 0.5f, 0.34f));
            var cloth = Mat("Cloth", new Color(0.98f, 0.93f, 0.84f));
            var plate = Mat("Plate", new Color(0.92f, 0.94f, 0.98f));
            var pancake = Mat("Pancake", new Color(0.93f, 0.72f, 0.38f));
            var wall = Mat("Wall", new Color(0.86f, 0.9f, 0.78f));
            var floor = Mat("Floor", new Color(0.62f, 0.56f, 0.66f));
            var lena = Mat("Lena", new Color(0.52f, 0.36f, 0.5f));

            var set = new GameObject("Kitchen (placeholder)").transform;
            Prim(PrimitiveType.Cube, "Floor", set, new Vector3(0f, -0.05f, 1f), new Vector3(12f, 0.1f, 10f), floor);
            Prim(PrimitiveType.Cube, "Back wall", set, new Vector3(0f, 2f, 4.5f), new Vector3(12f, 4f, 0.2f), wall);
            Prim(PrimitiveType.Cylinder, "Table", set, new Vector3(0f, 0.75f, 1f), new Vector3(2.4f, 0.04f, 1.6f), cloth);
            Prim(PrimitiveType.Cylinder, "Table leg", set, new Vector3(0f, 0.37f, 1f), new Vector3(0.2f, 0.37f, 0.2f), wood);
            // The pancake stack: the claim's centrepiece.
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cylinder, "Pancake " + (i + 1), set, new Vector3(0f, 0.8f + i * 0.035f, 1f), new Vector3(0.36f, 0.016f, 0.36f), pancake);
            // Breakfast for four: a plate at each seat (the premises' pieces), the chairs, the fourth pulled out.
            var seats = new[] { new Vector3(-0.8f, 0f, 1f), new Vector3(0.8f, 0f, 1f), new Vector3(0f, 0f, 1.65f), new Vector3(0f, 0f, 0.35f) };
            for (int i = 0; i < seats.Length; i++)
            {
                var s = seats[i];
                Prim(PrimitiveType.Cylinder, "Plate " + (i + 1), set, new Vector3(s.x * 0.8f, 0.79f, 1f + (s.z - 1f) * 0.8f), new Vector3(0.28f, 0.01f, 0.28f), plate);
                Vector3 out1 = (s - new Vector3(0f, 0f, 1f)).normalized;
                float pull = i == 3 ? 0.45f : 0f; // the fourth chair, pulled out
                var chair = new Vector3(s.x + out1.x * (0.45f + pull), 0.45f, s.z + out1.z * (0.45f + pull));
                Prim(PrimitiveType.Cube, "Chair " + (i + 1), set, chair, new Vector3(0.42f, 0.9f, 0.42f), wood);
            }
            // Lena's stand-in (her billboard comes with the real art).
            Prim(PrimitiveType.Capsule, "Lena (stand-in)", set, new Vector3(0f, 1.05f, 2.25f), new Vector3(0.5f, 0.75f, 0.5f), lena);

            new GameObject("Argument prototype").AddComponent<ArgumentPrototype>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("JAMKIT_ARGUMENT_OK " + ScenePath);
            return ScenePath;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Material Mat(string name, Color color)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
