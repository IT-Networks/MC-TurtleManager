using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enhanced area selection visualizer with persistent markers
/// Shows selected areas for mining/building with different visual styles
/// </summary>
public class AreaSelectionVisualizer : MonoBehaviour
{
    [Header("Visualization Prefabs")]
    public GameObject selectionMarkerPrefab;
    public GameObject boundingBoxPrefab;

    [Header("Colors")]
    public Color miningColor = new Color(1f, 0.3f, 0.2f, 0.5f);
    public Color buildingColor = new Color(0.2f, 1f, 0.3f, 0.5f);
    public Color persistentColor = new Color(0.8f, 0.8f, 0.2f, 0.6f);
    public Color invalidColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

    [Header("Settings")]
    public bool showPersistentMarkers = true;
    public bool showBoundingBox = true;
    public float markerSize = 1.02f;
    public float boundingBoxLineWidth = 0.1f;

    [Header("References")]
    public AreaSelectionManager areaSelectionManager;

    private GameObject currentSelectionGroup;
    private List<GameObject> currentMarkers = new List<GameObject>();
    private GameObject boundingBox;

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

        SetupVisualizationGroup();
    }

    private void SetupVisualizationGroup()
    {
        currentSelectionGroup = new GameObject("CurrentSelectionGroup");
        currentSelectionGroup.transform.SetParent(transform);
    }

    private void OnAreaSelected(List<Vector3> blocks, AreaSelectionManager.SelectionMode mode)
    {
        ClearCurrentSelection();
        VisualizeSelection(blocks, mode);

        if (showBoundingBox)
            CreateBoundingBox(blocks, mode);
    }

    private void OnSelectionCleared()
    {
        ClearCurrentSelection();
    }

    private void VisualizeSelection(List<Vector3> blocks, AreaSelectionManager.SelectionMode mode)
    {
        Color color = mode == AreaSelectionManager.SelectionMode.Mining ? miningColor : buildingColor;

        foreach (var block in blocks)
        {
            GameObject marker = CreateMarker(block, color, markerSize);
            marker.transform.SetParent(currentSelectionGroup.transform);
            currentMarkers.Add(marker);
        }

        Debug.Log($"Visualized {blocks.Count} blocks for {mode}");
    }

    private GameObject CreateMarker(Vector3 position, Color color, float size)
    {
        GameObject marker;

        if (selectionMarkerPrefab != null)
        {
            marker = Instantiate(selectionMarkerPrefab);
        }
        else
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(marker.GetComponent<Collider>()); // Remove collider
        }

        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * size;

        // Set material with transparency
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;

            renderer.material = mat;
        }

        return marker;
    }

    private void CreateBoundingBox(List<Vector3> blocks, AreaSelectionManager.SelectionMode mode)
    {
        if (blocks == null || blocks.Count == 0) return;

        // Calculate bounds
        Vector3 min = blocks[0];
        Vector3 max = blocks[0];

        foreach (var block in blocks)
        {
            min = Vector3.Min(min, block);
            max = Vector3.Max(max, block);
        }

        // Expand by block size
        min -= Vector3.one * 0.5f;
        max += Vector3.one * 0.5f;

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        // Create or reuse bounding box
        if (boundingBox == null)
        {
            if (boundingBoxPrefab != null)
            {
                boundingBox = Instantiate(boundingBoxPrefab, currentSelectionGroup.transform);
            }
            else
            {
                boundingBox = CreateWireframeBoundingBox(center, size, mode);
            }
        }

        boundingBox.transform.position = center;
        boundingBox.transform.localScale = size;
        boundingBox.SetActive(true);
    }

    private GameObject CreateWireframeBoundingBox(Vector3 center, Vector3 size, AreaSelectionManager.SelectionMode mode)
    {
        GameObject box = new GameObject("BoundingBox");
        box.transform.SetParent(currentSelectionGroup.transform);
        box.transform.position = center;

        Color color = mode == AreaSelectionManager.SelectionMode.Mining ? miningColor : buildingColor;
        color.a = 1f; // Full opacity for lines

        // Create 12 edges of the cube
        Vector3[] corners = new Vector3[8]
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

        int[][] edges = new int[][]
        {
            new int[] {0, 1}, new int[] {1, 2}, new int[] {2, 3}, new int[] {3, 0}, // Bottom
            new int[] {4, 5}, new int[] {5, 6}, new int[] {6, 7}, new int[] {7, 4}, // Top
            new int[] {0, 4}, new int[] {1, 5}, new int[] {2, 6}, new int[] {3, 7}  // Vertical
        };

        foreach (var edge in edges)
        {
            CreateLine(box.transform,
                Vector3.Scale(corners[edge[0]], size),
                Vector3.Scale(corners[edge[1]], size),
                color,
                boundingBoxLineWidth);
        }

        return box;
    }

    private void CreateLine(Transform parent, Vector3 start, Vector3 end, Color color, float width)
    {
        GameObject lineObj = new GameObject("Line");
        lineObj.transform.SetParent(parent, false);

        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        line.material = mat;
        line.useWorldSpace = false;
    }

    private void ClearCurrentSelection()
    {
        foreach (var marker in currentMarkers)
        {
            if (marker != null)
                Destroy(marker);
        }
        currentMarkers.Clear();

        if (boundingBox != null)
        {
            boundingBox.SetActive(false);
        }
    }

    // Persistent markers for task queue
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

        foreach (var block in blocks)
        {
            GameObject vis = CreateMarker(block, color, markerSize * 0.98f);
            vis.transform.SetParent(transform);
            marker.visualObjects.Add(vis);
        }

        persistentMarkers.Add(marker);
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

    // Adapt selection area for building mode
    public void AdaptSelectionForStructure(Vector3 origin, Vector3Int structureSize)
    {
        ClearCurrentSelection();

        List<Vector3> blocks = new List<Vector3>();

        for (int x = 0; x < structureSize.x; x++)
        {
            for (int y = 0; y < structureSize.y; y++)
            {
                for (int z = 0; z < structureSize.z; z++)
                {
                    Vector3 blockPos = origin + new Vector3(x, y, z);
                    blocks.Add(blockPos);
                }
            }
        }

        VisualizeSelection(blocks, AreaSelectionManager.SelectionMode.Building);
        CreateBoundingBox(blocks, AreaSelectionManager.SelectionMode.Building);

        Debug.Log($"Adapted selection for structure of size {structureSize}");
    }

    private void OnDestroy()
    {
        if (areaSelectionManager != null)
        {
            areaSelectionManager.OnAreaSelected -= OnAreaSelected;
            areaSelectionManager.OnSelectionCleared -= OnSelectionCleared;
        }

        ClearCurrentSelection();
        ClearAllPersistentMarkers();
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
