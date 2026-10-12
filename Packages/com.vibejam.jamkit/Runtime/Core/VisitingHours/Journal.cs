using System;
using System.Collections.Generic;

namespace JamKit.VisitingHours
{
    /// <summary>What a journal entry is. The journal's tabs: Facts, Memories, Deductions, Truths
    /// (personal entries, objects someone gave you, sit with the truths).</summary>
    public enum EntryKind { Fact, Memory, Deduction, Truth, Personal }

    /// <summary>One journal entry, as designer data. Text is in Nadia's voice.</summary>
    [Serializable]
    public class JournalEntryDef
    {
        public string id = "";
        public EntryKind kind;
        public string title = "";
        /// <summary>What Nadia wrote.</summary>
        public string text = "";
        /// <summary>A line she scribbled out under it (legible if you look closely). Empty for none.</summary>
        public string scribble = "";
        /// <summary>What she says when she uses it in an argument.</summary>
        public string sayLine = "";
        /// <summary>The art name of its picture (prop_memorial_card); empty draws a plain card.</summary>
        public string icon = "";
    }

    /// <summary>Two entries that together make a third (a deduction): the data-driven pair table.</summary>
    [Serializable]
    public class DeductionPairDef
    {
        public string a = "";
        public string b = "";
        public string result = "";
        /// <summary>How the open question reads while both halves are known and not yet combined.</summary>
        public string question = "";
    }

    public enum CombineResult
    {
        /// <summary>A new deduction.</summary>
        Made,
        /// <summary>The pair is right, but its deduction is already in the journal.</summary>
        AlreadyMade,
        /// <summary>The two don't go together (the gentle line).</summary>
        NoMatch,
        /// <summary>One of the two isn't in the journal.</summary>
        NotKnown,
        /// <summary>The same entry twice.</summary>
        Same,
    }

    /// <summary>A pair whose halves are both known and whose deduction isn't made yet.</summary>
    public struct OpenQuestion
    {
        public string a, b, result, question;
    }

    /// <summary>The journal's definitions: every entry and the pair table, with lookups and checks.</summary>
    public sealed class JournalBook
    {
        readonly List<JournalEntryDef> _entries = new List<JournalEntryDef>();
        readonly List<DeductionPairDef> _pairs = new List<DeductionPairDef>();
        readonly Dictionary<string, JournalEntryDef> _byId = new Dictionary<string, JournalEntryDef>(StringComparer.Ordinal);

