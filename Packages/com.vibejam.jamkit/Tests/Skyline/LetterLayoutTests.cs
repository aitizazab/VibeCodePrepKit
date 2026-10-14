using System;
using JamKit.Skyline;
using NUnit.Framework;

namespace JamKit.Tests.Skyline
{
    /// <summary>Task C2's pure half: the write-out timing and the placement of comic lettering (docs/SKYLINE-HUD.md).</summary>
    public class LetterLayoutTests
    {
        const float PW = 1920f, PH = 1080f, CX = 960f, CY = 540f, Margin = 40f;
        static readonly float[] None = new float[0];

        // ------------------------------------------------------------------ the write-out

        [Test]
        public void TheWipeStartsClosedAndEndsOpenAtTheWordsWipeTime()
        {
            foreach (string w in new[] { "ZAP!", "SPLORCH!", "CRACK-OPEN!", "A" })
            {
                int n = w.Length;
                Assert.That(LetterWipe.Revealed(n, 0f), Is.EqualTo(0f).Within(1e-5f), w);
                Assert.That(LetterWipe.Revealed(n, LetterWipe.WipeSeconds(n) + 1e-4f), Is.EqualTo(1f).Within(1e-4f), w);
                Assert.That(LetterWipe.Revealed(n, 10f), Is.EqualTo(1f).Within(1e-5f), w);
            }
        }

        [Test]
        public void TheWipeOnlyEverOpensAndGoesLeftToRight()
        {
            int n = 8;
            float prev = -1f;
            for (float t = 0f; t <= LetterWipe.WipeSeconds(n) + 0.05f; t += 0.005f)
            {
                float r = LetterWipe.Revealed(n, t);
                Assert.That(r, Is.GreaterThanOrEqualTo(prev - 1e-5f));
                prev = r;
                // a glyph to the left is never less open than the one to its right
                for (int i = 1; i < n; i++)
                    Assert.That(LetterWipe.GlyphOpen(i - 1, n, t), Is.GreaterThanOrEqualTo(LetterWipe.GlyphOpen(i, n, t) - 1e-5f));
            }
        }

