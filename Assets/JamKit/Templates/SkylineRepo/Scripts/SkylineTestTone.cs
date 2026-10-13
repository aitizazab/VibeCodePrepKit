using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The pipeline's test tone: a 440 Hz beep for 0.5 s every 2 s, so SkylinePass can prove the recordings
    /// carry audio (docs/SKYLINE-PLAN-M0.md, Task 9 stage E). Not for shipping scenes.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SkylineTestTone : MonoBehaviour
    {
        public float frequency = 440f;
        public float beepSeconds = 0.5f;
        public float everySeconds = 2f;

        AudioSource _source;
        float _next;

        void Awake()
        {
            int sr = AudioSettings.outputSampleRate;
            int n = Mathf.CeilToInt(beepSeconds * sr);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float fade = Mathf.Min(1f, t / 0.005f) * Mathf.Min(1f, (beepSeconds - t) / 0.02f);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.5f * fade;
            }
            var clip = AudioClip.Create("SkylineTestTone", n, 1, sr, false);
            clip.SetData(data, 0);
            _source = GetComponent<AudioSource>();
            _source.clip = clip;
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
        }

        void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + everySeconds;
            _source.Play();
        }
    }
}
