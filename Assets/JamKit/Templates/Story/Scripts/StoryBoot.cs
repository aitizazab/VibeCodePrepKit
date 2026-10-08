using JamKit.Shell;
using UnityEngine;

namespace JamKit.Narrative
{
    /// <summary>
    /// Hooks the story template into the shell's title screen when the story is the active template
    /// (GameConfig's first gameplay scene is the story scene): Continue (the autosave), Load and
    /// Endings buttons, "New game" instead of Play once there's something to continue, and the title,
    /// tagline and Play text the story changed (#meta) or the config's title variants pick.
    /// </summary>
    public static class StoryBoot
    {
        static StoryConfig _config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Hook()
        {
            _config = null;
            GameRoot.Ready -= OnReady;
            GameRoot.Ready += OnReady;
        }

        static void OnReady(GameRoot root)
        {
            _config = StoryConfig.Load();
            if (_config == null) return;
            if (SmokeRunner.Requested(out _)) StoryStorage.DeleteAll();
            StoryScreens.Ensure(_config);
            GameRoot.Ui.Title.Shown -= OnTitleShown;
            GameRoot.Ui.Title.Shown += OnTitleShown;
        }

        /// <summary>True when Play starts the story (the story template is the active one).</summary>
        public static bool StoryIsActive =>
            GameRoot.Exists && _config != null && GameRoot.Config.firstGameplayScene == _config.sceneName;

        static void OnTitleShown(TitleScreen title)
        {
            bool active = StoryIsActive;
            if (active)
            {
                title.AddButton("story-continue", "Continue", () => StoryGame.LoadSlot(StorySlots.Auto), "play", "jk-button--primary");
                title.AddButton("story-load", "Load", StoryScreens.OpenLoad, "howto");
                title.AddButton("story-endings", "Endings", StoryScreens.OpenEndings, "howto");
            }
            bool hasAuto = active && StoryStorage.Exists(StorySlots.Auto);
            title.SetButtonShown("story-continue", hasAuto);
            title.SetButtonShown("story-load", active && StoryStorage.AnySlot());
            title.SetButtonShown("story-endings", active && _config.endings.Count > 0);
            if (!active) return;
            ApplyTitleText(title);
            if (hasAuto) title.FocusFirst();
        }

        /// <summary>The title, tagline and Play text: what ink set with #meta wins, then the first title
        /// variant whose condition holds, then GameConfig.</summary>
        public static void ApplyTitleText(TitleScreen title)
        {
            var m = StoryStorage.Memory;
            string t = null, tag = null, play = null;
            var facts = new StoryGame.MemoryFacts(m);
            foreach (var v in _config.titleVariants)
            {
                if (v == null || !StoryCondition.Evaluate(v.condition, facts)) continue;
                if (!string.IsNullOrEmpty(v.title)) t = v.title;
                if (!string.IsNullOrEmpty(v.tagline)) tag = v.tagline;
                if (!string.IsNullOrEmpty(v.playLabel)) play = v.playLabel;
                break;
            }
            if (!string.IsNullOrEmpty(m.titleOverride)) t = m.titleOverride;
            if (!string.IsNullOrEmpty(m.taglineOverride)) tag = m.taglineOverride;
            if (!string.IsNullOrEmpty(m.playLabelOverride)) play = m.playLabelOverride;
            if (play == null && StoryStorage.Exists(StorySlots.Auto)) play = "New game";
            title.SetTitle(t);
            title.SetTagline(tag);
            title.SetButtonText("play", play);
        }
    }
}
