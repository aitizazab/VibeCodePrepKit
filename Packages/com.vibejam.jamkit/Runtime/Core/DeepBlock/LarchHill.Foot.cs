using System;
using System.Numerics;

namespace JamKit.DeepBlock
{
    /// <summary>The foot of the hill: the far bank, the creek, Walt's bike shop and yard, and the Row A yards (the back way,
    /// Night 1's lessons) up to Mill Street. docs/redesign/world.md "The foot".</summary>
    public sealed partial class LarchHill
    {
        /// <summary>The far bank's lane and lots, the park along the creek (the arrival, the Lindqvists' loop opposite).</summary>
        void MakeFarBank()
        {
            _layer = "farbank";
            // The creek bank park (where she arrives), its gravel path, Creek Lane South and its footpaths.
            // The far bank rises 3 m from the creek to the lane: the arrival stands on the rise, the whole hill ahead.
            Ground("The creek bank (far side)", WestX, -21f, EastX, CreekZ0, FarBankY, "grass");
            Ground("The creek path", WestX, -24f, EastX, -21f, FarBankY, "gravel");
            RampZ("The creek park (the rise)", WestX, -40f, EastX, -24f, FarRiseY, FarBankY, "grass");
            Ground("The creek park (the top of the rise)", WestX, -44f, EastX, -40f, FarRiseY, "grass");
            Ground("Creek Lane South (north footpath)", WestX, -46f, EastX, -44f, FarRiseY + 0.15f, "paving", "kerb");
            Ground("Creek Lane South", WestX, -52f, EastX, -46f, FarRiseY, "asphalt", "kerb");
            Ground("Creek Lane South (south footpath)", WestX, -54f, EastX, -52f, FarRiseY + 0.15f, "paving", "kerb");
            // The far bank's lots: front gardens (the loop plays in the Lindqvists'), houses facing the creek.
            Ground("The far bank's gardens", WestX, SouthZ, EastX, -54f, FarRiseY + 0.6f, "grass");
            var rnd = new Random(11);
            string[] walls = { "siding_white", "siding_blue", "brick_red", "siding_butter", "stucco", "siding_sage", "brick_cream", "siding_rose" };
            string[] roofs = { "roof_shingle_grey", "roof_shingle_red", "roof_slate", "roof_shingle_green" };
            int i = 0;
            for (float x = WestX + 2f; x <= EastX - 24f; x += 22f, i++)
            {
                bool lindqvist = x <= 128f && x + 22f > 128f;
                float cx = lindqvist ? 128f : x + 11f;
                var h = House(new HillHouse
                {
                    name = lindqvist ? "The Lindqvists' house" : "Far bank house " + (i + 1), lot = "far bank",
                    center = new Vector3(cx, FarRiseY + 0.6f + 0.45f, -84f), width = 11f + (float)rnd.NextDouble() * 3f, depth = 10f,
                    facing = HillFacing.North, storeys = rnd.Next(3) == 0 ? 1.5f : 2f, roof = (HillRoof)(rnd.Next(3)), pitch = 30f + rnd.Next(12),
                    plinth = 0.45f, wall = walls[i % walls.Length], roofMat = roofs[i % roofs.Length], seed = 100 + i, porch = true,
                });
                if (lindqvist) { h.wall = "siding_sage"; h.door = "door_yellow"; h.litShare = 0.3f; }
                // The front garden's picket fence on the footpath, a gap at the gate.
                float fx0 = cx - 11f, fx1 = cx + 11f;
                FenceX("Front fence", "picket", fx0, cx - 1f, -54.3f, FarRiseY + 0.6f, 1.0f, 0.1f, runnable: false);
                FenceX("Front fence", "picket", cx + 1f, fx1, -54.3f, FarRiseY + 0.6f, 1.0f, 0.1f, runnable: false);
                if (!lindqvist && rnd.Next(2) == 0) Tree("Far bank tree", cx + 7f, FarRiseY + 0.6f, -64f, rnd.Next(3) == 0 ? 2 : 0, 7f + (float)rnd.NextDouble() * 3f, 3f, 300 + i);
            }
            // Along the creek: a few trees, benches and lamps on the path, the tyre-swing tree by the kids' jump.
            Tree("The tyre-swing tree", 104f, FarBankY, -22.5f, 0, 11f, 4.6f, 7);
            for (float x = -120f; x < 130f; x += 34f)
            {
                if (Math.Abs(x - 97f) < 14f || Math.Abs(x - 128f) < 16f) continue;
                Tree("Creek-side tree", x, FarRiseY, -42f, (int)(Math.Abs(x) % 3f), 9f, 4f, (int)x + 500);
            }
            for (float x = -110f; x < 130f; x += 40f)
            {
                Prop(HillPropKind.Bench, x + 6f, FarBankY, -21.6f, 180f);
                Prop(HillPropKind.Streetlight, x + 16f, FarBankY, -23.6f, 0f);
            }
            Prop(HillPropKind.Bench, 120f, FarRiseY, -41.4f, 180f);
        }

