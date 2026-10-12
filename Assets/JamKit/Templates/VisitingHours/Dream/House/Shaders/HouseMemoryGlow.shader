// A memory's glow (House round 2, house-1.md blocker 2: the old glows were hard-edged translucent spheres that cut
// through the bookcase, the workbench and the door frame). A camera-facing soft disc drawn additively round the thing,
// pulled a little toward the camera so the thing doesn't halve it, faded where it meets anything behind it (the scene's
// depth: soft, no hard intersection line) and near the camera. The object's scale is the glow's diameter in metres.
Shader "JamKit/House/MemoryGlow"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 0.82, 0.5, 1)
        _Intensity ("Intensity", Float) = 0.9
        _Softness ("Depth fade (metres)", Float) = 0.4
        _Pulse ("Pulse (set by HouseMemoryView)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Softness;
                half _Pulse;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
                float eye : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 centerVS = TransformWorldToView(centerWS);
                float dist = -centerVS.z;
                float3 toCamera = normalize(-centerVS);
                centerVS += toCamera * min(size * 0.45, max(0.0, dist - 0.35));
                float3 positionVS = centerVS + float3(input.positionOS.xy * size, 0.0);
                o.positionCS = TransformWViewToHClip(positionVS);
                o.uv = input.uv;
                o.screen = ComputeScreenPos(o.positionCS);
                o.eye = -positionVS.z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                float r2 = dot(d, d);
                half fall = saturate(1.0 - r2);
                fall *= fall;
                half core = exp(-r2 * 10.0) * 0.5;
                float2 suv = i.screen.xy / i.screen.w;
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                half fade = saturate((scene - i.eye) / max(1e-3, _Softness));
                half near = saturate((i.eye - 0.25) / 0.6);
                half a = (fall + core) * fade * near * _Intensity * (1.0 + _Pulse);
                return half4(_Color.rgb * a, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
