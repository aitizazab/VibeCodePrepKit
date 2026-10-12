using System.Collections;
using System.Linq;
using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Tests.Play;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static JamKit.Tests.Play.ShellTestUtil;

namespace JamKit.Argument.Tests
{
    /// <summary>The argument prototype in the running shell: the whole battle driven by a gamepad alone (one focus
    /// model: d-pad moves, A chooses, B backs, the shortcut buttons), the transitions in and out of the pressure box,
    /// the world's ambience silent during the battle and back after, a won battle saved for later battles, the keyboard
    /// shortcuts and the 0.6 s Press hold, and Continue on the very frame the battle ends.</summary>
    public class ArgumentPrototypePlayTests
    {
        TestInput _input;
        Gamepad _pad;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return BootToTitle();
            _input = new TestInput("ArgumentTestKeyboard");
            _pad = _input.AddGamepad("ArgumentTestPad");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _input?.Dispose();
            _input = null;
            if (ArgumentHost.Current != null) ArgumentHost.Current.Close(false);
            yield return null;
        }

        static IEnumerator OpenPrototype(string chapter)
        {
            var r = PresenterMode.Jump(ArgumentPrototype.Scene, chapter);
            Assert.That(r, Is.EqualTo(JumpResult.Pending).Or.EqualTo(JumpResult.Jumped), "the prototype's chapter (is the scene in Build Settings?)");
            yield return WaitFor(() => !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == ArgumentPrototype.Scene
                && ArgumentPrototype.Current != null && ArgumentHost.Running, "the prototype and its battle", 20f);
        }

        /// <summary>Waits until the battle takes input on the premise list (the intro, the opening pressure phase and its
        /// transitions are over).</summary>
        static IEnumerator UntilBrowsing(ArgumentHost host, float timeout = 30f)
        {
            yield return WaitFor(() => host.Screen == ArgueScreen.Browse && !host.View.Busy && !host.Engine.InPressure, "the premise list", timeout);
            yield return null;
        }

        IEnumerator Tap(GamepadButton button)
        {
            _input.SetPad(button);
            yield return null;
            yield return null;
            _input.SetPad();
            yield return null;
            yield return null;
        }

        IEnumerator Key(Key key)
        {
            yield return _input.Press(key);
            yield return _input.Press();
        }

        /// <summary>Moves the focus to premise <paramref name="id"/> with the d-pad alone.</summary>
        IEnumerator PadTo(ArgumentHost host, string id)
        {
            var def = host.Engine.Def;
            int target = def.premises.FindIndex(p => p.id == id);
            Assert.That(target, Is.GreaterThanOrEqualTo(0), id);
            int tLine = 1 + target / 2, tCol = target % 2;
            for (int guard = 0; guard < 30 && host.FocusedId != id; guard++)
            {
                string f = host.FocusedId;
                int i = def.premises.FindIndex(p => p.id == f);
                int line = f == ArgumentEngine.ClaimId ? 0 : 1 + i / 2, col = f == ArgumentEngine.ClaimId ? 0 : i % 2;
                if (line != tLine) yield return Tap(line < tLine ? GamepadButton.DpadDown : GamepadButton.DpadUp);
                else yield return Tap(col < tCol ? GamepadButton.DpadRight : GamepadButton.DpadLeft);
            }
            Assert.That(host.FocusedId, Is.EqualTo(id), "the d-pad reaches " + id);
        }

