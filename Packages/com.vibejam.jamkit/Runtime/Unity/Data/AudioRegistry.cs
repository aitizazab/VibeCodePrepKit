using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace JamKit.Shell
{
    /// <summary>
    /// Sound ids to clips. An id with no clip (or no entry at all) falls back to a generated
    /// placeholder sound if <see cref="ProceduralAudio"/> knows the id, so the game is never silent.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Audio Registry", fileName = "AudioRegistry")]
    public sealed class AudioRegistry : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string id = "coin";
            [Tooltip("Leave empty to use the generated placeholder for this id (if there is one).")]
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Random pitch change per play, e.g. 0.05 = up to 5% higher or lower.")]
            [Range(0f, 0.5f)] public float pitchVariance = 0.05f;
            [Tooltip("Music only: loop the clip.")]
            public bool loop = true;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string id)
        {
            if (entries == null || string.IsNullOrEmpty(id)) return null;
            foreach (var e in entries)
                if (e != null && e.id == id) return e;
            return null;
        }
    }
}
