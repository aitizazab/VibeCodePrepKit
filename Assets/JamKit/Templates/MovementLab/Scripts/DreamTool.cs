using JamKit.Movement;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.MovementLab
{
    /// <summary>
    /// Nadia's dream tool (docs/MOVEMENT.md "Dream tool prototype"): the shell's <c>Fire</c> action (left mouse, RT;
    /// rebindable) sends a pulse of light from her right hand, hitscan from the camera. Aim assist (core
    /// <see cref="DreamToolAim"/>) locks the valid target nearest the crosshair within a cone (6 degrees on mouse, 8 on a
    /// pad) and shows a bracket on it, so the shot stays one action. A hit pops a glass tile, unfolds a socket's bridge or
    /// wall, or flips which colour is solid (core <see cref="DreamToolState"/>, owned by <see cref="DreamToolRun"/>).
    /// Feedback: a pulse at her hand, a beam, the target's flash, a lock tick and a sound per effect (core
    /// <see cref="DreamToolSounds"/>). It works only in the dream tool chapter.
    /// Test hook: while the course autopilot drives (<see cref="MovementLabGame.AutopilotOn"/>) and
    /// <see cref="autoFire"/> is on, it fires at each cue's target when in range (core <see cref="DreamToolPilot"/>).
    /// Her arm isn't raised on a shot: the body's pose has no hook between its feel and the rig (see the docs).
    /// </summary>
    public sealed class DreamTool : MonoBehaviour, IDreamToolSight
    {
        public MovementLabGame game;
        public DreamToolRun run;
        /// <summary>The first-person body: the pulse starts at her right hand when it's there.</summary>
        public MovementBody body;
        public Transform beam, pulse;
        public Renderer beamRenderer, pulseRenderer;
        public DreamToolDef def = new DreamToolDef();
        /// <summary>The autopilot fires the tool at its cues (switch off to watch a missed tile block the lane).</summary>
        public bool autoFire = true;
        public LayerMask sightMask = ~(1 << 2);
        [Range(0f, 1f)] public float volume = 0.8f;

        const float BeamTime = 0.12f, PulseTime = 0.18f;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        DreamToolPilot _pilot;
        int _locked = -1;
        bool _wasAuto;
        float _beamT = 1f, _pulseT = 1f;
        Vector3 _beamFrom, _beamTo;
        AudioSource[] _voices;
        int _voice;
        AudioClip _fire, _lock, _pop, _build, _flip, _dry;
        VisualElement _bracket;
        MaterialPropertyBlock _block;
        Color _beamColor, _pulseColor;
        DreamShot _last;
        int _shotsFired;

        /// <summary>The target the aim is locked on (-1: none).</summary>
        public int Locked => _locked;
        public DreamShot LastShot => _last;
        public int ShotsFired => _shotsFired;
        public bool UsingPad => GameRoot.Exists && (GameRoot.Input.LookFromGamepad || (GameRoot.Input.Fire.activeControl != null && GameRoot.Input.Fire.activeControl.device is Gamepad));

        void Start()
        {
            if (game == null || run == null || !GameRoot.Exists) { enabled = false; return; }
            _block = new MaterialPropertyBlock();
            _voices = new AudioSource[4];
            for (int i = 0; i < _voices.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.dopplerLevel = 0f;
                _voices[i] = src;
            }
            _fire = Clip("dt_fire"); _lock = Clip("dt_lock"); _pop = Clip("dt_pop");
            _build = Clip("dt_build"); _flip = Clip("dt_flip"); _dry = Clip("dt_dry");
            if (beamRenderer != null && beamRenderer.sharedMaterial != null && beamRenderer.sharedMaterial.HasProperty(ColorId)) _beamColor = beamRenderer.sharedMaterial.GetColor(ColorId);
            if (pulseRenderer != null && pulseRenderer.sharedMaterial != null && pulseRenderer.sharedMaterial.HasProperty(ColorId)) _pulseColor = pulseRenderer.sharedMaterial.GetColor(ColorId);
            if (beam != null) beam.gameObject.SetActive(false);
            if (pulse != null) pulse.gameObject.SetActive(false);
            BuildUi();
        }

        void OnDestroy() => _bracket?.RemoveFromHierarchy();

        /// <summary>A clip from the game's Audio Registry under the same id, else the generated one.</summary>
        static AudioClip Clip(string id)
        {
            var reg = GameRoot.Exists ? GameRoot.Audio.ClipFor(id) : null;
            if (reg != null) return reg;
            var data = DreamToolSounds.Make(id);
            var clip = AudioClip.Create("gen_" + id, data.Length, 1, DreamToolSounds.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void Play(AudioClip clip, float vol, float pitch = 1f)
        {
            if (clip == null || Application.isBatchMode || GameRoot.Audio.Muted) return;
            var src = _voices[_voice];
            _voice = (_voice + 1) % _voices.Length;
            src.pitch = pitch * Random.Range(0.97f, 1.03f);
            src.PlayOneShot(clip, vol * volume * GameRoot.Settings.sfxVolume);
        }

        /// <summary>Forget the autopilot's cues behind <paramref name="s"/> (a placement or the autopilot taking over).</summary>
        public void ResetPilot(float s)
        {
            if (_pilot == null && run.Course != null) _pilot = new DreamToolPilot(run.Course);
            _pilot?.Reset(s);
        }

        void Update()
        {
            var state = run.State;
            if (state == null) return;
            if (_pilot == null) _pilot = new DreamToolPilot(run.Course);
            float dt = Time.deltaTime;
            state.Tick(dt);
            Animate(dt);
            bool active = DreamToolCourse.InChapter(game.S) && GameRoot.Flow.Current == JamKit.Flow.GameState.Playing && !game.Respawning;
            var cam = game.player != null ? game.player.cam : null;
            if (!active || cam == null)
            {
                SetLock(-1);
                PlaceBracket(null);
                return;
            }
            var eye = cam.transform.position;
            var fwd = cam.transform.forward;
            var e = new System.Numerics.Vector3(eye.x, eye.y, eye.z);
            int locked = DreamToolAim.Pick(run.Course, state, e, new System.Numerics.Vector3(fwd.x, fwd.y, fwd.z), UsingPad ? def.conePad : def.coneMouse, def.range, this, out _);
            SetLock(locked);
            PlaceBracket(cam);

            bool auto = game.AutopilotOn;
            if (auto && !_wasAuto) _pilot.Reset(game.S - 1f);
            _wasAuto = auto;
            if (auto)
            {
                if (!autoFire) return;
                int t = _pilot.Next(game.S, e, state, def.range);
                if (t >= 0) FireAt(t);
            }
            else if (GameRoot.Input.Pressed(GameRoot.Input.Fire)) FireAt(_locked);
        }

        /// <summary>Fires at <paramref name="target"/> (-1 at nothing) if the tool is ready: the effect, the pulse, the beam, the sound.</summary>
        public DreamShot FireAt(int target)
        {
            var state = run.State;
            var shot = state.Fire(target, def.cooldown);
            if (!shot.fired) return shot;
            _last = shot;
            _shotsFired++;
            var cam = game.player.cam.transform;
            _beamFrom = body != null ? body.HandTarget(1) : cam.position + cam.right * 0.25f - cam.up * 0.2f;
            if ((_beamFrom - cam.position).sqrMagnitude > 1f) _beamFrom = cam.position + cam.forward * 0.4f + cam.right * 0.2f - cam.up * 0.2f;
            if (target >= 0)
            {
                var c = run.Course.targets[target].center;
                _beamTo = new Vector3(c.X, c.Y, c.Z);
            }
            else _beamTo = Physics.Raycast(cam.position, cam.forward, out var hit, def.range, sightMask, QueryTriggerInteraction.Ignore) ? hit.point : cam.position + cam.forward * def.range;
            _beamT = 0f;
            _pulseT = 0f;
            // Her right hand lifts towards where the pulse goes (the wall and ledge hands win over it).
            if (body != null) body.Gesture(1, _beamTo, 0.2f);
            Play(_fire, 0.7f);
            if (target < 0) Play(_dry, 0.5f);
            else if (shot.kind == DreamTargetKind.Tile) Play(_pop, 0.9f);
            else if (shot.kind == DreamTargetKind.Socket) Play(_build, 0.85f);
            else Play(_flip, 0.8f, shot.colour == DreamToolCourse.Pink ? 1f : 1.12f);
            run.OnShot(shot);
            return shot;
        }

        void SetLock(int target)
        {
            if (target == _locked) return;
            if (_locked >= 0) run.TargetView(_locked)?.SetLocked(false);
            _locked = target;
            if (target >= 0)
            {
                run.TargetView(target)?.SetLocked(true);
                Play(_lock, 0.35f);
            }
        }

        /// <summary>Line of sight for the aim: a raycast against the world, stopping short of the target by its size.</summary>
        public bool Blocked(System.Numerics.Vector3 from, System.Numerics.Vector3 to, float clearance)
        {
            var a = new Vector3(from.X, from.Y, from.Z);
            var d = new Vector3(to.X, to.Y, to.Z) - a;
            float len = d.magnitude - clearance;
            if (len <= 0f) return false;
            return Physics.Raycast(a, d.normalized, len, sightMask, QueryTriggerInteraction.Ignore);
        }

        // ---------- The pulse, the beam, the bracket ----------

        void Animate(float dt)
        {
            if (_beamT < 1f && beam != null)
            {
                _beamT = Mathf.Min(1f, _beamT + dt / BeamTime);
                beam.gameObject.SetActive(_beamT < 1f);
                Vector3 d = _beamTo - _beamFrom;
                float len = d.magnitude;
                if (len > 0.01f)
                {
                    beam.SetPositionAndRotation(_beamFrom + d * 0.5f, Quaternion.LookRotation(d / len));
                    float w = 0.06f * (1f - _beamT * 0.7f);
                    beam.localScale = new Vector3(w, w, len);
                }
                Fade(beamRenderer, _beamColor, 1f - _beamT);
            }
            if (_pulseT < 1f && pulse != null)
            {
                _pulseT = Mathf.Min(1f, _pulseT + dt / PulseTime);
                pulse.gameObject.SetActive(_pulseT < 1f);
                var from = body != null ? body.HandTarget(1) : _beamFrom;
                if ((from - _beamFrom).sqrMagnitude > 1f) from = _beamFrom;
                pulse.position = from;
                pulse.localScale = Vector3.one * Mathf.Lerp(0.08f, 0.32f, _pulseT);
                Fade(pulseRenderer, _pulseColor, 1f - _pulseT);
            }
        }

        void Fade(Renderer r, Color c, float alpha)
        {
            if (r == null || _block == null) return;
            r.GetPropertyBlock(_block);
            c.a *= alpha;
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        void BuildUi()
        {
            _bracket = new VisualElement { name = "dream-tool-lock", pickingMode = PickingMode.Ignore };
            _bracket.style.position = Position.Absolute;
            _bracket.style.width = 44;
            _bracket.style.height = 44;
            var gold = new Color(1f, 0.85f, 0.45f, 0.95f);
            // Four corner brackets.
            for (int i = 0; i < 4; i++)
            {
                var c = new VisualElement { pickingMode = PickingMode.Ignore };
                c.style.position = Position.Absolute;
                c.style.width = 12; c.style.height = 12;
                bool left = i % 2 == 0, top = i < 2;
                if (left) c.style.left = 0; else c.style.right = 0;
                if (top) c.style.top = 0; else c.style.bottom = 0;
                if (left) { c.style.borderLeftWidth = 3; c.style.borderLeftColor = gold; } else { c.style.borderRightWidth = 3; c.style.borderRightColor = gold; }
                if (top) { c.style.borderTopWidth = 3; c.style.borderTopColor = gold; } else { c.style.borderBottomWidth = 3; c.style.borderBottomColor = gold; }
                _bracket.Add(c);
            }
            _bracket.style.display = DisplayStyle.None;
            GameRoot.Hud.Root.Add(_bracket);
        }

        void PlaceBracket(Camera cam)
        {
            if (_bracket == null) return;
            if (cam == null || _locked < 0)
            {
                if (_bracket.style.display != DisplayStyle.None) _bracket.style.display = DisplayStyle.None;
                return;
            }
            var c = run.Course.targets[_locked].center;
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(c.X, c.Y, c.Z));
            var panel = _bracket.panel;
            if (sp.z <= 0f || panel == null) { _bracket.style.display = DisplayStyle.None; return; }
            // Screen pixels (origin bottom left) to the panel's coordinates (origin top left).
            Vector2 p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, Screen.height - sp.y));
            _bracket.style.display = DisplayStyle.Flex;
            _bracket.style.left = p.x - 22f;
            _bracket.style.top = p.y - 22f;
        }
    }
}
