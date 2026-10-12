using System;
using System.Numerics;
using static JamKit.Stealth.StealthMath;

namespace JamKit.Stealth
{
    /// <summary>
    /// How visible a target is to one pair of eyes, 0 (unseen) to about 1.3 (sprinting in the open, close): the vision
    /// cone, the peripheral band and the close sense; line of sight to three points of the body (low cover hides a
    /// crouched body, shows a standing one's head; a peeking head is looked for where it really is, and alone it's a small
    /// silhouette); distance; posture (crouched, sliding, fast, still); light; and a
    /// hiding spot. Pure maths, no allocation. The detection meter fills at <see cref="StealthDef.fillRate"/> times this.
    /// </summary>
    public static class StealthSight
    {
        public static float Visibility(StealthDef def, IStealthWorld world, Vector3 eye, float yawDegrees, in StealthTarget target,
            HideKind? hidingKind, out SightZone zone)
            => Visibility(def, world, eye, yawDegrees, target, hidingKind, out zone, 1f, 1f);

        /// <summary>The same with the cone (and periphery) widened by <paramref name="coneScale"/> (a wary seeker) and
        /// the sight range stretched by <paramref name="rangeScale"/> (the dream noticing a fast target).</summary>
        public static float Visibility(StealthDef def, IStealthWorld world, Vector3 eye, float yawDegrees, in StealthTarget target,
            HideKind? hidingKind, out SightZone zone, float coneScale, float rangeScale)
        {
            zone = SightZone.None;
            if (!target.present) return 0f;
            Vector3 chest = target.feet + new Vector3(0f, target.height * def.sampleMid, 0f);
            float dist = FlatDistance(eye, chest);
            float range = def.sightRange * Math.Max(0.1f, rangeScale);
            if (dist > range) return 0f;

            // Cone, periphery, or the close sense.
            float zoneScale;
            Vector3 flat = new Vector3(chest.X - eye.X, 0f, chest.Z - eye.Z);
            float angle = dist > 1e-4f ? Math.Abs(DeltaAngle(yawDegrees, YawOf(flat))) : 0f;
            // Above or below its vertical limit only the close sense works.
            bool vertical = def.verticalHalfAngle >= 90f
                || Math.Abs(Math.Atan2(chest.Y - eye.Y, Math.Max(dist, 1e-3f)) * 180.0 / Math.PI) <= def.verticalHalfAngle;
            if (!vertical) { if (dist > def.closeRange) return 0f; zone = SightZone.Close; zoneScale = 1f; }
            else if (angle <= Math.Min(170f, def.coneHalfAngle * coneScale)) { zone = SightZone.Cone; zoneScale = 1f; }
            else if (angle <= Math.Min(178f, def.peripheralHalfAngle * coneScale)) { zone = SightZone.Peripheral; zoneScale = def.peripheralScale; }
            else if (dist <= def.closeRange) { zone = SightZone.Close; zoneScale = 1f; }
            else return 0f;

            // A hiding spot.
            float hideScale = 1f;
            if (hidingKind.HasValue)
            {
                if (hidingKind.Value == HideKind.Bush) hideScale = dist <= def.bushRevealRange ? 1f : def.bushScale;
                else { zone = SightZone.None; return 0f; }
            }

            // Line of sight to three points on the body. Peeking, the head is where the eyes really are (the chest part of
            // the way there), and a head alone round a corner is a small silhouette.
            float h = Math.Max(0.3f, target.height);
            Vector3 lean = new Vector3(target.lean.X, 0f, target.lean.Z);
            bool peeking = lean.LengthSquared() >= def.peekMinLean * def.peekMinLean;
            bool low = world == null || world.LineClear(eye, target.feet + new Vector3(0f, h * def.sampleLow, 0f));
            bool mid = world == null || world.LineClear(eye, peeking ? chest + lean * def.peekChestShare : chest);
            bool high = world == null || world.LineClear(eye, target.feet + (peeking ? lean : Vector3.Zero) + new Vector3(0f, h * def.sampleHigh, 0f));
            int clear = (low ? 1 : 0) + (mid ? 1 : 0) + (high ? 1 : 0);
            if (clear == 0) { zone = SightZone.None; return 0f; }
            float losScale = peeking && clear == 1 && high ? def.peekExposure : clear / 3f;

            float distScale = dist <= def.nearRange ? 1f : Lerp(1f, def.farScale, (dist - def.nearRange) / Math.Max(0.01f, range - def.nearRange));

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
        public void Bump(float level) { if (_value < level) { _value = Math.Min(1f, level); _unseen = 0f; } }

        public void Set(float value) { _value = Clamp01(value); if (_value > 0f) _unseen = 0f; }

        public void Reset() { _value = 0f; _unseen = 999f; }
    }
}
