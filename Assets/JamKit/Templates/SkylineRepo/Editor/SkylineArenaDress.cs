using System.Collections.Generic;
using JamKit.Look.EditorTools;
using JamKit.Skyline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// The arena's dressing (Task 21; spec section 10 items 1-6 and 12; docs/SKYLINE-LOOK.md): CONCIERGE's white-and-gold glass
    /// mall over <see cref="GalleriaArena"/>'s grey box. The gameplay pieces keep their positions, sizes and colliders
    /// exactly; the dressing only re-skins them (library materials with metre UVs) and adds visual-only geometry (no
    /// colliders) inside their volumes, a few centimetres proud of a wall, or out of reach overhead:
    /// <list type="bullet">
    /// <item><b>The main hall's walls</b> become a built shell: the runnable wall boxes keep their colliders but lose their
    /// renderers, and in their slab stand white cladding with gold reveals, glass shopfronts with real shop interiors behind
    /// them (lit, shelved, signed), a fascia band, a glass curtain wall with mullions above (the city's towers seen through
    /// it), and a cornice. The north wall has a second row of shops above the mezzanine. The south wall (the warm-up hall is
    /// behind it) has shallow lit vitrines and the doorway's gold portal.</item>
    /// <item><b>The skylight</b>: white beams and steel mullions under the glass, which stripe the floor with the sun.</item>
    /// <item><b>The mezzanine</b>: a terrazzo deck over a white body and a ceiling-tile soffit with downlights, a steel handrail.</item>
    /// <item><b>The cover</b>: pillars in white cladding with gold capitals; food stalls in pine with menu boards; the info
    /// booth's screens showing CONCIERGE's notices; marble kiosks with lightbox adverts; marble planters with plants.</item>
    /// <item><b>The floors</b>: white terrazzo with a gold inlay (a border, two aisles, an octagonal medallion between the
    /// kiosks), marble under the mezzanine; the warm-up hall in cream marble.</item>
    /// <item><b>The warm-up hall</b>: wainscot, pilasters, cornice, closed shop shutters with remodel notices, CONCIERGE's
    /// welcome screen, the GALLERIA sign over the doorway, a roof structure; the kiosk is an espresso kiosk with glowing
    /// signs on all four sides (the landmark); the practice gantry is the firm's orange with hazard stripes.</item>
    /// <item><b>The city</b> outside (backdrop): ~30 glass towers, sky-bridges and a monorail, hazed into the sky's horizon
    /// colour by distance.</item>
    /// </list>
    /// </summary>
    public static partial class SkylineArenaBuilder
    {
        // ---------------------------------------------------------------------------------------------- dressing constants
        const float ShopDepth = 4.2f, ShopOpen = 3.5f, BandTop = 4.4f, BayHalf = 2.5f, CorniceY = 13.4f;
        const float Proud = 0.012f, PortalWidth = 0.35f;
        static readonly float[] SideBays = { -16f, -9f, -2f, 5f, 12f };
        static readonly float[] NorthBays = { -21f, -14f, -7f, 0f, 7f, 14f, 21f };
        static readonly float[] SouthVitrines = { -19f, -11f, 11f, 19f };
        static readonly Color ShopLightColor = new Color(1f, 0.87f, 0.7f);

        /// <summary>Sign atlas cells (tools/skyline/look/make-textures.py, SK_Signs.png: 4 x 8 cells of 2:1).</summary>
        const int SignTheGalleria = 0, SignConcierge = 1, SignDoNotRun = 2, SignRemodel = 3, SignWelcome = 4, SignGalleria = 5,
            SignEspresso = 6, SignFoodCourt = 7, SignShop0 = 8, ShopBrands = 12, SignMenu0 = 20, SignShopMore = 24, SignLift = 31;
        /// <summary>Every shop brand (12 in cells 8-19, 7 more in 24-30; fix round 1).</summary>
        const int AllBrands = 19;
        /// <summary>Banner cells (SK_Banners.png).</summary>
        const int BannerGalleria = 0, BannerConcierge = 1, BannerFoodCourt = 2, BannerSkyTerrace = 3;

        static Rect SignUv(int cell) => Cell(cell, 4, 8);
        static Rect PosterUv(int cell) => Cell(cell % 16, 4, 4);
        static Rect BannerUv(int cell) => Cell(cell, 4, 1);
        static Rect ShopSignUv(int brand) { brand %= AllBrands; return SignUv(brand < ShopBrands ? SignShop0 + brand : SignShopMore + brand - ShopBrands); }

        /// <summary>The main hall's shops (index: west 0-4, east 5-9, north 10-16 under the deck, 17-23 above it) to brands,
        /// so no wall shows a shop twice (judge A 9); -1 is the back of the service lift's lobby (the level's far goal,
        /// GalleriaArena.LiftLobby, under the deck at x 4..8): lift doors instead of a lit shop inside the lobby.</summary>
        static readonly int[] HallBrands = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, -1, 15, 16, 17, 18, 1, 14, 8, 4, 5 };
        /// <summary>The lift doors' centre: the middle of GalleriaArena.LiftLobby (x 4..8).</summary>
        const float LiftDoorX = 6f;

        /// <summary>How a brand's shop is fitted out (judge A 9: the same shelves in every shop): shelves of one or two kinds
        /// of goods (SK_Products rows: 0 books, 1 shoe boxes, 2 bottles, 3 jars and tins, 4 folded clothes, 5 boxed
        /// electronics, 6 cosmetics, 7 toys), clothes rails, display plinths, a wall of screens, or plants.</summary>
        enum Fit { Shelves, Rails, Plinths, Screens, Plants }
        static readonly (Fit fit, int left, int right)[] BrandFit =
        {
            (Fit.Shelves, 0, 0), (Fit.Shelves, 7, 7), (Fit.Plinths, 6, 6), (Fit.Plants, 3, 3), (Fit.Shelves, 3, 4),
            (Fit.Screens, 5, 5), (Fit.Shelves, 1, 1), (Fit.Rails, 4, 4), (Fit.Plants, 3, 3), (Fit.Shelves, 6, 6),
            (Fit.Screens, 5, 5), (Fit.Rails, 1, 2), (Fit.Shelves, 3, 2), (Fit.Rails, 4, 4), (Fit.Screens, 7, 7),
            (Fit.Plinths, 4, 1), (Fit.Shelves, 4, 3), (Fit.Shelves, 2, 3), (Fit.Shelves, 0, 6),
        };
        static Rect GoodsUv(int row, float u0, float width) => new Rect(u0, 1f - (row + 1) / 8f, width, 1f / 8f);

        static Rect Cell(int cell, int cols, int rows)
        {
            int col = cell % cols, row = cell / cols;
            float w = 1f / cols, h = 1f / rows;
            return new Rect(col * w, 1f - (row + 1) * h, w, h);
        }

        sealed class Mats
        {
            public Material terrazzo, marble, plaster, pine, cherry, parquet, shutter, ceiling, concrete, soil, steel, gold, dark, hazard;
            public Material products, signs, posters, panel, entry, banners;
            public Material glassShop, glassCurtain, glassSky, glassCounter, glassRail, glassVitrine, frit;
            public Material[] shopWalls;
            public Material[] shopFloors;
            public Material[] shutters;
        }

        static Mats LoadMats()
        {
            var m = new Mats
            {
                terrazzo = SkylineLookLibrary.Get("Terrazzo_White"), marble = SkylineLookLibrary.Get("Marble_Cream"),
                plaster = SkylineLookLibrary.Get("Plaster_White"), pine = SkylineLookLibrary.Get("Wood_Pine"),
                cherry = SkylineLookLibrary.Get("Wood_Cherry"), parquet = SkylineLookLibrary.Get("Parquet_Herringbone"),
                shutter = SkylineLookLibrary.Get("Shutter_Painted"), ceiling = SkylineLookLibrary.Get("Ceiling_Tile"),
                concrete = SkylineLookLibrary.Get("Concrete"), soil = SkylineLookLibrary.Get("Soil"),
                steel = SkylineLookLibrary.Get("Steel_Brushed"), gold = SkylineLookLibrary.Get("Gold_Brushed"),
                dark = SkylineLookLibrary.Get("Steel_Dark"), hazard = SkylineLookLibrary.Get("Hazard_Orange"),
                products = SkylineLookLibrary.Products(), signs = SkylineLookLibrary.Signs(), posters = SkylineLookLibrary.Posters(),
                panel = SkylineLookLibrary.LightPanel(), entry = SkylineLookLibrary.EntryMarker(),
                banners = SkylineLookLibrary.Banners(),
                glassShop = SkylineLookLibrary.Glass("Shopfront", new Color(0.62f, 0.72f, 0.76f, 0.16f)),
                glassCurtain = SkylineLookLibrary.Glass("Curtain", new Color(0.42f, 0.56f, 0.66f, 0.26f)),
                // Fix round 1 (judge A 7: at alpha 0.08 the roof read as an open pergola): a faint blue-green body tint you can
                // see against the sky, and very smooth, so it takes the probe's reflection at a slant; plus fritted bands.
                glassSky = SkylineLookLibrary.Glass("Skylight", new Color(0.66f, 0.82f, 0.86f, 0.2f), 0.97f),
                frit = SkylineLookLibrary.Glass("Frit", new Color(0.94f, 0.95f, 0.94f, 0.5f), 0.7f),
                glassCounter = SkylineLookLibrary.Glass("Counter", new Color(0.8f, 0.9f, 0.95f, 0.2f)),
                glassRail = SkylineLookLibrary.Glass("Balustrade", new Color(0.72f, 0.86f, 0.9f, 0.2f)),
                glassVitrine = SkylineLookLibrary.Glass("Vitrine", new Color(0.62f, 0.72f, 0.76f, 0.14f)),
            };
            // Shop wall paints (the brand's own colour inside, behind the glass): sage, blush, sky, sand, slate, mint.
            m.shopWalls = new[]
            {
                SkylineLookLibrary.Variant("Plaster_White", "Sage", new Color(0.78f, 0.86f, 0.74f)),
                SkylineLookLibrary.Variant("Plaster_White", "Blush", new Color(0.96f, 0.8f, 0.78f)),
                SkylineLookLibrary.Variant("Plaster_White", "Sky", new Color(0.74f, 0.84f, 0.96f)),
                SkylineLookLibrary.Variant("Plaster_White", "Sand", new Color(0.95f, 0.87f, 0.72f)),
                SkylineLookLibrary.Variant("Plaster_White", "Slate", new Color(0.55f, 0.6f, 0.66f)),
                SkylineLookLibrary.Variant("Plaster_White", "Mint", new Color(0.72f, 0.92f, 0.86f)),
            };
            m.shopFloors = new[] { m.parquet, m.marble, m.terrazzo };
            // The warm-up's closed shops, each its own colour (judge A 9: eight identical shutters).
            m.shutters = new[]
            {
                m.shutter,
                SkylineLookLibrary.Variant("Shutter_Painted", "Navy", new Color(0.36f, 0.42f, 0.58f)),
                SkylineLookLibrary.Variant("Shutter_Painted", "Sage", new Color(0.74f, 0.84f, 0.74f)),
                SkylineLookLibrary.Variant("Shutter_Painted", "Sand", new Color(1f, 0.9f, 0.74f)),
            };
            return m;
        }

        /// <summary>A wall in its own frame: u along it, y up, d into it from its inner face (0) to its outer face
        /// (<see cref="WallThick"/>) and beyond (outside). Every box it makes is axis-aligned in the world.</summary>
        struct Wall
        {
            public bool alongZ;       // the wall runs along z (its plane is x = plane), else along x (z = plane)
            public float plane;       // the inner face
            public float outSign;     // +1 when "into the wall" is +x / +z
            public Wall(bool alongZ, float plane, float outSign) { this.alongZ = alongZ; this.plane = plane; this.outSign = outSign; }
            public Vector3 P(float u, float y, float d) => alongZ ? new Vector3(plane + outSign * d, y, u) : new Vector3(u, y, plane + outSign * d);
            public Vector3 Out => alongZ ? new Vector3(outSign, 0f, 0f) : new Vector3(0f, 0f, outSign);
            public Vector3 In => -Out;
            public Vector3 U => alongZ ? Vector3.forward : Vector3.right;
            public void Box(SkylineKit k, Material m, float u0, float u1, float y0, float y1, float d0, float d1, float bevel = 0f) =>
                k.Box(m, P(u0, y0, d0), P(u1, y1, d1), bevel);
            /// <summary>A quad on the wall's inner side facing into the room, <paramref name="d"/> from the face (negative = proud).</summary>
            public void Panel(SkylineKit k, Material m, float uc, float yc, float w, float h, Rect uv, float d = -Proud) =>
                PanelAt(k, m, P(uc, yc, d), In, w, h, uv);
        }

        /// <summary>A vertical quad facing <paramref name="normal"/>, its right as a viewer facing it sees it.</summary>
        static void PanelAt(SkylineKit k, Material m, Vector3 center, Vector3 normal, float w, float h, Rect uv)
        {
            var right = Vector3.Cross(normal, Vector3.up).normalized;
            k.Quad(m, center, right, Vector3.up, w, h, uv);
        }

        /// <summary>A horizontal quad facing down (ceiling light panels), <paramref name="sx"/> x <paramref name="sz"/> m.</summary>
        static void CeilingPanel(SkylineKit k, Material m, Vector3 center, float sx, float sz)
        {
            k.Quad(m, center, Vector3.right, Vector3.back, sx, sz, new Rect(0f, 0f, 1f, 1f));
        }

        /// <summary>Fills [<paramref name="u0"/>, <paramref name="u1"/>] between two heights with a material, skipping the holes.</summary>
        static void Fill(SkylineKit k, Material m, Wall w, float u0, float u1, float y0, float y1, float d0, float d1, List<Vector2> holes)
        {
            float u = u0;
            if (holes != null)
            {
                holes.Sort((a, b) => a.x.CompareTo(b.x));
                foreach (var h in holes)
                {
                    if (h.y <= u || h.x >= u1) continue;
                    if (h.x > u) w.Box(k, m, u, h.x, y0, y1, d0, d1);
                    u = Mathf.Max(u, h.y);
                }
            }
            if (u < u1) w.Box(k, m, u, u1, y0, y1, d0, d1);
        }

        static List<Vector2> Bays(float[] centres, float half, params Vector2[] more)
        {
            var l = new List<Vector2>();
            foreach (var c in centres) l.Add(new Vector2(c - half, c + half));
            l.AddRange(more);
            return l;
        }

        // ================================================================================================ the entry point

        /// <summary>Dresses the built arena (called once the gameplay pieces exist). Returns the dressing's root.</summary>
        static Transform Dress(Transform arena, Transform warm, Transform hall, GameObject entryMarker)
        {
            var m = LoadMats();
            var root = Group(arena, "Dressing");
            root.gameObject.AddComponent<SkylineNoCollider>();
            var lights = Group(root, "Shop Lights");
            var props = Group(root, "Props");
            var rng = new System.Random(21);
            var kit = new SkylineKit();
            var inside = new SkylineKit();

            ReskinGameplay(m, warm, hall, entryMarker);
            HallShell(kit, inside, m, root, lights, props);
            SkylightAndMezzanine(kit, m, root, hall);
            Cover(kit, m, root, props, hall);
            Floors(kit, m, root);
            WarmupHall(kit, m, root, props, warm);
            LevelPieces(kit, m, root, warm, hall);
            City(kit, m, root, rng);

            int renderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            Debug.Log("SKYLINE_LOOK dressed: " + renderers + " dressing renderers, " + lights.childCount + " shop lights, " +
                      props.childCount + " props");
            return root;
        }

        // ================================================================================================ re-skinning

        /// <summary>The gameplay boxes keep their colliders and transforms; they get library materials with metre UVs. The
        /// main hall's walls and the mezzanine's deck lose their renderers (the shell and the deck dressing are drawn inside
        /// their volumes instead); the old shopfront strip, doorway bar and grey-box glass are replaced.</summary>
        static void ReskinGameplay(Mats m, Transform warm, Transform hall, GameObject entryMarker)
        {
            Skin(warm, "Floor", m.marble);
            foreach (var n in new[] { "Wall West", "Wall East", "Wall South" }) Skin(warm, n, m.plaster);
            Skin(warm, "Roof (glass)", m.glassSky, false);
            var rig = warm.Find("Practice Rig");
            if (rig != null) foreach (Transform t in rig) SkinGo(t.gameObject, m.hazard, true);
            var kiosk = warm.Find("Boost Kiosk");
            if (kiosk != null)
                foreach (Transform t in kiosk)
                    if (t.name.StartsWith("Counter Glass")) SkinGo(t.gameObject, m.glassCounter, false);

            Skin(hall, "Floor", m.terrazzo);
            Skin(hall, "Skylight Roof (glass)", m.glassSky, false);
            Skin(hall, "Mezzanine Balustrade (glass)", m.glassRail, false);
            foreach (var n in new[] { "Wall West", "Wall East", "Wall North", "Wall South West", "Wall South East", "Doorway Lintel", "Mezzanine Deck" })
                StripRenderer(hall.Find(n));
            var sign = hall.Find("Doorway Sign");
            if (sign != null) Object.DestroyImmediate(sign.gameObject);

            if (entryMarker != null)
            {
                // The lane's floor plate: the gold chevrons pointing into the hall, the texture fitted to its top face once.
                var r = entryMarker.GetComponent<MeshRenderer>();
                var b = r.bounds;
                var mesh = new Mesh { name = "Entry Marker (fitted)" };
                var t = entryMarker.transform;
                Vector3 lo = t.InverseTransformPoint(new Vector3(b.min.x, b.max.y, b.min.z)), hi = t.InverseTransformPoint(new Vector3(b.max.x, b.max.y, b.max.z));
                mesh.SetVertices(new List<Vector3> { new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z), new Vector3(hi.x, lo.y, hi.z), new Vector3(lo.x, lo.y, hi.z) });
                mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                entryMarker.GetComponent<MeshFilter>().sharedMesh = mesh;
                r.sharedMaterial = m.entry;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void Skin(Transform parent, string name, Material mat, bool metreUvs = true)
        {
            var t = parent.Find(name);
            if (t != null) SkinGo(t.gameObject, mat, metreUvs);
        }

        static void SkinGo(GameObject go, Material mat, bool metreUvs)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r == null || mat == null) return;
            r.sharedMaterial = mat;
            if (metreUvs) VhLookUv.BoxProject(go, true);
        }

        static void StripRenderer(Transform t)
        {
            if (t == null) return;
            var r = t.GetComponent<MeshRenderer>();
            if (r != null) Object.DestroyImmediate(r);
            var f = t.GetComponent<MeshFilter>();
            if (f != null) Object.DestroyImmediate(f);
        }

        // ================================================================================================ the hall's shell

        static void HallShell(SkylineKit kit, SkylineKit inside, Mats m, Transform root, Transform lights, Transform props)
        {
            float x0 = GalleriaArena.HallMinX, x1 = GalleriaArena.HallMaxX, z0 = GalleriaArena.HallMinZ, z1 = GalleriaArena.HallMaxZ;
            var west = new Wall(true, x0, -1f);
            var east = new Wall(true, x1, 1f);
            var north = new Wall(false, z1, 1f);
            var south = new Wall(false, z0, -1f);
            float side0 = z0 - WallThick, side1 = z1 + WallThick;
            int shop = 0;

            // West and east: five shops each, the curtain wall from 6 m.
            foreach (var w in new[] { west, east })
            {
                var holes = Bays(SideBays, BayHalf);
                LowerBand(kit, m, w, side0, side1, 0f, holes);
                Fill(kit, m.plaster, w, side0, side1, BandTop, 6f, 0f, WallThick, null);
                Reveal(kit, m, w, side0, side1, BandTop, null);
                Glazing(kit, m, w, side0, side1, 6f);
                foreach (var c in SideBays) Shop(kit, inside, m, w, c, 0f, shop++, lights, props);
            }

            // North: shops under the mezzanine, a second row of shops above it, then the curtain wall from 10 m.
            {
                var w = north;
                var holes = Bays(NorthBays, BayHalf);
                LowerBand(kit, m, w, x0, x1, 0f, holes);
                Fill(kit, m.plaster, w, x0, x1, BandTop, GalleriaArena.MezzanineY, 0f, WallThick, null);
                Reveal(kit, m, w, x0, x1, BandTop, null);
                LowerBand(kit, m, w, x0, x1, GalleriaArena.MezzanineY, holes);
                float upTop = GalleriaArena.MezzanineY + BandTop;
                Fill(kit, m.plaster, w, x0, x1, upTop, 10f, 0f, WallThick, null);
                Reveal(kit, m, w, x0, x1, upTop, null);
                Glazing(kit, m, w, x0, x1, 10f);
                foreach (var c in NorthBays) Shop(kit, inside, m, w, c, 0f, shop++, lights, props);
                foreach (var c in NorthBays) Shop(kit, inside, m, w, c, GalleriaArena.MezzanineY, shop++, lights, props);
            }

            // South (the warm-up hall is behind it): lit vitrines, the doorway's portal, the curtain wall above the warm-up roof.
            {
                var w = south;
                // The portal's jambs and header (PortalWidth) own the doorway's reveals and soffit, so no plaster face lies on theirs.
                var door = new Vector2(-DoorHalfWidth - PortalWidth, DoorHalfWidth + PortalWidth);
                var lowHoles = Bays(new float[0], 0f, door);
                var vitHoles = Bays(SouthVitrines, 2.4f, door);
                Fill(kit, m.plaster, w, x0, x1, 0f, 0.6f, 0f, WallThick, lowHoles);
                Fill(kit, m.plaster, w, x0, x1, 0.6f, 3f, 0f, WallThick, vitHoles);
                Fill(kit, m.plaster, w, x0, x1, 3f, DoorHeight + PortalWidth, 0f, WallThick, lowHoles);
                Fill(kit, m.plaster, w, x0, x1, DoorHeight + PortalWidth, 8.2f, 0f, WallThick, null);
                Fill(kit, m.dark, w, x0, x1, 0f, 0.12f, -0.01f, 0f, lowHoles);
                Fill(kit, m.marble, w, x0, x1, 0.12f, 0.6f, -Proud, 0f, lowHoles);
                Reveal(kit, m, w, x0, x1, BandTop, Bays(new float[0], 0f, new Vector2(-DoorHalfWidth - 0.45f, DoorHalfWidth + 0.45f)));
                Glazing(kit, m, w, x0, x1, 8.2f);
                int v = 0;
                foreach (var c in SouthVitrines) Vitrine(kit, m, w, c, v++);
                // The hall's side of the doorway: THE GALLERIA over it, in a gold frame.
                w.Box(kit, m.gold, -1.7f, 1.7f, 5.55f, 7.35f, -0.03f, 0f);
                w.Panel(kit, m.signs, 0f, 6.45f, 3.2f, 1.6f, SignUv(SignTheGalleria), -0.036f);
            }
            Portal(kit, m);

            kit.Flush(root, "Hall Shell");
            inside.Flush(root, "Shop Interiors", ShadowCastingMode.Off);
        }

        /// <summary>0..<see cref="BandTop"/> above <paramref name="y0"/>: cladding between the bays, marble wainscot and a steel
        /// skirting on it, and over the bays a graphite fascia (the shop's sign goes on it).</summary>
        static void LowerBand(SkylineKit k, Mats m, Wall w, float u0, float u1, float y0, List<Vector2> bays)
        {
            Fill(k, m.plaster, w, u0, u1, y0, y0 + BandTop, 0f, WallThick, bays);
            Fill(k, m.dark, w, u0, u1, y0, y0 + 0.12f, -0.01f, 0f, bays);
            Fill(k, m.marble, w, u0, u1, y0 + 0.12f, y0 + 1.2f, -Proud, 0f, bays);
            foreach (var b in bays) w.Box(k, m.dark, b.x, b.y, y0 + ShopOpen, y0 + BandTop, 0f, WallThick);
        }

        /// <summary>A gold reveal line just under <paramref name="y"/> along the wall (skipping <paramref name="holes"/>).</summary>
        static void Reveal(SkylineKit k, Mats m, Wall w, float u0, float u1, float y, List<Vector2> holes)
        {
            Fill(k, m.gold, w, u0, u1, y - 0.07f, y, -Proud, 0.02f, holes);
        }

        /// <summary>The curtain wall from <paramref name="y0"/> to the cornice: a glass pane in the slab, steel mullions every
        /// 2.5 m and transoms, a steel sill, then the white cornice with its gold line.</summary>
        static void Glazing(SkylineKit k, Mats m, Wall w, float u0, float u1, float y0)
        {
            // The pane at the runnable face (a wall-run above 6 m runs on the glass), the mullions just behind it.
            w.Box(k, m.glassCurtain, u0, u1, y0, CorniceY, 0.005f, 0.035f);
            w.Box(k, m.steel, u0, u1, y0 - 0.08f, y0, -0.06f, WallThick, 0.012f);
            int n = Mathf.Max(1, Mathf.RoundToInt((u1 - u0) / 2.5f));
            float step = (u1 - u0) / n;
            for (int i = 0; i <= n; i++)
            {
                float u = u0 + i * step;
                w.Box(k, m.steel, Mathf.Max(u0, u - 0.05f), Mathf.Min(u1, u + 0.05f), y0, CorniceY, 0.04f, 0.33f, 0.012f);
            }
            int rows = Mathf.Max(1, Mathf.RoundToInt((CorniceY - y0) / 2.4f));
            for (int r = 1; r < rows; r++)
            {
                float y = y0 + (CorniceY - y0) * r / rows;
                w.Box(k, m.steel, u0, u1, y - 0.05f, y + 0.05f, 0.04f, 0.33f, 0.012f);
            }
            w.Box(k, m.plaster, u0, u1, CorniceY, HallHeight, 0f, WallThick, 0.04f);
            w.Box(k, m.gold, u0, u1, CorniceY, CorniceY + 0.06f, -Proud, 0.02f);
        }

        /// <summary>
        /// One shop: a steel-framed glass front in the wall's slab (flush with the runnable face, so a wall-run runs on the
        /// glass and the gondolas' mines sit on it), the brand's sign on the fascia, and behind the glass a lit room 4.2 m deep:
        /// floor, painted walls, a ceiling with light panels, shelves of goods on both sides, a lightbox poster on the back wall,
        /// a display table or plants, and one warm point light (no shadows).
        /// </summary>
        static void Shop(SkylineKit k, SkylineKit inside, Mats m, Wall w, float c, float y0, int index, Transform lights, Transform props)
        {
            float a = c - BayHalf, b = c + BayHalf, D = ShopDepth;
            int brand = HallBrands[index % HallBrands.Length];
            if (brand < 0) { LiftDoors(k, m, w, c, w.alongZ ? c : LiftDoorX, y0); return; }
            // The front.
            w.Box(k, m.steel, a, b, y0, y0 + 0.3f, 0f, 0.12f);
            w.Box(k, m.glassShop, a + 0.06f, b - 0.06f, y0 + 0.3f, y0 + ShopOpen - 0.3f, 0.04f, 0.06f);
            foreach (var u in new[] { a, c - 0.04f, b - 0.08f })
                w.Box(k, m.steel, u, u + 0.08f, y0, y0 + ShopOpen - 0.3f, 0f, 0.12f);
            w.Box(k, m.dark, a, b, y0 + ShopOpen - 0.3f, y0 + ShopOpen, 0f, WallThick);
            // The sign on the fascia (graphite, ShopOpen..BandTop), between two gold strips.
            w.Panel(k, m.signs, c, y0 + (ShopOpen + BandTop) * 0.5f, 1.8f, 0.9f, ShopSignUv(brand));
            w.Box(k, m.gold, a + 0.15f, c - 1.05f, y0 + 3.92f, y0 + 3.98f, -Proud, 0f);
            w.Box(k, m.gold, c + 1.05f, b - 0.15f, y0 + 3.92f, y0 + 3.98f, -Proud, 0f);

            // The room behind (no shadows: the shell's roof over it casts them).
            var wallM = m.shopWalls[index % m.shopWalls.Length];
            var floorM = m.shopFloors[index % m.shopFloors.Length];
            w.Box(inside, floorM, a, b, y0 - 0.05f, y0, 0.06f, D);
            w.Box(inside, m.ceiling, a, b, y0 + ShopOpen, y0 + ShopOpen + 0.1f, WallThick, D);
            w.Box(k, m.concrete, a - 0.1f, b + 0.1f, y0 + ShopOpen + 0.1f, y0 + ShopOpen + 0.25f, WallThick, D + 0.1f);
            w.Box(inside, wallM, a - 0.1f, a, y0, y0 + ShopOpen + 0.1f, WallThick, D);
            w.Box(inside, wallM, b, b + 0.1f, y0, y0 + ShopOpen + 0.1f, WallThick, D);
            w.Box(inside, wallM, a - 0.1f, b + 0.1f, y0, y0 + ShopOpen + 0.1f, D, D + 0.1f);
            // Light panels in the ceiling.
            for (int i = 0; i < 2; i++)
            {
                var p = w.P(c, y0 + ShopOpen - 0.005f, 1.3f + i * 1.7f);
                if (w.alongZ) CeilingPanel(inside, m.panel, p, 0.5f, 3.2f);
                else CeilingPanel(inside, m.panel, p, 3.2f, 0.5f);
            }
            // The back wall: the brand's own lightbox advert in a graphite frame.
            w.Box(inside, m.dark, c - 0.85f, c + 0.85f, y0 + 1.05f, y0 + 2.75f, D - 0.05f, D);
            PanelAt(inside, m.posters, w.P(c, y0 + 1.9f, D - 0.055f), w.In, 1.5f, 1.5f, PosterUv(BrandPoster(brand)));
            // The fit-out (judge A 9: every shop had the same shelves): by what the brand sells.
            var fit = BrandFit[brand % BrandFit.Length];
            var rnd = new System.Random(1000 + index);
            float d0 = 0.9f, d1 = D - 0.25f;
            foreach (int side in new[] { -1, 1 })
            {
                int row = side < 0 ? fit.left : fit.right;
                switch (fit.fit)
                {
                    case Fit.Rails: Rails(inside, m, w, side, c, y0, d0, d1, index); break;
                    case Fit.Screens: SideScreens(inside, m, w, side, c, y0, d0, d1, index * 3 + (side > 0 ? 2 : 0)); break;
                    case Fit.Plinths: SideLightbox(inside, m, w, side, c, y0, d0, d1, BrandPoster(brand) + 1 + (side > 0 ? 5 : 0)); break;
                    default: Shelves(inside, m, w, side, c, y0, d0, d1, row, rnd); break;
                }
            }
            switch (fit.fit)
            {
                case Fit.Plants:
                {
                    var plant = SkylineLookLibrary.Prop("Calathea", props, w.P(c - 1f, y0, 1.6f), index * 37f, 0.9f);
                    var fern = SkylineLookLibrary.Prop("Fern", props, w.P(c + 0.9f, y0, 2.4f), index * 53f, 0.6f);
                    foreach (var go in new[] { plant, fern }) if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                    break;
                }
                case Fit.Plinths:
                    // Three white plinths with gold tops, one piece on each, staggered.
                    for (int i = 0; i < 3; i++)
                    {
                        float u = c - 0.95f + i * 0.95f, d = D * 0.5f + (i == 1 ? 0.45f : -0.25f), hgt = 0.85f + 0.2f * ((index + i) % 3);
                        w.Box(inside, m.plaster, u - 0.22f, u + 0.22f, y0, y0 + hgt, d - 0.22f, d + 0.22f);
                        w.Box(inside, m.gold, u - 0.24f, u + 0.24f, y0 + hgt, y0 + hgt + 0.03f, d - 0.24f, d + 0.24f);
                        w.Box(inside, m.products, u - 0.09f, u + 0.09f, y0 + hgt + 0.03f, y0 + hgt + 0.22f, d - 0.07f, d + 0.07f);
                    }
                    break;
                case Fit.Rails:
                    // A low table of folded clothes in the brand's colours.
                    w.Box(inside, m.cherry, c - 0.65f, c + 0.65f, y0, y0 + 0.75f, D * 0.5f - 0.35f, D * 0.5f + 0.35f);
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 3; j++)
                        {
                            float u = c - 0.42f + i * 0.42f, y = y0 + 0.75f + j * 0.07f;
                            w.Box(inside, m.shopWalls[(index + i * 2 + j) % m.shopWalls.Length], u - 0.17f, u + 0.17f, y, y + 0.065f, D * 0.5f - 0.2f, D * 0.5f + 0.2f);
                        }
                    break;
                default:
                    w.Box(inside, m.cherry, c - 0.7f, c + 0.7f, y0, y0 + 0.85f, D * 0.5f - 0.4f, D * 0.5f + 0.4f);
                    for (int i = 0; i < 3; i++)
                    {
                        float u = c - 0.45f + i * 0.45f;
                        w.Box(inside, m.products, u - 0.15f, u + 0.15f, y0 + 0.85f, y0 + 1.05f + 0.1f * ((index + i) % 3), D * 0.5f - 0.2f, D * 0.5f + 0.2f);
                    }
                    break;
            }
            if (brand == 9)
            {
                var sign = SkylineLookLibrary.Prop("WetFloorSign", props, w.P(c + 1.2f, y0, 1.0f), 200f, 0.62f);
                if (sign != null) foreach (var r in sign.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            // One warm light per shop (realtime, no shadows; Forward+).
            var light = new GameObject("Shop Light " + index).AddComponent<Light>();
            light.transform.SetParent(lights, false);
            light.transform.position = w.P(c, y0 + ShopOpen - 0.4f, D * 0.5f);
            light.type = LightType.Point;
            light.range = 5.5f;
            light.intensity = 3f;
            light.color = ShopLightColor;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Mixed;
        }

        /// <summary>The brand's own advert (SK_Posters: cells 0-11 the first twelve brands, 14 GILT &amp; GRAIN, 15 PIXEL ARCADE).</summary>
        static int BrandPoster(int brand) => brand < ShopBrands ? brand : brand == 12 ? 14 : brand == 14 ? 15 : (brand * 7) % ShopBrands;

        /// <summary>A shelving unit against a shop's side wall: four shelves of one kind of goods, each shelf its own stretch
        /// of the row (so no two shelves match).</summary>
        static void Shelves(SkylineKit k, Mats m, Wall w, int side, float c, float y0, float d0, float d1, int row, System.Random rnd)
        {
            float face = c + side * (BayHalf - 0.55f), back = c + side * BayHalf, len = d1 - d0;
            w.Box(k, m.pine, Mathf.Min(face, back), Mathf.Max(face, back), y0, y0 + 2.2f, d0, d1);
            var normal = side < 0 ? w.U : -w.U;
            for (int s = 0; s < 4; s++)
            {
                var center = w.P(face, y0 + 0.25f + (s + 0.5f) * 0.475f, d0 + len * 0.5f) + normal * 0.006f;
                PanelAt(k, m.products, center, normal, len, 0.475f, GoodsUv(row, (float)rnd.NextDouble(), len / 3.8f));
            }
        }

        /// <summary>A clothes rail along a shop's side wall, hung with garments in the brand's paints.</summary>
        static void Rails(SkylineKit k, Mats m, Wall w, int side, float c, float y0, float d0, float d1, int index)
        {
            float uWall = c + side * BayHalf, uIn = c + side * (BayHalf - 0.55f), uRail = (uWall + uIn) * 0.5f;
            w.Box(k, m.steel, uRail - 0.015f, uRail + 0.015f, y0 + 1.72f, y0 + 1.75f, d0, d1);
            foreach (float d in new[] { d0 + 0.02f, d1 - 0.05f }) w.Box(k, m.steel, uRail - 0.015f, uRail + 0.015f, y0, y0 + 1.72f, d, d + 0.03f);
            int g = 0;
            for (float d = d0 + 0.12f; d < d1 - 0.12f; d += 0.11f, g++)
            {
                var mat = m.shopWalls[(index * 3 + g * 5 + g / 3) % m.shopWalls.Length];
                float len = 0.72f + 0.12f * ((g * 7 + index) % 3);
                w.Box(k, mat, Mathf.Min(uWall, uIn) + 0.04f, Mathf.Max(uWall, uIn) - 0.02f, y0 + 1.7f - len, y0 + 1.7f, d, d + 0.05f);
            }
        }

        /// <summary>A wall of four glowing screens (the posters' adverts) on a graphite panel against a shop's side wall.</summary>
        static void SideScreens(SkylineKit k, Mats m, Wall w, int side, float c, float y0, float d0, float d1, int cell)
        {
            float uWall = c + side * BayHalf, len = d1 - d0;
            w.Box(k, m.dark, uWall, uWall - side * 0.04f, y0 + 0.8f, y0 + 2.95f, d0, d1);
            var normal = side < 0 ? w.U : -w.U;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    PanelAt(k, m.posters, w.P(uWall - side * 0.046f, y0 + 1.35f + i * 1.0f, d0 + len * (0.25f + 0.5f * j)), normal, 0.88f, 0.88f, PosterUv(cell + i * 2 + j));
        }

        /// <summary>One big lightbox advert on a shop's side wall (the display shops).</summary>
        static void SideLightbox(SkylineKit k, Mats m, Wall w, int side, float c, float y0, float d0, float d1, int cell)
        {
            float uWall = c + side * BayHalf, dc = (d0 + d1) * 0.5f;
            w.Box(k, m.dark, uWall, uWall - side * 0.04f, y0 + 0.95f, y0 + 2.65f, dc - 0.85f, dc + 0.85f);
            PanelAt(k, m.posters, w.P(uWall - side * 0.046f, y0 + 1.8f, dc), side < 0 ? w.U : -w.U, 1.5f, 1.5f, PosterUv(cell));
        }

        /// <summary>
        /// The back wall of the service lift's lobby (the level's far goal; judge A 15, spec 10.4), in the north wall's bay
        /// at <paramref name="c"/>: brushed-gold lift doors centred on <paramref name="doorC"/> in a steel frame on a cream
        /// marble recess filling the bay, two glowing light columns and an indicator over the doors (bloom finds them), SKY
        /// TERRACE on the fascia. All within the wall's slab (nothing proud of the runnable face). The lobby itself (walls,
        /// shutter, its own sign) is the level's (the builder's gameplay pieces).
        /// </summary>
        static void LiftDoors(SkylineKit k, Mats m, Wall w, float c, float doorC, float y0)
        {
            float a = c - BayHalf, b = c + BayHalf, d = doorC;
            w.Box(k, m.marble, a, b, y0, y0 + ShopOpen, 0.14f, WallThick);
            w.Box(k, m.steel, a, b, y0, y0 + 0.1f, 0f, 0.14f);
            w.Box(k, m.steel, d - 1.32f, d - 1.18f, y0, y0 + 2.95f, 0f, 0.14f, 0.01f);
            w.Box(k, m.steel, d + 1.18f, d + 1.32f, y0, y0 + 2.95f, 0f, 0.14f, 0.01f);
            w.Box(k, m.steel, d - 1.32f, d + 1.32f, y0 + 2.8f, y0 + 2.95f, 0f, 0.14f, 0.01f);
            w.Box(k, m.gold, d - 1.18f, d - 0.015f, y0 + 0.1f, y0 + 2.8f, 0.1f, 0.14f);
            w.Box(k, m.gold, d + 0.015f, d + 1.18f, y0 + 0.1f, y0 + 2.8f, 0.1f, 0.14f);
            w.Box(k, m.dark, d - 0.015f, d + 0.015f, y0 + 0.1f, y0 + 2.8f, 0.11f, 0.14f);
            foreach (float u in new[] { d - 1.5f, d + 1.5f })
                PanelAt(k, m.panel, w.P(u, y0 + 1.6f, 0.135f), w.In, 0.12f, 2.6f, new Rect(0f, 0f, 1f, 1f));
            PanelAt(k, m.panel, w.P(d, y0 + 3.15f, 0.135f), w.In, 0.9f, 0.1f, new Rect(0f, 0f, 1f, 1f));
            w.Panel(k, m.signs, c, y0 + (ShopOpen + BandTop) * 0.5f, 1.8f, 0.9f, SignUv(SignLift));
        }

        /// <summary>A shallow lit vitrine in the south wall's slab: glass at the face, a poster lightbox 0.3 m back, a steel
        /// frame round the opening, the slab closed behind it (the warm-up hall's side).</summary>
        static void Vitrine(SkylineKit k, Mats m, Wall w, float c, int index)
        {
            float a = c - 2.4f, b = c + 2.4f;
            w.Box(k, m.plaster, a, b, 0.6f, 3f, 0.32f, WallThick);
            w.Box(k, m.dark, a, b, 0.6f, 0.66f, 0f, 0.32f);
            w.Box(k, m.dark, a, b, 2.94f, 3f, 0f, 0.32f);
            w.Box(k, m.dark, a, a + 0.06f, 0.66f, 2.94f, 0f, 0.32f);
            w.Box(k, m.dark, b - 0.06f, b, 0.66f, 2.94f, 0f, 0.32f);
            w.Box(k, m.glassVitrine, a + 0.06f, b - 0.06f, 0.66f, 2.94f, 0.03f, 0.05f);
            for (int i = 0; i < 2; i++)
            {
                float uc = c - 1.15f + i * 2.3f;
                PanelAt(k, m.posters, w.P(uc, 1.8f, 0.315f), w.In, 2.1f, 2.1f, PosterUv(index * 2 + i + 6));
            }
        }

        /// <summary>The doorway's portal on both faces of the shared wall: brushed-steel jambs and header in a gold frame,
        /// 5 cm proud of each face (out of the opening: its reveals are the collider's edges).</summary>
        static void Portal(SkylineKit k, Mats m)
        {
            float z = GalleriaArena.HallMinZ, t = WallThick, p = 0.05f, pw = PortalWidth;
            float zLo = z - t - p, zHi = z + p;
            k.Box(m.steel, new Vector3(-DoorHalfWidth - pw, 0f, zLo), new Vector3(-DoorHalfWidth, DoorHeight, zHi), 0.025f);
            k.Box(m.steel, new Vector3(DoorHalfWidth, 0f, zLo), new Vector3(DoorHalfWidth + pw, DoorHeight, zHi), 0.025f);
            k.Box(m.steel, new Vector3(-DoorHalfWidth - pw, DoorHeight, zLo), new Vector3(DoorHalfWidth + pw, DoorHeight + pw, zHi), 0.025f);
            foreach (float face in new[] { zLo - 0.01f, zHi })
            {
                k.Box(m.gold, new Vector3(-DoorHalfWidth - 0.45f, 0f, face), new Vector3(-DoorHalfWidth - 0.35f, DoorHeight + 0.45f, face + 0.01f));
                k.Box(m.gold, new Vector3(DoorHalfWidth + 0.35f, 0f, face), new Vector3(DoorHalfWidth + 0.45f, DoorHeight + 0.45f, face + 0.01f));
                k.Box(m.gold, new Vector3(-DoorHalfWidth - 0.45f, DoorHeight + 0.35f, face), new Vector3(DoorHalfWidth + 0.45f, DoorHeight + 0.45f, face + 0.01f));
            }
        }

        // ================================================================================================ skylight, mezzanine

        static void SkylightAndMezzanine(SkylineKit k, Mats m, Transform root, Transform hall)
        {
            float x0 = GalleriaArena.HallMinX, x1 = GalleriaArena.HallMaxX, z0 = GalleriaArena.HallMinZ, z1 = GalleriaArena.HallMaxZ;
            // The skylight's structure: white beams every 5 m across the hall, steel mullions every 2.5 m along it.
            for (float z = z0 + 5f; z < z1 - 0.1f; z += 5f)
            {
                k.Box(m.plaster, new Vector3(x0, HallHeight - 0.7f, z - 0.18f), new Vector3(x1, HallHeight, z + 0.18f), 0.03f);
                k.Box(m.gold, new Vector3(x0, HallHeight - 0.72f, z - 0.06f), new Vector3(x1, HallHeight - 0.7f, z + 0.06f));
            }
            for (float x = x0 + 2.5f; x < x1 - 0.1f; x += 2.5f)
                k.Box(m.steel, new Vector3(x - 0.04f, HallHeight - 0.14f, z0), new Vector3(x + 0.04f, HallHeight, z1));
            // Fritted bands on the glass (judge A 7): a milky metre along every beam and round the edge, so the roof reads as
            // glass from the floor, not an open frame.
            float fy = HallHeight - 0.004f;
            for (float z = z0 + 5f; z < z1 - 0.1f; z += 5f) CeilingPanel(k, m.frit, new Vector3(0f, fy, z), x1 - x0, 1.1f);
            CeilingPanel(k, m.frit, new Vector3(0f, fy, z0 + 0.5f), x1 - x0, 1f);
            CeilingPanel(k, m.frit, new Vector3(0f, fy, z1 - 0.5f), x1 - x0, 1f);
            CeilingPanel(k, m.frit, new Vector3(x0 + 0.5f, fy, 0f), 1f, z1 - z0 - 2f);
            CeilingPanel(k, m.frit, new Vector3(x1 - 0.5f, fy, 0f), 1f, z1 - z0 - 2f);

            // Banners hung from the beams, well above any reach (bottom 10 m; the deck's top is 5 m): navy and white over the
            // west aisle, teal over the east one, SKY TERRACE in front of the deck over the service lift's lobby. The hall's halves
            // differ at a glance (judge B 3: "every bearing looks alike"), and the far goal is announced from the doorway.
            HangBanner(k, m, new Vector3(-10f, 0f, -10f), BannerConcierge);
            HangBanner(k, m, new Vector3(-10f, 0f, 0f), BannerGalleria);
            HangBanner(k, m, new Vector3(10f, 0f, -10f), BannerFoodCourt);
            HangBanner(k, m, new Vector3(10f, 0f, 0f), BannerFoodCourt);
            HangBanner(k, m, new Vector3(LiftDoorX - 1.2f, 0f, 10f), BannerSkyTerrace);
            HangBanner(k, m, new Vector3(LiftDoorX + 1.2f, 0f, 10f), BannerSkyTerrace);

            // The mezzanine (exactly the data's slab; its renderer is gone): terrazzo on top, white body, a ceiling-tile soffit
            // with downlights, gold and graphite lines on its front edge, a steel handrail and posts behind the glass.
            var d = GalleriaArena.MezzanineDeck;
            float dz0 = d.min.Z, dz1 = d.max.Z, y0 = d.min.Y, y1 = d.max.Y;
            k.Box(m.terrazzo, new Vector3(x0, y1 - 0.07f, dz0), new Vector3(x1, y1, dz1), 0.015f);
            k.Box(m.plaster, new Vector3(x0, y0 + 0.02f, dz0), new Vector3(x1, y1 - 0.07f, dz1), 0.02f);
            k.Box(m.ceiling, new Vector3(x0, y0, dz0), new Vector3(x1, y0 + 0.02f, dz1));
            k.Box(m.gold, new Vector3(x0, y0 + 0.12f, dz0 - Proud), new Vector3(x1, y0 + 0.17f, dz0));
            k.Box(m.dark, new Vector3(x0, y0, dz0 - Proud), new Vector3(x1, y0 + 0.06f, dz0));
            for (float x = x0 + 2f; x < x1; x += 4f)
                for (float z = dz0 + 1.6f; z < dz1; z += 3f)
                    k.Disc(m.panel, new Vector3(x, y0 - 0.004f, z), Vector3.down, 0.17f);
            k.Box(m.steel, new Vector3(x0, y1 + 1.02f, dz0 - 0.02f), new Vector3(x1, y1 + 1.09f, dz0 + 0.08f), 0.015f);
            for (float x = x0 + 1.25f; x < x1; x += 2.5f)
                k.Box(m.steel, new Vector3(x - 0.025f, y1, dz0 + 0.055f), new Vector3(x + 0.025f, y1 + 1.02f, dz0 + 0.1f));
            foreach (Transform t in hall)
            {
                if (!t.name.StartsWith("Spotlight Plinth")) continue;
                var b = t.GetComponent<Renderer>().bounds;
                Bevelled(k, t, m.dark, 0.02f);
                k.Box(m.gold, new Vector3(b.min.x - Proud, b.max.y - 0.08f, b.min.z - Proud), new Vector3(b.max.x + Proud, b.max.y - 0.03f, b.max.z + Proud));
            }
            k.Flush(root, "Skylight and Mezzanine");
        }

        /// <summary>A two-sided fabric banner (1.6 x 3.2 m, facing north and south) on a steel rod under the beam at
        /// <paramref name="at"/>'s z, its bottom at 10.05 m.</summary>
        static void HangBanner(SkylineKit k, Mats m, Vector3 at, int cell)
        {
            float top = HallHeight - 0.72f, w = 1.6f, h = 3.2f;
            k.Box(m.steel, new Vector3(at.x - w * 0.5f - 0.06f, top - 0.04f, at.z - 0.025f), new Vector3(at.x + w * 0.5f + 0.06f, top, at.z + 0.025f));
            foreach (var n in new[] { Vector3.forward, Vector3.back })
                PanelAt(k, m.banners, new Vector3(at.x, top - 0.04f - h * 0.5f, at.z) + n * 0.006f, n, w, h, BannerUv(cell));
        }

        // ================================================================================================ the cover

        static void Cover(SkylineKit k, Mats m, Transform root, Transform props, Transform hall)
        {
            var cover = hall.Find("Cover");
            if (cover == null) return;
            int stall = 0, booth = 0, kiosk = 0;
            var planterPlants = Group(root, "Planter Plants");
            planterPlants.gameObject.AddComponent<SkylineNoCollider>();
            foreach (Transform t in cover)
            {
                var r = t.GetComponent<MeshRenderer>();
                if (r == null) continue;
                var b = r.bounds;
                string n = t.name;
                if (n.StartsWith("Pillar"))
                {
                    if (n.EndsWith("Cap")) { SkinGo(t.gameObject, m.plaster, true); continue; }   // flush with the deck: no bevel
                    Bevelled(k, t, m.plaster, 0.03f);
                    Collar(k, m.dark, b, 0f, 0.15f, 0.015f, 0.008f);
                    Collar(k, m.gold, b, 1.1f, 1.16f, Proud, 0.006f);
                    Collar(k, m.gold, b, b.max.y - 0.3f, b.max.y, 0.02f, 0.012f);
                }
                else if (n.StartsWith("Food Stall"))
                {
                    Bevelled(k, t, m.pine, 0.02f);
                    Collar(k, m.dark, b, 0f, 0.15f, 0.015f, 0.008f);
                    Ring(k, m.steel, b, b.max.y - 0.04f, b.max.y + 0.02f, Proud);
                    // A menu board on each long face (x faces), and a steel ledge under it.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side < 0 ? b.min.x : b.max.x;
                        var normal = new Vector3(side, 0f, 0f);
                        float zc = b.center.z;
                        k.Box(m.dark, new Vector3(x, 2.7f, zc - 1.75f) + normal * 0.0f, new Vector3(x + side * 0.008f, 4.6f, zc + 1.75f));
                        // Each stall is one kitchen with its own menu on both faces (judge A 9: the boards were all alike).
                        PanelAt(k, m.signs, new Vector3(x + side * 0.014f, 3.65f, zc), normal, 3.4f, 1.7f, SignUv(SignMenu0 + stall % 4));
                        k.Box(m.steel, new Vector3(x, 1.04f, zc - 1.9f), new Vector3(x + side * 0.03f, 1.1f, zc + 1.9f));
                    }
                    stall++;
                }
                else if (n.StartsWith("Info Booth"))
                {
                    Bevelled(k, t, m.plaster, 0.02f);
                    Collar(k, m.marble, b, 0f, 0.45f, Proud, 0.008f);
                    Ring(k, m.gold, b, b.max.y - 0.06f, b.max.y, 0.015f);
                    bool back = n.EndsWith("Back");
                    if (back)
                    {
                        Screen(k, m, new Vector3(b.center.x, 2.9f, b.min.z), Vector3.back, 4.8f, 2.4f, SignUv(SignConcierge));
                        Screen(k, m, new Vector3(b.center.x, 2.9f, b.max.z), Vector3.forward, 4.8f, 2.4f, SignUv(SignDoNotRun));
                    }
                    else
                    {
                        bool westSide = b.center.x < 0f;
                        var outward = westSide ? Vector3.left : Vector3.right;
                        float xo = westSide ? b.min.x : b.max.x, xi = westSide ? b.max.x : b.min.x;
                        Screen(k, m, new Vector3(xo, 3f, b.center.z), outward, 3.6f, 1.8f, SignUv(booth == 0 ? SignWelcome : SignTheGalleria));
                        ScreenPoster(k, m, new Vector3(xi, 2.9f, b.center.z), -outward, 2f, 11 + booth);
                    }
                    booth++;
                }
                else if (n.StartsWith("Kiosk"))
                {
                    Bevelled(k, t, m.marble, 0.03f);
                    Collar(k, m.dark, b, 0f, 0.15f, 0.015f, 0.008f);
                    Ring(k, m.gold, b, b.max.y - 0.08f, b.max.y, 0.015f);
                    // Lightbox adverts: two on each long face, one on each end.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float z = side < 0 ? b.min.z : b.max.z;
                        for (int i = 0; i < 2; i++)
                            ScreenPoster(k, m, new Vector3(b.center.x - 1f + 2f * i, 1.45f, z), new Vector3(0f, 0f, side), 1.1f, kiosk * 4 + i + (side > 0 ? 2 : 0));
                        float x = side < 0 ? b.min.x : b.max.x;
                        ScreenPoster(k, m, new Vector3(x, 1.45f, b.center.z), new Vector3(side, 0f, 0f), 1.1f, kiosk * 4 + 9 + (side > 0 ? 1 : 0));
                    }
                    kiosk++;
                }
                else if (n.StartsWith("Planter"))
                {
                    // Hollow marble planter (the solid stays the data's box): rim walls, soil, then plants (visual only).
                    StripRenderer(t);
                    float rim = 0.08f, rb = 0.012f;
                    k.Box(m.marble, new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.max.x, b.max.y, b.min.z + rim), rb);
                    k.Box(m.marble, new Vector3(b.min.x, 0f, b.max.z - rim), new Vector3(b.max.x, b.max.y, b.max.z), rb);
                    k.Box(m.marble, new Vector3(b.min.x, 0f, b.min.z + rim), new Vector3(b.min.x + rim, b.max.y, b.max.z - rim), rb);
                    k.Box(m.marble, new Vector3(b.max.x - rim, 0f, b.min.z + rim), new Vector3(b.max.x, b.max.y, b.max.z - rim), rb);
                    k.Box(m.soil, new Vector3(b.min.x + rim, 0f, b.min.z + rim), new Vector3(b.max.x - rim, b.max.y - 0.08f, b.max.z - rim));
                    Ring(k, m.gold, b, b.max.y - 0.04f, b.max.y, 0.01f);
                    float top = b.max.y - 0.08f;
                    SkylineLookLibrary.Prop("Calathea", planterPlants, new Vector3(b.center.x - 0.45f, top, b.center.z), 20f + b.center.x * 7f, 0.75f);
                    SkylineLookLibrary.Prop("Calathea", planterPlants, new Vector3(b.center.x + 0.5f, top, b.center.z), 140f + b.center.x * 3f, 0.65f);
                    SkylineLookLibrary.Prop("Fern", planterPlants, new Vector3(b.center.x + 0.02f, top, b.center.z), 75f, 0.45f);
                }
            }
            k.Flush(root, "Cover Dressing");
        }

        /// <summary>A band round a box's four sides from <paramref name="y0"/> to <paramref name="y1"/>, <paramref name="out"/> proud.</summary>
        static void Ring(SkylineKit k, Material mat, Bounds b, float y0, float y1, float @out)
        {
            // Four strips (not one box: a ring at a box's top must leave its top face alone), each lightly bevelled.
            float bv = Mathf.Min(0.006f, @out * 0.4f);
            k.Box(mat, new Vector3(b.min.x - @out, y0, b.min.z - @out), new Vector3(b.max.x + @out, y1, b.min.z), bv);
            k.Box(mat, new Vector3(b.min.x - @out, y0, b.max.z), new Vector3(b.max.x + @out, y1, b.max.z + @out), bv);
            k.Box(mat, new Vector3(b.min.x - @out, y0, b.min.z), new Vector3(b.min.x, y1, b.max.z), bv);
            k.Box(mat, new Vector3(b.max.x, y0, b.min.z), new Vector3(b.max.x + @out, y1, b.max.z), bv);
        }

        /// <summary>A bevelled collar round a box (a base, a band, a capital): one box <paramref name="out"/> larger on every
        /// side, its inside hidden in the box.</summary>
        static void Collar(SkylineKit k, Material mat, Bounds b, float y0, float y1, float @out, float bevel)
        {
            k.Box(mat, new Vector3(b.min.x - @out, y0, b.min.z - @out), new Vector3(b.max.x + @out, y1, b.max.z + @out), bevel);
        }

        /// <summary>A gameplay box drawn bevelled: its renderer goes (its collider and transform stay exactly as they are) and a
        /// chamfered box of the same bounds is drawn in the kit, so its edges catch the sun. The chamfer only cuts inside the
        /// solid: nothing is drawn outside the collider.</summary>
        static void Bevelled(SkylineKit k, Transform t, Material mat, float bevel)
        {
            var r = t.GetComponent<MeshRenderer>();
            if (r == null) return;
            var b = r.bounds;
            StripRenderer(t);
            k.Box(mat, b.min, b.max, bevel);
        }

        /// <summary>An advertising screen on a face: a graphite frame 8 mm proud, the glowing sign cell 14 mm proud.</summary>
        static void Screen(SkylineKit k, Mats m, Vector3 faceCenter, Vector3 normal, float w, float h, Rect uv)
        {
            var right = Vector3.Cross(normal, Vector3.up).normalized;
            var half = right * (w * 0.5f + 0.08f) + Vector3.up * (h * 0.5f + 0.08f);
            var a = faceCenter - half;
            var b = faceCenter + half + normal * 0.008f;
            k.Box(m.dark, a, b);
            PanelAt(k, m.signs, faceCenter + normal * 0.014f, normal, w, h, uv);
        }

        static void ScreenPoster(SkylineKit k, Mats m, Vector3 faceCenter, Vector3 normal, float size, int cell)
        {
            var right = Vector3.Cross(normal, Vector3.up).normalized;
            var half = right * (size * 0.5f + 0.06f) + Vector3.up * (size * 0.5f + 0.06f);
            k.Box(m.gold, faceCenter - half, faceCenter + half + normal * 0.008f);
            PanelAt(k, m.posters, faceCenter + normal * 0.014f, normal, size, size, PosterUv(cell));
        }

        // ================================================================================================ floors

        /// <summary>The main hall's gold inlay (4 mm strips: a border 1.2 m in from the walls, two aisle lines, an octagonal
        /// medallion between the kiosks) and marble under the mezzanine. None crosses a cover piece.</summary>
        static void Floors(SkylineKit k, Mats m, Transform root)
        {
            const float h = 0.006f, w = 0.07f;   // 6 mm: no depth fighting with the floor at the hall's far end
            float bx = 23.8f, bz0 = -18.8f, bz1 = 11.2f;
            k.Box(m.gold, new Vector3(-bx, 0f, bz0 - w * 0.5f), new Vector3(bx, h, bz0 + w * 0.5f));
            k.Box(m.gold, new Vector3(-bx, 0f, bz1 - w * 0.5f), new Vector3(bx, h, bz1 + w * 0.5f));
            k.Box(m.gold, new Vector3(-bx - w * 0.5f, 0f, bz0), new Vector3(-bx + w * 0.5f, h, bz1));
            k.Box(m.gold, new Vector3(bx - w * 0.5f, 0f, bz0), new Vector3(bx + w * 0.5f, h, bz1));
            foreach (float x in new[] { -10f, 10f }) k.Box(m.gold, new Vector3(x - w * 0.5f, 0f, bz0), new Vector3(x + w * 0.5f, h, bz1));
            foreach (float r in new[] { 3.2f, 2.6f, 0.6f })
                for (int i = 0; i < 8; i++)
                {
                    float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                    float len = 2f * r * Mathf.Sin(22.5f * Mathf.Deg2Rad) + w;
                    var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (r * Mathf.Cos(22.5f * Mathf.Deg2Rad));
                    float yaw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg;
                    k.BoxYaw(m.gold, c + Vector3.up * (h * 0.5f), new Vector3(len, h, w), yaw);
                }
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f;
                k.BoxYaw(m.gold, c + Vector3.up * (h * 0.5f), new Vector3(2f, h, w), -i * 45f);
            }
            var d = GalleriaArena.MezzanineDeck;
            k.Box(m.marble, new Vector3(GalleriaArena.HallMinX + 0.05f, 0f, d.min.Z + 0.6f), new Vector3(GalleriaArena.HallMaxX - 0.05f, 0.005f, d.max.Z - 0.05f));
            k.Flush(root, "Floor Inlay", ShadowCastingMode.Off);
        }

        // ================================================================================================ the warm-up hall

        static void WarmupHall(SkylineKit k, Mats m, Transform root, Transform props, Transform warm)
        {
            float wx0 = GalleriaArena.WarmupMinX, wx1 = GalleriaArena.WarmupMaxX, wz0 = GalleriaArena.WarmupMinZ;
            float wz1 = GalleriaArena.HallMinZ - WallThick;
            var west = new Wall(true, wx0, -1f);
            var east = new Wall(true, wx1, 1f);
            var south = new Wall(false, wz0, -1f);
            var north = new Wall(false, wz1, 1f);   // the main hall's south wall, seen from the warm-up side
            float[] bays = { -43.5f, -37.5f, -31.5f, -25.5f };
            int brand = 4;
            foreach (var w in new[] { west, east })
            {
                var holes = Bays(bays, 2.2f);
                Trim(k, m, w, wz0, wz1, holes);
                foreach (var pu in new[] { -47.3f, -40.5f, -34.5f, -28.5f, -21.6f }) Pilaster(k, m, w, pu);
                int i = 0;
                int[] bannerCells = { BannerGalleria, BannerConcierge, BannerSkyTerrace, BannerConcierge };
                foreach (var c in bays)
                {
                    // A closed shop: a shutter in its own colour in a steel frame, its sign above, CONCIERGE's remodel notice
                    // on every other one, and a banner on the bare wall above (judge A 10).
                    int bay = i + (w.outSign > 0 ? 4 : 0);
                    w.Box(k, m.shutters[(bay * 3 + 1) % m.shutters.Length], c - 2.2f, c + 2.2f, 0f, 3.1f, -0.02f, 0f);
                    WallBanner(k, m, w, c, bannerCells[bay % bannerCells.Length]);
                    w.Box(k, m.steel, c - 2.32f, c - 2.2f, 0f, 3.3f, -0.035f, 0f);
                    w.Box(k, m.steel, c + 2.2f, c + 2.32f, 0f, 3.3f, -0.035f, 0f);
                    w.Box(k, m.dark, c - 2.32f, c + 2.32f, 3.1f, 3.3f, -0.035f, 0f);
                    w.Box(k, m.dark, c - 1.1f, c + 1.1f, 3.4f, 4.35f, -0.03f, 0f);
                    w.Panel(k, m.signs, c, 3.875f, 1.7f, 0.85f, SignUv(SignShop0 + (brand++ % ShopBrands)), -0.036f);
                    if ((i++ & 1) == (w.outSign > 0 ? 1 : 0))
                        w.Panel(k, m.signs, c, 1.75f, 1.6f, 0.8f, SignUv(SignRemodel), -0.026f);
                }
                UpperWall(k, m, w, wz0, wz1, null);
            }
            // South (behind the start): CONCIERGE's welcome screen between two pilasters, lightbox posters either side.
            Trim(k, m, south, wx0, wx1, null);
            foreach (var pu in new[] { -6f, 6f, -13.5f, 13.5f }) Pilaster(k, m, south, pu);
            south.Box(k, m.gold, -2.6f, 2.6f, 2.4f, 5.2f, -0.03f, 0f);
            south.Panel(k, m.signs, 0f, 3.8f, 5f, 2.5f, SignUv(SignWelcome), -0.036f);
            for (int i = 0; i < 2; i++)
            {
                float u = i == 0 ? -10f : 10f;
                south.Box(k, m.dark, u - 1.05f, u + 1.05f, 1.3f, 3.4f, -0.03f, 0f);
                south.Panel(k, m.posters, u, 2.35f, 1.9f, 1.9f, PosterUv(1 + i * 7), -0.036f);
                WallBanner(k, m, south, u, i == 0 ? BannerGalleria : BannerConcierge);
            }
            UpperWall(k, m, south, wx0, wx1, Bays(new float[0], 0f, new Vector2(-2.7f, 2.7f)));
            // North (the doorway's side): wainscot, the GALLERIA sign over the portal, the Galleria's screens either side.
            Trim(k, m, north, wx0, wx1, Bays(new float[0], 0f, new Vector2(-DoorHalfWidth - 0.45f, DoorHalfWidth + 0.45f)));
            north.Box(k, m.gold, -1.95f, 1.95f, 5.55f, 7.45f, -0.03f, 0f);
            north.Panel(k, m.signs, 0f, 6.5f, 3.6f, 1.8f, SignUv(SignGalleria), -0.036f);
            foreach (float u in new[] { -9f, 9f })
            {
                north.Box(k, m.dark, u - 1.75f, u + 1.75f, 2.6f, 4.6f, -0.03f, 0f);
                north.Panel(k, m.signs, u, 3.6f, 3.2f, 1.6f, SignUv(u < 0f ? SignTheGalleria : SignFoodCourt), -0.036f);
                WallBanner(k, m, north, u < 0f ? u - 3.6f : u + 3.6f, u < 0f ? BannerGalleria : BannerFoodCourt);
            }
            UpperWall(k, m, north, wx0, wx1, Bays(new float[0], 0f, new Vector2(-DoorHalfWidth - 0.45f, DoorHalfWidth + 0.45f)));

            // The roof's structure under its glass (above any reach: the kiosk's top is 4.6 m).
            for (float z = wz0 + 4f; z < wz1 - 0.1f; z += 4f)
                k.Box(m.plaster, new Vector3(wx0, WarmupHeight - 0.5f, z - 0.15f), new Vector3(wx1, WarmupHeight, z + 0.15f), 0.025f);
            for (float x = wx0 + 2.5f; x < wx1 - 0.1f; x += 2.5f)
                k.Box(m.steel, new Vector3(x - 0.035f, WarmupHeight - 0.1f, wz0), new Vector3(x + 0.035f, WarmupHeight, wz1));

            // The espresso kiosk (the boost ledge; the landmark): glowing signs on all four sides, a gold cornice at the
            // ledge, goods behind the counter glass, a steel kick.
            var kiosk = warm.Find("Boost Kiosk");
            if (kiosk != null)
            {
                var b = kiosk.GetComponent<Renderer>().bounds;
                Bevelled(k, kiosk, m.marble, 0.03f);
                Collar(k, m.dark, b, 0f, 0.15f, 0.015f, 0.008f);
                Ring(k, m.gold, b, b.max.y - 0.16f, b.max.y, 0.02f);
                Ring(k, m.gold, b, 2.48f, 2.54f, 0.012f);
                foreach (var normal in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    var face = b.center + Vector3.Scale(b.extents, normal);
                    Screen(k, m, new Vector3(face.x, 3.45f, face.z), normal, 2.6f, 1.3f, SignUv(SignEspresso));
                    PanelAt(k, m.products, new Vector3(face.x, 1.65f, face.z) + normal * 0.006f, normal, 2.8f, 1.3f, new Rect(0f, 0.5f, 0.54f, 0.25f));   // bottles and jars
                }
            }
            k.Flush(root, "Warm-up Dressing");
        }

        // ================================================================================================ the level's own pieces

        /// <summary>
        /// Final fix round (judge A round 3, defects 1 and 2): the level's pieces dressed in the look's white, gold and stone.
        /// Their colliders, transforms and positions stay exactly as built; only renderers are swapped for dressing drawn
        /// inside (or at most 2 cm proud of) their volumes.
        /// <list type="bullet">
        /// <item>The practice counter: a gift kiosk (graphite kick, marble base to a gold counter band, white upper body with
        /// a lightbox advert on each face, a gold cornice at the ledge you mantle).</item>
        /// <item>The wall-run lockers: a bank of white lacquered locker doors with gold pulls on a graphite frame, a marble
        /// plinth and a gold cornice (they were a teal crate wrapped in shutter slats).</item>
        /// <item>The service lift: the blank glowing panel on its fascia becomes a graphite sign board with SKY TERRACE lit
        /// on it between gold rules; the blank bar over the doors becomes a small lit SKY TERRACE indicator.</item>
        /// </list>
        /// </summary>
        static void LevelPieces(SkylineKit k, Mats m, Transform root, Transform warm, Transform hall)
        {
            var counter = warm != null ? warm.Find("Practice Counter") : null;
            if (counter != null && counter.GetComponent<MeshRenderer>() != null)
            {
                var b = counter.GetComponent<MeshRenderer>().bounds;
                Bevelled(k, counter, m.plaster, 0.02f);
                Collar(k, m.dark, b, 0f, 0.15f, 0.015f, 0.008f);
                Collar(k, m.marble, b, 0.15f, 1.1f, 0.012f, 0.008f);
                Collar(k, m.gold, b, 1.1f, 1.16f, 0.018f, 0.006f);
                Ring(k, m.dark, b, b.max.y - 0.2f, b.max.y - 0.12f, 0.012f);
                Ring(k, m.gold, b, b.max.y - 0.12f, b.max.y, 0.02f);
                int cell = 4;
                foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    var face = b.center + Vector3.Scale(b.extents, n);
                    ScreenPoster(k, m, new Vector3(face.x, 2.2f, face.z), n, 1.35f, cell);
                    cell += 3;
                }
            }

            var lockers = warm != null ? warm.Find("Wall-run Lockers") : null;
            if (lockers != null && lockers.GetComponent<MeshRenderer>() != null)
            {
                var b = lockers.GetComponent<MeshRenderer>().bounds;
                Bevelled(k, lockers, m.dark, 0.015f);
                Collar(k, m.marble, b, 0f, 0.3f, 0.012f, 0.008f);
                Ring(k, m.gold, b, b.max.y - 0.14f, b.max.y, 0.02f);
                // Doors on all four faces: columns ~0.5 m wide, five rows from the plinth to the cornice, 1 cm proud, with gaps.
                foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    bool alongX = Mathf.Abs(n.z) > 0.5f;
                    float s = alongX ? n.z : n.x;
                    float lo = alongX ? b.min.x : b.min.z, hi = alongX ? b.max.x : b.max.z;
                    float facePos = alongX ? (s > 0 ? b.max.z : b.min.z) : (s > 0 ? b.max.x : b.min.x);
                    int cols = Mathf.Max(1, Mathf.RoundToInt((hi - lo - 0.1f) / 0.5f));
                    float colW = (hi - lo - 0.1f) / cols, y0 = 0.36f, y1 = b.max.y - 0.2f;
                    const int rows = 5;
                    float rowH = (y1 - y0) / rows;
                    for (int c = 0; c < cols; c++)
                        for (int r = 0; r < rows; r++)
                        {
                            float u0 = lo + 0.05f + c * colW + 0.015f, u1 = u0 + colW - 0.03f;
                            float v0 = y0 + r * rowH + 0.015f, v1 = v0 + rowH - 0.03f;
                            float d0 = facePos, d1 = facePos + s * 0.01f;
                            k.Box(m.plaster, alongX ? new Vector3(u0, v0, d0) : new Vector3(d0, v0, u0), alongX ? new Vector3(u1, v1, d1) : new Vector3(d1, v1, u1));
                            // the pull: a short gold bar near the door's free edge, 1 cm proud of the door
                            float pu = c % 2 == 0 ? u1 - 0.06f : u0 + 0.04f, pv = (v0 + v1) * 0.5f, e1 = d1 + s * 0.01f;
                            k.Box(m.gold, alongX ? new Vector3(pu, pv - 0.08f, d1) : new Vector3(d1, pv - 0.08f, pu),
                                alongX ? new Vector3(pu + 0.02f, pv + 0.08f, e1) : new Vector3(e1, pv + 0.08f, pu + 0.02f));
                        }
                }
            }

            var lift = hall != null ? hall.Find("Service Lift") : null;
            if (lift != null)
            {
                var sign = lift.Find("Lift Sign");
                var fascia = lift.Find("Lift Fascia");
                if (sign != null && fascia != null && fascia.GetComponent<Renderer>() != null)
                {
                    StripRenderer(sign);
                    var fb = fascia.GetComponent<Renderer>().bounds;
                    float z = fb.min.z;   // the fascia's open (south) face
                    // A graphite board across the fascia, gold rules top and bottom, SKY TERRACE lit in the middle (2 cm proud).
                    k.Box(m.dark, new Vector3(fb.min.x + 0.08f, fb.min.y + 0.06f, z - 0.012f), new Vector3(fb.max.x - 0.08f, fb.max.y - 0.06f, z));
                    k.Box(m.gold, new Vector3(fb.min.x + 0.08f, fb.max.y - 0.1f, z - 0.016f), new Vector3(fb.max.x - 0.08f, fb.max.y - 0.07f, z - 0.012f));
                    k.Box(m.gold, new Vector3(fb.min.x + 0.08f, fb.min.y + 0.07f, z - 0.016f), new Vector3(fb.max.x - 0.08f, fb.min.y + 0.1f, z - 0.012f));
                    float h = Mathf.Min(0.76f, fb.size.y - 0.26f);
                    PanelAt(k, m.signs, new Vector3(fb.center.x, fb.center.y, z - 0.018f), Vector3.back, h * 2f, h, SignUv(SignLift));
                    // Two glowing uprights either side of the lettering.
                    foreach (float sx in new[] { -1f, 1f })
                        PanelAt(k, m.panel, new Vector3(fb.center.x + sx * (h + 0.35f), fb.center.y, z - 0.018f), Vector3.back, 0.08f, h * 0.8f, new Rect(0f, 0f, 1f, 1f));
                }
                var ind = lift.Find("Lift Indicator");
                if (ind != null && ind.GetComponent<Renderer>() != null)
                {
                    var ib = ind.GetComponent<Renderer>().bounds;
                    StripRenderer(ind);
                    float z = ib.min.z, cx = ib.center.x, cy = ib.center.y + 0.08f;
                    // A gold-framed lit panel over the doors, its face just in front of where the bar was.
                    k.Box(m.gold, new Vector3(cx - 0.55f, cy - 0.3f, z + 0.006f), new Vector3(cx + 0.55f, cy + 0.3f, ib.max.z));
                    k.Box(m.dark, new Vector3(cx - 0.5f, cy - 0.25f, z), new Vector3(cx + 0.5f, cy + 0.25f, ib.max.z));
                    PanelAt(k, m.signs, new Vector3(cx, cy, z - 0.004f), Vector3.back, 0.92f, 0.46f, SignUv(SignLift));
                }
            }
            k.Flush(root, "Level Pieces Dressing");
        }

        /// <summary>A room wall's trim: steel skirting, marble wainscot to 1.1 m, a white cornice with a gold line under the
        /// glass roof (all 1-5 cm proud of the runnable face).</summary>
        static void Trim(SkylineKit k, Mats m, Wall w, float u0, float u1, List<Vector2> holes)
        {
            Fill(k, m.dark, w, u0, u1, 0f, 0.12f, -0.015f, 0f, holes);
            Fill(k, m.marble, w, u0, u1, 0.12f, 1.1f, -Proud, 0f, holes);
            w.Box(k, m.plaster, u0, u1, WarmupHeight - 0.6f, WarmupHeight - 0.1f, -0.05f, 0f, 0.015f);
            w.Box(k, m.gold, u0, u1, WarmupHeight - 0.66f, WarmupHeight - 0.6f, -0.052f, 0f);
        }

        /// <summary>A pilaster in cream marble (it read as nothing in white on white, judge A 10), a gold base band and a gold
        /// capital under the cornice.</summary>
        static void Pilaster(SkylineKit k, Mats m, Wall w, float u)
        {
            w.Box(k, m.marble, u - 0.35f, u + 0.35f, 1.1f, WarmupHeight - 0.66f, -0.04f, 0f, 0.012f);
            w.Box(k, m.gold, u - 0.36f, u + 0.36f, 1.1f, 1.16f, -0.045f, 0f);
            w.Box(k, m.gold, u - 0.37f, u + 0.37f, WarmupHeight - 0.86f, WarmupHeight - 0.66f, -0.046f, 0f);
        }

        /// <summary>The warm-up hall's upper wall (judge A 10: 6-8 m of bare plaster): a graphite frieze with a gold line at
        /// 4.6 m and a glowing cove line under the cornice. All within 5 cm of the runnable face.</summary>
        static void UpperWall(SkylineKit k, Mats m, Wall w, float u0, float u1, List<Vector2> holes)
        {
            Fill(k, m.dark, w, u0, u1, 4.6f, 4.78f, -0.03f, 0f, holes);
            Fill(k, m.gold, w, u0, u1, 4.78f, 4.82f, -0.034f, 0f, holes);
            float y = WarmupHeight - 0.72f;
            w.Panel(k, m.panel, (u0 + u1) * 0.5f, y, Mathf.Abs(u1 - u0) - 0.2f, 0.035f, new Rect(0f, 0f, 1f, 1f), -0.047f);
        }

        /// <summary>A fabric banner (1 x 2 m) on the upper wall at <paramref name="u"/>, on a steel rod, 2 cm proud.</summary>
        static void WallBanner(SkylineKit k, Mats m, Wall w, float u, int cell)
        {
            w.Box(k, m.steel, u - 0.58f, u + 0.58f, 7.0f, 7.04f, -0.035f, 0f);
            w.Panel(k, m.banners, u, 6.0f, 1f, 2f, BannerUv(cell), -0.022f);
        }

        // ================================================================================================ the city outside

        /// <summary>
        /// Halcyon Row beyond the glass (named "(backdrop)": no colliders, no shadows): a paved ground, 34 towers in a ring
        /// 110-520 m out (inside the player camera's 600 m far clip), three sky-bridges and a monorail on piers.
        /// <para>Fix round 1 (judges A 2 / E 6: plain white boxes in the build; A 9: three or four towers over and over): the
        /// facades are URP Unlit with the light and the haze baked (<see cref="SkylineLookLibrary.Facade"/>; each face takes
        /// its sunlit, slanting or shaded version), seven curtain walls instead of four, and five shapes: a slab with a
        /// setback and a crown, a slender tower with a spire on a stone podium, a stepped tower with banded tiers, twin
        /// towers on a podium, and a slab turned off the grid. Far is always paler and bluer than near (three haze bands).</para>
        /// </summary>
        static void City(SkylineKit k, Mats m, Transform root, System.Random rng)
        {
            var info = SkylineLookLibrary.ReadSkyInfo();
            var haze = SkylineLookLibrary.HazeLinear(info);
            var city = Group(root, "Skyline City (backdrop)");
            // The streets and plazas round the Galleria: pale paving, hazed like the towers (seen only from the mezzanine).
            k.Box(SkylineLookLibrary.HazedGround(haze), new Vector3(-600f, -0.35f, -620f), new Vector3(600f, -0.05f, 580f));

            var centre = new Vector2(0f, -14f);
            var placed = new List<Vector4>();   // x, z, half-size, height
            int tries = 0;
            while (placed.Count < 34 && tries++ < 3000)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 110f + Mathf.Pow((float)rng.NextDouble(), 0.8f) * 410f;
                float half = 12f + (float)rng.NextDouble() * 12f;
                var p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
                if (Mathf.Abs(p.y - 160f) < half + 8f) continue;      // the monorail's line
                bool clear = true;
                foreach (var q in placed)
                    if (Mathf.Abs(q.x - p.x) < q.z + half + 14f && Mathf.Abs(q.y - p.y) < q.z + half + 14f) { clear = false; break; }
                if (!clear) continue;
                float height = 50f + (float)rng.NextDouble() * 70f + dist * 0.28f;
                placed.Add(new Vector4(p.x, p.y, half, height));
            }
            var kinds = SkylineLookLibrary.FacadeKinds;
            for (int t = 0; t < placed.Count; t++)
            {
                var q = placed[t];
                int band = SkylineLookLibrary.HazeBand(Vector2.Distance(new Vector2(q.x, q.y), centre));
                string kind = kinds[(t * 3 + (Mathf.Abs((int)(q.x * 0.37f + q.y * 0.71f)) % 2)) % kinds.Length];
                string crownKind = kind == "White" || kind == "Stone" ? "Silver" : "White";
                Tower(k, (t * 2 + t / 5) % 5, q, Skin(kind, band, haze), Skin(crownKind, band, haze), Skin("Stone", band, haze), rng);
            }
            // Sky-bridges between three pairs of near neighbours.
            int bridges = 0;
            for (int i = 0; i < placed.Count && bridges < 3; i++)
                for (int j = i + 1; j < placed.Count && bridges < 3; j++)
                {
                    var a = placed[i]; var b = placed[j];
                    var pa = new Vector2(a.x, a.y); var pb = new Vector2(b.x, b.y);
                    float gap = Vector2.Distance(pa, pb);
                    if (gap > 95f) continue;
                    float y = Mathf.Min(a.w, b.w) * 0.45f;
                    var mid = (pa + pb) * 0.5f;
                    var skin = Skin("White", SkylineLookLibrary.HazeBand(Vector2.Distance(mid, centre)), haze);
                    float yaw = -Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;
                    k.BoxYaw(skin, new Vector3(mid.x, y, mid.y), new Vector3(gap, 6f, 8f), yaw);
                    bridges++;
                }
            // The monorail: a white guideway 22 m up on piers, 160 m north, running east-west across the view.
            var rail = Skin("White", SkylineLookLibrary.HazeBand(190f), haze);
            k.Box(rail, new Vector3(-560f, 21f, 158.5f), new Vector3(560f, 23.2f, 161.5f));
            for (float x = -540f; x <= 540f; x += 60f)
                k.Box(rail, new Vector3(x - 1.2f, -0.05f, 158.8f), new Vector3(x + 1.2f, 21f, 161.2f));
            k.Flush(city, "City", ShadowCastingMode.Off);
        }

        /// <summary>A facade of <paramref name="kind"/> in a haze band, its sunlit, slanting or shaded version by face.</summary>
        static System.Func<Vector3, Material> Skin(string kind, int band, Color haze) =>
            n => SkylineLookLibrary.Facade(kind, band, haze, SkylineLookLibrary.LightFor(n));

        /// <summary>One tower at (q.x, q.y), half-width q.z, height q.w, in one of five shapes (see <see cref="City"/>).</summary>
        static void Tower(SkylineKit k, int shape, Vector4 q, System.Func<Vector3, Material> skin, System.Func<Vector3, Material> crown,
            System.Func<Vector3, Material> podium, System.Random rng)
        {
            float x = q.x, z = q.y, hx = q.z, H = q.w, r = (float)rng.NextDouble();
            float hz = hx * (0.75f + 0.5f * (float)rng.NextDouble());
            Vector3 V(float a, float b, float c) => new Vector3(a, b, c);
            switch (shape)
            {
                case 0:   // a slab with a setback and a crown
                {
                    float sh = H * (0.12f + 0.1f * r);
                    k.Box(skin, V(x - hx, -0.05f, z - hz), V(x + hx, H, z + hz));
                    k.Box(skin, V(x - hx * 0.7f, H, z - hz * 0.7f), V(x + hx * 0.7f, H + sh, z + hz * 0.7f));
                    k.Box(crown, V(x - hx * 0.74f, H + sh, z - hz * 0.74f), V(x + hx * 0.74f, H + sh + 3f, z + hz * 0.74f));
                    break;
                }
                case 1:   // slender, on a stone podium, with a spire
                {
                    float sx = hx * 0.6f, sz = hz * 0.6f, top = H * 1.2f;
                    k.Box(podium, V(x - hx, -0.05f, z - hz), V(x + hx, 16f, z + hz));
                    k.Box(skin, V(x - sx, 16f, z - sz), V(x + sx, top, z + sz));
                    k.Box(crown, V(x - sx * 0.8f, top, z - sz * 0.8f), V(x + sx * 0.8f, top + 5f, z + sz * 0.8f));
                    k.Box(crown, V(x - 0.7f, top + 5f, z - 0.7f), V(x + 0.7f, top + 5f + H * 0.2f, z + 0.7f));
                    break;
                }
                case 2:   // stepped: three tiers, a band at each step
                {
                    float h1 = H * (0.5f + 0.1f * r), h2 = H * 0.82f;
                    k.Box(skin, V(x - hx, -0.05f, z - hz), V(x + hx, h1, z + hz));
                    k.Box(crown, V(x - hx - 0.4f, h1 - 3f, z - hz - 0.4f), V(x + hx + 0.4f, h1, z + hz + 0.4f));
                    k.Box(skin, V(x - hx * 0.78f, h1, z - hz * 0.78f), V(x + hx * 0.78f, h2, z + hz * 0.78f));
                    k.Box(crown, V(x - hx * 0.78f - 0.4f, h2 - 3f, z - hz * 0.78f - 0.4f), V(x + hx * 0.78f + 0.4f, h2, z + hz * 0.78f + 0.4f));
                    k.Box(skin, V(x - hx * 0.55f, h2, z - hz * 0.55f), V(x + hx * 0.55f, H, z + hz * 0.55f));
                    k.Box(crown, V(x - hx * 0.58f, H, z - hz * 0.58f), V(x + hx * 0.58f, H + 2.5f, z + hz * 0.58f));
                    break;
                }
                case 3:   // twin towers on a podium
                {
                    float t = hx * 0.42f;
                    k.Box(podium, V(x - hx, -0.05f, z - hz), V(x + hx, 20f, z + hz));
                    k.Box(skin, V(x - hx, 20f, z - hz * 0.8f), V(x - hx + 2f * t, H, z + hz * 0.8f));
                    k.Box(skin, V(x + hx - 2f * t, 20f, z - hz * 0.8f), V(x + hx, H * (0.75f + 0.1f * r), z + hz * 0.8f));
                    k.Box(crown, V(x - hx + 0.6f, H, z - hz * 0.7f), V(x - hx + 2f * t - 0.6f, H + 3f, z + hz * 0.7f));
                    break;
                }
                default:  // a slab turned off the grid, a plant floor half way up, a crown
                {
                    float yaw = 12f + 30f * r;
                    k.BoxYaw(skin, V(x, H * 0.5f - 0.05f, z), V(hx * 2f, H + 0.1f, hz * 1.3f), yaw);
                    k.BoxYaw(crown, V(x, H * 0.55f, z), V(hx * 2f + 0.6f, 3.5f, hz * 1.3f + 0.6f), yaw);
                    k.BoxYaw(crown, V(x, H + 2f, z), V(hx * 1.7f, 4f, hz * 1.1f), yaw);
                    break;
                }
            }
        }

        // ================================================================================================ the comic-ink test areas
        //
        // docs/SKYLINE-STYLE.md rules 3 and 4, built into the arena for the look test: the lift lobby taken over by one strong
        // coloured light, the warm-up hall colour-free under one row of cool strip lights, and the drone seen close in the sun.
        // All visual: lights, glow strips with no colliders (under a SkylineNoCollider root), still cameras.

        /// <summary>The lift lobby's light, the warm-up hall's strip row and the three look-test stills. Called before the ink
        /// conversion, so the strips' glow materials are converted with the rest.</summary>
        static void InkAreas(Transform arena, Transform warm, Transform hall, Transform stills)
        {
            var root = Group(arena, "Ink Test Areas");
            root.gameObject.AddComponent<SkylineNoCollider>();

            // ---- the lift lobby (rule 3): one violet point light and a matching emissive strip under the deck's soffit. The
            // lobby has no small lights of its own to turn off (its only light was the sign and the doors' glow, both emissive
            // and kept). The light has no shadows, so the lobby's pool is hard-edged and the shutter does not block its spill.
            var l = GalleriaArena.LiftLobby;
            float t = GalleriaArena.LobbyWall, cx = (l.min.X + l.max.X) * 0.5f, cz = (l.min.Z + l.max.Z) * 0.5f;
            var violet = new Color(0x8A / 255f, 0x5C / 255f, 1f);
            var stripGlow = Emissive("SK_Emissive_LobbyViolet", new Color(violet.r * 2.4f, violet.g * 2.4f, violet.b * 2.4f));
            NoShadow(Box(root, "Lobby Strip", V(l.min.X + t + 0.35f, l.max.Y - 0.07f, l.min.Z + 0.9f), V(l.max.X - t - 0.35f, l.max.Y - 0.02f, l.max.Z - 0.9f),
                stripGlow, false, null, true));
            var lobbyLight = new GameObject("Lobby Light").AddComponent<Light>();
            lobbyLight.transform.SetParent(root, false);
            lobbyLight.transform.position = V(cx, l.max.Y - 1.0f, cz);
            lobbyLight.type = LightType.Point;
            lobbyLight.color = violet;
            lobbyLight.intensity = 6f;
            lobbyLight.range = 9f;
            lobbyLight.shadows = LightShadows.None;
            lobbyLight.lightmapBakeType = LightmapBakeType.Realtime;

            // ---- the warm-up hall (rule 4): colour-free (InkGreyWarmup, after the conversion) lit by one row of cool-white
            // strip lights hung 4.2 m up across the hall (the hall's roof is at 8 m: the pools are banded at a fixed
            // attenuation, so the lights hang low enough to reach the floor).
            var cool = new Color(0xDD / 255f, 0xE8 / 255f, 1f);
            var coolGlow = Emissive("SK_Emissive_StripCool", new Color(2.2f, 2.5f, 3.2f));
            for (int i = 0; i < 5; i++)
            {
                float x = -12f + 6f * i;
                NoShadow(Box(root, "Warm-up Strip " + (i + 1), V(x - 1.2f, 4.18f, -28.58f), V(x + 1.2f, 4.28f, -28.42f), coolGlow, false, null, true));
                var light = new GameObject("Warm-up Strip Light " + (i + 1)).AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.position = V(x, 4.0f, -28.5f);
                light.type = LightType.Point;
                light.color = cool;
                light.intensity = 7f;   // fix round 1: raised so the hard-edged pools take the hall over
                light.range = 11f;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Realtime;
            }

            // ---- the stills
            // lift-lobby: from the hall, low, into the lobby (the shutter's renderer is hidden while the shot renders, so the
            // light's pool and the lift doors show whatever the demo's state)
            var lobbyStill = Still(stills, "lift-lobby", V(cx - 0.5f, 1.1f, 9.5f), V(cx, 1.7f, l.max.Z - 1f));
            var shutter = hall != null ? hall.Find("Service Lift/Lift Shutter") : null;
            if (shutter != null && shutter.GetComponent<Renderer>() != null) lobbyStill.hideDuringShot = new[] { shutter.GetComponent<Renderer>() };

            // warmup-grey: from the doorway looking back in, the practice bot in frame
            var pb = U(GalleriaArena.PracticeBot);
            Still(stills, "warmup-grey", V(0.8f, 1.9f, GalleriaArena.HallMinZ - 1.4f), V(pb.x + 1.5f, 1.8f, pb.z));

            // drone-idle: along the east shopfronts in the sun with the skylight in frame, a drone close by, a bit right of
            // centre, face to the camera. The camera and the drone stand where the sun reaches (the south wall shades z < -12
            // at this height); the drone is made only for this still's render (SkylineDisplayBot).
            var cam = V(17.5f, 2.3f, -11f);
            var look = V(23.5f, 6.2f, 14f);
            var droneStill = Still(stills, "drone-idle", cam, look);
            var fwd = (look - cam).normalized;
            var flat = new Vector3(fwd.x, 0f, fwd.z).normalized;
            var right = Vector3.Cross(Vector3.up, flat).normalized;
            var at = cam + flat * 4f + right * 0.85f;
            at.y = 2.35f;
            var display = droneStill.gameObject.AddComponent<SkylineDisplayBot>();
            display.enemyId = SkylineEnemies.DroneId;
            // the larger drone and its much larger rotors are the gameplay bot now (EnemyDef.bodyScale 1.4, weakPointScale 2), so no display scale
            display.position = at;
            display.yaw = Mathf.Atan2(cam.x - at.x, cam.z - at.z) * Mathf.Rad2Deg;
        }

        /// <summary>The warm-up hall colour-free: every ink twin in the hall and its dressing swaps to its <c>__Grey</c>
        /// variant (<c>_Desaturate</c> 1). Glows keep their colour (the boost kiosk's sign, the strips). The floor inlay is one
        /// object shared with the main hall and stays coloured.</summary>
        static void InkGreyWarmup(Transform arena, Transform warm)
        {
            // fix round 1: the sun flooded the hall, so the strip lights could not take over. The roof glass now casts the sun's shadow
            // (an opaque shadow: the hall is dim, lit by its own strips and the cool ambient; the glass itself still shows the sky).
            var roof = warm != null ? warm.Find("Roof (glass)") : null;
            if (roof != null && roof.GetComponent<Renderer>() != null) roof.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            int n = SkylineInkConvert.MakeGrey(warm);
            var dressing = arena.Find("Dressing/Warm-up Dressing");
            if (dressing != null) n += SkylineInkConvert.MakeGrey(dressing);
            Debug.Log("SKYLINE_INK warm-up hall greyed: " + n + " material slots");
        }
    }
}
