using System.Collections.Generic;
using JamKit.Look;
using JamKit.Look.EditorTools;
using JamKit.Movement;
using JamKit.Skyline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Task B1 (docs/SKYLINE-PLAN-FLOOR1.md): floor 1 as a scene built FROM DATA, grey-box. <see cref="BuildFloor1"/> reads
    /// <see cref="RepossessArena"/> (the Food Court's solids), <see cref="RepossessSites"/> (sockets and doors) and
    /// <see cref="HotelLobby"/> (the player's start; the lobby's own solids are Task B2's, built in <see cref="BuildLobbyShell"/>)
    /// and makes exactly one collider per <see cref="FloorSolid"/>, named by the solid's id (so <c>lift-shutter</c>,
    /// <c>cab-doors</c> and every <c>facade-pane-r-c</c> can be found, disabled or removed at run time). Nothing is hand-placed:
    /// <c>SkylineLint.FloorDataMatch</c> checks the scene against the same data. The old arena build stays as it was
    /// (<see cref="Build"/>); this scene is <see cref="SkylineScenes.Floor1"/>, outside <see cref="SkylineScenes.All"/> until
    /// the switch-over task.
    /// <list type="bullet">
    /// <item>Boxes through the old <c>Box</c> helper (cube with its box collider, <see cref="MovementSurface.runnable"/> from the
    /// data); ramps and escalators as a wedge mesh with a convex MeshCollider (<see cref="FloorMeshes.Wedge"/>), so the
    /// collider's bounds equal the data's box and its top is the data's slope.</item>
    /// <item><b>Glass</b> solids (<c>FloorSolid.glass</c>) sit on the <see cref="FloorGlassLayer"/> layer (made in
    /// ProjectSettings/TagManager.asset on first build): movement still collides with it (the controller's mask is everything
    /// but Ignore Raycast); bots' sight skips it (<c>SkylineBot.IgnoredBySight</c>, W4); the player's bullets are meant to pass
    /// too, which needs <c>SkylineShooting.Mask</c> to skip the layer (A2's file, not this task's; note
    /// <c>RepossessArena.ShotBlockers</c> keeps the glass balustrades as shot-blockers).</item>
    /// <item>One marker per socket (<c>Socket &lt;id&gt;</c>, a flat violet disc as the circle decal root) and per spawn door
    /// (<c>Door &lt;id&gt;</c>, yawed like the data), the exit lift's zone, the arrival point, the player at the hotel hallway's
    /// start, the HUD, the ink rig and look, three reflection probes (the lobby and hallway, the pit, the ring and halls) and a
    /// first set of stills. No director: X0 adds <c>SkylineFloorDirector</c> through <see cref="WireDirector"/>.</item>
    /// </list>
    /// Extension points for the other floor tasks (each is a <c>static partial void</c> declared here; the owner writes the
    /// body in its own <c>SkylineArenaBuilder.Floor.&lt;Part&gt;.cs</c> and takes the arena root): B2 <see cref="BuildLobbyShell"/>;
    /// X0 <see cref="WireDirector"/>; the dressing tasks <see cref="FloorDressShops"/> (DR1), <see cref="FloorDressPit"/> (DR2),
    /// <see cref="FloorDressCorridor"/> (DR3), <see cref="FloorDressUpper"/> (DR4), <see cref="FloorDressLobby"/> (DR5). They run in
    /// that order after the solids and markers and BEFORE the ink conversion, so what they add is converted with the rest.
    /// Helpers for them: <see cref="FloorBox"/>, <see cref="FloorRamp"/>, <see cref="FloorSolidObject"/>,
    /// <see cref="FloorMaterialFor"/>, <see cref="FloorObject"/>, <see cref="FloorMarker"/>, <see cref="FloorStill"/>.
    /// </summary>
    public static partial class SkylineArenaBuilder
    {
        /// <summary>The layer glass solids sit on (movement collides with it; sight and bullets pass).</summary>
        public const string FloorGlassLayer = SkylineBot.GlassLayerName;

        // ------------------------------------------------------------------ extension points

        /// <summary>B2: the hotel lobby and staff hallway from <see cref="HotelLobby.Solids"/> (use <see cref="FloorSolidObject"/>
        /// and <see cref="FloorMaterialFor"/>; the lobby's group goes under <paramref name="arena"/>).</summary>
        static partial void BuildLobbyShell(Transform arena);

        /// <summary>X0: adds the wave-3 runtime components (the floor director, repossession, doors, facade, lobby, music, HUD parts).</summary>
        static partial void WireDirector(Transform arena, SkylinePlayer player, SkylineHud hud);

        static partial void FloorDressShops(Transform arena);       // DR1
        static partial void FloorDressPit(Transform arena);         // DR2
        static partial void FloorDressCorridor(Transform arena);    // DR3
        static partial void FloorDressUpper(Transform arena);       // DR4
        static partial void FloorDressLobby(Transform arena);       // DR5

        // ------------------------------------------------------------------ state of one build

        static readonly Dictionary<string, GameObject> _floorObjects = new Dictionary<string, GameObject>();
        static FloorPalette _floorPalette;
        static int _floorGlassLayerIndex;

        /// <summary>The grey-box materials by role (D0 and the dressing tasks re-skin them).</summary>
        sealed class FloorPalette
        {
            public Material floor, wall, ceiling, shopfront, pilaster, column, deck, ramp, escalator, rim, table, planter, kiosk, lift, props, fallback;
            public Material glassFacade, glassRail, glassBridge, glassSkylight;
            public Material socket;

            public Material For(FloorSolid s)
            {
                if (s.glass)
                {
                    switch (s.role)
                    {
                        case "facade-pane": return glassFacade;
                        case "deck": return glassBridge;
                        case "skylight": return glassSkylight;
                        default: return glassRail;
                    }
                }
                switch (s.role)
                {
                    case "floor": return floor;
                    case "wall": return wall;
                    case "ceiling": return ceiling;
                    case "shopfront": return shopfront;
                    case "pilaster": return pilaster;
                    case "column": return column;
                    case "deck": return deck;
                    case "ramp": return ramp;
                    case "stairs": return ramp;
                    case "escalator": return escalator;
                    case "rim": return rim;
                    case "table": return table;
                    case "planter": return planter;
                    case "kiosk": return kiosk;
                    case "lift": return lift;
                    case "desk": case "cart": case "stack": case "counter": case "locker": case "gantry": return props;
                    default: return fallback;
                }
            }
        }

        static Color FloorHex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static FloorPalette MakeFloorPalette() => new FloorPalette
        {
            // the style sheet: walls cool blue-white (plaster about #E9EDF2), floors warm cream (about #CDBB9C)
            floor = Lit("SK_Floor_Cream", FloorHex("#CDBB9C")),
            wall = Lit("SK_Wall_Plaster", FloorHex("#E9EDF2")),
            ceiling = Lit("SK_Ceiling", FloorHex("#C7CED8")),
            shopfront = Lit("SK_Shopfront", FloorHex("#DCE2EA")),
            pilaster = Lit("SK_Pilaster", FloorHex("#D3D9E2")),
            column = Lit("SK_Column", FloorHex("#D8DDE5")),
            deck = Lit("SK_Deck", FloorHex("#D8D2C4")),
            ramp = Lit("SK_Ramp", FloorHex("#C4B79A")),
            escalator = Lit("SK_Escalator", FloorHex("#AEB6C2")),
            rim = Lit("SK_PitRim", FloorHex("#D0D6DF")),
            table = Lit("SK_Table", FloorHex("#B39668")),
            planter = Lit("SK_Planter_Floor", FloorHex("#8BA58B")),
            kiosk = Lit("SK_Kiosk_Floor", FloorHex("#D9CBA6")),
            lift = Lit("SK_Lift", FloorHex("#9AA3AE")),
            props = Lit("SK_Lobby_Props", FloorHex("#D9CBA6")),
            fallback = Lit("SK_Floor_Default", FloorHex("#D5D9E0")),
            glassFacade = Glass("SK_Glass_Facade", new Color(0.62f, 0.78f, 0.9f, 0.14f)),
            glassRail = Glass("SK_Glass_Balustrade_Floor", new Color(0.7f, 0.8f, 0.88f, 0.25f)),
            glassBridge = Glass("SK_Glass_Bridge", new Color(0.72f, 0.84f, 0.92f, 0.3f)),
            glassSkylight = Glass("SK_Glass_Skylight_Floor", new Color(0.75f, 0.85f, 0.95f, 0.12f)),
            socket = Emissive("SK_Emissive_TowCircle", new Color(1.1f, 0.55f, 2.4f)),   // objective violet, HDR
        };

        // ------------------------------------------------------------------ the entry

        /// <summary>Builds <see cref="SkylineScenes.Floor1"/> (or any path) and saves it.</summary>
        public static void BuildFloor1(string path)
        {
            var scene = SkylineScenes.NewScene();
            SkylineLookLibrary.BeginBuild();
            _floorObjects.Clear();
            _floorGlassLayerIndex = FloorEnsureLayer(FloorGlassLayer);

            bool look = false, ink = false;
            try
            {
                SkylineLook.ApplyInk(scene);
                look = ink = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("SKYLINE_LOOK ink failed, the afternoon is used: " + e);
                try
                {
                    SkylineLook.ApplyAfternoon(scene);
                    look = true;
                }
                catch (System.Exception e2)
                {
                    Debug.LogError("SKYLINE_LOOK afternoon failed, the plain sun is used: " + e2);
                    Light();
                }
            }

            _floorPalette = MakeFloorPalette();
            var arena = new GameObject("Arena").transform;

            // ---------------------------------------------------------- the Food Court's solids, one object each
            var solidsRoot = Group(arena, "Food Court Solids");
            var roleGroups = new Dictionary<string, Transform>();
            foreach (var s in RepossessArena.Solids())
            {
                if (!roleGroups.TryGetValue(s.role, out var g))
                {
                    g = Group(solidsRoot, s.role);
                    roleGroups[s.role] = g;
                }
                FloorSolidObject(g, s, _floorPalette.For(s));
            }

            // ---------------------------------------------------------- sockets, doors, the exit lift's zone, the arrival
            FloorBuildSockets(arena);
            FloorBuildDoors(arena);
            FloorBuildExitZone(arena);
            FloorMarker(arena, "Arrival Point", U(RepossessArena.ArrivalPoint), 0f);

            // ---------------------------------------------------------- the lobby (B2) and the dressing (DR1-DR5), before the ink conversion
            BuildLobbyShell(arena);
            FloorDressShops(arena);
            FloorDressPit(arena);
            FloorDressCorridor(arena);
            FloorDressUpper(arena);
            FloorDressLobby(arena);

            // ---------------------------------------------------------- the player, the HUD, the director's wiring point
            var player = SkylinePlayerBuilder.Build(null, U(HotelLobby.PlayerStart), 0f);
            var hudGo = new GameObject("HUD");
            var doc = hudGo.AddComponent<UIDocument>();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel != null) doc.panelSettings = panel;
            else Debug.LogWarning("SkylineArenaBuilder: no " + PanelSettingsPath + "; the HUD makes a bare panel at run time");
            var hud = hudGo.AddComponent<SkylineHud>();
            hud.panelSettings = panel;
            hud.player = player;
            WireDirector(arena, player, hud);

            var stills = FloorGreyboxStills();

            // ---------------------------------------------------------- the comic-ink look (docs/SKYLINE-STYLE.md)
            if (ink)
            {
                try
                {
                    SkylineInkSetup.InstallFeature();
                    SkylineInkConvert.EnsureResourceMaterials();
                    SkylineInkConvert.ConvertScene(scene);
                }
                catch (System.Exception e) { Debug.LogError("SKYLINE_INK conversion failed: " + e); }
            }
            SkylineLook.SetupCameras(scene);
            if (look) FloorBakeReflections(scene, path, arena);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("SKYLINE_FLOOR built " + path + ": " + _floorObjects.Count + " solids, " + RepossessSites.Sockets().Length + " sockets, " +
                RepossessSites.Doors().Length + " doors, stills " + stills.childCount);
        }

        // ------------------------------------------------------------------ helpers for the other floor partials

        /// <summary>The object built for the solid with this id (the Food Court's or, once B2 has built it, the lobby's), or null.</summary>
        static GameObject FloorObject(string solidId) => solidId != null && _floorObjects.TryGetValue(solidId, out var go) ? go : null;

        /// <summary>The grey-box material for a solid by its role (what the Food Court's own solids get).</summary>
        static Material FloorMaterialFor(FloorSolid s) => (_floorPalette ?? (_floorPalette = MakeFloorPalette())).For(s);

        /// <summary>One solid as an object: a box or (for a ramp) a wedge, named by the solid's id, with its
        /// <see cref="MovementSurface"/> and, for glass, the glass layer and no shadow. Registered for <see cref="FloorObject"/>.</summary>
        static GameObject FloorSolidObject(Transform parent, FloorSolid s, Material m)
        {
            var go = s.ramp ? FloorRamp(parent, s, m) : FloorBox(parent, s, m);
            if (s.glass)
            {
                if (_floorGlassLayerIndex > 0) go.layer = _floorGlassLayerIndex;
                NoShadow(go);
            }
            _floorObjects[s.id] = go;
            return go;
        }

        /// <summary>A flat solid: a cube spanning the solid's box with its box collider and a MovementSurface saying whether it
        /// can be wall-run. Uses the old builder's <c>Box</c>.</summary>
        static GameObject FloorBox(Transform parent, FloorSolid s, Material m) =>
            Box(parent, s.id, U(s.box.min), U(s.box.max), m, true, s.runnable);

        /// <summary>A ramp or escalator: the data's wedge as a mesh and a convex MeshCollider, the object at the box's centre
        /// (the collider's bounds are then exactly the solid's box).</summary>
        static GameObject FloorRamp(Transform parent, FloorSolid s, Material m)
        {
            var wedge = FloorMeshes.Wedge(s);
            var centre = (s.box.min + s.box.max) * 0.5f;
            var go = new GameObject(s.id);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = U(centre);
            var verts = new Vector3[wedge.positions.Length];
            var normals = new Vector3[wedge.normals.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = U(wedge.positions[i] - centre);
                normals[i] = U(wedge.normals[i]);
            }
            var mesh = new Mesh { name = "Wedge " + s.id };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = wedge.triangles;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
            col.convex = true;
            go.AddComponent<MovementSurface>().runnable = s.runnable;
            return go;
        }

        /// <summary>An empty marker object at a world point, yawed like the player (0 = +Z, clockwise from above).</summary>
        static Transform FloorMarker(Transform parent, string name, Vector3 at, float yawDeg)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            t.SetPositionAndRotation(at, Quaternion.Euler(0f, yawDeg, 0f));
            return t;
        }

        static SkylineStillCamera FloorStill(Transform parent, string shot, Vector3 at, Vector3 lookAt) => Still(parent, shot, at, lookAt);

        // ------------------------------------------------------------------ sockets, doors, the exit zone

        static void FloorBuildSockets(Transform arena)
        {
            var root = Group(arena, "Sockets");
            foreach (var s in RepossessSites.Sockets())
            {
                var m = FloorMarker(root, "Socket " + s.id, U(RepossessSites.CircleCenter(s)), 0f);
                // the circle decal root: a flat violet disc as wide as the tow circle (R2 puts the lit ring on it)
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Circle Decal (grey-box)";
                Object.DestroyImmediate(disc.GetComponent<Collider>());
                disc.AddComponent<SkylineNoCollider>();
                disc.transform.SetParent(m, false);
                disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                float d = FloorRules.CircleRadius * 2f;
                disc.transform.localScale = new Vector3(d, 0.005f, d);     // the primitive is 1 m wide and 2 m tall
                disc.GetComponent<Renderer>().sharedMaterial = _floorPalette.socket;
                NoShadow(disc);
            }
        }

        static Color FloorDoorColor(DoorKind k)
        {
            switch (k)
            {
                case DoorKind.ShopShutter: return new Color(1.7f, 1.2f, 0.3f);
                case DoorKind.SkylightVent: return new Color(1.2f, 1.5f, 2.2f);
                case DoorKind.ServiceHatch: return new Color(0.4f, 1.6f, 0.6f);
                default: return new Color(1.7f, 0.9f, 0.25f);
            }
        }

        static void FloorBuildDoors(Transform arena)
        {
            var root = Group(arena, "Spawn Doors");
            var kinds = new Dictionary<DoorKind, Transform>();
            var mats = new Dictionary<DoorKind, Material>();
            foreach (var d in RepossessSites.Doors())
            {
                if (!kinds.TryGetValue(d.kind, out var g))
                {
                    g = Group(root, d.kind.ToString());
                    kinds[d.kind] = g;
                    mats[d.kind] = Emissive("SK_Emissive_Door_" + d.kind, FloorDoorColor(d.kind));
                }
                var m = FloorMarker(g, "Door " + d.id, U(d.position), d.yawDeg);
                // a small lit block, a nose toward the way the bot faces on arrival
                var body = Box(m, "Marker", m.position + V(-0.3f, -0.3f, -0.3f), m.position + V(0.3f, 0.3f, 0.3f), mats[d.kind], false, null, true);
                var nose = Box(m, "Marker Nose", m.position + V(-0.1f, -0.1f, -0.25f), m.position + V(0.1f, 0.1f, 0.25f), mats[d.kind], false, null, true);
                nose.transform.SetPositionAndRotation(m.position + m.forward * 0.55f, m.rotation);
                NoShadow(body);
                NoShadow(nose);
            }
        }

        static void FloorBuildExitZone(Transform arena)
        {
            var z = RepossessArena.ExitLiftZone;
            var go = new GameObject("Exit Lift Zone");
            go.transform.SetParent(arena, false);
            go.transform.position = U((z.min + z.max) * 0.5f);
            var zone = go.AddComponent<SkylineEncounterZone>();
            zone.encounterIndex = -1;
            zone.encounterId = "exit-lift";
            zone.size = U(z.max - z.min);
        }

        // ------------------------------------------------------------------ the glass layer

        /// <summary>The index of the layer called <paramref name="name"/>, made in ProjectSettings/TagManager.asset (the first free
        /// user layer from 8 to 29) if it does not exist. 0 (Default) and an error if there is no room.</summary>
        static int FloorEnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) { Debug.LogError("SKYLINE_FLOOR no TagManager: the glass stays on the Default layer"); return 0; }
            var tags = new SerializedObject(assets[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < 30; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(p.stringValue)) continue;
                p.stringValue = name;
                tags.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log("SKYLINE_FLOOR layer " + i + " = " + name + " (ProjectSettings/TagManager.asset changed)");
                return i;
            }
            Debug.LogError("SKYLINE_FLOOR no free user layer for " + name + ": the glass stays on the Default layer");
            return 0;
        }

        // ------------------------------------------------------------------ stills (a first set for the agents' own checks; J1 adds the list)

        static Transform FloorGreyboxStills()
        {
            var root = new GameObject("Stills").transform;
            FloorStill(root, "floor-01-arrival", V(0f, 1.7f, -20.5f), V(0f, -1f, 10f));
            FloorStill(root, "floor-02-pit-from-ring", V(-23f, 2.4f, 13f), V(-2f, -3f, 0f));
            FloorStill(root, "floor-03-bridge", V(8f, 7.6f, -14f), V(8f, 5f, 10f));
            FloorStill(root, "floor-04-north-gallery", V(-22f, 7.6f, 20f), V(30f, 7f, 20f));
            FloorStill(root, "floor-05-facade", V(18f, 1.8f, -10f), V(35f, 6f, 10f));
            FloorStill(root, "floor-06-skylight", V(0f, 1.7f, -4f), V(0f, 14f, 8f));
            FloorStill(root, "floor-07-hall-wide", V(-31f, 12f, -26f), V(14f, 1f, 8f));
            FloorStill(root, "floor-08-walk-in-shop", V(-10.4f, 1.7f, 18.9f), V(-10.4f, 1.5f, 23.4f));
            FloorStill(root, "floor-09-corridor", V(-28f, 1.7f, 25.7f), V(20f, 1.7f, 25.7f));
            FloorStill(root, "floor-10-exit-lift", V(-17f, 1.7f, 0f), V(-28f, 1.5f, 0f));
            FloorStill(root, "floor-11-hotel-hallway", V(0f, 1.7f, -76f), V(0f, 2f, -58f));
            return root;
        }

        // ------------------------------------------------------------------ reflection probes (own copy of SkylineLook's bake, three probes)

        /// <summary>Three box-projected probes baked next to the scene: the lobby with the hallway, the pit, and the ring with the
        /// galleries and halls. The pit's has the higher importance, so its volume wins where they overlap.</summary>
        static void FloorBakeReflections(Scene scene, string scenePath, Transform arena)
        {
            foreach (var r in arena.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.ReflectionProbeStatic);
            }
            var ring = VhLook.AddReflectionProbe(scene, new Vector3(0f, 8f, 0f), new Vector3(70.8f, 16.4f, 56.8f), 1, "Reflection probe (food court ring)");
            var pit = VhLook.AddReflectionProbe(scene, new Vector3(-2f, 0f, 0f), new Vector3(29f, 9f, 21f), 2, "Reflection probe (food court pit)");
            var lobby = VhLook.AddReflectionProbe(scene, new Vector3(0f, 4.7f, -53.6f), new Vector3(45.6f, 9.4f, 50.4f), 1, "Reflection probe (hotel lobby)");
            string dir = System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            FloorWarmUpPipeline();
            FloorBake(ring, dir + "/" + name + "_Reflections_Ring.exr", 256);
            FloorBake(pit, dir + "/" + name + "_Reflections_Pit.exr", 128);
            FloorBake(lobby, dir + "/" + name + "_Reflections_Lobby.exr", 128);
        }

        static void FloorBake(ReflectionProbe p, string path, int resolution)
        {
            p.resolution = resolution;
            p.hdr = true;
            p.blendDistance = 1f;
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            p.shadowDistance = 60f;
            try
            {
                VhLookLibrary.EnsureAssetFolder(path);
                p.mode = ReflectionProbeMode.Baked;
                bool ok = Lightmapping.BakeReflectionProbe(p, path);
                if (ok)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var cube = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    if (cube != null)
                    {
                        p.mode = ReflectionProbeMode.Custom;
                        p.customBakedTexture = cube;
                        Debug.Log("SKYLINE_LOOK reflections baked: " + p.name + " -> " + path);
                        return;
                    }
                }
                Debug.LogWarning("SKYLINE_LOOK reflections NOT baked (the sky's reflection is used): " + p.name);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("SKYLINE_LOOK reflections bake failed (the sky's reflection is used): " + p.name + ": " + e.Message);
            }
            p.mode = ReflectionProbeMode.Baked;
        }

        /// <summary>One render through URP so the pipeline exists in a batch-mode editor before the probe bake.</summary>
        static void FloorWarmUpPipeline()
        {
            var go = new GameObject("Skyline floor bake warm-up camera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(64, 64, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            }
            catch (System.Exception e) { Debug.LogWarning("SKYLINE_LOOK warm-up render failed: " + e.Message); }
            finally
            {
                Object.DestroyImmediate(go);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
