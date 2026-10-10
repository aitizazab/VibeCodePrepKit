using System;
using JamKit.Movement;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The movement's sound: every event has clips (recorded variants, else generated ones), the one-shots
    /// have at least 3 variants for round-robin, the shipped CC0 sets are really there (mono), the loops have no seam,
    /// and nothing peaks above -1 dBFS.</summary>
    public class MovementAudioEditorTests
    {
        /// <summary>The ids shipped as recorded variants (tools/audio/make-movement-sfx.py from Kenney's CC0 packs).</summary>
        static readonly string[] Recorded =
        {
            "mv_step", "mv_step_heavy", "mv_jump", "mv_djump", "mv_walljump", "mv_catch",
            "mv_land_soft", "mv_land", "mv_land_hard", "mv_mantle", "mv_kick", "mv_slidein",
        };

        static string[] AllIds()
        {
            var ids = new string[MoveSounds.OneShots.Length + MoveSounds.Loops.Length];
            MoveSounds.OneShots.CopyTo(ids, 0);
            MoveSounds.Loops.CopyTo(ids, MoveSounds.OneShots.Length);
            return ids;
        }

        [Test]
        public void EveryMovementEventHasClipsAndAGeneratedFallback()
        {
            foreach (var id in AllIds())
            {
                Assert.That(ProceduralAudio.Knows(id), Is.True, id + " is a known id");
                Assert.That(ProceduralAudio.VariantCount(id), Is.GreaterThanOrEqualTo(1), id);
                for (int v = 0; v < ProceduralAudio.VariantCount(id); v++)
                    Assert.That(ProceduralAudio.GetVariant(id, v), Is.Not.Null, id + " generated variant " + v);
                var clips = MovementAudio.ClipsFor(id, out _);
                Assert.That(clips, Is.Not.Empty, id);
                foreach (var c in clips) Assert.That(c, Is.Not.Null, id);
            }
        }

        [Test]
        public void OneShotsHaveAtLeastThreeVariants()
        {
            foreach (var id in MoveSounds.OneShots)
            {
                Assert.That(MovementAudio.ClipsFor(id, out _).Length, Is.GreaterThanOrEqualTo(3), id);
                Assert.That(ProceduralAudio.VariantCount(id), Is.GreaterThanOrEqualTo(3), id + " (generated fallback)");
            }
        }

        [Test]
        public void TheRecordedSetsShipAsMonoVariants()
        {
            foreach (var id in Recorded)
            {
                var clips = MovementAudio.ClipsFor(id, out bool recorded);
                Assert.That(recorded, Is.True, id + ": Runtime/Audio/Movement/Resources/" + MovementAudio.ResourceFolder + id + "_0.wav");
                Assert.That(clips.Length, Is.GreaterThanOrEqualTo(3), id);
                foreach (var c in clips)
                {
                    Assert.That(c.channels, Is.EqualTo(1), c.name + " is mono");
                    Assert.That(c.length, Is.InRange(0.03f, 1f), c.name);
                }
            }
        }

        [Test]
        public void NothingPeaksAboveMinusOneDbfs()
        {
            foreach (var id in AllIds())
                foreach (var c in MovementAudio.ClipsFor(id, out _))
                {
                    var data = Samples(c);
                    float pk = 0f;
                    foreach (var s in data) pk = Math.Max(pk, Math.Abs(s));
                    Assert.That(pk, Is.LessThan(MoveSounds.MaxPeak), c.name + " peak " + (20f * Mathf.Log10(pk + 1e-9f)).ToString("0.0") + " dBFS");
                    Assert.That(pk, Is.GreaterThan(0.05f), c.name + " is not silent");
                }
        }

        [Test]
        public void LoopsAreSeamless()
        {
            foreach (var id in MoveSounds.Loops)
            {
                var c = MovementAudio.ClipsFor(id, out _)[0];
                var s = Samples(c);
                Assert.That(s.Length, Is.GreaterThan(1000), id);
                double sum = 0;
                for (int i = 1; i < s.Length; i++) sum += (s[i] - s[i - 1]) * (s[i] - s[i - 1]);
                float rmsStep = (float)Math.Sqrt(sum / (s.Length - 1));
                float wrap = Math.Abs(s[0] - s[s.Length - 1]);
                Assert.That(wrap, Is.LessThanOrEqualTo(4f * rmsStep + 1e-4f), id + ": the last sample runs into the first (" + wrap + " vs a typical step " + rmsStep + ")");
            }
        }

        [Test]
        public void TheLabSceneDrivesTheSpeedMusic()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(JamKit.MovementLab.EditorTools.MovementLabBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                MovementAudio sound = null;
                foreach (var go in scene.GetRootGameObjects())
                    if (sound == null) sound = go.GetComponentInChildren<MovementAudio>(true);
                Assert.That(sound, Is.Not.Null);
                Assert.That(sound.music, Is.Not.Null, "the player's speed drives the stems");
                Assert.That(sound.music.stemSet, Is.EqualTo("lab"), "real stems go in Resources/JamKitMusicStems/lab/");
                Assert.That(sound.music.playOnStart, Is.True);
                Assert.That(MovementAudio.FindFeel(sound.body), Is.Not.Null, "the sound follows the speed visuals' tier (one source of truth)");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TheSpeedMusicStemsExistInStepWithMuffledCopies()
        {
            var go = new GameObject("music test");
            try
            {
                var music = go.AddComponent<MovementMusic>();
                music.stemSet = "no-such-set";
                music.Resolve();
                int length = -1;
                for (int slot = StemMixer.Base; slot <= StemMixer.Peak; slot++)
                {
                    var full = music.StemClip(slot);
                    var muffled = music.MuffledClip(slot);
                    Assert.That(full, Is.Not.Null, StemMixer.SlotNames[slot]);
                    Assert.That(muffled, Is.Not.Null, StemMixer.SlotNames[slot] + " muffled");
                    if (length < 0) length = full.samples;
                    Assert.That(full.samples, Is.EqualTo(length), "every stem one length");
                    Assert.That(muffled.samples, Is.EqualTo(length), "the muffled copy in step");
                    float pk = 0f;
                    foreach (var s in Samples(full)) pk = Math.Max(pk, Math.Abs(s));
                    Assert.That(pk, Is.InRange(0.2f, MoveSounds.MaxPeak), full.name);
                }
                Assert.That(music.StemClip(StemMixer.Tension), Is.Null, "no tension stem unless a game brings one");
                var supersonic = MovementAudio.ClipsFor("mv_supersonic", out _);
                Assert.That(supersonic, Is.Not.Empty);
                Assert.That(MovementAudio.ClipsFor("mv_breath_fast", out _), Is.Not.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static float[] Samples(AudioClip c)
        {
            c.LoadAudioData();
            var data = new float[c.samples * c.channels];
            Assert.That(c.GetData(data, 0), Is.True, c.name + " readable (Decompress On Load)");
            return data;
        }
    }
}
