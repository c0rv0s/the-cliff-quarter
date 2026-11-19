#ifndef TINYWORLD_COMMON_INCLUDED
#define TINYWORLD_COMMON_INCLUDED

float TWHash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
float2 TWHash2(float2 p) { float n = TWHash(p); return float2(n, TWHash(p + n * 17.3)); }
float TWNoise(float2 p)
{
    float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
    return lerp(lerp(TWHash(i), TWHash(i + float2(1, 0)), f.x), lerp(TWHash(i + float2(0, 1)), TWHash(i + 1), f.x), f.y);
}
float TWFbm(float2 p) { return TWNoise(p) * .5 + TWNoise(p * 2.03 + 7.1) * .27 + TWNoise(p * 4.11 + 3.3) * .15 + TWNoise(p * 8.3) * .08; }
// Distance to nearest Voronoi edge, for caustics.
float TWVoronoiEdge(float2 p)
{
    float2 i = floor(p), f = frac(p); float d1 = 8, d2 = 8;
    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
    {
        float2 g = float2(x, y); float2 o = TWHash2(i + g);
        o = .5 + .45 * sin(_Time.y * .9 + 6.2831 * o);
        float d = length(g + o - f);
        if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
    }
    return d2 - d1;
}
// Gentle sway; weight lives in vertex color alpha.
float4 _WandererVisitor;
float3 TWWind(float3 positionWS, float weight, float strength)
{
    float t = _Time.y;
    float gust = .6 + .4 * sin(t * .37 + positionWS.x * .02);
    float3 offset = float3(sin(t * 1.7 + positionWS.x * .45 + positionWS.z * .3), sin(t * 2.3 + positionWS.y) * .3, cos(t * 1.3 + positionWS.z * .5 + positionWS.y * .2));
    float3 brush = positionWS - _WandererVisitor.xyz;
    float along = saturate(1 - dot(brush, brush) * .70) * _WandererVisitor.w;
    return positionWS + (offset * gust * .09 + brush * along * .65) * weight * strength;
}
#endif
