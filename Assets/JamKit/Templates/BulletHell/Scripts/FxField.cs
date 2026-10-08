using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// Cheap game feel, drawn instanced like the bullets: sparks (hits, deaths, cleared bullets),
    /// expanding rings (explosions, bombs, phase changes) and a scrolling star background.
    /// Fixed-size struct arrays; when full, the oldest effect is replaced. Runs on game time.
    /// </summary>
    public sealed class FxField : MonoBehaviour
    {
        public Material material;
        [Tooltip("Soft ring texture material (rings); falls back to the main material.")]
        public Material ringMaterial;
        public int maxSparks = 1024;
        public int maxRings = 48;
        public int stars = 90;
        public float sparkZ = -1.5f;
        public float ringZ = -1.6f;
        public float starZ = 9f;

        struct SparkData { public Vector2 p, v; public float age, life, size; public Color color; }
        struct RingData { public Vector2 p; public float age, life, from, to; public Color color; }
        struct StarData { public Vector2 p; public float speed, size, twinkle; }

        SparkData[] _sparks;
        RingData[] _rings;
        StarData[] _stars;
        int _sparkCount, _sparkNext, _ringCount, _ringNext;
        InstancedQuads _sparkQuads, _ringQuads, _starQuads;
        Vector2 _half = new Vector2(10f, 6f);
        Color _starColor = new Color(0.6f, 0.75f, 1f, 0.5f);
        float _starSpeed = 1f;
        System.Random _random = new System.Random(7);

        public int SparkCount => _sparkCount;
        public int RingCount => _ringCount;

        void Awake() => Ensure();

        void Ensure()
        {
            if (_sparks != null) return;
            _sparks = new SparkData[Mathf.Max(16, maxSparks)];
            _rings = new RingData[Mathf.Max(4, maxRings)];
            _stars = new StarData[Mathf.Max(0, stars)];
            _sparkQuads = new InstancedQuads(material, _sparks.Length);
            _ringQuads = new InstancedQuads(ringMaterial != null ? ringMaterial : material, _rings.Length);
            _starQuads = new InstancedQuads(material, _stars.Length);
            for (int i = 0; i < _stars.Length; i++) _stars[i] = NewStar(true);
        }

        /// <summary>Area the stars cover (the whole camera view, half-extents).</summary>
        public void SetStarArea(Vector2 halfSize, Color color, float speed)
        {
            Ensure();
            _half = halfSize;
            _starColor = color;
            _starSpeed = speed;
            for (int i = 0; i < _stars.Length; i++) _stars[i] = NewStar(true);
        }

        public void SetStarColor(Color color) => _starColor = color;

        float Rand(float a, float b) => a + (float)_random.NextDouble() * (b - a);

        StarData NewStar(bool anywhere) => new StarData
        {
            p = new Vector2(Rand(-_half.x, _half.x), anywhere ? Rand(-_half.y, _half.y) : _half.y + 0.2f),
            speed = Rand(0.4f, 2.2f),
            size = Rand(0.03f, 0.09f),
            twinkle = Rand(0f, 6.28f),
        };

        /// <summary>A burst of <paramref name="count"/> sparks flying out from a point.</summary>
        public void Burst(Vector2 at, Color color, int count, float speed = 4f, float size = 0.12f, float life = 0.35f)
        {
            Ensure();
            for (int i = 0; i < count; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                float s = speed * Rand(0.35f, 1f);
                AddSpark(new SparkData
                {
                    p = at,
                    v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s,
                    life = life * Rand(0.6f, 1.2f),
                    size = size * Rand(0.6f, 1.2f),
                    color = color,
                });
            }
        }

        /// <summary>One small spark that drifts (a cleared bullet, a graze).</summary>
        public void Spark(Vector2 at, Color color, Vector2 velocity, float size = 0.1f, float life = 0.3f)
        {
            Ensure();
            AddSpark(new SparkData { p = at, v = velocity, life = life, size = size, color = color });
        }

        void AddSpark(SparkData s)
        {
            if (_sparkCount < _sparks.Length) _sparks[_sparkCount++] = s;
            else
            {
                _sparks[_sparkNext] = s;
                _sparkNext = (_sparkNext + 1) % _sparks.Length;
            }
        }

        /// <summary>An expanding, fading ring from radius <paramref name="from"/> to <paramref name="to"/>.</summary>
        public void Ring(Vector2 at, Color color, float from, float to, float life = 0.4f)
        {
            Ensure();
            var r = new RingData { p = at, from = from, to = to, life = Mathf.Max(0.02f, life), color = color };
            if (_ringCount < _rings.Length) _rings[_ringCount++] = r;
            else
            {
                _rings[_ringNext] = r;
                _ringNext = (_ringNext + 1) % _rings.Length;
            }
        }

        public void ClearEffects()
        {
            _sparkCount = 0;
            _ringCount = 0;
        }

        void Update()
        {
            Ensure();
            float dt = Time.deltaTime;
            int i = 0;
            while (i < _sparkCount)
            {
                ref SparkData s = ref _sparks[i];
                s.age += dt;
                if (s.age >= s.life) { _sparks[i] = _sparks[--_sparkCount]; continue; }
                s.p += s.v * dt;
                s.v *= 1f - Mathf.Min(1f, 3f * dt);
                i++;
            }
            i = 0;
            while (i < _ringCount)
            {
                ref RingData r = ref _rings[i];
                r.age += dt;
                if (r.age >= r.life) { _rings[i] = _rings[--_ringCount]; continue; }
                i++;
            }
            float starDt = Time.deltaTime > 0f ? Time.deltaTime : 0f;
            for (int k = 0; k < _stars.Length; k++)
            {
                ref StarData st = ref _stars[k];
                st.p.y -= st.speed * _starSpeed * starDt;
                st.twinkle += starDt * 3f;
                if (st.p.y < -_half.y - 0.2f) st = NewStar(false);
            }
        }

        void LateUpdate()
        {
            if (_sparks == null || !InstancedQuads.CanDraw) return;
            _starQuads.Clear();
            for (int k = 0; k < _stars.Length; k++)
            {
                var st = _stars[k];
                var c = _starColor;
                c.a *= 0.55f + 0.45f * Mathf.Sin(st.twinkle);
                float len = st.size * (1f + st.speed * 0.6f);
                _starQuads.Add(st.p.x, st.p.y, 0f, Mathf.PI * 0.5f, len, st.size, c);
            }
            _starQuads.Draw(starZ);

            _sparkQuads.Clear();
            for (int k = 0; k < _sparkCount; k++)
            {
                var s = _sparks[k];
                float k01 = s.age / s.life;
                var c = s.color;
                c.a *= 1f - k01 * k01;
                _sparkQuads.Add(s.p.x, s.p.y, 0f, s.size * (1f - 0.5f * k01), c);
            }
            _sparkQuads.Draw(sparkZ);

            _ringQuads.Clear();
            for (int k = 0; k < _ringCount; k++)
            {
                var r = _rings[k];
                float k01 = r.age / r.life;
                float e = 1f - (1f - k01) * (1f - k01);
                float d = 2f * Mathf.Lerp(r.from, r.to, e);
                var c = r.color;
                c.a *= 1f - k01;
                _ringQuads.Add(r.p.x, r.p.y, 0f, d, c);
            }
            _ringQuads.Draw(ringZ);
        }
    }
}
