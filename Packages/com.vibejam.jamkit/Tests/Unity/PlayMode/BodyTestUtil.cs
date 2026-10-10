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
    /// head stays inside the camera's cut-away, and the body has no colliders. For any template whose
    /// MovementBody has a real rig (the Movement Lab, the dream escape).
    /// </summary>
    public static class BodyTestUtil
    {
        public sealed class Report
        {
            public int catches, catchesChecked, slideFrames;
            public float worstWallHand, worstSlideFoot, worstHead, cutout;
            public override string ToString() =>
                $"catches {catches} (checked {catchesChecked}, worst hand {worstWallHand:0.000} m), slide frames {slideFrames} (worst ankle {worstSlideFoot:0.000} m), head {worstHead:0.000} m from the eye (cut-away {cutout:0.00} m)";
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
            r.cutout = rig.CutoutRadius;
            int catchFrame = -1, wallIdx = 0;
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end)
            {
                var ev = player.FrameEvents;
                if (ev.wallrunStarted)
                {
                    r.catches++;
                    catchFrame = Time.frameCount;
                    wallIdx = ev.wallSide > 0 ? 1 : 0;
                }
                // The coroutine runs after Update: what it reads is the last LateUpdate's pose, the frame before.
                if (catchFrame >= 0 && Time.frameCount - catchFrame == 3)
                {
                    if (player.Mode == MoveMode.Wallrun && mb.HandOnWall)
                    {
                        r.catchesChecked++;
                        r.worstWallHand = Mathf.Max(r.worstWallHand, rig.HandError[wallIdx]);
                    }
                    catchFrame = -1;
                }
                if (player.Mode == MoveMode.Slide && mb.Pose != null && mb.Pose.legs > 0.99f)
                {
                    r.slideFrames++;
                    r.worstSlideFoot = Mathf.Max(r.worstSlideFoot, Mathf.Max(rig.FootError[0], rig.FootError[1]));
                }
                r.worstHead = Mathf.Max(r.worstHead, rig.HeadDistance);
                yield return null;
            }
        }

        /// <summary>The checks: the wall hand on its spot (within <paramref name="handTolerance"/> m: the arm's reach),
        /// the slide feet reached, the head cut away from the first-person view.</summary>
        public static void AssertFollowed(Report r, bool expectWallruns, bool expectSlides, float handTolerance = 0.06f)
        {
            TestContext.Out.WriteLine("Body: " + r);
            if (expectWallruns)
            {
                Assert.That(r.catchesChecked, Is.GreaterThan(0), "a wallrun catch was watched");
                Assert.That(r.worstWallHand, Is.LessThan(handTolerance), "the wall hand lands on its spot by the second frame");
            }
            if (expectSlides)
            {
                Assert.That(r.slideFrames, Is.GreaterThan(0), "a slide was watched");
                Assert.That(r.worstSlideFoot, Is.LessThan(0.12f), "the legs take the slide pose");
            }
            Assert.That(r.worstHead, Is.LessThan(r.cutout - 0.04f), "the head stays inside the cut-away: the first-person camera never sees it");
        }
    }
}
