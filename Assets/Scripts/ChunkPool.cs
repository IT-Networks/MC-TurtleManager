using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// High-performance chunk pooling system with mesh caching
/// Eliminates GC allocations and mesh regeneration for previously loaded chunks
/// </summary>
public class ChunkPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("Maximum number of inactive chunks to keep in pool")]
    public int maxPoolSize = 100;

    [Tooltip("Preload this many chunk containers at startup")]
    public int preloadCount = 20;

    [Header("Performance")]
    [Tooltip("Enable mesh data caching for instant reload")]
    public bool enableMeshCaching = true;

    [Tooltip("Clear cached meshes after this many seconds of being unused")]
    public float meshCacheTimeout = 300f; // 5 minutes

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Pooled chunk containers (GameObjects with MeshFilter/MeshRenderer)
    private readonly Queue<GameObject> inactiveChunks = new Queue<GameObject>();
    private readonly HashSet<GameObject> activeChunks = new HashSet<GameObject>();

    // Mesh data cache for instant reloading
    private readonly Dictionary<Vector2Int, CachedChunkData> meshCache = new Dictionary<Vector2Int, CachedChunkData>();
    private readonly Dictionary<Vector2Int, float> meshCacheTimestamps = new Dictionary<Vector2Int, float>();

    // Statistics
    private int totalCreated = 0;
    private int totalReused = 0;
    private int totalCacheHits = 0;

    public int ActiveChunks => activeChunks.Count;
    public int PooledChunks => inactiveChunks.Count;
    public int CachedMeshes => meshCache.Count;

    void Start()
    {
        PreloadChunkContainers();
        InvokeRepeating(nameof(CleanupOldMeshCache), 60f, 60f);
    }

    /// <summary>
    /// Preloads chunk containers into the pool
    /// </summary>
    void PreloadChunkContainers()
    {
        for (int i = 0; i < preloadCount; i++)
        {
            GameObject chunk = CreateChunkContainer();
            chunk.SetActive(false);
            inactiveChunks.Enqueue(chunk);
        }

        if (showDebugInfo)
        {
            Debug.Log($"ChunkPool: Preloaded {preloadCount} chunk containers");
        }
    }

    /// <summary>
    /// Gets a chunk from pool or creates new one
    /// </summary>
    public GameObject GetChunk(Vector2Int coord, out bool hasCachedMesh)
    {
        GameObject chunk;

        // Try to reuse from pool
        if (inactiveChunks.Count > 0)
        {
            chunk = inactiveChunks.Dequeue();
            chunk.SetActive(true);
            totalReused++;
        }
        else
        {
            // Create new if pool is empty
            chunk = CreateChunkContainer();
            totalCreated++;
        }

        activeChunks.Add(chunk);
        chunk.name = $"Chunk_{coord.x}_{coord.y}";

        // Check if we have cached mesh data
        hasCachedMesh = enableMeshCaching && meshCache.ContainsKey(coord);

        if (hasCachedMesh)
        {
            totalCacheHits++;
            meshCacheTimestamps[coord] = Time.time;
        }

        if (showDebugInfo)
        {
            Debug.Log($"GetChunk({coord}): Cached={hasCachedMesh}, Reused={totalReused}/{totalCreated}");
        }

        return chunk;
    }

    /// <summary>
    /// Returns chunk to pool instead of destroying it
    /// </summary>
    public void ReturnChunk(GameObject chunk, Vector2Int coord, MeshGeometryData meshData = null)
    {
        if (chunk == null) return;

        activeChunks.Remove(chunk);

        // Cache mesh data if enabled
        if (enableMeshCaching && meshData != null)
        {
            CacheMeshData(coord, meshData);
        }

        // Return to pool if under max size
        if (inactiveChunks.Count < maxPoolSize)
        {
            // Clear mesh to free memory
            MeshFilter meshFilter = chunk.GetComponent<MeshFilter>();
            MeshCollider meshCollider = chunk.GetComponent<MeshCollider>();

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                meshFilter.sharedMesh.Clear();
            }

            // Clear MeshCollider to prevent "mesh has no vertices" warning
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null;
            }

            chunk.SetActive(false);
            chunk.transform.position = Vector3.zero;
            inactiveChunks.Enqueue(chunk);
        }
        else
        {
            // Pool is full, destroy
            Destroy(chunk);
        }

        if (showDebugInfo)
        {
            Debug.Log($"ReturnChunk({coord}): Pool size = {inactiveChunks.Count}");
        }
    }

    /// <summary>
    /// Gets cached mesh data if available
    /// </summary>
    public CachedChunkData GetCachedMeshData(Vector2Int coord)
    {
        if (meshCache.TryGetValue(coord, out CachedChunkData data))
        {
            meshCacheTimestamps[coord] = Time.time;
            return data;
        }
        return null;
    }

    /// <summary>
    /// Caches mesh data for fast reload
    /// </summary>
    void CacheMeshData(Vector2Int coord, MeshGeometryData meshData)
    {
        if (!meshCache.ContainsKey(coord))
        {
            meshCache[coord] = new CachedChunkData
            {
                vertices = meshData.vertices.ToArray(),
                triangles = meshData.triangles.ToArray(),
                uvs = meshData.uvs.ToArray(),
                normals = meshData.normals?.ToArray(),
                submeshCount = meshData.submeshCount,
                submeshes = new List<int[]>(meshData.submeshes)
            };
            meshCacheTimestamps[coord] = Time.time;
        }
    }

    /// <summary>
    /// Clears mesh cache for chunks that haven't been used recently
    /// </summary>
    void CleanupOldMeshCache()
    {
        if (!enableMeshCaching) return;

        float currentTime = Time.time;
        List<Vector2Int> toRemove = new List<Vector2Int>();

        foreach (var kvp in meshCacheTimestamps)
        {
            if (currentTime - kvp.Value > meshCacheTimeout)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var coord in toRemove)
        {
            meshCache.Remove(coord);
            meshCacheTimestamps.Remove(coord);
        }

        if (showDebugInfo && toRemove.Count > 0)
        {
            Debug.Log($"Cleaned up {toRemove.Count} old mesh caches");
        }
    }

    /// <summary>
    /// Creates a new chunk container GameObject
    /// </summary>
    GameObject CreateChunkContainer()
    {
        GameObject chunk = new GameObject("ChunkContainer");
        chunk.transform.SetParent(transform);

        // Add required components
        MeshFilter meshFilter = chunk.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = chunk.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = chunk.AddComponent<MeshCollider>();

        // Create new mesh
        meshFilter.sharedMesh = new Mesh();
        meshFilter.sharedMesh.name = "ChunkMesh";

        return chunk;
    }

    /// <summary>
    /// Clears all pools and caches
    /// </summary>
    public void ClearAll()
    {
        // Destroy all pooled chunks
        while (inactiveChunks.Count > 0)
        {
            GameObject chunk = inactiveChunks.Dequeue();
            if (chunk != null) Destroy(chunk);
        }

        // Clear active chunks tracking
        activeChunks.Clear();

        // Clear mesh cache
        meshCache.Clear();
        meshCacheTimestamps.Clear();

        // Reset stats
        totalCreated = 0;
        totalReused = 0;
        totalCacheHits = 0;

        if (showDebugInfo)
        {
            Debug.Log("ChunkPool: Cleared all pools and caches");
        }
    }

    /// <summary>
    /// Gets pool statistics
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            activeChunks = activeChunks.Count,
            pooledChunks = inactiveChunks.Count,
            cachedMeshes = meshCache.Count,
            totalCreated = totalCreated,
            totalReused = totalReused,
            totalCacheHits = totalCacheHits,
            reuseRate = totalCreated > 0 ? (float)totalReused / (totalCreated + totalReused) : 0f,
            cacheHitRate = totalCacheHits > 0 ? (float)totalCacheHits / (totalCreated + totalReused) : 0f
        };
    }

    void OnDestroy()
    {
        ClearAll();
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        var stats = GetStatistics();
        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Box("Chunk Pool Statistics");
        GUILayout.Label($"Active Chunks: {stats.activeChunks}");
        GUILayout.Label($"Pooled Chunks: {stats.pooledChunks}");
        GUILayout.Label($"Cached Meshes: {stats.cachedMeshes}");
        GUILayout.Label($"Total Created: {stats.totalCreated}");
        GUILayout.Label($"Total Reused: {stats.totalReused}");
        GUILayout.Label($"Reuse Rate: {stats.reuseRate:P0}");
        GUILayout.Label($"Cache Hit Rate: {stats.cacheHitRate:P0}");
        GUILayout.EndArea();
    }
}

