using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Diagnostics;
using Unity.VisualScripting;
using System;

public class Pathfinding3D : MonoBehaviour
{
    // Movement costs
    public const int MOVE_STRAIGHT_COST = 10;
    public const int MOVE_DIAGONAL_COST = 14;
    public const int JUMP_UP_COST = 20;

    [SerializeField] private int maxJumpHeight = 2;
    [SerializeField] private int maxFallDistance = 10;
    [SerializeField] private int maxSearchNodes = 5000;

    private List<ChunkInfo.BlockInfo> spawnedBlocks = new List<ChunkInfo.BlockInfo>();
    private HashSet<Vector3Int> surfaceBlocks;

    public Pathfinding3D(List<ChunkInfo.BlockInfo> spawnedBlocks)
    {
        this.spawnedBlocks = spawnedBlocks;
        CacheSurfaceBlocks();
    }

    public void SetBlockDictionary(List<ChunkInfo.BlockInfo> blocks)
    {
        spawnedBlocks = blocks;
        CacheSurfaceBlocks();
    }


    private void CacheSurfaceBlocks()
    {
        surfaceBlocks = new HashSet<Vector3Int>();
        foreach (var blockPos in spawnedBlocks)
        {
            Vector3Int abovePos = blockPos.localPosition + Vector3Int.up;
            if (!IsBlocked(abovePos))
            {
                surfaceBlocks.Add(blockPos.localPosition);
            }
        }
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 endPos)
    {
        Vector3Int startNode = Vector3Int.FloorToInt(startPos);
        Vector3Int endNode = Vector3Int.FloorToInt(endPos);

        if (startNode == endNode) return new List<Vector3> { endPos };
        if (IsBlocked(endNode)) return null;

          // A* Implementation
        List<PathNode> openSet = new List<PathNode>();
        HashSet<Vector3Int> closedSet = new HashSet<Vector3Int>();
        Dictionary<Vector3Int, PathNode> allNodes = new Dictionary<Vector3Int, PathNode>();

        PathNode startPathNode = new PathNode(startNode);
        startPathNode.GCost = 0;
        startPathNode.HCost = CalculateDistanceCost(startNode, endNode);
        openSet.Add(startPathNode);
        allNodes.Add(startNode, startPathNode);

        int iterations = 0;
        while (openSet.Count > 0 && iterations < maxSearchNodes)
        {
            iterations++;

            // Get node with lowest F cost
            PathNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost ||
                   (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            if (currentNode.Position == endNode)
            {
                return RetracePath(currentNode);
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode.Position);

            foreach (Vector3Int neighborPos in GetWalkableNeighbors(currentNode.Position))
            {
                if (closedSet.Contains(neighborPos)) continue;

                int newMovementCost = currentNode.GCost + CalculateDistanceCost(currentNode.Position, neighborPos);

                if (!allNodes.ContainsKey(neighborPos))
                {
                    allNodes[neighborPos] = new PathNode(neighborPos);
                }

                PathNode neighborNode = allNodes[neighborPos];

                if (newMovementCost < neighborNode.GCost || !openSet.Contains(neighborNode))
                {
                    neighborNode.GCost = newMovementCost;
                    neighborNode.HCost = CalculateDistanceCost(neighborPos, endNode);
                    neighborNode.CameFromNode = currentNode;

                    if (!openSet.Contains(neighborNode))
                    {
                        openSet.Add(neighborNode);
                    }
                }
            }
        }

        UnityEngine.Debug.LogWarning($"Pathfinding failed after {iterations} iterations");
        return null;
    }

    private IEnumerable<Vector3Int> GetWalkableNeighbors(Vector3Int position)
{
    bool isGrounded = IsSupported(position);

    // Cardinal directions only (no diagonals)
    Vector3Int[] directions = new Vector3Int[]
    {
        new Vector3Int(-1, 0, 0),   // Right
        new Vector3Int(1, 0, 0),  // Left
        new Vector3Int(0, 0, -1),   // Forward
        new Vector3Int(0, 0, 1),  // Back
        new Vector3Int(0, 1, 0),   // Up
        new Vector3Int(0, -1, 0)   // Down
    };

    foreach (Vector3Int dir in directions)
    {
        Vector3Int neighborPos = position + dir;

        // Skip if this is a horizontal move to a blocked position
        if (dir.y == 0 && IsBlocked(neighborPos))
        {
            continue;
        }

        // Handle horizontal movement
        if (dir.y == 0)
        {
            if (isGrounded)
            {
                // Grounded movement needs support below
                Vector3Int belowPos = neighborPos + Vector3Int.down;
                if (IsBlocked(belowPos) || surfaceBlocks.Contains(belowPos))
                {
                    yield return neighborPos;
                }
            }
            else
            {
                // Aerial horizontal movement just needs empty space
                yield return neighborPos;
            }
        }
        // Handle vertical movement up (jumping)
        else if (dir.y > 0)
        {
            // Can only jump from ground or while already ascending
            if ((isGrounded || position.y > 0) && !IsBlocked(neighborPos))
            {
                yield return neighborPos;
            }
        }
        // Handle vertical movement down (falling)
        else if (dir.y < 0)
        {
            // Check if we can fall this far
            bool canFall = true;
            for (int y = -1; y >= dir.y; y--)
            {
                Vector3Int checkPos = position + new Vector3Int(0, y, 0);
                if (IsBlocked(checkPos))
                {
                    canFall = false;
                    break;
                }
            }
            
            if (canFall)
            {
                yield return neighborPos;
            }
        }
    }
}
    private List<Vector3> RetracePath(PathNode endNode)
    {
        List<Vector3> path = new List<Vector3>();
        PathNode currentNode = endNode;

        while (currentNode != null)
        {
            path.Add(new Vector3(
                currentNode.Position.x + 0.5f,
                currentNode.Position.y + 0.5f,
                currentNode.Position.z + 0.5f
            ));
            currentNode = currentNode.CameFromNode;
        }

        path.Reverse();

        // Simplify path
        if (path.Count > 2)
        {
            List<Vector3> simplifiedPath = new List<Vector3> { path[0] };
            Vector3 lastDirection = (path[1] - path[0]).normalized;

            for (int i = 2; i < path.Count; i++)
            {
                Vector3 newDirection = (path[i] - simplifiedPath.Last()).normalized;
                if (Vector3.Distance(newDirection, lastDirection) > 0.1f)
                {
                    simplifiedPath.Add(path[i - 1]);
                    lastDirection = newDirection;
                }
            }

            simplifiedPath.Add(path.Last());
            return simplifiedPath;
        }

        return path;
    }
    public List<Vector3> CleanPath(List<Vector3> path)
{
    if (path.Count < 3) return path;
    
    List<Vector3> cleaned = new List<Vector3> { path[0] };
    
    for (int i = 1; i < path.Count - 1; i++)
    {
        Vector3 prevDir = (path[i] - path[i-1]).normalized;
        Vector3 nextDir = (path[i+1] - path[i]).normalized;
        
        // Only keep points where direction changes
        if (Vector3.Dot(prevDir, nextDir) < 0.99f)
            cleaned.Add(path[i]);
    }
    
    cleaned.Add(path[path.Count-1]);
    return cleaned;
}

    private bool IsSupported(Vector3Int position)
    {
        Vector3Int belowPos = position + Vector3Int.down;
        return IsBlocked(belowPos) || surfaceBlocks.Contains(belowPos);
    }

    private bool IsBlocked(Vector3Int position)
    {
        return spawnedBlocks.Any(b => b.localPosition == position);
    }

    private int CalculateDistanceCost(Vector3Int a, Vector3Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int dz = Mathf.Abs(a.z - b.z);

        // Chebyshev distance for horizontal, Manhattan for vertical
        return Mathf.Max(dx, dz) * MOVE_STRAIGHT_COST + dy * JUMP_UP_COST;
    }

    internal void AddBlockDictionary(List<ChunkInfo.BlockInfo> blockInfos)
    {
        if (spawnedBlocks == null)
            spawnedBlocks = new List<ChunkInfo.BlockInfo>();
        if (surfaceBlocks == null)
            surfaceBlocks = new HashSet<Vector3Int>();

        foreach (var block in blockInfos)
        {
            spawnedBlocks.Add(block);
            surfaceBlocks.Add(block.localPosition);
        }
        
        //CacheSurfaceBlocks();
    }

    private class PathNode
    {
        public Vector3Int Position { get; }
        public int GCost { get; set; }
        public int HCost { get; set; }
        public int FCost => GCost + HCost;
        public PathNode CameFromNode { get; set; }

        public PathNode(Vector3Int position)
        {
            Position = position;
            GCost = int.MaxValue;
            HCost = 0;
            CameFromNode = null;
        }
    }
}