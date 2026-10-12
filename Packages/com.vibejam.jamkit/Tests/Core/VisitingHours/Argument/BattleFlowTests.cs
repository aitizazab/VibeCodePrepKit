using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Argument;
using NUnit.Framework;

namespace JamKit.Tests.Argument
{
    /// <summary>
    /// The real-time battle (docs/redesign/battle.md): lines typed one at a time (the drawl, balanced tags, the face that
    /// eases back), the debate's rounds, evidence on the weak words, what wrong choices cost (a heartbeat, the card, a
    /// harder phase), hesitating, the showdown, the three rings, composure lost as a real end with a cost, and the
    /// timing target: Lena's battle 8-12 minutes, most of it in play, played through by a human-paced autopilot that
    /// makes a player's mistakes.
    /// </summary>
    public class BattleFlowTests
    {
        const float Dt = 1f / 60f;

        [OneTimeSetUp]
        public void Files() => ArgumentTestFiles.Use();

        static ArgumentSetup Setup(ArgumentDifficulty d = ArgumentDifficulty.Normal, ulong seed = 11) =>
            new ArgumentSetup { difficulty = d, allEvidence = true, seed = seed };

        static BattleFlow Lena(ArgumentSetup setup = null) => new BattleFlow(new ArgumentEngine(DreamLenaDemo.Build(), setup ?? Setup()), ArgumentPresenter.Debate);

        /// <summary>Plays the flow with the pilot until it's over (or a time limit), returning seconds played.</summary>
        static float Play(BattleFlow f, BattlePilot pilot, float limit = 1500f, System.Action<BattleFlow> each = null)
        {
            float t = 0f;
            while (!f.Over && t < limit)
            {
                var input = pilot.Steer(f, Dt);
                f.Tick(Dt, input);
                each?.Invoke(f);
                t += Dt;
            }
            return t;
        }

        /// <summary>Talks until the debate is up (the opening lines, the opening phase sat out by a dodger).</summary>
        static void ToDebate(BattleFlow f, float limit = 60f)
        {
            var pilot = new BattlePilot();
            float t = 0f;
            while (f.State != BattleState.Debate && t < limit)
            {
                var input = f.State == BattleState.Pressure ? pilot.Steer(f, Dt) : new BattleInput { fire = true, pick = -1 };
                f.Tick(Dt, input);
                t += Dt;
            }
            Assert.That(f.State, Is.EqualTo(BattleState.Debate), "the debate starts");
        }

        // ---------- Delivery ----------

        [Test]
        public void ALineIsTypedOneLetterAtATimeAndTheDrawlStretches()
        {
            var t = new Typewriter();
            t.Set("Then tell me I'm doing ju~st dandy.");
            Assert.That(t.Full, Is.EqualTo("Then tell me I'm doing juuust dandy."), "the drawled letter is spelled out");
            Assert.That(t.Typed, Is.Empty);
            int blips = 0, slow = 0;
            float time = 0f;
            while (!t.Done && time < 10f)
            {
                blips += t.Tick(Dt, 1f, out int s);
                slow += s;
                time += Dt;
                if (t.Shown > 0 && t.Shown < t.Length) Assert.That(t.Visible, Does.Contain(Typewriter.HiddenOpen), "the rest is there, unseen, so nothing jumps");
            }
            Assert.That(t.Done, Is.True);
            Assert.That(slow, Is.EqualTo(Typewriter.DrawlRepeat), "each stretched letter blips slowly");
            Assert.That(blips, Is.GreaterThan(5));
            Assert.That(time, Is.GreaterThan(t.Length * Typewriter.LetterSeconds + Typewriter.DrawlSeconds * 1.5f), "the drawl takes time");
            Assert.That(t.Visible, Is.EqualTo(t.Full), "all shown at the end");
        }

