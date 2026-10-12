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
        /// <summary>The game-wide Force tally before this battle (conditional lines read it; this battle's presses add to it).</summary>
        public int force;
        /// <summary>The hidden Despair value, 0..3 (3 = max), for conditional lines.</summary>
        public int despair;
        /// <summary>Where their anger starts (the day's bedside, docs/redesign/day.md): +1 starts it a step higher (Day 2's
        /// "Lena": she waits at the window); -1 is a step gentler (Day 2's "just sit": the first thing that would anger her
        /// doesn't, and her pressure in the debate is sparser). 0: as written.</summary>
        public int angerStart;
        /// <summary>Nadia has already said sorry out loud at his bedside (Day 1's "the phone"): the painful premise ("He
        /// called you first") is on the table from the start, and her admission reads that way (the <c>sorry</c> meter).</summary>
        public bool admitted;
        /// <summary>The sections presenter is playing it (arguing and dodging take turns; docs/redesign/battle.md, round 2):
        /// the coach's first lesson is the argue section, and lines can read it (the <c>sections</c> meter).</summary>
        public bool sections;
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
        /// <summary>The card breaks nothing here, but it gets an answer of its own (<see cref="AsideDef"/>); no cost.</summary>
        Aside,
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
        readonly HashSet<string> _retorted = new HashSet<string>();
        readonly List<Exchange> _transcript = new List<Exchange>(64);
        readonly List<PremiseDef> _visible = new List<PremiseDef>(16);
        readonly List<SlipSeed> _slipBuffer = new List<SlipSeed>(2);
        int _heat, _anger, _heart, _presses, _keysBroken, _phases, _antagonised;
        readonly HashSet<string> _coached = new HashSet<string>();
        bool _coachedThisTurn;
        bool _started, _ended;
        ArgumentOutcome _outcome;
        ArgumentStance _stance;
        PressurePhase _pressure;
        bool _pending, _pendingPainful;
        int _pendingLevel;
        float _pendingSeconds; // a gesture's length; 0: a full phase
        int _pendingHush, _pendingAnswer;
        ulong _phaseSeed;
        // The sections presenter: her own words caught, asides heard, her sky, her notes, what's been taught.
        readonly HashSet<string> _quotes = new HashSet<string>();
        int _asides, _fullPhases;
        bool _sky, _hushTaught, _answerTaught;
        Rails _skyRails;

        public ArgumentEngine(ArgumentDef def, ArgumentSetup setup)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _setup = setup ?? new ArgumentSetup();
            _phaseSeed = _setup.seed == 0 ? 1UL : _setup.seed;
            foreach (var p in _def.premises)
                if (p != null && !string.IsNullOrEmpty(p.id)) _status[p.id] = p.hidden ? PremiseStatus.Hidden : PremiseStatus.Standing;
            _heart = _setup.difficulty == ArgumentDifficulty.ScriptedButBeatable ? ScriptedStartStep : 0;
            _anger = Math.Max(0, Math.Min(MaxAnger, _setup.angerStart));
            _angerGrace = Math.Max(0, -_setup.angerStart);
        }

        int _angerGrace;
        readonly HashSet<string> _painfulPlayed = new HashSet<string>();

        /// <summary>Their anger rises a step (antagonising, a retort, a press), unless the day left them gentler: the
        /// first rise is absorbed.</summary>
        void RaiseAnger()
        {
            if (_angerGrace > 0) { _angerGrace--; return; }
            _anger = Math.Min(MaxAnger, _anger + 1);
        }

        // ---------- State ----------

        public ArgumentDef Def => _def;
        public ArgumentSetup Setup => _setup;
        public int Heat => _heat;
        public int Anger => _anger;
        public int HeartStep => _heart;
        public int HeartRate => HeartRates[Math.Min(PanicStep, Math.Max(0, _heart))];
        public int Presses => _presses;
        /// <summary>Antagonising cards and retorts that landed this battle.</summary>
        public int Antagonised => _antagonised;

        /// <summary>The numbers conditional lines read (<see cref="ArgumentCondition"/>).</summary>
        public ArgumentMeters Meters => new ArgumentMeters
        {
            anger = _anger, force = _setup.force + _presses, despair = _setup.despair, presses = _presses,
            antagonised = _antagonised, heat = _heat, sorry = _setup.admitted ? 1 : 0,
            asides = _asides, sections = _setup.sections ? 1 : 0,
        };

        /// <summary>The line to say for <paramref name="id"/>: the first conditional variant that holds now, else
        /// <paramref name="text"/>.</summary>
        public string Line(string id, string text)
        {
            var variants = _def.variants;
            if (variants.Count == 0 || string.IsNullOrEmpty(id)) return text;
            var m = Meters;
            for (int i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                if (v.parsed != null && v.id == id && v.parsed.Holds(m)) return v.text;
            }
            return text;
        }

        string L(PremiseDef p, string field, string text) => Line(p.id + "." + field, text);
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

        /// <summary>Every key premise is down, but their painful one stands, unanswered: the battle waits for Nadia to face
        /// it (docs/redesign/battle.md, "The last stand"). A retort counts as answering (badly).</summary>
        public bool LastStand
        {
            get
            {
                if (_ended) return false;
                bool anyKey = false;
                foreach (var p in _def.premises)
                {
                    if (p == null || !p.key) continue;
                    anyKey = true;
                    var s = Status(p.id);
                    if (s != PremiseStatus.Broken && s != PremiseStatus.Forced) return false;
                }
                if (!anyKey) return false;
                foreach (var p in _def.premises)
                    if (p != null && p.painful && Status(p.id) == PremiseStatus.Standing && !_retorted.Contains(p.id)) return true;
                return false;
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
            var card = _def.Evidence(id);
            if (card == null) return false;
            // Her own words are in Nadia's hand only once she's caught them, whatever the setup gives.
            if (card.IsQuote) return _quotes.Contains(id);
            if (_setup.allEvidence || card.UnlockedBy(_setup.evidence)) return true;
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
            // No mood on the opening: their opening pose shows (ArgumentArt.Opening) until a reply changes it.
            Say(Speaker.Opponent, _def.opponent, Line("opening", _def.opening), "");
            foreach (var s in _def.supporters)
                if (SupporterHere(s) && string.IsNullOrEmpty(s.premise) && !string.IsNullOrEmpty(s.line)) Say(Speaker.Supporter, s.speaker, s.line, "");
            foreach (var p in _def.premises)
                if (p != null && Status(p.id) == PremiseStatus.Standing) SupporterLines(p.id);
            // The tutorial's first lesson is the debate itself (move, keep clear); asking comes when the first statement
            // flies (the battle flow coaches it then). A battle played without the flow still hears both.
            if (!Coach(_setup.sections ? ArgumentCoach.Argue : ArgumentCoach.Debate)) Coach(ArgumentCoach.Ask);
            // Already said sorry at his bedside: what hurts most is on the table from the start (its phase still comes
            // when the night would have brought it out).
            if (_setup.admitted)
                foreach (var p in _def.premises)
                    if (p != null && p.painful && Status(p.id) == PremiseStatus.Hidden) Reveal(p.id, false);
            if (_def.openingPressure) Queue(0, false);
            _coachedThisTurn = false;
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
            Say(Speaker.Nadia, "Nadia", n == 1 ? S["nadia.ask"] : S["nadia.askAgain"], "");
            if (n >= 2 && (p.askAgainReveals.Count > 0 || !string.IsNullOrEmpty(p.askAgainReply)))
            {
                Say(Speaker.Opponent, _def.opponent, Or(L(p, "askAgainReply", p.askAgainReply), L(p, "askReply", p.askReply)), "");
                foreach (var r in p.askAgainReveals) Reveal(r, false);
            }
            else
            {
                Say(Speaker.Opponent, _def.opponent, Or(L(p, "askReply", p.askReply), p.text), "");
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
            var aside = p.AsideFor(evidenceId);
            if (aside != null)
            {
                // Nothing breaks, nothing costs: she just has an answer (or hasn't).
                Say(Speaker.Nadia, "Nadia", Or(Line(id + "." + evidenceId + ".aside", aside.line), S.Format("nadia.showNoTouch", "evidence", shown)), "");
                Say(Speaker.Opponent, _def.opponent, Line(id + "." + evidenceId + ".aside.reply", aside.reply), aside.expression);
                Logic(S.Format("logic.aside", "evidence", shown, "Evidence", Cap(shown), "premise", p.text, "opponent", _def.opponent));
                _asides++;
                _heard.Add(id);
                AfterExchange(false);
                return ChallengeStep.Aside;
            }
            if (p.unbreakable)
            {
                Say(Speaker.Nadia, "Nadia", S.Format("nadia.showUnbreakable", "evidence", shown, "premise", p.text), "");
                Say(Speaker.Opponent, _def.opponent, Or(L(p, "unbreakableReply", p.unbreakableReply), Line("noTouchReply", _def.noTouchReply)), "");
                Logic(S.Format("logic.unbreakable", "premise", p.text));
                _heard.Add(id);
                AfterExchange(false);
                return ChallengeStep.Unbreakable;
            }
            var c = p.ChallengeFor(evidenceId);
            if (c == null && p.truth != PremiseTruth.False)
            {
                Say(Speaker.Nadia, "Nadia", S.Format("nadia.showTrue", "evidence", shown, "premise", p.text), "");
                Say(Speaker.Opponent, _def.opponent, Or(L(p, "trueReply", p.trueReply), Line("noTouchReply", _def.noTouchReply)), "");
                Logic(S.Format("logic.true", "premise", p.text));
                RaiseHeat(1);
                CoachMisstep();
                _heard.Add(id);
                AfterExchange(false);
                return ChallengeStep.TrueOne;
            }
            if (c == null)
            {
                Say(Speaker.Nadia, "Nadia", S.Format("nadia.showNoTouch", "evidence", shown, "premise", p.text), "");
                Say(Speaker.Opponent, _def.opponent, Or(L(p, "noTouchReply", p.noTouchReply), Line("noTouchReply", _def.noTouchReply)), "");
                Logic(S.Format("logic.noTouch", "evidence", shown, "Evidence", Cap(shown), "premise", p.text));
                RaiseHeat(1);
                CoachMisstep();
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
            Say(Speaker.Nadia, "Nadia", S.Format("nadia.reason", "evidence", shown, "reason", reason.text), "");
            // An antagonising card angers them before they answer (so a line can depend on the anger it caused).
            if (reason.kind == ReasonKind.Antagonise) Antagonise();
            Say(Speaker.Opponent, _def.opponent, Line(reason.id + ".reply", reason.reply), reason.expression);
            _heard.Add(id);
            switch (reason.kind)
            {
                case ReasonKind.Sound:
                    Logic(S.Format("logic.sound", "premise", p.text, "evidence", shown, "Evidence", Cap(shown), "reason", reason.text));
                    Set(id, PremiseStatus.Broken);
                    if (p.key) _keysBroken++;
                    RevealUnder(p);
                    LowerHeat(1);
                    Coach(ArgumentCoach.Broken);
                    if ((c.effect == SoundEffect.FallBack || c.effect == SoundEffect.Deepen) && !string.IsNullOrEmpty(c.effectTarget))
                        Reveal(c.effectTarget, false);
                    AfterExchange(false);
                    break;
                case ReasonKind.Beside:
                    Logic(S.Format("logic.beside", "premise", p.text, "evidence", shown, "Evidence", Cap(shown), "reason", reason.text));
                    RaiseHeat(1);
                    CoachMisstep();
                    AfterExchange(true);
                    break;
                case ReasonKind.Antagonise:
                    Logic(S.Format("logic.antagonise", "premise", p.text, "opponent", _def.opponent, "reason", reason.text));
                    AfterExchange(true);
                    break;
                default:
                    Logic(S.Format("logic.nonSequitur", "premise", p.text, "evidence", shown, "Evidence", Cap(shown), "reason", reason.text));
                    RaiseHeat(1);
                    CoachMisstep();
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
            Say(Speaker.Nadia, "Nadia", L(p, "retort", p.retort.line), "");
            Antagonise();
            Say(Speaker.Opponent, _def.opponent, L(p, "retort.reply", p.retort.reply), p.retort.expression);
            _retorted.Add(id);
            Logic(string.IsNullOrEmpty(p.question) ? S.Format("logic.retort", "premise", p.text, "opponent", _def.opponent) : S.Format("logic.retortQuestion", "question", p.question, "premise", p.text, "opponent", _def.opponent));
            _heard.Add(id);
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
            Say(Speaker.Nadia, "Nadia", Or(L(p, "concedeLine", p.concedeLine), S["nadia.concede"]), "");
            _heard.Add(id);
            if (p.truth == PremiseTruth.False)
            {
                Set(id, PremiseStatus.ConcededFalse);
                Say(Speaker.Opponent, _def.opponent, Or(L(p, "concedeFalseReply", p.concedeFalseReply), S["opponent.concedeFalse"]), "");
                Logic(S.Format("logic.concedeFalse", "premise", p.text, "opponent", _def.opponent));
                AfterExchange(false);
                return true;
            }
            Set(id, PremiseStatus.Conceded);
            Say(Speaker.Opponent, _def.opponent, Or(L(p, "concedeReply", p.concedeReply), S["opponent.concedeTrue"]), "softened");
            Logic(S.Format("logic.concede", "premise", p.text, "opponent", _def.opponent));
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
            if (id == ClaimId)
            {
                // The line is chosen before the press counts (a "tamer line while she's been gentle" means before this one).
                string claimLine = Or(Line("pressClaimLine", _def.pressClaimLine), S["nadia.pressClaim"]);
                _presses++;
                if (_def.pressAngers) RaiseAnger();
                Say(Speaker.Nadia, "Nadia", claimLine, "");
                Say(Speaker.Opponent, _def.opponent, Or(Line("pressClaimReply", _def.pressClaimReply), S["opponent.silence"]), "hurt");
                Logic(S.Format("logic.pressClaim", "claim", _def.claim, "opponent", _def.opponent));
                Finish(ArgumentOutcome.Pressed);
                return true;
            }
            var p = _def.Premise(id);
            string line = Or(L(p, "pressLine", p.pressLine), S["nadia.press"]);
            _presses++;
            if (_def.pressAngers) RaiseAnger();
            Say(Speaker.Nadia, "Nadia", line, "");
            Say(Speaker.Opponent, _def.opponent, Or(L(p, "pressReply", p.pressReply), S["opponent.silence"]), "hurt");
            Logic(S.Format("logic.press", "premise", p.text, "opponent", _def.opponent));
            Set(id, PremiseStatus.Forced);
            RevealUnder(p);
            _heard.Add(id);
            RaiseHeat(2);
            AfterExchange(true);
            return true;
        }

        /// <summary>Every premise, hidden ones included, has been revealed and heard. What only a slip reveals (something
        /// she isn't saying) doesn't hold it up: hearing someone out can't depend on catching a paper plane.</summary>
        public bool CanLetItRest
        {
            get
            {
                if (!Open) return false;
                foreach (var p in _def.premises)
                {
                    if (p == null) continue;
                    if (Status(p.id) == PremiseStatus.Hidden)
                    {
                        if (p.revealedBySlip) continue;
                        return false;
                    }
                    if (!_heard.Contains(p.id)) return false;
                }
                return true;
            }
        }

        /// <summary>"I hear you." Heard out (or Pressed, if she forced anything).</summary>
        public bool LetItRest()
        {
            if (!CanLetItRest) return false;
            Say(Speaker.Nadia, "Nadia", Line("letItRestLine", _def.letItRestLine), "");
            Say(Speaker.Opponent, _def.opponent, Line("letItRestReply", _def.letItRestReply), "softened");
            Logic(S.Format("logic.rest", "opponent", _def.opponent, "claim", _def.claim));
            Finish(_presses > 0 ? ArgumentOutcome.Pressed : ArgumentOutcome.HeardOut);
            return true;
        }

        public bool CanConcedeClaim => Open;

        /// <summary>Nadia gives up the argument itself (Yielded).</summary>
        public bool ConcedeClaim()
        {
            if (!CanConcedeClaim) return false;
            Say(Speaker.Nadia, "Nadia", Or(Line("concedeClaimLine", _def.concedeClaimLine), S["nadia.concedeClaim"]), "");
            Say(Speaker.Opponent, _def.opponent, Or(Line("concedeClaimReply", _def.concedeClaimReply), S["opponent.silence"]), "");
            Logic(S.Format("logic.concedeClaim", "claim", _def.claim, "opponent", _def.opponent));
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
            if (hits > 0 && Hurt(hits)) return;
            string slip = _pressure.TakeCaughtSlip();
            if (slip != null && Status(slip) == PremiseStatus.Hidden)
            {
                var p = _def.Premise(slip);
                Logic(S.Format("logic.slip", "slip", Or(p?.slipName, S["logic.something"]), "opponent", _def.opponent));
                Reveal(slip, true);
            }
            if (_pressure != null && _pressure.Finished)
            {
                _pressure = null;
                CheckEnd();
            }
        }

        /// <summary>A slip caught outside a pressure phase (the debate's drifting pages and planes): reveals what it hid.
        /// False if it reveals nothing (already out).</summary>
        public bool CatchSlip(string premiseId)
        {
            if (_ended || Status(premiseId) != PremiseStatus.Hidden) return false;
            var p = _def.Premise(premiseId);
            if (p == null || !p.revealedBySlip) return false;
            Logic(S.Format("logic.slip", "slip", Or(p.slipName, S["logic.something"]), "opponent", _def.opponent));
            Reveal(premiseId, true);
            AfterExchange(false);
            return true;
        }

        /// <summary>Her own words (a quote card), caught as she said them: the card is in Nadia's hand from now on. False if
        /// it's no quote of this battle's or already caught.</summary>
        public bool CatchQuote(string evidenceId)
        {
            if (_ended) return false;
            var ev = _def.Evidence(evidenceId);
            if (ev == null || !ev.IsQuote || !_quotes.Add(evidenceId)) return false;
            Logic(S.Format("logic.quote", "quote", ev.words, "opponent", _def.opponent, "evidence", ev.name));
            return true;
        }

        public bool Caught(string evidenceId) => _quotes.Contains(evidenceId);
        /// <summary>Cards shown that got an answer of their own (<see cref="AsideDef"/>).</summary>
        public int Asides => _asides;

        /// <summary>Her sky is open (the sections presenter, after the painful phase): from now on every phase but the three
        /// rings is played on <paramref name="rails"/> (her constellation), with streaks along her lit lines.</summary>
        public void OpenSky(Rails rails)
        {
            _sky = true;
            _skyRails = rails;
        }

        public bool SkyOpen => _sky;
        /// <summary>Full phases begun so far (not gestures): her notes start with the second.</summary>
        public int FullPhases => _fullPhases;

        /// <summary>Hidden premises a slip would reveal (the debate seeds its pages and planes from these).</summary>
        public void SlipsWaiting(List<SlipSeed> into)
        {
            into.Clear();
            foreach (var p in _def.premises)
                if (p != null && p.revealedBySlip && Status(p.id) == PremiseStatus.Hidden) into.Add(new SlipSeed { premise = p.id, name = p.slipName });
        }

        /// <summary>Ends the current phase at once (debug skip; the view's "skip" in Off mode).</summary>
        public void SkipPressure()
        {
            _pending = false;
            _pressure = null;
            CheckEnd();
        }

        /// <summary>Nadia's heart steps up <paramref name="steps"/> times: a hit in the debate or the showdown, a wrong card,
        /// a hesitation (the real-time battle, docs/redesign/battle.md). At 160 bpm her composure is gone: she says her
        /// line, they answer, and the battle ends there (Composure lost: no retry). A tutorial's heart stops a step short.
        /// True if it ended the battle.</summary>
        public bool Hurt(int steps)
        {
            if (_ended || !_started) return _ended;
            for (int i = 0; i < steps; i++)
            {
                _heart++;
                // A tutorial's composure can't run out: its heart stops a step short of panic.
                if (_def.heartCap < PanicStep && _heart > _def.heartCap) _heart = _def.heartCap;
                if (_heart < PanicStep) continue;
                _heart = PanicStep;
                Say(Speaker.Nadia, "Nadia", Line("composureLostLine", _def.composureLostLine), "");
                Say(Speaker.Opponent, _def.opponent, Line("composureLostReply", _def.composureLostReply), _def.composureLostExpression);
                Logic(S.Format("logic.heart", "bpm", HeartRates[PanicStep].ToString(System.Globalization.CultureInfo.InvariantCulture)));
                _pressure = null;
                _pending = false;
                Finish(ArgumentOutcome.ComposureLost);
                return true;
            }
            return false;
        }

        /// <summary>A line the battle's flow adds to the transcript (a bark, a hesitation): said, not a verb.</summary>
        public void Interject(Speaker speaker, string who, string text, string expression)
        {
            if (_ended) return;
            Say(speaker, string.IsNullOrEmpty(who) ? (speaker == Speaker.Opponent ? _def.opponent : speaker == Speaker.Nadia ? "Nadia" : "") : who, text, expression);
        }

        /// <summary>A sentence for the transcript (the step spelled out) from the battle's flow.</summary>
        public void Note(string text) => Logic(text);

        /// <summary>Raises the heat (her temper) from the flow: a round of silence.</summary>
        public void AddHeat(int n) => RaiseHeat(n);

        /// <summary>Queues a pressure phase from the flow (a key premise she holds on to as it breaks).</summary>
        public void QueuePhase(int level, bool painful) => Queue(Math.Min(MaxHeat, Math.Max(0, level)), painful);

        /// <summary>The coach's line for <paramref name="moment"/> now, the first time only (the flow's moments: the first
        /// statement, the first "Because...", the first wrong card, the first showdown, the first slip).</summary>
        public bool CoachNow(string moment)
        {
            // Between exchanges: it doesn't use up the next exchange's lesson.
            bool said = Coach(moment);
            _coachedThisTurn = false;
            return said;
        }

        /// <summary>What a premise hid under it comes out when it falls (asking can't reach it any more).</summary>
        void RevealUnder(PremiseDef p)
        {
            foreach (var r in p.askReveals) Reveal(r, false);
        }

        /// <summary>The spec the next (or current) phase plays with.</summary>
        public PressureSpec SpecFor(int level, bool painful) =>
            painful && _def.painfulCall && _setup.difficulty != ArgumentDifficulty.ScriptedButBeatable
                ? PressureSpec.Call(_anger, _setup.difficulty, _setup.pressureSpeed * _def.pressureScale)
                : PressureSpec.For(level, _anger, _setup.difficulty, _setup.pressureSpeed * _def.pressureScale, painful, level >= 2 || painful ? _def.hotShapes : _def.calmShapes);

        /// <summary>The spec the debate's own pressure plays with now (its heat and anger; sparser than a phase).</summary>
        public PressureSpec DebateSpec() =>
            PressureSpec.ForDebate(_heat, _anger, _setup.difficulty, _setup.pressureSpeed * _def.pressureScale, _def.debateDensity * (_setup.angerStart < 0 ? 0.85f : 1f), _heat >= 2 ? _def.hotShapes : _def.calmShapes);

        void Queue(int level, bool painful)
        {
            if (_ended) return;
            if (_pending)
            {
                _pendingLevel = Math.Max(_pendingLevel, level);
                _pendingPainful |= painful;
                _pendingSeconds = 0f; // a full phase wins over a gesture
                _pendingHush = _pendingAnswer = 0;
                return;
            }
            _pending = true;
            _pendingLevel = level;
            _pendingPainful = painful;
            _pendingSeconds = 0f;
            _pendingHush = _pendingAnswer = 0;
        }

        /// <summary>A gesture (docs/argument-ideas/SELECTION.md 2): a short phase of <paramref name="seconds"/> after one of
        /// their replies, shaped by its feeling. Nothing if a phase is already coming.</summary>
        public void QueueGesture(int level, float seconds) => QueueGesture(level, seconds, 0, 0);

        /// <summary>A gesture with her care in it (<paramref name="hush"/> rings that pass a light holding still) or her
        /// question (<paramref name="answer"/> waves that pass a light that moves): SELECTION.md 3.</summary>
        public void QueueGesture(int level, float seconds, int hush, int answer)
        {
            if (_ended || _pending || _pressure != null) return;
            Queue(level, false);
            _pendingSeconds = Math.Max(2f, seconds);
            _pendingHush = Math.Max(0, hush);
            _pendingAnswer = Math.Max(0, answer);
        }

        /// <summary>The phase coming (or playing) is a gesture.</summary>
        public bool GestureNext => _pending && _pendingSeconds > 0f;

        void BeginPhase()
        {
            _pending = false;
            _slipBuffer.Clear();
            foreach (var p in _def.premises)
                if (p != null && p.revealedBySlip && Status(p.id) == PremiseStatus.Hidden && _slipBuffer.Count < 2)
                    _slipBuffer.Add(new SlipSeed { premise = p.id, name = p.slipName });
            _phaseSeed = _phaseSeed * 6364136223846793005UL + 1442695040888963407UL;
            var spec = SpecFor(_pendingLevel, _pendingPainful);
            bool gesture = _pendingSeconds > 0f;
            if (gesture && !spec.exemptFromRule) spec.duration = _pendingSeconds;
            if (!spec.exemptFromRule && spec.calls == 0)
            {
                // Her care and her questions (the sections presenter's gestures); the first of each teaches (can't hurt).
                if (_pendingHush > 0)
                {
                    spec.hush = _pendingHush;
                    spec.hushTeach = !_hushTaught;
                    // The first hush is the lesson: on a clear table, nothing else.
                    if (spec.hushTeach) spec.only = true;
                    _hushTaught = true;
                    spec.duration = Math.Max(spec.duration, PressurePhase.HushSeconds * _pendingHush + 1.6f);
                }
                if (_pendingAnswer > 0)
                {
                    spec.answer = _pendingAnswer;
                    spec.answerTeach = !_answerTaught;
                    _answerTaught = true;
                }
                // Her notes: from the second full phase on, her pencil follows the light (in the kitchen: on her lines,
                // with their dead ends, a pencil on your heels would be a trap).
                if (!gesture && _def.notes && _setup.sections && _fullPhases >= 1 && !_sky) spec.trail = true;
                if (_sky) spec.sky = true;
            }
            _pendingSeconds = 0f;
            _pendingHush = _pendingAnswer = 0;
            if (!gesture) _fullPhases++;
            _pressure = new PressurePhase(spec, _phaseSeed, _slipBuffer);
            if (_sky && spec.calls == 0 && _skyRails != null) _pressure.SetRails(_skyRails);
            _phases++;
            Coach(ArgumentCoach.Pressure);
        }

        // ---------- Rules ----------

        void AfterExchange(bool consequence)
        {
            CheckSurfacing();
            if (CheckEnd()) return;
            // At most one coach line per exchange: what the exchange itself brought (a misstep, a break) comes first.
            if (!_coachedThisTurn) CoachMoments();
            _coachedThisTurn = false;
            if (consequence) Queue(_heat, false);
            else if (_setup.difficulty == ArgumentDifficulty.ScriptedButBeatable) Queue(MaxHeat, false);
        }

        void Antagonise()
        {
            _antagonised++;
            RaiseAnger();
            RaiseHeat(2);
        }

        // ---------- The coach (Mags in the Night 1 tutorial) ----------

        /// <summary>The coach's line for <paramref name="moment"/>, the first time it comes (nothing without a coach).
        /// True if it was said now.</summary>
        bool Coach(string moment)
        {
            if (_def.coach.Count == 0 || _ended || _coached.Contains(moment) || !_def.coach.ContainsKey(moment)) return false;
            if (!_def.coach.TryGetValue(moment, out var line) || string.IsNullOrEmpty(line)) return false;
            _coached.Add(moment);
            _coachedThisTurn = true;
            Say(Speaker.Supporter, string.IsNullOrEmpty(_def.coachName) ? "Coach" : _def.coachName, Line("coach." + moment, line), "");
            return true;
        }

        /// <summary>A misstep: the coach's word about forcing (the real-time battle's flow says what the misstep cost
        /// instead, and forcing comes with the next one).</summary>
        void CoachMisstep()
        {
            if (!_def.coach.ContainsKey(ArgumentCoach.Wrong)) Coach(ArgumentCoach.Press);
        }

        /// <summary>Has the coach said the line for this moment yet (tests).</summary>
        public bool Coached(string moment) => _coached.Contains(moment);

        /// <summary>The moments that depend on what's on the table, one line at a time, in this order: something true
        /// she's heard (concede it), something she can break (challenge it), only what she can't break left (forcing
        /// exists), everything heard (let it rest).</summary>
        void CoachMoments()
        {
            if (_def.coach.Count == 0 || _ended) return;
            bool concedable = false, breakable = false, falseStanding = false, allHeard = true;
            foreach (var p in _def.premises)
            {
                if (p == null) continue;
                var st = Status(p.id);
                if (st == PremiseStatus.Hidden ? !p.revealedBySlip : !_heard.Contains(p.id)) allHeard = false;
                if (st != PremiseStatus.Standing) continue;
                if (p.truth == PremiseTruth.False) falseStanding = true;
                if (_heard.Contains(p.id) && p.truth != PremiseTruth.False && !p.key && !p.unbreakable) concedable = true;
                if (p.unbreakable) continue;
                foreach (var c in p.challenges)
                    if (c != null && HasEvidence(c.evidence)) { breakable = true; break; }
            }
            if (concedable && Coach(ArgumentCoach.Concede)) return;
            if (breakable && Coach(ArgumentCoach.Challenge)) return;
            if (!breakable && falseStanding && _coached.Contains(ArgumentCoach.Challenge) && Coach(ArgumentCoach.Press)) return;
            if (allHeard) Coach(ArgumentCoach.Rest);
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
            // Their painful question, once it's out, has to be faced before it ends (the last stand): conceded, forced,
            // or answered back.
            if (LastStand) return false;
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
                if (p == null || p.surfacesAfter.Count == 0) continue;
                var own = Status(p.id);
                // Out already (said sorry at the bedside): only its painful phase is still to come.
                bool phaseOnly = own != PremiseStatus.Hidden && p.painful && !_painfulPlayed.Contains(p.id);
                if (own != PremiseStatus.Hidden && !phaseOnly) continue;
                foreach (var after in p.surfacesAfter)
                {
                    var s = Status(after);
                    if (s != PremiseStatus.Broken && s != PremiseStatus.Conceded && s != PremiseStatus.ConcededFalse && s != PremiseStatus.Forced) continue;
                    if (!phaseOnly) Reveal(p.id, true);
                    if (p.painful) { _painfulPlayed.Add(p.id); Queue(MaxHeat, true); }
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
            _stance = _def.stanceRule == StanceRule.TwoEndings ? TwoEndings(outcome, _antagonised) : StanceFor(outcome, _anger, _presses);
            switch (outcome)
            {
                case ArgumentOutcome.Convinced:
                    Say(Speaker.Opponent, _def.opponent, Line("convincedReply", _def.convincedReply), "softened");
                    Say(Speaker.Opponent, _def.opponent, Line("truthText", _def.truthText), "softened");
                    break;
                case ArgumentOutcome.HeardOut:
                    Say(Speaker.Opponent, _def.opponent, Line("heardOutReply", _def.heardOutReply), "softened");
                    break;
                case ArgumentOutcome.Pressed:
                    Say(Speaker.Opponent, _def.opponent, Line("pressedReply", _def.pressedReply), "hurt");
                    break;
            }
            Logic(S.Format("logic.outcome", "outcome", S["outcome." + outcome], "opponent", _def.opponent, "stance", StanceWords(_stance, S)));
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

        /// <summary>The creator's two endings (Lena): she condemns Nadia after Pressed, Composure lost, or any antagonising
        /// that landed (a card or a retort); otherwise (Convinced, Heard out, Yielded) she's unsure.</summary>
        public static ArgumentStance TwoEndings(ArgumentOutcome outcome, int antagonised)
        {
            if (outcome == ArgumentOutcome.None) return ArgumentStance.None;
            if (antagonised > 0 || outcome == ArgumentOutcome.Pressed || outcome == ArgumentOutcome.ComposureLost) return ArgumentStance.Condemns;
            return ArgumentStance.Unsure;
        }

        /// <summary>The stance in words, from the screen file ("amenable", "unsure", "condemning").</summary>
        public static string StanceWords(ArgumentStance s) => StanceWords(s, ArgumentStrings.Shared);

        public static string StanceWords(ArgumentStance s, ArgumentStrings strings) => strings["stance." + s];

        /// <summary>The words this battle speaks in (its own, else the shared screen file).</summary>
        public ArgumentStrings Strings => _def.strings ?? ArgumentStrings.Shared;
        ArgumentStrings S => Strings;

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
            if (speaker == Speaker.Opponent) expression = _def.Face(expression);
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
