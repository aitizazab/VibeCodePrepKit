using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// Art style E's waking world in 3D: the muted palette, one material per colour (copies of the
    /// base material, shader JamKit/VisitingHours/Waking), the scene's hemisphere ambient and haze, and
    /// restyling a model's own materials (Kenney's are named wood, carpet, metal...) into the palette.
    /// Reusable for every waking-world scene: <c>var style = new VhWakingStyle(baseMaterial)</c>, then
    /// <c>style.Mat(VhWakingStyle.Wood)</c>, <c>style.Restyle(model)</c>, and <see cref="SetAmbient"/>.
    /// Dispose() destroys the copies.
    /// </summary>
    public sealed class VhWakingStyle
    {
        // The palette: muted, desaturated, a little cold (docs/art/prompts/styles/style-E-*.txt).
        public static readonly Color Wall = new Color(0.56f, 0.58f, 0.60f);
        public static readonly Color Ceiling = new Color(0.66f, 0.67f, 0.68f);
        public static readonly Color Floor = new Color(0.40f, 0.33f, 0.28f);
        public static readonly Color Wood = new Color(0.45f, 0.35f, 0.28f);
        public static readonly Color WoodDark = new Color(0.30f, 0.24f, 0.21f);
        public static readonly Color WoodPale = new Color(0.62f, 0.52f, 0.42f);
        public static readonly Color Sheet = new Color(0.76f, 0.76f, 0.74f);
        public static readonly Color Blanket = new Color(0.42f, 0.47f, 0.55f);
        public static readonly Color BlanketDark = new Color(0.32f, 0.35f, 0.42f);
        public static readonly Color Pillow = new Color(0.80f, 0.79f, 0.76f);
        public static readonly Color Metal = new Color(0.52f, 0.53f, 0.56f);
        public static readonly Color MetalDark = new Color(0.25f, 0.26f, 0.29f);
        public static readonly Color Lampshade = new Color(0.80f, 0.75f, 0.64f);
        public static readonly Color Plant = new Color(0.38f, 0.46f, 0.39f);
        public static readonly Color Skin = new Color(0.80f, 0.66f, 0.58f);
        public static readonly Color Thermal = new Color(0.50f, 0.51f, 0.54f);
        public static readonly Color Scrubs = new Color(0.20f, 0.25f, 0.38f);
        public static readonly Color Hair = new Color(0.22f, 0.16f, 0.13f);
        public static readonly Color PhoneBody = new Color(0.14f, 0.15f, 0.17f);
        public static readonly Color Mug = new Color(0.78f, 0.74f, 0.66f);
        public static readonly Color Coffee = new Color(0.36f, 0.24f, 0.17f);
        public static readonly Color Paper = new Color(0.86f, 0.84f, 0.78f);
        public static readonly Color Lanyard = new Color(0.20f, 0.27f, 0.42f);
        public static readonly Color Outside = new Color(0.08f, 0.09f, 0.13f);
        public static readonly Color Rug = new Color(0.46f, 0.40f, 0.40f);

        /// <summary>A Kenney material name's place in the palette (unknown names: their own colour, muted).</summary>
        public static bool PaletteFor(string materialName, out Color c)
        {
            string n = materialName ?? "";
            int space = n.IndexOf(' ');
            if (space > 0) n = n.Substring(0, space);
            switch (n)
            {
                case "wood": c = Wood; return true;
                case "woodDark": c = WoodDark; return true;
                case "carpet": c = Blanket; return true;
                case "carpetWhite": c = Sheet; return true;
                case "carpetDarker": c = BlanketDark; return true;
                case "metal": c = Metal; return true;
                case "metalDark": c = MetalDark; return true;
                case "lamp": c = Lampshade; return true;
                case "plant": c = Plant; return true;
                case "_defaultMat": c = WoodPale; return true;
                default: c = Color.gray; return false;
            }
        }

        /// <summary>Any colour pulled into style E: half its saturation, a little darker and cooler.</summary>
        public static Color Mute(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var m = Color.HSVToRGB(h, s * 0.5f, v * 0.85f);
            return new Color(m.r * 0.97f, m.g * 0.98f, m.b * 1.02f, 1f);
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int VertexColorId = Shader.PropertyToID("_VertexColorWeight");
        static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        public static readonly int HighlightId = Shader.PropertyToID("_Highlight");
        public static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SkyId = Shader.PropertyToID("_VHWakeSky");
        static readonly int GroundId = Shader.PropertyToID("_VHWakeGround");
        static readonly int FogId = Shader.PropertyToID("_VHWakeFog");
        static readonly int ShId = Shader.PropertyToID("_VHWakeSH");

        readonly Material _base;
        readonly Dictionary<long, Material> _cache = new Dictionary<long, Material>();
        readonly List<Material> _made = new List<Material>();

        public VhWakingStyle(Material baseMaterial)
        {
            _base = baseMaterial;
            if (_base == null)
            {
                var shader = Shader.Find("JamKit/VisitingHours/Waking");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader != null) _base = new Material(shader) { name = "VH Waking (made in code)" };
                if (_base != null) _made.Add(_base);
            }
        }

        public bool HasShader => _base != null && _base.shader != null && _base.shader.name == "JamKit/VisitingHours/Waking";

        /// <summary>The material for a colour (and an optional texture), shared by everything with the same look.</summary>
        public Material Mat(Color c, Texture tex = null, float outline = -1f, bool vertexColors = false)
        {
            long key = ((long)Mathf.RoundToInt(c.r * 255f) << 24) | ((long)Mathf.RoundToInt(c.g * 255f) << 16) | ((long)Mathf.RoundToInt(c.b * 255f) << 8);
            key ^= (tex != null ? (long)tex.GetInstanceID() << 32 : 0L);
            key ^= (long)Mathf.RoundToInt((outline + 1f) * 37f) << 40;
            if (vertexColors) key ^= 1L << 62;
            if (_cache.TryGetValue(key, out var m) && m != null) return m;
            m = Copy(c, tex);
            if (outline >= 0f && m.HasProperty(OutlineWidthId)) m.SetFloat(OutlineWidthId, outline);
            if (vertexColors && m.HasProperty(VertexColorId)) m.SetFloat(VertexColorId, 1f);
            _cache[key] = m;
            return m;
        }

        /// <summary>A material of its own (not shared): screens, the things that fade or glow one at a time.</summary>
        public Material Unique(Color c, Texture tex = null, string name = null)
        {
            var m = Copy(c, tex);
            if (name != null) m.name = name;
            return m;
        }

        /// <summary>A glowing screen: black body colour, the picture as emission.</summary>
        public Material Screen(Texture picture, float brightness, string name)
        {
            var m = Copy(new Color(0.02f, 0.02f, 0.03f), null);
            m.name = name;
            if (m.HasProperty(EmissionMapId)) m.SetTexture(EmissionMapId, picture != null ? picture : Texture2D.whiteTexture);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, Color.white * brightness);
            if (m.HasProperty(OutlineWidthId)) m.SetFloat(OutlineWidthId, 0f);
            if (m.HasProperty(RimStrengthId)) m.SetFloat(RimStrengthId, 0f);
            return m;
        }

        public static void SetEmission(Material m, Color c)
        {
            if (m != null && m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, c);
        }

        Material Copy(Color c, Texture tex)
        {
            var m = _base != null ? new Material(_base) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            m.name = "VH " + ColorUtility.ToHtmlStringRGB(c);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            else m.color = c;
            if (tex != null && m.HasProperty(BaseMapId)) m.SetTexture(BaseMapId, tex);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, Color.black);
            _made.Add(m);
            return m;
        }

        /// <summary>Replaces every renderer's materials under <paramref name="root"/> with palette ones (by
        /// the original material's name, or its colour muted), so imported models match the room.</summary>
        public void Restyle(GameObject root, float outline = -1f)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var shared = r.sharedMaterials;
                var next = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    Color c;
                    if (src == null) c = WoodPale;
                    else if (!PaletteFor(src.name, out c))
                    {
                        Color orig = src.HasProperty(BaseColorId) ? src.GetColor(BaseColorId) : (src.HasProperty("_Color") ? src.color : Color.gray);
                        c = Mute(orig);
                    }
                    next[i] = Mat(c, null, outline);
                }
                r.sharedMaterials = next;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
        }

        /// <summary>The waking world's sky and ground light (shade side), its haze (fog a = metres to full haze, 0 = none)
        /// and how much of Unity's own ambient (the scene's lighting settings) to add.</summary>
        public static void SetAmbient(Color sky, Color ground, Color fog, float fogDistance, float unityAmbient = 0.15f)
        {
            Shader.SetGlobalFloat(ShId, Mathf.Max(0f, unityAmbient));
            Shader.SetGlobalColor(SkyId, sky);
            Shader.SetGlobalColor(GroundId, ground);
            Shader.SetGlobalColor(FogId, new Color(fog.r, fog.g, fog.b, Mathf.Max(0f, fogDistance)));
        }

        public void Dispose()
        {
            foreach (var m in _made)
                VhUtil.Kill(m);
            _made.Clear();
            _cache.Clear();
        }
    }
}
