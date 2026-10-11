using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;
using JamKit.Stealth;
using NUnit.Framework;

namespace JamKit.Tests.Stealth
{
    /// <summary>The answers to "the movement trivializes stealth" (docs/STEALTH.md "When the movement outruns the
    /// guards"): A, loud movement and wary guards (a wider cone, a quicker turn, repeated noise escalates); B, the guards'
    /// call and the dream noticing speed; C2, being watched dims her lucidity. All off is the original kit.</summary>
    public class StealthAnswersTests
    {
        const float Dt = 1f / 60f;

        static SeekerBrain Guard(Vector3 at, float yaw = 0f, StealthRules rules = null)
        {
            var w = new StealthBoxWorld();
            var g = new WaypointGraph();
            g.Add(at);
            var b = new SeekerBrain(new StealthDef(), w, g, new List<HidingSpot>(), at, yaw) { rules = rules };
            return b;
        }

        static StealthTarget Mover(float x, float z, float speed)
        {
            var t = StealthTarget.At(new Vector3(x, 0f, z));
            t.speed = speed;
            return t;
        }

        // ---------- A: loud movement ----------

        [Test]
        public void LoudMovementIsHeardBeyondSightAndWalkingSlowlyStaysQuiet()
        {
            var def = new StealthDef();
            var feet = Vector3.Zero;
            float Radius(MoveMode mode, MoveEvents ev, float speed, bool crouched, bool loud)
            {
                var r = new StealthNoiseRules();
                float best = 0f;
                for (int i = 0; i < 60; i++) { var n = r.Tick(def, mode, i == 0 ? ev : default, feet, Dt, speed, crouched, loud); if (n.radius > best) best = n.radius; }
                return best;
            }
            var dj = new MoveEvents { doubleJumped = true };
            Assert.That(Radius(MoveMode.Air, dj, 9f, false, true), Is.GreaterThan(def.sightRange), "a double jump is heard beyond sight");
            Assert.That(Radius(MoveMode.Wallrun, default, 9f, false, true), Is.GreaterThan(def.sightRange), "so is a wallrun");
            float sprint = Radius(MoveMode.Sprint, default, 9.5f, false, true), fast = Radius(MoveMode.Sprint, default, 20f, false, true);
            Assert.That(fast, Is.GreaterThan(sprint + 5f), "sprinting is louder the faster you go");
            float walk = Radius(MoveMode.Grounded, default, 4f, false, true);
            Assert.That(walk, Is.InRange(1f, 4f), "walking is a small noise");
            Assert.That(Radius(MoveMode.Grounded, default, 2.5f, true, true), Is.EqualTo(0f), "crouch-walking is silent");
            Assert.That(Radius(MoveMode.Grounded, default, 4f, false, false), Is.EqualTo(0f), "the original kit: walking is silent");
            Assert.That(Radius(MoveMode.Air, dj, 9f, false, false), Is.EqualTo(0f), "and a double jump too");
        }

        [Test]
        public void AHeardNoiseMakesAGuardWaryNotAlertAndWidensItsCone()
        {
            var rules = new StealthRules { loudMovement = true };
            var g = Guard(Vector3.Zero, 0f, rules);
            // 50 degrees off: outside the 40 degree cone, inside a wary one (56).
            var side = Mover(10f * 0.766f, 10f * 0.643f, 2f);
            g.Tick(Dt, side, default);
            Assert.That(g.Zone, Is.Not.EqualTo(SightZone.Cone), "calm: only the periphery");
            g.Tick(Dt, default, StealthNoise.Make(new Vector3(0f, 0f, -6f), 12f, NoiseKind.Footsteps));
            Assert.That(g.Wary, Is.True);
            Assert.That(g.Events.becameWary, Is.True);
            Assert.That(g.State, Is.EqualTo(AlertState.Suspicious), "it goes to look");
            Assert.That(g.Meter, Is.LessThan(0.5f), "wary, not alert");
            var g2 = Guard(Vector3.Zero, 0f, rules);
            g2.Tick(Dt, default, StealthNoise.Make(new Vector3(0f, 0f, 20f), 30f, NoiseKind.Other));
            for (int i = 0; i < 40; i++) g2.Tick(Dt, default, default); // it faces the noise ahead (yaw 0)
            g2.Tick(Dt, side, default);
            Assert.That(g2.Zone, Is.EqualTo(SightZone.Cone), "wary: the wider cone sees it");
            // It stays wary for a while, then calms down.
            for (float t = 0f; t < g2.def.waryTime + 1f; t += Dt) g2.Tick(Dt, default, default);
            Assert.That(g2.Wary, Is.False);
            // The original kit: a noise makes it suspicious but never wary.
            var plain = Guard(Vector3.Zero);
            plain.Tick(Dt, default, StealthNoise.Make(new Vector3(0f, 0f, -6f), 12f, NoiseKind.Footsteps));
            Assert.That(plain.Wary, Is.False);
        }

        [Test]
        public void RepeatedNoiseEscalatesToAlert()
        {
            var g = Guard(Vector3.Zero, 0f, new StealthRules { loudMovement = true });
            var noise = StealthNoise.Make(new Vector3(0f, 0f, -8f), 15f, NoiseKind.Wallrun);
            bool escalated = false;
            for (int i = 0; i < g.def.escalateNoises; i++)
            {
                g.Tick(Dt, default, noise);
                escalated |= g.Events.escalated;
                for (int k = 0; k < 60; k++) g.Tick(Dt, default, default);
            }
            Assert.That(escalated, Is.True, $"{g.def.escalateNoises} noises in {g.def.escalateWindow} s");
            Assert.That(g.State, Is.EqualTo(AlertState.Searching), "alert: it searches the spot");
            Assert.That(g.Meter, Is.GreaterThanOrEqualTo(g.def.escalateMeter - 0.1f));
        }

