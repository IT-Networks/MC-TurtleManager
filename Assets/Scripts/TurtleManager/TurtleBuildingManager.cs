using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Manages building operations with structure placement and optimization
/// </summary>
public class TurtleBuildingManager : MonoBehaviour
{
    [Header("Building Settings")]
    public bool groupNearbyOperations = true;
    public float operationGroupingDistance = 5f;
    public float buildingPositionTolerance = 0.1f;

    private TurtleBaseManager baseManager;
    private TurtleMovementManager movementManager;
    private TurtleOperationManager operationManager;

    // Building state
    private StructureData currentStructure;
    private Vector3 currentBuildOrigin;

    private void Start()
    {
        baseManager = GetComponent<TurtleBaseManager>();
        movementManager = GetComponent<TurtleMovementManager>();
        operationManager = GetComponent<TurtleOperationManager>();
    }

    #region Building Operations

    /// <summary>
    /// Start building operation with structure data
    /// </summary>
    public void StartBuildingOperation(Vector3 buildOrigin, StructureData structure)
    {
        if (operationManager.CurrentOperation != TurtleOperationManager.OperationType.None)
        {
            Debug.LogWarning("Cannot start building - turtle is busy");
            return;
        }

        currentBuildOrigin = buildOrigin;
        currentStructure = structure;

        Debug.Log($"Starting building operation: {structure.name} at {buildOrigin} ({structure.blocks.Count} blocks)");
        
        operationManager.StartOperation(TurtleOperationManager.OperationType.Building, structure.blocks.Count);
        StartCoroutine(ExecuteBuildingOperation());
    }

    /// <summary>
    /// Execute building operation
    /// </summary>
    private IEnumerator ExecuteBuildingOperation()
    {
        if (currentStructure == null)
        {
            Debug.LogError("Cannot execute building - missing structure data");
            yield break;
        }

        // Sort blocks by Y coordinate (build from bottom up)
        var sortedBlocks = new List<StructureData.StructureBlock>(currentStructure.blocks);
        sortedBlocks.Sort((a, b) => a.position.y.CompareTo(b.position.y));

        // Group nearby blocks if optimization enabled
        if (groupNearbyOperations)
        {
            sortedBlocks = GroupNearbyBlocks(sortedBlocks);
        }

        foreach (var structureBlock in sortedBlocks)
        {
            Vector3 worldPos = currentBuildOrigin + (Vector3)structureBlock.position;
            
            yield return StartCoroutine(PlaceBlockWithPositioning(worldPos, structureBlock.blockType));

            operationManager.IncrementProcessed();
            yield return new WaitUntil(() => !baseManager.IsBusy);
            yield return new WaitForSeconds(1f);
        }

        operationManager.CompleteOperation();
        
        currentStructure = null;
        currentBuildOrigin = Vector3.zero;
        
        Debug.Log("Building operation completed");
    }

    /// <summary>
    /// Place a single block with positioning
    /// </summary>
    public IEnumerator PlaceBlockWithPositioning(Vector3 blockPosition, string blockType)
    {
        Debug.Log($"Placing {blockType} at {blockPosition}");

        // Find building position
        Vector3 buildingPos = movementManager.GetBestAdjacentPosition(blockPosition);
        
        if (buildingPos == Vector3.zero)
        {
            Debug.LogWarning($"No valid building position found for block at {blockPosition}");
            operationManager.IncrementFailed();
            yield break;
        }

        // Move to building position
        yield return StartCoroutine(movementManager.MoveTurtleToExactPosition(buildingPos));

        // Verify position
        Vector3 currentPos = baseManager.GetTurtlePosition();
        if (Vector3.Distance(currentPos, buildingPos) > buildingPositionTolerance)
        {
            Debug.LogWarning($"Failed to reach building position for {blockPosition}");
            operationManager.IncrementFailed();
            yield break;
        }

        // Execute building action
        yield return StartCoroutine(ExecuteBuildingAction(blockPosition, blockType));
    }

