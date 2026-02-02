using UnityEngine;
using VoxelEngine.Core;
using VoxelEngine.Core.Streaming;
using System.IO;

namespace VoxelEngine.Editing
{
    public class VoxelProjectionCapture : MonoBehaviour
    {
        public static VoxelProjectionCapture Instance { get; private set; }

        public ComputeShader projectionShader;
        public int resolution = 512;
        public float captureSize = 64.0f;
        public Vector3 captureCenter = Vector3.zero;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        [ContextMenu("Capture Alpha Mask")]
        public void Capture()
        {
            Texture2D tex = CaptureToTexture();
            if (tex != null)
            {
                byte[] bytes = tex.EncodeToPNG();
                string path = Path.Combine(Application.dataPath, "VoxelAlphaMask.png");
                File.WriteAllBytes(path, bytes);
                Debug.Log($"Saved capture to {path}");
                
                if (Application.isPlaying) Destroy(tex);
                else DestroyImmediate(tex);
            }
        }

        public Texture2D CaptureToTexture()
        {
            if (projectionShader == null)
            {
                Debug.LogError("Assign Projection Shader!");
                return null;
            }
            if (VoxelVolumePool.Instance == null)
            {
                Debug.LogError("VoxelVolumePool not found! Ensure the Voxel System is initialized.");
                return null;
            }

            RenderTexture rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
            rt.enableRandomWrite = true;
            rt.Create();

            int kernel = projectionShader.FindKernel("CSMain");
            
            var pool = VoxelVolumePool.Instance;
            
            // Bind all necessary buffers
            projectionShader.SetBuffer(kernel, "_GlobalNodeBuffer", pool.GlobalNodeBuffer);
            projectionShader.SetBuffer(kernel, "_GlobalPayloadBuffer", pool.GlobalPayloadBuffer);
            projectionShader.SetBuffer(kernel, "_GlobalBrickDataBuffer", pool.GlobalBrickDataBuffer);
            projectionShader.SetBuffer(kernel, "_PageTableBuffer", pool.GlobalPageTableBuffer);
            projectionShader.SetBuffer(kernel, "_ChunkBuffer", pool.ChunkBuffer);
            projectionShader.SetInt("_ChunkCount", pool.VisibleChunkCount);
            
            // TLAS might be null if not using raytracing feature or if empty
            if (pool.TLASGridBuffer != null)
                projectionShader.SetBuffer(kernel, "_TLASGridBuffer", pool.TLASGridBuffer);
            if (pool.TLASChunkIndexBuffer != null)
                projectionShader.SetBuffer(kernel, "_TLASChunkIndexBuffer", pool.TLASChunkIndexBuffer);
            
            projectionShader.SetVector("_TLASBoundsMin", pool.TLASBoundsMin);
            projectionShader.SetVector("_TLASBoundsMax", pool.TLASBoundsMax);
            projectionShader.SetInt("_TLASResolution", pool.TLASResolution);
            
            projectionShader.SetTexture(kernel, "_Result", rt);
            projectionShader.SetFloat("_CaptureSize", captureSize);
            projectionShader.SetVector("_CaptureCenter", captureCenter);
            projectionShader.SetInt("_MaxIterations", 256);
            projectionShader.SetInt("_MaxMarchSteps", 128);

            int groups = Mathf.CeilToInt(resolution / 8.0f);
            projectionShader.Dispatch(kernel, groups, groups, 1);

            // Readback
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            
            rt.Release();
            return tex;
        }

        private void SaveRenderTexture(RenderTexture rt, string fileName)
        {
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] bytes = tex.EncodeToPNG();
            string path = Path.Combine(Application.dataPath, fileName);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"Saved capture to {path}");
            
            if (Application.isPlaying) Destroy(tex);
            else DestroyImmediate(tex);
        }
    }
}
