using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Movement;

namespace JamKit.Skyline
{
    /// <summary>Drives the player's body in the headless sim, like a player's hands.</summary>
    public interface IPlayerBot
    {
        MoveInput Next(MoveSim sim, float time, float h);
    }

    /// <summary>A stationary enemy that fires at the player (docs/SKYLINE-DESIGN.md 2.5).</summary>
    public sealed class Shooter
    {
        public Vector3 position;
        public AimModel aim = AimModel.LeadsYou;
        /// <summary>m/s; 0 means a telegraphed hitscan (the spotlight).</summary>
        public float projectileSpeed = 30f;
        public float fireInterval = 1.2f;
        /// <summary>0..1, how much of the player's velocity a leading shot accounts for (rises with difficulty).</summary>
        public float leadFactor = 0.9f;
        public float projectileRadius = 0.15f;
        /// <summary>Seconds into the fight before the first shot.</summary>
        public float firstShotAt = 0.5f;
        /// <summary>Degrees of random aim error on a projectile (uniform over a cone); 0 is a perfect shot.</summary>
        public float aimSpreadDeg;
        /// <summary>A telegraphed hitscan's beam (projectileSpeed 0): its telegraph, lock, follow rate and hit radius.</summary>
        public float beamTelegraph = 0.8f, beamLockBefore = 0.25f, beamFollowRate = 10f, beamHitRadius = 0.6f;

        internal float nextFire = -1f;
        internal TelegraphedShot telegraphShot;
    }

    /// <summary>
    /// A headless fight: the real movement engine moving the player, stationary shooters, straight projectiles and the
    /// spotlight's telegraphed hitscan. It measures how often a way of moving gets hit (the dodging measurement, 2.6).
    /// The seed drives the shooters' aim error, so a fight repeats exactly.
    /// </summary>
    public sealed class CombatSim
    {
        struct Projectile
        {
            public Vector3 pos, vel;
            public float radius, age;
        }

        const float MaxProjectileAge = 4f;

        readonly MoveSim _player;
        readonly IPlayerBot _bot;
        readonly List<Shooter> _shooters;
        readonly List<Projectile> _shots = new List<Projectile>();
        readonly Rng _rng;

        public int Hits { get; private set; }
        public int ShotsFired { get; private set; }
        public float Time { get; private set; }

        public CombatSim(MoveSim player, IPlayerBot bot, IEnumerable<Shooter> shooters, ulong seed = 1)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _shooters = new List<Shooter>(shooters);
            _rng = new Rng(seed);
            foreach (var s in _shooters)
            {
                s.nextFire = s.firstShotAt;
                s.telegraphShot = null;   // a shooter reused across fights starts with no beam in flight
            }
        }

        public void Run(float seconds)
        {
            float h = 1f / _player.def.tickRate;
            int steps = (int)MathF.Ceiling(seconds / h);
            for (int i = 0; i < steps; i++) Step(h);
        }

        Vector3 Chest => _player.Position + new Vector3(0f, _player.Height * 0.5f, 0f);

