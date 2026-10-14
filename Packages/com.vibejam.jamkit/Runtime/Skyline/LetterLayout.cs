using System;

namespace JamKit.Skyline
{
    /// <summary>
    /// The write-out of one piece of comic lettering (docs/SKYLINE-PLAN-FLOOR1.md Task C2): each glyph's width wipes open left
    /// to right, about <see cref="ComicLettering.GlyphSeconds"/> per glyph and overlapped, then the word holds
    /// (<see cref="ComicLettering.HoldSeconds"/>) and fades (<see cref="ComicLettering.FadeSeconds"/>). The whole word takes
    /// <c>ComicLettering.SecondsFor(text)</c>. Pure, so the Unity side only reads the numbers. Starting version: a wipe per
    /// glyph (a true stroke order per word is a later option for the creator).
    /// </summary>
    public static class LetterWipe
    {
        /// <summary>When the wipe of an <paramref name="n"/>-glyph word is complete.</summary>
        public static float WipeSeconds(int n) => Math.Max(1, n) * ComicLettering.GlyphSeconds;

        /// <summary>How open glyph <paramref name="i"/> of <paramref name="n"/> is at time <paramref name="t"/>, 0..1. Glyph
        /// i starts at <c>i * (n - 2) / (n - 1)</c> glyph-times and opens over two, so the last one finishes exactly at
        /// <see cref="WipeSeconds"/> and neighbours overlap.</summary>
        public static float GlyphOpen(int i, int n, float t)
        {
            if (n < 1 || i < 0 || i >= n || float.IsNaN(t)) return 0f;
            float g = ComicLettering.GlyphSeconds;
            float len = n == 1 ? g : 2f * g;
            float start = n > 1 ? i * (n - 2f) * g / (n - 1f) : 0f;
            return Clamp01((t - start) / len);
        }

        /// <summary>The share of the word's width revealed at <paramref name="t"/> (the mean of the glyphs' openings; glyphs are
        /// taken as equally wide), 0..1.</summary>
        public static float Revealed(int n, float t)
        {
            if (n < 1) return 0f;
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += GlyphOpen(i, n, t);
            return sum / n;
        }

