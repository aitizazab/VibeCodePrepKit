using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Argument;
using NUnit.Framework;

namespace JamKit.Tests.Argument
{
    /// <summary>
    /// Nights 1 and 2 (docs/NIGHTS-1-2.md): the creator's notes in the prose files (// notes never shown, their
    /// replacement lines used, *bold*), conditional lines on Lena's anger, Force and Despair, cards that appear only once
    /// their memory or deduction is found, Lena's two endings (unsure or condemning, and what that hands the game), and
    /// Walt's Night 1 tutorial with Mags on the earpiece.
    /// </summary>
    public class ArgumentNightsTests
    {
        const float Dt = 1f / 60f;

        [OneTimeSetUp]
        public void Files() => ArgumentTestFiles.Use();

        static ArgumentEngine Lena(ArgumentSetup setup)
        {
            var e = new ArgumentEngine(DreamLenaDemo.Build(), setup);
            e.Start();
            SitOut(e);
            return e;
        }

        static ArgumentSetup Story(int force = 0, int despair = 0) =>
            new ArgumentSetup { difficulty = ArgumentDifficulty.Story, allEvidence = true, seed = 7, force = force, despair = despair };

        static void SitOut(ArgumentEngine e)
        {
            for (int i = 0; i < 4000 && e.InPressure && !e.Ended; i++) e.Tick(Dt, Vector2.Zero);
        }

        static ReasonDef Card(ArgumentEngine e, string premise, string evidence, ReasonKind kind)
        {
            var cards = new List<ReasonCard>();
            Assert.That(e.BeginChallenge(premise, evidence, cards), Is.EqualTo(ChallengeStep.Reasons), premise + " with " + evidence);
            return cards.First(x => x.reason.kind == kind).reason;
        }

        static string LastSaid(ArgumentEngine e, Speaker who) => e.Transcript.Last(x => x.speaker == who).text;

        // ---------- The format ----------

        [Test]
        public void NotesAreNeverShownAndTheCreatorsReplacementsAreUsed()
        {
            var d = ArgumentText.Parse(
                "// a note on its own line\n" +
                "L1.text: The line. // a note after it\n" +
                "L1.askReply: Spoken. (// a note in brackets)\n" +
                "claim: The old claim. // The new claim.\n" +
                "opening: The old opening. // The new opening.\n" +
                "noTouchReply: The old redirect. // The new redirect.\n" +
                "pressClaimLine: Kept. // not a replacement here\n");
            Assert.That(d.ContainsKey("//"), Is.False);
            Assert.That(d["L1.text"], Is.EqualTo("The line."));
            Assert.That(d["L1.askReply"], Is.EqualTo("Spoken."));
            Assert.That(d["claim"], Is.EqualTo("The new claim."), "the creator's rewrite is the note");
            Assert.That(d["opening"], Is.EqualTo("The new opening."));
            Assert.That(d["noTouchReply"], Is.EqualTo("The new redirect."));
            Assert.That(d["pressClaimLine"], Is.EqualTo("Kept."), "elsewhere a note is only a note");
        }

        [Test]
        public void StarsAreBold()
        {
            Assert.That(ArgumentText.Bold("He *needs* me."), Is.EqualTo("He <b>needs</b> me."));
            Assert.That(ArgumentText.Bold("*Two words* here, and *one*."), Is.EqualTo("<b>Two words</b> here, and <b>one</b>."));
            Assert.That(ArgumentText.Bold("3 * 4 * 5"), Is.EqualTo("3 * 4 * 5"), "loose stars stay");
            Assert.That(ArgumentText.Bold("a ** b"), Is.EqualTo("a ** b"));
            Assert.That(ArgumentText.Parse("L1.text: Don't you *dare*.")["L1.text"], Is.EqualTo("Don't you <b>dare</b>."));
        }

        [Test]
        public void TheOpeningLineLeavesTheOpeningPose()
        {
            foreach (var def in new[] { DreamLenaDemo.Build(), WaltNight1.Build() })
            {
                var e = new ArgumentEngine(def, new ArgumentSetup { difficulty = ArgumentDifficulty.Story, allEvidence = true });
                e.Start();
                Assert.That(e.Transcript.Where(x => x.speaker == Speaker.Opponent).All(x => x.expression.Length == 0), Is.True,
                    def.id + ": no mood yet, so the screen shows the antagonistic opening pose");
            }
        }

