using System;
using UnityEngine;

namespace JamKit.Doomlike.EditorTools
{
    /// <summary>
    /// A tiny pixel canvas for the generated placeholder art: y runs down (row 0 is the top, like an
    /// image editor), shapes are drawn with hard edges (pixel art), and <see cref="ToUnity"/> flips
    /// it for Texture2D. Deterministic: the same code gives the same PNG every time.
    /// </summary>
    public sealed class Px
    {
        public readonly int w, h;
        public readonly Color32[] px;

        public Px(int w, int h)
        {
            this.w = w;
            this.h = h;
            px = new Color32[w * h];
        }

        public static Color32 C(float r, float g, float b, float a = 1f) =>
            new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));

        public static Color32 Hex(int rgb) => new Color32((byte)((rgb >> 16) & 255), (byte)((rgb >> 8) & 255), (byte)(rgb & 255), 255);

        public static Color32 Mul(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0f, 255f), (byte)Mathf.Clamp(c.g * k, 0f, 255f), (byte)Mathf.Clamp(c.b * k, 0f, 255f), c.a);

        public static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));

        /// <summary>Deterministic 0..1 noise per integer point.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                n = (n ^ (n >> 13)) * 1274126177u;
                n ^= n >> 16;
                return (n & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Smooth tiling value noise on a period of <paramref name="period"/> cells.</summary>
        public static float Smooth(float x, float y, int period, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            float a = Hash(Mod(x0, period), Mod(y0, period), seed), b = Hash(Mod(x0 + 1, period), Mod(y0, period), seed);
            float c = Hash(Mod(x0, period), Mod(y0 + 1, period), seed), d = Hash(Mod(x0 + 1, period), Mod(y0 + 1, period), seed);
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

        public void Set(int x, int y, Color32 c)
        {
            if (In(x, y)) px[y * w + x] = c;
        }

        public Color32 Get(int x, int y) => In(x, y) ? px[y * w + x] : default;

        public void Fill(Color32 c)
        {
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        public void Rect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Math.Max(0, y0); y <= Math.Min(h - 1, y1); y++)
                for (int x = Math.Max(0, x0); x <= Math.Min(w - 1, x1); x++)
                    px[y * w + x] = c;
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            if (rx <= 0f || ry <= 0f) return;
            int x0 = (int)Math.Floor(cx - rx), x1 = (int)Math.Ceiling(cx + rx);
            int y0 = (int)Math.Floor(cy - ry), y1 = (int)Math.Ceiling(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        public void Circle(float cx, float cy, float r, Color32 c) => Ellipse(cx, cy, r, r, c);

        /// <summary>A thick line with round ends.</summary>
        public void Line(float x0, float y0, float x1, float y1, float thick, Color32 c)
        {
            float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int steps = Mathf.Max(1, (int)Math.Ceiling(len * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Circle(Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), thick * 0.5f, c);
            }
        }

        /// <summary>A filled polygon (points as x, y pairs).</summary>
        public void Poly(Color32 c, params float[] pts)
        {
            int n = pts.Length / 2;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                minX = Mathf.Min(minX, pts[i * 2]); maxX = Mathf.Max(maxX, pts[i * 2]);
                minY = Mathf.Min(minY, pts[i * 2 + 1]); maxY = Mathf.Max(maxY, pts[i * 2 + 1]);
            }
            for (int y = (int)Math.Floor(minY); y <= (int)Math.Ceiling(maxY); y++)
                for (int x = (int)Math.Floor(minX); x <= (int)Math.Ceiling(maxX); x++)
                {
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    bool inside = false;
                    for (int i = 0, j = n - 1; i < n; j = i++)
                    {
                        float xi = pts[i * 2], yi = pts[i * 2 + 1], xj = pts[j * 2], yj = pts[j * 2 + 1];
                        if ((yi > py0) != (yj > py0) && px0 < (xj - xi) * (py0 - yi) / (yj - yi) + xi) inside = !inside;
                    }
                    if (inside) Set(x, y, c);
                }
        }

        /// <summary>Darkens transparent pixels next to opaque ones: a 1-pixel outline that keeps
        /// sprites readable on any wall.</summary>
        public void Outline(Color32 c)
        {
            var copy = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (copy[y * w + x].a > 0) continue;
                    bool edge = false;
                    for (int d = 0; d < 4 && !edge; d++)
                    {
                        int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (In(nx, ny) && copy[ny * w + nx].a > 128) edge = true;
                    }
                    if (edge) px[y * w + x] = c;
                }
        }

        /// <summary>Copies <paramref name="src"/> in at (dx, dy) (top-left), skipping transparent pixels.</summary>
        public void Blit(Px src, int dx, int dy, bool flipX = false)
        {
            for (int y = 0; y < src.h; y++)
                for (int x = 0; x < src.w; x++)
                {
                    var c = src.px[y * src.w + (flipX ? src.w - 1 - x : x)];
                    if (c.a == 0) continue;
                    Set(dx + x, dy + y, c);
                }
        }

        /// <summary>Bottom-up rows, as Texture2D.SetPixels32 wants.</summary>
        public Color32[] ToUnity()
        {
            var o = new Color32[px.Length];
            for (int y = 0; y < h; y++)
                Array.Copy(px, y * w, o, (h - 1 - y) * w, w);
            return o;
        }
    }
}
