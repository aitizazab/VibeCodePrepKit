using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JamKit.Argument;
using JamKit.Flow;
using NUnit.Framework;

namespace JamKit.Tests.Argument
{
    /// <summary>
    /// The sections presenter (docs/redesign/battle.md, round 2): arguing and dodging take turns. Her case is laid out on
    /// the table and the stick picks what she said; her reply comes as a dodge shaped by its feeling; her patience is the
    /// clock; her own words are caught as she says them and held against her; her star wheel gets an answer; hush passes a
    /// light that holds still; her pencil follows the light; after the three rings the ceiling lifts and the light moves
    /// along her lines; Heard out gives Nadia her watch; and the timing of the whole battle.
    /// </summary>
    public class SectionsTests
    {
        const float Dt = 1f / 60f;

        [OneTimeSetUp]
        public void Files() => ArgumentTestFiles.Use();

        static ArgumentSetup Setup(ArgumentDifficulty d = ArgumentDifficulty.Normal, ulong seed = 11) =>
            new ArgumentSetup { difficulty = d, allEvidence = true, seed = seed };

        static BattleFlow Lena(ArgumentSetup setup = null) => new BattleFlow(new ArgumentEngine(DreamLenaDemo.Build(), setup ?? Setup()), ArgumentPresenter.Sections);

        static float Play(BattleFlow f, BattlePilot pilot, float limit = 1500f, System.Action<BattleFlow> each = null)
        {
            float t = 0f;
            while (!f.Over && t < limit)
            {
                f.Tick(Dt, pilot.Steer(f, Dt));
                each?.Invoke(f);
                t += Dt;
            }
            return t;
        }

        /// <summary>Talks (and dodges) until the argue section is up.</summary>
        static void ToArgue(BattleFlow f, float limit = 90f)
        {
            var pilot = new BattlePilot();
            for (float t = 0f; f.State != BattleState.Argue && t < limit; t += Dt)
                f.Tick(Dt, f.State == BattleState.Pressure ? pilot.Steer(f, Dt) : new BattleInput { fire = f.QuoteId.Length == 0 || f.QuoteOpen, pick = -1 });
            Assert.That(f.State, Is.EqualTo(BattleState.Argue), "the argue section");
        }

        static void Pick(BattleFlow f, string premise)
        {
            var at = f.Map[f.Map.IndexOf(premise)].position;
            f.Tick(Dt, new BattleInput { hasPoint = true, point = at, pick = -1 });
            Assert.That(f.SelectedId, Is.EqualTo(premise), "picked " + premise);
        }

        // ---------- The two sections ----------

        [Test]
        public void ArguingAndDodgingTakeTurns()
        {
            var f = Lena();
            Assert.That(f.Presenter, Is.EqualTo(ArgumentPresenter.Sections));
            var states = new List<BattleState>();
            var last = BattleState.Over;
            var pilot = new BattlePilot();
            for (float t = 0f; t < 120f && states.Count(s => s == BattleState.Argue) < 3; t += Dt)
            {
                var input = f.State == BattleState.Argue ? pilot.Steer(f, Dt) : f.State == BattleState.Pressure ? pilot.Steer(f, Dt) : new BattleInput { fire = f.QuoteId.Length == 0 || f.QuoteOpen, pick = -1 };
                f.Tick(Dt, input);
                if (f.State != last) { last = f.State; states.Add(f.State); }
                if (f.State == BattleState.Argue) Assert.That(f.Engine.Pressure, Is.Null, "nothing comes at her light while she argues");
            }
            TestContext.WriteLine(string.Join(" > ", states));
            Assert.That(states, Has.No.Member(BattleState.Debate), "no debate in the sections");
            int argue = states.IndexOf(BattleState.Argue);
            Assert.That(argue, Is.GreaterThan(0));
            Assert.That(states.Skip(argue).Contains(BattleState.Pressure), Is.True, "her reply comes as a dodge");
            // Every exchange is followed by a dodge before the next argue section.
            for (int i = argue + 1; i < states.Count; i++)
                if (states[i] == BattleState.Argue) Assert.That(states.Skip(argue + 1).Take(i - argue - 1), Has.Member(BattleState.Pressure), "a dodge between two argue sections");
        }

