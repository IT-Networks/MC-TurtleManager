using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main controller that coordinates all turtle subsystems and provides unified API
/// </summary>
public class TurtleMainController : MonoBehaviour
{
    [Header("Component References")]
    public TurtleBaseManager baseManager;
    public TurtleMovementManager movementManager;
    public TurtleMiningManager miningManager;
    public TurtleBuildingManager buildingManager;
    public TurtleOperationManager operationManager;
    public TurtleWorldManager worldManager;

    [Header("Integration Settings")]
    public bool autoFindComponents = true;
    public bool enableIntegratedLogging = true;

    // Unified events
    public System.Action<TurtleOperationManager.OperationType> OnOperationStarted;
    public System.Action<TurtleOperationManager.OperationType, OperationStats> OnOperationCompleted;
    public System.Action<OperationStats> OnProgressUpdate;

    private void Start()
    {
        InitializeComponents();
        SetupEventIntegration();
        
        if (enableIntegratedLogging)
        {
            Debug.Log("TurtleMainController initialized - All systems ready");
        }
    }

    #region Initialization

    private void InitializeComponents()
    {
        if (autoFindComponents)
        {
            FindMissingComponents();
        }

        ValidateComponents();
    }

    private void FindMissingComponents()
    {
        if (baseManager == null) baseManager = GetComponent<TurtleBaseManager>();
        if (movementManager == null) movementManager = GetComponent<TurtleMovementManager>();
        if (miningManager == null) miningManager = GetComponent<TurtleMiningManager>();
        if (buildingManager == null) buildingManager = GetComponent<TurtleBuildingManager>();
        if (operationManager == null) operationManager = GetComponent<TurtleOperationManager>();
        if (worldManager == null) worldManager = FindFirstObjectByType<TurtleWorldManager>();

        // Try to find in children if not found
        if (baseManager == null) baseManager = GetComponentInChildren<TurtleBaseManager>();
        if (movementManager == null) movementManager = GetComponentInChildren<TurtleMovementManager>();
        if (miningManager == null) miningManager = GetComponentInChildren<TurtleMiningManager>();
        if (buildingManager == null) buildingManager = GetComponentInChildren<TurtleBuildingManager>();
        if (operationManager == null) operationManager = GetComponentInChildren<TurtleOperationManager>();
    }

    private void ValidateComponents()
    {
        if (baseManager == null) Debug.LogError("TurtleBaseManager not found!");
        if (movementManager == null) Debug.LogError("TurtleMovementManager not found!");
        if (miningManager == null) Debug.LogWarning("TurtleMiningManager not found - mining operations disabled");
        if (buildingManager == null) Debug.LogWarning("TurtleBuildingManager not found - building operations disabled");
        if (operationManager == null) Debug.LogError("TurtleOperationManager not found!");
    }

    private void SetupEventIntegration()
    {
        if (operationManager != null)
        {
            operationManager.OnOperationStarted += (type) => OnOperationStarted?.Invoke(type);
            operationManager.OnOperationCompleted += (type, stats) => OnOperationCompleted?.Invoke(type, stats);
            operationManager.OnProgressUpdate += (stats) => OnProgressUpdate?.Invoke(stats);
        }
    }

    #endregion

    #region Public Mining API

    /// <summary>
    /// Start mining operation with block positions
    /// </summary>
    public void StartMiningOperation(List<Vector3> blockPositions)
    {
        if (miningManager == null)
        {
            Debug.LogError("Cannot start mining - TurtleMiningManager not available");
            return;
        }

        if (!IsReady())
        {
            Debug.LogWarning("Cannot start mining - turtle system not ready");
            return;
        }

        miningManager.StartMiningOperation(blockPositions);
    }

