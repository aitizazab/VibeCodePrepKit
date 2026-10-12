using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;
using Vec = System.Numerics.Vector2;

namespace JamKit.Argument
{
    /// <summary>
    /// The real-time battle on screen (docs/redesign/battle.md, "Presentation"), UI Toolkit built in code on the shell's
    /// HUD layer, laid out on the 1920 x 1080 reference. The look is the sprites' own: ink on paper with screentone,
    /// Lena's wine red for her feeling, Nadia's gold for her light.
    ///
    /// Left: the opponent, large, her face following each line (two layers cross-fading, each boiled), a glow behind her
    /// that warms with her temper. Right: the table (the arena) where her statements fly as she says them, their weak
    /// words glowing gold, her pressure inked in wine after a graphite sketch of where it will be, and Nadia's light.
    /// Above it, the case (the claim and a chip per premise); under it, the card in Nadia's hand and her heart monitor;
    /// along the bottom, the dialogue box, where every line is typed one at a time. Overlays: "Because...", the case
    /// file, the end card; the showdown plays on the table; composure lost closes the room in. A camera on the whole
    /// stage drifts, shakes on hits and leans in on the moments that matter.
    ///
    /// Text never clips: the dialogue box and every card size to their text (and shrink the type a step for long
    /// lines), statements shrink to fit the table, and the case file scrolls. Nothing is allocated per frame except where
    /// text changes and the arena's mesh (redrawn each frame).
    ///
    /// The sections presenter (round 2): in the argue section the table shows her case as a page of her notebook (each
    /// statement where she put it, pencil lines between, the one picked in full above the table); her replies come as a
    /// dodge, her line in a speech bubble over her; words worth holding her to are underlined as she says them; hush
    /// closes in soft and dashed (and the light shows whether it's holding still); her pencil follows the light; after
    /// the three rings the ceiling lifts and the table is her sky, her case a constellation the light moves along.
    /// </summary>
    public sealed class ArgumentStage
    {
        public const string RootName = "argument-stage";
        public const float ArenaX = 680f, ArenaY = 150f, ArenaW = 1200f, ArenaH = 640f;
        /// <summary>Pixels per surface unit (the arena is 6 units across).</summary>
        public const float Px = ArenaW / (2f * PressurePhase.HalfWidth);
        const int StatementPool = 6, WordPool = 10, ShowdownPool = 12, SlipPool = 2, ChipPool = 12, ReasonPool = 5, HandSide = 2, MapPool = 12;

        public static readonly Color Ink = new Color(0.07f, 0.06f, 0.08f);
        public static readonly Color Paper = new Color(0.94f, 0.91f, 0.85f);
        public static readonly Color PaperInk = new Color(0.11f, 0.09f, 0.12f);
        public static readonly Color Wine = new Color(0.62f, 0.11f, 0.18f);
        public static readonly Color WineBright = new Color(0.86f, 0.22f, 0.3f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.38f);
        public static readonly Color Graphite = new Color(0.62f, 0.62f, 0.66f);
        public static readonly Color Dim = new Color(0.62f, 0.59f, 0.66f);
        public static readonly Color MagsBlue = new Color(0.55f, 0.72f, 0.95f);

        public readonly VisualElement Root;
        readonly VisualElement _black, _camera, _backdrop, _grain, _arena, _arenaBg, _arenaTint, _arenaDraw, _statementsLayer, _wordsLayer;
        readonly VisualElement _showdownLayer, _slipLayer, _monitor, _panic, _flash;
        readonly VisualElement _portraitBack, _portraitFront;
        readonly Label _name, _temper, _caption, _boilLabel, _placeholder;
        readonly VisualElement _claimCard, _chips;
        readonly Label _claimLabel, _claimText, _bpm;
        readonly VisualElement _hand, _handCard;
        readonly Label _handName, _handNote, _handTag, _hints;
        readonly Label[] _handPrev = new Label[HandSide], _handNext = new Label[HandSide];
        readonly VisualElement _dialogue, _speakerTag;
        readonly Label _speaker, _line, _more;
        readonly VisualElement _because, _patience, _patienceFill;
        readonly Label _becauseHeading, _becausePatienceLabel;
        readonly VisualElement[] _reasonCards = new VisualElement[ReasonPool];
        readonly Label[] _reasonTexts = new Label[ReasonPool];
        readonly Label _showdownPrompt, _stateBanner, _endClaim;
        readonly VisualElement _caseFile, _end, _continue;
        readonly ScrollView _caseScroll;
        readonly Label _caseTitle, _endTitle, _endBody, _endCost;
        // The sections presenter: her case on the table, the statement picked, her speech bubble, her own words, her sky.
        readonly VisualElement _skyBg, _mapLayer, _focus, _bubble, _quotePage;
        readonly Label _focusTag, _focusText, _focusSaid, _bubbleText, _quotePrompt, _quotePageText;
        readonly VisualElement[] _mapCards = new VisualElement[MapPool];
        readonly Label[] _mapTexts = new Label[MapPool];
        readonly int[] _mapKey = new int[MapPool];
        readonly Vector2[] _mapAt = new Vector2[MapPool];
        /// <summary>The key line while Nadia argues (the host fills it with the player's keys), and the prompt under a quote.</summary>
        public string ArgueHints = "", QuotePrompt = "";

        sealed class StatementView
        {
            public VisualElement row;
            public Label pre, weak, post, tag;
            public int serial;
            public float font;
            public bool used;
        }

        readonly StatementView[] _statements = new StatementView[StatementPool];
        readonly Label[] _words = new Label[WordPool];
        readonly Label[] _showWords = new Label[ShowdownPool];
        readonly Label[] _slipNames = new Label[SlipPool];
        readonly VisualElement[] _chipBoxes = new VisualElement[ChipPool];
        readonly Label[] _chipTexts = new Label[ChipPool];

        // The face: two layers cross-fading, each with its own boil.
        readonly ArgumentBoilLayer _boilFront = new ArgumentBoilLayer(), _boilBack = new ArgumentBoilLayer();
        Texture2D _frontTex, _backTex;
        string _frontFace = "", _backFace = "", _who = "";
        float _fade = 1f, _lift, _talkBob;
        int _faceChanges = -1;
        float _boilLabelFor;
        bool _boilLabelAlways;

        // What the frame is drawing.
        BattleFlow _flow;
        float _time, _shake, _punch, _flashA, _flashRed, _monitorPhase;
        readonly float[] _ecg = new float[180];
        int _ecgHead;
        string _shownLine = "", _shownSpeaker = "", _shownClaim = "", _shownCaption = "", _shownHints = "";
        int _shownCard = -2, _shownCardsVersion = -1, _shownBecause = -1, _shownFocus = -1;
        float _lineFont = 30f;
        bool _endShown, _caseShown;

        /// <summary>A reason card clicked (its index), and Continue clicked.</summary>
        public Action<int> ReasonClicked;
        public Action ContinueClicked;
        /// <summary>The pointer is over the table: where, in surface units (null if not over it).</summary>
        public bool PointerOnTable => _pointerOnTable;
        public Vec PointerUnits => _pointerUnits;
        bool _pointerOnTable;
        Vec _pointerUnits;