        [Test]
        public void HerCaseIsLaidOutAndTheStickPicksAStatement()
        {
            var f = Lena(Setup(ArgumentDifficulty.Story));
            var map = f.Map;
            var claim = map[map.IndexOf(ArgumentEngine.ClaimId)];
            var l1 = map[map.IndexOf("L1")];
            var l1b = map[map.IndexOf("L1b")];
            var l3 = map[map.IndexOf("L3")];
            Assert.That(claim.position.Y, Is.GreaterThan(l1.position.Y), "the claim on top");
            Assert.That(l1b.position.Y, Is.LessThan(l1.position.Y), "what hangs from L1 under it");
            Assert.That(l1b.position.X, Is.EqualTo(l1.position.X).Within(0.01f));
            Assert.That(l1b.visible, Is.False, "hidden until she says it");
            Assert.That(l3.position.X, Is.GreaterThan(l1.position.X));
            foreach (var e in map.Entries)
            {
                Assert.That(System.Math.Abs(e.position.X), Is.LessThan(PressurePhase.HalfWidth - 0.3f), e.id + " on the table");
                Assert.That(System.Math.Abs(e.position.Y), Is.LessThan(PressurePhase.HalfHeight - 0.2f), e.id + " on the table");
            }
            ToArgue(f);
            Pick(f, "L1");
            // The stick: right from L1 is the next pillar of her case.
            for (int k = 0; k < 30; k++) f.Tick(Dt, new BattleInput { move = new Vector2(1f, 0f), pick = -1 });
            Assert.That(f.SelectedId, Is.Not.EqualTo("L1"), "the stick steps along her case");
            Assert.That(f.Map[f.Selected].position.X, Is.GreaterThan(l1.position.X));
            for (int k = 0; k < 60; k++) f.Tick(Dt, BattleInput.None);
            for (int k = 0; k < 20; k++) f.Tick(Dt, new BattleInput { move = new Vector2(0f, 1f), pick = -1 });
            Assert.That(f.SelectedId, Is.EqualTo(ArgumentEngine.ClaimId), "up to the claim");
        }

        [Test]
        public void AVerbOnTheStatementPickedThenHerReplyAsADodge()
        {
            var f = Lena(Setup(ArgumentDifficulty.Story));
            ToArgue(f);
            Pick(f, "L1");
            f.Tick(Dt, new BattleInput { concede = true, pick = -1 });
            Assert.That(f.Engine.Status("L1"), Is.EqualTo(PremiseStatus.Conceded), "Y: That's true");
            Assert.That(f.State, Is.EqualTo(BattleState.Talk), "the exchange is said");
            Assert.That(f.Engine.InPressure, Is.True, "and her reply is queued as a dodge");
            for (float t = 0f; f.State != BattleState.Pressure && t < 30f; t += Dt) f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            Assert.That(f.State, Is.EqualTo(BattleState.Pressure));
            Assert.That(f.Engine.Pressure.Spec.hush, Is.EqualTo(1), "a concession (her care): hush");
            Assert.That(f.Engine.Pressure.Spec.hushTeach, Is.True, "the first teaches: it can't hurt");
            Assert.That(f.Engine.Transcript.Any(x => x.text == f.Def.barks[ArgumentBarks.Hush]), Is.True, "\"Shh. Hold still.\"");
        }

        [Test]
        public void HerPatienceRunsOutAndSheComesAtYou()
        {
            var f = Lena();
            ToArgue(f);
            Assert.That(f.ArgueLimit, Is.InRange(18f, 35f), "she waits a while, not forever");
            int heat = f.Engine.Heat;
            float sketchAt = -1f;
            float t = 0f;
            while (f.State == BattleState.Argue && t < 60f)
            {
                f.Tick(Dt, BattleInput.None);
                t += Dt;
                if (sketchAt < 0f && f.ArgueSketch > 0f) sketchAt = t;
            }
            Assert.That(f.State, Is.EqualTo(BattleState.Pressure).Or.EqualTo(BattleState.Talk), "the attack she sketched comes");
            Assert.That(f.Engine.InPressure || f.Engine.Pressure != null, Is.True);
            Assert.That(f.Engine.Heat, Is.EqualTo(heat + 1), "her temper rises");
            Assert.That(sketchAt, Is.GreaterThan(0f), "she sketched it first (the overlap)");
            Assert.That(t - sketchAt, Is.InRange(BattleFlow.SketchSeconds - 0.5f, BattleFlow.SketchSeconds + 0.5f));
            Assert.That(f.Engine.Transcript.Any(x => x.text == f.Def.barks[ArgumentBarks.Idle]), Is.True);

            var story = Lena(Setup(ArgumentDifficulty.Story));
            ToArgue(story);
            Assert.That(story.ArgueLimit, Is.Zero, "Story: no clock");
            for (int k = 0; k < 60 * 60; k++) story.Tick(Dt, BattleInput.None);
            Assert.That(story.State, Is.EqualTo(BattleState.Argue));
        }

