using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages mining operations. Groups blocks into vertical columns and mines them
/// top-down to minimize pathfinding calls. Uses A*/NavMesh pathfinding via
/// TurtleMovementManager for navigation between columns.
///
/// Blocks that can't be reached (all neighbors solid) are deferred and retried
/// after surrounding blocks have been mined, creating air spaces.
/// </summary>
public class TurtleMiningManager : MonoBehaviour
{
    [Header("Mining Settings")]
    public float miningPositionTolerance = 0.1f;
    public int maxRetryPasses = 3;

    private TurtleBaseManager baseManager;
    private TurtleMovementManager movementManager;
    private TurtleOperationManager operationManager;

    private bool isMining = false;

    private void Start()
    {
        baseManager = FindFirstObjectByType<TurtleBaseManager>();
        movementManager = FindFirstObjectByType<TurtleMovementManager>();
        operationManager = FindFirstObjectByType<TurtleOperationManager>();
    }

    #region Public API

    public bool IsMining => isMining;

    /// <summary>
    /// Start mining the given block positions. Blocks are grouped into columns
    /// and ordered by nearest-column to reduce turtle travel.
    /// </summary>
    public void StartMiningOperation(List<Vector3> blockPositions)
    {
        if (operationManager.CurrentOperation != TurtleOperationManager.OperationType.None)
        {
            Debug.LogWarning("Cannot start mining - turtle is busy");
            return;
        }

        if (blockPositions == null || blockPositions.Count == 0)
        {
            Debug.LogWarning("No blocks to mine");
            return;
        }

        var optimizedBlocks = OptimizeByColumns(blockPositions);

        if (optimizedBlocks.Count == 0)
        {
            Debug.LogWarning("No valid blocks to mine after optimization");
            return;
        }

        Debug.Log($"Mining {optimizedBlocks.Count} blocks in {CountColumns(blockPositions)} columns");
        operationManager.StartOperation(TurtleOperationManager.OperationType.Mining, optimizedBlocks.Count);
        StartCoroutine(ExecuteMiningOperation(optimizedBlocks));
    }

    public void CancelMining()
    {
        isMining = false;
        StopAllCoroutines();
    }

    #endregion

    #region Mining Execution

    private IEnumerator ExecuteMiningOperation(List<Vector3> blocks)
    {
        isMining = true;
        var remaining = new List<Vector3>(blocks);
        var minedChunks = new HashSet<Vector2Int>();

        // Pin all chunks that contain blocks to mine - prevents frustum-based
        // unloading when camera isn't looking at the mining area
        var pinnedChunks = new HashSet<Vector2Int>();
        var worldMgr = baseManager.worldManager;
        if (worldMgr != null)
        {
            foreach (var block in blocks)
                pinnedChunks.Add(worldMgr.WorldPositionToChunkCoord(block));
            worldMgr.PinChunks(pinnedChunks);
            Debug.Log($"Pinned {pinnedChunks.Count} chunks for mining operation");
        }

        // If the top-most blocks are completely enclosed (no air neighbors),
        // dig a vertical shaft down to the mining area to create access
        yield return StartCoroutine(DigShaftToAreaIfNeeded(remaining, minedChunks));

        for (int pass = 0; pass < maxRetryPasses && remaining.Count > 0; pass++)
        {
            if (pass > 0)
            {
                // Regenerate meshes for chunks modified in previous pass
                // so deferred blocks can see updated air positions
                RegenerateMinedChunks(minedChunks);
                minedChunks.Clear();

                remaining = OptimizeByColumns(remaining);
                Debug.Log($"Retry pass {pass + 1}: {remaining.Count} deferred blocks");
            }

            var deferred = new List<Vector3>();
            Vector3 lastBlock = Vector3.zero;
            bool skipColumn = false;

            for (int i = 0; i < remaining.Count; i++)
            {
                Vector3 blockPos = remaining[i];
                bool sameColumn = i > 0 && IsSameColumn(lastBlock, blockPos);

                // On column transition, check if any deferred blocks now have accessible neighbors
                if (!sameColumn && i > 0 && deferred.Count > 0)
                {
                    RetryDeferredBlocks(deferred, remaining, i);
                }

                // If first block of column was unreachable, defer rest of column too
                if (sameColumn && skipColumn)
                {
                    deferred.Add(blockPos);
                    continue;
                }

                skipColumn = false;

                if (sameColumn)
                {
                    // Column mining: turtle must move into the previously mined space
                    // before it can dig the next block below/above.
                    // digdown only reaches 1 block below, so we must descend step by step.
                    Vector3 turtlePos = baseManager.GetTurtlePosition();
                    bool isDirectlyAbove = Mathf.RoundToInt(turtlePos.x) == Mathf.RoundToInt(blockPos.x) &&
                                           Mathf.RoundToInt(turtlePos.z) == Mathf.RoundToInt(blockPos.z) &&
                                           turtlePos.y > blockPos.y + 0.5f;

                    if (isDirectlyAbove)
                    {
                        // Move down into previously mined space (now air)
                        baseManager.QueueCommand(new TurtleCommand("down", baseManager.defaultTurtleId));
                        yield return new WaitUntil(() => !baseManager.IsBusy);
                        yield return StartCoroutine(ExecuteDig(blockPos));
                    }
                    else
                    {
                        // Not directly above (e.g. approached from side) - need full navigation
                        Vector3 miningPos = movementManager.GetBestAdjacentPosition(blockPos);
                        if (miningPos == Vector3.zero)
                        {
                            deferred.Add(blockPos);
                            skipColumn = true;
                            continue;
                        }
                        yield return StartCoroutine(NavigateAndDig(blockPos, miningPos));
                    }
                }
                else
                {
                    Vector3 miningPos = movementManager.GetBestAdjacentPosition(blockPos);

                    if (miningPos == Vector3.zero)
                    {
                        deferred.Add(blockPos);
                        skipColumn = true;
                        continue;
                    }

                    yield return StartCoroutine(NavigateAndDig(blockPos, miningPos));
                }

                // Track which chunks were modified
                TrackMinedChunk(blockPos, minedChunks);

                lastBlock = blockPos;
                operationManager.IncrementProcessed();
                yield return new WaitUntil(() => !baseManager.IsBusy);
                yield return new WaitForSeconds(1.5f);
            }

            if (deferred.Count == 0)
                break;

            if (deferred.Count == remaining.Count)
            {
                Debug.LogWarning($"{deferred.Count} blocks unreachable - no adjacent air positions available");
                for (int j = 0; j < deferred.Count; j++)
                    operationManager.IncrementFailed();
                break;
            }

            remaining = deferred;
        }

        // Final mesh regeneration for all modified chunks
        RegenerateMinedChunks(minedChunks);

        // Unpin chunks now that mining is done
        worldMgr?.UnpinChunks(pinnedChunks);

        isMining = false;
        operationManager.CompleteOperation();
        Debug.Log("Mining operation completed");
    }

