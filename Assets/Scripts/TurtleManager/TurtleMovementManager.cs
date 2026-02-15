using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages turtle movement, pathfinding, and precise positioning
/// </summary>
public class TurtleMovementManager : MonoBehaviour
{
    [Header("Pathfinding Integration")]
    public BlockWorldPathfinder pathfinder;
    public bool usePathfinding = true;
    public PathfindingOptions defaultPathfindingOptions = new PathfindingOptions();

    [Header("Movement Settings")]
    public bool validateMovementBeforeExecution = true;
    public float positionTolerance = 0.1f;
    public int maxMovementRetries = 3;

    [Header("Excavation Settings")]
    [Tooltip("Allow turtle to dig through obstacles when moving to target (faster but mines more blocks)")]
    public bool enableAutoExcavation = false;
    [Tooltip("Only used when Auto Excavation is enabled")]
    public bool showExcavationWarnings = true;

    private TurtleBaseManager baseManager;
    private TurtleOperationManager operationManager;
    private bool isFollowingPath = false;
    private int currentPathIndex = 0;
    private PathfindingResult currentPathResult;
    private PathfindingResult lastCompletedPath; // Store last completed path for visualization
    private string cachedDirection = null; // Cache direction locally to avoid stale server status

    private void Start()
    {
        baseManager = GetComponent<TurtleBaseManager>();
        if (baseManager == null)
            baseManager = FindFirstObjectByType<TurtleBaseManager>();

        operationManager = GetComponent<TurtleOperationManager>();
        if (operationManager == null)
            operationManager = FindFirstObjectByType<TurtleOperationManager>();

        // Auto-find pathfinder if not assigned
        if (pathfinder == null)
        {
            pathfinder = FindFirstObjectByType<BlockWorldPathfinder>();
            if (pathfinder != null)
            {
                Debug.Log("TurtleMovementManager: Auto-found BlockWorldPathfinder");
            }
            else
            {
                Debug.LogWarning("TurtleMovementManager: No BlockWorldPathfinder found. Pathfinding disabled.");
                usePathfinding = false;
            }
        }

        if (defaultPathfindingOptions == null)
        {
            defaultPathfindingOptions = new PathfindingOptions
            {
                canFly = false,
                optimizePath = true,
                removeRedundantMoves = true,
                maxOptimizationPasses = 3
            };
        }
    }

    #region Movement API

    /// <summary>
    /// Move turtle to exact position with retry mechanism
    /// </summary>
    public IEnumerator MoveTurtleToExactPosition(Vector3 targetPosition)
    {
        if (baseManager.GetCurrentStatus() == null) yield break;

        Vector3 currentPos = baseManager.GetTurtlePosition();

        if (Vector3.Distance(currentPos, targetPosition) <= positionTolerance)
        {
            Debug.Log($"Already at target position {targetPosition}");
            yield break;
        }

        Debug.Log($"Moving turtle from {currentPos} to {targetPosition}");

        int retryCount = 0;
        while (Vector3.Distance(baseManager.GetTurtlePosition(), targetPosition) > positionTolerance &&
               retryCount < maxMovementRetries)
        {
            if (usePathfinding && pathfinder != null)
            {
                Debug.Log($"[Pathfinding] Attempting to find path from {baseManager.GetTurtlePosition()} to {targetPosition}");
                var pathResult = pathfinder.FindPath(baseManager.GetTurtlePosition(), targetPosition, defaultPathfindingOptions);

                if (pathResult.success && pathResult.optimizedPath.Count > 0)
                {
                    Debug.Log($"[Pathfinding] SUCCESS - Path found with {pathResult.optimizedPath.Count} waypoints");
                    yield return StartCoroutine(FollowOptimizedPath(pathResult.rasterizedPath));
                }
                else
                {
                    Debug.LogWarning($"[Pathfinding] FAILED - No valid path found. Success: {pathResult.success}, Waypoints: {pathResult.optimizedPath?.Count ?? 0}");
                    Debug.LogWarning("Turtle cannot reach destination - skipping movement");
                    operationManager?.IncrementFailed();
                    yield break; // Don't try direct movement through blocks
                }
            }
            else
            {
                Debug.LogWarning("Pathfinder not available - cannot move safely");
                yield break;
            }

            yield return new WaitUntil(() => !baseManager.IsBusy);
            retryCount++;
        }

        Vector3 finalPos = baseManager.GetTurtlePosition();
        if (Vector3.Distance(finalPos, targetPosition) > positionTolerance)
        {
            Debug.LogWarning($"Failed to reach exact position after {retryCount} attempts. " +
                           $"Target: {targetPosition}, Actual: {finalPos}, Distance: {Vector3.Distance(finalPos, targetPosition):F2}");
        }
        else
        {
            Debug.Log($"Successfully reached target position {targetPosition}");
        }
    }

