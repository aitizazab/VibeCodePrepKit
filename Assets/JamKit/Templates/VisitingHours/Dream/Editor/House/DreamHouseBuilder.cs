using System.Collections.Generic;
using JamKit.EditorTools;
using JamKit.Look.EditorTools;
using JamKit.Perf.EditorTools;
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
            try
            {
                BuildEverything(null);
                // Built and saved, but the mesh lint found things wrong: fail loudly (the captures still run on it).
                if (LastMeshLintIssues > 0) { Debug.LogError("JAMKIT_HOUSE_MESHLINT the rebuild exits 3: " + LastMeshLintIssues + " issues"); code = 3; }
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// <summary>The last rebuild's mesh lint (<see cref="HouseMeshLintRun"/>): issues found.</summary>
        public static int LastMeshLintIssues;

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { DreamEscapeBuilder.Root + "/Scenes", DreamEscapeBuilder.MaterialsDir, DreamEscapeBuilder.Root + "/Data", DreamEscapeBuilder.Root + "/Generated", HouseMaterials.MatDir })
                DreamEscapeBuilder.EnsureFolder(d);
            DreamEscapeBuilder.EnsureTuning();
            MakePreset(ctx);
            DreamEscapeBuilder.ClearMaterials();
            HouseMaterials.Clear();
            HouseMaterials.ClearFinishes();
            HouseEscapeLook.Clear();
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
            // The evening outside (house-1.md: a flat peach void round the lot): Look's golden-hour rig (the low sun, the
            // HDRI sky, haze, the golden grade) over everything, and inside the house the interior grade (no haze, the
            // warmer exposure) as a local volume round the house's body.
            VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 10f, hazeBaseHeight = 0f });
            InteriorVolume();

            // The house, in its own frame (standing alone at the origin; on the hill World places it at HillAnchors).
            var houseRoot = new GameObject("The Lasname house (Night 2)").transform;
            HouseMeshLintRun.Begin();
            HouseGeometry.Build(h, houseRoot, true);
            HouseFurnishing.Build(h, houseRoot, true);
            HouseFurnishing.Lamps(h, houseRoot);
            var memories = HouseFurnishing.Memories(h, houseRoot);
            DreamHouseExterior.Details(h, houseRoot);
            var theosDoor = DreamHouseExterior.TheosDoor(houseRoot);
            HouseMeshLintRun.Pause();
            var standalone = new GameObject("Stand-ins (the house alone: Chapel Row)").transform;
            standalone.SetParent(houseRoot, false);
            DreamHouseExterior.Standalone(h, standalone);

            // The cast: Lena (at the stove), Lena in the tower's round window (seen when she's out), Theo seeking.
            var lena = HouseFurnishing.CutOut("Lena", DreamArt.Lena, houseRoot, U(h.lenaAt), LenaCardHeight);
            var lenaWindow = HouseFurnishing.CutOut("Lena (at the tower's window)", DreamArt.Lena, houseRoot, U(h.lenaWindow), LenaCardHeight);
            // She stands on her dais in the round window, facing the hill, lit by her reading lamp so she reads from Chapel
            // Row (she was missing from the shot: house-1.md).
            lenaWindow.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            var lw = lenaWindow.GetComponent<DreamCutoutView>();
            if (lw != null) lw.restYaw = 0f;
            var lenaLight = new GameObject("Light on Lena (the window)").AddComponent<Light>();
            lenaLight.transform.SetParent(lenaWindow.transform, false);
            lenaLight.transform.localPosition = new Vector3(0.3f, 1.9f, -0.6f);
            lenaLight.type = LightType.Point; lenaLight.range = 3f; lenaLight.intensity = 1.8f; lenaLight.color = new Color(1f, 0.8f, 0.55f);
            lenaLight.shadows = LightShadows.None;
            lenaWindow.SetActive(false);
            var theo = HouseFurnishing.Theo(h, houseRoot);
            var freeThings = new List<GameObject> { lena };
            if (theo != null && theo.figure != null) freeThings.Add(theo.figure.gameObject);
            var hung = new List<GameObject>();
            foreach (var m in memories) if (m != null) hung.Add(ThingOf(m));
            LastMeshLintIssues = HouseMeshLintRun.Finish(h, houseRoot, freeThings, hung);

            // The escape, upright, far below.
            var escapeRoot = new GameObject("The house throws her out (the escape)").transform;
            escapeRoot.position = houseRoot.TransformPoint(U(HouseEscape.Origin));
            var esc = HouseEscapeBuilder.Build(e, escapeRoot);

            // The player.
            var body = DreamEscapeBuilder.BuildPlayer(cam, new LabCheckpoint { name = "The street", position = h.arrival, yaw = h.arrivalYaw });
            // The sky, not a solid colour, behind everything; the far clip out to Look's backdrop ring.
            VhLook.SetupCamera(cam);
            cam.clearFlags = CameraClearFlags.Skybox;
            body.onlyMarkedWalls = true;   // every wall marked runnable runs (in the house: every wall longer than 1.5 m)
            body.gameObject.AddComponent<StealthPlayer>().body = body;
            var rig = cam.transform.parent;
            var fx = cam.gameObject.AddComponent<HouseCameraFx>();
            fx.cam = cam;
            fx.rig = rig;
            var sky = HouseEscapeBuilder.NightSky(cam);

            // Perf (docs/redesign/perf.md, House): shadows from only the two nearest room lights (HouseLightBudget), no
            // shadows from small things, small things culled with distance, the house's body static (batching, occludee,
            // the big solids occluders) for the occlusion bake. Not the memories, the cast or anything that moves, and
            // nothing of the escape (its rooms roll: HouseEscapeBuilder sets its own flags).
            var budget = new GameObject("Light budget (two shadowed lights)").AddComponent<HouseLightBudget>();
            budget.cam = cam;
            budget.roots = new[] { houseRoot, escapeRoot };   // the stand-in hill is under houseRoot
            foreach (var name in new[] { "Structure", "Furniture", "Lamps", "Outside details" })
            {
                var t = houseRoot.Find(name);
                if (t == null) continue;
                VhPerfShadows.Apply(t, VhPerfShadows.Rules.Indoor);
                if (name != "Lamps") VhPerfBatching.MarkStatic(t);
            }
            int culled = VhPerfLod.CullSmall(houseRoot.Find("Furniture"));
            Debug.Log("JAMKIT_HOUSE perf: " + culled + " small things culled with distance; static body marked; light budget on " + budget.roots.Length + " roots");

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

        /// <summary>The house's interior grade (Look's VH_HouseInterior: no haze, a warmer exposure) as a local volume
        /// round the house's body, over the golden-hour global one; its collider is a trigger on Ignore Raycast, so nothing
        /// walks or sees into it.</summary>
        static void InteriorVolume()
        {
            var go = new GameObject("Post (inside the house)");
            go.layer = 2;
            go.transform.position = new Vector3((DreamHouse.OutX0 + DreamHouse.OutX1) * 0.5f, (DreamHouse.Basement + DreamHouse.Attic + DreamHouse.TowerH) * 0.5f, (DreamHouse.Z0 + DreamHouse.Z1) * 0.5f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(DreamHouse.OutX1 - DreamHouse.OutX0 - 0.2f, DreamHouse.Attic + DreamHouse.TowerH - DreamHouse.Basement, DreamHouse.Z1 - DreamHouse.Z0 - 0.2f);
            var v = go.AddComponent<UnityEngine.Rendering.Volume>();
            v.isGlobal = false;
            v.priority = 20f;
            v.blendDistance = 0.8f;
            v.sharedProfile = VhLookProfiles.BuildHouseInterior();
        }

        /// <summary>A memory's thing without its glow (the lint measures the thing).</summary>
        static GameObject ThingOf(HouseMemoryView m)
        {
            var t = m.transform.Find("Thing");
            return t != null ? t.gameObject : m.gameObject;
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
