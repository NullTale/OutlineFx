#ifndef OUTLINE_UTILS_INCLUDED
#define OUTLINE_UTILS_INCLUDED

#include "UnityCG.cginc"

struct vertIn
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct fragIn
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
};

fragIn vert_mesh(vertIn v)
{
    fragIn o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = v.uv;

    return o;
}

fragIn vert_screen(vertIn v)
{
    fragIn o;
    o.vertex = v.vertex;
    o.uv = v.uv;

    return o;
}

float random(in const float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
}

#endif
