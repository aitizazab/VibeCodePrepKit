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
        /// <summary>Above 0: fires at where the player's chest was this many seconds ago (the drone's lagged aim, no lead).</summary>
        public float aimLagSeconds;
        /// <summary>A telegraphed hitscan's beam (projectileSpeed 0): its telegraph, lock, follow rate and hit radius.</summary>
        public float beamTelegraph = 0.8f, beamLockBefore = 0.25f, beamFollowRate = 10f, beamHitRadius = 0.6f;
        /// <summary>It fires only when the player's chest is within this many metres (and in line of sight); unlimited by default.</summary>
        public float range = float.PositiveInfinity;
        /// <summary>What a hit takes off the player's plating and chassis.</summary>
        public float damage = 10f;

        internal float nextFire = -1f;
        internal bool blind;
        internal TelegraphedShot telegraphShot;
    }

    /// <summary>Segments against cover boxes: pure and small, the fight's line-of-sight and projectile-stopping tests.</summary>
    public static class CoverGeometry
    {
        /// <summary>True when the segment <paramref name="a"/>-<paramref name="b"/> passes through the box (the slab test; a
        /// segment that only starts or ends inside counts, and so does one that grazes a face). A segment with a NaN or infinite end counts as
        /// blocked: an undefined line is never a clear line of sight, and a bolt with one is stopped.</summary>
        public static bool SegmentHitsBox(Vector3 a, Vector3 b, CoverBox box)
        {
            if (!Finite(a) || !Finite(b)) return true;
            Vector3 d = b - a;
            float t0 = 0f, t1 = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = Component(a, axis), dir = Component(d, axis), lo = Component(box.min, axis), hi = Component(box.max, axis);
                if (MathF.Abs(dir) < 1e-9f)
                {
                    if (o < lo || o > hi) return false;
                    continue;
                }
                float ta = (lo - o) / dir, tb = (hi - o) / dir;
                if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }
                if (ta > t0) t0 = ta;
                if (tb < t1) t1 = tb;
                if (t0 > t1) return false;
            }
            return true;
        }

        /// <summary>True when any box in <paramref name="cover"/> blocks the segment. A null or empty list blocks nothing.</summary>
        public static bool Blocked(Vector3 a, Vector3 b, IReadOnlyList<CoverBox> cover)
        {
            if (cover == null) return false;
            for (int i = 0; i < cover.Count; i++)
                if (SegmentHitsBox(a, b, cover[i])) return true;
            return false;
        }

        /// <summary>How far (m, on the floor plan) a point is from the nearest box's footprint; 0 inside one. Infinity with no cover.</summary>
        public static float DistanceToCover(Vector3 p, IReadOnlyList<CoverBox> cover)
        {
            float best = float.PositiveInfinity;
            if (cover == null) return best;
            for (int i = 0; i < cover.Count; i++)
            {
                float dx = MathF.Max(MathF.Max(cover[i].min.X - p.X, p.X - cover[i].max.X), 0f);
                float dz = MathF.Max(MathF.Max(cover[i].min.Z - p.Z, p.Z - cover[i].max.Z), 0f);
                best = MathF.Min(best, MathF.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        /// <summary>True when any box in <paramref name="cover"/>, grown by <paramref name="radius"/> on every side, blocks the
        /// segment: a projectile of that radius is stopped by a box its edge touches, not only by one its centre line crosses.</summary>
        public static bool BlockedThick(Vector3 a, Vector3 b, IReadOnlyList<CoverBox> cover, float radius)
        {
            if (cover == null) return false;
            if (!(radius > 0f)) return Blocked(a, b, cover);
            var grow = new Vector3(radius);
            for (int i = 0; i < cover.Count; i++)
                if (SegmentHitsBox(a, b, new CoverBox { min = cover[i].min - grow, max = cover[i].max + grow })) return true;
            return false;
        }

        /// <summary>The box moved by <paramref name="offset"/>.</summary>
        public static CoverBox Translated(CoverBox box, Vector3 offset) => new CoverBox { min = box.min + offset, max = box.max + offset };

        static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

        static bool Finite(Vector3 v) =>
            !(float.IsNaN(v.X) || float.IsInfinity(v.X) || float.IsNaN(v.Y) || float.IsInfinity(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.Z));
    }

    /// <summary>
    /// A headless fight: the real movement engine moving the player, stationary shooters, straight projectiles and the
    /// spotlight's telegraphed hitscan. It measures how often a way of moving gets hit (the dodging measurement, 2.6).
    /// The seed drives the shooters' aim error, so a fight repeats exactly. Cover boxes stop shots and block sight: a
    /// shooter fires only when the player's chest is in range and in line of sight (it holds its shot until then), a
    /// projectile that meets a box is destroyed, and a beam hits only if sight holds the moment it fires. Every hit also
    /// takes the shooter's damage off a <see cref="PlayerArmor"/> (plating 100 over chassis 100), to time a wreck.
    /// </summary>
    public sealed class CombatSim
    {
        struct Projectile
        {
            public Vector3 pos, vel;
            public float radius, age, damage;
        }

        const float MaxProjectileAge = 4f;
        /// <summary>The most a shooter waits (s, seeded) after it regains sight of the player before it fires (re-staggers a group).</summary>
        public const float ReacquireJitter = 0.4f;

        readonly MoveSim _player;
        readonly IPlayerBot _bot;
        readonly List<Shooter> _shooters;
        readonly List<Projectile> _shots = new List<Projectile>();
        readonly List<CoverBox> _cover;
        readonly Rng _rng;
        readonly PositionTrail _trail = new PositionTrail();

        public int Hits { get; private set; }
        public int ShotsFired { get; private set; }
        public float Time { get; private set; }
        public PlayerArmor Armor { get; } = new PlayerArmor();
        /// <summary>When the Unit's armour was wrecked, or -1 while it hasn't been. Hits keep counting after a wreck.</summary>
        public float WreckedAt { get; private set; } = -1f;

        public CombatSim(MoveSim player, IPlayerBot bot, IEnumerable<Shooter> shooters, ulong seed = 1, IEnumerable<CoverBox> cover = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _shooters = new List<Shooter>(shooters);
            _cover = cover != null ? new List<CoverBox>(cover) : new List<CoverBox>();
            _rng = new Rng(seed);
            foreach (var s in _shooters)
            {
                s.nextFire = s.firstShotAt;
                s.telegraphShot = null;   // a shooter reused across fights starts with no beam in flight
                s.blind = false;
            }
        }

        public void Run(float seconds)
        {
            float h = 1f / _player.def.tickRate;
            int steps = (int)MathF.Ceiling(seconds / h);
            for (int i = 0; i < steps; i++) Step(h);
        }

        Vector3 Chest => _player.Position + new Vector3(0f, _player.Height * 0.5f, 0f);

        /// <summary>The player's chest is within the shooter's range and nothing stands between its muzzle and the chest.</summary>
        bool Sees(Shooter s)
        {
            Vector3 chest = Chest;
            return Vector3.Distance(s.position, chest) <= s.range && !CoverGeometry.Blocked(s.position, chest, _cover);
        }

        void Hit(float damage)
        {
            Hits++;
            Armor.Damage(damage);
            if (Armor.Wrecked && WreckedAt < 0f) WreckedAt = Time;
        }

        void Step(float h)
        {
            var input = _bot.Next(_player, Time, h);
            _player.Step(in input, h);
            Time += h;
            _trail.Record(Time, Chest);

            foreach (var s in _shooters)
            {
                if (s.telegraphShot != null && s.telegraphShot.Active)
                {
                    if (s.telegraphShot.Step(h, Chest, out bool hit) && hit && Sees(s)) Hit(s.damage);
                    continue;
                }
                if (Time < s.nextFire) continue;
                if (!Sees(s))
                {
                    s.blind = true;   // it holds its shot until it has the player in range and in sight
                    continue;
                }
                if (s.blind)
                {
                    // regaining sight: a seeded reaction delay, so a group that sees the player again doesn't fire in lockstep
                    s.blind = false;
                    s.nextFire = Time + _rng.Range(0f, ReacquireJitter);
                    continue;
                }
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
                Vector3 aimAt = s.aimLagSeconds > 0f
                    ? Aim.LaggedPoint(_trail, Time, s.aimLagSeconds, Chest)
                    : s.aim == AimModel.LeadsYou
                        ? Aim.LeadPoint(s.position, Chest, _player.Velocity, s.projectileSpeed, s.leadFactor)
                        : Chest;
                Vector3 dir = aimAt - s.position;
                float len = dir.Length();
                if (len < 1e-4f) continue;
                _shots.Add(new Projectile
                {
                    pos = s.position, vel = Spread(dir / len, s.aimSpreadDeg) * s.projectileSpeed, radius = s.projectileRadius, damage = s.damage,
                });
            }

            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var p = _shots[i];
                Vector3 from = p.pos;
                p.pos += p.vel * h;
                p.age += h;
                if (CoverGeometry.BlockedThick(from, p.pos, _cover, p.radius)) { _shots.RemoveAt(i); continue; }   // the cover takes it (its edge counts)
                if (HitsPlayer(p.pos, p.radius)) { Hit(p.damage); _shots.RemoveAt(i); continue; }
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
        /// <summary>Hits of the worst and best seed for each bot (the spread behind the means).</summary>
        public int straightMin, straightMax, jinkMin, jinkMax;
        public int seeds;
        public float seconds;
        public float Ratio => jinkHitsMean > 0f ? straightHitsMean / jinkHitsMean : float.PositiveInfinity;
    }

    /// <summary>One fight's outcome for the bot that ran it.</summary>
    public struct DodgeFightResult
    {
        /// <summary>Hits taken, and shots the shooters actually fired (in range and in sight).</summary>
        public int hits, shots;
        /// <summary>When the Unit's armour (plating 100 over chassis 100) was wrecked, or -1.</summary>
        public float wreckedAt;
    }

    /// <summary>The dodging measurement (2.6): the same fight run by a straight-line bot and a jinking bot. Target on
    /// Normal: the straight-line bot takes about twice the hits, and neither goes unhit.</summary>
    public static class DodgeReport
    {
        /// <summary>Both bots run with seeds <paramref name="seed"/>..<paramref name="seed"/>+<paramref name="seeds"/>-1 (the
        /// seed drives the jinking bot's reversals and the shooters' aim error) and their hits are averaged. With no aim
        /// error the straight-line bot's runs are identical. <paramref name="makeStraight"/> and <paramref name="makeJink"/>
        /// replace M0's lane bots (an encounter's own lane); <paramref name="cover"/> is in the fight's frame.</summary>
        public static DodgeResult Run(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, float seconds, ulong seed, int seeds = 5,
            IReadOnlyList<CoverBox> cover = null, Func<ulong, IPlayerBot> makeStraight = null, Func<ulong, IPlayerBot> makeJink = null)
        {
            if (seeds < 1) seeds = 1;
            if (makeStraight == null) makeStraight = s => new StraightLineBot();
            if (makeJink == null) makeJink = s => new JinkingBot(s);
            int straightTotal = 0, jinkTotal = 0, shots = 0;
            int straightMin = int.MaxValue, straightMax = 0, jinkMin = int.MaxValue, jinkMax = 0;
            for (int i = 0; i < seeds; i++)
            {
                ulong s = seed + (ulong)i;
                var straight = Fight(makeWorld(), makeStraight(s), makeShooters(), seconds, s, cover, Vector3.Zero);
                straightTotal += straight.Hits;
                straightMin = Math.Min(straightMin, straight.Hits);
                straightMax = Math.Max(straightMax, straight.Hits);
                if (i == 0) shots = straight.ShotsFired;
                var jink = Fight(makeWorld(), makeJink(s), makeShooters(), seconds, s, cover, Vector3.Zero);
                jinkTotal += jink.Hits;
                jinkMin = Math.Min(jinkMin, jink.Hits);
                jinkMax = Math.Max(jinkMax, jink.Hits);
            }
            float straightMean = (float)straightTotal / seeds, jinkMean = (float)jinkTotal / seeds;
            return new DodgeResult
            {
                straightHits = (int)MathF.Round(straightMean), straightHitsMean = straightMean,
                jinkHits = (int)MathF.Round(jinkMean), jinkHitsMean = jinkMean,
                straightMin = straightMin, straightMax = straightMax, jinkMin = jinkMin, jinkMax = jinkMax,
                seeds = seeds, shots = shots, seconds = seconds,
            };
        }

        /// <summary>One bot's hits in each of the fights with seeds <paramref name="seed"/>..<paramref name="seed"/>+<paramref name="seeds"/>-1
        /// (<paramref name="makeBot"/> gets the seed), with no cover: the scripted clean-line routes use this.</summary>
        public static int[] HitsPerSeed(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, Func<ulong, IPlayerBot> makeBot,
            float seconds, ulong seed, int seeds = 5)
        {
            var fights = Fights(makeWorld, makeShooters, makeBot, seconds, seed, seeds, null, Vector3.Zero);
            var hits = new int[fights.Length];
            for (int i = 0; i < hits.Length; i++) hits[i] = fights[i].hits;
            return hits;
        }

        /// <summary>One bot's fight in each of the seeds <paramref name="seed"/>..<paramref name="seed"/>+<paramref name="seeds"/>-1
        /// (<paramref name="makeBot"/> gets the seed), starting at <paramref name="start"/> in the fight's frame: hits, shots
        /// fired and when the armour was wrecked. The clean-line and stand-still checks use this.</summary>
        public static DodgeFightResult[] Fights(Func<IMoveWorld> makeWorld, Func<List<Shooter>> makeShooters, Func<ulong, IPlayerBot> makeBot,
            float seconds, ulong seed, int seeds, IReadOnlyList<CoverBox> cover, Vector3 start)
        {
            if (seeds < 1) seeds = 1;
            var results = new DodgeFightResult[seeds];
            for (int i = 0; i < seeds; i++)
            {
                ulong s = seed + (ulong)i;
                var c = Fight(makeWorld(), makeBot(s), makeShooters(), seconds, s, cover, start);
                results[i] = new DodgeFightResult { hits = c.Hits, shots = c.ShotsFired, wreckedAt = c.WreckedAt };
            }
            return results;
        }

        static CombatSim Fight(IMoveWorld world, IPlayerBot bot, List<Shooter> shooters, float seconds, ulong seed, IReadOnlyList<CoverBox> cover, Vector3 start)
        {
            var sim = new MoveSim(world, new MoveDef());
            sim.Teleport(start, 0f, Vector3.Zero);
            var c = new CombatSim(sim, bot, shooters, seed, cover);
            c.Run(seconds);
            return c;
        }
    }
}
