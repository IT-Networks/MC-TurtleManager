using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// ChunkManager mit minimalen Anpassungen für NavMesh-Integration
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

    // Specialized components
    private readonly ChunkCache cache;
    private readonly ChunkJsonParser jsonParser;
    private readonly ChunkMeshBuilder meshBuilder;
    private readonly ChunkUVProvider uvProvider;
    
    // Block-Management Eigenschaften
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
                    currentMeshData = cachedData;
                    var cachedMesh = meshBuilder.BuildMeshFromData(cachedData, chunk);
                    yield return CoroutineHelper.Instance.StartCoroutine(ApplyPreparedMesh(cachedMesh, batchVerticesPerFrame));
                    
                    // Notifiziere über geladenen Chunk
                    NotifyChunkLoaded();
                    yield break;
                }
            }
            else
            {
                currentMeshData = cachedData;
                var cachedMesh = meshBuilder.BuildMeshFromData(cachedData, chunk);
                yield return CoroutineHelper.Instance.StartCoroutine(ApplyPreparedMesh(cachedMesh, batchVerticesPerFrame));
                
                // Notifiziere über geladenen Chunk
                NotifyChunkLoaded();
                yield break;
            }                 
        }

        // 2. Download and parse JSON data
        yield return CoroutineHelper.Instance.StartCoroutine(DownloadAndProcessChunk(batchVerticesPerFrame));
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

        // 3. Parse JSON on background thread
        ChunkMeshData meshData = null;
        CacheWriteData cacheWrite = null;
        
        Task parseTask = Task.Run(() =>
        {
            meshData = jsonParser.ParseChunkJson(json, out cacheWrite);
        });
        
        yield return new WaitUntil(() => parseTask.IsCompleted);

        if (meshData == null)
        {
            Debug.LogWarning($"Chunk {coord}: Failed to parse JSON data");
            yield break;
        }

        currentMeshData = meshData;

        // 4. Build mesh from parsed data
        PreparedChunkMesh prepared = meshBuilder.BuildMeshFromData(meshData, chunk);
        
        if (prepared.SubmeshCount == 0)
        {
            Debug.Log($"Chunk {coord}: No blocks to render");
            yield break;
        }

        // 5. Save to cache
        if (cacheWrite != null)
        {
            cache.SaveCache(cacheWrite);
        }

        // 6. Apply mesh to GameObject
        yield return CoroutineHelper.Instance.StartCoroutine(ApplyPreparedMesh(prepared, batchVerticesPerFrame));
        
        // 7. Notifiziere über geladenen Chunk
        NotifyChunkLoaded();
    }

    private IEnumerator ApplyPreparedMesh(PreparedChunkMesh prepared, int batchVerticesPerFrame)
    {
        var mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            subMeshCount = prepared.SubmeshCount
        };

        // Apply mesh data in batches
        yield return CoroutineHelper.Instance.StartCoroutine(meshBuilder.ApplyMeshBatched(mesh, prepared, batchVerticesPerFrame));

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

        Debug.Log($"Chunk {coord}: Mesh applied with {prepared.SubmeshCount} submeshes and {prepared.totalVertexCount} vertices");
    }

    /// <summary>
    /// Benachrichtigt das System, dass der Chunk vollständig geladen wurde
    /// </summary>
    private void NotifyChunkLoaded()
    {
        // Event für Chunk geladen (wird vom BlockWorldPathfinder abgehört)
        manager?.OnChunkLoaded?.Invoke(coord);
    }

    /// <summary>
    /// Löscht einen Block und regeneriert den Mesh
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

        // Entferne den Block
        currentMeshData.SetBlock(localPos.x, localPos.y, localPos.z, null);
        chunk.RemoveBlockAt(worldPosition);

        // Starte die Mesh-Regenerierung
        CoroutineHelper.Instance.StartCoroutine(RegenerateMeshCoroutine(batchVerticesPerFrame));
        
        Debug.Log($"Chunk {coord}: Block removed at {worldPosition}, regenerating mesh");
        return true;
    }

    /// <summary>
    /// Regeneriert den Mesh basierend auf den aktuellen Mesh-Daten
    /// </summary>
    private IEnumerator RegenerateMeshCoroutine(int batchVerticesPerFrame)
    {
        if (isRegenerating)
        {
            yield break;
        }

        isRegenerating = true;

        try
        {
            chunk.ClearBlocks();
            
            PreparedChunkMesh prepared = meshBuilder.BuildMeshFromData(currentMeshData, chunk);
            
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
            
            // Benachrichtige über Regenerierung abgeschlossen
            // Dies wird vom BlockWorldPathfinder genutzt um das NavMesh zu aktualisieren
            manager?.OnChunkRegenerated?.Invoke(coord);
        }
    }

    private Vector3Int WorldToLocalPosition(Vector3 worldPosition)
    {
        int localX = Mathf.FloorToInt(-worldPosition.x) - (coord.x * chunkSize);
        int localY = Mathf.FloorToInt(worldPosition.y);
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
        
        CoroutineHelper.Instance.StartCoroutine(RegenerateMeshCoroutine(batchVerticesPerFrame));
        
        Debug.Log($"Chunk {coord}: Block '{blockType}' added at {worldPosition}, regenerating mesh");
        return true;
    }

    private void RegisterWithWorldManager()
    {
        manager?.RegisterChunkManager(coord, this);
    }

    // Properties
    public bool IsLoaded => _go != null && _mf != null && _mf.sharedMesh != null;
    public bool IsRegenerating => isRegenerating;
    public int VertexCount => _mf?.sharedMesh?.vertexCount ?? 0;
    public int SubmeshCount => _mf?.sharedMesh?.subMeshCount ?? 0;
}

// Extension methods bleiben gleich
public static class ChunkManagerExtensions
{
    public static ChunkInfo GetChunkInfo(this ChunkManager chunkManager)
    {
        return chunkManager._go?.GetComponent<ChunkInfo>();
    }
}

// CoroutineHelper bleibt unverändert
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