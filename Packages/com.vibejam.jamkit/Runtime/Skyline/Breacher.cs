using System;
using System.Numerics;

namespace JamKit.Skyline
{
    /// <summary>The Breacher's self-knockback (docs/SKYLINE-DESIGN.md 2.2) as tunable data. The boost is limited only by the
    /// gun's fire rate (the creator's call, 2026-10-13); the one-boost-per-airtime rule stays as an option
    /// (<see cref="BreacherBoostDef.oneBoostPerAirtime"/>). No self-damage; no double-jump refresh (the engine's rule); about the jump
    /// upgrade's height. A braced shooter (on the ground, crouched, or wall-running) keeps only a share of the flat push and never
    /// lifts: the boost is felt only mid-air (the creator's call, 2026-10-13 evening).</summary>
    public sealed class BreacherBoostDef
    {
        /// <summary>m/s along the flat part of the shot's reverse direction.</summary>
        public float selfPushSpeed = 6f;
        /// <summary>Metres a straight-down shot lifts you, from rest or from a fall (it cancels the fall first).</summary>
        public float boostHeight = 1f;
        /// <summary>The shot must point at least this far down (the reverse direction's up part, 0..1) to lift you.</summary>
        public float minLift = 0.35f;
        /// <summary>The share of the flat push kept when the shooter is braced (on the ground, crouched, or wall-running); braced
        /// shots never lift. The creator's call (2026-10-13 evening): "a Breacher shot during a wall-run should just be mostly
        /// eaten up by the wall, the same when standing or crouched on the ground. The impulse should only be felt when shot mid-air."</summary>
        public float bracedScale = 0.2f;
        /// <summary>True: only one air boost per airtime (spent by a self-push in the air; a braced shot never spends it;
        /// refreshed by landing or touching a wall). False (the default; the creator's call, 2026-10-13: "just limited by its
        /// fire rate"): every shot boosts, setting the rise speed and never adding to a faster one.</summary>
        public bool oneBoostPerAirtime = false;
        /// <summary>Damage the shooter takes from their own blast (none, by the spec's proposal).</summary>
        public float selfDamage = 0f;
    }

    /// <summary>The optional one-boost-per-airtime rule's charge (only checked when <see cref="BreacherBoostDef.oneBoostPerAirtime"/>):
    /// spent by a self-push in the air (a braced shot never spends it), refreshed
    /// by landing or touching a wall. Call <see cref="Observe"/> every frame with the body's state. The engine can refuse
    /// a push, so a caller that asked for one reports the answer through <see cref="Commit"/>.</summary>
    public sealed class BoostCharge
    {
        public bool Ready { get; private set; } = true;

        public void Observe(bool grounded, bool onWall)
        {
            if (grounded || onWall) Ready = true;
        }

        public void Spend() => Ready = false;

        /// <summary>Settles a requested push: spends the boost only when the push would have spent it
        /// (<paramref name="spendsCharge"/>, from <see cref="BreacherKnockback.RequestSelfPush"/>) and the engine
        /// accepted it. A refused push costs nothing.</summary>
        public void Commit(bool spendsCharge, bool accepted)
        {
            if (spendsCharge && accepted) Spend();
        }
    }

    public static class BreacherKnockback
    {
        /// <summary>The velocity change a Breacher shot along <paramref name="aim"/> would give its shooter (for
        /// MoveSim.AddPush). <paramref name="braced"/>: the shooter is on the ground (standing, crouched or sliding) or
        /// wall-running. A braced shot is mostly eaten by the floor or the wall: only the flat part of the reverse direction times
        /// <see cref="BreacherBoostDef.selfPushSpeed"/> x <see cref="BreacherBoostDef.bracedScale"/>, with no lift (Y = 0), and it
        /// never spends the charge; so a jump pressed with the shot is still a normal ground jump (the shot first, then the jump).
        /// In the air: the flat part times <see cref="BreacherBoostDef.selfPushSpeed"/>, plus, when the shot points down by more
        /// than <see cref="BreacherBoostDef.minLift"/>, enough upward speed to reach sqrt(2 g boostHeight) x (how far down it
        /// points) from the current vertical speed (never negative). With <see cref="BreacherBoostDef.oneBoostPerAirtime"/> on,
        /// an air shot needs the charge (zero without it); off (the default) every air shot lifts. Pure: it spends nothing.
        /// <paramref name="spendsCharge"/> says whether the push uses the boost (any air shot; never a braced one); pass the
        /// engine's accept/refuse answer to <see cref="BoostCharge.Commit"/> to spend it only when the push was accepted.</summary>
        public static Vector3 RequestSelfPush(Vector3 aim, Vector3 velocity, bool braced, float gravity, BoostCharge charge, BreacherBoostDef d, out bool spendsCharge)
        {
            spendsCharge = false;
            if (d == null || charge == null) return Vector3.Zero;
            float len = aim.Length();
            if (!(len > 1e-4f) || float.IsInfinity(len)) return Vector3.Zero;
            Vector3 back = -aim / len;
            var flat = new Vector3(back.X, 0f, back.Z) * d.selfPushSpeed;
            if (braced) return flat * MathF.Max(0f, d.bracedScale);
            if (d.oneBoostPerAirtime && !charge.Ready) return Vector3.Zero;
            var push = flat;
            if (back.Y > d.minLift)
            {
                float boostUp = MathF.Sqrt(2f * MathF.Max(0f, gravity) * MathF.Max(0f, d.boostHeight));
                float vy = float.IsNaN(velocity.Y) ? 0f : velocity.Y;
                push.Y = MathF.Max(0f, boostUp * back.Y - vy);
            }
            spendsCharge = true;
            return push;
        }

        /// <summary>Request and commit in one go, for a caller whose push cannot be refused (or in tests): the same
        /// velocity change as <see cref="RequestSelfPush"/> (<paramref name="braced"/>: on the ground, crouched, or wall-running),
        /// with the boost spent right away when it is used.</summary>
        public static Vector3 SelfPush(Vector3 aim, Vector3 velocity, bool braced, float gravity, BoostCharge charge, BreacherBoostDef d, out bool boosted)
        {
            Vector3 push = RequestSelfPush(aim, velocity, braced, gravity, charge, d, out boosted);
            charge?.Commit(boosted, true);
            return push;
        }
    }
}
