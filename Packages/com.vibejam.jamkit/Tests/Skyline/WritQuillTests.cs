using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>The Writ's back (docs/SKYLINE-GUN-WRIT.md, "The back: inkwell and quill"): the ink is the ammo, the quill
    /// chain's poses (off to the right at the hip, curled round and extended in ADS), the reload that writes on a sheet of paper,
    /// and the ADS sight line staying clear of the quill through the whole blend. Pure maths (<see cref="WritQuill"/>); the
    /// model's own constants are checked against tools/skyline/guns/writ/model/build_writ.py when the repo is on disk.</summary>
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
            float full = WritQuill.InkScale(WritQuill.InkTarget(8, 8));
            float half = WritQuill.InkScale(WritQuill.InkTarget(4, 8));
            float empty = WritQuill.InkScale(WritQuill.InkTarget(0, 8));
            Assert.That(full, Is.EqualTo(1f).Within(Eps), "a full magazine is a full well");
            Assert.That(empty, Is.EqualTo(WritQuill.InkEmpty).Within(Eps), "an empty magazine is a thin film, never nothing");
            Assert.That(WritQuill.InkEmpty, Is.GreaterThan(0f));
            Assert.That(half, Is.EqualTo((1f + WritQuill.InkEmpty) / 2f).Within(Eps), "half a magazine is half the well");
            Assert.That(WritQuill.InkScale(-1f), Is.EqualTo(WritQuill.InkEmpty).Within(Eps));
            Assert.That(WritQuill.InkScale(3f), Is.EqualTo(1f).Within(Eps));
            Assert.That(WritQuill.InkTarget(3, 0), Is.EqualTo(1f), "no magazine size: full, no divide by zero");
        }

        [Test]
        public void TheInkRefillsBehindTheQuillWhileItWritesAndIsFullWhenItReturns()
        {
            // the Writ's partial reload: lift 0.08, return 0.70
            const float lift = 0.08f, ret = 0.70f;
            Assert.That(WritQuill.InkReloadTarget(0.25f, 0.02f, lift, ret), Is.EqualTo(0.25f).Within(Eps), "before the lift: the magazine's level");
            Assert.That(WritQuill.InkReloadTarget(0.25f, lift, lift, ret), Is.EqualTo(0.25f).Within(Eps));
            float last = 0.25f;
            for (float p = lift; p <= ret; p += 0.005f)
            {
                float v = WritQuill.InkReloadTarget(0.25f, p, lift, ret);
                Assert.That(v, Is.GreaterThanOrEqualTo(last - Eps), "the ink only rises");
                Assert.That(v, Is.InRange(0.25f - Eps, 1f + Eps));
                last = v;
            }
            float mid = WritQuill.InkReloadTarget(0.25f, (lift + ret) / 2f, lift, ret);
            Assert.That(mid, Is.GreaterThan(0.4f).And.LessThan(0.95f), "half way through the writing it is part refilled: behind the quill");
            Assert.That(WritQuill.InkReloadTarget(0.25f, ret, lift, ret), Is.EqualTo(1f).Within(Eps), "full as the quill returns");
            Assert.That(WritQuill.InkReloadTarget(0.25f, 0.9f, lift, ret), Is.EqualTo(1f));
            // the smoothed level follows, never overshooting
            float level = 0.25f;
            for (int i = 0; i < 90; i++)
            {
                float next = WritQuill.InkStep(level, 1f, 1f / 120f);
                Assert.That(next, Is.GreaterThanOrEqualTo(level));
                Assert.That(next, Is.LessThanOrEqualTo(1f));
                level = next;
            }
            Assert.That(level, Is.GreaterThan(0.99f));
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

        // ---------------------------------------------------------------- the chain

        static Vector3[] JointsOf(WritQuill.ChainPose p)
        {
            var j = new Vector3[WritQuill.Segments + 1];
            WritQuill.Joints(p, j, null);
            return j;
        }

        /// <summary>Samples of the quill's envelope: along its chain, and round it by the vane's radius, in the gun's frame.</summary>
        static List<Vector3> QuillPoints(WritQuill.ChainPose p)
        {
            var joints = JointsOf(p);
            var pts = new List<Vector3>();
            float r = WritQuill.FeatherRadius;
            for (int i = 0; i < WritQuill.Segments; i++)
                for (int k = 0; k <= 6; k++)
                {
                    Vector3 c = Vector3.Lerp(joints[i], joints[i + 1], k / 6f);
                    pts.Add(c);
                    foreach (var o in new[] { new Vector3(r, 0, 0), new Vector3(-r, 0, 0), new Vector3(0, r, 0), new Vector3(0, -r, 0), new Vector3(0, 0, r), new Vector3(0, 0, -r) })
                        pts.Add(c + o);
                }
            return pts;
        }

        [Test]
        public void TheQuillIsAChainOfFourSegmentsHalfAgainAsLongAsBefore()
        {
            Assert.That(WritQuill.Segments, Is.EqualTo(4));
            Assert.That(WritQuill.FeatherLength, Is.EqualTo(WritQuill.Segments * WritQuill.SegmentLength).Within(1e-5f));
            Assert.That(WritQuill.FeatherLength, Is.GreaterThanOrEqualTo(0.0805f * 1.4f), "larger: the creator asked for it");
            foreach (var pose in new[] { WritQuill.HipPose, WritQuill.AdsPose, WritQuill.IdlePose(0.5f, 0.6f), WritQuill.Reload(0.4f, 0.08f, 0.7f, 0.76f).Pose })
            {
                var j = JointsOf(pose);
                Assert.That(j.Length, Is.EqualTo(5));
                Assert.That((j[0] - pose.Base).Length(), Is.LessThan(Eps));
                for (int i = 0; i < WritQuill.Segments; i++)
                    Assert.That((j[i + 1] - j[i]).Length(), Is.EqualTo(WritQuill.SegmentLength).Within(1e-4f), "a rigid segment");
            }
        }

        [Test]
        public void TheTurnsCarryEachSegmentFromItsHipPlaceToItsPoseDirection()
        {
            var turns = new Quaternion[WritQuill.Segments];
            var joints = new Vector3[WritQuill.Segments + 1];
            WritQuill.Joints(WritQuill.HipPose, joints, turns);
            foreach (var q in turns)
                Assert.That(Math.Abs(q.W), Is.GreaterThan(1f - 1e-4f), "the hip pose is the model as imported: no turn");
            foreach (var pose in new[] { WritQuill.AdsPose, WritQuill.IdlePose(0.4f, 0.7f), WritQuill.Reload(0.3f, 0.08f, 0.7f, 0.76f).Pose })
            {
                WritQuill.Joints(pose, joints, turns);
                for (int i = 0; i < WritQuill.Segments; i++)
                {
                    WritQuill.Frame(WritQuill.HipPose, i, out var hx, out var hy, out var hz);
                    WritQuill.Frame(pose, i, out var px, out var py, out var pz);
                    Assert.That((Vector3.Transform(hy, turns[i]) - py).Length(), Is.LessThan(1e-3f), "segment " + i + " points where the pose says");
                    Assert.That((Vector3.Transform(hx, turns[i]) - px).Length(), Is.LessThan(1e-3f));
                    Assert.That((Vector3.Transform(hz, turns[i]) - pz).Length(), Is.LessThan(1e-3f));
                    Assert.That(Math.Abs(turns[i].Length() - 1f), Is.LessThan(1e-3f));
                }
            }
        }

        [Test]
        public void TheQuillSitsOffToTheRightAtTheHipLeaningBackAndCurlingOut()
        {
            var j = JointsOf(WritQuill.HipPose);
            Assert.That(j[1].X, Is.GreaterThan(WritQuill.Pivot.X), "out to the right");
            Assert.That(j[4].X, Is.GreaterThan(WritQuill.Pivot.X + 0.05f), "its tip is well out to the right");
            Assert.That(j[1].Z, Is.LessThan(WritQuill.Pivot.Z), "leaning back toward the player");
            Assert.That(j[1].Y, Is.GreaterThan(WritQuill.Pivot.Y + 0.02f), "standing up out of the well");
            Assert.That(WritQuill.HipPose.Curl, Is.GreaterThan(20f), "a visible curl");
            // the model's own pose is the idle pose with no aim and no flick
            var idle = WritQuill.IdlePose(0f, 0f);
            Assert.That((idle.Direction - WritQuill.HipDirection).Length(), Is.LessThan(Eps));
            Assert.That(idle.Curl, Is.EqualTo(WritQuill.HipCurl).Within(Eps));
        }

        [Test]
        public void AimingCurlsTheQuillRoundAndExtendsItBackAndOutBelowTheSightLine()
        {
            var hip = JointsOf(WritQuill.IdlePose(0f, 0f));
            var ads = JointsOf(WritQuill.IdlePose(1f, 0f));
            float hipChord = (hip[4] - hip[0]).Length(), adsChord = (ads[4] - ads[0]).Length();
            Assert.That(adsChord, Is.GreaterThan(hipChord + 0.002f), "straightened: the chain's reach from the well grows");
            Assert.That(ads[4].Z, Is.LessThan(hip[4].Z - 0.02f), "extended: it reaches further back toward the player than at the hip");
            Assert.That(ads[4].Z, Is.LessThan(WritQuill.Pivot.Z - 0.09f), "it reaches back toward the player");
            Assert.That(ads[4].X, Is.GreaterThan(WritQuill.Pivot.X + 0.04f), "and out to the right, round the receiver");
            Assert.That(ads[4].Y, Is.LessThan(hip[4].Y), "lower than at the hip");
            Assert.That(ads[4].Y, Is.LessThan(0.03f), "its tip is below the receiver's deck (0.026) plus a little");
            var mid = JointsOf(WritQuill.IdlePose(0.5f, 0f));
            float midChord = (mid[4] - mid[0]).Length();
            Assert.That(midChord, Is.InRange(Math.Min(hipChord, adsChord) - 1e-3f, Math.Max(hipChord, adsChord) + 1e-3f), "it eases between the two");
        }

        // ---------------------------------------------------------------- the flicks

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
            var rest = WritQuill.IdlePose(0f, 0f);
            var flicked = WritQuill.IdlePose(0f, WritQuill.Flick(WritQuill.FlickRise));
            var after = WritQuill.IdlePose(0f, WritQuill.Flick(WritQuill.FlickSeconds));
            Assert.That(Vector3.Dot(rest.Direction, flicked.Direction), Is.LessThan((float)Math.Cos(25.0 * Math.PI / 180.0)), "a flick moves it by a visible angle");
            Assert.That(flicked.Direction.Z, Is.GreaterThan(rest.Direction.Z), "forward");
            Assert.That(flicked.Curl, Is.GreaterThan(rest.Curl + 10f), "and the chain whips");
            Assert.That((after.Direction - rest.Direction).Length(), Is.LessThan(Eps), "and it is back at rest");
            Assert.That(after.Curl, Is.EqualTo(rest.Curl).Within(Eps));
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
        public void TheFlickIsSmallerInADS()
        {
            var hipRest = WritQuill.IdlePose(0f, 0f);
            var hipFlick = WritQuill.IdlePose(0f, 1f);
            var adsRest = WritQuill.IdlePose(1f, 0f);
            var adsFlick = WritQuill.IdlePose(1f, 1f);
            Assert.That(Vector3.Dot(adsRest.Direction, adsFlick.Direction), Is.GreaterThan(Vector3.Dot(hipRest.Direction, hipFlick.Direction)));
            Assert.That(adsFlick.Curl - adsRest.Curl, Is.LessThan(hipFlick.Curl - hipRest.Curl));
        }

        // ---------------------------------------------------------------- the sight line

        /// <summary>The eye in the gun's frame at an aim blend: the gun root's pose in the viewmodel camera's space is the rest
        /// pose blended into the pose AimDownSights.SolveSights gives (the sight line down the camera axis, the rear sight 0.13 m
        /// ahead of the eye), as SkylineViewmodel does; the eye is the camera's origin, inverted.</summary>
        static Vector3 EyeAt(float k)
        {
            Vector3 aimPos;
            Quaternion aimRot;
            AimDownSights.SolveSights(Rear, Front, SightDistance, out aimPos, out aimRot);
            var restRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, HipYawDegrees * (float)Math.PI / 180f);
            Vector3 pos = Vector3.Lerp(HipRest, aimPos, k);
            Quaternion rot = Quaternion.Slerp(restRot, aimRot, k);
            return Vector3.Transform(-pos, Quaternion.Inverse(rot));
        }

        static float DegreesBetween(Vector3 a, Vector3 b) =>
            (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b))))) * 180.0 / Math.PI);

        /// <summary>How far outside the window's cone (degrees) the quill's nearest point is, from the eye at blend k.</summary>
        static float ClearanceBeyondWindow(WritQuill.ChainPose pose, float k)
        {
            Vector3 eye = EyeAt(k);
            Vector3 toWindow = new Vector3(0f, Rear.Y, WindowRingZ) - eye;
            float cone = (float)(Math.Atan(WindowRingRadius / toWindow.Length()) * 180.0 / Math.PI);
            float least = 999f;
            foreach (var p in QuillPoints(pose)) least = Math.Min(least, DegreesBetween(p - eye, toWindow));
            return least - cone;
        }

        [Test]
        public void TheAimedEyeIsOnTheSightLineBehindTheWindow()
        {
            Vector3 eye = EyeAt(1f);
            Assert.That(eye.X, Is.EqualTo(0f).Within(Eps)); Assert.That(eye.Y, Is.EqualTo(Rear.Y).Within(Eps));
            Assert.That(eye.Z, Is.EqualTo(Rear.Z - SightDistance).Within(Eps), "the eye is on the sight line, 13 cm behind the window");
            Vector3 toWindow = new Vector3(0f, Rear.Y, WindowRingZ) - eye;
            float cone = (float)(Math.Atan(WindowRingRadius / toWindow.Length()) * 180.0 / Math.PI);
            Assert.That(cone, Is.InRange(5f, 12f));
        }

        [Test]
        public void TheSightWindowIsClearOfTheQuillThroughTheWholeAimBlend()
        {
            float worstBlend = 999f, worstAimed = 999f;
            for (float k = 0f; k <= 1.0001f; k += 0.05f)
            {
                for (float flick = 0f; flick <= 1.001f; flick += 0.25f)
                {
                    if (flick > 0f && k < 0.8f) continue;               // a shot's flick at the aim's start is a hip flick: not in this picture
                    float clear = ClearanceBeyondWindow(WritQuill.IdlePose(k, flick), k);
                    if (k >= 0.8f) worstAimed = Math.Min(worstAimed, clear); else worstBlend = Math.Min(worstBlend, clear);
                }
            }
            Assert.That(worstBlend, Is.GreaterThan(3f), "while the aim comes up the quill is never in the window's cone");
            Assert.That(worstAimed, Is.GreaterThan(15f), "aimed (0.8 to 1, every flick) it stays well outside the window's cone");
            Assert.That(ClearanceBeyondWindow(WritQuill.IdlePose(1f, 0f), 1f), Is.GreaterThan(20f), "fully aimed it is far from the picture");
        }

        [Test]
        public void AReloadDoesNotSendTheQuillTowardTheWindowAsTheAimComesUp()
        {
            // a reload can still be playing as the aim eases out of a held aim: the reload fades out by half-aimed
            var rq = WritQuill.Reload(0.3f, 0.08f, 0.7f, 0.76f);
            Assert.That(rq.Active, Is.True);
            var aimed = WritQuill.Pose(1f, 0f, rq);
            var idle = WritQuill.IdlePose(1f, 0f);
            Assert.That((aimed.Base - idle.Base).Length(), Is.LessThan(Eps), "aimed: the reload's movement is ignored");
            Assert.That(Math.Abs(aimed.Curl - idle.Curl), Is.LessThan(Eps));
            var half = WritQuill.Pose(0.5f, 0f, rq);
            Assert.That((half.Base - WritQuill.IdlePose(0.5f, 0f).Base).Length(), Is.LessThan(Eps), "half-aimed: gone too");
            var hip = WritQuill.Pose(0f, 0f, rq);
            Assert.That((hip.Base - rq.Pose.Base).Length(), Is.LessThan(Eps), "at the hip it is the reload's pose");
            // and through the blend, with the reload playing, it stays out of the window's cone
            for (float p = 0.08f; p <= 0.8f; p += 0.04f)
            {
                var r = WritQuill.Reload(p, 0.08f, 0.7f, 0.76f);
                for (float k = 0.5f; k <= 1.0001f; k += 0.1f)
                    Assert.That(ClearanceBeyondWindow(WritQuill.Pose(k, 0f, r), k), Is.GreaterThan(3f), "p " + p + " k " + k);
            }
        }

        [Test]
        public void AtTheHipTheQuillSitsOffToTheRightOutOfTheCrosshairLine()
        {
            var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, HipYawDegrees * (float)Math.PI / 180f);
            float least = 999f;
            foreach (var p in QuillPoints(WritQuill.HipPose))
            {
                Vector3 cam = HipRest + Vector3.Transform(p, turn);                       // the viewmodel camera's space
                float off = (float)(Math.Acos(cam.Z / cam.Length()) * 180.0 / Math.PI);  // from the crosshair line (+z)
                least = Math.Min(least, off);
            }
            Vector3 tip = HipRest + Vector3.Transform(JointsOf(WritQuill.HipPose)[4], turn);
            Assert.That(tip.X, Is.GreaterThan(HipRest.X), "the tip leans away from the crosshair, to the right of the gun");
            Assert.That(least, Is.GreaterThan(20f), "no part of it comes near the crosshair line at the hip");
        }

        // ---------------------------------------------------------------- the reload

        const float Lift = 0.08f, Return = 0.70f, Seat = 0.76f;

        /// <summary>A point of the gun's frame in the viewmodel camera's space at a point of a reload clip (the rest pose, then the
        /// clip's own offset and turn).</summary>
        static Vector3 CameraSpace(Vector3 p, PoseFrame pose)
        {
            var rest = Quaternion.CreateFromAxisAngle(Vector3.UnitY, HipYawDegrees * (float)Math.PI / 180f);
            const float d2r = (float)Math.PI / 180f;
            var clip = Quaternion.CreateFromYawPitchRoll(pose.gunEuler.Y * d2r, pose.gunEuler.X * d2r, pose.gunEuler.Z * d2r);
            return HipRest + pose.gunOffset + Vector3.Transform(p, rest * clip);
        }

        [Test]
        public void TheReloadLeavesTheWellWritesOnThePaperAndReturnsToTheWell()
        {
            Assert.That(WritQuill.Reload(0.02f, Lift, Return, Seat).Active, Is.False, "before the lift: at rest");
            Assert.That(WritQuill.Reload(Lift, Lift, Return, Seat).Active, Is.False);
            Assert.That(WritQuill.Reload(Seat + WritQuill.SettleFraction + 0.01f, Lift, Return, Seat).Active, Is.False, "settled: at rest again");
            Assert.That(WritQuill.Reload(1f, Lift, Return, Seat).Active, Is.False);
            Vector3 wellTip = WritQuill.HipPose.NibTip;
            bool wrote = false, hopped = false, left = false;
            float farthest = 0f;
            for (float p = Lift + 0.002f; p < Return; p += 0.004f)
            {
                var r = WritQuill.Reload(p, Lift, Return, Seat);
                Assert.That(r.Active, Is.True);
                Vector3 tip = r.Pose.NibTip;
                farthest = Math.Max(farthest, (tip - wellTip).Length());
                if (r.Writing)
                {
                    wrote = true;
                    Assert.That(tip.Y, Is.EqualTo(WritQuill.NibPaperY).Within(5e-4f), "the nib is on the paper while it writes");
                    // inside the sheet (the paper frame: u across, v away)
                    float yaw = WritQuill.PaperYawDegrees * (float)Math.PI / 180f;
                    float dx = tip.X - WritQuill.PaperX, dz = tip.Z - WritQuill.PaperZ;
                    float u = dx * (float)Math.Cos(yaw) - dz * (float)Math.Sin(yaw), v = dx * (float)Math.Sin(yaw) + dz * (float)Math.Cos(yaw);
                    Assert.That(Math.Abs(u), Is.LessThan(0.017f - 0.001f));
                    Assert.That(Math.Abs(v), Is.LessThan(0.013f - 0.001f));
                }
                else if (tip.Y > WritQuill.NibPaperY + 0.0015f && (tip - wellTip).Length() > 0.03f) hopped = true;
                if ((tip - wellTip).Length() > 0.02f) left = true;
            }
            Assert.That(left, Is.True, "it leaves the well");
            Assert.That(farthest, Is.GreaterThan(0.05f), "and goes to the paper on the table");
            Assert.That(wrote, Is.True, "it writes");
            Assert.That(hopped, Is.True, "and lifts between the strokes");
            var back = WritQuill.Reload(Return, Lift, Return, Seat);
            Assert.That((back.Pose.NibTip - wellTip).Length(), Is.LessThan(WritQuill.DipDepth + 0.0015f), "back in the well at the return");
            var seated = WritQuill.Reload(Seat + 0.01f, Lift, Return, Seat);
            Assert.That(seated.Active, Is.True, "still settling just after the seat");
            Assert.That((seated.Pose.NibTip - wellTip).Length(), Is.LessThan(WritQuill.DipDepth + 0.0015f));
        }

        [Test]
        public void TheHandwritingAppearsUnderTheNibStrokeByStroke()
        {
            Assert.That(WritQuill.ShownSegments(0f), Is.EqualTo(0));
            Assert.That(WritQuill.ShownSegments(1f), Is.EqualTo(WritQuill.InkSegmentCount));
            Assert.That(WritQuill.InkSegmentCount, Is.EqualTo(30));
            int last = 0;
            for (float s = 0f; s <= 1.0001f; s += 0.005f)
            {
                int n = WritQuill.ShownSegments(s);
                Assert.That(n, Is.GreaterThanOrEqualTo(last), "the line only grows");
                Assert.That(n, Is.InRange(0, WritQuill.InkSegmentCount));
                last = n;
            }
            // the piece the nib has just passed is what is shown: at the end of stroke 0's writing, 10 pieces
            float endOfFirst = WritQuill.StrokeWriting / WritQuill.StrokeCount;
            Assert.That(WritQuill.ShownSegments(endOfFirst + 0.001f), Is.EqualTo(WritQuill.InkSegmentsPerStroke));
            Assert.That(WritQuill.ShownSegments(endOfFirst * 0.5f), Is.InRange(4, 6), "half way along the first stroke: about half of it");
            // the reload reports the same through its span, and clears the page after the seat
            var mid = WritQuill.Reload(0.45f, Lift, Return, Seat);
            Assert.That(mid.Shown, Is.GreaterThan(0).And.LessThan(WritQuill.InkSegmentCount));
            Assert.That(WritQuill.Reload(Return, Lift, Return, Seat).Shown, Is.EqualTo(WritQuill.InkSegmentCount), "all of it is written when the quill returns");
            Assert.That(WritQuill.Reload(Seat + 0.02f, Lift, Return, Seat).Shown, Is.EqualTo(0), "a blank page again once the ink has seated");
            Assert.That(WritQuill.Reload(0.05f, Lift, Return, Seat).Shown, Is.EqualTo(0));
        }

        [Test]
        public void EveryStrokeLiesOnTheSheetOfPaper()
        {
            for (int k = 0; k < WritQuill.StrokeCount; k++)
                for (float t = 0f; t <= 1.0001f; t += 0.05f)
                {
                    Vector3 p = WritQuill.StrokePoint(k, t);
                    Assert.That(p.Y, Is.EqualTo(WritQuill.NibPaperY).Within(Eps));
                    float yaw = WritQuill.PaperYawDegrees * (float)Math.PI / 180f;
                    float dx = p.X - WritQuill.PaperX, dz = p.Z - WritQuill.PaperZ;
                    float u = dx * (float)Math.Cos(yaw) - dz * (float)Math.Sin(yaw), v = dx * (float)Math.Sin(yaw) + dz * (float)Math.Cos(yaw);
                    Assert.That(Math.Abs(u), Is.LessThan(0.017f - 0.002f), "stroke " + k + " stays inside the sheet's width");
                    Assert.That(Math.Abs(v), Is.LessThan(0.013f - 0.002f), "and its length");
                }
            Assert.That(WritQuill.StrokePoint(0, 0f).Z, Is.GreaterThan(WritQuill.StrokePoint(2, 0f).Z), "the lines run down the page, away from the player first");
        }

        [Test]
        public void DuringTheReloadTheQuillStaysOffTheCrosshairLineAtTheHipWithTheGunTilted()
        {
            var w = SkylineGuns.Writ();
            foreach (var kind in new[] { HandlingClipKind.ReloadPartial, HandlingClipKind.ReloadEmpty })
            {
                var c = w.Clip(kind);
                float lift = EventAt(c, "lift"), ret = EventAt(c, "return"), seat = EventAt(c, "seat");
                float least = 999f;
                for (float p = 0f; p <= 1.0001f; p += 0.01f)
                {
                    var rq = WritQuill.Reload(p, lift, ret, seat);
                    var pose = WritQuill.Pose(0f, 0f, rq);
                    var frame = c.SamplePose(p);
                    foreach (var pt in QuillPoints(pose))
                    {
                        Vector3 cam = CameraSpace(pt, frame);
                        if (cam.Z <= 0.02f) continue;                                  // behind the camera's near plane: not in the picture
                        least = Math.Min(least, (float)(Math.Acos(cam.Z / cam.Length()) * 180.0 / Math.PI));
                    }
                }
                Assert.That(least, Is.GreaterThan(14f), kind + ": the quill never crosses the crosshair at the hip, even tilted up");
            }
        }

        static float EventAt(HandlingClip c, string id)
        {
            foreach (var e in c.events) if (e.id == id) return e.at;
            return -1f;
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
            var seg = Regex.Match(py, @"SEG_N, SEG_LEN = (\d+), ([0-9.]+)");
            var nib = Regex.Match(py, @"NIB_BELOW = ([0-9.]+)");
            var hip = Regex.Match(py, @"QUILL_HIP_DIR = \(([-0-9.]+), ([-0-9.]+), ([-0-9.]+)\)");
            var toward = Regex.Match(py, @"QUILL_HIP_TOWARD = \(([-0-9.]+), ([-0-9.]+), ([-0-9.]+)\)");
            var curl = Regex.Match(py, @"QUILL_HIP_CURL = ([-0-9.]+)");
            Assert.That(well.Success && pivot.Success && seg.Success && nib.Success && hip.Success && toward.Success && curl.Success, Is.True, "the constants are in build_writ.py");
            Assert.That(WritQuill.Pivot.X, Is.EqualTo(F(well.Groups[1].Value)).Within(Eps));
            Assert.That(WritQuill.Pivot.Z, Is.EqualTo(F(well.Groups[2].Value)).Within(Eps));
            Assert.That(WritQuill.Pivot.Y, Is.EqualTo(F(pivot.Groups[1].Value)).Within(Eps));
            Assert.That(WritQuill.Segments, Is.EqualTo(int.Parse(seg.Groups[1].Value)));
            Assert.That(WritQuill.SegmentLength, Is.EqualTo(F(seg.Groups[2].Value)).Within(1e-5f));
            Assert.That(WritQuill.NibBelow, Is.EqualTo(F(nib.Groups[1].Value)).Within(1e-5f));
            var d = Vector3.Normalize(new Vector3(F(hip.Groups[1].Value), F(hip.Groups[2].Value), F(hip.Groups[3].Value)));
            Assert.That((WritQuill.HipDirection - d).Length(), Is.LessThan(Eps));
            var t = Vector3.Normalize(new Vector3(F(toward.Groups[1].Value), F(toward.Groups[2].Value), F(toward.Groups[3].Value)));
            Assert.That((WritQuill.HipToward - t).Length(), Is.LessThan(Eps));
            Assert.That(WritQuill.HipCurl, Is.EqualTo(F(curl.Groups[1].Value)).Within(Eps));
            Assert.That(WritQuill.FeatherLength, Is.EqualTo(F(seg.Groups[2].Value) * int.Parse(seg.Groups[1].Value)).Within(1e-4f));
        }

        [Test]
        public void ThePaperAndTheHandwritingMatchTheModel()
        {
            string py = BuildScript();
            if (py == null) { Assert.Ignore("tools/skyline/guns/writ/model/build_writ.py is not on disk here"); return; }
            var paper = Regex.Match(py, @"PAPER_X, PAPER_Z, PAPER_YAW, PAPER_SURFACE = ([-0-9.]+), ([-0-9.]+), ([-0-9.]+), ([-0-9.]+)");
            var u = Regex.Match(py, @"STROKE_U0, STROKE_U1 = ([-0-9.]+), ([-0-9.]+)");
            var v = Regex.Match(py, @"STROKE_V = \(([-0-9.]+), ([-0-9.]+), ([-0-9.]+)\)");
            var wave = Regex.Match(py, @"STROKE_AMP, STROKE_WAVES = ([-0-9.]+), ([-0-9.]+)");
            var counts = Regex.Match(py, @"STROKE_COUNT, INK_SEGMENTS = (\d+), (\d+)");
            var thick = Regex.Match(py, @"INK_WIDTH, INK_THICK = ([0-9.]+), ([0-9.]+)");
            Assert.That(paper.Success && u.Success && v.Success && wave.Success && counts.Success && thick.Success, Is.True);
            Assert.That(WritQuill.PaperX, Is.EqualTo(F(paper.Groups[1].Value)).Within(Eps));
            Assert.That(WritQuill.PaperZ, Is.EqualTo(F(paper.Groups[2].Value)).Within(Eps));
            Assert.That(WritQuill.PaperYawDegrees, Is.EqualTo(F(paper.Groups[3].Value)).Within(Eps));
            Assert.That(WritQuill.PaperSurface, Is.EqualTo(F(paper.Groups[4].Value)).Within(Eps));
            Assert.That(WritQuill.StrokeU0, Is.EqualTo(F(u.Groups[1].Value)).Within(Eps));
            Assert.That(WritQuill.StrokeU1, Is.EqualTo(F(u.Groups[2].Value)).Within(Eps));
            for (int i = 0; i < 3; i++) Assert.That(WritQuill.StrokeV[i], Is.EqualTo(F(v.Groups[i + 1].Value)).Within(Eps));
            Assert.That(WritQuill.StrokeAmp, Is.EqualTo(F(wave.Groups[1].Value)).Within(Eps));
            Assert.That(WritQuill.StrokeWaves, Is.EqualTo(F(wave.Groups[2].Value)).Within(Eps));
            Assert.That(WritQuill.StrokeCount, Is.EqualTo(int.Parse(counts.Groups[1].Value)));
            Assert.That(WritQuill.InkSegmentsPerStroke, Is.EqualTo(int.Parse(counts.Groups[2].Value)));
            Assert.That(WritQuill.InkThickness, Is.EqualTo(F(thick.Groups[2].Value)).Within(Eps));
            Assert.That(WritQuill.NibPaperY, Is.EqualTo(WritQuill.PaperSurface + WritQuill.InkThickness).Within(Eps));
        }

        [Test]
        public void TheModelHasNoGavelNoChairNoBarAcrossTheWindowAndNoObeliskBehindTheBead()
        {
            string py = BuildScript();
            if (py == null) { Assert.Ignore("tools/skyline/guns/writ/model/build_writ.py is not on disk here"); return; }
            // the creator, 2026-10-14: remove the high-back chair and the gavel entirely; the cross blocks the sight picture
            Assert.That(py, Does.Not.Contain("def gavel_parts"));
            Assert.That(py, Does.Not.Contain("def bench_parts"), "the chair lived in the bench's parts");
            Assert.That(py, Does.Not.Contain("K.to_object(\"Gavel\""));
            Assert.That(py, Does.Not.Contain("K.to_object(\"Bench\""));
            Assert.That(py, Does.Not.Contain("K.to_object(\"Mag\""), "nothing drops out of the grip in a reload");
            Assert.That(py, Does.Not.Contain("the bar (thin"), "no brass bar across the window");
            Assert.That(py, Does.Not.Contain("0.112, BRASS"), "no tall obelisk behind the bead");
            Assert.That(py, Does.Contain("sight_check("), "the export checks the window is clear");
        }
    }
}
