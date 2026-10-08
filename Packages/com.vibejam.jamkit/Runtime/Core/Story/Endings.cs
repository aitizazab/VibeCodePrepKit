using System;
using System.Collections.Generic;

namespace JamKit.Narrative
{
    /// <summary>An ending as the gallery knows it (the Unity side adds a picture).</summary>
    [Serializable]
    public class EndingInfo
    {
        public string id = "";
        public string title = "";
        public string description = "";
        /// <summary>Shown on the locked card ("Someone is waiting for a train..."). Empty = "???".</summary>
        public string hint = "";
        /// <summary>A secret ending: its card says only "Secret ending" until found.</summary>
        public bool secret;
    }

    /// <summary>One card of the endings gallery.</summary>
    public struct GalleryCard
    {
        public string id;
        /// <summary>1-based number in the list ("Ending 3").</summary>
        public int number;
        public bool found;
        public bool secret;
        public int times;
        public string title;
        public string description;
    }

    /// <summary>
    /// The endings registry: the game's endings (from data) plus what <see cref="StoryMemory"/> saw,
    /// turned into gallery cards and counts. Found endings show title and text; unfound ones show
    /// their hint (or ???); unfound secret ones show only that a secret exists.
    /// </summary>
    public static class EndingsRegistry
    {
        public const string LockedTitle = "???";
        public const string SecretTitle = "Secret ending";

        public static List<GalleryCard> Cards(IList<EndingInfo> endings, StoryMemory memory)
        {
            var cards = new List<GalleryCard>();
            if (endings == null) return cards;
            for (int i = 0; i < endings.Count; i++)
            {
                var e = endings[i];
                if (e == null) continue;
                var rec = memory != null ? memory.FindEnding(e.id) : null;
                bool found = rec != null;
                cards.Add(new GalleryCard
                {
                    id = e.id,
                    number = cards.Count + 1,
                    found = found,
                    secret = e.secret,
                    times = found ? rec.times : 0,
                    title = found ? e.title : e.secret ? SecretTitle : LockedTitle,
                    description = found ? e.description : e.secret ? "Not every ending shows itself the first time." : (string.IsNullOrEmpty(e.hint) ? "" : e.hint),
                });
            }
            return cards;
        }

        public static int Found(IList<EndingInfo> endings, StoryMemory memory)
        {
            if (endings == null || memory == null) return 0;
            int n = 0;
            foreach (var e in endings)
                if (e != null && memory.SeenEnding(e.id)) n++;
            return n;
        }

        public static int Total(IList<EndingInfo> endings)
        {
            if (endings == null) return 0;
            int n = 0;
            foreach (var e in endings) if (e != null) n++;
            return n;
        }

        public static EndingInfo Find(IList<EndingInfo> endings, string id)
        {
            if (endings == null || string.IsNullOrEmpty(id)) return null;
            foreach (var e in endings)
                if (e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        /// <summary>1-based place of an ending in the list (0 if unknown).</summary>
        public static int Number(IList<EndingInfo> endings, string id)
        {
            if (endings == null) return 0;
            int n = 0;
            foreach (var e in endings)
            {
                if (e == null) continue;
                n++;
                if (string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return n;
            }
            return 0;
        }

        /// <summary>Marks every ending as seen (debug "Unlock all endings").</summary>
        public static void UnlockAll(IList<EndingInfo> endings, StoryMemory memory)
        {
            if (endings == null || memory == null) return;
            foreach (var e in endings)
            {
                if (e == null || memory.SeenEnding(e.id)) continue;
                memory.endings.Add(new EndingRecord { id = e.id, times = 1, firstPlaythrough = memory.playthroughs });
            }
        }
    }
}
