using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The back of the Writ: an inkwell with a quill (the creator's pick, 2026-10-14, "A take 2: inkwell and quill";
    /// docs/SKYLINE-GUN-WRIT.md). Pure maths, so it is tested without a scene; <c>SkylineWritFx</c> applies it to the model's
    /// <c>Quill</c> chain and <c>InkLevel</c> object. Positions are in the gun's frame (metres; x right, y up, z forward).
    /// <list type="bullet">
    /// <item>The ink is the ammo: the level follows the magazine's fraction (<see cref="InkTarget"/>, <see cref="InkScale"/>),
    /// smoothly (<see cref="InkStep"/>), with a small slosh (<see cref="InkSlosh"/>). In a reload the quill leaves the well and
    /// the ink refills behind it while it writes (<see cref="InkReloadTarget"/>).</item>
    /// <item>The quill is a chain of <see cref="Segments"/> segments (the creator: "make the quill larger ... aiming down sights
    /// makes it curl around and extend"). A <see cref="ChainPose"/> is its nib's place, its direction, the way it bends and how
    /// far; <see cref="Joints"/> turns a pose into the segments' places and turns. At the hip it sits off to the right, out of
    /// the crosshair; as the aim comes up it curls round and extends back and out, away from the sight window
    /// (<see cref="Pose"/>); a shot flicks and whips it (<see cref="Flick"/>); a spot hit does a double flick
    /// (<see cref="Flourish"/>).</item>
    /// <item>The reload (the creator: "it leaves the ink vial, and writes on a paper on the table as the ink refills behind
    /// it"): <see cref="Reload"/> lifts the quill out of the well, takes it to the sheet of paper on the table, writes three
    /// strokes (<see cref="StrokePoint"/>; <see cref="ShownSegments"/> of the model's handwriting are visible) and returns
    /// it to the well.</item>
    /// </list>
    /// The model constants mirror tools/skyline/guns/writ/model/build_writ.py (QUILL_PIVOT, SEG_N, SEG_LEN, NIB_BELOW,
    /// QUILL_HIP_*, PAPER_*, STROKE_*): change them together (WritQuillTests reads the script).
    /// </summary>
    public static class WritQuill
    {
        // ---- the model (build_writ.py)
        /// <summary>The quill's pivot: the nib's mount, at the inkwell's mouth.</summary>
        public static readonly Vector3 Pivot = new Vector3(0.012f, 0.002f, -0.100f);
        /// <summary>The chain: segments of <see cref="SegmentLength"/> (0.122 m in all, half as long again as the old single piece).
        /// The nib is <see cref="NibBelow"/> long, below the pivot along the first segment; the vane's widest half-width is
        /// <see cref="FeatherRadius"/> (with the shaft).</summary>
        public const int Segments = 4;
        public const float SegmentLength = 0.0305f, FeatherLength = 0.122f, FeatherRadius = 0.0105f, NibBelow = 0.0105f;
        /// <summary>The model's own pose, the hip: leaning back toward the player and out to the right, away from the sight line,
        /// curling outward by <see cref="HipCurl"/> degrees in all.</summary>
        public static readonly Vector3 HipDirection = Vector3.Normalize(new Vector3(0.30f, 0.82f, -0.48f));
        public static readonly Vector3 HipToward = Vector3.Normalize(new Vector3(0.8f, 0.05f, -0.6f));
        public const float HipCurl = 45f;
        /// <summary>Aimed: the quill reaches out to the right and back past the receiver, curling round behind the sight window's
        /// eye, well below the sight line and out of the picture.</summary>
        public static readonly Vector3 AdsDirection = Vector3.Normalize(new Vector3(0.7f, -0.05f, -0.71f));
        public static readonly Vector3 AdsToward = Vector3.Normalize(new Vector3(0.2f, -0.2f, -0.96f));
        public const float AdsCurl = 15f;
        /// <summary>The well's top (the jar's full level) and floor, for the ink (m, gun frame y).</summary>
        public const float WellTopY = 0.0f, WellFloorY = -0.0217f;

        // ---- the paper and the handwriting (build_writ.py: PAPER_*, STROKE_*)
        public const float PaperX = 0.002f, PaperZ = -0.036f, PaperYawDegrees = 8f, PaperSurface = 0.0671f;
        public const float StrokeU0 = -0.0125f, StrokeU1 = 0.0115f, StrokeAmp = 0.0012f, StrokeWaves = 3.5f;
        public static readonly float[] StrokeV = { 0.0075f, 0f, -0.0075f };
        public const int StrokeCount = 3, InkSegmentsPerStroke = 10, InkSegmentCount = StrokeCount * InkSegmentsPerStroke;
        /// <summary>The ink line's thickness on the paper, and where the nib's tip rests while it writes.</summary>
        public const float InkThickness = 0.0003f;
        public const float NibPaperY = PaperSurface + InkThickness;

        // ---- the ink
        /// <summary>The ink's height scale when the magazine is empty: a thin film, never nothing (the well isn't dry).</summary>
        public const float InkEmpty = 0.06f;
        public const float InkRiseRate = 12f, InkFallRate = 8f;

        /// <summary>The ink fraction (0..1) the level heads for outside a reload: the magazine's fraction.</summary>
        public static float InkTarget(int ammo, int magazine) => magazine > 0 ? Clamp01((float)ammo / magazine) : 1f;

        /// <summary>The ink fraction during a reload (the quill leaves the well at <paramref name="liftAt"/> and returns at
        /// <paramref name="returnAt"/>, fractions of the clip): the magazine's fraction until the quill is on the paper, then
        /// refilling smoothly behind it, full when it returns.</summary>
        public static float InkReloadTarget(float ammoFraction, float progress, float liftAt, float returnAt)
        {
            float f = Clamp01(ammoFraction);
            if (!(progress > liftAt) || !(returnAt > liftAt)) return f;
            float u = (progress - liftAt) / (returnAt - liftAt);
            if (u >= 1f) return 1f;
            return f + (1f - f) * Smooth((u - 0.15f) / 0.8f);
        }

        /// <summary>The InkLevel object's height scale for an ink fraction: <see cref="InkEmpty"/> (empty) to 1 (full).</summary>
        public static float InkScale(float fraction) => InkEmpty + (1f - InkEmpty) * Clamp01(fraction);

        /// <summary>One frame of the smoothing from <paramref name="current"/> toward <paramref name="target"/>: exponential, quick
        /// to rise, a little slower to fall. Never overshoots.</summary>
        public static float InkStep(float current, float target, float dt)
        {
            if (!(dt > 0f)) return current;
            float rate = target > current ? InkRiseRate : InkFallRate;
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
        /// <summary>How far a full flick swings the quill forward at the hip (degrees), and how much more the chain whips
        /// (curl, degrees); both are cut by <see cref="FlickAdsCut"/> in ADS.</summary>
        public const float FlickDegrees = 30f, FlickCurlDegrees = 25f, FlickAdsCut = 0.7f;

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

        // ---- the chain

        /// <summary>The quill's pose: where the chain starts (<see cref="Base"/>, the pivot's place), the direction of its first
        /// segment, the way it bends (the bend axis is the cross of the direction and <see cref="Toward"/>: the tip curls toward
        /// that direction) and the total bend in degrees, spread evenly over the joints: segment i points at the direction turned
        /// by <see cref="Curl"/> * i / (<see cref="Segments"/> - 1).</summary>
        public struct ChainPose
        {
            public Vector3 Base, Direction, Toward;
            public float Curl;

            public ChainPose(Vector3 basePoint, Vector3 direction, Vector3 toward, float curl)
            {
                Base = basePoint;
                Direction = direction;
                Toward = toward;
                Curl = curl;
            }

            /// <summary>The nib's tip: <see cref="NibBelow"/> below the base, along the first segment.</summary>
            public Vector3 NibTip => Base - Vector3.Normalize(Direction) * NibBelow;
        }

        public static ChainPose HipPose => new ChainPose(Pivot, HipDirection, HipToward, HipCurl);
        public static ChainPose AdsPose => new ChainPose(Pivot, AdsDirection, AdsToward, AdsCurl);

        /// <summary>Segment <paramref name="i"/>'s frame for a pose: x the bend axis, y along the segment, z = x cross y.</summary>
        public static void Frame(in ChainPose p, int i, out Vector3 x, out Vector3 y, out Vector3 z)
        {
            Vector3 d = Vector3.Normalize(p.Direction);
            Vector3 axis = Vector3.Cross(d, Vector3.Normalize(p.Toward));
            if (axis.LengthSquared() < 1e-10f) axis = Vector3.Cross(d, Math.Abs(d.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
            axis = Vector3.Normalize(axis);
            float angle = Segments > 1 ? p.Curl * i / (Segments - 1) : 0f;
            y = Vector3.Normalize(Rotate(d, axis, angle));
            x = axis;
            z = Vector3.Cross(x, y);
        }

        /// <summary>The chain's joints (<see cref="Segments"/> + 1 points: joint i is segment i's pivot) and, for each segment,
        /// the turn from its place in the hip pose (the model as imported) to its place in this pose.</summary>
        public static void Joints(in ChainPose p, Vector3[] joints, Quaternion[] turns)
        {
            Vector3 at = p.Base;
            if (joints != null && joints.Length > 0) joints[0] = at;
            for (int i = 0; i < Segments; i++)
            {
                Frame(p, i, out var x, out var y, out var z);
                at += y * SegmentLength;
                if (joints != null && joints.Length > i + 1) joints[i + 1] = at;
                if (turns != null && turns.Length > i)
                {
                    Frame(HipPose, i, out var hx, out var hy, out var hz);
                    turns[i] = TurnBetween(hx, hy, hz, x, y, z);
                }
            }
        }

        /// <summary>The hip pose's joints (where the model's segments are placed).</summary>
        public static Vector3[] HipJoints()
        {
            var j = new Vector3[Segments + 1];
            Joints(HipPose, j, null);
            return j;
        }

        /// <summary>The turn taking the orthonormal frame (hx, hy, hz) onto (px, py, pz).</summary>
        static Quaternion TurnBetween(Vector3 hx, Vector3 hy, Vector3 hz, Vector3 px, Vector3 py, Vector3 pz)
        {
            // the matrix D with D*h_k = p_k: D = sum p_k h_k^T
            float m00 = px.X * hx.X + py.X * hy.X + pz.X * hz.X, m01 = px.X * hx.Y + py.X * hy.Y + pz.X * hz.Y, m02 = px.X * hx.Z + py.X * hy.Z + pz.X * hz.Z;
            float m10 = px.Y * hx.X + py.Y * hy.X + pz.Y * hz.X, m11 = px.Y * hx.Y + py.Y * hy.Y + pz.Y * hz.Y, m12 = px.Y * hx.Z + py.Y * hy.Z + pz.Y * hz.Z;
            float m20 = px.Z * hx.X + py.Z * hy.X + pz.Z * hz.X, m21 = px.Z * hx.Y + py.Z * hy.Y + pz.Z * hz.Y, m22 = px.Z * hx.Z + py.Z * hy.Z + pz.Z * hz.Z;
            float tr = m00 + m11 + m22, qx, qy, qz, qw;
            if (tr > 0f)
            {
                float s = (float)Math.Sqrt(tr + 1f) * 2f;
                qw = 0.25f * s; qx = (m21 - m12) / s; qy = (m02 - m20) / s; qz = (m10 - m01) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                qw = (m21 - m12) / s; qx = 0.25f * s; qy = (m01 + m10) / s; qz = (m02 + m20) / s;
            }
            else if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                qw = (m02 - m20) / s; qx = (m01 + m10) / s; qy = 0.25f * s; qz = (m12 + m21) / s;
            }
            else
            {
                float s = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
                qw = (m10 - m01) / s; qx = (m02 + m20) / s; qy = (m12 + m21) / s; qz = 0.25f * s;
            }
            return Quaternion.Normalize(new Quaternion(qx, qy, qz, qw));
        }

        /// <summary><paramref name="v"/> turned about the unit <paramref name="axis"/> by <paramref name="degrees"/> (Rodrigues).</summary>
        public static Vector3 Rotate(Vector3 v, Vector3 axis, float degrees)
        {
            float a = degrees * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        // ---- the idle poses (hip, aimed, flicks)

        /// <summary>The quill's pose when no reload is running: the hip pose eased into the aimed one by
        /// <paramref name="ads"/> (0 hip, 1 aimed), with a shot's <paramref name="flick"/> (0..1) swinging it toward the muzzle
        /// and whipping the chain, a smaller flick in ADS.</summary>
        public static ChainPose IdlePose(float ads, float flick)
        {
            ads = Clamp01(ads);
            Vector3 d = Vector3.Normalize(Vector3.Lerp(HipDirection, AdsDirection, ads));
            Vector3 to = Vector3.Normalize(Vector3.Lerp(HipToward, AdsToward, ads));
            float curl = HipCurl + (AdsCurl - HipCurl) * ads;
            if (flick > 0f)
            {
                float cut = 1f - FlickAdsCut * ads, f = Math.Min(1f, flick);
                var axis = Vector3.Cross(d, Vector3.UnitZ);       // swings the quill toward the muzzle's side
                if (axis.LengthSquared() > 1e-8f)
                {
                    var q = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), FlickDegrees * f * cut * (float)Math.PI / 180f);
                    d = Vector3.Normalize(Vector3.Transform(d, q));
                    to = Vector3.Normalize(Vector3.Transform(to, q));
                }
                curl += FlickCurlDegrees * f * cut;
            }
            return new ChainPose(Pivot, d, to, curl);
        }

        /// <summary>The quill's pose for the frame: <see cref="IdlePose"/>, or while a reload is running
        /// (<paramref name="reload"/>) the reload's pose, faded out as the aim comes up (gone by half-aimed, so the quill never
        /// leaves the well toward the sight window).</summary>
        public static ChainPose Pose(float ads, float flick, in ReloadQuill reload)
        {
            var idle = IdlePose(ads, flick);
            if (!reload.Active) return idle;
            float w = Clamp01(1f - 2f * Clamp01(ads));
            return Blend(idle, reload.Pose, w);
        }

        public static ChainPose Blend(in ChainPose a, in ChainPose b, float w)
        {
            w = Clamp01(w);
            return new ChainPose(Vector3.Lerp(a.Base, b.Base, w), Vector3.Normalize(Vector3.Lerp(a.Direction, b.Direction, w)),
                Vector3.Normalize(Vector3.Lerp(a.Toward, b.Toward, w)), a.Curl + (b.Curl - a.Curl) * w);
        }

        // ---- the reload

        /// <summary>How the reload splits the span from the "lift" event to the "return" event: out to the paper, writing, back.</summary>
        public const float OutShare = 0.2f, WriteEnd = 0.82f;
        /// <summary>Of each stroke's share of the writing, the part spent writing (the rest is the hop to the next stroke).</summary>
        public const float StrokeWriting = 0.8f;
        /// <summary>The nib lifts this high between strokes, and the arc it flies on the way out and back (m).</summary>
        public const float HopLift = 0.005f, TravelArc = 0.025f;
        /// <summary>The quill dips this much deeper than its rest when it returns (m), and the settle after the "seat" event is
        /// this long (a fraction of the clip).</summary>
        public const float DipDepth = 0.003f, SettleFraction = 0.05f;
        /// <summary>The quill's lean while it writes: leaning right and up, a little back, its tip curling right.</summary>
        public static readonly Vector3 WriteDirection = Vector3.Normalize(new Vector3(0.55f, 0.75f, -0.37f));
        public static readonly Vector3 WriteToward = Vector3.Normalize(new Vector3(0.9f, 0.1f, -0.4f));
        public const float WriteCurl = 30f;

        /// <summary>The quill during a reload: <see cref="Active"/> while it is away from its rest (or dipping), its full
        /// <see cref="Pose"/>, how far along the handwriting it is (<see cref="WriteProgress"/>, 0..1 over the three strokes) and
        /// how many of the model's handwriting pieces are visible (<see cref="Shown"/>, 0..<see cref="InkSegmentCount"/>).</summary>
        public struct ReloadQuill
        {
            public bool Active;
            public ChainPose Pose;
            public float WriteProgress;
            public int Shown;
            public bool Writing;
        }

        /// <summary>A point on the paper (the paper's own frame turned <see cref="PaperYawDegrees"/> about y): u across the page,
        /// v away from the player, at height <paramref name="y"/>.</summary>
        public static Vector3 PaperPoint(float u, float v, float y)
        {
            float a = PaperYawDegrees * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Vector3(PaperX + u * c + v * s, y, PaperZ - u * s + v * c);
        }

        /// <summary>Where stroke <paramref name="k"/> is at <paramref name="t"/> (0..1 along it): a left-to-right wave on the paper,
        /// at the nib's resting height.</summary>
        public static Vector3 StrokePoint(int k, float t)
        {
            k = k < 0 ? 0 : k >= StrokeCount ? StrokeCount - 1 : k;
            t = Clamp01(t);
            float u = StrokeU0 + (StrokeU1 - StrokeU0) * t;
            float v = StrokeV[k] + StrokeAmp * (float)Math.Sin(2.0 * Math.PI * StrokeWaves * t);
            return PaperPoint(u, v, NibPaperY);
        }

        /// <summary>The nib's tip at <paramref name="s"/> (0..1) through the whole handwriting: stroke after stroke, each written
        /// over <see cref="StrokeWriting"/> of its share and followed by a hop (the nib lifts <see cref="HopLift"/>) to the next
        /// stroke's start; after the last one it lifts away to the right. <paramref name="onPaper"/> is true while writing.</summary>
        public static Vector3 WriteTip(float s, out bool onPaper)
        {
            s = Clamp01(s);
            float x = s * StrokeCount;
            int k = Math.Min(StrokeCount - 1, (int)x);
            float f = x - k;
            onPaper = false;
            if (f < StrokeWriting)
            {
                onPaper = true;
                return StrokePoint(k, f / StrokeWriting);
            }
            float h = (f - StrokeWriting) / (1f - StrokeWriting);
            Vector3 from = StrokePoint(k, 1f);
            Vector3 to = k + 1 < StrokeCount ? StrokePoint(k + 1, 0f) : from + new Vector3(0.004f, 0f, 0f);
            return Vector3.Lerp(from, to, Smooth(h)) + new Vector3(0f, HopLift * (float)Math.Sin(Math.PI * h), 0f);
        }

        /// <summary>How many of the <see cref="InkSegmentCount"/> handwriting pieces are visible at <paramref name="s"/> (0..1):
        /// piece j of stroke k shows once the nib has passed its end.</summary>
        public static int ShownSegments(float s)
        {
            s = Clamp01(s);
            int n = 0;
            for (int k = 0; k < StrokeCount; k++)
            {
                float t = Clamp01((s * StrokeCount - k) / StrokeWriting);
                n += Math.Min(InkSegmentsPerStroke, (int)(t * InkSegmentsPerStroke + 1e-4f));
            }
            return n;
        }

        /// <summary>The quill's reload, from the clip's progress (0..1) and its "lift", "return" and "seat" events (fractions of
        /// the clip). Before the lift it is not active (the idle pose applies). From the lift: out to the paper
        /// (<see cref="OutShare"/> of the span to the return), writing (to <see cref="WriteEnd"/>), back to the well; the nib
        /// dips a little deeper as it returns and rests there until the seat, then settles to its lean over
        /// <see cref="SettleFraction"/> of the clip.</summary>
        public static ReloadQuill Reload(float progress, float liftAt, float returnAt, float seatAt)
        {
            var r = new ReloadQuill { Pose = HipPose };
            if (!(progress > liftAt) || !(returnAt > liftAt)) return r;
            float settleEnd = Math.Max(seatAt, returnAt) + SettleFraction;
            if (progress >= settleEnd) return r;
            r.Active = true;
            float u = (progress - liftAt) / (returnAt - liftAt);
            Vector3 wellTip = HipPose.NibTip;
            Vector3 up = Vector3.UnitY;
            Vector3 dir = HipDirection, to = HipToward, tip = wellTip;
            float curl = HipCurl, dip = 0f;
            if (u < OutShare)
            {
                float a = Smooth(u / OutShare);
                tip = Vector3.Lerp(wellTip, WriteTip(0f, out _), a) + up * (TravelArc * (float)Math.Sin(Math.PI * a));
                dir = Vector3.Normalize(Vector3.Lerp(HipDirection, WriteDirection, a));
                to = Vector3.Normalize(Vector3.Lerp(HipToward, WriteToward, a));
                curl = HipCurl + (WriteCurl - HipCurl) * a;
            }
            else if (u < WriteEnd)
            {
                float s = (u - OutShare) / (WriteEnd - OutShare);
                tip = WriteTip(s, out bool onPaper);
                r.Writing = onPaper;
                r.WriteProgress = s;
                r.Shown = ShownSegments(s);
                // the hand sways a little as it writes
                dir = Vector3.Normalize(WriteDirection + new Vector3(0.05f * (float)Math.Sin(s * 40f), 0f, 0.03f * (float)Math.Cos(s * 33f)));
                to = WriteToward;
                curl = WriteCurl;
            }
            else
            {
                r.WriteProgress = 1f;
                r.Shown = InkSegmentCount;
                float a = u >= 1f ? 1f : Smooth((u - WriteEnd) / (1f - WriteEnd));
                tip = Vector3.Lerp(WriteTip(1f, out _), wellTip, a) + up * (TravelArc * (float)Math.Sin(Math.PI * a));
                dir = Vector3.Normalize(Vector3.Lerp(WriteDirection, HipDirection, a));
                to = Vector3.Normalize(Vector3.Lerp(WriteToward, HipToward, a));
                curl = WriteCurl + (HipCurl - WriteCurl) * a;
                dip = Smooth((a - 0.6f) / 0.4f);
                if (progress > seatAt) dip *= 1f - Smooth((progress - seatAt) / Math.Max(1e-4f, settleEnd - seatAt));
            }
            if (u >= 1f)
            {
                r.Shown = progress > seatAt ? 0 : InkSegmentCount;      // the page is turned once the seat has happened
                dip = 1f;
                if (progress > seatAt) dip = 1f - Smooth((progress - seatAt) / Math.Max(1e-4f, settleEnd - seatAt));
            }
            r.Pose = new ChainPose(tip + dir * NibBelow - dir * (DipDepth * dip), dir, to, curl);
            return r;
        }

        static float Smooth(float u) { u = Clamp01(u); return u * u * (3f - 2f * u); }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
