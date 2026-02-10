using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Optimized area selection manager - maintains compatibility with existing code
/// </summary>
public class AreaSelectionManager : MonoBehaviour
{
    #region Enums and Classes
    
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
        public int blocksCompleted;
        public float progressPercentage;
        public string summary;
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
    
    #endregion

    #region Fields
    
    [Header("References")]
    public RTSCameraController cameraController;
    public TurtleMainController turtleMainController;
    public TurtleSelectionManager turtleSelectionManager;
    public BlockWorldPathfinder pathfinder;
    
    [Header("Selection Settings")]
    public LayerMask selectionLayerMask = -1;
    public float raycastDistance = 1000f;
    public bool showBlockValidation = true;
    public bool previewOptimization = true;
    public bool enableKeyboardInput = false; // Set to false when using ModernUIManager
    
    [Header("Visual Settings")]
    public Material selectionMaterial;
    public Color miningAreaColor = Color.red;
    public Color buildingAreaColor = Color.green;
    public Color validBlockColor = Color.blue;
    public Color invalidBlockColor = Color.gray;
    public float gizmoAlpha = 0.3f;
    
    [Header("Work Area Visualization")]
    public bool showWorkProgress = true;
    
    // Selection state
    private SelectionMode currentMode = SelectionMode.None;
    private Vector3? selectionStart;
    private Vector3? selectionEnd;
    private readonly List<Vector3> selectedBlocks = new List<Vector3>();
    private readonly List<Vector3> validBlocks = new List<Vector3>();
    private readonly List<Vector3> invalidBlocks = new List<Vector3>();
    private List<Vector3> optimizedOrder = new List<Vector3>();
    private SelectionStats currentStats;
    
    // Work area state (for compatibility)
    private WorkAreaVisualization activeWorkArea;
    
    // Visualization
    private AreaSelectionVisualizer visualizer;
    
    #endregion

    #region Events
    
    public System.Action<List<Vector3>, SelectionMode> OnAreaSelected;
    public System.Action OnSelectionCleared;
    public System.Action<SelectionStats> OnSelectionStatsUpdated;
    public System.Action<WorkAreaVisualization> OnWorkAreaCreated;
    public System.Action<Vector3> OnBlockCompleted;
    public System.Action OnWorkCompleted;
    
    #endregion

    #region Unity Lifecycle
    
    private void Awake()
    {
        visualizer = GetComponent<AreaSelectionVisualizer>() ?? 
                     gameObject.AddComponent<AreaSelectionVisualizer>();
        
        // Pass visual settings to visualizer
        if (visualizer != null)
        {
            visualizer.selectionMaterial = selectionMaterial;
            visualizer.miningColor = miningAreaColor;
            visualizer.buildingColor = buildingAreaColor;
            visualizer.validColor = validBlockColor;
            visualizer.invalidColor = invalidBlockColor;
        }
    }
    
    private void Start()
    {
        // Auto-find dependencies if not assigned
        if (turtleMainController == null)
            turtleMainController = FindFirstObjectByType<TurtleMainController>();

        if (turtleSelectionManager == null)
            turtleSelectionManager = FindFirstObjectByType<TurtleSelectionManager>();

        if (pathfinder == null)
            pathfinder = FindFirstObjectByType<BlockWorldPathfinder>();
            
        // Subscribe to turtle events
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
    }
    
    private void OnDestroy()
    {
        if (turtleMainController != null)
        {
            turtleMainController.OnProgressUpdate -= HandleOperationProgress;
            turtleMainController.OnOperationCompleted -= HandleOperationCompleted;
        }
        visualizer?.ClearVisualization();
    }
    
    #endregion

    #region Input Handling
    
