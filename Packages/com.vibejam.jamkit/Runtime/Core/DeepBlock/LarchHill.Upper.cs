using System;
using System.Numerics;

namespace JamKit.DeepBlock
{
    /// <summary>The middle band (Night 2's escape and after: the terraces, the Larch Steps, the church, the school, Hill Road
    /// and its Route 9 bus) and the top (Night 3: Crest Road's shops and St. Brigid's on the summit); the world's edges.
    /// docs/redesign/world.md "The middle band", "The top".</summary>
    public sealed partial class LarchHill
    {
        /// <summary>Hill Road (the bus line) up the east side, and its west footpath.</summary>
        public const float HillRoadX0 = 112f, HillRoadX1 = 122f, HillWalkX0 = 110f;
        /// <summary>The Larch Steps (the hill's spine: from Rowan Street straight up to the hospital), their planted banks.</summary>
        public const float StepsX0 = -6f, StepsX1 = 6f;

        /// <summary>A terrace street: its south footpath on top of the wall from the band below (with a railing), its road,
        /// its north footpath; <paramref name="z0"/> is the wall's line, <paramref name="y"/> the road.</summary>
        void TerraceStreet(string name, float z0, float y, float x0 = WestX, float x1 = HillWalkX0, float gap0 = 0f, float gap1 = 0f)
        {
            // The steps cross every terrace street at X -8..8 at the street's level (they're part of it there).
            Ground(name + " (south footpath)", x0, z0, x1, z0 + 4f, y + 0.15f, "paving");
            FenceX(name + "'s railing", "railing", x0, -8f, z0 + 0.35f, y + 0.15f, 1.1f, 0.08f, runnable: false);
            if (gap1 > gap0)
            {
                FenceX(name + "'s railing", "railing", 8f, gap0, z0 + 0.35f, y + 0.15f, 1.1f, 0.08f, runnable: false);
                FenceX(name + "'s railing", "railing", gap1, x1, z0 + 0.35f, y + 0.15f, 1.1f, 0.08f, runnable: false);
            }
            else FenceX(name + "'s railing", "railing", 8f, x1, z0 + 0.35f, y + 0.15f, 1.1f, 0.08f, runnable: false);
            Ground(name, x0, z0 + 4f, x1, z0 + 10f, y, "asphalt", "kerb");
            Ground(name + " (north footpath)", x0, z0 + 10f, x1, z0 + 12f, y + 0.15f, "paving", "kerb");
            for (float x = x0 + 14f; x < x1 - 4f; x += 32f)
                Prop(HillPropKind.Streetlight, x, y + 0.15f, z0 + 3.4f, 0f);
            for (float x = x0 + 30f; x < x1 - 4f; x += 46f)
                if (Math.Abs(x) > 12f) Prop(HillPropKind.Car, x, y, z0 + 5.1f, 90f, (int)Math.Abs(x + z0) % 7, "terrace");
        }

        /// <summary>A row of houses facing a terrace street, on lots from <paramref name="lz0"/> to <paramref name="lz1"/>
        /// (ground <paramref name="gy"/>), between X <paramref name="x0"/> and <paramref name="x1"/>, skipping
        /// <paramref name="skip0"/>..<paramref name="skip1"/>.</summary>
        void TerraceRow(string street, float x0, float x1, float lz0, float lz1, float gy, int seed, float lotW = 17f, float skip0 = 0f, float skip1 = 0f)
        {
            var r = new Random(seed);
            int n = 0;
            for (float x = x0; x + 6f <= x1; n++)
            {
                float w = Math.Min(lotW + (float)(r.NextDouble() * 4.0 - 2.0), x1 - x);
                if (x1 - (x + w) < 8f) w = x1 - x;
                float xa = x, xb = x + w;
                x += w;
                if (skip1 > skip0 && xb > skip0 && xa < skip1) continue;
                string name = street + ", number " + (n * 2 + 1 + seed % 7);
                Ground(name + " (front garden)", xa, lz0, xb, lz0 + 4f, gy, "grass", "kerb");
                Ground(name + " (house and garden)", xa, lz0 + 4f, xb, lz1, gy, "grass");
                float depth = Math.Min(11f, lz1 - lz0 - 6f);
                Ordinary(name, street, (xa + xb) * 0.5f, gy + 0.4f, lz0 + 4f + depth * 0.5f + 0.5f, Math.Min(12f, w - 4f), depth, HillFacing.South, seed * 31 + n, 0.4f);
                float cx = (xa + xb) * 0.5f;
                FenceX(name + " (front fence)", r.Next(2) == 0 ? "picket" : "brick_low", xa, cx - 0.9f, lz0 + 0.3f, gy, 0.9f, 0.12f, runnable: false);
                FenceX(name + " (front fence)", r.Next(2) == 0 ? "picket" : "brick_low", cx + 0.9f, xb, lz0 + 0.3f, gy, 0.9f, 0.12f, runnable: false);
                FenceZ(name + " (side fence)", "board", xb - 0.06f, lz0 + 0.4f, lz1, gy, 1.8f);
                if (lz1 - (lz0 + 4f + depth + 1f) > 5f) GardenThings(xa + 0.8f, xb - 0.8f, lz0 + 5f + depth + 0.5f, lz1 - 0.8f, gy, seed * 13 + n);
            }
        }

