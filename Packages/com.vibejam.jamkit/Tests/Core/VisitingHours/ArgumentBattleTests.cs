using System.Collections.Generic;
using System.Numerics;
using JamKit.Patterns;
using JamKit.VisitingHours;
using NUnit.Framework;

namespace JamKit.Tests.VisitingHours
{
    public class ArgumentBattleTests
    {
        const float Dt = 1f / 60f;

        static Journal Journal(params string[] known)
        {
            var j = new Journal(new JournalBook(SliceContent.Entries(), SliceContent.Pairs()));
            foreach (var id in known) j.Record(id);
            return j;
        }

        static StatementDef Quiet(string text, float duration = 2f, string key = "", params string[] offers) => new StatementDef
        {
            text = text,
            duration = duration,
            key = key,
            crackLine = "cracked",
            offers = new List<string>(offers),
            volleys = new List<VolleyDef>
            {
                new VolleyDef { pattern = new PatternDef { shape = PatternShape.Ring, count = 8, shots = 0, shotInterval = 0.5f, speed = 0.6f, lifetime = 10f }, originY = 2.7f },
            },
        };

        /// <summary>A small battle without practice: three short statements, the second has a key.</summary>
        static BattleDef Small() => new BattleDef
        {
            statements = new List<StatementDef>
            {
                Quiet("one"),
                Quiet("two", 6f, SliceContent.DedWaltDead, SliceContent.FactPhoto, SliceContent.DedWaltDead),
                Quiet("three"),
            },
            interlude = 0.2f,
            crackInterlude = 0.2f,
            objectFirstDelay = 0.3f,
            objectInterval = 0.6f,
        };

