using System.Collections;
using JamKit.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.VisitingHours.Tests
{
    /// <summary>
    /// The chain (docs/NIGHTS-1-2.md): Day 1 in style E with every one of its spots, the booth's dive into Night 1's own
    /// scene with the journal carried, the night handing back to Day 2 (the journal still there), Day 2's booth into
    /// Night 2, and the end card. Nights whose scene isn't in the build fall back as the slice does (the street, or the
    /// end card), so the test checks the hand-offs either way.
    /// </summary>
    public class VisitingHoursChainPlayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => BootToTitle();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            VhChainRunner.ForceEnabled = false;
            VisitingHoursGame.FastTransitions = false;
            if (GameRoot.Exists) Chain.Reset(GameRoot.Save.Data);
            yield return null;
        }

        static VisitingHoursGame Game => VisitingHoursGame.Current;

        static IEnumerator WaitForWaking(ChainSegment segment)
        {
            yield return WaitFor(() => !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == "VisitingHours" && Game != null
                && Game.ChainSegment == segment && (Game.CurrentMode == VisitingHoursGame.Mode.Explore || Game.CurrentMode == VisitingHoursGame.Mode.End),
                segment + " in the Visiting Hours scene", 30f);
            // The days open with a few lines (Rafael's handover, Day 2's intro): they start a frame or two after the place is
            // up, so wait for them and play them through, or they'd cover the prompt while a test looks around.
            if (segment != ChainSegment.End)
            {
                yield return WaitFor(() => GameRoot.Dialogue.IsActive, segment + "'s opening lines");
                yield return FinishDialogue();
            }
        }

        /// <summary>Stands Nadia at a thing's approach (as the smoke run does) and checks the prompt picks that thing.</summary>
        static IEnumerator Picks(string id, string day)
        {
            if (GameRoot.Dialogue.IsActive) yield return FinishDialogue();
            Assert.That(Game.CurrentMode, Is.EqualTo(VisitingHoursGame.Mode.Explore), day + ": free to look at " + id);
            Assert.That(Game.BuiltArea.Spot(id), Is.Not.Null, day + " has " + id);
            Game.StandAt(id);
            yield return Frames(3);
            Assert.That(Game.Target, Is.Not.Null, day + ": something in reach in front of " + id);
            Assert.That(Game.Target.def.id, Is.EqualTo(id), day + ": the prompt picks what's in front of Nadia");
        }

        static IEnumerator SkipToTheDive(SliceArea booth)
        {
            Assert.That(Game.DebugSkip(), Is.True);
            yield return WaitFor(() => Game != null && Game.Area == booth && Game.CurrentMode == VisitingHoursGame.Mode.Explore, booth.ToString(), 20f);
            if (GameRoot.Dialogue.IsActive) GameRoot.Dialogue.Stop();
            yield return Frames(2);
            if (Game != null && Game.CurrentMode == VisitingHoursGame.Mode.Explore) Game.DebugSkip();
        }

        [UnityTest]
        public IEnumerator TheChainHandsOffFromDayToNightAndBackToTheEndCard()
        {
            VhChainRunner.ForceEnabled = true;
            VisitingHoursGame.FastTransitions = true;
            GameRoot.Save.Data.seenHowToPlay = true;
            Assert.That(VhChainRunner.Instance, Is.Not.Null, "VhBoot puts the chain's runner on the GameRoot");
            Assert.That(GameRoot.Instance.SceneService.Load("VisitingHours"), Is.True);
            yield return WaitForWaking(ChainSegment.Day1);
            Assert.That(Game.Area, Is.EqualTo(SliceArea.Hospital));
            foreach (var def in Game.config.examinables)
                if (def.area == SliceArea.Hospital) Assert.That(Game.BuiltArea.Spot(def.id), Is.Not.Null, "Day 1's ward has " + def.id);

            // Day 1 → the booth → under. Night 1 in its own scene if it's built, else the slice's street.
            yield return SkipToTheDive(SliceArea.Booth);
            string night1 = VhChainRunner.NightScene(ChainSegment.Night1);
            if (night1 == null)
            {
                yield return WaitFor(() => Game != null && Game.Area == SliceArea.Street, "the slice's street (no Night 1 scene in the build)");
                Assert.Inconclusive("Night 1's scene isn't in Build Settings: rebuild the shell scenes for the whole chain");
            }
            yield return WaitFor(() => !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == night1 && VhChainRunner.Instance.InNight, night1, 30f);
            Assert.That(Chain.Knows(GameRoot.Save.Data, SliceContent.FactChart), Is.True, "Day 1's facts are carried into the night");
            Assert.That(Chain.Segment(GameRoot.Save.Data), Is.EqualTo(ChainSegment.Night1));

            // Night 1 ends (as if the street's NightEnded fired): Day 2, with the journal.
            yield return Frames(10);
            VhChainRunner.Instance.EndNightNow();
            yield return WaitForWaking(ChainSegment.Day2);
            Assert.That(Game.Area, Is.EqualTo(SliceArea.HospitalDay2));
            Assert.That(Game.Journal.Knows(SliceContent.FactChart), Is.True, "the journal came back from the night");
            foreach (var def in Game.config.examinables)
                if (def.area == SliceArea.HospitalDay2) Assert.That(Game.BuiltArea.Spot(def.id), Is.Not.Null, "Day 2's ward has " + def.id);

            // Day 2 → its booth → Night 2 (the house) → the end card.
            yield return SkipToTheDive(SliceArea.BoothDay2);
            string night2 = VhChainRunner.NightScene(ChainSegment.Night2);
            if (night2 != null)
            {
                yield return WaitFor(() => !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == night2 && VhChainRunner.Instance.InNight, night2, 30f);
                Assert.That(Chain.Knows(GameRoot.Save.Data, SliceContent.FactDidLenaGetOut), Is.True, "Day 2's facts are carried into Night 2");
                yield return Frames(10);
                VhChainRunner.Instance.EndNightNow();
            }
            yield return WaitForWaking(ChainSegment.End);
            yield return WaitFor(() => Game.CurrentMode == VisitingHoursGame.Mode.End, "the end card");
            Assert.That(Game.Hud.EndBody, Does.Contain("Walt:").And.Contain("Lena:").And.Contain("Force"));
        }

        [UnityTest]
        public IEnumerator EveryDuePlaceOnTheDaysCanBeLookedAt()
        {
            VisitingHoursGame.FastTransitions = true;
            GameRoot.Save.Data.seenHowToPlay = true;
            Assert.That(GameRoot.Instance.SceneService.Load("VisitingHours"), Is.True);
            yield return WaitForWaking(ChainSegment.Day1);
            // Every place on the bigger floor (docs/redesign/day.md), each from where she stands to look at it.
            foreach (var id in new[] { "rafael", "wristband", "vitals", "bouquet", "phone", "elias", "chart", "effects", "police_report", "photo", "station", "stairwell",
                "okafor", "round_board", "locker", "rota", "kettle", "june", "tv", "vending", "remembrance_book", "candles", "west_window", "sit1" })
                yield return Picks(id, "Day 1");
            Game.DebugJumpTo(SliceArea.HospitalDay2);
            yield return Frames(2);
            foreach (var id in new[] { "rafael2", "visitor", "wristband2", "round2", "vitals2", "sit", "stairwell2", "elias", "chart", "photo",
                "brandt", "brandt_band", "okafor", "okafor_chart", "round_board", "remembrance_book" })
                yield return Picks(id, "Day 2");
        }
    }
}
