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

    private TurtleBaseManager baseManager;
    private bool isFollowingPath = false;
    private int currentPathIndex = 0;
    private PathfindingResult currentPathResult;
    private string cachedDirection = null; // Cache direction locally to avoid stale server status

    private void Start()
    {
        baseManager = FindFirstObjectByType<TurtleBaseManager>();
        
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
                var pathResult = pathfinder.FindPath(baseManager.GetTurtlePosition(), targetPosition, defaultPathfindingOptions);
                
                if (pathResult.success && pathResult.optimizedPath.Count > 0)
                {
                    yield return StartCoroutine(FollowOptimizedPath(pathResult.rasterizedPath));
                }
                else
                {
                    yield return StartCoroutine(MoveDirectlyToPosition(targetPosition));
                }
            }
            else
            {
                yield return StartCoroutine(MoveDirectlyToPosition(targetPosition));
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
        currentPathIndex = 1;

        // Reset direction cache at start of new path to sync with server status
        cachedDirection = null;

        while (currentPathIndex < path.Count && isFollowingPath)
        {
            Vector3 currentPos = path[currentPathIndex - 1];
            Vector3 nextPos = path[currentPathIndex];

            yield return StartCoroutine(ExecuteMovementStep(currentPos, nextPos));
            yield return new WaitUntil(() => !baseManager.IsBusy);

            currentPathIndex++;
        }

        isFollowingPath = false;
        Debug.Log("Optimized path following completed");
    }

    /// <summary>
    /// Direct movement without pathfinding
    /// </summary>
    public IEnumerator MoveDirectlyToPosition(Vector3 targetPosition)
    {
        while (Vector3.Distance(baseManager.GetTurtlePosition(), targetPosition) > positionTolerance)
        {
            Vector3 currentPos = baseManager.GetTurtlePosition();
            Vector3 difference = targetPosition - currentPos;

            if (Mathf.Abs(difference.x) > positionTolerance)
            {
                string direction = difference.x > 0 ? "west" : "east";
                yield return StartCoroutine(FaceDirection(direction));
                baseManager.QueueCommand(new TurtleCommand("forward", baseManager.defaultTurtleId));
            }
            else if (Mathf.Abs(difference.z) > positionTolerance)
            {
                string direction = difference.z > 0 ? "south" : "north";
                yield return StartCoroutine(FaceDirection(direction));
                baseManager.QueueCommand(new TurtleCommand("forward", baseManager.defaultTurtleId));
            }
            else if (Mathf.Abs(difference.y) > positionTolerance)
            {
                if (difference.y > 0)
                {
                    baseManager.QueueCommand(new TurtleCommand("up", baseManager.defaultTurtleId));
                }
                else
                {
                    baseManager.QueueCommand(new TurtleCommand("down", baseManager.defaultTurtleId));
                }
            }

            yield return new WaitUntil(() => !baseManager.IsBusy);

            // Safety check
            if (Vector3.Distance(baseManager.GetTurtlePosition(), currentPos) < 0.1f)
            {
                Debug.LogWarning($"Turtle stuck at {baseManager.GetTurtlePosition()}, stopping movement");
                break;
            }
        }
    }

    /// <summary>
    /// Execute single movement step
    /// </summary>
    private IEnumerator ExecuteMovementStep(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;

        if (Mathf.Abs(direction.y) > 0.5f)
        {
            // Vertical movement
            if (direction.y > 0)
            {
                //baseManager.QueueCommand(new TurtleCommand("digup", baseManager.defaultTurtleId));
                baseManager.QueueCommand(new TurtleCommand("up", baseManager.defaultTurtleId));
            }
            else
            {
                //baseManager.QueueCommand(new TurtleCommand("digdown", baseManager.defaultTurtleId));
                baseManager.QueueCommand(new TurtleCommand("down", baseManager.defaultTurtleId));
            }
        }
        else
        {
            // Horizontal movement
            string targetDirection = GetDirectionFromVector(direction);
            yield return StartCoroutine(FaceDirection(targetDirection));
            baseManager.QueueCommand(new TurtleCommand("forward", baseManager.defaultTurtleId));
        }

        yield return null;
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
    /// Find best adjacent position to a target block
    /// </summary>
    public Vector3 GetBestAdjacentPosition(Vector3 blockPosition)
    {
        Vector3 currentPos = baseManager.GetTurtlePosition();
        
        Vector3[] adjacentPositions = {
            blockPosition + Vector3.right,   // East
            blockPosition + Vector3.left,    // West
            blockPosition + Vector3.forward, // North
            blockPosition + Vector3.back,    // South
            blockPosition + Vector3.down,    // Below
        };

        Vector3 bestPosition = Vector3.zero;
        float bestDistance = float.MaxValue;

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

        if (bestPosition != Vector3.zero)
        {
            Debug.Log($"Best adjacent position for {blockPosition}: {bestPosition} (distance: {bestDistance:F1})");
        }

        return bestPosition;
    }

    /// <summary>
    /// Check if position is accessible for turtle
    /// </summary>
    private bool IsPositionAccessible(Vector3 position)
    {
        if (baseManager.worldManager == null) return true;

        var chunk = baseManager.worldManager.GetChunkContaining(position);
        if (chunk == null || !chunk.IsLoaded) return false;

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