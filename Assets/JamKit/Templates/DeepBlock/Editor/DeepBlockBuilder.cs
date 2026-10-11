using System.Collections.Generic;
using System.IO;
using JamKit.DeepBlock;
using JamKit.EditorTools;
using JamKit.Movement;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Block = JamKit.DeepBlock.DeepBlock;

namespace JamKit.DeepBlockLab.EditorTools
{
    /// <summary>
    /// Builds the Deep Block lab (docs/DEEP-BLOCK.md) from core <see cref="Block"/>: greybox materials with the gold
    /// "usable" colour on what you climb, run or slide (a lip on ledges, a cap on fences, a stripe on runnable walls),
    /// golden-hour light from the bottom of the street, every piece (pitched roofs as prism meshes with colliders; the
    /// lane as a tilted slab; switch props under "Switches"; sheets as sight blockers), the hide-and-seek zone (Theo,
    /// his walking points and trampoline, the eight hiding places, the six memories), the player, and the game.
    /// Runs as a step of "JamKit/Rebuild Shell Scenes"; "JamKit/Deep Block/Rebuild Scene" rebuilds only the lab.
    /// </summary>
    public static class DeepBlockBuilder
    {
        public const string Root = "Assets/JamKit/Templates/DeepBlock";
        public const string SceneName = "DeepBlock";
        public const string ScenePath = Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = Root + "/DeepBlockGameConfig.asset";
        public const string MovementTuningPath = MovementTuning.SharedAssetPath; // the shared movement tuning
        public const string TheoTuningPath = Root + "/Data/TheoStealth.asset";
        public const string PrismPath = Root + "/Meshes/RoofPrism.asset";
        const string Mats = Root + "/Materials";

        public static readonly Color Sky = new Color(0.98f, 0.76f, 0.54f);

