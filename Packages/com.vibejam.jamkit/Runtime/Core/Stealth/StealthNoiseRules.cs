using System.Numerics;
using JamKit.Movement;

namespace JamKit.Stealth
{
    /// <summary>
    /// Noise from the movement kit: a hard landing (downward speed at least <see cref="StealthDef.hardLandingSpeed"/>,
    /// louder the harder), sprint footsteps (every <see cref="StealthDef.footstepEvery"/> s), wallrun steps, a slide's
    /// start. Walking, crouching and soft landings are silent. One call per frame with that frame's movement state and
    /// events; it returns the loudest noise of the frame (or none). Keep one per player.
    /// </summary>
    public sealed class StealthNoiseRules
    {
        float _footT;

        public StealthNoise Tick(StealthDef def, MoveMode mode, in MoveEvents events, Vector3 feet, float dt)
            => Tick(def, mode, events, feet, dt, 0f, false, false);

        /// <summary>The same, with answer A's loud movement when <paramref name="loud"/> (docs/STEALTH.md): walking makes a
        /// small noise each step (crouched: none), sprinting louder with <paramref name="speed"/>, a double jump and every
        /// wallrun step well beyond sight range, a slide's start and hard landings louder.</summary>
        public StealthNoise Tick(StealthDef def, MoveMode mode, in MoveEvents events, Vector3 feet, float dt, float speed, bool crouched, bool loud)
        {
            if (loud) return Loud(def, mode, events, feet, dt, speed, crouched);
            var best = default(StealthNoise);
            if (events.landed && events.landImpact >= def.hardLandingSpeed)
            {
                float hard = System.Math.Min(1.6f, events.landImpact / def.hardLandingSpeed);
                Louder(ref best, StealthNoise.Make(feet, def.landingNoiseRadius * hard, NoiseKind.Landing));
            }
            if (events.slideStarted) Louder(ref best, StealthNoise.Make(feet, def.slideNoiseRadius, NoiseKind.Slide));

            float stepRadius = mode == MoveMode.Sprint ? def.sprintNoiseRadius : mode == MoveMode.Wallrun ? def.wallrunNoiseRadius : 0f;
            if (stepRadius > 0f)
            {
                _footT -= dt;
                if (_footT <= 0f)
                {
                    _footT = def.footstepEvery;
                    Louder(ref best, StealthNoise.Make(feet, stepRadius, mode == MoveMode.Wallrun ? NoiseKind.Wallrun : NoiseKind.Footsteps));
                }
            }
            else _footT = 0f; // the first sprint step is heard at once
            return best;
        }

        StealthNoise Loud(StealthDef def, MoveMode mode, in MoveEvents events, Vector3 feet, float dt, float speed, bool crouched)
        {
            var best = default(StealthNoise);
            if (events.landed && events.landImpact >= def.hardLandingSpeed)
            {
                float loud = System.Math.Min(1.8f, events.landImpact / def.hardLandingSpeed);
                Louder(ref best, StealthNoise.Make(feet, def.landingNoiseRadius * def.loudLandingScale * loud, NoiseKind.Landing));
            }
            if (events.slideStarted) Louder(ref best, StealthNoise.Make(feet, def.loudSlideRadius, NoiseKind.Slide));
            if (events.doubleJumped) Louder(ref best, StealthNoise.Make(feet, def.loudDoubleJumpRadius, NoiseKind.Other));
            if (events.wallJumped) Louder(ref best, StealthNoise.Make(feet, def.loudWallrunRadius, NoiseKind.Wallrun));

            float step = 0f, every = def.footstepEvery;
            var kind = NoiseKind.Footsteps;
            if (mode == MoveMode.Wallrun) { step = def.loudWallrunRadius; kind = NoiseKind.Wallrun; }
            else if (mode == MoveMode.Sprint) step = def.loudSprintBase + def.loudSprintPerSpeed * speed;
            else if (mode == MoveMode.Grounded && !crouched && speed >= 1f) { step = def.loudWalkRadius; every = def.footstepEvery * 1.6f; }
            if (step > 0f)
            {
                _footT -= dt;
                if (_footT <= 0f)
                {
                    _footT = every;
                    Louder(ref best, StealthNoise.Make(feet, step, kind));
                }
            }
            else _footT = 0f;
            return best;
        }

        static void Louder(ref StealthNoise best, StealthNoise n)
        {
            if (n.radius > best.radius) best = n;
        }
    }
}
