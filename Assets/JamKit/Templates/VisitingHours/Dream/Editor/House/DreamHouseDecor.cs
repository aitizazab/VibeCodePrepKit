using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Night 2's house as a lived-in home (art pass 5, and the architecture pass's new plan, 2026-10-12: the creator found it "not at all furnished... for the
    /// space Elias spent most of his life in, his memories of it are rather bland"). Decoration only: no colliders, nothing
    /// on Theo's walking paths, the hiding spots, the routes or the run walls; the gameplay pieces are unchanged.
    ///
    /// The rules used (docs/DREAM-HOUSE.md "Interior notes", from feng shui and ordinary interior-design practice):
    /// a focal point per room (the fireplace, the table, the bed, the desk); the main seat backed and facing the room's
    /// focal point; furniture in groups, each anchored by a rug; clear walkways (about a metre) along the paths; light in
    /// layers (a pendant over each room, table and floor lamps lower down); soft and round things against the hard edges
    /// (rugs, cushions, a teddy, plants); things at different heights (rug, table, mantel, mirror, shelf).
    /// </summary>
    public static class DreamHouseDecor
    {
        const float F = DreamHouse.Ground;
        const float Kenney = 2.0f;   // Kenney's furniture is about half size: this makes a chair about 0.95 m tall

        static Transform _root;
        static readonly List<string> _missing = new List<string>();

        public static void Decorate(Transform root)
        {
            _missing.Clear();
            _unit = 0f;
            _root = new GameObject("Home (decoration)").transform;
            _root.SetParent(root, false);
            Kitchen();
            Living();
            Hall();
            Parents();
            Bath();
            TheosRoom();
            Attic();
            Debug.Log("JAMKIT_HOUSE art: home decoration placed" + (_missing.Count > 0 ? "; models missing: " + string.Join(", ", _missing) : ""));
        }

        // ---------- The rooms (the architecture pass of 2026-10-12: the plan is core DreamHouse's) ----------

        /// <summary>The kitchen and dining room: the table on its rug under a pendant; the counter's clutter and its
        /// cupboards; the fridge's drawings and notes; a clock; a plant.</summary>
        static void Kitchen()
        {
            var t = Group("Kitchen and dining");
            Rug(t, "rugRectangle", new Vector3(-3.4f, F, 75.7f), 4.4f, 3.0f, new Color(0.62f, 0.72f, 0.88f));
            Pendant(t, new Vector3(-3.4f, F, 75.7f), 2.8f);
            const float top = F + 0.95f;
            Model(t, "kitchenCoffeeMachine", new Vector3(-3.95f, top, 72.32f), Vector3.forward);
            Model(t, "toaster", new Vector3(-6.6f, top, 72.32f), Vector3.forward, 1.5f);
            var red = DreamProps.MeshLook("DecorMugRed", new Color(0.88f, 0.42f, 0.45f), 0.004f);
            var cream = DreamProps.MeshLook("DecorMug", new Color(0.98f, 0.95f, 0.9f), 0.004f);
            Prim(t, "Mug", PrimitiveType.Cylinder, new Vector3(-4.3f, top + 0.05f, 72.4f), new Vector3(0.09f, 0.05f, 0.09f), red);
            Prim(t, "Mug", PrimitiveType.Cylinder, new Vector3(-4.45f, top + 0.05f, 72.3f), new Vector3(0.09f, 0.05f, 0.09f), cream);
            // A kettle on the stove (Lena's): a round body, a spout, a handle.
            var kettle = DreamProps.MeshLook("DecorKettle", new Color(0.55f, 0.75f, 0.72f), 0.006f);
            Prim(t, "Kettle", PrimitiveType.Sphere, new Vector3(-4.9f, top + 0.1f, 72.32f), new Vector3(0.24f, 0.2f, 0.24f), kettle);
            Prim(t, "Kettle spout", PrimitiveType.Cylinder, new Vector3(-4.75f, top + 0.13f, 72.32f), new Vector3(0.04f, 0.08f, 0.04f), kettle, Quaternion.Euler(0f, 0f, -50f));
            FruitBowl(t, new Vector3(-6.68f, top, 74.7f));
            // Upper cupboards either side of the south window, and one over the west run.
            Model(t, "kitchenCabinetUpper", new Vector3(-6.58f, F + 1.5f, 72.22f), Vector3.forward);
            Model(t, "kitchenCabinetUpper", new Vector3(-4.02f, F + 1.5f, 72.22f), Vector3.forward);
            Model(t, "kitchenCabinetUpper", new Vector3(-6.78f, F + 1.5f, 74.95f), Vector3.right);
            // The fridge: the calendar (a memory) with the children's drawings and notes round it.
            const float front = -6.235f;
            Picture(t, "dream_pic_kids_drawing_1", new Vector3(front, F + 1.85f, 77.48f), Vector3.right, 0.26f, 0.34f, null);
            Picture(t, "dream_pic_kids_drawing_2", new Vector3(front, F + 0.85f, 76.72f), Vector3.right, 0.26f, 0.34f, null);
            Note(t, new Vector3(front, F + 0.95f, 77.5f), Vector3.right, new Color(1f, 0.92f, 0.55f));
            Note(t, new Vector3(front, F + 1.8f, 76.7f), Vector3.right, new Color(0.7f, 0.9f, 0.75f));
            Clock(t, new Vector3(-0.6f, F + 2.25f, DreamHouse.HallZ0 - 0.01f), Vector3.back);
            Model(t, "pottedPlant", new Vector3(2.2f, F, 77.6f), Vector3.zero);
            // A high shelf of plates along the west wall over the fridge's side (a dresser's top row).
            Books(t, new Vector3(-6.9f, F + 2.2f, 73.0f), true, new[] { 0f });
        }

        /// <summary>The living room: the fireplace on the east wall is the heart (the sofa faces it across a rug and the
        /// coffee table); the armchair with its side table and lamp; the floor lamp; books; plants; cushions in the bay.</summary>
        static void Living()
        {
            var t = Group("Living room");
            Rug(t, "rugRounded", new Vector3(5.3f, F, 75.1f), 3.6f, 2.6f, new Color(0.86f, 0.55f, 0.5f));
            Pendant(t, new Vector3(5.2f, F, 75.0f), 3.0f);
            Fireplace(t, 7.4f, 75.2f);
            var mug = DreamProps.MeshLook("DecorMug", new Color(0.98f, 0.95f, 0.9f), 0.004f);
            Prim(t, "Mug", PrimitiveType.Cylinder, new Vector3(5.0f, F + 0.45f + 0.05f, 74.8f), new Vector3(0.09f, 0.05f, 0.09f), mug);
            Model(t, "books", new Vector3(5.3f, F + 0.45f, 75.3f), Vector3.forward, 1.6f);
            Model(t, "plantSmall2", new Vector3(5.25f, F + 0.45f, 74.7f), Vector3.zero, 1.6f);
            Model(t, "sideTable", new Vector3(7.72f, F, 77.3f), Vector3.left, 1.3f);
            var lamp = Model(t, "lampRoundTable", new Vector3(7.72f, F + 0.5f, 77.3f), Vector3.zero, 1.5f);
            LampGlow(t, lamp, 0.22f);
            var floorLamp = Model(t, "lampRoundFloor", new Vector3(3.15f, F, 76.65f), Vector3.zero);
            LampGlow(t, floorLamp, 0.3f);
            Books(t, new Vector3(3.03f, F, 77.6f), true, new[] { 0.42f, 0.86f, 1.3f });
            Model(t, "pottedPlant", new Vector3(7.65f, F, 72.4f), Vector3.zero);
            Model(t, "plantSmall1", new Vector3(DreamHouse.BayX1 - 0.3f, F + 0.45f, DreamHouse.BayZ0 + 0.25f), Vector3.zero, 2.4f);
            // Cushions on the window seat.
            var cushions = new[] { new Color(0.98f, 0.82f, 0.45f), new Color(0.62f, 0.72f, 0.88f), new Color(0.9f, 0.6f, 0.65f) };
            for (int i = 0; i < 3; i++)
                DreamProps.Show("Cushion", DreamProps.RoundedBox("Cushion", new Vector3(0.42f, 0.36f, 0.14f), 0.06f), DreamProps.MeshLook("DecorCushion" + i, cushions[i], 0.006f),
                    t, Vector3.zero, Quaternion.Euler(-12f, (i - 1) * 8f, 0f)).transform.position = new Vector3(DreamHouse.BayX0 + 0.5f + i * 0.6f, F + 0.65f, DreamHouse.BayZ0 + 0.18f);
            // A painting over the sofa, on the half wall's header? No: on the north wall, over the bookcase's side.
            Picture(t, "dream_pic_photo_4", new Vector3(5.3f, F + 1.75f, DreamHouse.HallZ0 - 0.012f), Vector3.back, 0.9f, 0.7f, null, true);
        }

        /// <summary>The hall: a long runner, wall lights, the coats (a hiding spot, drawn by the dressing).</summary>
        static void Hall()
        {
            var t = Group("Hall");
            Rug(t, "rugRectangle", new Vector3(0.9f, F, 79.4f), 11.6f, 1.0f, new Color(0.8f, 0.62f, 0.78f));
            foreach (float x in new[] { -3.6f, 0.6f, 3.4f })
            {
                Prim(t, "Wall light (bracket)", PrimitiveType.Cube, new Vector3(x, F + 1.95f, DreamHouse.HallZ0 + 0.26f), new Vector3(0.08f, 0.16f, 0.12f), DreamProps.MeshLook("DecorBrass", new Color(0.95f, 0.78f, 0.38f), 0.004f));
                Prim(t, "Wall light", PrimitiveType.Sphere, new Vector3(x, F + 2.0f, DreamHouse.HallZ0 + 0.36f), Vector3.one * 0.5f,
                    DreamEscapeBuilder.Glow("DecorLampGlow", new Color(1f, 0.86f, 0.6f), 0.55f, 0f, 0.1f));
            }
            // The cupboard under the stairs: its little door, ajar.
            var door = DreamEscapeBuilder.Box("Cupboard door (ajar)", t, Vector3.zero, new Vector3(0.9f, 1.2f, 0.04f), DreamEscapeBuilder.Pastel("StairTread", new Color(0.78f, 0.58f, 0.46f), 0.02f));
            door.transform.position = new Vector3(DreamHouse.StairX0 + 0.25f, F + 0.62f, DreamHouse.HallZ1 - 0.25f);
            door.transform.rotation = Quaternion.Euler(0f, 60f, 0f);
            // The brother's and sister's childhood photos, climbing the wall beside the attic stairs.
            for (int i = 0; i < 4; i++)
            {
                float z = DreamHouse.StairZ1 - 0.8f - i * 0.6f;
                float stair = (DreamHouse.AtticFloor - F) * (DreamHouse.StairZ1 - z) / (DreamHouse.StairZ1 - DreamHouse.StairZ0);
                Picture(t, "dream_pic_childhood_" + (i + 1), new Vector3(DreamHouse.StairX0 + 0.01f, F + stair + 1.45f, z), Vector3.right, 0.5f, 0.4f, null, true);
            }
        }

        /// <summary>The parents' room: the bed on its rug, bedside tables and lamps, the wedding photo over the bed.</summary>
        static void Parents()
        {
            var t = Group("Parents' room");
            Rug(t, "rugRectangle", new Vector3(-5.2f, F, 82.6f), 3.4f, 2.2f, new Color(0.74f, 0.66f, 0.86f));
            Pendant(t, new Vector3(-4.2f, F, 82.3f), 2.7f);
            foreach (float x in new[] { -6.55f, -3.85f })
            {
                Model(t, "cabinetBedDrawerTable", new Vector3(x, F, DreamHouse.HubZ1 - 0.3f), Vector3.back);
                var lamp = Model(t, "lampRoundTable", new Vector3(x, F + 0.53f, DreamHouse.HubZ1 - 0.3f), Vector3.zero, 1.6f);
                LampGlow(t, lamp, 0.2f);
            }
            Picture(t, "dream_pic_photo_1", new Vector3(-5.2f, F + 1.7f, DreamHouse.HubZ1 - 0.012f), Vector3.back, 0.8f, 0.62f, null, true);
            Model(t, "pottedPlant", new Vector3(-1.75f, F, 81.1f), Vector3.zero);
        }

        /// <summary>The bath: the sink and its mirror, the toilet, the tub's curtain on its rail, a bath mat.</summary>
        static void Bath()
        {
            var t = Group("Bath");
            Model(t, "bathroomSink", new Vector3(1.18f, F, 81.4f), Vector3.left);
            DreamEscapeBuilder.Quad("Mirror", t, new Vector3(1.37f, F + 1.55f, 81.4f), Vector3.left, 0.5f, 0.6f, DreamEscapeBuilder.Pastel("DecorMirror", new Color(0.82f, 0.9f, 1f), 0f, 0.35f));
            Model(t, "toilet", new Vector3(1.15f, F, 82.6f), Vector3.left);
            Rug(t, "rugDoormat", new Vector3(0.1f, F, 82.75f), 1.0f, 0.5f, new Color(0.7f, 0.9f, 0.9f));
            DreamEscapeBuilder.Box("Curtain rail", t, new Vector3(0.1f, F + 2.05f, DreamHouse.HubZ1 - 0.8f), new Vector3(2.6f, 0.04f, 0.04f), DreamEscapeBuilder.Pastel("DecorChrome", new Color(0.85f, 0.88f, 0.92f), 0.01f));
            var curtain = DreamEscapeBuilder.Pastel("DecorShowerCurtain", new Color(0.98f, 0.82f, 0.86f), 0.01f);
            for (int i = 0; i < 4; i++)   // drawn half across, in folds
                DreamEscapeBuilder.Box("Curtain fold", t, new Vector3(-1.0f + i * 0.28f, F + 1.15f, DreamHouse.HubZ1 - 0.8f + (i % 2 == 0 ? 0.05f : -0.05f)), new Vector3(0.28f, 1.8f, 0.06f), curtain);
        }

        /// <summary>Theo's room: the round rug, the teddy on his bed, the bedside light, his desk's chair and the drawing
        /// pinned over it, the toy chest, toys under the window, and glow-in-the-dark stars on his ceiling (his mother's).</summary>
        static void TheosRoom()
        {
            var t = Group("Theo's room");
            Rug(t, "rugRound", new Vector3(4.7f, F, 82.3f), 2.0f, 2.0f, new Color(0.98f, 0.8f, 0.45f));
            Pendant(t, new Vector3(4.8f, F, 82.2f), 2.6f);
            Model(t, "bear", new Vector3(6.2f, F + 0.42f, DreamHouse.HubZ1 - 0.4f), Vector3.back, 1.1f);
            var nightLight = Model(t, "lampRoundTable", new Vector3(7.7f, F + 0.75f, 82.85f), Vector3.zero, 1.3f);
            LampGlow(t, nightLight, 0.18f);
            Model(t, "chairDesk", new Vector3(6.95f, F, 82.65f), Vector3.right);
            Picture(t, "dream_pic_kids_drawing_2", new Vector3(DreamHouse.HubX1 - 0.012f, F + 1.55f, 82.65f), Vector3.left, 0.36f, 0.48f, null);
            Model(t, "cardboardBoxClosed", new Vector3(3.4f, F, 81.2f), Vector3.back, 2.6f, new Color(0.45f, 0.7f, 0.9f));
            Toys(t, new Vector3(3.9f, F, 83.55f));
            Wardrobe(t);
            Books(t, new Vector3(7.72f, F, 81.6f), true, new[] { 0.42f, 0.86f, 1.3f });
            // Glow-in-the-dark stars on the ceiling.
            var star = DreamEscapeBuilder.Glow("TheoStar", new Color(0.92f, 1f, 0.75f), 0.9f, 0f, 0.25f);
            var rnd = new System.Random(11);
            for (int i = 0; i < 22; i++)
            {
                float x = 3.3f + 4.5f * (float)rnd.NextDouble(), z = 81.0f + 2.8f * (float)rnd.NextDouble();
                DreamEscapeBuilder.Quad("Ceiling star", t, new Vector3(x, F + 2.595f + DreamHouse.CeilingRaise, z), Vector3.down, 0.09f, 0.09f, star);
            }
        }

        /// <summary>Theo's wardrobe (a hiding spot; its sides and roof are the gameplay pieces): a back, both doors standing
        /// open into the room, and his clothes on the rail (small, a child's).</summary>
        static void Wardrobe(Transform parent)
        {
            var t = new GameObject("Theo's wardrobe (doors and clothes)").transform;
            t.SetParent(parent, false);
            var paint = DreamEscapeBuilder.Pastel("HouseWardrobe", new Color(0.62f, 0.78f, 0.86f), 0.04f);
            var knob = DreamEscapeBuilder.Pastel("DecorBrassKnob", new Color(0.95f, 0.78f, 0.38f), 0.01f);
            float z0 = DreamHouse.HallZ1 + 0.2f, z1 = 81.7f, x0 = 5.4f, x1 = 6.8f, top = F + 2.1f;
            DreamEscapeBuilder.Box("Wardrobe back", t, new Vector3((x0 + x1) * 0.5f, F + 1.05f, z0 + 0.02f), new Vector3(x1 - x0, 2.1f, 0.04f), paint);
            DreamEscapeBuilder.Box("Wardrobe plinth", t, new Vector3((x0 + x1) * 0.5f, F + 0.05f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.1f, z1 - z0), paint);
            // The doors: each 0.7 wide, hinged at the front corners, swung open square to the front (into the room).
            foreach (float hx in new[] { x0 + 0.03f, x1 - 0.03f })
            {
                DreamEscapeBuilder.Box("Wardrobe door", t, new Vector3(hx, F + 1.08f, z1 + 0.35f), new Vector3(0.04f, 1.96f, 0.68f), paint);
                DreamEscapeBuilder.Box("Wardrobe door panel", t, new Vector3(hx, F + 1.45f, z1 + 0.35f), new Vector3(0.05f, 0.8f, 0.5f), DreamEscapeBuilder.Pastel("HouseWardrobePanel", new Color(0.7f, 0.84f, 0.9f), 0.02f));
                Prim(t, "Wardrobe knob", PrimitiveType.Sphere, new Vector3(hx + (hx < 6f ? 0.04f : -0.04f), F + 1.05f, z1 + 0.62f), Vector3.one * 0.05f, knob);
            }
            // The rail and his clothes.
            DreamEscapeBuilder.Box("Wardrobe rail", t, new Vector3((x0 + x1) * 0.5f, top - 0.3f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 - 0.4f, 0.04f, 0.04f),
                DreamEscapeBuilder.Pastel("HouseRail", new Color(0.62f, 0.46f, 0.38f), 0.01f));
            var colours = new[] { new Color(0.95f, 0.75f, 0.3f), new Color(0.45f, 0.62f, 0.9f), new Color(0.9f, 0.5f, 0.45f), new Color(0.55f, 0.78f, 0.55f), new Color(0.98f, 0.95f, 0.9f) };
            var shirt = DreamProps.RoundedBox("Child's shirt", new Vector3(0.36f, 0.6f, 0.06f), 0.03f);
            for (int i = 0; i < colours.Length; i++)
            {
                float x = x0 + 0.42f + i * 0.14f;
                var go = DreamProps.Show("Child's shirt", shirt, DreamProps.MeshLook("Shirt" + i, colours[i], 0.01f), t, Vector3.zero, Quaternion.Euler(0f, 90f + (i % 2 == 0 ? 8f : -8f), 0f));
                go.transform.position = new Vector3(x, top - 0.65f, (z0 + z1) * 0.5f);
            }
        }

        /// <summary>The attic: Lena's observatory at the west end (a proposal: the telescope at the round window, her
        /// chair, star charts, her notebook), the family's stored life to the east (boxes, Theo's crib, the old bike, a
        /// rocking horse, the Christmas box).</summary>
        static void Attic()
        {
            var t = Group("Attic");
            float FA = DreamHouse.AtticFloor;
            // The observatory: a rug on the platform, the telescope on its tripod aimed at the window, her chair and table.
            Rug(t, "rugRound", new Vector3(-6.3f, FA + 0.3f, 78f), 1.3f, 2.8f, new Color(0.42f, 0.44f, 0.72f));
            var brass = DreamProps.MeshLook("DecorBrass", new Color(0.95f, 0.78f, 0.38f), 0.004f);
            var tube = DreamProps.MeshLook("DecorTelescope", new Color(0.3f, 0.32f, 0.55f), 0.006f);
            var feet = new Vector3(-6.2f, FA + 0.3f, 78f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var leg = new Vector3(Mathf.Cos(a) * 0.35f, 0f, Mathf.Sin(a) * 0.35f);
                var mid = feet + leg * 0.5f + Vector3.up * 0.6f;
                Prim(t, "Tripod leg", PrimitiveType.Cylinder, mid, new Vector3(0.04f, 0.62f, 0.04f), brass, Quaternion.FromToRotation(Vector3.up, (Vector3.up * 1.2f - leg).normalized));
            }
            Prim(t, "Telescope", PrimitiveType.Cylinder, feet + new Vector3(-0.15f, 1.35f, 0f), new Vector3(0.16f, 0.6f, 0.16f), tube, Quaternion.Euler(0f, 0f, 62f));
            Prim(t, "Telescope eyepiece", PrimitiveType.Cylinder, feet + new Vector3(0.38f, 1.08f, 0f), new Vector3(0.06f, 0.08f, 0.06f), brass, Quaternion.Euler(0f, 0f, 62f));
            Model(t, "loungeChair", new Vector3(-4.6f, FA, 77.4f), Vector3.left);
            Model(t, "sideTable", new Vector3(-4.6f, FA, 78.75f), Vector3.left, 1.3f);
            DreamEscapeBuilder.Box("Lena's notebook", t, new Vector3(-4.6f, FA + 0.52f, 78.75f), new Vector3(0.22f, 0.03f, 0.3f), DreamEscapeBuilder.Pastel("DecorNotebook", new Color(0.36f, 0.26f, 0.22f), 0.004f));
            var mug = DreamProps.MeshLook("DecorMug", new Color(0.98f, 0.95f, 0.9f), 0.004f);
            Prim(t, "Mug (cold)", PrimitiveType.Cylinder, new Vector3(-4.45f, FA + 0.56f, 78.6f), new Vector3(0.09f, 0.05f, 0.09f), mug);
            // Star charts pinned to the gable and the rafters.
            for (int i = 0; i < 3; i++)
                StarChart(t, new Vector3(DreamHouse.HubX0 + 0.012f, FA + 1.2f + (i == 1 ? 0.5f : 0f), 75.6f + i * 0.9f + (i == 2 ? 2.6f : 0f)), Vector3.right, 7 + i);
            StarChart(t, new Vector3(DreamHouse.ObsX1 - 0.02f, FA + 3.2f, 76.6f), Vector3.left, 21);
            // The stored life: boxes on and round the crates and the trunk, the old bike, a rocking horse, the Christmas box.
            Model(t, "cardboardBoxClosed", new Vector3(5.3f, FA + 1.2f, 74.9f), Vector3.back, 2.2f);
            Model(t, "cardboardBoxOpen", new Vector3(6.4f, FA, 74.4f), Vector3.back, 2.2f);
            Model(t, "cardboardBoxClosed", new Vector3(2.6f, FA, 74.2f), Vector3.back, 2.0f, new Color(0.86f, 0.42f, 0.45f));
            var tinsel = DreamEscapeBuilder.Glow("DecorTinsel", new Color(0.6f, 0.95f, 0.65f), 0.8f, 0f, 0.3f);
            Prim(t, "Tinsel", PrimitiveType.Sphere, new Vector3(2.6f, FA + 0.6f, 74.2f), new Vector3(0.45f, 0.12f, 0.35f), tinsel);
            // Elias's old bike (Walt fixed it), leaning on the south knee wall.
            var bike = new GameObject("The old bike").transform;
            bike.SetParent(t, false);
            bike.position = new Vector3(0.2f, FA + 0.36f, 72.6f);
            bike.rotation = Quaternion.Euler(0f, 90f, 8f);
            DreamProps.Show("Bike frame", DreamProps.BikeFrame(), DreamProps.MeshLook("DecorOldBike", new Color(0.93f, 0.5f, 0.55f), 0.005f), bike, Vector3.zero, Quaternion.identity);
            var tyre = DreamProps.Ring("Bike tyre", 0.33f, 0.028f);
            var tyreMat = DreamProps.MeshLook("Tyre", new Color(0.36f, 0.3f, 0.46f), 0.006f);
            DreamProps.Show("Bike wheel", tyre, tyreMat, bike, new Vector3(0f, 0f, -0.55f), Quaternion.identity);
            DreamProps.Show("Bike wheel", tyre, tyreMat, bike, new Vector3(0f, 0f, 0.55f), Quaternion.identity);
            RockingHorse(t, new Vector3(-1.6f, FA, 76.6f));
            // A stack of old picture frames against the north knee wall, faces to the wall.
            for (int i = 0; i < 4; i++)
                DreamEscapeBuilder.Box("Old frame (back)", t, new Vector3(-1.2f + i * 0.08f, FA + 0.45f, 83.9f - i * 0.05f), new Vector3(0.06f, 0.8f - i * 0.1f, 0.7f),
                    DreamEscapeBuilder.Pastel("AtticStorage", new Color(0.74f, 0.6f, 0.5f), 0.02f)).transform.rotation = Quaternion.Euler(-14f, 90f, 0f);
            // The way through the wall of stored things: old sheets on a line, two drawn back, one half across, starlight
            // showing round them (the observatory beyond).
            var lineMat = DreamEscapeBuilder.Pastel("HouseRail", new Color(0.62f, 0.46f, 0.38f), 0.01f);
            float lx = -2.55f, ly = FA + 2.05f;
            DreamEscapeBuilder.Box("Washing line", t, new Vector3(lx, ly, 78.5f), new Vector3(0.02f, 0.02f, 3.2f), lineMat);
            var sheets = new[] { new Color(0.97f, 0.9f, 0.88f), new Color(0.86f, 0.9f, 0.98f), new Color(0.96f, 0.94f, 0.84f) };
            var spans = new[] { (77.0f, 77.75f, 1.9f), (79.35f, 80.0f, 1.9f), (77.75f, 78.45f, 1.25f) };   // (z0, z1, drop)
            for (int i = 0; i < spans.Length; i++)
            {
                var (sz0, sz1, drop) = spans[i];
                var m = DreamEscapeBuilder.Pastel("AtticSheet" + i, sheets[i], 0.01f);
                int folds = Mathf.Max(2, Mathf.RoundToInt((sz1 - sz0) / 0.18f));
                float w = (sz1 - sz0) / folds;
                for (int k = 0; k < folds; k++)
                    DreamEscapeBuilder.Box("Sheet", t, new Vector3(lx + (k % 2 == 0 ? 0.03f : -0.03f), ly - drop * 0.5f, sz0 + w * (k + 0.5f)), new Vector3(0.03f, drop, w * 1.05f), m);
                foreach (float pz in new[] { sz0 + 0.06f, sz1 - 0.06f })
                    DreamEscapeBuilder.Box("Peg", t, new Vector3(lx, ly + 0.02f, pz), new Vector3(0.05f, 0.08f, 0.03f), DreamEscapeBuilder.Pastel("DecorPeg", new Color(0.95f, 0.6f, 0.55f), 0.01f));
            }
            // Fairy lights along the ridge from the stairs, past the sheets and on into the observatory: they lead the eye
            // (and her) west, to the stars.
            var fairy = DreamEscapeBuilder.Glow("AtticFairyLight", new Color(1f, 0.88f, 0.6f), 1.2f, 0f, 0.15f);
            for (float x = 2.4f; x > DreamHouse.HubX0 + 0.6f; x -= 0.3f)
            {
                float sag = 0.18f * Mathf.Sin(Mathf.Repeat((2.4f - x) / 1.2f, 1f) * Mathf.PI);   // drooping between rafters
                // Under the ridge on the storage side; in the observatory, under its higher ceiling, round the skylight.
                bool obs = x < DreamHouse.ObsX1;
                float y = obs ? FA + DreamHouse.ObsHeight - 0.45f - sag * 1.6f : FA + DreamHouse.RidgeHeight - 0.3f - sag;
                if (obs && x > DreamHouse.SkyX0 - 0.1f && x < DreamHouse.SkyX1 + 0.1f) continue;
                Prim(t, "Fairy light", PrimitiveType.Sphere, new Vector3(x, y, DreamHouse.RidgeZ + 0.14f), Vector3.one * 0.07f, fairy);
            }
            // A dim bulb hanging from the ridge in the middle.
            Prim(t, "Bulb (attic)", PrimitiveType.Sphere, new Vector3(1.2f, FA + 2.6f, DreamHouse.RidgeZ), Vector3.one * 0.2f,
                DreamEscapeBuilder.Glow("StairBulb", new Color(1f, 0.86f, 0.6f), 1.0f, 0f, 0.1f));
        }

        // ---------- Pieces ----------

        /// <summary>A pinned star chart: deep blue paper, white stars joined into a constellation.</summary>
        static void StarChart(Transform parent, Vector3 at, Vector3 normal, int seed)
        {
            var paper = DreamEscapeBuilder.Pastel("DecorChart", new Color(0.24f, 0.28f, 0.52f), 0.01f);
            DreamEscapeBuilder.Quad("Star chart", parent, at, normal, 0.6f, 0.45f, paper);
            var star = DreamEscapeBuilder.Glow("AtticStar", new Color(1f, 0.96f, 0.82f), 1.4f, 0f, 0.2f);
            Vector3 across = Mathf.Abs(normal.x) > 0.5f ? Vector3.forward : Vector3.right;
            var rnd = new System.Random(seed);
            for (int i = 0; i < 6; i++)
            {
                var p = at + normal * 0.006f + across * (((float)rnd.NextDouble() - 0.5f) * 0.5f) + Vector3.up * (((float)rnd.NextDouble() - 0.5f) * 0.36f);
                Prim(parent, "Chart star", PrimitiveType.Sphere, p, Vector3.one * 0.025f, star);
            }
        }

        /// <summary>A small rocking horse: a body, a head, two rockers.</summary>
        static void RockingHorse(Transform parent, Vector3 floor)
        {
            var wood = DreamProps.MeshLook("DecorHorse", new Color(0.95f, 0.85f, 0.7f), 0.006f);
            var mane = DreamProps.MeshLook("DecorMane", new Color(0.86f, 0.42f, 0.45f), 0.004f);
            DreamProps.Show("Horse body", DreamProps.RoundedBox("Horse body", new Vector3(0.22f, 0.24f, 0.6f), 0.1f), wood, parent, Vector3.zero, Quaternion.identity).transform.position = floor + new Vector3(0f, 0.55f, 0f);
            DreamProps.Show("Horse head", DreamProps.RoundedBox("Horse head", new Vector3(0.16f, 0.3f, 0.22f), 0.07f), wood, parent, Vector3.zero, Quaternion.Euler(-25f, 0f, 0f)).transform.position = floor + new Vector3(0f, 0.78f, 0.32f);
            Prim(parent, "Mane", PrimitiveType.Cube, floor + new Vector3(0f, 0.86f, 0.24f), new Vector3(0.05f, 0.2f, 0.12f), mane);
            foreach (float x in new[] { -0.12f, 0.12f })
                Prim(parent, "Rocker", PrimitiveType.Cylinder, floor + new Vector3(x, 0.1f, 0f), new Vector3(0.05f, 0.42f, 0.05f), wood, Quaternion.Euler(80f, 0f, 0f));
            foreach (var leg in new[] { new Vector3(-0.1f, 0f, -0.2f), new Vector3(0.1f, 0f, -0.2f), new Vector3(-0.1f, 0f, 0.2f), new Vector3(0.1f, 0f, 0.2f) })
                Prim(parent, "Horse leg", PrimitiveType.Cylinder, floor + leg + new Vector3(0f, 0.3f, 0f), new Vector3(0.04f, 0.18f, 0.04f), wood);
        }

        // ---------- Pieces ----------

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root, false);
            return t;
        }

        /// <summary>A Kenney model with its bottom at <paramref name="at"/>, turned so its front faces
        /// <paramref name="facing"/> (zero: as it comes), at <paramref name="scale"/> times Kenney's size. Optional tint
        /// replaces its colours.</summary>
        static GameObject Model(Transform parent, string name, Vector3 at, Vector3 facing, float scale = Kenney, Color? tint = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DreamHouseDressing.KenneyDir + "/" + name + ".fbx");
            if (prefab == null) { if (!_missing.Contains(name)) _missing.Add(name); return null; }
            float turn = facing == Vector3.zero ? float.NaN : DreamHouseDressing.TurnFor(prefab, facing);
            if (float.IsNaN(turn) && facing != Vector3.zero)
            {
                // No clear back (a cupboard, a box): Kenney's models share one front, so turn it as the chair turns.
                var chair = AssetDatabase.LoadAssetAtPath<GameObject>(DreamHouseDressing.KenneyDir + "/chair.fbx");
                if (chair != null) turn = DreamHouseDressing.TurnFor(chair, facing);
            }
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            var model = Object.Instantiate(prefab);
            model.name = name;
            model.transform.SetParent(pivot, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, float.IsNaN(turn) ? 0f : turn, 0f);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            pivot.localScale = Vector3.one * scale * UnitFactor();
            var b = DreamHouseDressing.WorldBounds(model);
            pivot.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            Restyle(model, tint);
            return model;
        }

        static float _unit;

        /// <summary>Kenney's sizes in this file are the kit's own units (its chair is 0.47 tall); the imported FBX can come in
        /// at another scale (the first capture showed every piece many times too big). Measured once from the chair.</summary>
        static float UnitFactor()
        {
            if (_unit > 0f) return _unit;
            _unit = 1f;
            var chair = AssetDatabase.LoadAssetAtPath<GameObject>(DreamHouseDressing.KenneyDir + "/chair.fbx");
            if (chair == null) return _unit;
            var probe = Object.Instantiate(chair);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            probe.transform.localScale = Vector3.one;
            float h = DreamHouseDressing.WorldBounds(probe).size.y;
            Object.DestroyImmediate(probe);
            if (h > 1e-4f) _unit = 0.47f / h;
            Debug.Log("JAMKIT_HOUSE art: Kenney unit factor " + _unit.ToString("0.####") + " (the chair imports " + h.ToString("0.###") + " tall)");
            return _unit;
        }

        /// <summary>A rug: Kenney's rug squeezed to <paramref name="width"/> (x) by <paramref name="depth"/> (z), a hair above
        /// the floor, in its own colour.</summary>
        static void Rug(Transform parent, string name, Vector3 centre, float width, float depth, Color colour)
        {
            var rug = Model(parent, name, centre, Vector3.zero, 1f, colour);
            if (rug == null) return;
            var pivot = rug.transform.parent;
            var b = DreamHouseDressing.WorldBounds(rug);
            pivot.localScale = Vector3.Scale(pivot.localScale, new Vector3(width / Mathf.Max(b.size.x, 1e-3f), 1.5f, depth / Mathf.Max(b.size.z, 1e-3f)));
            b = DreamHouseDressing.WorldBounds(rug);
            pivot.position += new Vector3(centre.x - b.center.x, centre.y + 0.006f - b.min.y, centre.z - b.center.z);
        }

        static void Restyle(GameObject model, Color? tint)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int k = 0; k < mats.Length; k++)
                    mats[k] = tint.HasValue ? DreamProps.MeshLook("DecorTint_" + ColorUtility.ToHtmlStringRGB(tint.Value), tint.Value, 0.008f)
                        : DreamHouseDressing.Pastelise(mats[k]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            GameObjectUtility.SetStaticEditorFlags(model, 0);
        }

        static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 at, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var go = DreamEscapeBuilder.Part(name, type, parent, Vector3.zero, scale, mat);
            go.transform.position = at;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            return go;
        }

        /// <summary>A soft warm glow at a lamp's shade (the dream's lights are painted, not lit).</summary>
        static void LampGlow(Transform parent, GameObject lamp, float size)
        {
            if (lamp == null) return;
            var b = DreamHouseDressing.WorldBounds(lamp);
            Prim(parent, "Lamp glow", PrimitiveType.Sphere, new Vector3(b.center.x, b.max.y - b.size.y * 0.18f, b.center.z), Vector3.one * size * 3f,
                DreamEscapeBuilder.Glow("DecorLampGlow", new Color(1f, 0.86f, 0.6f), 0.55f, 0f, 0.1f));
        }

        /// <summary>A pendant lamp hanging from a ceiling <paramref name="ceiling"/> m over <paramref name="floor"/>: a cord,
        /// a cream shade, a soft glow.</summary>
        static void Pendant(Transform parent, Vector3 floor, float ceiling)
        {
            var cord = DreamProps.MeshLook("DecorCord", new Color(0.36f, 0.3f, 0.46f), 0f);
            var shade = DreamProps.MeshLook("DecorShade", new Color(0.99f, 0.93f, 0.8f), 0.01f);
            // The plan's ceiling plus the raise (round 4); a longer cord, so the shade hangs at the same height as before.
            ceiling += DreamHouse.CeilingRaise;
            float drop = 0.2f + DreamHouse.CeilingRaise * 0.5f;
            Prim(parent, "Pendant cord", PrimitiveType.Cylinder, floor + new Vector3(0f, ceiling - drop, 0f), new Vector3(0.02f, drop, 0.02f), cord);
            Prim(parent, "Pendant shade", PrimitiveType.Cylinder, floor + new Vector3(0f, ceiling - drop * 2f - 0.08f, 0f), new Vector3(0.5f, 0.09f, 0.5f), shade);
            Prim(parent, "Pendant glow", PrimitiveType.Sphere, floor + new Vector3(0f, ceiling - drop * 2f - 0.18f, 0f), Vector3.one * 0.5f,
                DreamEscapeBuilder.Glow("DecorLampGlow", new Color(1f, 0.86f, 0.6f), 0.55f, 0f, 0.1f));
        }

        /// <summary>The fireplace in the chimney breast whose west face is at x = <paramref name="faceX"/>, centred on
        /// <paramref name="z"/> (facing the room, -x): a dark firebox with logs and the fire's glow, a hearth, a white mantel
        /// with a clock, candles, the siblings' photo and a plant, and a mirror above.</summary>
        static void Fireplace(Transform parent, float faceX, float z)
        {
            var t = new GameObject("Fireplace").transform;
            t.SetParent(parent, false);
            var n = Vector3.left;   // into the room
            Vector3 P(float out_, float y, float along) => new Vector3(faceX - out_, F + y, z + along);
            var white = DreamEscapeBuilder.Pastel("HouseTrim", DreamEscapeBuilder.Palette[0], 0.03f);
            var soot = DreamEscapeBuilder.Pastel("DecorSoot", new Color(0.3f, 0.25f, 0.36f), 0.02f);
            DreamEscapeBuilder.Quad("Firebox", t, P(0.04f, 0.45f, 0f), n, 1.1f, 0.85f, soot);
            DreamEscapeBuilder.Box("Hearth", t, P(0.3f, 0.04f, 0f), new Vector3(0.6f, 0.08f, 2.0f), white);
            // The surround: two pilasters and the mantel shelf.
            foreach (float a in new[] { -0.7f, 0.7f })
                DreamEscapeBuilder.Box("Mantel pilaster", t, P(0.06f, 0.6f, a), new Vector3(0.12f, 1.2f, 0.18f), white);
            float mantel = 1.22f;
            DreamEscapeBuilder.Box("Mantel", t, P(0.14f, mantel, 0f), new Vector3(0.28f, 0.08f, 1.8f), white);
            var log = DreamProps.MeshLook("DecorLog", new Color(0.55f, 0.36f, 0.3f), 0.006f);
            for (int i = 0; i < 3; i++)
                Prim(t, "Log", PrimitiveType.Cylinder, P(0.14f, 0.12f + (i == 1 ? 0.12f : 0f), -0.25f + i * 0.25f), new Vector3(0.12f, 0.28f, 0.12f), log, Quaternion.Euler(90f, 0f, 0f));
            Prim(t, "Fire", PrimitiveType.Sphere, P(0.15f, 0.34f, 0f), new Vector3(0.28f, 0.55f, 0.85f), DreamEscapeBuilder.Glow("DecorFire", new Color(1f, 0.62f, 0.35f), 1.2f, 0f, 0.35f));
            // On the mantel: a clock in the middle, the siblings' photo, two candles, a little plant.
            Clock(t, P(0.18f, mantel + 0.26f, 0f), n, 0.17f, true);
            Picture(t, "dream_pic_siblings", P(0.18f, mantel + 0.22f, 0.62f), n, 0.34f, 0.26f, null, true, standing: true);
            var wax = DreamProps.MeshLook("DecorCandle", new Color(0.99f, 0.96f, 0.88f), 0.004f);
            foreach (float a in new[] { -0.45f, -0.62f })
            {
                Prim(t, "Candle", PrimitiveType.Cylinder, P(0.14f, mantel + 0.16f, a), new Vector3(0.06f, 0.12f, 0.06f), wax);
                Prim(t, "Candle flame", PrimitiveType.Sphere, P(0.14f, mantel + 0.31f, a), Vector3.one * 0.12f, DreamEscapeBuilder.Glow("DecorFlame", new Color(1f, 0.82f, 0.45f), 1.1f, 0f, 0.3f));
            }
            Model(t, "plantSmall1", P(0.14f, mantel + 0.04f, -0.82f), Vector3.zero, 2.2f);
            // The mirror above, framed.
            var pane = DreamEscapeBuilder.Pastel("DecorMirror", new Color(0.82f, 0.9f, 1f), 0f, 0.35f);
            DreamEscapeBuilder.Quad("Mirror", t, P(0.04f, 2.1f, 0f), n, 1.0f, 0.75f, pane);
            DreamEscapeBuilder.Box("Mirror frame", t, P(0.055f, 2.1f + 0.4f, 0f), new Vector3(0.04f, 0.06f, 1.12f), white);
            DreamEscapeBuilder.Box("Mirror frame", t, P(0.055f, 2.1f - 0.4f, 0f), new Vector3(0.04f, 0.06f, 1.12f), white);
            DreamEscapeBuilder.Box("Mirror frame", t, P(0.055f, 2.1f, 0.53f), new Vector3(0.04f, 0.86f, 0.06f), white);
            DreamEscapeBuilder.Box("Mirror frame", t, P(0.055f, 2.1f, -0.53f), new Vector3(0.04f, 0.86f, 0.06f), white);
        }

        /// <summary>A round wall clock (or a standing mantel clock) facing <paramref name="normal"/>.</summary>
        static void Clock(Transform parent, Vector3 at, Vector3 normal, float radius = 0.22f, bool standing = false)
        {
            var face = DreamProps.MeshLook("DecorClockFace", new Color(0.99f, 0.97f, 0.92f), 0.006f);
            var rim = DreamProps.MeshLook("DecorClockRim", new Color(0.55f, 0.4f, 0.55f), 0.006f);
            var ink = DreamProps.MeshLook("DecorInk", new Color(0.36f, 0.3f, 0.46f), 0f);
            var rot = Quaternion.LookRotation(normal) * Quaternion.Euler(90f, 0f, 0f);   // a cylinder's axis along the normal
            Prim(parent, "Clock rim", PrimitiveType.Cylinder, at, new Vector3(radius * 2.2f, 0.03f, radius * 2.2f), rim, rot);
            Prim(parent, "Clock face", PrimitiveType.Cylinder, at + normal * 0.012f, new Vector3(radius * 2f, 0.03f, radius * 2f), face, rot);
            var up = Vector3.up; var side = Vector3.Cross(normal, up);
            Prim(parent, "Clock hand (hour)", PrimitiveType.Cube, at + normal * 0.05f + up * radius * 0.25f, new Vector3(0.025f, radius * 0.5f, 0.01f), ink, Quaternion.LookRotation(normal));
            Prim(parent, "Clock hand (minute)", PrimitiveType.Cube, at + normal * 0.05f + side * radius * 0.35f, new Vector3(radius * 0.7f, 0.018f, 0.01f), ink, Quaternion.LookRotation(normal));
            if (standing) Prim(parent, "Clock foot", PrimitiveType.Cube, at - up * (radius + 0.03f), new Vector3(radius * 1.6f, 0.05f, 0.1f), rim);
        }

        /// <summary>A picture from Art/Dream/ (or nothing if it's missing) in a thin frame, on a wall facing
        /// <paramref name="normal"/>, or standing on a shelf (leaning back a little).</summary>
        static void Picture(Transform parent, string art, Vector3 at, Vector3 normal, float w, float h, Material fallback, bool framed = false, bool standing = false)
        {
            var mat = DreamHouseBuilder.Picture(art, fallback);
            if (mat == null) { if (!_missing.Contains(art)) _missing.Add(art); return; }
            var q = DreamEscapeBuilder.Quad(art, parent, at + normal * 0.006f, normal, w, h, mat);
            if (standing) q.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(-12f, 0f, 0f);
            if (!framed) return;
            var wood = DreamEscapeBuilder.Pastel("HousePhotoFrame", new Color(0.62f, 0.46f, 0.38f), 0.01f);
            var frame = new GameObject(art + " (frame)").transform;
            frame.SetParent(parent, false);
            frame.position = at;
            frame.rotation = q.transform.rotation;
            float bar = 0.04f;
            foreach (var (pos, size) in new[] {
                (new Vector3(0f, h * 0.5f + bar * 0.5f, 0f), new Vector3(w + bar * 2f, bar, 0.025f)),
                (new Vector3(0f, -h * 0.5f - bar * 0.5f, 0f), new Vector3(w + bar * 2f, bar, 0.025f)),
                (new Vector3(w * 0.5f + bar * 0.5f, 0f, 0f), new Vector3(bar, h, 0.025f)),
                (new Vector3(-w * 0.5f - bar * 0.5f, 0f, 0f), new Vector3(bar, h, 0.025f)) })
            {
                var b = DreamEscapeBuilder.Box("Frame", frame, Vector3.zero, size, wood);
                b.transform.localPosition = pos;
                b.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>A small square note held by a magnet.</summary>
        static void Note(Transform parent, Vector3 at, Vector3 normal, Color colour)
        {
            DreamEscapeBuilder.Quad("Note", parent, at + normal * 0.006f, normal, 0.16f, 0.16f, DreamEscapeBuilder.Pastel("DecorNote" + ColorUtility.ToHtmlStringRGB(colour), colour, 0f));
            Prim(parent, "Magnet", PrimitiveType.Sphere, at + normal * 0.02f + Vector3.up * 0.06f, Vector3.one * 0.04f, DreamProps.MeshLook("DecorMagnet", new Color(0.88f, 0.42f, 0.45f), 0f));
        }

        /// <summary>A bowl of fruit (a cream bowl, oranges and apples) on a surface at <paramref name="at"/>.</summary>
        static void FruitBowl(Transform parent, Vector3 at)
        {
            Prim(parent, "Fruit bowl", PrimitiveType.Sphere, at + Vector3.up * 0.03f, new Vector3(0.36f, 0.14f, 0.36f), DreamProps.MeshLook("DecorBowl", new Color(0.98f, 0.95f, 0.9f), 0.006f));
            var orange = DreamProps.MeshLook("DecorOrange", new Color(0.98f, 0.65f, 0.3f), 0.004f);
            var apple = DreamProps.MeshLook("DecorApple", new Color(0.86f, 0.32f, 0.36f), 0.004f);
            var spots = new[] { new Vector3(-0.07f, 0.1f, -0.05f), new Vector3(0.07f, 0.1f, -0.03f), new Vector3(0f, 0.1f, 0.07f), new Vector3(0.01f, 0.17f, 0f) };
            for (int i = 0; i < spots.Length; i++)
                Prim(parent, "Fruit", PrimitiveType.Sphere, at + spots[i], Vector3.one * 0.1f, i % 2 == 0 ? orange : apple);
        }

        /// <summary>Rows of books on a bookcase's shelves (the bookcase's centre at <paramref name="at"/>, against an x wall
        /// when <paramref name="alongZ"/>), at the given shelf heights.</summary>
        static void Books(Transform parent, Vector3 at, bool alongZ, float[] shelves)
        {
            var colours = new[] { new Color(0.86f, 0.42f, 0.45f), new Color(0.45f, 0.62f, 0.86f), new Color(0.98f, 0.8f, 0.42f), new Color(0.55f, 0.75f, 0.55f), new Color(0.7f, 0.55f, 0.85f) };
            int k = 0;
            foreach (float y in shelves)
                for (float s = -0.3f; s <= 0.3f; s += 0.075f, k++)
                {
                    float h = 0.2f + 0.06f * ((k * 7) % 3);
                    var c = colours[(k * 3) % colours.Length];
                    var pos = alongZ ? new Vector3(at.x, at.y + y + h * 0.5f, at.z + s) : new Vector3(at.x + s, at.y + y + h * 0.5f, at.z);
                    var size = alongZ ? new Vector3(0.22f, h, 0.06f) : new Vector3(0.06f, h, 0.22f);
                    DreamEscapeBuilder.Box("Book", parent, pos, size, DreamEscapeBuilder.Pastel("DecorBook" + ColorUtility.ToHtmlStringRGB(c), c, 0.01f));
                }
        }

        /// <summary>Theo's toys in a corner: a stack of wooden blocks, a ball, a toy van.</summary>
        static void Toys(Transform parent, Vector3 corner)
        {
            var colours = new[] { new Color(0.86f, 0.42f, 0.45f), new Color(0.45f, 0.62f, 0.86f), new Color(0.98f, 0.8f, 0.42f) };
            for (int i = 0; i < 3; i++)
                Prim(parent, "Block", PrimitiveType.Cube, corner + new Vector3(0.05f * i, 0.09f + i * 0.18f, 0.02f * i), Vector3.one * 0.18f,
                    DreamProps.MeshLook("DecorBlock" + i, colours[i], 0.006f), Quaternion.Euler(0f, i * 17f, 0f));
            Prim(parent, "Ball", PrimitiveType.Sphere, corner + new Vector3(0.5f, 0.14f, 0.6f), Vector3.one * 0.28f, DreamProps.MeshLook("DecorBall", new Color(0.98f, 0.55f, 0.5f), 0.008f));
            DreamProps.Show("Toy van", DreamProps.RoundedBox("Toy van (small)", new Vector3(0.18f, 0.16f, 0.32f), 0.05f), DreamProps.MeshLook("DecorToyVan", new Color(0.45f, 0.72f, 0.86f), 0.006f),
                parent, Vector3.zero, Quaternion.Euler(0f, 30f, 0f)).transform.position = corner + new Vector3(0.9f, 0.1f, 0.1f);
        }
    }
}
