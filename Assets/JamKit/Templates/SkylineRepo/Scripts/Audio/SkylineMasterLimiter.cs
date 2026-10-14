using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The master safety limiter (<see cref="SkylineLimiter"/>, <see cref="SkylineMix.CeilingDb"/>) on the AudioListener's
    /// object: a filter there processes the whole mix after Unity sums it in float, before the output (and before the
    /// Recorder's audio capture). <see cref="SkylineMixer"/> adds it to the active listener; nothing else needs to.
    /// Logs <c>SKYLINE_MIX limiter ...</c> when it goes away (peak in, the deepest reduction, time spent over 1 dB).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkylineMasterLimiter : MonoBehaviour
    {
        readonly SkylineLimiter _dsp = new SkylineLimiter();
        int _sampleRate;
        volatile bool _ready;

        void OnEnable()
        {
            _sampleRate = AudioSettings.outputSampleRate;
            AudioSettings.OnAudioConfigurationChanged += OnConfigChanged;
        }

        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnConfigChanged;
        }

        void OnConfigChanged(bool deviceWasChanged)
        {
            _sampleRate = AudioSettings.outputSampleRate;
            _ready = false;   // the audio thread reconfigures on its next block
        }

        // Audio thread.
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!_ready || _dsp.Channels != Mathf.Min(channels, SkylineLimiter.MaxChannels))
            {
                int sr = _sampleRate > 0 ? _sampleRate : 48000;
                _dsp.Configure(sr, channels, SkylineMix.CeilingDb, SkylineMix.KneeDb, SkylineMix.LookaheadMs, SkylineMix.ReleaseMs);
                _ready = true;
            }
            _dsp.Process(data, channels);
        }

        /// <summary>One line for the pass's log: what the limiter saw and did since the last report.</summary>
        public string Report()
        {
            float peakDb = 20f * Mathf.Log10(Mathf.Max(1e-9f, _dsp.PeakIn));
            float grDb = -20f * Mathf.Log10(Mathf.Max(1e-9f, _dsp.MinGain));
            double over = _dsp.Frames > 0 ? 100.0 * _dsp.FramesOver1dB / _dsp.Frames : 0.0;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "SKYLINE_MIX limiter peak-in {0:F1} dBFS, deepest reduction {1:F1} dB, {2:F2}% of the time over 1 dB ({3:F0} s)",
                peakDb, grDb, over, _dsp.Frames / (double)Mathf.Max(1, _dsp.SampleRate));
        }

        void OnDestroy()
        {
            if (_dsp.Frames > 0) Debug.Log(Report());
        }
    }
}
