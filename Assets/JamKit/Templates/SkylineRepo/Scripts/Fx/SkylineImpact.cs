using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>Where a shot landed: a small chip that pops out along the surface normal and shrinks away. Pooled
    /// renderer-only cubes with two shared lit materials, filled up front by <see cref="Prewarm"/>. A hit on something
    /// hittable is brighter and plays its impact sound.</summary>
    public sealed class SkylineImpact : MonoBehaviour
    {
        const float Life = 0.25f, Size = 0.07f, HitSize = 0.11f, PopSpeed = 1.2f;
        const int PoolSize = 40;
        /// <summary>The impact sounds (Task 20's; until then a missing one logs once and plays nothing, amendment 10).</summary>
        public const string WorldSound = "impact/world", HitSound = "impact/hit";
        static readonly Color HitColor = new Color(1f, 0.85f, 0.5f), WorldColor = new Color(0.75f, 0.72f, 0.65f);

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
            i._renderer.sharedMaterial = SkylineBotLook.Lit(onHittable ? HitColor : WorldColor);
            i.gameObject.SetActive(true);
        }

        /// <summary>Fills the pool now (at scene start), so no chip is ever created mid-fight.</summary>
        public static void Prewarm()
        {
            Purge();
            SkylineBotLook.Lit(HitColor);
            SkylineBotLook.Lit(WorldColor);
            while (_pool.Count < PoolSize) Make().gameObject.SetActive(false);
        }

        static void Purge()
        {
            for (int k = _pool.Count - 1; k >= 0; k--) if (_pool[k] == null) _pool.RemoveAt(k);
        }

        static SkylineImpact Make()
        {
            var go = SkylineBotLook.Visual(PrimitiveType.Cube, "Impact", null, SkylineBotLook.Lit(WorldColor), false);
            var imp = go.AddComponent<SkylineImpact>();
            imp._renderer = go.GetComponent<Renderer>();
            _pool.Add(imp);
            return imp;
        }

        static SkylineImpact Take()
        {
            Purge();
            if (_pool.Count < PoolSize) return Make();
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