        void MakeMiddle()
        {
            _layer = "middle";
            float m1 = HillAnchors.M1Y, m2 = HillAnchors.M2Y, m3 = HillAnchors.M3Y, m4 = HillAnchors.M4Y, crest = HillAnchors.CrestY;
            // The terrace streets (each on the wall over the band below) and their rows.
            TerraceStreet("Chapel Row", 146f, m1, gap0: 35.5f, gap1: 40.5f);
            TerraceStreet("School Lane", 182f, m2);
            TerraceStreet("Bell Row", 220f, m3);
            TerraceStreet("Upper Row", 254f, m4);
            TerraceRow("Chapel Row", WestX, -8f, 158f, 182f, m1 + 0.6f, 3);
            TerraceRow("Chapel Row", 8f, HillWalkX0, 158f, 182f, m1 + 0.6f, 4);
            TerraceRow("School Lane", WestX, -96f, 194f, 220f, m2 + 0.6f, 5);
            TerraceRow("School Lane", 72f, HillWalkX0, 194f, 220f, m2 + 0.6f, 6);
            TerraceRow("Bell Row", WestX, -8f, 232f, 254f, m3 + 0.6f, 7);
            TerraceRow("Bell Row", 8f, HillWalkX0, 232f, 254f, m3 + 0.6f, 8);
            TerraceRow("Upper Row", WestX, -8f, 266f, 280f, m4 + 0.6f, 9, 13f);
            TerraceRow("Upper Row", 8f, HillWalkX0, 266f, 280f, m4 + 0.6f, 10, 13f);

            // The primary school (School Lane, west) and its playground; St. Anne's (School Lane, east) and its grounds.
            Ground("Larch Hill Primary (grounds)", -96f, 194f, -8f, 220f, m2 + 0.6f, "asphalt", "kerb");
            Building(new HillBuilding { kind = HillBuildingKind.School, name = "Larch Hill Primary", min = new Vector2(-96f, 199f), max = new Vector2(-44f, 218f), floorY = m2 + 0.6f, height = 8f, facing = HillFacing.South, seed = 31 });
            FenceX("The playground fence", "chainlink", -96f, -60f, 194.3f, m2 + 0.6f, 2f, 0.06f, runnable: false);
            FenceX("The playground fence", "chainlink", -56f, -8f, 194.3f, m2 + 0.6f, 2f, 0.06f, runnable: false);
            Prop(HillPropKind.Swing, -32f, m2 + 0.6f, 206f, 0f);
            Prop(HillPropKind.Slide, -24f, m2 + 0.6f, 210f, 90f);
            Prop(HillPropKind.Sandbox, -16f, m2 + 0.6f, 202f, 0f);
            Prop(HillPropKind.Chalk, -36f, m2 + 0.6f, 198f, 0f);
            Prop(HillPropKind.StreetSign, -61f, m2 + 0.6f, 194.8f, 0f, 11, "school sign");
            Ground("St. Anne's (grounds)", 8f, 194f, 72f, 220f, m2 + 0.6f, "grass", "kerb");
            Building(new HillBuilding { kind = HillBuildingKind.Church, name = "St. Anne's", min = new Vector2(46f, 202f), max = new Vector2(64f, 219.5f), floorY = m2 + 0.6f, height = 9f, facing = HillFacing.South, seed = 33 });
            Prop(HillPropKind.Bench, 30f, m2 + 0.6f, 200f, 180f);
            Prop(HillPropKind.Bench, 40f, m2 + 0.6f, 200f, 180f);
            Tree("St. Anne's yew", 24f, m2 + 0.6f, 208f, 3, 10f, 3.5f, 51);
            Tree("St. Anne's lime", 70f, m2 + 0.6f, 200f, 1, 13f, 4f, 52);
            FenceX("St. Anne's wall", "stone_low", 8f, 52f, 194.3f, m2 + 0.6f, 1.0f, 0.4f);
            FenceX("St. Anne's wall", "stone_low", 58f, 72f, 194.3f, m2 + 0.6f, 1.0f, 0.4f);

            // The Larch Steps: the hill's spine, straight up from Rowan Street to the hospital, crossing each terrace street.
            void Flight(string name, float z0, float z1, float y0, float y1)
            {
                RampZ(name, StepsX0, z0, StepsX1, z1, y0, y1, "steps");
                RampZ(name + " (west bank)", -8f, z0, StepsX0, z1, y0, y1, "grass");
                RampZ(name + " (east bank)", StepsX1, z0, 8f, z1, y0, y1, "grass");
            }
            Flight("The Larch Steps (Rowan to Chapel Row)", RowanNorthZ, 146f, RowanFoot, m1 + 0.15f);
            Flight("The Larch Steps (to School Lane)", 158f, 182f, m1 + 0.15f, m2 + 0.15f);
            Flight("The Larch Steps (to Bell Row)", 194f, 220f, m2 + 0.15f, m3 + 0.15f);
            Flight("The Larch Steps (to Upper Row)", 232f, 254f, m3 + 0.15f, m4 + 0.15f);
            Flight("The Larch Steps (to Crest Road)", 266f, 280f, m4 + 0.15f, crest + 0.15f);
            Flight("The Larch Steps (to St. Brigid's)", 292f, 308f, crest + 0.15f, HillAnchors.SummitY);

            // Hill Road: the Route 9 bus's way up the east side, flat at each terrace, steep between; its west footpath.
            void Road(string name, float z0, float z1, float y0, float y1)
            {
                if (Math.Abs(y1 - y0) < 0.01f)
                {
                    Ground(name, HillRoadX0, z0, HillRoadX1, z1, y0, "asphalt", "kerb");
                    Ground(name + " (footpath)", HillWalkX0, z0, HillRoadX0, z1, y0 + 0.15f, "paving", "kerb");
                }
                else
                {
                    RampZ(name, HillRoadX0, z0, HillRoadX1, z1, y0, y1, "asphalt");
                    RampZ(name + " (footpath)", HillWalkX0, z0, HillRoadX0, z1, y0 + 0.15f, y1 + 0.15f, "paving");
                }
            }
            Road("Hill Road (up from Rowan Street)", RowanNorthZ, 146f, RowanY, m1);
            Road("Hill Road (at Chapel Row)", 146f, 158f, m1, m1);
            Road("Hill Road (up to School Lane)", 158f, 182f, m1, m2);
            Road("Hill Road (at School Lane)", 182f, 194f, m2, m2);
            Road("Hill Road (up to Bell Row)", 194f, 220f, m2, m3);
            Road("Hill Road (at Bell Row: the bus stop)", 220f, 232f, m3, m3);
            Road("Hill Road (up to Upper Row)", 232f, 254f, m3, m4);
            Road("Hill Road (at Upper Row)", 254f, 266f, m4, m4);
            RampZ("The Hill Steps (to Crest Road)", HillWalkX0, 266f, HillRoadX1, 280f, m4 + 0.15f, crest + 0.15f, "steps");
            Ground("Hill Road (at Crest Road)", HillWalkX0, 280f, HillRoadX1, 292f, crest, "asphalt", "kerb");
            RampZ("Hill Road (up to St. Brigid's)", HillWalkX0, 292f, HillRoadX1, 322f, crest, HillAnchors.SummitY, "asphalt");
            // East of Hill Road: a strip of gardens and garages stepping up with it.
            Ground("Hill Road (its mouth)", HillRoadX0, RowanRoadZ1, HillRoadX1, RowanNorthZ, RowanY, "asphalt", "kerb");
            RampZ("East of Hill Road (the bank)", HillRoadX1, 146f, EastX, 292f, m1 + 0.6f, crest + 0.6f, "grass");
            RampZ("East of Hill Road (the top bank)", HillRoadX1, 292f, EastX, NorthZ, crest + 0.6f, HillAnchors.SummitY + 2f, "grass");
            for (float z = 160f; z < 290f; z += 26f)
                Tree("Hill Road's lime", 130f, m1 + 0.6f + (crest - m1) * (z - 146f) / 148f, z, 1, 12f, 4f, (int)z);
            // The Route 9 stop at Bell Row: the shelter, its sign, the bus waiting (it never leaves).
            Prop(HillPropKind.BusShelter, 111f, m3 + 0.15f, 228f, 270f, 0, "route 9");
            Prop(HillPropKind.BusStopSign, 111.2f, m3 + 0.15f, 222.6f, 270f, 15, "route 9");
            Box("The Route 9 bus", BlockKind.Prop, HillRoadX0 + 0.6f, 221f, HillRoadX0 + 3.2f, 232f, m3, m3 + 3.2f, runnable: true, style: "bus");
        }

