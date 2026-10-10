using System;

namespace JamKit.Movement
{
    /// <summary>Thresholds for the speed tiers (docs/SPEED-FEEL.md, RESEARCH-MOVEMENT 8.4): running, fast, flying.
    /// Shared by everything that reacts to speed (music stems, wind, the boom, streaks, the dream answering).</summary>
    [Serializable]
    public class SpeedTierDef
    {
        /// <summary>Enter "fast" at this horizontal speed (m/s).</summary>
        public float fastEnter = 8f;
        /// <summary>Enter "flying" at this horizontal speed (m/s): the crossing moment (the boom).</summary>
        public float flyingEnter = 13f;
        /// <summary>Hysteresis: drop out of a tier only this far below its entry speed, so hovering on the line doesn't flicker.</summary>
        public float exitMargin = 1.5f;
        /// <summary>...and only after being below that for this long, so a stumble or a mantle doesn't drop the tier (seconds).</summary>
        public float dropDelay = 0.6f;
        /// <summary>Smoothed blends rise this fast and fall this fast (per second): quick in, slow out.</summary>
        public float blendRise = 4f, blendFall = 0.8f;
    }

    /// <summary>What changed in one <see cref="SpeedTier.Tick"/>.</summary>
    public struct SpeedTierEvents
    {
        /// <summary>The tier went up this tick (to <see cref="SpeedTier.Tier"/>), or down.</summary>
        public bool up, down;
        /// <summary>Crossed into flying this tick: the boom.</summary>
        public bool enteredFlying;
    }

    /// <summary>The current speed tier with hysteresis and a drop delay, plus smoothed 0..1 blends for continuous
    /// effects. Pure C#; tick it once per frame (or per fixed step) with the horizontal speed.</summary>
    public sealed class SpeedTier
    {
        public const int Running = 0, Fast = 1, Flying = 2;

        readonly SpeedTierDef _def;
        int _tier;
        float _below;
        float _fastBlend, _flyingBlend;

        public SpeedTier(SpeedTierDef def) { _def = def ?? new SpeedTierDef(); }

        public SpeedTierDef Def => _def;
        /// <summary>0 running, 1 fast, 2 flying.</summary>
        public int Tier => _tier;
        /// <summary>Smoothed 0..1: how far into "fast or above" (1 while fast or flying).</summary>
        public float FastBlend => _fastBlend;
        /// <summary>Smoothed 0..1: how far into "flying".</summary>
        public float FlyingBlend => _flyingBlend;

        public void Reset()
        {
            _tier = Running;
            _below = 0f;
            _fastBlend = 0f;
            _flyingBlend = 0f;
        }

        public SpeedTierEvents Tick(float horizontalSpeed, float dt)
        {
            var ev = new SpeedTierEvents();
            int target = horizontalSpeed >= _def.flyingEnter ? Flying : (horizontalSpeed >= _def.fastEnter ? Fast : Running);

            if (target > _tier)
            {
                ev.up = true;
                if (target == Flying) ev.enteredFlying = true;
                _tier = target;
                _below = 0f;
            }
            else if (_tier > Running)
            {
                float exit = (_tier == Flying ? _def.flyingEnter : _def.fastEnter) - _def.exitMargin;
                if (horizontalSpeed < exit)
                {
                    _below += dt;
                    if (_below >= _def.dropDelay)
                    {
                        // Drop one tier per delay, or straight to where the speed is if that's lower still.
                        int floor = horizontalSpeed >= _def.fastEnter - _def.exitMargin ? Fast : Running;
                        _tier = Math.Min(_tier - 1, Math.Max(floor, Running));
                        if (_tier < Running) _tier = Running;
                        _below = 0f;
                        ev.down = true;
                    }
                }
                else _below = 0f;
            }

            _fastBlend = Approach(_fastBlend, _tier >= Fast ? 1f : 0f, dt);
            _flyingBlend = Approach(_flyingBlend, _tier >= Flying ? 1f : 0f, dt);
            return ev;
        }

        float Approach(float v, float target, float dt)
        {
            float rate = target > v ? _def.blendRise : _def.blendFall;
            float step = rate * dt;
            return target > v ? Math.Min(target, v + step) : Math.Max(target, v - step);
        }
    }
}
