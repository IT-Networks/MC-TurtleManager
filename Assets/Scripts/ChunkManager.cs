using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// ChunkManager with minimal adjustments for NavMesh integration
/// </summary>
public class ChunkManager
{
    public readonly Vector2Int coord;
    public readonly int chunkSize;
    public readonly TurtleWorldManager manager;

    private Coroutine loadedCoroutine;
    public GameObject _go;
    private MeshFilter _mf;
    private MeshRenderer _mr;
    private MeshCollider _mc;
    private ChunkInfo chunk;

    // Thread-safe loaded flag (can be accessed from background threads)
    private volatile bool _isLoaded = false;

    // Specialized components
    private readonly ChunkCache cache;
    private readonly ChunkJsonParser jsonParser;
    private readonly ChunkMeshBuilder meshBuilder;
    private readonly ChunkUVProvider uvProvider;

    // Block management state
    private ChunkMeshData currentMeshData;
    private bool isRegenerating = false;

    public ChunkManager(Vector2Int coord, int chunkSize, TurtleWorldManager manager)
    {
        this.coord = coord;
        this.chunkSize = chunkSize;
        this.manager = manager;

        // Initialize specialized components
        this.cache = new ChunkCache(coord, chunkSize);
        this.jsonParser = new ChunkJsonParser(coord, chunkSize);
        this.uvProvider = new ChunkUVProvider(1);
        this.meshBuilder = new ChunkMeshBuilder(uvProvider);

        CreateGameObject();
        RegisterWithWorldManager();
    }

    private void CreateGameObject()
    {
        // Try to get from pool if available
        ChunkPool pool = manager.GetComponent<ChunkPool>();
        bool hasCachedMesh = false;

        if (pool != null && manager.useChunkPooling)
        {
            _go = pool.GetChunk(coord, out hasCachedMesh);
            _go.transform.SetParent(manager.transform);

            // Get existing components
            _mf = _go.GetComponent<MeshFilter>();
            _mr = _go.GetComponent<MeshRenderer>();
            _mc = _go.GetComponent<MeshCollider>();

            // ChunkInfo needs to be re-added or reset
            chunk = _go.GetComponent<ChunkInfo>();
            if (chunk == null)
            {
                chunk = _go.AddComponent<ChunkInfo>();
            }

            // Ensure we have a valid mesh (create new if pooled chunk had cleared mesh)
            if (_mf.sharedMesh == null || _mf.sharedMesh.vertexCount == 0)
            {
                _mf.sharedMesh = new Mesh();
                _mf.sharedMesh.name = $"ChunkMesh_{coord.x}_{coord.y}";
            }

            // If we have cached mesh, load it immediately
            if (hasCachedMesh)
            {
                CachedChunkData cachedData = pool.GetCachedMeshData(coord);
                if (cachedData != null)
                {
                    cachedData.ApplyToMesh(_mf.sharedMesh);
                    _mc.sharedMesh = _mf.sharedMesh;
                    Debug.Log($"Chunk {coord}: Loaded from mesh cache (instant)");
                }
            }
        }
        else
        {
            // No pooling - create new GameObject
            _go = new GameObject($"Chunk_{coord.x}_{coord.y}")
            {
                transform =
                {
                    position = Vector3.zero,
                    parent = manager.transform
                }
            };

            _mf = _go.AddComponent<MeshFilter>();
            _mr = _go.AddComponent<MeshRenderer>();
            _mc = _go.AddComponent<MeshCollider>();
            chunk = _go.AddComponent<ChunkInfo>();
        }
    }

    /// <summary>
    /// Returns chunk to pool instead of destroying it
    /// </summary>
    public void ReturnToPool(ChunkPool pool)
    {
        manager?.UnregisterChunkManager(coord);

        if (loadedCoroutine != null)
        {
            CoroutineHelper.Instance.StopCoroutine(loadedCoroutine);
            loadedCoroutine = null;
        }

        // Extract mesh geometry for caching
        MeshGeometryData meshGeometry = null;
        if (_mf != null && _mf.sharedMesh != null)
        {
            meshGeometry = ExtractMeshGeometry(_mf.sharedMesh);
        }

        // Return to pool with mesh data for caching
        if (pool != null && _go != null)
        {
            pool.ReturnChunk(_go, coord, meshGeometry);
        }

        // Clear references but don't destroy GameObject (it's in pool)
        _go = null;
        _mf = null;
        _mr = null;
        _mc = null;
        chunk = null;
        currentMeshData = null;
    }

