// Frame-wide stylized lighting: soft two-band ramp, palette-tinted shadows,
// procedural surface patterns in world space, vertex-color tint, optional wind.
Shader "TinyWorld/Toon"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull mode", Float) = 2
        _BaseColor("Base", Color) = (1,1,1,1)
        _ShadowTint("Shadow tint", Color) = (.47,.56,.74,1)
        _RampCenter("Ramp center", Range(-1,1)) = .02
        _RampSoft("Ramp softness", Range(.005,.5)) = .09
        _Ambient("Ambient fill", Range(0,2)) = .55
        _Variation("World variation", Range(0,1)) = .18
        [Enum(None,0,Masonry,1,Rock,2,Roof,3,Paving,4,Calcite,5)] _Pattern("Pattern", Float) = 0
        _PatternStrength("Pattern strength", Range(0,2)) = 1
        _TopColor("Top growth", Color) = (.45,.55,.3,1)
        _TopAmount("Top growth amount", Range(0,1)) = 0
        _WetLine("Wet line", Range(0,1)) = 0
        _RimColor("Rim", Color) = (1,.93,.8,1)
        _RimStrength("Rim strength", Range(0,1)) = .22
        _Gloss("Gloss", Range(0,1)) = 0
        _Translucency("Translucency", Range(0,1)) = 0
        _Wind("Wind", Range(0,3)) = 0
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor, _ShadowTint, _TopColor, _RimColor, _EmissionColor;
        half _RampCenter, _RampSoft, _Ambient, _Variation, _Pattern, _PatternStrength, _TopAmount, _WetLine;
        half _RimStrength, _Gloss, _Translucency, _Wind;
        CBUFFER_END
        #include "TinyWorldCommon.hlsl"
        float3 DisplaceWS(float3 positionWS, half4 color) { return _Wind > 0 ? TWWind(positionWS, color.a, _Wind) : positionWS; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; half4 color:TEXCOORD2; half fog:TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 p = DisplaceWS(TransformObjectToWorld(i.positionOS.xyz), i.color);
                o.positionWS = p; o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS); o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            float2 WallUV(float3 p, float3 n) { float3 t = normalize(float3(-n.z, 0, n.x) + 1e-5); return float2(dot(p, t), p.y); }
            half Blocks(float2 uv, float2 size)
            {
                float row = floor(uv.y / size.y); uv.x += frac(row * .5) * size.x + row * .37;
                float2 cell = floor(uv / size), f = frac(uv / size);
                float edge = min(min(f.x, 1 - f.x) * size.x, min(f.y, 1 - f.y) * size.y);
                return lerp(.84, 1, smoothstep(.012, .04, edge)) * (1 + (TWHash(cell) - .5) * .09);
            }
            half3 Pattern(float3 p, float3 n, half3 albedo)
            {
                half shade = 1; float up = n.y;
                if (_Pattern == 1) shade = abs(up) < .6 ? Blocks(WallUV(p, n), float2(1.1, .48)) : Blocks(p.xz, float2(1.3, .9));
                else if (_Pattern == 4) shade = up > .6 ? Blocks(p.xz + p.y, float2(1.25, .82)) : Blocks(WallUV(p, n), float2(.9, .42));
                else if (_Pattern == 2)
                {
                    float fault = TWFbm(p.xz * .043 + p.y * .014);
                    float bedding = p.y + dot(p.xz,float2(.16,-.08)) + fault * 12;
                    float band = frac(bedding * .22);
                    float seam = (1 - smoothstep(.0,.13,band)) * smoothstep(.32,.65,fault);
                    shade = .97 + .085 * sin(bedding * 1.37) - .14 * seam;
                    shade *= lerp(.86,1.11,TWFbm(float2(dot(p.xz,float2(.09,.06)),p.y*.15)));
                }
                else if (_Pattern == 5)
                {
                    float2 plane=float2(dot(p.xz,float2(.83,.55)), p.y+.19*p.z-.09*p.x);
                    float fold=TWFbm(plane*.47);
                    float grain=TWNoise(plane*4.3);
                    float vein=abs(frac(plane.y*.96+fold*1.72+grain*.075)-.5);
                    float mineral=(1-smoothstep(.012,.045,vein))*smoothstep(.41,.68,fold);
                    float damp=smoothstep(.49,.72,fold+.07*n.y);
                    albedo*=lerp(half3(.85,.82,.79),half3(1.02,1.08,1.08),fold);
                    albedo=lerp(albedo,half3(.50,.49,.39),mineral*.57);
                    shade=.86+.21*fold+.16*grain-.19*damp;
                }
                else if (_Pattern == 3 && up > .15)
                {
                    float2 uv = WallUV(p, normalize(float3(n.x, 0, n.z) + 1e-4));
                    float ridge = .5 + .5 * cos(uv.x * 6.2831 / .34);
                    float row = frac(p.y / .26);
                    shade = lerp(.76, 1.06, ridge) * lerp(.8, 1, smoothstep(0, .2, row)) * (1 + (TWHash(floor(float2(uv.x / .34, p.y / .26))) - .5) * .14);
                }
                return albedo * lerp(1, shade, _PatternStrength);
            }
            half4 frag(V i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(facing, 1.0, -1.0), p = i.positionWS;
                float3 v = GetWorldSpaceNormalizeViewDir(p);
                half3 albedo = _BaseColor.rgb * i.color.rgb;
                float vary = TWFbm(p.xz * .06 + p.y * .045);
                albedo *= 1 + (vary - .5) * _Variation;
                albedo = lerp(albedo, albedo * half3(1.05, .98, .9), saturate(vary * 1.4 - .4) * _Variation);
                albedo = Pattern(p, n, albedo);
                if (_TopAmount > 0)
                {
                    float grass = smoothstep(.5, .78, n.y + (TWNoise(p.xz * .35) - .5) * .45);
                    albedo = lerp(albedo, _TopColor.rgb * (.9 + TWNoise(p.xz * 1.7) * .2), grass * _TopAmount);
                }
                if (_WetLine > 0) albedo *= lerp(1 - .28 * _WetLine, 1, saturate((p.y - .05 + TWNoise(p.xz * .8) * .25) / 1.1));

                float4 shadowCoord = TransformWorldToShadowCoord(p);
                Light light = GetMainLight(shadowCoord);
                float ndl = dot(n, light.direction);
                half atten = light.shadowAttenuation;
                half band = smoothstep(_RampCenter - _RampSoft, _RampCenter + _RampSoft, ndl) * atten;
                half form = lerp(.82, 1, saturate(ndl * 1.4));

                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half directAO = 1, indirectAO = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
                directAO = ao.directAmbientOcclusion; indirectAO = ao.indirectAmbientOcclusion;
                #endif
                half3 sky = SampleSH(n);
                half3 shadowSide = albedo * (_ShadowTint.rgb * .62 + sky * _Ambient) * indirectAO;
                half3 litSide = albedo * light.color * form * directAO + albedo * sky * _Ambient * .35 * indirectAO;
                // Broad cloud shadows cross many facades together, keeping shaded alleys cool.
                float cloud = smoothstep(.34,.71,TWNoise((p.xz + p.y * float2(.37,.12)) * .018 + _Time.y * float2(.019,.009)));
                litSide *= lerp(half3(1,.99,.96), half3(.76,.82,.9), cloud);
                half3 color = lerp(shadowSide, litSide, band);

                half rim = pow(1 - saturate(dot(n, v)), 4) * _RimStrength;
                color += rim * _RimColor.rgb * light.color * (.35 + .65 * band) * saturate(albedo * 1.6 + .2);
                if (_Gloss > 0)
                {
                    float3 h = normalize(light.direction + v);
                    color += smoothstep(.9, .94, pow(saturate(dot(n, h)), 8)) * _Gloss * band * light.color;
                }
                if (_Translucency > 0)
                    color += albedo * light.color * pow(saturate(dot(v, -light.direction)), 3) * _Translucency * (.4 + .6 * atten);
                color += _EmissionColor.rgb;
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(A i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = DisplaceWS(TransformObjectToWorld(i.positionOS.xyz), i.color);
                float3 n = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 l = normalize(_LightPosition - p);
                #else
                float3 l = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, l)));
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 vert(A i) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(i); return TransformWorldToHClip(DisplaceWS(TransformObjectToWorld(i.positionOS.xyz), i.color)); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            V vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); o.positionCS = TransformWorldToHClip(DisplaceWS(TransformObjectToWorld(i.positionOS.xyz), i.color)); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS) * IS_FRONT_VFACE(facing, 1.0, -1.0), 0); }
            ENDHLSL
        }
    }
}
