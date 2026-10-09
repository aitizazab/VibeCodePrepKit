using System.Collections;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>Presenter mode: a chapter starts a knot on a clean stage (no characters, text, hotspots
    /// or ending card left over), and attract mode reads at the auto-mode pace and clicks through a
    /// hotspot room by its walkthrough, pointing at each thing first.</summary>
    public sealed partial class StoryGame
    {
        AttractDriver _attractDriver;

        void SetupPresenter()
        {
            StoryPresenter.Register(PresenterMode.Chapters, this, this);
            _attractDriver = AttractDrive;
            PresenterMode.RegisterDriver(this, StoryPresenter.Scene, _attractDriver);
        }

        void TeardownPresenter() => PresenterMode.Unregister(this);

        /// <summary>Plays the story from <paramref name="knot"/> on a clean stage, with this music and
        /// ambience on until the knot's own tags change them ("" = silence). Memory isn't reset.</summary>
        public void PresenterKnot(string knot, string music, string ambience)
        {
            var d = GameRoot.Dialogue;
            if (d.IsActive || d.IsParked) d.Stop();
            d.SkipMode = false;
            _ctrlSkip = false;
            _state.Reset();
            _text.SetMode(false);
            _text.ClearNvl();
            _text.SetHiddenByPlayer(false);
            _stage.ShowState(_state);
            _chapter = "";
            _knot = "";
            _pendingEnding = "";
            _endedWith = "";
            _investigation = null;
            _usedHotspots.Clear();
            _lastItems.Clear();
            _hotspots.Hide();
            _inventory.SetVisible(false);
            _inventory.SetHeld("");
            _ended = false;
            _endingShown = false;
            _endingCard.style.display = DisplayStyle.None;
            var audio = GameRoot.Audio;
            if (string.IsNullOrEmpty(music)) audio.StopMusic(0.3f);
            else audio.PlayMusic(music);
            if (string.IsNullOrEmpty(ambience)) audio.StopAmbience(0.3f);
            else audio.PlayAmbience(ambience);
            d.Play(config.story.storyJson, knot);
        }

        // ---------- Attract mode ----------

        IEnumerator AttractDrive(string segment, float seconds, SyntheticInput input)
        {
            float end = Time.unscaledTime + seconds;
            var d = GameRoot.Dialogue;
            while (Time.unscaledTime < end)
            {
                // The shell's attract loop keeps auto mode on and takes choices after a moment.
                if (segment == "hotspots" && d.IsParked && _investigation != null && !d.IsActive)
                {
                    yield return AttractWalkthrough(_investigation, end);
                    continue;
                }
                yield return null;
            }
        }

        /// <summary>The room's walkthrough at a player's pace: point at the thing (its label shows), use it,
        /// read what it says, then the next.</summary>
        IEnumerator AttractWalkthrough(HotspotScene scene, float end)
        {
            var d = GameRoot.Dialogue;
            foreach (var step in scene.walkthrough)
            {
                if (_investigation != scene || Time.unscaledTime >= end) yield break;
                string spot = step, item = null;
                int gt = step.IndexOf('>');
                if (gt >= 0) { item = step.Substring(0, gt).Trim(); spot = step.Substring(gt + 1).Trim(); }
                while (d.IsActive && Time.unscaledTime < end) yield return null;
                if (!d.IsParked) yield break;
                int index = scene.hotspots.FindIndex(h => h.id == spot);
                if (index >= 0) _hotspots.Select(index);
                if (!string.IsNullOrEmpty(item)) _inventory.SetHeld(item);
                float look = Time.unscaledTime + 1.3f;
                while (Time.unscaledTime < look) yield return null;
                UseHotspot(spot, item);
                yield return null;
            }
        }
    }
}
