using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using VoxelEngine.Core.Streaming;

namespace VoxelEngine.Core.Editing
{
    public class VoxelDebrisManager : MonoBehaviour
    {
        [Header("References")]
        public StructuralIntegrityAnalyzer analyzer;
        public ComputeShader transferShader;

        [Header("Settings")]
        [Tooltip("Padding in voxels to add around the debris bounds.")]
        public int voxelPadding = 2;
        
        [Tooltip("Minimum resolution for debris volumes (power of 2 recommended).")]
        public int minResolution = 32;

        private void Start()
        {
            if (analyzer != null)
            {
                analyzer.OnAnalysisCompleted += HandleAnalysisCompleted;
            }
        }

        private void OnDestroy()
        {
            if (analyzer != null)
            {
                analyzer.OnAnalysisCompleted -= HandleAnalysisCompleted;
            }
        }

        private void HandleAnalysisCompleted(VoxelVolume sourceVol, Dictionary<uint, List<Vector3>> debrisIslands)
        {
            if (debrisIslands == null || debrisIslands.Count == 0) return;
            if (VoxelVolumePool.Instance == null)
            {
                Debug.LogWarning("[VoxelDebrisManager] VoxelVolumePool instance not found.");
                return;
            }

            float sourceVoxelSize = sourceVol.WorldSize / sourceVol.Resolution;

            foreach (var kvp in debrisIslands)
            {
                uint islandId = kvp.Key;
                List<Vector3> islandVoxels = kvp.Value;

                if (islandVoxels.Count == 0) continue;

                CreateDebrisVolume(sourceVol, islandId, islandVoxels, sourceVoxelSize);
            }
        }

        private void CreateDebrisVolume(VoxelVolume sourceVol, uint islandId, List<Vector3> voxels, float voxelSize)
        {
            // 1. Calculate Bounds
            Vector3 min = voxels[0];
            Vector3 max = voxels[0];

            for (int i = 1; i < voxels.Count; i++)
            {
                min = Vector3.Min(min, voxels[i]);
                max = Vector3.Max(max, voxels[i]);
            }

            // Apply padding
            float paddingWorld = voxelPadding * voxelSize;
            min -= Vector3.one * paddingWorld;
            max += Vector3.one * paddingWorld;

            // Calculate World Origin (Corner) to align with grid
            // min is currently Voxel Center. We want Voxel Corner.
            Vector3 worldOrigin = min - (Vector3.one * voxelSize * 0.5f);

            // 2. Determine Cubic Size
            Vector3 sizeVec = max - min;
            float maxSize = Mathf.Max(sizeVec.x, Mathf.Max(sizeVec.y, sizeVec.z));
            
            // 3. Determine Resolution
            int targetResolution = Mathf.CeilToInt(maxSize / voxelSize);
            targetResolution = Mathf.NextPowerOfTwo(Mathf.Max(targetResolution, minResolution));
            
            float finalSize = targetResolution * voxelSize;

            // 4. Instantiate Volume
            VoxelVolume debrisVol = VoxelVolumePool.Instance.GetVolume(
                worldOrigin, 
                finalSize, 
                -1, // Default nodes
                -1, // Default bricks
                targetResolution
            );

            if (debrisVol != null)
            {
                debrisVol.gameObject.name = $"Debris_{sourceVol.name}_{islandId}";
                Debug.Log($"[VoxelDebrisManager] Created Debris Volume ID:{islandId} Res:{targetResolution} Size:{finalSize:F2} at {worldOrigin}");
                
                PerformTransfer(sourceVol, debrisVol, voxels, voxelSize);
            }
            else
            {
                Debug.LogWarning($"[VoxelDebrisManager] Failed to create Debris Volume for ID:{islandId}");
            }
        }

