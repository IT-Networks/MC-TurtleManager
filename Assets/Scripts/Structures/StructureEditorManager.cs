using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Main manager for the structure editor scene
/// Handles block placement, editing, and saving structures
/// </summary>
public class StructureEditorManager : MonoBehaviour
{
    [Header("References")]
    public Camera editorCamera;
    public Transform gridContainer;
    public StructureManager structureManager;

    [Header("UI References")]
    public BlockPaletteUI blockPalette;
    public TMP_InputField structureNameInput;
    public TMP_InputField structureDescriptionInput;
    public TMP_Dropdown categoryDropdown;
    public TextMeshProUGUI blockCountText;
    public TextMeshProUGUI dimensionsText;
    public Button saveButton;
    public Button newButton;
    public Button loadButton;
    public Button clearButton;
    public Button testBuildButton;

    [Header("Grid Settings")]
    public bool showGrid = true;
    public int gridSize = 16;
    public float gridSpacing = 1f;
    public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    public Material gridMaterial;

    [Header("Block Settings")]
    public Material blockPreviewMaterial;
    public Color placementPreviewColor = new Color(0.2f, 1f, 0.3f, 0.5f);
    public Color deletePreviewColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    [Header("Camera Controls")]
    public float cameraRotateSpeed = 100f;
    public float cameraZoomSpeed = 10f;
    public float cameraPanSpeed = 5f;
    public float minZoomDistance = 5f;
    public float maxZoomDistance = 50f;

    private StructureData currentStructure;
    private string currentSelectedBlockType = "minecraft:stone";
    private bool isPlacementMode = true; // true = place, false = delete

    private Dictionary<Vector3Int, GameObject> placedBlockObjects = new Dictionary<Vector3Int, GameObject>();
    private GameObject previewBlockObject;
    private Vector3Int? currentHoverPosition;

    private GameObject gridObject;
    private bool isDragging = false;
    private Vector3 lastMousePosition;

    private void Start()
    {
        InitializeEditor();
        SetupUI();
        CreateNewStructure();
        UpdateGrid();
    }

    private void InitializeEditor()
    {
        if (editorCamera == null)
            editorCamera = Camera.main;

        if (structureManager == null)
            structureManager = StructureManager.Instance;

        if (gridContainer == null)
        {
            gridContainer = new GameObject("GridContainer").transform;
        }

        // Setup preview block
        CreatePreviewBlock();
    }

