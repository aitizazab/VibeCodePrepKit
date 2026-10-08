using System.Numerics;

namespace JamKit.Combat
{
    /// <summary>What dealt the damage, for resistances, hit sounds and death animations.</summary>
    public enum DamageKind { Generic, Bullet, Melee, Explosion, Hazard }

    /// <summary>One hit, as the attacker describes it. Health scales and clamps it; the attacker
    /// never needs to know the target's state.</summary>
    public struct DamageInfo
    {
        public float amount;
        /// <summary>Who dealt it (an entity id), so kills can be credited and friendly fire skipped.</summary>
        public int sourceId;
        public DamageKind kind;
        public bool isCrit;
        /// <summary>Push to apply to the target, in world units; Health ignores it, physics reads it.</summary>
        public Vector3 knockback;

        public DamageInfo(float amount, DamageKind kind = DamageKind.Generic, int sourceId = 0,
            bool isCrit = false, Vector3 knockback = default)
        {
            this.amount = amount;
            this.sourceId = sourceId;
            this.kind = kind;
            this.isCrit = isCrit;
            this.knockback = knockback;
        }
    }

    /// <summary>What a hit actually did, so callers can pick feedback (flash, gib, "blocked" ping).</summary>
    public readonly struct DamageResult
    {
        /// <summary>Health actually removed, after scaling and clamping to what was left.</summary>
        public readonly float applied;
        /// <summary>Damage beyond what killed the target (for gibbing on big overkill).</summary>
        public readonly float overkill;
        public readonly bool killed;
        /// <summary>True when invulnerability frames swallowed the hit.</summary>
        public readonly bool blocked;

        public DamageResult(float applied, float overkill, bool killed, bool blocked)
        {
            this.applied = applied;
            this.overkill = overkill;
            this.killed = killed;
            this.blocked = blocked;
        }
    }
}
