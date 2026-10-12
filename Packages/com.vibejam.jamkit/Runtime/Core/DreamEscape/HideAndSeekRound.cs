using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.DreamEscape
{
    /// <summary>How the round ended.</summary>
    public enum RoundEnd { None, AllFoundAndFound, CaughtThreeTimes }

    /// <summary>What a catch cost (for the presenter: the memory flying out of her hands to its new place).</summary>
    public struct CatchCost
    {
        /// <summary>1-3: which catch this was.</summary>
        public int catchNumber;
        /// <summary>The memory the house took back (null: she had nothing since the table) and where it hid it.</summary>
        public string memoryTaken;
        public Vector3 newPosition, newStandAt;
        public string newRoom;
        /// <summary>The third catch: the round is over.</summary>
        public bool roundOver;
    }

    /// <summary>
    /// The hide-and-seek's rules (docs/redesign/house.md "Hide-and-seek"), pure. Eight memories: the place card is found at
    /// the table, seven are hidden round the house. <b>Getting found costs</b>: each time Theo tags her the house takes
    /// back the last memory she found and hides it somewhere new (one of its other places, the furthest from where she
    /// was caught that it hasn't used), and he counts again, a little less each time; the third catch ends the round
    /// early, and she goes into Lena's battle with only what she's holding. Finding every memory plays the creator's
    /// line; then he comes for her, and his finding her ends the round.
    /// </summary>
    public sealed class HideAndSeekRound
    {
        public const int MaxCatches = 3;
        /// <summary>How long he counts (s) before the first search, and after each catch.</summary>
        public static readonly float[] CountSeconds = { 20f, 15f, 12f };

        readonly DreamHouse _house;
        readonly List<string> _found = new List<string>();
        readonly Dictionary<string, int> _place = new Dictionary<string, int>();   // -1: its first place, else an alt index
        readonly Dictionary<string, List<int>> _used = new Dictionary<string, List<int>>();
        int _catches;
        RoundEnd _end;
        bool _allFoundSaid;

        /// <summary>Theo's room is the base (shut) until she's found this many memories (the place card counts), unless she
        /// told Elias about Theo's team (Day 2's bedside): then it's open from the start.</summary>
        public const int TheoRoomOpensAt = 3;
        public static bool TheoRoomOpen(bool toldAboutTheosTeam, int found) => toldAboutTheosTeam || found >= TheoRoomOpensAt;

        public HideAndSeekRound(DreamHouse house)
        {
            _house = house;
            Reset();
        }

        public int Total => _house.memories.Count;
        public int FoundCount => _found.Count;
        public int Catches => _catches;
        public bool Over => _end != RoundEnd.None;
        public RoundEnd End => _end;
        /// <summary>Every memory found (the creator's line has played): Theo comes for her.</summary>
        public bool AllFound => _found.Count >= Total;
        public IReadOnlyList<string> Found => _found;
        public string Counter => "Memories " + _found.Count + " of " + Total;
        /// <summary>How long Theo counts now (shorter after each catch).</summary>
        public float CountTime => CountSeconds[Math.Min(_catches, CountSeconds.Length - 1)];

        public void Reset()
        {
            _found.Clear();
            _place.Clear();
            _used.Clear();
            foreach (var m in _house.memories) { _place[m.id] = -1; _used[m.id] = new List<int> { -1 }; }
            _catches = 0;
            _end = RoundEnd.None;
            _allFoundSaid = false;
        }

        public bool IsFound(string id) => _found.Contains(id);

        /// <summary>Where a memory is now (its first place, or where the house moved it).</summary>
        public Vector3 PositionOf(string id)
        {
            var m = _house.Memory(id);
            int k = _place[id];
            return k < 0 ? m.position : m.altPositions[k];
        }

        public Vector3 StandAtOf(string id)
        {
            var m = _house.Memory(id);
            int k = _place[id];
            return k < 0 ? m.standAt : m.altStandAt[k];
        }

        public string RoomOf(string id)
        {
            var m = _house.Memory(id);
            int k = _place[id];
            return k < 0 ? m.room : m.altRooms[k];
        }

        /// <summary>She took a memory. True the first time (it counts); <paramref name="last"/> is true when it was the last
        /// one (the creator's line plays, once).</summary>
        public bool Collect(string id, out bool last)
        {
            last = false;
            if (Over || _house.Memory(id) == null || _found.Contains(id)) return false;
            _found.Add(id);
            if (AllFound && !_allFoundSaid) { _allFoundSaid = true; last = true; }
            return true;
        }

        /// <summary>Theo tagged her at <paramref name="at"/>. Returns the cost; after every memory is found, his finding her
        /// just ends the round (no cost: she let him).</summary>
        public CatchCost Caught(Vector3 at)
        {
            var cost = new CatchCost();
            if (Over) return cost;
            if (AllFound)
            {
                _end = RoundEnd.AllFoundAndFound;
                cost.roundOver = true;
                return cost;
            }
            _catches++;
            cost.catchNumber = _catches;
            if (_catches >= MaxCatches)
            {
                _end = RoundEnd.CaughtThreeTimes;
                cost.roundOver = true;
                return cost;
            }
            // The last memory she found (not the one at the table if there's another) goes back into the house.
            if (_found.Count > 0)
            {
                string id = _found[_found.Count - 1];
                _found.RemoveAt(_found.Count - 1);
                var m = _house.Memory(id);
                int best = -2;
                float bestD = -1f;
                for (int k = -1; k < m.altPositions.Count; k++)
                {
                    if (_used[id].Contains(k) && _used[id].Count <= m.altPositions.Count) continue;   // its unused places first
                    var p = k < 0 ? m.position : m.altPositions[k];
                    float d = Vector3.Distance(p, at);
                    if (d > bestD) { bestD = d; best = k; }
                }
                if (best == -2) best = _place[id] == -1 ? 0 : -1;
                _place[id] = best;
                if (!_used[id].Contains(best)) _used[id].Add(best);
                cost.memoryTaken = id;
                cost.newPosition = PositionOf(id);
                cost.newStandAt = StandAtOf(id);
                cost.newRoom = RoomOf(id);
            }
            return cost;
        }

        /// <summary>A debug or chapter jump: these memories count as found.</summary>
        public void MarkFound(IEnumerable<string> ids)
        {
            foreach (var id in ids) if (_house.Memory(id) != null && !_found.Contains(id)) _found.Add(id);
            if (AllFound) _allFoundSaid = true;
        }
    }
}
