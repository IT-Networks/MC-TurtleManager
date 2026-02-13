using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages building mode with automatic area size adaptation based on structure size
/// Integrates with AreaSelectionManager to provide smart building placement
/// </summary>
public class BuildModeManager : MonoBehaviour
{
    [Header("References")]
    public AreaSelectionManager areaSelectionManager;
    public StructureManager structureManager;
    public AreaSelectionVisualizer visualizer;

    [Header("Preview Settings")]
    public bool showStructurePreview = true;
    public Material previewMaterial;
    public Color validPlacementColor = new Color(0.2f, 1f, 0.3f, 0.5f);
    public Color invalidPlacementColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    [Header("Building Settings")]
    public bool snapToGrid = true;
    public float gridSize = 1f;
    public bool checkCollisions = true;
    public LayerMask collisionCheckLayers = -1;

    private StructureData currentStructure;
    private Vector3 previewPosition;
    private GameObject previewObject;
    private bool isPlacementValid;

    // Performance: cached renderers and shared material to avoid per-frame allocations
    private Renderer[] _cachedRenderers;
    private Material _sharedPreviewMaterial;

    // Performance: cache validity result for same grid position
    private Vector3 _lastValidityCheckPos = new Vector3(float.NaN, float.NaN, float.NaN);
    private bool _lastValidityResult;

    // Performance: reusable buffer for OverlapBox to avoid allocation
    private static readonly Collider[] _overlapBuffer = new Collider[1];

    private void Start()
    {
        if (areaSelectionManager == null)
            areaSelectionManager = FindFirstObjectByType<AreaSelectionManager>();

        if (structureManager == null)
            structureManager = FindFirstObjectByType<StructureManager>();

        if (visualizer == null)
            visualizer = FindFirstObjectByType<AreaSelectionVisualizer>();
    }

    private void Update()
    {
        if (currentStructure != null && areaSelectionManager != null)
        {
            if (areaSelectionManager.CurrentMode == AreaSelectionManager.SelectionMode.Building)
            {
                UpdateBuildingPreview();
                HandleBuildingInput();
            }
        }
    }

