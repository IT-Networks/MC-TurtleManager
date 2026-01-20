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
    public ColumnBasedMiningOptimizer columnOptimizer;

    private TurtleBaseManager baseManager;
    private TurtleMovementManager movementManager;
    private TurtleOperationManager operationManager;

    // Mining state
    private Vector3 currentMiningTarget = Vector3.zero;
    private bool isPositioningForMining = false;
    private Vector3 lastMiningPosition = Vector3.zero;

    private void Start()
    {
        baseManager = FindFirstObjectByType<TurtleBaseManager>();
        movementManager = FindFirstObjectByType<TurtleMovementManager>();
        operationManager = FindFirstObjectByType<TurtleOperationManager>();

        if (columnOptimizer == null)
            columnOptimizer = FindFirstObjectByType<ColumnBasedMiningOptimizer>();
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

        // Filter out empty positions first
        var solidBlocks = FilterSolidBlocks(blockPositions);

        if (solidBlocks.Count == 0)
        {
            Debug.LogWarning("No solid blocks to mine in selection");
            return;
        }

        Debug.Log($"Filtered selection: {solidBlocks.Count} solid blocks from {blockPositions.Count} total positions");

        var optimizedBlocks = solidBlocks;

        // Use column-based optimizer
        if (enableMiningOptimization && columnOptimizer != null)
        {
            Vector3 turtlePos = baseManager.GetTurtlePosition();
            var columnPlan = columnOptimizer.OptimizeMining(solidBlocks, turtlePos);
            optimizedBlocks = columnPlan.optimizedBlockOrder;

            Debug.Log($"Column-based mining plan: {columnPlan.totalColumns} columns, {columnPlan.totalBlocks} blocks");
        }

        if (optimizedBlocks.Count == 0)
        {
            Debug.LogWarning("No valid blocks to mine after optimization");
            return;
        }

        operationManager.StartOperation(TurtleOperationManager.OperationType.Mining, optimizedBlocks.Count);
        StartCoroutine(ExecuteMiningOperation(optimizedBlocks));
    }

    /// <summary>
    /// Filter out air/empty blocks from selection
    /// </summary>
    private List<Vector3> FilterSolidBlocks(List<Vector3> blockPositions)
    {
        var solidBlocks = new List<Vector3>();

        foreach (var pos in blockPositions)
        {
            if (IsBlockMineable(pos))
            {
                solidBlocks.Add(pos);
            }
        }

        return solidBlocks;
    }

    /// <summary>
    /// Execute mining operation with optimized column-aware positioning
    /// </summary>
    private IEnumerator ExecuteMiningOperation(List<Vector3> blocks)
    {
        Debug.Log($"Starting mining operation: {blocks.Count} blocks");

        Vector3 lastBlockPos = Vector3.zero;
        bool needsRepositioning = true;

        for (int i = 0; i < blocks.Count; i++)
        {
            Vector3 blockPos = blocks[i];

            if (validateBlocksBeforeMining && !ShouldMineBlock(blockPos))
            {
                Debug.Log($"Skipping block at {blockPos} - validation failed");
                operationManager.IncrementSkipped();
                continue;
            }

            // Check if we need to reposition (optimize for column mining)
            if (i > 0 && columnOptimizer != null && columnOptimizer.optimizePathfinding)
            {
                Vector3 turtlePos = baseManager.GetTurtlePosition();
                needsRepositioning = columnOptimizer.NeedsRepositioning(lastBlockPos, blockPos, turtlePos);

                if (!needsRepositioning)
                {
                    Debug.Log($"Mining block {i + 1}/{blocks.Count} in same column - no repositioning needed");
                }
            }

            if (needsRepositioning)
            {
                yield return StartCoroutine(MineBlockWithPositioning(blockPos));
            }
            else
            {
                // Mine directly without repositioning (already adjacent)
                yield return StartCoroutine(ExecuteMiningAction(blockPos));
            }

            lastBlockPos = blockPos;
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

        // Check if block is actually solid
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
    /// Optimize mining order using column-based approach
    /// </summary>
    public List<Vector3> OptimizeMiningOrder(List<Vector3> blockPositions)
    {
        if (!enableMiningOptimization || columnOptimizer == null)
            return blockPositions;

        // Filter solid blocks first
        var solidBlocks = FilterSolidBlocks(blockPositions);

        if (solidBlocks.Count == 0)
            return new List<Vector3>();

        Vector3 turtlePos = baseManager.GetTurtlePosition();
        var columnPlan = columnOptimizer.OptimizeMining(solidBlocks, turtlePos);

        return columnPlan.optimizedBlockOrder;
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