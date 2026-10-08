using System;
using System.Collections.Generic;

namespace JamKit.Narrative
{
    public enum HotspotShapeKind { Rect, Polygon }

    /// <summary>
    /// A clickable region on a background, in normalized coordinates: x 0 (left) .. 1 (right),
    /// y 0 (top) .. 1 (bottom), so it fits the picture at any screen size. A rectangle is x, y
    /// (top-left), w, h; a polygon is its corners in order (x0, y0, x1, y1, ...).
    /// </summary>
    public struct HotspotShape
    {
        public HotspotShapeKind kind;
        public float x, y, w, h;
        public float[] points;

        public static HotspotShape Rect(float x, float y, float w, float h) =>
            new HotspotShape { kind = HotspotShapeKind.Rect, x = x, y = y, w = w, h = h };

        public static HotspotShape Polygon(params float[] xy) =>
            new HotspotShape { kind = HotspotShapeKind.Polygon, points = xy };

        public int PointCount => kind == HotspotShapeKind.Polygon && points != null ? points.Length / 2 : 4;

        /// <summary>Corner <paramref name="i"/> (rectangles have four, clockwise from the top-left).</summary>
        public void Point(int i, out float px, out float py)
        {
            if (kind == HotspotShapeKind.Polygon && points != null)
            {
                px = points[i * 2];
                py = points[i * 2 + 1];
                return;
            }
            switch (i & 3)
            {
                case 0: px = x; py = y; break;
                case 1: px = x + w; py = y; break;
                case 2: px = x + w; py = y + h; break;
                default: px = x; py = y + h; break;
            }
        }

        /// <summary>A polygon needs 3+ corners and a rectangle a size; null when fine.</summary>
        public string Problem()
        {
            if (kind == HotspotShapeKind.Rect)
            {
                if (!(w > 0f) || !(h > 0f)) return "the rectangle has no size (w and h must be above 0)";
                if (x < -0.01f || y < -0.01f || x + w > 1.01f || y + h > 1.01f) return "the rectangle goes off the picture (use 0..1 coordinates)";
                return null;
            }
            if (points == null || points.Length < 6) return "a polygon needs at least 3 points";
            if (points.Length % 2 != 0) return "a polygon's points come in x,y pairs";
            for (int i = 0; i < points.Length; i++)
                if (points[i] < -0.01f || points[i] > 1.01f || float.IsNaN(points[i])) return "a polygon point is off the picture (use 0..1 coordinates)";
            if (HotspotGeometry.Area(this) < 1e-5f) return "the polygon has no area (points in a line?)";
            return null;
        }
    }

