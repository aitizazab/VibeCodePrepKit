using System;
using System.Collections.Generic;
using JamKit.DeepBlock;
using JamKit.EditorTools;
using JamKit.Look.EditorTools;
using JamKit.Movement;
using JamKit.Shell;
using JamKit.VisitingHours.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Builds Larch Hill (docs/redesign/world.md; core <see cref="LarchHill"/>), the one hill of Nights 1-3, as the scene
    /// "DreamStreet" (the name the chain looks for): every collider of the hill (boxes, and convex wedges for its slopes;
    /// every wall runs: onlyMarkedWalls is off), the hill drawn in 40 m chunks (the ground, its retaining walls and kerbs,
    /// the houses, buildings, props, trees, fences and the scatter, in the CC0 PBR materials of <see cref="HillMaterials"/>),
    /// the Look agent's golden-hour rig (<see cref="VhLook"/>), trees, grass, terrain ring and soundscape, the Lasname house
    /// (the house builder's own, <c>DreamHouseExterior.Build</c>), the cast (paper cut-outs; Elias at his grill in 3D), the
    /// Saturday-life spots, the moving things (the shutter, the Rewind's wave), the memories, Nadia (with Lena's watch) and
    /// the game. Built a band of chunks at a time within the memory cap (DreamHillStreaming.cs), with a memory and size
    /// report in the log (<see cref="HillBuildReport"/>). A step of "JamKit/Rebuild Shell Scenes"; "JamKit/Dream
    /// Street/Rebuild Scene" rebuilds only it.
    /// </summary>
    public static partial class DreamStreetBuilder
    {
        public const string SceneName = "DreamStreet";
        public const string ScenePath = DreamEscapeBuilder.Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = DreamEscapeBuilder.Root + "/DreamStreetGameConfig.asset";
        public const string MeshFolder = HillMaterials.StreetRoot + "/Generated/Hill";
        /// <summary>The camera's clear colour behind the sky (the Look rig's sky draws over it).</summary>
        public static readonly Color Sky = new Color(1f, 0.82f, 0.66f);

        const float WaltEye = 1.62f, WaltEyeFromTop = 0.2f;
        public const float EliasEye = 1.66f, EliasEyeFromTop = 0.244f, EliasCardHeight = EliasEye / (1f - EliasEyeFromTop);
        /// <summary>The house's cut-outs sized by their eyes (the house builder's numbers: Lena's eyes at 1.60 m, 18.6% down
        /// her card; Theo's at 1.20 m, 26.2% down his).</summary>
        const float LenaCardHeight = 1.6f / (1f - 0.186f), TheoCardHeight = 1.2f / (1f - 0.262f);

        [JamKitBuildStep(247)]
        static void Build(SceneBuilder.Context ctx)
        {
            string scene = BuildEverything(ctx);
            if (string.IsNullOrEmpty(scene)) return;
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Dream Street/Rebuild Scene")]
        static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEverything(null);
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamStreetBuilder.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            int code = 0;
            try { Debug.Log("JAMKIT_STREET_OK " + BuildEverything(null)); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { DreamEscapeBuilder.Root + "/Scenes", DreamEscapeBuilder.MaterialsDir, DreamEscapeBuilder.Root + "/Data", DreamEscapeBuilder.Root + "/Generated", HillMaterials.MatRoot })
                DreamEscapeBuilder.EnsureFolder(d);
            DreamEscapeBuilder.EnsureTuning();
            MakePreset(ctx);
            DreamEscapeBuilder.ClearMaterials();
            HillMaterials.Reset();
            if (AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.DeleteAsset(MeshFolder);
            DreamEscapeBuilder.EnsureFolder(MeshFolder);
            AssetDatabase.SaveAssets();
            string path = BuildScene();
            foreach (var pack in HillMaterials.MissingPacks) Debug.LogWarning("JAMKIT_STREET texture pack missing (noise stands in): " + pack);
            Debug.Log("JAMKIT_STREET_OK " + path);
            return path;
        }

        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Visiting Hours: Larch Hill";
            p.tagline = "Night 1. Larch Hill.";
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
                new HowToPlayPage { title = "Move", body = DreamEscapeBuilder.MovePage },
                new HowToPlayPage { title = "Larch Hill", body = "Find your brother. His house is up the hill; St. Brigid's crowns it. Walk up to things to remember them." },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        // ---------- The scene ----------

        static string BuildScene()
        {
            var report = new HillBuildReport("build");
            HillMeshStore.ResetCounts();
            HillLods.Made = 0;
            var store = new HillMeshStore(MeshFolder);
            HillMeshStore.Current = store;
            try { return BuildScene(report, store); }
            finally { HillMeshStore.Current = null; }
        }

        static string BuildScene(HillBuildReport report, HillMeshStore store)
        {
            var h = LarchHill.Build();
            var scene = SceneBuilder.NewScene(Sky, out var cam);

            var first = new LabCheckpoint { name = "The arrival", position = h.arrival, yaw = h.arrivalYaw };
            var body = DreamEscapeBuilder.BuildPlayer(cam, first);
            body.transform.rotation = Quaternion.Euler(0f, h.arrivalYaw, 0f);
            body.onlyMarkedWalls = false;   // every wall runs (unless a MovementSurface says no): the creator's "most walls you can't run on"
            var watch = KeepsakeWatch(body);

            // The light: the Look agent's golden hour (sun low in the west-south-west, the hill side-lit, its top catching the
            // last light), its sky, post and haze; the camera sees to the terrain ring (4 km).
            var rig = VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 10f, hazeBaseHeight = 0f });
            VhLook.SetupCamera(cam);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 4000f);
            cam.clearFlags = CameraClearFlags.Skybox;   // the Look sky (BuildPlayer clears to a flat colour)
            _ = rig;

            var root = new GameObject("Larch Hill").transform;
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(root, false);
            var drawn = new GameObject("Drawn").transform;
            drawn.SetParent(root, false);
            report.Phase("colliders");

            // Colliders for every solid piece; the switches' (Walt, the shutter, the plank) kept apart for the game.
            GameObject waltGate = null, shutter = null, plank = null;
            var houseStandIn = new List<GameObject>();
            store.Begin("Colliders");
            foreach (var p in h.pieces)
            {
                if (!p.solid) continue;
                var go = Collider(p, colliders);
                if (p.switchIndex == LarchHill.WaltGateSwitch) waltGate = go;
                if (p.switchIndex == LarchHill.ShutterSwitch) shutter = go;
                if (p.switchIndex == LarchHill.CreekPlankSwitch) plank = go;
                if (p.style == "lasname_house") houseStandIn.Add(go);
            }
            store.Flush(false);
            report.Phase("the hill, band by band");

            // The drawing: the hill's chunks, two rows at a time (built, LODs, saved, unloaded, collected).
            var grid = new PieceGrid(h.pieces);
            int chunks = DrawHill(h, grid, drawn, store, report, HillRange(h));
            report.Phase("props", chunks + " chunks, " + HillLods.Made + " LOD meshes");
            var props = Props(h, root, store);
            store.Flush(true);
            report.Phase("trees");
            var trees = Trees(h, root);
            report.Phase("grass");
            var grass = DrawTufts(h, grid, root);
            PersistMeshes(grass.transform, store, "Grass");
            store.Flush(true);
            report.Phase("the terrain ring");
            var ring = Backdrop(h, root);
            PersistMeshes(ring.transform, store, "Backdrop");
            store.Flush(true);
            report.Phase("the house, the cast, the game");

            // The pieces the game moves or switches: drawn on their own objects.
            store.Begin("Pieces");
            if (shutter != null) ShutterLook(shutter.transform);
            if (plank != null) PlankLook(h, plank.transform);
            var hospitalLit = HospitalLit(h, root);

            // The house, the cast, the loop, the memories, the Rewind's wave, Saturday's life.
            var house = House(h, root, houseStandIn, out var parts);
            var cast = new GameObject("The cast (paper cut-outs; Elias in 3D)").transform;
            cast.SetParent(root, false);
            var walt = CutOut("Walt (on his wall)", DreamArt.Walt, cast, U(h.Spot(LarchHill.SpotWalt).position), WaltEye / (1f - WaltEyeFromTop), h.Spot(LarchHill.SpotWalt).yaw);
            var neighbour = CutOut("Mrs. Lindqvist (her roses)", DreamArt.NeighbourRoses, cast, U(h.Spot(LarchHill.SpotNeighbour).position), 1.7f, h.Spot(LarchHill.SpotNeighbour).yaw);
            var dog = CutOut("The dog", DreamArt.Dog, cast, U(h.Spot(LarchHill.SpotDog).position), 0.75f);
            var eliasOut = EliasAtTheGrill(h, cast);
            var sprinkler = Sprinkler(root, U(h.Spot(LarchHill.SpotSprinkler).position));
            var ball = Small("The dog's ball", root, U(h.Spot(LarchHill.SpotBallFrom).position), 0.22f, HillMat.CanvasRed);
            var paper = Memory("Memory: Saturday's paper", root, U(h.Spot(LarchHill.SpotPaper).position), new Vector3(0.42f, 0.05f, 0.3f));
            var ledger = Memory("Memory: Walt's ledger (open)", root, U(h.Spot(LarchHill.SpotLedger).position), new Vector3(0.5f, 0.05f, 0.34f));
            var nail = Memory("Memory: the nail in the lane", root, U(h.Spot(LarchHill.SpotNail).position) + Vector3.up * 0.1f, new Vector3(0.12f, 0.12f, 0.12f));
            var treehouse = Memory("Memory: the treehouse sign", root, U(h.Spot(LarchHill.SpotTreehouse).position) + Vector3.up * 0.9f, new Vector3(0.6f, 0.3f, 0.04f));
            var wave = Wave(root);
            SaturdayLife(h, root);
            Soundscape(h, root);
            Probes(scene);
            store.Flush(true);

            var game = new GameObject("Larch Hill (the night)").AddComponent<DreamStreetGame>();
            game.player = body;
            game.waltGate = waltGate;
            game.walt = walt.transform;
            game.neighbour = neighbour.transform;
            game.dog = dog.transform;
            game.sprinklerJet = sprinkler;
            game.ball = ball.transform;
            game.paper = paper;
            game.ledger = ledger;
            game.nail = nail;
            game.treehouseMemory = treehouse;
            game.patchedBike = new GameObject("The patched bike (marker)").transform;
            game.patchedBike.SetParent(root, false);
            game.patchedBike.position = U(h.Spot(LarchHill.SpotPatchedBike).position);
            game.shutter = shutter != null ? shutter.transform : null;
            game.wave = wave;
            game.creekPlank = plank;
            game.hospitalLit = hospitalLit;
            game.eliasOutside = eliasOut;
            game.eliasInside = parts.elias;
            game.theoInside = parts.theo;
            game.lenaInside = parts.lena;
            game.frontDoor = parts.door;
            game.frontDoorOpenYaw = parts.doorOpenYaw;
            game.porchLight = parts.light;
            game.porchGlow = parts.glow;
            game.houseRoot = house;
            game.keepsakeWatch = watch;

            // Occlusion and probes: the hill's chunks, props, trees and the ring are static (main bakes occlusion after this).
            MarkStatic(drawn);
            MarkStatic(props);
            MarkStatic(trees);
            MarkStatic(ring.transform);

            report.Phase("save");
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Phase("totals");
            report.SceneTotals(scene);
            report.Disk(MeshFolder, ScenePath);
            report.Finish();
            return ScenePath;
        }

        /// <summary>Lena's star watch on Nadia's right wrist (People's <c>VhKeepsakeWatch</c>), built on her first-person body
        /// and hidden: the game shows it when the keepsake is hers (<see cref="DreamStreetGame.SetKeepsakeOwned"/>).</summary>
        static VhKeepsakeWatch KeepsakeWatch(MovementController body)
        {
            Transform model = null;
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                if (t.name == "Nadia (body)") { model = t; break; }
            if (model == null) { Debug.LogWarning("JAMKIT_STREET no first-person body ('Nadia (body)') for Lena's watch"); return null; }
            var w = VhKeepsakeWatch.Ensure(model, false);
            Debug.Log("JAMKIT_STREET Lena's watch " + (w != null ? "on Nadia's right wrist (hidden until the keepsake is hers)" : "MISSING (no right forearm on the body)"));
            return w;
        }

        /// <summary>Elias at his barbecue: the People agent's 3D model (Resources/People/Elias, idle at the grill); a paper
        /// cut-out if the prefab isn't built yet.</summary>
        static GameObject EliasAtTheGrill(LarchHill h, Transform cast)
        {
            var spot = h.Spot(LarchHill.SpotBarbecue);
            var prefab = Resources.Load<GameObject>("People/Elias");
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = "Elias (at his barbecue, 3D)";
                go.transform.SetParent(cast, false);
                go.transform.SetPositionAndRotation(U(spot.position), Quaternion.Euler(0f, spot.yaw, 0f));
                Debug.Log("JAMKIT_STREET Elias at the grill: the 3D model");
                return go;
            }
            Debug.LogWarning("JAMKIT_STREET no Resources/People/Elias prefab (run VhPeopleBuild): Elias stays a cut-out");
            return CutOut("Elias (at his barbecue: a cut-out until his 3D prefab is built)", DreamArt.Elias, cast, U(spot.position), EliasCardHeight, spot.yaw);
        }

        /// <summary>The Look agent's golden-hour Saturday (bed, birds, wind in the trees, a far mower, the little-league
        /// game, the ice-cream van) over the middle of Night 1, plus the hill's own: the Lindqvists' sprinkler, the
        /// Fairbanks' mower, a dog by the dog, birds over the middle band, wind in the trees above Rowan Street.</summary>
        static void Soundscape(LarchHill h, Transform root)
        {
            var amb = VhLookAmbience.AddSaturdayDefault(root, new Vector3(40f, LarchHill.MillY + 3f, 40f), 3);
            VhLookAmbience.AddEmitter(amb, VhLookAmbience.Kind.Sprinkler, U(h.Spot(LarchHill.SpotSprinkler).position) + Vector3.up * 0.3f, 0.9f);
            VhLookAmbience.AddEmitter(amb, VhLookAmbience.Kind.Lawnmower, new Vector3(99f, 4.6f, 56f), 0.8f);
            VhLookAmbience.AddEmitter(amb, VhLookAmbience.Kind.Dog, U(h.Spot(LarchHill.SpotDog).position) + Vector3.up * 0.5f, 0.7f, 0);
            VhLookAmbience.AddEmitter(amb, VhLookAmbience.Kind.Birds, new Vector3(0f, 30f, 200f), 0.9f);
            VhLookAmbience.AddEmitter(amb, VhLookAmbience.Kind.TreeWind, new Vector3(-20f, 14f, 100f), 0.8f);
        }

        static void Safely(string what, Action draw)
        {
            try { draw(); }
            catch (Exception e) { Debug.LogWarning("JAMKIT_STREET couldn't draw " + what + ": " + e.Message); }
        }

        // ---------- Colliders ----------

        /// <summary>A piece's collider: a box, or for a slope a convex wedge (the box with its top following the slope);
        /// every one with a MovementSurface saying whether its walls run.</summary>
        static GameObject Collider(BlockPiece p, Transform parent)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(parent, false);
            if (p.rampAxis == 0)
            {
                go.transform.position = U(p.center);
                go.AddComponent<BoxCollider>().size = U(p.size);
            }
            else
            {
                go.transform.position = U(p.center);
                var mesh = new Mesh { name = p.name + " (wedge)" };
                var mn = U(p.Min) - U(p.center);
                var mx = U(p.Max) - U(p.center);
                float T(float x, float z) => p.TopAt(x + p.center.X, z + p.center.Z) - p.center.Y;
                mesh.vertices = new[]
                {
                    new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z),
                    new Vector3(mn.x, T(mn.x, mn.z), mn.z), new Vector3(mx.x, T(mx.x, mn.z), mn.z), new Vector3(mx.x, T(mx.x, mx.z), mx.z), new Vector3(mn.x, T(mn.x, mx.z), mx.z),
                };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
                mesh.RecalculateBounds();
                if (HillMeshStore.Current != null) HillMeshStore.Current.Add(mesh, true);
                else AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(MeshFolder + "/wedge.asset"));
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            go.AddComponent<MovementSurface>().runnable = p.runnable;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        // ---------- The moving and switching things ----------

        /// <summary>The Old Mill's shutter: its pivot at its bottom edge (the game scales it to the gap), corrugated steel.</summary>
        static void ShutterLook(Transform shutter)
        {
            var box = shutter.GetComponent<BoxCollider>();
            var size = box.size;
            float x = shutter.position.x, z = shutter.position.z;
            shutter.position = new Vector3(x, LarchHill.LaneY, z);
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(size.x, 1f, size.z);
            shutter.localScale = new Vector3(1f, LarchHill.ArchTop - LarchHill.LaneY, 1f);
            // It moves every frame: a kinematic body, so physics does not rebuild the static scene for it (the Perf agent's find).
            var moving = shutter.gameObject.AddComponent<Rigidbody>();
            moving.isKinematic = true;
            moving.useGravity = false;
            var m = new HillMesh("The shutter (look)");
            m.Box(HillMat.Corrugated, new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f), new Vector3(size.x * 0.5f, 1f, size.z * 0.5f));
            var look = m.Build(shutter, MeshFolder);
            look.transform.localPosition = Vector3.zero;
        }

        static void PlankLook(LarchHill h, Transform plank)
        {
            var p = h.pieces.Find(x => x.switchIndex == LarchHill.CreekPlankSwitch);
            var m = new HillMesh("The old plank (look)");
            m.Push(-U(p.center), Quaternion.identity);
            m.Box(HillMat.FenceWood, U(p.Min), U(p.Max), 0.02f);
            m.Pop();
            m.Build(plank, MeshFolder);
        }

        /// <summary>St. Brigid's lit windows (on when she told him his patients ask after him): warm bands over its south face
        /// and the tower's.</summary>
        static GameObject HospitalLit(LarchHill h, Transform root)
        {
            var b = h.buildings.Find(x => x.kind == HillBuildingKind.Hospital);
            var m = new HillMesh("St. Brigid's lit windows");
            if (b != null)
            {
                var r = new System.Random(8);
                float front = b.min.Y - 0.08f;
                for (int floor = 1; floor < 8; floor++)
                    for (float x = b.min.X + 2f; x < b.max.X - 3f; x += 3.2f)
                    {
                        if (r.Next(3) == 0) continue;
                        float y = b.floorY + floor * 3.5f + 1.1f;
                        m.Rect(HillMat.LampGlow, new Vector3(x + 1.2f, y, front), new Vector3(2.2f, 0f, 0f), new Vector3(0f, 1.5f, 0f), Vector3.back);
                    }
            }
            var go = m.Build(root, MeshFolder);
            foreach (var r2 in go.GetComponentsInChildren<MeshRenderer>()) r2.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        /// <summary>The Rewind's wave: a tall curtain of gold light across Rowan Street (the game moves it east).</summary>
        static Transform Wave(Transform root)
        {
            var t = new GameObject("The Rewind (the wave)").transform;
            t.SetParent(root, false);
            t.position = new Vector3(LarchHill.RewindFromX, LarchHill.RowanY, (LarchHill.LaneZ1 + LarchHill.RowanNorthZ) * 0.5f + 3f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hill_rewind_wave" };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 1f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent + 10;
            mat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.5f, 0.42f));
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HillMaterials.TexRoot + "/gen_leaf_decals/gen_leaf_decals_color.png");
            if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.SetTextureScale("_BaseMap", new Vector2(6f, 2f)); }
            AssetDatabase.CreateAsset(mat, HillMaterials.MatRoot + "/Hill_rewind_wave.mat");
            for (int i = 0; i < 3; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "The wave (sheet " + (i + 1) + ")";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(t, false);
                q.transform.localPosition = new Vector3(-i * 1.4f, 7f, 0f);
                q.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                q.transform.localScale = new Vector3(26f, 14f, 1f);
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var light = new GameObject("The wave's light").AddComponent<Light>();
            light.transform.SetParent(t, false);
            light.transform.localPosition = new Vector3(0.5f, 2.5f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.5f);
            light.range = 16f;
            light.intensity = 4f;
            light.shadows = LightShadows.None;
            t.gameObject.SetActive(false);
            return t;
        }

        // ---------- Saturday's life (spots for the People agent's prefabs; cut-outs where we have art) ----------

        static void SaturdayLife(LarchHill h, Transform root)
        {
            var life = new GameObject("Saturday's life (spots)").transform;
            life.SetParent(root, false);
            void Spot(string role, string activity, Vector3 at, float yaw, params Vector3[] path)
            {
                var s = new GameObject("Spot: " + role + " (" + activity + ")").AddComponent<HillPersonSpot>();
                s.transform.SetParent(life, false);
                s.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
                s.role = role;
                s.activity = activity;
                s.path = path;
            }
            float mill = LarchHill.MillY + 0.15f, rowan = LarchHill.RowanY, rowanFoot = LarchHill.RowanY + 0.15f, rb = 4.6f;
            Spot("okonkwo", "WashCar", new Vector3(78.5f, mill, 50.6f), 90f);
            Spot("fairbanks", "Mow", new Vector3(99f, rb, 56f), 90f, new Vector3(93f, rb, 56f), new Vector3(105f, rb, 56f));
            Spot("ruiz", "YardSale", new Vector3(116f, rb, 55.5f), 180f);
            Spot("kid_bike_1", "RideBike", new Vector3(-60f, LarchHill.MillY, 48f), 90f, new Vector3(-120f, LarchHill.MillY, 47f), new Vector3(120f, LarchHill.MillY, 47f), new Vector3(120f, LarchHill.MillY, 49.5f), new Vector3(-120f, LarchHill.MillY, 49.5f));
            Spot("kid_bike_2", "RideBike", new Vector3(30f, LarchHill.MillY, 49.5f), 270f, new Vector3(120f, LarchHill.MillY, 49.5f), new Vector3(-120f, LarchHill.MillY, 49.5f));
            Spot("allotment_old_man", "Garden", new Vector3(40f, rb, 70f), 200f);
            Spot("lindqvist", "WaterRoses", U(h.Spot(LarchHill.SpotNeighbour).position), -130f);
            Spot("walt", "SitOnWall", U(h.Spot(LarchHill.SpotWalt).position), 90f);
            Spot("elias", "Barbecue", U(h.Spot(LarchHill.SpotBarbecue).position), 210f);
            for (int i = 0; i < 8; i++)
            {
                float x = -96f + i * 11f;
                Spot("party_" + (i + 1), i % 3 == 0 ? "Dance" : i % 3 == 1 ? "ChatStanding" : "SitAtTable", new Vector3(x, rowan, 109f + (i % 2) * 2f), i * 45f);
            }
            Spot("party_grill", "Barbecue", new Vector3(-50f, rowan, 112.4f), 180f);
            // The one neighbour we have a picture for at the party: the man at its grill (the rest wait for their cut-out art).
            CutOut("A neighbour at the party's grill", DreamArt.NeighbourGrill, life, new Vector3(-50f, rowan, 112.4f), 1.78f, 180f);
            Spot("lemonade_kid", "Stall", new Vector3(-44f, rowanFoot, 114.6f), 180f);
            Spot("bus_driver", "SitInBus", new Vector3(114f, HillAnchors.M3Y + 1f, 230f), 180f);
            // Barbecue smoke over Eli's grill and the party's.
            var grill = h.props.Find(p => p.kind == HillPropKind.Grill && p.tag == "elias's barbecue");
            Smoke(root, grill != null ? U(grill.position) + Vector3.up * 1.1f : new Vector3(40.2f, LarchHill.RowanY + 1.6f, 124.8f));
            Smoke(root, new Vector3(-50f, LarchHill.RowanY + 1.0f, 111.5f));
        }

        static void Smoke(Transform root, Vector3 at)
        {
            var go = new GameObject("Barbecue smoke");
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 4f;
            main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startColor = new Color(0.92f, 0.9f, 0.88f, 0.28f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.15f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(0.25f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(HillMaterials.MatRoot + "/Hill_smoke.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Hill_smoke" };
                mat.SetFloat("_Surface", 1f);
                mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.5f));
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(HillMaterials.TexRoot + "/gen_stains/gen_stains_color.png");
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                AssetDatabase.CreateAsset(mat, HillMaterials.MatRoot + "/Hill_smoke.mat");
            }
            r.sharedMaterial = mat;
        }

        /// <summary>One reflection probe per street (the Look agent's).</summary>
        static void Probes(UnityEngine.SceneManagement.Scene scene)
        {
            VhLook.AddReflectionProbe(scene, new Vector3(97f, 3f, 0f), new Vector3(80f, 14f, 60f));
            VhLook.AddReflectionProbe(scene, new Vector3(50f, 3f, 28f), new Vector3(90f, 12f, 30f));
            VhLook.AddReflectionProbe(scene, new Vector3(0f, 7f, 48f), new Vector3(280f, 12f, 16f));
            VhLook.AddReflectionProbe(scene, new Vector3(-60f, 7f, 96f), new Vector3(110f, 10f, 14f));
            VhLook.AddReflectionProbe(scene, new Vector3(-30f, 12f, 110f), new Vector3(200f, 12f, 20f));
            VhLook.AddReflectionProbe(scene, new Vector3(0f, 30f, 220f), new Vector3(280f, 40f, 150f));
            VhLook.AddReflectionProbe(scene, new Vector3(0f, 55f, 320f), new Vector3(280f, 40f, 80f));
        }

        // ---------- Little things ----------

        static Transform Sprinkler(Transform root, Vector3 at)
        {
            var m = new HillMesh("The sprinkler");
            m.Cylinder(HillMat.MetalPaint, at, at + Vector3.up * 0.12f, 0.09f, 10);
            m.Build(root, MeshFolder);
            var jet = new GameObject("The sprinkler's jet").transform;
            jet.SetParent(root, false);
            jet.position = at + new Vector3(0f, 0.15f, 0f);
            var ps = jet.gameObject.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 5.5f;
            main.startSize = 0.06f;
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.55f);
            main.gravityModifier = 1f;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 180f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-50f, 0f, 0f);
            var r = jet.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(HillMaterials.MatRoot + "/Hill_smoke.mat");
            return jet;
        }

        static GameObject Small(string name, Transform root, Vector3 at, float size, string mat)
        {
            var m = new HillMesh(name);
            m.Ellipsoid(mat, Vector3.zero, Vector3.one * size * 0.5f, 10);
            var go = m.Build(root, MeshFolder);
            go.transform.position = at;
            return go;
        }

        /// <summary>A memory you walk up to: the thing itself (a paper, a book, a nail, a carved board) with a soft glow
        /// and a little light so it reads from a distance.</summary>
        static GameObject Memory(string name, Transform root, Vector3 at, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var m = new HillMesh(name + " (look)");
            m.Box(HillMat.Canvas, -size * 0.5f, size * 0.5f, 0.005f);
            m.Ellipsoid(HillMat.LampGlow, Vector3.zero, Vector3.one * 0.05f, 6);
            var look = m.Build(go.transform, MeshFolder);
            look.transform.localPosition = Vector3.zero;
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = Vector3.up * 0.25f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.range = 1.8f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            return go;
        }

        // ---------- The house on Rowan Street ----------

        public struct HouseParts
        {
            public Transform elias, theo, lena, door;
            public float doorOpenYaw;
            public Light light;
            public GameObject glow;
        }

        /// <summary>
        /// The Lasname house at its anchor: the house builder's own house (<c>DreamHouseExterior.Build</c>, called by name so
        /// its changes don't break this build; a kit house of the same footprint stands in if it's missing), with what the
        /// night needs on top: the family through the front window (cut-outs placed in the house's front room), its open
        /// front door put on a hinge so the night can shut it, and the porch light by its lantern. Round 2 (the House
        /// agent's request): this scene's own front wall and the room behind the window are gone (they overlapped the
        /// house); when the house builder's house is there, the core's stand-in box colliders are removed too (its own
        /// colliders are the house).
        /// </summary>
        static Transform House(LarchHill h, Transform root, List<GameObject> standIn, out HouseParts parts)
        {
            var house = new GameObject("The Lasname house").transform;
            house.SetParent(root, false);
            bool built = false;
            var t = Type.GetType("JamKit.DreamEscape.EditorTools.DreamHouseExterior, JamKit.DreamEscape.Editor");
            var build = t?.GetMethod("Build", new[] { typeof(Transform) });
            if (build != null)
            {
                try { build.Invoke(null, new object[] { house }); built = true; }
                catch (Exception e) { Debug.LogWarning("JAMKIT_STREET the house builder's outside failed: " + e.InnerException?.Message); }
            }
            foreach (var c in house.GetComponentsInChildren<Transform>(true))
                if (c != null && c != house && c.name.StartsWith("Lawn (beyond")) Object.DestroyImmediate(c.gameObject);
            Transform door = null;
            float openYaw = -95f;
            if (built)
            {
                foreach (var go in standIn) if (go != null) Object.DestroyImmediate(go);
                // Its lamps: two keep their shadows at most (Perf), small things don't cast.
                JamKit.Perf.EditorTools.VhPerfShadows.LimitShadowedLights(house, 2);
                JamKit.Perf.EditorTools.VhPerfShadows.Apply(house, JamKit.Perf.EditorTools.VhPerfShadows.Rules.Indoor);
                // The house builder's front door stands open against the hall (a leaf at x 0.6-0.65, z 0.17-1.37, hinged at
                // the right jamb): on a hinge there, shut it swings 90 degrees across the doorway.
                foreach (var c in house.GetComponentsInChildren<Transform>(true))
                    if (c.name == "The front door (open)") { door = c; break; }
                if (door != null)
                {
                    var hinge = new GameObject("The front door (on its hinge)").transform;
                    hinge.SetParent(house, false);
                    hinge.localPosition = new Vector3(0.625f, 0f, 0.17f);
                    openYaw = 90f;
                    hinge.localRotation = Quaternion.Euler(0f, openYaw, 0f);
                    door.SetParent(hinge, true);
                    door = hinge;
                }
            }
            else
            {
                var stand = new HillHouse
                {
                    name = "The Lasname house (stand-in)", center = new Vector3(-0.8f, 0f, 7.7f).ToNumerics(), width = 13.6f, depth = 15.4f, facing = HillFacing.South,
                    storeys = 1.5f, roof = HillRoof.GableFront, pitch = 38f, wall = HillMat.SidingRose, roofMat = HillMat.RoofRed, porch = false, plinth = 0f, seed = 5,
                };
                var m = new HillMesh("The Lasname house (stand-in)");
                HillHouses.Draw(m, stand);
                m.Build(house, MeshFolder);
            }
            if (door == null)
            {
                var hinge = new GameObject("The front door (on its hinge)").transform;
                hinge.SetParent(house, false);
                hinge.localPosition = new Vector3(-0.6f, 0f, 0.2f);
                var dm = new HillMesh("The front door (look)");
                dm.Box(HillMat.DoorRed, new Vector3(0f, 0f, -0.025f), new Vector3(1.2f, 2.22f, 0.025f), 0.01f);
                dm.Build(hinge, MeshFolder);
                hinge.localRotation = Quaternion.Euler(0f, openYaw, 0f);
                door = hinge;
            }
            // The family in the front room, seen through its window from the gate (the game shows them at the end).
            var family = new GameObject("The family (through the front window)").transform;
            family.SetParent(house, false);
            var theo = CutOut("Theo (setting the table)", DreamArt.Theo, family, new Vector3(-2.4f, 0f, 2.6f), TheoCardHeight);
            var elias = CutOut("Elias (at the stove)", DreamArt.Elias, family, new Vector3(-4.5f, 0f, 2.7f), EliasCardHeight);
            var lena = CutOut("Lena (out of sight)", DreamArt.Lena, family, new Vector3(-7.0f, 0f, 2.4f), LenaCardHeight);
            // The porch light by the house builder's lantern (x 0.95-1.25, y 1.95-2.35, z -0.37 to -0.15), its glow inside it.
            var porch = new GameObject("The porch light").AddComponent<Light>();
            porch.transform.SetParent(house, false);
            porch.transform.localPosition = new Vector3(1.1f, 2.15f, -0.55f);
            porch.type = LightType.Point;
            porch.color = new Color(1f, 0.84f, 0.58f);
            porch.range = 7f;
            porch.intensity = 2.4f;
            porch.shadows = LightShadows.None;
            var gm = new HillMesh("The porch light (glow)");
            gm.Ellipsoid(HillMat.LampGlow, built ? new Vector3(1.1f, 2.15f, -0.26f) : new Vector3(1.85f, 2.3f, -0.12f), Vector3.one * (built ? 0.07f : 0.14f), 8);
            var glow = gm.Build(house, MeshFolder);
            house.position = U(h.houseOrigin);
            parts = new HouseParts { elias = elias.transform, theo = theo.transform, lena = lena.transform, door = door, doorOpenYaw = openYaw, light = porch, glow = glow };
            return house;
        }

        // ---------- The cast ----------

        /// <summary>A billboard (docs/REDESIGN.md section 7: in the dream only Nadia and Elias are 3D; Walt, Lena, Theo and the
        /// neighbours are paper cut-outs, the People agent's DreamCutoutView: it turns, a beat late, to watch her, and breathes).
        /// Elias's becomes his 3D model when it lands.</summary>
        static GameObject CutOut(string name, string art, Transform parent, Vector3 feet, float height, float restYaw = 180f)
        {
            var tex = DreamArt.Get(art, out bool placeholder);
            Debug.Log("JAMKIT_STREET art: " + art + (placeholder ? " PLACEHOLDER" : " real"));
            var mat = DreamEscapeBuilder.Mat("Cutout_" + art, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", tex);
            float aspect = tex != null && tex.height > 0 ? tex.width / (float)tex.height : 0.5f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            var view = go.AddComponent<DreamCutoutView>();
            view.restYaw = restYaw;
            go.transform.rotation = Quaternion.Euler(0f, restYaw, 0f);
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "Card";
            Object.DestroyImmediate(card.GetComponent<Collider>());
            card.transform.SetParent(go.transform, false);
            card.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            card.transform.localScale = new Vector3(height * aspect, height, 1f);
            card.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            var r = card.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            view.card = card.transform;
            return go;
        }
    }

    static class HillVectorExt
    {
        public static System.Numerics.Vector3 ToNumerics(this Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
    }
}