        void Step(float h)
        {
            var input = _bot.Next(_player, Time, h);
            _player.Step(in input, h);
            Time += h;

            foreach (var s in _shooters)
            {
                if (s.telegraphShot != null && s.telegraphShot.Active)
                {
                    if (s.telegraphShot.Step(h, Chest, out bool hit) && hit) Hits++;
                    continue;
                }
                if (Time < s.nextFire) continue;
                s.nextFire = Time + s.fireInterval;
                ShotsFired++;
                if (s.projectileSpeed <= 0f)
                {
                    if (s.telegraphShot == null)
                        s.telegraphShot = new TelegraphedShot
                        {
                            telegraph = s.beamTelegraph, lockBefore = s.beamLockBefore, followRate = s.beamFollowRate, hitRadius = s.beamHitRadius,
                        };
                    s.telegraphShot.Start(Chest);
                    continue;
                }
                Vector3 aimAt = s.aim == AimModel.LeadsYou
                    ? Aim.LeadPoint(s.position, Chest, _player.Velocity, s.projectileSpeed, s.leadFactor)
                    : Chest;
                Vector3 dir = aimAt - s.position;
                float len = dir.Length();
                if (len < 1e-4f) continue;
                _shots.Add(new Projectile { pos = s.position, vel = Spread(dir / len, s.aimSpreadDeg) * s.projectileSpeed, radius = s.projectileRadius });
            }

            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var p = _shots[i];
                p.pos += p.vel * h;
                p.age += h;
                if (HitsPlayer(p.pos, p.radius)) { Hits++; _shots.RemoveAt(i); continue; }
                if (p.age > MaxProjectileAge) { _shots.RemoveAt(i); continue; }
                _shots[i] = p;
            }
        }

        /// <summary>The direction tilted by a random error uniform over a cone of <paramref name="deg"/> degrees.</summary>
        Vector3 Spread(Vector3 dir, float deg)
        {
            if (!(deg > 0f)) return dir;
            Vector3 side = Vector3.Normalize(Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX));
            Vector3 up = Vector3.Cross(side, dir);
            float angle = _rng.Range(0f, 2f * MathF.PI);
            float r = MathF.Tan(deg * MathF.PI / 180f) * MathF.Sqrt(_rng.NextFloat());
            return Vector3.Normalize(dir + (side * MathF.Cos(angle) + up * MathF.Sin(angle)) * r);
        }

        /// <summary>The player's capsule: a vertical segment from feet+radius to feet+height-radius, thickened by the radius.</summary>
        bool HitsPlayer(Vector3 point, float radius)
        {
            float r = _player.def.radius;
            Vector3 a = _player.Position + new Vector3(0f, r, 0f);
            Vector3 b = _player.Position + new Vector3(0f, MathF.Max(r, _player.Height - r), 0f);
            Vector3 ab = b - a;
            float t = Vector3.Dot(point - a, ab) / MathF.Max(1e-6f, Vector3.Dot(ab, ab));
            t = MathF.Max(0f, MathF.Min(1f, t));
            return Vector3.Distance(point, a + ab * t) <= r + radius;
        }
    }

    public struct DodgeResult
    {
        /// <summary>straightHits and jinkHits are the means below, rounded.</summary>
        public int straightHits, jinkHits, shots;
        /// <summary>Each bot's hits averaged over the seeds run.</summary>
        public float straightHitsMean, jinkHitsMean;
        public int seeds;
        public float seconds;
        public float Ratio => jinkHitsMean > 0f ? straightHitsMean / jinkHitsMean : float.PositiveInfinity;
    }

    /// <summary>The dodging measurement (2.6): the same fight run by a straight-line bot and a jinking bot. Target on
    /// Normal: the straight-line bot takes about twice the hits, and neither goes unhit.</summary>
    public static class DodgeReport
    {
        /// <summary>Both bots run with seeds <paramref name="seed"/>..<paramref name="seed"/>+<paramref name="seeds"/>-1 (the
        /// seed drives the jinking bot's reversals and the shooters' aim error) and their hits are averaged. With no aim
        /// error the straight-line bot's runs are identical.</summary>
        public static DodgeResult Run(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, float seconds, ulong seed, int seeds = 5)
        {
            if (seeds < 1) seeds = 1;
            int straightTotal = 0, jinkTotal = 0, shots = 0;
            for (int i = 0; i < seeds; i++)
            {
                ulong s = seed + (ulong)i;
                var straight = Fight(makeWorld(), new StraightLineBot(), makeShooters(), seconds, s);
                straightTotal += straight.Hits;
                if (i == 0) shots = straight.ShotsFired;
                jinkTotal += Fight(makeWorld(), new JinkingBot(s), makeShooters(), seconds, s).Hits;
            }
            float straightMean = (float)straightTotal / seeds, jinkMean = (float)jinkTotal / seeds;
            return new DodgeResult
            {
                straightHits = (int)MathF.Round(straightMean), straightHitsMean = straightMean,
                jinkHits = (int)MathF.Round(jinkMean), jinkHitsMean = jinkMean,
                seeds = seeds, shots = shots, seconds = seconds,
            };
        }

        /// <summary>One bot's hits in each of the fights with seeds <paramref name="seed"/>..<paramref name="seed"/>+<paramref name="seeds"/>-1
        /// (<paramref name="makeBot"/> gets the seed): the scripted clean-line routes use this to show a route takes no hits.</summary>
        public static int[] HitsPerSeed(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, Func<ulong, IPlayerBot> makeBot,
            float seconds, ulong seed, int seeds = 5)
        {
            if (seeds < 1) seeds = 1;
            var hits = new int[seeds];
            for (int i = 0; i < seeds; i++)
            {
                ulong s = seed + (ulong)i;
                hits[i] = Fight(makeWorld(), makeBot(s), makeShooters(), seconds, s).Hits;
            }
            return hits;
        }

        static CombatSim Fight(IMoveWorld world, IPlayerBot bot, List<Shooter> shooters, float seconds, ulong seed)
        {
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(Vector3.Zero, 0f, Vector3.Zero);
            var c = new CombatSim(sim, bot, shooters, seed);
            c.Run(seconds);
            return c;
        }
    }
}
