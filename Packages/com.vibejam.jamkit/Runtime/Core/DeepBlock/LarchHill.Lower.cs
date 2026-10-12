using System;
using System.Numerics;

namespace JamKit.DeepBlock
{
    /// <summary>The lower band: Mill Street, Larch Green, Row B and Back Lane B (the first test), the old oak, Rowan Street's
    /// block party (the Rewind) and the Lasnames' lot. docs/redesign/world.md "The lower band".</summary>
    public sealed partial class LarchHill
    {
        /// <summary>Mill Street's fence over the yards (2.6 m: the yards' tallest things stay under 2.6 m near it).</summary>
        public const float MillFence = 2.6f;
        /// <summary>The hedge along Rowan Street's edge over Back Lane B (2.75 m: no garage roof reaches its top).</summary>
        public const float RowanHedge = 2.75f, RowanHedgeZ1 = 101.5f;
        const float MillFoot = MillY + 0.15f, RowBGround = 4.6f, RowanFoot = RowanY + 0.15f, RowanGround = HillAnchors.LotGroundY;
        static readonly string[] Sidings = { "siding_white", "siding_blue", "siding_sage", "siding_butter", "siding_rose", "siding_lilac", "brick_red", "brick_cream", "stucco", "stucco_warm", "shingle_wall" };
        static readonly string[] Roofs = { "roof_shingle_grey", "roof_shingle_red", "roof_shingle_green", "roof_slate", "roof_clay" };
        static readonly string[] Doors = { "door_red", "door_blue", "door_green", "door_yellow" };

        /// <summary>A house of the hill's ordinary kind with its looks picked from <paramref name="seed"/>.</summary>
        HillHouse Ordinary(string name, string lot, float cx, float floorY, float cz, float width, float depth, HillFacing facing, int seed, float plinth = 0.45f)
        {
            var r = new Random(seed * 7919 + 13);
            var roof = (HillRoof)new[] { 0, 1, 1, 2, 2, 3, 1 }[r.Next(7)];
            return House(new HillHouse
            {
                name = name, lot = lot, center = new Vector3(cx, floorY, cz), width = width, depth = depth, facing = facing,
                storeys = new[] { 1.5f, 2f, 2f, 2f, 2.5f }[r.Next(5)], roof = roof, pitch = 28f + r.Next(16), overhang = 0.35f + (float)r.NextDouble() * 0.25f,
                plinth = plinth, wall = Sidings[r.Next(Sidings.Length)], roofMat = Roofs[r.Next(Roofs.Length)], trim = r.Next(4) == 0 ? "trim_cream" : "trim_white",
                door = Doors[r.Next(Doors.Length)], porch = r.Next(5) != 0 && (seed % 9) != 0, porchRoof = true, bay = r.Next(3) == 0, chimney = r.Next(3) != 0,
                shutters = r.Next(3) == 0, dormers = roof == HillRoof.GableSide && r.Next(2) == 0 ? 1 + r.Next(2) : 0, turret = (seed % 9) == 0 ? (r.Next(2) == 0 ? -1 : 1) : 0,
                seed = seed, litShare = 0.15f + (float)r.NextDouble() * 0.2f,
            });
        }

        /// <summary>A few garden things for a back garden between <paramref name="z0"/> and <paramref name="z1"/>.</summary>
        void GardenThings(float x0, float x1, float z0, float z1, float y, int seed)
        {
            var r = new Random(seed);
            HillPropKind[] kinds = { HillPropKind.Shed, HillPropKind.WashingLine, HillPropKind.Trampoline, HillPropKind.Grill, HillPropKind.PicnicTable,
                HillPropKind.Swing, HillPropKind.Greenhouse, HillPropKind.FlowerBed, HillPropKind.Bush, HillPropKind.BirdBath, HillPropKind.Kennel, HillPropKind.Wheelbarrow };
            int n = 2 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                var k = kinds[r.Next(kinds.Length)];
                var s = HillProp.SizeOf(k);
                float m = Math.Max(s.X, s.Z) * 0.5f + 0.6f;
                if (x1 - x0 < 2f * m + 1f || z1 - z0 < 2f * m + 1f) continue;
                float x = x0 + m + (float)r.NextDouble() * (x1 - x0 - 2f * m);
                float z = z0 + m + (float)r.NextDouble() * (z1 - z0 - 2f * m);
                Prop(k, x, y, z, r.Next(4) * 90f, r.Next(8));
            }
            if (r.Next(3) != 0) Tree("Garden tree", x0 + 2f + (float)r.NextDouble() * Math.Max(0.5f, x1 - x0 - 4f), y, (z0 + z1) * 0.5f, r.Next(3), 6.5f + (float)r.NextDouble() * 3.5f, 3f, seed + 1);
        }

        // ---------- Mill Street ----------

