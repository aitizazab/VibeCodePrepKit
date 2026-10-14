using System;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The master bus's safety limiter as plain DSP (no Unity types, so a console harness can run it on a WAV):
    /// a look-ahead peak limiter with a soft knee, linked across channels.
    ///
    /// Per frame: the level is the largest of every channel's sample and of the 4-point midpoint between the last two
    /// samples (catches most inter-sample peaks). Below the knee the gain target is 1; above it the level is bent by a tanh
    /// curve that approaches the ceiling and never passes it. The gain follows a target down at once and holds it for the
    /// look-ahead, then recovers exponentially (release). A moving average over the look-ahead turns that into a smooth
    /// ramp, and the audio is delayed by the look-ahead, so the gain has reached its target when the peak comes out: no
    /// sample leaves above the ceiling, and nothing is touched while the sum stays under the knee (a lone gun shot passes
    /// whole). Allocation only in <see cref="Configure"/>.
    /// </summary>
    public sealed class SkylineLimiter
    {
        public const int MaxChannels = 8;

        float _ceiling = 1f, _knee = 1f, _rel;
        int _look = 1, _channels;
        float[] _delay = new float[0];
        float[] _ring = new float[0];
        readonly float[] _h1 = new float[MaxChannels], _h2 = new float[MaxChannels], _h3 = new float[MaxChannels];
        int _pos;
        double _sum;
        float _cur = 1f;
        int _hold;

        /// <summary>Lowest gain applied since the last <see cref="ResetStats"/> (1 = never reduced).</summary>
        public float MinGain { get; private set; } = 1f;
        /// <summary>Highest level that came in since the last <see cref="ResetStats"/> (linear, 1 = 0 dBFS).</summary>
        public float PeakIn { get; private set; }
        /// <summary>Frames processed, and frames whose gain was more than 1 dB down, since the last <see cref="ResetStats"/>.</summary>
        public long Frames { get; private set; }
        public long FramesOver1dB { get; private set; }

        public int SampleRate { get; private set; }
        public int Channels => _channels;

        /// <summary>Sets the curve and the timing; clears the state when the sample rate or the channel count changed.</summary>
        public void Configure(int sampleRate, int channels, float ceilingDb, float kneeDb, float lookaheadMs, float releaseMs)
        {
            channels = Math.Max(1, Math.Min(MaxChannels, channels));
            _ceiling = (float)Math.Pow(10.0, ceilingDb / 20.0);
            _knee = _ceiling * (float)Math.Pow(10.0, -Math.Max(0.01f, kneeDb) / 20.0);
            int look = Math.Max(1, (int)Math.Round(lookaheadMs * 0.001 * sampleRate));
            _rel = (float)Math.Exp(-1.0 / Math.Max(1.0, releaseMs * 0.001 * sampleRate));
            if (sampleRate == SampleRate && channels == _channels && look == _look && _ring.Length == look) return;
            SampleRate = sampleRate;
            _channels = channels;
            _look = look;
            _delay = new float[look * channels];
            _ring = new float[look];
            for (int i = 0; i < look; i++) _ring[i] = 1f;
            _sum = look;
            _pos = 0;
            _cur = 1f;
            _hold = 0;
            Array.Clear(_h1, 0, MaxChannels);
            Array.Clear(_h2, 0, MaxChannels);
            Array.Clear(_h3, 0, MaxChannels);
        }

        public void ResetStats()
        {
            MinGain = 1f;
            PeakIn = 0f;
            Frames = 0;
            FramesOver1dB = 0;
        }

        /// <summary>The gain the knee asks for at <paramref name="level"/> (1 under the knee).</summary>
        public float Target(float level)
        {
            if (level <= _knee) return 1f;
            float span = _ceiling - _knee;
            float y = _knee + span * (float)Math.Tanh((level - _knee) / span);
            return y / level;
        }

        /// <summary>Limits interleaved <paramref name="data"/> in place (<paramref name="channels"/> per frame). Channels past
        /// <see cref="Configure"/>'s count pass through untouched, delayed with the rest only up to that count.</summary>
        public void Process(float[] data, int channels)
        {
            if (data == null || channels <= 0) return;
            int ch = Math.Min(channels, _channels);
            int frames = data.Length / channels;
            const float Over1dB = 0.891251f;
            for (int f = 0; f < frames; f++)
            {
                int o = f * channels;
                float level = 0f;
                for (int c = 0; c < ch; c++)
                {
                    float x = data[o + c];
                    float ax = x < 0f ? -x : x;
                    float mid = (-_h3[c] + 9f * _h2[c] + 9f * _h1[c] - x) * 0.0625f;
                    if (mid < 0f) mid = -mid;
                    if (ax > level) level = ax;
                    if (mid > level) level = mid;
                    _h3[c] = _h2[c];
                    _h2[c] = _h1[c];
                    _h1[c] = x;
                }
                if (level > PeakIn) PeakIn = level;

                float t = Target(level);
                if (t <= _cur) { _cur = t; _hold = _look; }
                else if (_hold > 0) _hold--;
                else _cur = t + (_cur - t) * _rel;

                _sum += _cur - _ring[_pos];
                _ring[_pos] = _cur;
                float g = (float)(_sum / _look);
                if (g > 1f) g = 1f;

                int d = _pos * _channels;
                for (int c = 0; c < ch; c++)
                {
                    float x = data[o + c];
                    data[o + c] = _delay[d + c] * g;
                    _delay[d + c] = x;
                }
                _pos++;
                if (_pos == _look)
                {
                    _pos = 0;
                    double s = 0.0;                       // once per look-ahead: no drift in the running sum
                    for (int i = 0; i < _look; i++) s += _ring[i];
                    _sum = s;
                }

                if (g < MinGain) MinGain = g;
                if (g < Over1dB) FramesOver1dB++;
                Frames++;
            }
        }
    }
}
