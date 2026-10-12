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
        public const string EditOpening = "Edit the opening";
        public const string Day1 = "Day 1";
        public const string Booth = "The booth";
        public const string Street = "The street";
        public const string Argument = "The argument with Walt";
        public const string EndConvinced = "End card: Convinced";
        public const string EndHeardOut = "End card: Heard out";
        public const string EndPressed = "End card: Pressed";
        public const string EndLost = "End card: Composure lost";
        // The chain (docs/NIGHTS-1-2.md): a jump to each segment after Day 1, from anywhere (the journal as the save has it).
        public const string ChainNight1 = "Chain: Night 1 (the street)";
        public const string ChainDay2 = "Chain: Day 2";
        public const string ChainNight2 = "Chain: Night 2 (the house)";
        public const string ChainEnd = "Chain: the end card";

        /// <summary>In the panel's order, which is the order they're registered in: the any-time ones first (VhBoot registers
        /// them when the game starts: the opening, its editor, the chain's jumps), then the slice's (while it runs).</summary>
        public static readonly string[] Chapters = { Opening, EditOpening, ChainNight1, ChainDay2, ChainNight2, ChainEnd, Day1, Booth, Street, Argument, EndConvinced, EndHeardOut, EndPressed, EndLost };

        /// <summary>The chapters that work from any state (registered by VhBoot, not the slice): the opening, its editor,
        /// the chain's jumps.</summary>
        public static readonly string[] AnyTimeChapters = { Opening, EditOpening, ChainNight1, ChainDay2, ChainNight2, ChainEnd };

        /// <summary>Attract-mode segments VisitingHoursGame.AttractDrive plays.</summary>
        public static readonly string[] Segments = { "walk", "argument" };

        /// <summary>About 90 s: the opening (to the cut to the phone from above), Nadia charting on Day 1, the street at night,
        /// the argument (played by its autopilot at real speed), the Convinced end card.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(Opening, 0.5f).Wait(18f)
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
        public static void RegisterOpening(ChapterRegistry chapters)
        {
            chapters.Register(Scene, Opening, VhBoot.PresenterOpening, PresenterMode.AnyTime);
            // The opening editor (docs/OPENING-EDITOR.md): goes to the title and opens it there.
            chapters.Register(Scene, EditOpening, VhOpeningEditor.OpenFromAnywhere, PresenterMode.AnyTime);
            chapters.Register(Scene, ChainNight1, () => JumpChain(ChainSegment.Night1), PresenterMode.AnyTime);
            chapters.Register(Scene, ChainDay2, () => JumpChain(ChainSegment.Day2), PresenterMode.AnyTime);
            chapters.Register(Scene, ChainNight2, () => JumpChain(ChainSegment.Night2), PresenterMode.AnyTime);
            chapters.Register(Scene, ChainEnd, () => JumpChain(ChainSegment.End), PresenterMode.AnyTime);
        }

        static void JumpChain(ChainSegment segment)
        {
            var runner = VhChainRunner.Instance;
            if (runner != null) runner.JumpTo(segment);
        }

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
