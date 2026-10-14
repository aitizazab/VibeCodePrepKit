using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the comic lettering in play (plan task C2; docs/SKYLINE-HUD.md "Comic lettering"): at most two words
    /// alive, none within 120 px of the crosshair, the hostile red-magenta fill with an ink keyline, a tilt within 8 degrees, a
    /// left-to-right write-out then a hold and a fade, and a pool that never grows (no element made after the first word). The
    /// words are placed with <see cref="SkylineLettering.ShowAtPanel"/> (panel px, no camera); the world projection is checked in
    /// the stills.
    /// </summary>
    public class SkylineLetteringTests
    {
        const string Drone = SkylineEnemies.DroneId, Care = SkylineEnemies.FloorCareId;
        static readonly Vector2 Crosshair = new Vector2(960f, 540f);

        GameObject _go;
        SkylineHud _hud;
        VisualElement _tree;

        SkylineLettering L => _hud.Lettering;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LetteringTest");
            _hud = _go.AddComponent<SkylineHud>();
            _tree = new VisualElement();
            _hud.BuildOn(_tree);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void TheLayerIsUnderTheSafeAreaAndStartsEmpty()
        {
            Assert.That(L.Layer, Is.Not.Null);
            Assert.That(L.Layer.parent, Is.SameAs(_hud.Safe));
            Assert.That(L.Active, Is.EqualTo(0));
        }

        [Test]
        public void AtMostTwoWordsAreEverAlive()
        {
            var a = L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            L.Tick(0.4f);
            var b = L.ShowAtPanel(LetterEvent.Kill, Care, SkylineGuns.WritId, new Vector2(400f, 800f), 60f);
            L.Tick(0.4f);
            var c = L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 800f), 60f);
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(c, Is.Null, "a third is refused while two are alive");
            Assert.That(L.Active, Is.EqualTo(2));
            // after one is gone a new one is welcome
            L.Tick(2f);
            Assert.That(L.Active, Is.EqualTo(0));
            Assert.That(L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f), Is.Not.Null);
        }

        [Test]
        public void ARequestInsideTheGapIsRefused()
        {
            Assert.That(L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f), Is.Not.Null);
            L.Tick(0.1f);
            Assert.That(L.ShowAtPanel(LetterEvent.Kill, Care, SkylineGuns.WritId, new Vector2(400f, 800f), 60f), Is.Null);
        }

        [Test]
        public void NoWordComesWithin120PxOfTheCrosshairWhateverTheBotDoes()
        {
            var boxes = new List<Vector4>();
            // a bot right under the crosshair, then one just beside it
            foreach (var anchor in new[] { Crosshair, Crosshair + new Vector2(30f, -20f), Crosshair + new Vector2(-200f, 40f) })
            {
                L.Reset();
                Assert.That(L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, anchor, 70f), Is.Not.Null);
                L.Tick(0.3f);
                L.ActiveBoxes(boxes);
                Assert.That(boxes.Count, Is.EqualTo(1));
                var b = boxes[0];
                Assert.That(LetterPlacement.DistanceToBox(Crosshair.x, Crosshair.y, b.x, b.y, b.z, b.w),
                    Is.GreaterThanOrEqualTo(LetterPlacement.CrosshairClearance - 0.01f));
            }
        }

        [Test]
        public void TwoWordsDoNotLieOnEachOther()
        {
            var boxes = new List<Vector4>();
            L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1400f, 300f), 60f);
            L.Tick(0.4f);
            L.ShowAtPanel(LetterEvent.Kill, Care, SkylineGuns.WritId, new Vector2(1420f, 320f), 60f);
            L.Tick(0.01f);
            L.ActiveBoxes(boxes);
            Assert.That(boxes.Count, Is.EqualTo(2));
            var a = boxes[0];
            var b = boxes[1];
            bool apart = Mathf.Abs(a.x - b.x) >= (a.z + b.z) * 0.5f - 0.01f || Mathf.Abs(a.y - b.y) >= (a.w + b.w) * 0.5f - 0.01f;
            Assert.That(apart, Is.True);
        }

        [Test]
        public void TheFillIsTheHostileHueWithAnInkKeylineAndTheTiltIsWithinEightDegrees()
        {
            var req = L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            L.Tick(0.05f);
            var el = L.Layer.Q<VisualElement>("hud-lettering-0");
            var label = el.Q<Label>(); // the first label is a keyline copy; the front is the last child
            Assert.That(label, Is.Not.Null);
            var kl = el.Q<VisualElement>("hud-lettering-clip-0").Children().GetEnumerator();
            kl.MoveNext();
            var keylined = (KeylinedLabel)kl.Current;
            Assert.That(keylined.Front.style.color.value, Is.EqualTo(SkylineHudStyle.Hostile));
            Assert.That(keylined.Text, Is.EqualTo(req.text));
            Assert.That(Mathf.Abs(req.tiltDeg), Is.LessThanOrEqualTo(ComicLettering.MaxTiltDeg + 1e-3f));
            Assert.That(el.style.rotate.value.angle.value, Is.EqualTo(req.tiltDeg).Within(1e-3f));
        }

        [Test]
        public void TheWordWritesItselfOutThenHoldsThenFades()
        {
            var req = L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            var el = L.Layer.Q<VisualElement>("hud-lettering-0");
            var clip = L.Layer.Q<VisualElement>("hud-lettering-clip-0");
            int n = req.text.Length;
            float wipe = LetterWipe.WipeSeconds(n);

            L.Tick(0.001f);
            float w0 = clip.style.width.value.value;
            Assert.That(w0, Is.LessThan(el.style.width.value.value * 0.2f), "starts closed");

            L.Tick(wipe * 0.5f);
            float wHalf = clip.style.width.value.value;
            Assert.That(wHalf, Is.GreaterThan(w0));
            Assert.That(wHalf, Is.LessThan(el.style.width.value.value));

            L.Tick(wipe * 0.5f + 0.01f);
            Assert.That(clip.style.width.value.value, Is.EqualTo(el.style.width.value.value).Within(0.5f), "fully open");
            Assert.That(el.style.opacity.value, Is.EqualTo(1f).Within(1e-3f), "holds");

            L.Tick(ComicLettering.HoldSeconds + ComicLettering.FadeSeconds * 0.5f);
            Assert.That(el.style.opacity.value, Is.InRange(0.2f, 0.8f), "fading");

            L.Tick(1f);
            Assert.That(L.Active, Is.EqualTo(0));
            Assert.That(el.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void TheWordsComeFromTheDraftSets()
        {
            var req = L.ShowAtPanel(LetterEvent.Kill, Care, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            Assert.That(ComicLettering.Words(Care, SkylineGuns.WritId, LetterEvent.Kill), Does.Contain(req.text));
            L.Tick(1.5f);
            var spot = L.ShowAtPanel(LetterEvent.WritSpotBreak, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            Assert.That(new[] { "OBJECTION!", "SUSTAINED!", "OVERRULED!" }, Does.Contain(spot.text));
            L.Tick(1.5f);
            var shove = L.ShowAtPanel(LetterEvent.BreacherShove, Drone, SkylineGuns.BreacherId, new Vector2(1500f, 300f), 60f);
            Assert.That(new[] { "WHUMP!", "BWOOM!" }, Does.Contain(shove.text));
        }

        [Test]
        public void ThePoolNeverGrows()
        {
            L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            int made = L.ElementsCreated;
            int children = L.Layer.childCount;
            for (int i = 0; i < 60; i++)
            {
                L.Tick(2f);
                L.ShowAtPanel(i % 2 == 0 ? LetterEvent.Kill : LetterEvent.WeakPoint, i % 3 == 0 ? Care : Drone, SkylineGuns.WritId,
                    new Vector2(300f + i * 20f, 300f), 50f);
            }
            Assert.That(L.ElementsCreated, Is.EqualTo(made));
            Assert.That(L.Layer.childCount, Is.EqualTo(children));
            Assert.That(L.Layer.childCount, Is.EqualTo(ComicLettering.MaxOnScreen));
        }

        [Test]
        public void AWreckRestartClearsEverything()
        {
            L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            Assert.That(L.Active, Is.EqualTo(1));
            L.Reset();
            Assert.That(L.Active, Is.EqualTo(0));
            // and the gap is forgotten, so the next kill is lettered at once
            Assert.That(L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f), Is.Not.Null);
        }

        [Test]
        public void ItShowsNoOtherTextThanTheDraftWords()
        {
            L.ShowAtPanel(LetterEvent.Kill, Drone, SkylineGuns.WritId, new Vector2(1500f, 300f), 60f);
            var texts = new List<string>();
            L.ActiveTexts(texts);
            Assert.That(texts.Count, Is.EqualTo(1));
            Assert.That(texts[0], Does.EndWith("!"));
        }
    }
}
