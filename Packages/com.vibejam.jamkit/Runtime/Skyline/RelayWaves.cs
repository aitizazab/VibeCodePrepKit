using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>
    /// The Relay's enemy waves (docs/SKYLINE-RELAY.md): a wave at the start and then one every <see cref="Interval"/> seconds,
    /// never more than <see cref="Cap"/> bots alive (a wave that doesn't fit waits for room; one waiting at most), from
    /// <see cref="WavePlans"/>'s mixes (the floor's: drones and floor-care units; a gondola, which needs a rail, is swapped for a
    /// drone). The spawn points are picked on the side away from the Unit's current target.
    /// </summary>
    public sealed class RelayWaves
    {
        public const float Interval = 20f;
        public const int Cap = 8;
        /// <summary>A bot never appears closer than this to the Unit.</summary>
        public const float MinPlayerDistance = 14f;

        readonly WaveClock _clock;
        readonly List<WaveDef> _released = new List<WaveDef>();

        public RelayWaves(int cap = Cap, float interval = Interval)
        {
            _clock = new WaveClock(cap, interval);
        }

        public int Released => _clock.Released;
        public int Skipped => _clock.Skipped;
        public bool Waiting => _clock.Waiting != null;

        /// <summary>Starts the clock: the first wave is due at once.</summary>
        public void Start() => _clock.Start();

        public void Stop() => _clock.Stop();

        public void Reset() => _clock.Reset();

        /// <summary>Advances by <paramref name="dt"/>; the waves that came due are appended to <paramref name="due"/> as the lists
        /// of enemy ids to spawn (gondolas swapped for drones).</summary>
        public void Tick(float dt, int living, List<string[]> due)
        {
            _released.Clear();
            _clock.Tick(dt, living, false, _released);
            foreach (var w in _released) due.Add(Playable(w.enemies));
        }

        public static string[] Playable(string[] enemies)
        {
            var r = new string[enemies.Length];
            for (int i = 0; i < r.Length; i++) r[i] = enemies[i] == SkylineEnemies.GondolaId ? SkylineEnemies.DroneId : enemies[i];
            return r;
        }

        /// <summary>Picks up to <paramref name="count"/> distinct points from <paramref name="candidates"/>, the farthest from
        /// <paramref name="target"/> first (flat distance), none closer than <see cref="MinPlayerDistance"/> to the Unit. Among
        /// the far half the order is shuffled by <paramref name="rng"/>, so two waves from the same side don't use the same
        /// doors. Fewer come back when too few qualify.</summary>
        public static List<Vector3> PickSpawns(IReadOnlyList<Vector3> candidates, Vector3 target, Vector3 player, int count, Rng rng)
        {
            var ok = new List<Vector3>();
            foreach (var c in candidates)
                if (Flat(c, player) >= MinPlayerDistance) ok.Add(c);
            ok.Sort((a, b) => Flat(b, target).CompareTo(Flat(a, target)));
            // shuffle the far half (at least as many as are asked for) so repeated waves vary
            int pool = Math.Min(ok.Count, Math.Max(count, (ok.Count + 1) / 2));
            for (int i = pool - 1; i > 0; i--)
            {
                int j = (int)(rng.NextUInt() % (uint)(i + 1));
                var tmp = ok[i]; ok[i] = ok[j]; ok[j] = tmp;
            }
            if (ok.Count > count) ok.RemoveRange(count, ok.Count - count);
            return ok;
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
