using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Argument
{
    /// <summary>
    /// The argument battle's screen (UI Toolkit, on the shell's HUD layer, built in code). Top: the opponent, their
    /// expression, the claim. Middle: the premise tags (shape and text tell the kind and status, never colour alone:
    /// a square tag for a fact, a chain for a link, dashed with "?" while hidden; struck through when broken, a check
    /// when conceded, "FORCED" when pressed) or, during pressure, the arena's surface. Right: the transcript, every
    /// step also as a sentence. Bottom: Nadia's heart rate, heat and anger, the verbs with their current keys, and
    /// the evidence tray or the reason cards when she's choosing. Buttons take clicks but never focus: keys and pads
    /// drive the selection through <see cref="ArgumentHost"/>. Nothing allocated per frame except while text changes.
    /// </summary>
    public sealed class ArgumentView
    {
        public const string RootName = "argument-view";
        static readonly Color Ink = new Color(0.96f, 0.94f, 0.9f);
        static readonly Color Dim = new Color(0.72f, 0.7f, 0.78f);
        static readonly Color Accent = new Color(1f, 0.79f, 0.29f);
        static readonly Color Panel = new Color(0.1f, 0.09f, 0.16f, 0.92f);
        static readonly Color Card = new Color(0.2f, 0.18f, 0.3f, 0.96f);
        static readonly Color Bad = new Color(1f, 0.42f, 0.42f);
        static readonly Color Good = new Color(0.48f, 0.88f, 0.56f);

        public readonly VisualElement Root;
        readonly Label _opponent, _claim, _placeholder, _heart, _heat, _anger, _status, _hint, _prompt;
        readonly VisualElement _premises, _surface, _tray, _reasons, _end, _transcriptBox, _verbs;
        readonly Label _endTitle, _endBody;
        readonly Button _endContinue;
        readonly List<Button> _premiseButtons = new List<Button>(16);
        readonly List<Button> _trayButtons = new List<Button>(16);
        readonly List<Button> _reasonButtons = new List<Button>(5);
        readonly List<Label> _lines = new List<Label>(10);
        readonly Label[] _slipLabels = new Label[2];
        readonly Dictionary<string, Button> _verbButtons = new Dictionary<string, Button>();
        ArgumentHost _host;

        public const int TranscriptLines = 9;

        public ArgumentView(VisualElement parent)
        {
            Root = new VisualElement { name = RootName, pickingMode = PickingMode.Ignore };
            Root.AddToClassList(UiHider.KeepClass); // the argument is the game: presenter mode's Hide UI keeps it
            Fill(Root);
            parent.Add(Root);

            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(shade);
            shade.style.backgroundColor = new Color(0.04f, 0.03f, 0.08f, 0.55f);
            Root.Add(shade);

            // Top: who, the claim, the placeholder badge.
            var top = Box(Root, 40, 24, 1380, 120);
            _opponent = Text(top, 26, Dim);
            _claim = Text(top, 40, Accent);
            _claim.style.unityFontStyleAndWeight = FontStyle.Bold;
            _placeholder = Text(Root, 20, Bad);
            Place(_placeholder, 1460, 30, 420, 40);
            _placeholder.style.unityTextAlign = TextAnchor.MiddleRight;

            // Middle: the premise tags, or the surface during pressure.
            _premises = new VisualElement { name = "argument-premises", pickingMode = PickingMode.Ignore };
            Place(_premises, 40, 170, 1380, 560);
            _premises.style.flexDirection = FlexDirection.Row;
            _premises.style.flexWrap = Wrap.Wrap;
            _premises.style.alignContent = Align.FlexStart;
            Root.Add(_premises);

            _surface = new VisualElement { name = "argument-surface", pickingMode = PickingMode.Ignore };
            Place(_surface, 110, 190, 1200, 640);
            _surface.style.backgroundColor = new Color(0.98f, 0.93f, 0.84f, 0.95f); // the tablecloth
            _surface.style.borderTopLeftRadius = _surface.style.borderTopRightRadius = 24;
            _surface.style.borderBottomLeftRadius = _surface.style.borderBottomRightRadius = 24;
            _surface.generateVisualContent += DrawSurface;
            _surface.style.display = DisplayStyle.None;
            Root.Add(_surface);
            for (int i = 0; i < _slipLabels.Length; i++)
            {
                _slipLabels[i] = Text(_surface, 18, new Color(0.3f, 0.2f, 0.5f));
                _slipLabels[i].style.position = Position.Absolute;
                _slipLabels[i].style.display = DisplayStyle.None;
            }

            // Right: the transcript.
            _transcriptBox = Box(Root, 1450, 90, 440, 760);
            _transcriptBox.style.backgroundColor = Panel;
            _transcriptBox.style.paddingLeft = _transcriptBox.style.paddingRight = 14;
            _transcriptBox.style.paddingTop = 10;
            var heading = Text(_transcriptBox, 20, Dim);
            heading.text = "TRANSCRIPT";
            for (int i = 0; i < TranscriptLines; i++)
            {
                var l = Text(_transcriptBox, 20, Ink);
                l.style.marginBottom = 6;
                _lines.Add(l);
            }

            // Bottom: heart rate, heat, anger; the prompt; the verbs; the tray or the reasons.
            var vitals = Box(Root, 40, 750, 360, 120);
            _heart = Text(vitals, 30, Ink);
            _heat = Text(vitals, 22, Dim);
            _anger = Text(vitals, 22, Dim);

            _prompt = Text(Root, 24, Ink);
            Place(_prompt, 420, 740, 1000, 40);

            _verbs = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(_verbs, 420, 780, 1010, 70);
            _verbs.style.flexDirection = FlexDirection.Row;
            Root.Add(_verbs);
            foreach (var v in new[] { "ask", "challenge", "concede", "press", "rest" }) _verbButtons[v] = VerbButton(v);

            _tray = new VisualElement { name = "argument-tray", pickingMode = PickingMode.Ignore };
            Place(_tray, 40, 870, 1390, 190);
            _tray.style.flexDirection = FlexDirection.Row;
            _tray.style.flexWrap = Wrap.Wrap;
            Root.Add(_tray);

            _reasons = new VisualElement { name = "argument-reasons", pickingMode = PickingMode.Ignore };
            Place(_reasons, 200, 300, 1100, 520);
            _reasons.style.backgroundColor = Panel;
            _reasons.style.paddingTop = _reasons.style.paddingBottom = 16;
            _reasons.style.paddingLeft = _reasons.style.paddingRight = 16;
            _reasons.style.display = DisplayStyle.None;
            Root.Add(_reasons);

            _status = Text(Root, 20, Dim);
            Place(_status, 1450, 860, 440, 60);
            _hint = Text(Root, 18, Dim);
            Place(_hint, 40, 1040, 1850, 30);

            // The end card.
            _end = Box(Root, 460, 260, 1000, 480);
            _end.style.backgroundColor = Panel;
            _end.style.paddingLeft = _end.style.paddingRight = _end.style.paddingTop = 30;
            _end.style.display = DisplayStyle.None;
            _endTitle = Text(_end, 44, Accent);
            _endBody = Text(_end, 26, Ink);
            _endBody.style.whiteSpace = WhiteSpace.Normal;
            _endContinue = new Button { name = "argument-continue", text = "Continue", focusable = false };
            StyleButton(_endContinue, 26);
            _endContinue.style.marginTop = 24;
            _endContinue.clicked += () => _host?.Continue();
            _end.Add(_endContinue);
        }

        public bool IsShown => Root.panel != null;

        public void Bind(ArgumentHost host) => _host = host;

        public void Remove() => Root.RemoveFromHierarchy();

        // ---------- What the host writes ----------

        public void SetHeader(string opponent, string expression, string claim, bool placeholder)
        {
            _opponent.text = string.IsNullOrEmpty(expression) ? opponent : opponent + "  (" + expression + ")";
            _claim.text = "CLAIM: “" + claim + "”";
            _placeholder.text = placeholder ? "PLACEHOLDER LINES: the creator writes the real ones" : "";
        }

        public void SetVitals(int heartRate, int heartStep, int heat, int anger)
        {
            _heart.text = "♥ " + heartRate + " bpm  " + Pips(heartStep, 5);
            _heat.text = "Heat " + Pips(heat, 3);
            _anger.text = anger > 0 ? "Anger " + Pips(anger, 3) : "";
            _heart.style.color = heartStep >= 4 ? Bad : Ink;
        }

        static string Pips(int n, int max)
        {
            var c = new char[max];
            for (int i = 0; i < max; i++) c[i] = i < n ? '●' : '○';
            return new string(c);
        }

        public void SetPrompt(string text) => _prompt.text = text ?? "";
        public void SetStatus(string text) => _status.text = text ?? "";
        public void SetHint(string text) => _hint.text = text ?? "";

        /// <summary>The verbs' labels with their current keys, and which are available.</summary>
        public void SetVerb(string verb, string label, bool enabled)
        {
            if (!_verbButtons.TryGetValue(verb, out var b)) return;
            b.text = label;
            b.SetEnabled(enabled);
            b.style.opacity = enabled ? 1f : 0.4f;
        }

        public void SetPressProgress(float t)
        {
            if (_verbButtons.TryGetValue("press", out var b))
                b.style.borderBottomColor = t > 0f ? Color.Lerp(Card, Bad, t) : Card;
        }

        /// <summary>The tags: index 0 is the claim, then premises (and dashed "?" for hidden ones, not selectable).</summary>
        public void SetPremises(IList<string> labels, IList<string> texts, IList<string> statuses, IList<bool> selectable, int selected)
        {
            while (_premiseButtons.Count < labels.Count)
            {
                int index = _premiseButtons.Count;
                var b = new Button { focusable = false };
                StyleButton(b, 20);
                b.style.width = 330;
                b.style.height = 150;
                b.style.marginRight = b.style.marginBottom = 14;
                b.style.whiteSpace = WhiteSpace.Normal;
                b.style.unityTextAlign = TextAnchor.UpperLeft;
                b.clicked += () => _host?.ClickPremise(index);
                _premiseButtons.Add(b);
                _premises.Add(b);
            }
            for (int i = 0; i < _premiseButtons.Count; i++)
            {
                var b = _premiseButtons[i];
                bool used = i < labels.Count;
                b.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;
                b.name = "argument-premise-" + i;
                b.text = "<b>" + labels[i] + "</b>   <size=80%>" + statuses[i] + "</size>\n" + texts[i];
                bool sel = i == selected;
                Border(b, sel ? Accent : (selectable[i] ? Dim : new Color(0.4f, 0.4f, 0.45f)), sel ? 5 : 2);
                b.style.opacity = selectable[i] ? 1f : 0.55f;
            }
        }

        /// <summary>The tray (and the retort card), or nothing.</summary>
        public void SetTray(IList<string> names, int selected)
        {
            int n = names != null ? names.Count : 0;
            while (_trayButtons.Count < n)
            {
                int index = _trayButtons.Count;
                var b = new Button { focusable = false };
                StyleButton(b, 20);
                b.style.width = 250;
                b.style.height = 64;
                b.style.marginRight = b.style.marginBottom = 10;
                b.style.whiteSpace = WhiteSpace.Normal;
                b.clicked += () => _host?.ClickEvidence(index);
                _trayButtons.Add(b);
                _tray.Add(b);
            }
            for (int i = 0; i < _trayButtons.Count; i++)
            {
                var b = _trayButtons[i];
                bool used = i < n;
                b.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;
                b.name = "argument-evidence-" + i;
                b.text = names[i];
                Border(b, i == selected ? Accent : Dim, i == selected ? 5 : 2);
            }
        }

        /// <summary>The "because..." cards (hidden when null).</summary>
        public void SetReasons(string heading, IList<string> texts, int selected)
        {
            bool shown = texts != null && texts.Count > 0;
            _reasons.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) return;
            if (_reasons.childCount == 0)
            {
                var h = Text(_reasons, 22, Dim);
                h.name = "argument-reasons-heading";
            }
            ((Label)_reasons[0]).text = heading;
            while (_reasonButtons.Count < texts.Count)
            {
                int index = _reasonButtons.Count;
                var b = new Button { focusable = false };
                StyleButton(b, 24);
                b.style.whiteSpace = WhiteSpace.Normal;
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                b.style.marginTop = 10;
                b.style.minHeight = 70;
                b.clicked += () => _host?.ClickReason(index);
                _reasonButtons.Add(b);
                _reasons.Add(b);
            }
            for (int i = 0; i < _reasonButtons.Count; i++)
            {
                var b = _reasonButtons[i];
                bool used = i < texts.Count;
                b.style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used) continue;
                b.name = "argument-reason-" + i;
                b.text = "Because... " + texts[i];
                Border(b, i == selected ? Accent : Dim, i == selected ? 5 : 2);
            }
        }

        /// <summary>The last lines of the transcript (Logic lines in the accent colour, in italics).</summary>
        public void SetTranscript(IReadOnlyList<Exchange> transcript)
        {
            int start = Math.Max(0, transcript.Count - TranscriptLines);
            for (int i = 0; i < TranscriptLines; i++)
            {
                var l = _lines[i];
                int t = start + i;
                if (t >= transcript.Count) { l.text = ""; continue; }
                var x = transcript[t];
                switch (x.speaker)
                {
                    case Speaker.Logic:
                        l.text = "<i>" + x.text + "</i>";
                        l.style.color = Accent;
                        break;
                    case Speaker.Nadia:
                        l.text = "<b>Nadia:</b> " + x.text;
                        l.style.color = Ink;
                        break;
                    default:
                        l.text = "<b>" + x.who + ":</b> " + x.text;
                        l.style.color = x.speaker == Speaker.Supporter ? Good : new Color(0.86f, 0.84f, 1f);
                        break;
                }
            }
        }

        public void ShowPressure(bool shown)
        {
            _surface.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            _premises.style.display = shown ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void ShowEnd(string title, string body)
        {
            _end.style.display = DisplayStyle.Flex;
            _endTitle.text = title;
            _endBody.text = body;
            _surface.style.display = DisplayStyle.None;
            _reasons.style.display = DisplayStyle.None;
        }

        public bool EndShown => _end.style.display == DisplayStyle.Flex;
        public string EndTitle => _endTitle.text;

        // ---------- The surface (pressure phases) ----------

        PressurePhase _phase;

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
            float w = float.IsNaN(r.width) || r.width <= 0f ? 1200f : r.width;
            float h = float.IsNaN(r.height) || r.height <= 0f ? 640f : r.height;
            float sx = w / (2f * PressurePhase.HalfWidth), sy = h / (2f * PressurePhase.HalfHeight);
            return new Vector2(w * 0.5f + u.X * sx, h * 0.5f - u.Y * sy);
        }

        float Scale
        {
            get
            {
                var r = _surface.contentRect;
                float w = float.IsNaN(r.width) || r.width <= 0f ? 1200f : r.width;
                return w / (2f * PressurePhase.HalfWidth);
            }
        }

        void DrawSurface(MeshGenerationContext mgc)
        {
            var phase = _phase;
            if (phase == null) return;
            var p = mgc.painter2D;
            float s = Scale;
            // Plates (walls): a column of plates with its gaps.
            p.fillColor = new Color(0.36f, 0.45f, 0.62f);
            var walls = phase.Walls;
            float step = PressurePhase.PlateRadius * 2.05f;
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (!w.active) continue;
                for (float y = -PressurePhase.HalfHeight + PressurePhase.PlateRadius; y <= PressurePhase.HalfHeight; y += step)
                {
                    if (phase.WallGapAt(w, y)) continue;
                    // Skip plates whose disc pokes into a gap (keep the drawn gap honest).
                    if (phase.WallGapAt(w, y + PressurePhase.PlateRadius) || phase.WallGapAt(w, y - PressurePhase.PlateRadius)) continue;
                    Disc(p, ToPixels(new System.Numerics.Vector2(w.x, y)), PressurePhase.PlateRadius * s);
                }
            }
            // Flour rings: arcs with their gaps.
            p.strokeColor = new Color(0.82f, 0.74f, 0.62f);
            p.lineWidth = PressurePhase.RingThickness * s;
            var rings = phase.Rings;
            for (int i = 0; i < rings.Count; i++)
            {
                var r = rings[i];
                if (!r.active || r.radius <= 0.01f) continue;
                var c = ToPixels(r.center);
                float rad = r.radius * s;
                // Unity's arcs run clockwise in screen space (y down), so angles flip sign.
                if (r.gapCount > 1)
                {
                    Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle1 - r.gapHalfAngle);
                    Arc(p, c, rad, r.gapAngle1 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
                }
                else Arc(p, c, rad, r.gapAngle0 + r.gapHalfAngle, r.gapAngle0 + 2f * Mathf.PI - r.gapHalfAngle);
            }
            // Batter.
            p.fillColor = new Color(0.93f, 0.78f, 0.42f);
            var blobs = phase.Blobs;
            for (int i = 0; i < blobs.Count; i++)
                if (blobs[i].active) Disc(p, ToPixels(blobs[i].position), PressurePhase.BlobRadius * s);
            // Slips: small paper planes (diamonds).
            p.fillColor = new Color(0.55f, 0.42f, 0.9f);
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
                p.fillColor = new Color(1f, 0.92f, 0.55f, 0.35f);
                Disc(p, c, PressurePhase.LightRadius * s * 2.2f);
                p.fillColor = new Color(1f, 0.85f, 0.3f);
                Disc(p, c, PressurePhase.LightRadius * s);
            }
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

        // ---------- Building ----------

        Button VerbButton(string verb)
        {
            var b = new Button { name = "argument-verb-" + verb, focusable = false };
            StyleButton(b, 20);
            b.style.width = 190;
            b.style.height = 60;
            b.style.marginRight = 10;
            b.style.whiteSpace = WhiteSpace.Normal;
            if (verb == "press")
            {
                // Press is a hold: pointer down starts it, up or leaving cancels it.
                b.RegisterCallback<PointerDownEvent>(_ => _host?.PointerPress(true), TrickleDown.TrickleDown);
                b.RegisterCallback<PointerUpEvent>(_ => _host?.PointerPress(false), TrickleDown.TrickleDown);
                b.RegisterCallback<PointerLeaveEvent>(_ => _host?.PointerPress(false));
            }
            else b.clicked += () => _host?.ClickVerb(verb);
            _verbs.Add(b);
            return b;
        }

        static void StyleButton(Button b, float fontSize)
        {
            b.style.fontSize = fontSize;
            b.style.color = Ink;
            b.style.backgroundColor = Card;
            b.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius = 10;
            b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 10;
            b.style.paddingLeft = b.style.paddingRight = 12;
            b.style.paddingTop = b.style.paddingBottom = 8;
            Border(b, Dim, 2);
        }

        static void Border(VisualElement e, Color c, float w)
        {
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = c;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = w;
        }

        static Label Text(VisualElement parent, float size, Color color)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(l);
            return l;
        }

        static VisualElement Box(VisualElement parent, float x, float y, float w, float h)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            Place(e, x, y, w, h);
            parent.Add(e);
            return e;
        }

        static void Place(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = w;
            e.style.height = h;
        }

        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.top = e.style.right = e.style.bottom = 0;
        }
    }
}
