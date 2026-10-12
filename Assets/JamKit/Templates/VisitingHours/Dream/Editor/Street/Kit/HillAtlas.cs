using UnityEngine;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The layouts of the generated atlases (tools/art/hill-textures.py draws them; the hill's meshes cut cells out of
    /// them by these rects). UV (0,0) is the texture's bottom-left, as Unity reads PNGs; the generator draws cell
    /// (column c, row r) with row 0 at the TOP of the image, so <see cref="Cell"/> flips rows.
    /// </summary>
    public static class HillAtlas
    {
        /// <summary>A cell of a grid atlas: column <paramref name="c"/>, row <paramref name="r"/> (row 0 at the top), with a
        /// small inset so mips don't bleed between cells.</summary>
        public static Rect Cell(int c, int r, int cols, int rows, float inset = 0.004f)
        {
            float w = 1f / cols, h = 1f / rows;
            return new Rect(c * w + inset, 1f - (r + 1) * h + inset, w - inset * 2f, h - inset * 2f);
        }

        // gen_interiors (2048 x 1024): 4 x 2 rooms seen through a window, warm lamplight, each 512 x 512.
        // 0 living room (sofa, lamp, picture), 1 kitchen (cabinets, window light), 2 bedroom (bed, curtains),
        // 3 stairwell (banister, hall light), 4 dining room (table, hanging lamp), 5 study (bookshelves),
        // 6 child's room (posters, toys), 7 dim room (curtains half drawn, no lamp).
        public const int InteriorCols = 4, InteriorRows = 2, InteriorCount = 8;
        public static Rect Interior(int i) => Cell(i % InteriorCols, (i / InteriorCols) % InteriorRows, InteriorCols, InteriorRows);

        // gen_interiors_shop (2048 x 1024): 4 x 2 shop interiors: 0 bakery, 1 diner, 2 florist, 3 pharmacy, 4 laundromat,
        // 5 newsagent, 6 hardware, 7 bike workshop.
        public static Rect ShopInterior(int i) => Cell(i % 4, (i / 4) % 2, 4, 2);

        // gen_interiors_ward (1024 x 512): 2 x 1: 0 a ward (beds, curtains, monitors), 1 a corridor (lights, doors).
        public static Rect WardInterior(int i) => Cell(i % 2, 0, 2, 1);

        // gen_signs (1024 x 2048): 1 x 16 rows, each 1024 x 128: painted signboards with lettering.
        public const int SignRows = 16;
        public const int SignAbernathy = 0, SignBakery = 1, SignDiner = 2, SignFlorist = 3, SignPharmacy = 4, SignLaundromat = 5,
            SignNews = 6, SignHardware = 7, SignHospital = 8, SignEmergency = 9, SignChurch = 10, SignSchool = 11,
            SignMillStreet = 12, SignRowanStreet = 13, SignLarchStreet = 14, SignBusStop = 15;
        public static readonly string[] SignTexts =
        {
            "ABERNATHY CYCLES", "LARCH HILL BAKERY", "CREST DINER", "ROSA'S FLOWERS", "HILL PHARMACY", "SUDS LAUNDROMAT",
            "THE NEWS NOOK", "PRUITT HARDWARE", "ST. BRIGID'S HOSPITAL", "EMERGENCY", "ST. ANNE'S CHURCH", "LARCH HILL PRIMARY",
            "MILL STREET", "ROWAN STREET", "LARCH STREET", "ROUTE 9  ST. BRIGID'S",
        };
        public static Rect Sign(int i) => Cell(0, i % SignRows, 1, SignRows, 0.002f);

        // gen_leaves, gen_leaves_blossom, gen_ivy (1024 x 1024): 2 x 2 leaf clusters with alpha (each a rounded clump of
        // leaves, opaque in the middle, ragged at the edge), lit from above (lighter tops).
        public static Rect Leaf(int i) => Cell(i % 2, (i / 2) % 2, 2, 2);

        // gen_grass_cards (1024 x 512) and gen_flowers (1024 x 512): 4 x 1 tufts, each 256 x 512, rooted at the bottom edge.
        public static Rect Tuft(int i) => Cell(i % 4, 0, 4, 1);

        // gen_cracks, gen_stains, gen_leaf_decals (1024 x 1024): 2 x 2 decals with alpha, centred in their cells.
        public static Rect Decal(int i) => Cell(i % 2, (i / 2) % 2, 2, 2);

        // gen_manhole, gen_drain (512 x 512): one decal each (a round manhole cover; a kerbside drain grate).
        public static Rect Whole => new Rect(0f, 0f, 1f, 1f);
    }
}
