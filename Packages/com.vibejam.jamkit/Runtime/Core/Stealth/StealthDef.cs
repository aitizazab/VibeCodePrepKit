using System;

namespace JamKit.Stealth
{
    /// <summary>
    /// Every stealth number in one place (docs/STEALTH.md "Every tunable number"): sight, the detection meter, the alert
    /// states, hearing and the seeker's walk. Metres, seconds, degrees. Held by a StealthTuning asset in Unity, or made
    /// in code. Serialized data: public fields only.
    /// </summary>
    [Serializable]
    public class StealthDef
    {
        // ---------- Sight ----------
        /// <summary>Eye height above the seeker's feet.</summary>
        public float eyeHeight = 1.6f;
        /// <summary>Nothing farther than this is seen.</summary>
        public float sightRange = 18f;
        /// <summary>Half the width of the main vision cone (degrees): full sight inside it.</summary>
        public float coneHalfAngle = 40f;
        /// <summary>Half the width of the peripheral band (degrees): outside the cone but inside this, sight is weaker.</summary>
        public float peripheralHalfAngle = 75f;
        /// <summary>How well the peripheral band sees, against the cone (0..1).</summary>
        public float peripheralScale = 0.3f;
        /// <summary>How far up or down it looks (degrees from level, to the target's chest). 90 = no limit (the default);
        /// a child seeker who "doesn't look up" uses about 15, so a roof or a treehouse a few metres off is above its sight
        /// (docs/DENSE-STREET.md 9, rule 3). The close sense ignores it.</summary>
        public float verticalHalfAngle = 90f;
        /// <summary>Within this distance the seeker senses you in any direction (a breath on the neck).</summary>
        public float closeRange = 1.4f;
        /// <summary>Full-rate sight up to this distance; beyond it the rate falls toward <see cref="farScale"/> at sightRange.</summary>
        public float nearRange = 5f;
        public float farScale = 0.3f;

        // ---------- What makes you easier or harder to see ----------
        /// <summary>Crouched (not sliding): a smaller, slower shape.</summary>
        public float crouchScale = 0.55f;
        /// <summary>Sliding: low, but fast and loud.</summary>
        public float slideScale = 0.8f;
        /// <summary>Moving at or above <see cref="fastSpeed"/> draws the eye.</summary>
        public float fastScale = 1.3f;
        public float fastSpeed = 7f;
        /// <summary>Standing still.</summary>
        public float stillScale = 0.75f;
        public float stillSpeed = 0.4f;
        /// <summary>Sight in full shadow (light 0), against full light (1).</summary>
        public float shadowScale = 0.25f;
        /// <summary>In a bush: this much of you shows, unless the seeker is within <see cref="bushRevealRange"/>.</summary>
        public float bushScale = 0.12f;
        public float bushRevealRange = 1.5f;
        /// <summary>The body is sampled at these fractions of its height for line of sight (feet to head).</summary>
        public float sampleLow = 0.2f, sampleMid = 0.55f, sampleHigh = 0.92f;

        // ---------- The detection meter ----------
        /// <summary>Meter per second at visibility 1 (0 to full in 1/fillRate seconds: about 0.8 s up close in the open).</summary>
        public float fillRate = 1.25f;
        /// <summary>The meter fills this much faster while the seeker is suspicious or searching (it's looking for you).</summary>
        public float alertFillScale = 1.5f;
        /// <summary>Unseen this long, the meter starts to drain...</summary>
        public float drainDelay = 1.2f;
        /// <summary>...at this rate per second.</summary>
        public float drainRate = 0.3f;
        /// <summary>The meter level that makes an unaware seeker suspicious.</summary>
        public float suspiciousAt = 0.3f;
        /// <summary>A noise heard adds this to the meter (at most up to suspiciousAt).</summary>
        public float noiseBump = 0.3f;

        // ---------- Alert states ----------
        /// <summary>Suspicious: turns to the stimulus and holds this long before walking over.</summary>
        public float reactTime = 0.6f;
        /// <summary>Searching: looks around at the last known position for this long.</summary>
        public float lookAroundTime = 2.4f;
        /// <summary>How far it sweeps its gaze each side while looking around (degrees).</summary>
        public float lookAroundAngle = 70f;
        /// <summary>Hiding spots within this distance of the last known position get checked.</summary>
        public float spotSearchRadius = 10f;
        /// <summary>Time spent checking one hiding spot.</summary>
        public float checkSpotTime = 1.1f;
        /// <summary>A patrol's search gives up after this long and the seeker returns to unaware.</summary>
        public float searchTime = 16f;
        /// <summary>Found, then unseen this long: back to searching at the last known position (patrol mode).</summary>
        public float loseSightTime = 2f;
        /// <summary>Hide-and-seek: seconds counted (eyes closed) before seeking.</summary>
        public float countTime = 10f;
        /// <summary>Close enough to the target to stop chasing.</summary>
        public float catchRange = 1.3f;

        // ---------- Walking ----------
        public float patrolSpeed = 1.8f;
        public float investigateSpeed = 2.6f;
        public float searchSpeed = 3.2f;
        public float chaseSpeed = 4.6f;
        /// <summary>Turn rate (degrees per second).</summary>
        public float turnRate = 200f;
        /// <summary>Reaching a point within this counts as there.</summary>
        public float arriveRadius = 0.35f;
        /// <summary>Pause at each patrol point.</summary>
        public float patrolPause = 1.2f;

        // ---------- Hearing ----------
        /// <summary>Noise radius scale for this seeker (2 = sharp ears).</summary>
        public float hearingScale = 1f;
        /// <summary>Through a wall (no line between them) a noise carries this share of its radius.</summary>
        public float occludedHearing = 0.5f;
        /// <summary>A landing at or above this downward speed (m/s) is hard enough to be heard.</summary>
        public float hardLandingSpeed = 7f;
        public float landingNoiseRadius = 12f;
        /// <summary>Sprinting footsteps: one noise of this radius every <see cref="footstepEvery"/> seconds.</summary>
        public float sprintNoiseRadius = 7f;
        public float footstepEvery = 0.35f;
        public float wallrunNoiseRadius = 9f;
        public float slideNoiseRadius = 5f;
    }
}
