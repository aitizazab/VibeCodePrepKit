using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// A gentle EQ on one world voice (the mix pass, 2026-10-14): a broad peaking cut around <see cref="SkylineMix.HarshCenterHz"/>
    /// that <see cref="SkylineSounds"/> sets per sound (<see cref="SkylineMix.HarshCutDb"/>: the bots' one-shots only), to take the
    /// edge off the 2-5 kHz band where the bots' chirps, whoops and zaps pile up. The filter runs on the audio thread
    /// (OnAudioFilterRead) on the voice's own GameObject, so it only ever touches that voice. A flat setting costs nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkylineVoiceEq : MonoBehaviour
    {
        readonly SkylineBiquad[] _bq = { new SkylineBiquad(), new SkylineBiquad() };
        volatile float _requestedDb;
        float _appliedDb = float.NaN;
        int _sampleRate = 48000;

        /// <summary>The cut to apply (dB at the centre; 0 = off). Main thread.</summary>
        public void SetCut(float db) => _requestedDb = db;

        void Awake()
        {
            _sampleRate = AudioSettings.outputSampleRate;
        }

        // Audio thread.
        void OnAudioFilterRead(float[] data, int channels)
        {
            float want = _requestedDb;
            if (want != _appliedDb)
            {
                foreach (var b in _bq)
                {
                    b.SetPeaking(_sampleRate, SkylineMix.HarshCenterHz, SkylineMix.HarshQ, want);
                    b.Reset();
                }
                _appliedDb = want;
            }
            if (_bq[0].IsFlat) return;
            if (channels == 1)
            {
                for (int i = 0; i < data.Length; i++) data[i] = _bq[0].Process(data[i]);
                return;
            }
            for (int i = 0; i + 1 < data.Length; i += channels)
            {
                data[i] = _bq[0].Process(data[i]);
                data[i + 1] = _bq[1].Process(data[i + 1]);
            }
        }
    }
}
