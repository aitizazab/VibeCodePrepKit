using System;

namespace JamKit.Movement
{
    /// <summary>
    /// The first-person movement's sounds made from code (pure C#, no Unity), and the mix rules that pick them.
    /// Recorded clips (CC0, Runtime/Audio/Movement) are preferred when they exist; these are the fallback, and
    /// they are the real sound for the loops (wind, slide scrape, wallrun rub) and the slide-boost whoosh.
    /// One-shots come in <see cref="Variants"/> seeded variants (round-robin); loops are seamless (the tail is
    /// crossfaded onto the head, and every modulation has a whole number of cycles per loop). One-shots peak at
    /// -3 dBFS, loops at -6 dBFS: never above -1 dBFS.
    /// </summary>
    public static class MoveSounds
    {
        public const int Rate = 32000;
        /// <summary>Round-robin variants per generated one-shot.</summary>
        public const int Variants = 4;
        public const float OneShotPeak = 0.708f; // -3 dBFS
        public const float LoopPeak = 0.5f;      // -6 dBFS
        public const float MaxPeak = 0.891f;     // -1 dBFS: nothing may go over this

        /// <summary>The loops: a low wind body, a bright air rush on top (both follow speed), the slide's
        /// scrape, the wallrun's shoe rub, the supersonic whistle (flying) and Nadia's breathing, calm and fast.</summary>
        public static readonly string[] Loops =
        {
            "mv_wind", "mv_wind_high", "mv_slide", "mv_wallrun", "mv_supersonic", "mv_breath_calm", "mv_breath_fast",
        };

        /// <summary>Every one-shot movement event, as MovementAudio plays them.</summary>
        public static readonly string[] OneShots =
        {
            "mv_step", "mv_step_heavy", "mv_jump", "mv_djump", "mv_walljump", "mv_catch",
            "mv_land_soft", "mv_land", "mv_land_hard", "mv_mantle", "mv_kick", "mv_slidein", "mv_boost",
        };

        public static bool IsLoop(string id) => Array.IndexOf(Loops, id) >= 0;
        public static bool Knows(string id) => IsLoop(id) || Array.IndexOf(OneShots, id) >= 0;
        public static int VariantCount(string id) => IsLoop(id) ? 1 : Knows(id) ? Variants : 0;

