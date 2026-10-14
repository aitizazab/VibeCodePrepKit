using System;
using System.Collections.Generic;

namespace JamKit.SkylineRepo
{
    /// <summary>What a sound is, for the mix: its bus gain, how far it ducks under speech, and its voice priority.</summary>
    public enum SkylineSoundCategory
    {
        /// <summary>The player's shots: writ/shot, breacher/blast, breacher/vent_hiss, breacher/boost.</summary>
        Guns,
        /// <summary>The player's gun handling: reloads, equips, inspects, dry fire (every other writ/ and breacher/ sound).</summary>
        Handling,
        /// <summary>What a shot hits: impact/*, writ/impact* (incl. the weak-spot gavel crack), writ/spot_*, breacher/impact.</summary>
        Impacts,
        /// <summary>The bots' one-shots: &lt;bot id&gt;/alert, telegraph, attack, hurt, plate_shed, wreck, ...</summary>
        Bots,
        /// <summary>Beds and loops: &lt;bot id&gt;/idle_loop, writ/hush.</summary>
        Ambience,
        /// <summary>Spoken lines and barks (the teaching-line voice and the bots' babble): they are not played through
        /// <see cref="SkylineSounds"/>, but they take this bus's user gain (the pause menu's Voices slider).</summary>
        Voices,
        /// <summary>Music (none in M1; the pause menu's Music slider drives this bus for when there is some).</summary>
        Music,
    }

    /// <summary>
    /// Skyline's mix, as data (judge round 1, E1/E8, C2/C7: the arena recording measured -5.9 LUFS with 77,233 clipped
    /// samples; every one-shot played at volume 1 with no bus, ducking or limiter). Every Skyline sound goes through
    /// <see cref="SkylineSounds"/>, which applies this:
    /// <list type="bullet">
    /// <item>a gain per category (the bus) and a trim per sound (WAVs mastered far hotter than their neighbours);</item>
    /// <item>one hit sound a shot: in one frame, a gun's own impact wins over the shared impact/hit tick
    /// (<see cref="LosesTo"/>; SkylineShooting already skips the tick for a gun with its own impact);</item>
    /// <item>a voice budget per pool that steals the least audible voice (quiet and nearly over), at most
    /// <see cref="MaxSameSound"/> of one sound at once (the oldest fades), and no retrigger of a non-gun sound within
    /// <see cref="RetriggerSeconds"/>;</item>
    /// <item>ducking under speech: the teaching lines (the arena's 2D voice, followed by its level) duck the effects fully,
    /// a bark partly (by its distance);</item>
    /// <item>the master safety limiter on the listener (<see cref="SkylineMasterLimiter"/>): nothing leaves above
    /// <see cref="CeilingDb"/>, and nothing is touched while the sum stays under the knee.</item>
    /// </list>
    /// The numbers come from an offline model of the sum (the shipped WAVs at the game's rates and the range's real
    /// schedule, with the limiter run as this C#): see .superpowers/sdd/SKYLINE-PLAN-M1/fix-r1-mix-report.md.
    /// </summary>
    public static class SkylineMix
    {
        // ---------------------------------------------------------------- the buses
        /// <summary>Linear gain per <see cref="SkylineSoundCategory"/>. The player's gun, its handling and its hits share one
        /// level (the gun agents balance shot against handling in the WAVs); the bots sit under the gun; the beds well under.</summary>
        public static readonly float[] CategoryGain =
        {
            0.63f,  // Guns      (-4 dB)
            0.63f,  // Handling  (-4 dB)
            0.63f,  // Impacts   (-4 dB)
            0.28f,  // Bots      (-11 dB: five bots fire every 0.8-1.2 s, and their WAVs are mastered hot)
            0.5f,   // Ambience  (-6 dB: the idle loops of every bot in earshot add up to a bed)
            1f,     // Voices    (the lines carry their own level, SkylineMix.VoiceGain; the player's slider scales it)
            1f,     // Music     (none yet)
        };

        /// <summary>How far each category ducks (dB) while a teaching line is spoken (a bark ducks by
        /// <see cref="BarkDuckShare"/> of it). The handling never ducks: a reload must stay audible.</summary>
        public static readonly float[] DuckDb = { -3f, 0f, -4f, -6f, -6f, 0f, 0f };

        /// <summary>Unity voice priority per category (0 = most important).</summary>
        public static readonly int[] Priority = { 16, 32, 48, 128, 200, 24, 220 };

        /// <summary>The arena's teaching-line voice (the 2D source the lines are spoken from): its level on the bus.</summary>
        public const float VoiceGain = 0.8f;

        /// <summary>A bark ducks the effects by this share of <see cref="DuckDb"/>, times its distance rolloff.</summary>
        public const float BarkDuckShare = 0.5f;
        /// <summary>Ducking time constants (seconds): in fast, out slowly enough to bridge the babble's gaps.</summary>
        public const float DuckAttack = 0.06f, DuckRelease = 0.35f;
        /// <summary>The voice tap's level (linear peak per audio block) above which a line counts as speaking, and how
        /// long it keeps counting after the last loud block.</summary>
        public const float SpeechThreshold = 0.01f, SpeechHold = 0.15f;

        // ---------------------------------------------------------------- the voices
        public const int ViewVoices = 10, WorldVoices = 18, SpareVoices = 4;
        public const int MaxSameSound = 3;
        public const float RetriggerSeconds = 0.04f;
        /// <summary>A stolen or capped voice fades out over this long instead of cutting (no click).</summary>
        public const float StealFadeSeconds = 0.04f;

