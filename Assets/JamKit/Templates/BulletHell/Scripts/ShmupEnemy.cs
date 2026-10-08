using JamKit.Combat;
using JamKit.Flow;
using JamKit.Patterns;
using JamKit.Shell;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace JamKit.BulletHell
{
    /// <summary>
    /// One enemy: a pooled sprite with a core <see cref="Health"/>, a core <see cref="EnemyMover"/>
    /// and a core <see cref="Emitter"/> playing its EnemyDef's attack (with the difficulty's bullet
    /// speed and density). The game spawns, ticks and recycles it.
    /// </summary>
    public sealed class ShmupEnemy : MonoBehaviour
    {
        public SpriteRenderer body;
        public SpriteFlash flash;

        EnemyDef _def;
        Health _health;
        readonly EnemyMover _mover = new EnemyMover();
        Emitter _emitter;
        float _actTime, _spin, _flashCooldown;
        int _id;
        bool _alive;

        public EnemyDef Def => _def;
        public Health Health => _health;
        public EnemyMover Mover => _mover;
        public int Id => _id;
        public bool Alive => _alive;
        public bool IsDead => _health != null && _health.IsDead;
        public bool IsGone => _mover.IsGone;
        public float Radius => _def != null ? _def.radius : 0.3f;
        public Vector2 Position => new Vector2(_mover.Position.X, _mover.Position.Y);

        public static ShmupEnemy Create(Transform parent, Material spriteMaterial)
        {
            var go = new GameObject("Enemy");
            go.transform.SetParent(parent, false);
            var e = go.AddComponent<ShmupEnemy>();
            e.body = go.AddComponent<SpriteRenderer>();
            if (spriteMaterial != null) e.body.sharedMaterial = spriteMaterial;
            e.flash = go.AddComponent<SpriteFlash>();
            e.flash.target = e.body;
            go.SetActive(false);
            return e;
        }

        public void Spawn(EnemyDef def, Vector2 from, Vector2 anchor, float hpScale, int id, Rng rng, Settings settings)
        {
            _def = def;
            _id = id;
            _alive = true;
            _actTime = 0f;
            _spin = 0f;
            _health = new Health(Mathf.Max(0.1f, def.hp * Mathf.Max(0.01f, hpScale)));
            _mover.Begin(def.movement, new SVector2(from.x, from.y), new SVector2(anchor.x, anchor.y));
            _emitter = def.attack != null && def.attack.steps != null && def.attack.steps.Count > 0 ? new Emitter(def.attack, rng) : null;
            if (_emitter != null && settings != null)
            {
                _emitter.SpeedScale = settings.enemyBulletSpeedScale;
                _emitter.DensityScale = settings.enemyDensityScale;
            }
            body.sprite = def.sprite;
            body.color = def.color;
            transform.localScale = Vector3.one * def.size;
            transform.position = new Vector3(from.x, from.y, transform.position.z);
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
        }

        public void Despawn()
        {
            _alive = false;
            gameObject.SetActive(false);
        }

        /// <summary>Moves; fires once it has arrived (and is on screen). Bullets go to the field.</summary>
        public void Tick(float dt, Vector2 player, BulletField field, PlayfieldBounds bounds)
        {
            if (!_alive) return;
            _mover.Tick(dt, new SVector2(player.x, player.y), bounds);
            var p = _mover.Position;
            transform.position = new Vector3(p.X, p.Y, transform.position.z);
            if (_def.spin != 0f)
            {
                _spin += _def.spin * dt;
                transform.rotation = Quaternion.Euler(0f, 0f, _spin);
            }
            _flashCooldown -= Time.unscaledDeltaTime;

            bool canFire = _mover.Phase == MoverPhase.Acting || (_mover.Phase == MoverPhase.Leaving && _def.movement.kind != EnemyMoveKind.Hover && _def.movement.kind != EnemyMoveKind.Strafe);
            if (_emitter == null || !canFire || !bounds.Contains(p, -0.2f)) return;
            _actTime += dt;
            if (_actTime < _def.attackDelay) return;
            var spawns = field.Spawns;
            spawns.Clear();
            _emitter.Tick(dt, p, new SVector2(player.x, player.y), spawns);
            if (spawns.Count > 0) field.EmitEnemy(spawns);
        }

        /// <summary>Applies a hit. Returns true if it killed.</summary>
        public bool Damage(float amount)
        {
            if (!_alive || _health.IsDead) return false;
            var r = _health.Damage(new DamageInfo(amount, DamageKind.Bullet));
            if (_flashCooldown <= 0f && flash != null)
            {
                flash.Flash(Color.white, 0.06f);
                _flashCooldown = 0.05f;
            }
            return r.killed;
        }

        public void Kill()
        {
            if (_alive && _health != null) _health.Kill();
        }
    }
}
