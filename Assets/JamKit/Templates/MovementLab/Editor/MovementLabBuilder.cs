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

namespace JamKit.MovementLab.EditorTools
{
    /// <summary>
    /// Builds the Movement Lab: its materials (a world-space grid in a colour per kind of surface: grey
    /// floors, blue slide ramps, teal runnable walls, amber mantle ledges, pink slide-under bars), the
    /// tuning asset (made once; edit it freely), its GameConfig preset and the scene (the player with
    /// the movement controller, camera feel, placeholder body, speed lines and sound; the course from
    /// core <see cref="LabCourse"/>; the dream-static pursuer). Runs as a step of
    /// "JamKit/Rebuild Shell Scenes"; "JamKit/Movement Lab/Rebuild Scene" rebuilds only the lab.
    /// Delete the Templates/MovementLab folder and it all disappears (the movement module itself lives in the package).
    /// </summary>
    public static class MovementLabBuilder
    {
        public const string Root = "Assets/JamKit/Templates/MovementLab";
        public const string SceneName = "MovementLab";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/MovementLabGameConfig.asset";
        public const string TuningPath = Root + "/Data/MovementLabTuning.asset";
        const string Mats = Root + "/Materials";

        static readonly Color Sky = new Color(0.68f, 0.75f, 0.86f);

        [JamKitBuildStep(240)]
        static void Build(SceneBuilder.Context ctx)
        {
            string scene = BuildEverything(ctx);
            if (string.IsNullOrEmpty(scene)) return;
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Movement Lab/Rebuild Scene")]
        static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEverything(null);
        }

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { Root + "/Scenes", Mats, Root + "/Data" }) EnsureFolder(d);
            EnsureTuning();
            MakePreset(ctx);
            var mats = MakeMaterials();
            AssetDatabase.SaveAssets();
            string path = BuildScene(mats);
            Debug.Log("JAMKIT_MOVEMENTLAB_OK " + path);
            return path;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static MovementTuning EnsureTuning()
        {
            var t = AssetDatabase.LoadAssetAtPath<MovementTuning>(TuningPath);
            if (t != null) return t;
            t = ScriptableObject.CreateInstance<MovementTuning>();
            AssetDatabase.CreateAsset(t, TuningPath);
            return t;
        }

