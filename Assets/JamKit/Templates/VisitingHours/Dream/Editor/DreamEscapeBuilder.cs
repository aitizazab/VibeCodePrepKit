using System.Collections.Generic;
using System.IO;
using JamKit.EditorTools;
using JamKit.Movement;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Builds Visiting Hours' first escape (docs/DREAM-ESCAPE.md): the dream version of Walt's street in real 3D
    /// from core <see cref="DreamEscapeCourse"/> data. Every piece becomes a root (with a <see cref="DreamPieceView"/>)
    /// holding its box (the collider) and its dressing: pastel houses with gable roofs and windows, picket
    /// fences, toy cars, the street-wide garage, porch, flat roofs, paper house fronts, the barbecue, trees,
    /// mailboxes. The route is lit warmer (soft gold pools on the ground), runnable walls carry a pastel gold
    /// stripe, ledges a gold lip. Plus the player (movement controller, camera feel, the lean-in hand, speed
    /// lines, sound), the static (a curved wall of pastel TV static and its wasps), the cast as cut-out
    /// billboards, the chase music, the golden-hour sky dome and the safe light at the end. Materials, the
    /// tuning asset, the GameConfig preset and the scene are written under Templates/VisitingHours/Dream.
    /// Runs as a step of "JamKit/Rebuild Shell Scenes"; "JamKit/Dream Escape/Rebuild Scene" rebuilds only this.
    /// </summary>
    public static class DreamEscapeBuilder
    {
        public const string Root = "Assets/JamKit/Templates/VisitingHours/Dream";
        public const string SceneName = "DreamEscape";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/DreamEscapeGameConfig.asset";
        /// <summary>The shared movement tuning (MovementTuning.SharedAssetPath, the Movement Lab's; 2026-10-11).</summary>
        public const string TuningPath = MovementTuning.SharedAssetPath;
        public const string MaterialsDir = Root + "/Materials";
        public const string GablePath = Root + "/Generated/DreamGable.asset";
        /// <summary>Nadia, the first-person body: the real 3D intruder in a billboard dream (the capsules stay without her).</summary>
        public const string NadiaDir = "Assets/JamKit/Templates/VisitingHours/Models/Nadia";
        public const string NadiaPath = NadiaDir + "/Nadia.fbx";
        public const string NadiaMaterialPath = MaterialsDir + "/Nadia_Body.mat";
        const string WakingShader = "JamKit/VisitingHours/Waking";

        // The golden hour.
        public static readonly Color Horizon = new Color(1f, 0.83f, 0.7f);
        public static readonly Color GoColor = new Color(1f, 0.8f, 0.46f);
        internal static readonly Vector4 SunDir = new Vector4(-0.35f, 0.42f, 0.84f, 0f);

        public static readonly Color[] Palette =
        {
            new Color(0.98f, 0.96f, 0.93f), // 0 picket white
            new Color(0.66f, 0.80f, 0.96f), // 1 baby blue
            new Color(0.99f, 0.90f, 0.62f), // 2 butter
            new Color(0.98f, 0.74f, 0.80f), // 3 rose
            new Color(0.70f, 0.92f, 0.80f), // 4 mint
            new Color(0.80f, 0.74f, 0.96f), // 5 lilac
            new Color(1.00f, 0.80f, 0.66f), // 6 peach
            new Color(0.58f, 0.84f, 0.86f), // 7 sky teal
            new Color(0.98f, 0.62f, 0.58f), // 8 coral
            new Color(0.82f, 0.92f, 0.62f), // 9 pistachio
            new Color(0.84f, 0.80f, 0.90f), // 10 lavender grey
            new Color(0.99f, 0.85f, 0.55f), // 11 apricot
        };

        static readonly Color[] RoofColors =
        {
            new Color(0.93f, 0.55f, 0.52f), new Color(0.6f, 0.52f, 0.78f), new Color(0.42f, 0.7f, 0.74f), new Color(0.95f, 0.66f, 0.42f),
        };

        static readonly Color Lawn = new Color(0.66f, 0.88f, 0.6f);
        static readonly Color Road = new Color(0.72f, 0.7f, 0.82f);
        static readonly Color Pavement = new Color(0.96f, 0.91f, 0.84f);
        static readonly Color WindowDark = new Color(0.4f, 0.36f, 0.6f);
        static readonly Color WindowWarm = new Color(1f, 0.88f, 0.6f);
        static readonly Color Plum = new Color(0.36f, 0.3f, 0.46f);

        [JamKitBuildStep(245)]
        static void Build(SceneBuilder.Context ctx)
        {
            string scene = BuildEverything(ctx);
            if (string.IsNullOrEmpty(scene)) return;
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Dream Escape/Rebuild Scene")]
        static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEverything(null);
        }

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { Root + "/Scenes", MaterialsDir, Root + "/Data", Root + "/Generated" }) EnsureFolder(d);
            EnsureTuning();
            MakePreset(ctx);
            _mats.Clear();
            AssetDatabase.SaveAssets();
            string path = BuildScene();
            Debug.Log("JAMKIT_DREAM_OK " + path);
            return path;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        internal static MovementTuning EnsureTuning()
        {
            var t = AssetDatabase.LoadAssetAtPath<MovementTuning>(TuningPath);
            if (t != null)
            {
                // An asset made before changed defaults (sprint went opt-in) takes them once.
                if (t.UpgradeDefaults()) EditorUtility.SetDirty(t);
                return t;
            }
            t = ScriptableObject.CreateInstance<MovementTuning>();
            t.UpgradeDefaults();
            AssetDatabase.CreateAsset(t, TuningPath);
            return t;
        }

        // ---------- Preset ----------

        /// <summary>The controls page of How to Play (sprint is opt-in: Shift / LB held, or L3 clicked).</summary>
        public const string MovePage = "WASD or the left stick to walk. Hold Left Shift (LB) to sprint, or click the left stick (L3) to sprint until you stop. Mouse or right stick to look.\nSpace (A) jumps, and jumps again in the air. Ctrl or C (hold B, or click R3) slides when you're running.";

        static void MakePreset(SceneBuilder.Context ctx)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath);
            if (existing != null)
            {
                // A preset made when sprint was automatic: its controls page now says how to sprint.
                foreach (var page in existing.howToPlay)
                    if (page != null && page.body != null && page.body.Contains("you sprint on your own")) { page.body = MovePage; EditorUtility.SetDirty(existing); }
                return;
            }
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Visiting Hours: the first escape";
            p.tagline = "The street noticed you. Run.";
            p.firstGameplayScene = SceneName;
            p.titleMusic = "music_title";
            p.gameMusic = "";
            p.showHowToPlayOnFirstRun = true;
            p.showPixelLookSetting = false;
            p.showTextSettings = false;
            p.showActionSettings = true;
            p.showBestTime = true;
            p.chooseDifficultyOnPlay = false;
            p.showControlLegend = false;
            p.credits = ctx != null && ctx.config != null ? ctx.config.credits : null;
            p.audio = ctx != null && ctx.config != null ? ctx.config.audio : null;
            p.howToPlay = new List<HowToPlayPage>
            {
                new HowToPlayPage
                {
                    title = "Run",
                    body = MovePage,
                },
                new HowToPlayPage
                {
                    title = "Keep ahead of the static",
                    body = "Follow the warm light. Walls with a gold stripe can be run along: jump at them along the wall and hold forward; jump off them quickly to keep your speed. Ledges pull you up by themselves.\nCaught or fallen, you wake at the last checkpoint, still running.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        // ---------- Materials ----------

        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        /// <summary>Forgets the materials made so far (a new build).</summary>
        internal static void ClearMaterials() => _mats.Clear();

        internal static Material Mat(string name, string shaderName)
        {
            if (_mats.TryGetValue(name, out var cached)) return cached;
            string path = MaterialsDir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Dream escape: shader " + shaderName + " not found.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            EditorUtility.SetDirty(mat);
            _mats[name] = mat;
            return mat;
        }

        /// <summary>The pastel doodle surface in a colour (one material per name).</summary>
        internal static Material Pastel(string name, Color color, float outline = 0.05f, float glow = 0f, float shape = 0f)
        {
            var m = Mat("Pastel_" + name + (shape > 0.5f ? "_Gable" : ""), "JamKit/Dream/Pastel");
            m.SetColor("_Color", color);
            m.SetFloat("_OutlineWidth", outline);
            m.SetFloat("_Emission", glow);
            m.SetFloat("_Shape", shape);
            m.SetVector("_SunDir", SunDir);
            return m;
        }

        internal static Material Glow(string name, Color color, float intensity, float mode, float pulse = 0.25f)
        {
            var m = Mat("Glow_" + name, "JamKit/Dream/Glow");
            m.SetColor("_Color", color);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Mode", mode);
            m.SetFloat("_Pulse", pulse);
            return m;
        }

        // ---------- Scene ----------

        static string BuildScene()
        {
            var dream = DreamEscapeCourse.Build();
            var course = dream.course;
            var scene = SceneBuilder.NewScene(Horizon, out var cam);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.82f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(1f, 0.86f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.56f, 0.5f, 0.7f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 230f;
            RenderSettings.skybox = null;
            var sun = new GameObject("Sun (golden hour)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.88f, 0.72f);
            sun.intensity = 1.05f;
            // Soft shadows: only Nadia casts one (the dream's own surfaces receive it as a lilac shade).
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.LookRotation(-new Vector3(SunDir.x, SunDir.y, SunDir.z).normalized);

            var body = BuildPlayer(cam, course.checkpoints[0]);

            var root = new GameObject("Dream street").transform;
            var gable = EnsureGableMesh();
            for (int i = 0; i < course.pieces.Count; i++) BuildPiece(dream, i, root, gable);
            BuildRouteLight(dream, root);
            var exit = BuildExit(dream, root);

            var cast = new GameObject("The cast (cut-outs)").transform;
            var placeholders = new List<string>();
            var real = new List<string>();
            foreach (var b in dream.billboards) BuildBillboard(b, cast, placeholders, real);

            var staticView = BuildStatic(course, placeholders, real);
            WireSpeedAnswer(cam, staticView);
            Debug.Log("JAMKIT_DREAM art: style-E files used: " + (real.Count > 0 ? string.Join(", ", real) : "none")
                + "; placeholders drawn: " + (placeholders.Count > 0 ? string.Join(", ", placeholders) : "none")
                + " (drop real files into " + DreamArt.ArtDir + " and rebuild)");

            // The chase music: the speed stems (MovementMusic) with the gap's tension (DreamChaseMusic) on top.
            var musicGo = new GameObject("Chase music (speed stems, placeholder)");
            var speedMusic = musicGo.AddComponent<MovementMusic>();
            speedMusic.stemSet = DreamChaseMusic.StemSet;
            speedMusic.playOnStart = false;
            var music = musicGo.AddComponent<DreamChaseMusic>();
            music.music = speedMusic;
            foreach (var n in new[] { "Pad", "Pulse", "Music box" })
            {
                var s = new GameObject(n);
                s.transform.SetParent(musicGo.transform, false);
                var src = s.AddComponent<AudioSource>();
                src.playOnAwake = false;
                if (n == "Pad") music.pad = src;
                else if (n == "Pulse") music.pulse = src;
                else music.box = src;
            }

            var sky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sky.name = "Sky dome (golden hour)";
            Object.DestroyImmediate(sky.GetComponent<Collider>());
            sky.transform.localScale = Vector3.one * 760f;
            var skyMat = Mat("DreamSky", "JamKit/Dream/Sky");
            skyMat.SetVector("_SunDir", SunDir);
            var sr = sky.GetComponent<MeshRenderer>();
            sr.sharedMaterial = skyMat;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;

            var game = new GameObject("Dream Escape").AddComponent<DreamEscapeGame>();
            game.player = body;
            game.staticView = staticView;
            game.music = music;
            var playerSound = body.GetComponent<MovementAudio>();
            if (playerSound != null) playerSound.music = speedMusic;
            game.courseRoot = root;
            game.skyDome = sky.transform;
            game.exitLight = exit;
            BuildUmbrella(dream.RainSpot, root, game, body);

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        internal static MovementController BuildPlayer(Camera cam, LabCheckpoint first)
        {
            // The body (yaw) > head (pitch, eye height, interpolation) > rig (bob, roll, dip) > camera:
            // docs/MOVEMENT.md "Using the movement in a scene", as the Movement Lab does.
            var playerGo = new GameObject("Nadia (player)");
            playerGo.transform.position = new Vector3(first.position.X, first.position.Y, first.position.Z);
            playerGo.AddComponent<CharacterController>();
            var body = playerGo.AddComponent<MovementController>();
            body.controller = playerGo.GetComponent<CharacterController>();
            body.tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(TuningPath);
            body.onlyMarkedWalls = true; // only the gold-striped walls run (RESEARCH-MOVEMENT 6.4)
            var head = new GameObject("Head").transform;
            head.SetParent(playerGo.transform, false);
            head.localPosition = new Vector3(0f, 1.5f, 0f);
            var rig = new GameObject("Rig").transform;
            rig.SetParent(head, false);
            cam.transform.SetParent(rig, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.orthographic = false;
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Horizon;
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = false;
            var shaker = cam.gameObject.AddComponent<CameraShaker>();
            shaker.strength = 0.08f;
            body.head = head;
            body.cam = cam;
            var feel = rig.gameObject.AddComponent<MovementCamera>();
            feel.body = body;
            feel.rig = rig;
            feel.cam = cam;
            // Nadia in the dream: her olive jacket and navy scrubs, drawn soft (PLACEHOLDER arms).
            var arms = cam.gameObject.AddComponent<MovementBody>();
            arms.body = body;
            arms.sleeveMaterial = Lit("Nadia_Sleeve", new Color(0.66f, 0.7f, 0.5f));
            arms.skinMaterial = Lit("Nadia_Skin", new Color(0.97f, 0.8f, 0.69f));
            arms.legMaterial = Lit("Nadia_Scrubs", new Color(0.4f, 0.46f, 0.68f));
            arms.shoeMaterial = Lit("Nadia_Shoes", new Color(0.97f, 0.95f, 0.92f));
            arms.feelCamera = feel;
            // Her real model in the waking world's material: lit by the dream's golden sun, a lilac sky and a pastel bounce.
            var nadiaMat = MovementBodySetup.EnsureMaterial(NadiaMaterialPath, WakingShader, MovementBodySetup.FindColorTexture(NadiaDir),
                new Color(0.40f, 0.37f, 0.46f), new Color(0.30f, 0.26f, 0.34f), 0f);
            if (nadiaMat != null)
            {
                if (nadiaMat.HasProperty("_RimColor")) nadiaMat.SetColor("_RimColor", new Color(1f, 0.84f, 0.62f));
                if (nadiaMat.HasProperty("_RimStrength")) nadiaMat.SetFloat("_RimStrength", 0.3f);
                if (nadiaMat.HasProperty("_ShadeColor")) nadiaMat.SetColor("_ShadeColor", new Color(0.74f, 0.68f, 0.9f));
            }
            MovementBodySetup.Wire(arms, body.transform, NadiaPath, nadiaMat, "Nadia (body)");
            var lines = cam.gameObject.AddComponent<SpeedLines>();
            lines.body = body;
            lines.material = Mat("SpeedLines", "JamKit/Movement/SpeedLines");
            // The speed visuals (docs/SPEED-FEEL.md): the boom, pastel confetti and petals, blooms at her feet, glowing
            // footprints, the edge blur. The dream answering (V7) is added once the static exists (WireSpeedAnswer).
            SpeedFeelSetup.Wire(cam, body, feel, lines, MaterialsDir, SpeedLook());
            var sound = playerGo.AddComponent<MovementAudio>();
            sound.body = body;
            return body;
        }

        /// <summary>The dream's speed particles: pastel confetti slips and petals drifting down, outlined in plum like the
        /// doodles; pastel blooms at her feet; warm gold footprints.</summary>
        static SpeedFeelSetup.Look SpeedLook() => new SpeedFeelSetup.Look
        {
            streakColors = new[] { Palette[1], Palette[2], Palette[3], Palette[4], Palette[5], Palette[6], Palette[9] },
            streakRim = Plum,
            streakShape = SpeedFeelSetup.Confetti,
            streakShapes = 2, // confetti and petals
            streakDrift = new Vector3(0.1f, -0.25f, 0f),
            streakJitter = 0.25f,
            puffColors = new[] { Palette[3], Palette[4], Palette[5], Palette[2] },
            puffRim = Plum,
            puffRimWidth = 0.18f,
            puffShape = SpeedFeelSetup.Flower,
            puffShapes = 1,
            puffSoft = 0f,
            footprint = new Color(1f, 0.82f, 0.5f, 0.9f),
        };

        /// <summary>V7, the dream answers speed (visual only): the grade, flowers opening beside the route, the static drawn back.</summary>
        static void WireSpeedAnswer(Camera cam, DreamStaticView staticView)
        {
            var answer = cam.gameObject.AddComponent<DreamSpeedAnswer>();
            answer.feel = cam.GetComponent<SpeedFeel>();
            answer.screen = cam.GetComponent<SpeedScreenFx>();
            answer.staticView = staticView;
            answer.flowerMaterial = SpeedFeelSetup.Motes(MaterialsDir, "SpeedFeel_Flowers", SpeedFeelSetup.Flower, 1, Plum, 0.14f, 0f, true, false);
            answer.flowerColors = new[] { Palette[3], Palette[5], Palette[1], Palette[6], Palette[0] };
        }

        static Material Lit(string name, Color color)
        {
            var mat = Mat(name, "Universal Render Pipeline/Lit");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            mat.color = color;
            return mat;
        }

        // ---------- Pieces ----------

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        internal static GameObject Box(string name, Transform parent, Vector3 worldCenter, Vector3 size, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = worldCenter;
            go.transform.localScale = size;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        /// <summary>A flat quad on a face: <paramref name="normal"/> is the way it faces.</summary>
        internal static GameObject Quad(string name, Transform parent, Vector3 worldCenter, Vector3 normal, float width, float height, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = worldCenter;
            // A quad shows its front toward -Z: point -Z along the normal.
            Vector3 up = Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up;
            go.transform.rotation = Quaternion.LookRotation(-normal, up);
            go.transform.localScale = new Vector3(width, height, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static void BuildPiece(DreamEscapeCourse dream, int index, Transform root, Mesh gable)
        {
            var p = dream.course.pieces[index];
            var look = dream.looks[index];
            int tint = dream.tints[index];
            Vector3 c = U(p.center), size = U(p.size);
            var go = new GameObject(p.name);
            go.transform.SetParent(root, false);
            go.transform.position = c;
            var t = go.transform;

            var view = go.AddComponent<DreamPieceView>();
            view.index = index;
            view.collapse = p.collapse;
            view.style = dream.collapseStyles[index];
            view.delay = dream.CollapseDelay(index);
            view.halfSize = size * 0.5f;
            view.foldAxis = look == DreamLook.SideFence ? Vector3.back : Vector3.right;
            view.peelSide = c.x < 0f ? -1f : 1f;

            Color tintColor = Palette[tint % Palette.Length];
            GameObject boxGo;
            if (p.kind == LabPieceKind.Ramp)
            {
                boxGo = Box("Box", t, c, size, Pastel("RoofTiles" + (tint % RoofColors.Length), RoofColors[tint % RoofColors.Length]), true);
                float len = p.size.Z, drop = p.rampTopStart - p.rampTopEnd;
                float angle = Mathf.Atan2(drop, len) * Mathf.Rad2Deg;
                const float thick = 0.8f;
                var rot = Quaternion.Euler(angle, 0f, 0f);
                Vector3 topMid = new Vector3(c.x, (p.rampTopStart + p.rampTopEnd) * 0.5f, c.z);
                boxGo.transform.SetPositionAndRotation(topMid - rot * Vector3.up * (thick * 0.5f), rot);
                boxGo.transform.localScale = new Vector3(size.x, thick, Mathf.Sqrt(len * len + drop * drop));
                // The house under the slope, down into the void.
                Box("House under the slope", t, new Vector3(c.x, (p.rampTopEnd - 0.4f + DreamEscapeCourse.VoidBottom) * 0.5f, c.z), new Vector3(size.x - 0.6f, p.rampTopEnd - 0.4f - DreamEscapeCourse.VoidBottom, size.z - 0.6f), Pastel("House" + (tint % Palette.Length), tintColor));
                Ledge(t, new Vector3(c.x, p.rampTopStart + 0.03f, p.Min.Z + 0.1f), size.x);
            }
            else
            {
                Material mat = BodyMaterial(look, tint, tintColor);
                boxGo = Box("Box", t, c, size, mat, p.solid);
            }
            if (p.solid)
            {
                var surface = boxGo.AddComponent<MovementSurface>();
                surface.runnable = p.runnable;
            }
            Dress(dream, index, p, look, tint, tintColor, t, boxGo, gable);
        }

        static Material BodyMaterial(DreamLook look, int tint, Color tintColor)
        {
            switch (look)
            {
                case DreamLook.Street: return Pastel("Lawn", Lawn, 0f);
                case DreamLook.SunkenLawn: return Pastel("SunkenLawn", new Color(0.58f, 0.8f, 0.66f), 0.05f);
                case DreamLook.Fence: case DreamLook.SideFence: return Pastel("Picket", Palette[0]);
                case DreamLook.GarageDoor: return Pastel("GarageDoor", new Color(0.99f, 0.93f, 0.74f));
                case DreamLook.GarageShell: return Pastel("Garage", Palette[2]);
                case DreamLook.Porch: case DreamLook.PorchRoof: return Pastel("Porch", new Color(0.98f, 0.95f, 0.9f));
                case DreamLook.ExitPorch: return Pastel("ExitPorch", new Color(1f, 0.93f, 0.8f), 0.05f, 0.25f);
                case DreamLook.GardenWall: return Pastel("GardenWall", new Color(0.97f, 0.72f, 0.68f));
                case DreamLook.Barbecue: return Pastel("Barbecue", Plum);
                case DreamLook.PicnicTable: return Pastel("Wood", new Color(0.86f, 0.68f, 0.56f));
                case DreamLook.Tree: return Pastel("Trunk", new Color(0.7f, 0.54f, 0.5f));
                case DreamLook.Mailbox: return Pastel("Mailbox", Palette[1]);
                case DreamLook.RoseBush: return Pastel("Leaves", new Color(0.5f, 0.78f, 0.56f));
                default: return Pastel("House" + (tint % Palette.Length), tintColor);
            }
        }

        static void Dress(DreamEscapeCourse dream, int index, LabPiece p, DreamLook look, int tint, Color tintColor, Transform t, GameObject boxGo, Mesh gable)
        {
            Vector3 c = U(p.center), size = U(p.size), min = U(p.Min), max = U(p.Max);
            var course = dream.course;
            switch (look)
            {
                case DreamLook.Street:
                {
                    float y = max.y;
                    var road = Pastel("Road", Road, 0.06f);
                    var pave = Pastel("Pavement", Pavement, 0.05f);
                    Quad("Road", t, new Vector3(0f, y + 0.02f, c.z), Vector3.up, 8f, size.z, road);
                    if (min.x <= -5.4f && max.x >= 5.4f)   // the 8 m garden path into the rain has no pavements over the void
                    {
                        Quad("Pavement (left)", t, new Vector3(-4.75f, y + 0.03f, c.z), Vector3.up, 1.5f, size.z, pave);
                        Quad("Pavement (right)", t, new Vector3(4.75f, y + 0.03f, c.z), Vector3.up, 1.5f, size.z, pave);
                    }
                    var dash = Pastel("Dash", new Color(1f, 0.95f, 0.7f), 0f);
                    for (float z = min.z + 2f; z < max.z - 1f; z += 5f)
                        Quad("Dash", t, new Vector3(0f, y + 0.04f, z), Vector3.up, 0.18f, 2f, dash);
                    break;
                }
                case DreamLook.House:
                    House(t, min, max, tint, tintColor, gable, true);
                    break;
                case DreamLook.Flat:
                    House(t, min, max, tint, tintColor, gable, true);
                    break;
                case DreamLook.Facade:
                case DreamLook.GardenWall:
                {
                    float laneX = course.LaneAt(c.z);
                    float faceSign = laneX < c.x ? -1f : 1f;               // the face toward the route
                    float faceX = faceSign < 0f ? min.x : max.x;
                    float floor = dream.FloorTopAt(course.LaneAt(min.z - 1.5f), min.z - 1.5f, max.y, out float top) ? top : max.y - 6f;
                    var normal = new Vector3(faceSign, 0f, 0f);
                    // The gold stripe: "this wall runs" (one go colour, used only for traversal).
                    var stripe = Pastel("GoStripe", GoColor, 0.03f, 0.75f);
                    Box("Run stripe", t, new Vector3(faceX + faceSign * 0.03f, floor + 1.45f, c.z), new Vector3(0.06f, 0.42f, size.z - 0.4f), stripe);
                    Box("Run stripe (thin)", t, new Vector3(faceX + faceSign * 0.03f, floor + 2.05f, c.z), new Vector3(0.06f, 0.1f, size.z - 0.4f), stripe);
                    if (look == DreamLook.Facade)
                    {
                        Windows(t, faceX + faceSign * 0.035f, normal, min.z, max.z, floor + 2.9f, max.y - 0.8f, tint);
                        var g = GableOn(t, gable, new Vector3(c.x, max.y + 1.6f, c.z), new Vector3(size.x, 3.2f, size.z), Pastel("House" + (tint % Palette.Length), tintColor, 0.05f, 0f, 1f));
                        g.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                        g.transform.localScale = new Vector3(size.z, 3.2f, size.x);
                    }
                    else
                    {
                        // Flower boxes along the top of the garden wall.
                        var flowers = Pastel("Flowers", new Color(1f, 0.62f, 0.74f), 0.04f);
                        for (float z = min.z + 1.5f; z < max.z - 1f; z += 3f)
                            Box("Flower box", t, new Vector3(c.x, max.y + 0.25f, z), new Vector3(size.x + 0.2f, 0.5f, 1.4f), flowers);
                    }
                    break;
                }
                case DreamLook.Fence:
                case DreamLook.SideFence:
                {
                    boxGo.GetComponent<MeshRenderer>().enabled = false;
                    var white = Pastel("Picket", Palette[0], 0.03f);
                    bool across = look == DreamLook.Fence;
                    float from = across ? min.x : min.z, to = across ? max.x : max.z;
                    float h = size.y;
                    for (float a = from + 0.15f; a < to - 0.05f; a += 0.3f)
                    {
                        var pos = across ? new Vector3(a, min.y + h * 0.5f + 0.05f, c.z) : new Vector3(c.x, min.y + h * 0.5f + 0.05f, a);
                        var s = across ? new Vector3(0.1f, h + 0.1f, 0.07f) : new Vector3(0.07f, h + 0.1f, 0.1f);
                        Box("Picket", t, pos, s, white);
                    }
                    for (int k = 0; k < 2; k++)
                    {
                        float y = min.y + h * (k == 0 ? 0.3f : 0.75f);
                        Box("Rail", t, new Vector3(c.x, y, c.z), across ? new Vector3(size.x, 0.08f, 0.05f) : new Vector3(0.05f, 0.08f, size.z), white);
                    }
                    break;
                }
                case DreamLook.Car:
                {
                    // A soft toy van (art pass 3, real meshes: DreamProps): a rounded body whose top is the roof you land
                    // on (the collider's top), a wrap of window glass, a cream roof cap, fat round tyres with white hubs.
                    boxGo.GetComponent<MeshRenderer>().enabled = false;
                    float street = max.y - DreamEscapeCourse.CarRoof;
                    const float lift = 0.28f;   // under the body: the wheels show
                    float bodyH = DreamEscapeCourse.CarRoof - lift;
                    var paint = DreamProps.MeshLook("Car" + (tint % RoofColors.Length), RoofColors[tint % RoofColors.Length], 0.03f);   // the roof colours: they stand out against the lilac void
                    var glass = DreamProps.MeshLook("CarGlass", WindowDark, 0.008f);
                    var tyre = DreamProps.MeshLook("Tyre", Plum, 0.01f);
                    var hub = DreamProps.MeshLook("CarHub", new Color(0.99f, 0.97f, 0.93f), 0.006f);
                    DreamProps.Show("Car body", DreamProps.RoundedBox("Toy van body", new Vector3(size.x, bodyH, size.z), 0.34f), paint, t,
                        new Vector3(c.x, street + lift + bodyH * 0.5f, c.z), Quaternion.identity);
                    DreamProps.Show("Roof", DreamProps.RoundedBox("Toy van roof", new Vector3(size.x - 0.25f, 0.1f, size.z - 0.6f), 0.05f),
                        DreamProps.MeshLook("CarRoofCap", new Color(0.99f, 0.96f, 0.9f), 0.008f), t, new Vector3(c.x, max.y - 0.035f, c.z), Quaternion.identity);
                    DreamProps.Show("Windows", DreamProps.RoundedBox("Toy van windows", new Vector3(size.x + 0.04f, 0.34f, size.z - 1.3f), 0.1f), glass, t,
                        new Vector3(c.x, max.y - 0.33f, c.z + 0.15f), Quaternion.identity);
                    DreamProps.Show("Windscreen", DreamProps.RoundedBox("Toy van windscreen", new Vector3(size.x - 0.5f, 0.32f, 0.08f), 0.03f), glass, t,
                        new Vector3(c.x, max.y - 0.33f, min.z + 0.01f), Quaternion.identity);
                    var tyreMesh = DreamProps.RoundedBox("Toy tyre", new Vector3(0.26f, 0.64f, 0.64f), 0.3f);
                    var hubMesh = DreamProps.RoundedBox("Toy hub", new Vector3(0.28f, 0.26f, 0.26f), 0.12f);
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var at = new Vector3(c.x + sx * (size.x * 0.5f - 0.08f), street + 0.32f, c.z + sz * (size.z * 0.5f - 0.85f));
                            DreamProps.Show("Wheel", tyreMesh, tyre, t, at, Quaternion.identity);
                            DreamProps.Show("Hub", hubMesh, hub, t, at + new Vector3(sx * 0.02f, 0f, 0f), Quaternion.identity);
                        }
                    var lamp = Pastel("Headlamp", new Color(1f, 0.96f, 0.8f), 0.02f, 0.9f);
                    for (int sx = -1; sx <= 1; sx += 2)
                        Box("Headlamp", t, new Vector3(c.x + sx * (size.x * 0.32f), street + 0.45f, min.z - 0.02f), new Vector3(0.36f, 0.2f, 0.06f), lamp);
                    break;
                }
                case DreamLook.GarageDoor:
                {
                    // Panel lines, and the warm glow under the half-open door: "slide here".
                    var seam = Pastel("DoorSeam", new Color(0.9f, 0.8f, 0.6f), 0f);
                    for (float y = min.y + 0.8f; y < max.y; y += 0.8f)
                        Box("Panel seam", t, new Vector3(c.x, y, min.z - 0.01f), new Vector3(size.x, 0.05f, 0.02f), seam);
                    float floor = min.y - 1.15f;
                    Quad("Glow under the door", t, new Vector3(0f, floor + 0.05f, c.z - 1.2f), Vector3.up, 9f, 3f, Glow("UnderDoor", GoColor, 0.9f, 0f, 0.35f));
                    Box("Door lip", t, new Vector3(c.x, min.y - 0.04f, min.z - 0.02f), new Vector3(size.x, 0.08f, 0.05f), Pastel("GoStripe", GoColor, 0.03f, 0.75f));
                    break;
                }
                case DreamLook.GarageShell:
                {
                    if (p.name != "Garage roof") break;
                    // Inside Walt's garage: bikes hanging from the roof, and a warm bulb or two.
                    // Real bicycles (art pass 3, DreamProps): tube frames with spokes, round tyres, each its own colour.
                    var wheel = Pastel("Tyre", Plum, 0.02f);
                    var tyreMat = DreamProps.MeshLook("Tyre", Plum, 0.008f);
                    var bikeColors = new[] { new Color(0.93f, 0.5f, 0.55f), new Color(0.45f, 0.72f, 0.86f), new Color(0.98f, 0.78f, 0.4f),
                        new Color(0.6f, 0.82f, 0.6f), new Color(0.78f, 0.6f, 0.9f), new Color(0.98f, 0.62f, 0.45f) };
                    var bikeFrame = DreamProps.BikeFrame();
                    var bikeTyre = DreamProps.Ring("Bike tyre", 0.33f, 0.028f);
                    for (int k = 0; k < 6; k++)
                    {
                        float x = -11f + k * 4.4f + (k >= 3 ? 1f : 0f);
                        if (Mathf.Abs(x) < 2.5f) continue;
                        float z = min.z + 4f + (k % 3) * 3.5f;
                        float y = min.y - 1.3f;
                        var bike = new GameObject("Bike").transform;
                        bike.SetParent(t, false);
                        bike.position = new Vector3(x, y, z);
                        DreamProps.Show("Bike frame", bikeFrame, DreamProps.MeshLook("BikeFrame" + k, bikeColors[k], 0.006f), bike, Vector3.zero, Quaternion.identity);
                        DreamProps.Show("Bike wheel", bikeTyre, tyreMat, bike, new Vector3(0f, 0f, -0.55f), Quaternion.identity);
                        DreamProps.Show("Bike wheel", bikeTyre, tyreMat, bike, new Vector3(0f, 0f, 0.55f), Quaternion.identity);
                        Box("Hook", t, new Vector3(x, (y + min.y) * 0.5f + 0.3f, z), new Vector3(0.03f, min.y - y - 0.2f, 0.03f), wheel);
                    }
                    var bulb = Glow("Bulb", new Color(1f, 0.85f, 0.6f), 1.2f, 0f, 0.1f);
                    for (float z = min.z + 3f; z < max.z; z += 6f)
                        Quad("Bulb", t, new Vector3(0f, min.y - 0.05f, z), Vector3.down, 2.4f, 2.4f, bulb);
                    break;
                }
                case DreamLook.Porch:
                case DreamLook.PorchRoof:
                case DreamLook.Roof:
                {
                    // A pastel house down into the void, a deck on top, a gold lip on the ledge you climb or land on.
                    boxGo.GetComponent<MeshRenderer>().sharedMaterial = Pastel("House" + (tint % Palette.Length), tintColor);
                    var deck = look == DreamLook.Roof ? Pastel("RoofDeck" + (tint % RoofColors.Length), Color.Lerp(RoofColors[tint % RoofColors.Length], Color.white, 0.35f), 0.05f)
                        : Pastel("Porch", new Color(0.98f, 0.95f, 0.9f));
                    Quad("Deck", t, new Vector3(c.x, max.y + 0.02f, c.z), Vector3.up, size.x - 0.1f, size.z - 0.1f, deck);
                    Ledge(t, new Vector3(c.x, max.y + 0.03f, min.z + 0.1f), size.x);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Windows(t, c.x + side * (size.x * 0.5f + 0.02f), new Vector3(side, 0f, 0f), min.z, max.z, Mathf.Max(min.y + 1f, max.y - 12f), max.y - 1.2f, tint);
                        Box("Parapet", t, new Vector3(c.x + side * (size.x * 0.5f - 0.15f), max.y + 0.2f, c.z), new Vector3(0.3f, 0.4f, size.z), Pastel("Parapet", Palette[0], 0.04f));
                    }
                    if (look == DreamLook.Roof)
                        Box("Chimney", t, new Vector3(c.x - size.x * 0.5f + 1.2f, max.y + 1.1f, c.z + size.z * 0.2f), new Vector3(0.9f, 2.2f, 0.9f), Pastel("Chimney", RoofColors[(tint + 1) % RoofColors.Length]));
                    else
                        for (float x = min.x + 0.4f; x <= max.x; x += 2f)
                            if (Mathf.Abs(x) > 2.4f) Box("Post", t, new Vector3(x, max.y + 0.5f, min.z + 0.2f), new Vector3(0.14f, 1f, 0.14f), Pastel("Picket", Palette[0], 0.03f));
                    break;
                }
                case DreamLook.ExitPorch:
                {
                    var deck = Pastel("ExitDeck", new Color(1f, 0.95f, 0.84f), 0.05f, 0.35f);
                    Quad("Deck", t, new Vector3(c.x, max.y + 0.02f, c.z), Vector3.up, size.x - 0.1f, size.z - 0.1f, deck);
                    Ledge(t, new Vector3(c.x, max.y + 0.03f, min.z + 0.1f), size.x);
                    break;
                }
                case DreamLook.Tree:
                {
                    var leaves = Pastel("Canopy", new Color(0.6f, 0.86f, 0.64f), 0.06f);
                    for (int k = 0; k < 3; k++)
                    {
                        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        ball.name = "Canopy";
                        Object.DestroyImmediate(ball.GetComponent<Collider>());
                        ball.transform.SetParent(t, false);
                        ball.transform.position = new Vector3(c.x + (k - 1) * 0.9f, max.y - 0.8f + (k == 1 ? 0.9f : 0f), c.z + (k == 1 ? 0.4f : -0.3f));
                        ball.transform.localScale = Vector3.one * (k == 1 ? 2.8f : 2.2f);
                        var r = ball.GetComponent<MeshRenderer>();
                        r.sharedMaterial = Pastel("CanopyBall", new Color(0.6f, 0.86f, 0.64f), 0f);
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    break;
                }
                case DreamLook.Mailbox:
                    boxGo.transform.localScale = new Vector3(0.12f, size.y, 0.12f);
                    boxGo.transform.position = new Vector3(c.x, min.y + size.y * 0.5f, c.z);
                    Box("Mailbox", t, new Vector3(c.x, max.y, c.z), new Vector3(0.4f, 0.32f, 0.6f), Pastel("Mailbox", Palette[1]));
                    break;
                case DreamLook.RoseBush:
                {
                    var rose = Pastel("Rose", new Color(0.95f, 0.38f, 0.48f), 0.02f);
                    for (float z = min.z + 0.4f; z < max.z; z += 0.9f)
                        Box("Rose", t, new Vector3(c.x, max.y + 0.05f, z), new Vector3(0.22f, 0.2f, 0.22f), rose);
                    break;
                }
                case DreamLook.Barbecue:
                {
                    var smoke = Pastel("Smoke", new Color(0.97f, 0.96f, 1f), 0f, 0.6f);
                    for (int k = 0; k < 3; k++)
                    {
                        var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        puff.name = "Smoke puff";
                        Object.DestroyImmediate(puff.GetComponent<Collider>());
                        puff.transform.SetParent(t, false);
                        puff.transform.position = new Vector3(c.x + k * 0.2f, max.y + 0.6f + k * 0.7f, c.z + k * 0.15f);
                        puff.transform.localScale = Vector3.one * (0.5f + k * 0.25f);
                        puff.GetComponent<MeshRenderer>().sharedMaterial = smoke;
                    }
                    break;
                }
            }
        }

        /// <summary>A gold lip along the top front edge of a ledge (mantle here, land here).</summary>
        static void Ledge(Transform t, Vector3 at, float width) =>
            Box("Ledge lip", t, at, new Vector3(width - 0.4f, 0.06f, 0.2f), Pastel("GoStripe", GoColor, 0.03f, 0.75f));

        internal static GameObject GableOn(Transform t, Mesh gable, Vector3 center, Vector3 size, Material mat)
        {
            var go = new GameObject("Gable roof");
            go.transform.SetParent(t, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = gable;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A house's dressing: windows on the face toward the street (and down into the void for a
        /// pillar), a door at street level, a gable roof (or a flat's painted gable).</summary>
        static void House(Transform t, Vector3 min, Vector3 max, int tint, Color tintColor, Mesh gable, bool roof)
        {
            Vector3 c = (min + max) * 0.5f, size = max - min;
            float faceSign = c.x < 0f ? 1f : -1f;   // houses look at the street (x = 0)
            float faceX = faceSign > 0f ? max.x : min.x;
            float bottom = Mathf.Max(min.y, max.y - 14f);
            Windows(t, faceX + faceSign * 0.03f, new Vector3(faceSign, 0f, 0f), min.z, max.z, bottom + 1.2f, max.y - 0.9f, tint);
            if (min.y > -3f && size.z > 6f)
            {
                // The front door (art pass 3): a white door in a frame, a step, and a little gable awning over it.
                Quad("Door", t, new Vector3(faceX + faceSign * 0.04f, min.y + 1.1f, c.z + 0.3f), new Vector3(faceSign, 0f, 0f), 1.1f, 2.1f, Pastel("Door", new Color(0.99f, 0.97f, 0.94f), 0.04f));
                var trim = Pastel("DoorTrim" + (tint % RoofColors.Length), RoofColors[tint % RoofColors.Length], 0.03f);
                foreach (float dz in new[] { -0.65f, 0.65f })
                    Box("Door frame", t, new Vector3(faceX + faceSign * 0.06f, min.y + 1.12f, c.z + 0.3f + dz), new Vector3(0.12f, 2.24f, 0.16f), trim);
                Box("Door frame (top)", t, new Vector3(faceX + faceSign * 0.06f, min.y + 2.26f, c.z + 0.3f), new Vector3(0.12f, 0.16f, 1.46f), trim);
                Box("Step", t, new Vector3(faceX + faceSign * 0.35f, min.y + 0.08f, c.z + 0.3f), new Vector3(0.6f, 0.16f, 1.6f), Pastel("Step", Pavement, 0.03f));
                var awning = GableOn(t, gable, new Vector3(faceX + faceSign * 0.45f, min.y + 2.62f, c.z + 0.3f), Vector3.one,
                    Pastel("RoofTiles" + (tint % RoofColors.Length), RoofColors[tint % RoofColors.Length], 0.05f, 0f, 1f));
                awning.name = "Door awning";
                awning.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                awning.transform.localScale = new Vector3(1.8f, 0.55f, 0.9f);
            }
            if (!roof) return;
            var roofMat = Pastel("RoofTiles" + (tint % RoofColors.Length), RoofColors[tint % RoofColors.Length], 0.05f, 0f, 1f);
            if (size.x > 1.5f)
            {
                var g = GableOn(t, gable, new Vector3(c.x, max.y + 1.25f, c.z), new Vector3(size.x + 0.8f, 2.5f, size.z + 0.6f), roofMat);
                g.name = "Gable roof";
            }
            else
            {
                // A flat (a paper house front): a painted gable on top, turned to face the street.
                var g = GableOn(t, gable, new Vector3(c.x, max.y + 1.6f, c.z), new Vector3(size.z, 3.2f, size.x), Pastel("House" + (tint % Palette.Length), tintColor, 0.05f, 0f, 1f));
                g.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }

        static void Windows(Transform t, float x, Vector3 normal, float z0, float z1, float y0, float y1, int tint)
        {
            var dark = Pastel("WindowDark", WindowDark, 0.07f);
            var warm = Pastel("WindowWarm", WindowWarm, 0.07f, 0.85f);
            var shutter = Pastel("Shutter" + (tint % RoofColors.Length), Color.Lerp(RoofColors[tint % RoofColors.Length], Color.white, 0.25f), 0.04f);
            var sill = Pastel("Sill", Palette[0], 0.03f);
            int k = tint;
            for (float y = y1 - 0.8f; y >= y0; y -= 2.6f)
                for (float z = z0 + 1.6f; z <= z1 - 1.4f; z += 2.8f)
                {
                    k = (k * 7 + 3) % 11;
                    Quad("Window", t, new Vector3(x, y, z), normal, 1.1f, 1.2f, k == 2 || k == 7 ? warm : dark);
                    // Shutters either side and a white sill (art pass 3), on the top two rows only: the rows below run
                    // down into the void, where nobody looks closely (and the scene stays light for WebGL).
                    if (y < y1 - 4.5f) continue;
                    foreach (float dz in new[] { -0.78f, 0.78f })
                        Quad("Shutter", t, new Vector3(x, y, z + dz), normal, 0.4f, 1.2f, shutter);
                    Box("Sill", t, new Vector3(x + normal.x * 0.05f, y - 0.66f, z), new Vector3(0.12f, 0.08f, 1.3f), sill);
                }
        }

        internal static Mesh EnsureGableMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GablePath);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "DreamGable" };
            // A prism in -0.5..0.5: the ridge along Z at the top, eaves at x = +-0.5 at the bottom.
            Vector3 l0 = new Vector3(-0.5f, -0.5f, -0.5f), r0 = new Vector3(0.5f, -0.5f, -0.5f), t0 = new Vector3(0f, 0.5f, -0.5f);
            Vector3 l1 = new Vector3(-0.5f, -0.5f, 0.5f), r1 = new Vector3(0.5f, -0.5f, 0.5f), t1 = new Vector3(0f, 0.5f, 0.5f);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            void Face(Vector3[] f)
            {
                Vector3 n = Vector3.Cross(f[1] - f[0], f[2] - f[0]).normalized;
                int b = verts.Count;
                foreach (var v in f) { verts.Add(v); norms.Add(n); }
                for (int i = 1; i + 1 < f.Length; i++) { tris.Add(b); tris.Add(b + i); tris.Add(b + i + 1); }
            }
            Face(new[] { l0, t0, r0 });           // front gable (faces -Z)
            Face(new[] { r1, t1, l1 });           // back gable (faces +Z)
            Face(new[] { l0, l1, t1, t0 });       // left slope
            Face(new[] { r0, t0, t1, r1 });       // right slope
            Face(new[] { l0, r0, r1, l1 });       // bottom
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, GablePath);
            return mesh;
        }

        // ---------- The route's light, the exit, the cast, the static ----------

        /// <summary>Soft gold pools of light along the route on whatever you run on: the dream's warmth shows the way.</summary>
        static void BuildRouteLight(DreamEscapeCourse dream, Transform root)
        {
            var course = dream.course;
            var parent = new GameObject("Route light (warm pools)").transform;
            parent.SetParent(root, false);
            var mat = Glow("RoutePool", GoColor, 0.32f, 0f, 0.3f);
            for (float s = 4f; s < course.finishS; s += 4.5f)
            {
                // Along a runnable wall the route is the wall (its gold stripe), not the ground below it.
                bool onWall = false;
                foreach (var p in course.pieces)
                    if (p.runnable && s >= p.Min.Z - 3f && s <= p.Max.Z + 1f) { onWall = true; break; }
                if (onWall) continue;
                float x = course.LaneAt(s);
                if (!dream.FloorTopAt(x, s, 100f, out float top)) continue;
                Quad("Pool", parent, new Vector3(x, top + 0.05f, s), Vector3.up, 3.2f, 3.2f, mat);
            }
        }

        /// <summary>The front door into the rain: the closed umbrella leaning by the door with its gold glow (the dream
        /// tool takes it), the open umbrella over her head (shown once she has it), the rain over the path, the tool's
        /// beam, and the <see cref="DreamUmbrella"/> that runs it (docs/DREAM-ESCAPE.md "The umbrella and the rain").</summary>
        internal static DreamUmbrella BuildUmbrella(DreamRainSpot spot, Transform root, MonoBehaviour host, MovementController body)
        {
            var parent = new GameObject("The front door (umbrella and rain)").transform;
            parent.SetParent(root, false);
            var at = U(spot.umbrella);
            var cloth = Pastel("Umbrella", new Color(0.98f, 0.55f, 0.62f), 0.03f, 0.15f);
            var wood = Pastel("UmbrellaHandle", new Color(0.55f, 0.36f, 0.3f), 0.02f);

            // Closed, leaning on the house front beside the door: a furled canopy on a shaft, a hooked handle.
            var prop = new GameObject("Umbrella (closed, by the door)").transform;
            prop.SetParent(parent, false);
            prop.position = at;
            prop.rotation = Quaternion.Euler(-12f, 0f, 8f);
            // Real meshes (DreamProps, art pass 2): furled folds around the shaft, a ferrule below, a J-hook handle.
            var clothMesh = DreamProps.MeshLook("UmbrellaCloth", new Color(0.98f, 0.55f, 0.62f), 0.008f);
            var woodMesh = DreamProps.MeshLook("UmbrellaHandle", new Color(0.55f, 0.36f, 0.3f), 0.004f);
            DreamProps.Show("Furled canopy", DreamProps.FurledCanopy(), clothMesh, prop, new Vector3(0f, -0.42f, 0f), Quaternion.identity);
            DreamProps.Show("Shaft and handle", DreamProps.ShaftWithHook(1.02f), woodMesh, prop, new Vector3(0f, -0.47f, 0f), Quaternion.identity);

            // The highlight: a breathing gold glow around it and a gold pool at its foot ("use this").
            // A soft camera-facing glow (House round 2: the hard translucent sphere read as a flat disc round the umbrella).
            var glow = Part("Umbrella glow (highlight)", PrimitiveType.Quad, parent, Vector3.zero, Vector3.one * 1.5f, HouseFurnishing.GlowMaterial("Umbrella", GoColor, 0.6f));
            glow.transform.position = at;
            Quad("Umbrella pool", glow.transform, at + new Vector3(0f, -0.5f, 0f), Vector3.up, 1.6f, 1.6f, Glow("UmbrellaPool", GoColor, 1f, 0f, 0.3f));

            // Open, over her head: a shallow dome on the body (it turns with her, not with her look); its rim shows in the
            // top of the view, and looking up shows its underside.
            var canopy = new GameObject("Umbrella (open, over her)");
            canopy.transform.SetParent(body.transform, false);
            canopy.transform.localPosition = new Vector3(0.05f, body.Def.eyeHeight + 0.5f, 0.12f);
            DreamProps.Show("Canopy", DreamProps.OpenCanopy(0.95f, 0.42f), clothMesh, canopy.transform, new Vector3(0f, -0.2f, 0f), Quaternion.identity);
            DreamProps.Show("Shaft", DreamProps.ShaftWithHook(1.0f), woodMesh, canopy.transform, new Vector3(0f, 0.22f, 0f), Quaternion.Euler(180f, 0f, 0f));
            foreach (var t in canopy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            canopy.SetActive(false);

            // The rain: pale streaks falling over the path past the door (and a little over the porch's end).
            var rainGo = new GameObject("Rain (past the door)");
            rainGo.transform.SetParent(parent, false);
            float z0 = spot.rainStartS - 6f, z1 = spot.rainEndS + 8f;
            rainGo.transform.position = new Vector3(0f, spot.floorY + 12f, (z0 + z1) * 0.5f);
            rainGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ps = rainGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 1f;
            main.startSpeed = 15f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.8f, 0.86f, 1f, 0.55f);
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 1800f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, z1 - z0, 0.1f);
            var pr = rainGo.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.04f;
            pr.lengthScale = 2f;
            pr.sharedMaterial = Glow("Rain", new Color(0.82f, 0.88f, 1f), 0.6f, 0f, 0f);
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;

            // The rain path's dressing (art pass 3): puddles catching the light, and two lamp posts glowing at the end.
            var puddle = Pastel("Puddle", new Color(0.72f, 0.76f, 0.95f), 0f, 0.35f);
            var disc = DreamProps.Disc("Puddle");
            int n = 0;
            for (float z = spot.rainStartS + 1.5f; z < spot.rainEndS - 1f; z += 3.1f, n++)
            {
                float x = (n % 2 == 0 ? -1f : 1f) * (0.5f + (n % 3) * 0.35f);
                var pd = DreamProps.Show("Puddle", disc, puddle, parent, Vector3.zero, Quaternion.identity);
                pd.transform.position = new Vector3(x, spot.floorY + 0.045f, z);   // above the street's road quad (+0.02)
                pd.transform.localScale = new Vector3(1.1f + (n % 3) * 0.3f, 1f, 0.7f + (n % 2) * 0.25f);
            }
            var post = DreamProps.MeshLook("LampPost", Plum, 0.008f);
            var lampGlow = Glow("LampWarm", new Color(1f, 0.86f, 0.6f), 1.1f, 0f, 0.15f);
            var postMesh = DreamProps.ShaftWithHook(2.9f, 0.05f, 0.35f);
            for (int side = -1; side <= 1; side += 2)
            {
                var lamp = new GameObject("Lamp post").transform;
                lamp.SetParent(parent, false);
                lamp.position = new Vector3(side * 2.4f, spot.floorY, spot.rainEndS - 2f);
                lamp.rotation = Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f);   // the hooks lean over the path
                DreamProps.Show("Post", postMesh, post, lamp, Vector3.zero, Quaternion.identity);
                var lantern = Part("Lantern", PrimitiveType.Cube, lamp, Vector3.zero, new Vector3(0.3f, 0.42f, 0.3f), Pastel("Lantern", new Color(1f, 0.88f, 0.62f), 0.03f, 0.9f));
                lantern.transform.localPosition = new Vector3(0.68f, 2.9f - 0.36f, 0f);
                var bulb = Part("Lamp", PrimitiveType.Sphere, lamp, Vector3.zero, Vector3.one * 0.9f, lampGlow);
                bulb.transform.localPosition = new Vector3(0.68f, 2.9f - 0.36f, 0f);
            }

            // The dream tool's beam (the same pulse of light as in the Movement Lab).
            var beam = Part("Dream tool beam", PrimitiveType.Cube, parent, Vector3.zero, Vector3.one, Glow("ToolBeam", new Color(1f, 0.95f, 0.8f), 1.2f, 0f, 0f));
            beam.SetActive(false);

            var u = host.gameObject.AddComponent<DreamUmbrella>();
            u.host = host;
            u.prop = prop;
            u.glow = glow.transform;
            u.canopy = canopy;
            u.rainFx = ps;
            u.beam = beam.transform;
            u.beamRenderer = beam.GetComponent<MeshRenderer>();
            return u;
        }

        /// <summary>A primitive with no collider and no shadows, placed in its parent's space.</summary>
        internal static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        static Light BuildExit(DreamEscapeCourse dream, Transform root)
        {
            var e = dream.exitLight;
            var at = new Vector3(e.X, e.Y, e.Z);
            var parent = new GameObject("The safe light (exit)").transform;
            parent.SetParent(root, false);
            parent.position = at;
            // A doorway pouring light, on a house front at the end of the porch.
            var front = Pastel("ExitHouse", Palette[6], 0.05f);
            Box("House front (left)", parent, at + new Vector3(-4f, 3.5f, 9.6f), new Vector3(4f, 7f, 0.5f), front);
            Box("House front (right)", parent, at + new Vector3(4f, 3.5f, 9.6f), new Vector3(4f, 7f, 0.5f), front);
            Box("Lintel", parent, at + new Vector3(0f, 5.8f, 9.6f), new Vector3(4f, 2.4f, 0.5f), front);
            Quad("Doorway light", parent, at + new Vector3(0f, 2.3f, 9.4f), Vector3.back, 4f, 4.6f, Pastel("Doorway", new Color(1f, 0.97f, 0.86f), 0f, 1f));
            var pillarMat = Glow("Pillar", new Color(1f, 0.86f, 0.6f), 0.9f, 1f, 0.2f);
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar of light";
            Object.DestroyImmediate(pillar.GetComponent<Collider>());
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = at + new Vector3(0f, 9f, 0f);
            pillar.transform.localScale = new Vector3(5f, 9f, 5f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
            Quad("Light pool", parent, at + new Vector3(0f, 0.06f, 0f), Vector3.up, 9f, 9f, Glow("ExitPool", GoColor, 1.1f, 0f, 0.2f));
            var light = new GameObject("Warm light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = at + new Vector3(0f, 3f, 2f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.range = 18f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            return light;
        }

        static void BuildBillboard(DreamBillboard b, Transform parent, List<string> placeholders, List<string> real)
        {
            string name = DreamArt.NameOf(b.who);
            var tex = DreamArt.Get(name, out bool placeholder);
            (placeholder ? placeholders : real).Add(name);
            var mat = Mat("Cutout_" + name, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", tex);
            float aspect = tex != null && tex.height > 0 ? tex.width / (float)tex.height : 0.5f;
            var go = new GameObject(b.name + " (cut-out)");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(b.position.X, b.position.Y, b.position.Z);
            var bb = go.AddComponent<DreamCutoutView>();
            bb.restYaw = b.position.X < 0f ? 90f : -90f; // facing the street
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "Card";
            Object.DestroyImmediate(card.GetComponent<Collider>());
            card.transform.SetParent(go.transform, false);
            card.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the quad's front toward the billboard's +Z
            card.transform.localScale = new Vector3(b.height * aspect, b.height, 1f);
            card.transform.localPosition = new Vector3(0f, b.height * 0.5f, 0f);
            var r = card.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            bb.card = card.transform;
            if (b.who == DreamCast.Dog) { bb.noticeDelay = 0.15f; bb.turnSpeed = 220f; }
        }

        static DreamStaticView BuildStatic(LabCourse course, List<string> placeholders, List<string> real)
        {
            var go = new GameObject("The static (pursuer)");
            go.transform.position = new Vector3(0f, 0f, course.chaseStartS - 14f);
            var view = go.AddComponent<DreamStaticView>();
            var wallMat = Mat("DreamStatic", "JamKit/Dream/Static");
            // A curved wall: an arc of radius 14 centred 14 m ahead, so its middle stands at the pursuer's
            // distance and its sides reach forward around the street.
            const float radius = 14f, height = 17f;
            float[] angles = { -80f, -48f, -16f, 16f, 48f, 80f };
            var wall = new List<Renderer>();
            var centre = new Vector3(0f, 0f, radius);
            for (int i = 0; i + 1 < angles.Length; i++)
            {
                Vector3 a = centre + new Vector3(Mathf.Sin(angles[i] * Mathf.Deg2Rad), 0f, -Mathf.Cos(angles[i] * Mathf.Deg2Rad)) * radius;
                Vector3 b = centre + new Vector3(Mathf.Sin(angles[i + 1] * Mathf.Deg2Rad), 0f, -Mathf.Cos(angles[i + 1] * Mathf.Deg2Rad)) * radius;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Static wall " + (i + 1);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(go.transform, false);
                Vector3 mid = (a + b) * 0.5f;
                q.transform.localPosition = mid + Vector3.up * (height * 0.5f);
                q.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
                q.transform.localScale = new Vector3(Vector3.Distance(a, b) + 0.6f, height, 1f);
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = wallMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                wall.Add(r);
            }
            view.wall = wall.ToArray();

            var mask = DreamArt.Get(DreamArt.WaspMask, out bool placeholder);
            (placeholder ? placeholders : real).Add(DreamArt.WaspMask);
            var waspMat = Mat("DreamWasp", "JamKit/Dream/Wasp");
            waspMat.SetTexture("_MainTex", mask);
            var swarm = new GameObject("Wasps").transform;
            swarm.SetParent(go.transform, false);
            const int count = 46;
            view.scouts = 6;
            var wasps = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Quad);
                w.name = i < count - view.scouts ? "Wasp " + (i + 1) : "Scout " + (i - (count - view.scouts) + 1);
                Object.DestroyImmediate(w.GetComponent<Collider>());
                w.transform.SetParent(swarm, false);
                w.transform.localPosition = new Vector3((i % 9 - 4) * 1.5f, 1f + i % 5, 2f);
                w.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                var r = w.GetComponent<MeshRenderer>();
                r.sharedMaterial = waspMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                wasps[i] = w.transform;
            }
            view.wasps = wasps;
            view.hiss = go.AddComponent<AudioSource>();
            view.hiss.playOnAwake = false;
            var buzzGo = new GameObject("Buzz (near you)");
            buzzGo.transform.SetParent(go.transform, false);
            view.buzz = buzzGo.AddComponent<AudioSource>();
            view.buzz.playOnAwake = false;
            return view;
        }
    }
}
