using JamKit.Dialogue;
using JamKit.Narrative;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class StoryTagTests
    {
        [Test]
        public void BackgroundWithTransitionAndSeconds()
        {
            var c = StoryCommand.Parse("#bg: platform fade 1.5");
            Assert.That(c.kind, Is.EqualTo(StoryCommandKind.Background));
            Assert.That(c.id, Is.EqualTo("platform"));
            Assert.That(c.transition, Is.EqualTo(StageTransition.Fade));
            Assert.That(c.hasSeconds, Is.True);
            Assert.That(c.seconds, Is.EqualTo(1.5f));
            Assert.That(c.error, Is.Null);

            c = StoryCommand.Parse("scene:office black");
            Assert.That(c.kind, Is.EqualTo(StoryCommandKind.Scene));
            Assert.That(c.transition, Is.EqualTo(StageTransition.Black));
            Assert.That(c.hasSeconds, Is.False);
        }

        [Test]
        public void ShowWordsInAnyOrder()
        {
            var c = StoryCommand.Parse("show:iris left smile slide 0.4");
            Assert.That(c.kind, Is.EqualTo(StoryCommandKind.Show));
            Assert.That(c.id, Is.EqualTo("iris"));
            Assert.That(c.expression, Is.EqualTo("smile"));
            Assert.That(c.hasX, Is.True);
            Assert.That(c.x, Is.EqualTo(0.27f).Within(0.001f));
            Assert.That(c.transition, Is.EqualTo(StageTransition.Slide));
            Assert.That(c.seconds, Is.EqualTo(0.4f));

            c = StoryCommand.Parse("show:bram at 40% flip");
            Assert.That(c.x, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(c.hasFlip && c.flip, Is.True);
            Assert.That(c.expression, Is.EqualTo(""));

            c = StoryCommand.Parse("show:iris smile sad");
            Assert.That(c.error, Does.Contain("two expressions"));
        }

        [Test]
        public void HideAllCgOffAndStopWords()
        {
            Assert.That(StoryCommand.Parse("hide:all").kind, Is.EqualTo(StoryCommandKind.HideAll));
            Assert.That(StoryCommand.Parse("cg:off").kind, Is.EqualTo(StoryCommandKind.CgOff));
            Assert.That(StoryCommand.Parse("cg:off fade 2").seconds, Is.EqualTo(2f));
            Assert.That(StoryCommand.Parse("music:stop").kind, Is.EqualTo(StoryCommandKind.StopMusic));
            Assert.That(StoryCommand.Parse("ambience:off").kind, Is.EqualTo(StoryCommandKind.StopAmbience));
            var m = StoryCommand.Parse("music:music_story_rain 3");
            Assert.That(m.kind, Is.EqualTo(StoryCommandKind.Music));
            Assert.That(m.id, Is.EqualTo("music_story_rain"));
            Assert.That(m.seconds, Is.EqualTo(3f));
        }

        [Test]
        public void EffectsParseTheirNumbers()
        {
            var shake = StoryCommand.Parse("shake:0.6 0.5");
            Assert.That(shake.amount, Is.EqualTo(0.6f));
            Assert.That(shake.seconds, Is.EqualTo(0.5f));
            Assert.That(StoryCommand.Parse("shake:5").amount, Is.EqualTo(1f), "clamped to 0..1");

            var flash = StoryCommand.Parse("flash:red 0.3");
            Assert.That(flash.text, Is.EqualTo("red"));
            Assert.That(flash.seconds, Is.EqualTo(0.3f));

            var fade = StoryCommand.Parse("fade:out black 2");
            Assert.That(fade.id, Is.EqualTo("out"));
            Assert.That(fade.text, Is.EqualTo("black"));
            Assert.That(StoryCommand.Parse("fade:sideways").error, Is.Not.Null);

            var zoom = StoryCommand.Parse("zoom:1.5 2 70% 30%");
            Assert.That(zoom.amount, Is.EqualTo(1.5f));
            Assert.That(zoom.seconds, Is.EqualTo(2f));
            Assert.That(zoom.x, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(zoom.y, Is.EqualTo(0.3f).Within(0.001f));

            Assert.That(StoryCommand.Parse("blur").amount, Is.EqualTo(6f), "#blur alone = soft focus");
            Assert.That(StoryCommand.Parse("blur:0").amount, Is.EqualTo(0f));
            Assert.That(StoryCommand.Parse("filter:grey").id, Is.EqualTo("gray"));
            Assert.That(StoryCommand.Parse("filter:purple").error, Does.Contain("unknown filter"));
            Assert.That(StoryCommand.Parse("glitch:0.8 1.2").seconds, Is.EqualTo(1.2f));
        }

        [Test]
        public void TextPacingAndStructure()
        {
            Assert.That(StoryCommand.Parse("nvl").kind, Is.EqualTo(StoryCommandKind.Nvl));
            Assert.That(StoryCommand.Parse("nvl:yes").error, Is.Not.Null, "flags take no value");
            Assert.That(StoryCommand.Parse("auto:2").seconds, Is.EqualTo(2f));
            Assert.That(StoryCommand.Parse("auto").hasSeconds, Is.False);
            Assert.That(StoryCommand.Parse("chapter:One - The Platform").text, Is.EqualTo("One - The Platform"));
            Assert.That(StoryCommand.Parse("ending:last_train").id, Is.EqualTo("last_train"));
            Assert.That(StoryCommand.Parse("ending:a b").error, Is.Not.Null);
            Assert.That(StoryCommand.Parse("investigate:office").kind, Is.EqualTo(StoryCommandKind.Investigate));
            Assert.That(StoryCommand.Parse("think").kind, Is.EqualTo(StoryCommandKind.Thought), "alias");
        }

        [Test]
        public void MetaActions()
        {
            var t = StoryCommand.Parse("meta:title Next Train Home");
            Assert.That(t.kind, Is.EqualTo(StoryCommandKind.Meta));
            Assert.That(t.id, Is.EqualTo("title"));
            Assert.That(t.text, Is.EqualTo("Next Train Home"));
            Assert.That(StoryCommand.Parse("meta:fakecrash 2").seconds, Is.EqualTo(2f));
            Assert.That(StoryCommand.Parse("meta:popup").error, Is.Not.Null);
            Assert.That(StoryCommand.Parse("meta:selfdestruct").error, Does.Contain("unknown meta action"));
        }

        [Test]
        public void UnknownTagsAndSuggestions()
        {
            var c = StoryCommand.Parse("bgg:platform");
            Assert.That(c.kind, Is.EqualTo(StoryCommandKind.Unknown));
            Assert.That(StoryTags.Suggest("bgg"), Is.EqualTo("bg"));
            Assert.That(StoryTags.Suggest("shwo"), Is.EqualTo("show"));
            Assert.That(StoryTags.Suggest("xyzzyplugh"), Is.Null);
            Assert.That(StoryCommand.Parse("speaker:Iris").kind, Is.EqualTo(StoryCommandKind.Speaker), "the shell's tags are known");
        }

        [Test]
        public void PositionsAndNumbers()
        {
            Assert.That(StoryCommand.TryPosition("farright", out float x), Is.True);
            Assert.That(x, Is.EqualTo(0.88f));
            Assert.That(StoryCommand.TryPosition("33%", out x), Is.True);
            Assert.That(x, Is.EqualTo(0.33f).Within(0.0001f));
            Assert.That(StoryCommand.TryPosition("upstage", out _), Is.False);
            Assert.That(StoryCommand.TryNumber("1.5s", out float s), Is.True, "\"1.5s\" reads as seconds");
            Assert.That(s, Is.EqualTo(1.5f));
        }
    }

    public class StageStateTests
    {
        static StoryCommand C(string tag) => StoryCommand.Parse(tag);

        [Test]
        public void ShowPlacesCharactersInFreeSpots()
        {
            var s = new StageState();
            Assert.That(s.Apply(C("show:iris")), Is.True);
            Assert.That(s.Find("iris").x, Is.EqualTo(0.5f));
            s.Apply(C("show:bram"));
            Assert.That(s.Find("bram").x, Is.EqualTo(0.27f), "the centre is taken");
            s.Apply(C("show:iris sad right"));
            Assert.That(s.Find("IRIS").expression, Is.EqualTo("sad"), "ids ignore case");
            Assert.That(s.Find("iris").x, Is.EqualTo(0.73f));
            s.Apply(C("show:iris"));
            Assert.That(s.Find("iris").expression, Is.EqualTo("sad"), "no expression keeps the face");
            s.Apply(C("move:iris center"));
            Assert.That(s.Find("iris").x, Is.EqualTo(0.5f));
            Assert.That(s.Apply(C("hide:nobody")), Is.False);
            Assert.That(s.Apply(C("hide:bram")), Is.True);
            Assert.That(s.characters.Count, Is.EqualTo(1));
            s.Apply(C("hide:all"));
            Assert.That(s.characters, Is.Empty);
        }

        [Test]
        public void BackgroundKeepsCharactersSceneClears()
        {
            var s = new StageState();
            s.Apply(C("show:iris"));
            s.Apply(C("cg:photo"));
            Assert.That(s.Apply(C("bg:platform")), Is.True);
            Assert.That(s.Apply(C("bg:platform")), Is.False, "same background: nothing changes");
            Assert.That(s.characters.Count, Is.EqualTo(1));
            s.Apply(C("scene:office"));
            Assert.That(s.background, Is.EqualTo("office"));
            Assert.That(s.characters, Is.Empty);
            Assert.That(s.cg, Is.EqualTo(""));
        }

        [Test]
        public void LastingEffectsAndSound()
        {
            var s = new StageState();
            s.Apply(C("music:music_story_rain"));
            s.Apply(C("ambience:amb_rain"));
            s.Apply(C("nvl"));
            s.Apply(C("zoom:1.3 1 30%"));
            s.Apply(C("blur:4"));
            s.Apply(C("filter:sepia 0.8"));
            s.Apply(C("fade:out red"));
            Assert.That(s.music, Is.EqualTo("music_story_rain"));
            Assert.That(s.ambience, Is.EqualTo("amb_rain"));
            Assert.That(s.nvl, Is.True);
            Assert.That(s.zoom, Is.EqualTo(1.3f));
            Assert.That(s.zoomX, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(s.blur, Is.EqualTo(4f));
            Assert.That(s.filter, Is.EqualTo("sepia"));
            Assert.That(s.filterAmount, Is.EqualTo(0.8f));
            Assert.That(s.fadedOut, Is.True);
            Assert.That(s.fadeColor, Is.EqualTo("red"));
            Assert.That(s.Apply(C("shake:1")), Is.False, "momentary effects aren't stage state");

            s.Apply(C("stopmusic"));
            s.Apply(C("adv"));
            s.Apply(C("zoom:1"));
            s.Apply(C("filter:none"));
            s.Apply(C("fade:in"));
            Assert.That(s.music, Is.EqualTo(""));
            Assert.That(s.nvl, Is.False);
            Assert.That(s.zoomX, Is.EqualTo(0.5f));
            Assert.That(s.filter, Is.EqualTo(""));
            Assert.That(s.fadedOut, Is.False);
        }

        [Test]
        public void CloneIsDeep()
        {
            var s = new StageState();
            s.Apply(C("show:iris smile left"));
            var copy = s.Clone();
            Assert.That(copy.SameAs(s), Is.True);
            s.Apply(C("show:iris sad"));
            Assert.That(copy.Find("iris").expression, Is.EqualTo("smile"));
            Assert.That(copy.SameAs(s), Is.False);
            var restored = new StageState();
            restored.CopyFrom(copy);
            Assert.That(restored.SameAs(copy), Is.True);
        }
    }

    public class RichLineTests
    {
        [Test]
        public void PlainLineIsUnchanged()
        {
            var l = RichLine.Parse("Hello there.");
            Assert.That(l.Plain, Is.EqualTo("Hello there."));
            Assert.That(l.Markup, Is.EqualTo("Hello there."));
            Assert.That(l.Build(5), Is.EqualTo("Hello"));
            Assert.That(l.HasMarkup, Is.False);
        }

        [Test]
        public void TagsAreNeverHalfShown()
        {
            var l = RichLine.Parse("I <b>never</b> said <i>that</i>.");
            Assert.That(l.Plain, Is.EqualTo("I never said that."));
            Assert.That(l.Length, Is.EqualTo(18));
            Assert.That(l.Build(5), Is.EqualTo("I <b>nev</b>"));
            Assert.That(l.Build(7), Is.EqualTo("I <b>never</b>"));
            Assert.That(l.Build(99), Is.EqualTo("I <b>never</b> said <i>that</i>."));
        }

        [Test]
        public void TypewriterTagsAreRemovedAndPaletteResolved()
        {
            var l = RichLine.Parse("Wait.<pause=1> <c=accent>Listen</c>.", n => n == "accent" ? "#ffcc00" : null);
            Assert.That(l.Plain, Is.EqualTo("Wait. Listen."));
            Assert.That(l.Markup, Is.EqualTo("Wait. <color=#ffcc00>Listen</color>."));
            Assert.That(l.PauseBefore(5), Is.EqualTo(1f), "the pause sits before the space");
            var unknown = RichLine.Parse("<c=teal>x</c>");
            Assert.That(unknown.Markup, Is.EqualTo("<color=teal>x</color>"));
        }

        [Test]
        public void HiddenRestKeepsTheLayout()
        {
            var l = RichLine.Parse("ab<b>cd</b>ef");
            string s = l.Build(3, true);
            Assert.That(s, Does.StartWith("ab<b>c"));
            Assert.That(s, Does.Contain("<alpha=#00>d</b>ef</alpha>"));
        }

        [Test]
        public void LoneAngleBracketIsText()
        {
            var l = RichLine.Parse("3 < 4 and <3");
            Assert.That(l.Plain, Is.EqualTo("3 < 4 and <3"));
        }

        [Test]
        public void SpeedAndPauseChangeTypingTime()
        {
            var normal = new Typewriter { charsPerSecond = 10f, sentencePause = 0f, clausePause = 0f };
            var slow = new Typewriter { charsPerSecond = 10f, sentencePause = 0f, clausePause = 0f };
            var paused = new Typewriter { charsPerSecond = 10f, sentencePause = 0f, clausePause = 0f };
            normal.Begin("abcdef");
            slow.Begin("<speed=0.5>abcdef</speed>");
            paused.Begin("abc<pause=1>def");
            normal.Tick(0.45f);
            slow.Tick(0.45f);
            paused.Tick(0.45f);
            Assert.That(normal.Visible, Is.EqualTo(4));
            Assert.That(slow.Visible, Is.EqualTo(2), "half speed");
            Assert.That(paused.Visible, Is.EqualTo(3), "stops at the pause");
            paused.Tick(1.1f);
            Assert.That(paused.Visible, Is.GreaterThan(3));
            Assert.That(slow.Length, Is.EqualTo(6));
            Assert.That(slow.Text, Is.EqualTo("abcdef"), "typewriter tags aren't drawn");
        }
    }

    public class StoryLineTests
    {
        static bool Known(string n) => n == "Iris" || n == "iris" || n == "Bram";

        [Test]
        public void SplitsKnownSpeakersOnly()
        {
            Assert.That(StoryLine.TrySplitSpeaker("Iris: You're late.", Known, out var sp, out var text), Is.True);
            Assert.That(sp, Is.EqualTo("Iris"));
            Assert.That(text, Is.EqualTo("You're late."));
            Assert.That(StoryLine.TrySplitSpeaker("Note: the door is open.", Known, out _, out text), Is.False);
            Assert.That(text, Is.EqualTo("Note: the door is open."));
            Assert.That(StoryLine.TrySplitSpeaker("Iris:no space", Known, out _, out _), Is.False);
            Assert.That(StoryLine.TrySplitSpeaker("The time was 10: late", Known, out _, out _), Is.False);
        }
    }
}
