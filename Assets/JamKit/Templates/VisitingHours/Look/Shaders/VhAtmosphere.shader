// Visiting Hours' atmosphere pass (VhAtmosphereFeature + the VhAtmosphere volume override): height/distance haze and
// screen-space sun shafts. URP-standard HLSL: Core.hlsl, core's Blit.hlsl (fullscreen triangle, _BlitTexture) and
// DeclareDepthTexture.hlsl.
//   Pass 0  shaft mask (quarter res, pass 3: the Perf agent's list): bright sky pixels near the sun.
//   Pass 1  radial blur towards the sun, 8 taps (run twice: long step, then short).
//   Pass 2  composite (full res): haze on geometry (never on the sky), plus the shafts.
// Texture coordinates follow Unity's render-texture layout (v = 0 at the image's bottom), which is also the viewport's, so
// the sun's viewport position from the camera can be compared with them directly.
Shader "JamKit/Look/Atmosphere"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D(_VhShaftTex);
        float4 _VhSunScreen;      // viewport xy of the sun, z = dot(camera forward, to sun), w = aspect
        float4 _VhAtmoSunDir;     // world direction to the sun
        float4 _VhHazeParams;     // density, base height, height falloff, start distance
        // Pass 4: the camera, passed by the feature itself (round 3's captures showed no haze at all: the pass's own
        // UNITY_MATRIX_I_VP / _WorldSpaceCameraPos weren't the camera's inside the full-screen draw, so every pixel came out
        // a few metres away). Position; right, up and forward axes (w = tan of the half FOV across, up); and the depth
        // buffer's own linearisation (Unity's _ZBufferParams layout).
        float4 _VhCamPos;
        float4 _VhCamRight;
        float4 _VhCamUp;
        float4 _VhCamFwd;
        float4 _VhZParams;
        float4 _VhHazeParams2;    // sun focus, max opacity
        float4 _VhFarHazeParams;  // far density, far start, far height falloff, far max opacity
        float4 _VhHazeColor;
        float4 _VhHazeSunColor;
        float4 _VhShaftParams;    // intensity, threshold, length, radius
        float4 _VhShaftTint;
        float _VhBlurStep;

        float VhLum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

        bool VhIsSky(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return rawDepth <= 1e-6;
        #else
            return rawDepth >= 1.0 - 1e-6;
        #endif
        }

        half4 FragMask(Varyings i) : SV_Target
        {
            float2 uv = i.texcoord;
            float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            float raw = SampleSceneDepth(uv);
            float sky = VhIsSky(raw) ? 1.0 : 0.0;
            float lum = VhLum(c);
            float t = _VhShaftParams.y;
            float bright = saturate((lum - t) / max(t, 1e-3));
            float2 d = uv - _VhSunScreen.xy;
            d.x *= _VhSunScreen.w;
            float fall = saturate(1.0 - length(d) / _VhShaftParams.w);
            fall *= fall;
            // Clamp so the sun disc itself doesn't dominate the streaks.
            float3 m = min(c, 4.0) * sky * bright * fall;
            return half4(m, 1.0);
        }

        half4 FragBlur(Varyings i) : SV_Target
        {
            const int N = 8;
            float2 uv = i.texcoord;
            float2 stepv = (_VhSunScreen.xy - uv) * _VhShaftParams.z * _VhBlurStep / N;
            float3 acc = 0;
            float w = 0, wt = 1.0;
            [unroll]
            for (int k = 0; k < N; k++)
            {
                acc += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + stepv * k, 0).rgb * wt;
                w += wt;
                wt *= 0.86;
            }
            return half4(acc / w, 1.0);
        }

        half4 FragComposite(Varyings i) : SV_Target
        {
            float2 uv = i.texcoord;
            float4 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
            float raw = SampleSceneDepth(uv);
            if (!VhIsSky(raw) && (_VhHazeParams.x > 0.0 || _VhFarHazeParams.x > 0.0))
            {
                // Eye depth from the raw depth, the view ray from the viewport position (v = 0 at the bottom).
                float eye = 1.0 / (_VhZParams.z * raw + _VhZParams.w);
                float2 ndc = uv * 2.0 - 1.0;
                float3 ray = _VhCamFwd.xyz + _VhCamRight.xyz * (ndc.x * _VhCamRight.w) + _VhCamUp.xyz * (ndc.y * _VhCamUp.w);
                float dist = eye * length(ray);
                float3 dir = normalize(ray);
                float d = max(dist - _VhHazeParams.w, 0.0);
                // Exponential height fog, integrated along the ray: density(h) = D * exp(-f (h - base)).
                float f = max(_VhHazeParams.z, 1e-4);
                float h0 = max(_VhCamPos.y + dir.y * _VhHazeParams.w - _VhHazeParams.y, -40.0);
                float k = f * dir.y * d;
                float shape = abs(k) > 1e-3 ? (1.0 - exp(-k)) / k : 1.0;
                float optical = _VhHazeParams.x * exp(-f * h0) * shape * d;
                float amount = min(1.0 - exp(-optical), _VhHazeParams2.y);
                // Far haze: the same integral with its own start and a gentle height falloff, so it grows steadily with
                // distance (the hill 300 m off goes soft, the ring at 2-3 km dissolves into the horizon's colour).
                float d2 = max(dist - _VhFarHazeParams.y, 0.0);
                float f2 = max(_VhFarHazeParams.z, 1e-5);
                float h2 = max(_VhCamPos.y + dir.y * _VhFarHazeParams.y - _VhHazeParams.y, -40.0);
                float k2 = f2 * dir.y * d2;
                float shape2 = abs(k2) > 1e-3 ? (1.0 - exp(-k2)) / k2 : 1.0;
                float optical2 = _VhFarHazeParams.x * exp(-f2 * max(h2, 0.0)) * shape2 * d2;
                float amount2 = min(1.0 - exp(-optical2), _VhFarHazeParams.w);
                float total = 1.0 - (1.0 - amount) * (1.0 - amount2);
                float mu = saturate(dot(dir, _VhAtmoSunDir.xyz));
                float3 hazeCol = lerp(_VhHazeColor.rgb, _VhHazeSunColor.rgb, pow(mu, _VhHazeParams2.x));
                c.rgb = lerp(c.rgb, hazeCol, total);
            }
            float3 sh = SAMPLE_TEXTURE2D_LOD(_VhShaftTex, sampler_LinearClamp, uv, 0).rgb;
            float facing = saturate(_VhSunScreen.z * 1.6 + 0.2);
            c.rgb += sh * _VhShaftTint.rgb * _VhShaftParams.x * facing;
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "VH Shaft Mask"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMask
            ENDHLSL
        }
        Pass
        {
            Name "VH Shaft Blur"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlur
            ENDHLSL
        }
        Pass
        {
            Name "VH Atmosphere Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite
            ENDHLSL
        }
    }
    Fallback Off
}
