using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>The Skyline HUD base (UI Toolkit, one UIDocument; docs/SKYLINE-HUD.md): the crosshair and its coloured hit
    /// marker (white a hit, yellow a weak point, gold a Writ spot, red a kill), the one layered-parallelogram health bar
    /// (<see cref="SkylineHudHealth"/>), the display-font ammo counter (<see cref="SkylineHudAmmo"/>), a wedge pointing to
    /// where the last hit came from, the hurt vignette (and the low-chassis red edge tint), and the movie-style subtitles
    /// (<see cref="SkylineHudSubtitles"/>). No boxes, no rounded corners, no CONCIERGE banner card, no speaker card, no bubbles
    /// above the bots; no damage numbers and no enemy health bars (the creator's rule). H3/H5/C2 add their own elements under
    /// <see cref="Safe"/> and <see cref="Root"/>.
    /// <para>Everything sits inside the screen's safe area and scales with the screen (the panel's scale-with-screen-size, 1920
    /// x 1080 reference).</para></summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SkylineHud : MonoBehaviour
    {
        // Layout (reference 1920 x 1080 panel units).
        const float Margin = 40f;
        const float CrossDot = 4f, MarkerArm = 12f, MarkerThick = 3f, MarkerGap = 7f;
        const float WedgeRadius = 120f, WedgeWidth = 46f, WedgeHeight = 8f;
                // Timing (unscaled seconds).
        const float MarkerTime = 0.18f, KillMarkerTime = 0.32f, WedgeTime = 1.1f, SayTime = SkylineHudSubtitles.DefaultSeconds;
        const float BannerTime = 2.8f, HurtDecay = 2.2f, BreakFlashTime = 0.28f;
        const float LowChassis = 0.35f;

        static readonly Color HurtColor = new Color(0.9f, 0.06f, 0.03f, 1f);

        public static SkylineHud Instance { get; private set; }

        [Tooltip("The JamKit panel settings (theme and font); the builder fills it. Empty: one is made at run time.")]
        public PanelSettings panelSettings;
        public SkylinePlayer player;

        VisualElement _root, _safe, _dot, _marker, _wedgePivot, _wedge;
        VisualElement _vignette, _breakFlash;
        readonly VisualElement[] _arms = new VisualElement[4];
        float _markerLeft, _markerTime, _wedgeLeft;
        float _hurt, _breakLeft;
        Vector3 _hitFrom;
        Rect _safeRect;
        Vector2Int _screen;
        Texture2D _vignetteTex;

        /// <summary>The full-screen HUD layer (under it: the vignette, the flashes, then <see cref="Safe"/>).</summary>
        public VisualElement Root => _root;
        /// <summary>Everything that must stay on screen lives here: it follows the screen's safe area.</summary>
        public VisualElement Safe => _safe;
        public SkylineHudHealth Health { get; } = new SkylineHudHealth();
        public SkylineHudAmmo Ammo { get; } = new SkylineHudAmmo();
        public SkylineHudSubtitles Subtitles { get; } = new SkylineHudSubtitles();
        /// <summary>The objective: job strip, edge markers, tow ring, wave clock (H3). The director binds it with <c>Objective.Bind(repossession, waveClock)</c>.</summary>
        public SkylineHudObjective Objective { get; } = new SkylineHudObjective();
        /// <summary>Comic lettering on kills and certain hits (C2).</summary>
        public SkylineLettering Lettering { get; } = new SkylineLettering();
        /// <summary>The feedback flashes: plating gained (screen edges), special ammo gained (the counter) (H5).</summary>
        public SkylineHudFlashes Flashes { get; } = new SkylineHudFlashes();

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
            Lettering.Unbind();
            Flashes.Unbind();
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
            Objective.BindPlayer(player);
            Lettering.Bind(player);
            Flashes.Bind(player, Ammo);
            SkylineAmmo.Ensure(player);   // the special-ammo economy (A2)
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

            // Full-screen layers first (drawn under the rest): the hurt vignette, the plating-break flash.
            _vignette = Fill(_root);
            _vignetteTex = VignetteTexture(128);
            _vignette.style.backgroundImage = new StyleBackground(_vignetteTex);
            _vignette.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _vignette.style.unityBackgroundImageTintColor = HurtColor;
            _vignette.style.opacity = 0f;
            _breakFlash = Fill(_root);
            _breakFlash.style.backgroundColor = new Color(0.75f, 0.92f, 1f, 1f);
            _breakFlash.style.opacity = 0f;
            Flashes.Build(_root);   // the plating-gained edge flash (H5)

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

            // The objective (job strip, markers, tow ring, wave clock) and the comic lettering (H3, C2).
            Objective.Build(_safe, Margin);
            Lettering.Build(_safe);

            // The subtitles: bottom centre, white text with an ink keyline, NO box, at most two lines (SkylineHudSubtitles).
            Subtitles.Build(_safe);
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

        /// <summary>Shows a subtitle line (the speaker's name a coloured prefix) for a few seconds. The "Subtitles" setting
        /// (<see cref="SkylineSettings.SubtitleMode"/>) decides whether it shows at all.</summary>
        public void Say(Speaker speaker, string text) => Say(speaker, text, SayTime);

        /// <summary>As <see cref="Say(Speaker, string)"/>, shown for <paramref name="seconds"/> (a teaching line's reading time).
        /// At most two lines show; a third pushes the oldest out; lines fade and slide, never pop.</summary>
        public void Say(Speaker speaker, string text, float seconds)
        {
            string name = speaker == Speaker.Concierge ? "CONCIERGE" : speaker.ToString().ToUpperInvariant();
            // CONCIERGE's lines are PA announcements (italics)
            Subtitles.Say(name, text, speaker == Speaker.Concierge, seconds);
        }

        /// <summary>A bot's bark as a subtitle, only in "Dialogue + bot barks" (<paramref name="botName"/> is the prefix).</summary>
        public void SayBark(string botName, string text) => Subtitles.Bark(botName, text, 0f);

        /// <summary>Takes the dialogue subtitles down now (their line ended early); they fade out.</summary>
        public void EndSay() => Subtitles.EndDialogue();

        /// <summary>CONCIERGE's notice (a fight starting): no banner card, just a PA subtitle line.</summary>
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
            Flashes.Tick(dt);

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

            Objective.Tick(dt);
            Lettering.Tick(dt);

            // The subtitles fade and slide; nothing pops.
            Subtitles.Tick(dt);
        }
    }
}
