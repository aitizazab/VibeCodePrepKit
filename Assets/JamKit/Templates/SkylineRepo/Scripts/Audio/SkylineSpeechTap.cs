using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Listens to a speech source's output (a filter on its object; the audio passes through untouched) and keeps the
    /// loudest sample of the latest blocks, so <see cref="SkylineMixer"/> ducks the effects exactly while a line is heard,
    /// gaps included (the release bridges them). <see cref="SkylineMixer"/> puts one on the arena's teaching-line voice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkylineSpeechTap : MonoBehaviour
    {
        volatile float _peak;

        /// <summary>The loudest sample since the last call (linear), then clears it. Main thread.</summary>
        public float TakePeak()
        {
            float p = _peak;
            _peak = 0f;
            return p;
        }

        // Audio thread.
        void OnAudioFilterRead(float[] data, int channels)
        {
            float p = _peak;
            for (int i = 0; i < data.Length; i++)
            {
                float a = data[i] < 0f ? -data[i] : data[i];
                if (a > p) p = a;
            }
            _peak = p;
        }
    }
}
