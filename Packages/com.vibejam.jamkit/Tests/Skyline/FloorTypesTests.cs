using System;
using System.Numerics;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task F0: the floor's shared vocabulary. The constants are pinned to the numbers in
    /// docs/SKYLINE-PLAN-FLOOR1.md (Global constraints), so a later edit that drifts from the spec fails here.</summary>
    public class FloorTypesTests
    {
        [Test]
        public void EveryFloorRuleEqualsTheSpecNumber()
        {
            Assert.That(FloorRules.TowSeconds, Is.EqualTo(10f));
            Assert.That(FloorRules.CircleRadius, Is.EqualTo(3f), "a 6 m tow circle");
            Assert.That(FloorRules.WaveSeconds, Is.EqualTo(64 * 60f / 122.5f).Within(1e-4f));   // 16 bars of the floor song
            Assert.That(FloorRules.DoorTelegraphSeconds, Is.EqualTo(2f));
            Assert.That(FloorRules.WaveCap, Is.EqualTo(10));
            Assert.That(FloorRules.MinBearingSpread, Is.EqualTo(90f));
            Assert.That(FloorRules.MinFromLastWave, Is.EqualTo(120f));
            Assert.That(FloorRules.MinSpawnDistance, Is.EqualTo(15f));
            Assert.That(FloorRules.ViewExclusionRange, Is.EqualTo(25f));
            Assert.That(FloorRules.MinExits, Is.EqualTo(2));
            Assert.That(FloorRules.ExitRadius, Is.EqualTo(10f));
            Assert.That(FloorRules.MaxSeenShare, Is.EqualTo(0.7f));
            Assert.That(FloorRules.MinCircleExposure, Is.EqualTo(0.5f));
            Assert.That(FloorRules.ExposureBearingSpread, Is.EqualTo(90f));
        }

        static readonly string[] Enemies =
            { SkylineEnemies.DroneId, SkylineEnemies.FloorCareId, SkylineEnemies.GondolaId, SkylineEnemies.SpotlightId };

        // rows: enemy, then the doors it may use in the order ShopShutter, SkylightVent, ServiceHatch, RailEnd
        [TestCase(SkylineEnemies.DroneId, true, true, false, false)]
        [TestCase(SkylineEnemies.FloorCareId, true, false, true, false)]
        [TestCase(SkylineEnemies.GondolaId, false, false, false, true)]
        [TestCase(SkylineEnemies.SpotlightId, false, false, false, false)]
        public void DoorAllowsMatchesTheSpec(string enemy, bool shutter, bool vent, bool hatch, bool rail)
        {
            Assert.That(FloorRules.DoorAllows(DoorKind.ShopShutter, enemy), Is.EqualTo(shutter));
            Assert.That(FloorRules.DoorAllows(DoorKind.SkylightVent, enemy), Is.EqualTo(vent));
            Assert.That(FloorRules.DoorAllows(DoorKind.ServiceHatch, enemy), Is.EqualTo(hatch));
            Assert.That(FloorRules.DoorAllows(DoorKind.RailEnd, enemy), Is.EqualTo(rail));
        }

        [Test]
        public void EveryDoorKindHasABotAndEveryFloorBotHasADoor()
        {
            foreach (DoorKind k in Enum.GetValues(typeof(DoorKind)))
            {
                bool any = false;
                foreach (var e in Enemies) any |= FloorRules.DoorAllows(k, e);
                Assert.That(any, Is.True, k + " is used by no bot");
            }
            Assert.That(FloorRules.DoorAllows(DoorKind.ShopShutter, "nobody"), Is.False, "an unknown id is allowed nowhere");
            Assert.That(FloorRules.DoorAllows(DoorKind.ShopShutter, null), Is.False);
        }

        static CoverBox Box(float x0, float y0, float z0, float x1, float y1, float z1) =>
            new CoverBox { min = new Vector3(x0, y0, z0), max = new Vector3(x1, y1, z1) };

        [Test]
        public void ATopOnAFlatSolidIsItsMaxY()
        {
            var s = new FloorSolid { id = "deck", role = "deck", box = Box(0, 5.6f, 0, 10, 6f, 10) };
            Assert.That(s.TopAt(3f, 4f), Is.EqualTo(6f));
            Assert.That(s.TopAt(-50f, 99f), Is.EqualTo(6f), "outside the box it is still the box's top");
        }

        [Test]
        public void ARampInterpolatesAlongZ()
        {
            // descends toward +Z: 0 m at z 4, -4 m at z 10 (the pit's north ramp climbs the other way)
            var s = new FloorSolid { id = "r", role = "ramp", ramp = true, rampAlongX = false, rampTopStart = 0f, rampTopEnd = -4f,
                box = Box(-4, -4, 4, 0, 0, 10) };
            Assert.That(s.TopAt(-2f, 4f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(s.TopAt(-2f, 7f), Is.EqualTo(-2f).Within(1e-5f));
            Assert.That(s.TopAt(-2f, 10f), Is.EqualTo(-4f).Within(1e-5f));
            Assert.That(s.TopAt(-2f, 100f), Is.EqualTo(-4f).Within(1e-5f), "clamped past the end");
            Assert.That(s.TopAt(99f, 7f), Is.EqualTo(-2f).Within(1e-5f), "x does not matter on a z ramp");
        }

        [Test]
        public void ARampInterpolatesAlongXWhenItRunsAlongX()
        {
            var s = new FloorSolid { id = "r", role = "ramp", ramp = true, rampAlongX = true, rampTopStart = -4f, rampTopEnd = 0f,
                box = Box(-16, -4, -2, -10, 0, 2) };
            Assert.That(s.TopAt(-16f, 0f), Is.EqualTo(-4f).Within(1e-5f));
            Assert.That(s.TopAt(-13f, 99f), Is.EqualTo(-2f).Within(1e-5f), "z does not matter on an x ramp");
            Assert.That(s.TopAt(-10f, 0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(s.TopAt(-200f, 0f), Is.EqualTo(-4f).Within(1e-5f));
        }

        [Test]
        public void ADegenerateRampDoesNotDivideByZero()
        {
            var s = new FloorSolid { ramp = true, rampTopStart = 1f, rampTopEnd = 3f, box = Box(0, 0, 5, 1, 3, 5) };
            Assert.That(s.TopAt(0f, 5f), Is.EqualTo(1f));
        }

        static readonly BeatTrigger[] Reactions = { BeatTrigger.AssetTowed, BeatTrigger.MeterStarted, BeatTrigger.FirstSpecialAmmo };
        static readonly BeatTrigger[] Notices =
            { BeatTrigger.WaveAnnounce, BeatTrigger.AlarmRaised, BeatTrigger.AllTowed, BeatTrigger.FloorStart };
        static readonly BeatTrigger[] Lessons = { BeatTrigger.CircleNear, BeatTrigger.LobbyDesk, BeatTrigger.LobbyLiftNear };
        static readonly BeatTrigger[] Hints = { BeatTrigger.LobbyArrive, BeatTrigger.LiftRiding };

        [Test]
        public void TheNewBeatTriggersHaveTheirUrgencyClasses()
        {
            foreach (var t in Reactions) Assert.That(TeachingQueue.UrgencyOf(t), Is.EqualTo(LineUrgency.Reaction), t.ToString());
            foreach (var t in Notices) Assert.That(TeachingQueue.UrgencyOf(t), Is.EqualTo(LineUrgency.Notice), t.ToString());
            foreach (var t in Lessons) Assert.That(TeachingQueue.UrgencyOf(t), Is.EqualTo(LineUrgency.Lesson), t.ToString());
            foreach (var t in Hints) Assert.That(TeachingQueue.UrgencyOf(t), Is.EqualTo(LineUrgency.Hint), t.ToString());
            Assert.That(Reactions.Length + Notices.Length + Lessons.Length + Hints.Length, Is.EqualTo(12), "all twelve new triggers");
        }

        [Test]
        public void TheOldBeatTriggersKeepTheirUrgency()
        {
            Assert.That(TeachingQueue.UrgencyOf(BeatTrigger.WarmupStart), Is.EqualTo(LineUrgency.Hint));
            Assert.That(TeachingQueue.UrgencyOf(BeatTrigger.PracticeBotSeen), Is.EqualTo(LineUrgency.Lesson));
            Assert.That(TeachingQueue.UrgencyOf(BeatTrigger.FirstSpotCreated), Is.EqualTo(LineUrgency.Reaction));
            Assert.That(TeachingQueue.UrgencyOf(BeatTrigger.FirstSeen), Is.EqualTo(LineUrgency.Notice));
            Assert.That(TeachingQueue.UrgencyOf(BeatTrigger.LiftOpened), Is.EqualTo(LineUrgency.Notice));
        }

        [Test]
        public void UrgencyOfNeverThrowsForAnyTrigger()
        {
            foreach (BeatTrigger t in Enum.GetValues(typeof(BeatTrigger)))
                Assert.DoesNotThrow(() => TeachingQueue.UrgencyOf(t), t.ToString());
        }
    }
}
