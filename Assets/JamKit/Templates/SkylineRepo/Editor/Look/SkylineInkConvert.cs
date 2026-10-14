using System.Collections.Generic;
using System.Text;
using JamKit.Look.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JamKit.SkylineRepo.EditorTools
{
    /// <summary>
    /// Turns the scene's look-library materials into their comic-ink twins (docs/SKYLINE-STYLE.md; docs/SKYLINE-PLAN-STYLE.md
    /// Task 3). Run at the end of each Skyline scene build:
    /// <list type="bullet">
    /// <item><see cref="ConvertScene"/>: every mesh and skinned renderer's shared material on URP Lit, Unlit or Simple Lit gets a
    /// cached twin <c>Look/Ink/Twins/INK_&lt;name&gt;.mat</c> on <c>Skyline/InkLit</c> (created or updated), coloured from
    /// <see cref="SkylineInkPalette"/>. The twin keeps the base map only when the palette says so (props, products, signs),
    /// keeps a normal map with <c>_NORMALMAP</c> and emission with <c>_EMISSION</c>, and an Unlit material (a sign, a poster)
    /// becomes a glow (<c>_Unlit</c>) with its colour and map as they were. Transparent materials (<c>SK_Glass_*</c>) are left alone.</item>
    /// <item><see cref="MakeGrey"/>: the colour-free zones, a per-renderer <c>INK_&lt;name&gt;__Grey</c> twin with <c>_Desaturate</c> 1.</item>
    /// <item><see cref="EnsureResourceMaterials"/>: the build-included materials in <c>Resources/SkylineFx</c> (the bot paint,
    /// the acid, the glow) moved onto InkLit and saved to disk, plus four InkLit templates, one per normal-map and emission
    /// keyword combination, which run-time code clones (<see cref="SkylineInkRoles.FromLit"/>). A keyword turned on at run
    /// time must live on a build-included material, or the build strips its variant (the invisible-bots lesson).</item>
    /// </list>
    /// </summary>
    public static class SkylineInkConvert
    {
        public const string TwinFolder = SkylineLookLibrary.LookRoot + "Ink/Twins/";
        public const string ResourcesFolder = SkylineLookLibrary.Root + "Resources/SkylineFx/";

        const string LitShader = "Universal Render Pipeline/Lit", UnlitShader = "Universal Render Pipeline/Unlit",
            SimpleLitShader = "Universal Render Pipeline/Simple Lit";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int CullId = Shader.PropertyToID("_Cull");

        static Shader InkShader => Shader.Find(SkylineInkRoles.ShaderName);

        /// <summary>True for an opaque material on URP Lit, Unlit or Simple Lit (not a glass, not an effect shader).</summary>
        public static bool IsConvertible(Material m)
        {
            if (m == null || m.shader == null) return false;
            string s = m.shader.name;
            if (s != LitShader && s != UnlitShader && s != SimpleLitShader) return false;
            if (m.name.StartsWith("SK_Glass_", System.StringComparison.Ordinal)) return false;
            if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) return false;   // transparent
            return true;
        }

        // ---------------------------------------------------------------------------------------------- the scene

        /// <summary>Swaps every convertible shared material under the scene's mesh and skinned renderers for its ink twin.
        /// Returns how many material slots were converted. Idempotent: twins are InkLit already, so a second run changes nothing.</summary>
        public static int ConvertScene(Scene scene)
        {
            if (InkShader == null) { Debug.LogError("SKYLINE_INK Skyline/InkLit not found; nothing converted"); return 0; }
            var session = new Session();
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (!IsConvertible(mats[i])) continue;
                        var twin = TwinFor(mats[i], false, session);
                        if (twin == null) continue;
                        mats[i] = twin;
                        changed = true;
                        n++;
                    }
                    if (changed) r.sharedMaterials = mats;
                }
            AssetDatabase.SaveAssets();
            Debug.Log("SKYLINE_INK converted " + n + " material slots (" + session.Count + " twins)");
            foreach (var line in SkylineHueLint.Run(scene)) Debug.LogWarning("SKYLINE_HUE " + line);   // the hostile hue is reserved (S0's lint)
            return n;
        }

        /// <summary>The colour-free zone: every ink twin under <paramref name="root"/> is swapped for its
        /// <c>INK_&lt;name&gt;__Grey</c> variant (<c>_Desaturate</c> 1; a glow stays as it is, so a sign keeps its colour).
        /// Returns the number of slots changed.</summary>
        public static int MakeGrey(Transform root)
        {
            var session = new Session();
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (!SkylineInkRoles.IsInk(m) || m.name.EndsWith("__Grey", System.StringComparison.Ordinal)) continue;
                    if (m.GetFloat("_Unlit") > 0.5f) continue;          // lights carry the hue
                    var grey = GreyTwin(m, session);
                    if (grey == null) continue;
                    mats[i] = grey;
                    changed = true;
                    n++;
                }
                if (changed) r.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
            return n;
        }

        /// <summary>A twin copied from an existing twin with <c>_Desaturate</c> 1, saved as <c>&lt;name&gt;__Grey</c>.</summary>
        static Material GreyTwin(Material twin, Session session)
        {
            string path = TwinFolder + twin.name + "__Grey.mat";
            var grey = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (grey == null)
            {
                VhLookLibrary.EnsureAssetFolder(path);
                grey = new Material(twin) { name = twin.name + "__Grey" };
                AssetDatabase.CreateAsset(grey, path);
            }
            else if (grey.shader != twin.shader) grey.shader = twin.shader;
            grey.CopyPropertiesFromMaterial(twin);
            grey.SetFloat("_Desaturate", 1f);
            // Fix round 1 (the colour-free hall read near-white): mid greys, not the palette's luminance. Floors #6E6E6E, everything
            // else #8E8E8E scaled by how light the palette colour is against the plaster (so trims and panels still differ).
            string n = twin.name.ToLowerInvariant();
            bool floor = n.Contains("marble") || n.Contains("terrazzo") || n.Contains("parquet") || n.Contains("floor") || n.Contains("ground");
            Color src = twin.GetColor("_BaseColor");
            float lum = src.r * 0.2126f + src.g * 0.7152f + src.b * 0.0722f;
            float g = floor ? 0x6E / 255f : Mathf.Clamp((0x8E / 255f) * Mathf.Clamp(lum / 0.8f, 0.55f, 1f), 0f, 1f);
            grey.SetColor("_BaseColor", new Color(g, g, g, 1f));
            foreach (var kw in twin.shaderKeywords) grey.EnableKeyword(kw);
            SkylineInkRoles.SetRole(grey, SkylineInkRoles.GetRole(twin));
            EditorUtility.SetDirty(grey);
            session.Count++;
            return grey;
        }

        /// <summary>Sets the ink role on a material: <c>_InkRole</c> and the <c>SkylineInkMask</c> pass on only when the role is
        /// above zero.</summary>
        public static void SetRole(Material m, int role) => SkylineInkRoles.SetRole(m, role);

        // ---------------------------------------------------------------------------------------------- the twins

        sealed class Session
        {
            public int Count;
            /// <summary>Source material (by instance) to its twin, and twin name to the source that owns it (two different
            /// materials with one name get separate twins).</summary>
            public readonly Dictionary<Material, Material> Done = new Dictionary<Material, Material>();
            public readonly Dictionary<string, string> NameOwner = new Dictionary<string, string>();
        }

        /// <summary>A signature of what a twin is made from, to tell two same-named materials apart.</summary>
        static string Signature(Material src)
        {
            var sb = new StringBuilder();
            sb.Append(src.shader != null ? src.shader.name : "");
            if (src.HasProperty(BaseColorId)) sb.Append(ColorUtility.ToHtmlStringRGBA(src.GetColor(BaseColorId)));
            if (src.HasProperty(BaseMapId) && src.GetTexture(BaseMapId) != null) sb.Append(src.GetTexture(BaseMapId).name);
            return sb.ToString();
        }

        static string Safe(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        /// <summary>The ink twin of <paramref name="src"/>: loaded from <see cref="TwinFolder"/> (created if missing) and
        /// refreshed from the source and the palette.</summary>
        static Material TwinFor(Material src, bool grey, Session session)
        {
            if (session.Done.TryGetValue(src, out var done)) return done;
            string baseName = Safe(src.name);
            string sig = Signature(src);
            string name = baseName;
            for (int k = 2; session.NameOwner.TryGetValue(name, out var owner) && owner != sig; k++) name = baseName + "_" + k;
            session.NameOwner[name] = sig;
            string path = TwinFolder + "INK_" + name + (grey ? "__Grey" : "") + ".mat";
            var twin = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (twin == null)
            {
                VhLookLibrary.EnsureAssetFolder(path);
                twin = new Material(InkShader) { name = "INK_" + name + (grey ? "__Grey" : "") };
                AssetDatabase.CreateAsset(twin, path);
            }
            Fill(src, twin, grey);
            session.Done[src] = twin;
            session.Count++;
            return twin;
        }

        /// <summary>Writes the twin's values from the source and the palette: colour and map (palette rules), normal map,
        /// emission, glow mode, gradient, hatching, rim, culling. Role 0 and the mask pass off.</summary>
        public static void Fill(Material src, Material dst, bool grey)
        {
            if (dst.shader != InkShader) dst.shader = InkShader;
            bool found = SkylineInkPalette.TryGet(src.name, out var e);
            bool srcUnlit = src.shader != null && src.shader.name == UnlitShader;
            bool glow = found ? e.emissive : srcUnlit;
            Color c = src.HasProperty(BaseColorId) ? src.GetColor(BaseColorId) : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            Texture map = src.HasProperty(BaseMapId) ? src.GetTexture(BaseMapId) : src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            bool keepTexture = glow || (found ? e.keepTexture : map != null);
            if (found && !e.keepTexture && !glow) c = e.color;
            if (!glow) c.a = 1f;
            dst.SetColor(BaseColorId, c);
            dst.SetTexture(BaseMapId, keepTexture ? map : null);
            if (keepTexture && map != null && src.HasProperty(BaseMapId))
            {
                dst.SetTextureScale(BaseMapId, src.GetTextureScale(BaseMapId));
                dst.SetTextureOffset(BaseMapId, src.GetTextureOffset(BaseMapId));
            }
            else
            {
                dst.SetTextureScale(BaseMapId, Vector2.one);
                dst.SetTextureOffset(BaseMapId, Vector2.zero);
            }

            // a normal map and emission stay (checked by the tests): the keywords follow the source
            bool normal = !glow && src.IsKeywordEnabled("_NORMALMAP") && src.HasProperty(BumpMapId) && src.GetTexture(BumpMapId) != null;
            dst.SetTexture(BumpMapId, normal ? src.GetTexture(BumpMapId) : null);
            if (normal && src.HasProperty(BumpScaleId)) dst.SetFloat(BumpScaleId, src.GetFloat(BumpScaleId));
            Keyword(dst, "_NORMALMAP", normal);
            bool emission = !glow && src.IsKeywordEnabled("_EMISSION") && src.HasProperty(EmissionColorId);
            if (emission)
            {
                dst.SetColor(EmissionColorId, src.GetColor(EmissionColorId));
                dst.SetTexture(EmissionMapId, src.HasProperty(EmissionMapId) ? src.GetTexture(EmissionMapId) : null);
            }
            Keyword(dst, "_EMISSION", emission);

            dst.SetFloat("_Unlit", glow ? 1f : 0f);
            dst.SetFloat("_GradBottom", glow ? 1f : found ? e.gradBottom : SkylineInkPalette.DefaultGradBottom);
            dst.SetFloat("_HatchStrength", glow ? 0f : found ? e.hatch : SkylineInkPalette.DefaultHatch);
            dst.SetFloat("_RimStrength", glow ? 0f : 0.25f);
            dst.SetFloat("_Desaturate", glow ? 0f : grey ? 1f : found ? e.desaturate : 0f);
            if (src.HasProperty(CullId)) dst.SetFloat(CullId, src.GetFloat(CullId));
            dst.enableInstancing = true;
            SkylineInkRoles.SetRole(dst, SkylineInkRoles.None);
            EditorUtility.SetDirty(dst);
        }

        static void Keyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        // ---------------------------------------------------------------------------------------------- Resources

        /// <summary>
        /// Moves the three Resources/SkylineFx materials onto InkLit and creates the four templates, saved to disk (the build
        /// includes everything in Resources, with the shader variants their keywords need). The bot paint (a normal-mapped
        /// PBR paint) and the acid (normal map and emission) take role 1: every bot and every acid puddle is hostile. The glow
        /// goes to glow mode with role 0; the code that makes a hostile bolt's or a Writ spot's glow sets its own role on its
        /// copy (SkylineBotLook.Glow). Idempotent.
        /// </summary>
        public static void EnsureResourceMaterials()
        {
            var ink = InkShader;
            if (ink == null) { Debug.LogError("SKYLINE_INK Skyline/InkLit not found; the Resources materials are unchanged"); return; }
            ConvertResource(ResourcesFolder + "SK_Bot_Paint.mat", ink, SkylineInkRoles.Hostile, false);
            ConvertResource(ResourcesFolder + "SK_Fx_Acid.mat", ink, SkylineInkRoles.Hostile, false);
            ConvertResource(ResourcesFolder + "SK_Emissive_FxGlow.mat", ink, SkylineInkRoles.None, true);
            Template(SkylineInkRoles.TemplatePlain, false, false, ink);
            Template(SkylineInkRoles.TemplateNormal, true, false, ink);
            Template(SkylineInkRoles.TemplateEmission, false, true, ink);
            Template(SkylineInkRoles.TemplateNormalEmission, true, true, ink);
            AssetDatabase.SaveAssets();
        }

        static void ConvertResource(string path, Shader ink, int role, bool glow)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { Debug.LogWarning("SKYLINE_INK missing " + path); return; }
            if (m.shader != ink)
            {
                // the values the source carries are read before the shader changes (the property names are shared)
                Color c = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                Texture map = m.HasProperty(BaseMapId) ? m.GetTexture(BaseMapId) : null;
                Texture bump = m.HasProperty(BumpMapId) ? m.GetTexture(BumpMapId) : null;
                bool normal = m.IsKeywordEnabled("_NORMALMAP");
                bool emission = m.IsKeywordEnabled("_EMISSION");
                Color em = m.HasProperty(EmissionColorId) ? m.GetColor(EmissionColorId) : Color.black;
                float bumpScale = m.HasProperty(BumpScaleId) ? m.GetFloat(BumpScaleId) : 1f;
                m.shader = ink;
                m.SetColor(BaseColorId, c);
                m.SetTexture(BaseMapId, map);
                m.SetTexture(BumpMapId, bump);
                m.SetFloat(BumpScaleId, bumpScale);
                m.SetColor(EmissionColorId, em);
                Keyword(m, "_NORMALMAP", normal);
                Keyword(m, "_EMISSION", emission);
            }
            m.SetFloat("_Unlit", glow ? 1f : 0f);
            m.SetFloat("_GradBottom", 1f);                          // painted metal and liquids: no wall gradient
            m.SetFloat("_HatchStrength", glow ? 0f : 0.08f);
            m.SetFloat("_RimStrength", glow ? 0f : 0.3f);
            m.enableInstancing = true;
            SkylineInkRoles.SetRole(m, role);
            EditorUtility.SetDirty(m);
        }

        static void Template(string name, bool normal, bool emission, Shader ink)
        {
            string path = ResourcesFolder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                VhLookLibrary.EnsureAssetFolder(path);
                m = new Material(ink) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != ink) m.shader = ink;
            Keyword(m, "_NORMALMAP", normal);
            Keyword(m, "_EMISSION", emission);
            m.SetColor(BaseColorId, Color.white);
            m.SetFloat("_Unlit", 0f);
            m.SetFloat("_GradBottom", 1f);
            m.SetFloat("_HatchStrength", 0.08f);
            m.SetFloat("_RimStrength", 0.3f);
            m.enableInstancing = true;
            SkylineInkRoles.SetRole(m, SkylineInkRoles.None);
            EditorUtility.SetDirty(m);
        }
    }
}