        void MakeMillStreet()
        {
            _layer = "mill";
            // The south footpath is the top of the wall over the Row A yards (4.15 m): a tall fence along it, with a gap at
            // the slot (the chimney's way out) and the Mill Steps.
            Ground("Mill Street (south footpath)", WestX, MillWallZ0, EastX, MillRoadZ0, MillFoot, "paving");
            FenceX("Mill Street's fence", "railing_tall", WestX, -5f, MillWallZ0 + 0.35f, MillFoot, MillFence, 0.1f, runnable: false);
            FenceX("Mill Street's fence", "railing_tall", 5f, A3X0, MillWallZ0 + 0.35f, MillFoot, MillFence, 0.1f, runnable: false);
            FenceX("Mill Street's fence", "railing_tall", SlotX0, EastX, MillWallZ0 + 0.35f, MillFoot, MillFence, 0.1f, runnable: false);
            Ground("Mill Street", WestX, MillRoadZ0, EastX, MillRoadZ1, MillY, "asphalt", "kerb");
            Ground("Mill Street (north footpath)", WestX, MillRoadZ1, EastX, RowBZ0, MillFoot, "paving", "kerb");
            foreach (float x in new[] { -120f, -96f, -70f, -44f, -26f, 30f, 52f, 74f, 100f, 124f })
                Prop(HillPropKind.Car, x, MillY, 45.3f, 90f, (int)Math.Abs(x) % 7, "mill");
            foreach (float x in new[] { -108f, -82f, -58f, -34f, 40f, 64f, 88f, 114f })
                Prop(HillPropKind.Car, x, MillY, 50.7f, 270f, (int)Math.Abs(x) % 5 + 2, "mill");
            for (float x = -130f; x < 135f; x += 30f)
            {
                Prop(HillPropKind.Streetlight, x, MillFoot, 43.3f, 180f);
                Prop(HillPropKind.PowerPole, x + 15f, MillFoot, 53.4f, 0f);
            }
            Prop(HillPropKind.StreetSign, 15.5f, MillFoot, 43.4f, 0f, 12, "mill street");
            Prop(HillPropKind.Mailbox, 22f, MillFoot, 53.3f, 180f);
            Prop(HillPropKind.Hydrant, -18f, MillFoot, 43.4f, 0f);
            Prop(HillPropKind.Hydrant, 60f, MillFoot, 53.4f, 0f);
        }

        // ---------- Larch Green ----------

        float GreenY(float z) => MillFoot + (LaneY - MillFoot) * Math.Max(0f, Math.Min(1f, (z - RowBZ0) / (LaneZ1 - RowBZ0)));

        void MakeGreen()
        {
            _layer = "green";
            RampZ("Larch Green (west lawn)", GreenX0, RowBZ0, -1.2f, LaneZ1, MillFoot, LaneY, "grass");
            RampZ("Larch Green (the path)", -1.2f, RowBZ0, 1.2f, LaneZ1, MillFoot, LaneY, "paving");
            RampZ("Larch Green (east lawn)", 1.2f, RowBZ0, GreenX1, LaneZ1, MillFoot, LaneY, "grass");
            Prop(HillPropKind.Bench, -2.4f, GreenY(64f), 64f, 90f);
            Prop(HillPropKind.Bench, 2.4f, GreenY(78f), 78f, 270f);
            Prop(HillPropKind.Swing, -4.6f, GreenY(70f), 70f, 90f);
            Prop(HillPropKind.Slide, 4.8f, GreenY(66f), 66f, 0f);
            Prop(HillPropKind.Fountain, 0f, GreenY(58.5f), 58.5f, 0f);
            Prop(HillPropKind.FlowerBed, -5f, GreenY(86f), 86f, 0f);
            Prop(HillPropKind.FlowerBed, 5f, GreenY(86f), 86f, 0f);
            Tree("The green's lime", -5.2f, GreenY(78f), 78f, 1, 11f, 3.8f, 41);
            Tree("The green's cherry", 5.2f, GreenY(58f), 58f, 2, 6f, 3f, 43);
            FenceZ("The green's west fence", "board", GreenX0 - 0.06f, RowBZ0, 84f, RowBGround, 1.8f);
            FenceZ("The green's west fence (low)", "picket", GreenX0 - 0.06f, 84f, LaneZ0, RowBGround, 1.0f, 0.08f, runnable: false);
            FenceZ("The green's east fence", "board", GreenX1 + 0.06f, RowBZ0, 84f, RowBGround, 1.8f);
            FenceZ("The green's east fence (low)", "picket", GreenX1 + 0.06f, 84f, LaneZ1, RowBGround, 1.0f, 0.08f, runnable: false);
        }

        // ---------- Row B (Mill Street's north side) ----------

