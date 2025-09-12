using UnityEngine;

/// <summary>
/// Provides UV coordinates for different block types and faces.
/// Handles texture atlas mapping.
/// </summary>
public class ChunkUVProvider
{
    private readonly float tileSize;
    private readonly int atlasSize;

    public ChunkUVProvider(int atlasSize = 16)
    {
        this.atlasSize = atlasSize;
        this.tileSize = 1.0f / atlasSize;
    }

    public Vector2[] GetUVsForBlockFace(string blockName, int faceIndex)
    {
        // Local UV coordinates within a tile
        Vector2[] localUVs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        // Get tile offset for this block and face
        Vector2 tileOffset = GetTileOffset(blockName, faceIndex);

        // Scale and translate UVs
        Vector2[] result = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            result[i] = tileOffset + localUVs[i] * tileSize;
        }

        return result;
    }

    private Vector2 GetTileOffset(string blockName, int faceIndex)
    {
        int x = 0, y = 0;

        switch (blockName.ToLower())
        {
            case "minecraft:grass":
                switch (faceIndex)
                {
                    case 2: x = 0; y = 15; break; // Top
                    case 3: x = 2; y = 15; break; // Bottom (dirt)
                    default: x = 1; y = 15; break; // Side
                }
                break;
                
            case "minecraft:stone":
                x = 3; y = 15;
                break;
                
            case "minecraft:wood":
            case "minecraft:log":
                switch (faceIndex)
                {
                    case 2: // Top
                    case 3: // Bottom
                        x = 4; y = 15; 
                        break;
                    default: // Side
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
                // Default texture (maybe a "missing texture" tile)
                x = 0; y = 0;
                break;
        }

        return new Vector2(x * tileSize, y * tileSize);
    }

    /// <summary>
    /// Adds a custom block texture mapping
    /// </summary>
    public void AddCustomBlockMapping(string blockName, int atlasX, int atlasY)
    {
        // This could be extended to use a dictionary for custom mappings
        // For now, modify the GetTileOffset method directly
    }

    /// <summary>
    /// Adds a custom block texture mapping with different textures per face
    /// </summary>
    public void AddCustomBlockMapping(string blockName, 
        Vector2Int top, Vector2Int bottom, Vector2Int side)
    {
        // This could be extended for more complex mapping systems
    }
}