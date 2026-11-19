#ifndef AUFFI_WIND_INCLUDED
#define AUFFI_WIND_INCLUDED

// Global wind state. WindManager.cs writes these with Shader.SetGlobal* so every
// wind-aware material reacts to the same direction and strength.
float4 _AuffiWindDirection;
float _AuffiWindStrength;
float _AuffiWindBend;
float _AuffiWindFrequency;
float _AuffiWindGustStrength;
float _AuffiWindGustFrequency;

// Returns the world-space offset for a vertex. Geometry below _WindStart (in metres
// above the object's pivot) stays put, so trunks and bush bases do not slide around;
// only the upper part of the plant bends.
float3 AuffiWindOffsetWS(float3 positionWS, float3 originWS)
{
    float height = max(positionWS.y - originWS.y, 0.0);
    float bend01 = saturate((height - _WindStart) / max(_WindEnd - _WindStart, 0.001));
    float bend = bend01 * bend01 * (3.0 - 2.0 * bend01);

    float2 directionXZ = _AuffiWindDirection.xz;
    float directionLength = length(directionXZ);
    float2 windDirection = directionLength > 0.0001 ? directionXZ / directionLength : float2(1.0, 0.0);

    // Offset the phase by the instance position so nearby plants do not sway in lockstep.
    float phase = dot(originWS.xz, float2(0.1731, 0.3179));
    float time = _Time.y * _AuffiWindFrequency;
    float sway = sin(time + phase) * 0.65 + sin(time * 2.27 + phase * 1.7) * 0.35;

    float gust = 1.0 - _AuffiWindGustStrength *
        (0.5 + 0.5 * sin(_Time.y * _AuffiWindGustFrequency + phase * 0.37));

    float amount = _AuffiWindStrength * _AuffiWindBend * _WindAmount * gust * bend * sway;
    return float3(windDirection.x, 0.0, windDirection.y) * amount;
}

void AuffiApplyWindOS(inout float3 positionOS)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 originWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
    float3 offsetWS = AuffiWindOffsetWS(positionWS, originWS);
    positionOS += TransformWorldToObjectDir(offsetWS, false);
}

#endif