        /// <summary>One Row B lot: front garden, house, back garden (to the garage strip on the lane in the west, to Rowan
        /// Street's wall in the east), its fences.</summary>
        void RowBLot(string name, float x0, float x1, int seed, bool laneStrip, bool fenceEast = true)
        {
            Ground(name + " (front garden)", x0, RowBZ0, x1, 58f, RowBGround, "grass", "kerb");
            Ground(name + " (beside the house)", x0, 58f, x1, 70f, RowBGround, "paving");
            float gardenTo = laneStrip ? 88f : LaneZ1;
            Ground(name + " (back garden)", x0, 70f, x1, gardenTo, RowBGround, "grass");
            float w = Math.Min(12f, x1 - x0 - 4f);
            Ordinary(name, "row b", (x0 + x1) * 0.5f, RowBGround + 0.45f, 64f, w, 11.5f, HillFacing.South, seed);
            float cx = (x0 + x1) * 0.5f;
            FenceX(name + " (front fence)", "picket", x0, cx - 0.9f, RowBZ0 + 0.3f, RowBGround, 1.0f, 0.08f, runnable: false);
            FenceX(name + " (front fence)", "picket", cx + 0.9f, x1, RowBZ0 + 0.3f, RowBGround, 1.0f, 0.08f, runnable: false);
            if (fenceEast)
            {
                FenceZ(name + " (side fence)", "board", x1, 58f, laneStrip ? 88f : 86f, RowBGround, 1.8f);
                if (!laneStrip) FenceZ(name + " (side fence, low by the wall)", "picket", x1, 86f, LaneZ1, RowBGround, 1.0f, 0.08f, runnable: false);
            }
            GardenThings(x0 + 0.8f, x1 - 0.8f, 71f, laneStrip ? 87f : 84f, RowBGround, seed + 77);
        }

        void MakeRowB()
        {
            _layer = "rowb";
            // West of the green: lots with garages on Back Lane B.
            RowBLot("Row B, the Mirandas'", -28f, GreenX0, 61, true, fenceEast: false);
            RowBLot("Row B, the Novaks'", -46f, -28f, 62, true);
            RowBLot("Row B, the Bells'", -58f, -46f, 63, true, fenceEast: false);
            RowBLot("Row B, the Adeyemis'", -80f, DitchX0, 64, true, fenceEast: false);
            RowBLot("Row B, the Castellanos'", -112f, -90f, 66, true);
            RowBLot("Row B, the corner", WestX, -112f, 67, false, fenceEast: false);
            FenceZ("The lane's end wall", "brick_low", -112.15f, LaneZ0, LaneZ1, LaneY, 2.2f, 0.3f);
            // East of the green: one house, the allotments (low: the arrival sees the house over them), then houses.
            RowBLot("Row B, the Whitfields'", GreenX1, 24f, 71, false);
            Ground("The allotments (by the street)", 24f, RowBZ0, 72f, 58f, RowBGround, "grass", "kerb");
            Ground("The allotments", 24f, 58f, 72f, LaneZ1, RowBGround, "dirt");
            FenceX("The allotments' fence", "chainlink", 24f, 46f, RowBZ0 + 0.3f, RowBGround, 1.2f, 0.06f, runnable: false);
            FenceX("The allotments' fence", "chainlink", 48f, 72f, RowBZ0 + 0.3f, RowBGround, 1.2f, 0.06f, runnable: false);
            FenceZ("The allotments' fence (east)", "chainlink", 72f, 58f, LaneZ1, RowBGround, 1.2f, 0.06f, runnable: false);
            var r = new Random(5);
            for (float x = 27f; x < 70f; x += 7f)
                for (float z = 62f; z < 80f; z += 6.5f)
                {
                    int k = r.Next(6);
                    if (k == 0) Prop(HillPropKind.Shed, x + 1.5f, RowBGround, z + 1f, r.Next(4) * 90f, r.Next(6));
                    else if (k == 1) Prop(HillPropKind.Greenhouse, x + 1.6f, RowBGround, z + 1.6f, 0f);
                    else Prop(HillPropKind.FlowerBed, x + 1.4f, RowBGround, z + 1.5f, 90f * r.Next(2), r.Next(6));
                }
            Prop(HillPropKind.Wheelbarrow, 44f, RowBGround, 80f, 30f);
            Prop(HillPropKind.Barrel, 30f, RowBGround, 60.5f, 0f);
            RowBLot("Row B, the Okonkwos'", 72f, 90f, 72, false);
            RowBLot("Row B, the Fairbanks'", 90f, 108f, 73, false);
            RowBLot("Row B, the Ruizes'", 108f, 124f, 74, false);
            RowBLot("Row B, the Ostranders'", 124f, EastX, 75, false, fenceEast: false);
        }

        // ---------- Back Lane B (west of the green): the first test ----------

        /// <summary>The ditch (a storm channel) across the lane and the gardens: double jump it on the roofs, or wall-run Rowan
        /// Street's wall over it in the lane. Its floor is 4.4 m down; the steps out are at its south end.</summary>
        public const float DitchX0 = -66f, DitchX1 = -58f, DitchFloor = 0.6f;
        /// <summary>The garage roofs (the lane's high line) and the workshop's taller roof.</summary>
        public const float GarageRoof = LaneY + 2.6f, WorkshopRoof = LaneY + 2.6f;
        /// <summary>The Old Mill's arch over the lane (its shutter at its east face, on the loop's timer).</summary>
        public const float MillX0 = -90f, MillX1 = -80f, ArchTop = LaneY + 3.4f;
        /// <summary>The sunken loading bay (wall-run over it), the scaffold's bar (slide), the old oak (Climb 2).</summary>
        public const float BayX0 = -100.4f, BayX1 = -92f, BayFloor = 2f, ScaffoldX = -102f, OakX = -108f, OakZ = 97.5f;

