using System;
using JamKit.Combat;
using UnityEngine;

namespace JamKit.Shell
{
    /// <summary>
    /// Hit points on a GameObject: a thin wrapper over the core <see cref="Health"/> (clamping,
    /// i-frames, difficulty scaling) that adds the feedback every hit wants: sprite flash, hit stop,
    /// screen shake, a sound, a floating number and blinking while invulnerable.
    /// Call <see cref="Hit"/> from whatever deals damage; listen to <see cref="Damaged"/> / <see cref="Died"/>.
    /// The player's copy honours the difficulty's damage scale and the debug menu's god mode.
    /// </summary>
    public sealed class Damageable : MonoBehaviour
    {
        public float maxHealth = 3f;
        [Tooltip("Seconds of invulnerability after each hit (players: about 1; most enemies: 0).")]
        public float invulnerableSeconds = 1f;
        [Tooltip("Uses the difficulty's damage-taken scale and the debug god mode.")]
        public bool isPlayer;

        [Header("Feedback")]
        public string hurtSound = "hurt";
        public string deathSound = "";
        public float hitStopSeconds = 0.08f;
        public float shakeTrauma = 0.45f;
        public Color flashColor = Color.white;
        public bool damageNumbers = true;
        public bool blinkWhileInvulnerable = true;

        Health _health;
        SpriteFlash _flash;
        SpriteRenderer _sprite;
        float _blinkClock;

        public Health Health
        {
            get
            {
                if (_health == null) Build();
                return _health;
            }
        }

        public event Action<DamageInfo, DamageResult> Damaged;
        public event Action<DamageInfo> Died;

        void Awake()
        {
            if (_health == null) Build();
            _flash = GetComponent<SpriteFlash>();
            _sprite = GetComponent<SpriteRenderer>();
        }

        void Build()
        {
            _health = new Health(Mathf.Max(0.01f, maxHealth), invulnerableSeconds);
            if (isPlayer && GameRoot.Exists) _health.DamageTakenScale = GameRoot.Settings.damageTakenScale;
            _health.Damaged += OnDamaged;
            _health.Died += info => Died?.Invoke(info);
        }

        /// <summary>Deals damage. Returns what happened (blocked by i-frames, killed...).</summary>
        public DamageResult Hit(float amount, DamageKind kind = DamageKind.Generic, int sourceId = 0)
        {
            if (JamKit.Presenting.Invincibility.Blocks(isPlayer, DebugMenu.GodMode, PresenterMode.InvincibleNow)) return new DamageResult(0f, 0f, false, true);
            return Health.Damage(new DamageInfo(amount, kind, sourceId));
        }

        void OnDamaged(DamageInfo info, DamageResult result)
        {
            if (result.applied <= 0f) return;
            if (_flash != null) _flash.Flash(flashColor, 0.12f);
            if (GameRoot.Exists)
            {
                var juice = GameRoot.Juice;
                if (hitStopSeconds > 0f) juice.HitStop(hitStopSeconds);
                juice.Shake(result.killed ? shakeTrauma * 1.5f : shakeTrauma);
                string sound = result.killed && !string.IsNullOrEmpty(deathSound) ? deathSound : hurtSound;
                if (!string.IsNullOrEmpty(sound)) GameRoot.Audio.Play(sound);
                if (damageNumbers)
                    juice.FloatingText(transform.position + Vector3.up * 0.6f, "-" + Mathf.CeilToInt(result.applied), new Color(1f, 0.4f, 0.35f));
            }
            Damaged?.Invoke(info, result);
        }

        void Update()
        {
            _health.Tick(Time.deltaTime);
            if (!blinkWhileInvulnerable || _sprite == null) return;
            var c = _sprite.color;
            float a = 1f;
            if (_health.IsInvulnerable && !_health.IsDead)
            {
                _blinkClock += Time.deltaTime;
                a = Mathf.Repeat(_blinkClock * 12f, 1f) < 0.5f ? 0.35f : 1f;
            }
            else _blinkClock = 0f;
            if (!Mathf.Approximately(c.a, a)) { c.a = a; _sprite.color = c; }
        }
    }
}