        // ---------------------------------------------------------------- the master
        /// <summary>The limiter's ceiling (sample and midpoint peaks; the model's true peak lands at -1.4 to -1.8 dBTP) and
        /// its soft knee, look-ahead and release.</summary>
        public const float CeilingDb = -2f, KneeDb = 2f, LookaheadMs = 5f, ReleaseMs = 150f;

        // ---------------------------------------------------------------- per-sound trims
        /// <summary>Trims (dB) on top of the bus. The drone's alert, telegraph and hurt are mastered 8-10 dB hotter than any
        /// other bot's (judge E8); the floor-care hurt too. The Writ's ordinary and weak hits take over from the dropped
        /// impact/hit tick (<see cref="LosesTo"/>), so they come up by the level the tick gave.</summary>
        static readonly Dictionary<string, float> TrimDb = new Dictionary<string, float>
        {
            { "mall-cop-drone/alert", -8f },
            { "mall-cop-drone/telegraph", -6f },
            { "mall-cop-drone/hurt", -6f },
            { "mall-cop-drone/attack", -3f },
            { "floor-care-unit/hurt", -6f },
            // The practice drone's rotor bed (the creator, 2026-10-14: "very grating noise ... it won't shut up"): the stem is
            // now a soft low hum (tools/skyline/bots/mall-cop-drone/sources.py) and sits another 6 dB under the other beds.
            { "mall-cop-drone/idle_loop", -6f },
            { "writ/impact", 5f },
            { "writ/impact_weak", 5f },
        };

        // ---------------------------------------------------------------- one hit sound a shot
        /// <summary>Loser → the sounds that win over it when they play in the same frame. A backstop for the rule both gun
        /// agents agreed, which SkylineShooting applies at the source (impact/hit only for a gun with no impact of its own);
        /// a Writ spot's own sound (spot_form / spot_break) still plays beside the hit, so two at most.</summary>
        static readonly Dictionary<string, string[]> Dedupe = new Dictionary<string, string[]>
        {
            { "impact/hit", new[] { "writ/impact", "writ/impact_weak", "writ/spot_pop", "breacher/impact" } },
        };
        static HashSet<string> _deferred;

        /// <summary>The category of a sound name ("writ/shot", "mall-cop-drone/alert").</summary>
        public static SkylineSoundCategory CategoryOf(string sound)
        {
            if (string.IsNullOrEmpty(sound)) return SkylineSoundCategory.Bots;
            if (sound.EndsWith("/idle_loop", StringComparison.Ordinal) || sound == "writ/hush") return SkylineSoundCategory.Ambience;
            if (sound.StartsWith("impact/", StringComparison.Ordinal)) return SkylineSoundCategory.Impacts;
            if (sound.StartsWith("writ/", StringComparison.Ordinal))
            {
                if (sound.StartsWith("writ/shot", StringComparison.Ordinal)) return SkylineSoundCategory.Guns;
                if (sound.StartsWith("writ/impact", StringComparison.Ordinal) || sound.StartsWith("writ/spot_", StringComparison.Ordinal))
                    return SkylineSoundCategory.Impacts;
                return SkylineSoundCategory.Handling;
            }
            if (sound.StartsWith("breacher/", StringComparison.Ordinal))
            {
                if (sound == "breacher/blast" || sound == "breacher/vent_hiss" || sound == "breacher/boost") return SkylineSoundCategory.Guns;
                if (sound == "breacher/impact") return SkylineSoundCategory.Impacts;
                return SkylineSoundCategory.Handling;
            }
            return SkylineSoundCategory.Bots;
        }

        /// <summary>The player's own volume for a category (the pause menu's Audio page): Voices and Music have a slider each;
        /// every other category is an effect. The master volume is the listener's (SkylineSettings.ApplyMaster).</summary>
        public static float UserGain(SkylineSoundCategory c)
        {
            var o = SkylineSettings.Options;
            if (c == SkylineSoundCategory.Voices) return o.voices;
            if (c == SkylineSoundCategory.Music) return o.music;
            return o.effects;
        }

        /// <summary>The bus gain times the sound's trim (linear).</summary>
        public static float GainOf(string sound)
        {
            float g = CategoryGain[(int)CategoryOf(sound)];
            if (sound != null && TrimDb.TryGetValue(sound, out var db)) g *= (float)Math.Pow(10.0, db / 20.0);
            return g;
        }

        /// <summary>True for a sound that takes part in "one hit sound a shot": it waits for the end of the frame.</summary>
        public static bool Deferred(string sound)
        {
            if (_deferred == null)
            {
                _deferred = new HashSet<string>();
                foreach (var kv in Dedupe)
                {
                    _deferred.Add(kv.Key);
                    foreach (var w in kv.Value) _deferred.Add(w);
                }
            }
            return sound != null && _deferred.Contains(sound);
        }

        /// <summary>True when <paramref name="sound"/> is dropped because a sound that wins over it plays in the same frame.</summary>
        public static bool LosesTo(string sound, ICollection<string> sameFrame)
        {
            if (sound == null || !Dedupe.TryGetValue(sound, out var winners)) return false;
            foreach (var w in winners) if (sameFrame.Contains(w)) return true;
            return false;
        }

        /// <summary>Ducking gain (linear) for a category at duck depth <paramref name="depth"/> (0 = none, 1 = a line).</summary>
        public static float DuckGain(SkylineSoundCategory c, float depth)
        {
            if (depth <= 0f) return 1f;
            return (float)Math.Pow(10.0, DuckDb[(int)c] * depth / 20.0);
        }
    }
}
