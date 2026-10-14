using System.Collections.Generic;
using System.Text.RegularExpressions;
using JamKit.Skyline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the HUD objective (plan task H3; docs/SKYLINE-HUD.md "The objective" and "The wave clock"): it builds on
    /// a bare element tree with the job strip, the clock, the ring and three markers; the strip ticks when an asset is towed; the
    /// ring shows only inside a circle and fills with the meter; the markers point at the untaken assets (a diamond in view, a
    /// chevron on the edge when behind) with a distance; the clock reads <c>FloorRules.WaveSeconds</c> (31.35 s), dims while a
    /// wave waits and flashes in its last 3 s; and the HUD says nothing in words (the creator's no-captions rule).
    /// </summary>
    public class SkylineHudObjectiveTests
    {
        GameObject _go, _camGo;
        SkylineHud _hud;
        VisualElement _tree;
        Camera _cam;

        SkylineHudObjective Obj => _hud.Objective;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HudObjectiveTest");
            _hud = _go.AddComponent<SkylineHud>();
            _tree = new VisualElement();
            _hud.BuildOn(_tree);
            _camGo = new GameObject("HudObjectiveCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.fieldOfView = 60f;
            _cam.aspect = 16f / 9f;
            Obj.CameraOverride = _cam;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camGo);
            Object.DestroyImmediate(_go);
        }

        static Repossession NewSheet(ulong seed = 11UL) => new Repossession(RepossessSites.Assets(), seed);

        static NVec3 Feet(Repossession r, int i) => r.Sheet[i].socket.position;

        static void Tow(Repossession r, int i)
        {
            var f = Feet(r, i);
            for (int k = 0; k < 700; k++) r.Tick(0.02f, f);
        }

        void LookAt(Repossession r, int i, float metresBack)
        {
            var s = r.Sheet[i].socket.position;
            _camGo.transform.position = new Vector3(s.X, s.Y + SkylineHudObjective.AssetHeight, s.Z - metresBack);
            _camGo.transform.rotation = Quaternion.identity;   // facing +Z
        }

        // ------------------------------------------------------------------ building

        [Test]
        public void ThePiecesExistUnderTheSafeArea()
        {
            Assert.That(Obj.Element, Is.Not.Null);
            Assert.That(Obj.Element.parent, Is.SameAs(_hud.Safe));
            Assert.That(_tree.Q<VisualElement>(SkylineHudObjective.StripName), Is.Not.Null);
            Assert.That(_tree.Q<VisualElement>(SkylineHudObjective.ClockName), Is.Not.Null);
            Assert.That(_tree.Q<VisualElement>(SkylineHudObjective.RingName), Is.Not.Null);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(Obj.Slot(i), Is.Not.Null);
                Assert.That(Obj.MarkerElement(i), Is.Not.Null);
            }
        }

        [Test]
        public void TheHudSaysNothingInWordsOnlyNumbers()
        {
            var rep = NewSheet();
            var wave = new WaveClock();
            wave.Start();
            var released = new List<WaveDef>();
            wave.Tick(1f, 0, false, released);
            Obj.Bind(rep, wave);
            LookAt(rep, 0, 10f);
            Obj.Tick(0.016f);
            var labels = Obj.Element.Query<Label>().ToList();
            Assert.That(labels.Count, Is.GreaterThan(0));
            foreach (var l in labels)
                Assert.That(Regex.IsMatch(l.text ?? "", @"^[0-9: ]*( m)?$"), Is.True, "label text: '" + l.text + "'");
        }

        // ------------------------------------------------------------------ the job strip

        [Test]
        public void TheStripShowsOnlyOnceBoundAndTicksATowedAsset()
        {
            Assert.That(Obj.Strip.style.display.value, Is.EqualTo(DisplayStyle.None));
            var rep = NewSheet();
            Obj.Bind(rep, null);
            LookAt(rep, 0, 10f);
            Obj.Tick(0.016f);
            Assert.That(Obj.Strip.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            for (int i = 0; i < 3; i++) Assert.That(Obj.SlotTowed(i), Is.False);
            Tow(rep, 1);
            Obj.Tick(0.016f);
            Assert.That(Obj.SlotTowed(0), Is.False);
            Assert.That(Obj.SlotTowed(1), Is.True);
            Assert.That(Obj.SlotTowed(2), Is.False);
        }

        [Test]
        public void TheGlyphsAreRealShapesInTheUnitBox()
        {
            foreach (AssetKind k in System.Enum.GetValues(typeof(AssetKind)))
            {
                var g = SkylineHudObjective.GlyphOf(k);
                Assert.That(g.body.Length, Is.GreaterThanOrEqualTo(1), k.ToString());
                foreach (var set in new[] { g.body, g.cut })
                    foreach (var poly in set)
                    {
                        Assert.That(poly.Length, Is.GreaterThanOrEqualTo(3));
                        foreach (var v in poly)
                        {
                            Assert.That(v.x, Is.InRange(0f, 1f));
                            Assert.That(v.y, Is.InRange(0f, 1f));
                        }
                    }
            }
            // each kind has its own silhouette
            Assert.That(SkylineHudObjective.GlyphOf(AssetKind.Mascot).body.Length, Is.Not.EqualTo(SkylineHudObjective.GlyphOf(AssetKind.ArcadeCabinet).body.Length));
        }

        // ------------------------------------------------------------------ the tow ring

        [Test]
        public void TheRingIsHiddenOutsideACircleAndFillsInsideOne()
        {
            var rep = NewSheet();
            Obj.Bind(rep, null);
            LookAt(rep, 0, 10f);
            for (int i = 0; i < 30; i++) Obj.Tick(0.016f);
            Assert.That(Obj.RingElement.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(Obj.RingEntry, Is.EqualTo(-1));

            // four seconds inside the first circle: the ring is at 40%
            var f = Feet(rep, 0);
            for (int i = 0; i < 200; i++) rep.Tick(0.02f, f);
            for (int i = 0; i < 40; i++) Obj.Tick(0.016f);
            Assert.That(rep.ActiveCircle, Is.EqualTo(0));
            Assert.That(Obj.RingEntry, Is.EqualTo(0));
            Assert.That(Obj.RingProgress, Is.EqualTo(0.4f).Within(0.02f));
            Assert.That(Obj.RingAlpha, Is.EqualTo(1f).Within(0.01f));
            Assert.That(Obj.RingElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            // out of the circle: the ring goes, the meter stays (it pauses, it never drains)
            rep.Tick(0.02f, f + new NVec3(40f, 0f, 0f));
            for (int i = 0; i < 60; i++) Obj.Tick(0.016f);
            Assert.That(Obj.RingElement.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(rep.Sheet[0].meter.Progress, Is.EqualTo(0.4f).Within(0.02f));
        }

        [Test]
        public void TheRingSweepFollowsTheMeter()
        {
            Assert.That(SkylineHudObjective.RingSweep(0f), Is.EqualTo(0f));
            Assert.That(SkylineHudObjective.RingSweep(0.4f), Is.EqualTo(144f).Within(1e-3f));
            Assert.That(SkylineHudObjective.RingSweep(1f), Is.EqualTo(360f).Within(1e-3f));
            Assert.That(SkylineHudObjective.RingSweep(7f), Is.EqualTo(360f).Within(1e-3f));
            Assert.That(SkylineHudObjective.RingSweep(float.NaN), Is.EqualTo(0f));
        }

        // ------------------------------------------------------------------ the markers

        [Test]
        public void AnAssetInViewGetsADiamondAndItsDistance()
        {
            var rep = NewSheet();
            Obj.Bind(rep, null);
            LookAt(rep, 0, 10f);
            Obj.Tick(0.016f);
            Assert.That(Obj.ModeOf(0), Is.EqualTo(SkylineHudObjective.MarkerMode.OnScreen));
            Assert.That(Obj.MarkerElement(0).style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Obj.MarkerLabel(0).Text, Is.EqualTo(HudText.Distance(10f)));
        }

        [Test]
        public void AnAssetBehindYouGetsAnEdgeChevronStillWithItsDistance()
        {
            var rep = NewSheet();
            Obj.Bind(rep, null);
            LookAt(rep, 0, 10f);
            _camGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // turn away from it
            Obj.Tick(0.016f);
            Assert.That(Obj.ModeOf(0), Is.EqualTo(SkylineHudObjective.MarkerMode.OffScreen));
            Assert.That(Obj.MarkerLabel(0).Text, Is.EqualTo(HudText.Distance(10f)));
            // the chevron sits inside the panel
            float x = Obj.MarkerElement(0).style.left.value.value, y = Obj.MarkerElement(0).style.top.value.value;
            Assert.That(x, Is.InRange(0f, 1920f));
            Assert.That(y, Is.InRange(0f, 1080f));
        }

        [Test]
        public void ATowedAssetLosesItsMarker()
        {
            var rep = NewSheet();
            Obj.Bind(rep, null);
            LookAt(rep, 0, 10f);
            Obj.Tick(0.016f);
            Assert.That(Obj.ModeOf(0), Is.Not.EqualTo(SkylineHudObjective.MarkerMode.Hidden));
            Tow(rep, 0);
            Obj.Tick(0.016f);
            Assert.That(Obj.ModeOf(0), Is.EqualTo(SkylineHudObjective.MarkerMode.Hidden));
            Assert.That(Obj.MarkerElement(0).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        // ------------------------------------------------------------------ the wave clock

        [Test]
        public void TheClockReadsTheFloorsIntervalNotAThirty()
        {
            var wave = new WaveClock();
            wave.Start();
            var released = new List<WaveDef>();
            wave.Tick(1f, 0, false, released);   // wave 1 goes out; the next is due one interval after the drop
            Obj.Bind(null, wave);
            Obj.Tick(0.016f);
            float expected = FloorRules.WaveSeconds - 1f;
            Assert.That(FloorRules.WaveSeconds, Is.EqualTo(31.35f).Within(0.01f));
            Assert.That(wave.SecondsToNext, Is.EqualTo(expected).Within(0.01f));
            Assert.That(Obj.ClockDigitsLabel.Text, Is.EqualTo(SkylineHudObjective.ClockDigits(expected)));
            Assert.That(Obj.ClockDigitsLabel.Text, Is.EqualTo("0:31"));
            Assert.That(Obj.ClockDigitsLabel.Text, Does.Not.Contain("NEXT"));
        }

        [Test]
        public void TheClockHidesUntilTheDropAndDimsWhileAWaveWaits()
        {
            var wave = new WaveClock(cap: 1);
            Obj.Bind(null, wave);
            Obj.Tick(0.016f);
            Assert.That(Obj.ClockElement.style.display.value, Is.EqualTo(DisplayStyle.None), "not running yet");
            wave.Start();
            var released = new List<WaveDef>();
            wave.Tick(1f, 5, false, released);   // wave 1 is due but there is no room under a cap of 1: it waits
            Assert.That(wave.Waiting, Is.Not.Null);
            Obj.Tick(0.016f);
            Assert.That(Obj.ClockElement.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Obj.ClockElement.style.opacity.value, Is.EqualTo(SkylineHudObjective.ClockWaitingOpacity).Within(1e-3f));
        }

        [Test]
        public void TheClockFlashesInItsLastThreeSeconds()
        {
            var wave = new WaveClock();
            wave.Start();
            var released = new List<WaveDef>();
            wave.Tick(FloorRules.WaveSeconds - 2.5f, 0, false, released);   // 2.5 s to the next wave
            Assert.That(wave.SecondsToNext, Is.EqualTo(2.5f).Within(0.05f));
            Obj.Bind(null, wave);
            var seen = new HashSet<Color>();
            var scales = new HashSet<float>();
            for (int i = 0; i < 40; i++)
            {
                Obj.Tick(0.02f);
                seen.Add(Obj.ClockDigitsLabel.Front.style.color.value);
                scales.Add(Obj.ClockElement.style.scale.value.value.x);
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "the digits change colour");
            Assert.That(scales.Count, Is.GreaterThan(1), "and pulse in size");

            // well before the last seconds nothing flashes
            var calm = new WaveClock();
            calm.Start();
            calm.Tick(1f, 0, false, released);
            Obj.Bind(null, calm);
            seen.Clear();
            for (int i = 0; i < 40; i++) { Obj.Tick(0.02f); seen.Add(Obj.ClockDigitsLabel.Front.style.color.value); }
            Assert.That(seen.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClockDigitsDropTheWordNext()
        {
            Assert.That(SkylineHudObjective.ClockDigits(21f), Is.EqualTo("0:21"));
            Assert.That(SkylineHudObjective.ClockDigits(0f), Is.EqualTo("0:00"));
            Assert.That(SkylineHudObjective.ClockDigits(65f), Is.EqualTo("1:05"));
        }
    }
}
