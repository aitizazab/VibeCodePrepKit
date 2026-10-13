using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>The mall-cop drone's bolt: a sphere of the def's projectile radius flying straight (FightSim's bolt). Each frame
    /// it sphere-casts the step: the target's own colliders take the hit (<see cref="ISkylineTarget.TakeAttack"/>); anything
    /// else that isn't a bot, a shed part or a trigger stops it; it expires after 4 s. A short orange slug, not glowing.</summary>
    public sealed class SkylineBolt : MonoBehaviour
    {
        const float MaxAge = 4f;
        static readonly RaycastHit[] _hits = new RaycastHit[16];

        Vector3 _vel;
        float _radius, _damage, _age;
        string _attackerId;
        Vector3 _attackerPos;
        ISkylineTarget _target;
        Transform _owner;

        public static SkylineBolt Fire(Vector3 from, Vector3 velocity, float radius, float damage, string attackerId, ISkylineTarget target, Transform owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Bolt";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = SkylineBotLook.Lit(new Color(1f, 0.45f, 0.12f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float d = Mathf.Max(0.05f, radius * 2f);
            go.transform.localScale = new Vector3(d, d * 2.2f, d);
            var b = go.AddComponent<SkylineBolt>();
            b._vel = velocity;
            b._radius = radius;
            b._damage = damage;
            b._attackerId = attackerId;
            b._attackerPos = from;
            b._target = target;
            b._owner = owner;
            go.transform.position = from;
            if (velocity.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.FromToRotation(Vector3.up, velocity);
            return b;
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
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position = from + step;
            if (_age > MaxAge) Destroy(gameObject);
        }

        bool IsTarget(Collider c)
        {
            if (!SkylineBot.TargetLive(_target)) return false;
            var root = _target.Root;
            return root != null && c.transform.IsChildOf(root);
        }
    }
}
