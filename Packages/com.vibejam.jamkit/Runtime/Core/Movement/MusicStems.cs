using System;

namespace JamKit.Movement
{
    /// <summary>How the speed music's stems switch and fade (docs/MOVEMENT.md, "Speed music").</summary>
    [Serializable]
    public class StemMixDef
    {
        /// <summary>Seconds for a stem to fade fully in once it's switched on.</summary>
        public float fadeIn = 1f;
        /// <summary>Seconds for a stem to fade fully out once it's switched off (slow, so a stumble doesn't drop the music).</summary>
        public float fadeOut = 2f;
        /// <summary>Stems switch only on these beat lines (4 = every bar of 4/4).</summary>
        public int quantizeBeats = 4;
    }

    /// <summary>
    /// The speed music's vertical layering, as pure C#: which stems are on for a speed tier, switched only when the
    /// music crosses a bar line, and each stem's gain fading in over <see cref="StemMixDef.fadeIn"/> and out over
    /// <see cref="StemMixDef.fadeOut"/>. Stem slots: 0 base (always), 1 drive (fast and up), 2 peak (flying),
    /// 3 tension (never by speed: the game drives it with a floor, like the dream escape's gap).
    /// A floor (0..1) keeps a stem at least that loud whatever the tier, continuously (no quantising).
    /// </summary>
    public sealed class StemMixer
    {
        public const int Base = 0, Drive = 1, Peak = 2, Tension = 3, Slots = 4;
        /// <summary>A stem tier that speed never reaches.</summary>
        public const int Never = 99;
        /// <summary>The tier each slot needs: base always, drive at fast, peak at flying, tension never.</summary>
        public static readonly int[] SlotTiers = { SpeedTier.Running, SpeedTier.Fast, SpeedTier.Flying, Never };
        public static readonly string[] SlotNames = { "base", "drive", "peak", "tension" };

        readonly StemMixDef _def;
        readonly bool[] _on = new bool[Slots];
        readonly float[] _gain = new float[Slots];
        readonly float[] _floor = new float[Slots];
        double _lastBeat = double.NaN;

        /// <summary>How many times the set of stems changed (after the first tick).</summary>
        public int SwitchCount;
        /// <summary>The bar line (in beats) of the last switch, and the beat position when it was applied.</summary>
        public double LastSwitchLine = -1, LastSwitchAt = -1;

        public StemMixer(StemMixDef def) { _def = def ?? new StemMixDef(); }

        public StemMixDef Def => _def;

        /// <summary>Does a stem that needs <paramref name="stemTier"/> play at speed tier <paramref name="tier"/>?</summary>
        public static bool Wants(int stemTier, int tier) => stemTier <= tier;

        public bool IsOn(int slot) => _on[slot];
        public float Gain(int slot) => _gain[slot];
        public float Floor(int slot) => _floor[slot];
        public void SetFloor(int slot, float v) => _floor[slot] = v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Stems switched on by speed now (base counts).</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Slots; i++) if (_on[i]) n++;
                return n;
            }
        }

        public void Reset()
        {
            _lastBeat = double.NaN;
            SwitchCount = 0;
            LastSwitchLine = LastSwitchAt = -1;
            for (int i = 0; i < Slots; i++) { _on[i] = false; _gain[i] = 0f; _floor[i] = 0f; }
        }

        /// <summary>Advance to music position <paramref name="beat"/> (beats since the stems started, increasing) at
        /// speed tier <paramref name="tier"/>. Returns true when the stems switched this tick (only on a bar line).</summary>
        public bool Tick(double beat, int tier, float dt)
        {
            bool switched = false;
            if (double.IsNaN(_lastBeat))
            {
                for (int i = 0; i < Slots; i++) _on[i] = Wants(SlotTiers[i], tier);
            }
            else
            {
                int q = Math.Max(1, _def.quantizeBeats);
                double line = Math.Floor(beat / q);
                if (line > Math.Floor(_lastBeat / q))
                {
                    for (int i = 0; i < Slots; i++)
                    {
                        bool w = Wants(SlotTiers[i], tier);
                        if (w != _on[i]) { _on[i] = w; switched = true; }
                    }
                    if (switched)
                    {
                        SwitchCount++;
                        LastSwitchLine = line * q;
                        LastSwitchAt = beat;
                    }
                }
            }
            _lastBeat = beat;
            float up = _def.fadeIn > 1e-4f ? dt / _def.fadeIn : 1f;
            float down = _def.fadeOut > 1e-4f ? dt / _def.fadeOut : 1f;
            for (int i = 0; i < Slots; i++)
            {
                float target = Math.Max(_on[i] ? 1f : 0f, _floor[i]);
                float g = _gain[i];
                _gain[i] = target > g ? Math.Min(target, g + up) : Math.Max(target, g - down);
            }
            return switched;
        }

        /// <summary>How open (bright) the music is for a speed (A2): 0 muffled at a standstill, 1 full at flying speed,
        /// and held full while the flying tier lasts.</summary>
        public static float Open(float speed, float flyingEnter, float flyingBlend) =>
            Math.Max(MoveMath.SmoothStep(0f, flyingEnter, speed), flyingBlend);

        /// <summary>A muffled copy of a loop (A2's pre-render, so it also works where there's no real-time filter, as on
        /// WebGL): a 4-pole low-pass run forwards then backwards around the loop, so it has no phase shift (a crossfade
        /// with the original doesn't comb) and no seam. Writes in place.</summary>
        public static void LowPassZeroPhase(float[] loop, int rate, float hz)
        {
            if (loop == null || loop.Length < 2) return;
            float a = 1f - (float)Math.Exp(-2.0 * Math.PI * hz / rate);
            Pass(loop, a, false);
            Pass(loop, a, false);
            Pass(loop, a, true);
            Pass(loop, a, true);
        }

        static void Pass(float[] x, float a, bool backward)
        {
            int n = x.Length;
            float y = 0f;
            // One warm-up lap around the loop, so the filter's state at the start is what the loop's end left.
            for (int lap = 0; lap < 2; lap++)
                for (int k = 0; k < n; k++)
                {
                    int i = backward ? n - 1 - k : k;
                    y += a * (x[i] - y);
                    if (lap == 1) x[i] = y;
                }
        }
    }
}
