using System;

namespace JamKit.Skyline
{
    /// <summary>How a spawn door moves during its 2 s telegraph.</summary>
    public enum DoorMotion
    {
        /// <summary>A shop shutter: rattles and rises (a roll-up panel on the shop's front).</summary>
        ShutterRise,
        /// <summary>A skylight vent: two flaps of a hatch slide apart.</summary>
        HatchSlide,
        /// <summary>A service hatch: a roll-up panel rises.</summary>
        HatchRoll,
        /// <summary>A rail end: the cable twangs (a quick damped shake).</summary>
        CableTwang
    }

    /// <summary>What one kind of spawn door does in its telegraph (docs/SKYLINE-FLOOR-REPOSSESS.md section 3; task SD1): a
    /// sound at the start (and at each beacon blink), a light, a motion. Pure data; the Unity door builds the parts.</summary>
    public sealed class DoorTelegraphLook
    {
        public DoorKind kind;
        public DoorMotion motion;
        /// <summary>The Skyline sound names played from the door: at the start, and again at each beacon blink when
        /// <see cref="blinkHz"/> is above zero. PLACEHOLDERS: existing clips, picked by role (the sound pass replaces them with
        /// clips of their own; only this table changes). The bot one-shots' voice gate files them under "bot.minor" or
        /// "bot.telegraph" by the last path part.</summary>
        public string startSound = "", blinkSound = "";
        /// <summary>The door's light colour (linear RGB, may exceed 1 for a glow) and the intensity it ramps up to.</summary>
        public float lightR, lightG, lightB, lightIntensity;
        /// <summary>The motion distance in metres (how far the panel rises or a flap slides) and its width, in metres.</summary>
        public float travel, width;
        /// <summary>Beacon blinks per second at the start and the end of the telegraph (0 for a door with no beacon).</summary>
        public float blinkHzStart, blinkHzEnd;
        public float blinkHz => blinkHzEnd > blinkHzStart ? blinkHzEnd : blinkHzStart;
    }

    /// <summary>The per-kind telegraph table and the timing curves the Unity door uses. A test pins that every kind has a
    /// sound, a light and a motion.</summary>
    public static class DoorTelegraphPlan
    {
        /// <summary>How long a shop shutter is (the draft width it is drawn at on its shop's front): the shop's blocks are 8 m
        /// (<c>RepossessArena.ShutterWidth</c>).</summary>
        public const float ShutterWidth = 8f;

        static readonly DoorTelegraphLook[] Looks =
        {
            new DoorTelegraphLook
            {
                kind = DoorKind.ShopShutter, motion = DoorMotion.ShutterRise,
                startSound = "window-washer-gondola/mine_lay", blinkSound = "",
                lightR = 1.7f, lightG = 1.2f, lightB = 0.3f, lightIntensity = 6f,        // warm, behind the panel
                travel = 2.5f, width = ShutterWidth, blinkHzStart = 0f, blinkHzEnd = 0f,
            },
            new DoorTelegraphLook
            {
                kind = DoorKind.SkylightVent, motion = DoorMotion.HatchSlide,
                startSound = "spotlight/lock_beep", blinkSound = "spotlight/lock_beep",
                lightR = 1.2f, lightG = 1.5f, lightB = 2.2f, lightIntensity = 8f,        // a white-blue beacon
                travel = 0.8f, width = 1.6f, blinkHzStart = 2f, blinkHzEnd = 6f,
            },
            new DoorTelegraphLook
            {
                kind = DoorKind.ServiceHatch, motion = DoorMotion.HatchRoll,
                startSound = "spotlight/lock_beep", blinkSound = "spotlight/lock_beep",
                lightR = 1.8f, lightG = 1.1f, lightB = 0.15f, lightIntensity = 7f,       // a hazard beacon, amber
                travel = 2.2f, width = 3f, blinkHzStart = 1.5f, blinkHzEnd = 4f,
            },
            new DoorTelegraphLook
            {
                kind = DoorKind.RailEnd, motion = DoorMotion.CableTwang,
                startSound = "window-washer-gondola/mine_arm", blinkSound = "",
                lightR = 1.7f, lightG = 1.6f, lightB = 1.3f, lightIntensity = 9f,        // the gondola's lamp
                travel = 0.35f, width = 0.5f, blinkHzStart = 0f, blinkHzEnd = 0f,
            },
        };