        /// <summary>The creek: 8 m wide in a stone channel 4.6 m deep, water 1.2 m deep. Its north side is a wall of houses,
        /// the flood wall, Walt's apron (the only landing) and the boathouse; the ford steps up its south bank are the way
        /// out if she falls in (wading is slow).</summary>
        void MakeCreek()
        {
            _layer = "creek";
            const float fordX0 = 116f, fordX1 = 120f;
            Ground("The creek bed", WestX, CreekZ0, fordX0, CreekZ1, CreekBed, "creek_bed");
            Ground("The creek bed (east)", fordX1, CreekZ0, EastX, CreekZ1, CreekBed, "creek_bed");
            // The ford steps: from the bed at the north wall up to the far bank (30 degrees).
            RampZ("The ford steps", fordX0, CreekZ0, fordX1, CreekZ1, FarBankY, CreekBed, "steps");
            Box("The creek (water)", BlockKind.Water, WestX, CreekZ0, EastX, CreekZ1, CreekBed, CreekWater, solid: false);
            // The old plank (the kids' crossing), back only when she told him about the creek: from the far bank to the east
            // end of Walt's apron.
            var plank = Box("The old plank", BlockKind.Plank, 106.2f, CreekZ0 - 0.6f, 106.9f, CreekZ1 + 0.4f, FarBankY - 0.06f, FarBankY + 0.08f, runnable: false, style: "plank");
            plank.switchIndex = CreekPlankSwitch;
            plank.presentWhenOpen = true;
            // The old footbridge, out since the flood: its abutments and the fallen span in the water.
            Box("The old footbridge (south abutment)", BlockKind.BrickWall, -44f, CreekZ0 + 0.01f, -38f, -13.5f, CreekBed, FarBankY + 0.4f, style: "stone_retaining");
            Box("The old footbridge (fallen span)", BlockKind.Plank, -43.5f, -13f, -38.5f, -10.5f, CreekBed, CreekBed + 0.9f, style: "deck");
        }

