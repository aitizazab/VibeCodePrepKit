using JamKit.DeepBlock;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.DreamEscape
{
    /// <summary>
    /// Night 1 on Larch Hill in presenter mode (docs/PRESENTING.md): the creek (the night from the start), Walt (in the
    /// shop), the back way, Back Lane B, the old oak, the Rewind and the house (docs/redesign/world.md).
    /// </summary>
    public static class DreamStreetPresenter
    {
        public const string Scene = "DreamStreet";

        public static readonly string[] Chapters =
        {
            LarchHill.ChapterArrival, LarchHill.ChapterWalt, LarchHill.ChapterBackWay, LarchHill.ChapterLane, LarchHill.ChapterOak,
            LarchHill.ChapterRewind, LarchHill.ChapterHouse,
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
