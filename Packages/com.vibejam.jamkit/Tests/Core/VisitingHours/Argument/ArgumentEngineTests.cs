using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Argument;
using NUnit.Framework;

namespace JamKit.Tests.Argument
{
    /// <summary>The prototype argument battle's rules (docs/ARGUMENT-PROTOTYPE.md), on the Dream-Lena demo:
    /// premise breaking, the reason cards, the opponent's replies, Ask / Concede / Press / Let it rest, slips,
    /// scripted reveals, the antagonising reply and its harder pressure, supporters, outcomes and stances.</summary>
    public class ArgumentEngineTests
    {
        const float Dt = 1f / 60f;

        [OneTimeSetUp]
        public void Files() => ArgumentTestFiles.Use();

        static ArgumentEngine Engine(ArgumentDifficulty difficulty = ArgumentDifficulty.Story, string[] evidence = null, ulong seed = 7)
        {
            var setup = new ArgumentSetup { difficulty = difficulty, seed = seed };
            if (evidence == null) setup.allEvidence = true;
            else setup.evidence.AddRange(evidence);
            var e = new ArgumentEngine(DreamLenaDemo.Build(), setup);
            e.Start();
            return e;
        }

        /// <summary>Plays out any pressure phase standing still (Story mode can't hurt).</summary>
        static void SitOutPressure(ArgumentEngine e)
        {
            for (int i = 0; i < 2000 && e.InPressure && !e.Ended; i++) e.Tick(Dt, Vector2.Zero);
        }

        static ReasonDef Card(ArgumentEngine e, string premise, string evidence, ReasonKind kind)
        {
            var cards = new List<ReasonCard>();
            Assert.That(e.BeginChallenge(premise, evidence, cards), Is.EqualTo(ChallengeStep.Reasons), premise + " with " + evidence);
            var c = cards.FirstOrDefault(x => x.reason.kind == kind);
            Assert.That(c.reason, Is.Not.Null, kind + " card");
            return c.reason;
        }

        static string LastOpponentLine(ArgumentEngine e) => e.Transcript.Last(x => x.speaker == Speaker.Opponent).text;

