// Skyline/InkLit: the material's inputs, shared by every pass (docs/SKYLINE-STYLE.md, "How it's built").
//
// One UnityPerMaterial CBUFFER included by all five passes, or the SRP Batcher breaks (URP's LitInput.hlsl does the same:
// "do not ifdef the properties here"). The property names of URP Lit that the project's code already sets (_BaseColor,
// _BaseMap, _BumpMap, _BumpScale, _EmissionColor, _EmissionMap) are kept, so `material.color`, `SetColor("_EmissionColor")`
// and the _NORMALMAP / _EMISSION keywords work on an InkLit material exactly as on a Lit one.
#ifndef SKYLINE_INK_LIT_INPUT_INCLUDED
#define SKYLINE_INK_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
// Declares _BaseMap, _BumpMap and _EmissionMap with their samplers, and SampleAlbedoAlpha / SampleNormal / SampleEmission
// (the last two honour _NORMALMAP and _EMISSION).
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _EmissionColor;
half _BumpScale;
half _GradBottom;       // the wall's brightness at _GradFloorY (1 = no gradient)
half _GradFloorY;       // world height where the gradient starts
half _GradHeight;       // metres the gradient takes to reach full brightness
half _Desaturate;       // 0..1: the colour-free zones
half _HatchStrength;    // how dark the deep-shade hatching is
half _InkRole;          // 0 none, 1 hostile, 2 objective, 3 scrap (strippable wreck), 4 viewmodel, 5 weak spot, 6 bot weak point, 7 objective x-ray only
half _InkWidth;         // role 3 only: the wreck outline's width as a fraction of the full thick outline (0.4..1), set per width step by SkylineInkRoles.FromLit
half _Unlit;            // 1: a glow (the colour and map as they are, no lighting): the Unlit materials' twins
half _RimStrength;
// URP's Lit keeps this in its buffer too; the debug-view macro is empty in a normal build.
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

/// The surface colour: painted map (white when none) x base colour, desaturated by _Desaturate towards its luminance, times
/// the vertical gradient (Team Fortress 2's trick: walls darken towards the floor and lighten towards the top).
half3 InkAlbedo(float2 uv, float3 posWS)
{
    half3 c = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb * _BaseColor.rgb;
    half lum = dot(c, half3(0.2126h, 0.7152h, 0.0722h));
    c = lerp(c, lum.xxx, saturate(_Desaturate));
    half grad = lerp(_GradBottom, 1.05h, saturate((posWS.y - _GradFloorY) / max(_GradHeight, 0.001h)));
    return c * grad;
}

#endif
