using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Small HUD glyphs drawn as <c>Painter2D</c> silhouettes (no textures): the gun icons beside the ammo counter. Each is a
    /// few hard-edged polygons in a unit square, sheared a little to the right at the top to match the slanted bars, filled
    /// and stroked with the ink keyline. Draft shapes for the creator to judge; H3 draws its own asset glyphs.
    /// </summary>
    public static class SkylineHudIcons
    {
        const float Lean = 0.14f;

        // Polygons in a 0..1 box, y down. The Writ: a slide over a grip. The Breacher: a fat barrel, pump and stock.
        static readonly Vector2[][] Writ =
        {
            P(0.10f, 0.12f, 0.98f, 0.12f, 0.98f, 0.40f, 0.10f, 0.40f),
            P(0.14f, 0.40f, 0.62f, 0.40f, 0.54f, 0.52f, 0.46f, 0.52f, 0.36f, 0.94f, 0.12f, 0.94f),
        };

        static readonly Vector2[][] Breacher =
        {
            P(0.34f, 0.20f, 1.00f, 0.20f, 1.00f, 0.44f, 0.34f, 0.44f),
            P(0.46f, 0.44f, 0.76f, 0.44f, 0.76f, 0.66f, 0.46f, 0.66f),
            P(0.00f, 0.30f, 0.34f, 0.26f, 0.34f, 0.58f, 0.24f, 0.58f, 0.14f, 0.94f, 0.00f, 0.88f),
        };

        static readonly Vector2[][] Generic = { P(0.05f, 0.30f, 0.98f, 0.30f, 0.98f, 0.55f, 0.05f, 0.55f) };

        static Vector2[] P(params float[] xy)
        {
            var v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return v;
        }

        /// <summary>The silhouette polygons for a gun id (unit box).</summary>
        public static Vector2[][] GunShapes(string gunId)
        {
            if (gunId == SkylineGuns.WritId) return Writ;
            if (gunId == SkylineGuns.BreacherId) return Breacher;
            return Generic;
        }

        /// <summary>Draws the gun's silhouette filling <paramref name="r"/>.</summary>
        public static void DrawGun(Painter2D p, string gunId, Rect r, Color fill, Color ink, float line)
        {
            var shapes = GunShapes(gunId);
            for (int s = 0; s < shapes.Length; s++)
            {
                var poly = shapes[s];
                p.BeginPath();
                for (int i = 0; i < poly.Length; i++)
                {
                    // Lean: the top of the glyph shifts right, like the bars.
                    float x = r.x + (poly[i].x + (1f - poly[i].y) * Lean) * r.width / (1f + Lean);
                    float y = r.y + poly[i].y * r.height;
                    if (i == 0) p.MoveTo(new Vector2(x, y)); else p.LineTo(new Vector2(x, y));
                }
                p.ClosePath();
                SkylineHudStyle.FillStroke(p, fill, ink, line);
            }
        }
    }
}
