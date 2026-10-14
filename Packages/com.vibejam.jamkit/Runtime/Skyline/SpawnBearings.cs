using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>What <see cref="SpawnBearings.Choose"/> needs to know: where the player is and looks, what the last wave did,
    /// whether an alarm is up, and which wave is arriving (docs/SKYLINE-FLOOR-REPOSSESS.md section 3).</summary>
    public struct SpawnQuery
    {
        /// <summary>The camera's horizontal half field of view when <see cref="viewHalfFovDeg"/> is left at 0.</summary>
        public const float DefaultHalfFovDeg = 55f;

        public Vector3 player;
        /// <summary>The way the player looks, degrees, 0 = +Z, clockwise from above (as <see cref="SpawnBearings.Bearing"/>).</summary>
        public float viewYawDeg;
        /// <summary>The camera's horizontal half-fov in degrees; 0 (unset) means <see cref="DefaultHalfFovDeg"/>.</summary>
        public float viewHalfFovDeg;
        /// <summary>The previous wave's mean bearing as seen from the player then; null for the first wave.</summary>
        public float? lastMeanBearing;
        /// <summary>The asset whose meter just started (CONCIERGE's alarm): the next wave comes from the doors nearest it.</summary>
        public Vector3? alarmAsset;
        public WaveDef wave;
        public ulong seed;
        /// <summary>Doors with a telegraph running (<see cref="WaveSpawner.Busy"/>): never chosen.</summary>
        public IReadOnlyList<string> busyDoors;
    }

    /// <summary>One bot of a wave and the door it arrives through.</summary>
    public sealed class SpawnAssignment
    {
        /// <summary>An <see cref="EnemyDef.id"/>.</summary>
        public string enemy = "";
        public SpawnDoor door;
    }

    /// <summary>The outcome of <see cref="SpawnBearings.Choose"/>.</summary>
    public sealed class SpawnChoice
    {
        /// <summary>One entry per bot of the wave, in the wave's order. Empty when <see cref="ok"/> is false.</summary>
        public List<SpawnAssignment> assignments = new List<SpawnAssignment>();
        /// <summary>The circular mean of the used doors' bearings from the player (degrees, -180..180, 0 = +Z).</summary>
        public float meanBearing;
        /// <summary>0: every rule held. 1: the 120-degree-from-the-last-wave rule was dropped. 2: the view rule within 25 m
        /// was dropped as well (the 15 m and 90-degree rules are never dropped).</summary>
        public int relaxed;
        /// <summary>False: no legal choice right now; the wave waits and the caller retries next frame.</summary>
        public bool ok;
    }

    /// <summary>
    /// Where a wave arrives (task W2; docs/SKYLINE-FLOOR-REPOSSESS.md section 3). The bots of a wave are split between TWO
    /// doors whose bearings from the player differ by at least 90 degrees, each bot at a door that
    /// <see cref="FloorRules.DoorAllows"/> it; the first door is at least 120 degrees from the previous wave's mean bearing;
    /// every door is at least 15 m from the player (flat distance) and, if within 25 m, outside the player's view cone. After
    /// an alarm the pair nearest the alarmed asset that obeys the rules wins. When no pair obeys everything the rules fall
    /// away in order (the 120, then the view rule); the 15 m and 90 degree rules never do, and then the wave waits
    /// (<see cref="SpawnChoice.ok"/> false). Pure and deterministic for a given query (the seed picks among equals).
    /// </summary>
    public static class SpawnBearings
    {
        /// <summary>Degrees, 0 along +Z, 90 along +X (the same as <see cref="EncounterRules.Bearing"/>).</summary>
        public static float Bearing(Vector3 from, Vector3 to) => EncounterRules.Bearing(from, to);

        /// <summary>The smallest angle between two bearings, 0..180.</summary>
        public static float AngleBetween(float a, float b)
        {
            float d = (a - b) % 360f;
            if (d < 0f) d += 360f;
            return d > 180f ? 360f - d : d;
        }

        /// <summary>The circular mean of some bearings in degrees (-180..180). Returns <paramref name="fallback"/> when they
        /// cancel out (two opposite bearings have no mean).</summary>
        public static float CircularMean(IReadOnlyList<float> bearings, float fallback)
        {
            double s = 0, c = 0;
            for (int i = 0; i < bearings.Count; i++)
            {
                double r = bearings[i] * Math.PI / 180.0;
                s += Math.Sin(r); c += Math.Cos(r);
            }
            if (s * s + c * c < 1e-6) return fallback;
            return (float)(Math.Atan2(s, c) * 180.0 / Math.PI);
        }

        sealed class Cand
        {
            public SpawnDoor door;
            public float bearing, dist;
        }

        /// <summary>Picks the doors for <see cref="SpawnQuery.wave"/>, or reports that none are legal right now.</summary>
        public static SpawnChoice Choose(SpawnQuery q, IReadOnlyList<SpawnDoor> doors)
        {
            var fail = new SpawnChoice { ok = false };
            var enemies = q.wave?.enemies;
            if (doors == null || enemies == null || enemies.Length == 0 || !Finite(q.player) || float.IsNaN(q.viewYawDeg))
                return fail;
            float halfFov = q.viewHalfFovDeg > 0f ? q.viewHalfFovDeg : SpawnQuery.DefaultHalfFovDeg;

            // Doors that are never rejected by a ladder step: free, and at least the minimum distance away.
            var cands = new List<Cand>();
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i];
                if (d == null || !Finite(d.position) || IsBusy(q.busyDoors, d.id)) continue;
                float dx = d.position.X - q.player.X, dz = d.position.Z - q.player.Z;
                float dist = MathF.Sqrt(dx * dx + dz * dz);
                if (dist < FloorRules.MinSpawnDistance) continue;
                cands.Add(new Cand { door = d, dist = dist, bearing = Bearing(q.player, d.position) });
            }

            for (int step = 0; step <= 2; step++)
            {
                bool useLast = step == 0 && q.lastMeanBearing.HasValue;
                bool useView = step <= 1;
                var choice = Search(q, enemies, cands, halfFov, useLast, useView);
                if (choice != null) { choice.relaxed = step; return choice; }
            }
            return fail;
        }

        static SpawnChoice Search(SpawnQuery q, string[] enemies, List<Cand> cands, float halfFov, bool useLast, bool useView)
        {
            // The view rule filters single doors.
            var legal = new List<Cand>();
            foreach (var c in cands)
            {
                if (useView && c.dist < FloorRules.ViewExclusionRange && AngleBetween(c.bearing, q.viewYawDeg) <= halfFov) continue;
                legal.Add(c);
            }

            bool single = enemies.Length < 2;
            SpawnChoice best = null;
            double bestCost = double.MaxValue;
            int ties = 0;
            ulong rng = q.seed ^ 0x9E3779B97F4A7C15UL;

            if (single)
            {
                foreach (var a in legal)
                {
                    if (!FloorRules.DoorAllows(a.door.kind, enemies[0])) continue;
                    if (useLast && AngleBetween(a.bearing, q.lastMeanBearing.Value) < FloorRules.MinFromLastWave) continue;
                    Consider(q, ref best, ref bestCost, ref ties, ref rng, a, null, enemies);
                }
                return best;
            }

            for (int i = 0; i < legal.Count; i++)
                for (int j = i + 1; j < legal.Count; j++)
                {
                    var a = legal[i]; var b = legal[j];
                    if (AngleBetween(a.bearing, b.bearing) < FloorRules.MinBearingSpread - FloorRules.BearingSlack) continue;
                    if (useLast)
                    {
                        // The pair's "first" door is the one further from the last wave; it must clear the 120.
                        float da = AngleBetween(a.bearing, q.lastMeanBearing.Value), db = AngleBetween(b.bearing, q.lastMeanBearing.Value);
                        if (Math.Max(da, db) < FloorRules.MinFromLastWave) continue;
                    }
                    Consider(q, ref best, ref bestCost, ref ties, ref rng, a, b, enemies);
                }
            return best;
        }

        // Scores a legal pair (or single door) and keeps it when it is the best so far; equals are chosen by reservoir
        // sampling with the seeded generator, so the result is deterministic for one query.
        static void Consider(SpawnQuery q, ref SpawnChoice best, ref double bestCost, ref int ties, ref ulong rng,
            Cand a, Cand b, string[] enemies)
        {
            var assigned = Assign(q, enemies, a, b, ref rng);
            if (assigned == null) return;
            double cost = 0;
            if (q.alarmAsset.HasValue)
            {
                cost = Vector3.Distance(a.door.position, q.alarmAsset.Value);
                if (b != null) cost += Vector3.Distance(b.door.position, q.alarmAsset.Value);
            }
            const double eps = 0.01;
            if (best == null || cost < bestCost - eps) { bestCost = cost; ties = 1; }
            else if (cost <= bestCost + eps)
            {
                ties++;
                if (NextBelow(ref rng, ties) != 0) return;   // keep the incumbent with probability (ties-1)/ties
                if (cost < bestCost) bestCost = cost;
            }
            else return;

            var used = new List<float> { a.bearing };
            if (b != null) used.Add(b.bearing);
            // The "first" door (further from the last wave, else a) gives the fallback mean when the two cancel out.
            float first = a.bearing;
            if (b != null && q.lastMeanBearing.HasValue
                && AngleBetween(b.bearing, q.lastMeanBearing.Value) > AngleBetween(a.bearing, q.lastMeanBearing.Value)) first = b.bearing;
            best = new SpawnChoice { assignments = assigned, meanBearing = CircularMean(used, first), ok = true };
        }

        // Splits the wave over the door(s). A bot goes to a door that allows it; both doors must end up used. Null when the
        // pair cannot host this wave.
        static List<SpawnAssignment> Assign(SpawnQuery q, string[] enemies, Cand a, Cand b, ref ulong rng)
        {
            int n = enemies.Length;
            var onA = new bool[n];
            var flex = new List<int>();
            int countA = 0, countB = 0;
            for (int i = 0; i < n; i++)
            {
                bool okA = FloorRules.DoorAllows(a.door.kind, enemies[i]);
                bool okB = b != null && FloorRules.DoorAllows(b.door.kind, enemies[i]);
                if (!okA && !okB) return null;
                if (okA && okB) { flex.Add(i); continue; }
                onA[i] = okA;
                if (okA) countA++; else countB++;
            }
            if (b == null)
            {
                var one = new List<SpawnAssignment>(n);
                for (int i = 0; i < n; i++) one.Add(new SpawnAssignment { enemy = enemies[i], door = a.door });
                return one;
            }
            // Flexible bots even the doors out; a tie goes to the door nearer the alarmed asset, else to the seeded door.
            bool tieToA = q.alarmAsset.HasValue
                ? Vector3.Distance(a.door.position, q.alarmAsset.Value) <= Vector3.Distance(b.door.position, q.alarmAsset.Value)
                : (Next(ref rng) & 1UL) == 0UL;
            foreach (int i in flex)
            {
                bool toA = countA < countB || (countA == countB && tieToA);
                onA[i] = toA;
                if (toA) countA++; else countB++;
            }
            if (countA == 0 || countB == 0) return null;
            var l = new List<SpawnAssignment>(n);
            for (int i = 0; i < n; i++) l.Add(new SpawnAssignment { enemy = enemies[i], door = onA[i] ? a.door : b.door });
            return l;
        }

        static bool IsBusy(IReadOnlyList<string> busy, string id)
        {
            if (busy == null) return false;
            for (int i = 0; i < busy.Count; i++) if (busy[i] == id) return true;
            return false;
        }

        static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

        // splitmix64
        static ulong Next(ref ulong s)
        {
            s += 0x9E3779B97F4A7C15UL;
            ulong z = s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        static ulong NextBelow(ref ulong s, int n) => Next(ref s) % (ulong)n;
    }

    /// <summary>
    /// The 2 s telegraph (task W2): <see cref="Order"/> starts every door's warning at once (a sound from its direction, a
    /// light, the door moving: the Unity door plays them when <c>telegraphStarted</c> lists it), and the bots of the order are
    /// due <see cref="FloorRules.DoorTelegraphSeconds"/> later. A door is <see cref="Busy"/> from the order until its bots
    /// are due; <see cref="Cancel"/> (a wreck) forgets every pending order.
    /// </summary>
    public sealed class WaveSpawner
    {
        sealed class Pending
        {
            public SpawnAssignment a;
            public float due;
            public bool announced;
        }

        readonly List<Pending> _pending = new List<Pending>();

        /// <summary>Orders the choice's bots at time <paramref name="now"/> (seconds, any steady clock). A choice that is not
        /// ok orders nothing.</summary>
        public void Order(SpawnChoice c, float now)
        {
            if (c == null || !c.ok || !float.IsFinite(now)) return;
            foreach (var a in c.assignments)
                _pending.Add(new Pending { a = a, due = now + FloorRules.DoorTelegraphSeconds });
        }

        /// <summary>Appends to <paramref name="telegraphStarted"/> each assignment whose telegraph has not been reported yet
        /// (first tick after its order), then to <paramref name="due"/> each whose 2 s are up, in order; due ones are removed.
        /// Either list may be null.</summary>
        public void Tick(float now, List<SpawnAssignment> due, List<SpawnAssignment> telegraphStarted)
        {
            if (!float.IsFinite(now) || _pending.Count == 0) return;
            foreach (var p in _pending)
                if (!p.announced) { p.announced = true; telegraphStarted?.Add(p.a); }
            const float slack = 1e-4f;
            for (int i = 0; i < _pending.Count;)
            {
                if (now + slack >= _pending[i].due) { due?.Add(_pending[i].a); _pending.RemoveAt(i); }
                else i++;
            }
        }

        /// <summary>True from an order until its bots are due: the door is mid-telegraph.</summary>
        public bool Busy(string doorId)
        {
            foreach (var p in _pending) if (p.a.door != null && p.a.door.id == doorId) return true;
            return false;
        }

        /// <summary>The ids of the doors mid-telegraph (feed it to <see cref="SpawnQuery.busyDoors"/>).</summary>
        public List<string> BusyDoors()
        {
            var l = new List<string>();
            foreach (var p in _pending)
                if (p.a.door != null && !l.Contains(p.a.door.id)) l.Add(p.a.door.id);
            return l;
        }

        public int PendingCount => _pending.Count;

        /// <summary>A wreck: every pending order is dropped, no bot arrives.</summary>
        public void Cancel() => _pending.Clear();
    }
}
