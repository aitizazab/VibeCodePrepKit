using System.Collections.Generic;
using JamKit.Look.EditorTools;
using JamKit.Movement;
using UnityEngine;
using UnityEngine.Rendering;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The house standing alone (the DreamHouse scene; on the hill World builds all of this for real): a stand-in of the hill
    /// round the lot at World's numbers (HillAnchors: the lots at the house's ground, Rowan Street below them, Chapel Row's
    /// terrace 5.4 m up behind the back fence, School Lane's 7 m above that, the hill climbing on, the ground falling away
    /// south), the neighbours' houses, trees, and Look's far ring so nothing ends in a void (house-1.md: "the lot ends in a
    /// cut dirt edge over peach void"; "Chapel Row is an empty paved plane with four posts").
    /// <para>Chapel Row is a street: the retaining wall with its coping and railing over the Lasnames' yard, a broad south
    /// pavement where the dream's door stands (the Lasnames' red front door in a piece of their sage clapboard, open, light
    /// round its edges), kerbs, the wet road with its centre dashes and a raised crossing on her line, the north pavement,
    /// front gardens and a row of houses with the lane up the hill between them (the path in the rain), street lamps lit,
    /// puddles; School Lane's wall at the lane's end with its houses above.</para>
    /// Everything she can reach has a collider; her line (x 0, from the door 33 m north) is flat and clear.
    /// </summary>
    public static class HouseEscapeChapelRow
    {
        static Vector3 U(NVec3 v) => new Vector3(v.X, v.Y, v.Z);

        const float RoadS = 32.5f, RoadN = 39.7f, KerbW = 0.3f, PavementN = 43f, FrontsZ = 48f, SchoolWallZ = 75.5f, Lane = 1.6f;
        const float Terrace = 7f;

        /// <summary>The stand-in ground's height (the house's frame) at (x, z).</summary>
        public static float Ground(float ty, float x, float z)
        {
            float g;
            if (z < -20f) g = -1f - (-20f - z) * 0.1f;            // down the hill, south of Rowan Street
            else if (z < -6.1f) g = -1f;                          // Rowan Street's band (the street and footpath sit on it)
            else if (z < 24.8f) g = -0.06f;                       // the lots, at the house's ground
            else if (z < 76f) g = ty - 0.06f;                     // Chapel Row's terrace
            else g = ty + Terrace - 0.06f + (z - 76f) * 0.1f;     // School Lane and the hill above
            // Under the house (and a metre round it, under its lawn): down below the basement, so no ground shows inside.
            float dx = Mathf.Max(DreamHouse.X0 - 0.15f - x, Mathf.Max(0f, x - (DreamHouse.GarageX1 + 0.15f)));
            float dz = Mathf.Max(DreamHouse.Z0 - 0.15f - z, Mathf.Max(0f, z - (DreamHouse.Z1 + 0.15f)));
            float outside = Mathf.Sqrt(Mathf.Max(0f, dx) * Mathf.Max(0f, dx) + Mathf.Max(0f, dz) * Mathf.Max(0f, dz));
            if (z > -6f && z < 24.8f && outside < 1f) g = Mathf.Min(g, Mathf.Lerp(-3.6f, -0.06f, outside));
            // Away from the streets the ground rolls a little.
            float far = Mathf.Clamp01((Mathf.Abs(x) - 70f) / 40f);
            if (far > 0f) g += (Mathf.PerlinNoise(x * 0.02f + 3f, z * 0.02f - 7f) - 0.5f) * 3f * far;
            return g;
        }

        public static Transform Build(DreamHouse h, Transform root)
        {
            float ty = h.exitDoor.Y;
            var ex = U(h.exitDoor);
            var stand = new GameObject("The hill round the house (stand-in)").transform;
            stand.SetParent(root, false);
            var b = new MeshBuilder();
            var colliders = new GameObject("Colliders").transform;
            colliders.SetParent(stand, false);
            void Solid(string name, Vector3 mn, Vector3 mx, Material[] faces, bool runnable = false)
            {
                if (faces != null) b.Box(mn, mx, faces);
                var go = new GameObject("Collider: " + name);
                go.transform.SetParent(colliders, false);
                go.transform.localPosition = (mn + mx) * 0.5f;
                go.AddComponent<BoxCollider>().size = mx - mn;
                go.AddComponent<MovementSurface>().runnable = runnable;
            }
            Material[] All(Material m) => new[] { m, m, m, m, m, m };
            Material[] TopOnly(Material top, Material side) => new[] { side, side, null, top, side, side };

            var brick = HouseMaterials.Look("Brick_Dark");
            var kerb = HouseMaterials.Foundation();
            var wetWalk = Wet("Concrete_Sidewalk", "ChapelWet", new Color(0.82f, 0.82f, 0.84f), 0.62f);
            var wetRoad = Wet("Asphalt", "ChapelWet", new Color(0.7f, 0.7f, 0.74f), 0.78f);
            var paint = HouseEscapeLook.Clean("RoadPaint", new Color(0.9f, 0.88f, 0.8f), 0.5f);
            var lawn = HouseMaterials.Lawn();

            // ---- Between the yard and the terrace: the Chapel Steps' foot, the retaining wall, its coping and railing.
            Solid("The Chapel Steps' foot", new Vector3(DreamHouse.YardX0, -1f, DreamHouse.YardZ1 + 0.1f), new Vector3(DreamHouse.YardX1, 0f, 24.1f), new[] { lawn, lawn, null, lawn, lawn, lawn });
            Solid("Chapel Row's wall", new Vector3(-150f, -1.2f, 24.1f), new Vector3(150f, ty, 24.6f), new[] { brick, brick, null, kerb, brick, brick }, true);
            b.Box(new Vector3(-150f, ty - 0.02f, 24.02f), new Vector3(150f, ty + 0.06f, 24.66f), kerb);
            Solid("Chapel Row's railing", new Vector3(-150f, ty, 24.3f), new Vector3(150f, ty + 1.1f, 24.5f), null);
            var kit = new HouseEscapeKit();
            var iron = HouseEscapeLook.Iron();
            for (float x = -60f; x <= 60f; x += 1.8f) kit.Rod(new Vector3(x, ty + 0.06f, 24.4f), new Vector3(x, ty + 1.05f, 24.4f), 0.022f, iron, 6);
            for (float x = -60f; x < 60f; x += 0.15f) if (Mathf.Repeat(x + 60f, 1.8f) > 0.1f) kit.Rod(new Vector3(x, ty + 0.12f, 24.4f), new Vector3(x, ty + 0.98f, 24.4f), 0.008f, iron, 4);
            kit.Rod(new Vector3(-60f, ty + 1.05f, 24.4f), new Vector3(60f, ty + 1.05f, 24.4f), 0.03f, iron, 8);
            kit.Rod(new Vector3(-60f, ty + 0.12f, 24.4f), new Vector3(60f, ty + 0.12f, 24.4f), 0.015f, iron, 6);

            // ---- Chapel Row: the south pavement, kerbs, the road (its raised crossing on her line), the north pavement.
            Solid("Chapel Row's south pavement", new Vector3(-150f, ty - 1.6f, 24.6f), new Vector3(150f, ty, RoadS - KerbW), TopOnly(wetWalk, kerb));
            Solid("Chapel Row's kerb (south)", new Vector3(-150f, ty - 0.6f, RoadS - KerbW), new Vector3(150f, ty, RoadS), TopOnly(kerb, kerb));
            Solid("Chapel Row (the road, west)", new Vector3(-150f, ty - 0.6f, RoadS), new Vector3(-3f, ty - 0.12f, RoadN), TopOnly(wetRoad, kerb));
            Solid("Chapel Row (the road, east)", new Vector3(3f, ty - 0.6f, RoadS), new Vector3(150f, ty - 0.12f, RoadN), TopOnly(wetRoad, kerb));
            Solid("Chapel Row's crossing", new Vector3(-3f, ty - 0.6f, RoadS), new Vector3(3f, ty, RoadN), TopOnly(wetRoad, wetRoad));
            for (float x = -2.6f; x < 2.7f; x += 1.0f) b.Box(new Vector3(x, ty, RoadS + 0.3f), new Vector3(x + 0.5f, ty + 0.004f, RoadN - 0.3f), paint);
            float mid = (RoadS + RoadN) * 0.5f;
            for (float x = -148f; x < 148f; x += 6f) if (Mathf.Abs(x + 1.5f) > 4.5f) b.Box(new Vector3(x, ty - 0.12f, mid - 0.06f), new Vector3(x + 3f, ty - 0.116f, mid + 0.06f), paint);
            Solid("Chapel Row's kerb (north)", new Vector3(-150f, ty - 0.6f, RoadN), new Vector3(150f, ty, RoadN + KerbW), TopOnly(kerb, kerb));
            Solid("Chapel Row's north pavement", new Vector3(-150f, ty - 1.6f, RoadN + KerbW), new Vector3(150f, ty, PavementN), TopOnly(wetWalk, kerb));
            // Puddles (dark, glossy, flush).
            var puddle = HouseEscapeLook.Clean("Puddle", new Color(0.08f, 0.09f, 0.11f), 0.97f);
            foreach (var (x, z, r) in new[] { (-6.5f, 34.6f, 0.9f), (7.8f, 37.2f, 1.3f), (-14f, 36f, 1.1f), (2.4f, 28.2f, 0.6f), (-3.4f, 41.4f, 0.5f), (16f, 33.8f, 0.8f) })
                kit.Disc(new Vector3(x, (z > RoadS && z < RoadN && Mathf.Abs(x) > 3f ? ty - 0.12f : ty) + 0.003f, z), Vector3.up, r, puddle, 24);

            // ---- The lane up the hill between the houses (her path in the rain), its hedges, the steps to School Lane.
            Solid("The lane up the hill", new Vector3(-Lane, ty - 1.6f, PavementN), new Vector3(Lane, ty, SchoolWallZ), TopOnly(wetWalk, kerb));
            var hedge = HouseMaterials.Hedge();
            foreach (float s in new[] { -1f, 1f })
                Solid("The lane's hedge", new Vector3(s < 0 ? -Lane - 0.7f : Lane, ty, PavementN + 0.4f), new Vector3(s < 0 ? -Lane : Lane + 0.7f, ty + 1.2f, FrontsZ - 0.5f), All(hedge));
            float stepsZ = SchoolWallZ - 9.5f;
            for (int k = 0; k < 36; k++)
            {
                float z0 = stepsZ + k * (9.5f / 36f), y = ty + (k + 1) * (Terrace / 36f);
                b.Box(new Vector3(-Lane, ty, z0), new Vector3(Lane, y, z0 + 9.5f / 36f), new[] { kerb, kerb, null, wetWalk, kerb, kerb });
            }
            foreach (float s in new[] { -1f, 1f }) kit.Rod(new Vector3(s * (Lane - 0.1f), ty + 0.95f, stepsZ), new Vector3(s * (Lane - 0.1f), ty + Terrace + 0.95f, SchoolWallZ), 0.025f, iron, 8);
            Solid("School Lane's wall", new Vector3(-150f, ty - 0.5f, SchoolWallZ), new Vector3(150f, ty + Terrace, SchoolWallZ + 0.5f), new[] { brick, brick, null, kerb, brick, brick }, true);
            Solid("School Lane's pavement", new Vector3(-150f, ty + Terrace - 2f, SchoolWallZ + 0.5f), new Vector3(150f, ty + Terrace, SchoolWallZ + 5f), TopOnly(wetWalk, kerb));
            Solid("School Lane", new Vector3(-150f, ty + Terrace - 2f, SchoolWallZ + 5f), new Vector3(150f, ty + Terrace - 0.12f, SchoolWallZ + 12f), TopOnly(wetRoad, kerb));

            // ---- Rowan Street beyond the lot's stand-in (the plan's runs x -34..40): the road and footpaths carry on.
            var asphalt = HouseMaterials.Asphalt();
            var paving = HouseMaterials.Paving();
            float sy = DreamHouse.StreetY;
            foreach (var (x0, x1) in new[] { (-150f, -34f), (40f, 150f) })
            {
                Solid("Rowan Street (on)", new Vector3(x0, sy - 1.15f, DreamHouse.StreetZ0), new Vector3(x1, sy - 0.15f, DreamHouse.FrontZ0 - 3f), TopOnly(asphalt, kerb));
                Solid("Rowan Street's footpath (on)", new Vector3(x0, sy - 1f, DreamHouse.FrontZ0 - 3f), new Vector3(x1, sy, DreamHouse.FrontZ0 - 0.25f), TopOnly(paving, kerb));
            }
            Solid("Rowan Street's far footpath", new Vector3(-150f, sy - 1.2f, DreamHouse.StreetZ0 - 3f), new Vector3(150f, sy, DreamHouse.StreetZ0), TopOnly(paving, kerb));
            b.Build("Chapel Row and the streets (stand-in)", stand);
            kit.Build("Railings and puddles", stand, false);

            // ---- The dream's door: the Lasnames' front door in a piece of their front wall, open, light round its edges.
            DreamsDoor(stand, colliders, ex, ty);

            // ---- Houses: Chapel Row's north side (the lane between them), School Lane above, Rowan Street's neighbours.
            var houses = new GameObject("Houses (stand-in)").transform;
            houses.SetParent(stand, false);
            System.Func<float, float, float> ground = (x, z) => Ground(ty, x, z);
            Row(houses, colliders, ty, -Lane - 2.4f, -1, FrontsZ, 0f, 30f, 11, ground, ex.x);
            Row(houses, colliders, ty, Lane + 2.4f, 1, FrontsZ, 0f, 30f, 23, ground, ex.x);
            Row(houses, colliders, ty + Terrace, -2f, -1, SchoolWallZ + 16f, 0f, -1f, 37, ground, ex.x);
            Row(houses, colliders, ty + Terrace, 2f, 1, SchoolWallZ + 16f, 0f, -1f, 41, ground, ex.x);
            // Rowan Street: the neighbours either side of the lot, and the far side (facing north).
            Row(houses, colliders, 0f, DreamHouse.YardX0 - 3f, -1, 2f, 0f, 22f, 53, ground, 0f, 70f);
            Row(houses, colliders, 0f, DreamHouse.YardX1 + 3f, 1, 2f, 0f, 22f, 59, ground, 0f, 70f);
            Row(houses, colliders, sy, -2f, -1, DreamHouse.StreetZ0 - 5f, 180f, 26f, 67, ground, 0f, 90f);
            Row(houses, colliders, sy, 2f, 1, DreamHouse.StreetZ0 - 5f, 180f, 26f, 71, ground, 0f, 90f);

            // ---- Street lamps (lit), trees, the garden trees by the house.
            var lamps = new GameObject("Street lamps").transform;
            lamps.SetParent(stand, false);
            foreach (var (x, z) in new[] { (-21f, RoadS - 0.8f), (-9f, RoadS - 0.8f), (9f, RoadS - 0.8f), (21f, RoadS - 0.8f), (-15f, RoadN + 0.9f), (-3.2f, RoadN + 0.9f), (3.2f, RoadN + 0.9f), (15f, RoadN + 0.9f), (-2.3f, 58f), (2.3f, 66f) })
                StreetLamp(lamps, colliders, new Vector3(x, z > PavementN && Mathf.Abs(x) > Lane ? Ground(ty, x, z) : ty, z));
            foreach (float x in new[] { -14f, -2.5f, 14f })
                StreetLamp(lamps, colliders, new Vector3(x, sy, DreamHouse.StreetZ0 - 1.5f));
            var trees = new GameObject("Trees (stand-in)").transform;
            trees.SetParent(stand, false);
            foreach (var (x, z, seed) in new[] { (-30f, 28.5f, 4), (31f, 28.5f, 9), (-24f, 45.5f, 6), (25f, 45.6f, 13), (-11f, -3.4f, 2), (15.5f, -3.4f, 5), (-45f, 10f, 17), (52f, 14f, 21) })
            {
                float gy = z > 24.6f && z < PavementN ? ty : Ground(ty, x, z);
                var t = VhLookTrees.Place(trees, new Vector3(x, gy, z), seed, z > 24f ? 9f : 8.5f);
                if (t != null) t.name = z < 0f ? "Front garden tree" : "Street tree";
            }
            VhLookTrees.Scatter(trees, new Vector2(-240f, -180f), new Vector2(240f, 330f), 14, 31, ground,
                p => !(p.z > -40f && p.z < 100f && Mathf.Abs(p.x) < 75f), 11f);

            // ---- The ground all round (Look's terrain, lawns by the houses) and the far ring.
            var origin = new Vector3(-260f, -40f, -200f);
            var size = new Vector3(520f, 120f, 560f);
            var terrain = VhLookGround.BuildTerrain(stand, new VhLookGround.TerrainOptions
            {
                origin = root.TransformPoint(origin),
                size = size,
                height = (x, z) => Ground(ty, x - root.position.x, z - root.position.z) + root.position.y,
                lawn = (x, z) => Mathf.Abs(x - root.position.x) < 70f && z - root.position.z > -40f && z - root.position.z < 100f ? 1f : 0.25f,
                assetPath = HouseEscapeLook.Dir + "/StandIn/HillStandIn_Terrain.asset",
                name = "The hill's ground (stand-in)",
                seed = 5,
            });
            if (terrain != null) terrain.gameObject.AddComponent<MovementSurface>().runnable = false;
            VhLookBackdrop.Build(stand, new VhLookBackdrop.Options
            {
                center = root.TransformPoint(new Vector3(0f, 0f, 60f)),
                innerRadius = 240f,
                outerRadius = 2800f,
                nearHeight = (x, z) => Ground(ty, x - root.position.x, z - root.position.z) + root.position.y,
                hole = new Rect(root.position.x + origin.x, root.position.z + origin.z, size.x, size.z),
                ridgeHeight = 190f,
                treeClusters = 18,
            });
            return root;
        }

        /// <summary>Look's material, wet: a variant tinted a touch darker and glossier (the rain).</summary>
        static Material Wet(string name, string variant, Color tint, float smoothness)
        {
            var m = VhLookLibrary.Tinted(name, variant, tint);
            if (m == null) return HouseMaterials.Look(name);
            m.SetFloat("_Smoothness", smoothness);
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A row of houses from <paramref name="startX"/> outward (<paramref name="dir"/> -1 west, +1 east), fronts
        /// at <paramref name="frontZ"/> facing the street (yaw 0: south; 180: north), full detail within
        /// <paramref name="fullWithin"/> m of <paramref name="nearX"/>, out to <paramref name="reach"/> m.</summary>
        static void Row(Transform parent, Transform colliders, float y, float startX, int dir, float frontZ, float yaw, float fullWithin, int seed, System.Func<float, float, float> ground, float nearX, float reach = 70f)
        {
            float x = startX;
            int n = 0;
            while (Mathf.Abs(x - startX) < reach && n < 12)
            {
                var s = VhLookHouses.Spec.Random(seed + n * 7);
                float front = s.width, depth = s.depth;
                float x0 = dir > 0 ? x : x - front, x1 = x0 + front;
                bool full = fullWithin > 0f && Mathf.Min(Mathf.Abs(x0 - nearX), Mathf.Abs(x1 - nearX)) < fullWithin;
                // Facing south (yaw 0) the front-left corner is the west end; facing north (yaw 180) the east end.
                var corner = yaw > 90f ? new Vector3(x1, y, frontZ) : new Vector3(x0, y, frontZ);
                var go = VhLookHouses.Build(parent, corner, yaw, s, full ? VhLookHouses.Lod.Full : VhLookHouses.Lod.Far, ground, "House (" + (full ? "full" : "far") + ")");
                if (go != null)
                {
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (!full) r.shadowCastingMode = ShadowCastingMode.Off;
                    float z0 = yaw > 90f ? frontZ - depth : frontZ, z1 = z0 + depth;
                    var c = new GameObject("Collider: a house");
                    c.transform.SetParent(colliders, false);
                    c.transform.localPosition = new Vector3((x0 + x1) * 0.5f, y + 3f, (z0 + z1) * 0.5f);
                    c.AddComponent<BoxCollider>().size = new Vector3(front, 8f, depth);
                    c.AddComponent<MovementSurface>().runnable = false;
                }
                x = dir > 0 ? x1 + 3f : x0 - 3f;
                n++;
            }
        }

        static void StreetLamp(Transform parent, Transform colliders, Vector3 foot)
        {
            var kit = new HouseEscapeKit();
            var iron = HouseEscapeLook.Iron();
            var glass = HouseEscapeLook.Glow("StreetLampGlass", new Color(1f, 0.84f, 0.6f), 3.2f);
            kit.Lathe(foot, Vector3.up, new[] { new Vector2(0.14f, 0f), new Vector2(0.12f, 0.3f), new Vector2(0.06f, 0.42f), new Vector2(0.05f, 3.4f), new Vector2(0.08f, 3.46f) }, iron, 12);
            var head = foot + Vector3.up * 3.46f;
            kit.Lathe(head, Vector3.up, new[] { new Vector2(0.09f, 0f), new Vector2(0.16f, 0.12f), new Vector2(0.17f, 0.42f), new Vector2(0.1f, 0.5f) }, glass, 14, true);
            kit.Lathe(head + Vector3.up * 0.5f, Vector3.up, new[] { new Vector2(0.22f, 0f), new Vector2(0.12f, 0.12f), new Vector2(0.02f, 0.22f) }, iron, 14);
            var go = kit.Build("Street lamp", parent);
            var c = go.AddComponent<BoxCollider>();
            c.center = foot + new Vector3(0f, 1.7f, 0f);
            c.size = new Vector3(0.18f, 3.4f, 0.18f);
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = head + Vector3.up * 0.25f;
            l.type = LightType.Point; l.range = 11f; l.intensity = 1.5f; l.color = new Color(1f, 0.8f, 0.55f); l.shadows = LightShadows.None;
        }

        /// <summary>The dream's door on Chapel Row's pavement: the Lasnames' red front door, open (out toward the street),
        /// in its white casing, set in a piece of their sage clapboard front with its fanlight; warm light round the leaf's
        /// edges; a stone step. She comes out of it running north; through it, looking back, the house and Lena's window.</summary>
        static void DreamsDoor(Transform parent, Transform colliders, Vector3 ex, float ty)
        {
            var b = new MeshBuilder();
            var siding = HouseMaterials.Siding();
            var trim = HouseEscapeLook.Clean("TrimWhite", new Color(0.93f, 0.91f, 0.86f), 0.45f);
            var red = HouseEscapeLook.DoorRed();
            var panel = HouseEscapeLook.Clean("FrontDoorPanel", new Color(0.48f, 0.1f, 0.09f), 0.6f);
            var glow = HouseEscapeLook.Glow("DreamsDoorLight", new Color(1f, 0.88f, 0.66f), 4f);
            const float half = 0.75f, head = 2.45f, wall = 1.9f, top = 3.3f, t = 0.15f;
            float z0 = ex.z - t, z1 = ex.z + t;
            void Solid(string name, Vector3 mn, Vector3 mx, Material m)
            {
                b.Box(mn, mx, m);
                var go = new GameObject("Collider: the dream's door (" + name + ")");
                go.transform.SetParent(colliders, false);
                go.transform.localPosition = (mn + mx) * 0.5f;
                go.AddComponent<BoxCollider>().size = mx - mn;
                go.AddComponent<MovementSurface>().runnable = false;
            }
            Solid("wall, left", new Vector3(ex.x - wall, ty, z0), new Vector3(ex.x - half, ty + top, z1), siding);
            Solid("wall, right", new Vector3(ex.x + half, ty, z0), new Vector3(ex.x + wall, ty + top, z1), siding);
            Solid("over the door", new Vector3(ex.x - half, ty + head, z0), new Vector3(ex.x + half, ty + top, z1), siding);
            b.Box(new Vector3(ex.x - wall - 0.06f, ty + top, z0 - 0.08f), new Vector3(ex.x + wall + 0.06f, ty + top + 0.12f, z1 + 0.08f), trim);
            b.Box(new Vector3(ex.x - wall - 0.06f, ty, z0 - 0.04f), new Vector3(ex.x - wall, ty + top, z1 + 0.04f), trim);
            b.Box(new Vector3(ex.x + wall, ty, z0 - 0.04f), new Vector3(ex.x + wall + 0.06f, ty + top, z1 + 0.04f), trim);
            foreach (float z in new[] { z0 - 0.03f, z1 })
            {
                b.Box(new Vector3(ex.x - half - 0.12f, ty, z), new Vector3(ex.x - half, ty + head + 0.12f, z + 0.03f), trim);
                b.Box(new Vector3(ex.x + half, ty, z), new Vector3(ex.x + half + 0.12f, ty + head + 0.12f, z + 0.03f), trim);
                b.Box(new Vector3(ex.x - half - 0.12f, ty + head, z), new Vector3(ex.x + half + 0.12f, ty + head + 0.12f, z + 0.03f), trim);
            }
            // The light round the opening's edges (the dream behind it).
            b.Box(new Vector3(ex.x - half, ty, z0 + 0.02f), new Vector3(ex.x - half + 0.025f, ty + head, z1 - 0.02f), glow);
            b.Box(new Vector3(ex.x + half - 0.025f, ty, z0 + 0.02f), new Vector3(ex.x + half, ty + head, z1 - 0.02f), glow);
            b.Box(new Vector3(ex.x - half, ty + head - 0.025f, z0 + 0.02f), new Vector3(ex.x + half, ty + head, z1 - 0.02f), glow);
            // The step.
            b.Box(new Vector3(ex.x - half - 0.2f, ty - 0.1f, z0 - 0.35f), new Vector3(ex.x + half + 0.2f, ty + 0.002f, z1 + 0.35f), HouseMaterials.Foundation());
            b.Build("The dream's door", parent);
            // The leaf, open outward on its hinge at the left jamb (out of her way: she runs out at x 0).
            var hinge = new GameObject("The dream's door (leaf, open)").transform;
            hinge.SetParent(parent, false);
            hinge.localPosition = new Vector3(ex.x - half, ty, z1);
            hinge.localRotation = Quaternion.Euler(0f, -100f, 0f);
            var lb = new MeshBuilder();
            lb.Box(new Vector3(0f, 0f, 0f), new Vector3(half * 2f - 0.04f, head - 0.02f, 0.05f), red);
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < 2; i++)
                {
                    float x0 = 0.12f + i * (half - 0.04f), y0 = 0.15f + j * (head * 0.5f - 0.05f);
                    foreach (float z in new[] { -0.012f, 0.05f })
                        lb.Box(new Vector3(x0, y0, z), new Vector3(x0 + half - 0.2f, y0 + head * 0.5f - 0.3f, z + 0.012f), panel);
                }
            var leafGo = lb.Build("Leaf", hinge);
            var lc = leafGo.AddComponent<BoxCollider>();
            lc.center = new Vector3(half - 0.02f, head * 0.5f, 0.025f);
            lc.size = new Vector3(half * 2f - 0.04f, head, 0.06f);
            leafGo.AddComponent<MovementSurface>().runnable = false;
            var knob = new HouseEscapeKit();
            knob.Ball(new Vector3(half * 2f - 0.16f, 1.0f, -0.04f), 0.035f, HouseEscapeLook.Brass(), 8);
            knob.Ball(new Vector3(half * 2f - 0.16f, 1.0f, 0.09f), 0.035f, HouseEscapeLook.Brass(), 8);
            knob.Build("Knobs", hinge, false);
            var light = new GameObject("Lamp: the dream's door").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = new Vector3(ex.x, ty + 1.6f, ex.z + 0.8f);
            light.type = LightType.Point; light.range = 6f; light.intensity = 1.4f; light.color = new Color(1f, 0.86f, 0.64f); light.shadows = LightShadows.None;
        }
    }
}
