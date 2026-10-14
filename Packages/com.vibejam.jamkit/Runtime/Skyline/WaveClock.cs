using System;
using System.Collections.Generic;

namespace JamKit.Skyline
{
    /// <summary>One wave of the floor: its number (1-based), the running time at which it is due, and who comes
    /// (<see cref="EnemyDef.id"/> strings, one per bot).</summary>
    public sealed class WaveDef
    {
        public int number;
        public float time;
        public string[] enemies = new string[0];
    }

    /// <summary>The composition of every wave on Normal (docs/SKYLINE-FLOOR-REPOSSESS.md section 3; docs/SKYLINE-PLAN-FLOOR1.md
    /// Task W1, decision D9). Pure data and a pure check; the numbers are starting values to be balanced with the creator.</summary>
    public static class WavePlans
    {
        /// <summary>Waves 5 and up alternate between the two mixes (odd = A, even = B).</summary>
        public const int FirstAlternatingWave = 5;
        /// <summary>D9: a gondola joins every <see cref="GondolaEvery"/>th wave from <see cref="GondolaFrom"/> when none is alive.</summary>
        public const int GondolaFrom = 8, GondolaEvery = 4;

        /// <summary>Wave <paramref name="number"/> (1-based) on Normal: 1 = 3 drones; 2 = 3 drones + 1 floor-care; 3 = 2 drones +
        /// 2 floor-care; 4 = the gondola + 2 drones; 5 and up alternate A = 3 drones + 1 floor-care (odd), B = 2 drones + 2
        /// floor-care (even), and every fourth wave from 8 is a gondola wave (the gondola + 2 drones) while no gondola is alive.
        /// A gondola wave carries no floor-care: floor, walls and air together would threaten all three layers (design 2.6),
        /// so the floor-care makes way for the drones, as wave 4 does. Wave 4 always carries its gondola (none can be alive
        /// before it).</summary>
        public static WaveDef For(int number, bool gondolaAlive)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number), "waves are numbered from 1");
            string[] who;
            switch (number)
            {
                case 1: who = Mix(3, 0, false); break;
                case 2: who = Mix(3, 1, false); break;
                case 3: who = Mix(2, 2, false); break;
                case 4: who = Mix(2, 0, true); break;
                default:
                    if (number >= GondolaFrom && (number - GondolaFrom) % GondolaEvery == 0 && !gondolaAlive)
                        who = Mix(2, 0, true);
                    else if (number % 2 == 1) who = Mix(3, 1, false);
                    else who = Mix(2, 2, false);
                    break;
            }
            return new WaveDef { number = number, time = (number - 1) * FloorRules.WaveSeconds, enemies = who };
        }

        static string[] Mix(int drones, int floorCare, bool gondola)
        {
            var l = new List<string>();
            if (gondola) l.Add(SkylineEnemies.GondolaId);
            for (int i = 0; i < drones; i++) l.Add(SkylineEnemies.DroneId);
            for (int i = 0; i < floorCare; i++) l.Add(SkylineEnemies.FloorCareId);
            return l.ToArray();
        }

        /// <summary>The wave's rule breaches in words, or null when it is fair: every enemy known and allowed on this floor
        /// (the spotlight is excluded), no mixed aim models (design 2.6) and at most two threatened layers
        /// (<see cref="EncounterRules.MaxLayers"/>).</summary>
        public static string[] OpenLayersCheck(WaveDef w)
        {
            var problems = new List<string>();
            if (w == null || w.enemies == null || w.enemies.Length == 0)
                return new[] { "wave: no enemies" };
            string name = "wave " + w.number;
            var aims = new List<AimModel>();
            var layers = new HashSet<ThreatLayer>();
            foreach (var id in w.enemies)
            {
                var d = SkylineEnemies.Find(id);
                if (d == null) { problems.Add($"{name}: unknown enemy '{id}'"); continue; }
                if (id == SkylineEnemies.SpotlightId)
                    problems.Add($"{name}: the spotlight is not on this floor (its shots at where you are now may not share a wave with the drone's leading shots)");
                aims.Add(d.aim);
                layers.Add(d.layer);
            }
            if (!AimRules.CanShareEncounter(aims.ToArray()))
                problems.Add(name + ": mixes shots that lead you with shots at where you are now (2.6)");
            if (layers.Count > EncounterRules.MaxLayers)
                problems.Add($"{name}: threatens {layers.Count} layers; at most {EncounterRules.MaxLayers}, so one is left open (2.6)");
            return problems.Count == 0 ? null : problems.ToArray();
        }
    }

    /// <summary>The wave clock: wave 1 is due the moment the clock starts (the drop), wave n at (n-1) x interval of running time,
    /// whatever the player does. Hitch-safe: a long frame releases every wave that came due, once, in order. A due wave that
    /// would take the living count past the cap waits (one at most); an interval that arrives while one waits is skipped (D9)
    /// and counted. The caller feeds the living bot count and whether a gondola is alive; it spawns what <c>released</c> gets.</summary>
    public sealed class WaveClock
    {
        /// <summary>Slack for float accumulation, so 1875 frames of 0.016 s still reach 30 s.</summary>
        const double Epsilon = 1e-4;

        readonly int cap;
        readonly double interval;
        double elapsed;
        int nextNumber = 1;

        public WaveClock(int cap = FloorRules.WaveCap, float interval = FloorRules.WaveSeconds)
        {
            if (cap < 1) throw new ArgumentOutOfRangeException(nameof(cap));
            if (!(interval > 0f) || float.IsInfinity(interval)) throw new ArgumentOutOfRangeException(nameof(interval));
            this.cap = cap;
            this.interval = interval;
        }

        public bool Running { get; private set; }
        /// <summary>Waves handed out so far (not counting skipped intervals or the one that waits).</summary>
        public int Released { get; private set; }
        /// <summary>Intervals that came due while another wave waited: dropped, never spawned.</summary>
        public int Skipped { get; private set; }
        /// <summary>The due wave that waits for room under the cap, or null.</summary>
        public WaveDef Waiting { get; private set; }

        /// <summary>Seconds until the next interval comes due (0 when the clock is not running or the next wave is already
        /// due). It keeps counting while a wave waits: nothing delays the clock.</summary>
        public float SecondsToNext => Running ? (float)Math.Max(0.0, (nextNumber - 1) * interval - elapsed) : 0f;

        /// <summary>Starts the clock (at the drop): wave 1 is due at once, so the first positive tick releases it. Does
        /// nothing if it already runs; after <see cref="Stop"/> it resumes counting from where it stopped.</summary>
        public void Start() { Running = true; }

        /// <summary>The third asset is towed: no more waves, and the waiting wave is cancelled.</summary>
        public void Stop() { Running = false; Waiting = null; }

        /// <summary>A wreck: everything back to the beginning, not running.</summary>
        public void Reset()
        {
            Running = false;
            elapsed = 0;
            nextNumber = 1;
            Released = 0;
            Skipped = 0;
            Waiting = null;
        }

        /// <summary>Advances the clock by <paramref name="dt"/> seconds and appends every wave released by this call to
        /// <paramref name="released"/>, in order. <paramref name="living"/> is the bots alive now; bots of waves released within
        /// this same call count against the cap. Does nothing when stopped or when dt is zero, negative or not finite.</summary>
        public void Tick(float dt, int living, bool gondolaAlive, List<WaveDef> released)
        {
            if (!Running) return;
            if (!(dt > 0f) || float.IsInfinity(dt)) return;
            if (released == null) throw new ArgumentNullException(nameof(released));
            elapsed += dt;
            if (living < 0) living = 0;

            // The older, waiting wave goes first.
            if (Waiting != null && living + Waiting.enemies.Length <= cap)
            {
                Release(Waiting, released, ref living, ref gondolaAlive);
                Waiting = null;
            }

            while ((nextNumber - 1) * interval <= elapsed + Epsilon)
            {
                int n = nextNumber++;
                if (Waiting != null) { Skipped++; continue; }
                var w = WavePlans.For(n, gondolaAlive);
                w.time = (float)((n - 1) * interval);
                if (living + w.enemies.Length <= cap) Release(w, released, ref living, ref gondolaAlive);
                else Waiting = w;
            }
        }

        void Release(WaveDef w, List<WaveDef> released, ref int living, ref bool gondolaAlive)
        {
            released.Add(w);
            Released++;
            living += w.enemies.Length;
            foreach (var id in w.enemies)
                if (id == SkylineEnemies.GondolaId) gondolaAlive = true;
        }
    }
}
