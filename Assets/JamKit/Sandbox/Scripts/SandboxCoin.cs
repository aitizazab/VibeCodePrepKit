using UnityEngine;

namespace JamKit.Sandbox
{
    /// <summary>Marker for the sandbox's floating pickups (SandboxGame collects them).</summary>
    public sealed class SandboxCoin : MonoBehaviour
    {
        public float bobHeight = 0.12f;
        public float bobSpeed = 3f;
        Vector3 _home;
        float _phase;

        void Awake()
        {
            _home = transform.position;
            _phase = _home.x * 1.7f;
        }

        void Update()
        {
            _phase += Time.deltaTime * bobSpeed;
            transform.position = _home + Vector3.up * (Mathf.Sin(_phase) * bobHeight);
        }
    }
}
