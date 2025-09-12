using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Verwaltet das NavMeshSurface für einen einzelnen Chunk
/// </summary>
public class ChunkNavMeshManager : MonoBehaviour
{
    private NavMeshSurface navMeshSurface;
    private ChunkManager chunkManager;
    private bool isBuilt = false;
    private Coroutine buildCoroutine;
    
    // NavMesh Settings
    [Header("NavMesh Settings")]
    public int agentTypeID = 0;
    public CollectObjects collectObjects = CollectObjects.Children;
    public LayerMask includeLayers = -1;
    public NavMeshCollectGeometry useGeometry = NavMeshCollectGeometry.RenderMeshes;
    public int defaultArea = 0;
    public bool ignoreNavMeshAgent = true;
    public bool ignoreNavMeshObstacle = true;
    public bool overrideTileSize = false;
    public int tileSize = 256;
    public bool overrideVoxelSize = false;
    public float voxelSize = 0.166f;
    public bool buildHeightMesh = false;
    public float minRegionArea = 2f;
    
    /// <summary>
    /// Initialisiert den NavMesh Manager für einen Chunk
    /// </summary>
    public void Initialize(ChunkManager chunk)
    {
        chunkManager = chunk;
        gameObject.name = $"NavMesh_{chunk.coord.x}_{chunk.coord.y}";
        
        // NavMeshSurface Komponente hinzufügen
        navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        ConfigureNavMeshSurface();
    }
    
    private void ConfigureNavMeshSurface()
    {
        if (navMeshSurface == null) return;
        
        // Konfiguriere NavMeshSurface mit den Einstellungen
        navMeshSurface.agentTypeID = agentTypeID;
        navMeshSurface.collectObjects = collectObjects;
        navMeshSurface.layerMask = includeLayers;
        navMeshSurface.useGeometry = useGeometry;
        navMeshSurface.defaultArea = defaultArea;
        navMeshSurface.ignoreNavMeshAgent = ignoreNavMeshAgent;
        navMeshSurface.ignoreNavMeshObstacle = ignoreNavMeshObstacle;
        navMeshSurface.overrideTileSize = overrideTileSize;
        navMeshSurface.tileSize = tileSize;
        navMeshSurface.overrideVoxelSize = overrideVoxelSize;
        navMeshSurface.voxelSize = voxelSize;
        navMeshSurface.buildHeightMesh = buildHeightMesh;
        navMeshSurface.minRegionArea = minRegionArea;
    }
    
    /// <summary>
    /// Baut das NavMesh für diesen Chunk
    /// </summary>
    public IEnumerator BuildNavMeshAsync()
    {
        if (navMeshSurface == null || chunkManager == null)
        {
            Debug.LogWarning($"Cannot build NavMesh - missing components");
            yield break;
        }
        
        // Warte bis der Chunk vollständig geladen ist
        while (!chunkManager.IsLoaded)
        {
            yield return null;
        }
        
        // Kleine Verzögerung damit die Mesh Collider sicher gesetzt sind
        yield return new WaitForSeconds(0.1f);
        
        // Baue NavMesh
        navMeshSurface.BuildNavMesh();
        isBuilt = true;
        
        Debug.Log($"NavMesh built for chunk {chunkManager.coord}");
    }
    
    /// <summary>
    /// Aktualisiert das NavMesh nach Änderungen
    /// </summary>
    public IEnumerator UpdateNavMeshAsync(Bounds updateBounds)
    {
        if (!isBuilt || navMeshSurface == null)
        {
            yield return BuildNavMeshAsync();
            yield break;
        }
        
        // Warte bis keine Regenerierung mehr läuft
        while (chunkManager.IsRegenerating)
        {
            yield return null;
        }
        
        // Aktualisiere nur den betroffenen Bereich
        var asyncOperation = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
        
        while (!asyncOperation.isDone)
        {
            yield return null;
        }
        
        Debug.Log($"NavMesh updated for chunk {chunkManager.coord}");
    }
    
    /// <summary>
    /// Vollständige Neuerstellung des NavMesh
    /// </summary>
    public IEnumerator RebuildNavMeshAsync()
    {
        if (navMeshSurface == null) yield break;
        
        // Warte bis keine Regenerierung mehr läuft
        while (chunkManager.IsRegenerating)
        {
            yield return null;
        }
        
        // Lösche altes NavMesh und baue neu
        navMeshSurface.RemoveData();
        yield return BuildNavMeshAsync();
    }
    
    /// <summary>
    /// Entfernt das NavMesh
    /// </summary>
    public void ClearNavMesh()
    {
        if (buildCoroutine != null)
        {
            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }
        
        if (navMeshSurface != null)
        {
            navMeshSurface.RemoveData();
            isBuilt = false;
        }
    }
    
    private void OnDestroy()
    {
        ClearNavMesh();
    }
    
    // Properties
    public bool IsBuilt => isBuilt;
    public NavMeshSurface Surface => navMeshSurface;
    public Bounds Bounds
    {
        get
        {
            if (chunkManager?._go == null) return new Bounds();
            
            var meshFilter = chunkManager._go.GetComponent<MeshFilter>();
            if (meshFilter?.sharedMesh == null) return new Bounds();
            
            return meshFilter.sharedMesh.bounds;
        }
    }
}

