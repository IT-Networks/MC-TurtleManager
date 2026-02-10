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

                // If first block of column was unreachable, defer rest of column too
                if (sameColumn && skipColumn)
                {
                    deferred.Add(blockPos);
                    continue;
                }

                skipColumn = false;

                if (sameColumn)
                {
                    yield return StartCoroutine(ExecuteDig(blockPos));
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

        isMining = false;
        operationManager.CompleteOperation();
        Debug.Log("Mining operation completed");
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

        // Update world state: remove block from ChunkInfo so pathfinding
        // knows this position is now air. Single lookup, no mesh regeneration
        // per block (too expensive during batch mining).
        var worldManager = baseManager.worldManager;
        if (worldManager != null)
        {
            var chunk = worldManager.GetChunkContaining(blockPosition);
            if (chunk != null)
            {
                chunk.GetChunkInfo()?.RemoveBlockAt(blockPosition);
            }
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