    /// <summary>
    /// Execute building action based on relative position
    /// </summary>
    private IEnumerator ExecuteBuildingAction(Vector3 blockPosition, string blockType)
    {
        Vector3 turtlePos = baseManager.GetTurtlePosition();
        Vector3 direction = blockPosition - turtlePos;

        string placeCommand = "place";
        string faceDirection = null;

        // Determine place command based on relative position
        if (Mathf.Abs(direction.y) > 0.5f)
        {
            if (direction.y > 0)
            {
                placeCommand = "placeup";
                Debug.Log($"Placing block above at {blockPosition}");
            }
            else
            {
                placeCommand = "placedown";
                Debug.Log($"Placing block below at {blockPosition}");
            }
        }
        else
        {
            // Horizontal placement - face the location first
            faceDirection = movementManager.GetDirectionFromVector(direction);
            Debug.Log($"Placing block in direction {faceDirection} at {blockPosition}");
        }

        // Face correct direction if needed
        if (!string.IsNullOrEmpty(faceDirection))
        {
            yield return StartCoroutine(movementManager.FaceDirection(faceDirection));
        }

        // Execute place command
        var placeCmd = new TurtleCommand(placeCommand, baseManager.defaultTurtleId)
        {
            targetPosition = blockPosition,
            blockType = blockType,
            requiresPositioning = false
        };
        
        baseManager.QueueCommand(placeCmd);
        
        // Wait for command execution
        yield return new WaitUntil(() => !baseManager.IsBusy);
    }

    #endregion

    #region Building Optimization

    /// <summary>
    /// Group nearby blocks to reduce travel time
    /// </summary>
    private List<StructureData.StructureBlock> GroupNearbyBlocks(List<StructureData.StructureBlock> blocks)
    {
        if (blocks.Count <= 1) return blocks;
        
        var result = new List<StructureData.StructureBlock>();
        var remaining = new List<StructureData.StructureBlock>(blocks);
        
        // Start with first block
        result.Add(remaining[0]);
        remaining.RemoveAt(0);
        
        while (remaining.Count > 0)
        {
            var current = result[result.Count - 1];
            
            // Find nearest remaining block within grouping distance
            int nearestIndex = 0;
            float nearestDistance = Vector3.Distance(current.position, remaining[0].position);
            
            for (int i = 1; i < remaining.Count; i++)
            {
                float distance = Vector3.Distance(current.position, remaining[i].position);
                if (distance < nearestDistance && distance <= operationGroupingDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }
            
            result.Add(remaining[nearestIndex]);
            remaining.RemoveAt(nearestIndex);
        }
        
        Debug.Log($"Grouped {blocks.Count} blocks into optimized build order");
        return result;
    }

    /// <summary>
    /// Validate building position
    /// </summary>
    public bool CanPlaceBlockAt(Vector3 position)
    {
        var chunk = baseManager.worldManager?.GetChunkContaining(position);
        if (chunk == null || !chunk.IsLoaded) return false;

        var chunkInfo = chunk.GetChunkInfo();
        if (chunkInfo == null) return true;

        // Check if position is empty
        var blockType = chunkInfo.GetBlockTypeAt(position);
        return string.IsNullOrEmpty(blockType) || IsAirBlock(blockType);
    }

    private bool IsAirBlock(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return true;
        string lower = blockType.ToLowerInvariant();
        return lower.Contains("air") || lower.Equals("minecraft:air");
    }

    #endregion

    #region Public Properties

    public StructureData CurrentStructure => currentStructure;
    public Vector3 CurrentBuildOrigin => currentBuildOrigin;
    public bool IsBuilding => currentStructure != null;

    #endregion

    public void CancelBuilding()
    {
        currentStructure = null;
        currentBuildOrigin = Vector3.zero;
        StopAllCoroutines();
    }
}