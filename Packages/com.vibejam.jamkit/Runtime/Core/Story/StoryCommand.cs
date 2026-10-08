using System;
using System.Collections.Generic;
using System.Globalization;
using JamKit.Dialogue;

namespace JamKit.Narrative
{
    public enum StoryCommandKind
    {
        None, Unknown,
        // Stage
        Background, Scene, Show, Hide, HideAll, Move, Cg, CgOff,
        // Camera and effects
        Shake, Flash, Fade, Zoom, Blur, Filter, Glitch,
        // Sound
        Music, StopMusic, Ambience, StopAmbience, Sfx,
        // Text
        Nvl, Adv, Clear, Thought, Chapter, Wait, Auto,
        // Story structure and meta
        Ending, Investigate, Leave, Meta,
        // The shell's own tags (handled by DialogueService; listed so the validator knows them)
        Speaker, Portrait, Event,
    }

    public enum StageTransition { Default, Cut, Fade, Flash, Black, Slide }

    /// <summary>What an argument names, for the validator.</summary>
    public enum StoryIdKind { None, Background, Character, Cg, Sound, Ending, Hotspots, Meta }

    /// <summary>
    /// One ink tag of the story template, parsed: "#bg:station fade 2" is a Background command with id
    /// "station", a Fade transition and 2 seconds. Words after the colon can come in any order where
    /// that's unambiguous (see <see cref="StoryTags"/> for every tag). <see cref="error"/> is set when
    /// the tag is known but its arguments aren't understood (the validator reports it with the line).
    /// </summary>
    public struct StoryCommand
    {
        public StoryCommandKind kind;
        /// <summary>The tag's key as written, lower case ("bg").</summary>
        public string key;
        /// <summary>Main id: background, character, CG, sound, ending, hotspot scene or meta action.</summary>
        public string id;
        public string expression;
        /// <summary>Free text: a chapter title, a meta action's text, a fade or flash colour.</summary>
        public string text;
        public StageTransition transition;
        public bool hasX;
        /// <summary>Horizontal place on the stage, 0 (left edge) .. 1 (right edge).</summary>
        public float x;
        public bool hasY;
        public float y;
        public bool hasSeconds;
        public float seconds;
        public bool hasAmount;
        /// <summary>Shake strength, zoom scale, blur pixels, filter or glitch strength.</summary>
        public float amount;
        /// <summary>"flip" (mirrored) or "noflip" was given.</summary>
        public bool hasFlip;
        public bool flip;
        /// <summary>Set when the arguments don't parse (the command is still usable with defaults).</summary>
        public string error;

        public float SecondsOr(float fallback) => hasSeconds ? seconds : fallback;
        public float AmountOr(float fallback) => hasAmount ? amount : fallback;

        public override string ToString() => kind + (string.IsNullOrEmpty(id) ? "" : " " + id) + (error != null ? " (" + error + ")" : "");

        // ---------- Parsing ----------

        public static StoryCommand Parse(string rawTag)
        {
            if (!DialogueTag.TryParse(rawTag, out var tag)) return new StoryCommand { kind = StoryCommandKind.None, key = "" };
            return Parse(tag);
        }

