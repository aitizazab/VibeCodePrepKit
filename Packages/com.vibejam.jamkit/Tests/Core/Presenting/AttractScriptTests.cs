using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace JamKit.Presenting.Tests
{
    public class AttractScriptTests
    {
        static readonly string[] Chapters = { "Floor 1", "Boss 1", "Draft" };
        static readonly string[] Segments = { "fight", "draft" };

        [Test]
        public void AValidScriptHasNoProblemsAndKnowsItsLength()
        {
            var s = new AttractScript("Game").Jump("Floor 1", 2f).Drive("fight", 10f).Jump("Draft").Drive("draft", 4f).Wait(1f);
            Assert.That(s.Validate(Chapters, Segments), Is.Empty);
            Assert.That(s.TotalSeconds, Is.EqualTo(18.5f));
            Assert.That(s.steps.Count, Is.EqualTo(5));
        }

        [Test]
        public void ProblemsAreNamed()
        {
            var s = new AttractScript("Game").Drive("fight", 0f).Jump("Floor 9").Drive("dance", 3f).Wait(0f);
            var p = s.Validate(Chapters, Segments);
            Assert.That(p, Has.Some.Contains("must start with a chapter jump"));
            Assert.That(p, Has.Some.Contains("Floor 9"));
            Assert.That(p, Has.Some.Contains("dance"));
            Assert.That(p, Has.Some.Contains("needs a duration"));
            Assert.That(new AttractScript("X").Validate(Chapters, Segments), Has.Some.Contains("no steps"));
        }

        [Test]
        public void TheSequencerLoopsAndCountsPasses()
        {
            var s = new AttractScript("Game").Jump("Floor 1").Drive("fight", 5f);
            var q = new AttractSequencer(s);
            Assert.That(q.Current, Is.Null);
            Assert.That(q.Next().name, Is.EqualTo("Floor 1"));
            Assert.That(q.Next().name, Is.EqualTo("fight"));
            Assert.That(q.Loops, Is.EqualTo(0));
            Assert.That(q.Next().name, Is.EqualTo("Floor 1"), "back to the start");
            Assert.That(q.Loops, Is.EqualTo(1));
            q.Restart();
            Assert.That(q.Index, Is.EqualTo(-1));
            Assert.That(new AttractSequencer(new AttractScript()).Next(), Is.Null);
            Assert.Throws<ArgumentNullException>(() => new AttractSequencer(null));
        }

        [Test]
        public void TheNaturalStickMovesAtALimitedRateAndStaysInTheCircle()
        {
            var v = NaturalStick.Step(Vector2.Zero, new Vector2(1f, 0f), 4f, 0.1f);
            Assert.That(v.X, Is.EqualTo(0.4f).Within(1e-5f), "not a snap");
            v = NaturalStick.Step(v, new Vector2(1f, 0f), 4f, 1f);
            Assert.That(v.X, Is.EqualTo(1f).Within(1e-5f));
            v = NaturalStick.Step(Vector2.Zero, new Vector2(3f, 4f), 100f, 1f);
            Assert.That(v.Length(), Is.EqualTo(1f).Within(1e-5f), "targets outside the circle are clamped");
            Assert.That(NaturalStick.Step(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 1f, 0f).X, Is.EqualTo(0.5f));
        }

        [Test]
        public void TurningEasesOffNearTheTargetAndTakesTheShortWay()
        {
            Assert.That(NaturalStick.Turn(0f, 90f), Is.EqualTo(1f));
            Assert.That(NaturalStick.Turn(0f, -90f), Is.EqualTo(-1f));
            Assert.That(NaturalStick.Turn(0f, 9f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(NaturalStick.Turn(170f, -170f), Is.GreaterThan(0f), "across 180 the short way");
            Assert.That(NaturalStick.DeltaAngle(350f, 10f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(NaturalStick.DeltaAngle(10f, 350f), Is.EqualTo(-20f).Within(1e-4f));
        }

        [Test]
        public void PhotoNamesAreSafeTimestampedAndUnique()
        {
            Assert.That(PhotoNames.Slug("Visiting Hours: Day 1!"), Is.EqualTo("Visiting-Hours-Day-1"));
            Assert.That(PhotoNames.Slug("  ???  "), Is.EqualTo("JamKit"));
            Assert.That(PhotoNames.Slug(null), Is.EqualTo("JamKit"));
            Assert.That(PhotoNames.Slug("Café"), Is.EqualTo("Caf"), "file names stay plain ASCII");
            Assert.That(PhotoNames.FileName("Bullet Hell", new DateTime(2026, 10, 17, 9, 5, 3)), Is.EqualTo("Bullet-Hell_20261017-090503.png"));
            var taken = new HashSet<string> { "a.png", "a-2.png" };
            Assert.That(PhotoNames.Unique("a.png", taken.Contains), Is.EqualTo("a-3.png"));
            Assert.That(PhotoNames.Unique("b.png", taken.Contains), Is.EqualTo("b.png"));
            Assert.That(PhotoNames.Unique("b.png", null), Is.EqualTo("b.png"));
        }
    }
}
