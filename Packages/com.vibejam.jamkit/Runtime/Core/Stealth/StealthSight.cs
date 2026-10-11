using System;
using System.Numerics;
using static JamKit.Stealth.StealthMath;

namespace JamKit.Stealth
{
    /// <summary>
    /// How visible a target is to one pair of eyes, 0 (unseen) to about 1.3 (sprinting in the open, close): the vision
    /// cone, the peripheral band and the close sense; line of sight to three points of the body (low cover hides a
    /// crouched body, shows a standing one's head); distance; posture (crouched, sliding, fast, still); light; and a
    /// hiding spot. Pure maths, no allocation. The detection meter fills at <see cref="StealthDef.fillRate"/> times this.
    /// </summary>
    public static class StealthSight
    {
        public static float Visibility(StealthDef def, IStealthWorld world, Vector3 eye, float yawDegrees, in StealthTarget target,
            HideKind? hidingKind, out SightZone zone)
        {
            zone = SightZone.None;
            if (!target.present) return 0f;
            Vector3 chest = target.feet + new Vector3(0f, target.height * def.sampleMid, 0f);
            float dist = FlatDistance(eye, chest);
            if (dist > def.sightRange) return 0f;

            // Cone, periphery, or the close sense.
            float zoneScale;
            Vector3 flat = new Vector3(chest.X - eye.X, 0f, chest.Z - eye.Z);
            float angle = dist > 1e-4f ? Math.Abs(DeltaAngle(yawDegrees, YawOf(flat))) : 0f;
            if (angle <= def.coneHalfAngle) { zone = SightZone.Cone; zoneScale = 1f; }
            else if (angle <= def.peripheralHalfAngle) { zone = SightZone.Peripheral; zoneScale = def.peripheralScale; }
            else if (dist <= def.closeRange) { zone = SightZone.Close; zoneScale = 1f; }
            else return 0f;

            // A hiding spot.
            float hideScale = 1f;
            if (hidingKind.HasValue)
            {
                if (hidingKind.Value == HideKind.Bush) hideScale = dist <= def.bushRevealRange ? 1f : def.bushScale;
                else { zone = SightZone.None; return 0f; }
            }

            // Line of sight to three points on the body.
            float h = Math.Max(0.3f, target.height);
            int clear = 0;
            if (world == null || world.LineClear(eye, target.feet + new Vector3(0f, h * def.sampleLow, 0f))) clear++;
            if (world == null || world.LineClear(eye, chest)) clear++;
            if (world == null || world.LineClear(eye, target.feet + new Vector3(0f, h * def.sampleHigh, 0f))) clear++;
            if (clear == 0) { zone = SightZone.None; return 0f; }
            float losScale = clear / 3f;

            float distScale = dist <= def.nearRange ? 1f : Lerp(1f, def.farScale, (dist - def.nearRange) / Math.Max(0.01f, def.sightRange - def.nearRange));

            float posture = 1f;
            if (target.sliding) posture = def.slideScale;
            else if (target.crouched) posture = def.crouchScale;
            if (target.speed >= def.fastSpeed) posture *= def.fastScale;
            else if (target.speed <= def.stillSpeed) posture *= def.stillScale;

            float light = world != null ? Clamp01(world.LightAt(chest)) : 1f;
            float lightScale = Lerp(def.shadowScale, 1f, light);

            return zoneScale * losScale * distScale * posture * lightScale * hideScale;
        }
    }

    /// <summary>
    /// The detection meter: fills over time while the seeker sees you (never instantly), holds a moment after it loses
    /// you, then drains. 0..1; full means found.
    /// </summary>
    public sealed class DetectionMeter
    {
        float _value, _unseen = 999f;

        public float Value => _value;
        public bool Full => _value >= 1f;
        /// <summary>Seconds since the meter last gained.</summary>
        public float Unseen => _unseen;

        /// <summary>Gains <paramref name="ratePerSecond"/> × dt while it's above zero; otherwise drains after the delay.</summary>
        public void Tick(StealthDef def, float ratePerSecond, float dt)
        {
            if (ratePerSecond > 0f)
            {
                _value = Math.Min(1f, _value + ratePerSecond * dt);
                _unseen = 0f;
                return;
            }
            _unseen += dt;
            if (_unseen > def.drainDelay) _value = Math.Max(0f, _value - def.drainRate * dt);
        }

        /// <summary>Raises the meter to at least <paramref name="level"/> (a noise, a scripted alarm).</summary>
        public void Bump(float level) { if (_value < level) _value = Math.Min(1f, level); }

        public void Set(float value) { _value = Clamp01(value); if (_value > 0f) _unseen = 0f; }

        public void Reset() { _value = 0f; _unseen = 999f; }
    }
}
