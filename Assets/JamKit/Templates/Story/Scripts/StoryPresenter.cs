using System.Collections.Generic;
using JamKit.Presenting;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Narrative
{
    /// <summary>
    /// The story template's presenter-mode chapters and attract script (docs/PRESENTING.md): the
    /// sample's key knots, the hotspot room and each ending. A chapter is a knot to start from, with
    /// the music and ambience to have on (the knot's own tags still change them). Edit the list for
    /// your story; the story check's tests make sure every knot here exists.
    /// </summary>
    public static class StoryPresenter
    {
        public const string Scene = "Story";

        public struct Chapter
        {
            public string name, knot, music, ambience;

            public Chapter(string name, string knot, string music = "", string ambience = "")
            {
                this.name = name;
                this.knot = knot;
                this.music = music;
                this.ambience = ambience;
            }
        }

        public static readonly Chapter[] List =
        {
            new Chapter("Prologue", "prologue"),
            new Chapter("The platform", "platform", "music_story_calm", "amb_rain"),
            new Chapter("The waiting room", "waiting_room", "music_story_calm", "amb_rain"),
            new Chapter("Lost property office (hotspots)", "office", "music_story_calm"),
            new Chapter("The train arrives", "train_arrives", "music_story_calm", "amb_rain"),
            new Chapter("Ending: Last Train", "ending_last_train"),
            new Chapter("Ending: Missed Connection", "ending_missed_connection"),
            new Chapter("Ending: Lost Property", "ending_lost_property"),
            new Chapter("Ending: Return Ticket", "ending_return_ticket"),
        };

        public static readonly string[] Chapters = Names();

        /// <summary>Attract-mode segments StoryGame.AttractDrive plays.</summary>
        public static readonly string[] Segments = { "read", "hotspots" };

        /// <summary>About 75 s: the opening page, the platform, the office clicked through, an ending.</summary>
        public static AttractScript Attract() => new AttractScript(Scene)
            .Jump("Prologue", 0.5f).Drive("read", 14f)
            .Jump("The platform", 0.5f).Drive("read", 18f)
            .Jump("Lost property office (hotspots)", 0.5f).Drive("hotspots", 24f)
            .Jump("Ending: Missed Connection", 0.5f).Drive("read", 18f);

        static string[] Names()
        {
            var names = new string[List.Length];
            for (int i = 0; i < List.Length; i++) names[i] = List[i].name;
            return names;
        }

        /// <summary>Every knot a chapter starts at (tests check they exist in the story).</summary>
        public static List<string> Knots()
        {
            var knots = new List<string>();
            foreach (var c in List) knots.Add(c.knot);
            return knots;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Declare()
        {
            PresenterMode.Declare(Scene, Chapters);
            PresenterMode.SetAttractScript(Attract());
        }

        public static void Register(ChapterRegistry chapters, StoryGame game, object owner)
        {
            foreach (var c in List)
            {
                var chapter = c;
                chapters.Register(Scene, chapter.name, () => game.PresenterKnot(chapter.knot, chapter.music, chapter.ambience), owner);
            }
        }
    }
}
