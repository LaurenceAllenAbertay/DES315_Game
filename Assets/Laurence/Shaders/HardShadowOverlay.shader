﻿Shader "Hidden/HardShadowOverlay"
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
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            // Maximum lights
            #define MAX_LIGHTS 16
            
            // Blit texture (provided by URP's Blitter)
            TEXTURE2D_X(_BlitTexture);
            
            // Parameters from C#
            float4 _ShadowColor;
            float4 _LightColor;
            int _LightCount;
            float4 _LightPositions[MAX_LIGHTS];
            float _LightRanges[MAX_LIGHTS];
            float _LightIndices[MAX_LIGHTS];
            
            // Wobble parameters
            float _WobbleAmount;     // How much the edge wobbles (in world units)
            float _WobbleSpeed;      // Animation speed
            float _WobbleFrequency;  // How many wobbles around the edge
            float _EdgeSoftness;     // Width of the soft/blurry edge
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Light GetAdditionalLightGlobal(int lightIndex, float3 positionWS)
            {
                Light light = GetAdditionalPerObjectLight(lightIndex, positionWS);

                #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                    half4 occlusionProbeChannels = _AdditionalLightsBuffer[lightIndex].occlusionProbeChannels;
                #else
                    half4 occlusionProbeChannels = _AdditionalLightsOcclusionProbes[lightIndex];
                #endif

                light.shadowAttenuation = AdditionalLightShadow(lightIndex, positionWS, light.direction, half4(1, 1, 1, 1), occlusionProbeChannels);

                #if defined(_LIGHT_COOKIES)
                    real3 cookieColor = SampleAdditionalLightCookie(lightIndex, positionWS);
                    light.color *= cookieColor;
                #endif

                return light;
            }

            // Simple noise function using layered sine waves
            float WobbleNoise(float3 worldPos, float3 lightPos, float time)
            {
                // Direction from light to point (for consistent wobble around the sphere)
                float3 dir = normalize(worldPos - lightPos);
    
                // Create wobble based on spherical coordinates
                float angle1 = atan2(dir.z, dir.x);
                float angle2 = asin(dir.y);
    
                // IMPORTANT: Use INTEGER frequency multipliers to ensure seamless wrapping
                // Non-integer values cause discontinuities at the atan2 wrap point (-π to π)
                float baseFreq = floor(_WobbleFrequency); // Ensure base is integer
    
                // Layer multiple sine waves at different frequencies and phases
                float wobble = 0.0;
    
                // Primary wobble (integer multipliers: 1x, 1x)
                wobble += sin(angle1 * baseFreq + time * _WobbleSpeed) * 0.5;
                wobble += sin(angle2 * baseFreq + time * _WobbleSpeed * 1.3) * 0.3;
    
                // Secondary detail (integer multipliers: 2x, 2x)
                wobble += sin(angle1 * baseFreq * 2.0 - time * _WobbleSpeed * 0.8) * 0.15;
                wobble += sin((angle1 + angle2) * baseFreq * 2.0 + time * _WobbleSpeed * 0.6) * 0.2;
    
                // Tertiary fine detail (integer multiplier: 4x)
                wobble += sin(angle1 * baseFreq * 4.0 + time * _WobbleSpeed * 1.1) * 0.08;
    
                // Normalize to roughly -1 to 1 range
                wobble *= 0.8;
    
                return wobble;
            }
            
            Varyings Vert(Attributes input)
            {
                Varyings output;
                
                // Fullscreen triangle
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                
                #if UNITY_UV_STARTS_AT_TOP
                    output.texcoord = float2(uv.x, 1.0 - uv.y);
                #else
                    output.texcoord = uv;
                #endif
                
                return output;
            }
            
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                
                // Sample scene color using point sampler to avoid redefinition issues
                float4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                
                // If no lights registered, return original
                if (_LightCount <= 0)
                {
                    return sceneColor;
                }
                
                // Sample depth
                float depth = SampleSceneDepth(uv);
                
                // Skip skybox
                #if UNITY_REVERSED_Z
                    if (depth < 0.0001) return sceneColor;
                #else
                    if (depth > 0.9999) return sceneColor;
                #endif
                
                // Reconstruct world position using URP helper (handles platform differences)
                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                
                // Check if pixel is within ANY light's range
                float inLight = 0.0;
                
                for (int i = 0; i < _LightCount && i < MAX_LIGHTS; i++)
                {
                    float3 lightPos = _LightPositions[i].xyz;
                    float lightRange = _LightRanges[i];
                    
                    float dist = distance(worldPos, lightPos);
                    
                    // Calculate wobble offset for this light
                    float wobble = WobbleNoise(worldPos, lightPos, _Time.y) * _WobbleAmount;
                    
                    // Apply wobble to the effective range
                    float effectiveRange = lightRange + wobble;
                    
                    // Soft edge using smoothstep for blur effect
                    // Edge softness determines width of transition zone
                    float softness = max(_EdgeSoftness, 0.01); // Prevent division issues
                    float innerEdge = effectiveRange - softness * 0.5;
                    float outerEdge = effectiveRange + softness * 0.5;
                    
                    // smoothstep gives us a nice soft transition
                    float insideThisLight = 1.0 - smoothstep(innerEdge, outerEdge, dist);
                    
                    // NEW: Sample URP Shadows
                    int additionalLightIndex = (int)_LightIndices[i];
                    if (additionalLightIndex >= 0)
                    {
                        // Use the global additional light index, not per-object indices
                        Light light = GetAdditionalLightGlobal(additionalLightIndex, worldPos);
                        float shadowSoftness = max(_EdgeSoftness, 0.001);
                        float shadowAtten = smoothstep(0.5 - shadowSoftness * 0.5, 0.5 + shadowSoftness * 0.5, light.shadowAttenuation);
                        insideThisLight *= shadowAtten;
                    }

                    inLight = max(inLight, insideThisLight);
                }
                
                // Shadow = not in any light
                float inShadow = 1.0 - inLight;

                // Apply both light tint and shadow tint
                float3 litColor = sceneColor.rgb * _LightColor.rgb;
                float3 shadowedColor = sceneColor.rgb * _ShadowColor.rgb;

                // Blend between lit and shadowed based on light influence
                float3 finalColor = lerp(shadowedColor, litColor, inLight * _LightColor.a);
                finalColor = lerp(sceneColor.rgb, finalColor, inShadow * _ShadowColor.a + inLight * _LightColor.a);

                return float4(finalColor, 1.0);
            }
            
            ENDHLSL
        }
    }
    
    Fallback Off
}
