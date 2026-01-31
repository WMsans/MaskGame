using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core;
using System.Collections.Generic;
using Unity.Collections;
using System.Linq;
using Unity.Mathematics;

namespace VoxelEngine.Core.Editing
{
    public class StructuralCleaner : MonoBehaviour
    {
        public ComputeShader voxelModifierShader;
        public StructuralIntegrityAnalyzer analyzer;

        [Header("Settings")]
        [Tooltip("If true, removes neighbors of floating voxels to ensure clean breaks and remove diagonal artifacts.")]
        public bool erodeFloatingVoxels = true;

        private void Start()
        {
            if (analyzer != null)
                analyzer.OnAnalysisCompleted += HandleAnalysisCompleted;
        }

        private void OnDestroy()
        {
            if (analyzer != null)
                analyzer.OnAnalysisCompleted -= HandleAnalysisCompleted;
        }

        private void HandleAnalysisCompleted(VoxelVolume vol, Dictionary<uint, List<Vector3>> debrisIslands)
        {
            if (debrisIslands == null || debrisIslands.Count == 0) return;
            List<Vector3> floatingVoxels = debrisIslands.Values.SelectMany(x => x).ToList();

            if (floatingVoxels == null || floatingVoxels.Count == 0) return;
            if (voxelModifierShader == null || !vol.IsReady) return;

            // 1. Prepare Data
            float worldToVoxelScale = vol.Resolution / vol.WorldSize;
            
            // Use a HashSet to store unique voxel indices (prevents duplicates from neighbor expansion)
            HashSet<Vector3Int> voxelsToRemove = new HashSet<Vector3Int>();
            HashSet<Vector3Int> uniqueBricks = new HashSet<Vector3Int>();

            int resBricks = vol.Resolution / 4;
            Vector3Int maxBrickIdx = new Vector3Int(resBricks - 1, resBricks - 1, resBricks - 1);

            // Define neighbors for erosion (6-way)
            Vector3Int[] neighborOffsets = new Vector3Int[]
            {
                Vector3Int.zero, // Include self
                Vector3Int.up, Vector3Int.down, 
                Vector3Int.left, Vector3Int.right,
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
            };

            foreach (var worldPos in floatingVoxels)
            {
                // Convert World -> Local Voxel Space
                Vector3 localPos = (worldPos - vol.WorldOrigin) * worldToVoxelScale;
                Vector3Int centerIdx = Vector3Int.FloorToInt(localPos);

                // Expand selection if erosion is enabled
                int iterations = erodeFloatingVoxels ? 7 : 1; 

                for (int i = 0; i < iterations; i++)
                {
                    Vector3Int targetIdx = centerIdx + neighborOffsets[i];
                    
                    // Bounds Check
                    if (targetIdx.x >= 0 && targetIdx.y >= 0 && targetIdx.z >= 0 &&
                        targetIdx.x < vol.Resolution && targetIdx.y < vol.Resolution && targetIdx.z < vol.Resolution)
                    {
                        voxelsToRemove.Add(targetIdx);
                    }
                }
            }

            // Convert unique indices back to centered local positions and calculate bricks
            List<Vector3> localVoxelPositions = new List<Vector3>(voxelsToRemove.Count);

            foreach (var vIdx in voxelsToRemove)
            {
                // Add 0.5 to center the float position in the voxel
                localVoxelPositions.Add(new Vector3(vIdx.x + 0.5f, vIdx.y + 0.5f, vIdx.z + 0.5f));

                // Identify Brick
                // Calculate brick range covering this voxel (accounting for 1-voxel padding)
                // Brick B covers [B*4-1, B*4+4]. 
                int minX = Mathf.CeilToInt((vIdx.x - 4) / 4.0f);
                int maxX = Mathf.FloorToInt((vIdx.x + 1) / 4.0f);
                int minY = Mathf.CeilToInt((vIdx.y - 4) / 4.0f);
                int maxY = Mathf.FloorToInt((vIdx.y + 1) / 4.0f);
                int minZ = Mathf.CeilToInt((vIdx.z - 4) / 4.0f);
                int maxZ = Mathf.FloorToInt((vIdx.z + 1) / 4.0f);

                // Clamp to volume bounds
                minX = Mathf.Max(minX, 0); maxX = Mathf.Min(maxX, maxBrickIdx.x);
                minY = Mathf.Max(minY, 0); maxY = Mathf.Min(maxY, maxBrickIdx.y);
                minZ = Mathf.Max(minZ, 0); maxZ = Mathf.Min(maxZ, maxBrickIdx.z);

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        for (int z = minZ; z <= maxZ; z++)
                            uniqueBricks.Add(new Vector3Int(x, y, z));
            }

            int voxelCount = localVoxelPositions.Count;
            int brickCount = uniqueBricks.Count;

            if (voxelCount == 0) return;

            // 2. Setup Buffers
            ComputeBuffer positionsBuffer = new ComputeBuffer(voxelCount, 12); // float3
            positionsBuffer.SetData(localVoxelPositions.ToArray());

            ComputeBuffer bricksBuffer = new ComputeBuffer(brickCount, 12); // int3
            int3[] brickArray = uniqueBricks.Select(b => new int3(b.x, b.y, b.z)).ToArray();
            bricksBuffer.SetData(brickArray);

            // 3. Dispatch Allocation (Ensure bricks exist)
            int kernelAlloc = voxelModifierShader.FindKernel("AllocateNodesList");
            SetCommonBuffers(kernelAlloc, vol);
            voxelModifierShader.SetBuffer(kernelAlloc, "_TargetBricks", bricksBuffer);
            voxelModifierShader.SetInt("_TargetBrickCount", brickCount);
            
            voxelModifierShader.SetInts("_MaxBrickIndex", new int[] {resBricks-1, resBricks-1, resBricks-1});
            voxelModifierShader.SetInts("_MinBrickIndex", new int[] {0, 0, 0});
             
            int groupsAlloc = Mathf.CeilToInt(brickCount / 64.0f);
            voxelModifierShader.Dispatch(kernelAlloc, groupsAlloc, 1, 1);

            // 4. Dispatch Extraction (Persistence) - Must happen BEFORE Removal
            int kernelExtract = voxelModifierShader.FindKernel("ExtractBricksList");
            SetCommonBuffers(kernelExtract, vol);
            voxelModifierShader.SetBuffer(kernelExtract, "_TargetBricks", bricksBuffer);
            voxelModifierShader.SetInt("_TargetBrickCount", brickCount);
            // Re-set MaxBrickIndex for safety in this kernel too
            voxelModifierShader.SetInts("_MaxBrickIndex", new int[] {resBricks-1, resBricks-1, resBricks-1});
            voxelModifierShader.SetInts("_MinBrickIndex", new int[] {0, 0, 0});

            int totalVoxelsToRead = brickCount * 216;
            GraphicsBuffer readbackBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalVoxelsToRead, 4);
            voxelModifierShader.SetBuffer(kernelExtract, "_ReadbackBuffer", readbackBuffer);