    /// <summary>
    /// Follow optimized path step by step
    /// </summary>
    public IEnumerator FollowOptimizedPath(List<Vector3> path)
    {
        if (path.Count < 2) yield break;

        isFollowingPath = true;
        currentPathResult = new PathfindingResult { optimizedPath = path };
        lastCompletedPath = currentPathResult; // Store for visualization
        currentPathIndex = 1;

        // Reset direction cache at start of new path to sync with server status
        cachedDirection = null;

        // Auto-show path visualization when movement starts
        TurtleObject turtleObj = GetComponent<TurtleObject>();
        if (turtleObj != null)
        {
            turtleObj.ShowPathAutomatically();
        }

        Debug.Log($"[Path] Starting path with {path.Count} waypoints");

        while (currentPathIndex < path.Count && isFollowingPath)
        {
            Vector3 currentPos = path[currentPathIndex - 1];
            Vector3 nextPos = path[currentPathIndex];

            Debug.Log($"[Path] Moving to waypoint {currentPathIndex}/{path.Count}: {nextPos}");
            yield return StartCoroutine(ExecuteMovementStep(currentPos, nextPos));
            yield return new WaitUntil(() => !baseManager.IsBusy);

            currentPathIndex++;
        }

        isFollowingPath = false;
        Debug.Log("Optimized path following completed");
    }

