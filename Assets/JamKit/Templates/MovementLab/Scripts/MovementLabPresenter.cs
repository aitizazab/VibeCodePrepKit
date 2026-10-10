using JamKit.Movement;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.MovementLab
{
    /// <summary>
    /// The Movement Lab's presenter-mode chapters (docs/PRESENTING.md): the course start, the wallrun
    /// section, the momentum chain, the chimney and the chase, each a checkpoint of the course; and
    /// an attract script in which the course autopilot runs the course, then the chase.
    /// </summary>
    public static class MovementLabPresenter
    {
        public const string Scene = "MovementLab";

        public static readonly string[] Chapters =
        {
            LabCourse.ChapterStart, LabCourse.ChapterWallruns, LabCourse.ChapterChain, LabCourse.ChapterChimney, LabCourse.ChapterChase,
        };

        /// <summary>The attract driver plays one segment: the autopilot running the course.</summary>
        public static readonly string[] Segments = { "course" };

        /// <summary>About 75 s: the course from the start (run-up, slide, gaps, wallruns, the chain), then the chase.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(LabCourse.ChapterStart, 1f).Drive("course", 34f)
            .Jump(LabCourse.ChapterChimney, 1f).Drive("course", 12f)
            .Jump(LabCourse.ChapterChase, 1f).Drive("course", 22f);

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
