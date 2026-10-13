using JamKit.Skyline;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>The M1 HUD (UI Toolkit, one UIDocument): the crosshair and its coloured hit marker (white a hit, yellow a weak
    /// point, gold a Writ spot, red a kill), the gun and its magazine, the Unit's own plating and chassis bars (the plating bar
    /// flashes when it breaks), a wedge pointing to where the last hit came from, and the subtitle line for Dot, CONCIERGE and
    /// Nan. No damage numbers and no enemy health bars (the creator's rule).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SkylineHud : MonoBehaviour
    {
        // Layout (reference 1920 x 1080 panel units).
        const float Margin = 40f, BarWidth = 320f, BarHeight = 14f, BarGap = 8f;
        const float CrossDot = 4f, MarkerArm = 12f, MarkerThick = 3f, MarkerGap = 7f;
        const float WedgeRadius = 120f, WedgeWidth = 46f, WedgeHeight = 8f;
        // Timing (unscaled seconds).
        const float MarkerTime = 0.18f, KillMarkerTime = 0.32f, FlashTime = 0.6f, WedgeTime = 1.1f, SayTime = 4f;

        public static SkylineHud Instance { get; private set; }

        [Tooltip("The JamKit panel settings (theme and font); the builder fills it. Empty: one is made at run time.")]
        public PanelSettings panelSettings;
        public SkylinePlayer player;

        VisualElement _root, _marker, _plating, _platingFill, _chassisFill, _wedgePivot, _wedge;
        Label _gun, _ammo, _speaker, _line;
        readonly VisualElement[] _arms = new VisualElement[4];
        float _markerLeft, _markerTime, _flashLeft, _wedgeLeft, _sayLeft;
        Vector3 _hitFrom;

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

        static Label Text(VisualElement parent, int size, Color c)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = c;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginLeft = 0;
            l.style.marginRight = 0;
            l.style.paddingLeft = 0;
            l.style.paddingRight = 0;
            parent.Add(l);
            return l;
        }

        void Build(VisualElement docRoot)
        {
            _root = new VisualElement { name = "skyline-hud", pickingMode = PickingMode.Ignore };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            docRoot.Add(_root);

            // The crosshair and the hit marker, at the centre.
            var centre = new VisualElement { pickingMode = PickingMode.Ignore };
            centre.style.position = Position.Absolute;
            centre.style.left = Length.Percent(50);
            centre.style.top = Length.Percent(50);
            centre.style.width = 0;
            centre.style.height = 0;
            _root.Add(centre);
            Box(centre, -CrossDot * 0.5f, -CrossDot * 0.5f, CrossDot, CrossDot, new Color(1f, 1f, 1f, 0.85f));
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

            // The Unit's plating and chassis, bottom left.
            var armor = new VisualElement { pickingMode = PickingMode.Ignore };
            armor.style.position = Position.Absolute;
            armor.style.left = Margin;
            armor.style.bottom = Margin;
            armor.style.width = BarWidth;
            armor.style.height = BarHeight * 2f + BarGap;
            _root.Add(armor);
            _plating = Box(armor, 0f, 0f, BarWidth, BarHeight, new Color(0f, 0f, 0f, 0.45f));
            _platingFill = Box(_plating, 0f, 0f, BarWidth, BarHeight, new Color(0.55f, 0.85f, 1f, 0.95f));
            var chassis = Box(armor, 0f, BarHeight + BarGap, BarWidth, BarHeight, new Color(0f, 0f, 0f, 0.45f));
            _chassisFill = Box(chassis, 0f, 0f, BarWidth, BarHeight, new Color(1f, 0.62f, 0.25f, 0.95f));

            // The gun and its magazine, bottom right.
            var gunBox = new VisualElement { pickingMode = PickingMode.Ignore };
            gunBox.style.position = Position.Absolute;
            gunBox.style.right = Margin;
            gunBox.style.bottom = Margin;
            gunBox.style.alignItems = Align.FlexEnd;
            _root.Add(gunBox);
            _ammo = Text(gunBox, 44, Color.white);
            _gun = Text(gunBox, 20, new Color(1f, 1f, 1f, 0.75f));

            // Subtitles, bottom centre.
            var say = new VisualElement { pickingMode = PickingMode.Ignore };
            say.style.position = Position.Absolute;
            say.style.left = Length.Percent(20);
            say.style.right = Length.Percent(20);
            say.style.bottom = 150;
            say.style.alignItems = Align.Center;
            _root.Add(say);
            _speaker = Text(say, 22, new Color(1f, 0.8f, 0.35f));
            _line = Text(say, 28, Color.white);
            _line.style.whiteSpace = WhiteSpace.Normal;
            _line.style.unityTextAlign = TextAnchor.MiddleCenter;
            _speaker.style.display = DisplayStyle.None;
            _line.style.display = DisplayStyle.None;
        }

        /// <summary>Shows a subtitle line under the speaker's name for a few seconds.</summary>
        public void Say(Speaker speaker, string text)
        {
            if (_line == null) return;
            _speaker.text = speaker == Speaker.Concierge ? "CONCIERGE" : speaker.ToString();
            _line.text = text ?? "";
            _speaker.style.display = DisplayStyle.Flex;
            _line.style.display = DisplayStyle.Flex;
            _sayLeft = SayTime;
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

        void Update()
        {
            if (_root == null) return;
            float dt = Time.unscaledDeltaTime;

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
                var a = player.Armor;
                if (a != null)
                {
                    _platingFill.style.width = BarWidth * (a.PlatingMax > 0f ? a.Plating / a.PlatingMax : 0f);
                    _chassisFill.style.width = BarWidth * (a.ChassisMax > 0f ? a.Chassis / a.ChassisMax : 0f);
                }
                var lo = player.Loadout;
                if (lo != null)
                {
                    var g = lo.Active;
                    _ammo.text = g.State.IsReloading ? "--" : g.Ammo + " / " + g.Def.weapon.magazineSize;
                    _gun.text = g.Def.displayName.ToUpperInvariant();
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
                _plating.style.backgroundColor = on ? new Color(1f, 0.25f, 0.2f, 0.85f) : new Color(0f, 0f, 0f, 0.45f);
                if (_flashLeft <= 0f) _plating.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            }

            if (_sayLeft > 0f)
            {
                _sayLeft -= dt;
                if (_sayLeft <= 0f)
                {
                    _speaker.style.display = DisplayStyle.None;
                    _line.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