        public static StoryCommand Parse(DialogueTag tag)
        {
            var c = new StoryCommand { key = tag.key, id = "", expression = "", text = "" };
            var info = StoryTags.Find(tag.key);
            if (info == null)
            {
                c.kind = StoryCommandKind.Unknown;
                c.error = "unknown tag";
                return c;
            }
            c.kind = info.kind;
            string value = tag.value ?? "";
            string[] words = value.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            switch (c.kind)
            {
                case StoryCommandKind.Background:
                case StoryCommandKind.Scene:
                case StoryCommandKind.Cg:
                    ParseIdTransition(ref c, words, c.kind == StoryCommandKind.Cg);
                    break;
                case StoryCommandKind.Show:
                    ParseShow(ref c, words);
                    break;
                case StoryCommandKind.Hide:
                    ParseHide(ref c, words);
                    break;
                case StoryCommandKind.HideAll:
                    ParseNumbers(ref c, words, 0, true, false);
                    break;
                case StoryCommandKind.Move:
                    ParseMove(ref c, words);
                    break;
                case StoryCommandKind.CgOff:
                    ParseIdTransition(ref c, PrependId(words, ""), false);
                    c.id = "";
                    break;
                case StoryCommandKind.Shake:
                    ParseNumbers(ref c, words, 0, true, true);
                    if (c.hasAmount) c.amount = Clamp(c.amount, 0f, 1f);
                    break;
                case StoryCommandKind.Glitch:
                    ParseNumbers(ref c, words, 0, true, true);
                    if (c.hasAmount) c.amount = Clamp(c.amount, 0f, 1f);
                    break;
                case StoryCommandKind.Flash:
                    ParseColourAndSeconds(ref c, words, 0);
                    break;
                case StoryCommandKind.Fade:
                    ParseFade(ref c, words);
                    break;
                case StoryCommandKind.Zoom:
                    ParseZoom(ref c, words);
                    break;
                case StoryCommandKind.Blur:
                    ParseNumbers(ref c, words, 0, true, true);
                    if (!c.hasAmount && c.error == null) c.amount = 6f;
                    c.hasAmount = true;
                    if (c.amount < 0f) c.amount = 0f;
                    break;
                case StoryCommandKind.Filter:
                    ParseFilter(ref c, words);
                    break;
                case StoryCommandKind.Music:
                case StoryCommandKind.Ambience:
                    if (words.Length == 0) { c.error = "needs a sound id (or stop)"; break; }
                    c.id = words[0];
                    if (IsStopWord(c.id)) { c.kind = c.kind == StoryCommandKind.Music ? StoryCommandKind.StopMusic : StoryCommandKind.StopAmbience; c.id = ""; }
                    ParseNumbers(ref c, words, 1, false, true);
                    break;
                case StoryCommandKind.StopMusic:
                case StoryCommandKind.StopAmbience:
                    ParseNumbers(ref c, words, 0, false, true);
                    break;
                case StoryCommandKind.Sfx:
                    if (words.Length == 0) c.error = "needs a sound id";
                    else c.id = words[0];
                    if (words.Length > 1) c.error = "takes one sound id";
                    break;
                case StoryCommandKind.Wait:
                case StoryCommandKind.Auto:
                    ParseNumbers(ref c, words, 0, false, true);
                    if (c.hasSeconds && c.seconds < 0f) c.seconds = 0f;
                    break;
                case StoryCommandKind.Chapter:
                    c.text = value.Trim();
                    if (c.text.Length == 0) c.error = "needs a title (#chapter:One. The Platform)";
                    break;
                case StoryCommandKind.Ending:
                case StoryCommandKind.Investigate:
                    if (words.Length != 1) c.error = "takes one id";
                    if (words.Length > 0) c.id = words[0];
                    break;
                case StoryCommandKind.Meta:
                    ParseMeta(ref c, value);
                    break;
                case StoryCommandKind.Speaker:
                case StoryCommandKind.Event:
                    c.text = value.Trim();
                    c.id = c.text;
                    break;
                case StoryCommandKind.Portrait:
                    if (PortraitCommand.TryParse(value, out var p)) { c.id = p.character; c.expression = p.expression; }
                    else c.error = "needs a character id";
                    break;
                default:
                    if (words.Length > 0 && info.noArguments) c.error = "takes no value";
                    break;
            }
            return c;
        }

        static string[] PrependId(string[] words, string id)
        {
            var list = new List<string> { id };
            list.AddRange(words);
            return list.ToArray();
        }

        static bool IsStopWord(string w)
        {
            string l = w.ToLowerInvariant();
            return l == "stop" || l == "off" || l == "none";
        }

