using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The Breacher's shove made visible on a bot (judge round 2, N3): a sprung lurch of the bot's rig, tipped back away from
    /// the shot and spun a little about its up axis, ringing out in about half a second. Added to a bot by
    /// <see cref="SkylineBreacherFx"/> on its first Breacher hit and kicked by every pellet after. Purely visual: it turns
    /// the rig (the drawn body) after the bot has posed it for the frame, so the bot's position, aim and colliders' logic
    /// are untouched. Every SkylineBot sets its rig's rotation outright in its own Update, so nothing accumulates frame to
    /// frame. The slide itself is the bot's own knockback (SkylineGuns.Breacher().enemyKnockback).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class SkylineShoveLurch : MonoBehaviour
    {
        // a damped spring per angle: stiffness, damping (about 1.6 Hz, settling in about half a second)
        const float Stiffness = 110f, Damping = 9f;
        // degrees a second of kick per whole shot (every pellet landing); a few pellets give a part of it
        const float TipKick = 520f, SpinKick = 420f;
        const float MaxTip = 38f, MaxSpin = 55f;

        SkylineBot _bot;
        Vector3 _axis = Vector3.right;
        float _tip, _tipVel, _spin, _spinVel, _spinSign = 1f;
        int _kickFrame = -1;

        /// <summary>The lurch on <paramref name="bot"/>, added on first use.</summary>
        public static SkylineShoveLurch On(SkylineBot bot)
        {
            if (bot == null) return null;
            var l = bot.GetComponent<SkylineShoveLurch>();
            if (l == null)
            {
                l = bot.gameObject.AddComponent<SkylineShoveLurch>();
                l._bot = bot;
            }
            return l;
        }

        /// <summary>One pellet's share (<paramref name="share"/>, 1 = a whole shot) of a shove along <paramref name="dir"/>.</summary>
        public void Kick(Vector3 dir, float share)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-6f) return;
            flat.Normalize();
            _axis = Vector3.Cross(Vector3.up, flat);   // tips the top away from the shooter
            _tipVel += TipKick * share;
            // the spin goes either way (a shove rarely lands dead centre), the same way for every pellet of one shot
            if (_kickFrame != Time.frameCount) { _kickFrame = Time.frameCount; _spinSign = Random.value < 0.5f ? -1f : 1f; }
            _spinVel += SpinKick * share * _spinSign;
        }

        void LateUpdate()
        {
            if (_bot == null) { Destroy(this); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Step(ref _tip, ref _tipVel, dt, MaxTip);
            Step(ref _spin, ref _spinVel, dt, MaxSpin);
            if (Mathf.Abs(_tip) < 0.05f && Mathf.Abs(_spin) < 0.05f && Mathf.Abs(_tipVel) < 0.5f && Mathf.Abs(_spinVel) < 0.5f) return;
            var rig = _bot.Rig;
            if (rig == null || rig == _bot.transform) return;   // only a rig the bot re-poses every frame
            rig.rotation = Quaternion.AngleAxis(_tip, _axis) * Quaternion.AngleAxis(_spin, Vector3.up) * rig.rotation;
        }

        static void Step(ref float angle, ref float vel, float dt, float max)
        {
            vel += (-Stiffness * angle - Damping * vel) * dt;
            angle = Mathf.Clamp(angle + vel * dt, -max, max);
        }
    }
}
