using System;

namespace JamKit.Skyline
{
    /// <summary>What earns a piece of comic lettering (docs/SKYLINE-HUD.md "Comic lettering"). Hits that are none of these
    /// ask for nothing.</summary>
    public enum LetterEvent { Kill, WeakPoint, WritSpotBreak, BreacherShove }

    /// <summary>The two display faces the creator chose (docs/SKYLINE-FLOOR-REPOSSESS.md section 10), used interchangeably.
    /// Both are SIL Open Font License (Google Fonts); C2 loads them.</summary>
    public enum LetterFont { LuckiestGuy, Bangers }

    /// <summary>One piece of lettering to draw: the word, its face, size class, tilt and how long it lives.</summary>
    public sealed class LetterRequest
    {
        public string text;
        public string botId, gunId;
        /// <summary>Total life in seconds: the glyph wipe, the hold, the fade (see <see cref="ComicLettering.SecondsFor"/>).</summary>
        public float seconds;
        /// <summary>Size class 1..3 (kill 3, Writ spot break 3, big shove 2, weak point 1).</summary>
        public int size;
        public LetterFont font;
        /// <summary>Tilt in degrees, within +-<see cref="ComicLettering.MaxTiltDeg"/>, seeded.</summary>
        public float tiltDeg;
        public LetterEvent evt;
    }

    /// <summary>Comic lettering core (docs/SKYLINE-PLAN-FLOOR1.md Task C1): which word, face, size and tilt, and whether to draw
    /// at all. Pure; the Unity side (C2) only draws what <see cref="Pick"/> returns. The words are DRAFTS kept as data
    /// (<see cref="Words"/>) for the creator to rewrite (docs/SKYLINE-DRAFT-LINES.md "Comic lettering").</summary>
    public sealed class ComicLettering
    {
        public const int MaxOnScreen = 2;
        public const float MinGap = 0.35f;
        /// <summary>A Breacher shove counts as "big" at or above this knockback speed, in m/s (data).</summary>
        public const float BigShoveKnockback = 8f;
        public const float MaxTiltDeg = 8f;
        /// <summary>Per-glyph wipe, the hold, the fade (docs/SKYLINE-PLAN-FLOOR1.md Task C2).</summary>
        public const float GlyphSeconds = 0.04f, HoldSeconds = 0.5f, FadeSeconds = 0.3f;

        static readonly string[] GenericKill = { "POW!", "BAM!", "KRAK!" };
        static readonly string[] GenericWeak = { "PING!", "TICK!" };
        static readonly string[] GenericSpot = { "SNAP!", "CRACK-OPEN!" };
        static readonly string[] GenericShove = { "THUD!", "WHAM!" };

        static readonly string[] DroneKill = { "KZZT!", "BZZT-CRK!", "ZAP!" };
        static readonly string[] FloorCareKill = { "SPLORCH!", "GLORP!", "SPLAT!" };
        static readonly string[] GondolaKill = { "CLANG!", "KA-SPLASH!" };
        static readonly string[] SpotlightKill = { "KLAK!", "FZZT-POP!" };
        static readonly string[] WeakWords = { "CRACK!", "TINK!" };
        static readonly string[] WritSpotWords = { "OBJECTION!", "SUSTAINED!", "OVERRULED!" };
        static readonly string[] ShoveWords = { "WHUMP!", "BWOOM!" };

        ulong _state;
        float _lastAt = float.NegativeInfinity;
        LetterFont _lastFont;
        bool _hasFont;
        // The last word per "event|bot or gun" set, so the same word is not picked twice in a row for one bot type.
        readonly System.Collections.Generic.Dictionary<string, string> _lastByKey =
            new System.Collections.Generic.Dictionary<string, string>();

        public ComicLettering(ulong seed)
        {
            _state = seed ^ 0x9E3779B97F4A7C15UL;
            Next(); Next();   // spin up: a small seed (0, 1, 2...) must not give a correlated first draw
        }

        /// <summary>True when a Breacher shove with this knockback speed deserves lettering.</summary>
        public static bool IsBigShove(float knockbackSpeed) => knockbackSpeed >= BigShoveKnockback;

        /// <summary>Size class of an event: kill 3, Writ spot break 3, Breacher shove 2, weak point 1.</summary>
        public static int SizeOf(LetterEvent e)
        {
            switch (e)
            {
                case LetterEvent.Kill: return 3;
                case LetterEvent.WritSpotBreak: return 3;
                case LetterEvent.BreacherShove: return 2;
                default: return 1;
            }
        }

