using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Enhanced area selection manager with persistent work area visualization
/// </summary>
public class AreaSelectionManager : MonoBehaviour
{
    [Header("Visual Settings")]
    public Material selectionMaterial;
    public Color miningAreaColor = Color.red;
    public Color buildingAreaColor = Color.green;
    public Color validBlockColor = Color.blue;
    public Color invalidBlockColor = Color.gray;
    public float gizmoAlpha = 0.3f;
    
    [Header("Work Area Visualization")]
    public Material workAreaMaterial;
    public Color workInProgressColor = new Color(1f, 0.5f, 0f, 0.5f); // Orange
    public Color completedBlockColor = new Color(0f, 1f, 0f, 0.3f); // Green
    public Color currentBlockColor = new Color(1f, 1f, 0f, 0.8f); // Yellow
    public float workAreaAlpha = 0.4f;
    public bool showWorkProgress = true;
    public bool animateCurrentBlock = true;
    public float pulseSpeed = 2f;
    public float pulseAmount = 0.2f;
    
    [Header("Selection Settings")]
    public LayerMask selectionLayerMask = -1;
    public float raycastDistance = 1000f;
    public bool showBlockValidation = true;
    public bool previewOptimization = true;
    
    [Header("References - Updated for New Architecture")]
    public RTSCameraController cameraController;
    public TurtleMainController turtleMainController;
    public BlockWorldPathfinder pathfinder;
    
    // Selection state
    private Vector3? selectionStart;
    private Vector3? selectionEnd;
    private SelectionMode currentMode = SelectionMode.None;
    private List<Vector3> selectedBlocks = new List<Vector3>();
    private List<Vector3> validBlocks = new List<Vector3>();
    private List<Vector3> invalidBlocks = new List<Vector3>();
    private List<Vector3> optimizedOrder = new List<Vector3>();
    
    // Work area state
    private WorkAreaVisualization activeWorkArea;
    private List<Vector3> workingBlocks = new List<Vector3>();
    private HashSet<Vector3> completedBlocks = new HashSet<Vector3>();
    private Vector3? currentWorkingBlock;
    
    // Visual feedback
    private GameObject selectionVisualization;
    private List<GameObject> blockGizmos = new List<GameObject>();
    private List<GameObject> validationGizmos = new List<GameObject>();
    private List<GameObject> optimizationGizmos = new List<GameObject>();
    
    // Statistics
    private SelectionStats currentStats;
    
    public enum SelectionMode
    {
        None,
        Mining,
        Building
    }

    [System.Serializable]
    public class SelectionStats
    {
        public int totalSelected;
        public int validBlocks;
        public int invalidBlocks;
        public float estimatedDistance;
        public float optimizationSavings;
        public string summary;
        public int blocksCompleted;
        public float progressPercentage;
    }
    
    [System.Serializable]
    public class WorkAreaVisualization
    {
        public GameObject container;
        public Dictionary<Vector3, GameObject> blockVisuals = new Dictionary<Vector3, GameObject>();
        public LineRenderer progressLine;
        public TextMesh progressText;
        public SelectionMode mode;
        public float startTime;
        public int totalBlocks;
        public int completedBlocks;
    }
    
    // Events
    public System.Action<List<Vector3>, SelectionMode> OnAreaSelected;
    public System.Action OnSelectionCleared;
    public System.Action<SelectionStats> OnSelectionStatsUpdated;
    public System.Action<WorkAreaVisualization> OnWorkAreaCreated;
    public System.Action<Vector3> OnBlockCompleted;
    public System.Action OnWorkCompleted;

