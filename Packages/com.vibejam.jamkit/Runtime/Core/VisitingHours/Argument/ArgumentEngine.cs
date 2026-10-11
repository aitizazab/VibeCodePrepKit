using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Argument
{
    public enum PremiseStatus { Hidden, Standing, Broken, Conceded, ConcededFalse, Forced }

    public enum Speaker { Opponent, Nadia, Supporter, Logic }

    /// <summary>One line of the transcript. <see cref="Speaker.Logic"/> lines are the step written as a sentence for a
    /// judge ("You said X. The fourth chair shows Y. So X doesn't hold.").</summary>
    public struct Exchange
    {
        public Speaker speaker;
        public string who;
        public string text;
        public string expression;
    }

    /// <summary>What Nadia brings to a battle: difficulty, the evidence she has, who's with her, earlier results.</summary>
    public sealed class ArgumentSetup
    {
        public ArgumentDifficulty difficulty = ArgumentDifficulty.Normal;
        /// <summary>Evidence ids she has (journal entries). <see cref="allEvidence"/> gives her every card the battle lists.</summary>
        public readonly List<string> evidence = new List<string>();
        public bool allEvidence;
        /// <summary>Supporters present (ids of the battle's SupporterDefs).</summary>
        public readonly List<string> supporters = new List<string>();
        /// <summary>Earlier battles' results: a truth won there counts as evidence here (Lena's L3'' needs Walt's).</summary>
        public ArgumentLedger ledger;
        /// <summary>Accessibility: pressure speed 0.5..1.</summary>
        public float pressureSpeed = 1f;
        public ulong seed = 1;
    }

    /// <summary>What picking a piece of evidence against a premise leads to.</summary>
    public enum ChallengeStep
    {
        /// <summary>Reason cards to choose from.</summary>
        Reasons,
        /// <summary>The evidence doesn't touch it: their line, a little heat.</summary>
        NoTouch,
        /// <summary>The premise is true: their line ("That's a true spoke. Leave it."), a little heat.</summary>
        TrueOne,
        /// <summary>It can't be broken tonight (the lesson).</summary>
        Unbreakable,
        /// <summary>Not now: not standing, not hers, the battle's over or a pressure phase is up.</summary>
        NotAllowed,
    }

    /// <summary>A reason card on screen (with the supporter who offered it, if any).</summary>
    public struct ReasonCard
    {
        public ReasonDef reason;
        public string supporter;
    }

    /// <summary>
    /// The rules of an argument battle, concept A "Take it apart" (docs/ARGUMENT-BATTLES.md section 3; the prototype:
    /// docs/ARGUMENT-PROTOTYPE.md). Verbs: Ask, Challenge (evidence, then a reason card), Concede, Press, Let it rest,
    /// Retort (the creator's antagonising reply), and conceding the claim. Heat (0..3) rises with unsound challenges
    /// (+1), presses (+2) and antagonising (+2) and falls with concessions and sound challenges; anger (0..3) rises
    /// only by antagonising and hardens every later pressure phase. Composure is Nadia's heart rate, 70..160 bpm in
    /// five steps: each hit is a step, conceding a true premise calms a step, 160 ends the battle. Pressure phases
    /// come only as consequences (the opening, an unsound reason, a press, a retort, a painful reveal), except in
    /// the Long Night's setting, where every exchange brings one. Ends with an outcome, a stance that later battles
    /// read (<see cref="Record"/>, kept in <see cref="ArgumentLedger"/>), and a transcript.
    /// </summary>
    public sealed class ArgumentEngine
    {
        public const string ClaimId = "claim";
        public static readonly int[] HeartRates = { 70, 88, 106, 124, 142, 160 };
        public const int PanicStep = 5;
        public const int ScriptedStartStep = 3;
        public const int MaxHeat = 3;
        public const int MaxAnger = 3;

        readonly ArgumentDef _def;
        readonly ArgumentSetup _setup;
        readonly Dictionary<string, PremiseStatus> _status = new Dictionary<string, PremiseStatus>();
        readonly Dictionary<string, int> _asked = new Dictionary<string, int>();
        readonly HashSet<string> _heard = new HashSet<string>();
        readonly List<Exchange> _transcript = new List<Exchange>(64);
        readonly List<PremiseDef> _visible = new List<PremiseDef>(16);
        readonly List<SlipSeed> _slipBuffer = new List<SlipSeed>(2);
        int _heat, _anger, _heart, _presses, _keysBroken, _phases;
        bool _started, _ended;
        ArgumentOutcome _outcome;
        ArgumentStance _stance;
        PressurePhase _pressure;
        bool _pending, _pendingPainful;
        int _pendingLevel;
        ulong _phaseSeed;

        public ArgumentEngine(ArgumentDef def, ArgumentSetup setup)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _setup = setup ?? new ArgumentSetup();
            _phaseSeed = _setup.seed == 0 ? 1UL : _setup.seed;
            foreach (var p in _def.premises)
                if (p != null && !string.IsNullOrEmpty(p.id)) _status[p.id] = p.hidden ? PremiseStatus.Hidden : PremiseStatus.Standing;
            _heart = _setup.difficulty == ArgumentDifficulty.ScriptedButBeatable ? ScriptedStartStep : 0;
        }

        // ---------- State ----------

        public ArgumentDef Def => _def;
        public ArgumentSetup Setup => _setup;
        public int Heat => _heat;
        public int Anger => _anger;
        public int HeartStep => _heart;
        public int HeartRate => HeartRates[Math.Min(PanicStep, Math.Max(0, _heart))];
        public int Presses => _presses;
        public int KeysBroken => _keysBroken;
        public int PhasesRun => _phases;
        public bool Started => _started;
        public bool Ended => _ended;
        public ArgumentOutcome Outcome => _outcome;
        public ArgumentStance Stance => _stance;
        public IReadOnlyList<Exchange> Transcript => _transcript;
        /// <summary>The phase being played, or null.</summary>
        public PressurePhase Pressure => _pressure;
        /// <summary>A phase is playing or about to: no verbs until it's over.</summary>
        public bool InPressure => _pressure != null || _pending;

        public PremiseStatus Status(string id) => id != null && _status.TryGetValue(id, out var s) ? s : PremiseStatus.Hidden;
        public bool IsHeard(string id) => _heard.Contains(id);
        public int TimesAsked(string id) => _asked.TryGetValue(id, out int n) ? n : 0;

        /// <summary>The premises on the table (revealed), in the battle's order. Rebuilt on each call (UI refresh only).</summary>
        public IReadOnlyList<PremiseDef> VisiblePremises
        {
            get
            {
                _visible.Clear();
                foreach (var p in _def.premises)
                    if (p != null && Status(p.id) != PremiseStatus.Hidden) _visible.Add(p);
                return _visible;
            }
        }

        /// <summary>Key premises still to break (Convinced needs them all).</summary>
        public int KeysLeft
        {
            get
            {
                int n = 0;
                foreach (var p in _def.premises)
                    if (p != null && p.key && Status(p.id) != PremiseStatus.Broken) n++;
                return n;
            }
        }

        bool Open => _started && !_ended && !InPressure;

        // ---------- Evidence ----------

        public bool HasEvidence(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            // A supporter's card is in the tray only while they're here.
            foreach (var s in _def.supporters)
                if (s != null && s.card != null && s.card.id == id) return SupporterHere(s);
            if (_def.Evidence(id) == null) return false;
            if (_setup.allEvidence || _setup.evidence.Contains(id)) return true;
            return _setup.ledger != null && _setup.ledger.HasTruth(id);
        }

        /// <summary>The tray: the battle's evidence Nadia has, then supporters' cards.</summary>
        public List<EvidenceDef> Tray()
        {
            var list = new List<EvidenceDef>();
            foreach (var e in _def.evidence)
                if (e != null && HasEvidence(e.id)) list.Add(e);
            foreach (var s in _def.supporters)
                if (s != null && s.card != null && SupporterHere(s) && !list.Contains(s.card)) list.Add(s.card);
            return list;
        }

        bool SupporterHere(SupporterDef s) => s != null && _setup.supporters.Contains(s.id);

        // ---------- Start ----------

        /// <summary>The claim, the opening line, the supporters' openers, and the opening pressure phase (low).</summary>
        public void Start()
        {
            if (_started) return;
            _started = true;
            Say(Speaker.Opponent, _def.opponent, _def.opening, "neutral");
            foreach (var s in _def.supporters)
                if (SupporterHere(s) && string.IsNullOrEmpty(s.premise) && !string.IsNullOrEmpty(s.line)) Say(Speaker.Supporter, s.speaker, s.line, "");
            foreach (var p in _def.premises)
                if (p != null && Status(p.id) == PremiseStatus.Standing) SupporterLines(p.id);
            Queue(0, false);
        }

        // ---------- Verbs ----------

        public bool CanAsk(string id) => Open && Standing(id) || Open && Status(id) == PremiseStatus.Conceded;

        /// <summary>"Why?" They explain; the first time may reveal a hidden premise beneath, the second time the deepest.</summary>
        public bool Ask(string id)
        {
            if (!CanAsk(id)) return false;
            var p = _def.Premise(id);
            int n = TimesAsked(id) + 1;
            _asked[id] = n;
            Say(Speaker.Nadia, "Nadia", n == 1 ? "Why do you say that?" : "...And under that?", "");
            if (n >= 2 && (p.askAgainReveals.Count > 0 || !string.IsNullOrEmpty(p.askAgainReply)))
            {
                Say(Speaker.Opponent, _def.opponent, Or(p.askAgainReply, p.askReply), "");
                foreach (var r in p.askAgainReveals) Reveal(r, false);
            }
            else
            {
                Say(Speaker.Opponent, _def.opponent, Or(p.askReply, p.text), "");
                if (n == 1) foreach (var r in p.askReveals) Reveal(r, false);
            }
            _heard.Add(id);
            AfterExchange(false);
            return true;
        }

        public bool CanChallenge(string id) => Open && Standing(id);

        /// <summary>Nadia puts <paramref name="evidenceId"/> against a premise. Fills <paramref name="cards"/> when there's
        /// a reason to choose (shuffled, the same order every time for this pair); otherwise the opponent answers at
        /// once and the step says why.</summary>
        public ChallengeStep BeginChallenge(string id, string evidenceId, List<ReasonCard> cards)
        {
            cards?.Clear();
            if (!CanChallenge(id) || !HasEvidence(evidenceId)) return ChallengeStep.NotAllowed;
            var p = _def.Premise(id);
            var ev = _def.Evidence(evidenceId);
            string shown = ev != null ? ev.name : evidenceId;
            if (p.unbreakable)
            {
                Say(Speaker.Nadia, "Nadia", "(shows " + shown + ") " + Quote(p.text) + "? Not with this.", "");
                Say(Speaker.Opponent, _def.opponent, Or(p.unbreakableReply, _def.noTouchReply), "");
                Logic("Nothing Nadia has answers " + Quote(p.text) + " tonight.");
                _heard.Add(id);
                AfterExchange(false);
                return ChallengeStep.Unbreakable;
            }
            var c = p.ChallengeFor(evidenceId);
            if (c == null && p.truth != PremiseTruth.False)
            {
                Say(Speaker.Nadia, "Nadia", "(shows " + shown + ") That isn't true.", "");
                Say(Speaker.Opponent, _def.opponent, Or(p.trueReply, _def.noTouchReply), "");
                Logic(Quote(p.text) + " is true: it can be conceded, not broken.");
                RaiseHeat(1);
                _heard.Add(id);
                AfterExchange(false);
                return ChallengeStep.TrueOne;
            }
            if (c == null)
            {
                Say(Speaker.Nadia, "Nadia", "(shows " + shown + ")", "");
                Say(Speaker.Opponent, _def.opponent, Or(p.noTouchReply, _def.noTouchReply), "");
                Logic(shown + " doesn't touch " + Quote(p.text) + ".");
                RaiseHeat(1);
                _heard.Add(id);
                AfterExchange(false);
                return ChallengeStep.NoTouch;
            }
            if (cards != null)
            {
                foreach (var r in c.reasons) if (r != null) cards.Add(new ReasonCard { reason = r });
                foreach (var s in _def.supporters)
                    if (SupporterHere(s) && s.reason != null && s.reasonPremise == id && s.reasonEvidence == evidenceId)
                        cards.Add(new ReasonCard { reason = s.reason, supporter = s.speaker });
                Shuffle(cards, id + "|" + evidenceId);
            }
            return ChallengeStep.Reasons;
        }

        /// <summary>Nadia states her reason. Sound: the premise comes out (or they fall back / deepen), heat falls.
        /// Beside the point or not following: they say why, heat rises, a pressure phase follows. Antagonising: anger.</summary>
        public bool ResolveChallenge(string id, string evidenceId, ReasonDef reason)
        {
            if (!CanChallenge(id) || reason == null || !HasEvidence(evidenceId)) return false;
            var p = _def.Premise(id);
            var c = p.ChallengeFor(evidenceId);
            if (c == null || p.unbreakable) return false;
            var ev = _def.Evidence(evidenceId);
            string shown = ev != null ? ev.name : evidenceId;
            Say(Speaker.Nadia, "Nadia", "(" + shown + ") " + reason.text, "");
            Say(Speaker.Opponent, _def.opponent, reason.reply, reason.expression);
            _heard.Add(id);
            switch (reason.kind)
            {
                case ReasonKind.Sound:
                    Logic("You said " + Quote(p.text) + ". " + Cap(shown) + " shows otherwise: " + reason.text + " So " + Quote(p.text) + " doesn't hold.");
                    Set(id, PremiseStatus.Broken);
                    if (p.key) _keysBroken++;
                    LowerHeat(1);
                    if ((c.effect == SoundEffect.FallBack || c.effect == SoundEffect.Deepen) && !string.IsNullOrEmpty(c.effectTarget))
                        Reveal(c.effectTarget, false);
                    AfterExchange(false);
                    break;
                case ReasonKind.Beside:
                    Logic(Quote(reason.text) + " may be true, but it doesn't touch " + Quote(p.text) + ".");
                    RaiseHeat(1);
                    AfterExchange(true);
                    break;
                case ReasonKind.Antagonise:
                    Logic("That dodged " + Quote(p.text) + " and went for " + _def.opponent + " instead. " + _def.opponent + " is angrier.");
                    Antagonise();
                    AfterExchange(true);
                    break;
                default:
                    Logic(Quote(reason.text) + " doesn't follow from " + shown + ".");
                    RaiseHeat(1);
                    AfterExchange(true);
                    break;
            }
            return true;
        }

        public bool CanRetort(string id) => Open && Standing(id) && _def.Premise(id)?.retort != null;

        /// <summary>The antagonising reply to their question: it dodges what they asked and attacks them. Anger +1,
        /// heat +2, and a pressure phase that's noticeably harder (faster, denser, fewer and narrower gaps).</summary>
        public bool Retort(string id)
        {
            if (!CanRetort(id)) return false;
            var p = _def.Premise(id);
            Say(Speaker.Nadia, "Nadia", p.retort.line, "");
            Say(Speaker.Opponent, _def.opponent, p.retort.reply, p.retort.expression);
            Logic("That dodged " + (string.IsNullOrEmpty(p.question) ? Quote(p.text) : "the question " + Quote(p.question)) + " and attacked " + _def.opponent + ". " + _def.opponent + " is angrier.");
            _heard.Add(id);
            Antagonise();
            AfterExchange(true);
            return true;
        }

        public bool CanConcede(string id) => Open && Standing(id);

        /// <summary>"That's true." A true (or half-true) premise glows: heart rate down a step, heat down, and they say
        /// what they hang on it. Conceding a false one closes that line (a misplay).</summary>
        public bool Concede(string id)
        {
            if (!CanConcede(id)) return false;
            var p = _def.Premise(id);
            Say(Speaker.Nadia, "Nadia", Or(p.concedeLine, "That's true."), "");
            _heard.Add(id);
            if (p.truth == PremiseTruth.False)
            {
                Set(id, PremiseStatus.ConcededFalse);
                Say(Speaker.Opponent, _def.opponent, Or(p.concedeFalseReply, "Then we agree."), "");
                Logic("Nadia agreed with " + Quote(p.text) + ", which isn't true: that line is closed.");
                AfterExchange(false);
                return true;
            }
            Set(id, PremiseStatus.Conceded);
            Say(Speaker.Opponent, _def.opponent, Or(p.concedeReply, "...Thank you."), "softened");
            Logic("Nadia conceded " + Quote(p.text) + ": it's true. Her heart slows.");
            if (_heart > 0) _heart--;
            LowerHeat(1);
            foreach (var r in p.concedeReveals) Reveal(r, false);
            AfterExchange(false);
            return true;
        }

        public bool CanPress(string id) => Open && (id == ClaimId || Standing(id));

        /// <summary>Force it down without evidence (a 0.6 s hold in the view). The piece breaks "forced", Force +1, heat
        /// +2 and a harsh phase. Pressing the claim ends the battle.</summary>
        public bool Press(string id)
        {
            if (!CanPress(id)) return false;
            _presses++;
            if (id == ClaimId)
            {
                Say(Speaker.Nadia, "Nadia", Or(_def.pressClaimLine, "Enough. You're wrong."), "");
                Say(Speaker.Opponent, _def.opponent, Or(_def.pressClaimReply, "..."), "hurt");
                Logic("Nadia forced the claim itself.");
                Finish(ArgumentOutcome.Pressed);
                return true;
            }
            var p = _def.Premise(id);
            Say(Speaker.Nadia, "Nadia", Or(p.pressLine, "No. It isn't."), "");
            Say(Speaker.Opponent, _def.opponent, Or(p.pressReply, "..."), "hurt");
            Logic("Nadia forced " + Quote(p.text) + " down without a reason.");
            Set(id, PremiseStatus.Forced);
            _heard.Add(id);
            RaiseHeat(2);
            AfterExchange(true);
            return true;
        }

        /// <summary>Every premise, hidden ones included, has been revealed and heard.</summary>
        public bool CanLetItRest
        {
            get
            {
                if (!Open) return false;
                foreach (var p in _def.premises)
                {
                    if (p == null) continue;
                    if (Status(p.id) == PremiseStatus.Hidden) return false;
                    if (!_heard.Contains(p.id)) return false;
                }
                return true;
            }
        }

        /// <summary>"I hear you." Heard out (or Pressed, if she forced anything).</summary>
        public bool LetItRest()
        {
            if (!CanLetItRest) return false;
            Say(Speaker.Nadia, "Nadia", _def.letItRestLine, "");
            if (!string.IsNullOrEmpty(_def.letItRestReply)) Say(Speaker.Opponent, _def.opponent, _def.letItRestReply, "softened");
            Logic("Nadia heard every premise out and let it rest.");
            Finish(_presses > 0 ? ArgumentOutcome.Pressed : ArgumentOutcome.HeardOut);
            return true;
        }

        public bool CanConcedeClaim => Open;

        /// <summary>Nadia gives up the argument itself (Yielded).</summary>
        public bool ConcedeClaim()
        {
            if (!CanConcedeClaim) return false;
            Say(Speaker.Nadia, "Nadia", Or(_def.concedeClaimLine, "...You're right."), "");
            Say(Speaker.Opponent, _def.opponent, Or(_def.concedeClaimReply, "..."), "");
            Logic("Nadia conceded the claim: " + Quote(_def.claim));
            Finish(ArgumentOutcome.Yielded);
            return true;
        }

        /// <summary>Ends it now (debug skip, tests).</summary>
        public void ForceOutcome(ArgumentOutcome outcome)
        {
            if (_ended || outcome == ArgumentOutcome.None) return;
            _pressure = null;
            _pending = false;
            Finish(outcome);
        }

        // ---------- Pressure ----------

        /// <summary>Plays the pressure phase (if one is up): <paramref name="move"/> moves Nadia's light (-1..1).
        /// Hits raise her heart rate; a caught slip reveals its premise. Starts a queued phase first.</summary>
        public void Tick(float dt, Vector2 move)
        {
            if (_ended) return;
            if (_pressure == null && _pending) BeginPhase();
            if (_pressure == null) return;
            _pressure.Tick(dt, move);
            int hits = _pressure.TakeHits();
            for (int i = 0; i < hits && !_ended; i++)
            {
                _heart++;
                if (_heart >= PanicStep)
                {
                    _heart = PanicStep;
                    Say(Speaker.Nadia, "Nadia", Or(_def.composureLostLine, "...I can't breathe."), "");
                    Logic("Nadia's heart reached " + HeartRates[PanicStep] + " bpm.");
                    _pressure = null;
                    Finish(ArgumentOutcome.ComposureLost);
                    return;
                }
            }
            string slip = _pressure.TakeCaughtSlip();
            if (slip != null && Status(slip) == PremiseStatus.Hidden)
            {
                var p = _def.Premise(slip);
                Logic("Nadia caught " + Or(p?.slipName, "something") + ": what " + _def.opponent + " isn't saying.");
                Reveal(slip, true);
            }
            if (_pressure != null && _pressure.Finished)
            {
                _pressure = null;
                CheckEnd();
            }
        }

        /// <summary>Ends the current phase at once (debug skip; the view's "skip" in Off mode).</summary>
        public void SkipPressure()
        {
            _pending = false;
            _pressure = null;
            CheckEnd();
        }

        /// <summary>The spec the next (or current) phase plays with.</summary>
        public PressureSpec SpecFor(int level, bool painful) =>
            PressureSpec.For(level, _anger, _setup.difficulty, _setup.pressureSpeed, painful, level >= 2 || painful ? _def.hotShapes : _def.calmShapes);

        void Queue(int level, bool painful)
        {
            if (_ended) return;
            if (_pending)
            {
                _pendingLevel = Math.Max(_pendingLevel, level);
                _pendingPainful |= painful;
                return;
            }
            _pending = true;
            _pendingLevel = level;
            _pendingPainful = painful;
        }

        void BeginPhase()
        {
            _pending = false;
            _slipBuffer.Clear();
            foreach (var p in _def.premises)
                if (p != null && p.revealedBySlip && Status(p.id) == PremiseStatus.Hidden && _slipBuffer.Count < 2)
                    _slipBuffer.Add(new SlipSeed { premise = p.id, name = p.slipName });
            _phaseSeed = _phaseSeed * 6364136223846793005UL + 1442695040888963407UL;
            _pressure = new PressurePhase(SpecFor(_pendingLevel, _pendingPainful), _phaseSeed, _slipBuffer);
            _phases++;
        }

        // ---------- Rules ----------

        void AfterExchange(bool consequence)
        {
            CheckSurfacing();
            if (CheckEnd()) return;
            if (consequence) Queue(_heat, false);
            else if (_setup.difficulty == ArgumentDifficulty.ScriptedButBeatable) Queue(MaxHeat, false);
        }

        void Antagonise()
        {
            _anger = Math.Min(MaxAnger, _anger + 1);
            RaiseHeat(2);
        }

        void RaiseHeat(int n) => _heat = Math.Min(MaxHeat, _heat + n);
        void LowerHeat(int n) => _heat = Math.Max(0, _heat - n);

        /// <summary>Every key premise broken or forced ends it: Convinced if nothing was forced.</summary>
        bool CheckEnd()
        {
            if (_ended) return true;
            if (!_started) return false;
            bool anyKey = false;
            foreach (var p in _def.premises)
            {
                if (p == null || !p.key) continue;
                anyKey = true;
                var s = Status(p.id);
                if (s != PremiseStatus.Broken && s != PremiseStatus.Forced) return false;
            }
            if (!anyKey) return false;
            // A pressure phase already playing finishes first; a queued one is dropped (the wheel trues).
            if (_pressure != null) return false;
            _pending = false;
            Finish(_presses > 0 ? ArgumentOutcome.Pressed : ArgumentOutcome.Convinced);
            return true;
        }

        void CheckSurfacing()
        {
            foreach (var p in _def.premises)
            {
                if (p == null || Status(p.id) != PremiseStatus.Hidden || p.surfacesAfter.Count == 0) continue;
                foreach (var after in p.surfacesAfter)
                {
                    var s = Status(after);
                    if (s != PremiseStatus.Broken && s != PremiseStatus.Conceded && s != PremiseStatus.ConcededFalse && s != PremiseStatus.Forced) continue;
                    Reveal(p.id, true);
                    if (p.painful) Queue(MaxHeat, true);
                    break;
                }
            }
        }

        void Reveal(string id, bool sayIt)
        {
            if (Status(id) != PremiseStatus.Hidden || _def.Premise(id) == null) return;
            var p = _def.Premise(id);
            Set(id, PremiseStatus.Standing);
            if (sayIt) Say(Speaker.Opponent, _def.opponent, p.text, p.painful ? "hurt" : "");
            if (!string.IsNullOrEmpty(p.question)) Say(Speaker.Opponent, _def.opponent, p.question, "");
            SupporterLines(id);
        }

        void SupporterLines(string premise)
        {
            foreach (var s in _def.supporters)
                if (SupporterHere(s) && s.premise == premise && !string.IsNullOrEmpty(s.line)) Say(Speaker.Supporter, s.speaker, s.line, "");
        }

        void Finish(ArgumentOutcome outcome)
        {
            if (_ended) return;
            _ended = true;
            _outcome = outcome;
            _pending = false;
            _stance = StanceFor(outcome, _anger, _presses);
            switch (outcome)
            {
                case ArgumentOutcome.Convinced:
                    if (!string.IsNullOrEmpty(_def.convincedReply)) Say(Speaker.Opponent, _def.opponent, _def.convincedReply, "softened");
                    if (!string.IsNullOrEmpty(_def.truthText)) Say(Speaker.Opponent, _def.opponent, _def.truthText, "softened");
                    break;
                case ArgumentOutcome.HeardOut:
                    if (!string.IsNullOrEmpty(_def.heardOutReply)) Say(Speaker.Opponent, _def.opponent, _def.heardOutReply, "softened");
                    break;
                case ArgumentOutcome.Pressed:
                    if (!string.IsNullOrEmpty(_def.pressedReply)) Say(Speaker.Opponent, _def.opponent, _def.pressedReply, "hurt");
                    break;
            }
            Logic("Outcome: " + outcome + ". " + _def.opponent + " is " + StanceWords(_stance) + ".");
        }

        /// <summary>
        /// Where they stand afterwards. Antagonising pushes towards Condemns: three retorts always condemn; two keep
        /// even a Convinced opponent only Unsure. Forcing hurts: a press with any anger, or two presses, condemns.
        /// </summary>
        public static ArgumentStance StanceFor(ArgumentOutcome outcome, int anger, int presses)
        {
            if (anger >= MaxAnger) return ArgumentStance.Condemns;
            switch (outcome)
            {
                case ArgumentOutcome.Convinced: return anger >= 2 ? ArgumentStance.Unsure : ArgumentStance.Amenable;
                case ArgumentOutcome.HeardOut: return anger == 0 ? ArgumentStance.Amenable : ArgumentStance.Unsure;
                case ArgumentOutcome.Pressed: return presses >= 2 || anger >= 1 ? ArgumentStance.Condemns : ArgumentStance.Unsure;
                case ArgumentOutcome.ComposureLost: return anger >= 2 ? ArgumentStance.Condemns : ArgumentStance.Unsure;
                case ArgumentOutcome.Yielded: return anger >= 2 ? ArgumentStance.Condemns : ArgumentStance.Unsure;
                default: return ArgumentStance.None;
            }
        }

        public static string StanceWords(ArgumentStance s)
        {
            switch (s)
            {
                case ArgumentStance.Amenable: return "amenable";
                case ArgumentStance.Unsure: return "unsure";
                case ArgumentStance.Condemns: return "condemning";
                default: return "unmoved";
            }
        }

        /// <summary>The result for the ledger (valid once <see cref="Ended"/>).</summary>
        public ArgumentRecord Record() => new ArgumentRecord
        {
            id = _def.id,
            outcome = _outcome,
            stance = _stance,
            anger = _anger,
            presses = _presses,
            truth = _outcome == ArgumentOutcome.Convinced ? _def.truthId ?? "" : "",
            personal = _outcome == ArgumentOutcome.HeardOut ? _def.personalId ?? "" : "",
            keysBroken = _keysBroken,
            difficulty = _setup.difficulty,
        };

        // ---------- Helpers ----------

        bool Standing(string id) => Status(id) == PremiseStatus.Standing && _def.Premise(id) != null;

        void Set(string id, PremiseStatus s) => _status[id] = s;

        void Say(Speaker speaker, string who, string text, string expression)
        {
            if (string.IsNullOrEmpty(text)) return;
            _transcript.Add(new Exchange { speaker = speaker, who = who ?? "", text = text, expression = expression ?? "" });
        }

        void Logic(string text) => Say(Speaker.Logic, "", text, "");

        void Shuffle(List<ReasonCard> cards, string key)
        {
            // The same order for the same premise and evidence every time (so a retry shows the same cards).
            ulong h = 1469598103934665603UL ^ _setup.seed;
            foreach (char ch in key) h = (h ^ ch) * 1099511628211UL;
            var rng = new Rng(h == 0 ? 1UL : h);
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                var t = cards[i];
                cards[i] = cards[j];
                cards[j] = t;
            }
        }

        static string Or(string a, string b) => string.IsNullOrEmpty(a) ? b ?? "" : a;
        static string Quote(string s) => "“" + (s ?? "") + "”";
        static string Cap(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
