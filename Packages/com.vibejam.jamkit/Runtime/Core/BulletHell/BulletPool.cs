using System;
using System.Collections.Generic;
using System.Numerics;
using JamKit.Patterns;

namespace JamKit.BulletHell
{
    [Flags]
    public enum BulletFlags : byte
    {
        None = 0,
        /// <summary>Already counted as a graze (each bullet grazes once).</summary>
        Grazed = 1,
        /// <summary>Turns toward the nearest target (player shots with the homing upgrade).</summary>
        Homing = 2,
        /// <summary>Bounces once off the bottom edge of the playfield (Rebound shots), then flies on normally.</summary>
        Bounce = 4,
        /// <summary>Already a ricochet (Last Gambit): it doesn't ricochet again.</summary>
        Ricocheted = 8,
        /// <summary>Its damage falls off with its age (Scatter pellets: see ShotRules.ScatterFalloffAt).</summary>
        Falloff = 16,
    }

    /// <summary>One bullet in a <see cref="BulletPool"/>. A plain struct: the pool keeps them in one
    /// array and moves them with <see cref="BulletMotion"/>, so there is no object per bullet.</summary>
    public struct Bullet
    {
        public Vector2 position;
        /// <summary>Heading in radians, counter-clockwise from +X.</summary>
        public float angle;
        public float speed;
        public float acceleration;
        public float angularVelocity;
        /// <summary>Seconds left to live; +infinity for no limit (culled off-screen instead).</summary>
        public float lifeLeft;
        public float age;
        /// <summary>Collision radius in world units (usually smaller than the drawn size: fair dodging).</summary>
        public float radius;
        public float damage;
        public int style;
        /// <summary>Extra targets it can pass through (player shots with pierce).</summary>
        public int pierce;
        /// <summary>The last target it hit, so a piercing shot doesn't hit the same enemy every frame.</summary>
        public int lastHitId;
        public BulletFlags flags;
        // Cached heading for the straight-line fast path.
        internal float dirX, dirY;

        public bool IsStraight => acceleration == 0f && angularVelocity == 0f;
    }

    /// <summary>The play area in world units. Bullets beyond it (plus a margin) are culled.</summary>
    public struct PlayfieldBounds
    {
        public float minX, minY, maxX, maxY;

        public PlayfieldBounds(float minX, float minY, float maxX, float maxY)
        {
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
        }

        /// <summary>A box centred on the origin.</summary>
        public static PlayfieldBounds Centered(float halfWidth, float halfHeight) =>
            new PlayfieldBounds(-halfWidth, -halfHeight, halfWidth, halfHeight);

        public float Width => maxX - minX;
        public float Height => maxY - minY;

        public bool Contains(Vector2 p, float margin = 0f) =>
            p.X >= minX - margin && p.X <= maxX + margin && p.Y >= minY - margin && p.Y <= maxY + margin;

        public Vector2 Clamp(Vector2 p, float inset = 0f) => new Vector2(
            Math.Min(Math.Max(p.X, minX + inset), maxX - inset),
            Math.Min(Math.Max(p.Y, minY + inset), maxY - inset));
    }

    /// <summary>A circle bullets can hit (an enemy, the boss). Filled by game code every frame.</summary>
    public struct HitTarget
    {
        public Vector2 position;
        public float radius;
        /// <summary>Unique per target (never 0), for the pierce rule.</summary>
        public int id;
        public bool active;
    }

    /// <summary>One bullet striking one target, reported by <see cref="BulletPool.CollideTargets"/>.</summary>
    public struct BulletHit
    {
        public int targetIndex;
        public float damage;
        public Vector2 position;
        /// <summary>The bullet's style (the balance report uses it to credit damage to an upgrade).</summary>
        public int style;
        public BulletFlags flags;
        /// <summary>Seconds it had flown (Scatter's falloff).</summary>
        public float age;
        /// <summary>The target's id (a ricochet looks for a different one).</summary>
        public int targetId;
    }

