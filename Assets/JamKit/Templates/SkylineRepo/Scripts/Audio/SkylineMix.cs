using System;
using System.Collections.Generic;
using JamKit.Skyline;

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
        /// <summary>What the player feels: player/hurt, player/plating_break and CONCIERGE's PA chime and sting. At the guns' level, so
        /// the player's own hit feedback is never buried under the bots' noise.</summary>
        Feedback,
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
            0.79f,  // Guns      (-2 dB; was -4: the mix pass lifted the player's side by 2 dB, the bots came down)
            0.79f,  // Handling  (-2 dB)
            0.79f,  // Impacts   (-2 dB)
            0.25f,  // Bots      (-12 dB; was -11 before the 2026-10-14 mix pass: the bots' WAVs are mastered 8-10 dB hotter than the guns')
            0.4f,   // Ambience  (-8 dB; was -6: only the nearest SkylineMix.MaxLoops loops are heard now, but they sit lower)
            1f,     // Voices    (the lines carry their own level, SkylineMix.VoiceGain; the player's slider scales it)
            1f,     // Music     (none yet)
            0.79f,  // Feedback  (-2 dB: the player's own hurt and plating cues; they were on the bots' bus, 9 dB down)
        };

        /// <summary>How far each category ducks (dB) while a teaching line is spoken (a bark ducks by
        /// <see cref="BarkDuckShare"/> of it). The handling never ducks: a reload must stay audible.</summary>
        public static readonly float[] DuckDb = { -3f, 0f, -4f, -8f, -9f, 0f, 0f, 0f };

        /// <summary>How far each category ducks (dB) under the player's own shots and hits (the sidechain: the bots' chatter gets out
        /// of the way of the gun while a shot, an impact or a hurt cue plays, then comes back over <see cref="ShotRelease"/>).</summary>
        public static readonly float[] ShotDuckDb = { 0f, 0f, 0f, -5f, -4f, 0f, 0f, 0f };
        /// <summary>How long a player sound holds the sidechain, by kind (seconds): a shot, an impact, hurt feedback.</summary>
        public const float ShotHold = 0.30f, ImpactHold = 0.18f, FeedbackHold = 0.35f;
        /// <summary>Sidechain time constants (seconds): in fast, out smoothly.</summary>
        public const float ShotAttack = 0.015f, ShotRelease = 0.30f;

        /// <summary>Unity voice priority per category (0 = most important).</summary>
        public static readonly int[] Priority = { 16, 32, 48, 128, 200, 24, 220, 20 };

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
        /// <summary>The same bot sound starting again inside this long is dropped (five bots attacking in one frame make one attack
        /// sound, not five stacked; a world impact too, for a spray of pellets).</summary>
        public const float BotRetriggerSeconds = 0.12f, WorldImpactRetriggerSeconds = 0.08f;

        // ---------------------------------------------------------------- the mix pass (2026-10-14): voice limits per group
        /// <summary>The idle loops that are heard at once: the nearest few. The rest fade to silence over <see cref="LoopFadeSeconds"/>.</summary>
        public const int MaxLoops = 3;
        public const float LoopFadeSeconds = 0.6f;
        /// <summary>A bark (a bot's babble) is spoken at this level (before the player's Voices slider). It was 0.9, outside every bus:
        /// four bots barking at once were 14 dB louder than the player's gun.</summary>
        public const float BarkGain = 0.3f;
        /// <summary>The bots' babble is low-passed at this frequency when it is synthesized (its square and saw blips were most of the
        /// 2-5 kHz energy of a bark).</summary>
        public const float BarkLowPassHz = 3000f;
        /// <summary>The harsh-band EQ on the world voices' bot sounds: a broad peaking cut centred here.</summary>
        public const float HarshCenterHz = 3400f, HarshQ = 0.6f;
        /// <summary>The default cut on a bot's one-shot (dB), and the harshest sounds' own (measured 2-5 kHz share of their energy:
        /// drone telegraph 68%, spotlight hurt 59%, floor-care hurt 54%, spotlight attack 33%).</summary>
        public const float BotHarshCutDb = -4f;
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
            // The bots' one-shots, brought to about the same pre-bus level (-21 to -23 dBFS RMS: the shipped WAVs measure from -10.1
            // (drone alert) to -27 (spotlight hurt), the guns' own are at -30 to -34). Measured 2026-10-14 on the WAVs with the ears.
            { "mall-cop-drone/alert", -9f },        // -10.1 RMS: the hottest sound in the game
            { "mall-cop-drone/telegraph", -4f },    // the creator's pick (rotor spool, -15.3 RMS, nothing in 2-5 kHz): the dodge cue, a touch hotter than the old one
            { "mall-cop-drone/hurt", -8f },         // -12.9
            { "mall-cop-drone/attack", -3f },       // -18.5
            { "floor-care-unit/hurt", -5f },        // the creator's pick (tank slosh, -17.1 RMS, low)
            { "floor-care-unit/attack", -2f },      // -19.5
            { "floor-care-unit/telegraph", -2f },   // -19.4
            { "spotlight/attack", 0f },            // the creator's pick (muffled rifle with crackle, -21.4 RMS; the old crack was -18.3 with trim -3)
            { "spotlight/telegraph", -2f },         // -19.2
            { "window-washer-gondola/mine_blast", -3f },   // -16.3
            // The beds. The practice drone's rotor is a soft hum (the creator, 2026-10-14) and sits 6 dB under the rest; the gondola's
            // winch (-17 RMS), the spotlight's (-18.5) and the floor unit's (-20) are far hotter than a bed should be.
            { "mall-cop-drone/idle_loop", -6f },
            { "window-washer-gondola/idle_loop", -4f },
            { "spotlight/idle_loop", -3f },
            { "floor-care-unit/idle_loop", -2f },
            // The Writ's ordinary and weak hits take over from the dropped impact/hit tick (LosesTo), so they come up by the level it gave.
            { "writ/impact", 5f },
            { "writ/impact_weak", 5f },
            // CONCIERGE's chime and sting are UI now (the Feedback bus): under the guns' level (the same as they were on the old bots bus, +1 dB).
            { "concierge/chime", -7f },
            { "concierge/sting", -7f },
        };

        /// <summary>The harsh-band cut (dB at <see cref="HarshCenterHz"/>) of the sounds that are harsher than the default
        /// <see cref="BotHarshCutDb"/> (measured: the share of their energy in 2-5 kHz).</summary>
        static readonly Dictionary<string, float> HarshCutOverrideDb = new Dictionary<string, float>
        {
            { "floor-care-unit/alert", -5f },       // 37%
            // The creator's four picks (2026-10-14) have almost nothing in 2-5 kHz (0-4%): no cut.
            { "mall-cop-drone/telegraph", 0f },
            { "floor-care-unit/hurt", 0f },
            { "spotlight/hurt", 0f },
            { "spotlight/attack", 0f },
        };

        /// <summary>The peaking cut (dB, 0 or negative) a sound's world voice applies around <see cref="HarshCenterHz"/>: the bots'
        /// one-shots only; the player's gun, handling, impacts and feedback keep their sound whole.</summary>
        public static float HarshCutDb(string sound)
        {
            if (CategoryOf(sound) != SkylineSoundCategory.Bots) return 0f;
            return sound != null && HarshCutOverrideDb.TryGetValue(sound, out var db) ? db : BotHarshCutDb;
        }

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
            if (sound.StartsWith("player/", StringComparison.Ordinal) || sound.StartsWith("concierge/", StringComparison.Ordinal))
                return SkylineSoundCategory.Feedback;
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

        /// <summary>Ducking gain (linear) for a category under a teaching line (<paramref name="depth"/>) and under the player's own
        /// shots and hits (<paramref name="shotDepth"/>; the sidechain), the two summed in dB.</summary>
        public static float DuckGain(SkylineSoundCategory c, float depth, float shotDepth)
        {
            double db = DuckDb[(int)c] * Math.Max(0f, depth) + ShotDuckDb[(int)c] * Math.Max(0f, shotDepth);
            return db >= 0.0 ? 1f : (float)Math.Pow(10.0, db / 20.0);
        }

        /// <summary>How long a player sound holds the sidechain (0 for a sound that does not duck the bots).</summary>
        public static float ShotHoldFor(SkylineSoundCategory c)
        {
            switch (c)
            {
                case SkylineSoundCategory.Guns: return ShotHold;
                case SkylineSoundCategory.Impacts: return ImpactHold;
                case SkylineSoundCategory.Feedback: return FeedbackHold;
                default: return 0f;
            }
        }

        /// <summary>The minimum gap before a sound may start again (seconds).</summary>
        public static float RetriggerFor(string sound)
        {
            var c = CategoryOf(sound);
            if (c == SkylineSoundCategory.Bots) return BotRetriggerSeconds;
            if (sound == "impact/world") return WorldImpactRetriggerSeconds;
            return RetriggerSeconds;
        }

        // ---------------------------------------------------------------- the voice gate
        /// <summary>The gate group of a bot one-shot ("mall-cop-drone/alert" gives "bot.alert"), or null for a sound the gate leaves alone
        /// (the player's, the impacts, the beds).</summary>
        public static string GroupOf(string sound)
        {
            if (string.IsNullOrEmpty(sound) || CategoryOf(sound) != SkylineSoundCategory.Bots) return null;
            int slash = sound.LastIndexOf('/');
            string kind = slash >= 0 ? sound.Substring(slash + 1) : sound;
            switch (kind)
            {
                case "alert": return "bot.alert";
                case "telegraph": return "bot.telegraph";
                case "attack": return "bot.attack";
                case "hurt": return "bot.hurt";
                case "plate_shed": case "wreck": case "mine_blast": case "acid_sweep": return "bot.break";
                default: return "bot.minor";
            }
        }

        /// <summary>Sets the gate's limits (the mix pass, 2026-10-14; the numbers are <see cref="SkylineMixRules"/>, in the pure
        /// runtime so the unit tests run a fight through them).</summary>
        public static void ConfigureGate(SkylineVoiceGate g) => SkylineMixRules.Configure(g);
    }
}