        void MakeTop()
        {
            _layer = "top";
            float crest = HillAnchors.CrestY, summit = HillAnchors.SummitY;
            TerraceStreet("Crest Road", 280f, crest);
            // The shops (two storeys, flats over them), built into the slope under the hospital's forecourt.
            Ground("Crest Road's shops (west)", WestX, 292f, -8f, 308f, crest + 0.2f, "paving", "kerb");
            Ground("Crest Road's shops (east)", 8f, 292f, HillWalkX0, 308f, crest + 0.2f, "paving", "kerb");
            var west = new HillBuilding { kind = HillBuildingKind.ShopRow, name = "Crest Road's shops (west)", min = new Vector2(-100f, 296f), max = new Vector2(-9f, 308f), floorY = crest + 0.2f, height = 8.5f, facing = HillFacing.South, seed = 41 };
            west.signs.AddRange(new[] { 1, 2, 3, 4, 5, 6 });
            Building(west);
            var east = new HillBuilding { kind = HillBuildingKind.ShopRow, name = "Crest Road's shops (east)", min = new Vector2(9f, 296f), max = new Vector2(100f, 308f), floorY = crest + 0.2f, height = 8.5f, facing = HillFacing.South, seed = 42 };
            east.signs.AddRange(new[] { 7, 0, -1, 2, 5, -1 });
            Building(east);
            Building(new HillBuilding { kind = HillBuildingKind.Kiosk, name = "The newspaper kiosk", min = new Vector2(-124f, 297f), max = new Vector2(-118f, 302f), floorY = crest + 0.2f, height = 3f, facing = HillFacing.South, seed = 43 });
            for (float x = -96f; x < 100f; x += 22f)
                if (Math.Abs(x) > 12f) Prop(HillPropKind.Planter, x, crest + 0.2f, 295f, 0f);

            // The summit: the forecourt (its turning circle, the ambulance bay) and St. Brigid's Hospital crowning the hill.
            Ground("St. Brigid's forecourt", -70f, 308f, 70f, 322f, summit, "paving");
            Ground("St. Brigid's lawns (west)", WestX, 308f, -70f, NorthZ, summit, "grass");
            Ground("St. Brigid's lawns (east)", 70f, 308f, HillWalkX0, NorthZ, summit, "grass");
            Ground("St. Brigid's (under it)", -70f, 322f, 70f, 352f, summit, "concrete");
            Ground("St. Brigid's car park", -70f, 352f, 70f, NorthZ, summit, "asphalt");
            Building(new HillBuilding { kind = HillBuildingKind.Hospital, name = "St. Brigid's Hospital", min = new Vector2(-60f, 322f), max = new Vector2(60f, 352f), floorY = summit, height = 28f, facing = HillFacing.South, seed = 45 });
            Prop(HillPropKind.Van, 40f, summit, 316f, 90f, 1, "ambulance");
            Prop(HillPropKind.Bench, -20f, summit, 312f, 0f);
            Prop(HillPropKind.Bench, 20f, summit, 312f, 0f);
            Prop(HillPropKind.Bollard, -9f, summit, 309f, 0f);
            Prop(HillPropKind.Bollard, 9f, summit, 309f, 0f);
            foreach (float x in new[] { -120f, -96f, 84f, 100f })
                Tree("St. Brigid's plane tree", x, summit, 326f, 0, 15f, 5.5f, (int)-x);
            for (float x = -60f; x <= 60f; x += 15f)
                Prop(HillPropKind.Car, x, summit, 362f, 0f, (int)Math.Abs(x) % 7, "hospital car park");
        }

        /// <summary>The world's edges: walls you never see past (the hill's sides run on as backdrop beyond them).</summary>
        void MakeEdges()
        {
            _layer = "bounds";
            Box("Bound (west)", BlockKind.Boundary, WestX - 4f, SouthZ, WestX, NorthZ, -8f, 140f);
            Box("Bound (east)", BlockKind.Boundary, EastX, SouthZ, EastX + 4f, NorthZ, -8f, 140f);
            Box("Bound (south)", BlockKind.Boundary, WestX, SouthZ - 4f, EastX, SouthZ, -8f, 140f);
            Box("Bound (north)", BlockKind.Boundary, WestX, NorthZ, EastX, NorthZ + 4f, -8f, 140f);
            _layer = "edges";
            // What you see at the edges: tall hedges and walls along each band's ends.
            Box("The west hedge (far bank)", BlockKind.Hedge, WestX - 1f, SouthZ, WestX, CreekZ0, FarRiseY, FarRiseY + 3.5f, runnable: false, style: "hedge");
            Box("The east hedge (far bank)", BlockKind.Hedge, EastX, SouthZ, EastX + 1f, CreekZ0, FarRiseY, FarRiseY + 3.5f, runnable: false, style: "hedge");
        }
    }
}