    /// <summary>
    /// Data-oriented bullet storage for a bullet hell: one array of <see cref="Bullet"/> structs,
    /// swap-remove on death, exact frame-rate-independent motion (core <see cref="BulletMotion"/>),
    /// lifetime and off-screen culling, and circle tests (hit, graze, bomb clear, targets).
    /// Thousands of bullets cost a few array passes per frame and allocate nothing.
    /// Only indices 0..Count-1 of <see cref="Items"/> are live; removal reorders them.
    /// </summary>
    public sealed class BulletPool
    {
        readonly Bullet[] _items;
        int _count;
        int _dropped;

        /// <summary>Damage multiplier a bullet gets when it bounces off the floor (Rebound shots come back hotter).</summary>
        public float bounceDamageScale = 1f;

        public BulletPool(int capacity)
        {
            if (capacity <= 0) throw new ArgumentException("capacity must be positive");
            _items = new Bullet[capacity];
        }

        public int Count => _count;
        public int Capacity => _items.Length;
        /// <summary>Bullets refused because the pool was full (since creation or <see cref="ResetDropped"/>).</summary>
        public int Dropped => _dropped;
        /// <summary>The backing array; read entries 0..Count-1 (renderers, debug views).</summary>
        public Bullet[] Items => _items;

        public ref Bullet this[int index] => ref _items[index];

        public void ResetDropped() => _dropped = 0;

        /// <summary>Adds a bullet. Returns false (and counts it as dropped) when full.</summary>
        public bool Spawn(Bullet b)
        {
            if (_count >= _items.Length) { _dropped++; return false; }
            if (!(b.lifeLeft > 0f)) b.lifeLeft = float.PositiveInfinity;
            b.dirX = (float)Math.Cos(b.angle);
            b.dirY = (float)Math.Sin(b.angle);
            _items[_count++] = b;
            return true;
        }

        /// <summary>Adds a bullet from loose values. lifetime 0 or less = no limit.</summary>
        public bool Spawn(Vector2 position, float angle, float speed, int style, float radius,
            float lifetime = 0f, float acceleration = 0f, float angularVelocity = 0f, float damage = 1f,
            int pierce = 0, BulletFlags flags = BulletFlags.None)
        {
            return Spawn(new Bullet
            {
                position = position,
                angle = angle,
                speed = speed,
                acceleration = acceleration,
                angularVelocity = angularVelocity,
                lifeLeft = lifetime > 0f ? lifetime : float.PositiveInfinity,
                radius = radius,
                damage = damage,
                style = style,
                pierce = pierce,
                flags = flags,
            });
        }

        /// <summary>Copies an emitter's output in. Collision radius comes from the style table
        /// (<paramref name="styleRadii"/>; unknown styles get <paramref name="fallbackRadius"/>).
        /// Spawns already past their lifetime (after a long hitch) are skipped. Returns how many were added.</summary>
        public int AddSpawns(List<BulletSpawn> spawns, IReadOnlyList<float> styleRadii, float damage = 1f, float fallbackRadius = 0.1f)
        {
            int added = 0;
            for (int i = 0; i < spawns.Count; i++)
            {
                var s = spawns[i];
                float life = float.PositiveInfinity;
                if (s.lifetime > 0f)
                {
                    life = s.lifetime - s.age;
                    if (life <= 0f) continue;
                }
                float r = styleRadii != null && s.styleIndex >= 0 && s.styleIndex < styleRadii.Count ? styleRadii[s.styleIndex] : fallbackRadius;
                bool ok = Spawn(new Bullet
                {
                    position = s.position,
                    angle = s.angle,
                    speed = s.speed,
                    acceleration = s.acceleration,
                    angularVelocity = s.angularVelocity,
                    lifeLeft = life,
                    age = s.age,
                    radius = r,
                    damage = damage,
                    style = s.styleIndex,
                });
                if (ok) added++;
            }
            return added;
        }

