using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides UV coordinates for different block types and faces.
/// Handles texture atlas mapping with per-block caching to avoid allocations.
/// </summary>
public class ChunkUVProvider
{
    private readonly float tileSize;
    private readonly int atlasSize;

    // Shared local UV corners — never changes, avoids per-call allocation
    private static readonly Vector2[] LocalUVs =
    {
        new Vector2(0, 0),
        new Vector2(1, 0),
        new Vector2(1, 1),
        new Vector2(0, 1)
    };

    // Cache: (blockName, faceIndex) → precomputed UV quad.
    // Most chunks have ~20-50 unique block names × 6 faces = ~300 entries max.
    private readonly Dictionary<long, Vector2[]> _uvCache = new Dictionary<long, Vector2[]>(256);

    public ChunkUVProvider(int atlasSize = 16)
    {
        this.atlasSize = atlasSize;
        this.tileSize = 1.0f / atlasSize;
    }

    public Vector2[] GetUVsForBlockFace(string blockName, int faceIndex)
    {
        // Use blockName hashCode + faceIndex as cache key
        long key = ((long)blockName.GetHashCode() << 4) | (uint)faceIndex;

        if (_uvCache.TryGetValue(key, out Vector2[] cached))
            return cached;

        Vector2 tileOffset = GetTileOffset(blockName, faceIndex);

        var result = new Vector2[4];
        for (int i = 0; i < 4; i++)
            result[i] = tileOffset + LocalUVs[i] * tileSize;

        _uvCache[key] = result;
        return result;
    }

    private Vector2 GetTileOffset(string blockName, int faceIndex)
    {
        int x = 0, y = 0;

        switch (blockName.ToLowerInvariant())
        {
            case "minecraft:grass":
                switch (faceIndex)
                {
                    case 2: x = 0; y = 15; break;
                    case 3: x = 2; y = 15; break;
                    default: x = 1; y = 15; break;
                }
                break;
            case "minecraft:stone":
                x = 3; y = 15;
                break;
            case "minecraft:wood":
            case "minecraft:log":
                switch (faceIndex)
                {
                    case 2:
                    case 3:
                        x = 4; y = 15;
                        break;
                    default:
                        x = 5; y = 15;
                        break;
                }
                break;
            case "minecraft:dirt":
                x = 2; y = 15;
                break;
            case "minecraft:cobblestone":
                x = 6; y = 15;
                break;
            case "minecraft:planks":
            case "minecraft:oak_planks":
                x = 7; y = 15;
                break;
            case "minecraft:sand":
                x = 8; y = 15;
                break;
            case "minecraft:gravel":
                x = 9; y = 15;
                break;
            case "minecraft:water":
                x = 10; y = 15;
                break;
            case "minecraft:leaves":
            case "minecraft:oak_leaves":
                x = 11; y = 15;
                break;
            default:
                x = 0; y = 0;
                break;
        }

        return new Vector2(x * tileSize, y * tileSize);
    }
}
