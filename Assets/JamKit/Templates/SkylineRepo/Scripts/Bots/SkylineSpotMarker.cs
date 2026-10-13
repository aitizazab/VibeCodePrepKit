using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// A Writ weak spot drawn as cracked casing (the creator's damage-feedback rules, 2026-10-13): a dark wiring gap with
    /// thin gold and white-hot seams jagging out of it, flickering, and a few sparks spitting from it. On a live bot, glow plus
    /// sparks means this and nothing else. It forms with <c>writ/spot_form</c> (a ceramic crack with an electric crackle);
    /// shooting it is a break-through: a spark burst and <c>writ/spot_break</c>. It sits on the surface (parented to the bot's
    /// rig at the spot's own position, facing out along the hit's normal) and has no collider, so shots land on the part under
    /// it and the Writ finds the spot by distance. A missing sound logs once (SkylineSounds).
    /// </summary>
    public sealed class SkylineSpotMarker : MonoBehaviour
    {
        public const string FormSound = "writ/spot_form", BreakSound = "writ/spot_break";
        static readonly Color HotGold = new Color(4f, 2.6f, 0.6f), WhiteHot = new Color(5f, 4.6f, 3.6f), Gap = new Color(0.03f, 0.03f, 0.035f);
        static readonly Color Wire = new Color(0.55f, 0.16f, 0.08f);

        public int spotId;
        readonly List<Renderer> _seams = new List<Renderer>();
        float _nextSpark, _flicker;
        int _seed;

        /// <summary>Makes the crack on <paramref name="rig"/> at <paramref name="localPoint"/> (rig space), facing
        /// <paramref name="worldNormal"/>.</summary>
        public static SkylineSpotMarker Create(Transform rig, int spotId, Vector3 localPoint, Vector3 worldNormal, int seed)
        {
            var go = new GameObject("WritSpot " + spotId);
            go.transform.SetParent(rig, false);
            go.transform.localPosition = localPoint;
            Vector3 n = worldNormal.sqrMagnitude > 1e-6f ? worldNormal.normalized : -rig.forward;
            go.transform.rotation = Quaternion.LookRotation(-n, Mathf.Abs(Vector3.Dot(n, Vector3.up)) < 0.95f ? Vector3.up : Vector3.forward);
            go.transform.position += n * 0.006f;
            var m = go.AddComponent<SkylineSpotMarker>();
            m.spotId = spotId;
            m._seed = seed;
            m.Build();
            SkylineSounds.PlayAt(FormSound, go.transform.position, 0.9f);
            SkylineSpark.Burst(go.transform.position, n, 5, 2.5f);
            return m;
        }

        void Build()
        {
            var rng = new JamKit.Rng((ulong)(_seed * 7919 + spotId * 104729 + 17));
            // the dark gap with wiring in it, along the crack's main line
            float angle = rng.Range(-40f, 40f);
            var main = Quaternion.Euler(0f, 0f, angle);
            Bar("Gap", Vector3.zero, main, 0.17f, 0.03f, SkylineBotLook.Lit(Gap), null);
            Bar("Wire", new Vector3(0.02f, 0.004f, -0.002f), main * Quaternion.Euler(0f, 0f, 8f), 0.08f, 0.008f, SkylineBotLook.Lit(Wire), null);
            Bar("Wire", new Vector3(-0.03f, -0.004f, -0.002f), main * Quaternion.Euler(0f, 0f, -12f), 0.06f, 0.008f, SkylineBotLook.Lit(Wire), null);
            // the seams: a jagged polyline either side of the gap, plus two branches
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = new Vector3(side * 0.012f, side * 0.016f, 0f);
                float a = angle;
                for (int k = 0; k < 4; k++)
                {
                    a += rng.Range(-35f, 35f);
                    float len = rng.Range(0.035f, 0.06f);
                    Vector3 dir = Quaternion.Euler(0f, 0f, a) * new Vector3(side, 0f, 0f);
                    Bar("Seam", p + dir * (len * 0.5f), Quaternion.Euler(0f, 0f, a), len, 0.007f, SkylineBotLook.Glow(k == 0 ? WhiteHot : HotGold), _seams);
                    p += dir * len;
                    if (k == 1)
                    {
                        float b = a + side * rng.Range(40f, 70f);
                        Vector3 bd = Quaternion.Euler(0f, 0f, b) * new Vector3(side, 0f, 0f);
                        Bar("Seam", p + bd * 0.02f, Quaternion.Euler(0f, 0f, b), 0.04f, 0.005f, SkylineBotLook.Glow(HotGold), _seams);
                    }
                }
            }
        }

        void Bar(string name, Vector3 local, Quaternion rot, float length, float width, Material mat, List<Renderer> into)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(length, width, 0.004f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (into != null) into.Add(r);
        }

        /// <summary>The spot was shot: a break-through (spark burst, crunchy break) and gone.</summary>
        public void BreakThrough()
        {
            Vector3 n = -transform.forward;
            SkylineSounds.PlayAt(BreakSound, transform.position, 1f);
            SkylineSpark.Burst(transform.position, n, 16, 4.5f);
            Destroy(gameObject);
        }

        /// <summary>The spot expired or was replaced: it closes quietly.</summary>
        public void Close() => Destroy(gameObject);

        void Update()
        {
            float dt = Time.deltaTime;
            // a crackling flicker on the seams
            _flicker -= dt;
            if (_flicker <= 0f)
            {
                _flicker = Random.Range(0.04f, 0.12f);
                for (int i = 0; i < _seams.Count; i++)
                    if (_seams[i] != null) _seams[i].enabled = Random.value > 0.12f;
            }
            _nextSpark -= dt;
            if (_nextSpark <= 0f)
            {
                _nextSpark = Random.Range(0.25f, 0.55f);
                SkylineSpark.Burst(transform.position, -transform.forward, Random.Range(1, 4), 2f);
            }
        }
    }

    /// <summary>A spark: a tiny white-hot fleck thrown out, falling and shrinking for a third of a second. Used only by the
    /// Writ's cracked casing and by fresh wrecks (the only things allowed to spark).</summary>
    public sealed class SkylineSpark : MonoBehaviour
    {
        const float Life = 0.35f, Size = 0.025f;
        const int PoolSize = 64;
        static readonly List<SkylineSpark> _pool = new List<SkylineSpark>();
        static int _next;
        static readonly Color Hot = new Color(5f, 3.8f, 1.6f);

        Vector3 _vel;
        float _left;

        public static void Burst(Vector3 at, Vector3 normal, int count, float speed)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var s = Take();
                Vector3 dir = (n + Random.insideUnitSphere * 0.8f).normalized;
                s._vel = dir * speed * Random.Range(0.5f, 1f);
                s._left = Life * Random.Range(0.6f, 1f);
                s.transform.position = at;
                s.transform.localScale = Vector3.one * Size;
                s.gameObject.SetActive(true);
            }
        }

        static SkylineSpark Take()
        {
            for (int k = _pool.Count - 1; k >= 0; k--) if (_pool[k] == null) _pool.RemoveAt(k);
            if (_pool.Count < PoolSize)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Spark";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = SkylineBotLook.Glow(Hot);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var s = go.AddComponent<SkylineSpark>();
                _pool.Add(s);
                return s;
            }
            _next = (_next + 1) % _pool.Count;
            return _pool[_next];
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _left -= dt;
            if (_left <= 0f) { gameObject.SetActive(false); return; }
            _vel += Physics.gravity * dt;
            transform.position += _vel * dt;
            transform.localScale = Vector3.one * (Size * Mathf.Clamp01(_left / Life + 0.2f));
        }
    }
}