        /// <summary>Moves every bullet by dt, then drops the expired ones and those more than
        /// <paramref name="cullMargin"/> outside <paramref name="bounds"/>.</summary>
        public void Step(float dt, PlayfieldBounds bounds, float cullMargin = 0.5f)
        {
            if (!(dt > 0f)) return;
            float minX = bounds.minX - cullMargin, maxX = bounds.maxX + cullMargin;
            float minY = bounds.minY - cullMargin, maxY = bounds.maxY + cullMargin;
            int i = 0;
            while (i < _count)
            {
                ref Bullet b = ref _items[i];
                b.age += dt;
                b.lifeLeft -= dt;
                if (b.IsStraight)
                {
                    float d = b.speed * dt;
                    b.position.X += b.dirX * d;
                    b.position.Y += b.dirY * d;
                }
                else
                {
                    BulletMotion.Step(ref b.position, ref b.angle, ref b.speed, b.acceleration, b.angularVelocity, dt);
                    b.dirX = (float)Math.Cos(b.angle);
                    b.dirY = (float)Math.Sin(b.angle);
                }
                float x = b.position.X, y = b.position.Y;
                if ((b.flags & BulletFlags.Bounce) != 0 && y < bounds.minY && b.dirY < 0f)
                {
                    // Off the floor: mirror the heading (and any curve) and keep going, once.
                    b.position.Y = y = bounds.minY + (bounds.minY - y);
                    b.angle = -b.angle;
                    b.angularVelocity = -b.angularVelocity;
                    b.dirY = -b.dirY;
                    b.flags &= ~BulletFlags.Bounce;
                    b.damage *= bounceDamageScale;
                }
                if (b.lifeLeft <= 0f || x < minX || x > maxX || y < minY || y > maxY)
                {
                    _items[i] = _items[--_count]; // the moved bullet is stepped next, at this index
                    continue;
                }
                i++;
            }
        }

        /// <summary>
        /// Time Dilation: bullets within <paramref name="radius"/> of <paramref name="centre"/> are pulled
        /// back along their heading so that, this step, they moved at <paramref name="scale"/> of their speed.
        /// Call right after <see cref="Step"/> with the same dt. Exact for straight bullets, close for curving ones.
        /// </summary>
        public void SlowNear(Vector2 centre, float radius, float scale, float dt)
        {
            if (!(dt > 0f) || scale >= 1f) return;
            float back = (1f - Math.Max(0f, scale)) * dt;
            float rr = radius * radius;
            for (int i = 0; i < _count; i++)
            {
                ref Bullet b = ref _items[i];
                float dx = b.position.X - centre.X, dy = b.position.Y - centre.Y;
                if (dx * dx + dy * dy > rr) continue;
                float d = b.speed * back;
                b.position.X -= b.dirX * d;
                b.position.Y -= b.dirY * d;
                b.age -= back;
                b.lifeLeft += back;
            }
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _count) throw new ArgumentOutOfRangeException(nameof(index));
            _items[index] = _items[--_count];
        }

        /// <summary>Removes everything. Returns how many there were.</summary>
        public int Clear()
        {
            int n = _count;
            _count = 0;
            return n;
        }

        /// <summary>Removes the bullets touching a circle (bombs, the boss's death). Their positions
        /// go into <paramref name="removed"/> if given (for sparkles or score pickups).</summary>
        public int ClearInCircle(Vector2 centre, float radius, List<Vector2> removed = null)
        {
            int n = 0;
            int i = 0;
            while (i < _count)
            {
                ref Bullet b = ref _items[i];
                float rr = radius + b.radius;
                if (Vector2.DistanceSquared(b.position, centre) <= rr * rr)
                {
                    removed?.Add(b.position);
                    _items[i] = _items[--_count];
                    n++;
                    continue;
                }
                i++;
            }
            return n;
        }

