using UnityEngine;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// The ink roles (docs/SKYLINE-STYLE.md rule 2) and the run-time half of the InkLit conversion. A role is a material's
    /// <c>_InkRole</c> number plus the <c>SkylineInkMask</c> pass switched on: the pass draws the object's visible silhouette
    /// into the mask, and <see cref="SkylineInkFeature"/> turns the mask into the thick outline in the role's hue.
    /// <list type="bullet">
    /// <item><see cref="Hostile"/> hot red-magenta, <see cref="Objective"/> cyan, <see cref="Scrap"/> green, <see cref="Viewmodel"/>
    /// the thin ink line round the guns, <see cref="WeakSpot"/> yellow: its own outline, drawn over a hostile body's.</item>
    /// <item>Weak spots sit in <see cref="WeakQueue"/> and the feature draws that queue into the mask last, so where a weak-spot
    /// part and its hostile body overlap in depth, the yellow overwrites the red-magenta.</item>
    /// </list>
    /// <para>Build rule: instances made at run time must come from build-included materials, or the build strips the shader
    /// variant they need (the invisible-bots lesson). <see cref="FromLit"/> therefore clones one of four InkLit templates in
    /// <c>Resources/SkylineFx</c> (the editor makes them: SkylineInkConvert.EnsureResourceMaterials), one per combination of
    /// the normal-map and emission keywords, and never turns a keyword on by itself.</para>
    /// </summary>
    public static class SkylineInkRoles
    {
        public const int None = 0, Hostile = 1, Objective = 2, Scrap = 3, Viewmodel = 4, WeakSpot = 5, BotWeakPoint = 6;

        public const string ShaderName = "Skyline/InkLit";
        /// <summary>The pass name and its LightMode tag (the same string, so SetShaderPassEnabled matches either way).</summary>
        public const string MaskPass = "SkylineInkMask";
        /// <summary>The render queue of weak-spot materials: inside the opaque range, after every ordinary opaque.</summary>
        public const int WeakQueue = 2050;
        /// <summary>The render queue of a bot's own weak-point parts (role 6): after the ordinary roles, before the Writ's spots (5 over 6 over 1).</summary>
        public const int BotWeakQueue = 2040;

        public const string TemplateFolder = "SkylineFx/";
        public const string TemplatePlain = "SK_Ink_Plain", TemplateNormal = "SK_Ink_Normal",
            TemplateEmission = "SK_Ink_Emission", TemplateNormalEmission = "SK_Ink_NormalEmission";

        static readonly int RoleId = Shader.PropertyToID("_InkRole");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int CullId = Shader.PropertyToID("_Cull");
        static readonly int UnlitId = Shader.PropertyToID("_Unlit");

        /// <summary>The role colour as the style sheet gives it (sRGB), for tests and docs.</summary>
        public static Color Hue(int role)
        {
            switch (role)
            {
                case Hostile: return new Color32(0xFF, 0x2E, 0x63, 255);
                case Objective: return new Color32(0x2E, 0xE6, 0xFF, 255);
                case Scrap: return new Color32(0x5C, 0xFF, 0x8A, 255);
                case Viewmodel: return new Color32(0x16, 0x14, 0x1F, 255);
                case WeakSpot: return new Color32(0xFF, 0xD6, 0x0A, 255);
                case BotWeakPoint: return new Color32(0xFF, 0x2E, 0x63, 255);   // pulsed towards yellow by the composite
                default: return Color.clear;
            }
        }

        /// <summary>A plain flat-colour InkLit material in <paramref name="c"/> with <paramref name="role"/> (the bots'
        /// primitive parts, the mines, the spot marker's gap and wiring); a clone of the plain template, so it is in the build.
        /// Null when the templates aren't in Resources.</summary>
        public static Material Flat(Color c, int role)
        {
            var template = Resources.Load<Material>(TemplateFolder + TemplatePlain);
            if (template == null) return null;
            var m = new Material(template) { name = "Ink flat" };
            m.SetColor(BaseColorId, c);
            SetRole(m, role);
            return m;
        }

        /// <summary>Sets a material's role: <c>_InkRole</c>, the mask pass on only when the role is above zero, and the weak
        /// spot's later queue. Works on an InkLit material; any other material is left alone (it has no such pass).</summary>
        public static void SetRole(Material m, int role)
        {
            if (m == null || !m.HasProperty(RoleId)) return;
            m.SetFloat(RoleId, role);
            m.SetShaderPassEnabled(MaskPass, role > 0);
            if (role == WeakSpot) m.renderQueue = WeakQueue;
            else if (role == BotWeakPoint) m.renderQueue = BotWeakQueue;
            else if (m.renderQueue == WeakQueue || m.renderQueue == BotWeakQueue) m.renderQueue = -1;
        }

        public static int GetRole(Material m) => m != null && m.HasProperty(RoleId) ? Mathf.RoundToInt(m.GetFloat(RoleId)) : None;

        /// <summary>True when the material is on the InkLit shader.</summary>
        public static bool IsInk(Material m) => m != null && m.shader != null && m.shader.name == ShaderName;

        /// <summary>The Resources name of the template with exactly these two keywords.</summary>
        public static string TemplateName(bool normalMap, bool emission) =>
            normalMap ? (emission ? TemplateNormalEmission : TemplateNormal) : (emission ? TemplateEmission : TemplatePlain);

        /// <summary>
        /// A new InkLit material from a (usually URP Lit) material: a clone of the matching build-included template with the
        /// source's colour, painted map, normal map and emission copied across, and <paramref name="role"/> set. A material that
        /// is already InkLit is cloned as it is. Null when the templates aren't in Resources (the caller keeps the source).
        /// One shared material per (source, role): bots of one kind share their materials, so the SRP Batcher keeps batching.
        /// </summary>
        public static Material FromLit(Material src, int role)
        {
            if (src == null) return null;
            var key = (src.GetInstanceID(), role);
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var made = MakeFromLit(src, role);
            if (made != null) _cache[key] = made;
            return made;
        }

        static readonly System.Collections.Generic.Dictionary<(int, int), Material> _cache = new System.Collections.Generic.Dictionary<(int, int), Material>();

        static Material MakeFromLit(Material src, int role)
        {
            if (src == null) return null;
            Material clone;
            if (IsInk(src)) clone = new Material(src);
            else
            {
                bool normal = src.IsKeywordEnabled("_NORMALMAP") && src.HasProperty(BumpMapId) && src.GetTexture(BumpMapId) != null;
                bool emission = src.IsKeywordEnabled("_EMISSION") && src.HasProperty(EmissionColorId);
                var template = Resources.Load<Material>(TemplateFolder + TemplateName(normal, emission));
                if (template == null) return null;
                clone = new Material(template);
                CopyLook(src, clone, normal, emission);
                if (src.shader != null && src.shader.name.Contains("Unlit")) clone.SetFloat(UnlitId, 1f);   // a glow stays a glow
            }
            clone.name = src.name + " (ink)";
            SetRole(clone, role);
            return clone;
        }

        /// <summary>Copies colour, map, normal map and emission from a Lit/Unlit/SimpleLit material onto an InkLit one. The keywords
        /// the target carries were fixed by the template; only values are copied here.</summary>
        public static void CopyLook(Material src, Material dst, bool normal, bool emission)
        {
            Color c = src.HasProperty(BaseColorId) ? src.GetColor(BaseColorId) : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            dst.SetColor(BaseColorId, c);
            Texture map = src.HasProperty(BaseMapId) ? src.GetTexture(BaseMapId) : src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            dst.SetTexture(BaseMapId, map);
            if (map != null && src.HasProperty(BaseMapId))
            {
                dst.SetTextureScale(BaseMapId, src.GetTextureScale(BaseMapId));
                dst.SetTextureOffset(BaseMapId, src.GetTextureOffset(BaseMapId));
            }
            if (normal)
            {
                dst.SetTexture(BumpMapId, src.GetTexture(BumpMapId));
                if (src.HasProperty(BumpScaleId)) dst.SetFloat(BumpScaleId, src.GetFloat(BumpScaleId));
            }
            if (emission)
            {
                dst.SetColor(EmissionColorId, src.GetColor(EmissionColorId));
                dst.SetTexture(EmissionMapId, src.HasProperty(EmissionMapId) ? src.GetTexture(EmissionMapId) : null);
            }
            if (src.HasProperty(CullId) && dst.HasProperty(CullId)) dst.SetFloat(CullId, src.GetFloat(CullId));
        }

        /// <summary>Gives every renderer under <paramref name="root"/> an InkLit copy of each of its materials, with
        /// <paramref name="role"/>. Returns how many materials were swapped. Used on run-time-built bodies and the viewmodel.</summary>
        public static int Apply(GameObject root, int role)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var ink = FromLit(mats[i], role);
                    if (ink == null) continue;
                    mats[i] = ink;
                    changed = true;
                    n++;
                }
                if (changed) r.sharedMaterials = mats;
            }
            return n;
        }
    }
}