        // ---------- Her own words (SELECTION.md 6) ----------

        [Test]
        public void HerOwnWordsAreCaughtAsSheSaysThemAndHeldAgainstHer()
        {
            var f = Lena();
            ToArgue(f);
            Assert.That(f.Engine.HasEvidence(DreamLenaDemo.QuoteHad), Is.False, "her words aren't in the hand until caught (even with every card)");
            Pick(f, "L3");
            f.Tick(Dt, new BattleInput { ask = true, pick = -1 });
            Assert.That(f.State, Is.EqualTo(BattleState.Talk));
            // Too soon: the first time it's only ignored (nobody knows yet).
            bool opened = false;
            for (float t = 0f; t < 20f && !f.Engine.Caught(DreamLenaDemo.QuoteHad); t += Dt)
            {
                bool open = f.QuoteOpen;
                if (open) opened = true;
                // Press every frame before the window (ignored, the first time), then once in it.
                f.Tick(Dt, new BattleInput { fire = !opened || open, pick = -1 });
            }
            Assert.That(opened, Is.True, "the window opened as she said \"this life he had\"");
            Assert.That(f.Engine.Caught(DreamLenaDemo.QuoteHad), Is.True, "caught");
            Assert.That(f.Count(CueKind.QuoteCaught), Is.EqualTo(1));
            Assert.That(f.Engine.HasEvidence(DreamLenaDemo.QuoteHad), Is.True);
            Assert.That(f.Cards, Does.Contain(DreamLenaDemo.QuoteHad), "in Nadia's hand");

            // Held against "This is the life he wants. The life he has." (L3', brought out by the same question).
            Assert.That(f.Engine.Status("L3b"), Is.EqualTo(PremiseStatus.Standing));
            var cards = new List<ReasonCard>();
            f.Engine.SkipPressure();
            Assert.That(f.Engine.BeginChallenge("L3b", DreamLenaDemo.QuoteHad, cards), Is.EqualTo(ChallengeStep.Reasons));
            var sound = cards.First(c => c.reason.kind == ReasonKind.Sound).reason;
            Assert.That(sound.text, Does.Contain("had"), "her own word, held to her");
            Assert.That(f.Engine.ResolveChallenge("L3b", DreamLenaDemo.QuoteHad, sound), Is.True);
            Assert.That(f.Engine.Status("L3b"), Is.EqualTo(PremiseStatus.Broken), "a second way to break it");
            Assert.That(f.Engine.Status("L3c"), Is.EqualTo(PremiseStatus.Standing), "she falls back, as with Saturday off");
        }

