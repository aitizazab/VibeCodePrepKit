using System.Collections.Generic;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>What bots shoot at: the player, or the gallery's dummy.</summary>
    public interface ISkylineTarget
    {
        /// <summary>Where shots aim (world space).</summary>
        Vector3 Chest { get; }
        /// <summary>The target's velocity (m/s), for bots that lead.</summary>
        Vector3 Velocity { get; }
        Transform Root { get; }
        void TakeAttack(float damage, string attackerId);
    }

    /// <summary>One hit from a gun (or the Writ's spot) on something hittable.</summary>
    public struct SkylineHitInfo
    {
        public GunDef gun;
        public float damage;
        public HitZone zone;
        /// <summary>The plate hit, or -1.</summary>
        public int plate;
        public Vector3 point, normal, direction;
        public bool fromWrit;
        public float knockback;
        public float time;
    }

    /// <summary>What a hit did.</summary>
    public struct SkylineHitResult
    {
        public BotDamage damage;
        public WritHit writ;
        public bool hit, killed;
    }

    /// <summary>Anything a gun can damage (a bot, the gallery's dummy).</summary>
    public interface ISkylineHittable
    {
        SkylineHitResult TakeHit(in SkylineHitInfo hit);
    }

    /// <summary>Marks a collider as part of a hittable: which zone, which plate, and whose. <see cref="owner"/> is
    /// serialized as a MonoBehaviour and cast to <see cref="ISkylineHittable"/> at hit time; an owner that isn't one makes
    /// the hit a no-op (hit = false).</summary>
    public sealed class SkylineHitZone : MonoBehaviour
    {
        public MonoBehaviour owner;
        public HitZone zone;
        public int plate = -1;

        public SkylineHitResult TakeHit(in SkylineHitInfo hit)
        {
            var hittable = owner as ISkylineHittable;
            if (hittable == null) return default(SkylineHitResult);
            var info = hit;
            info.zone = zone;
            info.plate = plate;
            return hittable.TakeHit(in info);
        }
    }

    /// <summary>Something aim assist may pull toward.</summary>
    public interface ISkylineAimable
    {
        Vector3 Center { get; }
        float Radius { get; }
        bool Alive { get; }
    }

    /// <summary>Aim assist's candidates: implementers add themselves in OnEnable and remove themselves in OnDisable.</summary>
    public static class SkylineTargets
    {
        public static readonly List<ISkylineAimable> Aimables = new List<ISkylineAimable>();
    }
}
