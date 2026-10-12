using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The slice's HUD on the shell's HUD layer: a small dot in the middle, the "E  Examine: ..."
    /// prompt, the journal hint (with the open questions count), a controls line that fades once
    /// Nadia has walked, a toast for new entries, the dream's warm vignette, a dimmer (the street
    /// fading at the end) and the black title cards between places.
    /// </summary>
    public sealed class VhHud
    {
        public readonly VisualElement Root;
        readonly VisualElement _dot, _vignette, _dim, _card;
        readonly Label _prompt, _journalHint, _controls, _toast, _cardTitle, _cardSub;
        float _toastLeft, _controlsLeft = -1f;
        string _promptText = "";
        int _shownQuestions = -1;
        static Texture2D _vignetteTex;

        public string PromptText => _promptText;
        public string ToastText => _toast.text;

        VisualElement _sheet;
        Label _sheetTitle;
        readonly System.Collections.Generic.List<Label> _sheetLines = new System.Collections.Generic.List<Label>();
        string _sheetKey = "";

        /// <summary>The round sheet (docs/redesign/day.md): a card at the top left with a title and its lines, each wrapping
        /// inside the card (nothing clips). Null title hides it.</summary>
        public void SetChecklist(string title, System.Collections.Generic.IReadOnlyList<string> lines)
        {
            string key = title ?? "";
            if (lines != null) foreach (var l in lines) key += "\n" + l;
            if (key == _sheetKey) return;
            _sheetKey = key;
            if (title == null)
            {
                if (_sheet != null) _sheet.style.display = DisplayStyle.None;
                return;
            }
            if (_sheet == null)
            {
                _sheet = new VisualElement { name = "vh-round-sheet", pickingMode = PickingMode.Ignore };
                var s = _sheet.style;
                s.position = Position.Absolute;
                s.left = 36;
                s.top = 96;
                s.width = 470;
                s.paddingLeft = 18;
                s.paddingRight = 18;
                s.paddingTop = 12;
                s.paddingBottom = 14;
                s.backgroundColor = new Color(0.93f, 0.91f, 0.86f, 0.9f);
                s.borderTopLeftRadius = 4;
                s.borderTopRightRadius = 4;
                s.borderBottomLeftRadius = 4;
                s.borderBottomRightRadius = 4;
                _sheetTitle = new Label("") { pickingMode = PickingMode.Ignore };
                _sheetTitle.style.fontSize = 22;
                _sheetTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                _sheetTitle.style.color = new Color(0.2f, 0.17f, 0.13f);
                _sheetTitle.style.whiteSpace = WhiteSpace.Normal;
                _sheetTitle.style.marginBottom = 6;
                _sheet.Add(_sheetTitle);
                Root.Add(_sheet);
            }
            _sheet.style.display = DisplayStyle.Flex;
            _sheetTitle.text = title;
            int n = lines != null ? lines.Count : 0;
            while (_sheetLines.Count < n)
            {
                var l = new Label("") { pickingMode = PickingMode.Ignore };
                l.style.fontSize = 19;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.marginTop = 3;
                _sheet.Add(l);
                _sheetLines.Add(l);
            }
            for (int i = 0; i < _sheetLines.Count; i++)
            {
                var l = _sheetLines[i];
                if (i >= n)
                {
                    l.style.display = DisplayStyle.None;
                    continue;
                }
                string text = lines[i];
                bool done = text.EndsWith(DutyRound.DoneMark);
                l.style.display = DisplayStyle.Flex;
                l.text = text;
                l.style.color = done ? new Color(0.36f, 0.42f, 0.34f) : new Color(0.2f, 0.17f, 0.13f);
            }
        }

        /// <summary>The round sheet's lines as shown (tests).</summary>
        public string ChecklistText => _sheet != null && _sheet.style.display == DisplayStyle.Flex ? _sheetKey : "";
        public string CardTitle => _cardTitle.text;
        public float CardAlpha => _card.resolvedStyle.opacity;

        public VhHud(VisualElement parent, StyleSheet style)
        {
            Root = new VisualElement { name = "vh-hud", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("vh-layer");
            if (style != null) Root.styleSheets.Add(style);
            parent.Add(Root);

            _vignette = Layer("vh-vignette");
            _vignette.style.backgroundImage = new StyleBackground(VignetteTexture());
            _vignette.style.opacity = 0f;
            _dim = Layer("vh-dim");
            _dim.style.opacity = 0f;

            _dot = new VisualElement { name = "vh-dot", pickingMode = PickingMode.Ignore };
            _dot.AddToClassList("vh-dot");
            Root.Add(_dot);
            _prompt = AddLabel("vh-prompt", "");
            _journalHint = AddLabel("vh-journal-hint", "J  Journal");
            _controls = AddLabel("vh-controls", "WASD / stick: walk    Mouse: look    Shift / L3: hurry    Q / F: peek    E: look closer    J: journal");
            _controls.style.opacity = 0f;
            _toast = AddLabel("vh-toast", "");
            _toast.style.opacity = 0f;

            _card = Layer("vh-card");
            _card.style.opacity = 0f;
            _cardTitle = new Label("") { name = "vh-card-title", pickingMode = PickingMode.Ignore };
            _cardTitle.AddToClassList("vh-card__title");
            _cardSub = new Label("") { name = "vh-card-sub", pickingMode = PickingMode.Ignore };
            _cardSub.AddToClassList("vh-card__sub");
            _card.Add(_cardTitle);
            _card.Add(_cardSub);
        }

        VisualElement Layer(string cls)
        {
            var e = new VisualElement { name = cls, pickingMode = PickingMode.Ignore };
            e.AddToClassList("vh-layer");
            e.AddToClassList(cls);
            Root.Add(e);
            return e;
        }

        Label AddLabel(string cls, string text)
        {
            var l = new Label(text) { name = cls, pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            Root.Add(l);
            return l;
        }

        public void Remove() => Root.RemoveFromHierarchy();

        public void SetExploring(bool on)
        {
            var d = on ? DisplayStyle.Flex : DisplayStyle.None;
            _dot.style.display = d;
            _journalHint.style.display = d;
            if (!on) SetPrompt("");
        }

        public void SetPrompt(string text)
        {
            text = text ?? "";
            if (text == _promptText) return;
            _promptText = text;
            _prompt.text = text;
            _prompt.style.opacity = text.Length > 0 ? 1f : 0f;
            _dot.EnableInClassList("vh-dot--target", text.Length > 0);
        }

        public void SetOpenQuestions(int count)
        {
            if (count == _shownQuestions) return;
            _shownQuestions = count;
            _journalHint.text = count > 0 ? "J  Journal    (" + count + (count == 1 ? " open question)" : " open questions)") : "J  Journal";
            _journalHint.EnableInClassList("vh-journal-hint--open", count > 0);
        }

        public void ShowControls(float seconds)
        {
            _controlsLeft = seconds;
            _controls.style.opacity = 1f;
        }

        public void HideControls()
        {
            _controlsLeft = -1f;
            _controls.style.opacity = 0f;
        }

        public void Toast(string text, float seconds = 3.2f)
        {
            _toast.text = text;
            _toast.style.opacity = 1f;
            _toastLeft = seconds;
        }

        public void SetVignette(float alpha, Color color)
        {
            _vignette.style.opacity = alpha;
            _vignette.style.unityBackgroundImageTintColor = color;
        }

        public void SetDim(float alpha) => _dim.style.opacity = Mathf.Clamp01(alpha);

        public void SetCard(float alpha, string title, string sub)
        {
            _card.style.opacity = Mathf.Clamp01(alpha);
            _card.style.display = alpha > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            if (title != null && _cardTitle.text != title) _cardTitle.text = title;
            if (sub != null && _cardSub.text != sub) _cardSub.text = sub;
        }

        VisualElement _end;
        Label _endTitle, _endBody;
        Button _endFinish, _endRetry;

        public bool EndShown => _end != null && _end.style.display == DisplayStyle.Flex;
        public string EndBody => _endBody != null ? _endBody.text : "";

        /// <summary>The end card: what happened, where the notes are, Finish and (optionally) Argue again.</summary>
        public void ShowEnd(string title, string body, Action finish, Action retry, string retryLabel = null)
        {
            if (_end == null)
            {
                _end = new VisualElement { name = "vh-end", pickingMode = PickingMode.Position };
                _end.AddToClassList("vh-layer");
                _end.AddToClassList("vh-end");
                var panel = new VisualElement();
                panel.AddToClassList("vh-end__panel");
                _end.Add(panel);
                _endTitle = new Label("") { name = "vh-end-title" };
                _endTitle.AddToClassList("vh-end__title");
                panel.Add(_endTitle);
                _endBody = new Label("") { name = "vh-end-body" };
                _endBody.AddToClassList("vh-end__body");
                panel.Add(_endBody);
                var row = new VisualElement();
                row.AddToClassList("vh-end__buttons");
                panel.Add(row);
                _endRetry = new Button { name = "vh-end-retry", text = "Argue with Walt again" };
                _endRetry.AddToClassList("jk-button");
                row.Add(_endRetry);
                _endFinish = new Button { name = "vh-end-finish", text = "Finish" };
                _endFinish.AddToClassList("jk-button");
                _endFinish.AddToClassList("jk-button--primary");
                row.Add(_endFinish);
                _endFinish.clicked += () => { var a = _onFinish; HideEnd(); a?.Invoke(); };
                _endRetry.clicked += () => { var a = _onRetry; HideEnd(); a?.Invoke(); };
                Root.Add(_end);
            }
            _onFinish = finish;
            _onRetry = retry;
            _endTitle.text = title;
            _endBody.text = body;
            _endRetry.text = retryLabel ?? "Argue with Walt again";
            _endRetry.style.display = retry != null ? DisplayStyle.Flex : DisplayStyle.None;
            _end.style.display = DisplayStyle.Flex;
            _end.BringToFront();
            _endFinish.Focus();
        }

        Action _onFinish, _onRetry;

        public void HideEnd()
        {
            if (_end != null) _end.style.display = DisplayStyle.None;
        }

        /// <summary>Presses an end-card button (tests, the smoke run): "finish" or "retry".</summary>
        public bool PressEnd(string which)
        {
            if (!EndShown) return false;
            var a = which == "retry" ? _onRetry : _onFinish;
            if (a == null) return false;
            HideEnd();
            a();
            return true;
        }

        public void Tick(float udt)
        {
            if (_toastLeft > 0f)
            {
                _toastLeft -= udt;
                if (_toastLeft <= 0f) _toast.style.opacity = 0f;
            }
            if (_controlsLeft > 0f)
            {
                _controlsLeft -= udt;
                if (_controlsLeft <= 0f) _controls.style.opacity = 0f;
            }
        }

        /// <summary>A soft dark-to-clear ring (the dream's hazy edges), made once.</summary>
        static Texture2D VignetteTexture()
        {
            if (_vignetteTex != null) return _vignetteTex;
            const int w = 128, h = 72;
            _vignetteTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "VH vignette", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, d));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            _vignetteTex.SetPixels32(px);
            _vignetteTex.Apply();
            return _vignetteTex;
        }
    }
}