        /// <summary>The samples (mono, <see cref="Rate"/>) of one variant, or null for an unknown id.</summary>
        public static float[] Make(string id, int variant)
        {
            if (!Knows(id)) return null;
            int v = ((variant % Variants) + Variants) % Variants;
            var rng = new Rng(0x9E3779B9u ^ (uint)(Hash(id) * 31 + v * 7919 + 1));
            float j = v / (float)(Variants - 1); // 0..1 across the variants
            switch (id)
            {
                case "mv_wind": return WindLow();
                case "mv_wind_high": return WindHigh();
                case "mv_slide": return SlideScrape();
                case "mv_wallrun": return WallRub();
                case "mv_supersonic": return Supersonic();
                case "mv_breath_calm": return Breath(4f, 1, 0.4f, 1.5f, 1.9f);
                case "mv_breath_fast": return Breath(2f, 3, 0.05f, 0.27f, 0.33f);
            }
            var b = new float[N(id == "mv_land_hard" ? 0.6f : 0.45f)];
            switch (id)
            {
                case "mv_step":
                    Step(b, 0f, 0.6f, 1f + 0.08f * (j - 0.5f), ref rng);
                    break;
                case "mv_step_heavy":
                    Thump(b, 0f, 88f - 8f * j, 46f, 0.14f, 0.05f, 0.9f);
                    Noise(b, 0f, 0.1f, 0.002f, 0.03f, 90f, 420f, 0.3f, ref rng);
                    break;
                case "mv_jump":
                    Noise(b, 0f, 0.06f, 0.002f, 0.02f, 150f, 1800f, 0.4f, ref rng);
                    Thump(b, 0f, 140f, 90f, 0.06f, 0.025f, 0.25f);
                    Sweep(b, 0.01f, 0.22f, 700f + 150f * j, 2600f + 300f * j, 0.9f, 0.55f, ref rng);
                    break;
                case "mv_djump":
                    // A jump-kit puff: a hiss with a hard front, a low whump under it and a faint rising pop.
                    Noise(b, 0f, 0.22f, 0.003f, 0.06f, 900f, 9000f, 0.9f, ref rng);
                    Sweep(b, 0f, 0.18f, 3000f + 400f * j, 7000f, 1.2f, 0.35f, ref rng);
                    Thump(b, 0f, 120f, 55f, 0.12f, 0.04f, 0.45f);
                    Tone(b, 0.005f, 560f + 60f * j, 980f + 60f * j, 0.09f, 0.1f);
                    break;
                case "mv_walljump":
                    Noise(b, 0f, 0.03f, 0.0008f, 0.008f, 1500f, 7000f, 0.8f, ref rng);
                    Thump(b, 0f, 165f - 10f * j, 90f, 0.07f, 0.025f, 0.5f);
                    Sweep(b, 0.02f, 0.2f, 1200f, 5000f + 600f * j, 0.8f, 0.35f, ref rng);
                    break;
                case "mv_catch":
                    Thump(b, 0f, 130f - 8f * j, 75f, 0.1f, 0.035f, 0.6f);
                    Noise(b, 0f, 0.06f, 0.001f, 0.02f, 300f, 2500f, 0.45f, ref rng);
                    Sweep(b, 0.02f, 0.26f, 2400f + 200f * j, 900f, 0.8f, 0.3f, ref rng);
                    break;
                case "mv_land_soft":
                    Step(b, 0f, 0.55f, 1.02f + 0.06f * (j - 0.5f), ref rng);
                    Step(b, 0.03f + 0.01f * j, 0.45f, 0.97f, ref rng);
                    break;
                case "mv_land":
                    Thump(b, 0f, 100f - 6f * j, 45f, 0.2f, 0.06f, 0.8f);
                    Noise(b, 0f, 0.12f, 0.001f, 0.04f, 80f, 900f, 0.5f, ref rng);
                    Step(b, 0.004f, 0.45f, 1f, ref rng);
                    break;
                case "mv_land_hard":
                    Thump(b, 0f, 82f - 6f * j, 34f, 0.35f, 0.11f, 1f);
                    Noise(b, 0f, 0.25f, 0.001f, 0.09f, 60f, 600f, 0.6f, ref rng);
                    Noise(b, 0f, 0.08f, 0.001f, 0.03f, 400f, 3000f, 0.3f, ref rng);
                    Grit(b, 0.005f, 0.14f, 700f, 1800f, 0.3f, ref rng);
                    break;
                case "mv_mantle":
                    Noise(b, 0f, 0.07f, 0.002f, 0.03f, 800f, 4000f, 0.3f, ref rng);
                    Sweep(b, 0.03f, 0.3f, 600f + 100f * j, 1800f, 0.7f, 0.45f, ref rng);
                    Step(b, 0.24f + 0.02f * j, 0.45f, 0.95f, ref rng);
                    break;
                case "mv_kick":
                    Thump(b, 0f, 96f - 6f * j, 50f, 0.12f, 0.04f, 0.8f);
                    Noise(b, 0f, 0.07f, 0.001f, 0.025f, 120f, 1500f, 0.6f, ref rng);
                    Sweep(b, 0.02f, 0.22f, 1500f, 6000f + 500f * j, 0.9f, 0.3f, ref rng);
                    break;
                case "mv_slidein":
                    Thump(b, 0f, 110f - 6f * j, 60f, 0.12f, 0.04f, 0.5f);
                    Noise(b, 0f, 0.16f, 0.002f, 0.06f, 200f, 2200f, 0.45f, ref rng);
                    Grit(b, 0.01f, 0.16f, 900f, 2200f + 200f * j, 0.3f, ref rng);
                    break;
                case "mv_boost":
                    Sweep(b, 0f, 0.4f, 1200f + 200f * j, 8500f - 600f * j, 1.1f, 0.9f, ref rng, true);
                    Sweep(b, 0f, 0.4f, 400f, 1600f + 200f * j, 0.7f, 0.4f, ref rng, true);
                    break;
            }
            int end = TrimTail(b);
            var o = new float[end];
            Array.Copy(b, o, end);
            Fade(o, 0.001f, 0.006f);
            Normalize(o, OneShotPeak);
            return o;
        }

        // ---------------------------------------------------------------- loops

