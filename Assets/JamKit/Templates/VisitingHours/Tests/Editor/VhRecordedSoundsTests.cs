using System;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>The opening's sounds are CC0 recordings (Templates/VisitingHours/Audio/Resources/JamKitSounds,
    /// tools/audio/make-vh-sfx.py, sources in ThirdParty/LICENSES.md): ProceduralAudio.Get plays them over the
    /// generated ones; the ring and the buzz loop together every 3 s; the breath sits on VhStage's 4.8 s breath; the
    /// levels keep the opening's set peaks; the loops have no seam.</summary>
    public class VhRecordedSoundsTests
    {
        static readonly string[] Ids = { "vh_breath", "vh_phone_ring", "vh_vibrate", "vh_rustle_big", "vh_rustle", "vh_clunk", "vh_tap" };

        static float[] Samples(AudioClip c)
        {
            c.LoadAudioData();
            var data = new float[c.samples * c.channels];
            Assert.That(c.GetData(data, 0), Is.True, c.name + " readable (Decompress On Load)");
            return data;
        }

        [Test]
        public void TheOpeningPlaysTheRecordings()
        {
            foreach (var id in Ids)
            {
                var rec = ProceduralAudio.Recorded(id);
                Assert.That(rec, Is.Not.Null, "Resources/" + ProceduralAudio.RecordedFolder + id + ".wav");
                Assert.That(ProceduralAudio.Get(id), Is.SameAs(rec), id + ": the recording wins over the generated sound");
                Assert.That(rec.channels, Is.EqualTo(1), id + " is mono");
            }
            Assert.That(ProceduralAudio.Recorded("no_such_sound"), Is.Null);
        }

        [Test]
        public void TheRingAndTheBuzzLoopTogetherEveryThreeSeconds()
        {
            var ring = ProceduralAudio.Recorded("vh_phone_ring");
            var buzz = ProceduralAudio.Recorded("vh_vibrate");
            Assert.That(ring.length, Is.EqualTo(3f).Within(0.002f));
            Assert.That(buzz.length, Is.EqualTo(ring.length).Within(0.0001f), "same length, so they stay in step");
            Assert.That(ring.frequency, Is.EqualTo(buzz.frequency));
        }

        [Test]
        public void TheBreathSitsOnHerChestsBreath()
        {
            var clip = ProceduralAudio.Recorded("vh_breath");
            float breaths = clip.length / 4.8f;
            Assert.That(breaths, Is.EqualTo(Mathf.Round(breaths)).Within(0.002f), "a whole number of VhStage's 4.8 s breaths");
            // Most of the sound is in the in and out phases of VhStage.BreathAt, almost none in the rest after.
            var s = Samples(clip);
            int sr = clip.frequency, cycle = Mathf.RoundToInt(4.8f * sr);
            double moving = 0, rest = 0, all = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double e = s[i] * s[i];
                float t = (i % cycle) / (float)sr;
                all += e;
                if (t < 4.3f) moving += e;
                else rest += e;
            }
            Assert.That(moving / all, Is.GreaterThan(0.9), "breathing while her chest moves");
            Assert.That(rest / all, Is.LessThan(0.05), "quiet in the pause");
        }

        [Test]
        public void TheLevelsKeepTheOpeningsPeaksAndTheLoopsHaveNoSeam()
        {
            var peaks = new[] { 0.35f, 0.75f, 0.6f, 0.6f, 0.55f, 0.75f, 0.6f };
            for (int k = 0; k < Ids.Length; k++)
            {
                var s = Samples(ProceduralAudio.Recorded(Ids[k]));
                float pk = 0f;
                foreach (var v in s) pk = Math.Max(pk, Math.Abs(v));
                // Vorbis import decodes with a little overshoot (the ring's 0.75 comes back as 0.846), so allow +20%.
                Assert.That(pk, Is.InRange(peaks[k] * 0.8f, peaks[k] * 1.2f), Ids[k] + " peak");
            }
            foreach (var id in new[] { "vh_breath", "vh_phone_ring", "vh_vibrate" })
            {
                var s = Samples(ProceduralAudio.Recorded(id));
                double sum = 0;
                for (int i = 1; i < s.Length; i++) sum += (s[i] - s[i - 1]) * (s[i] - s[i - 1]);
                float step = (float)Math.Sqrt(sum / (s.Length - 1));
                Assert.That(Math.Abs(s[0] - s[s.Length - 1]), Is.LessThanOrEqualTo(4f * step + 0.002f), id + " loops without a click");
            }
        }
    }
}