        /// <summary>Total life of a word: its glyph wipes, the hold, the fade.</summary>
        public static float SecondsFor(string text) => (text == null ? 0 : text.Length) * GlyphSeconds + HoldSeconds + FadeSeconds;

        /// <summary>The draft word set for an event: a kill by bot type, a weak point by bot type, a Writ spot break and a
        /// Breacher shove by gun. Unknown or null ids get a generic set. Never null or empty.</summary>
        public static string[] Words(string botId, string gunId, LetterEvent e)
        {
            switch (e)
            {
                case LetterEvent.Kill:
                    if (botId == SkylineEnemies.DroneId) return DroneKill;
                    if (botId == SkylineEnemies.FloorCareId) return FloorCareKill;
                    if (botId == SkylineEnemies.GondolaId) return GondolaKill;
                    if (botId == SkylineEnemies.SpotlightId) return SpotlightKill;
                    return GenericKill;
                case LetterEvent.WeakPoint:
                    return IsKnownBot(botId) ? WeakWords : GenericWeak;
                case LetterEvent.WritSpotBreak:
                    return gunId == SkylineGuns.WritId ? WritSpotWords : GenericSpot;
                case LetterEvent.BreacherShove:
                    return gunId == SkylineGuns.BreacherId ? ShoveWords : GenericShove;
                default:
                    return GenericKill;
            }
        }

        static bool IsKnownBot(string botId) =>
            botId == SkylineEnemies.DroneId || botId == SkylineEnemies.FloorCareId ||
            botId == SkylineEnemies.GondolaId || botId == SkylineEnemies.SpotlightId;

        /// <summary>The lettering for this event, or null when over the cap (<paramref name="onScreenCount"/> already at
        /// <see cref="MaxOnScreen"/>) or inside <see cref="MinGap"/> of the previous accepted request. A refused call changes
        /// nothing, so it never shifts the seeded sequence. The face alternates against the last one picked whenever another
        /// piece is still showing, so two on screen never share a face; with none showing it is chosen from the seed. The
        /// same word is never picked twice in a row for one set (bot type or gun).</summary>
        public LetterRequest Pick(LetterEvent e, string botId, string gunId, float now, float onScreenCount)
        {
            if (float.IsNaN(now) || float.IsNaN(onScreenCount)) return null;
            if (onScreenCount >= MaxOnScreen) return null;
            if (now - _lastAt < MinGap) return null;

            string[] set = Words(botId, gunId, e);
            string key = (int)e + "|" + (e == LetterEvent.Kill || e == LetterEvent.WeakPoint ? botId : gunId);
            _lastByKey.TryGetValue(key, out string prev);
            string word;
            if (set.Length == 1) word = set[0];
            else
            {
                // Pick among the words that differ from the last one for this set.
                int count = prev != null && Array.IndexOf(set, prev) >= 0 ? set.Length - 1 : set.Length;
                int k = (int)(Next() % (ulong)count);
                word = null;
                for (int i = 0, seen = 0; i < set.Length; i++)
                {
                    if (set[i] == prev) continue;
                    if (seen++ == k) { word = set[i]; break; }
                }
            }

            LetterFont font;
            if (onScreenCount >= 1f && _hasFont) font = _lastFont == LetterFont.LuckiestGuy ? LetterFont.Bangers : LetterFont.LuckiestGuy;
            else font = (Next() & 1UL) == 0UL ? LetterFont.LuckiestGuy : LetterFont.Bangers;
            float tilt = (float)((Next() % 2001UL) / 1000.0 - 1.0) * MaxTiltDeg;

            _lastAt = now;
            _lastFont = font; _hasFont = true;
            _lastByKey[key] = word;
            return new LetterRequest
            {
                text = word, botId = botId, gunId = gunId, evt = e,
                seconds = SecondsFor(word), size = SizeOf(e), font = font, tiltDeg = tilt,
            };
        }

        /// <summary>Forget the gap, the last face and the last words (a wreck restart); the seeded sequence continues.</summary>
        public void Reset()
        {
            _lastAt = float.NegativeInfinity;
            _hasFont = false;
            _lastByKey.Clear();
        }

        // splitmix64: tiny, deterministic, no System.Random (whose sequence is not guaranteed across runtimes).
        ulong Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