        void MakeBackLane()
        {
            _layer = "lane";
            // The lane itself, broken by the ditch and the loading bay; under the Old Mill's arch.
            Ground("Back Lane B", DitchX1, LaneZ0, GreenX0, LaneZ1, LaneY, "cobbles");
            Ground("Back Lane B (west of the ditch)", MillX1, LaneZ0, DitchX0, LaneZ1, LaneY, "cobbles");
            Ground("Back Lane B (under the Old Mill)", MillX0, LaneZ0, MillX1, LaneZ1, LaneY, "cobbles");
            Ground("Back Lane B (past the Old Mill)", BayX1, LaneZ0, MillX0, LaneZ1, LaneY, "cobbles");
            Ground("Back Lane B (the far end)", -112f, LaneZ0, BayX0, LaneZ1, LaneY, "cobbles");
            // The garage strip behind the gardens (Z 88-94): the lock-ups stand on it.
            Ground("The garage strip (the Mirandas')", -28f, 88f, GreenX0, LaneZ0, LaneY, "concrete");
            Ground("The garage strip (the Novaks')", -46f, 88f, -28f, LaneZ0, LaneY, "concrete");
            Ground("The garage strip (the Bells')", DitchX1, 88f, -46f, LaneZ0, LaneY, "concrete");
            Ground("The garage strip (the Adeyemis')", MillX1, 88f, DitchX0, LaneZ0, LaneY, "concrete");
            Ground("The garage strip (the Castellanos')", -112f, 88f, BayX0, LaneZ0, LaneY, "concrete");
            Ground("The garage strip (by the bay)", BayX1, 88f, MillX0, LaneZ0, LaneY, "concrete");
            Building(new HillBuilding { kind = HillBuildingKind.Garage, name = "The Mirandas' lock-ups", min = new Vector2(-28f, 88f), max = new Vector2(-16f, LaneZ0), floorY = LaneY, height = GarageRoof - LaneY, facing = HillFacing.North, wall = "brick_cream", seed = 81 });
            Building(new HillBuilding { kind = HillBuildingKind.Garage, name = "The Novaks' garages", min = new Vector2(-43f, 88f), max = new Vector2(-31f, LaneZ0), floorY = LaneY, height = GarageRoof - LaneY, facing = HillFacing.North, wall = "brick_red", seed = 82 });
            Building(new HillBuilding { kind = HillBuildingKind.Garage, name = "Mr. Bell's workshop", min = new Vector2(DitchX1, 88f), max = new Vector2(-48f, LaneZ0), floorY = LaneY, height = WorkshopRoof - LaneY, facing = HillFacing.North, wall = "corrugated", seed = 83 });
            Building(new HillBuilding { kind = HillBuildingKind.Garage, name = "The Adeyemis' garages", min = new Vector2(DitchX0, 88f), max = new Vector2(MillX1, LaneZ0), floorY = LaneY, height = GarageRoof - LaneY, facing = HillFacing.North, wall = "stucco", seed = 84 });
            // Crates at the start (up onto the roofs) and in each gap between the garages (back up after a fall).
            Prop(HillPropKind.Crates, -15.3f, LaneY, 93.2f, 0f, 0, "up onto the roofs");
            Prop(HillPropKind.Crates, -29.5f, LaneY, 93.2f, 0f, 1, "back up");
            Prop(HillPropKind.Crates, -44.6f, LaneY, 93.2f, 0f, 2, "back up");
            Prop(HillPropKind.Wheelie, -10f, LaneY, 94.6f, 0f);
            Prop(HillPropKind.Wheelie, -11f, LaneY, 94.6f, 0f, 1);
            Prop(HillPropKind.Bin, -36f, LaneY, 99.4f, 0f);

            // The ditch: a stone storm channel through the gardens and across the lane; steps up at its south end.
            Ground("The ditch", DitchX0, 78f, DitchX1, LaneZ1, DitchFloor, "creek_bed");
            RampZ("The ditch's steps", DitchX0, 70f, DitchX1, 78f, RowBGround, DitchFloor, "steps");
            Ground("The ditch path (to Mill Street)", DitchX0, RowBZ0, DitchX1, 70f, RowBGround, "paving", "kerb");
            Box("The ditch (water)", BlockKind.Water, DitchX0, 78f, DitchX1, LaneZ1, DitchFloor, DitchFloor + 0.25f, solid: false);
            FenceZ("The ditch's railing (east)", "railing", DitchX1 - 0.05f, 72f, 88f, RowBGround, 1.1f, 0.08f, runnable: false);
            FenceZ("The ditch's railing (west)", "railing", DitchX0 + 0.05f, 72f, 88f, RowBGround, 1.1f, 0.08f, runnable: false);
            FenceZ("The Bells' gate (from the ditch path)", "board", DitchX1 + 0.06f, 58f, 70f, RowBGround, 1.8f);

            // The Old Mill (three storeys of brick over the whole lot, its arch over the lane) and its shutter.
            House(new HillHouse
            {
                name = "The Old Mill", lot = "row b", center = new Vector3((MillX0 + MillX1) * 0.5f, RowBGround, 76f), width = MillX1 - MillX0, depth = 36f,
                facing = HillFacing.South, storeys = 3f, storeyHeight = 3.4f, roof = HillRoof.Flat, plinth = 0f, porch = false, chimney = false,
                wall = "brick_dark", roofMat = "roof_flat", trim = "trim_cream", door = "door_green", seed = 90, litShare = 0.1f,
            });
            Ground("The Old Mill (its yard)", MillX0, RowBZ0, MillX1, 58f, RowBGround, "cobbles", "kerb");
            Ground("The Old Mill (under it)", MillX0, 58f, MillX1, 94f, RowBGround, "concrete");
            Box("The Old Mill (over the lane)", BlockKind.House, MillX0, LaneZ0, MillX1, LaneZ1, ArchTop, RowBGround + 3f * 3.4f + 0.3f, style: "mill_bridge");
            var shutter = Box("The Old Mill's shutter", BlockKind.Door, MillX1 - 0.15f, LaneZ0, MillX1 + 0.15f, LaneZ1, LaneY, ArchTop, runnable: false, style: "shutter");
            shutter.switchIndex = ShutterSwitch;
            shutter.presentWhenOpen = false;

            // Past the mill: the sunken loading bay (its steps on its east side), the scaffold's bar, the old oak.
            Ground("The loading bay", BayX0, 88f, -96f, LaneZ0, BayFloor, "concrete");
            Ground("The loading bay (under the lane)", BayX0, LaneZ0, BayX1, LaneZ1, BayFloor, "concrete");
            RampX("The loading bay's steps", -96f, 88f, BayX1, LaneZ0, BayFloor, LaneY, "steps");
            Prop(HillPropKind.Pallets, -98f, BayFloor, 90f, 0f);
            Prop(HillPropKind.Barrel, -97.6f, BayFloor, 91.6f, 0f);
            Box("The scaffold's bar", BlockKind.Plank, ScaffoldX - 0.2f, LaneZ0, ScaffoldX + 0.2f, LaneZ1, LaneY + 1.15f, LaneY + 1.6f, runnable: false, style: "scaffold_bar");
            Box("The scaffold", BlockKind.Scaffold, ScaffoldX - 0.6f, LaneZ0, ScaffoldX + 0.6f, LaneZ1, LaneY + 1.6f, LaneY + 2.6f, runnable: false, style: "scaffold");
            var oak = Tree("The old oak", OakX, LaneY, OakZ, 4, 16f, 7f, 97);
            Box("The old oak (low limb)", BlockKind.Tree, -106.5f, 98.6f, -103.5f, 99.6f, 6.15f, 6.5f, style: "limb");
            Box("The old oak (middle limb)", BlockKind.Tree, -109.5f, 98.6f, -106.5f, 99.6f, 7.55f, 7.9f, style: "limb");
            Box("The old oak (limb over the wall)", BlockKind.Tree, -112f, 98.6f, -109.5f, LaneZ1, 9.05f, 9.4f, style: "limb");
            // The harder climb, round the trunk, to the treehouse (a memory).
            Box("The old oak (high limb)", BlockKind.Tree, -109.3f, 95.2f, -106.3f, 96.2f, 10.45f, 10.8f, style: "limb");
            Box("The old oak (top limb)", BlockKind.Tree, -111.8f, 95f, -109.3f, 96f, 11.85f, 12.2f, style: "limb");
            Box("The treehouse (floor)", BlockKind.Deck, -114.5f, 93.4f, -111.8f, 97.4f, 12.3f, 12.6f, style: "treehouse_floor");
            Box("The treehouse (back wall)", BlockKind.Plank, -114.5f, 93.4f, -114.3f, 97.4f, 12.6f, 14.4f, style: "treehouse_wall");
            Box("The treehouse (side wall)", BlockKind.Plank, -114.5f, 93.4f, -111.8f, 93.6f, 12.6f, 14.4f, style: "treehouse_wall");
            Box("The treehouse (roof)", BlockKind.Roof, -114.7f, 93.2f, -111.6f, 97.6f, 14.4f, 14.7f, style: "treehouse_roof");
            _ = oak;
        }

