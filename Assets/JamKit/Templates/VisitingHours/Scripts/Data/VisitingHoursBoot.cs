using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>A named picture for the opening and the title (filled by the builder from Art/; null: a stand-in drawn in code).</summary>
    [Serializable]
    public class VhArtSlot
    {
        public string name = "";
        public Texture2D texture;
    }

    /// <summary>A named model (Kenney's furniture, Nadia) the waking-world sets place and restyle.</summary>
    [Serializable]
    public class VhModelSlot
    {
        public string name = "";
        public GameObject prefab;
    }

    /// <summary>The phone's small speaker: Unity's audio filters on the voicemail after Nadia drops the phone.</summary>
    [Serializable]
    public class VhPhoneSpeaker
    {
        [Tooltip("Cuts the lows (Hz): a phone speaker has no bass.")]
        public float highPass = 650f;
        [Tooltip("Cuts the highs (Hz).")]
        public float lowPass = 3600f;
        [Range(0f, 1f)] public float distortion = 0.32f;
        [Range(0f, 1f)] public float volume = 0.62f;
        [Tooltip("Seconds the change takes (a quick crossfade, not a click).")]
        public float blend = 0.25f;
    }

    /// <summary>
    /// The part of Visiting Hours the title needs (in Resources so the title scene finds it): the
    /// opening's cue list and recordings, the waking-world look (the style-E material, the pictures,
    /// the models) and the title table's headline. Kept apart from <see cref="VisitingHoursConfig"/> so
    /// other templates' builds don't carry the whole slice. Made by the builder; edit it freely.
    /// Re-timing: move the markers under Tracks (seconds into each recording), or use
    /// JamKit > Visiting Hours > Re-time the Voicemail (tap along in Play mode). docs/OPENING-AND-TITLE.md.
    /// </summary>
    public sealed class VisitingHoursBoot : ScriptableObject
    {
        public const string ResourcePath = "JamKit/VisitingHoursBoot";

        [Tooltip("The slice's scene: the title hooks in only while it's GameConfig's first gameplay scene.")]
        public string sceneName = "VisitingHours";
        [Tooltip("The builder resets the cues and markers when its defaults change shape (a newer version).")]
        public int dataVersion;

        [Header("The opening (before the title)")]
        [Tooltip("Each cue fires at 'at' seconds, or, with an anchor like voicemail:drop, at that marker plus 'offset'.")]
        public List<OpeningCue> cues = new List<OpeningCue>();
        [Tooltip("The recordings and their markers (seconds into the recording). Moving a marker moves every cue anchored to it.")]
        public List<VoiceTrack> tracks = new List<VoiceTrack>();
        public AudioClip voicemail;
        public AudioClip newsAnchor;
        [Tooltip("Subtitles from the markers' lines. Off while the voicemail take differs from the script.")]
        public bool showSubtitles;
        [Tooltip("Hold Escape this long to skip to the title (0 = can't skip).")]
        public float holdToSkipSeconds = 1.2f;
        public VhPhoneSpeaker speaker = new VhPhoneSpeaker();
        [Range(0f, 1f)] public float voicemailVolume = 1f;
        [Range(0f, 1f)] public float newsVolume = 0.9f;
        [Tooltip("Templates/VisitingHours/UI/VisitingHours.uss (the opening's overlay, the tablet and the phone screens).")]
        public UnityEngine.UIElements.StyleSheet style;

        [Header("The title table")]
        [Tooltip("The tablet's scrolling headline (script/opening-script.md; Larpaca is the gang, LeBaget the corporation; an old WeWork becomes Larpaca).")]
        public string headline = "";
        [Tooltip("Pixels per second on the tablet's 1024-wide screen.")]
        public float headlineSpeed = 90f;

        [Header("The waking-world look (style E)")]
        [Tooltip("The base material (shader JamKit/VisitingHours/Waking); every colour is a copy of it.")]
        public Material wakingMaterial;
        public List<VhArtSlot> art = new List<VhArtSlot>();
        public List<VhModelSlot> models = new List<VhModelSlot>();
        [Tooltip("Nadia's rigged model (Models/Nadia/Nadia.fbx). Empty: a stand-in arm and blanket shape.")]
        public GameObject nadiaModel;
        [Tooltip("Nadia's colour texture (Color.jpg next to her FBX).")]
        public Texture2D nadiaTexture;

        public static VisitingHoursBoot Load() => Resources.Load<VisitingHoursBoot>(ResourcePath);

        /// <summary>A recording by its clip name (vo_elias_voicemail, vo_news_anchor).</summary>
        public AudioClip Clip(string clipName)
        {
            switch (clipName)
            {
                case "vo_elias_voicemail": return voicemail;
                case "vo_news_anchor": return newsAnchor;
                default: return null;
            }
        }

        /// <summary>A recording's length (0 when it's missing).</summary>
        public float ClipLength(string clipName)
        {
            var c = Clip(clipName);
            return c != null ? c.length : 0f;
        }

        public VoiceTrack Track(string name) => OpeningScript.FindTrack(tracks, name);

        public Texture2D Art(string name)
        {
            if (art == null) return null;
            foreach (var a in art)
                if (a != null && a.name == name && a.texture != null) return a.texture;
            return null;
        }

        /// <summary>Every picture whose name starts with <paramref name="prefix"/> (the news frames: news_0, news_1...), in order.</summary>
        public void ArtList(string prefix, List<Texture2D> into)
        {
            into.Clear();
            if (art == null) return;
            foreach (var a in art)
                if (a != null && a.texture != null && a.name.StartsWith(prefix, StringComparison.Ordinal)) into.Add(a.texture);
        }

        public GameObject Model(string name)
        {
            if (models == null) return null;
            foreach (var m in models)
                if (m != null && m.name == name && m.prefab != null) return m.prefab;
            return null;
        }

        /// <summary>The cues with absolute times (anchors resolved against the real recordings).</summary>
        public List<OpeningCue> Resolve(List<string> notes) => OpeningScript.Resolve(cues, tracks, ClipLength, showSubtitles, notes);
    }
}
