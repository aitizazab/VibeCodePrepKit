using System;
using System.Collections.Generic;

namespace JamKit.Narrative
{
    public enum LineKind { Narration, Dialogue, Thought, Choice, System }

    /// <summary>One line in the history: who said it (display name, "" for narration) and the text as drawn.</summary>
    [Serializable]
    public class BacklogEntry
    {
        public string speaker = "";
        public string text = "";
        public LineKind kind;

        public BacklogEntry Clone() => (BacklogEntry)MemberwiseClone();
    }

    /// <summary>
    /// The history the player can scroll back through: the last <see cref="Capacity"/> lines and the
    /// choices taken, oldest first. Saved with every save slot. The same line twice in a row is kept
    /// once (a load shows the current line again).
    /// </summary>
    public sealed class Backlog
    {
        readonly List<BacklogEntry> _entries = new List<BacklogEntry>();
        int _capacity;

        public Backlog(int capacity = 300) { _capacity = capacity < 1 ? 1 : capacity; }

        public int Capacity => _capacity;
        public int Count => _entries.Count;
        public BacklogEntry this[int index] => _entries[index];
        public IReadOnlyList<BacklogEntry> Entries => _entries;
        /// <summary>Changes every time a line is added or the history is replaced (views redraw on change).</summary>
        public int Version { get { return _version; } }
        int _version;

        public void Add(string speaker, string text, LineKind kind)
        {
            if (string.IsNullOrEmpty(text)) return;
            speaker = speaker ?? "";
            if (_entries.Count > 0)
            {
                var last = _entries[_entries.Count - 1];
                if (last.kind == kind && last.speaker == speaker && last.text == text) return;
            }
            _entries.Add(new BacklogEntry { speaker = speaker, text = text, kind = kind });
            if (_entries.Count > _capacity) _entries.RemoveRange(0, _entries.Count - _capacity);
            _version++;
        }

        public void AddChoice(string text) => Add("", text, LineKind.Choice);

        public void Clear()
        {
            _entries.Clear();
            _version++;
        }

        /// <summary>A copy for a save file.</summary>
        public List<BacklogEntry> ToList()
        {
            var list = new List<BacklogEntry>(_entries.Count);
            foreach (var e in _entries) list.Add(e.Clone());
            return list;
        }

        /// <summary>Replaces the history (loading a save).</summary>
        public void Load(IList<BacklogEntry> entries)
        {
            _entries.Clear();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.text)) _entries.Add(e.Clone());
            if (_entries.Count > _capacity) _entries.RemoveRange(0, _entries.Count - _capacity);
            _version++;
        }
    }
}
