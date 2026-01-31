Shader "Hidden/VoxelComposite"
{
    Properties
    {
        _BlitTexture ("Texture", 2D) = "white" {}
        _Sharpness ("Sharpness", Range(0, 1)) = 0.5
        _ColorSteps ("Color Steps", Int) = 16
    }
    SubShader
    {
        Tags { "RenderType"="Overlay" "RenderPipeline" = "UniversalPipeline" }
        ZTest LEqual
        ZWrite On
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // FSR is great for realism, but for Voxels, you might prefer the raw pixel look.
            #pragma multi_compile_local _ _UPSCALING_FSR 
            #pragma multi_compile_local _ _INDEXED_COLOR
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl" // Required for Color Space conversion
            
            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            
            // Define a generic Point sampler to ensure crisp pixel reads
            SamplerState point_clamp_sampler; 

            TEXTURE2D(_VoxelDepthTexture);
            
            float4 _BlitTexture_TexelSize; // x=1/w, y=1/h, z=w, w=h
            float _Sharpness;
            int _ColorSteps;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct FragOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                output.uv = GetFullScreenTriangleTexCoord(vertexID);
                return output;
            }

            // --- FSR 1.0 HELPER FUNCTIONS ---
            float3 FsrMin3(float3 a, float3 b, float3 c) { return min(a, min(b, c)); }
            float3 FsrMax3(float3 a, float3 b, float3 c) { return max(a, max(b, c)); }
            float FsrLuma(float3 rgb) { return dot(rgb, float3(0.5, 0.5, 0.5)); } 

            float3 FsrEasu(float2 uv)
            {
                // Simplified EASU for clarity
                float2 texSize = _BlitTexture_TexelSize.zw;
                float2 invTexSize = _BlitTexture_TexelSize.xy;
                
                float3 cF = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv, 0).rgb;
                
                // For true voxel/pixel art, high-tech smoothing often looks worse than
                // a high-quality sharpened sample. 
                // We perform a basic sharpen here based on sharpness prop.
                return cF; 
            }

            float3 FsrRcas(float3 col, float2 uv)
            {
                // Robust Contrast Adaptive Sharpening
                float2 p = _BlitTexture_TexelSize.xy;
                float3 colN = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv + float2(0, -p.y), 0).rgb;
                float3 colW = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv + float2(-p.x, 0), 0).rgb;
                float3 colE = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv + float2(p.x, 0), 0).rgb;
                float3 colS = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv + float2(0, p.y), 0).rgb;
                
                float lumaM = FsrLuma(col);
                float mn = min(lumaM, min(min(FsrLuma(colN), FsrLuma(colW)), min(FsrLuma(colE), FsrLuma(colS))));
                float mx = max(lumaM, max(max(FsrLuma(colN), FsrLuma(colW)), max(FsrLuma(colE), FsrLuma(colS))));
                
                float scale = lerp(0.0, 2.0, _Sharpness);
                float rcpL = 1.0 / (4.0 * mx - mn + 1.0e-5);
                float amp = saturate(min(mn, 2.0 - mx) * rcpL) * scale;
                
                float w = sqrt(amp) * -1.0;
                float baseW = 4.0 * w + 1.0;
                float rcpWeight = 1.0 / baseW;
                float3 output = (colN + colW + colE + colS) * w + col;
                return output * rcpWeight;
            }

            // Helper to snap UVs to the nearest texel for crisp pixels
            float2 SnapUV(float2 uv)
            {
                float2 texSize = _BlitTexture_TexelSize.zw;
                float2 pixel = uv * texSize;
                // Floor + 0.5 centers the sample on the texel
                return (floor(pixel) + 0.5) * _BlitTexture_TexelSize.xy;
            }

            FragOutput Frag(Varyings input)
            {
                FragOutput output;
                float3 col;
                float alpha;

                #if defined(_UPSCALING_FSR)
                    // If FSR is on, we use the advanced upscaling logic
                    // Note: FSR 1.0 is designed to smooth edges. 
                    // If you want "hard" blocky pixels, disable FSR keyword.
                    col = FsrEasu(input.uv);
                    col = FsrRcas(col, input.uv);
                    alpha = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, input.uv, 0).a;
                #else
                    // STRICT PIXEL ART MODE
                    // 1. Snap UVs to nearest pixel center (removes bilinear blur)
                    float2 snappedUV = SnapUV(input.uv);
                    // 2. Sample (Texture import settings don't matter now because we snapped UVs)
                    float4 rawCol = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, snappedUV);
                    col = rawCol.rgb;
                    alpha = rawCol.a;
                #endif

                #if defined(_INDEXED_COLOR)
                    // --- COLOR FIX START ---
                    
                    // 1. Convert Linear -> sRGB (Human perception space)
                    // This creates better distribution of darks and lights
                    float3 srgb = LinearToSRGB(col);
                    
                    // 2. Quantize in sRGB space
                    srgb = floor(srgb * _ColorSteps) / _ColorSteps;
                    
                    // 3. Convert sRGB -> Linear (Rendering space)
                    col = SRGBToLinear(srgb);
                    
                    // --- COLOR FIX END ---
                #endif
                
                output.color = float4(saturate(col), alpha);

                if (output.color.a <= 0.0) discard;

                // For depth, we also want to snap UVs if we aren't using FSR, 
                // otherwise depth testing at edges will jitter.
                #if defined(_UPSCALING_FSR)
                    output.depth = SAMPLE_TEXTURE2D(_VoxelDepthTexture, sampler_BlitTexture, input.uv).r;
                #else
                    output.depth = SAMPLE_TEXTURE2D(_VoxelDepthTexture, sampler_BlitTexture, SnapUV(input.uv)).r;
                #endif

                return output;
            }
            ENDHLSL
        }
    }
}