    /// <summary>
    /// Direct movement without pathfinding, with optional automatic excavation
    /// </summary>
    public IEnumerator MoveDirectlyToPosition(Vector3 targetPosition)
    {
        int stuckCounter = 0;
        int maxStuckAttempts = 3;

        while (Vector3.Distance(baseManager.GetTurtlePosition(), targetPosition) > positionTolerance)
        {
            Vector3 currentPos = baseManager.GetTurtlePosition();
            Vector3 difference = targetPosition - currentPos;

            Vector3 nextPos = currentPos;

            if (Mathf.Abs(difference.x) > positionTolerance)
            {
                string direction = difference.x > 0 ? "west" : "east";
                yield return StartCoroutine(FaceDirection(direction));

                // Calculate next position
                nextPos = currentPos + (difference.x > 0 ? Vector3.right : Vector3.left);

                // Check and dig if blocked (only if auto excavation enabled)
                if (enableAutoExcavation && IsBlockSolidAtPosition(nextPos))
                {
                    Debug.Log($"Excavating obstacle at {nextPos} to reach target");
                    baseManager.QueueCommand(new TurtleCommand("dig", baseManager.defaultTurtleId));
                    yield return new WaitUntil(() => !baseManager.IsBusy);

                    // Remove block and regenerate chunk mesh
                    if (baseManager.worldManager != null)
                    {
                        var chunk = baseManager.worldManager.GetChunkContaining(nextPos);
                        chunk?.RemoveBlockAndRegenerate(nextPos);
                    }
                }

                baseManager.QueueCommand(new TurtleCommand("forward", baseManager.defaultTurtleId));
            }
            else if (Mathf.Abs(difference.z) > positionTolerance)
            {
                string direction = difference.z > 0 ? "south" : "north";
                yield return StartCoroutine(FaceDirection(direction));

                // Calculate next position
                nextPos = currentPos + (difference.z > 0 ? Vector3.forward : Vector3.back);

                // Check and dig if blocked (only if auto excavation enabled)
                if (enableAutoExcavation && IsBlockSolidAtPosition(nextPos))
                {
                    Debug.Log($"Excavating obstacle at {nextPos} to reach target");
                    baseManager.QueueCommand(new TurtleCommand("dig", baseManager.defaultTurtleId));
                    yield return new WaitUntil(() => !baseManager.IsBusy);

                    // Remove block and regenerate chunk mesh
                    if (baseManager.worldManager != null)
                    {
                        var chunk = baseManager.worldManager.GetChunkContaining(nextPos);
                        chunk?.RemoveBlockAndRegenerate(nextPos);
                    }
                }

                baseManager.QueueCommand(new TurtleCommand("forward", baseManager.defaultTurtleId));
            }
            else if (Mathf.Abs(difference.y) > positionTolerance)
            {
                if (difference.y > 0)
                {
                    nextPos = currentPos + Vector3.up;

                    // Check and dig if blocked (only if auto excavation enabled)
                    if (enableAutoExcavation && IsBlockSolidAtPosition(nextPos))
                    {
                        Debug.Log($"Excavating obstacle above at {nextPos}");
                        baseManager.QueueCommand(new TurtleCommand("digup", baseManager.defaultTurtleId));
                        yield return new WaitUntil(() => !baseManager.IsBusy);

                        // Remove block and regenerate chunk mesh
                        if (baseManager.worldManager != null)
                        {
                            var chunk = baseManager.worldManager.GetChunkContaining(nextPos);
                            chunk?.RemoveBlockAndRegenerate(nextPos);
                        }
                    }

                    baseManager.QueueCommand(new TurtleCommand("up", baseManager.defaultTurtleId));
                }
                else
                {
                    nextPos = currentPos + Vector3.down;

                    // Check and dig if blocked (only if auto excavation enabled)
                    if (enableAutoExcavation && IsBlockSolidAtPosition(nextPos))
                    {
                        Debug.Log($"Excavating obstacle below at {nextPos}");
                        baseManager.QueueCommand(new TurtleCommand("digdown", baseManager.defaultTurtleId));
                        yield return new WaitUntil(() => !baseManager.IsBusy);

                        // Remove block and regenerate chunk mesh
                        if (baseManager.worldManager != null)
                        {
                            var chunk = baseManager.worldManager.GetChunkContaining(nextPos);
                            chunk?.RemoveBlockAndRegenerate(nextPos);
                        }
                    }

                    baseManager.QueueCommand(new TurtleCommand("down", baseManager.defaultTurtleId));
                }
            }

            yield return new WaitUntil(() => !baseManager.IsBusy);

            // Safety check for getting stuck
            Vector3 newPos = baseManager.GetTurtlePosition();
            if (Vector3.Distance(newPos, currentPos) < 0.1f)
            {
                stuckCounter++;
                Debug.LogWarning($"Turtle stuck at {newPos}, attempt {stuckCounter}/{maxStuckAttempts}");

                if (stuckCounter >= maxStuckAttempts)
                {
                    Debug.LogError($"Turtle permanently stuck at {newPos}, cannot reach {targetPosition}");
                    break;
                }

                // Try to unstuck by digging in the intended direction
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                stuckCounter = 0; // Reset counter if we moved
            }
        }
    }

    /// <summary>
    /// Execute single movement step with optional obstacle excavation
    /// </summary>
    private IEnumerator ExecuteMovementStep(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;

        if (Mathf.Abs(direction.y) > 0.5f)
        {
            // Vertical movement
            if (direction.y > 0)
            {
                // Check if block above is solid
                if (enableAutoExcavation && IsBlockSolidAtPosition(to))
                {
                    Debug.Log($"Obstacle detected above at {to}, excavating...");
                    baseManager.QueueCommand(new TurtleCommand("digup", baseManager.defaultTurtleId));
                    yield return new WaitUntil(() => !baseManager.IsBusy);

                    // Remove block and regenerate chunk mesh
                    if (baseManager.worldManager != null)
                    {
                        var chunk = baseManager.worldManager.GetChunkContaining(to);
                        chunk?.RemoveBlockAndRegenerate(to);
                    }
                }
                baseManager.QueueCommand(new TurtleCommand("up", baseManager.defaultTurtleId));
            }
            else
            {
                // Check if block below is solid
                if (enableAutoExcavation && IsBlockSolidAtPosition(to))
                {
                    Debug.Log($"Obstacle detected below at {to}, excavating...");
                    baseManager.QueueCommand(new TurtleCommand("digdown", baseManager.defaultTurtleId));
                    yield return new WaitUntil(() => !baseManager.IsBusy);

                    // Remove block and regenerate chunk mesh
                    if (baseManager.worldManager != null)
                    {
                        var chunk = baseManager.worldManager.GetChunkContaining(to);
                        chunk?.RemoveBlockAndRegenerate(to);
                    }
                }
                baseManager.QueueCommand(new TurtleCommand("down", baseManager.defaultTurtleId));
            }
        }
        else
        {
            // Horizontal movement
            string targetDirection = GetDirectionFromVector(direction);
            yield return StartCoroutine(FaceDirection(targetDirection));

            // Check if block ahead is solid
            if (enableAutoExcavation && IsBlockSolidAtPosition(to))
            {
                Debug.Log($"Obstacle detected ahead at {to}, excavating...");
                baseManager.QueueCommand(new TurtleCommand("dig", baseManager.defaultTurtleId));
                yield return new WaitUntil(() => !baseManager.IsBusy);

                // Remove block and regenerate chunk mesh
                if (baseManager.worldManager != null)
                {
                    var chunk = baseManager.worldManager.GetChunkContaining(to);
                    chunk?.RemoveBlockAndRegenerate(to);
                }
            }

            baseManager.QueueCommand(new TurtleCommand("forward", baseManager.defaultTurtleId));
        }

        yield return null;
    }

