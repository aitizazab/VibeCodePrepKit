using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// One grey-box piece of the lab's course (core <c>LabCourse.pieces[index]</c>), built by the
    /// MovementLabBuilder. Pieces in a collapse fall away when it fires (floors drop and tumble, the
    /// fold walls lean in toward the route) and come back when the player respawns before it.
    /// </summary>
    public sealed class LabPieceView : MonoBehaviour
    {
        public int index;
        public int collapse = -1;
        [Tooltip("Fold walls lean in toward the route instead of falling.")]
        public bool fold;

        Vector3 _home;
        Quaternion _homeRot;
        bool _homeSet;
        float _t = -1f;
        Vector3 _spin;
        Collider _col;

        public bool Collapsed => _t >= 0f;

        void Awake() => Remember();

        void Remember()
        {
            if (_homeSet) return;
            _homeSet = true;
            _home = transform.position;
            _homeRot = transform.rotation;
            _col = GetComponent<Collider>();
        }

        public void Collapse()
        {
            Remember();
            if (_t >= 0f) return;
            _t = 0f;
            if (_col != null) _col.enabled = false;
            var r = new System.Random(index * 7919 + 13);
            _spin = new Vector3((float)r.NextDouble() * 40f - 20f, (float)r.NextDouble() * 20f - 10f, (float)r.NextDouble() * 40f - 20f);
        }

        public void Restore()
        {
            Remember();
            _t = -1f;
            transform.SetPositionAndRotation(_home, _homeRot);
            gameObject.SetActive(true);
            if (_col != null) _col.enabled = true;
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            if (fold)
            {
                // Lean in about the bottom edge, toward the route (x = 0), to 25 degrees.
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / 1.2f));
                float side = _home.x > 0f ? 1f : -1f;
                var rot = Quaternion.Euler(0f, 0f, side * 25f * k);
                Vector3 pivot = _home + Vector3.down * (transform.lossyScale.y * 0.5f);
                transform.SetPositionAndRotation(pivot + rot * (_home - pivot), rot * _homeRot);
                return;
            }
            // Drop with gravity, tumbling a little; gone after a few seconds.
            float drop = 0.5f * 14f * _t * _t;
            transform.position = _home + Vector3.down * drop;
            transform.rotation = _homeRot * Quaternion.Euler(_spin * _t);
            if (_t > 3.5f) gameObject.SetActive(false);
        }
    }
}
