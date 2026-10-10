using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// The dream escape's presenter-mode chapters (docs/PRESENTING.md): Escape start (Walt's driveway, the static
    /// waking), Rooftops and Final run, each a checkpoint of the route; and an attract script in which the course
    /// autopilot runs the escape from the driveway to the light, then the final run again.
    /// </summary>
    public static class DreamEscapePresenter
    {
        public const string Scene = "DreamEscape";

        public static readonly string[] Chapters =
        {
            DreamEscapeCourse.ChapterStart, DreamEscapeCourse.ChapterRooftops, DreamEscapeCourse.ChapterFinal,
        };

        /// <summary>The attract driver plays one segment: the autopilot running the escape.</summary>
        public static readonly string[] Segments = { "escape" };

        /// <summary>About 70 s: the whole escape from Walt's driveway, then the final run once more.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(DreamEscapeCourse.ChapterStart, 1f).Drive("escape", 50f)
            .Jump(DreamEscapeCourse.ChapterFinal, 1f).Drive("escape", 12f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        /// <summary>Live jumps while the escape runs (<paramref name="game"/> may be null in edit-mode tests).</summary>
        public static void Register(ChapterRegistry chapters, DreamEscapeGame game, object owner)
        {
            foreach (var name in Chapters)
            {
                string chapter = name;
                chapters.Register(Scene, chapter, () => game.JumpToChapter(chapter), owner);
            }
        }
    }
}
