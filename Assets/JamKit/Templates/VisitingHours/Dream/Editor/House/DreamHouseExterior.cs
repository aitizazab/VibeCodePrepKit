using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// Night 2's house from outside (round 4, 2026-10-12; the creator: "the outside of the house needs a pass to make it look
    /// less box-like, and adhere to how the inside of the house looks like"). The front garden looks at the house's real
    /// shape: the main floor's plan (DreamHouse) turned so the hall's east end, with the red double door, faces the garden
    /// (x = hall z - <see cref="DreamHouse.FrontDoorZ"/>, z = 8.2 - hall x). So: the living room on the left with its two
    /// tall windows either side of an outside brick chimney, the bay window round the left side under its own steeper roof,
    /// Theo's room on the right with the small window over his desk, the gable over the front with the roof running back,
    /// the dormer over the attic stairs on the right slope, and at the back the observatory: the roof cut and raised into a
    /// taller block with clerestory windows, the round window and a little dome over its skylight (its windows show the
    /// attic's night). A house's details: plinth, clapboard and shingles, trim, fascia and bargeboards, gutters and
    /// downpipes, shutters, a porch on posts. Tall hedges close the garden at the house's corners; a lawn and trees behind.
    /// Decoration only: the front wall, the chimney and the hedges' colliders are DreamHouse pieces.
    /// </summary>
    public static class DreamHouseExterior
    {
        /// <summary>The main floor's height, floor to the attic's floor (the rooms' ceilings are under it).</summary>
        const float Up = DreamHouse.AtticFloor - DreamHouse.Ground;
        const float X0 = DreamHouse.OutX0, X1 = DreamHouse.OutX1, Eave = DreamHouse.EaveY;
        const float RidgeX = (X0 + X1) * 0.5f, RidgeY = Up + DreamHouse.RidgeHeight;
        const float ObsZ = 8.2f - DreamHouse.ObsX1, BackZ = 8.2f - (DreamHouse.HubX0 - 0.2f), ObsTop = Up + DreamHouse.ObsHeight;
        const float Wall = 0.25f, Plinth = 0.45f;

        static Transform _t;
        static Material _siding, _board, _shingle, _shingleLine, _trim, _roof, _stone, _brick, _mortar, _gutter, _night, _hedge;

        public static void Build(Transform root)
        {
            _t = new GameObject("The house (outside)").transform;
            _t.SetParent(root, false);
            var P = DreamEscapeBuilder.Palette;
            _siding = DreamEscapeBuilder.Pastel("HouseFront", P[1], 0.05f);
            _board = DreamEscapeBuilder.Pastel("HouseClapboard", Color.Lerp(P[1], new Color(0.3f, 0.25f, 0.45f), 0.18f), 0f);
            _shingle = DreamEscapeBuilder.Pastel("HouseShingle", new Color(0.95f, 0.74f, 0.64f), 0.04f);
            _shingleLine = DreamEscapeBuilder.Pastel("HouseShingleLine", new Color(0.82f, 0.56f, 0.5f), 0f);
            _trim = DreamEscapeBuilder.Pastel("HouseTrim", P[0], 0.03f);
            _roof = DreamEscapeBuilder.Pastel("HouseRoofSlab", new Color(0.93f, 0.52f, 0.5f), 0.05f);
            _stone = DreamEscapeBuilder.Pastel("HousePlinth", new Color(0.74f, 0.71f, 0.8f), 0.03f);
            _brick = DreamEscapeBuilder.Pastel("HouseBrickOutside", new Color(0.86f, 0.55f, 0.5f), 0.03f);
            _mortar = DreamEscapeBuilder.Pastel("DecorMortar", new Color(0.95f, 0.8f, 0.74f), 0f);
            _gutter = DreamEscapeBuilder.Pastel("HouseGutter", new Color(0.64f, 0.62f, 0.76f), 0.02f);
            _night = DreamEscapeBuilder.Pastel("AtticNight", new Color(0.18f, 0.2f, 0.42f), 0f, 0.8f);
            _hedge = DreamProps.MeshLook("GardenHedge", new Color(0.46f, 0.68f, 0.52f), 0.01f);

            Walls();
            MainRoof();
            Observatory();
            Bay();
            Dormer();
            Chimney();
            Porch();
            Windows();
            Garden();
        }

        // ---------- Helpers ----------

        static GameObject Box(string name, Vector3 c, Vector3 s, Material m) => DreamEscapeBuilder.Box(name, _t, c, s, m);
        static GameObject BoxMinMax(string name, Vector3 mn, Vector3 mx, Material m) => Box(name, (mn + mx) * 0.5f, mx - mn, m);

        static GameObject Prim(string name, PrimitiveType type, Vector3 at, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var go = DreamEscapeBuilder.Part(name, type, _t, Vector3.zero, scale, m);
            go.transform.position = at;
            if (rot.HasValue) go.transform.rotation = rot.Value;
            return go;
        }

        /// <summary>Clapboard (or shingle) lines across a wall face: horizontal strips from y0 to y1.</summary>
        static void Lines(Vector3 a, Vector3 b, float y0, float y1, Vector3 n, float step, Material m)
        {
            Vector3 mid = (a + b) * 0.5f;
            float len = (b - a).magnitude;
            bool alongZ = Mathf.Abs(n.x) > 0.5f;
            for (float y = y0 + step; y < y1 - 0.02f; y += step)
                Box("Board line", new Vector3(mid.x, y, mid.z) + n * 0.012f, alongZ ? new Vector3(0.02f, 0.03f, len) : new Vector3(len, 0.03f, 0.02f), m);
        }

        /// <summary>A roof slab from an eave point to the ridge (in the x-y plane, running from z0 to z1), with an overhang
        /// past the eave, its fascia board and gutter, and a bargeboard at the front.</summary>
        static void Slab(string name, float xEave, float yEave, float xRidge, float yRidge, float z0, float z1, float overhang, bool gutter = true)
        {
            var e = new Vector2(xEave, yEave);
            var r = new Vector2(xRidge, yRidge);
            var u = (r - e).normalized;
            var n = xRidge > xEave ? new Vector2(-u.y, u.x) : new Vector2(u.y, -u.x);
            var start = e - u * overhang;
            float len = (r - start).magnitude, thick = 0.14f;
            var c = (start + r) * 0.5f + n * (thick * 0.5f);
            float ang = Mathf.Atan2(r.y - start.y, r.x - start.x) * Mathf.Rad2Deg;
            var slab = Box(name, new Vector3(c.x, c.y, (z0 + z1) * 0.5f), new Vector3(len, thick, z1 - z0), _roof);
            slab.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            // The bargeboard along the front edge.
            var barge = Box("Bargeboard", new Vector3(c.x, c.y - 0.05f, z0 - 0.03f), new Vector3(len, 0.24f, 0.05f), _trim);
            barge.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            if (!gutter) return;
            // The fascia board and the gutter at the eave.
            var edge = start + n * (thick * 0.5f);
            float outSign = Mathf.Sign(xEave - xRidge);
            Box("Fascia", new Vector3(edge.x + outSign * 0.02f, edge.y - 0.08f, (z0 + z1) * 0.5f), new Vector3(0.05f, 0.22f, z1 - z0), _trim);
            Prim("Gutter", PrimitiveType.Cylinder, new Vector3(edge.x + outSign * 0.1f, edge.y - 0.16f, (z0 + z1) * 0.5f), new Vector3(0.14f, (z1 - z0) * 0.5f, 0.14f), _gutter, Quaternion.Euler(90f, 0f, 0f));
        }

        static void Downpipe(float x, float z, float top)
        {
            Prim("Downpipe", PrimitiveType.Cylinder, new Vector3(x, top * 0.5f, z), new Vector3(0.09f, top * 0.5f, 0.09f), _gutter);
            Prim("Downpipe shoe", PrimitiveType.Cylinder, new Vector3(x, 0.12f, z), new Vector3(0.12f, 0.08f, 0.12f), _gutter);
        }

        static void NightWindow(Vector3 c, Vector3 n, float w, float h)
        {
            DreamEscapeBuilder.Quad("Window (the attic's night)", _t, c + n * 0.004f, n, w, h, _night);
            DreamHouseRooms.Window(_t, c, n, w, h, pane: false);
            var star = DreamEscapeBuilder.Glow("AtticStar", new Color(1f, 0.96f, 0.82f), 1.4f, 0f, 0.2f);
            Vector3 across = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
            var rnd = new System.Random((int)(c.x * 13 + c.z * 7));
            for (int i = 0; i < 6; i++)
                Prim("Star", PrimitiveType.Sphere, c + n * 0.02f + across * (((float)rnd.NextDouble() - 0.5f) * w * 0.8f) + Vector3.up * (((float)rnd.NextDouble() - 0.5f) * h * 0.8f), Vector3.one * 0.05f, star);
        }

        static void Shutters(Vector3 c, Vector3 n, float w, float h)
        {
            var paint = DreamEscapeBuilder.Pastel("HouseShutter", new Color(0.36f, 0.42f, 0.7f), 0.03f);
            Vector3 across = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
            foreach (float s in new[] { -1f, 1f })
            {
                var at = c + n * 0.04f + across * s * (w * 0.5f + 0.22f);
                var size = Mathf.Abs(n.x) > 0.5f ? new Vector3(0.04f, h + 0.1f, 0.3f) : new Vector3(0.3f, h + 0.1f, 0.04f);
                Box("Shutter", at, size, paint);
                for (float y = -h * 0.4f; y <= h * 0.4f; y += 0.16f)
                    Box("Shutter slat", at + n * 0.025f + Vector3.up * y, Mathf.Abs(n.x) > 0.5f ? new Vector3(0.01f, 0.03f, 0.24f) : new Vector3(0.24f, 0.03f, 0.01f), _trim);
            }
        }

        // ---------- The walls ----------

        static void Walls()
        {
            // The sides and the back (the front wall is DreamHouse's, with its colliders); the observatory's block rises
            // full height at the back.
            BoxMinMax("Wall (left)", new Vector3(X0, 0f, 0.3f), new Vector3(X0 + Wall, Eave, ObsZ), _siding);
            BoxMinMax("Wall (right)", new Vector3(X1 - Wall, 0f, 0.3f), new Vector3(X1, Eave, ObsZ), _siding);
            Lines(new Vector3(X0, 0f, 0f), new Vector3(X0, 0f, ObsZ), Plinth, Eave, Vector3.left, 0.3f, _board);
            Lines(new Vector3(X1, 0f, 0f), new Vector3(X1, 0f, ObsZ), Plinth, Eave, Vector3.right, 0.3f, _board);
            Lines(new Vector3(X0, 0f, 0f), new Vector3(-1.6f, 0f, 0f), Plinth, Eave, Vector3.back, 0.3f, _board);
            Lines(new Vector3(1.6f, 0f, 0f), new Vector3(X1, 0f, 0f), Plinth, Eave, Vector3.back, 0.3f, _board);
            // The plinth: a stone band round the foot of the walls.
            BoxMinMax("Plinth (front, left)", new Vector3(X0 - 0.06f, 0f, -0.06f), new Vector3(-1.66f, Plinth, 0.1f), _stone);
            BoxMinMax("Plinth (front, right)", new Vector3(1.66f, 0f, -0.06f), new Vector3(X1 + 0.06f, Plinth, 0.1f), _stone);
            BoxMinMax("Plinth (left)", new Vector3(X0 - 0.06f, 0f, 0f), new Vector3(X0 + 0.1f, Plinth, BackZ + 0.06f), _stone);
            BoxMinMax("Plinth (right)", new Vector3(X1 - 0.1f, 0f, 0f), new Vector3(X1 + 0.06f, Plinth, BackZ + 0.06f), _stone);
            BoxMinMax("Plinth (back)", new Vector3(X0 - 0.06f, 0f, BackZ - 0.1f), new Vector3(X1 + 0.06f, Plinth, BackZ + 0.06f), _stone);
            // Corner boards.
            foreach (float x in new[] { X0 - 0.03f, X1 + 0.03f })
                Box("Corner board", new Vector3(x, (Plinth + Eave) * 0.5f, 0.02f), new Vector3(0.12f, Eave - Plinth, 0.12f), _trim);
            // The front gable, in shingles, with a round vent.
            DreamHouseRooms.Poly(_t, "Front gable", _shingle, new Vector3(X0, Eave, -0.005f), new Vector3(RidgeX, RidgeY, -0.005f), new Vector3(X1, Eave, -0.005f));
            for (float y = Eave + 0.22f; y < RidgeY - 0.2f; y += 0.22f)
            {
                float half = (RidgeY - y) / (RidgeY - Eave) * (X1 - RidgeX);
                float halfL = (RidgeY - y) / (RidgeY - Eave) * (RidgeX - X0);
                Box("Shingle line", new Vector3(RidgeX + (half - halfL) * 0.5f, y, -0.015f), new Vector3(half + halfL - 0.1f, 0.025f, 0.02f), _shingleLine);
            }
            Prim("Gable vent", PrimitiveType.Cylinder, new Vector3(RidgeX, Eave + 1.45f, -0.03f), new Vector3(0.55f, 0.02f, 0.55f), _trim, Quaternion.Euler(90f, 0f, 0f));
            for (int i = -1; i <= 1; i++)
                Box("Vent louvre", new Vector3(RidgeX, Eave + 1.45f + i * 0.12f, -0.05f), new Vector3(0.4f, 0.03f, 0.02f), _board);
        }

        // ---------- The roofs ----------

        static void MainRoof()
        {
            float z0 = -0.45f, z1 = ObsZ;
            Slab("Roof (left slope)", X0, Eave, RidgeX, RidgeY, z0, z1, 0.45f);
            Slab("Roof (right slope)", X1, Eave, RidgeX, RidgeY, z0, z1, 0.45f);
            Box("Ridge cap", new Vector3(RidgeX, RidgeY + 0.12f, (z0 + z1) * 0.5f), new Vector3(0.3f, 0.12f, z1 - z0), _trim);
            foreach (float x in new[] { X0 - 0.5f, X1 + 0.5f })
                foreach (float z in new[] { 0.35f, ObsZ - 0.35f })
                    Downpipe(x, z, Eave - 0.3f);
        }

        static void Observatory()
        {
            // Its walls, in shingles: the sides and the back rise to its flat roof; over the main roof, its front wall.
            BoxMinMax("Observatory wall (left)", new Vector3(X0, 0f, ObsZ), new Vector3(X0 + Wall, ObsTop, BackZ), _siding);
            BoxMinMax("Observatory wall (right)", new Vector3(X1 - Wall, 0f, ObsZ), new Vector3(X1, ObsTop, BackZ), _siding);
            BoxMinMax("Back wall", new Vector3(X0, 0f, BackZ - Wall), new Vector3(X1, ObsTop, BackZ), _siding);
            foreach (var (a, b, n) in new[] {
                (new Vector3(X0, 0f, ObsZ), new Vector3(X0, 0f, BackZ), Vector3.left),
                (new Vector3(X1, 0f, ObsZ), new Vector3(X1, 0f, BackZ), Vector3.right),
                (new Vector3(X0, 0f, BackZ), new Vector3(X1, 0f, BackZ), Vector3.forward) })
            {
                Lines(a, b, Plinth, Eave, n, 0.3f, _board);
                // Above the eaves, a band of shingles.
                var mid = (a + b) * 0.5f;
                float len = (b - a).magnitude;
                Box("Shingle band", new Vector3(mid.x, (Eave + ObsTop) * 0.5f, mid.z) + n * 0.01f, Mathf.Abs(n.x) > 0.5f ? new Vector3(0.02f, ObsTop - Eave, len) : new Vector3(len, ObsTop - Eave, 0.02f), _shingle);
                Lines(a + n * 0.01f, b + n * 0.01f, Eave, ObsTop, n, 0.22f, _shingleLine);
                Box("Band (trim)", new Vector3(mid.x, Eave, mid.z) + n * 0.03f, Mathf.Abs(n.x) > 0.5f ? new Vector3(0.06f, 0.12f, len) : new Vector3(len, 0.12f, 0.06f), _trim);
            }
            DreamHouseRooms.Poly(_t, "Observatory front (over the roof, left)", _shingle, new Vector3(X0, Eave, ObsZ - 0.005f), new Vector3(RidgeX, RidgeY, ObsZ - 0.005f), new Vector3(RidgeX, ObsTop, ObsZ - 0.005f), new Vector3(X0, ObsTop, ObsZ - 0.005f));
            DreamHouseRooms.Poly(_t, "Observatory front (over the roof, right)", _shingle, new Vector3(RidgeX, RidgeY, ObsZ - 0.005f), new Vector3(X1, Eave, ObsZ - 0.005f), new Vector3(X1, ObsTop, ObsZ - 0.005f), new Vector3(RidgeX, ObsTop, ObsZ - 0.005f));
            // Its flat roof with a cornice, and the little dome over the skylight (a slit for the telescope).
            BoxMinMax("Observatory roof", new Vector3(X0 - 0.3f, ObsTop, ObsZ - 0.3f), new Vector3(X1 + 0.3f, ObsTop + 0.2f, BackZ + 0.3f), _roof);
            BoxMinMax("Observatory cornice", new Vector3(X0 - 0.36f, ObsTop - 0.12f, ObsZ - 0.36f), new Vector3(X1 + 0.36f, ObsTop + 0.02f, BackZ + 0.36f), _trim);
            float sx = (DreamHouse.SkyZ0 + DreamHouse.SkyZ1) * 0.5f - DreamHouse.FrontDoorZ, sz = 8.2f - (DreamHouse.SkyX0 + DreamHouse.SkyX1) * 0.5f;
            var dome = DreamEscapeBuilder.Pastel("ObservatoryDome", new Color(0.84f, 0.86f, 0.95f), 0.04f);
            Prim("Dome drum", PrimitiveType.Cylinder, new Vector3(sx, ObsTop + 0.35f, sz), new Vector3(2.3f, 0.15f, 2.3f), _trim);
            Prim("Dome", PrimitiveType.Sphere, new Vector3(sx, ObsTop + 0.5f, sz), new Vector3(2.1f, 2.1f, 2.1f), dome);
            var slit = Box("Dome slit", new Vector3(sx, ObsTop + 1.2f, sz - 0.45f), new Vector3(0.32f, 0.9f, 0.5f), _night);
            slit.transform.rotation = Quaternion.Euler(-35f, 0f, 0f);
            Prim("Dome finial", PrimitiveType.Sphere, new Vector3(sx, ObsTop + 1.58f, sz + 0.1f), Vector3.one * 0.14f, DreamEscapeBuilder.Pastel("DecorBrassKnob", new Color(0.95f, 0.78f, 0.38f), 0.01f));
            // Its windows: the round one at the back, clerestories in the sides (the attic's night in them).
            float rx = RidgeX;
            var ring = Prim("Round window (frame)", PrimitiveType.Cylinder, new Vector3(rx, Up + 1.85f, BackZ + 0.02f), new Vector3(2.4f, 0.02f, 2.4f), _trim, Quaternion.Euler(90f, 0f, 0f));
            Prim("Round window (night)", PrimitiveType.Cylinder, new Vector3(rx, Up + 1.85f, BackZ + 0.04f), new Vector3(2.1f, 0.02f, 2.1f), _night, Quaternion.Euler(90f, 0f, 0f));
            foreach (float hx in new[] { -6.0f, -4.2f })
            {
                float z = 8.2f - hx;
                NightWindow(new Vector3(X0 - 0.01f, Up + 3.0f, z), Vector3.left, 1.1f, 0.82f);
                NightWindow(new Vector3(X1 + 0.01f, Up + 3.0f, z), Vector3.right, 1.1f, 0.82f);
            }
            foreach (float x in new[] { X0 - 0.2f, X1 + 0.2f })
                Downpipe(x, BackZ - 0.3f, ObsTop - 0.1f);
        }

        static void Bay()
        {
            // The living room's bay, out of the left side: one storey, its own steeper roof (40 degrees).
            float bx0 = DreamHouse.HubZ0 - 0.2f - (DreamHouse.BayZ0 - 0.2f) , bz0 = 8.2f - (DreamHouse.BayX1 + 0.2f), bz1 = 8.2f - (DreamHouse.BayX0 - 0.2f);
            float outer = X0 - bx0, top = 3.3f;
            BoxMinMax("Bay (walls)", new Vector3(outer, 0f, bz0), new Vector3(X0 + 0.05f, top, bz1), _siding);
            BoxMinMax("Bay plinth", new Vector3(outer - 0.06f, 0f, bz0 - 0.06f), new Vector3(X0, Plinth, bz1 + 0.06f), _stone);
            Lines(new Vector3(outer, 0f, bz0), new Vector3(outer, 0f, bz1), Plinth, top, Vector3.left, 0.3f, _board);
            float rise = (X0 - outer + 0.15f) * Mathf.Tan(40f * Mathf.Deg2Rad);
            Slab("Bay roof", outer, top, X0, top + rise, bz0 - 0.15f, bz1 + 0.15f, 0.15f);
            foreach (float z in new[] { bz0 - 0.15f, bz1 + 0.15f })
                DreamHouseRooms.Poly(_t, "Bay roof (hip end)", _roof, new Vector3(outer - 0.15f, top, z), new Vector3(X0, top, z), new Vector3(X0, top + rise, z));
            Box("Bay band", new Vector3(outer - 0.03f, top - 0.06f, (bz0 + bz1) * 0.5f), new Vector3(0.06f, 0.12f, bz1 - bz0), _trim);
        }

        static void Dormer()
        {
            // Over the attic stairs, on the right slope: its front flush with the wall, its cheeks down to the roof, a
            // little gable roof of its own.
            float dz0 = 8.2f - DreamHouse.DormerX1 - 0.2f, dz1 = 8.2f - DreamHouse.DormerX0 + 0.2f, top = Up + DreamHouse.DormerHeight;
            float meet = RidgeX + (RidgeY - top) / (RidgeY - Eave) * (X1 - RidgeX);   // where its top meets the main roof
            BoxMinMax("Dormer front", new Vector3(X1 - Wall, Eave - 0.1f, dz0), new Vector3(X1, top, dz1), _siding);
            foreach (float z in new[] { dz0, dz1 })
                DreamHouseRooms.Poly(_t, "Dormer cheek", _siding, new Vector3(meet, top, z), new Vector3(X1, top, z), new Vector3(X1, Eave, z));
            float zm = (dz0 + dz1) * 0.5f, half = (dz1 - dz0) * 0.5f, peak = top + half * Mathf.Tan(30f * Mathf.Deg2Rad);
            DreamHouseRooms.Poly(_t, "Dormer gable", _shingle, new Vector3(X1 + 0.005f, top, dz0), new Vector3(X1 + 0.005f, peak, zm), new Vector3(X1 + 0.005f, top, dz1));
            float back = RidgeX + (RidgeY - peak) / (RidgeY - Eave) * (X1 - RidgeX) - 0.2f;
            float len = X1 + 0.3f - back, slope = Mathf.Sqrt(half * half + (peak - top) * (peak - top)) + 0.2f;
            foreach (float s in new[] { -1f, 1f })
            {
                var slab = Box("Dormer roof", new Vector3((back + X1 + 0.3f) * 0.5f, (top + peak) * 0.5f + 0.06f, zm + s * half * 0.5f), new Vector3(len, 0.12f, slope), _roof);
                slab.transform.rotation = Quaternion.Euler(s * 30f, 0f, 0f);
            }
            DreamHouseRooms.Window(_t, new Vector3(X1 + 0.01f, Up + 1.4f, zm), Vector3.right, 1.2f, 0.8f, pane: false);
            DreamEscapeBuilder.Quad("Dormer window (the attic's night)", _t, new Vector3(X1 + 0.012f, Up + 1.4f, zm), Vector3.right, 1.2f, 0.8f, _night);
        }

        static void Chimney()
        {
            // The living room's chimney, outside on the front, its stack up past the roof, two pots.
            BoxMinMax("Chimney (base)", new Vector3(-5.1f, 0f, -0.6f), new Vector3(-3.3f, 2f, 0f), _brick);
            var shoulder = Box("Chimney (shoulder)", new Vector3(-4.2f, 2.1f, -0.3f), new Vector3(1.6f, 0.3f, 0.55f), _brick);
            BoxMinMax("Chimney (stack)", new Vector3(-4.8f, 2f, -0.55f), new Vector3(-3.6f, 7.0f, 0f), _brick);
            Box("Chimney (cap)", new Vector3(-4.2f, 7.05f, -0.28f), new Vector3(1.4f, 0.12f, 0.75f), _trim);
            foreach (float x in new[] { -4.5f, -3.9f })
                Prim("Chimney pot", PrimitiveType.Cylinder, new Vector3(x, 7.3f, -0.28f), new Vector3(0.24f, 0.18f, 0.24f), DreamEscapeBuilder.Pastel("ChimneyPot", new Color(0.82f, 0.48f, 0.4f), 0.03f));
            for (float y = 0.3f; y < 6.9f; y += 0.3f)
            {
                bool wide = y < 2f;
                Box("Mortar", new Vector3(-4.2f, y, wide ? -0.612f : -0.562f), new Vector3(wide ? 1.8f : 1.2f, 0.02f, 0.02f), _mortar);
            }
        }

        static void Porch()
        {
            // The porch over the red double door (the hall's end): its roof on two posts, a deck, a lantern.
            var gableMesh = DreamEscapeBuilder.EnsureGableMesh();
            var roofMat = DreamEscapeBuilder.Pastel("HouseFrontRoof", new Color(0.93f, 0.52f, 0.5f), 0.05f, 0f, 1f);
            DreamEscapeBuilder.GableOn(_t, gableMesh, new Vector3(0f, 3.3f, -0.95f), new Vector3(4.4f, 0.9f, 1.9f), roofMat).name = "Porch roof";
            Box("Porch beam", new Vector3(0f, 2.8f, -1.75f), new Vector3(4.3f, 0.18f, 0.16f), _trim);
            foreach (float x in new[] { -2.05f, 2.05f })
            {
                Box("Porch post", new Vector3(x, 1.4f, -1.75f), new Vector3(0.16f, 2.8f, 0.16f), _trim);
                Box("Porch post base", new Vector3(x, 0.15f, -1.75f), new Vector3(0.26f, 0.3f, 0.26f), _stone);
            }
            Box("Porch deck", new Vector3(0f, 0.02f, -0.95f), new Vector3(4.4f, 0.04f, 1.9f), DreamEscapeBuilder.Pastel("PorchDeck", new Color(0.8f, 0.64f, 0.52f), 0.02f));
            var lamp = DreamEscapeBuilder.Glow("DecorLampGlow", new Color(1f, 0.86f, 0.6f), 0.55f, 0f, 0.1f);
            Prim("Porch lantern", PrimitiveType.Cube, new Vector3(1.85f, 2.3f, -0.08f), new Vector3(0.18f, 0.26f, 0.12f), _trim);
            Prim("Porch lantern (glow)", PrimitiveType.Sphere, new Vector3(1.85f, 2.3f, -0.18f), Vector3.one * 0.18f, lamp);
        }

        /// <summary>A point on the plan (hall x, hall z) seen from outside: x = z - the front door's z (the right side 0.8 m
        /// further out, DreamHouse.OutX1), z = 8.2 - x.</summary>
        static Vector3 Out(float hx, float y, float hz) =>
            new Vector3(hz - DreamHouse.FrontDoorZ + (hz > DreamHouse.HubZ1 + 0.1f ? 0.8f : 0f), y, 8.2f - hx);

        static void Windows()
        {
            // The rooms' windows (DreamHouseRooms.HouseWindows), on the outer faces of the same walls (0.2 m out); shutters on
            // the front's and the side bedrooms' windows.
            foreach (var (c, n, w, h) in DreamHouseRooms.HouseWindows)
            {
                var outer = new Vector3(c.x, c.y, c.z) - n * 0.2f;
                var p = Out(outer.x, outer.y, outer.z);
                var normal = new Vector3(-n.z, 0f, n.x);   // the plan's outward normal (-n), turned
                p += normal * 0.01f;
                DreamHouseRooms.Window(_t, p, normal, w, h);
                if (w >= 0.7f && h >= 1.2f && Mathf.Abs(n.z) < 0.5f || (n.z < -0.5f && w >= 1.4f)) Shutters(p, normal, w, h);
            }
        }

        // ---------- The garden ----------

        static void Garden()
        {
            // Tall hedges across the side gardens, level with the observatory (their colliders are DreamHouse pieces), and
            // taller ones behind the house (out of reach) closing the view.
            foreach (var (a, b) in new[] { (-14f, X0), (X1, 14f) })
            {
                var mesh = DreamProps.RoundedBox("Hedge", new Vector3(b - a, 2.6f, 0.8f), 0.3f);
                DreamProps.Show("Hedge", mesh, _hedge, _t, Vector3.zero, Quaternion.identity).transform.position = new Vector3((a + b) * 0.5f, 1.3f, DreamHouse.SideGardenZ - 0.4f);
            }
            foreach (var (a, b) in new[] { (-16f, -5.6f), (5.6f, 16f) })
            {
                var mesh = DreamProps.RoundedBox("Hedge (back)", new Vector3(b - a, 4.6f, 1.0f), 0.4f);
                DreamProps.Show("Hedge (back)", mesh, _hedge, _t, Vector3.zero, Quaternion.identity).transform.position = new Vector3((a + b) * 0.5f, 2.3f, BackZ + 1.3f);
            }
            // A lawn on round the house's sides and back (outside the stretched hallway's walls), and trees.
            var lawn = DreamEscapeBuilder.Pastel("HouseLawn", new Color(0.7f, 0.86f, 0.66f));
            DreamEscapeBuilder.Quad("Lawn (beyond, left)", _t, new Vector3(-17.75f, -0.01f, 30f), Vector3.up, 24.5f, 60f, lawn);
            DreamEscapeBuilder.Quad("Lawn (beyond, right)", _t, new Vector3(17.75f, -0.01f, 30f), Vector3.up, 24.5f, 60f, lawn);
            var trunk = DreamEscapeBuilder.Pastel("Trunk", new Color(0.7f, 0.54f, 0.5f));
            var leaves = DreamProps.MeshLook("GardenTree", new Color(0.55f, 0.76f, 0.56f), 0.01f);
            var sphere = DreamProps.RoundedBox("Canopy", new Vector3(1f, 1f, 1f), 0.5f);
            foreach (var (x, z, s) in new[] { (-12.5f, 13.5f, 1.2f), (-10f, 19.5f, 1.5f), (11.5f, 13.5f, 1.1f), (9.5f, 19.5f, 1.5f), (-8f, 24f, 1.6f), (7.5f, 24f, 1.6f) })
            {
                Prim("Tree trunk", PrimitiveType.Cylinder, new Vector3(x, 2.2f * s, z), new Vector3(0.45f * s, 2.2f * s, 0.45f * s), trunk);
                foreach (var (ox, oy, oz, r) in new[] { (0f, 5.2f, 0f, 3.4f), (-1.1f, 4.5f, 0.6f, 2.4f), (1.0f, 4.6f, -0.5f, 2.5f) })
                {
                    var c = DreamProps.Show("Tree canopy", sphere, leaves, _t, Vector3.zero, Quaternion.identity).transform;
                    c.position = new Vector3(x + ox * s, oy * s, z + oz * s);
                    c.localScale = Vector3.one * r * s;
                }
            }
        }
    }
}
