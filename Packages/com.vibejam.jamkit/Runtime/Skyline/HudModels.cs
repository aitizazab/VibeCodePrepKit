using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>The one health bar's numbers (docs/SKYLINE-HUD.md; docs/SKYLINE-PLAN-FLOOR1.md Task V1): chassis (health) is the
    /// bar and plating (the shield) a brighter layer over it. Pure; the Unity HUD (H2) only draws what this says.</summary>
    public sealed class HealthBarModel
    {
        /// <summary>Chassis below this fraction pulses the bar and tints the screen edges red.</summary>
        public const float LowChassis = 0.35f;
        /// <summary>The low-health pulse rate, in cycles per second.</summary>
        public const float PulseHz = 1.6f;

        readonly float _chassisMax, _platingMax;
        float _chassis, _plating, _phase;

        /// <summary>Both bars start full. A max of 0 or less (or NaN) means that bar is always empty.</summary>
        public HealthBarModel(float chassisMax, float platingMax)
        {
            _chassisMax = Sane(chassisMax);
            _platingMax = Sane(platingMax);
            _chassis = _chassisMax;
            _plating = _platingMax;
        }

        static float Sane(float v) => float.IsNaN(v) || v < 0f ? 0f : v;

        /// <summary>Chassis as a fraction of its maximum, 0..1.</summary>
        public float ChassisFrac => _chassisMax > 0f ? _chassis / _chassisMax : 0f;
        /// <summary>Plating as a fraction of its maximum, 0..1.</summary>
        public float PlatingFrac => _platingMax > 0f ? _plating / _platingMax : 0f;
        /// <summary>0..1 sine at <see cref="PulseHz"/> while chassis is under <see cref="LowChassis"/> (starts at 0 when the
        /// bar goes low, so it eases in), else 0.</summary>
        public float LowPulse => Low ? 0.5f - 0.5f * (float)Math.Cos(_phase) : 0f;
        /// <summary>The red screen-edge tint, 0..1; it follows <see cref="LowPulse"/>.</summary>
        public float EdgeTint => LowPulse;
        /// <summary>Counts every time the plating went from above 0 to 0 (the shatter animation plays once per break).</summary>
        public int ShatterEvents { get; private set; }

        bool Low => ChassisFrac < LowChassis;

        /// <summary>Sets the current values; out of range clamps to 0..max and NaN counts as 0.</summary>
        public void Set(float chassis, float plating)
        {
            _chassis = Clamp(chassis, _chassisMax);
            float before = _plating;
            _plating = Clamp(plating, _platingMax);
            if (before > 0f && _plating <= 0f) ShatterEvents++;
            if (!Low) _phase = 0f;
        }

        static float Clamp(float v, float max)
        {
            if (float.IsNaN(v) || v < 0f) return 0f;
            return v > max ? max : v;
        }

        /// <summary>Advances the pulse.</summary>
        public void Tick(float dt)
        {
            if (float.IsNaN(dt) || dt <= 0f) return;
            if (!Low) { _phase = 0f; return; }
            _phase += 2f * (float)Math.PI * PulseHz * dt;
            _phase %= 2f * (float)Math.PI;
        }
    }

    /// <summary>Where an edge marker for a world point goes on the screen.</summary>
    public struct EdgeMarkerResult
    {
        /// <summary>The point is in front of the camera and inside the view.</summary>
        public bool onScreen;
        /// <summary>The point is behind the camera plane.</summary>
        public bool behind;
        /// <summary>Screen position, 0..1 with (0, 0) bottom left; always inside the margin box. On screen: the point's own place
        /// (pulled inside the margin box); off screen or behind: on the margin ring in the point's direction.</summary>
        public float x01, y01;
        /// <summary>The direction to the point as seen on screen, radians counter-clockwise from screen right (0 = right,
        /// pi/2 = up); it is the way a chevron on the ring points.</summary>
        public float angleRad;
        /// <summary>3D distance from the camera, metres.</summary>
        public float distance;
    }

    /// <summary>Projects a world point to the screen for the objective's edge markers (H3). Left-handed like Unity: +Y up, a camera
    /// facing +Z has +X on its right.</summary>
    public static class EdgeMarker
    {
        public static EdgeMarkerResult Compute(Vector3 cam, Vector3 forward, Vector3 up, float vFovDeg, float aspect,
            Vector3 world, float margin01 = 0.06f)
        {
            var r = new EdgeMarkerResult();
            Vector3 d = world - cam;
            r.distance = d.Length();
            if (float.IsNaN(r.distance)) r.distance = 0f;

            Vector3 f = Norm(forward, new Vector3(0f, 0f, 1f));
            Vector3 rightRaw = Vector3.Cross(up, f);
            Vector3 right = Norm(rightRaw, new Vector3(1f, 0f, 0f));
            Vector3 u = Vector3.Cross(f, right);

            float margin = float.IsNaN(margin01) ? 0.06f : Math.Max(0f, Math.Min(0.45f, margin01));
            if (!(aspect > 0.01f)) aspect = 16f / 9f;
            float tanV = (float)Math.Tan(Math.Max(1f, Math.Min(170f, float.IsNaN(vFovDeg) ? 60f : vFovDeg)) * Math.PI / 360.0);
            float tanH = tanV * aspect;

            float xc = Vector3.Dot(d, right), yc = Vector3.Dot(d, u), zc = Vector3.Dot(d, f);
            r.behind = zc <= 0f;

            float nx, ny;       // the point in normalised device coordinates (-1..1 inside the view)
            if (!r.behind)
            {
                nx = xc / (zc * tanH);
                ny = yc / (zc * tanV);
                r.onScreen = Math.Abs(nx) <= 1f && Math.Abs(ny) <= 1f;
            }
            else
            {
                // behind: the direction the point lies in across the view plane, length irrelevant
                nx = xc / tanH;
                ny = yc / tanV;
                if (Math.Abs(nx) < 1e-5f && Math.Abs(ny) < 1e-5f) ny = -1f;   // straight behind: point down
            }

            float half = 1f - 2f * margin;      // the margin box's half extent in NDC
            if (r.onScreen)
            {
                nx = Math.Max(-half, Math.Min(half, nx));
                ny = Math.Max(-half, Math.Min(half, ny));
            }
            else
            {
                float ax = Math.Abs(nx), ay = Math.Abs(ny);
                float s = Math.Min(ax > 1e-6f ? half / ax : float.MaxValue, ay > 1e-6f ? half / ay : float.MaxValue);
                nx *= s;
                ny *= s;
            }
            r.x01 = 0.5f + 0.5f * nx;
            r.y01 = 0.5f + 0.5f * ny;
            r.angleRad = (float)Math.Atan2(ny, nx * aspect);
            return r;
        }

        static Vector3 Norm(Vector3 v, Vector3 fallback)
        {
            float l = v.Length();
            if (float.IsNaN(l) || l < 1e-6f) return fallback;
            return v / l;
        }
    }

    /// <summary>Which subtitles show (docs/SKYLINE-HUD.md): none, dialogue (the crew and CONCIERGE), or dialogue and bot barks.</summary>
    public enum SubtitleMode { Off, Dialogue, DialogueAndBarks }

    /// <summary>The subtitle size setting.</summary>
    public enum SubtitleSize { Small, Medium, Large }

    /// <summary>One subtitle line on screen.</summary>
    public sealed class SubtitleLine
    {
        public string speaker, text;
        /// <summary>CONCIERGE's PA: drawn in italics.</summary>
        public bool pa;
        /// <summary>The time (the caller's clock) at which the line leaves the screen.</summary>
        public float expires;
    }

    /// <summary>The subtitles' content: at most two lines, newest last; a third pushes the oldest out; lines expire. Bark lines
    /// are kept only in <see cref="SubtitleMode.DialogueAndBarks"/>.</summary>
    public sealed class SubtitleBuffer
    {
        public const int MaxLines = 2;
        readonly List<SubtitleLine> _lines = new List<SubtitleLine>();

        /// <summary>Adds a line, unless the mode filters it (Off drops everything, Dialogue drops barks) or it is empty.</summary>
        public void Say(string speaker, string text, bool pa, bool bark, float now, float seconds, SubtitleMode mode)
        {
            Purge(now);
            if (mode == SubtitleMode.Off) return;
            if (bark && mode != SubtitleMode.DialogueAndBarks) return;
            if (string.IsNullOrWhiteSpace(text)) return;
            _lines.Add(new SubtitleLine
            {
                speaker = speaker ?? "",
                text = text,
                pa = pa,
                expires = now + (float.IsNaN(seconds) ? 0f : Math.Max(0f, seconds)),
            });
            while (_lines.Count > MaxLines) _lines.RemoveAt(0);
        }

        /// <summary>The lines still on screen at <paramref name="now"/>, oldest first.</summary>
        public IReadOnlyList<SubtitleLine> Visible(float now)
        {
            Purge(now);
            return _lines;
        }

        /// <summary>Drops every line (a setting change to Off, a restart).</summary>
        public void Clear() => _lines.Clear();

        void Purge(float now) => _lines.RemoveAll(l => l.expires <= now);

        /// <summary>The text size in pixels at the 1080p reference.</summary>
        public static float FontPx(SubtitleSize s)
        {
            switch (s)
            {
                case SubtitleSize.Small: return 28f;
                case SubtitleSize.Large: return 42f;
                default: return 34f;
            }
        }
    }

    /// <summary>Strings the HUD shows.</summary>
    public static class HudText
    {
        /// <summary>"NEXT 0:21": seconds until the next wave, rounded up (so it reads 0:00 only at the moment itself); minutes
        /// are not padded. Negative or NaN reads 0:00.</summary>
        public static string WaveClock(float seconds)
        {
            int s = float.IsNaN(seconds) || seconds <= 0f ? 0 : (int)Math.Ceiling(Math.Min(seconds, 5999f));
            return string.Format(CultureInfo.InvariantCulture, "NEXT {0}:{1:00}", s / 60, s % 60);
        }

        /// <summary>"34 m": whole metres, nearest. Negative or NaN reads 0 m.</summary>
        public static string Distance(float metres)
        {
            int m = float.IsNaN(metres) || metres <= 0f ? 0 : (int)Math.Round(Math.Min(metres, 99999f), MidpointRounding.AwayFromZero);
            return m.ToString(CultureInfo.InvariantCulture) + " m";
        }
    }

    /// <summary>Subtitle prefix colours by speaker (carried over from the M1 speaker card). None lies in the hostile red-magenta
    /// band (<c>#FF2E63</c> +- 20 degrees of hue), the reserved hue.</summary>
    public static class SpeakerColours
    {
        public static (float r, float g, float b) Of(string speaker)
        {
            switch ((speaker ?? "").Trim().ToUpperInvariant())
            {
                case "DOT": return (1f, 0.8f, 0.35f);
                case "NAN": return (0.55f, 0.92f, 0.85f);
                case "CONCIERGE": return (0.96f, 0.88f, 0.62f);
                default: return (1f, 1f, 1f);
            }
        }
    }

    /// <summary>What phase of the wreck sequence the screen is in.</summary>
    public enum WreckPhase { Stagger, Fall, Lie, FadeOut, Quote, FadeIn, Done }

    /// <summary>One sampled instant of the wreck sequence.</summary>
    public struct WreckFrame
    {
        public WreckPhase phase;
        /// <summary>Seconds since the sequence began, and how far through the current phase (0..1).</summary>
        public float time, phase01;
        /// <summary>Stagger and fall progress 0..1 each (the body animation: stagger sways and clutches, the fall tips the Unit
        /// over, the lie holds the final pose: head turned aside, one arm stretched out).</summary>
        public float stagger01, fall01;
        /// <summary>The black overlay's alpha 0..1 (1 while the quote shows).</summary>
        public float blackAlpha;
        /// <summary>The quote's alpha 0..1 (it fades in and out within its hold).</summary>
        public float quoteAlpha;
        /// <summary>The world may be reset behind black: true from the moment the screen is fully black until the sequence ends.</summary>
        public bool worldMayReset;
        /// <summary>The lift ride (music sync, the lobby view) may begin: true from the start of the final fade-in.</summary>
        public bool liftRideRunning;
    }

    /// <summary>Being wrecked, in time (docs/SKYLINE-FLOOR-REPOSSESS.md section 10): the Unit staggers, clutches at itself and falls,
    /// lies with its head turned aside and an arm out, the screen fades to black, a quote (CONCIERGE's passive-aggressive voice,
    /// drafts in docs/SKYLINE-DRAFT-LINES.md) holds for a time that scales with its length, then it fades into the lift ride.
    /// Pure timing: the player/Unity side poses the body and draws the overlay from <see cref="Sample"/>. Starting numbers, to be
    /// tuned with the creator.</summary>
    public sealed class WreckSequence
    {
        public const float StaggerSeconds = 0.7f, FallSeconds = 0.9f, LieSeconds = 0.9f, FadeOutSeconds = 0.8f, FadeInSeconds = 1.2f;
        /// <summary>The quote fades in and out inside its hold, this long each.</summary>
        public const float QuoteFadeSeconds = 0.35f;
        /// <summary>Quote hold = clamp(<see cref="HoldBase"/> + <see cref="HoldPerChar"/> * length, <see cref="HoldMin"/>, <see cref="HoldMax"/>).</summary>
        public const float HoldBase = 1.5f, HoldPerChar = 0.06f, HoldMin = 2f, HoldMax = 8f;

        /// <summary>How long the quote stays up: reading time that grows with its length. A null or empty quote still holds the minimum.</summary>
        public static float QuoteHold(int length)
        {
            float h = HoldBase + HoldPerChar * Math.Max(0, length);
            return Math.Max(HoldMin, Math.Min(HoldMax, h));
        }

        public int QuoteLength { get; }
        public float QuoteHoldSeconds { get; }
        /// <summary>The start time of each phase, then the total.</summary>
        public float FallAt => StaggerSeconds;
        public float LieAt => FallAt + FallSeconds;
        public float FadeOutAt => LieAt + LieSeconds;
        public float QuoteAt => FadeOutAt + FadeOutSeconds;
        public float FadeInAt => QuoteAt + QuoteHoldSeconds;
        public float TotalSeconds => FadeInAt + FadeInSeconds;

        public WreckSequence(string quote) : this(quote == null ? 0 : quote.Length) { }

        public WreckSequence(int quoteLength)
        {
            QuoteLength = Math.Max(0, quoteLength);
            QuoteHoldSeconds = QuoteHold(QuoteLength);
        }

        /// <summary>The state <paramref name="t"/> seconds after the wreck (negative reads as 0; past the end reads Done).</summary>
        public WreckFrame Sample(float t)
        {
            if (float.IsNaN(t) || t < 0f) t = 0f;
            var f = new WreckFrame { time = t };
            if (t < FallAt)
            {
                f.phase = WreckPhase.Stagger; f.phase01 = t / StaggerSeconds; f.stagger01 = f.phase01;
            }
            else if (t < LieAt)
            {
                f.phase = WreckPhase.Fall; f.phase01 = (t - FallAt) / FallSeconds; f.stagger01 = 1f; f.fall01 = f.phase01;
            }
            else if (t < FadeOutAt)
            {
                f.phase = WreckPhase.Lie; f.phase01 = (t - LieAt) / LieSeconds; f.stagger01 = 1f; f.fall01 = 1f;
            }
            else if (t < QuoteAt)
            {
                f.phase = WreckPhase.FadeOut; f.phase01 = (t - FadeOutAt) / FadeOutSeconds; f.stagger01 = 1f; f.fall01 = 1f;
                f.blackAlpha = f.phase01;
            }
            else if (t < FadeInAt)
            {
                f.phase = WreckPhase.Quote; f.phase01 = (t - QuoteAt) / QuoteHoldSeconds; f.stagger01 = 1f; f.fall01 = 1f;
                f.blackAlpha = 1f;
                float into = t - QuoteAt, left = FadeInAt - t;
                f.quoteAlpha = Math.Min(1f, Math.Min(into, left) / QuoteFadeSeconds);
                f.worldMayReset = true;
            }
            else if (t < TotalSeconds)
            {
                f.phase = WreckPhase.FadeIn; f.phase01 = (t - FadeInAt) / FadeInSeconds; f.stagger01 = 1f; f.fall01 = 1f;
                f.blackAlpha = 1f - f.phase01;
                f.worldMayReset = true;
                f.liftRideRunning = true;
            }
            else
            {
                f.phase = WreckPhase.Done; f.phase01 = 1f; f.stagger01 = 1f; f.fall01 = 1f;
                f.worldMayReset = true;
                f.liftRideRunning = true;
            }
            return f;
        }
    }
}
