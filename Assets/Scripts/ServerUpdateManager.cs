using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

/// <summary>
/// Manages block updates from the server and visualizes them with gizmos
/// OPTIONAL: Requires separate update server on port 4567 (disabled by default in IntegrationManager)
/// This is a debugging/monitoring tool - not required for core functionality
/// </summary>
public class ServerUpdateManager : MonoBehaviour
{
    [Header("Server Settings")]
    [Tooltip("URL for block updates endpoint (requires separate server on port 4567)")]
    public string blockUpdateUrl = "http://localhost:4567/updates";
    public float updateCheckInterval = 1f;
    public int maxUpdatesPerFrame = 50;
    
    [Header("Visual Settings")]
    public Material updateGizmoMaterial;
    public Color blockAddedColor = Color.green;
    public Color blockRemovedColor = Color.red;
    public Color blockModifiedColor = Color.yellow;
    public float gizmoDisplayTime = 3f;
    public float gizmoScale = 1.1f;
    
    [Header("References")]
    public TurtleWorldManager worldManager;
    
    // Update tracking
    private Dictionary<Vector3, UpdateGizmo> activeGizmos = new Dictionary<Vector3, UpdateGizmo>();
    private Queue<BlockUpdate> pendingUpdates = new Queue<BlockUpdate>();
    private long lastUpdateTimestamp = 0;
    
    [Serializable]
    public class BlockUpdate
    {
        public Vector3Int position;
        public string oldBlockType;
        public string newBlockType;
        public UpdateType updateType;
        public long timestamp;
        public string source; // "turtle", "player", "system", etc.
        
        public enum UpdateType
        {
            Added,
            Removed,
            Modified
        }
    }
    
    [Serializable]
    public class UpdateResponse
    {
        public List<BlockUpdate> updates;
        public long timestamp;
        public int totalUpdates;
    }
    
    public class UpdateGizmo
    {
        public GameObject gizmoObject;
        public float creationTime;
        public BlockUpdate.UpdateType updateType;
        public Vector3 position;
        
        public UpdateGizmo(Vector3 pos, BlockUpdate.UpdateType type)
        {
            position = pos;
            updateType = type;
            creationTime = Time.time;
        }
    }
    
    // Events
    public System.Action<BlockUpdate> OnBlockUpdated;
    public System.Action<List<BlockUpdate>> OnBatchUpdateReceived;

    private void Start()
    {
        StartCoroutine(UpdateCheckLoop());
        StartCoroutine(ProcessPendingUpdates());
        StartCoroutine(CleanupExpiredGizmos());
    }
    
    private IEnumerator UpdateCheckLoop()
    {
        while (true)
        {
            yield return StartCoroutine(CheckForUpdates());
            yield return new WaitForSeconds(updateCheckInterval);
        }
    }
    
