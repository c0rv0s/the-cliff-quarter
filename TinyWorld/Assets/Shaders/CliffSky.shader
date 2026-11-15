Shader "TinyWorld/CliffSky"
{
    Properties
    {
        _Sky("Zenith", Color) = (.27,.54,.72,1)
        _Horizon("Horizon", Color) = (.68,.81,.84,1)
        _Haze("Low haze", Color) = (.93,.88,.78,1)
        _Cloud("Cloud", Color) = (.99,.96,.9,1)
        _CloudShadow("Cloud shadow", Color) = (.62,.7,.82,1)
        _SunDir("Sun direction", Vector) = (-.4,.7,.5,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 vertex:SV_POSITION; float3 direction:TEXCOORD0; };
            float4 _Sky, _Horizon, _Haze, _Cloud, _CloudShadow, _SunDir;
            v2f vert(appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.direction = v.vertex.xyz; return o; }
            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
            }
            float fbm(float2 p) { float a = .5, s = 0; for (int i = 0; i < 5; i++) { s += noise(p) * a; p = p * 2.03 + 1.7; a *= .5; } return s; }
            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float3 sun = normalize(_SunDir.xyz);
                float up = saturate(d.y);
                half3 color = lerp(_Horizon.rgb, _Sky.rgb, pow(up, .5));
                color = lerp(color, _Haze.rgb, pow(1 - saturate(d.y + .02), 9) * .85);
                float sunDot = saturate(dot(d, sun));
                color += _Haze.rgb * (pow(sunDot, 6) * .22 + pow(sunDot, 60) * .35) + pow(sunDot, 1800) * 3;

                // Cumulus banks: denser near the horizon, lit from the sun side.
                float2 p = d.xz / (d.y + .16) * 1.25 + float2(_Time.y * .004, 0);
                float base = fbm(p * .9);
                float detail = fbm(p * 3.1 + 5);
                float density = base * .75 + detail * .35 - lerp(.42, .62, smoothstep(.0, .55, d.y));
                float cover = smoothstep(.0, .14, density) * smoothstep(-.02, .1, d.y);
                float lit = saturate(.55 + (fbm(p * .9 + sun.xz * .25) - base) * -3.5 + sunDot * .3);
                half3 cloud = lerp(_CloudShadow.rgb, _Cloud.rgb, lit);
                cloud += pow(sunDot, 8) * .2 * (1 - smoothstep(.0, .3, density));
                color = lerp(color, cloud, cover * .96);
                color = lerp(color, _Haze.rgb, pow(1 - saturate(d.y + .05), 14) * .7);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