/// <summary>
/// Verwaltet alle Chunk NavMeshes und koordiniert das Pathfinding
/// </summary>
public class ChunkNavMeshCoordinator : MonoBehaviour
{
    private Dictionary<Vector2Int, ChunkNavMeshManager> chunkNavMeshes = new Dictionary<Vector2Int, ChunkNavMeshManager>();
    private TurtleWorldManager worldManager;
    
    [Header("NavMesh Build Settings")]
    [SerializeField] private bool autoBuildOnChunkLoad = true;
    [SerializeField] private float buildDelay = 0.5f;
    [SerializeField] private bool autoRebuildOnBlockChange = true;
    
    // NavMesh Links für Chunk-Übergänge
    private List<NavMeshLink> chunkBridgeLinks = new List<NavMeshLink>();
    
    private void Start()
    {
        worldManager = GetComponent<TurtleWorldManager>();
        if (worldManager == null)
        {
            Debug.LogError("ChunkNavMeshCoordinator requires TurtleWorldManager!");
            enabled = false;
            return;
        }
        
        // Events registrieren
        worldManager.OnBlockRemoved += OnBlockRemoved;
        worldManager.OnBlockPlaced += OnBlockPlaced;
        worldManager.OnChunkRegenerated += OnChunkRegenerated;
    }
    
    private void OnDestroy()
    {
        // Events deregistrieren
        if (worldManager != null)
        {
            worldManager.OnBlockRemoved -= OnBlockRemoved;
            worldManager.OnBlockPlaced -= OnBlockPlaced;
            worldManager.OnChunkRegenerated -= OnChunkRegenerated;
        }
        
        // Alle NavMeshes aufräumen
        foreach (var navMesh in chunkNavMeshes.Values)
        {
            if (navMesh != null)
            {
                Destroy(navMesh.gameObject);
            }
        }
        chunkNavMeshes.Clear();
        
        // Links entfernen
        foreach (var link in chunkBridgeLinks)
        {
            if (link != null)
            {
                Destroy(link.gameObject);
            }
        }
        chunkBridgeLinks.Clear();
    }
    
    /// <summary>
    /// Registriert einen neuen Chunk und erstellt sein NavMesh
    /// </summary>
    public void RegisterChunk(ChunkManager chunk)
    {
        if (chunk == null || chunkNavMeshes.ContainsKey(chunk.coord)) return;
        
        // Erstelle NavMesh Manager für diesen Chunk
        GameObject navMeshGO = new GameObject($"NavMesh_{chunk.coord.x}_{chunk.coord.y}");
        navMeshGO.transform.SetParent(chunk._go.transform);
        navMeshGO.transform.localPosition = Vector3.zero;
        
        ChunkNavMeshManager navMeshManager = navMeshGO.AddComponent<ChunkNavMeshManager>();
        navMeshManager.Initialize(chunk);
        
        chunkNavMeshes[chunk.coord] = navMeshManager;
        
        if (autoBuildOnChunkLoad)
        {
            StartCoroutine(BuildNavMeshDelayed(navMeshManager, buildDelay));
        }
        
        // Erstelle Links zu benachbarten Chunks
        CreateChunkLinks(chunk.coord);
    }
    
    /// <summary>
    /// Entfernt einen Chunk und sein NavMesh
    /// </summary>
    public void UnregisterChunk(Vector2Int coord)
    {
        if (chunkNavMeshes.TryGetValue(coord, out var navMeshManager))
        {
            if (navMeshManager != null)
            {
                Destroy(navMeshManager.gameObject);
            }
            chunkNavMeshes.Remove(coord);
        }
        
        // Entferne betroffene Links
        RemoveChunkLinks(coord);
    }
    
    private IEnumerator BuildNavMeshDelayed(ChunkNavMeshManager navMeshManager, float delay)
    {
        yield return new WaitForSeconds(delay);
        yield return navMeshManager.BuildNavMeshAsync();
    }
    
    /// <summary>
    /// Erstellt NavMeshLinks zwischen benachbarten Chunks
    /// </summary>
    private void CreateChunkLinks(Vector2Int coord)
    {
        Vector2Int[] neighbors = {
            coord + Vector2Int.up,
            coord + Vector2Int.down,
            coord + Vector2Int.left,
            coord + Vector2Int.right
        };
        
        foreach (var neighbor in neighbors)
        {
            if (chunkNavMeshes.ContainsKey(neighbor))
            {
                CreateLinkBetweenChunks(coord, neighbor);
            }
        }
    }
    
