using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The floor-care unit's acid sweep drawn and timed (2.5: "ankle-height acid sweep"). The bot starts it facing the
    /// target and steps it on its own clock (<see cref="SkylineBot"/> checks <see cref="AcidSweep.Touches"/> at <see cref="T"/>
    /// before each <see cref="Advance"/>, as FightSim does, and damages once per sweep). The jet is a low green bar from the
    /// nozzle sweeping <see cref="EnemyDef.sweepArcDeg"/> left to right out to <see cref="EnemyDef.sweepRadius"/>; its tip
    /// leaves a puddle line that fades over a few seconds (<see cref="SkylinePuddle"/>, one pool shared by every unit).</summary>
    public sealed class SkylineAcid : MonoBehaviour
    {
        const float JetHeight = 0.12f, JetThickness = 0.07f, PuddleEvery = 0.05f;

        EnemyDef _def;
        Transform _jet;
        float _sincePuddle;

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
            var jet = SkylineBotLook.Visual(PrimitiveType.Cube, "Jet", null, SkylineBotLook.Lit(SkylineBotLook.Acid), false);
            jet.SetActive(false);
            a._jet = jet.transform;
            return a;
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
            if (_jet != null) _jet.gameObject.SetActive(false);
        }

        /// <summary>The jet's bearing now (degrees).</summary>
        public float JetBearing => Yaw - _def.sweepArcDeg * 0.5f + _def.sweepArcDeg * (_def.sweepSeconds > 0f ? Mathf.Clamp01(T / _def.sweepSeconds) : 1f);

        /// <summary>Moves the sweep on by <paramref name="dt"/> from <paramref name="nozzle"/> (world), drawing the jet; ends it
        /// past the def's sweep time.</summary>
        public void Advance(float dt, Vector3 nozzle, float floorY)
        {
            if (!Running) return;
            Vector3 dir = Quaternion.Euler(0f, JetBearing, 0f) * Vector3.forward;
            Vector3 from = new Vector3(nozzle.x, floorY + JetHeight, nozzle.z);
            Vector3 tip = from + dir * _def.sweepRadius;
            _jet.gameObject.SetActive(true);
            _jet.SetPositionAndRotation((from + tip) * 0.5f, Quaternion.LookRotation(dir));
            _jet.localScale = new Vector3(JetThickness, JetThickness, _def.sweepRadius);
            _sincePuddle += dt;
            if (_sincePuddle >= PuddleEvery)
            {
                _sincePuddle = 0f;
                Puddle(new Vector3(tip.x, floorY + 0.01f, tip.z));
                Puddle(new Vector3(Mathf.Lerp(from.x, tip.x, 0.6f), floorY + 0.01f, Mathf.Lerp(from.z, tip.z, 0.6f)));
            }
            T += dt;
            if (T > _def.sweepSeconds) Stop();
        }

        void Puddle(Vector3 at) => SkylinePuddle.Drop(at);

        void OnDestroy()
        {
            if (_jet != null) Destroy(_jet.gameObject);
        }
    }

    /// <summary>One acid puddle: a flat green disc that shrinks away over <see cref="Life"/> s. One pool for every floor-care
    /// unit, filled up front by <see cref="Prewarm"/> (back-to-back sweeps keep about 70 alive at once), so a sweep creates
    /// nothing and a wrecked unit's puddles just fade out.</summary>
    public sealed class SkylinePuddle : MonoBehaviour
    {
        const float Life = 2.5f, Size = 0.45f;
        const int PrewarmCount = 96;
        static readonly Color PuddleColor = new Color(0.42f, 0.7f, 0.12f);
        static readonly List<SkylinePuddle> _pool = new List<SkylinePuddle>();
        static int _scan;

        float _age;

        public static void Drop(Vector3 at)
        {
            var p = Take();
            p.transform.position = at;
            p.transform.localScale = new Vector3(Size, 0.005f, Size);
            p._age = 0f;
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
            var go = SkylineBotLook.Visual(PrimitiveType.Cylinder, "Acid Puddle", null, SkylineBotLook.Lit(PuddleColor), false);
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

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Life) { gameObject.SetActive(false); return; }
            float k = 1f - _age / Life;
            transform.localScale = new Vector3(Size * k, 0.005f, Size * k);
        }
    }
}
