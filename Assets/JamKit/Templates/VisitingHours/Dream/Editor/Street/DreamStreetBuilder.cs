using System.Collections.Generic;
using JamKit.DeepBlock;
using JamKit.EditorTools;
using JamKit.Movement;
using JamKit.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Builds Night 1's street on a hill (docs/LARCH-HILL.md; core <see cref="LarchHill"/>): every box of the hill in the
    /// dream's pastel look (the solid ones with colliders, ramps as tilted slabs), the gold marks on what you can use, Walt's
    /// bike shop at the bottom by the creek, the terraced houses, the stepped row, the garden stairs, the ravine, the
    /// Lasname house at the top (its outside is <see cref="DreamHouseExterior"/>, the house builder's; this adds its front,
    /// the room behind the window and the people in it, the door and the porch light) with the beacon standing up out of its
    /// tower, the cast (Walt, the neighbour, the dog, Elias at the barbecue), the neighbours' loop
    /// (the sprinkler, the ball), the memories, Nadia, and the game. A step of "JamKit/Rebuild Shell Scenes";
    /// "JamKit/Dream Street/Rebuild Scene" rebuilds only it.
    /// </summary>
    public static class DreamStreetBuilder
    {
        public const string SceneName = "DreamStreet";
        public const string ScenePath = DreamEscapeBuilder.Root + "/Scenes/" + SceneName + ".unity";
        public const string PresetPath = DreamEscapeBuilder.Root + "/DreamStreetGameConfig.asset";
        /// <summary>Golden hour on the hill.</summary>
        public static readonly Color Sky = new Color(1f, 0.82f, 0.66f);

        static readonly Color Lawn = new Color(0.66f, 0.88f, 0.6f);
        static readonly Color Road = new Color(0.72f, 0.7f, 0.82f);
        static readonly Color Pavement = new Color(0.96f, 0.91f, 0.84f);
        static readonly Color Stone = new Color(0.78f, 0.74f, 0.86f);
        static readonly Color Brick = new Color(0.88f, 0.6f, 0.56f);
        static readonly Color Wood = new Color(0.82f, 0.64f, 0.5f);
        static readonly Color Plum = new Color(0.36f, 0.3f, 0.46f);
        static readonly Color WindowWarm = new Color(1f, 0.88f, 0.6f);
        static readonly Color WindowDark = new Color(0.46f, 0.42f, 0.66f);
        static readonly Color[] RoofColors =
        {
            new Color(0.93f, 0.55f, 0.52f), new Color(0.6f, 0.52f, 0.78f), new Color(0.42f, 0.7f, 0.74f), new Color(0.95f, 0.66f, 0.42f),
        };

        // The cast's eye heights, as the house's (Nadia's eye is 1.5 m, Lena's 1.60): sized by the eyes, not feet to hair.
        const float WaltEye = 1.62f, WaltEyeFromTop = 0.2f;
        /// <summary>Elias: a little taller than Lena (eyes at 1.66 m); his drawing's eyes are 24.4% of the card down from its top.</summary>
        public const float EliasEye = 1.66f, EliasEyeFromTop = 0.244f, EliasCardHeight = EliasEye / (1f - EliasEyeFromTop);

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

        public static string BuildEverything(SceneBuilder.Context ctx)
        {
            foreach (var d in new[] { DreamEscapeBuilder.Root + "/Scenes", DreamEscapeBuilder.MaterialsDir, DreamEscapeBuilder.Root + "/Data", DreamEscapeBuilder.Root + "/Generated" })
                DreamEscapeBuilder.EnsureFolder(d);
            DreamEscapeBuilder.EnsureTuning();
            MakePreset(ctx);
            DreamEscapeBuilder.ClearMaterials();
            AssetDatabase.SaveAssets();
            string path = BuildScene();
            Debug.Log("JAMKIT_STREET_OK " + path);
            return path;
        }

        static void MakePreset(SceneBuilder.Context ctx)
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(PresetPath) != null) return;
            var p = ScriptableObject.CreateInstance<GameConfig>();
            p.gameTitle = "Visiting Hours: the street";
            p.tagline = "Night 1. Larch Street, on a hill.";
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
                    title = "The street",
                    body = "Find your brother. The house with the tower at the top of the hill: four ways up, once Walt lets you by. Walk up to things to remember them.",
                },
            };
            AssetDatabase.CreateAsset(p, PresetPath);
            AssetDatabase.SaveAssets();
        }

        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        // ---------- The scene ----------

        static string BuildScene()
        {
            var h = LarchHill.Build();
            var scene = SceneBuilder.NewScene(Sky, out var cam);

            // Golden hour: the sun low in the south-west, lighting the hill's faces; warm ambient; a long fog so the whole
            // climb reads from the bottom and the whole street from the top.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.9f, 0.8f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(1f, 0.84f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.56f, 0.48f, 0.62f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 560f;
            RenderSettings.skybox = null;
            var sun = new GameObject("Sun (golden hour)").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.78f, 0.56f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(16f, 35f, 0f);

            var first = new LabCheckpoint { name = "The arrival", position = h.arrival, yaw = h.arrivalYaw };
            var body = DreamEscapeBuilder.BuildPlayer(cam, first);
            body.transform.rotation = Quaternion.Euler(0f, h.arrivalYaw, 0f);
            cam.farClipPlane = 700f;

            var root = new GameObject("Larch Street (the hill)").transform;
            var gold = new GameObject("Gold (usable)").transform;
            gold.SetParent(root, false);
            var gableMesh = DreamEscapeBuilder.EnsureGableMesh();
            GameObject waltGate = null;
            int index = 0;
            foreach (var p in h.pieces)
            {
                var go = Piece(p, root, index++);
                if (go == null) continue;
                if (p.switchIndex == LarchHill.WaltGateSwitch) waltGate = go;
                if (p.usable) Gold(p, gold);
                if (p.kind == BlockKind.House && (p.layer == "terraces" || p.layer == "farbank")) Dress(p, root, index, p.layer == "farbank");
                if (p.kind == BlockKind.Roof && (p.layer == "terraces" || p.layer == "farbank"))
                {
                    Object.DestroyImmediate(go.GetComponent<MeshRenderer>());   // the gable is drawn; the box keeps the collider
                    GableRoof(p, root, gableMesh, index);
                }
                if (p.kind == BlockKind.House && p.layer == "roofs") DressRow(p, root, index);
            }
            Steps(h, root);
            Shop(root);
            var walt = CutOut("Walt (in the gate)", DreamArt.Walt, root, U(h.Spot(LarchHill.SpotWalt).position), WaltEye / (1f - WaltEyeFromTop));
            var gateLeaf = GateLeaf(root);

            // The neighbours' loop on terrace 1: the neighbour by her roses, the sprinkler, the dog and his ball.
            var neighbour = CutOut("Mrs. Lindqvist (her roses)", DreamArt.NeighbourRoses, root, U(h.Spot(LarchHill.SpotNeighbour).position), 1.7f);
            var dog = CutOut("The dog", DreamArt.Dog, root, U(h.Spot(LarchHill.SpotDog).position), 0.75f);
            var sprinkler = Sprinkler(root, U(h.Spot(LarchHill.SpotSprinkler).position));
            // Terrace 1's neighbour at his grill (he chats as she passes).
            var chatter = U(h.Spot(LarchHill.SpotChatter).position);
            CutOut("The neighbour at his grill (terrace 1)", DreamArt.NeighbourGrill, root, chatter, 1.75f);
            Grill("His grill", root, chatter + new Vector3(1.1f, 0f, 0.4f));
            var ball = DreamEscapeBuilder.Part("The dog's ball", PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.22f,
                DreamEscapeBuilder.Pastel("StreetBall", new Color(0.98f, 0.62f, 0.58f), 0.08f));
            ball.transform.position = U(h.Spot(LarchHill.SpotBallFrom).position);

            // The memories you walk up to: Saturday's paper on Walt's yard bench, his ledger on his workbench.
            var paper = Memory("Memory: Saturday's paper", root, U(h.Spot(LarchHill.SpotPaper).position), new Vector3(0.42f, 0.06f, 0.3f));
            var ledger = Memory("Memory: Walt's ledger", root, U(h.Spot(LarchHill.SpotLedger).position), new Vector3(0.36f, 0.05f, 0.26f));

            // The top: the house (its outside is the house builder's), the room in the window, the door, the porch light,
            // the beacon, Elias at the barbecue.
            var house = House(h, root, out var parts);
            var beacon = Beacon(h, root);
            var barbecue = U(h.Spot(LarchHill.SpotBarbecue).position);
            Grill("The barbecue", root, barbecue + new Vector3(0.9f, 0f, 0.3f));
            var eliasOut = CutOut("Elias (at the barbecue)", DreamArt.Elias, root, barbecue, EliasCardHeight);

            var game = new GameObject("Dream Street").AddComponent<DreamStreetGame>();
            game.player = body;
            game.waltGate = waltGate;
            game.gateLeaf = gateLeaf;
            game.walt = walt.transform;
            game.neighbour = neighbour.transform;
            game.dog = dog.transform;
            game.sprinklerJet = sprinkler;
            game.ball = ball.transform;
            game.paper = paper;
            game.ledger = ledger;
            game.eliasOutside = eliasOut;
            game.eliasInside = parts.elias;
            game.theoInside = parts.theo;
            game.lenaInside = parts.lena;
            game.frontDoor = parts.door;
            game.porchLight = parts.light;
            game.porchGlow = parts.glow;
            game.beacon = beacon;
            game.houseRoot = house;

            EditorSceneManager.SaveScene(scene, ScenePath);
            return ScenePath;
        }

        // ---------- Pieces ----------

        static Material MaterialOf(BlockPiece p, int index)
        {
            var P = DreamEscapeBuilder.Palette;
            string n = p.name;
            switch (p.kind)
            {
                case BlockKind.Ground:
                    if (n.Contains("hairpin") || n.Contains("top of the road")) return DreamEscapeBuilder.Pastel("StreetRoad", Road, 0.04f);
                    if (n.Contains("ravine") || n.Contains("creek bed") || n.Contains("Alley") || n.Contains("embankment")) return DreamEscapeBuilder.Pastel("StreetStone", Stone, 0.04f);
                    if (n.Contains("stairs") || n.Contains("Back lane") || n.Contains("path")) return DreamEscapeBuilder.Pastel("StreetPaving", Pavement, 0.04f);
                    return DreamEscapeBuilder.Pastel("StreetLawn", Lawn, 0.03f);
                case BlockKind.Lane: return DreamEscapeBuilder.Pastel("StreetRoad", Road, 0.04f);
                case BlockKind.House:
                    if (n.StartsWith("The Lasname") || n.StartsWith("The tower")) return null;   // the house builder's outside draws them
                    return DreamEscapeBuilder.Pastel("StreetHouse" + (index % 6), P[1 + (index * 7) % 11], 0.05f);
                case BlockKind.Roof: return DreamEscapeBuilder.Pastel("StreetRoof" + (index % 4), RoofColors[index % 4], 0.05f);
                case BlockKind.Shop: return DreamEscapeBuilder.Pastel("StreetShop", P[7], 0.05f);
                case BlockKind.BrickWall: return DreamEscapeBuilder.Pastel("StreetBrick", Brick, 0.04f);
                case BlockKind.Fence:
                case BlockKind.Picket: return DreamEscapeBuilder.Pastel("StreetPicket", P[0], 0.04f);
                case BlockKind.Hedge: return DreamProps.MeshLook("GardenHedge", new Color(0.46f, 0.68f, 0.52f), 0.01f);
                case BlockKind.Ledge: return DreamEscapeBuilder.Pastel(n.Contains("lookout") ? "StreetLookout" : "StreetCrate", n.Contains("lookout") ? Stone : Wood, 0.05f);
                case BlockKind.Deck: return DreamEscapeBuilder.Pastel("StreetDeck", Wood, 0.04f);
                case BlockKind.Door: return DreamEscapeBuilder.Pastel("StreetGarageDoor", P[2], 0.05f);
                case BlockKind.Water: return DreamEscapeBuilder.Glow("StreetWater", new Color(0.6f, 0.86f, 0.92f), 0.35f, 0f, 0.15f);
                default: return DreamEscapeBuilder.Pastel("StreetTimber", Wood, 0.04f);
            }
        }

        /// <summary>One box of the hill: a ramp as a tilted slab, the rest as boxes; solid ones with colliders (the movement's
        /// surfaces), the gate's collider without a renderer (its leaf is drawn), the boundaries never drawn.</summary>
        static GameObject Piece(BlockPiece p, Transform parent, int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = p.name;
            go.transform.SetParent(parent, false);
            if (p.rampAxis != 0)
            {
                const float thick = 4f;
                bool alongX = p.rampAxis == 1;
                float len = alongX ? p.size.X : p.size.Z, rise = p.rampHigh - p.rampLow;
                float angle = Mathf.Atan2(rise, len) * Mathf.Rad2Deg;
                var rot = alongX ? Quaternion.Euler(0f, 0f, angle) : Quaternion.Euler(-angle, 0f, 0f);
                var topMid = new Vector3(p.center.X, (p.rampLow + p.rampHigh) * 0.5f, p.center.Z);
                go.transform.SetPositionAndRotation(topMid - rot * Vector3.up * (thick * 0.5f), rot);
                float slant = Mathf.Sqrt(len * len + rise * rise);
                go.transform.localScale = alongX ? new Vector3(slant, thick, p.size.Z) : new Vector3(p.size.X, thick, slant);
                // The solid under the slab (the ramp's piece is solid down to its foot): a stepped fill, each step's top a
                // metre under the slope, so the slab's surface is what you walk on and nothing is hollow beneath the road.
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
                float axisMin = alongX ? p.Min.X : p.Min.Z;
                for (int i = 0; i < steps; i++)
                {
                    float a = axisMin + len * i / steps, b = axisMin + len * (i + 1) / steps;
                    float lowTop = p.rampLow + rise * (Mathf.Min(a, b) - axisMin) / len;
                    lowTop = Mathf.Min(lowTop, p.rampLow + rise * (Mathf.Max(a, b) - axisMin) / len) - 1f;
                    if (lowTop <= p.Min.Y + 0.2f) continue;
                    var under = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    under.name = p.name + " (under " + (i + 1) + ")";
                    under.transform.SetParent(parent, false);
                    float mid = (a + b) * 0.5f, span = b - a;
                    under.transform.position = alongX ? new Vector3(mid, (p.Min.Y + lowTop) * 0.5f, p.center.Z) : new Vector3(p.center.X, (p.Min.Y + lowTop) * 0.5f, mid);
                    under.transform.localScale = alongX ? new Vector3(span, lowTop - p.Min.Y, p.size.Z) : new Vector3(p.size.X, lowTop - p.Min.Y, span);
                    under.GetComponent<MeshRenderer>().sharedMaterial = DreamEscapeBuilder.Pastel("StreetStone", Stone, 0.04f);
                    under.AddComponent<MovementSurface>().runnable = false;
                    GameObjectUtility.SetStaticEditorFlags(under, 0);
                }
            }
            else
            {
                go.transform.position = U(p.center);
                go.transform.localScale = U(p.size);
            }
            var r = go.GetComponent<MeshRenderer>();
            var mat = MaterialOf(p, index);
            if (p.kind == BlockKind.Boundary || p.kind == BlockKind.Gate || mat == null) Object.DestroyImmediate(r);
            else
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = p.kind == BlockKind.Water || p.kind == BlockKind.Ground || p.kind == BlockKind.Lane ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            if (!p.solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            else go.AddComponent<MovementSurface>().runnable = p.runnable;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        /// <summary>The gold "usable" marks: a lip along a ledge's or roof's front (south) edge, a band under a slide-under
        /// door.</summary>
        static void Gold(BlockPiece p, Transform parent)
        {
            var goMat = DreamEscapeBuilder.Pastel("GoStripe", DreamEscapeBuilder.GoColor, 0.03f, 0.75f);
            if (p.kind == BlockKind.Door)
            {
                DreamEscapeBuilder.Box(p.name + " (gold)", parent, new Vector3(p.center.X, p.Min.Y + 0.04f, p.center.Z), new Vector3(p.size.X + 0.02f, 0.08f, p.size.Z + 0.04f), goMat);
                return;
            }
            DreamEscapeBuilder.Box(p.name + " (gold lip)", parent, new Vector3(p.center.X, p.Max.Y + 0.03f, p.Min.Z + 0.08f), new Vector3(p.size.X, 0.06f, 0.16f), goMat);
        }

        /// <summary>A terraced house's face: two windows up, two down, a door, on its south side (toward the view).</summary>
        static void Dress(BlockPiece p, Transform parent, int index, bool facesNorth = false)
        {
            var warm = DreamEscapeBuilder.Pastel("StreetWindowWarm", WindowWarm, 0.03f, 0.35f);
            var dark = DreamEscapeBuilder.Pastel("StreetWindowDark", WindowDark, 0.03f);
            var trim = DreamEscapeBuilder.Pastel("StreetTrim", DreamEscapeBuilder.Palette[0], 0.03f);
            float face = facesNorth ? p.Max.Z + 0.02f : p.Min.Z - 0.02f, y0 = p.Min.Y, outward = facesNorth ? 0.03f : -0.03f;
            var normal = facesNorth ? Vector3.forward : Vector3.back;
            float cx = p.center.X;
            foreach (float dx in new[] { -3.2f, 3.2f })
                foreach (float dy in new[] { 1.5f, 3.4f })
                    DreamEscapeBuilder.Quad("Window", parent, new Vector3(cx + dx, y0 + dy, face), normal, 1.4f, 1.1f, (index + (int)dy) % 3 == 0 ? warm : dark);
            DreamEscapeBuilder.Quad("Door", parent, new Vector3(cx, y0 + 1.1f, face), normal, 1.1f, 2.2f, DreamEscapeBuilder.Pastel("StreetDoor" + index % 3, RoofColors[(index + 1) % 4], 0.03f));
            DreamEscapeBuilder.Box("Door frame", parent, new Vector3(cx, y0 + 2.28f, face + outward), new Vector3(1.4f, 0.14f, 0.06f), trim);
        }

        /// <summary>A gable over a terraced house's roof box (decoration; the box keeps the collider).</summary>
        static void GableRoof(BlockPiece p, Transform parent, Mesh gable, int index)
        {
            var mat = DreamEscapeBuilder.Pastel("StreetRoof" + (index % 4) + "Gable", RoofColors[index % 4], 0.05f, 0f, 1f);
            DreamEscapeBuilder.GableOn(parent, gable, new Vector3(p.center.X, p.Min.Y + 1.1f, p.center.Z), new Vector3(p.size.X + 0.4f, 2.2f, p.size.Z + 0.4f), mat).name = p.name + " (gable)";
        }

        /// <summary>A row house's faces toward the middle of the street and down the hill: windows, a door at its foot.</summary>
        static void DressRow(BlockPiece p, Transform parent, int index)
        {
            var warm = DreamEscapeBuilder.Pastel("StreetWindowWarm", WindowWarm, 0.03f, 0.35f);
            var dark = DreamEscapeBuilder.Pastel("StreetWindowDark", WindowDark, 0.03f);
            float top = p.Max.Y, faceS = p.Min.Z - 0.02f;
            for (float y = top - 1.6f; y > top - 9f; y -= 2.6f)
                for (float x = p.Min.X + 2.5f; x < p.Max.X - 1.5f; x += 3.5f)
                    DreamEscapeBuilder.Quad("Window", parent, new Vector3(x, y, faceS), Vector3.back, 1.2f, 1.1f, ((int)(x + y) + index) % 3 == 0 ? warm : dark);
        }

        /// <summary>Treads drawn on the garden stairs' flights (decoration over the slope you walk).</summary>
        static void Steps(LarchHill h, Transform root)
        {
            var tread = DreamEscapeBuilder.Pastel("StreetTread", new Color(0.86f, 0.8f, 0.74f), 0.03f);
            foreach (var p in h.pieces)
            {
                if (p.rampAxis != 2 || !p.name.StartsWith("Garden stairs")) continue;
                float len = p.size.Z, rise = p.rampHigh - p.rampLow;
                int n = Mathf.Max(1, Mathf.RoundToInt(rise / 0.3f));
                for (int i = 0; i < n; i++)
                {
                    float z = p.Min.Z + (i + 0.5f) * len / n, y = p.rampLow + (i + 0.5f) * rise / n;
                    DreamEscapeBuilder.Box("Tread", root, new Vector3(p.center.X, y + 0.04f, z), new Vector3(p.size.X - 0.3f, 0.08f, 0.12f), tread);
                }
            }
        }

        /// <summary>Walt's shop's dressing: the sign over the garage door, a bike on its stand, the workbench's tools.</summary>
        static void Shop(Transform root)
        {
            var t = new GameObject("Walt's bike shop (dressing)").transform;
            t.SetParent(root, false);
            var sign = DreamEscapeBuilder.Pastel("StreetShopSign", new Color(0.98f, 0.85f, 0.55f), 0.05f, 0.2f);
            DreamEscapeBuilder.Box("The sign: Abernathy Cycles (no lettering yet)", t, new Vector3(-53f, 4.5f, 2.6f), new Vector3(7f, 0.9f, 0.15f), sign);
            var frame = DreamProps.BikeFrame();
            var bikeMat = DreamProps.MeshLook("StreetBike", new Color(0.42f, 0.7f, 0.74f), 0.01f);
            DreamProps.Show("A bike on the stand", frame, bikeMat, t, Vector3.zero, Quaternion.identity).transform.SetPositionAndRotation(new Vector3(-48f, 0.45f, 9f), Quaternion.Euler(0f, 90f, 0f));
            DreamProps.Show("A bike by the door", frame, bikeMat, t, Vector3.zero, Quaternion.identity).transform.SetPositionAndRotation(new Vector3(-58.5f, 0.45f, 1.5f), Quaternion.Euler(0f, 10f, 0f));
            var tool = DreamEscapeBuilder.Pastel("StreetTool", new Color(0.72f, 0.72f, 0.8f), 0.03f);
            DreamEscapeBuilder.Box("Pegboard", t, new Vector3(-61.6f, 2f, 9f), new Vector3(0.05f, 1.6f, 4f), DreamEscapeBuilder.Pastel("StreetPegboard", Wood, 0.02f));
            for (int i = 0; i < 6; i++)
                DreamEscapeBuilder.Box("Spanner", t, new Vector3(-61.55f, 1.6f + (i % 2) * 0.6f, 7.6f + i * 0.55f), new Vector3(0.04f, 0.4f, 0.06f), tool);
            var light = new GameObject("The shop's lamp").AddComponent<Light>();
            light.transform.SetParent(t, false);
            light.transform.position = new Vector3(-53f, 3.6f, 9f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.62f);
            light.range = 10f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }

        /// <summary>The garden gate Walt stands in (drawn 1.3 m; the piece above it is the collider that shuts the way up).</summary>
        static Transform GateLeaf(Transform root)
        {
            var hinge = new GameObject("Walt's gate (the leaf, on its hinge)").transform;
            hinge.SetParent(root, false);
            hinge.position = new Vector3(-54f, 0f, 24f);
            DreamEscapeBuilder.Box("Gate leaf", hinge, new Vector3(-52.5f, 0.65f, 24f), new Vector3(3f, 1.3f, 0.12f), DreamEscapeBuilder.Pastel("StreetPicket", DreamEscapeBuilder.Palette[0], 0.04f));
            return hinge;
        }

        static Transform Sprinkler(Transform root, Vector3 at)
        {
            var mat = DreamEscapeBuilder.Pastel("StreetSprinkler", new Color(0.62f, 0.62f, 0.74f), 0.03f);
            DreamEscapeBuilder.Part("Sprinkler", PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.18f, 0.12f, 0.18f), mat).transform.position = at + new Vector3(0f, 0.12f, 0f);
            var jet = new GameObject("The sprinkler's jet").transform;
            jet.SetParent(root, false);
            jet.position = at + new Vector3(0f, 0.25f, 0f);
            var water = DreamEscapeBuilder.Glow("StreetSprinklerWater", new Color(0.75f, 0.92f, 1f), 0.6f, 0f, 0.3f);
            for (int i = 0; i < 6; i++)
            {
                float d = 0.5f + i * 0.55f, y = 0.9f * Mathf.Sin(Mathf.PI * d / 3.4f);
                var drop = DreamEscapeBuilder.Part("Spray", PrimitiveType.Sphere, jet, new Vector3(0f, y, d), Vector3.one * (0.16f - i * 0.012f), water);
                drop.transform.localPosition = new Vector3(0f, y, d);
            }
            return jet;
        }

        /// <summary>A kettle grill in the dream's round mesh look, standing on the ground at <paramref name="foot"/> (inside the
        /// old 0.9 x 0.6 m box's footprint, 0.95 m to the lid's top): the bowl, the domed lid with its handle, three legs, a
        /// little shelf between them, the vent, and three soft puffs of smoke. Decoration only (no colliders).</summary>
        static void Grill(string name, Transform root, Vector3 foot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.position = foot;
            var body = DreamProps.MeshLook("StreetGrill", new Color(0.42f, 0.36f, 0.56f), 0.012f);
            var metal = DreamProps.MeshLook("StreetGrillMetal", new Color(0.78f, 0.76f, 0.86f), 0.01f);
            var smoke = DreamEscapeBuilder.Glow("StreetGrillSmoke", new Color(0.96f, 0.94f, 1f), 0.35f, 0f, 0.35f);
            void Part(string n, PrimitiveType type, Vector3 local, Vector3 scale, Material mat, Vector3 euler = default)
            {
                var go = DreamEscapeBuilder.Part(n, type, t, local, scale, mat);
                go.transform.localPosition = local;
                go.transform.localRotation = Quaternion.Euler(euler);
            }
            Part("Bowl", PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0f), new Vector3(0.58f, 0.36f, 0.58f), body);
            Part("Rim", PrimitiveType.Cylinder, new Vector3(0f, 0.66f, 0f), new Vector3(0.6f, 0.012f, 0.6f), metal);
            Part("Lid", PrimitiveType.Sphere, new Vector3(0f, 0.7f, 0f), new Vector3(0.56f, 0.46f, 0.56f), body);
            Part("Lid handle", PrimitiveType.Cube, new Vector3(0f, 0.95f, 0f), new Vector3(0.16f, 0.04f, 0.05f), metal);
            Part("Vent", PrimitiveType.Cylinder, new Vector3(0.12f, 0.9f, 0f), new Vector3(0.08f, 0.01f, 0.08f), metal, new Vector3(0f, 0f, 22f));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Sin(a) * 0.22f, 0.24f, Mathf.Cos(a) * 0.22f);
                Part("Leg", PrimitiveType.Cylinder, at, new Vector3(0.03f, 0.24f, 0.03f), metal, new Vector3(Mathf.Cos(a) * -8f, 0f, Mathf.Sin(a) * 8f));
            }
            Part("Shelf", PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.01f, 0.36f), metal);
            for (int i = 0; i < 3; i++)
                Part("Smoke", PrimitiveType.Sphere, new Vector3(0.12f + 0.05f * i, 1.08f + 0.2f * i, 0.04f * i), Vector3.one * (0.14f + 0.06f * i), smoke);
        }

        /// <summary>A memory you walk up to: a glowing card where it lies, with a soft halo.</summary>
        static GameObject Memory(string name, Transform root, Vector3 at, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var card = DreamEscapeBuilder.Glow("HouseMemory", new Color(1f, 0.86f, 0.62f), 1.1f, 0f, 0.4f);
            var halo = DreamEscapeBuilder.Glow("HouseMemoryHalo", new Color(1f, 0.92f, 0.75f), 0.35f, 0f, 0.3f);
            DreamEscapeBuilder.Part("Card", PrimitiveType.Cube, go.transform, Vector3.zero, size, card);
            DreamEscapeBuilder.Part("Halo", PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.7f, halo);
            return go;
        }

        /// <summary>The beacon: a soft column of gold light standing up out of the tower (no collider, no shadow).</summary>
        static GameObject Beacon(LarchHill h, Transform root)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "The beacon (over the tower)";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.position = U(h.beaconFoot) + new Vector3(0f, LarchHill.BeaconHeight * 0.5f, 0f);
            go.transform.localScale = new Vector3(2.2f, LarchHill.BeaconHeight * 0.5f, 2.2f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = DreamEscapeBuilder.Glow("StreetBeacon", new Color(1f, 0.86f, 0.6f), 0.7f, 1f, 0.15f);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // ---------- The house at the top ----------

        public struct HouseParts
        {
            public Transform elias, theo, lena, door;
            public Light light;
            public GameObject glow;
        }

        /// <summary>The Lasname house: <see cref="DreamHouseExterior"/> (the round 5 outside, the house builder's) built at the
        /// origin, then this scene's front wall (its window and door), the warm room behind the window with Theo setting the
        /// table, Elias at the stove and Lena out of sight beside it, the door on its hinge, the porch
        /// light; then the whole thing moved onto the knoll.</summary>
        static Transform House(LarchHill h, Transform root, out HouseParts parts)
        {
            var house = new GameObject("The Lasname house").transform;
            house.SetParent(root, false);
            DreamHouseExterior.Build(house);
            // The lawns the exterior lays round the house reach far past this knoll: not here.
            foreach (var t in house.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith("Lawn (beyond")) Object.DestroyImmediate(t.gameObject);

            const float x0 = DreamHouse.OutX0, x1 = DreamHouse.OutX1, eave = DreamHouse.EaveY;
            float ridgeX = (x0 + x1) * 0.5f, ridgeY = (DreamHouse.AtticFloor - DreamHouse.Ground) + DreamHouse.RidgeHeight;
            var P = DreamEscapeBuilder.Palette;
            var siding = DreamEscapeBuilder.Pastel("HouseFront", P[1], 0.05f);
            var trim = DreamEscapeBuilder.Pastel("HouseTrim", P[0], 0.03f);
            var front = new GameObject("The front (this scene's)").transform;
            front.SetParent(house, false);
            const float z0 = 0.05f, z1 = 0.5f;
            void Wall(string n, float a, float b, float y0, float y1) =>
                DreamEscapeBuilder.Box(n, front, new Vector3((a + b) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), new Vector3(b - a, y1 - y0, z1 - z0), siding);
            // The window into the living room (x -5.2 to -3.4, sill 0.9, head 2.4) and the door (x -1.5 to 1.5, 2.6 high).
            Wall("Front wall (left)", x0, -5.2f, 0f, eave);
            Wall("Front wall (under the window)", -5.2f, -3.4f, 0f, 0.9f);
            Wall("Front wall (over the window)", -5.2f, -3.4f, 2.4f, eave);
            Wall("Front wall (between)", -3.4f, -1.5f, 0f, eave);
            Wall("Front wall (over the door)", -1.5f, 1.5f, 2.6f, eave);
            Wall("Front wall (right)", 1.5f, x1, 0f, eave);
            DreamHouseRooms.Poly(front, "Front gable", DreamEscapeBuilder.Pastel("HouseShingle", new Color(0.95f, 0.74f, 0.64f), 0.04f),
                new Vector3(x0, eave, z0 - 0.005f), new Vector3(ridgeX, ridgeY, z0 - 0.005f), new Vector3(x1, eave, z0 - 0.005f));
            foreach (var (a, b, y0, y1) in new[] { (-5.3f, -3.3f, 0.82f, 0.98f), (-5.3f, -3.3f, 2.32f, 2.5f) })
                DreamEscapeBuilder.Box("Window frame", front, new Vector3((a + b) * 0.5f, (y0 + y1) * 0.5f, -0.02f), new Vector3(b - a, y1 - y0, 0.1f), trim);
            foreach (float x in new[] { -5.25f, -3.35f })
                DreamEscapeBuilder.Box("Window frame", front, new Vector3(x, 1.65f, -0.02f), new Vector3(0.1f, 1.5f, 0.1f), trim);
            DreamEscapeBuilder.Box("Window mullion", front, new Vector3(-4.3f, 1.65f, 0.0f), new Vector3(0.05f, 1.5f, 0.05f), trim);

            // The room behind the window: warm walls, a floor; the stove at the back.
            var room = new GameObject("The room behind the window").transform;
            room.SetParent(house, false);
            var warm = DreamEscapeBuilder.Glow("StreetRoomGlow", new Color(1f, 0.86f, 0.64f), 0.55f, 0f, 0.05f);
            DreamEscapeBuilder.Quad("Room (back wall)", room, new Vector3(-3.3f, 1.6f, 4.2f), Vector3.back, 6f, 3.2f, warm);
            DreamEscapeBuilder.Quad("Room (floor)", room, new Vector3(-3.3f, 0.02f, 2.3f), Vector3.up, 6f, 3.8f, DreamEscapeBuilder.Pastel("HouseFloor", Wood, 0.03f));
            DreamEscapeBuilder.Box("The stove", room, new Vector3(-4.6f, 0.45f, 3.6f), new Vector3(1f, 0.9f, 0.7f), DreamEscapeBuilder.Pastel("StreetStove", P[0], 0.04f));
            DreamEscapeBuilder.Box("The table", room, new Vector3(-2.6f, 0.38f, 2.2f), new Vector3(1.6f, 0.06f, 1f), DreamEscapeBuilder.Pastel("HouseTable", new Color(0.78f, 0.6f, 0.46f), 0.05f));
            var lamp = new GameObject("The room's light").AddComponent<Light>();
            lamp.transform.SetParent(room, false);
            lamp.transform.position = new Vector3(-3.5f, 2.6f, 2.4f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.84f, 0.6f);
            lamp.range = 6f;
            lamp.intensity = 2f;
            lamp.shadows = LightShadows.None;

            // The people in the window: Theo setting the table, Elias at the stove; Lena out of sight to the left.
            var theo = CutOut("Theo (setting the table)", DreamArt.Theo, room, new Vector3(-2.4f, 0f, 2.6f), DreamHouseBuilder.TheoCardHeight);
            var elias = CutOut("Elias (at the stove)", DreamArt.Elias, room, new Vector3(-4.5f, 0f, 2.7f), EliasCardHeight);
            var lena = CutOut("Lena (out of sight)", DreamArt.Lena, room, new Vector3(-7.0f, 0f, 2.4f), DreamHouseBuilder.LenaCardHeight);

            // The door on its hinge (open: swung in), and the porch light under the exterior's lantern.
            var hinge = new GameObject("The front door (on its hinge)").transform;
            hinge.SetParent(house, false);
            hinge.localPosition = new Vector3(-1.45f, 0f, 0.3f);
            DreamEscapeBuilder.Box("Door leaf", hinge, new Vector3(-1.45f + 1.45f, 1.28f, 0.3f), new Vector3(2.9f, 2.56f, 0.08f), DreamEscapeBuilder.Pastel("HouseFrontDoor", new Color(0.88f, 0.45f, 0.44f), 0.03f));
            hinge.localRotation = Quaternion.Euler(0f, -95f, 0f);
            DreamEscapeBuilder.Quad("Hall light (through the door)", house, new Vector3(0f, 1.3f, 1.2f), Vector3.back, 2.8f, 2.6f, warm);
            var porch = new GameObject("The porch light").AddComponent<Light>();
            porch.transform.SetParent(house, false);
            porch.transform.localPosition = new Vector3(1.85f, 2.2f, -0.6f);
            porch.type = LightType.Point;
            porch.color = new Color(1f, 0.86f, 0.6f);
            porch.range = 7f;
            porch.intensity = 2.4f;
            porch.shadows = LightShadows.None;
            var glow = DreamEscapeBuilder.Part("The porch light (glow)", PrimitiveType.Sphere, house, Vector3.zero, Vector3.one * 0.5f,
                DreamEscapeBuilder.Glow("StreetPorchGlow", new Color(1f, 0.9f, 0.66f), 1.2f, 0f, 0.1f));
            glow.transform.localPosition = new Vector3(1.85f, 2.3f, -0.12f);

            house.position = U(h.houseOrigin);
            parts = new HouseParts { elias = elias.transform, theo = theo.transform, lena = lena.transform, door = hinge, light = porch, glow = glow };
            return house;
        }

        // ---------- The cast ----------

        /// <summary>A flat cut-out of the dream's cast that turns to face the camera (the house's).</summary>
        static GameObject CutOut(string name, string art, Transform parent, Vector3 feet, float height)
        {
            var tex = DreamArt.Get(art, out bool placeholder);
            Debug.Log("JAMKIT_STREET art: " + art + (placeholder ? " PLACEHOLDER" : " real"));
            var mat = DreamEscapeBuilder.Mat("Cutout_" + art, "JamKit/Dream/Cutout");
            mat.SetTexture("_MainTex", tex);
            float aspect = tex != null && tex.height > 0 ? tex.width / (float)tex.height : 0.5f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = feet;
            go.AddComponent<DreamFaceCamera>();
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
            return go;
        }
    }
}
