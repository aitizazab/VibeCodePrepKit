using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// One tow meter (docs/SKYLINE-FLOOR-REPOSSESS.md, the objective): 10 s inside a tow circle takes the asset; leaving the
    /// circle PAUSES the meter, it never drains. Time is integrated, so the same seconds fill it however the frames are cut
    /// (a 0.5 s hitch, a 20 fps machine). <see cref="Done"/> is sticky until <see cref="Reset"/>.
    /// </summary>
    public sealed class TowMeter
    {
        // Float dt sums (625 x 0.016f) can land a hair under 10 s; this is far below one frame and far above that error.
        const double DoneSlack = 1e-4;

        double _seconds;
        bool _done;

        /// <summary>0..1: the share of <see cref="FloorRules.TowSeconds"/> spent inside the circle.</summary>
        public float Progress => _done ? 1f : (float)Math.Min(1.0, _seconds / FloorRules.TowSeconds);

        /// <summary>True from the first tick that put time into the meter (the alarm's cue); stays true until Reset.</summary>
        public bool Started => _seconds > 0.0 || _done;

        public bool Done => _done;

        /// <summary>Adds <paramref name="dt"/> seconds when <paramref name="inCircle"/>; does nothing outside, and nothing
        /// for a zero, negative, NaN or infinite dt.</summary>
        public void Tick(float dt, bool inCircle)
        {
            if (_done || !inCircle) return;
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            _seconds += dt;
            if (_seconds >= FloorRules.TowSeconds - DoneSlack) { _seconds = FloorRules.TowSeconds; _done = true; }
        }

        public void Reset() { _seconds = 0.0; _done = false; }
    }

    /// <summary>One line of the job sheet: an asset, the socket it sits on this run, and its meter.</summary>
    public sealed class JobEntry
    {
        public AssetDef asset;
        public SocketDef socket;
        public TowMeter meter = new TowMeter();
        public bool Towed => meter.Done;
    }

    /// <summary>
    /// The repossession objective: the job sheet (one asset per socket group, assets shuffled across the groups), the tow
    /// circles, and the events the rest of the floor reacts to. Pure: the Unity side feeds it the Unit's feet each frame.
    /// A circle is centred on its socket's position (the sockets are floor points, docs/SKYLINE-PLAN-FLOOR1.md layout sheet).
    /// </summary>
    public sealed class Repossession
    {
        /// <summary>The vertical reach of a circle: the Unit's feet must be within this of the socket's floor point, so the
        /// pit kiosk's circle does not fill from the bridge 6 m above it.</summary>
        public const float CircleHalfHeight = 2.5f;

        // Second PCG stream for the shuffle, so changing it never reshuffles the socket picks.
        const ulong ShuffleStream = 0x2545F4914F6CDD1DUL;

        readonly JobEntry[] _sheet;
        int _towed;
        int _active = -1;
        bool _allFired;
        readonly bool[] _startedFired;

        /// <summary>Picks one socket per group from <paramref name="sockets"/> (the floor's ten:
        /// <c>RepossessSites.Sockets()</c>) with <see cref="Pick(ulong, IReadOnlyList{SocketDef})"/>.</summary>
        public Repossession(IReadOnlyList<AssetDef> assets, ulong seed, IReadOnlyList<SocketDef> sockets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            SocketDef[] picks = Pick(seed, sockets);
            // The sheet keeps the assets' own order; which GROUP each asset lands in is the seeded shuffle.
            int[] perm = new int[picks.Length];
            for (int i = 0; i < perm.Length; i++) perm[i] = i;
            var rng = new Rng(seed, ShuffleStream);
            for (int i = perm.Length - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                int t = perm[i]; perm[i] = perm[j]; perm[j] = t;
            }
            int n = Math.Min(assets.Count, picks.Length);
            _sheet = new JobEntry[n];
            for (int i = 0; i < n; i++) _sheet[i] = new JobEntry { asset = assets[i], socket = picks[perm[i]] };
            _startedFired = new bool[n];
        }

        /// <summary>The floor's own ten sockets (<c>RepossessSites.Sockets()</c>).</summary>
        public Repossession(IReadOnlyList<AssetDef> assets, ulong seed) : this(assets, seed, RepossessSites.Sockets()) { }

        public IReadOnlyList<JobEntry> Sheet => _sheet;
        public int TowedCount => _towed;
        public bool AllTowed => _sheet.Length > 0 && _towed == _sheet.Length;

        /// <summary>The entry whose circle holds the feet (the nearer when circles overlap), else -1. Towed entries have no circle.</summary>
        public int ActiveCircle => _active;

        /// <summary>The first tick an asset's meter moves: the alarm (clamping is loud).</summary>
        public event Action<JobEntry> MeterStarted;
        /// <summary>The tick an asset's meter completes.</summary>
        public event Action<JobEntry> Towed;
        /// <summary>Once, when the third asset is towed.</summary>
        public event Action AllTowedEvent;

        /// <summary>Which circle holds <paramref name="feet"/>, ignoring towed entries (and what Tick uses).</summary>
        public int CircleAt(Vector3 feet)
        {
            if (float.IsNaN(feet.X) || float.IsNaN(feet.Y) || float.IsNaN(feet.Z)) return -1;
            int best = -1;
            float bestD2 = float.MaxValue;
            for (int i = 0; i < _sheet.Length; i++)
            {
                if (_sheet[i].Towed) continue;
                Vector3 c = _sheet[i].socket.position;
                float dx = feet.X - c.X, dz = feet.Z - c.Z, dy = feet.Y - c.Y;
                float d2 = dx * dx + dz * dz;
                if (d2 > FloorRules.CircleRadius * FloorRules.CircleRadius) continue;
                if (Math.Abs(dy) > CircleHalfHeight) continue;
                if (d2 < bestD2) { bestD2 = d2; best = i; }
            }
            return best;
        }

        /// <summary>Advances the objective by <paramref name="dt"/> seconds with the Unit's feet at <paramref name="feet"/>
        /// (3D: horizontal distance within <see cref="FloorRules.CircleRadius"/> and |dy| within <see cref="CircleHalfHeight"/>).
        /// Events fire in this order within one call: MeterStarted, Towed, AllTowedEvent.</summary>
        public void Tick(float dt, Vector3 feet)
        {
            _active = CircleAt(feet);
            if (_active < 0) return;
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            JobEntry e = _sheet[_active];
            e.meter.Tick(dt, true);
            if (e.meter.Started && !_startedFired[_active])
            {
                _startedFired[_active] = true;
                MeterStarted?.Invoke(e);
            }
            if (e.meter.Done)
            {
                _towed++;
                _active = -1;           // the asset is gone, so is its circle
                Towed?.Invoke(e);
                if (AllTowed && !_allFired)
                {
                    _allFired = true;
                    AllTowedEvent?.Invoke();
                }
            }
        }

        /// <summary>A wreck: meters back to 0, assets back on their sockets, the SAME sockets; the alarm can fire again.</summary>
        public void Reset()
        {
            for (int i = 0; i < _sheet.Length; i++) { _sheet[i].meter.Reset(); _startedFired[i] = false; }
            _towed = 0;
            _active = -1;
            _allFired = false;
        }

        /// <summary>One socket per group from the floor's own ten (<c>RepossessSites.Sockets()</c>).</summary>
        public static SocketDef[] Pick(ulong seed) => Pick(seed, RepossessSites.Sockets());

        /// <summary>One socket per group (Pit, Inside, High order), chosen from <paramref name="sockets"/> by
        /// <paramref name="seed"/>; the same seed always gives the same picks.</summary>
        public static SocketDef[] Pick(ulong seed, IReadOnlyList<SocketDef> sockets)
        {
            if (sockets == null) throw new ArgumentNullException(nameof(sockets));
            IReadOnlyList<SocketDef> all = sockets;
            var groups = (SocketGroup[])Enum.GetValues(typeof(SocketGroup));
            var rng = new Rng(seed);
            var result = new SocketDef[groups.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                var inGroup = new List<SocketDef>();
                for (int i = 0; i < all.Count; i++) if (all[i].group == groups[g]) inGroup.Add(all[i]);
                if (inGroup.Count == 0) throw new InvalidOperationException("no socket in group " + groups[g]);
                result[g] = inGroup[rng.Range(0, inGroup.Count)];
            }
            return result;
        }
    }
}