        static float[] WindLow()
        {
            int n = N(6f), fade = N(0.4f);
            var b = new float[n + fade];
            var rng = new Rng(17u);
            float lp1 = 0f, lp2 = 0f;
            var howl = new Biquad();
            for (int i = 0; i < b.Length; i++)
            {
                float x = rng.Next();
                lp1 += OnePole(300f) * (x - lp1);
                lp2 += OnePole(300f) * (lp1 - lp2);
                float lfo = Lfo(i, n, 1, 0.3f) * 0.6f + Lfo(i, n, 3, 1.7f) * 0.4f;
                howl.BandPass(480f + 180f * Lfo(i, n, 2, 0.9f), 3f);
                float h = howl.Process(x);
                float gust = 0.72f + 0.28f * lfo;
                b[i] = (lp2 * 3.2f + h * 0.25f) * gust;
            }
            return FinishLoop(b, n, fade);
        }

        static float[] WindHigh()
        {
            int n = N(4f), fade = N(0.3f);
            var b = new float[n + fade];
            var rng = new Rng(29u);
            float hp = 0f, lp = 0f, prev = 0f;
            var whistle = new Biquad();
            for (int i = 0; i < b.Length; i++)
            {
                float x = rng.Next();
                hp = OnePoleHp(1200f, hp, x, prev);
                prev = x;
                lp += OnePole(5000f) * (hp - lp);
                whistle.BandPass(3200f + 900f * Lfo(i, n, 2, 0.4f), 4f);
                float w = whistle.Process(x);
                float am = 0.8f + 0.2f * Lfo(i, n, 5, 2.1f);
                b[i] = (lp * 0.8f + w * 0.35f) * am;
            }
            return FinishLoop(b, n, fade);
        }

        static float[] SlideScrape()
        {
            int n = N(2f), fade = N(0.25f);
            var b = new float[n + fade];
            var rng = new Rng(41u);
            var body = new Biquad();
            var grit = new Biquad();
            body.BandPass(900f, 0.7f);
            grit.BandPass(2600f, 1.2f);
            float rumble = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float x = rng.Next();
                float imp = rng.Unit() < 900f / Rate ? rng.Next() * 4f : 0f;
                rumble += OnePole(140f) * (x - rumble);
                float am = 0.9f + 0.1f * Lfo(i, n, 4, 0.2f);
                b[i] = (body.Process(x) * 0.5f + grit.Process(imp) * 0.6f + rumble * 1.6f) * am;
            }
            return FinishLoop(b, n, fade);
        }

        static float[] WallRub()
        {
            int n = N(2f), fade = N(0.25f);
            var b = new float[n + fade];
            var rng = new Rng(53u);
            var body = new Biquad();
            var grit = new Biquad();
            body.BandPass(1100f, 1f);
            grit.BandPass(1800f, 1.4f);
            for (int i = 0; i < b.Length; i++)
            {
                float x = rng.Next();
                float imp = rng.Unit() < 250f / Rate ? rng.Next() * 3f : 0f;
                float am = 0.85f + 0.15f * Lfo(i, n, 3, 0.6f);
                b[i] = (body.Process(x) * 0.5f + grit.Process(imp) * 0.3f) * am;
            }
            return FinishLoop(b, n, fade);
        }

        /// <summary>The supersonic layer (A4): a high shimmering whistle. Sines a fifth and an octave apart, each
        /// with a twin 1.5 Hz off (a slow shimmer of beating), a gentle vibrato and a breathy band of noise. Every
        /// frequency fits a whole number of cycles in the 4 s loop.</summary>
        static float[] Supersonic()
        {
            int n = N(4f), fade = N(0.3f);
            var b = new float[n + fade];
            var rng = new Rng(61u);
            var air = new Biquad();
            double[] hz = { 1760.0, 1761.5, 2640.0, 2641.5, 3520.0 };
            float[] amp = { 0.3f, 0.3f, 0.18f, 0.18f, 0.08f };
            for (int i = 0; i < b.Length; i++)
            {
                double t = i / (double)Rate;
                // Vibrato as a phase wobble with 5 whole cycles per loop (so the phase comes back round).
                double vib = 0.0024 * Math.Sin(2.0 * Math.PI * 5.0 * i / n); // about 0.3 % of pitch (1.25 Hz)
                float x = 0f;
                for (int k = 0; k < hz.Length; k++) x += (float)Math.Sin(2.0 * Math.PI * hz[k] * t + hz[k] * vib) * amp[k];
                air.BandPass(3000f + 400f * Lfo(i, n, 3, 0.5f), 6f);
                float shimmer = 0.75f + 0.25f * Lfo(i, n, 7, 1.1f);
                b[i] = (x + air.Process(rng.Next()) * 0.5f) * shimmer;
            }
            return FinishLoop(b, n, fade);
        }

