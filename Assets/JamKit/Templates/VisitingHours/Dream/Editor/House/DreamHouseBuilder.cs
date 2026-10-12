using System.Collections.Generic;
using JamKit.EditorTools;
using JamKit.Look.EditorTools;
using JamKit.Movement;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Builds Night 2's scene (docs/redesign/house.md) from core <see cref="DreamHouse"/> and <see cref="HouseEscape"/>: the
    /// Look agent's house-interior lighting (VhLook.ApplyHouseInterior), the house (<see cref="HouseGeometry"/>,
    /// <see cref="HouseFurnishing"/>, <see cref="DreamHouseExterior"/>), its lamps and memories, Lena and Theo (the cast's
    /// cut-outs), the stand-ins round it when it stands alone (the lot, Rowan Street, Chapel Row), the escape at its origin
    /// (<see cref="HouseEscapeBuilder"/>), the umbrella and the rain on Chapel Row, the player (the dream's movement and
    /// body, with a <see cref="StealthPlayer"/> for her noise) and the <see cref="DreamHouseGame"/> wired to all of it. Logs
    /// the geometry lint (<see cref="HouseLint"/>). A step of "JamKit/Rebuild Shell Scenes"; "JamKit/Dream House/Rebuild
    /// Scene" builds only this.
    /// </summary>
    public static class DreamHouseBuilder
    {
        public const string SceneName = DreamHousePresenter.Scene;
        public const string ScenePath = DreamEscapeBuilder.Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = DreamEscapeBuilder.Root + "/DreamHouseGameConfig.asset";
        public const float NadiaEye = 1.5f;
        public const float LenaCardHeight = HouseFurnishing.LenaCardHeight, TheoCardHeight = HouseFurnishing.TheoCardHeight;

        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

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

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.DreamHouseBuilder.RebuildFromCommandLine</summary>
        public static void RebuildFromCommandLine()
        {
            int code = 0;
            try { BuildEverything(null); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { DreamEscapeBuilder.Root + "/Scenes", DreamEscapeBuilder.MaterialsDir, DreamEscapeBuilder.Root + "/Data", DreamEscapeBuilder.Root + "/Generated", HouseMaterials.MatDir })
                DreamEscapeBuilder.EnsureFolder(d);
            DreamEscapeBuilder.EnsureTuning();
            MakePreset(ctx);
            DreamEscapeBuilder.ClearMaterials();
            HouseMaterials.Clear();
            HouseMaterials.ClearFinishes();
            AssetDatabase.SaveAssets();
            string path = BuildScene();
            AssetDatabase.SaveAssets();
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
                    body = "Theo counts at the table, then looks for you through the whole house: upstairs, the basement, the attic, the garage, the yard. The eye at the top right fills while he can see you: when it's full, he's found you. Crouch, keep to the shade and the dark, hide in cupboards and behind curtains, coats and sheets. Every time he finds you the house takes back the last memory you found and hides it somewhere else; the third time, the game's over.\nFind the memories (look at one and press the interact key): the count is at the top.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        static string BuildScene()
        {
            var h = DreamHouse.Build();
            var e = HouseEscape.Build();
            var lint = HouseLint.Check(h.parts);
            foreach (var hit in lint) Debug.LogWarning("JAMKIT_HOUSE_LINT " + HouseLint.Describe(h.parts, hit));
            Debug.Log("JAMKIT_HOUSE_LINT " + lint.Count + " overlaps in " + h.parts.Count + " parts");

            var scene = SceneBuilder.NewScene(new Color(0.8f, 0.72f, 0.66f), out var cam);
            VhLook.ApplyHouseInterior(scene);

            // The house, in its own frame (standing alone at the origin; on the hill World places it at HillAnchors).
            var houseRoot = new GameObject("The Lasname house (Night 2)").transform;
            HouseGeometry.Build(h, houseRoot, true);
            HouseFurnishing.Build(h, houseRoot, true);
            HouseFurnishing.Lamps(h, houseRoot);
            var memories = HouseFurnishing.Memories(h, houseRoot);
            DreamHouseExterior.Details(h, houseRoot);
            var theosDoor = DreamHouseExterior.TheosDoor(houseRoot);
            var standalone = new GameObject("Stand-ins (the house alone: Chapel Row)").transform;
            standalone.SetParent(houseRoot, false);
            DreamHouseExterior.Standalone(h, standalone);

            // The cast: Lena (at the stove), Lena in the tower's round window (seen when she's out), Theo seeking.
            var lena = HouseFurnishing.CutOut("Lena", DreamArt.Lena, houseRoot, U(h.lenaAt), LenaCardHeight);
            var lenaWindow = HouseFurnishing.CutOut("Lena (at the tower's window)", DreamArt.Lena, houseRoot, U(h.lenaWindow), LenaCardHeight);
            lenaWindow.SetActive(false);
            var theo = HouseFurnishing.Theo(h, houseRoot);

            // The escape, upright, far below.
            var escapeRoot = new GameObject("The house throws her out (the escape)").transform;
            escapeRoot.position = houseRoot.TransformPoint(U(HouseEscape.Origin));
            var esc = HouseEscapeBuilder.Build(e, escapeRoot);

            // The player.
            var body = DreamEscapeBuilder.BuildPlayer(cam, new LabCheckpoint { name = "The street", position = h.arrival, yaw = h.arrivalYaw });
            body.onlyMarkedWalls = true;   // every wall marked runnable runs (in the house: every wall longer than 1.5 m)
            body.gameObject.AddComponent<StealthPlayer>().body = body;
            var rig = cam.transform.parent;
            var fx = cam.gameObject.AddComponent<HouseCameraFx>();
            fx.cam = cam;
            fx.rig = rig;
            var sky = HouseEscapeBuilder.NightSky(cam);

            // The game.
            var game = new GameObject("Dream House").AddComponent<DreamHouseGame>();
            game.player = body;
            game.houseRoot = houseRoot;
            game.escapeRoot = escapeRoot;
            game.theo = theo;
            game.lena = lena.transform;
            game.lenaInWindow = lenaWindow;
            game.memoryViews = memories;
            game.cameraFx = fx;
            game.fold = esc.fold;
            game.corridorDoor = esc.corridorDoor;
            game.corridorDoorLeaf = esc.corridorDoorLeaf;
            game.theoAtTheDoor = esc.theoAtTheDoor;
            game.escapeRoomGroups = esc.rooms;
            game.nightSky = sky;
            game.theosDoorShut = theosDoor;
            var exit = new GameObject("Where she comes out (Chapel Row)").transform;
            exit.SetParent(houseRoot, false);
            exit.localPosition = U(h.exitDoor);
            exit.localRotation = Quaternion.Euler(0f, h.exitYaw, 0f);
            game.exitDoor = exit;
            var chair = houseRoot.Find("Colliders/Collider: Chair (Nadia's: the fourth chair)");
            if (chair != null) game.nadiaChair = chair.GetComponent<Collider>();

            // The umbrella by the last hall's front door; the rain on Chapel Row.
            var spot = e.rain.Copy();
            spot.umbrella = new NVec3(escapeRoot.position.x + spot.umbrella.X, escapeRoot.position.y + spot.umbrella.Y, escapeRoot.position.z + spot.umbrella.Z);
            spot.floorY = exit.position.y;
            game.umbrella = DreamEscapeBuilder.BuildUmbrella(spot, escapeRoot, game, body);
            PlaceRain(game.umbrella, exit, spot);

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        /// <summary>The escape's shared umbrella builder lays its rain along the route; here the rain falls on Chapel Row,
        /// on the path from the dream's door up the hill (its puddles and lamp posts go: the stand-in has its own).</summary>
        static void PlaceRain(DreamUmbrella u, Transform exit, DreamRainSpot spot)
        {
            if (u == null) return;
            var parent = u.rainFx != null ? u.rainFx.transform.parent : null;
            if (parent != null)
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    var c = parent.GetChild(i);
                    if (c.name == "Puddle" || c.name == "Lamp post") Object.DestroyImmediate(c.gameObject);
                }
            if (u.rainFx != null)
            {
                float len = spot.rainEndS - spot.rainStartS + 12f;
                u.rainFx.transform.SetParent(exit, false);
                u.rainFx.transform.localPosition = new Vector3(0f, 12f, len * 0.5f - 4f);
                u.rainFx.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var shape = u.rainFx.shape;
                shape.scale = new Vector3(14f, len, 0.1f);
            }
        }
    }
}
