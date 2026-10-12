using System.Collections.Generic;
using System.IO;
using Ink.UnityIntegration;
using JamKit.Doomlike;
using JamKit.Shell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JamKit.VisitingHours.EditorTools
{
    /// <summary>
    /// The slice's data assets: the config (from the core's SliceContent the first time; afterwards
    /// only what's missing, so the creator's edits stay), the title's boot asset, Walt's and Mags's
    /// portraits (kept in the config, not the active GameConfig), the materials, and the art
    /// and voice references (a stand-in is replaced as soon as the real file exists).
    /// </summary>
    public static class VisitingHoursContent
    {
        public const string ConfigPath = VisitingHoursBuilder.Root + "/Data/VisitingHoursConfig.asset";
        public const string BootPath = VisitingHoursBuilder.Root + "/Resources/JamKit/VisitingHoursBoot.asset";
        public const string PortraitsDir = VisitingHoursBuilder.Root + "/Data/Portraits";
        public const string MaterialsDir = VisitingHoursBuilder.Root + "/Materials";
        public const string AudioDir = VisitingHoursBuilder.Root + "/Audio";
        public const string InkPath = VisitingHoursBuilder.Root + "/Dialogue/VisitingHours.ink";

        public static VisitingHoursConfig EnsureAll(VisitingHoursArt.Set art, bool reset)
        {
            foreach (var d in new[] { VisitingHoursBuilder.Root + "/Data", PortraitsDir, MaterialsDir, VisitingHoursBuilder.Root + "/Resources/JamKit" })
                VisitingHoursBuilder.EnsureFolder(d);

            var config = AssetDatabase.LoadAssetAtPath<VisitingHoursConfig>(ConfigPath);
            bool fresh = config == null;
            if (fresh)
            {
                config = ScriptableObject.CreateInstance<VisitingHoursConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            if (fresh || reset) FillDefaults(config);
            else FillMissing(config);
            if (config.story == null) config.story = AssetDatabase.LoadAssetAtPath<InkFile>(InkPath);
            if (config.story == null) Debug.LogError("JamKit: " + InkPath + " didn't import as an ink file.");
            Materials(config);
            ArtRefs(config, art);
            EditorUtility.SetDirty(config);

            config.portraits = Portraits(art, reset);
            EditorUtility.SetDirty(config);
            Boot(art, reset);
            // The ward's PBR materials and window sky (the Day agent's redesign: docs/redesign/day.md).
            VhWardLibraryBuilder.Ensure();
            return config;
        }

        static void FillDefaults(VisitingHoursConfig c)
        {
            c.entries = SliceContent.Entries();
            c.pairs = SliceContent.Pairs();
            c.gentleLines = SliceContent.GentleLines();
            c.walt = SliceContent.WaltBattle();
            c.examinables = SliceContent.Examinables();
            c.player = Tuning();
        }

        /// <summary>Calm walking: the DOOM-like's body, slowed right down (Shift jogs).</summary>
        static PlayerTuning Tuning() => new PlayerTuning
        {
            motion = new PlayerMotionDef { runSpeed = 3.9f, walkSpeed = 2.2f, acceleration = 14f, friction = 16f, turnGrip = 1.3f },
            bobAmount = 0.028f,
            footstepStride = 1.6f,
            mouseDegreesPerPixel = 0.08f,
            gamepadDegreesPerSecond = 120f,
            maxPitch = 70f,
            useRange = 2.6f,
            hazardDamagePerSecond = 0f,
            verticalAutoAimDegrees = 0f,
        };

        static void FillMissing(VisitingHoursConfig c)
        {
            if (c.entries == null || c.entries.Count == 0) c.entries = SliceContent.Entries();
            else
                foreach (var e in SliceContent.Entries())
                    if (c.entries.Find(x => x != null && x.id == e.id) == null) c.entries.Add(e);
            if (c.pairs == null || c.pairs.Count == 0) c.pairs = SliceContent.Pairs();
            if (c.gentleLines == null || c.gentleLines.Count == 0) c.gentleLines = SliceContent.GentleLines();
            if (c.walt == null || c.walt.statements == null || c.walt.statements.Count == 0) c.walt = SliceContent.WaltBattle();
            if (c.examinables == null || c.examinables.Count == 0) c.examinables = SliceContent.Examinables();
            else
                foreach (var e in SliceContent.Examinables())
                    if (c.FindExaminable(e.id) == null) c.examinables.Add(e);
            if (c.player == null || c.player.motion == null) c.player = Tuning();
        }

        // ---------- Materials ----------

        static void Materials(VisitingHoursConfig c)
        {
            var shader = Shader.Find("JamKit/VisitingHours/Painted");
            if (shader == null) throw new System.Exception("Shader JamKit/VisitingHours/Painted not found (Templates/VisitingHours/Shaders).");
            c.paintedMaterial = Mat("VH_Painted", shader, m =>
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_Cutoff", 0.03f);
                m.renderQueue = 3000;
            });
            c.worldMaterial = Mat("VH_World", shader, m =>
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_Cutoff", 0f);
                m.renderQueue = 2000;
            });
            c.skyMaterial = Mat("VH_Sky", shader, m =>
            {
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_Cutoff", 0f);
                m.SetFloat("_FogScale", 0.15f);
                m.renderQueue = 1990;
            });
        }

        static Material Mat(string name, Shader shader, System.Action<Material> tweak)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            tweak(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Art and voices ----------

        static void ArtRefs(VisitingHoursConfig c, VisitingHoursArt.Set art)
        {
            if (c.art == null) c.art = new List<ArtRef>();
            c.ResetArtCache();
            foreach (var (name, _) in VisitingHoursArt.All)
            {
                art.textures.TryGetValue(name, out var tex);
                var r = c.art.Find(x => x != null && x.name == name);
                if (r == null)
                {
                    r = new ArtRef { name = name };
                    c.art.Add(r);
                }
                // Keep a texture the creator set by hand; replace empty ones and stand-ins.
                if (r.texture == null || VisitingHoursArt.IsFallback(r.texture) || (r.fallback && !art.fallbacks.Contains(name)))
                    r.texture = tex;
                r.fallback = VisitingHoursArt.IsFallback(r.texture);
            }
            c.ResetArtCache();
        }

        /// <summary>Walt's and Mags's portraits, kept in the slice's own config (the game registers them
        /// with the dialogue at start), so the active GameConfig is never touched.</summary>
        static List<Portrait> Portraits(VisitingHoursArt.Set art, bool reset)
        {
            var walt = Portrait("Walt", art, reset, ("neutral", "portrait_walt_neutral"), ("sad", "portrait_walt_sad"), ("firm", "portrait_walt_firm"));
            var mags = Portrait("Mags", art, reset, ("neutral", "portrait_mags"));
            return new List<Portrait> { walt, mags };
        }

        /// <summary>A portrait whose expressions are the whole waist-up paintings (the base is a blank,
        /// so changing expression never ghosts a different silhouette underneath).</summary>
        static Portrait Portrait(string id, VisitingHoursArt.Set art, bool reset, params (string expression, string file)[] expressions)
        {
            string path = PortraitsDir + "/" + id + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<Portrait>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<Portrait>();
                AssetDatabase.CreateAsset(p, path);
            }
            p.characterId = id;
            if (p.baseSprite == null || reset) p.baseSprite = art.blank;
            if (string.IsNullOrEmpty(p.defaultExpression)) p.defaultExpression = "neutral";
            if (p.expressions == null) p.expressions = new List<Portrait.Expression>();
            foreach (var (expression, file) in expressions)
            {
                art.sprites.TryGetValue(file, out var sprite);
                var e = p.expressions.Find(x => x != null && x.name == expression);
                if (e == null) p.expressions.Add(new Portrait.Expression { name = expression, sprite = sprite });
                else if (e.sprite == null || reset || VisitingHoursArt.IsFallback(e.sprite) && !art.fallbacks.Contains(file)) e.sprite = sprite;
            }
            EditorUtility.SetDirty(p);
            return p;
        }

        static void Boot(VisitingHoursArt.Set art, bool reset)
        {
            var b = AssetDatabase.LoadAssetAtPath<VisitingHoursBoot>(BootPath);
            if (b == null)
            {
                b = ScriptableObject.CreateInstance<VisitingHoursBoot>();
                AssetDatabase.CreateAsset(b, BootPath);
            }
            b.sceneName = VisitingHoursBuilder.SceneName;
            b.style = AssetDatabase.LoadAssetAtPath<StyleSheet>(VisitingHoursBuilder.StylePath);
            b.voicemail = Voice(b.voicemail, "vo_elias_voicemail");
            b.newsAnchor = Voice(b.newsAnchor, "vo_news_anchor");
            VisitingHoursOpeningContent.Fill(b, reset);
            EditorUtility.SetDirty(b);
        }

        static AudioClip Voice(AudioClip current, string name)
        {
            if (current != null) return current;
            string path = AudioDir + "/" + name + ".wav";
            var clip = File.Exists(path) ? AssetDatabase.LoadAssetAtPath<AudioClip>(path) : null;
            if (clip == null) Debug.LogWarning("JAMKIT_VH audio missing: " + path + " (silence + subtitles until it's there)");
            return clip;
        }
    }
}
