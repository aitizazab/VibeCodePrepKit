using System;
using System.Collections.Generic;
using JamKit.DeepBlock;
using JamKit.EditorTools;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NV2 = System.Numerics.Vector2;
using NV3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// A preview scene of the hill's kit (no gameplay): twelve houses of every roof kind, storey count and finish, each
    /// tree kind (the big oak with two climbable limbs), bushes, a hedge, grass and flowers, every prop kind, every
    /// fence and wall style, and every landmark, on a lit lawn at golden hour (the Look agent's rig). Each item is its
    /// own chunk, and the build log lists its triangle count ("JAMKIT_KIT_PREVIEW tris ..."). Graphics needed for the
    /// capture (no -nographics):
    /// <code>Unity -batchmode -projectPath . -executeMethod JamKit.DreamEscape.EditorTools.HillKitPreview.CapturePreviewFromCommandLine -logFile out/hill-kit-preview.log</code>
    /// </summary>
    public static class HillKitPreview
    {
        public const string ScenePath = HillMaterials.StreetRoot + "/Scenes/HillKitPreview.unity";
        public const string MeshFolder = HillMaterials.StreetRoot + "/Generated/Preview";

        static NV3 N(float x, float y, float z) => new NV3(x, y, z);

        [MenuItem("JamKit/Dream Street/Kit Preview Scene")]
        static void Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildPreview();
        }

        /// <summary>-executeMethod JamKit.DreamEscape.EditorTools.HillKitPreview.CapturePreviewFromCommandLine: builds the
        /// preview scene and writes 8 stills (1920x1080) to out/hill-kit-preview/&lt;time&gt;/, then exits.</summary>
        public static void CapturePreviewFromCommandLine()
        {
            int code = 0;
            try
            {
                BuildPreview();
                string dir = VhLookCapture.Capture(ScenePath, Shots(), "hill-kit-preview");
                Debug.Log("JAMKIT_KIT_PREVIEW_OK " + dir);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        /// <summary>The stills: the street of houses, a porch up close, the trees, the props, the fences, the church and
        /// hospital, the shops.</summary>
        public static List<VhLookCapture.Shot> Shots() => new List<VhLookCapture.Shot>
        {
            new VhLookCapture.Shot("01-houses-street", new Vector3(-32f, 1.7f, 7f), new Vector3(-18f, 3.6f, 24f), 70f),
            new VhLookCapture.Shot("02-houses-overview", new Vector3(-78f, 15f, -6f), new Vector3(-8f, 3f, 42f), 60f),
            new VhLookCapture.Shot("03-house-porch-close", new Vector3(-41.2f, 1.6f, 11.5f), new Vector3(-44f, 2.4f, 20f), 70f),
            new VhLookCapture.Shot("04-trees", new Vector3(-38f, 3.2f, 3f), new Vector3(-2f, 6f, -24f), 70f),
            new VhLookCapture.Shot("05-props", new Vector3(-56f, 6.5f, -40f), new Vector3(-12f, 0.6f, -76f), 65f),
            new VhLookCapture.Shot("06-fences", new Vector3(-72f, 2.6f, -107f), new Vector3(-20f, 1f, -116f), 65f),
            new VhLookCapture.Shot("07-church-hospital", new Vector3(-62f, 4.5f, 104f), new Vector3(-38f, 13f, 150f), 70f),
            new VhLookCapture.Shot("08-shops", new Vector3(-66f, 1.7f, 226f), new Vector3(-62f, 3.2f, 206f), 70f),
        };

        /// <summary>Builds and saves the preview scene; returns its path.</summary>
        public static string BuildPreview()
        {
            HillMaterials.Reset();
            DreamEscapeBuilder.EnsureFolder(HillMaterials.StreetRoot + "/Scenes");
            DreamEscapeBuilder.EnsureFolder(HillMaterials.MatRoot);
            if (AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.DeleteAsset(MeshFolder);
            DreamEscapeBuilder.EnsureFolder(MeshFolder);

            var scene = SceneBuilder.NewScene(new Color(0.55f, 0.62f, 0.75f), out var cam);
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            cam.farClipPlane = 1500f;
            var first = Shots()[1];
            cam.transform.position = first.eye;
            cam.transform.rotation = Quaternion.LookRotation(first.look - first.eye, Vector3.up);
            try
            {
                VhLook.ApplyGoldenHour(scene, new VhLook.Options { sunAzimuth = 250f, sunElevation = 12f });
                VhLook.SetupCamera(cam);
            }
            catch (Exception e)
            {
                Debug.LogWarning("JAMKIT_KIT_PREVIEW the Look rig failed, a plain sun stands in: " + e.Message);
                var sun = new GameObject("Sun (golden hour)").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.77f, 0.52f);
                sun.intensity = 2.6f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(12f, 250f - 180f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.75f);
                RenderSettings.ambientEquatorColor = new Color(0.7f, 0.58f, 0.48f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.22f);
            }

            var root = new GameObject("Hill kit preview").transform;
            int total = 0;
            void Chunk(string name, Action<HillMesh> draw)
            {
                var m = new HillMesh(name);
                try { draw(m); }
                catch (Exception e) { Debug.LogError("JAMKIT_KIT_PREVIEW " + name + " failed: " + e); return; }
                var go = m.Build(root, MeshFolder);
                int tris = 0;
                foreach (var f in go.GetComponentsInChildren<MeshFilter>()) tris += f.sharedMesh != null ? f.sharedMesh.triangles.Length / 3 : 0;
                total += tris;
                Debug.Log("JAMKIT_KIT_PREVIEW tris " + tris + "  " + name);
            }

            // The lawn and the streets.
            Chunk("Ground", m =>
            {
                m.Quad(HillMat.Grass, new Vector3(-170f, 0f, -170f), new Vector3(170f, 0f, -170f), new Vector3(170f, 0f, 260f), new Vector3(-170f, 0f, 260f), Vector3.up);
                foreach (var (z0, z1) in new[] { (5f, 13f), (128f, 133f), (214f, 222f), (-46f, -44f) })
                    m.Quad(HillMat.Asphalt, new Vector3(-170f, 0.01f, z0), new Vector3(170f, 0.01f, z0), new Vector3(170f, 0.01f, z1), new Vector3(-170f, 0.01f, z1), Vector3.up);
                m.Box(HillMat.Kerb, new Vector3(-170f, 0f, 13f), new Vector3(170f, 0.14f, 13.3f), 0.02f);
                m.Quad(HillMat.Paving, new Vector3(-170f, 0.012f, 13.3f), new Vector3(170f, 0.012f, 13.3f), new Vector3(170f, 0.012f, 15.2f), new Vector3(-170f, 0.012f, 15.2f), Vector3.up);
            });

            // Twelve houses: every roof kind, storeys 1 to 3, porches with and without roofs, turrets, dormers, bays,
            // shutters, tall plinths (steps), and two turned to face east and west.
            var roofs = new[] { HillRoof.GableSide, HillRoof.GableFront, HillRoof.Hip, HillRoof.Gambrel, HillRoof.Flat, HillRoof.Shed };
            var walls = new[] { HillMat.SidingWhite, HillMat.BrickRed, HillMat.SidingBlue, HillMat.Stucco, HillMat.SidingSage, HillMat.BrickCream, HillMat.SidingButter, HillMat.ShingleWall, HillMat.SidingRose, HillMat.StuccoWarm, HillMat.SidingLilac, HillMat.BrickDark };
            var roofMats = new[] { HillMat.RoofGrey, HillMat.RoofRed, HillMat.RoofSlate, HillMat.RoofGreen, HillMat.RoofClay, HillMat.RoofMetal };
            var trims = new[] { HillMat.TrimWhite, HillMat.TrimCream, HillMat.TrimWhite, HillMat.TrimDark };
            var doors = new[] { HillMat.DoorRed, HillMat.DoorBlue, HillMat.DoorGreen, HillMat.DoorYellow };
            float[] storeys = { 2f, 1.5f, 2f, 2.5f, 1f, 2f, 3f, 1.5f, 2.5f, 2f, 1f, 2f };
            for (int i = 0; i < 12; i++)
            {
                int row = i / 6, col = i % 6;
                var h = new HillHouse
                {
                    name = "House " + (i + 1), seed = 17 + i * 31, width = 8.5f + (i % 3) * 0.8f, depth = 9.5f + (i % 2) * 1.2f,
                    storeys = storeys[i], storeyHeight = 2.85f + (i % 2) * 0.1f, roof = roofs[i % 6], pitch = 30f + (i % 4) * 5f,
                    overhang = 0.3f + (i % 3) * 0.15f, plinth = 0.3f + (i % 4) * 0.35f, wall = walls[i], roofMat = roofMats[(i + row) % 6],
                    trim = trims[i % 4], door = doors[i % 4], porch = i % 5 != 4, porchRoof = i % 3 != 2, bay = i % 4 == 1,
                    chimney = i % 3 != 1, shutters = i % 4 == 2, gutters = true, dormers = i % 4 == 0 ? 2 : i % 7 == 3 ? 1 : 0,
                    turret = i == 2 ? 1 : i == 9 ? -1 : 0, litShare = 0.3f, porchDepth = 2.2f,
                };
                h.facing = i == 10 ? HillFacing.East : i == 11 ? HillFacing.West : HillFacing.South;
                h.center = N(-66f + col * 22f, h.plinth, row == 0 ? 25f : 62f);   // the floor stands the plinth above the lawn
                Chunk(h.name + " (" + h.roof + ", " + h.storeys + " storeys)", m => HillHouses.Draw(m, h));
            }

            // Trees: each kind (the big oak with two limbs), bushes, a hedge, grass and flowers.
            int[] kinds = { 0, 1, 2, 3, 4, 0, 2, 3 };
            for (int k = 0; k < kinds.Length; k++)
            {
                var t = new HillTree { name = "Tree " + k + " (kind " + kinds[k] + ")", foot = N(-60f + k * 16f, 0f, -20f), kind = kinds[k], seed = 5 + k * 13 };
                switch (kinds[k])
                {
                    case 1: t.height = 13f; t.canopy = 3.2f; t.trunk = 0.3f; break;
                    case 2: t.height = 5.5f; t.canopy = 2.8f; t.trunk = 0.18f; break;
                    case 3: t.height = 12f; t.canopy = 3f; t.trunk = 0.28f; break;
                    case 4: t.height = 15f; t.canopy = 7f; t.trunk = 0.6f; break;
                    default: t.height = 10f; t.canopy = 4.2f; t.trunk = 0.35f; break;
                }
                Chunk(t.name, m =>
                {
                    HillTrees.Draw(m, t);
                    if (t.kind != 4) return;
                    var foot = HillKit.U(t.foot);
                    var limbs = new[]
                    {
                        new BlockPiece { name = "limb east", kind = BlockKind.Tree, style = "limb", center = N(foot.x + 0.6f + 2.2f, 3.2f - 0.25f, foot.z), size = N(4.4f, 0.5f, 0.8f) },
                        new BlockPiece { name = "limb south", kind = BlockKind.Tree, style = "limb", center = N(foot.x, 4.6f - 0.25f, foot.z - 0.6f - 1.8f), size = N(0.8f, 0.5f, 3.6f) },
                    };
                    foreach (var l in limbs) HillTrees.DrawLimb(m, l, foot);
                });
            }
            Chunk("Bushes, hedge, grass, flowers", m =>
            {
                for (int i = 0; i < 4; i++) HillTrees.DrawBush(m, new Vector3(-62f + i * 3f, 0f, -33f), new Vector3(1.2f + i * 0.3f, 0.9f + i * 0.2f, 1.2f + i * 0.2f), i * 7 + 1);
                HillTrees.DrawHedge(m, new BlockPiece { name = "hedge", kind = BlockKind.Hedge, center = N(-40f, 0.8f, -33f), size = N(9f, 1.6f, 0.9f) });
                HillTrees.DrawHedge(m, new BlockPiece { name = "hedge (along z)", kind = BlockKind.Hedge, center = N(-30f, 0.6f, -36f), size = N(0.8f, 1.2f, 6f) });
                HillTrees.DrawGrass(m, new Vector3(-16f, 0f, -34f), new Vector2(8f, 4f), 14f, 3);
                HillTrees.DrawFlowers(m, new Vector3(-6f, 0f, -34f), new Vector2(5f, 3f), 16f, 4);
            });

            // Every prop kind, eight to a row.
            var all = (HillPropKind[])Enum.GetValues(typeof(HillPropKind));
            for (int i = 0; i < all.Length; i++)
            {
                var p = new HillProp { kind = all[i], position = N(-49f + (i % 8) * 14f, 0f, -52f - (i / 8) * 9f), yaw = (i % 3 == 2) ? 90f : 0f, variant = i * 3 + 1, tag = "preview" };
                if (p.kind == HillPropKind.StreetSign) p.variant = HillAtlas.SignLarchStreet;
                Chunk("Prop " + p.kind + (p.yaw != 0f ? " (turned)" : ""), m => HillProps.Draw(m, p));
            }

            // Every fence and wall style, 8 m runs along x, and a picket run along z.
            for (int k = 0; k < HillFences.Styles.Length; k++)
            {
                string style = HillFences.Styles[k];
                float h = style == "stone_retaining" ? 3f : style == "concrete_retaining" ? 2.5f : style == "board" || style == "lattice" ? 1.8f : style == "hedge" ? 1.6f : style == "brick_low" ? 0.9f : style == "garden_wall" ? 1.2f : style == "stone_low" ? 0.8f : style == "chainlink" ? 1.5f : style == "gate" ? 1.1f : 1.05f;
                float t = style.Contains("retaining") ? 0.7f : style == "hedge" ? 0.9f : style == "stone_low" ? 0.5f : style == "brick_low" || style == "garden_wall" ? 0.3f : style == "board" || style == "lattice" ? 0.14f : 0.1f;
                float len = style == "gate" ? 1.0f : 8f;
                var piece = new BlockPiece { name = "fence " + style, kind = BlockKind.Fence, style = style, center = N(-66f + k * 12f, h * 0.5f, -116f), size = N(len, h, t) };
                Chunk("Fence " + style, m => HillFences.Draw(m, piece));
            }
            Chunk("Fence picket (along z)", m => HillFences.Draw(m, new BlockPiece { name = "picket z", kind = BlockKind.Picket, center = N(84f, 0.5f, -112f), size = N(0.1f, 1f, 8f) }));

            // The landmarks.
            HillBuilding B(HillBuildingKind kind, float x0, float z0, float x1, float z1, float height, HillFacing facing, float floorY = 0f, params int[] signs)
            {
                var b = new HillBuilding { kind = kind, name = kind.ToString(), min = new NV2(x0, z0), max = new NV2(x1, z1), height = height, facing = facing, floorY = floorY, seed = (int)kind * 11 + 3 };
                b.signs.AddRange(signs);
                return b;
            }
            var buildings = new List<HillBuilding>
            {
                B(HillBuildingKind.Church, -110f, 140f, -96f, 166f, 8f, HillFacing.South),
                B(HillBuildingKind.Hospital, -24f, 150f, 24f, 180f, 14f, HillFacing.South),
                B(HillBuildingKind.School, 40f, 140f, 70f, 154f, 7.6f, HillFacing.South),
                B(HillBuildingKind.ShopRow, -80f, 200f, -50f, 212f, 7.5f, HillFacing.North, 0f, HillAtlas.SignBakery, HillAtlas.SignDiner, HillAtlas.SignFlorist, HillAtlas.SignPharmacy),
                B(HillBuildingKind.CornerShop, -40f, 200f, -30f, 210f, 6.5f, HillFacing.North, 0f, HillAtlas.SignNews),
                B(HillBuildingKind.BikeShop, -20f, 200f, -12f, 210f, 5f, HillFacing.North, 0f, HillAtlas.SignAbernathy),
                B(HillBuildingKind.Garage, 0f, 200f, 15f, 206f, 2.6f, HillFacing.South),
                B(HillBuildingKind.Kiosk, 22f, 200f, 25f, 203f, 2.8f, HillFacing.South, 0f, HillAtlas.SignNews),
                B(HillBuildingKind.Boathouse, 35f, 200f, 43f, 212f, 5f, HillFacing.East),
                B(HillBuildingKind.WaterTower, 55f, 200f, 61f, 206f, 18f, HillFacing.South),
                B(HillBuildingKind.Treehouse, 70f, 200f, 73f, 203f, 3f, HillFacing.South, 3f),
                B(HillBuildingKind.Terrace, 85f, 200f, 105f, 209f, 7f, HillFacing.South),
            };
            foreach (var b in buildings) Chunk("Landmark " + b.kind, m => HillLandmarks.Draw(m, b));

            Debug.Log("JAMKIT_KIT_PREVIEW total tris " + total);
            foreach (var pack in HillMaterials.MissingPacks) Debug.LogWarning("JAMKIT_KIT_PREVIEW texture pack missing (noise stands in): " + pack);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("JAMKIT_KIT_PREVIEW scene " + ScenePath);
            return ScenePath;
        }
    }
}
