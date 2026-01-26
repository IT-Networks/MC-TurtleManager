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

            // Check if placement is valid
            isPlacementValid = CheckPlacementValidity(previewPosition);

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
        }

        if (previewObject != null)
        {
            previewObject.transform.position = previewPosition;

            // Update color based on validity
            UpdatePreviewColor(isPlacementValid ? validPlacementColor : invalidPlacementColor);
        }
    }

    private void CreateStructurePreviewMesh(GameObject parent, StructureData structure)
    {
        if (structure == null || structure.blocks == null) return;

        foreach (var block in structure.blocks)
        {
            GameObject blockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blockObj.transform.SetParent(parent.transform, false);
            blockObj.transform.localPosition = block.relativePosition;
            blockObj.transform.localScale = Vector3.one * 0.95f;

            // Remove collider
            Destroy(blockObj.GetComponent<Collider>());

            // Set transparent preview material
            Renderer renderer = blockObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                renderer.material = mat;
            }
        }
    }

    private void UpdatePreviewColor(Color color)
    {
        if (previewObject == null) return;

        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.material != null)
            {
                renderer.material.color = color;
            }
        }
    }

    private bool CheckPlacementValidity(Vector3 position)
    {
        if (!checkCollisions) return true;
        if (currentStructure == null) return false;

        Vector3Int size = currentStructure.GetSize();

        // Check each block position for collisions
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    Vector3 checkPos = position + new Vector3(x, y, z);

                    // Check for collisions at this position
                    Collider[] colliders = Physics.OverlapBox(checkPos, Vector3.one * 0.4f, Quaternion.identity, collisionCheckLayers);

                    if (colliders.Length > 0)
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
    }

    private void OnDisable()
    {
        ClearPreview();
    }
}
