using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Specialized manager for mining operations with precise block positioning
/// </summary>
public class TurtleMiningManager : MonoBehaviour
{
    [Header("Mining Settings")]
    public bool enableMiningOptimization = true;
    public bool validateBlocksBeforeMining = true;
    public float blockValidationRadius = 50f;
    public float miningPositionTolerance = 0.1f;

    [Header("References")]
    public MiningBlockValidator blockValidator;

    private TurtleBaseManager baseManager;
    private TurtleMovementManager movementManager;
    private TurtleOperationManager operationManager;
    
    // Mining state
    private Vector3 currentMiningTarget = Vector3.zero;
    private bool isPositioningForMining = false;

    private void Start()
    {
        baseManager = FindFirstObjectByType<TurtleBaseManager>();
        movementManager = FindFirstObjectByType<TurtleMovementManager>();
        operationManager = FindFirstObjectByType<TurtleOperationManager>();
        
        if (blockValidator == null)
            blockValidator = FindFirstObjectByType<MiningBlockValidator>();
    }

    #region Mining Operations

    /// <summary>
    /// Start mining operation with list of block positions
    /// </summary>
    public void StartMiningOperation(List<Vector3> blockPositions)
    {
        if (operationManager.CurrentOperation != TurtleOperationManager.OperationType.None)
        {
            Debug.LogWarning("Cannot start mining - turtle is busy");
            return;
        }

        var optimizedBlocks = blockPositions;
        
        if (enableMiningOptimization && blockValidator != null)
        {
            Vector3 turtlePos = baseManager.GetTurtlePosition();
            optimizedBlocks = blockValidator.ValidateBlocksForMining(blockPositions, turtlePos).validBlocks;
        }

        if (optimizedBlocks.Count == 0)
        {
            Debug.LogWarning("No valid blocks to mine");
            return;
        }

        operationManager.StartOperation(TurtleOperationManager.OperationType.Mining, optimizedBlocks.Count);
        StartCoroutine(ExecuteMiningOperation(optimizedBlocks));
    }

    /// <summary>
    /// Execute mining operation
    /// </summary>
    private IEnumerator ExecuteMiningOperation(List<Vector3> blocks)
    {
        Debug.Log($"Starting mining operation: {blocks.Count} blocks");

        foreach (Vector3 blockPos in blocks)
        {
            if (validateBlocksBeforeMining && !ShouldMineBlock(blockPos))
            {
                Debug.Log($"Skipping block at {blockPos} - validation failed");
                operationManager.IncrementSkipped();
                continue;
            }

            yield return StartCoroutine(MineBlockWithPositioning(blockPos));

            operationManager.IncrementProcessed();
            yield return new WaitUntil(() => !baseManager.IsBusy);
            yield return new WaitForSeconds(1.5f);
        }

        operationManager.CompleteOperation();
        Debug.Log("Mining operation completed");
    }

    /// <summary>
    /// Mine a single block with proper positioning
    /// </summary>
    public IEnumerator MineBlockWithPositioning(Vector3 blockPosition)
    {
        
        currentMiningTarget = blockPosition;
        isPositioningForMining = true;

    Debug.Log($"=== MINING DEBUG ===");
    Debug.Log($"Target Block: {blockPosition}");
    Debug.Log($"Current Turtle Position: {baseManager.GetTurtlePosition()}");

        Debug.Log($"Mining block at {blockPosition}");

        // Find best mining position
        Vector3 miningPosition = movementManager.GetBestAdjacentPosition(blockPosition);
        
        if (miningPosition == Vector3.zero)
        {
            Debug.LogWarning($"No valid mining position found for block at {blockPosition}");
            operationManager.IncrementFailed();
            yield break;
        }

        Debug.Log($"Moving from {baseManager.GetTurtlePosition()} to {miningPosition}");
        // Move to mining position
        yield return StartCoroutine(movementManager.MoveTurtleToExactPosition(miningPosition));

        Vector3 actualPos = baseManager.GetTurtlePosition();
        Debug.Log($"Actual position after movement: {actualPos}");
        Debug.Log($"Distance to target mining position: {Vector3.Distance(actualPos, miningPosition)}");
    

        // Verify position
        Vector3 currentPos = baseManager.GetTurtlePosition();
        if (Vector3.Distance(currentPos, miningPosition) > miningPositionTolerance)
        {
            Debug.LogWarning($"Failed to reach mining position. Current: {currentPos}, Target: {miningPosition}");
            operationManager.IncrementFailed();
            yield break;
        }

        // Execute mining action
        yield return StartCoroutine(ExecuteMiningAction(blockPosition));

        isPositioningForMining = false;
    }