        // ---------- Preset ----------

        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Movement Lab";
            p.tagline = "Slide, wallrun, double jump. Keep your speed.";
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
                    title = "Move",
                    body = "WASD or the left stick; you sprint on your own. Mouse or right stick to look.\nSpace (A) jumps, and jumps again in the air. Ctrl or C (B) slides when you're running.",
                },
                new HowToPlayPage
                {
                    title = "Keep your speed",
                    body = "Jump at a wall along it and hold forward to run on it; jump off it quickly to keep your speed. Ledges pull you up by themselves.\nF3 (View) shows your speed. Esc > Comfort and camera for the field of view and the effects.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        // ---------- Materials ----------

        public sealed class Materials
        {
            public Material floor, ramp, wall, ledge, bar, fold, marker, finish, staticCurtain, speedLines;
            public Material sleeve, skin, legs, shoes;
        }

        static Materials MakeMaterials()
        {
            var m = new Materials
            {
                floor = Grid("Grid_Floor", new Color(0.55f, 0.57f, 0.62f), new Color(0.74f, 0.76f, 0.82f)),
                ramp = Grid("Grid_SlideRamp", new Color(0.5f, 0.62f, 0.76f), new Color(0.74f, 0.84f, 0.95f)),
                wall = Grid("Grid_RunWall", new Color(0.36f, 0.68f, 0.7f), new Color(0.62f, 0.9f, 0.88f)),
                ledge = Grid("Grid_Ledge", new Color(0.84f, 0.65f, 0.42f), new Color(0.98f, 0.84f, 0.6f)),
                bar = Grid("Grid_SlideBar", new Color(0.9f, 0.5f, 0.58f), new Color(1f, 0.74f, 0.78f)),
                fold = Grid("Grid_Fold", new Color(0.42f, 0.42f, 0.5f), new Color(0.55f, 0.55f, 0.64f)),
                marker = Grid("Grid_Checkpoint", new Color(0.6f, 0.86f, 1f), new Color(0.85f, 0.95f, 1f), 1f),
                finish = Grid("Grid_Finish", new Color(1f, 0.86f, 0.5f), new Color(1f, 0.95f, 0.8f), 1f),
            };
            m.staticCurtain = Mat("DreamStatic", "JamKit/MovementLab/DreamStatic", null);
            m.speedLines = Mat("SpeedLines", "JamKit/Movement/SpeedLines", null);
            // The placeholder body: soft, slightly glossy URP Lit.
            m.sleeve = Lit("Body_Sleeve", new Color(0.62f, 0.58f, 0.84f));
            m.skin = Lit("Body_Skin", new Color(0.96f, 0.79f, 0.68f));
            m.legs = Lit("Body_Legs", new Color(0.36f, 0.56f, 0.62f));
            m.shoes = Lit("Body_Shoes", new Color(0.95f, 0.92f, 0.84f));
            return m;
        }

        static Material Mat(string name, string shaderName, Color? color)
        {
            string path = Mats + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Movement Lab: shader " + shaderName + " not found.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            if (color.HasValue && mat.HasProperty("_Color")) mat.SetColor("_Color", color.Value);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Grid(string name, Color color, Color line, float glow = 0f)
        {
            var mat = Mat(name, "JamKit/MovementLab/Grid", color);
            mat.SetColor("_LineColor", line);
            mat.SetFloat("_Glow", glow);
            return mat;
        }

        static Material Lit(string name, Color color)
        {
            var mat = Mat(name, "Universal Render Pipeline/Lit", null);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.28f);
            mat.color = color;
            return mat;
        }

        // ---------- Scene ----------

        static string BuildScene(Materials m)
        {
            var scene = SceneBuilder.NewScene(Sky, out var cam);
            var course = LabCourse.Build();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.64f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.36f, 0.42f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 260f;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // The body (yaw) > head (pitch, eye height, interpolation) > rig (bob, roll, dip) > camera.
            var playerGo = new GameObject("Player");
            var first = course.checkpoints[0];
            playerGo.transform.position = new Vector3(first.position.X, first.position.Y, first.position.Z);
            playerGo.AddComponent<CharacterController>();
            var body = playerGo.AddComponent<MovementController>();
            body.controller = playerGo.GetComponent<CharacterController>();
            body.tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(TuningPath);
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
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
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
            var arms = cam.gameObject.AddComponent<MovementBody>();
            arms.body = body;
            arms.sleeveMaterial = m.sleeve;
            arms.skinMaterial = m.skin;
            arms.legMaterial = m.legs;
            arms.shoeMaterial = m.shoes;
            var lines = cam.gameObject.AddComponent<SpeedLines>();
            lines.body = body;
            lines.material = m.speedLines;
            var sound = playerGo.AddComponent<MovementAudio>();
            sound.body = body;

            // The course.
            var root = new GameObject("Course").transform;
            for (int i = 0; i < course.pieces.Count; i++) BuildPiece(course.pieces[i], i, root, m);
            foreach (var cp in course.checkpoints) Marker(root, "Checkpoint " + cp.name, cp.position.Z, cp.position.Y, m.marker, 0.25f);
            Marker(root, "Finish", course.finishS, course.pieces.Find(p => p.name == "Finish").Max.Y, m.finish, 0.6f);

            // The pursuer: a curtain of dream-static across the route.
            var chaser = new GameObject("Dream static (pursuer)");
            var view = chaser.AddComponent<LabPursuerView>();
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Curtain";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(chaser.transform, false);
            quad.transform.localScale = new Vector3(16f, 22f, 1f);
            quad.transform.localPosition = new Vector3(0f, 4f, 0f);
            var qr = quad.GetComponent<MeshRenderer>();
            qr.sharedMaterial = m.staticCurtain;
            qr.shadowCastingMode = ShadowCastingMode.Off;
            view.curtain = qr;
            view.hiss = chaser.AddComponent<AudioSource>();
            view.hiss.playOnAwake = false;
            chaser.transform.position = new Vector3(0f, 0f, course.chaseStartS - 20f);

            var game = new GameObject("Movement Lab").AddComponent<MovementLabGame>();
            game.player = body;
            game.pursuerView = view;
            game.courseRoot = root;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        static void BuildPiece(LabPiece p, int index, Transform root, Materials m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = p.name;
            go.transform.SetParent(root, false);
            Material mat;
            switch (p.kind)
            {
                case LabPieceKind.Ramp: mat = m.ramp; break;
                case LabPieceKind.Wall: mat = m.wall; break;
                case LabPieceKind.Ledge: mat = m.ledge; break;
                case LabPieceKind.Bar: mat = m.bar; break;
                case LabPieceKind.Fold: mat = m.fold; break;
                default: mat = m.floor; break;
            }
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (p.kind == LabPieceKind.Ramp)
            {
                // A tilted slab whose top runs from (z0, top0) to (z1, top1).
                float len = p.size.Z, drop = p.rampTopStart - p.rampTopEnd;
                float angle = Mathf.Atan2(drop, len) * Mathf.Rad2Deg;
                const float thick = 1f;
                var rot = Quaternion.Euler(angle, 0f, 0f);
                Vector3 topMid = new Vector3(p.center.X, (p.rampTopStart + p.rampTopEnd) * 0.5f, p.center.Z);
                go.transform.SetPositionAndRotation(topMid - rot * Vector3.up * (thick * 0.5f), rot);
                go.transform.localScale = new Vector3(p.size.X, thick, Mathf.Sqrt(len * len + drop * drop));
            }
            else
            {
                go.transform.position = new Vector3(p.center.X, p.center.Y, p.center.Z);
                go.transform.localScale = new Vector3(p.size.X, p.size.Y, p.size.Z);
            }
            if (!p.solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.AddComponent<MovementSurface>().runnable = p.runnable;
            var view = go.AddComponent<LabPieceView>();
            view.index = index;
            view.collapse = p.collapse;
            view.fold = p.kind == LabPieceKind.Fold;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }

        static void Marker(Transform root, string name, float z, float floorTop, Material mat, float thickness)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = name + (side < 0 ? " (left)" : " (right)");
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(root, false);
                post.transform.position = new Vector3(side * (LabCourse.Half + 0.2f), floorTop + 1.6f, z);
                post.transform.localScale = new Vector3(thickness, 3.2f, thickness);
                var r = post.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
