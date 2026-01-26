using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RTSCameraController : MonoBehaviour
{
    [Header("Bewegung")]
    public float moveSpeed = 10f;
    public float sprintMultiplier = 2f;
    public float scrollSpeed = 20f;
    public float minY = 10f;
    public float maxY = 80f;

    [Header("Randbewegtung")]
    public int edgeBoundary = 10; // Pixelbereich am Bildschirmrand für Kamerabewegung
    public bool edgeScrollEnabled = true;

    [Header("Block Selection")]
    public LayerMask chunkLayerMask = -1; // Layer auf dem sich die Chunks befinden
    public Material highlightMaterial; // Material für highlighted Blöcke
    public float raycastDistance = 1000f;
    
    [Header("UI References")]
    public Canvas infoCanvas; // Canvas für Block-Informationen
    public Text blockInfoText; // Text UI Element für Block-Info
    public Text chunkInfoText; // Text UI Element für Chunk-Info
    public Button analyzeChunkButton; // Button für Chunk-Analyse
    
    [Header("Visual Feedback")]
    public GameObject blockHighlightPrefab; // Prefab für Block-Highlight (z.B. wireframe cube)
    public Color highlightColor = Color.yellow;
    public float highlightScale = 1.02f;

    private Vector3 moveDirection;
    private TurtleWorldManager worldManager;
    private Camera cam;
    
    // Block selection state
    private Vector3? selectedBlockPosition;
    private ChunkManager selectedChunk;
    private ChunkInfo.BlockInfo selectedBlockInfo;
    private GameObject currentHighlight;
    
    // UI state
    private bool showBlockInfo = false;

    void Start()
    {
        cam = GetComponent<Camera>();
        worldManager = FindFirstObjectByType<TurtleWorldManager>();
        
        if (worldManager == null)
        {
            Debug.LogError("TurtleWorldManager not found! Block selection will not work.");
        }
        
        SetupUI();
    }

    void SetupUI()
    {
        // Setup UI event listeners
        if (analyzeChunkButton != null)
        {
            analyzeChunkButton.onClick.AddListener(AnalyzeSelectedChunk);
        }
        
        // Initially hide info UI
        if (infoCanvas != null)
        {
            infoCanvas.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        HandleMovementInput();
        HandleEdgeScrolling();
        HandleZoom();
        HandleBlockSelection();
        HandleUIToggle();

        Vector3 pos = transform.position;
        pos += moveDirection * moveSpeed * Time.deltaTime;

        // Höhenlimit klammern
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }

    void HandleMovementInput()
    {
        moveDirection = Vector3.zero;

        // WASD oder Pfeiltasten
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = new Vector3(transform.right.x, 0, transform.right.z).normalized;

        moveDirection += right * h + forward * v;

        // Sprint mit Shift
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            moveDirection *= sprintMultiplier;
        }
    }

    void HandleEdgeScrolling()
    {
        if (!edgeScrollEnabled) return;

        Vector3 mousePos = Input.mousePosition;

        if (mousePos.x >= 0 && mousePos.x < edgeBoundary)
        {
            moveDirection += -transform.right;
        }
        else if (mousePos.x <= Screen.width && mousePos.x > Screen.width - edgeBoundary)
        {
            moveDirection += transform.right;
        }

        if (mousePos.y >= 0 && mousePos.y < edgeBoundary)
        {
            moveDirection += -transform.forward;
        }
        else if (mousePos.y <= Screen.height && mousePos.y > Screen.height - edgeBoundary)
        {
            moveDirection += transform.forward;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        Vector3 pos = transform.position;

        pos.y -= scroll * scrollSpeed;
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        transform.position = pos;
    }

    void HandleBlockSelection()
    {
        // Block-Selektion mit linker Maustaste
        if (Input.GetMouseButtonDown(0))
        {
            SelectBlockAtMousePosition();
        }
        
        // Block-Info anzeigen mit rechter Maustaste
        if (Input.GetMouseButtonDown(1))
        {
            ShowBlockInfoAtMousePosition();
        }
        
        // Selektion aufheben mit ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            DeselectBlock();
        }
    }
    
    void HandleUIToggle()
    {
        // Toggle Block-Info UI mit Tab
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            showBlockInfo = !showBlockInfo;
            if (infoCanvas != null)
            {
                infoCanvas.gameObject.SetActive(showBlockInfo);
            }
        }
    }

    void SelectBlockAtMousePosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, chunkLayerMask))
        {
            Vector3 hitPoint = hit.point;
            Vector3 blockPosition = GetBlockPositionFromHit(hit);
            
            // Find the chunk containing this block
            ChunkManager chunk = worldManager?.GetChunkContaining(blockPosition);
            if (chunk != null)
            {
                ChunkInfo chunkInfo = chunk.GetChunkInfo();
                if (chunkInfo != null)
                {
                    var blockInfo = chunkInfo.GetBlockAt(blockPosition);
                    if (blockInfo != null)
                    {
                        SelectBlock(blockPosition, chunk, blockInfo);
                        Debug.Log($"Selected block: {blockInfo.blockType} at {blockPosition}");
                    }
                }
            }
        }
    }
    
    void ShowBlockInfoAtMousePosition()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, chunkLayerMask))
        {
            Vector3 blockPosition = GetBlockPositionFromHit(hit);
            
            ChunkManager chunk = worldManager?.GetChunkContaining(blockPosition);
            if (chunk != null)
            {
                ChunkInfo chunkInfo = chunk.GetChunkInfo();
                if (chunkInfo != null)
                {
                    var blockInfo = chunkInfo.GetBlockAt(blockPosition);
                    if (blockInfo != null)
                    {
                        ShowQuickBlockInfo(blockInfo, chunk);
                    }
                }
            }
        }
    }
    
    Vector3 GetBlockPositionFromHit(RaycastHit hit)
    {
        // Konvertiere Hit-Point zu Block-Koordinaten
        Vector3 point = hit.point - hit.normal * 0.1f; // Leicht zurück vom Hit-Point
        return new Vector3(
            Mathf.Floor(point.x),
            Mathf.Floor(point.y),
            Mathf.Floor(point.z)
        );
    }

    void SelectBlock(Vector3 blockPosition, ChunkManager chunk, ChunkInfo.BlockInfo blockInfo)
    {
        // Deselect previous block
        DeselectBlock();
        
        // Set new selection
        selectedBlockPosition = blockPosition;
        selectedChunk = chunk;
        selectedBlockInfo = blockInfo;
        
        // Create visual highlight
        CreateBlockHighlight(blockPosition);
        
        // Update UI
        UpdateBlockInfoUI();
    }

    void DeselectBlock()
    {
        selectedBlockPosition = null;
        selectedChunk = null;
        selectedBlockInfo = null;
        
        // Remove highlight
        if (currentHighlight != null)
        {
            DestroyImmediate(currentHighlight);
            currentHighlight = null;
        }
        
        // Clear UI
        if (blockInfoText != null)
        {
            blockInfoText.text = "No block selected";
        }
    }
    
    void CreateBlockHighlight(Vector3 blockPosition)
    {
        if (blockHighlightPrefab != null)
        {
            currentHighlight = Instantiate(blockHighlightPrefab, blockPosition + Vector3.one * 0.5f, Quaternion.identity);
            currentHighlight.transform.localScale = Vector3.one * highlightScale;
            
            // Set highlight color if it has a renderer
            Renderer renderer = currentHighlight.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (highlightMaterial != null)
                {
                    renderer.material = highlightMaterial;
                }
                renderer.material.color = highlightColor;
            }
        }
        else
        {
            // Fallback: Create simple wireframe cube
            CreateSimpleHighlight(blockPosition);
        }
    }
    
    void CreateSimpleHighlight(Vector3 blockPosition)
    {
        currentHighlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        currentHighlight.transform.position = blockPosition + Vector3.one * 0.5f;
        currentHighlight.transform.localScale = Vector3.one * highlightScale;
        currentHighlight.name = "BlockHighlight";
        
        // Remove collider and make it wireframe
        DestroyImmediate(currentHighlight.GetComponent<Collider>());
        
        Renderer renderer = currentHighlight.GetComponent<Renderer>();
        if (highlightMaterial != null)
        {
            renderer.material = highlightMaterial;
        }
        else
        {
            // Create simple highlight material
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = highlightColor;
            mat.SetFloat("_Metallic", 0.5f);
            mat.SetFloat("_Glossiness", 0.8f);
            renderer.material = mat;
        }
    }

    void UpdateBlockInfoUI()
    {
        if (selectedBlockInfo != null && blockInfoText != null)
        {
            string info = $"Block Type: {selectedBlockInfo.blockType}\n";
            info += $"Position: {selectedBlockInfo.worldPosition}\n";
            info += $"Local Position: {selectedBlockInfo.localPosition}";
            blockInfoText.text = info;
        }
        
        if (selectedChunk != null && chunkInfoText != null)
        {
            ChunkInfo chunkInfo = selectedChunk.GetChunkInfo();
            if (chunkInfo != null)
            {
                string info = $"Chunk Coord: {selectedChunk.coord}\n";
                info += $"Total Blocks: {chunkInfo.BlockCount}\n";
                info += $"Vertices: {selectedChunk.VertexCount}\n";
                info += $"Submeshes: {selectedChunk.SubmeshCount}";
                chunkInfoText.text = info;
            }
        }
    }
    
    void ShowQuickBlockInfo(ChunkInfo.BlockInfo blockInfo, ChunkManager chunk)
    {
        // Show temporary info popup or log
        Debug.Log($"Block Info: {blockInfo.blockType} at {blockInfo.worldPosition}");
        
        // You could create a temporary UI popup here
        StartCoroutine(ShowTemporaryPopup($"{blockInfo.blockType}\n{blockInfo.worldPosition}"));
    }
    
    System.Collections.IEnumerator ShowTemporaryPopup(string text)
    {
        // Create temporary text popup at mouse position
        GameObject popup = new GameObject("TempPopup");
        popup.transform.SetParent(infoCanvas?.transform);
        
        Text popupText = popup.AddComponent<Text>();
        popupText.text = text;
        popupText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        popupText.fontSize = 14;
        popupText.color = Color.white;
        
        RectTransform rect = popup.GetComponent<RectTransform>();
        Vector2 screenPos = Input.mousePosition;
        rect.position = screenPos;
        
        yield return new WaitForSeconds(2f);
        
        if (popup != null)
            DestroyImmediate(popup);
    }

    // Button callback for chunk analysis
    public void AnalyzeSelectedChunk()
    {
        if (selectedChunk == null) return;
        
        ChunkInfo chunkInfo = selectedChunk.GetChunkInfo();
        if (chunkInfo == null) return;
        
        Debug.Log("=== Chunk Analysis ===");
        Debug.Log($"Chunk Coordinates: {selectedChunk.coord}");
        Debug.Log($"Total Blocks: {chunkInfo.BlockCount}");
        
        // Get block type statistics
        var stats = chunkInfo.GetBlockTypeStatistics();
        Debug.Log("Block Type Statistics:");
        foreach (var kvp in stats)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value} blocks");
        }
        
        // Find blocks near selected position
        if (selectedBlockPosition.HasValue)
        {
            var nearbyBlocks = chunkInfo.GetBlocksInRadius(selectedBlockPosition.Value, 5f);
            Debug.Log($"Blocks within 5 units of selected position: {nearbyBlocks.Count}");
        }
    }
    
    // Public methods for external access
    public Vector3? GetSelectedBlockPosition() => selectedBlockPosition;
    public ChunkManager GetSelectedChunk() => selectedChunk;
    public ChunkInfo.BlockInfo GetSelectedBlockInfo() => selectedBlockInfo;
    
    // Method to programmatically select a block
    public void SelectBlock(Vector3 worldPosition)
    {
        ChunkManager chunk = worldManager?.GetChunkContaining(worldPosition);
        if (chunk != null)
        {
            ChunkInfo chunkInfo = chunk.GetChunkInfo();
            if (chunkInfo != null)
            {
                var blockInfo = chunkInfo.GetBlockAt(worldPosition);
                if (blockInfo != null)
                {
                    SelectBlock(worldPosition, chunk, blockInfo);
                }
            }
        }
    }

    /// <summary>
    /// Jumps the camera to a specific world position
    /// </summary>
    public void JumpToPosition(Vector3 targetPosition)
    {
        // Maintain current camera height
        Vector3 newPosition = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
        transform.position = newPosition;

        Debug.Log($"Camera jumped to position: {newPosition}");
    }

    /// <summary>
    /// Smoothly moves camera to target position
    /// </summary>
    public void SmoothMoveToPosition(Vector3 targetPosition, float duration = 1f)
    {
        StartCoroutine(SmoothMoveCoroutine(targetPosition, duration));
    }

    private System.Collections.IEnumerator SmoothMoveCoroutine(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth interpolation
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPosition, endPosition, t);
            yield return null;
        }

        transform.position = endPosition;
    }

    void OnDestroy()
    {
        // Cleanup
        if (currentHighlight != null)
        {
            DestroyImmediate(currentHighlight);
        }
    }
}