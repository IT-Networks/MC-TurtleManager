using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container for chunk mesh data with block grid information.
/// Erweitert um Block-Modifikationsfunktionalität.
/// </summary>
public class ChunkMeshData
{
    // Das blockGrid ist nicht mehr readonly, damit wir es modifizieren können
    private readonly string[,,] blockGrid;
    public readonly Vector2Int coord;
    public readonly int chunkSize;
    public readonly int maxHeight;
    
    // Flag um zu verfolgen, ob die Daten verändert wurden
    private bool isModified = false;

    public ChunkMeshData(string[,,] blockGrid, Vector2Int coord, int chunkSize, int maxHeight = 400)
    {
        this.blockGrid = blockGrid;
        this.coord = coord;
        this.chunkSize = chunkSize;
        this.maxHeight = maxHeight;
    }

    /// <summary>
    /// Erstellt eine Kopie für sichere Modifikationen.
    /// </summary>
    /// <param name="original">Original ChunkMeshData</param>
    private ChunkMeshData(ChunkMeshData original)
    {
        this.coord = original.coord;
        this.chunkSize = original.chunkSize;
        this.maxHeight = original.maxHeight;
        this.isModified = false;
        
        // Deep copy des blockGrid
        this.blockGrid = new string[chunkSize, maxHeight, chunkSize];
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    this.blockGrid[x, y, z] = original.blockGrid[x, y, z];
                }
            }
        }
    }

    public string GetBlock(int x, int y, int z)
    {
        if (!IsValidPosition(x, y, z))
            return null;
        return blockGrid[x, y, z];
    }

    public bool HasBlock(int x, int y, int z)
    {
        return GetBlock(x, y, z) != null;
    }

    /// <summary>
    /// Setzt einen Block an der angegebenen Position.
    /// </summary>
    /// <param name="x">X-Koordinate im Chunk (0 bis chunkSize-1)</param>
    /// <param name="y">Y-Koordinate im Chunk (0 bis maxHeight-1)</param>
    /// <param name="z">Z-Koordinate im Chunk (0 bis chunkSize-1)</param>
    /// <param name="blockType">Blocktyp (null zum Entfernen)</param>
    /// <returns>True wenn erfolgreich gesetzt</returns>
    public bool SetBlock(int x, int y, int z, string blockType)
    {
        if (!IsValidPosition(x, y, z))
        {
            Debug.LogWarning($"ChunkMeshData: Invalid position ({x}, {y}, {z}) for chunk {coord}");
            return false;
        }

        string oldBlockType = blockGrid[x, y, z];
        
        // Nur setzen wenn sich der Wert tatsächlich ändert
        if (oldBlockType != blockType)
        {
            blockGrid[x, y, z] = blockType;
            isModified = true;
            
            Debug.Log($"ChunkMeshData: Block at ({x}, {y}, {z}) changed from '{oldBlockType}' to '{blockType}'");
        }

        return true;
    }

    /// <summary>
    /// Überprüft, ob die Position gültig ist.
    /// </summary>
    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < chunkSize &&
               y >= 0 && y < maxHeight &&
               z >= 0 && z < chunkSize;
    }

    /// <summary>
    /// Gibt zurück, ob die Chunk-Daten modifiziert wurden.
    /// </summary>
    public bool IsModified => isModified;

    /// <summary>
    /// Markiert die Daten als unverändert (z.B. nach dem Speichern).
    /// </summary>
    public void MarkAsUnmodified()
    {
        isModified = false;
    }

    /// <summary>
    /// Erstellt eine tiefe Kopie der ChunkMeshData.
    /// </summary>
    /// <returns>Kopie der ChunkMeshData</returns>
    public ChunkMeshData Clone()
    {
        return new ChunkMeshData(this);
    }

    /// <summary>
    /// Zählt die Anzahl der Blöcke im Chunk.
    /// </summary>
    /// <returns>Anzahl der Blöcke</returns>
    public int CountBlocks()
    {
        int count = 0;
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    if (blockGrid[x, y, z] != null)
                        count++;
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Überprüft, ob der Chunk komplett leer ist.
    /// </summary>
    /// <returns>True wenn keine Blöcke vorhanden sind</returns>
    public bool IsEmpty()
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    if (blockGrid[x, y, z] != null)
                        return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Gibt Statistiken über die Blocktypen zurück.
    /// </summary>
    /// <returns>Dictionary mit Blocktyp-Statistiken</returns>
    public Dictionary<string, int> GetBlockTypeStatistics()
    {
        var stats = new Dictionary<string, int>();
        
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    string blockType = blockGrid[x, y, z];
                    if (blockType != null)
                    {
                        if (stats.ContainsKey(blockType))
                            stats[blockType]++;
                        else
                            stats[blockType] = 1;
                    }
                }
            }
        }
        
        return stats;
    }

    /// <summary>
    /// Entfernt alle Blöcke eines bestimmten Typs.
    /// </summary>
    /// <param name="blockType">Der zu entfernende Blocktyp</param>
    /// <returns>Anzahl der entfernten Blöcke</returns>
    public int RemoveAllBlocksOfType(string blockType)
    {
        int removedCount = 0;
        
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    if (blockGrid[x, y, z] == blockType)
                    {
                        blockGrid[x, y, z] = null;
                        removedCount++;
                        isModified = true;
                    }
                }
            }
        }
        
        if (removedCount > 0)
        {
            Debug.Log($"ChunkMeshData: Removed {removedCount} blocks of type '{blockType}' from chunk {coord}");
        }
        
        return removedCount;
    }

    /// <summary>
    /// Ersetzt alle Blöcke eines Typs durch einen anderen.
    /// </summary>
    /// <param name="oldBlockType">Alter Blocktyp</param>
    /// <param name="newBlockType">Neuer Blocktyp</param>
    /// <returns>Anzahl der ersetzten Blöcke</returns>
    public int ReplaceBlockType(string oldBlockType, string newBlockType)
    {
        int replacedCount = 0;
        
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    if (blockGrid[x, y, z] == oldBlockType)
                    {
                        blockGrid[x, y, z] = newBlockType;
                        replacedCount++;
                        isModified = true;
                    }
                }
            }
        }
        
        if (replacedCount > 0)
        {
            Debug.Log($"ChunkMeshData: Replaced {replacedCount} blocks from '{oldBlockType}' to '{newBlockType}' in chunk {coord}");
        }
        
        return replacedCount;
    }

    /// <summary>
    /// Füllt einen kubischen Bereich mit einem Blocktyp.
    /// </summary>
    /// <param name="startX">Start X-Koordinate</param>
    /// <param name="startY">Start Y-Koordinate</param>
    /// <param name="startZ">Start Z-Koordinate</param>
    /// <param name="endX">End X-Koordinate (exklusiv)</param>
    /// <param name="endY">End Y-Koordinate (exklusiv)</param>
    /// <param name="endZ">End Z-Koordinate (exklusiv)</param>
    /// <param name="blockType">Blocktyp (null zum Entfernen)</param>
    /// <returns>Anzahl der geänderten Blöcke</returns>
    public int FillRegion(int startX, int startY, int startZ, int endX, int endY, int endZ, string blockType)
    {
        int changedCount = 0;
        
        // Clamp Werte zu gültigen Bereichen
        startX = Mathf.Max(0, startX);
        startY = Mathf.Max(0, startY);
        startZ = Mathf.Max(0, startZ);
        endX = Mathf.Min(chunkSize, endX);
        endY = Mathf.Min(maxHeight, endY);
        endZ = Mathf.Min(chunkSize, endZ);
        
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                for (int z = startZ; z < endZ; z++)
                {
                    if (blockGrid[x, y, z] != blockType)
                    {
                        blockGrid[x, y, z] = blockType;
                        changedCount++;
                        isModified = true;
                    }
                }
            }
        }
        
        if (changedCount > 0)
        {
            Debug.Log($"ChunkMeshData: Filled region ({startX},{startY},{startZ}) to ({endX},{endY},{endZ}) with '{blockType}', {changedCount} blocks changed");
        }
        
        return changedCount;
    }

    public List<Vector3> GetAllBlockPositions()
    {
        var positions = new List<Vector3>();
        
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    if (blockGrid[x, y, z] != null)
                    {
                        float wx = coord.x * chunkSize + x;
                        float wy = y - 128; // Adjust for world height offset
                        float wz = coord.y * chunkSize + z;
                        positions.Add(new Vector3(wx, wy, wz));
                    }
                }
            }
        }
        
        return positions;
    }

    /// <summary>
    /// Gibt alle Block-Positionen eines bestimmten Typs zurück.
    /// </summary>
    /// <param name="blockType">Der gesuchte Blocktyp</param>
    /// <returns>Liste der Weltpositionen</returns>
    public List<Vector3> GetBlockPositionsOfType(string blockType)
    {
        var positions = new List<Vector3>();
        
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < maxHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    if (blockGrid[x, y, z] == blockType)
                    {
                        float wx = coord.x * chunkSize + x;
                        float wy = y - 128; // Adjust for world height offset
                        float wz = coord.y * chunkSize + z;
                        positions.Add(new Vector3(wx, wy, wz));
                    }
                }
            }
        }
        
        return positions;
    }

    /// <summary>
    /// Gibt die lokalen Koordinaten für eine Weltposition zurück.
    /// </summary>
    /// <param name="worldPosition">Weltposition</param>
    /// <returns>Lokale Chunk-Koordinaten oder null wenn außerhalb</returns>
    public Vector3Int? WorldToLocalPosition(Vector3 worldPosition)
    {
        int localX = Mathf.FloorToInt(-worldPosition.x) - (coord.x * chunkSize);
        int localY = Mathf.FloorToInt(worldPosition.y + 128); // Adjust for world height offset
        int localZ = Mathf.FloorToInt(worldPosition.z) - (coord.y * chunkSize);

        if (IsValidPosition(localX, localY, localZ))
        {
            return new Vector3Int(localX, localY, localZ);
        }
        
        return null;
    }

    /// <summary>
    /// Konvertiert lokale Koordinaten zu Weltposition.
    /// </summary>
    /// <param name="localX">Lokale X-Koordinate</param>
    /// <param name="localY">Lokale Y-Koordinate</param>
    /// <param name="localZ">Lokale Z-Koordinate</param>
    /// <returns>Weltposition</returns>
    public Vector3 LocalToWorldPosition(int localX, int localY, int localZ)
    {
        float wx = -(coord.x * chunkSize + localX);
        float wy = localY - 128; // Adjust for world height offset
        float wz = coord.y * chunkSize + localZ;
        
        return new Vector3(wx, wy, wz);
    }
}