        [Test]
        public void TagsStayBalancedAsTheLineAppears()
        {
            var t = new Typewriter();
            t.Set("He's hiding from <b>him</b>. Don't you *dare*.");
            for (int i = 0; i < 40; i++) t.Tick(0.04f, 1f, out _);
            t.Set("He's hiding from <b>him</b>.");
            while (t.Shown < 18) t.Tick(Dt, 1f, out _); // into the bold word
            string v = t.Visible;
            Assert.That(v.Count(c => c == '<'), Is.EqualTo(v.Count(c => c == '>')));
            int open = Occurrences(v, "<b>"), close = Occurrences(v, "</b>");
            Assert.That(open, Is.EqualTo(close), "bold is closed before the hidden part and reopened in it: " + v);
            Assert.That(v.IndexOf(Typewriter.HiddenOpen), Is.GreaterThan(v.IndexOf("</b>")), "closed first");
            Assert.That(Typewriter.StripTags(v.Replace(Typewriter.HiddenOpen, "").Replace(Typewriter.HiddenClose, "")), Is.EqualTo("He's hiding from him."));
        }

        static int Occurrences(string s, string what)
        {
            int n = 0;
            for (int i = s.IndexOf(what); i >= 0; i = s.IndexOf(what, i + 1)) n++;
            return n;
        }

        [Test]
        public void LinesComeOneAtATimeAndHerFaceEasesBack()
        {
            var d = new Delivery();
            var face = new Portrait("antagonistic");
            d.Add(new Delivery.Line { speaker = Speaker.Nadia, who = "Nadia", text = "Why do you say that?", blocking = true });
            d.Add(new Delivery.Line { speaker = Speaker.Opponent, who = "Lena", text = "Don't. Don't you *dare* tell me I'm wrong.", expression = "angry", blocking = true });
            var seen = new List<string>();
            float t = 0f, angryFor = 0f;
            bool wasAngry = false;
            while (t < 20f && (!d.Idle || face.Shown != "neutral"))
            {
                face.SetIdle("neutral");
                var tock = d.Tick(Dt, 1f, false);
                if (tock.started)
                {
                    seen.Add(d.Current.who);
                    if (d.Current.speaker == Speaker.Opponent) face.Speak(d.Current.expression);
                }
                if (face.Speaking && (!d.HasLine || d.LineDone)) face.Quiet();
                face.Tick(Dt);
                if (face.Shown == "angry") { angryFor += Dt; wasAngry = true; }
                t += Dt;
            }
            Assert.That(seen, Is.EqualTo(new[] { "Nadia", "Lena" }), "one line, then the next");
            Assert.That(wasAngry, Is.True, "her face reacts to her line");
            Assert.That(face.Shown, Is.EqualTo("neutral"), "and eases back to listening");
            Assert.That(angryFor, Is.LessThan(6f), "no extreme face held while Nadia thinks");
        }

        [Test]
        public void ALongLineIsSaidInPagesAndEveryLineOfTheBattleFitsOne()
        {
            string longLine = string.Join(" ", Enumerable.Repeat("This is a sentence that goes on a while, as she does.", 9));
            Assert.That(Delivery.SplitPage(longLine, out var a, out var b), Is.True);
            Assert.That(a.EndsWith("."), Is.True);
            var d = new Delivery();
            d.Add(new Delivery.Line { speaker = Speaker.Opponent, who = "Lena", text = longLine, blocking = true });
            Assert.That(d.Queued, Is.GreaterThanOrEqualTo(2), "split into pages");
            // Every line the battles say fits a page as written.
            foreach (var def in new[] { DreamLenaDemo.Build(), WaltNight1.Build() })
                foreach (var kv in ArgumentText.Capture(def))
                    Assert.That(Typewriter.StripTags(kv.Value ?? "").Length, Is.LessThanOrEqualTo(Delivery.MaxPage), def.id + " " + kv.Key + " fits a page");
        }

        // ---------- The debate ----------

        [Test]
        public void TheDebateFliesWhatStandsThenTheClaimRoundAfterRound()
        {
            var f = Lena(Setup(ArgumentDifficulty.Story));
            ToDebate(f);
            var said = new List<string>();
            float t = 0f;
            int round = f.Debate.Round;
            while (f.Debate.Round < round + 2 && t < 120f)
            {
                f.Tick(Dt, BattleInput.None);
                foreach (var c in f.Cues) if (c.kind == CueKind.StatementSpawn) said.Add(c.id);
                t += Dt;
            }
            Assert.That(f.Debate.Round, Is.GreaterThanOrEqualTo(round + 2), "rounds follow each other");
            Assert.That(said, Does.Contain("L1").And.Contain("L2").And.Contain("L3"), "what stands is said");
            Assert.That(said, Does.Not.Contain("L1b"), "what's hidden isn't");
            Assert.That(said.Count(x => x == ArgumentEngine.ClaimId), Is.GreaterThanOrEqualTo(1), "every round ends on the claim");
            int claim = said.IndexOf(ArgumentEngine.ClaimId);
            Assert.That(said.Take(claim), Does.Contain("L3"));
        }