    public static class HotspotGeometry
    {
        public static bool Contains(HotspotShape s, float px, float py)
        {
            if (s.kind == HotspotShapeKind.Rect)
                return px >= s.x && px <= s.x + s.w && py >= s.y && py <= s.y + s.h;
            var p = s.points;
            if (p == null || p.Length < 6) return false;
            int n = p.Length / 2;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
                if ((yi > py) != (yj > py) && px < (xj - xi) * (py - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        public static float Area(HotspotShape s)
        {
            if (s.kind == HotspotShapeKind.Rect) return Math.Abs(s.w * s.h);
            var p = s.points;
            if (p == null || p.Length < 6) return 0f;
            int n = p.Length / 2;
            float a = 0f;
            for (int i = 0, j = n - 1; i < n; j = i++) a += (p[j * 2] + p[i * 2]) * (p[j * 2 + 1] - p[i * 2 + 1]);
            return Math.Abs(a * 0.5f);
        }

        /// <summary>The middle of the shape (where its marker and label go).</summary>
        public static void Centroid(HotspotShape s, out float cx, out float cy)
        {
            if (s.kind == HotspotShapeKind.Rect) { cx = s.x + s.w * 0.5f; cy = s.y + s.h * 0.5f; return; }
            var p = s.points;
            cx = 0f; cy = 0f;
            if (p == null || p.Length < 2) return;
            int n = p.Length / 2;
            float a = 0f, x = 0f, y = 0f;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float cross = p[j * 2] * p[i * 2 + 1] - p[i * 2] * p[j * 2 + 1];
                a += cross;
                x += (p[j * 2] + p[i * 2]) * cross;
                y += (p[j * 2 + 1] + p[i * 2 + 1]) * cross;
            }
            if (Math.Abs(a) < 1e-7f)
            {
                for (int i = 0; i < n; i++) { cx += p[i * 2]; cy += p[i * 2 + 1]; }
                cx /= n; cy /= n;
                return;
            }
            cx = x / (3f * a);
            cy = y / (3f * a);
        }

        public static void Bounds(HotspotShape s, out float minX, out float minY, out float maxX, out float maxY)
        {
            minX = minY = float.MaxValue;
            maxX = maxY = float.MinValue;
            int n = s.PointCount;
            for (int i = 0; i < n; i++)
            {
                s.Point(i, out float px, out float py);
                if (px < minX) minX = px;
                if (py < minY) minY = py;
                if (px > maxX) maxX = px;
                if (py > maxY) maxY = py;
            }
        }
    }

    /// <summary>
    /// Which hotspot is under a point, and keyboard / gamepad selection between hotspots. Disabled
    /// hotspots (their condition is false) are skipped. When shapes overlap, the smallest one wins,
    /// so a drawer inside a desk can be picked.
    /// </summary>
    public static class HotspotPicker
    {
        /// <summary>Index of the hotspot at (x, y), or -1.</summary>
        public static int Pick(IList<HotspotShape> shapes, IList<bool> enabled, float x, float y)
        {
            int best = -1;
            float bestArea = float.MaxValue;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (enabled != null && i < enabled.Count && !enabled[i]) continue;
                if (!HotspotGeometry.Contains(shapes[i], x, y)) continue;
                float a = HotspotGeometry.Area(shapes[i]);
                if (a < bestArea) { bestArea = a; best = i; }
            }
            return best;
        }

        /// <summary>The enabled hotspots in reading order (top row first, left to right; rows are 0.12 tall).</summary>
        public static List<int> ReadingOrder(IList<HotspotShape> shapes, IList<bool> enabled)
        {
            var order = new List<int>();
            for (int i = 0; i < shapes.Count; i++)
                if (enabled == null || i >= enabled.Count || enabled[i]) order.Add(i);
            order.Sort((a, b) =>
            {
                HotspotGeometry.Centroid(shapes[a], out float ax, out float ay);
                HotspotGeometry.Centroid(shapes[b], out float bx, out float by);
                int ra = (int)Math.Floor(ay / 0.12f), rb = (int)Math.Floor(by / 0.12f);
                if (ra != rb) return ra.CompareTo(rb);
                int c = ax.CompareTo(bx);
                return c != 0 ? c : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>Tab / shoulder buttons: the next (+1) or previous (-1) hotspot in reading order, wrapping.</summary>
        public static int Cycle(IList<HotspotShape> shapes, IList<bool> enabled, int current, int step)
        {
            var order = ReadingOrder(shapes, enabled);
            if (order.Count == 0) return -1;
            int at = order.IndexOf(current);
            if (at < 0) return step >= 0 ? order[0] : order[order.Count - 1];
            int next = ((at + step) % order.Count + order.Count) % order.Count;
            return order[next];
        }

        /// <summary>Arrow keys / stick: the nearest enabled hotspot in that direction (within 60 degrees
        /// of it), or the current one if there's none. With nothing selected, the first in reading order.</summary>
        public static int Step(IList<HotspotShape> shapes, IList<bool> enabled, int current, float dx, float dy)
        {
            if (current < 0 || current >= shapes.Count || (enabled != null && current < enabled.Count && !enabled[current]))
                return Cycle(shapes, enabled, -1, 1);
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f) return current;
            dx /= len; dy /= len;
            HotspotGeometry.Centroid(shapes[current], out float cx, out float cy);
            int best = current;
            float bestScore = float.MaxValue;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (i == current || (enabled != null && i < enabled.Count && !enabled[i])) continue;
                HotspotGeometry.Centroid(shapes[i], out float x, out float y);
                float vx = x - cx, vy = y - cy;
                float dist = (float)Math.Sqrt(vx * vx + vy * vy);
                if (dist < 1e-4f) continue;
                float cos = (vx * dx + vy * dy) / dist;
                if (cos < 0.5f) continue;
                // Prefer straight ahead: distance grows as the angle widens.
                float score = dist * (2f - cos);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}
