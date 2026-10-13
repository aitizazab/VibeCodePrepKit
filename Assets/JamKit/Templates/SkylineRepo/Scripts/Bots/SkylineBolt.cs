using System.Collections.Generic;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The mall-cop drone's bolt: a sphere of the def's projectile radius flying straight (FightSim's bolt). Each frame
    /// it sphere-casts the step: the target's own colliders take the hit (<see cref="ISkylineTarget.TakeAttack"/>); anything
    /// else that isn't a bot, a shed part or a trigger stops it; it expires after 4 s. A short orange slug, not glowing.
    /// Pooled renderer-only capsules (filled up front by <see cref="Prewarm"/>): a spent bolt is switched off and reused,
    /// never destroyed.</summary>
    public sealed class SkylineBolt : MonoBehaviour
    {
        const float MaxAge = 4f;
        const int PrewarmCount = 24;
        static readonly RaycastHit[] _hits = new RaycastHit[16];
        static readonly Color Slug = new Color(1f, 0.45f, 0.12f);
        static readonly List<SkylineBolt> _pool = new List<SkylineBolt>();

        Vector3 _vel;
        float _radius, _damage, _age;
        string _attackerId;
        Vector3 _attackerPos;
        ISkylineTarget _target;
        Transform _owner;

        public static SkylineBolt Fire(Vector3 from, Vector3 velocity, float radius, float damage, string attackerId, ISkylineTarget target, Transform owner)
        {
            var b = Take();
            var go = b.gameObject;
            float d = Mathf.Max(0.05f, radius * 2f);
            go.transform.localScale = new Vector3(d, d * 2.2f, d);
            b._vel = velocity;
            b._radius = radius;
            b._damage = damage;
            b._attackerId = attackerId;
            b._attackerPos = from;
            b._target = target;
            b._owner = owner;
            b._age = 0f;
            go.transform.SetPositionAndRotation(from, velocity.sqrMagnitude > 1e-6f ? Quaternion.FromToRotation(Vector3.up, velocity) : Quaternion.identity);
            go.SetActive(true);
            return b;
        }

        /// <summary>Fills the pool now (at scene start), so no bolt is created mid-fight.</summary>
        public static void Prewarm()
        {
            Purge();
            int idle = 0;
            for (int i = 0; i < _pool.Count; i++) if (!_pool[i].gameObject.activeSelf) idle++;
            for (; idle < PrewarmCount; idle++) Make();
        }

        /// <summary>Switches off every bolt in flight (the arena's restart).</summary>
        public static void ClearAll()
        {
            Purge();
            for (int i = 0; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        }

        static void Purge()
        {
            for (int i = _pool.Count - 1; i >= 0; i--) if (_pool[i] == null) _pool.RemoveAt(i);   // a scene change destroyed it
        }

        static SkylineBolt Make()
        {
            var go = SkylineBotLook.Visual(PrimitiveType.Capsule, "Bolt", null, SkylineBotLook.Lit(Slug), false);
            go.SetActive(false);
            var b = go.AddComponent<SkylineBolt>();
            _pool.Add(b);
            return b;
        }

        static SkylineBolt Take()
        {
            Purge();
            for (int i = 0; i < _pool.Count; i++) if (!_pool[i].gameObject.activeSelf) return _pool[i];
            return Make();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!(dt > 0f)) return;
            _age += dt;
            Vector3 from = transform.position;
            Vector3 step = _vel * dt;
            float len = step.magnitude;
            if (len > 1e-5f)
            {
                int n = Physics.SphereCastNonAlloc(from, Mathf.Max(0.01f, _radius), step / len, _hits, len, ~0, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                int bi = -1;
                for (int i = 0; i < n; i++)
                {
                    var c = _hits[i].collider;
                    if (c == null) continue;
                    bool isTarget = IsTarget(c);
                    if (!isTarget && SkylineBot.IgnoredBySight(c, _owner)) continue;
                    if (_hits[i].distance < best) { best = _hits[i].distance; bi = i; }
                }
                if (bi >= 0)
                {
                    var c = _hits[bi].collider;
                    if (IsTarget(c) && _target != null) _target.TakeAttack(_damage, _attackerId, _attackerPos);
                    gameObject.SetActive(false);
                    return;
                }
            }
            transform.position = from + step;
            if (_age > MaxAge) gameObject.SetActive(false);
        }

        bool IsTarget(Collider c)
        {
            if (!SkylineBot.TargetLive(_target)) return false;
            var root = _target.Root;
            return root != null && c.transform.IsChildOf(root);
        }
    }
}
