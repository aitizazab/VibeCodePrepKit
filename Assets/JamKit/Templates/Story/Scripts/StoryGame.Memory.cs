using System;
using Ink.Runtime;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Narrative
{
    public sealed partial class StoryGame
    {
        // ---------- ink functions (declared with EXTERNAL in StoryKit.ink, with fallbacks for Inky) ----------

        void OnStoryCreated(Story story)
        {
            BindMemoryFunctions(story, () => StoryStorage.Memory);
            ObserveInventory(story);
        }

        /// <summary>
        /// The cross-playthrough functions ink can call. Shared with tests and tools that run a story
        /// outside the scene.
        /// <code>
        /// playthrough_count()   1 on the first playthrough, 2 on the second...
        /// endings_seen()        how many different endings were ever reached
        /// seen_ending("id")     that ending was reached before (in any playthrough)
        /// last_ending()         id of the last ending reached ("" if none)
        /// remember("key", v)    keep a value forever (text, number, true/false)
        /// recall("key")         get it back (0 if never remembered); knows("key") checks
        /// visited_ever("knot")  that knot was ever seen, in any playthrough
        /// times_quit()          how often the player left mid-story (quit or closed the game)
        /// loads() / rewinds()   saves loaded / loads that went back in time, this playthrough
        /// was_reloaded()        loads() > 0
        /// </code>
        /// </summary>
        public static void BindMemoryFunctions(Story story, Func<StoryMemory> memory)
        {
            story.BindExternalFunction("playthrough_count", () => (object)Math.Max(1, memory().playthroughs), true);
            story.BindExternalFunction("endings_seen", () => (object)memory().DistinctEndings, true);
            story.BindExternalFunction("seen_ending", (string id) => (object)memory().SeenEnding(id), true);
            story.BindExternalFunction("last_ending", () => (object)(memory().lastEnding ?? ""), true);
            story.BindExternalFunction("remember", (string key, object value) =>
            {
                memory().Remember(key, value);
                StoryStorage.SaveMemory();
            });
            story.BindExternalFunction("recall", (string key) => memory().Recall(key) ?? (object)0, true);
            story.BindExternalFunction("knows", (string key) => (object)memory().Knows(key), true);
            story.BindExternalFunction("visited_ever", (string knot) => (object)memory().VisitedEver(knot), true);
            story.BindExternalFunction("times_quit", () => (object)memory().timesQuit, true);
            story.BindExternalFunction("loads", () => (object)memory().loadsThisRun, true);
            story.BindExternalFunction("rewinds", () => (object)memory().rewindsThisRun, true);
            story.BindExternalFunction("was_reloaded", () => (object)(memory().loadsThisRun > 0), true);
        }

        // ---------- Facts for conditions (hotspot Show If) ----------

        public bool TryGet(string name, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(name)) return false;
            var story = GameRoot.Dialogue.Story;
            int colon = name.IndexOf(':');
            string prefix = colon > 0 ? name.Substring(0, colon).ToLowerInvariant() : "";
            string rest = colon > 0 ? name.Substring(colon + 1) : name;
            switch (prefix)
            {
                case "item": value = HasItem(rest); return true;
                case "seen":
                    if (story == null) return false;
                    try { value = story.state.VisitCountAtPathString(rest) > 0; }
                    catch (Exception) { value = false; }
                    return true;
                case "var":
                    return TryInkVariable(story, rest, out value);
                case "":
                    if (TryInkVariable(story, rest, out value)) return true;
                    if (config.FindItem(rest) != null) { value = HasItem(rest); return true; }
                    if (TryMemoryFact(StoryStorage.Memory, rest, out value)) return true;
                    value = GameRoot.Save.Data.HasFlag(rest);
                    return true;
                default:
                    return TryMemoryFact(StoryStorage.Memory, name, out value);
            }
        }

        static bool TryInkVariable(Story story, string name, out object value)
        {
            value = null;
            if (story == null || !story.variablesState.GlobalVariableExistsWithName(name)) return false;
            value = story.variablesState[name];
            if (value is InkList list) value = list.Count > 0;
            return true;
        }

        /// <summary>The facts conditions can read without a story running (title variants): flag:,
        /// ending:, memory:, and playthrough, endings_seen, last_ending, times_quit, loads, rewinds, finished.</summary>
        public static bool TryMemoryFact(StoryMemory m, string name, out object value)
        {
            value = null;
            int colon = name.IndexOf(':');
            string prefix = colon > 0 ? name.Substring(0, colon).ToLowerInvariant() : "";
            string rest = colon > 0 ? name.Substring(colon + 1) : name;
            switch (prefix)
            {
                case "flag": value = GameRoot.Exists && GameRoot.Save.Data.HasFlag(rest); return true;
                case "ending": value = m.SeenEnding(rest); return true;
                case "memory": value = m.Recall(rest); return true;
                case "":
                    switch (rest)
                    {
                        case "playthrough": value = m.playthroughs; return true;
                        case "endings_seen": value = m.DistinctEndings; return true;
                        case "last_ending": value = m.lastEnding ?? ""; return true;
                        case "times_quit": value = m.timesQuit; return true;
                        case "loads": value = m.loadsThisRun; return true;
                        case "rewinds": value = m.rewindsThisRun; return true;
                        case "finished": value = m.finished; return true;
                    }
                    return false;
            }
            return false;
        }

        /// <summary>Conditions on the title screen (no story running).</summary>
        public sealed class MemoryFacts : IStoryFacts
        {
            readonly StoryMemory _m;
            public MemoryFacts(StoryMemory m) { _m = m; }
            public bool TryGet(string name, out object value)
            {
                if (TryMemoryFact(_m, name, out value)) return true;
                value = GameRoot.Exists && GameRoot.Save.Data.HasFlag(name);
                return true;
            }
        }

        // ---------- Fourth-wall tricks (#meta) ----------

        void Meta(StoryCommand cmd)
        {
            var m = Memory;
            switch (cmd.id)
            {
                case "title": m.titleOverride = cmd.text; break;
                case "tagline": m.taglineOverride = cmd.text; break;
                case "playlabel": m.playLabelOverride = cmd.text; break;
                case "reset":
                    m.titleOverride = "";
                    m.taglineOverride = "";
                    m.playLabelOverride = "";
                    break;
                case "fakecrash":
                    if (!GameRoot.Dialogue.SkipMode) _stage.FakeFreeze(GameRoot.Config.gameTitle, cmd.SecondsOr(2.5f));
                    break;
                case "popup":
                    if (!GameRoot.Dialogue.SkipMode) _stage.Popup(GameRoot.Config.gameTitle, cmd.text);
                    break;
                case "erase":
                    StoryStorage.DeleteAllSlots();
                    _last = null;
                    break;
            }
            StoryStorage.SaveMemory();
        }
    }
}
