using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// One piece of the dream street (core <c>DreamEscapeCourse.course.pieces[index]</c>), built by the
    /// DreamEscapeBuilder: a root at the piece's centre with its box and decoration as children. Pieces in a
    /// collapse go when the collapse's wave reaches them (<see cref="delay"/> after it fires): fences fold flat
    /// like felled dominoes, house fronts peel off like paper (and show nothing behind them), the rest drops
    /// away. Respawning before the collapse puts them back.
    /// </summary>
    public sealed class DreamPieceView : MonoBehaviour
    {
        public int index;
        public int collapse = -1;
        public DreamCollapseStyle style;
        [Tooltip("Seconds after the collapse fires before this piece goes (the wave reaching it).")]
        public float delay;
        [Tooltip("Half the piece's size: the pivot sits on its bottom edge.")]
        public Vector3 halfSize = Vector3.one * 0.5f;
        [Tooltip("Fold: the axis it folds about. Peel: the side it falls to (+1 away to +X, -1 to -X).")]
        public Vector3 foldAxis = Vector3.right;
        public float peelSide = -1f;

        Vector3 _home;
        Quaternion _homeRot;
        Vector3 _homeScale;
        bool _homeSet;
        float _t = -1f;
        Vector3 _spin;
        Collider[] _cols;

        public bool Collapsed => _t >= 0f;
        /// <summary>True once the piece has started to move (its wave reached it).</summary>
        public bool Moving => _t >= delay;

        void Awake() => Remember();

        void Remember()
        {
            if (_homeSet) return;
            _homeSet = true;
            _home = transform.position;
            _homeRot = transform.rotation;
            _homeScale = transform.localScale;
            _cols = GetComponentsInChildren<Collider>(true);
        }

        /// <summary>Starts the collapse; <paramref name="instant"/> puts it straight in its end state (a respawn past it).</summary>
        public void Collapse(bool instant = false)
        {
            Remember();
            if (_t >= 0f && !instant) return;
            _t = instant ? delay + 10f : 0f;
            foreach (var c in _cols) if (c != null) c.enabled = false;
            var r = new System.Random(index * 7919 + 13);
            _spin = new Vector3((float)r.NextDouble() * 40f - 20f, (float)r.NextDouble() * 20f - 10f, (float)r.NextDouble() * 40f - 20f);
            if (instant) Pose(10f);
        }

        public void Restore()
        {
            Remember();
            _t = -1f;
            transform.SetPositionAndRotation(_home, _homeRot);
            transform.localScale = _homeScale;
            gameObject.SetActive(true);
            foreach (var c in _cols) if (c != null) c.enabled = true;
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            Pose(_t - delay);
        }

        void Pose(float t)
        {
            if (t < 0f) return;
            Vector3 pivot = _home + Vector3.down * halfSize.y;
            switch (style)
            {
                case DreamCollapseStyle.FoldFlat:
                {
                    // Fall over about the bottom edge with a little bounce at the end, like a felled fence.
                    float k = Mathf.Clamp01(t / 0.45f);
                    float ease = k * k * (3f - 2f * k);
                    float bounce = k >= 1f ? Mathf.Sin(Mathf.Min(1f, (t - 0.45f) / 0.25f) * Mathf.PI) * 0.06f : 0f;
                    var rot = Quaternion.AngleAxis(86f * (ease - bounce), foldAxis);
                    transform.SetPositionAndRotation(pivot + rot * (_home - pivot), rot * _homeRot);
                    break;
                }
                case DreamCollapseStyle.Peel:
                {
                    // Peel off like a sheet of paper: a slow lean, then it flops over and drifts down, curling,
                    // thinning to nothing.
                    float lean = Mathf.Clamp01(t / 1.4f);
                    float angle = 105f * lean * lean;
                    float twist = 14f * Mathf.Sin(t * 1.7f) * lean;
                    var rot = Quaternion.AngleAxis(twist, Vector3.up) * Quaternion.AngleAxis(-peelSide * angle, Vector3.forward);
                    Vector3 drift = new Vector3(peelSide * 1.5f * lean * lean, -Mathf.Max(0f, t - 1f) * 3f * Mathf.Max(0f, t - 1f), 0f);
                    transform.SetPositionAndRotation(pivot + rot * (_home - pivot) + drift, rot * _homeRot);
                    float thin = 1f - Mathf.Clamp01((t - 1.6f) / 1.2f);
                    transform.localScale = new Vector3(_homeScale.x * Mathf.Max(0.05f, thin), _homeScale.y, _homeScale.z);
                    if (t > 3f) gameObject.SetActive(false);
                    break;
                }
                default:
                {
                    float drop = 0.5f * 14f * t * t;
                    transform.position = _home + Vector3.down * drop;
                    transform.rotation = _homeRot * Quaternion.Euler(_spin * t);
                    if (t > 3.5f) gameObject.SetActive(false);
                    break;
                }
            }
        }
    }
}
