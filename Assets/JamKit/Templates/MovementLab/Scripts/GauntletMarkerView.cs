using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// A Gauntlet touch marker as you see it (core <c>GauntletCourse.markers[index]</c>): a glowing gold plate that
    /// pulses while it waits, turns teal and settles when you touch it, and goes dull red if you pass it by
    /// (2 s at the finish). <see cref="Show"/> is set by <see cref="GauntletRun"/>.
    /// </summary>
    public sealed class GauntletMarkerView : MonoBehaviour
    {
        public enum Look { Waiting, Touched, Missed }

        public int index;
        public Renderer plate;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int LineId = Shader.PropertyToID("_LineColor");
        static readonly Color Waiting = new Color(1f, 0.78f, 0.3f), WaitingLine = new Color(1f, 0.95f, 0.75f);
        static readonly Color Touched = new Color(0.35f, 0.9f, 0.85f), TouchedLine = new Color(0.8f, 1f, 0.98f);
        static readonly Color Missed = new Color(0.6f, 0.3f, 0.32f), MissedLine = new Color(0.8f, 0.5f, 0.5f);

        MaterialPropertyBlock _block;
        Look _look = Look.Waiting;
        float _pop;
        Vector3 _scale;

        public Look Current => _look;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _scale = transform.localScale;
            Apply();
        }

        public void Show(Look look)
        {
            if (look == _look) return;
            if (look == Look.Touched) _pop = 1f;
            _look = look;
            Apply();
        }

        void Apply()
        {
            if (plate == null || _block == null) return;
            plate.GetPropertyBlock(_block);
            _block.SetColor(ColorId, _look == Look.Touched ? Touched : _look == Look.Missed ? Missed : Waiting);
            _block.SetColor(LineId, _look == Look.Touched ? TouchedLine : _look == Look.Missed ? MissedLine : WaitingLine);
            plate.SetPropertyBlock(_block);
        }

        void Update()
        {
            float k = _look == Look.Waiting ? 1f + 0.06f * Mathf.Sin(Time.time * 5f + index) : 1f;
            _pop = Mathf.MoveTowards(_pop, 0f, Time.deltaTime * 4f);
            float s = k + 0.25f * _pop;
            transform.localScale = new Vector3(_scale.x * s, _scale.y, _scale.z * s);
        }
    }
}