    private void HandleInput()
    {
        // Mode switching (only if keyboard input is enabled)
        if (enableKeyboardInput)
        {
            if (Input.GetKeyDown(KeyCode.M))
                ToggleMode(SelectionMode.Mining);
            else if (Input.GetKeyDown(KeyCode.B))
                ToggleMode(SelectionMode.Building);
            else if (Input.GetKeyDown(KeyCode.Escape))
                CancelSelection();
        }
        
        // Selection handling
        if (currentMode != SelectionMode.None)
        {
            if (Input.GetMouseButtonDown(0))
                StartSelection();
            else if (Input.GetMouseButton(0) && selectionStart.HasValue)
                UpdateSelection();
            else if (Input.GetMouseButtonUp(0) && selectionStart.HasValue)
                FinishSelection();
        }
        
        // Execute with Enter or Space
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) && selectedBlocks.Count > 0)
            ExecuteSelectedOperation();
            
        // Toggle optimization preview with Tab
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleOptimizationPreview();
            
        // Toggle work area visualization with V
        if (Input.GetKeyDown(KeyCode.V))
            showWorkProgress = !showWorkProgress;
    }
    
    #endregion

    #region Selection Logic
    
    private void StartSelection()
    {
        var worldPos = GetWorldPositionFromMouse();
        if (worldPos.HasValue)
        {
            selectionStart = GetBlockPosition(worldPos.Value);
            selectionEnd = selectionStart;
            Debug.Log($"Started {currentMode} selection at {selectionStart}");
        }
    }
    
    private void UpdateSelection()
    {
        var worldPos = GetWorldPositionFromMouse();
        if (worldPos.HasValue)
        {
            selectionEnd = GetBlockPosition(worldPos.Value);
            visualizer?.UpdateSelectionBox(selectionStart.Value, selectionEnd.Value, currentMode);
        }
    }
    
    private void FinishSelection()
    {
        if (!selectionStart.HasValue || !selectionEnd.HasValue) return;
        
        selectedBlocks.Clear();
        selectedBlocks.AddRange(GetBlocksInSelection(selectionStart.Value, selectionEnd.Value));
        
        ValidateSelectionWithNewSystem();
        OptimizeSelectionWithNewSystem();
        UpdateSelectionStats();
        
        Debug.Log($"Selected {selectedBlocks.Count} blocks for {currentMode}");
        OnAreaSelected?.Invoke(selectedBlocks, currentMode);
        
        CreateVisualization();
        
        selectionStart = null;
        selectionEnd = null;
    }
    
    #endregion

    #region Validation and Optimization
    
    private void ValidateSelectionWithNewSystem()
    {
        validBlocks.Clear();
        invalidBlocks.Clear();

        // Prüfe zuerst ob die benötigten Chunks geladen sind
        if (currentMode == SelectionMode.Mining && selectedBlocks.Count > 0)
        {
            CheckRequiredChunksLoaded(selectedBlocks);
        }

        if (currentMode == SelectionMode.Mining && turtleMainController != null)
        {
            validBlocks.AddRange(turtleMainController.ValidateMiningBlocks(selectedBlocks));
            invalidBlocks.AddRange(selectedBlocks.Except(validBlocks));
        }
        else if (currentMode == SelectionMode.Building && turtleMainController != null)
        {
            foreach (var blockPos in selectedBlocks)
            {
                if (turtleMainController.CanPlaceBlockAt(blockPos))
                    validBlocks.Add(blockPos);
                else
                    invalidBlocks.Add(blockPos);
            }
        }
        else
        {
            validBlocks.AddRange(selectedBlocks);
        }

        Debug.Log($"Block validation: {selectedBlocks.Count} total, {validBlocks.Count} valid, {invalidBlocks.Count} invalid");
    }

    /// <summary>
    /// Prüft ob alle benötigten Chunks für die Block-Liste geladen sind
    /// </summary>
    private void CheckRequiredChunksLoaded(List<Vector3> blocks)
    {
        if (turtleMainController == null || turtleMainController.worldManager == null)
            return;

        var worldManager = turtleMainController.worldManager;
        var requiredChunks = new HashSet<Vector2Int>();

        // Sammle alle benötigten Chunks
        foreach (var blockPos in blocks)
        {
            Vector2Int chunkCoord = worldManager.WorldPositionToChunkCoord(blockPos);
            requiredChunks.Add(chunkCoord);
        }

        // Prüfe welche Chunks nicht geladen sind
        var missingChunks = new List<Vector2Int>();
        var unloadedChunks = new List<Vector2Int>();

        foreach (var chunkCoord in requiredChunks)
        {
            var chunk = worldManager.GetChunkAt(chunkCoord);
            if (chunk == null)
            {
                missingChunks.Add(chunkCoord);
            }
            else if (!chunk.IsLoaded || chunk.VertexCount == 0)
            {
                unloadedChunks.Add(chunkCoord);
            }
        }

        if (missingChunks.Count > 0)
        {
            Debug.LogWarning($"Selection validation: {missingChunks.Count} required chunks are not loaded: {string.Join(", ", missingChunks)}");
            Debug.LogWarning($"  This may cause blocks to be filtered out as unmineable");
            Debug.LogWarning($"  Try moving the camera closer to the selected area to load the chunks");
        }

        if (unloadedChunks.Count > 0)
        {
            Debug.LogWarning($"Selection validation: {unloadedChunks.Count} required chunks exist but are not fully loaded: {string.Join(", ", unloadedChunks)}");
        }

        if (missingChunks.Count == 0 && unloadedChunks.Count == 0)
        {
            Debug.Log($"Selection validation: All {requiredChunks.Count} required chunks are loaded");
        }
    }
    
    private void OptimizeSelectionWithNewSystem()
    {
        optimizedOrder.Clear();

        if (currentMode == SelectionMode.Mining && turtleMainController != null && validBlocks.Count > 0)
        {
            optimizedOrder = turtleMainController.OptimizeMiningOrder(validBlocks);
        }
        else
        {
            optimizedOrder = new List<Vector3>(validBlocks);
        }
    }
    
    #endregion

    #region Execution - COMPATIBILITY METHODS
    
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
        }
    }
    
    // Compatibility method for ConstructionUI
    public void ExecuteTopDownMining()
    {
        ExecuteOptimizedMining();
    }
    
    private void ExecuteOptimizedMining()
    {
        List<Vector3> blocksToMine = previewOptimization && optimizedOrder.Count > 0 ?
                                     optimizedOrder : validBlocks;

        Debug.Log($"Executing mining operation: {blocksToMine.Count} blocks");

        // Create simple work area visualization
        CreateWorkAreaVisualization(blocksToMine, SelectionMode.Mining);

        // Use TurtleSelectionManager if available (multi-turtle support)
        if (turtleSelectionManager != null && turtleSelectionManager.HasSelection())
        {
            turtleSelectionManager.AssignMiningTask(blocksToMine);
            Debug.Log($"Mining task assigned to {turtleSelectionManager.GetSelectionCount()} selected turtle(s)");
        }
        // Fallback to single turtle controller
        else if (turtleMainController != null)
        {
            turtleMainController.StartOptimizedMining(blocksToMine);
            Debug.Log("Mining task assigned to default turtle");
        }
        else
        {
            Debug.LogError("No turtle controller or selection manager found!");
            return;
        }

        ClearVisualization();
        ResetSelection();
    }
    
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
        
        List<Vector3> buildPositions = new List<Vector3>();
        foreach (var block in structureData.blocks)
        {
            buildPositions.Add(buildOrigin + (Vector3)block.relativePosition);
        }
        CreateWorkAreaVisualization(buildPositions, SelectionMode.Building);
        
        turtleMainController.StartValidatedBuilding(buildOrigin, structureData);
        
        ClearVisualization();
        ResetSelection();
    }
    
    #endregion

    #region Public API - COMPATIBILITY METHODS
    
    // Main compatibility method for ConstructionUI
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
        
        visualizer?.SetMode(currentMode);
    }
    
    public void CancelSelection()
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
    
    public void ToggleOptimizationPreview()
    {
        previewOptimization = !previewOptimization;
        UpdateVisualization();
        Debug.Log($"Optimization preview: {(previewOptimization ? "ON" : "OFF")}");
    }
    
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
        report.AppendLine($"Estimated Distance: {currentStats.estimatedDistance:F1} units");
        
        if (currentStats.optimizationSavings > 0)
        {
            report.AppendLine($"Optimization Savings: {currentStats.optimizationSavings:F1}%");
        }

        return report.ToString();
    }
    
    public MiningOperationPlan GetMiningPlan()
    {
        if (turtleMainController == null || currentMode != SelectionMode.Mining)
            return null;

        return turtleMainController.PrepareMiningOperation(selectedBlocks);
    }
    
    #endregion

    #region Properties - COMPATIBILITY
    
    public bool IsSelecting => currentMode != SelectionMode.None;
    public SelectionMode CurrentMode => currentMode;
    public List<Vector3> SelectedBlocks => new List<Vector3>(selectedBlocks);
    public List<Vector3> ValidBlocks => new List<Vector3>(validBlocks);
    public List<Vector3> OptimizedOrder => new List<Vector3>(optimizedOrder);
    public SelectionStats CurrentStats => currentStats;
    
    #endregion

    #region Helper Methods
    
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
        // Block-Positionen müssen konsistent mit der Chunk-Logik berechnet werden
        // Verwende Floor um sicherzustellen dass wir immer die untere linke Ecke des Blocks bekommen
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

        currentStats.summary = GenerateStatSummary();
        OnSelectionStatsUpdated?.Invoke(currentStats);
    }
    
    private string GenerateStatSummary()
    {
        return $"Selection Summary:\n" +
               $"Total Blocks: {currentStats.totalSelected}\n" +
               $"Valid Blocks: {currentStats.validBlocks}\n" +
               $"Invalid Blocks: {currentStats.invalidBlocks}\n" +
               $"Mode: {currentMode}";
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
    
    private void UpdateVisualization()
    {
        if (selectionStart.HasValue && selectionEnd.HasValue && Input.GetMouseButton(0))
        {
            visualizer?.UpdateSelectionBox(selectionStart.Value, selectionEnd.Value, currentMode);
        }
        
        if (showBlockValidation)
        {
            visualizer?.UpdateVisualization(selectedBlocks, currentMode);
        }
    }
    
    private void CreateVisualization()
    {
        ClearVisualization();
        visualizer?.UpdateVisualization(selectedBlocks, currentMode);
    }
    
    private void ClearVisualization()
    {
        visualizer?.ClearVisualization();
    }
    
    #endregion
    
    #region Work Area Visualization - SIMPLIFIED FOR COMPATIBILITY
    
    private void CreateWorkAreaVisualization(List<Vector3> blocks, SelectionMode mode)
    {
        ClearWorkAreaVisualization();
        
        activeWorkArea = new WorkAreaVisualization
        {
            container = new GameObject("WorkAreaVisualization"),
            mode = mode,
            startTime = Time.time,
            totalBlocks = blocks.Count,
            completedBlocks = 0
        };
        
        OnWorkAreaCreated?.Invoke(activeWorkArea);
        
        Debug.Log($"Created work area visualization for {blocks.Count} blocks");
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
    }
    
    private void HandleOperationProgress(OperationStats stats)
    {
        if (activeWorkArea == null) return;
        
        int newCompletedCount = Mathf.FloorToInt(stats.Progress * activeWorkArea.totalBlocks);
        activeWorkArea.completedBlocks = newCompletedCount;
        
        if (currentStats != null)
        {
            currentStats.blocksCompleted = activeWorkArea.completedBlocks;
            currentStats.progressPercentage = stats.Progress * 100f;
            OnSelectionStatsUpdated?.Invoke(currentStats);
        }
    }
    
    private void HandleOperationCompleted(TurtleOperationManager.OperationType type, OperationStats stats)
    {
        if (activeWorkArea == null) return;
        
        Debug.Log($"Operation completed: {type}, Success: {stats.SuccessfulBlocks}/{stats.TotalAttempted} ({stats.SuccessRate:P1})");
        
        activeWorkArea.completedBlocks = activeWorkArea.totalBlocks;
        OnWorkCompleted?.Invoke();
        
        StartCoroutine(AutoClearWorkArea(3f));
    }
    
    private System.Collections.IEnumerator AutoClearWorkArea(float delay)
    {
        yield return new WaitForSeconds(delay);
        ClearWorkAreaVisualization();
    }
    
    #endregion
    
    #region Gizmos
    
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

        if (previewOptimization && optimizedOrder.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < optimizedOrder.Count - 1; i++)
            {
                Gizmos.DrawLine(optimizedOrder[i], optimizedOrder[i + 1]);
            }
        }
    }
    
    #endregion
}
