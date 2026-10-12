using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Argument
{
    /// <summary>Where the battle is (docs/redesign/battle.md).</summary>
    public enum BattleState
    {
        /// <summary>The opening lines (the debate hasn't started).</summary>
        Intro,
        /// <summary>An exchange is being said: the debate holds still.</summary>
        Talk,
        /// <summary>Real-time: statements fly, pressure comes, Nadia's light acts.</summary>
        Debate,
        /// <summary>Evidence landed on the weak words: pick the reason that follows, before they lose patience (the
        /// table slows almost to a stop).</summary>
        Because,
        /// <summary>A full pressure phase (a consequence): dodge.</summary>
        Pressure,
        /// <summary>A press: cut through their words as they come.</summary>
        Showdown,
        /// <summary>Nadia's composure has gone (160 bpm): the room closes in before she speaks.</summary>
        Panic,
        /// <summary>The end's own beat (the claim erased, the keepsake, the cracks...).</summary>
        Ending,
        /// <summary>The end card: waiting for Continue.</summary>
        Over,
    }

    /// <summary>One frame of the player's (or the pilot's) input.</summary>
    public struct BattleInput
    {
        /// <summary>The stick, d-pad, WASD or arrows (-1..1).</summary>
        public Vector2 move;
        /// <summary>A: show the card in hand (in the debate), choose (Because), cut (showdown), hurry a line (talk).</summary>
        public bool fire;
        /// <summary>X: "Why?" at what the light is on.</summary>
        public bool ask;
        /// <summary>Y: "That's true." (held on the claim: give in).</summary>
        public bool concede, concedeHeld;
        /// <summary>B held: press (force) what the light is on.</summary>
        public bool pressHeld;
        /// <summary>LB / RB (Q / E, the wheel): the next or previous card.</summary>
        public int cycle;
        /// <summary>Up / down in the Because cards (-1 up, 1 down).</summary>
        public int navY;
        /// <summary>A reason card chosen directly (the mouse), or -1.</summary>
        public int pick;
        /// <summary>The pointer moved: steer the light toward <see cref="point"/> (surface units).</summary>
        public bool hasPoint;
        public Vector2 point;

        public static BattleInput None => new BattleInput { pick = -1 };
    }

    public enum CueKind
    {
        LineStart, Blip, SlowBlip, StatementSpawn, Fire, Miss, Glance, Burned, WrongCard, HeartUp, HeartDown,
        Break, Reveal, BecauseOpen, BecauseMove, BecausePick, Hesitate, ShowdownStart, Cut, Whiff, WordLanded,
        ShowdownWon, PressureIn, PressureOut, Warn, SlipCaught, RoundStart, Idle, Ring, Panic, End, Cycle, HoldStart,
        HoldCancel, Ask, Concede, Retort, Rest, Yield, Unbreakable, Settle, Shatter,
    }

    /// <summary>Something the view and the sound react to this frame.</summary>
    public struct Cue
    {
        public CueKind kind;
        public string id;
        public int value;
        public Speaker speaker;
    }

    /// <summary>
    /// The argument battle as real-time play (docs/redesign/battle.md): the rules (<see cref="ArgumentEngine"/>: premises,
    /// ask, concede, challenge with evidence and a reason, press, let it rest, the outcomes) played inside a debate
    /// (<see cref="Debate"/>), a "Because..." under a clock, a showdown for pressing (<see cref="Showdown"/>), and the
    /// pressure phases, with every line delivered one at a time (<see cref="Delivery"/>) and the opponent's face
    /// following (<see cref="Portrait"/>).
    ///
    /// Wrong choices cost, and the player feels it: evidence that doesn't touch what was said, challenging something
    /// true, or a reason that doesn't follow costs a heartbeat, takes the card out of her hand for the next round, and
    /// (a wrong reason) heats them up and brings a harder pressure phase. Hesitating over the reasons costs the same. A
    /// round of silence heats them up. At 160 bpm Nadia's composure is gone: she leaves the table, and the battle ends
    /// there (no retry): Lena condemns her (Despair +1). Walt's tutorial can't be lost.
    ///
    /// Pure C#: the host feeds it input and time and draws what it says; tests and the timing simulation run it headless.
    /// </summary>
    public sealed class BattleFlow
    {
        public const float PressHold = 0.6f;
        public const float YieldHold = 1.1f;
        public const float ShotCooldown = 0.28f;
        public const float PanicSeconds = 2.4f;
        public const float EndingSeconds = 3.6f;
        public const string RetortCard = "@retort", RestCard = "@rest";
        /// <summary>Their opening pose (arms folded, palm raised): held until their first expressive line, and again as
        /// their idle once they're angry.</summary>
        public const string OpeningFace = "antagonistic";

        readonly ArgumentEngine _engine;
        readonly Debate _debate;
        readonly Delivery _delivery = new Delivery();
        readonly Portrait _portrait;
        readonly List<Cue> _cues = new List<Cue>(32);
        readonly List<string> _cards = new List<string>(12);
        readonly Dictionary<string, int> _burnedUntil = new Dictionary<string, int>();
        readonly List<ReasonCard> _because = new List<ReasonCard>(5);
        readonly ulong _seed;
        Showdown _showdown;
        BattleState _state = BattleState.Intro;
        float _stateTime, _cooldown, _hold, _patience, _patienceLeft, _captionAge = 99f, _panic, _ending;
        float _active, _talk;
        int _seen, _card, _becauseFocus, _shotsThisRound, _lastRound, _lastHeart, _warned;
        string _caption = "", _becausePremise = "", _becauseEvidence = "", _showdownTarget = "", _holdTarget = "";
        int _holdKind; // 0 none, 1 press, 2 give in
        bool _panicDone, _askCoached, _slipCoached, _expressed, _stood;
        PressurePhase _warnedPhase;
        Vector2 _light = new Vector2(-1.2f, -0.6f);
        float _textSpeed = 1f;

        public BattleFlow(ArgumentEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _seed = engine.Setup.seed == 0 ? 1UL : engine.Setup.seed;
            _debate = new Debate(engine, _seed * 31UL + 7UL);
            _portrait = new Portrait(OpeningFace);
            if (!_engine.Started) _engine.Start();
            _lastHeart = _engine.HeartStep;
            RefreshCards();
            PullTranscript();
            Enter(BattleState.Intro);
        }

        // ---------- What the view reads ----------

        public ArgumentEngine Engine => _engine;
        public ArgumentDef Def => _engine.Def;
        public Debate Debate => _debate;
        public Delivery Delivery => _delivery;
        public Portrait Portrait => _portrait;
        public Showdown Showdown => _showdown;
        public BattleState State => _state;
        public float StateTime => _stateTime;
        public IReadOnlyList<Cue> Cues => _cues;
        /// <summary>Nadia's light (surface units), wherever she is.</summary>
        public Vector2 Light => _light;
        /// <summary>The cards in hand: evidence ids, then the specials (<see cref="RetortCard"/>, <see cref="RestCard"/>).</summary>
        public IReadOnlyList<string> Cards => _cards;
        public int CardIndex => _card;
        public string CardInHand => _card >= 0 && _card < _cards.Count ? _cards[_card] : "";
        /// <summary>Rounds before a burned card is back (0: in hand).</summary>
        public int BurnedFor(string card) => _burnedUntil.TryGetValue(card ?? "", out int until) ? Math.Max(0, until - _debate.Round) : 0;
        /// <summary>The reason cards (Because).</summary>
        public IReadOnlyList<ReasonCard> Because => _because;
        public int BecauseFocus => _becauseFocus;
        public string BecausePremise => _becausePremise;
        public string BecauseEvidence => _becauseEvidence;
        /// <summary>0..1 of their patience left (Because); 1 when there's no clock (Story).</summary>
        public float Patience => _patience <= 0f ? 1f : Math.Max(0f, _patienceLeft / _patience);
        public string ShowdownTarget => _showdownTarget;
        /// <summary>A hold in progress (press or give in): 0..1, and on what.</summary>
        public float HoldProgress => _holdKind == 0 ? 0f : Math.Min(1f, _hold / (_holdKind == 1 ? PressHold : YieldHold));
        public bool HoldingPress => _holdKind == 1;
        public bool HoldingYield => _holdKind == 2;
        public string HoldTarget => _holdTarget;
        /// <summary>A short note on the screen (the step spelled out, or why something didn't work), and its age.</summary>
        public string Caption => _caption;
        public float CaptionAge => _captionAge;
        /// <summary>Seconds of real-time play (the debate, the Because, pressure, the showdown) and of talk so far.</summary>
        public float ActiveSeconds => _active;
        public float TalkSeconds => _talk;
        public bool Over => _state == BattleState.Over;
        /// <summary>The composure-lost beat's progress (0..1).</summary>
        public float PanicProgress => Math.Min(1f, _panic / PanicSeconds);
        public float EndingProgress => Math.Min(1f, _ending / EndingSeconds);
        /// <summary>Fills the coach's key words ({ask}, {concede}, {show}, {press}, {cards}, {move}) with the player's
        /// own keys (the host sets it from the bindings; the default names a pad's).</summary>
        public Func<string, string> Expand = DefaultKeys;

        public static string DefaultKeys(string text) =>
            string.IsNullOrEmpty(text) || text.IndexOf('{') < 0 ? text : text.Replace("{ask}", "X").Replace("{concede}", "Y").Replace("{show}", "A")
                .Replace("{press}", "B").Replace("{cards}", "LB / RB").Replace("{move}", "the stick");

        /// <summary>Text speed (1 = normal).</summary>
        public float TextSpeed { get => _textSpeed; set => _textSpeed = value <= 0f ? 1f : value; }

        static ArgumentStrings S => ArgumentStrings.Shared;
        ArgumentStrings Str => _engine.Strings;

        // ---------- The frame ----------

        public void Tick(float dt, in BattleInput input)
        {
            _cues.Clear();
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;
            _stateTime += dt;
            _captionAge += dt;
            if (_cooldown > 0f) _cooldown -= dt;
            switch (_state)
            {
                case BattleState.Debate:
                case BattleState.Because:
                case BattleState.Pressure:
                case BattleState.Showdown:
                    _active += dt;
                    break;
                case BattleState.Over:
                    break;
                default:
                    _talk += dt;
                    break;
            }

            // The lines play in every state; a press hurries or moves them on only where talking is all there is.
            bool talking = _state == BattleState.Intro || _state == BattleState.Talk || _state == BattleState.Ending;
            var tock = _delivery.Tick(dt, _textSpeed, talking && input.fire);
            if (tock.started)
            {
                var line = _delivery.Current;
                Cue(CueKind.LineStart, line.who, line.statement, line.speaker);
                if (line.speaker == Speaker.Opponent) _portrait.Speak(line.expression);
            }
            if (tock.blips > 0) Cue(CueKind.Blip, _delivery.Current.who, tock.blips, _delivery.Current.speaker);
            if (tock.slowBlips > 0) Cue(CueKind.SlowBlip, _delivery.Current.who, tock.slowBlips, _delivery.Current.speaker);
            if (_portrait.Speaking && (!_delivery.HasLine || _delivery.LineDone || _delivery.Current.speaker != Speaker.Opponent)) _portrait.Quiet();
            _portrait.SetIdle(IdleFace());
            _portrait.Tick(dt);

            // Someone ended it from outside (a test's pilot, a debug skip): go to its end.
            if (_engine.Ended && (_state == BattleState.Debate || _state == BattleState.Because || _state == BattleState.Pressure || _state == BattleState.Showdown))
            {
                Next();
                return;
            }

            switch (_state)
            {
                case BattleState.Intro:
                case BattleState.Talk:
                    if (!_delivery.Blocking) Next();
                    break;
                case BattleState.Debate: TickDebate(dt, input); break;
                case BattleState.Because: TickBecause(dt, input); break;
                case BattleState.Pressure: TickPressure(dt, input); break;
                case BattleState.Showdown: TickShowdown(dt, input); break;
                case BattleState.Panic:
                    _panic += dt;
                    if (_panic >= PanicSeconds)
                    {
                        _panicDone = true;
                        _portrait.Release(); // her answer shows its own face
                        PullTranscript();
                        Enter(BattleState.Talk);
                    }
                    break;
                case BattleState.Ending:
                    _ending += dt;
                    if (_ending >= EndingSeconds && !_delivery.Blocking)
                    {
                        Enter(BattleState.Over);
                        Cue(CueKind.End, _engine.Outcome.ToString(), 0);
                    }
                    break;
            }
            if (_state != BattleState.Panic) PullTranscript();
            HeartCues();
        }

        /// <summary>Moves on from an exchange: to the end, a talk, a phase, or back to the debate.</summary>
        void Next()
        {
            ResetHold();
            if (_engine.Ended)
            {
                if (_engine.Outcome == ArgumentOutcome.ComposureLost && !_panicDone) { Enter(BattleState.Panic); Cue(CueKind.Panic, "", 0); return; }
                if (_delivery.Blocking) { Enter(BattleState.Talk); return; }
                Enter(BattleState.Ending);
                return;
            }
            if (_delivery.Blocking) { Enter(BattleState.Talk); return; }
            if (_engine.InPressure) { Enter(BattleState.Pressure); return; }
            Enter(BattleState.Debate);
        }

        void Enter(BattleState s)
        {
            var was = _state;
            _state = s;
            _stateTime = 0f;
            switch (s)
            {
                case BattleState.Debate:
                    _debate.Begin(_light);
                    RefreshCards();
                    break;
                case BattleState.Pressure:
                    // The engine starts its queued phase on its next tick; start it now so her light can be placed.
                    if (_engine.Pressure == null) _engine.Tick(0.0001f, Vector2.Zero);
                    _engine.Pressure?.PlaceLight(_light);
                    _warnedPhase = _engine.Pressure;
                    _warned = _engine.Pressure != null ? _engine.Pressure.Telegraphs : 0;
                    Cue(CueKind.PressureIn, "", _engine.Pressure != null ? _engine.Pressure.Spec.calls : 0);
                    if (_engine.Pressure != null && _engine.Pressure.Spec.calls > 0) Bark(ArgumentBarks.Ring, false);
                    break;
                case BattleState.Ending:
                    _ending = 0f;
                    _portrait.Hold(EndFace());
                    break;
                case BattleState.Panic:
                    _panic = 0f;
                    _delivery.Clear();
                    _portrait.Hold(Def.Face("hurt"));
                    break;
            }
            if (was == BattleState.Pressure && s != BattleState.Pressure) Cue(CueKind.PressureOut, "", 0);
        }

        // ---------- The debate ----------

        void TickDebate(float dt, in BattleInput input)
        {
            if (_delivery.Blocking) { Enter(BattleState.Talk); return; }
            var move = Steer(input, _debate.Light);
            int spoken = _debate.Tick(dt, move);
            _light = _debate.Light;
            if (spoken > 0) Spoke(spoken);
            int hits = _debate.TakeHits();
            if (hits > 0 && _engine.Hurt(hits)) { Next(); return; }
            WarnCues(_debate.Hazards);

            if (_debate.Round != _lastRound)
            {
                if (_lastRound > 0)
                {
                    // A whole round of silence heats them up (the first round is for listening).
                    if (_shotsThisRound == 0 && _lastRound >= 2)
                    {
                        _engine.AddHeat(1);
                        _engine.Note(Str.Format("logic.idle", "opponent", Def.opponent));
                        Bark(ArgumentBarks.Idle, false);
                        Cue(CueKind.Idle, "", _engine.Heat);
                    }
                }
                _lastRound = _debate.Round;
                _shotsThisRound = 0;
                RefreshCards();
                Cue(CueKind.RoundStart, "", _lastRound);
            }

            if (!_slipCoached)
                foreach (var sl in _debate.Slips)
                    if (sl.active) { _slipCoached = true; _engine.CoachNow(ArgumentCoach.Slip); break; }
            string slip = _debate.TakeCaughtSlip();
            if (slip != null && _engine.CatchSlip(slip))
            {
                Cue(CueKind.SlipCaught, slip, 0);
                AfterVerb();
                return;
            }

            if (input.cycle != 0 && _cards.Count > 1)
            {
                _card = (_card + (input.cycle > 0 ? 1 : _cards.Count - 1)) % _cards.Count;
                Cue(CueKind.Cycle, CardInHand, _card);
            }

            // Holds: B to press what the light is on; Y on the claim to give in.
            if (_holdKind == 1)
            {
                if (!input.pressHeld) { ResetHold(); Cue(CueKind.HoldCancel, "", 1); }
                else if ((_hold += dt) >= PressHold) { StartShowdown(_holdTarget); return; }
            }
            else if (_holdKind == 2)
            {
                if (!input.concedeHeld) { ResetHold(); Cue(CueKind.HoldCancel, "", 2); }
                else if ((_hold += dt) >= YieldHold)
                {
                    ResetHold();
                    if (_engine.ConcedeClaim()) { Cue(CueKind.Yield, ArgumentEngine.ClaimId, 0); AfterVerb(); return; }
                }
            }
            else if (input.pressHeld && _cooldown <= 0f)
            {
                int i = _debate.Under(_debate.Light, false, out _);
                if (i >= 0)
                {
                    var s = _debate[i];
                    string target = s.claim ? ArgumentEngine.ClaimId : s.premise;
                    if (_engine.CanPress(target)) { _holdKind = 1; _hold = 0f; _holdTarget = target; Cue(CueKind.HoldStart, target, 1); }
                    else { Note("screen.why.cantPress"); _cooldown = 0.6f; }
                }
            }

            if (_cooldown > 0f || _holdKind != 0) return;
            if (input.ask) Shoot(0);
            else if (input.concede) Shoot(1);
            else if (input.fire) Shoot(2);
        }

        Vector2 Steer(in BattleInput input, Vector2 light)
        {
            var move = input.move;
            if (move.LengthSquared() < 0.04f && input.hasPoint)
            {
                var to = input.point - light;
                float d = to.Length();
                move = d > 0.03f ? to / d * Math.Min(1f, d / 0.25f) : Vector2.Zero;
            }
            return move;
        }

        void Spoke(int serial)
        {
            for (int i = 0; i < _debate.Statements.Count; i++)
            {
                var s = _debate[i];
                if (s.serial != serial) continue;
                string text;
                if (s.claim) text = Def.claim;
                else
                {
                    var p = Def.Premise(s.premise);
                    text = p == null ? s.Plain : s.question ? p.question : p.text;
                }
                _delivery.Add(new Delivery.Line { speaker = Speaker.Opponent, who = Def.opponent, text = text, expression = "", statement = serial, repeat = s.repeat });
                Cue(CueKind.StatementSpawn, s.premise, serial);
                if (!_askCoached && !s.claim)
                {
                    _askCoached = true;
                    if (_engine.CoachNow(ArgumentCoach.Ask)) PullTranscript();
                }
                return;
            }
        }

        /// <summary>A shot at what the light is on: 0 Ask (X), 1 Concede (Y), 2 the card in hand (A).</summary>
        void Shoot(int kind)
        {
            _shotsThisRound++;
            _cooldown = ShotCooldown;
            bool card = kind == 2;
            int i = _debate.Under(_debate.Light, card, out bool onWeak);
            if (i < 0) { Cue(CueKind.Miss, "", kind); return; }
            var s = _debate[i];
            Cue(CueKind.Fire, s.premise, kind);
            switch (kind)
            {
                case 0:
                    if (s.claim) { Glance(i, "screen.why.askClaim"); return; }
                    if (!_engine.CanAsk(s.premise)) { Glance(i, "screen.why.nothingToAsk"); return; }
                    _engine.Ask(s.premise);
                    Cue(CueKind.Ask, s.premise, 0);
                    AfterVerb();
                    return;
                case 1:
                    if (s.claim)
                    {
                        if (_engine.CanConcedeClaim) { _holdKind = 2; _hold = 0f; _holdTarget = ArgumentEngine.ClaimId; Cue(CueKind.HoldStart, ArgumentEngine.ClaimId, 2); }
                        return;
                    }
                    if (!_engine.CanConcede(s.premise)) { Glance(i, "screen.why.cantConcede"); return; }
                    _engine.Concede(s.premise);
                    Cue(CueKind.Concede, s.premise, _engine.Status(s.premise) == PremiseStatus.Conceded ? 1 : 0);
                    AfterVerb();
                    return;
            }
            string c = CardInHand;
            if (c.Length == 0) { Glance(i, "screen.why.noCards"); return; }
            if (BurnedFor(c) > 0)
            {
                Cue(CueKind.Burned, c, BurnedFor(c));
                Note("screen.why.burned", c);
                return;
            }
            if (c == RetortCard)
            {
                if (s.claim || !_engine.CanRetort(s.premise)) { Glance(i, "screen.why.retortOnly"); return; }
                _engine.Retort(s.premise);
                Cue(CueKind.Retort, s.premise, 0);
                AfterVerb();
                return;
            }
            if (c == RestCard)
            {
                if (!s.claim) { Glance(i, "screen.why.restOnClaim"); return; }
                if (!_engine.LetItRest()) { Glance(i, "screen.why.notYet"); return; }
                Cue(CueKind.Rest, ArgumentEngine.ClaimId, 0);
                AfterVerb();
                return;
            }
            if (s.claim) { Glance(i, "screen.why.claimNoChallenge"); return; }
            if (s.question) { Glance(i, "screen.why.questionEvidence"); return; }
            if (!onWeak) { Glance(i, "screen.why.glance"); return; }
            var step = _engine.BeginChallenge(s.premise, c, _because);
            switch (step)
            {
                case ChallengeStep.Reasons:
                    _becausePremise = s.premise;
                    _becauseEvidence = c;
                    _becauseFocus = 0;
                    _patience = _engine.Setup.difficulty == ArgumentDifficulty.Story ? 0f : BecausePatience();
                    _patienceLeft = _patience;
                    _debate.PlaceLight(_light);
                    Enter(BattleState.Because);
                    Cue(CueKind.BecauseOpen, s.premise, _because.Count);
                    if (_engine.CoachNow(ArgumentCoach.Because)) PullTranscript(false);
                    return;
                case ChallengeStep.NoTouch:
                case ChallengeStep.TrueOne:
                    Wrong(c, s.premise);
                    AfterVerb();
                    return;
                case ChallengeStep.Unbreakable:
                    Cue(CueKind.Unbreakable, s.premise, 0);
                    AfterVerb();
                    return;
                default:
                    Glance(i, "screen.why.cantChallenge");
                    return;
            }
        }

        /// <summary>Seconds they wait for Nadia's reason: time to read the cards (about 14 letters a second) and decide,
        /// less the angrier and more heated they are; the tutorial is twice as patient.</summary>
        float BecausePatience()
        {
            int letters = 0;
            foreach (var c in _because) letters += c.reason != null ? c.reason.text.Length : 0;
            float read = 5f + letters / 14f;
            if (Def.heartCap < ArgumentEngine.PanicStep) return read * 2f;
            return Math.Max(read * 0.6f, read * (1f - 0.1f * _engine.Anger - 0.05f * _engine.Heat));
        }

        void Glance(int statement, string whyKey)
        {
            Cue(CueKind.Glance, statement >= 0 ? _debate[statement].premise : "", 0);
            Note(whyKey);
        }

        /// <summary>A wrong card: a heartbeat, and the card is out of her hand until the round after next.</summary>
        void Wrong(string card, string premise)
        {
            Burn(card);
            Cue(CueKind.WrongCard, premise, 0);
            // Mags: what it cost, the first time; that forcing exists, the next.
            if (!_engine.CoachNow(ArgumentCoach.Wrong)) _engine.CoachNow(ArgumentCoach.Press);
            _engine.Hurt(1);
        }

        void Burn(string card)
        {
            if (string.IsNullOrEmpty(card) || card[0] == '@') return;
            _burnedUntil[card] = _debate.Round + 2;
            var ev = Def.Evidence(card);
            string name = ev != null ? ev.name : card;
            _engine.Note(Str.Format("logic.burned", "evidence", name, "Evidence", Cap(name)));
            Cue(CueKind.Burned, card, 2);
        }

        /// <summary>After a verb: the debate catches up (what fell leaves, what came out joins), and the exchange is said.
        /// Their reply's feeling plays as a gesture (SELECTION.md 2): when it's angry or hurt, a short phase where they
        /// mean it; rattled, a smaller one.</summary>
        void AfterVerb()
        {
            ResetHold();
            _debate.Refresh();
            RefreshCards();
            if (_engine.LastStand && !_stood)
            {
                // Everything she built is down but the one thing Nadia hasn't answered.
                _stood = true;
                PullTranscript();
                Bark(ArgumentBarks.Stand, true);
            }
            string felt = "";
            var t = _engine.Transcript;
            for (int k = _seen; k < t.Count; k++) if (t[k].speaker == Speaker.Opponent && t[k].expression.Length > 0) felt = t[k].expression;
            if (!_engine.Ended && !_engine.InPressure)
            {
                if (felt == Def.Face("angry") && felt != "neutral" || felt == Def.Face("hurt")) _engine.QueueGesture(Math.Max(1, _engine.Heat), 4.5f);
                else if (felt == "rattled") _engine.QueueGesture(0, 3f);
            }
            PullTranscript();
            _cooldown = ShotCooldown;
            Next();
        }

        void ResetHold()
        {
            _holdKind = 0;
            _hold = 0f;
            _holdTarget = "";
        }

        // ---------- Because... ----------

        void TickBecause(float dt, in BattleInput input)
        {
            // The table holds still while she thinks (nothing can hit a light that can't move); their patience is the clock.
            if (_patience > 0f)
            {
                _patienceLeft -= dt;
                if (_patienceLeft <= 0f) { Hesitate(); return; }
            }
            if (input.navY != 0 && _because.Count > 0)
            {
                int f = Math.Max(0, Math.Min(_because.Count - 1, _becauseFocus + input.navY));
                if (f != _becauseFocus) { _becauseFocus = f; Cue(CueKind.BecauseMove, "", f); }
            }
            int pick = input.pick >= 0 && input.pick < _because.Count ? input.pick : input.fire && _stateTime > 0.25f ? _becauseFocus : -1;
            if (pick >= 0) Resolve(pick);
        }

        void Resolve(int index)
        {
            var reason = _because[index].reason;
            string premise = _becausePremise, card = _becauseEvidence;
            Cue(CueKind.BecausePick, premise, (int)reason.kind);
            if (!_engine.ResolveChallenge(premise, card, reason)) { AfterVerb(); return; }
            if (reason.kind == ReasonKind.Sound)
            {
                if (_engine.Status(premise) == PremiseStatus.Broken)
                {
                    Cue(CueKind.Break, premise, 0);
                    // A key premise doesn't go quietly: she holds on to it with everything she has (a phase), unless
                    // that was the last of them.
                    var pd = Def.Premise(premise);
                    if (pd != null && pd.key && !_engine.Ended) _engine.QueuePhase(Math.Max(2, _engine.Heat + 1), false);
                }
            }
            else if (reason.kind != ReasonKind.Antagonise) Wrong(card, premise);
            AfterVerb();
        }

        /// <summary>Their patience ran out before Nadia said why: she falters (a heartbeat, the card out of her hand).</summary>
        void Hesitate()
        {
            Cue(CueKind.Hesitate, _becausePremise, 0);
            var ev = Def.Evidence(_becauseEvidence);
            string name = ev != null ? ev.name : _becauseEvidence;
            PullTranscript();
            Bark(ArgumentBarks.Hesitate, true);
            _engine.Note(Str.Format("logic.hesitate", "evidence", name, "Evidence", Cap(name), "opponent", Def.opponent));
            Burn(_becauseEvidence);
            _engine.Hurt(1);
            AfterVerb();
        }

        // ---------- Pressure (the engine's phases) ----------

        void TickPressure(float dt, in BattleInput input)
        {
            var phase = _engine.Pressure;
            var move = phase != null ? Steer(input, phase.Light) : input.move;
            _engine.Tick(dt, move);
            if (_engine.Pressure != null) _light = _engine.Pressure.Light;
            WarnCues(_engine.Pressure);
            if (_engine.Ended) { Next(); return; }
            if (!_engine.InPressure)
            {
                _debate.PlaceLight(_light);
                _debate.Refresh();
                RefreshCards();
                Next();
            }
        }

        void WarnCues(PressurePhase phase)
        {
            if (phase != _warnedPhase) { _warnedPhase = phase; _warned = phase != null ? phase.Telegraphs : 0; return; }
            if (phase == null) return;
            if (phase.Telegraphs > _warned) Cue(CueKind.Warn, "", phase.Telegraphs - _warned);
            _warned = phase.Telegraphs;
            if (phase.Spec.calls > 0 && phase.CallsRung > _ringsCued) { _ringsCued = phase.CallsRung; Cue(CueKind.Ring, "", _ringsCued); }
        }

        int _ringsCued;

        // ---------- The showdown ----------

        void StartShowdown(string target)
        {
            ResetHold();
            _showdownTarget = target;
            float pace = Def.heartCap < ArgumentEngine.PanicStep ? 0.75f : 1f;
            _showdown = new Showdown(Def.showdown, _engine.Anger, target == ArgumentEngine.ClaimId, _engine.Setup.difficulty, pace, _seed + (ulong)_debate.Round * 977UL);
            Enter(BattleState.Showdown);
            Cue(CueKind.ShowdownStart, target, _showdown.Total);
            if (_engine.CoachNow(ArgumentCoach.Showdown)) PullTranscript(false);
        }

        void TickShowdown(float dt, in BattleInput input)
        {
            int r = _showdown.Tick(dt, input.fire);
            if (r > 0) Cue(CueKind.Cut, _showdownTarget, _showdown.Cuts);
            else if (r < 0) Cue(CueKind.Whiff, _showdownTarget, 0);
            int hits = _showdown.TakeHits();
            if (hits > 0)
            {
                Cue(CueKind.WordLanded, _showdownTarget, hits);
                if (_engine.Hurt(hits)) { Next(); return; }
            }
            if (!_showdown.Finished) return;
            string target = _showdownTarget;
            Cue(CueKind.ShowdownWon, target, _showdown.Landed);
            _engine.Press(target);
            // The showdown was the fight: no phase on top of it (the heat stays up).
            if (!_engine.Ended && _engine.InPressure) _engine.SkipPressure();
            if (target != ArgumentEngine.ClaimId) Cue(CueKind.Shatter, target, 0);
            AfterVerb();
        }

        // ---------- Lines ----------

        /// <summary>New transcript lines go to the screen: speech is said one line at a time (blocking: the debate
        /// waits); the step spelled out goes to the caption.</summary>
        void PullTranscript(bool blocking = true)
        {
            var t = _engine.Transcript;
            for (; _seen < t.Count; _seen++)
            {
                var x = t[_seen];
                if (x.speaker == Speaker.Logic) { _caption = x.text; _captionAge = 0f; continue; }
                if (x.speaker == Speaker.Opponent && x.expression.Length > 0) _expressed = true;
                string text = x.speaker == Speaker.Supporter && Expand != null ? Expand(x.text) : x.text;
                // A premise said as it comes out (or their question) flies as a statement next: it doesn't hold play.
                bool passing = x.speaker == Speaker.Opponent && IsStatementText(x.text);
                _delivery.Add(new Delivery.Line
                {
                    speaker = x.speaker, who = x.who, text = text, expression = x.expression, blocking = blocking && !passing,
                    // Nadia's words land as a quick cut-in; theirs take their time.
                    speed = x.speaker == Speaker.Nadia ? 1.7f : 1f,
                });
            }
        }

        bool IsStatementText(string text)
        {
            foreach (var p in Def.premises)
                if (p != null && (p.text == text || p.question == text)) return true;
            return false;
        }

        /// <summary>A short line of theirs (<see cref="ArgumentBarks"/>), kept in the transcript; it plays over the debate
        /// unless <paramref name="blocking"/>.</summary>
        void Bark(string id, bool blocking)
        {
            if (!Def.barks.TryGetValue(id, out var text) || string.IsNullOrEmpty(text)) return;
            PullTranscript();
            text = _engine.Line("bark." + id, text);
            _engine.Interject(Speaker.Opponent, Def.opponent, text, "");
            _seen = _engine.Transcript.Count;
            _delivery.Add(new Delivery.Line { speaker = Speaker.Opponent, who = Def.opponent, text = text, expression = "", blocking = blocking });
        }

        void Note(string key, string card = null)
        {
            string name = card;
            if (card != null)
            {
                var ev = Def.Evidence(card);
                name = card == RetortCard ? Str["screen.card.retort"] : card == RestCard ? Str["screen.card.rest"] : ev != null ? ev.name : card;
            }
            _caption = Str.Format(key, "evidence", name ?? "", "Evidence", Cap(name ?? ""), "n", BurnedFor(card).ToString(System.Globalization.CultureInfo.InvariantCulture), "opponent", Def.opponent);
            _captionAge = 0f;
        }

        void HeartCues()
        {
            int heart = _engine.HeartStep;
            if (heart > _lastHeart) Cue(CueKind.HeartUp, "", heart);
            else if (heart < _lastHeart) Cue(CueKind.HeartDown, "", heart);
            _lastHeart = heart;
        }

        void Cue(CueKind kind, string id, int value, Speaker speaker = Speaker.Logic)
        {
            _cues.Add(new Cue { kind = kind, id = id ?? "", value = value, speaker = speaker });
            _cueCounts[(int)kind]++;
        }

        readonly int[] _cueCounts = new int[64];

        /// <summary>How many times <paramref name="kind"/> has happened this battle (captures and tests: cues only last a
        /// frame).</summary>
        public int Count(CueKind kind) => _cueCounts[(int)kind];

        // ---------- Cards ----------

        void RefreshCards()
        {
            string keep = CardInHand;
            _cards.Clear();
            foreach (var e in _engine.Def.evidence) if (e != null && _engine.HasEvidence(e.id)) _cards.Add(e.id);
            bool retort = false;
            foreach (var p in _engine.Def.premises)
                if (p != null && p.retort != null && !string.IsNullOrEmpty(p.question) && _engine.Status(p.id) == PremiseStatus.Standing) retort = true;
            if (retort) _cards.Add(RetortCard);
            if (_engine.CanLetItRest) _cards.Add(RestCard);
            _card = Math.Max(0, _cards.IndexOf(keep));
            if (_cards.Count == 0) _card = -1;
        }

        // ---------- Faces ----------

        /// <summary>Their listening face while Nadia decides: the opening pose until they've shown a feeling, then
        /// neutral, or the opening pose again once they're angry (guarded, not raging).</summary>
        string IdleFace() => !_expressed || _engine.Anger >= 2 ? OpeningFace : "neutral";

        string EndFace()
        {
            switch (_engine.Outcome)
            {
                case ArgumentOutcome.Convinced:
                case ArgumentOutcome.HeardOut: return "softened";
                case ArgumentOutcome.Pressed: return Def.Face("hurt");
                case ArgumentOutcome.ComposureLost: return Def.Face(Def.composureLostExpression);
                default: return "neutral";
            }
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
