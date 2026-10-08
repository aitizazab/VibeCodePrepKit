using System;
using System.Collections.Generic;
using Ink.UnityIntegration;
using JamKit.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative
{
    /// <summary>A background: the id ink uses (#bg:id), a picture (or a plain colour until there is one).</summary>
    [Serializable]
    public class BackgroundDef
    {
        public string id = "room";
        [Tooltip("16:9 is best (it fills the stage). Leave empty to use the colour.")]
        public Sprite sprite;
        public Color color = new Color(0.2f, 0.2f, 0.25f);
        [Tooltip("For you: what this place is (not shown in the game).")]
        public string note = "";
    }

    /// <summary>A character on stage: id for #show, display name, name colour, and a Portrait (base + expressions).</summary>
    [Serializable]
    public class CharacterDef
    {
        public string id = "someone";
        [Tooltip("Shown in the name box. \"Iris: Hello.\" in ink works with the id or this name.")]
        public string displayName = "Someone";
        public Color nameColor = new Color(1f, 0.79f, 0.29f);
        [Tooltip("The shell's Portrait asset: base sprite plus named expressions (same size, transparent).")]
        public Portrait portrait;
        [Tooltip("Height on stage as a share of the screen (0.9 = nearly full height).")]
        [Range(0.2f, 1.4f)] public float height = 0.9f;
        [Tooltip("Lifts (+) or lowers (-) the character, as a share of the screen.")]
        [Range(-0.5f, 0.5f)] public float yOffset;
        [Tooltip("Sound id that blips while this character's lines type (empty: the shell's blip).")]
        public string voice = "";
    }

    /// <summary>A full-screen picture (#cg:id).</summary>
    [Serializable]
    public class CgDef
    {
        public string id = "moment";
        public Sprite sprite;
        public Color color = new Color(0.3f, 0.3f, 0.35f);
        [Tooltip("For you (not shown).")]
        public string note = "";
    }

    /// <summary>An inventory item: id (also an item of ink's LIST Items), name, icon.</summary>
    [Serializable]
    public class ItemDef
    {
        public string id = "key";
        public string displayName = "Key";
        [TextArea(1, 3)] public string description = "";
        public Sprite icon;
        public Color color = new Color(0.9f, 0.8f, 0.4f);
    }

    /// <summary>One ending of the game (#ending:id), as the gallery shows it.</summary>
    [Serializable]
    public class EndingDef
    {
        public string id = "ending";
        public string title = "An Ending";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Shown on the locked card. Empty = ???")]
        [TextArea(1, 3)] public string hint = "";
        [Tooltip("Hidden until found (the card only says a secret exists).")]
        public bool secret;
        public Sprite image;
        public Color color = new Color(0.4f, 0.4f, 0.6f);
    }

    [Serializable]
    public class NamedColor
    {
        public string name = "accent";
        public Color color = new Color(1f, 0.79f, 0.29f);
    }

    /// <summary>The title screen after certain events: the first variant whose condition is true wins
    /// (conditions like "ending:return_ticket", "playthrough >= 2"; see StoryCondition).</summary>
    [Serializable]
    public class TitleVariant
    {
        public string condition = "ending:some_ending";
        [Tooltip("Empty keeps the game's title.")]
        public string title = "";
        [Tooltip("Empty keeps the tagline.")]
        public string tagline = "";
        [Tooltip("The Play button's text. Empty keeps it.")]
        public string playLabel = "";
    }

    /// <summary>
    /// The story template's one config: the ink story, every id ink can use (backgrounds, characters,
    /// CGs, items, hotspot scenes, endings, palette colours), title variants, and text settings. Lives
    /// in Resources so the title screen can show the endings gallery and saves before the story scene loads.
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Story/Story Config", fileName = "StoryConfig")]
    public sealed class StoryConfig : ScriptableObject
    {
        public const string ResourcePath = "JamKit/StoryConfig";

        [Header("Story")]
        [Tooltip("The main .ink file (it can INCLUDE others).")]
        public InkFile story;
        [Tooltip("Knot to start at. Empty = the top of the file.")]
        public string startKnot = "";
        [Tooltip("The scene that plays the story (Build Settings name).")]
        public string sceneName = "Story";

        [Header("Stage")]
        public List<BackgroundDef> backgrounds = new List<BackgroundDef>();
        public List<CharacterDef> characters = new List<CharacterDef>();
        public List<CgDef> cgs = new List<CgDef>();
        [Tooltip("Colours for <c=name>...</c> in lines (ink can't have # in a line, so no hex there).")]
        public List<NamedColor> palette = new List<NamedColor>();

        [Header("Point and click")]
        public List<ItemDef> items = new List<ItemDef>();
        public List<HotspotScene> hotspotScenes = new List<HotspotScene>();
        [Tooltip("The ink LIST that names the items.")]
        public string itemsList = "Items";
        [Tooltip("The ink variable holding what the player carries (VAR inventory = ()).")]
        public string inventoryVariable = "inventory";

        [Header("Endings and memory")]
        public List<EndingDef> endings = new List<EndingDef>();
        public List<TitleVariant> titleVariants = new List<TitleVariant>();
        [Tooltip("How long the ending card stays before the victory screen (a click goes on sooner).")]
        public float endingCardSeconds = 6f;

        [Header("Text")]
        public float charsPerSecond = 40f;
        [Tooltip("Full-screen text (#nvl): lines per page before it clears itself.")]
        public int nvlMaxLines = 8;
        [Tooltip("\"Iris: Hello.\" sets the speaker for that line (the name must be a character's id or display name).")]
        public bool inlineSpeakers = true;
        [Tooltip("The small buttons on the text box: Log, Auto, Skip, Save, Load, Quick save/load.")]
        public bool quickMenu = true;

        [Header("Look")]
        public StyleSheet style;

        [Header("Checks")]
        [Tooltip("Your own tags (handled by your code via GameRoot.Dialogue.TagReceived): the validator won't call them unknown.")]
        public List<string> extraTags = new List<string>();
        [Tooltip("Filled by the editor (JamKit > Story > Validate Ink): every knot, for the debug menu's Jump to knot.")]
        public List<string> knotIndex = new List<string>();

        public BackgroundDef FindBackground(string id) => Find(backgrounds, id, b => b.id);
        public CharacterDef FindCharacter(string id)
        {
            var c = Find(characters, id, x => x.id);
            if (c != null || string.IsNullOrEmpty(id) || characters == null) return c;
            foreach (var x in characters)
                if (x != null && string.Equals(x.displayName, id, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }
        public CgDef FindCg(string id) => Find(cgs, id, c => c.id);
        public ItemDef FindItem(string id) => Find(items, id, i => i.id);
        public EndingDef FindEnding(string id) => Find(endings, id, e => e.id);
        public HotspotScene FindHotspotScene(string id) => Find(hotspotScenes, id, h => h.id);

        public bool TryPalette(string name, out Color color)
        {
            color = Color.white;
            if (palette == null || string.IsNullOrEmpty(name)) return false;
            foreach (var p in palette)
                if (p != null && string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)) { color = p.color; return true; }
            return false;
        }

        /// <summary>"#rrggbb" for &lt;c=name&gt;, or null.</summary>
        public string PaletteHex(string name) => TryPalette(name, out var c) ? "#" + ColorUtility.ToHtmlStringRGB(c) : null;

        /// <summary>The endings as the core gallery sees them.</summary>
        public List<EndingInfo> EndingInfos()
        {
            var list = new List<EndingInfo>();
            if (endings == null) return list;
            foreach (var e in endings)
                if (e != null) list.Add(new EndingInfo { id = e.id, title = e.title, description = e.description, hint = e.hint, secret = e.secret });
            return list;
        }

        static T Find<T>(List<T> list, string id, Func<T, string> key) where T : class
        {
            if (list == null || string.IsNullOrEmpty(id)) return null;
            foreach (var x in list)
                if (x != null && string.Equals(key(x), id, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }

        static StoryConfig _loaded;

        /// <summary>The config in Resources (cached), or null when the story template was removed.</summary>
        public static StoryConfig Load()
        {
            if (!ReferenceEquals(_loaded, null) && _loaded != null) return _loaded;
            _loaded = Resources.Load<StoryConfig>(ResourcePath);
            return _loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _loaded = null;
    }
}
