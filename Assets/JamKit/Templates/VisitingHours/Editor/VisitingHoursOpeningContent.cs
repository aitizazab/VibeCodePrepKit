using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// Fills the title's boot asset for the opening and the title table (docs/OPENING-AND-TITLE.md):
    /// the cue list and the recordings' markers (the defaults the first time; the creator's edits stay),
    /// the headline (from script/opening-script.md, read only), the voices, the style-E material, the
    /// Kenney models (ThirdParty/), Nadia's model (Models/Nadia/) and the pictures in Art/ (found by name,
    /// in any subfolder; anything missing is drawn in code at run time and logged).
    /// </summary>
    public static class VisitingHoursOpeningContent
    {
        /// <summary>Bump to reset the defaults on the next Rebuild (when they change shape): the cues from any older
        /// version, the voice tracks (and their markers) only from before version 2 (3 re-timed the title's pieces; 4 her falling asleep and the pieces paced by the report).</summary>
        public const int DataVersion = 4;

        public const string Root = VisitingHoursBuilder.Root;
        public const string KenneyDir = Root + "/ThirdParty/Kenney/FurnitureKit";
        public const string NadiaDir = Root + "/Models/Nadia";
        public const string NadiaPath = NadiaDir + "/Nadia.fbx";
        public const string WakingShader = "JamKit/VisitingHours/Waking";
        public const string WakingMaterialPath = Root + "/Materials/VH_Waking.mat";
        public const string ScriptPath = "script/opening-script.md";
        public const float NadiaScale = 1.69f;

        /// <summary>The models the bedroom places (slot, Kenney file).</summary>
        public static readonly (string slot, string file)[] Models =
        {
            ("nightstand", "cabinetBedDrawerTable"), ("lamp", "lampRoundTable"), ("chair", "chair"), ("rug", "rugRectangle"),
            ("shelf", "bookcaseOpenLow"), ("books", "books"), ("plant", "pottedPlant"),
            // The waking days' ward and booth (VhWakingWard; the bedroom doesn't place these).
            ("chair_desk", "chairDesk"), ("sink", "bathroomSink"), ("armchair", "loungeChair"), ("coat_rack", "coatRackStanding"),
            ("radio", "radio"), ("box", "cardboardBoxClosed"),
        };

        /// <summary>The pictures the sets use, and the file names (no extension, any case, any subfolder of Art/) each one accepts.</summary>
        public static readonly (string slot, string[] files)[] ArtSlots =
        {
            ("tex_bedroom_wall", new[] { "tex_wallpaper", "tex_bedroom_wall", "tex_wall_bedroom", "bedroom_wall", "tex_wall_plaster", "tex_plaster" }),
            ("tex_bedroom_floor", new[] { "tex_woodfloor", "tex_bedroom_floor", "tex_floor_bedroom", "bedroom_floor", "tex_floor_wood", "tex_wood_floor" }),
            ("tex_wood", new[] { "tex_wood", "tex_wood_furniture", "tex_bed_wood" }),
            ("tex_table_wood", new[] { "tex_tablewood", "tex_table_wood", "tex_table", "table_wood", "tex_wood_table" }),
            ("tex_blanket", new[] { "tex_knit", "tex_blanket", "tex_fabric_blanket", "blanket" }),
            ("tex_bedsheet", new[] { "tex_bedsheet", "tex_sheet", "tex_bed_sheet", "bedsheet" }),
            ("coffee_quit", new[] { "op_coffee", "coffee_quit", "coffee_swirl_quit", "tex_coffee_quit", "coffee_milk_quit" }),
            ("coffee_swirl", new[] { "op_coffee_swirl", "coffee_swirl", "coffee_surface", "tex_coffee", "coffee" }),
            ("notepad_page", new[] { "op_notepad", "notepad_page", "notepad", "tex_notepad", "notepad_credits" }),
            ("badge_face", new[] { "op_badge", "badge_face", "id_badge", "badge", "tex_badge", "id_badge_face" }),
            ("badge_photo", new[] { "op_badge_photo", "badge_photo", "nadia_photo", "photo_nadia", "id_photo", "nadia_badge_photo" }),
            ("photo_frame", new[] { "op_photo_frame", "photo_frame", "framed_photo", "bedroom_photo", "nightstand_photo" }),
            ("phone_wallpaper", new[] { "op_phone_wallpaper", "phone_wallpaper", "wallpaper", "tex_phone_wallpaper", "lockscreen" }),
        };

        /// <summary>The tablet's news-video frames: every picture whose name starts with one of these (sorted by name).</summary>
        public static readonly string[] NewsFramePrefixes = { "op_news", "news_frame", "tablet_news", "news_video", "vh_news_frame", "newsframe" };

        /// <summary>A file name as the slots match it: lower case, '-' as '_', a review "-takeN" suffix dropped
        /// (op-coffee-take2.png matches "op_coffee").</summary>
        public static string Key(string fileName)
        {
            string k = fileName.ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            int take = k.LastIndexOf("_take", StringComparison.Ordinal);
            if (take > 0)
            {
                bool digits = take + 5 < k.Length;
                for (int i = take + 5; i < k.Length; i++) digits &= char.IsDigit(k[i]);
                if (digits) k = k.Substring(0, take);
            }
            return k;
        }

        /// <summary>Pictures the runtime reads pixels from (to write "Quit" into a plain swirl, a photo into the badge, "Settings" onto it, "Credits" onto the notepad).</summary>
        static readonly string[] ReadableSlots = { "coffee_swirl", "badge_photo", "notepad_page", "badge_face" };

        public static void Fill(VisitingHoursBoot b, bool reset)
        {
            bool fresh = b.dataVersion < DataVersion;
            if (reset || fresh || b.cues == null || b.cues.Count == 0) b.cues = SliceContent.OpeningCues();
            if (reset || b.dataVersion < 2 || b.tracks == null || b.tracks.Count == 0)
                b.tracks = new List<VoiceTrack> { SliceContent.VoicemailTrack(), SliceContent.NewsTrack() };
            else if (b.dataVersion < 4)
            {
                // The new, shorter news report: its track only (the voicemail keeps any re-timed markers).
                int i = b.tracks.FindIndex(t => t != null && t.name == "news");
                if (i >= 0) b.tracks[i] = SliceContent.NewsTrack();
                else b.tracks.Add(SliceContent.NewsTrack());
            }
            b.dataVersion = DataVersion;
            b.headline = SliceContent.Headline(ReadHeadline());
            b.wakingMaterial = EnsureMaterial();
            b.models = ModelSlots();
            EnsureNadiaImport();
            b.nadiaModel = AssetDatabase.LoadAssetAtPath<GameObject>(NadiaPath);
            b.nadiaTexture = FindNadiaTexture();
            b.art = ArtSlotsFound(out var missing);
            if (missing.Count > 0) Debug.Log("JAMKIT_VH art missing (opening and title, drawn in code until it's in Art/): " + string.Join(", ", missing));
            if (b.nadiaModel == null) Debug.Log("JAMKIT_VH Nadia's model isn't in " + NadiaPath + ": the opening uses the stand-in arm and blanket shape.");
            var problems = OpeningScript.Problems(b.cues, b.tracks, b.ClipLength);
            foreach (var p in problems) Debug.LogWarning("Visiting Hours opening: " + p);
        }

        /// <summary>The headline line under "## The tablet's scrolling headline" in the creator's script (read only), or "".</summary>
        public static string ReadHeadline()
        {
            try
            {
                if (!File.Exists(ScriptPath)) return "";
                var lines = File.ReadAllLines(ScriptPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].StartsWith("## ", StringComparison.Ordinal) || lines[i].IndexOf("headline", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string l = lines[j].Trim();
                        if (l.Length == 0) continue;
                        if (l.StartsWith("#", StringComparison.Ordinal) || l.StartsWith("---", StringComparison.Ordinal)) break;
                        return l;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Visiting Hours: couldn't read " + ScriptPath + " (" + e.Message + "); the default headline is used.");
            }
            return "";
        }

        static Material EnsureMaterial()
        {
            var shader = Shader.Find(WakingShader);
            if (shader == null)
            {
                Debug.LogError("JamKit: shader " + WakingShader + " not found (Templates/VisitingHours/Shaders/VisitingHoursWaking.shader).");
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WakingMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader) { name = "VH_Waking" };
                AssetDatabase.CreateAsset(mat, WakingMaterialPath);
            }
            if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        static List<VhModelSlot> ModelSlots()
        {
            var list = new List<VhModelSlot>();
            foreach (var (slot, file) in Models)
            {
                string path = KenneyDir + "/" + file + ".fbx";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) Debug.Log("JAMKIT_VH model missing: " + path + " (a stand-in shape is built)");
                list.Add(new VhModelSlot { name = slot, prefab = go });
            }
            return list;
        }

        /// <summary>Nadia's FBX: Tripo exports her 0.98 m tall, so the importer scales her by 1.69 (about 1.65 m),
        /// imports no materials (the style-E material is made in code) and no animation.</summary>
        public static void EnsureNadiaImport()
        {
            var imp = AssetImporter.GetAtPath(NadiaPath) as ModelImporter;
            if (imp == null) return;
            if (VhNadiaImport.Apply(imp)) imp.SaveAndReimport();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { NadiaDir }))
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (ti != null && VhNadiaImport.Apply(ti)) ti.SaveAndReimport();
            }
        }

        static Texture2D FindNadiaTexture()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { NadiaDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (name == "color" || name.EndsWith("_color", StringComparison.Ordinal) || name.Contains("basecolor") || name.Contains("albedo"))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        /// <summary>Every picture under Art/ (any subfolder), by lower-case file name without extension.</summary>
        static Dictionary<string, string> ArtFiles()
        {
            var map = new Dictionary<string, string>();
            if (!AssetDatabase.IsValidFolder(VisitingHoursArt.ArtDir)) return map;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VisitingHoursArt.ArtDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string key = Key(Path.GetFileNameWithoutExtension(path));
                if (!map.ContainsKey(key)) map[key] = path;
            }
            return map;
        }

        static List<VhArtSlot> ArtSlotsFound(out List<string> missing)
        {
            missing = new List<string>();
            var files = ArtFiles();
            var list = new List<VhArtSlot>();
            foreach (var (slot, names) in ArtSlots)
            {
                string found = null;
                foreach (var n in names)
                    if (files.TryGetValue(n, out found)) break;
                if (found == null)
                {
                    if (slot != "coffee_swirl" && slot != "badge_photo" && slot != "tex_bedsheet") missing.Add(slot);
                    continue;
                }
                bool tile = slot.StartsWith("tex_", StringComparison.Ordinal);
                Import(found, tile, Array.IndexOf(ReadableSlots, slot) >= 0);
                list.Add(new VhArtSlot { name = slot, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(found) });
            }
            var frames = new List<string>();
            foreach (var kv in files)
                foreach (var prefix in NewsFramePrefixes)
                    if (kv.Key.StartsWith(prefix, StringComparison.Ordinal)) { frames.Add(kv.Value); break; }
            frames.Sort(StringComparer.OrdinalIgnoreCase);
            if (frames.Count == 0) missing.Add("news frames (" + NewsFramePrefixes[0] + "_*)");
            for (int i = 0; i < frames.Count; i++)
            {
                Import(frames[i], false, false);
                list.Add(new VhArtSlot { name = "news_" + i, texture = AssetDatabase.LoadAssetAtPath<Texture2D>(frames[i]) });
            }
            return list;
        }

        /// <summary>Smooth, mipmapped, at most 2048; tiles repeat; only touched when something differs.</summary>
        static void Import(string path, bool tile, bool readable)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (ti.filterMode != FilterMode.Bilinear && ti.filterMode != FilterMode.Trilinear) { ti.filterMode = FilterMode.Bilinear; dirty = true; }
            if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
            var wrap = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if (ti.wrapMode != wrap) { ti.wrapMode = wrap; dirty = true; }
            if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; dirty = true; }
            if (readable && !ti.isReadable) { ti.isReadable = true; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
    }

    /// <summary>Import settings for Nadia's Tripo model and its textures (reproducible on any machine).</summary>
    public sealed class VhNadiaImport : AssetPostprocessor
    {
        static bool IsNadia(string path) => path.StartsWith(VisitingHoursOpeningContent.NadiaDir + "/", StringComparison.Ordinal);

        void OnPreprocessModel()
        {
            if (IsNadia(assetPath) && assetImporter is ModelImporter mi) Apply(mi);
        }

        void OnPreprocessTexture()
        {
            if (IsNadia(assetPath) && assetImporter is TextureImporter ti) Apply(ti);
        }

        /// <summary>Returns true when it changed something.</summary>
        public static bool Apply(ModelImporter mi)
        {
            bool dirty = false;
            if (!Mathf.Approximately(mi.globalScale, VisitingHoursOpeningContent.NadiaScale)) { mi.globalScale = VisitingHoursOpeningContent.NadiaScale; dirty = true; }
            if (mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; dirty = true; }
            if (mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            if (mi.optimizeGameObjects) { mi.optimizeGameObjects = false; dirty = true; }
            return dirty;
        }

        public static bool Apply(TextureImporter ti)
        {
            bool dirty = false;
            string name = Path.GetFileNameWithoutExtension(ti.assetPath).ToLowerInvariant();
            if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; dirty = true; }
            if (name.Contains("normal") && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            bool linear = name.Contains("roughness") || name.Contains("metallic");
            if (linear && ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
            return dirty;
        }
    }

    /// <summary>
    /// JamKit > Visiting Hours > Re-time the Voicemail: in Play mode with Visiting Hours active, plays the
    /// opening with a prompt; tap Space as Elias starts each marked line. When the voicemail ends the
    /// taps become the markers in the boot asset (saved), and every cue anchored to them follows.
    /// </summary>
    [InitializeOnLoad]
    public static class VhRetimeMenu
    {
        static VhRetimeMenu()
        {
            VhOpening.Retimed -= OnRetimed;
            VhOpening.Retimed += OnRetimed;
        }

        [MenuItem("JamKit/Visiting Hours/Re-time the Voicemail (tap along in Play mode)")]
        static void Retime()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Re-time the voicemail",
                    "Press Play first (with Visiting Hours as the template), then choose this again.\n\nThe opening plays; tap Space each time Elias starts the line shown at the top. When the voicemail ends, the new times are saved into Resources/JamKit/VisitingHoursBoot.asset > Tracks > voicemail.",
                    "OK");
                return;
            }
            VhOpening.RetimeMode = true;
            if (VhBoot.PlayOpeningNow() == null) Debug.LogWarning("Visiting Hours: the opening can only play on the title, with Visiting Hours active.");
        }

        static void OnRetimed(List<float> taps, float length)
        {
            VhOpening.RetimeMode = false;
            var boot = AssetDatabase.LoadAssetAtPath<VisitingHoursBoot>(VisitingHoursContent.BootPath);
            var track = boot != null ? boot.Track("voicemail") : null;
            if (track == null) return;
            Undo.RecordObject(boot, "Re-time the voicemail");
            if (!OpeningScript.Retime(track, taps, length))
            {
                Debug.LogWarning("Visiting Hours: the taps didn't fit the markers; nothing changed.");
                return;
            }
            EditorUtility.SetDirty(boot);
            AssetDatabase.SaveAssets();
            var lines = new List<string>();
            foreach (var m in track.markers) lines.Add(m.name + " " + m.at.ToString("0.00") + " s");
            Debug.Log("Visiting Hours: voicemail re-timed (" + length.ToString("0.00") + " s): " + string.Join(", ", lines));
        }
    }
}
