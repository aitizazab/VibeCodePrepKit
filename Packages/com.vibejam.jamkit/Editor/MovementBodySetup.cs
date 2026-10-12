using JamKit.Movement;
using UnityEditor;
using UnityEngine;

namespace JamKit.EditorTools
{
    /// <summary>
    /// Builders' helper for the first-person body: puts a Mixamo-boned character model under the player
    /// and wires it into the <see cref="MovementBody"/> through a <see cref="MixamoBodyRig"/> (the capsules
    /// stay as the fallback when the model or its material is missing). Paths come from the template, so
    /// the package holds no template assets.
    /// </summary>
    public static class MovementBodySetup
    {
        /// <summary>The model at <paramref name="modelPath"/> under <paramref name="player"/>, every slot drawn with
        /// <paramref name="material"/>, wired as <paramref name="body"/>'s rig. Null when the model or the material is missing.</summary>
        public static MixamoBodyRig Wire(MovementBody body, Transform player, string modelPath, Material material, string name = "Body")
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null || material == null || body == null)
            {
                Debug.Log("JAMKIT_MOVEMENT body: " + (prefab == null ? modelPath + " isn't there" : "no material") + "; the placeholder arms stay.");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetParent(player, false);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            var rig = go.AddComponent<MixamoBodyRig>();
            rig.model = go.transform;
            // The head, hair and neck's sphere for the first-person cut-away, measured here (the mesh isn't Read/Write, so a
            // player can't measure it) and saved with the scene.
            if (!rig.MeasureHead()) Debug.Log("JAMKIT_MOVEMENT body: couldn't measure " + modelPath + "'s head; the rig estimates it from the bones.");
            body.rig = rig;
            return rig;
        }

        /// <summary>A material asset at <paramref name="path"/> with <paramref name="shaderName"/> and <paramref name="texture"/> as its
        /// base map (white tint), and, when the shader has them, its own sky and ground ambient (a scene's light
        /// for it). Null when the shader isn't in the project.</summary>
        public static Material EnsureMaterial(string path, string shaderName, Texture2D texture, Color skyAmbient, Color groundAmbient, float unityAmbient)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) return null;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_SkyAmbient")) mat.SetColor("_SkyAmbient", skyAmbient);
            if (mat.HasProperty("_GroundAmbient")) mat.SetColor("_GroundAmbient", groundAmbient);
            if (mat.HasProperty("_SHAmbient")) mat.SetFloat("_SHAmbient", unityAmbient);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The first texture in <paramref name="folder"/> (and below) named Color, *_color, *basecolor or *albedo.</summary>
        public static Texture2D FindColorTexture(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (n == "color" || n.EndsWith("_color", System.StringComparison.Ordinal) || n.Contains("basecolor") || n.Contains("albedo"))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            return null;
        }
    }
}
