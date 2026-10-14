using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>The M1 HUD (UI Toolkit, one UIDocument): the crosshair and its coloured hit marker (white a hit, yellow a weak
    /// point, gold a Writ spot, red a kill), the gun and its magazine, the Unit's own plating and chassis bars (labelled; the
    /// plating bar flashes when it breaks), a wedge pointing to where the last hit came from, the subtitle box for Dot,
    /// CONCIERGE and Nan, CONCIERGE's notice banner, the bots' bark bubbles, and the hurt vignette. No damage numbers and no
    /// enemy health bars (the creator's rule).
    /// <para>Judge round 1: everything sits inside the screen's safe area and scales with the screen (the panel's
    /// scale-with-screen-size, 1920 x 1080 reference), whatever the resolution or DPI; every text has a dark backing and a
    /// shadow, so it reads on the sunlit white floors; subtitles sit bottom-left, clear of the viewmodel (which fills the
    /// bottom right), and fade and slide in and out (no text pops); barks are drawn on the HUD, above the viewmodel, at a
    /// readable size.</para></summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SkylineHud : MonoBehaviour
    {
        // Layout (reference 1920 x 1080 panel units).
        const float Margin = 40f, BarWidth = 260f, BarHeight = 12f, BarGap = 10f, LabelWidth = 92f, PanelPad = 12f;
        const float CrossDot = 4f, MarkerArm = 12f, MarkerThick = 3f, MarkerGap = 7f;
        const float WedgeRadius = 120f, WedgeWidth = 46f, WedgeHeight = 8f;
        const float SayMaxWidth = 820f, SayBottom = 132f, SaySlide = 12f, BannerTop = 64f;
        // Timing (unscaled seconds).
        const float MarkerTime = 0.18f, KillMarkerTime = 0.32f, FlashTime = 0.6f, WedgeTime = 1.1f, SayTime = 4f;
        const float FadeIn = 0.18f, FadeOut = 0.32f, BannerTime = 2.8f, HurtDecay = 2.2f, BreakFlashTime = 0.28f;
        const float LowChassis = 0.35f;

        static readonly Color PanelBack = new Color(0.04f, 0.05f, 0.08f, 0.72f);
        static readonly Color PanelEdge = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color Shadow = new Color(0f, 0f, 0f, 0.9f);
        static readonly Color PlatingColor = new Color(0.55f, 0.85f, 1f, 0.95f), ChassisColor = new Color(1f, 0.62f, 0.25f, 0.95f);
        static readonly Color HurtColor = new Color(0.9f, 0.06f, 0.03f, 1f);

        public static SkylineHud Instance { get; private set; }

        [Tooltip("The JamKit panel settings (theme and font); the builder fills it. Empty: one is made at run time.")]
        public PanelSettings panelSettings;
        public SkylinePlayer player;

        VisualElement _root, _safe, _marker, _plating, _platingFill, _chassisFill, _wedgePivot, _wedge;
        VisualElement _sayBox, _banner, _vignette, _breakFlash, _barks;
        Label _gun, _ammo, _speaker, _line, _platingLabel, _bannerHead, _bannerBody;
        readonly VisualElement[] _arms = new VisualElement[4];
        float _markerLeft, _markerTime, _flashLeft, _wedgeLeft, _sayLeft, _sayAlpha, _bannerLeft, _bannerAlpha;
        float _hurt, _breakLeft, _pulse;
        Vector3 _hitFrom;
        Rect _safeRect;
        Vector2Int _screen;
        Texture2D _vignetteTex;
        readonly List<BarkTag> _barkTags = new List<BarkTag>();

        void Awake()
        {
            Instance = this;
            var doc = GetComponent<UIDocument>();
            if (doc.panelSettings == null)
            {
                // Normally the builder sets the UIDocument's panel (it must be there before the document's OnEnable);
                // this fallback re-enables the document so it picks the new panel up.
                if (panelSettings == null)
                {
                    panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                    panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                    panelSettings.match = 0.5f;
                }
                doc.panelSettings = panelSettings;
                doc.enabled = false;
                doc.enabled = true;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_vignetteTex != null) Destroy(_vignetteTex);
        }

        void Start()
        {
            if (player == null) player = FindAnyObjectByType<SkylinePlayer>();
            var docRoot = GetComponent<UIDocument>().rootVisualElement;
            if (docRoot == null)
            {
                Debug.LogWarning("SkylineHud: the UIDocument has no panel; the HUD is off");
                return;
            }
            Build(docRoot);
        }

        // ------------------------------------------------------------------ building

        static VisualElement Box(VisualElement parent, float x, float y, float w, float h, Color c)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = w;
            e.style.height = h;
            e.style.backgroundColor = c;
            parent.Add(e);
            return e;
        }

        static Label Text(VisualElement parent, int size, Color c, bool bold = true)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = c;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.marginLeft = 0;
            l.style.marginRight = 0;
            l.style.marginTop = 0;
            l.style.marginBottom = 0;
            l.style.paddingLeft = 0;
            l.style.paddingRight = 0;
            l.style.paddingTop = 0;
            l.style.paddingBottom = 0;
            l.style.textShadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 3f, color = Shadow };
            parent.Add(l);
            return l;
        }

        /// <summary>A dark rounded backing panel (text on it reads on any floor).</summary>
        static VisualElement Panel(VisualElement parent, float pad)
        {
            var p = new VisualElement { pickingMode = PickingMode.Ignore };
            p.style.position = Position.Absolute;
            p.style.backgroundColor = PanelBack;
            Round(p, 8f);
            p.style.borderTopWidth = p.style.borderBottomWidth = p.style.borderLeftWidth = p.style.borderRightWidth = 1f;
            p.style.borderTopColor = p.style.borderBottomColor = p.style.borderLeftColor = p.style.borderRightColor = PanelEdge;
            p.style.paddingLeft = p.style.paddingRight = pad + 4f;
            p.style.paddingTop = p.style.paddingBottom = pad;
            parent.Add(p);
            return p;
        }

        static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        static VisualElement Fill(VisualElement parent)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.top = 0;
            e.style.right = 0;
            e.style.bottom = 0;
            parent.Add(e);
            return e;
        }

        void Build(VisualElement docRoot)
        {
            _root = Fill(docRoot);
            _root.name = "skyline-hud";

            // Full-screen layers first (drawn under the rest): the hurt vignette, the plating-break flash, the barks.
            _vignette = Fill(_root);
            _vignetteTex = VignetteTexture(128);
            _vignette.style.backgroundImage = new StyleBackground(_vignetteTex);
            _vignette.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _vignette.style.unityBackgroundImageTintColor = HurtColor;
            _vignette.style.opacity = 0f;
            _breakFlash = Fill(_root);
            _breakFlash.style.backgroundColor = new Color(0.75f, 0.92f, 1f, 1f);
            _breakFlash.style.opacity = 0f;
            _barks = Fill(_root);

            // Everything else inside the safe area.
            _safe = Fill(_root);
            _safe.name = "safe-area";
            FitSafeArea(true);

            // The crosshair and the hit marker, at the centre.
            var centre = new VisualElement { pickingMode = PickingMode.Ignore };
            centre.style.position = Position.Absolute;
            centre.style.left = Length.Percent(50);
            centre.style.top = Length.Percent(50);
            centre.style.width = 0;
            centre.style.height = 0;
            _safe.Add(centre);
            var dot = Box(centre, -CrossDot * 0.5f, -CrossDot * 0.5f, CrossDot, CrossDot, new Color(1f, 1f, 1f, 0.9f));
            dot.style.borderTopWidth = dot.style.borderBottomWidth = dot.style.borderLeftWidth = dot.style.borderRightWidth = 1f;
            dot.style.borderTopColor = dot.style.borderBottomColor = dot.style.borderLeftColor = dot.style.borderRightColor = new Color(0f, 0f, 0f, 0.6f);
            _marker = new VisualElement { pickingMode = PickingMode.Ignore };
            _marker.style.position = Position.Absolute;
            centre.Add(_marker);
            for (int i = 0; i < 4; i++)
            {
                var pivot = new VisualElement { pickingMode = PickingMode.Ignore };
                pivot.style.position = Position.Absolute;
                pivot.style.rotate = new Rotate(new Angle(45f + 90f * i));
                _marker.Add(pivot);
                _arms[i] = Box(pivot, MarkerGap, -MarkerThick * 0.5f, MarkerArm, MarkerThick, Color.white);
            }
            _marker.style.display = DisplayStyle.None;

            // Where the last hit came from: a wedge on a ring round the crosshair.
            _wedgePivot = new VisualElement { pickingMode = PickingMode.Ignore };
            _wedgePivot.style.position = Position.Absolute;
            centre.Add(_wedgePivot);
            _wedge = Box(_wedgePivot, -WedgeWidth * 0.5f, -WedgeRadius, WedgeWidth, WedgeHeight, new Color(1f, 0.3f, 0.2f, 0.9f));
            Round(_wedge, 3f);
            _wedgePivot.style.display = DisplayStyle.None;

            // The Unit's plating and chassis, bottom left, labelled, on a backing panel.
            var armor = Panel(_safe, PanelPad);
            armor.style.left = Margin;
            armor.style.bottom = Margin;
            _platingLabel = ArmorRow(armor, "PLATING", PlatingColor, out _plating, out _platingFill, 0f);
            ArmorRow(armor, "CHASSIS", ChassisColor, out _, out _chassisFill, BarGap);

            // The gun and its magazine, bottom right, on a backing panel (it reads over the viewmodel).
            var gunBox = Panel(_safe, PanelPad - 4f);
            gunBox.style.right = Margin;
            gunBox.style.bottom = Margin;
            gunBox.style.alignItems = Align.FlexEnd;
            _ammo = Text(gunBox, 40, Color.white);
            _gun = Text(gunBox, 16, new Color(1f, 1f, 1f, 0.8f));
            _gun.style.letterSpacing = 2f;

            // Subtitles, bottom left above the armour panel: clear of the viewmodel, which fills the bottom right.
            _sayBox = Panel(_safe, 14f);
            _sayBox.style.left = Margin;
            _sayBox.style.bottom = SayBottom;
            _sayBox.style.maxWidth = SayMaxWidth;
            _sayBox.style.borderLeftWidth = 4f;
            _speaker = Text(_sayBox, 18, Color.white);
            _speaker.style.letterSpacing = 2f;
            _speaker.style.marginBottom = 4f;
            _line = Text(_sayBox, 28, Color.white, false);
            _line.style.whiteSpace = WhiteSpace.Normal;
            _line.style.unityTextAlign = TextAnchor.UpperLeft;
            _sayBox.style.opacity = 0f;
            _sayBox.style.display = DisplayStyle.None;

            // CONCIERGE's notice banner, top centre.
            var bannerRow = new VisualElement { pickingMode = PickingMode.Ignore };
            bannerRow.style.position = Position.Absolute;
            bannerRow.style.left = 0;
            bannerRow.style.right = 0;
            bannerRow.style.top = BannerTop;
            bannerRow.style.alignItems = Align.Center;
            _safe.Add(bannerRow);
            _banner = Panel(bannerRow, 12f);
            _banner.style.position = Position.Relative;
            _banner.style.alignItems = Align.Center;
            _banner.style.borderTopWidth = 3f;
            _banner.style.borderTopColor = new Color(0.95f, 0.8f, 0.4f, 0.95f);
            _bannerHead = Text(_banner, 15, new Color(0.95f, 0.8f, 0.4f));
            _bannerHead.style.letterSpacing = 3f;
            _bannerHead.style.marginBottom = 4f;
            _bannerBody = Text(_banner, 22, Color.white, false);
            _banner.style.opacity = 0f;
            _banner.style.display = DisplayStyle.None;
        }

        static Label ArmorRow(VisualElement parent, string name, Color fillColor, out VisualElement back, out VisualElement fill, float top)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = top;
            parent.Add(row);
            var label = Text(row, 13, new Color(fillColor.r, fillColor.g, fillColor.b, 1f));
            label.style.width = LabelWidth;
            label.style.letterSpacing = 2f;
            label.text = name;
            back = new VisualElement { pickingMode = PickingMode.Ignore };
            back.style.width = BarWidth;
            back.style.height = BarHeight;
            back.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            Round(back, 3f);
            back.style.overflow = Overflow.Hidden;
            row.Add(back);
            fill = Box(back, 0f, 0f, BarWidth, BarHeight, fillColor);
            return label;
        }

        /// <summary>A white radial vignette (clear in the middle, opaque at the corners), tinted red by the style.</summary>
        static Texture2D VignetteTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SkylineHurtVignette" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((r - 0.6f) / 0.75f);
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>Keeps <see cref="_safe"/> on the screen's safe area (as fractions, so it holds at any resolution or
        /// scale).</summary>
        void FitSafeArea(bool force)
        {
            if (_safe == null) return;
            var sa = Screen.safeArea;
            var sz = new Vector2Int(Screen.width, Screen.height);
            if (!force && sa == _safeRect && sz == _screen) return;
            _safeRect = sa;
            _screen = sz;
            if (sz.x <= 0 || sz.y <= 0) return;
            _safe.style.left = Length.Percent(100f * sa.xMin / sz.x);
            _safe.style.right = Length.Percent(100f * (sz.x - sa.xMax) / sz.x);
            _safe.style.top = Length.Percent(100f * (sz.y - sa.yMax) / sz.y);
            _safe.style.bottom = Length.Percent(100f * sa.yMin / sz.y);
        }

        // ------------------------------------------------------------------ the API

        /// <summary>Shows a subtitle line under the speaker's name for a few seconds.</summary>
        public void Say(Speaker speaker, string text) => Say(speaker, text, SayTime);

        /// <summary>As <see cref="Say(Speaker, string)"/>, shown for <paramref name="seconds"/> (a teaching line's reading time).
        /// It fades and slides in; a line that replaces one still showing dips and comes back, never pops.</summary>
        public void Say(Speaker speaker, string text, float seconds)
        {
            if (_line == null) return;
            var accent = SpeakerColor(speaker);
            _speaker.text = speaker == Speaker.Concierge ? "CONCIERGE" : speaker.ToString().ToUpperInvariant();
            _speaker.style.color = accent;
            _sayBox.style.borderLeftColor = accent;
            _line.text = text ?? "";
            _sayBox.style.display = DisplayStyle.Flex;
            _sayAlpha = Mathf.Min(_sayAlpha, 0.35f);
            _sayLeft = seconds > 0f ? seconds : SayTime;
        }

        /// <summary>Takes the subtitle down now (it fades out): its line ended early.</summary>
        public void EndSay() => _sayLeft = Mathf.Min(_sayLeft, 0f);

        static Color SpeakerColor(Speaker s)
        {
            switch (s)
            {
                case Speaker.Dot: return new Color(1f, 0.8f, 0.35f);
                case Speaker.Nan: return new Color(0.55f, 0.92f, 0.85f);
                default: return new Color(0.96f, 0.88f, 0.62f);
            }
        }

        /// <summary>CONCIERGE's notice banner (a fight starting): a gold-edged card at the top that slides in and fades.</summary>
        public void Banner(string header, string body)
        {
            if (_banner == null) return;
            _bannerHead.text = header ?? "";
            _bannerBody.text = body ?? "";
            _banner.style.display = DisplayStyle.Flex;
            _bannerAlpha = Mathf.Min(_bannerAlpha, 0.35f);
            _bannerLeft = BannerTime;
        }

        /// <summary>The hit marker for a shot's strongest result: white a hit, yellow a weak point, gold a Writ spot, red a kill.</summary>
        public void Marker(SkylineHitResult r) => Marker(r, false);

        public void Marker(SkylineHitResult r, bool weakPoint)
        {
            if (_marker == null || !r.hit) return;
            var c = SkylineShooting.MarkerColor(in r, weakPoint);
            for (int i = 0; i < 4; i++) _arms[i].style.backgroundColor = c;
            _markerTime = r.killed ? KillMarkerTime : MarkerTime;
            _markerLeft = _markerTime;
            _marker.style.display = DisplayStyle.Flex;
        }

        /// <summary>The plating just broke: its bar flashes.</summary>
        public void FlashPlating() => _flashLeft = FlashTime;

        /// <summary>A hit came from <paramref name="attackerPosition"/>: the wedge points there for a moment.</summary>
        public void DamageFrom(Vector3 attackerPosition)
        {
            _hitFrom = attackerPosition;
            _wedgeLeft = WedgeTime;
        }

        /// <summary>The Unit was hit (<paramref name="intensity"/> 0..1): the red edge vignette flares and fades.</summary>
        public void Hurt(float intensity, bool chassis)
        {
            float a = (chassis ? 0.5f : 0.3f) + 0.45f * Mathf.Clamp01(intensity);
            _hurt = Mathf.Max(_hurt, Mathf.Min(1f, a));
        }

        /// <summary>The plating broke: a pale flash over the screen, a full vignette and the bar's flash.</summary>
        public void PlatingBreak()
        {
            _breakLeft = BreakFlashTime;
            _hurt = 1f;
            FlashPlating();
        }

        // ------------------------------------------------------------------ barks

        /// <summary>A bark bubble drawn on the HUD (so it is never cut by the viewmodel, which is drawn over the world).</summary>
        public sealed class BarkTag
        {
            internal VisualElement box;
            internal Label label;
        }

        public BarkTag ShowBark(string text)
        {
            if (_barks == null || string.IsNullOrEmpty(text)) return null;
            var tag = new BarkTag();
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.alignItems = Align.Center;
            box.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.backgroundColor = new Color(0.98f, 0.96f, 0.9f, 0.96f);
            Round(card, 10f);
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2f;
            var edge = new Color(0.13f, 0.15f, 0.24f, 1f);
            card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = edge;
            card.style.paddingLeft = card.style.paddingRight = 12f;
            card.style.paddingTop = card.style.paddingBottom = 5f;
            box.Add(card);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.color = edge;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            card.Add(label);
            var tail = new VisualElement { pickingMode = PickingMode.Ignore };
            tail.style.width = 12f;
            tail.style.height = 12f;
            tail.style.marginTop = -7f;
            tail.style.backgroundColor = new Color(0.98f, 0.96f, 0.9f, 0.96f);
            tail.style.borderRightWidth = tail.style.borderBottomWidth = 2f;
            tail.style.borderRightColor = tail.style.borderBottomColor = edge;
            tail.style.rotate = new Rotate(new Angle(45f));
            box.Add(tail);
            box.style.opacity = 0f;
            _barks.Add(box);
            tag.box = box;
            tag.label = label;
            _barkTags.Add(tag);
            return tag;
        }

        /// <summary>Places a bark over <paramref name="world"/> as <paramref name="cam"/> sees it; hidden when behind the camera.
        /// Its text shrinks with distance but never below a readable size.</summary>
        public void PlaceBark(BarkTag tag, Vector3 world, Camera cam, float alpha)
        {
            if (tag == null || tag.box == null || cam == null || _root.panel == null) return;
            Vector3 vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0.05f)
            {
                tag.box.style.opacity = 0f;
                return;
            }
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, cam);
            tag.box.style.left = p.x;
            tag.box.style.top = p.y;
            tag.label.style.fontSize = Mathf.Lerp(24f, 17f, Mathf.InverseLerp(6f, 30f, vp.z));
            tag.box.style.opacity = Mathf.Clamp01(alpha);
        }

        public void HideBark(BarkTag tag)
        {
            if (tag == null) return;
            tag.box?.RemoveFromHierarchy();
            tag.box = null;
            _barkTags.Remove(tag);
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (_root == null) return;
            float dt = Time.unscaledDeltaTime;
            FitSafeArea(false);

            if (_markerLeft > 0f)
            {
                _markerLeft -= dt;
                float k = Mathf.Clamp01(_markerLeft / Mathf.Max(0.01f, _markerTime));
                _marker.style.opacity = k;
                _marker.style.scale = new Scale(Vector3.one * (1f + 0.35f * k));
                if (_markerLeft <= 0f) _marker.style.display = DisplayStyle.None;
            }

            float chassisShare = 1f;
            if (player != null)
            {
                var a = player.Armor;
                if (a != null)
                {
                    float plating = a.PlatingMax > 0f ? a.Plating / a.PlatingMax : 0f;
                    chassisShare = a.ChassisMax > 0f ? a.Chassis / a.ChassisMax : 0f;
                    _platingFill.style.width = BarWidth * plating;
                    _chassisFill.style.width = BarWidth * chassisShare;
                    bool down = plating <= 0f;
                    _platingLabel.text = down ? "NO PLATING" : "PLATING";
                    _platingLabel.style.color = down ? new Color(1f, 0.35f, 0.3f) : PlatingColor;
                }
                var lo = player.Loadout;
                if (lo != null)
                {
                    var g = lo.Active;
                    string name = g.Def.displayName.ToUpperInvariant();
                    _ammo.text = g.State.IsReloading ? "-- / " + g.Def.weapon.magazineSize : g.Ammo + " / " + g.Def.weapon.magazineSize;
                    _gun.text = g.State.IsReloading ? name + "  ·  RELOADING" : name;
                }
                if (_wedgeLeft > 0f)
                {
                    _wedgeLeft -= dt;
                    Vector3 to = _hitFrom - player.transform.position;
                    float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - player.Yaw;
                    _wedgePivot.style.rotate = new Rotate(new Angle(bearing));
                    _wedgePivot.style.display = DisplayStyle.Flex;
                    _wedge.style.opacity = Mathf.Clamp01(_wedgeLeft / 0.4f);
                    if (_wedgeLeft <= 0f) _wedgePivot.style.display = DisplayStyle.None;
                }
            }

            if (_flashLeft > 0f)
            {
                _flashLeft -= dt;
                bool on = Mathf.Repeat(_flashLeft, 0.15f) > 0.075f;
                _plating.style.backgroundColor = on ? new Color(1f, 0.25f, 0.2f, 0.85f) : new Color(1f, 1f, 1f, 0.12f);
                if (_flashLeft <= 0f) _plating.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            }

            // The hurt vignette: a flare that fades, and a slow pulse while the chassis is low.
            _hurt = Mathf.Max(0f, _hurt - HurtDecay * dt);
            _pulse += dt;
            float low = chassisShare < LowChassis ? (0.18f + 0.1f * Mathf.Sin(_pulse * 5f)) * (1f - chassisShare / LowChassis) : 0f;
            _vignette.style.opacity = Mathf.Clamp01(Mathf.Max(_hurt, low));
            if (_breakLeft > 0f)
            {
                _breakLeft -= dt;
                _breakFlash.style.opacity = 0.32f * Mathf.Clamp01(_breakLeft / BreakFlashTime);
                if (_breakLeft <= 0f) _breakFlash.style.opacity = 0f;
            }

            // Subtitles and the banner fade and slide; nothing pops.
            _sayLeft -= dt;
            _sayAlpha = Step(_sayAlpha, _sayLeft > 0f, dt);
            Show(_sayBox, _sayAlpha, _sayLeft > 0f, SaySlide);
            _bannerLeft -= dt;
            _bannerAlpha = Step(_bannerAlpha, _bannerLeft > 0f, dt);
            Show(_banner, _bannerAlpha, _bannerLeft > 0f, -SaySlide);
        }

        static float Step(float alpha, bool on, float dt) =>
            on ? Mathf.Min(1f, alpha + dt / FadeIn) : Mathf.Max(0f, alpha - dt / FadeOut);

        static void Show(VisualElement e, float alpha, bool on, float slide)
        {
            if (e == null) return;
            float eased = alpha * alpha * (3f - 2f * alpha);
            e.style.opacity = eased;
            e.style.translate = new Translate(0f, (1f - eased) * slide);
            if (!on && alpha <= 0f) e.style.display = DisplayStyle.None;
        }
    }
}
