using JamKit.Skyline;
using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// What the player feels when the Unit is hit, and the beats around a fight (judge round 1: "taking damage barely
    /// registers"; "the fight start and the plating break have no camera or audio beat").
    /// <list type="bullet">
    /// <item><see cref="Hurt"/> (hooked in <see cref="SkylinePlayer.TakeAttack"/>): the hurt sound ("player/hurt", louder for a
    /// bigger hit, at most one every 0.09 s), a small camera knock down and away from the attacker, a little shake, and the
    /// HUD's red edge vignette. When the plating breaks: the plating-break sound ("player/plating_break"), a bigger knock and
    /// shake, and the HUD's break flash.</item>
    /// <item><see cref="FightStart"/> (hooked in the arena director's encounter start): CONCIERGE's security sting
    /// ("concierge/sting"), a light shake and the HUD's notice banner.</item>
    /// <item><see cref="ConciergeChime"/>: CONCIERGE's PA chime ("concierge/chime") before each of its notices, skipped right
    /// after the sting.</item>
    /// </list>
    /// The sounds are named cues played through <see cref="SkylineSounds.PlayViewmodel"/> (the mix owns their levels);
    /// they are synthesized by tools/skyline/player/render.py. Static: nothing to add to a scene.
    /// </summary>
    public static class SkylineDamageFeedback
    {
        public const string HurtSound = "player/hurt", PlatingBreakSound = "player/plating_break";
        public const string StingSound = "concierge/sting", ChimeSound = "concierge/chime";
        /// <summary>How long CONCIERGE's voice waits after its chime starts.</summary>
        public const float ChimeLead = 0.6f;
        const float HurtSoundGap = 0.18f, StingQuiet = 1.6f;

        static readonly CameraKickDef HurtKick = new CameraKickDef { pitchDeg = -1.1f, yawDeg = 1.5f, recoverRate = 8f };
        static readonly CameraKickDef BreakKick = new CameraKickDef { pitchDeg = -2.4f, yawDeg = 3f, recoverRate = 6f };

        static float _lastHurtSound = -10f, _lastSting = -10f;

        /// <summary>The Unit took <paramref name="hit"/> from something at <paramref name="attackerPosition"/>.</summary>
        public static void Hurt(SkylinePlayer player, ArmorHit hit, Vector3 attackerPosition)
        {
            if (player == null || player.Armor == null) return;
            float dealt = hit.toPlating + hit.toChassis;
            float max = Mathf.Max(1f, player.Armor.PlatingMax + player.Armor.ChassisMax);
            // a share of the Unit's whole armour, eased so a chip still reads and a big hit reads bigger
            float k = Mathf.Clamp01(0.35f + 4f * dealt / max);
            bool chassis = hit.toChassis > 0f;

            float now = Time.unscaledTime;
            if (now - _lastHurtSound >= HurtSoundGap)
            {
                _lastHurtSound = now;
                SkylineSounds.PlayViewmodel(HurtSound, Mathf.Lerp(0.55f, 0.95f, k));
            }
            if (hit.platingBroke) SkylineSounds.PlayViewmodel(PlatingBreakSound, 1f);

            // knock the view away from the attacker: yaw to the side the hit didn't come from
            Vector3 to = attackerPosition - player.transform.position;
            float bearing = Mathf.DeltaAngle(player.Yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            float side = Mathf.Abs(bearing) < 10f ? 0f : -Mathf.Sign(bearing);
            player.Kick.Kick(hit.platingBroke ? BreakKick : HurtKick, side, hit.platingBroke ? 1f : k);
            player.Shake.AddTrauma(hit.platingBroke ? 0.45f : 0.12f + 0.2f * k);

            var hud = SkylineHud.Instance;
            if (hud != null)
            {
                hud.Hurt(chassis ? Mathf.Max(0.7f, k) : k, chassis);
                if (hit.platingBroke) hud.PlatingBreak();
            }
        }

        /// <summary>A fight has started: CONCIERGE's security sting, a light shake and the notice banner.</summary>
        public static void FightStart(SkylinePlayer player)
        {
            _lastSting = Time.unscaledTime;
            SkylineSounds.PlayViewmodel(StingSound, 0.9f);
            if (player != null) player.Shake.AddTrauma(0.15f);
            var hud = SkylineHud.Instance;
            if (hud != null) hud.Banner("CONCIERGE · SECURITY NOTICE", "Unscheduled guest activity. Security is responding.");
        }

        /// <summary>Plays CONCIERGE's PA chime before a notice and returns how long the voice should wait; right after the
        /// fight sting (which already announced CONCIERGE) no chime, and no wait.</summary>
        public static float ConciergeChime()
        {
            if (Time.unscaledTime - _lastSting < StingQuiet) return 0f;
            SkylineSounds.PlayViewmodel(ChimeSound, 0.75f);
            return ChimeLead;
        }
    }
}
