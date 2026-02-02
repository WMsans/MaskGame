using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using VoxelEngine.Core;
using VoxelEngine.Core.Editing;

namespace Game
{
    public class GameLoopManager : MonoBehaviour
    {
        [Header("Moving Prefab Settings")]
        [Tooltip("The prefab that moves from start to end.")]
        public GameObject movingPrefab;
        public Transform startPosition;
        public Transform endPosition;
        public float moveDuration = 2f;

        [Header("Queue Settings")]
        [Tooltip("The prefab for the queue items.")]
        public GameObject queuePrefab;
        [Tooltip("The position where the first item in the queue stands (in front of window).")]
        public Transform queueStartPosition;
        [Tooltip("Offset between items in the queue.")]
        public Vector3 queueOffset = new Vector3(0, 0, 1.5f);
        public int queueCount = 5;
        public float queueMoveDuration = 1f;

        private GameObject _movingInstance;
        private List<GameObject> _queueInstances = new List<GameObject>();

        private void Start()
        {
            InitializeQueue();
            SpawnAndMovePrefab();
        }

        /// <summary>
        /// Public method to be called by the "Complete" button.
        /// </summary>
        public void OnCompleteButtonClicked()
        {
            RegenerateTerrain();
            AdvanceQueue();
            SpawnAndMovePrefab();
        }

        private void SpawnAndMovePrefab()
        {
            if (movingPrefab == null || startPosition == null || endPosition == null)
            {
                Debug.LogWarning("[GameLoopManager] Moving Prefab settings are missing.");
                return;
            }

            // Cleanup previous instance if it exists
            if (_movingInstance != null)
            {
                // Kill any existing tweens on the transform
                _movingInstance.transform.DOKill();
                Destroy(_movingInstance);
            }

            _movingInstance = Instantiate(movingPrefab, startPosition.position, startPosition.rotation);
            _movingInstance.transform.DOMove(endPosition.position, moveDuration).SetEase(Ease.Linear);
        }

        private void InitializeQueue()
        {
            if (queuePrefab == null || queueStartPosition == null)
            {
                Debug.LogWarning("[GameLoopManager] Queue settings are missing.");
                return;
            }

            // Clear any existing queue
            foreach (var instance in _queueInstances)
            {
                if (instance != null) Destroy(instance);
            }
            _queueInstances.Clear();

            // Spawn initial queue
            for (int i = 0; i < queueCount; i++)
            {
                Vector3 spawnPos = queueStartPosition.position + (queueOffset * i);
                GameObject instance = Instantiate(queuePrefab, spawnPos, queueStartPosition.rotation);
                _queueInstances.Add(instance);
            }
        }

        private void AdvanceQueue()
        {
            if (_queueInstances.Count == 0) return;

            // 1. Remove the first item (the one at the window)
            GameObject finishedItem = _queueInstances[0];
            _queueInstances.RemoveAt(0);
            
            if (finishedItem != null)
            {
                finishedItem.transform.DOKill();
                Destroy(finishedItem);
            }

            // 2. Move remaining items forward
            for (int i = 0; i < _queueInstances.Count; i++)
            {
                GameObject item = _queueInstances[i];
                if (item != null)
                {
                    // Target position is based on their new index
                    Vector3 targetPos = queueStartPosition.position + (queueOffset * i);
                    item.transform.DOMove(targetPos, queueMoveDuration).SetEase(Ease.OutQuad);
                }
            }

            // 3. Spawn a new item at the end of the line
            if (queuePrefab != null && queueStartPosition != null)
            {
                // Spawn at the position *after* the last current item moves
                // The new item's index will be _queueInstances.Count (which is queueCount - 1)
                int newIndex = _queueInstances.Count;
                Vector3 targetPos = queueStartPosition.position + (queueOffset * newIndex);
                
                // Optional: Spawn it further back and walk in, or just spawn in place?
                // "allowing the next in line to stand in front of window".
                // I'll spawn it at the previous last position + offset (where the last item *was*) 
                // and move it to the new last position, or just spawn it at the new last position.
                // Let's spawn it at the very end.
                
                GameObject newItem = Instantiate(queuePrefab, targetPos + queueOffset, queueStartPosition.rotation);
                newItem.transform.DOMove(targetPos, queueMoveDuration).SetEase(Ease.OutQuad);
                _queueInstances.Add(newItem);
            }
        }

        private void RegenerateTerrain()
        {
            // 1. Clear Edits
            if (VoxelEditManager.Instance != null)
            {
                Debug.Log("[GameLoopManager] Clearing Voxel Edits...");
                VoxelEditManager.Instance.transform.parent.position = Vector3.zero;
                VoxelEditManager.Instance.transform.parent.localRotation = Quaternion.identity;
                VoxelEditManager.Instance.Clear();
            }

            // 2. Force Volumes to Regenerate
            var volumes = VoxelVolumeRegistry.Volumes;
            Debug.Log($"[GameLoopManager] Regenerating {volumes.Count} volumes...");
            
            foreach (var volume in volumes)
            {
                if (volume != null && volume.gameObject.activeInHierarchy)
                {
                    volume.Regenerate();
                }
            }
        }
    }
}
