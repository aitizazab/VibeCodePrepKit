using System.Collections.Generic;
using JamKit.DeepBlock;
using UnityEngine;
using static JamKit.DreamEscape.EditorTools.HillKit;
using Random = System.Random;
using Side = JamKit.DreamEscape.EditorTools.HillMesh.Side;

namespace JamKit.DreamEscape.EditorTools
{
    /// <summary>
    /// The hill's houses, drawn from <see cref="HillHouse"/> on the colliders <see cref="HillColliders.House"/> makes from
    /// the same numbers: the walls' outer faces on the footprint, the shingles' top surface on the roof slopes' sloped
    /// tops (the roof's thickness hangs below), the porch deck's top at the floor, the porch roof's top at
    /// <see cref="HillColliders.PorchRoofHeight"/> + <see cref="HillColliders.PorchRoofThick"/>, the steps' nosings on
    /// the steps' slope, the chimney and the turret inside their boxes. Trim (casings, sills, boards, gutters,
    /// downpipes, lamps) stands at most 0.12 m off a collider face. Dormers stand on the front slope above it (drawn only).
    /// <para>Everything else varies by <see cref="HillHouse.seed"/>: window sizes and layouts, sash or casement bars,
    /// casing style, eaves (boxed or with rafter tails), fish-scale gables, quoins, the door's style and glass, transom
    /// and sidelights, porch posts, lattice skirts, the house number, flower boxes, curtains, ivy, which rooms are lit.</para>
    /// </summary>
    public static class HillHouses
    {
        /// <summary>Draws the house in world coordinates into <paramref name="m"/>.</summary>
        public static void Draw(HillMesh m, HillHouse h)
        {
            if (h == null || h.width < 2f || h.depth < 2f) return;
            var b = new Builder(m, h);
            m.Push(Frame(U(h.center), U(h.Right), Vector3.up, U(h.Back)));
            try { b.Draw(); }
            finally { m.Pop(); }
        }

        enum Edge { Inner, Eave, Rake }

        sealed class RoofPlane
        {
            public Vector3[] p;
            public Edge[] e;
            public RoofPlane(Vector3[] p, Edge[] e) { this.p = p; this.e = e; }
        }

        sealed class WallDef
        {
            public int index;
            public Vector3 a, along, n;
            public float len, topA, topB;
            public bool peaked;
            public List<Vector2> outline;
            public readonly List<Rect> holes = new List<Rect>();
            public readonly List<Rect> busy = new List<Rect>();
            public readonly List<Win> wins = new List<Win>();
            public readonly List<DoorSpec> doors = new List<DoorSpec>();
            public Vector3 At(float s) => a + along * s;
        }

        sealed class Builder
        {
            readonly HillMesh m;
            readonly HillHouse h;
            readonly Random r;
            readonly HillRoof roof;
            readonly float hw, hd, o, eave, ridge, low, t, X, Z, G, sh, sill, winW, winH, bay, revealDepth;
            readonly int nFull, muntin, casing, doorStyle;
            readonly bool half, siding, brick, stucco, headCap, craftsman, fishScale, quoins, flowerBoxes, transom, sidelights;
            readonly string wallMat, trim, roofMat, frameMat, reveal, shutterMat, boxMat, chimneyMat, dormerWall, stepMat, bandMat;
            readonly float pw, pd, tx, tz;
            readonly int postStyle;
            readonly List<WallDef> walls = new List<WallDef>();

            public Builder(HillMesh m, HillHouse h)
            {
                this.m = m;
                this.h = h;
                r = new Random(h.seed * 7919 + 1013);
                roof = h.roof;
                hw = h.width * 0.5f;
                hd = h.depth * 0.5f;
                o = Mathf.Max(0f, h.overhang);
                eave = h.EaveHeight;
                ridge = h.RidgeHeight;
                float tanP = Mathf.Tan(h.pitch * Mathf.Deg2Rad);
                float drop = roof == HillRoof.Gambrel ? o * 1.6f : tanP * o;
                low = eave - drop;
                X = hw + o;
                Z = hd + o;
                G = roof == HillRoof.GableFront ? (ridge - low) / X : roof == HillRoof.Shed ? (ridge - low) / (2f * Z) : roof == HillRoof.Flat ? 0f : (ridge - low) / Z;
                t = 0.17f;
                sh = h.storeyHeight;
                nFull = Mathf.Max(1, Mathf.FloorToInt(h.storeys + 0.001f));
                half = h.storeys - nFull > 0.01f;

                string w = h.wall ?? HillMat.SidingWhite;
                brick = w.StartsWith("brick");
                stucco = w == HillMat.Stucco || w == HillMat.StuccoWarm || w == HillMat.Plaster || w == HillMat.ConcretePanel;
                siding = !brick && !stucco;
                wallMat = w;
                trim = string.IsNullOrEmpty(h.trim) ? HillMat.TrimWhite : h.trim;
                roofMat = string.IsNullOrEmpty(h.roofMat) ? HillMat.RoofGrey : h.roofMat;
                frameMat = brick && Chance(r, 0.3f) ? HillMat.TrimCream : trim;
                reveal = siding ? trim : wallMat;
                casing = brick ? 1 : stucco ? 2 : 0;
                var muntinChoices = brick ? new[] { 1, 2, 4, 2 } : stucco ? new[] { 3, 4, 1, 5 } : new[] { 1, 2, 2, 3, 5 };
                muntin = Pick(r, muntinChoices);
                headCap = Chance(r, 0.6f);
                craftsman = Chance(r, 0.35f) && o >= 0.3f;
                fishScale = siding && Chance(r, 0.4f);
                quoins = (brick || stucco) && Chance(r, 0.35f);
                flowerBoxes = Chance(r, 0.35f);
                sill = R(r, 0.82f, 0.95f);
                winW = R(r, 0.82f, 1.15f);
                winH = Mathf.Min(R(r, 1.32f, 1.6f), sh - sill - 0.45f);
                bay = R(r, 2.3f, 3.1f);
                revealDepth = siding ? R(r, 0.1f, 0.12f) : R(r, 0.12f, 0.15f);
                doorStyle = r.Next(4);
                transom = Chance(r, 0.4f);
                sidelights = Chance(r, 0.25f);
                shutterMat = Pick(r, new[] { HillMat.TrimDark, HillMat.DoorGreen, HillMat.DoorBlue, h.door ?? HillMat.DoorRed, HillMat.TrimDark });
                boxMat = Chance(r, 0.5f) ? trim : HillMat.FenceWood;
                chimneyMat = brick ? wallMat : Pick(r, new[] { HillMat.BrickRed, HillMat.BrickDark, HillMat.BrickRed, HillMat.Plinth });
                dormerWall = siding ? wallMat : brick ? HillMat.ShingleWall : wallMat;
                stepMat = h.porch && Chance(r, 0.5f) ? HillMat.Deck : Pick(r, new[] { HillMat.Concrete, HillMat.StoneCoping, HillMat.BrickRed });
                bandMat = siding ? trim : brick ? HillMat.StoneCoping : HillMat.Plaster;
                postStyle = r.Next(3);
                pw = (h.porchWidth > 0f ? h.porchWidth : h.width * 0.6f) * 0.5f;
                pd = h.porchDepth;
                tx = h.turret * (hw - 1.2f);
                tz = -hd + 0.3f;
            }

            // ---------- The roof's surface (the colliders' sloped tops) ----------

            float RoofTop(float x, float z)
            {
                switch (roof)
                {
                    case HillRoof.Flat: return eave;
                    case HillRoof.GableFront: return low + G * (X - Mathf.Abs(x));
                    case HillRoof.Shed: return low + G * (z + Z);
                    case HillRoof.Hip: return Mathf.Min(low + G * (Z - Mathf.Abs(z)), low + G * (X - Mathf.Abs(x)));
                    default: return low + G * (Z - Mathf.Abs(z));
                }
            }

            float WallTop(Vector3 p) => roof == HillRoof.Flat ? eave + 0.53f : RoofTop(p.x, p.z) - t * 0.55f;