    /// <summary>
    /// Checks whether the highest blocks in the mining set are fully enclosed (no air neighbors).
    /// If so, digs a vertical shaft from the surface down to the mining area to create access.
    /// Picks the column in the mining set nearest to the turtle's current X,Z position.
    /// </summary>
    private IEnumerator DigShaftToAreaIfNeeded(List<Vector3> blocks, HashSet<Vector2Int> minedChunks)
    {
        if (blocks.Count == 0) yield break;

        // Find the highest Y among mining blocks
        float highestY = float.MinValue;
        foreach (var b in blocks)
        {
            if (b.y > highestY) highestY = b.y;
        }

        // Check if any block at the highest Y level has an accessible neighbor
        bool anyAccessible = false;
        foreach (var b in blocks)
        {
            if (Mathf.RoundToInt(b.y) == Mathf.RoundToInt(highestY) && HasAccessibleNeighbor(b))
            {
                anyAccessible = true;
                break;
            }
        }

        if (anyAccessible)
        {
            Debug.Log("Mining area has accessible blocks - no shaft needed");
            yield break;
        }

        Debug.Log("Mining area is fully enclosed - digging access shaft");

        // Pick the column (X,Z) in the mining set nearest to the turtle
        Vector3 turtlePos = baseManager.GetTurtlePosition();
        var turtleXZ = new Vector2(turtlePos.x, turtlePos.z);

        // Collect unique columns that contain the highest-Y blocks
        var topColumns = new Dictionary<Vector2Int, float>();
        foreach (var b in blocks)
        {
            var key = new Vector2Int(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.z));
            if (!topColumns.ContainsKey(key) || b.y > topColumns[key])
                topColumns[key] = b.y;
        }

