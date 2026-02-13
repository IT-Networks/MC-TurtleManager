using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Debug = UnityEngine.Debug;

/// <summary>
/// Area selection visualizer with object pooling and cached materials.
///
/// PERFORMANCE OPTIMIZATIONS:
/// - Marker object pool: reuses GameObjects instead of Instantiate/Destroy per frame
/// - Shared materials: ONE transparent material + ONE line material created at startup
/// - MaterialPropertyBlock: per-marker color changes without material copies
/// - Bounding box reuse: single wireframe box repositioned/rescaled instead of recreated
/// </summary>
public class AreaSelectionVisualizer : MonoBehaviour
{
    [Header("Visualization Prefabs")]
    public GameObject selectionMarkerPrefab;
    public GameObject boundingBoxPrefab;

    [Header("Materials")]
    public Material selectionMaterial;

    [Header("Colors")]
    public Color miningColor = new Color(1f, 0.3f, 0.2f, 0.5f);
    public Color buildingColor = new Color(0.2f, 1f, 0.3f, 0.5f);
    public Color persistentColor = new Color(0.8f, 0.8f, 0.2f, 0.6f);
    public Color invalidColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    public Color validColor = new Color(0.2f, 1f, 0.3f, 0.6f);

    [Header("Settings")]
    public bool showPersistentMarkers = true;
    public bool showBoundingBox = true;
    public float markerSize = 1.02f;
    public float boundingBoxLineWidth = 0.1f;

    [Header("References")]
    public AreaSelectionManager areaSelectionManager;

    private GameObject currentSelectionGroup;

    // Object pool for selection markers — avoids Instantiate/Destroy per frame
    private readonly List<GameObject> _markerPool = new List<GameObject>();
    private int _activeMarkerCount = 0;

    // Cached materials — created once at startup, never per-block
    private Material _sharedTransparentMaterial;
    private Material _sharedLineMaterial;
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    // Reusable MaterialPropertyBlock — zero-allocation per-marker color changes
    private MaterialPropertyBlock _propertyBlock;

    // Bounding box — created once, repositioned on updates
    private GameObject _boundingBoxObj;
    private LineRenderer[] _boundingBoxLines;
    private static readonly Vector3[] BBoxCorners = new Vector3[8]
    {
        new Vector3(-0.5f, -0.5f, -0.5f),
        new Vector3( 0.5f, -0.5f, -0.5f),
        new Vector3( 0.5f, -0.5f,  0.5f),
        new Vector3(-0.5f, -0.5f,  0.5f),
        new Vector3(-0.5f,  0.5f, -0.5f),
        new Vector3( 0.5f,  0.5f, -0.5f),
        new Vector3( 0.5f,  0.5f,  0.5f),
        new Vector3(-0.5f,  0.5f,  0.5f)
    };
    private static readonly int[][] BBoxEdges = new int[][]
    {
        new int[] {0, 1}, new int[] {1, 2}, new int[] {2, 3}, new int[] {3, 0},
        new int[] {4, 5}, new int[] {5, 6}, new int[] {6, 7}, new int[] {7, 4},
        new int[] {0, 4}, new int[] {1, 5}, new int[] {2, 6}, new int[] {3, 7}
    };

    private List<PersistentAreaMarker> persistentMarkers = new List<PersistentAreaMarker>();

    private void Start()
    {
        if (areaSelectionManager == null)
            areaSelectionManager = FindFirstObjectByType<AreaSelectionManager>();

        if (areaSelectionManager != null)
        {
            areaSelectionManager.OnAreaSelected += OnAreaSelected;
            areaSelectionManager.OnSelectionCleared += OnSelectionCleared;
        }

        _propertyBlock = new MaterialPropertyBlock();
        _sharedTransparentMaterial = CreateHDRPTransparentMaterial();
        _sharedLineMaterial = CreateHDRPLineMaterial();

        SetupVisualizationGroup();
        CreateReusableBoundingBox();
    }

    private void SetupVisualizationGroup()
    {
        currentSelectionGroup = new GameObject("CurrentSelectionGroup");
        currentSelectionGroup.transform.SetParent(transform);
    }

    #region Cached Material Creation (once at startup)

    private Material CreateHDRPTransparentMaterial()
    {
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);

