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

namespace JamKit.DreamEscape.Tests
{
    /// <summary>The speed visuals during the dream escape's autopilot run (docs/SPEED-FEEL.md "Speed feel (built)"): the
    /// boom fires once per real crossing; streaks follow the tier; footprints at flying on foot plants; V7, the dream
    /// answers at flying (the grade rises, flowers open, the static is drawn further back) while the pursuer itself is
    /// untouched (the drawn wall sits exactly the recede behind the pursuer's real distance, and the run still escapes
    /// uncaught); no allocation per frame in the steady state. Numbers go to the test output.</summary>
    public class SpeedFeelDreamPlayTests
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
            if (Game != null)
            {
                Game.SetAutopilot(false);
                Game.player.InputEnabled = true;
            }
            PresenterMode.Close();
            yield return null;
        }

        static DreamEscapeGame Game => DreamEscapeGame.Current;

        [UnityTest]
        public IEnumerator TheEscapeAutopilotShowsTheSpeedVisualsAndTheDreamAnswers()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(DreamEscapePresenter.Scene), Is.True, "DreamEscape is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == DreamEscapePresenter.Scene
                && Game != null && Game.Course != null, "the dream escape scene");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);

            var cam = Game.player.cam;
            var feel = cam.GetComponent<SpeedFeel>();
            var feelCam = Game.player.GetComponentInChildren<MovementCamera>();
            var streaks = cam.GetComponent<SpeedStreaks>();
            var feet = cam.GetComponent<SpeedFootfalls>();
            var screen = cam.GetComponent<SpeedScreenFx>();
            var lines = cam.GetComponent<SpeedLines>();
            var answer = cam.GetComponent<DreamSpeedAnswer>();
            Assert.That(feel, Is.Not.Null);
            Assert.That(answer, Is.Not.Null);
            var view = Game.staticView;

            DebugMenu.TimeScale = 2f;
            Game.JumpToChapter(DreamEscapeCourse.ChapterStart);
            yield return Frames(2);
            int caught = Game.Caught;
            var watch = new SpeedFeelWatch();
            float maxAnswer = 0f, maxSat = 0f, maxRecede = 0f, worstWallOffset = 0f;
            int recedeChecks = 0;
            var probe = cam.gameObject.AddComponent<SpeedFeelProbe>();
            probe.tick = () =>
            {
                watch.Frame(feel, feelCam, streaks, feet, screen);
                maxAnswer = Mathf.Max(maxAnswer, answer.Answer);
                maxSat = Mathf.Max(maxSat, screen.GradeSaturation);
                maxRecede = Mathf.Max(maxRecede, view.visualRecede);
                // Visual only: the wall is drawn the recede behind the pursuer's real distance (which nothing else moves).
                if (Game.Chasing && view.Shown && !Game.Respawning)
                {
                    float real = Game.Course.route.PointAt(Game.Pursuer.S, 0f).Z;
                    float drawnBack = real - view.transform.position.z;
                    worstWallOffset = Mathf.Max(worstWallOffset, Mathf.Abs(drawnBack - view.visualRecede));
                    recedeChecks++;
                }
            };
            bool measured = false;
            Game.SetAutopilot(true);
            float until = Time.realtimeSinceStartup + 70f;
            while (Game.Finishes == 0 && Time.realtimeSinceStartup < until)
            {
                if (!measured && feel.Levels.flying >= 0.99f && !Game.Respawning)
                {
                    measured = true;
                    SpeedFeelWatch.AssertNoAllocation(feel, streaks, feet, screen, lines, answer);
                }
                yield return null;
            }
            Game.SetAutopilot(false);
            Object.Destroy(probe);
            TestContext.Out.WriteLine(watch.ToString());
            TestContext.Out.WriteLine($"the dream answered up to {maxAnswer:0.00} (saturation +{maxSat:0.00}, the static drawn {maxRecede:0.0} m back), {answer.FlowersOpened} flowers; "
                + $"puffs {feet.PuffsEmitted}, footprints {feet.FootprintsLaid}; wall offset error {worstWallOffset:0.000} m over {recedeChecks} frames");

            Assert.That(Game.Finishes, Is.GreaterThan(0), "the autopilot still reaches the light");
            Assert.That(Game.Caught - caught, Is.EqualTo(0), "the static never caught it (the pursuer is unchanged)");
            Assert.That(watch.booms, Is.GreaterThanOrEqualTo(1), "the boom fired");
            Assert.That(watch.boomsOffCrossing, Is.EqualTo(0), "every boom is on a crossing");
            Assert.That(watch.booms, Is.LessThanOrEqualTo(watch.crossings), "at most one boom per crossing");
            Assert.That(watch.maxRateRunning, Is.EqualTo(0f), "no confetti while running slowly");
            Assert.That(watch.maxRateFlying, Is.GreaterThan(watch.maxRateFast), "more at flying than at fast");
            Assert.That(feet.FootprintsLaid, Is.GreaterThan(0), "footprints at flying");
            Assert.That(watch.footprintsOffFlying, Is.EqualTo(0), "footprints only at flying");
            Assert.That(watch.footprintsOffStep, Is.EqualTo(0), "footprints only on foot plants");
            Assert.That(maxAnswer, Is.GreaterThan(0.95f), "the dream answered at flying");
            Assert.That(maxSat, Is.GreaterThan(0.2f), "brighter and more saturated");
            Assert.That(answer.FlowersOpened, Is.GreaterThan(0), "flowers opened along the route");
            Assert.That(maxRecede, Is.GreaterThan(2f), "the static fell back (visually)");
            Assert.That(recedeChecks, Is.GreaterThan(0));
            // The recede is applied the moment DreamSpeedAnswer sets it (2026-10-12): before, the wall was drawn with the
            // previous frame's recede, off by one frame's change (up to 0.31 m on a long frame at 23.7 m/s).
            Assert.That(worstWallOffset, Is.LessThan(0.02f), "the wall is drawn exactly the recede behind the pursuer's real distance, in the same frame");
            Assert.That(measured, Is.True, "the steady state at flying was measured for allocations");
        }
    }
}
