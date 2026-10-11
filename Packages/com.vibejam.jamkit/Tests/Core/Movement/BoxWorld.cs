using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Tests.Movement
{
    /// <summary>
    /// A fake world for the movement tests: axis-aligned boxes and ramps (a box whose top slopes
    /// along Z, or along X for a roof pitch), with a capsule collide-and-slide by sub-stepped penetration resolution, and the
    /// probes <see cref="IMoveWorld"/> asks for. Close enough to Unity's CharacterController for the
    /// rules (not for exact positions).
    /// </summary>
    public sealed class BoxWorld : IMoveWorld
    {
        public sealed class Box
        {
            public Vector3 min, max;
            public bool runnable = true;
            public bool enabled = true;
            /// <summary>Ramp: the top goes from <see cref="rampTopStart"/> at min.Z to <see cref="rampTopEnd"/> at max.Z (max.Y is the higher one).</summary>
            public bool ramp;
            /// <summary>The ramp slopes along X instead (from min.X to max.X): a roof pitch beside the route.</summary>
            public bool rampAlongX;
            public float rampTopStart, rampTopEnd;
            public int id;

            public float TopAt(float z) => TopAt(0f, z);

            public float TopAt(float x, float z)
            {
                if (!ramp) return max.Y;
                float t = rampAlongX ? (x - min.X) / Math.Max(1e-4f, max.X - min.X) : (z - min.Z) / Math.Max(1e-4f, max.Z - min.Z);
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                return rampTopStart + (rampTopEnd - rampTopStart) * t;
            }

            public Vector3 RampNormal
            {
                get
                {
                    if (rampAlongX)
                    {
                        float lx = max.X - min.X;
                        return Vector3.Normalize(new Vector3(-(rampTopEnd - rampTopStart), lx, 0f));
                    }
                    float len = max.Z - min.Z;
                    var n = new Vector3(0f, len, -(rampTopEnd - rampTopStart));
                    return Vector3.Normalize(n);
                }
            }
        }

        public readonly List<Box> Boxes = new List<Box>();
        /// <summary>Like a CharacterController's skin width: the capsule keeps this gap from what it touches.</summary>
        public float skin;

        public Box Add(Vector3 center, Vector3 size, bool runnable = true)
        {
            var b = new Box { min = center - size * 0.5f, max = center + size * 0.5f, runnable = runnable, id = Boxes.Count + 1 };
            Boxes.Add(b);
            return b;
        }

        /// <summary>A floor slab whose top is at <paramref name="top"/>, from z0 to z1, x0 to x1, 1 m thick.</summary>
        public Box Floor(float x0, float x1, float z0, float z1, float top, float thickness = 1f) =>
            Add(new Vector3((x0 + x1) * 0.5f, top - thickness * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, thickness, z1 - z0));

        public Box Ramp(float x0, float x1, float z0, float z1, float topStart, float topEnd, float bottom)
        {
            var b = new Box
            {
                min = new Vector3(x0, bottom, z0),
                max = new Vector3(x1, Math.Max(topStart, topEnd), z1),
                ramp = true, rampTopStart = topStart, rampTopEnd = topEnd, runnable = false, id = Boxes.Count + 1,
            };
            Boxes.Add(b);
            return b;
        }

        /// <summary>A ramp whose top slopes along X, from <paramref name="topStart"/> at x0 to <paramref name="topEnd"/> at x1.</summary>
        public Box RampX(float x0, float x1, float z0, float z1, float topStart, float topEnd, float bottom)
        {
            var b = Ramp(x0, x1, z0, z1, topStart, topEnd, bottom);
            b.rampAlongX = true;
            return b;
        }

        // ---------- IMoveWorld ----------

        public Vector3 Move(Vector3 feet, Vector3 delta, float radius, float height, Vector3[] normals, out int hitCount)
        {
            hitCount = 0;
            float len = delta.Length();
            int sub = Math.Max(1, Math.Min(64, (int)Math.Ceiling(len / (radius * 0.4f))));
            Vector3 step = delta / sub;
            for (int s = 0; s < sub; s++)
            {
                feet += step;
                for (int iter = 0; iter < 4; iter++)
                {
                    bool any = false;
                    foreach (var b in Boxes)
                    {
                        if (!b.enabled) continue;
                        if (!Penetration(b, feet, radius + skin, height, out var n, out float depth) || depth < 1e-5f) continue;
                        feet += n * depth;
                        step = MoveMath.Clip(step, n);
                        AddNormal(normals, ref hitCount, n);
                        any = true;
                    }
                    if (!any) break;
                }
            }
            return feet;
        }

        static void AddNormal(Vector3[] normals, ref int count, Vector3 n)
        {
            for (int i = 0; i < count && i < normals.Length; i++)
                if (Vector3.Dot(normals[i], n) > 0.99f) return;
            if (count < normals.Length) normals[count] = n;
            count++;
            if (count > normals.Length) count = normals.Length;
        }

        public bool CastDown(Vector3 feet, float radius, float maxDistance, out SurfaceHit hit)
        {
            hit = default;
            bool found = false;
            float best = float.MaxValue;
            foreach (var b in Boxes)
            {
                if (!b.enabled) continue;
                float cx = Clamp(feet.X, b.min.X, b.max.X), cz = Clamp(feet.Z, b.min.Z, b.max.Z);
                float dx = feet.X - cx, dz = feet.Z - cz;
                if (dx * dx + dz * dz > radius * radius) continue;
                float top = b.TopAt(cx, cz);
                float d = feet.Y - top;
                if (d < -0.05f || d > maxDistance) continue;
                if (d < best)
                {
                    best = d;
                    hit = new SurfaceHit { point = new Vector3(cx, top, cz), normal = b.ramp ? b.RampNormal : MoveMath.Up, distance = Math.Max(0f, d), id = b.id, runnable = b.runnable };
                    found = true;
                }
            }
            return found;
        }

        public bool CastWall(Vector3 origin, Vector3 direction, float radius, float maxDistance, out SurfaceHit hit)
        {
            hit = default;
            bool found = false;
            float best = float.MaxValue;
            foreach (var b in Boxes)
            {
                if (!b.enabled || b.ramp) continue;
                Vector3 mn = b.min - new Vector3(radius), mx = b.max + new Vector3(radius);
                if (!RayBox(origin, direction, mn, mx, out float t, out Vector3 n)) continue;
                if (t > maxDistance || t >= best) continue;
                best = t;
                Vector3 p = origin + direction * t - n * radius;
                hit = new SurfaceHit { point = p, normal = n, distance = t, id = b.id, runnable = b.runnable };
                found = true;
            }
            return found;
        }

        public bool CastLedge(Vector3 origin, float maxDistance, out SurfaceHit hit)
        {
            hit = default;
            bool found = false;
            float bestTop = float.MinValue;
            foreach (var b in Boxes)
            {
                if (!b.enabled) continue;
                if (origin.X < b.min.X || origin.X > b.max.X || origin.Z < b.min.Z || origin.Z > b.max.Z) continue;
                float top = b.TopAt(origin.X, origin.Z);
                if (top > origin.Y || origin.Y - top > maxDistance) continue;
                if (top > bestTop)
                {
                    bestTop = top;
                    hit = new SurfaceHit { point = new Vector3(origin.X, top, origin.Z), normal = b.ramp ? b.RampNormal : MoveMath.Up, distance = origin.Y - top, id = b.id, runnable = b.runnable };
                    found = true;
                }
            }
            return found;
        }

        public bool CapsuleFits(Vector3 feet, float radius, float height)
        {
            foreach (var b in Boxes)
                if (b.enabled && Penetration(b, feet, radius, height, out _, out float d) && d > 0.01f) return false;
            return true;
        }

        // ---------- Geometry ----------

        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;

        static bool Penetration(Box b, Vector3 feet, float r, float height, out Vector3 n, out float depth)
        {
            n = default;
            depth = 0f;
            float a = feet.Y + r, top = feet.Y + Math.Max(height, 2f * r) - r;
            if (b.ramp)
            {
                // Solid under a sloped top: only the top surface pushes (up the slope's normal).
                if (b.rampAlongX)
                {
                    if (feet.Z < b.min.Z - r * 0.5f || feet.Z > b.max.Z + r * 0.5f || feet.X < b.min.X || feet.X > b.max.X) return false;
                }
                else if (feet.X < b.min.X - r * 0.5f || feet.X > b.max.X + r * 0.5f || feet.Z < b.min.Z || feet.Z > b.max.Z) return false;
                float surf = b.TopAt(feet.X, feet.Z);
                if (feet.Y >= surf || feet.Y < b.min.Y) return false;
                n = MoveMath.Up;
                depth = surf - feet.Y;
                return true;
            }
            float qx = Clamp(feet.X, b.min.X, b.max.X), qz = Clamp(feet.Z, b.min.Z, b.max.Z);
            float segY;
            if (top < b.min.Y) segY = top;
            else if (a > b.max.Y) segY = a;
            else segY = Clamp((a + top) * 0.5f, Math.Max(a, b.min.Y), Math.Min(top, b.max.Y));
            float qy = Clamp(segY, b.min.Y, b.max.Y);
            var d = new Vector3(feet.X - qx, segY - qy, feet.Z - qz);
            float dist = d.Length();
            if (dist > 1e-5f)
            {
                if (dist >= r) return false;
                n = d / dist;
                depth = r - dist;
                return true;
            }
            // The axis is inside the box: push out the shortest way.
            float px = b.max.X - feet.X + r, nx = feet.X - b.min.X + r;
            float pz = b.max.Z - feet.Z + r, nz = feet.Z - b.min.Z + r;
            float py = b.max.Y - a + r, ny = top + r - b.min.Y;
            depth = px; n = new Vector3(1, 0, 0);
            if (nx < depth) { depth = nx; n = new Vector3(-1, 0, 0); }
            if (pz < depth) { depth = pz; n = new Vector3(0, 0, 1); }
            if (nz < depth) { depth = nz; n = new Vector3(0, 0, -1); }
            if (py < depth) { depth = py; n = new Vector3(0, 1, 0); }
            if (ny < depth) { depth = ny; n = new Vector3(0, -1, 0); }
            return true;
        }

        static bool RayBox(Vector3 o, Vector3 d, Vector3 mn, Vector3 mx, out float tHit, out Vector3 normal)
        {
            tHit = 0f;
            normal = default;
            float tmin = float.MinValue, tmax = float.MaxValue;
            Vector3 nmin = default;
            for (int axis = 0; axis < 3; axis++)
            {
                float oo = axis == 0 ? o.X : axis == 1 ? o.Y : o.Z;
                float dd = axis == 0 ? d.X : axis == 1 ? d.Y : d.Z;
                float lo = axis == 0 ? mn.X : axis == 1 ? mn.Y : mn.Z;
                float hi = axis == 0 ? mx.X : axis == 1 ? mx.Y : mx.Z;
                if (Math.Abs(dd) < 1e-8f)
                {
                    if (oo < lo || oo > hi) return false;
                    continue;
                }
                float t1 = (lo - oo) / dd, t2 = (hi - oo) / dd;
                Vector3 n1 = Axis(axis, -1f), n2 = Axis(axis, 1f);
                if (t1 > t2) { float tt = t1; t1 = t2; t2 = tt; var nn = n1; n1 = n2; n2 = nn; }
                if (t1 > tmin) { tmin = t1; nmin = n1; }
                if (t2 < tmax) tmax = t2;
                if (tmin > tmax) return false;
            }
            if (tmax < 0f) return false;
            if (tmin < 0f) { tHit = 0f; normal = -Vector3.Normalize(MoveMath.Flat(d)); return true; }
            tHit = tmin;
            normal = nmin;
            return true;
        }

        static Vector3 Axis(int axis, float s) => axis == 0 ? new Vector3(s, 0, 0) : axis == 1 ? new Vector3(0, s, 0) : new Vector3(0, 0, s);
    }
}
