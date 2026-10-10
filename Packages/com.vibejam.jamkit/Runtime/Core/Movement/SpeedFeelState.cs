using System;

namespace JamKit.Movement
{
    /// <summary>
    /// The numbers of the speed VISUALS (docs/SPEED-FEEL.md "Speed feel (built)"): the boom when you cross into
    /// flying (V1), the world-space streaks (V2), the footfall puffs and footprints (V5), the edge zoom blur (V6) and
    /// the dream answering (V7). They all follow the shared <see cref="SpeedTier"/>; every one is multiplied by the
    /// Comfort screen's Motion effects (0..1). "Reduce effects" turns the edge blur and the chromatic pulse off and
    /// keeps the streaks and the speed lines subtle (<see cref="reduceShare"/>). Lives in the movement tuning asset.
    /// </summary>
    [Serializable]
    public class SpeedFeelDef
    {
        // ---- V1: the boom, on the crossing into flying ----
        /// <summary>Extra vertical field of view at the crossing, degrees, on top of the speed FOV (x Motion effects).</summary>
        public float boomFov = 6f;
        /// <summary>Seconds for the FOV punch to reach its peak (a snap, not a zoom).</summary>
        public float boomRise = 0.05f;
        /// <summary>Seconds for the punch to settle back to the speed FOV (eased out).</summary>
        public float boomSettle = 0.4f;
        /// <summary>Seconds the shockwave ring takes to ripple from the screen centre to past the edges.</summary>
        public float shockTime = 0.45f;
        /// <summary>The ring's push at its start, in screen heights (0.02 = 2% of the screen; x Motion effects). It fades as it grows.</summary>
        public float shockStrength = 0.02f;
        /// <summary>The ring's thickness, in screen heights.</summary>
        public float shockWidth = 0.09f;
        /// <summary>The ring's final radius, in screen heights from the centre (0.9 reaches the corners of 16:9).</summary>
        public float shockReach = 1.0f;
        /// <summary>The chromatic pulse's colour split at the screen edge, in screen heights (x Motion effects). Off with "reduce effects".</summary>
        public float chromaPulse = 0.008f;
        /// <summary>How many rendered frames the chromatic pulse lasts (the brief: one frame).</summary>
        public int chromaFrames = 1;

        // ---- V2: world-space streaks (motes in the lab, confetti and petals in the dream) ----
        /// <summary>Motes spawned per second at the fast tier's full blend.</summary>
        public float streakRateFast = 45f;
        /// <summary>Extra motes per second at the flying tier's full blend (so flying is fast + this).</summary>
        public float streakRateFlying = 95f;
        /// <summary>Stretch at fast and at flying: seconds of camera travel each mote is drawn along (0.04 s at 20 m/s is a 0.8 m streak).</summary>
        public float streakStretchFast = 0.018f, streakStretchFlying = 0.04f;
        /// <summary>Motes spawn where you'll pass them in this many seconds (min..max), so each one goes by within its life.</summary>
        public float streakPassMin = 0.35f, streakPassMax = 1.5f;
        /// <summary>...but never nearer or further ahead than this (m).</summary>
        public float streakAheadMin = 4f, streakAheadMax = 38f;
        /// <summary>No mote spawns closer than this to your line of travel (m), so none flies through your face.</summary>
        public float streakClear = 1.5f;
        /// <summary>The box they spawn in around your line: half-width sideways, how far below and above the eye (m).</summary>
        public float streakSpread = 7f, streakBelow = 1.3f, streakAbove = 4.5f;
        /// <summary>Mote size range (m).</summary>
        public float streakSizeMin = 0.06f, streakSizeMax = 0.13f;
        /// <summary>The clear centre: motes nearer than <see cref="centerClearNear"/> m fade out inside this radius of the screen
        /// (fraction of the screen height from the centre; 0.2 keeps the central ~40% clear).</summary>
        public float centerClear = 0.2f, centerClearNear = 8f;

        // ---- V3: the contrast-proof speed lines (the numbers of when they show are on SpeedLines) ----
        /// <summary>With "reduce effects", the streaks' density and the speed lines' alpha are kept at this share.</summary>
        public float reduceShare = 0.4f;