    /// <summary>
    /// Check if there's a solid block at the given position
    /// </summary>
    private bool IsBlockSolidAtPosition(Vector3 position)
    {
        if (baseManager.worldManager == null) return false;

        var chunk = baseManager.worldManager.GetChunkContaining(position);
        if (chunk == null || !chunk.IsLoaded) return false;

        var chunkInfo = chunk.GetChunkInfo();
        if (chunkInfo == null) return false;

        var blockType = chunkInfo.GetBlockTypeAt(position);

        // Check if it's a solid, non-air block
        if (string.IsNullOrEmpty(blockType) || IsAirBlock(blockType))
            return false;

        return true; // It's a solid block
    }

    #endregion

    #region Direction Management

    /// <summary>
    /// Face turtle in specific direction
    /// </summary>
    public IEnumerator FaceDirection(string targetDirection)
    {
        var status = baseManager.GetCurrentStatus();
        if (status == null) yield break;

        // Use cached direction if available, otherwise use server status
        string currentDirection = !string.IsNullOrEmpty(cachedDirection) ? cachedDirection : status.direction;

        if (currentDirection == targetDirection)
        {
            cachedDirection = targetDirection; // Update cache
            yield break;
        }

        Debug.Log($"Turning from {currentDirection} to {targetDirection}");

        var turnCommands = GetTurnCommands(currentDirection, targetDirection);
        foreach (string command in turnCommands)
        {
            baseManager.QueueCommand(new TurtleCommand(command, baseManager.defaultTurtleId));
        }

        // Update cached direction immediately after queueing turn commands
        cachedDirection = targetDirection;

        yield return null;
    }

    /// <summary>
    /// Get direction string from movement vector
    /// </summary>
    public string GetDirectionFromVector(Vector3 direction)
    {
        Vector3 normalizedDir = direction.normalized;

        if (Mathf.Abs(normalizedDir.x) > Mathf.Abs(normalizedDir.z))
        {
            return normalizedDir.x > 0 ? "west" : "east";
        }
        else
        {
            return normalizedDir.z > 0 ? "south" : "north";
        }
    }

    /// <summary>
    /// Calculate turn commands between directions
    /// </summary>
    private List<string> GetTurnCommands(string currentDir, string targetDir)
    {
        var directions = new Dictionary<string, int>
        {
            { "north", 0 },
            { "east", 1 },
            { "south", 2 },
            { "west", 3 }
        };

        if (!directions.ContainsKey(currentDir) || !directions.ContainsKey(targetDir))
            return new List<string>();

        int current = directions[currentDir];
        int target = directions[targetDir];
        int diff = (target - current + 4) % 4;

        List<string> commands = new List<string>();

        switch (diff)
        {
            case 1:
                commands.Add("right");
                break;
            case 2:
                commands.Add("right");
                commands.Add("right");
                break;
            case 3:
                commands.Add("left");
                break;
        }

        return commands;
    }

    #endregion

    #region Position Utilities

