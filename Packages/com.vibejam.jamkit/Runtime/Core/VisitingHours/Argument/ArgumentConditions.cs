using System;
using System.Collections.Generic;
using System.Globalization;

namespace JamKit.Argument
{
    /// <summary>
    /// The numbers a conditional line can read (the prose file's <c>id[condition]: text</c>,
    /// docs/ARGUMENT-PROTOTYPE.md, "Conditional lines"):
    /// <list type="bullet">
    /// <item><c>anger</c>: the opponent's anger this battle, 0..3. Antagonising cards and retorts raise it, and in Lena's battle
    /// presses do too. Thresholds used: 2 = angry, 3 = furious (max).</item>
    /// <item><c>force</c>: the game-wide Force tally (the player imposing their will): what the game passed in, plus this
    /// battle's presses. Thresholds used: 1 or less = low, 3 or more = high.</item>
    /// <item><c>despair</c>: the hidden Despair value the game passes in, 0..3 (3 = max).</item>
    /// <item><c>presses</c>: presses this battle. <c>antagonised</c>: antagonising cards and retorts this battle.
    /// <c>heat</c>: the battle's heat, 0..3.</item>
    /// <item><c>sorry</c>: 1 when Nadia has already said sorry at his bedside (Day 1's "the phone"), else 0.</item>
    /// <item><c>asides</c>: cards shown this battle that got an answer of their own (<see cref="AsideDef"/>: Lena's star
    /// wheel). <c>sections</c>: 1 in the sections presenter (arguing and dodging take turns), 0 in the debate.</item>
    /// </list>
    /// </summary>
    public struct ArgumentMeters
    {
        public int anger, force, despair, presses, antagonised, heat, sorry, asides, sections;

        public const int MaxAnger = 3, DespairMax = 3, ForceHigh = 3;

        public int Get(ArgumentMeter m)
        {
            switch (m)
            {
                case ArgumentMeter.Anger: return anger;
                case ArgumentMeter.Force: return force;
                case ArgumentMeter.Despair: return despair;
                case ArgumentMeter.Presses: return presses;
                case ArgumentMeter.Antagonised: return antagonised;
                case ArgumentMeter.Sorry: return sorry;
                case ArgumentMeter.Asides: return asides;
                case ArgumentMeter.Sections: return sections;
                default: return heat;
            }
        }
    }

    public enum ArgumentMeter { Anger, Force, Despair, Presses, Antagonised, Heat, Sorry, Asides, Sections }

    /// <summary>
    /// A conditional line's condition: comparisons joined by "," (and) and "|" (or), "and" binding tighter:
    /// <c>anger>=1|presses>=1</c>, <c>despair>=3,anger&lt;=1,force&lt;=1</c>. Operators: &gt;= &lt;= &gt; &lt; = == !=.
    /// Parsed once (when the battle's lines are applied); evaluating allocates nothing.
    /// </summary>
    public sealed class ArgumentCondition
    {
        struct Clause
        {
            public ArgumentMeter meter;
            public string op;
            public int value;
        }

        // Groups of clauses: any group whose clauses all hold.
        readonly List<Clause[]> _groups = new List<Clause[]>();

        public string Source { get; }

        ArgumentCondition(string source) { Source = source; }

        /// <summary>Reads a condition; null and an error message if it doesn't read.</summary>
        public static ArgumentCondition Parse(string text, out string error)
        {
            error = null;
            var c = new ArgumentCondition(text ?? "");
            if (string.IsNullOrWhiteSpace(text)) return c;
            foreach (var group in text.Split('|'))
            {
                var parts = group.Split(',');
                var clauses = new Clause[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!ParseClause(parts[i].Trim(), out clauses[i], out error)) return null;
                }
                c._groups.Add(clauses);
            }
            return c;
        }

        static readonly string[] Ops = { ">=", "<=", "!=", "==", ">", "<", "=" };