        /// <summary>Plays with the autopilot until the battle ends: per statement, listen, press or say its key.</summary>
        static ArgumentBattle Play(BattleDef def, Journal j, PilotAction[] plan, bool invulnerable = true, float maxSeconds = 120f)
        {
            var b = new ArgumentBattle(def, j, 7) { Invulnerable = invulnerable };
            b.Start();
            for (float t = 0f; t < maxSeconds && b.Phase != BattlePhase.Done; t += Dt)
            {
                int i = b.StatementIndex;
                var action = i >= 0 && i < plan.Length ? plan[i] : PilotAction.Listen;
                if (b.Phase == BattlePhase.Interlude && i + 1 < plan.Length) action = plan[i + 1] == PilotAction.Press ? PilotAction.Press : PilotAction.Listen;
                string key = b.Statement != null ? b.Statement.key : "";
                b.Tick(Dt, BattlePilot.Steer(b, BattlePilot.Goal(b, action, key)));
            }
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.Done), "the battle ended");
            return b;
        }

        [Test]
        public void YourZoneWhenAStatementEndsIsYourStance()
        {
            var b = Play(Small(), Journal(), new[] { PilotAction.Listen, PilotAction.Press, PilotAction.Listen });
            Assert.That(b.StanceAt(0), Is.EqualTo(Stance.Listen));
            Assert.That(b.StanceAt(1), Is.EqualTo(Stance.Press));
            Assert.That(b.StanceAt(2), Is.EqualTo(Stance.Listen));
            Assert.That(b.Presses, Is.EqualTo(1));
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.HeardOut), "one press in three falls through to Heard out");
            Assert.That(b.HeardOutClean, Is.False, "it pressed once");
            Assert.That(b.ForceAdded, Is.EqualTo(1), "each press adds to the Force tally");
        }

        [Test]
        public void ThePressingZoneIsAboveTheMiddle()
        {
            var b = new ArgumentBattle(Small(), Journal());
            b.Start();
            Assert.That(b.InPressZone, Is.False, "Nadia starts low, listening");
            for (int i = 0; i < 90; i++) b.Tick(Dt, new Vector2(0f, 1f));
            Assert.That(b.Player.Y, Is.GreaterThan(0f));
            Assert.That(b.InPressZone, Is.True);
            Assert.That(b.Player.Y, Is.LessThanOrEqualTo(b.Def.halfHeight), "the box holds her in");
        }

        [Test]
        public void PressingMakesTheVolleysDenser()
        {
            int Spawned(bool high)
            {
                var def = Small();
                def.statements[0].duration = 30f;
                var b = new ArgumentBattle(def, Journal(), 3) { Invulnerable = true };
                b.Start();
                for (int i = 0; i < 300; i++) b.Tick(Dt, new Vector2(0f, high ? 1f : -1f));
                return b.BulletsFired;
            }
            int listening = Spawned(false), pressing = Spawned(true);
            Assert.That(pressing, Is.GreaterThan(listening * 1.2f), "pressing: " + pressing + ", listening: " + listening);
        }

        [Test]
        public void ObjectsOnlyAppearWhenNadiaKnowsThem()
        {
            int Appeared(Journal j)
            {
                var b = new ArgumentBattle(Small(), j, 5) { Invulnerable = true };
                b.Start();
                int n = 0;
                // Stay in the corner, away from where objects drift, for the whole first two statements.
                for (int i = 0; i < 60 * 9 && b.Phase != BattlePhase.Done; i++)
                {
                    b.Tick(Dt, new Vector2(-1f, -1f));
                    foreach (var e in b.Events) if (e.kind == BattleEventKind.ObjectAppeared) n++;
                }
                return n;
            }
            Assert.That(Appeared(Journal()), Is.EqualTo(0), "you can't say what you don't know");
            Assert.That(Appeared(Journal(SliceContent.FactPhoto)), Is.GreaterThan(0));
            var b2 = new ArgumentBattle(Small(), Journal(SliceContent.DedWaltDead), 5);
            b2.Start();
            for (int i = 0; i < 60 * 3; i++)
            {
                b2.Tick(Dt, Vector2.Zero);
                foreach (var e in b2.Events)
                    if (e.kind == BattleEventKind.ObjectAppeared) Assert.That(e.statement, Is.EqualTo(1), "only during the statement it answers");
            }
        }

        [Test]
        public void TheRightObjectCracksTheStatementAndSoftensTheNextVolley()
        {
            var j = Journal(SliceContent.KnownBeforeWalt());
            var b = new ArgumentBattle(Small(), j, 9) { Invulnerable = true };
            b.Start();
            bool cracked = false;
            for (int i = 0; i < 60 * 30 && b.Phase != BattlePhase.Done && !cracked; i++)
            {
                var action = b.StatementIndex == 1 ? PilotAction.Touch : PilotAction.Listen;
                b.Tick(Dt, BattlePilot.Steer(b, BattlePilot.Goal(b, action, SliceContent.DedWaltDead), 0.01f));
                foreach (var e in b.Events) if (e.kind == BattleEventKind.Crack) cracked = true;
            }
            Assert.That(cracked, Is.True);
            Assert.That(b.IsCracked(1), Is.True);
            Assert.That(b.StanceAt(1), Is.EqualTo(Stance.Spoke));
            Assert.That(b.LastReply, Is.EqualTo("cracked"));
            Assert.That(b.LastSay, Does.Contain("2015"), "Nadia says the deduction");
            while (b.Phase == BattlePhase.Interlude) b.Tick(Dt, Vector2.Zero);
            Assert.That(b.StatementIndex, Is.EqualTo(2));
            Assert.That(b.VolleyScale, Is.LessThan(1f), "a softer next volley");
        }

        [Test]
        public void AWrongObjectGetsALineAndAHarsherNextVolley()
        {
            var j = Journal(SliceContent.FactPhoto);
            var b = new ArgumentBattle(Small(), j, 11) { Invulnerable = true };
            b.Start();
            int wrongs = 0;
            for (int i = 0; i < 60 * 30 && b.Phase != BattlePhase.Done && wrongs == 0; i++)
            {
                var action = b.StatementIndex == 1 ? PilotAction.Touch : PilotAction.Listen;
                b.Tick(Dt, BattlePilot.Steer(b, BattlePilot.Goal(b, action, SliceContent.FactPhoto), 0.01f));
                foreach (var e in b.Events) if (e.kind == BattleEventKind.Wrong) wrongs++;
            }
            Assert.That(wrongs, Is.EqualTo(1));
            Assert.That(b.WrongCount, Is.EqualTo(1));
            Assert.That(b.LastReply, Is.EqualTo("That's not what this is about."));
            Assert.That(b.IsCracked(1), Is.False);
            while (b.StatementIndex == 1 && b.Phase != BattlePhase.Done) b.Tick(Dt, Vector2.Zero);
            while (b.Phase == BattlePhase.Interlude) b.Tick(Dt, Vector2.Zero);
            Assert.That(b.VolleyScale, Is.GreaterThan(1f), "a harsher next volley");
        }

        [Test]
        public void FiveHitsLoseComposure()
        {
            var def = new BattleDef
            {
                composure = 5,
                hitInvulnerable = 0.2f,
                statements = new List<StatementDef>
                {
                    new StatementDef
                    {
                        text = "aimed",
                        duration = 60f,
                        volleys = new List<VolleyDef>
                        {
                            new VolleyDef { pattern = new PatternDef { shape = PatternShape.Aimed, count = 1, shots = 0, shotInterval = 0.25f, speed = 4f, aimAtTarget = true, lifetime = 5f } },
                        },
                    },
                },
            };
            var b = new ArgumentBattle(def, Journal());
            b.Start();
            int hits = 0;
            for (int i = 0; i < 60 * 30 && b.Phase != BattlePhase.Done; i++)
            {
                b.Tick(Dt, Vector2.Zero);
                foreach (var e in b.Events) if (e.kind == BattleEventKind.Hit) hits++;
            }
            Assert.That(b.Outcome, Is.EqualTo(BattleOutcome.ComposureLost));
            Assert.That(b.Composure, Is.EqualTo(0));
            Assert.That(hits, Is.EqualTo(5));
        }

        [Test]
        public void ThePracticeVolleyCantHurt()
        {
            var b = new ArgumentBattle(SliceContent.WaltBattle(), Journal());
            b.Start();
            Assert.That(b.Phase, Is.EqualTo(BattlePhase.Practice));
            int absorbed = 0;
            // Stand right under the volley's origin.
            while (b.Phase == BattlePhase.Practice)
            {
                b.Tick(Dt, BattlePilot.Steer(b, new Vector2(0f, 2f), 0.01f));
                foreach (var e in b.Events) if (e.kind == BattleEventKind.Absorbed) absorbed++;
            }
            Assert.That(absorbed, Is.GreaterThan(0), "the practice bullets reached her");
            Assert.That(b.Composure, Is.EqualTo(b.MaxComposure));
        }

        [Test]
        public void TheOutcomeRules()
        {
            var def = SliceContent.WaltBattle();
            var none = new bool[4];
            var key = new[] { false, false, true, false };
            var all = new[] { Stance.Listen, Stance.Listen, Stance.Listen, Stance.Listen };
            Assert.That(ArgumentBattle.Resolve(def, all, key, 3), Is.EqualTo(BattleOutcome.Convinced), "every key statement cracked");
            Assert.That(ArgumentBattle.Resolve(def, all, none, 3), Is.EqualTo(BattleOutcome.HeardOut));
            var half = new[] { Stance.Press, Stance.Listen, Stance.Press, Stance.Listen };
            Assert.That(ArgumentBattle.Resolve(def, half, none, 3), Is.EqualTo(BattleOutcome.Pressed), "pressed at least half");
            var one = new[] { Stance.Press, Stance.Listen, Stance.Listen, Stance.Listen };
            Assert.That(ArgumentBattle.Resolve(def, one, none, 3), Is.EqualTo(BattleOutcome.HeardOut), "mixed play falls through by what was done most");
            Assert.That(ArgumentBattle.Resolve(def, half, key, 3), Is.EqualTo(BattleOutcome.Convinced), "Convinced wins over pressing");
            Assert.That(ArgumentBattle.Resolve(def, all, key, 0), Is.EqualTo(BattleOutcome.ComposureLost));
            var optionalOnly = new[] { false, false, false, true };
            Assert.That(ArgumentBattle.Resolve(def, all, optionalOnly, 3), Is.EqualTo(BattleOutcome.HeardOut), "the optional crack alone doesn't convince");

            var keyless = Small();
            keyless.statements[1].key = "";
            Assert.That(ArgumentBattle.Resolve(keyless, new[] { Stance.Listen, Stance.Listen, Stance.Listen }, new bool[3], 5), Is.EqualTo(BattleOutcome.HeardOut), "no key statements: never Convinced");
        }

        [Test]
        public void WaltCanBeConvincedHeardOutOrPressed()
        {
            var convinced = Play(SliceContent.WaltBattle(), Journal(SliceContent.KnownBeforeWalt()),
                new[] { PilotAction.Listen, PilotAction.Listen, PilotAction.Touch, PilotAction.Touch });
            Assert.That(convinced.Outcome, Is.EqualTo(BattleOutcome.Convinced));
            Assert.That(convinced.IsCracked(2), Is.True);
            Assert.That(convinced.IsCracked(3), Is.True, "the optional spiral crack too");

            var heard = Play(SliceContent.WaltBattle(), Journal(),
                new[] { PilotAction.Listen, PilotAction.Listen, PilotAction.Listen, PilotAction.Listen });
            Assert.That(heard.Outcome, Is.EqualTo(BattleOutcome.HeardOut));
            Assert.That(heard.HeardOutClean, Is.True);
            Assert.That(heard.ForceAdded, Is.EqualTo(0));

            var pressed = Play(SliceContent.WaltBattle(), Journal(),
                new[] { PilotAction.Press, PilotAction.Press, PilotAction.Listen, PilotAction.Listen });
            Assert.That(pressed.Outcome, Is.EqualTo(BattleOutcome.Pressed));
            Assert.That(pressed.ForceAdded, Is.EqualTo(3), "two presses plus the Pressed outcome");
        }

        [Test]
        public void TheEventsListIsReusedAndTicksDontGrowIt()
        {
            var b = new ArgumentBattle(SliceContent.WaltBattle(), Journal(SliceContent.KnownBeforeWalt()), 2) { Invulnerable = true };
            b.Start();
            for (int i = 0; i < 60 * 20; i++)
            {
                b.Tick(Dt, new Vector2(0.3f, -0.2f));
                Assert.That(b.Events.Count, Is.LessThan(16));
            }
            Assert.That(b.Bullets.Dropped, Is.EqualTo(0), "the pool is big enough for every volley");
        }
    }
}
