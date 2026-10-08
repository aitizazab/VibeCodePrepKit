using System;
using System.Numerics;

namespace JamKit.Combat
{
    /// <summary>One frame of shake: add <see cref="offset"/> to the camera position (2D) or to its
    /// yaw/pitch (FPS), and <see cref="roll"/> to its roll, both in the units of the max values.</summary>
    public struct ShakeSample
    {
        public Vector2 offset;
        public float roll;
    }

    /// <summary>
    /// Trauma-model screen shake: hits add trauma (0..1), it decays over time, and the shake is
    /// trauma squared, so small hits barely move the camera while big ones slam it, and stacking
    /// hits never shakes harder than the max. Smooth noise, not random jitter, and seedable.
    /// </summary>
    [Serializable]
    public sealed class ScreenShake
    {
        /// <summary>Largest offset at full trauma: world units for a 2D camera, degrees for FPS yaw/pitch.</summary>
        public float maxOffset = 0.5f;
        /// <summary>Largest roll at full trauma, in degrees.</summary>
        public float maxRoll = 5f;
        /// <summary>Trauma lost per second. 1 means a full-trauma shake lasts one second.</summary>
        public float decayPerSecond = 1.5f;
        /// <summary>Noise wiggles per second: higher feels like a rattle, lower like a sway.</summary>
        public float frequency = 15f;
        public int seed = 1;

        float _trauma;
        double _time;

        public float Trauma => _trauma;
        /// <summary>Trauma squared: how hard the camera is shaking right now, 0..1.</summary>
        public float Intensity => _trauma * _trauma;

        /// <summary>Adds trauma (typically 0.1 for a hit, 0.5+ for an explosion); the total stays in 0..1.</summary>
        public void AddTrauma(float amount)
        {
            float t = _trauma + amount;
            _trauma = t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _trauma = Math.Max(0f, _trauma - decayPerSecond * dt);
            _time += dt * frequency;
        }

        public ShakeSample Sample()
        {
            float k = _trauma * _trauma;
            var s = new ShakeSample();
            if (k <= 0f) return s;
            uint baseSeed = unchecked((uint)seed);
            s.offset = new Vector2(
                maxOffset * k * ValueNoise.Sample(_time, baseSeed),
                maxOffset * k * ValueNoise.Sample(_time, unchecked(baseSeed + 0x9E3779B9u)));
            s.roll = maxRoll * k * ValueNoise.Sample(_time, unchecked(baseSeed + 0x3C6EF372u));
            return s;
        }

        /// <summary>Stops all shake at once (scene change, pause menu).</summary>
        public void Clear() => _trauma = 0f;
    }
}