        public ArgumentStage(VisualElement parent)
        {
            Root = new VisualElement { name = RootName, pickingMode = PickingMode.Ignore };
            Root.AddToClassList(UiHider.KeepClass);
            ArgueUi.Fill(Root);
            parent.Add(Root);

            _black = Box(Root, "argument-black", PickingMode.Position);
            ArgueUi.Fill(_black);
            _black.style.backgroundColor = ArgueUi.Black;
            _black.style.opacity = 0f;

            _camera = Box(Root, "argument-camera", PickingMode.Ignore);
            ArgueUi.Fill(_camera);
            _camera.style.opacity = 0f;
            _camera.style.transformOrigin = new TransformOrigin(Length.Percent(30), Length.Percent(45));

            // The backdrop: her glow and the paper grain.
            _backdrop = Box(_camera, "argument-backdrop", PickingMode.Ignore);
            ArgueUi.Fill(_backdrop);
            _backdrop.generateVisualContent += DrawBackdrop;
            _grain = Box(_camera, "argument-grain", PickingMode.Ignore);
            ArgueUi.Fill(_grain);
            _grain.style.backgroundImage = new StyleBackground(StageArt.Grain());
            _grain.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat));
            _grain.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(new Length(256f), new Length(256f)));

            // ---------- The opponent ----------
            _portraitBack = Portrait("argument-portrait-back");
            _portraitFront = Portrait("argument-portrait");
            _name = Text(_camera, "", ArgumentArt.Display, 58, Paper, "argument-name");
            ArgueUi.Place(_name, 44, 22, 600, 76);
            _name.style.textShadow = Shadow(3f);
            _temper = Text(_camera, "", ArgumentArt.Body, 22, Dim, "argument-temper");
            ArgueUi.Place(_temper, 48, 96, 600, 30);
            _boilLabel = Text(_camera, "", ArgumentArt.Body, 17, Dim, "argument-boil-label");
            ArgueUi.Place(_boilLabel, 24, 0, 640, 24);
            _boilLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            _boilLabel.style.display = DisplayStyle.None;
            _placeholder = Text(_camera, "", ArgumentArt.BodyItalic, 16, WineBright, "argument-placeholder");
            ArgueUi.Place(_placeholder, 1360, 4, 520, 22);
            _placeholder.style.unityTextAlign = TextAnchor.UpperRight;

            // ---------- The case (above the table) ----------
            _claimCard = Card(_camera, "argument-claim", Paper, Ink);
            ArgueUi.Place(_claimCard, ArenaX, 22, 560, 0);
            _claimCard.style.paddingLeft = _claimCard.style.paddingRight = 18;
            _claimCard.style.paddingTop = 6;
            _claimCard.style.paddingBottom = 8;
            _claimLabel = Text(_claimCard, "", ArgumentArt.BodyBold, 15, Wine);
            _claimLabel.style.letterSpacing = 3f;
            _claimText = Text(_claimCard, "", ArgumentArt.Display, 30, PaperInk, "argument-claim-text");
            _chips = Box(_camera, "argument-chips", PickingMode.Ignore);
            ArgueUi.Place(_chips, ArenaX + 580, 26, ArenaW - 580, 112);
            _chips.style.flexDirection = FlexDirection.Row;
            _chips.style.flexWrap = Wrap.Wrap;
            _chips.style.alignContent = Align.FlexStart;
            for (int i = 0; i < ChipPool; i++)
            {
                var b = Card(_chips, "argument-chip-" + i, new Color(0.13f, 0.11f, 0.15f, 0.95f), new Color(0.35f, 0.31f, 0.4f));
                b.style.marginRight = 8;
                b.style.marginBottom = 8;
                b.style.paddingLeft = b.style.paddingRight = 10;
                b.style.paddingTop = b.style.paddingBottom = 3;
                b.style.display = DisplayStyle.None;
                _chipBoxes[i] = b;
                _chipTexts[i] = Text(b, "", ArgumentArt.BodyBold, 17, Paper);
            }

            // ---------- The table ----------
            _arena = Box(_camera, "argument-table", PickingMode.Position);
            ArgueUi.Place(_arena, ArenaX, ArenaY, ArenaW, ArenaH);
            _arena.style.overflow = Overflow.Hidden;
            ArgueUi.Border(_arena, new Color(0.02f, 0.02f, 0.03f), 3f);
            ArgueUi.Round(_arena, 6f);
            _arena.RegisterCallback<PointerMoveEvent>(e => Pointer(e.localPosition));
            _arena.RegisterCallback<PointerLeaveEvent>(_ => _pointerOnTable = false);
            _arenaBg = Box(_arena, "argument-table-art", PickingMode.Ignore);
            ArgueUi.Fill(_arenaBg);
            _arenaBg.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
            // Her sky: over the table once the ceiling lifts (the sections presenter).
            _skyBg = Box(_arena, "argument-sky", PickingMode.Ignore);
            ArgueUi.Fill(_skyBg);
            _skyBg.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
            _skyBg.style.opacity = 0f;
            _skyBg.style.display = DisplayStyle.None;
            _arenaTint = Box(_arena, "argument-table-tint", PickingMode.Ignore);
            ArgueUi.Fill(_arenaTint);
            _arenaTint.style.backgroundColor = new Color(0f, 0f, 0f, 1f);
            _arenaTint.style.opacity = 0.25f;
            _wordsLayer = Layer(_arena, "argument-words");
            for (int i = 0; i < WordPool; i++)
            {
                var l = Text(_wordsLayer, "", ArgumentArt.Display, 34, Wine);
                l.style.position = Position.Absolute;
                l.style.whiteSpace = WhiteSpace.NoWrap;
                l.style.rotate = new Rotate(new Angle(-90f, AngleUnit.Degree));
                l.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                l.style.letterSpacing = 6f;
                l.style.display = DisplayStyle.None;
                _words[i] = l;
            }
            _arenaDraw = Layer(_arena, "argument-table-draw");
            _arenaDraw.generateVisualContent += DrawArena;
            // Her case, written where she put it (the sections presenter's argue section).
            _mapLayer = Layer(_arena, "argument-case-map");
            for (int i = 0; i < MapPool; i++)
            {
                var c = Card(_mapLayer, "argument-map-" + i, new Color(Paper.r, Paper.g, Paper.b, 0.94f), Graphite);
                c.style.position = Position.Absolute;
                c.style.maxWidth = 240;
                c.style.paddingLeft = c.style.paddingRight = 9;
                c.style.paddingTop = c.style.paddingBottom = 4;
                c.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
                c.style.display = DisplayStyle.None;
                _mapCards[i] = c;
                var t = Text(c, "", ArgumentArt.BodyBold, 19, PaperInk);
                t.style.whiteSpace = WhiteSpace.Normal;
                t.style.unityTextAlign = TextAnchor.MiddleCenter;
                _mapTexts[i] = t;
                _mapKey[i] = -1;
            }
            // The statement picked, in full, above the table (where the debate's claim and chips were).
            _focus = Card(_camera, "argument-focus", new Color(0.05f, 0.045f, 0.06f, 0.92f), Gold);
            ArgueUi.Place(_focus, ArenaX, 14, ArenaW, 0);
            _focus.style.paddingLeft = _focus.style.paddingRight = 18;
            _focus.style.paddingTop = 6;
            _focus.style.paddingBottom = 8;
            _focus.style.display = DisplayStyle.None;
            _focusTag = Text(_focus, "", ArgumentArt.BodyBold, 14, Gold, "argument-focus-tag");
            _focusTag.style.letterSpacing = 2f;
            _focusText = Text(_focus, "", ArgumentArt.Body, 22, Paper, "argument-focus-text");
            _focusText.style.whiteSpace = WhiteSpace.Normal;
            _focusSaid = Text(_focus, "", ArgumentArt.BodyItalic, 17, new Color(1f, 0.78f, 0.8f), "argument-focus-said");
            _focusSaid.style.whiteSpace = WhiteSpace.Normal;
            _statementsLayer = Layer(_arena, "argument-statements");
            for (int i = 0; i < StatementPool; i++) _statements[i] = MakeStatement(i);
            _slipLayer = Layer(_arena, "argument-slips");
            for (int i = 0; i < SlipPool; i++)
            {
                var l = Text(_slipLayer, "", ArgumentArt.BodyItalic, 20, Paper);
                l.style.position = Position.Absolute;
                l.style.whiteSpace = WhiteSpace.NoWrap;
                l.style.textShadow = Shadow(2f);
                l.style.display = DisplayStyle.None;
                _slipNames[i] = l;
            }
            _showdownLayer = Layer(_arena, "argument-showdown");
            for (int i = 0; i < ShowdownPool; i++)
            {
                var l = Text(_showdownLayer, "", ArgumentArt.Display, 46, WineBright);
                l.style.position = Position.Absolute;
                l.style.whiteSpace = WhiteSpace.NoWrap;
                l.style.textShadow = Shadow(4f);
                l.style.display = DisplayStyle.None;
                _showWords[i] = l;
            }
            // The end beat: her claim, alone on the table, and what the ending does to it.
            _endClaim = Text(_arena, "", ArgumentArt.Display, 44, Paper, "argument-end-claim");
            ArgueUi.Place(_endClaim, 80, ArenaH * 0.5f - 60f, ArenaW - 160, 0);
            _endClaim.style.unityTextAlign = TextAnchor.MiddleCenter;
            _endClaim.style.textShadow = Shadow(4f);
            _endClaim.style.display = DisplayStyle.None;
            _showdownPrompt = Text(_arena, "", ArgumentArt.BodyItalic, 24, Gold, "argument-showdown-prompt");
            ArgueUi.Place(_showdownPrompt, 24, ArenaH - 52, ArenaW - 48, 40);
            _showdownPrompt.style.unityTextAlign = TextAnchor.MiddleCenter;
            _showdownPrompt.style.display = DisplayStyle.None;
            _stateBanner = Text(_arena, "", ArgumentArt.Display, 30, Gold, "argument-banner");
            ArgueUi.Place(_stateBanner, 0, 14, ArenaW, 44);
            _stateBanner.style.unityTextAlign = TextAnchor.MiddleCenter;
            _stateBanner.style.textShadow = Shadow(3f);
            _caption = Text(_arena, "", ArgumentArt.BodyItalic, 22, Gold, "argument-caption");
            ArgueUi.Place(_caption, 24, 0, ArenaW - 48, 0);
            _caption.style.top = StyleKeyword.Auto;
            _caption.style.bottom = 10;
            _caption.style.backgroundColor = new Color(0f, 0f, 0f, 0.62f);
            _caption.style.paddingLeft = _caption.style.paddingRight = 12;
            _caption.style.paddingTop = _caption.style.paddingBottom = 4;
            ArgueUi.Round(_caption, 6f);

            // ---------- Under the table: the hand, the monitor, the keys ----------
            _hand = Box(_camera, "argument-hand", PickingMode.Ignore);
            ArgueUi.Place(_hand, ArenaX, ArenaY + ArenaH + 10, 920, 0);
            _hand.style.flexDirection = FlexDirection.Row;
            _hand.style.alignItems = Align.Center;
            for (int i = HandSide - 1; i >= 0; i--) _handPrev[i] = HandSide_(_hand, "argument-hand-prev-" + i);
            _handCard = Card(_hand, "argument-hand-card", Paper, Ink);
            _handCard.style.width = 440;
            _handCard.style.minHeight = 72;
            _handCard.style.marginLeft = _handCard.style.marginRight = 12;
            _handCard.style.paddingLeft = _handCard.style.paddingRight = 16;
            _handCard.style.paddingTop = _handCard.style.paddingBottom = 6;
            _handTag = Text(_handCard, "", ArgumentArt.BodyBold, 14, Wine);
            _handTag.style.letterSpacing = 2f;
            _handName = Text(_handCard, "", ArgumentArt.Display, 26, PaperInk, "argument-hand-name");
            _handNote = Text(_handCard, "", ArgumentArt.BodyItalic, 18, new Color(0.3f, 0.27f, 0.3f), "argument-hand-note");
            for (int i = 0; i < HandSide; i++) _handNext[i] = HandSide_(_hand, "argument-hand-next-" + i);
            _hints = Text(_camera, "", ArgumentArt.Body, 18, Dim, "argument-hints");
            ArgueUi.Place(_hints, ArenaX, ArenaY + ArenaH + 104, ArenaW, 0);
            _monitor = Box(_camera, "argument-monitor", PickingMode.Ignore);
            ArgueUi.Place(_monitor, ArenaX + ArenaW - 250, ArenaY + ArenaH + 8, 250, 84);
            _monitor.generateVisualContent += DrawMonitor;
            _bpm = Text(_monitor, "", ArgumentArt.Display, 30, Gold, "argument-bpm");
            ArgueUi.Place(_bpm, 0, 2, 244, 40);
            _bpm.style.unityTextAlign = TextAnchor.UpperRight;
            _bpm.style.textShadow = Shadow(2f);

            // ---------- The dialogue box ----------
            _dialogue = Card(_camera, "argument-dialogue", Paper, Ink);
            _dialogue.style.position = Position.Absolute;
            _dialogue.style.left = 30;
            _dialogue.style.width = 1860;
            _dialogue.style.bottom = 14;
            _dialogue.style.minHeight = 118;
            _dialogue.style.paddingLeft = _dialogue.style.paddingRight = 30;
            _dialogue.style.paddingTop = 26;
            _dialogue.style.paddingBottom = 16;
            ArgueUi.Border(_dialogue, Ink, 3f);
            _speakerTag = Card(_dialogue, "argument-speaker-tag", Wine, Ink);
            _speakerTag.style.position = Position.Absolute;
            _speakerTag.style.left = 22;
            _speakerTag.style.top = -22;
            _speakerTag.style.paddingLeft = _speakerTag.style.paddingRight = 14;
            _speakerTag.style.paddingTop = _speakerTag.style.paddingBottom = 2;
            _speaker = Text(_speakerTag, "", ArgumentArt.Display, 24, Paper, "argument-speaker");
            _line = Text(_dialogue, "", ArgumentArt.Body, 30, PaperInk, "argument-line");
            _more = Text(_dialogue, "", ArgumentArt.Body, 22, Wine, "argument-more");
            _more.style.position = Position.Absolute;
            _more.style.right = 18;
            _more.style.bottom = 6;

            // Her speech bubble, over her (her lines while Nadia dodges or argues: the creator's note on SELECTION.md 2).
            _bubble = Card(_camera, "argument-bubble", Paper, Ink);
            ArgueUi.Place(_bubble, 30, 12, 620, 0);
            _bubble.style.paddingLeft = _bubble.style.paddingRight = 20;
            _bubble.style.paddingTop = _bubble.style.paddingBottom = 10;
            ArgueUi.Border(_bubble, Ink, 3f);
            ArgueUi.Round(_bubble, 18f);
            _bubble.style.display = DisplayStyle.None;
            var tail = Box(_bubble, "argument-bubble-tail", PickingMode.Ignore);
            tail.style.position = Position.Absolute;
            tail.style.left = 90;
            tail.style.bottom = -13;
            tail.style.width = 22;
            tail.style.height = 22;
            tail.style.backgroundColor = Paper;
            tail.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            ArgueUi.Border(tail, Ink, 3f);
            tail.style.borderTopWidth = 0f;
            tail.style.borderLeftWidth = 0f;
            _bubbleText = Text(_bubble, "", ArgumentArt.Body, 23, PaperInk, "argument-bubble-text");
            _bubbleText.style.whiteSpace = WhiteSpace.Normal;

            // Her own words: the prompt while they're said, and the page that flies to the hand when they're caught.
            _quotePrompt = Text(_camera, "", ArgumentArt.BodyBold, 24, Gold, "argument-quote-prompt");
            _quotePrompt.style.position = Position.Absolute;
            _quotePrompt.style.left = 60;
            _quotePrompt.style.bottom = 150;
            _quotePrompt.style.textShadow = Shadow(3f);
            _quotePrompt.style.display = DisplayStyle.None;
            _quotePage = Card(_camera, "argument-quote-page", new Color(0.98f, 0.95f, 0.86f), Wine);
            _quotePage.style.position = Position.Absolute;
            _quotePage.style.paddingLeft = _quotePage.style.paddingRight = 14;
            _quotePage.style.paddingTop = _quotePage.style.paddingBottom = 6;
            _quotePage.style.display = DisplayStyle.None;
            _quotePageText = Text(_quotePage, "", ArgumentArt.BodyItalic, 24, Wine, "argument-quote-page-text");

            // ---------- Overlays ----------
            _because = Card(_camera, "argument-because", new Color(0.04f, 0.035f, 0.05f, 0.94f), Gold);
            ArgueUi.Place(_because, ArenaX + 40, ArenaY + 18, ArenaW - 80, 0);
            _because.style.paddingLeft = _because.style.paddingRight = 26;
            _because.style.paddingTop = 16;
            _because.style.paddingBottom = 20;
            _because.style.display = DisplayStyle.None;
            _patience = Box(_because, "argument-patience", PickingMode.Ignore);
            _patience.style.height = 8;
            _patience.style.marginBottom = 4;
            _patience.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            ArgueUi.Round(_patience, 4f);
            _patienceFill = Box(_patience, "argument-patience-fill", PickingMode.Ignore);
            _patienceFill.style.height = 8;
            _patienceFill.style.backgroundColor = WineBright;
            ArgueUi.Round(_patienceFill, 4f);
            _becausePatienceLabel = Text(_because, "", ArgumentArt.BodyItalic, 17, Dim);
            _becauseHeading = Text(_because, "", ArgumentArt.Display, 28, Paper, "argument-because-heading");
            _becauseHeading.style.marginTop = 6;
            _becauseHeading.style.marginBottom = 10;
            for (int i = 0; i < ReasonPool; i++)
            {
                var c = Card(_because, "argument-reason-" + i, Paper, new Color(0.3f, 0.27f, 0.32f));
                c.style.marginBottom = 10;
                c.style.paddingLeft = c.style.paddingRight = 20;
                c.style.paddingTop = c.style.paddingBottom = 10;
                int index = i;
                c.pickingMode = PickingMode.Position;
                c.RegisterCallback<ClickEvent>(_ => ReasonClicked?.Invoke(index));
                c.style.display = DisplayStyle.None;
                _reasonCards[i] = c;
                _reasonTexts[i] = Text(c, "", ArgumentArt.Body, 27, PaperInk);
            }

            _caseFile = Card(Root, "argument-case-file", new Color(0.03f, 0.028f, 0.04f, 0.97f), Gold);
            ArgueUi.Place(_caseFile, 160, 70, 1600, 940);
            _caseFile.style.paddingLeft = _caseFile.style.paddingRight = 34;
            _caseFile.style.paddingTop = 20;
            _caseFile.style.display = DisplayStyle.None;
            _caseTitle = Text(_caseFile, "", ArgumentArt.Display, 40, Gold, "argument-case-title");
            _caseScroll = new ScrollView(ScrollViewMode.Vertical) { name = "argument-case-scroll" };
            _caseScroll.style.height = 840;
            _caseScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _caseFile.Add(_caseScroll);

            _end = Card(Root, "argument-end", Paper, Ink);
            ArgueUi.Place(_end, 460, 170, 1000, 0);
            _end.style.paddingLeft = _end.style.paddingRight = 48;
            _end.style.paddingTop = _end.style.paddingBottom = 34;
            ArgueUi.Border(_end, Ink, 4f);
            _end.style.display = DisplayStyle.None;
            _endTitle = Text(_end, "", ArgumentArt.Display, 72, Wine, "argument-end-title");
            _endBody = Text(_end, "", ArgumentArt.Body, 29, PaperInk, "argument-end-body");
            _endBody.style.marginTop = 8;
            _endCost = Text(_end, "", ArgumentArt.BodyBold, 26, Wine, "argument-end-cost");
            _endCost.style.marginTop = 14;
            _continue = Card(_end, "argument-continue", Ink, Gold);
            _continue.style.marginTop = 26;
            _continue.style.alignSelf = Align.FlexStart;
            _continue.style.paddingLeft = _continue.style.paddingRight = 34;
            _continue.pickingMode = PickingMode.Position;
            _continue.RegisterCallback<ClickEvent>(_ => ContinueClicked?.Invoke());
            Text(_continue, "", ArgumentArt.BodyBold, 30, Gold, "argument-continue-text");

            _panic = Box(Root, "argument-panic", PickingMode.Ignore);
            ArgueUi.Fill(_panic);
            _panic.style.backgroundImage = new StyleBackground(StageArt.Vignette());
            _panic.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
            _panic.style.opacity = 0f;
            _flash = Box(Root, "argument-flash", PickingMode.Ignore);
            ArgueUi.Fill(_flash);
            _flash.style.opacity = 0f;
        }

        // ---------- Building ----------

        static VisualElement Box(VisualElement parent, string name, PickingMode picking)
        {
            var e = new VisualElement { name = name, pickingMode = picking };
            parent?.Add(e);
            return e;
        }

        static VisualElement Layer(VisualElement parent, string name)
        {
            var e = Box(parent, name, PickingMode.Ignore);
            ArgueUi.Fill(e);
            return e;
        }

        static VisualElement Card(VisualElement parent, string name, Color fill, Color edge)
        {
            var e = ArgueUi.CardBox(parent, name, fill, edge);
            e.pickingMode = PickingMode.Ignore;
            ArgueUi.Round(e, 6f);
            return e;
        }

        static Label Text(VisualElement parent, string text, Font font, float size, Color color, string name = null) =>
            ArgueUi.Text(parent, text, font, size, color, name);

        static StyleTextShadow Shadow(float r) => new StyleTextShadow(new TextShadow { offset = new Vector2(r * 0.5f, r * 0.5f), blurRadius = r, color = new Color(0f, 0f, 0f, 0.9f) });

        VisualElement Portrait(string name)
        {
            var p = Box(_camera, name, PickingMode.Ignore);
            ArgueUi.Place(p, 24, 140, 640, 860);
            p.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            p.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Bottom));
            p.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            return p;
        }

        Label HandSide_(VisualElement parent, string name)
        {
            var l = Text(parent, "", ArgumentArt.Body, 19, Dim, name);
            l.style.width = 110;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }

        StatementView MakeStatement(int i)
        {
            var v = new StatementView { font = 30f };
            v.row = Box(_statementsLayer, "argument-statement-" + i, PickingMode.Ignore);
            v.row.style.position = Position.Absolute;
            v.row.style.flexDirection = FlexDirection.Row;
            v.row.style.alignItems = Align.Center;
            v.row.style.paddingLeft = v.row.style.paddingRight = 8;
            v.row.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            v.row.style.display = DisplayStyle.None;
            v.tag = Text(v.row, "", ArgumentArt.BodyBold, 15, Gold);
            v.tag.style.marginRight = 8;
            v.pre = Text(v.row, "", ArgumentArt.BodyBold, 30, Paper);
            v.weak = Text(v.row, "", ArgumentArt.BodyBold, 30, Gold);
            v.post = Text(v.row, "", ArgumentArt.BodyBold, 30, Paper);
            foreach (var l in new[] { v.pre, v.weak, v.post, v.tag })
            {
                l.style.whiteSpace = WhiteSpace.NoWrap;
                l.style.textShadow = Shadow(3f);
            }
            v.weak.style.backgroundColor = new Color(1f, 0.78f, 0.3f, 0.16f);
            v.weak.style.borderBottomColor = Gold;
            v.weak.style.borderBottomWidth = 3f;
            v.weak.style.paddingLeft = v.weak.style.paddingRight = 3;
            return v;
        }

        void Pointer(Vector2 local)
        {
            _pointerOnTable = true;
            _pointerUnits = new Vec((local.x - ArenaW * 0.5f) / Px, (ArenaH * 0.5f - local.y) / Px);
        }

        // ---------- What the host and tests read ----------

        public bool IsShown => Root.panel != null;
        public string LineText => _line.text;
        public string SpeakerText => _speaker.text;
        public string Face => _frontFace;
        public bool EndShown => _end.style.display == DisplayStyle.Flex;
        public bool BecauseShown => _because.style.display == DisplayStyle.Flex;
        public bool CaseFileShown => _caseFile.style.display == DisplayStyle.Flex;
        public string EndTitle => _endTitle.text;
        public VisualElement ContinueButton => _continue;
        public VisualElement ReasonCard(int i) => i >= 0 && i < ReasonPool ? _reasonCards[i] : null;
        public string ClaimText => _claimText.text;
        public Texture SpriteShown => _portraitFront.resolvedStyle.backgroundImage.texture != null ? (Texture)_portraitFront.resolvedStyle.backgroundImage.texture : _portraitFront.resolvedStyle.backgroundImage.renderTexture;
        public string BoilLabel => _boilLabel.style.display == DisplayStyle.Flex ? _boilLabel.text : "";
        /// <summary>Statements on screen (their premise ids and plain text), for tests and the capture's notes.</summary>
        public int StatementsShown
        {
            get
            {
                int n = 0;
                foreach (var v in _statements) if (v.used) n++;
                return n;
            }
        }

        // ---------- Transitions ----------

        struct Anim
        {
            public float time, duration;
            public Action<float> apply;
            public Action done;
        }

        readonly List<Anim> _anims = new List<Anim>(8);
        public bool Busy => _anims.Count > 0;

        void Tween(float duration, Action<float> apply, Action done)
        {
            apply(0f);
            _anims.Add(new Anim { duration = Mathf.Max(0.01f, duration), apply = apply, done = done });
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>In from the world: the black rises over it, then the stage settles in (her glow slides in, the table
        /// lifts).</summary>
        public void PlayIntro(Action done)
        {
            Tween(0.45f, t => _black.style.opacity = t, () =>
                Tween(0.7f, t =>
                {
                    _camera.style.opacity = t;
                    _arena.style.translate = new Translate(0, (1f - t) * 30f);
                    _backdrop.style.translate = new Translate((1f - t) * -80f, 0);
                }, done));
        }

        /// <summary>Out to the world: the stage fades, then the black; then the view removes itself (on the panel's own
        /// clock: the host may already be gone).</summary>
        public void PlayOutro()
        {
            _anims.Clear();
            float from = _camera.resolvedStyle.opacity;
            Tween(0.35f, t =>
            {
                _camera.style.opacity = from * (1f - t);
                _end.style.opacity = 1f - t;
            }, () => Tween(0.5f, t => _black.style.opacity = 1f - t, Remove));
            float last = Time.realtimeSinceStartup;
            Root.schedule.Execute(() =>
            {
                float now = Time.realtimeSinceStartup;
                StepAnims(Mathf.Min(0.1f, now - last));
                last = now;
            }).Every(16).Until(() => Root.panel == null || !Busy);
        }

        void StepAnims(float dt)
        {
            for (int i = 0; i < _anims.Count; i++)
            {
                var a = _anims[i];
                a.time += dt;
                float t = Mathf.Clamp01(a.time / a.duration);
                a.apply(Smooth(t));
                _anims[i] = a;
                if (t < 1f) continue;
                _anims.RemoveAt(i);
                i--;
                a.done?.Invoke();
            }
        }

        public void Remove()
        {
            Root.RemoveFromHierarchy();
            _boilFront.Dispose();
            _boilBack.Dispose();
        }

        // ---------- The boil label (F10) ----------

        public void ShowBoilLabel(bool always = false)
        {
            if (always) _boilLabelAlways = true;
            _boilLabelFor = 4f;
            var s = ArgumentBoil.For(_frontFace);
            _boilLabel.text = ArgumentBoil.Reacting
                ? "Boil (" + ArgumentBoil.CycleKeyName + "): by her mood, " + _frontFace + ": " + s.Describe()
                : "Boil (" + ArgumentBoil.CycleKeyName + "): " + s.Describe() + "  ·  " + ArgumentBoil.ReactKeyName + ": back to her mood";
            _boilLabel.style.display = DisplayStyle.Flex;
        }

        // ---------- The frame ----------

        static ArgumentStrings S => ArgumentStrings.Shared;

        /// <summary>Draws the battle as it is now. <paramref name="hints"/>: the key hint line (rebuilt by the host when the
        /// bindings change). <paramref name="faceOverride"/>: a capture's held expression (null: hers).</summary>
        public void Render(BattleFlow f, float dt, string hints, string faceOverride, bool reduceEffects, float shakeScale)
        {
            _flow = f;
            _time += dt;
            StepAnims(dt);
            if (_boilLabelFor > 0f && !_boilLabelAlways)
            {
                _boilLabelFor -= dt;
                if (_boilLabelFor <= 0f) _boilLabel.style.display = DisplayStyle.None;
            }
            var def = f.Def;
            var e = f.Engine;
            Cues(f, reduceEffects, shakeScale);
            Camera(dt, f, reduceEffects, shakeScale);
            Opponent(f, dt, faceOverride);
            CaseStrip(f);
            Statements(f);
            Words(f);
            Slips(f);
            ShowdownWords(f);
            Hand(f);
            EndBeat(f);
            MapCards(f);
            Sky(f);
            Dialogue(f);
            Quote(f);
            Because(f);
            Monitor(f, dt);
            string keys = f.State == BattleState.Argue ? ArgueHints : hints;
            if (keys != _shownHints) { _shownHints = keys ?? ""; _hints.text = _shownHints; }
            _hints.style.display = f.State == BattleState.Debate || f.State == BattleState.Argue ? DisplayStyle.Flex : DisplayStyle.None;
            bool placeholder = def.placeholderLines;
            if (placeholder != _shownPlaceholder) { _shownPlaceholder = placeholder; _placeholder.text = placeholder ? S["screen.placeholder"] : ""; }

            // The caption: the step spelled out, or why something didn't work; it fades after a few seconds.
            string cap = f.CaptionAge < 4.5f ? f.Caption : "";
            if (cap != _shownCaption) { _shownCaption = cap; _caption.text = cap; }
            _caption.style.display = cap.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _caption.style.opacity = Mathf.Clamp01((4.5f - f.CaptionAge) / 0.6f);

            // The banner names a phase as it opens ("The showdown", "He called you first", "Composure").
            int bannerKey = (int)f.State * 100 + (f.Engine.Anger > 0 ? 10 : 0) + (f.Engine.LastStand ? 20 : 0) + (f.Engine.Pressure != null && f.Engine.Pressure.Spec.calls > 0 ? 40 : 0)
                + (f.Sections ? (f.Turn <= 1 ? 1 : 0) + (f.Sky && f.SkyProgress < 1f ? 2 : 0) : (f.Debate.Round <= 1 ? 1 : 0));
            if (bannerKey != _bannerKey) { _bannerKey = bannerKey; _stateBanner.text = Banner(f); }
            _stateBanner.style.opacity = f.StateTime < 2.2f ? Mathf.Clamp01((2.2f - f.StateTime) / 0.5f) : 0f;

            // The table dims while words are being said (the exchange holds the room), clears in play; her case reads over
            // a darker table.
            float tint = f.State == BattleState.Talk || f.State == BattleState.Intro ? 0.5f : f.State == BattleState.Showdown ? 0.62f : f.State == BattleState.Because ? 0.55f
                : f.State == BattleState.Argue ? (f.Sky ? 0.15f : 0.42f) : 0.22f;
            _tint = Mathf.MoveTowards(_tint, tint, dt * 2.5f);
            _arenaTint.style.opacity = _tint;
            if (_arenaBg.style.backgroundImage.value.texture == null) _arenaBg.style.backgroundImage = new StyleBackground(StageArt.Table(def.arena));

            // Composure: the room closes in as her heart climbs, and all the way when it's gone.
            float panic = f.State == BattleState.Panic ? 0.55f + 0.45f * f.PanicProgress : f.Engine.Outcome == ArgumentOutcome.ComposureLost && (f.State == BattleState.Talk || f.State == BattleState.Ending || f.State == BattleState.Over) ? 1f : Mathf.Max(0f, (e.HeartStep - 2) * 0.16f);
            float pulse = e.HeartStep >= 3 ? 0.06f * Mathf.Sin(_time * Mathf.PI * 2f * e.HeartRate / 60f) : 0f;
            _panic.style.opacity = Mathf.Clamp01(panic + pulse);
            _flashA = Mathf.MoveTowards(_flashA, 0f, dt * 2.2f);
            _flash.style.opacity = _flashA;
            bool red = _flashRed > 0.5f;
            if (red != _flashWasRed) { _flashWasRed = red; _flash.style.backgroundColor = red ? new Color(0.75f, 0.08f, 0.12f) : Paper; }

            // Repaints (docs/redesign/perf.md, Battle 2): her glow only when her temper changes; the monitor at 30 Hz; the
            // table every frame (its hazards move).
            int glow = f.Engine.Heat * 10 + f.Engine.Anger;
            if (glow != _shownGlow) { _shownGlow = glow; _backdrop.MarkDirtyRepaint(); }
            _arenaDraw.MarkDirtyRepaint();
            _monitorTime += dt;
            if (_monitorTime >= 1f / 30f) { _monitorTime = 0f; _monitor.MarkDirtyRepaint(); }
        }

        int _bannerKey = -1, _shownGlow = -1, _shownTemper = -1, _shownBpm = -1, _shownShowdownLeft = -1;
        float _tint = 0.25f, _monitorTime;
        bool _shownPlaceholder, _flashWasRed, _shownBecauseClock;
        string _shownWordsFor;
        readonly List<string> _upperWords = new List<string>(8);

        static string Banner(BattleFlow f)
        {
            string who = f.Def.opponent;
            switch (f.State)
            {
                case BattleState.Showdown: return S["screen.banner.showdown"];
                case BattleState.Because: return "";
                case BattleState.Pressure:
                    var p = f.Engine.Pressure;
                    if (p != null && p.Spec.calls > 0) return S["screen.banner.rings"];
                    return f.Engine.Anger > 0 ? S.Format("screen.banner.angry", "opponent", who) : S["screen.banner.pressure"];
                case BattleState.Panic: return S["screen.banner.panic"];
                case BattleState.Debate: return f.Engine.LastStand ? S.Format("screen.banner.stand", "opponent", who) : f.Debate.Round <= 1 ? S.Format("screen.banner.debate", "opponent", who) : "";
                case BattleState.Argue: return f.Engine.LastStand ? S.Format("screen.banner.stand", "opponent", who) : f.Turn <= 1 ? S.Format("screen.banner.argue", "opponent", who) : "";
                case BattleState.Talk: return f.Sky && f.SkyProgress < 1f ? S["screen.banner.sky"] : "";
                default: return "";
            }
        }

        // ---------- Cues: the camera and the flash ----------

        void Cues(BattleFlow f, bool reduceEffects, float shakeScale)
        {
            foreach (var c in f.Cues)
            {
                switch (c.kind)
                {
                    case CueKind.HeartUp: _shake = Mathf.Max(_shake, 1f); _flashA = Mathf.Max(_flashA, reduceEffects ? 0.08f : 0.22f); _flashRed = 1f; break;
                    case CueKind.Break: _punch = 1f; _flashA = Mathf.Max(_flashA, reduceEffects ? 0.1f : 0.3f); _flashRed = 0f; break;
                    case CueKind.WrongCard: _shake = Mathf.Max(_shake, 0.7f); break;
                    case CueKind.Reveal:
                    case CueKind.SlipCaught: _punch = 0.6f; break;
                    case CueKind.ShowdownStart: _punch = 0.8f; break;
                    case CueKind.Cut: _shake = Mathf.Max(_shake, 0.35f); break;
                    case CueKind.Panic: _shake = 1.4f; break;
                    case CueKind.Ring: _shake = Mathf.Max(_shake, 0.4f); break;
                    case CueKind.PressureIn: _punch = Mathf.Max(_punch, 0.4f); break;
                }
            }
        }

        void Camera(float dt, BattleFlow f, bool reduceEffects, float shakeScale)
        {
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 2.6f);
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 1.4f);
            float amp = reduceEffects ? 0f : 14f * _shake * shakeScale;
            float sx = amp * (Mathf.PerlinNoise(_time * 23f, 0.3f) - 0.5f) * 2f;
            float sy = amp * (Mathf.PerlinNoise(0.7f, _time * 23f) - 0.5f) * 2f;
            // A slow drift, like a held camera; a lean in on the moments that matter.
            float drift = reduceEffects ? 0f : 1f;
            sx += drift * Mathf.Sin(_time * 0.21f) * 4f;
            sy += drift * Mathf.Sin(_time * 0.17f + 1.3f) * 3f;
            float lean = f.State == BattleState.Showdown || f.State == BattleState.Panic ? 0.035f : f.State == BattleState.Talk ? 0.012f : 0f;
            float scale = 1f + (reduceEffects ? 0f : lean + 0.03f * Smooth(_punch));
            _camera.style.translate = new Translate(sx, sy);
            _camera.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        // ---------- The opponent ----------

        void Opponent(BattleFlow f, float dt, string faceOverride)
        {
            var def = f.Def;
            var portrait = f.Portrait;
            if (def.opponent != _name.text) _name.text = def.opponent;
            // Rebuilt only when her temper changes (docs/redesign/perf.md, Battle 1).
            int temperKey = f.Engine.Heat * 10 + f.Engine.Anger;
            if (temperKey != _shownTemper)
            {
                _shownTemper = temperKey;
                _temper.text = S["screen.temper"] + " " + Pips(f.Engine.Heat, 3) + (f.Engine.Anger > 0 ? "     " + S["screen.anger"] + " " + Pips(f.Engine.Anger, 3) : "");
                _temper.style.color = f.Engine.Anger > 0 ? WineBright : Dim;
            }

            string face = faceOverride ?? portrait.Shown;
            if (face != _frontFace || def.opponent != _who)
            {
                // A change of face: the new one fades in over the old (no pop), with a small lift as they speak.
                bool first = _frontFace.Length == 0;
                _who = def.opponent;
                _backFace = _frontFace;
                _backTex = _frontTex;
                _frontFace = face;
                _frontTex = ArgumentArt.Sprite(def.opponent, face, out _);
                _fade = first ? 1f : 0f;
                _lift = first ? 0f : 1f;
            }
            _fade = Mathf.MoveTowards(_fade, 1f, dt / 0.22f);
            _lift = Mathf.MoveTowards(_lift, 0f, dt / 0.3f);
            // While their line is being typed, a little breathing bob.
            bool talking = f.Delivery.HasLine && !f.Delivery.LineDone && f.Delivery.Current.speaker == Speaker.Opponent;
            _talkBob = Mathf.MoveTowards(_talkBob, talking ? 1f : 0f, dt * 4f);
            float bob = _talkBob * Mathf.Sin(_time * 9f) * 3f;
            float lift = Smooth(_lift);
            _portraitFront.style.opacity = Smooth(_fade);
            _portraitFront.style.scale = new Scale(new Vector3(1f + 0.02f * lift, 1f + 0.02f * lift, 1f));
            _portraitFront.style.translate = new Translate(0, -6f * lift + bob);
            _portraitBack.style.opacity = 1f - Smooth(_fade);
            if (_frontTex != null) Show(_portraitFront, _boilFront.Tick(dt, _frontTex, Shown(_portraitFront, _frontTex), ArgumentBoil.For(_frontFace)));
            if (_backTex != null && _fade < 1f) Show(_portraitBack, _boilBack.Tick(dt, _backTex, Shown(_portraitBack, _backTex), ArgumentBoil.For(_backFace)));
            _portraitBack.style.display = _fade < 1f && _backTex != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static Vector2 Shown(VisualElement slot, Texture2D tex)
        {
            var r = slot.contentRect;
            var size = float.IsNaN(r.width) || r.width <= 0f ? new Vector2(640f, 860f) : new Vector2(r.width, r.height);
            return ArgumentBoil.Shown(new Vector2(tex.width, tex.height), size);
        }

        static void Show(VisualElement slot, Texture t)
        {
            if (t == null) return;
            var current = slot.style.backgroundImage.value;
            if (t is RenderTexture rt)
            {
                if (current.renderTexture != rt) slot.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                else slot.MarkDirtyRepaint();
            }
            else if (current.texture != t) slot.style.backgroundImage = new StyleBackground(t as Texture2D);
        }

        void DrawBackdrop(MeshGenerationContext mgc)
        {
            var f = _flow;
            if (f == null) return;
            var p = mgc.painter2D;
            // Her glow: wine, warmer with her temper and anger; Nadia's gold under the table.
            float heat = Mathf.Clamp01(0.25f + 0.2f * f.Engine.Heat + 0.15f * f.Engine.Anger);
            var c = new Vector2(330f, 560f);
            for (int i = 8; i >= 1; i--)
            {
                p.fillColor = new Color(Wine.r, Wine.g, Wine.b, 0.035f * heat * (9 - i) / 8f);
                p.BeginPath();
                p.Arc(c, 80f * i, 0f, 360f);
                p.Fill();
            }
            p.fillColor = new Color(Gold.r, Gold.g, Gold.b, 0.05f);
            p.BeginPath();
            p.Arc(new Vector2(ArenaX + ArenaW * 0.5f, ArenaY + ArenaH + 40f), 260f, 0f, 360f);
            p.Fill();
        }

        // ---------- The case strip ----------

        void CaseStrip(BattleFlow f)
        {
            // The sections show her case on the table itself (and the statement picked above it): no strip.
            bool strip = !f.Sections;
            if (strip != _stripShown)
            {
                _stripShown = strip;
                _claimCard.style.display = strip ? DisplayStyle.Flex : DisplayStyle.None;
                _chips.style.display = strip ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!strip) return;
            var def = f.Def;
            var e = f.Engine;
            string claimLabel = S["screen.claim"];
            if (_claimLabel.text != claimLabel) _claimLabel.text = claimLabel;
            if (_shownClaim != def.claim)
            {
                _shownClaim = def.claim;
                _claimText.text = def.claim;
                int len = Typewriter.StripTags(def.claim).Length;
                _claimText.style.fontSize = len > 70 ? 22 : len > 50 ? 25 : 30;
            }
            bool stand = e.LastStand;
            if (stand != _shownStand)
            {
                _shownStand = stand;
                _claimCard.style.borderTopColor = _claimCard.style.borderBottomColor = _claimCard.style.borderLeftColor = _claimCard.style.borderRightColor = stand ? WineBright : Ink;
            }
            int chip = 0;
            foreach (var p in def.premises)
            {
                if (p == null || chip >= ChipPool) continue;
                var st = e.Status(p.id);
                var box = _chipBoxes[chip];
                var t = _chipTexts[chip];
                // Rebuilt only when its state changes (docs/redesign/perf.md, Battle 1).
                if (_chipState[chip] == (int)st) { chip++; continue; }
                _chipState[chip] = (int)st;
                chip++;
                box.style.display = DisplayStyle.Flex;
                string label;
                Color fill, ink = Paper;
                switch (st)
                {
                    case PremiseStatus.Hidden: label = "?"; fill = new Color(0.1f, 0.09f, 0.12f, 0.9f); ink = Dim; break;
                    case PremiseStatus.Broken: label = "<s>" + p.label + "</s>"; fill = new Color(0.08f, 0.07f, 0.09f, 0.9f); ink = Dim; break;
                    case PremiseStatus.Conceded: label = p.label + "  " + S["screen.chip.true"]; fill = new Color(0.16f, 0.3f, 0.2f, 0.95f); break;
                    case PremiseStatus.ConcededFalse: label = p.label + "  " + S["screen.chip.agreed"]; fill = new Color(0.25f, 0.24f, 0.2f, 0.95f); ink = Dim; break;
                    case PremiseStatus.Forced: label = p.label + "  " + S["screen.chip.forced"]; fill = new Color(0.45f, 0.08f, 0.12f, 0.95f); break;
                    default:
                        label = p.label + (p.key ? "  " + S["screen.key"] : "") + (p.unbreakable ? "  " + S["screen.chip.holds"] : "");
                        fill = p.key ? new Color(0.36f, 0.24f, 0.08f, 0.95f) : new Color(0.16f, 0.14f, 0.2f, 0.95f);
                        break;
                }
                if (t.text != label) t.text = label;
                t.style.color = ink;
                box.style.backgroundColor = fill;
            }
            for (; chip < ChipPool; chip++)
                if (_chipState[chip] != -2) { _chipState[chip] = -2; _chipBoxes[chip].style.display = DisplayStyle.None; }
        }

        readonly int[] _chipState = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
        bool _stripShown = true;
        bool _shownStand;

        // ---------- Statements in flight ----------

        Vector2 ToPx(Vec u) => new Vector2(ArenaW * 0.5f + u.X * Px, ArenaH * 0.5f - u.Y * Px);

        void Statements(BattleFlow f)
        {
            var d = f.Debate;
            bool show = f.State == BattleState.Debate || f.State == BattleState.Talk || f.State == BattleState.Because || f.State == BattleState.Intro;
            foreach (var v in _statements) v.used = false;
            if (show)
            {
                for (int i = 0; i < d.Statements.Count; i++)
                {
                    var s = d[i];
                    if (!s.active) continue;
                    var v = Bind(s);
                    if (v == null) continue;
                    v.used = true;
                    Place(f, v, s);
                }
            }
            foreach (var v in _statements)
            {
                if (v.used) continue;
                if (v.serial != 0) { v.serial = 0; v.row.style.display = DisplayStyle.None; }
            }
        }

        StatementView Bind(in Debate.Statement s)
        {
            foreach (var v in _statements) if (v.serial == s.serial) return v;
            foreach (var v in _statements)
            {
                if (v.serial != 0) continue;
                v.serial = s.serial;
                v.pre.text = s.pre;
                v.weak.text = s.weak;
                v.post.text = s.post;
                v.tag.text = s.claim ? S["screen.claim"] : s.question ? S["screen.statement.question"] : "";
                v.tag.style.display = v.tag.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                // A statement fits the table: long ones in smaller type.
                int len = s.Plain.Length;
                v.font = len > 80 ? 24f : len > 62 ? 27f : 30f;
                var weakInk = s.question ? WineBright : Gold;
                foreach (var l in new[] { v.pre, v.weak, v.post }) l.style.fontSize = v.font;
                v.weak.style.color = s.claim ? Paper : weakInk;
                v.weak.style.backgroundColor = s.claim ? new Color(0f, 0f, 0f, 0f) : new Color(weakInk.r, weakInk.g, weakInk.b, 0.16f);
                v.weak.style.borderBottomWidth = s.claim ? 0f : 3f;
                v.weak.style.borderBottomColor = weakInk;
                ArgumentArt.Use(v.weak, s.claim ? ArgumentArt.Display : s.question ? ArgumentArt.BodyItalic : ArgumentArt.BodyBold);
                v.row.style.backgroundColor = s.claim ? new Color(Wine.r * 0.6f, Wine.g * 0.6f, Wine.b * 0.6f, 0.85f) : new Color(0f, 0f, 0f, 0f);
                ArgueUi.Round(v.row, 6f);
                v.row.style.paddingTop = v.row.style.paddingBottom = s.claim ? 6 : 0;
                v.row.style.display = DisplayStyle.Flex;
                return v;
            }
            return null;
        }

        void Place(BattleFlow f, StatementView v, in Debate.Statement s)
        {
            var r = v.row.layout;
            float w = float.IsNaN(r.width) || r.width <= 1f ? s.width * Px : r.width;
            float h = float.IsNaN(r.height) || r.height <= 1f ? s.height * Px : r.height;
            // Too wide for the table (a long line in a large face): a step smaller next frame.
            if (w > ArenaW - 30f && v.font > 20f)
            {
                v.font -= 2f;
                foreach (var l in new[] { v.pre, v.weak, v.post }) l.style.fontSize = v.font;
            }
            var c = ToPx(s.position);
            v.row.style.left = c.x - w * 0.5f;
            v.row.style.top = c.y - h * 0.5f;
            float alpha = s.Alpha * (f.State == BattleState.Debate ? 1f : 0.6f);
            v.row.style.opacity = alpha;
            float tilt = s.tilt;
            float scale = 1f;
            Color ink = Paper;
            if (s.fate == Debate.Fate.Shattered) { scale = 1f + s.fateAge * 0.6f; ink = WineBright; tilt += s.fateAge * 20f; }
            else if (s.fate == Debate.Fate.Settled) { ink = Dim; }
            v.row.style.rotate = new Rotate(new Angle(tilt, AngleUnit.Degree));
            v.row.style.scale = new Scale(new Vector3(scale, scale, 1f));
            v.pre.style.color = ink;
            v.post.style.color = ink;
            // The view's measure goes back to the debate, so what the light touches is what's drawn.
            if (!s.measured && !float.IsNaN(r.width) && r.width > 1f)
            {
                var wr = v.weak.layout;
                float w0 = float.IsNaN(wr.x) ? 0f : wr.x, w1 = float.IsNaN(wr.width) ? r.width : wr.x + wr.width;
                f.Debate.Measure(s.serial, r.width / Px, r.height / Px, w0 / Px, w1 / Px);
            }
        }

        // ---------- Pressure words on her walls ----------

        void Words(BattleFlow f)
        {
            var phase = Phase(f);
            var words = f.Def.pressureWords;
            // Her words in capitals, made once per battle (not per wall per frame: docs/redesign/perf.md, Battle 1).
            if (_shownWordsFor != f.Def.id)
            {
                _shownWordsFor = f.Def.id;
                _upperWords.Clear();
                foreach (var w in words) _upperWords.Add((w ?? "").ToUpperInvariant());
            }
            int used = 0;
            if (phase != null && words.Count > 0 && f.State != BattleState.Showdown)
            {
                var walls = phase.Walls;
                for (int i = 0; i < walls.Count && used < WordPool; i++)
                {
                    var w = walls[i];
                    if (!w.active) continue;
                    var l = _words[used++];
                    string text = _upperWords[w.serial % _upperWords.Count];
                    if (l.text != text) l.text = text;
                    float x = w.warn > 0f ? Mathf.Sign(w.x) * (PressurePhase.HalfWidth - 0.35f) : w.x;
                    var px = ToPx(new Vec(x, 0f));
                    l.style.display = DisplayStyle.Flex;
                    var lr = l.layout;
                    float lw = float.IsNaN(lr.width) ? 200f : lr.width, lh = float.IsNaN(lr.height) ? 40f : lr.height;
                    l.style.left = px.x - lw * 0.5f;
                    l.style.top = px.y - lh * 0.5f;
                    l.style.opacity = w.warn > 0f ? 0.18f : 0.42f;
                }
            }
            for (int i = used; i < WordPool; i++) if (_words[i].style.display != DisplayStyle.None) _words[i].style.display = DisplayStyle.None;
        }

        static PressurePhase Phase(BattleFlow f) =>
            f.State == BattleState.Pressure ? f.Engine.Pressure : !f.Sections && (f.State == BattleState.Debate || f.State == BattleState.Talk || f.State == BattleState.Because) ? f.Debate.Hazards : null;

        void Slips(BattleFlow f)
        {
            int used = 0;
            var phase = f.State == BattleState.Pressure ? f.Engine.Pressure : null;
            if (f.State == BattleState.Debate)
            {
                var slips = f.Debate.Slips;
                for (int i = 0; i < slips.Count && used < SlipPool; i++)
                    if (slips[i].active) SlipName(used++, slips[i].name, slips[i].position);
            }
            else if (phase != null)
            {
                var slips = phase.Slips;
                for (int i = 0; i < slips.Count && used < SlipPool; i++)
                    if (slips[i].active) SlipName(used++, slips[i].name, slips[i].position);
            }
            for (int i = used; i < SlipPool; i++) _slipNames[i].style.display = DisplayStyle.None;
        }

        void SlipName(int i, string name, Vec at)
        {
            var l = _slipNames[i];
            if (l.text != name) l.text = name ?? "";
            var p = ToPx(at);
            l.style.display = DisplayStyle.Flex;
            l.style.left = p.x + 24f;
            l.style.top = p.y - 14f;
        }

        // ---------- The showdown ----------

        void ShowdownWords(BattleFlow f)
        {
            var sd = f.State == BattleState.Showdown ? f.Showdown : null;
            _showdownPrompt.style.display = sd != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (sd != null)
            {
                int left = sd.Total - sd.Cuts - sd.Landed;
                if (left != _shownShowdownLeft)
                {
                    _shownShowdownLeft = left;
                    _showdownPrompt.text = S.Format("screen.showdown.prompt", "n", left.ToString(CultureInfo.InvariantCulture));
                }
            }
            int used = 0;
            if (sd != null)
            {
                var words = sd.Words;
                for (int i = 0; i < words.Count && used < ShowdownPool; i++)
                {
                    var w = words[i];
                    if (!w.active) continue;
                    var l = _showWords[used++];
                    // Stripped once per word (the label keeps it), not every frame.
                    if (!ReferenceEquals(l.userData, w.text)) { l.userData = w.text; l.text = Typewriter.StripTags(w.text); }
                    l.style.display = DisplayStyle.Flex;
                    var lr = l.layout;
                    float lw = float.IsNaN(lr.width) ? w.width * Px : lr.width, lh = float.IsNaN(lr.height) ? 56f : lr.height;
                    var p = ToPx(new Vec(w.x, 0.15f));
                    float off = w.cut ? -w.doneAge * 160f : w.landed ? w.doneAge * 60f : 0f;
                    l.style.left = p.x - lw * 0.5f;
                    l.style.top = p.y - lh * 0.5f + off;
                    l.style.opacity = w.cut || w.landed ? Mathf.Clamp01(1f - w.doneAge / 0.6f) : 1f;
                    l.style.color = w.cut ? Gold : w.landed ? WineBright : Paper;
                    l.style.rotate = new Rotate(new Angle(w.cut ? -8f * w.doneAge * 4f : 0f, AngleUnit.Degree));
                }
            }
            for (int i = used; i < ShowdownPool; i++) if (_showWords[i].style.display != DisplayStyle.None) _showWords[i].style.display = DisplayStyle.None;
        }

        // ---------- The end beat ----------

        /// <summary>Convinced: she strikes her own claim out, line by line (SELECTION.md 8: she erases it herself). Heard
        /// out: it warms to gold and stays, hers. Pressed: it cracks. Yielded: it stands, bright. Composure lost: the room
        /// has already gone dark.</summary>
        void EndBeat(BattleFlow f)
        {
            bool on = f.State == BattleState.Ending || f.State == BattleState.Over;
            var outcome = f.Engine.Outcome;
            if (!on || outcome == ArgumentOutcome.ComposureLost) { _endClaim.style.display = DisplayStyle.None; return; }
            if (_endClaim.text != f.Def.claim) _endClaim.text = f.Def.claim;
            _endClaim.style.display = DisplayStyle.Flex;
            float t = f.State == BattleState.Over ? 1f : f.EndingProgress;
            _endClaim.style.opacity = Mathf.Clamp01(t * 3f) * (outcome == ArgumentOutcome.Convinced ? Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((t - 0.6f) / 0.4f)) : 1f);
            _endClaim.style.color = outcome == ArgumentOutcome.HeardOut ? Color.Lerp(Paper, Gold, t) : outcome == ArgumentOutcome.Pressed ? Color.Lerp(Paper, WineBright, t) : Paper;
        }

        void DrawEndBeat(Painter2D p, BattleFlow f)
        {
            var outcome = f.Engine.Outcome;
            float t = f.State == BattleState.Over ? 1f : f.EndingProgress;
            var r = _endClaim.layout;
            if (float.IsNaN(r.width) || r.width <= 0f) return;
            float y = r.y + r.height * 0.5f;
            if (outcome == ArgumentOutcome.Convinced)
            {
                // Her pencil, struck through her own words.
                float k = Mathf.Clamp01((t - 0.15f) / 0.5f);
                p.strokeColor = WineBright;
                p.lineWidth = 5f;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(r.x + 20f, y + 4f));
                p.LineTo(new Vector2(r.x + 20f + (r.width - 40f) * k, y - 2f));
                p.Stroke();
            }
            else if (outcome == ArgumentOutcome.Pressed)
            {
                float k = Mathf.Clamp01(t * 1.6f);
                p.strokeColor = new Color(WineBright.r, WineBright.g, WineBright.b, k);
                p.lineWidth = 3f;
                float cx = r.x + r.width * 0.5f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 1.05f + 0.4f;
                    p.BeginPath();
                    p.MoveTo(new Vector2(cx, y));
                    p.LineTo(new Vector2(cx + Mathf.Cos(a) * 60f * k, y + Mathf.Sin(a) * 30f * k));
                    p.LineTo(new Vector2(cx + Mathf.Cos(a + 0.3f) * 130f * k, y + Mathf.Sin(a + 0.2f) * 70f * k));
                    p.Stroke();
                }
            }
        }

        // ---------- The hand ----------

        void Hand(BattleFlow f)
        {
            var cards = f.Cards;
            bool show = f.State == BattleState.Debate || f.State == BattleState.Talk || f.State == BattleState.Argue;
            if (show != _handShown) { _handShown = show; _hand.style.display = show ? DisplayStyle.Flex : DisplayStyle.None; }
            if (!show) return;
            int version = cards.Count * 1000 + f.Round;
            if (f.CardIndex == _shownCard && version == _shownCardsVersion && !f.Cues.Contains(CueKind.Burned)) return;
            _shownCard = f.CardIndex;
            _shownCardsVersion = version;
            string c = f.CardInHand;
            if (c.Length == 0)
            {
                _handTag.text = S["screen.hand.empty"];
                _handName.text = "";
                _handNote.text = "";
            }
            else
            {
                Name(f, c, out string name, out string note);
                int burned = f.BurnedFor(c);
                _handTag.text = burned > 0 ? S.Format("screen.hand.burned", "n", burned.ToString(CultureInfo.InvariantCulture)) : S.Format("screen.hand.tag", "n", (f.CardIndex + 1).ToString(CultureInfo.InvariantCulture), "keys", cards.Count.ToString(CultureInfo.InvariantCulture));
                _handName.text = burned > 0 ? "<s>" + name + "</s>" : name;
                _handNote.text = note;
                var ev = f.Def.Evidence(c);
                bool quote = ev != null && ev.IsQuote;
                if (quote && burned == 0) _handTag.text = S["screen.quote.tag"] + "   " + _handTag.text;
                _handCard.style.backgroundColor = c == BattleFlow.RetortCard ? new Color(0.95f, 0.82f, 0.82f) : c == BattleFlow.RestCard ? new Color(1f, 0.92f, 0.74f) : burned > 0 ? new Color(0.62f, 0.6f, 0.58f)
                    : quote ? new Color(0.99f, 0.95f, 0.85f) : Paper;
            }
            for (int i = 0; i < HandSide; i++)
            {
                _handPrev[i].text = Neighbour(f, -(i + 1));
                _handNext[i].text = Neighbour(f, i + 1);
            }
        }

        string Neighbour(BattleFlow f, int offset)
        {
            var cards = f.Cards;
            int n = cards.Count;
            if (n <= 1 || Math.Abs(offset) >= n || f.CardIndex < 0) return "";
            int i = ((f.CardIndex + offset) % n + n) % n;
            Name(f, cards[i], out string name, out _);
            return f.BurnedFor(cards[i]) > 0 ? "<s>" + name + "</s>" : name;
        }

        static void Name(BattleFlow f, string card, out string name, out string note)
        {
            if (card == BattleFlow.RetortCard) { name = S["screen.card.retort"]; note = S["screen.card.retortNote"]; return; }
            if (card == BattleFlow.RestCard) { name = S["screen.card.rest"]; note = S["screen.card.restNote"]; return; }
            var ev = f.Def.Evidence(card);
            name = ev != null ? ev.name : card;
            note = ev != null ? ev.note : "";
        }

        bool _handShown = true;

        // ---------- The dialogue box (and her speech bubble) ----------

        void Dialogue(BattleFlow f)
        {
            var d = f.Delivery;
            bool has = d.HasLine;
            var line = has ? d.Current : d.Last;
            // While Nadia dodges or argues, her lines are said in a bubble over her, not down at the bottom.
            bool bubble = f.Sections && has && line.speaker == Speaker.Opponent && (f.State == BattleState.Pressure || f.State == BattleState.Argue);
            Bubble(f, bubble);
            if (!d.HadAny || bubble) { _dialogue.style.display = DisplayStyle.None; return; }
            _dialogue.style.display = DisplayStyle.Flex;
            _dialogue.style.opacity = has ? 1f : 0.55f;
            string who = line.speaker == Speaker.Supporter ? line.who + "  " + S["screen.earpiece"] : line.who;
            if (who != _shownSpeaker)
            {
                _shownSpeaker = who;
                _speaker.text = who;
                Color tag = line.speaker == Speaker.Opponent ? Wine : line.speaker == Speaker.Nadia ? new Color(0.55f, 0.4f, 0.12f) : new Color(0.2f, 0.32f, 0.5f);
                _speakerTag.style.backgroundColor = tag;
            }
            string text = has ? (f.QuoteId.Length > 0 ? QuoteLine(f) : d.Typewriter.Visible) : Typewriter.Spelled(line.text);
            if (text != _shownLine)
            {
                _shownLine = text;
                _line.text = text;
                // Long lines take a smaller face so the box stays a few lines high; the box grows to fit whatever's left.
                int len = has ? d.Typewriter.Length : Typewriter.StripTags(line.text).Length;
                float font = len > 300 ? 24f : len > 210 ? 26f : len > 140 ? 28f : 30f;
                if (Math.Abs(font - _lineFont) > 0.1f) { _lineFont = font; _line.style.fontSize = font; }
                ArgumentArt.Use(_line, line.speaker == Speaker.Nadia || line.statement != 0 && line.speaker != Speaker.Opponent ? ArgumentArt.BodyItalic : ArgumentArt.Body);
            }
            string more = has && d.LineDone && (f.State == BattleState.Talk || f.State == BattleState.Intro) ? "▼" : "";
            if (_more.text != more) _more.text = more;
        }

        string _bubbleShown = "";
        bool _bubbleOn;

        void Bubble(BattleFlow f, bool on)
        {
            if (on != _bubbleOn)
            {
                _bubbleOn = on;
                _bubble.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                // Over her, where her name is (the bubble says who's speaking).
                _name.style.opacity = on ? 0f : 1f;
                _temper.style.opacity = on ? 0f : 1f;
            }
            if (!on) return;
            string text = f.Delivery.Typewriter.Visible;
            if (!ReferenceEquals(text, _bubbleShown)) { _bubbleShown = text; _bubbleText.text = text; }
            // It sits over her, nudged up while she talks.
            _bubble.style.translate = new Translate(0, -4f * _talkBob);
        }

        /// <summary>The line being said, with her quotable words underlined in her wine as they're typed (the rest of the line
        /// laid out but unseen, so nothing jumps). Built only when more has been typed.</summary>
        string QuoteLine(BattleFlow f)
        {
            var tw = f.Delivery.Typewriter;
            int shown = tw.Shown;
            int key = shown * 4 + (f.QuoteOpen ? 1 : 0);
            if (key == _quoteLineKey && ReferenceEquals(_quoteLinePlain, tw.Plain)) return _quoteLine;
            _quoteLineKey = key;
            _quoteLinePlain = tw.Plain;
            string plain = tw.Plain;
            int a = Mathf.Clamp(f.QuoteStart, 0, plain.Length), b = Mathf.Clamp(f.QuoteEnd, a, plain.Length);
            _sb.Clear();
            for (int i = 0; i < plain.Length; i++)
            {
                if (i == shown) _sb.Append(Typewriter.HiddenOpen);
                if (i == a && i < shown) _sb.Append("<u><color=#B3263A>");
                char c = plain[i];
                if (c == '<') _sb.Append("<noparse><</noparse>");
                else _sb.Append(c);
                if (i == b - 1 && a < shown && i < shown) _sb.Append("</color></u>");
                else if (i == shown - 1 && i >= a && i < b - 1) _sb.Append("</color></u>");
            }
            if (shown < plain.Length) _sb.Append(Typewriter.HiddenClose);
            _quoteLine = _sb.ToString();
            return _quoteLine;
        }

        readonly StringBuilder _sb = new StringBuilder(512);
        string _quoteLine = "", _quoteLinePlain;
        int _quoteLineKey = -1;

        /// <summary>The prompt while her words are worth catching, and the page that flies to the hand when they're caught.</summary>
        void Quote(BattleFlow f)
        {
            bool open = f.QuoteOpen;
            if (open != _quotePromptOn)
            {
                _quotePromptOn = open;
                _quotePrompt.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                if (open) _quotePrompt.text = QuotePrompt.Length > 0 ? QuotePrompt : S.Format("screen.quote.prompt", "show", "A");
            }
            if (open) _quotePrompt.style.opacity = 0.65f + 0.35f * Mathf.Sin(_time * 9f);
            string caught = f.QuoteCaught;
            bool fly = caught.Length > 0;
            if (fly != _quotePageOn) { _quotePageOn = fly; _quotePage.style.display = fly ? DisplayStyle.Flex : DisplayStyle.None; }
            if (!fly) return;
            if (_quotePageFor != caught)
            {
                _quotePageFor = caught;
                var ev = f.Def.Evidence(caught);
                _quotePageText.text = "“" + (ev != null ? ev.words : caught) + "”";
            }
            // From the dialogue box up to the hand, tilting like a torn page.
            float t = Smooth(Mathf.Clamp01(f.QuoteCaughtAge / 1.1f));
            var from = new Vector2(220f, 1080f - 170f);
            var to = new Vector2(ArenaX + 140f, ArenaY + ArenaH + 20f);
            var at = Vector2.Lerp(from, to, t) + new Vector2(0f, -120f * Mathf.Sin(t * Mathf.PI));
            _quotePage.style.left = at.x;
            _quotePage.style.top = at.y;
            _quotePage.style.rotate = new Rotate(new Angle(-6f + 14f * t, AngleUnit.Degree));
            _quotePage.style.opacity = f.QuoteCaughtAge < 1.1f ? 1f : Mathf.Clamp01(1f - (f.QuoteCaughtAge - 1.1f) / 0.3f);
        }

        bool _quotePromptOn, _quotePageOn;
        string _quotePageFor = "";

        // ---------- Her case on the table (the sections presenter) ----------

        /// <summary>Each statement she's made, written where it sits on her case (a note on the page; under her sky, a star's
        /// name), the one picked raised and gold-edged and spelled out in full along the top of the table.</summary>
        void MapCards(BattleFlow f)
        {
            bool on = f.Sections && (f.State == BattleState.Argue || f.State == BattleState.Talk || f.State == BattleState.Because || f.State == BattleState.Intro);
            var map = f.Map;
            if (on) map.Refresh(f.Engine);
            float alpha = f.State == BattleState.Argue ? 1f : 0.45f;
            for (int i = 0; i < MapPool; i++)
            {
                bool used = on && i < map.Count && map[i].visible;
                var card = _mapCards[i];
                if (!used)
                {
                    if (_mapKey[i] != -1) { _mapKey[i] = -1; card.style.display = DisplayStyle.None; }
                    continue;
                }
                var e = map[i];
                bool picked = f.State == BattleState.Argue && f.Selected == i;
                int key = (int)e.status * 8 + (picked ? 1 : 0) + (f.Sky ? 2 : 0) + 4;
                if (key != _mapKey[i])
                {
                    _mapKey[i] = key;
                    card.style.display = DisplayStyle.Flex;
                    _mapTexts[i].text = MapLabel(f, e);
                    StyleMapCard(f, card, _mapTexts[i], e, picked, i);
                }
                // Placed by its measured size, centred on its place (under its star in the sky).
                var r = card.layout;
                float w = float.IsNaN(r.width) || r.width < 1f ? 180f : r.width, h = float.IsNaN(r.height) || r.height < 1f ? 34f : r.height;
                var c = ToPx(e.position) + (f.Sky ? new Vector2(0f, 26f + h * 0.5f) : Vector2.zero);
                var at = new Vector2(c.x - w * 0.5f, c.y - h * 0.5f);
                if ((at - _mapAt[i]).sqrMagnitude > 0.25f)
                {
                    _mapAt[i] = at;
                    card.style.left = at.x;
                    card.style.top = at.y;
                }
                card.style.opacity = alpha;
            }
            Focus(f);
        }

        string MapLabel(BattleFlow f, in CaseMap.Entry e)
        {
            if (e.claim) return Typewriter.StripTags(f.Def.claim);
            var p = f.Def.Premise(e.id);
            string words = p == null ? e.id : Typewriter.StripTags(string.IsNullOrEmpty(p.weak) ? p.text : p.weak);
            switch (e.status)
            {
                case PremiseStatus.Broken: return "<s>" + words + "</s>";
                case PremiseStatus.Conceded: return words + "  <size=70%>(" + S["screen.chip.true"] + ")</size>";
                case PremiseStatus.ConcededFalse: return words + "  <size=70%>(" + S["screen.chip.agreed"] + ")</size>";
                case PremiseStatus.Forced: return words + "  <size=70%>(" + S["screen.chip.forced"] + ")</size>";
                default: return p != null && p.key ? words + "  <size=70%>" + S["screen.key"] + "</size>" : words;
            }
        }

        void StyleMapCard(BattleFlow f, VisualElement card, Label text, in CaseMap.Entry e, bool picked, int i)
        {
            bool sky = f.Sky;
            Color fill, edge, ink;
            if (sky)
            {
                // A star's name: no paper, just the words under the star.
                fill = new Color(0.02f, 0.025f, 0.06f, picked ? 0.75f : 0.35f);
                edge = picked ? Gold : new Color(0f, 0f, 0f, 0f);
                ink = e.status == PremiseStatus.Standing || e.claim ? Paper : Dim;
            }
            else
            {
                fill = e.claim ? new Color(Wine.r * 0.7f, Wine.g * 0.7f, Wine.b * 0.7f, 0.95f)
                    : e.status == PremiseStatus.Conceded ? new Color(0.86f, 0.93f, 0.84f, 0.95f)
                    : e.status == PremiseStatus.Forced ? new Color(0.55f, 0.12f, 0.16f, 0.95f)
                    : e.status == PremiseStatus.Standing ? new Color(Paper.r, Paper.g, Paper.b, 0.95f) : new Color(0.7f, 0.68f, 0.64f, 0.85f);
                edge = picked ? Gold : e.claim ? Ink : Graphite;
                ink = e.claim || e.status == PremiseStatus.Forced ? Paper : e.status == PremiseStatus.Standing || e.status == PremiseStatus.Conceded ? PaperInk : new Color(0.35f, 0.33f, 0.36f);
            }
            card.style.backgroundColor = fill;
            ArgueUi.Border(card, edge, picked ? 3f : 1.5f);
            text.style.color = ink;
            ArgumentArt.Use(text, e.claim ? ArgumentArt.Display : ArgumentArt.BodyBold);
            text.style.fontSize = e.claim ? 22 : 19;
            // A note on the page sits a little askew; the one picked straightens and lifts.
            float tilt = sky || picked ? 0f : ((i * 37) % 7 - 3) * 0.8f;
            card.style.rotate = new Rotate(new Angle(tilt, AngleUnit.Degree));
            card.style.scale = new Scale(picked ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one);
        }

        string _focusFor = "";
        int _focusKey = -1;

        /// <summary>The statement picked, in full: what she said (the words that matter in gold), what kind it is and where
        /// it stands, her question if she's asked one, and what she said when asked why.</summary>
        void Focus(BattleFlow f)
        {
            bool on = f.Sections && f.State == BattleState.Argue;
            if (!on) { if (_focusKey != -1) { _focusKey = -1; _focusFor = ""; _focus.style.display = DisplayStyle.None; } return; }
            string id = f.SelectedId;
            var e = f.Engine;
            int key = (int)e.Status(id) * 16 + e.TimesAsked(id) + (e.IsHeard(id) ? 8 : 0);
            if (id == _focusFor && key == _focusKey) return;
            _focusFor = id;
            _focusKey = key;
            _focus.style.display = DisplayStyle.Flex;
            if (id == ArgumentEngine.ClaimId)
            {
                _focusTag.text = S["screen.claim"];
                _focusText.text = Typewriter.StripTags(f.Def.claim);
                _focusSaid.text = S["screen.prompt.claim"];
                return;
            }
            var p = f.Def.Premise(id);
            if (p == null) return;
            var st = e.Status(id);
            _focusTag.text = p.label + "  ·  " + (p.kind == PremiseKind.Fact ? S["screen.fact"] : S["screen.link"]) + (p.key ? "  ·  " + S["screen.key"] : "")
                + "  ·  " + StatusWords(st, e.IsHeard(id));
            string plain = Typewriter.StripTags(Typewriter.Spelled(p.text));
            Debate.Split(plain, p.weak, out string pre, out string weak, out string post);
            _focusText.text = st == PremiseStatus.Broken ? "<s>" + plain + "</s>" : pre + "<color=#FFCC61><u>" + weak + "</u></color>" + post;
            string said = "";
            if (!string.IsNullOrEmpty(p.question) && st == PremiseStatus.Standing && p.retort != null) said = S["screen.argue.asks"] + " “" + Typewriter.StripTags(Typewriter.Spelled(p.question)) + "”";
            else if (e.TimesAsked(id) > 0 && !string.IsNullOrEmpty(p.askReply)) said = S["screen.argue.said"] + " " + Typewriter.StripTags(Typewriter.Spelled(e.TimesAsked(id) >= 2 && p.askAgainReply.Length > 0 ? p.askAgainReply : p.askReply));
            _focusSaid.text = said;
            _focusSaid.style.display = said.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------- Her sky ----------

        bool _skyShown;

        /// <summary>The ceiling lifts (SELECTION.md 7): her night comes down over the table, which sinks away.</summary>
        void Sky(BattleFlow f)
        {
            bool sky = f.Sky;
            if (sky != _skyShown)
            {
                _skyShown = sky;
                _skyBg.style.display = sky ? DisplayStyle.Flex : DisplayStyle.None;
                if (sky && _skyBg.style.backgroundImage.value.texture == null) _skyBg.style.backgroundImage = new StyleBackground(StageArt.Sky());
                if (!sky) { _arenaBg.style.opacity = 1f; _arenaBg.style.translate = new Translate(0, 0); }
            }
            if (!sky) return;
            float t = Smooth(f.SkyProgress);
            _skyBg.style.opacity = t;
            _skyBg.style.translate = new Translate(0, (1f - t) * -ArenaH * 0.6f);
            _arenaBg.style.opacity = 1f - 0.9f * t;
            _arenaBg.style.translate = new Translate(0, t * 60f);
        }

        // ---------- Because... ----------

        void Because(BattleFlow f)
        {
            bool show = f.State == BattleState.Because;
            _because.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) { _shownBecause = -1; return; }
            var cards = f.Because;
            int sig = f.BecausePremise.GetHashCode() ^ cards.Count;
            if (sig != _shownBecause)
            {
                _shownBecause = sig;
                var p = f.Def.Premise(f.BecausePremise);
                var ev = f.Def.Evidence(f.BecauseEvidence);
                _becauseHeading.text = S.Format("screen.prompt.reasonHeading", "opponent", f.Def.opponent, "premise", p != null ? p.text : "", "evidence", ev != null ? ev.name : f.BecauseEvidence);
                int letters = 0;
                foreach (var c in cards) letters += c.reason.text.Length;
                // The cards size to their text; with a lot to read the type steps down so they all fit the table.
                float font = letters > 360 ? 23f : letters > 280 ? 25f : 27f;
                for (int i = 0; i < ReasonPool; i++)
                {
                    bool used = i < cards.Count;
                    _reasonCards[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!used) continue;
                    _reasonTexts[i].text = (string.IsNullOrEmpty(cards[i].supporter) ? "" : "<i>(" + cards[i].supporter + ")</i>  ") + S["screen.because"] + " " + cards[i].reason.text;
                    _reasonTexts[i].style.fontSize = font;
                }
                _shownFocus = -1;
            }
            if (f.BecauseFocus != _shownFocus)
            {
                _shownFocus = f.BecauseFocus;
                for (int i = 0; i < ReasonPool; i++)
                {
                    bool on = i == f.BecauseFocus;
                    ArgueUi.Border(_reasonCards[i], on ? Gold : new Color(0.3f, 0.27f, 0.32f), on ? 4f : 2f);
                    _reasonCards[i].style.backgroundColor = on ? new Color(1f, 0.95f, 0.82f) : Paper;
                    _reasonCards[i].style.translate = new Translate(on ? 10f : 0f, 0);
                }
            }
            // Her patience as a scale, not a width (no layout every frame: docs/redesign/perf.md, Battle 2).
            float patience = f.Patience;
            _patienceFill.style.width = Length.Percent(100f);
            _patienceFill.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            _patienceFill.style.scale = new Scale(new Vector3(Mathf.Max(0.0001f, patience), 1f, 1f));
            bool low = patience < 0.3f;
            if (low != _patienceLow) { _patienceLow = low; _patienceFill.style.backgroundColor = low ? WineBright : Wine; }
            bool story = f.Engine.Setup.difficulty == ArgumentDifficulty.Story;
            if (story != _shownBecauseClock || _becausePatienceLabel.text.Length == 0)
            {
                _shownBecauseClock = story;
                _becausePatienceLabel.text = story ? S["screen.because.noClock"] : S.Format("screen.because.patience", "opponent", f.Def.opponent);
            }
        }

        bool _patienceLow;

        // ---------- The heart monitor ----------

        void Monitor(BattleFlow f, float dt)
        {
            int bpm = f.Engine.HeartRate;
            // Rebuilt only when the rate changes (docs/redesign/perf.md, Battle 1).
            if (bpm != _shownBpm)
            {
                _shownBpm = bpm;
                _bpm.text = bpm.ToString(CultureInfo.InvariantCulture) + " " + S["screen.bpm"];
                _bpm.style.color = f.Engine.HeartStep >= 4 ? WineBright : f.Engine.HeartStep >= 2 ? new Color(1f, 0.62f, 0.4f) : Gold;
            }
            // The trace: a beat at her heart rate, scrolling.
            _monitorPhase += dt * bpm / 60f;
            float ph = _monitorPhase - Mathf.Floor(_monitorPhase);
            float v = ph < 0.06f ? Mathf.Sin(ph / 0.06f * Mathf.PI) * 0.9f : ph < 0.1f ? -0.35f * Mathf.Sin((ph - 0.06f) / 0.04f * Mathf.PI) : ph > 0.35f && ph < 0.45f ? 0.18f * Mathf.Sin((ph - 0.35f) / 0.1f * Mathf.PI) : 0f;
            _ecg[_ecgHead] = v;
            _ecgHead = (_ecgHead + 1) % _ecg.Length;
        }

        void DrawMonitor(MeshGenerationContext mgc)
        {
            if (_flow == null) return;
            var p = mgc.painter2D;
            var r = _monitor.contentRect;
            float w = float.IsNaN(r.width) || r.width <= 0f ? 250f : r.width;
            float h = float.IsNaN(r.height) || r.height <= 0f ? 84f : r.height;
            p.fillColor = new Color(0f, 0f, 0f, 0.55f);
            p.BeginPath();
            p.MoveTo(new Vector2(0, 0)); p.LineTo(new Vector2(w, 0)); p.LineTo(new Vector2(w, h)); p.LineTo(new Vector2(0, h)); p.ClosePath();
            p.Fill();
            var c = _flow.Engine.HeartStep >= 4 ? WineBright : Gold;
            p.strokeColor = new Color(c.r, c.g, c.b, 0.9f);
            p.lineWidth = 2.2f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            int n = _ecg.Length;
            for (int i = 0; i < n; i++)
            {
                float x = 8f + (w - 16f) * i / (n - 1);
                float y = h * 0.66f - _ecg[(_ecgHead + i) % n] * h * 0.42f;
                if (i == 0) p.MoveTo(new Vector2(x, y));
                else p.LineTo(new Vector2(x, y));
            }
            p.Stroke();
        }

        // ---------- The table: pressure, the light, slips, the showdown ----------

        void DrawArena(MeshGenerationContext mgc)
        {
            var f = _flow;
            if (f == null) return;
            var p = mgc.painter2D;
            var phase = Phase(f);
            if (f.Sections) DrawCase(p, f, phase);
            if (phase != null && f.State != BattleState.Showdown)
            {
                DrawWarnings(p, phase);
                DrawHazards(p, phase);
                DrawSections(p, phase);
            }
            if (f.State == BattleState.Argue) DrawPatience(p, f);
            if (f.State == BattleState.Showdown && f.Showdown != null) DrawShowdown(p, f.Showdown);
            if (f.State == BattleState.Ending || f.State == BattleState.Over) DrawEndBeat(p, f);
            if (f.State == BattleState.Debate)
                foreach (var s in f.Debate.Slips) if (s.active) DrawSlip(p, s.position, s.premise, s.age);
            if (f.State == BattleState.Pressure && phase != null)
                foreach (var s in phase.Slips) if (s.active) DrawSlip(p, s.position, s.premise, s.age);
            if (f.State == BattleState.Debate || f.State == BattleState.Pressure || f.State == BattleState.Talk || f.State == BattleState.Because)
                DrawLight(p, f, phase);
        }

        /// <summary>Graphite first (SELECTION.md 1: she sketches it before she inks it): a slightly shaky pencil line
        /// where each attack will be, darker and quicker as its warning runs out.</summary>
        void DrawWarnings(Painter2D p, PressurePhase phase)
        {
            float tele = Mathf.Max(0.05f, phase.Spec.telegraph);
            int jitterStep = Mathf.FloorToInt(_time * 8f);
            var walls = phase.Walls;
            float step = PressurePhase.PlateRadius * 2.05f;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (!w.active || w.warn <= 0f) continue;
                float a = WarnAlpha(w.warn, tele);
                p.strokeColor = new Color(Graphite.r, Graphite.g, Graphite.b, a);
                p.lineWidth = 1.6f;
                float x = Mathf.Sign(w.x) * (PressurePhase.HalfWidth - PressurePhase.PlateRadius);
                for (float y = -PressurePhase.HalfHeight + PressurePhase.PlateRadius; y <= PressurePhase.HalfHeight; y += step)
                {
                    if (phase.WallGapAt(w, y) || phase.WallGapAt(w, y + PressurePhase.PlateRadius) || phase.WallGapAt(w, y - PressurePhase.PlateRadius)) continue;
                    var c = ToPx(new Vec(x, y)) + Jitter(w.serial * 31 + (int)(y * 10f), jitterStep);
                    p.BeginPath();
                    p.Arc(c, PressurePhase.PlateRadius * Px * 0.9f, 0f, 360f);
                    p.Stroke();
                }
            }
            var rings = phase.Rings;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (!r.active || r.warn <= 0f || r.hush) continue;
                float a = WarnAlpha(r.warn, tele);
                var c = ToPx(r.center) + Jitter(r.serial, jitterStep);
                p.strokeColor = new Color(Graphite.r, Graphite.g, Graphite.b, a);
                p.lineWidth = 1.6f;
                if (r.answerMe)
                {
                    // The phone, ringing: sound waves at the table's edge.
                    for (int k = 1; k <= 3; k++) Arc(p, c, 18f * k + 6f * Mathf.Sin(_time * 14f), Mathf.PI * 0.6f, Mathf.PI * 1.4f);
                    continue;
                }
                float rad = 0.45f * Px;
                if (r.gapCount > 1)
                {
                    Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle1 - r.gapHalfAngle);
                    Arc(p, c, rad, r.gapAngle1 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
                }
                else Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
                p.strokeColor = new Color(Gold.r, Gold.g, Gold.b, a * 0.5f);
                p.lineWidth = 1.4f;
                Lane(p, r.center, r.gapAngle0);
                if (r.gapCount > 1) Lane(p, r.center, r.gapAngle1);
            }
            var blobs = phase.Blobs;
            for (int i = 0; i < blobs.Count; i++)
            {
                var b = blobs[i];
                if (!b.active || b.warn <= 0f || b.mark) continue;
                float a = WarnAlpha(b.warn, tele);
                var from = b.position;
                var to = b.position + b.velocity * 10f;
                if (!ClipToBox(ref from, ref to)) continue;
                p.strokeColor = new Color(Graphite.r, Graphite.g, Graphite.b, a * 0.8f);
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(ToPx(from) + Jitter(b.serial, jitterStep));
                p.LineTo(ToPx(to) + Jitter(b.serial + 7, jitterStep));
                p.Stroke();
            }
        }

        static Vector2 Jitter(int seed, int step)
        {
            uint h = (uint)(seed * 73856093 ^ step * 19349663);
            h = (h ^ (h >> 13)) * 1274126177u;
            float a = ((h & 0xffff) / 65535f - 0.5f) * 3f, b = (((h >> 16) & 0xffff) / 65535f - 0.5f) * 3f;
            return new Vector2(a, b);
        }

        static float WarnAlpha(float warn, float telegraph)
        {
            float left = Mathf.Clamp01(warn / telegraph);
            return Mathf.Lerp(0.85f, 0.3f, left);
        }

        /// <summary>Ink: wine plates with a paper rim, her rings, her lobs; the three rings as dark jagged waves.</summary>
        void DrawHazards(Painter2D p, PressurePhase phase)
        {
            var walls = phase.Walls;
            float step = PressurePhase.PlateRadius * 2.05f;
            float pr = PressurePhase.PlateRadius * Px;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (!w.active || w.warn > 0f) continue;
                for (float y = -PressurePhase.HalfHeight + PressurePhase.PlateRadius; y <= PressurePhase.HalfHeight; y += step)
                {
                    if (phase.WallGapAt(w, y) || phase.WallGapAt(w, y + PressurePhase.PlateRadius) || phase.WallGapAt(w, y - PressurePhase.PlateRadius)) continue;
                    var c = ToPx(new Vec(w.x, y));
                    p.fillColor = Wine;
                    Disc(p, c, pr);
                    p.strokeColor = new Color(Paper.r, Paper.g, Paper.b, 0.85f);
                    p.lineWidth = 2f;
                    p.BeginPath();
                    p.Arc(c, pr * 0.66f, 0f, 360f);
                    p.Stroke();
                }
            }
            var rings = phase.Rings;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (!r.active || r.warn > 0f || r.radius <= 0.01f || r.hush) continue;
                var c = ToPx(r.center);
                float rad = r.radius * Px;
                if (r.answerMe)
                {
                    p.strokeColor = new Color(0.02f, 0.02f, 0.03f, 0.95f);
                    p.lineWidth = PressurePhase.RingThickness * Px * 0.9f;
                    p.BeginPath();
                    p.Arc(c, rad, 0f, 360f);
                    p.Stroke();
                    p.strokeColor = WineBright;
                    p.lineWidth = 2.5f;
                    for (int k = 0; k < 40; k++)
                    {
                        float a = k * Mathf.PI * 2f / 40f + _time * 0.6f;
                        var dir = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                        p.BeginPath();
                        p.MoveTo(c + dir * (rad - 10f));
                        p.LineTo(c + dir * (rad + 10f));
                        p.Stroke();
                    }
                    continue;
                }
                p.strokeColor = r.guard ? new Color(WineBright.r, WineBright.g, WineBright.b, 0.85f) : Wine;
                p.lineWidth = PressurePhase.RingThickness * Px * (r.guard ? 0.7f : 0.85f);
                if (r.gapCount > 1)
                {
                    Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle1 - r.gapHalfAngle);
                    Arc(p, c, rad, r.gapAngle1 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
                }
                else Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
            }
            var blobs = phase.Blobs;
            for (int i = 0; i < blobs.Count; i++)
            {
                var b = blobs[i];
                if (!b.active || b.warn > 0f || b.mark || b.streak) continue;
                var c = ToPx(b.position);
                p.fillColor = Wine;
                Disc(p, c, PressurePhase.BlobRadius * Px);
                p.fillColor = new Color(Paper.r, Paper.g, Paper.b, 0.7f);
                Disc(p, c + new Vector2(-6f, -6f), 5f);
            }
        }

        void DrawLight(Painter2D p, BattleFlow f, PressurePhase phase)
        {
            // In the sections the light is only on the table while she comes at it.
            if (f.Sections && f.State != BattleState.Pressure) return;
            var light = f.State == BattleState.Pressure && phase != null ? phase.Light : f.Light;
            var c = ToPx(light);
            bool blink = phase != null && phase.Invulnerable > 0f && ((int)(phase.Invulnerable * 12f) & 1) == 0;
            if (blink) return;
            p.fillColor = new Color(Gold.r, Gold.g, Gold.b, 0.22f);
            Disc(p, c, PressurePhase.LightRadius * Px * 2.6f);
            p.fillColor = Gold;
            Disc(p, c, PressurePhase.LightRadius * Px);
            // Hush or answer-me on the table: a ring round the light, whole while it holds still, broken while it moves
            // (how a player sees which they're doing: the creator's "need a way to show that to the player").
            if (phase != null && phase.StillnessMatters)
            {
                bool still = !phase.LightMoving;
                p.strokeColor = still ? Paper : new Color(Paper.r, Paper.g, Paper.b, 0.55f);
                p.lineWidth = 2.5f;
                float r = PressurePhase.LightRadius * Px * 2.2f;
                if (still) { p.BeginPath(); p.Arc(c, r, 0f, 360f); p.Stroke(); }
                else for (int k = 0; k < 8; k++) Arc(p, c, r, k * Mathf.PI / 4f + _time * 3f, k * Mathf.PI / 4f + _time * 3f + 0.35f);
            }
            if (f.State != BattleState.Debate) return;
            // The aim: a ring as far as the light reaches; bright when it's on a statement's weak words.
            int under = f.Debate.Under(light, true, out bool onWeak);
            float a = under < 0 ? 0.35f : onWeak ? 1f : 0.6f;
            p.strokeColor = new Color(Gold.r, Gold.g, Gold.b, a);
            p.lineWidth = onWeak ? 3f : 2f;
            p.BeginPath();
            p.Arc(c, Debate.AimRadius * Px, 0f, 360f);
            p.Stroke();
            if (f.HoldProgress > 0f)
            {
                p.strokeColor = f.HoldingPress ? WineBright : Paper;
                p.lineWidth = 5f;
                p.BeginPath();
                p.Arc(c, Debate.AimRadius * Px + 8f, -90f, -90f + 360f * f.HoldProgress);
                p.Stroke();
            }
        }

        /// <summary>Her case under the cards: in the kitchen, pencil lines from each statement up to what it holds up (rubbed
        /// out once it's down, torn where it was forced); under her sky, her constellation: lit lines her showers run along,
        /// faint traces, snapped splinters, and a star at each statement (a conceded fact glows: cover).</summary>
        void DrawCase(Painter2D p, BattleFlow f, PressurePhase phase)
        {
            bool sky = f.Sky;
            bool argue = f.State == BattleState.Argue || f.State == BattleState.Talk || f.State == BattleState.Because || f.State == BattleState.Intro;
            if (!argue && !(sky && f.State == BattleState.Pressure)) return;
            var map = f.Map;
            float fade = f.State == BattleState.Argue || f.State == BattleState.Pressure ? 1f : 0.5f;
            if (sky) fade *= Mathf.Clamp01(f.SkyProgress * 1.5f);
            var links = map.Links;
            for (int i = 0; i < links.Count; i++)
            {
                var l = links[i];
                if (!l.visible || (l.across && !sky)) continue;
                var a = ToPx(map[l.a].position);
                var b = ToPx(map[l.b].position);
                if (sky)
                {
                    switch (l.state)
                    {
                        case CaseMap.LinkState.Lit:
                            p.strokeColor = new Color(1f, 0.93f, 0.75f, 0.25f * fade);
                            p.lineWidth = 9f;
                            Line(p, a, b);
                            p.strokeColor = new Color(1f, 0.95f, 0.82f, 0.9f * fade);
                            p.lineWidth = 2.5f;
                            Line(p, a, b);
                            break;
                        case CaseMap.LinkState.Rubbed:
                            p.strokeColor = new Color(0.7f, 0.72f, 0.85f, 0.35f * fade);
                            p.lineWidth = 1.5f;
                            Dashed(p, a, b, 10f, 8f);
                            break;
                        default:
                            // Snapped: two splinters and a gap.
                            p.strokeColor = new Color(WineBright.r, WineBright.g, WineBright.b, 0.8f * fade);
                            p.lineWidth = 2f;
                            Line(p, a, Vector2.Lerp(a, b, 0.4f));
                            Line(p, Vector2.Lerp(a, b, 0.6f), b);
                            break;
                    }
                }
                else
                {
                    // Graphite on her page, with an arrowhead where it leads.
                    var col = l.state == CaseMap.LinkState.Lit ? new Color(0.2f, 0.19f, 0.22f, 0.85f) : l.state == CaseMap.LinkState.Snapped ? new Color(Wine.r, Wine.g, Wine.b, 0.9f) : new Color(0.3f, 0.29f, 0.32f, 0.35f);
                    p.strokeColor = new Color(col.r, col.g, col.b, col.a * fade);
                    p.lineWidth = l.state == CaseMap.LinkState.Lit ? 2.2f : 1.4f;
                    var jitter = Jitter(i * 17, Mathf.FloorToInt(_time * 3f)) * 0.4f;
                    if (l.state == CaseMap.LinkState.Rubbed) Dashed(p, a, b + jitter, 7f, 6f);
                    else if (l.state == CaseMap.LinkState.Snapped) { Line(p, a, Vector2.Lerp(a, b, 0.42f)); Line(p, Vector2.Lerp(a, b, 0.58f), b); }
                    else
                    {
                        Line(p, a, b + jitter);
                        var dir = (b - a).normalized;
                        var tip = Vector2.Lerp(a, b, 0.62f);
                        var side = new Vector2(-dir.y, dir.x);
                        p.BeginPath();
                        p.MoveTo(tip - dir * 9f + side * 6f);
                        p.LineTo(tip);
                        p.LineTo(tip - dir * 9f - side * 6f);
                        p.Stroke();
                    }
                }
            }
            if (!sky) return;
            for (int i = 0; i < map.Count; i++)
            {
                var e = map[i];
                if (!e.visible) continue;
                var c = ToPx(e.position);
                bool cover = e.status == PremiseStatus.Conceded;
                if (cover)
                {
                    p.fillColor = new Color(Gold.r, Gold.g, Gold.b, 0.18f * fade);
                    Disc(p, c, PressurePhase.CoverRadius * Px);
                }
                float r = e.claim ? 13f : e.status == PremiseStatus.Standing ? 10f : 7f;
                p.fillColor = new Color(1f, 0.96f, 0.85f, (e.status == PremiseStatus.Standing || e.claim || cover ? 1f : 0.45f) * fade);
                Star(p, c, r, r * 0.42f, _time * 0.2f + i);
            }
        }

        /// <summary>Her patience, as a pencil line drawing itself along the near edge; in its last seconds, the attack she's
        /// sketching at the table's edges (the overlap into the next dodge).</summary>
        void DrawPatience(Painter2D p, BattleFlow f)
        {
            if (f.ArgueLimit <= 0f) return;
            float k = Mathf.Clamp01(f.ArgueTime / f.ArgueLimit);
            p.strokeColor = new Color(Wine.r, Wine.g, Wine.b, 0.6f + 0.4f * k);
            p.lineWidth = 3f;
            p.BeginPath();
            p.MoveTo(new Vector2(20f, ArenaH - 8f));
            p.LineTo(new Vector2(20f + (ArenaW - 40f) * k, ArenaH - 8f + Mathf.Sin(_time * 7f) * 0.8f));
            p.Stroke();
            float s = f.ArgueSketch;
            if (s <= 0f) return;
            int step = Mathf.FloorToInt(_time * 8f);
            p.strokeColor = new Color(Graphite.r, Graphite.g, Graphite.b, 0.25f + 0.6f * s);
            p.lineWidth = 1.6f;
            int n = Mathf.CeilToInt(9 * s);
            for (int i = 0; i < n; i++)
            {
                float y = 40f + i * (ArenaH - 80f) / 8f;
                var left = new Vector2(PressurePhase.PlateRadius * Px + 6f, y) + Jitter(i * 11, step);
                var right = new Vector2(ArenaW - PressurePhase.PlateRadius * Px - 6f, ArenaH - y) + Jitter(i * 13 + 5, step);
                p.BeginPath(); p.Arc(left, PressurePhase.PlateRadius * Px * 0.9f, 0f, 360f); p.Stroke();
                p.BeginPath(); p.Arc(right, PressurePhase.PlateRadius * Px * 0.9f, 0f, 360f); p.Stroke();
            }
        }

        /// <summary>The sections' own hazards: hush (soft, round, dashed, closing in), her pencil's marks (graphite, then
        /// ink) and its tip, and shooting stars under her sky (a bright head, a tail back toward the radiant).</summary>
        void DrawSections(Painter2D p, PressurePhase phase)
        {
            var rings = phase.Rings;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (!r.active || !r.hush) continue;
                var c = ToPx(r.center);
                float rad = Mathf.Max(2f, r.radius * Px);
                float a = r.warn > 0f ? 0.45f : 0.9f;
                p.strokeColor = new Color(Paper.r, Paper.g, Paper.b, a);
                p.lineWidth = r.warn > 0f ? 2f : PressurePhase.RingThickness * Px * 0.45f;
                int dashes = 28;
                for (int k = 0; k < dashes; k++)
                {
                    float a0 = k * Mathf.PI * 2f / dashes + _time * 0.4f;
                    Arc(p, c, rad, a0, a0 + Mathf.PI / dashes);
                }
            }
            var blobs = phase.Blobs;
            float pr = PressurePhase.BlobRadius * Px;
            for (int i = 0; i < blobs.Count; i++)
            {
                var b = blobs[i];
                if (!b.active) continue;
                var c = ToPx(b.position);
                if (b.mark)
                {
                    if (b.warn > 0f)
                    {
                        // Pencilled: a small cross.
                        p.strokeColor = new Color(Graphite.r, Graphite.g, Graphite.b, 0.8f);
                        p.lineWidth = 1.8f;
                        Line(p, c + new Vector2(-6f, -6f), c + new Vector2(6f, 6f));
                        Line(p, c + new Vector2(-6f, 6f), c + new Vector2(6f, -6f));
                    }
                    else
                    {
                        p.fillColor = new Color(Wine.r, Wine.g, Wine.b, Mathf.Clamp01(b.ttl / 0.4f));
                        Disc(p, c, pr * 0.8f);
                    }
                    continue;
                }
                if (b.streak && b.warn <= 0f)
                {
                    var tail = c - new Vector2(b.velocity.X, -b.velocity.Y).normalized * 60f;
                    p.strokeColor = new Color(1f, 0.93f, 0.78f, 0.35f);
                    p.lineWidth = 5f;
                    Line(p, tail, c);
                    p.fillColor = new Color(1f, 0.97f, 0.9f);
                    Disc(p, c, pr * 0.7f);
                }
            }
            if (phase.TrailOn)
            {
                // Her pencil, following.
                var tip = ToPx(phase.TrailTip);
                p.strokeColor = new Color(0.25f, 0.24f, 0.27f, 0.95f);
                p.lineWidth = 5f;
                Line(p, tip + new Vector2(4f, -4f), tip + new Vector2(34f, -34f));
                p.fillColor = new Color(0.9f, 0.75f, 0.45f);
                p.BeginPath();
                p.MoveTo(tip);
                p.LineTo(tip + new Vector2(9f, -2f));
                p.LineTo(tip + new Vector2(2f, -9f));
                p.ClosePath();
                p.Fill();
            }
        }

        static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        static void Dashed(Painter2D p, Vector2 a, Vector2 b, float dash, float gap)
        {
            float len = Vector2.Distance(a, b);
            if (len < 1f) return;
            var dir = (b - a) / len;
            for (float s = 0f; s < len; s += dash + gap) Line(p, a + dir * s, a + dir * Mathf.Min(len, s + dash));
        }

        static void Star(Painter2D p, Vector2 c, float outer, float inner, float spin)
        {
            p.BeginPath();
            for (int k = 0; k < 10; k++)
            {
                float a = spin + k * Mathf.PI / 5f - Mathf.PI / 2f;
                float r = (k & 1) == 0 ? outer : inner;
                var v = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                if (k == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.ClosePath();
            p.Fill();
        }

        void DrawSlip(Painter2D p, Vec at, string premise, float age)
        {
            var c = ToPx(at);
            float wob = Mathf.Sin(age * 3f) * 6f;
            p.fillColor = Paper;
            p.strokeColor = Ink;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-24f, -10f + wob));
            p.LineTo(c + new Vector2(26f, 0f));
            p.LineTo(c + new Vector2(-24f, 12f - wob));
            p.LineTo(c + new Vector2(-14f, 0f));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }

        void DrawShowdown(Painter2D p, Showdown sd)
        {
            float markX = ToPx(new Vec(Showdown.Mark, 0f)).x;
            float w0 = ToPx(new Vec(Showdown.Mark - Showdown.Window, 0f)).x, w1 = ToPx(new Vec(Showdown.Mark + Showdown.Window, 0f)).x;
            p.fillColor = new Color(Gold.r, Gold.g, Gold.b, sd.InWindow ? 0.2f : 0.1f);
            p.BeginPath();
            p.MoveTo(new Vector2(w0, 40f)); p.LineTo(new Vector2(w1, 40f)); p.LineTo(new Vector2(w1, ArenaH - 70f)); p.LineTo(new Vector2(w0, ArenaH - 70f)); p.ClosePath();
            p.Fill();
            p.strokeColor = sd.Staggered ? Dim : Gold;
            p.lineWidth = 4f;
            p.BeginPath();
            p.MoveTo(new Vector2(markX, 40f));
            p.LineTo(new Vector2(markX, ArenaH - 70f));
            p.Stroke();
            // Each cut leaves a slash across the word.
            foreach (var w in sd.Words)
            {
                if (!w.active || !w.cut) continue;
                var c = ToPx(new Vec(w.x, 0.15f));
                float a = Mathf.Clamp01(1f - w.doneAge / 0.5f);
                p.strokeColor = new Color(Gold.r, Gold.g, Gold.b, a);
                p.lineWidth = 6f;
                p.BeginPath();
                p.MoveTo(c + new Vector2(-w.width * Px * 0.6f, 40f));
                p.LineTo(c + new Vector2(w.width * Px * 0.6f, -40f));
                p.Stroke();
            }
            // Nadia, at her mark.
            var n = ToPx(new Vec(Showdown.Mark - 0.35f, 0.15f));
            p.fillColor = Gold;
            Disc(p, n, 14f);
        }

        void Lane(Painter2D p, Vec centre, float angle)
        {
            var dir = new Vec(Mathf.Cos(angle), Mathf.Sin(angle));
            var a = centre + dir * 0.55f;
            var b = centre + dir * 8f;
            if (!ClipToBox(ref a, ref b)) return;
            p.BeginPath();
            p.MoveTo(ToPx(a));
            p.LineTo(ToPx(b));
            p.Stroke();
        }

        static bool ClipToBox(ref Vec a, ref Vec b)
        {
            float t0 = 0f, t1 = 1f;
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (!Clip(-dx, a.X + PressurePhase.HalfWidth, ref t0, ref t1)) return false;
            if (!Clip(dx, PressurePhase.HalfWidth - a.X, ref t0, ref t1)) return false;
            if (!Clip(-dy, a.Y + PressurePhase.HalfHeight, ref t0, ref t1)) return false;
            if (!Clip(dy, PressurePhase.HalfHeight - a.Y, ref t0, ref t1)) return false;
            var start = a;
            a = new Vec(start.X + dx * t0, start.Y + dy * t0);
            b = new Vec(start.X + dx * t1, start.Y + dy * t1);
            return true;
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }

        static void Disc(Painter2D p, Vector2 c, float r)
        {
            p.BeginPath();
            p.Arc(c, r, 0f, 360f);
            p.Fill();
        }

        static void Arc(Painter2D p, Vector2 c, float r, float fromRad, float toRad)
        {
            p.BeginPath();
            p.Arc(c, r, -toRad * Mathf.Rad2Deg, -fromRad * Mathf.Rad2Deg);
            p.Stroke();
        }

        static string Pips(int n, int max)
        {
            var c = new char[max];
            for (int i = 0; i < max; i++) c[i] = i < n ? '●' : '○';
            return new string(c);
        }

        // ---------- The case file (paused) ----------

        public void ShowCaseFile(BattleFlow f, bool shown)
        {
            _caseShown = shown;
            _caseFile.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) return;
            _caseTitle.text = S["screen.case.title"];
            _caseScroll.Clear();
            var def = f.Def;
            var e = f.Engine;
            Section(S.Format("screen.case.premises", "opponent", def.opponent));
            foreach (var p in def.premises)
            {
                if (p == null) continue;
                var st = e.Status(p.id);
                if (st == PremiseStatus.Hidden) { Row(p.label + "  ·  " + S["screen.status.hidden"], S["screen.hiddenText"], "", Dim); continue; }
                string said = e.TimesAsked(p.id) >= 2 && p.askAgainReply.Length > 0 ? p.askReply + "\n" + p.askAgainReply : e.TimesAsked(p.id) >= 1 ? p.askReply : "";
                if (p.question.Length > 0 && st == PremiseStatus.Standing) said = (said.Length > 0 ? said + "\n" : "") + S.Format("screen.prompt.question", "question", p.question);
                Row(p.label + "  ·  " + (p.kind == PremiseKind.Fact ? S["screen.fact"] : S["screen.link"]) + (p.key ? "  ·  " + S["screen.key"] : "") + "  ·  " + StatusWords(st, e.IsHeard(p.id)),
                    st == PremiseStatus.Broken ? "<s>" + p.text + "</s>" : p.text, said, st == PremiseStatus.Standing ? Paper : Dim);
            }
            Section(S["screen.case.evidence"]);
            foreach (var c in f.Cards)
            {
                Name(f, c, out string name, out string note);
                int b = f.BurnedFor(c);
                Row(name + (b > 0 ? "  ·  " + S.Format("screen.hand.burned", "n", b.ToString(CultureInfo.InvariantCulture)) : ""), note, "", b > 0 ? Dim : Gold);
            }
            Section(S["screen.transcriptTitle"]);
            var t = e.Transcript;
            for (int i = Mathf.Max(0, t.Count - 40); i < t.Count; i++)
            {
                var x = t[i];
                var l = Text(null, x.speaker == Speaker.Logic ? x.text : "<b>" + x.who + ":</b> " + x.text, x.speaker == Speaker.Logic ? ArgumentArt.BodyItalic : ArgumentArt.Body, 22,
                    x.speaker == Speaker.Logic ? Gold : x.speaker == Speaker.Opponent ? new Color(1f, 0.75f, 0.78f) : x.speaker == Speaker.Supporter ? MagsBlue : Paper);
                l.style.marginBottom = 6;
                _caseScroll.Add(l);
            }
        }

        public bool CaseShown => _caseShown;

        public void ScrollCaseFile(int dir) => _caseScroll.scrollOffset += new Vector2(0f, dir * 140f);

        void Section(string title)
        {
            var l = Text(null, title, ArgumentArt.Display, 30, Gold);
            l.style.marginTop = 18;
            l.style.marginBottom = 8;
            _caseScroll.Add(l);
        }

        void Row(string head, string body, string said, Color ink)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.marginBottom = 10;
            box.style.paddingLeft = 12;
            ArgueUi.Border(box, new Color(0f, 0f, 0f, 0f), 0f);
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = ink;
            box.Add(Text(null, head, ArgumentArt.BodyBold, 18, Dim));
            box.Add(Text(null, body, ArgumentArt.Body, 25, ink));
            if (!string.IsNullOrEmpty(said)) box.Add(Text(null, said, ArgumentArt.BodyItalic, 21, new Color(1f, 0.78f, 0.8f)));
            _caseScroll.Add(box);
        }

        static string StatusWords(PremiseStatus s, bool heard)
        {
            switch (s)
            {
                case PremiseStatus.Broken: return S["screen.status.broken"];
                case PremiseStatus.Conceded: return S["screen.status.conceded"];
                case PremiseStatus.ConcededFalse: return S["screen.status.agreed"];
                case PremiseStatus.Forced: return S["screen.status.forced"];
                case PremiseStatus.Hidden: return S["screen.status.hidden"];
                default: return heard ? S["screen.status.heard"] : S["screen.status.standing"];
            }
        }

        // ---------- The end card ----------

        public void ShowEnd(string title, string body, string cost, string continueLabel, bool good)
        {
            if (_endShown) return;
            _endShown = true;
            _endTitle.text = title;
            _endTitle.style.color = good ? new Color(0.55f, 0.36f, 0.08f) : Wine;
            _endBody.text = body;
            _endCost.text = cost;
            _endCost.style.display = string.IsNullOrEmpty(cost) ? DisplayStyle.None : DisplayStyle.Flex;
            var label = _continue.Q<Label>("argument-continue-text");
            if (label != null) label.text = continueLabel;
            _end.style.display = DisplayStyle.Flex;
            Tween(0.6f, t => { _end.style.opacity = t; _end.style.translate = new Translate(0, (1f - t) * 40f); }, null);
        }

        public void FocusContinue(bool on) => ArgueUi.Focus(_continue, on);
    }

    static class CueListExtensions
    {
        public static bool Contains(this IReadOnlyList<Cue> cues, CueKind kind)
        {
            for (int i = 0; i < cues.Count; i++) if (cues[i].kind == kind) return true;
            return false;
        }
    }
}
