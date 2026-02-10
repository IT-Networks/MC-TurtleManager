using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component that stores information about blocks within a chunk.
/// Used for gameplay interactions and queries.
/// </summary>
public class ChunkInfo : MonoBehaviour
{
    [System.Serializable]
    public class BlockInfo
    {
        public Vector3 worldPosition;
        public string blockType;
        public Vector3Int localPosition;

        public BlockInfo(Vector3 worldPos, string type)
        {
            worldPosition = worldPos;
            blockType = type;
            // Calculate local position if needed
            localPosition = Vector3Int.FloorToInt(worldPos);
        }
    }

    [SerializeField] private List<BlockInfo> blocks = new List<BlockInfo>();
    private Dictionary<Vector3Int, BlockInfo> blockLookup = new Dictionary<Vector3Int, BlockInfo>();
    private HashSet<Vector3Int> loggedMissingBlocks = new HashSet<Vector3Int>(); // Track logged positions to avoid spam

    /// <summary>
    /// Adds a block to this chunk's registry
    /// </summary>
    public void AddBlock(Vector3 worldPosition, string blockType)
    {
        var blockInfo = new BlockInfo(worldPosition, blockType);
        blocks.Add(blockInfo);
        
        Vector3Int key = Vector3Int.FloorToInt(worldPosition);
        blockLookup[key] = blockInfo;
    }

    /// <summary>
    /// Gets block information at a specific world position
    /// </summary>
    public BlockInfo GetBlockAt(Vector3 worldPosition)
    {
        Vector3Int key = Vector3Int.FloorToInt(worldPosition);
        // No Y normalization needed - AddBlock() uses direct world position
        bool found = blockLookup.TryGetValue(key, out BlockInfo blockInfo);

        if (!found && blocks.Count > 0 && !loggedMissingBlocks.Contains(key))
        {
            // Debug: Check if there's a block very close to this position (only log once per position)
            foreach (var block in blocks)
            {
                float distance = Vector3.Distance(block.worldPosition, worldPosition);
                if (distance < 0.5f)
                {
                    Debug.LogWarning($"ChunkInfo: Block lookup failed for {worldPosition} (key={key})");
                    Debug.LogWarning($"  But found nearby block at {block.worldPosition} (distance={distance:F3})");
                    Debug.LogWarning($"  Nearby block type: {block.blockType}");
                    loggedMissingBlocks.Add(key); // Prevent spam
                    break;
                }
            }
        }

        return blockInfo;
    }

    /// <summary>
    /// Gets block type at a specific world position
    /// </summary>
    public string GetBlockTypeAt(Vector3 worldPosition)
    {
        return GetBlockAt(worldPosition)?.blockType;
    }

    /// <summary>
    /// Checks if there's a block at the specified world position
    /// </summary>
    public bool HasBlockAt(Vector3 worldPosition)
    {
        Vector3Int key = Vector3Int.FloorToInt(worldPosition);
        return blockLookup.ContainsKey(key);
    }

    /// <summary>
    /// Gets all blocks of a specific type
    /// </summary>
    public List<BlockInfo> GetBlocksOfType(string blockType)
    {
        var result = new List<BlockInfo>();
        foreach (var block in blocks)
        {
            if (string.Equals(block.blockType, blockType, System.StringComparison.OrdinalIgnoreCase))
            {
                result.Add(block);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets all blocks within a radius of a world position
    /// </summary>
    public List<BlockInfo> GetBlocksInRadius(Vector3 center, float radius)
    {
        var result = new List<BlockInfo>();
        float radiusSqr = radius * radius;
        
        foreach (var block in blocks)
        {
            if ((block.worldPosition - center).sqrMagnitude <= radiusSqr)
            {
                result.Add(block);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Removes a block from the registry (for dynamic modification)
    /// </summary>
    public bool RemoveBlockAt(Vector3 worldPosition)
    {
        Vector3Int key = Vector3Int.FloorToInt(worldPosition);
        if (blockLookup.TryGetValue(key, out BlockInfo blockInfo))
        {
            blocks.Remove(blockInfo);
            blockLookup.Remove(key);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all block information
    /// </summary>
    public void ClearBlocks()
    {
        blocks.Clear();
        blockLookup.Clear();
        loggedMissingBlocks.Clear();
    }

    /// <summary>
    /// Gets total number of blocks in this chunk
    /// </summary>
    public int BlockCount => blocks.Count;

    /// <summary>
    /// Gets all blocks in this chunk
    /// </summary>
    public List<ChunkInfo.BlockInfo> GetAllBlocks()
    {
        return new List<ChunkInfo.BlockInfo>(blocks);
    }

    /// <summary>
    /// Gets statistics about block types in this chunk
    /// </summary>
    public Dictionary<string, int> GetBlockTypeStatistics()
    {
        var stats = new Dictionary<string, int>();
        foreach (var block in blocks)
        {
            if (stats.ContainsKey(block.blockType))
                stats[block.blockType]++;
            else
                stats[block.blockType] = 1;
        }
        return stats;
    }


}