        /// <summary>Walt's bike shop on the north bank (its apron the only landing: the first double jump; its garage door
        /// half open: the first slide), the counter with Saturday's paper, the workbench with his ledger, the patched bike;
        /// and his yard behind it, where he sits on the low end of the yard wall (the old way over to the yards).</summary>
        void MakeWalt()
        {
            _layer = "walt";
            Ground("Walt's apron", WaltX0, CreekZ1, WaltX1, ShopZ0, FootY, "concrete");
            Ground("Walt's shop (floor)", WaltX0, ShopZ0, WaltX1, ShopZ1, FootY, "concrete");
            Ground("Walt's yard", WaltX0, ShopZ1, WaltX1, YardZ1, FootY, "gravel");
            const float sx0 = 86f, sx1 = 108f, sh = 4.4f, t = 0.3f;
            const float gx0 = 92f, gx1 = 98f;   // the garage door
            // The shop's walls (an inside you walk through), its door, its roof.
            Box("Walt's shop (front wall, west)", BlockKind.Shop, sx0, ShopZ0, gx0, ShopZ0 + t, FootY, sh, style: "brick_red");
            Box("Walt's shop (front wall, east)", BlockKind.Shop, gx1, ShopZ0, sx1, ShopZ0 + t, FootY, sh, style: "brick_red");
            Box("Walt's shop (over the garage door)", BlockKind.Shop, gx0, ShopZ0, gx1, ShopZ0 + t, 4.0f, sh, style: "brick_red");
            Box("Walt's garage door (half open)", BlockKind.Door, gx0, ShopZ0 + 0.02f, gx1, ShopZ0 + t - 0.02f, GarageDoorGap, 4.0f, runnable: false, usable: true, style: "garage_door");
            Box("Walt's shop (west wall)", BlockKind.Shop, sx0, ShopZ0, sx0 + t, ShopZ1, FootY, sh, style: "brick_red");
            Box("Walt's shop (east wall)", BlockKind.Shop, sx1 - t, ShopZ0, sx1, ShopZ1, FootY, sh, style: "brick_red");
            Box("Walt's shop (back wall, west)", BlockKind.Shop, sx0, ShopZ1 - t, 95f, ShopZ1, FootY, sh, style: "brick_red");
            Box("Walt's shop (back wall, east)", BlockKind.Shop, 97.2f, ShopZ1 - t, sx1, ShopZ1, FootY, sh, style: "brick_red");
            Box("Walt's shop (over the back door)", BlockKind.Shop, 95f, ShopZ1 - t, 97.2f, ShopZ1, 2.3f, sh, style: "brick_red");
            RoofZ("Walt's shop (roof, front)", sx0 - 0.4f, ShopZ0 - 0.5f, sx1 + 0.4f, 6f, sh + 0.1f, 7.0f, "roof_metal", sh);
            RoofZ("Walt's shop (roof, back)", sx0 - 0.4f, 6f, sx1 + 0.4f, ShopZ1 + 0.5f, 7.0f, sh + 0.1f, "roof_metal", sh);
            // Inside: the workbench along the west wall (the ledger), the counter by the door (the paper), the bike stand.
            Prop(HillPropKind.Workbench, sx0 + 0.8f, FootY, 6f, 90f, 0, "walt");
            Box("Walt's counter", BlockKind.Deck, 99f, 8.6f, 104.5f, 9.6f, FootY, 1.0f, runnable: false, style: "counter");
            Prop(HillPropKind.Bike, 96f, FootY, 6.5f, 90f, 1, "the patched bike");
            Prop(HillPropKind.TyreStack, 106.4f, FootY, 2.2f, 0f);
            Prop(HillPropKind.TyreStack, 106.4f, FootY, 3.4f, 0f);
            Prop(HillPropKind.Bike, 104f, FootY, 12.6f, 0f, 2);
            Prop(HillPropKind.Bike, 101.6f, FootY, 12.6f, 0f, 3);
            Prop(HillPropKind.BikeRack, 104f, FootY, -5f, 90f);

            // The yard: walled (2.9 m), the low end of its west wall (1.6 m) by the Mill Street wall, where Walt sits. The
            // yard's north side is Mill Street's wall (4.15 m, a tall fence on top).
            // (Within 10.5 m of Mill Street's fence nothing you can stand on is 2.6 m or more: the bands' rule.)
            FenceZ("Walt's yard wall (west)", "brick_low", WaltX0 - 0.25f, ShopZ1, YardZ1, FootY, 1.6f, 0.5f);
            FenceZ("Walt's yard wall (east)", "brick_low", WaltX1 + 0.15f, ShopZ1, 28f, FootY, 3.4f, 0.3f);
            FenceZ("Walt's yard wall (east, by the street)", "brick_low", WaltX1 + 0.15f, 28f, YardZ1, FootY, 1.6f, 0.3f);
            var walt = Box("Walt (sitting on the wall)", BlockKind.Prop, WaltX0 + 0.01f, 35.2f, WaltX0 + 0.9f, 36.8f, FootY, 2.2f, runnable: false, style: "walt");
            walt.switchIndex = WaltGateSwitch;
            walt.presentWhenOpen = false;
            Prop(HillPropKind.Crates, 90f, FootY, 20f, 15f);
            Prop(HillPropKind.Pallets, 92.4f, FootY, 21f, 0f);
            Prop(HillPropKind.TyreStack, 107f, FootY, 18f, 0f);
            Prop(HillPropKind.TyreStack, 107f, FootY, 19.2f, 0f);
            Prop(HillPropKind.Barrel, 106.8f, FootY, 24f, 0f);
            Prop(HillPropKind.Bench, 101f, FootY, 30f, 0f, 0, "walt's bench");
            Prop(HillPropKind.Wheelbarrow, 99f, FootY, 36f, 70f);
            Prop(HillPropKind.Workbench, 104f, FootY, 38.6f, 0f, 1, "yard bench");
            Prop(HillPropKind.Bike, 95f, FootY, 38.2f, 0f, 4);
            Tree("Walt's apple tree", 96f, FootY, 22f, 2, 6f, 2.8f, 13);

            // The boathouse east of the apron (its wall is the creek's north side there).
            Building(new HillBuilding { kind = HillBuildingKind.Boathouse, name = "The boathouse", min = new Vector2(WaltX1 + 0.3f, CreekZ1), max = new Vector2(EastX, ShopZ1), floorY = FootY, height = 6.5f, facing = HillFacing.South, seed = 3 });
            Ground("The boathouse slip", WaltX1, CreekZ1, EastX, ShopZ1, FootY, "concrete");
            Ground("The boathouse yard", WaltX1, ShopZ1, EastX, YardZ1, FootY, "gravel");
        }

