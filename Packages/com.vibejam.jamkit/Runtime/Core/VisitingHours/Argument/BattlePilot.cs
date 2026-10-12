using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Argument
{
    /// <summary>
    /// The real-time battle's autopilot (the prototype's "watch it play", the recorded run for review, the house's tests):
    /// it decides like <see cref="ArgumentPilot"/>, then plays the decision out in the debate. It loads the card,
    /// waits for the statement to come round, steers its light there through the pressure (a <see cref="PlanningDodger"/>
    /// whose home is the target), and fires once its light is on the weak words; it picks the reason, cuts the
    /// showdown's words in time, dodges the phases, and keeps moving through the three rings.
    ///
    /// <see cref="Human"/> paces it like a first-time player (reads before it moves, thinks over the reasons); off, it
    /// plays as fast as the battle lets it. Scripted <see cref="Mistake"/>s make it slip the way a player would, so a
    /// recorded run shows what wrong choices cost.
    /// </summary>
    public sealed class BattlePilot
    {
        public enum Mistake
        {
            /// <summary>The next challenge first shows a card that doesn't touch what she said.</summary>
            WrongCard,
            /// <summary>The next "Because..." picks a reason that doesn't follow.</summary>
            WrongReason,
            /// <summary>The next "Because..." waits until their patience runs out.</summary>
            Hesitate,
            /// <summary>Forces the next premise it means to argue (a press and its showdown) instead.</summary>
            Press,
            /// <summary>Misses one word in the next showdown.</summary>
            MissCut,
            /// <summary>Stands still in the next pressure phase (hit after hit).</summary>
            StandStill,
            /// <summary>The next evidence shot lands on the statement but off its glowing words (it glances off).</summary>
            Glance,
            /// <summary>Lets the next of her quotable words go by without catching them (the sections presenter).</summary>
            MissQuote,
        }

        /// <summary>A first-time player's slips (the recorded run and the timing test): three wrong cards, a glance off
        /// the statement, two wrong reasons and a hesitation.</summary>
        public static Mistake[] FirstTime => new[]
        {
            Mistake.WrongCard, Mistake.Glance, Mistake.WrongReason, Mistake.WrongCard, Mistake.Hesitate, Mistake.WrongReason, Mistake.WrongCard,
        };

        readonly ArgumentPilot _brain = new ArgumentPilot();
        readonly List<Mistake> _mistakes = new List<Mistake>();
        // The fairness tests' player (a fine grid, 0.9 of her speed): untouched by every fair pattern.
        readonly DodgePlanner _planner = new DodgePlanner(PressureRule.TestCell, PressureRule.TestStep, PressureRule.PlannerHorizon, PressurePhase.LightSpeed * 0.9f, 0f);
        PlanningDodger _dodger;
        PressurePhase _dodging;
        bool _human;
        bool _hasPlan;
        PilotPlan _plan;
        string _card = "";
        bool _press, _wrongCardNow;
        float _seen, _cycleWait, _orbit;
        string _lastAction = "";

        public BattlePilot(bool human = false, params Mistake[] mistakes)
        {
            _human = human;
            if (mistakes != null) _mistakes.AddRange(mistakes);
        }

        public bool Human { get => _human; set => _human = value; }
        public string LastAction => _lastAction;
        public int MistakesLeft => _mistakes.Count;

        bool Take(Mistake m)
        {
            int i = _mistakes.IndexOf(m);
            if (i < 0) return false;
            _mistakes.RemoveAt(i);
            return true;
        }

        bool Has(Mistake m) => _mistakes.Contains(m);

        /// <summary>The input for the next tick of <paramref name="dt"/>.</summary>
        public BattleInput Steer(BattleFlow f, float dt)
        {
            var input = BattleInput.None;
            switch (f.State)
            {
                case BattleState.Intro:
                case BattleState.Talk:
                case BattleState.Ending:
                    // Her words worth holding her to: catch them as she says them (a person a beat after they start).
                    if (f.QuoteId.Length > 0)
                    {
                        if (f.QuoteOpen)
                        {
                            _quoteSeen += dt;
                            if (Has(Mistake.MissQuote)) { if (_quoteSeen > 0.05f) _missingQuote = f.QuoteId; }
                            else if (!_human || _quoteSeen > 0.35f) input.fire = true;
                        }
                        break;
                    }
                    if (_missingQuote.Length > 0) { Take(Mistake.MissQuote); _missingQuote = ""; }
                    _quoteSeen = 0f;
                    // A player reads; fast, it hurries every line.
                    if (!_human) input.fire = true;
                    else if (f.Delivery.LineDone && f.StateTime > 0.2f) input.fire = (int)(f.StateTime * 10f) % 25 == 0;
                    break;
                case BattleState.Argue: DoArgue(f, dt, ref input); break;
                case BattleState.Debate: DoDebate(f, dt, ref input); break;
                case BattleState.Because: DoBecause(f, dt, ref input); break;
                case BattleState.Pressure: DoPressure(f, dt, ref input); break;
                case BattleState.Showdown: DoShowdown(f, ref input); break;
            }
            return input;
        }

        // ---------- The debate ----------

        void DoDebate(BattleFlow f, float dt, ref BattleInput input)
        {
            var e = f.Engine;
            var d = f.Debate;
            for (int k = 0; k < d.Slips.Count; k++)
                if (d.Slips[k].active)
                {
                    // Something she isn't saying, drifting past: go and catch it.
                    input.move = Dodge(d.Hazards, d.Slips[k].position, dt, f.Light);
                    return;
                }
            if (_human && _hasPlan && d.Find(_plan.premise, false) < 0) _hasPlan = false; // it went by: act on what's next
            if (!_hasPlan) Decide(f);
            if (!_hasPlan) { input.move = Dodge(d.Hazards, new Vector2(-1f, 0f), dt, f.Light); return; }

            // Load the card it means to show.
            string want = _plan.verb == PilotVerb.Challenge ? _card : _plan.verb == PilotVerb.Rest ? BattleFlow.RestCard : null;
            if (_cycleWait > 0f) _cycleWait -= dt;
            if (want != null && f.CardInHand != want && _cycleWait <= 0f && (!_human || s0Seen(f)))
            {
                // The shorter way round.
                int at = -1, n = f.Cards.Count;
                for (int k = 0; k < n; k++) if (f.Cards[k] == want) at = k;
                if (at >= 0) input.cycle = ((at - f.CardIndex + n) % n) <= n / 2 ? 1 : -1;
                // Each card read on the way (its name and note) before the next.
                _cycleWait = _human ? 0.9f : 0.05f;
            }

            string target = _plan.premise;
            int i = d.Find(target, false);
            if (i < 0)
            {
                _seen = 0f;
                // Wait mid-table for it to come round (keeping clear of the pressure).
                input.move = Dodge(d.Hazards, new Vector2(-0.6f, 0f), dt, f.Light);
                // A target that's gone for good (resolved some other way): think again.
                if (target != ArgumentEngine.ClaimId && e.Status(target) != PremiseStatus.Standing && !(_plan.verb == PilotVerb.Ask && e.CanAsk(target))) _hasPlan = false;
                return;
            }
            var s = d[i];
            _seen += dt;
            bool evidence = _plan.verb == PilotVerb.Challenge || _plan.verb == PilotVerb.Rest;
            bool glance = _plan.verb == PilotVerb.Challenge && Has(Mistake.Glance) && s.weak0 > 0.35f;
            var aim = glance ? new Vector2(s.Left + 0.12f, s.position.Y) : _plan.verb == PilotVerb.Challenge ? Debate.WeakCentre(s) : s.position;
            aim += s.velocity * 0.25f;
            aim.X = Clamp(aim.X, -PressurePhase.HalfWidth + 0.15f, PressurePhase.HalfWidth - 0.15f);
            aim.Y = Clamp(aim.Y, -PressurePhase.HalfHeight + 0.15f, PressurePhase.HalfHeight - 0.15f);
            // A person reads it first (while dodging): the first time it's said, the whole statement; after that, a glance.
            // Showing evidence takes a moment more: which card, and where it bites.
            if (_human && s.age < ReadTime(s) + (_plan.verb == PilotVerb.Challenge ? 1.5f : 0f)) { input.move = Dodge(d.Hazards, new Vector2(-0.6f, 0f), dt, f.Light); return; }
            input.move = Dodge(d.Hazards, aim, dt, f.Light);

            int under = d.Under(f.Light, evidence, out bool onWeak);
            bool onTarget = under == i && (_plan.verb != PilotVerb.Challenge || onWeak || glance);
            if (_press)
            {
                if (under == i || f.HoldingPress) input.pressHeld = true;
                if (f.HoldingPress) input.move = Vector2.Zero;
                return;
            }
            if (!onTarget) return;
            if (want != null && want.Length > 0 && want[0] != '@' && f.BurnedFor(want) > 0) return; // wait for it to come back
            switch (_plan.verb)
            {
                case PilotVerb.Ask: input.ask = true; break;
                case PilotVerb.Concede: input.concede = true; break;
                case PilotVerb.Yield: input.concede = input.concedeHeld = true; input.move = Vector2.Zero; return;
                case PilotVerb.Challenge:
                case PilotVerb.Rest:
                    if (f.CardInHand != want) return;
                    input.fire = true;
                    break;
            }
            _lastAction = _plan.what;
            if (glance && !onWeak && input.fire) { Take(Mistake.Glance); _lastAction = "glanced off " + _plan.premise; return; } // try again
            if (_wrongCardNow) { _wrongCardNow = false; Take(Mistake.WrongCard); }
            _hasPlan = false;
        }

        /// <summary>A first-time player acts on what's in front of them: of the statements flying now, the first it has
        /// something to do with (ask what it hasn't heard about, break what a card in hand breaks, concede what's true,
        /// ask again where there's more), and it lets it rest on the claim only when nothing can be broken.</summary>
        bool HumanChoose(BattleFlow f, out PilotPlan plan)
        {
            plan = default;
            var e = f.Engine;
            var d = f.Debate;
            bool breakable = false;
            foreach (var p in e.VisiblePremises)
            {
                if (e.Status(p.id) != PremiseStatus.Standing || p.unbreakable) continue;
                foreach (var c in p.challenges) if (c != null && e.HasEvidence(c.evidence)) breakable = true;
            }
            for (int i = 0; i < d.Statements.Count; i++)
            {
                var s = d[i];
                // A first-time player hears a new statement out the first time it's said, and acts when it comes round.
                if (!s.active || s.fate != Debate.Fate.Flying || s.question || s.life - s.age < 1.6f || !s.repeat) continue;
                if (s.claim)
                {
                    if (!breakable && e.CanLetItRest) { plan = new PilotPlan { verb = PilotVerb.Rest, premise = ArgumentEngine.ClaimId, what = "let it rest" }; return true; }
                    continue;
                }
                var pd = e.Def.Premise(s.premise);
                if (pd == null || e.Status(pd.id) != PremiseStatus.Standing) continue;
                if (e.TimesAsked(pd.id) == 0) { plan = new PilotPlan { verb = PilotVerb.Ask, premise = pd.id, what = "asked about " + pd.label }; return true; }
                if (!pd.unbreakable)
                    foreach (var c in pd.challenges)
                    {
                        if (c == null || !e.HasEvidence(c.evidence)) continue;
                        var sound = ArgumentPilot.Sound(c);
                        if (sound == null) continue;
                        plan = new PilotPlan { verb = PilotVerb.Challenge, premise = pd.id, evidence = c.evidence, reason = sound, what = "broke " + pd.label + " with " + c.evidence };
                        return true;
                    }
                if (pd.truth != PremiseTruth.False && !pd.key) { plan = new PilotPlan { verb = PilotVerb.Concede, premise = pd.id, what = "conceded " + pd.label }; return true; }
                if (e.TimesAsked(pd.id) == 1 && !string.IsNullOrEmpty(pd.askAgainReply)) { plan = new PilotPlan { verb = PilotVerb.Ask, premise = pd.id, what = "asked " + pd.label + " again" }; return true; }
            }
            return false;
        }

        void Decide(BattleFlow f)
        {
            var e = f.Engine;
            _press = false;
            _wrongCardNow = false;
            if (_human)
            {
                if (!HumanChoose(f, out _plan))
                {
                    // Nothing in front of it: after a few rounds of that, it falls back on the full picture.
                    if (f.Debate.Round - _stuckSince < 3 || !_brain.Choose(e, out _plan)) { if (_stuckSince < 0) _stuckSince = f.Debate.Round; return; }
                }
                _stuckSince = -1;
            }
            else if (!_brain.Choose(e, out _plan)) return;
            _hasPlan = true;
            _seen = 0f;
            _card = _plan.evidence ?? "";
            if (_plan.verb == PilotVerb.Challenge && Has(Mistake.WrongCard))
            {
                // Show something that doesn't touch it (or, failing that, nothing changes).
                foreach (var c in f.Cards)
                {
                    if (c.Length == 0 || c[0] == '@' || c == _plan.evidence || f.BurnedFor(c) > 0) continue;
                    if (e.Def.Premise(_plan.premise)?.ChallengeFor(c) != null) continue;
                    _card = c;
                    _wrongCardNow = true;
                    _plan.what = "showed " + c + " against " + _plan.premise + " (wrong)";
                    break;
                }
            }
            var pressed = e.Def.Premise(_plan.premise ?? "");
            if (Has(Mistake.Press) && pressed != null && !pressed.key && e.CanPress(_plan.premise) && (_plan.verb == PilotVerb.Challenge || _plan.verb == PilotVerb.Concede))
            {
                Take(Mistake.Press);
                _press = true;
                _plan.what = "pressed " + _plan.premise;
            }
            // The card it means to show may be burned: wait for it to come back, arguing something else meanwhile.
            if (_plan.verb == PilotVerb.Challenge && f.BurnedFor(_card) > 0)
            {
                var skip = new List<string> { _plan.premise };
                if (_brain.Choose(e, out var other, skip) && other.verb != PilotVerb.Yield) { _plan = other; _card = other.evidence ?? ""; }
            }
        }

        // ---------- The argue section (the sections presenter) ----------

        int _argueTurn = -1;
        readonly HashSet<string> _slipped = new HashSet<string>();
        bool _hesitating;
        float _argueSince, _quoteSeen;
        string _missingQuote = "";

        void DoArgue(BattleFlow f, float dt, ref BattleInput input)
        {
            var e = f.Engine;
            var map = f.Map;
            if (f.Turn != _argueTurn)
            {
                // A new turn: look at the case as it is now, and decide.
                _argueTurn = f.Turn;
                _argueSince = 0f;
                _hasPlan = false;
            }
            _argueSince += dt;
            if (!_hasPlan)
            {
                _press = false;
                _wrongCardNow = false;
                if (_human && ArgueChoose(f, out _plan)) _hasPlan = true;
                else if (_brain.Choose(e, out _plan)) _hasPlan = true;
                // The card it means may be out of its hand for a turn: argue something else meanwhile.
                if (_hasPlan && _plan.verb == PilotVerb.Challenge && f.BurnedFor(_plan.evidence) > 0)
                {
                    _hasPlan = _brain.Choose(e, out var other, new List<string> { _plan.premise }) && other.verb != PilotVerb.Yield && (other.verb != PilotVerb.Challenge || f.BurnedFor(other.evidence) == 0);
                    if (_hasPlan) _plan = other;
                }
                if (!_hasPlan) return;
                _card = _plan.evidence ?? "";
                PlanMistakes(f);
            }
            int target = map.IndexOf(_plan.premise ?? ArgumentEngine.ClaimId);
            if (target < 0 || !map[target].visible) { _hasPlan = false; return; }

            // A person reads the case first: what's on the table, then the statement they mean, then the card.
            string want = _plan.verb == PilotVerb.Challenge ? _card : _plan.verb == PilotVerb.Rest ? BattleFlow.RestCard : null;
            float think = 0.2f;
            if (_human)
            {
                var pd = e.Def.Premise(_plan.premise ?? "");
                string said = pd != null ? Typewriter.StripTags(pd.text) : Typewriter.StripTags(e.Def.claim);
                // A first-time player: looks over the case, reads the statement (about 11 letters a second), weighs the card.
                think = 2f + said.Length / 11f + (want != null ? 1.6f : 0f);
            }
            if (f.Selected != target)
            {
                // Point at it (a person moves the stick there: a moment per step).
                if (_argueSince < think * 0.5f) return;
                input.hasPoint = true;
                input.point = map[target].position;
                return;
            }
            if (want != null && f.CardInHand != want)
            {
                if (_cycleWait > 0f) { _cycleWait -= dt; return; }
                int at = -1, n = f.Cards.Count;
                for (int k = 0; k < n; k++) if (f.Cards[k] == want) at = k;
                if (at < 0) { _hasPlan = false; return; }
                input.cycle = ((at - f.CardIndex + n) % n) <= n / 2 ? 1 : -1;
                _cycleWait = _human ? 0.9f : 0.05f;
                return;
            }
            if (_argueSince < think) return;
            if (want != null && want.Length > 0 && want[0] != '@' && f.BurnedFor(want) > 0) { _hasPlan = false; return; }
            if (_press) { input.pressHeld = true; return; }
            switch (_plan.verb)
            {
                case PilotVerb.Ask: input.ask = true; break;
                case PilotVerb.Concede: input.concede = true; break;
                case PilotVerb.Yield: input.concede = input.concedeHeld = true; return;
                default: input.fire = true; break;
            }
            _lastAction = _plan.what;
            if (_wrongCardNow) { _wrongCardNow = false; Take(Mistake.WrongCard); }
            _hasPlan = false;
        }

        /// <summary>A first-time player at her case: of what's on the table, the first it has something to do with (ask
        /// what it hasn't heard about, break what a card in hand breaks, concede what's true, ask again where there's
        /// more), and the claim (let it rest) only when nothing can be broken.</summary>
        bool ArgueChoose(BattleFlow f, out PilotPlan plan)
        {
            plan = default;
            var e = f.Engine;
            bool breakable = false;
            foreach (var p in e.VisiblePremises)
            {
                if (e.Status(p.id) != PremiseStatus.Standing || p.unbreakable) continue;
                foreach (var c in p.challenges) if (c != null && e.HasEvidence(c.evidence)) breakable = true;
            }
            foreach (var pd in e.VisiblePremises)
            {
                if (e.Status(pd.id) != PremiseStatus.Standing) continue;
                if (e.TimesAsked(pd.id) == 0) { plan = new PilotPlan { verb = PilotVerb.Ask, premise = pd.id, what = "asked about " + pd.label }; return true; }
                if (!pd.unbreakable)
                    foreach (var c in pd.challenges)
                    {
                        if (c == null || !e.HasEvidence(c.evidence) || f.BurnedFor(c.evidence) > 0) continue;
                        var sound = ArgumentPilot.Sound(c);
                        if (sound == null) continue;
                        plan = new PilotPlan { verb = PilotVerb.Challenge, premise = pd.id, evidence = c.evidence, reason = sound, what = "broke " + pd.label + " with " + c.evidence };
                        return true;
                    }
                if (pd.truth != PremiseTruth.False && !pd.key) { plan = new PilotPlan { verb = PilotVerb.Concede, premise = pd.id, what = "conceded " + pd.label }; return true; }
                if (e.TimesAsked(pd.id) == 1 && !string.IsNullOrEmpty(pd.askAgainReply)) { plan = new PilotPlan { verb = PilotVerb.Ask, premise = pd.id, what = "asked " + pd.label + " again" }; return true; }
            }
            if (!breakable && e.CanLetItRest) { plan = new PilotPlan { verb = PilotVerb.Rest, premise = ArgumentEngine.ClaimId, what = "let it rest" }; return true; }
            return false;
        }

        /// <summary>The scripted slips that apply to the plan just made (a wrong card first, a press instead).</summary>
        void PlanMistakes(BattleFlow f)
        {
            var e = f.Engine;
            // A glance needs flying words: nothing to glance off in the sections.
            if (_plan.verb == PilotVerb.Challenge) Take(Mistake.Glance);
            // A person slips once on a statement, then gets it right (the slips spread over the battle).
            if (_plan.verb == PilotVerb.Challenge && Has(Mistake.WrongCard) && !_slipped.Contains(_plan.premise ?? ""))
            {
                _slipped.Add(_plan.premise ?? "");
                foreach (var c in f.Cards)
                {
                    if (c.Length == 0 || c[0] == '@' || c == _plan.evidence || f.BurnedFor(c) > 0) continue;
                    var pd = e.Def.Premise(_plan.premise);
                    if (pd?.ChallengeFor(c) != null || pd?.AsideFor(c) != null) continue;
                    _card = c;
                    _wrongCardNow = true;
                    _plan.what = "showed " + c + " against " + _plan.premise + " (wrong)";
                    break;
                }
            }
            var pressed = e.Def.Premise(_plan.premise ?? "");
            if (Has(Mistake.Press) && pressed != null && !pressed.key && e.CanPress(_plan.premise) && (_plan.verb == PilotVerb.Challenge || _plan.verb == PilotVerb.Concede))
            {
                Take(Mistake.Press);
                _press = true;
                _plan.what = "pressed " + _plan.premise;
            }
        }

        // ---------- Because ----------

        void DoBecause(BattleFlow f, float dt, ref BattleInput input)
        {
            if (Has(Mistake.Hesitate) && (!f.Sections || !_slipped.Contains(f.BecausePremise) || _hesitating))
            {
                _hesitating = true;
                if (f.Patience <= 0.02f) { Take(Mistake.Hesitate); _hesitating = false; if (f.Sections) _slipped.Add(f.BecausePremise); }
                return;
            }
            // A person reads the cards and thinks (about 20 letters a second, and a moment to decide).
            float think = 0.15f;
            if (_human)
            {
                int letters = 0;
                foreach (var c in f.Because) letters += c.reason.text.Length;
                think = 1.5f + letters / 18f;
            }
            if (f.StateTime < think) return;
            // In the sections a person slips once on a statement, then gets it right.
            bool wrong = (!f.Sections || !_slipped.Contains(f.BecausePremise)) && Take(Mistake.WrongReason);
            if (wrong && f.Sections) _slipped.Add(f.BecausePremise);
            int pick = -1;
            for (int i = 0; i < f.Because.Count; i++)
            {
                var k = f.Because[i].reason.kind;
                if (wrong ? k == ReasonKind.Beside || k == ReasonKind.NonSequitur : k == ReasonKind.Sound) { pick = i; break; }
            }
            if (pick < 0) pick = 0;
            if (_human && f.BecauseFocus != pick)
            {
                input.navY = pick > f.BecauseFocus ? 1 : -1;
                if (wrong) _mistakes.Insert(0, Mistake.WrongReason); // not yet: put it back until the focus arrives
                return;
            }
            input.pick = pick;
            _lastAction = (wrong ? "a reason that misses" : "the sound reason") + " against " + f.BecausePremise;
        }

        // ---------- Pressure ----------

        void DoPressure(BattleFlow f, float dt, ref BattleInput input)
        {
            var phase = f.Engine.Pressure;
            if (phase == null) return;
            if (Has(Mistake.StandStill))
            {
                if (phase.Time > phase.Duration - 0.3f) Take(Mistake.StandStill);
                return;
            }
            // Her hush passes a light that holds still: hold still.
            if (phase.HushOn) { input.move = Vector2.Zero; return; }
            var move = Dodge(phase, Vector2.Zero, dt, phase.Light);
            if ((phase.Spec.calls > 0 || phase.AnswerOn) && move.LengthSquared() < 0.25f)
            {
                // The three rings pass a light that keeps moving: circle.
                _orbit += dt * 1.6f;
                var target = new Vector2((float)Math.Cos(_orbit) * 1.2f - 0.8f, (float)Math.Sin(_orbit) * 0.8f);
                var to = target - phase.Light;
                move = to.LengthSquared() > 1e-4f ? Vector2.Normalize(to) : new Vector2(1f, 0f);
            }
            input.move = move;
        }

        Vector2 Dodge(PressurePhase phase, Vector2 home, float dt, Vector2 light)
        {
            if (phase == null)
            {
                var to = home - light;
                return to.LengthSquared() > 0.01f ? Vector2.Normalize(to) : Vector2.Zero;
            }
            if (phase != _dodging || _dodger == null)
            {
                _dodging = phase;
                if (_dodger == null) _dodger = new PlanningDodger(_planner, PressureRule.Reaction, home);
                _dodger.Reset();
            }
            _dodger.SetHome(home);
            return _dodger.Steer(phase, dt);
        }

        // ---------- The showdown ----------

        void DoShowdown(BattleFlow f, ref BattleInput input)
        {
            _hasPlan = false;
            _press = false;
            var s = f.Showdown;
            if (s == null || !s.InWindow) return;
            float until = s.UntilWindow;
            // Cut near the middle of the window (a person is a little early or late; this is a little early).
            if (until > 0.06f) return;
            int front = s.Front;
            if (_skipWord < 0 && Take(Mistake.MissCut)) _skipWord = front;
            if (front == _skipWord) return;
            input.fire = true;
        }

        int _skipWord = -1;
        int _stuckSince = -1;

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>How long a person takes to read a statement in flight: all of it the first time (about 16 letters a
        /// second while dodging: half a calm reading speed), a glance when it comes round again.</summary>
        static float ReadTime(in Debate.Statement s) => s.repeat ? 1f : 1.2f + s.Plain.Length / 11f;

        static bool s0Seen(BattleFlow f) => true;
    }
}
