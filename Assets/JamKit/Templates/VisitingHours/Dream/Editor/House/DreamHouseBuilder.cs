using System.Collections.Generic;
using JamKit.EditorTools;
using JamKit.Movement;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Builds Night 2's house (docs/DREAM-HOUSE.md) from core <see cref="DreamHouse"/>: every piece as a box in the
    /// dream's pastel doodle look (the front garden and the house front, the stretched hallway with its gold-striped
    /// wall and the far end that moves away, the half-open door onto the slide, the ground floor's rooms and furniture,
    /// the house on its side for the escape, the front door and the rain path); the stealth kit's parts (Theo as a
    /// hide-and-seek <see cref="SeekerAgent"/> with his cone and meter and a cut-out, the hiding spots, light and shade,
    /// five memories under a <see cref="CollectibleCounter"/>); Lena's cut-out at the stove; the house folding shut (the
    /// escape's pursuer); the umbrella and the rain at the front door (<see cref="DreamEscapeBuilder.BuildUmbrella"/>);
    /// the player (the dream escape's, with a <see cref="StealthPlayer"/>); and the <see cref="DreamHouseGame"/>.
    /// Runs as a step of "JamKit/Rebuild Shell Scenes"; "JamKit/Dream House/Rebuild Scene" rebuilds only this.
    /// </summary>
    public static class DreamHouseBuilder
    {
        public const string SceneName = DreamHousePresenter.Scene;
        public const string ScenePath = DreamEscapeBuilder.Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = DreamEscapeBuilder.Root + "/DreamHouseGameConfig.asset";

        // Noon, with rain coming: a paler, cooler dream than the street's golden hour.
        public static readonly Color Sky = new Color(0.84f, 0.84f, 0.92f);
        static readonly Color Wood = new Color(0.86f, 0.7f, 0.56f);
        static readonly Color Wallpaper = new Color(0.98f, 0.9f, 0.84f);
        static readonly Color WallpaperTipped = new Color(0.9f, 0.84f, 0.96f);

        [JamKitBuildStep(246)]
        static void Build(SceneBuilder.Context ctx)
        {
            string scene = BuildEverything(ctx);
            if (string.IsNullOrEmpty(scene)) return;
            if (ctx.config != null && ctx.config.firstGameplayScene == SceneName && ctx.scenes.Count >= 2) ctx.scenes.Insert(2, scene);
            else ctx.scenes.Add(scene);
        }

        [MenuItem("JamKit/Dream House/Rebuild Scene")]
        static void RebuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildEverything(null);
        }

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { DreamEscapeBuilder.Root + "/Scenes", DreamEscapeBuilder.MaterialsDir, DreamEscapeBuilder.Root + "/Data", DreamEscapeBuilder.Root + "/Generated" })
                DreamEscapeBuilder.EnsureFolder(d);
            DreamEscapeBuilder.EnsureTuning();
            MakePreset(ctx);
            DreamEscapeBuilder.ClearMaterials();
            AssetDatabase.SaveAssets();
            string path = BuildScene();
            Debug.Log("JAMKIT_HOUSE_OK " + path);
            return path;
        }

        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Visiting Hours: the house";
            p.tagline = "Pancakes, hide-and-seek, and the house turning over.";
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
                new HowToPlayPage
                {
                    title = "Hide-and-seek",
                    body = "Theo counts, then looks for you: his cone shows where he's looking, the bar above him how close he is to seeing you. Crouch, keep to the shade, hide in the pantry, the curtains, the coats or his wardrobe. Find the memories while he looks: the count is at the top.\nThe dream tool (left mouse, RT) takes the umbrella by the front door.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        static Material Look(HouseLook look)
        {
            var P = DreamEscapeBuilder.Palette;
            switch (look)
            {
                case HouseLook.Lawn: return DreamEscapeBuilder.Pastel("HouseLawn", new Color(0.7f, 0.86f, 0.66f));
                case HouseLook.Facade: return DreamEscapeBuilder.Pastel("HouseFront", P[1], 0.05f);
                case HouseLook.Hallway: return DreamEscapeBuilder.Pastel("HouseHallwayFloor", Wood, 0.04f);
                case HouseLook.RunWall: return DreamEscapeBuilder.Pastel("HouseHallwayRunWall", P[6], 0.05f);
                case HouseLook.Lintel: return DreamEscapeBuilder.Pastel("HouseDoor", P[0], 0.05f);
                case HouseLook.Stairs: return DreamEscapeBuilder.Pastel("HouseStairs", new Color(0.8f, 0.62f, 0.5f), 0.05f);
                case HouseLook.Floor: return DreamEscapeBuilder.Pastel("HouseFloor", Wood, 0.03f);
                case HouseLook.Wall: return DreamEscapeBuilder.Pastel("HouseWallpaper", Wallpaper, 0.04f);
                case HouseLook.Ceiling: return DreamEscapeBuilder.Pastel("HouseCeiling", P[0], 0.02f);
                case HouseLook.Counter: return DreamEscapeBuilder.Pastel("HouseCounter", P[4], 0.05f);
                case HouseLook.Fridge: return DreamEscapeBuilder.Pastel("HouseFridge", P[0], 0.05f);
                case HouseLook.Table: return DreamEscapeBuilder.Pastel("HouseTable", new Color(0.78f, 0.6f, 0.46f), 0.05f);
                case HouseLook.Chair: return DreamEscapeBuilder.Pastel("HouseChair", P[2], 0.05f);
                case HouseLook.Sofa: return DreamEscapeBuilder.Pastel("HouseSofa", P[3], 0.05f);
                case HouseLook.Bed: return DreamEscapeBuilder.Pastel("HouseBed", P[7], 0.05f);
                case HouseLook.Desk: return DreamEscapeBuilder.Pastel("HouseDesk", P[11], 0.05f);
                case HouseLook.Closet: return DreamEscapeBuilder.Pastel("HouseCloset", P[5], 0.05f);
                case HouseLook.Curtain: return DreamEscapeBuilder.Pastel("HouseCurtain", P[8], 0.03f);
                case HouseLook.TippedFloor: return DreamEscapeBuilder.Pastel("HouseTippedFloor", Wood, 0.05f);
                case HouseLook.TippedWall: return DreamEscapeBuilder.Pastel("HouseTippedWall", WallpaperTipped, 0.04f);
                case HouseLook.Porch: return DreamEscapeBuilder.Pastel("HouseHall", P[10], 0.04f);
                default: return DreamEscapeBuilder.Pastel("HouseRainPath", new Color(0.7f, 0.72f, 0.82f), 0.03f);
            }
        }

        static string BuildScene()
        {
            var h = DreamHouse.Build();
            var c = h.course;
            var scene = SceneBuilder.NewScene(Sky, out var cam);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.86f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.94f, 0.88f, 0.86f);
            RenderSettings.ambientGroundColor = new Color(0.56f, 0.52f, 0.66f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 180f;
            RenderSettings.skybox = null;
            var sun = new GameObject("Sun (noon, rain coming)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.96f, 0.94f, 0.98f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            sun.transform.rotation = Quaternion.Euler(60f, -20f, 0f);

            var body = DreamEscapeBuilder.BuildPlayer(cam, c.checkpoints[h.arrivalCheckpoint]);
            body.gameObject.AddComponent<StealthPlayer>().body = body;

            var root = new GameObject("The house").transform;
            for (int i = 0; i < c.pieces.Count; i++) Piece(h, i, root);
            var hallwayEnd = HallwayEnd(h, root);
            Details(h, root);
            BuildStealth(h, root, out var theo, out var counter);
            var lena = CutOut("Lena (at the stove)", DreamArt.Lena, root, U(h.lenaAt), 1.72f);
            var fold = Fold(root);

            var game = new GameObject("Dream House").AddComponent<DreamHouseGame>();
            game.player = body;
            game.theo = theo;
            game.memories = counter;
            game.hallwayEnd = hallwayEnd;
            game.fold = fold;
            game.umbrella = DreamEscapeBuilder.BuildUmbrella(h.rain, root, game, body);
            lena.name = "Lena (cut-out)";

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        // ---------- Pieces ----------

        static void Piece(DreamHouse h, int index, Transform root)
        {
            var p = h.course.pieces[index];
            var look = h.looks[index];
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = p.name;
            go.transform.SetParent(root, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Look(look);
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (p.kind == LabPieceKind.Ramp)
            {
                float len = p.size.Z, drop = p.rampTopStart - p.rampTopEnd;
                float angle = Mathf.Atan2(drop, len) * Mathf.Rad2Deg;
                const float thick = 1f;
                var rot = Quaternion.Euler(angle, 0f, 0f);
                var topMid = new Vector3(p.center.X, (p.rampTopStart + p.rampTopEnd) * 0.5f, p.center.Z);
                go.transform.SetPositionAndRotation(topMid - rot * Vector3.up * (thick * 0.5f), rot);
                go.transform.localScale = new Vector3(p.size.X, thick, Mathf.Sqrt(len * len + drop * drop));
            }
            else
            {
                go.transform.position = U(p.center);
                go.transform.localScale = U(p.size);
            }
            if (!p.solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.AddComponent<MovementSurface>().runnable = p.runnable;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            if (p.runnable)
            {
                // The gold "go" stripe on the face toward the route (the dream escape's look), at the height you catch it.
                float laneX = h.course.LaneAt(p.center.Z);
                float face = laneX < p.center.X ? p.Min.X - 0.03f : p.Max.X + 0.03f;
                float floor = p.Max.Y - 6f;
                if (look == HouseLook.RunWall) floor = 0f;
                var stripe = DreamEscapeBuilder.Pastel("GoStripe", DreamEscapeBuilder.GoColor, 0.03f, 0.75f);
                DreamEscapeBuilder.Box(p.name + " stripe", root, new Vector3(face, floor + 1.45f, p.center.Z), new Vector3(0.06f, 0.42f, p.size.Z - 0.4f), stripe);
                DreamEscapeBuilder.Box(p.name + " stripe (thin)", root, new Vector3(face, floor + 2.05f, p.center.Z), new Vector3(0.06f, 0.1f, p.size.Z - 0.4f), stripe);
            }
            if (look == HouseLook.TippedFloor)
            {
                // Floorboards running along the wall that used to be the floor: it reads as a floor turned on its side.
                var plank = DreamEscapeBuilder.Pastel("HousePlankLine", new Color(0.62f, 0.46f, 0.38f), 0.02f);
                float laneX = h.course.LaneAt(p.center.Z);
                float face = laneX < p.center.X ? p.Min.X - 0.02f : p.Max.X + 0.02f;
                for (float y = p.Max.Y - 0.9f; y > p.Max.Y - 7f; y -= 0.9f)
                    DreamEscapeBuilder.Box(p.name + " plank line", root, new Vector3(face, y, p.center.Z), new Vector3(0.03f, 0.05f, p.size.Z), plank);
            }
        }

        /// <summary>The hallway's far end: a wall with a door that starts close and moves away as she enters (decoration).</summary>
        static Transform HallwayEnd(DreamHouse h, Transform root)
        {
            var end = new GameObject("The hallway's far end (it stretches away)").transform;
            end.SetParent(root, false);
            end.position = new Vector3(0f, 0f, h.stretchFrom);
            var mat = Look(HouseLook.Wall);
            DreamEscapeBuilder.Box("Far wall (left)", end, new Vector3(-3f, 2f, h.stretchFrom), new Vector3(3f, 4f, 0.2f), mat);
            DreamEscapeBuilder.Box("Far wall (right)", end, new Vector3(3f, 2f, h.stretchFrom), new Vector3(3f, 4f, 0.2f), mat);
            DreamEscapeBuilder.Box("Far wall (over the door)", end, new Vector3(0f, 3.2f, h.stretchFrom), new Vector3(3f, 1.6f, 0.2f), mat);
            DreamEscapeBuilder.Quad("The far door (kitchen light)", end, new Vector3(0f, 1.2f, h.stretchFrom + 0.12f), Vector3.back, 3f, 2.4f,
                DreamEscapeBuilder.Glow("HouseKitchenDoor", new Color(1f, 0.9f, 0.7f), 0.8f, 0f, 0.15f));
            return end;
        }

        /// <summary>The dressing: the door leaf ajar onto the slide, windows, the table set for four, the fridge calendar,
        /// the height marks, Theo's drawings, the photo wall, the house's front windows. All decoration (no colliders).</summary>
        static void Details(DreamHouse h, Transform root)
        {
            const float F = DreamHouse.Ground;
            var t = new GameObject("Details").transform;
            t.SetParent(root, false);
            var white = DreamEscapeBuilder.Pastel("HouseTrim", DreamEscapeBuilder.Palette[0], 0.03f);
            var plum = DreamEscapeBuilder.Pastel("HouseInk", new Color(0.36f, 0.3f, 0.46f), 0.02f);
            var window = DreamEscapeBuilder.Glow("HouseWindow", new Color(0.86f, 0.9f, 1f), 0.7f, 0f, 0f);
            // The house front: two windows either side of the door, and a warm doorway.
            foreach (float x in new[] { -8f, -4.5f, 4.5f, 8f })
                DreamEscapeBuilder.Quad("Front window", t, new Vector3(x, 3.2f, -0.02f), Vector3.back, 1.6f, 1.8f, window);
            // The door half open onto the slide: its leaf swung in.
            var leaf = DreamEscapeBuilder.Box("The door, half open", t, new Vector3(-1.1f, 0.5f, 56.9f), new Vector3(0.08f, 3f, 0.9f), white);
            leaf.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            DreamEscapeBuilder.Quad("Glow under the door", t, new Vector3(0f, -0.95f, 55.2f), Vector3.up, 3f, 2f, DreamEscapeBuilder.Glow("HouseUnderDoor", DreamEscapeBuilder.GoColor, 0.9f, 0f, 0.35f));
            // Ground-floor windows (the light coming in) on the outer walls.
            foreach (float z in new[] { 75f, 82f, 94f })
            {
                DreamEscapeBuilder.Quad("Window (west)", t, new Vector3(DreamHouse.HubX0 + 0.02f, F + 1.6f, z), Vector3.right, 1.8f, 1.4f, window);
                DreamEscapeBuilder.Quad("Window (east)", t, new Vector3(DreamHouse.HubX1 - 0.02f, F + 1.6f, z), Vector3.left, 1.8f, 1.4f, window);
            }
            // The table set for four: plates, and a stack of pancakes in the middle.
            var plate = DreamEscapeBuilder.Pastel("HousePlate", DreamEscapeBuilder.Palette[0], 0.02f);
            foreach (var pz in new[] { new Vector2(-7.4f, 77.5f), new Vector2(-4.6f, 77.5f), new Vector2(-6f, 76.5f), new Vector2(-6f, 78.5f) })
                DreamEscapeBuilder.Box("Plate", t, new Vector3(pz.x, F + 0.8f, pz.y), new Vector3(0.5f, 0.03f, 0.5f), plate);
            DreamEscapeBuilder.Box("Pancakes", t, new Vector3(-6f, F + 0.88f, 77.5f), new Vector3(0.45f, 0.14f, 0.45f), DreamEscapeBuilder.Pastel("HousePancakes", new Color(0.98f, 0.8f, 0.5f), 0.03f));
            // The fridge calendar (its keys on Thursdays are the creator's; a blank grid here).
            DreamEscapeBuilder.Quad("Fridge calendar", t, new Vector3(-11.08f, F + 1.4f, 81.7f), Vector3.right, 0.6f, 0.8f, Picture(DreamArt.PicCalendar, white));
            // The height marks on Theo's door frame: they stop at ten.
            for (int i = 0; i < 7; i++)
                DreamEscapeBuilder.Box("Height mark", t, new Vector3(-6.02f, F + 0.9f + i * 0.09f, 89.1f), new Vector3(0.03f, 0.015f, 0.18f), plum);
            // Theo's drawings on his desk, and the photo wall in the study.
            DreamEscapeBuilder.Box("Theo's drawings", t, new Vector3(-7f, F + 0.77f, 89.9f), new Vector3(0.6f, 0.01f, 0.45f), white);
            var drawing = Picture(DreamArt.PicTheoDrawing, null);
            if (drawing != null)
                DreamEscapeBuilder.Quad("Theo's drawing (picture)", t, new Vector3(-7f, F + 0.782f, 89.9f), Vector3.up, 0.6f, 0.45f, drawing);
            for (int i = 0; i < 6; i++)
            {
                int pic = (i / 3 == 1 ? 1 : 4) + i % 3;   // the top row is photos 1 to 3, left to right; the bottom row 4 to 6
                DreamEscapeBuilder.Quad("Photo", t, new Vector3(5.5f + (i % 3) * 1.2f, F + 1.3f + (i / 3) * 0.8f, DreamHouse.HubZ1 - 0.02f), Vector3.back, 0.7f, 0.55f,
                    Picture(DreamArt.PicPhoto + pic, white));
            }
            // The escape: a picture frame and a light hanging sideways off the tipped walls.
            DreamEscapeBuilder.Box("Picture (sideways)", t, new Vector3(-4.4f, 1.2f, 205f), new Vector3(0.05f, 0.8f, 1.1f), white);
            DreamEscapeBuilder.Box("Lamp (sideways)", t, new Vector3(3f, 0.3f, 240f), new Vector3(0.9f, 0.3f, 0.3f), white);
        }

        // ---------- Stealth ----------

        static void BuildStealth(DreamHouse h, Transform root, out SeekerAgent theo, out CollectibleCounter counter)
        {
            var parent = new GameObject("Hide-and-seek").transform;
            parent.SetParent(root, false);
            var zones = new GameObject("Light zones").transform;
            zones.SetParent(parent, false);
            foreach (var l in h.lights)
            {
                var z = new GameObject("Light zone: " + l.name).AddComponent<StealthLightZone>();
                z.transform.SetParent(zones, false);
                z.transform.position = U(l.center);
                z.size = U(l.size);
                z.level = l.level;
            }
            var spots = new GameObject("Hiding spots").transform;
            spots.SetParent(parent, false);
            foreach (var s in h.spots)
            {
                var v = new GameObject(s.name).AddComponent<HidingSpotView>();
                v.transform.SetParent(spots, false);
                v.transform.position = U(s.Center);
                v.kind = s.kind;
                v.size = U(s.Size);
                var a = new GameObject("Check point").transform;
                a.SetParent(v.transform, false);
                a.position = U(s.Approach);
                v.approach = a;
            }

            // The memories: soft glowing cards that spin until she comes close.
            counter = new GameObject("Memories").AddComponent<CollectibleCounter>();
            counter.transform.SetParent(parent, false);
            var card = DreamEscapeBuilder.Glow("HouseMemory", new Color(1f, 0.86f, 0.62f), 1.1f, 0f, 0.4f);
            var halo = DreamEscapeBuilder.Glow("HouseMemoryHalo", new Color(1f, 0.92f, 0.75f), 0.35f, 0f, 0.3f);
            foreach (var m in h.memories)
            {
                var go = new GameObject("Memory: " + m.name);
                go.transform.SetParent(counter.transform, false);
                go.transform.position = U(m.position);
                var item = go.AddComponent<Collectible>();
                item.id = m.id;
                item.sound = "chime";
                var visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                DreamEscapeBuilder.Part("Card", PrimitiveType.Cube, visual, Vector3.zero, new Vector3(0.32f, 0.42f, 0.03f), card);
                DreamEscapeBuilder.Part("Halo", PrimitiveType.Sphere, visual, Vector3.zero, Vector3.one * 0.7f, halo);
                item.visual = visual;
            }

            // Theo: a hide-and-seek seeker (no colliders) with his walking map, his cone and meter, and a cut-out.
            var go2 = new GameObject("Theo (seeker)");
            go2.transform.SetParent(parent, false);
            go2.transform.position = U(h.theoStart);
            go2.transform.rotation = Quaternion.Euler(0f, h.theoYaw, 0f);
            theo = go2.AddComponent<SeekerAgent>();
            theo.mode = SeekerMode.HideAndSeek;
            theo.countYaw = h.theoCountYaw;
            theo.connectDistance = h.connectDistance;
            var wps = new GameObject("Theo's waypoints").transform;
            wps.SetParent(parent, false);
            for (int i = 0; i < h.nodes.Count; i++)
            {
                var n = new GameObject("Waypoint " + i).transform;
                n.SetParent(wps, false);
                n.position = U(h.nodes[i]);
            }
            theo.waypoints = wps;
            var overlay = go2.AddComponent<SeekerOverlay>();
            overlay.seeker = theo;
            overlay.coneMaterial = Overlay("HouseSeeker_Cone", 4f);
            overlay.meterMaterial = Overlay("HouseSeeker_Meter", 8f);
            overlay.meterHeight = 1.75f;
            var cut = CutOut("Theo (cut-out)", DreamArt.Theo, go2.transform, U(h.theoStart), 1.38f);
            cut.transform.localPosition = Vector3.zero;
        }

        static Material Overlay(string name, float zTest)
        {
            var mat = DreamEscapeBuilder.Mat(name, "JamKit/Stealth/Overlay");
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_ZTestMode")) mat.SetFloat("_ZTestMode", zTest);
            return mat;
        }

        /// <summary>A flat cut-out (the dream's cast) that turns to face the camera.</summary>
        /// <summary>A story picture (the photo wall, the fridge calendar, Theo's drawing) when its style-E file exists in
        /// <c>Art/Dream/</c>, else <paramref name="fallback"/> (the plain card). Visual only: the quads have no colliders.</summary>
        static Material Picture(string art, Material fallback)
        {
            if (DreamArt.RealPath(art) == null) return fallback;
            var tex = DreamArt.Get(art, out _);
            Debug.Log("JAMKIT_HOUSE art: " + art + " real");
            var mat = DreamEscapeBuilder.Mat("Picture_" + art, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Cutoff", 0.1f);
            return mat;
        }

        static GameObject CutOut(string name, string art, Transform parent, Vector3 feet, float height)
        {
            var tex = DreamArt.Get(art, out bool placeholder);
            Debug.Log("JAMKIT_HOUSE art: " + art + (placeholder ? " PLACEHOLDER" : " real"));
            var mat = DreamEscapeBuilder.Mat("Cutout_" + art, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", tex);
            float aspect = tex != null && tex.height > 0 ? tex.width / (float)tex.height : 0.5f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            go.AddComponent<DreamFaceCamera>();
            var cardGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cardGo.name = "Card";
            Object.DestroyImmediate(cardGo.GetComponent<Collider>());
            cardGo.transform.SetParent(go.transform, false);
            cardGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            cardGo.transform.localScale = new Vector3(height * aspect, height, 1f);
            cardGo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            var r = cardGo.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>The house folding shut behind her: a wall of wallpaper with a doorframe, tilting in as it gains.</summary>
        static Transform Fold(Transform root)
        {
            var fold = new GameObject("The house folding shut (pursuer)").transform;
            fold.SetParent(root, false);
            var paper = DreamEscapeBuilder.Pastel("HouseFold", new Color(0.74f, 0.66f, 0.84f), 0.06f);
            DreamEscapeBuilder.Box("Folding wall", fold, new Vector3(0f, 7f, 0f), new Vector3(22f, 14f, 0.4f), paper);
            DreamEscapeBuilder.Box("Doorframe", fold, new Vector3(0f, 4.2f, -0.25f), new Vector3(2.6f, 3.4f, 0.1f), DreamEscapeBuilder.Pastel("HouseTrim", DreamEscapeBuilder.Palette[0], 0.03f));
            fold.gameObject.SetActive(false);
            return fold;
        }
    }
}
