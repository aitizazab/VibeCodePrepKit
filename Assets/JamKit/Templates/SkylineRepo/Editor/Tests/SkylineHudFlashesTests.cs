using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the HUD flashes (plan task H5; docs/SKYLINE-HUD.md): the edge flash's curve is 0 outside its 0.35 s,
    /// peaks at 35%, and never goes negative or above the peak; the overlay is one pooled hard-edged element with no text; a
    /// special-ammo gain flashes the counter only with a special weapon out. Tolerances, never exact floats (Unity's Mono and
    /// .NET differ in the last bits).
    /// </summary>
    public class SkylineHudFlashesTests
    {
        GameObject _go;
        SkylineHud _hud;
        VisualElement _tree;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HudFlashTest");
            _hud = _go.AddComponent<SkylineHud>();
            _tree = new VisualElement();
            _hud.BuildOn(_tree);
        }

        [TearDown]
        public void TearDown()
        {
            _hud.Flashes.Unbind();
            Object.DestroyImmediate(_go);
        }

        static void Walk(VisualElement e, List<VisualElement> into)
        {
            into.Add(e);
            foreach (var c in e.Children()) Walk(c, into);
        }

        [Test]
        public void TheEdgeCurveIsZeroOutsideTheFlashAndPeaksAtThirtyFivePercent()
        {
            Assert.That(SkylineHudFlashCurves.EdgeShape(-1f), Is.EqualTo(0f));
            Assert.That(SkylineHudFlashCurves.EdgeShape(0f), Is.EqualTo(0f));
            Assert.That(SkylineHudFlashCurves.EdgeShape(SkylineHudFlashCurves.EdgeSeconds), Is.EqualTo(0f));
            Assert.That(SkylineHudFlashCurves.EdgeShape(5f), Is.EqualTo(0f));
            Assert.That(SkylineHudFlashCurves.EdgeShape(float.NaN), Is.EqualTo(0f));
            Assert.That(SkylineHudFlashCurves.EdgeShape(SkylineHudFlashCurves.EdgeAttack), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(SkylineHudFlashCurves.EdgeOpacity(SkylineHudFlashCurves.EdgeAttack), Is.EqualTo(0.35f).Within(1e-4f));
        }

        [Test]
        public void TheEdgeCurveRisesThenOnlyFalls()
        {
            float prev = 0f;
            float peakT = SkylineHudFlashCurves.EdgeAttack;
            for (float t = 0.001f; t < peakT; t += 0.002f)
            {
                float v = SkylineHudFlashCurves.EdgeShape(t);
                Assert.That(v, Is.GreaterThanOrEqualTo(prev - 1e-5f), "rising at " + t);
                prev = v;
            }
            prev = 1f;
            for (float t = peakT + 0.001f; t < SkylineHudFlashCurves.EdgeSeconds; t += 0.005f)
            {
                float v = SkylineHudFlashCurves.EdgeShape(t);
                Assert.That(v, Is.InRange(0f, 1f));
                Assert.That(v, Is.LessThanOrEqualTo(prev + 1e-5f), "falling at " + t);
                prev = v;
            }
        }

        [Test]
        public void SpecialAmmoFlashesTheCounterOnlyWithASpecialWeaponOut()
        {
            Assert.That(SkylineHudFlashCurves.AmmoFlashWanted(true, 1), Is.True);
            Assert.That(SkylineHudFlashCurves.AmmoFlashWanted(false, 1), Is.False);
            Assert.That(SkylineHudFlashCurves.AmmoFlashWanted(true, 0), Is.False);
        }

        [Test]
        public void ThePlatingFlashShowsForItsLengthThenGoesAway()
        {
            var f = _hud.Flashes;
            Assert.That(f.Edge, Is.Not.Null);
            Assert.That(f.EdgeActive, Is.False);
            Assert.That(f.Edge.style.display.value, Is.EqualTo(DisplayStyle.None));
            f.PlatingGained();
            Assert.That(f.EdgeActive, Is.True);
            Assert.That(f.Edge.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            f.Tick(SkylineHudFlashCurves.EdgeAttack);
            Assert.That(f.Edge.style.opacity.value, Is.EqualTo(SkylineHudFlashCurves.EdgePeak).Within(0.01f));
            f.Tick(SkylineHudFlashCurves.EdgeSeconds);
            Assert.That(f.EdgeActive, Is.False);
            Assert.That(f.Edge.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(f.Edge.style.opacity.value, Is.EqualTo(0f));
        }

        [Test]
        public void AFlashDuringAFlashRestartsItWithoutAddingElements()
        {
            var f = _hud.Flashes;
            var all = new List<VisualElement>();
            Walk(_tree, all);
            int before = all.Count;
            f.PlatingGained();
            f.Tick(0.2f);
            f.PlatingGained();
            Assert.That(f.EdgeOpacityNow, Is.EqualTo(0f).Within(1e-4f), "back at the start of the curve");
            all.Clear();
            Walk(_tree, all);
            Assert.That(all.Count, Is.EqualTo(before), "one pooled overlay");
        }

        [Test]
        public void TheFlashOverlayHasNoTextAndNoRoundedCorners()
        {
            var all = new List<VisualElement>();
            Walk(_hud.Flashes.Edge, all);
            foreach (var e in all)
            {
                Assert.That(e, Is.Not.InstanceOf<TextElement>(), "no label text on the HUD (the creator's rule)");
                Assert.That(e.style.borderTopLeftRadius.value.value, Is.EqualTo(0f).Within(1e-4f));
            }
            Assert.That(_hud.Root.Contains(_hud.Flashes.Edge), Is.True);
        }

        [Test]
        public void TheFlashSitsUnderTheSafeAreaSoItNeverCoversTheBars()
        {
            int edge = _hud.Root.IndexOf(_hud.Flashes.Edge);
            int safe = _hud.Root.IndexOf(_hud.Safe);
            Assert.That(edge, Is.GreaterThanOrEqualTo(0));
            Assert.That(edge, Is.LessThan(safe));
        }
    }
}
