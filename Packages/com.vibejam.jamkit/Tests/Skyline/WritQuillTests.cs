using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Writ's back (docs/SKYLINE-GUN-WRIT.md, "The back: inkwell and quill"): the ink is the ammo, the quill's
    /// poses and flicks, and the ADS sight line staying clear of the quill. Pure maths (<see cref="WritQuill"/>); the model's
    /// own constants are checked against tools/skyline/guns/writ/model/build_writ.py when the repo is on disk.</summary>
    public class WritQuillTests
    {
        const float Eps = 1e-4f;
        // the Writ's sights in the gun's frame (build_writ.py: SightRear, SightFront; SkylineViewmodel.WritRearSight/FrontSight)
        static readonly Vector3 Rear = new Vector3(0f, 0.088f, -0.079f), Front = new Vector3(0f, 0.088f, 0.202f);
        const float SightDistance = 0.13f;                      // SkylineViewmodel.WritSightDistance: the eye to the rear sight
        const float WindowRingRadius = 0.0168f, WindowRingZ = -0.0865f;   // the brass ring round the window (build_writ.py)
        // SkylineViewmodel's hip rest (WritRest, WritRestEuler)
        static readonly Vector3 HipRest = new Vector3(0.2f, -0.13f, 0.5f);
        const float HipYawDegrees = -9f;

        // ---------------------------------------------------------------- the ink is the ammo

        [Test]
        public void TheInkLevelFollowsTheMagazine()
        {
            float full = WritQuill.InkScale(WritQuill.InkTarget(8, 8, WritQuill.InkReload.None));
            float half = WritQuill.InkScale(WritQuill.InkTarget(4, 8, WritQuill.InkReload.None));
            float empty = WritQuill.InkScale(WritQuill.InkTarget(0, 8, WritQuill.InkReload.None));
            Assert.That(full, Is.EqualTo(1f).Within(Eps), "a full magazine is a full well");
            Assert.That(empty, Is.EqualTo(WritQuill.InkEmpty).Within(Eps), "an empty magazine is a thin film, never nothing");
            Assert.That(WritQuill.InkEmpty, Is.GreaterThan(0f));
            Assert.That(half, Is.EqualTo((1f + WritQuill.InkEmpty) / 2f).Within(Eps), "half a magazine is half the well");
            Assert.That(WritQuill.InkScale(-1f), Is.EqualTo(WritQuill.InkEmpty).Within(Eps));
            Assert.That(WritQuill.InkScale(3f), Is.EqualTo(1f).Within(Eps));
            Assert.That(WritQuill.InkTarget(3, 0, WritQuill.InkReload.None), Is.EqualTo(1f), "no magazine size: full, no divide by zero");
        }

        [Test]
        public void TheInkRefillsOnTheSeatAndSinksWhileTheDrumIsOut()
        {
            // the Writ's partial reload: mag_out 0.09, seat 0.565
            Assert.That(WritQuill.ReloadStage(0.05f, 0.09f, 0.565f), Is.EqualTo(WritQuill.InkReload.None));
            Assert.That(WritQuill.ReloadStage(0.3f, 0.09f, 0.565f), Is.EqualTo(WritQuill.InkReload.DrumOut));
            Assert.That(WritQuill.ReloadStage(0.565f, 0.09f, 0.565f), Is.EqualTo(WritQuill.InkReload.Seated));
            Assert.That(WritQuill.InkTarget(5, 8, WritQuill.InkReload.None), Is.EqualTo(5f / 8f).Within(Eps));
            Assert.That(WritQuill.InkTarget(5, 8, WritQuill.InkReload.DrumOut), Is.EqualTo(WritQuill.InkDrained).Within(Eps), "drum out: the well drains");
            Assert.That(WritQuill.InkTarget(0, 8, WritQuill.InkReload.DrumOut), Is.EqualTo(0f).Within(Eps), "already empty stays empty");
            Assert.That(WritQuill.InkTarget(5, 8, WritQuill.InkReload.Seated), Is.EqualTo(1f), "the seat refills it");
            // the level rises (never overshooting) and is full within a moment of the seat
            float level = WritQuill.InkDrained;
            for (int i = 0; i < 60; i++)
            {
                float next = WritQuill.InkStep(level, 1f, 1f / 120f, true);
                Assert.That(next, Is.GreaterThanOrEqualTo(level));
                Assert.That(next, Is.LessThanOrEqualTo(1f));
                level = next;
            }
            Assert.That(level, Is.GreaterThan(0.99f), "half a second after the seat the well is full");
        }

        [Test]
        public void TheInkSmoothsInBothDirectionsWithoutJumping()
        {
            float up = WritQuill.InkStep(0.5f, 1f, 1f / 60f);
            Assert.That(up, Is.GreaterThan(0.5f).And.LessThan(0.8f), "one frame moves it a part of the way");
            float down = WritQuill.InkStep(0.5f, 0f, 1f / 60f);
            Assert.That(down, Is.LessThan(0.5f).And.GreaterThan(0.2f));
            Assert.That(WritQuill.InkStep(0.5f, 1f, 0f), Is.EqualTo(0.5f), "no time, no change");
            Assert.That(WritQuill.InkStep(0.5f, 0.5f, 0.1f), Is.EqualTo(0.5f).Within(Eps));
        }

        [Test]
        public void TheSloshRingsAndSettles()
        {
            var s = new WritQuill.InkSlosh();
            Assert.That(s.Offset, Is.EqualTo(0f));
            s.Kick(1.5f);
            float peak = 0f;
            for (int i = 0; i < 30; i++) { s.Step(1f / 60f); peak = Math.Max(peak, Math.Abs(s.Offset)); }
            Assert.That(peak, Is.GreaterThan(0.02f), "a kick shows");
            Assert.That(peak, Is.LessThanOrEqualTo(WritQuill.InkSlosh.Limit + Eps));
            for (int i = 0; i < 120; i++) s.Step(1f / 60f);
            Assert.That(Math.Abs(s.Offset), Is.LessThan(0.002f), "it settles");
            s.Kick(500f);
            s.Step(1f);                                           // a huge kick and a huge frame can't run away
            Assert.That(Math.Abs(s.Offset), Is.LessThanOrEqualTo(WritQuill.InkSlosh.Limit + Eps));
            float scale = WritQuill.InkDrawScale(1f, s.Offset);
            Assert.That(scale, Is.LessThanOrEqualTo(1.02f + Eps));
            Assert.That(WritQuill.InkDrawScale(0f, -WritQuill.InkSlosh.Limit), Is.GreaterThanOrEqualTo(0.03f - Eps));
        }

        // ---------------------------------------------------------------- the quill's poses

        [Test]
        public void TheQuillLeansBackAndOutAtTheHipAndLiesFlatInADS()
        {
            Vector3 hip, flat;
            float dip;
            WritQuill.Pose(0f, 0f, 0f, 0f, out hip, out dip);
            WritQuill.Pose(1f, 0f, 0f, 0f, out flat, out dip);
            Assert.That((hip - WritQuill.HipDirection).Length(), Is.LessThan(Eps), "the model's own pose is the hip lean");
            Assert.That(hip.X, Is.GreaterThan(0.3f), "out, away from the sight line");
            Assert.That(hip.Z, Is.LessThan(-0.3f), "back toward the player");
            Assert.That(hip.Y, Is.GreaterThan(0.5f), "standing up out of the well");
            Assert.That(flat.Y, Is.LessThan(0.2f), "flat");
            Assert.That(flat.Z, Is.LessThan(-0.9f));
            Assert.That(flat.Y, Is.LessThan(hip.Y));
            Assert.That(dip, Is.EqualTo(0f));
            Vector3 mid;
            WritQuill.Pose(0.5f, 0f, 0f, 0f, out mid, out dip);
            Assert.That(mid.Y, Is.LessThan(hip.Y).And.GreaterThan(flat.Y), "it eases between the two");
            Assert.That(mid.Length(), Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void AFlickGoesOutAndReturnsToRest()
        {
            Assert.That(WritQuill.Flick(0f), Is.EqualTo(0f));
            Assert.That(WritQuill.Flick(-1f), Is.EqualTo(0f));
            Assert.That(WritQuill.Flick(WritQuill.FlickRise), Is.EqualTo(1f).Within(Eps), "full flick");
            Assert.That(WritQuill.Flick(WritQuill.FlickSeconds), Is.EqualTo(0f));
            Assert.That(WritQuill.Flick(1f), Is.EqualTo(0f));
            Assert.That(WritQuill.FlickSeconds, Is.EqualTo(0.12f).Within(Eps), "about 0.12 s");
            float last = 1f;
            for (float t = WritQuill.FlickRise; t < WritQuill.FlickSeconds; t += 0.002f)
            {
                float f = WritQuill.Flick(t);
                Assert.That(f, Is.LessThanOrEqualTo(last + Eps), "it only settles back");
                Assert.That(f, Is.InRange(0f, 1f));
                last = f;
            }
            Vector3 rest, flicked, after;
            float dip;
            WritQuill.Pose(0f, 0f, 0f, 0f, out rest, out dip);
            WritQuill.Pose(0f, WritQuill.Flick(WritQuill.FlickRise), 0f, 0f, out flicked, out dip);
            WritQuill.Pose(0f, WritQuill.Flick(WritQuill.FlickSeconds), 0f, 0f, out after, out dip);
            Assert.That(Vector3.Dot(rest, flicked), Is.LessThan((float)Math.Cos(25.0 * Math.PI / 180.0)), "a flick moves it by a visible angle");
            Assert.That(flicked.Z, Is.GreaterThan(rest.Z), "forward");
            Assert.That((after - rest).Length(), Is.LessThan(Eps), "and it is back at rest");
        }

        [Test]
        public void TheSpotFlourishIsADoubleFlickUnderAThirdOfASecond()
        {
            Assert.That(WritQuill.FlourishSeconds, Is.LessThan(0.3f));
            Assert.That(WritQuill.Flourish(WritQuill.FlourishSeconds), Is.EqualTo(0f));
            Assert.That(WritQuill.Flourish(0f), Is.EqualTo(0f));
            int peaks = 0;
            float prev = 0f, prev2 = 0f;
            for (float t = 0.001f; t < WritQuill.FlourishSeconds + 0.02f; t += 0.002f)
            {
                float f = WritQuill.Flourish(t);
                if (prev > prev2 && prev >= f && prev > 0.5f) peaks++;
                prev2 = prev;
                prev = f;
            }
            Assert.That(peaks, Is.EqualTo(2), "two flicks");
            Assert.That(WritQuill.Flourish(WritQuill.FlourishGap), Is.LessThan(0.3f), "it settles between the two");
            Assert.That(WritQuill.Flourish(WritQuill.FlourishGap + WritQuill.FlickRise), Is.EqualTo(1f).Within(Eps), "the second is the bigger");
        }

        [Test]
        public void TheReloadDipsTheQuillAndStirsOnce()
        {
            // the Writ's partial reload: mag_out 0.09, windup 0.34
            float w, a;
            WritQuill.Stir(0.05f, 0.09f, 0.34f, out w, out a);
            Assert.That(w, Is.EqualTo(0f)); Assert.That(a, Is.EqualTo(0f));
            WritQuill.Stir(0.5f, 0.09f, 0.34f, out w, out a);
            Assert.That(w, Is.EqualTo(0f), "out of the well again before the gavel's windup");
            float peakW = 0f, lastA = 0f;
            for (float p = 0.09f; p < 0.34f; p += 0.002f)
            {
                WritQuill.Stir(p, 0.09f, 0.34f, out w, out a);
                peakW = Math.Max(peakW, w);
                Assert.That(a, Is.GreaterThanOrEqualTo(lastA - Eps), "the stir only turns one way");
                lastA = a;
            }
            Assert.That(peakW, Is.EqualTo(1f).Within(Eps), "all the way in the well");
            Assert.That(lastA, Is.EqualTo(360f).Within(1f), "one full turn");
            Vector3 d;
            float dip;
            WritQuill.Stir(0.215f, 0.09f, 0.34f, out w, out a);
            WritQuill.Pose(0f, 0f, w, a, out d, out dip);
            Assert.That(dip, Is.EqualTo(WritQuill.DipDepth).Within(Eps), "dipped");
            Assert.That(d.Y, Is.GreaterThan(WritQuill.HipDirection.Y), "upright in the well");
        }

        [Test]
        public void TheFlickIsSmallerInADS()
        {
            Vector3 hipRest, hipFlick, adsRest, adsFlick;
            float dip;
            WritQuill.Pose(0f, 0f, 0f, 0f, out hipRest, out dip);
            WritQuill.Pose(0f, 1f, 0f, 0f, out hipFlick, out dip);
            WritQuill.Pose(1f, 0f, 0f, 0f, out adsRest, out dip);
            WritQuill.Pose(1f, 1f, 0f, 0f, out adsFlick, out dip);
            Assert.That(Vector3.Dot(adsRest, adsFlick), Is.GreaterThan(Vector3.Dot(hipRest, hipFlick)));
        }

        // ---------------------------------------------------------------- the sight line

        /// <summary>The eye in the gun's frame when aimed: the gun-root pose AimDownSights.SolveSights gives (the sight line down
        /// the camera axis, the rear sight 0.13 m ahead of the eye), inverted.</summary>
        static Vector3 AimedEye()
        {
            Vector3 pos;
            Quaternion rot;
            AimDownSights.SolveSights(Rear, Front, SightDistance, out pos, out rot);
            return Vector3.Transform(-pos, Quaternion.Inverse(rot));
        }

        static float DegreesFromSightLine(Vector3 eye, Vector3 p)
        {
            Vector3 axis = Vector3.Normalize(Front - Rear), v = Vector3.Normalize(p - eye);
            return (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, Vector3.Dot(axis, v)))) * 180.0 / Math.PI);
        }

        /// <summary>Samples of the quill's envelope (its axis and the vane's edges, pivot to tip) for a direction and dip.</summary>
        static System.Collections.Generic.IEnumerable<Vector3> QuillPoints(Vector3 d, float dip)
        {
            Vector3 s1 = Vector3.Normalize(Vector3.Cross(d, Vector3.UnitY));
            if (s1.LengthSquared() < 0.5f) s1 = Vector3.UnitX;
            Vector3 s2 = Vector3.Normalize(Vector3.Cross(d, s1));
            for (int i = 0; i <= 10; i++)
                foreach (float side in new[] { -1f, 0f, 1f })
                {
                    yield return WritQuill.Point(d, dip, i / 10f, s1, side);
                    yield return WritQuill.Point(d, dip, i / 10f, s2, side);
                }
        }

        [Test]
        public void TheAimedSightLineIsClearOfTheQuillInEveryFlickAndBlend()
        {
            Vector3 eye = AimedEye();
            Assert.That(eye.X, Is.EqualTo(0f).Within(Eps)); Assert.That(eye.Y, Is.EqualTo(Rear.Y).Within(Eps));
            Assert.That(eye.Z, Is.EqualTo(Rear.Z - SightDistance).Within(Eps), "the eye is on the sight line, 13 cm behind the window");
            // what the player looks through: the ring round the window, seen from the eye
            float windowCone = (float)(Math.Atan(WindowRingRadius / (WindowRingZ - eye.Z)) * 180.0 / Math.PI);
            Assert.That(windowCone, Is.InRange(5f, 12f));
            float margin = 15f;
            float worst = 999f;
            for (float flick = 0f; flick <= 1.001f; flick += 0.25f)
                for (float ads = 0.8f; ads <= 1.001f; ads += 0.1f)
                {
                    Vector3 d;
                    float dip;
                    WritQuill.Pose(ads, flick, 0f, 0f, out d, out dip);
                    foreach (var p in QuillPoints(d, dip))
                    {
                        worst = Math.Min(worst, DegreesFromSightLine(eye, p));
                        Assert.That(p.Y, Is.LessThan(Rear.Y - WindowRingRadius - 0.03f), "below the sight window, with room to spare");
                    }
                }
            Assert.That(worst, Is.GreaterThan(windowCone + margin), "the quill stays well outside the window's cone");
        }

        [Test]
        public void TheFullyAimedQuillLiesFlatAndLow()
        {
            Vector3 eye = AimedEye(), d;
            float dip;
            WritQuill.Pose(1f, 0f, 0f, 0f, out d, out dip);
            foreach (var p in QuillPoints(d, dip))
            {
                Assert.That(p.Y, Is.LessThan(0.03f), "its top is below the receiver's deck (0.026) plus the vane's width");
                Assert.That(DegreesFromSightLine(eye, p), Is.GreaterThan(30f), "and far from the line of sight");
            }
        }

        [Test]
        public void AtTheHipTheQuillLeansOutOfTheCrosshairLine()
        {
            Vector3 d;
            float dip;
            WritQuill.Pose(0f, 0f, 0f, 0f, out d, out dip);
            var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, HipYawDegrees * (float)Math.PI / 180f);
            float least = 999f;
            foreach (var p in QuillPoints(d, dip))
            {
                Vector3 cam = HipRest + Vector3.Transform(p, turn);                       // the viewmodel camera's space
                float off = (float)(Math.Acos(cam.Z / cam.Length()) * 180.0 / Math.PI);  // from the crosshair line (+z)
                least = Math.Min(least, off);
            }
            Vector3 tip = HipRest + Vector3.Transform(WritQuill.Point(d, dip, 1f, Vector3.UnitX, 0f), turn);
            Assert.That(tip.X, Is.GreaterThan(HipRest.X), "the tip leans away from the crosshair, to the right of the gun");
            Assert.That(least, Is.GreaterThan(20f), "no part of it comes near the crosshair line at the hip");
        }

        // ---------------------------------------------------------------- the model's constants

        static string BuildScript()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string f = Path.Combine(dir.FullName, "tools", "skyline", "guns", "writ", "model", "build_writ.py");
                    if (File.Exists(f)) return File.ReadAllText(f);
                    dir = dir.Parent;
                }
            }
            return null;
        }

        static float F(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);

        [Test]
        public void TheModelsQuillConstantsMatchThisCode()
        {
            string py = BuildScript();
            if (py == null) { Assert.Ignore("tools/skyline/guns/writ/model/build_writ.py is not on disk here"); return; }
            var well = Regex.Match(py, @"WELL_X, WELL_Z = ([-0-9.]+), ([-0-9.]+)");
            var pivot = Regex.Match(py, @"QUILL_PIVOT = \(WELL_X, ([-0-9.]+), WELL_Z\)");
            var hip = Regex.Match(py, @"QUILL_HIP_DIR = \(([-0-9.]+), ([-0-9.]+), ([-0-9.]+)\)");
            var len = Regex.Match(py, @"FEATHER_LEN = ([0-9.]+)");
            Assert.That(well.Success && pivot.Success && hip.Success && len.Success, Is.True, "the constants are in build_writ.py");
            Assert.That(WritQuill.Pivot.X, Is.EqualTo(F(well.Groups[1].Value)).Within(Eps));
            Assert.That(WritQuill.Pivot.Z, Is.EqualTo(F(well.Groups[2].Value)).Within(Eps));
            Assert.That(WritQuill.Pivot.Y, Is.EqualTo(F(pivot.Groups[1].Value)).Within(Eps));
            var d = Vector3.Normalize(new Vector3(F(hip.Groups[1].Value), F(hip.Groups[2].Value), F(hip.Groups[3].Value)));
            Assert.That((WritQuill.HipDirection - d).Length(), Is.LessThan(Eps));
            Assert.That(WritQuill.FeatherLength, Is.EqualTo(F(len.Groups[1].Value)).Within(Eps));
        }
    }
}
