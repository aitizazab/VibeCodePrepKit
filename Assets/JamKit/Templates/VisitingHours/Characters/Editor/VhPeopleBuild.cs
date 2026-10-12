using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace JamKit.VisitingHours.Characters.EditorTools
{
    /// <summary>
    /// Builds the 3D cast's prefabs from the models (docs/redesign/people.md "Building the prefabs"):
    /// for each character in <see cref="Cast"/> whose model exists, it sets the model to a Humanoid import at the
    /// character's height, makes a URP Lit material from the model's textures (base colour, normal map, and a
    /// metallic/smoothness map built from Tripo's separate roughness and metallic maps), imports every clip in
    /// Models/Animations as Humanoid (baked in place, looping by name), builds one Animator Controller with a state per
    /// clip, and saves Characters/Resources/People/&lt;Name&gt;.prefab with <see cref="VhCharacter"/>, <see cref="VhFace"/>
    /// and a capsule collider. Re-running it rebuilds everything (safe to repeat).
    ///
    /// Menu: JamKit > Visiting Hours > People > Build the Cast. Batch:
    /// -executeMethod JamKit.VisitingHours.Characters.EditorTools.VhPeopleBuild.BuildFromCommandLine
    /// </summary>
    public static class VhPeopleBuild
    {
        public const string Root = "Assets/JamKit/Templates/VisitingHours/";
        public const string ModelsDir = Root + "Models/";
        public const string AnimationsDir = ModelsDir + "Animations/";
        public const string CharactersDir = Root + "Characters/";
        public const string GeneratedDir = CharactersDir + "Generated/";
        public const string PeopleDir = CharactersDir + "Resources/People/";

        public sealed class Spec
        {
            public string name, model, note;
            public float height;
            public Spec(string name, string model, float height, string note) { this.name = name; this.model = model; this.height = height; this.note = note; }
        }

        /// <summary>The cast: prefab name, model path under Models/, standing height in metres.</summary>
        /// <remarks>Nadia is not here: Models/Nadia/Nadia.fbx is imported as Generic for the World agent's first-person body
        /// (MovementBody / MixamoBodyRig), and switching it to Humanoid would break that. A third-person Nadia gets her own
        /// copy of the model when one is needed.</remarks>
        public static readonly Spec[] Cast =
        {
            new Spec("Lena", "Lena/Lena.fbx", 1.88f, "6'2\" (the creator: fine)"),
            new Spec("Theo", "Theo/Theo.fbx", 1.38f, "ten years old"),
            new Spec("Elias", "Elias/Elias.fbx", 1.83f, "the dream Elias: barbecue apron"),
            new Spec("EliasBed", "EliasBed/EliasBed.fbx", 1.83f, "the waking Elias in the hospital gown, posed lying"),
            new Spec("Walt", "Walt/Walt.fbx", 1.86f, "tall, thin, old"),
        };

        /// <summary>States that loop (a clip name starting with any of these). Everything else is a one-shot.</summary>
        public static readonly string[] Looping =
        {
            "idle", "talk", "walk", "run", "jog", "sit", "lie", "sleep", "listen", "think", "breath",
            "mow", "wash", "grill", "water", "ride", "bike", "browse", "sweep", "rake", "type", "write", "read",
        };

        [MenuItem("JamKit/Visiting Hours/People/Build the Cast")]
        static void Menu() { Debug.Log(Build()); }

        public static void BuildFromCommandLine()
        {
            int code = 0;
            try { Debug.Log("JAMKIT_VH_PEOPLE_BUILD_OK\n" + Build()); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string Build()
        {
            var report = new System.Text.StringBuilder();
            EnsureFolder(GeneratedDir);
            EnsureFolder(PeopleDir);
            var clips = ImportClips(report);
            foreach (var spec in Cast)
            {
                string path = ModelsDir + spec.model;
                if (AssetImporter.GetAtPath(path) == null) { report.AppendLine(spec.name + ": no model at " + path + " (skipped)"); continue; }
                try { report.AppendLine(BuildOne(spec, path, clips)); }
                catch (Exception e) { report.AppendLine(spec.name + ": FAILED " + e.Message); Debug.LogException(e); }
            }
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        // ---- Animations ----

        static List<AnimationClip> ImportClips(System.Text.StringBuilder report)
        {
            var list = new List<AnimationClip>();
            if (!AssetDatabase.IsValidFolder(AnimationsDir.TrimEnd('/'))) { report.AppendLine("animations: none (no " + AnimationsDir + ")"); return list; }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationsDir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                string state = StateName(path);
                bool dirty = false;
                if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
                if (!imp.importAnimation) { imp.importAnimation = true; dirty = true; }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
                var src = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
                if (src.Length > 0)
                {
                    var c = src[0];
                    bool loop = IsLooping(state);
                    if (c.name != state || c.loopTime != loop || !c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ || !c.keepOriginalOrientation || !c.keepOriginalPositionY || !c.keepOriginalPositionXZ)
                    {
                        c.name = state;
                        c.loopTime = loop;
                        c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;   // baked in place: code moves the character
                        c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                        imp.clipAnimations = new[] { c };
                        dirty = true;
                    }
                }
                if (dirty) imp.SaveAndReimport();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) { list.Add(clip); break; }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            report.Append("animations (" + list.Count + "):");
            foreach (var c in list) report.Append(' ').Append(c.name);
            report.AppendLine();
            return list;
        }

        /// <summary>"Models/Animations/Talk Gesture 2.fbx" → "talk-gesture-2".</summary>
        public static string StateName(string path)
        {
            string n = Path.GetFileNameWithoutExtension(path).Trim().ToLowerInvariant();
            var sb = new System.Text.StringBuilder(n.Length);
            bool dash = false;
            foreach (char ch in n)
            {
                if (char.IsLetterOrDigit(ch)) { sb.Append(ch); dash = false; }
                else if (!dash && sb.Length > 0) { sb.Append('-'); dash = true; }
            }
            return sb.ToString().TrimEnd('-');
        }

        public static bool IsLooping(string state)
        {
            foreach (var p in Looping) if (state.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        // ---- One character ----

        static string BuildOne(Spec spec, string modelPath, List<AnimationClip> clips)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            bool dirty = false;
            if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
            if (imp.importAnimation) { imp.importAnimation = false; dirty = true; }
            if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (!imp.importBlendShapes) { imp.importBlendShapes = true; dirty = true; }
            if (dirty) imp.SaveAndReimport();

            // Height: scale the import so the standing model is spec.height tall.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            float h = MeasureHeight(model);
            if (h > 0.01f && Mathf.Abs(h - spec.height) > 0.01f)
            {
                imp.globalScale *= spec.height / h;
                imp.SaveAndReimport();
                model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                h = MeasureHeight(model);
            }

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(modelPath)) if (o is Avatar a) avatar = a;
            string avatarNote = avatar == null ? "NO AVATAR" : avatar.isHuman && avatar.isValid ? "humanoid avatar ok" : "avatar not humanoid/valid";

            var mat = BuildMaterial(spec, modelPath);
            var controller = BuildController(spec, clips);

            // The prefab: root (VhCharacter, VhFace, capsule) > the model (Animator).
            var root = new GameObject(spec.name);
            try
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                body.transform.SetParent(root.transform, false);
                body.name = "Body";
                var anim = body.GetComponent<Animator>();
                if (anim == null) anim = body.AddComponent<Animator>();
                anim.avatar = avatar;
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                int shapeCount = 0;
                SkinnedMeshRenderer shapes = null;
                foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr)
                    {
                        smr.updateWhenOffscreen = false;
                        if (smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > shapeCount) { shapes = smr; shapeCount = smr.sharedMesh.blendShapeCount; }
                    }
                }

                var face = root.AddComponent<VhFace>();
                face.shapes = shapes;
                var ch = root.AddComponent<VhCharacter>();
                ch.characterName = spec.name;
                ch.animator = anim;
                ch.face = face;

                var cap = root.AddComponent<CapsuleCollider>();
                cap.height = spec.height;
                cap.radius = Mathf.Clamp(spec.height * 0.13f, 0.15f, 0.3f);
                cap.center = new Vector3(0f, spec.height * 0.5f, 0f);

                string prefabPath = PeopleDir + spec.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return spec.name + ": " + prefabPath + " · " + h.ToString("0.00") + " m · " + avatarNote + " · " + controller.layers[0].stateMachine.states.Length + " states · " + shapeCount + " blendshapes";
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static float MeasureHeight(GameObject model)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                bool any = false;
                var b = new Bounds();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                return any ? b.size.y : 0f;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ---- Material ----

        static Material BuildMaterial(Spec spec, string modelPath)
        {
            string dir = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            Texture2D baseMap = null, normal = null, rough = null, metal = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (n.EndsWith("_metallicsmoothness")) continue;
                if (n.Contains("normal")) normal = Normal(p);
                else if (n.Contains("rough")) rough = Linear(p, true);
                else if (n.Contains("metal")) metal = Linear(p, true);
                else if (n.Contains("color") || n.Contains("albedo") || n.Contains("diffuse") || n.Contains("base")) baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string matPath = GeneratedDir + spec.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            else mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP"); }
            var ms = rough != null || metal != null ? MetallicSmoothness(spec, rough, metal) : null;
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 0.75f);   // scales the map's alpha: cloth and skin stay soft
                mat.SetFloat("_Metallic", 1f);
            }
            else
            {
                mat.DisableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 0.25f);
                mat.SetFloat("_Metallic", 0f);
            }
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.SetFloat("_SpecularHighlights", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D Normal(string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D Linear(string path, bool readable)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.sRGBTexture || ti.isReadable != readable || ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.sRGBTexture = false; ti.isReadable = readable; ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>URP's metallic map: R = metallic, A = smoothness (1 - roughness). Written once per character.</summary>
        static Texture2D MetallicSmoothness(Spec spec, Texture2D rough, Texture2D metal)
        {
            var src = rough != null ? rough : metal;
            int w = Mathf.Min(src.width, 2048), h = Mathf.Min(src.height, 2048);
            var r = rough != null ? Resample(rough, w, h) : null;
            var m = metal != null ? Resample(metal, w, h) : null;
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
            {
                byte mv = m != null ? m[i].r : (byte)0;
                byte sv = r != null ? (byte)(255 - r[i].r) : (byte)64;
                px[i] = new Color32(mv, mv, mv, sv);
            }
            outTex.SetPixels32(px);
            outTex.Apply();
            string path = GeneratedDir + spec.name + "_MetallicSmoothness.png";
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.mipmapEnabled = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color32[] Resample(Texture2D t, int w, int h)
        {
            if (t.width == w && t.height == h) return t.GetPixels32();
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(t, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tmp);
            return px;
        }

        // ---- Controller ----

        static AnimatorController BuildController(Spec spec, List<AnimationClip> clips)
        {
            string path = GeneratedDir + spec.name + ".controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState idle = null;
            // Per-character clips (Models/<Name>/Animations) override shared ones of the same name.
            var own = new Dictionary<string, AnimationClip>();
            string ownDir = ModelsDir + Path.GetDirectoryName(spec.model).Replace('\\', '/') + "/Animations";
            if (AssetDatabase.IsValidFolder(ownDir))
                foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ownDir }))
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                        if (o is AnimationClip c && !c.name.StartsWith("__preview__")) own[c.name] = c;
            var names = new HashSet<string>();
            foreach (var c in clips) names.Add(c.name);
            foreach (var n in own.Keys) names.Add(n);
            var sorted = new List<string>(names);
            sorted.Sort(string.CompareOrdinal);
            foreach (var n in sorted)
            {
                AnimationClip clip = own.TryGetValue(n, out var oc) ? oc : clips.Find(c => c.name == n);
                var st = sm.AddState(n);
                st.motion = clip;
                st.writeDefaultValues = true;
                if (n == "idle") idle = st;
            }
            if (idle == null && sm.states.Length == 0) idle = sm.AddState("idle");   // an empty idle: the bind pose
            if (idle != null) sm.defaultState = idle;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