        /// <summary>A roof slope along Z, solid from <paramref name="bottom"/> (the walls' top it rests on; NaN: 0.3 m
        /// under its low edge) up to its slope.</summary>
        BlockPiece RoofZ(string name, float x0, float z0, float x1, float z1, float atZ0, float atZ1, string mat, float bottom = float.NaN)
        {
            float b = float.IsNaN(bottom) ? Math.Min(atZ0, atZ1) - 0.3f : bottom;
            var p = Box(name, BlockKind.Roof, x0, z0, x1, z1, b, Math.Max(atZ0, atZ1), style: mat);
            p.rampAxis = 2;
            p.rampLow = z0 < z1 ? atZ0 : atZ1;
            p.rampHigh = z0 < z1 ? atZ1 : atZ0;
            return p;
        }

        /// <summary>A roof slope along X (see <see cref="RoofZ"/>).</summary>
        BlockPiece RoofX(string name, float x0, float z0, float x1, float z1, float atX0, float atX1, string mat, float bottom = float.NaN)
        {
            float b = float.IsNaN(bottom) ? Math.Min(atX0, atX1) - 0.3f : bottom;
            var p = Box(name, BlockKind.Roof, x0, z0, x1, z1, b, Math.Max(atX0, atX1), style: mat);
            p.rampAxis = 1;
            p.rampLow = x0 < x1 ? atX0 : atX1;
            p.rampHigh = x0 < x1 ? atX1 : atX0;
            return p;
        }

        // The back way's lots, east to west.
        public const float A1X0 = 60f, A2X0 = 36f, A3X0 = 12f;
        /// <summary>The Pruitts' pool (the wall-run): X 65-77 against Mill Street's wall; the patio east of it is the run-up.</summary>
        public const float PoolX0 = 65f, PoolX1 = 77f, PoolFloor = -1.6f, PoolWater = -0.35f;
        /// <summary>The pergola's fallen beam on the Pruitts'/Okafors' line (slide under it).</summary>
        public const float PergolaGap = 1.15f, PergolaZ0 = 36.5f;
        /// <summary>The Hendersons' raised garden (the jump and grab: its wall and fence stand 2.9 m over the Okafors' lawn).</summary>
        public const float HendersonY = 1.4f, HendersonFence = 1.5f;
        /// <summary>The slot behind the old stable (the wall-jump chimney up to Mill Street): 3.5 m wide, its floor a little
        /// sunk, the stable's back wall on one side and Mill Street's wall on the other.</summary>
        public const float SlotZ0 = 36.5f, SlotFloor = -0.4f, SlotX0 = 14.5f, SlotX1 = 34f;