        // ---------- B: the dream pushes back ----------

        [Test]
        public void ASuspiciousGuardCallsTheGuardsNearHim()
        {
            var rules = new StealthRules { dreamPushesBack = true };
            var a = Guard(Vector3.Zero, 0f, rules);
            var near = Guard(new Vector3(15f, 0f, 0f), 180f, rules);
            var far = Guard(new Vector3(80f, 0f, 0f), 180f, rules);
            var guards = new List<SeekerBrain> { a, near, far };
            var target = Mover(0f, 8f, 2f); // in front of a only
            bool called = false;
            for (float t = 0f; t < 3f && !called; t += Dt)
            {
                foreach (var g in guards) g.Tick(Dt, target, default);
                called = SeekerSquad.Propagate(guards, rules) > 0;
            }
            Assert.That(called, Is.True);
            Assert.That(near.State, Is.EqualTo(AlertState.Suspicious), "the near guard comes");
            Assert.That(near.Wary, Is.True);
            Assert.That(far.State, Is.EqualTo(AlertState.Unaware), "out of earshot of the call");
            Assert.That(SeekerSquad.Propagate(guards, new StealthRules()), Is.EqualTo(0), "off: no calls");
        }

        [Test]
        public void TheFasterYouMoveTheFartherTheDreamNoticesYou()
        {
            var on = new StealthRules { dreamPushesBack = true };
            var def = new StealthDef();
            Assert.That(on.NoticeScale(def, 9.5f), Is.EqualTo(1.27f).Within(0.01f));
            Assert.That(on.NoticeScale(def, 22f), Is.EqualTo(2.02f).Within(0.01f));
            Assert.That(new StealthRules().NoticeScale(def, 22f), Is.EqualTo(1f));
            var g = Guard(Vector3.Zero, 0f, on);
            g.Tick(Dt, Mover(0f, 24f, 2f), default);
            Assert.That(g.Visibility, Is.EqualTo(0f), "walking at 24 m: out of range");
            g.Tick(Dt, Mover(0f, 24f, 16f), default);
            Assert.That(g.Visibility, Is.GreaterThan(0f), "running at 16 m/s: the dream notices you from 24 m");
            var off = Guard(Vector3.Zero);
            off.Tick(Dt, Mover(0f, 24f, 16f), default);
            Assert.That(off.Visibility, Is.EqualTo(0f));
        }

        // ---------- C2: being watched dims her lucidity ----------

        [Test]
        public void NearAGuardTheDreamPowersFadeAndAwayTheyComeBack()
        {
            var def = new StealthDef();
            var guard = Guard(Vector3.Zero);
            var guards = new List<SeekerBrain> { guard };
            var fade = new LucidityFade();
            var near = new Vector3(0f, 0f, 6f);
            Assert.That(LucidityFade.IsWatched(def, near, guards), Is.True, "6 m from a guard");
            for (float t = 0f; t < def.lucidityFadeTime + 0.1f; t += Dt) fade.Tick(def, LucidityFade.IsWatched(def, near, guards), Dt);
            Assert.That(fade.Faded, Is.True);
            var p = fade.Profile;
            Assert.That(p, Is.Not.Null);
            Assert.That(p.doubleJump || p.wallrun || p.slide, Is.False, "no dream powers");
            Assert.That(p.speedCap, Is.EqualTo(def.watchedSpeedCap));
            var away = new Vector3(0f, 0f, 30f);
            Assert.That(LucidityFade.IsWatched(def, away, guards), Is.False);
            for (float t = 0f; t < def.lucidityRecoverTime + 0.1f; t += Dt) fade.Tick(def, false, Dt);
            Assert.That(fade.Value, Is.EqualTo(1f));
            Assert.That(fade.Profile, Is.Null, "lucid again: everything");
            // A wary or alert guard watches from farther.
            var wary = Guard(Vector3.Zero, 0f, new StealthRules { loudMovement = true });
            wary.Tick(Dt, default, StealthNoise.Make(new Vector3(0f, 0f, -3f), 10f, NoiseKind.Footsteps));
            Assert.That(LucidityFade.IsWatched(def, new Vector3(0f, 0f, 15f), new List<SeekerBrain> { wary }), Is.True, "15 m from a wary guard");
            Assert.That(LucidityFade.IsWatched(def, new Vector3(0f, 0f, 15f), guards), Is.False, "15 m from a calm one: free");
        }

        [Test]
        public void TheWatchedProfilesSpeedCapHoldsTheBodyDown()
        {
            var w = new JamKit.Tests.Movement.BoxWorld();
            w.Floor(-50f, 50f, -50f, 500f, 0f);
            var sim = JamKit.Tests.Movement.MoveTestKit.Standing(w, Vector3.Zero, null, 0f, new Vector3(0f, 0f, 9.5f));
            sim.SetProfile(new MoveProfile { speedCap = 6.5f, slide = false, doubleJump = false, wallrun = false });
            JamKit.Tests.Movement.MoveTestKit.Run(sim, JamKit.Tests.Movement.MoveTestKit.Forward(), 1f);
            Assert.That(sim.HorizontalSpeed, Is.LessThanOrEqualTo(6.5f + 1e-3f));
            Assert.That(sim.MaxSpeed, Is.EqualTo(6.5f));
        }
    }
}
