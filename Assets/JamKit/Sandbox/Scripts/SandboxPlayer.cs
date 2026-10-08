using JamKit.Shell;
using UnityEngine;

namespace JamKit.Sandbox
{
    /// <summary>The sandbox's square: moves with Move (WASD / left stick), dashes with Dash (Space / A).</summary>
    [RequireComponent(typeof(Damageable))]
    public sealed class SandboxPlayer : MonoBehaviour
    {
        public float speed = 6f;
        public float dashSpeed = 18f;
        public float dashSeconds = 0.12f;
        public float dashCooldown = 0.6f;
        [Tooltip("The player stays inside this box around the origin.")]
        public Vector2 arenaHalfSize = new Vector2(8.5f, 4.5f);

        Damageable _damageable;
        float _dashLeft, _dashCooldownLeft;
        Vector2 _dashDir = Vector2.right;
        Vector2 _lastMove = Vector2.right;

        public Damageable Damageable => _damageable != null ? _damageable : (_damageable = GetComponent<Damageable>());

        void Awake() => _damageable = GetComponent<Damageable>();

        void Update()
        {
            if (!GameRoot.Exists) return;
            var input = GameRoot.Input;
            Vector2 move = input.MoveVector;
            if (move.sqrMagnitude > 0.01f) _lastMove = move.normalized;

            float dt = Time.deltaTime;
            _dashCooldownLeft -= dt;
            if (input.Pressed(input.Dash) && _dashCooldownLeft <= 0f)
            {
                _dashDir = _lastMove;
                _dashLeft = dashSeconds;
                _dashCooldownLeft = dashCooldown;
                Damageable.Health.GrantInvulnerability(dashSeconds + 0.05f);
                GameRoot.Audio.Play("dash");
            }

            Vector2 velocity = move * speed;
            if (_dashLeft > 0f)
            {
                _dashLeft -= dt;
                velocity = _dashDir * dashSpeed;
            }
            Vector3 p = transform.position + (Vector3)(velocity * dt);
            p.x = Mathf.Clamp(p.x, -arenaHalfSize.x, arenaHalfSize.x);
            p.y = Mathf.Clamp(p.y, -arenaHalfSize.y, arenaHalfSize.y);
            transform.position = p;
        }

        /// <summary>Tests use this to walk the player onto things.</summary>
        public void Teleport(Vector2 position) => transform.position = new Vector3(position.x, position.y, transform.position.z);
    }
}
