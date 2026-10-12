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
    /// <summary>The real-time battle in the running shell (docs/redesign/battle.md): it fades in from the world and the
    /// debate runs; a gamepad alone asks (X), concedes (Y), turns its cards (RB), shows one on the weak words (A), picks
    /// the reason (d-pad, A), opens the case file (View), presses (hold B) and cuts the showdown (A); the keyboard does the
    /// same (1, 3, Q / E, hold 4); the world's ambience gives way and comes back; a won battle is saved and A on the end
    /// card returns to the menu; Continue on the frame it ends still gives the record; the fast autopilot finishes.</summary>
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
            // These play the debate (milestone 1); the sections have their own tests below.
            ArgumentHost.UsePresenterForSession(ArgumentPresenter.Debate);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _input?.Dispose();
            _input = null;
            if (ArgumentHost.Current != null) ArgumentHost.Current.Close(false);
            ArgumentHost.UsePresenterForSession(null);
            yield return null;
        }

        static IEnumerator OpenPrototype(string chapter)
        {
            var r = PresenterMode.Jump(ArgumentPrototype.Scene, chapter);
            Assert.That(r, Is.EqualTo(JumpResult.Pending).Or.EqualTo(JumpResult.Jumped), "the prototype's chapter (is the scene in Build Settings?)");
            yield return WaitFor(() => !GameRoot.IsLoadingScene && SceneManager.GetActiveScene().name == ArgumentPrototype.Scene
                && ArgumentPrototype.Current != null && ArgumentHost.Running, "the prototype and its battle", 20f);
        }

        /// <summary>Waits until the debate is up and <paramref name="premise"/>'s statement is flying where the light can
        /// reach it (its weak words, or its middle), then puts the light there.</summary>
        static IEnumerator LightOn(ArgumentHost host, string premise, bool weak, float timeout = 120f)
        {
            yield return WaitFor(() =>
            {
                var f = host.Flow;
                if (f.State != BattleState.Debate) return false;
                int i = f.Debate.Find(premise, false);
                if (i < 0) return false;
                var s = f.Debate[i];
                var at = weak ? Debate.WeakCentre(s) : s.position;
                return s.age > 0.5f && s.life - s.age > 2.5f && Mathf.Abs(at.X) < 2.3f;
            }, premise + "'s statement in reach", timeout);
            var st = host.Flow.Debate[host.Flow.Debate.Find(premise, false)];
            host.Flow.Debate.PlaceLight(weak ? Debate.WeakCentre(st) : st.position);
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

        [UnityTest]
        public IEnumerator TheBattleFadesInAndTheDebateRuns()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            Assert.That(host.Stage.Busy, Is.True, "it fades in from the world, not a cut");
            Assert.That(host.State, Is.EqualTo(BattleState.Intro), "her opening lines first");
            yield return WaitFor(() => host.State == BattleState.Pressure, "the opening phase (laying the table)", 30f);
            yield return WaitFor(() => host.State == BattleState.Debate && host.Stage.StatementsShown > 0, "the debate, a statement flying", 30f);
            Assert.That(host.Stage.LineText, Is.Not.Empty, "her statement is said as it flies");
            Assert.That(GameRoot.Input.GameplayAllowed, Is.False, "world gameplay waits while she argues");
        }

        [UnityTest]
        public IEnumerator TheWholeBattleOnAGamepadAlone()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            var e = host.Engine;

            // X asks about what the light is on.
            yield return LightOn(host, "L3", false);
            yield return Tap(GamepadButton.West);
            Assert.That(e.TimesAsked("L3"), Is.EqualTo(1), "X: Why?");
            Assert.That(e.Status("L3b"), Is.EqualTo(PremiseStatus.Standing), "asking L3 brought out L3'");

            // Y concedes what's true.
            yield return LightOn(host, "L1", false);
            yield return Tap(GamepadButton.North);
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Conceded), "Y: That's true");
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Standing));

            // RB to the fourth chair; A on L1''s weak words; the d-pad to the sound reason; A.
            yield return WaitFor(() => host.State == BattleState.Debate, "the debate", 60f);
            for (int guard = 0; guard < 20 && host.Flow.CardInHand != DreamLenaDemo.FourthChair; guard++) yield return Tap(GamepadButton.RightShoulder);
            Assert.That(host.Flow.CardInHand, Is.EqualTo(DreamLenaDemo.FourthChair), "RB turns the cards");
            yield return LightOn(host, "L1b", true);
            yield return Tap(GamepadButton.South);
            Assert.That(host.State, Is.EqualTo(BattleState.Because), "A shows the card on her weak words: Because...");
            Assert.That(host.Stage.BecauseShown, Is.True);
            int sound = host.Flow.Because.ToList().FindIndex(c => c.reason.kind == ReasonKind.Sound);
            for (int i = 0; i < sound; i++) yield return Tap(GamepadButton.DpadDown);
            Assert.That(host.Flow.BecauseFocus, Is.EqualTo(sound));
            yield return Tap(GamepadButton.South);
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Broken), "the sound reason breaks it");
            Assert.That(host.Flow.Count(CueKind.Break), Is.EqualTo(1));

            // View opens the case file (the battle waits); B closes it.
            yield return WaitFor(() => host.State == BattleState.Debate, "the debate again", 60f);
            yield return Tap(GamepadButton.Select);
            Assert.That(host.CaseFileOpen && host.Stage.CaseFileShown, Is.True, "View: the case file");
            float active = host.Flow.ActiveSeconds;
            yield return Frames(20);
            Assert.That(host.Flow.ActiveSeconds, Is.EqualTo(active), "the battle waits");
            yield return Tap(GamepadButton.East);
            Assert.That(host.CaseFileOpen, Is.False, "B closes it");

            // Hold B on L2: the showdown; A cuts each word as it reaches the mark.
            yield return LightOn(host, "L2", false);
            _input.SetPad(GamepadButton.East);
            yield return WaitFor(() => host.State == BattleState.Showdown, "the showdown (a 0.6 s hold)", 3f);
            _input.SetPad();
            bool held = false;
            yield return WaitFor(() =>
            {
                if (host.State != BattleState.Showdown) return true;
                bool cut = host.Flow.Showdown.InWindow && host.Flow.Showdown.UntilWindow < 0.05f && !held;
                if (cut) { _input.SetPad(GamepadButton.South); held = true; }
                else if (held) { _input.SetPad(); held = false; }
                return false;
            }, "every word cut or landed", 40f);
            _input.SetPad();
            Assert.That(e.Status("L2"), Is.EqualTo(PremiseStatus.Forced), "pressing forced it");
            Assert.That(host.Flow.Count(CueKind.Cut), Is.GreaterThan(0), "A cut her words");
        }

        [UnityTest]
        public IEnumerator TheKeyboardDoesTheSame()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            var e = host.Engine;
            yield return LightOn(host, "L1", false);
            yield return Key(UnityEngine.InputSystem.Key.Digit3);
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Conceded), "3 concedes");
            yield return LightOn(host, "L3", false);
            yield return Key(UnityEngine.InputSystem.Key.Digit1);
            Assert.That(e.TimesAsked("L3"), Is.EqualTo(1), "1 asks");

            // E and Q turn the cards.
            yield return WaitFor(() => host.State == BattleState.Debate, "the debate", 60f);
            string first = host.Flow.CardInHand;
            yield return Key(UnityEngine.InputSystem.Key.E);
            Assert.That(host.Flow.CardInHand, Is.Not.EqualTo(first), "E: the next card");
            yield return Key(UnityEngine.InputSystem.Key.Q);
            Assert.That(host.Flow.CardInHand, Is.EqualTo(first), "Q: back");

            // Press is a hold: a tap does nothing, 0.6 s starts the showdown.
            yield return LightOn(host, "L2", false);
            yield return Key(UnityEngine.InputSystem.Key.Digit4);
            Assert.That(host.State, Is.Not.EqualTo(BattleState.Showdown), "a tap isn't a press");
            yield return LightOn(host, "L2", false);
            _input.Set(UnityEngine.InputSystem.Key.Digit4);
            yield return WaitFor(() => host.State == BattleState.Showdown, "the showdown", 3f);
            _input.Set();
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
            yield return WaitFor(() => host.EndCardShown, "the end card", 45f);
            host.Continue();
            yield return null;
            Assert.That(GameRoot.Audio.CurrentAmbience, Is.EqualTo("amb_rain"), "the world's ambience is back");
        }

        [UnityTest]
        public IEnumerator AWonBattleIsSavedAndContinueReturnsToTheMenu()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            Assert.That(GameRoot.Input.Bindings.FindRow("argue-challenge").Shown, Is.True, "the argument's Controls rows show");
            Assert.That(new ArgumentPilot().Play(host.Engine), Is.EqualTo(ArgumentOutcome.Convinced));
            yield return WaitFor(() => host.EndCardShown && host.Stage.EndShown, "the end card", 45f);
            Assert.That(host.Stage.EndTitle, Is.EqualTo(ArgumentStrings.Shared["outcome.Convinced"]));
            var saved = ArgumentLedger.Parse(GameRoot.Save.Data.GetEntry(ArgumentLedger.SaveKey));
            Assert.That(saved.StanceOf(DreamLenaDemo.Id), Is.EqualTo(ArgumentStance.Unsure), "kept in the save (the creator's two endings: convinced, Lena is unsure)");
            yield return Frames(45);
            yield return Tap(GamepadButton.South);
            Assert.That(ArgumentHost.Running, Is.False, "A on the end card continues");
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
            host.Continue(); // the same frame: the end card isn't up yet
            Assert.That(called, Is.True);
            Assert.That(got, Is.Not.Null, "a finished battle always calls back with its record");
            Assert.That(got.outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(ArgumentLedger.Parse(GameRoot.Save.Data.GetEntry(ArgumentLedger.SaveKey)).StanceOf(DreamLenaDemo.Id), Is.EqualTo(ArgumentStance.Unsure), "and it's saved");
            yield return null;
            Assert.That(ArgumentHost.Running, Is.False);
        }

        // ---------- The sections presenter (docs/redesign/battle.md, round 2) ----------

        /// <summary>Waits for the argue section and steps the d-pad to <paramref name="premise"/>.</summary>
        IEnumerator Pick(ArgumentHost host, string premise, float timeout = 90f)
        {
            yield return WaitFor(() => host.State == BattleState.Argue && host.Flow.Map.IndexOf(premise) >= 0 && host.Flow.Map[host.Flow.Map.IndexOf(premise)].visible, premise + " on her case", timeout);
            for (int guard = 0; guard < 16 && host.Flow.SelectedId != premise; guard++)
            {
                var to = host.Flow.Map[host.Flow.Map.IndexOf(premise)].position - host.Flow.Map[host.Flow.Selected].position;
                var button = System.Math.Abs(to.X) > System.Math.Abs(to.Y) ? (to.X > 0f ? GamepadButton.DpadRight : GamepadButton.DpadLeft) : (to.Y > 0f ? GamepadButton.DpadUp : GamepadButton.DpadDown);
                yield return Tap(button);
                yield return Frames(4);
            }
            Assert.That(host.Flow.SelectedId, Is.EqualTo(premise), "the d-pad picks " + premise);
        }

        [UnityTest]
        public IEnumerator TheSectionsOnAGamepadAlone()
        {
            ArgumentHost.UsePresenterForSession(ArgumentPresenter.Sections);
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            var e = host.Engine;
            Assert.That(host.Flow.Presenter, Is.EqualTo(ArgumentPresenter.Sections));
            yield return WaitFor(() => host.State == BattleState.Argue, "the argue section", 40f);
            Assert.That(host.Engine.Pressure, Is.Null, "nothing comes at the light while Nadia argues");

            // Y concedes what's picked; her reply comes as a dodge.
            yield return Pick(host, "L1");
            yield return Tap(GamepadButton.North);
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Conceded), "Y: That's true");
            yield return WaitFor(() => host.State == BattleState.Pressure, "her reply, as a dodge", 30f);

            // X asks; her words are caught as she says them (Story catches them by itself).
            yield return Pick(host, "L3");
            yield return Tap(GamepadButton.West);
            Assert.That(e.TimesAsked("L3"), Is.EqualTo(1), "X: Why?");
            yield return WaitFor(() => e.Caught(DreamLenaDemo.QuoteHad), "her words, caught", 20f);

            // RB to the fourth chair; A on L1'; the d-pad to the sound reason; A.
            yield return Pick(host, "L1b");
            for (int guard = 0; guard < 20 && host.Flow.CardInHand != DreamLenaDemo.FourthChair; guard++) yield return Tap(GamepadButton.RightShoulder);
            Assert.That(host.Flow.CardInHand, Is.EqualTo(DreamLenaDemo.FourthChair), "RB turns the cards");
            yield return Tap(GamepadButton.South);
            Assert.That(host.State, Is.EqualTo(BattleState.Because), "A shows the card: Because...");
            int sound = host.Flow.Because.ToList().FindIndex(c => c.reason.kind == ReasonKind.Sound);
            for (int i = 0; i < sound; i++) yield return Tap(GamepadButton.DpadDown);
            yield return Tap(GamepadButton.South);
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Broken), "the sound reason breaks it");

            // View opens the case file in the argue section too.
            yield return WaitFor(() => host.State == BattleState.Argue, "the argue section again", 60f);
            yield return Tap(GamepadButton.Select);
            Assert.That(host.CaseFileOpen, Is.True, "View: the case file");
            yield return Tap(GamepadButton.East);
            Assert.That(host.CaseFileOpen, Is.False);

            // Hold B on L2: the showdown.
            yield return Pick(host, "L2");
            _input.SetPad(GamepadButton.East);
            yield return WaitFor(() => host.State == BattleState.Showdown, "the showdown (a 0.6 s hold)", 3f);
            _input.SetPad();
        }

        [UnityTest]
        public IEnumerator TheSectionsFastAutopilotReachesHerSkyAndTheEnd()
        {
            ArgumentHost.UsePresenterForSession(ArgumentPresenter.Sections);
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            host.Autopilot = true;
            bool sky = false;
            yield return WaitFor(() => { sky |= host.Flow.Sky; return host.EndCardShown; }, "the fast autopilot to the end card", 120f);
            Assert.That(sky, Is.True, "the ceiling lifted on the way");
            Assert.That(host.Engine.Outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            host.Continue();
        }

        [UnityTest]
        public IEnumerator HeardOutSetsTheKeepsakeFlag()
        {
            ArgumentHost.UsePresenterForSession(ArgumentPresenter.Sections);
            yield return OpenPrototype(ArgumentPrototype.Story);
            ArgumentHost.Current.Close(false);
            GameRoot.Save.Data.ClearFlag(ArgumentBattles.KeepsakeFlag);
            var host = ArgumentHost.Begin(DreamLenaDemo.Build(), ArgumentPrototype.SetupFor(ArgumentPrototype.Story));
            host.Engine.SkipPressure();
            host.Engine.ForceOutcome(ArgumentOutcome.HeardOut);
            host.Continue();
            Assert.That(GameRoot.Save.Data.HasFlag(ArgumentBattles.KeepsakeFlag), Is.True, "Heard out: Lena's watch is Nadia's, in the save");
            GameRoot.Save.Data.ClearFlag(ArgumentBattles.KeepsakeFlag);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFastAutopilotPlaysItToTheEnd()
        {
            yield return OpenPrototype(ArgumentPrototype.Story);
            var host = ArgumentHost.Current;
            host.Autopilot = true;
            yield return WaitFor(() => host.EndCardShown, "the fast autopilot to the end card", 120f);
            Assert.That(host.Engine.Outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(host.Flow.Count(CueKind.Break), Is.GreaterThanOrEqualTo(3), "it broke the three key premises in play");
            host.Continue();
        }
    }
}
