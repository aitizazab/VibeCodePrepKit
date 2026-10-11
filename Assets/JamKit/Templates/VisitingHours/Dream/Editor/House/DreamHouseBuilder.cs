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
                case HouseLook.Hallway: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseHallwayFloor", Wood, 0.04f), DreamHouseDressing.PatternFloorboards, 2.4f, 0.28f);
                case HouseLook.RunWall: return DreamEscapeBuilder.Pastel("HouseHallwayRunWall", P[6], 0.05f);
                case HouseLook.Lintel: return DreamEscapeBuilder.Pastel("HouseDoor", P[0], 0.05f);
                case HouseLook.Stairs: return DreamEscapeBuilder.Pastel("HouseStairs", new Color(0.8f, 0.62f, 0.5f), 0.05f);
                case HouseLook.Floor: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseFloor", Wood, 0.03f), DreamHouseDressing.PatternFloorboards, 2.4f, 0.28f);
                case HouseLook.Wall: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseWallpaper", Wallpaper, 0.04f), DreamHouseDressing.PatternWallpaper, 1.2f, 0.35f);
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
                case HouseLook.TippedFloor: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseTippedFloor", Wood, 0.05f), DreamHouseDressing.PatternFloorboards, 2.4f, 0.28f);
                case HouseLook.TippedWall: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseTippedWall", WallpaperTipped, 0.04f), DreamHouseDressing.PatternWallpaper, 1.2f, 0.35f);
                case HouseLook.Porch: return DreamEscapeBuilder.Pastel("HouseHall", P[10], 0.04f);
                case HouseLook.Header: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseWallpaper", Wallpaper, 0.04f), DreamHouseDressing.PatternWallpaper, 1.2f, 0.35f);
                case HouseLook.Chimney: return DreamEscapeBuilder.Pastel("DecorBrick", new Color(0.86f, 0.55f, 0.5f), 0.03f);
                case HouseLook.Bookcase: return DreamEscapeBuilder.Pastel("HouseBookcase", new Color(0.78f, 0.6f, 0.46f), 0.04f);
                case HouseLook.WindowSeat: return DreamEscapeBuilder.Pastel("HouseWindowSeat", new Color(0.95f, 0.72f, 0.7f), 0.04f);
                case HouseLook.Bath: return DreamEscapeBuilder.Pastel("HouseBath", P[0], 0.04f);
                case HouseLook.Railing: return DreamEscapeBuilder.Pastel("StairRail", new Color(0.62f, 0.46f, 0.38f), 0.03f);
                case HouseLook.KneeWall:
                case HouseLook.Gable:
                case HouseLook.Roof: return DreamEscapeBuilder.Pastel("AtticBoards", new Color(0.84f, 0.7f, 0.58f), 0.03f);
                case HouseLook.RoofStep: return DreamEscapeBuilder.Pastel("AtticBoards", new Color(0.84f, 0.7f, 0.58f), 0.03f);
                case HouseLook.Storage: return DreamEscapeBuilder.Pastel("AtticStorage", new Color(0.74f, 0.6f, 0.5f), 0.04f);
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
            DreamHouseDressing.Begin();
            for (int i = 0; i < c.pieces.Count; i++) Piece(h, i, root);
            DreamHouseDressing.Report();
            var hallwayEnd = HallwayEnd(h, root);
            Details(h, root);
            DreamHouseRooms.Build(h, root);   // the architecture pass: walls with character, windows, stairs, hatch, attic
            DreamHouseDecor.Decorate(root);   // art pass 5: a lived-in home (decoration only)
            BuildStealth(h, root, out var theo, out var counter);
            var lena = CutOut("Lena (at the stove)", DreamArt.Lena, root, U(h.lenaAt), LenaCardHeight);
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
            if (!p.runnable && p.kind != LabPieceKind.Ramp) DreamHouseDressing.Furnish(p.name, go, root);   // visual only
            if (look == HouseLook.RoofStep || (look == HouseLook.Stairs && p.Min.Z >= DreamHouse.HubZ0)) r.enabled = false;   // behind the roof; the stairs are drawn as steps
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
            // The house front (art pass 4): framed windows either side of the door, clapboard lines, a porch roof on two
            // posts over the door, and a wide painted gable on top (the front is a flat: nothing stands behind it).
            foreach (float x in new[] { -8f, -4.5f, 4.5f, 8f })
            {
                FramedWindow("Front window", t, new Vector3(x, 3.2f, -0.02f), Vector3.back, 1.6f, 1.8f);
                FramedWindow("Front window (upstairs)", t, new Vector3(x, 5.6f, -0.02f), Vector3.back, 1.4f, 1.3f);
            }
            var board = DreamEscapeBuilder.Pastel("HouseClapboard", Color.Lerp(DreamEscapeBuilder.Palette[1], new Color(0.3f, 0.25f, 0.45f), 0.18f), 0f);
            for (float y = 0.45f; y < 6.9f; y += 0.45f)
                foreach (float side in new[] { -1f, 1f })
                    DreamEscapeBuilder.Box("Clapboard line", t, new Vector3(side * 7.75f, y, -0.012f), new Vector3(12.5f, 0.035f, 0.02f), board);
            var gableMesh = DreamEscapeBuilder.EnsureGableMesh();
            var roofMat = DreamEscapeBuilder.Pastel("HouseFrontRoof", new Color(0.93f, 0.52f, 0.5f), 0.05f, 0f, 1f);
            DreamEscapeBuilder.GableOn(t, gableMesh, new Vector3(0f, 7f + 1.6f, 0.25f), new Vector3(28.6f, 3.2f, 0.7f), roofMat).name = "House front gable";
            DreamEscapeBuilder.GableOn(t, gableMesh, new Vector3(0f, 3.25f, -0.85f), new Vector3(4.4f, 0.9f, 1.7f), roofMat).name = "Porch roof";
            foreach (float x in new[] { -2.05f, 2.05f })
                DreamEscapeBuilder.Box("Porch post", t, new Vector3(x, 1.4f, -1.55f), new Vector3(0.16f, 2.8f, 0.16f), white);
            // The door half open onto the slide: its leaf swung in.
            var leaf = DreamEscapeBuilder.Box("The door, half open", t, new Vector3(-1.1f, 0.5f, 56.9f), new Vector3(0.08f, 3f, 0.9f), white);
            leaf.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            DreamEscapeBuilder.Quad("Glow under the door", t, new Vector3(0f, -0.95f, 55.2f), Vector3.up, 3f, 2f, DreamEscapeBuilder.Glow("HouseUnderDoor", DreamEscapeBuilder.GoColor, 0.9f, 0f, 0.35f));
            // The house's own windows, walls, stairs, hatch and attic: DreamHouseRooms (the architecture pass, 2026-10-12).
            // The table set for four: plates, and a stack of pancakes in the middle.
            // Art pass 4: round plates, and a real stack of pancakes with butter and a drip of syrup ("Sunday pancakes").
            var plate = DreamProps.MeshLook("HousePlate", DreamEscapeBuilder.Palette[0], 0.006f);
            var tc = new Vector3(h.table.X, F, h.table.Z);
            foreach (var off in new[] { new Vector2(-1.1f, 0f), new Vector2(1.1f, 0f), new Vector2(0f, -0.48f), new Vector2(0f, 0.48f) })
                MeshPart("Plate", PrimitiveType.Cylinder, t, tc + new Vector3(off.x, 0.795f, off.y), new Vector3(0.36f, 0.012f, 0.36f), plate);
            MeshPart("Plate", PrimitiveType.Cylinder, t, tc + new Vector3(0f, 0.795f, 0f), new Vector3(0.44f, 0.012f, 0.44f), plate);
            var pancake = DreamProps.MeshLook("HousePancake", new Color(0.93f, 0.7f, 0.4f), 0.006f);
            for (int k = 0; k < 6; k++)
                MeshPart("Pancake", PrimitiveType.Cylinder, t, tc + new Vector3(0.012f * Mathf.Sin(k * 2.1f), 0.825f + k * 0.034f, 0.012f * Mathf.Cos(k * 1.7f)),
                    new Vector3(0.32f - k * 0.006f, 0.016f, 0.32f - k * 0.006f), pancake);
            MeshPart("Butter", PrimitiveType.Cube, t, tc + new Vector3(0f, 0.825f + 6 * 0.034f + 0.005f, 0f), new Vector3(0.07f, 0.04f, 0.06f),
                DreamProps.MeshLook("HouseButter", new Color(1f, 0.93f, 0.62f), 0.004f));
            MeshPart("Syrup", PrimitiveType.Cylinder, t, tc + new Vector3(0f, 0.825f + 5 * 0.034f + 0.018f, 0f), new Vector3(0.22f, 0.004f, 0.2f),
                DreamProps.MeshLook("HouseSyrup", new Color(0.72f, 0.42f, 0.2f), 0f));
            // The fridge calendar (its keys on Thursdays are the creator's; a blank grid here), on the fridge's front.
            var cal = h.memories.Find(m => m.id == "memory_calendar").position;
            DreamEscapeBuilder.Quad("Fridge calendar", t, new Vector3(-6.23f, cal.Y, cal.Z), Vector3.right, 0.5f, 0.66f, Picture(DreamArt.PicCalendar, white));
            // The height marks on the dining room's door frame (the way to the hall): they stop at ten.
            var marks = h.memories.Find(m => m.id == "memory_height_marks").position;
            for (int i = 0; i < 7; i++)
                DreamEscapeBuilder.Box("Height mark", t, new Vector3(marks.X + 0.06f, F + 0.9f + i * 0.09f, marks.Z), new Vector3(0.015f, 0.015f, 0.16f), plum);
            // Theo's drawing, up on top of his bookcase (climb his desk to see it).
            var drawn = h.memories.Find(m => m.id == "memory_drawing").position;
            var drawing = Picture(DreamArt.PicTheoDrawing, white);
            DreamEscapeBuilder.Quad("Theo's drawing (on top of the bookcase)", t, new Vector3(drawn.X, F + 2.012f, drawn.Z), Vector3.up, 0.44f, 0.33f, drawing);
            // The photo wall: the hall's north wall, between the parents' door and the cupboard under the stairs.
            var photos = h.memories.Find(m => m.id == "memory_photos").position;
            var wood = DreamEscapeBuilder.Pastel("HousePhotoFrame", new Color(0.62f, 0.46f, 0.38f), 0.01f);
            float face = DreamHouse.HallZ1 - 0.015f;
            for (int i = 0; i < 6; i++)
            {
                int pic = (i / 3 == 1 ? 1 : 4) + i % 3;   // the top row is photos 1 to 3, left to right; the bottom row 4 to 6
                var at = new Vector3(photos.X + 1.05f - (i % 3) * 1.05f, F + 1.3f + (i / 3) * 0.66f, face);
                DreamEscapeBuilder.Quad("Photo", t, at, Vector3.back, 0.6f, 0.47f, Picture(DreamArt.PicPhoto + pic, white));
                DreamEscapeBuilder.Box("Photo frame", t, at + new Vector3(0f, 0.255f, -0.015f), new Vector3(0.68f, 0.04f, 0.03f), wood);
                DreamEscapeBuilder.Box("Photo frame", t, at + new Vector3(0f, -0.255f, -0.015f), new Vector3(0.68f, 0.04f, 0.03f), wood);
                DreamEscapeBuilder.Box("Photo frame", t, at + new Vector3(0.32f, 0f, -0.015f), new Vector3(0.04f, 0.55f, 0.03f), wood);
                DreamEscapeBuilder.Box("Photo frame", t, at + new Vector3(-0.32f, 0f, -0.015f), new Vector3(0.04f, 0.55f, 0.03f), wood);
            }
            // The two front doors (art pass 2): the way in from the garden, and the way out onto the rain.
            Doorway("The front door (the way in)", t, 0.25f, 0f, true);
            Doorway("The front door (the way out)", t, 318.75f, -5f, false);
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
            var cut = CutOut("Theo (cut-out)", DreamArt.Theo, go2.transform, U(h.theoStart), TheoCardHeight);
            cut.transform.localPosition = Vector3.zero;
        }

        static Material Overlay(string name, float zTest)
        {
            var mat = DreamEscapeBuilder.Mat(name, "JamKit/Stealth/Overlay");
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_ZTestMode")) mat.SetFloat("_ZTestMode", zTest);
            return mat;
        }

        /// <summary>A story picture (the photo wall, the fridge calendar, Theo's drawing) when its style-E file exists in
        /// <c>Art/Dream/</c>, else <paramref name="fallback"/> (the plain card). Visual only: the quads have no colliders.</summary>
        internal static Material Picture(string art, Material fallback)
        {
            if (DreamArt.RealPath(art) == null) return fallback;
            var tex = DreamArt.Get(art, out _);
            Debug.Log("JAMKIT_HOUSE art: " + art + " real");
            var mat = DreamEscapeBuilder.Mat("Picture_" + art, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Cutoff", 0.1f);
            return mat;
        }

        /// <summary>A Unity primitive in the dream's mesh look (no collider, no shadows): small round props.</summary>
        static GameObject MeshPart(string name, PrimitiveType type, Transform parent, Vector3 worldPos, Vector3 scale, Material mat)
        {
            var go = DreamEscapeBuilder.Part(name, type, parent, Vector3.zero, scale, mat);
            go.transform.position = worldPos;
            return go;
        }

        /// <summary>A window (art pass 4): a bright pane, a white frame and cross bars, a sill. On a wall facing
        /// <paramref name="normal"/> (an axis); decoration, no colliders.</summary>
        static void FramedWindow(string name, Transform parent, Vector3 centre, Vector3 normal, float width, float height)
        {
            var pane = DreamEscapeBuilder.Pastel("HouseWindowPane", new Color(0.8f, 0.88f, 1f), 0f, 0.55f);
            var frame = DreamEscapeBuilder.Pastel("HouseWindowFrame", DreamEscapeBuilder.Palette[0], 0.02f);
            DreamEscapeBuilder.Quad(name, parent, centre, normal, width, height, pane);
            bool alongZ = Mathf.Abs(normal.x) > 0.5f;   // the wall runs along z (a side wall) or x (the front)
            Vector3 across = alongZ ? Vector3.forward : Vector3.right;
            Vector3 Size(float along, float up) => alongZ ? new Vector3(0.08f, up, along) : new Vector3(along, up, 0.08f);
            Vector3 c = centre + normal * 0.03f;
            const float bar = 0.1f;
            DreamEscapeBuilder.Box(name + " frame", parent, c + Vector3.up * (height * 0.5f), Size(width + bar, bar), frame);
            DreamEscapeBuilder.Box(name + " frame", parent, c - Vector3.up * (height * 0.5f), Size(width + bar, bar), frame);
            DreamEscapeBuilder.Box(name + " frame", parent, c + across * (width * 0.5f), Size(bar, height + bar), frame);
            DreamEscapeBuilder.Box(name + " frame", parent, c - across * (width * 0.5f), Size(bar, height + bar), frame);
            DreamEscapeBuilder.Box(name + " bar", parent, c, Size(width, 0.05f), frame);
            DreamEscapeBuilder.Box(name + " bar", parent, c, Size(0.05f, height), frame);
            DreamEscapeBuilder.Box(name + " sill", parent, c + normal * 0.06f - Vector3.up * (height * 0.5f + 0.06f), Size(width + 0.3f, 0.07f) + new Vector3(Mathf.Abs(normal.x), 0f, Mathf.Abs(normal.z)) * 0.1f, frame);
        }

        /// <summary>A front door in the 3 m gap of a house-front wall centred on <paramref name="z"/> (decoration, no
        /// colliders): a white frame set into the wall either side, a doorstep on the outside, and double doors in the
        /// family's coral, panelled both sides, with brass knobs. Both doors open the way she goes (+z: into the house
        /// for the way in, out onto the rain for the way out), swung right back so they never narrow the gap.</summary>
        static void Doorway(string name, Transform parent, float z, float floor, bool wayIn)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            var trim = DreamEscapeBuilder.Pastel("HouseTrim", DreamEscapeBuilder.Palette[0], 0.03f);
            var door = DreamEscapeBuilder.Pastel("HouseFrontDoor", new Color(0.88f, 0.45f, 0.44f), 0.03f);
            var panel = DreamEscapeBuilder.Pastel("HouseFrontDoorPanel", new Color(0.95f, 0.6f, 0.56f), 0.02f);
            var brass = DreamEscapeBuilder.Pastel("HouseBrass", new Color(0.95f, 0.78f, 0.38f), 0.01f, 0.2f);
            foreach (float x in new[] { -1.58f, 1.58f })
                DreamEscapeBuilder.Box("Door frame (side)", t, new Vector3(x, floor + 1.32f, z), new Vector3(0.16f, 2.64f, 0.62f), trim);
            DreamEscapeBuilder.Box("Door frame (top)", t, new Vector3(0f, floor + 2.68f, z), new Vector3(3.32f, 0.16f, 0.62f), trim);
            float outside = wayIn ? -1f : 1f;   // the side she comes from (in) or goes to (out)
            DreamEscapeBuilder.Box("Doorstep", t, new Vector3(0f, floor + 0.03f, z + outside * 0.55f), new Vector3(3.2f, 0.06f, 0.5f), trim);
            const float face = 0.28f;   // the leaves hang on the +z face of the wall
            for (int side = -1; side <= 1; side += 2)
            {
                // A leaf hinged at the gap's edge (x = 1.5 * side), built closed across its half of the gap, then swung
                // 88 degrees so its free edge goes +z (a positive yaw moves -x toward +z; a negative one moves +x there).
                var hinge = new GameObject(side < 0 ? "Door (left leaf)" : "Door (right leaf)").transform;
                hinge.SetParent(t, false);
                hinge.position = new Vector3(1.5f * side, floor, z + face);
                float cx = 1.5f * side - side * 0.73f;
                DreamEscapeBuilder.Box("Leaf", hinge, new Vector3(cx, floor + 1.27f, z + face), new Vector3(1.44f, 2.5f, 0.07f), door);
                foreach (float py in new[] { 0.75f, 1.85f })
                    foreach (float s in new[] { -1f, 1f })
                        DreamEscapeBuilder.Box("Panel", hinge, new Vector3(cx, floor + py, z + face + s * 0.045f), new Vector3(1.0f, 0.8f, 0.02f), panel);
                foreach (float s in new[] { -1f, 1f })
                {
                    var knob = DreamEscapeBuilder.Part("Knob", PrimitiveType.Sphere, hinge, Vector3.zero, Vector3.one * 0.08f, brass);
                    knob.transform.position = new Vector3(1.5f * side - side * 1.3f, floor + 1.05f, z + face + s * 0.07f);
                }
                hinge.rotation = Quaternion.Euler(0f, 88f * side, 0f);
            }
        }

        /// <summary>A flat cut-out (the dream's cast) that turns to face the camera.</summary>
        // The cut-outs are sized by their eyes, not their feet-to-hair (2026-10-12, the creator: "why are we taller than
        // Lena?"). The drawings have big heads: Lena's eyes are 18.6% of the card down from its top, Theo's 26.2%, so at the
        // old card heights (1.72 m, 1.38 m) her eyes were at 1.40 m and his at 1.02 m, under Nadia's eye (1.5 m, MoveDef).
        // Now Lena's eyes sit where a 1.72 m woman's do (1.60 m: she's a little taller than Nadia) and Theo's where a
        // ten-year-old's do (about 1.20 m).
        public const float NadiaEye = 1.5f;
        public const float LenaEye = 1.6f, LenaEyeFromTop = 0.186f, TheoEye = 1.2f, TheoEyeFromTop = 0.262f;
        public const float LenaCardHeight = LenaEye / (1f - LenaEyeFromTop), TheoCardHeight = TheoEye / (1f - TheoEyeFromTop);

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