    /// <summary>
    /// Filter solid blocks from selection (for visualization)
    /// </summary>
    public List<Vector3> ValidateMiningBlocks(List<Vector3> blockPositions)
    {
        if (worldManager == null) return blockPositions;

        var solidBlocks = new List<Vector3>();

        foreach (var pos in blockPositions)
        {
            var chunk = worldManager.GetChunkContaining(pos);
            if (chunk == null || !chunk.IsLoaded) continue;

            var chunkInfo = chunk.GetChunkInfo();
            if (chunkInfo == null) continue;

            var blockType = chunkInfo.GetBlockTypeAt(pos);
            if (string.IsNullOrEmpty(blockType)) continue;

            string lower = blockType.ToLowerInvariant();
            bool isAir = lower.Contains("air") || lower.Equals("minecraft:air");

            if (!isAir)
            {
                solidBlocks.Add(pos);
            }
        }

        return solidBlocks;
    }

    /// <summary>
    /// Optimize mining order
    /// </summary>
    public List<Vector3> OptimizeMiningOrder(List<Vector3> blockPositions)
    {
        if (miningManager == null) return blockPositions;

        return miningManager.OptimizeMiningOrder(blockPositions);
    }

    #endregion

    #region Public Building API

    /// <summary>
    /// Start building operation with structure
    /// </summary>
    public void StartBuildingOperation(Vector3 buildOrigin, StructureData structure)
    {
        if (buildingManager == null)
        {
            Debug.LogError("Cannot start building - TurtleBuildingManager not available");
            return;
        }

        if (!IsReady())
        {
            Debug.LogWarning("Cannot start building - turtle system not ready");
            return;
        }

        buildingManager.StartBuildingOperation(buildOrigin, structure);
    }
    /// <summary>
    /// Check if block can be placed at position
    /// </summary>
    public bool CanPlaceBlockAt(Vector3 position)
    {
        if (buildingManager == null) return false;

        return buildingManager.CanPlaceBlockAt(position);
    }

    #endregion

    #region Public Movement API

    /// <summary>
    /// Move turtle to specific position
    /// </summary>
    public void MoveTurtleToPosition(Vector3 targetPosition, System.Action onComplete = null)
    {
        if (movementManager == null)
        {
            Debug.LogError("Cannot move turtle - TurtleMovementManager not available");
            return;
        }

        StartCoroutine(MoveTurtleCoroutine(targetPosition, onComplete));
    }

    private System.Collections.IEnumerator MoveTurtleCoroutine(Vector3 targetPosition, System.Action onComplete)
    {
        yield return StartCoroutine(movementManager.MoveTurtleToExactPosition(targetPosition));
        onComplete?.Invoke();
    }

    #endregion

    #region Operation Control

    /// <summary>
    /// Cancel current operation
    /// </summary>
    public void CancelCurrentOperation()
    {
        if (operationManager != null)
        {
            operationManager.CancelOperation();
        }

        if (miningManager != null) miningManager.CancelMining();
        if (buildingManager != null) buildingManager.CancelBuilding();
        if (movementManager != null) movementManager.CancelMovement();
        if (baseManager != null) baseManager.ClearCommandQueue();

        Debug.Log("All operations cancelled");
    }

    /// <summary>
    /// Emergency stop all operations
    /// </summary>
    public void EmergencyStop()
    {
        if (baseManager != null) baseManager.EmergencyStop();
        
        CancelCurrentOperation();

        Debug.Log("Emergency stop executed - all systems halted");
    }

    #endregion

    #region Status and Information

    /// <summary>
    /// Check if turtle system is ready for operations
    /// </summary>
    public bool IsReady()
    {
        return baseManager != null && 
               baseManager.GetCurrentStatus() != null && 
               !IsBusy();
    }

    /// <summary>
    /// Check if any operation is currently running
    /// </summary>
    public bool IsBusy()
    {
        if (baseManager?.IsBusy == true) return true;
        if (operationManager?.IsOperationActive == true) return true;
        
        return false;
    }

    /// <summary>
    /// Get current turtle position
    /// </summary>
    public Vector3 GetTurtlePosition()
    {
        return baseManager?.GetTurtlePosition() ?? Vector3.zero;
    }

