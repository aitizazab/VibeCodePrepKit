using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The small part of the slice the title screen needs (it lives in Resources so the title scene
    /// can find it): the opening's cues and pictures, the voices, and the TV news behind the menu.
    /// Kept apart from <see cref="VisitingHoursConfig"/> so other templates' builds don't carry
    /// the whole slice. Made by the builder; edit it freely.
    /// </summary>
    public sealed class VisitingHoursBoot : ScriptableObject
    {
        public const string ResourcePath = "JamKit/VisitingHoursBoot";

        [Tooltip("The slice's scene: the title hooks in only while it's GameConfig's first gameplay scene.")]
        public string sceneName = "VisitingHours";

        [Header("The opening (before the title)")]
        public List<OpeningCue> opening = new List<OpeningCue>();
        public Texture2D ceiling;
        public Texture2D phone;
        public AudioClip voicemail;
        public AudioClip sleepIn;
        [Tooltip("Stretch the voicemail's subtitles to the recording's length (when there is one).")]
        public bool fitSubtitlesToVoicemail = true;
        [Tooltip("Hold Escape this long to skip the opening (0 = can't skip).")]
        public float holdToSkipSeconds = 1.2f;

        [Tooltip("Templates/VisitingHours/UI/VisitingHours.uss (the opening and the title's TV look).")]
        public UnityEngine.UIElements.StyleSheet style;

        [Header("The title")]
        public Texture2D titleNews;
        public AudioClip newsAnchor;
        public string ticker = "";
        [Range(0f, 1f)] public float anchorVolume = 0.55f;

        public static VisitingHoursBoot Load() => Resources.Load<VisitingHoursBoot>(ResourcePath);

        /// <summary>A voice by the name a cue uses.</summary>
        public AudioClip Voice(string name)
        {
            switch (name)
            {
                case "vo_elias_voicemail": return voicemail;
                case "vo_nadia_sleep_in": return sleepIn;
                case "vo_news_anchor": return newsAnchor;
                default: return null;
            }
        }
    }
}
