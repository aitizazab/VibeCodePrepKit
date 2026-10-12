using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The house's own furniture where there's no model that fits (house-1.md blocker 4: the 23 built-in-place parts read as
    /// grey-box primitives: a blank-slab fridge with no handles, door-less counters, a tap-less sink, a telescope of white
    /// cubes, beds of cloud-noise blocks, books as flat bricks). Each is built to read as the real thing at eye height:
    /// panelled doors with handles and a recessed plinth, a worktop with an overhang, a sunk sink with a gooseneck tap, a
    /// rounded enamel fridge with chrome handles, beds with frames, mattresses, puffed duvets and pillows, a lathed brass
    /// telescope on a wooden tripod, and so on: smooth shapes (<see cref="HouseShapes"/>) in clean enamel, chrome, brass,
    /// painted and natural wood and fabric (never the plaster-noise paint). Everything stays inside its part's box (its
    /// collider), and is drawn into the house's <see cref="MeshBuilder"/> (recorded for the mesh lint).
    /// </summary>
    public static class HouseProps
    {
        // ---------- Materials (clean: no plaster noise) ----------
        static Material Enamel(string n, Color c) => HouseEscapeLook.Enamel(n, c);
        static Material Chrome => HouseEscapeLook.Chrome();
        static Material Brass => HouseEscapeLook.Brass();
        static Material Iron => HouseEscapeLook.Iron();
        static Material Steel => HouseEscapeLook.Clean("SinkSteel", new Color(0.72f, 0.74f, 0.76f), 0.7f, 1f);
        static Material Dark => HouseEscapeLook.Clean("DarkInside", new Color(0.16f, 0.16f, 0.17f), 0.3f);
        static Material Walnut => HouseMaterials.TrimWalnut();
        static Material Oak => HouseMaterials.Surface("wood_floor", "PropOak", new Color(0.95f, 0.85f, 0.72f), 0.45f);
        static Material Painted(string n, Color c) => HouseMaterials.Surface("white_planks_clean", "Painted" + n, c, 0.45f);
        static Material Worktop => HouseMaterials.Surface("wood_floor", "Butcher", new Color(0.86f, 0.7f, 0.52f), 0.5f);
        static Material Fabric(string n, Color c) => HouseMaterials.Fabric(n, c);

        /// <summary>A part's box as a local frame: x across (right), y up, z toward its front (yaw 0 = +z), centred on
        /// the box's floor. Yaws are multiples of 90 here (the plan's furniture is square to its walls).</summary>
        struct Frame
        {
            public Vector3 c, r, f;
            public float w, d, h;
            public Quaternion q;
            public Vector3 P(float x, float y, float z) => c + r * x + Vector3.up * y + f * z;

            public void Box(MeshBuilder b, float x0, float y0, float z0, float x1, float y1, float z1, Material m)
            {
                var a = P(x0, y0, z0); var bb = P(x1, y1, z1);
                b.Box(Vector3.Min(a, bb), Vector3.Max(a, bb), m);
            }

            public void Round(MeshBuilder b, float x0, float y0, float z0, float x1, float y1, float z1, float radius, Material m, float puff = 0f)
            {
                var size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
                HouseShapes.RoundedBox(b, P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), size, radius, m, q, 3, puff);
            }

            public void Rod(MeshBuilder b, Vector3 a, Vector3 bb, float radius, Material m, int sides = 10) =>
                HouseShapes.Rod(b, P(a.x, a.y, a.z), P(bb.x, bb.y, bb.z), radius, m, sides);
        }

        static Frame FrameOf(Vector3 mn, Vector3 mx, float yaw)
        {
            var q = Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f);
            var f = q * Vector3.forward; var r = q * Vector3.right;
            var size = mx - mn;
            bool turned = Mathf.Abs(f.x) > 0.5f;
            return new Frame
            {
                c = new Vector3((mn.x + mx.x) * 0.5f, mn.y, (mn.z + mx.z) * 0.5f), r = r, f = f, q = q,
                w = turned ? size.z : size.x, d = turned ? size.x : size.z, h = size.y,
            };
        }

        /// <summary>Builds <paramref name="role"/> into its box; false when it isn't one of these.</summary>
        public static bool Build(string role, MeshBuilder b, Vector3 mn, Vector3 mx, float yaw)
        {
            var F = FrameOf(mn, mx, yaw);
            switch (role)
            {
                case "kitchen.counter": Counter(b, F, false, false); return true;
                case "kitchen.sink_counter": Counter(b, F, true, false); return true;
                case "kitchen.island": Counter(b, F, false, true); return true;
                case "kitchen.cabinet_upper": UpperCabinet(b, F); return true;
                case "kitchen.fridge": Fridge(b, F); return true;
                case "bath.sink": if (F.w > 0.85f) Vanity(b, F); else Basin(b, F); return true;
                case "bath.bathtub": Bathtub(b, F); return true;
                case "bedroom.single_bed": Bed(b, F, new Color(0.35f, 0.55f, 0.82f), new Color(0.98f, 0.82f, 0.36f)); return true;
                case "bedroom.double_bed": Bed(b, F, new Color(0.86f, 0.8f, 0.7f), new Color(0.72f, 0.36f, 0.32f)); return true;
                case "observatory.telescope": Telescope(b, F); return true;
                case "attic.crib": Crib(b, F); return true;
                case "rec.pingpong": PingPong(b, F); return true;
                case "furnace.furnace": Furnace(b, F); return true;
                case "furnace.heater": WaterHeater(b, F); return true;
                case "garage.freezer": Freezer(b, F); return true;
                case "laundry.washer": Washer(b, F, false); return true;
                case "laundry.dryer": Washer(b, F, true); return true;
                case "workshop.pegboard": Pegboard(b, F); return true;
                case "hall.umbrella_stand": UmbrellaStand(b, F); return true;
                case "yard.grill": Grill(b, F); return true;
                case "living.window_seat": WindowSeat(b, F); return true;
                case "living.mantel": Mantel(b, F); return true;
                case "dining.pancakes": Pancakes(b, F); return true;
                case "observatory.chart_table": Table(b, F, Walnut, Oak); return true;
                case "observatory.platform": Platform(b, F); return true;
                case "wardrobe.side": WardrobeSide(b, mn, mx); return true;
                case "wardrobe.top": WardrobeTop(b, mn, mx); return true;
                case "theo.bookcase": Bookcase(b, F, Painted("TheoWhite", new Color(0.94f, 0.93f, 0.9f)), true); return true;
                case "pantry.shelves": PantryShelves(b, F); return true;
                case "closet.shelves": ClosetShelves(b, F); return true;
                case "bedroom.screen": Screen(b, mn, mx); return true;
                case "upperhall.linen_press": LinenPress(b, F); return true;
                case "attic.suitcases": Suitcases(b, F); return true;
                case "attic.rolled_rug": RolledRug(b, mn, mx); return true;
            }
            return false;
        }

        // =====================================================================================================
        // The kitchen
        // =====================================================================================================

        static void Handle(MeshBuilder b, Frame F, float x, float y0, float y1, float z)
        {
            // A bar handle on two posts, proud of the door.
            F.Rod(b, new Vector3(x, y0, z), new Vector3(x, y0, z + 0.03f), 0.006f, Chrome, 6);
            F.Rod(b, new Vector3(x, y1, z), new Vector3(x, y1, z + 0.03f), 0.006f, Chrome, 6);
            F.Rod(b, new Vector3(x, y0 - 0.01f, z + 0.03f), new Vector3(x, y1 + 0.01f, z + 0.03f), 0.008f, Chrome, 8);
        }

        /// <summary>A shaker door or drawer front: the frame proud, the panel set back.</summary>
        static void Door(MeshBuilder b, Frame F, float x0, float x1, float y0, float y1, float z, Material m)
        {
            const float rail = 0.055f;
            F.Box(b, x0 + 0.003f, y0 + 0.003f, z, x1 - 0.003f, y1 - 0.003f, z + 0.006f, m);   // the panel
            F.Box(b, x0 + 0.003f, y0 + 0.003f, z, x0 + rail, y1 - 0.003f, z + 0.02f, m);
            F.Box(b, x1 - rail, y0 + 0.003f, z, x1 - 0.003f, y1 - 0.003f, z + 0.02f, m);
            F.Box(b, x0 + rail, y0 + 0.003f, z, x1 - rail, y0 + rail, z + 0.02f, m);
            F.Box(b, x0 + rail, y1 - rail, z, x1 - rail, y1 - 0.003f, z + 0.02f, m);
        }

        static void Counter(MeshBuilder b, Frame F, bool sink, bool island)
        {
            var cab = Painted("Cabinets", new Color(0.62f, 0.72f, 0.62f));
            float hw = F.w * 0.5f, hd = F.d * 0.5f, top = F.h;
            const float plinth = 0.1f, recess = 0.06f, worktop = 0.035f, over = 0.025f;
            // The carcass behind the doors, the recessed plinth, the worktop overhanging the doors.
            F.Box(b, -hw + 0.01f, 0f, -hd, hw - 0.01f, plinth, hd - recess, HouseEscapeLook.Clean("Plinth", new Color(0.18f, 0.17f, 0.16f), 0.3f));
            F.Box(b, -hw, plinth, -hd, hw, top - worktop, hd - over - 0.02f, cab);
            if (sink)
            {
                // The worktop round a sunk steel basin, the basin's walls and floor, a gooseneck tap behind it.
                float bx = Mathf.Min(0.34f, hw - 0.1f), bz0 = -hd + 0.12f, bz1 = hd - over - 0.08f;
                F.Box(b, -hw, top - worktop, -hd, -bx, top, hd, Worktop);
                F.Box(b, bx, top - worktop, -hd, hw, top, hd, Worktop);
                F.Box(b, -bx, top - worktop, -hd, bx, top, bz0, Worktop);
                F.Box(b, -bx, top - worktop, bz1, bx, top, hd, Worktop);
                F.Box(b, -bx, top - 0.2f, bz0, bx, top - 0.19f, bz1, Steel);
                F.Box(b, -bx, top - 0.2f, bz0, -bx + 0.01f, top - 0.002f, bz1, Steel);
                F.Box(b, bx - 0.01f, top - 0.2f, bz0, bx, top - 0.002f, bz1, Steel);
                F.Box(b, -bx, top - 0.2f, bz0, bx, top - 0.002f, bz0 + 0.01f, Steel);
                F.Box(b, -bx, top - 0.2f, bz1 - 0.01f, bx, top - 0.002f, bz1, Steel);
                var tap = new[] { F.P(0f, top, bz0 - 0.04f), F.P(0f, top + 0.24f, bz0 - 0.04f), F.P(0f, top + 0.3f, bz0 + 0.02f), F.P(0f, top + 0.27f, bz0 + 0.12f), F.P(0f, top + 0.2f, bz0 + 0.15f) };
                HouseShapes.Tube(b, tap, 0.013f, Chrome, 10);
                F.Rod(b, new Vector3(-0.06f, top, bz0 - 0.04f), new Vector3(-0.06f, top + 0.06f, bz0 - 0.04f), 0.012f, Chrome, 8);
                F.Rod(b, new Vector3(0.06f, top, bz0 - 0.04f), new Vector3(0.06f, top + 0.06f, bz0 - 0.04f), 0.012f, Chrome, 8);
            }
            else F.Round(b, -hw, top - worktop, -hd, hw, top, hd, 0.008f, Worktop);
            // Doors 0.4-0.6 m wide; on the island a drawer row on top.
            int n = Mathf.Max(1, Mathf.RoundToInt(F.w / 0.5f));
            float dw = F.w / n, front = hd - over - 0.02f;
            float drawerTop = top - worktop - 0.01f, doorTop = island ? drawerTop - 0.17f : drawerTop;
            for (int k = 0; k < n; k++)
            {
                float x0 = -hw + k * dw, x1 = x0 + dw;
                Door(b, F, x0, x1, plinth + 0.005f, doorTop, front, cab);
                Handle(b, F, k % 2 == 0 ? x1 - 0.05f : x0 + 0.05f, doorTop - 0.18f, doorTop - 0.05f, front + 0.02f);
                if (island)
                {
                    Door(b, F, x0, x1, doorTop + 0.005f, drawerTop, front, cab);
                    F.Rod(b, new Vector3((x0 + x1) * 0.5f - 0.06f, doorTop + 0.085f, front + 0.05f), new Vector3((x0 + x1) * 0.5f + 0.06f, doorTop + 0.085f, front + 0.05f), 0.007f, Chrome, 8);
                }
            }
        }

        static void UpperCabinet(MeshBuilder b, Frame F)
        {
            var cab = Painted("Cabinets", new Color(0.62f, 0.72f, 0.62f));
            float hw = F.w * 0.5f, hd = F.d * 0.5f;
            F.Box(b, -hw, 0f, -hd, hw, F.h - 0.03f, hd - 0.022f, cab);
            F.Box(b, -hw, F.h - 0.03f, -hd, hw, F.h, hd, cab);   // the crown, flush with the doors' fronts
            int n = Mathf.Max(1, Mathf.RoundToInt(F.w / 0.45f));
            float dw = F.w / n;
            for (int k = 0; k < n; k++)
            {
                float x0 = -hw + k * dw, x1 = x0 + dw;
                Door(b, F, x0, x1, 0.005f, F.h - 0.035f, hd - 0.022f, cab);
                Handle(b, F, k % 2 == 0 ? x1 - 0.045f : x0 + 0.045f, 0.05f, 0.16f, hd - 0.002f);
            }
        }

        /// <summary>A rounded cream enamel fridge: two doors with a shadow gap, long chrome handles on the left (hinges
        /// right), a kick grille, a little badge.</summary>
        static void Fridge(MeshBuilder b, Frame F)
        {
            var enamel = Enamel("FridgeCream", new Color(0.94f, 0.91f, 0.83f));
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Round(b, -hw, 0.08f, -hd, hw, h, hd - 0.05f, 0.06f, enamel);
            F.Box(b, -hw + 0.03f, 0f, -hd + 0.03f, hw - 0.03f, 0.08f, hd - 0.1f, Dark);   // the kick space
            F.Box(b, -hw + 0.06f, 0.015f, hd - 0.1f, hw - 0.06f, 0.075f, hd - 0.085f, Chrome);   // the grille
            float split = h * 0.68f;
            F.Round(b, -hw + 0.01f, split + 0.012f, hd - 0.06f, hw - 0.01f, h - 0.02f, hd - 0.012f, 0.03f, enamel);
            F.Round(b, -hw + 0.01f, 0.1f, hd - 0.06f, hw - 0.01f, split - 0.012f, hd - 0.012f, 0.03f, enamel);
            float hx = -hw + 0.07f;
            Handle(b, F, hx, split + 0.06f, split + 0.36f, hd - 0.012f);
            Handle(b, F, hx, split - 0.42f, split - 0.08f, hd - 0.012f);
            F.Box(b, hw - 0.2f, h - 0.1f, hd - 0.013f, hw - 0.08f, h - 0.07f, hd - 0.006f, Chrome);   // the badge
        }

        // =====================================================================================================
        // Bathrooms
        // =====================================================================================================

        static Material Porcelain => Enamel("Porcelain", new Color(0.96f, 0.96f, 0.95f));

        static void TapPair(MeshBuilder b, Frame F, float y, float z)
        {
            var spout = new[] { F.P(0f, y, z), F.P(0f, y + 0.12f, z), F.P(0f, y + 0.14f, z + 0.06f), F.P(0f, y + 0.1f, z + 0.11f) };
            HouseShapes.Tube(b, spout, 0.011f, Chrome, 10);
            foreach (float x in new[] { -0.09f, 0.09f })
            {
                HouseShapes.Lathe(b, F.P(x, y, z), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.016f, 0.05f), new Vector2(0.025f, 0.055f), new Vector2(0.025f, 0.07f), new Vector2(0f, 0.075f) }, Chrome, 10);
            }
        }

        static void Basin(MeshBuilder b, Frame F)
        {
            float hd = F.d * 0.5f, top = F.h;
            // The pedestal (lathed), the bowl (a rounded basin with its hollow), the taps.
            HouseShapes.Lathe(b, F.P(0f, 0f, -hd * 0.25f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.13f, 0f), new Vector2(0.1f, 0.06f), new Vector2(0.075f, 0.35f), new Vector2(0.09f, top - 0.2f), new Vector2(0f, top - 0.18f) }, Porcelain, 18);
            F.Round(b, -F.w * 0.5f, top - 0.2f, -hd, F.w * 0.5f, top, hd, 0.06f, Porcelain);
            F.Round(b, -F.w * 0.5f + 0.07f, top - 0.12f, -hd + 0.12f, F.w * 0.5f - 0.07f, top + 0.004f, hd - 0.07f, 0.05f, HouseEscapeLook.Clean("BasinHollow", new Color(0.85f, 0.86f, 0.86f), 0.85f));
            TapPair(b, F, top, -hd + 0.06f);
        }

        static void Vanity(MeshBuilder b, Frame F)
        {
            var cab = Painted("Vanity", new Color(0.86f, 0.88f, 0.86f));
            float hw = F.w * 0.5f, hd = F.d * 0.5f, top = F.h;
            F.Box(b, -hw, 0.08f, -hd, hw, top - 0.04f, hd - 0.04f, cab);
            F.Box(b, -hw + 0.02f, 0f, -hd, hw - 0.02f, 0.08f, hd - 0.08f, Dark);
            int n = Mathf.Max(2, Mathf.RoundToInt(F.w / 0.45f));
            for (int k = 0; k < n; k++)
            {
                float x0 = -hw + k * F.w / n, x1 = x0 + F.w / n;
                Door(b, F, x0, x1, 0.085f, top - 0.05f, hd - 0.04f, cab);
                Handle(b, F, k % 2 == 0 ? x1 - 0.05f : x0 + 0.05f, top - 0.25f, top - 0.12f, hd - 0.02f);
            }
            F.Round(b, -hw, top - 0.04f, -hd, hw, top, hd, 0.01f, HouseEscapeLook.Clean("VanityTop", new Color(0.9f, 0.89f, 0.86f), 0.7f));
            F.Round(b, -0.22f, top - 0.1f, -hd + 0.1f, 0.22f, top + 0.003f, hd - 0.08f, 0.05f, HouseEscapeLook.Clean("BasinHollow", new Color(0.85f, 0.86f, 0.86f), 0.85f));
            TapPair(b, F, top, -hd + 0.05f);
        }

        static void Bathtub(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Round(b, -hw, 0.04f, -hd, hw, h, hd, 0.07f, Porcelain);
            F.Round(b, -hw + 0.07f, 0.22f, -hd + 0.07f, hw - 0.07f, h + 0.004f, hd - 0.07f, 0.12f, HouseEscapeLook.Clean("TubInside", new Color(0.88f, 0.9f, 0.9f), 0.9f));
            F.Box(b, -hw + 0.04f, 0f, -hd + 0.04f, hw - 0.04f, 0.04f, hd - 0.04f, Dark);
            // The taps at one end (the back, -z in the frame: the long sides are along x).
            var spout = new[] { F.P(-hw + 0.04f, h, 0f), F.P(-hw + 0.04f, h + 0.1f, 0f), F.P(-hw + 0.1f, h + 0.13f, 0f), F.P(-hw + 0.16f, h + 0.09f, 0f) };
            HouseShapes.Tube(b, spout, 0.013f, Chrome, 10);
        }

        // =====================================================================================================
        // Beds
        // =====================================================================================================

        /// <summary>A bed (head at the back of its box: -z in its frame): a walnut frame with a tall panelled headboard
        /// and a low footboard on turned legs, the mattress, a puffed duvet folded back at the head, two pillows, a throw
        /// across the foot.</summary>
        static void Bed(MeshBuilder b, Frame F, Color duvet, Color throwColour)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, H = F.h;
            float head = Mathf.Min(H, 1.05f), mattressTop = Mathf.Min(0.58f, H - 0.05f);
            var wood = Walnut;
            // Headboard and footboard.
            F.Round(b, -hw, 0f, -hd, hw, head, -hd + 0.06f, 0.02f, wood);
            F.Box(b, -hw + 0.1f, 0.42f, -hd + 0.06f, hw - 0.1f, head - 0.1f, -hd + 0.07f, Oak);
            F.Round(b, -hw, 0f, hd - 0.05f, hw, 0.62f, hd, 0.02f, wood);
            // Side rails.
            F.Box(b, -hw + 0.02f, 0.18f, -hd + 0.06f, -hw + 0.06f, 0.32f, hd - 0.05f, wood);
            F.Box(b, hw - 0.06f, 0.18f, -hd + 0.06f, hw - 0.02f, 0.32f, hd - 0.05f, wood);
            // The mattress, the duvet (puffed, hanging a little over the rails), the pillows, the throw.
            F.Round(b, -hw + 0.05f, 0.3f, -hd + 0.07f, hw - 0.05f, mattressTop - 0.08f, hd - 0.06f, 0.05f, Fabric("Mattress", new Color(0.95f, 0.94f, 0.9f)));
            F.Round(b, -hw + 0.02f, mattressTop - 0.14f, -hd + 0.55f, hw - 0.02f, mattressTop, hd - 0.055f, 0.05f, Fabric("Duvet" + duvet.GetHashCode(), duvet), 0.025f);
            F.Round(b, -hw + 0.04f, mattressTop - 0.06f, -hd + 0.42f, hw - 0.04f, mattressTop + 0.02f, -hd + 0.6f, 0.04f, Fabric("DuvetFold" + duvet.GetHashCode(), duvet * 0.92f), 0.01f);
            int pillows = F.w > 1.2f ? 2 : 1;
            for (int k = 0; k < pillows; k++)
            {
                float pw = (F.w - 0.2f) / pillows;
                float x0 = -hw + 0.1f + k * pw;
                F.Round(b, x0 + 0.02f, mattressTop - 0.08f, -hd + 0.09f, x0 + pw - 0.02f, mattressTop + 0.06f, -hd + 0.4f, 0.06f, Fabric("Pillow", new Color(0.97f, 0.96f, 0.93f)), 0.03f);
            }
            F.Round(b, -hw + 0.01f, mattressTop - 0.02f, hd - 0.5f, hw - 0.01f, mattressTop + 0.025f, hd - 0.12f, 0.02f, Fabric("Throw" + throwColour.GetHashCode(), throwColour));
        }

        // =====================================================================================================
        // The observatory, the attic, the basement, the garage
        // =====================================================================================================

        /// <summary>Lena's brass refractor on a wooden tripod, aimed up through the round window (+z in its frame) at the
        /// sky: the dew shield, the tube with its bands, the focuser and eyepiece, the finder, the mount.</summary>
        static void Telescope(MeshBuilder b, Frame F)
        {
            float h = F.h, len = Mathf.Min(1.25f, F.d * 0.9f + 0.4f);
            var mount = F.P(0f, h * 0.7f, -0.02f);
            // The tripod: three legs from the mount to the floor.
            foreach (var (x, z) in new[] { (-0.32f, -0.22f), (0.32f, -0.22f), (0f, 0.34f) })
                HouseShapes.Rod(b, mount + Vector3.down * 0.05f, F.P(Mathf.Clamp(x, -F.w * 0.48f, F.w * 0.48f), 0f, Mathf.Clamp(z, -F.d * 0.48f, F.d * 0.48f)), 0.018f, Oak, 8);
            HouseShapes.Lathe(b, mount + Vector3.down * 0.12f, Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.05f, 0.08f), new Vector2(0.035f, 0.12f), new Vector2(0f, 0.13f) }, Brass, 14);
            // The tube, raised 32 degrees toward the front, balanced on the mount.
            var dir = (F.f * Mathf.Cos(32f * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(32f * Mathf.Deg2Rad)).normalized;
            var rot = Quaternion.FromToRotation(Vector3.up, dir);
            var start = mount - dir * (len * 0.42f);
            HouseShapes.Lathe(b, start, rot, new[]
            {
                new Vector2(0f, -0.08f), new Vector2(0.02f, -0.08f), new Vector2(0.02f, 0f), new Vector2(0.032f, 0.0f), new Vector2(0.032f, 0.08f),
                new Vector2(0.045f, 0.09f), new Vector2(0.045f, len * 0.8f), new Vector2(0.052f, len * 0.81f), new Vector2(0.055f, len), new Vector2(0.048f, len),
                new Vector2(0.048f, len - 0.02f), new Vector2(0f, len - 0.02f),
            }, HouseEscapeLook.Clean("TelescopeTube", new Color(0.12f, 0.16f, 0.3f), 0.75f), 22);
            foreach (float t in new[] { 0.12f, 0.45f, 0.78f })
                HouseShapes.Lathe(b, start + dir * (len * t), rot, new[] { new Vector2(0.046f, 0f), new Vector2(0.05f, 0.005f), new Vector2(0.05f, 0.03f), new Vector2(0.046f, 0.035f) }, Brass, 22);
            // The finder scope alongside.
            var side = Vector3.Cross(dir, Vector3.up).normalized;
            HouseShapes.Rod(b, start + dir * (len * 0.3f) + side * 0.07f + Vector3.up * 0.04f, start + dir * (len * 0.6f) + side * 0.07f + Vector3.up * 0.04f, 0.015f, Brass, 10);
            // The eyepiece, pointing down to where she'd stand.
            HouseShapes.Rod(b, start - dir * 0.08f, start - dir * 0.08f + Vector3.down * 0.07f + F.f * -0.02f, 0.012f, Brass, 8);
        }

        static void Crib(MeshBuilder b, Frame F)
        {
            var white = Painted("CribWhite", new Color(0.94f, 0.92f, 0.87f));
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            foreach (var x in new[] { -hw + 0.025f, hw - 0.025f })
                foreach (var z in new[] { -hd + 0.025f, hd - 0.025f })
                    F.Rod(b, new Vector3(x, 0f, z), new Vector3(x, h, z), 0.022f, white, 10);
            foreach (var z in new[] { -hd + 0.025f, hd - 0.025f })
            {
                F.Round(b, -hw, h - 0.05f, z - 0.02f, hw, h, z + 0.02f, 0.015f, white);
                F.Round(b, -hw, 0.28f, z - 0.02f, hw, 0.32f, z + 0.02f, 0.012f, white);
                for (float x = -hw + 0.1f; x < hw - 0.07f; x += 0.09f) F.Rod(b, new Vector3(x, 0.32f, z), new Vector3(x, h - 0.05f, z), 0.01f, white, 6);
            }
            foreach (var x in new[] { -hw + 0.025f, hw - 0.025f })
            {
                F.Round(b, x - 0.02f, h - 0.05f, -hd, x + 0.02f, h, hd, 0.015f, white);
                for (float z = -hd + 0.1f; z < hd - 0.07f; z += 0.09f) F.Rod(b, new Vector3(x, 0.32f, z), new Vector3(x, h - 0.05f, z), 0.01f, white, 6);
            }
            F.Round(b, -hw + 0.04f, 0.32f, -hd + 0.04f, hw - 0.04f, 0.42f, hd - 0.04f, 0.03f, Fabric("CribMattress", new Color(0.9f, 0.86f, 0.78f)));
            F.Round(b, -hw + 0.15f, 0.42f, -hd + 0.08f, 0.1f, 0.47f, hd - 0.1f, 0.02f, Fabric("CribBlanket", new Color(0.62f, 0.78f, 0.86f)), 0.01f);
        }

        static void PingPong(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            var green = HouseEscapeLook.Clean("PingPongGreen", new Color(0.1f, 0.34f, 0.24f), 0.35f);
            var white = HouseEscapeLook.Clean("PingPongLine", new Color(0.95f, 0.95f, 0.92f), 0.4f);
            F.Box(b, -hw, h - 0.04f, -hd, hw, h, hd, green);
            F.Box(b, -0.006f, h, -hd + 0.01f, 0.006f, h + 0.002f, hd - 0.01f, white);
            F.Box(b, -hw + 0.01f, h, -hd + 0.01f, hw - 0.01f, h + 0.002f, -hd + 0.03f, white);
            F.Box(b, -hw + 0.01f, h, hd - 0.03f, hw - 0.01f, h + 0.002f, hd - 0.01f, white);
            foreach (var x in new[] { -hw + 0.12f, hw - 0.12f })
                foreach (var z in new[] { -hd + 0.25f, hd - 0.25f })
                    F.Rod(b, new Vector3(x, 0f, z), new Vector3(x, h - 0.04f, z), 0.022f, Iron, 8);
            F.Rod(b, new Vector3(-hw + 0.12f, 0.15f, 0f), new Vector3(hw - 0.12f, 0.15f, 0f), 0.015f, Iron, 6);
        }

        static void Furnace(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            var body = HouseEscapeLook.Clean("Furnace", new Color(0.55f, 0.56f, 0.55f), 0.45f, 0.6f);
            F.Round(b, -hw + 0.02f, 0f, -hd + 0.02f, hw - 0.02f, h - 0.25f, hd - 0.02f, 0.03f, body);
            Door(b, F, -hw + 0.12f, hw - 0.12f, 0.2f, h - 0.6f, hd - 0.02f, body);
            F.Box(b, -0.05f, 0.45f, hd - 0.02f, 0.05f, 0.52f, hd - 0.005f, HouseEscapeLook.Glow("PilotLight", new Color(1f, 0.55f, 0.2f), 2.5f));
            // The plenum and a round duct up to the ceiling.
            F.Round(b, -hw + 0.06f, h - 0.25f, -hd + 0.06f, hw - 0.06f, h - 0.1f, hd - 0.06f, 0.02f, body);
            HouseShapes.Lathe(b, F.P(0f, h - 0.1f, -hd * 0.3f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.14f, 0.1f), new Vector2(0f, 0.1f) }, Steel, 18);
        }

        static void WaterHeater(MeshBuilder b, Frame F)
        {
            float r = Mathf.Min(F.w, F.d) * 0.48f, h = F.h;
            HouseShapes.Lathe(b, F.P(0f, 0f, 0f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r * 0.9f, 0f), new Vector2(r, 0.05f), new Vector2(r, h - 0.15f), new Vector2(r * 0.85f, h - 0.06f), new Vector2(0f, h - 0.04f) }, Enamel("Heater", new Color(0.9f, 0.9f, 0.88f)), 24);
            foreach (float x in new[] { -0.08f, 0.08f }) F.Rod(b, new Vector3(x, h - 0.08f, 0f), new Vector3(x, h, 0f), 0.02f, HouseEscapeLook.Clean("Copper", new Color(0.72f, 0.42f, 0.26f), 0.6f, 1f), 8);
        }

        static void Freezer(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            var enamel = Enamel("Freezer", new Color(0.95f, 0.95f, 0.93f));
            F.Round(b, -hw, 0.04f, -hd, hw, h - 0.07f, hd, 0.04f, enamel);
            F.Round(b, -hw + 0.005f, h - 0.065f, -hd + 0.005f, hw - 0.005f, h, hd - 0.005f, 0.03f, enamel);
            F.Box(b, -0.12f, h - 0.1f, hd, 0.12f, h - 0.07f, hd + 0f, Chrome);
            F.Box(b, -hw + 0.04f, 0f, -hd + 0.04f, hw - 0.04f, 0.04f, hd - 0.04f, Dark);
        }

        static void Washer(MeshBuilder b, Frame F, bool dryer)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            var enamel = Enamel("Washer", new Color(0.95f, 0.95f, 0.94f));
            F.Round(b, -hw, 0.03f, -hd, hw, h, hd - 0.01f, 0.03f, enamel);
            F.Box(b, -hw + 0.03f, 0f, -hd + 0.03f, hw - 0.03f, 0.03f, hd - 0.05f, Dark);
            // The control strip and knobs, the round door (a chrome ring round dark glass).
            F.Box(b, -hw + 0.03f, h - 0.13f, hd - 0.012f, hw - 0.03f, h - 0.03f, hd - 0.004f, HouseEscapeLook.Clean("WasherPanel", new Color(0.78f, 0.8f, 0.82f), 0.6f));
            F.Rod(b, new Vector3(hw - 0.12f, h - 0.08f, hd - 0.005f), new Vector3(hw - 0.12f, h - 0.08f, hd + 0f), 0.025f, Chrome, 12);
            float cy = (h - 0.15f) * 0.5f + 0.05f, rr = Mathf.Min(hw - 0.06f, 0.2f);
            var doorRot = F.q * Quaternion.Euler(90f, 0f, 0f);
            HouseShapes.Lathe(b, F.P(0f, cy, hd - 0.012f), doorRot, new[] { new Vector2(rr * 0.75f, 0f), new Vector2(rr, 0f), new Vector2(rr, 0.01f), new Vector2(rr * 0.8f, 0.012f) }, Chrome, 24);
            HouseShapes.Lathe(b, F.P(0f, cy, hd - 0.012f), doorRot, new[] { new Vector2(0f, 0f), new Vector2(rr * 0.76f, 0f), new Vector2(0f, 0.004f) }, HouseEscapeLook.Clean(dryer ? "DryerGlass" : "WasherGlass", dryer ? new Color(0.2f, 0.22f, 0.25f) : new Color(0.14f, 0.2f, 0.26f), 0.95f), 24);
        }

        static void Pegboard(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Box(b, -hw, 0f, -hd, hw, h, hd, HouseMaterials.Surface("wooden_panels", "Pegboard", new Color(0.82f, 0.68f, 0.5f), 0.2f));
            // Tools on hooks: a hammer, a saw's blade, two wrenches, a coil of cord (all within its box's front).
            var steel = Steel;
            F.Rod(b, new Vector3(-hw + 0.2f, h * 0.25f, hd - 0.004f), new Vector3(-hw + 0.2f, h * 0.7f, hd - 0.004f), 0.012f, Oak, 6);
            F.Box(b, -hw + 0.15f, h * 0.7f, hd - 0.01f, -hw + 0.25f, h * 0.76f, hd - 0.0f, steel);
            F.Box(b, -hw + 0.4f, h * 0.3f, hd - 0.003f, -hw + 0.75f, h * 0.55f, hd - 0.0f, steel);
            F.Box(b, -hw + 0.75f, h * 0.38f, hd - 0.01f, -hw + 0.85f, h * 0.55f, hd - 0.0f, Oak);
            for (int k = 0; k < 3; k++)
                F.Rod(b, new Vector3(hw - 0.45f + k * 0.08f, h * 0.3f, hd - 0.004f), new Vector3(hw - 0.45f + k * 0.08f, h * 0.62f, hd - 0.004f), 0.008f + k * 0.002f, steel, 6);
        }

        static void UmbrellaStand(MeshBuilder b, Frame F)
        {
            float r = Mathf.Min(F.w, F.d) * 0.46f, h = F.h * 0.65f;
            HouseShapes.Lathe(b, F.P(0f, 0f, 0f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r * 0.85f, 0f), new Vector2(r, 0.04f), new Vector2(r * 0.9f, h * 0.6f), new Vector2(r, h), new Vector2(r * 0.9f, h), new Vector2(0f, 0.03f) }, Enamel("UmbrellaStand", new Color(0.24f, 0.33f, 0.45f)), 18);
            // Two furled umbrellas leaning in it, hooked handles up (to the box's top).
            foreach (var (x, z, c) in new[] { (-0.03f, 0.02f, new Color(0.22f, 0.26f, 0.5f)), (0.035f, -0.025f, new Color(0.55f, 0.18f, 0.16f)) })
            {
                var basePos = F.P(x, 0.03f, z);
                var top = F.P(x * 1.6f, F.h - 0.1f, z * 1.6f);
                var dir = (top - basePos).normalized;
                HouseShapes.Lathe(b, basePos, Quaternion.FromToRotation(Vector3.up, dir), new[] { new Vector2(0f, 0f), new Vector2(0.01f, 0.03f), new Vector2(0.035f, 0.25f), new Vector2(0.03f, (top - basePos).magnitude - 0.1f), new Vector2(0.008f, (top - basePos).magnitude - 0.05f), new Vector2(0f, (top - basePos).magnitude - 0.05f) }, Fabric("Umbrella" + c.GetHashCode(), c), 10);
                var hook = new[] { top - dir * 0.06f, top, top + Vector3.up * 0.04f + F.r * 0.03f, top + F.r * 0.07f + Vector3.up * 0.02f, top + F.r * 0.07f - Vector3.up * 0.02f };
                HouseShapes.Tube(b, hook, 0.008f, Walnut, 6);
            }
        }

        static void Grill(MeshBuilder b, Frame F)
        {
            float r = Mathf.Min(F.w, F.d) * 0.45f, h = F.h;
            var black = Iron;
            foreach (var a in new[] { 0f, 120f, 240f })
            {
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                HouseShapes.Rod(b, F.P(0f, h * 0.62f, 0f) + dir * r * 0.5f, F.P(0f, 0f, 0f) + dir * r * 0.95f, 0.014f, black, 8);
            }
            HouseShapes.Lathe(b, F.P(0f, h * 0.58f, 0f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r * 0.7f, 0.02f), new Vector2(r, 0.16f), new Vector2(r, 0.2f), new Vector2(r * 0.98f, 0.22f), new Vector2(r * 0.7f, h * 0.42f - 0.04f), new Vector2(0.03f, h * 0.42f - 0.02f), new Vector2(0f, h * 0.42f) }, black, 22);
            F.Rod(b, new Vector3(0f, h - 0.06f, 0f), new Vector3(0f, h, 0f), 0.025f, Walnut, 8);
        }

        static void WindowSeat(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Box(b, -hw, 0f, -hd, hw, h - 0.1f, hd, Walnut);
            Door(b, F, -hw + 0.04f, -0.02f, 0.06f, h - 0.14f, hd, Walnut);
            Door(b, F, 0.02f, hw - 0.04f, 0.06f, h - 0.14f, hd, Walnut);
            F.Round(b, -hw + 0.02f, h - 0.1f, -hd + 0.02f, hw - 0.02f, h, hd - 0.02f, 0.035f, Fabric("Teal", new Color(0.26f, 0.48f, 0.5f)), 0.012f);
        }

        static void Mantel(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Round(b, -hw, 0f, -hd, hw, h, hd, 0.012f, Walnut);
        }

        static void Pancakes(MeshBuilder b, Frame F)
        {
            float r = Mathf.Min(F.w, F.d) * 0.48f;
            HouseShapes.Lathe(b, F.P(0f, 0f, 0f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r * 0.6f, 0f), new Vector2(r * 0.65f, 0.004f), new Vector2(r, 0.014f), new Vector2(r * 0.97f, 0.018f), new Vector2(r * 0.62f, 0.008f), new Vector2(0f, 0.008f) }, Porcelain, 24);
            var cake = HouseEscapeLook.Clean("Pancake", new Color(0.86f, 0.6f, 0.3f), 0.35f);
            for (int k = 0; k < 4; k++)
            {
                float y = 0.008f + k * 0.015f, rr = r * (0.66f - 0.015f * k);
                HouseShapes.Lathe(b, F.P(k % 2 * 0.004f, y, -(k % 2) * 0.003f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(rr * 0.95f, 0f), new Vector2(rr, 0.006f), new Vector2(rr * 0.96f, 0.014f), new Vector2(0f, 0.015f) }, cake, 20);
            }
            HouseShapes.Lathe(b, F.P(0.01f, 0.07f, 0.01f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r * 0.5f, 0f), new Vector2(r * 0.42f, 0.003f), new Vector2(0f, 0.004f) }, HouseEscapeLook.Clean("Syrup", new Color(0.55f, 0.28f, 0.08f), 0.9f), 16);
            F.Box(b, -0.02f, 0.074f, -0.02f, 0.02f, 0.086f, 0.02f, HouseEscapeLook.Clean("Butter", new Color(1f, 0.92f, 0.58f), 0.5f));
        }

        static void Table(MeshBuilder b, Frame F, Material legs, Material top)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Round(b, -hw, h - 0.04f, -hd, hw, h, hd, 0.01f, top);
            F.Box(b, -hw + 0.05f, h - 0.12f, -hd + 0.05f, hw - 0.05f, h - 0.04f, hd - 0.05f, legs);
            foreach (var x in new[] { -hw + 0.05f, hw - 0.05f })
                foreach (var z in new[] { -hd + 0.05f, hd - 0.05f })
                    HouseShapes.Lathe(b, F.P(x, 0f, z), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(0.022f, 0f), new Vector2(0.018f, h * 0.4f), new Vector2(0.028f, h * 0.5f), new Vector2(0.025f, h - 0.12f), new Vector2(0f, h - 0.12f) }, legs, 10);
        }

        /// <summary>Lena's viewing platform: a deck on its frame with a nosing, two steps up on its open side.</summary>
        static void Platform(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Box(b, -hw, h - 0.04f, -hd, hw, h, hd, HouseMaterials.Deck());
            F.Box(b, -hw, 0f, -hd, hw, h - 0.04f, hd, Walnut);
        }

        // =====================================================================================================
        // Wardrobes and shelves
        // =====================================================================================================

        static void WardrobeSide(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            // A side panel: walnut, a raised panel on each face (inset), a plinth.
            b.Box(mn, mx, Walnut);
            var s = mx - mn;
            bool thinX = s.x < s.z;
            const float inset = 0.08f, proud = 0.006f;
            for (int side = -1; side <= 1; side += 2)
            {
                if (thinX)
                {
                    float x = side < 0 ? mn.x : mx.x;
                    b.Box(new Vector3(Mathf.Min(x, x + side * proud), mn.y + 0.15f, mn.z + inset), new Vector3(Mathf.Max(x, x + side * proud), mx.y - inset, mx.z - inset), Oak);
                }
                else
                {
                    float z = side < 0 ? mn.z : mx.z;
                    b.Box(new Vector3(mn.x + inset, mn.y + 0.15f, Mathf.Min(z, z + side * proud)), new Vector3(mx.x - inset, mx.y - inset, Mathf.Max(z, z + side * proud)), Oak);
                }
            }
        }

        static void WardrobeTop(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            b.Box(mn, mx, Walnut);
        }

        /// <summary>A bookcase: carcass, shelves, and books that read as books: each its own rounded volume, of varied
        /// height, thickness and colour, some leaning, a stack lying flat, a gap or two.</summary>
        static void Bookcase(MeshBuilder b, Frame F, Material carcass, bool books)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            const float t = 0.022f;
            F.Box(b, -hw, 0f, -hd, -hw + t, h, hd, carcass);
            F.Box(b, hw - t, 0f, -hd, hw, h, hd, carcass);
            F.Box(b, -hw + t, h - t, -hd, hw - t, h, hd, carcass);
            F.Box(b, -hw + t, 0f, -hd, hw - t, 0.08f, hd, carcass);
            F.Box(b, -hw + t, 0.08f, -hd, hw - t, h - t, -hd + 0.012f, carcass);
            int shelves = Mathf.Max(2, Mathf.RoundToInt((h - 0.1f) / 0.36f));
            var rnd = new System.Random((int)(F.c.x * 100f + F.c.z * 37f));
            Color[] spines = { new Color(0.62f, 0.18f, 0.16f), new Color(0.2f, 0.32f, 0.52f), new Color(0.86f, 0.74f, 0.4f), new Color(0.24f, 0.44f, 0.3f), new Color(0.9f, 0.88f, 0.82f), new Color(0.42f, 0.26f, 0.4f), new Color(0.95f, 0.55f, 0.2f) };
            for (int k = 0; k < shelves; k++)
            {
                float y0 = 0.08f + (h - t - 0.08f) * k / shelves, y1 = 0.08f + (h - t - 0.08f) * (k + 1) / shelves;
                if (k > 0) F.Box(b, -hw + t, y0 - t, -hd + 0.012f, hw - t, y0, hd, carcass);
                if (!books) continue;
                float room = y1 - y0 - (k == shelves - 1 ? 0f : t) - 0.02f;
                float x = -hw + t + 0.01f;
                while (x < hw - t - 0.05f)
                {
                    int kind = rnd.Next(10);
                    if (kind == 0) { x += 0.06f + (float)rnd.NextDouble() * 0.06f; continue; }   // a gap
                    if (kind == 1 && x < hw - t - 0.25f)
                    {
                        // A stack lying flat.
                        float sy = y0;
                        for (int s = 0; s < 3 + rnd.Next(3); s++)
                        {
                            float th = 0.025f + (float)rnd.NextDouble() * 0.02f, ww = 0.16f + (float)rnd.NextDouble() * 0.06f;
                            if (sy + th > y0 + room) break;
                            var m = Fabric("Book" + s % spines.Length, spines[rnd.Next(spines.Length)]);
                            F.Round(b, x, sy, -hd + 0.03f, x + ww, sy + th, -hd + 0.03f + Mathf.Min(F.d - 0.05f, 0.22f), 0.004f, m);
                            sy += th;
                        }
                        x += 0.24f;
                        continue;
                    }
                    float bw = 0.022f + (float)rnd.NextDouble() * 0.03f, bh = Mathf.Min(room, 0.17f + (float)rnd.NextDouble() * 0.12f), bd = Mathf.Min(F.d - 0.05f, 0.14f + (float)rnd.NextDouble() * 0.08f);
                    if (x + bw > hw - t - 0.005f) break;
                    var mat = Fabric("Book" + rnd.Next(spines.Length), spines[rnd.Next(spines.Length)]);
                    F.Round(b, x, y0, -hd + 0.03f, x + bw, y0 + bh, -hd + 0.03f + bd, 0.004f, mat);
                    x += bw + 0.002f;
                }
            }
        }

        static void PantryShelves(MeshBuilder b, Frame F)
        {
            var carcass = Painted("Pantry", new Color(0.92f, 0.9f, 0.84f));
            Bookcase(b, F, carcass, false);
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            int shelves = Mathf.Max(2, Mathf.RoundToInt((h - 0.1f) / 0.36f));
            var rnd = new System.Random(7);
            var glass = HouseEscapeLook.Clean("Jar", new Color(0.86f, 0.62f, 0.32f), 0.9f);
            var lid = Brass;
            for (int k = 0; k < shelves; k++)
            {
                float y0 = 0.08f + (h - 0.1f) * k / shelves;
                for (float x = -hw + 0.08f; x < hw - 0.08f; x += 0.13f)
                {
                    float r = 0.04f + (float)rnd.NextDouble() * 0.015f, jh = 0.1f + (float)rnd.NextDouble() * 0.08f;
                    if (rnd.Next(4) == 0)
                        F.Round(b, x - 0.05f, y0, -hd * 0.3f - 0.03f, x + 0.05f, y0 + 0.24f, -hd * 0.3f + 0.03f, 0.005f, HouseEscapeLook.Clean("Box" + rnd.Next(3), new[] { new Color(0.9f, 0.7f, 0.2f), new Color(0.7f, 0.2f, 0.15f), new Color(0.3f, 0.45f, 0.7f) }[rnd.Next(3)], 0.3f));
                    else
                    {
                        HouseShapes.Lathe(b, F.P(x, y0, 0f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r, 0f), new Vector2(r, jh), new Vector2(r * 0.8f, jh + 0.01f), new Vector2(0f, jh + 0.01f) }, glass, 12);
                        HouseShapes.Lathe(b, F.P(x, y0 + jh + 0.01f, 0f), Quaternion.identity, new[] { new Vector2(0f, 0f), new Vector2(r * 0.85f, 0f), new Vector2(r * 0.85f, 0.015f), new Vector2(0f, 0.015f) }, lid, 12);
                    }
                }
            }
        }

        static void ClosetShelves(MeshBuilder b, Frame F)
        {
            var carcass = Painted("Closet", new Color(0.93f, 0.92f, 0.88f));
            Bookcase(b, F, carcass, false);
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            int shelves = Mathf.Max(2, Mathf.RoundToInt((h - 0.1f) / 0.36f));
            var rnd = new System.Random(11);
            Color[] cs = { new Color(0.85f, 0.82f, 0.75f), new Color(0.3f, 0.36f, 0.5f), new Color(0.6f, 0.25f, 0.25f), new Color(0.35f, 0.45f, 0.35f), new Color(0.92f, 0.92f, 0.9f) };
            for (int k = 0; k < shelves; k++)
            {
                float y0 = 0.08f + (h - 0.1f) * k / shelves;
                for (float x = -hw + 0.04f; x < hw - 0.34f; x += 0.38f)
                {
                    float sy = y0;
                    for (int s = 0; s < 2 + rnd.Next(3); s++)
                    {
                        F.Round(b, x, sy, -hd + 0.02f, x + 0.32f, sy + 0.055f, hd - 0.03f, 0.02f, Fabric("Folded" + s, cs[rnd.Next(cs.Length)]), 0.008f);
                        sy += 0.055f;
                    }
                }
            }
        }

        // =====================================================================================================
        // Sight breakers and stored things (round 2)
        // =====================================================================================================

        /// <summary>A three-leaf dressing screen, its leaves zig-zagging across the box's thin side: walnut frames, linen
        /// panels.</summary>
        static void Screen(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            var size = mx - mn;
            bool alongX = size.x >= size.z;
            float len = alongX ? size.x : size.z, th = alongX ? size.z : size.x, h = size.y;
            var linen = Fabric("ScreenLinen", new Color(0.9f, 0.84f, 0.72f));
            for (int k = 0; k < 3; k++)
            {
                float a0 = len * k / 3f, a1 = len * (k + 1) / 3f;
                float w0 = k % 2 == 0 ? 0.02f : th - 0.02f, w1 = k % 2 == 0 ? th - 0.02f : 0.02f;
                Vector3 P(float a, float w, float y) => alongX ? new Vector3(mn.x + a, mn.y + y, mn.z + w) : new Vector3(mn.x + w, mn.y + y, mn.z + a);
                var c0 = P(a0, w0, 0f); var c1 = P(a1, w1, 0f);
                HouseShapes.Rod(b, c0 + Vector3.up * 0.02f, c0 + Vector3.up * (h - 0.01f), 0.015f, Walnut, 8);
                HouseShapes.Rod(b, c1 + Vector3.up * 0.02f, c1 + Vector3.up * (h - 0.01f), 0.015f, Walnut, 8);
                HouseShapes.Rod(b, c0 + Vector3.up * (h - 0.03f), c1 + Vector3.up * (h - 0.03f), 0.014f, Walnut, 8);
                HouseShapes.Rod(b, c0 + Vector3.up * 0.18f, c1 + Vector3.up * 0.18f, 0.014f, Walnut, 8);
                var dir = c1 - c0; var n = Vector3.Cross(Vector3.up, dir).normalized;
                var q0 = c0 + Vector3.up * 0.2f; var q1 = c1 + Vector3.up * 0.2f; var q2 = c1 + Vector3.up * (h - 0.05f); var q3 = c0 + Vector3.up * (h - 0.05f);
                b.QuadFacing(q0, q1, q2, q3, linen, (q0 + q2) * 0.5f - n);
                b.QuadFacing(q0, q1, q2, q3, linen, (q0 + q2) * 0.5f + n);
            }
        }

        /// <summary>A tall linen press: a plinth, two panelled doors on a cupboard, a cornice, brass knobs.</summary>
        static void LinenPress(MeshBuilder b, Frame F)
        {
            var paint = Painted("LinenPress", new Color(0.78f, 0.8f, 0.72f));
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            F.Box(b, -hw + 0.02f, 0f, -hd, hw - 0.02f, 0.1f, hd - 0.03f, paint);
            F.Box(b, -hw, 0.1f, -hd, hw, h - 0.08f, hd - 0.025f, paint);
            F.Round(b, -hw, h - 0.08f, -hd, hw, h, hd, 0.01f, paint);
            Door(b, F, -hw + 0.02f, -0.005f, 0.12f, h - 0.1f, hd - 0.025f, paint);
            Door(b, F, 0.005f, hw - 0.02f, 0.12f, h - 0.1f, hd - 0.025f, paint);
            foreach (float x in new[] { -0.05f, 0.05f })
                HouseShapes.Lathe(b, F.P(x, h * 0.5f, hd - 0.008f), F.q * Quaternion.Euler(90f, 0f, 0f), new[] { new Vector2(0f, 0f), new Vector2(0.012f, 0f), new Vector2(0.007f, 0.015f), new Vector2(0.016f, 0.025f), new Vector2(0f, 0.03f) }, Brass, 10);
        }

        static void Suitcases(MeshBuilder b, Frame F)
        {
            float hw = F.w * 0.5f, hd = F.d * 0.5f, h = F.h;
            var leather = HouseEscapeLook.Clean("Suitcase", new Color(0.42f, 0.25f, 0.14f), 0.4f);
            var tan = HouseEscapeLook.Clean("SuitcaseTan", new Color(0.72f, 0.56f, 0.36f), 0.35f);
            F.Round(b, -hw, 0f, -hd, hw, h * 0.45f, hd, 0.03f, leather);
            F.Round(b, -hw + 0.06f, h * 0.45f, -hd + 0.04f, hw - 0.08f, h * 0.8f, hd - 0.04f, 0.03f, tan);
            F.Round(b, -hw + 0.15f, h * 0.8f, -hd + 0.08f, hw - 0.2f, h, hd - 0.1f, 0.025f, leather);
            foreach (float x in new[] { -hw * 0.5f, hw * 0.5f })
                F.Box(b, x - 0.02f, 0f, hd - 0.002f, x + 0.02f, h * 0.45f, hd + 0f, HouseEscapeLook.Clean("Strap", new Color(0.2f, 0.12f, 0.07f), 0.3f));
        }

        static void RolledRug(MeshBuilder b, Vector3 mn, Vector3 mx)
        {
            var size = mx - mn;
            bool alongX = size.x >= size.z;
            float r = Mathf.Min(size.y, alongX ? size.z : size.x) * 0.5f;
            var a = alongX ? new Vector3(mn.x, mn.y + r, (mn.z + mx.z) * 0.5f) : new Vector3((mn.x + mx.x) * 0.5f, mn.y + r, mn.z);
            var dir = alongX ? Vector3.right : Vector3.forward;
            float len = alongX ? size.x : size.z;
            HouseShapes.Lathe(b, a, Quaternion.FromToRotation(Vector3.up, dir), new[] { new Vector2(0f, 0f), new Vector2(r * 0.95f, 0f), new Vector2(r, 0.02f), new Vector2(r, len - 0.02f), new Vector2(r * 0.95f, len), new Vector2(0f, len) }, Fabric("RolledRug", new Color(0.62f, 0.26f, 0.22f)), 16);
        }

        // =====================================================================================================
        // Cloth
        // =====================================================================================================

        /// <summary>Hanging cloth by role (curtains with a rod, the shower curtain on rings, coats on their hooks, the
        /// washing and the sheets pegged on their lines). True when handled.</summary>
        public static bool Cloth(string role, MeshBuilder b, Vector3 mn, Vector3 mx, int wallSide)
        {
            var size = mx - mn;
            bool alongX = size.x >= size.z;
            var along = alongX ? Vector3.right : Vector3.forward;
            // Facing away from the wall it hangs on (wallSide +1: the wall is at the box's max side).
            var facing = (alongX ? Vector3.forward : Vector3.right) * (wallSide > 0 ? -1f : 1f);
            float width = alongX ? size.x : size.z, depth = alongX ? size.z : size.x;
            var mid = new Vector3((mn.x + mx.x) * 0.5f, mx.y, (mn.z + mx.z) * 0.5f);
            // Against a wall: the rail and the cloth's plane near the wall; on a line: the middle.
            var topMid = wallSide != 0 ? mid - facing * (depth * 0.5f - Mathf.Min(0.06f, depth * 0.3f)) : mid;
            var topLeft = topMid - along * (width * 0.5f);
            switch (role)
            {
                case "den.curtains":
                {
                    var m = HouseMaterials.Fabric("Cloth_den_curtains", new Color(0.82f, 0.62f, 0.32f));
                    HouseShapes.Cloth(b, topLeft + facing * 0.03f - Vector3.up * 0.06f, along, facing, width, size.y - 0.08f, Mathf.Max(3, Mathf.RoundToInt(width / 0.22f)), Mathf.Min(0.05f, depth * 0.25f), m);
                    HouseShapes.Rod(b, topLeft - Vector3.up * 0.03f, topLeft + along * width - Vector3.up * 0.03f, 0.014f, Brass, 8);
                    return true;
                }
                case "bath.curtain":
                {
                    var m = HouseMaterials.Fabric("Cloth_bath_curtain", new Color(0.88f, 0.93f, 0.95f));
                    HouseShapes.Cloth(b, topLeft - Vector3.up * 0.05f, along, facing, width, size.y - 0.06f, Mathf.Max(4, Mathf.RoundToInt(width / 0.16f)), Mathf.Min(0.025f, depth * 0.4f), m);
                    HouseShapes.Rod(b, topLeft - Vector3.up * 0.015f, topLeft + along * width - Vector3.up * 0.015f, 0.012f, Chrome, 8);
                    return true;
                }
                case "backhall.coats":
                case "closet.clothes":
                {
                    // A rail of hooks along the top and coats hanging from them: shoulders, a body that flares to the hem,
                    // sleeves, in wool and waxed cotton.
                    Color[] cs = { new Color(0.24f, 0.32f, 0.5f), new Color(0.62f, 0.3f, 0.22f), new Color(0.84f, 0.76f, 0.5f), new Color(0.3f, 0.42f, 0.32f), new Color(0.88f, 0.4f, 0.42f), new Color(0.2f, 0.2f, 0.22f) };
                    var back = topMid;
                    HouseShapes.Rod(b, back - along * (width * 0.5f) - Vector3.up * 0.04f, back + along * (width * 0.5f) - Vector3.up * 0.04f, 0.015f, role == "closet.clothes" ? Chrome : Walnut, 8);
                    int n = Mathf.Max(2, Mathf.RoundToInt(width / (role == "closet.clothes" ? 0.18f : 0.32f)));
                    for (int k = 0; k < n; k++)
                    {
                        float a = -width * 0.5f + width * (k + 0.5f) / n;
                        var hook = back + along * a - Vector3.up * 0.04f;
                        var m = HouseMaterials.Fabric("Coat" + (k % cs.Length), cs[(k * 7 + 3) % cs.Length]);
                        float len = size.y - 0.12f - (k % 3) * 0.12f, cw = Mathf.Min(0.42f, width / n * (role == "closet.clothes" ? 2.2f : 1.3f));
                        float cd = Mathf.Min(depth - 0.08f, 0.16f);
                        float reach = role == "closet.clothes" ? Mathf.Min(cw, depth - 0.1f) : cd;
                        if (role == "closet.clothes") cw = reach;
                        var c = hook + facing * (reach * 0.5f + 0.01f) - Vector3.up * (0.06f + len * 0.5f);
                        var turn = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(0f, role == "closet.clothes" ? 90f : 0f, 0f);
                        HouseShapes.RoundedBox(b, c, new Vector3(cw, len, cd), Mathf.Min(0.07f, cd * 0.45f), m, turn, 3, 0f);
                        HouseShapes.RoundedBox(b, hook - Vector3.up * 0.1f + facing * 0.02f, new Vector3(cw * 0.55f, 0.12f, cd * 0.8f), 0.04f, m, turn, 2);
                    }
                    return true;
                }
                case "laundry.washing":
                case "yard.sheets":
                {
                    // Sheets (or shirts and towels in the laundry) pegged on the line, sagging between pegs, a breeze in
                    // their folds.
                    bool sheets = role == "yard.sheets";
                    Color[] cs = sheets ? new[] { new Color(0.96f, 0.95f, 0.9f), new Color(0.85f, 0.9f, 0.96f), new Color(0.96f, 0.9f, 0.84f) } : new[] { new Color(0.9f, 0.88f, 0.8f), new Color(0.5f, 0.6f, 0.75f), new Color(0.85f, 0.45f, 0.4f), new Color(0.95f, 0.95f, 0.92f) };
                    int n = sheets ? 3 : 5;
                    float gap = 0.08f, pw = (width - gap * (n - 1)) / n;
                    for (int k = 0; k < n; k++)
                    {
                        var tl = topLeft + along * (k * (pw + gap)) - Vector3.up * 0.005f;
                        float ph = sheets ? size.y - 0.05f - (k % 2) * 0.2f : size.y * (0.45f + 0.15f * (k % 3));
                        HouseShapes.Cloth(b, tl, along, facing, pw, ph, sheets ? 3 : 2, 0.02f, HouseMaterials.Fabric("Washing" + k % cs.Length, cs[k % cs.Length]), 0.04f, 0.6f);
                        foreach (float pa in new[] { 0.05f, pw - 0.05f })
                            b.Box(tl + along * pa - along * 0.006f - facing * 0.008f - Vector3.up * 0.05f, tl + along * pa + along * 0.006f + facing * 0.008f + Vector3.up * 0.005f, Oak);
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