        [Test]
        public void TheCreatorsBattleReadsTheirWay()
        {
            var def = DreamLenaDemo.Build();
            Assert.That(def.claim, Is.EqualTo("He <b>needs</b> me. He needs Theo."));
            Assert.That(def.opening, Is.EqualTo("Come on, Nadia. I know you loved this as much as he did."));
            Assert.That(def.noTouchReply, Is.EqualTo("Do you take me for a fool? Or do you just wish to mock me?"));
            Assert.That(def.Premise("L1").trueReply, Is.EqualTo("Don't. Don't you <b>dare</b> tell me I'm wrong."));
            var nonsequitur = def.Premise("L3b").challenges[0].reasons.First(r => r.kind == ReasonKind.NonSequitur);
            Assert.That(nonsequitur.reply, Is.EqualTo("That's cruel, and false. Why would you even say that?"));
            // No note shows anywhere.
            foreach (var kv in ArgumentText.Capture(def)) Assert.That(kv.Value, Does.Not.Contain("//"), kv.Key);
            foreach (var v in def.variants) Assert.That(v.text, Does.Not.Contain("//"), v.id);
            Assert.That(def.personalId, Is.EqualTo(DreamLenaDemo.Planisphere));
        }

        // ---------- Conditions ----------

        [Test]
        public void ConditionsReadAndOrAndTheMeters()
        {
            var c = ArgumentCondition.Parse("anger>=1|presses>=1", out string error);
            Assert.That(error, Is.Null);
            Assert.That(c.Holds(new ArgumentMeters()), Is.False);
            Assert.That(c.Holds(new ArgumentMeters { anger = 1 }), Is.True);
            Assert.That(c.Holds(new ArgumentMeters { presses = 2 }), Is.True);
            var and = ArgumentCondition.Parse("despair>=3, anger<=1, force<=1", out error);
            Assert.That(and.Holds(new ArgumentMeters { despair = 3, anger = 1, force = 1 }), Is.True);
            Assert.That(and.Holds(new ArgumentMeters { despair = 3, anger = 2, force = 1 }), Is.False);
            Assert.That(ArgumentCondition.Parse("anger=0", out _).Holds(new ArgumentMeters()), Is.True);
            Assert.That(ArgumentCondition.Parse("anger!=0", out _).Holds(new ArgumentMeters()), Is.False);
            Assert.That(ArgumentCondition.Parse("", out _).Holds(new ArgumentMeters { anger = 3 }), Is.True, "no condition: always");
            Assert.That(ArgumentCondition.Parse("mood>=2", out error), Is.Null);
            Assert.That(error, Does.Contain("mood"));
            Assert.That(ArgumentCondition.Parse("anger>=lots", out error), Is.Null);
            Assert.That(ArgumentCondition.Parse("anger", out error), Is.Null);

            var v = ArgumentText.ParseVariants("L1.text: plain\nL1.text[anger>=2]: angry // note\n");
            Assert.That(v.Count, Is.EqualTo(1));
            Assert.That(v[0].id, Is.EqualTo("L1.text"));
            Assert.That(v[0].condition, Is.EqualTo("anger>=2"));
            Assert.That(v[0].text, Is.EqualTo("angry"));
            Assert.That(ArgumentText.Parse("L1.text[anger>=2]: angry").ContainsKey("L1.text"), Is.False, "a conditional line isn't a plain one");

            var def = DreamLenaDemo.Structure();
            var problems = ArgumentText.ApplyVariants(def, "L1.text[mood>2]: x\nL9.text[anger>1]: y\nL1.text[anger>1]: z\n");
            Assert.That(problems.Count, Is.EqualTo(2), "a bad condition and an unknown line are reported");
            Assert.That(def.variants.Count, Is.EqualTo(1));
        }

        [Test]
        public void SheTurnsSharperWhenAngryOrCloseToCondemning()
        {
            const string silver = "You know it's true. I don't need your silver tongue to say otherwise.";
            // Calm: the plain line.
            var calm = Lena(Story());
            calm.Concede("L1");
            calm.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, Card(calm, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Sound));
            SitOut(calm);
            Assert.That(calm.BeginChallenge("L1c", DreamLenaDemo.Photo, new List<ReasonCard>()), Is.EqualTo(ChallengeStep.TrueOne));
            Assert.That(LastSaid(calm, Speaker.Opponent), Is.EqualTo(calm.Def.Premise("L1c").trueReply));

