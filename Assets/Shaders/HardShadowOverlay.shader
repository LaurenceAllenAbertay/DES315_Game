Shader "Hidden/HardShadowOverlay"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "HardShadowOverlay"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HardShadowFrag

            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define MAX_LIGHTS 16
            #define SHADOW_SAMPLES 8
            
            TEXTURE2D(_HardShadowOccluderMask);

            float4  _ShadowColor;
            float4  _LightColor;
            int     _LightCount;
            float4  _LightPositions[MAX_LIGHTS];
            float   _LightRanges[MAX_LIGHTS];
            float   _LightIndices[MAX_LIGHTS];

            float   _WobbleAmount;
            float   _WobbleSpeed;
            float   _WobbleFrequency;
            float   _EdgeSoftness;
            
            // Samples URP's shadow map for an additional light at a world position.
            Light GetAdditionalLightGlobal(int lightIndex, float3 positionWS)
            {
                Light light = GetAdditionalPerObjectLight(lightIndex, positionWS);

                #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                    half4 occlusionProbeChannels = _AdditionalLightsBuffer[lightIndex].occlusionProbeChannels;
                #else
                    half4 occlusionProbeChannels = _AdditionalLightsOcclusionProbes[lightIndex];
                #endif

                light.shadowAttenuation = AdditionalLightShadow(
                    lightIndex, positionWS, light.direction,
                    half4(1, 1, 1, 1), occlusionProbeChannels);

                return light;
            }
            
            // Multi-tap soft shadow sampling.
            float SampleShadowSoft(int lightIndex, float3 worldPos, float3 lightPos, float softRadius)
            {
                if (softRadius <= 0.001)
                {
                    Light l = GetAdditionalLightGlobal(lightIndex, worldPos);
                    return l.shadowAttenuation;
                }

                float3 lightDir  = normalize(lightPos - worldPos);
                float3 tangent   = abs(lightDir.y) < 0.99
                    ? normalize(cross(lightDir, float3(0, 1, 0)))
                    : normalize(cross(lightDir, float3(1, 0, 0)));
                float3 bitangent = cross(lightDir, tangent);

                float total     = 0.0;
                float weightSum = 0.0;

                 for (int s = 0; s < SHADOW_SAMPLES; s++)
                {
                    float angle = (float)s / (float)SHADOW_SAMPLES * 6.28318;
                    float r     = softRadius * (0.3 + 0.7 * frac(sin(s * 12.9898) * 43758.5453));

                    float3 offset    = (cos(angle) * tangent + sin(angle) * bitangent) * r;
                    float3 samplePos = worldPos + offset;

                    Light l = GetAdditionalLightGlobal(lightIndex, samplePos);

                    float weight = 1.0 - (r / softRadius) * 0.5;
                    total     += l.shadowAttenuation * weight;
                    weightSum += weight;
                }

                return total / weightSum;
            }
            
            // Wobble noise — integer frequency multiples only to avoid seam.
            float WobbleNoise(float3 worldPos, float3 lightPos, float time)
            {
                float3 dir   = normalize(worldPos - lightPos);
                float angle1 = atan2(dir.z, dir.x);
                float angle2 = asin(clamp(dir.y, -1.0, 1.0));

                float baseFreq = floor(_WobbleFrequency);

                float wobble = 0.0;
                wobble += sin(angle1 * baseFreq             + time * _WobbleSpeed)        * 0.50;
                wobble += sin(angle2 * baseFreq             + time * _WobbleSpeed * 1.3)  * 0.30;
                wobble += sin(angle1 * baseFreq * 2.0       - time * _WobbleSpeed * 0.8)  * 0.15;
                wobble += sin((angle1 + angle2) * baseFreq * 2.0 + time * _WobbleSpeed * 0.6) * 0.20;
                wobble += sin(angle1 * baseFreq * 4.0       + time * _WobbleSpeed * 1.1)  * 0.08;
                wobble *= 0.8;

                return wobble;
            }

            float4 HardShadowFrag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Only process pixels belonging to occluder-layer objects.
                float occluderMask = SAMPLE_TEXTURE2D(_HardShadowOccluderMask, sampler_LinearClamp, uv).r;
                if (occluderMask < 0.5)
                    return sceneColor;

                float depth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    if (depth <= 0.0001) return sceneColor;
                #else
                    if (depth >= 0.9999) return sceneColor;
                #endif

                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                float inLight = 0.0;

                for (int i = 0; i < _LightCount && i < MAX_LIGHTS; i++)
                {
                    float3 lightPos = _LightPositions[i].xyz;
                    float  range    = _LightRanges[i];
                    float  dist     = distance(worldPos, lightPos);

                    float wobble         = WobbleNoise(worldPos, lightPos, _Time.y) * _WobbleAmount;
                    float effectiveRange = range + wobble;

                    float softness  = max(_EdgeSoftness, 0.001);
                    float innerEdge = effectiveRange - softness * 0.5;
                    float outerEdge = effectiveRange + softness * 0.5;

                    float insideThisLight = 1.0 - smoothstep(innerEdge, outerEdge, dist);

                    int urpIndex = (int)_LightIndices[i];
                    if (urpIndex >= 0)
                    {
                        float shadowSoftRadius = _EdgeSoftness * 0.3;
                        float shadowAtten      = SampleShadowSoft(urpIndex, worldPos, lightPos, shadowSoftRadius);
                        insideThisLight *= shadowAtten;
                    }

                    inLight = max(inLight, insideThisLight);
                }

                float inShadow = 1.0 - inLight;

                float3 litColor      = sceneColor.rgb * _LightColor.rgb;
                float3 shadowedColor = sceneColor.rgb * _ShadowColor.rgb;

                float3 finalColor = lerp(shadowedColor, litColor, inLight * _LightColor.a);
                finalColor = lerp(sceneColor.rgb, finalColor,
                    saturate(inShadow * _ShadowColor.a + inLight * _LightColor.a));

                return float4(finalColor, sceneColor.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
