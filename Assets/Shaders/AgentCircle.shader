Shader "LaminarFlow/AgentCircle"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 0.6)
        _Softness ("Edge Softness", Range(0, 0.5)) = 0.15
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.3
        _HighlightIntensity ("Highlight Intensity", Range(0, 1)) = 0.4
        _ShadowOffset ("Shadow Offset", Vector) = (0.15, -0.15, 0, 0)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            Name "AgentCircle"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Per-instance color array (max batch size is 1023)
            // Using different name to avoid conflict with material property
            float4 _Colors[1023];

            CBUFFER_START(UnityPerMaterial)
                float _Softness;
                float _ShadowIntensity;
                float _HighlightIntensity;
                float4 _ShadowOffset;
            CBUFFER_END

            Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                // Get per-instance color from array using instance ID
                output.color = float4(1, 1, 1, 0.6);
                #ifdef UNITY_INSTANCING_ENABLED
                    output.color = _Colors[instanceID];
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Get instanced color from vertex shader
                float4 baseColor = input.color;

                // Calculate distance from center (UV is 0-1, so center is 0.5, 0.5)
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0; // Normalize so edge is at 1.0

                // Soft circle falloff
                float softness = max(_Softness, 0.001);
                float alpha = 1.0 - smoothstep(1.0 - softness, 1.0, dist);

                // Discard pixels outside circle for better performance
                clip(alpha - 0.01);

                // === PSEUDO-3D LIGHTING EFFECTS ===

                // Simulate top-left highlight
                float2 highlightDir = normalize(float2(-0.3, 0.5));
                float highlightDot = dot(normalize(centered), highlightDir);
                float highlight = smoothstep(-0.3, 0.8, highlightDot) * (1.0 - dist);

                // Simulate bottom-right shadow (opposite of highlight)
                float2 shadowDir = normalize(float2(0.4, -0.4));
                float shadowDot = dot(normalize(centered), shadowDir);
                float shadow = smoothstep(-0.2, 0.9, shadowDot) * (1.0 - dist * 0.7);

                // Apply lighting to base color
                half3 finalColor = baseColor.rgb;

                // Add highlight (brighten)
                finalColor += highlight * _HighlightIntensity * 0.5;

                // Add shadow (darken)
                finalColor -= shadow * _ShadowIntensity * 0.3;

                // Subtle edge darkening for depth
                float edgeDarken = smoothstep(0.7, 1.0, dist);
                finalColor *= 1.0 - edgeDarken * 0.2;

                half4 color = half4(finalColor, baseColor.a * alpha);

                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
