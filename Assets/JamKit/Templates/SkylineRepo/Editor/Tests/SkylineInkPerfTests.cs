using System.Collections.Generic;
using JamKit.SkylineRepo.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the ink pass's cost work and its optional stylization (docs/SKYLINE-STYLE.md, "How it's built" and
    /// "Optional stylization"): the occupancy-skip maths (which block counts make "no occupancy here" a safe reason to skip the
    /// 24-tap outline search), the switch parser, the volume's defaults (every stylization off) and the composite shader's passes.
    /// </summary>
    public class SkylineInkPerfTests
    {
        [Test]
        public void TheOccupancySizeRoundsUpAndNeverReachesZero()
        {
            Assert.That(SkylineInkMath.OccupancySize(1920), Is.EqualTo(480));
            Assert.That(SkylineInkMath.OccupancySize(1080), Is.EqualTo(270));
            Assert.That(SkylineInkMath.OccupancySize(1081), Is.EqualTo(271));
            Assert.That(SkylineInkMath.OccupancySize(1), Is.EqualTo(1));
            Assert.That(SkylineInkMath.OccupancySize(0), Is.EqualTo(1));
        }

        [Test]
        public void TheDilationCoversTheOutlineRadiusAtEveryHeight()
        {
            // The skip is only safe if blocks * 4 pixels reach the whole search radius (a pixel is up to 3 px from its block's far edge,
            // so k blocks reach at least 4k px): check a spread of widths and render heights.
            foreach (float width in new[] { 1f, 2f, 4f, 6f, 8f })
            {
                foreach (float height in new[] { 540f, 720f, 1080f, 1440f, 2160f })
                {
                    float radius = SkylineInkMath.OutlineRadiusPx(width, height);
                    int k = SkylineInkMath.DilationBlocks(radius);
                    Assert.That(k * SkylineInkMath.OccupancyBlock, Is.GreaterThanOrEqualTo(radius - 1e-3f), "width " + width + " at " + height);
                    Assert.That(k, Is.GreaterThanOrEqualTo(1));
                    Assert.That((k - 1) * SkylineInkMath.OccupancyBlock, Is.LessThan(radius + 1e-3f), "no wasted block: width " + width + " at " + height);
                }
            }
        }

        [Test]
        public void TheStyleSheetsOutlineAt1080pNeedsOneBlockAndNineLoads()
        {
            int k = SkylineInkMath.DilationBlocks(SkylineInkMath.OutlineRadiusPx(4f, 1080f));
            Assert.That(k, Is.EqualTo(1));
            Assert.That(SkylineInkMath.DilationLoads(k), Is.EqualTo(9));
            Assert.That(SkylineInkMath.DilationLoads(2), Is.EqualTo(25));
        }

        [Test]
        public void TheOccupancyWorkIsAnOrderOfMagnitudeBelowTheOldSearch()
        {
            long old = SkylineInkMath.OldMaskTapsPerFrame(1920, 1080);
            long now = SkylineInkMath.NewOccupancyReadsPerFrame(1920, 1080, 1);
            Assert.That(old, Is.EqualTo(1920L * 1080L * 24L));
            Assert.That(now * 10, Is.LessThan(old), "the occupancy passes plus one load per pixel cost under a tenth of the old taps");
        }

        [Test]
        public void ThePixelAngleShrinksWithResolutionAndMatchesTheGeometry()
        {
            // a 60 degree vertical field: m11 = 1 / tan(30 deg); 1080 px => one pixel spans about 2 tan(30) / 1080 rad
            float m11 = 1f / Mathf.Tan(30f * Mathf.Deg2Rad);
            Assert.That(SkylineInkMath.PixelAngle(m11, 1080f), Is.EqualTo(2f * Mathf.Tan(30f * Mathf.Deg2Rad) / 1080f).Within(1e-6f));
            Assert.That(SkylineInkMath.PixelAngle(m11, 2160f), Is.LessThan(SkylineInkMath.PixelAngle(m11, 1080f)));
            Assert.That(float.IsInfinity(SkylineInkMath.PixelAngle(0f, 1080f)), Is.False, "a degenerate matrix does not divide by zero");
        }

        [Test]
        public void TheWobbleAmplitudeScalesWithTheHeightAndClamps()
        {
            Assert.That(SkylineInkMath.WobbleAmplitudePx(1f, 1080f), Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(SkylineInkMath.WobbleAmplitudePx(1f, 2160f), Is.EqualTo(2.4f).Within(1e-5f));
            Assert.That(SkylineInkMath.WobbleAmplitudePx(5f, 1080f), Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(SkylineInkMath.WobbleAmplitudePx(0f, 1080f), Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void TheSwitchParsesNamesStrengthsAndAll()
        {
            var unknown = new List<string>();
            var s = SkylineInkStyle.Parse("halftone,wobble=0.5, Paper-Grain=2 ,bogus", unknown);
            Assert.That(s.halftone, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(s.wobble, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(s.grain, Is.EqualTo(1f).Within(1e-5f), "clamped to 1");
            Assert.That(s.lineWeight, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(s.posterize, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(unknown, Is.EqualTo(new[] { "bogus" }));

            var all = SkylineInkStyle.Parse("all", null);
            Assert.That(new[] { all.lineWeight, all.halftone, all.wobble, all.posterize, all.grain }, Is.All.EqualTo(1f).Within(1e-5f));
            Assert.That(SkylineInkStyle.Parse("all,none", null).Any, Is.False);
            Assert.That(SkylineInkStyle.Parse("", null).Any, Is.False);
            Assert.That(SkylineInkStyle.Parse("line_weight=0.25", null).lineWeight, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void EveryStylizationIsOffByDefault()
        {
            var ink = ScriptableObject.CreateInstance<SkylineInk>();
            try
            {
                Assert.That(SkylineInkStyle.FromVolume(ink).Any, Is.False);
                Assert.That(ink.creasesFromDepth.value, Is.False, "the original normal-based creases until the creator picks");
            }
            finally { Object.DestroyImmediate(ink); }
        }

        [Test]
        public void TheSwitchRaisesTheVolumeNeverLowersIt()
        {
            var ink = ScriptableObject.CreateInstance<SkylineInk>();
            try
            {
                ink.halftoneDots.Override(0.8f);
                SkylineInkStyle.SetCommandLineForTest("halftone=0.3,grain", 0);
                var s = SkylineInkStyle.Resolve(ink);
                Assert.That(s.halftone, Is.EqualTo(0.8f).Within(1e-5f), "the larger of the two wins");
                Assert.That(s.grain, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(SkylineInkStyle.UseDepthCreases(ink), Is.False);
                SkylineInkStyle.SetCommandLineForTest("", 1);
                Assert.That(SkylineInkStyle.UseDepthCreases(ink), Is.True, "-skyline-ink-depth-creases forces it");
                SkylineInkStyle.SetCommandLineForTest("", -1);
                ink.creasesFromDepth.Override(true);
                Assert.That(SkylineInkStyle.UseDepthCreases(ink), Is.False, "-skyline-ink-normal-creases forces the other way");
            }
            finally
            {
                SkylineInkStyle.SetCommandLineForTest("", 0);
                Object.DestroyImmediate(ink);
            }
        }

        [Test]
        public void TheCompositeShaderHasItsThreePasses()
        {
            var shader = Shader.Find(SkylineInkSetup.CompositeShader);
            Assert.That(shader, Is.Not.Null);
            var m = new Material(shader);
            try
            {
                Assert.That(m.passCount, Is.EqualTo(3));
                Assert.That(m.FindPass("Skyline Ink Composite"), Is.EqualTo(0), "the feature draws pass 0 as the composite");
                Assert.That(m.FindPass("Skyline Ink Occupancy"), Is.EqualTo(1));
                Assert.That(m.FindPass("Skyline Ink Occupancy Dilate"), Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void TheLitShaderReadsTheStylizationGlobalNotAKeyword()
        {
            // The invisible-bots lesson (docs/SKYLINE-PLAYTEST-1.md): a keyword a material turns on at run time must be saved on a
            // build-included material. The stylization reaches the lit shader as a global vector, so there is no keyword to lose.
            string src = System.IO.File.ReadAllText("Assets/JamKit/Templates/SkylineRepo/Look/Ink/InkLit.shader");
            Assert.That(src, Does.Contain("_SkInkLitStyle"));
            Assert.That(src, Does.Not.Contain("_SKYLINE_INK_STYLE"));
            Assert.That(src, Does.Not.Contain("shader_feature_local _HALFTONE"));
        }
    }
}
