using System.Collections.Generic;
using System.Reflection;
using JamKit.Skyline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo.Tests
{
    /// <summary>
    /// EditMode checks for the subtitles (plan task H4; docs/SKYLINE-HUD.md): bottom centre, no box, at most two lines, the
    /// speaker as a prefix, CONCIERGE in italics, the mode filter (Off / Dialogue / Dialogue + bot barks), the three sizes, the
    /// fade-out, and that the bubbles above the bots are gone (<c>BarkTag</c>, <c>ShowBark</c>, <c>PlaceBark</c>, <c>HideBark</c>
    /// no longer exist). The clock is the subtitles' own (<see cref="SkylineHudSubtitles.Tick"/>), so nothing waits. The
    /// settings are the shared static options, so each test sets what it needs and the fixture puts them back.
    /// </summary>
    public class SkylineHudSubtitlesTests
    {
        GameObject _go;
        SkylineHud _hud;
        VisualElement _tree;
        SubtitleMode _mode;
        SubtitleSize _size;

        SkylineHudSubtitles Sub => _hud.Subtitles;

        [SetUp]
        public void SetUp()
        {
            _mode = SkylineSettings.Options.subtitleMode;
            _size = SkylineSettings.Options.subtitleSize;
            SkylineSettings.Options.subtitleMode = SubtitleMode.Dialogue;
            SkylineSettings.Options.subtitleSize = SubtitleSize.Medium;
            _go = new GameObject("HudSubtitlesTest");
            _hud = _go.AddComponent<SkylineHud>();
            _tree = new VisualElement();
            _hud.BuildOn(_tree);
        }

        [TearDown]
        public void TearDown()
        {
            SkylineSettings.Options.subtitleMode = _mode;
            SkylineSettings.Options.subtitleSize = _size;
            Object.DestroyImmediate(_go);
        }

        static string Plain(VisualElement lineEl) => lineEl.Q<Label>().text;

        [Test]
        public void ADialogueLineShowsWithItsSpeakerAndGoesAfterItsTime()
        {
            _hud.Say(Speaker.Nan, "Mind the clock, love.", 2f);
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(1));
            Assert.That(Plain(Sub.ElementOf(Sub.Lines[0])), Is.EqualTo("NAN: Mind the clock, love."));
            Assert.That(Sub.TextOf(Sub.Lines[0]), Does.Contain("<color=#").And.Contain("NAN:</color>"));
            int steps = 0;
            while (Sub.Lines.Count > 0 && steps++ < 100) Sub.Tick(0.1f);   // frame by frame until its time is up
            Assert.That(Sub.Lines.Count, Is.EqualTo(0));
            Assert.That(Sub.OnScreen, Is.EqualTo(1), "still fading out, not popping");
            for (int i = 0; i < 5; i++) Sub.Tick(0.1f);
            Assert.That(Sub.OnScreen, Is.EqualTo(0));
        }

        [Test]
        public void ThereIsNoBoxAndTheBlockSitsBottomCentre()
        {
            _hud.Say(Speaker.Dot, "Clamped!", 2f);
            Sub.Tick(0.1f);
            var all = new List<VisualElement>();
            void Walk(VisualElement e) { all.Add(e); foreach (var c in e.Children()) Walk(c); }
            Walk(Sub.Box);
            foreach (var e in all)
            {
                Assert.That(e.style.backgroundColor.value.a, Is.EqualTo(0f), e.name + " has a fill");
                Assert.That(e.style.borderTopWidth.value, Is.EqualTo(0f), e.name + " has a border");
                Assert.That(e.style.borderTopLeftRadius.value.value, Is.EqualTo(0f), e.name + " is rounded");
            }
            var box = Sub.Box;
            Assert.That(box.style.bottom.value.value, Is.EqualTo(SkylineHudSubtitles.BottomPx));
            Assert.That(box.style.left.value.value, Is.EqualTo(box.style.right.value.value), "centred");
            Assert.That(box.style.alignItems.value, Is.EqualTo(Align.Center));
        }

        [Test]
        public void ConciergeIsItalicAndTheCrewIsNot()
        {
            _hud.Say(Speaker.Concierge, "Please remain calm.", 3f);
            _hud.Say(Speaker.Dot, "Told you.", 3f);
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(2));
            Assert.That(Sub.Lines[0].pa, Is.True);
            Assert.That(Sub.ElementOf(Sub.Lines[0]).Q<KeylinedLabel>().Front.style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Italic));
            Assert.That(Sub.ElementOf(Sub.Lines[1]).Q<KeylinedLabel>().Front.style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Normal));
        }

        [Test]
        public void AtMostTwoLinesAThirdPushesTheOldestOut()
        {
            _hud.Say(Speaker.Nan, "One.", 5f);
            Sub.Tick(0.2f);
            _hud.Say(Speaker.Dot, "Two.", 5f);
            Sub.Tick(0.2f);
            _hud.Say(Speaker.Nan, "Three.", 5f);
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(2));
            Assert.That(Sub.Lines[0].text, Is.EqualTo("Two."));
            Assert.That(Sub.Lines[1].text, Is.EqualTo("Three."));
            Assert.That(Sub.OnScreen, Is.EqualTo(3), "the pushed-out line is still fading");
            for (int i = 0; i < 5; i++) Sub.Tick(0.1f);
            Assert.That(Sub.OnScreen, Is.EqualTo(2), "and then it is gone");
        }

        [Test]
        public void ALongNewestLineThatWrapsToTwoRowsLeavesNoRoomForTheOlderOne()
        {
            _hud.Say(Speaker.Nan, "Short.", 5f);
            Sub.Tick(0.1f);
            string longText = new string('x', 6).Replace("x", "word word word word word word word word word word ");   // ~300 characters
            _hud.Say(Speaker.Dot, longText, 5f);
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(1));
            Assert.That(Sub.Lines[0].speaker, Is.EqualTo("DOT"));
            Assert.That(SkylineHudSubtitles.RowsFor("DOT", "Clamped! Hold still-ish.", 34f, 1344f), Is.EqualTo(1));
            Assert.That(SkylineHudSubtitles.RowsFor("DOT", new string('a', 120), 34f, 1344f), Is.EqualTo(2));
        }

        [Test]
        public void TheModeFiltersWhatShows()
        {
            SkylineSettings.Options.subtitleMode = SubtitleMode.Off;
            _hud.Say(Speaker.Nan, "Hello.", 3f);
            _hud.SayBark("MALL-COP DRONE", "Halt!");
            Sub.Tick(0.1f);
            Assert.That(Sub.OnScreen, Is.EqualTo(0), "Off shows nothing");

            SkylineSettings.Options.subtitleMode = SubtitleMode.Dialogue;
            _hud.SayBark("MALL-COP DRONE", "Halt!");
            Sub.Tick(0.1f);
            Assert.That(Sub.OnScreen, Is.EqualTo(0), "Dialogue drops the barks");
            _hud.Say(Speaker.Nan, "Hello.", 3f);
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(1));

            SkylineSettings.Options.subtitleMode = SubtitleMode.DialogueAndBarks;
            _hud.SayBark("MALL-COP DRONE", "Halt!");
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(2));
            Assert.That(Plain(Sub.ElementOf(Sub.Lines[1])), Is.EqualTo("MALL-COP DRONE: Halt!"));
        }

        [Test]
        public void SwitchingToOffClearsTheScreen()
        {
            _hud.Say(Speaker.Nan, "Hello.", 30f);
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(1));
            SkylineSettings.Options.subtitleMode = SubtitleMode.Off;
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(0));
            Sub.Tick(1f);
            Assert.That(Sub.OnScreen, Is.EqualTo(0));
        }

        [TestCase(SubtitleSize.Small, 28f)]
        [TestCase(SubtitleSize.Medium, 34f)]
        [TestCase(SubtitleSize.Large, 42f)]
        public void TheSizeSettingSetsTheTextSize(SubtitleSize size, float px)
        {
            SkylineSettings.Options.subtitleSize = size;
            _hud.Say(Speaker.Dot, "Size check.", 3f);
            Sub.Tick(0.1f);
            Assert.That(Sub.ElementOf(Sub.Lines[0]).Q<KeylinedLabel>().Front.style.fontSize.value.value, Is.EqualTo(px));
        }

        [Test]
        public void ChangingTheSizeWhileALineShowsResizesItAndKeepsItsWords()
        {
            _hud.Say(Speaker.Dot, "Keep me.", 30f);
            Sub.Tick(0.1f);
            SkylineSettings.Options.subtitleSize = SubtitleSize.Large;
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(1));
            var lbl = Sub.ElementOf(Sub.Lines[0]).Q<KeylinedLabel>();
            Assert.That(lbl.Front.style.fontSize.value.value, Is.EqualTo(42f));
            Assert.That(Plain(Sub.ElementOf(Sub.Lines[0])), Is.EqualTo("DOT: Keep me."));
        }

        [Test]
        public void TheSameLineSaidAgainWhileShowingIsNotRepeated()
        {
            _hud.Say(Speaker.Dot, "Clamped!", 2f);
            Sub.Tick(0.1f);
            _hud.Say(Speaker.Dot, "Clamped!", 2f);
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(1));
        }

        [Test]
        public void EndSayTakesDialogueDownButNotABark()
        {
            SkylineSettings.Options.subtitleMode = SubtitleMode.DialogueAndBarks;
            _hud.Say(Speaker.Nan, "Hello.", 30f);
            _hud.SayBark("SPOTLIGHT", "Smile.");
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(2));
            _hud.EndSay();
            Sub.Tick(0.1f);
            Assert.That(Sub.Lines.Count, Is.EqualTo(1));
            Assert.That(Sub.Lines[0].speaker, Is.EqualTo("SPOTLIGHT"));
        }

        [Test]
        public void TheBubblesAboveTheBotsAreGone()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            Assert.That(typeof(SkylineHud).GetNestedType("BarkTag", flags), Is.Null, "BarkTag");
            foreach (var m in new[] { "ShowBark", "PlaceBark", "HideBark" })
                Assert.That(typeof(SkylineHud).GetMethod(m, flags), Is.Null, m);
            Assert.That(typeof(SkylineHud).Assembly.GetType("JamKit.SkylineRepo.SkylineBarkBubble"), Is.Null, "SkylineBarkBubble");
        }

        [Test]
        public void MarkupInTheWordsCannotBreakTheLabel()
        {
            _hud.Say(Speaker.Nan, "<size=300>Huge</size>", 3f);
            Sub.Tick(0.1f);
            Assert.That(Sub.TextOf(Sub.Lines[0]), Does.Not.Contain("<size"));
        }
    }
}