            public void Draw()
            {
                AddWall(new Vector3(-hw, 0f, -hd), Vector3.right, Vector3.back, h.width);
                AddWall(new Vector3(hw, 0f, -hd), Vector3.forward, Vector3.right, h.depth);
                AddWall(new Vector3(hw, 0f, hd), Vector3.left, Vector3.forward, h.width);
                AddWall(new Vector3(-hw, 0f, hd), Vector3.back, Vector3.left, h.depth);
                Exclusions();
                FrontDoor();
                LayoutWindows();
                foreach (var w in walls) DrawWall(w);
                DrawRoof();
                if (h.chimney && roof != HillRoof.Flat) Chimney();
                if (h.turret != 0) Turret();
                Dormers();
                if (h.porch) Porch();
                else if (h.plinth > 0.15f) Steps(-hd, false);
            }

            void AddWall(Vector3 a, Vector3 along, Vector3 n, float len)
            {
                var w = new WallDef { index = walls.Count, a = a, along = along, n = n, len = len };
                w.topA = WallTop(a);
                w.topB = WallTop(a + along * len);
                float tM = WallTop(a + along * len * 0.5f);
                w.outline = new List<Vector2> { new Vector2(0f, 0f), new Vector2(len, 0f), new Vector2(len, w.topB) };
                if (tM > (w.topA + w.topB) * 0.5f + 0.02f) { w.outline.Add(new Vector2(len * 0.5f, tM)); w.peaked = true; }
                w.outline.Add(new Vector2(0f, w.topA));
                walls.Add(w);
            }

            /// <summary>Places on the walls that windows must keep clear of: the turret, the porch roof.</summary>
            void Exclusions()
            {
                var front = walls[0];
                if (h.turret != 0)
                {
                    front.busy.Add(new Rect(tx - 1.6f + hw, -1f, 3.2f, 99f));
                    if (h.turret > 0) walls[1].busy.Add(new Rect(-1f, -1f, 2.9f, 99f));
                    else walls[3].busy.Add(new Rect(walls[3].len - 1.9f, -1f, 2.9f, 99f));
                }
                if (h.porch && h.porchRoof)
                    front.busy.Add(new Rect(-pw - 0.25f + hw, HillColliders.PorchRoofHeight - 0.12f, pw * 2f + 0.5f, 0.42f));
            }

            // ---------- Openings ----------

            void FrontDoor()
            {
                var w = walls[0];
                float limit = h.porch ? Mathf.Min(pw - 0.8f, 0.45f) : 0.4f;
                float dx = Mathf.Clamp(R(r, -0.5f, 0.5f), -Mathf.Max(0f, limit), Mathf.Max(0f, limit));
                float dw = R(r, 0.9f, 1.0f);
                var d = new DoorSpec
                {
                    x0 = hw + dx - dw * 0.5f, x1 = hw + dx + dw * 0.5f, height = R(r, 2.05f, 2.15f), depth = revealDepth + 0.03f,
                    style = doorStyle, transom = transom, sidelights = sidelights && (!h.porch || pw > 1.6f), casing = casing == 1 ? 1 : casing == 2 ? 2 : 0,
                    wall = wallMat, reveal = reveal, trim = trim, leaf = string.IsNullOrEmpty(h.door) ? HillMat.DoorRed : h.door,
                    hallUV = HillAtlas.Interior(Chance(r, h.litShare + 0.3f) ? 3 : 7),
                };
                if (h.porch && h.porchRoof && d.Hole.yMax + 0.25f > HillColliders.PorchRoofHeight - 0.12f) d.transom = false;
                var hole = d.Hole;
                var fp = new Rect(hole.xMin - 0.18f, -0.1f, hole.width + 0.36f, hole.height + 0.35f);
                w.doors.Add(d);
                w.holes.Add(hole);
                w.busy.Add(fp);
                // The porch lamp beside it and the house number on the other side.
                w.busy.Add(new Rect(fp.xMin - 0.3f, 1.5f, 0.3f, 0.5f));
                w.busy.Add(new Rect(fp.xMax, 1.4f, 0.4f, 0.3f));
            }

            Win MakeWin(float cx, float y0, float ww, float wh, int storey)
            {
                var win = new Win
                {
                    x0 = cx - ww * 0.5f, x1 = cx + ww * 0.5f, y0 = y0, y1 = y0 + wh, depth = revealDepth,
                    muntins = muntin, casing = casing, wall = wallMat, reveal = reveal, trim = trim, frame = frameMat,
                    headCap = headCap, shutters = h.shutters, shutter = shutterMat, box = boxMat,
                };
                bool lit = Chance(r, h.litShare);
                int[] rooms = storey == 0 ? new[] { 0, 1, 4, 5, 0 } : storey >= nFull ? new[] { 6, 2, 5 } : new[] { 2, 6, 5, 2 };
                win.roomUV = HillAtlas.Interior(lit ? Pick(r, rooms) : 7);
                win.roomFlip = Chance(r, 0.5f);
                win.curtains = Chance(r, lit ? 0.35f : 0.6f);
                win.curtain = Chance(r, 0.6f) ? HillMat.Canvas : HillMat.TrimCream;
                win.roomDepth = R(r, 0.7f, 1.2f);
                return win;
            }

            bool TryPlace(WallDef w, Win win)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var fp = win.Footprint;
                    bool ok = fp.xMin >= 0.18f && fp.xMax <= w.len - 0.18f && Fits(w.outline, fp, 0.04f);
                    if (ok)
                        foreach (var b in w.busy)
                            if (fp.xMin < b.xMax + 0.05f && b.xMin < fp.xMax + 0.05f && fp.yMin < b.yMax + 0.05f && b.yMin < fp.yMax + 0.05f) { ok = false; break; }
                    if (ok)
                    {
                        w.busy.Add(fp);
                        w.holes.Add(win.Hole);
                        w.wins.Add(win);
                        return true;
                    }
                    if (!win.shutters && !win.flowerBox) return false;
                    win.shutters = false;
                    win.flowerBox = false;
                }
                return false;
            }

            List<float> Bays(float len, float footW)
            {
                float usable = len - 0.6f;
                float spacing = Mathf.Max(bay, footW + 0.4f);
                int n = Mathf.Max(1, Mathf.FloorToInt(usable / spacing));
                var c = new List<float>();
                for (int i = 0; i < n; i++) c.Add(0.3f + usable * (i + 0.5f) / n);
                return c;
            }

            void LayoutWindows()
            {
                float shutterW = h.shutters ? winW + 0.1f : 0f;
                float footW = winW + 0.22f + shutterW;
                // The recessed bay: a wide three-light window on the ground floor, front if there's room.
                bool bayDone = !h.bay;
                foreach (var w in walls)
                {
                    bool front = w.index == 0, back = w.index == 2;
                    var bays = Bays(w.len, footW);
                    for (int k = 0; k < nFull; k++)
                    {
                        float y0 = k * sh + (k == 0 ? sill : sill - 0.04f);
                        float wh = k == 0 ? winH : Mathf.Min(winH, sh - sill - 0.45f) - 0.05f;
                        foreach (float c in bays)
                        {
                            if (!front && !back && Chance(r, 0.22f)) continue;   // blank stretches on the sides
                            if (!bayDone && k == 0 && (front || !h.porch))
                            {
                                var bw = MakeWin(c, y0 - 0.05f, Mathf.Min(2.3f, bay - 0.25f), wh + 0.1f, 0);
                                bw.muntins = 6;
                                bw.depth = 0.3f;
                                bw.shutters = false;
                                bw.headCap = true;
                                if (TryPlace(w, bw)) { bayDone = true; continue; }
                            }
                            float ww = winW;
                            float yy = y0, hh = wh;
                            int style = muntin;
                            if (k > 0 && !front && Chance(r, 0.15f)) { ww = 0.6f; yy = k * sh + 1.3f; hh = 0.75f; style = 0; }   // a bathroom
                            var win = MakeWin(c, yy, ww, hh, k);
                            win.muntins = style;
                            win.flowerBox = flowerBoxes && front && (k == 0 || Chance(r, 0.3f));
                            TryPlace(w, win);
                        }
                    }
                    // Over the front door: a window on each upper storey.
                    if (front)
                        for (int k = 1; k < nFull; k++)
                        {
                            var d = w.doors.Count > 0 ? w.doors[0] : null;
                            if (d == null) break;
                            float c = (d.x0 + d.x1) * 0.5f;
                            var win = MakeWin(c, k * sh + sill + 0.1f, Mathf.Min(winW, 0.8f), Mathf.Min(winH, 1.2f), k);
                            win.shutters = false;
                            TryPlace(w, win);
                        }
                    // The half storey and the attic: windows in the gables.
                    if (w.peaked || roof == HillRoof.Shed)
                    {
                        float y0 = half ? nFull * sh + 0.5f : eave + 0.3f;
                        float ww = half ? Mathf.Min(winW, 0.9f) : 0.62f, wh = half ? 1.05f : 0.7f;
                        int storey = half ? nFull : nFull + 1;
                        var centres = w.len > 8.5f && half ? new[] { w.len * 0.5f - 0.85f, w.len * 0.5f + 0.85f } : new[] { w.len * 0.5f };
                        foreach (float c in centres)
                        {
                            var win = MakeWin(c, y0, ww, wh, storey);
                            win.shutters = false;
                            if (!half) win.muntins = Chance(r, 0.5f) ? 5 : 0;
                            if (!TryPlace(w, win))
                            {
                                win = MakeWin(c, y0 - 0.1f, ww * 0.8f, wh * 0.8f, storey);
                                win.shutters = false;
                                TryPlace(w, win);
                            }
                        }
                    }
                }
            }