    /// <summary>
    /// Execute the actual mining command based on block position
    /// </summary>
    private IEnumerator ExecuteMiningAction(Vector3 blockPosition)
    {
        Vector3 turtlePos = baseManager.GetTurtlePosition();
        Vector3 direction = blockPosition - turtlePos;

        string digCommand = "dig";
        string faceDirection = null;

        // Determine dig command based on relative position
        if (Mathf.Abs(direction.y) > 0.5f)
        {
            // Vertical mining
            if (direction.y > 0)
            {
                digCommand = "digup";
                Debug.Log($"Mining block above at {blockPosition}");
            }
            else
            {
                digCommand = "digdown";
                Debug.Log($"Mining block below at {blockPosition}");
            }
        }
        else
        {
            // Horizontal mining - need to face the block first
            faceDirection = movementManager.GetDirectionFromVector(direction);
            Debug.Log($"Mining block in direction {faceDirection} at {blockPosition}");
        }

        // Face correct direction if needed
        if (!string.IsNullOrEmpty(faceDirection))
        {
            yield return StartCoroutine(movementManager.FaceDirection(faceDirection));
        }

        // Execute dig command
        var digCmd = new TurtleCommand(digCommand, baseManager.defaultTurtleId)
        {
            targetPosition = blockPosition,
            requiresPositioning = false
        };

        baseManager.QueueCommand(digCmd);

        // Wait for command execution
        yield return new WaitUntil(() => !baseManager.IsBusy);
        //Delete Block in ChunkInfo
        var chunk = baseManager.worldManager?.GetChunkContaining(blockPosition);
        chunk?.GetChunkInfo()?.RemoveBlockAt(blockPosition);
        baseManager.worldManager?.RemoveBlockAtWorldPosition(blockPosition);
    }

    #endregion

    #region Block Validation

    /// <summary>
    /// Check if block should be mined
    /// </summary>
    private bool ShouldMineBlock(Vector3 blockPosition)
    {
        // Distance check
        Vector3 turtlePos = baseManager.GetTurtlePosition();
        float distance = Vector3.Distance(turtlePos, blockPosition);
        
        if (distance > blockValidationRadius)
        {
            Debug.LogWarning($"Block at {blockPosition} too far from turtle ({distance:F1} > {blockValidationRadius})");
            return false;
        }

        // Use validator if available
        if (blockValidator != null)
        {
            var result = blockValidator.ValidateBlocksForMining(blockPosition, turtlePos);
            return result.validBlocks.Count > 0;
        }

        // Fallback validation
        return IsBlockMineable(blockPosition);
    }

    private bool IsBlockMineable(Vector3 blockPosition)
    {
        var chunk = baseManager.worldManager?.GetChunkContaining(blockPosition);
        if (chunk == null || !chunk.IsLoaded) return false;

        var chunkInfo = chunk.GetChunkInfo();
        if (chunkInfo == null) return false;

        var blockType = chunkInfo.GetBlockTypeAt(blockPosition);
        return !string.IsNullOrEmpty(blockType) && !IsAirBlock(blockType);
    }

    private bool IsAirBlock(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return true;
        string lower = blockType.ToLowerInvariant();
        return lower.Contains("air") || lower.Equals("minecraft:air");
    }

    #endregion

    #region Optimization

    /// <summary>
    /// Validate mining blocks using validator
    /// </summary>
    public List<Vector3> ValidateMiningBlocks(List<Vector3> blockPositions)
    {
        if (!validateBlocksBeforeMining || blockValidator == null)
            return new List<Vector3>(blockPositions);

        Vector3 turtlePos = baseManager.GetTurtlePosition();
        return blockValidator.ValidateBlocksForMining(blockPositions, turtlePos).validBlocks;
    }

    /// <summary>
    /// Optimize mining order based on turtle position
    /// </summary>
    public List<Vector3> OptimizeMiningOrder(List<Vector3> blockPositions)
    {
        if (!enableMiningOptimization) return blockPositions;

        Vector3 turtlePos = baseManager.GetTurtlePosition();
        
        var optimized = new List<Vector3>(blockPositions);
        optimized.Sort((a, b) => 
            Vector3.Distance(a, turtlePos).CompareTo(Vector3.Distance(b, turtlePos)));

        return optimized;
    }

    #endregion

    #region Public Properties

    public bool IsPositioningForMining => isPositioningForMining;
    public Vector3 CurrentMiningTarget => currentMiningTarget;

    #endregion

    public void CancelMining()
    {
        isPositioningForMining = false;
        currentMiningTarget = Vector3.zero;
        StopAllCoroutines();
    }
}