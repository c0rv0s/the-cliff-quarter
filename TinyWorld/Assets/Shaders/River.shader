Shader "TinyWorld/River"
{
    Properties { _BaseColor("Water", Color) = (.09,.49,.52,1) _Shallow("Shallow", Color) = (.24,.72,.64,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; float fog : TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _Shallow;
            CBUFFER_END
            V vert(A i) { V o; o.world = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.world); o.fog=ComputeFogFactor(o.positionCS.z); return o; }
            half4 frag(V i) : SV_Target
            {
                float2 p = i.world.xz;
                float t = _Time.y;
                float a = sin(p.x * .83 + p.y * .33 + t*.65);
                float b = sin(p.x * 1.7 - p.y * 1.1 - t*.95);
                float c = sin(p.x *.12 + p.y *.21);
                float glint = pow(saturate(a*b*sin(p.x*.43+p.y*.73+t*.3)), 20) * .18;
                half3 col = lerp(_BaseColor.rgb, _Shallow.rgb, .22 + .2*c + .08*a) + glint;
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
    }
}
