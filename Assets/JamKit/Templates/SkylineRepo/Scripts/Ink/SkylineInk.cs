using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JamKit.SkylineRepo
{
    /// <summary>
    /// Volume override for Skyline's comic-ink look (<see cref="SkylineInkFeature"/>, shader Hidden/Skyline/InkComposite;
    /// docs/SKYLINE-STYLE.md). The renderer feature lives on the shared PC renderer, so it does nothing unless this override is
    /// <see cref="inkEnabled"/> in the volume stack: Visiting Hours and the other templates never turn it on.
    /// <list type="bullet">
    /// <item><b>Thin ink:</b> scenery lines from depth and normal edges, about <see cref="lineWidthPx"/> px at 1080p, fading
    /// with distance from <see cref="fadeStart"/> to <see cref="fadeEnd"/> metres down to <see cref="farInk"/> of their strength.</item>
    /// <item><b>Thick outlines:</b> <see cref="outlineWidthPx"/> px at 1080p round the objects whose material has an ink role
    /// (hostile, objective, scrap, weak spot; the viewmodel gets a thin one in the ink colour).</item>
    /// </list>
    /// Widths are given at 1080p and scale with the render height, so the look holds at 1440p.
    /// </summary>
    [Serializable]
    [VolumeComponentMenu("JamKit/Skyline Ink")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class SkylineInk : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Turns the comic-ink pass on. Off by default so the shared renderer is untouched for other templates. (Named inkEnabled, not active: VolumeComponent already has a bool active, and a same-named field would hide it and clash in serialisation.)")]
        public BoolParameter inkEnabled = new BoolParameter(false);
        [Tooltip("The scenery line colour (sRGB, #16141F).")]
        public ColorParameter inkColor = new ColorParameter(new Color(0.0863f, 0.0784f, 0.1216f, 1f), false, false, false);
        [Tooltip("Scenery line width in pixels at 1080p.")]
        public ClampedFloatParameter lineWidthPx = new ClampedFloatParameter(1.5f, 0.5f, 4f);
        [Tooltip("Thick outline width in pixels at 1080p (constant on screen).")]
        public ClampedFloatParameter outlineWidthPx = new ClampedFloatParameter(4f, 1f, 8f);
        [Tooltip("Distance (m) where the scenery lines start to fade.")]
        public FloatParameter fadeStart = new FloatParameter(20f);
        [Tooltip("Distance (m) where the scenery lines reach their faintest.")]
        public FloatParameter fadeEnd = new FloatParameter(140f);
        [Tooltip("The fraction of a line's strength left at fadeEnd (the far city reads as soft shapes, not wire).")]
        public ClampedFloatParameter farInk = new ClampedFloatParameter(0.3f, 0f, 1f);
        [Tooltip("Relative depth jump (against the nearer neighbour's distance) that makes a silhouette line.")]
        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.03f, 0.002f, 0.5f);
        [Tooltip("1 - dot(normal, neighbour normal) that makes a crease line.")]
        public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.35f, 0.02f, 1.5f);

        public bool IsActive() => inkEnabled.value;
    }
}