    /// <summary>
    /// Erstellt einen NavMeshLink zwischen zwei Chunks
    /// </summary>
    private void CreateLinkBetweenChunks(Vector2Int chunk1, Vector2Int chunk2)
    {
        var manager1 = worldManager.GetChunkManager(chunk1);
        var manager2 = worldManager.GetChunkManager(chunk2);
        
        if (manager1 == null || manager2 == null) return;
        
        // Berechne Verbindungspunkte an den Chunk-Grenzen
        Vector3 pos1 = GetChunkBorderPosition(chunk1, chunk2);
        Vector3 pos2 = GetChunkBorderPosition(chunk2, chunk1);
        
        // Erstelle NavMeshLink
        GameObject linkGO = new GameObject($"ChunkLink_{chunk1}_{chunk2}");
        linkGO.transform.SetParent(transform);
        
        NavMeshLink link = linkGO.AddComponent<NavMeshLink>();
        link.startPoint = pos1;
        link.endPoint = pos2;
        link.width = worldManager.chunkSize * 0.5f;
        link.bidirectional = true;
        link.autoUpdate = true;
        link.area = 0; // Walkable area
        
        chunkBridgeLinks.Add(link);
    }
    
    /// <summary>
    /// Berechnet die Grenzposition zwischen zwei Chunks
    /// </summary>
    private Vector3 GetChunkBorderPosition(Vector2Int fromChunk, Vector2Int toChunk)
    {
        Vector3 chunkCenter = new Vector3(
            -fromChunk.x * worldManager.chunkSize - worldManager.chunkSize * 0.5f,
            0,
            fromChunk.y * worldManager.chunkSize + worldManager.chunkSize * 0.5f
        );
        
        Vector3 direction = new Vector3(
            -(toChunk.x - fromChunk.x) * worldManager.chunkSize * 0.5f,
            0,
            (toChunk.y - fromChunk.y) * worldManager.chunkSize * 0.5f
        );
        
        return chunkCenter + direction;
    }
    
    /// <summary>
    /// Entfernt alle Links die mit einem Chunk verbunden sind
    /// </summary>
    private void RemoveChunkLinks(Vector2Int coord)
    {
        chunkBridgeLinks.RemoveAll(link =>
        {
            if (link != null && link.name.Contains(coord.ToString()))
            {
                Destroy(link.gameObject);
                return true;
            }
            return false;
        });
    }
    
    // Event Handlers
    private void OnBlockRemoved(Vector3 worldPosition, string blockType)
    {
        if (!autoRebuildOnBlockChange) return;
        
        Vector2Int chunkCoord = worldManager.WorldPositionToChunkCoord(worldPosition);
        if (chunkNavMeshes.TryGetValue(chunkCoord, out var navMeshManager))
        {
            StartCoroutine(UpdateNavMeshAfterBlockChange(navMeshManager, worldPosition));
        }
    }
    
    private void OnBlockPlaced(Vector3 worldPosition, string blockType)
    {
        if (!autoRebuildOnBlockChange) return;
        
        Vector2Int chunkCoord = worldManager.WorldPositionToChunkCoord(worldPosition);
        if (chunkNavMeshes.TryGetValue(chunkCoord, out var navMeshManager))
        {
            StartCoroutine(UpdateNavMeshAfterBlockChange(navMeshManager, worldPosition));
        }
    }
    
    private void OnChunkRegenerated(Vector2Int chunkCoord)
    {
        if (chunkNavMeshes.TryGetValue(chunkCoord, out var navMeshManager))
        {
            StartCoroutine(navMeshManager.RebuildNavMeshAsync());
        }
    }
    
    private IEnumerator UpdateNavMeshAfterBlockChange(ChunkNavMeshManager navMeshManager, Vector3 changePosition)
    {
        // Erstelle Update-Bounds um die geänderte Position
        Bounds updateBounds = new Bounds(changePosition, Vector3.one * 3f);
        yield return navMeshManager.UpdateNavMeshAsync(updateBounds);
    }
    
    /// <summary>
    /// Findet einen Pfad zwischen zwei Positionen über mehrere Chunks
    /// </summary>
    public NavMeshPath CalculatePath(Vector3 sourcePosition, Vector3 targetPosition)
    {
        NavMeshPath path = new NavMeshPath();
        
        // Stelle sicher, dass beide Chunks geladen sind
        Vector2Int sourceChunk = worldManager.WorldPositionToChunkCoord(sourcePosition);
        Vector2Int targetChunk = worldManager.WorldPositionToChunkCoord(targetPosition);
        
        if (!chunkNavMeshes.ContainsKey(sourceChunk) || !chunkNavMeshes.ContainsKey(targetChunk))
        {
            Debug.LogWarning($"Cannot calculate path - chunks not loaded");
            return path;
        }
        
        // Berechne Pfad
        NavMesh.CalculatePath(sourcePosition, targetPosition, NavMesh.AllAreas, path);
        
        return path;
    }
    
    /// <summary>
    /// Prüft ob eine Position auf dem NavMesh erreichbar ist
    /// </summary>
    public bool IsPositionOnNavMesh(Vector3 position, float maxDistance = 1f)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas);
    }
    
    /// <summary>
    /// Findet die nächste gültige Position auf dem NavMesh
    /// </summary>
    public bool GetNearestPointOnNavMesh(Vector3 position, out Vector3 nearestPoint, float maxDistance = 10f)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, maxDistance, NavMesh.AllAreas))
        {
            nearestPoint = hit.position;
            return true;
        }
        
        nearestPoint = position;
        return false;
    }
}