using UnityEngine;
using VoxelEngine.Core.Streaming;
using VoxelEngine.Core;

namespace MaskGame
{
    public class MaskGameController : MonoBehaviour
    {
        [Header("Single Volume Settings")]
        public int resolution = 128;
        public float worldSize = 128f;
        public Vector3 position = Vector3.zero;

        private VoxelVolume _activeVolume;

        private void Start()
        {
            // We need to wait for VoxelVolumePool to initialize. 
            // Since VoxelVolumePool initializes in Awake, it should be ready by Start.
            if (VoxelVolumePool.Instance == null)
            {
                Debug.LogError("[MaskGameController] VoxelVolumePool not found!");
                return;
            }

            InitializeSingleVolume();
        }

        private void InitializeSingleVolume()
        {
            Debug.Log($"[MaskGameController] Initializing Single Volume (Res: {resolution}, Size: {worldSize})...");
            
            // Request a volume from the pool
            // Nodes: Default (50k) is enough for 128^3
            // Bricks: Default (40k) is enough for 128^3
            _activeVolume = VoxelVolumePool.Instance.GetVolume(position, worldSize, resolution: resolution);

            if (_activeVolume != null)
            {
                _activeVolume.name = "Single_World_Volume";
                Debug.Log("[MaskGameController] Volume initialized successfully.");
            }
            else
            {
                Debug.LogError("[MaskGameController] Failed to get volume from pool. Check Pool Size/Memory.");
            }
        }
    }
}