        static void ParseIdTransition(ref StoryCommand c, string[] words, bool allowOff)
        {
            if (words.Length == 0) { c.error = "needs an id"; return; }
            c.id = words[0];
            if (allowOff && (IsStopWord(c.id) || c.id.ToLowerInvariant() == "hide"))
            {
                c.kind = StoryCommandKind.CgOff;
                c.id = "";
            }
            for (int i = 1; i < words.Length; i++)
            {
                if (TryTransition(words[i], out var t)) { c.transition = t; continue; }
                if (TryNumber(words[i], out float f) && !c.hasSeconds) { c.seconds = Math.Max(0f, f); c.hasSeconds = true; continue; }
                c.error = "didn't understand \"" + words[i] + "\"";
            }
        }

        static void ParseShow(ref StoryCommand c, string[] words)
        {
            if (words.Length == 0) { c.error = "needs a character id"; return; }
            c.id = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                string w = words[i];
                string l = w.ToLowerInvariant();
                if (l == "at") continue;
                if (l == "flip" || l == "flipped") { c.flip = true; c.hasFlip = true; continue; }
                if (l == "noflip" || l == "unflip") { c.flip = false; c.hasFlip = true; continue; }
                if (TryPosition(w, out float x)) { c.x = x; c.hasX = true; continue; }
                if (TryTransition(w, out var t)) { c.transition = t; continue; }
                if (TryNumber(w, out float f)) { if (!c.hasSeconds) { c.seconds = Math.Max(0f, f); c.hasSeconds = true; } else c.error = "two durations"; continue; }
                if (c.expression.Length == 0) c.expression = w;
                else c.error = "two expressions (\"" + c.expression + "\" and \"" + w + "\")";
            }
        }

        static void ParseHide(ref StoryCommand c, string[] words)
        {
            if (words.Length == 0) { c.error = "needs a character id (or all)"; return; }
            c.id = words[0];
            if (c.id.ToLowerInvariant() == "all") { c.kind = StoryCommandKind.HideAll; c.id = ""; }
            for (int i = 1; i < words.Length; i++)
            {
                if (TryTransition(words[i], out var t)) { c.transition = t; continue; }
                if (TryNumber(words[i], out float f) && !c.hasSeconds) { c.seconds = Math.Max(0f, f); c.hasSeconds = true; continue; }
                c.error = "didn't understand \"" + words[i] + "\"";
            }
        }