        // ---------- Rowan Street: the block party, the Rewind ----------

        /// <summary>The Rewind's obstacles across Rowan Street (each spans the street; each lot line beside it has a tall fence
        /// so the gardens aren't a way round): the gazebo's curtain (slide), the hay bales (vault), the trench by the removal
        /// truck (wall-run the truck), the car and stall (mantle), the trench across the street (jump), the stage.</summary>
        public const float GazeboX = -98f, BalesX = -80.5f, TruckX0 = -72f, TruckX1 = -60f, TrenchX0 = -71f, TrenchX1 = -61f,
            CarX = -44f, TruckHeight = 3.4f, CrossTrenchX0 = -30f, CrossTrenchX1 = -23f, StageX0 = -14f, StageX1 = -10f, TrenchFloor = 6.5f;

        void MakeRowan()
        {
            _layer = "rowan";
            // The south footpath: the top of the wall over Back Lane B and the gardens (its railing, a gap where the oak's
            // limb reaches over); cut by the trench by the truck and the trench across the street.
            // The strip along the wall's top: a tall hedge over Back Lane B (no garage roof or scaffold reaches its top; the
            // old oak's limb comes through a gap in it), a railing over the green's east half and the gardens.
            Ground("Rowan Street (the wall's top)", WestX, LaneZ1, EastX, RowanHedgeZ1, RowanFoot, "paving");
            Box("Rowan Street's railing", BlockKind.Fence, WestX, LaneZ1, -112f, LaneZ1 + 0.12f, RowanFoot, RowanFoot + RowanHedge, runnable: false, style: "railing_tall");
            Box("Rowan Street's railing", BlockKind.Fence, -109f, LaneZ1, 0f, LaneZ1 + 0.12f, RowanFoot, RowanFoot + RowanHedge, runnable: false, style: "railing_tall");
            FenceX("Rowan Street's railing", "railing", 0f, EastX, LaneZ1 + 0.35f, RowanFoot, 1.1f, 0.08f, runnable: false);
            // The footpath, cut by the trench by the truck and the trench across the street.
            Ground("Rowan Street (south footpath)", WestX, RowanHedgeZ1, TrenchX0, RowanRoadZ0, RowanFoot, "paving");
            Ground("Rowan Street (south footpath, by the truck)", TrenchX1, RowanHedgeZ1, CrossTrenchX0, RowanRoadZ0, RowanFoot, "paving");
            Ground("Rowan Street (south footpath, east)", CrossTrenchX1, RowanHedgeZ1, EastX, RowanRoadZ0, RowanFoot, "paving");
            Ground("The trench by the truck", TrenchX0, RowanHedgeZ1, TrenchX1, RowanRoadZ0, TrenchFloor, "dirt");
            Ground("The trench across the street", CrossTrenchX0, RowanHedgeZ1, CrossTrenchX1, RowanNorthZ, TrenchFloor, "dirt");
            Ground("Rowan Street", WestX, RowanRoadZ0, CrossTrenchX0, RowanRoadZ1, RowanY, "asphalt", "kerb");
            Ground("Rowan Street (east)", CrossTrenchX1, RowanRoadZ0, EastX, RowanRoadZ1, RowanY, "asphalt", "kerb");
            Ground("Rowan Street (north footpath)", WestX, RowanRoadZ1, CrossTrenchX0, RowanNorthZ, RowanFoot, "paving", "kerb");
            Ground("Rowan Street (north footpath, east)", CrossTrenchX1, RowanRoadZ1, HillRoadX0, RowanNorthZ, RowanFoot, "paving", "kerb");
            Ground("Rowan Street (north footpath, past Hill Road)", HillRoadX1, RowanRoadZ1, EastX, RowanNorthZ, RowanFoot, "paving", "kerb");

            // The party across the street, west to east.
            Box("The gazebo's curtain", BlockKind.Sheet, GazeboX - 0.25f, RowanHedgeZ1, GazeboX + 0.25f, RowanNorthZ, RowanY + 1.15f, RowanY + 2.6f, runnable: false, style: "valance");
            Box("The gazebo", BlockKind.Roof, GazeboX - 2.6f, RowanHedgeZ1, GazeboX + 2.6f, RowanNorthZ, RowanY + 2.6f, RowanY + 3f, runnable: false, style: "gazebo");
            foreach (float lx in new[] { GazeboX - 2.5f, GazeboX + 2.4f })
                foreach (float lz in new[] { RowanHedgeZ1 + 0.05f, RowanNorthZ - 0.15f })
                    Box("The gazebo's leg", BlockKind.Post, lx, lz, lx + 0.1f, lz + 0.1f, RowanY, RowanY + 2.6f, runnable: false, style: "gazebo_leg");
            Box("The hay bales", BlockKind.Prop, BalesX - 0.7f, RowanHedgeZ1, BalesX + 0.7f, RowanNorthZ, RowanY, RowanY + 1.3f, runnable: false, style: "hay");
            Box("The removal truck", BlockKind.Prop, TruckX0, RowanRoadZ0 + 0.2f, TruckX1, 112f, RowanY, RowanY + TruckHeight, runnable: true, style: "truck");
            Box("The bouncy castle", BlockKind.Prop, TruckX0, 112f, TruckX1 - 2f, RowanNorthZ, RowanY, RowanY + 3.6f, runnable: false, style: "bouncy_castle");
            Prop(HillPropKind.Car, CarX, RowanY, 107.3f, 0f, 5, "across the road");
            Box("The skip", BlockKind.Prop, CarX - 1f, RowanHedgeZ1 + 0.1f, CarX + 1f, RowanRoadZ0 - 0.1f, RowanFoot, RowanFoot + 1.9f, runnable: true, style: "skip");
            Box("The stacked crates", BlockKind.Prop, CarX - 1f, 109.6f, CarX + 1f, RowanRoadZ1 - 0.05f, RowanY, RowanY + 1.6f, runnable: false, style: "crates");
            Box("The lemonade stall", BlockKind.Prop, CarX - 1f, RowanRoadZ1 + 0.05f, CarX + 1f, RowanNorthZ, RowanFoot, RowanFoot + 2.4f, runnable: true, style: "stall");
            Box("The stage", BlockKind.Deck, StageX0, RowanHedgeZ1, StageX1, RowanNorthZ, RowanY, RowanY + 1.1f, runnable: false, style: "stage");
            // Tables, chairs and the band's things along the street between the obstacles (the line runs by them).
            foreach (float x in new[] { -92f, -86f, -54f, -38f, -18f })
            {
                Prop(HillPropKind.PicnicTable, x, RowanY, 109f, 90f, 0, "party");
                Prop(HillPropKind.YardSaleTable, x - 2.5f, RowanFoot, 115f, 0f, (int)-x % 5, "party");
            }
            Prop(HillPropKind.Grill, -50f, RowanY, 111.5f, 0f, 1, "party grill");
            for (float x = -104f; x < 100f; x += 34f) Prop(HillPropKind.Streetlight, x, RowanFoot, 101.3f, 0f);

            // The lots west of the Larch Steps (each line between them a tall fence: the gardens are no way round).
            var lotLines = new[] { -116f, -98f, -80f, -62f, -44f, -26f, -8f };
            Building(new HillBuilding { kind = HillBuildingKind.CornerShop, name = "The Rowan corner shop", min = new Vector2(-138f, 118f), max = new Vector2(-118f, 134f), floorY = RowanGround, height = 7f, facing = HillFacing.South, seed = 21 });
            Ground("The corner shop's forecourt", WestX, RowanNorthZ, -116f, 118f, RowanGround, "paving", "kerb");
            Ground("The corner shop (under it)", WestX, 118f, -116f, 146f, RowanGround, "concrete");
            for (int i = 0; i + 1 < lotLines.Length; i++)
            {
                float x0 = lotLines[i], x1 = lotLines[i + 1];
                string name = "Rowan Street, number " + (2 + i * 2);
                Ground(name + " (front garden)", x0, RowanNorthZ, x1, 122f, RowanGround, "grass", "kerb");
                Ground(name + " (house and back garden)", x0, 122f, x1, 146f, RowanGround, "grass");
                Ordinary(name, "rowan west", (x0 + x1) * 0.5f, RowanGround + 0.4f, 128.5f, 12f, 11f, HillFacing.South, 120 + i, 0.4f);
                float cx = (x0 + x1) * 0.5f;
                FenceX(name + " (front fence)", "picket", x0, cx - 0.9f, RowanNorthZ + 0.3f, RowanGround, 1.0f, 0.08f, runnable: false);
                FenceX(name + " (front fence)", "picket", cx + 0.9f, x1, RowanNorthZ + 0.3f, RowanGround, 1.0f, 0.08f, runnable: false);
                FenceZ(name + " (tall side fence)", "board", x0 + 0.06f, RowanNorthZ, 146f, RowanGround, 2.6f, 0.12f);
                GardenThings(x0 + 0.8f, x1 - 0.8f, 135f, 145f, RowanGround, 140 + i);
            }
            FenceZ("Rowan Street, number 12 (tall side fence)", "board", -8f - 0.06f, RowanNorthZ, 146f, RowanGround, 2.6f, 0.12f);

            // East of the Lasnames' (X 42 to Hill Road) and past Hill Road.
            float[] east = { 42f, 59.5f, 77f, 94.5f, HillWalkX0 };
            for (int i = 0; i + 1 < east.Length; i++)
            {
                float x0 = east[i], x1 = east[i + 1];
                string name = "Rowan Street, number " + (21 + i * 2);
                Ground(name + " (front garden)", x0, RowanNorthZ, x1, 122f, RowanGround, "grass", "kerb");
                Ground(name + " (house and back garden)", x0, 122f, x1, 146f, RowanGround, "grass");
                Ordinary(name, "rowan east", (x0 + x1) * 0.5f, RowanGround + 0.4f, 128.5f, 11.5f, 11f, HillFacing.South, 160 + i, 0.4f);
                float cx = (x0 + x1) * 0.5f;
                FenceX(name + " (front fence)", "picket", x0, cx - 0.9f, RowanNorthZ + 0.3f, RowanGround, 1.0f, 0.08f, runnable: false);
                FenceX(name + " (front fence)", "picket", cx + 0.9f, x1, RowanNorthZ + 0.3f, RowanGround, 1.0f, 0.08f, runnable: false);
                FenceZ(name + " (side fence)", "board", x1 - 0.06f, RowanNorthZ, 146f, RowanGround, 1.8f);
                GardenThings(x0 + 0.8f, x1 - 0.8f, 135f, 145f, RowanGround, 170 + i);
            }
            Ground("Rowan Street, number 31 (front garden)", HillRoadX1, RowanNorthZ, EastX, 122f, RowanGround, "grass", "kerb");
            Ground("Rowan Street, number 31", HillRoadX1, 122f, EastX, 146f, RowanGround, "grass");
            Ordinary("Rowan Street, number 31", "rowan east", 131f, RowanGround + 0.4f, 128.5f, 11f, 11f, HillFacing.South, 168, 0.4f);
            Prop(HillPropKind.StreetSign, -9f, RowanFoot, 114.8f, 90f, 13, "rowan street");
        }

