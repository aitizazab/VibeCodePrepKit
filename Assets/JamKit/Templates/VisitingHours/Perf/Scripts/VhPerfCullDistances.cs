using UnityEngine;

namespace JamKit.Perf
{
    /// <summary>
    /// Per-layer cull distances for the scene's cameras (Camera.layerCullDistances): renderers on a listed layer are not
    /// drawn beyond its distance (times the tier's <see cref="VhPerfTier.cullDistanceScale"/>). A builder adds one to the
    /// scene (<c>VhPerfCull.AddLayerDistances</c> in the editor) and puts small things on the layers: "PerfSmall" (cups,
    /// cans, toys, flowers: 25 m), "PerfDetail" (kerb litter, decals, wires: 60 m). Layers that don't exist in the project are
    /// skipped (VhPerfSetup adds the two). Screen-size culling with a LODGroup (<c>VhPerfLod.CullWhenSmall</c>) does the same
    /// for objects that can't change layer.
    /// <para>No per-frame allocation: the distances array is made once and set again only when the main camera changes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VhPerfCullDistances : MonoBehaviour
    {
        public const string SmallLayer = "PerfSmall", DetailLayer = "PerfDetail";

        [Tooltip("Layer names, matched by index with distances.")]
        public string[] layers = { SmallLayer, DetailLayer };
        [Tooltip("Metres beyond which renderers on the layer are culled (0: never).")]
        public float[] distances = { 25f, 60f };
        [Tooltip("Spherical distance (from the camera) instead of along its view axis: no popping when turning.")]
        public bool spherical = true;

        readonly float[] _d = new float[32];
        Camera _applied;
        float _scale = -1f;

        void OnEnable()
        {
            _applied = null;
            Build();
        }

        void Build()
        {
            for (int i = 0; i < _d.Length; i++) _d[i] = 0f;
            _scale = VhPerfTiers.Active ? VhPerfTiers.Current.cullDistanceScale : 1f;
            int n = Mathf.Min(layers != null ? layers.Length : 0, distances != null ? distances.Length : 0);
            for (int i = 0; i < n; i++)
            {
                int layer = LayerMask.NameToLayer(layers[i]);
                if (layer < 0 || distances[i] <= 0f) continue;
                _d[layer] = distances[i] * _scale;
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            float scale = VhPerfTiers.Active ? VhPerfTiers.Current.cullDistanceScale : 1f;
            if (scale != _scale) { Build(); _applied = null; }
            if (cam == null || cam == _applied) return;
            cam.layerCullDistances = _d;
            cam.layerCullSpherical = spherical;
            _applied = cam;
        }
    }
}
