using JamKit.Dialogue;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class DialogueTagTests
    {
        [Test]
        public void SplitsKeyAndValue()
        {
            Assert.That(DialogueTag.TryParse(" Speaker : Ava Moss ", out var t), Is.True);
            Assert.That(t.key, Is.EqualTo("speaker"));
            Assert.That(t.value, Is.EqualTo("Ava Moss"));
        }

        [Test]
        public void TagWithoutValue()
        {
            Assert.That(DialogueTag.TryParse("#FLASH", out var t), Is.True);
            Assert.That(t.key, Is.EqualTo("flash"));
            Assert.That(t.value, Is.EqualTo(""));
        }

        [Test]
        public void RejectsEmpty()
        {
            Assert.That(DialogueTag.TryParse(null, out _), Is.False);
            Assert.That(DialogueTag.TryParse("   ", out _), Is.False);
            Assert.That(DialogueTag.TryParse(":x", out _), Is.False);
        }

        [Test]
        public void NumbersUseInvariantCulture()
        {
            DialogueTag.TryParse("wait:0.5", out var t);
            Assert.That(t.FloatOr(9f), Is.EqualTo(0.5f));
            DialogueTag.TryParse("shake:lots", out t);
            Assert.That(t.FloatOr(0.3f), Is.EqualTo(0.3f));
            DialogueTag.TryParse("shake", out t);
            Assert.That(t.FloatOr(0.3f), Is.EqualTo(0.3f));
        }

        [Test]
        public void PortraitWordsInAnyOrder()
        {
            Assert.That(PortraitCommand.TryParse("Ava happy right", out var p), Is.True);
            Assert.That(p.character, Is.EqualTo("Ava"));
            Assert.That(p.expression, Is.EqualTo("happy"));
            Assert.That(p.side, Is.EqualTo(PortraitSide.Right));
            Assert.That(p.hide, Is.False);

            PortraitCommand.TryParse("Ava left", out p);
            Assert.That(p.expression, Is.EqualTo(""));
            Assert.That(p.side, Is.EqualTo(PortraitSide.Left));

            PortraitCommand.TryParse("Bo hide", out p);
            Assert.That(p.hide, Is.True);
            Assert.That(p.side, Is.EqualTo(PortraitSide.Keep));

            Assert.That(PortraitCommand.TryParse("", out _), Is.False);
        }
    }

    public class TypewriterTests
    {
        [Test]
        public void RevealsOverTime()
        {
            var tw = new Typewriter { charsPerSecond = 10f, sentencePause = 0f, clausePause = 0f };
            tw.Begin("abcdefghij");
            Assert.That(tw.Visible, Is.EqualTo(0));
            tw.Tick(0.55f);
            Assert.That(tw.Visible, Is.EqualTo(5));
            Assert.That(tw.IsComplete, Is.False);
            tw.Tick(10f);
            Assert.That(tw.IsComplete, Is.True);
            Assert.That(tw.Visible, Is.EqualTo(10));
        }

        [Test]
        public void CompleteShowsEverything()
        {
            var tw = new Typewriter();
            tw.Begin("Hello there.");
            tw.Delay(5f);
            Assert.That(tw.IsWaiting, Is.True);
            tw.Complete();
            Assert.That(tw.IsComplete, Is.True);
            Assert.That(tw.Visible, Is.EqualTo(12));
        }

        [Test]
        public void DelayHoldsTheLineBack()
        {
            var tw = new Typewriter { charsPerSecond = 100f };
            tw.Begin("abc");
            tw.Delay(1f);
            tw.Tick(0.9f);
            Assert.That(tw.Visible, Is.EqualTo(0));
            tw.Tick(0.2f);
            Assert.That(tw.Visible, Is.GreaterThan(0), "the leftover 0.1 s after the delay types");
        }

        [Test]
        public void PunctuationPauses()
        {
            var fast = new Typewriter { charsPerSecond = 10f, sentencePause = 0f };
            var slow = new Typewriter { charsPerSecond = 10f, sentencePause = 1f };
            fast.Begin("Hi. There");
            slow.Begin("Hi. There");
            fast.Tick(0.65f);
            slow.Tick(0.65f);
            Assert.That(slow.Visible, Is.LessThan(fast.Visible));
        }

        [Test]
        public void EmptyLineIsCompleteAtOnce()
        {
            var tw = new Typewriter();
            tw.Begin(null);
            Assert.That(tw.IsComplete, Is.True);
            Assert.That(tw.Tick(1f), Is.False);
        }
    }
}
