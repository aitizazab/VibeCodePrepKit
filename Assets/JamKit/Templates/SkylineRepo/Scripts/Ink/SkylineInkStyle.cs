using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The stylization strengths (0 = off .. 1 = full) the ink pass applies, whether they come from the
    /// <see cref="SkylineInk"/> volume or from the <c>-skyline-ink-style</c> switch.</summary>
    public struct SkylineInkStyleSettings
    {
        public float lineWeight, halftone, wobble, posterize, grain;

        public bool Any => lineWeight > 0f || halftone > 0f || wobble > 0f || posterize > 0f || grain > 0f;

        public static SkylineInkStyleSettings Max(SkylineInkStyleSettings a, SkylineInkStyleSettings b)
        {
            return new SkylineInkStyleSettings
            {
                lineWeight = Mathf.Max(a.lineWeight, b.lineWeight),
                halftone = Mathf.Max(a.halftone, b.halftone),
                wobble = Mathf.Max(a.wobble, b.wobble),
                posterize = Mathf.Max(a.posterize, b.posterize),
                grain = Mathf.Max(a.grain, b.grain),
            };
        }
    }

    /// <summary>
    /// The stylization options on top of the style sheet's comic ink (docs/SKYLINE-STYLE.md, "Optional stylization"), all off by
    /// default, and the switches that turn them on for a capture:
    /// <c>-skyline-ink-style lineweight,halftone,wobble,posterize,grain</c> (each optionally <c>=strength</c>, default 1; <c>all</c>;
    /// <c>none</c>) and <c>-skyline-ink-depth-creases</c> / <c>-skyline-ink-normal-creases</c> to force the crease source for a
    /// perf A/B. The switches never write to the volume: they raise the strengths the pass uses (the larger of the two wins).
    /// </summary>
    public static class SkylineInkStyle
    {
        public const string Switch = "-skyline-ink-style";
        public const string DepthCreasesSwitch = "-skyline-ink-depth-creases";
        public const string NormalCreasesSwitch = "-skyline-ink-normal-creases";

        public static readonly string[] Names = { "lineweight", "halftone", "wobble", "posterize", "grain" };

        /// <summary>Parses a switch value such as <c>halftone,wobble=0.5</c>. Unknown names are added to <paramref name="unknown"/>.</summary>
        public static SkylineInkStyleSettings Parse(string value, List<string> unknown)
        {
            var s = new SkylineInkStyleSettings();
            if (string.IsNullOrWhiteSpace(value)) return s;
            foreach (var raw in value.Split(new[] { ',', ';', '+' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string part = raw.Trim().ToLowerInvariant();
                float strength = 1f;
                int eq = part.IndexOf('=');
                if (eq >= 0)
                {
                    if (!float.TryParse(part.Substring(eq + 1), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out strength)) strength = 1f;
                    part = part.Substring(0, eq);
                }
                strength = Mathf.Clamp01(strength);
                part = part.Replace("-", "").Replace("_", "");
                switch (part)
                {
                    case "lineweight": s.lineWeight = strength; break;
                    case "halftone": s.halftone = strength; break;
                    case "wobble": s.wobble = strength; break;
                    case "posterize": s.posterize = strength; break;
                    case "grain": case "papergrain": s.grain = strength; break;
                    case "all": s = new SkylineInkStyleSettings { lineWeight = strength, halftone = strength, wobble = strength, posterize = strength, grain = strength }; break;
                    case "none": s = new SkylineInkStyleSettings(); break;
                    default: if (unknown != null) unknown.Add(part); break;
                }
            }
            return s;
        }

        /// <summary>The volume's strengths.</summary>
        public static SkylineInkStyleSettings FromVolume(SkylineInk ink)
        {
            if (ink == null) return new SkylineInkStyleSettings();
            return new SkylineInkStyleSettings
            {
                lineWeight = ink.lineWeightVariation.value,
                halftone = ink.halftoneDots.value,
                wobble = ink.handWobble.value,
                posterize = ink.posterize.value,
                grain = ink.paperGrain.value,
            };
        }

        static bool _read;
        static SkylineInkStyleSettings _cli;
        static int _creaseForce;   // 0 the volume decides, 1 depth curvature, -1 normals

        /// <summary>Reads the command line once (a test sets the state with <see cref="SetCommandLineForTest"/> instead).</summary>
        static void ReadOnce()
        {
            if (_read) return;
            _read = true;
            string v = SkylineRunArgs.Value(Switch);
            var unknown = new List<string>();
            _cli = Parse(v, unknown);
            if (unknown.Count > 0) Debug.LogWarning("SKYLINE_INK unknown " + Switch + " names: " + string.Join(",", unknown) + " (known: " + string.Join(",", Names) + ", all, none)");
            if (SkylineRunArgs.Has(DepthCreasesSwitch)) _creaseForce = 1;
            else if (SkylineRunArgs.Has(NormalCreasesSwitch)) _creaseForce = -1;
        }

        public static void SetCommandLineForTest(string styleValue, int creaseForce)
        {
            _read = true;
            _cli = Parse(styleValue, null);
            _creaseForce = creaseForce;
        }

        public static void ResetCommandLineForTest() { _read = false; _cli = default(SkylineInkStyleSettings); _creaseForce = 0; }

        /// <summary>What the pass uses for this camera: the larger of the volume's and the switch's strengths.</summary>
        public static SkylineInkStyleSettings Resolve(SkylineInk ink)
        {
            ReadOnce();
            var s = SkylineInkStyleSettings.Max(FromVolume(ink), _cli);
            if (SkylinePerfSwitches.NoLineWeight) s.lineWeight = 0f;   // measurement only: -skyline-no-lineweight
            return s;
        }

        /// <summary>True when the creases come from the depth buffer's curvature (no normals prepass).</summary>
        public static bool UseDepthCreases(SkylineInk ink)
        {
            ReadOnce();
            if (_creaseForce != 0) return _creaseForce > 0;
            return ink != null && ink.creasesFromDepth.value;
        }
    }
}