            voxelModifierShader.Dispatch(kernelExtract, groupsAlloc, 1, 1);

            // 5. Dispatch Removal
            int kernelRemove = voxelModifierShader.FindKernel("RemoveVoxelList");
            SetCommonBuffers(kernelRemove, vol);
            voxelModifierShader.SetBuffer(kernelRemove, "_TargetPositions", positionsBuffer);
            voxelModifierShader.SetInt("_TargetCount", voxelCount);
             
            int groupsRemove = Mathf.CeilToInt(voxelCount / 64.0f);
            voxelModifierShader.Dispatch(kernelRemove, groupsRemove, 1, 1);

            // 6. Readback
            AsyncGPUReadback.Request(readbackBuffer, (req) => 
            {
                // Cleanup Buffers
                positionsBuffer.Release();
                bricksBuffer.Release();
                readbackBuffer.Release();

                if (req.hasError) 
                {
                    Debug.LogError("[StructuralCleaner] GPU Readback error");
                    return;
                }

                using (NativeArray<uint> data = req.GetData<uint>())
                {
                    ProcessReadbackData(data, vol, brickArray, voxelsToRemove);
                }
            });
        }

        private void SetCommonBuffers(int kernel, VoxelVolume vol)
        {
            voxelModifierShader.SetBuffer(kernel, "_NodeBuffer", vol.NodeBuffer);
            voxelModifierShader.SetBuffer(kernel, "_PayloadBuffer", vol.PayloadBuffer);
            voxelModifierShader.SetBuffer(kernel, "_BrickDataBuffer", vol.BrickDataBuffer);
            voxelModifierShader.SetBuffer(kernel, "_CounterBuffer", vol.CounterBuffer);
            voxelModifierShader.SetBuffer(kernel, "_PageTableBuffer", vol.BufferManager.PageTableBuffer);
            voxelModifierShader.SetInt("_NodeOffset", vol.BufferManager.PageTableOffset);
            voxelModifierShader.SetInt("_PayloadOffset", vol.BufferManager.PageTableOffset);
            voxelModifierShader.SetInt("_BrickOffset", vol.BufferManager.BrickDataOffset);
            voxelModifierShader.SetInt("_MaxBricks", vol.MaxBricks);
        }

