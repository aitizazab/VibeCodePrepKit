using System.Collections.Generic;
using JamKit.Combat;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Doomlike
{
    // Shooting, damage both ways, explosions, line of sight and gunfire noise.
    public sealed partial class DoomGame
    {
        struct Noise
        {
            public Vector3 position;
            public float radius;
        }

        readonly Noise[] _noises = new Noise[8];
        int _noiseCount;
        readonly List<System.Numerics.Vector2> _pellets = new List<System.Numerics.Vector2>(16);
        readonly List<DoomEnemy> _hitEnemies = new List<DoomEnemy>(16);
        readonly List<float> _hitDamage = new List<float>(16);
        readonly List<Vector3> _hitPoints = new List<Vector3>(16);
        RaycastHit _ray;

        /// <summary>Times the player was shot at and hit (counted in god mode too), for tests.</summary>
        public int PlayerHitsTaken;
        public float PlayerDamageTaken;

        // ---------- Player weapons ----------

        void FireWeapon(float dt, bool held, bool pressed)
        {
            int shots = _arsenal.Tick(dt, held, pressed, out bool dry);
            if (dry) PlaySound("blip", 0.6f);
            var gun = _arsenal.Current;
            if (_view != null && _arsenal.CurrentAsset != _view.Weapon && !_arsenal.Lowering) _view.SetWeapon(_arsenal.CurrentAsset);
            if (shots <= 0 || gun == null) return;
            for (int i = 0; i < shots; i++) Shoot(gun.asset);
            _arsenal.SwitchIfEmpty();
        }

        /// <summary>One shot of <paramref name="w"/> from the camera, as the player.</summary>
        public void Shoot(WeaponAsset w)
        {
            Vector3 origin = cam.transform.position;
            Vector3 aim = AutoAim(origin, cam.transform.forward, w.kind == WeaponKind.Melee ? w.range + 1f : w.range);
            Vector3 right = Vector3.Cross(Vector3.up, aim);
            if (right.sqrMagnitude < 1e-6f) right = cam.transform.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(aim, right);
            float damageScale = _stats.Get(DoomStats.Damage);

            switch (w.kind)
            {
                case WeaponKind.Melee:
                {
                    float dmg = w.damage * damageScale * _stats.Get(DoomStats.MeleeDamage);
                    if (Physics.SphereCast(origin, 0.3f, aim, out _ray, Mathf.Max(0.5f, w.range), LevelBuilder.WorldMask | LevelBuilder.ActorMask, QueryTriggerInteraction.Ignore))
                    {
                        if (_ray.collider.TryGetComponent<DoomEnemy>(out var enemy))
                            DamageEnemy(enemy, HitscanMath.Roll(dmg, w.damageVariance, _rng), DamageKind.Melee, origin, _ray.point);
                        else fx.Spawn(FxField.Fx.Puff, _ray.point + _ray.normal * 0.1f, 0.35f, Color.white);
                        PlaySound(w.fireSound, 1f);
                    }
                    else PlaySound("dash", 0.4f);
                    AddNoise(origin, w.noiseRadius);
                    break;
                }
                case WeaponKind.Hitscan:
                {
                    HitscanMath.PelletOffsets(w.pellets, w.spreadDegrees, w.verticalSpreadDegrees, _rng, _pellets, w.firstPelletCentered);
                    _hitEnemies.Clear();
                    _hitDamage.Clear();
                    _hitPoints.Clear();
                    int mask = LevelBuilder.WorldMask | LevelBuilder.ActorMask;
                    for (int p = 0; p < _pellets.Count; p++)
                    {
                        var o = _pellets[p];
                        Vector3 dir = (aim + right * Mathf.Tan(o.X) + up * Mathf.Tan(o.Y)).normalized;
                        if (!Physics.Raycast(origin, dir, out _ray, w.range, mask, QueryTriggerInteraction.Ignore)) continue;
                        if (_ray.collider.TryGetComponent<DoomEnemy>(out var enemy) && enemy.Alive)
                        {
                            float d = HitscanMath.Roll(w.damage, w.damageVariance, _rng) * damageScale
                                * HitscanMath.Falloff(_ray.distance, w.range, w.falloffStart, w.minFalloff);
                            int at = _hitEnemies.IndexOf(enemy);
                            if (at < 0)
                            {
                                _hitEnemies.Add(enemy);
                                _hitDamage.Add(d);
                                _hitPoints.Add(_ray.point);
                            }
                            else _hitDamage[at] += d;
                        }
                        else fx.Spawn(FxField.Fx.Puff, _ray.point + _ray.normal * 0.08f, 0.35f, new Color(1f, 0.95f, 0.8f));
                    }
                    // Pellets add up before the hit lands, so a point-blank shotgun blast can gib.
                    for (int i = 0; i < _hitEnemies.Count; i++)
                        DamageEnemy(_hitEnemies[i], _hitDamage[i], DamageKind.Bullet, origin, _hitPoints[i]);
                    _flash = 1f;
                    PlaySound(w.fireSound, 1f);
                    AddNoise(origin, w.noiseRadius);
                    break;
                }
                case WeaponKind.Projectile:
                {
                    Vector3 start = origin + aim * 0.6f - up * 0.12f;
                    float horiz = new Vector2(aim.x, aim.z).magnitude;
                    float slope = horiz > 0.05f ? aim.y / horiz : 0f;
                    float angle = Mathf.Atan2(aim.z, aim.x);
                    projectiles.Spawn(start, angle, w.projectileSpeed, 0f, 0f, 6f, start.y + slope * 10f, 10f,
                        w.damage * damageScale, w.projectileRadius, w.splashRadius, w.projectileColor, true, 0, w.selfDamageScale);
                    _flash = 0.7f;
                    PlaySound(w.fireSound, 1f);
                    AddNoise(origin, w.noiseRadius);
                    break;
                }
            }
            if (w.kind != WeaponKind.Melee) player.Kick(w.recoilDegrees);
            if (w.shake > 0f) GameRoot.Juice.Shake(w.shake);
            if (_view != null) _view.Fired();
        }

        /// <summary>Vertical auto-aim: if an enemy is close to the crosshair left-right and in sight,
        /// the shot's pitch bends to its middle, so mouse height matters less (and a pad works).</summary>
        Vector3 AutoAim(Vector3 origin, Vector3 forward, float range)
        {
            float maxDeg = config.player.verticalAutoAimDegrees;
            if (maxDeg <= 0f) return forward;
            var flat = new Vector2(forward.x, forward.z);
            if (flat.sqrMagnitude < 1e-4f) return forward;
            flat.Normalize();
            DoomEnemy best = null;
            float bestScore = 1.0001f;
            float bestDist = 0f;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.Alive) continue;
                Vector3 c = e.Center;
                var d = new Vector2(c.x - origin.x, c.z - origin.z);
                float dist = d.magnitude;
                if (dist < 0.3f || dist > range) continue;
                float ang = Vector2.Angle(flat, d);
                // Wider for close enemies: their bodies cover more of the view.
                float allowed = Mathf.Max(maxDeg, Mathf.Atan2(e.asset.radius, dist) * Mathf.Rad2Deg);
                float score = ang / allowed;
                if (score >= bestScore) continue;
                if (!LineOfSight(origin, c)) continue;
                best = e;
                bestScore = score;
                bestDist = dist;
            }
            if (best == null) return forward;
            Vector3 center = best.Center;
            float dy = center.y - origin.y;
            var dir = new Vector3(flat.x * bestDist, dy, flat.y * bestDist);
            return dir.normalized;
        }

        // ---------- Damage ----------

        /// <summary>Damages an enemy as the player (hit markers, kill credit, blood, gibs).</summary>
        public void DamageEnemy(DoomEnemy enemy, float amount, DamageKind kind, Vector3 from, Vector3 hitPoint)
        {
            if (enemy == null || !enemy.Alive || amount <= 0f) return;
            var r = enemy.TakeDamage(amount, kind, 1);
            if (r.applied <= 0f) return;
            fx.Spawn(FxField.Fx.Blood, hitPoint, 0.45f, Color.white);
            _hud.HitMarker(r.killed);
            if (r.killed) OnEnemyKilled(enemy, from);
        }

        void OnEnemyKilled(DoomEnemy enemy, Vector3 from)
        {
            var a = enemy.asset;
            PlayAt(a.deathSound, enemy.transform.position, 1f);
            if (config.killHitStop > 0f) GameRoot.Juice.HitStop(config.killHitStop);
            if (enemy.Gibbed)
            {
                Vector3 c = enemy.Center;
                Vector3 away = (enemy.transform.position - from);
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                for (int i = 0; i < 7; i++)
                {
                    var v = away * _rng.Range(1f, 4f) + new Vector3(_rng.Range(-2f, 2f), _rng.Range(2f, 6f), _rng.Range(-2f, 2f));
                    fx.Spawn(FxField.Fx.Blood, c, _rng.Range(0.25f, 0.5f), Color.white, v, true);
                }
                PlaySound("explosion", 0.5f);
            }
            _score += a.score;
            float steal = _inventory.EffectStacks(DoomStats.Lifesteal) * 4f;
            if (steal > 0f) player.Health.Heal(steal);
            SpawnDrops(enemy);
        }

        void CountKills()
        {
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i].TakeKill()) _tally.kills++;
        }

        /// <summary>Damages the player from a source position (red flash, shake, direction hint).</summary>
        public void DamagePlayer(float amount, Vector3 from, DamageKind kind)
        {
            if (!player.Alive || amount <= 0f || _phase != Phase.Playing) return;
            PlayerHitsTaken++;
            if (DebugMenu.PlayerInvincible) return;
            float armor = _stats.Get(DoomStats.Armor);
            var r = player.Health.Damage(new DamageInfo(amount * (1f - armor), kind));
            if (r.applied <= 0f) return;
            PlayerDamageTaken += r.applied;
            float k = Mathf.Clamp01(r.applied / 25f);
            GameRoot.Juice.Flash(new Color(1f, 0.1f, 0.05f, Mathf.Lerp(0.12f, config.hurtFlashAlpha, k)), 0.25f);
            GameRoot.Juice.Shake(config.hurtShake * Mathf.Lerp(0.4f, 1f, k));
            PlaySound("hurt", 0.8f);
            Vector3 d = from - player.transform.position;
            if (d.sqrMagnitude > 0.01f)
            {
                float rel = GridMath.WrapSigned(Mathf.Atan2(d.z, d.x) - GridMath.AngleFromYaw(player.Yaw));
                _hud.DamageFrom(rel, 0.4f + k);
            }
            if (r.killed) OnPlayerDied();
        }

        /// <summary>A blast: fire, a shake, and damage falling off with distance (behind walls is safe).</summary>
        public void Explode(Vector3 at, float radius, float damage, bool fromPlayer, float selfScale)
        {
            if (fromPlayer) radius *= _stats.Get(DoomStats.SplashRadius);
            fx.Spawn(FxField.Fx.Blast, at, Mathf.Max(1f, radius * 0.9f), Color.white);
            PlayAt("explosion", at, 1f);
            float pd = Vector3.Distance(at, player.EyePosition);
            GameRoot.Juice.Shake(Mathf.Clamp01(0.6f - pd / (radius * 6f)));
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.Alive) continue;
                Vector3 c = e.Center;
                float d = Mathf.Max(0f, Vector3.Distance(at, c) - e.asset.radius);
                float dmg = HitscanMath.Splash(damage, radius, d);
                if (dmg <= 0f || !LineOfSight(at, c)) continue;
                if (fromPlayer) DamageEnemy(e, dmg, DamageKind.Explosion, at, c);
                else e.TakeDamage(dmg, DamageKind.Explosion, 0);
            }
            if (!fromPlayer || selfScale > 0f)
            {
                Vector3 pc = player.transform.position + Vector3.up * (player.Height * 0.5f);
                float d = Mathf.Max(0f, Vector3.Distance(at, pc) - player.Radius);
                float dmg = HitscanMath.Splash(damage, radius, d) * (fromPlayer ? selfScale : 1f);
                if (dmg > 0f && LineOfSight(at, pc)) DamagePlayer(dmg, at, DamageKind.Explosion);
            }
            AddNoise(at, radius * 5f);
        }

        // ---------- Senses and sound ----------

        /// <summary>Nothing solid (walls, closed doors) between two points.</summary>
        public bool LineOfSight(Vector3 a, Vector3 b) =>
            !Physics.Linecast(a, b, LevelBuilder.WorldMask, QueryTriggerInteraction.Ignore);

        /// <summary>Gunfire: enemies within the radius hear it this frame.</summary>
        public void AddNoise(Vector3 at, float radius)
        {
            if (radius <= 0f) return;
            if (_noiseCount < _noises.Length) _noises[_noiseCount++] = new Noise { position = at, radius = radius };
        }

        public bool HeardNoise(Vector3 at)
        {
            for (int i = 0; i < _noiseCount; i++)
                if ((_noises[i].position - at).sqrMagnitude <= _noises[i].radius * _noises[i].radius) return true;
            return false;
        }

        public void PlaySound(string id, float volume)
        {
            if (!string.IsNullOrEmpty(id)) GameRoot.Audio.Play(id, volume);
        }

        /// <summary>A sound at a place: quieter with distance (silent beyond about 35 m).</summary>
        public void PlayAt(string id, Vector3 at, float volume)
        {
            if (string.IsNullOrEmpty(id)) return;
            float d = Vector3.Distance(at, player.transform.position);
            float v = volume * Mathf.Clamp01(1.15f - d / 35f);
            if (v > 0.05f) GameRoot.Audio.Play(id, v);
        }
    }
}