        // ---- V5: footfall puffs and glowing footprints ----
        /// <summary>Puff particles per footstep at fast and above (x Motion effects, rounded; 0 turns puffs off).</summary>
        public int puffCount = 6;
        /// <summary>How fast the puff spreads (m/s), how long it lasts (s), its starting size (m).</summary>
        public float puffSpeed = 1.1f, puffLife = 0.55f, puffSize = 0.16f;
        /// <summary>Footprints at flying: how long one glows before it's gone (s) and its length (m).</summary>
        public float footprintLife = 1.6f, footprintSize = 0.34f;

        // ---- V6: edge zoom blur, flying only ----
        /// <summary>Strength at full flying, 0..1 (x Motion effects). Off with "reduce effects".</summary>
        public float edgeBlur = 0.6f;
        /// <summary>How far the blur smears along the radius at full strength (fraction of the distance to the centre).</summary>
        public float edgeBlurSpread = 0.07f;
        /// <summary>Sharp inside this radius, full blur from the outer one (fractions of the half-diagonal: the centre stays sharp).</summary>
        public float edgeBlurInner = 0.45f, edgeBlurOuter = 1.0f;

        // ---- V7: the dream answers (the dream escape only; visual only) ----
        /// <summary>At full flying the dream's saturation rises by this share (0.25 = +25%) and its brightness lifts by this much.</summary>
        public float dreamSaturation = 0.25f, dreamBrightness = 0.07f;
        /// <summary>Flowers opening beside the route per second at full flying, how long each stays open (s), its size (m).</summary>
        public float flowerRate = 14f, flowerLife = 2.6f, flowerSize = 0.5f;
        /// <summary>The static's wall is drawn this much further back at full flying (m). Visual only: the pursuer's distance is untouched.</summary>
        public float staticRecede = 3f;
    }

    /// <summary>One frame of speed visuals, from <see cref="SpeedFeelState.Tick"/>: every level is already scaled by
    /// Motion effects and "reduce effects".</summary>
    public struct SpeedFeelLevels
    {
        /// <summary>The tier (<see cref="SpeedTier.Running"/>, Fast, Flying) and its smoothed blends.</summary>
        public int tier;
        public float fast, flying;
        /// <summary>The boom fired this frame (a real crossing into flying).</summary>
        public bool boom;
        /// <summary>V1: degrees to add to the field of view now.</summary>
        public float boomFov;
        /// <summary>V1: the shockwave ring's radius (screen heights) and push (0 when there's no ring).</summary>
        public float shockRadius, shockStrength, shockWidth;
        /// <summary>V1: the chromatic split now (screen heights; 0 except on the pulse frame).</summary>
        public float chroma;
        /// <summary>V2: motes per second, their stretch (seconds of camera travel) and their opacity (0..1).</summary>
        public float streakRate, streakStretch, streakAlpha;
        /// <summary>V3: the share of the speed lines' alpha to keep (Motion effects x reduce).</summary>
        public float linesShare;
        /// <summary>V5: puff particles per footstep (0: none), and whether footprints are laid.</summary>
        public int puffs;
        public bool footprints;
        /// <summary>V6: the edge blur's strength now, 0..1.</summary>
        public float edgeBlur;
        /// <summary>V7: how much the dream answers now, 0..1 (flying blend x Motion effects).</summary>
        public float dream;
    }

    /// <summary>
    /// The speed visuals' state: ticks the shared <see cref="SpeedTier"/> with the horizontal speed once per rendered
    /// frame and turns it into <see cref="SpeedFeelLevels"/>. Pure C#, no allocation per tick. A teleport (respawn,
    /// chapter jump) calls <see cref="Teleported"/> so arriving at speed isn't a crossing.
    /// </summary>
    public sealed class SpeedFeelState
    {
        readonly SpeedTier _tier;
        SpeedFeelDef _def;
        float _boomT = -1f;
        int _chromaLeft;
        int _booms;
        bool _suppress;
        SpeedFeelLevels _levels;

        public SpeedFeelState(SpeedFeelDef def, SpeedTierDef tiers)
        {
            _def = def ?? new SpeedFeelDef();
            _tier = new SpeedTier(tiers);
        }