        /// <summary>Index of the first bullet overlapping the circle, or -1. For the player's hitbox.</summary>
        public int FirstOverlap(Vector2 centre, float radius)
        {
            for (int i = 0; i < _count; i++)
            {
                ref Bullet b = ref _items[i];
                float rr = radius + b.radius;
                float dx = b.position.X - centre.X, dy = b.position.Y - centre.Y;
                if (dx * dx + dy * dy <= rr * rr) return i;
            }
            return -1;
        }

        /// <summary>Marks bullets inside the graze ring (and not yet grazed) as grazed; returns how many
        /// were new this call. Bullets that actually hit (within <paramref name="hitRadius"/>) don't count.</summary>
        public int Graze(Vector2 centre, float grazeRadius, float hitRadius)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                ref Bullet b = ref _items[i];
                if ((b.flags & BulletFlags.Grazed) != 0) continue;
                float dx = b.position.X - centre.X, dy = b.position.Y - centre.Y;
                float d2 = dx * dx + dy * dy;
                float g = grazeRadius + b.radius, h = hitRadius + b.radius;
                if (d2 <= g * g && d2 > h * h)
                {
                    b.flags |= BulletFlags.Grazed;
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// Player shots against enemies: every bullet overlapping an active target reports a hit
        /// (damage and where) into <paramref name="hits"/> (cleared first), then is removed unless it
        /// still has pierce left (a piercing shot hits each target once). Returns the hit count.
        /// </summary>
        public int CollideTargets(HitTarget[] targets, int targetCount, List<BulletHit> hits)
        {
            hits.Clear();
            if (targetCount <= 0) return 0;
            int i = 0;
            while (i < _count)
            {
                ref Bullet b = ref _items[i];
                bool removed = false;
                for (int t = 0; t < targetCount; t++)
                {
                    ref HitTarget target = ref targets[t];
                    if (!target.active || target.id == b.lastHitId) continue;
                    float rr = target.radius + b.radius;
                    if (Vector2.DistanceSquared(b.position, target.position) > rr * rr) continue;
                    hits.Add(new BulletHit { targetIndex = t, damage = b.damage, position = b.position, style = b.style, flags = b.flags, age = b.age, targetId = target.id });
                    if (b.pierce > 0)
                    {
                        b.pierce--;
                        b.lastHitId = target.id;
                        continue;
                    }
                    _items[i] = _items[--_count];
                    removed = true;
                    break;
                }
                if (!removed) i++;
            }
            return hits.Count;
        }

        /// <summary>Turns <see cref="BulletFlags.Homing"/> bullets toward their nearest active target
        /// by at most <paramref name="turnRate"/> radians per second.</summary>
        public void SteerHoming(HitTarget[] targets, int targetCount, float turnRate, float dt)
        {
            if (targetCount <= 0 || !(dt > 0f)) return;
            float maxTurn = turnRate * dt;
            for (int i = 0; i < _count; i++)
            {
                ref Bullet b = ref _items[i];
                if ((b.flags & BulletFlags.Homing) == 0) continue;
                int best = -1;
                float bestD = float.PositiveInfinity;
                for (int t = 0; t < targetCount; t++)
                {
                    if (!targets[t].active) continue;
                    float d = Vector2.DistanceSquared(targets[t].position, b.position);
                    if (d < bestD) { bestD = d; best = t; }
                }
                if (best < 0) continue;
                Vector2 to = targets[best].position - b.position;
                float want = (float)Math.Atan2(to.Y, to.X);
                float delta = WrapAngle(want - b.angle);
                if (delta > maxTurn) delta = maxTurn;
                else if (delta < -maxTurn) delta = -maxTurn;
                b.angle = WrapAngle(b.angle + delta);
                b.dirX = (float)Math.Cos(b.angle);
                b.dirY = (float)Math.Sin(b.angle);
            }
        }

        /// <summary>Into (-pi, pi].</summary>
        public static float WrapAngle(float a)
        {
            const float TwoPi = (float)(Math.PI * 2);
            a %= TwoPi;
            if (a > Math.PI) a -= TwoPi;
            else if (a <= -Math.PI) a += TwoPi;
            return a;
        }
    }
}
