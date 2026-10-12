using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.DreamEscape
{
    /// <summary>What a drawn thing is to the mesh lint.</summary>
    public enum DrawnKind
    {
        /// <summary>Drawn inside its own part's box (built furniture, a stair's treads): it must stay inside it.</summary>
        Owned,
        /// <summary>Fixed to a face (skirting, rails, cornices, casings, frames): it may sit a few cm proud into a room.</summary>
        Mounted,
        /// <summary>A model fitted into its part's box: upright, standing on the box's floor, filling it.</summary>
        Model,
        /// <summary>A thing standing free (a cut-out, a memory, a prop with no box): it must rest on something.</summary>
        Free,
        /// <summary>Hung (a pendant, a picture, a curtain): it must touch its wall, ceiling or rail.</summary>
        Hanging,
    }

    /// <summary>A drawn thing's bounds (the house's frame), what it is and which part owns it (-1: none).</summary>
    public struct HouseDrawn
    {
        public string name;
        public Vector3 min, max;
        public DrawnKind kind;
        public int owner;
        /// <summary>Its bounds are an oriented box's (a slope): overlap tests are skipped, the box is too loose.</summary>
        public bool oriented;
        /// <summary>A model's real size (upright, front +z; zero: unknown) and its fitted size in the same axes.</summary>
        public Vector3 realSize, fittedSize;
    }

    /// <summary>One thing wrong with what was drawn.</summary>
    public sealed class MeshLintIssue
    {
        /// <summary>overlap, outside, floating, sideways, scale, part-floating.</summary>
        public string kind;
        public string what;
        public float amount;
        public override string ToString() => $"[{kind}] {what} ({amount:0.000})";
    }

    /// <summary>
    /// The house's mesh lint (house-1.md: the plan's box lint passed while the rendered meshes lay on their sides,
    /// floated and interpenetrated). It checks what the builder actually drew, by bounds: every drawn thing stays inside
    /// its own part, pokes no more than <see cref="Poke"/> into any other solid or any other drawn model, rests on a
    /// floor or a surface (or, hung, touches its wall or ceiling), and every fitted model stands upright (its proportions
    /// match its real ones) at about its real size. Plan level, every furniture part rests on something or hangs from
    /// something. Pure: the dotnet tests run it on synthetic boxes and on the plan; the rebuild runs it on the scene and
    /// fails loudly (<c>JAMKIT_HOUSE_MESHLINT</c>).
    /// </summary>
    public static class HouseMeshLint
    {
        /// <summary>How far one thing may sit into another (a skirting against a bookcase, a frame on a wall).</summary>
        public const float Poke = 0.02f;
        /// <summary>How near counts as touching (resting on, hung from).</summary>
        public const float Touch = 0.03f;
        /// <summary>A model whose height-to-width ratio is off its real one by more than this lies on its side.</summary>
        public const float UprightTolerance = 0.35f;

        static HouseLint.Obb Box(Vector3 min, Vector3 max) => new HouseLint.Obb
        {
            c = (min + max) * 0.5f, u0 = Vector3.UnitX, u1 = Vector3.UnitY, u2 = Vector3.UnitZ, h = (max - min) * 0.5f, Min = min, Max = max,
        };

        /// <summary>Everything wrong with <paramref name="drawn"/> over <paramref name="parts"/>.</summary>
        public static List<MeshLintIssue> Check(IList<HousePart> parts, IList<HouseDrawn> drawn)
        {
            var issues = new List<MeshLintIssue>();
            var obbs = new HouseLint.Obb[parts.Count];
            for (int i = 0; i < parts.Count; i++) obbs[i] = HouseLint.Of(parts[i].piece, parts[i].rampAlongX);
            for (int i = 0; i < drawn.Count; i++)
            {
                var d = drawn[i];
                if (d.max.X - d.min.X <= 0f && d.max.Y - d.min.Y <= 0f && d.max.Z - d.min.Z <= 0f) continue;
                var db = Box(d.min, d.max);
                // 1. Into a solid that isn't its own.
                if (!d.oriented)
                    for (int j = 0; j < parts.Count; j++)
                    {
                        if (j == d.owner || !parts[j].piece.solid) continue;
                        if (d.kind == DrawnKind.Mounted && parts[j].look == HouseLook.Glass) continue;   // glazing bars, frames
                        float depth = HouseLint.Depth(db, obbs[j]);
                        float allowed = d.kind == DrawnKind.Mounted ? 0.035f : Poke;
                        if (parts[j].piece.kind == LabPieceKind.Ramp && parts[j].look == HouseLook.Roof) allowed = Math.Max(allowed, 0.05f);
                        if (depth > allowed) issues.Add(new MeshLintIssue { kind = "overlap", what = $"\"{d.name}\" ({d.kind}) is {depth:0.000} m into \"{parts[j].piece.name}\"", amount = depth });
                    }
                // 2. Outside its own part.
                if (d.owner >= 0 && (d.kind == DrawnKind.Owned || d.kind == DrawnKind.Model))
                {
                    var o = parts[d.owner].piece;
                    var omin = obbs[d.owner].Min; var omax = obbs[d.owner].Max;
                    float tol = o.kind == LabPieceKind.Ramp ? 0.12f : Poke;
                    float out_ = Math.Max(Math.Max(Math.Max(omin.X - d.min.X, d.max.X - omax.X), Math.Max(omin.Y - d.min.Y, d.max.Y - omax.Y)), Math.Max(omin.Z - d.min.Z, d.max.Z - omax.Z));
                    if (out_ > tol) issues.Add(new MeshLintIssue { kind = "outside", what = $"\"{d.name}\" sticks {out_:0.000} m out of its part \"{o.name}\"", amount = out_ });
                }
                // 3. Resting / hung.
                if (d.kind == DrawnKind.Free || d.kind == DrawnKind.Model || d.kind == DrawnKind.Hanging)
                {
                    bool ok = d.kind == DrawnKind.Hanging ? Touches(parts, obbs, d, -1) : RestsOn(parts, obbs, d);
                    if (d.kind == DrawnKind.Model && !ok && d.owner >= 0)
                    {
                        // A model stands on its box's floor; the box is checked at plan level.
                        var o = parts[d.owner].piece;
                        ok = Math.Abs(d.min.Y - o.Min.Y) <= Touch || Math.Abs(d.max.Y - o.Max.Y) <= Touch && Touches(parts, obbs, d, d.owner);
                    }
                    if (!ok) issues.Add(new MeshLintIssue { kind = "floating", what = $"\"{d.name}\" ({d.kind}) rests on nothing and touches nothing (bottom at {d.min.Y:0.00})", amount = d.min.Y });
                }
                // 4. Upright and about its real size.
                if (d.kind == DrawnKind.Model && d.realSize.LengthSquared() > 1e-6f && d.fittedSize.LengthSquared() > 1e-6f)
                {
                    float real = d.realSize.Y / Math.Max(1e-3f, Math.Max(d.realSize.X, d.realSize.Z));
                    float now = d.fittedSize.Y / Math.Max(1e-3f, Math.Max(d.fittedSize.X, d.fittedSize.Z));
                    float off = Math.Abs(now - real) / Math.Max(1e-3f, Math.Max(now, real));
                    if (off > UprightTolerance) issues.Add(new MeshLintIssue { kind = "sideways", what = $"\"{d.name}\" is {now:0.00} tall to wide; its real shape is {real:0.00}: it lies on its side or its axes are wrong", amount = off });
                    float scale = Math.Max(d.fittedSize.X, Math.Max(d.fittedSize.Y, d.fittedSize.Z)) / Math.Max(1e-3f, Math.Max(d.realSize.X, Math.Max(d.realSize.Y, d.realSize.Z)));
                    if (scale < 0.55f || scale > 1.8f) issues.Add(new MeshLintIssue { kind = "scale", what = $"\"{d.name}\" is drawn at {scale:0.00} of its real size: its box is wrong for it", amount = scale });
                }
            }
            // 5. Drawn models and free things into each other.
            for (int a = 0; a < drawn.Count; a++)
            {
                var da = drawn[a];
                if (da.oriented || da.kind != DrawnKind.Model && da.kind != DrawnKind.Free) continue;
                var ba = Box(da.min, da.max);
                for (int b = a + 1; b < drawn.Count; b++)
                {
                    var dbb = drawn[b];
                    if (dbb.oriented || dbb.kind != DrawnKind.Model && dbb.kind != DrawnKind.Free) continue;
                    if (da.owner >= 0 && da.owner == dbb.owner) continue;
                    float depth = HouseLint.Depth(ba, Box(dbb.min, dbb.max));
                    if (depth > Poke) issues.Add(new MeshLintIssue { kind = "overlap", what = $"\"{da.name}\" and \"{dbb.name}\" are {depth:0.000} m into each other", amount = depth });
                }
            }
            issues.AddRange(PlanParts(parts));
            return issues;
        }

        /// <summary>Plan level: every furniture, cupboard, picture, rug and cloth part rests on a solid's top or hangs
        /// touching one (a pendant from its ceiling, a picture on its wall, the sheets on their line).</summary>
        public static List<MeshLintIssue> PlanParts(IList<HousePart> parts)
        {
            var issues = new List<MeshLintIssue>();
            var obbs = new HouseLint.Obb[parts.Count];
            for (int i = 0; i < parts.Count; i++) obbs[i] = HouseLint.Of(parts[i].piece, parts[i].rampAlongX);
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var look = part.look;
                if (look != HouseLook.Furniture && look != HouseLook.Cupboard && look != HouseLook.Picture && look != HouseLook.Rug && look != HouseLook.Cloth && look != HouseLook.Rail) continue;
                var p = part.piece;
                var d = new HouseDrawn { name = p.name, min = p.Min, max = p.Max, owner = i };
                if (RestsOn(parts, obbs, d) || Touches(parts, obbs, d, i)) continue;
                issues.Add(new MeshLintIssue { kind = "part-floating", what = $"the part \"{p.name}\" ({look}) rests on nothing and touches nothing", amount = p.Min.Y });
            }
            return issues;
        }

        /// <summary>Its bottom is on a solid's top (within <see cref="Touch"/>) somewhere under its footprint.</summary>
        static bool RestsOn(IList<HousePart> parts, HouseLint.Obb[] obbs, HouseDrawn d)
        {
            float cx = (d.min.X + d.max.X) * 0.5f, cz = (d.min.Z + d.max.Z) * 0.5f;
            for (int j = 0; j < parts.Count; j++)
            {
                if (j == d.owner || !parts[j].piece.solid) continue;
                var q = parts[j].piece;
                if (d.max.X <= q.Min.X || d.min.X >= q.Max.X || d.max.Z <= q.Min.Z || d.min.Z >= q.Max.Z) continue;
                float top = q.kind == LabPieceKind.Ramp
                    ? DreamHouse.RampTop(parts[j], Math.Max(q.Min.X, Math.Min(q.Max.X, cx)), Math.Max(q.Min.Z, Math.Min(q.Max.Z, cz)))
                    : q.Max.Y;
                if (Math.Abs(top - d.min.Y) <= Touch) return true;
            }
            return false;
        }

        /// <summary>Its bounds, grown by <see cref="Touch"/>, meet a solid other than <paramref name="except"/>.</summary>
        static bool Touches(IList<HousePart> parts, HouseLint.Obb[] obbs, HouseDrawn d, int except)
        {
            var grown = Box(d.min - new Vector3(Touch), d.max + new Vector3(Touch));
            for (int j = 0; j < parts.Count; j++)
            {
                if (j == except || j == d.owner || !parts[j].piece.solid) continue;
                if (HouseLint.Depth(grown, obbs[j]) > 0f) return true;
            }
            return false;
        }
    }
}
