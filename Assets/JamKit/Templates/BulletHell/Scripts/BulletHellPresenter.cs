using JamKit.Presenting;
using JamKit.Roguelite;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.BulletHell
{
    /// <summary>
    /// The bullet hell's presenter-mode chapters and attract script (docs/PRESENTING.md). The names
    /// are declared at load so the panel lists them on the title screen; the game registers the
    /// jumps while its scene runs. Edit the lists to fit your game: tests check every attract jump
    /// names a chapter here and every drive names a segment the game plays.
    /// </summary>
    public static class BulletHellPresenter
    {
        public const string Scene = "BulletHell";

        public const string Floor1 = "Floor 1";
        public const string Draft = "Upgrade draft";
        public const string Encounter = "Encounter room (Forge)";
        public const string Boss1 = "Floor 1 boss";
        public const string Floor2 = "Floor 2";
        public const string Boss2 = "Floor 2 boss";
        public const string Floor3 = "Floor 3";
        public const string Boss3 = "Floor 3 boss";

        /// <summary>In the order the panel lists them (and "Next chapter" walks them).</summary>
        public static readonly string[] Chapters = { Floor1, Draft, Encounter, Boss1, Floor2, Boss2, Floor3, Boss3 };

        /// <summary>Attract-mode segments the game can play (BulletHellGame.AttractDrive).</summary>
        public static readonly string[] Segments = { "fight", "draft", "encounter" };

        /// <summary>About 75 s of the best of a run, looped: a fight, a draft, the Forge, a boss, floor 3.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump(Floor1, 2.5f).Drive("fight", 16f)
            .Jump(Draft, 0.5f).Drive("draft", 5f)
            .Jump(Encounter, 0.5f).Drive("encounter", 6f)
            .Jump(Boss2, 3f).Drive("fight", 22f)
            .Jump(Floor3, 2.5f).Drive("fight", 14f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        /// <summary>The live jumps (<paramref name="game"/> may be null in edit-mode tests: nothing runs then).</summary>
        public static void Register(ChapterRegistry chapters, BulletHellGame game, object owner)
        {
            chapters.Register(Scene, Floor1, () => game.PresenterFloor(0, false), owner);
            chapters.Register(Scene, Draft, () => game.PresenterRoom(RoomType.Treasure), owner);
            chapters.Register(Scene, Encounter, () => game.PresenterRoom(RoomType.Forge), owner);
            chapters.Register(Scene, Boss1, () => game.PresenterFloor(0, true), owner);
            chapters.Register(Scene, Floor2, () => game.PresenterFloor(1, false), owner);
            chapters.Register(Scene, Boss2, () => game.PresenterFloor(1, true), owner);
            chapters.Register(Scene, Floor3, () => game.PresenterFloor(2, false), owner);
            chapters.Register(Scene, Boss3, () => game.PresenterFloor(2, true), owner);
        }
    }
}
