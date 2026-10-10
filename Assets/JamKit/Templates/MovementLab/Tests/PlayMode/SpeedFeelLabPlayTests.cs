using System.Collections;
using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using JamKit.Tests.Play;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.MovementLab.Tests
{
    /// <summary>The speed visuals during the Gauntlet autopilot (docs/SPEED-FEEL.md "Speed feel (built)"): the boom fires
    /// once per real crossing into flying (and the camera's FOV punches); the streak emitter's rate follows the tier
    /// (none while running, more at flying than at fast); footprints are laid at flying on foot plants only; the
    /// full-screen pass runs at flying; the per-frame code allocates nothing in the steady state. Numbers go to the
    /// test output.</summary>
    public class SpeedFeelLabPlayTests
    {
        float _motion;
        bool _reduce;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _motion = GameRoot.Settings.motionEffects;
            _reduce = GameRoot.Settings.reduceEffects;
            GameRoot.Settings.motionEffects = 1f;
            GameRoot.Settings.reduceEffects = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (GameRoot.Exists)
            {
                GameRoot.Settings.motionEffects = _motion;
                GameRoot.Settings.reduceEffects = _reduce;
            }
            if (MovementLabGame.Current != null) MovementLabGame.Current.SetAutopilot(false);
            PresenterMode.Close();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheGauntletAutopilotShowsTheSpeedVisualsByTier()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLabPresenter.Scene), Is.True, "MovementLab is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLabPresenter.Scene
                && MovementLabGame.Current != null && GauntletRun.Current != null && GauntletRun.Current.Clock != null, "the Movement Lab and its Gauntlet");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
            var game = MovementLabGame.Current;
            var run = GauntletRun.Current;
            var cam = game.player.cam;
            var feel = cam.GetComponent<SpeedFeel>();
            var feelCam = game.player.GetComponentInChildren<MovementCamera>();
            var streaks = cam.GetComponent<SpeedStreaks>();
            var feet = cam.GetComponent<SpeedFootfalls>();
            var screen = cam.GetComponent<SpeedScreenFx>();
            var lines = cam.GetComponent<SpeedLines>();
            Assert.That(feel, Is.Not.Null);
            Assert.That(streaks, Is.Not.Null);
            Assert.That(feet, Is.Not.Null);
            Assert.That(screen, Is.Not.Null);

            DebugMenu.TimeScale = 2f;
            run.Restart();
            yield return Frames(2);
            var watch = new SpeedFeelWatch();
            var probe = cam.gameObject.AddComponent<SpeedFeelProbe>();
            probe.tick = () => watch.Frame(feel, feelCam, streaks, feet, screen);
            bool measured = false;
            game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 60f;
            while (run.Clock.State != GauntletState.Finished && Time.realtimeSinceStartup < until)
            {
                if (!measured && feel.Levels.flying >= 0.99f)
                {
                    measured = true;
                    SpeedFeelWatch.AssertNoAllocation(feel, streaks, feet, screen, lines);
                }
                yield return null;
            }
            game.SetAutopilot(false);
            UnityEngine.Object.Destroy(probe);
            TestContext.Out.WriteLine(watch.ToString());
            TestContext.Out.WriteLine($"puffs {feet.PuffsEmitted} over {feet.FastSteps} fast steps, footprints {feet.FootprintsLaid}, motes emitted {streaks.Emitted}");

            Assert.That(run.Clock.State, Is.EqualTo(GauntletState.Finished), "the autopilot finished (speed visuals don't change the run)");
            Assert.That(watch.booms, Is.GreaterThanOrEqualTo(1), "the boom fired on crossing into flying");
            Assert.That(watch.boomsOffCrossing, Is.EqualTo(0), "every boom is on a crossing");
            Assert.That(watch.booms, Is.LessThanOrEqualTo(watch.crossings), "at most one boom per crossing");
            Assert.That(watch.maxBoomFov, Is.GreaterThan(3f), "the FOV punched out");
            Assert.That(watch.maxRateRunning, Is.EqualTo(0f), "no streaks while running slowly");
            Assert.That(watch.framesFlying, Is.GreaterThan(0), "it flew");
            Assert.That(watch.maxRateFlying, Is.GreaterThan(watch.maxRateFast), "more streaks at flying than at fast");
            Assert.That(watch.maxAlive, Is.GreaterThan(10), "streaks were in the air");
            Assert.That(feet.PuffsEmitted, Is.GreaterThan(0), "puffs at fast");
            Assert.That(feet.FootprintsLaid, Is.GreaterThan(0), "footprints at flying");
            Assert.That(watch.footprintsOffFlying, Is.EqualTo(0), "footprints only at flying");
            Assert.That(watch.footprintsOffStep, Is.EqualTo(0), "footprints only on foot plants");
            Assert.That(watch.screenFrames, Is.GreaterThan(0), "the full-screen pass ran (edge blur at flying)");
            Assert.That(watch.maxBlur, Is.GreaterThan(0f));
            Assert.That(measured, Is.True, "the steady state at flying was measured for allocations");
        }

        [UnityTest]
        public IEnumerator ReduceEffectsTurnsTheBlurAndTheChromaticPulseOffInTheLab()
        {
            GameRoot.Settings.reduceEffects = true;
            Assert.That(GameRoot.Instance.SceneService.Load(MovementLabPresenter.Scene), Is.True);
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == MovementLabPresenter.Scene
                && MovementLabGame.Current != null && GauntletRun.Current != null && GauntletRun.Current.Clock != null, "the Movement Lab");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            var game = MovementLabGame.Current;
            var cam = game.player.cam;
            var feel = cam.GetComponent<SpeedFeel>();
            var screen = cam.GetComponent<SpeedScreenFx>();
            DebugMenu.TimeScale = 2f;
            GauntletRun.Current.Restart();
            yield return Frames(2);
            game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 40f;
            int booms = 0;
            float maxBlur = 0f, maxChroma = 0f, maxFlying = 0f;
            var probe = cam.gameObject.AddComponent<SpeedFeelProbe>();
            probe.tick = () =>
            {
                if (feel.BoomThisFrame) booms++;
                maxBlur = Mathf.Max(maxBlur, screen.EdgeBlur);
                maxChroma = Mathf.Max(maxChroma, screen.Chroma);
                maxFlying = Mathf.Max(maxFlying, feel.Levels.flying);
            };
            while (Time.realtimeSinceStartup < until && GauntletRun.Current.Clock.State != GauntletState.Finished && (booms == 0 || maxFlying < 0.99f))
                yield return null;
            game.SetAutopilot(false);
            UnityEngine.Object.Destroy(probe);
            Assert.That(booms, Is.GreaterThan(0), "it crossed into flying");
            Assert.That(maxFlying, Is.GreaterThan(0.99f));
            Assert.That(maxBlur, Is.EqualTo(0f), "no edge blur with reduce effects");
            Assert.That(maxChroma, Is.EqualTo(0f), "no chromatic pulse with reduce effects");
        }
    }
}