        [JamKitBuildStep(255)]
        static void Build(SceneBuilder.Context ctx)
        {
            string scene = BuildEverything(ctx);
            if (string.IsNullOrEmpty(scene)) return;
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Deep Block/Rebuild Scene")]
        static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEverything(null);
        }

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { Root + "/Scenes", Mats, Root + "/Data", Root + "/Meshes" }) EnsureFolder(d);
            EnsureTunings();
            MakePreset(ctx);
            var mats = MakeMaterials();
            var prism = EnsurePrism();
            AssetDatabase.SaveAssets();
            string path = BuildScene(mats, prism);
            Debug.Log("JAMKIT_DEEPBLOCK_OK " + path);
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
            if (AssetDatabase.LoadAssetAtPath<StealthTuning>(TheoTuningPath) == null)
            {
                var theo = ScriptableObject.CreateInstance<StealthTuning>();
                theo.def = Block.TheoDef();
                AssetDatabase.CreateAsset(theo, TheoTuningPath);
            }
        }

        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "The Deep Block";
            p.tagline = "Larch Street at golden hour, built backwards and upwards.";
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
                new HowToPlayPage { title = "Move", body = "WASD or the left stick. Shift (LB) or L3 sprints. Space (A) jumps twice. Ctrl or C (B, R3) slides. Ledges pull you up by themselves." },
                new HowToPlayPage
                {
                    title = "The deep block",
                    body = "Four ways to the hill gate: the street, the back gardens and the lane, the roofs, and the creek under the street. Gold marks what you can climb, run or slide. Esc > chapters: each route, and hide and seek with Theo.\nF7 (LB + d-pad right): the night. F8 (LB + d-pad left): the autopilot runs the route. R (View) restarts.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        // ---------- Materials ----------

        public sealed class Materials
        {
            public Material grass, road, pavement, tarmac, earth, house, roof, garage, brick, block, fence, picket, hedge, timber,
                deck, post, gate, scaffold, glass, water, sheet, trunk, leaves, gold, sleeve, skin, legs, shoes, theo, theoHead,
                visor, memory, cone, meter, paint;
        }

        static Materials MakeMaterials()
        {
            return new Materials
            {
                grass = Lit("Block_Grass", new Color(0.46f, 0.56f, 0.33f)),
                road = Lit("Block_Road", new Color(0.32f, 0.31f, 0.33f)),
                pavement = Lit("Block_Pavement", new Color(0.68f, 0.64f, 0.6f)),
                tarmac = Lit("Block_Tarmac", new Color(0.38f, 0.36f, 0.37f)),
                earth = Lit("Block_Earth", new Color(0.52f, 0.43f, 0.33f)),
                house = Lit("Block_House", new Color(0.86f, 0.8f, 0.7f)),
                roof = Lit("Block_Roof", new Color(0.66f, 0.38f, 0.3f)),
                garage = Lit("Block_Garage", new Color(0.66f, 0.62f, 0.58f)),
                brick = Lit("Block_Brick", new Color(0.62f, 0.4f, 0.32f)),
                block = Lit("Block_BlockWall", new Color(0.62f, 0.62f, 0.6f)),
                fence = Lit("Block_Fence", new Color(0.55f, 0.43f, 0.32f)),
                picket = Lit("Block_Picket", new Color(0.93f, 0.92f, 0.88f)),
                hedge = Lit("Block_Hedge", new Color(0.22f, 0.4f, 0.2f)),
                timber = Lit("Block_Timber", new Color(0.7f, 0.56f, 0.4f)),
                deck = Lit("Block_Deck", new Color(0.62f, 0.48f, 0.35f)),
                post = Lit("Block_Post", new Color(0.45f, 0.38f, 0.32f)),
                gate = Lit("Block_Gate", new Color(0.28f, 0.42f, 0.46f)),
                scaffold = Lit("Block_Scaffold", new Color(0.6f, 0.64f, 0.68f)),
                glass = Transparent("Block_Glass", new Color(0.75f, 0.9f, 0.95f, 0.28f)),
                water = Transparent("Block_Water", new Color(0.35f, 0.55f, 0.65f, 0.6f)),
                sheet = Lit("Block_Sheet", new Color(0.96f, 0.95f, 0.92f)),
                trunk = Lit("Block_Trunk", new Color(0.4f, 0.3f, 0.22f)),
                leaves = Lit("Block_Leaves", new Color(0.32f, 0.48f, 0.24f)),
                gold = Lit("Block_Gold", new Color(1f, 0.78f, 0.3f), 0.35f),
                paint = Lit("Block_Paint", new Color(0.36f, 0.35f, 0.36f)),
                sleeve = Lit("Body_Sleeve", new Color(0.62f, 0.58f, 0.84f)),
                skin = Lit("Body_Skin", new Color(0.96f, 0.79f, 0.68f)),
                legs = Lit("Body_Legs", new Color(0.36f, 0.56f, 0.62f)),
                shoes = Lit("Body_Shoes", new Color(0.95f, 0.92f, 0.84f)),
                theo = Lit("Theo_Body", new Color(0.95f, 0.62f, 0.25f)),
                theoHead = Lit("Theo_Head", new Color(0.96f, 0.82f, 0.7f)),
                visor = Lit("Theo_Eyes", new Color(0.12f, 0.12f, 0.16f)),
                memory = Lit("Memory", new Color(0.7f, 0.85f, 1f), 0.8f),
                cone = Overlay("Theo_Cone", 4f),
                meter = Overlay("Theo_Meter", 8f),
            };
        }

        static Material Load(string name, string shaderName)
        {
            string path = Mats + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("Deep Block: shader " + shaderName + " not found.");
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
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
            mat.color = color;
            if (emission > 0f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return mat;
        }

        /// <summary>URP Lit, transparent (alpha blended, no depth write).</summary>
        static Material Transparent(string name, Color color)
        {
            var mat = Lit(name, color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        static Material Overlay(string name, float zTest)
        {
            var mat = Load(name, "JamKit/Stealth/Overlay");
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_ZTestMode")) mat.SetFloat("_ZTestMode", zTest);
            return mat;
        }

        /// <summary>A unit triangular prism: base 1 wide (X) on y 0, apex at y 1 over x 0, 1 long (Z). The roofs.</summary>
        static Mesh EnsurePrism()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PrismPath);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = "RoofPrism" };
            var a = new Vector3(-0.5f, 0f, -0.5f); var b = new Vector3(0.5f, 0f, -0.5f); var c = new Vector3(0f, 1f, -0.5f);
            var d = new Vector3(-0.5f, 0f, 0.5f); var e = new Vector3(0.5f, 0f, 0.5f); var f = new Vector3(0f, 1f, 0.5f);
            // Flat-shaded: each face its own vertices.
            var v = new List<Vector3>();
            var t = new List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r) { int i = v.Count; v.Add(p); v.Add(q); v.Add(r); t.Add(i); t.Add(i + 1); t.Add(i + 2); }
            void Quad(Vector3 p, Vector3 q, Vector3 r, Vector3 s) { Tri(p, q, r); Tri(p, r, s); }
            Tri(a, c, b);          // the -Z gable
            Tri(d, e, f);          // the +Z gable
            Quad(a, d, f, c);      // the -X pitch
            Quad(b, c, f, e);      // the +X pitch
            Quad(a, b, e, d);      // the base
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, PrismPath);
            return mesh;
        }

        // ---------- Scene ----------

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        static string BuildScene(Materials m, Mesh prism)
        {
            var block = Block.Build();
            var scene = SceneBuilder.NewScene(Sky, out var cam);

            // Golden hour: a low sun at the bottom of the street (west, behind Walt's shop), every building throwing a
            // long shadow uphill; warm ambient.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.7f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.5f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.24f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 240f;
            var sun = new GameObject("Sun (golden hour)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.72f, 0.46f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(9f, 8f, 0f);

            var root = new GameObject("The deep block").transform;
            var switchRoot = new GameObject("Switches").transform;
            var trims = new GameObject("Gold (usable)").transform;
            foreach (var p in block.pieces)
            {
                if (p.kind == BlockKind.Roof) continue; // the prisms below are the roofs
                var parent = p.switchIndex >= 0 ? switchRoot : root;
                var go = Piece(p, parent, m);
                if (go == null) continue;
                if (p.switchIndex >= 0)
                {
                    var sw = go.AddComponent<BlockSwitchProp>();
                    sw.switchIndex = p.switchIndex;
                    sw.presentWhenOpen = p.presentWhenOpen;
                }
                if (p.usable) Gold(p, trims, m.gold);
                if (p.kind == BlockKind.House) Roof(p, root, m.roof, prism);
            }

            // Route start pads and the finish at the hill gate.
            var pads = new GameObject("Route starts").transform;
            foreach (var r in block.routes)
            {
                var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = "Start: " + r.name;
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                pad.transform.SetParent(pads, false);
                pad.transform.position = U(r.start) + Vector3.up * 0.01f;
                pad.transform.localScale = new Vector3(1.2f, 0.01f, 1.2f);
                pad.GetComponent<MeshRenderer>().sharedMaterial = m.gold;
            }

            // Hide-and-seek: the hiding places, the memories, Theo.
            var zone = new GameObject("Hide-and-seek zone").transform;
            foreach (var s in block.spots)
            {
                var v = new GameObject(s.name).AddComponent<HidingSpotView>();
                v.transform.SetParent(zone, false);
                v.transform.position = U(s.Center);
                v.kind = s.kind;
                v.size = U(s.Size);
                var a = new GameObject("Check point").transform;
                a.SetParent(v.transform, false);
                a.position = U(s.Approach);
                v.approach = a;
            }
            var counter = new GameObject("Memories").AddComponent<CollectibleCounter>();
            counter.transform.SetParent(zone, false);
            for (int i = 0; i < block.memories.Count; i++)
            {
                var go = new GameObject("Memory " + (i + 1));
                go.transform.SetParent(counter.transform, false);
                go.transform.position = U(block.memories[i]);
                var item = go.AddComponent<Collectible>();
                item.id = "memory-" + (i + 1);
                var holder = new GameObject("Visual").transform;
                holder.SetParent(go.transform, false);
                var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gem.name = "Gem";
                Object.DestroyImmediate(gem.GetComponent<Collider>());
                gem.transform.SetParent(holder, false);
                gem.transform.localScale = Vector3.one * 0.3f;
                gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                gem.GetComponent<MeshRenderer>().sharedMaterial = m.memory;
                item.visual = holder;
            }

            var theoGo = new GameObject("Theo");
            theoGo.transform.SetParent(zone, false);
            theoGo.transform.position = U(block.trampoline);
            theoGo.transform.rotation = Quaternion.Euler(0f, block.theoCountYaw, 0f);
            Shape(PrimitiveType.Capsule, "Body", theoGo.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.6f, 0.5f), m.theo);
            Shape(PrimitiveType.Sphere, "Head", theoGo.transform, new Vector3(0f, 1.18f, 0f), Vector3.one * 0.34f, m.theoHead);
            Shape(PrimitiveType.Cube, "Eyes", theoGo.transform, new Vector3(0f, 1.2f, 0.15f), new Vector3(0.24f, 0.07f, 0.06f), m.visor);
            var theo = theoGo.AddComponent<SeekerAgent>();
            theo.tuning = AssetDatabase.LoadAssetAtPath<StealthTuning>(TheoTuningPath);
            theo.mode = SeekerMode.HideAndSeek;
            theo.countYaw = block.theoCountYaw;
            theo.connectDistance = 14f;
            var wps = new GameObject("Theo's walking points").transform;
            wps.SetParent(zone, false);
            for (int i = 0; i < block.theoNodes.Count; i++)
            {
                var n = new GameObject("Point " + i).transform;
                n.SetParent(wps, false);
                n.position = U(block.theoNodes[i]);
            }
            theo.waypoints = wps;
            var look = new GameObject("Trampoline (Theo's lookout)").transform;
            look.SetParent(zone, false);
            look.position = U(block.trampoline);
            theo.lookout = look;
            theo.lookoutPlatform = block.trampolineHeight;
            theo.lookoutBounce = block.bounceHeight;
            theo.lookoutPeriod = block.bouncePeriod;
            var overlay = theoGo.AddComponent<SeekerOverlay>();
            overlay.seeker = theo;
            overlay.coneMaterial = m.cone;
            overlay.meterMaterial = m.meter;
            overlay.meterHeight = 1.7f;

            // The player, at the street route's start.
            var start = block.routes[0];
            var playerGo = new GameObject("Player");
            playerGo.transform.position = U(start.start);
            playerGo.transform.rotation = Quaternion.Euler(0f, start.startYaw, 0f);
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
            cam.farClipPlane = 300f;
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
            sp.tuning = theo.tuning;

            var game = new GameObject("Deep Block").AddComponent<DeepBlockGame>();
            game.player = body;
            game.theo = theo;
            game.memories = counter;
            game.switchRoot = switchRoot;
            game.night = 1;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        static Material MaterialOf(BlockPiece p, Materials m)
        {
            switch (p.kind)
            {
                case BlockKind.Ground: return p.layer == "hill" ? m.earth : p.Max.Y < -1f ? m.earth : m.grass;
                case BlockKind.Lane: return m.tarmac;
                case BlockKind.Paint: return p.name.StartsWith("Pavement") ? m.pavement : p.name.StartsWith("Lane") ? m.tarmac : m.road;
                case BlockKind.House: return m.house;
                case BlockKind.Shop: return m.brick;
                case BlockKind.Garage: return m.garage;
                case BlockKind.BrickWall: return m.brick;
                case BlockKind.BlockWall: return m.block;
                case BlockKind.Fence: return m.fence;
                case BlockKind.Picket: return m.picket;
                case BlockKind.Hedge: return m.hedge;
                case BlockKind.Deck: return m.deck;
                case BlockKind.Post: return m.post;
                case BlockKind.Gate:
                case BlockKind.Door: return m.gate;
                case BlockKind.Plank: return m.timber;
                case BlockKind.Scaffold: return m.scaffold;
                case BlockKind.Glass: return m.glass;
                case BlockKind.Water: return m.water;
                case BlockKind.Sheet: return m.sheet;
                case BlockKind.Tree: return p.solid ? m.trunk : m.leaves;
                default: return m.timber;
            }
        }

        static GameObject Piece(BlockPiece p, Transform parent, Materials m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = p.name;
            go.transform.SetParent(parent, false);
            if (p.kind == BlockKind.Lane)
            {
                // A tilted slab whose top runs from rampLow at min Z to rampHigh at max Z.
                float len = p.size.Z, rise = p.rampHigh - p.rampLow;
                const float thick = 3f;
                var rot = Quaternion.Euler(-Mathf.Atan2(rise, len) * Mathf.Rad2Deg, 0f, 0f);
                var topMid = new Vector3(p.center.X, (p.rampLow + p.rampHigh) * 0.5f, p.center.Z);
                go.transform.SetPositionAndRotation(topMid - rot * Vector3.up * (thick * 0.5f), rot);
                go.transform.localScale = new Vector3(p.size.X, thick, Mathf.Sqrt(len * len + rise * rise));
            }
            else
            {
                go.transform.position = new Vector3(p.center.X, p.center.Y, p.center.Z);
                go.transform.localScale = new Vector3(p.size.X, p.size.Y, p.size.Z);
            }
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialOf(p, m);
            if (p.kind == BlockKind.Boundary) Object.DestroyImmediate(r);
            else if (p.kind == BlockKind.Paint || p.kind == BlockKind.Water || p.kind == BlockKind.Glass) r.shadowCastingMode = ShadowCastingMode.Off;
            if (!p.solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            else if (p.kind != BlockKind.Ground && p.kind != BlockKind.Lane && p.kind != BlockKind.Boundary)
                go.AddComponent<MovementSurface>().runnable = p.runnable;
            if (p.sightOnly) go.AddComponent<StealthSightBlocker>().size = new Vector3(p.size.X, p.size.Y, p.size.Z);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        /// <summary>A house's pitched roof: the prism over its footprint, inset by the eaves strip, with a collider.</summary>
        static void Roof(BlockPiece house, Transform parent, Material mat, Mesh prism)
        {
            var go = new GameObject(house.name + " roof");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(house.center.X, Block.Eaves, house.center.Z);
            go.transform.localScale = new Vector3(house.size.X - 2f * Block.EavesStrip, Block.Ridge - Block.Eaves, house.size.Z);
            go.AddComponent<MeshFilter>().sharedMesh = prism;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = prism;
            go.AddComponent<MovementSurface>().runnable = false;
        }

        /// <summary>The gold "usable" marks: a cap on a fence or wall top, a stripe along a runnable wall, a lip around a
        /// ledge's top edges, a band on a slide-under door.</summary>
        static void Gold(BlockPiece p, Transform parent, Material gold)
        {
            var c = new Vector3(p.center.X, p.center.Y, p.center.Z);
            var s = new Vector3(p.size.X, p.size.Y, p.size.Z);
            float top = p.Max.Y;
            const float t = 0.06f, w = 0.14f;
            bool thinX = s.x < 0.7f, thinZ = s.z < 0.7f;
            if (p.kind == BlockKind.Door)
            {
                // The underside of a half-open door: what you slide under.
                Strip(p.name + " (gold)", parent, new Vector3(c.x, p.Min.Y + t * 0.5f, c.z), new Vector3(s.x + 0.02f, t, s.z + 0.02f), gold);
                return;
            }
            if (p.runnable && (thinX || thinZ))
            {
                // A runnable wall: a stripe on both faces at shoulder height.
                float y = Mathf.Min(top - 0.3f, 1.4f);
                Strip(p.name + " (gold stripe)", parent, new Vector3(c.x, y, c.z), new Vector3(s.x + 0.04f, 0.25f, s.z + 0.04f), gold);
                return;
            }
            if (thinX || thinZ)
            {
                // A fence, a hedge, a sawhorse: a cap along its top.
                Strip(p.name + " (gold cap)", parent, new Vector3(c.x, top + t * 0.5f, c.z), new Vector3(s.x + 0.02f, t, s.z + 0.02f), gold);
                return;
            }
            // A ledge: a lip along its four top edges.
            Strip(p.name + " (lip -X)", parent, new Vector3(p.Min.X + w * 0.5f, top + t * 0.5f, c.z), new Vector3(w, t, s.z), gold);
            Strip(p.name + " (lip +X)", parent, new Vector3(p.Max.X - w * 0.5f, top + t * 0.5f, c.z), new Vector3(w, t, s.z), gold);
            Strip(p.name + " (lip -Z)", parent, new Vector3(c.x, top + t * 0.5f, p.Min.Z + w * 0.5f), new Vector3(s.x, t, w), gold);
            Strip(p.name + " (lip +Z)", parent, new Vector3(c.x, top + t * 0.5f, p.Max.Z - w * 0.5f), new Vector3(s.x, t, w), gold);
        }

        static void Strip(string name, Transform parent, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Shape(PrimitiveType type, string name, Transform parent, Vector3 local, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }
}
