using UnityEngine;

namespace JamKit.Sandbox
{
    /// <summary>The red block: slides between two points and hurts the player on touch.</summary>
    public sealed class SandboxHazard : MonoBehaviour
    {
        public Vector2 pointA = new Vector2(-2f, 2.5f);
        public Vector2 pointB = new Vector2(-2f, -2.5f);
        public float secondsPerTrip = 2.2f;
        public float damage = 1f;
        [Tooltip("Half the block's size, for the touch test.")]
        public Vector2 halfSize = new Vector2(0.5f, 0.5f);

        float _t;

        void Update()
        {
            _t += Time.deltaTime / Mathf.Max(0.1f, secondsPerTrip);
            float k = Mathf.PingPong(_t, 1f);
            k = k * k * (3f - 2f * k);
            Vector2 p = Vector2.Lerp(pointA, pointB, k);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            transform.Rotate(0f, 0f, 90f * Time.deltaTime);
        }

        /// <summary>Box-versus-box touch with the player's half size.</summary>
        public bool Touches(Vector3 position, Vector2 otherHalfSize)
        {
            Vector2 d = (Vector2)(position - transform.position);
            return Mathf.Abs(d.x) < halfSize.x + otherHalfSize.x && Mathf.Abs(d.y) < halfSize.y + otherHalfSize.y;
        }
    }
}
