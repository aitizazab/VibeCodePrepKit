using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 2's house in presenter mode (docs/PRESENTING.md): the front garden, hide-and-seek (just begun), the
    /// argument (its debug choice), the house tipping (the escape from its start) and the front door (the umbrella and
    /// the rain); and an attract script in which the course autopilot runs the way in, then the escape out into the rain.
    /// </summary>
    public static class DreamHousePresenter
    {
        public const string Scene = "DreamHouse";

        public static readonly string[] Chapters =
        {
            DreamHouse.ChapterArrival, DreamHouse.ChapterHideAndSeek, DreamHouse.ChapterArgument, DreamHouse.ChapterTips, DreamHouse.ChapterFrontDoor,
        };

        /// <summary>The attract driver plays one segment: the autopilot running.</summary>
        public static readonly string[] Segments = { "house" };

        /// <summary>About 50 s: the way in from the front garden, then the house tipping and out into the rain.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(DreamHouse.ChapterArrival, 1f).Drive("house", 14f)
            .Jump(DreamHouse.ChapterTips, 1f).Drive("house", 26f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        /// <summary>Live jumps while the house runs (<paramref name="game"/> may be null in edit-mode tests).</summary>
        public static void Register(ChapterRegistry chapters, DreamHouseGame game, object owner)
        {
            foreach (var name in Chapters)
            {
                string chapter = name;
                chapters.Register(Scene, chapter, () => game.JumpToChapter(chapter), owner);
            }
        }
    }
}