            // ---------- Walls ----------

            void DrawWall(WallDef w)
            {
                m.Push(WallFrame(w.At(0f), w.along, w.n));
                const Side back = Side.ZNeg;
                float split = half ? nFull * sh : Mathf.Min(w.topA, w.topB);
                if (fishScale && w.peaked && split < Mathf.Max(w.topA, w.topB) + 0.5f)
                {
                    var lower = ClipY(w.outline, split, true);
                    var upper = ClipY(w.outline, split, false);
                    var hl = new List<Rect>();
                    var hu = new List<Rect>();
                    foreach (var hole in w.holes) (hole.yMax <= split + 1e-3f ? hl : hu).Add(hole);
                    WallFace(m, wallMat, lower, hl);
                    WallFace(m, HillMat.ShingleWall, upper, hu);
                    Band(w, split, 0.1f);
                }
                else WallFace(m, wallMat, w.outline, w.holes);
                foreach (var win in w.wins) Window(m, win, r);
                foreach (var d in w.doors) Door(m, d, r);
                // Bands between the storeys.
                for (int k = 1; k < nFull; k++) Band(w, k * sh, siding ? 0.08f : 0.06f);
                if (half) Band(w, nFull * sh, 0.07f);
                // Corner boards on sidings; quoins on brick and render.
                if (siding)
                {
                    m.Box(trim, new Vector3(-0.028f, 0f, 0f), new Vector3(0.13f, w.topA - 0.02f, 0.028f), 0f, back);
                    m.Box(trim, new Vector3(w.len - 0.13f, 0f, 0f), new Vector3(w.len, w.topB - 0.02f, 0.028f), 0f, back);
                }
                else if (quoins)
                {
                    string q = brick ? HillMat.StoneCoping : HillMat.Plinth;
                    float top = Mathf.Min(w.topA, w.topB) - 0.3f;
                    int i = 0;
                    for (float y = 0.05f; y + 0.27f < top; y += 0.32f, i++)
                    {
                        float l = i % 2 == 0 ? 0.36f : 0.22f;
                        m.Box(q, new Vector3(-0.016f, y, 0f), new Vector3(l, y + 0.27f, 0.016f), 0.006f, back);
                        float l2 = i % 2 == 0 ? 0.22f : 0.36f;
                        m.Box(q, new Vector3(w.len - l2, y, 0f), new Vector3(w.len, y + 0.27f, 0.016f), 0.006f, back);
                    }
                }
                // The frieze under the eaves.
                if (!w.peaked && roof != HillRoof.Flat && !brick && Mathf.Abs(w.topA - w.topB) < 0.02f)
                {
                    float y1 = w.topA - t * 0.45f + 0.005f;
                    m.Box(trim, new Vector3(siding ? 0.13f : 0f, y1 - 0.22f, 0f), new Vector3(w.len - (siding ? 0.13f : 0f), y1, 0.022f), 0f, back);
                }
                // A flat roof's cornice.
                if (roof == HillRoof.Flat)
                    m.Box(brick ? HillMat.StoneCoping : trim, new Vector3(-0.06f, eave + 0.28f, 0f), new Vector3(w.len, eave + 0.4f, 0.06f), 0.012f, back);
                // The plinth, from the floor down to below the lowest ground, and its drip.
                float pb = -h.plinth - 0.3f;
                m.Box(HillMat.Plinth, new Vector3(-0.04f, pb, 0f), new Vector3(w.len, 0f, 0.04f), 0f, back | Side.YPos);
                foreach (var seg in DripSegments(w))
                    m.Box(HillMat.StoneCoping, new Vector3(seg.x, -0.035f, 0f), new Vector3(seg.y, 0.012f, 0.065f), 0.01f, back);
                if (w.index == 0) FrontDetails(w);
                if (w.index == 1 || w.index == 3) IvyHere(w);
                m.Pop();
            }

            List<Vector2> DripSegments(WallDef w)
            {
                var segs = new List<Vector2> { new Vector2(-0.065f, w.len) };
                if (w.index != 0) return segs;
                float cut0 = float.MaxValue, cut1 = float.MinValue;
                if (h.porch) { cut0 = hw - pw - 0.03f; cut1 = hw + pw + 0.03f; }
                else if (h.plinth > 0.15f) { cut0 = hw - 0.92f; cut1 = hw + 0.92f; }
                if (cut0 > cut1) return segs;
                segs.Clear();
                if (cut0 > 0f) segs.Add(new Vector2(-0.065f, cut0));
                if (cut1 < w.len) segs.Add(new Vector2(cut1, w.len));
                return segs;
            }

            bool IvyHere(WallDef w)
            {
                var ir = new Random(h.seed * 31 + w.index);
                if (!Chance(ir, 0.22f)) return false;
                float cx = Chance(ir, 0.5f) ? R(ir, 0.9f, 1.6f) : w.len - R(ir, 0.9f, 1.6f);
                float ih = Mathf.Min(R(ir, 2f, 4f), Mathf.Min(w.topA, w.topB) - 0.2f);
                Ivy(m, ir, cx, ih * 0.5f + 0.05f, R(ir, 1.2f, 2.2f), ih);
                return true;
            }

            void Band(WallDef w, float y, float halfH)
            {
                var s = Span(w.outline, y + halfH);
                float x0 = Mathf.Max(0f, s.x), x1 = Mathf.Min(w.len, s.y);
                if (x1 - x0 < 0.2f) return;
                m.Box(bandMat, new Vector3(x0 - (siding ? 0.028f : 0f), y - halfH, 0f), new Vector3(x1, y + halfH, siding ? 0.04f : 0.022f), 0.006f, Side.ZNeg);
            }

            static List<Vector2> ClipY(IList<Vector2> poly, float y, bool below)
            {
                var res = new List<Vector2>();
                for (int i = 0; i < poly.Count; i++)
                {
                    var a = poly[i];
                    var b = poly[(i + 1) % poly.Count];
                    bool ina = below ? a.y <= y : a.y >= y;
                    bool inb = below ? b.y <= y : b.y >= y;
                    if (ina) res.Add(a);
                    if (ina != inb) res.Add(Vector2.Lerp(a, b, (y - a.y) / (b.y - a.y)));
                }
                return res;
            }

            /// <summary>The porch lamp and the house number beside the front door.</summary>
            void FrontDetails(WallDef w)
            {
                if (w.doors.Count == 0) return;
                var d = w.doors[0];
                var hole = d.Hole;
                float casingW = d.casing == 0 ? 0.11f : d.casing == 2 ? 0.12f : 0.02f;
                float lx = hole.xMin - casingW - 0.2f;
                m.Box(HillMat.MetalDark, new Vector3(lx - 0.06f, 1.62f, 0f), new Vector3(lx + 0.06f, 1.95f, 0.02f), 0.006f, Side.ZNeg);
                m.Box(HillMat.LampGlow, new Vector3(lx - 0.055f, 1.67f, 0.03f), new Vector3(lx + 0.055f, 1.86f, 0.11f));
                m.Box(HillMat.MetalDark, new Vector3(lx - 0.075f, 1.86f, 0.02f), new Vector3(lx + 0.075f, 1.9f, 0.12f), 0.01f);
                m.Box(HillMat.MetalDark, new Vector3(lx - 0.065f, 1.63f, 0.02f), new Vector3(lx + 0.065f, 1.67f, 0.115f), 0.008f);
                m.Box(HillMat.MetalDark, new Vector3(lx - 0.015f, 1.75f, 0.02f), new Vector3(lx + 0.015f, 1.8f, 0.03f));
                string num = (1 + ((h.seed % 97) + 97) % 97 * 2 + (h.seed % 2 == 0 ? 1 : 0)).ToString();
                float nx = hole.xMax + casingW + 0.08f;
                if (siding) m.Box(trim, new Vector3(nx - 0.04f, 1.42f, 0f), new Vector3(nx + num.Length * 0.085f + 0.04f, 1.62f, 0.02f), 0.005f, Side.ZNeg);
                Digits(m, siding ? HillMat.MetalDark : HillMat.Steel, num, new Vector3(nx, 1.46f, siding ? 0.02f : 0f), 0.12f);
            }

