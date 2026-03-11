Shader "Custom/OccludedOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlinePower ("Outline Thickness", Range(0.1, 8.0)) = 2.0
        _OutlineIntensity ("Outline Intensity", Range(1.0, 10.0)) = 3.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        
        Pass
        {
            Name "FresnelOutline"
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlinePower;
                float _OutlineIntensity;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetCameraPositionWS() - positionWS;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                
                // Fresnel effect (JUST LIKE IN MY CMP301, OMG UNI WAS USEFUL)
                float fresnel = 1.0 - saturate(dot(normal, viewDir));
                fresnel = pow(fresnel, _OutlinePower);
                
                // Output color
                half4 color = _OutlineColor;
                color.rgb *= _OutlineIntensity;
                color.a = fresnel;
                
                return color;
            }
            
            ENDHLSL
        }
    }
}