    private void Start()
    {
        // Auto-find main controller if not assigned
        if (turtleMainController == null)
        {
            turtleMainController = FindFirstObjectByType<TurtleMainController>();
        }
        
        if (pathfinder == null)
        {
            pathfinder = FindFirstObjectByType<BlockWorldPathfinder>();
        }
        
        // Subscribe to turtle events for work progress tracking
        if (turtleMainController != null)
        {
            turtleMainController.OnProgressUpdate += HandleOperationProgress;
            turtleMainController.OnOperationCompleted += HandleOperationCompleted;
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateVisualization();
        UpdateWorkAreaVisualization();
    }

    private void HandleInput()
    {
        // Start selection with Left Click (when in selection mode)
        if (Input.GetMouseButtonDown(0) && currentMode != SelectionMode.None)
        {
            StartSelection();
        }
        
        // Update selection while dragging
        if (Input.GetMouseButton(0) && selectionStart.HasValue && currentMode != SelectionMode.None)
        {
            UpdateSelection();
        }
        
        // Finish selection
        if (Input.GetMouseButtonUp(0) && selectionStart.HasValue && selectionEnd.HasValue)
        {
            FinishSelection();
        }
        
        // Cancel selection with Right Click or ESC
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSelection();
        }
        
        // Toggle selection modes with hotkeys
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMode(SelectionMode.Mining);
        }
        
        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleMode(SelectionMode.Building);
        }

        // Execute selection with Enter or Space
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteSelectedOperation();
        }

        // Preview optimization with Tab
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleOptimizationPreview();
        }
        
        // Toggle work area visualization with V
        if (Input.GetKeyDown(KeyCode.V))
        {
            showWorkProgress = !showWorkProgress;
            if (activeWorkArea != null)
            {
                activeWorkArea.container.SetActive(showWorkProgress);
            }
        }
    }

    private void StartSelection()
    {
        Vector3? worldPos = GetWorldPositionFromMouse();
        if (worldPos.HasValue)
        {
            selectionStart = GetBlockPosition(worldPos.Value);
            selectionEnd = selectionStart;
            Debug.Log($"Started {currentMode} selection at {selectionStart}");
        }
    }

    private void UpdateSelection()
    {
        Vector3? worldPos = GetWorldPositionFromMouse();
        if (worldPos.HasValue)
        {
            selectionEnd = GetBlockPosition(worldPos.Value);
        }
    }

    private void FinishSelection()
    {
        if (selectionStart.HasValue && selectionEnd.HasValue)
        {
            selectedBlocks = GetBlocksInSelection(selectionStart.Value, selectionEnd.Value);
            
            // Validate and optimize selection using new architecture
            ValidateSelectionWithNewSystem();
            OptimizeSelectionWithNewSystem();
            UpdateSelectionStats();
            
            Debug.Log($"Selected {selectedBlocks.Count} blocks for {currentMode}");
            OnAreaSelected?.Invoke(selectedBlocks, currentMode);
            
            CreateVisualization();
        }
    }

    /// <summary>
    /// Creates persistent work area visualization when operation starts
    /// </summary>
    private void CreateWorkAreaVisualization(List<Vector3> blocks, SelectionMode mode)
    {
        // Clear any existing work area
        ClearWorkAreaVisualization();
        
        activeWorkArea = new WorkAreaVisualization
        {
            container = new GameObject("WorkAreaVisualization"),
            mode = mode,
            startTime = Time.time,
            totalBlocks = blocks.Count,
            completedBlocks = 0
        };
        
        workingBlocks = new List<Vector3>(blocks);
        completedBlocks.Clear();
        
        // Create individual block visualizations
        foreach (var blockPos in blocks)
        {
            GameObject blockViz = CreateWorkBlockVisualization(blockPos, mode);
            activeWorkArea.blockVisuals[blockPos] = blockViz;
            blockViz.transform.SetParent(activeWorkArea.container.transform);
        }
        
        // Create progress line renderer
        if (blocks.Count > 1)
        {
            GameObject lineObj = new GameObject("WorkProgressLine");
            lineObj.transform.SetParent(activeWorkArea.container.transform);
            
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.yellow;
            lr.endColor = Color.green;
            lr.startWidth = 0.15f;
            lr.endWidth = 0.15f;
            lr.positionCount = blocks.Count;
            lr.useWorldSpace = true;
            
            for (int i = 0; i < blocks.Count; i++)
            {
                lr.SetPosition(i, blocks[i]);
            }
            
            activeWorkArea.progressLine = lr;
        }
        
        // Create progress text
        GameObject textObj = new GameObject("WorkProgressText");
        textObj.transform.SetParent(activeWorkArea.container.transform);
        textObj.transform.position = GetWorkAreaCenter(blocks) + Vector3.up * 2f;
        
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = "0%";
        tm.fontSize = 30;
        tm.color = Color.white;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        
        activeWorkArea.progressText = tm;
        
        OnWorkAreaCreated?.Invoke(activeWorkArea);
        
        Debug.Log($"Created work area visualization for {blocks.Count} blocks");
    }
    
    private GameObject CreateWorkBlockVisualization(Vector3 position, SelectionMode mode)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = $"WorkBlock_{position}";
        block.transform.position = position;
        block.transform.localScale = Vector3.one * 1.02f; // Slightly larger than actual block
        
        // Remove collider
        DestroyImmediate(block.GetComponent<Collider>());
        
        // Setup material
        Renderer renderer = block.GetComponent<Renderer>();
        Material mat = CreateTransparentMaterial();
        
        Color color = mode == SelectionMode.Mining ? miningAreaColor : buildingAreaColor;
        color.a = workAreaAlpha;
        mat.color = color;
        renderer.material = mat;
        
        return block;
    }
    
    private void UpdateWorkAreaVisualization()
    {
        if (activeWorkArea == null || !showWorkProgress) return;
        
        // Update current working block from turtle position
        if (turtleMainController != null && turtleMainController.IsBusy())
        {
            Vector3 turtlePos = turtleMainController.GetTurtlePosition();
            currentWorkingBlock = GetClosestWorkingBlock(turtlePos);
        }
        
        // Update block colors based on status
        foreach (var kvp in activeWorkArea.blockVisuals)
        {
            Vector3 blockPos = kvp.Key;
            GameObject blockViz = kvp.Value;
            
            if (blockViz == null) continue;
            
            Renderer renderer = blockViz.GetComponent<Renderer>();
            if (renderer == null) continue;
            
            Color targetColor;
            float targetScale = 1.02f;
            
            if (completedBlocks.Contains(blockPos))
            {
                // Completed block
                targetColor = completedBlockColor;
            }
            else if (currentWorkingBlock.HasValue && currentWorkingBlock.Value == blockPos)
            {
                // Current working block - animate
                targetColor = currentBlockColor;
                if (animateCurrentBlock)
                {
                    float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                    targetScale = 1.02f + pulse;
                    targetColor.a = workAreaAlpha + pulse * 0.3f;
                }
            }
            else
            {
                // Pending block
                targetColor = workInProgressColor;
            }
            
            renderer.material.color = Color.Lerp(renderer.material.color, targetColor, Time.deltaTime * 5f);
            blockViz.transform.localScale = Vector3.Lerp(
                blockViz.transform.localScale, 
                Vector3.one * targetScale, 
                Time.deltaTime * 5f
            );
        }
        
        // Update progress text
        if (activeWorkArea.progressText != null)
        {
            float progress = activeWorkArea.totalBlocks > 0 
                ? (float)activeWorkArea.completedBlocks / activeWorkArea.totalBlocks * 100f 
                : 0f;
            
            activeWorkArea.progressText.text = $"{progress:F0}%\n{activeWorkArea.completedBlocks}/{activeWorkArea.totalBlocks}";
            
            // Pulse text when working
            if (animateCurrentBlock && currentWorkingBlock.HasValue)
            {
                float pulse = Mathf.Sin(Time.time * pulseSpeed * 0.5f) * 0.2f + 0.8f;
                activeWorkArea.progressText.color = new Color(1f, 1f, 1f, pulse);
            }
        }
        
        // Update progress line color
        if (activeWorkArea.progressLine != null && workingBlocks.Count > 0)
        {
            float progress = (float)activeWorkArea.completedBlocks / activeWorkArea.totalBlocks;
            
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.red, 0.0f),
                    new GradientColorKey(Color.yellow, progress),
                    new GradientColorKey(Color.green, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.5f, 0.0f),
                    new GradientAlphaKey(1.0f, progress),
                    new GradientAlphaKey(0.3f, 1.0f)
                }
            );
            
            activeWorkArea.progressLine.colorGradient = gradient;
        }
    }
    
    private Vector3 GetClosestWorkingBlock(Vector3 position)
    {
        if (workingBlocks.Count == 0) return position;
        
        Vector3 closest = workingBlocks[0];
        float minDist = Vector3.Distance(position, closest);
        
        foreach (var block in workingBlocks)
        {
            if (completedBlocks.Contains(block)) continue;
            
            float dist = Vector3.Distance(position, block);
            if (dist < minDist)
            {
                minDist = dist;
                closest = block;
            }
        }
        
        return closest;
    }
    
    private Vector3 GetWorkAreaCenter(List<Vector3> blocks)
    {
        if (blocks.Count == 0) return Vector3.zero;
        
        Vector3 sum = Vector3.zero;
        foreach (var block in blocks)
        {
            sum += block;
        }
        return sum / blocks.Count;
    }
    
    private void HandleOperationProgress(OperationStats stats)
    {
        if (activeWorkArea == null) return;
        
        // Update completed blocks based on progress
        int newCompletedCount = Mathf.FloorToInt(stats.Progress * activeWorkArea.totalBlocks);
        
        // Mark newly completed blocks
        while (activeWorkArea.completedBlocks < newCompletedCount && 
               activeWorkArea.completedBlocks < workingBlocks.Count)
        {
            Vector3 completedBlock = workingBlocks[activeWorkArea.completedBlocks];
            completedBlocks.Add(completedBlock);
            activeWorkArea.completedBlocks++;
            
            OnBlockCompleted?.Invoke(completedBlock);
            
            // Visual feedback for completed block
            if (activeWorkArea.blockVisuals.TryGetValue(completedBlock, out GameObject blockViz))
            {
                StartCoroutine(AnimateBlockCompletion(blockViz));
            }
        }
        
        // Update stats
        if (currentStats != null)
        {
            currentStats.blocksCompleted = activeWorkArea.completedBlocks;
            currentStats.progressPercentage = stats.Progress * 100f;
            OnSelectionStatsUpdated?.Invoke(currentStats);
        }
    }
    
    private System.Collections.IEnumerator AnimateBlockCompletion(GameObject block)
    {
        if (block == null) yield break;
        
        Vector3 originalScale = block.transform.localScale;
        float animTime = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < animTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animTime;
            
            // Pop effect
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
            block.transform.localScale = originalScale * scale;
            
            yield return null;
        }
        
        block.transform.localScale = originalScale;
    }
    
    private void HandleOperationCompleted(TurtleOperationManager.OperationType type, OperationStats stats)
    {
        if (activeWorkArea == null) return;
        
        Debug.Log($"Operation completed: {type}, Success: {stats.SuccessfulBlocks}/{stats.TotalAttempted} ({stats.SuccessRate:P1})");
        
        // Mark all blocks as completed
        foreach (var block in workingBlocks)
        {
            completedBlocks.Add(block);
        }
        
        activeWorkArea.completedBlocks = activeWorkArea.totalBlocks;
        
        // Update all visuals to completed state
        foreach (var kvp in activeWorkArea.blockVisuals)
        {
            if (kvp.Value != null)
            {
                Renderer renderer = kvp.Value.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = completedBlockColor;
                }
            }
        }
        
        // Update progress text
        if (activeWorkArea.progressText != null)
        {
            activeWorkArea.progressText.text = "COMPLETE!\n100%";
            activeWorkArea.progressText.color = Color.green;
        }
        
        OnWorkCompleted?.Invoke();
        
        // Auto-clear work area after delay
        StartCoroutine(AutoClearWorkArea(5f));
    }
    
    private System.Collections.IEnumerator AutoClearWorkArea(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Fade out work area
        float fadeTime = 1f;
        float elapsed = 0f;
        
        while (elapsed < fadeTime && activeWorkArea != null)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            
            foreach (var kvp in activeWorkArea.blockVisuals)
            {
                if (kvp.Value != null)
                {
                    Renderer renderer = kvp.Value.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color color = renderer.material.color;
                        color.a = alpha * workAreaAlpha;
                        renderer.material.color = color;
                    }
                }
            }
            
            if (activeWorkArea.progressText != null)
            {
                Color textColor = activeWorkArea.progressText.color;
                textColor.a = alpha;
                activeWorkArea.progressText.color = textColor;
            }
            
            yield return null;
        }
        
        ClearWorkAreaVisualization();
    }
    
    private void ClearWorkAreaVisualization()
    {
        if (activeWorkArea != null)
        {
            if (activeWorkArea.container != null)
            {
                DestroyImmediate(activeWorkArea.container);
            }
            activeWorkArea = null;
        }
        
        workingBlocks.Clear();
        completedBlocks.Clear();
        currentWorkingBlock = null;
    }

    /// <summary>
    /// Updated validation using the new turtle system architecture
    /// </summary>
    private void ValidateSelectionWithNewSystem()
    {
        validBlocks.Clear();
        invalidBlocks.Clear();

        if (currentMode == SelectionMode.Mining && turtleMainController != null)
        {
            // Use the new architecture's validation system
            validBlocks = turtleMainController.ValidateMiningBlocks(selectedBlocks);
            invalidBlocks = selectedBlocks.Except(validBlocks).ToList();
        }
        else if (currentMode == SelectionMode.Building && turtleMainController != null)
        {
            // Validate building positions
            validBlocks.Clear();
            invalidBlocks.Clear();
            
            foreach (var blockPos in selectedBlocks)
            {
                if (turtleMainController.CanPlaceBlockAt(blockPos))
                {
                    validBlocks.Add(blockPos);
                }
                else
                {
                    invalidBlocks.Add(blockPos);
                }
            }
        }
        else
        {
            // Fallback: assume all blocks are valid
            validBlocks = new List<Vector3>(selectedBlocks);
        }

        Debug.Log($"Block validation: {selectedBlocks.Count} total, {validBlocks.Count} valid, {invalidBlocks.Count} invalid");
    }

    /// <summary>
    /// Updated optimization using the new turtle system architecture
    /// </summary>
    private void OptimizeSelectionWithNewSystem()
    {
        optimizedOrder.Clear();

        if (currentMode == SelectionMode.Mining && turtleMainController != null && validBlocks.Count > 0)
        {
            // Use the new architecture's optimization system
            optimizedOrder = turtleMainController.OptimizeMiningOrder(validBlocks);
        }
        else
        {
            optimizedOrder = new List<Vector3>(validBlocks);
        }

        if (pathfinder != null && optimizedOrder.Count > 1)
        {
            // Calculate potential savings
            float originalDistance = CalculatePathDistance(validBlocks);
            float optimizedDistance = CalculatePathDistance(optimizedOrder);
            float savings = originalDistance > 0 ? (originalDistance - optimizedDistance) / originalDistance * 100f : 0f;
            
            Debug.Log($"Path optimization: {originalDistance:F1} -> {optimizedDistance:F1} units ({savings:F1}% savings)");
        }
    }

    private float CalculatePathDistance(List<Vector3> blocks)
    {
        if (blocks.Count < 2) return 0f;

        float totalDistance = 0f;
        Vector3 startPos = turtleMainController?.GetTurtlePosition() ?? blocks[0];

        Vector3 currentPos = startPos;
        foreach (var block in blocks)
        {
            totalDistance += Vector3.Distance(currentPos, block);
            currentPos = block;
        }

        return totalDistance;
    }

    private void UpdateSelectionStats()
    {
        currentStats = new SelectionStats
        {
            totalSelected = selectedBlocks.Count,
            validBlocks = validBlocks.Count,
            invalidBlocks = invalidBlocks.Count,
            estimatedDistance = CalculatePathDistance(optimizedOrder),
            blocksCompleted = 0,
            progressPercentage = 0f
        };

        if (validBlocks.Count > 0 && optimizedOrder.Count > 0)
        {
            float originalDistance = CalculatePathDistance(validBlocks);
            float optimizedDistance = CalculatePathDistance(optimizedOrder);
            currentStats.optimizationSavings = originalDistance > 0 
                ? (originalDistance - optimizedDistance) / originalDistance * 100f 
                : 0f;
        }

        currentStats.summary = GenerateStatsummary();
        OnSelectionStatsUpdated?.Invoke(currentStats);
    }

    private string GenerateStatsummary()
    {
        return $"Selection Summary:\n" +
               $"Total Blocks: {currentStats.totalSelected}\n" +
               $"Valid Blocks: {currentStats.validBlocks}\n" +
               $"Invalid Blocks: {currentStats.invalidBlocks}\n" +
               $"Completed: {currentStats.blocksCompleted} ({currentStats.progressPercentage:F1}%)\n" +
               $"Estimated Distance: {currentStats.estimatedDistance:F1} units\n" +
               $"Optimization Savings: {currentStats.optimizationSavings:F1}%\n" +
               $"Mode: {currentMode}";
    }

    private void CancelSelection()
    {
        selectionStart = null;
        selectionEnd = null;
        currentMode = SelectionMode.None;
        selectedBlocks.Clear();
        validBlocks.Clear();
        invalidBlocks.Clear();
        optimizedOrder.Clear();
        currentStats = null;
        
        ClearVisualization();
        OnSelectionCleared?.Invoke();
        
        Debug.Log("Selection cancelled");
    }

    public void ToggleMode(SelectionMode mode)
    {
        if (currentMode == mode)
        {
            CancelSelection();
        }
        else
        {
            CancelSelection();
            currentMode = mode;
            Debug.Log($"Selection mode: {currentMode}");
        }
    }

    public void ToggleOptimizationPreview()
    {
        previewOptimization = !previewOptimization;
        UpdateVisualization();
        Debug.Log($"Optimization preview: {(previewOptimization ? "ON" : "OFF")}");
    }

    private Vector3? GetWorldPositionFromMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, selectionLayerMask))
        {
            return hit.point;
        }
        
        return null;
    }

    private Vector3 GetBlockPosition(Vector3 worldPosition)
    {
        return new Vector3(
            Mathf.Floor(worldPosition.x),
            Mathf.Floor(worldPosition.y),
            Mathf.Floor(worldPosition.z)
        );
    }

    private List<Vector3> GetBlocksInSelection(Vector3 start, Vector3 end)
    {
        List<Vector3> blocks = new List<Vector3>();
        
        Vector3 min = new Vector3(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y),
            Mathf.Min(start.z, end.z)
        );
        
        Vector3 max = new Vector3(
            Mathf.Max(start.x, end.x),
            Mathf.Max(start.y, end.y),
            Mathf.Max(start.z, end.z)
        );
        
        for (float x = min.x; x <= max.x; x++)
        {
            for (float y = min.y; y <= max.y; y++)
            {
                for (float z = min.z; z <= max.z; z++)
                {
                    blocks.Add(new Vector3(x, y, z));
                }
            }
        }
        
        return blocks;
    }

    private void UpdateVisualization()
    {
        // Update selection box during dragging
        if (selectionStart.HasValue && selectionEnd.HasValue && Input.GetMouseButton(0))
        {
            UpdateSelectionBox();
        }
    }

    private void UpdateSelectionBox()
    {
        if (selectionVisualization == null)
        {
            selectionVisualization = CreateSelectionBox();
        }
        
        Vector3 start = selectionStart.Value;
        Vector3 end = selectionEnd.Value;
        
        Vector3 center = (start + end) / 2f;
        Vector3 size = new Vector3(
            Mathf.Abs(end.x - start.x) + 1f,
            Mathf.Abs(end.y - start.y) + 1f,
            Mathf.Abs(end.z - start.z) + 1f
        );
        
        selectionVisualization.transform.position = center;
        selectionVisualization.transform.localScale = size;
        
        // Set color based on mode
        Color color = currentMode == SelectionMode.Mining ? miningAreaColor : buildingAreaColor;
        color.a = gizmoAlpha;
        selectionVisualization.GetComponent<Renderer>().material.color = color;
    }

    private GameObject CreateSelectionBox()
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "SelectionBox";
        
        // Remove collider
        DestroyImmediate(box.GetComponent<Collider>());
        
        // Setup material
        Renderer renderer = box.GetComponent<Renderer>();
        Material mat = CreateTransparentMaterial();
        renderer.material = mat;
        
        return box;
    }

    private void CreateVisualization()
    {
        ClearVisualization();
        
        // Create block gizmos
        CreateBlockGizmos();
        
        // Create validation visualization
        if (showBlockValidation)
        {
            CreateValidationGizmos();
        }
        
        // Create optimization preview
        if (previewOptimization && optimizedOrder.Count > 1)
        {
            CreateOptimizationGizmos();
        }
    }

    private void CreateBlockGizmos()
    {
        Color gizmoColor = currentMode == SelectionMode.Mining ? miningAreaColor : buildingAreaColor;
        gizmoColor.a = gizmoAlpha;
        
        foreach (Vector3 blockPos in selectedBlocks)
        {
            GameObject gizmo = CreateBlockGizmo(blockPos, gizmoColor, "BlockGizmo");
            blockGizmos.Add(gizmo);
        }
    }

    private void CreateValidationGizmos()
    {
        // Valid blocks in blue
        Color validColor = validBlockColor;
        validColor.a = gizmoAlpha;
        
        foreach (Vector3 blockPos in validBlocks)
        {
            GameObject gizmo = CreateBlockGizmo(blockPos, validColor, "ValidBlock", 1.05f);
            validationGizmos.Add(gizmo);
        }
        
        // Invalid blocks in gray
        Color invalidColor = invalidBlockColor;
        invalidColor.a = gizmoAlpha;
        
        foreach (Vector3 blockPos in invalidBlocks)
        {
            GameObject gizmo = CreateBlockGizmo(blockPos, invalidColor, "InvalidBlock", 0.95f);
            validationGizmos.Add(gizmo);
        }
    }

    private void CreateOptimizationGizmos()
    {
        // Draw path lines between optimized blocks
        for (int i = 0; i < optimizedOrder.Count - 1; i++)
        {
            Vector3 start = optimizedOrder[i];
            Vector3 end = optimizedOrder[i + 1];

            GameObject line = CreatePathLine(start, end, i);
            optimizationGizmos.Add(line);
        }

        // Number the blocks in optimized order
        for (int i = 0; i < optimizedOrder.Count; i++)
        {
            GameObject numberGizmo = CreateNumberGizmo(optimizedOrder[i], i + 1);
            optimizationGizmos.Add(numberGizmo);
        }
    }

    private GameObject CreateBlockGizmo(Vector3 position, Color color, string name, float scale = 1.1f)
    {
        GameObject gizmo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gizmo.name = $"{name}_{position}";
        gizmo.transform.position = position;
        gizmo.transform.localScale = Vector3.one * scale;
        
        // Remove collider
        DestroyImmediate(gizmo.GetComponent<Collider>());
        
        // Setup material
        Renderer renderer = gizmo.GetComponent<Renderer>();
        Material mat = CreateTransparentMaterial();
        mat.color = color;
        renderer.material = mat;
        
        return gizmo;
    }

    private GameObject CreatePathLine(Vector3 start, Vector3 end, int index)
    {
        GameObject line = new GameObject($"PathLine_{index}");
        LineRenderer lr = line.AddComponent<LineRenderer>();
        
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.green;
        lr.endColor = Color.red;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        
        return line;
    }

    private GameObject CreateNumberGizmo(Vector3 position, int number)
    {
        GameObject numberGO = new GameObject($"Number_{number}");
        numberGO.transform.position = position + Vector3.up * 1.5f;
        
        // Create a simple text mesh (you might want to use TextMeshPro for better results)
        TextMesh textMesh = numberGO.AddComponent<TextMesh>();
        textMesh.text = number.ToString();
        textMesh.fontSize = 20;
        textMesh.color = Color.white;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        
        return numberGO;
    }

    private Material CreateTransparentMaterial()
    {
        Material mat = new Material(Shader.Find("HDRP/Lit"));
        mat.SetFloat("_Mode", 3); // Transparent mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }

    private void ClearVisualization()
    {
        if (selectionVisualization != null)
        {
            DestroyImmediate(selectionVisualization);
            selectionVisualization = null;
        }
        
        ClearGizmoList(blockGizmos);
        ClearGizmoList(validationGizmos);
        ClearGizmoList(optimizationGizmos);
    }

    private void ClearGizmoList(List<GameObject> gizmos)
    {
        foreach (GameObject gizmo in gizmos)
        {
            if (gizmo != null)
                DestroyImmediate(gizmo);
        }
        gizmos.Clear();
    }

    // Public API
    public bool IsSelecting => currentMode != SelectionMode.None;
    public SelectionMode CurrentMode => currentMode;
    public List<Vector3> SelectedBlocks => new List<Vector3>(selectedBlocks);
    public List<Vector3> ValidBlocks => new List<Vector3>(validBlocks);
    public List<Vector3> OptimizedOrder => new List<Vector3>(optimizedOrder);
    public SelectionStats CurrentStats => currentStats;
    
    /// <summary>
    /// Executes the selected operation using the new turtle architecture
    /// </summary>
    public void ExecuteSelectedOperation()
    {
        if (validBlocks.Count == 0)
        {
            Debug.LogWarning("No valid blocks selected for operation");
            return;
        }
        
        switch (currentMode)
        {
            case SelectionMode.Mining:
                ExecuteOptimizedMining();
                break;
            case SelectionMode.Building:
                Debug.Log("Please select a structure to build first");
                break;
            default:
                Debug.LogWarning("No operation mode selected");
                break;
        }
    }
    
    private void ExecuteOptimizedMining()
    {
        if (turtleMainController == null)
        {
            Debug.LogError("TurtleMainController not found!");
            return;
        }

        List<Vector3> blocksToMine = previewOptimization && optimizedOrder.Count > 0 
            ? optimizedOrder 
            : validBlocks;

        Debug.Log($"Executing mining operation: {blocksToMine.Count} blocks");
        
        if (currentStats != null)
        {
            Debug.Log(currentStats.summary);
        }

        // Create work area visualization BEFORE starting the operation
        CreateWorkAreaVisualization(blocksToMine, SelectionMode.Mining);

        // Use the new architecture's mining method
        turtleMainController.StartOptimizedMining(blocksToMine);
        
        // Clear selection visualization but keep work area
        ClearVisualization();
        ResetSelection();
    }
    
    /// <summary>
    /// Executes building operation with selected structure using new architecture
    /// </summary>
    public void ExecuteBuilding(StructureData structureData)
    {
        if (validBlocks.Count == 0 || structureData == null)
        {
            Debug.LogWarning("No valid build location selected or no structure provided");
            return;
        }
        
        if (turtleMainController == null)
        {
            Debug.LogError("TurtleMainController not found!");
            return;
        }

        Vector3 buildOrigin = validBlocks[0];
        Debug.Log($"Executing building operation: {structureData.name} at {buildOrigin}");
        
        // Create work area visualization for building
        List<Vector3> buildPositions = new List<Vector3>();
        foreach (var block in structureData.blocks)
        {
            buildPositions.Add(buildOrigin + (Vector3)block.position);
        }
        CreateWorkAreaVisualization(buildPositions, SelectionMode.Building);
        
        // Use the new architecture's validated building method
        turtleMainController.StartValidatedBuilding(buildOrigin, structureData);
        
        ClearVisualization();
        ResetSelection();
    }

    /// <summary>
    /// Resets selection without clearing visualization
    /// </summary>
    private void ResetSelection()
    {
        selectedBlocks.Clear();
        validBlocks.Clear();
        invalidBlocks.Clear();
        optimizedOrder.Clear();
        currentStats = null;
        currentMode = SelectionMode.None;
        selectionStart = null;
        selectionEnd = null;
    }

    /// <summary>
    /// Forces revalidation of current selection using new architecture
    /// </summary>
    public void RevalidateSelection()
    {
        if (selectedBlocks.Count > 0)
        {
            ValidateSelectionWithNewSystem();
            OptimizeSelectionWithNewSystem();
            UpdateSelectionStats();
            CreateVisualization();
            
            Debug.Log("Selection revalidated and optimized");
        }
    }

    /// <summary>
    /// Gets a detailed report of the current selection using new architecture
    /// </summary>
    public string GetDetailedSelectionReport()
    {
        if (currentStats == null)
            return "No active selection";

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== SELECTION REPORT ===");
        report.AppendLine($"Mode: {currentMode}");
        report.AppendLine($"Total Selected: {currentStats.totalSelected}");
        report.AppendLine($"Valid Blocks: {currentStats.validBlocks}");
        report.AppendLine($"Invalid Blocks: {currentStats.invalidBlocks}");
        report.AppendLine($"Completed: {currentStats.blocksCompleted} ({currentStats.progressPercentage:F1}%)");
        report.AppendLine($"Estimated Distance: {currentStats.estimatedDistance:F1} units");
        
        if (currentStats.optimizationSavings > 0)
        {
            report.AppendLine($"Optimization Savings: {currentStats.optimizationSavings:F1}%");
        }

        // Add system health check
        if (turtleMainController != null)
        {
            report.AppendLine("\n--- SYSTEM STATUS ---");
            report.AppendLine($"System Ready: {turtleMainController.IsReady()}");
            report.AppendLine($"Current Status: {turtleMainController.GetStatusString()}");
        }

        // Add work area status
        if (activeWorkArea != null)
        {
            report.AppendLine("\n--- WORK AREA STATUS ---");
            report.AppendLine($"Total Blocks: {activeWorkArea.totalBlocks}");
            report.AppendLine($"Completed: {activeWorkArea.completedBlocks}");
            float workProgress = activeWorkArea.totalBlocks > 0 
                ? (float)activeWorkArea.completedBlocks / activeWorkArea.totalBlocks * 100f 
                : 0f;
            report.AppendLine($"Progress: {workProgress:F1}%");
            float elapsedTime = Time.time - activeWorkArea.startTime;
            report.AppendLine($"Elapsed Time: {elapsedTime:F1}s");
        }

        if (optimizedOrder.Count > 0)
        {
            report.AppendLine("\n--- OPTIMIZED ORDER ---");
            for (int i = 0; i < Mathf.Min(optimizedOrder.Count, 10); i++)
            {
                report.AppendLine($"{i + 1}. {optimizedOrder[i]}");
            }
            
            if (optimizedOrder.Count > 10)
            {
                report.AppendLine($"... and {optimizedOrder.Count - 10} more blocks");
            }
        }

        if (invalidBlocks.Count > 0)
        {
            report.AppendLine("\n--- INVALID BLOCKS ---");
            for (int i = 0; i < Mathf.Min(invalidBlocks.Count, 5); i++)
            {
                report.AppendLine($"- {invalidBlocks[i]} (reason: validation failed)");
            }
            
            if (invalidBlocks.Count > 5)
            {
                report.AppendLine($"... and {invalidBlocks.Count - 5} more invalid blocks");
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Get mining operation plan using new architecture
    /// </summary>
    public MiningOperationPlan GetMiningPlan()
    {
        if (turtleMainController == null || currentMode != SelectionMode.Mining)
            return null;

        return turtleMainController.PrepareMiningOperation(selectedBlocks);
    }

    /// <summary>
    /// Exports selection data for external use
    /// </summary>
    public SelectionExport ExportSelection()
    {
        return new SelectionExport
        {
            mode = currentMode,
            selectedBlocks = new List<Vector3>(selectedBlocks),
            validBlocks = new List<Vector3>(validBlocks),
            invalidBlocks = new List<Vector3>(invalidBlocks),
            optimizedOrder = new List<Vector3>(optimizedOrder),
            stats = currentStats,
            timestamp = System.DateTime.Now
        };
    }

    /// <summary>
    /// Imports previously exported selection data
    /// </summary>
    public void ImportSelection(SelectionExport export)
    {
        if (export == null) return;

        CancelSelection();
        
        currentMode = export.mode;
        selectedBlocks = export.selectedBlocks ?? new List<Vector3>();
        validBlocks = export.validBlocks ?? new List<Vector3>();
        invalidBlocks = export.invalidBlocks ?? new List<Vector3>();
        optimizedOrder = export.optimizedOrder ?? new List<Vector3>();
        currentStats = export.stats;

        CreateVisualization();
        OnAreaSelected?.Invoke(selectedBlocks, currentMode);
        
        Debug.Log($"Imported selection: {selectedBlocks.Count} blocks, mode: {currentMode}");
    }

    private void OnDestroy()
    {
        ClearVisualization();
        ClearWorkAreaVisualization();
        
        // Unsubscribe from events
        if (turtleMainController != null)
        {
            turtleMainController.OnProgressUpdate -= HandleOperationProgress;
            turtleMainController.OnOperationCompleted -= HandleOperationCompleted;
        }
    }

    // Gizmos for editor visualization
    private void OnDrawGizmos()
    {
        if (selectionStart.HasValue && selectionEnd.HasValue)
        {
            Gizmos.color = currentMode == SelectionMode.Mining ? miningAreaColor : buildingAreaColor;
            
            Vector3 start = selectionStart.Value;
            Vector3 end = selectionEnd.Value;
            Vector3 center = (start + end) / 2f;
            Vector3 size = new Vector3(
                Mathf.Abs(end.x - start.x) + 1f,
                Mathf.Abs(end.y - start.y) + 1f,
                Mathf.Abs(end.z - start.z) + 1f
            );
            
            Gizmos.DrawWireCube(center, size);
        }

        // Draw optimization path in editor
        if (previewOptimization && optimizedOrder.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < optimizedOrder.Count - 1; i++)
            {
                Vector3 start = optimizedOrder[i];
                Vector3 end = optimizedOrder[i + 1];
                Gizmos.DrawLine(start, end);
            }
        }
        
        // Draw work area in editor
        if (activeWorkArea != null && workingBlocks.Count > 0)
        {
            // Draw completed blocks
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            foreach (var block in completedBlocks)
            {
                Gizmos.DrawCube(block, Vector3.one * 0.9f);
            }
            
            // Draw current working block
            if (currentWorkingBlock.HasValue)
            {
                Gizmos.color = new Color(1, 1, 0, 0.5f);
                float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
                Gizmos.DrawCube(currentWorkingBlock.Value, Vector3.one * (1f + pulse));
            }
            
            // Draw pending blocks
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            foreach (var block in workingBlocks)
            {
                if (!completedBlocks.Contains(block) && 
                    (!currentWorkingBlock.HasValue || currentWorkingBlock.Value != block))
                {
                    Gizmos.DrawCube(block, Vector3.one * 0.9f);
                }
            }
        }
    }

    // UI Integration Methods
    public void OnUI_ToggleMining() => ToggleMode(SelectionMode.Mining);
    public void OnUI_ToggleBuilding() => ToggleMode(SelectionMode.Building);
    public void OnUI_ExecuteOperation() => ExecuteSelectedOperation();
    public void OnUI_CancelSelection() => CancelSelection();
    public void OnUI_ToggleOptimization() => ToggleOptimizationPreview();
    public void OnUI_RevalidateSelection() => RevalidateSelection();
    public void OnUI_ToggleWorkVisualization() => showWorkProgress = !showWorkProgress;
    public void OnUI_ClearWorkArea() => ClearWorkAreaVisualization();
}

/// <summary>
/// Data structure for exporting/importing selections
/// </summary>
[System.Serializable]
public class SelectionExport
{
    public AreaSelectionManager.SelectionMode mode;
    public List<Vector3> selectedBlocks;
    public List<Vector3> validBlocks;
    public List<Vector3> invalidBlocks;
    public List<Vector3> optimizedOrder;
    public AreaSelectionManager.SelectionStats stats;
    public System.DateTime timestamp;
}