        /// <summary>Opacity at <paramref name="t"/>: 1 through the wipe and the hold, then a linear fade to 0.</summary>
        public static float Alpha(int n, float t)
        {
            if (float.IsNaN(t) || t < 0f) return 0f;
            float fadeStart = WipeSeconds(n) + ComicLettering.HoldSeconds;
            if (t <= fadeStart) return 1f;
            return 1f - Clamp01((t - fadeStart) / ComicLettering.FadeSeconds);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// Where a piece of lettering sits on the HUD panel (panel units, y down; the HUD's 1920 x 1080 reference): beside its
    /// bot, never within <see cref="CrosshairClearance"/> of the crosshair, inside the panel margin, not over another
    /// piece. Pure; <c>SkylineLettering</c> feeds it the projected bot.
    /// </summary>
    public static class LetterPlacement
    {
        /// <summary>The crosshair's clear zone: the nearest point of a word's box is at least this far from it (the spec: 120 px).</summary>
        public const float CrosshairClearance = 120f;
        /// <summary>The gap between the bot's silhouette and the word.</summary>
        public const float Gap = 18f;
        /// <summary>The number of candidate sides round a bot.</summary>
        public const int Sides = 8;

        // right-up, left-up, right, left, up, right-down, left-down, down
        static readonly float[] Dx = { 1f, -1f, 1f, -1f, 0f, 1f, -1f, 0f };
        static readonly float[] Dy = { -1f, -1f, 0f, 0f, -1f, 1f, 1f, 1f };

        /// <summary>The centre of side <paramref name="side"/> (0..<see cref="Sides"/>-1) of a bot at (bx, by) with apparent
        /// radius <paramref name="radius"/>, for a word box w x h.</summary>
        public static void Candidate(int side, float bx, float by, float radius, float w, float h, out float cx, out float cy)
        {
            side = ((side % Sides) + Sides) % Sides;
            float dx = Dx[side], dy = Dy[side];
            if (dx != 0f)
            {
                cx = bx + dx * (radius + Gap + w * 0.5f);
                cy = by + dy * (0.6f * radius + 0.5f * h);
                if (dy == 0f) cy = by;
            }
            else
            {
                cx = bx;
                cy = by + dy * (radius + Gap + h * 0.5f);
            }
        }

        /// <summary>Distance from the point (px, py) to the box centred (cx, cy) of size w x h; 0 inside.</summary>
        public static float DistanceToBox(float px, float py, float cx, float cy, float w, float h)
        {
            float ddx = Math.Max(0f, Math.Abs(px - cx) - w * 0.5f);
            float ddy = Math.Max(0f, Math.Abs(py - cy) - h * 0.5f);
            return (float)Math.Sqrt(ddx * ddx + ddy * ddy);
        }

        static bool Overlaps(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh) =>
            Math.Abs(ax - bx) < (aw + bw) * 0.5f && Math.Abs(ay - by) < (ah + bh) * 0.5f;

        static bool Fits(float cx, float cy, float w, float h, float crossX, float crossY, float panelW, float panelH, float margin,
            float[] others, int otherCount)
        {
            if (cx - w * 0.5f < margin || cx + w * 0.5f > panelW - margin) return false;
            if (cy - h * 0.5f < margin || cy + h * 0.5f > panelH - margin) return false;
            if (DistanceToBox(crossX, crossY, cx, cy, w, h) < CrosshairClearance) return false;
            for (int i = 0; i < otherCount; i++)
                if (Overlaps(cx, cy, w, h, others[4 * i], others[4 * i + 1], others[4 * i + 2], others[4 * i + 3])) return false;
            return true;
        }

        /// <summary>Finds a place for a w x h word next to a bot at (bx, by) of apparent radius <paramref name="radius"/>.
        /// Tries side <paramref name="preferred"/> first, then the others in order; with none valid, takes the preferred side
        /// and pushes it away from the crosshair, inside the margin. <paramref name="others"/> holds <paramref name="otherCount"/>
        /// boxes as (centre x, centre y, w, h) quadruples. Returns false when there is no valid spot at all (the caller
        /// hides the word); the result never lies within <see cref="CrosshairClearance"/> of the crosshair, or outside the
        /// margin, or over another word.</summary>
        public static bool Place(float bx, float by, float radius, float w, float h, float crossX, float crossY,
            float panelW, float panelH, float margin, int preferred, float[] others, int otherCount,
            out float cx, out float cy, out int side)
        {
            cx = cy = 0f;
            side = ((preferred % Sides) + Sides) % Sides;
            if (float.IsNaN(bx) || float.IsNaN(by) || float.IsNaN(radius) || !(w > 0f) || !(h > 0f)) return false;
            for (int k = 0; k < Sides; k++)
            {
                int s = (side + k) % Sides;
                Candidate(s, bx, by, radius, w, h, out float tx, out float ty);
                if (Fits(tx, ty, w, h, crossX, crossY, panelW, panelH, margin, others, otherCount))
                {
                    cx = tx; cy = ty; side = s;
                    return true;
                }
            }
            // No side clear: take the preferred one, slide it away from the crosshair, and keep it inside the margin.
            Candidate(side, bx, by, radius, w, h, out float x, out float y);
            for (int i = 0; i < 80; i++)
            {
                x = Math.Max(margin + w * 0.5f, Math.Min(panelW - margin - w * 0.5f, x));
                y = Math.Max(margin + h * 0.5f, Math.Min(panelH - margin - h * 0.5f, y));
                if (Fits(x, y, w, h, crossX, crossY, panelW, panelH, margin, others, otherCount)) { cx = x; cy = y; return true; }
                float vx = x - crossX, vy = y - crossY;
                float len = (float)Math.Sqrt(vx * vx + vy * vy);
                if (len < 1e-3f) { vx = Dx[side] == 0f ? 1f : Dx[side]; vy = 0f; len = 1f; }
                x += vx / len * 12f;
                y += vy / len * 12f;
            }
            return false;
        }
    }
}
