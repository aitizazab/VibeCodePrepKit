using JamKit.Shell;
using UnityEngine;

namespace JamKit.Movement
{
    /// <summary>
    /// The movement's sound (placeholders made by <see cref="ProceduralAudio"/> until the Audio Registry
    /// has clips for these ids): a wind loop that rises with speed (volume smoothstep 6..18 m/s, pitch
    /// up with speed), footsteps on the stride (faster at speed, 1.5x the cadence on a wall), a slide
    /// scrape loop, a wallrun scrape loop and a "catch" when a wall takes you, a whoosh for the jump, a
    /// different airy pop for the double jump, a kick for the wall jump, a boost whoosh, a mantle
    /// scuff, and a landing thud scaled by the impact. Loops play on their own sources at the
    /// effects volume; one-shots go through GameRoot.Audio. Silent in batch mode like the shell.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class MovementAudio : MonoBehaviour
    {
        public MovementController body;
        [Range(0f, 1f)] public float windVolume = 0.55f;
        [Range(0f, 1f)] public float slideVolume = 0.45f;
        [Range(0f, 1f)] public float wallVolume = 0.35f;
        [Range(0f, 1f)] public float stepVolume = 0.32f;

        AudioSource _wind, _slide, _wall;
        float _windV, _slideV, _wallV;

        public float WindLevel => _windV;

        void Start()
        {
            _wind = Loop("mv_wind");
            _slide = Loop("mv_slide");
            _wall = Loop("mv_wallrun");
        }

        AudioSource Loop(string id)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.volume = 0f;
            src.clip = GameRoot.Exists ? GameRoot.Audio.ClipFor(id) : ProceduralAudio.Get(id);
            if (src.clip != null)
            {
                // Start each loop at a different point so the layers don't line up.
                src.timeSamples = (int)(src.clip.samples * Random.value) % Mathf.Max(1, src.clip.samples);
                src.Play();
            }
            return src;
        }

        void Update()
        {
            if (body == null || body.Sim == null || _wind == null) return;
            bool exists = GameRoot.Exists;
            float sfx = exists ? GameRoot.Settings.sfxVolume : 0.8f;
            bool muted = exists && GameRoot.Audio.Muted;
            bool paused = Time.timeScale <= 0f;
            float udt = Time.unscaledDeltaTime;
            var sim = body.Sim;
            var mode = sim.Mode;
            float speed = sim.HorizontalSpeed;
            var ev = body.FrameEvents;

            float wind = MoveMath.SmoothStep(6f, 18f, speed) * windVolume;
            float slide = mode == MoveMode.Slide ? slideVolume * Mathf.Clamp01((speed - 3f) / 6f) : 0f;
            float wall = mode == MoveMode.Wallrun ? wallVolume : 0f;
            if (paused) wind = slide = wall = 0f;
            _windV = Mathf.MoveTowards(_windV, wind, udt * 1.5f);
            _slideV = Mathf.MoveTowards(_slideV, slide, udt * 8f);
            _wallV = Mathf.MoveTowards(_wallV, wall, udt * 10f);
            Set(_wind, _windV * sfx, 0.85f + 0.35f * MoveMath.Clamp01(speed / 20f), muted);
            Set(_slide, _slideV * sfx, 0.8f + 0.4f * MoveMath.Clamp01(speed / 14f), muted);
            Set(_wall, _wallV * sfx, 1f, muted);

            if (!exists || paused) return;
            var audio = GameRoot.Audio;
            if (ev.wallrunStarted) audio.Play("mv_catch", 0.9f);
            if (ev.wallJumped) audio.Play("mv_walljump", 0.9f);
            else if (ev.doubleJumped) audio.Play("mv_djump", 0.85f);
            else if (ev.jumped) audio.Play("mv_jump", 0.7f);
            if (ev.slideBoosted) audio.Play("mv_boost", 0.7f);
            if (ev.mantleStarted) audio.Play("mv_mantle", 0.8f);
            if (ev.landed && !ev.slideStarted)
            {
                float thud = Mathf.Clamp01((ev.landImpact - 3f) / 15f);
                if (thud > 0.03f) audio.Play("mv_land", 0.25f + 0.75f * thud);
                else audio.Play("mv_step", stepVolume);
            }
            if (body.Stepped) audio.Play("mv_step", mode == MoveMode.Wallrun ? stepVolume * 1.2f : stepVolume);
        }

        static void Set(AudioSource s, float volume, float pitch, bool muted)
        {
            if (s == null) return;
            s.volume = volume;
            s.pitch = pitch;
            s.mute = muted;
        }
    }
}