        static bool ParseClause(string s, out Clause clause, out string error)
        {
            clause = default;
            error = null;
            foreach (var op in Ops)
            {
                int at = s.IndexOf(op, StringComparison.Ordinal);
                if (at <= 0) continue;
                string name = s.Substring(0, at).Trim().ToLowerInvariant();
                string number = s.Substring(at + op.Length).Trim();
                if (!Meter(name, out clause.meter)) { error = "unknown meter \"" + name + "\" (anger, force, despair, presses, antagonised, heat, sorry, asides, sections)"; return false; }
                if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out clause.value)) { error = "not a whole number: \"" + number + "\""; return false; }
                clause.op = op == "==" ? "=" : op;
                return true;
            }
            error = "no comparison in \"" + s + "\" (use >= <= > < = !=)";
            return false;
        }

        static bool Meter(string name, out ArgumentMeter m)
        {
            switch (name)
            {
                case "anger": m = ArgumentMeter.Anger; return true;
                case "sorry": m = ArgumentMeter.Sorry; return true;
                case "force": m = ArgumentMeter.Force; return true;
                case "despair": m = ArgumentMeter.Despair; return true;
                case "presses": m = ArgumentMeter.Presses; return true;
                case "antagonised": m = ArgumentMeter.Antagonised; return true;
                case "heat": m = ArgumentMeter.Heat; return true;
                case "asides": m = ArgumentMeter.Asides; return true;
                case "sections": m = ArgumentMeter.Sections; return true;
            }
            m = ArgumentMeter.Anger;
            return false;
        }

        /// <summary>True if any group's clauses all hold (an empty condition always holds).</summary>
        public bool Holds(in ArgumentMeters m)
        {
            if (_groups.Count == 0) return true;
            for (int g = 0; g < _groups.Count; g++)
            {
                var clauses = _groups[g];
                bool all = true;
                for (int i = 0; i < clauses.Length && all; i++)
                {
                    int v = m.Get(clauses[i].meter), n = clauses[i].value;
                    switch (clauses[i].op)
                    {
                        case ">=": all = v >= n; break;
                        case "<=": all = v <= n; break;
                        case ">": all = v > n; break;
                        case "<": all = v < n; break;
                        case "!=": all = v != n; break;
                        default: all = v == n; break;
                    }
                }
                if (all) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The moments a coach speaks at (Mags on the earpiece in the Night 1 tutorial), each the first time it comes; the
    /// prose file's <c>coach.&lt;moment&gt;</c> lines:
    /// <list type="bullet">
    /// <item><c>ask</c>: the battle has started: ask why.</item>
    /// <item><c>challenge</c>: a premise is on the table that something in her tray can break.</item>
    /// <item><c>concede</c>: she has heard a premise that's true.</item>
    /// <item><c>pressure</c>: the first pressure phase.</item>
    /// <item><c>press</c>: the first misstep (a reason that misses, evidence that doesn't touch it, challenging what's true),
    /// or once only premises she can't break are left: forcing exists, and costs.</item>
    /// <item><c>rest</c>: everything's been heard: she can let it rest.</item>
    /// <item><c>broken</c>: the first premise she breaks.</item>
    /// </list>
    /// </summary>
    public static class ArgumentCoach
    {
        public const string Ask = "ask", Challenge = "challenge", Concede = "concede", Pressure = "pressure", Press = "press",
            Rest = "rest", Broken = "broken";
        /// <summary>The real-time battle's moments (docs/redesign/battle.md): the debate starts (move the light, keep off
        /// the red); the first "Because..." (pick the reason that follows, before they lose patience); the first wrong
        /// card (it costs a heartbeat and the card); the first showdown (cut through their words); the first slip.</summary>
        public const string Debate = "debate", Because = "because", Wrong = "wrong", Showdown = "showdown", Slip = "slip";
        /// <summary>The sections presenter: the first argue section (her case on the table: pick a statement and act on
        /// it), and the first of her words worth holding her to (catch them as she says them).</summary>
        public const string Argue = "argue", Quote = "quote";

        public static readonly string[] Moments = { Debate, Argue, Ask, Concede, Challenge, Because, Wrong, Pressure, Press, Showdown, Broken, Slip, Quote, Rest };
    }

    /// <summary>The short lines the real-time battle calls for, in each battle's prose file as <c>bark.&lt;id&gt;</c>.</summary>
    public static class ArgumentBarks
    {
        /// <summary>A whole round of her statements went by and Nadia said nothing (her temper rises).</summary>
        public const string Idle = "idle";
        /// <summary>Nadia showed her evidence and then couldn't say why in time.</summary>
        public const string Hesitate = "hesitate";
        /// <summary>The evidence glanced off: it hit the statement, not the words that matter.</summary>
        public const string Glance = "glance";
        /// <summary>The three rings begin (the painful phase: a phone at the edge of the table).</summary>
        public const string Ring = "ring";
        /// <summary>Every key premise is down, but their painful question stands unanswered (the last stand).</summary>
        public const string Stand = "stand";

        public static readonly string[] Ids = { Idle, Hesitate, Glance, Ring, Stand };

        /// <summary>The sections presenter's (a battle that uses them lists them in its prose file): the ceiling lifts (her
        /// sky, SELECTION.md 7); the first hush (hold still); what she noted about how Nadia moved (SELECTION.md 4): kept
        /// her distance, came close, never still.</summary>
        public const string Sky = "sky", Hush = "hush", NotesFar = "notesFar", NotesNear = "notesNear", NotesRestless = "notesRestless";
        public static readonly string[] Sections = { Sky, Hush, NotesFar, NotesNear, NotesRestless };
    }
}