        [Test]
        public void PressingTooSoonLosesHerWordsOnceSheKnowsAndStoryCatchesThem()
        {
            // Story: they collect themselves.
            var story = Lena(Setup(ArgumentDifficulty.Story));
            ToArgue(story);
            Pick(story, "L3");
            story.Tick(Dt, new BattleInput { ask = true, pick = -1 });
            for (float t = 0f; t < 15f && !story.Engine.Caught(DreamLenaDemo.QuoteHad); t += Dt) story.Tick(Dt, BattleInput.None);
            Assert.That(story.Engine.Caught(DreamLenaDemo.QuoteHad), Is.True, "Story catches her words by itself");

            // Normal: after the first, a press before her words are said loses them.
            var f = Lena();
            ToArgue(f);
            Pick(f, "L3");
            f.Tick(Dt, new BattleInput { ask = true, pick = -1 });
            for (float t = 0f; t < 15f && f.QuoteId.Length == 0; t += Dt) f.Tick(Dt, BattleInput.None);
            Assert.That(f.QuoteId, Is.EqualTo(DreamLenaDemo.QuoteHad), "\"this life he had\" is in her answer");
            for (float t = 0f; t < 15f && f.QuoteId.Length > 0; t += Dt) f.Tick(Dt, BattleInput.None); // the first goes by
            Assert.That(f.Engine.Caught(DreamLenaDemo.QuoteHad), Is.False, "let go by: not caught");
            Assert.That(f.Count(CueKind.QuoteMissed), Is.GreaterThanOrEqualTo(1));
            // Ask L3' why: "All of us". Press as the line starts, before the words.
            for (float t = 0f; f.State != BattleState.Argue && t < 60f; t += Dt) f.Tick(Dt, f.State == BattleState.Pressure ? new BattlePilot().Steer(f, Dt) : new BattleInput { pick = -1 });
            Pick(f, "L3b");
            f.Tick(Dt, new BattleInput { ask = true, pick = -1 });
            for (float t = 0f; t < 10f && f.QuoteId != DreamLenaDemo.QuoteAllOfUs; t += Dt) f.Tick(Dt, BattleInput.None);
            Assert.That(f.QuoteId, Is.EqualTo(DreamLenaDemo.QuoteAllOfUs));
            Assert.That(f.QuoteOpen, Is.False, "not said yet");
            f.Tick(Dt, new BattleInput { fire = true, pick = -1 });
            Assert.That(f.QuoteId, Is.Empty, "too soon: lost");
            Assert.That(f.Caption, Is.EqualTo(ArgumentStrings.Shared["screen.quote.early"]));
            for (int k = 0; k < 200; k++) f.Tick(Dt, new BattleInput { fire = k % 7 == 0, pick = -1 });
            Assert.That(f.Engine.Caught(DreamLenaDemo.QuoteAllOfUs), Is.False);
        }

        [Test]
        public void EveryQuoteIsInOneOfHerLines()
        {
            var def = DreamLenaDemo.Build();
            var lines = ArgumentText.Capture(def);
            foreach (var id in DreamLenaDemo.Quotes)
            {
                var ev = def.Evidence(id);
                Assert.That(ev.IsQuote, Is.True);
                Assert.That(ev.words, Is.Not.Empty);
                bool said = def.premises.Any(p => Typewriter.StripTags(Typewriter.Spelled(p.askReply ?? "")).IndexOf(ev.words, System.StringComparison.OrdinalIgnoreCase) >= 0);
                Assert.That(said, Is.True, id + ": \"" + ev.words + "\" is in one of her answers");
            }
        }

        // ---------- Her star wheel, her watch ----------

        [Test]
        public void HerStarWheelGetsAnAnswerAndCostsNothing()
        {
            var e = new ArgumentEngine(DreamLenaDemo.Build(), Setup(ArgumentDifficulty.Story));
            e.Start();
            e.SkipPressure();
            int heart = e.HeartStep, heat = e.Heat;
            Assert.That(e.HasEvidence(DreamLenaDemo.Thursdays), Is.True);
            Assert.That(e.BeginChallenge("L2", DreamLenaDemo.Thursdays, new List<ReasonCard>()), Is.EqualTo(ChallengeStep.Aside));
            Assert.That(e.HeartStep, Is.EqualTo(heart), "no cost");
            Assert.That(e.Heat, Is.EqualTo(heat));
            Assert.That(e.Status("L2"), Is.EqualTo(PremiseStatus.Standing), "nothing breaks");
            Assert.That(e.Transcript.Last(x => x.speaker == Speaker.Opponent).text, Does.Contain("Thursdays"));
            Assert.That(e.Meters.asides, Is.EqualTo(1));
            // Her hand-over knows it.
            Assert.That(e.Line("letItRestReply", e.Def.letItRestReply), Does.Contain("Thursdays"));
            var plain = new ArgumentEngine(DreamLenaDemo.Build(), Setup(ArgumentDifficulty.Story));
            Assert.That(plain.Line("letItRestReply", plain.Def.letItRestReply), Does.Not.Contain("Thursdays"));
        }

