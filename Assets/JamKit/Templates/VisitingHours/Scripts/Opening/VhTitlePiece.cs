using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>What a thing on the title table does when it's chosen.</summary>
    public enum VhTitleAction { None, Play, Quit, Settings, Credits }

    /// <summary>
    /// One thing on the title table (the tablet, the mug, the notepad, the pen, the badge, the table
    /// itself): it fades in (dithered, so it stays solid and casts its shadow as it comes), and when it
    /// can be chosen it glows and lifts a little while it's highlighted. Pieces with the same
    /// <see cref="group"/> highlight together (the pen goes with the notepad).
    /// </summary>
    public sealed class VhTitlePiece : MonoBehaviour
    {
        public string id = "";
        public VhTitleAction action;
        [Tooltip("Pieces in one group are one choice (the pen and the notepad are Credits).")]
        public string group = "";
        [Tooltip("How far it lifts when highlighted (metres).")]
        public float lift = 0.012f;
        [Tooltip("A small tilt when highlighted (degrees around its own X).")]
        public float tilt = 2.5f;
        public Renderer[] renderers = new Renderer[0];

        Vector3 _basePos;
        Quaternion _baseRot;
        bool _based;
        float _fade = 1f, _fadeTarget = 1f, _fadeSpeed = 1f;
        float _glow, _glowTarget;
        MaterialPropertyBlock _block;

        public string Group => string.IsNullOrEmpty(group) ? id : group;
        public float Fade => _fade;
        public bool Highlighted => _glowTarget > 0f;
        public float Glow => _glow;
        public bool Visible => _fade > 0.999f;

        void Capture()
        {
            if (_based) return;
            _based = true;
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
        }

        /// <summary>Fades to <paramref name="target"/> (0 hidden, 1 shown) over <paramref name="seconds"/> (0 = at once).</summary>
        /// <summary>The piece was moved (the opening editor): where it is now is where it rests and lifts from.</summary>
        public void Rebase()
        {
            _based = true;
            _glow = _glowTarget = 0f;
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
        }

        public void FadeTo(float target, float seconds)
        {
            _fadeTarget = Mathf.Clamp01(target);
            if (seconds <= 0f) _fade = _fadeTarget;
            else _fadeSpeed = 1f / seconds;
            Apply();
        }

        public void SetHighlight(bool on, bool instant = false)
        {
            _glowTarget = on ? 1f : 0f;
            if (instant) _glow = _glowTarget;
        }

        public void Tick(float dt)
        {
            Capture();
            _fade = Mathf.MoveTowards(_fade, _fadeTarget, dt * _fadeSpeed);
            _glow = Mathf.MoveTowards(_glow, _glowTarget, dt * 5f);
            float e = VhUtil.Ease(_glow);
            transform.localPosition = _basePos + Vector3.up * (lift * e);
            transform.localRotation = _baseRot * Quaternion.Euler(-tilt * e, 0f, 0f);
            Apply();
        }

        void Apply()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            float fade = VhUtil.Ease(_fade);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(VhWakingStyle.FadeId, fade);
                _block.SetFloat(VhWakingStyle.HighlightId, _glow * 0.55f);
                r.SetPropertyBlock(_block);
                bool on = fade > 0.001f;
                if (r.enabled != on) r.enabled = on;
            }
        }
    }
}