        [Test]
        public void EvidenceOnTheWeakWordsOpensTheBecauseAndOffThemItGlancesOff()
        {
            var f = Lena(Setup(ArgumentDifficulty.Story));
            ToDebate(f);
            // L1 conceded brings out L1' ("nothing left for him"): hold the fourth chair against it.
            Assert.That(f.Engine.Concede("L1"), Is.True);
            f.Debate.Refresh();
            Assert.That(WaitFor(f, () => Ready(f, "L1b"), 120f), Is.True, "L1' comes round");
            while (f.CardInHand != DreamLenaDemo.FourthChair) f.Tick(Dt, new BattleInput { cycle = 1, pick = -1 });
            int i = f.Debate.Find("L1b", false);
            var s = f.Debate[i];
            // Off the weak words: on the statement's first letters ("So there's").
            var off = new Vector2(s.Left + 0.08f, s.position.Y);
            if (s.weak0 > 0.3f)
            {
                f.Debate.PlaceLight(off);
                f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
                Assert.That(f.State, Is.EqualTo(BattleState.Debate), "a glance doesn't open anything");
                Assert.That(f.Cues.Any(c => c.kind == CueKind.Glance), Is.True);
                Assert.That(f.Caption, Is.EqualTo(ArgumentStrings.Shared["screen.why.glance"]));
                for (int k = 0; k < 30; k++) f.Tick(Dt, BattleInput.None);
            }
            i = f.Debate.Find("L1b", false);
            f.Debate.PlaceLight(Debate.WeakCentre(f.Debate[i]));
            f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            Assert.That(f.State, Is.EqualTo(BattleState.Because), "evidence on the glowing words: Because...");
            Assert.That(f.Because.Count, Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>The premise's statement is in flight, faded in, with room left before it goes.</summary>
        static bool Ready(BattleFlow f, string premise)
        {
            if (f.State != BattleState.Debate) return false;
            int i = f.Debate.Find(premise, false);
            return i >= 0 && f.Debate[i].age > 0.4f && f.Debate[i].life - f.Debate[i].age > 2f && System.Math.Abs(Debate.WeakCentre(f.Debate[i]).X) < 2.4f;
        }

        static bool WaitFor(BattleFlow f, System.Func<bool> until, float limit)
        {
            var pilot = new BattlePilot();
            for (float t = 0f; t < limit; t += Dt)
            {
                if (until()) return true;
                var input = f.State == BattleState.Debate ? BattleInput.None : pilot.Steer(f, Dt);
                f.Tick(Dt, input);
            }
            return until();
        }

        [Test]
        public void AWrongCardCostsAHeartbeatAndTheCardUntilTheRoundAfterNext()
        {
            var f = Lena();
            ToDebate(f);
            Assert.That(WaitFor(f, () => Ready(f, "L1"), 120f), Is.True);
            // The police report against "Out there, Theo is dead": true, so it can't be challenged.
            while (f.CardInHand != DreamLenaDemo.PoliceReport) f.Tick(Dt, new BattleInput { cycle = 1, pick = -1 });
            int heart = f.Engine.HeartStep;
            int i = f.Debate.Find("L1", false);
            f.Debate.PlaceLight(Debate.WeakCentre(f.Debate[i]));
            f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            Assert.That(f.Engine.HeartStep, Is.EqualTo(heart + 1), "a heartbeat");
            Assert.That(f.BurnedFor(DreamLenaDemo.PoliceReport), Is.EqualTo(2), "out of her hand");
            Assert.That(f.State, Is.EqualTo(BattleState.Talk), "and she answers");
            Assert.That(f.Engine.Transcript.Any(x => x.speaker == Speaker.Logic && x.text.Contains("out of Nadia's hand")), Is.True, "the transcript says what it cost");
        }

        [Test]
        public void AWrongReasonCostsAHeartbeatTheCardAndAHarderPhase()
        {
            var f = Lena();
            ToDebate(f);
            f.Engine.Concede("L1");
            f.Debate.Refresh();
            Assert.That(WaitFor(f, () => Ready(f, "L1b"), 120f), Is.True);
            while (f.CardInHand != DreamLenaDemo.FourthChair) f.Tick(Dt, new BattleInput { cycle = 1, pick = -1 });
            int heat = f.Engine.Heat;
            f.Debate.PlaceLight(Debate.WeakCentre(f.Debate[f.Debate.Find("L1b", false)]));
            f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            Assert.That(f.State, Is.EqualTo(BattleState.Because));
            int heart = f.Engine.HeartStep;
            int wrong = f.Because.ToList().FindIndex(c => c.reason.kind == ReasonKind.NonSequitur);
            f.Tick(Dt, new BattleInput { pick = wrong });
            Assert.That(f.Engine.HeartStep, Is.EqualTo(heart + 1));
            Assert.That(f.BurnedFor(DreamLenaDemo.FourthChair), Is.GreaterThan(0));
            Assert.That(f.Engine.Heat, Is.GreaterThan(heat), "she's heated");
            Assert.That(f.Engine.InPressure, Is.True, "and a phase follows");
            Assert.That(f.Engine.Status("L1b"), Is.EqualTo(PremiseStatus.Standing));
        }

        [Test]
        public void HesitatingOverTheReasonCostsTheSame()
        {
            var f = Lena();
            ToDebate(f);
            f.Engine.Concede("L1");
            f.Debate.Refresh();
            Assert.That(WaitFor(f, () => Ready(f, "L1b"), 120f), Is.True);
            while (f.CardInHand != DreamLenaDemo.FourthChair) f.Tick(Dt, new BattleInput { cycle = 1, pick = -1 });
            f.Debate.PlaceLight(Debate.WeakCentre(f.Debate[f.Debate.Find("L1b", false)]));
            f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            int heart = f.Engine.HeartStep;
            float t = 0f;
            while (f.State == BattleState.Because && t < 30f) { f.Tick(Dt, BattleInput.None); t += Dt; }
            Assert.That(t, Is.InRange(10f, 30f), "she waits long enough to read the cards, but not forever");
            Assert.That(f.Engine.HeartStep, Is.EqualTo(heart + 1));
            Assert.That(f.BurnedFor(DreamLenaDemo.FourthChair), Is.GreaterThan(0));
            Assert.That(f.Engine.Transcript.Any(x => x.text == f.Def.barks[ArgumentBarks.Hesitate]), Is.True, "she says so");
        }

        [Test]
        public void StorySettingHasNoClockOnTheReasons()
        {
            var f = Lena(Setup(ArgumentDifficulty.Story));
            ToDebate(f);
            f.Engine.Concede("L1");
            f.Debate.Refresh();
            Assert.That(WaitFor(f, () => Ready(f, "L1b"), 120f), Is.True);
            while (f.CardInHand != DreamLenaDemo.FourthChair) f.Tick(Dt, new BattleInput { cycle = 1, pick = -1 });
            f.Debate.PlaceLight(Debate.WeakCentre(f.Debate[f.Debate.Find("L1b", false)]));
            f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            for (int k = 0; k < 60 * 30; k++) f.Tick(Dt, BattleInput.None);
            Assert.That(f.State, Is.EqualTo(BattleState.Because));
            Assert.That(f.Patience, Is.EqualTo(1f));
        }

        // ---------- The showdown ----------

        [Test]
        public void PressingIsAShowdownAndItForcesThePremise()
        {
            var f = Lena();
            ToDebate(f);
            Assert.That(WaitFor(f, () => Ready(f, "L2"), 120f), Is.True);
            int i = f.Debate.Find("L2", false);
            f.Debate.PlaceLight(f.Debate[i].position);
            float t = 0f;
            while (f.State == BattleState.Debate && t < 2f) { f.Tick(Dt, new BattleInput { pressHeld = true, pick = -1 }); t += Dt; }
            Assert.That(f.State, Is.EqualTo(BattleState.Showdown), "a 0.6 s hold starts it");
            Assert.That(t, Is.InRange(BattleFlow.PressHold - 0.05f, BattleFlow.PressHold + 0.2f));
            int heart = f.Engine.HeartStep;
            // Cut the first word, let the rest land.
            bool cut = false;
            t = 0f;
            while (f.State == BattleState.Showdown && t < 30f)
            {
                bool fire = !cut && f.Showdown.InWindow;
                if (fire) cut = true;
                f.Tick(Dt, new BattleInput { fire = fire, pick = -1 });
                t += Dt;
            }
            Assert.That(f.Engine.Status("L2"), Is.EqualTo(PremiseStatus.Forced), "forced");
            Assert.That(f.Engine.Presses, Is.EqualTo(1));
            Assert.That(f.Engine.HeartStep, Is.GreaterThan(heart), "every word missed landed");
            Assert.That(f.Engine.Anger, Is.EqualTo(1), "pressing angers her");
        }

        [Test]
        public void TheShowdownsWordsCanBeCutInTime()
        {
            var s = new Showdown(new[] { "Sit down.", "You weren't here." }, 1, false, ArgumentDifficulty.Normal, 1f, 3);
            int cuts = 0, whiffs = 0;
            for (float t = 0f; t < 30f && !s.Finished; t += Dt)
            {
                bool fire = s.InWindow && s.UntilWindow < 0.05f;
                int r = s.Tick(Dt, fire);
                if (r > 0) cuts++;
                if (r < 0) whiffs++;
            }
            Assert.That(s.Finished, Is.True);
            Assert.That(cuts, Is.EqualTo(s.Total), "every word cut in its window");
            Assert.That(whiffs, Is.Zero);
            Assert.That(s.TakeHits(), Is.Zero);

            var early = new Showdown(new[] { "Sit down." }, 0, false, ArgumentDifficulty.Normal, 1f, 3);
            Assert.That(early.Tick(Dt, true), Is.EqualTo(-1), "a swing at nothing");
            Assert.That(early.Staggered, Is.True, "staggers her");
        }

        // ---------- The three rings ----------

        [Test]
        public void TheThreeRingsHurtOnlyAStillLight()
        {
            var spec = PressureSpec.Call(0, ArgumentDifficulty.Normal, 1f);
            spec.interval = 1000f; // the rings alone
            var still = new PressurePhase(spec, 5, null);
            still.PlaceLight(new Vector2(-1f, 0f));
            int stillHits = 0;
            while (!still.Finished) { still.Tick(Dt, Vector2.Zero); stillHits += still.TakeHits(); }
            Assert.That(still.CallsRung, Is.EqualTo(3), "three rings");
            Assert.That(stillHits, Is.GreaterThanOrEqualTo(2), "standing still, the waves hurt");

            var moving = new PressurePhase(spec, 5, null);
            moving.PlaceLight(new Vector2(-1f, 0f));
            int movingHits = 0;
            float a = 0f;
            while (!moving.Finished)
            {
                a += Dt * 3f;
                moving.Tick(Dt, new Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a)) * 0.6f);
                movingHits += moving.TakeHits();
            }
            Assert.That(movingHits, Is.Zero, "moving, they pass");
        }

