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

    [Header("Movement-Based Prioritization")]
    [Tooltip("Prioritize chunks in camera movement direction (like Minecraft)")]
    public bool useMovementPrioritization = true;
    [Tooltip("Minimum camera movement to trigger reprioritization")]
    public float movementThreshold = 2f;

    [Header("Chunk Pooling & Caching")]
    [Tooltip("Enable chunk pooling for performance")]
    public bool useChunkPooling = true;
    [Tooltip("Enable mesh caching for instant reload")]
    public bool enableMeshCaching = true;
    [Tooltip("Maximum pooled chunks (0 = unlimited)")]
    public int maxPooledChunks = 100;
    [Tooltip("Maximum loaded chunks before trimming farthest. 0 = unlimited (chunks stay loaded until explicitly removed)")]
    public int maxLoadedChunks = 0;

    [Header("Block Interaction Settings")]
    [SerializeField] private bool enableBlockInteractions = true;
    [SerializeField] private int maxConcurrentRegenerations = 3;
    [Tooltip("Sollen Block-Änderungen automatisch an den Server gesendet werden?")]
    [SerializeField] private bool autoSyncToServer = false;

    // Laufzeit
    private readonly Dictionary<Vector2Int, ChunkManager> _loadedChunks = new();
    private Vector2Int _currentCameraChunk = new(int.MinValue, int.MinValue);
    private Camera _cam;
    private CameraMovementTracker _movementTracker;
    private ChunkPool _chunkPool;

    // Movement-based prioritization
    private Coroutine _currentChunkLoadingCoroutine;
    private bool _isLoadingChunks = false;
    private Vector3 _lastCameraPosition;
    private float _timeSinceLastMovement = 0f;

    // Optional: Material-Cache pro Blocktyp (für Submeshes)
    private readonly Dictionary<string, Material> _materialCache = new();

    // Block-Management
    private readonly HashSet<Vector2Int> regeneratingChunks = new HashSet<Vector2Int>();
    private readonly Queue<BlockModification> pendingServerUpdates = new Queue<BlockModification>();

    // Chunk pinning - keeps chunks loaded during mining/building regardless of camera
    private readonly HashSet<Vector2Int> _pinnedChunks = new();

    // Events für Block-Interaktionen
    public System.Action<Vector2Int> OnChunkLoaded;
    public System.Action<Vector3, string> OnBlockRemoved;
    public System.Action<Vector3, string> OnBlockPlaced;
    public System.Action<Vector2Int> OnChunkRegenerated;

    // --- Turtle Status (optional, aus deinem Vorgänger übernommen) ---
    private GameObject turtleInstance;

    [Serializable] public class TurtlePosition { public float x, y, z; }
    [Serializable] public class TurtleWorldStatus { public string label; public string direction; public TurtlePosition position; }
    [Serializable] public class StatusWrapper { public List<TurtleWorldStatus> entries; }
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

        // Initialize chunk pool
        if (useChunkPooling)
        {
            _chunkPool = GetComponent<ChunkPool>();
            if (_chunkPool == null)
            {
                _chunkPool = gameObject.AddComponent<ChunkPool>();
                _chunkPool.maxPoolSize = maxPooledChunks;
                _chunkPool.enableMeshCaching = enableMeshCaching;
                Debug.Log("ChunkPool initialized");
            }
        }

        // Initialize movement tracker
        if (useMovementPrioritization && _cam != null)
        {
            _movementTracker = _cam.GetComponent<CameraMovementTracker>();
            if (_movementTracker == null)
            {
                _movementTracker = _cam.gameObject.AddComponent<CameraMovementTracker>();
                _movementTracker.movementThreshold = movementThreshold;
            }
            _lastCameraPosition = _cam.transform.position;
        }

        StartCoroutine(ChunkStreamingLoop());
        // NOTE: Turtle spawning now handled by MultiTurtleManager - old system disabled
        // StartCoroutine(SpawnOrUpdateTurtle());

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
        _pinnedChunks.Clear();
        regeneratingChunks.Clear();
        pendingServerUpdates.Clear();
        // NOTE: Turtle cleanup now handled by MultiTurtleManager
        // if (turtleInstance != null) Destroy(turtleInstance);
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

                // Check if camera moved significantly (for movement-based prioritization)
                bool cameraMovedSignificantly = false;
                if (useMovementPrioritization && _movementTracker != null)
                {
                    float distanceMoved = Vector3.Distance(_lastCameraPosition, camPos);
                    cameraMovedSignificantly = distanceMoved > movementThreshold && _movementTracker.IsMoving;
                }

                // Start new loading if chunk changed OR camera moved significantly
                if (camChunk != _currentCameraChunk || (cameraMovedSignificantly && !_isLoadingChunks))
                {
                    // CRITICAL: Always stop the previous coroutine before starting a new one.
                    // Without this, two UpdateLoadedChunks run concurrently - the new one
                    // unloads chunks while the old one is still loading them.
                    if (_currentChunkLoadingCoroutine != null)
                    {
                        StopCoroutine(_currentChunkLoadingCoroutine);
                        _currentChunkLoadingCoroutine = null;
                        _isLoadingChunks = false;
                    }

                    _currentCameraChunk = camChunk;
                    _lastCameraPosition = camPos;

                    _currentChunkLoadingCoroutine = StartCoroutine(UpdateLoadedChunks(camChunk));
                }
            }
            yield return new WaitForSeconds(chunkRefreshInterval);
        }
    }

    IEnumerator UpdateLoadedChunks(Vector2Int camChunk)
    {
        _isLoadingChunks = true;

        HashSet<Vector2Int> needed = new();

        if (useFrustumBasedLoading && _cam != null)
        {
            needed = GetFrustumVisibleChunks();
            if (frustumBufferRings > 0)
            {
                needed.UnionWith(GetBufferRingChunks(needed, frustumBufferRings));
            }
        }
        else
        {
            for (int x = -chunkLoadRadius; x <= chunkLoadRadius; x++)
            {
                for (int z = -chunkLoadRadius; z <= chunkLoadRadius; z++)
                {
                    needed.Add(new Vector2Int(camChunk.x + x, camChunk.y + z));
                }
            }
        }

        needed.UnionWith(_pinnedChunks);

        // --- LOAD: Nur neue Chunks laden, bestehende bleiben erhalten ---
        List<ChunkLoadPriority> prioritizedChunks = PrioritizeChunks(needed, camChunk);

        var chunksToLoad = new List<ChunkLoadPriority>();
        foreach (var chunkPriority in prioritizedChunks)
        {
            if (!_loadedChunks.ContainsKey(chunkPriority.coord))
            {
                chunksToLoad.Add(chunkPriority);
            }
        }

        foreach (var chunkPriority in chunksToLoad)
        {
            Vector2Int coord = chunkPriority.coord;

            // Prüfe ob Kamera sich bewegt hat → ChunkStreamingLoop wird neu starten
            Vector3 camPos = _cam.transform.position;
            Vector2Int currentCamChunk = new(
                Mathf.FloorToInt(-camPos.x / (float)chunkSize),
                Mathf.FloorToInt(camPos.z / (float)chunkSize)
            );
            if (currentCamChunk != _currentCameraChunk)
            {
                _isLoadingChunks = false;
                yield break;
            }

            // Unvollständige Chunks ersetzen
            if (_loadedChunks.ContainsKey(coord))
            {
                var existingChunk = _loadedChunks[coord];

                if (existingChunk != null && existingChunk.IsLoaded && existingChunk._go != null && existingChunk.VertexCount > 0)
                {
                    continue;
                }
                else
                {
                    Debug.LogWarning($"Chunk {coord} exists but is not properly loaded, replacing it");
                    UnloadChunk(coord);
                }
            }

            var chunk = new ChunkManager(coord, chunkSize, this);
            _loadedChunks.Add(coord, chunk);

            yield return StartCoroutine(chunk.LoadAndSpawnChunk());

            if (!chunk.IsLoaded || chunk._go == null || chunk.VertexCount == 0)
            {
                Debug.LogWarning($"Chunk {coord} failed to load properly, removing");
                UnloadChunk(coord);
            }
        }

        // --- TRIM: Nur entladen wenn Maximum überschritten ---
        // Chunks bleiben geladen auch wenn sie nicht mehr im Frustum sind.
        // Erst bei Überschreitung des Limits werden die entferntesten entladen.
        if (maxLoadedChunks > 0 && _loadedChunks.Count > maxLoadedChunks)
        {
            TrimFarthestChunks(camChunk, needed);
        }

        _isLoadingChunks = false;
    }

    /// <summary>
    /// Entlädt einen einzelnen Chunk aus _loadedChunks (Pool oder Destroy).
    /// </summary>
    private void UnloadChunk(Vector2Int coord)
    {
        if (!_loadedChunks.TryGetValue(coord, out var chunk))
            return;

        regeneratingChunks.Remove(coord);

        if (chunk != null)
        {
            if (useChunkPooling && _chunkPool != null)
                chunk.ReturnToPool(_chunkPool);
            else
                chunk.DestroyChunk();
        }

        _loadedChunks.Remove(coord);
    }

    /// <summary>
    /// Entfernt die am weitesten entfernten Chunks bis maxLoadedChunks erreicht ist.
    /// Pinned und aktuell benötigte Chunks werden nie entfernt.
    /// </summary>
    private void TrimFarthestChunks(Vector2Int camChunk, HashSet<Vector2Int> needed)
    {
        var candidates = new List<(Vector2Int coord, float distance)>();
        foreach (var kvp in _loadedChunks)
        {
            if (!needed.Contains(kvp.Key) && !_pinnedChunks.Contains(kvp.Key))
            {
                float dist = Vector2Int.Distance(kvp.Key, camChunk);
                candidates.Add((kvp.Key, dist));
            }
        }

        // Entfernteste zuerst
        candidates.Sort((a, b) => b.distance.CompareTo(a.distance));

        int toRemove = _loadedChunks.Count - maxLoadedChunks;
        int removed = 0;
        for (int i = 0; i < toRemove && i < candidates.Count; i++)
        {
            UnloadChunk(candidates[i].coord);
            removed++;
        }

        if (removed > 0)
        {
            Debug.Log($"Trimmed {removed} farthest chunks (total: {_loadedChunks.Count}/{maxLoadedChunks})");
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
    /// Pins chunk coordinates so they stay loaded regardless of camera position.
    /// Used during mining/building operations to prevent frustum-based unloading.
    /// </summary>
    public void PinChunks(IEnumerable<Vector2Int> coords)
    {
        foreach (var coord in coords)
            _pinnedChunks.Add(coord);
    }

    /// <summary>
    /// Unpins chunk coordinates, allowing normal frustum-based unloading again.
    /// </summary>
    public void UnpinChunks(IEnumerable<Vector2Int> coords)
    {
        foreach (var coord in coords)
            _pinnedChunks.Remove(coord);
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

            // Special color tinting for grass blocks (green top)
            if (lower.Contains("grass"))
            {
                mat.SetColor("_TopColor", new Color(0.4f, 0.8f, 0.3f, 1f)); // Grass green
                // Keep other sides at default white (1,1,1,1)
                mat.SetColor("_BottomColor", Color.white);
                mat.SetColor("_FrontColor", Color.white);
                mat.SetColor("_BackColor", Color.white);
                mat.SetColor("_LeftColor", Color.white);
                mat.SetColor("_RightColor", Color.white);
            }
        }

        _materialCache[blockName] = mat;
        return mat;
    }
    private BlockTextureData LoadBlockTextures(string blockName)
    {
        BlockTextureData data = new BlockTextureData();

        // Basisname (z. B. "stone", "grass")
        // Handle both "minecraft:stone" and "stone" formats
        string[] parts = blockName.Split(':');
        string baseName = parts.Length > 1 ? parts[1] : parts[0];
        string basePath = $"{textureResourceFolder}/{baseName}";

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
                // Parse turtle status (supports both array and object formats)
                List<TurtleWorldStatus> statusList = ParseTurtleStatusJson(req.downloadHandler.text);

                if (statusList == null || statusList.Count == 0)
                {
                    yield return new WaitForSeconds(2f);
                    continue;
                }

                foreach (var status in statusList)
                {
                    // Clamp Y coordinate to valid Minecraft range (-64 to 320)
                    // Prevents turtle from spawning below bedrock or above build limit
                    float clampedY = Mathf.Clamp(status.position.y, -64f, 320f);

                    if (clampedY != status.position.y)
                    {
                        Debug.LogWarning($"Turtle '{status.label}' Y position clamped from {status.position.y} to {clampedY} (valid range: -64 to 320)");
                    }

                    // Apply world Y offset (Minecraft Y=-64 becomes Unity Y=0)
                    Vector3 pos = new(-status.position.x, clampedY + 64f, status.position.z);

                    if (turtleInstance == null)
                    {
                        turtleInstance = Instantiate(turtlePrefab, pos, Quaternion.identity);
                        turtleInstance.name = status.label;
                        turtleInstance.transform.rotation = Quaternion.LookRotation(DirectionToVector(status.direction));
                    }
                    else
                    {
                        // Update position and rotation when turtle exists
                        turtleInstance.transform.position = pos;
                        turtleInstance.transform.rotation = Quaternion.LookRotation(DirectionToVector(status.direction));
                    }
                }
            }
            yield return new WaitForSeconds(2f);
        }
    }

    private List<TurtleWorldStatus> ParseTurtleStatusJson(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText))
            return null;

        try
        {
            // Try array format first (newer API)
            var statusArray = Newtonsoft.Json.JsonConvert.DeserializeObject<List<TurtleWorldStatus>>(jsonText);
            if (statusArray != null && statusArray.Count > 0)
                return statusArray;
        }
        catch (Newtonsoft.Json.JsonException)
        {
            // Not array format, try object format
        }

        try
        {
            // Try object format with wrapper
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<StatusWrapper>(jsonText);
            if (parsed?.entries != null && parsed.entries.Count > 0)
                return parsed.entries;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to parse turtle status JSON: {ex.Message}");
        }

        return null;
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

        // Calculate actual view distance based on camera far plane
        float viewDistance = _cam.farClipPlane;
        int actualCheckDistance = Mathf.Min(
            Mathf.CeilToInt(viewDistance / chunkSize) + 2, // +2 for safety margin
            maxFrustumCheckDistance
        );

        // Check chunks in calculated radius around camera
        for (int x = -actualCheckDistance; x <= actualCheckDistance; x++)
        {
            for (int z = -actualCheckDistance; z <= actualCheckDistance; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(centerChunk.x + x, centerChunk.y + z);

                // Quick distance check first (cheaper than bounds test)
                float chunkDistance = Mathf.Sqrt(x * x + z * z) * chunkSize;
                if (chunkDistance > viewDistance + chunkSize)
                    continue; // Too far, skip

                // Create bounds for this chunk
                Bounds chunkBounds = GetChunkBounds(chunkCoord);

                // Test if chunk is actually in frustum
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
    /// Prioritizes chunks based on camera movement direction (Minecraft-style)
    /// </summary>
    private List<ChunkLoadPriority> PrioritizeChunks(HashSet<Vector2Int> chunks, Vector2Int cameraChunk)
    {
        List<ChunkLoadPriority> prioritized = new List<ChunkLoadPriority>();

        Vector3 cameraPos = _cam != null ? _cam.transform.position : Vector3.zero;
        Vector3 movementDir = Vector3.zero;

        // Get movement direction if tracker is available
        if (useMovementPrioritization && _movementTracker != null && _movementTracker.IsMoving)
        {
            movementDir = _movementTracker.MovementDirection;
        }

        foreach (var chunkCoord in chunks)
        {
            float priority = CalculateChunkPriority(chunkCoord, cameraChunk, cameraPos, movementDir);
            prioritized.Add(new ChunkLoadPriority { coord = chunkCoord, priority = priority });
        }

        // Sort by priority (higher priority first)
        prioritized.Sort((a, b) => b.priority.CompareTo(a.priority));

        return prioritized;
    }

    /// <summary>
    /// Calculates loading priority for a chunk
    /// Priority factors:
    /// 1. Distance from camera (closer = MUCH higher priority)
    /// 2. Alignment with movement direction (in front = higher)
    /// 3. Currently visible in frustum (visible = moderate boost)
    /// </summary>
    private float CalculateChunkPriority(Vector2Int chunkCoord, Vector2Int cameraChunk, Vector3 cameraPos, Vector3 movementDir)
    {
        float priority = 100f; // Base priority

        // Factor 1: Distance (MUCH stronger priority for close chunks)
        float distance = Vector2Int.Distance(chunkCoord, cameraChunk);

        // Exponential falloff for distance - very close chunks get MUCH higher priority
        // Distance 0: +500, Distance 1: +250, Distance 2: +125, Distance 3: +62.5, etc.
        float distancePriority = 500f / (distance + 1f);
        priority += distancePriority;

        // Factor 2: Movement direction alignment (reduced from 75 to 30)
        if (useMovementPrioritization && movementDir != Vector3.zero)
        {
            // Get direction from camera to chunk center
            Bounds chunkBounds = GetChunkBounds(chunkCoord);
            Vector3 toChunk = (chunkBounds.center - cameraPos).normalized;

            // Ignore Y component for horizontal alignment
            Vector3 toChunkFlat = new Vector3(toChunk.x, 0, toChunk.z).normalized;
            Vector3 movementFlat = new Vector3(movementDir.x, 0, movementDir.z).normalized;

            if (toChunkFlat != Vector3.zero && movementFlat != Vector3.zero)
            {
                // Dot product: 1 = same direction, -1 = opposite direction
                float alignment = Vector3.Dot(toChunkFlat, movementFlat);

                // Moderate boost for chunks in movement direction (reduced from 75 to 30)
                float movementPriority = alignment * 30f; // -30 to +30
                priority += movementPriority;

                // Extra boost for chunks directly ahead (reduced from 50 to 20)
                if (alignment > 0.8f) // Within ~36 degrees
                {
                    priority += 20f; // Extra priority for straight ahead
                }
            }
        }

        // Factor 3: Frustum visibility (REDUCED from +100 to +50)
        // Visible chunks get boost, but not so much that they override distance
        if (useFrustumBasedLoading && _cam != null)
        {
            Bounds chunkBounds = GetChunkBounds(chunkCoord);
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_cam);

            if (GeometryUtility.TestPlanesAABB(frustumPlanes, chunkBounds))
            {
                priority += 50f; // Moderate boost for visible chunks (was 100)
            }
        }

        return priority;
    }

    /// <summary>
    /// Helper class for chunk loading prioritization
    /// </summary>
    private class ChunkLoadPriority
    {
        public Vector2Int coord;
        public float priority;
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