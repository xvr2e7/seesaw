Shader "LaminarFlow/OpticalFlowHSV"
{
    Properties
    {
        _VelocityTex ("Velocity Texture", 2D) = "gray" {}
        _VelocityScale ("Velocity Scale", Range(0.1, 5)) = 1.5
        _SaturationRange ("Saturation (Min, Max)", Vector) = (0.4, 0.95, 0, 0)
        _ValueRange ("Value (Min, Max)", Vector) = (0.2, 0.9, 0, 0)
        _HueOffset ("Hue Offset", Range(0, 1)) = 0
        
        [Header(Visual Style)]
        _ColorVibrancy ("Color Vibrancy", Range(0.5, 2)) = 1.3
        _FlowIntensity ("Flow Intensity", Range(0, 3)) = 1.5
        _BackgroundColor ("Background Color", Color) = (0.08, 0.06, 0.12, 1)
        _NoiseAmount ("Noise Amount", Range(0, 0.15)) = 0.03
        _BlendSoftness ("Blend Softness", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "OpticalFlowHSV"
            
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
                float3 worldPos : TEXCOORD1;
            };
            
            TEXTURE2D(_VelocityTex);
            SAMPLER(sampler_VelocityTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _VelocityTex_ST;
                float _VelocityScale;
                float4 _SaturationRange;
                float4 _ValueRange;
                float _HueOffset;
                float _ColorVibrancy;
                float _FlowIntensity;
                float4 _BackgroundColor;
                float _NoiseAmount;
                float _BlendSoftness;
            CBUFFER_END
            
            // Hash function for noise
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            
            // Smooth noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            
            // Fractal noise for organic look
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }
            
            // HSV to RGB conversion - attempt cleaner version
            float3 HSVtoRGB(float3 hsv)
            {
                float h = frac(hsv.x) * 6.0;
                float s = saturate(hsv.y);
                float v = saturate(hsv.z);
                
                float c = v * s;
                float x = c * (1.0 - abs(fmod(h, 2.0) - 1.0));
                float m = v - c;
                
                float3 rgb;
                
                if (h < 1.0)      rgb = float3(c, x, 0);
                else if (h < 2.0) rgb = float3(x, c, 0);
                else if (h < 3.0) rgb = float3(0, c, x);
                else if (h < 4.0) rgb = float3(0, x, c);
                else if (h < 5.0) rgb = float3(x, 0, c);
                else              rgb = float3(c, 0, x);
                
                return rgb + m;
            }
            
            // Standard optical flow color wheel
            float3 VelocityToColor(float2 velocity, float magnitude)
            {
                // Calculate angle from velocity (atan2 gives -PI to PI)
                float angle = atan2(velocity.y, velocity.x);
                
                // Convert to 0-1 range for hue
                // Standard optical flow: 0 = right (red), 0.25 = up, 0.5 = left (cyan), 0.75 = down
                float hue = (angle / (2.0 * 3.14159265)) + 0.5;
                hue = frac(hue + _HueOffset);
                
                // Scale magnitude for visualization
                float scaledMag = saturate(magnitude * _VelocityScale * _FlowIntensity);
                
                // Saturation based on magnitude - more movement = more color
                float satMin = _SaturationRange.x;
                float satMax = _SaturationRange.y;
                float saturation = lerp(satMin, satMax, scaledMag);
                
                // Value (brightness) - ensure we always have some visibility
                float valMin = _ValueRange.x;
                float valMax = _ValueRange.y;
                float value = lerp(valMin, valMax, scaledMag);
                
                return HSVtoRGB(float3(hue, saturation, value));
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _VelocityTex);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample velocity texture
                float4 velData = SAMPLE_TEXTURE2D(_VelocityTex, sampler_VelocityTex, input.uv);
                
                // Decode velocity from texture (stored as 0-1, convert back to -1 to 1)
                float2 velocity = (velData.rg - 0.5) * 2.0;
                float magnitude = velData.b;
                float hasData = velData.a;
                
                // Add organic noise
                float2 noiseUV = input.uv * 30.0 + _Time.y * 0.05;
                float n = fbm(noiseUV);
                
                // Convert velocity to color
                float3 flowColor = VelocityToColor(velocity, magnitude);
                
                // Add noise variation to hue for organic feel
                float3 noisyHSV = float3(
                    frac(atan2(velocity.y, velocity.x) / (2.0 * 3.14159265) + 0.5 + _HueOffset + n * _NoiseAmount),
                    lerp(_SaturationRange.x, _SaturationRange.y, saturate(magnitude * _VelocityScale * _FlowIntensity)),
                    lerp(_ValueRange.x, _ValueRange.y, saturate(magnitude * _VelocityScale * _FlowIntensity))
                );
                flowColor = HSVtoRGB(noisyHSV);
                
                // Apply vibrancy
                flowColor = pow(max(flowColor, 0.001), 1.0 / _ColorVibrancy);
                
                // Calculate blend factor - areas with more data/movement show more color
                float blendFactor = saturate(hasData * (magnitude * _FlowIntensity + _BlendSoftness));
                
                // Blend with background
                float3 finalColor = lerp(_BackgroundColor.rgb, flowColor, blendFactor);
                
                // Subtle vignette for depth
                float2 centered = input.uv - 0.5;
                float vignette = 1.0 - dot(centered, centered) * 0.2;
                finalColor *= vignette;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
