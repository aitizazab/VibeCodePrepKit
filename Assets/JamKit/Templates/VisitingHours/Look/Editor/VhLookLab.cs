using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// LookLab: a lit street corner for judging the look (Look/Scenes/LookLab.unity). Pass 2, after the judge's round 1:
    /// <list type="bullet">
    /// <item>The ground is a Unity Terrain with four blended layers (lawn, dry gold grass, dirt, clover) under a large
    /// variation mask, with grass and wildflower tufts in drifts and as overhang along every paving edge, leaf litter in the
    /// gutters, oil and dirt stains, kerb-inlet storm drains, an iron manhole.</item>
    /// <item>The cross street climbs the hill (kerbs and sidewalks with it) to a second street halfway up and a crest road at
    /// the top: no dead end. Houses (VhLookHouses, far LOD: windows, doors, eaves, trim, porches, chimneys) line both, set
    /// back on lots, with driveways; stands of trees of four species between them.</item>
    /// <item>The world is closed by a terrain ring to ~2.9 km (VhLookBackdrop): farmland hills, tree stands, wooded ridges,
    /// dissolving into the far haze.</item>
    /// <item>The main house and the bungalow next door are VhLookHouses at full detail; trees are VhLookTrees' species.</item>
    /// <item>A golden-hour Saturday soundscape (VhLookAmbience) and a frame-time probe path (VhPerfPath) along the shots.</item>
    /// </list>
    /// Built by <see cref="Build"/>, captured by <see cref="Capture"/> (out/look-captures/&lt;time&gt;/): the ten shots of
    /// round 1 (same views, for comparison) plus two: into the sun through a crown, and a wide street view.
    /// </summary>
    public static class VhLookLab
    {
        public const string ScenePath = VhLookLibrary.LookRoot + "Scenes/LookLab.unity";
        public const string TerrainPath = VhLookLibrary.LookRoot + "Scenes/LookLab_Terrain.asset";

        // The terrain's extent (x/z) and the hill streets.
        static readonly Rect TerrainRect = new Rect(-340f, -90f, 680f, 740f);
        const float CrossX = 30f, RowanZ = 200f, CrestZ = 622f;

        [MenuItem("JamKit/Look/Build LookLab")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static string Build()
        {
            VhLookLibrary.Build();
            VhLookAmbience.ConfigureImporters();
            VhLookSetup.InstallRendererFeatures();
            VhLookProfiles.BuildAll();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var world = new GameObject("LookLab (street corner)").transform;
            Terrain(world);
            Street(world);
            HillStreets(world);
            var mainSpec = VhLookHouses.Spec.Clapboard(new Color(0.72f, 0.8f, 0.66f), 2);
            mainSpec.width = 14f; mainSpec.depth = 10f; mainSpec.roofRise = 3.3f;
            mainSpec.roof = VhLookLibrary.Get("Roof_ShinglesAsphalt");
            mainSpec.door = VhLookLibrary.Paint("DoorRed", new Color(0.55f, 0.13f, 0.1f), 1.4f);
            VhLookHouses.Build(world, new Vector3(-8f, 0f, 18f), 0f, mainSpec, VhLookHouses.Lod.Full, Ground, "House (clapboard, two storeys)");
            var bungalow = VhLookHouses.Spec.Brick(5);
            bungalow.width = 16f; bungalow.depth = 11f;
            VhLookHouses.Build(world, new Vector3(-34f, 0f, 17f), 0f, bungalow, VhLookHouses.Lod.Full, Ground, "Neighbour (brick bungalow)");
            Fences(world);
            Planting(world);
            HillTrees(world);
            GroundDetail(world);
            var car = VhLookLibrary.Prop("CoveredCar", world, new Vector3(10f, 0.11f, 15.5f), 0f);
            if (car != null) AlignLongAxisToZ(car);
            var manhole = VhLookLibrary.Prop("ManholeCover", world, new Vector3(16f, 0.004f, 1.6f), 0f);
            if (manhole != null) { var c = manhole.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c); }

            VhLookBackdrop.Build(world, new VhLookBackdrop.Options
            {
                center = new Vector3(0f, 0f, 280f), innerRadius = 330f, outerRadius = 2900f, nearHeight = Ground, hole = TerrainRect,
                blendDistance = 650f, seed = 7, roads = FarRoads(),
            });

            // Warm lamps inside the houses (seen through the windows).
            Lamp(world, new Vector3(-3f, 2.6f, 21f), 2.2f);
            Lamp(world, new Vector3(-26f, 2.2f, 21f), 1.6f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(24f, 1.7f, -10f);
            camGo.transform.LookAt(new Vector3(-2f, 4f, 20f));
            cam.fieldOfView = 60f;
            VhLook.SetupCamera(cam);

            VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 10f });
            VhLook.AddReflectionProbe(scene, new Vector3(0f, 4f, 2f), new Vector3(120f, 16f, 30f), 1, "Reflection probe (street)");
            VhLook.AddReflectionProbe(scene, new Vector3(-1f, 3.5f, 23f), new Vector3(14f, 6f, 10f), 2, "Reflection probe (house)");
            VhLookAmbience.AddSaturdayDefault(world, new Vector3(5f, 0f, 10f), 1);
            // Probes round the street only (the hill and the ring are lit by the sky; a global volume over 0.5 km2 of
            // terrain would make the bake slow and heavy).
            var pvGo = new GameObject("Probe volume (the street)");
            pvGo.transform.SetParent(world, false);
            pvGo.transform.position = new Vector3(0f, 8f, 12f);
            var pv = pvGo.AddComponent<ProbeVolume>();
            pv.mode = ProbeVolume.Mode.Local;
            pv.size = new Vector3(150f, 22f, 70f);
            PerfPath(world);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("JAMKIT_LOOK LookLab built: " + ScenePath);
            return ScenePath;
        }

        /// <summary>The streets carry on out of the scene into the ring (round 2: "the road stops in a field"): the main road
        /// both ways, curving off between the fields, and Rowan Street and the crest road a little way each side.</summary>
        static List<VhLookBackdrop.FarRoad> FarRoads()
        {
            VhLookBackdrop.FarRoad R(float startY, params float[] xz)
            {
                var pts = new Vector2[xz.Length / 2];
                for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
                return new VhLookBackdrop.FarRoad { points = pts, startY = startY };
            }
            return new List<VhLookBackdrop.FarRoad>
            {
                R(0f, 333f, 0f, 520f, -6f, 760f, -40f, 1050f, -120f, 1500f, -300f, 2100f, -560f, 2900f, -820f),
                R(0f, -333f, 0f, -520f, 8f, -780f, 52f, -1100f, 150f, -1600f, 360f, -2300f, 650f, -3000f, 900f),
                R(RowanY(330f), 328f, RowanZ, 520f, RowanZ + 20f, 800f, RowanZ + 110f, 1200f, RowanZ + 330f),
                R(RowanY(-330f), -328f, RowanZ, -540f, RowanZ - 10f, -820f, RowanZ + 60f, -1250f, RowanZ + 240f),
                R(CrestY(330f), 328f, CrestZ, 560f, CrestZ + 40f, 900f, CrestZ + 180f),
                R(CrestY(-330f), -328f, CrestZ, -560f, CrestZ + 30f, -880f, CrestZ + 150f),
            };
        }

        static void Lamp(Transform world, Vector3 p, float intensity)
        {
            var lamp = new GameObject("Lamp inside (warm)").AddComponent<Light>();
            lamp.transform.SetParent(world, false);
            lamp.transform.position = p;
            lamp.type = LightType.Point;
            lamp.range = 7f;
            lamp.intensity = intensity;
            lamp.color = new Color(1f, 0.72f, 0.45f);
            lamp.shadows = LightShadows.None;
        }

        // ---------- The capture ----------

        static readonly Vector3 ToSun = VhLookRig.Direction(10f, 250f);

        public static List<VhLookCapture.Shot> Shots()
        {
            Vector3 eye8 = new Vector3(0f, 1.7f, 0f);
            // Shot 11: under the corner tree's crown edge, the sun low through its lower leaves.
            var eye11 = new Vector3(20f, 1.5f, 10f) - new Vector3(ToSun.x, 0f, ToSun.z).normalized * 12f;
            return new List<VhLookCapture.Shot>
            {
                new VhLookCapture.Shot("01-street-corner", new Vector3(24f, 1.7f, -10f), new Vector3(-2f, 4f, 20f), 60f),
                new VhLookCapture.Shot("02-house-sunlit-side", new Vector3(-21f, 1.7f, -3f), new Vector3(-1f, 4f, 22f), 55f),
                new VhLookCapture.Shot("03-into-the-sun-tree", new Vector3(20f, 1.6f, 18f), new Vector3(0f, 6f, 11f), 60f),
                new VhLookCapture.Shot("04-facade-detail", new Vector3(-4.2f, 1.7f, 13.2f), new Vector3(-5.6f, 3.0f, 18f), 50f),
                new VhLookCapture.Shot("05-kerb-low", new Vector3(-30f, 0.55f, -5.1f), new Vector3(10f, 0.25f, -4.3f), 55f),
                new VhLookCapture.Shot("06-hill-vista", new Vector3(30f, 14f, -20f), new Vector3(0f, 22f, 250f), 60f),
                new VhLookCapture.Shot("07-driveway-car-fence", new Vector3(16.5f, 1.7f, 8.5f), new Vector3(9f, 1.0f, 18f), 55f),
                new VhLookCapture.Shot("08-sky-sunward", eye8, eye8 + new Vector3(ToSun.x, 0.32f, ToSun.z), 70f),
                new VhLookCapture.Shot("09-sky-away", eye8, eye8 + new Vector3(-ToSun.x, 0.35f, -ToSun.z), 70f),
                new VhLookCapture.Shot("10-cross-street-uphill", new Vector3(31f, 1.7f, 2f), new Vector3(29f, 4f, 80f), 60f),
                new VhLookCapture.Shot("11-into-the-sun-foliage", eye11, eye11 + ToSun * 20f + Vector3.up * 0.6f, 55f),
                new VhLookCapture.Shot("12-street-wide", new Vector3(62f, 4.5f, -14f), new Vector3(-10f, 6f, 45f), 65f),
            };
        }

        [MenuItem("JamKit/Look/Capture LookLab")]
        static void CaptureMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.RevealInFinder(Capture());
        }

        public static string Capture()
        {
            if (!File.Exists(ScenePath)) Build();
            return VhLookCapture.Capture(ScenePath, Shots(), "look-captures");
        }

        static void PerfPath(Transform world)
        {
            var go = new GameObject("Perf path (the shots)");
            go.transform.SetParent(world, false);
            var path = go.AddComponent<VhPerfPath>();
            var shots = Shots();
            path.eyes = new Vector3[shots.Count];
            path.looks = new Vector3[shots.Count];
            for (int i = 0; i < shots.Count; i++) { path.eyes[i] = shots[i].eye; path.looks[i] = shots[i].look; }
            path.secondsPerLeg = 3.5f;
            path.fieldOfView = 60f;
        }

        // ---------- Batch entry points (main runs these; see docs/redesign/look.md) ----------

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.BuildFromCommandLine (imports, library, renderer feature,
        /// profiles, the LookLab scene). No GPU work beyond importing.</summary>
        public static void BuildFromCommandLine() => Run(() => { Build(); return 0; });

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.BakeFromCommandLine (GPU: probes, reflections, ambient).</summary>
        public static void BakeFromCommandLine() => Run(() => VhLook.BakeScene(ScenePath, 3f) ? 0 : 2);

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.CaptureFromCommandLine (GPU: twelve 1920x1080 stills).</summary>
        public static void CaptureFromCommandLine() => Run(() => { Debug.Log("JAMKIT_LOOK_CAPTURE_OK " + Capture()); return 0; });

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.AllFromCommandLine: build, bake, capture in one run.</summary>
        public static void AllFromCommandLine() => Run(() =>
        {
            Build();
            bool baked = VhLook.BakeScene(ScenePath, 3f);
            Debug.Log("JAMKIT_LOOK_CAPTURE_OK " + Capture() + (baked ? "" : " (bake failed: unbaked lighting)"));
            return baked ? 0 : 2;
        });

        /// <summary>
        /// -executeMethod JamKit.Look.EditorTools.VhLookLab.BuildPerfPlayerFromCommandLine [-vhOut out/look-perf]: a Windows
        /// player of LookLab alone, for the frame-time probe (run it with -perf-probe; see VhPerfProbe).
        /// </summary>
        public static void BuildPerfPlayerFromCommandLine() => Run(() =>
        {
            if (!File.Exists(ScenePath)) Build();
            string dir = VhLookArgs.Get("-vhOut") ?? Path.Combine("out", "look-perf");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(dir, "LookLab.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("JAMKIT_LOOK perf player: " + report.summary.result + " -> " + Path.GetFullPath(options.locationPathName) +
                      " (" + (report.summary.totalSize / (1024 * 1024)) + " MB)");
            return report.summary.result == BuildResult.Succeeded ? 0 : 1;
        });

        static void Run(Func<int> body)
        {
            int code;
            try { code = body(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        // ---------- The ground ----------

        /// <summary>The hill's own shape (0 on the flat street, rising north from z = 45).</summary>
        static float Hill(float x, float z)
        {
            float rise = Mathf.Max(0f, z - 45f);
            float h = rise * 0.11f + rise * rise * 0.00008f;
            h += (Mathf.PerlinNoise(x * 0.012f + 3.1f, z * 0.012f + 7.7f) - 0.5f) * Mathf.Min(1f, rise / 60f) * 14f;
            h += Mathf.Abs(x) * 0.02f * Mathf.Min(1f, rise / 80f);
            return h;
        }

        static float Natural(float x, float z) => 0.1f + Hill(x, z);

        /// <summary>The cross street's surface height (the hill smoothed along it), 0 on the flat.</summary>
        static float CrossRoadY(float z)
        {
            float s = 0f;
            for (int k = -4; k <= 4; k++) s += Hill(CrossX, z + k * 4f);
            return s / 9f;
        }

        static float SmoothAlongX(float x, float z)
        {
            float s = 0f;
            for (int k = -4; k <= 4; k++) s += Hill(x + k * 4f, z);
            return s / 9f;
        }

        /// <summary>Rowan Street (east-west, halfway up) and the crest road: the hill smoothed along them, meeting the cross
        /// street at its own height.</summary>
        static float RowanY(float x) => SmoothAlongX(x, RowanZ) - SmoothAlongX(CrossX, RowanZ) + CrossRoadY(RowanZ);
        static float CrestY(float x) => SmoothAlongX(x, CrestZ) - SmoothAlongX(CrossX, CrestZ) + CrossRoadY(CrestZ);

        /// <summary>Beside a road (6.6-15.6 m from its line): the ground eases from the lawn level next to the sidewalk back to
        /// the natural slope.</summary>
        static float Verge(float g, float d, float roadY)
        {
            if (d < 6.6f || d >= 15.6f) return g;
            return Mathf.Lerp(roadY + 0.1f, g, Mathf.SmoothStep(0f, 1f, (d - 6.6f) / 9f));
        }

        /// <summary>Under a road and its sidewalks (within 6.6 m): a little below the surface (the paving sits on it), rising
        /// to the lawn level at the sidewalk's outer edge. +inf outside.</summary>
        static float Bed(float d, float roadY)
        {
            if (d >= 6.6f) return float.PositiveInfinity;
            if (d < 5.8f) return roadY - 0.12f;
            return Mathf.Lerp(roadY - 0.12f, roadY + 0.1f, (d - 5.8f) / 0.8f);
        }

        /// <summary>The ground height everywhere (terrain, tufts, trees, houses, the backdrop's near edge).</summary>
        public static float Ground(float x, float z)
        {
            float g = Natural(x, z);
            bool mainOn = Mathf.Abs(x) < 345f;
            bool crossOn = z > -6f && z < CrestZ + 8f;
            float dMain = Mathf.Abs(z), dCross = Mathf.Abs(x - CrossX), dRowan = Mathf.Abs(z - RowanZ), dCrest = Mathf.Abs(z - CrestZ);
            float yCross = crossOn && dCross < 16f ? CrossRoadY(z) : 0f;
            float yRowan = mainOn && dRowan < 16f ? RowanY(x) : 0f;
            float yCrest = mainOn && dCrest < 16f ? CrestY(x) : 0f;
            // Verges first (each road eases the ground towards its lawn level), then the road beds (lowest wins).
            if (mainOn) g = Verge(g, dMain, 0f);
            if (crossOn) g = Verge(g, dCross, yCross);
            if (mainOn) g = Verge(g, dRowan, yRowan);
            if (mainOn) g = Verge(g, dCrest, yCrest);
            if (mainOn) g = Mathf.Min(g, Bed(dMain, 0f));
            if (crossOn) g = Mathf.Min(g, Bed(dCross, yCross));
            if (mainOn) g = Mathf.Min(g, Bed(dRowan, yRowan));
            if (mainOn) g = Mathf.Min(g, Bed(dCrest, yCrest));
            return g;
        }

        static bool OnPaving(float x, float z)
        {
            if (Mathf.Abs(z) < 6.4f) return true;                                   // main road and sidewalks
            if (Mathf.Abs(x - CrossX) < 6.4f && z > -6f && z < CrestZ + 6.4f) return true;
            if (Mathf.Abs(z - RowanZ) < 6.4f || Mathf.Abs(z - CrestZ) < 6.4f) return true;
            if (x > 7.9f && x < 12.1f && z < 27.1f) return true;                    // driveway
            if (x > -2.1f && x < 0f && z < 14.7f) return true;                      // front walk
            return false;
        }

        static void Terrain(Transform world)
        {
            var t = VhLookGround.BuildTerrain(world, new VhLookGround.TerrainOptions
            {
                origin = new Vector3(TerrainRect.xMin, -2f, TerrainRect.yMin),
                size = new Vector3(TerrainRect.width, 140f, TerrainRect.height),
                height = Ground,
                lawn = (x, z) => z < 44f && z > -60f ? 1f : Mathf.Clamp01(1f - (Mathf.Min(Mathf.Abs(x - CrossX), Mathf.Min(Mathf.Abs(z - RowanZ), Mathf.Abs(z - CrestZ))) - 10f) / 14f) * 0.7f,
                dirt = DirtAt,
                assetPath = TerrainPath,
                seed = 3,
                name = "Ground (terrain)",
            });
            t.gameObject.isStatic = true;
        }

        /// <summary>Bare earth: along the board fence and the foundations (beds), round the yard trees' bases, a worn path.</summary>
        static float DirtAt(float x, float z)
        {
            float d = 0f;
            if (z > 7f && z < 38f && Mathf.Abs(x + 12f) < 0.7f) d = Mathf.Max(d, 0.7f);              // the board fence's foot
            if (x > -8.5f && x < 6.5f && z > 27.8f && z < 28.8f) d = Mathf.Max(d, 0.8f);             // behind the house
            if (x > -34.5f && x < -17.5f && z > 16f && z < 17.1f) d = Mathf.Max(d, 0.8f);            // the bungalow's front bed
            foreach (var p in YardTrees)
            {
                float r = Mathf.Sqrt((x - p.x) * (x - p.x) + (z - p.z) * (z - p.z));
                d = Mathf.Max(d, Mathf.Clamp01(1.6f - r) * 0.9f);
            }
            // A worn footpath across the south verge.
            float path = Mathf.Abs(z + 6.4f - (x + 40f) * 0.55f);
            if (x > -40f && x < 10f && z < -6.4f && z > -40f && path < 0.45f) d = Mathf.Max(d, 0.75f);
            return d;
        }

        // ---------- The street ----------

        static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material m, float bevel = 0.02f, bool collider = true)
        {
            var go = VhLookUv.Block(name, parent, (min + max) * 0.5f, max - min, m, bevel);
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void Street(Transform world)
        {
            var asphalt = VhLookLibrary.Get("Asphalt");
            var kerb = VhLookLibrary.Get("Concrete_Kerb");
            var walk = VhLookLibrary.Get("Concrete_Sidewalk");
            var lane = VhLookLibrary.Get("Road_LaneLine");
            var root = new GameObject("Street").transform;
            root.SetParent(world, false);

            // The main road (east-west) with its painted lane, edge to edge of the terrain.
            var road = Box("Road (main)", root, new Vector3(-335f, -0.3f, -4f), new Vector3(335f, 0f, 4f), lane, 0.01f);
            VhLookUv.BoxProject(road, true, 90f);
            road.GetComponent<MeshRenderer>().sharedMaterial = LaneMaterial(lane);
            Box("Intersection", root, new Vector3(26f, -0.3f, -4f), new Vector3(34f, 0.002f, 4f), asphalt, 0.005f);
            Box("Kerb S", root, new Vector3(-335f, -0.2f, -4.3f), new Vector3(335f, 0.15f, -4f), kerb, 0.03f);
            Box("Sidewalk S", root, new Vector3(-335f, -0.2f, -6.3f), new Vector3(335f, 0.15f, -4.3f), walk, 0.01f);
            Box("Kerb N (west)", root, new Vector3(-335f, -0.2f, 4f), new Vector3(25.7f, 0.15f, 4.3f), kerb, 0.03f);
            Box("Kerb N (east)", root, new Vector3(34.3f, -0.2f, 4f), new Vector3(335f, 0.15f, 4.3f), kerb, 0.03f);
            Box("Sidewalk N (west)", root, new Vector3(-335f, -0.2f, 4.3f), new Vector3(23.7f, 0.15f, 6.3f), walk, 0.01f);
            Box("Sidewalk N (east)", root, new Vector3(36.3f, -0.2f, 4.3f), new Vector3(335f, 0.15f, 6.3f), walk, 0.01f);

            // Paths and beds on the house's lot.
            Box("Driveway", root, new Vector3(8f, -0.2f, 6.3f), new Vector3(12f, 0.115f, 27f), walk, 0.02f);
            Box("Flower bed (west)", root, new Vector3(-8.6f, -0.2f, 16.6f), new Vector3(-3.2f, 0.13f, 17.95f), VhLookLibrary.Get("Dirt"), 0.04f);
            Box("Flower bed (east)", root, new Vector3(1.2f, -0.2f, 16.6f), new Vector3(6.6f, 0.13f, 17.95f), VhLookLibrary.Get("Dirt"), 0.04f);
            Box("Gravel path (side)", root, new Vector3(-11.2f, -0.2f, 18f), new Vector3(-9.2f, 0.12f, 30f), VhLookLibrary.Get("Gravel"), 0.02f);
            Box("Front walk", root, new Vector3(-2.0f, -0.2f, 6.3f), new Vector3(-0.1f, 0.13f, 14.64f), walk, 0.02f);
            Box("Mailbox post", root, new Vector3(0.9f, 0.1f, 6.7f), new Vector3(1.0f, 1.15f, 6.8f), VhLookLibrary.Get("Fence_Planks"), 0.01f);
            Box("Mailbox", root, new Vector3(0.75f, 1.15f, 6.5f), new Vector3(1.15f, 1.38f, 7.0f), VhLookLibrary.Paint("MailboxBlue", new Color(0.2f, 0.32f, 0.5f), 1.5f), 0.04f);
        }

        static Material LaneMaterial(Material lane)
        {
            string path = VhLookLibrary.MatRoot + "VHM_Road_LaneLine__8m.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lane); { VhLookLibrary.EnsureAssetFolder(path); AssetDatabase.CreateAsset(m, path); } }
            else m.CopyPropertiesFromMaterial(lane);
            // The texture is one two-lane road across its width: stretch it over the road's 8 m (and 8 m per repeat along it).
            m.SetTextureScale("_BaseMap", new Vector2(1f / 8f, 1f / 8f));
            m.SetTextureOffset("_BaseMap", new Vector2(0.5f, 0f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- The hill streets: the cross street climbing to Rowan Street and the crest road ----------

        /// <summary>A strip following a road's line: cross-section from <paramref name="a"/> to <paramref name="b"/> (metres
        /// across the road, left to right), its top <paramref name="top"/> above the road surface, with side faces down to
        /// 0.6 m below. <paramref name="along"/> runs along the road (z for the cross street, x for the others).</summary>
        static void Strip(Transform parent, string name, bool alongZ, float line, float s0, float s1, float a, float b, float top,
            Func<float, float> roadY, Material m, List<Vector2> skips = null, float step = 2f)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 P(float s, float c, float dy)
            {
                float y = roadY(s) + top + dy;
                return alongZ ? new Vector3(line + c, y, s) : new Vector3(s, y, line + c);
            }
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 up, bool topFace)
            {
                int i = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
                var nn = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                if (Vector3.Dot(nn, up) < 0f) { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); nn = -nn; }
                else { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
                for (int k = 0; k < 4; k++) n.Add(nn);
                foreach (var p in new[] { p0, p1, p2, p3 })
                    uv.Add(topFace ? new Vector2(p.x, p.z) : alongZ ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
            }
            for (float s = s0; s < s1 - 1e-3f; s += step)
            {
                float e = Mathf.Min(s + step, s1);
                bool skip = false;
                if (skips != null) foreach (var k in skips) if (s + 1e-3f < k.y && e - 1e-3f > k.x) skip = true;
                if (skip) continue;
                Quad(P(s, a, 0f), P(e, a, 0f), P(e, b, 0f), P(s, b, 0f), Vector3.up, true);
                var side = alongZ ? Vector3.right : Vector3.forward;
                Quad(P(s, a, 0f), P(e, a, 0f), P(e, a, -top - 0.6f), P(s, a, -top - 0.6f), -side, false);
                Quad(P(s, b, 0f), P(e, b, 0f), P(e, b, -top - 0.6f), P(s, b, -top - 0.6f), side, false);
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
        }

        static void Road(Transform parent, string name, bool alongZ, float line, float s0, float s1, Func<float, float> y,
            List<Vector2> roadSkips, List<Vector2> walkSkips)
        {
            var asphalt = VhLookLibrary.Get("Asphalt");
            var kerb = VhLookLibrary.Get("Concrete_Kerb");
            var walk = VhLookLibrary.Get("Concrete_Sidewalk");
            Strip(parent, name + " (road)", alongZ, line, s0, s1, -4f, 4f, 0f, y, asphalt, roadSkips);
            Strip(parent, name + " (kerb L)", alongZ, line, s0, s1, -4.3f, -4f, 0.15f, y, kerb, walkSkips);
            Strip(parent, name + " (kerb R)", alongZ, line, s0, s1, 4f, 4.3f, 0.15f, y, kerb, walkSkips);
            Strip(parent, name + " (sidewalk L)", alongZ, line, s0, s1, -6.3f, -4.3f, 0.15f, y, walk, walkSkips);
            Strip(parent, name + " (sidewalk R)", alongZ, line, s0, s1, 4.3f, 6.3f, 0.15f, y, walk, walkSkips);
        }

        static void HillStreets(Transform world)
        {
            var root = new GameObject("Hill streets (backdrop)").transform;
            root.SetParent(world, false);
            // The cross street from the main road up the hill to the crest road (no dead end).
            Road(root, "Cross street", true, CrossX, 4f, CrestZ + 4f, CrossRoadY, null,
                new List<Vector2> { new Vector2(RowanZ - 6.3f, RowanZ + 6.3f), new Vector2(CrestZ - 6.3f, CrestZ + 6.3f) });
            var skipCross = new List<Vector2> { new Vector2(CrossX - 4f, CrossX + 4f) };
            var skipCrossWalk = new List<Vector2> { new Vector2(CrossX - 6.3f, CrossX + 6.3f) };
            Road(root, "Rowan Street", false, RowanZ, -330f, 330f, RowanY, skipCross, skipCrossWalk);
            Road(root, "Crest Road", false, CrestZ, -330f, 330f, CrestY, skipCross, skipCrossWalk);

            // Houses on lots along both, set back 16 m from the road's centre, at least 4 m apart, with driveways.
            var houses = new GameObject("Hill houses").transform;
            houses.SetParent(root, false);
            int seed = 11;
            var walk = VhLookLibrary.Get("Concrete_Sidewalk");
            var lots = new System.Random(4127);
            float L() => (float)lots.NextDouble();
            // Pass 3 (round 2: "every house faces the same way; identical rows"): set back 13.5-20 m, turned up to 6 degrees
            // off the street, lots 25-35 m wide, and a third of the houses gable-front (VhLookHouses.Spec.Random).
            for (float z = 62f; z < CrestZ - 30f; z += Mathf.Lerp(27f, 35f, L()))
            {
                if (Mathf.Abs(z - RowanZ) < 24f) continue;
                foreach (var side in new[] { -1f, 1f })
                {
                    var spec = VhLookHouses.Spec.Random(seed++);
                    float frontX = CrossX + side * Mathf.Lerp(13.5f, 20f, L());
                    float yaw = (side < 0 ? 270f : 90f) + (L() - 0.5f) * 12f;
                    var corner = new Vector3(frontX, 0f, side < 0 ? z - spec.width * 0.5f : z + spec.width * 0.5f);
                    VhLookHouses.Build(houses, corner, yaw, spec, VhLookHouses.Lod.Far, Ground, "House " + seed);
                    FrontPlanting(houses, corner, yaw, spec.width, seed);
                    float dz = z + spec.width * 0.5f + 2.2f;
                    Driveway(houses, new Vector2(CrossX + side * 6.3f, dz), new Vector2(frontX + side * 4f, dz), walk);
                }
            }
            foreach (var line in new[] { RowanZ, CrestZ })
                for (float x = -230f; x < 300f; x += Mathf.Lerp(25f, 35f, L()))
                {
                    if (Mathf.Abs(x - CrossX) < 26f) continue;
                    foreach (var side in new[] { -1f, 1f })
                    {
                        if (line == CrestZ && side > 0) continue;   // the crest road: houses on its near side only
                        var spec = VhLookHouses.Spec.Random(seed++);
                        float frontZ = line + side * Mathf.Lerp(13.5f, 20f, L());
                        float yaw = (side > 0 ? 0f : 180f) + (L() - 0.5f) * 12f;
                        var corner = new Vector3(side > 0 ? x - spec.width * 0.5f : x + spec.width * 0.5f, 0f, frontZ);
                        VhLookHouses.Build(houses, corner, yaw, spec, VhLookHouses.Lod.Far, Ground, "House " + seed);
                        FrontPlanting(houses, corner, yaw, spec.width, seed);
                        float dx = x + spec.width * 0.5f + 2.2f;
                        Driveway(houses, new Vector2(dx, line + side * 6.3f), new Vector2(dx, frontZ + side * 4f), walk);
                    }
                }
        }

        /// <summary>Foundation shrubs along a hill house's front (round 3: "houses on empty golf-course lawns"): two to four,
        /// irregularly spaced, a metre or so out from the wall.</summary>
        static void FrontPlanting(Transform parent, Vector3 corner, float yaw, float width, int seed)
        {
            var rng = new System.Random(seed * 613 + 1);
            float R() => (float)rng.NextDouble();
            var rot = Quaternion.Euler(0f, yaw, 0f);
            int n = 2 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float u = Mathf.Lerp(0.8f, width - 0.8f, (i + R() * 0.7f) / n);
                var p = corner + rot * new Vector3(u, 0f, -Mathf.Lerp(0.9f, 1.5f, R()));
                if (OnPaving(p.x, p.z)) continue;
                p.y = Ground(p.x, p.z) - 0.1f;
                VhLookTrees.Place(parent, p, VhLookTrees.Species.Shrub, rng.Next(3), Mathf.Lerp(1.1f, 1.9f, R()), false, R() * 360f);
            }
        }

        /// <summary>A 3 m concrete driveway from a to b (world xz) following the ground.</summary>
        static void Driveway(Transform parent, Vector2 a, Vector2 b, Material m)
        {
            var dir = (b - a).normalized;
            var right = new Vector2(dir.y, -dir.x) * 1.5f;
            int segs = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.5f));
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)segs);
                foreach (var q in new[] { p - right, p + right })
                {
                    v.Add(new Vector3(q.x, Ground(q.x, q.y) + 0.07f, q.y));
                    uv.Add(q);
                }
            }
            for (int i = 0; i < segs; i++)
            {
                int k = i * 2;
                t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k + 1); t.Add(k + 2); t.Add(k + 3);
            }
            var mesh = new Mesh { name = "Driveway" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            var tris = mesh.triangles;
            if (Vector3.Cross(v[2] - v[0], v[1] - v[0]).y < 0f)
                for (int i = 0; i < tris.Length; i += 3) { int x = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = x; }
            mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = new GameObject("Driveway");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        // ---------- Fences, planting, trees ----------

        static void Fences(Transform world)
        {
            var root = new GameObject("Fences").transform;
            root.SetParent(world, false);
            var boards = VhLookLibrary.Get("Fence_Planks");
            var white = VhLookLibrary.Paint("TrimWhite", new Color(0.95f, 0.94f, 0.9f));
            // A board fence between the two lots (x = -12), sidewalk to the back.
            for (float z = 7.5f; z < 38f; z += 2f)
            {
                Box("Fence post", root, new Vector3(-12.07f, 0f, z - 0.07f), new Vector3(-11.93f, 1.75f, z + 0.07f), boards, 0.015f);
                if (z + 2f < 38.5f)
                    Box("Fence panel", root, new Vector3(-12.02f, 0.12f, z + 0.07f), new Vector3(-11.98f, 1.68f, z + 1.93f), boards, 0.006f);
            }
            // A white picket fence along the front of the lawn, gap at the walk and the driveway: pickets merged into one mesh.
            var parts = new List<CombineInstance>();
            var picket = VhLookUv.BevelBox(new Vector3(0.08f, 0.95f, 0.025f), 0.008f);
            for (float x = -11.8f; x < 7.9f; x += 0.16f)
            {
                if (x > -2.15f && x < 0.05f) continue;
                parts.Add(new CombineInstance { mesh = picket, transform = Matrix4x4.Translate(new Vector3(x, 0.575f, 6.75f)) });
            }
            var pickets = new Mesh { name = "Pickets", indexFormat = IndexFormat.UInt32 };
            pickets.CombineMeshes(parts.ToArray(), true, true);
            var pg = new GameObject("Picket fence");
            pg.transform.SetParent(root, false);
            pg.AddComponent<MeshFilter>().sharedMesh = pickets;
            pg.AddComponent<MeshRenderer>().sharedMaterial = white;
            foreach (var seg in new[] { (-11.9f, -2.15f), (0.05f, 7.95f) })
            {
                Box("Picket rail low", root, new Vector3(seg.Item1, 0.32f, 6.79f), new Vector3(seg.Item2, 0.4f, 6.83f), white, 0.008f, false);
                Box("Picket rail high", root, new Vector3(seg.Item1, 0.8f, 6.79f), new Vector3(seg.Item2, 0.88f, 6.83f), white, 0.008f, false);
                var col = new GameObject("Picket fence collider");
                col.transform.SetParent(root, false);
                var bc = col.AddComponent<BoxCollider>();
                bc.center = new Vector3((seg.Item1 + seg.Item2) * 0.5f, 0.575f, 6.77f);
                bc.size = new Vector3(seg.Item2 - seg.Item1, 0.95f, 0.1f);
            }
        }

        static readonly Vector3[] YardTrees =
        {
            new Vector3(-1f, 0f, 11f), new Vector3(-15f, 0f, 9f), new Vector3(20f, 0f, 10f), new Vector3(39f, 0f, 14f),
            new Vector3(-22f, 0f, -17f), new Vector3(12f, 0f, -12f), new Vector3(-44f, 0f, 9.5f), new Vector3(47f, 0f, 30f),
        };

        static void Planting(Transform world)
        {
            var root = new GameObject("Trees").transform;
            root.SetParent(world, false);
            float G(Vector3 p) => Ground(p.x, p.z) - 0.15f;
            Vector3 At(int i) => new Vector3(YardTrees[i].x, G(YardTrees[i]), YardTrees[i].z);
            VhLookTrees.Place(root, At(0), VhLookTrees.Species.Maple, 0, 10.5f, false, 20f);    // the lawn's maple (backlit in shot 03)
            VhLookTrees.Place(root, At(1), VhLookTrees.Species.Oak, 1, 12f, false, 140f);       // by the board fence
            VhLookTrees.Place(root, At(2), VhLookTrees.Species.Elm, 0, 9.5f, false, 75f);       // at the corner (shot 11)
            VhLookTrees.Place(root, At(3), VhLookTrees.Species.Maple, 2, 11f, false, 200f);     // across the cross street
            VhLookTrees.Place(root, At(4), VhLookTrees.Species.Birch, 0, 11f, false, 10f);      // the south verge (off the sun's line)
            VhLookTrees.Place(root, At(5), VhLookTrees.Species.Oak, 2, 10f, true, 300f);
            VhLookTrees.Place(root, At(6), VhLookTrees.Species.Elm, 1, 12.5f, false, 33f);
            VhLookTrees.Place(root, At(7), VhLookTrees.Species.Birch, 2, 10f, false, 250f);
            // A birch pair and a maple in the bungalow's yard, a stand behind the houses.
            VhLookTrees.Cluster(root, new Vector3(-26f, 0f, 34f), 7f, 4, 21, 10f, (x, z) => Ground(x, z), null, 3.5f, null, true);
            VhLookTrees.Cluster(root, new Vector3(4f, 0f, 38f), 8f, 5, 22, 11f, (x, z) => Ground(x, z), null, 3.5f, null, true);
            // Pass 3: moved south, off the line to the sun (round 2's shot 08 had the sun behind this stand: no disc anywhere).
            VhLookTrees.Cluster(root, new Vector3(-56f, 0f, -48f), 10f, 6, 23, 11f, (x, z) => Ground(x, z), null, 3.5f, p => !OnPaving(p.x, p.z) && !OnSunLine(p), true);
            // Foundation shrubs along the beds (clear of the porch).
            int k = 0;
            foreach (var x in new[] { -7.6f, -5.6f, -3.9f, 1.9f, 3.8f, 5.8f })
                VhLookTrees.Place(root, new Vector3(x, 0.13f, 17.25f), VhLookTrees.Species.Shrub, k++ % 3, 1.4f + (k % 3) * 0.2f, false, k * 61f);
            foreach (var x in new[] { -33f, -30f, -21f, -18.5f })
                VhLookTrees.Place(root, new Vector3(x, G(new Vector3(x, 0f, 16.4f)), 16.4f), VhLookTrees.Species.Shrub, k++ % 3, 1.3f, false, k * 47f);
        }

        /// <summary>Stands of trees over the hill and street trees along the hill streets (clear of roads and lots).</summary>
        static void HillTrees(Transform world)
        {
            var root = new GameObject("Hill trees (backdrop)").transform;
            root.SetParent(world, false);
            bool Allowed(Vector3 p)
            {
                if (OnPaving(p.x, p.z)) return false;
                if (Mathf.Abs(p.x - CrossX) < 30f && p.z > 50f && p.z < CrestZ - 20f) return false;          // the cross street's lots
                if ((Mathf.Abs(p.z - RowanZ) < 30f || Mathf.Abs(p.z - CrestZ) < 30f) && Mathf.Abs(p.x) < 320f) return false;   // the other lots
                if (p.z < 45f) return false;                                                                       // the street below
                return true;
            }
            VhLookTrees.Scatter(root, new Vector2(-330f, 50f), new Vector2(330f, 640f), 46, 5, (x, z) => Ground(x, z), Allowed, 10.5f);
            // Street trees along the cross street (in the verge between the sidewalk and the lots), every ~22 m.
            int k = 0;
            for (float z = 52f; z < CrestZ - 20f; z += 22f)
                foreach (var side in new[] { -1f, 1f })
                {
                    float x = CrossX + side * 8.2f, zz = z + side * 6f;
                    if (Mathf.Abs(zz - RowanZ) < 10f) continue;
                    VhLookTrees.Place(root, new Vector3(x, Ground(x, zz) - 0.15f, zz), (k % 3 == 0) ? VhLookTrees.Species.Maple : VhLookTrees.Species.Elm,
                        k % 3, 9f + (k * 37 % 5), false, k * 53f);
                    k++;
                }
        }

        // ---------- Ground detail: grass, litter, stains, drains ----------

        static void GroundDetail(Transform world)
        {
            var root = new GameObject("Ground detail").transform;
            root.SetParent(world, false);
            var tufts = new VhLookGround.Tufts(7, (x, z) => Ground(x, z));
            // Overhang along every paving edge near the camera's streets.
            tufts.Edge(new Vector2(-120f, 6.3f), new Vector2(23.7f, 6.3f), Vector2.up);
            tufts.Edge(new Vector2(36.3f, 6.3f), new Vector2(120f, 6.3f), Vector2.up);
            tufts.Edge(new Vector2(-120f, -6.3f), new Vector2(120f, -6.3f), Vector2.down);
            tufts.Edge(new Vector2(CrossX - 6.3f, 6.3f), new Vector2(CrossX - 6.3f, 190f), Vector2.left, 0.38f);
            tufts.Edge(new Vector2(CrossX + 6.3f, 6.3f), new Vector2(CrossX + 6.3f, 190f), Vector2.right, 0.38f);
            tufts.Edge(new Vector2(8f, 6.8f), new Vector2(8f, 27f), Vector2.left);
            tufts.Edge(new Vector2(12f, 6.3f), new Vector2(12f, 27f), Vector2.right);
            tufts.Edge(new Vector2(-2f, 6.8f), new Vector2(-2f, 14.6f), Vector2.left, 0.28f, 0.12f);
            tufts.Edge(new Vector2(-0.1f, 6.8f), new Vector2(-0.1f, 14.6f), Vector2.right, 0.28f, 0.12f);
            tufts.Edge(new Vector2(-8.6f, 16.6f), new Vector2(-3.2f, 16.6f), Vector2.down, 0.3f, 0.1f);
            tufts.Edge(new Vector2(1.2f, 16.6f), new Vector2(6.6f, 16.6f), Vector2.down, 0.3f, 0.1f);
            tufts.Edge(new Vector2(-11.85f, 7f), new Vector2(-11.85f, 38f), Vector2.right, 0.3f);
            tufts.Edge(new Vector2(-12.15f, 7f), new Vector2(-12.15f, 38f), Vector2.left, 0.3f);
            tufts.Edge(new Vector2(-11.9f, 6.9f), new Vector2(7.9f, 6.9f), Vector2.up, 0.3f, 0.06f);
            // Drifts across the lawns and verges (off the paving, the beds and the houses).
            bool Lawn(float x, float z)
            {
                if (OnPaving(x, z)) return false;
                if (x > -8.7f && x < 6.7f && z > 16.5f && z < 29f) return false;        // the house and its beds
                if (x > -34.2f && x < -17.8f && z > 16f && z < 29f) return false;       // the bungalow
                if (x > -11.3f && x < -9.1f && z > 18f && z < 30f) return false;        // the gravel path
                return true;
            }
            tufts.Scatter(new Rect(-90f, 6.3f, 113f, 38f), 0.12f, Lawn, 0.07f, 0.18f);
            tufts.Scatter(new Rect(36.3f, 6.3f, 60f, 38f), 0.12f, Lawn, 0.07f, 0.18f);
            tufts.Scatter(new Rect(-90f, -45f, 180f, 38.7f), 0.1f, Lawn, 0.1f, 0.25f);
            tufts.Scatter(new Rect(0f, 44f, 60f, 120f), 0.05f, Lawn, 0.12f, 0.45f);   // the lower hillside: drier, wildflowers
            tufts.Build(root, "Grass and wildflowers");

            // Leaf litter: drifts in the gutters against the kerbs, under the yard trees, along the board fence.
            int seed = 1;
            foreach (var x in new[] { -38f, -9f, 17f, 52f })
                VhLookGround.Litter(root, new Vector3(x, 0.002f, -3.55f), 1.2f, 4, seed++);
            foreach (var x in new[] { -24f, 3f, 21.5f })
                VhLookGround.Litter(root, new Vector3(x, 0.002f, 3.55f), 1.2f, 4, seed++);
            foreach (var p in new[] { YardTrees[0], YardTrees[1], YardTrees[2], YardTrees[4] })
                VhLookGround.Litter(root, p, 2.4f, 6, seed++, (x, z) => Ground(x, z));
            VhLookGround.Litter(root, new Vector3(-12.5f, 0f, 22f), 1.4f, 3, seed++, (x, z) => Ground(x, z));
            VhLookGround.Litter(root, new Vector3(CrossX - 3.5f, 0f, 18f), 1.0f, 3, seed++, (x, z) => CrossRoadY(z) + 0.002f);

            // Stains: oil on the driveway (under the car and by the garage end) and in the lanes; dirt on the sidewalks.
            VhLookGround.Stain(root, new Vector3(10f, 0.115f, 15.2f), 1.5f, 10f, VhLookGround.StainKind.Oil);
            VhLookGround.Stain(root, new Vector3(9.6f, 0.115f, 24.5f), 1.1f, 70f, VhLookGround.StainKind.Oil);
            VhLookGround.Stain(root, new Vector3(10.6f, 0.115f, 9f), 2.2f, 0f, VhLookGround.StainKind.Dirt, 0.6f);
            foreach (var p in new[] { new Vector2(-30f, 2.1f), new Vector2(-6f, -1.9f), new Vector2(22f, 2f), new Vector2(46f, -2.2f), new Vector2(8f, 1.8f) })
                VhLookGround.Stain(root, new Vector3(p.x, 0f, p.y), 1.1f, p.x * 13f, VhLookGround.StainKind.Oil, 1.6f);
            foreach (var p in new[] { new Vector2(-18f, -5.2f), new Vector2(4f, 5.4f), new Vector2(40f, -5.0f), new Vector2(-50f, 5.2f) })
                VhLookGround.Stain(root, new Vector3(p.x, 0.15f, p.y), 1.4f, p.x * 7f, VhLookGround.StainKind.Dirt, 1.3f);

            // Storm drains at the kerbs (kerb inlets with iron grates).
            VhLookGround.StormDrain(root, new Vector3(12f, 0f, -4f), Vector3.right, Vector3.forward);
            VhLookGround.StormDrain(root, new Vector3(-30f, 0f, 4f), Vector3.right, Vector3.back);
            VhLookGround.StormDrain(root, new Vector3(CrossX - 4f, CrossRoadY(22f), 22f), Vector3.forward, Vector3.right);
        }

        /// <summary>Within 8 degrees of the bearing to the sun as seen from the street's middle (shots 08 and 12 look into the
        /// sun: nothing tall stands there, so the disc shows).</summary>
        static bool OnSunLine(Vector3 p)
        {
            var d = new Vector2(p.x, p.z);
            if (d.magnitude < 15f) return false;
            var sun = new Vector2(ToSun.x, ToSun.z).normalized;
            return Vector2.Angle(d, sun) < 8f;
        }

        static void AlignLongAxisToZ(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && b.size.x > b.size.z) go.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * go.transform.rotation;
            // Sit it on the driveway.
            if (any) go.transform.position += Vector3.up * (0.115f - b.min.y);
        }
    }
}
