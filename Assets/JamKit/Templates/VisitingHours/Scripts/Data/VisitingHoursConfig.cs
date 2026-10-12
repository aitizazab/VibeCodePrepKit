using System;
using System.Collections.Generic;
using Ink.UnityIntegration;
using JamKit.Doomlike;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>One named picture: the art's file name (without .png) and its texture. The builder
    /// fills these from Templates/VisitingHours/Art, or with a neutral stand-in (fallback) until the
    /// real file exists.</summary>
    [Serializable]
    public class ArtRef
    {
        public string name = "";
        public Texture2D texture;
        [Tooltip("A generated stand-in: the next Rebuild Shell Scenes swaps in Art/<name>.png when it exists.")]
        public bool fallback;
    }

    /// <summary>
    /// Everything the Visiting Hours slice is made of, editable in the Inspector: the ink, the
    /// journal's entries and pair table, Walt's argument, the things to examine, the walking
    /// tuning, the art by name and the look of each place. Made by JamKit > Rebuild Shell Scenes
    /// from the core's SliceContent the first time (only what's missing afterwards).
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Visiting Hours/Config", fileName = "VisitingHoursConfig")]
    public sealed class VisitingHoursConfig : ScriptableObject
    {
        [Header("Story")]
        public InkFile story;

        [Header("Journal")]
        public List<JournalEntryDef> entries = new List<JournalEntryDef>();
        public List<DeductionPairDef> pairs = new List<DeductionPairDef>();
        [Tooltip("What Nadia thinks when two entries don't go together (in turn).")]
        public List<string> gentleLines = new List<string>();

        [Header("Walt's argument")]
        public BattleDef walt = new BattleDef();
        [Tooltip("Show the faint words \"press\" and \"listen\" in the box's halves. Off tests whether the zones read on their own.")]
        public bool showZoneWords = true;

        [Header("Characters")]
        [Tooltip("Walt's and Mags's portraits. The slice registers them with the dialogue when it starts, so it never depends on the active GameConfig's list.")]
        public List<Portrait> portraits = new List<Portrait>();

        [Header("Places")]
        public List<ExamineDef> examinables = new List<ExamineDef>();
        [Tooltip("Walt notices you (and you remember him at the grill) this close, metres.")]
        public float waltNoticeRange = 15f;

        [Header("Walking (the DOOM-like's controller, no weapons)")]
        public PlayerTuning player = new PlayerTuning();
        [Tooltip("Reach for E, metres.")]
        public float useRange = 2.6f;

        [Header("Look")]
        public Color hospitalTint = new Color(0.86f, 0.9f, 0.96f);
        public Color hospitalFog = new Color(0.16f, 0.18f, 0.2f);
        public Vector3 hospitalFogParams = new Vector3(10f, 40f, 0.35f);
        public Color boothTint = new Color(0.62f, 0.66f, 0.78f);
        public Color dreamTint = new Color(1.08f, 1f, 0.9f);
        public Color dreamFog = new Color(1f, 0.82f, 0.6f);
        public Vector3 dreamFogParams = new Vector3(14f, 70f, 0.5f);
        [Range(0f, 1f)] public float dreamVignette = 0.55f;

        [Header("Materials (made by the builder)")]
        public Material paintedMaterial;
        public Material worldMaterial;
        public Material skyMaterial;

        [Header("Art (by file name)")]
        public List<ArtRef> art = new List<ArtRef>();

        Dictionary<string, Texture2D> _art;

        /// <summary>The journal's book: this asset's entries, and any of the core's newer ones it doesn't have yet (so the
        /// days' new finds work before the next Rebuild Shell Scenes copies them in).</summary>
        public JournalBook MakeBook()
        {
            var all = new List<JournalEntryDef>(entries);
            foreach (var e in SliceContent.Entries())
            {
                bool have = false;
                foreach (var x in all) if (x != null && x.id == e.id) { have = true; break; }
                if (!have) all.Add(e);
            }
            return new JournalBook(all, pairs);
        }

        List<ExamineDef> _defaultExaminables;

        /// <summary>The picture with this file name (portrait_walt_sad, tex_grass...), or null.</summary>
        public Texture2D Art(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_art == null)
            {
                _art = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
                foreach (var a in art)
                    if (a != null && !string.IsNullOrEmpty(a.name) && a.texture != null) _art[a.name] = a.texture;
            }
            return _art.TryGetValue(name, out var t) ? t : null;
        }

        public Portrait FindPortrait(string id)
        {
            if (portraits == null || string.IsNullOrEmpty(id)) return null;
            foreach (var p in portraits)
                if (p != null && string.Equals(p.characterId, id, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        /// <summary>The examinable with this id: this asset's, else the core's default (a newer one the asset doesn't have yet).</summary>
        public ExamineDef FindExaminable(string id)
        {
            foreach (var e in examinables)
                if (e != null && e.id == id) return e;
            if (_defaultExaminables == null) _defaultExaminables = SliceContent.Examinables();
            foreach (var e in _defaultExaminables)
                if (e.id == id) return e;
            return null;
        }

        /// <summary>Forget the name lookup (the builder calls it after changing the art list).</summary>
        public void ResetArtCache() => _art = null;

        void OnValidate() => _art = null;
    }
}
