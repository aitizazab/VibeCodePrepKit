using System;
using System.Collections.Generic;
using JamKit.Shell;
using UnityEngine;

namespace JamKit.Narrative
{
    /// <summary>
    /// Checks the story's data and ink together: the ids ink may use come from the config (and the
    /// Audio Registry plus the generated placeholder sounds), the ink source is read by
    /// <see cref="InkScan"/>, and <see cref="StoryValidator"/> reports problems in plain words.
    /// The editor menu (JamKit > Story > Validate Ink), the ink import check, the build check and the
    /// EditMode tests all run this.
    /// </summary>
    public static class StoryValidation
    {
        /// <summary>Every id the story may use, from the config.</summary>
        public static StoryIds Ids(StoryConfig config, AudioRegistry audio)
        {
            var ids = new StoryIds();
            if (config == null) return ids;
            foreach (var b in config.backgrounds) if (b != null && !string.IsNullOrEmpty(b.id)) ids.backgrounds.Add(b.id);
            foreach (var c in config.cgs) if (c != null && !string.IsNullOrEmpty(c.id)) ids.cgs.Add(c.id);
            foreach (var e in config.endings) if (e != null && !string.IsNullOrEmpty(e.id)) ids.endings.Add(e.id);
            foreach (var i in config.items) if (i != null && !string.IsNullOrEmpty(i.id)) ids.items.Add(i.id);
            foreach (var p in config.palette) if (p != null && !string.IsNullOrEmpty(p.name)) ids.palette.Add(p.name);
            foreach (var h in config.hotspotScenes) if (h != null && !string.IsNullOrEmpty(h.id)) ids.hotspotScenes.Add(h.id);
            foreach (var t in config.extraTags) if (!string.IsNullOrEmpty(t)) ids.extraTags.Add(t.TrimStart('#'));
            foreach (var c in config.characters)
            {
                if (c == null) continue;
                var exprs = new List<string>();
                if (c.portrait != null && c.portrait.expressions != null)
                    foreach (var e in c.portrait.expressions) if (e != null && !string.IsNullOrEmpty(e.name)) exprs.Add(e.name);
                ids.AddCharacter(c.id, c.displayName, exprs);
            }
            foreach (var id in ProceduralAudio.Ids) ids.sounds.Add(id);
            if (audio != null && audio.entries != null)
                foreach (var e in audio.entries) if (e != null && !string.IsNullOrEmpty(e.id)) ids.sounds.Add(e.id);
            ids.itemsList = string.IsNullOrEmpty(config.itemsList) ? "Items" : config.itemsList;
            if (!string.IsNullOrEmpty(config.startKnot)) ids.dataKnots.Add(config.startKnot);
            foreach (var h in config.hotspotScenes)
            {
                if (h == null || h.hotspots == null) continue;
                foreach (var spot in h.hotspots)
                {
                    if (spot == null) continue;
                    if (!string.IsNullOrEmpty(spot.knot)) ids.dataKnots.Add(spot.knot);
                    if (spot.itemUses != null)
                        foreach (var u in spot.itemUses) if (u != null && !string.IsNullOrEmpty(u.knot)) ids.dataKnots.Add(u.knot);
                }
                if (!string.IsNullOrEmpty(h.fallbackItemKnot)) ids.dataKnots.Add(h.fallbackItemKnot);
            }
            return ids;
        }

        /// <summary>Validates the given ink sources (file name to text) against the config. Also checks
        /// the config itself: duplicate ids, characters without portraits, unknown hotspot backgrounds.</summary>
        public static StoryProblems Check(StoryConfig config, AudioRegistry audio, IEnumerable<KeyValuePair<string, string>> inkFiles)
        {
            var scan = InkScan.Read(inkFiles);
            var ids = Ids(config, audio);
            var scenes = new List<HotspotSceneInfo>();
            if (config != null)
                foreach (var h in config.hotspotScenes) if (h != null) scenes.Add(h.ToInfo());
            var problems = StoryValidator.Validate(scan, ids, scenes);
            CheckConfig(config, problems);
            return problems;
        }

        static void CheckConfig(StoryConfig c, StoryProblems problems)
        {
            if (c == null) { problems.Error("StoryConfig", 0, "no story config"); return; }
            if (c.story == null) problems.Error("StoryConfig", 0, "no ink story set (Story field)");
            Dupes(c.backgrounds, b => b.id, "background", problems);
            Dupes(c.characters, x => x.id, "character", problems);
            Dupes(c.cgs, x => x.id, "CG", problems);
            Dupes(c.items, x => x.id, "item", problems);
            Dupes(c.endings, x => x.id, "ending", problems);
            Dupes(c.hotspotScenes, x => x.id, "hotspot scene", problems);
            foreach (var ch in c.characters)
            {
                if (ch == null) continue;
                if (ch.portrait == null) problems.Error("StoryConfig", 0, "character \"" + ch.id + "\" has no Portrait (base sprite and expressions)");
                else if (ch.portrait.baseSprite == null) problems.Error("StoryConfig", 0, "character \"" + ch.id + "\"'s portrait has no base sprite");
            }
            foreach (var v in c.titleVariants)
            {
                if (v == null) continue;
                string err = StoryCondition.Check(v.condition);
                if (err != null) problems.Error("StoryConfig", 0, "title variant \"" + v.condition + "\": " + err);
            }
            if (c.endings.Count == 0) problems.Warning("StoryConfig", 0, "no endings listed: the endings gallery is empty");
        }

        static void Dupes<T>(List<T> list, Func<T, string> key, string what, StoryProblems problems) where T : class
        {
            if (list == null) return;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in list)
            {
                if (x == null) { problems.Error("StoryConfig", 0, "an empty entry in the " + what + " list"); continue; }
                string k = key(x);
                if (string.IsNullOrEmpty(k)) problems.Error("StoryConfig", 0, "a " + what + " without an id");
                else if (!seen.Add(k)) problems.Error("StoryConfig", 0, "two " + what + "s with the id \"" + k + "\"");
            }
        }
    }
}
