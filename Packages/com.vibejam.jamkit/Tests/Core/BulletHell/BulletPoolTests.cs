using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.BulletHell;
using JamKit.Patterns;
using NUnit.Framework;

namespace JamKit.Tests.BulletHell
{
    public class BulletPoolTests
    {
        static readonly PlayfieldBounds Box = PlayfieldBounds.Centered(6f, 5f);
        const float Up = (float)(Math.PI / 2);

        [Test]
        public void StraightBulletsMoveAtTheirSpeed()
        {
            var pool = new BulletPool(8);
            pool.Spawn(Vector2.Zero, 0f, 2f, 0, 0.1f);
            for (int i = 0; i < 10; i++) pool.Step(0.1f, Box);
            Assert.That(pool[0].position.X, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(pool[0].position.Y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(pool[0].age, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void CurvingBulletsMatchBulletMotion()
        {
            var pool = new BulletPool(4);
            pool.Spawn(Vector2.Zero, 0f, 3f, 0, 0.1f, acceleration: 1f, angularVelocity: 0.8f);
            Vector2 p = Vector2.Zero;
            float a = 0f, s = 3f;
            for (int i = 0; i < 20; i++)
            {
                pool.Step(0.05f, Box);
                BulletMotion.Step(ref p, ref a, ref s, 1f, 0.8f, 0.05f);
            }
            Assert.That(Vector2.Distance(pool[0].position, p), Is.LessThan(1e-4f));
            Assert.That(pool[0].angle, Is.EqualTo(a).Within(1e-5f));
        }

        [Test]
        public void ExpiredAndOffscreenBulletsAreCulled()
        {
            var pool = new BulletPool(8);
            pool.Spawn(Vector2.Zero, 0f, 0f, 0, 0.1f, lifetime: 0.5f);       // expires
            pool.Spawn(new Vector2(5.9f, 0f), 0f, 10f, 0, 0.1f);           // flies off the right side
            pool.Spawn(Vector2.Zero, Up, 0.1f, 0, 0.1f);                   // stays
            for (int i = 0; i < 10; i++) pool.Step(0.1f, Box, 0.5f);
            Assert.That(pool.Count, Is.EqualTo(1));
            Assert.That(pool[0].position.Y, Is.EqualTo(0.1f).Within(1e-4f));
        }

        [Test]
        public void SwapRemoveStillStepsEveryBulletOnce()
        {
            var pool = new BulletPool(8);
            pool.Spawn(Vector2.Zero, 0f, 0f, 0, 0.1f, lifetime: 0.05f);  // dies on the first step
            pool.Spawn(Vector2.Zero, 0f, 1f, 0, 0.1f);
            pool.Spawn(Vector2.Zero, 0f, 2f, 0, 0.1f);
            pool.Step(0.5f, Box);
            Assert.That(pool.Count, Is.EqualTo(2));
            float sum = pool[0].position.X + pool[1].position.X;
            Assert.That(sum, Is.EqualTo(1.5f).Within(1e-4f), "each survivor moved exactly once");
        }

        [Test]
        public void FullPoolDropsAndCounts()
        {
            var pool = new BulletPool(2);
            Assert.That(pool.Spawn(Vector2.Zero, 0f, 1f, 0, 0.1f), Is.True);
            Assert.That(pool.Spawn(Vector2.Zero, 0f, 1f, 0, 0.1f), Is.True);
            Assert.That(pool.Spawn(Vector2.Zero, 0f, 1f, 0, 0.1f), Is.False);
            Assert.That(pool.Dropped, Is.EqualTo(1));
        }

        [Test]
        public void AddSpawnsUsesStyleRadiiAndSkipsDeadSpawns()
        {
            var pool = new BulletPool(8);
            var spawns = new List<BulletSpawn>
            {
                new BulletSpawn { position = Vector2.Zero, speed = 1f, styleIndex = 1, lifetime = 5f, age = 0.1f },
                new BulletSpawn { position = Vector2.Zero, speed = 1f, styleIndex = 9, lifetime = 0f },
                new BulletSpawn { position = Vector2.Zero, speed = 1f, styleIndex = 0, lifetime = 1f, age = 2f },
            };
            int added = pool.AddSpawns(spawns, new[] { 0.1f, 0.3f }, 1f, 0.2f);
            Assert.That(added, Is.EqualTo(2));
            Assert.That(pool[0].radius, Is.EqualTo(0.3f));
            Assert.That(pool[0].lifeLeft, Is.EqualTo(4.9f).Within(1e-5f));
            Assert.That(pool[1].radius, Is.EqualTo(0.2f), "unknown style gets the fallback radius");
            Assert.That(float.IsPositiveInfinity(pool[1].lifeLeft), Is.True);
        }

        [Test]
        public void HitTestUsesBothRadii()
        {
            var pool = new BulletPool(4);
            pool.Spawn(new Vector2(0.25f, 0f), 0f, 0f, 0, 0.1f);
            Assert.That(pool.FirstOverlap(Vector2.Zero, 0.1f), Is.EqualTo(-1), "0.25 apart, radii sum 0.2");
            Assert.That(pool.FirstOverlap(Vector2.Zero, 0.16f), Is.EqualTo(0));
        }

        [Test]
        public void GrazeCountsEachBulletOnceAndNotHits()
        {
            var pool = new BulletPool(4);
            pool.Spawn(new Vector2(0.5f, 0f), 0f, 0f, 0, 0.1f);   // in the ring
            pool.Spawn(new Vector2(0.05f, 0f), 0f, 0f, 0, 0.1f);  // a hit, not a graze
            pool.Spawn(new Vector2(3f, 0f), 0f, 0f, 0, 0.1f);     // far
            Assert.That(pool.Graze(Vector2.Zero, 0.6f, 0.08f), Is.EqualTo(1));
            Assert.That(pool.Graze(Vector2.Zero, 0.6f, 0.08f), Is.EqualTo(0), "already grazed");
        }

        [Test]
        public void ClearInCircleRemovesOnlyInside()
        {
            var pool = new BulletPool(8);
            pool.Spawn(new Vector2(1f, 0f), 0f, 0f, 0, 0.1f);
            pool.Spawn(new Vector2(0f, 2.05f), 0f, 0f, 0, 0.1f);
            pool.Spawn(new Vector2(4f, 0f), 0f, 0f, 0, 0.1f);
            var removed = new List<Vector2>();
            Assert.That(pool.ClearInCircle(Vector2.Zero, 2f, removed), Is.EqualTo(2));
            Assert.That(removed.Count, Is.EqualTo(2));
            Assert.That(pool.Count, Is.EqualTo(1));
            Assert.That(pool[0].position.X, Is.EqualTo(4f));
            Assert.That(pool.Clear(), Is.EqualTo(1));
            Assert.That(pool.Count, Is.EqualTo(0));
        }

        [Test]
        public void ShotsHitTargetsAndPierceOncePerTarget()
        {
            var pool = new BulletPool(8);
            pool.Spawn(new Vector2(0f, 0f), Up, 0f, 0, 0.1f, damage: 2f);
            pool.Spawn(new Vector2(0f, 0f), Up, 0f, 0, 0.1f, damage: 1f, pierce: 1);
            pool.Spawn(new Vector2(5f, 0f), Up, 0f, 0, 0.1f);
            var targets = new HitTarget[]
            {
                new HitTarget { position = new Vector2(0.2f, 0f), radius = 0.3f, id = 1, active = true },
                new HitTarget { position = new Vector2(5f, 0f), radius = 0.3f, id = 2, active = false },
            };
            var hits = new List<BulletHit>();
            Assert.That(pool.CollideTargets(targets, 2, hits), Is.EqualTo(2));
            Assert.That(pool.Count, Is.EqualTo(2), "the plain shot is used up, the piercing one goes on");
            Assert.That(pool.CollideTargets(targets, 2, hits), Is.EqualTo(0), "a piercing shot doesn't hit the same target twice");
            targets[1].active = true;
            Assert.That(pool.CollideTargets(targets, 2, hits), Is.EqualTo(1), "inactive targets are skipped until active");
        }

        [Test]
        public void HomingTurnsTowardTheNearestTarget()
        {
            var pool = new BulletPool(4);
            pool.Spawn(Vector2.Zero, Up, 5f, 0, 0.1f, flags: BulletFlags.Homing);
            pool.Spawn(Vector2.Zero, Up, 5f, 0, 0.1f);
            var targets = new[] { new HitTarget { position = new Vector2(3f, 0f), radius = 0.3f, id = 1, active = true } };
            pool.SteerHoming(targets, 1, 2f, 0.1f);
            Assert.That(pool[0].angle, Is.EqualTo(Up - 0.2f).Within(1e-4f), "turned right by rate x dt");
            Assert.That(pool[1].angle, Is.EqualTo(Up), "non-homing bullets ignore targets");
            pool.Step(0.1f, Box);
            Assert.That(pool[0].position.X, Is.GreaterThan(0f), "the cached heading follows the turn");
        }

        [Test]
        public void ThreeThousandBulletsStepWithoutGrowing()
        {
            var pool = new BulletPool(4096);
            for (int i = 0; i < 3000; i++)
                pool.Spawn(Vector2.Zero, i * 0.01f, 0.5f + (i % 7) * 0.1f, i % 5, 0.1f, angularVelocity: (i % 3) * 0.2f);
            for (int f = 0; f < 120; f++) pool.Step(1f / 60f, PlayfieldBounds.Centered(50f, 50f));
            Assert.That(pool.Count, Is.EqualTo(3000));
            Assert.That(pool.Dropped, Is.EqualTo(0));
        }
    }
}