        [Test]
        public void HeardOutGivesNadiaHerWatch()
        {
            var e = new ArgumentEngine(DreamLenaDemo.Build(), Setup(ArgumentDifficulty.Story));
            e.Start();
            e.SkipPressure();
            e.ForceOutcome(ArgumentOutcome.HeardOut);
            var record = e.Record();
            Assert.That(record.personal, Is.EqualTo(DreamLenaDemo.Watch));
            var result = ArgumentBattles.Result(e.Def, record);
            Assert.That(result.keepsake, Is.EqualTo(DreamLenaDemo.Watch), "the game gets the watch");
            var save = new SaveData();
            Assert.That(ArgumentBattles.OwnsKeepsake(save), Is.False);
            Assert.That(ArgumentBattles.RecordKeepsake(save, record), Is.True);
            Assert.That(save.HasFlag(ArgumentBattles.KeepsakeFlag), Is.True, "the save's keepsake flag");
            Assert.That(ArgumentBattles.OwnsKeepsake(save), Is.True);
            Assert.That(e.Def.letItRestReply, Does.Contain("wrist"), "she hands it over");
            Assert.That(e.Def.personalText, Does.Contain("watch"));

            var won = new ArgumentEngine(DreamLenaDemo.Build(), Setup(ArgumentDifficulty.Story));
            won.Start();
            won.ForceOutcome(ArgumentOutcome.Convinced);
            var other = new SaveData();
            Assert.That(ArgumentBattles.RecordKeepsake(other, won.Record()), Is.False, "only Heard out gives it");
            Assert.That(other.HasFlag(ArgumentBattles.KeepsakeFlag), Is.False);
        }

        // ---------- Hush, answer me, her notes ----------

        static PressureSpec Quiet(int hush = 0, int answer = 0, bool trail = false, float duration = 12f)
        {
            var s = PressureSpec.For(0, 0, ArgumentDifficulty.Normal, 1f, false, new[] { PressureShape.Wall });
            s.only = true; // nothing else
            s.duration = duration;
            s.hush = hush;
            s.answer = answer;
            s.trail = trail;
            return s;
        }

        [Test]
        public void HushPassesALightThatHoldsStillAndHurtsOneThatStruggles()
        {
            var still = new PressurePhase(Quiet(hush: 1), 3, null);
            still.PlaceLight(new Vector2(0.5f, 0.2f));
            int stillHits = 0;
            bool sawHush = false;
            while (!still.Finished) { still.Tick(Dt, Vector2.Zero); stillHits += still.TakeHits(); sawHush |= still.HushOn; }
            Assert.That(sawHush, Is.True, "a hush closed in");
            Assert.That(stillHits, Is.Zero, "held still: it passes");

            var moving = new PressurePhase(Quiet(hush: 1), 3, null);
            moving.PlaceLight(new Vector2(0.5f, 0.2f));
            int movingHits = 0;
            float a = 0f;
            while (!moving.Finished)
            {
                a += Dt * 2f;
                moving.Tick(Dt, new Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a)));
                movingHits += moving.TakeHits();
            }
            Assert.That(movingHits, Is.GreaterThan(0), "struggling: it hurts");