    /// <summary>
    /// Get current turtle status
    /// </summary>
    public TurtleStatus GetTurtleStatus()
    {
        var baseStatus = baseManager?.GetCurrentStatus();
        if (baseStatus == null)
            return new TurtleStatus();

        // Convert TurtleBaseStatus to TurtleStatus
        return new TurtleStatus
        {
            label = baseStatus.label,
            direction = baseStatus.direction,
            position = new Vector3Int(
                (int)baseStatus.position.x,
                (int)baseStatus.position.y,
                (int)baseStatus.position.z
            ),
            fuel = baseStatus.fuelLevel,
            status = baseStatus.isBusy ? "busy" : "idle"
        };
    }

    /// <summary>
    /// Get current operation type
    /// </summary>
    public TurtleOperationManager.OperationType GetCurrentOperation()
    {
        return operationManager?.CurrentOperation ?? TurtleOperationManager.OperationType.None;
    }

    /// <summary>
    /// Get operation progress (0.0 to 1.0)
    /// </summary>
    public float GetOperationProgress()
    {
        return operationManager?.Progress ?? 0f;
    }

    /// <summary>
    /// Get estimated time remaining in seconds
    /// </summary>
    public float GetEstimatedTimeRemaining()
    {
        return operationManager?.EstimatedTimeRemaining ?? 0f;
    }

    /// <summary>
    /// Get detailed operation summary
    /// </summary>
    public string GetOperationSummary()
    {
        if (operationManager == null) return "Operation manager not available";
        
        return operationManager.GetOperationSummary();
    }

    /// <summary>
    /// Get compact status string
    /// </summary>
    public string GetStatusString()
    {
        if (!IsReady()) return "Not Ready";
        if (!IsBusy()) return "Idle";
        
        return operationManager?.GetProgressString() ?? "Working";
    }

    #endregion

    #region System Information

    /// <summary>
    /// Get system health report
    /// </summary>
    public string GetSystemHealthReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== TURTLE SYSTEM HEALTH ===");
        
        report.AppendLine($"Base Manager: {(baseManager != null ? "OK" : "MISSING")}");
        report.AppendLine($"Movement Manager: {(movementManager != null ? "OK" : "MISSING")}");
        report.AppendLine($"Mining Manager: {(miningManager != null ? "OK" : "MISSING")}");
        report.AppendLine($"Building Manager: {(buildingManager != null ? "OK" : "MISSING")}");
        report.AppendLine($"Operation Manager: {(operationManager != null ? "OK" : "MISSING")}");
        report.AppendLine($"World Manager: {(worldManager != null ? "OK" : "MISSING")}");
        
        report.AppendLine();
        report.AppendLine($"System Ready: {IsReady()}");
        report.AppendLine($"Currently Busy: {IsBusy()}");
        report.AppendLine($"Turtle Position: {GetTurtlePosition()}");
        report.AppendLine($"Current Operation: {GetCurrentOperation()}");
        
        if (operationManager?.CurrentStats != null)
        {
            report.AppendLine();
            report.AppendLine("Current Operation Stats:");
            report.AppendLine(operationManager.CurrentStats.ToString());
        }

        return report.ToString();
    }

    /// <summary>
    /// Get available capabilities
    /// </summary>
    public Dictionary<string, bool> GetCapabilities()
    {
        return new Dictionary<string, bool>
        {
            ["Mining"] = miningManager != null,
            ["Building"] = buildingManager != null,
            ["Movement"] = movementManager != null,
            ["World Manager"] = worldManager != null,
            ["Status Tracking"] = baseManager != null,
            ["Operation Management"] = operationManager != null
        };
    }

    #endregion

    private void OnDestroy()
    {
        CancelCurrentOperation();
    }
}

#region Integration Extensions

