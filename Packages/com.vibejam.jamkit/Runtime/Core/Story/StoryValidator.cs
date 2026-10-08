using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace JamKit.Narrative
{
    /// <summary>The ids a story may use, collected from the game's data (backgrounds, characters...).</summary>
    public sealed class StoryIds
    {
        public readonly HashSet<string> backgrounds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> cgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Music, ambience and sound effects share one id list (the Audio Registry plus generated placeholders).</summary>
        public readonly HashSet<string> sounds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> endings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> hotspotScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> palette = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Your own tags (handled by your code through TagReceived): not reported as unknown.</summary>
        public readonly HashSet<string> extraTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Character id to its expression names (empty = any expression is fine).</summary>
        public readonly Dictionary<string, HashSet<string>> characters = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Names that may start a line ("Iris: ..."): character ids and display names.</summary>
        public readonly HashSet<string> speakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Knots started by data or code (hotspots, the start knot): must exist, count as reachable.</summary>
        public readonly List<string> dataKnots = new List<string>();
        public string itemsList = "Items";
        /// <summary>Off: sound ids aren't checked (no registry available).</summary>
        public bool checkSounds = true;

        public void AddCharacter(string id, string displayName, IEnumerable<string> expressions)
        {
            if (string.IsNullOrEmpty(id)) return;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (expressions != null) foreach (var e in expressions) if (!string.IsNullOrEmpty(e)) set.Add(e);
            characters[id] = set;
            speakers.Add(id);
            if (!string.IsNullOrEmpty(displayName)) speakers.Add(displayName);
        }
    }

    public struct StoryProblem
    {
        public bool error;
        public string file;
        public int line;
        public string message;

        public override string ToString() => (error ? "ERROR " : "warning ") + (string.IsNullOrEmpty(file) ? "" : file + (line > 0 ? ":" + line : "") + ": ") + message;
    }

    public sealed class StoryProblems
    {
        public readonly List<StoryProblem> all = new List<StoryProblem>();

        public int ErrorCount { get { int n = 0; foreach (var p in all) if (p.error) n++; return n; } }
        public int WarningCount => all.Count - ErrorCount;
        public bool Ok => ErrorCount == 0;

        public void Error(string file, int line, string message) => all.Add(new StoryProblem { error = true, file = file, line = line, message = message });
        public void Warning(string file, int line, string message) => all.Add(new StoryProblem { error = false, file = file, line = line, message = message });

        public List<string> Errors() { var l = new List<string>(); foreach (var p in all) if (p.error) l.Add(p.ToString()); return l; }
        public List<string> Warnings() { var l = new List<string>(); foreach (var p in all) if (!p.error) l.Add(p.ToString()); return l; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var p in all) if (p.error) sb.AppendLine(p.ToString());
            foreach (var p in all) if (!p.error) sb.AppendLine(p.ToString());
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>A hotspot as the validator sees it (the Unity asset, flattened).</summary>
    public sealed class HotspotInfo
    {
        public string id = "";
        public string label = "";
        public HotspotShape shape;
        public string knot = "";
        public string showIf = "";
        public readonly List<KeyValuePair<string, string>> itemKnots = new List<KeyValuePair<string, string>>();
    }

    public sealed class HotspotSceneInfo
    {
        public string id = "";
        public string background = "";
        public string fallbackItemKnot = "";
        public readonly List<HotspotInfo> hotspots = new List<HotspotInfo>();
        /// <summary>The smoke test's path through the scene: "hotspot" or "item>hotspot", in order.</summary>
        public readonly List<string> walkthrough = new List<string>();
    }

    /// <summary>
    /// Checks a story before anyone plays it: every tag is known and its ids exist in the game's data,
    /// expressions belong to their character, palette colours exist, diverts go somewhere, the knots
    /// data starts exist, hotspot scenes are well formed, endings in ink match the endings list, and
    /// knots nothing reaches are reported. Errors fail the EditMode tests and the build check.
    /// </summary>
    public static class StoryValidator
    {
        static readonly Regex MarkupRx = new Regex(@"<(/?)([A-Za-z]+)(=([^>]*))?>", RegexOptions.Compiled);

        public static StoryProblems Validate(InkScan scan, StoryIds ids, IList<HotspotSceneInfo> hotspotScenes = null)
        {
            var problems = new StoryProblems();
            if (scan == null) { problems.Error("", 0, "no ink to check"); return problems; }
            ids = ids ?? new StoryIds();

            var endingsInInk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in scan.tags)
            {
                CheckTag(t.text, t.dynamic, ids, problems, t.file, t.line);
                var cmd = StoryCommand.Parse(t.text);
                if (cmd.kind == StoryCommandKind.Ending && !string.IsNullOrEmpty(cmd.id)) endingsInInk.Add(cmd.id);
            }
            foreach (var t in scan.texts) CheckText(t.text, ids, problems, t.file, t.line);

            // Diverts go to real places.
            foreach (var d in scan.diverts)
                if (!scan.PathExists(d.target, d.knot))
                    problems.Error(d.file, d.line, "\"-> " + d.target + "\" goes nowhere: there's no knot, stitch or label called \"" + d.target + "\"" + Near(d.target, KnotNames(scan)));

            // Knots that data starts.
            foreach (var k in ids.dataKnots)
                if (!string.IsNullOrEmpty(k) && !scan.PathExists(k, null))
                    problems.Error("", 0, "the game data starts knot \"" + k + "\", which isn't in the ink" + Near(k, KnotNames(scan)));

            // Hotspot scenes.
            if (hotspotScenes != null)
                foreach (var s in hotspotScenes) CheckHotspotScene(s, scan, ids, problems);

            // Endings: in ink and in the list.
            if (endingsInInk.Count == 0) problems.Warning("", 0, "no #ending tag anywhere: the endings gallery will stay empty");
            foreach (var e in ids.endings)
                if (!endingsInInk.Contains(e)) problems.Warning("", 0, "ending \"" + e + "\" is in the endings list but no #ending:" + e + " tag reaches it");

            // Items: the ink LIST and the item data agree.
            if (scan.lists.TryGetValue(ids.itemsList, out var listItems))
            {
                foreach (var it in listItems)
                    if (!ids.items.Contains(it)) problems.Error("", 0, "LIST " + ids.itemsList + " has \"" + it + "\" but there's no item with that id in the story config (it needs a name and an icon)");
                foreach (var it in ids.items)
                    if (!listItems.Contains(it)) problems.Warning("", 0, "item \"" + it + "\" is in the story config but not in ink's LIST " + ids.itemsList);
            }
            else if (ids.items.Count > 0)
                problems.Warning("", 0, "the story config has items but the ink has no LIST " + ids.itemsList + " = ... (the inventory stays empty)");

            // Unreachable knots.
            var roots = new List<string>(ids.dataKnots);
            var reach = scan.Reachable(roots);
            foreach (var k in scan.knots)
                if (!k.isFunction && !reach.Contains(k.name))
                    problems.Warning(k.file, k.line, "knot \"" + k.name + "\" is never reached (no divert, hotspot or start points to it)");
            return problems;
        }

        static IEnumerable<string> KnotNames(InkScan scan)
        {
            foreach (var k in scan.knots) yield return k.name;
        }

        /// <summary>Checks one tag (without the #). Used for every tag in the ink, and by tests on single strings.</summary>
        public static void CheckTag(string tagText, bool dynamic, StoryIds ids, StoryProblems problems, string file = "", int line = 0)
        {
            ids = ids ?? new StoryIds();
            var cmd = StoryCommand.Parse(tagText);
            if (cmd.kind == StoryCommandKind.None) return;
            if (cmd.kind == StoryCommandKind.Unknown)
            {
                if (ids.extraTags.Contains(cmd.key)) return;
                string near = StoryTags.Suggest(cmd.key);
                problems.Error(file, line, "unknown tag #" + cmd.key + (near != null ? " (did you mean #" + near + "?)" : "")
                    + ". Add it to the story config's Extra Tags if your own code handles it.");
                return;
            }
            if (dynamic) return; // {logic} inside: the value is only known when it runs
            if (cmd.error != null)
            {
                var info = StoryTags.Find(cmd.key);
                problems.Error(file, line, "#" + tagText + ": " + cmd.error + (info != null ? " (example: " + info.example + ")" : ""));
            }
            switch (cmd.kind)
            {
                case StoryCommandKind.Background:
                case StoryCommandKind.Scene:
                    Need(ids.backgrounds, cmd.id, "background", tagText, problems, file, line);
                    break;
                case StoryCommandKind.Cg:
                    Need(ids.cgs, cmd.id, "CG", tagText, problems, file, line);
                    break;
                case StoryCommandKind.Show:
                case StoryCommandKind.Hide:
                case StoryCommandKind.Move:
                    if (string.IsNullOrEmpty(cmd.id)) break;
                    if (!ids.characters.TryGetValue(cmd.id, out var exprs))
                    {
                        problems.Error(file, line, "#" + tagText + ": no character with id \"" + cmd.id + "\"" + Near(cmd.id, ids.characters.Keys));
                        break;
                    }
                    if (!string.IsNullOrEmpty(cmd.expression) && exprs.Count > 0 && !exprs.Contains(cmd.expression))
                        problems.Error(file, line, "#" + tagText + ": " + cmd.id + " has no expression \"" + cmd.expression + "\" (has: " + string.Join(", ", exprs) + ")");
                    break;
                case StoryCommandKind.Music:
                case StoryCommandKind.Ambience:
                case StoryCommandKind.Sfx:
                    if (ids.checkSounds) Need(ids.sounds, cmd.id, "sound", tagText, problems, file, line);
                    break;
                case StoryCommandKind.Ending:
                    Need(ids.endings, cmd.id, "ending (add it to the story config's Endings)", tagText, problems, file, line);
                    break;
                case StoryCommandKind.Investigate:
                    Need(ids.hotspotScenes, cmd.id, "hotspot scene", tagText, problems, file, line);
                    break;
                case StoryCommandKind.Flash:
                case StoryCommandKind.Fade:
                    if (!string.IsNullOrEmpty(cmd.text) && !IsColourWord(cmd.text) && !ids.palette.Contains(cmd.text))
                        problems.Error(file, line, "#" + tagText + ": \"" + cmd.text + "\" isn't a colour (white, black, red... or a palette name)");
                    break;
            }
        }

        static void Need(HashSet<string> set, string id, string what, string tagText, StoryProblems problems, string file, int line)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!set.Contains(id)) problems.Error(file, line, "#" + tagText + ": no " + what + " with id \"" + id + "\"" + Near(id, set));
        }

        public static readonly string[] ColourWords = { "white", "black", "red", "green", "blue", "yellow", "orange", "purple", "gray", "grey", "pink", "cyan" };

        public static bool IsColourWord(string w)
        {
            foreach (var c in ColourWords) if (string.Equals(c, w, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Inline markup in a line: palette names, pause and speed numbers; a speaker prefix that looks misspelt.</summary>
        public static void CheckText(string text, StoryIds ids, StoryProblems problems, string file = "", int line = 0)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text.IndexOf('<') >= 0)
            {
                foreach (Match m in MarkupRx.Matches(text))
                {
                    if (m.Groups[1].Value == "/") continue;
                    string name = m.Groups[2].Value.ToLowerInvariant();
                    string value = m.Groups[4].Value.Trim().Trim('"');
                    if (name == "c" && !ids.palette.Contains(value) && !IsColourWord(value))
                        problems.Error(file, line, "<c=" + value + ">: no palette colour called \"" + value + "\"" + Near(value, ids.palette));
                    if ((name == "pause" || name == "p" || name == "speed") && value.Length > 0 && !StoryCommand.TryNumber(value, out _))
                        problems.Error(file, line, "<" + name + "=" + value + ">: \"" + value + "\" isn't a number");
                }
            }
            if (ids.speakers.Count > 0 && StoryLine.LooksLikeSpeakerPrefix(text, out string candidate) && !ids.speakers.Contains(candidate))
            {
                foreach (var s in ids.speakers)
                    if (Distance(candidate.ToLowerInvariant(), s.ToLowerInvariant()) <= 2 && s.Length >= 3)
                    {
                        problems.Warning(file, line, "\"" + candidate + ":\" isn't a character (did you mean " + s + "?): the line shows as narration");
                        break;
                    }
            }
        }

        static void CheckHotspotScene(HotspotSceneInfo s, InkScan scan, StoryIds ids, StoryProblems problems)
        {
            string where = "hotspot scene " + s.id;
            if (string.IsNullOrEmpty(s.id)) problems.Error(where, 0, "a hotspot scene has no id");
            if (!string.IsNullOrEmpty(s.background) && !ids.backgrounds.Contains(s.background))
                problems.Error(where, 0, "background \"" + s.background + "\" doesn't exist" + Near(s.background, ids.backgrounds));
            if (s.hotspots.Count == 0) problems.Error(where, 0, "has no hotspots");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in s.hotspots)
            {
                string hw = where + " / " + (string.IsNullOrEmpty(h.id) ? "(no id)" : h.id);
                if (string.IsNullOrEmpty(h.id)) problems.Error(hw, 0, "a hotspot has no id");
                else if (!seen.Add(h.id)) problems.Error(hw, 0, "two hotspots have the id \"" + h.id + "\"");
                if (string.IsNullOrEmpty(h.label)) problems.Warning(hw, 0, "no label: players see nothing when they point at it");
                string shape = h.shape.Problem();
                if (shape != null) problems.Error(hw, 0, shape);
                if (string.IsNullOrEmpty(h.knot)) problems.Error(hw, 0, "no knot to run when it's clicked");
                else if (!scan.PathExists(h.knot, null)) problems.Error(hw, 0, "knot \"" + h.knot + "\" isn't in the ink" + Near(h.knot, KnotNames(scan)));
                string cond = StoryCondition.Check(h.showIf);
                if (cond != null) problems.Error(hw, 0, "Show If \"" + h.showIf + "\": " + cond);
                foreach (var name in StoryCondition.Names(h.showIf)) CheckConditionName(name, scan, ids, problems, hw);
                foreach (var use in h.itemKnots)
                {
                    if (!ids.items.Contains(use.Key)) problems.Error(hw, 0, "uses item \"" + use.Key + "\", which isn't in the story config's items" + Near(use.Key, ids.items));
                    if (string.IsNullOrEmpty(use.Value) || !scan.PathExists(use.Value, null)) problems.Error(hw, 0, "item knot \"" + use.Value + "\" isn't in the ink");
                }
            }
            if (!string.IsNullOrEmpty(s.fallbackItemKnot) && !scan.PathExists(s.fallbackItemKnot, null))
                problems.Error(where, 0, "fallback item knot \"" + s.fallbackItemKnot + "\" isn't in the ink");
            foreach (var step in s.walkthrough)
            {
                string spot = step, item = null;
                int gt = step.IndexOf('>');
                if (gt >= 0) { item = step.Substring(0, gt).Trim(); spot = step.Substring(gt + 1).Trim(); }
                bool found = false;
                foreach (var h in s.hotspots) if (string.Equals(h.id, spot, StringComparison.OrdinalIgnoreCase)) found = true;
                if (!found) problems.Error(where, 0, "walkthrough step \"" + step + "\": no hotspot \"" + spot + "\"");
                if (item != null && !ids.items.Contains(item)) problems.Error(where, 0, "walkthrough step \"" + step + "\": no item \"" + item + "\"");
            }
            if (s.walkthrough.Count == 0) problems.Warning(where, 0, "no walkthrough: the smoke test can't play through it");
        }

        static void CheckConditionName(string name, InkScan scan, StoryIds ids, StoryProblems problems, string where)
        {
            int colon = name.IndexOf(':');
            string prefix = colon > 0 ? name.Substring(0, colon).ToLowerInvariant() : "";
            string rest = colon > 0 ? name.Substring(colon + 1) : name;
            switch (prefix)
            {
                case "item":
                    if (!ids.items.Contains(rest)) problems.Error(where, 0, "condition names item \"" + rest + "\", which doesn't exist");
                    break;
                case "seen":
                    if (!scan.PathExists(rest, null)) problems.Error(where, 0, "condition names knot \"" + rest + "\", which isn't in the ink");
                    break;
                case "ending":
                    if (!ids.endings.Contains(rest)) problems.Error(where, 0, "condition names ending \"" + rest + "\", which isn't in the endings list");
                    break;
                case "var":
                    if (!scan.variables.Contains(rest)) problems.Error(where, 0, "condition names ink variable \"" + rest + "\", which isn't declared (VAR " + rest + " = ...)");
                    break;
                case "":
                    if (!scan.variables.Contains(rest) && !ids.items.Contains(rest) && !IsBuiltinFact(rest))
                        problems.Warning(where, 0, "condition name \"" + rest + "\" isn't an ink VAR or an item: it reads a flag (flag:" + rest + ")");
                    break;
            }
        }

        /// <summary>Names conditions always know (from memory).</summary>
        public static readonly string[] BuiltinFacts = { "playthrough", "endings_seen", "last_ending", "times_quit", "loads", "rewinds", "finished" };

        static bool IsBuiltinFact(string name)
        {
            foreach (var b in BuiltinFacts) if (b == name) return true;
            return false;
        }

        static string Near(string word, IEnumerable<string> options)
        {
            if (string.IsNullOrEmpty(word) || options == null) return "";
            string best = null;
            int bestD = int.MaxValue;
            foreach (var o in options)
            {
                if (string.IsNullOrEmpty(o)) continue;
                int d = Distance(word.ToLowerInvariant(), o.ToLowerInvariant());
                if (d < bestD) { bestD = d; best = o; }
            }
            return best != null && bestD <= Math.Max(2, word.Length / 3) ? " (did you mean \"" + best + "\"?)" : "";
        }

        public static int Distance(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                var tmp = prev; prev = cur; cur = tmp;
            }
            return prev[b.Length];
        }
    }
}
