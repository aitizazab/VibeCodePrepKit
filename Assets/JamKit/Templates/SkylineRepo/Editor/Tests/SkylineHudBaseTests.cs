using System.Collections.Generic;
using JamKit.Skyline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the HUD base (plan task H2; docs/SKYLINE-HUD.md): the HUD builds on a bare element tree, its
    /// pieces exist, nothing in it is a rounded-corner box (the creator: "stylized, not generic rounded boxes"), the old boxed
    /// bars, speaker card and banner are gone, the ammo face is the one swappable setting and falls back with a log line.
    /// </summary>
    public class SkylineHudBaseTests
    {
        GameObject _go;
        SkylineHud _hud;
        VisualElement _tree;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HudBaseTest");
            _hud = _go.AddComponent<SkylineHud>();
            _tree = new VisualElement();
            _hud.BuildOn(_tree);
            SkylineSettings.Options.subtitleMode = SubtitleMode.Dialogue;   // whatever the machine's saved options say
            SkylineSettings.Options.subtitleSize = SubtitleSize.Medium;
        }

        [TearDown]
        public void TearDown()
        {
            SkylineHudStyle.AmmoFontName = null;   // back to the default / command line
            Object.DestroyImmediate(_go);
        }

        static void Walk(VisualElement e, List<VisualElement> into)
        {
            into.Add(e);
            foreach (var c in e.Children()) Walk(c, into);
        }

        [Test]
        public void ThePiecesExist()
        {
            Assert.That(_hud.Root, Is.Not.Null);
            Assert.That(_hud.Safe, Is.Not.Null);
            Assert.That(_tree.Q<VisualElement>("hud-health"), Is.Not.Null);
            Assert.That(_tree.Q<VisualElement>("hud-ammo"), Is.Not.Null);
            Assert.That(_tree.Q<VisualElement>("hud-say"), Is.Not.Null);
            Assert.That(_hud.Ammo.Digits, Is.Not.Null);
            Assert.That(_hud.Ammo.GunName, Is.Not.Null);
        }

        [Test]
        public void NoElementHasRoundedCorners()
        {
            var all = new List<VisualElement>();
            Walk(_tree, all);
            Assert.That(all.Count, Is.GreaterThan(10));
            foreach (var e in all)
            {
                Assert.That(e.style.borderTopLeftRadius.value.value, Is.EqualTo(0f), e.name);
                Assert.That(e.style.borderTopRightRadius.value.value, Is.EqualTo(0f), e.name);
                Assert.That(e.style.borderBottomLeftRadius.value.value, Is.EqualTo(0f), e.name);
                Assert.That(e.style.borderBottomRightRadius.value.value, Is.EqualTo(0f), e.name);
            }
        }

        [Test]
        public void TheOldLabelledBarsAreGone()
        {
            var all = new List<VisualElement>();
            Walk(_tree, all);
            foreach (var e in all)
            {
                if (e is Label l) Assert.That(l.text, Does.Not.Contain("PLATING").And.Not.Contain("CHASSIS"));
            }
        }

        [Test]
        public void TheHealthBarIsOneSlantedLayeredShape()
        {
            // The corners lean: the top edge sits right of the bottom edge by the skew.
            var c = SkylineHudHealth.Corners(10f, 20f, 300f, 22f, 14f);
            Assert.That(c[1].x - c[0].x, Is.EqualTo(14f));
            Assert.That(c[2].x - c[3].x, Is.EqualTo(14f));
            Assert.That(c[0].y, Is.EqualTo(42f));
            Assert.That(c[1].y, Is.EqualTo(20f));
            Assert.That(SkylineHudHealth.PlateSkew, Is.EqualTo(14f * 14f / 22f).Within(1e-4f));
        }

        [Test]
        public void ThePlatingShattersIntoEightShardsThatFallAndFade()
        {
            Assert.That(SkylineHudHealth.ShardCount, Is.EqualTo(8));
            Assert.That(SkylineHudHealth.ShatterSeconds, Is.EqualTo(0.5f));
            for (int i = 0; i < SkylineHudHealth.ShardCount; i++)
            {
                var start = SkylineHudHealth.Shard(i, 0f, out float a0);
                var end = SkylineHudHealth.Shard(i, 1f, out float a1);
                Assert.That(a0, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(a1, Is.EqualTo(0f).Within(1e-4f));
                float y0 = (start[0].y + start[1].y + start[2].y + start[3].y) * 0.25f;
                float y1 = (end[0].y + end[1].y + end[2].y + end[3].y) * 0.25f;
                Assert.That(y1, Is.GreaterThan(y0 + 10f), "shard " + i + " falls");
            }
        }

        [Test]
        public void TheAmmoFaceIsOneSwappableSetting()
        {
            Assert.That(SkylineHudStyle.AmmoFontDefault, Is.EqualTo("LuckiestGuy"));
            Assert.That(SkylineHudStyle.Faces.ContainsKey("LuckiestGuy"), Is.True);
            Assert.That(SkylineHudStyle.Faces.ContainsKey("Anton"), Is.True);
            Assert.That(SkylineHudStyle.Faces.ContainsKey("Bangers"), Is.True);
            Assert.That(SkylineHudStyle.AmmoFontName, Is.EqualTo("LuckiestGuy"));
            SkylineHudStyle.AmmoFontName = "Bangers";
            Assert.That(SkylineHudStyle.AmmoFontName, Is.EqualTo("Bangers"));
        }

        [Test]
        public void TheFacesLoadFromResources()
        {
            Assert.That(SkylineHudStyle.LoadFace("Anton"), Is.Not.Null, "ThirdParty/Fonts/Resources/SkylineFonts/Anton");
            Assert.That(SkylineHudStyle.LoadFace("Bangers"), Is.Not.Null);
            Assert.That(SkylineHudStyle.LoadFace("LuckiestGuy"), Is.Not.Null, "ThirdParty/Fonts/Resources/SkylineFonts/LuckiestGuy");
        }

        [Test]
        public void AMissingFaceLogsOnceAndFallsBack()
        {
            LogAssert.Expect(LogType.Warning, "SKYLINE_HUD font missing: NoSuchFace; using the project font");
            Assert.That(SkylineHudStyle.LoadFace("NoSuchFace"), Is.Null);
            Assert.That(SkylineHudStyle.LoadFace("NoSuchFace"), Is.Null);   // second call: no second warning
        }

        [Test]
        public void ASubtitleIsAColouredPrefixWithNoBox()
        {
            _hud.Say(Speaker.Concierge, "Please remain calm.", 2f);
            _hud.Subtitles.Tick(0.1f);
            var say = _tree.Q<VisualElement>("hud-say");
            Assert.That(say.style.backgroundColor.value.a, Is.EqualTo(0f));
            var first = _hud.Subtitles.Lines[0];
            var lbl = _hud.Subtitles.ElementOf(first).Q<Label>();
            Assert.That(lbl.text, Does.Contain("CONCIERGE:"));
            Assert.That(lbl.style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Italic));   // PA lines are italic
            _hud.Say(Speaker.Dot, "Hello.", 2f);
            _hud.Subtitles.Tick(0.1f);
            var second = _hud.Subtitles.Lines[1];
            lbl = _hud.Subtitles.ElementOf(second).Q<Label>();
            Assert.That(lbl.text, Does.Contain("DOT:"));
            Assert.That(lbl.style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Normal));
        }

        [Test]
        public void TheKeylineIsInkOnEveryCopyBehindTheFront()
        {
            var k = new KeylinedLabel(40f, 3f, null);
            k.SetText("<color=#ffffff>12</color>", "12");
            int back = 0;
            foreach (var c in k.Children())
            {
                var l = (Label)c;
                if (l == k.Front) continue;
                back++;
                Assert.That(l.text, Is.EqualTo("12"));
                Assert.That(l.style.color.value, Is.EqualTo(SkylineHudStyle.Ink));
            }
            Assert.That(back, Is.EqualTo(8));
            Assert.That(k.Front.text, Does.Contain("12"));
        }
    }
}
