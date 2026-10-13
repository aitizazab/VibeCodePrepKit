using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The floor-care unit's acid sweep drawn and timed (2.5: "ankle-height acid sweep"). The bot starts it facing the
    /// target and steps it on its own clock (<see cref="SkylineBot"/> checks <see cref="AcidSweep.Touches"/> at <see cref="T"/>
    /// before each <see cref="Advance"/>, as FightSim does, and damages once per sweep). The jet is a low green bar from the
    /// nozzle sweeping <see cref="EnemyDef.sweepArcDeg"/> left to right out to <see cref="EnemyDef.sweepRadius"/>; its tip
    /// leaves a puddle line that fades over a few seconds.</summary>
    public sealed class SkylineAcid : MonoBehaviour
    {
        const float JetHeight = 0.12f, JetThickness = 0.07f, PuddleEvery = 0.05f, PuddleLife = 2.5f, PuddleSize = 0.45f;

        EnemyDef _def;
        Transform _jet;
        float _sincePuddle;
        readonly List<Transform> _puddles = new List<Transform>();
        readonly List<float> _puddleAge = new List<float>();

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
            var jet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            jet.name = "Jet";
            Object.DestroyImmediate(jet.GetComponent<Collider>());
            jet.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Lit(SkylineBotLook.Acid);
            jet.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

        void Puddle(Vector3 at)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = "Acid Puddle";
            Object.DestroyImmediate(p.GetComponent<Collider>());
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = SkylineBotLook.Lit(new Color(0.42f, 0.7f, 0.12f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.transform.position = at;
            p.transform.localScale = new Vector3(PuddleSize, 0.005f, PuddleSize);
            _puddles.Add(p.transform);
            _puddleAge.Add(0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _puddles.Count - 1; i >= 0; i--)
            {
                var p = _puddles[i];
                _puddleAge[i] += dt;
                if (p == null || _puddleAge[i] >= PuddleLife)
                {
                    if (p != null) Destroy(p.gameObject);
                    _puddles.RemoveAt(i);
                    _puddleAge.RemoveAt(i);
                    continue;
                }
                float k = 1f - _puddleAge[i] / PuddleLife;
                p.localScale = new Vector3(PuddleSize * k, 0.005f, PuddleSize * k);
            }
        }

        void OnDestroy()
        {
            foreach (var p in _puddles) if (p != null) Destroy(p.gameObject);
            if (_jet != null) Destroy(_jet.gameObject);
        }
    }
}