    /// <summary>
    /// Find best adjacent position to a target block with pathfinding verification
    /// </summary>
    public Vector3 GetBestAdjacentPosition(Vector3 blockPosition)
    {
        Vector3 currentPos = baseManager.GetTurtlePosition();

        Vector3[] adjacentPositions = {
            blockPosition + Vector3.up,      // Above (for digdown)
            blockPosition + Vector3.right,   // East
            blockPosition + Vector3.left,    // West
            blockPosition + Vector3.forward, // North
            blockPosition + Vector3.back,    // South
            blockPosition + Vector3.down,    // Below (for digup)
        };

        Vector3 bestPosition = Vector3.zero;
        float bestDistance = float.MaxValue;
        bool foundReachablePosition = false;

        // First pass: Try to find a position that's both accessible AND reachable via pathfinding
        foreach (Vector3 pos in adjacentPositions)
        {
            if (IsPositionAccessible(pos))
            {
                // Check if we can actually path to this position
                bool isReachable = CanPathToPosition(currentPos, pos);
                float distance = Vector3.Distance(currentPos, pos);

                if (isReachable && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = pos;
                    foundReachablePosition = true;
                }
            }
        }

        // Second pass: If no reachable position found, find closest accessible position
        // (turtle will need to dig its way there)
        if (!foundReachablePosition)
        {
            if (showExcavationWarnings)
            {
                if (enableAutoExcavation)
                {
                    Debug.LogWarning($"No directly reachable adjacent position found for {blockPosition}. Turtle will excavate a path.");
                }
                else
                {
                    Debug.LogWarning($"No directly reachable adjacent position found for {blockPosition}. Enable 'Auto Excavation' to allow turtle to dig through obstacles, or turtle may get stuck!");
                }
            }

            bestDistance = float.MaxValue;
            foreach (Vector3 pos in adjacentPositions)
            {
                if (IsPositionAccessible(pos))
                {
                    float distance = Vector3.Distance(currentPos, pos);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPosition = pos;
                    }
                }
            }
        }

        if (bestPosition != Vector3.zero && showExcavationWarnings)
        {
            string reachabilityInfo = foundReachablePosition ? "directly reachable" : (enableAutoExcavation ? "needs excavation (enabled)" : "needs excavation (DISABLED - may fail!)");
            Debug.Log($"Best adjacent position for {blockPosition}: {bestPosition} (distance: {bestDistance:F1}, {reachabilityInfo})");
        }

