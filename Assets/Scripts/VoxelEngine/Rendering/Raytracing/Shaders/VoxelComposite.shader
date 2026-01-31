Shader "Hidden/VoxelComposite"
{
    Properties
    {
        _BlitTexture ("Texture", 2D) = "white" {}
        _Sharpness ("Sharpness", Range(0, 1)) = 0.5
        _ColorSteps ("Color Steps", Int) = 16
        
        // [NEW: Outline Settings]
        _OutlineThickness ("Outline Thickness", Float) = 1.0
        _OutlineThreshold ("Outline Threshold", Float) = 0.005
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
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
            #pragma multi_compile_local _ _UPSCALING_FSR 
            #pragma multi_compile_local _ _INDEXED_COLOR
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl" 
            
            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);
            
            SamplerState point_clamp_sampler;
            TEXTURE2D(_VoxelDepthTexture);
            
            float4 _BlitTexture_TexelSize; // x=1/w, y=1/h, z=w, w=h
            float _Sharpness;
            int _ColorSteps;
            
            // [NEW: Outline Vars]
            float _OutlineThickness;
            float _OutlineThreshold;
            float4 _OutlineColor;

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

            // --- FSR HELPER FUNCTIONS (UNCHANGED) ---
            float3 FsrMin3(float3 a, float3 b, float3 c) { return min(a, min(b, c)); }
            float3 FsrMax3(float3 a, float3 b, float3 c) { return max(a, max(b, c)); }
            float FsrLuma(float3 rgb) { return dot(rgb, float3(0.5, 0.5, 0.5)); } 

            float3 FsrEasu(float2 uv) {
                float2 texSize = _BlitTexture_TexelSize.zw;
                float2 invTexSize = _BlitTexture_TexelSize.xy;
                float3 cF = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv, 0).rgb;
                return cF;
            }

            float3 FsrRcas(float3 col, float2 uv) {
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

            float2 SnapUV(float2 uv) {
                float2 texSize = _BlitTexture_TexelSize.zw;
                float2 pixel = uv * texSize;
                return (floor(pixel) + 0.5) * _BlitTexture_TexelSize.xy;
            }

            FragOutput Frag(Varyings input)
            {
                FragOutput output;
                float3 col;
                float alpha;
                float2 uv = input.uv;

                #if defined(_UPSCALING_FSR)
                    col = FsrEasu(uv);
                    col = FsrRcas(col, uv);
                    alpha = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_BlitTexture, uv, 0).a;
                #else
                    float2 snappedUV = SnapUV(uv);
                    float4 rawCol = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, snappedUV);
                    col = rawCol.rgb;
                    alpha = rawCol.a;
                    uv = snappedUV; // Use snapped UV for depth consistency
                #endif

                // --- [NEW: OUTLINE DETECTION] ---
                float depthC = SAMPLE_TEXTURE2D(_VoxelDepthTexture, sampler_BlitTexture, uv).r;
                // [CHANGE] Use uniform _OutlineThickness
                float2 texel = _BlitTexture_TexelSize.xy * _OutlineThickness;

                float depthN = SAMPLE_TEXTURE2D(_VoxelDepthTexture, sampler_BlitTexture, uv + float2(0, texel.y)).r;
                float depthE = SAMPLE_TEXTURE2D(_VoxelDepthTexture, sampler_BlitTexture, uv + float2(texel.x, 0)).r;

                // Simple gradient check
                float dDiff = length(float2(depthC - depthN, depthC - depthE));
                
                // [CHANGE] Use uniform _OutlineThreshold
                float isEdge = step(_OutlineThreshold, dDiff);

                if (isEdge > 0.5)
                {
                    col = _OutlineColor.rgb; 
                }

                #if defined(_INDEXED_COLOR)
                    // Quantize colors for retro feel (applied after outline)
                    float3 srgb = LinearToSRGB(col);
                    srgb = floor(srgb * _ColorSteps) / _ColorSteps;
                    col = SRGBToLinear(srgb);
                #endif
                
                output.color = float4(saturate(col), alpha);
                if (output.color.a <= 0.0) discard;

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