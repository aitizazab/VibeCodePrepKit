using System;
using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>What one shot (all its pellets) did.</summary>
    public struct SkylineShotOutcome
    {
        public bool hit, killed, weakPoint, spot;
        /// <summary>Damage applied over every pellet that hit (after falloff and multipliers).</summary>
        public float damage;
        /// <summary>Distance to the first thing hit (m), or the gun's range.</summary>
        public float range;
        /// <summary>The strongest result among the pellets (kill, then spot, then weak point, then a hit), for the marker.</summary>
        public SkylineHitResult marker;
    }

    /// <summary>Hitscan firing (docs/SKYLINE-PLAN-M1.md Task 14): pellets spread uniformly in the gun's cone from a seeded
    /// Rng, each a Physics.Raycast up to the gun's range; a SkylineHitZone hit becomes a SkylineHitInfo with the falloff
    /// applied, the Writ's flag and the knockback split across the pellets.</summary>
    public static class SkylineShooting
    {
        /// <summary>Everything but Ignore Raycast (layer 2, where MovementController puts the player's own body).</summary>
        public const int Mask = ~(1 << 2);

        /// <summary>Fires <paramref name="gun"/> from <paramref name="eye"/> along <paramref name="aim"/>. Each pellet that
        /// reaches a hit zone calls its TakeHit and <paramref name="onHit"/>; tracers start at <paramref name="muzzle"/>.</summary>
        public static SkylineShotOutcome Fire(GunDef gun, Vector3 eye, Vector3 aim, Vector3 muzzle, Rng rng,
            Action<SkylineHitInfo, SkylineHitResult> onHit)
        {
            var o = new SkylineShotOutcome { range = gun.range };
            if (aim.sqrMagnitude < 1e-8f) return o;
            aim.Normalize();
            bool fromWrit = gun.id == SkylineGuns.WritId;
            int pellets = Math.Max(1, gun.pellets);
            float knock = gun.enemyKnockback / pellets;
            float cosMax = Mathf.Cos(Mathf.Max(0f, gun.spreadDeg) * Mathf.Deg2Rad);
            Quaternion toAim = Quaternion.FromToRotation(Vector3.forward, aim);
            bool anyRange = false;
            int rank = 0;
            bool hitSounded = false, worldSounded = false;   // one hit sound and one world sound a shot, however many pellets
            // One hit sound a shot comes from the gun's own impact (writ/impact, writ/impact_weak, breacher/impact, played by
            // its fx on onHit); impact/hit is only the fallback for a gun with no "<gun>/impact" sound. A Writ spot adds the
            // spot's own sound (spot_form / spot_break), so two at most. World hits keep impact/world, once a shot.
            bool ownImpact = SkylineSounds.Get(gun.id + "/impact") != null;
            for (int p = 0; p < pellets; p++)
            {
                Vector3 dir = aim;
                if (gun.spreadDeg > 0f)
                {
                    // Uniform over the cone's cap: cos(theta) uniform in [cos(spread), 1], phi uniform.
                    float cos = 1f - rng.NextFloat() * (1f - cosMax);
                    float sin = Mathf.Sqrt(Mathf.Max(0f, 1f - cos * cos));
                    float phi = rng.NextFloat() * Mathf.PI * 2f;
                    dir = toAim * new Vector3(sin * Mathf.Cos(phi), sin * Mathf.Sin(phi), cos);
                }
                Vector3 end = eye + dir * gun.range;
                if (Physics.Raycast(eye, dir, out var hit, gun.range, Mask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    if (!anyRange) { o.range = hit.distance; anyRange = true; }
                    var zone = hit.collider.GetComponent<SkylineHitZone>();
                    bool onHittable = false;
                    if (zone != null)
                    {
                        var info = new SkylineHitInfo
                        {
                            gun = gun,
                            damage = gun.damage * gun.FalloffScale(hit.distance),
                            plate = -1,
                            point = hit.point,
                            normal = hit.normal,
                            direction = dir,
                            fromWrit = fromWrit,
                            knockback = knock,
                            time = Time.time,
                        };
                        var r = zone.TakeHit(in info);
                        if (r.hit)
                        {
                            onHittable = true;
                            info.zone = zone.zone;
                            info.plate = zone.plate;
                            o.hit = true;
                            o.damage += r.damage.Applied > 0f ? r.damage.Applied : info.damage * (r.writ.multiplier > 0f ? r.writ.multiplier : 1f);
                            bool weak = zone.zone == HitZone.WeakPoint;
                            if (r.killed) o.killed = true;
                            if (r.writ.spotHit) o.spot = true;
                            if (weak) o.weakPoint = true;
                            int k = r.killed ? 4 : r.writ.spotHit ? 3 : weak ? 2 : 1;
                            if (k > rank) { rank = k; o.marker = r; }
                            onHit?.Invoke(info, r);
                        }
                    }
                    SkylineImpact.Spawn(hit.point, hit.normal, onHittable);
                    if (onHittable && !hitSounded && ownImpact) hitSounded = true;   // the gun's fx plays its own impact
                    if (onHittable ? !hitSounded : !worldSounded)
                    {
                        SkylineSounds.PlayAt(onHittable ? SkylineImpact.HitSound : SkylineImpact.WorldSound, hit.point, onHittable ? 0.8f : 0.5f);
                        if (onHittable) hitSounded = true; else worldSounded = true;
                    }
                }
                SkylineTracer.Spawn(muzzle, end, new Color(1f, 0.92f, 0.7f));
            }
            return o;
        }

        /// <summary>The marker's colour: white a hit, yellow a weak point, gold a Writ spot, red a kill.</summary>
        public static Color MarkerColor(in SkylineHitResult r, bool weakPoint)
        {
            if (r.killed) return new Color(1f, 0.18f, 0.15f);
            if (r.writ.spotHit) return new Color(1f, 0.72f, 0.1f);
            if (weakPoint) return new Color(1f, 0.95f, 0.25f);
            return Color.white;
        }
    }
}
