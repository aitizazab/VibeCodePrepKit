using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Visiting Hours' presenter-mode chapters and attract script (docs/PRESENTING.md): the opening
    /// (on the title, from anywhere), Day 1, the booth, the street, the argument with Walt, and each
    /// outcome's end card. The opening is registered by VhBoot (it needs no run); the rest by the game.
    /// </summary>
    public static class VhPresenter
    {
        public const string Scene = "VisitingHours";

        public const string Opening = "Opening";
        public const string Day1 = "Day 1";
        public const string Booth = "The booth";
        public const string Street = "The street";
        public const string Argument = "The argument with Walt";
        public const string EndConvinced = "End card: Convinced";
        public const string EndHeardOut = "End card: Heard out";
        public const string EndPressed = "End card: Pressed";
        public const string EndLost = "End card: Composure lost";

        public static readonly string[] Chapters = { Opening, Day1, Booth, Street, Argument, EndConvinced, EndHeardOut, EndPressed, EndLost };

        /// <summary>Attract-mode segments VisitingHoursGame.AttractDrive plays.</summary>
        public static readonly string[] Segments = { "walk", "argument" };

        /// <summary>About 85 s: the opening over the title, Nadia charting on Day 1, the street at night,
        /// the argument (played by its autopilot at real speed), the Convinced end card.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(Opening, 0.5f).Wait(14f)
            .Jump(Day1, 1.5f).Drive("walk", 22f)
            .Jump(Street, 1.5f).Drive("walk", 18f)
            .Jump(Argument, 1f).Drive("argument", 22f)
            .Jump(EndConvinced, 0.5f).Wait(5f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        /// <summary>The opening (works from any state: it goes back to the title and plays it there).</summary>
        public static void RegisterOpening(ChapterRegistry chapters) =>
            chapters.Register(Scene, Opening, VhBoot.PresenterOpening, PresenterMode.AnyTime);

        /// <summary>Everything else, live while the slice's scene runs.</summary>
        public static void Register(ChapterRegistry chapters, VisitingHoursGame game, object owner)
        {
            chapters.Register(Scene, Day1, () => game.PresenterDay1(), owner);
            chapters.Register(Scene, Booth, () => game.PresenterBooth(), owner);
            chapters.Register(Scene, Street, () => game.PresenterStreet(), owner);
            chapters.Register(Scene, Argument, () => game.PresenterArgument(), owner);
            chapters.Register(Scene, EndConvinced, () => game.PresenterEnd(BattleOutcome.Convinced), owner);
            chapters.Register(Scene, EndHeardOut, () => game.PresenterEnd(BattleOutcome.HeardOut), owner);
            chapters.Register(Scene, EndPressed, () => game.PresenterEnd(BattleOutcome.Pressed), owner);
            chapters.Register(Scene, EndLost, () => game.PresenterEnd(BattleOutcome.ComposureLost), owner);
        }
    }
}
