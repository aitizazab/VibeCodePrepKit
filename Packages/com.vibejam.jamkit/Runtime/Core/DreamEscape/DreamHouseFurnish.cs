using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The house lived in: the furniture and dressing (every solid piece a box the model is fitted into: the model roles
    /// are Dream/House/house-assets.json's; a role with no model is built by the builder), the hiding spots, Theo's walking
    /// map, the memories (with the places the house hides them again) and the lamps.
    /// </summary>
    public sealed partial class DreamHouse
    {
        /// <summary>A box anywhere (wall-hung things, things on things).</summary>
        HousePart Box(string name, string model, float x0, float x1, float y0, float y1, float z0, float z1, float yaw = 0f, HouseLook look = HouseLook.Furniture, bool solid = true) =>
            Part(name, LabPieceKind.Ledge, look, V(x0, y0, z0), V(x1, y1, z1), solid, false, model, yaw);

        /// <summary>A picture on a wall (a framed photo or drawing; <paramref name="picture"/> is its Art/Dream name).</summary>
        HousePart Picture(string name, string picture, bool alongX, float faceAt, int side, float a0, float a1, float y0, float y1)
        {
            const float d = 0.025f;
            float c0 = side > 0 ? faceAt : faceAt - d, c1 = side > 0 ? faceAt + d : faceAt;
            return alongX
                ? Box(name, picture, a0, a1, y0, y1, c0, c1, side > 0 ? 0f : 180f, HouseLook.Picture)
                : Box(name, picture, c0, c1, y0, y1, a0, a1, side > 0 ? 90f : -90f, HouseLook.Picture);
        }

        /// <summary>A rug (flat on the floor: walked over, not a solid).</summary>
        void Rug(string name, string model, float x0, float x1, float z0, float z1, float floor) =>
            Box(name, model, x0, x1, floor, floor + 0.012f, z0, z1, 0f, HouseLook.Rug, false);

        /// <summary>Hanging cloth (coats, sheets, a shower curtain, long curtains): see-through to the touch, so she can
        /// push into it and hide behind it.</summary>
        void Cloth(string name, string model, float x0, float x1, float y0, float y1, float z0, float z1) =>
            Box(name, model, x0, x1, y0, y1, z0, z1, 0f, HouseLook.Cloth, false);

        /// <summary>A wardrobe you can stand in: its back is the wall, two sides and a top; the front open (its doors
        /// stand open against the sides). <paramref name="openTo"/>: the open side (+z, -z, +x, -x as 0..3).</summary>
        void Wardrobe(string name, float x0, float x1, float z0, float z1, float floor, int openTo, float height = 2.1f)
        {
            const float t = 0.05f;
            bool alongX = openTo <= 1;   // opens toward ±z: its sides are at x0 and x1
            if (alongX)
            {
                Box(name + " (side)", "wardrobe.side", x0, x0 + t, floor, floor + height, z0, z1, 0f, HouseLook.Cupboard);
                Box(name + " (side)", "wardrobe.side", x1 - t, x1, floor, floor + height, z0, z1, 0f, HouseLook.Cupboard);
                Box(name + " (top)", "wardrobe.top", x0 + t, x1 - t, floor + height - t, floor + height, z0, z1, 0f, HouseLook.Cupboard);
            }
            else
            {
                Box(name + " (side)", "wardrobe.side", x0, x1, floor, floor + height, z0, z0 + t, 0f, HouseLook.Cupboard);
                Box(name + " (side)", "wardrobe.side", x0, x1, floor, floor + height, z1 - t, z1, 0f, HouseLook.Cupboard);
                Box(name + " (top)", "wardrobe.top", x0, x1, floor + height - t, floor + height, z0 + t, z1 - t, 0f, HouseLook.Cupboard);
            }
        }

        // =====================================================================================================
        void Furnish()
        {
            const float G = Ground, U = Upper, B = Basement, A = Attic;

            // ---------- The living room: the sofa faces the fire; the armchair in the corner; the window seat.
            _room = "living";
            Furniture("Sofa", "living.sofa", -3.6f, -2.78f, 2.15f, 3.96f, G, 0.75f, -90f);
            Furniture("Coffee table", "living.coffee_table", -5.4f, -4.74f, 2.2f, 4.0f, G, 0.5f, 90f);
            Furniture("Armchair", "living.armchair", -6.6f, -5.6f, 4.7f, 5.89f, G, 1.12f, 180f);
            Furniture("Side table", "living.side_table", -3.45f, -2.9f, 4.2f, 4.75f, G, 0.55f);
            Prop("Lamp (side table)", "bedroom.table_lamp", -3.17f, 4.47f, G + 0.55f, 0.3f, 0.3f, 0.5f);
            Furniture("Floor lamp", "living.floor_lamp", -2.35f, -2.0f, 1.75f, 2.1f, G, 1.6f);
            Furniture("Bookcase (living room)", "living.bookshelf", -4.25f, -2.88f, 5.34f, 5.92f, G, 2.06f, 180f);
            Furniture("Window seat", "living.window_seat", -6.35f, -5.05f, Z0 + e, 0.65f, G, 0.45f);
            Box("Mantel", "living.mantel", -6.75f, -6.45f, G + 1.15f, G + 1.22f, 2.3f, 4.1f, 0f, HouseLook.Chimney);
            Prop("Mantel clock", "living.mantel_clock", -6.6f, 3.2f, G + 1.22f, 0.25f, 0.48f, 0.16f, 90f);
            Prop("Vase (mantel)", "living.vase.1", -6.6f, 2.55f, G + 1.22f, 0.22f, 0.22f, 0.31f);
            Prop("Photo (mantel)", "living.photo_frame", -6.6f, 3.85f, G + 1.22f, 0.1f, 0.22f, 0.27f, 90f);
            Prop("Books (coffee table)", "living.books", -5.07f, 3.4f, G + 0.5f, 0.16f, 0.55f, 0.24f, 90f);
            Prop("Plant (by the window)", "living.plant.3", -2.25f, 0.55f, G, 0.5f, 0.5f, 0.9f);
            Rug("Rug (living room)", "living.rug", -6.2f, -3.7f, 1.7f, 4.6f, G);
            Picture("Painting over the sofa", "dream_pic_childhood_1", false, -1.775f, -1, 2.4f, 3.6f, G + 1.3f, G + 2.1f);

            // ---------- The front hall: the console and mirror, the coat stand, the umbrella stand by the door, the photo
            // wall up the stairs (the four of them at the lake is one of the memories).
            _room = "hall";
            Furniture("Hall console", "hall.console", 1.22f, 1.6f, 3.7f, 4.08f, G, 0.76f);
            Box("Hall mirror", "hall.mirror", 1.595f, 1.625f, G + 1.1f, G + 1.84f, 3.65f, 4.14f, -90f);
            Furniture("Coat stand", "hall.coat_rack", 1.05f, 1.45f, 0.35f, 0.75f, G, 1.8f);
            Furniture("Umbrella stand", "hall.umbrella_stand", 0.72f, 0.94f, 0.22f, 0.44f, G, 0.62f);
            Rug("Doormat", "hall.doormat", -0.45f, 0.45f, 0.2f, 0.75f, G);
            Rug("Hall runner", "living.rug", -0.25f, 0.95f, 1.0f, 5.6f, G);
            float[] frames = { 2.4f, 3.25f, 4.1f, 4.95f };
            for (int k = 0; k < frames.Length; k++)
            {
                float z = frames[k], stair = (z - MainStair.z0) / (MainStair.z1 - MainStair.z0) * Upper;
                if (k == 1) continue;   // the lake photo is a memory (the builder hangs it there)
                Picture("Photo up the stairs " + (k + 1), "dream_pic_photo_" + (k + 2), false, -1.625f, 1, z - 0.25f, z + 0.25f, stair + 1.25f, stair + 1.75f);
            }

            // ---------- The dining room: the table set for four (the fourth chair is Nadia's), the sideboard.
            _room = "dining";
            table = V(4.55f, G, 2.7f);
            Furniture("Dining table", "dining.table", 3.55f, 5.55f, 2.1f, 3.3f, G, 0.76f);
            Furniture("Chair (Lena's)", "dining.chair", 4.33f, 4.78f, 3.32f, 3.87f, G, 0.96f, 180f);
            Furniture("Chair (Theo's)", "dining.chair", 4.33f, 4.78f, 1.53f, 2.08f, G, 0.96f, 0f);
            Furniture("Chair (Elias's: empty)", "dining.chair", 2.95f, 3.5f, 2.48f, 2.93f, G, 0.96f, 90f);
            Furniture("Chair (Nadia's: the fourth chair)", "dining.chair", 5.6f, 6.15f, 2.48f, 2.93f, G, 0.96f, -90f);
            nadiaChair = V(5.875f, G, 2.7f);
            nadiaChairYaw = -90f;
            theoAt = V(3.75f, G, 1.72f);   // beside his chair, not in it (his cut-out stood through it: house-1.md)
            Prop("Pancakes (Theo's)", "dining.pancakes", 4.55f, 2.3f, G + 0.76f, 0.26f, 0.26f, 0.09f);
            Prop("Pancakes (Lena's)", "dining.pancakes", 4.55f, 3.1f, G + 0.76f, 0.26f, 0.26f, 0.09f);
            Prop("Pancakes (Elias's: going cold)", "dining.pancakes", 3.75f, 2.7f, G + 0.76f, 0.26f, 0.26f, 0.09f);
            Prop("Pancakes (Nadia's)", "dining.pancakes", 5.35f, 2.7f, G + 0.76f, 0.26f, 0.26f, 0.09f);
            Prop("Syrup and the teapot", "dining.dishes", 4.55f, 2.7f, G + 0.76f, 0.7f, 0.36f, 0.17f);
            Furniture("Sideboard", "dining.sideboard", 6.73f, 7.35f, 0.9f, 2.1f, G, 1.18f, -90f);
            Prop("Fruit bowl (sideboard)", "dining.fruit_bowl", 7.04f, 1.5f, G + 1.18f, 0.31f, 0.31f, 0.09f);
            Box("Pendant (dining)", "dining.pendant_lamp", 4.33f, 4.77f, GroundCeil - 0.95f, GroundCeil, 2.48f, 2.92f);
            Picture("Painting (dining)", "dream_pic_childhood_2", false, X1 - e, -1, 3.0f, 4.4f, G + 1.4f, G + 2.2f);
            Rug("Rug (dining)", "living.rug", 2.9f, 6.2f, 1.2f, 4.2f, G);

            // ---------- The kitchen: counters round the north and east walls (the sink under the window, the stove where
            // Lena cooks), the fridge with the calendar, the island, the upper cupboards; the pantry.
            _room = "kitchen";
            Furniture("Fridge", "kitchen.fridge", 1.85f, 2.75f, 10.1f, 10.85f, G, 1.85f, 180f);
            Furniture("Counter (north, west)", "kitchen.counter", 2.8f, 3.6f, 10.25f, 10.85f, G, 0.92f, 180f);
            Furniture("Counter (the sink)", "kitchen.sink_counter", 3.6f, 5.4f, 10.25f, 10.85f, G, 0.92f, 180f);
            Furniture("Counter (east, south)", "kitchen.counter", 6.75f, 7.35f, 5.6f, 6.2f, G, 0.92f, -90f);
            Furniture("Stove", "kitchen.stove", 6.75f, 7.35f, 6.2f, 6.95f, G, 0.92f, -90f);
            Furniture("Counter (east, north)", "kitchen.counter", 6.75f, 7.35f, 6.95f, 7.65f, G, 0.92f, -90f);
            Box("Upper cupboard (west)", "kitchen.cabinet_upper", 2.8f, 3.55f, G + 1.45f, G + 2.2f, 10.5f, 10.85f, 180f);
            Box("Upper cupboard (east)", "kitchen.cabinet_upper", 5.45f, 5.92f, G + 1.45f, G + 2.2f, 10.5f, 10.85f, 180f);
            Box("Upper cupboard (by the stove)", "kitchen.cabinet_upper", 7.0f, 7.35f, G + 1.45f, G + 2.2f, 5.6f, 7.65f, -90f);
            Furniture("Kitchen island", "kitchen.island", 3.4f, 5.2f, 7.6f, 8.5f, G, 0.92f);
            Prop("Kettle", "kitchen.kettle", 3.05f, 10.55f, G + 0.92f, 0.32f, 0.25f, 0.3f, 180f);
            Prop("Toaster", "kitchen.toaster", 5.15f, 10.55f, G + 0.92f, 0.3f, 0.2f, 0.22f, 180f);
            Prop("Pan (on the stove)", "kitchen.pot", 7.05f, 6.4f, G + 0.92f, 0.26f, 0.18f, 0.18f, -90f);
            Prop("Fruit bowl", "kitchen.fruit_bowl", 3.9f, 8.05f, G + 0.92f, 0.31f, 0.31f, 0.09f);
            Prop("Bananas", "kitchen.fruit.2", 4.6f, 8.05f, G + 0.92f, 0.6f, 0.24f, 0.18f);
            Prop("Chopping board", "kitchen.chopping_board", 7.05f, 7.3f, G + 0.92f, 0.25f, 0.45f, 0.04f, 90f);
            Prop("Plant (windowsill)", "kitchen.windowsill_plant", 4.5f, 10.92f, G + 1.05f, 0.17f, 0.12f, 0.27f);
            Box("Pendant (kitchen, west)", "kitchen.pendant_lamp", 3.65f, 4.08f, GroundCeil - 0.95f, GroundCeil, 7.83f, 8.27f);
            Box("Pendant (kitchen, east)", "kitchen.pendant_lamp", 4.52f, 4.95f, GroundCeil - 0.95f, GroundCeil, 7.83f, 8.27f);
            lenaAt = V(6.35f, G, 6.6f);
            _room = "pantry";
            Furniture("Pantry shelves (east)", "pantry.shelves", 7.0f, 7.35f, 9.5f, 10.85f, G, 2.0f, -90f);
            Furniture("Pantry shelves (north)", "pantry.shelves", 6.1f, 7.0f, 10.5f, 10.85f, G, 2.0f, 180f);

            // ---------- The back hall: the coats on their hooks (hide in them), the shoe bench.
            _room = "backhall";
            Cloth("Coats on the hooks", "backhall.coats", 1.25f, 1.62f, G + 0.45f, G + 1.9f, 8.2f, 9.6f);
            Furniture("Shoe bench", "yard.bench", 1.15f, 1.62f, 9.75f, 10.75f, G, 0.45f, -90f);
            Rug("Mat (back door)", "hall.doormat", -0.4f, 0.4f, 10.2f, 10.8f, G);

            // ---------- The den: Elias's desk under the window, the TV and the couch, the bookcase, the drawers, the
            // long curtains at the west window (hide behind them).
            _room = "den";
            Furniture("Elias's desk", "study.desk", -6.4f, -4.4f, 9.9f, 10.85f, G, 0.79f, 180f);
            Furniture("Desk chair", "study.desk_chair", -5.7f, -5.1f, 9.15f, 9.75f, G, 1.0f);
            Prop("Desk lamp", "study.desk_lamp", -6.1f, 10.5f, G + 0.79f, 0.25f, 0.25f, 0.45f);
            Prop("Books (desk)", "study.books", -4.8f, 10.55f, G + 0.79f, 0.55f, 0.16f, 0.24f);
            Furniture("Bookcase (den)", "study.bookshelf", -2.36f, -1.78f, 6.5f, 7.87f, G, 2.06f, -90f);
            Furniture("Couch (den)", "living.sofa", -4.31f, -2.5f, 7.7f, 8.52f, G, 0.75f, 0f);
            Furniture("TV stand", "living.tv_stand", -3.65f, -2.73f, 10.41f, 10.85f, G, 0.53f, 180f);
            Prop("TV", "living.tv", -3.19f, 10.63f, G + 0.53f, 0.4f, 0.35f, 0.41f, 180f);
            Furniture("Drawers (den)", "study.filing_cabinet", -7.3f, -5.65f, 6.1f, 7.05f, G, 1.05f, 0f);
            Cloth("Curtains (den, west)", "den.curtains", -7.35f, -7.12f, G, G + 2.6f, 7.1f, 9.1f);
            Rug("Rug (den)", "living.rug_round", -4.2f, -2.4f, 8.7f, 10.2f, G);

            // ---------- Theo's room: his bed, the wardrobe he hides in, his desk under the bookcase (his drawing on top),
            // the toy chest; the height marks on his door frame (a memory).
            _room = "theo";
            Furniture("Theo's bed", "bedroom.single_bed", -5.9f, -3.9f, 3.95f, 4.92f, U, 0.95f, 90f);
            Wardrobe("Theo's wardrobe", -3.5f, -2.1f, 3.95f, 4.92f, U, 1);
            Furniture("Theo's desk", "bedroom.kid_desk", -4.4f, -3.69f, Z0 + e, 0.7f, U, 0.75f, 180f);
            Furniture("Theo's bookcase", "theo.bookcase", -3.62f, -2.82f, Z0 + e, 0.5f, U, 1.9f, 180f);
            Furniture("Theo's chair", "bedroom.kid_chair", -5.15f, -4.58f, 0.2f, 0.88f, U, 1.0f, 90f);
            Furniture("Toy chest", "bedroom.toy_chest", -7.3f, -6.89f, 0.6f, 1.42f, U, 0.35f, 90f);
            Prop("Teddy (on the toy chest)", "bedroom.teddy_bear", -7.1f, 0.78f, U + 0.35f, 0.3f, 0.25f, 0.35f, 90f);
            Prop("Alarm clock", "bedroom.alarm_clock", -3.85f, 0.45f, U + 0.75f, 0.23f, 0.17f, 0.17f, 180f);
            Prop("Rubber duck (toy chest)", "bedroom.toy", -7.1f, 1.22f, U + 0.35f, 0.21f, 0.3f, 0.29f, 90f);
            Rug("Rug (Theo's room)", "bedroom.rug", -5.8f, -3.4f, 1.6f, 3.6f, U);
            Picture("Theo's drawing (the house)", "dream_pic_kids_drawing_1", true, 4.925f, -1, -6.9f, -6.2f, U + 1.1f, U + 1.6f);
            Picture("Theo's drawing (the four of us)", "dream_pic_kids_drawing_2", true, 4.925f, -1, -3.8f, -3.55f, U + 1.6f, U + 1.95f);

            // ---------- The bathroom: the tub behind its curtain (hide in it), the basin, the toilet.
            _room = "bath";
            Furniture("Bathtub", "bath.bathtub", -4.12f, -3.4f, 5.08f, 6.8f, U, 0.55f, 90f);
            Cloth("Shower curtain", "bath.curtain", -3.42f, -3.36f, U + 0.55f, U + 2.3f, 5.08f, 6.8f);
            Furniture("Basin", "bath.sink", -4.0f, -3.3f, 6.95f, 7.52f, U, 0.85f, 180f);
            Furniture("Toilet", "bath.toilet", -2.9f, -2.45f, 6.95f, 7.52f, U, 0.8f, 180f);
            Box("Bathroom mirror", "bath.mirror", -3.9f, -3.4f, U + 1.2f, U + 1.95f, 7.495f, 7.525f, 180f);

            // ---------- The guest room, made up for her: the bed with her old quilt, the wardrobe, the dresser.
            _room = "guest";
            Furniture("Guest bed (made up for her)", "bedroom.double_bed", -5.2f, -3.6f, 8.85f, 10.85f, U, 1.05f, 180f);
            Furniture("Nightstand (guest, west)", "bedroom.nightstand", -5.75f, -5.24f, 10.34f, 10.85f, U, 0.62f, 180f);
            Furniture("Nightstand (guest, east)", "bedroom.nightstand", -3.56f, -3.05f, 10.34f, 10.85f, U, 0.62f, 180f);
            Prop("Flowers (for her)", "living.vase.1", -3.3f, 10.6f, U + 0.62f, 0.22f, 0.22f, 0.31f);
            Wardrobe("Guest wardrobe", X0 + e, -6.0f, 5.08f, 6.05f, U, 0, 2.1f);
            Furniture("Dresser (guest)", "bedroom.dresser", -7.34f, -6.85f, 6.4f, 7.54f, U, 1.88f, 90f);

            // ---------- The upper hall: the reading chair over the front door.
            _room = "upperhall";
            Furniture("Reading chair (landing)", "living.armchair", 0.55f, 1.6f, 0.2f, 1.35f, U, 1.1f, -45f);
            Rug("Runner (upper hall)", "living.rug", -0.3f, 0.9f, 6.2f, 10.4f, U);
            Picture("Photo (upper hall)", "dream_pic_photo_6", false, 1.625f, -1, 8.1f, 8.7f, U + 1.3f, U + 1.8f);
            // The linen press halfway along: the hall's 10 m run no longer shows end to end.
            Furniture("Linen press (upper hall)", "upperhall.linen_press", 1.07f, 1.625f, 6.3f, 7.4f, U, 2.0f, -90f);

            // ---------- Elias and Lena's room: the bed, the nightstands, the dresser.
            _room = "parents";
            // A grown-ups' bed (house-1.md: it was child-sized in a 5.6 x 6.4 m room): 1.8 x 2.1, its headboard 1.1 high.
            Furniture("Their bed", "bedroom.double_bed", 5.25f, 7.35f, 2.8f, 4.6f, U, 1.1f, -90f);
            Furniture("Nightstand (Lena's)", "bedroom.nightstand", 6.84f, 7.35f, 2.24f, 2.75f, U, 0.62f, -90f);
            Furniture("Nightstand (Elias's)", "bedroom.nightstand", 6.84f, 7.35f, 4.65f, 5.16f, U, 0.62f, -90f);
            Prop("Lamp (Lena's side)", "bedroom.table_lamp", 7.1f, 2.5f, U + 0.62f, 0.3f, 0.3f, 0.5f);
            // A dressing screen and her armchair: something between the door and the bed (he saw the whole room from the door).
            Furniture("Dressing screen", "bedroom.screen", 2.5f, 3.9f, 3.3f, 3.55f, U, 1.75f, 90f);
            Furniture("Armchair (theirs)", "living.armchair", 2.0f, 3.0f, 0.3f, 1.4f, U, 1.1f, 45f);
            Furniture("Dresser (theirs)", "bedroom.dresser", 3.4f, 4.54f, 6.03f, 6.52f, U, 1.88f, 180f);
            Rug("Rug (their room)", "living.rug", 3.2f, 5.2f, 2.4f, 5.0f, U);
            _room = "closet";
            Furniture("Closet shelves", "closet.shelves", 1.85f, 4.45f, 9.3f, 9.67f, U, 2.0f, 180f);
            Cloth("Clothes on the rail (west)", "closet.clothes", 1.78f, 2.35f, U + 0.7f, U + 1.95f, 6.8f, 9.2f);
            _room = "ensuite";
            Cloth("Shower curtain (en-suite)", "bath.curtain", 6.3f, 6.36f, U, U + 2.1f, 8.6f, 9.67f);
            Furniture("Vanity (en-suite)", "bath.sink", 4.75f, 5.9f, 9.2f, 9.67f, U, 0.85f, 180f);
            Furniture("Toilet (en-suite)", "bath.toilet", 6.6f, 7.1f, 6.7f, 7.25f, U, 0.8f, 0f);

            // ---------- The attic: what the family keeps (boxes in the low eaves, the old crib, a trunk, the rocking chair)
            // and the tower: Lena's telescope on its platform, her chair, her chart table.
            _room = "attic";
            float[] boxXs = { -2.5f, -1.4f, 0.2f, 1.3f, 5.9f, 6.8f };
            foreach (float bx in boxXs)
            {
                Box("Boxes (south eave)", "attic.box", bx, bx + 0.52f, A, A + 0.68f, 0.3f, 0.82f);
                if (bx < DormerX0 - 0.6f || bx > DormerX1 + 0.1f) Box("Boxes (north eave)", "attic.box", bx, bx + 0.52f, A, A + 0.68f, 10.2f, 10.72f);
            }
            Furniture("Trunk (attic)", "attic.trunk", 3.0f, 3.82f, 2.0f, 2.41f, A, 0.35f);
            Furniture("Theo's old crib", "attic.crib", -1.2f, 0.2f, 7.4f, 8.1f, A, 0.9f);
            Furniture("Rocking chair (attic)", "attic.rocking_chair", 4.6f, 5.31f, 3.4f, 4.23f, A, 0.99f, -120f);
            Furniture("The Christmas box", "attic.box", 6.2f, 6.9f, 4.6f, 5.3f, A, 0.5f);
            Prop("Oil lamp (attic)", "attic.lamp", 3.41f, 2.2f, A + 0.35f, 0.22f, 0.22f, 0.8f);
            // The family's stored life in rows (the attic was one vast empty hall: house-1.md): two runs of shelving
            // across it under the ridge, the old wardrobe, a rolled rug, suitcases: bays to hide in and break his sight.
            Furniture("Shelving across the attic (east)", "garage.shelves", 1.95f, 2.55f, 4.3f, 5.22f, A, 1.9f, 90f);
            Furniture("Shelving across the attic (west)", "garage.shelves", -1.1f, -0.5f, 4.6f, 5.52f, A, 1.9f, 90f);
            Furniture("The old wardrobe (attic)", "bedroom.dresser", 0.4f, 1.55f, 7.6f, 8.09f, A, 1.88f, 180f);
            Furniture("Suitcases (attic)", "attic.suitcases", 5.5f, 6.3f, 6.3f, 6.8f, A, 0.55f);
            Furniture("A rolled rug (attic)", "attic.rolled_rug", -2.6f, 0.0f, 3.2f, 3.5f, A, 0.3f);
            _room = "observatory";
            // The dais under the round window (Lena stands on it to watch Chapel Row), the telescope on the floor aimed
            // up through the window at the sky, her chart table, her chair, the bookcase on the west wall.
            Furniture("Lena's dais (under the round window)", "observatory.platform", -6.0f, -4.5f, 7.0f, 7.85f, A, 1.0f);
            Furniture("Lena's telescope", "observatory.telescope", -5.6f, -4.8f, 5.3f, 6.4f, A, 1.75f, 0f);
            Furniture("Lena's chart table", "observatory.chart_table", -4.4f, -3.6f, 6.6f, 7.4f, A, 0.75f);
            Furniture("Lena's chair", "attic.rocking_chair", -4.5f, -3.79f, 3.4f, 4.23f, A, 0.99f, 45f);
            Prop("Binoculars", "attic.binoculars", -3.8f, 6.8f, A + 0.75f, 0.17f, 0.29f, 0.08f);
            Furniture("Bookcase (observatory)", "living.bookshelf", -7.34f, -6.76f, 4.0f, 5.37f, A, 2.06f, 90f);

            // ---------- The basement: the rec room (the old couch, the TV, the ping-pong table), the furnace room (dark:
            // hide behind the furnace), Elias's workshop (his bench, his radio), the laundry (washing on the line).
            _room = "rec";
            Furniture("Ping-pong table", "rec.pingpong", -5.5f, -4.0f, 2.3f, 4.9f, B, 0.76f);
            Furniture("Old couch", "living.sofa", -7.3f, -6.48f, 0.2f, 2.01f, B, 0.75f, 90f);
            Furniture("TV stand (rec room)", "living.tv_stand", -2.68f, -2.24f, 0.6f, 1.52f, B, 0.53f, -90f);
            Prop("Old TV", "living.tv", -2.46f, 1.06f, B + 0.53f, 0.35f, 0.4f, 0.41f, -90f);
            Furniture("Games shelf", "basement.shelves", -3.2f, -2.28f, 5.9f, 6.42f, B, 1.9f, 180f);
            // A bookcase standing out in the room past its doorway: the room was seen whole from the door.
            Furniture("Old bookcase (rec room divider)", "living.bookshelf", -3.75f, -3.17f, 3.0f, 4.37f, B, 2.06f, 90f);
            _room = "furnace";
            Furniture("The furnace", "furnace.furnace", -6.4f, -5.2f, 9.6f, 10.85f, B, 1.8f);
            Furniture("Water heater", "furnace.heater", -4.6f, -3.9f, 10.15f, 10.85f, B, 1.6f);
            Furniture("Boxes (furnace room)", "basement.box", -3.0f, -2.48f, 10.3f, 10.82f, B, 0.68f);
            _room = "workshop";
            Furniture("Elias's workbench", "basement.workbench", 3.0f, 4.33f, Z0 + e, 0.71f, B, 0.83f, 180f);
            Furniture("Toolbox", "basement.toolbox", 5.0f, 5.69f, Z0 + e, 0.56f, B, 0.65f, 180f);
            Furniture("Workshop shelves", "basement.shelves", 6.75f, 7.35f, 1.0f, 1.92f, B, 1.9f, -90f);
            Box("Pegboard", "workshop.pegboard", 2.9f, 4.4f, B + 1.1f, B + 2.0f, Z0 + e, Z0 + e + 0.03f, 0f);
            _room = "laundry";
            Furniture("Washing machine", "laundry.washer", 5.5f, 6.15f, 10.2f, 10.85f, B, 0.88f, 180f);
            Furniture("Dryer", "laundry.dryer", 6.25f, 6.9f, 10.2f, 10.85f, B, 0.88f, 180f);
            Furniture("Laundry basket", "laundry.basket", 4.6f, 5.1f, 10.35f, 10.85f, B, 0.45f);
            // The line across the laundry, wall to wall, the washing pegged on it.
            Box("Washing line (laundry)", "laundry.line", 1.7f + i, X1 - e, B + 2.0f, B + 2.02f, 8.775f, 8.795f, 0f, HouseLook.Rail);
            Cloth("Washing on the line", "laundry.washing", 2.1f, 4.9f, B + 0.5f, B + 2.0f, 8.75f, 8.82f);
            _room = "corridor";
            Furniture("Jars on the shelves", "basement.shelves", 0.98f, 1.62f, 0.8f, 1.72f, B, 1.9f, -90f);

            // ---------- The garage: the car under its cover, shelves, the bench, the freezer; the mudroom's bench.
            _room = "garage";
            Furniture("The car (under its cover)", "garage.car", 8.7f, 10.5f, 1.6f, 6.0f, GarageFloor, 1.41f);
            Furniture("Garage shelves (south)", "garage.shelves", 11.25f, 11.85f, 0.6f, 1.52f, GarageFloor, 1.9f, -90f);
            Furniture("Garage shelves (north)", "garage.shelves", 11.25f, 11.85f, 1.6f, 2.52f, GarageFloor, 1.9f, -90f);
            Furniture("Garage bench", "garage.workbench", 10.0f, 11.33f, 6.79f, 7.35f, GarageFloor, 0.83f, 180f);
            Furniture("Chest freezer", "garage.freezer", 7.7f, 8.35f, 0.2f, 1.15f, GarageFloor, 0.9f, 90f);
            Prop("Gas can", "garage.gas_can", 11.55f, 4.3f, GarageFloor, 0.27f, 0.34f, 0.32f);
            Furniture("Boxes (garage)", "garage.box", 7.7f, 8.22f, 6.7f, 7.32f, GarageFloor, 0.68f);
            _room = "mudroom";
            Furniture("Mudroom bench", "yard.bench", 9.83f, 10.35f, 8.0f, 9.2f, G, 0.45f, -90f);
            Cloth("Raincoats (mudroom)", "backhall.coats", 10.0f, 10.34f, G + 0.5f, G + 1.9f, 9.3f, 10.6f);

            // ---------- The shed's inside; flower pots on the patio; the bench at the end of the yard.
            _room = "shed";
            Furniture("Shed shelves", "garage.shelves", 16.1f, 16.7f, 17.7f, 18.55f, Yard + 0.05f, 1.9f, -90f);
            _room = "yard";
            Prop("Flower pot (patio)", "yard.flower_pot", 3.3f, 11.6f, Yard + 0.02f, 0.27f, 0.26f, 0.22f);
            Prop("Flower pot (patio, west)", "yard.flower_pot", -3.3f, 13.9f, Yard + 0.02f, 0.27f, 0.26f, 0.22f);
            Furniture("Bench (end of the yard)", "yard.bench", -0.6f, 0.57f, 20.8f, 21.3f, Yard, 0.89f, 180f);
            Furniture("Shrubs (by the fence)", "yard.shrub", 11.0f, 13.4f, 21.0f, 21.95f, Yard, 2.0f);
            Furniture("Fire pit", "yard.firepit", 8.0f, 9.45f, 15.2f, 16.65f, Yard, 0.39f);
        }

        // =====================================================================================================
        // Hiding spots, Theo's walking map, light
        // =====================================================================================================
        void Stealth_()
        {
            const float G = Ground, U = Upper, B = Basement, A = Attic;
            // Closets: he has to open them to find you. Cloth: hard to see behind unless he's right there. Each spot's
            // approach is where he stands to check it.
            Spot("The cupboard under the stairs", HideKind.Closet, "understairs", V(-1.1f, G + 0.9f, 5.15f), V(1.0f, 1.8f, 1.5f), V(0.3f, G, 5.0f));
            Spot("The pantry", HideKind.Closet, "pantry", V(6.55f, G + 1f, 9.98f), V(0.9f, 2f, 1.0f), V(6.5f, G, 8.6f));
            Spot("The coats (back hall)", HideKind.Bush, "backhall", V(1.25f, G + 1f, 8.9f), V(0.75f, 2f, 1.4f), V(0.2f, G, 8.9f));
            Spot("Behind the den curtains", HideKind.Bush, "den", V(-7.05f, G + 1f, 8.1f), V(0.6f, 2f, 2.0f), V(-6.0f, G, 8.1f));
            Spot("Theo's wardrobe", HideKind.Closet, "theo", V(-2.8f, U + 1f, 4.45f), V(1.3f, 2f, 0.9f), V(-2.8f, U, 3.2f));
            Spot("Behind the shower curtain", HideKind.Bush, "bath", V(-3.76f, U + 0.55f + 0.95f, 5.94f), V(0.7f, 1.9f, 1.7f), V(-2.7f, U, 5.94f));
            Spot("The guest wardrobe", HideKind.Closet, "guest", V(-6.67f, U + 1f, 5.55f), V(1.25f, 2f, 0.9f), V(-6.67f, U, 6.9f));
            Spot("The walk-in closet", HideKind.Closet, "closet", V(3.15f, U + 1f, 8.1f), V(2.6f, 2f, 2.6f), V(2.7f, U, 5.9f));
            Spot("The en-suite shower", HideKind.Bush, "ensuite", V(6.83f, U + 1f, 9.13f), V(0.95f, 2f, 1.0f), V(5.6f, U, 8.6f));
            Spot("Behind the furnace", HideKind.Bush, "furnace", V(-6.9f, B + 1f, 10.1f), V(0.8f, 2f, 1.4f), V(-5.8f, B, 8.9f));
            Spot("Behind the washing", HideKind.Bush, "laundry", V(3.5f, B + 1f, 9.3f), V(2.6f, 2f, 0.9f), V(3.5f, B, 7.6f));
            Spot("Behind the car", HideKind.Bush, "garage", V(11.2f, GarageFloor + 1f, 3.4f), V(0.9f, 2f, 1.5f), V(11.1f, GarageFloor, 6.3f));
            Spot("The attic boxes", HideKind.Bush, "attic", V(6.3f, A + 0.8f, 2.2f), V(1.2f, 1.6f, 1.0f), V(5.0f, A, 4.0f));
            Spot("Lena's observatory", HideKind.Bush, "observatory", V(-6.6f, A + 1f, 6.6f), V(1.1f, 2f, 1.4f), V(-4.2f, A, 5.6f));
            Spot("The shed", HideKind.Closet, "shed", V(15.0f, Yard + 1.05f, 19.9f), V(2.0f, 2f, 1.1f), V(12.9f, Yard, 19.1f));
            Spot("Behind the sheets on the line", HideKind.Bush, "yard", V(-0.9f, Yard + 1f, 16.0f), V(5.0f, 2f, 0.8f), V(-0.9f, Yard, 14.6f));
            Spot("The treehouse", HideKind.Closet, "treehouse", V(TreeX - 0.5f, TreeFloor + 0.8f, TreeZ), V(2.1f, 1.6f, 1.9f), V(TreeX + 5.3f, Yard, TreeZ));

            // Theo counts at the table, his face in his arms, looking at the window.
            theoStart = theoAt;
            theoCountYaw = 0f;
            openFloor.Add(V(-4.5f, G, 1.2f));   // the living room
            openFloor.Add(V(4.4f, G, 7.0f));    // the kitchen
            openFloor.Add(V(-5.0f, U, 2.0f));   // Theo's room
            openFloor.Add(V(-0.4f, B, 2.0f));   // the basement corridor
            openFloor.Add(V(0f, Yard, 17f));    // the back yard

            Nodes();
        }

        /// <summary>Theo's walking points: a grid in every room (kept clear of the furniture), a point each side of every
        /// door he fits through, the stairs' ends linked.</summary>
        void Nodes()
        {
            foreach (var room in rooms)
            {
                if (room.id == "street" || room.id == "understairs" || room.id == "atticstairs") continue;
                float step = room.outside ? 4.0f : 1.9f;
                foreach (var r in room.rects)
                {
                    float w = r.x1 - r.x0, d = r.z1 - r.z0;
                    if (w < 0.9f || d < 0.9f) continue;
                    int nx = Math.Max(1, (int)Math.Round(w / step)), nz = Math.Max(1, (int)Math.Round(d / step));
                    for (int ix = 0; ix < nx; ix++)
                        for (int iz = 0; iz < nz; iz++)
                        {
                            float x = r.x0 + w * (ix + 0.5f) / nx, z = r.z0 + d * (iz + 0.5f) / nz;
                            var p = V(x, room.floorY, z);
                            if (room.id == "attic" && RoofAt(z) < 1.9f) continue;
                            // Over a stairwell there's no floor: no point there (Theo and her autopilot walked into the hole).
                            if (float.IsNaN(FloorAt(p + V(0f, 0.05f, 0f))) || Math.Abs(FloorAt(p + V(0f, 0.05f, 0f)) - room.floorY) > 0.05f) continue;
                            if (Free(p, 0.32f, 1.6f)) Node(room.id, x, room.floorY, z);
                        }
                }
            }
            // The doors (both sides, 0.55 m out), where he fits (1.5 m and more).
            foreach (var dr in doors)
            {
                if (dr.y1 - dr.y0 < 1.5f || dr.name == "Garage door") continue;   // too low for him, or shut
                float mid = (dr.a + dr.b) * 0.5f, off = dr.thickness * 0.5f + 0.5f;
                for (int s = -1; s <= 1; s += 2)
                {
                    var p = dr.alongX ? V(mid, dr.y0, dr.c + s * off) : V(dr.c + s * off, dr.y0, mid);
                    float floor = FloorAt(p);
                    if (float.IsNaN(floor)) continue;
                    p.Y = floor;
                    var room = RoomAt(p + V(0f, 0.1f, 0f));
                    if (room == null || room.id == "street" || room.id == "understairs" || room.id == "atticstairs") continue;
                    if (Free(p, 0.3f, 1.6f)) Node(room.id, p.X, p.Y, p.Z);
                }
            }
            // The stairs: a point off each end, linked (Theo walks the slope between them).
            LinkStair(MainStair, Ground, Upper, false, 1, "hall", "upperhall");
            LinkStair(BasementStair, Basement, Ground, false, 1, "corridor", "backhall");
            LinkStair(AtticStair, Upper, Attic, true, 1, "upperhall", "dormer");
            LinkStair(new PlanRect(TreeX + 1.8f, TreeX + 4.8f, TreeZ - 0.45f, TreeZ + 0.45f), Yard, TreeFloor, true, -1, "yard", "treehouse");
            // The porch and the back door's steps (small height changes outside).
            int porchIn = Node("hall", 0f, Ground, 0.9f), porchOut = Node("frontgarden", 0f, Yard + 0.02f, -1.4f), path = Node("frontgarden", 0f, Yard + 0.02f, -4.0f);
            Link(porchIn, porchOut); Link(porchOut, path);
            int backIn = Node("backhall", 0f, Ground, 10.2f), patio = Node("yard", 0f, Yard + 0.02f, 12.3f), lawn = Node("yard", 0f, Yard, 17.0f);
            Link(backIn, patio); Link(patio, lawn);
            int mudIn = Node("mudroom", 9.25f, Ground, 10.2f), mudOut = Node("yard", 9.25f, Yard, 12.9f);
            Link(mudIn, mudOut);
            int garIn = Node("garage", 11.4f, GarageFloor, 5.65f), garOut = Node("yard", 12.75f, Yard, 5.65f);
            Link(garIn, garOut);
        }

        void LinkStair(PlanRect r, float bottom, float top, bool alongX, int up, string lowRoom, string highRoom)
        {
            const float off = 0.55f;
            Vector3 low, high;
            if (alongX)
            {
                float z = r.CZ;
                low = up > 0 ? V(r.x0 - off, bottom, z) : V(r.x1 + off, bottom, z);
                high = up > 0 ? V(r.x1 + off, top, z) : V(r.x0 - off, top, z);
            }
            else
            {
                float x = r.CX;
                low = up > 0 ? V(x, bottom, r.z0 - off) : V(x, bottom, r.z1 + off);
                high = up > 0 ? V(x, top, r.z1 + off) : V(x, top, r.z0 - off);
            }
            int a = Node(lowRoom, low.X, low.Y, low.Z), b = Node(highRoom, high.X, high.Y, high.Z);
            Link(a, b);
        }

        /// <summary>True when a body of radius <paramref name="r"/> and height <paramref name="h"/> standing at
        /// <paramref name="feet"/> touches no solid (floors under the feet don't count).</summary>
        public bool Free(Vector3 feet, float r, float h)
        {
            var lo = feet + V(-r + 0.003f, 0.06f, -r + 0.003f);
            var hi = feet + V(r - 0.003f, h, r - 0.003f);
            foreach (var part in parts)
            {
                var p = part.piece;
                if (!p.solid) continue;
                var mn = p.Min; var mx = p.Max;
                if (p.kind == LabPieceKind.Ramp)
                {
                    if (part.look == HouseLook.Roof) continue;   // the roof is over her head (the attic's points skip the low eaves)
                    // On a stair's footprint, under its slab: room only if the body fits under it.
                    if (lo.X < mx.X && hi.X > mn.X && lo.Z < mx.Z && hi.Z > mn.Z && feet.Y + 0.06f < RampTop(part, feet.X, feet.Z) && feet.Y + h > RampTop(part, feet.X, feet.Z) - 0.35f) return false;
                    continue;
                }
                if (lo.X < mx.X && hi.X > mn.X && lo.Y < mx.Y && hi.Y > mn.Y && lo.Z < mx.Z && hi.Z > mn.Z) return false;
            }
            return true;
        }

        /// <summary>The top of the highest solid floor under a point within 0.6 m (NaN: none).</summary>
        public float FloorAt(Vector3 p)
        {
            float best = float.NaN;
            foreach (var part in parts)
            {
                var q = part.piece;
                if (!q.solid || p.X <= q.Min.X || p.X >= q.Max.X || p.Z <= q.Min.Z || p.Z >= q.Max.Z) continue;
                float top = q.kind == LabPieceKind.Ramp ? RampTop(part, p.X, p.Z) : q.Max.Y;
                if (top > p.Y + 0.6f || top < p.Y - 0.6f) continue;
                if (float.IsNaN(best) || top > best) best = top;
            }
            return best;
        }

        // =====================================================================================================
        // The memories: eight; the place card is found at the table, the other seven are hidden round the house. Each
        // has two other places the house can put it back when Theo catches her.
        // =====================================================================================================
        void Memories_()
        {
            const float G = Ground, U = Upper, B = Basement, A = Attic;
            var m = Memory(DreamHouseStory.MemPlaceCard, "Her place card at the fourth chair", "dining", "dream_pic_place_card", V(5.1f, G + 0.77f, 2.95f), V(nadiaChair.X, G, nadiaChair.Z));
            m.atTable = true;
            Alt(m, "hall", V(1.4f, G + 0.77f, 3.82f), V(0.6f, G, 3.9f));
            Alt(m, "theo", V(-4.05f, U + 0.76f, 0.42f), V(-4.05f, U, 1.75f));

            m = Memory(DreamHouseStory.MemCalendar, "The fridge calendar", "kitchen", "dream_pic_calendar_n2", V(2.3f, G + 1.35f, 10.08f), V(2.3f, G, 9.25f));
            Alt(m, "den", V(-5.6f, G + 0.8f, 10.35f), V(-4.3f, G, 9.4f));
            Alt(m, "laundry", V(7.33f, B + 1.4f, 8.5f), V(6.5f, B, 8.5f));

            m = Memory(DreamHouseStory.FactPhoto, "The photo of the four of them at the lake", "hall", "dream_pic_photo_1", V(-1.61f, 2.45f, 3.25f), V(-0.15f, G, 3.25f));
            Alt(m, "guest", V(-5.5f, U + 0.63f, 10.6f), V(-6.25f, U, 9.9f));
            Alt(m, "rec", V(-7.34f, B + 1.4f, 1.2f), V(-5.9f, B, 1.2f));

            m = Memory(DreamHouseStory.MemHeightMarks, "Theo's height marks on his door frame", "theo", null, V(-1.79f, U + 1.25f, 1.96f), V(-2.6f, U, 2.3f));
            Alt(m, "kitchen", V(6.99f, G + 1.25f, 9.31f), V(6.6f, G, 8.55f));
            Alt(m, "corridor", V(1.62f, B + 1.25f, 8.36f), V(0.8f, B, 8.7f));

            // Pinned to the wall over his desk at eye height (it lay flat on top of the bookcase, unreadable: house-1.md).
            m = Memory(DreamHouseStory.MemBadDream, "Theo's drawing: THE BAD DREAM", "theo", "dream_pic_bad_dream", V(-4.05f, U + 1.35f, Z0 + e + 0.012f), V(-4.05f, U, 1.3f));
            Alt(m, "rec", V(-4.8f, B + 0.78f, 3.2f), V(-3.5f, B, 3.2f));
            Alt(m, "guest", V(-4.4f, U + 0.62f, 10.4f), V(-4.4f, U, 8.5f));

            m = Memory(DreamHouseStory.MemSaturdayPaper, "Today's paper, folded into Theo's hat", "treehouse", "dream_pic_paper_hat", V(TreeX - 1.0f, TreeFloor + 0.05f, TreeZ + 0.5f), V(TreeX - 0.2f, TreeFloor, TreeZ));
            Alt(m, "frontgarden", V(0.55f, Yard + 0.03f, -1.0f), V(0f, Yard + 0.02f, -1.7f));
            Alt(m, "garage", V(9.6f, GarageFloor + 1.42f, 1.9f), V(9.6f, GarageFloor, 1.0f));

            m = Memory(DreamHouseStory.MemRadio, "Elias's radio: the same forecast, word for word", "workshop", "dream_pic_radio", V(3.66f, B + 0.84f, 0.45f), V(3.66f, B, 1.3f));
            Alt(m, "kitchen", V(3.2f, G + 0.93f, 10.55f), V(3.2f, G, 9.6f));
            Alt(m, "garage", V(10.6f, GarageFloor + 0.84f, 7.05f), V(10.6f, GarageFloor, 6.2f));

            m = Memory(DreamHouseStory.MemLenaThursdays, "Lena's star wheel, set to a Thursday", "observatory", "dream_pic_star_wheel", V(-4.0f, A + 0.76f, 7.0f), V(-4.0f, A, 6.0f));
            Alt(m, "parents", V(7.1f, U + 0.63f, 4.8f), V(6.4f, U, 5.6f));
            Alt(m, "attic", V(3.41f, A + 0.36f, 2.0f), V(3.41f, A, 2.9f));
        }

        // =====================================================================================================
        void Lamps_()
        {
            foreach (var r in rooms)
            {
                if (r.outside) continue;
                float h = r.ceilingY - 0.45f;
                string kind = r.style == "basement" || r.style == "garage" || r.id == "pantry" || r.id == "closet" ? "bulb" : "pendant";
                if (r.id == "observatory") kind = "moon";
                if (r.id == "attic") kind = "fairy";
                if (r.id == "understairs" || r.id == "atticstairs") continue;
                foreach (var rc in r.rects)
                    if (rc.Area > 1.0f) Lamp(r.name, kind, V(rc.CX, Math.Min(h, r.floorY + 2.5f), rc.CZ), Math.Max(4f, Math.Max(rc.x1 - rc.x0, rc.z1 - rc.z0) * 0.9f), r.light);
            }
            Lamp("The fire", "fire", V(-6.6f, Ground + 0.4f, 3.2f), 6f, 1.2f);
            Lamp("Floor lamp", "table", V(-2.17f, Ground + 1.45f, 1.92f), 4f, 0.8f);
            Lamp("Porch lantern", "porch", V(1.1f, Ground + 2.2f, -0.35f), 6f, 0.9f);
            Lamp("Lena's lamp", "table", V(7.1f, Upper + 1.0f, 2.6f), 3.5f, 0.7f);
            Lamp("Theo's night light", "table", V(-3.85f, Upper + 0.9f, 0.45f), 3f, 0.6f);
            lenaWindow = V(-5.25f, Attic + 1.0f, TowerZ1 - e - 0.4f);   // on her dais, in the round window
            hallBreathFrom = V(0f, Ground + 1.5f, 0.9f);
            hallBreathTo = V(0.5f, Ground + 1.2f, 8.5f);
        }
    }
}
