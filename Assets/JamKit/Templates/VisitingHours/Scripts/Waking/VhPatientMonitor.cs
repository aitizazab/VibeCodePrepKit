using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// A bedside patient monitor that's alive (docs/redesign/day.md): the ECG sweeps across in green, the pleth in cyan, the
    /// heart rate, SpO2 and pressure in numbers on the right; it beeps on every beat (room 14's, spatial). The heart rate
    /// eases towards a target, so the bedside can make it climb when she says something that lands (<see cref="Raise"/>) or
    /// settle while she just sits with him (<see cref="Calm"/>). Drawn into its own small texture at 30 Hz from a buffer it
    /// keeps (nothing allocated per frame).
    /// </summary>
    public sealed class VhPatientMonitor : MonoBehaviour
    {
        const int W = 256, H = 160, Graph = 176;
        public float restingRate = 62f;
        public int spo2 = 98, systolic = 118, diastolic = 74;
        public bool audible;
        [Tooltip("The digits atlas (0-9, '/', '%', ' ': 32x48 each, readable).")]
        public Texture2D digits;

        Texture2D _tex;
        Color32[] _px;
        Color32[] _digitPx;
        float _rate, _target, _hold, _phase, _sweep, _redraw;
        int _lastY = -1, _lastP = -1, _shownRate = -1;
        AudioSource _beep;
        Material _mat;

        static AudioClip _beepClip;

        public float Rate => _rate;
        public Texture2D Screen => _tex;

        /// <summary>Builds its texture and returns the screen's material (an emissive screen in the waking style).</summary>
        public Material Build(VhWakingStyle style, Texture2D digitAtlas, bool beeps)
        {
            digits = digitAtlas;
            audible = beeps;
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "VH monitor screen", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            _px = new Color32[W * H];
            if (digits != null && digits.isReadable) _digitPx = digits.GetPixels32();
            _rate = _target = restingRate;
            Clear();
            _mat = style.Screen(_tex, 1.6f, "VH monitor");
            if (audible)
            {
                _beep = gameObject.AddComponent<AudioSource>();
                _beep.playOnAwake = false;
                _beep.spatialBlend = 1f;
                _beep.minDistance = 1.5f;
                _beep.maxDistance = 16f;
                _beep.rolloffMode = AudioRolloffMode.Linear;
                _beep.clip = BeepClip();
            }
            return _mat;
        }

        void OnDestroy()
        {
            if (_tex != null) VhUtil.Kill(_tex);
            if (_mat != null) VhUtil.Kill(_mat);
        }

        /// <summary>His heart climbs to <paramref name="bpm"/> and holds there <paramref name="seconds"/>, then eases back.</summary>
        public void Raise(float bpm, float seconds)
        {
            _target = Mathf.Max(_target, bpm);
            _hold = Mathf.Max(_hold, seconds);
        }

        /// <summary>His heart settles to <paramref name="bpm"/> (below resting) for <paramref name="seconds"/>.</summary>
        public void Calm(float bpm, float seconds)
        {
            _target = bpm;
            _hold = Mathf.Max(_hold, seconds);
        }

        /// <summary>The resting rate (a worse day after a forced exit is a higher one).</summary>
        public void SetResting(float bpm)
        {
            restingRate = bpm;
            if (_hold <= 0f) _target = bpm;
        }

        void Update()
        {
            if (_px == null) return;
            float dt = Time.deltaTime;
            if (_hold > 0f) _hold -= dt;
            else _target = Mathf.MoveTowards(_target, restingRate, dt * 1.5f);
            _rate = Mathf.MoveTowards(_rate, _target, dt * 4f);

            float beat = _rate / 60f;
            float before = _phase;
            _phase += dt * beat;
            if (_phase >= 1f)
            {
                _phase -= 1f;
                if (audible && _beep != null && GameRootAudible()) _beep.PlayOneShot(_beep.clip, 0.5f * Sfx());
            }

            // The sweep: 64 px a second across the graph area, the trace drawn as it goes, a gap cleared ahead of it.
            float from = _sweep;
            _sweep += dt * 64f;
            int x0 = Mathf.FloorToInt(from), x1 = Mathf.FloorToInt(_sweep);
            for (int x = x0; x < x1; x++)
            {
                int gx = x % Graph;
                if (gx == 0) { _lastY = -1; _lastP = -1; }
                float ph = Mathf.Repeat(before + (x - x0 + 1) / Mathf.Max(1f, x1 - x0) * (_phase - before + (_phase < before ? 1f : 0f)), 1f);
                ClearColumn((gx + 6) % Graph);
                ClearColumn((gx + 7) % Graph);
                int y = 112 + Mathf.RoundToInt(Ecg(ph) * 34f);
                int p = 30 + Mathf.RoundToInt(Pleth(ph) * 22f);
                Line(gx, _lastY < 0 ? y : _lastY, y, new Color32(80, 255, 140, 255));
                Line(gx, _lastP < 0 ? p : _lastP, p, new Color32(90, 210, 255, 255));
                _lastY = y;
                _lastP = p;
            }
            if (_sweep > Graph * 1000f) _sweep -= Graph * 1000f;

            int shown = Mathf.RoundToInt(_rate);
            if (shown != _shownRate)
            {
                _shownRate = shown;
                Numbers();
            }
            _redraw += dt;
            if (_redraw >= 1f / 30f)
            {
                _redraw = 0f;
                _tex.SetPixels32(_px);
                _tex.Apply(false);
            }
        }

        static float GameRootSfx() => JamKit.Shell.GameRoot.Exists ? JamKit.Shell.GameRoot.Settings.sfxVolume : 1f;
        static float Sfx() => GameRootSfx();
        static bool GameRootAudible() => !JamKit.Shell.GameRoot.Exists || !JamKit.Shell.GameRoot.Audio.Muted;

        /// <summary>A heartbeat's shape over one beat (P, QRS, T), -1..1 up.</summary>
        static float Ecg(float t)
        {
            float v = 0f;
            v += 0.12f * Mathf.Exp(-Sq((t - 0.12f) / 0.025f));
            v -= 0.12f * Mathf.Exp(-Sq((t - 0.205f) / 0.008f));
            v += 1.0f * Mathf.Exp(-Sq((t - 0.22f) / 0.01f));
            v -= 0.28f * Mathf.Exp(-Sq((t - 0.24f) / 0.01f));
            v += 0.25f * Mathf.Exp(-Sq((t - 0.45f) / 0.05f));
            return v;
        }

        static float Pleth(float t) => Mathf.Exp(-Sq((t - 0.32f) / 0.09f)) * 0.9f + Mathf.Exp(-Sq((t - 0.5f) / 0.06f)) * 0.25f;

        static float Sq(float x) => x * x;

        void Clear()
        {
            var bg = new Color32(6, 10, 12, 255);
            for (int i = 0; i < _px.Length; i++) _px[i] = bg;
            // The divider between the graphs and the numbers.
            for (int y = 8; y < H - 8; y++) _px[y * W + Graph + 2] = new Color32(40, 60, 64, 255);
            Numbers();
        }

        void ClearColumn(int x)
        {
            var bg = new Color32(6, 10, 12, 255);
            for (int y = 0; y < H; y++) _px[y * W + x] = bg;
        }

        void Line(int x, int ya, int yb, Color32 c)
        {
            int lo = Mathf.Clamp(Mathf.Min(ya, yb), 1, H - 2), hi = Mathf.Clamp(Mathf.Max(ya, yb), 1, H - 2);
            for (int y = lo; y <= hi; y++)
            {
                _px[y * W + x] = c;
                if (x + 1 < Graph) _px[y * W + x + 1] = c;
            }
        }

        /// <summary>The numbers on the right: the heart rate (big, green), SpO2 (cyan), pressure (white).</summary>
        void Numbers()
        {
            var bg = new Color32(6, 10, 12, 255);
            for (int y = 0; y < H; y++)
                for (int x = Graph + 4; x < W; x++) _px[y * W + x] = bg;
            Text(Mathf.Clamp(_shownRate < 0 ? Mathf.RoundToInt(restingRate) : _shownRate, 0, 999).ToString(), Graph + 8, 96, 1f, new Color32(90, 255, 150, 255));
            Text(spo2 + "%", Graph + 10, 48, 0.62f, new Color32(100, 220, 255, 255));
            Text(systolic + "/" + diastolic, Graph + 8, 12, 0.42f, new Color32(230, 236, 240, 255));
        }

        void Text(string s, int x, int y, float scale, Color32 c)
        {
            if (_digitPx == null) return;
            int gw = Mathf.RoundToInt(32 * scale), gh = Mathf.RoundToInt(48 * scale);
            int cx = x;
            for (int i = 0; i < s.Length; i++)
            {
                int glyph = s[i] >= '0' && s[i] <= '9' ? s[i] - '0' : s[i] == '/' ? 10 : s[i] == '%' ? 11 : 12;
                for (int py = 0; py < gh; py++)
                    for (int px = 0; px < gw; px++)
                    {
                        int sx = glyph * 32 + Mathf.Min(31, (int)(px / scale)), sy = Mathf.Min(47, (int)(py / scale));
                        var a = _digitPx[sy * digits.width + sx].a;
                        if (a < 40) continue;
                        int tx = cx + px, ty = y + py;
                        if (tx >= W || ty >= H) continue;
                        _px[ty * W + tx] = new Color32((byte)(c.r * a / 255), (byte)(c.g * a / 255), (byte)(c.b * a / 255), 255);
                    }
                cx += Mathf.RoundToInt(gw * 0.78f);
            }
        }

        static AudioClip BeepClip()
        {
            if (_beepClip != null) return _beepClip;
            const int rate = 44100;
            int n = (int)(rate * 0.09f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (n - i) / 600f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 960f * t) * 0.35f * env;
            }
            _beepClip = AudioClip.Create("vh_monitor_beep", n, 1, rate, false);
            _beepClip.SetData(data, 0);
            return _beepClip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _beepClip = null;
    }
}
