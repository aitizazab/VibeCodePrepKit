using System;
using System.Collections.Generic;
using System.Globalization;

namespace JamKit.Narrative
{
    /// <summary>Answers names in a condition: ink variables, items, flags, memory.</summary>
    public interface IStoryFacts
    {
        /// <summary>The value of a name ("brass_key", "item:brass_key", "seen:office_desk", "playthrough"),
        /// or false if nothing by that name exists.</summary>
        bool TryGet(string name, out object value);
    }

    /// <summary>
    /// The little condition language used in data (hotspot "Show If", title variants):
    /// <code>
    /// brass_key                  true if an ink variable, item or flag by that name is set/held/true
    /// !door_open                 not
    /// item:brass_key && !seen:office_desk     prefixes name the source; && and || (or "and", "or")
    /// trust >= 2                 compare numbers (==, !=, &gt;, &gt;=, &lt;, &lt;=)
    /// last_ending == "stayed"    compare text
    /// (a || b) && c              brackets
    /// </code>
    /// Prefixes: item:, flag: (remembered forever), seen:knot (visited this playthrough),
    /// ending:id (ever reached), var: (ink variable), memory:key (remember/recall).
    /// An empty condition is true. Errors make it false and are reported by <see cref="Check"/>.
    /// </summary>
    public static class StoryCondition
    {
        enum T { Name, Number, Text, And, Or, Not, Eq, Ne, Gt, Ge, Lt, Le, Open, Close, End }

        struct Token
        {
            public T type;
            public string text;
            public float number;
        }

        /// <summary>Evaluates; a syntax error or an unknown name is false.</summary>
        public static bool Evaluate(string condition, IStoryFacts facts) => Evaluate(condition, facts, out _);

        public static bool Evaluate(string condition, IStoryFacts facts, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(condition)) return true;
            var tokens = new List<Token>();
            if (!Tokenize(condition, tokens, out error)) return false;
            var p = new Parser { tokens = tokens, facts = facts };
            object v;
            try
            {
                v = p.Or();
                if (p.Peek.type != T.End) throw new FormatException("unexpected \"" + p.Peek.text + "\"");
            }
            catch (FormatException e)
            {
                error = e.Message;
                return false;
            }
            return Truthy(v);
        }

        /// <summary>Syntax check only (names aren't looked up). Null when fine.</summary>
        public static string Check(string condition)
        {
            Evaluate(condition, null, out string error);
            return error;
        }

        /// <summary>Every name the condition reads (for the validator).</summary>
        public static List<string> Names(string condition)
        {
            var names = new List<string>();
            var tokens = new List<Token>();
            if (string.IsNullOrWhiteSpace(condition) || !Tokenize(condition, tokens, out _)) return names;
            foreach (var t in tokens) if (t.type == T.Name) names.Add(t.text);
            return names;
        }