            // After an antagonising card: close to condemning, so the sharper line.
            var sharp = Lena(Story());
            sharp.Concede("L1");
            sharp.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, Card(sharp, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Antagonise));
            SitOut(sharp);
            sharp.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, Card(sharp, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Sound));
            SitOut(sharp);
            Assert.That(sharp.BeginChallenge("L1c", DreamLenaDemo.Photo, new List<ReasonCard>()), Is.EqualTo(ChallengeStep.TrueOne));
            Assert.That(LastSaid(sharp, Speaker.Opponent), Is.EqualTo(silver));
        }

        [Test]
        public void ForgivenessAndCrueltyDependOnTheMeters()
        {
            const string forgive = "...Thank you for saying it. And you should know I'd forgive you for any role you think you played in this.";
            // L4 surfaces after L1''... reach it and concede it: calm and no despair gets the addition.
            ArgumentEngine ToL4(ArgumentSetup s)
            {
                var e = Lena(s);
                e.Concede("L1");
                e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, Card(e, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Sound));
                SitOut(e);
                e.Concede("L1c");
                SitOut(e);
                Assert.That(e.Status("L4"), Is.EqualTo(PremiseStatus.Standing), "L4 surfaced");
                return e;
            }
            var calm = ToL4(Story());
            calm.Concede("L4");
            Assert.That(LastSaid(calm, Speaker.Opponent), Is.EqualTo(forgive));
            var despairing = ToL4(Story(despair: 1));
            despairing.Concede("L4");
            Assert.That(LastSaid(despairing, Speaker.Opponent), Is.EqualTo(despairing.Def.Premise("L4").concedeReply), "any despair: the plain line");

            // Max anger and high Force: the cruel line.
            var e2 = Lena(Story(force: 3));
            e2.Retort("L2");
            SitOut(e2);
            e2.Concede("L1");
            e2.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, Card(e2, "L1b", DreamLenaDemo.FourthChair, ReasonKind.Antagonise));
            SitOut(e2);
            e2.Ask("L3");
            e2.Press("L3");
            SitOut(e2);
            Assert.That(e2.Anger, Is.EqualTo(3), "a retort, an antagonising card and a press");
            Assert.That(e2.Meters.force, Is.EqualTo(4), "the tally passed in, plus this battle's press");
            e2.ResolveChallenge("L3b", DreamLenaDemo.SaturdayOff, Card(e2, "L3b", DreamLenaDemo.SaturdayOff, ReasonKind.NonSequitur));
            Assert.That(LastSaid(e2, Speaker.Opponent), Is.EqualTo("That's cruel, and false. I should cut your tongue out for that."));
        }

        [Test]
        public void TheTamerPressWhileNadiasBeenGentle()
        {
            var gentle = Lena(Story());
            gentle.Concede("L1");
            SitOut(gentle);
            gentle.Press("L2");
            Assert.That(LastSaid(gentle, Speaker.Nadia), Is.EqualTo("Please, Lena. Stop saying that. You don't know that."));
            Assert.That(gentle.Anger, Is.EqualTo(1), "pressing angers Lena");

            var rough = Lena(Story());
            rough.Retort("L2");
            SitOut(rough);
            Assert.That(rough.CanPress("L2"), Is.True);
            rough.Press("L2");
            Assert.That(LastSaid(rough, Speaker.Nadia), Is.EqualTo(rough.Def.Premise("L2").pressLine));
        }

        // ---------- Cards ----------

        [Test]
        public void ACardIsInTheTrayOnlyOnceItsMemoryOrDeductionIsFound()
        {
            var none = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { difficulty = ArgumentDifficulty.Story });
            Assert.That(none.Tray(), Is.Empty, "nothing found, nothing in the tray");
            foreach (var id in DreamLenaDemo.Cards) Assert.That(none.HasEvidence(id), Is.False, id);

            var setup = new ArgumentSetup { difficulty = ArgumentDifficulty.Story };
            setup.evidence.AddRange(new[] { DreamLenaDemo.FourthChair, "mem_some_other_memory" });
            var some = new ArgumentEngine(DreamLenaDemo.Build(), setup);
            Assert.That(some.Tray().Select(c => c.id), Is.EqualTo(new[] { DreamLenaDemo.FourthChair }), "only what's found (other journal ids are ignored)");

            // A card can also be unlocked by another journal id.
            var def = DreamLenaDemo.Build();
            def.Evidence(DreamLenaDemo.BadDream).unlockedBy.Add("mem_theos_room_drawing");
            var s2 = new ArgumentSetup { difficulty = ArgumentDifficulty.Story };
            s2.evidence.Add("mem_theos_room_drawing");
            Assert.That(new ArgumentEngine(def, s2).HasEvidence(DreamLenaDemo.BadDream), Is.True);

            // The plan's table: every card the battle has, and what breaks what.
            var lena = DreamLenaDemo.Structure();
            Assert.That(lena.evidence.Select(c => c.id), Is.EquivalentTo(DreamLenaDemo.Cards.Concat(DreamLenaDemo.Quotes)), "the cards, and her own words (caught in play)");
            Assert.That(lena.Premise("L1b").ChallengeFor(DreamLenaDemo.FourthChair), Is.Not.Null);
            Assert.That(lena.Premise("L3b").ChallengeFor(DreamLenaDemo.SaturdayOff), Is.Not.Null);
            Assert.That(lena.Premise("L3c").ChallengeFor(DreamLenaDemo.SameSaturday), Is.Not.Null);
            Assert.That(lena.Premise("L3").ChallengeFor(DreamLenaDemo.BadDream), Is.Not.Null, "Theo's drawing breaks \"He chose this\" (2026-10-12)");
            Assert.That(lena.Premise("L3").ChallengeFor(DreamLenaDemo.BadDream).effectTarget, Is.EqualTo("L5"), "and brings out L5");
            Assert.That(lena.Premise("L5").unbreakable, Is.True, "L5 can't be broken tonight: it carries into Night 3");
            Assert.That(lena.premises.Where(p => p.key).Select(p => p.id), Is.EquivalentTo(new[] { "L1b", "L3b", "L3c" }), "Convinced = L1', L3', L3''");
        }

        [Test]
        public void WithOnlyWhatsFoundTheHonestPathStillWorks()
        {
            var setup = new ArgumentSetup { difficulty = ArgumentDifficulty.Story, seed = 3 };
            setup.evidence.AddRange(new[] { DreamLenaDemo.FourthChair, DreamLenaDemo.SaturdayOff, DreamLenaDemo.SameSaturday, DreamLenaDemo.PoliceReport });
            var e = new ArgumentEngine(DreamLenaDemo.Build(), setup);
            Assert.That(new ArgumentPilot().Play(e), Is.EqualTo(ArgumentOutcome.Convinced));

            var missing = new ArgumentSetup { difficulty = ArgumentDifficulty.Story, seed = 3 };
            missing.evidence.AddRange(new[] { DreamLenaDemo.FourthChair, DreamLenaDemo.SaturdayOff });
            var e2 = new ArgumentEngine(DreamLenaDemo.Build(), missing);
            Assert.That(new ArgumentPilot().Play(e2), Is.EqualTo(ArgumentOutcome.HeardOut), "without the same Saturday, L3'' stands");
        }

        [Test]
        public void TheAutopilotNeverStallsWithoutTheCards()
        {
            // The house can open the argument before anything is found (a chapter jump): nothing breaks, the premises behind
            // the missing cards never come out, so it can't be heard out either. The autopilot gives in rather than stall.
            var e = new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { difficulty = ArgumentDifficulty.Story, seed = 9 });
            Assert.That(new ArgumentPilot().Play(e), Is.EqualTo(ArgumentOutcome.Yielded));
            Assert.That(e.Ended, Is.True);
            Assert.That(e.Stance, Is.EqualTo(ArgumentStance.Unsure), "giving in leaves her unsure");
        }

        // ---------- The two endings ----------

        [Test]
        public void LenaEndsUnsureOrCondemning()
        {
            Assert.That(ArgumentEngine.TwoEndings(ArgumentOutcome.Convinced, 0), Is.EqualTo(ArgumentStance.Unsure));
            Assert.That(ArgumentEngine.TwoEndings(ArgumentOutcome.HeardOut, 0), Is.EqualTo(ArgumentStance.Unsure));
            Assert.That(ArgumentEngine.TwoEndings(ArgumentOutcome.Yielded, 0), Is.EqualTo(ArgumentStance.Unsure));
            Assert.That(ArgumentEngine.TwoEndings(ArgumentOutcome.Pressed, 0), Is.EqualTo(ArgumentStance.Condemns));
            Assert.That(ArgumentEngine.TwoEndings(ArgumentOutcome.ComposureLost, 0), Is.EqualTo(ArgumentStance.Condemns));
            Assert.That(ArgumentEngine.TwoEndings(ArgumentOutcome.Convinced, 1), Is.EqualTo(ArgumentStance.Condemns), "an antagonising card that landed");

            var honest = Lena(Story());
            new ArgumentPilot().Play(honest);
            var r = ArgumentBattles.Result(honest.Def, honest.Record());
            Assert.That(r.outcome, Is.EqualTo(ArgumentOutcome.Convinced));
            Assert.That(r.Condemns, Is.False);
            Assert.That(r.despairAdded, Is.Zero);
            Assert.That(r.truth, Is.EqualTo(DreamLenaDemo.TruthLena));

            var pressed = Lena(Story());
            pressed.Press(ArgumentEngine.ClaimId);
            var p = ArgumentBattles.Result(pressed.Def, pressed.Record());
            Assert.That(p.outcome, Is.EqualTo(ArgumentOutcome.Pressed));
            Assert.That(p.Condemns, Is.True);
            Assert.That(p.despairAdded, Is.EqualTo(ArgumentBattles.DespairOnCondemn), "condemning raises Despair");
            Assert.That(p.forceAdded, Is.EqualTo(1), "the press goes on the Force tally");

            var lost = Lena(Story());
            lost.ForceOutcome(ArgumentOutcome.ComposureLost);
            Assert.That(ArgumentBattles.Result(lost.Def, lost.Record()).Condemns, Is.True);
        }

        // ---------- Night 1: Walt, with Mags ----------

        static ArgumentEngine Walt(ArgumentDifficulty difficulty, params string[] found)
        {
            var setup = new ArgumentSetup { difficulty = difficulty, seed = 5 };
            setup.evidence.AddRange(found);
            var e = new ArgumentEngine(WaltNight1.Build(), setup);
            e.Start();
            return e;
        }

        [Test]
        public void MagsTeachesEachVerbAsItsFirstNeeded()
        {
            var e = Walt(ArgumentDifficulty.Normal, WaltNight1.WaltDead, WaltNight1.SameSaturday);
            Assert.That(e.InPressure, Is.False, "no opening pressure: the first comes with the first misstep");
            Assert.That(e.Coached(ArgumentCoach.Debate), Is.True, "first: the debate (move, keep clear)");
            Assert.That(LastSaid(e, Speaker.Supporter), Is.EqualTo(e.Def.coach[ArgumentCoach.Debate]));
            Assert.That(e.Transcript.Last(x => x.speaker == Speaker.Supporter).who, Is.EqualTo("Mags"));
            Assert.That(e.Coached(ArgumentCoach.Challenge), Is.False, "nothing she can break is on the table yet");
            Assert.That(e.Transcript.Count(x => x.speaker == Speaker.Supporter), Is.EqualTo(1), "one line at a time");
            // The battle's flow coaches asking as the first statement flies.
            Assert.That(e.CoachNow(ArgumentCoach.Ask), Is.True);
            Assert.That(e.CoachNow(ArgumentCoach.Ask), Is.False, "once");
            e.Ask("W1");
            Assert.That(e.Status("W2"), Is.EqualTo(PremiseStatus.Standing), "asking reveals the key premise");
            Assert.That(e.Coached(ArgumentCoach.Concede), Is.True, "she's heard something true: concede it");
            Assert.That(e.Coached(ArgumentCoach.Challenge), Is.False, "the next lesson waits for the next exchange");
            e.Concede("W1");
            Assert.That(e.Status("W3"), Is.EqualTo(PremiseStatus.Standing), "conceding brings out what he hangs on it");
            Assert.That(e.Coached(ArgumentCoach.Challenge), Is.True, "now: something she has can break it");
            // A misstep: pressure (the flow has Mags say what it cost, and forcing at the next one: BattleFlowTests).
            e.ResolveChallenge("W3", WaltNight1.SameSaturday, Card(e, "W3", WaltNight1.SameSaturday, ReasonKind.Beside));
            Assert.That(e.Coached(ArgumentCoach.Press), Is.False, "the real-time battle's flow coaches missteps");
            Assert.That(e.InPressure, Is.True);
            e.Tick(Dt, Vector2.Zero);
            Assert.That(e.Coached(ArgumentCoach.Pressure), Is.True);
            SitOut(e);
            e.ResolveChallenge("W2", WaltNight1.WaltDead, Card(e, "W2", WaltNight1.WaltDead, ReasonKind.Sound));
            Assert.That(e.Coached(ArgumentCoach.Broken) || e.Ended, Is.True);
            Assert.That(e.Outcome, Is.EqualTo(ArgumentOutcome.Convinced), "the key link broken");
            Assert.That(e.Record().truth, Is.EqualTo(WaltNight1.TruthId), "Walt's truth, which also breaks Lena's L3''");
        }

        [Test]
        public void TheTutorialCantBeLost()
        {
            var e = Walt(ArgumentDifficulty.Normal, WaltNight1.SameSaturday);
            e.Ask("W1");
            e.Concede("W1");
            int phases = 0;
            for (int round = 0; round < 12 && !e.Ended; round++)
            {
                if (!e.CanChallenge("W3")) break;
                e.ResolveChallenge("W3", WaltNight1.SameSaturday, Card(e, "W3", WaltNight1.SameSaturday, ReasonKind.NonSequitur));
                if (e.InPressure) phases++;
                // Standing still in the middle: hit after hit.
                for (int i = 0; i < 6000 && e.InPressure && !e.Ended; i++) e.Tick(Dt, Vector2.Zero);
                Assert.That(e.HeartStep, Is.LessThanOrEqualTo(ArgumentEngine.PanicStep - 1), "composure stops a step short");
            }
            Assert.That(phases, Is.GreaterThan(3));
            Assert.That(e.Outcome, Is.Not.EqualTo(ArgumentOutcome.ComposureLost));
            Assert.That(e.HeartStep, Is.EqualTo(ArgumentEngine.PanicStep - 1), "it did get there, and no further");
            Assert.That(e.Def.pressureScale, Is.LessThan(1f), "gentler pressure");
        }

        [Test]
        public void WaltStepsAsideInEveryOutcomeAndIsRemembered()
        {
            var noDeduction = Walt(ArgumentDifficulty.Story, WaltNight1.WaltAlive);
            Assert.That(new ArgumentPilot().Play(noDeduction), Is.EqualTo(ArgumentOutcome.HeardOut), "without Walt is dead, the key link stands");
            Assert.That(noDeduction.Record().personal, Is.EqualTo(WaltNight1.Wrench));

            var forced = Walt(ArgumentDifficulty.Story);
            forced.Press(ArgumentEngine.ClaimId);
            var r = ArgumentBattles.Result(forced.Def, forced.Record());
            Assert.That(r.outcome, Is.EqualTo(ArgumentOutcome.Pressed));
            Assert.That(r.despairAdded, Is.Zero, "only Lena's condemning raises Despair");
            Assert.That(r.forceAdded, Is.EqualTo(1));
            Assert.That(forced.Transcript.Last(x => x.speaker == Speaker.Opponent).expression, Is.EqualTo("sad"), "pushed: his sad drawing (face.hurt)");

            var ledger = new ArgumentLedger();
            ledger.Set(forced.Record());
            Assert.That(ArgumentLedger.Parse(ledger.Serialize()).StanceOf(WaltNight1.Id), Is.EqualTo(forced.Stance), "remembered for the ending");
        }

        // ---------- The hand-off ----------

        [Test]
        public void TheGameStartsABattleByIdAndGetsTheResultBack()
        {
            Assert.That(ArgumentBattles.Ids, Is.EquivalentTo(new[] { WaltNight1.Id, DreamLenaDemo.Id }));
            foreach (var id in ArgumentBattles.Ids) Assert.That(ArgumentBattles.Build(id).id, Is.EqualTo(id));
            Assert.That(ArgumentBattles.Build("nobody"), Is.Null);

            var context = new ArgumentContext { force = 2, despair = 9, difficulty = ArgumentDifficulty.Story };
            context.found.AddRange(new[] { DreamLenaDemo.FourthChair, "mem_unrelated" });
            var setup = ArgumentBattles.Setup(context);
            Assert.That(setup.force, Is.EqualTo(2));
            Assert.That(setup.despair, Is.EqualTo(ArgumentMeters.DespairMax), "clamped to 0..3");
            Assert.That(setup.difficulty, Is.EqualTo(ArgumentDifficulty.Story));
            var e = new ArgumentEngine(ArgumentBattles.Build(ArgumentBattles.Lena), setup);
            Assert.That(e.Tray().Select(c => c.id), Is.EqualTo(new[] { DreamLenaDemo.FourthChair }));

            var closedEarly = ArgumentBattles.Result(e.Def, null);
            Assert.That(closedEarly.outcome, Is.EqualTo(ArgumentOutcome.None));
            Assert.That(closedEarly.battleId, Is.EqualTo(DreamLenaDemo.Id));
        }
    }
}