        static void ParseMove(ref StoryCommand c, string[] words)
        {
            if (words.Length == 0) { c.error = "needs a character id and a place"; return; }
            c.id = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                string w = words[i];
                if (w.ToLowerInvariant() == "to") continue;
                if (w.ToLowerInvariant() == "flip" || w.ToLowerInvariant() == "flipped") { c.flip = true; c.hasFlip = true; continue; }
                if (w.ToLowerInvariant() == "noflip" || w.ToLowerInvariant() == "unflip") { c.flip = false; c.hasFlip = true; continue; }
                if (TryPosition(w, out float x)) { c.x = x; c.hasX = true; continue; }
                if (TryNumber(w, out float f) && !c.hasSeconds) { c.seconds = Math.Max(0f, f); c.hasSeconds = true; continue; }
                c.error = "didn't understand \"" + w + "\"";
            }
            if (!c.hasX && c.error == null) c.error = "needs a place (left, right, center, farleft, farright or 30%)";
        }

        /// <summary>First word: amount (if <paramref name="amountFirst"/>), then seconds.</summary>
        static void ParseNumbers(ref StoryCommand c, string[] words, int start, bool amountFirst, bool allowSeconds)
        {
            int n = 0;
            for (int i = start; i < words.Length; i++)
            {
                if (!TryNumber(words[i], out float f)) { c.error = "\"" + words[i] + "\" isn't a number"; continue; }
                if (amountFirst && n == 0) { c.amount = f; c.hasAmount = true; }
                else if (allowSeconds && !c.hasSeconds) { c.seconds = Math.Max(0f, f); c.hasSeconds = true; }
                else c.error = "too many numbers";
                n++;
            }
        }

        static void ParseColourAndSeconds(ref StoryCommand c, string[] words, int start)
        {
            for (int i = start; i < words.Length; i++)
            {
                if (TryNumber(words[i], out float f))
                {
                    if (!c.hasSeconds) { c.seconds = Math.Max(0f, f); c.hasSeconds = true; }
                    else c.error = "two durations";
                }
                else if (c.text.Length == 0) c.text = words[i];
                else c.error = "didn't understand \"" + words[i] + "\"";
            }
        }

        static void ParseFade(ref StoryCommand c, string[] words)
        {
            if (words.Length == 0) { c.error = "needs out or in (#fade:out black 1)"; return; }
            string dir = words[0].ToLowerInvariant();
            if (dir != "out" && dir != "in") { c.error = "the first word is out or in"; return; }
            c.id = dir;
            ParseColourAndSeconds(ref c, words, 1);
        }

        static void ParseZoom(ref StoryCommand c, string[] words)
        {
            if (words.Length == 0) { c.error = "needs a scale (#zoom:1.3, #zoom:1 resets)"; return; }
            if (!TryNumber(words[0], out float scale) || scale <= 0f) { c.error = "the first word is the scale, like 1.3"; return; }
            c.amount = Clamp(scale, 0.5f, 4f);
            c.hasAmount = true;
            for (int i = 1; i < words.Length; i++)
            {
                string w = words[i];
                if (w.EndsWith("%", StringComparison.Ordinal) && TryPosition(w, out float p))
                {
                    if (!c.hasX) { c.x = p; c.hasX = true; }
                    else if (!c.hasY) { c.y = p; c.hasY = true; }
                    else c.error = "too many places";
                    continue;
                }
                if (TryPosition(w, out float x) && !c.hasX) { c.x = x; c.hasX = true; continue; }
                if (TryNumber(w, out float f) && !c.hasSeconds) { c.seconds = Math.Max(0f, f); c.hasSeconds = true; continue; }
                c.error = "didn't understand \"" + w + "\"";
            }
        }

        public static readonly string[] Filters = { "none", "sepia", "gray", "grey", "invert", "night", "dream" };

        static void ParseFilter(ref StoryCommand c, string[] words)
        {
            if (words.Length == 0) { c.error = "needs a filter: " + string.Join(", ", Filters); return; }
            c.id = words[0].ToLowerInvariant();
            if (c.id == "grey") c.id = "gray";
            if (c.id == "off") c.id = "none";
            if (Array.IndexOf(Filters, c.id) < 0) c.error = "unknown filter \"" + words[0] + "\" (" + string.Join(", ", Filters) + ")";
            ParseNumbers(ref c, words, 1, true, true);
            if (c.hasAmount) c.amount = Clamp(c.amount, 0f, 1f);
        }

        /// <summary>Meta actions: the fourth-wall tricks (see StoryTags.MetaActions).</summary>
        static void ParseMeta(ref StoryCommand c, string value)
        {
            value = value.Trim();
            int space = value.IndexOf(' ');
            c.id = (space < 0 ? value : value.Substring(0, space)).ToLowerInvariant();
            c.text = space < 0 ? "" : value.Substring(space + 1).Trim();
            if (c.id.Length == 0) { c.error = "needs an action: " + string.Join(", ", StoryTags.MetaActions); return; }
            if (Array.IndexOf(StoryTags.MetaActions, c.id) < 0) { c.error = "unknown meta action \"" + c.id + "\" (" + string.Join(", ", StoryTags.MetaActions) + ")"; return; }
            switch (c.id)
            {
                case "title":
                case "tagline":
                case "playlabel":
                case "popup":
                    if (c.text.Length == 0 && c.id == "popup") c.error = "needs the text to show";
                    break;
                case "fakecrash":
                    if (c.text.Length > 0)
                    {
                        if (TryNumber(c.text, out float s)) { c.seconds = Math.Max(0.2f, s); c.hasSeconds = true; }
                        else c.error = "takes seconds";
                    }
                    break;
            }
        }

        // ---------- Words ----------

        public static bool TryTransition(string word, out StageTransition t)
        {
            switch (word.ToLowerInvariant())
            {
                case "cut": case "instant": t = StageTransition.Cut; return true;
                case "fade": case "crossfade": case "dissolve": t = StageTransition.Fade; return true;
                case "flash": case "white": t = StageTransition.Flash; return true;
                case "black": case "dip": t = StageTransition.Black; return true;
                case "slide": t = StageTransition.Slide; return true;
                default: t = StageTransition.Default; return false;
            }
        }

        /// <summary>Named places: farleft 0.12, left 0.27, center 0.5, right 0.73, farright 0.88; or "30%".</summary>
        public static bool TryPosition(string word, out float x)
        {
            x = 0.5f;
            if (string.IsNullOrEmpty(word)) return false;
            switch (word.ToLowerInvariant())
            {
                case "farleft": case "far-left": x = 0.12f; return true;
                case "left": x = 0.27f; return true;
                case "center": case "centre": case "middle": x = 0.5f; return true;
                case "right": x = 0.73f; return true;
                case "farright": case "far-right": x = 0.88f; return true;
                case "offleft": case "off-left": x = -0.25f; return true;
                case "offright": case "off-right": x = 1.25f; return true;
            }
            if (word.EndsWith("%", StringComparison.Ordinal) && float.TryParse(word.Substring(0, word.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float p))
            {
                x = Clamp(p / 100f, -0.5f, 1.5f);
                return true;
            }
            return false;
        }

        public static bool TryNumber(string word, out float f)
        {
            if (float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f)) return true;
            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                && float.TryParse(word.Substring(0, word.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f)) return true;
            f = 0f;
            return false;
        }

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>One known tag: its key, aliases, what it does and an example (shown by the validator).</summary>
    public sealed class StoryTagInfo
    {
        public string key;
        public string[] aliases;
        public StoryCommandKind kind;
        public StoryIdKind idKind;
        public bool noArguments;
        public string summary;
        public string example;
    }

    /// <summary>
    /// Every tag the story template understands. docs/TEMPLATE-STORY.md has the same table with
    /// examples; the validator uses this list to catch typos ("#bgg" is an error, with a suggestion).
    /// </summary>
    public static class StoryTags
    {
        public static readonly string[] MetaActions = { "title", "tagline", "playlabel", "reset", "fakecrash", "popup", "erase" };

        public static readonly StoryTagInfo[] All =
        {
            T("bg", StoryCommandKind.Background, StoryIdKind.Background, "background (keeps characters on stage)", "#bg:platform fade 1.5", "background"),
            T("scene", StoryCommandKind.Scene, StoryIdKind.Background, "new scene: background, and clears characters and CG", "#scene:office black"),
            T("show", StoryCommandKind.Show, StoryIdKind.Character, "character on stage, with expression, place, transition", "#show:iris smile left", "char"),
            T("hide", StoryCommandKind.Hide, StoryIdKind.Character, "take a character off (or #hide:all)", "#hide:iris fade"),
            T("move", StoryCommandKind.Move, StoryIdKind.Character, "slide a character to a place", "#move:iris center 0.6"),
            T("cg", StoryCommandKind.Cg, StoryIdKind.Cg, "full-screen picture over everything (#cg:off removes it)", "#cg:photo fade 2"),
            T("shake", StoryCommandKind.Shake, StoryIdKind.None, "shake the stage, strength 0..1, seconds", "#shake:0.5"),
            T("flash", StoryCommandKind.Flash, StoryIdKind.None, "full-screen flash, colour and seconds", "#flash:white 0.3"),
            T("fade", StoryCommandKind.Fade, StoryIdKind.None, "fade the stage out to a colour, or back in", "#fade:out black 1"),
            T("zoom", StoryCommandKind.Zoom, StoryIdKind.None, "camera zoom (1 = normal), seconds, focus point", "#zoom:1.4 2 70% 40%"),
            T("blur", StoryCommandKind.Blur, StoryIdKind.None, "soft focus in pixels (0 = sharp), seconds", "#blur:6 1"),
            T("filter", StoryCommandKind.Filter, StoryIdKind.None, "colour filter: sepia, gray, invert, night, dream, none", "#filter:sepia"),
            T("glitch", StoryCommandKind.Glitch, StoryIdKind.None, "digital glitch over stage and text, strength and seconds", "#glitch:0.8 1.2"),
            T("music", StoryCommandKind.Music, StoryIdKind.Sound, "music by id (crossfades; #music:stop)", "#music:music_story_rain 2"),
            T("stopmusic", StoryCommandKind.StopMusic, StoryIdKind.None, "fade the music out", "#stopmusic:2", "musicstop"),
            T("ambience", StoryCommandKind.Ambience, StoryIdKind.Sound, "looping background sound (rain, hum)", "#ambience:amb_rain", "amb"),
            T("stopambience", StoryCommandKind.StopAmbience, StoryIdKind.None, "fade the ambience out", "#stopambience", "ambiencestop"),
            T("sfx", StoryCommandKind.Sfx, StoryIdKind.Sound, "one sound effect", "#sfx:door_knock", "sound"),
            T("nvl", StoryCommandKind.Nvl, StoryIdKind.None, "full-screen text mode (for prose)", "#nvl", null, true),
            T("adv", StoryCommandKind.Adv, StoryIdKind.None, "back to the text box", "#adv", null, true),
            T("clear", StoryCommandKind.Clear, StoryIdKind.None, "clear the full-screen text page", "#clear", null, true),
            T("thought", StoryCommandKind.Thought, StoryIdKind.None, "this line is a thought (italic, no quotes)", "#thought", "think", true),
            T("chapter", StoryCommandKind.Chapter, StoryIdKind.None, "chapter title card", "#chapter:One - The Platform"),
            T("wait", StoryCommandKind.Wait, StoryIdKind.None, "pause before the next line types", "#wait:1"),
            T("auto", StoryCommandKind.Auto, StoryIdKind.None, "this line goes on by itself after N seconds", "#auto:2"),
            T("ending", StoryCommandKind.Ending, StoryIdKind.Ending, "this route's ending (recorded for the gallery)", "#ending:last_train"),
            T("investigate", StoryCommandKind.Investigate, StoryIdKind.Hotspots, "open a clickable hotspot scene", "#investigate:office", "hotspots"),
            T("leave", StoryCommandKind.Leave, StoryIdKind.None, "end the hotspot scene; the story goes on", "#leave", null, true),
            T("meta", StoryCommandKind.Meta, StoryIdKind.Meta, "fourth-wall tricks: title, tagline, playlabel, reset, fakecrash, popup, erase", "#meta:title Next Train Home"),
            T("speaker", StoryCommandKind.Speaker, StoryIdKind.None, "name in the box until changed (or write \"Iris: line\")", "#speaker:Iris"),
            T("portrait", StoryCommandKind.Portrait, StoryIdKind.Character, "the shell's box portrait (other templates); use #show here", "#portrait:Guide happy left"),
            T("event", StoryCommandKind.Event, StoryIdKind.None, "tell game code something happened", "#event:door_opened"),
        };

        static StoryTagInfo T(string key, StoryCommandKind kind, StoryIdKind idKind, string summary, string example, string alias = null, bool noArgs = false) =>
            new StoryTagInfo { key = key, kind = kind, idKind = idKind, summary = summary, example = example, aliases = alias == null ? new string[0] : new[] { alias }, noArguments = noArgs };

        static Dictionary<string, StoryTagInfo> _byKey;

        public static StoryTagInfo Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_byKey == null)
            {
                var d = new Dictionary<string, StoryTagInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in All)
                {
                    d[t.key] = t;
                    foreach (var a in t.aliases) d[a] = t;
                }
                _byKey = d;
            }
            return _byKey.TryGetValue(key, out var info) ? info : null;
        }

        /// <summary>The closest known key (for "did you mean"), or null when nothing is close.</summary>
        public static string Suggest(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string best = null;
            int bestD = int.MaxValue;
            foreach (var t in All)
            {
                int d = Distance(key.ToLowerInvariant(), t.key);
                if (d < bestD) { bestD = d; best = t.key; }
            }
            return bestD <= (key.Length <= 3 ? 1 : 2) ? best : null;
        }

        static int Distance(string a, string b)
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
