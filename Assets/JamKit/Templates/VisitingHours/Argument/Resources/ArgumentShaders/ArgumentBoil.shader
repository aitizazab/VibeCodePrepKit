// The battle sprite's hand-drawn boil (ArgumentBoil.cs): Lily's SVG filter from the scene 2 prototype
// (feTurbulence type="fractalNoise" numOctaves="2" feeding feDisplacementMap) as a blit shader.
// Each pixel is pushed by two fractal noises (x and y), at most _BoilScale.xy of the sprite's size; the noise has
// _BoilScale.zw cycles across the sprite; _BoilSeed re-rolls the noise (a new value a step = the boil).
Shader "Hidden/JamKit/ArgumentBoil"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BoilScale;
            float _BoilSeed;

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
            }

            // Gradient (Perlin-style) noise, about -0.7..0.7.
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = dot(Hash2(i), f);
                float b = dot(Hash2(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(Hash2(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(Hash2(i + float2(1, 1)), f - float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // feTurbulence's fractal noise with two octaves: each octave twice the frequency and half the weight.
            float Fractal(float2 p)
            {
                return Noise(p) + 0.5 * Noise(p * 2.0);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                // A new seed jumps to an unrelated part of the noise (kept small so precision holds).
                float2 seed = Hash2(float2(_BoilSeed * 0.731, _BoilSeed * 1.379 + 0.5)) * 97.0;
                float2 p = i.uv * _BoilScale.zw;
                // feDisplacementMap: the push is scale * (channel - 0.5), the channel being (noise + 1) / 2, and the
                // scale twice the strength: so the push is strength * noise.
                float2 push = float2(Fractal(p + seed), Fractal(p + seed + float2(41.3, 17.9))) * _BoilScale.xy;
                float2 uv = i.uv + push;
                fixed4 c = tex2D(_MainTex, uv);
                // Nothing comes in from beyond her sides or the top (the clamp would smear those edge pixels); the
                // bottom edge is where the drawing is cut off mid-figure, so there it holds the edge (no ragged gap).
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(uv.y, 1.0);
                return c * inside;
            }
            ENDCG
        }
    }
    Fallback Off
}
