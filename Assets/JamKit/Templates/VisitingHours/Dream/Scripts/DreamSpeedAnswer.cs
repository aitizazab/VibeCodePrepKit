using JamKit.Movement;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// V7, the dream answers your speed (docs/SPEED-FEEL.md; RESEARCH-MOVEMENT 8.4 rule 5, "speed gets a visible
    /// reward"). At flying: the dream brightens and saturates (<see cref="SpeedScreenFx.SetGrade"/>), small flowers
    /// open on the ground beside your line a few metres ahead, and the static's wall is drawn a little further back
    /// (<see cref="DreamStaticView.visualRecede"/>). Everything follows the flying blend x Motion effects.
    /// VISUAL ONLY: the pursuer's distance, speed and catch are untouched (they live in core Pursuer and
    /// DreamEscapeGame). Flowers are one hand-emitted, recycled particle system; no allocation per frame.
    /// </summary>
    [DefaultExecutionOrder(58)]
    public sealed class DreamSpeedAnswer : MonoBehaviour
    {
        public SpeedFeel feel;
        public SpeedScreenFx screen;
        public DreamStaticView staticView;
        [Tooltip("JamKit/Movement/SpeedMotes with the flower shape (_Shapes = 3, 1).")]
        public Material flowerMaterial;
        public Color[] flowerColors =
        {
            new Color(0.98f, 0.74f, 0.80f), new Color(0.80f, 0.74f, 0.96f), new Color(0.66f, 0.80f, 0.96f), new Color(1f, 0.80f, 0.66f),
        };
        [Tooltip("Flowers open this far ahead of you (m, min..max), at least this far beside your line and at most that far.")]
        public float aheadMin = 5f, aheadMax = 16f, besideMin = 1.3f, besideMax = 3.8f;
        [Tooltip("What the flowers can stand on (the dream's solid pieces).")]
        public LayerMask groundMask = ~(1 << 2);

        ParticleSystem _flowers;
        ParticleSystem.EmitParams _emit;
        float _accum;
        uint _rng = 0x68E31DA4u;
        int _opened;
        float _answer;

        /// <summary>How much the dream answers now, 0..1 (flying x Motion effects).</summary>
        public float Answer => _answer;
        /// <summary>Flowers opened so far.</summary>
        public int FlowersOpened => _opened;
        public ParticleSystem Flowers => _flowers;

        void Start()
        {
            if (flowerMaterial != null)
            {
                _flowers = MakeFlowers(transform, flowerMaterial);
            }
            _emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, velocity = Vector3.zero };
        }

        static ParticleSystem MakeFlowers(Transform parent, Material mat)
        {
            var go = new GameObject("Flowers (the dream answers)");
            go.layer = 2;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.startSpeed = 0f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            // Open quickly, stay, close at the end.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, 1.1f), new Keyframe(0.2f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
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
            if (feel == null) return;
            var l = feel.Levels;
            var d = feel.Def ?? new SpeedFeelDef();
            _answer = l.dream;
            if (screen != null) screen.SetGrade(d.dreamSaturation * _answer, d.dreamBrightness * _answer);
            if (staticView != null) staticView.visualRecede = d.staticRecede * _answer;
            float dt = Time.deltaTime;
            if (_flowers == null || feel.body == null || dt <= 0f || _answer <= 0.05f) { _accum = 0f; return; }

            var body = feel.body;
            Vector3 v = body.Velocity;
            v.y = 0f;
            Vector3 fwd = v.sqrMagnitude > 0.25f ? v.normalized : body.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 feet = body.RenderPosition;
            _accum += d.flowerRate * _answer * dt;
            int n = 0;
            while (_accum >= 1f && n < 4)
            {
                _accum -= 1f;
                n++;
                float side = Next() < 0.5f ? -1f : 1f;
                Vector3 probe = feet + fwd * Mathf.Lerp(aheadMin, aheadMax, Next()) + right * (side * Mathf.Lerp(besideMin, besideMax, Next())) + Vector3.up * 2.5f;
                if (!Physics.Raycast(probe, Vector3.down, out var hit, 6f, groundMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.7f) continue;
                float size = d.flowerSize * Mathf.Lerp(0.7f, 1.2f, Next());
                _emit.position = hit.point + Vector3.up * (size * 0.45f);
                _emit.startLifetime = d.flowerLife * Mathf.Lerp(0.85f, 1.15f, Next());
                _emit.startSize = size;
                _emit.startColor = flowerColors != null && flowerColors.Length > 0 ? flowerColors[(int)(Next() * flowerColors.Length) % flowerColors.Length] : Color.white;
                _emit.randomSeed = NextUInt();
                _flowers.Emit(_emit, 1);
                _opened++;
            }
            if (_accum > 4f) _accum = 4f;
        }

        void OnDisable()
        {
            if (screen != null) screen.SetGrade(0f, 0f);
            if (staticView != null) staticView.visualRecede = 0f;
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