    /// <summary>
    /// Extracts mesh geometry data from Unity Mesh for caching
    /// </summary>
    private MeshGeometryData ExtractMeshGeometry(Mesh mesh)
    {
        var geometryData = new MeshGeometryData
        {
            vertices = new List<Vector3>(mesh.vertices),
            triangles = new List<int>(mesh.triangles),
            uvs = new List<Vector2>(mesh.uv),
            normals = mesh.normals != null ? new List<Vector3>(mesh.normals) : new List<Vector3>(),
            submeshCount = mesh.subMeshCount,
            submeshes = new List<int[]>()
        };

        // Extract submeshes if more than one
        if (mesh.subMeshCount > 1)
        {
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                geometryData.submeshes.Add(mesh.GetTriangles(i));
            }
        }

        return geometryData;
    }

    /// <summary>
    /// Completely destroys chunk (no pooling)
    /// </summary>
    public void DestroyChunk()
    {
        manager?.UnregisterChunkManager(coord);

        if (loadedCoroutine != null)
        {
            CoroutineHelper.Instance.StopCoroutine(loadedCoroutine);
            loadedCoroutine = null;
        }

        if (_mf?.sharedMesh != null)
            UnityEngine.Object.Destroy(_mf.sharedMesh);

        if (_go != null)
            UnityEngine.Object.Destroy(_go);

        _go = null;
        _mf = null;
        _mr = null;
        _mc = null;
        chunk = null;
        currentMeshData = null;
        _isLoaded = false;
    }

    public IEnumerator LoadAndSpawnChunk(int batchVerticesPerFrame = 10000)
    {
        // 1. Try to load from cache
        if (cache.TryLoadCache(out ChunkMeshData cachedData, out long cachedTS))
        {
            Debug.Log($"Chunk {coord}: Using cached data (TS: {cachedTS})");

            string url = $"{manager.chunkUpdateUrl}chunkX={coord.x}&chunkZ={coord.y}";

            using UnityWebRequest req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Chunk {coord}: HTTP error: {req.error}");
                yield break;
            }

            string json = req.downloadHandler.text;
            JObject root = JObject.Parse(json);

            // Parse timestamp
            if (root["lastUpdate"] != null && long.TryParse(root["lastUpdate"]?.ToString(), out long lastUpdate))
            {
                Debug.Log($"Chunk {coord}: Server last update TS: {lastUpdate}, Cached TS: {cachedTS}");
                if (lastUpdate > cachedTS)
                {
                    Debug.Log($"Chunk {coord}: Cached data is outdated, downloading new data");
                }
                else
                {
                    yield return CoroutineHelper.Instance.StartCoroutine(
                        BuildCachedMeshAndApply(cachedData, batchVerticesPerFrame));
                    yield break;
                }
            }
            else
            {
                yield return CoroutineHelper.Instance.StartCoroutine(
                    BuildCachedMeshAndApply(cachedData, batchVerticesPerFrame));
                yield break;
            }
        }

        // 2. Download and parse JSON data
        yield return CoroutineHelper.Instance.StartCoroutine(DownloadAndProcessChunk(batchVerticesPerFrame));
    }

    /// <summary>
    /// Builds mesh from cached data on a background thread and applies it.
    /// </summary>
    private IEnumerator BuildCachedMeshAndApply(ChunkMeshData cachedData, int batchVerticesPerFrame)
    {
        currentMeshData = cachedData;

        // Snapshot adjacent chunks on main thread for thread-safe face culling
        var adjData = SnapshotAdjacentChunkData();
        PreparedChunkMesh cachedMesh = null;
        string errorMessage = null;
        Task buildTask = Task.Run(() =>
        {
            try
            {
                cachedMesh = meshBuilder.BuildMeshFromData(cachedData, adjData);
            }
            catch (System.Exception ex)
            {
                errorMessage = $"Exception during cached mesh building: {ex.Message}\n{ex.StackTrace}";
            }
        });

        yield return new WaitUntil(() => buildTask.IsCompleted);

        if (cachedMesh == null)
        {
            Debug.LogError($"Chunk {coord}: Failed to build mesh from cached data. {errorMessage}");
            yield break;
        }

        yield return CoroutineHelper.Instance.StartCoroutine(ApplyPreparedMesh(cachedMesh, batchVerticesPerFrame));
        NotifyChunkLoaded();
    }

    private IEnumerator DownloadAndProcessChunk(int batchVerticesPerFrame)
    {
        string url = $"{manager.blockDataUrl}&chunkX={coord.x}&chunkZ={coord.y}";

        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Chunk {coord}: HTTP error: {req.error}");
            yield break;
        }

        string json = req.downloadHandler.text;

        // 3. Parse JSON and build mesh on background thread
        // Snapshot adjacent chunks on main thread for thread-safe face culling
        var adjData3 = SnapshotAdjacentChunkData();
        ChunkMeshData meshData = null;
        CacheWriteData cacheWrite = null;
        PreparedChunkMesh prepared = null;
        string errorMessage = null;

        Task asyncTask = Task.Run(() =>
        {
            try
            {
                // Parse JSON
                meshData = jsonParser.ParseChunkJson(json, out cacheWrite);

                if (meshData != null)
                {
                    // Build mesh from parsed data (background thread)
                    prepared = meshBuilder.BuildMeshFromData(meshData, adjData3);
                }
                else
                {
                    errorMessage = "JSON parsing returned null";
                }
            }
            catch (System.Exception ex)
            {
                errorMessage = $"Exception during mesh building: {ex.Message}\n{ex.StackTrace}";
            }
        });

        yield return new WaitUntil(() => asyncTask.IsCompleted);

        if (meshData == null)
        {
            Debug.LogWarning($"Chunk {coord}: Failed to parse JSON data. {errorMessage}");
            yield break;
        }

        if (prepared == null)
        {
            Debug.LogError($"Chunk {coord}: Failed to build mesh from data. {errorMessage}");
            yield break;
        }

        currentMeshData = meshData;

        if (prepared.SubmeshCount == 0)
        {
            Debug.Log($"Chunk {coord}: No blocks to render");
            yield break;
        }

        // 4. Save to cache (async)
        if (cacheWrite != null)
        {
            Task cacheTask = Task.Run(() =>
            {
                cache.SaveCache(cacheWrite);
            });
            yield return new WaitUntil(() => cacheTask.IsCompleted);
        }

        // 5. Apply mesh to GameObject (on main thread)
        yield return CoroutineHelper.Instance.StartCoroutine(ApplyPreparedMesh(prepared, batchVerticesPerFrame));

        // 6. Notify that chunk is loaded
        NotifyChunkLoaded();
    }

    private IEnumerator ApplyPreparedMesh(PreparedChunkMesh prepared, int batchVerticesPerFrame)
    {
        if (prepared == null)
        {
            Debug.LogError($"Chunk {coord}: PreparedChunkMesh is null");
            yield break;
        }

        var mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            subMeshCount = prepared.SubmeshCount
        };

        // Apply mesh data in batches
        yield return CoroutineHelper.Instance.StartCoroutine(meshBuilder.ApplyMeshBatched(mesh, prepared, batchVerticesPerFrame));

        // Update ChunkInfo with block positions (on main thread)
        if (chunk != null && prepared.blockPositions != null)
        {
            foreach (var (position, blockType) in prepared.blockPositions)
            {
                chunk.AddBlock(position, blockType);
            }
        }

        // Set up materials
        var materials = new Material[prepared.SubmeshCount];
        for (int i = 0; i < prepared.SubmeshCount; i++)
        {
            materials[i] = manager.GetMaterialForBlock(prepared.blockNames[i]);
        }

        // Destroy old mesh
        if (_mf.sharedMesh != null && _mf.sharedMesh != mesh)
        {
            UnityEngine.Object.Destroy(_mf.sharedMesh);
        }

        // Apply to Unity components
        _mf.sharedMesh = mesh;
        _mr.sharedMaterials = materials;
        _mc.sharedMesh = mesh;

        // Mark as loaded (thread-safe flag)
        _isLoaded = true;

        Debug.Log($"Chunk {coord}: Mesh applied with {prepared.SubmeshCount} submeshes and {prepared.totalVertexCount} vertices");
    }

    /// <summary>
    /// Notifies the system that the chunk has been fully loaded.
    /// </summary>
    private void NotifyChunkLoaded()
    {
        // Chunk loaded event (listened to by BlockWorldPathfinder)
        manager?.OnChunkLoaded?.Invoke(coord);
    }

    /// <summary>
    /// Removes a block from both ChunkMeshData and ChunkInfo without mesh regeneration.
    /// Used during batch mining where mesh regen happens once at the end.
    /// </summary>
    public bool RemoveBlockFromData(Vector3 worldPosition)
    {
        if (currentMeshData == null) return false;

        Vector3Int localPos = WorldToLocalPosition(worldPosition);
        if (!IsValidLocalPosition(localPos)) return false;

        currentMeshData.SetBlock(localPos.x, localPos.y, localPos.z, null);
        chunk?.RemoveBlockAt(worldPosition);
        return true;
    }

    /// <summary>
    /// Removes a block and regenerates the mesh.
    /// </summary>
    public bool RemoveBlockAndRegenerate(Vector3 worldPosition, int batchVerticesPerFrame = 10000)
    {
        if (currentMeshData == null || isRegenerating)
        {
            Debug.LogWarning($"Chunk {coord}: Cannot remove block - no mesh data available or already regenerating");
            return false;
        }

        Vector3Int localPos = WorldToLocalPosition(worldPosition);
        
        if (!IsValidLocalPosition(localPos))
        {
            Debug.LogWarning($"Chunk {coord}: Position {worldPosition} is outside chunk bounds");
            return false;
        }

        if (currentMeshData.GetBlock(localPos.x, localPos.y, localPos.z) == null)
        {
            Debug.LogWarning($"Chunk {coord}: No block found at position {worldPosition}");
            return false;
        }

        // Remove the block
        currentMeshData.SetBlock(localPos.x, localPos.y, localPos.z, null);
        chunk.RemoveBlockAt(worldPosition);

        // Set flag immediately to prevent TOCTOU race with concurrent callers
        isRegenerating = true;
        CoroutineHelper.Instance.StartCoroutine(RegenerateMeshCoroutine(batchVerticesPerFrame));
        
        Debug.Log($"Chunk {coord}: Block removed at {worldPosition}, regenerating mesh");
        return true;
    }

    /// <summary>
    /// Triggers mesh regeneration from current ChunkInfo data.
    /// Used after batch block removal (e.g. mining) to update visuals.
    /// </summary>
    public void RegenerateMesh(int batchVerticesPerFrame = 10000)
    {
        if (currentMeshData == null || isRegenerating) return;
        isRegenerating = true;
        CoroutineHelper.Instance.StartCoroutine(RegenerateMeshCoroutine(batchVerticesPerFrame));
    }

    /// <summary>
    /// Regenerates the mesh based on current mesh data.
    /// Callers must set isRegenerating = true before starting this coroutine.
    /// </summary>
    private IEnumerator RegenerateMeshCoroutine(int batchVerticesPerFrame)
    {
        try
        {
            chunk.ClearBlocks();

            // Snapshot adjacent chunks on main thread for thread-safe face culling
            var adjData4 = SnapshotAdjacentChunkData();
            PreparedChunkMesh prepared = null;
            Task buildTask = Task.Run(() =>
            {
                prepared = meshBuilder.BuildMeshFromData(currentMeshData, adjData4);
            });

            yield return new WaitUntil(() => buildTask.IsCompleted);

            if (prepared.SubmeshCount == 0)
            {
                var emptyMesh = new Mesh();

                if (_mf.sharedMesh != null)
                {
                    UnityEngine.Object.Destroy(_mf.sharedMesh);
                }

                _mf.sharedMesh = emptyMesh;
                _mr.sharedMaterials = new Material[0];
                _mc.sharedMesh = emptyMesh;

                Debug.Log($"Chunk {coord}: All blocks removed, mesh cleared");
            }
            else
            {
                yield return CoroutineHelper.Instance.StartCoroutine(ApplyPreparedMesh(prepared, batchVerticesPerFrame));
            }
        }
        finally
        {
            isRegenerating = false;

            // Notify that regeneration is complete (used by BlockWorldPathfinder to update NavMesh)
            manager?.OnChunkRegenerated?.Invoke(coord);
        }
    }

    private Vector3Int WorldToLocalPosition(Vector3 worldPosition)
    {
        int localX = Mathf.FloorToInt(-worldPosition.x) - (coord.x * chunkSize);
        // CRITICAL FIX: Add Y-offset of 128 as in ChunkMeshData.WorldToLocalPosition
        // Unity world coordinates: Y=0 corresponds to Minecraft Y=-128
        // ChunkMeshBuilder stores blocks with y-128 in ChunkInfo
        int localY = Mathf.FloorToInt(worldPosition.y + 128);
        int localZ = Mathf.FloorToInt(worldPosition.z) - (coord.y * chunkSize);

        return new Vector3Int(localX, localY, localZ);
    }

    private bool IsValidLocalPosition(Vector3Int localPos)
    {
        return localPos.x >= 0 && localPos.x < chunkSize &&
               localPos.y >= 0 && localPos.y < (currentMeshData?.maxHeight ?? 256) &&
               localPos.z >= 0 && localPos.z < chunkSize;
    }

    public bool AddBlockAndRegenerate(Vector3 worldPosition, string blockType, int batchVerticesPerFrame = 10000)
    {
        if (currentMeshData == null || isRegenerating)
        {
            Debug.LogWarning($"Chunk {coord}: Cannot add block - no mesh data available or already regenerating");
            return false;
        }

        Vector3Int localPos = WorldToLocalPosition(worldPosition);
        
        if (!IsValidLocalPosition(localPos))
        {
            Debug.LogWarning($"Chunk {coord}: Position {worldPosition} is outside chunk bounds");
            return false;
        }

        currentMeshData.SetBlock(localPos.x, localPos.y, localPos.z, blockType);

        // Set flag immediately to prevent TOCTOU race with concurrent callers
        isRegenerating = true;
        CoroutineHelper.Instance.StartCoroutine(RegenerateMeshCoroutine(batchVerticesPerFrame));
        
        Debug.Log($"Chunk {coord}: Block '{blockType}' added at {worldPosition}, regenerating mesh");
        return true;
    }

    private void RegisterWithWorldManager()
    {
        manager?.RegisterChunkManager(coord, this);
    }

    // Properties
    public bool IsLoaded => _isLoaded;
    public bool IsRegenerating => isRegenerating;
    public int VertexCount => _mf?.sharedMesh?.vertexCount ?? 0;
    public int SubmeshCount => _mf?.sharedMesh?.subMeshCount ?? 0;
    public ChunkMeshData GetCurrentMeshData() => currentMeshData;

    // Thread-safe accessor for ChunkInfo
    public ChunkInfo GetChunkInfo() => chunk;

    /// <summary>
    /// Snapshots adjacent chunk edge data on the main thread for thread-safe
    /// cross-chunk face culling inside Task.Run().
    /// </summary>
    private Dictionary<Vector2Int, ChunkMeshData> SnapshotAdjacentChunkData()
    {
        var result = new Dictionary<Vector2Int, ChunkMeshData>(4);
        Vector2Int[] offsets = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

        foreach (var offset in offsets)
        {
            var adjCoord = coord + offset;
            var adjChunk = manager.GetChunkAt(adjCoord);
            if (adjChunk != null && adjChunk.IsLoaded)
            {
                var adjMeshData = adjChunk.GetCurrentMeshData();
                if (adjMeshData != null)
                    result[adjCoord] = adjMeshData;
            }
        }

        return result;
    }
}

// Extension methods - removed GetChunkInfo as it's now an instance method

// CoroutineHelper remains unchanged
public class CoroutineHelper : MonoBehaviour
{
    private static CoroutineHelper _instance;

    public static CoroutineHelper Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CoroutineHelper");
                GameObject.DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineHelper>();
            }
            return _instance;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}