        [Test]
        public void TheSoundPathWithWaltsTruthIsConvinced()
        {
            var e = Engine();
            var outcome = new ArgumentPilot().Play(e);
            Assert.That(outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(e.KeysBroken, Is.EqualTo(3), "L1', L3' and L3''");
            Assert.That(e.Presses, Is.EqualTo(0));
            Assert.That(e.Stance, Is.EqualTo(ArgumentStance.Unsure), "the creator's two endings: convinced, she's unsure (open to her blessing later)");
            var rec = e.Record();
            Assert.That(rec.truth, Is.EqualTo(DreamLenaDemo.TruthLena));
            Assert.That(rec.outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(e.Transcript.Any(x => x.speaker == Speaker.Logic && x.text.StartsWith("You said")), Is.True, "steps written as sentences");
        }

        [Test]
        public void TheOpeningIsAPressurePhaseAndAskingIsFree()
        {
            var e = Engine();
            Assert.That(e.InPressure, Is.True, "the opening: their feeling as they make the claim");
            e.Tick(Dt, Vector2.Zero);
            Assert.That(e.PhasesRun, Is.EqualTo(1));
            e.SkipPressure(); // (not sitting it out: its slip would reveal L5 first)
            Assert.That(e.Status("L5"), Is.EqualTo(PremiseStatus.Hidden));
            Assert.That(e.Ask("L3"), Is.True);
            Assert.That(e.Status("L3b"), Is.EqualTo(PremiseStatus.Standing), "asking L3 reveals L3'");
            Assert.That(e.InPressure, Is.False, "pressure only as a consequence");
            Assert.That(e.Heat, Is.EqualTo(0));
            Assert.That(e.Ask("L3"), Is.True);
            Assert.That(e.Transcript.Last(x => x.speaker == Speaker.Opponent).text, Is.EqualTo(e.Def.Premise("L3").askAgainReply), "asked again, she points to Theo's room");
            Assert.That(e.Status("L5"), Is.EqualTo(PremiseStatus.Hidden), "the drawing (or the paper plane) brings out the deepest, not asking (2026-10-12)");
        }

        [Test]
        public void ConcedingATruePremiseRevealsItsLinkAndCalms()
        {
            var e = Engine(ArgumentDifficulty.ScriptedButBeatable);
            Assert.That(e.HeartRate, Is.EqualTo(124), "the Long Night starts high");
            e.SkipPressure();
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Hidden));
            Assert.That(e.Concede("L1"), Is.True);
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Conceded));
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Standing), "what she hangs on it");
            Assert.That(e.HeartRate, Is.EqualTo(106), "one step down");
            Assert.That(LastOpponentLine(e), Does.Contain("nothing left"));
        }

        [Test]
        public void EachReasonGetsItsOwnReplyAndOnlyTheSoundOneBreaks()
        {
            var e = Engine();
            SitOutPressure(e);
            e.Concede("L1");
            var cards = new List<ReasonCard>();
            Assert.That(e.BeginChallenge("L1b", DreamLenaDemo.FourthChair, cards), Is.EqualTo(ChallengeStep.Reasons));
            Assert.That(cards.Select(c => c.reason.kind), Is.EquivalentTo(new[] { ReasonKind.Sound, ReasonKind.Beside, ReasonKind.NonSequitur, ReasonKind.Antagonise }));
            var again = new List<ReasonCard>();
            e.BeginChallenge("L1b", DreamLenaDemo.FourthChair, again);
            Assert.That(again.Select(c => c.reason.text), Is.EqualTo(cards.Select(c => c.reason.text)), "the same order on a retry");

            var beside = Card(e, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Beside);
            Assert.That(e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, beside), Is.True);
            Assert.That(LastOpponentLine(e), Is.EqualTo(beside.reply));
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Standing));
            Assert.That(e.Heat, Is.EqualTo(1));
            Assert.That(e.InPressure, Is.True, "a wrong reason brings a pressure phase");
            SitOutPressure(e);

            var leap = Card(e, "L1b", DreamLenaDemo.FourthChair, ReasonKind.NonSequitur);
            e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, leap);
            Assert.That(LastOpponentLine(e), Is.EqualTo(leap.reply));
            Assert.That(e.Heat, Is.EqualTo(2));
            Assert.That(e.Transcript.Last(x => x.speaker == Speaker.Logic).text, Does.Contain("doesn't follow"));
            SitOutPressure(e);

            var sound = Card(e, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Sound);
            e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, sound);
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.Broken));
            Assert.That(e.Status("L1c"), Is.EqualTo(PremiseStatus.Standing), "she falls back to a weaker premise");
            Assert.That(e.Heat, Is.EqualTo(1), "arguing well calms the room");
            Assert.That(e.InPressure, Is.False);
        }

        [Test]
        public void TrueIrrelevantAndUnbreakablePremisesAnswerAtOnce()
        {
            var e = Engine();
            SitOutPressure(e);
            Assert.That(e.BeginChallenge("L1", DreamLenaDemo.HeightMarks, new List<ReasonCard>()), Is.EqualTo(ChallengeStep.TrueOne));
            Assert.That(e.Heat, Is.EqualTo(1), "challenging a true premise costs a little");
            e.Concede("L1");
            Assert.That(e.BeginChallenge("L1b", DreamLenaDemo.Photo, new List<ReasonCard>()), Is.EqualTo(ChallengeStep.NoTouch));
            Assert.That(LastOpponentLine(e), Is.EqualTo(e.Line("noTouchReply", e.Def.noTouchReply)));
            e.Concede("L2");
            Assert.That(e.BeginChallenge("L2b", DreamLenaDemo.FourthChair, new List<ReasonCard>()), Is.EqualTo(ChallengeStep.Unbreakable));
            Assert.That(e.Status("L2b"), Is.EqualTo(PremiseStatus.Standing), "can't be broken tonight");
        }

        [Test]
        public void WithoutWaltsTruthTheWishCantBreak()
        {
            var e = Engine(ArgumentDifficulty.Story, DreamLenaDemo.EvidenceWithoutWalt);
            Assert.That(e.HasEvidence(DreamLenaDemo.TruthWalt), Is.False);
            var outcome = new ArgumentPilot().Play(e);
            Assert.That(e.Status("L3c"), Is.EqualTo(PremiseStatus.Standing), "L3'' needs Night 1's truth");
            Assert.That(outcome, Is.EqualTo(ArgumentOutcome.HeardOut), "Heard out at best");
            Assert.That(e.Record().personal, Is.EqualTo(DreamLenaDemo.Planisphere), "her keepsake (a placeholder)");
        }

        [Test]
        public void ATruthWonInAnEarlierBattleIsEvidence()
        {
            var ledger = new ArgumentLedger();
            ledger.Set(new ArgumentRecord { id = "walt_night1", outcome = ArgumentOutcome.Convinced, stance = ArgumentStance.Amenable, truth = DreamLenaDemo.TruthWalt });
            var setup = new ArgumentSetup { difficulty = ArgumentDifficulty.Story, ledger = ledger };
            setup.evidence.AddRange(DreamLenaDemo.EvidenceWithoutWalt);
            var e = new ArgumentEngine(DreamLenaDemo.Build(), setup);
            e.Start();
            Assert.That(e.HasEvidence(DreamLenaDemo.TruthWalt), Is.True);
            Assert.That(e.Tray().Any(x => x.id == DreamLenaDemo.TruthWalt), Is.True);
            Assert.That(new ArgumentPilot().Play(e), Is.EqualTo(ArgumentOutcome.Convinced));
        }

        [Test]
        public void ASlipRevealsWhatTheyArentSaying()
        {
            // Story mode: slips collect themselves after 3 s; the opening phase carries the paper plane.
            var e = Engine();
            Assert.That(e.Status("L5"), Is.EqualTo(PremiseStatus.Hidden));
            SitOutPressure(e);
            Assert.That(e.Status("L5"), Is.EqualTo(PremiseStatus.Standing));
            Assert.That(e.Transcript.Any(x => x.speaker == Speaker.Logic && x.text.Contains("paper plane")), Is.True);
        }

        [Test]
        public void APainfulPremiseSurfacesByScriptWithTheHighPhase()
        {
            var e = Engine();
            SitOutPressure(e);
            e.Concede("L1");
            var sound = Card(e, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Sound);
            e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, sound);
            Assert.That(e.Status("L4"), Is.EqualTo(PremiseStatus.Hidden));
            e.Concede("L1c");
            Assert.That(e.Status("L4"), Is.EqualTo(PremiseStatus.Standing), "\"He called you first\"");
            Assert.That(e.InPressure, Is.True);
            e.Tick(Dt, Vector2.Zero);
            Assert.That(e.Pressure.Spec.level, Is.EqualTo(3), "high");
            Assert.That(e.Pressure.Spec.duration, Is.EqualTo(PressureSpec.PainfulDuration), "longer since the redesign (14 s: the three rings)");
            Assert.That(e.Pressure.Spec.calls, Is.EqualTo(3), "the three rings");
        }

        [Test]
        public void LetItRestOnlyOnceEveryPremiseIsHeard()
        {
            var e = Engine(ArgumentDifficulty.Story, DreamLenaDemo.EvidenceWithoutWalt);
            SitOutPressure(e);
            Assert.That(e.CanLetItRest, Is.False);
            Assert.That(e.LetItRest(), Is.False);
            var pilot = new ArgumentPilot();
            for (int i = 0; i < 200 && !e.CanLetItRest && !e.Ended; i++)
            {
                if (e.InPressure) { SitOutPressure(e); continue; }
                if (!pilot.Step(e)) break;
                if (pilot.LastAction == "let it rest") break;
            }
            Assert.That(e.Ended || e.CanLetItRest, Is.True);
            foreach (var p in e.Def.premises)
                if (!e.Ended) Assert.That(e.IsHeard(p.id), Is.True, p.label);
        }

        [Test]
        public void PressingForcesAPieceAndPressingTheClaimEndsIt()
        {
            var e = Engine();
            SitOutPressure(e);
            Assert.That(e.Press("L1"), Is.True);
            Assert.That(e.Status("L1"), Is.EqualTo(PremiseStatus.Forced));
            Assert.That(e.Heat, Is.EqualTo(2));
            Assert.That(e.InPressure, Is.True, "a harsh phase");
            SitOutPressure(e);
            Assert.That(e.Press(ArgumentEngine.ClaimId), Is.True);
            Assert.That(e.Ended, Is.True);
            Assert.That(e.Outcome, Is.EqualTo(ArgumentOutcome.Pressed));
            Assert.That(e.Stance, Is.EqualTo(ArgumentStance.Condemns), "two presses condemn");
        }

        [Test]
        public void ConcedingAFalsePremiseClosesThatLine()
        {
            var e = Engine();
            SitOutPressure(e);
            e.Concede("L1");
            Assert.That(e.Concede("L1b"), Is.True);
            Assert.That(e.Status("L1b"), Is.EqualTo(PremiseStatus.ConcededFalse));
            Assert.That(e.CanChallenge("L1b"), Is.False);
            Assert.That(e.BeginChallenge("L1b", DreamLenaDemo.FourthChair, new List<ReasonCard>()), Is.EqualTo(ChallengeStep.NotAllowed));
        }

        [Test]
        public void ConcedingTheClaimYields()
        {
            var e = Engine();
            SitOutPressure(e);
            Assert.That(e.ConcedeClaim(), Is.True);
            Assert.That(e.Outcome, Is.EqualTo(ArgumentOutcome.Yielded));
            Assert.That(e.Stance, Is.EqualTo(ArgumentStance.Unsure));
        }

        [Test]
        public void HitsRaiseTheHeartRateAndFiveLoseComposure()
        {
            var e = Engine(ArgumentDifficulty.Normal);
            Assert.That(e.HeartRate, Is.EqualTo(70));
            int guard = 0;
            // Stand still and keep getting angry: phases until her heart gives out.
            while (!e.Ended && guard++ < 100000)
            {
                if (e.InPressure) { e.Tick(Dt, Vector2.Zero); continue; }
                var standing = e.Def.premises.FirstOrDefault(p => e.Status(p.id) == PremiseStatus.Standing);
                if (standing == null || !e.Press(standing.id)) break;
            }
            Assert.That(e.Outcome, Is.EqualTo(ArgumentOutcome.ComposureLost));
            Assert.That(e.HeartRate, Is.EqualTo(160));
        }

        // ---------- The creator's additions ----------

        [Test]
        public void TheAntagonisingReplyDodgesHerQuestionAndAngersHer()
        {
            var e = Engine();
            SitOutPressure(e);
            Assert.That(e.CanRetort("L2"), Is.True, "she asks \"Then tell me where I am\"");
            Assert.That(e.Retort("L2"), Is.True);
            Assert.That(e.Anger, Is.EqualTo(1));
            Assert.That(e.Heat, Is.EqualTo(2));
            Assert.That(e.Transcript.Last(x => x.speaker == Speaker.Logic).text, Does.Contain("dodged"));
            Assert.That(e.InPressure, Is.True);
            e.Tick(Dt, Vector2.Zero);
            var angry = e.Pressure.Spec;
            var calm = PressureSpec.For(2, 0, ArgumentDifficulty.Normal, 1f, false, e.Def.hotShapes);
            Assert.That(angry.anger, Is.EqualTo(1));
            Assert.That(angry.speed, Is.GreaterThan(calm.speed), "faster");
            Assert.That(angry.interval, Is.LessThan(calm.interval), "denser");
            Assert.That(angry.gap, Is.LessThan(calm.gap), "narrower gaps");
            Assert.That(angry.gaps, Is.LessThan(calm.gaps), "fewer gaps");
        }

        [Test]
        public void AngerMakesThePatternNoticeablyHarder()
        {
            for (int level = 0; level <= 3; level++)
            {
                var prev = PressureSpec.For(level, 0, ArgumentDifficulty.Normal, 1f, false, null);
                for (int anger = 1; anger <= 3; anger++)
                {
                    var s = PressureSpec.For(level, anger, ArgumentDifficulty.Normal, 1f, false, null);
                    Assert.That(s.speed, Is.GreaterThan(prev.speed * 1.15f), "noticeably faster, level " + level + " anger " + anger);
                    Assert.That(s.interval, Is.LessThan(prev.interval * 0.9f), "noticeably denser");
                    Assert.That(s.gap, Is.LessThanOrEqualTo(prev.gap));
                    prev = s;
                }
            }
            // And in play: the same seconds hold more of it. Furious throws twice as much; the fairness check drops
            // what nobody could escape (PressureRule), and what's left still lands noticeably more often.
            var shapes = new[] { PressureShape.Wall, PressureShape.Bloom };
            void Count(int anger, out int thrown, out int landed)
            {
                thrown = landed = 0;
                for (ulong seed = 11; seed <= 13; seed++)
                {
                    var phase = new PressurePhase(PressureSpec.For(1, anger, ArgumentDifficulty.Normal, 1f, false, shapes), seed, null);
                    var dodger = new PlanningDodger(new DodgePlanner(PressureRule.TestCell, PressureRule.TestStep, PressureRule.PlannerHorizon, PressurePhase.LightSpeed * 0.9f, 0f), 0f, Vector2.Zero);
                    while (!phase.Finished) phase.Tick(Dt, dodger.Steer(phase, Dt));
                    thrown += phase.Spawned + phase.Skipped;
                    landed += phase.Spawned;
                }
            }
            Count(0, out int calmThrown, out int calmLanded);
            Count(3, out int furiousThrown, out int furiousLanded);
            Assert.That(furiousThrown, Is.GreaterThanOrEqualTo(calmThrown * 2), "twice as dense when furious");
            Assert.That(furiousLanded, Is.GreaterThanOrEqualTo(calmLanded * 1.4f), "and still noticeably denser after the fairness check");
        }

        [Test]
        public void AntagonisingPushesTheOutcomeTowardsCondemns()
        {
            Assert.That(ArgumentEngine.StanceFor(ArgumentOutcome.Convinced, 0, 0), Is.EqualTo(ArgumentStance.Amenable));
            Assert.That(ArgumentEngine.StanceFor(ArgumentOutcome.Convinced, 2, 0), Is.EqualTo(ArgumentStance.Unsure));
            Assert.That(ArgumentEngine.StanceFor(ArgumentOutcome.Convinced, 3, 0), Is.EqualTo(ArgumentStance.Condemns));
            Assert.That(ArgumentEngine.StanceFor(ArgumentOutcome.HeardOut, 1, 0), Is.EqualTo(ArgumentStance.Unsure));
            Assert.That(ArgumentEngine.StanceFor(ArgumentOutcome.Pressed, 1, 1), Is.EqualTo(ArgumentStance.Condemns));

            // In Lena's battle (the creator's two endings): the antagonising card, then the retort, then the honest path.
            // Convinced, but an antagonising card that landed means she condemns Nadia.
            var e = Engine();
            SitOutPressure(e);
            e.Concede("L1");
            var dig = Card(e, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Antagonise);
            e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, dig);
            Assert.That(LastOpponentLine(e), Is.EqualTo(dig.reply));
            Assert.That(e.Anger, Is.EqualTo(1));
            SitOutPressure(e);
            e.Retort("L2");
            Assert.That(e.Anger, Is.EqualTo(2));
            SitOutPressure(e);
            Assert.That(new ArgumentPilot().Play(e), Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(e.Stance, Is.EqualTo(ArgumentStance.Condemns));
            Assert.That(e.Record().anger, Is.EqualTo(2));
            Assert.That(e.Antagonised, Is.EqualTo(2));
        }

        [Test]
        public void OutcomesCarryForwardThroughTheSave()
        {
            var e = Engine();
            new ArgumentPilot().Play(e);
            var ledger = new ArgumentLedger();
            ledger.Set(e.Record());
            string text = ledger.Serialize();

            var back = ArgumentLedger.Parse(text);
            var r = back.Get(DreamLenaDemo.Id);
            Assert.That(r, Is.Not.Null);
            Assert.That(r.outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(r.stance, Is.EqualTo(ArgumentStance.Unsure));
            Assert.That(r.truth, Is.EqualTo(DreamLenaDemo.TruthLena));
            Assert.That(r.plays, Is.EqualTo(1));
            Assert.That(back.StanceOf(DreamLenaDemo.Id), Is.EqualTo(ArgumentStance.Unsure), "a later battle reads where Lena stands");
            Assert.That(back.HasTruth(DreamLenaDemo.TruthLena), Is.True);
            Assert.That(back.StanceOf("doctor_night3"), Is.EqualTo(ArgumentStance.None));

            // Played again: the latest result wins, the play count goes on.
            back.Set(new ArgumentRecord { id = DreamLenaDemo.Id, outcome = ArgumentOutcome.Pressed, stance = ArgumentStance.Condemns, presses = 2 });
            var again = ArgumentLedger.Parse(back.Serialize()).Get(DreamLenaDemo.Id);
            Assert.That(again.stance, Is.EqualTo(ArgumentStance.Condemns));
            Assert.That(again.plays, Is.EqualTo(2));

            // A damaged save loses only its bad lines.
            var damaged = ArgumentLedger.Parse("garbage\n" + text + "x|y\n");
            Assert.That(damaged.Records.Count, Is.EqualTo(1));
            Assert.That(ArgumentLedger.Parse(null).Records.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheLongNightSettingIsNearImpossibleButBeatable()
        {
            var spec = PressureSpec.For(0, 0, ArgumentDifficulty.ScriptedButBeatable, 1f, false, null);
            Assert.That(spec.level, Is.EqualTo(3));
            Assert.That(spec.gap, Is.EqualTo(PressureSpec.MinGapScripted).Within(1e-5f), "the gap only just fits her light");
            Assert.That(spec.gap - 2f * PressurePhase.LightRadius, Is.LessThan(0.06f));
            Assert.That(spec.shapes, Is.EqualTo(new[] { PressureShape.Wall }), "walls only: a path always exists");
            Assert.That(spec.interval, Is.LessThan(PressureSpec.For(3, 0, ArgumentDifficulty.Normal, 1f, false, null).interval * 0.6f));

            // Standing still loses (it starts at 124 bpm: two hits from panic).
            var still = Engine(ArgumentDifficulty.ScriptedButBeatable);
            for (int i = 0; i < 20000 && !still.Ended; i++)
            {
                if (still.InPressure) still.Tick(Dt, Vector2.Zero);
                else if (!new ArgumentPilot().Step(still)) break;
            }
            Assert.That(still.Outcome, Is.EqualTo(ArgumentOutcome.ComposureLost));

            // A perfect dodge wins: every phase of the honest path, untouched.
            var perfect = Engine(ArgumentDifficulty.ScriptedButBeatable);
            Assert.That(new ArgumentPilot().Play(perfect), Is.EqualTo(ArgumentOutcome.Convinced), "technically winnable");
            Assert.That(perfect.PhasesRun, Is.GreaterThan(5), "after every exchange");
        }

        [Test]
        public void EveryLongNightPhaseCanBeDodgedPerfectly([Values(1UL, 2UL, 3UL, 5UL, 8UL, 13UL, 21UL, 34UL)] ulong seed)
        {
            for (int anger = 0; anger <= 3; anger++)
            {
                var phase = new PressurePhase(PressureSpec.For(3, anger, ArgumentDifficulty.ScriptedButBeatable, 1f, false, null), seed, null);
                while (!phase.Finished) phase.Tick(Dt, PressurePilot.Steer(phase));
                Assert.That(phase.Hits, Is.EqualTo(0), "seed " + seed + ", anger " + anger);
            }
        }

        [Test]
        public void SupportersCanAddALineAndACard()
        {
            var def = DreamLenaDemo.Build();
            def.supporters.Add(new SupporterDef
            {
                id = "theo", speaker = "Theo", line = "(placeholder) Dad sets my chair too.",
                card = new EvidenceDef { id = "sup_theo_drawing", name = "Theo's drawing", style = EvidenceStyle.Doodled },
                reason = new ReasonDef { kind = ReasonKind.Sound, text = "(placeholder) Theo drew him awake.", reply = "(placeholder) ...He did, didn't he." },
                reasonPremise = "L1b", reasonEvidence = DreamLenaDemo.FourthChair,
            });

            var absent = new ArgumentEngine(def, new ArgumentSetup { difficulty = ArgumentDifficulty.Story, allEvidence = true });
            absent.Start();
            Assert.That(absent.Transcript.Any(x => x.speaker == Speaker.Supporter), Is.False);
            Assert.That(absent.HasEvidence("sup_theo_drawing"), Is.False);

            var setup = new ArgumentSetup { difficulty = ArgumentDifficulty.Story, allEvidence = true };
            setup.supporters.Add("theo");
            var e = new ArgumentEngine(def, setup);
            e.Start();
            Assert.That(e.Transcript.Any(x => x.speaker == Speaker.Supporter && x.who == "Theo"), Is.True, "a line");
            Assert.That(e.Tray().Any(x => x.id == "sup_theo_drawing"), Is.True, "a card");
            SitOutPressure(e);
            e.Concede("L1");
            var cards = new List<ReasonCard>();
            e.BeginChallenge("L1b", DreamLenaDemo.FourthChair, cards);
            Assert.That(cards.Count, Is.EqualTo(5));
            Assert.That(cards.Any(c => c.supporter == "Theo"), Is.True, "an extra reason card");
        }
    }
}
