using System;
using System.Collections.Generic;

namespace JamKit.Movement
{
    /// <summary>The moves a course asks for (each with the size it must fit), for <see cref="CourseReach.Check"/>.</summary>
    public enum CourseBeatKind
    {
        RunUp, FenceHop, Wallrun, CarHop, Slide, Mantle, Gap, DoubleGap, WallToWall, SlideRamp,
        /// <summary>A waist-high block: hop it (the fast line) or mantle it and sprint-kick away (the safe line).</summary>
        Vault,
        /// <summary>Wall-kick up between two walls <see cref="CourseBeat.size"/> apart, climbing <see cref="CourseBeat.height"/>.</summary>
        Chimney,
        /// <summary>A slide jump over a gap landing with crouch held, straight back into a slide.</summary>
        SlideHop,
        /// <summary>An optional gap only momentum clears (a slide boost, slide-hops, air strafing): more than a double
        /// jump at sprint, less than a double jump at the slide boost's cap.</summary>
        Skip,
    }

    /// <summary>One movement beat of a route with the size that has to fit the movement's reach.</summary>
    public sealed class CourseBeat
    {
        public CourseBeatKind kind;
        public string name;
        /// <summary>Where the beat starts along the route.</summary>
        public float s;
        /// <summary>The gap, wall length or spacing it asks the player to cover (m).</summary>
        public float size;
        /// <summary>The height it asks the player to clear or climb (m), or the clearance under a slide bar.</summary>
        public float height;
    }

    /// <summary>
    /// Checks a course's beats against the movement's measured reach (docs/RESEARCH-MOVEMENT.md 4.3) and the Unity
    /// controller's clearance (a slide gap must clear the crouched capsule, its skin and the crouched step offset).
    /// Shared by the Movement Lab's Gauntlet and Visiting Hours' dream escape.
    /// </summary>
    public static class CourseReach
    {
        /// <summary>Clearance a slide gap needs over the crouched capsule (the controller's sweep must not graze it).</summary>
        public const float SlideMargin = 0.05f;
        /// <summary>The longest wall one wallrun covers: a 1.75 s run at 12 m/s (longer needs a kick).</summary>
        public const float WallMax = 21.5f;

        /// <summary>Flat-ground distance of a single jump at <paramref name="speed"/>.</summary>
        public static float SingleJumpReach(MoveDef def, float speed)
        {
            float v0 = (float)Math.Sqrt(2f * def.gravity * def.jumpHeight);
            return speed * 2f * v0 / def.gravity;
        }

        /// <summary>Flat-ground distance of a jump plus a double jump at its apex.</summary>
        public static float DoubleJumpReach(MoveDef def, float speed)
        {
            float g = def.gravity;
            float up1 = (float)Math.Sqrt(2f * def.jumpHeight / g);
            float up2 = (float)Math.Sqrt(2f * def.doubleJumpHeight / g);
            float down = (float)Math.Sqrt(2f * (def.jumpHeight + def.doubleJumpHeight) / g);
            return speed * (up1 + up2 + down);
        }

        /// <summary>The lowest gap a slide passes at full speed on Unity's CharacterController: the crouched capsule,
        /// its skin, and the step offset the controller's step-up pass lifts it by before sweeping forward.</summary>
        public static float SlideGapNeeded(MoveDef def, float skin, float crouchStepOffset) =>
            def.slideHeight + skin + crouchStepOffset + SlideMargin;

