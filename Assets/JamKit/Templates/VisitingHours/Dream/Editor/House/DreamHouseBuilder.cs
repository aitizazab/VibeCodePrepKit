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
                case HouseLook.Stairs: return DreamEscapeBuilder.Pastel("HouseStairs", new Color(0.52f, 0.36f, 0.27f), 0.05f);   // walnut (round 5: with the oxblood)
                case HouseLook.Floor: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseFloor", Wood, 0.03f), DreamHouseDressing.PatternFloorboards, 2.4f, 0.28f);
                // The house's paper (the creator's pick, round 4): the oxblood regency stripe.
                case HouseLook.Wall: return DreamHouseDressing.ColourPattern(DreamEscapeBuilder.Pastel("HouseWallpaper", Color.white, 0.04f), DreamHouseRooms.WallTile, DreamHouseRooms.WallTileMetres);
                case HouseLook.Ceiling: return DreamEscapeBuilder.Pastel("HouseCeiling", P[0], 0.02f);
                case HouseLook.Counter: return DreamEscapeBuilder.Pastel("HouseCounter", P[4], 0.05f);
                case HouseLook.Fridge: return DreamEscapeBuilder.Pastel("HouseFridge", P[0], 0.05f);
                case HouseLook.Table: return DreamEscapeBuilder.Pastel("HouseTable", new Color(0.78f, 0.6f, 0.46f), 0.05f);
                case HouseLook.Chair: return DreamEscapeBuilder.Pastel("HouseChair", P[2], 0.05f);
                case HouseLook.Sofa: return DreamEscapeBuilder.Pastel("HouseSofa", P[3], 0.05f);
                case HouseLook.Bed: return DreamEscapeBuilder.Pastel("HouseBed", P[7], 0.05f);
                case HouseLook.Desk: return DreamEscapeBuilder.Pastel("HouseDesk", P[11], 0.05f);
                case HouseLook.Closet: return DreamEscapeBuilder.Pastel("HouseCloset", P[5], 0.05f);
                case HouseLook.Curtain: return DreamEscapeBuilder.Pastel("HouseCurtain", new Color(0.86f, 0.66f, 0.36f), 0.03f);   // ochre
                case HouseLook.TippedFloor: return DreamHouseDressing.Pattern(DreamEscapeBuilder.Pastel("HouseTippedFloor", Wood, 0.05f), DreamHouseDressing.PatternFloorboards, 2.4f, 0.28f);
                case HouseLook.TippedWall: return DreamHouseDressing.ColourPattern(DreamEscapeBuilder.Pastel("HouseTippedWall", Color.white, 0.04f), DreamHouseRooms.WallTile, DreamHouseRooms.WallTileMetres);
                case HouseLook.Street: return DreamEscapeBuilder.Pastel("HouseStreet", new Color(0.62f, 0.62f, 0.72f), 0.03f);
                case HouseLook.Porch: return DreamEscapeBuilder.Pastel("HouseHall", P[10], 0.04f);
                case HouseLook.Header: return DreamHouseDressing.ColourPattern(DreamEscapeBuilder.Pastel("HouseWallpaper", Color.white, 0.04f), DreamHouseRooms.WallTile, DreamHouseRooms.WallTileMetres);
                case HouseLook.Chimney: return DreamEscapeBuilder.Pastel("DecorBrick", new Color(0.86f, 0.55f, 0.5f), 0.03f);
                case HouseLook.Bookcase: return DreamEscapeBuilder.Pastel("HouseBookcase", new Color(0.78f, 0.6f, 0.46f), 0.04f);
                case HouseLook.WindowSeat: return DreamEscapeBuilder.Pastel("HouseWindowSeat", new Color(0.32f, 0.5f, 0.52f), 0.04f);   // teal
                case HouseLook.Bath: return DreamEscapeBuilder.Pastel("HouseBath", P[0], 0.04f);
                case HouseLook.Railing: return DreamEscapeBuilder.Pastel("StairRail", new Color(0.62f, 0.46f, 0.38f), 0.03f);
                case HouseLook.KneeWall:
                case HouseLook.Gable:
                case HouseLook.Roof:
                case HouseLook.RoofStep: return DreamHouseRooms.AtticBoards();
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
            int firstPiece = root.childCount;
            for (int i = 0; i < c.pieces.Count; i++) Piece(h, i, root);
            EscapeRooms(h, root, firstPiece);   // the escape's rooms grouped (they fold shut one by one) and dressed
            DreamHouseDressing.Report();
            var hallwayEnd = HallwayEnd(h, root);
            Details(h, root);
            DreamHouseRooms.Build(h, root);   // the architecture pass: walls with character, windows, stairs, hatch, attic
            DreamHouseExterior.Build(root);   // round 4: the house from its garden, matching the inside
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
            // The house front: DreamHouseExterior (round 4: the outside matches the inside).
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
            // The fridge calendar: "Pancakes!" on every Sunday, small stars on Lena's Thursdays (the re-plot).
            var cal = h.memories.Find(m => m.id == DreamHouseStory.MemCalendar).position;
            DreamEscapeBuilder.Quad("Fridge calendar", t, new Vector3(-6.23f, cal.Y, cal.Z), Vector3.right, 0.5f, 0.66f, Picture(DreamArt.PicCalendarN2, white));
            // Nadia's place card at the fourth chair, in Theo's handwriting (a little tent card on the table's end).
            var seat = h.memories.Find(m => m.id == DreamHouseStory.MemPlaceCard).position;
            var card = DreamEscapeBuilder.Quad("Nadia's place card", t, new Vector3(-2.12f, F + 0.86f, seat.Z), Vector3.right, 0.2f, 0.12f, Picture(DreamArt.PicPlaceCard, white));
            card.transform.rotation = Quaternion.LookRotation(Vector3.left) * Quaternion.Euler(-14f, 0f, 0f);
            // The height marks on the dining room's door frame (the way to the hall): they stop at ten.
            var marks = h.memories.Find(m => m.id == DreamHouseStory.MemHeightMarks).position;
            for (int i = 0; i < 7; i++)
                DreamEscapeBuilder.Box("Height mark", t, new Vector3(marks.X + 0.06f, F + 0.9f + i * 0.09f, marks.Z), new Vector3(0.015f, 0.015f, 0.16f), plum);
            // Theo's drawing, "THE BAD DREAM", up on top of his bookcase in his room (climb his desk to see it).
            var drawn = h.memories.Find(m => m.id == DreamHouseStory.MemBadDream).position;
            var drawing = Picture(DreamArt.PicBadDream, white);
            DreamEscapeBuilder.Quad("Theo's drawing (on top of the bookcase)", t, new Vector3(drawn.X, F + 2.012f, drawn.Z), Vector3.up, 0.44f, 0.33f, drawing);
            // The photo wall: the hall's north wall, between the parents' door and the cupboard under the stairs.
            var photos = h.memories.Find(m => m.id == DreamHouseStory.FactPhoto).position;
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
            Doorway("The front door (the way out)", t, h.frontDoorZ, h.frontDoorFloor, false);
            LenaAtTheWindow(t, h.frontDoorZ + 0.27f, h.frontDoorFloor);
            Street(h, root);
            // The escape's rooms are dressed by EscapeDecor.
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

        // ---------- The escape (rebuilt as the night's peak, 2026-10-12 night) ----------

        /// <summary>Groups the escape's pieces (and their stripes and lines) by room, "Escape room: (name)", so the game can
        /// fold each room shut behind her as one; then dresses each room so it reads as a room of this house that can't
        /// fit in it.</summary>
        static void EscapeRooms(DreamHouse h, Transform root, int firstChild)
        {
            var groups = new Dictionary<string, Transform>();
            foreach (var r in h.escapeRooms)
            {
                var g = new GameObject(DreamHouseGame.RoomGroupPrefix + r.name).transform;
                g.SetParent(root, false);
                groups[r.name] = g;
            }
            var made = new List<Transform>();
            for (int i = firstChild; i < root.childCount; i++) made.Add(root.GetChild(i));
            float start = h.escapeRooms.Count > 0 ? h.escapeRooms[0].s - 2f : float.MaxValue;
            foreach (var c in made)
            {
                if (groups.ContainsValue(c)) continue;
                var r = c.GetComponentInChildren<Renderer>();
                float z = r != null ? r.bounds.center.z : c.position.z;
                if (z < start || z >= h.frontDoorZ - 0.3f) continue;
                string room = null;
                foreach (var er in h.escapeRooms) if (z >= er.s - 0.01f) room = er.name;
                if (room == null) room = h.escapeRooms[0].name;
                c.SetParent(groups[room], true);
            }
            EscapeDecor(h, groups);
        }

        /// <summary>THE SWITCH for the escape's ceilings (the creator decides): true closes in every escape room but the
        /// observatory (open to the sky); false leaves them open to the sky over their walls. Rebuild the house after
        /// changing it.</summary>
        public static bool EscapeCeilings = true;
        /// <summary>How tall the escape's rooms are (floor to ceiling); the last hall meets the front door's wall.</summary>
        public const float EscapeRoomHeight = 7.5f, LastHallHeight = 6f;
        const string ObservatoryRoom = "The observatory, open to the sky", LastHallRoom = "The last hall", AtticStairsRoom = "The attic stairs, the wrong way";

        /// <summary>Theo's window in the left wall before the stairwell door: shut (curtained), or open, lit, with Theo in it
        /// (the game opens it when Lena ended unsure: the shortcut round the door).</summary>
        static void TheosWindow(DreamHouse h, Transform parent, float floor, Material trim)
        {
            float w = DreamHouse.TheoWindowWidth, ht = DreamHouse.TheoWindowHeight, x = DreamHouse.TheoWindowX + 0.01f;
            var c = new Vector3(x, floor + DreamHouse.TheoWindowSill + ht * 0.5f, h.theoWindowZ);
            var t = new GameObject("Theo's window").transform;
            t.SetParent(parent, false);
            DreamEscapeBuilder.Box("Window frame (top)", t, c + new Vector3(0.03f, ht * 0.5f + 0.05f, 0f), new Vector3(0.08f, 0.1f, w + 0.2f), trim);
            DreamEscapeBuilder.Box("Window frame (sill)", t, c + new Vector3(0.06f, -ht * 0.5f - 0.05f, 0f), new Vector3(0.14f, 0.1f, w + 0.3f), trim);
            foreach (float dz in new[] { -w * 0.5f - 0.05f, w * 0.5f + 0.05f })
                DreamEscapeBuilder.Box("Window frame (side)", t, c + new Vector3(0.03f, 0f, dz), new Vector3(0.08f, ht + 0.2f, 0.1f), trim);
            var shut = new GameObject(DreamHouseGame.TheoWindowShut).transform;
            shut.SetParent(t, false);
            DreamEscapeBuilder.Quad("Curtain (drawn)", shut, c, Vector3.right, w, ht, DreamEscapeBuilder.Pastel("HouseCurtain", new Color(0.86f, 0.66f, 0.36f), 0.03f));
            var open = new GameObject(DreamHouseGame.TheoWindowOpenName).transform;
            open.SetParent(t, false);
            DreamEscapeBuilder.Quad("Window (lit, open)", open, c, Vector3.right, w, ht, DreamEscapeBuilder.Glow("HouseWindowLit", new Color(1f, 0.86f, 0.6f), 0.7f, 0f, 0.1f));
            var mat = DreamEscapeBuilder.Mat("Cutout_" + DreamArt.Theo, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", DreamArt.Get(DreamArt.Theo, out _));
            var theo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            theo.name = "Theo (in the window)";
            Object.DestroyImmediate(theo.GetComponent<Collider>());
            theo.transform.SetParent(open, false);
            theo.transform.position = new Vector3(x + 0.03f, floor + DreamHouse.TheoWindowSill + 0.55f, h.theoWindowZ - 0.25f);
            theo.transform.rotation = Quaternion.LookRotation(Vector3.left);
            theo.transform.localScale = new Vector3(0.5f, 1.1f, 1f);
            theo.GetComponent<MeshRenderer>().sharedMaterial = mat;
            open.gameObject.SetActive(false);   // the game opens it
        }

        static void EscapeDecor(DreamHouse h, Dictionary<string, Transform> groups)
        {
            LabPiece P(string n) => h.course.pieces.Find(x => x.name == n);
            float RampY(LabPiece p, float z) => Mathf.Lerp(p.rampTopStart, p.rampTopEnd, Mathf.InverseLerp(p.Min.Z, p.Max.Z, z));
            var white = DreamEscapeBuilder.Pastel("HouseTrim", DreamEscapeBuilder.Palette[0], 0.03f);
            var frame = DreamEscapeBuilder.Pastel("HousePhotoFrame", new Color(0.62f, 0.46f, 0.38f), 0.01f);
            var plateMat = DreamProps.MeshLook("HousePlate", DreamEscapeBuilder.Palette[0], 0.006f);
            var chair = DreamEscapeBuilder.Pastel("HouseChair", DreamEscapeBuilder.Palette[2], 0.05f);
            var night = DreamEscapeBuilder.Pastel("AtticNight", new Color(0.18f, 0.2f, 0.42f), 0f, 0.8f);
            var star = DreamEscapeBuilder.Glow("AtticStar", new Color(1f, 0.96f, 0.82f), 1.4f, 0f, 0.2f);
            var bulb = DreamEscapeBuilder.Glow("StairBulb", new Color(1f, 0.86f, 0.6f), 1.0f, 0f, 0.1f);
            var rnd = new System.Random(31);

            // The rooms' walls and ceilings (2026-10-13 review: the escape's floors read as strips in a void, the pendant, the
            // clock, the calendar and the hanging counters floated, the sky showed over every room). Picture only (no
            // collider, so the course and its wallruns are unchanged). Each floor gets its two side walls up to its room's
            // ceiling: in the tipped rooms the old floor on the left and the old ceiling on the right; the last hall the
            // right way up, papered; the attic in its boards. Each room but the observatory gets a ceiling (EscapeCeilings),
            // the tipped rooms' being the old wall (papered). The observatory keeps its open night (closed at the far end
            // below).
            if (h.escapeRooms.Count > 0)
            {
                var oldFloor = Look(HouseLook.TippedFloor);
                var oldCeiling = Look(HouseLook.Ceiling);
                var paper = Look(HouseLook.Wall);
                var boards = Look(HouseLook.Roof);
                for (int r = 0; r < h.escapeRooms.Count; r++)
                {
                    var er = h.escapeRooms[r];
                    if (er.name == ObservatoryRoom || !groups.TryGetValue(er.name, out var g)) continue;
                    float rs = er.s - 0.01f, re = r + 1 < h.escapeRooms.Count ? h.escapeRooms[r + 1].s : h.frontDoorZ - 0.25f;
                    var floors = h.course.pieces.FindAll(p => p.kind == LabPieceKind.Floor && p.name != "The entrance hall" && p.center.Z >= rs && p.center.Z < re);
                    if (floors.Count == 0) continue;
                    bool lastHall = er.name == LastHallRoom, attic = er.name == AtticStairsRoom;
                    Material left = lastHall ? paper : attic ? boards : oldFloor, right = lastHall ? paper : attic ? boards : oldCeiling;
                    Material over = lastHall ? oldCeiling : attic ? boards : paper;
                    float top = float.MinValue, low = float.MaxValue;
                    foreach (var p in floors) { top = Mathf.Max(top, p.Max.Y); low = Mathf.Min(low, p.Max.Y); }
                    float ceil = top + (lastHall ? LastHallHeight : EscapeRoomHeight);   // the last hall meets the front door's wall
                    // The walls run the room's whole length (2026-10-13 capture: between its floors, over the slides and the
                    // gaps, the sky showed through in slots under the ceiling). Where a run wall stands on that side (and the
                    // 3 m run-up to it), only the strip above it, so the wall she runs stays the one that reads.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var runs = h.course.pieces.FindAll(p => p.kind == LabPieceKind.Wall && p.center.X * side > 4f && p.Max.Z > er.s && p.Min.Z < re);
                        runs.Sort((a, b) => a.Min.Z.CompareTo(b.Min.Z));
                        float z = er.s;
                        void Full(float z0, float z1)
                        {
                            if (z1 - z0 < 0.05f) return;
                            float y0 = low - 2f;
                            DreamEscapeBuilder.Box(side < 0f ? "Room wall (left)" : "Room wall (right)", g, new Vector3(4.6f * side, (y0 + ceil) * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.1f, ceil - y0, z1 - z0), side < 0f ? left : right);
                        }
                        foreach (var w in runs)
                        {
                            float a = Mathf.Max(z, w.Min.Z - 3f), b = Mathf.Min(re, w.Max.Z);
                            Full(z, a);
                            if (b > a && ceil > w.Max.Y + 0.05f)
                                DreamEscapeBuilder.Box(side < 0f ? "Room wall (left, over the run)" : "Room wall (right, over the run)", g, new Vector3(4.6f * side, (w.Max.Y + ceil) * 0.5f, (a + b) * 0.5f), new Vector3(0.1f, ceil - w.Max.Y, b - a), side < 0f ? left : right);
                            z = Mathf.Max(z, b);
                        }
                        Full(z, re);
                    }
                    if (EscapeCeilings)
                        DreamEscapeBuilder.Box("Room ceiling", g, new Vector3(0f, ceil + 0.05f, (er.s + re) * 0.5f), new Vector3(9.3f, 0.1f, re - er.s), over);
                }
            }

            // A chair, stuck to the wall that was its floor: its legs on the old floor (the left wall), its seat sideways.
            void ChairOnTheWall(Transform g, Vector3 foot, float turn)
            {
                var c = new GameObject("Chair (stuck to the wall-floor)").transform;
                c.SetParent(g, false);
                c.position = foot;
                DreamEscapeBuilder.Box("Chair seat", c, foot + new Vector3(0f, 0.46f, 0f), new Vector3(0.46f, 0.05f, 0.46f), chair);
                DreamEscapeBuilder.Box("Chair back", c, foot + new Vector3(-0.2f, 0.75f, 0f), new Vector3(0.05f, 0.55f, 0.46f), chair);
                foreach (var (lx, lz) in new[] { (-0.2f, -0.2f), (0.2f, -0.2f), (-0.2f, 0.2f), (0.2f, 0.2f) })
                    DreamEscapeBuilder.Box("Chair leg", c, foot + new Vector3(lx, 0.22f, lz), new Vector3(0.04f, 0.44f, 0.04f), frame);
                c.rotation = Quaternion.Euler(0f, 0f, -90f) * Quaternion.Euler(0f, turn, 0f);
            }

            void Photo(Transform g, Vector3 at, Vector3 n, int pic, float roll)
            {
                var q = DreamEscapeBuilder.Quad("Photo (in the wrong place)", g, at, n, 0.7f, 0.55f, Picture(DreamArt.PicPhoto + (1 + pic % 6), white));
                q.transform.rotation = q.transform.rotation * Quaternion.Euler(0f, 0f, roll);
                var f = DreamEscapeBuilder.Box("Photo frame", g, at - n * 0.01f, Mathf.Abs(n.x) > 0.5f ? new Vector3(0.02f, 0.66f, 0.82f) : Mathf.Abs(n.y) > 0.5f ? new Vector3(0.82f, 0.02f, 0.66f) : new Vector3(0.82f, 0.66f, 0.02f), frame);
                f.transform.rotation = Quaternion.AngleAxis(roll, n) * f.transform.rotation;
            }

            // The kitchen tilts: the table's plates sliding down it, its chairs stuck to the wall that's now the floor, the
            // pendant hanging sideways, the hatch's frame.
            if (groups.TryGetValue("The kitchen tilts", out var kitchen))
            {
                var table = P("The tilting table");
                for (int i = 0; i < 5; i++)
                {
                    float z = table.Min.Z + 2f + i * 2.8f;
                    MeshPart("Plate (sliding)", PrimitiveType.Cylinder, kitchen, new Vector3(i % 2 == 0 ? -0.6f : 0.6f, RampY(table, z) + 0.02f, z), new Vector3(0.36f, 0.012f, 0.36f), plateMat);
                }
                var floor = P("The tipped kitchen (its wall is the floor)");
                int n = 0;
                foreach (float z in new[] { floor.Min.Z + 3f, floor.Min.Z + 7f, floor.Min.Z + 11f })
                    ChairOnTheWall(kitchen, new Vector3(-4.55f, floor.Max.Y + 1.4f + 0.9f * (n % 2), z), 25f * (n++ - 1));
                var pend = MeshPart("Pendant (hanging sideways)", PrimitiveType.Cylinder, kitchen, new Vector3(3.2f, floor.Max.Y + 2.4f, floor.Min.Z + 9f), new Vector3(0.5f, 0.1f, 0.5f), DreamProps.MeshLook("DecorShade", new Color(0.99f, 0.93f, 0.8f), 0.01f));
                pend.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                DreamEscapeBuilder.Box("Pendant cord (to the old ceiling)", kitchen, new Vector3(3.925f, floor.Max.Y + 2.4f, floor.Min.Z + 9f), new Vector3(1.25f, 0.03f, 0.03f), frame);
                var hatch = P("The hatch (its frame: slide under)");
                DreamEscapeBuilder.Box("Hatch frame (bottom)", kitchen, new Vector3(0f, hatch.Min.Y - 0.06f, hatch.center.Z), new Vector3(9f, 0.12f, hatch.size.Z + 0.1f), white);
            }

            // The hallway turns over while she runs it: its photos, runner and lights turn with it (the game rolls them).
            if (groups.TryGetValue("The hallway turns over", out var hallRoom))
            {
                var hall = P("The hallway (on its side)");
                var wall = P("The hallway's floor (turning into the wall)");
                var turning = new GameObject(DreamHouseGame.TurningGroup).transform;
                turning.SetParent(hallRoom, false);
                // Its pivot half the room's width up, so the walls roll onto the floor and the floor onto the wall.
                turning.position = new Vector3(0f, hall.Max.Y + 4.5f, (hall.Min.Z + wall.Max.Z) * 0.5f);
                var runner = DreamEscapeBuilder.Pastel("HouseRunnerEscape", new Color(0.32f, 0.52f, 0.52f), 0.02f);
                DreamEscapeBuilder.Box("The runner (on the wall)", turning, new Vector3(4.45f, hall.Max.Y + 3f, (hall.Min.Z + wall.Max.Z) * 0.5f), new Vector3(0.04f, 1.0f, wall.Max.Z - hall.Min.Z - 4f), runner);
                for (int i = 0; i < 8; i++)
                {
                    float z = hall.Min.Z + 4f + i * 4.5f;
                    Photo(turning, new Vector3(-2.6f + (i % 3) * 1.9f, hall.Max.Y + 0.012f, z), Vector3.up, i, 90f * (i % 2));
                    DreamEscapeBuilder.Part("Wall light (sideways)", PrimitiveType.Sphere, turning, Vector3.zero, Vector3.one * 0.35f, bulb).transform.position = new Vector3(4.2f, hall.Max.Y + 5.2f, z + 2f);
                }
            }

            // The stairs flatten: the banisters still run down the slide.
            if (groups.TryGetValue("The stairs flatten", out var stairs))
            {
                var ramp = P("The staircase (now a slide)");
                foreach (float x in new[] { -4.3f, 4.3f })
                    for (float z = ramp.Min.Z + 0.5f; z < ramp.Max.Z; z += 1.2f)
                        DreamEscapeBuilder.Box("Banister", stairs, new Vector3(x, RampY(ramp, z) + 0.5f, z), new Vector3(0.06f, 1f, 0.06f), frame);
            }

            // Theo's room where the stairs were: his toys on his bed's floor, stars on the dark.
            if (groups.TryGetValue("Theo's room, where the stairs were", out var theo))
            {
                var toy = new[] { new Color(0.95f, 0.75f, 0.3f), new Color(0.45f, 0.62f, 0.9f), new Color(0.9f, 0.5f, 0.45f) };
                foreach (var p in h.course.pieces)
                {
                    if (p.name != "Theo's bed (a floor now)") continue;
                    for (int i = 0; i < 4; i++)
                        DreamEscapeBuilder.Box("Toy", theo, new Vector3(-3.5f + i * 2.2f, p.Max.Y + 0.15f, p.Min.Z + 1.5f + i * 1.2f), Vector3.one * 0.3f, DreamEscapeBuilder.Pastel("Toy" + (i % 3), toy[i % 3], 0.02f));
                }
                var bunk = P("Theo's bunk (slide under it)");
                foreach (float x in new[] { -4.2f, 4.2f })
                    DreamEscapeBuilder.Box("Bunk post", theo, new Vector3(x, bunk.Min.Y - 0.6f, bunk.center.Z), new Vector3(0.12f, 1.2f, 0.12f), frame);
            }

            // The hallway, again (wrong): the same photos, upside down; a clock without hands; the coats hanging up.
            if (groups.TryGetValue("The hallway, again", out var again))
            {
                var wall = P("The hallway again: the other wall");
                for (int i = 0; i < 6; i++)
                    Photo(again, new Vector3(-4.48f, wall.Max.Y - 3.4f + (i % 2) * 0.8f, wall.Min.Z + 1.5f + i * 2.2f), Vector3.right, i, 180f);
                var hall = P("The hall of the same door");
                for (int i = 1; i <= 3; i++)
                {
                    var sameDoor = P("The same door (half open) " + i);
                    if (sameDoor == null) continue;
                    var leaf = DreamEscapeBuilder.Box("The same door (its leaf)", again, new Vector3(2.6f, sameDoor.Min.Y - 0.05f + 1.1f, sameDoor.Max.Z + 0.5f), new Vector3(0.07f, 2.2f, 0.95f), DreamEscapeBuilder.Pastel("HouseFrontDoor", new Color(0.88f, 0.45f, 0.44f), 0.03f));
                    leaf.transform.rotation = Quaternion.Euler(0f, 70f, 0f);
                    DreamEscapeBuilder.Box("The same door (its frame)", again, new Vector3(0f, sameDoor.Min.Y - 0.04f, sameDoor.center.Z), new Vector3(9f, 0.08f, sameDoor.size.Z + 0.12f), white);
                }
                var face = DreamProps.MeshLook("DecorClockFace", new Color(0.98f, 0.97f, 0.94f), 0.01f);
                var clock = MeshPart("Clock (no hands)", PrimitiveType.Cylinder, again, new Vector3(4.46f, hall.Max.Y + 2.4f, hall.Min.Z + 6f), new Vector3(0.6f, 0.02f, 0.6f), face);
                clock.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // The observatory, open to the sky: night all round the floating boards, stars, the telescope, the round window
            // she runs along.
            if (groups.TryGetValue("The observatory, open to the sky", out var obs))
            {
                var first = P("The observatory's first boards");
                var wall = P("Along the round window (over the sky)");
                float z0 = first.Min.Z - 2f, z1 = wall.Max.Z + 1f, y = first.Max.Y;   // z1: where the attic's room starts
                DreamEscapeBuilder.Quad("Night (left)", obs, new Vector3(-14f, y + 2f, (z0 + z1) * 0.5f), Vector3.right, z1 - z0, 30f, night);
                DreamEscapeBuilder.Quad("Night (right)", obs, new Vector3(14f, y + 2f, (z0 + z1) * 0.5f), Vector3.left, z1 - z0, 30f, night);
                DreamEscapeBuilder.Quad("Night (below)", obs, new Vector3(0f, y - 9f, (z0 + z1) * 0.5f), Vector3.up, 28f, z1 - z0, night);
                DreamEscapeBuilder.Quad("Night (above)", obs, new Vector3(0f, y + 16f, (z0 + z1) * 0.5f), Vector3.down, 28f, z1 - z0, night);
                // The far end (2026-10-13 review: it was open, a bare hole onto the void): night round a doorway into the
                // attic, framed.
                DreamEscapeBuilder.Quad("Night (far, left)", obs, new Vector3(-9.35f, y + 3.5f, z1), Vector3.back, 9.3f, 25f, night);
                DreamEscapeBuilder.Quad("Night (far, right)", obs, new Vector3(9.35f, y + 3.5f, z1), Vector3.back, 9.3f, 25f, night);
                DreamEscapeBuilder.Quad("Night (far, over the door)", obs, new Vector3(0f, y + 11f, z1), Vector3.back, 9.4f, 10f, night);
                DreamEscapeBuilder.Quad("Night (far, under the door)", obs, new Vector3(0f, y - 6.1f, z1), Vector3.back, 9.4f, 5.8f, night);
                DreamEscapeBuilder.Box("Attic door frame (top)", obs, new Vector3(0f, y + 6f, z1 - 0.05f), new Vector3(9.6f, 0.2f, 0.1f), white);
                foreach (float x in new[] { -4.75f, 4.75f })
                    DreamEscapeBuilder.Box("Attic door frame (side)", obs, new Vector3(x, y + 1.4f, z1 - 0.05f), new Vector3(0.2f, 9.2f, 0.1f), white);
                for (int i = 0; i < 140; i++)
                {
                    float sx = (float)(rnd.NextDouble() * 26 - 13), sy = y - 8f + (float)rnd.NextDouble() * 23f, sz = z0 + (float)rnd.NextDouble() * (z1 - z0);
                    if (Mathf.Abs(sx) < 5.5f && sy > y - 3f && sy < y + 7f) continue;   // keep the run clear
                    DreamEscapeBuilder.Part("Star", PrimitiveType.Sphere, obs, Vector3.zero, Vector3.one * (0.06f + 0.12f * (float)rnd.NextDouble()), star).transform.position = new Vector3(sx, sy, sz);
                }
                var plat = P("The telescope's platform");
                var brass = DreamProps.MeshLook("DecorBrass", new Color(0.95f, 0.78f, 0.38f), 0.004f);
                var tube = DreamProps.MeshLook("DecorTelescope", new Color(0.3f, 0.32f, 0.55f), 0.006f);
                var feet = new Vector3(-3.2f, plat.Max.Y, plat.center.Z);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f * Mathf.Deg2Rad;
                    var leg = new Vector3(Mathf.Cos(a) * 0.4f, 0f, Mathf.Sin(a) * 0.4f);
                    var l = MeshPart("Tripod leg", PrimitiveType.Cylinder, obs, feet + leg * 0.5f + Vector3.up * 0.7f, new Vector3(0.05f, 0.72f, 0.05f), brass);
                    l.transform.rotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up * 1.4f - leg).normalized);
                }
                MeshPart("Telescope", PrimitiveType.Cylinder, obs, feet + new Vector3(0f, 1.6f, 0.2f), new Vector3(0.2f, 0.7f, 0.2f), tube).transform.rotation = Quaternion.Euler(55f, 0f, 0f);
                var ring = MeshPart("The round window (its frame)", PrimitiveType.Cylinder, obs, new Vector3(wall.Min.X - 0.04f, y + 2.8f, wall.center.Z), new Vector3(5f, 0.02f, 5f), frame);
                ring.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var pane = MeshPart("The round window (night)", PrimitiveType.Cylinder, obs, new Vector3(wall.Min.X - 0.06f, y + 2.8f, wall.center.Z), new Vector3(4.5f, 0.02f, 4.5f), night);
                pane.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // The attic stairs the wrong way: stored boxes tumbling beside the slide.
            if (groups.TryGetValue("The attic stairs, the wrong way", out var atticStairs))
            {
                var ramp = P("The attic stairs (a slide)");
                var box = DreamEscapeBuilder.Pastel("AtticStorage", new Color(0.74f, 0.6f, 0.5f), 0.04f);
                for (int i = 0; i < 6; i++)
                {
                    float z = ramp.Min.Z + 1f + i * 2.6f;
                    var b = DreamEscapeBuilder.Box("Box (tumbling)", atticStairs, new Vector3(i % 2 == 0 ? -3.9f : 3.9f, RampY(ramp, z) + 0.4f, z), Vector3.one * 0.7f, box);
                    b.transform.rotation = Quaternion.Euler(15f * i, 30f * i, 10f * i);
                }
            }

            // The kitchen, come round again (turned): its counters hanging from the ceiling, its plates on the table again.
            if (groups.TryGetValue("The kitchen, come round again", out var kitchen2))
            {
                var floor = P("The kitchen again (turned)");
                var counter = DreamEscapeBuilder.Pastel("HouseCounter", DreamEscapeBuilder.Palette[4], 0.05f);
                foreach (float x in new[] { -3.9f, 3.9f })
                    DreamEscapeBuilder.Box("Counter (hanging from the ceiling)", kitchen2, new Vector3(x, floor.Max.Y + (EscapeCeilings ? EscapeRoomHeight - 0.45f : 4.1f), floor.center.Z + 4f), new Vector3(1.2f, 0.9f, 10f), counter);
                var table = P("The table again (the long way)");
                for (int i = 0; i < 4; i++)
                {
                    float z = table.Min.Z + 2.5f + i * 3.4f;
                    MeshPart("Plate (again)", PrimitiveType.Cylinder, kitchen2, new Vector3(i % 2 == 0 ? 0.6f : -0.6f, RampY(table, z) + 0.02f, z), new Vector3(0.36f, 0.012f, 0.36f), plateMat);
                }
                var cal = DreamEscapeBuilder.Quad("The calendar (upside down)", kitchen2, new Vector3(-4.48f, floor.Max.Y + 2f, floor.Min.Z + 6f), Vector3.right, 0.5f, 0.66f, Picture(DreamArt.PicCalendarN2, white));
                cal.transform.rotation = cal.transform.rotation * Quaternion.Euler(0f, 0f, 180f);
            }

            // The hallway, a third time: the photo wall on both its walls.
            if (groups.TryGetValue("The hallway, a third time", out var third))
            {
                foreach (var (name, x, n) in new[] { ("The third hallway: the left wall", -4.48f, Vector3.right), ("The third hallway: the right wall", 4.48f, Vector3.left) })
                {
                    var w = P(name);
                    if (w == null) continue;
                    for (int i = 0; i < 5; i++)
                        Photo(third, new Vector3(x, w.Max.Y - 3.6f + (i % 2) * 0.9f, w.Min.Z + 1.8f + i * 2.6f), n, i + 2, 0f);
                }
            }

            // The door onto the stairwell: its frame, and a bare bulb hanging over the drop.
            if (groups.TryGetValue("The door onto the stairwell", out var door))
            {
                var over = P("The stairwell door (over it)");
                DreamEscapeBuilder.Box("Stairwell door frame", door, new Vector3(0f, over.Min.Y - 0.05f, over.center.Z - 0.22f), new Vector3(2.4f, 0.1f, 0.06f), white);
                foreach (float x in new[] { -1.15f, 1.15f })
                    DreamEscapeBuilder.Box("Stairwell door frame", door, new Vector3(x, over.Min.Y - 1.15f, over.center.Z - 0.22f), new Vector3(0.1f, 2.3f, 0.06f), white);
                DreamEscapeBuilder.Part("Bulb (over the stairwell)", PrimitiveType.Sphere, door, Vector3.zero, Vector3.one * 0.3f, bulb).transform.position = new Vector3(0f, over.Max.Y + 0.6f, over.center.Z + 12f);
                TheosWindow(h, door, P("Before the stairwell door").Max.Y, white);
            }
        }
        /// <summary>Lena watching from the window as Nadia leaves (Night 2, step 7): a lit window beside the way out's door,
        /// on its outside face, with Lena in it.</summary>
        static void LenaAtTheWindow(Transform parent, float z, float floor)
        {
            var t = new GameObject("Lena at the window").transform;
            t.SetParent(parent, false);
            var c = new Vector3(-4.2f, floor + 1.55f, z);
            DreamEscapeBuilder.Quad("Window (lit)", t, c, Vector3.forward, 1.3f, 1.6f, DreamEscapeBuilder.Glow("HouseWindowLit", new Color(1f, 0.86f, 0.6f), 0.7f, 0f, 0.1f));
            var mat = DreamEscapeBuilder.Mat("Cutout_" + DreamArt.Lena, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", DreamArt.Get(DreamArt.Lena, out _));
            var lena = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lena.name = "Lena (in the window)";
            Object.DestroyImmediate(lena.GetComponent<Collider>());
            lena.transform.SetParent(t, false);
            lena.transform.position = c + new Vector3(0.1f, -0.1f, 0.02f);
            lena.transform.rotation = Quaternion.LookRotation(Vector3.back);
            lena.transform.localScale = new Vector3(0.62f, 1.6f, 1f);
            lena.GetComponent<MeshRenderer>().sharedMaterial = mat;
            DreamHouseRooms.Window(t, c + Vector3.forward * 0.03f, Vector3.forward, 1.3f, 1.6f, pane: false);
        }

        /// <summary>The street (Night 2, step 1): the road and its kerbs, two neighbours' house fronts across it, a lamp post,
        /// the neighbour who says "Morning, Nadia!", Walt waving her up, and the loop seen (the same sprinkler, the same ball
        /// as Night 1).</summary>
        static void Street(DreamHouse h, Transform root)
        {
            var t = new GameObject("The street").transform;
            t.SetParent(root, false);
            float z0 = DreamHouse.StreetZ0, z1 = -24f;
            var kerb = DreamEscapeBuilder.Pastel("StreetKerb", new Color(0.86f, 0.84f, 0.88f), 0.03f);
            var line = DreamEscapeBuilder.Pastel("StreetLine", new Color(0.98f, 0.92f, 0.6f), 0f);
            DreamEscapeBuilder.Box("Pavement (near)", t, new Vector3(0f, 0.04f, z1 - 1.5f), new Vector3(28f, 0.08f, 3f), kerb);
            DreamEscapeBuilder.Box("Pavement (far)", t, new Vector3(0f, 0.04f, z0 + 1.5f), new Vector3(28f, 0.08f, 3f), kerb);
            for (float x = -12f; x < 13f; x += 3f)
                DreamEscapeBuilder.Box("Road line", t, new Vector3(x, 0.01f, (z0 + z1) * 0.5f), new Vector3(1.4f, 0.02f, 0.18f), line);
            // Across the street: two neighbours' house fronts (flats, the street's look), a lamp post.
            var gable = DreamEscapeBuilder.EnsureGableMesh();
            foreach (var (x, colour) in new[] { (-8f, new Color(0.96f, 0.78f, 0.6f)), (8f, new Color(0.7f, 0.82f, 0.95f)) })
            {
                var wall = DreamEscapeBuilder.Pastel("StreetHouse_" + Mathf.RoundToInt(x), colour, 0.04f);
                DreamEscapeBuilder.Box("Neighbour's house", t, new Vector3(x, 2.4f, z0 - 1.5f), new Vector3(9f, 4.8f, 3f), wall);
                DreamEscapeBuilder.GableOn(t, gable, new Vector3(x, 5.7f, z0 - 1.5f), new Vector3(9.6f, 1.8f, 3.2f), DreamEscapeBuilder.Pastel("HouseFrontRoof", new Color(0.93f, 0.52f, 0.5f), 0.05f, 0f, 1f));
                foreach (float wx in new[] { -2.4f, 2.4f })
                    DreamHouseRooms.Window(t, new Vector3(x + wx, 2.6f, z0 + 0.01f), Vector3.forward, 1.1f, 1.3f);
            }
            var post = DreamEscapeBuilder.Pastel("StreetLamp", new Color(0.36f, 0.32f, 0.46f), 0.02f);
            DreamEscapeBuilder.Box("Lamp post", t, new Vector3(4f, 2f, z1 - 2.6f), new Vector3(0.14f, 4f, 0.14f), post);
            DreamEscapeBuilder.Part("Lamp", PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.4f, DreamEscapeBuilder.Glow("StreetLampGlow", new Color(1f, 0.9f, 0.65f), 0.8f, 0f, 0.1f)).transform.position = new Vector3(4f, 4.1f, z1 - 2.6f);
            // The loop, seen: the sprinkler on the neighbour's lawn, the ball (Night 1's, to the second).
            DreamEscapeBuilder.Part("Sprinkler", PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.2f, 0.1f, 0.2f), post).transform.position = new Vector3(-10.5f, 0.15f, z0 + 4.2f);
            DreamEscapeBuilder.Part("Sprinkler spray", PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1.6f, 0.6f, 1.6f), DreamEscapeBuilder.Glow("StreetSpray", new Color(0.7f, 0.85f, 1f), 0.4f, 0f, 0.5f)).transform.position = new Vector3(-10.5f, 0.5f, z0 + 4.2f);
            DreamEscapeBuilder.Part("Ball", PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.32f, DreamEscapeBuilder.Pastel("StreetBall", new Color(0.95f, 0.45f, 0.4f), 0.02f)).transform.position = new Vector3(2.2f, 0.18f, z0 + 9f);
            // The neighbour who knows her name now, and Walt at the garden's edge waving her up.
            CutOut("A neighbour (Morning, Nadia!)", "dream_neighbour_roses", t, new Vector3(-7f, 0.08f, z0 + 2.2f), 1.75f);
            CutOut("Walt (waving her up)", "dream_walt", t, new Vector3(3.2f, 0f, z1 + 1.2f), 1.85f);
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
            foreach (float x in new[] { -1.54f, 1.54f })   // over the wall's jambs (coplanar faces flickered: the "holes" by the door)
                DreamEscapeBuilder.Box("Door frame (side)", t, new Vector3(x, floor + 1.32f, z), new Vector3(0.24f, 2.64f, 0.66f), trim);
            DreamEscapeBuilder.Box("Door frame (top)", t, new Vector3(0f, floor + 2.66f, z), new Vector3(3.32f, 0.2f, 0.66f), trim);
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
