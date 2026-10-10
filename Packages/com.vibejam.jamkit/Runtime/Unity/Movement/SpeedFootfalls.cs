using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// V5, footfall puffs and glowing footprints (docs/SPEED-FEEL.md): at fast and above, each footstep kicks up a
    /// puff (dust in the lab, a small pastel bloom in the dream), on the ground or off a wall you run on; at flying,
    /// each ground step also leaves a glowing footprint that fades behind you. Timed by the controller's foot plants
    /// (<see cref="MovementController.Stepped"/>, which foot from <see cref="MovementController.StridePhase"/>). Two
    /// hand-emitted, world-space particle systems, recycled; no allocation per frame. Counts x Motion effects.
    /// Put it on the camera.
    /// </summary>
    [DefaultExecutionOrder(57)]
    public sealed class SpeedFootfalls : MonoBehaviour
    {
        public SpeedFeel feel;
        [Tooltip("JamKit/Movement/SpeedMotes for the puffs (soft puff shape in the lab, blooms in the dream).")]
        public Material puffMaterial;
        [Tooltip("JamKit/Movement/SpeedMotes, additive (_DstBlend One), shoe-print shape: the glowing footprints.")]
        public Material footprintMaterial;
        public Color[] puffColors = { new Color(0.78f, 0.76f, 0.72f, 0.75f), new Color(0.86f, 0.84f, 0.8f, 0.7f) };
        public Color footprintColor = new Color(0.55f, 0.85f, 1f, 0.9f);
        [Tooltip("How far each foot lands from the middle (m).")]
        public float footSpread = 0.13f;
        [Tooltip("Turns the footprints if they point the wrong way on screen (degrees).")]
        public float footprintYawOffset = 0f;
        [Tooltip("Puffs drift up this fast (m/s) besides spreading out.")]
        public float puffRise = 0.35f;

        ParticleSystem _puffs, _prints;
        ParticleSystem.EmitParams _emit;
        uint _rng = 0x2545F491u;
        int _puffCount, _printCount, _steps;
        int _lastPrintFrame = -1;

        /// <summary>Puff particles emitted so far.</summary>
        public int PuffsEmitted => _puffCount;
        /// <summary>Footprints laid so far.</summary>
        public int FootprintsLaid => _printCount;
        /// <summary>Foot plants seen at fast or above.</summary>
        public int FastSteps => _steps;
        public int LastFootprintFrame => _lastPrintFrame;

        void Start()
        {
            if (puffMaterial != null) _puffs = SpeedStreaks.MakeSystem(transform, "Footfall puffs", puffMaterial, ParticleSystemRenderMode.Billboard, 160, false);
            if (footprintMaterial != null) _prints = SpeedStreaks.MakeSystem(transform, "Footprints", footprintMaterial, ParticleSystemRenderMode.HorizontalBillboard, 48, false);
            if (_puffs != null)
            {
                var size = _puffs.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            }
            _emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        }

        void LateUpdate()
        {
            if (feel == null || feel.body == null || feel.body.Sim == null) return;
            var body = feel.body;
            if (!body.Stepped) return;
            var l = feel.Levels;
            if (l.puffs <= 0 && !l.footprints) return;
            _steps++;
            var d = feel.Def ?? new SpeedFeelDef();
            var sim = body.Sim;
            bool wall = sim.Mode == MoveMode.Wallrun;
            int foot = ((int)Mathf.Floor(body.StridePhase)) & 1;
            float side = foot == 0 ? -1f : 1f;
            Vector3 feet = body.RenderPosition;
            Vector3 v = body.Velocity;
            v.y = 0f;
            Vector3 fwd = v.sqrMagnitude > 0.25f ? v.normalized : body.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 at, away;
            if (wall)
            {
                // The foot meets the wall about knee height, on the wall's side.
                var n = sim.WallNormal;
                away = new Vector3(n.X, n.Y, n.Z);
                at = feet + Vector3.up * 0.5f - away * body.Def.radius;
            }
            else
            {
                away = Vector3.up;
                at = feet + right * (side * footSpread) + Vector3.up * 0.04f;
            }

            if (_puffs != null && l.puffs > 0)
            {
                for (int i = 0; i < l.puffs; i++)
                {
                    Vector3 r = new Vector3(Next() * 2f - 1f, Next() * 0.5f, Next() * 2f - 1f);
                    Vector3 spread = wall ? (away + r * 0.8f) : new Vector3(r.x, 0f, r.z);
                    _emit.position = at + spread * 0.05f;
                    _emit.velocity = spread.normalized * (d.puffSpeed * Mathf.Lerp(0.6f, 1.2f, Next())) + Vector3.up * puffRise - fwd * 0.4f;
                    _emit.startLifetime = d.puffLife * Mathf.Lerp(0.8f, 1.2f, Next());
                    _emit.startSize = d.puffSize * Mathf.Lerp(0.7f, 1.3f, Next());
                    _emit.startColor = puffColors != null && puffColors.Length > 0 ? puffColors[(int)(Next() * puffColors.Length) % puffColors.Length] : Color.white;
                    _emit.rotation = 0f;
                    _emit.randomSeed = NextUInt();
                    _puffs.Emit(_emit, 1);
                    _puffCount++;
                }
            }

            if (_prints != null && l.footprints && !wall)
            {
                _emit.position = feet + right * (side * footSpread) + Vector3.up * 0.025f;
                _emit.velocity = Vector3.zero;
                _emit.startLifetime = d.footprintLife;
                _emit.startSize = d.footprintSize;
                _emit.startColor = footprintColor;
                _emit.rotation = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg + footprintYawOffset;
                _emit.randomSeed = NextUInt();
                _prints.Emit(_emit, 1);
                _printCount++;
                _lastPrintFrame = Time.frameCount;
            }
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
