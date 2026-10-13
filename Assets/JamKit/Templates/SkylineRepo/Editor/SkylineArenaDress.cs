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
            SignEspresso = 6, SignFoodCourt = 7, SignShop0 = 8, ShopBrands = 12, SignMenu0 = 20;

        static Rect SignUv(int cell) => Cell(cell, 4, 8);
        static Rect PosterUv(int cell) => Cell(cell % 16, 4, 4);

        static Rect Cell(int cell, int cols, int rows)
        {
            int col = cell % cols, row = cell / cols;
            float w = 1f / cols, h = 1f / rows;
            return new Rect(col * w, 1f - (row + 1) * h, w, h);
        }

        sealed class Mats
        {
            public Material terrazzo, marble, plaster, pine, cherry, parquet, shutter, ceiling, concrete, soil, steel, gold, dark, hazard;
            public Material products, signs, posters, panel, entry;
            public Material glassShop, glassCurtain, glassSky, glassCounter, glassRail, glassVitrine;
            public Material[] shopWalls;
            public Material[] shopFloors;
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
                glassShop = SkylineLookLibrary.Glass("Shopfront", new Color(0.62f, 0.72f, 0.76f, 0.16f)),
                glassCurtain = SkylineLookLibrary.Glass("Curtain", new Color(0.42f, 0.56f, 0.66f, 0.26f)),
                glassSky = SkylineLookLibrary.Glass("Skylight", new Color(0.8f, 0.9f, 0.98f, 0.08f)),
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
            int brand = index % ShopBrands;
            // The front.
            w.Box(k, m.steel, a, b, y0, y0 + 0.3f, 0f, 0.12f);
            w.Box(k, m.glassShop, a + 0.06f, b - 0.06f, y0 + 0.3f, y0 + ShopOpen - 0.3f, 0.04f, 0.06f);
            foreach (var u in new[] { a, c - 0.04f, b - 0.08f })
                w.Box(k, m.steel, u, u + 0.08f, y0, y0 + ShopOpen - 0.3f, 0f, 0.12f);
            w.Box(k, m.dark, a, b, y0 + ShopOpen - 0.3f, y0 + ShopOpen, 0f, WallThick);
            // The sign on the fascia (graphite, ShopOpen..BandTop), between two gold strips.
            w.Panel(k, m.signs, c, y0 + (ShopOpen + BandTop) * 0.5f, 1.8f, 0.9f, SignUv(SignShop0 + brand));
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
            // The back wall: a lightbox poster in a graphite frame, a wall shelf under it.
            w.Box(inside, m.dark, c - 0.85f, c + 0.85f, y0 + 1.05f, y0 + 2.75f, D - 0.05f, D);
            PanelAt(inside, m.posters, w.P(c, y0 + 1.9f, D - 0.055f), w.In, 1.5f, 1.5f, PosterUv(index * 5 + 3));
            // Side shelving: two units of goods facing the room.
            foreach (int side in new[] { -1, 1 })
            {
                float face = c + side * (BayHalf - 0.55f);
                float back = c + side * BayHalf;
                w.Box(inside, m.pine, Mathf.Min(face, back), Mathf.Max(face, back), y0, y0 + 2.2f, 0.9f, D - 0.25f);
                var normal = side < 0 ? w.U : -w.U;
                float len = D - 0.25f - 0.9f;
                var center = w.P(face, y0 + 1.15f, 0.9f + len * 0.5f) + normal * 0.006f;
                // Four shelves of goods (half the atlas's eight rows) over 1.9 m; U scaled so the goods keep their shape.
                float half = ((index + (side > 0 ? 1 : 0)) & 1) * 0.5f;
                PanelAt(inside, m.products, center, normal, len, 1.9f, new Rect(0f, half, len / 3.8f, 0.5f));
            }
            // The middle: plants for the florist and the tea shop, a lacquered display table elsewhere.
            if (brand == 8 || brand == 3)
            {
                var plant = SkylineLookLibrary.Prop("Calathea", props, w.P(c - 1f, y0, 1.6f), index * 37f, 0.9f);
                var fern = SkylineLookLibrary.Prop("Fern", props, w.P(c + 0.9f, y0, 2.4f), index * 53f, 0.6f);
                foreach (var go in new[] { plant, fern }) if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            else
            {
                w.Box(inside, m.cherry, c - 0.7f, c + 0.7f, y0, y0 + 0.85f, D * 0.5f - 0.4f, D * 0.5f + 0.4f);
                for (int i = 0; i < 3; i++)
                {
                    float u = c - 0.45f + i * 0.45f;
                    w.Box(inside, m.products, u - 0.15f, u + 0.15f, y0 + 0.85f, y0 + 1.05f + 0.1f * ((index + i) % 3), D * 0.5f - 0.2f, D * 0.5f + 0.2f);
                }
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
                        PanelAt(k, m.signs, new Vector3(x + side * 0.014f, 3.65f, zc), normal, 3.4f, 1.7f, SignUv(SignMenu0 + (stall * 2 + (side > 0 ? 1 : 0)) % 4));
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
                foreach (var c in bays)
                {
                    // A closed shop: a pale shutter in a steel frame, its sign above, CONCIERGE's remodel notice on every other one.
                    w.Box(k, m.shutter, c - 2.2f, c + 2.2f, 0f, 3.1f, -0.02f, 0f);
                    w.Box(k, m.steel, c - 2.32f, c - 2.2f, 0f, 3.3f, -0.035f, 0f);
                    w.Box(k, m.steel, c + 2.2f, c + 2.32f, 0f, 3.3f, -0.035f, 0f);
                    w.Box(k, m.dark, c - 2.32f, c + 2.32f, 3.1f, 3.3f, -0.035f, 0f);
                    w.Box(k, m.dark, c - 1.1f, c + 1.1f, 3.4f, 4.35f, -0.03f, 0f);
                    w.Panel(k, m.signs, c, 3.875f, 1.7f, 0.85f, SignUv(SignShop0 + (brand++ % ShopBrands)), -0.036f);
                    if ((i++ & 1) == (w.outSign > 0 ? 1 : 0))
                        w.Panel(k, m.signs, c, 1.75f, 1.6f, 0.8f, SignUv(SignRemodel), -0.026f);
                }
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
            }
            // North (the doorway's side): wainscot, the GALLERIA sign over the portal, the Galleria's screens either side.
            Trim(k, m, north, wx0, wx1, Bays(new float[0], 0f, new Vector2(-DoorHalfWidth - 0.45f, DoorHalfWidth + 0.45f)));
            north.Box(k, m.gold, -1.95f, 1.95f, 5.55f, 7.45f, -0.03f, 0f);
            north.Panel(k, m.signs, 0f, 6.5f, 3.6f, 1.8f, SignUv(SignGalleria), -0.036f);
            foreach (float u in new[] { -9f, 9f })
            {
                north.Box(k, m.dark, u - 1.75f, u + 1.75f, 2.6f, 4.6f, -0.03f, 0f);
                north.Panel(k, m.signs, u, 3.6f, 3.2f, 1.6f, SignUv(u < 0f ? SignTheGalleria : SignFoodCourt), -0.036f);
            }

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
                    PanelAt(k, m.products, new Vector3(face.x, 1.65f, face.z) + normal * 0.006f, normal, 2.8f, 1.3f, new Rect(0f, 0.25f, 0.54f, 0.25f));
                }
            }
            k.Flush(root, "Warm-up Dressing");
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

        static void Pilaster(SkylineKit k, Mats m, Wall w, float u)
        {
            w.Box(k, m.plaster, u - 0.35f, u + 0.35f, 1.1f, WarmupHeight - 0.66f, -0.04f, 0f, 0.012f);
            w.Box(k, m.gold, u - 0.36f, u + 0.36f, 1.1f, 1.16f, -0.045f, 0f);
        }

        // ================================================================================================ the city outside

        /// <summary>
        /// Halcyon Row beyond the glass (named "(backdrop)": no colliders, no shadows): a paved ground, ~30 glass towers in a
        /// ring 110-520 m out (inside the player camera's 600 m far clip), three sky-bridges and a monorail on piers. Each tower
        /// takes one of four curtain walls in one of four haze bands, so far is always paler and bluer than near, fading into
        /// the sky's own horizon colour (<see cref="SkylineLookLibrary.Facade"/>).
        /// </summary>
        static void City(SkylineKit k, Mats m, Transform root, System.Random rng)
        {
            var info = SkylineLookLibrary.ReadSkyInfo();
            var haze = SkylineLookLibrary.HazeLinear(info);
            float[] bandEdge = { 160f, 260f, 380f, float.MaxValue };
            float[] bandDist = { 130f, 210f, 320f, 460f };
            var city = Group(root, "Skyline City (backdrop)");
            // The streets and plazas round the Galleria: pale paving, hazed like the towers (seen only from the mezzanine).
            k.Box(SkylineLookLibrary.HazedGround(0.3f, haze), new Vector3(-600f, -0.35f, -620f), new Vector3(600f, -0.05f, 580f));

            var centre = new Vector2(0f, -14f);
            var placed = new List<Vector4>();   // x, z, half-size, height
            int tries = 0;
            while (placed.Count < 30 && tries++ < 2000)
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
            foreach (var q in placed)
            {
                float dist = Vector2.Distance(new Vector2(q.x, q.y), centre);
                int band = 0;
                while (dist > bandEdge[band]) band++;
                float f = 1f - Mathf.Exp(-0.0032f * bandDist[band]);
                string kind = SkylineLookLibrary.FacadeKinds[Mathf.Abs((int)(q.x * 7f + q.y * 13f)) % 4];
                var mat = SkylineLookLibrary.Facade(kind, band, f, haze);
                var crown = SkylineLookLibrary.Facade("White", band, f, haze);
                float hx = q.z, hz = q.z * (0.75f + 0.5f * (float)rng.NextDouble());
                k.Box(mat, new Vector3(q.x - hx, -0.05f, q.y - hz), new Vector3(q.x + hx, q.w, q.y + hz));
                // A setback and a crown.
                float sh = q.w * (0.12f + 0.1f * (float)rng.NextDouble());
                k.Box(mat, new Vector3(q.x - hx * 0.7f, q.w, q.y - hz * 0.7f), new Vector3(q.x + hx * 0.7f, q.w + sh, q.y + hz * 0.7f));
                k.Box(crown, new Vector3(q.x - hx * 0.74f, q.w + sh, q.y - hz * 0.74f), new Vector3(q.x + hx * 0.74f, q.w + sh + 3f, q.y + hz * 0.74f));
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
                    float y = Mathf.Min(a.w, b.w) * 0.55f;
                    var mid = (pa + pb) * 0.5f;
                    float dist = Vector2.Distance(mid, centre);
                    int band = 0;
                    while (dist > bandEdge[band]) band++;
                    var mat = SkylineLookLibrary.Facade("White", band, 1f - Mathf.Exp(-0.0032f * bandDist[band]), haze);
                    float yaw = -Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;
                    k.BoxYaw(mat, new Vector3(mid.x, y, mid.y), new Vector3(gap, 6f, 8f), yaw);
                    bridges++;
                }
            // The monorail: a white guideway 22 m up on piers, 160 m north, running east-west across the view.
            var rail = SkylineLookLibrary.Facade("White", 1, 1f - Mathf.Exp(-0.0032f * 190f), haze);
            k.Box(rail, new Vector3(-560f, 21f, 158.5f), new Vector3(560f, 23.2f, 161.5f));
            for (float x = -540f; x <= 540f; x += 60f)
                k.Box(rail, new Vector3(x - 1.2f, -0.05f, 158.8f), new Vector3(x + 1.2f, 21f, 161.2f));
            k.Flush(city, "City", ShadowCastingMode.Off);
        }
    }
}
