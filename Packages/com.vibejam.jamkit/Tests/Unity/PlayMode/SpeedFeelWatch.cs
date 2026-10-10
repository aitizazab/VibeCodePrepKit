using System;
using System.Reflection;
using JamKit.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;

namespace JamKit.Tests.Play
{
    /// <summary>What a run of the speed visuals looked like, frame by frame (fed by <see cref="SpeedFeelProbe"/>), for the
    /// Movement Lab's and the dream escape's speed-feel play tests. Also the per-frame allocation check.</summary>
    public sealed class SpeedFeelWatch
    {
        public int booms, crossings, boomsOffCrossing, footprintsOffFlying, footprintsOffStep;
        public float maxBoomFov, maxRateRunning, maxRateFast, maxRateFlying, maxBlur;
        public int framesRunning, framesFast, framesFlying, maxAlive, screenFrames;
        int _lastTier = -1, _lastPrints;

        public void Frame(SpeedFeel feel, MovementCamera cam, SpeedStreaks streaks, SpeedFootfalls feet, SpeedScreenFx screen)
        {
            var l = feel.Levels;
            int tier = feel.State != null ? feel.State.Tier.Tier : 0;
            bool crossed = _lastTier >= 0 && _lastTier < SpeedTier.Flying && tier == SpeedTier.Flying;
            if (crossed) crossings++;
            if (feel.BoomThisFrame)
            {
                booms++;
                if (!crossed) boomsOffCrossing++;
            }
            _lastTier = tier;
            maxBoomFov = Mathf.Max(maxBoomFov, cam.BoomFov);
            if (tier == SpeedTier.Running && l.fast <= 0f) { framesRunning++; maxRateRunning = Mathf.Max(maxRateRunning, streaks.Rate); }
            if (tier == SpeedTier.Fast && l.fast >= 0.99f && l.flying <= 0f) { framesFast++; maxRateFast = Mathf.Max(maxRateFast, streaks.Rate); }
            if (tier == SpeedTier.Flying && l.flying >= 0.99f) { framesFlying++; maxRateFlying = Mathf.Max(maxRateFlying, streaks.Rate); }
            maxAlive = Mathf.Max(maxAlive, streaks.Alive);
            if (screen.Active) screenFrames++;
            maxBlur = Mathf.Max(maxBlur, screen.EdgeBlur);
            if (feet.FootprintsLaid != _lastPrints)
            {
                if (tier != SpeedTier.Flying) footprintsOffFlying++;
                if (!feel.body.Stepped) footprintsOffStep++;
                _lastPrints = feet.FootprintsLaid;
            }
        }

        public override string ToString() =>
            $"booms {booms} (tier crossings {crossings}, booms off a crossing {boomsOffCrossing}), max boom FOV {maxBoomFov:0.0} deg; "
            + $"streak rate running {maxRateRunning:0} / fast {maxRateFast:0} / flying {maxRateFlying:0} per s (frames {framesRunning}/{framesFast}/{framesFlying}), most alive {maxAlive}; "
            + $"screen pass frames {screenFrames}, max edge blur {maxBlur:0.00}; footprints off flying {footprintsOffFlying}, off a foot plant {footprintsOffStep}";

        /// <summary>Calls each speed visual's per-frame method under Unity's GC allocation constraint.</summary>
        public static void AssertNoAllocation(params MonoBehaviour[] parts)
        {
            var calls = new Action[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var m = parts[i].GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Assert.That(m, NUnit.Framework.Is.Not.Null, parts[i].GetType().Name + ".LateUpdate");
                calls[i] = (Action)Delegate.CreateDelegate(typeof(Action), parts[i], m);
            }
            TestDelegate all = () => { for (int i = 0; i < calls.Length; i++) calls[i](); };
            all(); // warm up
            Assert.That(all, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(), "the speed visuals' per-frame code allocates nothing");
        }
    }
}
