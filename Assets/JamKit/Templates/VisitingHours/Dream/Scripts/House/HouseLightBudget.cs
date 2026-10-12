using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 2's light budget (docs/redesign/perf.md, House 1: 89 point lights, 9 of them shadowed, 54 cube faces a frame):
    /// every light under <see cref="roots"/> stays shadowless except the <see cref="shadowed"/> nearest of the room lights
    /// marked "(main)" in their names, which cast soft shadows while the camera is within their reach. Re-chosen five times
    /// a second; nothing is allocated after Start, and a light's shadow mode is only written when it changes. Lights far
    /// from the camera need nothing from here: URP's Forward+ culls them by range.
    /// </summary>
    public sealed class HouseLightBudget : MonoBehaviour
    {
        public Transform[] roots;
        public Camera cam;
        /// <summary>How many of the main lights cast shadows at once.</summary>
        public int shadowed = 2;
        /// <summary>A main light can cast while the camera is within its range plus this (m).</summary>
        public float reach = 1f;
        public float interval = 0.2f;

        Light[] _main;
        float _next;
        int[] _best;
        float[] _bestD;

        void Start()
        {
            int n = 0;
            if (roots != null)
                foreach (var r in roots)
                {
                    if (r == null) continue;
                    foreach (var l in r.GetComponentsInChildren<Light>(true))
                    {
                        if (l.type == LightType.Directional) continue;
                        if (l.name.Contains("(main)")) n++;
                        else if (l.shadows != LightShadows.None) l.shadows = LightShadows.None;
                    }
                }
            _main = new Light[n];
            int k = 0;
            if (roots != null)
                foreach (var r in roots)
                {
                    if (r == null) continue;
                    foreach (var l in r.GetComponentsInChildren<Light>(true))
                        if (l.type != LightType.Directional && l.name.Contains("(main)")) { _main[k++] = l; l.shadows = LightShadows.None; }
                }
            _best = new int[Mathf.Max(1, shadowed)];
            _bestD = new float[_best.Length];
        }

        void Update()
        {
            if (_main == null || _main.Length == 0 || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + interval;
            var c = cam != null ? cam : Camera.main;
            if (c == null) return;
            var p = c.transform.position;
            for (int i = 0; i < _best.Length; i++) { _best[i] = -1; _bestD[i] = float.MaxValue; }
            for (int i = 0; i < _main.Length; i++)
            {
                var l = _main[i];
                if (l == null || !l.isActiveAndEnabled) continue;
                float d = (l.transform.position - p).sqrMagnitude, r = l.range + reach;
                if (d > r * r) continue;
                for (int s = 0; s < _best.Length; s++)
                {
                    if (d >= _bestD[s]) continue;
                    for (int m = _best.Length - 1; m > s; m--) { _best[m] = _best[m - 1]; _bestD[m] = _bestD[m - 1]; }
                    _best[s] = i; _bestD[s] = d;
                    break;
                }
            }
            for (int i = 0; i < _main.Length; i++)
            {
                var l = _main[i];
                if (l == null) continue;
                bool on = false;
                for (int s = 0; s < _best.Length; s++) if (_best[s] == i) { on = true; break; }
                var want = on ? LightShadows.Soft : LightShadows.None;
                if (l.shadows != want) l.shadows = want;
            }
        }
    }
}
