using System;
using System.Collections.Generic;

namespace JamKit.Presenting
{
    public enum JumpResult { Unknown, Jumped, Pending, Failed }

    /// <summary>
    /// Presenter mode's chapter select: named jump points per game scene ("group"). A template
    /// declares its chapter names up front (so the list shows on the title screen before the game
    /// scene is loaded) and registers the live jumps while its scene runs. Jumping to a declared
    /// chapter that isn't live yet leaves it pending: the shell starts the game scene and runs the
    /// jump as soon as the template registers it. Names stay listed after the scene unloads.
    /// </summary>
    public sealed class ChapterRegistry
    {
        sealed class Entry
        {
            public string group = "";
            public string name = "";
            public Action jump;
            public object owner;
        }

        readonly List<Entry> _entries = new List<Entry>();
        string _pendingGroup = "", _pendingName = "";
        string _lastGroup = "", _lastName = "";
        string _lastError = "";

        public int Count => _entries.Count;
        public string NameAt(int i) => _entries[i].name;
        public string GroupAt(int i) => _entries[i].group;
        public bool IsLive(int i) => _entries[i].jump != null;
        /// <summary>Who registered the live jump (null when not live or registered without an owner).</summary>
        public object OwnerAt(int i) => _entries[i].owner;

        /// <summary>The chapter waiting for its scene to register it ("" when none).</summary>
        public string PendingName => _pendingName;
        public string PendingGroup => _pendingGroup;
        public bool HasPending => _pendingName.Length > 0;
        /// <summary>The last chapter jumped to ("" before the first jump).</summary>
        public string LastJumped => _lastName;
        public string LastJumpedGroup => _lastGroup;
        /// <summary>The exception message of the last failed jump.</summary>
        public string LastError => _lastError;

        /// <summary>Lists <paramref name="names"/> under <paramref name="group"/> (in this order) without
        /// jumps; names already known keep their place.</summary>
        public void Declare(string group, IList<string> names)
        {
            if (names == null) return;
            group = group ?? "";
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[i];
                if (string.IsNullOrEmpty(n) || IndexOf(group, n) >= 0) continue;
                _entries.Add(new Entry { group = group, name = n });
            }
        }

        /// <summary>Makes a chapter live (adds it if new; a second registration replaces the jump).
        /// <paramref name="owner"/> lets <see cref="Unregister"/> take a scene's jumps away at once.</summary>
        public void Register(string group, string name, Action jump, object owner = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A chapter needs a name.", nameof(name));
            if (jump == null) throw new ArgumentNullException(nameof(jump));
            group = group ?? "";
            int i = IndexOf(group, name);
            Entry e;
            if (i >= 0) e = _entries[i];
            else
            {
                e = new Entry { group = group, name = name };
                _entries.Add(e);
            }
            e.jump = jump;
            e.owner = owner;
        }

        /// <summary>Takes away every live jump <paramref name="owner"/> registered (the names stay listed).
        /// Returns how many.</summary>
        public int Unregister(object owner)
        {
            if (owner == null) return 0;
            int n = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!ReferenceEquals(e.owner, owner) || e.jump == null) continue;
                e.jump = null;
                e.owner = null;
                n++;
            }
            return n;
        }

        public void Clear()
        {
            _entries.Clear();
            _pendingGroup = _pendingName = "";
            _lastGroup = _lastName = "";
            _lastError = "";
        }

        public int IndexOf(string group, string name)
        {
            group = group ?? "";
            for (int i = 0; i < _entries.Count; i++)
                if (string.Equals(_entries[i].name, name, StringComparison.Ordinal) && string.Equals(_entries[i].group, group, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        public bool IsLive(string group, string name)
        {
            int i = IndexOf(group, name);
            return i >= 0 && _entries[i].jump != null;
        }

        /// <summary>Fills <paramref name="output"/> with the indices of <paramref name="group"/>'s chapters, in order.</summary>
        public int InGroup(string group, List<int> output)
        {
            output.Clear();
            group = group ?? "";
            for (int i = 0; i < _entries.Count; i++)
                if (string.Equals(_entries[i].group, group, StringComparison.Ordinal)) output.Add(i);
            return output.Count;
        }

        /// <summary>
        /// Jumps now when the chapter is live; when it's only declared, remembers it as pending (the
        /// caller then loads the scene; <see cref="RunPending"/> finishes the jump). Unknown names do nothing.
        /// A jump that throws returns Failed (the message is in <see cref="LastError"/>).
        /// </summary>
        public JumpResult Jump(string group, string name)
        {
            int i = IndexOf(group, name);
            if (i < 0) return JumpResult.Unknown;
            var e = _entries[i];
            if (e.jump == null)
            {
                _pendingGroup = e.group;
                _pendingName = e.name;
                return JumpResult.Pending;
            }
            _pendingGroup = _pendingName = "";
            _lastGroup = e.group;
            _lastName = e.name;
            try
            {
                e.jump();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                return JumpResult.Failed;
            }
            return JumpResult.Jumped;
        }

        /// <summary>True when the pending chapter has become live.</summary>
        public bool PendingReady => HasPending && IsLive(_pendingGroup, _pendingName);

        /// <summary>Runs the pending chapter if it's live; otherwise keeps waiting (returns Pending), or
        /// Unknown when nothing is pending.</summary>
        public JumpResult RunPending()
        {
            if (!HasPending) return JumpResult.Unknown;
            if (!PendingReady) return JumpResult.Pending;
            return Jump(_pendingGroup, _pendingName);
        }

        public void CancelPending() => _pendingGroup = _pendingName = "";

        /// <summary>Marks a known chapter as pending without running it (the caller is about to load its
        /// scene). False for unknown names.</summary>
        public bool SetPending(string group, string name)
        {
            int i = IndexOf(group, name);
            if (i < 0) return false;
            _pendingGroup = _entries[i].group;
            _pendingName = _entries[i].name;
            return true;
        }

        /// <summary>The chapter after <paramref name="name"/> in its group ("" at the end or if unknown):
        /// the presenter's "next chapter" when something goes wrong.</summary>
        public string NextAfter(string group, string name)
        {
            int at = IndexOf(group, name);
            if (at < 0) return "";
            for (int i = at + 1; i < _entries.Count; i++)
                if (string.Equals(_entries[i].group, _entries[at].group, StringComparison.Ordinal)) return _entries[i].name;
            return "";
        }

        /// <summary>The group's first chapter ("" when it has none).</summary>
        public string FirstIn(string group)
        {
            group = group ?? "";
            for (int i = 0; i < _entries.Count; i++)
                if (string.Equals(_entries[i].group, group, StringComparison.Ordinal)) return _entries[i].name;
            return "";
        }
    }
}
