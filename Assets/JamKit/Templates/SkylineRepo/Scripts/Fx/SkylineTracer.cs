using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>A shot's tracer: a thin bright bar from the muzzle to where the shot ended, gone in a few frames. Pooled
    /// primitives (no shader lookups, so the build needs nothing extra).</summary>
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
            t._renderer.material.color = color;
            t._left = Life;
            t.gameObject.SetActive(true);
        }

        static SkylineTracer Take()
        {
            for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i);   // a scene change destroyed it
            if (_pool.Count < PoolSize)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Tracer";
                Object.DestroyImmediate(go.GetComponent<Collider>());   // now: a pooled cube's collider must never catch this frame's pellets
                var r = go.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var t = go.AddComponent<SkylineTracer>();
                t._renderer = r;
                _pool.Add(t);
                return t;
            }
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
