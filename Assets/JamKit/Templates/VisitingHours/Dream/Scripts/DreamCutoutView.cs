using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// A neighbour (or the dog) as a flat cut-out on the dream street. It stands facing the street and, once you
    /// come within <see cref="noticeDistance"/>, turns about its feet to watch you: a beat late, never quite
    /// fast enough, and it keeps watching after you've passed. Eerie but cute. Turns only about the vertical
    /// axis (it stays a flat card); a slow breathing bob keeps it from looking printed.
    /// </summary>
    public sealed class DreamCutoutView : MonoBehaviour
    {
        public Transform card;
        public float noticeDistance = 32f;
        [Tooltip("Degrees per second at most.")]
        public float turnSpeed = 140f;
        [Tooltip("Seconds it takes to notice you.")]
        public float noticeDelay = 0.35f;
        public float restYaw;

        Transform _target;
        float _yaw, _vel, _noticed = -1f, _phase;

        /// <summary>The cut-out's yaw now (degrees).</summary>
        public float Yaw => _yaw;

        void Start()
        {
            _yaw = restYaw;
            _phase = transform.position.x * 1.7f + transform.position.z * 0.31f;
            Apply(0f);
        }

        /// <summary>What it watches (the player's camera); null finds the main camera.</summary>
        public void Watch(Transform target) => _target = target;

        void LateUpdate()
        {
            if (_target == null)
            {
                var cam = Camera.main;
                if (cam != null) _target = cam.transform;
            }
            float dt = Time.deltaTime;
            float want = restYaw;
            if (_target != null)
            {
                Vector3 d = _target.position - transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist < noticeDistance)
                {
                    if (_noticed < 0f) _noticed = 0f;
                    _noticed += dt;
                }
                if (_noticed >= noticeDelay && dist > 0.01f) want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            if (dt > 0f) _yaw = Mathf.SmoothDampAngle(_yaw, want, ref _vel, 0.28f, turnSpeed, dt);
            Apply(dt);
        }

        void Apply(float dt)
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (card != null)
            {
                // Breathe: a slow, tiny bob of the card.
                float bob = 0.012f * Mathf.Sin(Time.time * 1.9f + _phase);
                card.localPosition = new Vector3(card.localPosition.x, card.localScale.y * (0.5f + bob), card.localPosition.z);
            }
        }
    }
}
