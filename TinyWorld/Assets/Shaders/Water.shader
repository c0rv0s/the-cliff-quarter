// Clear stylized river: depth color, refraction, bed caustics, shoreline foam lines, sky fresnel.
Shader "TinyWorld/Water"
{
    Properties
    {
        _Shallow("Shallow", Color) = (.42,.86,.8,1)
        _Mid("Mid", Color) = (.11,.62,.66,1)
        _Deep("Deep", Color) = (.07,.3,.42,1)
        _Sky("Sky reflection", Color) = (.72,.86,.9,1)
        _Foam("Foam", Color) = (.97,.98,.95,1)
        _Clarity("Clarity", Range(0,1)) = .16
        _Refraction("Refraction", Range(0,.1)) = .025
        _Caustics("Caustics", Range(0,2)) = .65
        _FoamWidth("Foam width", Range(0,1)) = .16
        _Flow("Flow", Vector) = (0,.35,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Blend Off Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Shallow, _Mid, _Deep, _Sky, _Foam; half _Clarity, _Refraction, _Caustics, _FoamWidth; float4 _Flow;
            CBUFFER_END
            #include "TinyWorldCommon.hlsl"
            struct A { float4 positionOS:POSITION; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half fog:TEXCOORD1; };
            V vert(A i) { V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.fog = ComputeFogFactor(o.positionCS.z); return o; }
            float Height(float2 q, float t) { return TWNoise(q * .55 + _Flow.xy * t * .9) * .6 + TWNoise(q * 1.3 - _Flow.yx * t * .6 + 4.1) * .4; }
            float3 BedAt(float2 uv) { return ComputeWorldSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_VP); }
            half4 frag(V i) : SV_Target
            {
                float3 p = i.positionWS; float t = _Time.y;
                float3 v = GetWorldSpaceNormalizeViewDir(p);
                float e = .12, h = Height(p.xz, t);
                float3 n = normalize(float3(h - Height(p.xz + float2(e, 0), t), 1.6, h - Height(p.xz + float2(0, e), t)) * float3(2.2, 1, 2.2));
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float3 bed = BedAt(uv);
                float depth = max(0, p.y - bed.y);
                float2 refractUV = uv + n.xz * _Refraction * saturate(depth * .6);
                float3 bed2 = BedAt(refractUV);
                if (bed2.y > p.y) { refractUV = uv; bed2 = bed; }
                float depth2 = max(0, p.y - bed2.y), viewDepth = distance(bed2, p);
                half3 scene = SampleSceneColor(refractUV);

                half3 tint = lerp(_Shallow.rgb, _Mid.rgb, smoothstep(.2, 2.6, depth2));
                tint = lerp(tint, _Deep.rgb, smoothstep(2.6, 9, depth2));
                half3 color = lerp(scene * tint * 1.55, tint, saturate(1 - exp(-viewDepth * _Clarity)));

                Light light = GetMainLight(TransformWorldToShadowCoord(p));
                half bedLight = MainLightRealtimeShadow(TransformWorldToShadowCoord(bed2));
                float2 cq = bed2.xz * .42 + _Flow.xy * t * .4;
                half caustic = (1 - smoothstep(0, .09, TWVoronoiEdge(cq))) * .6 + (1 - smoothstep(0, .07, TWVoronoiEdge(cq * 1.7 + 3.3))) * .4;
                color += caustic * _Caustics * light.color * bedLight * exp(-depth2 * .3) * saturate(depth2 * 3) * .35;

                half fresnel = .03 + .5 * pow(1 - saturate(dot(n, v)), 5);
                half shade = lerp(.72, 1, light.shadowAttenuation);
                color = lerp(color, _Sky.rgb * shade, fresnel);
                float3 hv = normalize(light.direction + v);
                half sparkle = smoothstep(.985, .995, dot(n, hv)) * light.shadowAttenuation;
                color += sparkle * light.color * 1.6;
                half streak = smoothstep(.66, .74, TWNoise(float2(p.x * .9, p.z * .07 - t * _Flow.y * .8))) * .07;
                color += streak * shade;

                float wobble = (TWNoise(p.xz * .7 + t * .25) - .5) * .22;
                float fd = depth + wobble;
                half edge = 1 - smoothstep(_FoamWidth * .45, _FoamWidth, fd);
                float ringAt = _FoamWidth * 3.2 + sin(t * 1.1 + p.x * .2 + p.z * .17) * _FoamWidth * .7;
                half ring = (1 - smoothstep(.0, _FoamWidth * .45, abs(fd - ringAt))) * .55 * (TWNoise(p.xz * 1.6 + t * .2) > .38);
                color = lerp(color, _Foam.rgb * shade, saturate(edge + ring));
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
