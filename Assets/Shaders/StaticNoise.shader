Shader "LaminarFlow/StaticNoise"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.12, 0.1, 1)
        _NoiseColor ("Noise Color", Color) = (0.3, 0.35, 0.3, 1)
        _Speed ("Speed", Range(1, 50)) = 15
        _Scale ("Scale", Range(10, 500)) = 200
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.3
        _ScanlineCount ("Scanline Count", Range(50, 500)) = 200
        _FlickerSpeed ("Flicker Speed", Range(0, 20)) = 5
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.5)) = 0.1
        _HorizontalBands ("Horizontal Bands", Range(0, 1)) = 0.2
        _GhostingIntensity ("Ghosting", Range(0, 0.5)) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "StaticNoise"
            
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
                float4 _BaseColor;
                float4 _NoiseColor;
                float _Speed;
                float _Scale;
                float _ScanlineIntensity;
                float _ScanlineCount;
                float _FlickerSpeed;
                float _FlickerIntensity;
                float _HorizontalBands;
                float _GhostingIntensity;
            CBUFFER_END
            
            // Hash functions for noise
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            
            float hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
            // Value noise
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                float2 u = f * f * (3.0 - 2.0 * f);
                
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            
            // FBM noise for more organic look
            float fbm(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _Speed;
                
                // Base static noise
                float2 noiseUV = uv * _Scale;
                float n1 = hash(floor(noiseUV) + floor(time * 60));
                float n2 = hash(floor(noiseUV * 0.5) + floor(time * 30));
                float n3 = noise(noiseUV * 0.25 + time);
                
                // Combine noise layers
                float staticNoise = n1 * 0.6 + n2 * 0.3 + n3 * 0.1;
                
                // Horizontal banding (like tracking issues)
                float band = sin(uv.y * 50.0 + time * 0.3) * 0.5 + 0.5;
                band *= sin(uv.y * 7.0 - time * 0.1) * 0.5 + 0.5;
                staticNoise += band * _HorizontalBands;
                
                // Scanlines
                float scanline = sin(uv.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                scanline = pow(scanline, 2.0);
                float scanlineEffect = 1.0 - scanline * _ScanlineIntensity;
                
                // Rolling bar (like old TV interference)
                float barY = frac(time * 0.05);
                float bar = smoothstep(barY - 0.05, barY, uv.y) * (1.0 - smoothstep(barY, barY + 0.1, uv.y));
                staticNoise += bar * 0.3;
                
                // Global flicker
                float flicker = 1.0 + sin(time * _FlickerSpeed) * _FlickerIntensity;
                flicker *= 1.0 + hash(float2(floor(time * 10), 0)) * _FlickerIntensity;
                
                // Ghosting / horizontal smear
                float ghost = noise(float2(uv.x * 100 + time * 2, uv.y * 10));
                ghost = smoothstep(0.4, 0.6, ghost) * _GhostingIntensity;
                
                // Color output
                float brightness = staticNoise * scanlineEffect * flicker;
                brightness = saturate(brightness + ghost);
                
                // Occasional bright specs
                float spec = hash(floor(noiseUV * 2) + floor(time * 120));
                if (spec > 0.995)
                {
                    brightness = 1.0;
                }
                
                // Final color
                float3 color = lerp(_BaseColor.rgb, _NoiseColor.rgb, brightness);
                
                // Vignette
                float2 centered = uv - 0.5;
                float vignette = 1.0 - dot(centered, centered) * 0.5;
                color *= vignette;
                
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