        if (mat.HasProperty("_SurfaceType"))
        {
            mat.SetFloat("_SurfaceType", 1);
            mat.SetFloat("_BlendMode", 0);
            mat.SetFloat("_AlphaCutoffEnable", 0);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.renderQueue = 3000;
        }
        else
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        return mat;
    }

    private Material CreateHDRPLineMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);

        if (mat.HasProperty("_SurfaceType"))
        {
            mat.SetFloat("_SurfaceType", 1);
            mat.SetFloat("_BlendMode", 0);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_AlphaCutoffEnable", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.renderQueue = 3000;
        }
        else
        {
            mat.SetInt("_Cull", 0);
        }

        return mat;
    }

    #endregion

    #region Marker Object Pool

    /// <summary>
    /// Get a marker from the pool or create a new one. Uses shared material.
    /// </summary>
    private GameObject AcquireMarker(Vector3 position, Color color, float size)
    {
        GameObject marker;

        if (_activeMarkerCount < _markerPool.Count)
        {
            // Reuse existing pooled marker
            marker = _markerPool[_activeMarkerCount];
            marker.SetActive(true);
        }
        else
        {
            // Create new marker and add to pool
            if (selectionMarkerPrefab != null)
            {
                marker = Instantiate(selectionMarkerPrefab);
            }
            else
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(marker.GetComponent<Collider>());
            }

            // Assign shared material ONCE (no per-block material copy)
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _sharedTransparentMaterial;
            }

            marker.transform.SetParent(currentSelectionGroup.transform);
            _markerPool.Add(marker);
        }

        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * size;

        // Set per-marker color via MaterialPropertyBlock (zero allocation)
        Renderer rend = marker.GetComponent<Renderer>();
        if (rend != null)
        {
            _propertyBlock.SetColor(ColorID, color);
            _propertyBlock.SetColor(BaseColorID, color);
            rend.SetPropertyBlock(_propertyBlock);
        }

        _activeMarkerCount++;
        return marker;
    }

    /// <summary>
    /// Return all active markers to the pool (deactivate, don't destroy).
    /// </summary>
    private void ReturnAllMarkersToPool()
    {
        for (int i = 0; i < _activeMarkerCount; i++)
        {
            if (_markerPool[i] != null)
                _markerPool[i].SetActive(false);
        }
        _activeMarkerCount = 0;
    }

    #endregion

    #region Reusable Bounding Box

    private void CreateReusableBoundingBox()
    {
        _boundingBoxObj = new GameObject("BoundingBox");
        _boundingBoxObj.transform.SetParent(currentSelectionGroup.transform);
        _boundingBoxObj.SetActive(false);

        _boundingBoxLines = new LineRenderer[12];
        for (int i = 0; i < 12; i++)
        {
            GameObject lineObj = new GameObject($"Edge_{i}");
            lineObj.transform.SetParent(_boundingBoxObj.transform, false);

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.startWidth = boundingBoxLineWidth;
            line.endWidth = boundingBoxLineWidth;
            line.positionCount = 2;
            line.material = _sharedLineMaterial;
            line.useWorldSpace = false;

            _boundingBoxLines[i] = line;
        }
    }

    private void UpdateBoundingBox(Vector3 min, Vector3 max, Color color)
    {
        if (_boundingBoxObj == null) return;

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        _boundingBoxObj.transform.position = center;
        _boundingBoxObj.SetActive(true);

        color.a = 1f;

        for (int i = 0; i < 12; i++)
        {
            LineRenderer line = _boundingBoxLines[i];
            int[] edge = BBoxEdges[i];

            line.SetPosition(0, Vector3.Scale(BBoxCorners[edge[0]], size));
            line.SetPosition(1, Vector3.Scale(BBoxCorners[edge[1]], size));

            line.startColor = color;
            line.endColor = color;
        }
    }

    private void HideBoundingBox()
    {
        if (_boundingBoxObj != null)
            _boundingBoxObj.SetActive(false);
    }

    #endregion

    #region Selection Visualization

    private void OnAreaSelected(List<Vector3> blocks, AreaSelectionManager.SelectionMode mode)
    {
        ClearCurrentSelection();
        VisualizeSelection(blocks, mode);
    }

    private void OnSelectionCleared()
    {
        ClearCurrentSelection();
    }

    private void VisualizeSelection(List<Vector3> blocks, AreaSelectionManager.SelectionMode mode)
    {
        Color color = mode == AreaSelectionManager.SelectionMode.Mining ? miningColor : buildingColor;

        for (int i = 0; i < blocks.Count; i++)
        {
            AcquireMarker(blocks[i], color, markerSize);
        }

        if (showBoundingBox && blocks.Count > 0)
        {
            CalculateBoundsAndShow(blocks, color);
        }
    }

    private void CalculateBoundsAndShow(List<Vector3> blocks, Color color)
    {
        Vector3 min = blocks[0];
        Vector3 max = blocks[0];

        for (int i = 1; i < blocks.Count; i++)
        {
            min = Vector3.Min(min, blocks[i]);
            max = Vector3.Max(max, blocks[i]);
        }

        min -= Vector3.one * 0.5f;
        max += Vector3.one * 0.5f;

        UpdateBoundingBox(min, max, color);
    }

    private void ClearCurrentSelection()
    {
        ReturnAllMarkersToPool();
        HideBoundingBox();
    }

    #endregion

    #region Persistent Markers

    public void AddPersistentMarker(List<Vector3> blocks, TaskType type, int taskId)
    {
        if (!showPersistentMarkers) return;

        PersistentAreaMarker marker = new PersistentAreaMarker
        {
            taskId = taskId,
            taskType = type,
            blocks = new List<Vector3>(blocks),
            visualObjects = new List<GameObject>()
        };

        Color color = type == TaskType.Mining ? miningColor : buildingColor;
        color.a = persistentColor.a;

        // Persistent markers use dedicated GameObjects (not pooled — they live long-term)
        foreach (var block in blocks)
        {
            GameObject vis = CreatePersistentMarker(block, color, markerSize * 0.98f);
            vis.transform.SetParent(transform);
            marker.visualObjects.Add(vis);
        }

        persistentMarkers.Add(marker);
    }

    /// <summary>
    /// Creates a dedicated marker for persistent visualization (not pooled).
    /// Uses shared material + MaterialPropertyBlock.
    /// </summary>
    private GameObject CreatePersistentMarker(Vector3 position, Color color, float size)
    {
        GameObject marker;
        if (selectionMarkerPrefab != null)
        {
            marker = Instantiate(selectionMarkerPrefab);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(marker.GetComponent<Collider>());
        }

        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * size;

        Renderer rend = marker.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = _sharedTransparentMaterial;
            _propertyBlock.SetColor(ColorID, color);
            _propertyBlock.SetColor(BaseColorID, color);
            rend.SetPropertyBlock(_propertyBlock);
        }

        return marker;
    }

    public void RemovePersistentMarker(int taskId)
    {
        var marker = persistentMarkers.FirstOrDefault(m => m.taskId == taskId);
        if (marker != null)
        {
            foreach (var obj in marker.visualObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            persistentMarkers.Remove(marker);
        }
    }

    public void ClearAllPersistentMarkers()
    {
        foreach (var marker in persistentMarkers)
        {
            foreach (var obj in marker.visualObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
        }
        persistentMarkers.Clear();
    }

    #endregion

    #region Public API (Compatibility)

    public void AdaptSelectionForStructure(Vector3 origin, Vector3Int structureSize)
    {
        ClearCurrentSelection();

        int capacity = structureSize.x * structureSize.y * structureSize.z;
        List<Vector3> blocks = new List<Vector3>(capacity);

        for (int x = 0; x < structureSize.x; x++)
        {
            for (int y = 0; y < structureSize.y; y++)
            {
                for (int z = 0; z < structureSize.z; z++)
                {
                    blocks.Add(origin + new Vector3(x, y, z));
                }
            }
        }

        VisualizeSelection(blocks, AreaSelectionManager.SelectionMode.Building);
    }

    public void SetMode(AreaSelectionManager.SelectionMode mode)
    {
        // Mode is handled by OnAreaSelected
    }

    public void UpdateSelectionBox(Vector3 start, Vector3 end, AreaSelectionManager.SelectionMode mode)
    {
        int minX = Mathf.FloorToInt(Mathf.Min(start.x, end.x));
        int minY = Mathf.FloorToInt(Mathf.Min(start.y, end.y));
        int minZ = Mathf.FloorToInt(Mathf.Min(start.z, end.z));
        int maxX = Mathf.FloorToInt(Mathf.Max(start.x, end.x));
        int maxY = Mathf.FloorToInt(Mathf.Max(start.y, end.y));
        int maxZ = Mathf.FloorToInt(Mathf.Max(start.z, end.z));

        int capacity = (maxX - minX + 1) * (maxY - minY + 1) * (maxZ - minZ + 1);
        List<Vector3> blocks = new List<Vector3>(capacity);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    blocks.Add(new Vector3(x, y, z));
                }
            }
        }

        ClearCurrentSelection();
        VisualizeSelection(blocks, mode);
    }

    public void UpdateVisualization(List<Vector3> blocks, AreaSelectionManager.SelectionMode mode)
    {
        ClearCurrentSelection();
        VisualizeSelection(blocks, mode);
    }

    public void ClearVisualization()
    {
        ClearCurrentSelection();
    }

    #endregion

    private void OnDestroy()
    {
        if (areaSelectionManager != null)
        {
            areaSelectionManager.OnAreaSelected -= OnAreaSelected;
            areaSelectionManager.OnSelectionCleared -= OnSelectionCleared;
        }

        // Destroy all pooled markers
        foreach (var marker in _markerPool)
        {
            if (marker != null)
                Destroy(marker);
        }
        _markerPool.Clear();

        ClearAllPersistentMarkers();

        // Clean up cached materials
        if (_sharedTransparentMaterial != null) Destroy(_sharedTransparentMaterial);
        if (_sharedLineMaterial != null) Destroy(_sharedLineMaterial);
    }
}

[System.Serializable]
public class PersistentAreaMarker
{
    public int taskId;
    public TaskType taskType;
    public List<Vector3> blocks;
    public List<GameObject> visualObjects;
}
