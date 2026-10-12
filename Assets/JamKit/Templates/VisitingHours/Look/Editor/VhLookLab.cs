using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JamKit.Look.EditorTools
{
    /// <summary>
    /// LookLab: a lit street corner for judging the look (Look/Scenes/LookLab.unity): a two-storey clapboard house with
    /// recessed windows, trim, gutters, a porch and a chimney; a brick neighbour; a lawn with a picket fence and a board
    /// fence; kerbs, sidewalks, an intersection; trees; a covered car in the driveway; a manhole; and a hillside of trees and
    /// houses rising north into the haze. Everything uses the Look library. Built by <see cref="Build"/>, captured by
    /// <see cref="Capture"/> (out/look-captures/&lt;time&gt;/).
    /// </summary>
    public static class VhLookLab
    {
        public const string ScenePath = VhLookLibrary.LookRoot + "Scenes/LookLab.unity";

        [MenuItem("JamKit/Look/Build LookLab")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static string Build()
        {
            VhLookLibrary.Build();
            VhLookSetup.InstallRendererFeatures();
            VhLookProfiles.BuildAll();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var world = new GameObject("LookLab (street corner)").transform;
            Street(world);
            House(world, new Vector3(-8f, 0f, 18f), 14f, 10f, VhLookLibrary.Tinted("Clapboard_Painted", "Sage", new Color(0.72f, 0.8f, 0.66f)));
            Neighbour(world);
            Fences(world);
            Planting(world);
            Hillside(world);
            var car = VhLookLibrary.Prop("CoveredCar", world, new Vector3(10f, 0.11f, 15.5f), 0f);
            if (car != null) AlignLongAxisToZ(car);
            var manhole = VhLookLibrary.Prop("ManholeCover", world, new Vector3(16f, 0.004f, 1.6f), 0f);
            if (manhole != null) { var c = manhole.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c); }

            // A warm lamp inside the house (seen through the front windows).
            var lamp = new GameObject("Lamp inside (warm)").AddComponent<Light>();
            lamp.transform.SetParent(world, false);
            lamp.transform.position = new Vector3(-3f, 2.2f, 21f);
            lamp.type = LightType.Point;
            lamp.range = 7f;
            lamp.intensity = 2.2f;
            lamp.color = new Color(1f, 0.72f, 0.45f);
            lamp.shadows = LightShadows.None;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(24f, 1.7f, -10f);
            camGo.transform.LookAt(new Vector3(-2f, 4f, 20f));
            cam.fieldOfView = 60f;
            VhLook.SetupCamera(cam);

            VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 10f });
            VhLook.AddReflectionProbe(scene, new Vector3(0f, 4f, 2f), new Vector3(120f, 16f, 30f), 1, "Reflection probe (street)");
            VhLook.AddReflectionProbe(scene, new Vector3(-1f, 3.5f, 23f), new Vector3(14f, 6f, 10f), 2, "Reflection probe (house)");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("JAMKIT_LOOK LookLab built: " + ScenePath);
            return ScenePath;
        }

        // ---------- The capture ----------

        static readonly Vector3 ToSun = VhLookRig.Direction(10f, 250f);

        public static List<VhLookCapture.Shot> Shots()
        {
            Vector3 eye8 = new Vector3(0f, 1.7f, 0f);
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

        // ---------- Batch entry points (main runs these; see docs/redesign/look.md) ----------

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.BuildFromCommandLine (imports, library, renderer feature,
        /// profiles, the LookLab scene). No GPU work beyond importing.</summary>
        public static void BuildFromCommandLine() => Run(() => { Build(); return 0; });

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.BakeFromCommandLine (GPU: probes, reflections, ambient).</summary>
        public static void BakeFromCommandLine() => Run(() => VhLook.BakeScene(ScenePath) ? 0 : 2);

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.CaptureFromCommandLine (GPU: ten 1920x1080 stills).</summary>
        public static void CaptureFromCommandLine() => Run(() => { Debug.Log("JAMKIT_LOOK_CAPTURE_OK " + Capture()); return 0; });

        /// <summary>-executeMethod JamKit.Look.EditorTools.VhLookLab.AllFromCommandLine: build, bake, capture in one run.</summary>
        public static void AllFromCommandLine() => Run(() =>
        {
            Build();
            bool baked = VhLook.BakeScene(ScenePath);
            Debug.Log("JAMKIT_LOOK_CAPTURE_OK " + Capture() + (baked ? "" : " (bake failed: unbaked lighting)"));
            return baked ? 0 : 2;
        });

        static void Run(Func<int> body)
        {
            int code;
            try { code = body(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        // ---------- Building blocks ----------

        static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material m, float bevel = 0.02f, bool collider = true)
        {
            var go = VhLookUv.Block(name, parent, (min + max) * 0.5f, max - min, m, bevel);
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>A box in a wall's frame: u along the wall (right), v up, w inward from the outer face.</summary>
        static GameObject WallBox(string name, Transform parent, Vector3 origin, Quaternion rot, float u0, float u1, float v0, float v1,
            float w0, float w1, Material m, float bevel = 0.01f, bool collider = true)
        {
            var size = new Vector3(Mathf.Abs(u1 - u0), Mathf.Abs(v1 - v0), Mathf.Abs(w1 - w0));
            var localCenter = new Vector3((u0 + u1) * 0.5f, (v0 + v1) * 0.5f, (w0 + w1) * 0.5f);
            var go = VhLookUv.Block(name, parent, origin + rot * localCenter, size, m, bevel);
            go.transform.rotation = rot;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        struct Opening
        {
            public float u0, v0, u1, v1;
            public bool door;
            public Opening(float u0, float v0, float u1, float v1, bool door = false) { this.u0 = u0; this.v0 = v0; this.u1 = u1; this.v1 = v1; this.door = door; }
        }

        /// <summary>
        /// A wall with real openings: the solid parts are boxes around the holes (so windows have depth), and each opening gets
        /// its trim, sill, glass and muntins (or a door).
        /// </summary>
        static void Wall(Transform parent, string name, Vector3 origin, Vector3 inward, float width, float height, float thickness,
            List<Opening> openings, Material wall, Material trim, Material glass, Material door)
        {
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            // Column breakpoints along u.
            var us = new List<float> { 0f, width };
            foreach (var o in openings) { us.Add(o.u0); us.Add(o.u1); }
            us.Sort();
            for (int i = 0; i < us.Count - 1; i++)
            {
                float a = us[i], b = us[i + 1];
                if (b - a < 1e-3f) continue;
                float mid = (a + b) * 0.5f;
                var vs = new List<float> { 0f, height };
                foreach (var o in openings)
                    if (mid > o.u0 && mid < o.u1) { vs.Add(o.v0); vs.Add(o.v1); }
                vs.Sort();
                for (int k = 0; k < vs.Count - 1; k++)
                {
                    float c = vs[k], d = vs[k + 1];
                    if (d - c < 1e-3f) continue;
                    float vm = (c + d) * 0.5f;
                    bool hole = false;
                    foreach (var o in openings)
                        if (mid > o.u0 && mid < o.u1 && vm > o.v0 && vm < o.v1) hole = true;
                    if (!hole) WallBox(name + " piece", root, origin, rot, a, b, c, d, 0f, thickness, wall, 0.004f);
                }
            }
            foreach (var o in openings)
            {
                const float t = 0.1f, out_ = -0.05f;
                WallBox("Trim L", root, origin, rot, o.u0 - t, o.u0, o.v0 - (o.door ? 0f : t), o.v1 + t, out_, 0.06f, trim, 0.01f, false);
                WallBox("Trim R", root, origin, rot, o.u1, o.u1 + t, o.v0 - (o.door ? 0f : t), o.v1 + t, out_, 0.06f, trim, 0.01f, false);
                WallBox("Trim head", root, origin, rot, o.u0 - t - 0.04f, o.u1 + t + 0.04f, o.v1 + t, o.v1 + t + 0.08f, out_ - 0.03f, 0.06f, trim, 0.012f, false);
                // Reveal (the opening's inner faces, painted like the trim).
                WallBox("Reveal L", root, origin, rot, o.u0, o.u0 + 0.02f, o.v0, o.v1, 0f, thickness, trim, 0.002f, false);
                WallBox("Reveal R", root, origin, rot, o.u1 - 0.02f, o.u1, o.v0, o.v1, 0f, thickness, trim, 0.002f, false);
                WallBox("Reveal top", root, origin, rot, o.u0, o.u1, o.v1 - 0.02f, o.v1, 0f, thickness, trim, 0.002f, false);
                if (o.door)
                {
                    WallBox("Door", root, origin, rot, o.u0 + 0.02f, o.u1 - 0.02f, o.v0, o.v1 - 0.02f, 0.1f, 0.15f, door, 0.01f);
                    // Raised panels and a brass knob.
                    float pw = (o.u1 - o.u0 - 0.3f) * 0.5f;
                    for (int r = 0; r < 2; r++)
                        for (int cidx = 0; cidx < 2; cidx++)
                        {
                            float pu = o.u0 + 0.1f + cidx * (pw + 0.1f);
                            float pv = o.v0 + 0.15f + r * 1.0f;
                            WallBox("Door panel", root, origin, rot, pu, pu + pw, pv, pv + 0.8f, 0.085f, 0.1f, door, 0.015f, false);
                        }
                    WallBox("Door knob", root, origin, rot, o.u1 - 0.16f, o.u1 - 0.1f, o.v0 + 0.95f, o.v0 + 1.01f, 0.03f, 0.1f,
                        VhLookLibrary.Paint("Brass", new Color(0.85f, 0.65f, 0.3f), 1.6f), 0.02f, false);
                    continue;
                }
                WallBox("Sill", root, origin, rot, o.u0 - 0.14f, o.u1 + 0.14f, o.v0 - 0.07f, o.v0, -0.12f, 0.08f, trim, 0.015f, false);
                WallBox("Glass", root, origin, rot, o.u0, o.u1, o.v0, o.v1, 0.11f, 0.12f, glass, 0.001f, false);
                // Sash and muntins (a six-over-six look).
                float uc = (o.u0 + o.u1) * 0.5f, vc = (o.v0 + o.v1) * 0.5f;
                WallBox("Meeting rail", root, origin, rot, o.u0, o.u1, vc - 0.03f, vc + 0.03f, 0.07f, 0.11f, trim, 0.005f, false);
                WallBox("Muntin v", root, origin, rot, uc - 0.015f, uc + 0.015f, o.v0, o.v1, 0.085f, 0.11f, trim, 0.004f, false);
                for (int m = 1; m <= 2; m++)
                {
                    float vv = Mathf.Lerp(o.v0, vc, m / 3f);
                    float vv2 = Mathf.Lerp(vc, o.v1, m / 3f);
                    WallBox("Muntin h", root, origin, rot, o.u0, o.u1, vv - 0.012f, vv + 0.012f, 0.09f, 0.11f, trim, 0.003f, false);
                    WallBox("Muntin h", root, origin, rot, o.u0, o.u1, vv2 - 0.012f, vv2 + 0.012f, 0.09f, 0.11f, trim, 0.003f, false);
                }
                WallBox("Sash", root, origin, rot, o.u0, o.u1, o.v0, o.v0 + 0.06f, 0.07f, 0.11f, trim, 0.005f, false);
                // Curtains half drawn, just inside.
                var curtain = VhLookLibrary.Tinted("Plaster_Interior", "Curtain", new Color(0.95f, 0.86f, 0.74f));
                float cw = (o.u1 - o.u0) * 0.26f;
                WallBox("Curtain L", root, origin, rot, o.u0 + 0.02f, o.u0 + cw, o.v0 + 0.05f, o.v1, thickness + 0.05f, thickness + 0.08f, curtain, 0.01f, false);
                WallBox("Curtain R", root, origin, rot, o.u1 - cw, o.u1 - 0.02f, o.v0 + 0.05f, o.v1, thickness + 0.05f, thickness + 0.08f, curtain, 0.01f, false);
            }
        }

        // ---------- The street ----------

        static void Street(Transform world)
        {
            var asphalt = VhLookLibrary.Get("Asphalt");
            var kerb = VhLookLibrary.Get("Concrete_Kerb");
            var walk = VhLookLibrary.Get("Concrete_Sidewalk");
            var lawn = VhLookLibrary.Get("Grass_Lawn");
            var lane = VhLookLibrary.Get("Road_LaneLine");
            var root = new GameObject("Street").transform;
            root.SetParent(world, false);

            // The main road (east-west) with its painted lane: the lane texture's lines run along V, so the top is turned.
            var road = Box("Road (main)", root, new Vector3(-80f, -0.3f, -4f), new Vector3(80f, 0f, 4f), lane, 0.01f);
            VhLookUv.BoxProject(road, true, 90f);
            var laneMat = LaneMaterial(lane);
            road.GetComponent<MeshRenderer>().sharedMaterial = laneMat;
            // The cross street (north, up the hill).
            var cross = Box("Road (cross street)", root, new Vector3(26f, -0.3f, 4f), new Vector3(34f, 0f, 120f), asphalt, 0.01f);
            VhLookUv.BoxProject(cross);
            Box("Intersection", root, new Vector3(26f, -0.3f, -4f), new Vector3(34f, 0.002f, 4f), asphalt, 0.005f);

            // Kerbs (15 cm) and sidewalks.
            Box("Kerb S", root, new Vector3(-80f, -0.2f, -4.3f), new Vector3(80f, 0.15f, -4f), kerb, 0.03f);
            Box("Sidewalk S", root, new Vector3(-80f, -0.2f, -6.3f), new Vector3(80f, 0.15f, -4.3f), walk, 0.01f);
            Box("Verge S", root, new Vector3(-80f, -0.2f, -40f), new Vector3(80f, 0.1f, -6.3f), lawn, 0.01f);
            Box("Kerb N (west)", root, new Vector3(-80f, -0.2f, 4f), new Vector3(25.7f, 0.15f, 4.3f), kerb, 0.03f);
            Box("Kerb N (east)", root, new Vector3(34.3f, -0.2f, 4f), new Vector3(80f, 0.15f, 4.3f), kerb, 0.03f);
            Box("Sidewalk N (west)", root, new Vector3(-80f, -0.2f, 4.3f), new Vector3(23.7f, 0.15f, 6.3f), walk, 0.01f);
            Box("Sidewalk N (east)", root, new Vector3(36.3f, -0.2f, 4.3f), new Vector3(80f, 0.15f, 6.3f), walk, 0.01f);
            Box("Kerb cross W", root, new Vector3(25.7f, -0.2f, 4.3f), new Vector3(26f, 0.15f, 120f), kerb, 0.03f);
            Box("Kerb cross E", root, new Vector3(34f, -0.2f, 4.3f), new Vector3(34.3f, 0.15f, 120f), kerb, 0.03f);
            Box("Sidewalk cross W", root, new Vector3(23.7f, -0.2f, 4.3f), new Vector3(25.7f, 0.15f, 120f), walk, 0.01f);
            Box("Sidewalk cross E", root, new Vector3(34.3f, -0.2f, 4.3f), new Vector3(36.3f, 0.15f, 120f), walk, 0.01f);

            // Lawns behind the sidewalks; a driveway; a flower bed under the house's windows.
            Box("Lawn (the house)", root, new Vector3(-80f, -0.3f, 6.3f), new Vector3(23.7f, 0.1f, 40f), lawn, 0.01f);
            Box("Lawn (east)", root, new Vector3(36.3f, -0.3f, 6.3f), new Vector3(80f, 0.1f, 40f), lawn, 0.01f);
            Box("Driveway", root, new Vector3(8f, -0.2f, 6.3f), new Vector3(12f, 0.115f, 27f), VhLookLibrary.Get("Concrete_Sidewalk"), 0.02f);
            Box("Flower bed", root, new Vector3(-8.6f, -0.2f, 16.6f), new Vector3(6.6f, 0.13f, 18f), VhLookLibrary.Get("Dirt"), 0.04f);
            Box("Gravel path (side)", root, new Vector3(-11.2f, -0.2f, 18f), new Vector3(-9.2f, 0.12f, 30f), VhLookLibrary.Get("Gravel"), 0.02f);
            Box("Front walk", root, new Vector3(-2.0f, -0.2f, 6.3f), new Vector3(-0.1f, 0.13f, 14.64f), walk, 0.02f);
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

        // ---------- The house ----------

        static void House(Transform world, Vector3 corner, float width, float depth, Material siding)
        {
            var root = new GameObject("House (clapboard, two storeys)").transform;
            root.SetParent(world, false);
            var trim = VhLookLibrary.Paint("TrimWhite", new Color(0.95f, 0.94f, 0.9f));
            var glass = VhLookLibrary.Glass();
            var door = VhLookLibrary.Paint("DoorRed", new Color(0.55f, 0.13f, 0.1f), 1.4f);
            var roofMat = VhLookLibrary.Get("Roof_ShinglesAsphalt");
            var found = VhLookLibrary.Get("Concrete_Kerb");
            float baseY = 0.7f, wallH = 5.6f, th = 0.22f;
            float x0 = corner.x, z0 = corner.z, x1 = x0 + width, z1 = z0 + depth;

            Box("Foundation", root, new Vector3(x0 - 0.06f, 0f, z0 - 0.06f), new Vector3(x1 + 0.06f, baseY, z1 + 0.06f), found, 0.03f);

            // Front (faces -Z): two ground-floor windows, the door, three upstairs windows.
            var front = new List<Opening>
            {
                new Opening(1.2f, 1.0f, 2.6f, 2.8f), new Opening(3.4f, 1.0f, 4.8f, 2.8f),
                new Opening(6.4f, 0f, 7.5f, 2.3f, true),
                new Opening(9.2f, 1.0f, 10.6f, 2.8f), new Opening(11.4f, 1.0f, 12.8f, 2.8f),
                new Opening(1.6f, 3.6f, 2.9f, 5.0f), new Opening(6.3f, 3.6f, 7.6f, 5.0f), new Opening(11.1f, 3.6f, 12.4f, 5.0f),
            };
            Wall(root, "Wall front", new Vector3(x0, baseY, z0), Vector3.forward, width, wallH, th, front, siding, trim, glass, door);
            // West side (faces -X): two windows per floor.
            var west = new List<Opening>
            {
                new Opening(2.0f, 1.0f, 3.4f, 2.8f), new Opening(6.4f, 1.0f, 7.8f, 2.8f),
                new Opening(2.2f, 3.6f, 3.4f, 5.0f), new Opening(6.6f, 3.6f, 7.8f, 5.0f),
            };
            Wall(root, "Wall west", new Vector3(x0, baseY, z1 - th), Vector3.right, depth - 2f * th, wallH, th, west, siding, trim, glass, door);
            Wall(root, "Wall east", new Vector3(x1, baseY, z0 + th), Vector3.left, depth - 2f * th, wallH, th,
                new List<Opening> { new Opening(4.0f, 1.0f, 5.4f, 2.8f), new Opening(4.2f, 3.6f, 5.4f, 5.0f) }, siding, trim, glass, door);
            Wall(root, "Wall back", new Vector3(x1, baseY, z1), Vector3.back, width, wallH, th, new List<Opening>(), siding, trim, glass, door);

            // Inside: an inward-facing shell so the windows show a room, and a floor between the storeys.
            var interior = VhLookLibrary.Tinted("Plaster_Interior", "WarmRoom", new Color(0.98f, 0.9f, 0.78f));
            var shell = new GameObject("Interior shell");
            shell.transform.SetParent(root, false);
            var shellMesh = VhLookUv.BevelBox(new Vector3(width - 2f * th - 0.04f, wallH - 0.04f, depth - 2f * th - 0.04f), 0.01f);
            Invert(shellMesh);
            shell.transform.position = new Vector3((x0 + x1) * 0.5f, baseY + wallH * 0.5f, (z0 + z1) * 0.5f);
            shell.AddComponent<MeshFilter>().sharedMesh = shellMesh;
            shell.AddComponent<MeshRenderer>().sharedMaterial = interior;
            Box("Upstairs floor", root, new Vector3(x0 + th, baseY + 2.95f, z0 + th), new Vector3(x1 - th, baseY + 3.15f, z1 - th), interior, 0.01f, false);

            // Corner boards and a frieze under the eaves (the trim that makes clapboard read as a real house).
            foreach (var cx in new[] { x0, x1 })
                foreach (var cz in new[] { z0, z1 })
                    Box("Corner board", root, new Vector3(cx - 0.09f, baseY, cz - 0.09f), new Vector3(cx + 0.09f, baseY + wallH, cz + 0.09f), trim, 0.015f, false);
            Box("Frieze front", root, new Vector3(x0 - 0.05f, baseY + wallH - 0.3f, z0 - 0.05f), new Vector3(x1 + 0.05f, baseY + wallH, z0 + 0.02f), trim, 0.01f, false);
            Box("Frieze back", root, new Vector3(x0 - 0.05f, baseY + wallH - 0.3f, z1 - 0.02f), new Vector3(x1 + 0.05f, baseY + wallH, z1 + 0.05f), trim, 0.01f, false);

            // The roof: a gable along X with eaves, fascia, gutters, downspouts, and gable ends in siding.
            float eaveY = baseY + wallH, over = 0.45f, rise = 3.3f;
            var roof = GableRoof(width + 2f * over, depth + 2f * over, rise);
            var roofGo = new GameObject("Roof (shingles)");
            roofGo.transform.SetParent(root, false);
            roofGo.transform.position = new Vector3((x0 + x1) * 0.5f, eaveY, (z0 + z1) * 0.5f);
            roofGo.AddComponent<MeshFilter>().sharedMesh = roof;
            roofGo.AddComponent<MeshRenderer>().sharedMaterial = roofMat;
            roofGo.AddComponent<MeshCollider>().sharedMesh = roof;
            var gable = GableEnd(depth, rise * depth / (depth + 2f * over));
            foreach (var gx in new[] { x0, x1 })
            {
                var g = new GameObject("Gable end");
                g.transform.SetParent(root, false);
                g.transform.position = new Vector3(gx, eaveY, (z0 + z1) * 0.5f);
                g.AddComponent<MeshFilter>().sharedMesh = gable;
                g.AddComponent<MeshRenderer>().sharedMaterial = siding;
            }
            var galv = VhLookLibrary.Get("Galvanized_Corrugated");
            var gutterMat = VhLookLibrary.Paint("GutterWhite", new Color(0.92f, 0.92f, 0.9f), 1.5f);
            foreach (var sz in new[] { -1f, 1f })
            {
                float ez = sz < 0 ? z0 - over : z1 + over;
                Box("Fascia", root, new Vector3(x0 - over, eaveY - 0.22f, ez - 0.03f), new Vector3(x1 + over, eaveY + 0.02f, ez + 0.03f), trim, 0.01f, false);
                Box("Gutter", root, new Vector3(x0 - over, eaveY - 0.2f, ez + sz * 0.03f - 0.06f), new Vector3(x1 + over, eaveY - 0.06f, ez + sz * 0.03f + 0.06f), gutterMat, 0.03f, false);
                Box("Downspout", root, new Vector3(x0 - 0.05f, baseY - 0.6f, ez + sz * 0.03f - 0.04f), new Vector3(x0 + 0.03f, eaveY - 0.1f, ez + sz * 0.03f + 0.04f), gutterMat, 0.02f, false);
            }
            // Chimney (brick) with a cap and a flashing collar.
            var brick = VhLookLibrary.Get("Brick_Red");
            Box("Chimney", root, new Vector3(x1 - 2.6f, baseY + 1f, z0 + 6.2f), new Vector3(x1 - 1.6f, eaveY + rise + 1.4f, z0 + 7.2f), brick, 0.02f);
            Box("Chimney cap", root, new Vector3(x1 - 2.7f, eaveY + rise + 1.4f, z0 + 6.1f), new Vector3(x1 - 1.5f, eaveY + rise + 1.52f, z0 + 7.3f), found, 0.02f, false);
            Box("Flashing", root, new Vector3(x1 - 2.65f, eaveY + rise * 0.55f, z0 + 6.15f), new Vector3(x1 - 1.55f, eaveY + rise * 0.55f + 0.12f, z0 + 7.25f), galv, 0.005f, false);

            // The porch: deck, steps, posts, a railing and a small hipped roof.
            var deck = VhLookLibrary.Get("Deck_Planks");
            float px0 = x0 + 5.2f, px1 = x0 + 8.7f, pz0 = z0 - 2.4f;
            Box("Porch deck", root, new Vector3(px0, baseY - 0.12f, pz0), new Vector3(px1, baseY, z0), deck, 0.02f);
            Box("Porch skirt", root, new Vector3(px0 + 0.05f, 0f, pz0 + 0.05f), new Vector3(px1 - 0.05f, baseY - 0.12f, z0), trim, 0.01f, false);
            for (int s = 0; s < 3; s++)
            {
                float top = baseY - 0.15f * (s + 1);
                Box("Step " + (s + 1), root, new Vector3(x0 + 6.0f, 0f, pz0 - 0.32f * (s + 1)), new Vector3(x0 + 7.9f, top, pz0 - 0.32f * s), found, 0.02f);
            }
            foreach (var postX in new[] { px0 + 0.1f, px1 - 0.1f })
                Box("Porch post", root, new Vector3(postX - 0.08f, baseY, pz0 + 0.1f - 0.08f), new Vector3(postX + 0.08f, baseY + 2.7f, pz0 + 0.1f + 0.08f), trim, 0.02f);
            Box("Porch beam", root, new Vector3(px0, baseY + 2.7f, pz0), new Vector3(px1, baseY + 2.9f, pz0 + 0.2f), trim, 0.015f, false);
            var porchRoof = GableRoof(px1 - px0 + 0.5f, 2.65f, 0.6f);
            var pr = new GameObject("Porch roof");
            pr.transform.SetParent(root, false);
            pr.transform.position = new Vector3((px0 + px1) * 0.5f, baseY + 2.9f, z0 - 1.325f);
            pr.AddComponent<MeshFilter>().sharedMesh = porchRoof;
            pr.AddComponent<MeshRenderer>().sharedMaterial = roofMat;
            // Railing either side of the steps.
            foreach (var rx in new[] { (px0, x0 + 6.0f), (x0 + 7.9f, px1) })
            {
                Box("Rail", root, new Vector3(rx.Item1, baseY + 0.85f, pz0 + 0.05f), new Vector3(rx.Item2, baseY + 0.92f, pz0 + 0.12f), trim, 0.01f, false);
                for (float bx = rx.Item1 + 0.1f; bx < rx.Item2 - 0.05f; bx += 0.14f)
                    Box("Baluster", root, new Vector3(bx - 0.02f, baseY, pz0 + 0.065f), new Vector3(bx + 0.02f, baseY + 0.85f, pz0 + 0.105f), trim, 0.004f, false);
            }
            // House number and a mailbox post by the walk.
            Box("Mailbox post", root, new Vector3(0.9f, 0.1f, 6.7f), new Vector3(1.0f, 1.15f, 6.8f), VhLookLibrary.Get("Fence_Planks"), 0.01f);
            Box("Mailbox", root, new Vector3(0.75f, 1.15f, 6.5f), new Vector3(1.15f, 1.38f, 7.0f), VhLookLibrary.Paint("MailboxBlue", new Color(0.2f, 0.32f, 0.5f), 1.5f), 0.04f);
        }

        static void Invert(Mesh m)
        {
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            m.triangles = t;
            var n = m.normals;
            for (int i = 0; i < n.Length; i++) n[i] = -n[i];
            m.normals = n;
            m.RecalculateTangents();
        }

        /// <summary>A gable roof (ridge along X) of total footprint w x d, rising <paramref name="rise"/>; thickness 0.12 m;
        /// metre UVs (u along the ridge, v up the slope).</summary>
        static Mesh GableRoof(float w, float d, float rise)
        {
            float hw = w * 0.5f, hd = d * 0.5f, t = 0.12f;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            float slope = Mathf.Sqrt(hd * hd + rise * rise);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ue)
            {
                var nn = Vector3.Cross(b - a, e - a).normalized;
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(e);
                for (int k = 0; k < 4; k++) n.Add(nn);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ue);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            // Top surfaces (south slope faces -Z, north slope +Z).
            Quad(new Vector3(-hw, 0f, -hd), new Vector3(-hw, rise, 0f), new Vector3(hw, rise, 0f), new Vector3(hw, 0f, -hd),
                new Vector2(-hw, 0f), new Vector2(-hw, slope), new Vector2(hw, slope), new Vector2(hw, 0f));
            Quad(new Vector3(hw, 0f, hd), new Vector3(hw, rise, 0f), new Vector3(-hw, rise, 0f), new Vector3(-hw, 0f, hd),
                new Vector2(hw, 0f), new Vector2(hw, slope), new Vector2(-hw, slope), new Vector2(-hw, 0f));
            // Undersides (soffits), a thickness below.
            Quad(new Vector3(hw, -t, -hd), new Vector3(hw, rise - t, 0f), new Vector3(-hw, rise - t, 0f), new Vector3(-hw, -t, -hd),
                new Vector2(hw, 0f), new Vector2(hw, slope), new Vector2(-hw, slope), new Vector2(-hw, 0f));
            Quad(new Vector3(-hw, -t, hd), new Vector3(-hw, rise - t, 0f), new Vector3(hw, rise - t, 0f), new Vector3(hw, -t, hd),
                new Vector2(-hw, 0f), new Vector2(-hw, slope), new Vector2(hw, slope), new Vector2(hw, 0f));
            // Eave edges and rake edges.
            Quad(new Vector3(-hw, -t, -hd), new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd), new Vector3(hw, -t, -hd),
                new Vector2(-hw, 0f), new Vector2(-hw, t), new Vector2(hw, t), new Vector2(hw, 0f));
            Quad(new Vector3(hw, -t, hd), new Vector3(hw, 0f, hd), new Vector3(-hw, 0f, hd), new Vector3(-hw, -t, hd),
                new Vector2(hw, 0f), new Vector2(hw, t), new Vector2(-hw, t), new Vector2(-hw, 0f));
            foreach (var sx in new[] { -1f, 1f })
            {
                float x = sx * hw;
                Quad(new Vector3(x, -t, -hd), new Vector3(x, rise - t, 0f), new Vector3(x, rise, 0f), new Vector3(x, 0f, -hd),
                    new Vector2(0f, 0f), new Vector2(slope, 0f), new Vector2(slope, t), new Vector2(0f, t));
                Quad(new Vector3(x, 0f, hd), new Vector3(x, rise, 0f), new Vector3(x, rise - t, 0f), new Vector3(x, -t, hd),
                    new Vector2(0f, t), new Vector2(slope, t), new Vector2(slope, 0f), new Vector2(0f, 0f));
            }
            var mesh = new Mesh { name = "Gable roof " + w.ToString("0.#") + "x" + d.ToString("0.#") };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0);
            FixRoofWinding(mesh, rise);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Makes each roof face front-face along its own normal (computed from the shape: up-ish on top, down under).</summary>
        static void FixRoofWinding(Mesh mesh, float rise)
        {
            var v = mesh.vertices; var t = mesh.triangles; var nrm = mesh.normals;
            var centre = new Vector3(0f, rise * 0.35f, 0f);
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                var face = Vector3.Cross(b - a, c - a);
                var mid = (a + b + c) / 3f;
                if (Vector3.Dot(face, mid - centre) < 0f) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
                var nn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                nrm[t[i]] = nn; nrm[t[i + 1]] = nn; nrm[t[i + 2]] = nn;
            }
            mesh.triangles = t;
            mesh.normals = nrm;
        }

        /// <summary>A triangular gable end (in the YZ plane, both sides), base width d, height h, metre UVs.</summary>
        static Mesh GableEnd(float d, float h)
        {
            float hd = d * 0.5f;
            var a = new Vector3(0f, 0f, -hd); var b = new Vector3(0f, h, 0f); var c = new Vector3(0f, 0f, hd);
            var mesh = new Mesh { name = "Gable end" };
            mesh.SetVertices(new List<Vector3> { a, b, c, a, c, b });
            mesh.SetNormals(new List<Vector3> { Vector3.left, Vector3.left, Vector3.left, Vector3.right, Vector3.right, Vector3.right });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(-hd, 0f), new Vector2(0f, h), new Vector2(hd, 0f), new Vector2(-hd, 0f), new Vector2(hd, 0f), new Vector2(0f, h) });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 3, 4, 5 }, 0);
            // Fix each triangle to face along its stored normal.
            var verts = mesh.vertices; var tr = mesh.triangles; var nn = mesh.normals;
            for (int i = 0; i < tr.Length; i += 3)
            {
                var f = Vector3.Cross(verts[tr[i + 1]] - verts[tr[i]], verts[tr[i + 2]] - verts[tr[i]]);
                if (Vector3.Dot(f, nn[tr[i]]) < 0f) { int k = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = k; }
            }
            mesh.triangles = tr;
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- The neighbour, fences, planting, the hillside ----------

        static void Neighbour(Transform world)
        {
            var root = new GameObject("Neighbour (brick bungalow)").transform;
            root.SetParent(world, false);
            var brick = VhLookLibrary.Get("Brick_Red");
            var trim = VhLookLibrary.Paint("TrimWhite", new Color(0.95f, 0.94f, 0.9f));
            var glass = VhLookLibrary.Glass();
            var door = VhLookLibrary.Paint("DoorGreen", new Color(0.16f, 0.3f, 0.22f), 1.4f);
            float x0 = -34f, z0 = 17f, w = 16f, d = 11f, baseY = 0.45f, h = 3.2f;
            Box("Foundation", root, new Vector3(x0 - 0.05f, 0f, z0 - 0.05f), new Vector3(x0 + w + 0.05f, baseY, z0 + d + 0.05f), VhLookLibrary.Get("Concrete_Kerb"), 0.03f);
            Wall(root, "Wall front", new Vector3(x0, baseY, z0), Vector3.forward, w, h, 0.25f,
                new List<Opening> { new Opening(1.5f, 0.8f, 3.5f, 2.4f), new Opening(5f, 0f, 6.1f, 2.2f, true), new Opening(8f, 0.8f, 10f, 2.4f), new Opening(12f, 0.8f, 14f, 2.4f) },
                brick, trim, glass, door);
            Wall(root, "Wall east", new Vector3(x0 + w, baseY, z0 + 0.25f), Vector3.left, d - 0.5f, h, 0.25f,
                new List<Opening> { new Opening(4.5f, 0.8f, 6.3f, 2.4f) }, brick, trim, glass, door);
            Wall(root, "Wall west", new Vector3(x0, baseY, z0 + d - 0.25f), Vector3.right, d - 0.5f, h, 0.25f, new List<Opening>(), brick, trim, glass, door);
            Wall(root, "Wall back", new Vector3(x0 + w, baseY, z0 + d), Vector3.back, w, h, 0.25f, new List<Opening>(), brick, trim, glass, door);
            var shell = new GameObject("Interior shell");
            shell.transform.SetParent(root, false);
            var sm = VhLookUv.BevelBox(new Vector3(w - 0.54f, h - 0.04f, d - 0.54f), 0.01f);
            Invert(sm);
            shell.transform.position = new Vector3(x0 + w * 0.5f, baseY + h * 0.5f, z0 + d * 0.5f);
            shell.AddComponent<MeshFilter>().sharedMesh = sm;
            shell.AddComponent<MeshRenderer>().sharedMaterial = VhLookLibrary.Tinted("Plaster_Interior", "WarmRoom", new Color(0.98f, 0.9f, 0.78f));
            var roof = GableRoof(w + 1f, d + 1f, 2.4f);
            var rg = new GameObject("Roof (slate shingles)");
            rg.transform.SetParent(root, false);
            rg.transform.position = new Vector3(x0 + w * 0.5f, baseY + h, z0 + d * 0.5f);
            rg.AddComponent<MeshFilter>().sharedMesh = roof;
            rg.AddComponent<MeshRenderer>().sharedMaterial = VhLookLibrary.Get("Roof_ShinglesSlate");
            var gable = GableEnd(d, 2.4f * d / (d + 1f));
            foreach (var gx in new[] { x0, x0 + w })
            {
                var g = new GameObject("Gable end");
                g.transform.SetParent(root, false);
                g.transform.position = new Vector3(gx, baseY + h, z0 + d * 0.5f);
                g.AddComponent<MeshFilter>().sharedMesh = gable;
                g.AddComponent<MeshRenderer>().sharedMaterial = VhLookLibrary.Get("Siding_WoodBrown");
            }
        }

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

        static void Planting(Transform world)
        {
            var root = new GameObject("Trees").transform;
            root.SetParent(world, false);
            VhLookTrees.Place(root, new Vector3(-1f, 0.1f, 11f), 2, 10f);       // the lawn's maple (backlit in shot 03)
            VhLookTrees.Place(root, new Vector3(-15f, 0.1f, 9f), 5, 12f);       // by the board fence
            VhLookTrees.Place(root, new Vector3(20f, 0.1f, 10f), 3, 8.5f);      // at the corner
            VhLookTrees.Place(root, new Vector3(39f, 0.1f, 14f), 6, 11f);       // across the cross street
            VhLookTrees.Place(root, new Vector3(-24f, 0.1f, -10f), 4, 9.5f);    // the south verge
            VhLookTrees.Place(root, new Vector3(12f, 0.1f, -12f), 1, 10.5f, true);
            // Low shrubs along the flower bed (small crowns on short trunks).
            for (int i = 0; i < 6; i++)
                VhLookTrees.Place(root, new Vector3(-7.4f + i * 2.4f, 0.13f, 17.2f), 10 + i, 1.6f);
        }

        /// <summary>The hill rising north into the haze: a heightfield of worn grass, dotted with trees and houses.</summary>
        static void Hillside(Transform world)
        {
            var root = new GameObject("Hillside (backdrop)").transform;
            root.SetParent(world, false);
            const int nx = 90, nz = 90;
            float xMin = -320f, xMax = 320f, zMin = 38f, zMax = 640f;
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = Mathf.Lerp(xMin, xMax, i / (float)nx), z = Mathf.Lerp(zMin, zMax, j / (float)nz);
                    verts[j * (nx + 1) + i] = new Vector3(x, HillHeight(x, z), z);
                    uvs[j * (nx + 1) + i] = new Vector2(x, z);
                }
            var tris = new int[nx * nz * 6];
            int t = 0;
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "Hillside", indexFormat = IndexFormat.UInt32, vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var hill = new GameObject("Hillside ground");
            hill.transform.SetParent(root, false);
            hill.AddComponent<MeshFilter>().sharedMesh = mesh;
            hill.AddComponent<MeshRenderer>().sharedMaterial = VhLookLibrary.Get("Grass_DirtMix");

            var rng = new System.Random(42);
            float R() => (float)rng.NextDouble();
            var trees = new GameObject("Hill trees").transform;
            trees.SetParent(root, false);
            for (int k = 0; k < 70; k++)
            {
                float x = Mathf.Lerp(-260f, 260f, R()), z = Mathf.Lerp(55f, 520f, R());
                if (Mathf.Abs(x - 30f) < 10f) continue;   // keep the cross street clear
                VhLookTrees.Place(trees, new Vector3(x, HillHeight(x, z) - 0.2f, z), 1 + k % 6, 8f + Mathf.Round(R() * 6f), R() < 0.18f);
            }
            var houses = new GameObject("Hill houses").transform;
            houses.SetParent(root, false);
            var tints = new[] { new Color(0.95f, 0.88f, 0.7f), new Color(0.72f, 0.8f, 0.86f), new Color(0.9f, 0.78f, 0.72f), new Color(0.82f, 0.86f, 0.74f), new Color(0.97f, 0.95f, 0.9f) };
            for (int k = 0; k < 26; k++)
            {
                float side = k % 2 == 0 ? 1f : -1f;
                float z = 70f + (k / 2) * 34f + R() * 8f;
                float x = 30f + side * (22f + R() * 10f);
                if (k >= 14) x = Mathf.Lerp(-220f, 220f, R());
                float y = HillHeight(x, z);
                var wallMat = VhLookLibrary.Tinted("Clapboard_Painted", "Hill" + (k % tints.Length), tints[k % tints.Length]);
                var roofMat = VhLookLibrary.Get(k % 3 == 0 ? "Roof_ShinglesSlate" : "Roof_ShinglesAsphalt");
                float w = 10f + R() * 4f, d = 8f + R() * 3f, h = R() < 0.5f ? 3.2f : 5.8f;
                var hgo = new GameObject("House " + k).transform;
                hgo.SetParent(houses, false);
                hgo.position = new Vector3(x, y - 0.4f, z);
                hgo.rotation = Quaternion.Euler(0f, x < 30f ? 90f : -90f, 0f);
                var body = VhLookUv.Block("Body", hgo, hgo.position + Vector3.up * (h * 0.5f + 0.4f), new Vector3(w, h + 0.8f, d), wallMat, 0.05f);
                body.transform.rotation = hgo.rotation;
                var rm = GableRoof(w + 0.8f, d + 0.8f, 2.6f);
                var r = new GameObject("Roof");
                r.transform.SetParent(hgo, false);
                r.transform.localPosition = new Vector3(0f, h + 0.8f, 0f);
                r.AddComponent<MeshFilter>().sharedMesh = rm;
                r.AddComponent<MeshRenderer>().sharedMaterial = roofMat;
                var g = GableEnd(d, 2.6f * d / (d + 0.8f));
                foreach (var gx in new[] { -w * 0.5f, w * 0.5f })
                {
                    var ge = new GameObject("Gable");
                    ge.transform.SetParent(hgo, false);
                    ge.transform.localPosition = new Vector3(gx, h + 0.8f, 0f);
                    ge.AddComponent<MeshFilter>().sharedMesh = g;
                    ge.AddComponent<MeshRenderer>().sharedMaterial = wallMat;
                }
            }
        }

        static float HillHeight(float x, float z)
        {
            float rise = Mathf.Max(0f, z - 45f);
            float h = rise * 0.11f + rise * rise * 0.00008f;
            h += (Mathf.PerlinNoise(x * 0.012f + 3.1f, z * 0.012f + 7.7f) - 0.5f) * Mathf.Min(1f, rise / 60f) * 14f;
            h += Mathf.Abs(x) * 0.02f * Mathf.Min(1f, rise / 80f);
            return h - 0.25f;
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
