using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Combat;
using JamKit.Doomlike;
using JamKit.Roguelite;
using NUnit.Framework;

namespace JamKit.Tests.Doomlike
{
    public class AngleFrameTests
    {
        const float D = GridMath.Deg;

        [Test]
        public void FrontBackAndSides()
        {
            // Facing east (0). Camera east of it = in front.
            Assert.That(AngleFrame.Frame(0f, 0f), Is.EqualTo(0), "front");
            Assert.That(AngleFrame.Frame(0f, 180f * D), Is.EqualTo(4), "back");
            Assert.That(AngleFrame.Frame(0f, 90f * D), Is.EqualTo(2), "camera to its left (north)");
            Assert.That(AngleFrame.Frame(0f, -90f * D), Is.EqualTo(6), "camera to its right (south)");
            Assert.That(AngleFrame.Frame(0f, 45f * D), Is.EqualTo(1));
            Assert.That(AngleFrame.Frame(0f, -45f * D), Is.EqualTo(7));
        }

        [Test]
        public void RoundsToTheNearestFrameAndWraps()
        {
            Assert.That(AngleFrame.Frame(0f, 22f * D), Is.EqualTo(0));
            Assert.That(AngleFrame.Frame(0f, 23f * D), Is.EqualTo(1));
            Assert.That(AngleFrame.Frame(0f, 350f * D), Is.EqualTo(0), "-10 degrees is still the front");
            Assert.That(AngleFrame.Frame(10f * D * 36f, 0f), Is.EqualTo(0), "a full turn of facing");
            Assert.That(AngleFrame.Frame(90f * D, 90f * D + 180f * D), Is.EqualTo(4));
        }