/// <summary>
/// Cached chunk mesh data for instant reload
/// </summary>
[System.Serializable]
public class CachedChunkData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;
    public Vector3[] normals;
    public int submeshCount;
    public List<int[]> submeshes;

    public void ApplyToMesh(Mesh mesh)
    {
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;

        if (submeshCount > 1)
        {
            mesh.subMeshCount = submeshCount;
            for (int i = 0; i < submeshes.Count; i++)
            {
                mesh.SetTriangles(submeshes[i], i);
            }
        }
        else
        {
            mesh.triangles = triangles;
        }

        if (normals != null && normals.Length > 0)
        {
            mesh.normals = normals;
        }
        else
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
    }
}

/// <summary>
/// Helper class for mesh geometry data (vertices, triangles, UVs)
/// Separate from ChunkMeshData which stores block grid
/// </summary>
public class MeshGeometryData
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Vector2> uvs = new List<Vector2>();
    public List<Vector3> normals = new List<Vector3>();
    public int submeshCount = 1;
    public List<int[]> submeshes = new List<int[]>();
}

/// <summary>
/// Pool statistics for monitoring
/// </summary>
[System.Serializable]
public struct PoolStatistics
{
    public int activeChunks;
    public int pooledChunks;
    public int cachedMeshes;
    public int totalCreated;
    public int totalReused;
    public int totalCacheHits;
    public float reuseRate;
    public float cacheHitRate;
}
