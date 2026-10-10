using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JamKit.Movement
{
    /// <summary>
    /// V2, world-space streaks (docs/SPEED-FEEL.md): motes (the lab) or pastel confetti and petals (the dream) hang
    /// in the air ahead and stretch into streaks as you pass them, so speed reads on any background with real
    /// parallax. One particle emitter around the player in world space, emitting by hand where you'll pass within
    /// about 0.35-1.5 s, never closer than ~1.5 m to your line of travel; the shader fades near ones out of the
    /// screen's clear centre. Rate and stretch follow the speed tiers (<see cref="SpeedFeel"/>; none while running
    /// slowly), x Motion effects; subtle with "reduce effects". Recycled particles, no allocation per frame.
    /// Put it on the camera.
    /// </summary>
    [DefaultExecutionOrder(56)]
    public sealed class SpeedStreaks : MonoBehaviour
    {
        public SpeedFeel feel;
        [Tooltip("A material using JamKit/Movement/SpeedMotes (its _Shapes pick motes or confetti and petals; its rim is the dark outline).")]
        public Material material;
        [Tooltip("Colours the motes are drawn in, picked at random per mote.")]
        public Color[] palette = { new Color(0.93f, 0.94f, 0.97f), new Color(0.84f, 0.86f, 0.9f) };
        [Tooltip("A slow drift each mote has (m/s): petals floating down in the dream; zero for still dust.")]
        public Vector3 drift = Vector3.zero;
        [Tooltip("Random drift on top, per axis (m/s).")]
        public float driftJitter = 0f;
        [Tooltip("The most motes alive at once (recycled).")]
        public int maxParticles = 600;
        [Tooltip("The most motes emitted in one frame (a hitch never bursts).")]
        public int maxPerFrame = 12;

        ParticleSystem _ps;
        ParticleSystemRenderer _renderer;
        ParticleSystem.EmitParams _emit;
        float _accum;
        uint _rng = 0x9E3779B9u;
        int _emitted;
        float _rate;
        MaterialPropertyBlock _block;
        static readonly int CenterClearId = Shader.PropertyToID("_CenterClear");

        /// <summary>Motes per second right now (follows the tier).</summary>
        public float Rate => _rate;
        /// <summary>Motes emitted so far.</summary>
        public int Emitted => _emitted;
        /// <summary>Motes alive now.</summary>
        public int Alive => _ps != null ? _ps.particleCount : 0;
        public ParticleSystem System => _ps;

        void Start()
        {
            if (material == null) { enabled = false; return; }
            _ps = MakeSystem(transform, "Speed streaks", material, ParticleSystemRenderMode.Stretch, maxParticles, true);
            _renderer = _ps.GetComponent<ParticleSystemRenderer>();
            _renderer.velocityScale = 0f;
            _renderer.lengthScale = 1f;
            _renderer.cameraVelocityScale = 0.001f;
            _emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            _block = new MaterialPropertyBlock();
        }

        /// <summary>A world-space particle system emitted by hand (shared by the speed visuals): no emission or shape
        /// module, a fade in and out over life, our motes shader with the stable random in TEXCOORD0.z.</summary>
        internal static ParticleSystem MakeSystem(Transform parent, string name, Material mat, ParticleSystemRenderMode mode, int max, bool fadeIn)
        {
            var go = new GameObject(name);
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, max);
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 0.1f;
            main.gravityModifier = 0f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                fadeIn
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = mode;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.StableRandomX,
            });
            ps.Play();
            return ps;
        }

        void LateUpdate()
        {
            if (_ps == null || feel == null || feel.body == null || feel.body.Sim == null) return;
            var l = feel.Levels;
            var d = feel.Def ?? new SpeedFeelDef();
            float dt = Time.deltaTime;
            _rate = l.streakRate;
            _renderer.cameraVelocityScale = Mathf.Max(0.001f, l.streakStretch);
            // The clear centre from the tuning (near motes fade out of the middle of the screen).
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(CenterClearId, new Vector4(d.centerClear, d.centerClearNear, 0f, 0f));
            _renderer.SetPropertyBlock(_block);
            if (_rate <= 0f || dt <= 0f) { _accum = 0f; return; }

            var body = feel.body;
            Vector3 v = body.Velocity;
            v.y = 0f;
            float speed = Mathf.Max(v.magnitude, 1f);
            Vector3 fwd = v.sqrMagnitude > 0.25f ? v.normalized : Flat(transform.forward);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 eye = transform.position;

            _accum += _rate * dt;
            int n = 0;
            while (_accum >= 1f && n < maxPerFrame)
            {
                _accum -= 1f;
                n++;
                float pass = Mathf.Lerp(d.streakPassMin, d.streakPassMax, Next());
                float ahead = Mathf.Clamp(speed * pass, d.streakAheadMin, d.streakAheadMax);
                float x = 0f, y = 0f;
                for (int k = 0; k < 6; k++)
                {
                    x = (Next() * 2f - 1f) * d.streakSpread;
                    y = Mathf.Lerp(-d.streakBelow, d.streakAbove, Next());
                    if (x * x + y * y >= d.streakClear * d.streakClear) break;
                }
                if (x * x + y * y < d.streakClear * d.streakClear) x = (x < 0f ? -1f : 1f) * d.streakClear;
                _emit.position = eye + fwd * ahead + right * x + Vector3.up * y;
                _emit.velocity = drift + new Vector3(Next() - 0.5f, Next() - 0.5f, Next() - 0.5f) * (2f * driftJitter);
                _emit.startLifetime = ahead / speed + 0.6f;
                _emit.startSize = Mathf.Lerp(d.streakSizeMin, d.streakSizeMax, Next());
                Color c = palette != null && palette.Length > 0 ? palette[(int)(Next() * palette.Length) % palette.Length] : Color.white;
                c.a *= l.streakAlpha;
                _emit.startColor = c;
                _emit.randomSeed = NextUInt();
                _ps.Emit(_emit, 1);
                _emitted++;
            }
            if (_accum > maxPerFrame) _accum = maxPerFrame;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        uint NextUInt()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return _rng;
        }

        float Next() => (NextUInt() & 0xFFFFFF) / 16777216f;
    }
}
