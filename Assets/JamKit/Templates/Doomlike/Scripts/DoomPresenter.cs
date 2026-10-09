using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Doomlike
{
    /// <summary>
    /// The DOOM-like's presenter-mode chapters and attract script (docs/PRESENTING.md): each level,
    /// the boss, the end tally. Declared at load, registered live by DoomGame while its scene runs.
    /// </summary>
    public static class DoomPresenter
    {
        public const string Scene = "Doomlike";

        public const string Level1 = "Level 1";
        public const string Level2 = "Level 2";
        public const string Level3 = "Level 3";
        public const string Boss = "The boss";
        public const string Tally = "End tally";

        public static readonly string[] Chapters = { Level1, Level2, Level3, Boss, Tally };

        /// <summary>Attract-mode segments DoomGame.AttractDrive plays.</summary>
        public static readonly string[] Segments = { "explore", "fight" };

        /// <summary>About 70 s: walking level 1 and shooting what turns up, level 2, the boss, the tally.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(Level1, 2f).Drive("explore", 20f)
            .Jump(Level2, 2f).Drive("explore", 16f)
            .Jump(Boss, 1f).Drive("fight", 20f)
            .Jump(Tally, 0.5f).Wait(5f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        public static void Register(ChapterRegistry chapters, DoomGame game, object owner)
        {
            chapters.Register(Scene, Level1, () => game.PresenterLevel(0), owner);
            chapters.Register(Scene, Level2, () => game.PresenterLevel(1), owner);
            chapters.Register(Scene, Level3, () => game.PresenterLevel(2), owner);
            chapters.Register(Scene, Boss, () => game.PresenterBoss(), owner);
            chapters.Register(Scene, Tally, () => game.PresenterTally(), owner);
        }
    }
}
