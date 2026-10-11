using System;
using System.Collections.Generic;

namespace JamKit.Stealth
{
    /// <summary>
    /// A counter of things to find: register them, collect them (each once), ask how many are left or whether any are,
    /// and hear when the last one is found (<see cref="AllCollected"/> fires once; a game hooks a line to it, such as
    /// Visiting Hours' "I'm not sure if there's anything left to find..."). Story-free and engine-free.
    /// </summary>
    public sealed class CollectibleSet
    {
        readonly List<string> _ids = new List<string>();
        readonly List<bool> _got = new List<bool>();
        int _collected;
        bool _allFired;

        /// <summary>(index, remaining) after each new find.</summary>
        public event Action<int, int> Collected;
        /// <summary>Once, when the last one is found.</summary>
        public event Action AllCollected;

        public int Total => _ids.Count;
        public int CollectedCount => _collected;
        public int Remaining => _ids.Count - _collected;
        public bool AnyLeft => Remaining > 0;
        public bool AllFound => _ids.Count > 0 && Remaining == 0;

        /// <summary>Adds one (ids should be unique; a repeated id returns the first one's index).</summary>
        public int Register(string id)
        {
            int at = IndexOf(id);
            if (at >= 0) return at;
            _ids.Add(id ?? "");
            _got.Add(false);
            _allFired = false;
            return _ids.Count - 1;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < _ids.Count; i++) if (_ids[i] == id) return i;
            return -1;
        }

        public string IdOf(int index) => index >= 0 && index < _ids.Count ? _ids[index] : null;

        public bool IsCollected(int index) => index >= 0 && index < _got.Count && _got[index];

        /// <summary>Collects one. True the first time; false if it was already found or doesn't exist.</summary>
        public bool Collect(int index)
        {
            if (index < 0 || index >= _got.Count || _got[index]) return false;
            _got[index] = true;
            _collected++;
            Collected?.Invoke(index, Remaining);
            if (Remaining == 0 && !_allFired)
            {
                _allFired = true;
                AllCollected?.Invoke();
            }
            return true;
        }

        public bool Collect(string id) => Collect(IndexOf(id));

        /// <summary>Puts one back, not found (a checkpoint restart drops what was picked up since). True if it was collected.</summary>
        public bool Uncollect(int index)
        {
            if (index < 0 || index >= _got.Count || !_got[index]) return false;
            _got[index] = false;
            _collected--;
            _allFired = false;
            return true;
        }

        /// <summary>Everything back to not found (a restart). Registrations stay.</summary>
        public void ResetFound()
        {
            for (int i = 0; i < _got.Count; i++) _got[i] = false;
            _collected = 0;
            _allFired = false;
        }

        /// <summary>A line for a HUD: "3 / 5 found", or "All 5 found".</summary>
        public string Summary() => AllFound ? "All " + Total + " found" : _collected + " / " + Total + " found";
    }
}
