using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// The Movement Lab's presenter-mode chapters (docs/PRESENTING.md): the Gauntlet (start, teaching half, combo
    /// half, finish), then the old course as free roam: the course start, the wallrun
    /// section, the momentum chain, the chimney and the chase, each a checkpoint of the course; and
    /// an attract script in which the course autopilot runs the Gauntlet, then the momentum chain.
    /// </summary>
    public static class MovementLabPresenter
    {
        public const string Scene = "MovementLab";

        public static readonly string[] Chapters =
        {
            GauntletCourse.ChapterStart, GauntletCourse.ChapterTeaching, GauntletCourse.ChapterCombo, GauntletCourse.ChapterFinish,
            LabCourse.ChapterStart, LabCourse.ChapterWallruns, LabCourse.ChapterChain, LabCourse.ChapterChimney, LabCourse.ChapterChase,
        };

        /// <summary>The attract driver plays one segment: the autopilot running the course.</summary>
        public static readonly string[] Segments = { "course" };

        /// <summary>About 70 s: the whole Gauntlet from the start pad (the autopilot's run is about 45 s), then the old course's momentum chain.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(GauntletCourse.ChapterStart, 1f).Drive("course", 50f)
            .Jump(LabCourse.ChapterChain, 1f).Drive("course", 12f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        /// <summary>Live jumps while the lab runs (<paramref name="game"/> may be null in edit-mode tests).</summary>
        public static void Register(ChapterRegistry chapters, MovementLabGame game, object owner)
        {
            foreach (var name in Chapters)
            {
                string chapter = name;
                chapters.Register(Scene, chapter, () => game.JumpToChapter(chapter), owner);
            }
        }
    }
}
