using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Relay's cable, drawn (docs/SKYLINE-RELAY.md): a chain of thin box segments between two points with a sag that shrinks
    /// as the cable pulls taut (no LineRenderer: its shader would be a variant the build may not carry). Hidden until it is
    /// shown; <see cref="Show"/> each frame it is wanted.
    /// </summary>
    public sealed class SkylineRelayTether
    {
        const int Segments = 18;
        const float Thickness = 0.06f;

        readonly Transform _root;
        readonly Transform[] _seg = new Transform[Segments];
        readonly Vector3[] _pts = new Vector3[Segments + 1];
        readonly Material _mat;
        bool _shown;

        public SkylineRelayTether(Transform parent)
        {
            _mat = SkylineInkRoles.Flat(SkylineRelayProp.Amber, SkylineInkRoles.None) ?? SkylineBotLook.Lit(SkylineRelayProp.Amber);
            _root = new GameObject("Relay Cable").transform;
            _root.SetParent(parent, false);
            for (int i = 0; i < Segments; i++)
            {
                var go = SkylineBotLook.Visual(PrimitiveType.Cube, "Segment " + i, _root, _mat, false);
                _seg[i] = go.transform;
            }
            _root.gameObject.SetActive(false);
        }

        public bool Shown => _shown;

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _root.gameObject.SetActive(false);
        }

        /// <summary>Draws the cable from <paramref name="a"/> to <paramref name="b"/>; <paramref name="length"/> is how long the cable
        /// is (the sag is what it has to spare over the distance).</summary>
        public void Show(Vector3 a, Vector3 b, float length)
        {
            if (!_shown)
            {
                _shown = true;
                _root.gameObject.SetActive(true);
            }
            float dist = Vector3.Distance(a, b);
            float sag = Mathf.Min(1.4f, Mathf.Max(0f, length - dist) * 0.2f);
            float lowest = Mathf.Min(a.y, b.y) - 0.35f;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y = Mathf.Max(lowest, p.y - sag * 4f * t * (1f - t));
                _pts[i] = p;
            }
            for (int i = 0; i < Segments; i++)
            {
                Vector3 d = _pts[i + 1] - _pts[i];
                float len = d.magnitude;
                var tr = _seg[i];
                if (len < 1e-4f) { tr.localScale = Vector3.zero; continue; }
                tr.position = (_pts[i] + _pts[i + 1]) * 0.5f;
                tr.rotation = Quaternion.LookRotation(d / len);
                tr.localScale = new Vector3(Thickness, Thickness, len + 0.01f);
            }
        }

        public void Destroy()
        {
            if (_mat != null) Object.Destroy(_mat);
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }
}