        [Test]
        public void ThePainfulPremiseBringsTheThreeRings()
        {
            var e = new ArgumentEngine(DreamLenaDemo.Build(), Setup());
            Assert.That(e.SpecFor(3, true).calls, Is.EqualTo(3));
            Assert.That(e.SpecFor(3, true).duration, Is.EqualTo(PressureSpec.PainfulDuration));
            Assert.That(e.SpecFor(1, false).calls, Is.Zero);
        }

        // ---------- Composure ----------

        [Test]
        public void LosingComposureEndsItThereAndLenaCondemns()
        {
            var f = Lena();
            ToDebate(f);
            // A player who never moves and never answers: hit after hit, then the silence heats her up.
            float t = 0f;
            var states = new HashSet<BattleState>();
            while (!f.Over && t < 900f)
            {
                var input = f.State == BattleState.Talk || f.State == BattleState.Ending ? new BattleInput { fire = true, pick = -1 } : BattleInput.None;
                f.Tick(Dt, input);
                states.Add(f.State);
                t += Dt;
            }
            Assert.That(f.Over, Is.True, "it ends");
            Assert.That(f.Engine.Outcome, Is.EqualTo(ArgumentOutcome.ComposureLost));
            Assert.That(states, Does.Contain(BattleState.Panic), "the room closes in first");
            Assert.That(f.Engine.HeartRate, Is.EqualTo(160));
            var r = ArgumentBattles.Result(f.Def, f.Engine.Record());
            Assert.That(r.Condemns, Is.True, "Lena condemns her");
            Assert.That(r.despairAdded, Is.EqualTo(1), "Despair +1");
            Assert.That(f.Engine.Transcript.Any(x => x.text == f.Def.composureLostLine), Is.True, "the creator's line");
            Assert.That(f.Engine.Transcript.Any(x => x.text == f.Def.composureLostReply), Is.True, "and her answer");
            // No retry: the flow stays over.
            for (int k = 0; k < 120; k++) f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            Assert.That(f.Over && f.Engine.Ended, Is.True);
        }