            // ---------- Roof ----------

            void DrawRoof()
            {
                if (roof == HillRoof.Flat) { FlatRoof(); return; }
                var planes = new List<RoofPlane>();
                var up = Vector3.up;
                switch (roof)
                {
                    case HillRoof.GableFront:
                        planes.Add(new RoofPlane(new[] { new Vector3(-X, low, Z), new Vector3(-X, low, -Z), new Vector3(0f, ridge, -Z), new Vector3(0f, ridge, Z) }, new[] { Edge.Eave, Edge.Rake, Edge.Inner, Edge.Rake }));
                        planes.Add(new RoofPlane(new[] { new Vector3(X, low, -Z), new Vector3(X, low, Z), new Vector3(0f, ridge, Z), new Vector3(0f, ridge, -Z) }, new[] { Edge.Eave, Edge.Rake, Edge.Inner, Edge.Rake }));
                        break;
                    case HillRoof.Shed:
                        planes.Add(new RoofPlane(new[] { new Vector3(-X, low, -Z), new Vector3(X, low, -Z), new Vector3(X, ridge, Z), new Vector3(-X, ridge, Z) }, new[] { Edge.Eave, Edge.Rake, Edge.Rake, Edge.Rake }));
                        break;
                    case HillRoof.Hip:
                    {
                        float a = Mathf.Max(X - Z, 0f), b = Mathf.Max(Z - X, 0f), peak = low + G * Mathf.Min(X, Z);
                        planes.Add(Hip4(new Vector3(-X, low, -Z), new Vector3(X, low, -Z), new Vector3(a, peak, -b), new Vector3(-a, peak, -b)));
                        planes.Add(Hip4(new Vector3(X, low, Z), new Vector3(-X, low, Z), new Vector3(-a, peak, b), new Vector3(a, peak, b)));
                        planes.Add(Hip4(new Vector3(X, low, -Z), new Vector3(X, low, Z), new Vector3(a, peak, b), new Vector3(a, peak, -b)));
                        planes.Add(Hip4(new Vector3(-X, low, Z), new Vector3(-X, low, -Z), new Vector3(-a, peak, -b), new Vector3(-a, peak, b)));
                        break;
                    }
                    default:
                        planes.Add(new RoofPlane(new[] { new Vector3(-X, low, -Z), new Vector3(X, low, -Z), new Vector3(X, ridge, 0f), new Vector3(-X, ridge, 0f) }, new[] { Edge.Eave, Edge.Rake, Edge.Inner, Edge.Rake }));
                        planes.Add(new RoofPlane(new[] { new Vector3(X, low, Z), new Vector3(-X, low, Z), new Vector3(-X, ridge, 0f), new Vector3(X, ridge, 0f) }, new[] { Edge.Eave, Edge.Rake, Edge.Inner, Edge.Rake }));
                        break;
                }
                foreach (var p in planes) Slab(p, roofMat, trim, trim, t);
                // Ridge and hip caps.
                switch (roof)
                {
                    case HillRoof.GableFront:
                        m.Beam(roofMat, new Vector3(0f, ridge + 0.012f, -Z - 0.02f), new Vector3(0f, ridge + 0.012f, Z + 0.02f), 0.26f, 0.07f, 0.01f);
                        break;
                    case HillRoof.Shed:
                        m.Beam(HillMat.Gutter, new Vector3(-X - 0.02f, ridge + 0.01f, Z - 0.05f), new Vector3(X + 0.02f, ridge + 0.01f, Z - 0.05f), 0.14f, 0.04f);
                        break;
                    case HillRoof.Hip:
                    {
                        float a = Mathf.Max(X - Z, 0f), b = Mathf.Max(Z - X, 0f), peak = low + G * Mathf.Min(X, Z);
                        if (a > 0.05f) m.Beam(roofMat, new Vector3(-a, peak + 0.012f, 0f), new Vector3(a, peak + 0.012f, 0f), 0.26f, 0.07f, 0.01f);
                        if (b > 0.05f) m.Beam(roofMat, new Vector3(0f, peak + 0.012f, -b), new Vector3(0f, peak + 0.012f, b), 0.26f, 0.07f, 0.01f);
                        foreach (int sx in new[] { -1, 1 })
                            foreach (int sz in new[] { -1, 1 })
                                m.Beam(roofMat, new Vector3(sx * X, low + 0.01f, sz * Z), new Vector3(sx * a, peak + 0.012f, sz * b), 0.2f, 0.06f, 0.008f);
                        break;
                    }
                    default:
                        m.Beam(roofMat, new Vector3(-X - 0.02f, ridge + 0.012f, 0f), new Vector3(X + 0.02f, ridge + 0.012f, 0f), 0.26f, 0.07f, 0.01f);
                        if (roof == HillRoof.Gambrel)
                            foreach (int sz in new[] { -1, 1 })
                            {
                                float zk = sz * Z * 0.52f;
                                float yk = RoofTop(0f, zk) + 0.02f;
                                m.Beam(trim, new Vector3(-X, yk, zk), new Vector3(X, yk, zk), 0.12f, 0.045f, 0.008f);
                            }
                        break;
                }
                // Eaves: fascia boards, gutters, downpipes, rafter tails; rakes: bargeboards.
                bool pipeSide = Chance(r, 0.5f);
                foreach (var p in planes)
                {
                    var c = Centroid(p.p);
                    for (int i = 0; i < p.p.Length; i++)
                    {
                        if (p.e[i] == Edge.Inner) continue;
                        var a = p.p[i];
                        var b = p.p[(i + 1) % p.p.Length];
                        var mid = (a + b) * 0.5f;
                        var n = new Vector3(mid.x - c.x, 0f, mid.z - c.z);
                        var dirH = b - a;
                        dirH.y = 0f;
                        if (dirH.sqrMagnitude > 1e-6f) n = Vector3.Cross(Vector3.up, dirH).normalized * Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, dirH), n));
                        n.Normalize();
                        if (p.e[i] == Edge.Rake)
                        {
                            float bh = t + 0.1f;
                            var off = n * 0.022f - up * (bh * 0.5f - 0.02f);
                            m.Beam(trim, a + off, b + off, 0.04f, bh, 0.006f);
                            continue;
                        }
                        // The fascia board over the slab's edge.
                        var foff = n * 0.012f - up * (t * 0.5f + 0.03f);
                        m.Beam(trim, a + foff, b + foff, 0.025f, t + 0.06f, 0.004f);
                        if (craftsman) RafterTails(a, b, n);
                        if (!h.gutters) continue;
                        var g0 = a + n * 0.06f - up * 0.07f;
                        var g1 = b + n * 0.06f - up * 0.07f;
                        m.Cylinder(HillMat.Gutter, g0, g1, 0.055f, 6, true);
                        Downpipe(a, b, n, pipeSide);
                        pipeSide = !pipeSide;
                    }
                }
            }

            RoofPlane Hip4(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                if ((c - d).sqrMagnitude < 1e-4f) return new RoofPlane(new[] { a, b, c }, new[] { Edge.Eave, Edge.Inner, Edge.Inner });
                return new RoofPlane(new[] { a, b, c, d }, new[] { Edge.Eave, Edge.Inner, Edge.Inner, Edge.Inner });
            }

            static Vector3 Centroid(Vector3[] p)
            {
                var c = Vector3.zero;
                foreach (var v in p) c += v;
                return c / p.Length;
            }

            /// <summary>A roof plane as a slab: its top on the collider's slope, its underside <paramref name="th"/> below
            /// (the soffit), its outer edges faced in trim (fascia, bargeboards).</summary>
            void Slab(RoofPlane p, string top, string under, string edge, float th)
            {
                int n = p.p.Length;
                var bot = new Vector3[n];
                for (int i = 0; i < n; i++) bot[i] = p.p[i] - Vector3.up * th;
                m.Poly(top, p.p, Vector3.up);
                m.Poly(under, bot, Vector3.down);
                var c = Centroid(p.p);
                for (int i = 0; i < n; i++)
                {
                    if (p.e[i] == Edge.Inner) continue;
                    int j = (i + 1) % n;
                    var mid = (p.p[i] + p.p[j]) * 0.5f;
                    var outward = new Vector3(mid.x - c.x, 0f, mid.z - c.z);
                    m.Quad(edge, p.p[i], p.p[j], bot[j], bot[i], outward);
                }
            }

            void RafterTails(Vector3 a, Vector3 b, Vector3 n)
            {
                var dir = (b - a);
                dir.y = 0f;
                float len = dir.magnitude;
                if (len < 1f) return;
                dir /= len;
                float y = a.y;   // eave edges are level
                float rise = G * o;
                for (float s = o + 0.15f; s < len - o - 0.1f; s += 0.6f)
                {
                    var atEdge = a + dir * s;
                    var p0 = new Vector3(atEdge.x, y + rise - t - 0.05f, atEdge.z) - n * o;
                    var p1 = new Vector3(atEdge.x, y - t - 0.05f + G * 0.03f, atEdge.z) - n * 0.03f;
                    m.Beam(trim, p0, p1, 0.05f, 0.1f);
                }
            }

            void Downpipe(Vector3 a, Vector3 b, Vector3 n, bool preferEnd)
            {
                var dir = b - a;
                dir.y = 0f;
                float len = dir.magnitude;
                if (len < 1f) return;
                dir /= len;
                // The house's corners lie one overhang in from the eave's ends.
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    bool atEnd = attempt == 0 ? preferEnd : !preferEnd;
                    float s = atEnd ? len - o - 0.14f : o + 0.14f;
                    var e = a + dir * s;
                    var wallPt = e - n * o + n * 0.07f;
                    if (!PipeClear(wallPt)) continue;
                    float y = a.y;
                    var gutter = e + n * 0.06f;
                    float bottom = -h.plinth - 0.22f;
                    var path = new[]
                    {
                        new Vector3(gutter.x, y - 0.1f, gutter.z),
                        new Vector3(gutter.x, y - 0.17f, gutter.z),
                        new Vector3(wallPt.x, y - 0.2f, wallPt.z),
                        new Vector3(wallPt.x, y - 0.3f, wallPt.z),
                        new Vector3(wallPt.x, bottom + 0.15f, wallPt.z),
                        new Vector3(wallPt.x, bottom, wallPt.z) + n * 0.03f,
                    };
                    m.Tube(HillMat.Gutter, path, new[] { 0.04f }, 6, false, 1f);
                    // Brackets holding it to the wall.
                    for (float by = 1.2f; by < y - 0.6f; by += 2.2f)
                        m.Box(HillMat.Gutter, new Vector3(wallPt.x - 0.05f, by, wallPt.z - 0.05f) - n * 0.02f, new Vector3(wallPt.x + 0.05f, by + 0.04f, wallPt.z + 0.05f) - n * 0.02f);
                    return;
                }
            }

            /// <summary>Whether a downpipe at this point (next to a wall) clears the porch, the steps and the turret.</summary>
            bool PipeClear(Vector3 p)
            {
                if (h.turret != 0 && p.x > tx - 1.6f && p.x < tx + 1.6f && p.z < -hd + 1.9f) return false;
                if (h.porch && p.z < -hd + 0.2f && Mathf.Abs(p.x) < pw + 0.35f) return false;
                if (!h.porch && h.plinth > 0.15f && p.z < -hd + 0.2f && Mathf.Abs(p.x) < 1.1f) return false;
                return true;
            }

            void FlatRoof()
            {
                float i = 0.25f, top = eave + 0.6f, cop = 0.07f;
                m.Quad(HillMat.RoofFlat, new Vector3(-hw + i, eave, -hd + i), new Vector3(hw - i, eave, -hd + i), new Vector3(hw - i, eave, hd - i), new Vector3(-hw + i, eave, hd - i), Vector3.up);
                // The parapet's inner faces and its coping (its top at the collider's top, 4 cm over the outer face).
                m.Quad(wallMat, new Vector3(-hw + i, eave, -hd + i), new Vector3(hw - i, eave, -hd + i), new Vector3(hw - i, top - cop, -hd + i), new Vector3(-hw + i, top - cop, -hd + i), Vector3.forward);
                m.Quad(wallMat, new Vector3(-hw + i, eave, hd - i), new Vector3(hw - i, eave, hd - i), new Vector3(hw - i, top - cop, hd - i), new Vector3(-hw + i, top - cop, hd - i), Vector3.back);
                m.Quad(wallMat, new Vector3(-hw + i, eave, -hd + i), new Vector3(-hw + i, eave, hd - i), new Vector3(-hw + i, top - cop, hd - i), new Vector3(-hw + i, top - cop, -hd + i), Vector3.right);
                m.Quad(wallMat, new Vector3(hw - i, eave, -hd + i), new Vector3(hw - i, eave, hd - i), new Vector3(hw - i, top - cop, hd - i), new Vector3(hw - i, top - cop, -hd + i), Vector3.left);
                string cm = HillMat.StoneCoping;
                m.Box(cm, new Vector3(-hw - 0.04f, top - cop, -hd - 0.04f), new Vector3(hw + 0.04f, top, -hd + i + 0.03f), 0.012f);
                m.Box(cm, new Vector3(-hw - 0.04f, top - cop, hd - i - 0.03f), new Vector3(hw + 0.04f, top, hd + 0.04f), 0.012f);
                m.Box(cm, new Vector3(-hw - 0.04f, top - cop, -hd + i + 0.03f), new Vector3(-hw + i + 0.03f, top, hd - i - 0.03f), 0.012f);
                m.Box(cm, new Vector3(hw - i - 0.03f, top - cop, -hd + i + 0.03f), new Vector3(hw + 0.04f, top, hd - i - 0.03f), 0.012f);
                // A rooflight flush with the roof and the drain outlets' downpipes.
                float lx = R(r, -hw * 0.4f, hw * 0.4f), lz = R(r, -hd * 0.3f, hd * 0.3f);
                m.Box(HillMat.MetalPaint, new Vector3(lx - 0.6f, eave, lz - 0.45f), new Vector3(lx + 0.6f, eave + 0.1f, lz + 0.45f), 0.02f, Side.YNeg);
                m.Quad(HillMat.Glass, new Vector3(lx - 0.52f, eave + 0.105f, lz - 0.37f), new Vector3(lx + 0.52f, eave + 0.105f, lz - 0.37f), new Vector3(lx + 0.52f, eave + 0.105f, lz + 0.37f), new Vector3(lx - 0.52f, eave + 0.105f, lz + 0.37f), Vector3.up);
                if (h.gutters)
                    foreach (int sx in new[] { -1, 1 })
                    {
                        var p = new Vector3(sx * (hw - 0.3f), 0f, hd + 0.07f);
                        m.Box(HillMat.Gutter, new Vector3(p.x - 0.12f, eave + 0.05f, hd), new Vector3(p.x + 0.12f, eave + 0.25f, hd + 0.11f), 0.01f, Side.ZNeg);
                        m.Cylinder(HillMat.Gutter, new Vector3(p.x, -h.plinth - 0.22f, p.z), new Vector3(p.x, eave + 0.06f, p.z), 0.04f, 6, false);
                    }
            }

            // ---------- Chimney ----------

            void Chimney()
            {
                float cx = hw - 1.1f, top = ridge + 0.9f;
                float x0 = cx - 0.4f, x1 = cx + 0.4f, z0 = -0.35f, z1 = 0.45f;
                float baseY = Mathf.Min(Mathf.Min(RoofTop(x0, z0), RoofTop(x1, z0)), Mathf.Min(RoofTop(x0, z1), RoofTop(x1, z1))) - 0.35f;
                baseY = Mathf.Max(baseY, eave - 0.5f);
                m.Box(chimneyMat, new Vector3(x0 + 0.03f, baseY, z0 + 0.03f), new Vector3(x1 - 0.03f, top - 0.62f, z1 - 0.03f), 0.015f, Side.YNeg);
                m.Box(chimneyMat, new Vector3(x0, top - 0.62f, z0), new Vector3(x1, top - 0.5f, z1), 0.01f);
                m.Box(HillMat.StoneCoping, new Vector3(x0, top - 0.5f, z0), new Vector3(x1, top - 0.42f, z1), 0.012f);
                // A lead flashing collar where it leaves the roof.
                float fy = Mathf.Max(RoofTop(x0, z0), RoofTop(x0, z1));
                m.Box(HillMat.MetalDark, new Vector3(x0 + 0.02f, Mathf.Min(RoofTop(x0, z0), RoofTop(x1, z1)) - 0.02f, z0 + 0.02f), new Vector3(x1 - 0.02f, fy + 0.08f, z1 - 0.02f), 0f, Side.YNeg | Side.YPos);
                int pots = 1 + r.Next(3);
                for (int i = 0; i < pots; i++)
                {
                    float px = pots == 1 ? cx : cx - 0.22f + 0.44f * i / (pots - 1);
                    float pz = (z0 + z1) * 0.5f;
                    float ph = R(r, 0.22f, 0.32f);
                    m.Cylinder(HillMat.RoofClay, new Vector3(px, top - 0.42f, pz), new Vector3(px, top - 0.42f + ph, pz), 0.075f, 8, false);
                    m.Cylinder(HillMat.RoofClay, new Vector3(px, top - 0.42f + ph - 0.06f, pz), new Vector3(px, top - 0.42f + ph, pz), 0.092f, 8, true);
                }
            }

            // ---------- Turret ----------

            void Turret()
            {
                const float a = 1.45f;
                float halfW = a * Mathf.Tan(22.5f * Mathf.Deg2Rad);
                var c = new Vector3(tx, 0f, tz);
                float top = eave + 0.15f, pb = -h.plinth - 0.3f;
                string coneMat = Chance(r, 0.3f) ? HillMat.Copper : roofMat;
                for (int k = 0; k < 8; k++)
                {
                    float th = k * 45f * Mathf.Deg2Rad;
                    var n = new Vector3(Mathf.Sin(th), 0f, -Mathf.Cos(th));
                    if (n.z > 0.9f) continue;   // the back face is inside the house
                    var along = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                    m.Push(WallFrame(c + n * a - along * halfW, along, n));
                    float L = halfW * 2f;
                    var outline = new List<Vector2> { new Vector2(0f, 0f), new Vector2(L, 0f), new Vector2(L, top), new Vector2(0f, top) };
                    var wins = new List<Win>();
                    var holes = new List<Rect>();
                    if (n.z < -0.3f)
                        for (int s = 0; s < nFull + (half ? 1 : 0); s++)
                        {
                            float y0 = s < nFull ? s * sh + sill : nFull * sh + 0.5f;
                            float wh = s < nFull ? Mathf.Min(winH, sh - sill - 0.45f) : 0.9f;
                            if (y0 + wh + 0.3f > top) continue;
                            var win = MakeWin(L * 0.5f, y0, 0.62f, wh, s);
                            win.shutters = false;
                            win.flowerBox = false;
                            win.casing = casing == 1 ? 1 : 2;
                            win.trim = trim;
                            wins.Add(win);
                            holes.Add(win.Hole);
                        }
                    WallFace(m, wallMat, outline, holes);
                    foreach (var w in wins) Window(m, w, r);
                    for (int s = 1; s < nFull; s++) m.Box(bandMat, new Vector3(-0.01f, s * sh - 0.07f, 0f), new Vector3(L + 0.01f, s * sh + 0.07f, 0.03f), 0f, Side.ZNeg);
                    m.Box(trim, new Vector3(-0.02f, top - 0.24f, 0f), new Vector3(L + 0.02f, top - 0.02f, 0.05f), 0.01f, Side.ZNeg);
                    m.Box(HillMat.Plinth, new Vector3(-0.01f, pb, 0f), new Vector3(L + 0.01f, 0f, 0.04f), 0f, Side.ZNeg);
                    m.Pop();
                }
                // The conical roof, its eave inside the turret's box.
                float rv = 1.5f / Mathf.Cos(22.5f * Mathf.Deg2Rad);
                var apex = new Vector3(tx, eave + 2.04f, tz);
                var ring = new Vector3[8];
                for (int k = 0; k < 8; k++)
                {
                    float th = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                    ring[k] = c + new Vector3(Mathf.Sin(th), 0f, -Mathf.Cos(th)) * rv + Vector3.up * top;
                }
                for (int k = 0; k < 8; k++)
                {
                    var p0 = ring[k];
                    var p1 = ring[(k + 1) % 8];
                    var mid = (p0 + p1) * 0.5f - new Vector3(tx, top, tz);
                    m.Tri(coneMat, p0, p1, apex, mid + Vector3.up * 0.6f);
                    m.Quad(trim, p0, p1, p1 - Vector3.up * 0.08f, p0 - Vector3.up * 0.08f, mid);
                }
                var under = new Vector3[8];
                for (int k = 0; k < 8; k++) under[k] = ring[k] - Vector3.up * 0.08f;
                m.Poly(trim, under, Vector3.down);
                m.Cylinder(HillMat.MetalDark, apex - Vector3.up * 0.1f, new Vector3(tx, eave + 2.19f, tz), 0.022f, 6, false);
                m.Ellipsoid(HillMat.Copper, new Vector3(tx, eave + 2.1f, tz), new Vector3(0.05f, 0.05f, 0.05f), 6);
            }

            // ---------- Dormers ----------

            void Dormers()
            {
                if (h.dormers <= 0 || roof == HillRoof.Flat) return;
                bool sideSlope = roof == HillRoof.GableFront;
                float zExt = sideSlope ? X : Z, wallZ = sideSlope ? hw : hd, halfLen = sideSlope ? hd : hw, ridgeZ = roof == HillRoof.Shed ? Z : 0f;
                if (sideSlope) m.Push(Frame(Vector3.zero, Vector3.forward, Vector3.up, Vector3.left));
                try
                {
                    // Keep clear of the chimney and the turret (in the slope's own x).
                    var keepOut = new List<Vector2>();
                    if (h.chimney) keepOut.Add(sideSlope ? new Vector2(-0.6f, 0.7f) : new Vector2(hw - 1.6f, hw - 0.6f));
                    if (h.turret != 0)
                    {
                        if (!sideSlope) keepOut.Add(new Vector2(tx - 1.7f, tx + 1.7f));
                        else if (h.turret > 0) keepOut.Add(new Vector2(-hd - 1.3f, -hd + 1.95f));
                    }
                    int n = Mathf.Min(h.dormers, 3);
                    float wd = R(r, 1.3f, 1.65f);
                    float usable = halfLen * 2f - 1.6f;
                    if (usable < wd) return;
                    for (int i = 0; i < n; i++)
                    {
                        float xc = -usable * 0.5f + usable * (i + 0.5f) / n;
                        bool clash = false;
                        foreach (var k in keepOut) if (xc + wd * 0.5f + 0.15f > k.x && xc - wd * 0.5f - 0.15f < k.y) clash = true;
                        if (clash) continue;
                        Dormer(xc, wd, zExt, wallZ, ridgeZ);
                    }
                }
                finally { if (sideSlope) m.Pop(); }
            }

            /// <summary>One gabled dormer in a slope frame (the slope rises along +z from its eave at -zExt).</summary>
            void Dormer(float xc, float wd, float zExt, float wallZ, float ridgeZ)
            {
                float T(float z) => low + G * (z + zExt);
                float Zat(float y) => (y - low) / G - zExt;
                if (G < 0.2f) return;
                float zf = -wallZ + 0.3f, yb = T(zf);
                float wh = R(r, 0.75f, 0.92f), ww = wd - 0.55f;
                for (int attempt = 0; attempt < 2; attempt++, wh -= 0.15f)
                {
                    float ys = yb + 0.18f, yt = ys + wh, yE = yt + 0.3f;
                    float k = R(r, 0.85f, 1.1f);
                    float yR = yE + (wd * 0.5f) * k;
                    float zR = Zat(yR);
                    if (zR > ridgeZ - 0.3f) continue;
                    if (roof == HillRoof.Hip && Mathf.Abs(xc) + wd * 0.5f + 0.35f > X - (zR + Z)) return;
                    // The front wall, with its window.
                    m.Push(WallFrame(new Vector3(xc - wd * 0.5f, 0f, zf), Vector3.right, Vector3.back));
                    var outline = new List<Vector2> { new Vector2(0f, yb), new Vector2(wd, yb), new Vector2(wd, yE - 0.05f), new Vector2(wd * 0.5f, yR - 0.05f), new Vector2(0f, yE - 0.05f) };
                    var win = MakeWin(wd * 0.5f, ys, ww, wh, nFull);
                    win.shutters = false;
                    win.flowerBox = false;
                    win.casing = siding ? 0 : 2;
                    win.headCap = false;
                    win.wall = dormerWall;
                    win.reveal = siding ? trim : dormerWall;
                    win.depth = 0.1f;
                    WallFace(m, dormerWall, outline, new[] { win.Hole });
                    Window(m, win, r);
                    m.Box(trim, new Vector3(-0.02f, yb, 0f), new Vector3(0.09f, yE - 0.06f, 0.025f), 0f, Side.ZNeg);
                    m.Box(trim, new Vector3(wd - 0.09f, yb, 0f), new Vector3(wd + 0.02f, yE - 0.06f, 0.025f), 0f, Side.ZNeg);
                    m.Box(HillMat.MetalDark, new Vector3(-0.03f, yb - 0.01f, -0.02f), new Vector3(wd + 0.03f, yb + 0.05f, 0.03f), 0f, Side.ZNeg);
                    m.Pop();
                    // The cheeks.
                    float zE = Zat(yE - 0.05f);
                    foreach (int s in new[] { -1, 1 })
                    {
                        float x = xc + s * wd * 0.5f;
                        m.Tri(dormerWall, new Vector3(x, yb, zf), new Vector3(x, yE - 0.05f, zf), new Vector3(x, yE - 0.05f, zE), new Vector3(s, 0f, 0f));
                    }
                    // Its little gable roof, valleys on the main slope.
                    float ov = 0.12f, ovf = 0.16f;
                    float kk = (yR - yE) / (wd * 0.5f);
                    float yEdge = yE - ov * kk;
                    float zEd = Zat(yEdge);
                    foreach (int s in new[] { -1, 1 })
                    {
                        float xo = xc + s * (wd * 0.5f + ov);
                        var plane = new RoofPlane(new[] { new Vector3(xo, yEdge, zf - ovf), new Vector3(xc, yR, zf - ovf), new Vector3(xc, yR, zR), new Vector3(xo, yEdge, zEd) },
                            new[] { Edge.Rake, Edge.Inner, Edge.Inner, Edge.Eave });
                        Slab(plane, roofMat, trim, trim, 0.09f);
                    }
                    m.Beam(roofMat, new Vector3(xc, yR + 0.01f, zf - ovf - 0.01f), new Vector3(xc, yR + 0.01f, zR), 0.16f, 0.05f, 0.008f);
                    return;
                }
            }

            // ---------- Porch and steps ----------

            void Porch()
            {
                float zf0 = -hd - pd, P = h.plinth, bottom = -P - 0.3f;
                const Side back = Side.ZPos;
                m.Box(HillMat.Deck, new Vector3(-pw, -0.05f, zf0), new Vector3(pw, 0f, -hd), 0f, Side.YNeg | back);
                // The rim boards round the deck.
                m.Box(trim, new Vector3(-pw - 0.025f, -0.3f, zf0 - 0.025f), new Vector3(pw + 0.025f, -0.004f, zf0), 0.005f, back);
                m.Box(trim, new Vector3(-pw - 0.025f, -0.3f, zf0), new Vector3(-pw, -0.004f, -hd), 0f, Side.XPos | back | Side.ZNeg);
                m.Box(trim, new Vector3(pw, -0.3f, zf0), new Vector3(pw + 0.025f, -0.004f, -hd), 0f, Side.XNeg | back | Side.ZNeg);
                // The skirt: lattice over a dark void, in a frame.
                if (bottom < -0.34f)
                {
                    float y0 = bottom, y1 = -0.3f;
                    string lat = Chance(r, 0.5f) ? trim : HillMat.FenceWood;
                    m.Quad(HillMat.TrimDark, new Vector3(-pw, y0, zf0 + 0.05f), new Vector3(pw, y0, zf0 + 0.05f), new Vector3(pw, y1, zf0 + 0.05f), new Vector3(-pw, y1, zf0 + 0.05f), Vector3.back);
                    m.Quad(HillMat.TrimDark, new Vector3(-pw + 0.05f, y0, zf0), new Vector3(-pw + 0.05f, y0, -hd), new Vector3(-pw + 0.05f, y1, -hd), new Vector3(-pw + 0.05f, y1, zf0), Vector3.left);
                    m.Quad(HillMat.TrimDark, new Vector3(pw - 0.05f, y0, zf0), new Vector3(pw - 0.05f, y0, -hd), new Vector3(pw - 0.05f, y1, -hd), new Vector3(pw - 0.05f, y1, zf0), Vector3.right);
                    m.Push(WallFrame(new Vector3(-pw, 0f, zf0), Vector3.right, Vector3.back));
                    Lattice(m, lat, 0.06f, pw * 2f - 0.06f, y0, y1, 0.01f, 0.17f, 0.04f);
                    m.Pop();
                    m.Push(WallFrame(new Vector3(-pw, 0f, -hd), Vector3.back, Vector3.left));
                    Lattice(m, lat, 0.06f, pd, y0, y1, 0.01f, 0.17f, 0.04f);
                    m.Pop();
                    m.Push(WallFrame(new Vector3(pw, 0f, zf0), Vector3.forward, Vector3.right));
                    Lattice(m, lat, 0.06f, pd, y0, y1, 0.01f, 0.17f, 0.04f);
                    m.Pop();
                    foreach (float x in new[] { -pw, pw - 0.12f })
                        m.Box(trim, new Vector3(x, y0, zf0 - 0.02f), new Vector3(x + 0.12f, y1, zf0 + 0.1f), 0f, back);
                }
                float zp = zf0 + 0.12f;
                if (h.porchRoof)
                {
                    float ph = HillColliders.PorchRoofHeight, top = ph + HillColliders.PorchRoofThick;
                    foreach (float x in new[] { -pw + 0.12f, pw - 0.12f }) Post(x, zp, ph);
                    // The header and side beams under the roof (trim: 0.12 below its collider).
                    m.Box(trim, new Vector3(-pw + 0.04f, ph - 0.12f, zp - 0.07f), new Vector3(pw - 0.04f, ph, zp + 0.07f), 0.008f, Side.YPos);
                    foreach (float x in new[] { -pw + 0.12f, pw - 0.12f })
                        m.Box(trim, new Vector3(x - 0.06f, ph - 0.1f, zp + 0.07f), new Vector3(x + 0.06f, ph, -hd), 0f, Side.YPos | back);
                    // The roof: fascia and beadboard soffit in trim, a walkable flat top.
                    var mn = new Vector3(-pw - 0.15f, ph, zf0 - 0.15f);
                    var mx = new Vector3(pw + 0.15f, top, -hd);
                    m.Box(trim, mn, mx, 0f, Side.YPos | back);
                    m.Quad(roofMat, new Vector3(mn.x, top, mn.z), new Vector3(mx.x, top, mn.z), new Vector3(mx.x, top, mx.z), new Vector3(mn.x, top, mx.z), Vector3.up);
                    // A moulding under the fascia and a metal drip edge over it (front and sides only: the soffit and top stay clear).
                    m.Box(trim, new Vector3(mn.x - 0.03f, ph - 0.03f, mn.z - 0.03f), new Vector3(mx.x + 0.03f, ph + 0.03f, mn.z + 0.06f), 0.01f);
                    m.Box(trim, new Vector3(mn.x - 0.03f, ph - 0.03f, mn.z + 0.06f), new Vector3(mn.x + 0.06f, ph + 0.03f, -hd), 0.01f, back);
                    m.Box(trim, new Vector3(mx.x - 0.06f, ph - 0.03f, mn.z + 0.06f), new Vector3(mx.x + 0.03f, ph + 0.03f, -hd), 0.01f, back);
                    m.Box(HillMat.Gutter, new Vector3(mn.x - 0.012f, top - 0.035f, mn.z - 0.012f), new Vector3(mx.x + 0.012f, top + 0.006f, mn.z + 0.05f), 0f, Side.YNeg);
                    m.Box(HillMat.Gutter, new Vector3(mn.x - 0.012f, top - 0.035f, mn.z + 0.05f), new Vector3(mn.x + 0.05f, top + 0.006f, -hd), 0f, Side.YNeg | back);
                    m.Box(HillMat.Gutter, new Vector3(mx.x - 0.05f, top - 0.035f, mn.z + 0.05f), new Vector3(mx.x + 0.012f, top + 0.006f, -hd), 0f, Side.YNeg | back);
                    // The porch light in the soffit.
                    float lz = (zf0 - hd) * 0.5f;
                    m.Cylinder(HillMat.MetalDark, new Vector3(0f, ph - 0.035f, lz), new Vector3(0f, ph, lz), 0.13f, 10, true);
                    m.Cylinder(HillMat.LampGlow, new Vector3(0f, ph - 0.06f, lz), new Vector3(0f, ph - 0.035f, lz), 0.1f, 10, true);
                }
                else
                {
                    foreach (float x in new[] { -pw + 0.06f, pw - 0.06f, -0.95f, 0.95f }) Newel(x, zf0 + 0.06f);
                    foreach (float x in new[] { -pw + 0.06f, pw - 0.06f }) Newel(x, -hd - 0.06f);
                }
                // Railings: the front either side of the steps, and the sides.
                float rz = h.porchRoof ? zp : zf0 + 0.06f, rx = h.porchRoof ? pw - 0.12f : pw - 0.06f, inset = h.porchRoof ? 0.08f : 0.06f;
                Railing(new Vector3(-rx + inset, 0f, rz), new Vector3(-0.95f - (h.porchRoof ? 0f : 0.06f), 0f, rz));
                Railing(new Vector3(0.95f + (h.porchRoof ? 0f : 0.06f), 0f, rz), new Vector3(rx - inset, 0f, rz));
                Railing(new Vector3(-rx, 0f, rz + inset), new Vector3(-rx, 0f, -hd - (h.porchRoof ? 0f : 0.06f)));
                Railing(new Vector3(rx, 0f, rz + inset), new Vector3(rx, 0f, -hd - (h.porchRoof ? 0f : 0.06f)));
                if (P > 0.15f) Steps(zf0, true);
            }

            void Post(float x, float z, float height)
            {
                string pm = trim;
                switch (postStyle)
                {
                    case 1:   // a turned column
                        m.Box(pm, new Vector3(x - 0.08f, 0f, z - 0.08f), new Vector3(x + 0.08f, 0.22f, z + 0.08f), 0.012f);
                        m.Tube(pm, new[] { new Vector3(x, 0.22f, z), new Vector3(x, 0.4f, z), new Vector3(x, height * 0.6f, z), new Vector3(x, height - 0.25f, z) },
                            new[] { 0.068f, 0.064f, 0.06f, 0.054f }, 10, false, 1f);
                        m.Cylinder(pm, new Vector3(x, height - 0.27f, z), new Vector3(x, height - 0.22f, z), 0.068f, 10, false);
                        m.Box(pm, new Vector3(x - 0.08f, height - 0.22f, z - 0.08f), new Vector3(x + 0.08f, height, z + 0.08f), 0.012f);
                        break;
                    case 2:   // craftsman: a stone pier and a square post
                        m.Box(HillMat.Plinth, new Vector3(x - 0.08f, 0f, z - 0.08f), new Vector3(x + 0.08f, 0.95f, z + 0.08f), 0.015f);
                        m.Box(HillMat.StoneCoping, new Vector3(x - 0.08f, 0.95f, z - 0.08f), new Vector3(x + 0.08f, 1.0f, z + 0.08f), 0.01f);
                        m.Box(pm, new Vector3(x - 0.06f, 1.0f, z - 0.06f), new Vector3(x + 0.06f, height - 0.12f, z + 0.06f), 0.01f);
                        m.Box(pm, new Vector3(x - 0.08f, height - 0.12f, z - 0.08f), new Vector3(x + 0.08f, height, z + 0.08f), 0.01f);
                        break;
                    default:  // square, with a base and a capital
                        m.Box(pm, new Vector3(x - 0.08f, 0f, z - 0.08f), new Vector3(x + 0.08f, 0.3f, z + 0.08f), 0.012f);
                        m.Box(pm, new Vector3(x - 0.062f, 0.3f, z - 0.062f), new Vector3(x + 0.062f, height - 0.18f, z + 0.062f), 0.01f);
                        m.Box(pm, new Vector3(x - 0.072f, height - 0.18f, z - 0.072f), new Vector3(x + 0.072f, height - 0.14f, z + 0.072f), 0.006f);
                        m.Box(pm, new Vector3(x - 0.08f, height - 0.14f, z - 0.08f), new Vector3(x + 0.08f, height, z + 0.08f), 0.012f);
                        break;
                }
            }

            void Newel(float x, float z)
            {
                m.Box(trim, new Vector3(x - 0.06f, 0f, z - 0.06f), new Vector3(x + 0.06f, 0.98f, z + 0.06f), 0.01f);
                m.Box(trim, new Vector3(x - 0.075f, 0.98f, z - 0.075f), new Vector3(x + 0.075f, 1.03f, z + 0.075f), 0.012f);
            }

            /// <summary>A porch railing 0.9 m high between two points on the deck: rails and square balusters.</summary>
            void Railing(Vector3 a, Vector3 b)
            {
                var d = b - a;
                float len = d.magnitude;
                if (len < 0.15f) return;
                var dir = d / len;
                bool alongX = Mathf.Abs(dir.x) > 0.5f;
                Vector3 Lo(Vector3 p, float w, float y) => alongX ? new Vector3(Mathf.Min(a.x, b.x), y, p.z - w) : new Vector3(p.x - w, y, Mathf.Min(a.z, b.z));
                Vector3 Hi(Vector3 p, float w, float y) => alongX ? new Vector3(Mathf.Max(a.x, b.x), y, p.z + w) : new Vector3(p.x + w, y, Mathf.Max(a.z, b.z));
                m.Box(trim, Lo(a, 0.04f, 0.86f), Hi(a, 0.04f, 0.92f), 0.01f);
                m.Box(trim, Lo(a, 0.03f, 0.06f), Hi(a, 0.03f, 0.12f), 0.006f);
                int n = Mathf.Max(1, Mathf.FloorToInt(len / 0.13f));
                for (int i = 0; i < n; i++)
                {
                    var p = a + dir * (len * (i + 0.5f) / n);
                    m.Box(trim, new Vector3(p.x - 0.02f, 0.12f, p.z - 0.02f), new Vector3(p.x + 0.02f, 0.86f, p.z + 0.02f), 0f, Side.YNeg | Side.YPos);
                }
            }

            /// <summary>Steps from the ground up to the floor in front of <paramref name="zTop"/>: their nosings on the
            /// steps collider's slope (0.6 m up per metre out), as wide as the door's bay.</summary>
            void Steps(float zTop, bool porch)
            {
                float P = h.plinth, run = P / 0.6f;
                int n = Mathf.Max(1, Mathf.RoundToInt(P / 0.18f));
                float rh = P / n, td = run / n, zb = zTop - run, bottom = -P - 0.3f;
                for (int i = 1; i < n; i++)
                {
                    float y = -P + i * rh, z = zb + i * td;
                    m.Box(stepMat, new Vector3(-0.82f, i == 1 ? bottom : y - rh - 0.02f, z), new Vector3(0.82f, y, zTop), 0.012f, Side.ZPos | Side.YNeg);
                }
                if (P > 0.3f)
                {
                    string cheek = stepMat == HillMat.Deck ? trim : HillMat.Plinth;
                    var outline = new[] { new Vector2(zb - 0.02f, bottom), new Vector2(zb - 0.02f, -P + 0.07f), new Vector2(zTop, 0.07f), new Vector2(zTop, bottom) };
                    Extrude(m, cheek, outline, Vector3.forward, Vector3.up, Vector3.right, 0.82f, 0.9f);
                    Extrude(m, cheek, outline, Vector3.forward, Vector3.up, Vector3.right, -0.9f, -0.82f);
                }
            }
        }
    }
}
