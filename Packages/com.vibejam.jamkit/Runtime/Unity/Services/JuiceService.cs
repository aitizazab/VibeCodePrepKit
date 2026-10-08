using JamKit.Combat;
using JamKit.Flow;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Game feel in one place: hit stop (core <see cref="HitStop"/>, folded into Time.timeScale by
    /// GameRoot), screen shake (core <see cref="ScreenShake"/>, applied by <see cref="CameraShaker"/>),
    /// a full-screen flash and floating text. The player's "Screen shake" setting scales the shake.
    /// </summary>
    public sealed class JuiceService : MonoBehaviour
    {
        public readonly HitStop HitStopState = new HitStop();
        public readonly ScreenShake ShakeState = new ScreenShake { maxOffset = 0.35f, maxRoll = 1.5f, decayPerSecond = 1.6f };

        ShellUI _ui;
        float _shakeSetting = 1f;

        public void Init(ShellUI ui) => _ui = ui;

        public void ApplySettings(Settings s) => _shakeSetting = s != null ? Mathf.Clamp01(s.screenShake) : 1f;

        /// <summary>Freezes (timeScale 0) or slows game time for a few real milliseconds: 0.05-0.1 s per hit.</summary>
        public void HitStop(float seconds, float timeScale = 0f) => HitStopState.Request(seconds, timeScale);

        /// <summary>Adds trauma: 0.2 a bump, 0.5 a hit, 1 an explosion. Adds a CameraShaker to the main camera if needed.</summary>
        public void Shake(float trauma)
        {
            if (_shakeSetting <= 0f || !(trauma > 0f)) return;
            ShakeState.AddTrauma(trauma);
            CameraShaker.EnsureOnMainCamera();
        }

        /// <summary>Full-screen colour flash that fades out over <paramref name="seconds"/> (real time).</summary>
        public void Flash(Color color, float seconds = 0.15f)
        {
            if (_ui != null) _ui.Flash(color, seconds);
        }

        public void Flash() => Flash(new Color(1f, 1f, 1f, 0.6f));

        /// <summary>Text that rises and fades at a world position ("+1", "-2", "Combo!").</summary>
        public void FloatingText(Vector3 worldPosition, string text, Color color)
        {
            if (_ui != null) _ui.FloatText(worldPosition, text, color);
        }

        /// <summary>This frame's shake, already scaled by the player's setting.</summary>
        public ShakeSample CurrentShake
        {
            get
            {
                var s = ShakeState.Sample();
                s.offset *= _shakeSetting;
                s.roll *= _shakeSetting;
                return s;
            }
        }

        /// <summary>Called by GameRoot first thing every frame, with real time.</summary>
        public void Tick(float unscaledDt, bool paused)
        {
            HitStopState.Tick(unscaledDt);
            if (!paused) ShakeState.Tick(unscaledDt);
        }

        /// <summary>Scene changes and quitting to the title: no stop or shake carries over.</summary>
        public void ResetAll()
        {
            HitStopState.Clear();
            ShakeState.Clear();
        }
    }
}
