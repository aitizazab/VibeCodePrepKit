using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Ink.UnityIntegration;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace JamKit.EditorTools
{
    /// <summary>
    /// Marks a static method <c>void M(SceneBuilder.Context ctx)</c> that SceneBuilder runs after the
    /// shell's own steps (the sandbox uses it to build its scene). Lower order runs first.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class JamKitBuildStepAttribute : Attribute
    {
        public readonly int order;
        public JamKitBuildStepAttribute(int order = 100) { this.order = order; }
    }

    /// <summary>
    /// Rebuilds the shell from code: Boot and Title scenes, the UI and input assets, placeholder
    /// sprites, the sample portraits and Build Settings. Menu "JamKit/Rebuild Shell Scenes", or batch:
    /// <c>Unity -batchmode -quit -executeMethod JamKit.EditorTools.SceneBuilder.RebuildFromCommandLine</c>.
    /// Scenes and shell assets are regenerated every time. Team assets (GameConfig, AudioRegistry,
    /// the input actions) are only created when missing, so your edits survive a rebuild.
    /// </summary>
    public static class SceneBuilder
    {
        public const string Root = "Assets/JamKit";
        public const string ScenesDir = Root + "/Scenes";
        public const string ArtDir = Root + "/Art/Generated";
        public const string UiDir = Root + "/UI";
        public const string ResourcesDir = Root + "/Resources/JamKit";

        /// <summary>Scene paths added to Build Settings after Boot and Title (before the sandbox), e.g. your own levels.</summary>
        public static readonly List<string> ExtraScenes = new List<string>();

        /// <summary>What the build steps share.</summary>
        public sealed class Context
        {
            public Sprite square, circle, roundedSquare;
            public Material flashMaterial;
            public GameConfig config;
            /// <summary>Scene paths for Build Settings, in order.</summary>
            public readonly List<string> scenes = new List<string>();
        }

        [MenuItem("JamKit/Rebuild Shell Scenes")]
        public static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Rebuild();
        }

        public static void RebuildFromCommandLine()
        {
            bool ok;
            try { ok = Rebuild(); }
            catch (Exception e) { Debug.LogError("JAMKIT_SCENES_FAIL " + e); ok = false; }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Rebuild()
        {
            foreach (var d in new[] { ScenesDir, ArtDir, UiDir, ResourcesDir, Root + "/Input", Root + "/Audio", Root + "/Dialogue/Portraits", Root + "/Art" })
                EnsureFolder(d);

            var ctx = new Context();
            ctx.square = MakeSprite("square", 64, 64, Shapes.RoundedRect(64, 64, 6f, Color.white));
            ctx.roundedSquare = MakeSprite("rounded", 64, 64, Shapes.RoundedRect(64, 64, 18f, Color.white));
            ctx.circle = MakeSprite("circle", 64, 64, Shapes.Circle(64, Color.white));
            ctx.flashMaterial = MakeFlashMaterial();

            var portraits = MakePortraits();
            var audio = EnsureAudioRegistry();
            var input = EnsureInputActions();
            var shell = MakeShellAssets(input);
            CompileInk();
            ctx.config = EnsureGameConfig(audio, portraits);

            ctx.scenes.Add(BuildBoot());
            ctx.scenes.Add(BuildTitle());
            ctx.scenes.AddRange(ExtraScenes);

            var steps = TypeCache.GetMethodsWithAttribute<JamKitBuildStepAttribute>()
                .Where(m => m.IsStatic)
                .OrderBy(m => m.GetCustomAttribute<JamKitBuildStepAttribute>().order);
            foreach (var m in steps) m.Invoke(null, new object[] { ctx });

            WriteBuildSettings(ctx.scenes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("JAMKIT_SCENES_OK " + string.Join(", ", ctx.scenes.Select(Path.GetFileNameWithoutExtension)));
            // ReferenceEquals, not ==: later build steps open new scenes, which unloads the asset, and
            // Unity's == reports an unloaded (but saved) object as null.
            if (ReferenceEquals(shell, null)) return false;
            return true;
        }

        // ---------- Assets ----------

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Writes a PNG (only if its pixels changed) and imports it as a sprite.</summary>
        public static Sprite MakeSprite(string name, int w, int h, Color[] pixels, float pixelsPerUnit = 64f, string dir = ArtDir)
        {
            string path = dir + "/" + name + ".png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit)
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency
                || importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Material MakeFlashMaterial()
        {
            string path = Root + "/Art/SpriteFlash.mat";
            var shader = Shader.Find("JamKit/SpriteFlash");
            if (shader == null) throw new Exception("Shader JamKit/SpriteFlash not found (Packages/com.vibejam.jamkit/Runtime/Unity/Shaders).");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "SpriteFlash" };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static List<Portrait> MakePortraits()
        {
            var list = new List<Portrait>();
            list.Add(MakePortrait("Guide", new Color(0.36f, 0.78f, 0.74f), new Color(0.18f, 0.42f, 0.62f), false,
                ("neutral", Shapes.Face(Shapes.Mood.Neutral)), ("happy", Shapes.Face(Shapes.Mood.Happy)), ("worried", Shapes.Face(Shapes.Mood.Worried))));
            list.Add(MakePortrait("Rival", new Color(0.86f, 0.52f, 0.86f), new Color(0.45f, 0.22f, 0.55f), true,
                ("neutral", Shapes.Face(Shapes.Mood.Neutral)), ("smug", Shapes.Face(Shapes.Mood.Smug))));
            return list;
        }

        static Portrait MakePortrait(string id, Color skin, Color body, bool spiky, params (string name, Color[] pixels)[] expressions)
        {
            string dir = Root + "/Dialogue/Portraits";
            var p = LoadOrCreate<Portrait>(dir + "/" + id + ".asset", out _);
            p.characterId = id;
            p.baseSprite = MakeSprite("portrait_" + id.ToLowerInvariant() + "_base", Shapes.PortraitSize, Shapes.PortraitSize,
                Shapes.PortraitBase(skin, body, spiky), 100f, dir);
            p.defaultExpression = "neutral";
            p.expressions = new List<Portrait.Expression>();
            foreach (var (name, pixels) in expressions)
            {
                var sprite = MakeSprite("portrait_" + id.ToLowerInvariant() + "_" + name, Shapes.PortraitSize, Shapes.PortraitSize, pixels, 100f, dir);
                p.expressions.Add(new Portrait.Expression { name = name, sprite = sprite });
            }
            EditorUtility.SetDirty(p);
            return p;
        }

        static AudioRegistry EnsureAudioRegistry()
        {
            var reg = LoadOrCreate<AudioRegistry>(Root + "/Audio/AudioRegistry.asset", out bool created);
            if (created)
            {
                foreach (var id in ProceduralAudio.Ids)
                {
                    bool music = id.StartsWith("music", StringComparison.Ordinal);
                    reg.entries.Add(new AudioRegistry.Entry
                    {
                        id = id,
                        volume = music ? 0.6f : (id == "blip" ? 0.5f : 1f),
                        pitchVariance = music || id.StartsWith("ui", StringComparison.Ordinal) ? 0f : 0.06f,
                        loop = music,
                    });
                }
                EditorUtility.SetDirty(reg);
            }
            return reg;
        }

        static InputActionAsset EnsureInputActions()
        {
            string path = Root + "/Input/JamKitControls.inputactions";
            if (!File.Exists(path))
            {
                var asset = DefaultControls.Create();
                File.WriteAllText(path, asset.ToJson());
                UnityEngine.Object.DestroyImmediate(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        static ShellAssets MakeShellAssets(InputActionAsset input)
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiDir + "/JamKitTheme.tss");
            if (theme == null) Debug.LogWarning("JamKit: " + UiDir + "/JamKitTheme.tss is missing; controls will be unstyled.");
            var panel = LoadOrCreate<PanelSettings>(UiDir + "/JamKitPanelSettings.asset", out _);
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 100;
            EditorUtility.SetDirty(panel);

            var a = LoadOrCreate<ShellAssets>(ResourcesDir + "/ShellAssets.asset", out _);
            a.panelSettings = panel;
            a.input = input;
            a.styleSheets = new List<StyleSheet>
            {
                Need<StyleSheet>(UiDir + "/Theme.uss"),
                Need<StyleSheet>(UiDir + "/Shell.uss"),
            };
            string s = UiDir + "/Screens/";
            a.title = Need<VisualTreeAsset>(s + "Title.uxml");
            a.settings = Need<VisualTreeAsset>(s + "Settings.uxml");
            a.pause = Need<VisualTreeAsset>(s + "Pause.uxml");
            a.gameOver = Need<VisualTreeAsset>(s + "GameOver.uxml");
            a.victory = Need<VisualTreeAsset>(s + "Victory.uxml");
            a.credits = Need<VisualTreeAsset>(s + "Credits.uxml");
            a.howToPlay = Need<VisualTreeAsset>(s + "HowToPlay.uxml");
            a.hud = Need<VisualTreeAsset>(s + "Hud.uxml");
            a.dialogue = Need<VisualTreeAsset>(s + "Dialogue.uxml");
            a.debugMenu = Need<VisualTreeAsset>(s + "DebugMenu.uxml");
            EditorUtility.SetDirty(a);
            return a;
        }

        static T Need<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"JamKit: missing {typeof(T).Name} at {path}");
            return asset;
        }

        /// <summary>Marks every .ink under Assets/JamKit as a master file and reimports it, so the
        /// compiled story exists even in batch mode (the ink package normally detects this a frame later).</summary>
        public static void CompileInk()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("glob:\"*.ink\"", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".ink", StringComparison.OrdinalIgnoreCase) && !paths.Contains(path)) paths.Add(path);
            }
            // Files INCLUDEd by another file are parts of that story, not stories of their own.
            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
                foreach (var inc in InkImporter.GatherDependenciesFromSourceFile(path)) included.Add(inc.Replace(Path.DirectorySeparatorChar, '/'));
            // Includes first, so their masters compile against the current text.
            paths.Sort((a, b) => included.Contains(b).CompareTo(included.Contains(a)));
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                bool shouldBeMaster = !included.Contains(path);
                var so = new SerializedObject(importer);
                var master = so.FindProperty("isMasterFile");
                var file = AssetDatabase.LoadAssetAtPath<InkFile>(path);
                if (master != null && master.boolValue != shouldBeMaster)
                {
                    master.boolValue = shouldBeMaster;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    importer.SaveAndReimport();
                }
                else if (shouldBeMaster && (file == null || !file.isCompiled))
                {
                    importer.SaveAndReimport();
                }
                if (!shouldBeMaster) continue;
                file = AssetDatabase.LoadAssetAtPath<InkFile>(path);
                if (file == null || !file.isCompiled) Debug.LogError("JamKit: ink file didn't compile: " + path);
            }
        }

        static GameConfig EnsureGameConfig(AudioRegistry audio, List<Portrait> portraits)
        {
            var c = LoadOrCreate<GameConfig>(ResourcesDir + "/GameConfig.asset", out bool created);
            if (created)
            {
                c.gameTitle = "JamKit Sandbox";
                c.tagline = "A tiny game that tests the shell";
                c.firstGameplayScene = "Sandbox";
                c.credits = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Credits.txt");
                c.howToPlay = new List<HowToPlayPage>
                {
                    new HowToPlayPage
                    {
                        title = "Move",
                        body = "Move with WASD, the arrow keys or the left stick.\nSpace or (A) does a quick dash.",
                        image = MakeSprite("howto_move", 128, 128, Shapes.RoundedRect(128, 128, 14f, new Color(0.45f, 0.85f, 1f))),
                    },
                    new HowToPlayPage
                    {
                        title = "Collect",
                        body = "Grab all three coins to win.",
                        image = MakeSprite("howto_coin", 128, 128, Shapes.Circle(128, new Color(1f, 0.83f, 0.25f))),
                    },
                    new HowToPlayPage
                    {
                        title = "Stay safe",
                        body = "The red block hurts. You have 3 hearts, and after a hit you blink and can't be hurt for a moment.\nEscape or Start pauses.",
                        image = MakeSprite("howto_hazard", 128, 128, Shapes.RoundedRect(128, 128, 14f, new Color(1f, 0.3f, 0.3f))),
                    },
                };
            }
            // Fill references that are missing, without touching anything the team set.
            if (c.audio == null) c.audio = audio;
            if (c.credits == null) c.credits = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Credits.txt");
            if (c.portraits == null) c.portraits = new List<Portrait>();
            foreach (var p in portraits)
                if (!c.portraits.Contains(p) && c.FindPortrait(p.characterId) == null) c.portraits.Add(p);
            EditorUtility.SetDirty(c);
            return c;
        }

        // ---------- Scenes ----------

        /// <summary>A new empty scene with an orthographic camera (no AudioListener: GameRoot owns that).</summary>
        public static Scene NewScene(Color background, out Camera camera, float orthoSize = 5.6f)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            go.transform.position = new Vector3(0f, 0f, -10f);
            if (go.GetComponent<UniversalAdditionalCameraData>() == null) go.AddComponent<UniversalAdditionalCameraData>();
            return scene;
        }

        public static string Save(Scene scene, string name)
        {
            string path = ScenesDir + "/" + name + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        static string BuildBoot()
        {
            var scene = NewScene(Color.black, out _);
            var note = new GameObject("Boot (GameRoot creates itself, then loads Title)");
            note.transform.position = Vector3.zero;
            return Save(scene, "Boot");
        }

        static string BuildTitle()
        {
            var scene = NewScene(new Color(0.08f, 0.08f, 0.16f), out var cam);
            cam.gameObject.AddComponent<CameraShaker>();
            return Save(scene, "Title");
        }

        /// <summary>A sprite object (the shape sprites are white; tint with color).</summary>
        public static GameObject SpriteObject(Context ctx, string name, Sprite sprite, Color color, Vector3 position, Vector3 scale, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (ctx.flashMaterial != null) sr.sharedMaterial = ctx.flashMaterial;
            return go;
        }

        static void WriteBuildSettings(List<string> scenes)
        {
            var list = new List<EditorBuildSettingsScene>();
            foreach (var path in scenes.Distinct())
                if (File.Exists(path)) list.Add(new EditorBuildSettingsScene(path, true));
            // Keep scenes the team added, except Unity's template scene.
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (list.Any(s => s.path == existing.path)) continue;
                if (existing.path.EndsWith("/SampleScene.unity", StringComparison.Ordinal)) continue;
                if (!File.Exists(existing.path)) continue;
                list.Add(existing);
            }
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
