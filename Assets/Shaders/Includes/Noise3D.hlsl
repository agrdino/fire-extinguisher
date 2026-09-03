#ifndef ASCE_NOISE_3D_INCLUDED
#define ASCE_NOISE_3D_INCLUDED

float Hash31(float3 position)
{
    position = frac(position * 0.1031);
    position += dot(position, position.yzx + 33.33);
    return frac((position.x + position.y) * position.z);
}

float ValueNoise3D(float3 position)
{
    float3 cell = floor(position);
    float3 localPosition = frac(position);
    float3 blend = localPosition * localPosition * (3.0 - 2.0 * localPosition);

    float n000 = Hash31(cell + float3(0.0, 0.0, 0.0));
    float n100 = Hash31(cell + float3(1.0, 0.0, 0.0));
    float n010 = Hash31(cell + float3(0.0, 1.0, 0.0));
    float n110 = Hash31(cell + float3(1.0, 1.0, 0.0));
    float n001 = Hash31(cell + float3(0.0, 0.0, 1.0));
    float n101 = Hash31(cell + float3(1.0, 0.0, 1.0));
    float n011 = Hash31(cell + float3(0.0, 1.0, 1.0));
    float n111 = Hash31(cell + float3(1.0, 1.0, 1.0));

    float x00 = lerp(n000, n100, blend.x);
    float x10 = lerp(n010, n110, blend.x);
    float x01 = lerp(n001, n101, blend.x);
    float x11 = lerp(n011, n111, blend.x);
    float xy0 = lerp(x00, x10, blend.y);
    float xy1 = lerp(x01, x11, blend.y);
    return lerp(xy0, xy1, blend.z);
}

void Noise3D_float(float3 Position, float Scale, out float Out)
{
    Out = ValueNoise3D(Position * Scale);
}

void Noise3D_half(half3 Position, half Scale, out half Out)
{
    Out = (half)ValueNoise3D((float3)Position * (float)Scale);
}

#endif
