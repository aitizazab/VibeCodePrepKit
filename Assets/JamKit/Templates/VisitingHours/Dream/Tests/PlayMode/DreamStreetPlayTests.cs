using System.Collections;
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
    /// <summary>Night 1's street on the real controller (docs/LARCH-HILL.md): the autopilot's way in double jumps the creek,
    /// slides under the garage door and meets Walt (the memory and the deduction in the save's dream journal), and the
    /// gate opens once he steps aside; the road climbs to the top; at the garden gate the door closes and the porch light
    /// goes out, and the night ends; the neighbours' loop heard twice and the paper make the same Saturday. The save's
    /// dream journal and Walt's outcome are cleared before and after each test.</summary>
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
                && Game != null && Game.Hill != null, "the street");
            yield return WaitFor(() => GameRoot.Ui.FadeAlpha == 0f, "the scene's fade-in", 5f);
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator TheWayInCrossesTheCreekAndTheShopAndWaltStepsAside()
        {
            yield return StartStreet();
            Assert.That(Game.Phase, Is.EqualTo(StreetPhase.Arrival));
            Assert.That(Game.LineText, Does.Contain(LarchStreetLines.Placeholder), "Mags, marked a placeholder");
            Assert.That(Game.waltGate.activeSelf, Is.True, "the way up is shut");
            DebugMenu.TimeScale = 2f;
            Game.Drive(Game.Hill.wayIn);
            yield return WaitFor(() => Game.Phase >= StreetPhase.Yard, "the autopilot over the creek and through the shop", 30f);
            Assert.That(Game.Respawns, Is.EqualTo(0), "no fall: the double jump made the creek");
            Assert.That(Game.Story.Knows(LarchStreetStory.MemWaltAlive), Is.True, "Walt alive, waving");
            Assert.That(Game.Story.Knows(LarchStreetStory.DedWaltDead), Is.True, "with Day 1's card: Walt died in 2015");
            Assert.That(DreamJournal.Has(Journal, LarchStreetStory.DedWaltDead), Is.True, "kept in the save's dream journal");
            // Under the autopilot the argument is the debug choice (heard out): he steps aside.
            yield return WaitFor(() => Game.Story.WaltAside, "Walt's argument and his stepping aside", 30f);
            Game.Drive(null);
            Assert.That(Game.waltGate.activeSelf, Is.False, "the gate opens");
            Assert.That(GameRoot.Save.Data.GetEntry(LarchStreetStory.WaltOutcomeKey), Is.EqualTo("heardout"), "how she treated him, kept");
            Assert.That(Game.Phase, Is.EqualTo(StreetPhase.Climb));
        }

        [UnityTest]
        public IEnumerator TheRoadClimbsToTheTopAndAtTheGateTheDoorCloses()
        {
            yield return StartStreet();
            Game.JumpToChapter(LarchHill.ChapterRoad);
            yield return Frames(2);
            Assert.That(Game.Phase, Is.EqualTo(StreetPhase.Climb));
            DebugMenu.TimeScale = 2f;
            var road = Game.Hill.routes.Find(r => r.name == LarchHill.RouteRoad);
            Game.Drive(road);
            yield return WaitFor(() => Game.Phase >= StreetPhase.Top, "the autopilot up the road's two switchbacks to the top", 60f);
            Game.Drive(null);
            Assert.That(Game.eliasOutside.activeSelf || Game.Speaking, Is.True, "Elias at the barbecue, going in as she arrives");
            bool ended = false;
            System.Action onEnd = () => ended = true;
            DreamStreetGame.NightEnded += onEnd;
            try
            {
                var gate = Game.Hill.Spot(LarchHill.SpotGate).position;
                Game.player.Teleport(new Vector3(gate.X, gate.Y - 0.98f, gate.Z), 0f, Vector3.zero);
                yield return WaitFor(() => Game.Ending, "the garden gate", 3f);
                Assert.That(Game.porchLight.enabled, Is.True, "the porch light on");
                Assert.That(Game.eliasInside.gameObject.activeSelf, Is.True, "Elias in the window");
                yield return WaitFor(() => !Game.porchLight.enabled, "the door shut and the porch light out", 20f);
                Assert.That(Quaternion.Angle(Game.frontDoor.localRotation, Quaternion.identity), Is.LessThan(1f), "the door is shut");
                Assert.That(Game.eliasInside.gameObject.activeSelf, Is.False, "Lena took him inside");
                yield return WaitFor(() => ended, "Mags's last line and the fade", 20f);
                Assert.That(Game.Phase, Is.EqualTo(StreetPhase.Ended));
                Assert.That(DreamJournal.Has(Journal, LarchStreetStory.MemTheHouse), Is.True);
                Assert.That(GameRoot.Save.Data.HasFlag(LarchStreetStory.NightDoneFlag), Is.True);
            }
            finally
            {
                DreamStreetGame.NightEnded -= onEnd;
            }
        }

        [UnityTest]
        public IEnumerator TheNeighboursLoopAndThePaperMakeTheSameSaturday()
        {
            yield return StartStreet();
            Game.JumpToChapter(LarchHill.ChapterRoad);
            yield return Frames(2);
            var paper = Game.paper.transform.position;
            Game.player.Teleport(new Vector3(paper.x, paper.y - 0.35f, paper.z - 0.6f), 0f, Vector3.zero);
            yield return WaitFor(() => Game.Story.Knows(LarchStreetStory.MemSaturdayPaper), "Saturday's paper", 3f);
            Assert.That(DreamJournal.Has(Journal, DreamJournal.SaturdayPaper), Is.True, "the id Night 2 looks for, in the journal");
            Assert.That(Game.paper.activeSelf, Is.False);
            // Stand in the neighbours' garden until her line comes round twice (a 24 s loop).
            var at = Game.Hill.Spot(LarchHill.SpotSprinkler).position;
            Game.player.Teleport(new Vector3(at.X - 2f, at.Y + 0.02f, at.Z - 1f), 0f, Vector3.zero);
            DebugMenu.TimeScale = 4f;
            yield return WaitFor(() => Game.HeardLine >= 2, "her line, twice", 30f);
            Assert.That(Game.Story.Knows(LarchStreetStory.MemNeighboursLoop), Is.True);
            Assert.That(Game.Story.Knows(LarchStreetStory.DedSameSaturday), Is.True, "He's living one Saturday, over and over");
            Assert.That(DreamJournal.Has(Journal, LarchStreetStory.DedSameSaturday), Is.True, "for Night 2's battle");
        }
    }
}
