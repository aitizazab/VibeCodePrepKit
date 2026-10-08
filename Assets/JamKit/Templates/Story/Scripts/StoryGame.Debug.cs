using System;
using System.Collections;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    public sealed partial class StoryGame
    {
        readonly List<Button> _debugButtons = new List<Button>();
        Func<bool> _skipHandler;
        Func<IEnumerator> _smokeDriver;

        void SetupDebug()
        {
            _skipHandler = DebugSkip;
            DebugMenu.SkipHandler = _skipHandler;
            _smokeDriver = SmokeDrive;
            SmokeRunner.GameplayDriver = _smokeDriver;
            AddDebugButton("Story: skip to the next choice", () => GameRoot.Dialogue.SkipToChoice());
            AddDebugButton("Story: jump to knot...", OpenKnotList);
            AddDebugButton("Story: give all items", GiveAllItems);
            AddDebugButton("Story: show all hotspots", () => _hotspots.ShowAll = !_hotspots.ShowAll);
            AddDebugButton("Story: unlock all endings", UnlockAllEndings);
            AddDebugButton("Story: reset memory (all playthroughs)", ResetMemory);
#if UNITY_EDITOR
            gameObject.AddComponent<StoryInkReloader>().Init(this);
#endif
            _smokeLog = SmokeRunner.Requested(out _);
            if (_smokeLog)
            {
                GameRoot.Dialogue.ChoiceMade += SmokeChoice;
                SmokeLog("story: playthrough " + Memory.playthroughs + ", starting at knot \"" + config.startKnot + "\"");
            }
        }

        void TeardownDebug()
        {
            if (_smokeLog) GameRoot.Dialogue.ChoiceMade -= SmokeChoice;
            if (DebugMenu.SkipHandler == _skipHandler) DebugMenu.SkipHandler = null;
            if (SmokeRunner.GameplayDriver == _smokeDriver) SmokeRunner.GameplayDriver = null;
            foreach (var b in _debugButtons) b.RemoveFromHierarchy();
            _debugButtons.Clear();
        }

        void AddDebugButton(string text, Action action)
        {
            var panel = GameRoot.Ui != null && GameRoot.Ui.DebugPanel != null ? GameRoot.Ui.DebugPanel.Root.Q<VisualElement>("debug-panel") : null;
            if (panel == null) return;
            var b = new Button(action) { text = text };
            b.AddToClassList("jk-debug-button");
            var fps = panel.Q<Button>("debug-fps");
            int at = fps != null ? panel.IndexOf(fps) : panel.childCount;
            panel.Insert(Mathf.Max(0, at), b);
            _debugButtons.Add(b);
        }

        /// <summary>Debug "Next scene": mash on until the knot changes, a choice comes up, a hotspot scene opens or the story ends.</summary>
        public bool DebugSkip()
        {
            var d = GameRoot.Dialogue;
            if (_endingShown) { FinishEndingCard(); return true; }
            if (d.IsParked && _investigation != null)
            {
                // Out of the hotspot scene: run its walkthrough.
                StartCoroutine(Walkthrough(_investigation, 200));
                return true;
            }
            if (!d.IsActive) return false;
            string start = _knot;
            for (int i = 0; i < 3000 && d.IsActive; i++)
            {
                if (d.ChoiceCount > 0)
                {
                    if (i == 0) { d.Choose(0); continue; }
                    break;
                }
                d.AutoStep();
                if (_knot != start) break;
            }
            return true;
        }

        public List<string> KnotNames()
        {
            var list = new List<string>();
            if (config.knotIndex != null && config.knotIndex.Count > 0) { list.AddRange(config.knotIndex); return list; }
            var story = GameRoot.Dialogue.Story;
            if (story == null) return list;
            foreach (var name in story.mainContentContainer.namedContent.Keys) list.Add(name);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        void OpenKnotList() => StoryScreens.OpenKnots(KnotNames(), k => JumpToKnot(k));

        /// <summary>Debug: go to a knot (keeps the stage as it is; the knot's own tags set it).</summary>
        public bool JumpToKnot(string knot)
        {
            var d = GameRoot.Dialogue;
            if (_ended) return false;
            if (GameRoot.Flow.Current == GameState.Paused) GameRoot.Instance.Resume();
            _hotspots.Hide();
            _inventory.SetVisible(false);
            _investigation = null;
            return d.JumpTo(knot);
        }

        public void UnlockAllEndings()
        {
            EndingsRegistry.UnlockAll(config.EndingInfos(), Memory);
            StoryStorage.SaveMemory();
            GameRoot.Hud.Message("All endings unlocked (debug).", 2f);
        }

        public void ResetMemory()
        {
            StoryStorage.ResetMemory();
            Memory.BeginPlaythrough();
            StoryStorage.SaveMemory();
            GameRoot.Hud.Message("Memory reset: the game has forgotten every playthrough.", 2.5f);
        }

        // ---------- Smoke run and tests ----------

        /// <summary>Plays on by itself to an ending: first choices, and hotspot scenes by their walkthrough.
        /// Stops when the ending card shows (it then closes it, which goes to Victory).</summary>
        public IEnumerator PlayToEnding(int maxSteps = 6000, bool closeCard = true)
        {
            var d = GameRoot.Dialogue;
            for (int i = 0; i < maxSteps && !_ended; i++)
            {
                if (GameRoot.Flow.Current == GameState.Paused) GameRoot.Instance.Resume();
                if (d.IsActive) d.AutoStep();
                else if (d.IsParked && _investigation != null) yield return Walkthrough(_investigation, 100);
                else if (!d.IsActive && !d.IsParked) break;
                if (i % 20 == 0) yield return null;
            }
            if (closeCard && _endingShown)
            {
                yield return null;
                FinishEndingCard();
            }
        }

        /// <summary>Follows a hotspot scene's walkthrough (each step runs its knot to the end), until the scene closes.</summary>
        public IEnumerator Walkthrough(HotspotScene scene, int maxLinesPerStep)
        {
            var d = GameRoot.Dialogue;
            foreach (var step in scene.walkthrough)
            {
                if (_investigation != scene) yield break;
                string spot = step, item = null;
                int gt = step.IndexOf('>');
                if (gt >= 0) { item = step.Substring(0, gt).Trim(); spot = step.Substring(gt + 1).Trim(); }
                if (!d.IsParked) yield break;
                UseHotspot(spot, item);
                for (int i = 0; i < maxLinesPerStep && d.IsActive; i++)
                {
                    d.AutoStep();
                    if (i % 10 == 0) yield return null;
                }
                yield return null;
            }
        }

        IEnumerator SmokeDrive()
        {
            SmokeLog("story driver: playing on to an ending (first choices, hotspot rooms by their walkthrough)");
            yield return PlayToEnding();
            float end = Time.realtimeSinceStartup + 5f;
            while (GameRoot.Flow.Current != GameState.Victory && Time.realtimeSinceStartup < end) yield return null;
            SmokeLog("story summary: " + _smokeChoices + " choice(s), " + _smokeHotspots + " hotspot(s) used in "
                + (_smokeRooms.Count > 0 ? "room(s) " + string.Join(", ", _smokeRooms) : "no hotspot room")
                + ", ending " + (_smokeEnding.Length > 0 ? "\"" + _smokeEnding + "\"" : "NONE") + ", state " + GameRoot.Flow.Current);
            // The smoke run must prove the story was played, not only that the debug win works.
            if (_smokeEnding.Length == 0) Debug.LogError("StoryGame smoke: the story didn't reach an ending (stopped at knot \"" + _knot + "\").");
            else if (GameRoot.Flow.Current != GameState.Victory) Debug.LogError("StoryGame smoke: the ending didn't lead to the Victory screen.");
        }

        // ---------- Smoke log (only in a -jamSmoke run): what the run actually did ----------

        bool _smokeLog;
        int _smokeChoices, _smokeHotspots;
        readonly List<string> _smokeRooms = new List<string>();
        string _smokeEnding = "";

        static void SmokeLog(string what) => Debug.Log("JAMKIT_SMOKE " + what);

        void SmokeChoice(int index, string text)
        {
            _smokeChoices++;
            SmokeLog("story choice " + _smokeChoices + " (in \"" + _knot + "\"): #" + (index + 1) + " \"" + JamKit.Dialogue.RichLine.StripTags(text ?? "") + "\"");
        }

        void SmokeNoteRoom(string id)
        {
            if (!_smokeLog) return;
            if (!_smokeRooms.Contains(id)) _smokeRooms.Add(id);
            SmokeLog("story hotspot room \"" + id + "\" opened");
        }

        void SmokeNoteHotspot(string spot, string item)
        {
            if (!_smokeLog) return;
            _smokeHotspots++;
            SmokeLog("story hotspot \"" + spot + "\"" + (string.IsNullOrEmpty(item) ? "" : " with item \"" + item + "\""));
        }

        void SmokeNoteEnding(string id)
        {
            if (!_smokeLog) return;
            _smokeEnding = string.IsNullOrEmpty(id) ? "the_end" : id;
            SmokeLog("story ending reached: \"" + _smokeEnding + "\" (playthrough " + Memory.playthroughs + ")");
        }
    }
}