        [Test]
        public void WaltsTutorialCantBeLostAndMagsTeachesInTurn()
        {
            var setup = new ArgumentSetup { seed = 4 };
            setup.evidence.AddRange(new[] { WaltNight1.WaltDead, WaltNight1.SameSaturday, WaltNight1.WaltAlive });
            var f = new BattleFlow(new ArgumentEngine(WaltNight1.Build(), setup), ArgumentPresenter.Debate);
            var pilot = new BattlePilot(true, BattlePilot.Mistake.WrongCard, BattlePilot.Mistake.WrongReason, BattlePilot.Mistake.Press, BattlePilot.Mistake.StandStill);
            int maxHeart = 0;
            float t = Play(f, pilot, 1200f, x => maxHeart = System.Math.Max(maxHeart, x.Engine.HeartStep));
            TestContext.WriteLine("Walt: " + f.Engine.Outcome + " in " + t.ToString("0") + " s (" + f.ActiveSeconds.ToString("0") + " s in play, " + f.TalkSeconds.ToString("0") + " s talk); coached: "
                + string.Join(", ", ArgumentCoach.Moments.Where(m => f.Engine.Coached(m))));
            Assert.That(f.Over, Is.True, "it ends");
            Assert.That(f.Engine.Outcome, Is.Not.EqualTo(ArgumentOutcome.ComposureLost));
            Assert.That(maxHeart, Is.LessThan(ArgumentEngine.PanicStep), "composure stops a step short");
            foreach (var m in new[] { ArgumentCoach.Debate, ArgumentCoach.Ask, ArgumentCoach.Concede, ArgumentCoach.Challenge, ArgumentCoach.Because, ArgumentCoach.Wrong, ArgumentCoach.Showdown })
                Assert.That(f.Engine.Coached(m), Is.True, "Mags taught " + m);
            Assert.That(f.Engine.Transcript.Where(x => x.speaker == Speaker.Supporter).All(x => x.who == "Mags"), Is.True);
        }

