using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The house's geometry lint (pure; the dotnet tests run it, the builder logs it): no two solids interpenetrate. Boxes
    /// are boxes; ramps (stairs, roof slopes) are the rotated slab the builder makes of them (<see cref="RampThickness"/>
    /// under the slope). A few meetings are how buildings are built and are allowed up to a small depth: a stair's end on
    /// the floor it lands on, roof slopes meeting at the ridge, a roof's slab over the top of a wall, steps bedded in the
    /// lawn. Everything else must not overlap by more than a millimetre.
    /// </summary>
    public static class HouseLint
    {
        public const float RampThickness = 0.3f;
        public const float Tolerance = 0.002f;

        public struct Obb
        {
            public Vector3 c, u0, u1, u2, h;
            public Vector3 Min, Max;
        }

        public struct Hit
        {
            public int a, b;
            public float depth;
            public override string ToString() => $"{a}/{b} {depth:0.000} m";
        }

        public static Obb Of(LabPiece p, bool rampAlongX)
        {
            var o = new Obb();
            if (p.kind != LabPieceKind.Ramp)
            {
                o.c = p.center; o.u0 = Vector3.UnitX; o.u1 = Vector3.UnitY; o.u2 = Vector3.UnitZ; o.h = p.size * 0.5f;
                o.Min = p.Min; o.Max = p.Max;
                return o;
            }
            float run = rampAlongX ? p.size.X : p.size.Z, width = rampAlongX ? p.size.Z : p.size.X;
            float rise = p.rampTopEnd - p.rampTopStart, len = (float)Math.Sqrt(run * run + rise * rise);
            var along = rampAlongX ? new Vector3(run, rise, 0f) / len : new Vector3(0f, rise, run) / len;
            var side = rampAlongX ? Vector3.UnitZ : Vector3.UnitX;
            var n = Vector3.Normalize(Vector3.Cross(rampAlongX ? along : side, rampAlongX ? side : along));
            if (n.Y < 0f) n = -n;
            var topMid = new Vector3(p.center.X, (p.rampTopStart + p.rampTopEnd) * 0.5f, p.center.Z);
            o.c = topMid - n * (RampThickness * 0.5f);
            o.u0 = side; o.u1 = along; o.u2 = n;
            o.h = new Vector3(width * 0.5f, len * 0.5f, RampThickness * 0.5f);
            var ext = new Vector3(
                Math.Abs(o.u0.X) * o.h.X + Math.Abs(o.u1.X) * o.h.Y + Math.Abs(o.u2.X) * o.h.Z,
                Math.Abs(o.u0.Y) * o.h.X + Math.Abs(o.u1.Y) * o.h.Y + Math.Abs(o.u2.Y) * o.h.Z,
                Math.Abs(o.u0.Z) * o.h.X + Math.Abs(o.u1.Z) * o.h.Y + Math.Abs(o.u2.Z) * o.h.Z);
            o.Min = o.c - ext; o.Max = o.c + ext;
            return o;
        }

        /// <summary>How deep two oriented boxes overlap (the smallest overlap over the separating axes); 0 or less: apart.</summary>
        public static float Depth(in Obb a, in Obb b)
        {
            if (a.Max.X <= b.Min.X || b.Max.X <= a.Min.X || a.Max.Y <= b.Min.Y || b.Max.Y <= a.Min.Y || a.Max.Z <= b.Min.Z || b.Max.Z <= a.Min.Z) return 0f;
            var axes = new Vector3[15];
            axes[0] = a.u0; axes[1] = a.u1; axes[2] = a.u2; axes[3] = b.u0; axes[4] = b.u1; axes[5] = b.u2;
            int k = 6;
            Vector3[] ua = { a.u0, a.u1, a.u2 }, ub = { b.u0, b.u1, b.u2 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var x = Vector3.Cross(ua[i], ub[j]);
                    axes[k++] = x.LengthSquared() > 1e-8f ? Vector3.Normalize(x) : Vector3.Zero;
                }
            float best = float.MaxValue;
            var d = b.c - a.c;
            for (int i = 0; i < 15; i++)
            {
                var L = axes[i];
                if (L == Vector3.Zero) continue;
                float ra = Math.Abs(Vector3.Dot(a.u0, L)) * a.h.X + Math.Abs(Vector3.Dot(a.u1, L)) * a.h.Y + Math.Abs(Vector3.Dot(a.u2, L)) * a.h.Z;
                float rb = Math.Abs(Vector3.Dot(b.u0, L)) * b.h.X + Math.Abs(Vector3.Dot(b.u1, L)) * b.h.Y + Math.Abs(Vector3.Dot(b.u2, L)) * b.h.Z;
                float overlap = ra + rb - Math.Abs(Vector3.Dot(d, L));
                if (overlap <= 0f) return 0f;
                if (overlap < best) best = overlap;
            }
            return best;
        }

        /// <summary>The allowed depth for two looks meeting (see the class summary).</summary>
        public static float Allowed(HouseLook a, HouseLook b, LabPiece pa, LabPiece pb)
        {
            bool rampA = pa.kind == LabPieceKind.Ramp, rampB = pb.kind == LabPieceKind.Ramp;
            if (!rampA && !rampB) return Tolerance;
            HouseLook ramp = rampA ? a : b, other = rampA ? b : a;
            var otherPiece = rampA ? pb : pa;
            if (rampA && rampB) return ramp == HouseLook.Roof && other == HouseLook.Roof ? 0.45f : Tolerance;
            // A chimney goes up through the roof (the roof is flashed round it).
            if (ramp == HouseLook.Roof && other == HouseLook.Chimney) return float.MaxValue;
            if (ramp == HouseLook.Roof && (other == HouseLook.Wall || other == HouseLook.Roof)) return 0.45f;
            // A step's slab butting against the wall it climbs to (its end is square to its slope: its corner sits in the wall).
            if (otherPiece.kind == LabPieceKind.Wall && ramp != HouseLook.Roof) return 0.12f;
            // A ramp's square ends bed into the floors it runs between.
            if (otherPiece.kind == LabPieceKind.Floor && ramp != HouseLook.Roof) return 0.4f;
            // A stair's ends bed into the floor it starts on and the floor it lands on.
            if (other == HouseLook.Slab || other == HouseLook.Lawn || other == HouseLook.Path || other == HouseLook.Porch || other == HouseLook.Wood && otherPiece.kind == LabPieceKind.Floor) return 0.4f;
            return Tolerance;
        }

        /// <summary>Every pair of solid parts that overlap more than allowed.</summary>
        public static List<Hit> Check(IList<HousePart> parts)
        {
            var obbs = new Obb[parts.Count];
            for (int i = 0; i < parts.Count; i++) obbs[i] = Of(parts[i].piece, parts[i].rampAlongX);
            var hits = new List<Hit>();
            for (int a = 0; a < parts.Count; a++)
            {
                if (!parts[a].piece.solid) continue;
                for (int b = a + 1; b < parts.Count; b++)
                {
                    if (!parts[b].piece.solid) continue;
                    float depth = Depth(obbs[a], obbs[b]);
                    if (depth <= Allowed(parts[a].look, parts[b].look, parts[a].piece, parts[b].piece)) continue;
                    hits.Add(new Hit { a = a, b = b, depth = depth });
                }
            }
            return hits;
        }

        public static string Describe(IList<HousePart> parts, Hit h) =>
            $"\"{parts[h.a].piece.name}\" ({parts[h.a].look}) and \"{parts[h.b].piece.name}\" ({parts[h.b].look}) overlap {h.depth:0.000} m";
    }
}
