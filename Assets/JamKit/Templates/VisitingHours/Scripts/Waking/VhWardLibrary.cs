using System;
using System.Collections.Generic;
using UnityEngine;

namespace JamKit.VisitingHours
{
    /// <summary>
    /// The ward's PBR materials (URP/Lit, each with an albedo, an OpenGL normal map and a packed mask: metallic, occlusion,
    /// smoothness) and the sky seen through its windows. Made in the editor by <c>VhWardLibraryBuilder</c> (JamKit > Visiting
    /// Hours > Build the Ward Materials, and every Rebuild Shell Scenes) from <c>Templates/VisitingHours/Ward/Textures</c>
    /// (CC0, ambientCG and Poly Haven: <c>Ward/LICENSES.md</c>), kept as assets so a build keeps their shader variants.
    /// The ward asks for a surface by key ("floor", "wall"...); <see cref="Tinted"/> gives a tinted copy for one colour.
    /// docs/redesign/day.md, "The look".
    /// </summary>
    [CreateAssetMenu(menuName = "JamKit/Visiting Hours/Ward Library", fileName = "VhWardLibrary")]
    public sealed class VhWardLibrary : ScriptableObject
    {
        public const string ResourcePath = "JamKit/VhWardLibrary";

        [Serializable]
        public class Surface
        {
            public string key = "";
            public Material material;
            [Tooltip("Metres one repeat of the texture covers (the meshes' UVs are in metres).")]
            public float metresPerTile = 1f;
            [Tooltip("The albedo's mean colour (sRGB): Colour() divides by it, so a surface comes out the colour asked for.")]
            public Color mean = Color.white;
        }

        public List<Surface> surfaces = new List<Surface>();
        [Tooltip("The sky through the windows (Skybox/Panoramic with the HDRI).")]
        public Material sky;
        [Tooltip("The ward's printed and handwritten pictures (signs, notices, the round board, the TV, the remembrance book, the monitor's digits), from Ward/Textures/Pictures.")]
        public List<Texture2D> pictures = new List<Texture2D>();

        /// <summary>A picture by its file name (sign_ward, board_day1...), or null.</summary>
        public Texture2D Picture(string name)
        {
            foreach (var t in pictures)
                if (t != null && t.name == name) return t;
            return null;
        }

        static VhWardLibrary _loaded;
        static bool _tried;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = null;
            _tried = false;
        }

        /// <summary>The library in the build, or null (then the ward keeps its style-E colours and says so once).</summary>
        public static VhWardLibrary Load()
        {
            if (_tried) return _loaded;
            _tried = true;
            _loaded = Resources.Load<VhWardLibrary>(ResourcePath);
            if (_loaded == null) Debug.LogWarning("JAMKIT_VH ward: no " + ResourcePath + " (run JamKit > Visiting Hours > Build the Ward Materials): flat colours stand in");
            return _loaded;
        }

        public Surface Find(string key)
        {
            foreach (var s in surfaces)
                if (s != null && s.key == key && s.material != null) return s;
            return null;
        }

        readonly Dictionary<long, Material> _tints = new Dictionary<long, Material>();

        /// <summary>
        /// The surface <paramref name="key"/> coming out about <paramref name="colour"/> on average (its texture's detail kept:
        /// the tint is the colour over the albedo's mean), one material per key and colour, owned by <paramref name="owner"/>
        /// (destroyed with the place). Null if the key isn't in the library.
        /// </summary>
        public Material Colour(string key, Color colour, List<Material> owner)
        {
            var s = Find(key);
            if (s == null) return null;
            long id = ((long)key.GetHashCode() << 32) ^ (uint)colour.GetHashCode();
            if (_tints.TryGetValue(id, out var m) && m != null) return m;
            m = new Material(s.material) { name = s.material.name + " (" + ColorUtility.ToHtmlStringRGB(colour) + ")" };
            var mean = s.mean;
            m.SetColor(BaseColorId, new Color(Div(colour.r, mean.r), Div(colour.g, mean.g), Div(colour.b, mean.b), 1f));
            _tints[id] = m;
            owner?.Add(m);
            return m;
        }

        static float Div(float c, float mean) => Mathf.Clamp(c / Mathf.Max(0.05f, mean), 0f, 2.2f);

        /// <summary>Forget tinted copies (a place was taken down and destroyed them).</summary>
        public void ForgetTints() => _tints.Clear();

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    }
}
