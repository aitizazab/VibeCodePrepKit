using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>A shot's tracer: a thin bright bar from the muzzle to where the shot ended, gone in a few frames. Pooled
    /// renderer-only cubes (no colliders made or destroyed) with one shared lit material per colour (no per-tracer material
    /// copies, no shader lookups), filled up front by <see cref="Prewarm"/>.</summary>
    public sealed class SkylineTracer : MonoBehaviour
    {
        const float Life = 0.06f, Thickness = 0.012f;
        const int PoolSize = 24;

        static readonly List<SkylineTracer> _pool = new List<SkylineTracer>();
        static int _next;

        Renderer _renderer;
        float _left;

        /// <summary>Draws one tracer from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static void Spawn(Vector3 from, Vector3 to, Color color)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (!(len > 0.05f)) return;
            var t = Take();
            t.transform.SetPositionAndRotation(from + d * 0.5f, Quaternion.LookRotation(d / len));
            t.transform.localScale = new Vector3(Thickness, Thickness, len);
            t._renderer.sharedMaterial = SkylineBotLook.Lit(color);
            t._left = Life;
            t.gameObject.SetActive(true);
        }

        /// <summary>Fills the pool now (at scene start), so no tracer is ever created mid-fight.</summary>
        public static void Prewarm()
        {
            Purge();
            while (_pool.Count < PoolSize) Make().gameObject.SetActive(false);
        }

        static void Purge()
        {
            for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i);   // a scene change destroyed it
        }

        static SkylineTracer Make()
        {
            var go = SkylineBotLook.Visual(PrimitiveType.Cube, "Tracer", null, SkylineBotLook.Lit(Color.white), false);
            var t = go.AddComponent<SkylineTracer>();
            t._renderer = go.GetComponent<Renderer>();
            _pool.Add(t);
            return t;
        }

        static SkylineTracer Take()
        {
            Purge();
            if (_pool.Count < PoolSize) return Make();
            _next = (_next + 1) % _pool.Count;
            return _pool[_next];
        }

        void Update()
        {
            _left -= Time.deltaTime;
            if (_left <= 0f) gameObject.SetActive(false);
        }
    }
}
