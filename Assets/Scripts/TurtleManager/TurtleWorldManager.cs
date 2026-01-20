using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class TurtleWorldManager : MonoBehaviour
{
[Header("Materials / Prefabs")]
    public Material standardBlockMaterial;
    public Material waterMaterial;
    public Material leavesMaterial;
    public GameObject turtlePrefab;

    [Header("Resources Settings")]
    [Tooltip("Ordnername innerhalb von 'Resources', aus dem Blocktexturen geladen werden.")]
    public string textureResourceFolder = "Textures/Minecraft";

    [Header("Server")]
    [Tooltip("Basis-URL für einzelne Chunk-Abfragen. Es werden ?blockX=&blockZ= angehängt (Blockkoordinaten!).")]
    public string blockDataUrl = "http://localhost:4567/chunkdata?radius=0";
    public string chunkUpdateUrl = "http://localhost:4567/chunkupdate?";
    public string statusUrl = "http://192.168.178.211:4999/status/all";

    [Header("Streaming Settings")]
    public int chunkSize = 16;
    public int chunkLoadRadius = 2;   // in Chunk-Einheiten
    public float chunkRefreshInterval = 0.5f;

    [Header("Camera-Based Loading")]
    [Tooltip("Use camera frustum for chunk loading instead of fixed radius")]
    public bool useFrustumBasedLoading = true;
    [Tooltip("Additional chunk rings to load around visible chunks")]
    public int frustumBufferRings = 1;
    [Tooltip("Maximum chunk distance to check for frustum culling")]
    public int maxFrustumCheckDistance = 15;

    [Header("Block Interaction Settings")]
    [SerializeField] private bool enableBlockInteractions = true;
    [SerializeField] private int maxConcurrentRegenerations = 3;
    [Tooltip("Sollen Block-Änderungen automatisch an den Server gesendet werden?")]
    [SerializeField] private bool autoSyncToServer = false;

    // Laufzeit
    private readonly Dictionary<Vector2Int, ChunkManager> _loadedChunks = new();
    private Vector2Int _currentCameraChunk = new(int.MinValue, int.MinValue);
    private Camera _cam;

    // Optional: Material-Cache pro Blocktyp (für Submeshes)
    private readonly Dictionary<string, Material> _materialCache = new();

    // Block-Management
    private readonly HashSet<Vector2Int> regeneratingChunks = new HashSet<Vector2Int>();
    private readonly Queue<BlockModification> pendingServerUpdates = new Queue<BlockModification>();

    // Events für Block-Interaktionen
    public System.Action<Vector2Int> OnChunkLoaded;
    public System.Action<Vector3, string> OnBlockRemoved;
    public System.Action<Vector3, string> OnBlockPlaced;
    public System.Action<Vector2Int> OnChunkRegenerated;

    // --- Turtle Status (optional, aus deinem Vorgänger übernommen) ---
    private GameObject turtleInstance;

    [Serializable] public class TurtlePosition { public float x, y, z; }
    [Serializable] public class TurtleStatus { public string label; public string direction; public TurtlePosition position; }
    [Serializable] public class StatusWrapper { public List<TurtleStatus> entries; }
    [Serializable]
    public class BlockTextureData
    {
        public Texture2D top, bottom, front, back, left, right;

        public bool HasSeparateSides =>
            top != null || bottom != null || front != null ||
            back != null || left != null || right != null;
    }

    // Neue Klassen für Block-Management
    [Serializable]
    public class BlockModification
    {
        public Vector3 worldPosition;
        public string oldBlockType;
        public string newBlockType;
        public System.DateTime timestamp;
        public bool sentToServer;

        public BlockModification(Vector3 pos, string oldType, string newType)
        {
            worldPosition = pos;
            oldBlockType = oldType;
            newBlockType = newType;
            timestamp = System.DateTime.Now;
            sentToServer = false;
        }
    }

    void Start()
    {
        _cam = Camera.main;
        if (_cam == null) Debug.LogError("Keine Hauptkamera gefunden. Wechsele auf die Kamera mit RTSCameraController.");

        StartCoroutine(ChunkStreamingLoop());
        StartCoroutine(SpawnOrUpdateTurtle());
        
        if (autoSyncToServer)
        {
            StartCoroutine(ServerSyncLoop());
        }
    }

    void OnDisable()
    {
        StopAllCoroutines();
        foreach (var kvp in _loadedChunks) kvp.Value.DestroyChunk();
        _loadedChunks.Clear();
        regeneratingChunks.Clear();
        pendingServerUpdates.Clear();
        if (turtleInstance != null) Destroy(turtleInstance);
    }

    IEnumerator ChunkStreamingLoop()
    {
        while (true)
        {
            if (_cam != null)
            {
                Vector3 camPos = _cam.transform.position;
                Vector2Int camChunk = new(
                    Mathf.FloorToInt(-camPos.x / (float)chunkSize),
                    Mathf.FloorToInt(camPos.z / (float)chunkSize)
                );

                if (camChunk != _currentCameraChunk)
                {
                    _currentCameraChunk = camChunk;
                    yield return StartCoroutine(UpdateLoadedChunks(camChunk));
                }
            }
            yield return new WaitForSeconds(chunkRefreshInterval);
        }
    }

    IEnumerator UpdateLoadedChunks(Vector2Int camChunk)
    {
        HashSet<Vector2Int> needed = new();

        if (useFrustumBasedLoading && _cam != null)
        {
            // Use camera frustum-based loading
            needed = GetFrustumVisibleChunks();
            // Add buffer rings
            if (frustumBufferRings > 0)
            {
                needed.UnionWith(GetBufferRingChunks(needed, frustumBufferRings));
            }
        }
        else
        {
            // Use traditional radius-based loading
            for (int x = -chunkLoadRadius; x <= chunkLoadRadius; x++)
            {
                for (int z = -chunkLoadRadius; z <= chunkLoadRadius; z++)
                {
                    needed.Add(new Vector2Int(camChunk.x + x, camChunk.y + z));
                }
            }
        }

        foreach (var coord in needed)
        {
            // Wenn Kamera in der Zwischenzeit einen neuen Chunk erreicht → abbrechen
            Vector3 camPos = _cam.transform.position;
            Vector2Int currentCamChunk = new(
                Mathf.FloorToInt(-camPos.x / (float)chunkSize),
                Mathf.FloorToInt(camPos.z / (float)chunkSize)
            );
            if (currentCamChunk != _currentCameraChunk)
            {
                yield break;
            }

            if (_loadedChunks.ContainsKey(coord)) continue;

            // Create new ChunkManager (no longer MonoBehaviour)
            var chunk = new ChunkManager(coord, chunkSize, this);
            _loadedChunks.Add(coord, chunk);

            // Load chunk data
            yield return StartCoroutine(chunk.LoadAndSpawnChunk());            
        }

        // Entladen von Chunks, die nicht mehr benötigt werden
        List<Vector2Int> toUnload = new();
        foreach (var kvp in _loadedChunks)
        {
            if (!needed.Contains(kvp.Key))
                toUnload.Add(kvp.Key);
        }
        foreach (var c in toUnload)
        {
            // Entferne aus regenerating chunks wenn vorhanden
            regeneratingChunks.Remove(c);
            
            _loadedChunks[c].DestroyChunk();
            _loadedChunks.Remove(c);
        }
    }

    // *** NEUE BLOCK-MANAGEMENT METHODEN ***

    /// <summary>
    /// Registriert einen ChunkManager im System.
    /// </summary>
    public void RegisterChunkManager(Vector2Int coord, ChunkManager chunkManager)
    {
        // Da wir bereits _loadedChunks verwenden, ist keine separate Registrierung nötig
        // Diese Methode ist für Kompatibilität mit den anderen Artefakten
        if (!_loadedChunks.ContainsKey(coord))
        {
            Debug.Log($"Registered ChunkManager for chunk {coord}");
        }
    }

    /// <summary>
    /// Entfernt einen ChunkManager aus dem System.
    /// </summary>
    public void UnregisterChunkManager(Vector2Int coord)
    {
        regeneratingChunks.Remove(coord);
        Debug.Log($"Unregistered ChunkManager for chunk {coord}");
    }

    /// <summary>
    /// Gibt den ChunkManager für die angegebenen Koordinaten zurück.
    /// </summary>
    public ChunkManager GetChunkManager(Vector2Int coord)
    {
        _loadedChunks.TryGetValue(coord, out ChunkManager chunkManager);
        return chunkManager;
    }

    /// <summary>
    /// Ermittelt den ChunkManager basierend auf einer Weltposition.
    /// </summary>
    public ChunkManager GetChunkManagerAtWorldPosition(Vector3 worldPosition)
    {
        Vector2Int chunkCoord = WorldPositionToChunkCoord(worldPosition);
        return GetChunkManager(chunkCoord);
    }

    /// <summary>
    /// Konvertiert eine Weltposition zu Chunk-Koordinaten.
    /// </summary>
    public Vector2Int WorldPositionToChunkCoord(Vector3 worldPosition)
    {
        int chunkX = Mathf.FloorToInt(-worldPosition.x / chunkSize);
        int chunkZ = Mathf.FloorToInt(worldPosition.z / chunkSize);
        
        return new Vector2Int(chunkX, chunkZ);
    }

    /// <summary>
    /// Entfernt einen Block an der angegebenen Weltposition.
    /// </summary>
    public bool RemoveBlockAtWorldPosition(Vector3 worldPosition)
    {
        if (!enableBlockInteractions) 
        {
            Debug.LogWarning("Block interactions are disabled");
            return false;
        }

        ChunkManager chunkManager = GetChunkManagerAtWorldPosition(worldPosition);
        if (chunkManager == null)
        {
            Debug.LogWarning($"No chunk found for world position {worldPosition}");
            return false;
        }

        Vector2Int chunkCoord = WorldPositionToChunkCoord(worldPosition);
        
        // Prüfe, ob bereits eine Regenerierung läuft
        if (regeneratingChunks.Contains(chunkCoord))
        {
            Debug.LogWarning($"Chunk {chunkCoord} is already regenerating");
            return false;
        }

        // Prüfe die maximale Anzahl gleichzeitiger Regenerierungen
        if (regeneratingChunks.Count >= maxConcurrentRegenerations)
        {
            Debug.LogWarning($"Maximum concurrent regenerations ({maxConcurrentRegenerations}) reached");
            return false;
        }

        // Hole den aktuellen Blocktyp vor dem Entfernen
        string removedBlockType = chunkManager.GetChunkInfo()?.GetBlockTypeAt(worldPosition);

        // Markiere als regenerierend
        regeneratingChunks.Add(chunkCoord);

        // Entferne den Block
        bool success = chunkManager.RemoveBlockAndRegenerate(worldPosition);

        if (success)
        {
            // Event feuern
            OnBlockRemoved?.Invoke(worldPosition, removedBlockType);
            
            // Für Server-Synchronisation vormerken
            if (autoSyncToServer)
            {
                pendingServerUpdates.Enqueue(new BlockModification(worldPosition, removedBlockType, null));
            }
            
            // Starte Coroutine zum Tracking der Regenerierung
            StartCoroutine(TrackChunkRegeneration(chunkCoord, chunkManager));
        }
        else
        {
            regeneratingChunks.Remove(chunkCoord);
        }

        return success;
    }

    /// <summary>
    /// Platziert einen Block an der angegebenen Weltposition.
    /// </summary>
    public bool PlaceBlockAtWorldPosition(Vector3 worldPosition, string blockType)
    {
        if (!enableBlockInteractions)
        {
            Debug.LogWarning("Block interactions are disabled");
            return false;
        }

        ChunkManager chunkManager = GetChunkManagerAtWorldPosition(worldPosition);
        if (chunkManager == null)
        {
            Debug.LogWarning($"No chunk found for world position {worldPosition}");
            return false;
        }

        Vector2Int chunkCoord = WorldPositionToChunkCoord(worldPosition);
        
        // Prüfe, ob bereits eine Regenerierung läuft
        if (regeneratingChunks.Contains(chunkCoord))
        {
            Debug.LogWarning($"Chunk {chunkCoord} is already regenerating");
            return false;
        }

        // Prüfe die maximale Anzahl gleichzeitiger Regenerierungen
        if (regeneratingChunks.Count >= maxConcurrentRegenerations)
        {
            Debug.LogWarning($"Maximum concurrent regenerations ({maxConcurrentRegenerations}) reached");
            return false;
        }

        // Hole den aktuellen Blocktyp vor dem Setzen
        string oldBlockType = chunkManager.GetChunkInfo()?.GetBlockTypeAt(worldPosition);

        // Markiere als regenerierend
        regeneratingChunks.Add(chunkCoord);

        // Platziere den Block
        bool success = chunkManager.AddBlockAndRegenerate(worldPosition, blockType);

        if (success)
        {
            // Event feuern
            OnBlockPlaced?.Invoke(worldPosition, blockType);
            
            // Für Server-Synchronisation vormerken
            if (autoSyncToServer)
            {
                pendingServerUpdates.Enqueue(new BlockModification(worldPosition, oldBlockType, blockType));
            }
            
            // Starte Coroutine zum Tracking der Regenerierung
            StartCoroutine(TrackChunkRegeneration(chunkCoord, chunkManager));
        }
        else
        {
            regeneratingChunks.Remove(chunkCoord);
        }

        return success;
    }

    /// <summary>
    /// Trackt den Regenerierungsstatus eines Chunks.
    /// </summary>
    private IEnumerator TrackChunkRegeneration(Vector2Int chunkCoord, ChunkManager chunkManager)
    {
        // Warte, bis die Regenerierung abgeschlossen ist
        while (chunkManager != null && chunkManager.IsRegenerating)
        {
            yield return null;
        }

        // Entferne aus der Regenerierungs-Liste
        regeneratingChunks.Remove(chunkCoord);
        
        // Feuere Event
        OnChunkRegenerated?.Invoke(chunkCoord);
        
        Debug.Log($"Chunk {chunkCoord} regeneration completed");
    }

    /// <summary>
    /// Server-Synchronisations-Loop für Block-Änderungen.
    /// </summary>
    private IEnumerator ServerSyncLoop()
    {
        while (true)
        {
            while (pendingServerUpdates.Count > 0)
            {
                var modification = pendingServerUpdates.Dequeue();
                if (!modification.sentToServer)
                {
                    yield return StartCoroutine(SendBlockUpdateToServer(modification));
                }
            }
            
            yield return new WaitForSeconds(1f); // Prüfe jede Sekunde auf pending updates
        }
    }

    /// <summary>
    /// Sendet eine Block-Änderung an den Server.
    /// </summary>
    private IEnumerator SendBlockUpdateToServer(BlockModification modification)
    {
        if (string.IsNullOrEmpty(chunkUpdateUrl)) yield break;

        // Erstelle URL für Block-Update
        Vector2Int chunkCoord = WorldPositionToChunkCoord(modification.worldPosition);
        string url = $"{chunkUpdateUrl}chunkX={chunkCoord.x}&chunkZ={chunkCoord.y}";
        
        // Erstelle JSON payload (du musst das an dein Server-API anpassen)
        var updateData = new
        {
            worldPosition = modification.worldPosition,
            oldBlockType = modification.oldBlockType,
            newBlockType = modification.newBlockType,
            timestamp = modification.timestamp.ToString("yyyy-MM-dd HH:mm:ss")
        };
        
        string jsonData = JsonUtility.ToJson(updateData);
        
        using (UnityWebRequest request = UnityWebRequest.Put(url, jsonData))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                modification.sentToServer = true;
                Debug.Log($"Block update sent to server: {modification.newBlockType} at {modification.worldPosition}");
            }
            else
            {
                Debug.LogWarning($"Failed to send block update to server: {request.error}");
                // Füge zurück zur Warteschlange für erneuten Versuch
                pendingServerUpdates.Enqueue(modification);
            }
        }
    }    

    /// <summary>
    /// Gibt Statistiken über das Block-Management zurück.
    /// </summary>
    public BlockManagementStats GetBlockManagementStats()
    {
        int totalBlocks = 0;
        int totalVertices = 0;
        int loadedChunkCount = _loadedChunks.Count;
        int regeneratingCount = regeneratingChunks.Count;

        foreach (var chunkManager in _loadedChunks.Values)
        {
            if (chunkManager != null && chunkManager.IsLoaded)
            {
                ChunkInfo chunkInfo = chunkManager.GetChunkInfo();
                if (chunkInfo != null)
                {
                    totalBlocks += chunkInfo.BlockCount;
                }
                totalVertices += chunkManager.VertexCount;
            }
        }

        return new BlockManagementStats
        {
            LoadedChunks = loadedChunkCount,
            RegeneratingChunks = regeneratingCount,
            TotalBlocks = totalBlocks,
            TotalVertices = totalVertices,
            PendingServerUpdates = pendingServerUpdates.Count
        };
    }

    /// <summary>
    /// Führt eine Bereinigung nicht mehr benötigter Chunk-Referenzen durch.
    /// </summary>
    public void CleanupDestroyedChunks()
    {
        var toRemove = new List<Vector2Int>();
        
        foreach (var kvp in _loadedChunks)
        {
            if (kvp.Value == null || kvp.Value._go == null)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var coord in toRemove)
        {
            UnregisterChunkManager(coord);
            _loadedChunks.Remove(coord);
        }

        if (toRemove.Count > 0)
        {
            Debug.Log($"Cleaned up {toRemove.Count} destroyed chunk references");
        }
    }

    // *** BESTEHENDE METHODEN (unverändert) ***

    // Wird von Chunk genutzt, um die passende Material-Instanz für einen Blocktyp zu bekommen
    public Material GetMaterialForBlock(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return standardBlockMaterial;

        if (_materialCache.TryGetValue(blockName, out var cachedMat))
            return cachedMat;

        Material mat = new Material(standardBlockMaterial);

        string lower = blockName.ToLowerInvariant();
        if (lower.Contains("water"))
        {
            mat = new Material(waterMaterial);
        }
        else if (lower.Contains("leaves"))
        {
            mat = new Material(leavesMaterial);
        }
        else
        {
            // Texturen laden
            BlockTextureData texData = LoadBlockTextures(lower);

            if (texData.HasSeparateSides)
            {
                // Shader muss dafür vorbereitet sein (mit _MainTex_Top, _MainTex_Bottom, ...)
                if (texData.top)    mat.SetTexture("_TopTex", texData.top);
                if (texData.bottom) mat.SetTexture("_BottomTex", texData.bottom);
                if (texData.front)  mat.SetTexture("_FrontTex", texData.front);
                if (texData.back)   mat.SetTexture("_BackTex", texData.back);
                if (texData.left)   mat.SetTexture("_LeftTex", texData.left);
                if (texData.right)  mat.SetTexture("_RightTex", texData.right);
            }
            else if (texData.top != null)
            {
                // Eine einzige Textur für alle Seiten
                mat.mainTexture = texData.top;
            }
        }

        _materialCache[blockName] = mat;
        return mat;
    }
    private BlockTextureData LoadBlockTextures(string blockName)
    {
        BlockTextureData data = new BlockTextureData();

        // Basisname (z. B. "stone", "grass")
        string basePath = $"{textureResourceFolder}/{blockName.Split(':')[1]}";

        // Prüfe, ob eine einzelne Textur existiert
        Texture2D single = Resources.Load<Texture2D>(basePath);
        if (single != null)
        {
            data.top = data.bottom = data.front = data.back = data.left = data.right = single;
            return data;
        }

        // Sonst einzelne Seiten versuchen
        data.top    = Resources.Load<Texture2D>($"{basePath}_top");
        data.bottom = Resources.Load<Texture2D>($"{basePath}_bottom");
        data.front  = Resources.Load<Texture2D>($"{basePath}_side");
        data.back   = Resources.Load<Texture2D>($"{basePath}_side");
        data.left   = Resources.Load<Texture2D>($"{basePath}_side");
        data.right  = Resources.Load<Texture2D>($"{basePath}_side");

        return data;
    }


    // --- Turtle (optional) ---
    IEnumerator SpawnOrUpdateTurtle()
    {
        while (true)
        {
            if (string.IsNullOrEmpty(statusUrl)) { yield return new WaitForSeconds(2f); continue; }

            using UnityWebRequest req = UnityWebRequest.Get(statusUrl);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<StatusWrapper>(req.downloadHandler.text);
                if (parsed?.entries == null) { yield return new WaitForSeconds(2f); continue; }

                foreach (var status in parsed.entries)
                {
                    Vector3 pos = new(-status.position.x, status.position.y, status.position.z);
                    if (turtleInstance == null)
                    {
                        turtleInstance = Instantiate(turtlePrefab, pos, Quaternion.identity);
                        turtleInstance.name = status.label;
                        turtleInstance.transform.rotation = Quaternion.LookRotation(DirectionToVector(status.direction));
                    }
                    else
                    {
                        var rts = turtleInstance.GetComponent<RTSController>();
                        if (rts == null || !rts.isMoving)
                        {
                            turtleInstance.transform.position = pos;
                            turtleInstance.transform.rotation = Quaternion.LookRotation(DirectionToVector(status.direction));
                        }
                    }
                }
            }
            yield return new WaitForSeconds(2f);
        }
    }

    Vector3 DirectionToVector(string dir) => dir switch
    {
        "north" => Vector3.back,
        "south" => Vector3.forward,
        "east" => Vector3.left,
        "west" => Vector3.right,
        _ => Vector3.forward,
    };

    // *** FRUSTUM-BASED CHUNK LOADING METHODS ***

    /// <summary>
    /// Gets all chunks visible in camera frustum
    /// </summary>
    private HashSet<Vector2Int> GetFrustumVisibleChunks()
    {
        HashSet<Vector2Int> visible = new HashSet<Vector2Int>();
        if (_cam == null) return visible;

        // Get camera frustum planes
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_cam);

        // Get camera chunk position
        Vector3 camPos = _cam.transform.position;
        Vector2Int centerChunk = new Vector2Int(
            Mathf.FloorToInt(-camPos.x / chunkSize),
            Mathf.FloorToInt(camPos.z / chunkSize)
        );

        // Check chunks in radius around camera
        for (int x = -maxFrustumCheckDistance; x <= maxFrustumCheckDistance; x++)
        {
            for (int z = -maxFrustumCheckDistance; z <= maxFrustumCheckDistance; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(centerChunk.x + x, centerChunk.y + z);

                // Create bounds for this chunk
                Bounds chunkBounds = GetChunkBounds(chunkCoord);

                // Test if chunk is in frustum
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, chunkBounds))
                {
                    visible.Add(chunkCoord);
                }
            }
        }

        return visible;
    }

    /// <summary>
    /// Gets buffer ring chunks around visible chunks
    /// </summary>
    private HashSet<Vector2Int> GetBufferRingChunks(HashSet<Vector2Int> visibleChunks, int rings)
    {
        HashSet<Vector2Int> buffer = new HashSet<Vector2Int>();

        foreach (var chunk in visibleChunks)
        {
            for (int ring = 1; ring <= rings; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int z = -ring; z <= ring; z++)
                    {
                        // Only add chunks on the ring boundary
                        if (Mathf.Abs(x) < ring && Mathf.Abs(z) < ring)
                            continue;

                        Vector2Int neighbor = new Vector2Int(chunk.x + x, chunk.y + z);

                        // Don't add if already visible
                        if (!visibleChunks.Contains(neighbor))
                        {
                            buffer.Add(neighbor);
                        }
                    }
                }
            }
        }

        return buffer;
    }

    /// <summary>
    /// Gets world-space bounds for a chunk coordinate
    /// </summary>
    private Bounds GetChunkBounds(Vector2Int chunkCoord)
    {
        // Convert chunk coordinate to world position (X is negated)
        float worldX = -chunkCoord.x * chunkSize;
        float worldZ = chunkCoord.y * chunkSize;

        Vector3 center = new Vector3(
            worldX + chunkSize * 0.5f,
            128f, // Approximate center height
            worldZ + chunkSize * 0.5f
        );

        Vector3 size = new Vector3(chunkSize, 256f, chunkSize);

        return new Bounds(center, size);
    }

    // Public API for external access to chunks (erweitert)
    public ChunkManager GetChunkAt(Vector2Int coord)
    {
        _loadedChunks.TryGetValue(coord, out ChunkManager chunk);
        return chunk;
    }

    public ChunkManager GetChunkContaining(Vector3 worldPos)
    {
        Vector2Int coord = new Vector2Int(
            Mathf.FloorToInt(-worldPos.x / chunkSize),
            Mathf.FloorToInt(worldPos.z / chunkSize)
        );
        return GetChunkAt(coord);
    }

    public List<ChunkManager> GetAllLoadedChunks()
    {
        return new List<ChunkManager>(_loadedChunks.Values);
    }

    /// <summary>
    /// Gibt alle geladenen Chunk-Koordinaten zurück.
    /// </summary>
    public IEnumerable<Vector2Int> GetLoadedChunkCoordinates()
    {
        return _loadedChunks.Keys;
    }

    // Debug-Methoden
    void Update()
    {
        // Periodische Bereinigung (alle 30 Sekunden)
        if (Time.frameCount % (30 * 60) == 0) // Annahme: 60 FPS
        {
            CleanupDestroyedChunks();
        }
    }

    void OnGUI()
    {
        if (!enableBlockInteractions) return;

        // Debug-Interface (nur wenn Block-Interaktionen aktiviert sind)
        var stats = GetBlockManagementStats();
        GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 140));
        GUILayout.BeginVertical("Box");
        
        GUILayout.Label("Chunk Management Stats:");
        GUILayout.Label($"Loaded Chunks: {stats.LoadedChunks}");
        GUILayout.Label($"Regenerating: {stats.RegeneratingChunks}");
        GUILayout.Label($"Total Blocks: {stats.TotalBlocks}");
        GUILayout.Label($"Total Vertices: {stats.TotalVertices}");
        if (autoSyncToServer)
        {
            GUILayout.Label($"Pending Server Updates: {stats.PendingServerUpdates}");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}

/// <summary>
/// Erweiterte Statistik-Struktur für das Block-Management.
/// </summary>
[System.Serializable]
public struct BlockManagementStats
{
    public int LoadedChunks;
    public int RegeneratingChunks;
    public int TotalBlocks;
    public int TotalVertices;
    public int PendingServerUpdates;
}