/// <summary>
/// Extension methods for easier integration with existing systems
/// </summary>
public static class TurtleMainControllerExtensions
{
    /// <summary>
    /// Start mining with already validated blocks (validation handled by AreaSelectionManager)
    /// </summary>
    public static void StartOptimizedMining(this TurtleMainController controller, List<Vector3> blocks)
    {
        // Blocks are already validated by AreaSelectionManager - skip redundant validation
        var optimizedBlocks = controller.OptimizeMiningOrder(blocks);
        controller.StartMiningOperation(optimizedBlocks);
    }

    /// <summary>
    /// Check system readiness and log issues
    /// </summary>
    public static bool CheckSystemReadiness(this TurtleMainController controller)
    {
        bool ready = controller.IsReady();
        
        if (!ready)
        {
            Debug.LogWarning("Turtle system not ready:");
            Debug.LogWarning(controller.GetSystemHealthReport());
        }
        
        return ready;
    }

    /// <summary>
    /// Start building with automatic validation
    /// </summary>
    public static void StartValidatedBuilding(this TurtleMainController controller, Vector3 buildOrigin, StructureData structure)
    {
        if (!controller.CheckSystemReadiness())
        {
            Debug.LogError("Cannot start building - system not ready");
            return;
        }

        // Validate build area
        bool canBuild = true;
        foreach (var block in structure.blocks)
        {
            Vector3 worldPos = buildOrigin + (Vector3)block.relativePosition;
            if (!controller.CanPlaceBlockAt(worldPos))
            {
                Debug.LogWarning($"Cannot place block at {worldPos} - position occupied or invalid");
                canBuild = false;
                break;
            }
        }

        if (canBuild)
        {
            controller.StartBuildingOperation(buildOrigin, structure);
        }
        else
        {
            Debug.LogError("Building validation failed - cannot place structure at specified location");
        }
    }

    /// <summary>
    /// Move turtle with callback and error handling
    /// </summary>
    public static void MoveTurtleWithCallback(this TurtleMainController controller, Vector3 target, 
        System.Action onSuccess = null, System.Action<string> onError = null)
    {
        if (!controller.CheckSystemReadiness())
        {
            onError?.Invoke("System not ready");
            return;
        }

        controller.MoveTurtleToPosition(target, () => {
            // Check if we actually reached the target
            float distance = Vector3.Distance(controller.GetTurtlePosition(), target);
            if (distance <= 0.5f) // Allow some tolerance
            {
                onSuccess?.Invoke();
            }
            else
            {
                onError?.Invoke($"Failed to reach target. Distance: {distance:F2}");
            }
        });
    }

    /// <summary>
    /// Get mining report for a list of blocks
    /// </summary>
    public static string GetMiningReport(this TurtleMainController controller, List<Vector3> blocks)
    {
        if (controller.worldManager == null)
            return "World manager not available";

        Vector3 turtlePos = controller.GetTurtlePosition();
        var validBlocks = controller.ValidateMiningBlocks(blocks);

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== MINING REPORT ===");
        report.AppendLine($"Total Selected: {blocks.Count}");
        report.AppendLine($"Solid Blocks: {validBlocks.Count}");
        report.AppendLine($"Empty Blocks: {blocks.Count - validBlocks.Count}");
        report.AppendLine($"Turtle Position: {turtlePos}");

        if (validBlocks.Count > 0)
        {
            float avgDistance = validBlocks.Average(b => Vector3.Distance(b, turtlePos));
            report.AppendLine($"Average Distance: {avgDistance:F1}");
        }

        return report.ToString();
    }

    /// <summary>
    /// Execute emergency procedures with full system recovery
    /// </summary>
    public static void ExecuteEmergencyRecovery(this TurtleMainController controller)
    {
        Debug.LogWarning("Executing emergency recovery procedures...");
        
        controller.EmergencyStop();
        
        // Wait for systems to stabilize
        controller.StartCoroutine(DelayedSystemRestart(controller));
    }

    private static System.Collections.IEnumerator DelayedSystemRestart(TurtleMainController controller)
    {
        yield return new WaitForSeconds(2f);
        
        // Restart essential systems
        if (controller.baseManager != null)
        {
            controller.baseManager.EmergencyStop(); // This restarts core coroutines
        }
        
        Debug.Log("Emergency recovery completed");
    }

