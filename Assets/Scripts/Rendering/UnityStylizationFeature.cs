using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace VoxelEngine.Core.Rendering
{
    public class UnityStylizationFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Header("Shader References")]
            public Shader compositeShader; // Assign 'Hidden/VoxelComposite' here

            [Header("Timing")]
            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

            [Header("Quality")]
            public VoxelRaytracerFeature.QualityLevel qualityLevel = VoxelRaytracerFeature.QualityLevel.High;
            [Range(0.1f, 1.0f)]
            public float renderScale = 1.0f;

            [Header("Upscaling")]
            public VoxelRaytracerFeature.UpscalingMode upscalingMode = VoxelRaytracerFeature.UpscalingMode.SpatialFSR;
            [Range(0.0f, 1.0f)] public float sharpness = 0.5f;

            [Header("Celluloid Outline")]
            [Range(0.0f, 4.0f)] public float outlineThickness = 1.5f;
            [Range(0.0001f, 0.1f)] public float outlineThreshold = 0.001f;
            public Color outlineColor = Color.black;

            [Header("Retro Look")]
            public bool indexedColor = false;
            [Range(2, 64)]
            public int colorSteps = 8;
        }

        public Settings settings = new Settings();
        private StylizationPass _pass;
        private Material _compositeMaterial;

        public override void Create()
        {
            _pass = new StylizationPass(settings);

            // Try to find the shader automatically if not assigned
            if (settings.compositeShader == null)
                settings.compositeShader = Shader.Find("Hidden/VoxelComposite");

            if (settings.compositeShader != null)
                _compositeMaterial = new Material(settings.compositeShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.compositeShader == null || _compositeMaterial == null) return;
            
            // Only apply in Game View or Scene View
            if (renderingData.cameraData.cameraType != CameraType.Game && 
                renderingData.cameraData.cameraType != CameraType.SceneView) return;

            _pass.UpdateSettings(settings);
            _pass.Setup(_compositeMaterial);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_compositeMaterial);
        }

        class StylizationPass : ScriptableRenderPass
        {
            private Settings _settings;
            private Material _material;

            // Property IDs (Matching VoxelComposite.shader)
            private static readonly int _SharpnessParams = Shader.PropertyToID("_Sharpness");
            private static readonly int _ColorStepsParams = Shader.PropertyToID("_ColorSteps");
            private static readonly int _OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");
            private static readonly int _OutlineThresholdID = Shader.PropertyToID("_OutlineThreshold");
            private static readonly int _OutlineColorID = Shader.PropertyToID("_OutlineColor");
            private static readonly int _JitterParams = Shader.PropertyToID("_Jitter");
            
            // We reuse the shader's depth property, but bind Camera Depth to it
            private static readonly int _VoxelDepthTextureParams = Shader.PropertyToID("_VoxelDepthTexture"); 

            private class PassData
            {
                public TextureHandle source;
                public TextureHandle destination;
                public TextureHandle lowResTarget;
                public TextureHandle cameraDepth;
                public Material material;
                public Settings settings;
            }

            public StylizationPass(Settings settings)
            {
                _settings = settings;
                renderPassEvent = settings.injectionPoint;
            }

            public void UpdateSettings(Settings newSettings)
            {
                _settings = newSettings;
                renderPassEvent = newSettings.injectionPoint;
            }

            public void Setup(Material material)
            {
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                TextureHandle cameraColor = resourceData.activeColorTexture;
                TextureHandle cameraDepth = resourceData.activeDepthTexture;
                
                if (!cameraColor.IsValid() || !cameraDepth.IsValid()) return;

                // 1. Calculate Resolution
                float scale = 1.0f;
                switch (_settings.qualityLevel)
                {
                    case VoxelRaytracerFeature.QualityLevel.High: scale = 1.0f; break;
                    case VoxelRaytracerFeature.QualityLevel.Low: scale = 0.5f; break;
                    case VoxelRaytracerFeature.QualityLevel.Custom: scale = _settings.renderScale; break;
                }

                var cameraDesc = cameraData.cameraTargetDescriptor;
                int scaledWidth = Mathf.Max(1, Mathf.RoundToInt(cameraDesc.width * scale));
                int scaledHeight = Mathf.Max(1, Mathf.RoundToInt(cameraDesc.height * scale));

                // 2. Define Low-Res Intermediate Target
                // FIX: Explicitly create TextureDesc instead of trying to cast from RenderTextureDescriptor
                TextureDesc desc = new TextureDesc(scaledWidth, scaledHeight);
                desc.colorFormat = cameraDesc.graphicsFormat;
                desc.depthBufferBits = DepthBits.None; // We don't need a depth buffer for the color target
                desc.msaaSamples = MSAASamples.None;   // Post-process targets usually don't need MSAA
                desc.name = "UnityStylization_LowRes";
                
                TextureHandle lowResColor = renderGraph.CreateTexture(desc);

                // --- PASS 1: Downsample ---
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Stylization Downsample", out var data))
                {
                    data.source = cameraColor;
                    data.lowResTarget = lowResColor;
                    
                    builder.UseTexture(data.source, AccessFlags.Read);
                    builder.SetRenderAttachment(data.lowResTarget, 0, AccessFlags.Write);
                    
                    builder.SetRenderFunc((PassData pd, RasterGraphContext ctx) =>
                    {
                        // Blit Source to bound color attachment (LowRes)
                        Blitter.BlitTexture(ctx.cmd, pd.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                    });
                }

                // --- PASS 2: Composite (Upscale + Effects) ---
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Stylization Upscale & FX", out var data))
                {
                    data.lowResTarget = lowResColor;
                    data.destination = cameraColor;
                    data.cameraDepth = cameraDepth;
                    data.material = _material;
                    data.settings = _settings;

                    builder.UseTexture(data.lowResTarget, AccessFlags.Read);
                    builder.UseTexture(data.cameraDepth, AccessFlags.Read);
                    builder.SetRenderAttachment(data.destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData pd, RasterGraphContext ctx) =>
                    {
                        Material mat = pd.material;

                        // Bind Camera Depth to the shader property intended for Voxel Depth
                        mat.SetTexture(_VoxelDepthTextureParams, pd.cameraDepth);

                        // Transfer Settings
                        mat.SetFloat(_SharpnessParams, pd.settings.sharpness);
                        mat.SetFloat(_OutlineThicknessID, pd.settings.outlineThickness);
                        mat.SetFloat(_OutlineThresholdID, pd.settings.outlineThreshold);
                        mat.SetColor(_OutlineColorID, pd.settings.outlineColor);
                        mat.SetInt(_ColorStepsParams, pd.settings.colorSteps);
                        mat.SetVector(_JitterParams, Vector4.zero); 

                        // Keywords
                        if (pd.settings.upscalingMode == VoxelRaytracerFeature.UpscalingMode.SpatialFSR)
                            mat.EnableKeyword("_UPSCALING_FSR");
                        else
                            mat.DisableKeyword("_UPSCALING_FSR");

                        if (pd.settings.indexedColor)
                            mat.EnableKeyword("_INDEXED_COLOR");
                        else
                            mat.DisableKeyword("_INDEXED_COLOR");

                        // Blit LowRes -> Destination (Screen) using the material
                        Blitter.BlitTexture(ctx.cmd, pd.lowResTarget, new Vector4(1, 1, 0, 0), mat, 0);
                    });
                }
            }
        }
    }
}