            var teach = Quiet(hush: 1);
            teach.hushTeach = true;
            var soft = new PressurePhase(teach, 3, null);
            soft.PlaceLight(new Vector2(0.5f, 0.2f));
            int softHits = 0;
            a = 0f;
            while (!soft.Finished) { a += Dt * 2f; soft.Tick(Dt, new Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a))); softHits += soft.TakeHits(); }
            Assert.That(softHits, Is.Zero, "the first teaches: it can't hurt");
        }

        [Test]
        public void AnswerMePassesALightThatMoves()
        {
            var still = new PressurePhase(Quiet(answer: 2), 3, null);
            still.PlaceLight(new Vector2(0f, 0f));
            int stillHits = 0;
            while (!still.Finished) { still.Tick(Dt, Vector2.Zero); stillHits += still.TakeHits(); }
            Assert.That(stillHits, Is.GreaterThan(0), "standing still, her question hurts");
            var moving = new PressurePhase(Quiet(answer: 2), 3, null);
            moving.PlaceLight(new Vector2(0f, 0f));
            int movingHits = 0;
            float a = 0f;
            while (!moving.Finished) { a += Dt * 3f; moving.Tick(Dt, new Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a)) * 0.6f); movingHits += moving.TakeHits(); }
            Assert.That(movingHits, Is.Zero, "answering (moving), it passes");
        }

        [Test]
        public void HerPencilFollowsTheLightAndCatchesOneThatStops()
        {
            var still = new PressurePhase(Quiet(trail: true), 3, null);
            still.PlaceLight(new Vector2(-1f, 0f));
            int stillHits = 0;
            while (!still.Finished) { still.Tick(Dt, Vector2.Zero); stillHits += still.TakeHits(); }
            Assert.That(stillHits, Is.GreaterThan(0), "a light that stops is caught");

            // Round the whole table: her pencil never catches up.
            var round = new PressurePhase(Quiet(trail: true), 3, null);
            round.PlaceLight(new Vector2(-2.5f, -1.2f));
            int roundHits = 0, leg = 0;
            var corners = new[] { new Vector2(2.5f, -1.2f), new Vector2(2.5f, 1.2f), new Vector2(-2.5f, 1.2f), new Vector2(-2.5f, -1.2f) };
            bool sawTip = false;
            while (!round.Finished)
            {
                var to = corners[leg] - round.Light;
                if (to.Length() < 0.1f) { leg = (leg + 1) % 4; to = corners[leg] - round.Light; }
                round.Tick(Dt, Vector2.Normalize(to));
                roundHits += round.TakeHits();
                if (round.TrailOn) sawTip = true;
            }
            Assert.That(sawTip, Is.True, "her pencil followed");
            Assert.That(roundHits, Is.Zero, "using the whole table, it never catches up");
            Assert.That(round.Refused, Is.Zero, "every mark pencilled before it's inked (the rule)");
        }

        // ---------- Her sky (SELECTION.md 7) ----------

        [Test]
        public void UnderHerSkyTheLightMovesOnlyAlongHerLines()
        {
            var def = DreamLenaDemo.Build();
            var e = new ArgumentEngine(def, Setup(ArgumentDifficulty.Story));
            e.Start();
            e.SkipPressure();
            e.Ask("L3");
            e.Concede("L1");
            e.Concede("L2");
            var map = new CaseMap(def);
            map.Refresh(e);
            var rails = new Rails();
            map.BuildRails(rails);
            Assert.That(rails.Walkable, Is.GreaterThan(4), "her lines");
            Assert.That(rails.Cover.Count, Is.EqualTo(2), "the facts conceded are cover");
            var spec = PressureSpec.For(1, 0, ArgumentDifficulty.Normal, 1f, false, new[] { PressureShape.Arc });
            spec.sky = true;
            var phase = new PressurePhase(spec, 7, null);
            phase.SetRails(rails);
            phase.PlaceLight(new Vector2(0.3f, -0.9f));
            Assert.That(rails.DistanceTo(phase.Light), Is.LessThan(0.01f), "put on a line");
            var stick = new[] { new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0.3f), new Vector2(0.2f, -1f) };
            float moved = 0f;
            for (int k = 0; k < 600; k++)
            {
                var before = phase.Light;
                phase.Tick(Dt, stick[(k / 90) % stick.Length]);
                moved += Vector2.Distance(before, phase.Light);
                Assert.That(rails.DistanceTo(phase.Light), Is.LessThan(0.02f), "always on a line");
            }
            Assert.That(moved, Is.GreaterThan(3f), "and it gets around");
            Assert.That(phase.Spawned, Is.GreaterThan(0), "streaks came");
            Assert.That(phase.Refused, Is.Zero);
            Assert.That(phase.Blobs.Where(b => b.serial > 0).All(b => b.streak || b.mark), Is.True, "under her sky, streaks");
        }

        [Test]
        public void AfterTheThreeRingsTheCeilingLifts()
        {
            var f = Lena(Setup(ArgumentDifficulty.Story));
            ToArgue(f);
            var e = f.Engine;
            // Bring "He called you first" out: concede L1, break L1' with the fourth chair, concede L1''.
            e.Concede("L1");
            e.SkipPressure();
            var cards = new List<ReasonCard>();
            e.BeginChallenge("L1b", DreamLenaDemo.FourthChair, cards);
            e.ResolveChallenge("L1b", DreamLenaDemo.FourthChair, cards.First(c => c.reason.kind == ReasonKind.Sound).reason);
            e.SkipPressure();
            e.Concede("L1c");
            Assert.That(e.InPressure, Is.True, "the three rings are coming");
            Assert.That(f.Sky, Is.False);
            var pilot = new BattlePilot();
            bool rang = false;
            for (float t = 0f; t < 60f && !f.Sky; t += Dt)
            {
                f.Tick(Dt, f.State == BattleState.Pressure ? pilot.Steer(f, Dt) : new BattleInput { fire = true, pick = -1 });
                if (e.Pressure != null && e.Pressure.Spec.calls > 0) rang = true;
            }
            Assert.That(rang, Is.True, "the three rings played");
            Assert.That(f.Sky, Is.True, "then the ceiling lifts");
            Assert.That(e.SkyOpen, Is.True);
            Assert.That(e.Transcript.Any(x => x.text == f.Def.barks[ArgumentBarks.Sky]), Is.True, "\"Someone just looked up and drew the lines.\"");
            Assert.That(f.Rails.Walkable, Is.GreaterThan(0));
            // The next phase is played on her lines.
            for (float t = 0f; t < 90f && !(f.State == BattleState.Pressure && e.Pressure != null); t += Dt)
            {
                var input = f.State == BattleState.Argue ? new BattleInput { hasPoint = true, point = f.Map[f.Map.IndexOf("L2")].position, ask = f.SelectedId == "L2", pick = -1 } : new BattleInput { fire = true, pick = -1 };
                f.Tick(Dt, input);
            }
            Assert.That(e.Pressure, Is.Not.Null);
            Assert.That(e.Pressure.Spec.sky, Is.True, "a sky phase");
            Assert.That(e.Pressure.Rails, Is.Not.Null, "on her lines");
        }

        // ---------- The bedside, Walt, the whole battle ----------

        [Test]
        public void TheBedsideStillShapesHerInTheSections()
        {
            var f = new BattleFlow(new ArgumentEngine(DreamLenaDemo.Build(), new ArgumentSetup { allEvidence = true, angerStart = 1, admitted = true }), ArgumentPresenter.Sections);
            Assert.That(f.Engine.Anger, Is.EqualTo(1), "Day 2's \"Lena\": a step angrier");
            Assert.That(f.Engine.Status("L4"), Is.EqualTo(PremiseStatus.Standing), "Day 1's \"the phone\": on the table from the start");
            f.Map.Refresh(f.Engine);
            Assert.That(f.Map[f.Map.IndexOf("L4")].visible, Is.True, "and on her case");
        }

        [Test]
        public void WaltsTutorialTeachesTheSectionsAndCantBeLost()
        {
            var setup = new ArgumentSetup { seed = 4 };
            setup.evidence.AddRange(new[] { WaltNight1.WaltDead, WaltNight1.SameSaturday, WaltNight1.WaltAlive });
            var f = new BattleFlow(new ArgumentEngine(WaltNight1.Build(), setup), ArgumentPresenter.Sections);
            var pilot = new BattlePilot(true, BattlePilot.Mistake.WrongCard, BattlePilot.Mistake.WrongReason, BattlePilot.Mistake.Press, BattlePilot.Mistake.StandStill);
            int maxHeart = 0;
            float t = Play(f, pilot, 1200f, x => maxHeart = System.Math.Max(maxHeart, x.Engine.HeartStep));
            TestContext.WriteLine("Walt (sections): " + f.Engine.Outcome + " in " + t.ToString("0") + " s (" + f.ArgueSeconds.ToString("0") + " s arguing, " + f.DodgeSeconds.ToString("0") + " s dodging, " + f.TalkSeconds.ToString("0") + " s talk); coached: "
                + string.Join(", ", ArgumentCoach.Moments.Where(m => f.Engine.Coached(m))));
            Assert.That(f.Over, Is.True);
            Assert.That(f.Engine.Outcome, Is.Not.EqualTo(ArgumentOutcome.ComposureLost));
            Assert.That(maxHeart, Is.LessThan(ArgumentEngine.PanicStep));
            foreach (var m in new[] { ArgumentCoach.Argue, ArgumentCoach.Ask, ArgumentCoach.Concede, ArgumentCoach.Challenge, ArgumentCoach.Because, ArgumentCoach.Wrong, ArgumentCoach.Showdown })
                Assert.That(f.Engine.Coached(m), Is.True, "Mags taught " + m);
            Assert.That(f.Engine.Coached(ArgumentCoach.Debate), Is.False, "not the debate's lesson");
            Assert.That(f.Engine.Transcript.Any(x => x.speaker == Speaker.Supporter && x.text.Contains("pick it and ask him why")), Is.True, "Mags's sections wording (sections=1)");
        }

        [Test]
        public void LenasSectionsRunEightToTwelveMinutes()
        {
            var f = Lena(Setup(ArgumentDifficulty.Normal, 21));
            var pilot = new BattlePilot(true, FirstTime);
            var log = new List<string>();
            float t = Play(f, pilot, 1500f, x =>
            {
                foreach (var c in x.Cues)
                    if (c.kind == CueKind.Ask || c.kind == CueKind.Concede || c.kind == CueKind.Break || c.kind == CueKind.WrongCard || c.kind == CueKind.PressureIn || c.kind == CueKind.Hesitate
                        || c.kind == CueKind.ShowdownStart || c.kind == CueKind.QuoteCaught || c.kind == CueKind.SkyOpens || c.kind == CueKind.Notes || c.kind == CueKind.Idle)
                        log.Add((x.ActiveSeconds + x.TalkSeconds).ToString("0") + "s " + c.kind + " " + c.id + (c.kind == CueKind.PressureIn ? " (" + Describe(x.Engine.Pressure) + ")" : ""));
                    else if (c.kind == CueKind.HeartUp)
                        log.Add((x.ActiveSeconds + x.TalkSeconds).ToString("0") + "s   heart " + x.Engine.HeartRate + " in " + x.State + (x.State == BattleState.Pressure ? " (" + Describe(x.Engine.Pressure) + ")" : ""));
            });
            float total = f.ActiveSeconds + f.TalkSeconds;
            TestContext.WriteLine("Lena (sections): " + f.Engine.Outcome + " in " + t.ToString("0") + " s: " + f.ArgueSeconds.ToString("0") + " s arguing, " + f.DodgeSeconds.ToString("0")
                + " s dodging, " + (f.ActiveSeconds - f.ArgueSeconds - f.DodgeSeconds).ToString("0") + " s Because/showdown, " + f.TalkSeconds.ToString("0") + " s talk; dodging "
                + (100f * f.DodgeSeconds / total).ToString("0") + "%, in play " + (100f * f.ActiveSeconds / total).ToString("0") + "%; heart " + f.Engine.HeartRate + ", turns " + f.Turn
                + ", phases " + f.Engine.PhasesRun + ", sky " + f.Sky + ", quotes " + DreamLenaDemo.Quotes.Count(q => f.Engine.Caught(q)));
            foreach (var l in log) TestContext.WriteLine("  " + l);
            Assert.That(f.Over, Is.True, "it ends");
            Assert.That(f.Engine.Outcome, Is.EqualTo(ArgumentOutcome.Convinced), "the honest line, with a player's mistakes, still convinces her");
            Assert.That(t, Is.InRange(8f * 60f, 12f * 60f), "8-12 minutes");
            Assert.That(f.Sky, Is.True, "the second phase: her sky");
            Assert.That(f.DodgeSeconds / total, Is.GreaterThanOrEqualTo(0.3f), "a real share of it dodging");
            Assert.That(f.ActiveSeconds / total, Is.GreaterThanOrEqualTo(0.6f), "60% or more in play (arguing, dodging, Because, the showdown)");
        }

        static string Describe(PressurePhase p)
        {
            if (p == null) return "";
            var s = p.Spec;
            return p.Duration.ToString("0.0") + " s, level " + s.level + (s.hush > 0 ? ", hush" : "") + (s.answer > 0 ? ", answer" : "") + (s.trail ? ", notes" : "")
                + (s.sky ? ", sky" : "") + (s.calls > 0 ? ", rings" : "");
        }

        /// <summary>A first-time player's slips in the sections: three wrong cards, two wrong reasons, a hesitation, and one
        /// of her quotable lines let go by.</summary>
        public static BattlePilot.Mistake[] FirstTime => new[]
        {
            BattlePilot.Mistake.WrongCard, BattlePilot.Mistake.WrongReason, BattlePilot.Mistake.MissQuote, BattlePilot.Mistake.WrongCard,
            BattlePilot.Mistake.Hesitate, BattlePilot.Mistake.WrongReason, BattlePilot.Mistake.WrongCard,
        };

        [Test]
        public void TheFastAutopilotFinishesTheSections()
        {
            foreach (var d in new[] { ArgumentDifficulty.Story, ArgumentDifficulty.Normal })
            {
                var f = Lena(Setup(d, 5));
                float t = Play(f, new BattlePilot(), 1500f);
                TestContext.WriteLine("fast (" + d + "): " + f.Engine.Outcome + " in " + t.ToString("0") + " s");
                Assert.That(f.Over, Is.True);
                Assert.That(f.Engine.Outcome, Is.EqualTo(ArgumentOutcome.Convinced), d.ToString());
                Assert.That(t, Is.LessThan(560f), "the house's skip plays it at 4x within its 150 s");
            }
        }
    }
}