    private IEnumerator CheckForUpdates()
    {
        string url = $"{blockUpdateUrl}?since={lastUpdateTimestamp}";
        
        using UnityWebRequest request = UnityWebRequest.Get(url);
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                UpdateResponse response = JsonUtility.FromJson<UpdateResponse>(request.downloadHandler.text);
                
                if (response?.updates != null && response.updates.Count > 0)
                {
                    Debug.Log($"Received {response.updates.Count} block updates from server");
                    
                    // Queue updates for processing
                    foreach (var update in response.updates)
                    {
                        pendingUpdates.Enqueue(update);
                    }
                    
                    lastUpdateTimestamp = response.timestamp;
                    OnBatchUpdateReceived?.Invoke(response.updates);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to parse update response: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"Failed to fetch updates: {request.error}");
        }
    }
    
    private IEnumerator ProcessPendingUpdates()
    {
        while (true)
        {
            int processedCount = 0;
            
            while (pendingUpdates.Count > 0 && processedCount < maxUpdatesPerFrame)
            {
                BlockUpdate update = pendingUpdates.Dequeue();
                ProcessBlockUpdate(update);
                processedCount++;
            }
            
            yield return new WaitForEndOfFrame();
        }
    }
    
    private void ProcessBlockUpdate(BlockUpdate update)
    {
        Vector3Int worldPos = new Vector3Int(update.position.x, update.position.y, update.position.z);
        
        // Determine update type
        BlockUpdate.UpdateType updateType = DetermineUpdateType(update);
        update.updateType = updateType;
        
        // Create visual gizmo
        CreateUpdateGizmo(worldPos, updateType);
        
        // Apply update to chunk if available
        ApplyUpdateToChunk(worldPos, update);
        
        // Trigger event
        OnBlockUpdated?.Invoke(update);
        
        Debug.Log($"Processed block update: {updateType} at {worldPos} ({update.oldBlockType} -> {update.newBlockType})");
    }
    
    private BlockUpdate.UpdateType DetermineUpdateType(BlockUpdate update)
    {
        bool hadBlock = !string.IsNullOrEmpty(update.oldBlockType) && update.oldBlockType != "air";
        bool hasBlock = !string.IsNullOrEmpty(update.newBlockType) && update.newBlockType != "air";
        
        if (!hadBlock && hasBlock)
            return BlockUpdate.UpdateType.Added;
        else if (hadBlock && !hasBlock)
            return BlockUpdate.UpdateType.Removed;
        else if (hadBlock && hasBlock && update.oldBlockType != update.newBlockType)
            return BlockUpdate.UpdateType.Modified;
        else
            return BlockUpdate.UpdateType.Modified; // Default case
    }
    
    private void CreateUpdateGizmo(Vector3 position, BlockUpdate.UpdateType updateType)
    {
        // Remove existing gizmo at this position
        if (activeGizmos.ContainsKey(position))
        {
            RemoveGizmo(position);
        }
        
        // Create new gizmo
        GameObject gizmo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gizmo.name = $"UpdateGizmo_{updateType}_{position}";
        gizmo.transform.position = position + Vector3.one * 0.5f;
        gizmo.transform.localScale = Vector3.one * gizmoScale;
        
        // Remove collider
        DestroyImmediate(gizmo.GetComponent<Collider>());
        
        // Setup material and color
        Renderer renderer = gizmo.GetComponent<Renderer>();
        Material gizmoMat = updateGizmoMaterial != null ? 
            new Material(updateGizmoMaterial) : 
            CreateDefaultGizmoMaterial();
        
        Color gizmoColor = updateType switch
        {
            BlockUpdate.UpdateType.Added => blockAddedColor,
            BlockUpdate.UpdateType.Removed => blockRemovedColor,
            BlockUpdate.UpdateType.Modified => blockModifiedColor,
            _ => Color.white
        };
        
        gizmoMat.color = gizmoColor;
        renderer.material = gizmoMat;
        
        // Add pulsing effect
        var pulseComponent = gizmo.AddComponent<GizmoPulseEffect>();
        pulseComponent.Initialize(gizmoScale, gizmoColor, updateType);
        
        // Store gizmo
        UpdateGizmo updateGizmo = new UpdateGizmo(position, updateType)
        {
            gizmoObject = gizmo
        };
        
        activeGizmos[position] = updateGizmo;
    }
    
    private Material CreateDefaultGizmoMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3); // Transparent mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.SetFloat("_Metallic", 0.5f);
        mat.SetFloat("_Glossiness", 0.8f);
        return mat;
    }
    
    private void ApplyUpdateToChunk(Vector3Int worldPosition, BlockUpdate update)
    {
        if (worldManager == null) return;
        
        // Find the chunk containing this block
        ChunkManager chunk = worldManager.GetChunkContaining(worldPosition);
        if (chunk == null) return;
        
        ChunkInfo chunkInfo = chunk.GetChunkInfo();
        if (chunkInfo == null) return;
        
        // Update block information
        if (update.updateType == BlockUpdate.UpdateType.Removed || 
            (update.updateType == BlockUpdate.UpdateType.Modified && string.IsNullOrEmpty(update.newBlockType)))
        {
            chunkInfo.RemoveBlockAt(worldPosition);
        }
        else
        {
            // Add or modify block
            chunkInfo.AddBlock(worldPosition, update.newBlockType);
        }
        
        // Note: We don't immediately rebuild the chunk mesh here
        // The server should handle the actual mesh updates through normal chunk updates
        // This just updates our local block information for queries
    }
    
    private IEnumerator CleanupExpiredGizmos()
    {
        while (true)
        {
            List<Vector3> toRemove = new List<Vector3>();
            
            foreach (var kvp in activeGizmos)
            {
                if (Time.time - kvp.Value.creationTime > gizmoDisplayTime)
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (var pos in toRemove)
            {
                RemoveGizmo(pos);
            }
            
            yield return new WaitForSeconds(0.5f); // Check every half second
        }
    }
    
    private void RemoveGizmo(Vector3 position)
    {
        if (activeGizmos.TryGetValue(position, out UpdateGizmo gizmo))
        {
            if (gizmo.gizmoObject != null)
            {
                DestroyImmediate(gizmo.gizmoObject);
            }
            activeGizmos.Remove(position);
        }
    }
    
    // Public API
    public void ClearAllGizmos()
    {
        List<Vector3> positions = new List<Vector3>(activeGizmos.Keys);
        foreach (var pos in positions)
        {
            RemoveGizmo(pos);
        }
        Debug.Log("Cleared all update gizmos");
    }
    
    public void SetUpdateCheckInterval(float interval)
    {
        updateCheckInterval = Mathf.Max(0.1f, interval);
    }
    
    public List<UpdateGizmo> GetActiveGizmos()
    {
        return new List<UpdateGizmo>(activeGizmos.Values);
    }
    
    public int GetActiveGizmoCount()
    {
        return activeGizmos.Count;
    }
    
    public int GetPendingUpdateCount()
    {
        return pendingUpdates.Count;
    }
    
    // Force update check (for manual triggers)
    public void ForceUpdateCheck()
    {
        StartCoroutine(CheckForUpdates());
    }
    
    // Reset timestamp to get all updates
    public void ResetUpdateTimestamp()
    {
        lastUpdateTimestamp = 0;
        Debug.Log("Reset update timestamp - will fetch all updates on next check");
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
        ClearAllGizmos();
    }
    
    // Debug methods
    private void OnDrawGizmos()
    {
        // Draw debug info for active gizmos
        foreach (var kvp in activeGizmos)
        {
            Vector3 pos = kvp.Key + Vector3.one * 0.5f;
            
            Gizmos.color = kvp.Value.updateType switch
            {
                BlockUpdate.UpdateType.Added => blockAddedColor,
                BlockUpdate.UpdateType.Removed => blockRemovedColor,
                BlockUpdate.UpdateType.Modified => blockModifiedColor,
                _ => Color.white
            };
            
            Gizmos.DrawWireCube(pos, Vector3.one * gizmoScale);
        }
    }
}