    /// <summary>
    /// Sets the current structure to build
    /// </summary>
    public void SetStructureToBuild(StructureData structure)
    {
        currentStructure = structure;
        _lastValidityCheckPos = new Vector3(float.NaN, float.NaN, float.NaN); // Invalidate cache

        if (structure != null)
        {
            Debug.Log($"Set structure to build: {structure.name} (Size: {structure.GetSize()})");

            // Switch to building mode if not already
            if (areaSelectionManager != null && areaSelectionManager.CurrentMode != AreaSelectionManager.SelectionMode.Building)
            {
                areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.Building);
            }
        }
        else
        {
            ClearPreview();
        }
    }

    private void UpdateBuildingPreview()
    {
        if (!showStructurePreview || currentStructure == null)
        {
            ClearPreview();
            return;
        }

        // Get mouse position in world
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f))
        {
            previewPosition = hit.point;

            // Snap to grid if enabled
            if (snapToGrid)
            {
                previewPosition = SnapToGrid(previewPosition, gridSize);
            }

            // Check if placement is valid (cached — only recalculate when grid position changes)
            if (previewPosition != _lastValidityCheckPos)
            {
                _lastValidityCheckPos = previewPosition;
                _lastValidityResult = CheckPlacementValidity(previewPosition);
            }
            isPlacementValid = _lastValidityResult;

            // Update or create preview
            UpdatePreviewVisualization();

            // Update area visualizer with structure size
            if (visualizer != null)
            {
                Vector3Int size = currentStructure.GetSize();
                visualizer.AdaptSelectionForStructure(previewPosition, size);
            }
        }
    }

    private void UpdatePreviewVisualization()
    {
        if (previewObject == null && structureManager != null)
        {
            // Create preview object
            previewObject = new GameObject("StructurePreview");
            previewObject.transform.SetParent(transform);

            // Create visual representation of structure
            CreateStructurePreviewMesh(previewObject, currentStructure);

            // Cache renderers once at creation time
            _cachedRenderers = previewObject.GetComponentsInChildren<Renderer>();
        }

        if (previewObject != null)
        {
            previewObject.transform.position = previewPosition;

            // Update color based on validity using cached renderers
            Color color = isPlacementValid ? validPlacementColor : invalidPlacementColor;
            UpdatePreviewColor(color);
        }
    }

    private void CreateStructurePreviewMesh(GameObject parent, StructureData structure)
    {
        if (structure == null || structure.blocks == null) return;

        // Create ONE shared material for all preview blocks (was creating per block)
        _sharedPreviewMaterial = new Material(Shader.Find("Standard"));
        _sharedPreviewMaterial.SetFloat("_Mode", 3); // Transparent
        _sharedPreviewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _sharedPreviewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _sharedPreviewMaterial.SetInt("_ZWrite", 0);
        _sharedPreviewMaterial.DisableKeyword("_ALPHATEST_ON");
        _sharedPreviewMaterial.EnableKeyword("_ALPHABLEND_ON");
        _sharedPreviewMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        _sharedPreviewMaterial.renderQueue = 3000;

        foreach (var block in structure.blocks)
        {
            GameObject blockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blockObj.transform.SetParent(parent.transform, false);
            blockObj.transform.localPosition = block.relativePosition;
            blockObj.transform.localScale = Vector3.one * 0.95f;

            // Remove collider
            Destroy(blockObj.GetComponent<Collider>());

            // Use shared material instead of creating new per block
            Renderer renderer = blockObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _sharedPreviewMaterial;
            }
        }
    }

    private void UpdatePreviewColor(Color color)
    {
        if (_cachedRenderers == null) return;

        // Update shared material color once (affects all blocks using it)
        if (_sharedPreviewMaterial != null)
        {
            _sharedPreviewMaterial.color = color;
        }
    }

    private bool CheckPlacementValidity(Vector3 position)
    {
        if (!checkCollisions) return true;
        if (currentStructure == null) return false;

        Vector3Int size = currentStructure.GetSize();

        // Broad-phase: single AABB check for entire structure bounding box
        Vector3 boundsCenter = position + new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
        Vector3 boundsHalfExtents = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
        int broadPhaseHits = Physics.OverlapBoxNonAlloc(boundsCenter, boundsHalfExtents, _overlapBuffer, Quaternion.identity, collisionCheckLayers);
        if (broadPhaseHits == 0)
        {
            return true; // No collisions anywhere in bounding box — skip per-block checks
        }

        // Narrow-phase: check individual block positions (only if broad-phase detected something)
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    Vector3 checkPos = position + new Vector3(x, y, z);

                    // Use NonAlloc to avoid per-call heap allocation
                    int hitCount = Physics.OverlapBoxNonAlloc(checkPos, Vector3.one * 0.4f, _overlapBuffer, Quaternion.identity, collisionCheckLayers);

                    if (hitCount > 0)
                    {
                        return false; // Collision detected
                    }
                }
            }
        }

        return true;
    }

    private Vector3 SnapToGrid(Vector3 position, float gridSize)
    {
        return new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            Mathf.Round(position.y / gridSize) * gridSize,
            Mathf.Round(position.z / gridSize) * gridSize
        );
    }

    private void HandleBuildingInput()
    {
        // Confirm placement with left click
        if (Input.GetMouseButtonDown(0) && isPlacementValid)
        {
            ConfirmPlacement();
        }

        // Cancel with right click or ESC
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelBuilding();
        }

        // Rotate structure with R key
        if (Input.GetKeyDown(KeyCode.R) && previewObject != null)
        {
            previewObject.transform.Rotate(0, 90f, 0);
            _lastValidityCheckPos = new Vector3(float.NaN, float.NaN, float.NaN); // Invalidate on rotation
        }
    }

    private void ConfirmPlacement()
    {
        if (currentStructure == null || !isPlacementValid) return;

        Debug.Log($"Confirmed placement of {currentStructure.name} at {previewPosition}");

        // Create building task
        ModernUIManager.Instance?.CreateBuildingTask(previewPosition, currentStructure);

        // Optionally clear selection or keep for multiple placements
        // CancelBuilding();
    }

    private void CancelBuilding()
    {
        ClearPreview();
        currentStructure = null;

        if (areaSelectionManager != null)
        {
            areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.None);
        }
    }

    private void ClearPreview()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
        _cachedRenderers = null;

        if (_sharedPreviewMaterial != null)
        {
            Destroy(_sharedPreviewMaterial);
            _sharedPreviewMaterial = null;
        }

        _lastValidityCheckPos = new Vector3(float.NaN, float.NaN, float.NaN);
    }

    private void OnDisable()
    {
        ClearPreview();
    }
}
