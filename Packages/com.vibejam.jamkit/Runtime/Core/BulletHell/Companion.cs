using System;
using System.Collections.Generic;
using JamKit.Patterns;

namespace JamKit.BulletHell
{
    /// <summary>
    /// A companion's numbers in battle (docs/DESIGN-ROGUELITE.md, section 6): it orbits the ship and
    /// shoots (weaker than the ship), takes one bullet meant for the player on a cooldown, and calls out
    /// the boss's next pattern with a short bark. The template's CompanionDef asset holds one of these.
    /// </summary>
    [Serializable]
    public class CompanionRules
    {
        public float orbitRadius = 0.95f;
        /// <summary>Degrees per second around the ship.</summary>
        public float orbitSpeed = 110f;
        public float fireInterval = 0.32f;
        /// <summary>Damage per shot as a fraction of the ship's shot damage (the ship is the main gun).</summary>
        public float damageScale = 0.4f;
        public float shotSpeed = 15f;
        /// <summary>Seconds before the guard is back after it took a bullet (times companion_shield_cooldown).</summary>
        public float guardCooldown = 18f;
        /// <summary>Fewest seconds between two barks.</summary>
        public float barkCooldown = 6f;
        /// <summary>How early the boss's next pattern is called out.</summary>
        public float barkLead = 1.2f;
        /// <summary>Seconds a bark stays on screen.</summary>
        public float barkSeconds = 2.4f;
        /// <summary>When it joins the run: after this many bosses (0 = from the start, 1 = met after the first boss).</summary>
        public int joinAfterBosses;
        /// <summary>Its own health: enemy bullets that touch it cost 1. 0 = it can't be hurt.</summary>
        public float maxHealth;
        /// <summary>Hit radius when it has health.</summary>
        public float hitRadius = 0.22f;
        /// <summary>Seconds it's down (no shots, no guard) before it's back at full health. 0 = it stays down until a Rest.</summary>
        public float reviveSeconds = 12f;

        public void Validate(List<string> errors)
        {
            if (orbitRadius <= 0.2f) errors.Add("Companion: the orbit radius is too small.");
            if (fireInterval <= 0.02f) errors.Add("Companion: the fire interval must be above 0.02 s.");
            if (damageScale < 0f || damageScale >= 1f) errors.Add("Companion: its damage scale must be 0 or more and below 1 (weaker than the ship).");
            if (shotSpeed <= 0f) errors.Add("Companion: shot speed must be positive.");
            if (guardCooldown < 1f) errors.Add("Companion: the guard cooldown must be at least 1 s.");
            if (barkCooldown < 0f || barkLead < 0f || barkSeconds <= 0f) errors.Add("Companion: bark timings must be positive.");
            if (joinAfterBosses < 0) errors.Add("Companion: join after 0 or more bosses.");
            if (maxHealth < 0f || reviveSeconds < 0f || (maxHealth > 0f && hitRadius <= 0f)) errors.Add("Companion: health, hit radius and revive time can't be negative.");
        }
    }

    /// <summary>The companion's moment-to-moment state: orbit, gun clock, guard cooldown and bark timer.</summary>
    public sealed class CompanionState
    {
        CompanionRules _rules;
        float _angle, _fireClock, _guardLeft, _guardMax, _barkLeft, _health, _downLeft;
        int _guards, _knockdowns;
        bool _down;

        public CompanionRules Rules => _rules;
        /// <summary>Degrees around the ship (0 = right).</summary>
        public float AngleDegrees => _angle;
        public bool GuardReady => _guardLeft <= 0f;
        /// <summary>0 right after a guard, 1 when ready again (for the ring on screen).</summary>
        public float GuardFraction => _guardMax > 0f ? 1f - Math.Max(0f, _guardLeft) / _guardMax : 1f;
        public float GuardSecondsLeft => Math.Max(0f, _guardLeft);
        /// <summary>Bullets it took for the player this run.</summary>
        public int Guards => _guards;
        public bool CanBark => _barkLeft <= 0f;
        /// <summary>Its health (only when <see cref="CompanionRules.maxHealth"/> is above 0).</summary>
        public float Health => _health;
        public bool HasHealth => _rules != null && _rules.maxHealth > 0f;
        /// <summary>Knocked down: no shots, no guard, until it revives.</summary>
        public bool Down => _down;
        public int Knockdowns => _knockdowns;
        /// <summary>0 right after it went down, 1 when it's back (for a meter).</summary>
        public float ReviveFraction => !_down || _rules == null || _rules.reviveSeconds <= 0f ? (_down ? 0f : 1f) : 1f - _downLeft / _rules.reviveSeconds;

        /// <summary>An enemy bullet touched it. Returns true when that knocked it down.</summary>
        public bool Hurt(float amount)
        {
            if (!HasHealth || _down || amount <= 0f) return false;
            _health = Math.Max(0f, _health - amount);
            if (_health > 0f) return false;
            _down = true;
            _knockdowns++;
            _downLeft = _rules.reviveSeconds;
            return true;
        }

