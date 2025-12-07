Shader "LaminarFlow/AgentCircle"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 0.6)
        _Softness ("Edge Softness", Range(0, 0.5)) = 0.15
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            
            CBUFFER_START(UnityPerMaterial)
                float _Softness;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                // Get instanced color
                float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                
                // Calculate distance from center (UV is 0-1, so center is 0.5, 0.5)
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0; // Normalize so edge is at 1.0
                
                // Soft circle falloff
                float softness = max(_Softness, 0.001);
                float alpha = 1.0 - smoothstep(1.0 - softness, 1.0, dist);
                
                // Discard pixels outside circle for better performance
                clip(alpha - 0.01);
                
                half4 color = baseColor;
                color.a *= alpha;
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
