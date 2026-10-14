using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>Controller aim assist for one gun (docs/SKYLINE-DESIGN.md 2.4): the stick turns slower near a target, and a
    /// mild pull toward it while the player is turning or moving. Pad only; the caller decides that.</summary>
    public sealed class AimAssistDef
    {
        /// <summary>Within this angle (degrees) of a target's edge, the stick's turn rate is scaled by <see cref="slowdownScale"/>.</summary>
        public float slowdownConeDeg = 6f;
        public float slowdownScale = 0.55f;
        /// <summary>Within this angle of a target's edge, the view is pulled toward its centre at up to <see cref="magnetDegPerSec"/>.</summary>
        public float magnetConeDeg = 3f;
        public float magnetDegPerSec = 8f;
        /// <summary>Targets farther than this (m) get no assist.</summary>
        public float range = 40f;
        // Hip-fire is assisted less than aiming down sights (the creator, 2026-10-14: "aim assist is too strong when
        // hip-firing"). The numbers above are the ADS numbers (aim 1); at the hip (aim 0) the two cones are scaled by
        // hipConeScale, the magnet's speed by hipMagnetScale, and the slowdown's strength (how far the turn scale is below 1)
        // by hipSlowdownStrength; in between they blend with the eased aim amount.
        public float hipConeScale = 0.5f;
        public float hipMagnetScale = 0.5f;
        public float hipSlowdownStrength = 0.5f;

        static float Blend(float hip, float aim) => hip + (1f - hip) * (aim < 0f ? 0f : aim > 1f ? 1f : aim);

        /// <summary>The slowdown cone (degrees) at an aim amount (0 hip .. 1 aimed).</summary>
        public float SlowdownCone(float aim) => slowdownConeDeg * Blend(hipConeScale, aim);

        /// <summary>The magnet cone (degrees) at an aim amount.</summary>
        public float MagnetCone(float aim) => magnetConeDeg * Blend(hipConeScale, aim);

        /// <summary>The magnet's speed (degrees per second) at an aim amount.</summary>
        public float MagnetSpeed(float aim) => magnetDegPerSec * Blend(hipMagnetScale, aim);

        /// <summary>The stick's turn scale inside the slowdown cone at an aim amount (1 = no slowdown).</summary>
        public float SlowdownTurnScale(float aim) => 1f - (1f - slowdownScale) * Blend(hipSlowdownStrength, aim);
    }

    /// <summary>Something aim assist can settle on: a bot's centre and radius (m). The caller passes only targets in line of sight.</summary>
    public struct AimTarget
    {
        public Vector3 center;
        public float radius;

        public AimTarget(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }

    public struct AimAssistResult
    {
        /// <summary>Multiply the stick's turn rate by this (1 = no assist).</summary>
        public float turnScale;
        /// <summary>Degrees to add to the view this frame (yaw right, pitch down: Unity's convention).</summary>
        public float yawNudge, pitchNudge;
        /// <summary>Index of the target assisted, or -1.</summary>
        public int target;
    }

    public static class AimAssist
    {
        const float Rad2Deg = 180f / MathF.PI;

        /// <summary>The signed smallest difference b - a in degrees, in (-180, 180].</summary>
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f) d -= 360f;
            else if (d <= -180f) d += 360f;
            return d;
        }

        /// <summary>The yaw (0 along +Z, 90 along +X) and pitch (positive looks down) from <paramref name="eye"/> to <paramref name="point"/>.</summary>
        public static void Angles(Vector3 eye, Vector3 point, out float yaw, out float pitch)
        {
            Vector3 d = point - eye;
            float flat = MathF.Sqrt(d.X * d.X + d.Z * d.Z);
            yaw = MathF.Atan2(d.X, d.Z) * Rad2Deg;
            pitch = MathF.Atan2(-d.Y, flat) * Rad2Deg;
        }

        /// <summary>The assist for this frame. Picks the target whose edge is closest in angle to the crosshair (within
        /// range, in front), slows the stick inside <see cref="AimAssistDef.slowdownConeDeg"/>, and while
        /// <paramref name="playerTurningOrMoving"/> nudges the view toward the target's centre inside
        /// <see cref="AimAssistDef.magnetConeDeg"/>, never past it.</summary>
        public static AimAssistResult Compute(Vector3 eye, float yaw, float pitch, IReadOnlyList<AimTarget> targets,
            bool playerTurningOrMoving, float dt, AimAssistDef d) =>
            Compute(eye, yaw, pitch, targets, playerTurningOrMoving, dt, d, 1f);

        /// <summary>As above for an aim amount: 0 hip-fire (the gun's reduced hip cones, magnet and slowdown), 1 aimed down
        /// sights (the gun's full numbers), blending in between.</summary>
        public static AimAssistResult Compute(Vector3 eye, float yaw, float pitch, IReadOnlyList<AimTarget> targets,
            bool playerTurningOrMoving, float dt, AimAssistDef d, float aim)
        {
            var r = new AimAssistResult { turnScale = 1f, target = -1 };
            if (targets == null || d == null) return r;
            float bestEdge = float.MaxValue, bestDy = 0f, bestDp = 0f, bestCentre = 0f;
            int best = -1;
            for (int i = 0; i < targets.Count; i++)
            {
                float dist = Vector3.Distance(eye, targets[i].center);
                if (!(dist > 1e-3f) || dist > d.range) continue;
                Angles(eye, targets[i].center, out float ty, out float tp);
                float dy = DeltaAngle(yaw, ty);
                float dp = tp - pitch;
                float centre = MathF.Sqrt(dy * dy + dp * dp);
                if (!(centre <= 90f)) continue;                                   // beside or behind the view: never
                float half = MathF.Atan(MathF.Max(0f, targets[i].radius) / dist) * Rad2Deg;
                float edge = MathF.Max(0f, centre - half);
                if (edge < bestEdge)
                {
                    bestEdge = edge;
                    bestDy = dy;
                    bestDp = dp;
                    bestCentre = centre;
                    best = i;
                }
            }
            float slowCone = d.SlowdownCone(aim), magnetCone = d.MagnetCone(aim);
            if (best < 0 || (bestEdge > slowCone && bestEdge > magnetCone)) return r;
            r.target = best;
            if (bestEdge <= slowCone) r.turnScale = d.SlowdownTurnScale(aim);
            if (playerTurningOrMoving && bestEdge <= magnetCone && bestCentre > 1e-4f && dt > 0f)
            {
                float step = MathF.Min(d.MagnetSpeed(aim) * dt, bestCentre);
                r.yawNudge = bestDy / bestCentre * step;
                r.pitchNudge = bestDp / bestCentre * step;
            }
            return r;
        }
    }

    /// <summary>A gun's camera kick (2.3: "camera kick that recovers").</summary>
    public sealed class CameraKickDef
    {
        /// <summary>Degrees the view kicks up per shot.</summary>
        public float pitchDeg = 1.5f;
        /// <summary>Most degrees of sideways jitter per shot.</summary>
        public float yawDeg = 0.4f;
        /// <summary>How fast it recovers (1/s, exponential): about 95% back after 3 / this seconds.</summary>
        public float recoverRate = 9f;
    }

    /// <summary>The view's kick offset in degrees, added on top of the player's aim and never written into it, so the aim
    /// recovers by itself. Negative pitch is up (Unity's convention).</summary>
    public sealed class CameraKick
    {
        float _rate = 9f;

        public float Pitch { get; private set; }
        public float Yaw { get; private set; }

        /// <summary>One shot's kick. <paramref name="side"/> in -1..1 (from the caller's seeded random) picks the jitter.</summary>
        public void Kick(CameraKickDef d, float side, float scale = 1f)
        {
            if (d == null || !(scale > 0f)) return;
            if (float.IsNaN(side)) side = 0f;
            side = Math.Max(-1f, Math.Min(1f, side));
            Pitch -= d.pitchDeg * scale;
            Yaw += d.yawDeg * side * scale;
            _rate = Math.Max(0.1f, d.recoverRate);
        }

        public void Tick(float dt)
        {
            if (!(dt > 0f)) return;
            float k = MathF.Exp(-_rate * dt);
            Pitch *= k;
            Yaw *= k;
        }

        public void Clear()
        {
            Pitch = 0f;
            Yaw = 0f;
        }
    }
}
