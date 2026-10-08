using System;
using System.Collections.Generic;
using JamKit.Flow;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    public sealed partial class StoryGame
    {
        StorySnapshot _last;

        /// <summary>The latest snapshot (taken at every line, choice and hotspot scene).</summary>
        public StorySnapshot LastSnapshot => _last;

        /// <summary>Everything a save needs, right now. Null when there's no story running.</summary>
        public StorySnapshot Capture()
        {
            var d = GameRoot.Dialogue;
            if (d.Story == null || _ended) return _last;
            var line = d.Line;
            var s = new StorySnapshot
            {
                label = _chapter.Length > 0 ? _chapter : Pretty(_knot),
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                playSeconds = _playSeconds,
                playthrough = Memory.playthroughs,
                turn = d.TurnIndex,
                knot = _knot,
                chapter = _chapter,
                inkState = d.SaveState(),
                stage = _state.Clone(),
                backlog = _backlog.ToList(),
                lineSpeaker = line.speakerId ?? "",
                lineText = line.text ?? "",
                lineKind = line.kind == DialogueLineKind.Thought ? LineKind.Thought : line.kind == DialogueLineKind.Dialogue ? LineKind.Dialogue : LineKind.Narration,
                investigation = _investigation != null ? _investigation.id : "",
                parked = d.IsParked,
                pendingEnding = _pendingEnding,
                used = new List<string>(_usedHotspots),
            };
            foreach (var e in _text.NvlPage) s.nvlPage.Add(e.Clone());
            _last = s;
            return s;
        }

        static string Pretty(string knot)
        {
            if (string.IsNullOrEmpty(knot)) return "Start";
            string s = knot.Replace('_', ' ');
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        void Autosave()
        {
            _linesSinceAutosave = 0;
            if (_last == null || _ended) return;
            StoryStorage.WriteSlot(StorySlots.Auto, _last);
            StoryStorage.SaveMemory();
        }

        /// <summary>Saves the story as it is now into a slot ("slot1".."slot3", "quick"). Returns false when there's nothing to save.</summary>
        public bool SaveTo(string slot)
        {
            if (_ended) { GameRoot.Hud.Message("The story is over: nothing to save.", 2f); return false; }
            var s = Capture();
            if (s == null) { GameRoot.Hud.Message("Nothing to save yet.", 2f); return false; }
            StoryStorage.WriteSlot(slot, s);
            StoryStorage.SaveMemory();
            GameRoot.Hud.Message("Saved: " + StorySlots.DisplayName(slot), 1.5f);
            GameRoot.Audio.Play("ui_select");
            return true;
        }

        public void QuickSave() => SaveTo(StorySlots.Quick);

        public void QuickLoad()
        {
            if (!StoryStorage.Exists(StorySlots.Quick)) { GameRoot.Hud.Message("No quick save yet (F5 or Q.Save makes one).", 2f); return; }
            LoadNow(StorySlots.Quick);
        }

        /// <summary>Loads a slot: in the story scene at once; from the title, starts the story scene and loads it there.</summary>
        public static void LoadSlot(string slot)
        {
            if (_current != null) { _current.LoadNow(slot); return; }
            if (!GameRoot.Exists || !StoryStorage.Exists(slot)) return;
            _pendingLoad = slot;
            var cfg = StoryConfig.Load();
            if (GameRoot.Flow.Current == GameState.Title) GameRoot.Instance.StartGame(false); // a save: no difficulty picker
            else if (cfg != null) GameRoot.Instance.SceneService.Load(cfg.sceneName);
        }

        /// <summary>Puts a saved moment back: stage, sound, history, text mode, ink state and the line on screen.</summary>
        public bool LoadNow(string slot)
        {
            var snap = StoryStorage.ReadSlot(slot);
            if (snap == null) { GameRoot.Hud.Message(StorySlots.DisplayName(slot) + " is empty.", 2f); return false; }
            if (GameRoot.Flow.Current == GameState.Paused) GameRoot.Instance.Resume();
            var d = GameRoot.Dialogue;
            d.AutoMode = false;
            d.SkipMode = false;

            // Continuing from the autosave isn't "loading" (the game noticed the quit already); any other slot is.
            if (slot == StorySlots.Auto) Memory.inProgress = true;
            else Memory.RecordLoad(snap.turn);
            StoryStorage.SaveMemory();
            _ended = false;
            _endedWith = "";
            _endingShown = false;
            _endingCard.style.display = DisplayStyle.None;

            _state.CopyFrom(snap.stage);
            _stage.ShowState(_state);
            _text.SetHiddenByPlayer(false);
            _text.SetMode(_state.nvl);
            _text.LoadNvl(snap.nvlPage);
            if (string.IsNullOrEmpty(_state.music)) GameRoot.Audio.StopMusic(0.4f);
            else GameRoot.Audio.PlayMusic(_state.music, 0.6f);
            if (string.IsNullOrEmpty(_state.ambience)) GameRoot.Audio.StopAmbience(0.4f);
            else GameRoot.Audio.PlayAmbience(_state.ambience, 0.6f);
            _backlog.Load(snap.backlog);
            _chapter = snap.chapter ?? "";
            _knot = snap.knot ?? "";
            _pendingEnding = snap.pendingEnding ?? "";
            _playSeconds = snap.playSeconds;
            _investigation = config.FindHotspotScene(snap.investigation);
            _usedHotspots.Clear();
            foreach (var u in snap.used) _usedHotspots.Add(u);
            _hotspots.Hide();
            _inventory.SetVisible(false);
            _inventory.SetHeld("");

            var line = new DialogueLineInfo
            {
                speaker = snap.lineSpeaker,
                speakerId = snap.lineSpeaker,
                text = snap.lineText,
                knot = snap.knot,
                kind = snap.lineKind == LineKind.Thought ? DialogueLineKind.Thought : snap.lineKind == LineKind.Dialogue ? DialogueLineKind.Dialogue : DialogueLineKind.Narration,
            };
            _last = snap;
            if (!d.Resume(config.story.storyJson, snap.inkState, line, snap.parked, config.startKnot))
            {
                GameRoot.Hud.Message("That save doesn't fit this version of the story.", 3f);
                return false;
            }
            RefreshInventory(false);
            GameRoot.Hud.Message("Loaded: " + StorySlots.DisplayName(slot), 1.5f);
            return true;
        }
    }
}
