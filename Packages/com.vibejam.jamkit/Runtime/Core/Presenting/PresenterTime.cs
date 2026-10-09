using System;
using JamKit.Flow;

namespace JamKit.Presenting
{
    /// <summary>
    /// Presenter mode's hold on game time: slow motion (1, 0.5, 0.25), a freeze frame, and the
    /// "backstage" stop while the presenter panel is open. Folded into the game's time scale by
    /// <see cref="GameTime.Scale"/>, on top of hit stop and the debug menu's speed.
    /// </summary>
    public sealed class PresenterTime
    {
        public static readonly float[] SlowSteps = { 1f, 0.5f, 0.25f };

        int _step;
        bool _frozen, _backstage;

        public int SlowStep => _step;
        public float SlowMotion => SlowSteps[_step];

        public bool Frozen
        {
            get => _frozen;
            set => _frozen = value;
        }

        /// <summary>True while the presenter panel is open: nothing moves behind it.</summary>
        public bool Backstage
        {
            get => _backstage;
            set => _backstage = value;
        }

        /// <summary>The factor on game time: 0 frozen or backstage, else the slow-motion step.</summary>
        public float Factor => _frozen || _backstage ? 0f : SlowSteps[_step];

        /// <summary>True when presenter mode leaves time alone.</summary>
        public bool IsDefault => _step == 0 && !_frozen && !_backstage;

        public void SetSlowStep(int step) => _step = Math.Max(0, Math.Min(SlowSteps.Length - 1, step));

        /// <summary>1 -> 0.5 -> 0.25 -> 1. Returns the new speed.</summary>
        public float CycleSlowMotion()
        {
            _step = (_step + 1) % SlowSteps.Length;
            return SlowSteps[_step];
        }

        public void Reset()
        {
            _step = 0;
            _frozen = false;
            _backstage = false;
        }
    }

    /// <summary>The game's time scale from everything that has a say in it.</summary>
    public static class GameTime
    {
        /// <summary>
        /// Playing: hit stop x debug speed x presenter factor. Paused, dialogue and the end screens:
        /// 0 (presenter slow motion never wakes them). Title, credits, boot: 1. A scene load: 0.
        /// </summary>
        public static float Scale(GameState state, bool loading, float hitStopScale, float debugScale, float presenterFactor)
        {
            if (loading) return 0f;
            switch (state)
            {
                case GameState.Playing:
                    return Clamp01(hitStopScale) * Math.Max(0f, debugScale) * Math.Max(0f, presenterFactor);
                case GameState.Paused:
                case GameState.Dialogue:
                case GameState.GameOver:
                case GameState.Victory:
                    return 0f;
                default:
                    return 1f;
            }
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>Who decides whether the player takes damage.</summary>
    public static class Invincibility
    {
        /// <summary>True when damage to this target is blocked: only the player, and only while the
        /// debug god mode or presenter invincibility is on.</summary>
        public static bool Blocks(bool isPlayer, bool debugGodMode, bool presenterInvincible) =>
            isPlayer && (debugGodMode || presenterInvincible);
    }

    /// <summary>A button combination held for a while (the gamepad's presenter chord): fires once per hold.</summary>
    public sealed class HoldChord
    {
        public float holdSeconds = 1f;
        float _held;
        bool _fired;

        public float Held => _held;
        public float Progress01 => holdSeconds > 0f ? Math.Min(1f, _held / holdSeconds) : 1f;

        /// <summary>Call every frame with whether all the buttons are down. True on the frame the hold completes.</summary>
        public bool Tick(bool allDown, float dt)
        {
            if (!allDown)
            {
                _held = 0f;
                _fired = false;
                return false;
            }
            if (dt > 0f) _held += dt;
            if (_fired || _held < holdSeconds) return false;
            _fired = true;
            return true;
        }

        public void Reset()
        {
            _held = 0f;
            _fired = false;
        }
    }
}
