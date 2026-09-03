#ifndef ASCE_WORLD_Y_DISSOLVE_INCLUDED
#define ASCE_WORLD_Y_DISSOLVE_INCLUDED

void WorldYDissolve_float(float3 PositionWS, float MinY, float MaxY, float Noise, float Extend, float Power, float Amount, float EdgeWidth, out float Out, out float Edge)
{
    float shapedNoise = pow(saturate(Noise), max(Power, 0.0001));
    float safeExtend = max(Extend, 0.0);
    float safeEdgeWidth = max(EdgeWidth, 0.0);
    float noiseOffset = (shapedNoise * 2.0 - 1.0) * safeExtend;
    float safeMinY = min(MinY, MaxY);
    float safeMaxY = max(MinY, MaxY);
    float heightRange = max(safeMaxY - safeMinY, 0.0001);
    float height01 = saturate((PositionWS.y - safeMinY) / heightRange);
    float dissolveField = height01 + noiseOffset;
    float endpointOffset = safeEdgeWidth + 0.001;
    float dissolveThreshold = lerp(-safeExtend - endpointOffset, 1.0 + safeExtend + endpointOffset, saturate(Amount));
    Out = step(dissolveThreshold, dissolveField);
    Edge = Out * (1.0 - step(dissolveThreshold + safeEdgeWidth, dissolveField));
}

void WorldYDissolve_half(half3 PositionWS, half MinY, half MaxY, half Noise, half Extend, half Power, half Amount, half EdgeWidth, out half Out, out half Edge)
{
    float result;
    float edgeResult;
    WorldYDissolve_float((float3)PositionWS, (float)MinY, (float)MaxY, (float)Noise, (float)Extend, (float)Power, (float)Amount, (float)EdgeWidth, result, edgeResult);
    Out = (half)result;
    Edge = (half)edgeResult;
}

#endif
