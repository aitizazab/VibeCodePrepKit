using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The argument's 2D overlay (UI Toolkit, on the HUD layer): the speaker's portrait above the
    /// box, their line as a subtitle, the box with its two halves (listening below with a soft
    /// glow, pressing above, nearer the portrait), Nadia's composure as a small light with its
    /// pips, the objects drifting in, and the bullets, drawn as one generated mesh each frame
    /// (no element per bullet, nothing allocated per frame).
    /// </summary>
    public sealed class VhBattleView
    {
        const float BoxPixelsWide = 960f;

        static readonly Color[] StyleColors =
        {
            new Color(0.74f, 0.86f, 1f),   // gentle rain
            new Color(1f, 0.8f, 0.48f),    // slow rings
            new Color(0.96f, 0.93f, 0.86f),// walls
            new Color(1f, 0.9f, 0.52f),    // the spiral
        };

        public readonly VisualElement Root;
        readonly VisualElement _portrait, _box, _press, _listen, _bullets, _light, _pips, _hint;
        readonly Label _statement, _say, _reply, _pressWord, _listenWord;
        readonly VisualElement[] _objects;
        readonly Label[] _objectLabels;
        readonly string[] _objectShown;
        readonly VisualElement[] _pipDots;
        ArgumentBattle _battle;
        VisitingHoursConfig _config;
        float _scale = 120f, _halfW = 4f, _halfH = 2.5f;
        int _shownStatement = -2, _shownSerial = -1, _shownComposure = -1;
        string _shownExpression = "";
        float _blinkClock, _lineAge;
        static Texture2D _dot;

        public bool IsShown => Root.style.display == DisplayStyle.Flex;
        public string StatementText => _statement.text;
        public string ReplyText => _reply.text;

        public VhBattleView(VisualElement parent, StyleSheet style, int maxObjects)
        {
            Root = new VisualElement { name = "vh-battle", pickingMode = PickingMode.Ignore };
            Root.AddToClassList("vh-layer");
            Root.AddToClassList("vh-battle");
            Root.AddToClassList(UiHider.KeepClass); // the argument is the game: presenter mode's Hide UI keeps it
            if (style != null) Root.styleSheets.Add(style);
            parent.Add(Root);

            _portrait = Add(Root, "vh-battle__portrait");
            _statement = AddLabel(Root, "vh-battle__statement");
            _box = Add(Root, "vh-battle__box");
            _press = Add(_box, "vh-battle__zone");
            _press.AddToClassList("vh-battle__zone--press");
            _listen = Add(_box, "vh-battle__zone");
            _listen.AddToClassList("vh-battle__zone--listen");
            _pressWord = AddLabel(_press, "vh-battle__zone-word");
            _pressWord.text = "press";
            _listenWord = AddLabel(_listen, "vh-battle__zone-word");
            _listenWord.text = "listen";

            _bullets = Add(_box, "vh-battle__bullets");
            _bullets.generateVisualContent += DrawBullets;

            _objects = new VisualElement[Mathf.Max(1, maxObjects)];
            _objectLabels = new Label[_objects.Length];
            _objectShown = new string[_objects.Length];
            for (int i = 0; i < _objects.Length; i++)
            {
                _objects[i] = Add(_box, "vh-battle__object");
                var icon = Add(_objects[i], "vh-battle__object-icon");
                icon.name = "icon";
                _objectLabels[i] = AddLabel(_objects[i], "vh-battle__object-label");
                _objects[i].style.display = DisplayStyle.None;
            }
            _light = Add(_box, "vh-battle__light");

            _pips = Add(Root, "vh-battle__pips");
            _pipDots = new VisualElement[8];
            for (int i = 0; i < _pipDots.Length; i++) _pipDots[i] = Add(_pips, "vh-battle__pip");

            _say = AddLabel(Root, "vh-battle__say");
            _reply = AddLabel(Root, "vh-battle__reply");
            _hint = AddLabel(Root, "vh-battle__hint");
            ((Label)_hint).text = "WASD / stick: move the light";
            Root.style.display = DisplayStyle.None;
        }

        static VisualElement Add(VisualElement parent, string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent.Add(e);
            return e;
        }

        static Label AddLabel(VisualElement parent, string cls)
        {
            var l = new Label("") { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            parent.Add(l);
            return l;
        }

        public void Show(ArgumentBattle battle, VisitingHoursConfig config)
        {
            _battle = battle;
            _config = config;
            var d = battle.Def;
            _halfW = d.halfWidth;
            _halfH = d.halfHeight;
            _scale = BoxPixelsWide / (2f * _halfW);
            _box.style.width = BoxPixelsWide;
            _box.style.height = _scale * 2f * _halfH;
            float pressFrac = Mathf.Clamp01((_halfH - d.pressLine) / (2f * _halfH));
            _press.style.height = Length.Percent(pressFrac * 100f);
            _listen.style.top = Length.Percent(pressFrac * 100f);
            _listen.style.height = Length.Percent((1f - pressFrac) * 100f);
            bool words = config == null || config.showZoneWords;
            _pressWord.style.display = words ? DisplayStyle.Flex : DisplayStyle.None;
            _listenWord.style.display = words ? DisplayStyle.Flex : DisplayStyle.None;
            _shownStatement = -2;
            _shownSerial = battle.LineSerial;
            _shownComposure = -1;
            _shownExpression = "";
            _statement.text = d.practiceText;
            _say.text = "";
            _reply.text = "";
            _say.style.opacity = 0f;
            _reply.style.opacity = 0f;
            for (int i = 0; i < _pipDots.Length; i++) _pipDots[i].style.display = i < battle.MaxComposure ? DisplayStyle.Flex : DisplayStyle.None;
            SetExpression("neutral");
            Root.style.display = DisplayStyle.Flex;
            Root.style.opacity = 1f;
            Tick(0f);
        }

        public void Hide()
        {
            Root.style.display = DisplayStyle.None;
            _battle = null;
        }

        public void SetFade(float alpha) => Root.style.opacity = Mathf.Clamp01(alpha);

        void SetExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression) || expression == _shownExpression || _battle == null) return;
            _shownExpression = expression;
            var portrait = _config != null ? _config.FindPortrait(_battle.Def.speaker) : null;
            if (portrait == null && GameRoot.Exists) portrait = GameRoot.Config.FindPortrait(_battle.Def.speaker);
            var sprite = portrait != null ? portrait.FindExpression(expression) : null;
            if (sprite == null && portrait != null) sprite = portrait.FindExpression("");
            _portrait.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
        }

        public void Tick(float udt)
        {
            var b = _battle;
            if (b == null) return;
            _blinkClock += udt;
            _lineAge += udt;

            // The statement (or the practice line) and the speaker's face.
            if (b.StatementIndex != _shownStatement && (b.Phase == BattlePhase.Statement || b.Phase == BattlePhase.Practice))
            {
                _shownStatement = b.StatementIndex;
                var s = b.Statement;
                _statement.text = s != null ? s.text : b.Def.practiceText;
                SetExpression(s != null ? s.expression : "neutral");
                _statement.RemoveFromClassList("vh-battle__statement--in");
                _statement.AddToClassList("vh-battle__statement--in");
            }
            _hint.style.display = b.Phase == BattlePhase.Practice ? DisplayStyle.Flex : DisplayStyle.None;

            // What Nadia said with an object, and the reply.
            if (b.LineSerial != _shownSerial)
            {
                _shownSerial = b.LineSerial;
                _say.text = "<i>" + b.LastSay + "</i>";
                _reply.text = b.LastReply;
                _say.style.opacity = 1f;
                _reply.style.opacity = 1f;
                _lineAge = 0f;
                SetExpression(b.LastReplyExpression);
            }
            else if (_lineAge > 4.5f && _say.style.opacity.value > 0f)
            {
                _say.style.opacity = 0f;
                _reply.style.opacity = 0f;
            }

            // Zones: the one she's in glows; both pulse while the statement is about to count.
            bool pressing = b.InPressZone;
            bool counting = b.Phase == BattlePhase.Statement && b.StatementTimeLeft < 2f;
            _press.EnableInClassList("vh-battle__zone--on", pressing);
            _listen.EnableInClassList("vh-battle__zone--on", !pressing);
            float pulse = counting ? 0.55f + 0.45f * Mathf.Sin(_blinkClock * 9f) : 1f;
            _pressWord.style.opacity = (pressing ? 0.55f : 0.22f) * pulse;
            _listenWord.style.opacity = (!pressing ? 0.55f : 0.22f) * pulse;

            // The light.
            var p = b.Player;
            float lx = (p.X + _halfW) * _scale, ly = (_halfH - p.Y) * _scale;
            _light.style.translate = new Translate(lx - 13f, ly - 13f);
            bool blinkOff = b.Blink > 0f && Mathf.Repeat(_blinkClock * 12f, 1f) < 0.5f;
            _light.style.opacity = blinkOff ? 0.25f : 1f;
            _light.EnableInClassList("vh-battle__light--press", pressing);

            if (b.Composure != _shownComposure)
            {
                _shownComposure = b.Composure;
                for (int i = 0; i < _pipDots.Length; i++) _pipDots[i].EnableInClassList("vh-battle__pip--lost", i >= b.Composure);
            }

            // The objects.
            var objs = b.Objects;
            for (int i = 0; i < _objects.Length; i++)
            {
                var e = _objects[i];
                if (i >= objs.Length || !objs[i].active)
                {
                    if (e.style.display != DisplayStyle.None) e.style.display = DisplayStyle.None;
                    continue;
                }
                var o = objs[i];
                if (_objectShown[i] != o.entry)
                {
                    _objectShown[i] = o.entry;
                    var def = _config != null ? FindEntry(o.entry) : null;
                    _objectLabels[i].text = def != null ? def.title : o.entry;
                    var tex = _config != null && def != null ? _config.Art(def.icon) : null;
                    e.Q("icon").style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
                }
                e.style.display = DisplayStyle.Flex;
                e.style.translate = new Translate((o.position.X + _halfW) * _scale - 45f, (_halfH - o.position.Y) * _scale - 45f);
            }
            _bullets.MarkDirtyRepaint();
        }

        JournalEntryDef FindEntry(string id)
        {
            foreach (var e in _config.entries) if (e != null && e.id == id) return e;
            return null;
        }

        // ---------- Bullets: one mesh of soft dots ----------

        void DrawBullets(MeshGenerationContext mgc)
        {
            var b = _battle;
            if (b == null) return;
            var pool = b.Bullets;
            int n = Mathf.Min(pool.Count, 4000);
            if (n == 0) return;
            var mesh = mgc.Allocate(n * 4, n * 6, DotTexture());
            var items = pool.Items;
            for (int i = 0; i < n; i++)
            {
                ref var bullet = ref items[i];
                float cx = (bullet.position.X + _halfW) * _scale;
                float cy = (_halfH - bullet.position.Y) * _scale;
                float r = Mathf.Max(5f, bullet.radius * _scale * 1.9f);
                var c = StyleColors[bullet.style >= 0 && bullet.style < StyleColors.Length ? bullet.style : 0];
                Color32 tint = c;
                mesh.SetNextVertex(new Vertex { position = new Vector3(cx - r, cy - r, Vertex.nearZ), tint = tint, uv = new Vector2(0f, 1f) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(cx + r, cy - r, Vertex.nearZ), tint = tint, uv = new Vector2(1f, 1f) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(cx + r, cy + r, Vertex.nearZ), tint = tint, uv = new Vector2(1f, 0f) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(cx - r, cy + r, Vertex.nearZ), tint = tint, uv = new Vector2(0f, 0f) });
                ushort k = (ushort)(i * 4);
                mesh.SetNextIndex(k);
                mesh.SetNextIndex((ushort)(k + 1));
                mesh.SetNextIndex((ushort)(k + 2));
                mesh.SetNextIndex((ushort)(k + 2));
                mesh.SetNextIndex((ushort)(k + 3));
                mesh.SetNextIndex(k);
            }
        }

        static Texture2D DotTexture()
        {
            if (_dot != null) return _dot;
            const int s = 32;
            _dot = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "VH bullet", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float core = 1f - Mathf.SmoothStep(0.42f, 0.55f, d);
                    float halo = (1f - Mathf.SmoothStep(0.5f, 1f, d)) * 0.35f;
                    float a = Mathf.Clamp01(core + halo);
                    byte w = (byte)(255f * Mathf.Lerp(0.85f, 1f, core));
                    px[y * s + x] = new Color32(w, w, w, (byte)(a * 255f));
                }
            _dot.SetPixels32(px);
            _dot.Apply();
            return _dot;
        }
    }
}
