using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace JamKit.Narrative
{
    /// <summary>
    /// A quick read of ink source text (no compiling): knots, stitches, functions, diverts, tags,
    /// LIST and VAR declarations, EXTERNALs and INCLUDEs, each with its file and line. The story
    /// validator checks the tags and the knot graph from it, so a typo is reported at the line that
    /// has it, in words a writer can act on. The real ink compiler still checks the syntax.
    /// </summary>
    public sealed class InkScan
    {
        public sealed class Knot
        {
            public string name;
            public string file;
            public int line;
            public bool isFunction;
            public readonly List<string> stitches = new List<string>();
            /// <summary>Knot names this knot diverts or threads to (first path segment).</summary>
            public readonly HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal);
        }

        public struct TagUse
        {
            /// <summary>The tag text without the #, trimmed.</summary>
            public string text;
            public string file;
            public int line;
            /// <summary>Holds inline logic ({...}): only known at run time.</summary>
            public bool dynamic;
            /// <summary>Knot it's in ("" = before the first knot).</summary>
            public string knot;
        }

        public struct DivertUse
        {
            public string target;
            public string file;
            public int line;
            public string knot;
        }

        public struct TextUse
        {
            public string text;
            public string file;
            public int line;
            public string knot;
        }

        public readonly List<Knot> knots = new List<Knot>();
        public readonly List<TagUse> tags = new List<TagUse>();
        public readonly List<DivertUse> diverts = new List<DivertUse>();
        /// <summary>Lines of text (for checking inline markup like &lt;c=name&gt;).</summary>
        public readonly List<TextUse> texts = new List<TextUse>();
        /// <summary>LIST name to its items.</summary>
        public readonly Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public readonly HashSet<string> variables = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> externals = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<string> includes = new List<string>();
        /// <summary>Labelled gathers and choices: "- (label)", "* (label) text".</summary>
        public readonly HashSet<string> labels = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Knots the top of the story (before the first knot) diverts to.</summary>
        public readonly HashSet<string> rootTargets = new HashSet<string>(StringComparer.Ordinal);

        static readonly Regex KnotRx = new Regex(@"^\s*={2,}\s*(function\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*(\([^)]*\))?\s*=*\s*$", RegexOptions.Compiled);
        static readonly Regex StitchRx = new Regex(@"^\s*=\s*([A-Za-z_][A-Za-z0-9_]*)\s*(\([^)]*\))?\s*$", RegexOptions.Compiled);
        static readonly Regex DivertRx = new Regex(@"(->|<-)\s*([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
        static readonly Regex ListRx = new Regex(@"^\s*LIST\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$", RegexOptions.Compiled);
        static readonly Regex VarRx = new Regex(@"^\s*(VAR|CONST)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        static readonly Regex ExternalRx = new Regex(@"^\s*EXTERNAL\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        static readonly Regex LabelRx = new Regex(@"^\s*[-*+](\s*[-*+])*\s*\(([A-Za-z_][A-Za-z0-9_]*)\)", RegexOptions.Compiled);
        static readonly Regex IncludeRx = new Regex(@"^\s*INCLUDE\s+(.+?)\s*$", RegexOptions.Compiled);

        public Knot FindKnot(string name)
        {
            foreach (var k in knots)
                if (k.name == name) return k;
            return null;
        }

        public bool HasKnot(string name) => FindKnot(name) != null;

        /// <summary>Reads several files (name to text) into one scan, as one story.</summary>
        public static InkScan Read(IEnumerable<KeyValuePair<string, string>> files)
        {
            var scan = new InkScan();
            foreach (var f in files) scan.Add(f.Key, f.Value);
            return scan;
        }

        public static InkScan Read(string fileName, string text)
        {
            var scan = new InkScan();
            scan.Add(fileName, text);
            return scan;
        }

        public void Add(string file, string text)
        {
            string[] lines = StripBlockComments(text ?? "").Replace("\r", "").Split('\n');
            Knot current = null;
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string line = StripLineComment(lines[i]);
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;

                var m = KnotRx.Match(line);
                if (m.Success)
                {
                    current = new Knot { name = m.Groups[2].Value, file = file, line = lineNo, isFunction = m.Groups[1].Success };
                    knots.Add(current);
                    continue;
                }
                m = StitchRx.Match(line);
                if (m.Success && !trimmed.StartsWith("==", StringComparison.Ordinal))
                {
                    if (current != null) current.stitches.Add(m.Groups[1].Value);
                    continue;
                }
                m = IncludeRx.Match(line);
                if (m.Success) { includes.Add(m.Groups[1].Value); continue; }
                m = ExternalRx.Match(line);
                if (m.Success) { externals.Add(m.Groups[1].Value); continue; }
                m = ListRx.Match(line);
                if (m.Success)
                {
                    var items = new List<string>();
                    foreach (var raw in m.Groups[2].Value.Split(','))
                    {
                        string it = raw.Trim().Trim('(', ')').Trim();
                        int eq = it.IndexOf('=');
                        if (eq >= 0) it = it.Substring(0, eq).Trim();
                        it = it.Trim('(', ')').Trim();
                        if (it.Length > 0) items.Add(it);
                    }
                    lists[m.Groups[1].Value] = items;
                    variables.Add(m.Groups[1].Value);
                    continue;
                }
                m = VarRx.Match(line);
                if (m.Success) { variables.Add(m.Groups[2].Value); continue; }
                if (trimmed.StartsWith("~", StringComparison.Ordinal)) continue; // logic line: no tags, no diverts

                string knotName = current != null ? current.name : "";
                var lm = LabelRx.Match(line);
                if (lm.Success) labels.Add(lm.Groups[2].Value);
                foreach (Match d in DivertRx.Matches(line))
                {
                    string target = d.Groups[2].Value;
                    if (target == "DONE" || target == "END") continue;
                    diverts.Add(new DivertUse { target = target, file = file, line = lineNo, knot = knotName });
                    string first = target.Split('.')[0];
                    if (current != null) current.targets.Add(first);
                    else rootTargets.Add(first);
                }
                ReadTags(line, file, lineNo, knotName);
                string words = TextPart(line);
                if (words.Length > 0) texts.Add(new TextUse { text = words, file = file, line = lineNo, knot = knotName });
            }
        }

        void ReadTags(string line, string file, int lineNo, string knot)
        {
            int i = 0;
            while (i < line.Length)
            {
                int hash = NextUnescaped(line, '#', i);
                if (hash < 0) return;
                int end = line.Length;
                int nextHash = NextUnescaped(line, '#', hash + 1);
                if (nextHash >= 0) end = nextHash;
                int divert = line.IndexOf("->", hash + 1, StringComparison.Ordinal);
                if (divert >= 0 && divert < end) end = divert;
                // Inside inline logic ({cond: text #tag}) the tag ends at the brace that closes it.
                int depth = 0;
                for (int k = hash + 1; k < end; k++)
                {
                    if (line[k] == '{') depth++;
                    else if (line[k] == '}')
                    {
                        if (depth == 0) { end = k; break; }
                        depth--;
                    }
                }
                string tag = line.Substring(hash + 1, end - hash - 1).Trim();
                if (tag.Length > 0) tags.Add(new TagUse { text = tag, file = file, line = lineNo, knot = knot, dynamic = tag.IndexOf('{') >= 0 });
                i = end;
                if (end == divert) i = end + 2;
                else if (end < line.Length && line[end] == '}') i = end + 1;
            }
        }

        static int NextUnescaped(string s, char c, int from)
        {
            for (int i = from; i < s.Length; i++)
                if (s[i] == c && (i == 0 || s[i - 1] != '\\')) return i;
            return -1;
        }

        /// <summary>The words of a content line, without the choice marks, tags and diverts.</summary>
        static string TextPart(string line)
        {
            string s = line.Trim();
            while (s.Length > 0 && (s[0] == '*' || s[0] == '+' || s[0] == '-' && !s.StartsWith("->", StringComparison.Ordinal))) s = s.Substring(1).TrimStart();
            int hash = NextUnescaped(s, '#', 0);
            if (hash >= 0) s = s.Substring(0, hash);
            int divert = s.IndexOf("->", StringComparison.Ordinal);
            if (divert >= 0) s = s.Substring(0, divert);
            return s.Trim();
        }

        static string StripLineComment(string line)
        {
            int c = line.IndexOf("//", StringComparison.Ordinal);
            return c >= 0 ? line.Substring(0, c) : line;
        }

        static string StripBlockComments(string text)
        {
            if (text.IndexOf("/*", StringComparison.Ordinal) < 0) return text;
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (i + 1 < text.Length && text[i] == '/' && text[i + 1] == '*')
                {
                    int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) end = text.Length - 2;
                    for (int k = i; k < end + 2 && k < text.Length; k++) if (text[k] == '\n') sb.Append('\n');
                    i = end + 2;
                    continue;
                }
                sb.Append(text[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>Knots reachable from the top of the story and from <paramref name="extraRoots"/>
        /// (knots that game data or code starts: hotspots, the start knot).</summary>
        public HashSet<string> Reachable(IEnumerable<string> extraRoots)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            foreach (var t in rootTargets) stack.Push(t);
            if (extraRoots != null) foreach (var r in extraRoots) if (!string.IsNullOrEmpty(r)) stack.Push(r.Split('.')[0]);
            while (stack.Count > 0)
            {
                string k = stack.Pop();
                if (!seen.Add(k)) continue;
                var knot = FindKnot(k);
                if (knot == null) continue;
                foreach (var t in knot.targets) if (!seen.Contains(t)) stack.Push(t);
            }
            return seen;
        }

        /// <summary>True if <paramref name="path"/> ("knot", "knot.stitch", or a stitch inside <paramref name="fromKnot"/>) exists.</summary>
        public bool PathExists(string path, string fromKnot)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string[] parts = path.Split('.');
            var k = FindKnot(parts[0]);
            if (k != null)
            {
                if (parts.Length == 1) return true;
                return k.stitches.Contains(parts[1]) || parts.Length > 1; // labelled gathers (knot.label) aren't scanned
            }
            var here = FindKnot(fromKnot ?? "");
            if (here != null && here.stitches.Contains(parts[0])) return true;
            // A labelled gather or choice, or a variable holding a divert target.
            return labels.Contains(parts[0]) || variables.Contains(parts[0]);
        }
    }
}
