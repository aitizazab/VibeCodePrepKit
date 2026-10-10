using System.Collections;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The speed music in the Movement Lab's real scene: the autopilot's run raises the number of stems
    /// playing (base, then drive at fast, peak at flying), and every switch lands on a bar line. The sound reads the
    /// speed visuals' own tier, so the boom and the music agree on when flying starts.</summary>
    public class MovementMusicPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (Game != null) Game.SetAutopilot(false);
            PresenterMode.Close();
            yield return null;
        }

        static MovementLabGame Game => MovementLabGame.Current;

        static IEnumerator StartLab()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLabPresenter.Scene), Is.True, "MovementLab is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLabPresenter.Scene
                && Game != null && Game.player != null, "the Movement Lab scene");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheAutopilotsRunRaisesTheStemsOnBarLines()
        {
            yield return StartLab();
            var sound = Game.player.GetComponent<MovementAudio>();
            Assert.That(sound, Is.Not.Null);
            var music = sound.music;
            Assert.That(music, Is.Not.Null, "the lab's player drives the speed music");
            yield return WaitFor(() => music.Playing && music.Beat > 0.5, "the stems to start", 5f);
            Assert.That(music.ActiveStems, Is.EqualTo(1), "standing on the start pad: the base alone");
            var feel = MovementAudio.FindFeel(Game.player);
            Assert.That(feel, Is.Not.Null);
            Assert.That(sound.SharesTheVisualTier, Is.True);
            Assert.That(sound.Speed, Is.SameAs(feel.State.Tier), "one tier for the sound and the visuals");
            int booms = feel.Booms;

            Game.SetAutopilot(true);
            int most = 1, switches = 0, lastCount = music.Mixer.SwitchCount;
            float maxSpeed = 0f;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 30f)
            {
                if (most >= 3 && switches >= 1) break;
                if (most >= 2 && switches >= 1 && Time.realtimeSinceStartup - start > 20f) break;
                yield return null;
                maxSpeed = Mathf.Max(maxSpeed, Game.player.Speed);
                Assert.That(music.Tier, Is.EqualTo(feel.State.Tier.Tier), "the music hears the visuals' tier the same frame");
                // (A coroutine wakes after Update: both the boom and the music's tier were set in last frame's LateUpdate.)
                if (feel.Booms != booms)
                {
                    booms = feel.Booms;
                    Assert.That(music.Tier, Is.EqualTo(SpeedTier.Flying), "the boom and the music agree on when flying starts");
                }
                most = Mathf.Max(most, music.ActiveStems);
                int count = music.Mixer.SwitchCount;
                if (count != lastCount)
                {
                    lastCount = count;
                    switches++;
                    double line = music.Mixer.LastSwitchLine, at = music.Mixer.LastSwitchAt;
                    TestContext.Out.WriteLine("switch " + switches + ": bar line " + line + ", applied at beat " + at.ToString("0.000") + ", " + music.ActiveStems + " stems, tier " + music.Tier);
                    Assert.That(line % music.beatsPerBar, Is.EqualTo(0.0).Within(1e-6), "on a bar line");
                    Assert.That(at - line, Is.InRange(0.0, 0.5), "applied as the music crossed the line (within half a beat)");
                }
            }
            Game.SetAutopilot(false);
            TestContext.Out.WriteLine("top speed " + maxSpeed.ToString("0.0") + " m/s, most stems " + most + ", switches " + switches + ", booms " + booms);
            Assert.That(most, Is.GreaterThanOrEqualTo(2), "the run reached the fast tier's drive stem");
            Assert.That(switches, Is.GreaterThanOrEqualTo(1));
        }
    }
}
