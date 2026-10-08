using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JamKit.Dialogue
{
    /// <summary>
    /// The ink knot names a game looks up at its story moments (a boss appears, changes phase, falls;
    /// the player dies, rests, starts a run), most specific first. A game plays the first knot the
    /// ink file has (<see cref="Resolve"/>); when none exists nothing plays, silently. So a writer
    /// adds lines by adding knots, and a missing knot is never an error.
    /// <code>
    /// boss_&lt;id&gt;_intro           -> boss_intro
    /// boss_&lt;id&gt;_phase_&lt;n&gt;       -> boss_&lt;id&gt;_phase -> boss_phase      (n = the phase that starts: 2, 3...)
    /// boss_&lt;id&gt;_defeat          -> boss_defeat
    /// death_&lt;killer&gt;            -> death_floor_&lt;n&gt; -> death           (death screen line)
    /// rest_floor_&lt;n&gt;            -> rest                                 (companion on Rest rooms)
    /// run_start_&lt;run&gt;           -> run_start                            (run = 1 on the first run)
    /// &lt;kind&gt;_floor_&lt;n&gt;          -> &lt;kind&gt;                               (an encounter room: forge, altar, wager, trial_board, trader)
    /// companion_bark_&lt;boss&gt;_&lt;what&gt; -> companion_bark_&lt;what&gt;            (a short in-battle line; never a dialogue)
    /// </code>
    /// Ids are lowercase with underscores (<see cref="Id"/>): "The Last Light" is "the_last_light".
    /// </summary>
    public static class StoryHooks
    {
        /// <summary>A display name or id as an ink-safe id: lowercase letters, digits and single underscores.</summary>
        public static string Id(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            bool gap = false;
            foreach (char raw in name)
            {
                char c = char.ToLowerInvariant(raw);
                bool keep = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (keep)
                {
                    if (gap && sb.Length > 0) sb.Append('_');
                    gap = false;
                    sb.Append(c);
                }
                else if (c == '_' || c == ' ' || c == '-' || c == '.' || c == '/') gap = true;
                // anything else (apostrophes, accents, punctuation) is dropped
            }
            return sb.ToString();
        }

        static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

        public static string[] BossIntro(string bossId)
        {
            string id = Id(bossId);
            return id.Length > 0 ? new[] { "boss_" + id + "_intro", "boss_intro" } : new[] { "boss_intro" };
        }

        /// <summary><paramref name="phaseNumber"/> is the phase that just started, counted from 1 (so 2 is the first change).</summary>
        public static string[] BossPhase(string bossId, int phaseNumber)
        {
            string id = Id(bossId);
            if (id.Length == 0) return new[] { "boss_phase" };
            return new[] { "boss_" + id + "_phase_" + N(phaseNumber), "boss_" + id + "_phase", "boss_phase" };
        }

        public static string[] BossDefeat(string bossId)
        {
            string id = Id(bossId);
            return id.Length > 0 ? new[] { "boss_" + id + "_defeat", "boss_defeat" } : new[] { "boss_defeat" };
        }

        /// <summary>The death screen's line. <paramref name="killerId"/>: the boss's id in a boss fight, else
        /// the room kind ("battle", "elite"); <paramref name="floor"/> counted from 1.</summary>
        public static string[] Death(string killerId, int floor)
        {
            var list = new List<string>(3);
            string killer = Id(killerId);
            if (killer.Length > 0) list.Add("death_" + killer);
            if (floor > 0) list.Add("death_floor_" + N(floor));
            list.Add("death");
            return list.ToArray();
        }

        public static string[] Rest(int floor) =>
            floor > 0 ? new[] { "rest_floor_" + N(floor), "rest" } : new[] { "rest" };

        /// <summary><paramref name="runNumber"/>: 1 on the very first run (the save's runs started).</summary>
        public static string[] RunStart(int runNumber) =>
            runNumber > 0 ? new[] { "run_start_" + N(runNumber), "run_start" } : new[] { "run_start" };

        /// <summary>An encounter room's line before its choice: <c>forge_floor_2</c>, then <c>forge</c>.</summary>
        public static string[] Encounter(string kind, int floor)
        {
            string k = Id(kind);
            if (k.Length == 0) return new string[0];
            return floor > 0 ? new[] { k + "_floor_" + N(floor), k } : new[] { k };
        }

        /// <summary>
        /// A companion bark: <paramref name="what"/> is the context ("spiral", "ring", "guard"...), and with a
        /// boss id the boss-specific knot is tried first: <c>companion_bark_gatekeeper_spiral</c>, then
        /// <c>companion_bark_spiral</c>. No general fallback: an unwritten context stays silent.
        /// </summary>
        public static string[] CompanionBark(string what, string bossId = null)
        {
            string w = Id(what);
            if (w.Length == 0) return new string[0];
            string b = Id(bossId);
            return b.Length > 0
                ? new[] { "companion_bark_" + b + "_" + w, "companion_bark_" + w }
                : new[] { "companion_bark_" + w };
        }

        /// <summary>The first candidate the story has, or null (then play nothing). A throwing
        /// <paramref name="exists"/> counts as "no".</summary>
        public static string Resolve(IReadOnlyList<string> candidates, Func<string, bool> exists)
        {
            if (candidates == null || exists == null) return null;
            for (int i = 0; i < candidates.Count; i++)
            {
                string c = candidates[i];
                if (string.IsNullOrEmpty(c)) continue;
                bool has;
                try { has = exists(c); }
                catch (Exception) { has = false; }
                if (has) return c;
            }
            return null;
        }
    }
}
