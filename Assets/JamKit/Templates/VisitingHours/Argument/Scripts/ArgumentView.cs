using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument
{
    /// <summary>
    /// The argument battle's screen (UI Toolkit on the shell's HUD layer, built in code; docs/ARGUMENT-PROTOTYPE.md,
    /// "The screen"). Mostly black: the opponent's sprite large on the left (an expression per state); on the right
    /// the claim as a paper card (the centrepiece), the premises as designed cards, a panel that reads the focused
    /// premise in full (with what they said about it), the verbs; along the bottom a roomy dialogue box with the last
    /// exchange and the step spelled out as a sentence. Evidence, reasons, the transcript and the end card open
    /// over the right side. The pressure phase plays in a box on black beside the sprite. Timed transitions: in from
    /// the world, claims folding away as the box opens (and back), out to the world. The host (ArgumentHost) decides
    /// what's shown and owns the focus (ArgueNav); this class only builds and draws. No per-frame allocation except
    /// while text changes.
    /// </summary>
    public sealed class ArgumentView
    {
        public const string RootName = "argument-view";
        public const int MaxPremises = 12;
        const float Right = 690f, RightW = 1200f;

        public readonly VisualElement Root;
        readonly VisualElement _black, _content, _claims, _surfaceWrap, _surface;
        readonly VisualElement _sprite, _claimCard, _premiseGrid, _focusPanel, _verbRow, _dialogue;
        readonly VisualElement _evidence, _evidenceGrid, _reasons, _reasonList, _transcript, _end;
        readonly ScrollView _transcriptScroll;
        readonly Label _name, _expression, _spriteNote, _heart, _heat, _anger, _placeholder;
        readonly Label _claimLabel, _claimText, _claimKeys;
        readonly Label _focusHeader, _focusText, _focusSaidHeader, _focusSaid, _prompt;
        readonly Label _lineOpponent, _lineNadia, _lineLogic, _hint;
        readonly Label _evidencePrompt, _reasonHeading, _endTitle, _endBody, _pressureLabel, _transcriptTitle;
        readonly List<VisualElement> _cards = new List<VisualElement>(MaxPremises);
        readonly List<Label[]> _cardParts = new List<Label[]>(MaxPremises);
        readonly List<VisualElement> _evidenceCards = new List<VisualElement>(16);
        readonly List<VisualElement> _reasonCards = new List<VisualElement>(5);
        readonly Dictionary<string, VisualElement> _verbs = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, Label> _verbLabels = new Dictionary<string, Label>();
        readonly Label[] _slipLabels = new Label[2];
        readonly VisualElement _endContinue;
        string _shownSprite = "";
        // The sprite's boil (ArgumentBoil): what it distorts, for which expression, and the label F10 shows.
        readonly ArgumentBoilLayer _boil = new ArgumentBoilLayer();
        Texture2D _spriteTexture;
        Texture _spriteShown;
        string _spriteExpression = "";
        readonly Label _boilLabel;
        float _boilLabelFor;
        bool _boilLabelAlways;
        PressurePhase _phase;

        /// <summary>Hover and click on any of its items (the host maps them to its focus model).</summary>
        public Action<VisualElement> Hovered, Clicked;
        /// <summary>The pointer pressed or released on an item (Press is a hold).</summary>
        public Action<VisualElement, bool> PointerHeld;

        public ArgumentView(VisualElement parent)
        {
            Root = new VisualElement { name = RootName, pickingMode = PickingMode.Ignore };
            Root.AddToClassList(UiHider.KeepClass); // the argument is the game: presenter mode's Hide UI keeps it
            ArgueUi.Fill(Root);
            parent.Add(Root);

            // The black: the scene behind is hidden (it fades in from the world, and back out at the end).
            _black = new VisualElement { name = "argument-black", pickingMode = PickingMode.Position };
            ArgueUi.Fill(_black);
            _black.style.backgroundColor = ArgueUi.Black;
            _black.style.opacity = 0f;
            Root.Add(_black);

            _content = new VisualElement { name = "argument-content", pickingMode = PickingMode.Ignore };
            ArgueUi.Fill(_content);
            _content.style.opacity = 0f;
            Root.Add(_content);

            // ---------- Left: the opponent ----------
            _sprite = new VisualElement { name = "argument-sprite", pickingMode = PickingMode.Ignore };
            ArgueUi.Place(_sprite, 20, 60, 640, 860);
            _sprite.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            _sprite.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Bottom));
            _content.Add(_sprite);
            _spriteNote = ArgueUi.Text(_content, "", ArgumentArt.BodyItalic, 16, ArgueUi.Dim, "argument-sprite-note");
            ArgueUi.Place(_spriteNote, 40, 880, 600, 24);
            _boilLabel = ArgueUi.Text(_content, "", ArgumentArt.Body, 17, ArgueUi.Dim, "argument-boil-label");
            // Above her name, the one strip on her side nothing else uses (on her slot the sprite drew over it).
            ArgueUi.Place(_boilLabel, 24, 0, 660, 24);
            _boilLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            _boilLabel.style.paddingLeft = 8;
            _boilLabel.style.display = DisplayStyle.None;
            _name = ArgueUi.Text(_content, "", ArgumentArt.Display, 54, ArgueUi.Ink, "argument-name");
            ArgueUi.Place(_name, 40, 24, 420, 70);
            _expression = ArgueUi.Text(_content, "", ArgumentArt.BodyItalic, 22, ArgueUi.Dim, "argument-expression");
            ArgueUi.Place(_expression, 44, 92, 420, 30);

            var vitals = new VisualElement { pickingMode = PickingMode.Ignore };
            ArgueUi.Place(vitals, 440, 30, 230, 110);
            _content.Add(vitals);
            _heart = ArgueUi.Text(vitals, "", ArgumentArt.Display, 30, ArgueUi.Ink, "argument-heart");
            _heart.style.unityTextAlign = TextAnchor.UpperRight;
            _heat = ArgueUi.Text(vitals, "", ArgumentArt.Body, 20, ArgueUi.Dim, "argument-heat");
            _heat.style.unityTextAlign = TextAnchor.UpperRight;
            _anger = ArgueUi.Text(vitals, "", ArgumentArt.Body, 20, ArgueUi.Bad, "argument-anger");
            _anger.style.unityTextAlign = TextAnchor.UpperRight;

            // ---------- Right: the claims ----------
            _claims = new VisualElement { name = "argument-claims", pickingMode = PickingMode.Ignore };
            ArgueUi.Place(_claims, Right, 24, RightW, 870);
            _claims.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(0));
            _content.Add(_claims);

            _claimCard = ArgueUi.CardBox(_claims, "argument-claim", ArgueUi.Paper, new Color(0.6f, 0.5f, 0.4f));
            ArgueUi.Place(_claimCard, 0, 0, RightW, 150);
            _claimCard.style.paddingLeft = _claimCard.style.paddingRight = 28;
            _claimCard.style.paddingTop = 12;
            Hook(_claimCard);
            _claimLabel = ArgueUi.Text(_claimCard, "", ArgumentArt.BodyBold, 16, new Color(0.5f, 0.38f, 0.3f));
            _claimLabel.style.letterSpacing = 3f;
            _claimText = ArgueUi.Text(_claimCard, "", ArgumentArt.Display, 46, ArgueUi.PaperInk, "argument-claim-text");
            _claimKeys = ArgueUi.Text(_claimCard, "", ArgumentArt.BodyItalic, 19, new Color(0.42f, 0.33f, 0.28f));

            _premiseGrid = new VisualElement { name = "argument-premises", pickingMode = PickingMode.Ignore };
            ArgueUi.Place(_premiseGrid, 0, 168, RightW, 470);
            _premiseGrid.style.flexDirection = FlexDirection.Row;
            _premiseGrid.style.flexWrap = Wrap.Wrap;
            _premiseGrid.style.alignContent = Align.FlexStart;
            _claims.Add(_premiseGrid);
            for (int i = 0; i < MaxPremises; i++) MakePremiseCard(i);

            _focusPanel = ArgueUi.CardBox(_claims, "argument-focus-panel", ArgueUi.Panel, ArgueUi.CardEdge);
            ArgueUi.Place(_focusPanel, 0, 650, RightW, 150);
            _prompt = ArgueUi.Text(_focusPanel, "", ArgumentArt.BodyItalic, 19, ArgueUi.Gold, "argument-prompt");
            _focusHeader = ArgueUi.Text(_focusPanel, "", ArgumentArt.BodyBold, 18, ArgueUi.Dim);
            _focusText = ArgueUi.Text(_focusPanel, "", ArgumentArt.Body, 27, ArgueUi.Ink, "argument-focus-text");
            _focusSaidHeader = ArgueUi.Text(_focusPanel, "", ArgumentArt.BodyBold, 16, ArgueUi.Dim);
            _focusSaidHeader.style.marginTop = 4;
            _focusSaid = ArgueUi.Text(_focusPanel, "", ArgumentArt.BodyItalic, 22, ArgueUi.Blue, "argument-focus-said");

            _verbRow = new VisualElement { name = "argument-verbs", pickingMode = PickingMode.Ignore };
            ArgueUi.Place(_verbRow, 0, 812, RightW, 58);
            _verbRow.style.flexDirection = FlexDirection.Row;
            _claims.Add(_verbRow);
            foreach (var v in new[] { "ask", "challenge", "concede", "press", "rest", "transcript" }) MakeVerb(v);

            // ---------- The pressure box (on black, beside the sprite) ----------
            _surfaceWrap = new VisualElement { name = "argument-pressure", pickingMode = PickingMode.Ignore };
            ArgueUi.Place(_surfaceWrap, Right, 70, RightW, 720);
            _surfaceWrap.style.display = DisplayStyle.None;
            _surfaceWrap.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            _content.Add(_surfaceWrap);
            _pressureLabel = ArgueUi.Text(_surfaceWrap, "", ArgumentArt.BodyItalic, 22, ArgueUi.Gold, "argument-pressure-label");
            _surface = new VisualElement { name = "argument-surface", pickingMode = PickingMode.Ignore };
            _surface.style.width = RightW;
            _surface.style.height = RightW * PressurePhase.HalfHeight / PressurePhase.HalfWidth;
            _surface.style.marginTop = 8;
            _surface.style.backgroundColor = new Color(0.07f, 0.06f, 0.1f);
            ArgueUi.Border(_surface, new Color(0.36f, 0.3f, 0.44f), 2f);
            ArgueUi.Round(_surface, 18f);
            _surface.generateVisualContent += DrawSurface;
            _surfaceWrap.Add(_surface);
            for (int i = 0; i < _slipLabels.Length; i++)
            {
                _slipLabels[i] = ArgueUi.Text(_surface, "", ArgumentArt.BodyItalic, 18, new Color(0.78f, 0.7f, 1f));
                _slipLabels[i].style.position = Position.Absolute;
                _slipLabels[i].style.display = DisplayStyle.None;
            }

            // ---------- Bottom: the dialogue box ----------
            _dialogue = ArgueUi.CardBox(_content, "argument-dialogue", ArgueUi.Panel, ArgueUi.CardEdge);
            ArgueUi.Place(_dialogue, 20, 908, 1880, 160);
            _dialogue.style.paddingLeft = _dialogue.style.paddingRight = 28;
            _lineOpponent = ArgueUi.Text(_dialogue, "", ArgumentArt.Body, 28, ArgueUi.Ink, "argument-line-opponent");
            _lineNadia = ArgueUi.Text(_dialogue, "", ArgumentArt.Body, 23, ArgueUi.Dim, "argument-line-nadia");
            _lineLogic = ArgueUi.Text(_dialogue, "", ArgumentArt.BodyItalic, 22, ArgueUi.Gold, "argument-line-logic");
            _hint = ArgueUi.Text(_dialogue, "", ArgumentArt.Body, 16, new Color(0.5f, 0.48f, 0.56f), "argument-hint");
            _hint.style.position = Position.Absolute;
            _hint.style.right = 24;
            _hint.style.bottom = 6;
            _hint.style.unityTextAlign = TextAnchor.LowerRight;

            _placeholder = ArgueUi.Text(_content, "", ArgumentArt.BodyItalic, 16, ArgueUi.Bad, "argument-placeholder");
            ArgueUi.Place(_placeholder, Right + RightW - 520, 4, 520, 22);
            _placeholder.style.unityTextAlign = TextAnchor.UpperRight;

            // ---------- Overlays on the right ----------
            _evidence = Overlay("argument-evidence-panel");
            _evidencePrompt = ArgueUi.Text(_evidence, "", ArgumentArt.Display, 32, ArgueUi.Ink, "argument-evidence-prompt");
            _evidencePrompt.style.marginBottom = 14;
            _evidenceGrid = new VisualElement { pickingMode = PickingMode.Ignore };
            _evidenceGrid.style.flexDirection = FlexDirection.Row;
            _evidenceGrid.style.flexWrap = Wrap.Wrap;
            _evidence.Add(_evidenceGrid);

            _reasons = Overlay("argument-reasons-panel");
            _reasonHeading = ArgueUi.Text(_reasons, "", ArgumentArt.Display, 30, ArgueUi.Ink, "argument-reasons-heading");
            _reasonHeading.style.marginBottom = 10;
            _reasonList = new VisualElement { pickingMode = PickingMode.Ignore };
            _reasons.Add(_reasonList);

            _transcript = Overlay("argument-transcript-panel");
            _transcriptTitle = ArgueUi.Text(_transcript, "", ArgumentArt.Display, 30, ArgueUi.Ink, "argument-transcript-title");
            _transcriptScroll = new ScrollView(ScrollViewMode.Vertical) { name = "argument-transcript-scroll" };
            _transcriptScroll.style.height = 760;
            _transcriptScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _transcript.Add(_transcriptScroll);

            _end = ArgueUi.CardBox(_content, "argument-end", ArgueUi.Panel, ArgueUi.Gold);
            ArgueUi.Place(_end, 520, 230, 880, 0);
            _end.style.paddingLeft = _end.style.paddingRight = 40;
            _end.style.paddingTop = _end.style.paddingBottom = 30;
            _end.style.display = DisplayStyle.None;
            _endTitle = ArgueUi.Text(_end, "", ArgumentArt.Display, 64, ArgueUi.Gold, "argument-end-title");
            _endBody = ArgueUi.Text(_end, "", ArgumentArt.Body, 27, ArgueUi.Ink, "argument-end-body");
            _endBody.style.marginTop = 10;
            _endBody.style.marginBottom = 22;
            _endContinue = Button(_end, "argument-continue", "", 30);
        }

        // ---------- Items ----------

        void Hook(VisualElement e)
        {
            e.pickingMode = PickingMode.Position;
            e.RegisterCallback<PointerEnterEvent>(_ => Hovered?.Invoke(e));
            e.RegisterCallback<ClickEvent>(_ => Clicked?.Invoke(e));
            e.RegisterCallback<PointerDownEvent>(_ => PointerHeld?.Invoke(e, true));
            e.RegisterCallback<PointerUpEvent>(_ => PointerHeld?.Invoke(e, false));
            e.RegisterCallback<PointerLeaveEvent>(_ => PointerHeld?.Invoke(e, false));
        }

        VisualElement Button(VisualElement parent, string name, string text, float size)
        {
            var b = ArgueUi.CardBox(parent, name, ArgueUi.Card, ArgueUi.CardEdge);
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            ArgueUi.Text(b, text, ArgumentArt.BodyBold, size, ArgueUi.Ink);
            Hook(b);
            return b;
        }

        void MakePremiseCard(int i)
        {
            var c = ArgueUi.CardBox(_premiseGrid, "argument-premise-" + i, ArgueUi.Card, ArgueUi.CardEdge);
            c.style.width = 590;
            c.style.height = 86;
            c.style.marginRight = i % 2 == 0 ? 20 : 0;
            c.style.marginBottom = 10;
            var top = new VisualElement { pickingMode = PickingMode.Ignore };
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            c.Add(top);
            var label = ArgueUi.Text(top, "", ArgumentArt.Display, 20, ArgueUi.Gold);
            label.style.marginRight = 10;
            var kind = ArgueUi.Chip(top, "", new Color(0.24f, 0.22f, 0.32f), ArgueUi.Ink);
            var key = ArgueUi.Chip(top, "", new Color(0.55f, 0.36f, 0.12f), ArgueUi.Ink);
            var status = ArgueUi.Text(top, "", ArgumentArt.BodyBold, 15, ArgueUi.Dim);
            status.style.marginLeft = StyleKeyword.Auto;
            var text = ArgueUi.Text(c, "", ArgumentArt.Body, 21, ArgueUi.Ink);
            text.style.marginTop = 2;
            text.style.overflow = Overflow.Hidden;
            c.style.display = DisplayStyle.None;
            Hook(c);
            _cards.Add(c);
            _cardParts.Add(new[] { label, kind, key, status, text });
        }

        void MakeVerb(string id)
        {
            var b = ArgueUi.CardBox(_verbRow, "argument-verb-" + id, ArgueUi.Card, ArgueUi.CardEdge);
            b.style.width = 188;
            b.style.height = 54;
            b.style.marginRight = 14;
            b.style.paddingTop = b.style.paddingBottom = 4;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            var l = ArgueUi.Text(b, "", ArgumentArt.BodyBold, 19, ArgueUi.Ink);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            Hook(b);
            _verbs[id] = b;
            _verbLabels[id] = l;
        }

        VisualElement Overlay(string name)
        {
            var o = ArgueUi.CardBox(_content, name, new Color(0.05f, 0.045f, 0.07f, 0.985f), ArgueUi.CardEdge);
            ArgueUi.Place(o, Right, 24, RightW, 870);
            o.style.paddingLeft = o.style.paddingRight = 28;
            o.style.paddingTop = 22;
            o.style.display = DisplayStyle.None;
            return o;
        }

        // ---------- What the host reads ----------

        public VisualElement ClaimCard => _claimCard;
        public VisualElement PremiseCard(int i) => i >= 0 && i < _cards.Count ? _cards[i] : null;
        public VisualElement Verb(string id) => _verbs.TryGetValue(id, out var v) ? v : null;
        public VisualElement EvidenceCard(int i) => i >= 0 && i < _evidenceCards.Count ? _evidenceCards[i] : null;
        public VisualElement ReasonCard(int i) => i >= 0 && i < _reasonCards.Count ? _reasonCards[i] : null;
        public VisualElement ContinueButton => _endContinue;
        public string ClaimText => _claimText.text;
        public string FocusText => _focusText.text;
        public string OpponentLine => _lineOpponent.text;
        public string LogicLine => _lineLogic.text;
        public bool EndShown => _end.style.display == DisplayStyle.Flex;
        public string EndTitle => _endTitle.text;
        public bool IsShown => Root.panel != null;
        public string SpriteName => _shownSprite;
        public bool BoxShown => _surfaceWrap.style.display == DisplayStyle.Flex;
        public bool TranscriptShown => _transcript.style.display == DisplayStyle.Flex;

        // ---------- What the host writes ----------

        public void SetOpponent(string name, string expression, string expressionWord, string placeholderNote)
        {
            _name.text = name;
            _expression.text = expressionWord;
            string key = (name ?? "") + "_" + (expression ?? "");
            if (key == _shownSprite) return;
            _shownSprite = key;
            _spriteExpression = expression ?? "";
            var tex = ArgumentArt.Sprite(name, expression, out bool placeholder);
            _spriteTexture = tex;
            ShowSprite(_boil.Tick(0f, tex, SpriteShownPx(), ArgumentBoil.For(_spriteExpression)));
            _spriteNote.text = placeholder ? placeholderNote : "";
        }

        // ---------- The boil ----------

        /// <summary>What the sprite slot shows: the boiled render texture, or the sprite itself (tests).</summary>
        public Texture SpriteShown => _spriteShown;
        public string BoilLabel => _boilLabel.style.display == DisplayStyle.Flex ? _boilLabel.text : "";

        /// <summary>Shows the boil's preset and values under her for a few seconds (F10, Shift+F10), or for good (captures).</summary>
        public void ShowBoilLabel(bool always = false)
        {
            if (always) _boilLabelAlways = true;
            _boilLabelFor = 4f;
            var s = ArgumentBoil.For(_spriteExpression);
            _boilLabel.text = "Boil (" + ArgumentBoil.CycleKeyName + "): " + s.Describe()
                + (ArgumentBoil.Reacting ? "  ·  reacting to her mood (" + ArgumentBoil.ReactKeyName + ")" : "  ·  " + ArgumentBoil.ReactKeyName + ": react to her mood");
            _boilLabel.style.display = DisplayStyle.Flex;
        }

        void ShowSprite(Texture t)
        {
            if (ReferenceEquals(t, _spriteShown))
            {
                if (t is RenderTexture) _sprite.MarkDirtyRepaint();
                return;
            }
            _spriteShown = t;
            if (t is RenderTexture rt) _sprite.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else _sprite.style.backgroundImage = new StyleBackground(t as Texture2D);
        }

        /// <summary>How big the sprite stands on screen (1080p pixels: the panel's reference size).</summary>
        Vector2 SpriteShownPx()
        {
            if (_spriteTexture == null) return Vector2.zero;
            var r = _sprite.contentRect;
            var slot = float.IsNaN(r.width) || r.width <= 0f ? new Vector2(640f, 860f) : new Vector2(r.width, r.height);
            return ArgumentBoil.Shown(new Vector2(_spriteTexture.width, _spriteTexture.height), slot);
        }

        public void SetPlaceholderBadge(string text) => _placeholder.text = text ?? "";

        public void SetVitals(string heart, bool racing, string heat, string anger)
        {
            _heart.text = heart;
            _heart.style.color = racing ? ArgueUi.Bad : ArgueUi.Ink;
            _heat.text = heat;
            _anger.text = anger;
        }

        public void SetClaim(string label, string text, string keys)
        {
            _claimLabel.text = label;
            _claimText.text = text;
            _claimKeys.text = keys;
        }

        /// <summary>Shows premise card <paramref name="i"/> (null label hides it). Status changes the card's look:
        /// broken cards are struck and dimmed, conceded ones edged green, forced ones red, hidden ones dark with "?".</summary>
        public void SetPremise(int i, string label, string kind, string key, string status, string text, PremiseStatus state)
        {
            if (i < 0 || i >= _cards.Count) return;
            var c = _cards[i];
            if (label == null) { c.style.display = DisplayStyle.None; return; }
            c.style.display = DisplayStyle.Flex;
            var parts = _cardParts[i];
            parts[0].text = label;
            parts[1].text = kind;
            parts[1].style.display = string.IsNullOrEmpty(kind) ? DisplayStyle.None : DisplayStyle.Flex;
            parts[2].text = key;
            parts[2].style.display = string.IsNullOrEmpty(key) ? DisplayStyle.None : DisplayStyle.Flex;
            parts[3].text = status;
            parts[4].text = state == PremiseStatus.Broken ? "<s>" + text + "</s>" : text;
            Color fill = ArgueUi.Card, edge = ArgueUi.CardEdge, ink = ArgueUi.Ink;
            switch (state)
            {
                case PremiseStatus.Broken: fill = new Color(0.08f, 0.07f, 0.1f); ink = new Color(0.55f, 0.53f, 0.6f); break;
                case PremiseStatus.Conceded: edge = new Color(0.35f, 0.6f, 0.42f); break;
                case PremiseStatus.ConcededFalse: edge = new Color(0.5f, 0.48f, 0.4f); ink = ArgueUi.Dim; break;
                case PremiseStatus.Forced: edge = new Color(0.75f, 0.3f, 0.28f); break;
                case PremiseStatus.Hidden: fill = new Color(0.06f, 0.055f, 0.08f); edge = new Color(0.3f, 0.28f, 0.36f); ink = ArgueUi.Dim; break;
            }
            c.style.backgroundColor = fill;
            c.userData = edge;
            if (!ArgueUi.IsFocused(c)) ArgueUi.Border(c, edge, 2f);
            parts[4].style.color = ink;
            parts[3].style.color = state == PremiseStatus.Broken || state == PremiseStatus.Forced ? ArgueUi.Bad : state == PremiseStatus.Conceded ? ArgueUi.Good : ArgueUi.Dim;
        }

        public void SetFocusPanel(string prompt, string header, string text, string saidHeader, string said)
        {
            _prompt.text = prompt ?? "";
            _focusHeader.text = header ?? "";
            _focusText.text = text ?? "";
            _focusSaidHeader.text = said != null && said.Length > 0 ? saidHeader : "";
            _focusSaid.text = said ?? "";
        }

        public void SetVerb(string id, string label)
        {
            if (_verbLabels.TryGetValue(id, out var l)) l.text = label;
        }

        public void SetPressProgress(string verb, float t)
        {
            if (_verbs.TryGetValue(verb, out var b)) b.style.backgroundColor = t > 0f ? Color.Lerp(ArgueUi.Card, new Color(0.55f, 0.16f, 0.16f), t) : ArgueUi.Card;
        }

        public void SetDialogue(string opponentLine, string nadiaLine, string logicLine)
        {
            _lineOpponent.text = opponentLine ?? "";
            _lineNadia.text = nadiaLine ?? "";
            _lineLogic.text = logicLine ?? "";
        }

        public void SetHint(string text) => _hint.text = text ?? "";

        // ---------- Overlays ----------

        public void ShowEvidence(string prompt, IList<string> names, IList<string> notes)
        {
            _evidencePrompt.text = prompt;
            while (_evidenceCards.Count < names.Count)
            {
                var c = ArgueUi.CardBox(_evidenceGrid, "argument-evidence-" + _evidenceCards.Count, ArgueUi.Card, ArgueUi.CardEdge);
                c.style.width = 360;
                c.style.minHeight = 120;
                c.style.marginRight = 18;
                c.style.marginBottom = 16;
                ArgueUi.Text(c, "", ArgumentArt.Display, 25, ArgueUi.Ink);
                ArgueUi.Text(c, "", ArgumentArt.BodyItalic, 19, ArgueUi.Dim).style.marginTop = 4;
                Hook(c);
                _evidenceCards.Add(c);
            }
            for (int i = 0; i < _evidenceCards.Count; i++)
            {
                var c = _evidenceCards[i];
                bool used = i < names.Count;
                c.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;
                ((Label)c[0]).text = names[i];
                ((Label)c[1]).text = notes[i];
            }
            _evidence.style.display = DisplayStyle.Flex;
        }

        public void HideEvidence() => _evidence.style.display = DisplayStyle.None;

        public void ShowReasons(string heading, IList<string> texts)
        {
            _reasonHeading.text = heading;
            while (_reasonCards.Count < texts.Count)
            {
                var c = ArgueUi.CardBox(_reasonList, "argument-reason-" + _reasonCards.Count, ArgueUi.Card, ArgueUi.CardEdge);
                c.style.minHeight = 96;
                c.style.marginBottom = 14;
                c.style.justifyContent = Justify.Center;
                ArgueUi.Text(c, "", ArgumentArt.Body, 27, ArgueUi.Ink);
                Hook(c);
                _reasonCards.Add(c);
            }
            for (int i = 0; i < _reasonCards.Count; i++)
            {
                var c = _reasonCards[i];
                bool used = i < texts.Count;
                c.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (used) ((Label)c[0]).text = texts[i];
            }
            _reasons.style.display = DisplayStyle.Flex;
        }

        public void HideReasons() => _reasons.style.display = DisplayStyle.None;

        public void ShowTranscript(string title, IReadOnlyList<Exchange> transcript)
        {
            _transcriptTitle.text = title;
            _transcriptScroll.Clear();
            foreach (var x in transcript)
            {
                Label l;
                switch (x.speaker)
                {
                    case Speaker.Logic: l = ArgueUi.Text(null, x.text, ArgumentArt.BodyItalic, 21, ArgueUi.Gold); break;
                    case Speaker.Nadia: l = ArgueUi.Text(null, "<b>Nadia:</b> " + x.text, ArgumentArt.Body, 23, ArgueUi.Ink); break;
                    case Speaker.Supporter: l = ArgueUi.Text(null, "<b>" + x.who + ":</b> " + x.text, ArgumentArt.Body, 23, ArgueUi.Good); break;
                    default: l = ArgueUi.Text(null, "<b>" + x.who + ":</b> " + x.text, ArgumentArt.Body, 23, ArgueUi.Blue); break;
                }
                l.style.marginBottom = 10;
                _transcriptScroll.Add(l);
            }
            _transcript.style.display = DisplayStyle.Flex;
            _transcriptScroll.schedule.Execute(() => _transcriptScroll.scrollOffset = new Vector2(0f, float.MaxValue));
        }

        public void ScrollTranscript(int dir) => _transcriptScroll.scrollOffset += new Vector2(0f, dir * 120f);
        public void HideTranscript() => _transcript.style.display = DisplayStyle.None;

        public void ShowEnd(string title, string body, string continueLabel)
        {
            _endTitle.text = title;
            _endBody.text = body;
            ((Label)_endContinue[0]).text = continueLabel;
            _end.style.display = DisplayStyle.Flex;
            Tween(0.5f, t => { _end.style.opacity = t; _end.style.translate = new Translate(0, (1f - t) * 30f); }, null);
        }

        public void HideOverlays()
        {
            HideEvidence();
            HideReasons();
            HideTranscript();
        }

        // ---------- Transitions (timed, never a cut) ----------

        struct Anim
        {
            public float time, duration;
            public Action<float> apply;
            public Action done;
        }

        readonly List<Anim> _anims = new List<Anim>(8);

        /// <summary>A transition is playing (the host waits for it before taking input).</summary>
        public bool Busy => _anims.Count > 0;

        void Tween(float duration, Action<float> apply, Action done)
        {
            apply(0f);
            _anims.Add(new Anim { duration = Mathf.Max(0.01f, duration), apply = apply, done = done });
        }

        /// <summary>Advances the transitions (unscaled time: they finish even if the game is slowed) and the boil.</summary>
        public void Tick(float dt)
        {
            if (_spriteTexture != null) ShowSprite(_boil.Tick(dt, _spriteTexture, SpriteShownPx(), ArgumentBoil.For(_spriteExpression)));
            if (_boilLabelFor > 0f && !_boilLabelAlways)
            {
                _boilLabelFor -= dt;
                if (_boilLabelFor <= 0f) _boilLabel.style.display = DisplayStyle.None;
            }
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

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>In from the world: the black fades up over the scene, then the opponent slides in and the claims settle.</summary>
        public void PlayIntro(Action done)
        {
            Tween(0.45f, t => _black.style.opacity = t, () =>
            {
                Tween(0.6f, t =>
                {
                    _content.style.opacity = t;
                    _sprite.style.translate = new Translate((1f - t) * -140f, 0);
                    _claims.style.translate = new Translate(0, (1f - t) * -24f);
                }, done);
            });
        }

        /// <summary>Out to the world: everything fades back to black, then the black fades away; then the view removes
        /// itself. Runs on the panel's own scheduler (the host has already gone).</summary>
        public void PlayOutro()
        {
            _anims.Clear();
            Tween(0.35f, t => _content.style.opacity = 1f - t, () =>
                Tween(0.5f, t => _black.style.opacity = 1f - t, Remove));
            float last = Time.realtimeSinceStartup;
            Root.schedule.Execute(() =>
            {
                float now = Time.realtimeSinceStartup;
                Tick(Mathf.Min(0.1f, now - last));
                last = now;
            }).Every(16).Until(() => Root.panel == null || !Busy);
        }

        /// <summary>The claims fold away and the box opens (about half a second).</summary>
        public void OpenBox(Action done)
        {
            HideOverlays();
            Tween(0.25f, t =>
            {
                _claims.style.scale = new Scale(new Vector3(1f, 1f - t, 1f));
                _claims.style.opacity = 1f - t;
            }, () =>
            {
                _claims.style.display = DisplayStyle.None;
                _surfaceWrap.style.display = DisplayStyle.Flex;
                Tween(0.28f, t =>
                {
                    _surfaceWrap.style.scale = new Scale(new Vector3(0.3f + 0.7f * t, 0.3f + 0.7f * t, 1f));
                    _surfaceWrap.style.opacity = t;
                }, done);
            });
        }

        /// <summary>The box collapses and the claims fold back in (about half a second).</summary>
        public void CloseBox(Action done)
        {
            Tween(0.25f, t =>
            {
                _surfaceWrap.style.scale = new Scale(new Vector3(1f - 0.7f * t, 1f - 0.7f * t, 1f));
                _surfaceWrap.style.opacity = 1f - t;
            }, () =>
            {
                _surfaceWrap.style.display = DisplayStyle.None;
                _claims.style.display = DisplayStyle.Flex;
                Tween(0.28f, t =>
                {
                    _claims.style.scale = new Scale(new Vector3(1f, t, 1f));
                    _claims.style.opacity = t;
                }, done);
            });
        }

        public void Remove()
        {
            Root.RemoveFromHierarchy();
            _boil.Dispose();
        }

        // ---------- The surface (pressure phases) ----------

        public void SetPressureLabel(string text) => _pressureLabel.text = text ?? "";

        public void SetPhase(PressurePhase phase)
        {
            _phase = phase;
            _surface.MarkDirtyRepaint();
            for (int i = 0; i < _slipLabels.Length; i++)
            {
                var l = _slipLabels[i];
                var slips = phase != null ? phase.Slips : null;
                bool on = slips != null && i < slips.Count && slips[i].active;
                l.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) continue;
                var p = ToPixels(slips[i].position);
                if (l.text != slips[i].name) l.text = slips[i].name;
                l.style.left = p.x + 14f;
                l.style.top = p.y - 12f;
            }
        }

        Vector2 ToPixels(System.Numerics.Vector2 u)
        {
            var r = _surface.contentRect;
            float w = float.IsNaN(r.width) || r.width <= 0f ? RightW : r.width;
            float h = float.IsNaN(r.height) || r.height <= 0f ? RightW * PressurePhase.HalfHeight / PressurePhase.HalfWidth : r.height;
            float sx = w / (2f * PressurePhase.HalfWidth), sy = h / (2f * PressurePhase.HalfHeight);
            return new Vector2(w * 0.5f + u.X * sx, h * 0.5f - u.Y * sy);
        }

        float Scale
        {
            get
            {
                var r = _surface.contentRect;
                float w = float.IsNaN(r.width) || r.width <= 0f ? RightW : r.width;
                return w / (2f * PressurePhase.HalfWidth);
            }
        }

        void DrawSurface(MeshGenerationContext mgc)
        {
            var phase = _phase;
            if (phase == null) return;
            var p = mgc.painter2D;
            float s = Scale;
            DrawWarnings(p, phase, s);
            // Plates (walls): a column of plates with its gaps.
            p.fillColor = new Color(0.86f, 0.88f, 0.94f);
            var walls = phase.Walls;
            float step = PressurePhase.PlateRadius * 2.05f;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (!w.active || w.warn > 0f) continue;
                for (float y = -PressurePhase.HalfHeight + PressurePhase.PlateRadius; y <= PressurePhase.HalfHeight; y += step)
                {
                    if (phase.WallGapAt(w, y) || phase.WallGapAt(w, y + PressurePhase.PlateRadius) || phase.WallGapAt(w, y - PressurePhase.PlateRadius)) continue;
                    Disc(p, ToPixels(new System.Numerics.Vector2(w.x, y)), PressurePhase.PlateRadius * s);
                }
            }
            // Flour rings: arcs with their gaps.
            p.strokeColor = new Color(0.96f, 0.92f, 0.84f, 0.9f);
            p.lineWidth = PressurePhase.RingThickness * s;
            var rings = phase.Rings;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (!r.active || r.radius <= 0.01f) continue;
                var c = ToPixels(r.center);
                float rad = r.radius * s;
                if (r.gapCount > 1)
                {
                    Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle1 - r.gapHalfAngle);
                    Arc(p, c, rad, r.gapAngle1 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
                }
                else Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
            }
            // Batter.
            p.fillColor = new Color(0.95f, 0.76f, 0.38f);
            var blobs = phase.Blobs;
            for (int i = 0; i < blobs.Count; i++)
                if (blobs[i].active && blobs[i].warn <= 0f) Disc(p, ToPixels(blobs[i].position), PressurePhase.BlobRadius * s);
            // Slips: paper planes (diamonds).
            p.fillColor = new Color(0.72f, 0.6f, 1f);
            var slips = phase.Slips;
            for (int i = 0; i < slips.Count; i++)
            {
                if (!slips[i].active) continue;
                var c = ToPixels(slips[i].position);
                float r = PressurePhase.SlipRadius * s;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x, c.y - r));
                p.LineTo(new Vector2(c.x + r, c.y));
                p.LineTo(new Vector2(c.x, c.y + r));
                p.LineTo(new Vector2(c.x - r, c.y));
                p.ClosePath();
                p.Fill();
            }
            // Nadia's presence: a small light (blinking while she can't be hit).
            bool blink = phase.Invulnerable > 0f && ((int)(phase.Invulnerable * 12f) & 1) == 0;
            if (!blink)
            {
                var c = ToPixels(phase.Light);
                p.fillColor = new Color(1f, 0.9f, 0.55f, 0.3f);
                Disc(p, c, PressurePhase.LightRadius * s * 2.4f);
                p.fillColor = new Color(1f, 0.86f, 0.36f);
                Disc(p, c, PressurePhase.LightRadius * s);
            }
        }

        // ---------- Warnings (the standing rule, PressureRule): every attack shows where it will be before it can hurt ----------

        /// <summary>Plates wait as a ghost column at the edge they'll come in from, with their gaps; a ring shows its
        /// centre, a faint outline with its gaps and the lanes out through them; batter shows its path across the box.
        /// They grow stronger (and pulse faster) as the warning runs out.</summary>
        void DrawWarnings(Painter2D p, PressurePhase phase, float s)
        {
            float tele = Mathf.Max(0.05f, phase.Spec.telegraph);
            var walls = phase.Walls;
            float step = PressurePhase.PlateRadius * 2.05f;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (!w.active || w.warn <= 0f) continue;
                p.fillColor = new Color(0.86f, 0.88f, 0.94f, WarnAlpha(w.warn, tele));
                float x = Mathf.Sign(w.x) * (PressurePhase.HalfWidth - PressurePhase.PlateRadius);
                for (float y = -PressurePhase.HalfHeight + PressurePhase.PlateRadius; y <= PressurePhase.HalfHeight; y += step)
                {
                    if (phase.WallGapAt(w, y) || phase.WallGapAt(w, y + PressurePhase.PlateRadius) || phase.WallGapAt(w, y - PressurePhase.PlateRadius)) continue;
                    Disc(p, ToPixels(new System.Numerics.Vector2(x, y)), PressurePhase.PlateRadius * s * 0.8f);
                }
            }
            var rings = phase.Rings;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (!r.active || r.warn <= 0f) continue;
                float a = WarnAlpha(r.warn, tele);
                var c = ToPixels(r.center);
                p.fillColor = new Color(0.96f, 0.92f, 0.84f, a);
                Disc(p, c, 0.05f * s);
                p.strokeColor = new Color(0.96f, 0.92f, 0.84f, a);
                p.lineWidth = 2f;
                float rad = 0.45f * s;
                if (r.gapCount > 1)
                {
                    Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle1 - r.gapHalfAngle);
                    Arc(p, c, rad, r.gapAngle1 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
                }
                else Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
                // The lanes out: the way through each gap, to the edge of the box.
                p.strokeColor = new Color(1f, 0.86f, 0.36f, a * 0.55f);
                p.lineWidth = 1.5f;
                Lane(p, r.center, r.gapAngle0);
                if (r.gapCount > 1) Lane(p, r.center, r.gapAngle1);
            }
            var blobs = phase.Blobs;
            for (int i = 0; i < blobs.Count; i++)
            {
                var b = blobs[i];
                if (!b.active || b.warn <= 0f) continue;
                float a = WarnAlpha(b.warn, tele);
                var from = b.position;
                var to = b.position + b.velocity * 10f;
                if (!ClipToBox(ref from, ref to)) continue;
                p.strokeColor = new Color(0.95f, 0.76f, 0.38f, a * 0.8f);
                p.lineWidth = PressurePhase.BlobRadius * s * 0.6f;
                p.BeginPath();
                p.MoveTo(ToPixels(from));
                p.LineTo(ToPixels(to));
                p.Stroke();
                p.fillColor = new Color(0.95f, 0.76f, 0.38f, a);
                Disc(p, ToPixels(from), PressurePhase.BlobRadius * s);
            }
        }

        /// <summary>Faint at first, strong and quick-pulsing as the warning runs out.</summary>
        static float WarnAlpha(float warn, float telegraph)
        {
            float left = Mathf.Clamp01(warn / telegraph);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(26f, 10f, left));
            return Mathf.Lerp(0.75f, 0.25f, left) + 0.15f * pulse;
        }

        void Lane(Painter2D p, System.Numerics.Vector2 centre, float angle)
        {
            var dir = new System.Numerics.Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var a = centre + dir * 0.55f;
            var b = centre + dir * 8f;
            if (!ClipToBox(ref a, ref b)) return;
            p.BeginPath();
            p.MoveTo(ToPixels(a));
            p.LineTo(ToPixels(b));
            p.Stroke();
        }

        /// <summary>Clips a segment (surface units) to the box; false if none of it is inside.</summary>
        static bool ClipToBox(ref System.Numerics.Vector2 a, ref System.Numerics.Vector2 b)
        {
            float t0 = 0f, t1 = 1f;
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (!Clip(-dx, a.X + PressurePhase.HalfWidth, ref t0, ref t1)) return false;
            if (!Clip(dx, PressurePhase.HalfWidth - a.X, ref t0, ref t1)) return false;
            if (!Clip(-dy, a.Y + PressurePhase.HalfHeight, ref t0, ref t1)) return false;
            if (!Clip(dy, PressurePhase.HalfHeight - a.Y, ref t0, ref t1)) return false;
            var start = a;
            a = new System.Numerics.Vector2(start.X + dx * t0, start.Y + dy * t0);
            b = new System.Numerics.Vector2(start.X + dx * t1, start.Y + dy * t1);
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

        public void Repaint() => _surface.MarkDirtyRepaint();
    }
}
