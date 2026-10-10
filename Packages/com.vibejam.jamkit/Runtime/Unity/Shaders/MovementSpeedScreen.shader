// The speed visuals over the whole screen (Runtime/Unity/Movement/SpeedScreenFx.cs, docs/SPEED-FEEL.md), in one
// full-screen blit (URP 17 render graph, Blit.hlsl's Vert and _BlitTexture):
//   V1 the boom: a soft shockwave ring rippling out from the centre (a push plus a faint highlight) and a
//      one-frame chromatic split, stronger towards the edges;
//   V6 the edge zoom blur: 8 taps towards the centre, weighted so the centre stays sharp;
//   V7 the dream's grade: more saturation and a gentle lift towards white.
// Everything is driven by script; at zero it is a straight copy (but the script doesn't queue the pass then).
// Plain HLSL, explicit-LOD samples (safe inside branches on WebGL 2 / GLES 3).
Shader "JamKit/Movement/SpeedScreen"
{
    Properties
    {
        _Shock ("Ring (radius, push, width, aspect)", Vector) = (0, 0, 0.09, 1.7778)
        _Chroma ("Chromatic split (x)", Vector) = (0, 0, 0, 0)
        _EdgeBlur ("Edge blur (reach, inner, outer)", Vector) = (0, 0.45, 1, 0)
        _Grade ("Grade (saturation, lift)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "SpeedScreen"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Shock;    // x ring radius (screen heights), y push (screen heights), z width, w aspect (width / height)
            float4 _Chroma;   // x split at the corners (screen heights)
            float4 _EdgeBlur; // x reach (fraction of the way to the centre), y sharp inside, z full from (fractions of the half-diagonal)
            float4 _Grade;    // x extra saturation (0.25 = +25%), y lift towards white

            half3 Tap(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float aspect = max(_Shock.w, 0.1);
                // Position from the centre in screen heights (x stretched by the aspect, so the ring is round).
                float2 p = float2((uv.x - 0.5) * aspect, uv.y - 0.5);
                float r = length(p);
                float2 dir = r > 1e-4 ? p / r : float2(0.0, 0.0);
                float halfDiag = length(float2(0.5 * aspect, 0.5));
                float edge = saturate(r / halfDiag);

                // V1: the ring pushes the picture outward where it passes.
                float ring = 0.0;
                UNITY_BRANCH
                if (_Shock.y > 0.0)
                {
                    float d = (r - _Shock.x) / max(_Shock.z, 0.001);
                    ring = exp(-d * d);
                    float2 push = dir * ring * _Shock.y;
                    push.x /= aspect;
                    uv -= push;
                }

                // V6: blur towards the centre, only out at the edges.
                half3 col;
                float blur = _EdgeBlur.x * smoothstep(_EdgeBlur.y, _EdgeBlur.z, edge);
                UNITY_BRANCH
                if (blur > 1e-4)
                {
                    half3 acc = half3(0.0, 0.0, 0.0);
                    UNITY_UNROLL
                    for (int i = 0; i < 8; i++)
                    {
                        float k = 1.0 - blur * (i / 7.0);
                        acc += Tap(0.5 + (uv - 0.5) * k);
                    }
                    col = acc * 0.125;
                }
                else
                {
                    col = Tap(uv);
                }

                // V1: the chromatic pulse, red out and blue in, growing towards the edges.
                UNITY_BRANCH
                if (_Chroma.x > 0.0)
                {
                    float2 off = dir * _Chroma.x * edge;
                    off.x /= aspect;
                    col.r = Tap(uv + off).r;
                    col.b = Tap(uv - off).b;
                }

                // V1: a faint highlight along the ring, fading with its push.
                col = lerp(col, col * 1.12 + 0.035, ring * saturate(_Shock.y * 40.0));

                // V7: the dream brightens and saturates.
                half luma = dot(col, half3(0.2126, 0.7152, 0.0722));
                col = max(lerp(half3(luma, luma, luma), col, 1.0 + _Grade.x), 0.0);
                col += _Grade.y * (1.0 - saturate(col));

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
