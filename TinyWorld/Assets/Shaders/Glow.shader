// Additive beacon/halo: fades along mesh UV.y and at grazing angles.
Shader "TinyWorld/Glow"
{
    Properties { [HDR] _Color("Color", Color) = (1,.8,.4,1) _Pulse("Pulse", Float) = 1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; half _Pulse; CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 n:TEXCOORD1; float3 p:TEXCOORD2; half fog:TEXCOORD3; };
            V vert(A i) { V o; o.p = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.p); o.uv = i.uv; o.n = TransformObjectToWorldNormal(i.normalOS); o.fog = ComputeFogFactor(o.positionCS.z); return o; }
            half4 frag(V i) : SV_Target
            {
                float facing = abs(dot(normalize(i.n), GetWorldSpaceNormalizeViewDir(i.p)));
                half fade = pow(1 - i.uv.y, 2.2) * smoothstep(0, .08, i.uv.y) * facing * facing;
                half pulse = lerp(1, .75 + .25 * sin(_Time.y * 2.4 + i.p.x), _Pulse);
                half3 c = _Color.rgb * fade * pulse;
                return half4(MixFogColor(c, 0, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
