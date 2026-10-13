using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Breacher's look (docs/SKYLINE-GUN-BREACHER.md; Task 19), driven only by <see cref="SkylinePlayer.CueRaised"/> and
    /// <see cref="SkylinePlayer.HitLanded"/>, so the player's code doesn't know it exists. It adds itself to every
    /// SkylinePlayer when a scene loads (no builder or scene edit needed). Its knockback is felt three ways:
    /// <list type="bullet">
    /// <item>the shot: a muzzle blast on the viewmodel (a white-hot core and spikes for three frames, then smoke), a flash of
    /// light on the world, a shockwave ring thrown out along the aim, a dust kick where the centre of the spread lands
    /// within 4 m, and the cell's window dimming as its charge is spent;</item>
    /// <item>bots: sparks at every pellet that lands, one heavy <c>breacher/impact</c> per shot, and plates the Breacher
    /// sheds kicked away along the shot (the bots' own code already slides them);</item>
    /// <item>a boost (an accepted self-push in the air, read from the decision log): <c>breacher/boost</c>, and a blast under
    /// your feet: a dust ring racing out over the floor the spread hit, or, with no floor within 6 m, a pressure puff of
    /// white rings and vapour under you.</item>
    /// </list>
    /// After each shot the cell vents (the "vent" event): a jet of vapour from its port. A seated cell flashes hot.
    /// Everything is pooled and made up front; materials are SkylineBotLook's shared lit and glowing ones.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class SkylineBreacherFx : MonoBehaviour
    {
        public const string BoostSound = "breacher/boost", ImpactSound = "breacher/impact";

        // ---- tuning (presentation only; the gun's numbers live in SkylineGuns.Breacher and BreacherBoostDef)
        const float FlashLife = 0.045f, SpikeLength = 0.13f, LightLife = 0.07f, LightRange = 7f, LightIntensity = 4f;
        const float RingLife = 0.14f, RingDistance = 1.1f, RingFrom = 0.12f, RingTo = 1.15f;
        const float DustRange = 4f, BoostFloorRange = 6f, DustRingTo = 2.6f, DustRingLife = 0.28f;
        const float PlateFling = 7f, PlateLift = 2f, PlateSearch = 1.3f;
        const float CellDip = 0.4f, CellHot = 0.35f;
        const int Chunks = 40, Puffs = 28, Rings = 6, Spikes = 6, Smokes = 6, Vents = 8;

        static readonly Color Hot = new Color(6f, 3.6f, 1.4f);
        static readonly Color RingHot = new Color(2.6f, 2.1f, 1.5f);
        static readonly Color BoostRing = new Color(2.2f, 2.4f, 2.6f);
        static readonly Color Dust = new Color(0.6f, 0.56f, 0.5f);
        static readonly Color Smoke = new Color(0.52f, 0.52f, 0.55f);
        static readonly Color Vapour = new Color(0.86f, 0.88f, 0.92f);
        static readonly Color CellFull = new Color(1f, 0.6f, 0.12f), CellSpent = new Color(0.22f, 0.08f, 0.02f), CellSeat = new Color(1f, 0.95f, 0.7f);

        public SkylinePlayer player;

        sealed class Bit
        {
            public Transform t;
            public Vector3 vel;
            public float age, life, size, to, drag, gravity;
            public int kind;          // 0 chunk (shrinks), 1 puff (grows, then shrinks), 2 ring (eases out), 3 spike, 4 core
            public bool local;        // moves in its parent's space (the viewmodel)
            public Vector3 baseScale; // spikes: (w, w, length)
        }

        readonly List<Bit> _chunks = new List<Bit>(), _puffs = new List<Bit>(), _rings = new List<Bit>();
        readonly List<Bit> _spikes = new List<Bit>(), _smokes = new List<Bit>(), _vents = new List<Bit>();
        readonly List<Bit> _live = new List<Bit>();
        readonly List<(Vector3 point, Vector3 dir)> _plateKicks = new List<(Vector3, Vector3)>();
        readonly HashSet<int> _kicked = new HashSet<int>();
        int _nChunk, _nPuff, _nRing, _nSmoke, _nVent;
        Bit _core;
        Transform _worldRoot, _vmRoot, _flashRoot;
        Light _light;
        float _lightLeft;
        int _lightPrewarm = 3;   // the first frames: a faint light at the eye, so the extra-light shader variants are built at
                                 // scene start (Task 17's hitch rule), not on the first shot
        static Mesh _ringMesh;

        // the viewmodel's Breacher (found once the viewmodel has built its rig)
        Transform _vmCam, _gunRoot, _muzzle, _mag, _ventPort;
        Material _cellMat;
        bool _cellEmission;
        Color _cellShown = new Color(-1f, 0f, 0f);
        float _dipLeft, _hotLeft;

        bool _pendingShot;
        float _shotTime;
        Vector3 _shotAim;
        int _impactFrame = -1;

        // ---------------------------------------------------------------- installing itself

        static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _installed = false;
            _ringMesh = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!_installed)
            {
                SceneManager.sceneLoaded += (s, m) => AttachAll();
                _installed = true;
            }
            AttachAll();
        }

        /// <summary>Gives every SkylinePlayer in the loaded scenes its Breacher effects (once each).</summary>
        public static void AttachAll()
        {
            foreach (var p in Object.FindObjectsByType<SkylinePlayer>(FindObjectsSortMode.None))
                if (p != null && p.GetComponent<SkylineBreacherFx>() == null) p.gameObject.AddComponent<SkylineBreacherFx>().player = p;
        }

        // ---------------------------------------------------------------- life cycle

        void Awake()
        {
            if (player == null) player = GetComponent<SkylinePlayer>();
        }

        void OnEnable()
        {
            if (player == null) player = GetComponent<SkylinePlayer>();
            if (player == null) return;
            player.CueRaised += OnCue;
            player.HitLanded += OnHit;
        }

        void OnDisable()
        {
            if (player == null) return;
            player.CueRaised -= OnCue;
            player.HitLanded -= OnHit;
        }

        void Start()
        {
            _worldRoot = new GameObject("Breacher FX").transform;
            var lit = SkylineBotLook.Lit(Dust);
            for (int i = 0; i < Chunks; i++) _chunks.Add(MakeBit(PrimitiveType.Cube, _worldRoot, lit, 0));
            for (int i = 0; i < Puffs; i++) _puffs.Add(MakeBit(PrimitiveType.Sphere, _worldRoot, SkylineBotLook.Lit(Vapour), 1));
            for (int i = 0; i < Rings; i++) _rings.Add(MakeRing(_worldRoot, SkylineBotLook.Glow(RingHot)));
            SkylineBotLook.Glow(BoostRing);
            SkylineBotLook.Lit(Smoke);
            var lightGo = new GameObject("Breacher flash light");
            lightGo.transform.SetParent(_worldRoot, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.62f, 0.3f);
            _light.range = LightRange;
            _light.intensity = 0.001f;
            _light.shadows = LightShadows.None;
            _light.enabled = true;
            if (player != null) _light.transform.position = player.EyePosition;
        }

        void OnDestroy()
        {
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            if (_vmRoot != null) Destroy(_vmRoot.gameObject);
        }

        Bit MakeBit(PrimitiveType type, Transform parent, Material m, int kind)
        {
            var go = SkylineBotLook.Visual(type, "fx", parent, m, false);
            go.SetActive(false);
            return new Bit { t = go.transform, kind = kind };
        }

        Bit MakeRing(Transform parent, Material m)
        {
            var go = new GameObject("fx ring");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = RingMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return new Bit { t = go.transform, kind = 2 };
        }

        /// <summary>A flat ring (inner radius 0.86, outer 1) in the XY plane, facing both ways.</summary>
        static Mesh RingMesh()
        {
            if (_ringMesh != null) return _ringMesh;
            const int seg = 48;
            var v = new Vector3[seg * 2];
            var tri = new int[seg * 12];
            for (int i = 0; i < seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v[i * 2] = d * 0.86f;
                v[i * 2 + 1] = d;
                int j = (i + 1) % seg, k = i * 12;
                int a0 = i * 2, a1 = i * 2 + 1, b0 = j * 2, b1 = j * 2 + 1;
                tri[k] = a0; tri[k + 1] = b0; tri[k + 2] = a1;
                tri[k + 3] = a1; tri[k + 4] = b0; tri[k + 5] = b1;
                tri[k + 6] = a0; tri[k + 7] = a1; tri[k + 8] = b0;
                tri[k + 9] = a1; tri[k + 10] = b1; tri[k + 11] = b0;
            }
            _ringMesh = new Mesh { name = "Breacher ring", vertices = v, triangles = tri };
            _ringMesh.RecalculateNormals();
            _ringMesh.RecalculateBounds();
            return _ringMesh;
        }

        /// <summary>Finds the viewmodel's Breacher (the gun root, its Muzzle, its cell and the cell's vent port) once the
        /// viewmodel has built its rig, and builds the viewmodel-side effects there.</summary>
        bool Resolve()
        {
            if (_gunRoot != null) return true;
            var vm = player != null ? player.viewmodel : null;
            if (vm == null || vm.OverlayCamera == null) return false;
            _vmCam = vm.OverlayCamera.transform;
            _gunRoot = _vmCam.Find("Gun " + SkylineGuns.BreacherId);
            if (_gunRoot == null) return false;
            _muzzle = FindDeep(_gunRoot, "Muzzle");
            _mag = FindDeep(_gunRoot, "Mag");
            _ventPort = FindDeep(_gunRoot, "CellVent");
            FixModelFacing();

            _vmRoot = new GameObject("Breacher FX (viewmodel)").transform;
            _vmRoot.SetParent(_vmCam, false);
            _flashRoot = new GameObject("Breacher muzzle blast").transform;
            _flashRoot.SetParent(_muzzle != null ? _muzzle : _gunRoot, false);
            var hot = SkylineBotLook.Glow(Hot);
            _core = MakeBit(PrimitiveType.Sphere, _flashRoot, hot, 4);
            _core.local = true;
            for (int i = 0; i < Spikes; i++)
            {
                var s = MakeBit(PrimitiveType.Cube, _flashRoot, hot, 3);
                s.local = true;
                _spikes.Add(s);
            }
            for (int i = 0; i < Smokes; i++)
            {
                var s = MakeBit(PrimitiveType.Sphere, _vmRoot, SkylineBotLook.Lit(Smoke), 1);
                s.local = true;
                _smokes.Add(s);
            }
            for (int i = 0; i < Vents; i++)
            {
                var s = MakeBit(PrimitiveType.Sphere, _vmRoot, SkylineBotLook.Lit(Vapour), 1);
                s.local = true;
                _vents.Add(s);
            }
            SetupCell();
            return true;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>The model's muzzle must point down the gun's +Z. If an import turned the model (an axis convention gone
        /// wrong), turn it back so the barrel faces forward, and say so once.</summary>
        void FixModelFacing()
        {
            if (_muzzle == null || _gunRoot == null) return;
            Transform model = _muzzle;
            while (model.parent != null && model.parent != _gunRoot) model = model.parent;
            if (model == _muzzle || model.parent != _gunRoot) return;   // the primitive fallback: built facing forward
            Vector3 m = _gunRoot.InverseTransformPoint(_muzzle.position);
            if (m.magnitude < 0.1f) return;
            Vector3 want = new Vector3(0f, 0.09f, 1f).normalized;
            if (Vector3.Angle(m, want) < 30f) return;
            model.localRotation = Quaternion.FromToRotation(m.normalized, want) * model.localRotation;
            Debug.Log("SKYLINE_BREACHER_FX the model's muzzle pointed " + m + "; turned to face forward");
        }

        void SetupCell()
        {
            if (_mag == null) return;
            var r = _mag.GetComponent<Renderer>();
            if (r == null) r = _mag.GetComponentInChildren<Renderer>();
            if (r == null) return;
            var mats = r.materials;   // this renderer's own copies, made once
            int pick = 0;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.IndexOf("Glow", System.StringComparison.OrdinalIgnoreCase) >= 0) { pick = i; break; }
            _cellMat = mats[pick];
            _cellEmission = _cellMat.HasProperty("_EmissionColor");
            if (_cellEmission)
            {
                _cellMat.EnableKeyword("_EMISSION");
                _cellMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        // ---------------------------------------------------------------- the hooks

        int BreacherSlot()
        {
            var l = player != null ? player.Loadout : null;
            if (l == null) return -1;
            for (int i = 0; i < 2; i++) if (l[i] != null && l[i].Def.id == SkylineGuns.BreacherId) return i;
            return -1;
        }

        void OnCue(GunCue cue, int slot)
        {
            if (_worldRoot == null || player == null || player.Loadout == null || slot != BreacherSlot()) return;
            if (cue.kind == GunCueKind.Shot)
            {
                Resolve();
                Shot();
            }
            else if (cue.kind == GunCueKind.Event && cue.ev.id == "vent")
            {
                Resolve();
                Vent();
            }
            else if (cue.kind == GunCueKind.Event && cue.ev.id == "seat") _hotLeft = CellHot;
        }

        void OnHit(SkylineHitInfo info, SkylineHitResult r)
        {
            if (info.gun == null || info.gun.id != SkylineGuns.BreacherId) return;
            SkylineSpark.Burst(info.point, info.normal, 3, 5.5f);
            if (_impactFrame != Time.frameCount)
            {
                _impactFrame = Time.frameCount;   // one heavy slam per shot, not nine
                SkylineSounds.PlayAt(ImpactSound, info.point, 1f);
            }
            if (r.damage.plateShed >= 0) _plateKicks.Add((info.point, info.direction));
        }

        // ---------------------------------------------------------------- the shot

        void Shot()
        {
            Vector3 eye = player.EyePosition, aim = player.AimDirection;
            _pendingShot = true;
            _shotTime = Time.time;
            _shotAim = aim;
            _dipLeft = CellDip;

            // the viewmodel's muzzle blast: a white-hot core and spikes for three frames
            if (_core != null && _gunRoot != null && _flashRoot != null)
            {
                _flashRoot.rotation = _gunRoot.rotation;
                Spawn(_core, Vector3.zero, Vector3.zero, FlashLife, 0.11f, 0f, 0f, 0f);
                for (int i = 0; i < _spikes.Count; i++)
                {
                    var s = _spikes[i];
                    float a = (i / (float)_spikes.Count) * 360f + Random.Range(-20f, 20f);
                    var dir = Quaternion.Euler(0f, 0f, a) * Quaternion.Euler(Random.Range(28f, 70f), 0f, 0f) * Vector3.forward;
                    if (i == 0) dir = Vector3.forward;
                    float len = SpikeLength * (i == 0 ? 1.6f : Random.Range(0.6f, 1.1f));
                    s.baseScale = new Vector3(0.016f, 0.016f, len);
                    Spawn(s, dir * len * 0.5f, Vector3.zero, FlashLife, 1f, 0f, 0f, 0f);
                    s.t.localRotation = Quaternion.LookRotation(dir);
                }
                // smoke rolling off the muzzle (in the viewmodel camera's space, so it hangs while the gun kicks)
                Vector3 mz = _vmCam.InverseTransformPoint(_muzzle != null ? _muzzle.position : _gunRoot.position);
                for (int k = 0; k < 3; k++)
                {
                    var s = Next(_smokes, ref _nSmoke);
                    var v = new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.05f, 0.18f), Random.Range(0.15f, 0.4f));
                    Spawn(s, mz + v * 0.05f, v, 0.5f, 0.025f, 0.11f, 3f, -0.05f);
                }
            }

            // light on the world
            Vector3 muzzleWorld = player.viewmodel != null ? player.viewmodel.MuzzleWorld() : eye + aim * 0.6f;
            _light.transform.position = muzzleWorld + aim * 0.3f;
            _light.enabled = true;
            _lightLeft = LightLife;

            // the shockwave thrown out along the aim
            var ring = Next(_rings, ref _nRing);
            ring.t.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Glow(RingHot);
            Spawn(ring, eye + aim * RingDistance, Vector3.zero, RingLife, RingFrom, RingTo, 0f, 0f);
            ring.t.rotation = Quaternion.LookRotation(aim);

            // a dust kick where the centre of the spread lands, when it lands close
            if (Physics.Raycast(eye, aim, out var hit, DustRange, SkylineShooting.Mask, QueryTriggerInteraction.Ignore))
            {
                DustBurst(hit.point, hit.normal, 6, 3.5f);
                SkylineSpark.Burst(hit.point, hit.normal, 4, 4f);
            }
        }

        void Vent()
        {
            _dipLeft = Mathf.Max(_dipLeft, 0.15f);
            if (_vmCam == null || _gunRoot == null) return;
            Vector3 port = _ventPort != null ? _ventPort.position : (_mag != null ? _mag.position : _gunRoot.position);
            Vector3 p = _vmCam.InverseTransformPoint(port);
            Vector3 side = _vmCam.InverseTransformDirection(_gunRoot.right);
            Vector3 fwd = _vmCam.InverseTransformDirection(_gunRoot.forward);
            for (int k = 0; k < 5; k++)
            {
                var s = Next(_vents, ref _nVent);
                Vector3 v = side * Random.Range(0.5f, 0.9f) + fwd * Random.Range(0.05f, 0.3f) + Vector3.up * Random.Range(0.0f, 0.12f);
                Spawn(s, p, v, Random.Range(0.25f, 0.4f), 0.008f, 0.05f, 5f, 0.1f);
            }
        }

        // ---------------------------------------------------------------- the boost

        void Boost(Vector3 aim)
        {
            Vector3 feet = player.transform.position;
            bool down = -aim.y > (player.BoostDef != null ? player.BoostDef.minLift : 0.35f);
            SkylineSounds.PlayViewmodel(BoostSound, down ? 1f : 0.6f);
            if (Physics.Raycast(player.EyePosition, aim, out var hit, BoostFloorRange, SkylineShooting.Mask, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.5f)
            {
                // the blast hit the floor: a dust ring races out over it, with a pressure ring and a flash
                var ring = Next(_rings, ref _nRing);
                ring.t.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Glow(RingHot);
                Spawn(ring, hit.point + hit.normal * 0.04f, Vector3.zero, DustRingLife, 0.3f, DustRingTo, 0f, 0f);
                ring.t.rotation = Quaternion.LookRotation(hit.normal);
                Vector3 tangent = Vector3.Cross(hit.normal, Vector3.right);
                if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(hit.normal, Vector3.forward);
                tangent.Normalize();
                for (int i = 0; i < 18; i++)
                {
                    var c = Next(_chunks, ref _nChunk);
                    Vector3 d = Quaternion.AngleAxis(i * 20f + Random.Range(-8f, 8f), hit.normal) * tangent;
                    Vector3 v = d * Random.Range(5f, 8f) + hit.normal * Random.Range(0.5f, 2.2f);
                    Spawn(c, hit.point + hit.normal * 0.05f, v, Random.Range(0.45f, 0.7f), Random.Range(0.05f, 0.09f), 0f, 3.5f, 9.8f);
                    c.t.rotation = Random.rotation;
                }
                for (int i = 0; i < 8; i++)
                {
                    var p = Next(_puffs, ref _nPuff);
                    Vector3 d = Quaternion.AngleAxis(i * 45f + Random.Range(-10f, 10f), hit.normal) * tangent;
                    Spawn(p, hit.point + d * 0.3f + hit.normal * 0.1f, d * Random.Range(1.5f, 2.5f) + hit.normal * 0.6f, 0.65f, 0.12f, 0.45f, 2.5f, -0.3f);
                    p.t.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Lit(Dust);
                }
                _light.transform.position = hit.point + hit.normal * 0.5f;
                _light.enabled = true;
                _lightLeft = LightLife * 1.5f;
            }
            else
            {
                // nothing under you: the pressure itself, a pale ring and vapour bursting out under your feet
                Vector3 at = feet - aim * 0.2f;
                Vector3 n = -aim;
                for (int k = 0; k < 2; k++)
                {
                    var ring = Next(_rings, ref _nRing);
                    ring.t.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Glow(BoostRing);
                    Spawn(ring, at - n * (0.25f * k), Vector3.zero, DustRingLife * (1f + 0.4f * k), 0.2f, 1.6f + 0.6f * k, 0f, 0f);
                    ring.t.rotation = Quaternion.LookRotation(n);
                }
                Vector3 tangent = Vector3.Cross(n, Vector3.right);
                if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(n, Vector3.forward);
                tangent.Normalize();
                for (int i = 0; i < 10; i++)
                {
                    var p = Next(_puffs, ref _nPuff);
                    Vector3 d = Quaternion.AngleAxis(i * 36f + Random.Range(-10f, 10f), n) * tangent;
                    Spawn(p, at + d * 0.15f, d * Random.Range(2f, 3.2f) - n * Random.Range(0.5f, 1.5f), 0.55f, 0.1f, 0.35f, 3f, 0f);
                    p.t.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Lit(Vapour);
                }
            }
        }

        void DustBurst(Vector3 point, Vector3 normal, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var c = Next(_chunks, ref _nChunk);
                Vector3 v = (normal + Random.insideUnitSphere * 0.9f).normalized * speed * Random.Range(0.5f, 1f);
                Spawn(c, point + normal * 0.04f, v, Random.Range(0.35f, 0.55f), Random.Range(0.03f, 0.06f), 0f, 4f, 9.8f);
                c.t.rotation = Random.rotation;
            }
            var p = Next(_puffs, ref _nPuff);
            Spawn(p, point + normal * 0.12f, normal * 0.8f, 0.5f, 0.08f, 0.32f, 3f, -0.2f);
            p.t.GetComponent<Renderer>().sharedMaterial = SkylineBotLook.Lit(Dust);
        }

        // ---------------------------------------------------------------- particles

        static Bit Next(List<Bit> pool, ref int next)
        {
            var b = pool[next];
            next = (next + 1) % pool.Count;
            return b;
        }

        void Spawn(Bit b, Vector3 pos, Vector3 vel, float life, float size, float to, float drag, float gravity)
        {
            if (b == null || b.t == null) return;
            if (b.local) b.t.localPosition = pos;
            else b.t.position = pos;
            b.vel = vel;
            b.age = 0f;
            b.life = life;
            b.size = size;
            b.to = to;
            b.drag = drag;
            b.gravity = gravity;
            if (!b.t.gameObject.activeSelf) b.t.gameObject.SetActive(true);
            if (!_live.Contains(b)) _live.Add(b);
            Shape(b, 0f);
        }

        static void Shape(Bit b, float u)
        {
            switch (b.kind)
            {
                case 0: b.t.localScale = Vector3.one * (b.size * (1f - u)); break;
                case 1:
                {
                    float s = Mathf.Lerp(b.size, b.to, 1f - (1f - u) * (1f - u));
                    if (u > 0.65f) s *= 1f - (u - 0.65f) / 0.35f;
                    b.t.localScale = Vector3.one * s;
                    break;
                }
                case 2:
                {
                    float r = Mathf.Lerp(b.size, b.to, 1f - (1f - u) * (1f - u) * (1f - u));
                    b.t.localScale = new Vector3(r, r, 1f);
                    break;
                }
                case 3: b.t.localScale = new Vector3(b.baseScale.x * (1f - u * 0.5f), b.baseScale.y * (1f - u * 0.5f), b.baseScale.z * (1f - u)); break;
                case 4: b.t.localScale = Vector3.one * (b.size * (1f - u * 0.6f)); break;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var b = _live[i];
                if (b.t == null) { _live.RemoveAt(i); continue; }
                b.age += dt;
                if (b.age >= b.life)
                {
                    b.t.gameObject.SetActive(false);
                    _live.RemoveAt(i);
                    continue;
                }
                if (b.vel.sqrMagnitude > 1e-8f || b.gravity != 0f)
                {
                    b.vel += Vector3.down * (b.gravity * dt);
                    b.vel *= Mathf.Exp(-b.drag * dt);
                    if (b.local) b.t.localPosition += b.vel * dt;
                    else b.t.position += b.vel * dt;
                }
                Shape(b, b.age / b.life);
            }
            if (_lightPrewarm > 0 && --_lightPrewarm == 0 && _lightLeft <= 0f) _light.enabled = false;
            if (_lightLeft > 0f)
            {
                _lightLeft -= dt;
                _light.intensity = _lightLeft > 0f ? LightIntensity * (_lightLeft / LightLife) : 0f;
                if (_lightLeft <= 0f) _light.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (player == null) return;
            if (_gunRoot == null) Resolve();

            // a boost is a self-push the engine accepted: SkylinePlayer logs it right after the shot this frame
            if (_pendingShot)
            {
                _pendingShot = false;
                var log = player.Decisions;
                if (log != null && log.Count > 0)
                {
                    var last = log[log.Count - 1];
                    if (last.kind == DecisionKind.Boost && last.time == _shotTime) Boost(_shotAim);
                }
            }

            KickPlates();
            Cell(Time.deltaTime);
        }

        /// <summary>Plates the Breacher shed this frame fly off along the shot, harder than the bots' own throw.</summary>
        void KickPlates()
        {
            if (_plateKicks.Count == 0) return;
            var debris = Object.FindObjectsByType<SkylineDebris>(FindObjectsSortMode.None);
            if (_kicked.Count > 512) _kicked.Clear();
            for (int i = 0; i < debris.Length; i++)
            {
                var d = debris[i];
                if (d == null) continue;
                var rb = d.GetComponent<Rigidbody>();
                if (rb == null || !_kicked.Add(rb.GetInstanceID())) continue;
                for (int k = 0; k < _plateKicks.Count; k++)
                {
                    var (point, dir) = _plateKicks[k];
                    if ((rb.position - point).sqrMagnitude > PlateSearch * PlateSearch) continue;
                    Vector3 flat = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
                    rb.AddForce((flat * PlateFling + Vector3.up * PlateLift) * rb.mass, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.Impulse);
                    break;
                }
            }
            _plateKicks.Clear();
        }

        /// <summary>The cell's window shows its charge: amber when full, dark when spent; it dims for a moment as a shot
        /// draws on it, and flashes white-hot when a fresh cell seats.</summary>
        void Cell(float dt)
        {
            if (_cellMat == null) return;
            int slot = BreacherSlot();
            if (slot < 0) return;
            var gun = player.Loadout[slot];
            int mag = gun.Def.weapon.magazineSize;
            float charge = mag > 0 ? Mathf.Clamp01(gun.Ammo / (float)mag) : 0f;
            Color c = Color.Lerp(CellSpent, CellFull, charge);
            float glow = Mathf.Lerp(0.3f, 3f, charge);
            if (_dipLeft > 0f)
            {
                _dipLeft -= dt;
                float k = Mathf.Clamp01(_dipLeft / CellDip);
                glow *= 1f - 0.75f * k;
            }
            if (_hotLeft > 0f)
            {
                _hotLeft -= dt;
                float k = Mathf.Clamp01(_hotLeft / CellHot);
                c = Color.Lerp(c, CellSeat, k);
                glow = Mathf.Lerp(glow, 7f, k);
            }
            Color shown = new Color(c.r * glow, c.g * glow, c.b * glow, 1f);
            if ((shown - _cellShown).maxColorComponent < 0.01f && (_cellShown - shown).maxColorComponent < 0.01f) return;
            _cellShown = shown;
            if (_cellEmission) _cellMat.SetColor("_EmissionColor", shown);
            else _cellMat.color = c;
        }
    }
}
