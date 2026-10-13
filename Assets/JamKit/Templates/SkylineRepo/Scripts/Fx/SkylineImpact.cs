using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Where a shot landed: a small chip that pops out along the surface normal and shrinks away. Pooled primitives.
    /// A hit on something hittable is brighter and plays its impact sound.</summary>
    public sealed class SkylineImpact : MonoBehaviour
    {
        const float Life = 0.25f, Size = 0.07f, HitSize = 0.11f, PopSpeed = 1.2f;
        const int PoolSize = 40;
        /// <summary>The impact sounds (Task 20's; until then a missing one logs once and plays nothing, amendment 10).</summary>
        public const string WorldSound = "impact/world", HitSound = "impact/hit";

        static readonly List<SkylineImpact> _pool = new List<SkylineImpact>();
        static int _next;

        Renderer _renderer;
        Vector3 _velocity;
        float _left, _size;

        public static void Spawn(Vector3 point, Vector3 normal, bool onHittable)
        {
            var i = Take();
            i._size = onHittable ? HitSize : Size;
            i.transform.SetPositionAndRotation(point + normal * 0.02f, Random.rotation);
            i.transform.localScale = Vector3.one * i._size;
            i._velocity = normal * PopSpeed;
            i._left = Life;
            i._renderer.material.color = onHittable ? new Color(1f, 0.85f, 0.5f) : new Color(0.75f, 0.72f, 0.65f);
            i.gameObject.SetActive(true);
        }

        static SkylineImpact Take()
        {
            for (int k = _pool.Count - 1; k >= 0; k--) if (_pool[k] == null) _pool.RemoveAt(k);
            if (_pool.Count < PoolSize)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Impact";
                Object.Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var imp = go.AddComponent<SkylineImpact>();
                imp._renderer = r;
                _pool.Add(imp);
                return imp;
            }
            _next = (_next + 1) % _pool.Count;
            return _pool[_next];
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _left -= dt;
            if (_left <= 0f) { gameObject.SetActive(false); return; }
            transform.position += _velocity * dt;
            _velocity *= Mathf.Exp(-8f * dt);
            transform.localScale = Vector3.one * (_size * (_left / Life));
        }
    }
}
