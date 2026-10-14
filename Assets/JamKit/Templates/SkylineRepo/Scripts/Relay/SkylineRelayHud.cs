using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Relay's HUD pieces (docs/SKYLINE-RELAY.md), in the HUD's style (display font, ink keyline, no boxes) and with no label
    /// text: the timer (big digits, top centre; hostile red and flashing under 15 s), the six station letters floating over their
    /// stations (the current target's bright violet, the others faint), a short bar under the crosshair while the button is held,
    /// a small keycard glyph while the card is in hand, and a soft violet pulse over the screen on each finished step.
    /// </summary>
    public sealed class SkylineRelayHud
    {
        public const float TimerPx = 112f, LetterPx = 54f, WarnSeconds = 15f;
        static readonly Color Violet = new Color32(0xB3, 0x6B, 0xFF, 255);

        VisualElement _timerBox, _hold, _holdFill, _pulse, _card;
        KeylinedLabel _timer;
        readonly KeylinedLabel[] _letters = new KeylinedLabel[6];
        VisualElement _root;
        float _pulseLeft, _flashClock;
        bool _warn, _won;

        public bool Built => _root != null;

        /// <summary>Adds the pieces to the HUD. False when the HUD isn't ready yet.</summary>
        public bool Build(SkylineHud hud)
        {
            if (hud == null || hud.Root == null || hud.Safe == null) return false;
            _root = hud.Root;
            string face = SkylineHudStyle.AmmoFontName;

            // the pulse sits under everything else, over the scene
            _pulse = new VisualElement { pickingMode = PickingMode.Ignore };
            _pulse.style.position = Position.Absolute;
            _pulse.style.left = _pulse.style.top = _pulse.style.right = _pulse.style.bottom = 0;
            _pulse.style.backgroundColor = Violet;
            _pulse.style.opacity = 0f;
            _root.Insert(0, _pulse);

            _timerBox = SkylineHudStyle.Layer(hud.Safe, "relay-timer");
            _timerBox.style.left = 0;
            _timerBox.style.right = 0;
            _timerBox.style.top = 14f;
            _timerBox.style.flexDirection = FlexDirection.Column;
            _timerBox.style.alignItems = Align.Center;
            _timer = new KeylinedLabel(TimerPx, SkylineHudStyle.KeylinePx + 1f, face);
            _timerBox.Add(_timer);

            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.style.width = 64f;
            _card.style.height = 40f;
            _card.style.marginTop = 4f;
            _card.style.backgroundColor = SkylineHudStyle.Paper;
            _card.style.borderTopWidth = _card.style.borderBottomWidth = _card.style.borderLeftWidth = _card.style.borderRightWidth = 3f;
            _card.style.borderTopColor = _card.style.borderBottomColor = _card.style.borderLeftColor = _card.style.borderRightColor = SkylineHudStyle.Ink;
            _card.style.rotate = new Rotate(new Angle(-8f));
            var stripe = new VisualElement { pickingMode = PickingMode.Ignore };
            stripe.style.position = Position.Absolute;
            stripe.style.left = 6f;
            stripe.style.top = 8f;
            stripe.style.width = 36f;
            stripe.style.height = 9f;
            stripe.style.backgroundColor = SkylineHudStyle.Chassis;
            _card.Add(stripe);
            _card.style.display = DisplayStyle.None;
            _timerBox.Add(_card);

            _hold = SkylineHudStyle.Layer(hud.Safe, "relay-hold");
            _hold.style.left = Length.Percent(50);
            _hold.style.top = Length.Percent(57);
            _hold.style.width = 180f;
            _hold.style.height = 16f;
            _hold.style.marginLeft = -90f;
            _hold.style.backgroundColor = SkylineHudStyle.Track;
            _hold.style.borderTopWidth = _hold.style.borderBottomWidth = _hold.style.borderLeftWidth = _hold.style.borderRightWidth = 3f;
            _hold.style.borderTopColor = _hold.style.borderBottomColor = _hold.style.borderLeftColor = _hold.style.borderRightColor = SkylineHudStyle.Ink;
            _holdFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _holdFill.style.height = Length.Percent(100);
            _holdFill.style.width = Length.Percent(0);
            _holdFill.style.backgroundColor = SkylineHudStyle.Paper;
            _hold.Add(_holdFill);
            _hold.style.display = DisplayStyle.None;

            for (int i = 0; i < _letters.Length; i++)
            {
                var l = _letters[i] = new KeylinedLabel(LetterPx, SkylineHudStyle.KeylinePx, face);
                l.SetText(((char)('A' + i)).ToString());
                l.style.position = Position.Absolute;
                l.style.left = 0;
                l.style.top = 0;
                l.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                l.style.display = DisplayStyle.None;
                _root.Add(l);
            }
            return true;
        }

        public void Destroy()
        {
            if (_root == null) return;
            _pulse?.RemoveFromHierarchy();
            _timerBox?.RemoveFromHierarchy();
            _hold?.RemoveFromHierarchy();
            foreach (var l in _letters) l?.RemoveFromHierarchy();
            _root = null;
        }

        // ------------------------------------------------------------------ the timer

        /// <summary>The digits: whole seconds counting down (one decimal in the last ten); hostile red and flashing under
        /// <see cref="WarnSeconds"/>; green once the run is won.</summary>
        public void SetTimer(float remaining, bool won)
        {
            if (_timer == null) return;
            _won = won;
            _warn = !won && remaining < WarnSeconds;
            string s = remaining < 10f && !won ? remaining.ToString("0.0", CultureInfo.InvariantCulture)
                : Mathf.CeilToInt(remaining).ToString(CultureInfo.InvariantCulture);
            if (won) s = remaining.ToString("0.0", CultureInfo.InvariantCulture);
            if (_timer.Text != s) _timer.SetText(s);
            if (won) _timer.SetColor(SkylineHudStyle.Green);
            else if (!_warn) _timer.SetColor(SkylineHudStyle.Paper);
        }

        public void SetCard(bool inHand)
        {
            if (_card != null) _card.style.display = inHand ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The button's hold, 0..1; negative hides the bar.</summary>
        public void SetHold(float t)
        {
            if (_hold == null) return;
            if (t < 0f) { _hold.style.display = DisplayStyle.None; return; }
            _hold.style.display = DisplayStyle.Flex;
            _holdFill.style.width = Length.Percent(Mathf.Clamp01(t) * 100f);
        }

        public void Pulse() => _pulseLeft = 0.45f;

        /// <summary>Per frame (unscaled seconds).</summary>
        public void Tick(float dt)
        {
            if (_pulse == null) return;
            if (_pulseLeft > 0f)
            {
                _pulseLeft = Mathf.Max(0f, _pulseLeft - dt);
                _pulse.style.opacity = 0.22f * (_pulseLeft / 0.45f);
            }
            else _pulse.style.opacity = 0f;
            if (_warn)
            {
                _flashClock += dt;
                bool on = Mathf.Repeat(_flashClock, 0.5f) < 0.3f;
                _timer.SetColor(on ? SkylineHudStyle.Hostile : new Color(SkylineHudStyle.Hostile.r, SkylineHudStyle.Hostile.g, SkylineHudStyle.Hostile.b, 0.4f));
            }
            else _flashClock = 0f;
        }

        // ------------------------------------------------------------------ the letters

        /// <summary>Places the six station letters: <paramref name="points"/> are the world positions over the stations,
        /// <paramref name="current"/> the station to head for (-1 none; its letter is bright violet and larger, the others faint).</summary>
        public void UpdateLetters(Camera cam, Vector3[] points, int current)
        {
            if (_root == null || _root.panel == null || cam == null) return;
            for (int i = 0; i < _letters.Length; i++)
            {
                var l = _letters[i];
                Vector3 vp = cam.WorldToViewportPoint(points[i]);
                if (vp.z <= 0.1f) { l.style.display = DisplayStyle.None; continue; }
                Vector2 pos = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, points[i], cam);
                l.style.display = DisplayStyle.Flex;
                l.style.left = pos.x;
                l.style.top = pos.y;
                bool cur = i == current;
                l.style.opacity = cur ? 1f : 0.3f;
                l.SetColor(cur ? Violet : SkylineHudStyle.Paper);
                float s = cur ? 1.25f : 0.8f;
                l.style.scale = new Scale(new Vector3(s, s, 1f));
            }
        }

        public void HideLetters()
        {
            foreach (var l in _letters) if (l != null) l.style.display = DisplayStyle.None;
        }
    }
}
