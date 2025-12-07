Shader "LaminarFlow/ToolCursor"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.8, 1.0, 0.4)
        _Strength ("Strength", Range(0, 1)) = 0
        _RingThickness ("Ring Thickness", Range(0.01, 0.3)) = 0.08
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.2)) = 0.05
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3
        _PulseAmount ("Pulse Amount", Range(0, 0.2)) = 0.05
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        
        Pass
        {
            Name "ToolCursor"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Strength;
                float _RingThickness;
                float _EdgeSoftness;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Calculate distance from center (UV 0.5, 0.5 is center)
                float2 centered = input.uv - 0.5;
                float dist = length(centered) * 2.0; // Normalize so edge is at 1.0
                
                // Pulse effect when active
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount * _Strength;
                
                // Ring parameters
                float outerRadius = 1.0;
                float innerRadius = 1.0 - _RingThickness * pulse;
                
                // Soft ring edges
                float outerEdge = 1.0 - smoothstep(outerRadius - _EdgeSoftness, outerRadius, dist);
                float innerEdge = smoothstep(innerRadius - _EdgeSoftness, innerRadius, dist);
                float ring = outerEdge * innerEdge;
                
                // Center fill when active (shows area of effect)
                float centerFill = (1.0 - smoothstep(0.0, innerRadius, dist)) * _Strength * 0.15;
                
                // Combine ring and center fill
                float alpha = ring + centerFill;
                
                // Apply base color
                half4 color = _Color;
                color.a *= alpha;
                
                // Boost brightness when active
                color.rgb *= 1.0 + _Strength * 0.5;
                
                // Inner glow/gradient when active
                float innerGlow = (1.0 - dist) * _Strength * 0.3;
                color.rgb += innerGlow * _Color.rgb;
                
                // Discard fully transparent pixels
                clip(color.a - 0.001);
                
                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
