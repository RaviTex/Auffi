#ifndef AUFFI_UNLIT_INPUT_INCLUDED
#define AUFFI_UNLIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

// Copy of URP's UnlitInput.hlsl with the per-material wind height range added so the
// shader stays SRP Batcher compatible.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4 _BaseColor;
    half _Cutoff;
    half _Surface;
    float _WindStart;
    float _WindEnd;
    float _WindAmount;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
    UNITY_DOTS_INSTANCED_PROP(float , _WindStart)
    UNITY_DOTS_INSTANCED_PROP(float , _WindEnd)
    UNITY_DOTS_INSTANCED_PROP(float , _WindAmount)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Surface;
static float  unity_DOTS_Sampled_WindStart;
static float  unity_DOTS_Sampled_WindEnd;
static float  unity_DOTS_Sampled_WindAmount;

void SetupDOTSUnlitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_Cutoff    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Surface   = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
    unity_DOTS_Sampled_WindStart = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _WindStart);
    unity_DOTS_Sampled_WindEnd   = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _WindEnd);
    unity_DOTS_Sampled_WindAmount = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _WindAmount);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSUnlitMaterialPropertyCaches()

#define _BaseColor  unity_DOTS_Sampled_BaseColor
#define _Cutoff     unity_DOTS_Sampled_Cutoff
#define _Surface    unity_DOTS_Sampled_Surface
#define _WindStart  unity_DOTS_Sampled_WindStart
#define _WindEnd    unity_DOTS_Sampled_WindEnd
#define _WindAmount unity_DOTS_Sampled_WindAmount

#endif

#endif