    /// <summary>
    /// Get comprehensive status for UI display
    /// </summary>
    public static TurtleSystemStatus GetComprehensiveStatus(this TurtleMainController controller)
    {
        return new TurtleSystemStatus
        {
            isReady = controller.IsReady(),
            isBusy = controller.IsBusy(),
            position = controller.GetTurtlePosition(),
            currentOperation = controller.GetCurrentOperation(),
            progress = controller.GetOperationProgress(),
            estimatedTimeRemaining = controller.GetEstimatedTimeRemaining(),
            statusString = controller.GetStatusString(),
            capabilities = controller.GetCapabilities(),
            queuedCommands = controller.baseManager?.QueuedCommands ?? 0
        };
    }

    /// <summary>
    /// Validate and prepare mining operation
    /// </summary>
    public static MiningOperationPlan PrepareMiningOperation(this TurtleMainController controller, List<Vector3> blocks)
    {
        var plan = new MiningOperationPlan();
        
        if (!controller.CheckSystemReadiness())
        {
            plan.isValid = false;
            plan.errorMessage = "System not ready";
            return plan;
        }

        Vector3 turtlePos = controller.GetTurtlePosition();
        
        plan.originalBlocks = new List<Vector3>(blocks);
        plan.validBlocks = controller.ValidateMiningBlocks(blocks);
        plan.optimizedBlocks = controller.OptimizeMiningOrder(plan.validBlocks);
        
        plan.totalBlocks = blocks.Count;
        plan.validBlockCount = plan.validBlocks.Count;
        plan.skippedBlocks = plan.totalBlocks - plan.validBlockCount;
        
        plan.estimatedDistance = CalculatePathDistance(plan.optimizedBlocks, turtlePos);
        plan.estimatedTime = EstimateMiningTime(plan.optimizedBlocks);
        
        plan.isValid = plan.validBlockCount > 0;
        plan.errorMessage = plan.isValid ? null : "No valid blocks to mine";
        
        return plan;
    }

    private static float CalculatePathDistance(List<Vector3> blocks, Vector3 startPos)
    {
        if (blocks.Count == 0) return 0f;
        
        float distance = Vector3.Distance(startPos, blocks[0]);
        for (int i = 1; i < blocks.Count; i++)
        {
            distance += Vector3.Distance(blocks[i-1], blocks[i]);
        }
        return distance;
    }

    private static float EstimateMiningTime(List<Vector3> blocks)
    {
        // Rough estimation: 3 seconds per block (movement + mining + delays)
        return blocks.Count * 3f;
    }
}

#region Supporting Data Structures

/// <summary>
/// Comprehensive system status for UI integration
/// </summary>
[System.Serializable]
public class TurtleSystemStatus
{
    public bool isReady;
    public bool isBusy;
    public Vector3 position;
    public TurtleOperationManager.OperationType currentOperation;
    public float progress;
    public float estimatedTimeRemaining;
    public string statusString;
    public Dictionary<string, bool> capabilities;
    public int queuedCommands;
}

/// <summary>
/// Mining operation planning data
/// </summary>
[System.Serializable]
public class MiningOperationPlan
{
    public bool isValid;
    public string errorMessage;
    
    public List<Vector3> originalBlocks;
    public List<Vector3> validBlocks;
    public List<Vector3> optimizedBlocks;
    
    public int totalBlocks;
    public int validBlockCount;
    public int skippedBlocks;
    
    public float estimatedDistance;
    public float estimatedTime;
    
    public float ValidBlockPercentage => totalBlocks > 0 ? (float)validBlockCount / totalBlocks * 100f : 0f;
    
    public override string ToString()
    {
        return $"Mining Plan: {validBlockCount}/{totalBlocks} valid blocks ({ValidBlockPercentage:F1}%), " +
               $"Distance: {estimatedDistance:F1}, Time: {estimatedTime:F1}s";
    }
}

#endregion

#endregion