        /// <summary>Row A: the creek-side terrace (X 12-84) whose back yards are the back way, and the terrace west of the
        /// Foot Square; the Foot Square and the Mill Steps (closed off from the creek by the flood wall).</summary>
        void MakeRowA()
        {
            _layer = "rowa";
            // The terrace of creek-side houses, their fronts on the water (X 12-84 east of the square, and west of it).
            string[] walls = { "brick_red", "stucco_warm", "siding_blue", "brick_cream", "siding_rose", "stucco" };
            string[] roofs = { "roof_slate", "roof_clay", "roof_shingle_grey", "roof_slate", "roof_shingle_red", "roof_clay" };
            int i = 0;
            for (float x = A3X0; x < WaltX0 - 1f; x += 12f, i++)
                House(new HillHouse
                {
                    name = "Row A house " + (i + 1), lot = "row a", center = new Vector3(x + 6f, FootY, -1f), width = 12f, depth = 14f,
                    facing = HillFacing.South, storeys = i % 3 == 1 ? 3f : 2.5f, roof = i % 2 == 0 ? HillRoof.GableSide : HillRoof.Gambrel, pitch = 38f,
                    overhang = 0.3f, plinth = 0f, porch = false, wall = walls[i % walls.Length], roofMat = roofs[i % roofs.Length], seed = 200 + i,
                    dormers = i % 2, litShare = 0.2f,
                });
            i = 0;
            for (float x = -24f; x >= WestX; x -= 12f, i++)
                House(new HillHouse
                {
                    name = "Row A west house " + (i + 1), lot = "row a west", center = new Vector3(x + 6f, FootY, -1f), width = 12f, depth = 14f,
                    facing = HillFacing.South, storeys = 2.5f + (i % 2) * 0.5f, roof = i % 3 == 0 ? HillRoof.Gambrel : HillRoof.GableSide, pitch = 36f,
                    overhang = 0.3f, plinth = 0f, porch = false, wall = walls[(i + 3) % walls.Length], roofMat = roofs[(i + 2) % roofs.Length], seed = 260 + i,
                });
            // The ground under the terraces and their back yards (west of the square: private, walled).
            Ground("Row A (under the houses)", A3X0, CreekZ1, WaltX0, 6f, FootY, "concrete");
            Ground("Row A west (under the houses)", WestX, CreekZ1, -12f, 6f, FootY, "concrete");
            Ground("Row A west yards", WestX, 6f, -12f, MillWallZ0, FootY, "grass");
            for (float x = -24f; x > WestX; x -= 12f)
                FenceZ("Row A west yard fence", "board", x, 6f, MillWallZ0, FootY, 1.8f);

            // The Foot Square at the bottom of the Mill Steps, walled off from the creek by the flood wall.
            Ground("The Foot Square", -12f, CreekZ1, A3X0, 14f, FootY, "cobbles");
            FenceX("The flood wall", "stone_low", -12f, A3X0, CreekZ1 + 0.3f, FootY, 3.6f, 0.6f);
            Prop(HillPropKind.Fountain, 0f, FootY, 3f);
            Prop(HillPropKind.BusStopSign, 8f, FootY, 10f, 270f, 15, "route 9 terminus");
            Prop(HillPropKind.PhoneBox, -9f, FootY, 10f, 90f);
            Prop(HillPropKind.Bench, -6f, FootY, 0f, 0f);
            Prop(HillPropKind.Bench, 6f, FootY, 0f, 0f);
            RampZ("The Mill Steps", -5f, 14f, 5f, MillWallZ0, FootY, MillY + 0.15f, "steps");
            RampZ("The Mill Steps' west bank", -12f, 14f, -5f, MillWallZ0, FootY, MillY + 0.15f, "grass");
            RampZ("The Mill Steps' east bank", 5f, 14f, 11.7f, MillWallZ0, FootY, MillY + 0.15f, "grass");
            // The steps' east wall: the Hendersons' side (5 m, too tall to climb from their garden).
            FenceZ("The Mill Steps' wall", "garden_wall", 11.85f, 6f, MillWallZ0, FootY, 5f, 0.3f);

            // ---------- The back way: the Pruitts' (A1), the Okafors' (A2), the Hendersons' (A3) ----------
            // A1, the Pruitts': the patio (run-up), the pool against Mill Street's wall (wall-run over it), the deck.
            Ground("The Pruitts' garden", A1X0, 6f, WaltX0, 22f, FootY, "grass");
            FenceX("The Pruitts' patio wall", "garden_wall", A1X0, WaltX0, 22.15f, FootY, 3.6f, 0.3f);
            Ground("The Pruitts' patio", PoolX1, 22.3f, WaltX0, MillWallZ0, FootY, "paving");
            Ground("The Pruitts' pool (deep end)", PoolX0, 22.3f, PoolX1 - 3f, MillWallZ0, PoolFloor, "pool");
            RampX("The Pruitts' pool (steps at the shallow end)", PoolX1 - 3f, 22.3f, PoolX1, MillWallZ0, PoolFloor, FootY, "pool");
            Box("The Pruitts' pool (water)", BlockKind.Water, PoolX0, 22.3f, PoolX1, MillWallZ0, PoolFloor, PoolWater, solid: false);
            Ground("The Pruitts' deck", A1X0, 22.3f, PoolX0, MillWallZ0, FootY, "deck");
            Prop(HillPropKind.PicnicTable, 80.5f, FootY, 27f, 90f);
            Prop(HillPropKind.Planter, 81f, FootY, 23.2f, 0f);
            Prop(HillPropKind.Grill, 79f, FootY, 23.4f, 0f, 0, "pruitts");
            Prop(HillPropKind.Planter, 62.5f, FootY, 23.2f, 0f);
            // The Pruitts'/Okafors' line: a tall hedge, and the old pergola by the wall, fallen: its beam at 1.15 m.
            Box("The Pruitts' hedge", BlockKind.Hedge, A1X0 - 0.5f, 6f, A1X0 + 0.5f, 28f, FootY, 3.4f, runnable: false, style: "hedge");
            Box("The Pruitts' hedge (clipped low by the street)", BlockKind.Hedge, A1X0 - 0.5f, 28f, A1X0 + 0.5f, PergolaZ0, FootY, 1.75f, runnable: false, style: "hedge");
            Box("The fallen pergola (beam)", BlockKind.Plank, A1X0 - 0.3f, PergolaZ0, A1X0 + 0.3f, MillWallZ0, PergolaGap, PergolaGap + 0.45f, runnable: false, style: "pergola_beam");

            // A2, the Okafors': a lawn of kids' things; on its west line the Hendersons' raised garden, wall and fence 2.9 m.
            Ground("The Okafors' garden", A2X0, 6f, A1X0, MillWallZ0, FootY, "grass");
            Prop(HillPropKind.Trampoline, 52f, FootY, 13f, 0f);
            Prop(HillPropKind.PaddlingPool, 46f, FootY, 18f, 0f);
            Prop(HillPropKind.WashingLine, 54f, FootY, 26f, 90f);
            Prop(HillPropKind.Swing, 42f, FootY, 12f, 0f);
            Prop(HillPropKind.Chalk, 48f, FootY, 31f, 0f);
            Prop(HillPropKind.Ball, 50f, FootY, 34f, 0f);
            Prop(HillPropKind.Bike, 57f, FootY, 10f, 30f, 5, "okafor kid's bike");
            Tree("The Okafors' cherry", 40f, FootY, 22f, 2, 5.5f, 2.6f, 17);

            // A3, the Hendersons': the raised garden (1.4 m), the old stable along its north side, and behind the stable the
            // slot against Mill Street's wall: the chimney.
            Ground("The Hendersons' garden", A3X0, 6f, A2X0, 26f, HendersonY, "grass");
            Ground("The Hendersons' garden (by the slot)", SlotX1, 26f, A2X0, MillWallZ0, HendersonY, "grass");
            Ground("Hill Road (past St. Brigid's)", HillWalkX0, 322f, HillRoadX1, NorthZ, HillAnchors.SummitY, "asphalt", "kerb");
            // A thick garden wall flush with the raised garden's face (the mantle needs a top to land on: the movement's
            // ledge probe looks 0.35 m past the face, so anything thinner can't be climbed).
            FenceZ("The Hendersons' wall", "brick_low", A2X0 - 0.25f, 6f, 28f, HendersonY, HendersonFence, 0.5f);
            FenceZ("The Hendersons' wall (low by the street)", "brick_low", A2X0 - 0.25f, 28f, MillWallZ0, HendersonY, 1.0f, 0.5f);
            Ground("The Hendersons' garden (under the old stable)", A3X0, 26f, SlotX1, SlotZ0, HendersonY, "grass");
            Building(new HillBuilding { kind = HillBuildingKind.Garage, name = "The old stable", min = new Vector2(A3X0, 26f), max = new Vector2(SlotX1, SlotZ0), floorY = HendersonY, height = 6.4f, facing = HillFacing.South, wall = "brick_red", seed = 9 });
            Ground("The slot (behind the old stable)", SlotX0, SlotZ0, SlotX1, MillWallZ0, SlotFloor, "cobbles");
            // At the slot's end, a landing at the street's height (the chimney climbs onto it, then through the gap in the fence).
            Ground("The slot's landing", A3X0, SlotZ0, SlotX0, MillWallZ0, MillFoot, "paving");
            Prop(HillPropKind.Bin, 32.8f, SlotFloor, 37.2f, 0f);
            Prop(HillPropKind.Greenhouse, 18f, HendersonY, 12f, 0f);
            Prop(HillPropKind.FlowerBed, 27f, HendersonY, 10f, 90f);
            Prop(HillPropKind.BirdBath, 22f, HendersonY, 20f, 0f);
            Tree("The Hendersons' pear", 30f, HendersonY, 16f, 0, 7f, 3f, 19);
        }
    }
}