        /// <summary>Back on its feet at full health (a Rest room, a revive).</summary>
        public void Revive()
        {
            _down = false;
            _downLeft = 0f;
            _health = _rules != null ? _rules.maxHealth : 0f;
        }

        public void Begin(CompanionRules rules)
        {
            _rules = rules ?? new CompanionRules();
            _angle = 90f;
            _fireClock = 0f;
            _guardLeft = 0f;
            _guardMax = _rules.guardCooldown;
            _barkLeft = 0f;
            _guards = 0;
            _knockdowns = 0;
            Revive();
        }

        /// <summary>Where it is, relative to the ship.</summary>
        public System.Numerics.Vector2 Offset
        {
            get
            {
                float r = _rules != null ? _rules.orbitRadius : 1f;
                double a = _angle * Math.PI / 180.0;
                return new System.Numerics.Vector2((float)Math.Cos(a) * r, (float)Math.Sin(a) * r * 0.6f + 0.25f);
            }
        }

        /// <summary>Advances the orbit and timers. Returns the number of shots due this tick (0 when not
        /// <paramref name="shooting"/>).</summary>
        public int Tick(float dt, bool shooting, float cooldownScale)
        {
            if (_rules == null) return 0;
            dt = Math.Max(0f, dt);
            _angle = (_angle + _rules.orbitSpeed * dt) % 360f;
            float max = _rules.guardCooldown * Math.Max(0.05f, cooldownScale);
            if (_guardMax > 0f && max != _guardMax) _guardLeft *= max / _guardMax; // an upgrade mid-wait shortens it
            _guardMax = max;
            if (_guardLeft > 0f) _guardLeft -= dt;
            if (_barkLeft > 0f) _barkLeft -= dt;
            if (_down && _rules.reviveSeconds > 0f)
            {
                _downLeft -= dt;
                if (_downLeft <= 0f) Revive();
            }
            if (!shooting || _down) { _fireClock = 0f; return 0; }
            _fireClock += dt;
            int shots = 0;
            float interval = Math.Max(0.02f, _rules.fireInterval);
            while (_fireClock >= interval && shots < 8)
            {
                _fireClock -= interval;
                shots++;
            }
            return shots;
        }

        /// <summary>A bullet would hit the player: if the guard is ready it takes it (true) and cools down.</summary>
        public bool TryGuard()
        {
            if (_rules == null || !GuardReady || _down) return false;
            _guardLeft = _guardMax;
            _guards++;
            return true;
        }

        /// <summary>Makes the guard wait (tests, a debug button).</summary>
        public void SpendGuard() => _guardLeft = _guardMax;

        /// <summary>A bark was shown: the next one waits for the cooldown.</summary>
        public void Barked() => _barkLeft = _rules != null ? _rules.barkCooldown : 5f;
    }

    /// <summary>
    /// Which pattern step of a boss phase is about to start: the companion calls it out a moment early.
    /// Pure arithmetic on a <see cref="PatternSequence"/>'s step times and its loop.
    /// </summary>
    public static class PatternCallout
    {
        /// <summary>
        /// The index of the step whose first volley falls in (<paramref name="time"/>, time + <paramref name="lead"/>]
        /// (wrapping into the next loop pass), or -1. <paramref name="time"/> is the emitter's time.
        /// </summary>
        public static int Upcoming(PatternSequence seq, float time, float lead)
        {
            if (seq == null || seq.steps == null || seq.steps.Count == 0 || lead <= 0f) return -1;
            float duration = seq.Duration;
            bool loops = seq.loop && !float.IsInfinity(duration);
            float period = loops ? Math.Max(0.001f, duration + Math.Max(0f, seq.loopDelay)) : float.PositiveInfinity;
            int best = -1;
            float bestStart = float.PositiveInfinity;
            for (int i = 0; i < seq.steps.Count; i++)
            {
                var step = seq.steps[i];
                if (step == null || step.pattern == null) continue;
                float start = Math.Max(0f, step.time) + Math.Max(0f, step.pattern.startDelay);
                float next;
                if (loops)
                {
                    // The next time this step starts after `time`.
                    float passes = (float)Math.Floor((time - start) / period) + 1f;
                    next = start + Math.Max(0f, passes) * period;
                    if (next <= time) next += period;
                }
                else next = start > time ? start : float.PositiveInfinity;
                if (next - time <= lead && next < bestStart)
                {
                    best = i;
                    bestStart = next;
                }
            }
            return best;
        }

        /// <summary>A pattern shape as a bark context word: "ring", "spread", "spiral", "aimed", "shotgun", "wall".</summary>
        public static string ShapeWord(PatternShape shape)
        {
            switch (shape)
            {
                case PatternShape.Ring: return "ring";
                case PatternShape.Spread: return "spread";
                case PatternShape.Spiral: return "spiral";
                case PatternShape.Aimed: return "aimed";
                case PatternShape.Shotgun: return "shotgun";
                case PatternShape.Line: return "wall";
                default: return "pattern";
            }
        }
    }
}
