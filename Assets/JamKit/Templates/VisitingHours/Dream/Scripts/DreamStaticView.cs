using JamKit.Shell;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The dream's immune system as you see and hear it: a curved wall of pastel TV static across the street at
    /// the pursuer's distance (core <c>Pursuer</c>), a swarm of static "wasps" boiling ahead of it, and a few
    /// scouts that fly at the edges of your view (always at least two, more as the gap closes), so the chase is
    /// in your peripheral vision even when you look ahead. A hiss from the wall and a buzz near you, both
    /// louder as it closes in. Built by the DreamEscapeBuilder; no allocation per frame.
    /// </summary>
    public sealed class DreamStaticView : MonoBehaviour
    {
        public Renderer[] wall;
        public Transform[] wasps;
        [Tooltip("How many of the wasps are scouts that fly beside the player's view.")]
        public int scouts = 6;
        public AudioSource hiss, buzz;
        [Range(0f, 1f)] public float hissVolume = 0.65f;
        [Range(0f, 1f)] public float buzzVolume = 0.45f;
        /// <summary>V7 (docs/SPEED-FEEL.md): the wall is DRAWN this many metres further back (set each frame by
        /// DreamSpeedAnswer at flying). Visual only: the pursuer's distance and catch don't read it.
        /// <para>Applied the moment it's set. The game places the wall in its Update, and DreamSpeedAnswer sets this
        /// later, in its LateUpdate, so a plain field drew the wall with the previous frame's recede: a frame late, off
        /// by the change in one frame (2026-10-12: 0.31 m in a PlayMode run, at 23.7 m/s with a long frame).</para></summary>
        public float visualRecede
        {
            get => _recede;
            set
            {
                float shift = Mathf.Max(0f, value) - Mathf.Max(0f, _recede);
                _recede = value;
                if (shift != 0f && _placed) Recede(shift);
            }
        }
        float _recede;
        bool _placed;

        static readonly int ThreatId = Shader.PropertyToID("_Threat");
        MaterialPropertyBlock _block;
        Renderer[] _waspRenderers;
        Vector3[] _scoutPos;
        float _threat;
        bool _shown;
        Vector3 _center;
        Transform _cam;

        public bool Shown => _shown;
        public float ThreatShown => _threat;
        /// <summary>How many scouts fly beside the player right now.</summary>
        public int ScoutsOut => _scoutsOut;
        int _scoutsOut;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _waspRenderers = new Renderer[wasps != null ? wasps.Length : 0];
            for (int i = 0; i < _waspRenderers.Length; i++) _waspRenderers[i] = wasps[i] != null ? wasps[i].GetComponent<Renderer>() : null;
            _scoutPos = new Vector3[Mathf.Max(1, scouts)];
        }

        void Start()
        {
            SetupSource(hiss, GameRoot.Exists ? GameRoot.Audio.ClipFor("mv_static") : ProceduralAudio.Get("mv_static"), 0.75f);
            SetupSource(buzz, DreamSynth.Buzz, 0f);
            Show(false);
        }

        static void SetupSource(AudioSource s, AudioClip clip, float spatial)
        {
            if (s == null) return;
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.dopplerLevel = 0f;
            s.minDistance = 4f;
            s.maxDistance = 45f;
            s.volume = 0f;
            if (clip != null) s.Play();
        }

        public void Show(bool shown)
        {
            _shown = shown;
            if (wall != null) foreach (var r in wall) if (r != null) r.enabled = shown;
            if (_waspRenderers != null) foreach (var r in _waspRenderers) if (r != null) r.enabled = shown;
            if (!shown)
            {
                if (hiss != null) hiss.volume = 0f;
                if (buzz != null) buzz.volume = 0f;
                _scoutsOut = 0;
            }
        }

        /// <summary>Places the wall at <paramref name="center"/> (on the route at the pursuer's distance, at floor
        /// height), facing +Z, and flies the wasps; <paramref name="threat"/> 0..1; <paramref name="cam"/> the player's camera.</summary>
        public void Place(Vector3 center, float threat, Transform cam)
        {
            float dt = Time.deltaTime;
            _cam = cam;
            center.z -= Mathf.Max(0f, _recede); // the route runs along +Z: back is -Z
            _center = center;
            transform.position = center;
            _placed = true;
            _threat = Mathf.MoveTowards(_threat, threat, 2f * dt);
            if (wall != null)
                foreach (var r in wall)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetFloat(ThreatId, _threat);
                    r.SetPropertyBlock(_block);
                }
            if (_shown) FlyWasps(dt);
            float sfx = GameRoot.Exists ? GameRoot.Settings.sfxVolume : 0.8f;
            bool muted = GameRoot.Exists && GameRoot.Audio.Muted;
            if (hiss != null)
            {
                hiss.volume = _shown ? (0.4f + 0.6f * _threat) * hissVolume * sfx : 0f;
                hiss.pitch = 0.9f + 0.3f * _threat;
                hiss.mute = muted;
            }
            if (buzz != null)
            {
                buzz.volume = _shown ? (0.18f + 0.82f * _threat * _threat) * buzzVolume * sfx : 0f;
                buzz.pitch = 0.95f + 0.25f * _threat;
                buzz.mute = muted;
            }
        }

        /// <summary>Moves the drawn wall and its swarm <paramref name="shift"/> metres further back (-Z) at once. The
        /// scouts, children too but flying beside the camera, keep their place in the world.</summary>
        void Recede(float shift)
        {
            var back = new Vector3(0f, 0f, shift);
            _center -= back;
            transform.position = _center;
            if (wasps == null) return;
            for (int i = Mathf.Max(0, wasps.Length - scouts); i < wasps.Length; i++)
                if (wasps[i] != null) wasps[i].position += back;
        }

        void FlyWasps(float dt)
        {
            if (wasps == null) return;
            float t = Time.time;
            Quaternion face = _cam != null ? Quaternion.LookRotation(_cam.forward, Vector3.up) : Quaternion.identity;
            int swarm = Mathf.Max(0, wasps.Length - scouts);
            for (int i = 0; i < swarm; i++)
            {
                var w = wasps[i];
                if (w == null) continue;
                float a = i * 2.399f;
                float x = Mathf.Sin(a * 3.1f + t * (0.7f + 0.05f * i)) * 13f;
                float y = 1f + Mathf.Abs(Mathf.Sin(a * 1.7f + t * 1.3f)) * 6f;
                float z = 1.5f + Mathf.Abs(Mathf.Sin(a + t * (1.1f + 0.03f * i))) * (3f + 4f * _threat);
                w.position = _center + new Vector3(x, y, z) + JitterOf(i, t);
                w.rotation = face;
                float flap = 0.85f + 0.15f * Mathf.Sin(t * 50f + i);
                w.localScale = new Vector3(0.55f * flap, 0.55f, 1f);
            }
            // Scouts: beside the player's view, at about 45-60 degrees off the gaze, a few metres out.
            int want = 2 + Mathf.RoundToInt(4f * _threat);
            _scoutsOut = 0;
            for (int k = 0; k < scouts && swarm + k < wasps.Length; k++)
            {
                var w = wasps[swarm + k];
                if (w == null) continue;
                bool active = k < want && _cam != null;
                if (_waspRenderers[swarm + k] != null) _waspRenderers[swarm + k].enabled = active;
                if (!active) { _scoutPos[k] = w.position; continue; }
                _scoutsOut++;
                float side = (k % 2 == 0) ? 1f : -1f;
                float ang = (46f + 8f * Mathf.Sin(t * 0.9f + k * 1.3f) + (k / 2) * 6f) * side;
                Vector3 fwd = _cam.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
                fwd.Normalize();
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * fwd;
                float dist = 4.5f + 2.5f * Mathf.Sin(t * 0.6f + k) * 0.5f + 1.5f * (1f - _threat);
                Vector3 target = _cam.position + dir * dist + Vector3.up * (0.4f + 0.8f * Mathf.Sin(t * 2.1f + k * 2f)) + JitterOf(k + 40, t) * 0.6f;
                // Follow with a lag so they drift and dart instead of being glued to the screen.
                if ((_scoutPos[k] - target).sqrMagnitude > 400f) _scoutPos[k] = target;
                _scoutPos[k] = Vector3.Lerp(_scoutPos[k], target, 1f - Mathf.Exp(-6f * dt));
                w.position = _scoutPos[k];
                w.rotation = face;
                float flap = 0.85f + 0.15f * Mathf.Sin(t * 55f + k * 3f);
                w.localScale = new Vector3(0.4f * flap, 0.4f, 1f);
            }
        }

        static Vector3 JitterOf(int i, float t)
        {
            float s = Mathf.Floor(t * 12f) + i * 17.13f;
            return new Vector3(Mathf.Sin(s * 12.9898f) * 0.12f, Mathf.Sin(s * 78.233f) * 0.12f, Mathf.Sin(s * 37.719f) * 0.12f);
        }
    }
}