/// <summary>
/// Component for animated gizmo effects
/// </summary>
public class GizmoPulseEffect : MonoBehaviour
{
    private float baseScale;
    private Color baseColor;
    private ServerUpdateManager.BlockUpdate.UpdateType updateType;
    private Renderer gizmoRenderer;
    private Material gizmoMaterial;
    
    public void Initialize(float scale, Color color, ServerUpdateManager.BlockUpdate.UpdateType type)
    {
        baseScale = scale;
        baseColor = color;
        updateType = type;
        
        gizmoRenderer = GetComponent<Renderer>();
        if (gizmoRenderer != null)
        {
            gizmoMaterial = gizmoRenderer.material;
        }
    }
    
    private void Update()
    {
        if (gizmoRenderer == null || gizmoMaterial == null) return;
        
        // Pulsing scale effect
        float pulseSpeed = updateType switch
        {
            ServerUpdateManager.BlockUpdate.UpdateType.Added => 2f,
            ServerUpdateManager.BlockUpdate.UpdateType.Removed => 3f,
            ServerUpdateManager.BlockUpdate.UpdateType.Modified => 1.5f,
            _ => 2f
        };
        
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.1f;
        transform.localScale = Vector3.one * baseScale * pulse;
        
        // Fading effect over time
        float age = Time.time - GetComponent<ServerUpdateManager>()?.gizmoDisplayTime ?? 0f;
        float maxAge = FindFirstObjectByType<ServerUpdateManager>()?.gizmoDisplayTime ?? 3f;
        float alpha = Mathf.Clamp01(1f - (age / maxAge));
        
        Color currentColor = baseColor;
        currentColor.a = alpha * 0.7f; // Semi-transparent
        gizmoMaterial.color = currentColor;
    }
}