        // ---------- What the day's bedside changed (docs/redesign/day.md) ----------

        [Test]
        public void TheBedsideSetsHerStartingAnger()
        {
            var wary = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true, angerStart = 1 });
            Assert.That(wary.Anger, Is.EqualTo(1), "Day 2's \"Lena\": she starts a step angrier");
            Assert.That(wary.SpecFor(1, false).speed, Is.GreaterThan(new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup()).SpecFor(1, false).speed), "and her pressure is sharper");

            var softer = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true, angerStart = -1, difficulty = ArgumentDifficulty.Story });
            softer.Start();
            softer.SkipPressure();
            Assert.That(softer.Press("L2"), Is.True);
            Assert.That(softer.Anger, Is.Zero, "\"just sit\": the first thing that would anger her doesn't");
            softer.SkipPressure();
            softer.Press("L1");
            Assert.That(softer.Anger, Is.EqualTo(1), "the next does");

            var ctx = new ArgumentContext { angerStart = 5, admitted = true };
            var setup = ArgumentBattles.Setup(ctx);
            Assert.That(setup.angerStart, Is.EqualTo(1), "clamped to a step either way");
            Assert.That(setup.admitted, Is.True);
        }

        [Test]
        public void SaidSorryAtTheBedsideTheAdmissionIsOpenFromTheStart()
        {
            var e = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true, admitted = true, difficulty = ArgumentDifficulty.Story });
            e.Start();
            Assert.That(e.Status("L4"), Is.EqualTo(PremiseStatus.Standing), "\"He called you first\" is on the table from the start");
            e.SkipPressure();
            Assert.That(e.Ask("L4"), Is.True);
            Assert.That(e.Transcript.Last(x => x.speaker == Speaker.Opponent).text, Does.Contain("already said it to him"), "she knows (the sorry meter)");
            // Its three rings still come when the night would have brought it out.
            e.Concede("L1");
            e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, Pick(e, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Sound));
            e.SkipPressure();
            e.Concede("L1c");
            Assert.That(e.InPressure, Is.True);
            e.Tick(Dt, Vector2.Zero);
            Assert.That(e.Pressure.Spec.calls, Is.EqualTo(3), "the three rings, once");
            e.SkipPressure();
            Assert.That(e.Concede("L4"), Is.True);
            Assert.That(e.Transcript.Last(x => x.speaker == Speaker.Nadia).text, Does.Contain("I said it to him today"), "her admission reads that way");

            var plain = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true });
            plain.Start();
            Assert.That(plain.Status("L4"), Is.EqualTo(PremiseStatus.Hidden), "without it, it surfaces later");
        }

        static ReasonDef Pick(ArgumentEngine e, string premise, string evidence, ReasonKind kind)
        {
            var cards = new List<ReasonCard>();
            Assert.That(e.BeginChallenge(premise, evidence, cards), Is.EqualTo(ChallengeStep.Reasons));
            return cards.First(c => c.reason.kind == kind).reason;
        }

        // ---------- The whole battle ----------

        /// <summary>The mistakes the recorded run makes (and the timing test), as a first-time player would: three wrong
        /// cards, a glance off the statement, two wrong reasons and a hesitation.</summary>
        public static BattlePilot.Mistake[] FirstTime => BattlePilot.FirstTime;

        [Test]
        public void LenasBattleRunsEightToTwelveMinutesMostlyInPlay()
        {
            var f = Lena(Setup(ArgumentDifficulty.Normal, 21));
            // A first-time player: reads before acting, and slips the way people do.
            var pilot = new BattlePilot(true, FirstTime);
            var log = new List<string>();
            BattleState last = BattleState.Intro;
            float t = Play(f, pilot, 1500f, x =>
            {
                if (x.State != last) { last = x.State; }
                foreach (var c in x.Cues)
                    if (c.kind == CueKind.Ask || c.kind == CueKind.Concede || c.kind == CueKind.Break || c.kind == CueKind.WrongCard || c.kind == CueKind.PressureIn || c.kind == CueKind.Hesitate || c.kind == CueKind.ShowdownStart)
                        log.Add((x.ActiveSeconds + x.TalkSeconds).ToString("0") + "s " + c.kind + " " + c.id);
            });
            float active = f.ActiveSeconds, talk = f.TalkSeconds;
            TestContext.WriteLine("Lena: " + f.Engine.Outcome + " in " + t.ToString("0") + " s: " + active.ToString("0") + " s in play, " + talk.ToString("0") + " s talk ("
                + (100f * active / (active + talk)).ToString("0") + "% in play); heart " + f.Engine.HeartRate + ", rounds " + f.Debate.Round);
            foreach (var l in log) TestContext.WriteLine("  " + l);
            Assert.That(f.Over, Is.True, "it ends");
            Assert.That(f.Engine.Outcome, Is.EqualTo(ArgumentOutcome.Convinced), "the honest line, with a player's mistakes, still convinces her");
            Assert.That(t, Is.InRange(8f * 60f, 12f * 60f), "8-12 minutes");
            Assert.That(active / (active + talk), Is.GreaterThanOrEqualTo(0.6f), "60% or more in play");
        }

        [Test]
        public void TheFastAutopilotFinishesQuickly()
        {
            var f = Lena(Setup(ArgumentDifficulty.Story, 5));
            float t = Play(f, new BattlePilot(), 1500f);
            TestContext.WriteLine("fast: " + f.Engine.Outcome + " in " + t.ToString("0") + " s");
            Assert.That(f.Over, Is.True);
            Assert.That(f.Engine.Outcome, Is.EqualTo(ArgumentOutcome.Convinced));
        }
    }
}
