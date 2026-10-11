using System.Collections.Generic;
using System.IO;
using JamKit.EditorTools;
using JamKit.Movement;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.StealthLab.EditorTools
{
    /// <summary>
    /// Builds the Stealth Lab (docs/STEALTH.md): its materials, its two tuning assets (movement and stealth; made once,
    /// edit freely), its GameConfig preset and the scene from core <see cref="StealthYard"/>: the yard's boxes (solid
    /// ones with colliders; bushes, the canopy and the closet curtain without), light zones and the lamp, hiding spots,
    /// the five collectibles, the player (movement controller, camera feel, placeholder body, sound, StealthPlayer) and
    /// the seeker (its waypoints, patrol, overlay). Runs as a step of "JamKit/Rebuild Shell Scenes";
    /// "JamKit/Stealth Lab/Rebuild Scene" rebuilds only the lab. Delete Templates/StealthLab and it all disappears (the
    /// stealth kit itself lives in the package).
    /// </summary>
    public static class StealthLabBuilder
    {
        public const string Root = "Assets/JamKit/Templates/StealthLab";
        public const string SceneName = "StealthLab";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/StealthLabGameConfig.asset";
        public const string MovementTuningPath = MovementTuning.SharedAssetPath; // the shared movement tuning
        public const string StealthTuningPath = Root + "/Data/StealthLabStealth.asset";
        public const string OverlayShader = "JamKit/Stealth/Overlay";
        const string Mats = Root + "/Materials";

        static readonly Color Sky = new Color(0.55f, 0.62f, 0.74f);

        [JamKitBuildStep(250)]
        static void Build(SceneBuilder.Context ctx)
        {
            string scene = BuildEverything(ctx);
            if (string.IsNullOrEmpty(scene)) return;
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Stealth Lab/Rebuild Scene")]
        static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEverything(null);
        }

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { Root + "/Scenes", Mats, Root + "/Data" }) EnsureFolder(d);
            EnsureTunings();
            MakePreset(ctx);
            var mats = MakeMaterials();
            AssetDatabase.SaveAssets();
            string path = BuildScene(mats);
            Debug.Log("JAMKIT_STEALTHLAB_OK " + path);
            return path;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void EnsureTunings()
        {
            var t = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementTuningPath);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<MovementTuning>();
                t.UpgradeDefaults();
                AssetDatabase.CreateAsset(t, MovementTuningPath);
            }
            else if (t.UpgradeDefaults()) EditorUtility.SetDirty(t);
            if (AssetDatabase.LoadAssetAtPath<StealthTuning>(StealthTuningPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<StealthTuning>(), StealthTuningPath);
        }

        // ---------- Preset ----------

        public const string MovePage = "WASD or the left stick to walk. Hold Left Shift (LB) to sprint, or click the left stick (L3). Mouse or right stick to look.\nSpace (A) jumps, and jumps again in the air. Ctrl or C (hold B, or click R3) crouches, and slides when you're running.";

        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Stealth Lab";
            p.tagline = "Hide. Sneak. Don't get found.";
            p.firstGameplayScene = SceneName;
            p.titleMusic = "music_title";
            p.gameMusic = "";
            p.showHowToPlayOnFirstRun = true;
            p.showPixelLookSetting = false;
            p.showTextSettings = false;
            p.showActionSettings = true;
            p.showBestTime = false;
            p.chooseDifficultyOnPlay = false;
            p.showControlLegend = false;
            p.credits = ctx != null && ctx.config != null ? ctx.config.credits : null;
            p.audio = ctx != null && ctx.config != null ? ctx.config.audio : null;
            p.howToPlay = new List<HowToPlayPage>
            {
                new HowToPlayPage { title = "Move", body = MovePage },
                new HowToPlayPage
                {
                    title = "Don't get found",
                    body = "The bar at the top fills while the seeker sees you: it fills slower far away, in the dark, crouched, still, or behind low cover. Hide in bushes, under the deck or in the closet, but a searching seeker checks them.\nHard landings, sprinting and wallruns make noise. R (View) restarts.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        // ---------- Materials ----------

        public sealed class Materials
        {
            public Material ground, fence, wall, runWall, cover, ledge, roof, bar, post, bush, canopy, curtain, house, shed;
            public Material sleeve, skin, legs, shoes, seekerBody, seekerHead, visor, collectible, cone, meter, ring, guardBody, checkpoint;
        }

        static Materials MakeMaterials()
        {
            return new Materials
            {
                ground = Lit("Yard_Ground", new Color(0.42f, 0.52f, 0.38f)),
                fence = Lit("Yard_Fence", new Color(0.55f, 0.47f, 0.38f)),
                wall = Lit("Yard_Wall", new Color(0.7f, 0.68f, 0.64f)),
                runWall = Lit("Yard_RunWall", new Color(0.36f, 0.68f, 0.7f)),
                cover = Lit("Yard_Cover", new Color(0.62f, 0.6f, 0.56f)),
                ledge = Lit("Yard_Ledge", new Color(0.84f, 0.65f, 0.42f)),
                roof = Lit("Yard_Roof", new Color(0.6f, 0.42f, 0.36f)),
                bar = Lit("Yard_Bar", new Color(0.9f, 0.5f, 0.58f)),
                post = Lit("Yard_Post", new Color(0.5f, 0.4f, 0.32f)),
                bush = Lit("Yard_Bush", new Color(0.2f, 0.45f, 0.22f)),
                canopy = Lit("Yard_Canopy", new Color(0.16f, 0.3f, 0.18f)),
                curtain = Lit("Yard_Curtain", new Color(0.46f, 0.3f, 0.5f)),
                house = Lit("Yard_House", new Color(0.78f, 0.74f, 0.66f)),
                shed = Lit("Yard_Shed", new Color(0.52f, 0.44f, 0.36f)),
                sleeve = Lit("Body_Sleeve", new Color(0.62f, 0.58f, 0.84f)),
                skin = Lit("Body_Skin", new Color(0.96f, 0.79f, 0.68f)),
                legs = Lit("Body_Legs", new Color(0.36f, 0.56f, 0.62f)),
                shoes = Lit("Body_Shoes", new Color(0.95f, 0.92f, 0.84f)),
                seekerBody = Lit("Seeker_Body", new Color(0.85f, 0.42f, 0.32f)),
                seekerHead = Lit("Seeker_Head", new Color(0.96f, 0.82f, 0.7f)),
                visor = Lit("Seeker_Visor", new Color(0.12f, 0.12f, 0.16f)),
                collectible = Lit("Collectible", new Color(1f, 0.8f, 0.3f), 0.6f),
                cone = Overlay("Seeker_Cone", 4f),
                ring = Overlay("Noise_Ring", 4f),
                guardBody = Lit("Guard_Body", new Color(0.36f, 0.42f, 0.7f)),
                checkpoint = Lit("Checkpoint", new Color(0.3f, 0.95f, 0.75f), 0.5f),
                meter = Overlay("Seeker_Meter", 8f),
            };
        }

        static Material Load(string name, string shaderName)
        {
            string path = Mats + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Stealth Lab: shader " + shaderName + " not found.");
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
            return mat;
        }

        static Material Lit(string name, Color color, float emission = 0f)
        {
            var mat = Load(name, "Universal Render Pipeline/Lit");
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            mat.color = color;
            if (emission > 0f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return mat;
        }

        static Material Overlay(string name, float zTest)
        {
            var mat = Load(name, OverlayShader);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_ZTestMode")) mat.SetFloat("_ZTestMode", zTest);
            return mat;
        }

        // ---------- Scene ----------

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        static string BuildScene(Materials m)
        {
            var yard = StealthYard.Build();
            var scene = SceneBuilder.NewScene(Sky, out var cam);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.25f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;
            var sun = new GameObject("Moon").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.78f, 0.84f, 1f);
            sun.intensity = 0.75f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // The yard.
            var root = new GameObject("Yard").transform;
            foreach (var p in yard.pieces) Piece(p, root, m);
            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.86f, 0.6f);
            lamp.range = 11f;
            lamp.intensity = 3f;
            lamp.transform.SetParent(root, false);
            lamp.transform.position = U(yard.lampPosition);
            var pole = Box("Lamp post", root, U(yard.lampPosition) - new Vector3(0f, 1.9f, 0f), new Vector3(0.15f, 3.8f, 0.15f), m.post, false);
            pole.name = "Lamp post";
            var zones = new GameObject("Light zones").transform;
            zones.SetParent(root, false);
            foreach (var l in yard.lights)
            {
                var z = new GameObject("Light zone: " + l.name).AddComponent<StealthLightZone>();
                z.transform.SetParent(zones, false);
                z.transform.position = U(l.center);
                z.size = U(l.size);
                z.level = l.level;
            }
            var spots = new GameObject("Hiding spots").transform;
            spots.SetParent(root, false);
            foreach (var s in yard.spots)
            {
                var v = new GameObject("Hiding spot: " + s.name).AddComponent<HidingSpotView>();
                v.name = s.name;
                v.transform.SetParent(spots, false);
                v.transform.position = U(s.Center);
                v.kind = s.kind;
                v.size = U(s.Size);
                var a = new GameObject("Check point").transform;
                a.SetParent(v.transform, false);
                a.position = U(s.Approach);
                v.approach = a;
            }

            // The collectibles.
            var counter = new GameObject("Collectibles").AddComponent<CollectibleCounter>();
            foreach (var c in yard.collectibles)
            {
                var go = new GameObject("Collectible: " + c.id);
                go.transform.SetParent(counter.transform, false);
                go.transform.position = U(c.position);
                var item = go.AddComponent<Collectible>();
                item.id = c.id;
                var gem = Box("Gem", go.transform, U(c.position), new Vector3(0.32f, 0.32f, 0.32f), m.collectible, false);
                gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                var holder = new GameObject("Visual").transform;
                holder.SetParent(go.transform, false);
                gem.transform.SetParent(holder, true);
                item.visual = holder;
            }

            // The checkpoints: a glowing pad on the ground at each (no collider; the game checks the box).
            var cps = new GameObject("Checkpoints").transform;
            var zonesList = new List<StealthCheckpointZone>();
            foreach (var c in yard.checkpoints)
            {
                var z = new GameObject("Checkpoint: " + c.name).AddComponent<StealthCheckpointZone>();
                z.transform.SetParent(cps, false);
                z.transform.position = U(c.spawn);
                z.label = c.name;
                z.size = U(c.size);
                z.yaw = c.yaw;
                var pad = Box("Pad", z.transform, U(c.spawn) + new Vector3(0f, 0.02f, 0f), new Vector3(c.size.X, 0.04f, c.size.Z), m.checkpoint, false);
                pad.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                zonesList.Add(z);
            }

            // The player: body (yaw) > head (pitch) > rig (bob, roll) > camera.
            var playerGo = new GameObject("Player");
            playerGo.transform.position = U(yard.playerStart);
            playerGo.transform.rotation = Quaternion.Euler(0f, yard.playerYaw, 0f);
            playerGo.AddComponent<CharacterController>();
            var body = playerGo.AddComponent<MovementController>();
            body.controller = playerGo.GetComponent<CharacterController>();
            body.tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(MovementTuningPath);
            var head = new GameObject("Head").transform;
            head.SetParent(playerGo.transform, false);
            head.localPosition = new Vector3(0f, 1.5f, 0f);
            var rig = new GameObject("Rig").transform;
            rig.SetParent(head, false);
            cam.transform.SetParent(rig, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.orthographic = false;
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Sky;
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = false;
            cam.gameObject.AddComponent<CameraShaker>().strength = 0.06f;
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
            arms.feelCamera = feel;
            playerGo.AddComponent<MovementAudio>().body = body;
            var sp = playerGo.AddComponent<StealthPlayer>();
            sp.body = body;
            sp.tuning = AssetDatabase.LoadAssetAtPath<StealthTuning>(StealthTuningPath);
            // The answers to fast movement: the noise made visible (A), the daytime section and the lucidity fade (C).
            var rings = playerGo.AddComponent<NoiseRings>();
            rings.player = sp;
            rings.material = m.ring;
            var pr = playerGo.AddComponent<StealthPlayerRules>();
            pr.body = body;
            pr.tuning = sp.tuning;

            // The seeker: a figure (no colliders), its waypoints and overlay.
            var seekerGo = new GameObject("Seeker");
            seekerGo.transform.position = U(yard.seekerStart);
            seekerGo.transform.rotation = Quaternion.Euler(0f, yard.seekerYaw, 0f);
            Capsule("Body", seekerGo.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.85f, 0.7f), m.seekerBody);
            Sphere("Head", seekerGo.transform, new Vector3(0f, 1.62f, 0f), 0.42f, m.seekerHead);
            var visor = Box("Eyes", seekerGo.transform, Vector3.zero, new Vector3(0.34f, 0.1f, 0.1f), m.visor, false);
            visor.transform.SetParent(seekerGo.transform, false);
            visor.transform.localPosition = new Vector3(0f, 1.66f, 0.19f);
            var agent = seekerGo.AddComponent<SeekerAgent>();
            agent.tuning = sp.tuning;
            agent.mode = SeekerMode.HideAndSeek;
            agent.countYaw = yard.countYaw;
            agent.connectDistance = yard.connectDistance;
            var wps = new GameObject("Seeker waypoints").transform;
            for (int i = 0; i < yard.nodes.Count; i++)
            {
                var n = new GameObject("Waypoint " + i).transform;
                n.SetParent(wps, false);
                n.position = U(yard.nodes[i]);
            }
            agent.waypoints = wps;
            agent.patrol = new List<int>(yard.patrol);
            var overlay = seekerGo.AddComponent<SeekerOverlay>();
            overlay.seeker = agent;
            overlay.coneMaterial = m.cone;
            overlay.meterMaterial = m.meter;

            // Two more guards for the three-guard option: overlapping patrols of the west and east halves (off until chosen).
            var extra = new List<SeekerAgent>();
            foreach (var (name, start, loop) in new[] { ("Guard 2 (west)", yard.guard2Start, yard.patrol2), ("Guard 3 (east)", yard.guard3Start, yard.patrol3) })
            {
                var go = new GameObject(name);
                go.transform.position = U(start);
                Capsule("Body", go.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.85f, 0.7f), m.guardBody);
                Sphere("Head", go.transform, new Vector3(0f, 1.62f, 0f), 0.42f, m.seekerHead);
                var eyes = Box("Eyes", go.transform, Vector3.zero, new Vector3(0.34f, 0.1f, 0.1f), m.visor, false);
                eyes.transform.SetParent(go.transform, false);
                eyes.transform.localPosition = new Vector3(0f, 1.66f, 0.19f);
                var g = go.AddComponent<SeekerAgent>();
                g.tuning = sp.tuning;
                g.mode = SeekerMode.Patrol;
                g.connectDistance = yard.connectDistance;
                g.waypoints = wps;
                g.patrol = new List<int>(loop);
                var o = go.AddComponent<SeekerOverlay>();
                o.seeker = g;
                o.coneMaterial = m.cone;
                o.meterMaterial = m.meter;
                go.SetActive(false);
                extra.Add(g);
            }

            var game = new GameObject("Stealth Lab").AddComponent<StealthLabGame>();
            game.player = body;
            game.seeker = agent;
            game.extraGuards = extra;
            game.collectibles = counter;
            game.checkpoints = zonesList;
            game.playerStart = U(yard.playerStart);
            game.playerYaw = yard.playerYaw;
            game.ambientLight = yard.ambientLight;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        static void Piece(YardPiece p, Transform root, Materials m)
        {
            Material mat;
            switch (p.kind)
            {
                case YardPieceKind.Floor: mat = m.ground; break;
                case YardPieceKind.RunWall: mat = p.name.StartsWith("Fence") ? m.fence : m.runWall; break;
                case YardPieceKind.Cover: mat = m.cover; break;
                case YardPieceKind.Ledge: mat = m.ledge; break;
                case YardPieceKind.Roof: mat = m.roof; break;
                case YardPieceKind.Bar: mat = m.bar; break;
                case YardPieceKind.Post: mat = m.post; break;
                case YardPieceKind.Bush: mat = m.bush; break;
                case YardPieceKind.Canopy: mat = m.canopy; break;
                case YardPieceKind.Curtain: mat = m.curtain; break;
                default: mat = p.name == "House" ? m.house : p.name == "Shed" ? m.shed : m.wall; break;
            }
            var go = Box(p.name, root, U(p.center), U(p.size), mat, p.Solid);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = p.kind == YardPieceKind.Floor ? ShadowCastingMode.Off : ShadowCastingMode.On;
            if (p.Solid && p.kind != YardPieceKind.Floor)
                go.AddComponent<MovementSurface>().runnable = p.kind == YardPieceKind.RunWall || p.kind == YardPieceKind.Wall;
        }

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        static void Capsule(string name, Transform parent, Vector3 local, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void Sphere(string name, Transform parent, Vector3 local, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
