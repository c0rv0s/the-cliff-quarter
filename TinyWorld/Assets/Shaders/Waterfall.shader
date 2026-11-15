// Toon waterfall: falling streaks in the river palette, a foamy lip and churned base. UV: x across, y metres fallen.
Shader "TinyWorld/Waterfall"
{
    Properties
    {
        _Deep("Deep", Color) = (.11,.55,.6,1)
        _Light("Light", Color) = (.56,.89,.86,1)
        _Foam("Foam", Color) = (.97,.98,.95,1)
        _Speed("Fall speed", Float) = 2.4
        _Height("Fall height", Float) = 100
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Deep, _Light, _Foam; float _Speed, _Height;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            float Hash(float n) { return frac(sin(n) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f); float n = i.x + i.y * 57;
                return lerp(lerp(Hash(n), Hash(n + 1), f.x), lerp(Hash(n + 57), Hash(n + 58), f.x), f.y);
            }
            V vert(A i)
            {
                V o; float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws); o.uv = i.uv; o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float s = Noise(float2(i.uv.x * 2.2, i.uv.y * .12 - t * .35)) * .6 + Noise(float2(i.uv.x * 5.3 + 7, i.uv.y * .3 - t * .8)) * .5;
                half3 c = lerp(_Deep.rgb, _Light.rgb, smoothstep(.35, .75, s));
                float foam = smoothstep(.78, .86, s) + saturate(1.4 - i.uv.y * .6) + saturate((i.uv.y - _Height + 6) * .2);
                c = lerp(c, _Foam.rgb, saturate(foam));
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