        return bestPosition;
    }

    /// <summary>
    /// Check if we can path to a position (quick check)
    /// </summary>
    private bool CanPathToPosition(Vector3 from, Vector3 to)
    {
        // Quick distance check - if very far, don't bother pathfinding
        float distance = Vector3.Distance(from, to);
        if (distance > 50f) return false;

        // If already very close, consider it reachable
        if (distance < 2f) return true;

        // Try pathfinding with NavMesh
        if (usePathfinding && pathfinder != null)
        {
            var pathResult = pathfinder.FindPath(from, to, defaultPathfindingOptions);
            return pathResult.success;
        }

        // Fallback: Check if path is clear using simple raycast-style check
        return IsPathClear(from, to);
    }

    /// <summary>
    /// Simple check if path between two points is relatively clear
    /// </summary>
    private bool IsPathClear(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        Vector3 normalized = direction.normalized;

        // Sample a few points along the path
        int samples = Mathf.Min((int)distance + 1, 10);
        for (int i = 1; i <= samples; i++)
        {
            Vector3 checkPos = from + normalized * (distance * i / samples);

            if (!IsPositionClearOrDiggable(checkPos))
            {
                return false; // Path blocked by undiggable obstacle
            }
        }

        return true;
    }

    /// <summary>
    /// Check if position is clear or can be dug through
    /// </summary>
    private bool IsPositionClearOrDiggable(Vector3 position)
    {
        if (baseManager.worldManager == null) return true;

        var chunk = baseManager.worldManager.GetChunkContaining(position);
        if (chunk == null || !chunk.IsLoaded) return true;

        var chunkInfo = chunk.GetChunkInfo();
        if (chunkInfo == null) return true;

        var blockType = chunkInfo.GetBlockTypeAt(position);

        // Position is clear
        if (string.IsNullOrEmpty(blockType) || IsAirBlock(blockType)) return true;

        // Check if it's a diggable block (not bedrock, etc.)
        return IsBlockDiggable(blockType);
    }

    /// <summary>
    /// Check if a block can be dug
    /// </summary>
    private bool IsBlockDiggable(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return false;

        string lower = blockType.ToLowerInvariant();

        // Unbreakable blocks
        if (lower.Contains("bedrock")) return false;
        if (lower.Contains("barrier")) return false;
        if (lower.Contains("command")) return false;
        if (lower.Contains("end_portal_frame")) return false;

        // Everything else is diggable
        return true;
    }

    /// <summary>
    /// Check if position is accessible for turtle
    /// </summary>
    private bool IsPositionAccessible(Vector3 position)
    {
        if (baseManager.worldManager == null) return true;

        var chunk = baseManager.worldManager.GetChunkContaining(position);
        if (chunk == null || !chunk.IsLoaded)
        {
            // Chunk not loaded (camera looking elsewhere) - assume accessible
            // rather than blocking the operation. Pathfinder will validate the actual path.
            Debug.LogWarning($"IsPositionAccessible: chunk not loaded for {position}, assuming accessible");
            return true;
        }

        var chunkInfo = chunk.GetChunkInfo();
        if (chunkInfo == null) return true;

        var blockType = chunkInfo.GetBlockTypeAt(position);
        if (!string.IsNullOrEmpty(blockType) && !IsAirBlock(blockType)) return false;

        // Check ground for walking turtles
        if (defaultPathfindingOptions?.canFly != true)
        {
            var groundPosition = position + Vector3.down;
            var groundType = chunkInfo.GetBlockTypeAt(groundPosition);
            return !string.IsNullOrEmpty(groundType) && !IsAirBlock(groundType);
        }

        return true;
    }

    private bool IsAirBlock(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return true;
        string lower = blockType.ToLowerInvariant();
        return lower.Contains("air") || lower.Equals("minecraft:air");
    }

    #endregion

    #region Public Properties

    public bool IsFollowingPath => isFollowingPath;
    public Vector3 CurrentTarget => currentPathResult?.optimizedPath?.Count > currentPathIndex
        ? currentPathResult.optimizedPath[currentPathIndex]
        : Vector3.zero;

    /// <summary>
    /// Check if turtle has an active or recently completed path
    /// </summary>
    public bool HasActivePath()
    {
        // Check active path first
        if (currentPathResult != null &&
            currentPathResult.optimizedPath != null &&
            currentPathResult.optimizedPath.Count > 0)
        {
            return true;
        }

        // Check last completed path as fallback
        return lastCompletedPath != null &&
               lastCompletedPath.optimizedPath != null &&
               lastCompletedPath.optimizedPath.Count > 0;
    }

    /// <summary>
    /// Get the current path (remaining waypoints if active, or last completed path)
    /// </summary>
    public List<Vector3> GetCurrentPath()
    {
        // If actively following path, return remaining waypoints
        if (isFollowingPath && currentPathResult != null && currentPathResult.optimizedPath != null)
        {
            List<Vector3> remainingPath = new List<Vector3>();
            for (int i = currentPathIndex; i < currentPathResult.optimizedPath.Count; i++)
            {
                remainingPath.Add(currentPathResult.optimizedPath[i]);
            }
            return remainingPath;
        }

        // Otherwise return last completed path (for visualization after completion)
        if (lastCompletedPath != null && lastCompletedPath.optimizedPath != null)
        {
            return new List<Vector3>(lastCompletedPath.optimizedPath);
        }

        return new List<Vector3>();
    }

    /// <summary>
    /// Get the full path including completed waypoints
    /// </summary>
    public List<Vector3> GetFullPath()
    {
        if (currentPathResult != null && currentPathResult.optimizedPath != null)
        {
            return new List<Vector3>(currentPathResult.optimizedPath);
        }

        if (lastCompletedPath != null && lastCompletedPath.optimizedPath != null)
        {
            return new List<Vector3>(lastCompletedPath.optimizedPath);
        }

        return new List<Vector3>();
    }

    #endregion

    public void CancelMovement()
    {
        isFollowingPath = false;
        currentPathResult = null;
        currentPathIndex = 0;
        cachedDirection = null; // Reset direction cache
        baseManager.ClearCommandQueue();
    }
}