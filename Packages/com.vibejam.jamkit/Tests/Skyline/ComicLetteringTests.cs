using System;
using System.Collections.Generic;
using System.Linq;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task C1: the comic lettering core (docs/SKYLINE-PLAN-FLOOR1.md).</summary>
    public class ComicLetteringTests
    {
        const string Drone = SkylineEnemies.DroneId, Care = SkylineEnemies.FloorCareId, Gondola = SkylineEnemies.GondolaId;
        const string Writ = SkylineGuns.WritId, Breacher = SkylineGuns.BreacherId;

        [Test]
        public void TheCapAndTheGapAreTheSpec()
        {
            Assert.That(ComicLettering.MaxOnScreen, Is.EqualTo(2));
            Assert.That(ComicLettering.MinGap, Is.EqualTo(0.35f));
            Assert.That(ComicLettering.BigShoveKnockback, Is.EqualTo(8f));
        }

        [Test]
        public void TheDraftWordsAreTheSpecsSets()
        {
            Assert.That(ComicLettering.Words(Drone, Writ, LetterEvent.Kill), Is.EquivalentTo(new[] { "KZZT!", "BZZT-CRK!", "ZAP!" }));
            Assert.That(ComicLettering.Words(Care, Writ, LetterEvent.Kill), Is.EquivalentTo(new[] { "SPLORCH!", "GLORP!", "SPLAT!" }));
            Assert.That(ComicLettering.Words(Gondola, Writ, LetterEvent.Kill), Is.EquivalentTo(new[] { "CLANG!", "KA-SPLASH!" }));
            Assert.That(ComicLettering.Words(Drone, Writ, LetterEvent.WritSpotBreak), Is.EquivalentTo(new[] { "OBJECTION!", "SUSTAINED!", "OVERRULED!" }));
            Assert.That(ComicLettering.Words(Drone, Writ, LetterEvent.WeakPoint), Is.EquivalentTo(new[] { "CRACK!", "TINK!" }));
            Assert.That(ComicLettering.Words(Drone, Breacher, LetterEvent.BreacherShove), Is.EquivalentTo(new[] { "WHUMP!", "BWOOM!" }));
        }

        [Test]
        public void PickedWordsComeFromTheRightSets()
        {
            var c = new ComicLettering(7);
            float t = 0f;
            foreach (var e in new[] { LetterEvent.Kill, LetterEvent.WeakPoint, LetterEvent.WritSpotBreak, LetterEvent.BreacherShove })
                foreach (var bot in new[] { Drone, Care, Gondola })
                    for (int i = 0; i < 20; i++)
                    {
                        string gun = e == LetterEvent.BreacherShove ? Breacher : Writ;
                        var r = c.Pick(e, bot, gun, t += 1f, 0);
                        Assert.That(r, Is.Not.Null);
                        Assert.That(ComicLettering.Words(bot, gun, e), Does.Contain(r.text));
                        Assert.That(r.evt, Is.EqualTo(e));
                    }
        }

        [Test]
        public void TheSameWordIsNeverPickedTwiceInARowForOneBotType()
        {
            var c = new ComicLettering(3);
            float t = 0f;
            foreach (var bot in new[] { Drone, Care, Gondola })
            {
                string last = null;
                for (int i = 0; i < 60; i++)
                {
                    var r = c.Pick(LetterEvent.Kill, bot, Writ, t += 10f, 0);
                    Assert.That(r.text, Is.Not.EqualTo(last), bot + " #" + i);
                    last = r.text;
                }
            }
        }

        [Test]
        public void EveryWordOfASetGetsUsedOverTime()
        {
            var c = new ComicLettering(11);
            var seen = new HashSet<string>();
            for (int i = 0; i < 80; i++) seen.Add(c.Pick(LetterEvent.Kill, Drone, Writ, 10f * (i + 1), 0).text);
            Assert.That(seen, Is.EquivalentTo(ComicLettering.Words(Drone, Writ, LetterEvent.Kill)));
        }

        [Test]
        public void NeverMoreThanTwoAtOnce()
        {
            var c = new ComicLettering(1);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 10f, 0), Is.Not.Null);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 11f, 1), Is.Not.Null);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 12f, 2), Is.Null);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 13f, 3), Is.Null);
        }

        [Test]
        public void ARequestInsideTheGapIsRefusedAndDoesNotRestartTheGap()
        {
            var c = new ComicLettering(1);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 5f, 0), Is.Not.Null);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 5.30f, 0), Is.Null);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 5.34f, 0), Is.Null);
            // 0.35 s after the ACCEPTED one, not after the refused ones.
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 5.36f, 0), Is.Not.Null);
        }

        [Test]
        public void ARefusedCallDoesNotShiftTheSeededSequence()
        {
            var a = new ComicLettering(99);
            var b = new ComicLettering(99);
            a.Pick(LetterEvent.Kill, Drone, Writ, 1f, 0);
            b.Pick(LetterEvent.Kill, Drone, Writ, 1f, 0);
            Assert.That(b.Pick(LetterEvent.Kill, Drone, Writ, 1.1f, 0), Is.Null);
            Assert.That(b.Pick(LetterEvent.Kill, Drone, Writ, 2f, 5), Is.Null);
            var ra = a.Pick(LetterEvent.Kill, Care, Writ, 3f, 0);
            var rb = b.Pick(LetterEvent.Kill, Care, Writ, 3f, 0);
            Assert.That(rb.text, Is.EqualTo(ra.text));
            Assert.That(rb.font, Is.EqualTo(ra.font));
            Assert.That(rb.tiltDeg, Is.EqualTo(ra.tiltDeg));
        }

        [Test]
        public void SameSeedSameSequenceDifferentSeedDiffers()
        {
            string Run(ulong seed)
            {
                var c = new ComicLettering(seed);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 40; i++)
                {
                    var r = c.Pick((LetterEvent)(i % 4), i % 2 == 0 ? Drone : Care, i % 3 == 0 ? Breacher : Writ, 10f * (i + 1), i % 2);
                    sb.Append(r.text).Append('/').Append(r.font).Append('/').Append(r.tiltDeg).Append(';');
                }
                return sb.ToString();
            }
            Assert.That(Run(5), Is.EqualTo(Run(5)));
            Assert.That(Run(5), Is.Not.EqualTo(Run(6)));
        }

        [Test]
        public void UnknownBotAndGunIdsFallBackToAGenericSet()
        {
            foreach (var e in new[] { LetterEvent.Kill, LetterEvent.WeakPoint, LetterEvent.WritSpotBreak, LetterEvent.BreacherShove })
            {
                string[] generic = ComicLettering.Words("nope", "nada", e);
                Assert.That(generic, Is.Not.Empty);
                Assert.That(ComicLettering.Words(null, null, e), Is.EqualTo(generic));
                Assert.That(generic.All(w => !string.IsNullOrEmpty(w)));
                // not the bot-specific draft words
                var specific = ComicLettering.Words(Drone, e == LetterEvent.BreacherShove ? Breacher : Writ, e);
                Assert.That(generic, Is.Not.EqualTo(specific));
                Assert.That(generic.Intersect(specific), Is.Empty);
            }
            Assert.That(ComicLettering.Words("nope", Writ, LetterEvent.Kill), Does.Not.Contain("KZZT!"));
            var c = new ComicLettering(2);
            var r = c.Pick(LetterEvent.Kill, null, null, 1f, 0);
            Assert.That(ComicLettering.Words(null, null, LetterEvent.Kill), Does.Contain(r.text));
        }

        [Test]
        public void SizesAreKill3SpotBreak3ShoveTwoWeakPointOne()
        {
            var c = new ComicLettering(4);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 10f, 0).size, Is.EqualTo(3));
            Assert.That(c.Pick(LetterEvent.WritSpotBreak, Drone, Writ, 20f, 0).size, Is.EqualTo(3));
            Assert.That(c.Pick(LetterEvent.BreacherShove, Drone, Breacher, 30f, 0).size, Is.EqualTo(2));
            Assert.That(c.Pick(LetterEvent.WeakPoint, Drone, Writ, 40f, 0).size, Is.EqualTo(1));
        }

        [Test]
        public void ABigShoveIsEightMetresPerSecondOrMore()
        {
            Assert.That(ComicLettering.IsBigShove(7.99f), Is.False);
            Assert.That(ComicLettering.IsBigShove(8f), Is.True);
            Assert.That(ComicLettering.IsBigShove(20f), Is.True);
        }

        [Test]
        public void TwoOnScreenNeverShareAFace()
        {
            // Every pick made while one piece is still showing takes the other face from the piece before it.
            for (ulong seed = 0; seed < 50; seed++)
            {
                var c = new ComicLettering(seed);
                var first = c.Pick(LetterEvent.Kill, Drone, Writ, 1f, 0);
                var second = c.Pick(LetterEvent.Kill, Care, Writ, 2f, 1);
                Assert.That(second.font, Is.Not.EqualTo(first.font), "seed " + seed);
                // a third, once the first is gone and the second still shows
                var third = c.Pick(LetterEvent.WeakPoint, Drone, Writ, 3f, 1);
                Assert.That(third.font, Is.Not.EqualTo(second.font), "seed " + seed);
            }
        }

        [Test]
        public void BothFacesAreUsedWhenNothingIsShowing()
        {
            var c = new ComicLettering(8);
            var seen = new HashSet<LetterFont>();
            for (int i = 0; i < 40; i++) seen.Add(c.Pick(LetterEvent.Kill, Drone, Writ, 10f * (i + 1), 0).font);
            Assert.That(seen, Is.EquivalentTo(new[] { LetterFont.LuckiestGuy, LetterFont.Bangers }));
        }

        [Test]
        public void TiltStaysWithinEightDegreesAndLifeIsWipeHoldFade()
        {
            var c = new ComicLettering(21);
            for (int i = 0; i < 100; i++)
            {
                var r = c.Pick(LetterEvent.Kill, Drone, Writ, 10f * (i + 1), 0);
                Assert.That(Math.Abs(r.tiltDeg), Is.LessThanOrEqualTo(ComicLettering.MaxTiltDeg));
                Assert.That(r.seconds, Is.EqualTo(r.text.Length * 0.04f + 0.5f + 0.3f).Within(1e-4f));
            }
        }

        [Test]
        public void ResetForgetsTheGapAndTheLastFaceButNotTheSeed()
        {
            var c = new ComicLettering(5);
            c.Pick(LetterEvent.Kill, Drone, Writ, 5f, 0);
            c.Reset();
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 5.01f, 0), Is.Not.Null);
        }

        [Test]
        public void NaNInputsAskForNothing()
        {
            var c = new ComicLettering(1);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, float.NaN, 0), Is.Null);
            Assert.That(c.Pick(LetterEvent.Kill, Drone, Writ, 1f, float.NaN), Is.Null);
        }
    }
}
