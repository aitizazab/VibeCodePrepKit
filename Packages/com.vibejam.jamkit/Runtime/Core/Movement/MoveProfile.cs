using System;

namespace JamKit.Movement
{
    /// <summary>Which movement a section uses: the dream's full set, or the waking world's grounded one.</summary>
    public enum MoveProfileKind { Dream, Daytime }

    /// <summary>
    /// A movement profile (docs/MOVEMENT.md "Profiles: dream and daytime"): which abilities a section allows, on top of
    /// the shared tuning. The dream allows everything. The daytime (waking-world) profile, at the creator's request
    /// (2026-10-11): no double jump, no wallrun, no slide, the air strafe turned well down, and jump fatigue (after
    /// three quick jumps, the next ones get lower) so nobody bunny-hops round a hospital. A <see cref="MoveSim"/> with no profile allows everything.
    /// </summary>
    [Serializable]
    public class MoveProfile
    {
        public bool doubleJump = true;
        public bool wallrun = true;
        /// <summary>Off: crouch at speed only crouches (no slide, no slide boost, no slide hop).</summary>
        public bool slide = true;
        /// <summary>Scales the air strafe (both its per-tick cap and its acceleration). Speed gained per tick goes with the
        /// square of the cap, so 0.3 leaves about 9% of the dream's gain: a jump still bends a little toward the stick,
        /// but strafing can't build speed. The slow-jump air control (below airControlSpeed) is untouched.</summary>
        public float airStrafeScale = 1f;
        /// <summary>Jump fatigue: ground jumps each within <see cref="jumpFatigueRecover"/> s of landing from the last
        /// one form a chain. The first <see cref="jumpFatigueFreeJumps"/> of a chain are full height; each one after that
        /// is lower by <see cref="jumpFatigueStep"/> of the height, down to <see cref="jumpFatigueMin"/>. Standing on the
        /// ground that long resets the chain. Daytime has it on (the creator, 2026-10-12: "Jump fatigue should be on for
        /// daytime, but only kick in after 3 jumps").</summary>
        public bool jumpFatigue;
        /// <summary>Full-height jumps at the start of a chain (3: the fourth quick jump is the first lower one).</summary>
        public int jumpFatigueFreeJumps = 3;
        public float jumpFatigueStep = 0.25f;
        public float jumpFatigueMin = 0.5f;
        public float jumpFatigueRecover = 0.8f;
        /// <summary>A lower horizontal speed cap while this profile is on (0: the def's). Used by the stealth kit's
        /// lucidity fade (watched: no dream powers, slower).</summary>
        public float speedCap;

        public static MoveProfile Dream() => new MoveProfile();

        public static MoveProfile Daytime() => new MoveProfile
        {
            doubleJump = false, wallrun = false, slide = false, airStrafeScale = 0.3f, jumpFatigue = true,
        };

        public MoveProfile Clone() => (MoveProfile)MemberwiseClone();

        /// <summary>The height share of a jump that is <paramref name="chain"/> jumps into a fatigue chain (0: the first).</summary>
        public float FatigueHeight(int chain)
            => jumpFatigue ? Math.Max(jumpFatigueMin, 1f - jumpFatigueStep * Math.Max(0, chain + 1 - Math.Max(1, jumpFatigueFreeJumps))) : 1f;
    }
}