        public JournalBook(IEnumerable<JournalEntryDef> entries, IEnumerable<DeductionPairDef> pairs)
        {
            if (entries != null)
                foreach (var e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.id)) continue;
                    _entries.Add(e);
                    if (!_byId.ContainsKey(e.id)) _byId.Add(e.id, e);
                }
            if (pairs != null)
                foreach (var p in pairs)
                    if (p != null) _pairs.Add(p);
        }

        public IReadOnlyList<JournalEntryDef> Entries => _entries;
        public IReadOnlyList<DeductionPairDef> Pairs => _pairs;

        public JournalEntryDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _byId.TryGetValue(id, out var e) ? e : null;
        }

        /// <summary>The pair made of these two entries, in either order, or null.</summary>
        public DeductionPairDef FindPair(string a, string b)
        {
            for (int i = 0; i < _pairs.Count; i++)
            {
                var p = _pairs[i];
                if ((p.a == a && p.b == b) || (p.a == b && p.b == a)) return p;
            }
            return null;
        }

        /// <summary>Plain-words problems: duplicate or empty ids, pairs that name unknown entries or
        /// combine an entry with itself, results that aren't deductions or truths, two pairs for one result.</summary>
        public void Validate(List<string> problems)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in _entries)
            {
                if (!seen.Add(e.id)) problems.Add("two journal entries have the id \"" + e.id + "\"");
                if (string.IsNullOrEmpty(e.title)) problems.Add("journal entry \"" + e.id + "\" has no title");
            }
            var results = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in _pairs)
            {
                string name = "the pair " + p.a + " + " + p.b;
                if (Find(p.a) == null) problems.Add(name + " names an unknown entry \"" + p.a + "\"");
                if (Find(p.b) == null) problems.Add(name + " names an unknown entry \"" + p.b + "\"");
                if (p.a == p.b) problems.Add(name + " combines an entry with itself");
                var r = Find(p.result);
                if (r == null) problems.Add(name + " makes an unknown entry \"" + p.result + "\"");
                else if (r.kind != EntryKind.Deduction && r.kind != EntryKind.Truth) problems.Add(name + " makes \"" + p.result + "\", which isn't a deduction or a truth");
                if (!string.IsNullOrEmpty(p.result) && !results.Add(p.result)) problems.Add("two pairs make \"" + p.result + "\"");
            }
        }
    }

    /// <summary>
    /// The journal's state: which entries Nadia has (in the order she found them), combining two
    /// into a deduction, and the open questions (pairs whose halves are both known but not yet
    /// combined). The rushing check asks <see cref="HasOpenQuestions"/>.
    /// </summary>
    public sealed class Journal
    {
        readonly JournalBook _book;
        readonly List<string> _order = new List<string>();
        readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);
        int _revision;

        public Journal(JournalBook book) { _book = book ?? new JournalBook(null, null); }

        public JournalBook Book => _book;
        /// <summary>Goes up whenever an entry is added (the UI redraws when it changes).</summary>
        public int Revision => _revision;
        public int Count => _order.Count;
        /// <summary>The i-th entry found.</summary>
        public string IdAt(int i) => _order[i];

        public bool Knows(string id) => !string.IsNullOrEmpty(id) && _known.Contains(id);

        /// <summary>Adds an entry. False when it's unknown to the book or already in the journal.</summary>
        public bool Record(string id)
        {
            if (_book.Find(id) == null || _known.Contains(id)) return false;
            _known.Add(id);
            _order.Add(id);
            _revision++;
            return true;
        }

        /// <summary>Takes an entry out again (replaying an argument). False when it wasn't there.</summary>
        public bool Forget(string id)
        {
            if (string.IsNullOrEmpty(id) || !_known.Remove(id)) return false;
            _order.Remove(id);
            _revision++;
            return true;
        }

        public void Clear()
        {
            _known.Clear();
            _order.Clear();
            _revision++;
        }

        /// <summary>Tries a pair. <paramref name="result"/> is the deduction's id when the pair is right.</summary>
        public CombineResult Combine(string a, string b, out string result)
        {
            result = "";
            if (!Knows(a) || !Knows(b)) return CombineResult.NotKnown;
            if (a == b) return CombineResult.Same;
            var pair = _book.FindPair(a, b);
            if (pair == null) return CombineResult.NoMatch;
            result = pair.result;
            if (Knows(pair.result)) return CombineResult.AlreadyMade;
            Record(pair.result);
            return CombineResult.Made;
        }

        /// <summary>Fills <paramref name="output"/> (cleared first) with the open questions; returns how many.</summary>
        public int OpenQuestions(List<OpenQuestion> output)
        {
            output.Clear();
            var pairs = _book.Pairs;
            for (int i = 0; i < pairs.Count; i++)
            {
                var p = pairs[i];
                if (!IsOpen(p)) continue;
                output.Add(new OpenQuestion { a = p.a, b = p.b, result = p.result, question = p.question });
            }
            return output.Count;
        }

        /// <summary>The rushing check: is there anything Nadia could still work out right now?</summary>
        public bool HasOpenQuestions
        {
            get
            {
                var pairs = _book.Pairs;
                for (int i = 0; i < pairs.Count; i++)
                    if (IsOpen(pairs[i])) return true;
                return false;
            }
        }

        /// <summary>True when this entry is half of an open question.</summary>
        public bool IsInOpenQuestion(string id)
        {
            var pairs = _book.Pairs;
            for (int i = 0; i < pairs.Count; i++)
            {
                var p = pairs[i];
                if ((p.a == id || p.b == id) && IsOpen(p)) return true;
            }
            return false;
        }

        bool IsOpen(DeductionPairDef p) => Knows(p.a) && Knows(p.b) && !Knows(p.result);

        /// <summary>The entries of one kind, in the order found (into <paramref name="output"/>, cleared first).</summary>
        public int EntriesOfKind(EntryKind kind, List<JournalEntryDef> output)
        {
            output.Clear();
            for (int i = 0; i < _order.Count; i++)
            {
                var e = _book.Find(_order[i]);
                if (e != null && e.kind == kind) output.Add(e);
            }
            return output.Count;
        }
    }

    /// <summary>The slice's places.</summary>
    /// <summary>The places of the slice and the chain's days (appended at the end: assets store them by number).</summary>
    public enum SliceArea { Hospital, Booth, Street, HospitalDay2, BoothDay2 }

    /// <summary>Something Nadia can look at with E (the Unity layer places it in the world).</summary>
    [Serializable]
    public class ExamineDef
    {
        public string id = "";
        public SliceArea area;
        /// <summary>The prompt's words: "E  Examine: the chart".</summary>
        public string label = "";
        public string knot = "";
        /// <summary>The entry its knot writes (for checks; the knot's #event tag does the writing).</summary>
        public string records = "";
    }

    /// <summary>The slice's hidden numbers, kept for the endings (none of them shows on screen).</summary>
    public sealed class SliceState
    {
        public readonly Journal journal;
        /// <summary>Force: the player imposing their will on Nadia (every pressed statement, a Pressed outcome).</summary>
        public int force;
        /// <summary>The Long Night's despair: hard to reach, slippery (unused in the slice; here so later nights can add to it).</summary>
        public float despair;
        public BattleOutcome waltOutcome;

        public SliceState(JournalBook book) { journal = new Journal(book); }
    }
}
