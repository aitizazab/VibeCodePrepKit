// The speed particles (docs/SPEED-FEEL.md): V2's world-space motes, confetti and petals (SpeedStreaks.cs), V5's
// footfall puffs and glowing footprints (SpeedFootfalls.cs) and V7's opening flowers (the dream escape). The shapes
// are drawn here from the quad's UV (no texture): 0 round mote, 1 confetti slip, 2 petal, 3 five-petal flower,
// 4 shoe print, 5 soft puff. Each particle picks one of [_Shapes.x, _Shapes.x + _Shapes.y) by its stable random
// (vertex stream TEXCOORD0.z). Every shape has a light core (the particle colour) and a dark rim (_RimColor), so it
// reads on bright pastel and on dark scenes alike. Near the camera, particles fade out inside the clear centre of
// the screen (_CenterClear), so they never hide the route. Premultiplied alpha by default; _DstBlend = One makes it
// additive (the glowing footprints). Fog applies.
Shader "JamKit/Movement/SpeedMotes"
{
    Properties
    {
        _RimColor ("Rim (outline) colour", Color) = (0.16, 0.15, 0.2, 1)
        _RimWidth ("Rim width (share of the shape)", Range(0, 0.6)) = 0.2
        _Glow ("Core brightness", Range(0, 4)) = 1
        _Soft ("Edge softness", Range(0, 1)) = 0
        _Shapes ("Shapes (first id, count)", Vector) = (0, 1, 0, 0)
        _CenterClear ("Clear centre (radius in screen heights, near distance m)", Vector) = (0.2, 8, 0, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "SpeedMotes"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimWidth;
                half _Glow;
                half _Soft;
                float4 _Shapes;
                float4 _CenterClear;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float3 uvRand : TEXCOORD0; // xy the quad's UV, z the particle's stable random (0..1)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 uvRand : TEXCOORD0;
                float fade : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(ws);
                o.color = v.color;
                o.uvRand = v.uvRand;
                // Distance in front of the camera, and where on screen (in screen heights from the centre).
                float depth = -TransformWorldToView(ws).z;
                float2 ndc = o.positionCS.xy / max(o.positionCS.w, 1e-4);
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float rad = length(float2(ndc.x * aspect, ndc.y)) * 0.5;
                float nearness = 1.0 - smoothstep(_CenterClear.y * 0.6, _CenterClear.y, depth);
                float clearC = _CenterClear.x > 0.0 ? smoothstep(_CenterClear.x * 0.7, _CenterClear.x, rad) : 1.0;
                o.fade = lerp(1.0, clearC, nearness) * smoothstep(0.25, 0.9, depth);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Ellipse(float2 q, float2 r)
            {
                return (length(q / r) - 1.0) * min(r.x, r.y);
            }

            // Signed distance (in quad half-widths) of shape `id` at q (-1..1).
            float Shape(float id, float2 q)
            {
                if (id < 0.5) return length(q) - 0.8;                                      // round mote
                if (id < 1.5) { float2 b = abs(q) - float2(0.7, 0.36);                       // confetti slip
                                return length(max(b, 0.0)) + min(max(b.x, b.y), 0.0) - 0.14; }
                if (id < 2.5) return Ellipse(q, float2(0.46, 0.88));                          // petal
                if (id < 3.5) { float a = atan2(q.y, q.x);                                     // flower, five lobes
                                return length(q) - (0.6 + 0.3 * cos(5.0 * a)); }
                if (id < 4.5) return min(Ellipse(q - float2(0.0, 0.22), float2(0.44, 0.62)),  // shoe print: sole and heel
                                         length(q - float2(0.0, -0.66)) - 0.3);
                return length(q) - 0.75;                                                      // soft puff
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 q = i.uvRand.xy * 2.0 - 1.0;
                float id = _Shapes.x + min(floor(i.uvRand.z * max(_Shapes.y, 1.0)), max(_Shapes.y, 1.0) - 1.0);
                float d = Shape(id, q);
                float aa = max(fwidth(d), 1e-3);
                float soft = max(_Soft, aa);
                half inside = 1.0 - smoothstep(-soft, aa, d);
                half rim = _RimWidth > 0.0 ? smoothstep(-_RimWidth - aa, -_RimWidth + aa, d) : 0.0;
                half3 core = i.color.rgb * _Glow;
                // The flower gets a small butter-coloured heart.
                if (id > 2.5 && id < 3.5) core = lerp(half3(1.0, 0.9, 0.55), core, smoothstep(0.18, 0.28, length(q)));
                half3 col = lerp(core, _RimColor.rgb, rim);
                col = MixFog(col, i.fog);
                half a = inside * i.color.a * i.fade;
                return half4(col * a, a);
            }
            ENDHLSL
        }
    }
}
