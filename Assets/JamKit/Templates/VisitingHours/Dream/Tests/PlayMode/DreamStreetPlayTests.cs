using System;
using System.Collections;
using System.IO;
using System.Text;
using JamKit.DeepBlock;
using JamKit.Presenting;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.DreamEscape.Tests
{
    /// <summary>Night 1 on Larch Hill on the real controller (docs/redesign/world.md): the way in (the creek's double jump,
    /// the garage door's slide, the paper, Walt and his argument); each leg of the critical path under the autopilot; the
    /// Rewind catching her when she stops and letting her through when she runs; the gate and the door; the loop and the
    /// paper before Walt; the bedside's dream changes; and the recorded run of the whole critical path (Explicit: it writes
    /// a frame every 0.5 s to out/hill-run/&lt;time&gt;/). The save's journal and Walt's outcome are cleared around each test.</summary>
    public class DreamStreetPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugMenu.TimeScale = 1f;
            if (Game != null) Game.Drive(null);
            if (GameRoot.Exists) Clear();
            PresenterMode.Close();
            yield return null;
        }

        static void Clear()
        {
            GameRoot.Save.Data.SetEntry(DreamJournal.Key, null);
            GameRoot.Save.Data.SetEntry(LarchStreetStory.WaltOutcomeKey, null);
            GameRoot.Save.Data.SetEntry("vh_arguments", null);
            GameRoot.Save.Data.ClearFlag(LarchStreetStory.NightDoneFlag);
        }

        static DreamStreetGame Game => DreamStreetGame.Current;
        static string Journal => GameRoot.Save.Data.GetEntry(DreamJournal.Key);

        static IEnumerator StartStreet()
        {
            Assert.That(GameRoot.Instance.SceneService.Load(DreamStreetPresenter.Scene), Is.True, "DreamStreet is in Build Settings");
            yield return WaitFor(() => GameRoot.Flow.InRun && !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == DreamStreetPresenter.Scene
                && Game != null && Game.Hill != null, "the hill");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheWayInCrossesTheCreekAndTheShopAndWaltGetsUp()
        {
            yield return StartStreet();
            Assert.That(Game.Phase, Is.EqualTo(StreetPhase.Arrival));
            Assert.That(Game.waltGate.activeSelf, Is.True, "Walt on his wall");
            DebugMenu.TimeScale = 2f;
            Game.Drive(Game.Hill.wayIn);
            yield return WaitFor(() => Game.Phase >= StreetPhase.Yard, "the autopilot over the creek and through the shop", 40f);
            Assert.That(Game.Wading, Is.False, "the double jump made the creek");
            Assert.That(Game.Story.Knows(LarchStreetStory.MemSaturdayPaper), Is.True, "the paper on the counter, on the way through");
            Assert.That(Game.Story.Knows(LarchStreetStory.DedWaltDead), Is.True, "with Day 1's card: Walt died in 2015");
            yield return WaitFor(() => Game.Story.WaltAside, "Walt's argument and his getting up", 40f);
            Game.Drive(null);
            Assert.That(Game.waltGate.activeSelf, Is.False, "the low wall is free");
            Assert.That(GameRoot.Save.Data.GetEntry(LarchStreetStory.WaltOutcomeKey), Is.EqualTo("heardout"));
            Assert.That(Game.Phase, Is.EqualTo(StreetPhase.Climb));
        }

        [UnityTest]
        public IEnumerator EachLegOfTheCriticalPathRunsOnTheRealController([Values(0, 1, 2, 3)] int leg)
        {
            yield return StartStreet();
            string[] chapters = { LarchHill.ChapterBackWay, LarchHill.ChapterLane, LarchHill.ChapterLane, LarchHill.ChapterOak };
            Game.JumpToChapter(chapters[leg]);
            yield return Frames(2);
            var route = Game.Hill.night1[leg];
            Game.player.Teleport(new Vector3(route.start.X, route.start.Y, route.start.Z), route.startYaw, Vector3.zero);
            yield return Frames(2);
            DebugMenu.TimeScale = 2f;
            Game.Drive(route);
            float finish = Game.Hill.finishY[route];
            var end = route.course.route.PointAt(route.finishS, 0f);
            yield return WaitFor(() => Game.player.Sim.Grounded && Vector2.Distance(new Vector2(Game.player.transform.position.x, Game.player.transform.position.z), new Vector2(end.X, end.Z)) < 1.5f
                && Game.player.transform.position.y > finish - 0.3f, route.name + " on the real controller", 60f);
            Assert.That(Game.Respawns, Is.EqualTo(0), "never out of the world");
        }

        [UnityTest]
        public IEnumerator TheRewindCatchesHerWhenSheStopsAndLetsHerThroughWhenSheRuns()
        {
            yield return StartStreet();
            Game.JumpToChapter(LarchHill.ChapterRewind);
            yield return Frames(3);
            yield return WaitFor(() => Game.WaveOn, "the Rewind begins when she's on Rowan Street", 5f);
            yield return WaitFor(() => Game.Catches >= 1, "standing still, the wave catches her", 30f);
            Assert.That(Game.player.transform.position.x, Is.LessThan(-100f), "back at the west end");
            yield return WaitFor(() => Game.WaveOn, "the wave again", 5f);
            Game.Drive(Game.Hill.night1[4]);
            yield return WaitFor(() => Game.Ending || Game.player.transform.position.x >= LarchHill.RewindSafeX, "she outruns the wave to the gate", 60f);
            Assert.That(Game.Catches, Is.EqualTo(1), "a clean run isn't caught");
        }

        [UnityTest]
        public IEnumerator AtTheGateTheDoorClosesAndTheNightEnds()
        {
            yield return StartStreet();
            Game.JumpToChapter(LarchHill.ChapterHouse);
            yield return Frames(2);
            bool ended = false;
            Action onEnd = () => ended = true;
            DreamStreetGame.NightEnded += onEnd;
            try
            {
                var gate = Game.Hill.Spot(LarchHill.SpotGate).position;
                Game.player.Teleport(new Vector3(gate.X, gate.Y - 0.98f, gate.Z), 0f, Vector3.zero);
                yield return WaitFor(() => Game.Ending, "the garden gate", 3f);
                Assert.That(Game.porchLight.enabled, Is.True, "the porch light on");
                Assert.That(Game.eliasInside.gameObject.activeSelf, Is.True, "Eli in the window");
                yield return WaitFor(() => !Game.porchLight.enabled, "the door shut and the porch light out", 20f);
                Assert.That(Quaternion.Angle(Game.frontDoor.localRotation, Quaternion.identity), Is.LessThan(1f), "the door is shut");
                yield return WaitFor(() => ended, "Mags's last line and the fade", 20f);
                Assert.That(DreamJournal.Has(Journal, LarchStreetStory.MemTheHouse), Is.True);
                Assert.That(GameRoot.Save.Data.HasFlag(LarchStreetStory.NightDoneFlag), Is.True);
            }
            finally { DreamStreetGame.NightEnded -= onEnd; }
        }

        [UnityTest]
        public IEnumerator TheLoopAndThePaperMakeTheSameSaturdayBeforeWalt()
        {
            yield return StartStreet();
            DebugMenu.TimeScale = 4f;
            yield return WaitFor(() => Game.HeardLine >= 2, "Mrs. Lindqvist's line, twice, heard from the arrival", 40f);
            DebugMenu.TimeScale = 1f;
            Assert.That(Game.Story.Knows(LarchStreetStory.MemNeighboursLoop), Is.True);
            var paper = Game.paper.transform.position;
            Game.player.Teleport(new Vector3(paper.x, 0.02f, paper.z - 1.2f), 0f, Vector3.zero);
            yield return WaitFor(() => Game.Story.Knows(LarchStreetStory.MemSaturdayPaper), "Saturday's paper", 3f);
            Assert.That(Game.Story.Knows(LarchStreetStory.DedSameSaturday), Is.True, "He's living one Saturday, over and over");
            Assert.That(Game.Story.WaltAside, Is.False, "before Walt's argument");
            Assert.That(DreamJournal.Has(Journal, DreamJournal.SaturdayPaper), Is.True);
        }

        [UnityTest]
        public IEnumerator TheBedsideChangesTheNight()
        {
            yield return StartStreet();
            Assert.That(Game.creekPlank.activeSelf, Is.False, "no plank unless she told him about the creek");
            Assert.That(Game.hospitalLit.activeSelf, Is.False);
            Assert.That(Game.ledger.activeSelf, Is.False, "Walt's ledger is put away");
            Game.forceDreamChanges = new[] { JamKit.VisitingHours.DreamChanges.CreekPlank, JamKit.VisitingHours.DreamChanges.HospitalLit,
                JamKit.VisitingHours.DreamChanges.WaltLedger, JamKit.VisitingHours.DreamChanges.WaltWarm };
            Game.NewNight();
            yield return Frames(2);
            Assert.That(Game.creekPlank.activeSelf, Is.True, "the plank across the creek");
            Assert.That(Game.hospitalLit.activeSelf, Is.True, "St. Brigid's lit");
            Assert.That(Game.ledger.activeSelf, Is.True, "the ledger open on the bench");
            Assert.That(Game.WaltWarm, Is.True);
        }

        /// <summary>The recorded run (main runs it for the judge): the whole of Night 1's critical path under the autopilot
        /// on the real controller, a 1920 x 1080 frame every 0.5 s and a timeline (time, leg, phase, position, speed, mode)
        /// to out/hill-run/&lt;time&gt;/.</summary>
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator RecordTheNight1CriticalPath()
        {
            yield return StartStreet();
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "out", "hill-run", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            var cam = Game.player.cam;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { name = "hill-run" };
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            var timeline = new StringBuilder("t,leg,phase,x,y,z,speed,mode,catches\n");
            Game.DriveNight1();
            float t = 0f, nextShot = 0f;
            int frame = 0;
            while (t < 600f && !Game.Ending)
            {
                yield return null;
                t += Time.deltaTime;
                var p = Game.player.transform.position;
                timeline.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00},{1},{2},{3:0.00},{4:0.00},{5:0.00},{6:0.0},{7},{8}\n",
                    t, Game.ChainLeg, Game.Phase, p.x, p.y, p.z, Game.player.Speed, Game.player.Mode, Game.Catches);
                if (t < nextShot) continue;
                nextShot += 0.5f;
                var prev = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = prev;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, $"run-{frame++:0000}.jpg"), tex.EncodeToJPG(88));
            }
            File.WriteAllText(Path.Combine(dir, "timeline.csv"), timeline.ToString());
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            Debug.Log("JAMKIT_HILL_RUN " + dir + " (" + frame + " frames, " + t.ToString("0.0") + " s, ended at the gate: " + Game.Ending + ")");
            Assert.That(Game.Ending, Is.True, "the autopilot reached the Lasnames' gate; see " + dir);
        }
    }
}
