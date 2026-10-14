using System;
using System.Globalization;
using System.Text;

namespace JamKit.Skyline
{
    /// <summary>
    /// The player's options (the pause menu's Audio and Look pages) as plain data, saved as one string: four volumes
    /// (master for the whole mix; music; effects, which is the guns, impacts, bots and beds; voices, which is the lines and
    /// the barks) from 0 to 1, a look sensitivity multiplier for the mouse and one for the pad, invert Y, aim assist, and the
    /// field of view.
    /// Pure (no engine): SkylineSettings keeps it in PlayerPrefs and applies it.
    /// </summary>
    public sealed class SkylineOptions
    {
        public const float MinSensitivity = 0.25f, MaxSensitivity = 3f;
        /// <summary>The vertical field of view, degrees (the Look page's slider). 68 is what the movement camera gave before the
        /// slider existed (about 100 degrees across at 16:9), so it is the default and the "no change" point.</summary>
        public const float MinFieldOfView = 60f, MaxFieldOfView = 110f, DefaultFieldOfView = 68f;
        public const string Header = "skyline-options 1";

        public float master = 1f, music = 1f, effects = 1f, voices = 1f;
        /// <summary>Multiplies <c>MouseDegreesPerPixel</c>.</summary>
        public float mouseSensitivity = 1f;
        /// <summary>Multiplies <c>PadDegreesPerSecond</c>.</summary>
        public float padSensitivity = 1f;
        public bool invertY;
        public bool aimAssist = true;
        /// <summary>The main camera's resting vertical field of view (degrees); aiming down sights narrows it by the gun's own
        /// factor from there, and the speed effects widen it in proportion.</summary>
        public float fieldOfView = DefaultFieldOfView;
        /// <summary>Which subtitles show (docs/SKYLINE-HUD.md): dialogue by default; the pause menu's Subtitles page sets it.</summary>
        public SubtitleMode subtitleMode = SubtitleMode.Dialogue;
        /// <summary>The subtitle text size.</summary>
        public SubtitleSize subtitleSize = SubtitleSize.Medium;

        public SkylineOptions Clone() => (SkylineOptions)MemberwiseClone();

        /// <summary>Puts every value back in its range (a non-number becomes the default).</summary>
        public void Clamp()
        {
            master = Unit(master, 1f);
            music = Unit(music, 1f);
            effects = Unit(effects, 1f);
            voices = Unit(voices, 1f);
            mouseSensitivity = Range(mouseSensitivity, MinSensitivity, MaxSensitivity, 1f);
            padSensitivity = Range(padSensitivity, MinSensitivity, MaxSensitivity, 1f);
            fieldOfView = Range(fieldOfView, MinFieldOfView, MaxFieldOfView, DefaultFieldOfView);
            if (!Enum.IsDefined(typeof(SubtitleMode), subtitleMode)) subtitleMode = SubtitleMode.Dialogue;
            if (!Enum.IsDefined(typeof(SubtitleSize), subtitleSize)) subtitleSize = SubtitleSize.Medium;
        }

        static float Unit(float v, float fallback) => Range(v, 0f, 1f, fallback);

        static float Range(float v, float lo, float hi, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return fallback;
            return v < lo ? lo : v > hi ? hi : v;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Header).Append('\n');
            Add(sb, "master", master);
            Add(sb, "music", music);
            Add(sb, "effects", effects);
            Add(sb, "voices", voices);
            Add(sb, "mouse", mouseSensitivity);
            Add(sb, "pad", padSensitivity);
            Add(sb, "fov", fieldOfView);
            sb.Append("subtitles=").Append((int)subtitleMode).Append('\n');
            sb.Append("subtitleSize=").Append((int)subtitleSize).Append('\n');
            sb.Append("invertY=").Append(invertY ? "1" : "0").Append('\n');
            sb.Append("aimAssist=").Append(aimAssist ? "1" : "0").Append('\n');
            return sb.ToString();
        }

        static void Add(StringBuilder sb, string key, float v) =>
            sb.Append(key).Append('=').Append(v.ToString("0.####", CultureInfo.InvariantCulture)).Append('\n');

