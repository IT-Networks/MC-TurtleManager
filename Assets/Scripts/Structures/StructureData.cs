using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Data structure representing a buildable structure composed of Minecraft blocks
/// Can be created in the structure editor and built by turtles
/// </summary>
[Serializable]
public class StructureData
{
    public string name = "New Structure";
    public string description = "";
    public string category = "Custom";
    public string author = "";
    public long createdTimestamp;
    public long modifiedTimestamp;

    [SerializeField]
    public List<BlockData> blocks = new List<BlockData>();

    [SerializeField]
    public Vector3Int size = Vector3Int.one;

    public Vector3Int originOffset = Vector3Int.zero;

    // Metadata
    public int blockCount => blocks?.Count ?? 0;
    public bool isValid => blocks != null && blocks.Count > 0;

    public StructureData()
    {
        createdTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        modifiedTimestamp = createdTimestamp;
    }

    public StructureData(string structureName) : this()
    {
        name = structureName;
    }

    /// <summary>
    /// Adds a block to the structure
    /// </summary>
    public void AddBlock(Vector3Int position, string blockType)
    {
        // Check if block already exists at position
        RemoveBlockAt(position);

        blocks.Add(new BlockData
        {
            relativePosition = position,
            blockType = blockType
        });

        UpdateSize();
        UpdateModifiedTime();
    }

    /// <summary>
    /// Removes a block at the specified position
    /// </summary>
    public void RemoveBlockAt(Vector3Int position)
    {
        blocks.RemoveAll(b => b.relativePosition == position);
        UpdateSize();
        UpdateModifiedTime();
    }

    /// <summary>
    /// Gets the block at a specific position
    /// </summary>
    public BlockData GetBlockAt(Vector3Int position)
    {
        return blocks.Find(b => b.relativePosition == position);
    }

    /// <summary>
    /// Checks if a block exists at the position
    /// </summary>
    public bool HasBlockAt(Vector3Int position)
    {
        return blocks.Exists(b => b.relativePosition == position);
    }

    /// <summary>
    /// Updates the structure's bounding size
    /// </summary>
    public void UpdateSize()
    {
        if (blocks == null || blocks.Count == 0)
        {
            size = Vector3Int.one;
            return;
        }

        Vector3Int min = blocks[0].relativePosition;
        Vector3Int max = blocks[0].relativePosition;

        foreach (var block in blocks)
        {
            min = Vector3Int.Min(min, block.relativePosition);
            max = Vector3Int.Max(max, block.relativePosition);
        }

        size = max - min + Vector3Int.one;
        originOffset = min;
    }

    /// <summary>
    /// Gets the structure size
    /// </summary>
    public Vector3Int GetSize()
    {
        return size;
    }

    /// <summary>
    /// Gets the total volume (may include empty spaces)
    /// </summary>
    public int GetVolume()
    {
        return size.x * size.y * size.z;
    }

    /// <summary>
    /// Gets block type statistics
    /// </summary>
    public Dictionary<string, int> GetBlockTypeCount()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (var block in blocks)
        {
            if (counts.ContainsKey(block.blockType))
                counts[block.blockType]++;
            else
                counts[block.blockType] = 1;
        }

        return counts;
    }

    /// <summary>
    /// Clears all blocks
    /// </summary>
    public void Clear()
    {
        blocks.Clear();
        size = Vector3Int.one;
        originOffset = Vector3Int.zero;
        UpdateModifiedTime();
    }

    /// <summary>
    /// Creates a deep copy of this structure
    /// </summary>
    public StructureData Clone()
    {
        StructureData clone = new StructureData
        {
            name = name + " (Copy)",
            description = description,
            category = category,
            author = author,
            size = size,
            originOffset = originOffset,
            blocks = new List<BlockData>()
        };

        foreach (var block in blocks)
        {
            clone.blocks.Add(new BlockData
            {
                relativePosition = block.relativePosition,
                blockType = block.blockType
            });
        }

        return clone;
    }

    /// <summary>
    /// Optimizes the structure by normalizing positions to start at (0,0,0)
    /// </summary>
    public void Normalize()
    {
        if (blocks == null || blocks.Count == 0)
            return;

        Vector3Int min = blocks[0].relativePosition;
        foreach (var block in blocks)
        {
            min = Vector3Int.Min(min, block.relativePosition);
        }

        // Offset all blocks so minimum is at (0,0,0)
        for (int i = 0; i < blocks.Count; i++)
        {
            blocks[i].relativePosition -= min;
        }

        originOffset = Vector3Int.zero;
        UpdateSize();
    }

    private void UpdateModifiedTime()
    {
        modifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public override string ToString()
    {
        return $"{name} ({blockCount} blocks, {size})";
    }
}

/// <summary>
/// Individual block data within a structure
/// </summary>
[Serializable]
public class BlockData
{
    public Vector3Int relativePosition;
    public string blockType;

    // Optional metadata
    public int rotation = 0; // For future use
    public string metadata = ""; // For future use (e.g., chest contents)

    public BlockData()
    {
    }

    public BlockData(Vector3Int pos, string type)
    {
        relativePosition = pos;
        blockType = type;
    }
}
