using JamKit.Audio;
using JamKit.Flow;
using NUnit.Framework;

namespace JamKit.Tests
{
    public class RunStatsTests
    {
        [Test]
        public void FormatsTimes()
        {
            Assert.That(RunStats.FormatTime(0f), Is.EqualTo("0:00"));
            Assert.That(RunStats.FormatTime(7.9f), Is.EqualTo("0:07"));
            Assert.That(RunStats.FormatTime(65f), Is.EqualTo("1:05"));
            Assert.That(RunStats.FormatTime(3723f), Is.EqualTo("1:02:03"));
            Assert.That(RunStats.FormatTime(float.NaN), Is.EqualTo("0:00"));
            Assert.That(RunStats.FormatTimePrecise(65.37f), Is.EqualTo("1:05.3"));
        }

        [Test]
        public void SetReplacesByLabel()
        {
            var r = new RunStats();
            r.Set("Coins", "1 / 3");
            r.Set("Hits", "0");
            r.Set("Coins", "2 / 3");
            Assert.That(r.Extra.Count, Is.EqualTo(2));
            Assert.That(r.Extra[0].value, Is.EqualTo("2 / 3"));
            r.Tick(1.5f);
            r.Tick(-3f);
            r.score = 5;
            Assert.That(r.time, Is.EqualTo(1.5f));
            r.Reset();
            Assert.That(r.Extra.Count, Is.EqualTo(0));
            Assert.That(r.score, Is.EqualTo(0));
            Assert.That(r.time, Is.EqualTo(0f));
        }

        [Test]
        public void DifficultyWordsCycle()
        {
            Assert.That(DifficultyText.Next(Difficulty.Hard, 1), Is.EqualTo(Difficulty.Story));
            Assert.That(DifficultyText.Next(Difficulty.Story, -1), Is.EqualTo(Difficulty.Hard));
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                Assert.That(DifficultyText.Describe(d), Is.Not.Empty);
                Assert.That(DifficultyText.Name(d), Is.EqualTo(d.ToString()));
            }
            Assert.That(DifficultyText.All, Is.EqualTo(new[] { Difficulty.Story, Difficulty.Normal, Difficulty.Hard }), "picker order: easiest first");
            Assert.That(DifficultyText.Badge(Difficulty.Normal), Is.Not.Empty, "Normal is the recommended start");
            Assert.That(DifficultyText.Badge(Difficulty.Hard), Is.Empty);
        }

        [Test]
        public void TheEndScreenQuoteResetsWithTheRun()
        {
            var r = new RunStats();
            Assert.That(r.quote, Is.Empty);
            r.SetQuote(" Moth ", "  Again? Fine. I'm not going anywhere. ");
            Assert.That(r.quote, Is.EqualTo("Again? Fine. I'm not going anywhere."));
            Assert.That(r.quoteSpeaker, Is.EqualTo("Moth"));
            r.SetQuote("Moth", null);
            Assert.That(r.quote, Is.Empty);
            Assert.That(r.quoteSpeaker, Is.Empty, "no speaker without a line");
            r.SetQuote(null, "The door stays shut.");
            Assert.That(r.quoteSpeaker, Is.Empty, "narration");
            r.Reset();
            Assert.That(r.quote, Is.Empty);
        }

        [Test]
        public void ChoosingADifficultyAppliesItsAssists()
        {
            // What the run-start picker does with a choice (same value as Settings).
            var s = new Settings();
            Assert.That(s.difficulty, Is.EqualTo(Difficulty.Normal), "Normal is preselected the first time");
            s.SetDifficulty(Difficulty.Hard);
            Assert.That(s.extraLives, Is.EqualTo(0));
            Assert.That(s.enemyBulletSpeedScale, Is.GreaterThan(1f));
            s.SetDifficulty(Difficulty.Story);
            Assert.That(s.damageTakenScale, Is.EqualTo(0.5f));
            Assert.That(s.extraLives, Is.GreaterThan(1));
        }

        [Test]
        public void FlagsPersistAndMigrate()
        {
            var save = new SaveData { flags = null };
            Assert.That(save.HasFlag("x"), Is.False);
            Assert.That(save.Migrate(), Is.True);
            Assert.That(save.SetFlag("met_ava"), Is.True);
            Assert.That(save.SetFlag("met_ava"), Is.False);
            Assert.That(save.SetFlag(""), Is.False);
            Assert.That(save.HasFlag("met_ava"), Is.True);
            Assert.That(save.ClearFlag("met_ava"), Is.True);
            Assert.That(save.HasFlag("met_ava"), Is.False);
        }
    }

    public class ToneSynthTests
    {
        [Test]
        public void SequenceHasTheRightLengthAndIsAudible()
        {
            var buf = ToneSynth.Sequence(22050, 7,
                new ToneSynth.Note(Waveform.Square, 440f, 880f, 0.1f),
                new ToneSynth.Note(Waveform.Noise, 2000f, 200f, 0.2f));
            Assert.That(buf.Length, Is.EqualTo(ToneSynth.SampleCount(0.1f, 22050) + ToneSynth.SampleCount(0.2f, 22050)));
            Assert.That(ToneSynth.Peak(buf), Is.GreaterThan(0.1f));
            Assert.That(ToneSynth.Peak(buf), Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void SameSeedSameSound()
        {
            var n = new ToneSynth.Note(Waveform.Noise, 3000f, 3000f, 0.05f);
            var a = ToneSynth.Sequence(8000, 3, n);
            var b = ToneSynth.Sequence(8000, 3, n);
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void MixClipsAtTheBufferEnd()
        {
            var buf = new float[100];
            ToneSynth.Mix(buf, 90, new ToneSynth.Note(Waveform.Sine, 100f, 100f, 1f, 1f), 1000, 1u, false);
            Assert.That(buf[0], Is.EqualTo(0f));
            Assert.That(ToneSynth.Peak(buf), Is.GreaterThan(0f));
            Assert.That(ToneSynth.NoteHz(12), Is.EqualTo(880f).Within(0.01f));
        }
    }
}
