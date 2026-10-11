using System.Collections;
using System.Linq;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Argument.Tests
{
    /// <summary>The argument prototype in the running shell: its chapter opens the scene and the battle; a won battle
    /// is saved (what later battles read) and Continue returns to the menu; the keys drive the verbs through the
    /// shell's rebindable actions (Press as a 0.6 s hold); the world stands still while Nadia argues.</summary>
    public class ArgumentPrototypePlayTests
    {
        TestInput _keys;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _keys?.Dispose();
            _keys = null;
            if (ArgumentHost.Current != null) ArgumentHost.Current.Close(false);
            yield return null;
        }

        static IEnumerator OpenPrototype(string chapter)
        {
            var r = PresenterMode.Jump(ArgumentPrototype.Scene, chapter);
            Assert.That(r, Is.EqualTo(JumpResult.Pending).Or.EqualTo(JumpResult.Jumped), "the prototype's chapter (is the scene in Build Settings?)");
            yield return WaitFor(() => !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == ArgumentPrototype.Scene
                && ArgumentPrototype.Current != null && ArgumentHost.Running, "the prototype and its battle", 20f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheChapterOpensTheBattleAndTheWorldWaits()
        {
            yield return OpenPrototype(ArgumentPrototype.Normal);
            var host = ArgumentHost.Current;
            Assert.That(host.Engine.Started, Is.True);
            Assert.That(host.Engine.Def.id, Is.EqualTo(DreamLenaDemo.Id));
            Assert.That(host.Engine.InPressure, Is.True, "the opening pressure phase");
            Assert.That(GameRoot.Input.GameplayAllowed, Is.False, "world gameplay waits while she argues");
            Assert.That(GameRoot.Input.Bindings.FindRow("argue-challenge").Shown, Is.True, "the argument's Controls rows show");
            Assert.That(GameRoot.Input.Bindings.MoveDpad, Is.True, "the d-pad steers her light");
            Assert.That(host.View.IsShown, Is.True);
            host.Close();
            yield return null;
            Assert.That(ArgumentHost.Running, Is.False);
            Assert.That(GameRoot.Input.GameplayAllowed, Is.True);
            Assert.That(GameRoot.Input.Bindings.FindRow("argue-challenge").Shown, Is.False);
            Assert.That(ArgumentPrototype.Current.MenuShown, Is.True, "back to the prototype's menu");
        }

        [UnityTest]
        public IEnumerator AWonBattleIsSavedForLaterBattles()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            Assert.That(new ArgumentPilot().Play(host.Engine), Is.EqualTo(ArgumentOutcome.Convinced));
            yield return WaitFor(() => host.Mode == ArgueMode.Ended, "the end card", 5f);
            Assert.That(host.View.EndShown, Is.True);
            Assert.That(host.View.EndTitle, Is.EqualTo("Convinced"));
            var saved = ArgumentLedger.Parse(GameRoot.Save.Data.GetEntry(ArgumentLedger.SaveKey));
            Assert.That(saved.StanceOf(DreamLenaDemo.Id), Is.EqualTo(ArgumentStance.Amenable), "kept in the save");
            Assert.That(saved.HasTruth(DreamLenaDemo.TruthLena), Is.True);
            host.Continue();
            yield return null;
            Assert.That(ArgumentHost.Running, Is.False);
            Assert.That(ArgumentPrototype.Current.MenuShown, Is.True);
        }

        [UnityTest]
        public IEnumerator TheKeysDriveTheVerbs()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            var e = host.Engine;
            e.SkipPressure();
            _keys = new TestInput("ArgumentTestKeyboard");
            yield return null;

            // 3 concedes the selected premise.
            Assert.That(host.Select("L1"), Is.True);
            yield return _keys.Press(Key.Digit3);
            yield return _keys.Press();
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Conceded));
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Standing));

            // 1 asks.
            host.Select("L3");
            yield return _keys.Press(Key.Digit1);
            yield return _keys.Press();
            Assert.That(e.Status("L3b"), Is.EqualTo(PremiseStatus.Standing), "asking L3 reveals L3'");

            // 2 challenges: pick the fourth chair (Enter), then the sound reason (arrows, Enter).
            host.Select("L1b");
            yield return _keys.Press(Key.Digit2);
            yield return _keys.Press();
            Assert.That(host.Mode, Is.EqualTo(ArgueMode.PickEvidence));
            int chair = host.TrayIds.ToList().IndexOf(DreamLenaDemo.FourthChair);
            for (int i = 0; i < chair; i++) { yield return _keys.Press(Key.RightArrow); yield return _keys.Press(); }
            yield return _keys.Press(Key.Enter);
            yield return _keys.Press();
            Assert.That(host.Mode, Is.EqualTo(ArgueMode.PickReason));
            int sound = host.Cards.ToList().FindIndex(c => c.reason.kind == ReasonKind.Sound);
            for (int i = 0; i < sound; i++) { yield return _keys.Press(Key.DownArrow); yield return _keys.Press(); }
            yield return _keys.Press(Key.Enter);
            yield return _keys.Press();
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Broken), "the sound reason breaks it");
            Assert.That(e.Status("L1c"), Is.EqualTo(PremiseStatus.Standing), "she falls back");

            // Press is a hold: a tap does nothing, 0.6 s forces it.
            host.Select("L2");
            yield return _keys.Press(Key.Digit4);
            yield return _keys.Press();
            Assert.That(e.Presses, Is.EqualTo(0), "a tap isn't a press");
            _keys.Set(Key.Digit4);
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until && e.Presses == 0) yield return null;
            _keys.Set();
            Assert.That(e.Status("L2"), Is.EqualTo(PremiseStatus.Forced));
            Assert.That(e.Presses, Is.EqualTo(1));
        }
    }
}