        /// <summary>The look of a kind (never null for a defined kind).</summary>
        public static DoorTelegraphLook For(DoorKind k)
        {
            for (int i = 0; i < Looks.Length; i++) if (Looks[i].kind == k) return Looks[i];
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        /// <summary>The share (0..1) of the telegraph that has run after <paramref name="elapsed"/> seconds.</summary>
        public static float Progress(float elapsed)
        {
            if (!(elapsed > 0f)) return 0f;
            float p = elapsed / FloorRules.DoorTelegraphSeconds;
            return p > 1f ? 1f : p;
        }

        /// <summary>A panel's rise or a flap's slide, 0..1 at progress <paramref name="p"/>: it hangs shut for the first fifth
        /// (the rattle), then eases open (smoothstep) so it is fully open exactly at the end of the 2 s.</summary>
        public static float Open(float p)
        {
            if (!(p > 0.2f)) return 0f;
            float t = (p - 0.2f) / 0.8f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>The rattle of a shutter before and while it lifts: an offset in metres (a few cm), strongest in the first
        /// fifth and fading to nothing by the end. <paramref name="elapsed"/> in seconds.</summary>
        public static float Rattle(float elapsed)
        {
            float p = Progress(elapsed);
            float amp = 0.03f * (1f - p);
            return amp * (float)Math.Sin(elapsed * 2f * Math.PI * 17.0);
        }

        /// <summary>The cable twang: a damped shake in metres, <paramref name="travel"/> at the start, dying out by the end.</summary>
        public static float Twang(float elapsed, float travel)
        {
            float p = Progress(elapsed);
            double decay = Math.Exp(-3.0 * p);
            return travel * (float)(decay * Math.Sin(elapsed * 2.0 * Math.PI * 4.0));
        }

        /// <summary>The beacon's brightness 0..1 at <paramref name="elapsed"/>: a square-ish pulse whose rate climbs from
        /// <see cref="DoorTelegraphLook.blinkHzStart"/> to <see cref="DoorTelegraphLook.blinkHzEnd"/> over the telegraph. A door
        /// with no beacon glows steadily, rising with the telegraph.</summary>
        public static float Beacon(DoorTelegraphLook look, float elapsed)
        {
            float p = Progress(elapsed);
            if (look.blinkHz <= 0f) return 0.35f + 0.65f * p;
            // phase integrates the rate: f(t) = f0 + (f1 - f0) * t / T, phase = f0 t + (f1 - f0) t^2 / (2T)
            float T = FloorRules.DoorTelegraphSeconds;
            float t = elapsed < 0f ? 0f : elapsed > T ? T : elapsed;
            double phase = look.blinkHzStart * t + (look.blinkHzEnd - look.blinkHzStart) * t * t / (2.0 * T);
            double frac = phase - Math.Floor(phase);
            return frac < 0.5 ? 1f : 0.15f;
        }

        /// <summary>How many beacon blinks (beeps) have started by <paramref name="elapsed"/> seconds: the integral of the rate.</summary>
        public static int BlinksStarted(DoorTelegraphLook look, float elapsed)
        {
            if (look.blinkHz <= 0f) return 0;
            float T = FloorRules.DoorTelegraphSeconds;
            float t = elapsed < 0f ? 0f : elapsed > T ? T : elapsed;
            double phase = look.blinkHzStart * t + (look.blinkHzEnd - look.blinkHzStart) * t * t / (2.0 * T);
            return (int)Math.Floor(phase) + 1;   // the blink that starts at t = 0 counts
        }
    }
}