        // Find nearest column to turtle
        Vector2Int bestCol = default;
        float bestDist = float.MaxValue;
        float bestTopY = highestY;
        foreach (var kvp in topColumns)
        {
            float dist = Vector2.Distance(new Vector2(kvp.Key.x, kvp.Key.y), turtleXZ);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestCol = kvp.Key;
                bestTopY = kvp.Value;
            }
        }

        int shaftX = bestCol.x;
        int shaftZ = bestCol.y; // Vector2Int.y is the Z coordinate
        int targetY = Mathf.RoundToInt(bestTopY);

        // Find the surface: scan upward from targetY to find the first air block
        var worldManager = baseManager.worldManager;
        if (worldManager == null) yield break;

        int surfaceY = targetY;
        for (int y = targetY; y < targetY + 128; y++)
        {
            var checkPos = new Vector3(shaftX, y, shaftZ);
            if (!IsBlockSolidAt(checkPos))
            {
                surfaceY = y;
                break;
            }
        }

        if (surfaceY <= targetY)
        {
            Debug.Log("Could not find surface above mining area - shaft not needed or area is already accessible");
            yield break;
        }

        Debug.Log($"Digging shaft at ({shaftX}, {shaftZ}) from Y={surfaceY} down to Y={targetY}");

        // Move turtle to the surface entry point of the shaft
        // Enable auto-excavation so the turtle can dig to the shaft location if needed
        bool prevAutoExcavation = movementManager.enableAutoExcavation;
        movementManager.enableAutoExcavation = true;

        Vector3 shaftEntry = new Vector3(shaftX, surfaceY, shaftZ);
        yield return StartCoroutine(movementManager.MoveTurtleToExactPosition(shaftEntry));

        // Dig down step by step: always dig then move (more robust than checking each block)
        for (int y = surfaceY - 1; y >= targetY; y--)
        {
            Vector3 blockBelow = new Vector3(shaftX, y, shaftZ);

            // Always dig — if the position is already air, digdown is harmless
            baseManager.QueueCommand(new TurtleCommand("digdown", baseManager.defaultTurtleId));
            yield return new WaitUntil(() => !baseManager.IsBusy);

            // Update world state with immediate visual update AND proper event firing
            // CRITICAL FIX: Use RemoveBlockAndRegenerate instead of RemoveBlockFromData + RegenerateMesh
            // This ensures:
            // 1. Block disappears visually (immediate)
            // 2. OnBlockRemoved event fires (pathfinder updates)
            // 3. OnChunkRegenerated event fires (NavMesh updates)
            var chunk = worldManager.GetChunkContaining(blockBelow);
            chunk?.RemoveBlockAndRegenerate(blockBelow, 10000);

            TrackMinedChunk(blockBelow, minedChunks);

            // Move down into the cleared space
            baseManager.QueueCommand(new TurtleCommand("down", baseManager.defaultTurtleId));
            yield return new WaitUntil(() => !baseManager.IsBusy);
        }

        // Restore previous auto-excavation setting
        movementManager.enableAutoExcavation = prevAutoExcavation;

        // Regenerate meshes for shaft chunks so pathfinding sees the new air
        RegenerateMinedChunks(minedChunks);
        minedChunks.Clear();

        Debug.Log($"Access shaft complete - turtle at ({shaftX}, {targetY}, {shaftZ})");
    }

    private bool IsBlockSolidAt(Vector3 position)
    {
        var worldManager = baseManager.worldManager;
        if (worldManager == null) return false;

        var chunk = worldManager.GetChunkContaining(position);
        if (chunk == null || !chunk.IsLoaded) return false;

        // Use ChunkMeshData (source of truth) instead of ChunkInfo which may be incomplete
        return chunk.HasBlockAtWorld(position);
    }

    private IEnumerator NavigateAndDig(Vector3 blockPosition, Vector3 miningPosition)
    {
        yield return StartCoroutine(movementManager.MoveTurtleToExactPosition(miningPosition));

        Vector3 currentPos = baseManager.GetTurtlePosition();
        if (Vector3.Distance(currentPos, miningPosition) > miningPositionTolerance)
        {
            Debug.LogWarning($"Failed to reach mining position for {blockPosition}");
            operationManager.IncrementFailed();
            yield break;
        }

        yield return StartCoroutine(ExecuteDig(blockPosition));
    }

    private IEnumerator ExecuteDig(Vector3 blockPosition)
    {
        Vector3 turtlePos = baseManager.GetTurtlePosition();
        Vector3 direction = blockPosition - turtlePos;

        string digCommand = "dig";

        if (Mathf.Abs(direction.y) > 0.5f)
        {
            digCommand = direction.y > 0 ? "digup" : "digdown";
        }
        else
        {
            string faceDir = movementManager.GetDirectionFromVector(direction);
            yield return StartCoroutine(movementManager.FaceDirection(faceDir));
        }

        var cmd = new TurtleCommand(digCommand, baseManager.defaultTurtleId)
        {
            targetPosition = blockPosition,
            requiresPositioning = false
        };
        baseManager.QueueCommand(cmd);

        yield return new WaitUntil(() => !baseManager.IsBusy);

        // Update world state: remove block from both ChunkMeshData and ChunkInfo
        // Update world state with immediate visual update AND proper event firing
        // CRITICAL FIX: Use RemoveBlockAndRegenerate to trigger OnBlockRemoved and OnChunkRegenerated events
        // This ensures:
        // 1. Block disappears visually (immediate)
        // 2. OnBlockRemoved event fires (pathfinder cache invalidates)
        // 3. OnChunkRegenerated event fires (NavMesh updates)
        var worldManager = baseManager.worldManager;
        if (worldManager != null)
        {
            var chunk = worldManager.GetChunkContaining(blockPosition);
            chunk?.RemoveBlockAndRegenerate(blockPosition, 10000);
        }
    }

    private void TrackMinedChunk(Vector3 blockPosition, HashSet<Vector2Int> minedChunks)
    {
        var worldManager = baseManager.worldManager;
        if (worldManager != null)
        {
            minedChunks.Add(worldManager.WorldPositionToChunkCoord(blockPosition));
        }
    }

    private void RegenerateMinedChunks(HashSet<Vector2Int> chunkCoords)
    {
        var worldManager = baseManager.worldManager;
        if (worldManager == null) return;

        foreach (var coord in chunkCoords)
        {
            var chunk = worldManager.GetChunkManager(coord);
            if (chunk != null && chunk.IsLoaded)
            {
                chunk.RegenerateMesh();
            }
        }
    }

    /// <summary>
    /// Checks deferred blocks for newly accessible neighbors (from just-mined blocks
    /// creating air). Moves matches back into the remaining work list.
    /// </summary>
    private void RetryDeferredBlocks(List<Vector3> deferred, List<Vector3> remaining, int insertIndex)
    {
        var recovered = new List<Vector3>();

        for (int j = deferred.Count - 1; j >= 0; j--)
        {
            if (HasAccessibleNeighbor(deferred[j]))
            {
                recovered.Add(deferred[j]);
                deferred.RemoveAt(j);
            }
        }

        if (recovered.Count > 0)
        {
            // Re-sort recovered blocks into columns (top-down)
            recovered.Sort((a, b) => b.y.CompareTo(a.y));
            remaining.InsertRange(insertIndex, recovered);
            Debug.Log($"Intra-pass retry: {recovered.Count} deferred blocks now reachable");
        }
    }

    private bool HasAccessibleNeighbor(Vector3 blockPos)
    {
        Vector3[] neighbors =
        {
            blockPos + Vector3.up,
            blockPos + Vector3.down,
            blockPos + Vector3.right,
            blockPos + Vector3.left,
            blockPos + Vector3.forward,
            blockPos + Vector3.back
        };

        var worldManager = baseManager.worldManager;
        if (worldManager == null) return false;

        foreach (var neighbor in neighbors)
        {
            var chunk = worldManager.GetChunkContaining(neighbor);
            if (chunk == null || !chunk.IsLoaded) continue;

            // Use ChunkMeshData (source of truth) — no block = air = accessible
            if (!chunk.HasBlockAtWorld(neighbor))
                return true;
        }

        return false;
    }

    #endregion

    #region Column Optimization

    /// <summary>
    /// Groups blocks into vertical columns (same X,Z), sorts columns by nearest
    /// to turtle, and orders blocks top-down within each column.
    /// </summary>
    private List<Vector3> OptimizeByColumns(List<Vector3> blocks)
    {
        // Group blocks by their X,Z position into columns
        var columns = new Dictionary<Vector2Int, List<Vector3>>();

        foreach (var block in blocks)
        {
            var key = new Vector2Int(Mathf.RoundToInt(block.x), Mathf.RoundToInt(block.z));
            if (!columns.ContainsKey(key))
                columns[key] = new List<Vector3>();
            columns[key].Add(block);
        }

        // Sort blocks within each column: top-down (highest Y first)
        foreach (var col in columns.Values)
            col.Sort((a, b) => b.y.CompareTo(a.y));

        // Sort columns by nearest to turtle (greedy nearest-neighbor)
        Vector3 turtlePos = baseManager.GetTurtlePosition();
        var current = new Vector2Int(Mathf.RoundToInt(turtlePos.x), Mathf.RoundToInt(turtlePos.z));
        var remaining = new List<Vector2Int>(columns.Keys);
        var result = new List<Vector3>();

        while (remaining.Count > 0)
        {
            remaining.Sort((a, b) =>
                Vector2Int.Distance(a, current).CompareTo(Vector2Int.Distance(b, current)));

            var nearest = remaining[0];
            remaining.RemoveAt(0);
            result.AddRange(columns[nearest]);
            current = nearest;
        }

        return result;
    }

    private bool IsSameColumn(Vector3 a, Vector3 b)
    {
        return Mathf.RoundToInt(a.x) == Mathf.RoundToInt(b.x) &&
               Mathf.RoundToInt(a.z) == Mathf.RoundToInt(b.z);
    }

    private int CountColumns(List<Vector3> blocks)
    {
        return blocks.Select(b => new Vector2Int(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.z)))
                     .Distinct().Count();
    }

    #endregion
}
