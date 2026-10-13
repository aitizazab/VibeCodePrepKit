using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The gallery's mannequin: an <see cref="ISkylineTarget"/> that never dies. Its root is its feet (as the
    /// player's); its chest is 1.25 m up (as the player's). It is hidden (inactive) until the director reveals it, and bots
    /// don't see an inactive target. Its velocity is measured from its movement, so a drone leads it when the director moves
    /// it. A hit flinches it and logs <c>SKYLINE_GALLERY dummy hit &lt;attacker&gt; &lt;damage&gt;</c>.</summary>
    public sealed class SkylineDummy : MonoBehaviour, ISkylineTarget
    {
        const float ChestHeight = 1.25f, FlinchSeconds = 0.25f, FlinchDeg = 14f;

        [Tooltip("The part that flinches (the body above the feet).")]
        public Transform body;

        Vector3 _last, _vel;
        bool _haveLast;
        float _flinch;
        Vector3 _flinchAxis = Vector3.right;

        public Vector3 Chest => transform.position + Vector3.up * ChestHeight;
        public Vector3 Velocity => _vel;
        public Transform Root => transform;
        public int Hits { get; private set; }

        public void TakeAttack(float damage, string attackerId, Vector3 attackerPosition)
        {
            Hits++;
            Debug.Log("SKYLINE_GALLERY dummy hit " + attackerId + " " + damage.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            Vector3 away = transform.position - attackerPosition;
            away.y = 0f;
            _flinchAxis = away.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, away.normalized) : Vector3.right;
            _flinch = FlinchSeconds;
        }

        void OnEnable() { _haveLast = false; _vel = Vector3.zero; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                if (_haveLast) _vel = (transform.position - _last) / dt;
                _last = transform.position;
                _haveLast = true;
            }
            if (body != null)
            {
                _flinch = Mathf.Max(0f, _flinch - dt);
                float k = _flinch / FlinchSeconds;
                body.localRotation = Quaternion.AngleAxis(FlinchDeg * k * k, transform.InverseTransformDirection(_flinchAxis));
            }
        }
    }
}