    private void SetupUI()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveCurrentStructure);

        if (newButton != null)
            newButton.onClick.AddListener(CreateNewStructure);

        if (clearButton != null)
            clearButton.onClick.AddListener(ClearAllBlocks);

        if (loadButton != null)
            loadButton.onClick.AddListener(ShowLoadDialog);

        if (testBuildButton != null)
            testBuildButton.onClick.AddListener(TestBuildStructure);

        if (blockPalette != null)
        {
            blockPalette.OnBlockSelected += OnBlockTypeSelected;
        }

        // Setup category dropdown
        if (categoryDropdown != null)
        {
            categoryDropdown.ClearOptions();
            categoryDropdown.AddOptions(new List<string> { "Basics", "Buildings", "Decorations", "Custom" });
        }
    }

    private void Update()
    {
        HandleCameraControls();
        HandleBlockPlacement();
        UpdatePreview();
        UpdateUI();
    }

    private void HandleCameraControls()
    {
        if (editorCamera == null) return;

        // Rotate camera with middle mouse button
        if (Input.GetMouseButton(2))
        {
            float rotX = Input.GetAxis("Mouse X") * cameraRotateSpeed * Time.deltaTime;
            float rotY = -Input.GetAxis("Mouse Y") * cameraRotateSpeed * Time.deltaTime;

            editorCamera.transform.RotateAround(Vector3.zero, Vector3.up, rotX);
            editorCamera.transform.RotateAround(Vector3.zero, editorCamera.transform.right, rotY);
            editorCamera.transform.LookAt(Vector3.zero);
        }

        // Zoom with scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            Vector3 direction = (editorCamera.transform.position - Vector3.zero).normalized;
            float currentDistance = Vector3.Distance(editorCamera.transform.position, Vector3.zero);
            float newDistance = Mathf.Clamp(currentDistance - scroll * cameraZoomSpeed, minZoomDistance, maxZoomDistance);

            editorCamera.transform.position = Vector3.zero + direction * newDistance;
        }

        // Pan with Shift + Middle mouse
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButton(2))
        {
            Vector3 move = new Vector3(
                -Input.GetAxis("Mouse X") * cameraPanSpeed * Time.deltaTime,
                -Input.GetAxis("Mouse Y") * cameraPanSpeed * Time.deltaTime,
                0
            );

            editorCamera.transform.Translate(move, Space.Self);
        }
    }

    private void HandleBlockPlacement()
    {
        // Toggle between place and delete mode
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
        {
            isPlacementMode = false;
        }
        else if (Input.GetKeyUp(KeyCode.Delete) || Input.GetKeyUp(KeyCode.Backspace))
        {
            isPlacementMode = true;
        }

        // Get grid position under mouse
        Vector3Int? gridPos = GetGridPositionUnderMouse();

        if (gridPos.HasValue)
        {
            currentHoverPosition = gridPos;

            // Place block with left click
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                if (isPlacementMode)
                {
                    PlaceBlock(gridPos.Value, currentSelectedBlockType);
                }
                else
                {
                    RemoveBlock(gridPos.Value);
                }
            }

            // Continue drag placement
            if (Input.GetMouseButton(0) && isDragging)
            {
                if (isPlacementMode)
                {
                    PlaceBlock(gridPos.Value, currentSelectedBlockType);
                }
                else
                {
                    RemoveBlock(gridPos.Value);
                }
            }

            // Quick delete with right click
            if (Input.GetMouseButton(1))
            {
                RemoveBlock(gridPos.Value);
            }
        }
        else
        {
            currentHoverPosition = null;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private Vector3Int? GetGridPositionUnderMouse()
    {
        Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);

        // IMPROVED: First try raycasting against existing blocks for better stacking
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 1000f))
        {
            // Use hit normal to determine which face was hit
            // Place block adjacent to the hit face in placement mode
            // For deletion mode, use the hit block's position
            Vector3 point;

            if (isPlacementMode)
            {
                // Place on the surface the mouse is pointing at
                point = hit.point + hit.normal * (gridSpacing * 0.5f);
            }
            else
            {
                // Delete the block we're pointing at
                point = hit.point - hit.normal * (gridSpacing * 0.5f);
            }

            int x = Mathf.RoundToInt(point.x / gridSpacing);
            int y = Mathf.RoundToInt(point.y / gridSpacing);
            int z = Mathf.RoundToInt(point.z / gridSpacing);

            if (Mathf.Abs(x) <= gridSize && Mathf.Abs(y) <= gridSize && Mathf.Abs(z) <= gridSize)
            {
                return new Vector3Int(x, y, z);
            }
        }

        // Fallback: Raycast against ground plane (Y=0) only when no blocks hit
        Plane gridPlane = new Plane(Vector3.up, Vector3.zero);
        float distance;

        if (gridPlane.Raycast(ray, out distance))
        {
            Vector3 point = ray.GetPoint(distance);

            // Snap to grid
            int x = Mathf.RoundToInt(point.x / gridSpacing);
            int y = 0; // Always place at Y=0 when using ground plane
            int z = Mathf.RoundToInt(point.z / gridSpacing);

            // Clamp to grid bounds
            if (Mathf.Abs(x) <= gridSize && Mathf.Abs(z) <= gridSize)
            {
                return new Vector3Int(x, y, z);
            }
        }

        return null;
    }

    private void UpdatePreview()
    {
        if (previewBlockObject == null) return;

        if (currentHoverPosition.HasValue && !isDragging)
        {
            Vector3 worldPos = new Vector3(
                currentHoverPosition.Value.x * gridSpacing,
                currentHoverPosition.Value.y * gridSpacing,
                currentHoverPosition.Value.z * gridSpacing
            );

            previewBlockObject.transform.position = worldPos;
            previewBlockObject.SetActive(true);

            // Set color based on mode
            Renderer renderer = previewBlockObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = isPlacementMode ? placementPreviewColor : deletePreviewColor;
            }
        }
        else
        {
            previewBlockObject.SetActive(false);
        }
    }

    private void PlaceBlock(Vector3Int gridPosition, string blockType)
    {
        if (currentStructure == null) return;

        // Add to structure data
        currentStructure.AddBlock(gridPosition, blockType);

        // Create or update visual block
        if (!placedBlockObjects.ContainsKey(gridPosition))
        {
            Vector3 worldPos = new Vector3(
                gridPosition.x * gridSpacing,
                gridPosition.y * gridSpacing,
                gridPosition.z * gridSpacing
            );

            GameObject blockObj = CreateBlockObject(worldPos, blockType);
            placedBlockObjects[gridPosition] = blockObj;
        }
    }

    private void RemoveBlock(Vector3Int gridPosition)
    {
        if (currentStructure == null) return;

        // Remove from structure data
        currentStructure.RemoveBlockAt(gridPosition);

        // Remove visual block
        if (placedBlockObjects.TryGetValue(gridPosition, out GameObject blockObj))
        {
            Destroy(blockObj);
            placedBlockObjects.Remove(gridPosition);
        }
    }

    private GameObject CreateBlockObject(Vector3 position, string blockType)
    {
        // Check if this is a special block type (cross-shaped plants, chains, etc.)
        BlockRenderType renderType = GetBlockRenderType(blockType);

        GameObject blockObj;

        switch (renderType)
        {
            case BlockRenderType.CrossPlant:
                blockObj = CreateCrossPlantMesh(position, blockType);
                break;

            case BlockRenderType.Chain:
                blockObj = CreateChainMesh(position, blockType);
                break;

            default:
                blockObj = CreateStandardCubeMesh(position, blockType);
                break;
        }

        blockObj.transform.SetParent(gridContainer);
        return blockObj;
    }

    private GameObject CreateStandardCubeMesh(Vector3 position, string blockType)
    {
        GameObject blockObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blockObj.transform.position = position;
        blockObj.transform.localScale = Vector3.one * gridSpacing * 0.98f;

        // Load actual block textures from TurtleWorldManager
        Renderer renderer = blockObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            TurtleWorldManager worldManager = FindFirstObjectByType<TurtleWorldManager>();
            if (worldManager != null)
            {
                // Use the world manager's material loading system
                Material blockMaterial = worldManager.GetMaterialForBlock(blockType);
                if (blockMaterial != null)
                {
                    renderer.material = blockMaterial;
                }
                else
                {
                    // Fallback to simple color
                    renderer.material = new Material(Shader.Find("Standard"));
                    renderer.material.color = GetBlockColor(blockType);
                }
            }
            else
            {
                // Fallback when no world manager
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = GetBlockColor(blockType);
            }
        }

        // Ensure the block has a MeshCollider for raycasting
        MeshCollider collider = blockObj.GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.convex = false;
        }

        return blockObj;
    }

    private void CreatePreviewBlock()
    {
        previewBlockObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewBlockObject.name = "PreviewBlock";
        previewBlockObject.transform.localScale = Vector3.one * gridSpacing * 1.02f;

        // Make transparent
        Renderer renderer = previewBlockObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            renderer.material = mat;
        }

        // Remove collider
        Collider collider = previewBlockObject.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        previewBlockObject.SetActive(false);
    }

    /// <summary>
    /// Block render types for different block geometries
    /// </summary>
    private enum BlockRenderType
    {
        Standard,      // Normal cube blocks
        CrossPlant,    // Cross-shaped plants (flowers, grass, saplings)
        Chain,         // Chains (vertical or horizontal)
        TintedCross    // Tinted cross (colored grass, ferns)
    }

    /// <summary>
    /// Determines the render type based on block name
    /// Based on Minecraft 1.21 block models
    /// </summary>
    private BlockRenderType GetBlockRenderType(string blockType)
    {
        string lower = blockType.ToLowerInvariant();

        // Cross-shaped plants (use block/cross.json model)
        // Common flowers
        if (lower.Contains("poppy") || lower.Contains("dandelion") ||
            lower.Contains("orchid") || lower.Contains("allium") ||
            lower.Contains("tulip") || lower.Contains("daisy") ||
            lower.Contains("cornflower") || lower.Contains("lily_of_the_valley") ||
            lower.Contains("wither_rose") || lower.Contains("torchflower"))
        {
            return BlockRenderType.CrossPlant;
        }

        // Saplings
        if (lower.Contains("sapling"))
        {
            return BlockRenderType.CrossPlant;
        }

        // Grass and ferns (tinted cross)
        if ((lower.Contains("tall_grass") || lower.Contains("fern") ||
             lower.Contains("dead_bush")) && !lower.Contains("block"))
        {
            return BlockRenderType.TintedCross;
        }

        // Wheat, carrots, potatoes, etc. (crops)
        if (lower.Contains("wheat") || lower.Contains("carrots") ||
            lower.Contains("potatoes") || lower.Contains("beetroots") ||
            lower.Contains("nether_wart"))
        {
            return BlockRenderType.CrossPlant;
        }

        // Mushrooms
        if ((lower.Contains("mushroom") || lower.Contains("fungus")) &&
            !lower.Contains("block") && !lower.Contains("stem"))
        {
            return BlockRenderType.CrossPlant;
        }

        // Chains from Create mod or vanilla
        if (lower.Contains("chain"))
        {
            return BlockRenderType.Chain;
        }

        return BlockRenderType.Standard;
    }

    /// <summary>
    /// Creates a cross-shaped mesh for plants (flowers, grass, saplings)
    /// Based on Minecraft's block/cross.json model
    /// </summary>
    private GameObject CreateCrossPlantMesh(Vector3 position, string blockType)
    {
        GameObject plantObj = new GameObject($"Plant_{blockType}");
        plantObj.transform.position = position;

        // Create mesh for cross-shaped plant
        MeshFilter meshFilter = plantObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = plantObj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "CrossPlantMesh";

        // Cross consists of 2 perpendicular quads
        // Each quad goes from corner to corner diagonally
        float size = gridSpacing * 0.9f;
        float halfSize = size * 0.5f;
        float sqrt2 = Mathf.Sqrt(2f);
        float diagonalHalf = halfSize * sqrt2;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // First diagonal (from -X-Z to +X+Z) - 2 sided
        // Front face
        vertices.Add(new Vector3(-diagonalHalf, 0, -diagonalHalf));  // 0
        vertices.Add(new Vector3(diagonalHalf, 0, diagonalHalf));    // 1
        vertices.Add(new Vector3(diagonalHalf, size, diagonalHalf)); // 2
        vertices.Add(new Vector3(-diagonalHalf, size, -diagonalHalf)); // 3

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        triangles.Add(0); triangles.Add(2); triangles.Add(1);
        triangles.Add(0); triangles.Add(3); triangles.Add(2);

        // Back face (reversed winding)
        vertices.Add(new Vector3(-diagonalHalf, 0, -diagonalHalf));  // 4
        vertices.Add(new Vector3(diagonalHalf, 0, diagonalHalf));    // 5
        vertices.Add(new Vector3(diagonalHalf, size, diagonalHalf)); // 6
        vertices.Add(new Vector3(-diagonalHalf, size, -diagonalHalf)); // 7

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        triangles.Add(4); triangles.Add(5); triangles.Add(6);
        triangles.Add(4); triangles.Add(6); triangles.Add(7);

        // Second diagonal (from +X-Z to -X+Z) - 2 sided
        // Front face
        vertices.Add(new Vector3(diagonalHalf, 0, -diagonalHalf));   // 8
        vertices.Add(new Vector3(-diagonalHalf, 0, diagonalHalf));   // 9
        vertices.Add(new Vector3(-diagonalHalf, size, diagonalHalf)); // 10
        vertices.Add(new Vector3(diagonalHalf, size, -diagonalHalf)); // 11

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        triangles.Add(8); triangles.Add(10); triangles.Add(9);
        triangles.Add(8); triangles.Add(11); triangles.Add(10);

        // Back face (reversed winding)
        vertices.Add(new Vector3(diagonalHalf, 0, -diagonalHalf));   // 12
        vertices.Add(new Vector3(-diagonalHalf, 0, diagonalHalf));   // 13
        vertices.Add(new Vector3(-diagonalHalf, size, diagonalHalf)); // 14
        vertices.Add(new Vector3(diagonalHalf, size, -diagonalHalf)); // 15

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        triangles.Add(12); triangles.Add(13); triangles.Add(14);
        triangles.Add(12); triangles.Add(14); triangles.Add(15);

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        // Apply material with texture
        TurtleWorldManager worldManager = FindFirstObjectByType<TurtleWorldManager>();
        if (worldManager != null)
        {
            Material blockMaterial = worldManager.GetMaterialForBlock(blockType);
            if (blockMaterial != null)
            {
                // Create transparent version for plants
                Material plantMaterial = new Material(blockMaterial);
                plantMaterial.SetFloat("_Mode", 1); // Cutout mode for transparency
                plantMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                plantMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                plantMaterial.SetInt("_ZWrite", 1);
                plantMaterial.EnableKeyword("_ALPHATEST_ON");
                plantMaterial.DisableKeyword("_ALPHABLEND_ON");
                plantMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                plantMaterial.renderQueue = 2450;

                meshRenderer.material = plantMaterial;
            }
            else
            {
                meshRenderer.material = CreateFallbackPlantMaterial(blockType);
            }
        }
        else
        {
            meshRenderer.material = CreateFallbackPlantMaterial(blockType);
        }

        // Add box collider for raycasting (smaller than visual)
        BoxCollider collider = plantObj.AddComponent<BoxCollider>();
        collider.size = new Vector3(size * 0.5f, size, size * 0.5f);
        collider.center = new Vector3(0, size * 0.5f, 0);

        return plantObj;
    }

    /// <summary>
    /// Creates a chain mesh (simplified vertical chain)
    /// </summary>
    private GameObject CreateChainMesh(Vector3 position, string blockType)
    {
        // For now, use a thin vertical cylinder for chains
        GameObject chainObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chainObj.transform.position = position;
        chainObj.transform.localScale = new Vector3(0.1f, gridSpacing * 0.5f, 0.1f);

        Renderer renderer = chainObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material chainMaterial = new Material(Shader.Find("Standard"));
            chainMaterial.color = new Color(0.3f, 0.3f, 0.3f); // Dark gray for chains
            chainMaterial.SetFloat("_Metallic", 0.8f);
            renderer.material = chainMaterial;
        }

        return chainObj;
    }

    /// <summary>
    /// Creates a fallback plant material when textures are not available
    /// </summary>
    private Material CreateFallbackPlantMaterial(string blockType)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = GetPlantColor(blockType);

        // Enable alpha cutout for transparency
        mat.SetFloat("_Mode", 1);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 2450;

        return mat;
    }

    /// <summary>
    /// Gets appropriate color for plant types
    /// </summary>
    private Color GetPlantColor(string blockType)
    {
        string lower = blockType.ToLowerInvariant();

        if (lower.Contains("poppy")) return new Color(0.8f, 0.1f, 0.1f);
        if (lower.Contains("dandelion")) return new Color(1f, 0.9f, 0.2f);
        if (lower.Contains("orchid")) return new Color(0.6f, 0.4f, 0.8f);
        if (lower.Contains("allium")) return new Color(0.7f, 0.3f, 0.7f);
        if (lower.Contains("tulip")) return new Color(0.9f, 0.5f, 0.4f);
        if (lower.Contains("grass") || lower.Contains("fern")) return new Color(0.3f, 0.7f, 0.3f);
        if (lower.Contains("sapling")) return new Color(0.4f, 0.6f, 0.2f);
        if (lower.Contains("mushroom") && lower.Contains("red")) return new Color(0.8f, 0.2f, 0.2f);
        if (lower.Contains("mushroom") && lower.Contains("brown")) return new Color(0.6f, 0.4f, 0.3f);

        return new Color(0.4f, 0.7f, 0.3f); // Default green for plants
    }

    private Color GetBlockColor(string blockType)
    {
        // Simple color mapping for common blocks
        if (blockType.Contains("stone")) return new Color(0.5f, 0.5f, 0.5f);
        if (blockType.Contains("dirt")) return new Color(0.6f, 0.4f, 0.2f);
        if (blockType.Contains("grass") && blockType.Contains("block")) return new Color(0.3f, 0.7f, 0.3f);
        if (blockType.Contains("wood")) return new Color(0.6f, 0.4f, 0.2f);
        if (blockType.Contains("cobblestone")) return new Color(0.4f, 0.4f, 0.4f);
        if (blockType.Contains("planks")) return new Color(0.7f, 0.5f, 0.3f);
        if (blockType.Contains("glass")) return new Color(0.8f, 0.9f, 1f, 0.5f);
        if (blockType.Contains("brick")) return new Color(0.7f, 0.3f, 0.2f);

        return Color.white;
    }

    private void UpdateGrid()
    {
        if (gridObject != null)
            Destroy(gridObject);

        if (!showGrid) return;

        gridObject = new GameObject("EditorGrid");
        gridObject.transform.SetParent(gridContainer);

        // Create grid lines
        for (int x = -gridSize; x <= gridSize; x++)
        {
            CreateGridLine(
                new Vector3(x * gridSpacing, 0, -gridSize * gridSpacing),
                new Vector3(x * gridSpacing, 0, gridSize * gridSpacing)
            );
        }

        for (int z = -gridSize; z <= gridSize; z++)
        {
            CreateGridLine(
                new Vector3(-gridSize * gridSpacing, 0, z * gridSpacing),
                new Vector3(gridSize * gridSpacing, 0, z * gridSpacing)
            );
        }
    }

    private void CreateGridLine(Vector3 start, Vector3 end)
    {
        GameObject lineObj = new GameObject("GridLine");
        lineObj.transform.SetParent(gridObject.transform);

        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.startWidth = 0.02f;
        line.endWidth = 0.02f;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = gridColor;
        line.material = mat;
    }

    private void UpdateUI()
    {
        if (currentStructure == null) return;

        if (blockCountText != null)
        {
            blockCountText.text = $"Blocks: {currentStructure.blockCount}";
        }

        if (dimensionsText != null)
        {
            Vector3Int size = currentStructure.GetSize();
            dimensionsText.text = $"Size: {size.x} x {size.y} x {size.z}";
        }
    }

    public void CreateNewStructure()
    {
        ClearAllBlocks();

        currentStructure = new StructureData("New Structure");

        if (structureNameInput != null)
            structureNameInput.text = currentStructure.name;

        if (structureDescriptionInput != null)
            structureDescriptionInput.text = currentStructure.description;

        Debug.Log("Created new structure");
    }

    public void SaveCurrentStructure()
    {
        if (currentStructure == null || currentStructure.blockCount == 0)
        {
            Debug.LogWarning("Cannot save empty structure");
            return;
        }

        // Update structure data from UI
        if (structureNameInput != null)
            currentStructure.name = structureNameInput.text;

        if (structureDescriptionInput != null)
            currentStructure.description = structureDescriptionInput.text;

        if (categoryDropdown != null)
            currentStructure.category = categoryDropdown.options[categoryDropdown.value].text;

        // Save
        if (structureManager != null)
        {
            structureManager.SaveStructure(currentStructure);
            Debug.Log($"Saved structure: {currentStructure.name}");
        }
    }

    public void ClearAllBlocks()
    {
        foreach (var blockObj in placedBlockObjects.Values)
        {
            if (blockObj != null)
                Destroy(blockObj);
        }

        placedBlockObjects.Clear();

        if (currentStructure != null)
            currentStructure.Clear();
    }

    public void ShowLoadDialog()
    {
        // TODO: Implement load dialog
        Debug.Log("Load dialog not implemented yet");
    }

    public void TestBuildStructure()
    {
        if (currentStructure == null || currentStructure.blockCount == 0)
        {
            Debug.LogWarning("No structure to test");
            return;
        }

        // TODO: Switch to main scene and test building
        Debug.Log($"Testing structure: {currentStructure.name}");
    }

    private void OnBlockTypeSelected(string blockType)
    {
        currentSelectedBlockType = blockType;
        Debug.Log($"Selected block type: {blockType}");
    }

    private void OnDestroy()
    {
        if (blockPalette != null)
        {
            blockPalette.OnBlockSelected -= OnBlockTypeSelected;
        }
    }
}
