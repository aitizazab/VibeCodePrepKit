using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.Look
{
    /// <summary>
    /// Volume override for Visiting Hours' atmosphere pass (<see cref="VhAtmosphereFeature"/>, shader JamKit/Look/Atmosphere):
    /// <list type="bullet">
    /// <item><b>Haze:</b> distance- and height-based aerial perspective. Thicker low down and far away, tinted warm towards
    /// the sun and cool away from it, so the far end of the hill goes soft and golden. Never applied to the sky.</item>
    /// <item><b>Sun shafts:</b> screen-space light shafts from the sun through gaps in silhouettes (leaves, eaves, fences).</item>
    /// </list>
    /// Inactive (the pass is skipped) unless haze density or shaft intensity is above zero.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("JamKit/VH Atmosphere")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class VhAtmosphere : VolumeComponent, IPostProcessComponent
    {
        [Header("Haze")]
        [Tooltip("Extinction per metre at the haze's base height (0 = off). 0.004 reads as golden-hour haze over 200 m.")]
        public MinFloatParameter hazeDensity = new MinFloatParameter(0f, 0f);
        [Tooltip("World height (m) where the haze is at full density; it thins upward from there.")]
        public FloatParameter hazeBaseHeight = new FloatParameter(0f);
        [Tooltip("How fast the haze thins with height (per metre). Higher = haze hugs the low ground.")]
        public MinFloatParameter hazeHeightFalloff = new MinFloatParameter(0.03f, 0f);
        [Tooltip("Distance (m) before the haze starts, keeping the near ground crisp.")]
        public MinFloatParameter hazeStart = new MinFloatParameter(12f, 0f);
        [Tooltip("The haze colour looking away from the sun (a soft cool blue-lilac).")]
        public ColorParameter hazeColor = new ColorParameter(new Color(0.62f, 0.68f, 0.82f), true, false, true);
        [Tooltip("The haze colour looking towards the sun (warm gold).")]
        public ColorParameter hazeSunColor = new ColorParameter(new Color(1.25f, 0.86f, 0.55f), true, false, true);
        [Tooltip("How tight the warm lobe around the sun is (higher = tighter).")]
        public ClampedFloatParameter hazeSunFocus = new ClampedFloatParameter(4f, 1f, 32f);
        [Tooltip("The most the haze can cover (keeps far silhouettes faintly visible).")]
        public ClampedFloatParameter hazeMaxOpacity = new ClampedFloatParameter(0.85f, 0f, 1f);

        [Header("Far haze (aerial perspective: the far hills and the terrain ring dissolve into the sky's horizon)")]
        [Tooltip("Extinction per metre beyond the far start (0 = off). 0.004 softens a hill 300 m off and dissolves 2 km.")]
        public MinFloatParameter farHazeDensity = new MinFloatParameter(0f, 0f);
        [Tooltip("Distance (m) where the far haze begins.")]
        public MinFloatParameter farHazeStart = new MinFloatParameter(150f, 0f);
        [Tooltip("How fast the far haze thins with height (per metre): gentle, so high hills still go soft.")]
        public MinFloatParameter farHazeHeightFalloff = new MinFloatParameter(0.003f, 0f);
        [Tooltip("The most the far haze can cover.")]
        public ClampedFloatParameter farHazeMaxOpacity = new ClampedFloatParameter(0.97f, 0f, 1f);

        [Header("Sun shafts")]
        [Tooltip("Strength of the shafts (0 = off).")]
        public MinFloatParameter shaftIntensity = new MinFloatParameter(0f, 0f);
        [Tooltip("Colour of the shafts (multiplied by the sky's brightness behind the occluders).")]
        public ColorParameter shaftTint = new ColorParameter(new Color(1f, 0.82f, 0.6f), true, false, true);
        [Tooltip("Brightness the sky must reach to cast shafts.")]
        public MinFloatParameter shaftThreshold = new MinFloatParameter(0.7f, 0f);
        [Tooltip("How far across the screen the shafts reach (0-1 of the way to the sun).")]
        public ClampedFloatParameter shaftLength = new ClampedFloatParameter(0.6f, 0.05f, 1f);
        [Tooltip("Screen radius around the sun that can emit shafts.")]
        public ClampedFloatParameter shaftRadius = new ClampedFloatParameter(0.45f, 0.05f, 2f);

        public bool IsActive() => hazeDensity.value > 0f || farHazeDensity.value > 0f || shaftIntensity.value > 0f;
        public bool HazeActive => hazeDensity.value > 0f || farHazeDensity.value > 0f;
        public bool ShaftsActive => shaftIntensity.value > 0f;
    }
}