        private void ProcessReadbackData(NativeArray<uint> data, VoxelVolume vol, int3[] bricks, HashSet<Vector3Int> voxelsToRemove)
        {
            if (VoxelEditManager.Instance == null) return;

            Vector3Int volOriginBrick = VoxelEditManager.Instance.GetBrickCoordinate(vol.WorldOrigin);
            uint packedAir = PackVoxelData(MAX_SDF_RANGE, Vector3.up, 0);

            int cursor = 0;
            int editsRegistered = 0;

            for (int i = 0; i < bricks.Length; i++)
            {
                int3 b = bricks[i];
                Vector3Int localBrick = new Vector3Int(b.x, b.y, b.z);
                Vector3Int globalBrick = volOriginBrick + localBrick;

                if (cursor + 216 > data.Length) break;

                // Slice data for this brick
                uint[] brickData = data.GetSubArray(cursor, 216).ToArray();
                cursor += 216;

                bool hasChanges = false;

                // Masking: Remove floating voxels from the brick data, preserving ground
                for (int z = 0; z < 6; z++)
                {
                    for (int y = 0; y < 6; y++)
                    {
                        for (int x = 0; x < 6; x++)
                        {
                            // Calculate local voxel position (relative to volume)
                            // Brick covers [B*4-1, B*4+4]
                            // Index 0 in storage corresponds to B*4 - 1
                            int vx = (localBrick.x * 4) - 1 + x;
                            int vy = (localBrick.y * 4) - 1 + y;
                            int vz = (localBrick.z * 4) - 1 + z;
                            
                            Vector3Int vIdx = new Vector3Int(vx, vy, vz);

                            int flatIdx = z * 36 + y * 6 + x;

                            if (voxelsToRemove.Contains(vIdx))
                            {
                                // It is floating. Remove it (Set to Air).
                                brickData[flatIdx] = packedAir;
                                hasChanges = true;
                            }
                            // Else keep original data (Ground)
                        }
                    }
                }

                if (hasChanges)
                {
                    VoxelEditManager.Instance.RegisterEdit(globalBrick, brickData);
                    editsRegistered++;
                }
            }
            
            Debug.Log($"[StructuralCleaner] Successfully removed {editsRegistered} floating bricks (masked).");
        }

        private const float MAX_SDF_RANGE = 4.0f;

        private static uint PackVoxelData(float sdf, Vector3 normal, uint materialID)
        {
            uint mat = materialID & 0xFF;
            float normalizedSDF = Mathf.Clamp(sdf / MAX_SDF_RANGE, -1.0f, 1.0f);
            uint sdfInt = (uint)((normalizedSDF * 0.5f + 0.5f) * 255.0f);
            uint norm = PackNormalOct(normal);
            return mat | (sdfInt << 8) | (norm << 16);
        }

        private static uint PackNormalOct(Vector3 n)
        {
            float sum = Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z);
            if (sum < 1e-5f) return 0; 

            n /= sum;
            Vector2 oct = n.z >= 0 ? new Vector2(n.x, n.y) : (Vector2.one - new Vector2(Mathf.Abs(n.y), Mathf.Abs(n.x))) * new Vector2(n.x >= 0 ? 1 : -1, n.y >= 0 ? 1 : -1);
            
            uint x = (uint)(Mathf.Clamp01(oct.x * 0.5f + 0.5f) * 255.0f);
            uint y = (uint)(Mathf.Clamp01(oct.y * 0.5f + 0.5f) * 255.0f);
            
            return x | (y << 8);
        }
    }
}