        /// <summary>Every beat that doesn't fit, in words (empty = all good). <paramref name="skin"/> and
        /// <paramref name="crouchStepOffset"/> are the Unity controller's (MovementController skinWidth, crouchStepOffset).</summary>
        public static List<string> Check(IList<CourseBeat> beats, MoveDef def, float skin = 0.04f, float crouchStepOffset = 0f)
        {
            var problems = new List<string>();
            float single = SingleJumpReach(def, def.sprintSpeed);
            float dbl = DoubleJumpReach(def, def.sprintSpeed);
            foreach (var b in beats)
            {
                switch (b.kind)
                {
                    case CourseBeatKind.FenceHop:
                        if (b.height > def.jumpHeight - 0.25f) problems.Add($"{b.name}: {b.height:0.00} m is too tall to hop (jump {def.jumpHeight:0.00} m)");
                        if (b.height < def.mantleMinHeight) problems.Add($"{b.name}: {b.height:0.00} m is under the mantle height, so a missed hop isn't rescued");
                        break;
                    case CourseBeatKind.Vault:
                        if (b.height > def.jumpHeight - 0.2f) problems.Add($"{b.name}: {b.height:0.00} m is too tall to hop (jump {def.jumpHeight:0.00} m)");
                        if (b.height < def.mantleMinHeight || b.height > def.mantleMaxHeight - 0.2f) problems.Add($"{b.name}: {b.height:0.00} m isn't a mantle, so the safe line can't climb it");
                        if (b.size > 1f) problems.Add($"{b.name}: {b.size:0.0} m deep is too deep to hop");
                        break;
                    case CourseBeatKind.CarHop:
                        if (b.height > def.jumpHeight - 0.15f) problems.Add($"{b.name}: roof {b.height:0.00} m above the jump's {def.jumpHeight:0.00} m");
                        if (b.size > single - 1.5f) problems.Add($"{b.name}: {b.size:0.0} m between roofs is too far for a hop ({single:0.0} m)");
                        break;
                    case CourseBeatKind.Gap:
                    case CourseBeatKind.SlideHop:
                        if (b.size > single - 0.35f) problems.Add($"{b.name}: {b.size:0.0} m gap, a single jump reaches {single:0.0} m");
                        if (b.size < 3f) problems.Add($"{b.name}: {b.size:0.0} m gap is too small to read as a gap");
                        break;
                    case CourseBeatKind.DoubleGap:
                        if (b.size > dbl - 1f) problems.Add($"{b.name}: {b.size:0.0} m gap, a double jump reaches {dbl:0.0} m");
                        if (b.size <= single) problems.Add($"{b.name}: {b.size:0.0} m doesn't need the double jump (single {single:0.0} m)");
                        break;
                    case CourseBeatKind.Skip:
                    {
                        float fast = DoubleJumpReach(def, def.slideBoostMaxSpeed);
                        if (b.size <= dbl + 0.5f) problems.Add($"{b.name}: {b.size:0.0} m is no skip, a double jump at sprint reaches {dbl:0.0} m");
                        if (b.size > fast - 0.5f) problems.Add($"{b.name}: {b.size:0.0} m is out of reach even at the slide boost's {def.slideBoostMaxSpeed:0} m/s ({fast:0.0} m)");
                        break;
                    }
                    case CourseBeatKind.Wallrun:
                        if (b.size < 8f || b.size > WallMax) problems.Add($"{b.name}: wall {b.size:0.0} m (8 to {WallMax:0.0})");
                        if (b.height < 3f) problems.Add($"{b.name}: wall only {b.height:0.0} m above the floor (3 m at least)");
                        break;
                    case CourseBeatKind.WallToWall:
                        if (b.size < 3.5f || b.size > 6.5f) problems.Add($"{b.name}: walls {b.size:0.0} m apart (3.5 to 6.5 chain)");
                        break;
                    case CourseBeatKind.Chimney:
                        if (b.size < 3f || b.size > 4f) problems.Add($"{b.name}: walls {b.size:0.0} m apart (3 to 4 climbs, about 1.2 m a kick)");
                        if (b.height > 5f) problems.Add($"{b.name}: a {b.height:0.0} m climb is too high for one chimney");
                        break;
                    case CourseBeatKind.Mantle:
                        if (b.height < def.mantleMinHeight || b.height > def.mantleMaxHeight - 0.2f) problems.Add($"{b.name}: step {b.height:0.00} m outside the mantle's {def.mantleMinHeight:0.0}-{def.mantleMaxHeight - 0.2f:0.0} m");
                        break;
                    case CourseBeatKind.Slide:
                    {
                        float need = SlideGapNeeded(def, skin, crouchStepOffset);
                        if (b.height < need) problems.Add($"{b.name}: {b.height:0.00} m under the bar; a slide needs {need:0.00} (capsule {def.slideHeight:0.00} + skin {skin:0.00} + crouched step offset {crouchStepOffset:0.00} + {SlideMargin:0.00})");
                        if (b.height > def.standHeight + skin - 0.05f) problems.Add($"{b.name}: {b.height:0.00} m under the bar, you could walk under it");
                        break;
                    }
                }
            }
            return problems;
        }
    }
}
