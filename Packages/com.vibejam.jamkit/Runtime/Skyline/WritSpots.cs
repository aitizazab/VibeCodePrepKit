using System;
using System.Collections.Generic;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>Where a shot landed on a bot. WeakPoint is a bot's designed weak point (its lens, its display): what a
    /// headshot is on a robot. Plate is armour that sheds (Enemies.cs).</summary>
    public enum HitZone { Body, Plate, WeakPoint }

    public struct WeakSpot
    {
        public int id;
        /// <summary>Where it sits, in the bot's own space.</summary>
        public Vector3 local;
        public float createdAt;
    }

    /// <summary>The Writ's spot numbers and rules (docs/SKYLINE-DESIGN.md 2.2) as they stand this run; upgrades change
    /// them. The rules (consumed on hit, at most 3, 6 s) are data defaults pending the creator, so they are fields.</summary>
    public sealed class WritTuning
    {
        /// <summary>A spot hit's multiplier: always more than the Writ's weak-point (headshot) multiplier (the identity).</summary>
        public float spotMultiplier = 2.5f;
        public int maxSpotsPerBot = 3;

        /// <summary>The cap at the Writ's third level, the capstone Case Law (the creator's call, 2026-10-13: "Levels 1 and 2
        /// have 3 max weak spots, Level 3 can have 5"). Case Law's other half, any damage hitting the spots, is unbuilt.</summary>
        public const int CapstoneMaxSpots = 5;

        /// <summary>The tuning at a Writ level (1-3, the three upgrade tiers; outside the range is clamped): the spot cap is
        /// 3, 3, 5. Only the cap is level-dependent here; the built upgrades still go through <see cref="UpgradePaths.Apply"/>
        /// (which refuses unbuilt ones, so Case Law's cap can't go through it).</summary>
        public static WritTuning ForLevel(int level)
        {
            var t = new WritTuning();
            if (level >= 3) t.maxSpotsPerBot = CapstoneMaxSpots;
            return t;
        }

        public float spotLifetime = 6f;
        /// <summary>How close (m, bot space) a hit must land to a spot to hit it.</summary>
        public float spotRadius = 0.2f;
        /// <summary>True: a spot hit removes the spot. False: the spot stays until it expires or is replaced.</summary>
        public bool consumeOnHit = true;
    }

    /// <summary>The weak spots on one bot: created by Writ hits on its weak point, consumed by Writ hits on them, expiring
    /// after <see cref="WritTuning.spotLifetime"/>, at most <see cref="WritTuning.maxSpotsPerBot"/> (the oldest replaced).</summary>
    public sealed class WeakSpots
    {
        readonly List<WeakSpot> _spots = new List<WeakSpot>();
        int _nextId = 1;

        public int Count => _spots.Count;
        public IReadOnlyList<WeakSpot> All => _spots;

        public void Expire(float now, WritTuning t)
        {
            for (int i = _spots.Count - 1; i >= 0; i--)
                if (now - _spots[i].createdAt >= t.spotLifetime) _spots.RemoveAt(i);
        }

        public WeakSpot Add(Vector3 local, float now, WritTuning t)
        {
            Expire(now, t);
            while (_spots.Count >= Math.Max(1, t.maxSpotsPerBot)) _spots.RemoveAt(0);
            var s = new WeakSpot { id = _nextId++, local = local, createdAt = now };
            _spots.Add(s);
            return s;
        }

        /// <summary>Removes and returns the spot nearest to <paramref name="local"/> within <see cref="WritTuning.spotRadius"/>.</summary>
        public bool TryConsume(Vector3 local, float now, WritTuning t, out WeakSpot spot)
        {
            int best = Nearest(local, now, t);
            if (best < 0)
            {
                spot = default;
                return false;
            }
            spot = _spots[best];
            _spots.RemoveAt(best);
            return true;
        }

        /// <summary>Like <see cref="TryConsume"/> but leaves the spot in place.</summary>
        public bool TryFind(Vector3 local, float now, WritTuning t, out WeakSpot spot)
        {
            int best = Nearest(local, now, t);
            spot = best < 0 ? default : _spots[best];
            return best >= 0;
        }

        int Nearest(Vector3 local, float now, WritTuning t)
        {
            Expire(now, t);
            int best = -1;
            float bestDistance = t.spotRadius;
            for (int i = 0; i < _spots.Count; i++)
            {
                float d = Vector3.Distance(_spots[i].local, local);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        public void Clear() => _spots.Clear();
    }

    public struct WritHit
    {
        /// <summary>Multiply the hit's damage by this.</summary>
        public float multiplier;
        public bool spotCreated, spotHit;
        /// <summary>The spot created or hit.</summary>
        public WeakSpot spot;
    }

    public static class Writ
    {
        /// <summary>What a hit at <paramref name="local"/> (bot space) in <paramref name="zone"/> does. A Writ hit
        /// (<paramref name="fromWrit"/>) near a spot hits it for <see cref="WritTuning.spotMultiplier"/> (and consumes it
        /// when <see cref="WritTuning.consumeOnHit"/>); otherwise a weak-point hit takes the gun's
        /// <paramref name="weakPointMultiplier"/>, and from the Writ leaves a spot there.</summary>
        public static WritHit Resolve(WeakSpots spots, HitZone zone, Vector3 local, float now, bool fromWrit, float weakPointMultiplier, WritTuning t)
        {
            var r = new WritHit { multiplier = 1f };
            if (spots == null || t == null) return r;
            if (fromWrit)
            {
                WeakSpot hit;
                bool found = t.consumeOnHit ? spots.TryConsume(local, now, t, out hit) : spots.TryFind(local, now, t, out hit);
                if (found)
                {
                    r.multiplier = t.spotMultiplier;
                    r.spotHit = true;
                    r.spot = hit;
                    return r;
                }
            }
            if (zone == HitZone.WeakPoint)
            {
                r.multiplier = weakPointMultiplier;
                if (fromWrit)
                {
                    r.spot = spots.Add(local, now, t);
                    r.spotCreated = true;
                }
            }
            return r;
        }
    }

    public enum UpgradeKind { Numbers, Augment, Capstone }

    /// <summary>One step of a gun's upgrade path (2.2: numbers and stats, then augments that deepen the identity, then a
    /// capstone that extends it). The augments and capstones are proposals for the creator, built after M1.</summary>
    public sealed class UpgradeDef
    {
        public string id = "", gun = "", name = "", description = "";
        public UpgradeKind kind;
        /// <summary>Built in M1 (the first of each path); the rest are written and built later.</summary>
        public bool built;
    }

    public static class UpgradePaths
    {
        public static UpgradeDef[] Writ() => new[]
        {
            new UpgradeDef { id = "writ-deeper-ink", gun = SkylineGuns.WritId, kind = UpgradeKind.Numbers, built = true, name = "Deeper Ink",
                description = "Spot hits deal x3.25 instead of x2.5." },
            new UpgradeDef { id = "writ-precedent", gun = SkylineGuns.WritId, kind = UpgradeKind.Augment, name = "Precedent",
                description = "After a reload, one extra spot appears on the last bot you hit for every two spot hits the last magazine landed (at most 3)." },
            new UpgradeDef { id = "writ-case-law", gun = SkylineGuns.WritId, kind = UpgradeKind.Capstone, name = "Case Law",
                description = "Any damage can hit the spots: the Breacher's pellets, explosions, a hire's attacks." },
        };

        public static UpgradeDef[] Breacher() => new[]
        {
            new UpgradeDef { id = "breacher-quick-cell", gun = SkylineGuns.BreacherId, kind = UpgradeKind.Numbers, built = true, name = "Quick Cell",
                description = "Reloads 25% faster; the reload's sounds and animation speed up with it." },
            new UpgradeDef { id = "breacher-eviction-notice", gun = SkylineGuns.BreacherId, kind = UpgradeKind.Augment, name = "Eviction Notice",
                description = "A bot knocked into a wall or another bot takes impact damage (and so does the bot it hits)." },
            new UpgradeDef { id = "breacher-forced-entry", gun = SkylineGuns.BreacherId, kind = UpgradeKind.Capstone, name = "Forced Entry",
                description = "A boost's blast knocks back and damages bots within 4 m of where you fired it." },
        };

        /// <summary>Applies a built upgrade to this run's gun (idempotent). False, changing nothing, for one that isn't built
        /// or belongs to another gun.</summary>
        public static bool Apply(UpgradeDef u, SkylineGun gun, WritTuning writ)
        {
            if (u == null || !u.built || gun == null || u.gun != gun.Def.id) return false;
            switch (u.id)
            {
                case "writ-deeper-ink":
                    if (writ == null) return false;
                    writ.spotMultiplier = 3.25f;
                    return true;
                case "breacher-quick-cell":
                    gun.ReloadSpeed = 1.25f;
                    return true;
                default:
                    return false;
            }
        }
    }
}