        [Test]
        public void NeighbouringGlyphsOverlapAndOneGlyphTakesTwoGlyphTimes()
        {
            int n = 8;
            float g = ComicLettering.GlyphSeconds;
            // at a glyph time in, glyph 0 is half open and glyph 1 has begun
            Assert.That(LetterWipe.GlyphOpen(0, n, g), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(LetterWipe.GlyphOpen(1, n, g), Is.GreaterThan(0f));
            Assert.That(LetterWipe.GlyphOpen(0, n, 2f * g), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ItHoldsThenFadesToNothingAtTheWordsTotalLife()
        {
            string w = "SPLORCH!";
            int n = w.Length;
            float total = ComicLettering.SecondsFor(w);
            Assert.That(LetterWipe.Alpha(n, 0f), Is.EqualTo(1f));
            Assert.That(LetterWipe.Alpha(n, LetterWipe.WipeSeconds(n) + ComicLettering.HoldSeconds - 0.001f), Is.EqualTo(1f));
            Assert.That(LetterWipe.Alpha(n, total - ComicLettering.FadeSeconds * 0.5f), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(LetterWipe.Alpha(n, total + 0.01f), Is.EqualTo(0f));
            Assert.That(LetterWipe.Alpha(n, float.NaN), Is.EqualTo(0f));
        }

        // ------------------------------------------------------------------ the placement

        [Test]
        public void ABotFarFromTheCrosshairGetsTheWordBesideItOnTheRight()
        {
            Assert.That(LetterPlacement.Place(1500f, 400f, 60f, 300f, 100f, CX, CY, PW, PH, Margin, 0, None, 0,
                out float cx, out float cy, out int side), Is.True);
            Assert.That(side, Is.EqualTo(0));
            Assert.That(cx, Is.GreaterThan(1500f + 60f), "beside the bot, not on it");
            Assert.That(cy, Is.LessThan(400f));
        }

        [Test]
        public void ABotUnderTheCrosshairNeverGetsAWordNearIt()
        {
            for (int s = 0; s < LetterPlacement.Sides; s++)
            {
                bool ok = LetterPlacement.Place(CX + 5f, CY - 3f, 80f, 320f, 110f, CX, CY, PW, PH, Margin, s, None, 0,
                    out float cx, out float cy, out _);
                Assert.That(ok, Is.True, "side " + s);
                Assert.That(LetterPlacement.DistanceToBox(CX, CY, cx, cy, 320f, 110f), Is.GreaterThanOrEqualTo(LetterPlacement.CrosshairClearance - 0.01f));
            }
        }

        [Test]
        public void TheWordStaysInsideTheMargin()
        {
            foreach (var bot in new[] { new[] { 20f, 20f }, new[] { 1900f, 1060f }, new[] { 1900f, 30f }, new[] { 30f, 1050f } })
            {
                bool ok = LetterPlacement.Place(bot[0], bot[1], 50f, 300f, 100f, CX, CY, PW, PH, Margin, 0, None, 0,
                    out float cx, out float cy, out _);
                Assert.That(ok, Is.True);
                Assert.That(cx - 150f, Is.GreaterThanOrEqualTo(Margin - 0.01f));
                Assert.That(cx + 150f, Is.LessThanOrEqualTo(PW - Margin + 0.01f));
                Assert.That(cy - 50f, Is.GreaterThanOrEqualTo(Margin - 0.01f));
                Assert.That(cy + 50f, Is.LessThanOrEqualTo(PH - Margin + 0.01f));
            }
        }

        [Test]
        public void ASecondWordDoesNotLieOnTheFirst()
        {
            // two bots side by side: the second word must not overlap the first
            Assert.That(LetterPlacement.Place(1200f, 300f, 50f, 300f, 100f, CX, CY, PW, PH, Margin, 0, None, 0,
                out float x1, out float y1, out _), Is.True);
            var others = new[] { x1, y1, 300f, 100f };
            Assert.That(LetterPlacement.Place(1210f, 310f, 50f, 300f, 100f, CX, CY, PW, PH, Margin, 0, others, 1,
                out float x2, out float y2, out _), Is.True);
            Assert.That(Math.Abs(x2 - x1) >= 300f - 0.01f || Math.Abs(y2 - y1) >= 100f - 0.01f, Is.True);
        }

        [Test]
        public void ThePreferredSideHoldsWhileItStaysValid()
        {
            // the same bot asked for side 1 (left-up) keeps side 1, so a word does not jump between frames
            Assert.That(LetterPlacement.Place(1500f, 700f, 50f, 280f, 90f, CX, CY, PW, PH, Margin, 1, None, 0,
                out _, out _, out int side), Is.True);
            Assert.That(side, Is.EqualTo(1));
        }

        [Test]
        public void WithNoRoomAtAllItSaysSoInsteadOfCoveringTheCrosshair()
        {
            // a panel too small to hold a word 120 px clear of the crosshair
            Assert.That(LetterPlacement.Place(200f, 200f, 50f, 300f, 100f, 200f, 200f, 420f, 300f, 20f, 0, None, 0, out _, out _, out _), Is.False);
        }

        [Test]
        public void BadNumbersAreRefusedNotThrown()
        {
            Assert.That(LetterPlacement.Place(float.NaN, 0f, 10f, 100f, 50f, CX, CY, PW, PH, Margin, 0, None, 0, out _, out _, out _), Is.False);
            Assert.That(LetterPlacement.Place(100f, 100f, 10f, 0f, 50f, CX, CY, PW, PH, Margin, 0, None, 0, out _, out _, out _), Is.False);
        }

        [Test]
        public void AnyBotAnywhereGivesAValidPlaceOrNone()
        {
            var rng = new Random(7);
            for (int i = 0; i < 2000; i++)
            {
                float bx = (float)(rng.NextDouble() * PW), by = (float)(rng.NextDouble() * PH);
                float w = 120f + (float)rng.NextDouble() * 400f, h = 60f + (float)rng.NextDouble() * 80f;
                if (!LetterPlacement.Place(bx, by, 20f + (float)rng.NextDouble() * 120f, w, h, CX, CY, PW, PH, Margin, rng.Next(8), None, 0,
                        out float cx, out float cy, out _)) continue;
                Assert.That(LetterPlacement.DistanceToBox(CX, CY, cx, cy, w, h), Is.GreaterThanOrEqualTo(LetterPlacement.CrosshairClearance - 0.01f));
                Assert.That(cx - w * 0.5f, Is.GreaterThanOrEqualTo(Margin - 0.01f));
                Assert.That(cx + w * 0.5f, Is.LessThanOrEqualTo(PW - Margin + 0.01f));
            }
        }
    }
}