        // ---------- The Lasnames' lot ----------

        void MakeLasnames()
        {
            _layer = "lasnames";
            float lx0 = HillAnchors.LotX0, lx1 = HillAnchors.LotX1, y = RowanGround;
            const float hx0 = 16.4f, hx1 = 30f, hz0 = 122f, hz1 = 137.4f;
            Ground("The Lasnames' front garden", lx0, RowanNorthZ, lx1, hz0, y, "grass", "kerb");
            Ground("The Lasnames' side garden (west)", lx0, hz0, hx0, HillAnchors.LotZ1, y, "grass");
            Ground("The Lasnames' house (ground)", hx0, hz0, hx1, hz1, y, "paving");
            Ground("The Lasnames' side garden (east)", hx1, hz0, lx1, 143f, y, "grass");
            Ground("The Lasnames' back garden", hx0, hz1, hx1, HillAnchors.LotZ1, y, "grass");
            RampX("The Chapel Steps", hx1, 143f, 40f, HillAnchors.LotZ1, y, HillAnchors.M1Y + 0.15f, "steps");
            Ground("The Lasnames' back corner", 40f, 143f, lx1, HillAnchors.LotZ1, y, "grass");
            // The street side between the Larch Steps and the lot (a little path to the steps).
            Ground("The path by the Larch Steps", GreenX1, RowanNorthZ, lx0, HillAnchors.LotZ1, y, "paving", "kerb");
            // The house: the house builder's outside stands here; these are its solid body, roof and tower (the round 5
            // exterior: walls 4.5 m, the ridge 7.1 m over the middle, the tower to 10.9 m at the back).
            float floor = HillAnchors.HouseOrigin.Y, ridgeX = HillAnchors.HouseOrigin.X - 0.8f;
            Box("The Lasname house", BlockKind.House, hx0, hz0 + 0.3f, hx1, hz1, floor, floor + 4.5f, style: "lasname_house");
            RoofX("The Lasname house (roof, west)", hx0 - 0.4f, hz0, ridgeX, hz1 + 0.4f, floor + 4.4f, floor + 7.1f, "roof_shingle_red", floor + 4.5f);
            RoofX("The Lasname house (roof, east)", ridgeX, hz0, hx1 + 0.4f, hz1 + 0.4f, floor + 7.1f, floor + 4.4f, "roof_shingle_red", floor + 4.5f);
            Box("The Lasname house (tower)", BlockKind.House, HillAnchors.HouseOrigin.X - 4f, hz1 - 4.3f, HillAnchors.HouseOrigin.X + 1.6f, hz1 + 0.3f, floor + 7.1f, floor + 10.9f, style: "lasname_house");
            // The fences: the picket fence and its gate on Rowan Street (shut: she's held there), the side fences.
            FenceX("The Lasnames' fence", "picket", lx0, HillAnchors.FrontGate.X - 1f, RowanNorthZ + 0.3f, y, 1.05f, 0.08f, runnable: false);
            FenceX("The Lasnames' fence", "picket", HillAnchors.FrontGate.X + 1f, lx1, RowanNorthZ + 0.3f, y, 1.05f, 0.08f, runnable: false);
            var gate = Box("The Lasnames' gate", BlockKind.Gate, HillAnchors.FrontGate.X - 1f, RowanNorthZ + 0.26f, HillAnchors.FrontGate.X + 1f, RowanNorthZ + 0.34f, y, y + 1.05f, runnable: false, style: "gate");
            _ = gate;
            FenceZ("The Lasnames' side fence (west)", "board", lx0 + 0.06f, RowanNorthZ + 0.4f, HillAnchors.LotZ1, y, 1.8f);
            FenceZ("The Lasnames' side fence (east)", "board", lx1 - 0.06f, RowanNorthZ + 0.4f, HillAnchors.LotZ1, y, 1.8f);
            // The side garden: Elias's barbecue, the big maple with its rope swing, the porch light.
            Prop(HillPropKind.Grill, 40.2f, y, 124.8f, 200f, 0, "elias's barbecue");
            Prop(HillPropKind.PicnicTable, 37.5f, y, 129.5f, 90f, 0, "lasnames");
            Tree("The Lasnames' maple (the big tree)", 38f, y, 136f, 0, 14f, 6f, 1);
            Prop(HillPropKind.FlowerBed, 12.5f, y, 119.5f, 90f, 2);
            Prop(HillPropKind.Mailbox, HillAnchors.FrontGate.X + 1.6f, RowanFoot, RowanNorthZ - 0.3f, 180f, 0, "lasname");
        }
    }
}
