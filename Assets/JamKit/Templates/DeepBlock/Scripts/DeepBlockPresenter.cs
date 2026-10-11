using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;
using Block = JamKit.DeepBlock.DeepBlock;

namespace JamKit.DeepBlockLab
{
    /// <summary>The Deep Block lab's presenter-mode chapters (docs/PRESENTING.md): the four routes to the hill gate, hide
    /// and seek in the gardens, and free roam.</summary>
    public static class DeepBlockPresenter
    {
        public const string Scene = "DeepBlock";
        public const string ChapterFreeRoam = "Free roam";

        public static readonly string[] Chapters =
        {
            Block.ChapterStreet, Block.ChapterBack, Block.ChapterRoof, Block.ChapterUnder, Block.ChapterHideAndSeek, ChapterFreeRoam,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare() => PresenterMode.Declare(Scene, Chapters);

        public static void Register(ChapterRegistry chapters, DeepBlockGame game, object owner)
        {
            for (int i = 0; i < 4; i++)
            {
                int route = i;
                chapters.Register(Scene, Chapters[i], () => game.StartRoute(route), owner);
            }
            chapters.Register(Scene, Block.ChapterHideAndSeek, () => game.StartHideAndSeek(), owner);
            chapters.Register(Scene, ChapterFreeRoam, game.FreeRoam, owner);
        }
    }
}
