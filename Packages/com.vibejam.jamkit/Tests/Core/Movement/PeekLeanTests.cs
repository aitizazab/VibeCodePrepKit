using System.Numerics;
using JamKit.Movement;
using NUnit.Framework;

namespace JamKit.Tests.Movement
{
    /// <summary>The peek (docs/MOVEMENT.md "Peeking"): held left or right the eyes lean out to the full distance over the
    /// ease time, eased in and out; letting go (or a sprint, a jump, a slide) eases back; a wall cuts the lean at once to the
    /// room beside the eyes (never into it); switching sides passes through upright; both held is neither; leaning, she only
    /// walks slowly.</summary>
    public class PeekLeanTests
    {
        const float Dt = 1f / 60f;
        static readonly PeekLeanDef Def = new PeekLeanDef();

        static void Run(PeekLean lean, float seconds, int want, bool allowed = true, float room = 10f)
        {
            int frames = (int)System.Math.Round(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                int side = lean.ProbeSide(want);
                lean.Tick(Dt, want, allowed, side, room, Def);
            }
        }

        [Test]
        public void TheNumbersAreInTheFeelBand()
        {
            Assert.That(Def.distance, Is.InRange(0.35f, 0.45f), "about 0.35-0.45 m sideways");
            Assert.That(Def.easeTime, Is.InRange(0.15f, 0.2f), "eased in about 0.15-0.2 s");
            Assert.That(Def.roll, Is.InRange(2f, 5f), "a few degrees of roll, no more (comfort)");
        }

        [Test]
        public void HoldingRightLeansOutEasedAndLettingGoComesBack()
        {
            var lean = new PeekLean();
            Run(lean, Def.easeTime * 0.25f, +1);
            float quarter = lean.Offset(Def);
            Assert.That(quarter, Is.GreaterThan(0f).And.LessThan(Def.distance * 0.25f), "eased in: slower than linear at first");
            Run(lean, Def.easeTime, +1);
            Assert.That(lean.Offset(Def), Is.EqualTo(Def.distance).Within(1e-4f), "full lean within the ease time");
            Assert.That(lean.Roll(Def), Is.EqualTo(Def.roll).Within(1e-4f));
            Run(lean, Def.easeTime + 0.02f, 0);
            Assert.That(lean.Offset(Def), Is.EqualTo(0f), "upright again");
            Assert.That(lean.Leaning, Is.False);
        }

        [Test]
        public void LeftIsNegativeAndBothHeldIsNeither()
        {
            var lean = new PeekLean();
            Run(lean, 0.5f, -1);
            Assert.That(lean.Offset(Def), Is.EqualTo(-Def.distance).Within(1e-4f));
            Assert.That(lean.Side, Is.EqualTo(-1));
            Run(lean, 0.5f, 0); // both held reads as 0 (the callers add -1 and +1)
            Assert.That(lean.Leaning, Is.False);
        }

        [Test]
        public void SwitchingSidesPassesThroughUpright()
        {
            var lean = new PeekLean();
            Run(lean, 0.5f, -1);
            bool sawUpright = false;
            for (int i = 0; i < 60; i++)
            {
                lean.Tick(Dt, +1, true, lean.ProbeSide(+1), 10f, Def);
                if (System.Math.Abs(lean.Offset(Def)) < 0.03f) sawUpright = true;
            }
            Assert.That(sawUpright, Is.True);
            Assert.That(lean.Offset(Def), Is.EqualTo(Def.distance).Within(1e-4f));
        }

        [Test]
        public void AWallCutsTheLeanAtOnceAndNeverPastIt()
        {
            var lean = new PeekLean();
            Run(lean, 0.5f, +1);
            Assert.That(lean.Offset(Def), Is.EqualTo(Def.distance).Within(1e-4f));
            // A wall comes up 0.12 m beside the eyes (she side-stepped toward it): the lean is cut the same frame.
            lean.Tick(Dt, +1, true, lean.ProbeSide(+1), 0.12f, Def);
            Assert.That(lean.Offset(Def), Is.LessThanOrEqualTo(0.12f + 1e-4f));
            // Holding on against it never pushes past it, and with no room at all she stays upright.
            Run(lean, 1f, +1, true, 0.12f);
            Assert.That(lean.Offset(Def), Is.LessThanOrEqualTo(0.12f + 1e-4f).And.GreaterThan(0.1f));
            var blocked = new PeekLean();
            Run(blocked, 1f, +1, true, 0f);
            Assert.That(blocked.Offset(Def), Is.EqualTo(0f), "flat against a wall: no lean");
            Assert.That(blocked.Roll(Def), Is.EqualTo(0f), "and no roll");
        }

        [Test]
        public void ASideThatWasntProbedStaysUpright()
        {
            var lean = new PeekLean();
            // A long frame wanting right, but the room reported for the left side: it doesn't trust it.
            lean.Tick(0.5f, +1, true, -1, 10f, Def);
            Assert.That(lean.Offset(Def), Is.EqualTo(0f));
        }

        [Test]
        public void SprintingJumpingOrSlidingLetsItGo()
        {
            Assert.That(PeekLean.Allowed(MoveMode.Grounded, false), Is.True, "standing, crouched or walking");
            Assert.That(PeekLean.Allowed(MoveMode.Grounded, true), Is.False, "sprint held or toggled");
            foreach (var mode in new[] { MoveMode.Sprint, MoveMode.Air, MoveMode.Slide, MoveMode.Wallrun, MoveMode.Mantle })
                Assert.That(PeekLean.Allowed(mode, false), Is.False, mode.ToString());
            var lean = new PeekLean();
            Run(lean, 0.5f, +1);
            Run(lean, Def.easeTime + 0.02f, +1, allowed: false);
            Assert.That(lean.Leaning, Is.False, "eased back while still held");
        }

        [Test]
        public void LeaningSheOnlyWalksSlowly()
        {
            const float fullAt = 0.7f;
            var full = PeekLean.LimitMove(new Vector2(0f, 1f), fullAt, Def);
            Assert.That(full.Length(), Is.EqualTo(fullAt * Def.walkShare).Within(1e-4f));
            Assert.That(full.Y, Is.GreaterThan(0f), "the direction is kept");
            var gentle = PeekLean.LimitMove(new Vector2(0.1f, 0.1f), fullAt, Def);
            Assert.That(gentle.X, Is.EqualTo(0.1f).Within(1e-5f), "a gentle push is left alone");
        }
    }
}
