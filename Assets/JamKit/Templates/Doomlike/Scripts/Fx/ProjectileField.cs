using JamKit.Patterns;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// Every flying shot (enemy fireballs from their patterns, the player's launcher) as a struct
    /// array plus pooled billboards. Shots move on the floor plane with the core BulletMotion (so
    /// curving and accelerating patterns work in 3D) and climb or drop toward the height they were
    /// aimed at. Walls and doors stop them (a raycast per step); hits on the player and enemies
    /// are distance tests. Allocation-free after Init.
    /// </summary>
    public sealed class ProjectileField : MonoBehaviour
    {
        public const int Capacity = 256;

        public struct Shot
        {
            public System.Numerics.Vector2 xz;
            public float y, vy;
            public float angle, speed, accel, turn;
            public float age, life;
            public float damage, radius, splash, selfScale;
            public bool fromPlayer;
            public int ownerId;
            public Color color;
        }

        readonly Shot[] _shots = new Shot[Capacity];
        Billboard[] _views;
        int _count;
        int _dropped;
        RaycastHit _hit;

        public int Count => _count;
        public int Dropped => _dropped;
        public Shot Get(int i) => _shots[i];

        public void Init(Material spriteMaterial, Texture fxSheet)
        {
            if (_views != null) return;
            _views = new Billboard[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                _views[i] = Billboard.Create("shot " + i, transform, Vector3.zero, 0.5f, 0.5f, spriteMaterial, fxSheet, Color.white, 1f);
                _views[i].SetFrame(FxField.SheetCell((int)FxField.Fx.Glow, 0));
                _views[i].SetVisible(false);
            }
        }

        /// <summary>Adds a shot at world <paramref name="position"/> flying along the 2D
        /// <paramref name="angle"/> (radians, on the floor plane) and reaching <paramref name="targetY"/>
        /// after <paramref name="targetDistance"/> metres. Returns false when the pool is full.</summary>
        public bool Spawn(Vector3 position, float angle, float speed, float accel, float turn, float life,
            float targetY, float targetDistance, float damage, float radius, float splash, Color color,
            bool fromPlayer, int ownerId, float selfScale = 0f, float age = 0f)
        {
            if (_views == null) return false;
            if (_count >= Capacity) { _dropped++; return false; }
            float time = speed > 0.1f ? Mathf.Max(0.05f, targetDistance / speed) : 1f;
            var s = new Shot
            {
                xz = new System.Numerics.Vector2(position.x, position.z),
                y = position.y,
                vy = (targetY - position.y) / time,
                angle = angle,
                speed = speed,
                accel = accel,
                turn = turn,
                age = 0f,
                life = life > 0f ? life : 8f,
                damage = damage,
                radius = Mathf.Max(0.05f, radius),
                splash = splash,
                selfScale = selfScale,
                fromPlayer = fromPlayer,
                ownerId = ownerId,
                color = color,
            };
            int i = _count++;
            _shots[i] = s;
            var v = _views[i];
            float size = s.radius * 3.2f;
            v.SetSize(size, size);
            v.SetTint(color);
            v.SetVisible(true);
            if (age > 0f) Advance(ref _shots[i], age);
            Place(i);
            return true;
        }

        static void Advance(ref Shot s, float dt)
        {
            BulletMotion.Step(ref s.xz, ref s.angle, ref s.speed, s.accel, s.turn, dt);
            s.y += s.vy * dt;
            s.age += dt;
        }

        void Place(int i)
        {
            ref var s = ref _shots[i];
            float size = s.radius * 3.2f;
            _views[i].transform.position = new Vector3(s.xz.X, s.y - size * 0.5f, s.xz.Y);
        }

        public void Tick(float dt, DoomGame game, float cameraYaw)
        {
            if (_views == null) return;
            var player = game.player;
            Vector3 pp = player.transform.position;
            float pr = player.Radius, ph = player.Height;
            var enemies = game.Enemies;
            for (int i = _count - 1; i >= 0; i--)
            {
                ref var s = ref _shots[i];
                var from = new Vector3(s.xz.X, s.y, s.xz.Y);
                Advance(ref s, dt);
                var to = new Vector3(s.xz.X, s.y, s.xz.Y);
                bool remove = s.age >= s.life;
                Vector3 impact = to;

                Vector3 step = to - from;
                float len = step.magnitude;
                if (!remove && len > 1e-5f
                    && Physics.Raycast(from, step / len, out _hit, len + s.radius * 0.5f, LevelBuilder.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    remove = true;
                    impact = _hit.point + _hit.normal * 0.15f;
                }
                if (!remove && s.y < -0.5f) remove = true;

                if (!remove)
                {
                    if (!s.fromPlayer)
                    {
                        if (player.Alive && HitsCylinder(to, s.radius, pp, pr, ph))
                        {
                            remove = true;
                            impact = to;
                            if (s.splash <= 0f) game.DamagePlayer(s.damage, from, JamKit.Combat.DamageKind.Bullet);
                        }
                    }
                    else
                    {
                        for (int e = 0; e < enemies.Count; e++)
                        {
                            var en = enemies[e];
                            if (!en.Alive) continue;
                            var ep = en.transform.position;
                            if (!HitsCylinder(to, s.radius, ep, en.asset.radius, en.TopHeight)) continue;
                            remove = true;
                            impact = to;
                            if (s.splash <= 0f) game.DamageEnemy(en, s.damage, JamKit.Combat.DamageKind.Bullet, from, to);
                            break;
                        }
                    }
                }

                if (remove)
                {
                    if (s.age < s.life || s.splash > 0f) Impact(game, ref s, impact);
                    RemoveAt(i);
                    continue;
                }
                Place(i);
                _views[i].Face(cameraYaw);
            }
        }

        static bool HitsCylinder(Vector3 p, float r, Vector3 basePos, float radius, float height)
        {
            if (p.y < basePos.y - r || p.y > basePos.y + height + r) return false;
            float dx = p.x - basePos.x, dz = p.z - basePos.z;
            float rr = r + radius;
            return dx * dx + dz * dz <= rr * rr;
        }

        void Impact(DoomGame game, ref Shot s, Vector3 at)
        {
            if (s.splash > 0f)
            {
                game.Explode(at, s.splash, s.damage, s.fromPlayer, s.selfScale);
            }
            else game.fx.Spawn(FxField.Fx.Puff, at, 0.6f, s.color);
        }

        void RemoveAt(int i)
        {
            int last = _count - 1;
            if (i != last)
            {
                _shots[i] = _shots[last];
                var v = _views[i];
                _views[i] = _views[last];
                _views[last] = v;
            }
            _views[last].SetVisible(false);
            _count--;
        }

        public void Clear()
        {
            if (_views == null) return;
            for (int i = 0; i < _count; i++) _views[i].SetVisible(false);
            _count = 0;
        }
    }
}
