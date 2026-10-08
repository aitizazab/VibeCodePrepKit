using System.Collections.Generic;
using Ink.UnityIntegration;
using JamKit.Shell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.Narrative.EditorTools
{
    /// <summary>
    /// The sample's data: the story config (backgrounds, characters, CGs, items, endings, palette,
    /// title variants), the two characters' Portrait assets and the office hotspot scene. Made once;
    /// a rebuild only fills references that are empty (a sprite you replaced stays yours).
    /// "JamKit > Story > Reset Content to Defaults" rewrites them all.
    /// </summary>
    public static class StoryContent
    {
        public const string ConfigPath = StoryBuilder.Root + "/Resources/JamKit/StoryConfig.asset";
        public const string CharactersDir = StoryBuilder.Root + "/Data/Characters";
        public const string HotspotsDir = StoryBuilder.Root + "/Data/Hotspots";
        public const string MainInkPath = StoryBuilder.Root + "/Dialogue/LastTrain.ink";

        public static StoryConfig EnsureAll(StoryArt.Set art, bool reset)
        {
            StoryBuilder.EnsureFolder(StoryBuilder.Root + "/Resources/JamKit");
            StoryBuilder.EnsureFolder(CharactersDir);
            StoryBuilder.EnsureFolder(HotspotsDir);
            var iris = Character("Iris", "iris", art, reset);
            var bram = Character("Bram", "bram", art, reset);
            var office = Office(art, reset);

            var config = AssetDatabase.LoadAssetAtPath<StoryConfig>(ConfigPath);
            bool fresh = config == null;
            if (fresh)
            {
                config = ScriptableObject.CreateInstance<StoryConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            if (fresh || reset) Fill(config, art, iris, bram, office);
            else FillMissing(config, art, iris, bram, office);
            EditorUtility.SetDirty(config);
            return config;
        }

        static Portrait Character(string fileName, string id, StoryArt.Set art, bool reset)
        {
            string path = CharactersDir + "/" + fileName + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<Portrait>(path);
            bool fresh = p == null;
            if (fresh)
            {
                p = ScriptableObject.CreateInstance<Portrait>();
                AssetDatabase.CreateAsset(p, path);
            }
            if (fresh || reset)
            {
                p.characterId = id;
                p.baseSprite = art.portraitBases[id];
                p.defaultExpression = "neutral";
                p.expressions = new List<Portrait.Expression>();
                foreach (var kv in art.expressions[id]) p.expressions.Add(new Portrait.Expression { name = kv.Key, sprite = kv.Value });
            }
            else
            {
                if (p.baseSprite == null) p.baseSprite = art.portraitBases[id];
                foreach (var kv in art.expressions[id])
                {
                    var e = p.expressions.Find(x => x != null && x.name == kv.Key);
                    if (e == null) p.expressions.Add(new Portrait.Expression { name = kv.Key, sprite = kv.Value });
                    else if (e.sprite == null) e.sprite = kv.Value;
                }
            }
            EditorUtility.SetDirty(p);
            return p;
        }

        static HotspotScene Office(StoryArt.Set art, bool reset)
        {
            string path = HotspotsDir + "/Office.asset";
            var s = AssetDatabase.LoadAssetAtPath<HotspotScene>(path);
            bool fresh = s == null;
            if (fresh)
            {
                s = ScriptableObject.CreateInstance<HotspotScene>();
                AssetDatabase.CreateAsset(s, path);
            }
            if (!fresh && !reset) return s;
            s.id = "office";
            s.background = "office";
            s.prompt = "Find her ticket. Click things to look at them (or arrow keys and Enter). Items appear at the bottom left: click one, then click where to use it.";
            s.showMarkers = true;
            s.fallbackItemKnot = "office_no_use";
            s.hotspots = new List<HotspotScene.Hotspot>
            {
                new HotspotScene.Hotspot { id = "door", label = "Door to the waiting room", shape = HotspotShapeKind.Rect, rect = new Rect(0.03f, 0.18f, 0.14f, 0.77f), knot = "office_door" },
                // A polygon, to show the shape: the coat on its stand.
                new HotspotScene.Hotspot
                {
                    id = "coat", label = "Grey coat", shape = HotspotShapeKind.Polygon, knot = "office_coat",
                    points = new List<Vector2> { new Vector2(0.215f, 0.3f), new Vector2(0.252f, 0.22f), new Vector2(0.29f, 0.3f), new Vector2(0.3f, 0.75f), new Vector2(0.205f, 0.75f) },
                },
                new HotspotScene.Hotspot { id = "board", label = "Notice board", rect = new Rect(0.36f, 0.14f, 0.2f, 0.26f), knot = "office_board" },
                new HotspotScene.Hotspot { id = "window", label = "Window", rect = new Rect(0.62f, 0.12f, 0.18f, 0.3f), knot = "office_window" },
                new HotspotScene.Hotspot { id = "shelves", label = "Lost property shelves", rect = new Rect(0.82f, 0.1f, 0.16f, 0.82f), knot = "office_shelves" },
                new HotspotScene.Hotspot
                {
                    id = "desk", label = "Bram's desk", shape = HotspotShapeKind.Polygon, knot = "office_desk",
                    points = new List<Vector2> { new Vector2(0.34f, 0.56f), new Vector2(0.74f, 0.56f), new Vector2(0.76f, 0.66f), new Vector2(0.74f, 0.92f), new Vector2(0.34f, 0.92f), new Vector2(0.32f, 0.66f) },
                },
                new HotspotScene.Hotspot
                {
                    id = "desk_drawer", label = "Desk drawer", rect = new Rect(0.48f, 0.66f, 0.12f, 0.08f), knot = "office_drawer",
                    itemUses = new List<HotspotScene.ItemUse> { new HotspotScene.ItemUse { item = "brass_key", knot = "office_drawer_key" } },
                },
            };
            s.walkthrough = new List<string> { "coat", "brass_key>desk_drawer", "shelves", "door" };
            EditorUtility.SetDirty(s);
            return s;
        }

        static void Fill(StoryConfig c, StoryArt.Set art, Portrait iris, Portrait bram, HotspotScene office)
        {
            c.story = AssetDatabase.LoadAssetAtPath<InkFile>(MainInkPath);
            c.startKnot = "";
            c.sceneName = StoryBuilder.SceneName;
            c.backgrounds = new List<BackgroundDef>
            {
                Bg("platform_night", art, "#1d2a4a", "the platform at night, in the rain"),
                Bg("waiting_room", art, "#7a522c", "the warm waiting room"),
                Bg("office", art, "#3f5244", "the lost property office (hotspot scene)"),
                Bg("platform_dawn", art, "#f2a46b", "the platform at sunrise"),
                Bg("train_interior", art, "#5b452c", "inside the last train"),
                Bg("street_rain", art, "#26304f", "the street at the top of the station steps"),
                new BackgroundDef { id = "black", color = Color.black, note = "plain black" },
                new BackgroundDef { id = "white", color = Color.white, note = "plain white" },
            };
            c.characters = new List<CharacterDef>
            {
                new CharacterDef { id = "iris", displayName = "Iris", nameColor = Hex("#e8c547"), portrait = iris, height = 0.92f, voice = "voice_high" },
                new CharacterDef { id = "bram", displayName = "Bram", nameColor = Hex("#7ea6e0"), portrait = bram, height = 0.98f, voice = "voice_low" },
            };
            c.cgs = new List<CgDef>
            {
                Cg("cg_photo", art, "#b4935f", "the old photograph"),
                Cg("cg_train", art, "#10131f", "the 23:52 arriving"),
                Cg("cg_umbrella", art, "#53588a", "two people under one umbrella"),
                Cg("cg_ticket", art, "#c9a06a", "the ticket in the drawer"),
            };
            c.palette = new List<NamedColor>
            {
                new NamedColor { name = "accent", color = Hex("#ffc94a") },
                new NamedColor { name = "memory", color = Hex("#d9b38c") },
                new NamedColor { name = "danger", color = Hex("#ff6b6b") },
            };
            c.items = new List<ItemDef>
            {
                new ItemDef { id = "brass_key", displayName = "Small brass key", description = "From the pocket of a grey coat.", icon = art.items["brass_key"], color = Hex("#d8b13f") },
                new ItemDef { id = "ticket", displayName = "Ticket to Wexley", description = "Brown card. A name in pencil on the back.", icon = art.items["ticket"], color = Hex("#c9a06a") },
                new ItemDef { id = "photograph", displayName = "Old photograph", description = "Two people on this platform, long ago.", icon = art.items["photograph"], color = Hex("#efe3c8") },
            };
            c.hotspotScenes = new List<HotspotScene> { office };
            c.endings = new List<EndingDef>
            {
                new EndingDef { id = "last_train", title = "The Last Train", description = "You caught the 23:52. Somebody stayed behind.", hint = "Some people get on the train.", image = art.backgrounds["train_interior"], color = Hex("#5b452c") },
                new EndingDef { id = "missed_connection", title = "Missed Connection", description = "You missed the last train on purpose, and walked home at sunrise.", hint = "Some people stay.", image = art.cgs["cg_umbrella"], color = Hex("#53588a") },
                new EndingDef { id = "lost_property", title = "Lost Property", description = "You gave away your ticket. Something lost was found.", hint = "Some things belong to someone. Look carefully in the office.", image = art.cgs["cg_photo"], color = Hex("#b4935f") },
                new EndingDef { id = "return_ticket", title = "Return Ticket", description = "You came back, and this time she left the station.", secret = true, image = art.backgrounds["street_rain"], color = Hex("#26304f") },
            };
            c.titleVariants = new List<TitleVariant>
            {
                // The secret ending changes the title itself with #meta tags (see LastTrain_Endings.ink);
                // this variant shows the data way: after any other ending, the title screen remembers.
                new TitleVariant { condition = "endings_seen >= 1 && !ending:return_ticket", tagline = "Somebody is still waiting on the platform.", playLabel = "Go back" },
            };
            c.endingCardSeconds = 7f;
            c.charsPerSecond = 40f;
            c.nvlMaxLines = 8;
            c.inlineSpeakers = true;
            c.quickMenu = true;
            c.itemsList = "Items";
            c.inventoryVariable = "inventory";
            c.style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StoryBuilder.StylePath);
            c.extraTags = new List<string>();
        }

        /// <summary>A rebuild on an existing config: only empty references are filled.</summary>
        static void FillMissing(StoryConfig c, StoryArt.Set art, Portrait iris, Portrait bram, HotspotScene office)
        {
            if (c.story == null) c.story = AssetDatabase.LoadAssetAtPath<InkFile>(MainInkPath);
            if (c.style == null) c.style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StoryBuilder.StylePath);
            foreach (var b in c.backgrounds)
                if (b != null && b.sprite == null && art.backgrounds.TryGetValue(b.id, out var sp)) b.sprite = sp;
            foreach (var g in c.cgs)
                if (g != null && g.sprite == null && art.cgs.TryGetValue(g.id, out var sp)) g.sprite = sp;
            foreach (var i in c.items)
                if (i != null && i.icon == null && art.items.TryGetValue(i.id, out var sp)) i.icon = sp;
            foreach (var ch in c.characters)
            {
                if (ch == null || ch.portrait != null) continue;
                if (ch.id == "iris") ch.portrait = iris;
                else if (ch.id == "bram") ch.portrait = bram;
            }
            if (c.hotspotScenes.Count == 0) c.hotspotScenes.Add(office);
        }

        static BackgroundDef Bg(string id, StoryArt.Set art, string hex, string note) =>
            new BackgroundDef { id = id, sprite = art.backgrounds[id], color = Hex(hex), note = note };

        static CgDef Cg(string id, StoryArt.Set art, string hex, string note) =>
            new CgDef { id = id, sprite = art.cgs[id], color = Hex(hex), note = note };

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
