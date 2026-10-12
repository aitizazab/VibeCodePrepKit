using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Argument
{
    /// <summary>How the battle is played (docs/redesign/battle.md, round 2). The creator, 2026-10-12: "Putting it into the
    /// bullet-hell portion of the fight doesn't make the argument any more enticing, it should probably stay within its own
    /// section, with only a slight overlap. Either way, I'd have to play it to be sure." So both stay, behind this switch.</summary>
    public enum ArgumentPresenter
    {
        /// <summary>Arguing and dodging take turns: her case is on the table (her notebook; under her sky, her
        /// constellation), Nadia picks what she said and acts on it, her reply comes as a dodge, and so on.</summary>
        Sections,
        /// <summary>Milestone 1: the debate, where her statements fly across the table while her pressure comes.</summary>
        Debate,
    }

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
        /// <summary>The sections presenter's argue section: her case on the table, Nadia picks what she said and acts on it
        /// (nothing comes at her light; her patience is the clock).</summary>
        Argue,
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
        // The sections presenter.
        Select, QuoteOpen, QuoteCaught, QuoteMissed, Aside, SkyOpens, Notes, Hush,
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
        readonly ArgumentPresenter _presenter;
        readonly CaseMap _map;
        readonly Rails _rails = new Rails();
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

        public BattleFlow(ArgumentEngine engine) : this(engine, ArgumentPresenter.Sections) { }

        public BattleFlow(ArgumentEngine engine, ArgumentPresenter presenter)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _presenter = presenter;
            _seed = engine.Setup.seed == 0 ? 1UL : engine.Setup.seed;
            _debate = new Debate(engine, _seed * 31UL + 7UL);
            _map = new CaseMap(engine.Def);
            _portrait = new Portrait(OpeningFace);
            if (!_engine.Started) _engine.Setup.sections = presenter == ArgumentPresenter.Sections;
            if (!_engine.Started) _engine.Start();
            _map.Refresh(_engine);
            _selected = _map.First();
            _lastHeart = _engine.HeartStep;
            RefreshCards();
            PullTranscript();
            Enter(BattleState.Intro);
        }

        // ---------- What the view reads ----------

        public ArgumentEngine Engine => _engine;
        public ArgumentDef Def => _engine.Def;
        public Debate Debate => _debate;
        public ArgumentPresenter Presenter => _presenter;
        public bool Sections => _presenter == ArgumentPresenter.Sections;
        /// <summary>Her case on the table (the sections presenter): where each statement sits, the lines between them.</summary>
        public CaseMap Map => _map;
        /// <summary>The statement picked on her case (an index into <see cref="Map"/>).</summary>
        public int Selected => _selected;
        public string SelectedId => _selected >= 0 && _selected < _map.Count ? _map[_selected].id : ArgumentEngine.ClaimId;
        /// <summary>The argue sections so far (the sections presenter's rounds: a burned card is back after two).</summary>
        public int Turn => _turn;
        /// <summary>Rounds of the debate, or turns of the sections: what a burned card counts.</summary>
        public int Round => Sections ? _turn : _debate.Round;
        /// <summary>Seconds into this argue section, and how long her patience lasts (0: no clock).</summary>
        public float ArgueTime => _argueTime;
        public float ArgueLimit => _argueLimit;
        /// <summary>0..1: her pencil sketching what comes if Nadia says nothing (the last seconds of her patience).</summary>
        public float ArgueSketch => _argueLimit <= 0f ? 0f : Math.Max(0f, Math.Min(1f, (_argueTime - (_argueLimit - SketchSeconds)) / SketchSeconds));
        /// <summary>Her sky is open (SELECTION.md 7), and how far the ceiling has lifted (0..1).</summary>
        public bool Sky => _sky;
        public float SkyProgress => _sky ? Math.Min(1f, _skyTime / SkySeconds) : 0f;
        /// <summary>Her lines under the sky (the light moves along them in a phase).</summary>
        public Rails Rails => _rails;
        /// <summary>A quote of hers is in the line being said: its card id, and where its words run in the line (letters).</summary>
        public string QuoteId => _qLive ? _qId : "";
        public int QuoteStart => _qStart;
        public int QuoteEnd => _qEnd;
        /// <summary>Her words are being said now: {show} catches them.</summary>
        public bool QuoteOpen => _qLive && QuoteWindow;
        /// <summary>The quote just caught (its card id, for a moment), for the view's page flying to the hand.</summary>
        public string QuoteCaught => _qCaughtAge < 1.4f ? _qCaught : "";
        public float QuoteCaughtAge => _qCaughtAge;
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
        public int BurnedFor(string card) => _burnedUntil.TryGetValue(card ?? "", out int until) ? Math.Max(0, until - Round) : 0;
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
            _qCaughtAge += dt;
            if (_sky) _skyTime += dt;
            if (_cooldown > 0f) _cooldown -= dt;
            switch (_state)
            {
                case BattleState.Debate:
                case BattleState.Because:
                case BattleState.Pressure:
                case BattleState.Showdown:
                    _active += dt;
                    if (_state == BattleState.Pressure) _dodge += dt;
                    break;
                case BattleState.Argue:
                    _active += dt;
                    _argue += dt;
                    break;
                case BattleState.Over:
                    break;
                default:
                    _talk += dt;
                    break;
            }

            // The lines play in every state; a press hurries or moves them on only where talking is all there is. While
            // her words are worth holding her to, the press is for catching them instead (Pages from her notebook).
            bool talking = _state == BattleState.Intro || _state == BattleState.Talk || _state == BattleState.Ending;
            bool caughtPress = QuotePress(input.fire);
            var tock = _delivery.Tick(dt, _textSpeed, talking && input.fire && !caughtPress);
            if (tock.started)
            {
                var line = _delivery.Current;
                Cue(CueKind.LineStart, line.who, line.statement, line.speaker);
                if (line.speaker == Speaker.Opponent) _portrait.Speak(line.expression);
                StartQuote();
            }
            TickQuote(dt);
            if (tock.blips > 0) Cue(CueKind.Blip, _delivery.Current.who, tock.blips, _delivery.Current.speaker);
            if (tock.slowBlips > 0) Cue(CueKind.SlowBlip, _delivery.Current.who, tock.slowBlips, _delivery.Current.speaker);
            if (_portrait.Speaking && (!_delivery.HasLine || _delivery.LineDone || _delivery.Current.speaker != Speaker.Opponent)) _portrait.Quiet();
            _portrait.SetIdle(IdleFace());
            _portrait.Tick(dt);

            // Someone ended it from outside (a test's pilot, a debug skip): go to its end.
            if (_engine.Ended && (_state == BattleState.Debate || _state == BattleState.Argue || _state == BattleState.Because || _state == BattleState.Pressure || _state == BattleState.Showdown))
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
                case BattleState.Argue: TickArgue(dt, input); break;
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
            Enter(Sections ? BattleState.Argue : BattleState.Debate);
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
                case BattleState.Argue:
                    EnterArgue();
                    break;
                case BattleState.Pressure:
                    // Under her sky, the lines as they stand now (the phase moves the light along them).
                    if (_sky && _engine.Pressure == null)
                    {
                        _map.Refresh(_engine);
                        _map.BuildRails(_rails);
                    }
                    // The engine starts its queued phase on its next tick; start it now so her light can be placed.
                    if (_engine.Pressure == null) _engine.Tick(0.0001f, Vector2.Zero);
                    _engine.Pressure?.PlaceLight(_light);
                    _warnedPhase = _engine.Pressure;
                    _warned = _engine.Pressure != null ? _engine.Pressure.Telegraphs : 0;
                    Cue(CueKind.PressureIn, "", _engine.Pressure != null ? _engine.Pressure.Spec.calls : 0);
                    if (_engine.Pressure != null) PhaseBegins(_engine.Pressure);
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
            Act(kind, s.premise, s.claim, s.question, onWeak);
        }

        /// <summary>A verb on one of her statements (<paramref name="premise"/>, or the claim): 0 Ask (X), 1 Concede (Y), 2
        /// the card in hand (A). <paramref name="question"/>: the statement is her question (the debate's); <paramref
        /// name="onWeak"/>: the card landed on the words that matter (the sections presenter: always).</summary>
        void Act(int kind, string premise, bool claim, bool question, bool onWeak)
        {
            _actOn = premise;
            Cue(CueKind.Fire, premise, kind);
            switch (kind)
            {
                case 0:
                    if (claim) { Glance(premise, "screen.why.askClaim"); return; }
                    if (!_engine.CanAsk(premise)) { Glance(premise, "screen.why.nothingToAsk"); return; }
                    _engine.Ask(premise);
                    Cue(CueKind.Ask, premise, 0);
                    AfterVerb();
                    return;
                case 1:
                    if (claim)
                    {
                        if (_engine.CanConcedeClaim) { _holdKind = 2; _hold = 0f; _holdTarget = ArgumentEngine.ClaimId; Cue(CueKind.HoldStart, ArgumentEngine.ClaimId, 2); }
                        return;
                    }
                    if (!_engine.CanConcede(premise)) { Glance(premise, "screen.why.cantConcede"); return; }
                    _engine.Concede(premise);
                    Cue(CueKind.Concede, premise, _engine.Status(premise) == PremiseStatus.Conceded ? 1 : 0);
                    AfterVerb();
                    return;
            }
            string c = CardInHand;
            if (c.Length == 0) { Glance(premise, "screen.why.noCards"); return; }
            if (BurnedFor(c) > 0)
            {
                Cue(CueKind.Burned, c, BurnedFor(c));
                Note("screen.why.burned", c);
                return;
            }
            if (c == RetortCard)
            {
                if (claim || !_engine.CanRetort(premise)) { Glance(premise, "screen.why.retortOnly"); return; }
                _engine.Retort(premise);
                Cue(CueKind.Retort, premise, 0);
                AfterVerb();
                return;
            }
            if (c == RestCard)
            {
                if (!claim) { Glance(premise, "screen.why.restOnClaim"); return; }
                if (!_engine.LetItRest()) { Glance(premise, "screen.why.notYet"); return; }
                Cue(CueKind.Rest, ArgumentEngine.ClaimId, 0);
                AfterVerb();
                return;
            }
            if (claim) { Glance(premise, "screen.why.claimNoChallenge"); return; }
            if (question) { Glance(premise, "screen.why.questionEvidence"); return; }
            if (!onWeak) { Glance(premise, "screen.why.glance"); return; }
            var step = _engine.BeginChallenge(premise, c, _because);
            switch (step)
            {
                case ChallengeStep.Reasons:
                    _becausePremise = premise;
                    _becauseEvidence = c;
                    _becauseFocus = 0;
                    _patience = _engine.Setup.difficulty == ArgumentDifficulty.Story ? 0f : BecausePatience();
                    _patienceLeft = _patience;
                    if (!Sections) _debate.PlaceLight(_light);
                    Enter(BattleState.Because);
                    Cue(CueKind.BecauseOpen, premise, _because.Count);
                    if (_engine.CoachNow(ArgumentCoach.Because)) PullTranscript(false);
                    return;
                case ChallengeStep.NoTouch:
                case ChallengeStep.TrueOne:
                    Wrong(c, premise);
                    AfterVerb();
                    return;
                case ChallengeStep.Unbreakable:
                    Cue(CueKind.Unbreakable, premise, 0);
                    AfterVerb();
                    return;
                case ChallengeStep.Aside:
                    Cue(CueKind.Aside, premise, 0);
                    AfterVerb();
                    return;
                default:
                    Glance(premise, "screen.why.cantChallenge");
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

        void Glance(string premise, string whyKey)
        {
            Cue(CueKind.Glance, premise ?? "", 0);
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
            _burnedUntil[card] = Round + 2;
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
            _turnPending = true;
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
            // Her question in the air: one she just put, or the one standing on what Nadia acted on (her questions want an
            // answer: SELECTION.md 3's answer-me).
            var on = Def.Premise(_actOn ?? "");
            bool asked = on != null && !string.IsNullOrEmpty(on.question) && _engine.Status(on.id) == PremiseStatus.Standing;
            _actOn = null;
            var t = _engine.Transcript;
            for (int k = _seen; k < t.Count; k++)
            {
                if (t[k].speaker != Speaker.Opponent) continue;
                if (t[k].expression.Length > 0) felt = t[k].expression;
                if (IsQuestionText(t[k].text)) asked = true;
            }
            if (!_engine.Ended && !_engine.InPressure)
            {
                if (Sections) ReplyPhase(felt, asked);
                else if (felt == Def.Face("angry") && felt != "neutral" || felt == Def.Face("hurt")) _engine.QueueGesture(Math.Max(1, _engine.Heat), 4.5f);
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
            var before = _light;
            _engine.Tick(dt, move);
            if (_engine.Pressure != null)
            {
                _light = _engine.Pressure.Light;
                if (_engine.Pressure.Spec.trail) NoteMove(dt, before, _light);
            }
            WarnCues(_engine.Pressure);
            if (_engine.Ended) { Next(); return; }
            if (!_engine.InPressure)
            {
                _debate.PlaceLight(_light);
                _debate.Refresh();
                RefreshCards();
                PhaseEnds();
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
            _showdown = new Showdown(Def.showdown, _engine.Anger, target == ArgumentEngine.ClaimId, _engine.Setup.difficulty, pace, _seed + (ulong)Round * 977UL);
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
            // In the sections presenter nothing flies: what she says is said, and holds the room.
            if (Sections) return false;
            foreach (var p in Def.premises)
                if (p != null && (p.text == text || p.question == text)) return true;
            return false;
        }

        bool IsQuestionText(string text)
        {
            foreach (var p in Def.premises)
                if (p != null && !string.IsNullOrEmpty(p.question) && p.question == text) return true;
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

        // ---------- The sections presenter (docs/redesign/battle.md, round 2) ----------

        /// <summary>Her pencil sketches the coming attack over the last this-many seconds of her patience; the ceiling takes
        /// this long to lift; a quote stays catchable this long after its last word is said.</summary>
        public const float SketchSeconds = 3f, SkySeconds = 3f, QuoteGrace = 1.2f;
        /// <summary>How long her reply plays as a dodge, by its feeling (the creator: "the stretches ... were both too
        /// short"): angry or hurt, rattled, softened (with its hush), anything else.</summary>
        public const float ReplyAngry = 10f, ReplyRattled = 8f, ReplySoft = 7.5f, ReplyPlain = 7f;

        readonly int[] _notesSaid = new int[3];
        string _actOn;
        int _selected, _turn;
        float _argueTime, _argueLimit, _navHold, _skyTime, _dodge, _argue;
        bool _navFirst = true, _sky, _ringsPlayed, _skyPrompted, _argueCoached, _notesPrompted, _turnPending = true;
        // Her notes on how Nadia moved (the phases her pencil followed in).
        float _notesNear, _notesFar, _notesTime, _notesMoved;
        // The quote in the line being said.
        string _qId = "", _qCaught = "";
        int _qStart, _qEnd, _quotesSeen;
        float _qAfter, _qCaughtAge = 99f;
        bool _qLive;

        /// <summary>Seconds dodging (pressure phases) and arguing (the argue sections) so far: the sections presenter's
        /// split of <see cref="ActiveSeconds"/> (the rest of it is "Because..." and the showdown).</summary>
        public float DodgeSeconds => _dodge;
        public float ArgueSeconds => _argue;

        void EnterArgue()
        {
            // A new turn after an exchange or a phase (not when a line merely held the room).
            if (_turnPending) { _turn++; _turnPending = false; }
            _argueTime = 0f;
            _navHold = 0f;
            _navFirst = true;
            _map.Refresh(_engine);
            if (_selected < 0 || _selected >= _map.Count || !_map[_selected].visible) _selected = _map.First();
            // A statement that's down: move on to the next one standing (the claim when nothing stands).
            if (_map[_selected].status != PremiseStatus.Standing && !_map[_selected].claim) _selected = _map.First();
            RefreshCards();
            _argueLimit = PatienceToArgue();
            if (!_argueCoached)
            {
                // The coach: the sections themselves (if the opening didn't already), then asking.
                _argueCoached = true;
                bool said = _engine.CoachNow(ArgumentCoach.Argue);
                said |= _engine.CoachNow(ArgumentCoach.Ask);
                if (said) PullTranscript();
            }
        }

        /// <summary>How long she waits for Nadia to say something before she pushes (her patience): longer with more on the
        /// table, shorter once she's angry; no clock in Story or the tutorial.</summary>
        float PatienceToArgue()
        {
            if (_engine.Setup.difficulty == ArgumentDifficulty.Story || Def.heartCap < ArgumentEngine.PanicStep) return 0f;
            int standing = 0;
            for (int i = 1; i < _map.Count; i++) if (_map[i].visible && _map[i].status == PremiseStatus.Standing) standing++;
            float limit = Math.Min(34f, 20f + 2.5f * standing);
            if (_engine.Anger >= 2) limit *= 0.8f;
            return limit;
        }

        void TickArgue(float dt, in BattleInput input)
        {
            if (_delivery.Blocking) { Enter(BattleState.Talk); return; }
            // Her patience runs while nothing is being said.
            if (!_delivery.HasLine || _delivery.LineDone) _argueTime += dt;
            if (_argueLimit > 0f && _argueTime >= _argueLimit)
            {
                // Nadia said nothing: she pushes (her temper rises, and the attack she was sketching comes).
                ResetHold();
                _engine.AddHeat(1);
                _engine.Note(Str.Format("logic.idle", "opponent", Def.opponent));
                Bark(ArgumentBarks.Idle, false);
                Cue(CueKind.Idle, "", _engine.Heat);
                _engine.QueuePhase(_engine.Heat, false);
                Next();
                return;
            }

            // Picking what she said: the stick steps from statement to statement; the mouse points at one.
            var m = input.move;
            if (m.LengthSquared() > 0.25f)
            {
                _navHold -= dt;
                if (_navHold <= 0f)
                {
                    int next = _map.Step(_selected, m);
                    if (next != _selected) { _selected = next; Cue(CueKind.Select, SelectedId, _selected); }
                    _navHold = _navFirst ? 0.3f : 0.16f;
                    _navFirst = false;
                }
            }
            else { _navHold = 0f; _navFirst = true; }
            if (input.hasPoint)
            {
                int at = _map.Nearest(input.point, 0.6f);
                if (at >= 0 && at != _selected) { _selected = at; Cue(CueKind.Select, SelectedId, _selected); }
            }

            if (input.cycle != 0 && _cards.Count > 1)
            {
                _card = (_card + (input.cycle > 0 ? 1 : _cards.Count - 1)) % _cards.Count;
                Cue(CueKind.Cycle, CardInHand, _card);
            }

            var e = _map[_selected];
            string target = e.claim ? ArgumentEngine.ClaimId : e.id;
            if (_holdKind == 1)
            {
                if (!input.pressHeld || target != _holdTarget) { ResetHold(); Cue(CueKind.HoldCancel, "", 1); }
                else if ((_hold += dt) >= PressHold) { StartShowdown(_holdTarget); return; }
            }
            else if (_holdKind == 2)
            {
                if (!input.concedeHeld || !e.claim) { ResetHold(); Cue(CueKind.HoldCancel, "", 2); }
                else if ((_hold += dt) >= YieldHold)
                {
                    ResetHold();
                    if (_engine.ConcedeClaim()) { Cue(CueKind.Yield, ArgumentEngine.ClaimId, 0); AfterVerb(); return; }
                }
            }
            else if (input.pressHeld && _cooldown <= 0f)
            {
                if (_engine.CanPress(target)) { _holdKind = 1; _hold = 0f; _holdTarget = target; Cue(CueKind.HoldStart, target, 1); }
                else { Note("screen.why.cantPress"); _cooldown = 0.6f; }
            }

            if (_cooldown > 0f || _holdKind != 0) return;
            int kind = input.ask ? 0 : input.concede ? 1 : input.fire ? 2 : -1;
            if (kind < 0) return;
            _shotsThisRound++;
            _cooldown = ShotCooldown;
            Act(kind, target, e.claim, false, true);
        }

        /// <summary>Her reply, played as a dodge (SELECTION.md 2: a gesture for every reply, now every exchange's own
        /// section): angry or hurt, a real phase; rattled, a shorter one; softened (a concession: her care), a hush that
        /// passes a light holding still; her question, a wave that passes a light that moves; anything else, a short one.</summary>
        void ReplyPhase(string felt, bool asked)
        {
            int heat = _engine.Heat;
            if (felt.Length > 0 && (felt == Def.Face("angry") && felt != "neutral" || felt == Def.Face("hurt")))
                _engine.QueueGesture(Math.Max(1, heat), ReplyAngry);
            else if (felt == "rattled") _engine.QueueGesture(heat, ReplyRattled);
            else if (felt == "softened") _engine.QueueGesture(Math.Max(0, heat - 1), ReplySoft, Def.hush ? 1 : 0, 0);
            else _engine.QueueGesture(heat, ReplyPlain, 0, Def.hush && asked ? 1 : 0);
        }

        /// <summary>A phase begins: the three rings' ring, her hush the first time ("Shh."), the prompts that teach.</summary>
        void PhaseBegins(PressurePhase phase)
        {
            var spec = phase.Spec;
            if (spec.calls > 0) { _ringsPlayed = true; Bark(ArgumentBarks.Ring, false); }
            if (!Sections) return;
            if (spec.hush > 0)
            {
                Cue(CueKind.Hush, "", spec.hush);
                if (spec.hushTeach) { Bark(ArgumentBarks.Hush, false); Note("screen.prompt.hush"); }
            }
            else if (spec.answer > 0 && spec.answerTeach) Note("screen.prompt.answer");
            else if (spec.sky && !_skyPrompted) { _skyPrompted = true; Note("screen.prompt.sky"); }
            else if (spec.trail && !_notesPrompted) { _notesPrompted = true; Note("screen.prompt.notes"); }
        }

        /// <summary>A phase is over: after the three rings the ceiling lifts (her sky); after a phase her pencil followed
        /// in, she says how Nadia moved (once for each way, never in Story).</summary>
        void PhaseEnds()
        {
            _turnPending = true;
            if (!Sections) return;
            if (_ringsPlayed && !_sky && Def.sky) OpenSky();
            if (_notesTime > 1f)
            {
                float near = _notesNear / _notesTime, far = _notesFar / _notesTime, moved = _notesMoved / _notesTime;
                _notesNear = _notesFar = _notesTime = _notesMoved = 0f;
                if (_engine.Setup.difficulty == ArgumentDifficulty.Story) return;
                int kind = far > 0.5f ? 0 : near > 0.45f ? 1 : moved > 1.3f ? 2 : -1;
                if (kind < 0 || _notesSaid[kind] > 0) return;
                _notesSaid[kind]++;
                Bark(kind == 0 ? ArgumentBarks.NotesFar : kind == 1 ? ArgumentBarks.NotesNear : ArgumentBarks.NotesRestless, false);
                Cue(CueKind.Notes, "", kind);
            }
        }

        /// <summary>Her notes: where the light kept to (near her, her side of the table on the left; or far from her) and
        /// how much it moved.</summary>
        void NoteMove(float dt, Vector2 from, Vector2 to)
        {
            _notesTime += dt;
            if (to.X < -1f) _notesNear += dt;
            else if (to.X > 1f) _notesFar += dt;
            _notesMoved += Vector2.Distance(from, to);
        }

        /// <summary>The ceiling lifts (SELECTION.md 7): her case becomes a constellation, and every phase from now on is
        /// played on its lines.</summary>
        void OpenSky()
        {
            _sky = true;
            _skyTime = 0f;
            _map.Refresh(_engine);
            _map.BuildRails(_rails);
            _engine.OpenSky(_rails);
            Cue(CueKind.SkyOpens, "", 0);
            Bark(ArgumentBarks.Sky, true);
        }

        // ---------- Her own words (SELECTION.md 6, "Pages from her notebook") ----------

        /// <summary>A line of hers starts: if it has words worth holding her to (a quote card not yet caught), note where
        /// they run.</summary>
        void StartQuote()
        {
            _qLive = false;
            var line = _delivery.Current;
            if (line.speaker != Speaker.Opponent) return;
            string plain = _delivery.Typewriter.Plain;
            foreach (var ev in Def.evidence)
            {
                if (ev == null || !ev.IsQuote || string.IsNullOrEmpty(ev.words) || _engine.Caught(ev.id)) continue;
                int at = plain.IndexOf(ev.words, StringComparison.OrdinalIgnoreCase);
                if (at < 0) continue;
                _qId = ev.id;
                _qStart = at;
                _qEnd = at + ev.words.Length;
                _qAfter = 0f;
                _qLive = true;
                return;
            }
        }

        bool QuoteWindow => _qLive && _delivery.HasLine && _delivery.Typewriter.Shown > _qStart && _qAfter <= QuoteGrace;

        /// <summary>A press while her words are worth catching: in the window it catches them; too soon, it's lost (the first
        /// time ever, it's only ignored: nobody knows yet); after the window, it's an ordinary press. True if it was used.</summary>
        bool QuotePress(bool fire)
        {
            if (!_qLive || !_delivery.HasLine) return false;
            if (_engine.Setup.difficulty == ArgumentDifficulty.Story && QuoteWindow) { CatchQuote(); return fire; }
            if (!fire) return false;
            if (QuoteWindow) { CatchQuote(); return true; }
            if (_delivery.Typewriter.Shown <= _qStart)
            {
                if (_quotesSeen == 0) return true;
                _qLive = false;
                Cue(CueKind.QuoteMissed, _qId, 0);
                Note("screen.quote.early");
                return true;
            }
            return false;
        }

        void TickQuote(float dt)
        {
            if (!_qLive) return;
            if (!_delivery.HasLine) { QuoteGone(); return; }
            int shown = _delivery.Typewriter.Shown;
            if (shown > _qStart && !_openCued)
            {
                _openCued = true;
                Cue(CueKind.QuoteOpen, _qId, 0);
                if (_engine.CoachNow(ArgumentCoach.Quote)) PullTranscript(false);
            }
            if (shown >= _qEnd) _qAfter += dt;
            if (_qAfter > QuoteGrace) QuoteGone();
        }

        bool _openCued;

        void QuoteGone()
        {
            if (_qLive && _openCued) { _quotesSeen++; Cue(CueKind.QuoteMissed, _qId, 1); }
            _qLive = false;
            _openCued = false;
        }

        void CatchQuote()
        {
            string id = _qId;
            _qLive = false;
            _openCued = false;
            _quotesSeen++;
            if (!_engine.CatchQuote(id)) return;
            _qCaught = id;
            _qCaughtAge = 0f;
            Cue(CueKind.QuoteCaught, id, 0);
            // The transcript keeps its sentence; the caption says it plainly.
            PullTranscript(false);
            var ev = Def.Evidence(id);
            _caption = Str.Format("screen.quote.caught", "quote", ev != null ? ev.words : id, "opponent", Def.opponent);
            _captionAge = 0f;
            RefreshCards();
        }

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