        public SpeedTier Tier => _tier;
        public SpeedFeelDef Def => _def;
        /// <summary>Swap the numbers (a tuning asset edited in play mode).</summary>
        public void SetDef(SpeedFeelDef def) => _def = def ?? new SpeedFeelDef();
        /// <summary>How many booms have fired (real crossings into flying).</summary>
        public int Booms => _booms;
        public SpeedFeelLevels Levels => _levels;
        /// <summary>Seconds since the last boom (-1 when none is playing).</summary>
        public float BoomTime => _boomT;

        public void Reset()
        {
            _tier.Reset();
            _boomT = -1f;
            _chromaLeft = 0;
            _levels = default;
        }

        /// <summary>The player was put somewhere new: the next tick's crossing (arriving at speed) doesn't boom.</summary>
        public void Teleported() => _suppress = true;

        /// <param name="horizontalSpeed">m/s.</param>
        /// <param name="dt">Game seconds since the last tick (0 while paused).</param>
        /// <param name="motion">The Comfort screen's Motion effects, 0..1.</param>
        /// <param name="reduce">"Reduce effects" is on.</param>
        public SpeedFeelLevels Tick(float horizontalSpeed, float dt, float motion, bool reduce)
        {
            var d = _def;
            motion = Clamp01(motion);
            var ev = _tier.Tick(horizontalSpeed, dt);
            bool boom = ev.enteredFlying && !_suppress;
            _suppress = false;
            if (boom)
            {
                _booms++;
                _boomT = 0f;
                _chromaLeft = Math.Max(0, d.chromaFrames);
            }
            else if (_boomT >= 0f)
            {
                _boomT += dt;
                if (_boomT > Math.Max(d.boomRise + d.boomSettle, d.shockTime)) _boomT = -1f;
            }

            var l = new SpeedFeelLevels
            {
                tier = _tier.Tier,
                fast = _tier.FastBlend,
                flying = _tier.FlyingBlend,
                boom = boom,
            };
            float share = reduce ? Clamp01(d.reduceShare) : 1f;

            // V1: the FOV punch (snap up, ease back), the ring, the chromatic pulse.
            if (_boomT >= 0f)
            {
                l.boomFov = d.boomFov * BoomPunch(_boomT, d.boomRise, d.boomSettle) * motion;
                if (_boomT < d.shockTime && d.shockTime > 0f)
                {
                    float k = _boomT / d.shockTime;
                    float eased = 1f - (1f - k) * (1f - k); // fast out, slowing down
                    l.shockRadius = eased * d.shockReach;
                    l.shockStrength = d.shockStrength * (1f - k) * motion;
                    l.shockWidth = d.shockWidth;
                }
            }
            if (_chromaLeft > 0)
            {
                if (!reduce) l.chroma = d.chromaPulse * motion;
                _chromaLeft--;
            }

            // V2 and V3.
            float smoothFast = SmoothStep01(l.fast), smoothFly = SmoothStep01(l.flying);
            l.streakRate = (d.streakRateFast * smoothFast + d.streakRateFlying * smoothFly) * motion * share;
            l.streakStretch = (d.streakStretchFast * smoothFast + (d.streakStretchFlying - d.streakStretchFast) * smoothFly) * motion;
            l.streakAlpha = Clamp01(smoothFast) * (reduce ? 0.7f : 1f);
            l.linesShare = motion * share;

            // V5: puffs from fast, footprints at flying.
            l.puffs = _tier.Tier >= SpeedTier.Fast ? (int)Math.Round(d.puffCount * motion) : 0;
            l.footprints = _tier.Tier >= SpeedTier.Flying && motion > 0.01f;

            // V6: flying only, never with "reduce effects".
            l.edgeBlur = reduce ? 0f : Clamp01(d.edgeBlur) * smoothFly * motion;

            // V7.
            l.dream = smoothFly * motion;

            _levels = l;
            return l;
        }

        /// <summary>The boom's FOV shape over time: 0 -> 1 in <paramref name="rise"/> s, then eased back to 0 over <paramref name="settle"/> s.</summary>
        public static float BoomPunch(float t, float rise, float settle)
        {
            if (t < 0f) return 0f;
            if (t < rise) return rise > 0f ? t / rise : 1f;
            float k = settle > 0f ? (t - rise) / settle : 1f;
            if (k >= 1f) return 0f;
            return 1f - SmoothStep01(k);
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float SmoothStep01(float x)
        {
            x = Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