        public static bool Truthy(object v)
        {
            switch (v)
            {
                case null: return false;
                case bool b: return b;
                case int i: return i != 0;
                case float f: return f != 0f;
                case double d: return d != 0.0;
                case string s: return s.Length > 0 && s != "0" && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase);
                default: return true;
            }
        }

        static bool Tokenize(string s, List<Token> tokens, out string error)
        {
            error = null;
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (c == '(') { tokens.Add(new Token { type = T.Open, text = "(" }); i++; continue; }
                if (c == ')') { tokens.Add(new Token { type = T.Close, text = ")" }); i++; continue; }
                if (c == '&' && i + 1 < s.Length && s[i + 1] == '&') { tokens.Add(new Token { type = T.And, text = "&&" }); i += 2; continue; }
                if (c == '|' && i + 1 < s.Length && s[i + 1] == '|') { tokens.Add(new Token { type = T.Or, text = "||" }); i += 2; continue; }
                if (c == '=' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token { type = T.Eq, text = "==" }); i += 2; continue; }
                if (c == '!' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token { type = T.Ne, text = "!=" }); i += 2; continue; }
                if (c == '>' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token { type = T.Ge, text = ">=" }); i += 2; continue; }
                if (c == '<' && i + 1 < s.Length && s[i + 1] == '=') { tokens.Add(new Token { type = T.Le, text = "<=" }); i += 2; continue; }
                if (c == '>') { tokens.Add(new Token { type = T.Gt, text = ">" }); i++; continue; }
                if (c == '<') { tokens.Add(new Token { type = T.Lt, text = "<" }); i++; continue; }
                if (c == '!') { tokens.Add(new Token { type = T.Not, text = "!" }); i++; continue; }
                if (c == '"' || c == '\'')
                {
                    int end = s.IndexOf(c, i + 1);
                    if (end < 0) { error = "a text in quotes isn't closed"; return false; }
                    tokens.Add(new Token { type = T.Text, text = s.Substring(i + 1, end - i - 1) });
                    i = end + 1;
                    continue;
                }
                if (char.IsDigit(c) || (c == '-' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    int start = i++;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    string num = s.Substring(start, i - start);
                    if (!float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) { error = "\"" + num + "\" isn't a number"; return false; }
                    tokens.Add(new Token { type = T.Number, text = num, number = f });
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i++;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_' || s[i] == '.' || s[i] == ':' || s[i] == '-')) i++;
                    string word = s.Substring(start, i - start);
                    string l = word.ToLowerInvariant();
                    if (l == "and") tokens.Add(new Token { type = T.And, text = word });
                    else if (l == "or") tokens.Add(new Token { type = T.Or, text = word });
                    else if (l == "not") tokens.Add(new Token { type = T.Not, text = word });
                    else if (l == "true") tokens.Add(new Token { type = T.Number, text = word, number = 1f });
                    else if (l == "false") tokens.Add(new Token { type = T.Number, text = word, number = 0f });
                    else tokens.Add(new Token { type = T.Name, text = word });
                    continue;
                }
                error = "unexpected \"" + c + "\"";
                return false;
            }
            tokens.Add(new Token { type = T.End, text = "end" });
            return true;
        }

        sealed class Parser
        {
            public List<Token> tokens;
            public IStoryFacts facts;
            int _pos;

            public Token Peek => tokens[_pos];
            Token Take() => tokens[_pos++];

            public object Or()
            {
                object left = And();
                while (Peek.type == T.Or)
                {
                    Take();
                    object right = And();
                    left = Truthy(left) || Truthy(right);
                }
                return left;
            }

            object And()
            {
                object left = Unary();
                while (Peek.type == T.And)
                {
                    Take();
                    object right = Unary();
                    left = Truthy(left) && Truthy(right);
                }
                return left;
            }

            object Unary()
            {
                if (Peek.type == T.Not)
                {
                    Take();
                    return !Truthy(Unary());
                }
                return Comparison();
            }

            object Comparison()
            {
                object left = Atom();
                var t = Peek.type;
                if (t == T.Eq || t == T.Ne || t == T.Gt || t == T.Ge || t == T.Lt || t == T.Le)
                {
                    Take();
                    object right = Atom();
                    return Compare(left, right, t);
                }
                return left;
            }

            object Atom()
            {
                var tok = Take();
                switch (tok.type)
                {
                    case T.Open:
                    {
                        object v = Or();
                        if (Take().type != T.Close) throw new FormatException("a bracket isn't closed");
                        return v;
                    }
                    case T.Number: return tok.number;
                    case T.Text: return tok.text;
                    case T.Name:
                        if (facts == null) return null;
                        return facts.TryGet(tok.text, out object v2) ? v2 : null;
                    case T.End: throw new FormatException("the condition ends too soon");
                    default: throw new FormatException("unexpected \"" + tok.text + "\"");
                }
            }

            static object Compare(object a, object b, T op)
            {
                if (TryNumber(a, out float x) && TryNumber(b, out float y))
                {
                    switch (op)
                    {
                        case T.Eq: return x == y;
                        case T.Ne: return x != y;
                        case T.Gt: return x > y;
                        case T.Ge: return x >= y;
                        case T.Lt: return x < y;
                        default: return x <= y;
                    }
                }
                string sa = a == null ? "" : Convert.ToString(a, CultureInfo.InvariantCulture);
                string sb = b == null ? "" : Convert.ToString(b, CultureInfo.InvariantCulture);
                int cmp = string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
                switch (op)
                {
                    case T.Eq: return cmp == 0;
                    case T.Ne: return cmp != 0;
                    case T.Gt: return cmp > 0;
                    case T.Ge: return cmp >= 0;
                    case T.Lt: return cmp < 0;
                    default: return cmp <= 0;
                }
            }

            static bool TryNumber(object v, out float f)
            {
                switch (v)
                {
                    case int i: f = i; return true;
                    case float fl: f = fl; return true;
                    case double d: f = (float)d; return true;
                    case bool b: f = b ? 1f : 0f; return true;
                    case null: f = 0f; return true;
                    case string s: return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
                    default: f = 0f; return false;
                }
            }
        }
    }
}
