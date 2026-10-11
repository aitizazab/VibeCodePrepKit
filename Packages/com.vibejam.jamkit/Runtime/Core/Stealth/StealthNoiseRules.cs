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
        {
            var best = default(StealthNoise);
            if (events.landed && events.landImpact >= def.hardLandingSpeed)
            {
                float loud = System.Math.Min(1.6f, events.landImpact / def.hardLandingSpeed);
                Louder(ref best, StealthNoise.Make(feet, def.landingNoiseRadius * loud, NoiseKind.Landing));
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

        static void Louder(ref StealthNoise best, StealthNoise n)
        {
            if (n.radius > best.radius) best = n;
        }
    }
}
