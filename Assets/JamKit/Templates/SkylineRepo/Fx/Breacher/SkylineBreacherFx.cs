using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Breacher's look (docs/SKYLINE-GUN-BREACHER.md; Task 19), driven only by <see cref="SkylinePlayer.CueRaised"/> and
    /// <see cref="SkylinePlayer.HitLanded"/>, so the player's code doesn't know it exists. It adds itself to every
    /// SkylinePlayer when a Skyline scene loads (no builder or scene edit needed). Its knockback is felt three ways:
    /// <list type="bullet">
    /// <item>the shot: a hot muzzle blast on the viewmodel (a fireball core and a star of flame for about four frames, then a
    /// wisp of smoke), a flash of light on the world, a soft pressure ring and a short blast cloud out of the muzzle, a dust
    /// kick where the centre of the spread lands on the world within 4 m, and the cell's window dimming as its charge is
    /// spent;</item>
    /// <item>bots: dark chips and flakes of their white-and-gold casing and a dull grey dust puff at every pellet that
    /// lands, one heavy <c>breacher/impact</c> per shot, and plates the Breacher sheds kicked away along the shot (the bots'
    /// own code already slides them). No glow and no sparks on a live bot: there they mean a Writ spot (the spec's
    /// damage-feedback rule, docs/SKYLINE-DESIGN.md 14), and the spot's spark pool is never touched;</item>
    /// <item>a boost (an accepted self-push in the air, read from the decision log): <c>breacher/boost</c>, and a blast under
    /// your feet: a dust ring racing out over the floor the spread hit, or, with no floor within 6 m, a pressure puff of
    /// soft rings and vapour under you.</item>
    /// </list>
    /// After each shot the cell vents (the "vent" event): a jet of vapour from its port. A seated cell flashes hot.
    /// <para>Smoke, dust, vapour, flame and the pressure rings are soft particles: camera-facing quads with a generated
    /// radial texture, drawn with the always-included Sprites/Default shader (unlit, alpha-blended, never writing depth),
    /// fading out as they grow. Only the casing chips and the floor's debris chunks are solid (lit, small, short-lived).
    /// Everything is pooled and made up front (a plate kick uses a non-allocating physics overlap); the cell's window
    /// material copies are freed with the component.</para>
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class SkylineBreacherFx : MonoBehaviour
    {
        public const string BoostSound = "breacher/boost", ImpactSound = "breacher/impact";

        // ---- tuning (presentation only; the gun's numbers live in SkylineGuns.Breacher and BreacherBoostDef)
        const float FlashLife = 0.065f, LightLife = 0.07f, LightRange = 7f, LightIntensity = 4f;
        // the pressure ring: centred on the muzzle (low right of the view), 0.7 m out along the aim, at most 0.55 m wide,
        // soft and gone in 0.2 s, so it reads as the blast leaving the barrel, not a reticle over the target
        const float RingLife = 0.2f, RingDistance = 0.7f, RingFrom = 0.12f, RingTo = 0.55f;
        const float DustRange = 4f, BoostFloorRange = 6f, DustRingTo = 2.6f, DustRingLife = 0.28f;
        // round 2 (N3): plates fly further, so the shove reads past the camera kick
        const float PlateFling = 11f, PlateLift = 3f, PlateSearch = 1.3f;
        const float CellDip = 0.4f, CellHot = 0.35f;
        const int Chunks = 40, Chips = 40, Rings = 4, Softs = 64, SoftRings = 6, VmSofts = 24, Flames = 8;

        static readonly Color Hot = new Color(5f, 2.6f, 0.8f, 1f);
        static readonly Color FlameHot = new Color(4f, 1.7f, 0.45f, 1f);
        static readonly Color RingHot = new Color(2.6f, 2.1f, 1.5f);
        static readonly Color PressureRing = new Color(2.0f, 1.45f, 0.85f, 0.75f);
        static readonly Color BoostRing = new Color(1.6f, 1.75f, 1.9f, 0.7f);
        static readonly Color Dust = new Color(0.6f, 0.56f, 0.5f);
        static readonly Color DustSoft = new Color(0.66f, 0.62f, 0.55f, 0.45f);
        static readonly Color DullDust = new Color(0.45f, 0.45f, 0.47f, 0.4f);
        static readonly Color BlastSmoke = new Color(0.55f, 0.5f, 0.46f, 0.32f);
        static readonly Color GunSmoke = new Color(0.62f, 0.62f, 0.64f, 0.3f);
        static readonly Color Vapour = new Color(0.9f, 0.92f, 0.95f, 0.5f);
        static readonly Color CellFull = new Color(1f, 0.6f, 0.12f), CellSpent = new Color(0.22f, 0.08f, 0.02f), CellSeat = new Color(1f, 0.95f, 0.7f);

        public SkylinePlayer player;

        sealed class Bit
        {
            public Transform t;
            public Renderer r;
            public Vector3 vel;
            public float age, life, size, to, drag, gravity, spin, length;
            // 0 chunk (shrinks), 2 glow ring (eases out), 5 flake (a flat chip that shrinks),
            // 6 soft puff (camera-facing, grows and fades), 7 soft ring (faces its own way, grows and fades),
            // 8 flame petal (camera-facing, stretched, shrinks fast)
            public int kind;
            public bool local;        // moves in its parent's space (the viewmodel)
            public Color color;       // soft kinds: HDR tint, alpha = starting opacity
        }

        readonly List<Bit> _chunks = new List<Bit>(), _rings = new List<Bit>(), _chips = new List<Bit>();
        readonly List<Bit> _softs = new List<Bit>(), _softRings = new List<Bit>(), _vmSofts = new List<Bit>(), _flames = new List<Bit>();
        readonly List<Bit> _live = new List<Bit>();
        static readonly Collider[] _overlap = new Collider[48];
        readonly List<(Vector3 point, Vector3 dir)> _plateKicks = new List<(Vector3, Vector3)>();
        readonly HashSet<int> _kicked = new HashSet<int>();
        int _nChunk, _nRing, _nChip, _nSoft, _nSoftRing, _nVmSoft;
        Transform _worldRoot, _vmRoot, _flashRoot;
        Light _light;
        float _lightLeft;
        int _lightPrewarm = 3;   // the first frames: a faint light at the eye, so the extra-light shader variants are built at
                                 // scene start (Task 17's hitch rule), not on the first shot
        MaterialPropertyBlock _block;
        static Mesh _ringMesh, _quad;
        static Texture2D _puffTex, _ringTex;
        static Material _puffMat, _ringMat;

        // the viewmodel's Breacher (found once the viewmodel has built its rig)
        Transform _vmCam, _gunRoot, _muzzle, _mag, _ventPort;
        Material _cellMat;
        Material[] _cellMats;   // the cell renderer's own material copies (destroyed in OnDestroy)
        SkylineBreacherLook _look;   // the gun's painted-metal, gunmetal and polymer materials (made in code)
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
            _quad = null;
            _puffTex = null;
            _ringTex = null;
            _puffMat = null;
            _ringMat = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!_installed)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;   // never twice, even with domain reload off
                SceneManager.sceneLoaded += OnSceneLoaded;
                _installed = true;
            }
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (IsSkylineScene(SceneManager.GetSceneAt(i))) { AttachAll(); break; }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsSkylineScene(scene)) AttachAll();
        }

        /// <summary>Skyline Repo's scenes (SkylineArena, SkylineFiringRange, SkylineGallery); every other template's scenes
        /// (Visiting Hours included) are left alone.</summary>
        static bool IsSkylineScene(Scene scene) => scene.IsValid() && scene.name.StartsWith("Skyline", System.StringComparison.Ordinal);

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
            _block = new MaterialPropertyBlock();
            _worldRoot = new GameObject("Breacher FX").transform;
            var lit = SkylineBotLook.Lit(Dust);
            for (int i = 0; i < Chunks; i++) _chunks.Add(MakeBit(PrimitiveType.Cube, _worldRoot, lit, 0));
            for (int i = 0; i < Rings; i++) _rings.Add(MakeRing(_worldRoot, SkylineBotLook.Glow(RingHot)));
            for (int i = 0; i < Chips; i++) _chips.Add(MakeBit(PrimitiveType.Cube, _worldRoot, SkylineBotLook.Lit(SkylineBotLook.White), 5));
            for (int i = 0; i < Softs; i++) _softs.Add(MakeSoft(_worldRoot, PuffMaterial(), 6));
            for (int i = 0; i < SoftRings; i++) _softRings.Add(MakeSoft(_worldRoot, RingMaterial(), 7));
            SkylineBotLook.Lit(SkylineBotLook.Gold);
            var lightGo = new GameObject("Breacher flash light");
            lightGo.transform.SetParent(_worldRoot, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.62f, 0.3f);
            _light.range = LightRange;
            _light.intensity = 0.001f;
            _light.shadows = LightShadows.None;
            _light.enabled = true;
            if (player != null)
            {
                _light.transform.position = player.EyePosition;
                // one invisible puff and ring in front of the eye for the first frames: their shader is built at scene start
                Vector3 front = player.EyePosition + player.AimDirection * 0.6f;
                Soft(Next(_softs, ref _nSoft), front, Vector3.zero, 0.1f, 0.01f, 0.01f, 0f, 0f, new Color(1f, 1f, 1f, 0.004f));
                Soft(Next(_softRings, ref _nSoftRing), front, Vector3.zero, 0.1f, 0.01f, 0.01f, 0f, 0f, new Color(1f, 1f, 1f, 0.004f));
            }
        }

        void OnDestroy()
        {
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            if (_vmRoot != null) Destroy(_vmRoot.gameObject);
            if (_cellMats != null)
                for (int i = 0; i < _cellMats.Length; i++) if (_cellMats[i] != null) Destroy(_cellMats[i]);
            _cellMats = null;
            if (_look != null) _look.Dispose();
            _look = null;
            _cellMat = null;
        }

        Bit MakeBit(PrimitiveType type, Transform parent, Material m, int kind)
        {
            var go = SkylineBotLook.Visual(type, "fx", parent, m, false);
            go.SetActive(false);
            return new Bit { t = go.transform, r = go.GetComponent<Renderer>(), kind = kind };
        }

        static Bit MakeSoft(Transform parent, Material m, int kind)
        {
            var go = new GameObject(kind == 7 ? "fx soft ring" : kind == 8 ? "fx flame" : "fx puff");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new Bit { t = go.transform, r = r, kind = kind };
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
            return new Bit { t = go.transform, r = r, kind = 2 };
        }

        // ---------------------------------------------------------------- soft particle assets (made once, in code)

        /// <summary>A unit quad facing +Z with white vertex colours (the sprite shader multiplies by them).</summary>
        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            _quad = new Mesh { name = "Breacher soft quad" };
            _quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            return _quad;
        }

        /// <summary>A soft, lumpy round puff: alpha falls off smoothly from the centre, broken by a few low-frequency lumps so
        /// it reads as smoke rather than a disc.</summary>
        static Texture2D PuffTexture()
        {
            if (_puffTex != null) return _puffTex;
            const int n = 64;
            _puffTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Breacher puff", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Atan2(v, u);
                    float lump = 0.82f + 0.1f * Mathf.Sin(a * 3f + 0.7f) + 0.08f * Mathf.Sin(a * 5f + 2.1f) + 0.06f * Mathf.Sin(r * 9f);
                    float k = Mathf.Clamp01(1f - r / lump);
                    float alpha = k * k * (3f - 2f * k);
                    byte shade = (byte)(215 + 40 * Mathf.Clamp01(1f - r));
                    px[y * n + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
                }
            _puffTex.SetPixels32(px);
            _puffTex.Apply(true, true);
            return _puffTex;
        }

        /// <summary>A soft ring: a bright band at 80% of the radius with feathered edges.</summary>
        static Texture2D RingTexture()
        {
            if (_ringTex != null) return _ringTex;
            const int n = 128;
            _ringTex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Breacher ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float d = (r - 0.8f) / 0.11f;
                    float alpha = r > 1f ? 0f : Mathf.Exp(-d * d) + 0.12f * Mathf.Clamp01(1f - Mathf.Abs(r - 0.6f) / 0.25f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            _ringTex.SetPixels32(px);
            _ringTex.Apply(true, true);
            return _ringTex;
        }

        /// <summary>The soft particles' material: Sprites/Default (in GraphicsSettings' always-included shaders, so it is in
        /// every build; unlit, alpha-blended, no depth write, both faces), tinted per particle through _Color.</summary>
        static Material SoftMaterial(Texture2D tex, string name)
        {
            var sh = Shader.Find("Sprites/Default");
            Material m = sh != null ? new Material(sh) : new Material(SkylineBotLook.Glow(Color.white));
            m.name = name;
            m.mainTexture = tex;
            return m;
        }

        static Material PuffMaterial() => _puffMat != null ? _puffMat : (_puffMat = SoftMaterial(PuffTexture(), "Breacher puff"));
        static Material RingMaterial() => _ringMat != null ? _ringMat : (_ringMat = SoftMaterial(RingTexture(), "Breacher ring"));

        /// <summary>A flat ring (inner radius 0.86, outer 1) in the XY plane, facing both ways (the boost's floor ring).</summary>
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
            _muzzle = SkylineBotBodies.Find(_gunRoot, "Muzzle");
            _mag = SkylineBotBodies.Find(_gunRoot, "Mag");
            _ventPort = SkylineBotBodies.Find(_gunRoot, "CellVent");
            FixModelFacing();

            _vmRoot = new GameObject("Breacher FX (viewmodel)").transform;
            _vmRoot.SetParent(_vmCam, false);
            _flashRoot = new GameObject("Breacher muzzle blast").transform;
            _flashRoot.SetParent(_muzzle != null ? _muzzle : _gunRoot, false);
            for (int i = 0; i < Flames; i++)
            {
                var f = MakeSoft(_flashRoot, PuffMaterial(), 8);
                f.local = true;
                _flames.Add(f);
            }
            for (int i = 0; i < VmSofts; i++)
            {
                var s = MakeSoft(_vmRoot, PuffMaterial(), 6);
                s.local = true;
                _vmSofts.Add(s);
            }
            _look = new SkylineBreacherLook();
            _look.Apply(_gunRoot);   // before SetupCell, so the cell's own copies are made from the textured materials
            SetupCell();
            return true;
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
            var mats = r.materials;   // this renderer's own copies, made once and destroyed in OnDestroy
            _cellMats = mats;
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
            if (_worldRoot == null || info.gun == null || info.gun.id != SkylineGuns.BreacherId) return;
            CasingChips(info.point, info.normal, info.direction);
            Lurch(info);
            if (_impactFrame != Time.frameCount)
            {
                _impactFrame = Time.frameCount;   // one heavy slam per shot, not nine
                SkylineSounds.PlayAt(ImpactSound, info.point, 1f);
            }
            if (r.damage.plateShed >= 0) _plateKicks.Add((info.point, info.direction));
        }

        /// <summary>Tips and spins the bot this pellet landed on (SkylineShoveLurch), a pellet's share of a whole shot.</summary>
        void Lurch(SkylineHitInfo info)
        {
            int n = Physics.OverlapSphereNonAlloc(info.point, 0.06f, _overlap, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var bot = _overlap[i] != null ? _overlap[i].GetComponentInParent<SkylineBot>() : null;
                if (bot == null) continue;
                int pellets = info.gun != null && info.gun.pellets > 0 ? info.gun.pellets : 1;
                SkylineShoveLurch.On(bot)?.Kick(info.direction, 1f / pellets);
                return;
            }
        }

        // ---------------------------------------------------------------- the shot

        void Shot()
        {
            Vector3 eye = player.EyePosition, aim = player.AimDirection;
            _pendingShot = true;
            _shotTime = Time.time;
            _shotAim = aim;
            _dipLeft = CellDip;

            // the viewmodel's muzzle blast: a fireball core with two smaller balls down the barrel's line, and a star of
            // flame petals in the view's plane, for about four frames; then a wisp of smoke off the muzzle
            if (_gunRoot != null && _flashRoot != null && _vmCam != null)
            {
                Vector3 fwd = _gunRoot.forward;
                int f = 0;
                for (int k = 0; k < 3 && f < _flames.Count; k++, f++)
                {
                    var b = _flames[f];
                    Vector3 at = _flashRoot.position + fwd * (0.05f * k);
                    Flame(b, at, 0f, 0.2f - 0.05f * k, 0f, FlashLife * (1f - 0.15f * k), FlameHot * (k == 0 ? 1.25f : 1f));
                }
                for (; f < _flames.Count; f++)
                {
                    var b = _flames[f];
                    float angle = (f - 3) * (360f / (_flames.Count - 3)) + Random.Range(-18f, 18f);
                    float len = Random.Range(0.14f, 0.24f);
                    Vector3 dir = _vmCam.rotation * (Quaternion.Euler(0f, 0f, angle) * Vector3.up);
                    Flame(b, _flashRoot.position + dir * (len * 0.45f), angle, 0.06f, len, FlashLife, FlameHot);
                }
                Vector3 mz = _vmCam.InverseTransformPoint(_flashRoot.position);
                for (int k = 0; k < 3; k++)
                {
                    var s = Next(_vmSofts, ref _nVmSoft);
                    var v = new Vector3(Random.Range(-0.03f, 0.08f), Random.Range(0.08f, 0.2f), Random.Range(0.05f, 0.2f));
                    Soft(s, mz + v * 0.1f, v, Random.Range(0.4f, 0.6f), 0.04f, 0.15f, 2.5f, -0.06f, GunSmoke);
                }
            }

            // light on the world
            Vector3 muzzleWorld = player.viewmodel != null ? player.viewmodel.MuzzleWorld() : eye + aim * 0.6f;
            _light.transform.position = muzzleWorld + aim * 0.3f;
            _light.enabled = true;
            _lightLeft = LightLife;

            // the pressure leaving the barrel: a soft warm ring and a short blast cloud from the muzzle along the aim
            var ring = Next(_softRings, ref _nSoftRing);
            Soft(ring, muzzleWorld + aim * RingDistance, Vector3.zero, RingLife, RingFrom, RingTo, 0f, 0f, PressureRing);
            ring.t.rotation = Quaternion.LookRotation(aim);
            for (int k = 0; k < 4; k++)
            {
                var c = Next(_softs, ref _nSoft);
                Vector3 v = (aim + Random.insideUnitSphere * 0.25f).normalized * Random.Range(4f, 6.5f);
                Soft(c, muzzleWorld + aim * 0.25f, v, Random.Range(0.35f, 0.5f), 0.15f, Random.Range(0.45f, 0.65f), 7f, -0.2f, BlastSmoke);
            }

            // a dust kick where the centre of the spread lands, when it lands close
            // (the world only: a bot's own hits are its casing chips, from HitLanded; no sparks anywhere, since the effects
            // can't tell world metal from tile and sparks on a bot would read as a Writ spot)
            if (Physics.Raycast(eye, aim, out var hit, DustRange, SkylineShooting.Mask, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<SkylineHitZone>() == null)
                DustBurst(hit.point, hit.normal, 6, 3.5f);
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
                var s = Next(_vmSofts, ref _nVmSoft);
                Vector3 v = side * Random.Range(0.5f, 0.9f) + fwd * Random.Range(0.05f, 0.3f) + Vector3.up * Random.Range(0.0f, 0.12f);
                Soft(s, p, v, Random.Range(0.3f, 0.45f), 0.015f, 0.08f, 5f, 0.05f, Vapour);
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
                // the blast hit the floor: a hot ring races out over it, debris skids outward, a dust cloud rolls up
                var ring = Next(_rings, ref _nRing);
                ring.r.sharedMaterial = SkylineBotLook.Glow(RingHot);
                Spawn(ring, hit.point + hit.normal * 0.04f, Vector3.zero, DustRingLife, 0.3f, DustRingTo, 0f, 0f);
                ring.t.rotation = Quaternion.LookRotation(hit.normal);
                Vector3 tangent = Tangent(hit.normal);
                for (int i = 0; i < 18; i++)
                {
                    var c = Next(_chunks, ref _nChunk);
                    Vector3 d = Quaternion.AngleAxis(i * 20f + Random.Range(-8f, 8f), hit.normal) * tangent;
                    Vector3 v = d * Random.Range(5f, 8f) + hit.normal * Random.Range(0.5f, 2.2f);
                    Spawn(c, hit.point + hit.normal * 0.05f, v, Random.Range(0.45f, 0.7f), Random.Range(0.05f, 0.09f), 0f, 3.5f, 9.8f);
                    c.t.rotation = Random.rotation;
                }
                for (int i = 0; i < 10; i++)
                {
                    var p = Next(_softs, ref _nSoft);
                    Vector3 d = Quaternion.AngleAxis(i * 36f + Random.Range(-10f, 10f), hit.normal) * tangent;
                    Soft(p, hit.point + d * 0.3f + hit.normal * 0.15f, d * Random.Range(2f, 3.5f) + hit.normal * 0.5f,
                         Random.Range(0.7f, 0.95f), 0.3f, Random.Range(0.9f, 1.3f), 2.5f, -0.3f, DustSoft);
                }
                _light.transform.position = hit.point + hit.normal * 0.5f;
                _light.enabled = true;
                _lightLeft = LightLife * 1.5f;
            }
            else
            {
                // nothing under you: the pressure itself, soft pale rings and vapour bursting out under your feet
                Vector3 at = feet - aim * 0.2f;
                Vector3 n = -aim;
                for (int k = 0; k < 2; k++)
                {
                    var ring = Next(_softRings, ref _nSoftRing);
                    Soft(ring, at - n * (0.25f * k), Vector3.zero, DustRingLife * (1.2f + 0.4f * k), 0.3f, 1.8f + 0.6f * k, 0f, 0f, BoostRing);
                    ring.t.rotation = Quaternion.LookRotation(n);
                }
                Vector3 tangent = Tangent(n);
                for (int i = 0; i < 10; i++)
                {
                    var p = Next(_softs, ref _nSoft);
                    Vector3 d = Quaternion.AngleAxis(i * 36f + Random.Range(-10f, 10f), n) * tangent;
                    Soft(p, at + d * 0.15f, d * Random.Range(2f, 3.2f) - n * Random.Range(0.5f, 1.5f), 0.6f, 0.2f, 0.7f, 3f, 0f, Vapour);
                }
            }
        }

        static Vector3 Tangent(Vector3 n)
        {
            Vector3 t = Vector3.Cross(n, Vector3.right);
            if (t.sqrMagnitude < 0.01f) t = Vector3.Cross(n, Vector3.forward);
            return t.normalized;
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
            for (int k = 0; k < 2; k++)
            {
                var p = Next(_softs, ref _nSoft);
                Soft(p, point + normal * 0.12f, (normal + Random.insideUnitSphere * 0.4f) * 0.8f, Random.Range(0.45f, 0.6f), 0.12f, 0.45f, 3f, -0.2f, DustSoft);
            }
        }

        /// <summary>A pellet on a live bot: three dark chips and flakes of its white-and-gold casing knocked off along the
        /// shot and the surface, and a soft dull grey dust puff that fades in a third of a second. Never glowing.</summary>
        void CasingChips(Vector3 point, Vector3 normal, Vector3 dir)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : -dir.normalized;
            Vector3 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : -n;
            for (int i = 0; i < 3; i++)
            {
                var c = Next(_chips, ref _nChip);
                c.r.sharedMaterial = SkylineBotLook.Lit(i == 2 ? SkylineBotLook.Gold : SkylineBotLook.White);
                Vector3 v = (n * 0.8f + d * 0.6f + Random.insideUnitSphere * 0.7f).normalized * Random.Range(2.5f, 5f);
                Spawn(c, point + n * 0.03f, v, Random.Range(0.4f, 0.6f), Random.Range(0.025f, 0.045f), 0f, 2.5f, 9.8f);
                c.t.rotation = Random.rotation;
            }
            var p = Next(_softs, ref _nSoft);
            Soft(p, point + n * 0.08f, n * 0.6f + d * 0.4f, 0.35f, 0.06f, 0.26f, 3f, -0.1f, DullDust);
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

        void Soft(Bit b, Vector3 pos, Vector3 vel, float life, float size, float to, float drag, float gravity, Color color)
        {
            if (b == null) return;
            b.color = color;
            b.spin = Random.Range(0f, 360f);
            Spawn(b, pos, vel, life, size, to, drag, gravity);
        }

        /// <summary>A flame petal or fireball (world position <paramref name="at"/>, under the muzzle's blast root).</summary>
        void Flame(Bit b, Vector3 at, float angle, float width, float length, float life, Color color)
        {
            if (b == null) return;
            b.color = color;
            b.spin = angle;
            b.length = length;
            Spawn(b, _flashRoot.InverseTransformPoint(at), Vector3.zero, life, width, 0f, 0f, 0f);
        }

        void Shape(Bit b, float u)
        {
            switch (b.kind)
            {
                case 0: b.t.localScale = Vector3.one * (b.size * (1f - u)); break;
                case 2:
                {
                    float r = Mathf.Lerp(b.size, b.to, 1f - (1f - u) * (1f - u) * (1f - u));
                    b.t.localScale = new Vector3(r, r, 1f);
                    break;
                }
                case 5: b.t.localScale = new Vector3(b.size, b.size * 0.25f, b.size * 0.7f) * (1f - u); break;
                case 6:
                case 7:
                {
                    float s = Mathf.Lerp(b.size, b.to, 1f - (1f - u) * (1f - u));
                    b.t.localScale = new Vector3(s, s, 1f);
                    float fadeIn = Mathf.Clamp01(u / 0.08f);
                    Tint(b, b.color.a * fadeIn * Mathf.Pow(1f - u, 1.6f), 1f);
                    if (b.kind == 6) Face(b, b.spin);
                    break;
                }
                case 8:
                {
                    float w = b.size * (1f - 0.4f * u);
                    float l = b.length > 0f ? b.length * (1f - 0.7f * u) : w;
                    b.t.localScale = new Vector3(w, l, 1f);
                    Tint(b, Mathf.Clamp01(1.2f * (1f - u)), 1f - 0.5f * u);
                    Face(b, b.spin);
                    break;
                }
            }
        }

        void Tint(Bit b, float alpha, float intensity)
        {
            if (b.r == null || _block == null) return;
            var c = b.color;
            _block.SetColor("_Color", new Color(c.r * intensity, c.g * intensity, c.b * intensity, alpha));
            b.r.SetPropertyBlock(_block);
        }

        /// <summary>Turns a soft quad to face the camera that draws it (the main camera in the world, the viewmodel's overlay
        /// camera for the gun's own effects), rolled by <paramref name="roll"/> degrees.</summary>
        void Face(Bit b, float roll)
        {
            Transform cam = b.local ? _vmCam : (player != null && player.Camera != null ? player.Camera.transform : null);
            if (cam == null) return;
            b.t.rotation = cam.rotation * Quaternion.Euler(0f, 0f, roll);
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
            if (_kicked.Count > 512) _kicked.Clear();
            Physics.SyncTransforms();   // the plates were thrown this frame: make sure the overlap sees them
            for (int k = 0; k < _plateKicks.Count; k++)
            {
                var (point, dir) = _plateKicks[k];
                Vector3 flat = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
                int n = Physics.OverlapSphereNonAlloc(point, PlateSearch, _overlap, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var rb = _overlap[i] != null ? _overlap[i].attachedRigidbody : null;
                    if (rb == null || rb.isKinematic || rb.GetComponent<SkylineDebris>() == null) continue;   // shed parts only
                    if (!_kicked.Add(rb.GetInstanceID())) continue;   // once each (a plate has several colliders)
                    rb.AddForce((flat * PlateFling + Vector3.up * PlateLift) * rb.mass, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * 6f, ForceMode.Impulse);
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
