using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.Events;

namespace JamKit.Stealth
{
    /// <summary>
    /// The scene's collectibles counter (core <see cref="CollectibleSet"/>): every enabled <see cref="Collectible"/>
    /// registers with it. Ask <see cref="Remaining"/>, <see cref="AnyLeft"/>, <see cref="Summary"/>; hear
    /// <see cref="ItemCollected"/> and, once, <see cref="AllFound"/> / <see cref="onAllFound"/> when the last is picked up
    /// (Visiting Hours hooks its "I'm not sure if there's anything left to find..." line there). Story-free.
    /// </summary>
    public sealed class CollectibleCounter : MonoBehaviour
    {
        public UnityEvent onAllFound = new UnityEvent();
        [Tooltip("Say \"3 / 5 found\" on the HUD at each find.")]
        public bool announce = true;

        /// <summary>(the item, how many are left).</summary>
        public event Action<Collectible, int> ItemCollected;
        public event Action AllFound;

        static CollectibleCounter _current;
        readonly CollectibleSet _set = new CollectibleSet();
        readonly List<Collectible> _items = new List<Collectible>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _current = null;

        /// <summary>The scene's counter (made on first use if the scene has none).</summary>
        public static CollectibleCounter Current
        {
            get
            {
                if (_current == null) _current = FindFirstObjectByType<CollectibleCounter>();
                if (_current == null) _current = new GameObject("Collectibles").AddComponent<CollectibleCounter>();
                return _current;
            }
        }

        /// <summary>The scene's counter if it has one (never makes one).</summary>
        public static CollectibleCounter Existing => _current;

        public CollectibleSet Set => _set;
        public int Total => _set.Total;
        public int Remaining => _set.Remaining;
        public bool AnyLeft => _set.AnyLeft;
        public bool AllCollected => _set.AllFound;
        /// <summary>A template's own wording for the count (found, total): Night 2's house says "Memories: 2 of 5".
        /// Null: the set's summary.</summary>
        public Func<int, int, string> summaryFormat;
        public string Summary => summaryFormat != null ? summaryFormat(Total - Remaining, Total) : _set.Summary();

        void Awake()
        {
            if (_current == null || _current == this) _current = this;
            _set.Collected += OnCollected;
            _set.AllCollected += OnAll;
        }

        void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        internal int Register(Collectible c)
        {
            int i = _set.Register(string.IsNullOrEmpty(c.id) ? c.name : c.id);
            while (_items.Count <= i) _items.Add(null);
            _items[i] = c;
            return i;
        }

        internal bool Collect(int index) => _set.Collect(index);

        /// <summary>Puts one collectible back where it was (dropped). True if it had been collected.</summary>
        public bool Uncollect(int index)
        {
            if (!_set.Uncollect(index)) return false;
            if (index < _items.Count && _items[index] != null) _items[index].Show(true);
            return true;
        }

        /// <summary>Every collectible back in place (a restart).</summary>
        public void ResetAll()
        {
            _set.ResetFound();
            foreach (var c in _items) if (c != null) c.Show(true);
        }

        void OnCollected(int index, int remaining)
        {
            var item = index < _items.Count ? _items[index] : null;
            if (announce && GameRoot.Exists && remaining > 0) GameRoot.Hud.Message(_set.Summary(), 1.6f);
            ItemCollected?.Invoke(item, remaining);
        }

        void OnAll()
        {
            if (announce && GameRoot.Exists) GameRoot.Hud.Message(_set.Summary(), 2.5f);
            AllFound?.Invoke();
            onAllFound?.Invoke();
        }
    }
}
