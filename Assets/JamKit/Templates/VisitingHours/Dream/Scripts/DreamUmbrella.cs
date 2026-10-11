using JamKit.Movement;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The front door into the rain (docs/DREAM-ESCAPE.md "The umbrella and the rain"). A closed umbrella leans by the
    /// door, glowing gold ("use this"). Nadia takes it with the dream tool she learnt in the Movement Lab: the shell's
    /// Fire action (left mouse, RT), the same aim assist (core <see cref="DreamToolAim"/>: 6 degrees on a mouse, 8 on a
    /// pad, a halo when it locks), the same pulse and sounds. Then it opens over her and the rain can't touch her.
    /// Without it the rain soaks her (core <see cref="DreamRain"/>): she runs and jumps heavier and the edges of the
    /// view darken, a modest placeholder cost. Going on past the point of no return without it is the Despair route:
    /// the save's <see cref="DreamRain.DespairFlag"/> is set (read it with <see cref="DreamEscapeGame.Despair"/>;
    /// only the debug line shows it for now). Placed before the porch (a new run, a respawn, a chapter), the umbrella
    /// is back by the door.
    /// Test hook: while the course autopilot drives and <see cref="autoFire"/> is on, it takes the umbrella when near.
    /// </summary>
    public sealed class DreamUmbrella : MonoBehaviour
    {
        /// <summary>The scene's run (the dream escape, the Night 2 house): an <see cref="IDreamRainHost"/>.</summary>
        public MonoBehaviour host;
        /// <summary>The closed umbrella by the door, and its gold glow (shown while it waits; bigger when locked).</summary>
        public Transform prop;
        public Transform glow;
        /// <summary>The open umbrella over her head (a child of the camera's rig), shown once she has it.</summary>
        public GameObject canopy;
        /// <summary>The rain over the path past the door.</summary>
        public ParticleSystem rainFx;
        public Transform beam;
        public Renderer beamRenderer;
        public DreamToolDef tool = new DreamToolDef();
        public DreamRainDef rainDef = new DreamRainDef();
        public bool autoFire = true;
        public LayerMask sightMask = ~(1 << 2);
        [Range(0f, 1f)] public float volume = 0.8f;

        static DreamUmbrella _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        public static DreamUmbrella Current => _current;

        DreamRainSpot _d;
        IDreamRainHost _host;
        DreamRain _rain;
        MovementTuning _tuning, _heavyTuning;
        MoveDef _base, _baseDef, _heavy;
        bool _heavyOn, _locked, _debugShown = true;
        float _beamT = 1f, _glowBase = 1f;
        Vector3 _beamFrom, _beamTo;
        AudioSource _voice, _loop;
        AudioClip _fire, _take, _dry, _lockTick;
        VisualElement _vignette;
        Label _debug;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock _block;
        Color _beamColor;
        int _lastDespairShown = -1;

        public DreamRain Rain => _rain;
        public bool Locked => _locked;
        public bool HasUmbrella => _rain != null && _rain.HasUmbrella;
        /// <summary>The movement is the heavier, soaked copy of the tuning right now.</summary>
        public bool Heavy => _heavyOn;

        /// <summary>A new door and rain for this run (the Night 2 house: Lena condemning Nadia brings the point of no return
        /// nearer and soaks faster). Puts the umbrella back by the door.</summary>
        public void UseSpot(DreamRainSpot spot, DreamRainDef def)
        {
            if (!TryInit()) return;
            SetHeavy(false);
            _d = spot;
            rainDef = def;
            _rain = new DreamRain(spot) { def = def };
            Show();
        }

        void Start()
        {
            _host = host as IDreamRainHost;
            if (_host == null || !GameRoot.Exists) { enabled = false; return; }
            _current = this;
            _block = new MaterialPropertyBlock();
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _loop = gameObject.AddComponent<AudioSource>();
            _loop.playOnAwake = false;
            _loop.loop = true;
            _loop.spatialBlend = 0f;
            _loop.volume = 0f;
            _loop.clip = RainLoop();
            if (!Application.isBatchMode) _loop.Play();
            _fire = Clip("dt_fire"); _take = Clip("dt_build"); _dry = Clip("dt_dry"); _lockTick = Clip("dt_lock");
            if (glow != null) _glowBase = glow.localScale.x;
            if (beamRenderer != null && beamRenderer.sharedMaterial != null && beamRenderer.sharedMaterial.HasProperty(ColorId)) _beamColor = beamRenderer.sharedMaterial.GetColor(ColorId);
            if (beam != null) beam.gameObject.SetActive(false);
            BuildUi();
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
            SetHeavy(false);
            if (_heavyTuning != null) Destroy(_heavyTuning);
            _vignette?.RemoveFromHierarchy();
            _debug?.RemoveFromHierarchy();
        }

        bool TryInit()
        {
            if (_rain != null) return true;
            if (_host == null) _host = host as IDreamRainHost;
            if (_host == null || _host.RainSpot == null || _host.Player == null) return false;
            _d = _host.RainSpot;
            _rain = new DreamRain(_d) { def = rainDef };
            // The soaked movement is a runtime copy of the tuning (the asset is never touched), so everything else it holds
            // (the body's feel) stays the same.
            _tuning = _host.Player.tuning;
            _baseDef = _host.Player.def;
            _base = _host.Player.Def;
            _heavyTuning = _tuning != null ? Instantiate(_tuning) : null;
            if (_heavyTuning != null) _heavyTuning.name = _tuning.name + " (soaked, runtime copy)";
            _heavy = _heavyTuning != null ? _heavyTuning.def : _base.Clone();
            Show();
            return true;
        }

        static AudioClip Clip(string id)
        {
            var reg = GameRoot.Exists ? GameRoot.Audio.ClipFor(id) : null;
            if (reg != null) return reg;
            var data = DreamToolSounds.Make(id);
            var clip = AudioClip.Create("gen_" + id, data.Length, 1, DreamToolSounds.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A soft rain hiss, two seconds, seamless (the tail crossfaded onto the head). Placeholder.</summary>
        static AudioClip RainLoop()
        {
            const int rate = 22050;
            int n = rate * 2, fade = rate / 4;
            var b = new float[n + fade];
            var r = new System.Random(7);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float x = (float)r.NextDouble() * 2f - 1f;
                lp += 0.35f * (x - lp);
                hp = lp - hp * 0.02f;
                float drop = r.NextDouble() < 0.004 ? (float)r.NextDouble() * 0.6f : 0f;
                b[i] = 0.25f * hp + drop;
            }
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = b[i];
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = data[i] * k + b[n + i] * (1f - k); }
            var clip = AudioClip.Create("gen_dream_rain", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(AudioClip clip, float vol)
        {
            if (clip == null || Application.isBatchMode || GameRoot.Audio.Muted) return;
            _voice.PlayOneShot(clip, vol * volume * GameRoot.Settings.sfxVolume);
        }

        bool UsingPad => GameRoot.Input.LookFromGamepad || (GameRoot.Input.Fire.activeControl != null && GameRoot.Input.Fire.activeControl.device is Gamepad);

        void Update()
        {
            if (!TryInit()) return;
            float dt = Time.deltaTime;
            float s = _host.S;
            // Placed before the porch (a new run, a respawn, a chapter jump): the umbrella is back by the door.
            if (s < _d.resetS && (_rain.HasUmbrella || _rain.InRain || _rain.Soak > 0f || _rain.DespairThisRun))
            {
                _rain.Reset();
                Show();
            }
            bool playing = GameRoot.Flow.Current == JamKit.Flow.GameState.Playing && !_host.Respawning;
            if (playing)
            {
                var ev = _rain.Tick(dt, s);
                if (ev.despairChosen) ChooseDespair();
                Aim(s);
            }
            else SetLock(false);
            SetHeavy(_rain.Soak > 0.001f);
            if (_heavyOn)
            {
                float k = _rain.SpeedScale, j = _rain.JumpScale;
                _heavy.walkSpeed = _base.walkSpeed * k;
                _heavy.sprintSpeed = _base.sprintSpeed * k;
                _heavy.jumpHeight = _base.jumpHeight * j;
                _heavy.doubleJumpHeight = _base.doubleJumpHeight * j;
            }
            Effects(dt, s);
            DebugLine();
        }

        void Aim(float s)
        {
            if (_rain.HasUmbrella || s < _d.resetS - 10f) { SetLock(false); return; }
            var cam = _host.Player.cam;
            if (cam == null) return;
            var eye = cam.transform.position;
            var fwd = cam.transform.forward;
            var e = new System.Numerics.Vector3(eye.x, eye.y, eye.z);
            bool locks = _rain.Locks(e, new System.Numerics.Vector3(fwd.x, fwd.y, fwd.z), UsingPad ? tool.conePad : tool.coneMouse, tool.range) && InSight(eye);
            SetLock(locks);
            if (_host.AutopilotOn)
            {
                if (autoFire && Vector3.Distance(eye, Prop()) < 14f) Take();
                return;
            }
            if (GameRoot.Input.Pressed(GameRoot.Input.Fire))
            {
                if (_locked) Take();
                else
                {
                    // A shot at nothing: the same puff as the lab's tool.
                    Pulse(eye + fwd * tool.range, eye);
                    Play(_fire, 0.6f);
                    Play(_dry, 0.5f);
                }
            }
        }

        Vector3 Prop() => new Vector3(_d.umbrella.X, _d.umbrella.Y, _d.umbrella.Z);

        bool InSight(Vector3 eye)
        {
            var d = Prop() - eye;
            float len = d.magnitude - _d.umbrellaRadius - 0.25f;
            return len <= 0f || !Physics.Raycast(eye, d.normalized, len, sightMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>The dream tool hits the umbrella: she has it, it opens over her (tests call this too).</summary>
        public bool Take()
        {
            if (!TryInit() || !_rain.PickUp()) return false;
            var cam = _host.Player.cam != null ? _host.Player.cam.transform : transform;
            Pulse(Prop(), cam.position + cam.right * 0.25f - cam.up * 0.25f + cam.forward * 0.3f);
            Play(_fire, 0.7f);
            Play(_take, 0.85f);
            SetLock(false);
            Show();
            GameRoot.Hud.Message("The umbrella opens over her.", 2f);
            return true;
        }

        void ChooseDespair()
        {
            var save = GameRoot.Save.Data;
            if (!save.HasFlag(DreamRain.DespairFlag))
            {
                save.SetFlag(DreamRain.DespairFlag);
                GameRoot.Save.Save();
            }
            Debug.Log("JAMKIT_DREAM despair: she went on into the rain without the umbrella (the save's " + DreamRain.DespairFlag + " is set)");
        }

        void SetLock(bool on)
        {
            if (on == _locked) return;
            _locked = on;
            if (on) Play(_lockTick, 0.35f);
        }

        void Show()
        {
            bool has = _rain != null && _rain.HasUmbrella;
            if (prop != null) prop.gameObject.SetActive(!has);
            if (glow != null) glow.gameObject.SetActive(!has);
            if (canopy != null) canopy.SetActive(has);
        }

        void SetHeavy(bool on)
        {
            if (_host == null || _host.Player == null || on == _heavyOn) return;
            _heavyOn = on;
            if (on)
            {
                if (_heavyTuning != null) _host.Player.tuning = _heavyTuning;
                else _host.Player.def = _heavy;
            }
            else
            {
                _host.Player.tuning = _tuning;
                _host.Player.def = _baseDef;
            }
        }

        void Pulse(Vector3 to, Vector3 from)
        {
            _beamFrom = from;
            _beamTo = to;
            _beamT = 0f;
        }

        void Effects(float dt, float s)
        {
            // The glow by the door breathes; locked, it swells.
            if (glow != null && glow.gameObject.activeSelf)
            {
                float k = (_locked ? 1.35f : 1f) * (1f + 0.08f * Mathf.Sin(Time.time * 3f));
                glow.localScale = Vector3.one * _glowBase * k;
            }
            if (beam != null && _beamT < 1f)
            {
                _beamT = Mathf.Min(1f, _beamT + dt / 0.12f);
                beam.gameObject.SetActive(_beamT < 1f);
                Vector3 d = _beamTo - _beamFrom;
                float len = d.magnitude;
                if (len > 0.01f)
                {
                    beam.SetPositionAndRotation(_beamFrom + d * 0.5f, Quaternion.LookRotation(d / len));
                    float w = 0.06f * (1f - _beamT * 0.7f);
                    beam.localScale = new Vector3(w, w, len);
                }
                if (beamRenderer != null)
                {
                    beamRenderer.GetPropertyBlock(_block);
                    var c = _beamColor;
                    c.a *= 1f - _beamT;
                    _block.SetColor(ColorId, c);
                    beamRenderer.SetPropertyBlock(_block);
                }
            }
            // The rain falls over the path; you hear it from the porch, louder out in it, drumming softer on the umbrella.
            bool nearRain = s > _d.rainStartS - 15f;
            if (rainFx != null)
            {
                if (nearRain && !rainFx.isPlaying) rainFx.Play();
                else if (!nearRain && rainFx.isPlaying) rainFx.Stop();
            }
            if (_loop != null)
            {
                float target = !nearRain ? 0f : !_rain.InRain ? 0.25f : _rain.HasUmbrella ? 0.45f : 0.7f;
                bool muted = GameRoot.Audio.Muted || Time.timeScale <= 0f;
                _loop.volume = Mathf.MoveTowards(_loop.volume, muted ? 0f : target * volume * GameRoot.Settings.sfxVolume, Time.unscaledDeltaTime * 1.5f);
                _loop.pitch = _rain.InRain && _rain.HasUmbrella ? 0.85f : 1f;
            }
            if (_vignette != null)
            {
                float a = _rain.Vignette;
                _vignette.style.opacity = a;
                var want = a > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
                if (_vignette.style.display != want) _vignette.style.display = want;
            }
        }

        void DebugLine()
        {
            if (_debug == null) return;
            bool toggle = (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame);
            if (toggle && GameRoot.Input.GameplayAllowed) _debugShown = !_debugShown;
            bool show = _debugShown && _host.S > _d.resetS - 30f;
            var want = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (_debug.style.display != want) _debug.style.display = want;
            if (!show) return;
            // The words change only when the state does (no string every frame).
            int state = (DreamEscapeGame.Despair ? 1 : 0) + (_rain.HasUmbrella ? 2 : 0) + (_rain.DespairThisRun ? 4 : 0) + ((int)(_rain.Soak * 10f) << 3);
            if (state == _lastDespairShown) return;
            _lastDespairShown = state;
            _debug.text = "Despair: " + (DreamEscapeGame.Despair ? "SET" : "no") + (_rain.DespairThisRun ? " (this run)" : "")
                + "   umbrella: " + (_rain.HasUmbrella ? "yes" : "no") + "   soak " + (_rain.Soak * 100f).ToString("0") + "%";
        }

        void BuildUi()
        {
            var root = GameRoot.Hud.Root;
            _vignette = new VisualElement { name = "dream-rain-vignette", pickingMode = PickingMode.Ignore };
            _vignette.style.position = Position.Absolute;
            _vignette.style.left = 0; _vignette.style.right = 0; _vignette.style.top = 0; _vignette.style.bottom = 0;
            _vignette.style.backgroundImage = new StyleBackground(VignetteTexture());
            _vignette.style.display = DisplayStyle.None;
            root.Add(_vignette);
            _debug = new Label { name = "dream-despair-debug", pickingMode = PickingMode.Ignore };
            _debug.style.position = Position.Absolute;
            _debug.style.left = 16; _debug.style.bottom = 12;
            _debug.style.fontSize = 14;
            _debug.style.color = new Color(0.95f, 0.95f, 1f, 0.9f);
            _debug.style.display = DisplayStyle.None;
            root.Add(_debug);
        }

        /// <summary>A soft cold vignette: clear in the middle, a blue-grey dark toward the edges.</summary>
        static Texture2D VignetteTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Dream rain vignette", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                    float a = Mathf.SmoothStep(0.25f, 1f, r);
                    px[y * size + x] = new Color32(18, 22, 40, (byte)(255f * Mathf.Clamp01(a * 1.1f)));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
