using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 2's screen (UI Toolkit, on the shell's HUD): the subtitles (one line at a time, each fading in, the speaker's
    /// name in their colour, Nadia's thoughts in italics), Theo's calls up in the corner, the memory counter with a pip
    /// for each memory and the three catches, Theo's eye (how close he is to seeing her), the prompt, the three-way choice,
    /// the memory up close, the chapter titles, the white and black fades, and the card when Mags pulls her out.
    /// Text wraps inside its box (never clipped). No allocation per frame beyond what UI Toolkit does with text.
    /// </summary>
    public sealed class HouseHud
    {
        readonly VisualElement _root, _overlayRoot;
        readonly VisualElement _subBox, _counterBox, _pips, _catches, _eyeBox, _eyeFill, _vignette, _vigImage, _choice, _overlay, _card;
        readonly Label _speaker, _line, _theoCall, _counter, _eyeWord, _prompt, _title, _vigText, _cardText, _cardCosts;
        readonly List<VisualElement> _pipList = new List<VisualElement>(), _catchList = new List<VisualElement>();
        readonly Label[] _options = new Label[3];
        float _lineAlpha, _lineTarget, _theoLeft, _titleLeft, _vigLeft, _overlayAlpha, _overlayTarget, _overlaySpeed = 3f;

        static readonly Color Ink = new Color(0.98f, 0.96f, 0.92f), Panel = new Color(0.08f, 0.06f, 0.1f, 0.62f), Gold = new Color(1f, 0.82f, 0.45f);

        public static Color SpeakerColour(string speaker)
        {
            switch (speaker)
            {
                case DreamHouseLines.Lena: return new Color(1f, 0.86f, 0.42f);
                case DreamHouseLines.Theo: return new Color(1f, 0.74f, 0.32f);
                case DreamHouseLines.Mags: return new Color(0.62f, 0.86f, 0.95f);
                case DreamHouseLines.Nadia: return new Color(0.85f, 0.9f, 0.7f);
                default: return new Color(0.92f, 0.86f, 0.98f);
            }
        }

        public HouseHud(VisualElement hudRoot, VisualElement overlayRoot)
        {
            _overlayRoot = overlayRoot;
            _root = new VisualElement { name = "house-hud", pickingMode = PickingMode.Ignore };
            Fill(_root);
            hudRoot.Add(_root);

            // Subtitles, bottom centre.
            _subBox = Box(_root, "house-subtitle");
            _subBox.style.left = Length.Percent(18); _subBox.style.right = Length.Percent(18); _subBox.style.bottom = 54;
            _subBox.style.flexDirection = FlexDirection.Column;
            _subBox.style.alignItems = Align.Center;
            _speaker = Text(_subBox, 17, Gold);
            _speaker.style.unityFontStyleAndWeight = FontStyle.Bold;
            _line = Text(_subBox, 23, Ink);
            _subBox.style.opacity = 0f;

            // Theo's calls, top left.
            _theoCall = Text(_root, 19, SpeakerColour(DreamHouseLines.Theo));
            _theoCall.style.position = Position.Absolute;
            _theoCall.style.left = 36; _theoCall.style.top = 120; _theoCall.style.maxWidth = 520;
            _theoCall.style.backgroundColor = Panel;
            Pad(_theoCall, 10, 6);
            _theoCall.style.opacity = 0f;

            // The counter, top centre: "Memories 3 of 8", the pips, the catches.
            _counterBox = Box(_root, "house-counter");
            _counterBox.style.top = 22; _counterBox.style.left = Length.Percent(35); _counterBox.style.right = Length.Percent(35);
            _counterBox.style.alignItems = Align.Center;
            _counter = Text(_counterBox, 19, Ink);
            _pips = Row(_counterBox);
            _catches = Row(_counterBox);
            _counterBox.style.display = DisplayStyle.None;

            // Theo's eye, top right: a bar and a word.
            _eyeBox = Box(_root, "house-eye");
            _eyeBox.style.top = 22; _eyeBox.style.right = 36; _eyeBox.style.width = 240;
            _eyeWord = Text(_eyeBox, 17, Ink);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.height = 10; bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
            Round(bar, 5);
            _eyeBox.Add(bar);
            _eyeFill = new VisualElement { pickingMode = PickingMode.Ignore };
            // Full width, scaled along x from its left edge (a scale is a transform: no layout pass per frame).
            _eyeFill.style.height = 10; _eyeFill.style.width = Length.Percent(100); _eyeFill.style.backgroundColor = Gold;
            _eyeFill.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
            _eyeFill.style.scale = new Scale(new Vector3(0f, 1f, 1f));
            Round(_eyeFill, 5);
            bar.Add(_eyeFill);
            _eyeBox.style.display = DisplayStyle.None;

            // The prompt and the choice, above the subtitles.
            _prompt = Text(_root, 20, Gold);
            _prompt.style.position = Position.Absolute;
            _prompt.style.left = Length.Percent(25); _prompt.style.right = Length.Percent(25); _prompt.style.bottom = 150;
            _prompt.style.unityTextAlign = TextAnchor.MiddleCenter;
            _prompt.style.display = DisplayStyle.None;
            _choice = Box(_root, "house-choice");
            _choice.style.left = Length.Percent(28); _choice.style.right = Length.Percent(28); _choice.style.bottom = 160;
            for (int i = 0; i < 3; i++) { _options[i] = Text(_choice, 21, Ink); _options[i].style.marginTop = 4; }
            _choice.style.display = DisplayStyle.None;

            // The title, centre.
            _title = Text(_root, 40, Ink);
            _title.style.position = Position.Absolute;
            _title.style.left = 0; _title.style.right = 0; _title.style.top = Length.Percent(18);
            _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _title.style.unityFontStyleAndWeight = FontStyle.Italic;
            _title.style.opacity = 0f;

            // The memory up close.
            _vignette = Box(_root, "house-memory");
            _vignette.style.left = Length.Percent(30); _vignette.style.right = Length.Percent(30); _vignette.style.top = Length.Percent(12);
            _vignette.style.alignItems = Align.Center;
            _vigImage = new VisualElement { pickingMode = PickingMode.Ignore };
            _vigImage.style.width = Length.Percent(100); _vigImage.style.height = 300;
            _vigImage.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _vignette.Add(_vigImage);
            _vigText = Text(_vignette, 19, Ink);
            _vigText.style.unityFontStyleAndWeight = FontStyle.Italic;
            _vignette.style.opacity = 0f;

            // The fades and the card (over everything).
            _overlay = new VisualElement { name = "house-overlay", pickingMode = PickingMode.Ignore };
            Fill(_overlay);
            _overlay.style.backgroundColor = Color.black;
            _overlay.style.opacity = 0f;
            _overlayRoot.Add(_overlay);
            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.style.position = Position.Absolute;
            _card.style.left = Length.Percent(20); _card.style.right = Length.Percent(20); _card.style.top = Length.Percent(30);
            _card.style.alignItems = Align.Center;
            _cardText = Text(_card, 24, Ink);
            _cardCosts = Text(_card, 18, new Color(0.95f, 0.55f, 0.5f));
            _cardCosts.style.marginTop = 26;
            _card.style.display = DisplayStyle.None;
            _overlay.Add(_card);
        }

        public void Remove()
        {
            _root.RemoveFromHierarchy();
            _overlay.RemoveFromHierarchy();
        }

        public void Show(bool on) => _root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        // ---------- Lines ----------

        /// <summary>One line on the subtitle (fades in; <see cref="ClearLine"/> fades it out).</summary>
        public void Line(HouseLine l)
        {
            bool thought = string.IsNullOrEmpty(l.speaker);
            _speaker.text = thought ? "" : l.speaker;
            _speaker.style.display = thought ? DisplayStyle.None : DisplayStyle.Flex;
            _speaker.style.color = SpeakerColour(l.speaker);
            _line.text = l.text;
            _line.style.unityFontStyleAndWeight = thought ? FontStyle.Italic : FontStyle.Normal;
            _lineAlpha = 0f;
            _lineTarget = 1f;
        }

        public void ClearLine() => _lineTarget = 0f;
        public string LineText => _lineTarget > 0f ? _line.text : "";

        /// <summary>Theo calling out (up in the corner, for a few seconds; doesn't interrupt the subtitles).</summary>
        public void TheoCalls(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _theoCall.text = "Theo: " + text;
            _theoLeft = 2.6f;
        }

        public string TheoCall => _theoLeft > 0f ? _theoCall.text : "";

        // ---------- Counter and eye ----------

        public void ShowCounter(bool on)
        {
            _counterBox.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _eyeBox.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Counter(int found, int total, int catches, int maxCatches)
        {
            _counter.text = "Memories " + found + " of " + total;
            Pips(_pips, _pipList, total, found, Gold, 12);
            Pips(_catches, _catchList, maxCatches, catches, new Color(0.95f, 0.35f, 0.3f), 9);
        }

        public string CounterText => _counter.text;

        static void Pips(VisualElement row, List<VisualElement> list, int n, int lit, Color on, int size)
        {
            while (list.Count < n)
            {
                var p = new VisualElement { pickingMode = PickingMode.Ignore };
                p.style.width = size; p.style.height = size; p.style.marginLeft = 3; p.style.marginRight = 3; p.style.marginTop = 4;
                Round(p, size * 0.5f);
                p.style.borderTopWidth = 1; p.style.borderBottomWidth = 1; p.style.borderLeftWidth = 1; p.style.borderRightWidth = 1;
                var b = new Color(1f, 1f, 1f, 0.5f);
                p.style.borderTopColor = b; p.style.borderBottomColor = b; p.style.borderLeftColor = b; p.style.borderRightColor = b;
                row.Add(p);
                list.Add(p);
            }
            for (int i = 0; i < list.Count; i++)
            {
                list[i].style.display = i < n ? DisplayStyle.Flex : DisplayStyle.None;
                list[i].style.backgroundColor = i < lit ? on : new Color(0f, 0f, 0f, 0.25f);
            }
        }

        string _eyeWordShown;
        int _eyeShown = -1;

        /// <summary>Theo's eye: how close he is to seeing her (0..1) and what he's doing (pass the same string instance
        /// while it doesn't change: the label is only written when it does).</summary>
        public void Eye(float meter, string word)
        {
            if (!ReferenceEquals(word, _eyeWordShown)) { _eyeWord.text = word; _eyeWordShown = word; }
            // Only when it moves visibly (a 1/200 step): no style writes on a still frame.
            int q = Mathf.RoundToInt(Mathf.Clamp01(meter) * 200f);
            if (q == _eyeShown) return;
            _eyeShown = q;
            float m = q / 200f;
            _eyeFill.style.scale = new Scale(new Vector3(m, 1f, 1f));
            _eyeFill.style.backgroundColor = Color.Lerp(Gold, new Color(0.95f, 0.25f, 0.2f), Mathf.InverseLerp(0.5f, 1f, m));
        }

        // ---------- Prompt, choice ----------

        public void Prompt(string text)
        {
            _prompt.text = text ?? "";
            _prompt.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public string PromptText => _prompt.style.display == DisplayStyle.Flex ? _prompt.text : "";

        public void Choice(string a, string b, string c)
        {
            _options[0].text = a; _options[1].text = b; _options[2].text = c;
            _choice.style.display = DisplayStyle.Flex;
        }

        public void HideChoice() => _choice.style.display = DisplayStyle.None;
        public bool ChoiceShown => _choice.style.display == DisplayStyle.Flex;

        // ---------- Title, memory, fades, card ----------

        public void Title(string text, float seconds = 2.2f)
        {
            _title.text = text;
            _titleLeft = seconds;
        }

        public void Memory(Texture2D picture, string thought, float seconds = 3.2f)
        {
            _vigImage.style.backgroundImage = picture != null ? new StyleBackground(picture) : new StyleBackground(StyleKeyword.None);
            _vigImage.style.display = picture != null ? DisplayStyle.Flex : DisplayStyle.None;
            _vigText.text = thought ?? "";
            _vigLeft = seconds;
        }

        /// <summary>Fades the screen to <paramref name="colour"/> at <paramref name="alpha"/> (0 clears) at a speed (per s).</summary>
        public void Fade(Color colour, float alpha, float speed = 3f)
        {
            _overlay.style.backgroundColor = colour;
            _overlayTarget = Mathf.Clamp01(alpha);
            _overlaySpeed = speed;
            if (speed <= 0f) _overlayAlpha = _overlayTarget;
        }

        public float FadeAlpha => _overlayAlpha;

        public void Card(string text, string costs)
        {
            _cardText.text = text ?? "";
            _cardCosts.text = costs ?? "";
            _card.style.display = string.IsNullOrEmpty(text) && string.IsNullOrEmpty(costs) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void Tick(float unscaledDt)
        {
            float dt = unscaledDt;
            _lineAlpha = Mathf.MoveTowards(_lineAlpha, _lineTarget, dt * 4f);
            _subBox.style.opacity = _lineAlpha;
            _theoLeft -= dt;
            _theoCall.style.opacity = Mathf.Clamp01(_theoLeft * 2f);
            _titleLeft -= dt;
            _title.style.opacity = Mathf.Clamp01(Mathf.Min(_titleLeft * 1.5f, 1f));
            _vigLeft -= dt;
            _vignette.style.opacity = Mathf.Clamp01(_vigLeft * 2f);
            _overlayAlpha = Mathf.MoveTowards(_overlayAlpha, _overlayTarget, dt * _overlaySpeed);
            _overlay.style.opacity = _overlayAlpha;
            _overlay.style.display = _overlayAlpha > 0.001f || _overlayTarget > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------- Building ----------

        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        static VisualElement Box(VisualElement parent, string name)
        {
            var b = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            b.style.position = Position.Absolute;
            b.style.backgroundColor = Panel;
            Round(b, 10);
            Pad(b, 16, 10);
            parent.Add(b);
            return b;
        }

        static VisualElement Row(VisualElement parent)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row;
            r.style.justifyContent = Justify.Center;
            parent.Add(r);
            return r;
        }

        static Label Text(VisualElement parent, int size, Color colour)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.flexShrink = 0;
            parent.Add(l);
            return l;
        }

        static void Pad(VisualElement e, float h, float v)
        {
            e.style.paddingLeft = h; e.style.paddingRight = h; e.style.paddingTop = v; e.style.paddingBottom = v;
        }

        static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }
    }
}
