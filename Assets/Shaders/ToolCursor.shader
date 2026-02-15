Shader "LaminarFlow/ToolCursor"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.9, 1.0, 0.7)
        _ActiveColor ("Active Color", Color) = (0.8, 1.0, 1.0, 0.9)
        _Strength ("Strength", Range(0, 1)) = 0
        _RingThickness ("Ring Thickness", Range(0.01, 0.3)) = 0.1
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.2)) = 0.03
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 4
        _PulseAmount ("Pulse Amount", Range(0, 0.3)) = 0.15
        _ScanlineSpeed ("Scanline Speed", Range(0, 5)) = 2
        _ScanlineCount ("Scanline Count", Range(2, 20)) = 8
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
                float4 _ActiveColor;
                float _Strength;
                float _RingThickness;
                float _EdgeSoftness;
                float _PulseSpeed;
                float _PulseAmount;
                float _ScanlineSpeed;
                float _ScanlineCount;
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
                float angle = atan2(centered.y, centered.x);

                // Pulse effect when active
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount * _Strength;

                // Ring parameters
                float outerRadius = 1.0;
                float innerRadius = 1.0 - _RingThickness * pulse;

                // Soft ring edges
                float outerEdge = 1.0 - smoothstep(outerRadius - _EdgeSoftness, outerRadius, dist);
                float innerEdge = smoothstep(innerRadius - _EdgeSoftness, innerRadius, dist);
                float ring = outerEdge * innerEdge;

                // === NEW: Animated scanlines for active state ===
                float scanlines = 0.0;
                if (_Strength > 0.1)
                {
                    // Rotating scanlines that sweep around the circle
                    float angleNormalized = (angle / 6.28318) + 0.5; // Normalize to 0-1
                    float scanTime = _Time.y * _ScanlineSpeed;
                    float scanPattern = sin(angleNormalized * _ScanlineCount * 6.28318 - scanTime);
                    scanlines = smoothstep(0.3, 0.7, scanPattern) * _Strength * 0.3;
                }

                // === NEW: Radial gradient for depth ===
                float radialGradient = 1.0 - smoothstep(0.0, 1.0, dist);
                float centerGlow = radialGradient * radialGradient * _Strength * 0.25;

                // Center fill when active (shows area of effect)
                float centerFill = (1.0 - smoothstep(0.0, innerRadius, dist)) * _Strength * 0.08;

                // Combine all elements
                float alpha = ring + centerFill + centerGlow;

                // Blend between base and active color based on strength
                half4 baseColor = lerp(_Color, _ActiveColor, _Strength);
                half4 color = baseColor;
                color.a *= alpha;

                // Add scanline brightness boost to ring only
                color.rgb += scanlines * ring * _ActiveColor.rgb;

                // Boost overall brightness when active
                color.rgb *= 1.0 + _Strength * 0.4;

                // Inner glow/gradient when active
                float innerGlow = radialGradient * _Strength * 0.2;
                color.rgb += innerGlow * _ActiveColor.rgb;

                // === NEW: Edge highlight for crispness ===
                float edgeHighlight = smoothstep(0.92, 0.98, dist) * (1.0 - smoothstep(0.98, 1.0, dist));
                color.rgb += edgeHighlight * _ActiveColor.rgb * 0.5;

                // Discard fully transparent pixels
                clip(color.a - 0.001);

                return color;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