        /// <summary>The options a <see cref="Serialize"/>d string describes; whatever is missing or unreadable stays at its
        /// default.</summary>
        public static SkylineOptions Deserialize(string text)
        {
            var o = new SkylineOptions();
            if (!string.IsNullOrEmpty(text))
            {
                foreach (var raw in text.Split('\n'))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq), value = line.Substring(eq + 1).Trim();
                    float f;
                    bool isNumber = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
                    switch (key)
                    {
                        case "master": if (isNumber) o.master = f; break;
                        case "music": if (isNumber) o.music = f; break;
                        case "effects": if (isNumber) o.effects = f; break;
                        case "voices": if (isNumber) o.voices = f; break;
                        case "mouse": if (isNumber) o.mouseSensitivity = f; break;
                        case "pad": if (isNumber) o.padSensitivity = f; break;
                        case "fov": if (isNumber) o.fieldOfView = f; break;
                        case "subtitles": if (isNumber) o.subtitleMode = (SubtitleMode)(int)f; break;
                        case "subtitleSize": if (isNumber) o.subtitleSize = (SubtitleSize)(int)f; break;
                        case "invertY": o.invertY = value == "1"; break;
                        case "aimAssist": o.aimAssist = value != "0"; break;
                    }
                }
            }
            o.Clamp();
            return o;
        }
    }

    /// <summary>
    /// Aim down sights, as pure numbers (docs: the pause-menu plan, section 3): the ease in (0.15 s) and out (0.12 s), the scales
    /// that follow it (field of view, look sensitivity, move speed, pellet spread; each gun's own, in <see cref="GunDef"/>),
    /// when a gun may be aimed, and the pose that puts a gun's sights on the centre of the screen.
    /// </summary>
    public static class AimDownSights
    {
        public const float InSeconds = 0.15f, OutSeconds = 0.12f;

        /// <summary>The raw amount (0 hip, 1 aimed) moved toward <paramref name="want"/> by <paramref name="dt"/> seconds.</summary>
        public static float Step(float amount, bool want, float dt)
        {
            if (!(dt > 0f)) return Clamp01(amount);
            float to = want ? 1f : 0f;
            float rate = dt / (want ? InSeconds : OutSeconds);
            if (amount < to) return Math.Min(to, amount + rate);
            return Math.Max(to, amount - rate);
        }

        /// <summary>The smoothstep of a raw amount: the eased 0..1 the poses and scales use.</summary>
        public static float Ease(float amount)
        {
            float t = Clamp01(amount);
            return t * t * (3f - 2f * t);
        }

        /// <summary>A gun's scale at this raw amount: 1 at the hip, <paramref name="aimedScale"/> fully aimed.</summary>
        public static float Scale(float aimedScale, float amount) => 1f + (aimedScale - 1f) * Ease(amount);

        /// <summary>True when the gun may be aimed: not equipping, inspecting or reloading (and the caller has already taken
        /// sprint out of the question: aiming ends a sprint).</summary>
        public static bool CanAim(bool equipped, bool equipping, bool inspecting, bool reloading) =>
            equipped && !equipping && !inspecting && !reloading;

        /// <summary>The gun-root pose (in the viewmodel camera's space) that puts the sight line through the screen centre: the
        /// line from <paramref name="rear"/> to <paramref name="front"/> (the gun's frame) runs straight down the camera's
        /// forward axis with the rear sight <paramref name="rearDistance"/> metres in front of the eye. The rotation is the
        /// smallest one that does it (no roll).</summary>
        public static void SolveSights(System.Numerics.Vector3 rear, System.Numerics.Vector3 front, float rearDistance,
            out System.Numerics.Vector3 position, out System.Numerics.Quaternion rotation)
        {
            var dir = front - rear;
            rotation = FromTo(dir, new System.Numerics.Vector3(0f, 0f, 1f));
            position = new System.Numerics.Vector3(0f, 0f, rearDistance) - System.Numerics.Vector3.Transform(rear, rotation);
        }

        /// <summary>For a gun with no sight models: no turn, the muzzle <paramref name="below"/> metres under the screen
        /// centre (its line just below the middle), the gun's origin <paramref name="distance"/> metres out.</summary>
        public static void SolveMuzzle(System.Numerics.Vector3 muzzle, float below, float distance,
            out System.Numerics.Vector3 position, out System.Numerics.Quaternion rotation)
        {
            rotation = System.Numerics.Quaternion.Identity;
            position = new System.Numerics.Vector3(-muzzle.X, -below - muzzle.Y, distance);
        }

        /// <summary>The smallest rotation taking direction <paramref name="a"/> to direction <paramref name="b"/>.</summary>
        public static System.Numerics.Quaternion FromTo(System.Numerics.Vector3 a, System.Numerics.Vector3 b)
        {
            if (a.LengthSquared() < 1e-12f || b.LengthSquared() < 1e-12f) return System.Numerics.Quaternion.Identity;
            a = System.Numerics.Vector3.Normalize(a);
            b = System.Numerics.Vector3.Normalize(b);
            float d = System.Numerics.Vector3.Dot(a, b);
            if (d > 0.999999f) return System.Numerics.Quaternion.Identity;
            if (d < -0.999999f) return System.Numerics.Quaternion.CreateFromAxisAngle(new System.Numerics.Vector3(0f, 1f, 0f), (float)Math.PI);
            var axis = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(a, b));
            return System.Numerics.Quaternion.CreateFromAxisAngle(axis, (float)Math.Acos(d));
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// How Skyline sets the shared movement camera's speed effects on its own player (the shared code and Visiting Hours'
    /// values are untouched; SkylinePlayer and the player builder apply these to the MovementCamera).
    /// The creator (2026-10-14): sprinting from a dead stop kicked the view wider for a moment; that is reserved for much
    /// faster movement. Before: the sprint kick (fired when sprint starts below 60% of sprint speed) punched the view out
    /// <see cref="OldSprintKickFov"/> degrees for 0.35 s, and the speed widening ran from 9.5 m/s (exactly sprint speed) to
    /// 22 m/s at up to +8 degrees. Now: no sprint kick on the view, and the widening starts 3.5 m/s above sprint speed
    /// (<see cref="SpeedFovFrom"/>) and reaches its full +8 degrees at <see cref="SpeedFovFull"/> (slides at speed, Breacher
    /// boosts, chained wall-runs), with a slower ease (<see cref="SpeedFovRate"/>).
    /// </summary>
    public static class SkylineViewFeel
    {
        public const float OldSprintKickFov = 4f, OldSpeedFovFrom = 9.5f, OldSpeedFovFull = 22f, OldSpeedFovRate = 6f;
        /// <summary>Degrees the view is punched out when a sprint starts from slow (0: never).</summary>
        public const float SprintKickFov = 0f;
        /// <summary>The speed widening (m/s): none at or below <see cref="SpeedFovFrom"/>, all of it at <see cref="SpeedFovFull"/>.</summary>
        public const float SpeedFovFrom = 13f, SpeedFovFull = 26f;
        /// <summary>The view follows the widening's target at this rate (per second); lower is softer.</summary>
        public const float SpeedFovRate = 4f;
    }
}
