using JamKit.DeepBlock;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 1's street in presenter mode (docs/PRESENTING.md): the creek (the night from the start), Walt (in the yard),
    /// each of the four ways up (from its start, Walt already aside) and the top (by the lookout).
    /// </summary>
    public static class DreamStreetPresenter
    {
        public const string Scene = "DreamStreet";

        public static readonly string[] Chapters =
        {
            LarchHill.ChapterArrival, LarchHill.ChapterWalt, LarchHill.ChapterRoad, LarchHill.ChapterStairs, LarchHill.ChapterRoofs,
            LarchHill.ChapterRavine, LarchHill.ChapterTop,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare() => PresenterMode.Declare(Scene, Chapters);

        /// <summary>Live jumps while the street runs (<paramref name="game"/> may be null in edit-mode tests).</summary>
        public static void Register(ChapterRegistry chapters, DreamStreetGame game, object owner)
        {
            foreach (var name in Chapters)
            {
                string chapter = name;
                chapters.Register(Scene, chapter, () => game.JumpToChapter(chapter), owner);
            }
        }
    }
}
