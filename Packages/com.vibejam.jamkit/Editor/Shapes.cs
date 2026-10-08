using UnityEngine;

namespace JamKit.EditorTools
{
    /// <summary>
    /// Anti-aliased placeholder shapes drawn into pixel arrays (bottom-up rows, as Texture2D wants):
    /// squares, circles and simple layered portrait faces. No external art needed.
    /// </summary>
    public static class Shapes
    {
        public const int PortraitSize = 256;

        public enum Mood { Neutral, Happy, Worried, Smug }

        public static Color[] RoundedRect(int w, int h, float radius, Color color)
        {
            var px = new Color[w * h];
            float hw = w * 0.5f, hh = h * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedBoxSdf(x + 0.5f - hw, y + 0.5f - hh, hw - 1f, hh - 1f, radius);
                px[y * w + x] = WithAlpha(color, Coverage(d));
            }
            return px;
        }

        public static Color[] Circle(int size, Color color)
        {
            var px = new Color[size * size];
            float c = size * 0.5f, r = c - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt(Sq(x + 0.5f - c) + Sq(y + 0.5f - c)) - r;
                px[y * size + x] = WithAlpha(color, Coverage(d));
            }
            return px;
        }

        /// <summary>Shoulders and a head, no face (the expression layer adds eyes and mouth).</summary>
        public static Color[] PortraitBase(Color skin, Color body, bool spiky)
        {
            int s = PortraitSize;
            var px = new Color[s * s];
            Color outline = new Color(0.08f, 0.07f, 0.12f);
            Color hair = Color.Lerp(body, Color.black, 0.35f);
            // Body: a wide rounded box at the bottom.
            Fill(px, (x, y) => RoundedBoxSdf(x - 128f, y - 20f, 92f, 70f, 40f), outline, 4f);
            Fill(px, (x, y) => RoundedBoxSdf(x - 128f, y - 20f, 88f, 66f, 36f), body, 0f);
            // Head.
            Fill(px, (x, y) => Mathf.Sqrt(Sq(x - 128f) + Sq(y - 138f)) - 74f, outline, 4f);
            Fill(px, (x, y) => Mathf.Sqrt(Sq(x - 128f) + Sq(y - 138f)) - 70f, skin, 0f);
            // Hair.
            if (spiky)
            {
                for (int i = 0; i < 5; i++)
                {
                    float cx = 72f + i * 28f;
                    Fill(px, (x, y) => TriangleSdf(x, y, cx - 22f, 180f, cx + 22f, 180f, cx + 6f, 232f), hair, 0f);
                }
            }
            else
            {
                Fill(px, (x, y) => Mathf.Max(Mathf.Sqrt(Sq(x - 128f) + Sq(y - 150f)) - 72f, 176f - y), hair, 0f);
            }
            return px;
        }

        /// <summary>Eyes, brows and mouth on a transparent layer the size of the portrait.</summary>
        public static Color[] Face(Mood mood)
        {
            int s = PortraitSize;
            var px = new Color[s * s];
            Color ink = new Color(0.1f, 0.08f, 0.14f);
            Color cheek = new Color(1f, 0.45f, 0.5f, 0.45f);
            float ey = 140f;
            switch (mood)
            {
                case Mood.Happy:
                    Arc(px, 100f, ey - 6f, 13f, 20f, 160f, 4.5f, ink);
                    Arc(px, 156f, ey - 6f, 13f, 20f, 160f, 4.5f, ink);
                    Arc(px, 128f, 112f, 26f, 200f, 340f, 5f, ink);
                    Fill(px, (x, y) => Mathf.Sqrt(Sq(x - 84f) + Sq(y - 118f)) - 11f, cheek, 0f);
                    Fill(px, (x, y) => Mathf.Sqrt(Sq(x - 172f) + Sq(y - 118f)) - 11f, cheek, 0f);
                    break;
                case Mood.Worried:
                    Dot(px, 100f, ey, 8f, ink);
                    Dot(px, 156f, ey, 8f, ink);
                    Line(px, 84f, ey + 26f, 112f, ey + 18f, 3.5f, ink);
                    Line(px, 144f, ey + 18f, 172f, ey + 26f, 3.5f, ink);
                    Arc(px, 128f, 90f, 18f, 30f, 150f, 4.5f, ink);
                    break;
                case Mood.Smug:
                    Line(px, 86f, ey, 114f, ey, 4.5f, ink);
                    Line(px, 142f, ey, 170f, ey, 4.5f, ink);
                    Line(px, 84f, ey + 20f, 114f, ey + 14f, 3.5f, ink);
                    Line(px, 142f, ey + 22f, 172f, ey + 26f, 3.5f, ink);
                    Arc(px, 140f, 112f, 22f, 220f, 330f, 4.5f, ink);
                    break;
                default:
                    Dot(px, 100f, ey, 9f, ink);
                    Dot(px, 156f, ey, 9f, ink);
                    Line(px, 112f, 104f, 144f, 104f, 4.5f, ink);
                    break;
            }
            return px;
        }

        // ---------- Drawing ----------

        delegate float Sdf(float x, float y);

        static void Fill(Color[] px, Sdf sdf, Color color, float strokeOutset)
        {
            int s = (int)Mathf.Sqrt(px.Length);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float a = Coverage(sdf(x + 0.5f, y + 0.5f)) * color.a;
                if (a <= 0f) continue;
                int i = y * s + x;
                px[i] = Over(px[i], new Color(color.r, color.g, color.b, a));
            }
        }

        static void Dot(Color[] px, float cx, float cy, float r, Color c) =>
            Fill(px, (x, y) => Mathf.Sqrt(Sq(x - cx) + Sq(y - cy)) - r, c, 0f);

        static void Line(Color[] px, float x0, float y0, float x1, float y1, float r, Color c) =>
            Fill(px, (x, y) => SegmentDistance(x, y, x0, y0, x1, y1) - r, c, 0f);

        /// <summary>An arc of a circle (degrees, counter-clockwise from +X) drawn with round caps.</summary>
        static void Arc(Color[] px, float cx, float cy, float radius, float fromDeg, float toDeg, float thickness, Color c)
        {
            const int steps = 14;
            for (int i = 0; i < steps; i++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(fromDeg, toDeg, (i + 1) / (float)steps) * Mathf.Deg2Rad;
                Line(px, cx + Mathf.Cos(a0) * radius, cy + Mathf.Sin(a0) * radius,
                    cx + Mathf.Cos(a1) * radius, cy + Mathf.Sin(a1) * radius, thickness, c);
            }
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = len2 > 0f ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / len2) : 0f;
            return Mathf.Sqrt(Sq(px - (ax + t * dx)) + Sq(py - (ay + t * dy)));
        }

        static float RoundedBoxSdf(float x, float y, float hw, float hh, float r)
        {
            r = Mathf.Min(r, Mathf.Min(hw, hh));
            float qx = Mathf.Abs(x) - hw + r, qy = Mathf.Abs(y) - hh + r;
            float outside = Mathf.Sqrt(Sq(Mathf.Max(qx, 0f)) + Sq(Mathf.Max(qy, 0f)));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float TriangleSdf(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            // Inside test by edge signs; distance to the nearest edge (negative inside).
            float d = Mathf.Min(SegmentDistance(x, y, ax, ay, bx, by),
                Mathf.Min(SegmentDistance(x, y, bx, by, cx, cy), SegmentDistance(x, y, cx, cy, ax, ay)));
            bool inside = Side(x, y, ax, ay, bx, by) >= 0f == Side(x, y, bx, by, cx, cy) >= 0f
                && Side(x, y, bx, by, cx, cy) >= 0f == Side(x, y, cx, cy, ax, ay) >= 0f;
            return inside ? -d : d;
        }

        static float Side(float x, float y, float ax, float ay, float bx, float by) => (bx - ax) * (y - ay) - (by - ay) * (x - ax);

        static float Coverage(float sdf) => Mathf.Clamp01(0.5f - sdf);

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return new Color(0f, 0f, 0f, 0f);
            return new Color(
                (src.r * src.a + dst.r * dst.a * (1f - src.a)) / a,
                (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a,
                (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a,
                a);
        }

        static float Sq(float v) => v * v;
    }
}