        private void PerformTransfer(VoxelVolume src, VoxelVolume dst, List<Vector3> worldPositions, float voxelSize)
        {
            if (transferShader == null || !src.IsReady || !dst.IsReady) return;

            // 1. Calculate Translation (Dst = Src - Trans)
            // Trans = (DstOrigin - SrcOrigin) / VoxelSize
            // Since we aligned DstOrigin, this should be integer (or very close).
            Vector3 transFloat = (dst.WorldOrigin - src.WorldOrigin) / voxelSize;
            int3 translation = new int3(
                Mathf.RoundToInt(transFloat.x),
                Mathf.RoundToInt(transFloat.y),
                Mathf.RoundToInt(transFloat.z)
            );

            // 2. Prepare Data Lists
            List<int3> srcVoxelIndices = new List<int3>(worldPositions.Count);
            HashSet<Vector3Int> uniqueDstBricks = new HashSet<Vector3Int>();

            foreach (var wp in worldPositions)
            {
                // Source Local Position (Floats) -> Voxel Index
                Vector3 srcLocal = (wp - src.WorldOrigin) / voxelSize;
                // wp is center (x.5). srcLocal is x.5. Floor gives x.
                int3 srcIdx = new int3(
                    Mathf.FloorToInt(srcLocal.x),
                    Mathf.FloorToInt(srcLocal.y),
                    Mathf.FloorToInt(srcLocal.z)
                );
                srcVoxelIndices.Add(srcIdx);

                // Destination Voxel Index
                int3 dstIdx = srcIdx - translation;
                
                // Determine Dst Brick
                Vector3Int dstBrick = new Vector3Int(
                    Mathf.FloorToInt(dstIdx.x / 4.0f),
                    Mathf.FloorToInt(dstIdx.y / 4.0f),
                    Mathf.FloorToInt(dstIdx.z / 4.0f)
                );
                uniqueDstBricks.Add(dstBrick);
            }

            int voxelCount = srcVoxelIndices.Count;
            int brickCount = uniqueDstBricks.Count;

            if (voxelCount == 0) return;

            // 3. Setup Buffers
            ComputeBuffer voxelListBuffer = new ComputeBuffer(voxelCount, 12); // int3
            voxelListBuffer.SetData(srcVoxelIndices.ToArray());

            ComputeBuffer brickListBuffer = new ComputeBuffer(brickCount, 12); // int3
            int3[] brickArray = uniqueDstBricks.Select(b => new int3(b.x, b.y, b.z)).ToArray();
            brickListBuffer.SetData(brickArray);

            // 4. Kernel: AllocateDebrisBricks
            int kernelAlloc = transferShader.FindKernel("AllocateDebrisBricks");
            
            // Set Dst Buffers (RW)
            transferShader.SetBuffer(kernelAlloc, "_DstNodeBuffer", dst.NodeBuffer);
            transferShader.SetBuffer(kernelAlloc, "_DstPayloadBuffer", dst.PayloadBuffer);
            transferShader.SetBuffer(kernelAlloc, "_DstBrickDataBuffer", dst.BrickDataBuffer);
            transferShader.SetBuffer(kernelAlloc, "_DstCounterBuffer", dst.CounterBuffer);
            transferShader.SetBuffer(kernelAlloc, "_DstPageTableBuffer", dst.BufferManager.PageTableBuffer);
            
            transferShader.SetInt("_DstResolution", dst.Resolution);
            transferShader.SetInt("_DstNodeOffset", dst.BufferManager.PageTableOffset); 
            transferShader.SetInt("_DstPayloadOffset", dst.BufferManager.PageTableOffset);
            transferShader.SetInt("_DstBrickOffset", dst.BufferManager.BrickDataOffset);
            transferShader.SetInt("_DstMaxBricks", dst.MaxBricks);

            // Set Inputs
            transferShader.SetBuffer(kernelAlloc, "_TargetBrickList", brickListBuffer);
            transferShader.SetInt("_TargetBrickCount", brickCount);

            int groupsAlloc = Mathf.CeilToInt(brickCount / 64.0f);
            transferShader.Dispatch(kernelAlloc, groupsAlloc, 1, 1);

            // 5. Kernel: TransferVoxels
            int kernelTransfer = transferShader.FindKernel("TransferVoxels");

            // Set Src Buffers (ReadOnly)
            transferShader.SetBuffer(kernelTransfer, "_SrcNodeBuffer", src.NodeBuffer);
            transferShader.SetBuffer(kernelTransfer, "_SrcPayloadBuffer", src.PayloadBuffer);
            transferShader.SetBuffer(kernelTransfer, "_SrcBrickDataBuffer", src.BrickDataBuffer); 
            transferShader.SetBuffer(kernelTransfer, "_SrcPageTableBuffer", src.BufferManager.PageTableBuffer);
            
            transferShader.SetInt("_SrcResolution", src.Resolution);
            transferShader.SetInt("_SrcNodeOffset", src.BufferManager.PageTableOffset);
            transferShader.SetInt("_SrcPayloadOffset", src.BufferManager.PageTableOffset);
            transferShader.SetInt("_SrcBrickOffset", src.BufferManager.BrickDataOffset);

            // Set Dst Buffers (RW)
            transferShader.SetBuffer(kernelTransfer, "_DstNodeBuffer", dst.NodeBuffer);
            transferShader.SetBuffer(kernelTransfer, "_DstPayloadBuffer", dst.PayloadBuffer);
            transferShader.SetBuffer(kernelTransfer, "_DstBrickDataBuffer", dst.BrickDataBuffer);
            transferShader.SetBuffer(kernelTransfer, "_DstPageTableBuffer", dst.BufferManager.PageTableBuffer);
            
            transferShader.SetInt("_DstResolution", dst.Resolution);
            transferShader.SetInt("_DstNodeOffset", dst.BufferManager.PageTableOffset);
            transferShader.SetInt("_DstPayloadOffset", dst.BufferManager.PageTableOffset);
            transferShader.SetInt("_DstBrickOffset", dst.BufferManager.BrickDataOffset);

            // Set Inputs
            transferShader.SetBuffer(kernelTransfer, "_TargetVoxelList", voxelListBuffer);
            transferShader.SetInt("_TargetVoxelCount", voxelCount);
            transferShader.SetInts("_Translation", new int[] { translation.x, translation.y, translation.z });

            int groupsTransfer = Mathf.CeilToInt(voxelCount / 64.0f);
            transferShader.Dispatch(kernelTransfer, groupsTransfer, 1, 1);

            // 6. Cleanup & Notify
            voxelListBuffer.Release();
            brickListBuffer.Release();

            ActivateDebris(dst);
        }

        private void ActivateDebris(VoxelVolume vol)
        {            
            Debug.Log($"[VoxelDebrisManager] Activated Debris {vol.name}");
        }
    }
}