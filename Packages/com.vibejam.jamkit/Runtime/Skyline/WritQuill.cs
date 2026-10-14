using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The back of the Writ: an inkwell with a quill (the creator's pick, 2026-10-14, "A take 2: inkwell and quill";
    /// docs/SKYLINE-GUN-WRIT.md). Pure maths, so it is tested without a scene; <c>SkylineWritFx</c> applies it to the model's
    /// <c>Quill</c> and <c>InkLevel</c> objects. Positions are in the gun's frame (metres; x right, y up, z forward).
    /// <list type="bullet">
    /// <item>The ink is the ammo: the level follows the magazine's fraction (<see cref="InkTarget"/>, <see cref="InkScale"/>),
    /// smoothly (<see cref="InkStep"/>), with a small slosh (<see cref="InkSlosh"/>). In a reload the old drum going out drains
    /// it and the "seat" event refills it.</item>
    /// <item>The quill leans back and out at the hip, away from the sight line, lies flat in ADS, flicks on every shot
    /// (<see cref="Flick"/>), does a double flick on a spot hit (<see cref="Flourish"/>) and dips into the well and stirs once
    /// as the drum drops in a reload (<see cref="Stir"/>). <see cref="Pose"/> combines them into the quill's direction.</item>
    /// </list>
    /// The model constants mirror tools/skyline/guns/writ/model/build_writ.py (QUILL_PIVOT, QUILL_HIP_DIR, FEATHER_LEN):
    /// change them together.
    /// </summary>
    public static class WritQuill
    {
        // ---- the model (build_writ.py)
        /// <summary>The quill's pivot: the nib, at the inkwell's mouth.</summary>
        public static readonly Vector3 Pivot = new Vector3(0.012f, 0.002f, -0.100f);
        /// <summary>The feather's length from the pivot to its tip, and the radius of the vane about the shaft (m).</summary>
        public const float FeatherLength = 0.0805f, FeatherRadius = 0.0075f;
        /// <summary>The model's own pose: the quill's axis (pivot to tip) leaning back toward the player and out to the right.</summary>
        public static readonly Vector3 HipDirection = Vector3.Normalize(new Vector3(0.42f, 0.78f, -0.46f));
        /// <summary>In ADS the quill lies nearly flat, pointing back at the player and a little out, far below the sight line.</summary>
        public static readonly Vector3 FlatDirection = Vector3.Normalize(new Vector3(0.30f, 0.10f, -0.95f));
        /// <summary>The well's top (the jar's full level) and floor, for the ink (m, gun frame y).</summary>
        public const float WellTopY = 0.0f, WellFloorY = -0.0217f;

        // ---- the ink
        /// <summary>The ink's height scale when the magazine is empty: a thin film, never nothing (the well isn't dry).</summary>
        public const float InkEmpty = 0.06f;
        /// <summary>With the drum out, the ink sinks to this fraction until the new drum seats.</summary>
        public const float InkDrained = 0.1f;
        public const float InkRiseRate = 12f, InkFallRate = 8f, InkSeatRate = 16f;

        /// <summary>Where a reload is, as far as the ink cares.</summary>
        public enum InkReload { None, DrumOut, Seated }

        /// <summary>The reload's stage from the clip's progress (0..1) and its "mag_out" and "seat" event fractions.</summary>
        public static InkReload ReloadStage(float progress, float magOutAt, float seatAt)
        {
            if (progress >= seatAt) return InkReload.Seated;
            if (progress >= magOutAt) return InkReload.DrumOut;
            return InkReload.None;
        }

        /// <summary>The ink fraction (0..1) the level heads for: the magazine's fraction; sunk while the drum is out; full once
        /// the new drum has seated.</summary>
        public static float InkTarget(int ammo, int magazine, InkReload stage)
        {
            float f = magazine > 0 ? Clamp01((float)ammo / magazine) : 1f;
            if (stage == InkReload.Seated) return 1f;
            if (stage == InkReload.DrumOut) return Math.Min(f, InkDrained);
            return f;
        }

        /// <summary>The InkLevel object's height scale for an ink fraction: <see cref="InkEmpty"/> (empty) to 1 (full).</summary>
        public static float InkScale(float fraction) => InkEmpty + (1f - InkEmpty) * Clamp01(fraction);

        /// <summary>One frame of the smoothing from <paramref name="current"/> toward <paramref name="target"/>: exponential, quick
        /// to rise, a little slower to fall, quickest on the seat's refill. Never overshoots.</summary>
        public static float InkStep(float current, float target, float dt, bool seated = false)
        {
            if (!(dt > 0f)) return current;
            float rate = target > current ? (seated ? InkSeatRate : InkRiseRate) : InkFallRate;
            float k = 1f - (float)Math.Exp(-rate * dt);
            return current + (target - current) * k;
        }

        /// <summary>The ink's surface bobbing on a spring: kicked by a shot or the gun's movement, it rings and settles.</summary>
        public sealed class InkSlosh
        {
            public const float Stiffness = 180f, Damping = 9f, Limit = 0.12f;
            float _pos, _vel;

            /// <summary>The surface's offset as a fraction of the well's height (±<see cref="Limit"/>).</summary>
            public float Offset => _pos;

            /// <summary>An impulse: positive pushes the surface up first.</summary>
            public void Kick(float velocity) => _vel += velocity;

            public void Step(float dt)
            {
                if (!(dt > 0f)) return;
                dt = Math.Min(dt, 0.033f);                 // a long frame can't blow the spring up
                _vel += (-Stiffness * _pos - Damping * _vel) * dt;
                _pos += _vel * dt;
                if (_pos > Limit) { _pos = Limit; if (_vel > 0f) _vel = 0f; }
                else if (_pos < -Limit) { _pos = -Limit; if (_vel < 0f) _vel = 0f; }
            }

            public void Reset() { _pos = 0f; _vel = 0f; }
        }

        /// <summary>The height scale to draw: the smoothed fraction plus the slosh, kept inside the jar.</summary>
        public static float InkDrawScale(float smoothedFraction, float slosh) =>
            Math.Max(0.03f, Math.Min(1.02f, InkScale(smoothedFraction) + slosh));

        // ---- the flicks
        /// <summary>A shot's flick lasts this long: out fast, then settling back (0 outside it).</summary>
        public const float FlickSeconds = 0.12f, FlickRise = 0.04f;
        /// <summary>A spot hit's flourish: two flicks, under 0.3 s in all.</summary>
        public const float FlourishSeconds = 0.26f, FlourishGap = 0.13f;
        /// <summary>How far a full flick swings the quill forward at the hip (degrees); in ADS this is cut by <see cref="FlickAdsCut"/>.</summary>
        public const float FlickDegrees = 30f, FlickAdsCut = 0.7f;

        /// <summary>The flick's amount (0..1) at <paramref name="age"/> seconds after a shot: 0 before and after, up to 1 in
        /// <see cref="FlickRise"/> (fast, slowing), then eased back to 0 by <see cref="FlickSeconds"/>.</summary>
        public static float Flick(float age)
        {
            if (!(age > 0f) || age >= FlickSeconds) return 0f;
            if (age < FlickRise) { float u = age / FlickRise; return 1f - (1f - u) * (1f - u); }
            float v = (age - FlickRise) / (FlickSeconds - FlickRise);
            return 1f - v * v * (3f - 2f * v);
        }

        /// <summary>The weak-spot flourish: a quick double flick (the second a touch bigger) within <see cref="FlourishSeconds"/>.</summary>
        public static float Flourish(float age)
        {
            if (!(age > 0f) || age >= FlourishSeconds) return 0f;
            return Math.Max(0.8f * Flick(age), Flick(age - FlourishGap));
        }

        // ---- the reload's dip and stir
        /// <summary>The quill's dip into the well (m along its axis) and the tilt of its stir (degrees from vertical).</summary>
        public const float DipDepth = 0.006f, StirTiltDegrees = 14f;

        /// <summary>The reload's dip and stir from the clip's progress and its "mag_out" and "windup" fractions (the drum drops
        /// from mag_out; the gavel's windup is where the quill must be out of the way): <paramref name="weight"/> is how far the
        /// quill is in the stir (0 hip or ADS pose, 1 upright in the well) and <paramref name="angleDeg"/> its stir's turn,
        /// one full turn in all. Both 0 outside that span.</summary>
        public static void Stir(float progress, float magOutAt, float windupAt, out float weight, out float angleDeg)
        {
            weight = 0f;
            angleDeg = 0f;
            if (!(progress > magOutAt) || !(windupAt > magOutAt) || progress >= windupAt) return;
            float u = (progress - magOutAt) / (windupAt - magOutAt);
            weight = Smooth(u / 0.3f) * (1f - Smooth((u - 0.8f) / 0.2f));
            angleDeg = 360f * Smooth((u - 0.25f) / 0.5f);
        }

        // ---- the pose
        /// <summary>The quill's axis (a unit vector in the gun's frame) and its dip (m along -axis).</summary>
        /// <param name="ads">Aim-down-sights amount (0 hip, 1 aimed), eased.</param>
        /// <param name="flick">The flick (0..1), already the larger of a shot's and a flourish's.</param>
        /// <param name="stirWeight">From <see cref="Stir"/>.</param>
        /// <param name="stirAngleDeg">From <see cref="Stir"/>.</param>
        public static void Pose(float ads, float flick, float stirWeight, float stirAngleDeg, out Vector3 direction, out float dip)
        {
            ads = Clamp01(ads);
            Vector3 d = Vector3.Normalize(Vector3.Lerp(HipDirection, FlatDirection, ads));
            if (stirWeight > 0f)
            {
                float tilt = StirTiltDegrees * (float)Math.PI / 180f, a = stirAngleDeg * (float)Math.PI / 180f;
                var up = new Vector3((float)(Math.Sin(tilt) * Math.Cos(a)), (float)Math.Cos(tilt), (float)(Math.Sin(tilt) * Math.Sin(a)));
                d = Vector3.Normalize(Vector3.Lerp(d, up, Clamp01(stirWeight)));
            }
            if (flick > 0f)
            {
                var axis = Vector3.Cross(d, Vector3.UnitZ);       // swings the quill toward the muzzle's side
                if (axis.LengthSquared() > 1e-8f)
                {
                    float deg = FlickDegrees * Math.Min(1f, flick) * (1f - FlickAdsCut * ads);
                    d = Vector3.Transform(d, Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), deg * (float)Math.PI / 180f));
                }
            }
            direction = Vector3.Normalize(d);
            dip = DipDepth * Clamp01(stirWeight);
        }

        /// <summary>A point on the quill (its axis, or the vane's edge at <paramref name="side"/> in -1..1 about it) at
        /// <paramref name="along"/> (0 pivot, 1 tip) for an axis <paramref name="direction"/> and <paramref name="dip"/>, in the
        /// gun's frame. A conservative envelope: a cylinder of <see cref="FeatherRadius"/> about the axis, with
        /// <paramref name="side"/> offsetting it along <paramref name="sideAxis"/>.</summary>
        public static Vector3 Point(Vector3 direction, float dip, float along, Vector3 sideAxis, float side) =>
            Pivot - direction * dip + direction * (FeatherLength * along) + sideAxis * (FeatherRadius * side);

        static float Smooth(float u) { u = Clamp01(u); return u * u * (3f - 2f * u); }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
