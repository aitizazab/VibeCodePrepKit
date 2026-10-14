using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The floor-care unit's acid sweep drawn and timed (2.5: "ankle-height acid sweep"). The bot starts it facing the
    /// target and steps it on its own clock (<see cref="SkylineBot"/> checks <see cref="AcidSweep.Touches"/> at <see cref="T"/>
    /// before each <see cref="Advance"/>, as FightSim does, and damages once per sweep: the look below changes none of that).
    /// <para>The look (judge round 1: the jet had been a stretched green cube and the puddles flat paper discs): a liquid
    /// stream from the nozzle, low over the floor with a slight bow, that lags behind the nozzle as it swings (a hose's curve,
    /// not a rigid bar), fanning out toward the end, with a bright toxic core inside a wet, glossy sheath; droplets spatter
    /// off where it lands; and where it lands it leaves a line of glossy, irregular puddles (<see cref="SkylinePuddle"/>, one
    /// pool shared by every unit) that splat out and shrink away.</para></summary>
    public sealed class SkylineAcid : MonoBehaviour
    {
        const float JetHeight = 0.12f, PuddleEvery = 0.05f;
        const int StreamPoints = 14, DropletCount = 14;
        /// <summary>How far behind the nozzle's swing the stream's far end trails (s of swing).</summary>
        const float Lag = 0.11f;
        /// <summary>The stream's bow above the straight line from the nozzle to the floor (m).</summary>
        const float Bow = 0.1f;
        public static readonly Color AcidWet = new Color(0.36f, 0.66f, 0.08f);
        static readonly Color AcidCore = new Color(0.9f, 1.9f, 0.25f);
        static readonly Color DropletColor = new Color(0.45f, 0.78f, 0.12f);

        EnemyDef _def;
        GameObject _jet;
        LineRenderer _sheath, _core;
        readonly Vector3[] _pts = new Vector3[StreamPoints];
        float _sincePuddle;
        Transform[] _drops;
        Vector3[] _dropVel;
        float[] _dropFloor;
        int _nextDrop;

        public bool Running { get; private set; }
        /// <summary>Seconds into the sweep.</summary>
        public float T { get; private set; }
        /// <summary>The facing the sweep is centred on (degrees, 0 along +Z).</summary>
        public float Yaw { get; private set; }
        /// <summary>This sweep has already landed its one hit.</summary>
        public bool Hit;

        public static SkylineAcid Create(Transform parent, EnemyDef def)
        {
            var go = new GameObject("Acid");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<SkylineAcid>();
            a._def = def;
            a._jet = new GameObject("Acid Jet");
            a._sheath = Line(a._jet.transform, "Sheath", SkylineBotLook.Wet(AcidWet), true,
                new AnimationCurve(new Keyframe(0f, 0.06f), new Keyframe(0.6f, 0.1f), new Keyframe(1f, 0.2f)));
            a._core = Line(a._jet.transform, "Core", SkylineBotLook.Glow(AcidCore), false,
                new AnimationCurve(new Keyframe(0f, 0.025f), new Keyframe(0.7f, 0.035f), new Keyframe(1f, 0.05f)));
            a._jet.SetActive(false);
            a._drops = new Transform[DropletCount];
            a._dropVel = new Vector3[DropletCount];
            a._dropFloor = new float[DropletCount];
            var dropMat = SkylineBotLook.Wet(DropletColor);
            for (int i = 0; i < DropletCount; i++)
            {
                var d = SkylineBotLook.Visual(PrimitiveType.Sphere, "Acid Drop", a._jet.transform.parent, dropMat, false);
                d.SetActive(false);
                a._drops[i] = d.transform;
            }
            return a;
        }

        static LineRenderer Line(Transform parent, string name, Material m, bool lit, AnimationCurve width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = m;
            lr.useWorldSpace = true;
            lr.positionCount = StreamPoints;
            lr.widthCurve = width;
            lr.widthMultiplier = 1f;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.generateLightingData = lit;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = lit;
            return lr;
        }

        public void Begin(float yawDeg)
        {
            Running = true;
            T = 0f;
            Yaw = yawDeg;
            Hit = false;
            _sincePuddle = PuddleEvery;
        }

        public void Stop()
        {
            Running = false;
            if (_jet != null) _jet.SetActive(false);
        }

        float SweepRate => _def.sweepSeconds > 0f ? _def.sweepArcDeg / _def.sweepSeconds : 0f;

        /// <summary>The jet's bearing now (degrees).</summary>
        public float JetBearing => BearingAt(T);

        float BearingAt(float t) => Yaw - _def.sweepArcDeg * 0.5f + _def.sweepArcDeg * (_def.sweepSeconds > 0f ? Mathf.Clamp01(t / _def.sweepSeconds) : 1f);

        /// <summary>Moves the sweep on by <paramref name="dt"/> from <paramref name="nozzle"/> (world), drawing the stream;
        /// ends it past the def's sweep time.</summary>
        public void Advance(float dt, Vector3 nozzle, float floorY)
        {
            if (!Running) return;
            Vector3 from = new Vector3(nozzle.x, floorY + JetHeight, nozzle.z);
            float wob = Time.time * 22f;
            for (int i = 0; i < StreamPoints; i++)
            {
                float u = (float)i / (StreamPoints - 1);
                // the far part of the stream left the nozzle a moment ago, when it pointed further back in the swing
                float bearing = BearingAt(T - Lag * u);
                Vector3 dir = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
                Vector3 side = new Vector3(dir.z, 0f, -dir.x);
                Vector3 p = from + dir * (_def.sweepRadius * u);
                p.y = Mathf.Lerp(floorY + JetHeight, floorY + 0.02f, u * u) + Bow * 4f * u * (1f - u);
                p += side * (Mathf.Sin(u * 9f - wob) * 0.03f * u);
                _pts[i] = p;
            }
            _jet.SetActive(true);
            _sheath.SetPositions(_pts);
            _core.SetPositions(_pts);
            Vector3 land = _pts[StreamPoints - 1];
            _sincePuddle += dt;
            if (_sincePuddle >= PuddleEvery)
            {
                _sincePuddle = 0f;
                SkylinePuddle.Drop(new Vector3(land.x, floorY + 0.004f, land.z));
                Vector3 mid = _pts[(StreamPoints * 2) / 3];
                SkylinePuddle.Drop(new Vector3(mid.x, floorY + 0.004f, mid.z), 0.75f);
                Spatter(land, floorY);
                Spatter(land, floorY);
            }
            T += dt;
            if (T > _def.sweepSeconds) Stop();
        }

        /// <summary>A droplet thrown up and out from where the stream lands.</summary>
        void Spatter(Vector3 at, float floorY)
        {
            var d = _drops[_nextDrop];
            _dropVel[_nextDrop] = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(1.2f, 2.4f), Random.Range(-1.6f, 1.6f));
            _dropFloor[_nextDrop] = floorY;
            d.position = at + Vector3.up * 0.03f;
            d.localScale = Vector3.one * Random.Range(0.03f, 0.055f);
            d.gameObject.SetActive(true);
            _nextDrop = (_nextDrop + 1) % _drops.Length;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!(dt > 0f) || _drops == null) return;
            for (int i = 0; i < _drops.Length; i++)
            {
                var d = _drops[i];
                if (d == null || !d.gameObject.activeSelf) continue;
                _dropVel[i] += Physics.gravity * dt;
                d.position += _dropVel[i] * dt;
                if (d.position.y <= _dropFloor[i] + 0.01f) d.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (_jet != null) Destroy(_jet);
            if (_drops != null) foreach (var d in _drops) if (d != null) Destroy(d.gameObject);
        }
    }

    /// <summary>One acid puddle: a glossy, irregular blob of acid on the floor (one of a few shapes, turned and sized at random,
    /// domed a few millimetres like a liquid's meniscus, very smooth so it catches the light) that splats out in a moment,
    /// sits, then shrinks away over <see cref="Life"/> s. One pool for every floor-care unit, filled up front by
    /// <see cref="Prewarm"/> (back-to-back sweeps keep about 70 alive at once), so a sweep creates nothing and a wrecked
    /// unit's puddles just fade out.</summary>
    public sealed class SkylinePuddle : MonoBehaviour
    {
        const float Life = 2.5f, Size = 0.5f, Splat = 0.12f, Height = 0.014f;
        const int PrewarmCount = 110, Shapes = 4;
        static readonly List<SkylinePuddle> _pool = new List<SkylinePuddle>();
        static readonly Mesh[] _shapes = new Mesh[Shapes];
        static int _scan;

        float _age, _life, _size;

        public static void Drop(Vector3 at) => Drop(at, 1f, Life);

        /// <summary>A puddle at <paramref name="at"/> (on the floor), <paramref name="scale"/> times the usual size, lasting
        /// <paramref name="life"/> s.</summary>
        public static void Drop(Vector3 at, float scale, float life = Life)
        {
            var p = Take();
            // a hair above the floor and above each other in turn, so overlapping puddles never flicker
            p.transform.position = at + Vector3.up * Random.Range(0f, 0.004f);
            p.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            p._size = Size * scale * Random.Range(0.75f, 1.25f);
            p._life = Mathf.Max(0.5f, life);
            p._age = 0f;
            p.GetComponent<MeshFilter>().sharedMesh = Shape(Random.Range(0, Shapes));
            p.Apply();
            p.gameObject.SetActive(true);
        }

        public static void Prewarm()
        {
            Purge();
            while (_pool.Count < PrewarmCount) Make();
        }

        static void Purge()
        {
            for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i);
        }

        static SkylinePuddle Make()
        {
            var go = new GameObject("Acid Puddle");
            go.AddComponent<MeshFilter>().sharedMesh = Shape(_pool.Count % Shapes);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = SkylineBotLook.Wet(SkylineAcid.AcidWet);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            var p = go.AddComponent<SkylinePuddle>();
            _pool.Add(p);
            return p;
        }

        static SkylinePuddle Take()
        {
            Purge();
            for (int k = 0; k < _pool.Count; k++)
            {
                _scan = (_scan + 1) % _pool.Count;
                if (!_pool[_scan].gameObject.activeSelf) return _pool[_scan];
            }
            return Make();
        }

        /// <summary>A puddle shape about 1 unit across: a wobbly outline (a few overlapping waves round the rim), domed to 1 at
        /// the centre, falling to a soft edge.</summary>
        static Mesh Shape(int i)
        {
            if (_shapes[i] != null) return _shapes[i];
            var rng = new System.Random(4241 * (i + 3));
            const int N = 24;
            float p1 = (float)rng.NextDouble() * 6.3f, p2 = (float)rng.NextDouble() * 6.3f, p3 = (float)rng.NextDouble() * 6.3f;
            float a2 = 0.12f + 0.1f * (float)rng.NextDouble(), a3 = 0.08f + 0.08f * (float)rng.NextDouble(), a5 = 0.05f + 0.05f * (float)rng.NextDouble();
            var verts = new List<Vector3> { new Vector3(0f, 1f, 0f) };
            for (int k = 0; k < N; k++)
            {
                float t = k * Mathf.PI * 2f / N;
                float r = 0.5f * (1f + a2 * Mathf.Sin(2f * t + p1) + a3 * Mathf.Sin(3f * t + p2) + a5 * Mathf.Sin(5f * t + p3));
                verts.Add(new Vector3(Mathf.Cos(t) * r * 0.8f, 0.8f, Mathf.Sin(t) * r * 0.8f));   // the meniscus
                verts.Add(new Vector3(Mathf.Cos(t) * r, 0f, Mathf.Sin(t) * r));                    // the edge
            }
            var tris = new List<int>();
            for (int k = 0; k < N; k++)
            {
                int a = 1 + 2 * k, b = 1 + 2 * ((k + 1) % N);
                tris.Add(0); tris.Add(b); tris.Add(a);
                tris.Add(a); tris.Add(b); tris.Add(b + 1);
                tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
            }
            var m = new Mesh { name = "Acid puddle " + i };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            _shapes[i] = m;
            return m;
        }

        void Apply()
        {
            // splats out over its first moment, sits, then shrinks over its last 40%
            float grow = Mathf.Clamp01(_age / Splat);
            float k = 0.35f + 0.65f * (1f - (1f - grow) * (1f - grow));
            float fade = Mathf.Clamp01((_life - _age) / (_life * 0.4f));
            float s = _size * k * fade;
            transform.localScale = new Vector3(s, Height * fade, s);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life) { gameObject.SetActive(false); return; }
            Apply();
        }
    }
}
