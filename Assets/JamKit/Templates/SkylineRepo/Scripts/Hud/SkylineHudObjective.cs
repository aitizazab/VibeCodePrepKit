using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;
using NVec3 = System.Numerics.Vector3;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The HUD's objective (plan task H3; docs/SKYLINE-HUD.md "The objective" and "The wave clock"): the job sheet's three asset
    /// glyphs along the top edge (ticked in the objective violet when towed), a marker per untaken asset (a violet chevron on the
    /// screen's edge pointing at it when it is off screen or behind, a violet diamond over it when it is in view, which is what
    /// shows it through walls) with the distance in metres, the tow meter as a ring round the crosshair while the Unit stands in a
    /// circle, and the wave clock at the top right. No bot count.
    /// <para><b>No label text</b> (the creator, 2026-10-14: "there shouldn't be any text that says Plating"): the glyphs, the
    /// colours and the shapes say what everything is. The only characters drawn are numbers: a distance ("34 m") and the clock
    /// ("0:21", a wave icon beside it instead of the word NEXT). The ring keeps the crosshair free: the asset's glyph sits on the
    /// ring at twelve o'clock, not over the crosshair in its middle.</para>
    /// <para>Everything is pure data underneath: <see cref="Repossession"/> (the sheet, the active circle, each meter),
    /// <see cref="WaveClock"/> (<c>SecondsToNext</c>, which counts the interval from <c>FloorRules.WaveSeconds</c>, 16 bars of
    /// the floor song, not a literal 30) and V1's <see cref="EdgeMarker"/> and <see cref="HudText"/>. The director calls
    /// <see cref="Bind(Repossession, WaveClock)"/> when the floor starts (and again after a wreck builds a new run).</para>
    /// </summary>
    public sealed class SkylineHudObjective
    {
        public const string RootName = "hud-objective", StripName = "hud-job-strip", ClockName = "hud-wave-clock", RingName = "hud-tow-ring";
        public static readonly Color Violet = new Color32(0xB3, 0x6B, 0xFF, 255);   // docs/SKYLINE-STYLE.md: objectives

        // Layout (reference 1920 x 1080 panel units).
        public const float TopPx = 26f, SlotW = 84f, SlotH = 64f, SlotGap = 14f, SlotSkew = 12f;
        public const float RingRadius = 64f, RingBox = 176f, RingTrack = 14f, RingFill = 8f, RingIcon = 38f;
        public const float MarkerBox = 64f, MarkerLabelPx = 30f, MarkerLabelOffset = 50f;
        public const float ClockDigitPx = 56f, ClockIconW = 46f, ClockIconH = 34f;
        /// <summary>The clock flashes in its last this many seconds; it dims while a wave waits for room under the cap.</summary>
        public const float ClockFlashSeconds = 3f, ClockWaitingOpacity = 0.45f, ClockFlashHz = 4f;
        /// <summary>The height above an asset's floor socket the markers aim at (the middle of the asset).</summary>
        public const float AssetHeight = 1.2f;

        public enum MarkerMode { Hidden, OnScreen, OffScreen }

        // ---- the glyphs (shapes in a 0..1 box, y down): bodies are filled and keylined, cuts are ink
        public struct Glyph
        {
            public Vector2[][] body, cut;
        }

        static Vector2[] P(params float[] xy)
        {
            var v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return v;
        }

        static readonly Glyph MascotGlyph = new Glyph
        {
            body = new[]
            {
                P(0.06f, 0.14f, 0.30f, 0.14f, 0.30f, 0.42f, 0.06f, 0.42f),                                         // ears
                P(0.70f, 0.14f, 0.94f, 0.14f, 0.94f, 0.42f, 0.70f, 0.42f),
                P(0.20f, 0.22f, 0.80f, 0.22f, 0.96f, 0.42f, 0.96f, 0.72f, 0.78f, 0.96f, 0.22f, 0.96f, 0.04f, 0.72f, 0.04f, 0.42f), // head
            },
            cut = new[]
            {
                P(0.26f, 0.42f, 0.42f, 0.42f, 0.42f, 0.60f, 0.26f, 0.60f),                                         // eyes
                P(0.58f, 0.42f, 0.74f, 0.42f, 0.74f, 0.60f, 0.58f, 0.60f),
                P(0.28f, 0.72f, 0.72f, 0.72f, 0.62f, 0.86f, 0.38f, 0.86f),                                         // grin
            },
        };

        static readonly Glyph ChairGlyph = new Glyph
        {
            body = new[]
            {
                P(0.08f, 0.04f, 0.36f, 0.04f, 0.44f, 0.62f, 0.14f, 0.62f),                                         // the leaning back
                P(0.14f, 0.54f, 0.84f, 0.54f, 0.84f, 0.72f, 0.14f, 0.72f),                                         // the seat
                P(0.76f, 0.58f, 0.98f, 0.82f, 0.88f, 0.92f, 0.66f, 0.70f),                                         // the leg rest
                P(0.22f, 0.72f, 0.34f, 0.72f, 0.34f, 0.96f, 0.22f, 0.96f),                                         // legs
                P(0.60f, 0.72f, 0.72f, 0.72f, 0.72f, 0.96f, 0.60f, 0.96f),
            },
            cut = new[]
            {
                P(0.32f, 0.40f, 0.70f, 0.40f, 0.70f, 0.48f, 0.32f, 0.48f),                                         // the arm
            },
        };

        static readonly Glyph CabinetGlyph = new Glyph
        {
            body = new[]
            {
                P(0.26f, 0.02f, 0.74f, 0.02f, 0.74f, 0.98f, 0.26f, 0.98f),                                         // the cabinet
            },
            cut = new[]
            {
                P(0.33f, 0.10f, 0.67f, 0.10f, 0.67f, 0.42f, 0.33f, 0.42f),                                         // the screen
                P(0.31f, 0.52f, 0.69f, 0.52f, 0.69f, 0.60f, 0.31f, 0.60f),                                         // the controls
                P(0.44f, 0.72f, 0.56f, 0.72f, 0.56f, 0.82f, 0.44f, 0.82f),                                         // the coin slot
            },
        };

        /// <summary>The glyph shapes of an asset kind (a mascot head, a massage chair, an arcade cabinet; draft shapes for the
        /// creator to judge).</summary>
        public static Glyph GlyphOf(AssetKind kind)
        {
            switch (kind)
            {
                case AssetKind.Mascot: return MascotGlyph;
                case AssetKind.MassageChair: return ChairGlyph;
                default: return CabinetGlyph;
            }
        }

        /// <summary>Draws an asset glyph filling <paramref name="r"/> (a slight lean to the right at the top, like the bars).</summary>
        public static void DrawGlyph(Painter2D p, AssetKind kind, Rect r, Color fill, Color ink, float line)
        {
            const float lean = 0.10f;
            var g = GlyphOf(kind);
            for (int pass = 0; pass < 2; pass++)
            {
                var polys = pass == 0 ? g.body : g.cut;
                for (int s = 0; s < polys.Length; s++)
                {
                    var poly = polys[s];
                    p.BeginPath();
                    for (int i = 0; i < poly.Length; i++)
                    {
                        float x = r.x + (poly[i].x + (1f - poly[i].y) * lean) * r.width / (1f + lean);
                        float y = r.y + poly[i].y * r.height;
                        if (i == 0) p.MoveTo(new Vector2(x, y)); else p.LineTo(new Vector2(x, y));
                    }
                    p.ClosePath();
                    if (pass == 0) SkylineHudStyle.FillStroke(p, fill, ink, line);
                    else { p.fillColor = ink; p.Fill(); }
                }
            }
        }

        /// <summary>The clock's text without the word: "0:21". The pure model says "NEXT 0:21"; the HUD draws a wave icon in
        /// place of the word (the creator's no-captions rule).</summary>
        public static string ClockDigits(float seconds)
        {
            string t = HudText.WaveClock(seconds);
            return t.StartsWith("NEXT ") ? t.Substring(5) : t;
        }

        /// <summary>The tow ring's sweep in degrees for a meter progress (0..1, clamped; NaN reads 0).</summary>
        public static float RingSweep(float progress) => float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress) * 360f;

        // ------------------------------------------------------------------ state

        VisualElement _root, _strip, _clockEl, _ring;
        readonly VisualElement[] _slots = new VisualElement[3];
        readonly VisualElement[] _markerEls = new VisualElement[3];
        readonly VisualElement[] _markerShapes = new VisualElement[3];
        readonly KeylinedLabel[] _markerLabels = new KeylinedLabel[3];
        readonly MarkerMode[] _modes = new MarkerMode[3];
        readonly bool[] _slotTowed = new bool[3];
        readonly AssetKind[] _slotKinds = new AssetKind[3];
        readonly float[] _markerAngle = new float[3];
        VisualElement _clockIcon;
        KeylinedLabel _clockDigits;
        Repossession _rep;
        WaveClock _wave;
        SkylinePlayer _player;
        string _clockText;
        float _time, _ringAlpha, _ringProgress;
        int _ringEntry = -1;
        AssetKind _ringKind;
        Color _clockColour = SkylineHudStyle.Paper;

        /// <summary>Test hook: use this camera instead of the player's.</summary>
        public Camera CameraOverride;

        public VisualElement Element => _root;
        public VisualElement Strip => _strip;
        public VisualElement ClockElement => _clockEl;
        public VisualElement RingElement => _ring;
        public KeylinedLabel ClockDigitsLabel => _clockDigits;
        public VisualElement Slot(int i) => _slots[i];
        public VisualElement MarkerElement(int i) => _markerEls[i];
        public KeylinedLabel MarkerLabel(int i) => _markerLabels[i];
        public MarkerMode ModeOf(int i) => _modes[i];
        public bool SlotTowed(int i) => _slotTowed[i];
        public int RingEntry => _ringEntry;
        public float RingProgress => _ringProgress;
        public float RingAlpha => _ringAlpha;

        // ------------------------------------------------------------------ building

        /// <summary>Adds the objective's layers to <paramref name="parent"/> (the HUD's safe area): the job strip top centre, the
        /// clock top right, the tow ring on the crosshair, the markers.</summary>
        public VisualElement Build(VisualElement parent, float margin)
        {
            string face = SkylineHudStyle.AmmoFontDefault;
            _root = SkylineHudStyle.Layer(parent, RootName);
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;

            // The job sheet: three slanted plates with an asset glyph each, top centre.
            _strip = SkylineHudStyle.Layer(_root, StripName);
            _strip.style.top = TopPx;
            _strip.style.left = Length.Percent(50);
            float stripW = 3 * SlotW + 2 * SlotGap;
            _strip.style.width = stripW;
            _strip.style.height = SlotH;
            _strip.style.translate = new Translate(Length.Percent(-50), 0f);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var s = _slots[i] = SkylineHudStyle.Layer(_strip, "hud-job-" + i);
                s.style.left = i * (SlotW + SlotGap);
                s.style.top = 0;
                s.style.width = SlotW;
                s.style.height = SlotH;
                s.generateVisualContent += ctx => DrawSlot(ctx, idx);
                _slotKinds[i] = i == 0 ? AssetKind.Mascot : i == 1 ? AssetKind.MassageChair : AssetKind.ArcadeCabinet;
            }
            _strip.style.display = DisplayStyle.None;

            // The wave clock, top right: a wave icon and the digits.
            _clockEl = SkylineHudStyle.Layer(_root, ClockName);
            _clockEl.style.top = TopPx - 6f;
            _clockEl.style.right = margin;
            _clockEl.style.flexDirection = FlexDirection.Row;
            _clockEl.style.alignItems = Align.Center;
            _clockIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            _clockIcon.style.width = ClockIconW;
            _clockIcon.style.height = ClockIconH;
            _clockIcon.style.marginRight = 10f;
            _clockIcon.generateVisualContent += DrawClockIcon;
            _clockEl.Add(_clockIcon);
            _clockDigits = new KeylinedLabel(ClockDigitPx, SkylineHudStyle.KeylinePx, face);
            _clockEl.Add(_clockDigits);
            _clockEl.style.display = DisplayStyle.None;

            // The tow ring, centred on the crosshair.
            _ring = SkylineHudStyle.Layer(_root, RingName);
            _ring.style.left = Length.Percent(50);
            _ring.style.top = Length.Percent(50);
            _ring.style.width = RingBox;
            _ring.style.height = RingBox;
            _ring.style.marginLeft = -RingBox * 0.5f;
            _ring.style.marginTop = -RingBox * 0.5f;
            _ring.generateVisualContent += DrawRing;
            _ring.style.display = DisplayStyle.None;

            // The markers: an anchor point each, with its shape and its distance.
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var m = _markerEls[i] = SkylineHudStyle.Layer(_root, "hud-edge-marker-" + i);
                m.style.width = 0;
                m.style.height = 0;
                var shape = _markerShapes[i] = SkylineHudStyle.Layer(m, "hud-edge-marker-shape-" + i);
                shape.style.left = -MarkerBox * 0.5f;
                shape.style.top = -MarkerBox * 0.5f;
                shape.style.width = MarkerBox;
                shape.style.height = MarkerBox;
                shape.generateVisualContent += ctx => DrawMarker(ctx, idx);
                var lab = _markerLabels[i] = new KeylinedLabel(MarkerLabelPx, 2f, face);
                lab.style.position = Position.Absolute;
                lab.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                lab.SetColor(Violet);
                m.Add(lab);
                m.style.display = DisplayStyle.None;
            }
            return _root;
        }

        /// <summary>Follows the floor's objective and wave clock. Either may be null (that part hides). Call again after a wreck
        /// gives a new <see cref="Repossession"/>.</summary>
        public void Bind(Repossession rep, WaveClock wave)
        {
            _rep = rep;
            _wave = wave;
            _clockText = null;
            for (int i = 0; i < 3; i++) { _modes[i] = MarkerMode.Hidden; _slotTowed[i] = false; }
            if (rep != null)
                for (int i = 0; i < 3 && i < rep.Sheet.Count; i++) _slotKinds[i] = rep.Sheet[i].asset.kind;
            foreach (var s in _slots) s?.MarkDirtyRepaint();
        }

        /// <summary>The player whose camera projects the markers (the HUD passes it in <c>Start</c>).</summary>
        public void BindPlayer(SkylinePlayer player) => _player = player;

        // ------------------------------------------------------------------ the frame

        /// <summary>Per frame (unscaled seconds).</summary>
        public void Tick(float dt)
        {
            if (_root == null) return;
            _time += dt;
            TickClock();
            TickSheet(dt);
        }

        void TickClock()
        {
            bool show = _wave != null && _wave.Running;
            _clockEl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            float s = _wave.SecondsToNext;
            string text = ClockDigits(s);
            if (text != _clockText)
            {
                _clockText = text;
                _clockDigits.SetText(text);
            }
            bool flashing = s > 0f && s <= ClockFlashSeconds;
            float pulse = flashing ? (Mathf.Sin(_time * ClockFlashHz * Mathf.PI * 2f) > 0f ? 1f : 0f) : 0f;
            Color c = Color.Lerp(SkylineHudStyle.Paper, SkylineHudStyle.Chassis, pulse);
            if (c != _clockColour)
            {
                _clockColour = c;
                _clockDigits.SetColor(c);
                _clockIcon.MarkDirtyRepaint();
            }
            float k = flashing ? 1f + 0.12f * pulse : 1f;
            _clockEl.style.scale = new Scale(new Vector3(k, k, 1f));
            _clockEl.style.opacity = _wave.Waiting != null ? ClockWaitingOpacity : 1f;
        }

        void TickSheet(float dt)
        {
            bool have = _rep != null;
            _strip.style.display = have ? DisplayStyle.Flex : DisplayStyle.None;
            if (!have)
            {
                for (int i = 0; i < 3; i++) { _modes[i] = MarkerMode.Hidden; _markerEls[i].style.display = DisplayStyle.None; }
                SetRing(-1, 0f, dt);
                return;
            }

            // the job sheet: tick the towed ones; the asset in the circle pulses
            int active = _rep.ActiveCircle;
            for (int i = 0; i < 3; i++)
            {
                bool present = i < _rep.Sheet.Count;
                _slots[i].style.display = present ? DisplayStyle.Flex : DisplayStyle.None;
                if (!present) continue;
                bool towed = _rep.Sheet[i].Towed;
                var kind = _rep.Sheet[i].asset.kind;
                if (towed != _slotTowed[i] || kind != _slotKinds[i])
                {
                    _slotTowed[i] = towed;
                    _slotKinds[i] = kind;
                    _slots[i].MarkDirtyRepaint();
                }
                float k = i == active ? 1f + 0.07f * Mathf.Sin(_time * 9f) : 1f;
                _slots[i].style.scale = new Scale(new Vector3(k, k, 1f));
            }

            TickMarkers();

            // the tow ring: only inside a circle
            float progress = active >= 0 && active < _rep.Sheet.Count ? _rep.Sheet[active].meter.Progress : 0f;
            SetRing(active, progress, dt);
        }

        void TickMarkers()
        {
            var cam = CameraOverride != null ? CameraOverride : _player != null && _player.Camera != null ? _player.Camera : Camera.main;
            float pw = _root.resolvedStyle.width, ph = _root.resolvedStyle.height;
            if (!(pw > 1f) || !(ph > 1f)) { pw = 1920f; ph = 1080f; }
            for (int i = 0; i < 3; i++)
            {
                bool show = cam != null && i < _rep.Sheet.Count && !_rep.Sheet[i].Towed;
                if (!show)
                {
                    _modes[i] = MarkerMode.Hidden;
                    _markerEls[i].style.display = DisplayStyle.None;
                    continue;
                }
                var sock = _rep.Sheet[i].socket.position;
                var cp = cam.transform.position;
                var fwd = cam.transform.forward;
                var up = cam.transform.up;
                var res = EdgeMarker.Compute(new NVec3(cp.x, cp.y, cp.z), new NVec3(fwd.x, fwd.y, fwd.z), new NVec3(up.x, up.y, up.z),
                    cam.fieldOfView, cam.aspect, new NVec3(sock.X, sock.Y + AssetHeight, sock.Z));
                var mode = res.onScreen ? MarkerMode.OnScreen : MarkerMode.OffScreen;
                float x = res.x01 * pw, y = (1f - res.y01) * ph;
                var m = _markerEls[i];
                m.style.display = DisplayStyle.Flex;
                m.style.left = x;
                m.style.top = y;
                if (mode != _modes[i] || !Mathf.Approximately(res.angleRad, _markerAngle[i]))
                {
                    _modes[i] = mode;
                    _markerAngle[i] = res.angleRad;
                    // a chevron rotates to point at the asset; the on-screen diamond stays upright
                    _markerShapes[i].style.rotate = new Rotate(new Angle(mode == MarkerMode.OffScreen ? -res.angleRad * Mathf.Rad2Deg : 0f));
                    _markerShapes[i].MarkDirtyRepaint();
                }
                // the distance sits on the inward side of the marker (towards the centre of the screen), never off the edge
                Vector2 inward = new Vector2(pw * 0.5f - x, ph * 0.5f - y);
                if (inward.sqrMagnitude < 1f) inward = Vector2.down;
                inward.Normalize();
                if (mode == MarkerMode.OnScreen) inward = Vector2.up;   // under the diamond (UI y grows downwards)
                var lab = _markerLabels[i];
                lab.style.left = inward.x * MarkerLabelOffset;
                lab.style.top = inward.y * MarkerLabelOffset;
                string d = HudText.Distance(res.distance);
                if (lab.Text != d) lab.SetText(d);
            }
        }

        void SetRing(int entry, float progress, float dt)
        {
            bool on = entry >= 0;
            if (on && entry < _rep.Sheet.Count)
            {
                if (entry != _ringEntry || _rep.Sheet[entry].asset.kind != _ringKind)
                {
                    _ringKind = _rep.Sheet[entry].asset.kind;
                    _ring.MarkDirtyRepaint();
                }
                _ringEntry = entry;
                _ringProgress = progress;
            }
            float target = on ? 1f : 0f;
            _ringAlpha += (target - _ringAlpha) * Mathf.Min(1f, dt * 16f);
            if (Mathf.Abs(target - _ringAlpha) < 0.01f) _ringAlpha = target;
            bool visible = _ringAlpha > 0.01f;
            _ring.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) { _ringEntry = on ? entry : -1; return; }
            _ring.style.opacity = _ringAlpha;
            _ring.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ drawing

        void DrawSlot(MeshGenerationContext ctx, int i)
        {
            var p = ctx.painter2D;
            bool towed = _slotTowed[i];
            // a slanted plate, hard edges
            SkylineHudStyle.Parallelogram(p, 4f, 4f, SlotW - SlotSkew - 8f, SlotH - 8f, SlotSkew);
            SkylineHudStyle.FillStroke(p, towed ? new Color(Violet.r, Violet.g, Violet.b, 0.30f) : SkylineHudStyle.Track,
                SkylineHudStyle.Ink, SkylineHudStyle.KeylinePx);
            var glyphRect = new Rect(SlotSkew + 10f, 11f, SlotW - SlotSkew - 26f, SlotH - 22f);
            DrawGlyph(p, _slotKinds[i], glyphRect, towed ? Violet : SkylineHudStyle.Paper, SkylineHudStyle.Ink, 2.5f);
            if (towed)
            {
                // the tick: ink keyline under a violet stroke... drawn big across the glyph
                p.lineCap = LineCap.Round;
                p.lineJoin = LineJoin.Round;
                for (int pass = 0; pass < 2; pass++)
                {
                    p.BeginPath();
                    p.MoveTo(new Vector2(SlotW * 0.32f, SlotH * 0.55f));
                    p.LineTo(new Vector2(SlotW * 0.46f, SlotH * 0.78f));
                    p.LineTo(new Vector2(SlotW * 0.80f, SlotH * 0.16f));
                    p.lineWidth = pass == 0 ? 13f : 7f;
                    p.strokeColor = pass == 0 ? SkylineHudStyle.Ink : SkylineHudStyle.Paper;
                    p.Stroke();
                }
            }
        }

        void DrawClockIcon(MeshGenerationContext ctx)
        {
            // a wave: two zig-zag strokes, ink under the colour
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Miter;
            p.lineCap = LineCap.Butt;
            for (int pass = 0; pass < 2; pass++)
                for (int row = 0; row < 2; row++)
                {
                    float y0 = ClockIconH * (0.30f + 0.42f * row);
                    p.BeginPath();
                    p.MoveTo(new Vector2(3f, y0 + 6f));
                    for (int k = 1; k <= 4; k++) p.LineTo(new Vector2(3f + k * (ClockIconW - 6f) / 4f, y0 + (k % 2 == 0 ? 6f : -6f)));
                    p.lineWidth = pass == 0 ? 10f : 5f;
                    p.strokeColor = pass == 0 ? SkylineHudStyle.Ink : _clockColour;
                    p.Stroke();
                }
        }

        void DrawRing(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var c = new Vector2(RingBox * 0.5f, RingBox * 0.5f);
            // the empty track: ink, see-through
            p.BeginPath();
            p.Arc(c, RingRadius, new Angle(0f), new Angle(360f));
            p.lineWidth = RingTrack;
            p.strokeColor = new Color(SkylineHudStyle.Ink.r, SkylineHudStyle.Ink.g, SkylineHudStyle.Ink.b, 0.78f);
            p.Stroke();
            // the meter: violet, clockwise from twelve o'clock
            float sweep = RingSweep(_ringProgress);
            if (sweep > 0.5f)
            {
                p.BeginPath();
                p.Arc(c, RingRadius, new Angle(-90f), new Angle(-90f + sweep));
                p.lineWidth = RingFill;
                p.lineCap = LineCap.Butt;
                p.strokeColor = Violet;
                p.Stroke();
            }
            // the asset's glyph on the ring at twelve o'clock (the middle is the crosshair's)
            var r = new Rect(c.x - RingIcon * 0.5f, c.y - RingRadius - RingIcon * 0.5f, RingIcon, RingIcon);
            DrawGlyph(p, _ringKind, r, Violet, SkylineHudStyle.Ink, 2.5f);
        }

        void DrawMarker(MeshGenerationContext ctx, int i)
        {
            var p = ctx.painter2D;
            var c = new Vector2(MarkerBox * 0.5f, MarkerBox * 0.5f);
            if (_modes[i] == MarkerMode.OnScreen)
            {
                // a hollow diamond over the asset: it shows through walls because it is drawn on the HUD
                const float r = 17f;
                for (int pass = 0; pass < 2; pass++)
                {
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(0f, -r));
                    p.LineTo(c + new Vector2(r, 0f));
                    p.LineTo(c + new Vector2(0f, r));
                    p.LineTo(c + new Vector2(-r, 0f));
                    p.ClosePath();
                    p.lineJoin = LineJoin.Miter;
                    p.lineWidth = pass == 0 ? 9f : 4f;
                    p.strokeColor = pass == 0 ? SkylineHudStyle.Ink : Violet;
                    p.Stroke();
                }
            }
            else if (_modes[i] == MarkerMode.OffScreen)
            {
                // a chevron pointing right (the element is rotated to point at the asset)
                p.BeginPath();
                p.MoveTo(c + new Vector2(-14f, -17f));
                p.LineTo(c + new Vector2(18f, 0f));
                p.LineTo(c + new Vector2(-14f, 17f));
                p.LineTo(c + new Vector2(-6f, 0f));
                p.ClosePath();
                SkylineHudStyle.FillStroke(p, Violet, SkylineHudStyle.Ink, SkylineHudStyle.KeylinePx);
            }
        }
    }
}