        [Test]
        public void EveryFrameAppearsOnceAroundACircle()
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < 8; i++) seen.Add(AngleFrame.Frame(1.234f, 1.234f + i * 45f * D));
            Assert.That(seen.Count, Is.EqualTo(8));
        }

        [Test]
        public void FromPositions()
        {
            var enemy = new Vector2(5f, 5f);
            // Facing north (90): a camera due north sees the front, due south the back.
            Assert.That(AngleFrame.Frame(90f * D, enemy, new Vector2(5f, 20f)), Is.EqualTo(0));
            Assert.That(AngleFrame.Frame(90f * D, enemy, new Vector2(5f, -20f)), Is.EqualTo(4));
            Assert.That(AngleFrame.Frame(90f * D, enemy, new Vector2(-10f, 5f)), Is.EqualTo(2), "west of a north-facing enemy is its left");
            Assert.That(AngleFrame.Frame(90f * D, enemy, enemy), Is.EqualTo(0), "same spot: front");
        }

        [Test]
        public void FiveDirectionSheetsMirrorTheRightSide()
        {
            bool mirror;
            Assert.That(AngleFrame.SheetColumn(0, 5, out mirror), Is.EqualTo(0)); Assert.That(mirror, Is.False);
            Assert.That(AngleFrame.SheetColumn(4, 5, out mirror), Is.EqualTo(4)); Assert.That(mirror, Is.False);
            Assert.That(AngleFrame.SheetColumn(5, 5, out mirror), Is.EqualTo(3)); Assert.That(mirror, Is.True);
            Assert.That(AngleFrame.SheetColumn(6, 5, out mirror), Is.EqualTo(2)); Assert.That(mirror, Is.True);
            Assert.That(AngleFrame.SheetColumn(7, 5, out mirror), Is.EqualTo(1)); Assert.That(mirror, Is.True);
            Assert.That(AngleFrame.SheetColumn(6, 8, out mirror), Is.EqualTo(6)); Assert.That(mirror, Is.False);
            Assert.That(AngleFrame.SheetColumn(6, 1, out mirror), Is.EqualTo(0)); Assert.That(mirror, Is.False);
            Assert.That(AngleFrame.SheetColumn(-1, 8, out mirror), Is.EqualTo(7));
        }
    }

    public class GridMathTests
    {
        [Test]
        public void CellsRoundTripThroughWorldSpace()
        {
            var c = GridMath.CellCenter(3, 1, 10, 2.5f);
            Assert.That(c.X, Is.EqualTo(8.75f).Within(1e-4f));
            Assert.That(c.Y, Is.EqualTo(21.25f).Within(1e-4f), "row 1 of 10 is near the north edge");
            Assert.That(GridMath.WorldToCell(c, 8, 10, 2.5f, out int x, out int y), Is.True);
            Assert.That(x, Is.EqualTo(3));
            Assert.That(y, Is.EqualTo(1));
            Assert.That(GridMath.WorldToCell(new Vector2(-1f, 5f), 8, 10, 2.5f, out _, out _), Is.False);
        }

        [Test]
        public void YawAndAngleAgree()
        {
            Assert.That(GridMath.YawFromAngle(0f), Is.EqualTo(90f).Within(1e-4f), "east is yaw 90");
            Assert.That(GridMath.YawFromAngle(90f * GridMath.Deg), Is.EqualTo(0f).Within(1e-4f), "north is yaw 0");
            Assert.That(GridMath.AngleFromYaw(180f), Is.EqualTo(-90f * GridMath.Deg).Within(1e-4f), "yaw 180 faces south");
            Assert.That(GridMath.WrapSigned(GridMath.TwoPi + 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(GridMath.WrapSigned(-0.5f), Is.EqualTo(-0.5f).Within(1e-4f));
        }
    }

    public class AmmoPoolsTests
    {
        static List<AmmoTypeDef> Types() => new List<AmmoTypeDef>
        {
            new AmmoTypeDef { id = "bullets", start = 50, max = 200 },
            new AmmoTypeDef { id = "shells", start = 0, max = 50 },
        };

        [Test]
        public void StartsAddsCapsAndSpends()
        {
            var p = new AmmoPools(Types());
            int b = p.IndexOf("bullets"), s = p.IndexOf("SHELLS");
            Assert.That(b, Is.EqualTo(0));
            Assert.That(s, Is.EqualTo(1), "ids ignore case");
            Assert.That(p.IndexOf("rockets"), Is.EqualTo(-1));
            Assert.That(p.Amount(b), Is.EqualTo(50));
            Assert.That(p.Add(s, 8), Is.EqualTo(8));
            Assert.That(p.Add(s, 100), Is.EqualTo(42), "only up to the max");
            Assert.That(p.Add(s, 1), Is.EqualTo(0), "full: the pickup stays");
            Assert.That(p.IsFull(s), Is.True);
            Assert.That(p.Spend(b, 60), Is.False, "can't overspend");
            Assert.That(p.Spend(b, 10), Is.True);
            Assert.That(p.Amount(b), Is.EqualTo(40));
            Assert.That(p.Spend(-1, 999), Is.True, "no ammo type (fists) is free");
        }

        [Test]
        public void CapacityScaleRaisesTheMax()
        {
            var p = new AmmoPools(Types());
            p.CapacityScale = 1.5f;
            Assert.That(p.Max(1), Is.EqualTo(75));
            p.Add(1, 100);
            Assert.That(p.Amount(1), Is.EqualTo(75));
            p.CapacityScale = 1f;
            Assert.That(p.Amount(1), Is.EqualTo(75), "lowering the cap never takes ammo away");
            Assert.That(p.Add(1, 5), Is.EqualTo(0));
        }

        [Test]
        public void TwoGunsShareOnePool()
        {
            var p = new AmmoPools(Types());
            var pistol = new WeaponState(new WeaponDef { magazineSize = 0, startingReserve = 0, fireMode = FireMode.Semi, fireInterval = 0.2f });
            var chaingun = new WeaponState(new WeaponDef { magazineSize = 0, startingReserve = 0, fireMode = FireMode.Auto, fireInterval = 0.1f });
            int b = p.IndexOf("bullets");

            p.Feed(pistol, b);
            Assert.That(pistol.Tick(0.016f, true, true), Is.EqualTo(1));
            p.Collect(pistol, b);
            Assert.That(p.Amount(b), Is.EqualTo(49));

            p.Feed(chaingun, b);
            int shots = 0;
            for (int i = 0; i < 100; i++) shots += chaingun.Tick(0.05f, true);
            p.Collect(chaingun, b);
            Assert.That(shots, Is.EqualTo(49), "the chaingun empties what the pistol left");
            Assert.That(p.Amount(b), Is.EqualTo(0));

            p.Feed(pistol, b);
            int dry = 0;
            pistol.DryFired += () => dry++;
            Assert.That(pistol.Tick(0.5f, true, true), Is.EqualTo(0));
            Assert.That(dry, Is.EqualTo(1), "empty pool: the dry click");
        }

        [Test]
        public void ResetAndFill()
        {
            var p = new AmmoPools(Types());
            p.Spend(0, 50);
            p.FillAll();
            Assert.That(p.Amount(0), Is.EqualTo(200));
            Assert.That(p.Amount(1), Is.EqualTo(50));
            p.Reset();
            Assert.That(p.Amount(0), Is.EqualTo(50));
            Assert.That(p.Amount(1), Is.EqualTo(0));
            Assert.That(p.ShotsLeft(0, 2), Is.EqualTo(25));
        }
    }

    public class HitscanMathTests
    {
        [Test]
        public void FalloffIsFullThenLinearThenZero()
        {
            Assert.That(HitscanMath.Falloff(5f, 40f, 10f, 0.4f), Is.EqualTo(1f));
            Assert.That(HitscanMath.Falloff(10f, 40f, 10f, 0.4f), Is.EqualTo(1f));
            Assert.That(HitscanMath.Falloff(25f, 40f, 10f, 0.4f), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(HitscanMath.Falloff(40f, 40f, 10f, 0.4f), Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(HitscanMath.Falloff(41f, 40f, 10f, 0.4f), Is.EqualTo(0f), "beyond range");
            Assert.That(HitscanMath.Falloff(1000f, 0f, 10f, 0.4f), Is.EqualTo(1f), "no range = no falloff");
        }

        [Test]
        public void PelletsAreDeterministicAndInsideTheCone()
        {
            var a = new List<Vector2>();
            var b = new List<Vector2>();
            HitscanMath.PelletOffsets(8, 12f, 6f, new Rng(99), a);
            HitscanMath.PelletOffsets(8, 12f, 6f, new Rng(99), b);
            Assert.That(a.Count, Is.EqualTo(8));
            Assert.That(a, Is.EqualTo(b), "same seed, same pattern");
            float hx = 6f * GridMath.Deg, hy = 3f * GridMath.Deg;
            foreach (var o in a)
            {
                float e = (o.X / hx) * (o.X / hx) + (o.Y / hy) * (o.Y / hy);
                Assert.That(e, Is.LessThanOrEqualTo(1.0001f), "inside the spread ellipse");
            }
            var c = new List<Vector2>();
            HitscanMath.PelletOffsets(8, 12f, 6f, new Rng(100), c);
            Assert.That(c, Is.Not.EqualTo(a), "another seed, another pattern");
        }

        [Test]
        public void FirstPelletCanBeDeadCentre()
        {
            var o = new List<Vector2>();
            HitscanMath.PelletOffsets(1, 10f, 10f, new Rng(5), o, true);
            Assert.That(o, Is.EqualTo(new[] { Vector2.Zero }));
            HitscanMath.PelletOffsets(0, 10f, 10f, new Rng(5), o);
            Assert.That(o.Count, Is.EqualTo(1), "at least one pellet");
        }

        [Test]
        public void OffsetTurnsTheDirection()
        {
            var f = new Vector3(0f, 0f, 1f);
            var r = new Vector3(1f, 0f, 0f);
            var u = new Vector3(0f, 1f, 0f);
            Assert.That(HitscanMath.Offset(f, r, u, Vector2.Zero), Is.EqualTo(f));
            var d = HitscanMath.Offset(f, r, u, new Vector2(45f * GridMath.Deg, 0f));
            Assert.That(d.X, Is.EqualTo(d.Z).Within(1e-4f), "45 degrees to the right");
            Assert.That(d.Length(), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void SplashAndRolls()
        {
            Assert.That(HitscanMath.Splash(100f, 4f, 0f), Is.EqualTo(100f));
            Assert.That(HitscanMath.Splash(100f, 4f, 4f), Is.EqualTo(25f).Within(1e-3f));
            Assert.That(HitscanMath.Splash(100f, 4f, 4.1f), Is.EqualTo(0f));
            var rng = new Rng(3);
            for (int i = 0; i < 200; i++)
                Assert.That(HitscanMath.Roll(10f, 0.2f, rng), Is.InRange(8f, 12f));
            Assert.That(HitscanMath.Roll(10f, 0f, rng), Is.EqualTo(10f));
        }

        [Test]
        public void EnemyAimGetsWorseWithDistanceAndSpeed()
        {
            float near = HitscanMath.EnemyHitChance(0.8f, 2f, 0f);
            float far = HitscanMath.EnemyHitChance(0.8f, 30f, 0f);
            float strafing = HitscanMath.EnemyHitChance(0.8f, 2f, 9f);
            Assert.That(near, Is.GreaterThan(far));
            Assert.That(near, Is.GreaterThan(strafing));
            Assert.That(near, Is.LessThanOrEqualTo(0.8f));
        }
    }

    public class EnemyBrainTests
    {
        static EnemyBrainDef Def() => new EnemyBrainDef
        {
            sightRange = 20f, fovDegrees = 180f, hearingRange = 15f, reactionSeconds = 0.5f,
            attackRange = 10f, attackCooldown = 1f, aggression = 1000f,
            windupSeconds = 0.4f, fireSeconds = 0.1f, recoverSeconds = 0.3f,
            painChance = 1f, painSeconds = 0.3f,
        };

        static BrainSenses See(float distance, float off = 0f) =>
            new BrainSenses { canSee = true, distance = distance, angleOffFacing = off };

        static void Run(EnemyBrain b, float seconds, BrainSenses s, Rng rng)
        {
            int ticks = (int)Math.Round(seconds / 0.05f);
            for (int i = 0; i < ticks; i++) b.Tick(0.05f, s, rng);
        }

        [Test]
        public void SeesThenReactsThenChases()
        {
            var b = new EnemyBrain(Def());
            var rng = new Rng(1);
            int alerts = 0;
            b.Alerted += () => alerts++;
            b.Tick(0.05f, See(15f), rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Alert));
            Assert.That(alerts, Is.EqualTo(1));
            Assert.That(b.WantsToMove, Is.False, "turning and shouting, not walking yet");
            Run(b, 0.55f, See(15f), rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(b.WantsToMove, Is.True);
        }

        [Test]
        public void DoesntSeeBehindItOrTooFar()
        {
            var b = new EnemyBrain(Def());
            var rng = new Rng(1);
            Run(b, 1f, See(15f, 120f * GridMath.Deg), rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Idle), "outside its 180 degree view");
            Run(b, 1f, See(25f), rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Idle), "beyond sight range");
            Run(b, 1f, new BrainSenses { canSee = false, distance = 5f }, rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Idle), "no line of sight");
        }

        [Test]
        public void GunfireWakesItThroughWalls()
        {
            var b = new EnemyBrain(Def());
            b.Tick(0.05f, new BrainSenses { canSee = false, distance = 12f, heardNoise = true }, new Rng(1));
            Assert.That(b.State, Is.EqualTo(EnemyState.Alert));
            var far = new EnemyBrain(Def());
            far.Tick(0.05f, new BrainSenses { canSee = false, distance = 30f, heardNoise = true }, new Rng(1));
            Assert.That(far.State, Is.EqualTo(EnemyState.Idle), "too far to hear");
        }

        [Test]
        public void AttackTelegraphsFiresAndRecovers()
        {
            var b = new EnemyBrain(Def());
            var rng = new Rng(2);
            var events = new List<string>();
            b.AttackStarted += i => events.Add("start");
            b.AttackFired += i => events.Add("fire");
            b.AttackEnded += () => events.Add("end");
            b.Alert();
            Run(b, 0.55f, See(8f), rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Chase));
            Run(b, 0.3f, See(8f), rng); // the wake-up grace (0.25 s) runs out
            Assert.That(b.State, Is.EqualTo(EnemyState.Attack), "in range, in sight, ready");
            Assert.That(b.Phase, Is.EqualTo(AttackPhase.Windup));
            Assert.That(events, Is.EqualTo(new[] { "start" }), "the telegraph comes before the hit");
            Run(b, 0.4f, See(8f), rng);
            Assert.That(events, Is.EqualTo(new[] { "start", "fire" }));
            Run(b, 0.5f, See(8f), rng);
            Assert.That(events, Is.EqualTo(new[] { "start", "fire", "end" }));
            Assert.That(b.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(b.CooldownLeft, Is.GreaterThan(0f), "cooling down");
        }

        [Test]
        public void NoAttackOutOfRangeOrSight()
        {
            var b = new EnemyBrain(Def());
            var rng = new Rng(3);
            b.Alert();
            Run(b, 3f, See(15f), rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Chase), "too far: keeps chasing");
            Run(b, 3f, new BrainSenses { canSee = false, distance = 5f }, rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Chase), "can't see: no attack");
        }

        [Test]
        public void PainInterruptsTheWindup()
        {
            var b = new EnemyBrain(Def());
            var rng = new Rng(4);
            int fired = 0, pains = 0;
            b.AttackFired += i => fired++;
            b.PainStarted += () => pains++;
            b.Alert();
            Run(b, 0.85f, See(8f), rng);
            Assert.That(b.Phase, Is.EqualTo(AttackPhase.Windup));
            b.Damaged(0.5f, rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Pain));
            Assert.That(pains, Is.EqualTo(1));
            Run(b, 0.35f, See(8f), rng);
            Assert.That(b.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(fired, Is.EqualTo(0), "the interrupted attack never landed");
            b.Damaged(0.4f, rng);
            Assert.That(pains, Is.EqualTo(1), "can't be stun-locked: pain has a cooldown");
        }

        [Test]
        public void DamageWakesItAtOnce()
        {
            var def = Def();
            def.painChance = 0f;
            var b = new EnemyBrain(def);
            b.Damaged(0.9f, new Rng(1));
            Assert.That(b.State, Is.EqualTo(EnemyState.Chase), "shot in the back: straight to chasing");
        }

        [Test]
        public void EnragesBelowItsThreshold()
        {
            var def = Def();
            def.enrageBelow = 0.5f;
            def.painChance = 0f;
            var b = new EnemyBrain(def);
            b.Damaged(0.6f, new Rng(1));
            Assert.That(b.Enraged, Is.False);
            b.Damaged(0.45f, new Rng(1));
            Assert.That(b.Enraged, Is.True);
            Assert.That(b.SpeedScale, Is.EqualTo(def.enrageSpeedScale));
        }

        [Test]
        public void DeadStaysDead()
        {
            var b = new EnemyBrain(Def());
            b.Kill();
            Run(b, 1f, See(5f), new Rng(1));
            b.Alert();
            b.Damaged(0.1f, new Rng(1));
            Assert.That(b.State, Is.EqualTo(EnemyState.Dead));
            Assert.That(b.WantsToMove, Is.False);
        }

        [Test]
        public void SecondaryAttackIsChosenByChance()
        {
            var b = new EnemyBrain(Def()) { secondaryChance = 1f };
            var rng = new Rng(5);
            int used = -1;
            b.AttackStarted += i => used = i;
            b.Alert();
            Run(b, 1f, See(8f), rng);
            Assert.That(used, Is.EqualTo(1));
        }
    }

    public class LevelTallyTests
    {
        [Test]
        public void PercentagesRoundDownAndEmptyIsFull()
        {
            Assert.That(LevelTally.Percent(1, 3), Is.EqualTo(33));
            Assert.That(LevelTally.Percent(3, 3), Is.EqualTo(100));
            Assert.That(LevelTally.Percent(0, 0), Is.EqualTo(100), "no secrets is not 0%");
            Assert.That(LevelTally.Percent(5, 3), Is.EqualTo(100), "never above 100");
            Assert.That(LevelTally.Percent(-1, 3), Is.EqualTo(0));
        }

        [Test]
        public void RunTotalAddsLevels()
        {
            var a = new LevelTally();
            a.Reset("One", 10, 2, 5, 90f);
            a.kills = 10; a.secrets = 1; a.time = 80f;
            var b = new LevelTally();
            b.Reset("Two", 20, 0, 5, 120f);
            b.kills = 5; b.items = 5; b.time = 130f;
            Assert.That(a.UnderPar, Is.True);
            Assert.That(b.UnderPar, Is.False);
            var total = new LevelTally();
            total.Add(a);
            total.Add(b);
            Assert.That(total.kills, Is.EqualTo(15));
            Assert.That(total.KillPercent, Is.EqualTo(50));
            Assert.That(total.SecretPercent, Is.EqualTo(50));
            Assert.That(total.time, Is.EqualTo(210f));
        }
    }

    public class PlayerMotionTests
    {
        static readonly PlayerMotionDef Def = new PlayerMotionDef { runSpeed = 10f, acceleration = 50f, friction = 40f };

        [Test]
        public void AcceleratesToTopSpeedAndStops()
        {
            var v = Vector2.Zero;
            var wish = new Vector2(0f, 1f);
            v = PlayerMotion.Step(v, wish, 10f, Def, 0.1f);
            Assert.That(v.Y, Is.EqualTo(5f).Within(1e-4f), "50 m/s^2 for 0.1 s");
            for (int i = 0; i < 10; i++) v = PlayerMotion.Step(v, wish, 10f, Def, 0.1f);
            Assert.That(v.Length(), Is.EqualTo(10f).Within(1e-4f), "never faster than the wished speed");
            for (int i = 0; i < 3; i++) v = PlayerMotion.Step(v, Vector2.Zero, 10f, Def, 0.1f);
            Assert.That(v.Length(), Is.EqualTo(0f).Within(1e-4f), "friction stops it in a quarter second");
        }

        [Test]
        public void FrameRateBarelyMatters()
        {
            Vector2 slow = Vector2.Zero, fast = Vector2.Zero;
            var wish = new Vector2(1f, 0f);
            for (int i = 0; i < 6; i++) slow = PlayerMotion.Step(slow, wish, 10f, Def, 1f / 30f);
            for (int i = 0; i < 24; i++) fast = PlayerMotion.Step(fast, wish, 10f, Def, 1f / 120f);
            Assert.That(slow.X, Is.EqualTo(fast.X).Within(0.01f));
        }

        [Test]
        public void KnockbackBleedsOff()
        {
            var v = new Vector2(30f, 0f);
            v = PlayerMotion.Step(v, new Vector2(1f, 0f), 10f, Def, 0.1f);
            Assert.That(v.X, Is.LessThan(30f));
            Assert.That(v.X, Is.GreaterThan(10f), "not snapped back at once");
        }

        [Test]
        public void WishFollowsYaw()
        {
            var w = PlayerMotion.WishFromInput(new Vector2(0f, 1f), 90f);
            Assert.That(w.X, Is.EqualTo(1f).Within(1e-4f), "yaw 90 walks east");
            var s = PlayerMotion.WishFromInput(new Vector2(1f, 0f), 0f);
            Assert.That(s.X, Is.EqualTo(1f).Within(1e-4f), "strafe right at yaw 0 is east");
        }
    }

    public class DoomStatsTests
    {
        [Test]
        public void BaseStatsAndUpgrades()
        {
            var stats = new StatBlock();
            DoomStats.SetBase(stats, 100f);
            Assert.That(stats.Get(DoomStats.Damage), Is.EqualTo(1f));
            Assert.That(stats.Get(DoomStats.MaxHealth), Is.EqualTo(100f));
            var inv = new UpgradeInventory(stats);
            var up = new UpgradeDef("thick", Rarity.Common, 3);
            up.modifiers.Add(new StatModifier(DoomStats.MaxHealth, StatOp.Flat, 25f));
            inv.Take(up);
            inv.Take(up);
            Assert.That(stats.Get(DoomStats.MaxHealth), Is.EqualTo(150f));
            var armor = new UpgradeDef("armor", Rarity.Epic, 0);
            armor.modifiers.Add(new StatModifier(DoomStats.Armor, StatOp.Flat, 0.5f));
            inv.Take(armor);
            inv.Take(armor);
            Assert.That(stats.Get(DoomStats.Armor), Is.EqualTo(0.75f), "armor is capped");
        }

        [Test]
        public void FireRateSpeedsUpACopyNotTheAsset()
        {
            var asset = new WeaponDef { magazineSize = 0, fireInterval = 0.2f, burstInterval = 0.05f };
            var live = DoomStats.Copy(asset);
            DoomStats.ApplyFireRate(asset, live, 2f);
            Assert.That(live.fireInterval, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(live.burstInterval, Is.EqualTo(0.025f).Within(1e-5f));
            Assert.That(asset.fireInterval, Is.EqualTo(0.2f));
            Assert.That(live.magazineSize, Is.EqualTo(0));
        }

        [Test]
        public void KnowsItsEffects()
        {
            Assert.That(DoomStats.IsKnownEffect("heal_full"), Is.True);
            Assert.That(DoomStats.IsKnownEffect("heal:25"), Is.True);
            Assert.That(DoomStats.HealAmount("heal:25"), Is.EqualTo(25f));
            Assert.That(DoomStats.IsKnownEffect("heal:x"), Is.False);
            Assert.That(DoomStats.IsKnownEffect("fly"), Is.False);
            Assert.That(DoomStats.IsKnownStat("fire_rate"), Is.True);
            Assert.That(DoomStats.IsKnownStat("luck"), Is.False);
        }
    }
}
