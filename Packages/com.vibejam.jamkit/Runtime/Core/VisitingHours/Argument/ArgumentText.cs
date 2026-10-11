using System;
using System.Collections.Generic;
using System.Text;

namespace JamKit.Argument
{
    /// <summary>
    /// The battle's words live in text files the creator edits directly (docs/ARGUMENT-PROTOTYPE.md, "The prose files"),
    /// not in code: one file per battle (lena_night2.txt) and one for the screen (screen.txt), under
    /// Assets/JamKit/Templates/VisitingHours/Argument/Resources/ArgumentText/. The format, one entry per line:
    /// <code>
    /// # a heading (ignored)
    /// L1.text: Out there, Theo is dead.
    ///   ? what the line must say (a note: any indented line, ignored)
    /// </code>
    /// The id before the first colon, the line after it; "\n" in a line is a line break. Ids match docs/ARGUMENT-PROSE-BRIEF.md.
    /// </summary>
    public static class ArgumentText
    {
        /// <summary>Reads a file's entries (later duplicates win). Null or "" gives none.</summary>
        public static Dictionary<string, string> Parse(string text)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return d;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line[0] == '#' || line[0] == '?' || line[0] == '>') continue;
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim();
                if (key.Length == 0 || key.IndexOf(' ') >= 0) continue;
                d[key] = line.Substring(colon + 1).Trim().Replace("\\n", "\n");
            }
            return d;
        }

        // ---------- A battle's lines ----------

        static readonly string[] BattleFields =
        {
            "opponent", "claim", "opening", "noTouchReply", "pressClaimLine", "pressClaimReply", "concedeClaimLine",
            "concedeClaimReply", "letItRestLine", "letItRestReply", "truthText", "personalText", "convincedReply",
            "heardOutReply", "pressedReply", "composureLostLine",
        };

        static readonly string[] PremiseFields =
        {
            "text", "question", "askReply", "askAgainReply", "concedeLine", "concedeReply", "concedeFalseReply",
            "trueReply", "noTouchReply", "unbreakableReply", "pressLine", "pressReply", "slipName",
        };

        static string Kind(ReasonKind k)
        {
            switch (k)
            {
                case ReasonKind.Sound: return "sound";
                case ReasonKind.Beside: return "beside";
                case ReasonKind.NonSequitur: return "nonsequitur";
                default: return "antagonise";
            }
        }

        /// <summary>Every id <paramref name="def"/> has a line for, in file order: the battle's fields, then each premise's
        /// fields that apply, its retort, its reason cards (one set per premise: "L1b.sound", "L1b.sound.reply"; a
        /// line "L3c.truth_walt.sound" overrides it for one piece of evidence), then each evidence card's name and note.</summary>
        public static List<string> Keys(ArgumentDef def)
        {
            var keys = new List<string>();
            foreach (var f in BattleFields) keys.Add(f);
            foreach (var p in def.premises)
            {
                if (p == null) continue;
                foreach (var f in PremiseFields)
                    if (PremiseFieldApplies(p, f)) keys.Add(p.id + "." + f);
                if (p.retort != null) { keys.Add(p.id + ".retort"); keys.Add(p.id + ".retort.reply"); }
                var seen = new HashSet<ReasonKind>();
                foreach (var c in p.challenges)
                    foreach (var r in c.reasons)
                        if (r != null && seen.Add(r.kind)) { keys.Add(p.id + "." + Kind(r.kind)); keys.Add(p.id + "." + Kind(r.kind) + ".reply"); }
            }
            foreach (var e in def.evidence)
                if (e != null) { keys.Add(e.id + ".name"); keys.Add(e.id + ".note"); }
            return keys;
        }

        /// <summary>Which of a premise's lines it can ever show (an unbreakable one has no press lines' use for trueReply, etc.).</summary>
        static bool PremiseFieldApplies(PremiseDef p, string f)
        {
            switch (f)
            {
                case "question": return p.retort != null;
                case "askAgainReply": return p.askAgainReveals.Count > 0;
                case "concedeFalseReply": return p.truth == PremiseTruth.False;
                case "trueReply": return p.truth != PremiseTruth.False && !p.unbreakable;
                case "noTouchReply": return false; // optional per premise: the battle's noTouchReply covers it
                case "unbreakableReply": return p.unbreakable;
                case "slipName": return p.revealedBySlip;
                case "concedeReply": return p.truth != PremiseTruth.False; // a false one gets concedeFalseReply
                default: return true;
            }
        }

        /// <summary>Fills <paramref name="def"/>'s words from <paramref name="lines"/> (the structure stays). Returns the ids
        /// with no line (they show as "[id]" on screen, so a gap is visible, never silent).</summary>
        public static List<string> Apply(ArgumentDef def, IDictionary<string, string> lines)
        {
            var missing = new List<string>();
            lines = lines ?? new Dictionary<string, string>();
            string Get(string key)
            {
                if (lines.TryGetValue(key, out var v)) return v;
                missing.Add(key);
                return "[" + key + "]";
            }
            string Opt(string key) => lines.TryGetValue(key, out var v) ? v : "";

            def.opponent = Get("opponent");
            def.claim = Get("claim");
            def.opening = Get("opening");
            def.noTouchReply = Get("noTouchReply");
            def.pressClaimLine = Get("pressClaimLine");
            def.pressClaimReply = Get("pressClaimReply");
            def.concedeClaimLine = Get("concedeClaimLine");
            def.concedeClaimReply = Get("concedeClaimReply");
            def.letItRestLine = Get("letItRestLine");
            def.letItRestReply = Get("letItRestReply");
            def.truthText = Get("truthText");
            def.personalText = Get("personalText");
            def.convincedReply = Get("convincedReply");
            def.heardOutReply = Get("heardOutReply");
            def.pressedReply = Get("pressedReply");
            def.composureLostLine = Get("composureLostLine");

            foreach (var p in def.premises)
            {
                if (p == null) continue;
                string P(string f) => PremiseFieldApplies(p, f) ? Get(p.id + "." + f) : Opt(p.id + "." + f);
                p.text = P("text");
                p.question = P("question");
                p.askReply = P("askReply");
                p.askAgainReply = P("askAgainReply");
                p.concedeLine = P("concedeLine");
                p.concedeReply = P("concedeReply");
                p.concedeFalseReply = P("concedeFalseReply");
                p.trueReply = P("trueReply");
                p.noTouchReply = Opt(p.id + ".noTouchReply");
                p.unbreakableReply = P("unbreakableReply");
                p.pressLine = P("pressLine");
                p.pressReply = P("pressReply");
                p.slipName = P("slipName");
                if (p.retort != null)
                {
                    p.retort.line = Get(p.id + ".retort");
                    p.retort.reply = Get(p.id + ".retort.reply");
                }
                foreach (var c in p.challenges)
                {
                    foreach (var r in c.reasons)
                    {
                        if (r == null) continue;
                        string k = Kind(r.kind);
                        string specific = p.id + "." + c.evidence + "." + k;
                        r.text = lines.TryGetValue(specific, out var st) ? st : Get(p.id + "." + k);
                        r.reply = lines.TryGetValue(specific + ".reply", out var sr) ? sr : Get(p.id + "." + k + ".reply");
                    }
                }
            }
            foreach (var e in def.evidence)
            {
                if (e == null) continue;
                e.name = Get(e.id + ".name");
                e.note = Get(e.id + ".note");
            }
            // A reason shared by several challenges of one premise is asked for once.
            var unique = new List<string>();
            foreach (var m in missing) if (!unique.Contains(m)) unique.Add(m);
            return unique;
        }

        /// <summary>The lines a battle has now, by id (the inverse of <see cref="Apply"/>): to write a file from a battle built in code.</summary>
        public static Dictionary<string, string> Capture(ArgumentDef def)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["opponent"] = def.opponent, ["claim"] = def.claim, ["opening"] = def.opening, ["noTouchReply"] = def.noTouchReply,
                ["pressClaimLine"] = def.pressClaimLine, ["pressClaimReply"] = def.pressClaimReply, ["concedeClaimLine"] = def.concedeClaimLine,
                ["concedeClaimReply"] = def.concedeClaimReply, ["letItRestLine"] = def.letItRestLine, ["letItRestReply"] = def.letItRestReply,
                ["truthText"] = def.truthText, ["personalText"] = def.personalText, ["convincedReply"] = def.convincedReply,
                ["heardOutReply"] = def.heardOutReply, ["pressedReply"] = def.pressedReply, ["composureLostLine"] = def.composureLostLine,
            };
            foreach (var p in def.premises)
            {
                if (p == null) continue;
                var f = new Dictionary<string, string>
                {
                    ["text"] = p.text, ["question"] = p.question, ["askReply"] = p.askReply, ["askAgainReply"] = p.askAgainReply,
                    ["concedeLine"] = p.concedeLine, ["concedeReply"] = p.concedeReply, ["concedeFalseReply"] = p.concedeFalseReply,
                    ["trueReply"] = p.trueReply, ["unbreakableReply"] = p.unbreakableReply, ["pressLine"] = p.pressLine,
                    ["pressReply"] = p.pressReply, ["slipName"] = p.slipName,
                };
                foreach (var kv in f) if (PremiseFieldApplies(p, kv.Key)) d[p.id + "." + kv.Key] = kv.Value ?? "";
                if (p.retort != null) { d[p.id + ".retort"] = p.retort.line; d[p.id + ".retort.reply"] = p.retort.reply; }
                foreach (var c in p.challenges)
                    foreach (var r in c.reasons)
                    {
                        if (r == null) continue;
                        string k = p.id + "." + Kind(r.kind);
                        if (!d.ContainsKey(k)) { d[k] = r.text; d[k + ".reply"] = r.reply; }
                    }
            }
            foreach (var e in def.evidence)
                if (e != null) { d[e.id + ".name"] = e.name; d[e.id + ".note"] = e.note; }
            return d;
        }

        /// <summary>Writes a battle's lines as a file (headings per premise, a note under each line saying what it must
        /// do). Used to create a file from a battle's structure; the creator then edits the lines.</summary>
        public static string Export(ArgumentDef def, IDictionary<string, string> lines)
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(def.id).Append(": every line of this battle. Edit the text after the colon; keep the id before it.\n");
            sb.Append("# Indented lines starting with ? are notes (what the line must say); the game ignores them. \\n is a line break.\n");
            sb.Append("# The flow and every line in detail: docs/ARGUMENT-PROSE-BRIEF.md.\n\n");
            string Line(string key) => lines != null && lines.TryGetValue(key, out var v) ? v.Replace("\n", "\\n") : "";
            void Entry(string key, string note)
            {
                sb.Append(key).Append(": ").Append(Line(key)).Append('\n');
                if (!string.IsNullOrEmpty(note)) sb.Append("  ? ").Append(note).Append('\n');
            }
            var keys = Keys(def);
            sb.Append("## The claim and the frame\n");
            foreach (var k in keys) if (k.IndexOf('.') < 0) Entry(k, BattleNote(k));
            foreach (var p in def.premises)
            {
                if (p == null) continue;
                sb.Append("\n## ").Append(p.id).Append(" (").Append(p.label).Append("): a ").Append(p.kind == PremiseKind.Fact ? "fact" : "link")
                  .Append(", ").Append(p.truth.ToString().ToUpperInvariant()).Append(p.key ? ", KEY" : "").Append(p.unbreakable ? ", can't be broken tonight" : "")
                  .Append(p.hidden ? ", hidden at first" : "").Append(p.painful ? ", painful" : "").Append('\n');
                foreach (var k in keys)
                    if (k.StartsWith(p.id + ".", StringComparison.Ordinal)) Entry(k, PremiseNote(k.Substring(p.id.Length + 1)));
            }
            sb.Append("\n## The evidence cards\n");
            foreach (var e in def.evidence)
                if (e != null) { Entry(e.id + ".name", "the card's name, as it reads in a sentence (\"the fourth chair\")"); Entry(e.id + ".note", "one line under the name: what it is"); }
            return sb.ToString();
        }

        static string BattleNote(string f)
        {
            switch (f)
            {
                case "opponent": return "who Nadia is arguing with";
                case "claim": return "their position in one short line: the centre of the screen";
                case "opening": return "their first line as the battle starts, ending in the claim";
                case "noTouchReply": return "when Nadia's evidence doesn't touch the premise: a redirect";
                case "pressClaimLine": return "Nadia forcing the claim itself";
                case "pressClaimReply": return "their answer to being forced (it ends the battle)";
                case "concedeClaimLine": return "Nadia giving in on the claim";
                case "concedeClaimReply": return "their answer (it ends the battle)";
                case "letItRestLine": return "Nadia, once she's heard every premise: she stops fighting and hears them";
                case "letItRestReply": return "their answer; they hand over their keepsake";
                case "truthText": return "the truth Nadia wins (Convinced): what she now understands, kept for later battles";
                case "personalText": return "the keepsake Nadia gets (Heard out): names the object";
                case "convincedReply": return "their line when convinced";
                case "heardOutReply": return "their line when heard out: not convinced, but respected";
                case "pressedReply": return "their line when Nadia won by force";
                case "composureLostLine": return "Nadia at 160 bpm: panic, she has to stop";
            }
            return "";
        }

        static string PremiseNote(string f)
        {
            switch (f)
            {
                case "text": return "the premise card: what they claim, in their words";
                case "question": return "a question they put to Nadia (a retort can dodge it)";
                case "askReply": return "their answer when Nadia asks why";
                case "askAgainReply": return "when asked a second time: what lies under it";
                case "concedeLine": return "Nadia conceding it";
                case "concedeReply": return "their answer to the concession (often states the next premise)";
                case "concedeFalseReply": return "when Nadia agrees with something false";
                case "trueReply": return "when Nadia tries to challenge something true: it's true, concede it";
                case "unbreakableReply": return "why it can't be broken tonight";
                case "pressLine": return "Nadia forcing it without a reason";
                case "pressReply": return "their answer to being forced";
                case "slipName": return "the slip as it flies in the pressure phase";
                case "retort": return "Nadia's antagonising reply: dodges the question, attacks them (angers them)";
                case "retort.reply": return "their angry answer";
                case "sound": return "the reason that follows: the proof that breaks this premise";
                case "sound.reply": return "their answer: concedes, falls back or digs deeper";
                case "beside": return "a reason that's true but beside the point";
                case "beside.reply": return "why it misses";
                case "nonsequitur": return "a reason that doesn't follow from the evidence";
                case "nonsequitur.reply": return "why it doesn't follow";
                case "antagonise": return "an antagonising card: dismisses them instead of answering (angers them)";
                case "antagonise.reply": return "their hurt, angry answer";
            }
            return "";
        }
    }

    /// <summary>
    /// The screen's and the transcript's own words (prompts, labels, Nadia's default lines, the sentences that spell out
    /// each step), from screen.txt (<see cref="ArgumentText"/>'s format). Templates take {premise}, {evidence},
    /// {Evidence}, {reason}, {opponent}, {question}, {slip}, {bpm}, {outcome}, {stance}, {claim}, {n}, {keys}.
    /// A line that's missing shows as "[its id]".
    /// </summary>
    public sealed class ArgumentStrings
    {
        readonly Dictionary<string, string> _lines;

        public ArgumentStrings(IDictionary<string, string> lines)
        {
            _lines = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines != null) foreach (var kv in lines) _lines[kv.Key] = kv.Value;
        }

        /// <summary>Where the shared screen file comes from (the Unity side reads the Resources file; tests read it from
        /// disk). Null: every line shows as its id.</summary>
        public static Func<string> SharedSource;
        static ArgumentStrings _shared;
        static string _sharedFrom;

        /// <summary>The screen file's lines, read once (again if the source returns new text).</summary>
        public static ArgumentStrings Shared
        {
            get
            {
                string text = SharedSource != null ? SharedSource() : null;
                if (_shared == null || !ReferenceEquals(text, _sharedFrom))
                {
                    _shared = new ArgumentStrings(ArgumentText.Parse(text));
                    _sharedFrom = text;
                }
                return _shared;
            }
        }

        public bool Has(string key) => _lines.ContainsKey(key);
        public IEnumerable<string> Ids => _lines.Keys;

        /// <summary>The line for <paramref name="key"/> ("[key]" if missing).</summary>
        public string this[string key] => _lines.TryGetValue(key, out var v) ? v : "[" + key + "]";

        /// <summary>The line with its {placeholders} filled from name/value pairs.</summary>
        public string Format(string key, params string[] pairs)
        {
            string s = this[key];
            if (pairs == null) return s;
            for (int i = 0; i + 1 < pairs.Length; i += 2) s = s.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? "");
            return s;
        }

        /// <summary>Every id the screen asks for (the screen file must have each one).</summary>
        public static readonly string[] Required =
        {
            // Nadia's own words and the opponent's defaults.
            "nadia.ask", "nadia.askAgain", "nadia.showUnbreakable", "nadia.showTrue", "nadia.showNoTouch", "nadia.reason",
            "nadia.concede", "nadia.press", "nadia.pressClaim", "nadia.concedeClaim",
            "opponent.concedeTrue", "opponent.concedeFalse", "opponent.silence",
            // The transcript's sentences (each step spelled out).
            "logic.unbreakable", "logic.true", "logic.noTouch", "logic.sound", "logic.beside", "logic.nonSequitur",
            "logic.antagonise", "logic.retort", "logic.retortQuestion", "logic.concedeFalse", "logic.concede",
            "logic.press", "logic.pressClaim", "logic.rest", "logic.concedeClaim", "logic.slip", "logic.heart",
            "logic.outcome", "logic.something",
            "stance.Amenable", "stance.Unsure", "stance.Condemns", "stance.None",
            "outcome.Convinced", "outcome.HeardOut", "outcome.Pressed", "outcome.ComposureLost", "outcome.Yielded", "outcome.None",
            // The screen.
            "screen.claim", "screen.fact", "screen.link", "screen.key", "screen.hidden", "screen.hiddenText",
            "screen.status.standing", "screen.status.heard", "screen.status.broken", "screen.status.conceded",
            "screen.status.agreed", "screen.status.forced", "screen.status.hidden", "screen.keysHolding", "screen.keyHolding",
            "screen.verb.ask", "screen.verb.challenge", "screen.verb.concede", "screen.verb.concedeClaim", "screen.verb.press",
            "screen.verb.pressClaim", "screen.verb.rest", "screen.verb.transcript", "screen.back",
            "screen.prompt.claim", "screen.prompt.premise", "screen.prompt.question", "screen.prompt.pickEvidence",
            "screen.prompt.pickReason", "screen.prompt.pressure", "screen.prompt.pressureAngry", "screen.prompt.reasonHeading",
            "screen.retortCard", "screen.because",
            "screen.why.nothingToAsk", "screen.why.cantConcede", "screen.why.cantPress", "screen.why.notYet",
            "screen.why.claimNoChallenge", "screen.why.cantChallenge",
            "screen.heard", "screen.saidAbout", "screen.transcriptTitle", "screen.placeholder", "screen.heart", "screen.heat", "screen.anger",
            "screen.end.continue", "screen.end.stance", "screen.end.truth", "screen.end.personal", "screen.end.tally", "screen.end.saved",
            "screen.hint.browse", "screen.hint.verbs", "screen.hint.pick", "screen.hint.pressure", "screen.hint.end", "screen.hint.transcript",
            "menu.title", "menu.subtitle", "menu.normal", "menu.story", "menu.longNight", "menu.noWalt", "menu.watch", "menu.forget",
            "menu.title.back", "menu.savedNone", "menu.saved", "menu.hint",
        };
    }
}
