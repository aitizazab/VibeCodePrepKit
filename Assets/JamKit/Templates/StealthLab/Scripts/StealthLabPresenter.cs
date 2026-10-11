using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.StealthLab
{
    /// <summary>The Stealth Lab's presenter-mode chapters (docs/PRESENTING.md): hide and seek, and patrol. Each restarts
    /// the yard in that round.</summary>
    public static class StealthLabPresenter
    {
        public const string Scene = "StealthLab";
        public const string ChapterHideAndSeek = "Hide and seek";
        public const string ChapterPatrol = "Patrol";

        public static readonly string[] Chapters = { ChapterHideAndSeek, ChapterPatrol };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare() => PresenterMode.Declare(Scene, Chapters);

        public static void Register(ChapterRegistry chapters, StealthLabGame game, object owner)
        {
            chapters.Register(Scene, ChapterHideAndSeek, () => game.Begin(StealthLabGame.Round.HideAndSeek), owner);
            chapters.Register(Scene, ChapterPatrol, () => game.Begin(StealthLabGame.Round.Patrol), owner);
        }
    }
}
