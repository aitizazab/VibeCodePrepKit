using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>The Skyline HUD base (UI Toolkit, one UIDocument; docs/SKYLINE-HUD.md): the crosshair and its coloured hit
    /// marker (white a hit, yellow a weak point, gold a Writ spot, red a kill), the one layered-parallelogram health bar
    /// (<see cref="SkylineHudHealth"/>), the display-font ammo counter (<see cref="SkylineHudAmmo"/>), a wedge pointing to
    /// where the last hit came from, the hurt vignette (and the low-chassis red edge tint), and an interim unboxed subtitle
    /// line. No boxes, no rounded corners, no CONCIERGE banner card, no speaker card; no damage numbers and no enemy health
    /// bars (the creator's rule). H3/H4/H5/C2 add their own elements under <see cref="Safe"/> and <see cref="Root"/>.
    /// <para>Everything sits inside the screen's safe area and scales with the screen (the panel's scale-with-screen-size, 1920
    /// x 1080 reference). Barks are still drawn here until H4 removes them.</para></summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SkylineHud : MonoBehaviour
    {
        // Layout (reference 1920 x 1080 panel units).
        const float Margin = 40f;
        const float CrossDot = 4f, MarkerArm = 12f, MarkerThick = 3f, MarkerGap = 7f;
        const float WedgeRadius = 120f, WedgeWidth = 46f, WedgeHeight = 8f;
        const float SayBottom = 120f, SaySlide = 12f, SaySizePx = 34f;
        // Timing (unscaled seconds).
        const float MarkerTime = 0.18f, KillMarkerTime = 0.32f, WedgeTime = 1.1f, SayTime = 4f;
        const float FadeIn = 0.18f, FadeOut = 0.32f, BannerTime = 2.8f, HurtDecay = 2.2f, BreakFlashTime = 0.28f;
        const float LowChassis = 0.35f;

        static readonly Color HurtColor = new Color(0.9f, 0.06f, 0.03f, 1f);

        public static SkylineHud Instance { get; private set; }

        [Tooltip("The JamKit panel settings (theme and font); the builder fills it. Empty: one is made at run time.")]
        public PanelSettings panelSettings;
        public SkylinePlayer player;

        VisualElement _root, _safe, _dot, _marker, _wedgePivot, _wedge;
        VisualElement _sayBox, _vignette, _breakFlash, _barks;
        KeylinedLabel _say;
        readonly VisualElement[] _arms = new VisualElement[4];
        float _markerLeft, _markerTime, _wedgeLeft, _sayLeft, _sayAlpha;
        float _hurt, _breakLeft;
        Vector3 _hitFrom;
        Rect _safeRect;
        Vector2Int _screen;
        Texture2D _vignetteTex;
        readonly List<BarkTag> _barkTags = new List<BarkTag>();

        /// <summary>The full-screen HUD layer (under it: the vignette, the flashes, the barks, then <see cref="Safe"/>).</summary>
        public VisualElement Root => _root;
        /// <summary>Everything that must stay on screen lives here: it follows the screen's safe area.</summary>
        public VisualElement Safe => _safe;
        public SkylineHudHealth Health { get; } = new SkylineHudHealth();
        public SkylineHudAmmo Ammo { get; } = new SkylineHudAmmo();

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
            BuildOn(docRoot);
            Health.Bind(player);
            Ammo.Bind(player);
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

        /// <summary>Only the bark bubbles still round (H4 removes them).</summary>
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

        /// <summary>Builds the HUD under <paramref name="docRoot"/> (the UIDocument's root, or any element: the EditMode tests
        /// build it on a bare one). Call <see cref="Health"/>.Bind and <see cref="Ammo"/>.Bind afterwards.</summary>
        public void BuildOn(VisualElement docRoot)
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
            var dot = _dot = Box(centre, -CrossDot * 0.5f, -CrossDot * 0.5f, CrossDot, CrossDot, new Color(1f, 1f, 1f, 0.9f));
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
            _wedgePivot.style.display = DisplayStyle.None;

            // The one health bar, bottom left; the ammo counter, bottom right.
            Health.Build(_safe, Margin);
            Ammo.Build(_safe, Margin);

            // The interim subtitle: bottom centre, white text with an ink keyline, NO box, italic for CONCIERGE's PA lines.
            // H4 replaces it with SkylineHudSubtitles (two lines, the size setting, the barks filter).
            _sayBox = SkylineHudStyle.Layer(_safe, "hud-say");
            _sayBox.style.left = Length.Percent(15);
            _sayBox.style.right = Length.Percent(15);
            _sayBox.style.bottom = SayBottom;
            _sayBox.style.alignItems = Align.Center;
            _say = new KeylinedLabel(SaySizePx, 2f, null, true);
            _say.style.width = Length.Percent(100);
            _say.SetWrap(true, TextAnchor.LowerCenter);
            _sayBox.Add(_say);
            _sayBox.style.opacity = 0f;
            _sayBox.style.display = DisplayStyle.None;
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

        /// <summary>Shows a subtitle line (the speaker's name a coloured prefix) for a few seconds.</summary>
        public void Say(Speaker speaker, string text) => Say(speaker, text, SayTime);

        /// <summary>As <see cref="Say(Speaker, string)"/>, shown for <paramref name="seconds"/> (a teaching line's reading time).
        /// It fades and slides in; a line that replaces one still showing dips and comes back, never pops.</summary>
        public void Say(Speaker speaker, string text, float seconds)
        {
            if (_say == null) return;
            string name = speaker == Speaker.Concierge ? "CONCIERGE" : speaker.ToString().ToUpperInvariant();
            var (r, g, b) = SpeakerColours.Of(name);
            string hex = ColorUtility.ToHtmlStringRGB(new Color(r, g, b));
            string body = (text ?? "").Replace("<", "");
            _say.SetItalic(speaker == Speaker.Concierge);   // CONCIERGE's lines are PA announcements
            _say.SetText("<color=#" + hex + ">" + name + ":</color> " + body, name + ": " + body);
            _sayBox.style.display = DisplayStyle.Flex;
            _sayAlpha = Mathf.Min(_sayAlpha, 0.35f);
            _sayLeft = seconds > 0f ? seconds : SayTime;
        }

        /// <summary>Takes the subtitle down now (it fades out): its line ended early.</summary>
        public void EndSay() => _sayLeft = Mathf.Min(_sayLeft, 0f);

        /// <summary>CONCIERGE's notice (a fight starting): no banner card any more, just a PA subtitle line (H4 owns subtitles).</summary>
        public void Banner(string header, string body) => Say(Speaker.Concierge, body, BannerTime);

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

        /// <summary>The plating just broke: the bar flashes.</summary>
        public void FlashPlating() => Health.Flash();

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

            if (player != null)
            {
                // Aim down sights: for the Writ the sights are the crosshair, so the dot fades out as they come up (the Breacher
                // keeps its dot; the hit marker stays for both).
                if (_dot != null && player.Loadout != null)
                {
                    bool sights = player.Loadout.Active.Def.id == SkylineGuns.WritId;
                    _dot.style.opacity = sights ? 1f - player.AimEased : 1f;
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

            Health.Tick(dt);
            Ammo.Tick(dt);

            // The hurt vignette: a flare that fades, and the red edge tint that follows the low-chassis pulse (HealthBarModel).
            _hurt = Mathf.Max(0f, _hurt - HurtDecay * dt);
            float chassisShare = Health.ChassisFrac;
            float low = chassisShare < LowChassis ? (0.12f + 0.2f * Health.EdgeTint) * (1f - chassisShare / LowChassis) : 0f;
            _vignette.style.opacity = Mathf.Clamp01(Mathf.Max(_hurt, low));
            if (_breakLeft > 0f)
            {
                _breakLeft -= dt;
                _breakFlash.style.opacity = 0.32f * Mathf.Clamp01(_breakLeft / BreakFlashTime);
                if (_breakLeft <= 0f) _breakFlash.style.opacity = 0f;
            }

            // The subtitle fades and slides; nothing pops.
            _sayLeft -= dt;
            _sayAlpha = Step(_sayAlpha, _sayLeft > 0f, dt);
            Show(_sayBox, _sayAlpha, _sayLeft > 0f, SaySlide);
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