        /// <summary>Challenges the focused premise with <paramref name="evidence"/> and the sound reason, by pad: A opens
        /// its verbs, A on Challenge, the d-pad to the evidence, A, the d-pad to the sound card, A.</summary>
        IEnumerator PadChallenge(ArgumentHost host, string evidence)
        {
            yield return Tap(GamepadButton.South);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.Verbs), "A opens the premise's verbs");
            Assert.That(host.FocusedId, Is.EqualTo("challenge"), "focus lands on Challenge");
            yield return Tap(GamepadButton.South);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.PickEvidence));
            int at = host.TrayIds.ToList().IndexOf(evidence);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), evidence + " is in the tray");
            for (int i = 0; i < at % 3; i++) yield return Tap(GamepadButton.DpadRight);
            for (int i = 0; i < at / 3; i++) yield return Tap(GamepadButton.DpadDown);
            Assert.That(host.FocusedId, Is.EqualTo(evidence));
            yield return Tap(GamepadButton.South);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.PickReason));
            int sound = host.Cards.ToList().FindIndex(c => c.reason.kind == ReasonKind.Sound);
            for (int i = 0; i < sound; i++) yield return Tap(GamepadButton.DpadDown);
            Assert.That(host.FocusedId, Is.EqualTo("reason" + sound));
            yield return Tap(GamepadButton.South);
        }

        [UnityTest]
        public IEnumerator TheWholeBattleOnAGamepadAlone()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            var e = host.Engine;
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.Intro).Or.EqualTo(ArgueScreen.BoxIn), "it fades in from the world, not a cut");
            yield return WaitFor(() => host.Screen == ArgueScreen.Pressure, "the opening pressure phase in its box", 10f);
            Assert.That(host.View.BoxShown, Is.True, "the box is open");
            yield return UntilBrowsing(host);
            Assert.That(host.View.BoxShown, Is.False, "the box has collapsed and the claims are back");

            // B on the premise list goes nowhere; the focus is on a card.
            yield return Tap(GamepadButton.East);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.Browse));

            // Y concedes the focused premise; A, then B, opens and closes its verbs.
            yield return PadTo(host, "L1");
            yield return Tap(GamepadButton.South);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.Verbs));
            yield return Tap(GamepadButton.East);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.Browse), "B backs out");
            Assert.That(host.FocusedId, Is.EqualTo("L1"), "focus comes back where it was");
            yield return Tap(GamepadButton.North);
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Conceded));

            // L1': the fourth chair and the sound reason break it; she falls back to L1''.
            yield return UntilBrowsing(host);
            yield return PadTo(host, "L1b");
            yield return PadChallenge(host, DreamLenaDemo.FourthChair);
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Broken));
            Assert.That(host.Audio.LastSound, Is.EqualTo("sfx_break"), "the premise breaking has its sound");

            // Concede L1'' (her painful L4 surfaces: the high pressure phase, then back).
            yield return UntilBrowsing(host);
            yield return PadTo(host, "L1c");
            yield return Tap(GamepadButton.North);
            yield return WaitFor(() => host.Screen == ArgueScreen.Pressure, "the painful phase", 5f);
            yield return UntilBrowsing(host, 20f);
            Assert.That(e.Status("L4"), Is.EqualTo(PremiseStatus.Standing));

            // X asks L3 (L3' appears); L3' with Saturday off; L3'' with Walt's truth.
            yield return PadTo(host, "L3");
            yield return Tap(GamepadButton.West);
            Assert.That(e.Status("L3b"), Is.EqualTo(PremiseStatus.Standing));
            yield return UntilBrowsing(host);
            yield return PadTo(host, "L3b");
            yield return PadChallenge(host, DreamLenaDemo.SaturdayOff);
            Assert.That(e.Status("L3b"), Is.EqualTo(PremiseStatus.Broken));
            yield return UntilBrowsing(host, 20f);
            yield return PadTo(host, "L3c");
            yield return PadChallenge(host, DreamLenaDemo.TruthWalt);

            // Every key premise broken: Convinced; A on the end card's Continue.
            yield return WaitFor(() => host.Screen == ArgueScreen.End && !host.View.Busy, "the end card", 10f);
            Assert.That(e.Outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(host.FocusedId, Is.EqualTo("continue"), "focus on Continue");
            yield return Tap(GamepadButton.South);
            Assert.That(ArgumentHost.Running, Is.False);
            Assert.That(ArgumentHost.LoadLedger().StanceOf(DreamLenaDemo.Id), Is.EqualTo(ArgumentStance.Unsure));

            // The menu, same model: the d-pad moves its focus, no wrap.
            var menu = ArgumentPrototype.Current;
            yield return WaitFor(() => menu.MenuShown, "the menu again", 3f);
            string first = menu.FocusedId;
            yield return Tap(GamepadButton.DpadUp);
            Assert.That(menu.FocusedId, Is.EqualTo(first), "no wrap-around at the top");
            yield return Tap(GamepadButton.DpadDown);
            Assert.That(menu.FocusedId, Is.Not.EqualTo(first), "the d-pad moves the menu's focus");
        }

        [UnityTest]
        public IEnumerator TheWorldsAmbienceGivesWayAndComesBack()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            ArgumentHost.Current.Close(false);
            yield return null;
            GameRoot.Audio.PlayAmbience("amb_rain", 0.01f);
            Assert.That(GameRoot.Audio.CurrentAmbience, Is.EqualTo("amb_rain"));
            var host = ArgumentHost.Begin(DreamLenaDemo.Build(), ArgumentPrototype.SetupFor(ArgumentPrototype.Story));
            yield return null;
            Assert.That(GameRoot.Audio.CurrentAmbience, Is.EqualTo(""), "the world's ambience is silent during the battle");
            Assert.That(host.Audio.WorldAmbience, Is.EqualTo("amb_rain"), "remembered");
            Assert.That(host.Audio.MusicStarted, Is.True, "the battle's own loop plays");
            new ArgumentPilot().Play(host.Engine);
            yield return WaitFor(() => host.Screen == ArgueScreen.End, "the end card", 15f);
            Assert.That(host.Audio.LastSound, Does.StartWith("sfx_end"), "the end card has its sound");
            host.Continue();
            yield return null;
            Assert.That(GameRoot.Audio.CurrentAmbience, Is.EqualTo("amb_rain"), "the world's ambience is back");
        }

        [UnityTest]
        public IEnumerator TheKeyboardShortcutsAndThePressHold()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            var e = host.Engine;
            yield return UntilBrowsing(host);

            Assert.That(host.Select("L1"), Is.True);
            yield return Key(UnityEngine.InputSystem.Key.Digit3);
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Conceded), "3 concedes");
            host.Select("L3");
            yield return Key(UnityEngine.InputSystem.Key.Digit1);
            Assert.That(e.Status("L3b"), Is.EqualTo(PremiseStatus.Standing), "1 asks");

            // Arrows and Enter through the same screens.
            host.Select("L1b");
            yield return Key(UnityEngine.InputSystem.Key.Digit2);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.PickEvidence), "2 challenges");
            yield return Key(UnityEngine.InputSystem.Key.Backspace);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.Verbs), "Backspace backs out one step");
            yield return Key(UnityEngine.InputSystem.Key.Backspace);
            Assert.That(host.Screen, Is.EqualTo(ArgueScreen.Browse));

            // Press is a hold: a tap does nothing, 0.6 s forces it.
            host.Select("L2");
            yield return Key(UnityEngine.InputSystem.Key.Digit4);
            Assert.That(e.Presses, Is.EqualTo(0), "a tap isn't a press");
            _input.Set(UnityEngine.InputSystem.Key.Digit4);
            float until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until && e.Presses == 0) yield return null;
            _input.Set();
            Assert.That(e.Status("L2"), Is.EqualTo(PremiseStatus.Forced));
            Assert.That(e.Presses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AWonBattleIsSavedAndContinueReturnsToTheMenu()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            Assert.That(GameRoot.Input.GameplayAllowed, Is.False, "world gameplay waits while she argues");
            Assert.That(GameRoot.Input.Bindings.FindRow("argue-challenge").Shown, Is.True, "the argument's Controls rows show");
            Assert.That(new ArgumentPilot().Play(host.Engine), Is.EqualTo(ArgumentOutcome.Convinced));
            yield return WaitFor(() => host.Screen == ArgueScreen.End && host.View.EndShown, "the end card", 15f);
            Assert.That(host.View.EndTitle, Is.EqualTo(ArgumentStrings.Shared["outcome.Convinced"]));
            var saved = ArgumentLedger.Parse(GameRoot.Save.Data.GetEntry(ArgumentLedger.SaveKey));
            Assert.That(saved.StanceOf(DreamLenaDemo.Id), Is.EqualTo(ArgumentStance.Unsure), "kept in the save (the creator's two endings: convinced, Lena is unsure)");
            host.Continue();
            yield return null;
            Assert.That(ArgumentHost.Running, Is.False);
            Assert.That(GameRoot.Input.GameplayAllowed, Is.True);
            Assert.That(ArgumentPrototype.Current.MenuShown, Is.True);
        }

        [UnityTest]
        public IEnumerator ContinueOnTheFrameItEndsStillGivesTheRecord()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            ArgumentHost.Current.Close(false);
            ArgumentRecord got = null;
            bool called = false;
            var host = ArgumentHost.Begin(DreamLenaDemo.Build(), ArgumentPrototype.SetupFor(ArgumentPrototype.Story), r => { called = true; got = r; });
            Assert.That(new ArgumentPilot().Play(host.Engine), Is.EqualTo(ArgumentOutcome.Convinced));
            host.Continue(); // the same frame: the host hasn't shown the end card yet
            Assert.That(called, Is.True);
            Assert.That(got, Is.Not.Null, "a finished battle always calls back with its record");
            Assert.That(got.outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(ArgumentLedger.Parse(GameRoot.Save.Data.GetEntry(ArgumentLedger.SaveKey)).StanceOf(DreamLenaDemo.Id), Is.EqualTo(ArgumentStance.Unsure), "and it's saved");
            yield return null;
            Assert.That(ArgumentHost.Running, Is.False);
        }
    }
}
