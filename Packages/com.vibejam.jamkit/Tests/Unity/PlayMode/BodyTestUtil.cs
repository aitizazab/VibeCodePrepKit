using System;
using System.Collections;
using JamKit.Movement;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.Tests.Play
{
    /// <summary>
    /// Watches a first-person body while something (an autopilot) moves the player: the wall hand reaches
    /// its spot by the second frame after each wallrun catch, the ankles reach the slide pose's feet, the
    /// head stays inside the first-person cut-away, and the body has no colliders. For any template whose
    /// MovementBody has a real rig (the Movement Lab, the dream escape).
    /// </summary>
    public static class BodyTestUtil
    {
        public sealed class Report
        {
            public int catches, catchesChecked, slideFrames, reachedEarly;
            public float leastLeadForward = float.MaxValue, leastLeadUp = float.MaxValue, worstWallGap;
            public float worstWallHand, worstSlideFoot, worstHead, cutout, leastHeadMargin = float.MaxValue;
            public int firstPersonFrames;
            public override string ToString() =>
                $"catches {catches} (reaching out before contact on {reachedEarly}, checked {catchesChecked}, worst hand off its target {worstWallHand:0.000} m, "
                + $"worst gap to the wall {worstWallGap:0.00} m), slide frames {slideFrames}"
                + (slideFrames > 0 ? $" (worst ankle off its target {worstSlideFoot:0.000} m, leading ankle at least {leastLeadForward:0.00} m ahead and {leastLeadUp:0.00} m up)" : "")
                + $", head bone at most {worstHead:0.000} m from the camera, the head's sphere at least {leastHeadMargin:0.000} m inside the cut-away "
                + $"(up to {cutout:0.00} m, {firstPersonFrames} first-person frames)";
        }

        public static MixamoBodyRig RigOf(MovementController player)
        {
            var mb = player != null ? player.GetComponentInChildren<MovementBody>(true) : null;
            Assert.That(mb, Is.Not.Null, "the MovementBody");
            Assert.That(mb.rig, Is.Not.Null, "the body is wired to a model (the builders wire Nadia)");
            Assert.That(mb.UsingRig, Is.True, "the model is bound and draws (not the capsules)");
            var rig = mb.rig as MixamoBodyRig;
            Assert.That(rig, Is.Not.Null);
            Assert.That(rig.GetComponentsInChildren<Collider>(true), Is.Empty, "no colliders: the movement can't change");
            return rig;
        }

        /// <summary>Watches until <paramref name="done"/> or <paramref name="seconds"/> pass (real time).</summary>
        public static IEnumerator Watch(MovementController player, Func<bool> done, float seconds, Report r)
        {
            var mb = player.GetComponentInChildren<MovementBody>(true);
            var rig = RigOf(player);
            int catchFrame = -1, wallIdx = 0;
            bool counted = false;
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end)
            {
                var ev = player.FrameEvents;
                if (ev.wallrunStarted)
                {
                    r.catches++;
                    // The pose read here is the frame before contact: the lean-in should already be under way.
                    wallIdx = ev.wallSide > 0 ? 1 : 0;
                    if (mb.Pose != null && mb.Pose.reach[wallIdx] > 0.3f) r.reachedEarly++;
                    catchFrame = Time.frameCount;
                    counted = false;
                }
                // The hand is planted on the contact frame; the coroutine reads the pose a frame late, so every wallrun
                // frame from two after the catch must have the hand on its spot (the IK reached it).
                if (catchFrame >= 0 && player.Mode == MoveMode.Wallrun && mb.HandOnWall && Time.frameCount - catchFrame >= 2)
                {
                    if (!counted) { r.catchesChecked++; counted = true; }
                    r.worstWallHand = Mathf.Max(r.worstWallHand, rig.HandError[wallIdx]);
                    r.worstWallGap = Mathf.Max(r.worstWallGap, mb.WallGap);
                }
                if (player.Mode != MoveMode.Wallrun) catchFrame = -1;
                if (player.Mode == MoveMode.Slide && mb.Pose != null && mb.Pose.legs > 0.99f)
                {
                    r.slideFrames++;
                    r.worstSlideFoot = Mathf.Max(r.worstSlideFoot, Mathf.Max(rig.FootError[0], rig.FootError[1]));
                    // The leading (left) foot out in front and up: what makes the feet show at the bottom of the screen.
                    var lead = rig.FootLocal[0];
                    r.leastLeadForward = Mathf.Min(r.leastLeadForward, lead.z);
                    r.leastLeadUp = Mathf.Min(r.leastLeadUp, lead.y);
                }
                r.worstHead = Mathf.Max(r.worstHead, rig.HeadDistance);
                if (rig.CutoutRadius > 0f)
                {
                    r.firstPersonFrames++;
                    r.cutout = Mathf.Max(r.cutout, rig.CutoutRadius);
                    r.leastHeadMargin = Mathf.Min(r.leastHeadMargin, rig.HeadMargin);
                }
                yield return null;
            }
        }

        /// <summary>The checks: the wall hand reaches its planted spot (within <paramref name="handTolerance"/> m: the spot is kept
        /// within the arm's reach, so this is only the IK's rounding; when a catch comes from further off the wall than her arm,
        /// the spot is where the stretched arm meets it, and the gap is reported), it reached out before contact, the leading
        /// foot out in front and up in a slide (the slide pose's own target is reported, not asserted), the head cut away from
        /// the first-person view.</summary>
        public static void AssertFollowed(Report r, bool expectWallruns, bool expectSlides, float handTolerance = 0.03f)
        {
            TestContext.Out.WriteLine("Body: " + r);
            if (expectWallruns)
            {
                Assert.That(r.catchesChecked, Is.GreaterThan(0), "a wallrun catch was watched");
                Assert.That(r.worstWallHand, Is.LessThan(handTolerance), "the wall hand is on its spot (planted on the contact frame)");
                Assert.That(r.reachedEarly, Is.GreaterThan(0), "the wall hand reached out before contact (the lean-in) on at least one catch");
            }
            if (expectSlides)
            {
                Assert.That(r.slideFrames, Is.GreaterThan(0), "a slide was watched");
                Assert.That(r.leastLeadForward, Is.GreaterThan(0.45f), "the leading foot is out in front in the slide");
                Assert.That(r.leastLeadUp, Is.GreaterThan(0.12f), "and up off the ground, where the camera sees it");
            }
            Assert.That(r.firstPersonFrames, Is.GreaterThan(0), "first-person frames were watched");
            Assert.That(r.leastHeadMargin, Is.GreaterThanOrEqualTo(0f), "the head, hair and neck stay inside the cut-away: the first-person camera never sees them");
        }
    }
}