        /// <summary>Breathing (A6): <paramref name="cycles"/> breaths in a loop of <paramref name="seconds"/>, each an
        /// inhale (brighter) then an exhale (darker) of shaped noise, with a pause after.</summary>
        static float[] Breath(float seconds, int cycles, float gap, float inSec, float outSec)
        {
            int n = N(seconds), fade = N(0.05f);
            var b = new float[n + fade];
            var rng = new Rng(67u + (uint)cycles);
            var f1 = new Biquad();
            var f2 = new Biquad();
            float cycle = seconds / cycles;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (i % n) / (float)Rate;
                float c = t % cycle;
                float e = 0f, bright = 0f;
                if (c >= gap && c < gap + inSec) { float u = (c - gap) / inSec; e = (float)Math.Pow(Math.Sin(Math.PI * u), 2) * 0.8f; bright = 1f; }
                else if (c >= gap + inSec && c < gap + inSec + outSec) { float u = (c - gap - inSec) / outSec; e = (float)Math.Pow(Math.Sin(Math.PI * Math.Pow(u, 0.7)), 2); }
                f1.BandPass(900f + 500f * bright, 0.9f);
                f2.BandPass(2100f + 700f * bright, 2.5f);
                float x = rng.Next();
                b[i] = (f1.Process(x) * 0.7f + f2.Process(x) * 0.4f) * e;
            }
            return FinishLoop(b, n, fade);
        }

        static float[] FinishLoop(float[] b, int n, int fade)
        {
            var o = Seamless(b, n, fade);
            Normalize(o, LoopPeak);
            return o;
        }

        /// <summary>A seamless loop of <paramref name="n"/> samples from a render of n + fade samples: the tail
        /// past n is crossfaded (equal power) onto the head, so the last sample runs straight into the first.</summary>
        public static float[] Seamless(float[] b, int n, int fade)
        {
            var o = new float[n];
            Array.Copy(b, o, n);
            for (int i = 0; i < fade && n + i < b.Length; i++)
            {
                float t = (i + 0.5f) / fade;
                o[i] = b[i] * (float)Math.Sin(t * Math.PI * 0.5) + b[n + i] * (float)Math.Cos(t * Math.PI * 0.5);
            }
            return o;
        }

        // ---------------------------------------------------------------- layers

        static void Step(float[] b, float at, float vol, float pitch, ref Rng rng)
        {
            Noise(b, at, 0.07f, 0.0015f, 0.018f, 120f, 1400f * pitch, 0.6f * vol, ref rng);
            Thump(b, at, 110f * pitch, 65f * pitch, 0.07f, 0.022f, 0.5f * vol);
            Grit(b, at, 0.04f, 1200f, 2500f * pitch, 0.15f * vol, ref rng);
        }

        /// <summary>Filtered noise (high-pass then a 2-pole low-pass) with a linear attack and an exponential decay.</summary>
        static void Noise(float[] b, float at, float dur, float attack, float tau, float hpHz, float lpHz, float vol, ref Rng rng)
        {
            int s = N(at), len = N(dur);
            float hp = 0f, prev = 0f, l1 = 0f, l2 = 0f, a = OnePole(lpHz);
            for (int i = 0; i < len && s + i < b.Length; i++)
            {
                float x = rng.Next();
                hp = OnePoleHp(hpHz, hp, x, prev);
                prev = x;
                l1 += a * (hp - l1);
                l2 += a * (l1 - l2);
                b[s + i] += l2 * 2f * vol * Env(i / (float)Rate, attack, tau);
            }
        }

        /// <summary>A band-pass swept from f0 to f1 (a whoosh); a swell (rise then fall) when <paramref name="swell"/>.</summary>
        static void Sweep(float[] b, float at, float dur, float f0, float f1, float q, float vol, ref Rng rng, bool swell = false)
        {
            int s = N(at), len = N(dur);
            var bp = new Biquad();
            for (int i = 0; i < len && s + i < b.Length; i++)
            {
                float t = i / (float)len;
                bp.BandPass(f0 * (float)Math.Pow(f1 / f0, t), q);
                float e = swell ? (float)Math.Pow(Math.Sin(Math.PI * Math.Pow(t, 0.7)), 1.5) : (float)Math.Sin(Math.PI * Math.Min(1.0, t * 3.0) * 0.5) * (1f - t) * (1f - t);
                b[s + i] += bp.Process(rng.Next()) * 1.6f * vol * e;
            }
        }

        /// <summary>A sine falling exponentially in pitch, with a 2 ms attack and an exponential decay.</summary>
        static void Thump(float[] b, float at, float hz0, float hz1, float dur, float tau, float vol)
        {
            int s = N(at), len = N(dur);
            double ph = 0.0;
            for (int i = 0; i < len && s + i < b.Length; i++)
            {
                float t = i / (float)len;
                float hz = hz0 * (float)Math.Pow(hz1 / hz0, t);
                ph += 2.0 * Math.PI * hz / Rate;
                b[s + i] += (float)Math.Sin(ph) * vol * Env(i / (float)Rate, 0.002f, tau) * (1f - t);
            }
        }

        static void Tone(float[] b, float at, float hz0, float hz1, float dur, float vol)
        {
            int s = N(at), len = N(dur);
            double ph = 0.0;
            for (int i = 0; i < len && s + i < b.Length; i++)
            {
                float t = i / (float)len;
                ph += 2.0 * Math.PI * (hz0 + (hz1 - hz0) * t) / Rate;
                b[s + i] += (float)Math.Sin(ph) * vol * (float)Math.Sin(Math.PI * t);
            }
        }

        /// <summary>Sparse random clicks through a band-pass: grit, gravel, a shoe's rough sole.</summary>
        static void Grit(float[] b, float at, float dur, float perSecond, float hz, float vol, ref Rng rng)
        {
            int s = N(at), len = N(dur);
            var bp = new Biquad();
            bp.BandPass(hz, 1.2f);
            for (int i = 0; i < len && s + i < b.Length; i++)
            {
                float imp = rng.Unit() < perSecond / Rate ? rng.Next() * 4f : 0f;
                b[s + i] += bp.Process(imp) * vol * (1f - i / (float)len);
            }
        }

        // ---------------------------------------------------------------- helpers

        static int N(float seconds) => (int)(seconds * Rate + 0.5f);

        static float Env(float t, float attack, float tau) => t < attack ? t / attack : (float)Math.Exp(-(t - attack) / tau);

        static float OnePole(float hz) => 1f - (float)Math.Exp(-2.0 * Math.PI * hz / Rate);

        static float OnePoleHp(float hz, float y, float x, float prevX)
        {
            float rc = 1f / (2f * (float)Math.PI * hz), dt = 1f / Rate;
            float a = rc / (rc + dt);
            return a * (y + x - prevX);
        }

        /// <summary>A sine with a whole number of cycles over n samples (so it loops), in -1..1.</summary>
        static float Lfo(int i, int n, int cycles, float phase) => (float)Math.Sin(2.0 * Math.PI * cycles * i / n + phase);

        static int Hash(string s)
        {
            int h = 17;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h & 0x7fffffff;
        }

        /// <summary>The length up to the last sample above -60 dB of the peak (plus 5 ms).</summary>
        static int TrimTail(float[] b)
        {
            float pk = Peak(b), floor = pk * 0.001f;
            int last = b.Length - 1;
            while (last > 0 && Math.Abs(b[last]) < floor) last--;
            return Math.Min(b.Length, last + 1 + N(0.005f));
        }

        static void Fade(float[] b, float inSec, float outSec)
        {
            int fi = Math.Min(N(inSec), b.Length), fo = Math.Min(N(outSec), b.Length);
            for (int i = 0; i < fi; i++) b[i] *= i / (float)fi;
            for (int i = 0; i < fo; i++) b[b.Length - 1 - i] *= i / (float)fo;
        }

        public static float Peak(float[] b)
        {
            float p = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float a = Math.Abs(b[i]);
                if (a > p) p = a;
            }
            return p;
        }

        public static void Normalize(float[] b, float peak)
        {
            float p = Peak(b);
            if (p <= 1e-6f) return;
            float k = peak / p;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        /// <summary>xorshift32: the same sound every run.</summary>
        struct Rng
        {
            uint _s;
            public Rng(uint seed) { _s = seed == 0u ? 1u : seed; }
            public float Unit()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return (_s >> 8) / 16777216f;
            }
            public float Next() => Unit() * 2f - 1f;
        }

        /// <summary>RBJ band-pass (0 dB peak), retunable per sample.</summary>
        struct Biquad
        {
            float _b0, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

            public void BandPass(float hz, float q)
            {
                double w = 2.0 * Math.PI * Math.Min(hz, Rate * 0.45f) / Rate;
                double alpha = Math.Sin(w) / (2.0 * q);
                double a0 = 1.0 + alpha;
                _b0 = (float)(alpha / a0);
                _b2 = (float)(-alpha / a0);
                _a1 = (float)(-2.0 * Math.Cos(w) / a0);
                _a2 = (float)((1.0 - alpha) / a0);
            }

            public float Process(float x)
            {
                float y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1;
                _x1 = x;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }
    }

    /// <summary>
    /// How the movement's sound follows the body (MovementAudio reads these each frame; pure so dotnet tests them).
    /// Footsteps get heavier and a touch lower as you go from walk to sprint; landings pick a tier by fall speed so
    /// ordinary hops stay soft; the wind body comes in from 5 m/s and the bright air rush on top from 11 m/s.
    /// </summary>
    public static class MoveSoundMix
    {
        public enum LandTier { Step, Soft, Normal, Hard }

        /// <summary>0 at walk speed, 1 at sprint and above.</summary>
        public static float StepWeight(float speed, float walk, float sprint) =>
            sprint > walk ? MoveMath.Clamp01((speed - walk) / (sprint - walk)) : 1f;

        public static float StepVolume(float weight) => 0.6f + 0.4f * weight;
        public static float StepPitch(float weight) => 1.04f - 0.08f * weight;
        /// <summary>The low thump layered under a footstep: none at walk, about half at sprint.</summary>
        public static float StepHeavy(float weight) => 0.55f * weight * weight;

        /// <summary>Fall speed (m/s, positive) to a landing tier: a plain jump lands soft (about 7.8 m/s), a jump
        /// plus double jump normal (about 10.4), a real drop hard (12.5 and over).</summary>
        public static LandTier Landing(float impact) =>
            impact < 3.5f ? LandTier.Step : impact < 8.5f ? LandTier.Soft : impact < 12.5f ? LandTier.Normal : LandTier.Hard;

        public static float LandGain(float impact) => 0.5f + 0.5f * MoveMath.Clamp01((impact - 3f) / 15f);

        public static float WindLow(float speed) => MoveMath.SmoothStep(5f, 16f, speed);
        public static float WindHigh(float speed) => MoveMath.SmoothStep(11f, 21f, speed);
        public static float WindPitch(float speed) => 0.9f + 0.25f * MoveMath.Clamp01(speed / 22f);

        /// <summary>The slide scrape fades out as the slide slows (silent at 4 m/s, where a slide ends).</summary>
        public static float SlideGain(float speed) => MoveMath.Clamp01((speed - 4f) / 5f);
        public static float SlidePitch(float speed) => 0.85f + 0.35f * MoveMath.Clamp01((speed - 4f) / 10f);

        /// <summary>A5, tunnel focus: the gain on world and ambient sounds (not the music or wind) while flying,
        /// 1 - amount x the flying blend.</summary>
        public static float Duck(float flyingBlend, float amount) => 1f - MoveMath.Clamp01(amount) * MoveMath.Clamp01(flyingBlend);

        /// <summary>A4: the supersonic whistle follows the flying blend, a touch higher the faster you go.</summary>
        public static float SupersonicPitch(float speed, float flyingEnter) => 1f + 0.06f * MoveMath.Clamp01((speed - flyingEnter) / 9f);

        /// <summary>A6: exertion (0..1) builds while fast or flying (full after <paramref name="build"/> seconds of
        /// flying, half as fast while merely fast) and recovers over <paramref name="recover"/> seconds below that.</summary>
        public static float Exertion(float e, int tier, float dt, float build, float recover)
        {
            if (tier >= SpeedTier.Fast)
            {
                float rate = (tier >= SpeedTier.Flying ? 1f : 0.5f) / Math.Max(0.01f, build);
                return Math.Min(1f, e + rate * dt);
            }
            return Math.Max(0f, e - dt / Math.Max(0.01f, recover));
        }

        /// <summary>Round-robin: a random variant that is never the last one played (r01 in 0..1).</summary>
        public static int NextVariant(int count, int last, float r01)
        {
            if (count <= 1) return 0;
            bool skip = last >= 0 && last < count;
            int span = skip ? count - 1 : count;
            int k = (int)(r01 * span);
            if (k >= span) k = span - 1;
            if (k < 0) k = 0;
            if (skip && k >= last) k++;
            return k;
        }
    }
}
