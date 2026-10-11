using JamKit.Presenting;
using JamKit.Shell;
using JamKit.Stealth;
using UnityEngine;

namespace JamKit.StealthLab
{
    /// <summary>The Stealth Lab's presenter-mode chapters (docs/PRESENTING.md): hide and seek, patrol, and a patrol with
    /// each answer to fast movement (docs/STEALTH.md "When the movement outruns the guards"), one where a catch fails the
    /// subquest, and one with the recommended set (A + C2, three guards, a catch restarts at the checkpoint).</summary>
    public static class StealthLabPresenter
    {
        public const string Scene = "StealthLab";
        public const string ChapterHideAndSeek = "Hide and seek";
        public const string ChapterPatrol = "Patrol";
        public const string ChapterLoud = "A: loud movement";
        public const string ChapterPushBack = "B: the dream pushes back";
        public const string ChapterDaytime = "C1: daytime section";
        public const string ChapterLucidity = "C2: lucidity fades when watched";
        public const string ChapterFailSubquest = "Caught: the subquest fails";
        public const string ChapterRecommended = "Recommended: A + C2, three guards, checkpoint restart";

        public static readonly string[] Chapters =
        {
            ChapterHideAndSeek, ChapterPatrol, ChapterLoud, ChapterPushBack, ChapterDaytime, ChapterLucidity, ChapterFailSubquest, ChapterRecommended,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare() => PresenterMode.Declare(Scene, Chapters);

        public static void Register(ChapterRegistry chapters, StealthLabGame game, object owner)
        {
            chapters.Register(Scene, ChapterHideAndSeek, () => game.Begin(StealthLabGame.Round.HideAndSeek), owner);
            chapters.Register(Scene, ChapterPatrol, () => game.Try(false, false, false, false, false), owner);
            chapters.Register(Scene, ChapterLoud, () => game.Try(true, false, false, false, false), owner);
            chapters.Register(Scene, ChapterPushBack, () => game.Try(false, true, false, false, true), owner);
            chapters.Register(Scene, ChapterDaytime, () => game.Try(false, false, true, false, false), owner);
            chapters.Register(Scene, ChapterLucidity, () => game.Try(false, false, false, true, false), owner);
            chapters.Register(Scene, ChapterFailSubquest, () => game.Try(false, false, false, false, true, CaughtOutcome.FailSubquest), owner);
            chapters.Register(Scene, ChapterRecommended, () => game.Try(true, false, false, true, true, CaughtOutcome.RestartCheckpoint), owner);
        }
    }
}
