using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds Unity meshes from chunk data with face culling and batched processing.
/// Now supports cross-chunk face culling for optimal performance.
/// </summary>
public class ChunkMeshBuilder
{
    private readonly ChunkUVProvider uvProvider;

    // Adjacent chunk data snapshot for thread-safe cross-chunk face culling.
    // Set per BuildMeshFromData call; safe because each ChunkManager owns its own ChunkMeshBuilder.
    private Dictionary<Vector2Int, ChunkMeshData> _adjacentChunkData;

    public ChunkMeshBuilder(ChunkUVProvider uvProvider = null)
    {
        this.uvProvider = uvProvider ?? new ChunkUVProvider(1);
    }

    public PreparedChunkMesh BuildMeshFromData(ChunkMeshData data, Dictionary<Vector2Int, ChunkMeshData> adjacentChunkData = null)
    {
        _adjacentChunkData = adjacentChunkData;
        var submeshes = new Dictionary<string, SubmeshBuild>();
        var blockPositions = new List<(Vector3 position, string blockType)>();

        for (int x = 0; x < data.chunkSize; x++)
        {
            for (int y = 0; y < data.maxHeight; y++)
            {
                for (int z = 0; z < data.chunkSize; z++)
                {
                    string blockName = data.GetBlock(x, y, z);
                    if (blockName == null) continue;

                    // FILTER: Skip turtle blocks - they are spawned separately as entities
                    if (IsTurtleBlock(blockName)) continue;

                    if (!submeshes.TryGetValue(blockName, out var sb))
                        submeshes[blockName] = sb = new SubmeshBuild();

                    // CRITICAL FIX: Y-offset consistent with ChunkMeshData
                    // blockGrid index y is converted to Unity world coordinates: y - 128
                    // This ensures mesh position and ChunkInfo position match
                    float wx = -(data.coord.x * data.chunkSize + x);
                    float wy = y - 128; // Y-Offset: blockGrid Index → Unity Y
                    float wz = data.coord.y * data.chunkSize + z;

                    Vector3 worldPos = new Vector3(wx, wy, wz);

                    // Store block positions for ChunkInfo (same coordinates as mesh)
                    blockPositions.Add((worldPos, blockName));

                    // ENHANCED: Check if this block needs special rendering (cross-shaped plants, chains, etc.)
                    if (IsCrossPlantBlock(blockName))
                    {
                        // Add cross-shaped mesh for plants
                        AddCrossPlantMesh(sb, worldPos, blockName);
                    }
                    else if (IsChainBlock(blockName))
                    {
                        // Add chain mesh (simplified for now)
                        AddChainMesh(sb, worldPos, blockName);
                    }
                    else
                    {
                        // Standard cube with face culling
                        bool[] visibleFaces = GetVisibleFaces(data, x, y, z);
                        AddCubeFaces(sb, worldPos, visibleFaces, blockName);
                    }
                }
            }
        }

        var result = submeshes.Count == 0 ? new PreparedChunkMesh() : PreparedChunkMesh.FromBuild(submeshes);
        result.blockPositions = blockPositions;
        return result;
    }

    private bool[] GetVisibleFaces(ChunkMeshData data, int x, int y, int z)
    {
        return new bool[6]
        {
            // Front (Z+)
            z == data.chunkSize - 1 ? !HasBlockInAdjacentChunk(data, x, y, z, 0, 0, 1) : !data.HasBlock(x, y, z + 1),
            // Back (Z-)
            z == 0 ? !HasBlockInAdjacentChunk(data, x, y, z, 0, 0, -1) : !data.HasBlock(x, y, z - 1),
            // Top (Y+)
            y == data.maxHeight - 1 || !data.HasBlock(x, y + 1, z),
            // Bottom (Y-)
            y == 0 || !data.HasBlock(x, y - 1, z),
            // Left (X-) - CORRECTED: was x == 0, now x == data.chunkSize - 1
            x == data.chunkSize - 1 ? !HasBlockInAdjacentChunk(data, x, y, z, 1, 0, 0) : !data.HasBlock(x + 1, y, z),
            // Right (X+) - CORRECTED: was x == data.chunkSize - 1, now x == 0
            x == 0 ? !HasBlockInAdjacentChunk(data, x, y, z, -1, 0, 0) : !data.HasBlock(x - 1, y, z)
        };
    }

    /// <summary>
    /// Checks if there's a block in an adjacent chunk at the given position.
    /// Returns true if a block exists (face should be hidden), false if not (face should be visible).
    /// Thread-safe: reads from pre-snapshotted ChunkMeshData instead of Unity objects.
    /// </summary>
    private bool HasBlockInAdjacentChunk(ChunkMeshData data, int x, int y, int z, int chunkOffsetX, int chunkOffsetY, int chunkOffsetZ)
    {
        if (_adjacentChunkData == null)
            return false;

        Vector2Int adjacentChunkCoord = new Vector2Int(
            data.coord.x + chunkOffsetX,
            data.coord.y + chunkOffsetZ
        );

        if (!_adjacentChunkData.TryGetValue(adjacentChunkCoord, out var adjData))
            return false;

        // Calculate local position in the adjacent chunk
        int adjX = (x + (chunkOffsetX < 0 ? data.chunkSize - 1 : chunkOffsetX > 0 ? -data.chunkSize + 1 : 0)) % data.chunkSize;
        int adjZ = (z + (chunkOffsetZ < 0 ? data.chunkSize - 1 : chunkOffsetZ > 0 ? -data.chunkSize + 1 : 0)) % data.chunkSize;

        if (adjX < 0) adjX += data.chunkSize;
        if (adjZ < 0) adjZ += data.chunkSize;

        return adjData.HasBlock(adjX, y, adjZ);
    }

    private void AddCubeFaces(SubmeshBuild sb, Vector3 center, bool[] visibleFaces, string blockName)
    {
        // Cube corner vertices
        Vector3[] corners = new Vector3[8]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), // 0: left-bottom-back
            new Vector3( 0.5f, -0.5f, -0.5f), // 1: right-bottom-back
            new Vector3( 0.5f,  0.5f, -0.5f), // 2: right-top-back
            new Vector3(-0.5f,  0.5f, -0.5f), // 3: left-top-back
            new Vector3(-0.5f, -0.5f,  0.5f), // 4: left-bottom-front
            new Vector3( 0.5f, -0.5f,  0.5f), // 5: right-bottom-front
            new Vector3( 0.5f,  0.5f,  0.5f), // 6: right-top-front
            new Vector3(-0.5f,  0.5f,  0.5f)  // 7: left-top-front
        };

        // Face vertex indices
        int[,] faceVertices = new int[6, 4]
        {
            {4, 5, 6, 7}, // Front (Z+)
            {1, 0, 3, 2}, // Back (Z-)
            {7, 6, 2, 3}, // Top (Y+)
            {0, 1, 5, 4}, // Bottom (Y-)
            {0, 4, 7, 3}, // Left (X-) 
            {5, 1, 2, 6}  // Right (X+)
        };

        int[] faceTriangles = new int[6] { 0, 1, 2, 0, 2, 3 };

        for (int face = 0; face < 6; face++)
        {
            if (!visibleFaces[face]) continue;

            int startVertex = sb.vertices.Count;
            Vector2[] faceUVs = uvProvider.GetUVsForBlockFace(blockName, face);

            // Add vertices for this face
            for (int i = 0; i < 4; i++)
            {
                Vector3 vertex = corners[faceVertices[face, i]] + center;
                sb.vertices.Add(vertex);
                sb.uvs.Add(faceUVs[i]);
            }

            // Add triangle indices
            for (int i = 0; i < 6; i++)
            {
                sb.tris.Add(startVertex + faceTriangles[i]);
            }
        }
    }

    public IEnumerator ApplyMeshBatched(Mesh mesh, PreparedChunkMesh prepared, int batchVerticesPerFrame = 10000)
    {
        var allVerts = new List<Vector3>(prepared.totalVertexCount);
        var allUvs = new List<Vector2>(prepared.totalVertexCount);
        var submeshTriangles = new List<int>[prepared.SubmeshCount];
        
        for (int i = 0; i < prepared.SubmeshCount; i++)
            submeshTriangles[i] = new List<int>(prepared.submeshBuilds[i].tris.Count);

        int vertexOffset = 0;
        int processedVertices = 0;

        for (int si = 0; si < prepared.SubmeshCount; si++)
        {
            var sb = prepared.submeshBuilds[si];
            allVerts.AddRange(sb.vertices);
            allUvs.AddRange(sb.uvs);

            foreach (var idx in sb.tris)
                submeshTriangles[si].Add(idx + vertexOffset);

            vertexOffset += sb.vertices.Count;
            processedVertices += sb.vertices.Count;

            if (processedVertices >= batchVerticesPerFrame)
            {
                processedVertices = 0;
                yield return null;
            }
        }

        mesh.SetVertices(allVerts);
        mesh.SetUVs(0, allUvs);
        for (int si = 0; si < prepared.SubmeshCount; si++)
            mesh.SetTriangles(submeshTriangles[si], si, false);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// Checks if a block should be rendered as a cross-shaped plant
    /// Based on Minecraft 1.21 block/cross.json and block/tinted_cross.json models
    /// </summary>
    private bool IsCrossPlantBlock(string blockName)
    {
        string lower = blockName.ToLowerInvariant();

        // Flowers (use block/cross.json)
        if (lower.Contains("poppy") || lower.Contains("dandelion") ||
            lower.Contains("orchid") || lower.Contains("allium") ||
            lower.Contains("tulip") || lower.Contains("daisy") ||
            lower.Contains("cornflower") || lower.Contains("lily_of_the_valley") ||
            lower.Contains("wither_rose") || lower.Contains("torchflower") ||
            lower.Contains("pink_petals"))
        {
            return true;
        }

        // Saplings
        if (lower.Contains("sapling"))
        {
            return true;
        }

        // Tall grass, ferns, dead bush (use block/tinted_cross.json)
        if ((lower.Contains("tall_grass") || lower.Contains("fern") ||
             lower.Contains("dead_bush") || lower.Contains("warped_roots") ||
             lower.Contains("crimson_roots")) && !lower.Contains("block"))
        {
            return true;
        }

        // Crops
        if (lower.Contains("wheat") || lower.Contains("carrots") ||
            lower.Contains("potatoes") || lower.Contains("beetroots") ||
            lower.Contains("nether_wart") || lower.Contains("sweet_berry"))
        {
            return true;
        }

        // Mushrooms (not mushroom blocks or stems)
        if ((lower.Contains("mushroom") || lower.Contains("fungus")) &&
            !lower.Contains("block") && !lower.Contains("stem"))
        {
            return true;
        }

        // Sugar cane, bamboo sapling
        if (lower.Contains("sugar_cane") || lower.Contains("bamboo_sapling"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a block is a chain
    /// </summary>
    private bool IsChainBlock(string blockName)
    {
        string lower = blockName.ToLowerInvariant();
        return lower.Contains("chain");
    }

    /// <summary>
    /// Checks if a block is a ComputerCraft turtle
    /// Turtles are spawned separately as entities, so they should not be rendered in chunks
    /// </summary>
    private bool IsTurtleBlock(string blockName)
    {
        string lower = blockName.ToLowerInvariant();

        // ComputerCraft / CC:Tweaked turtle blocks
        // Examples: "computercraft:turtle", "computercraft:turtle_advanced", "cc:turtle"
        return lower.Contains("turtle");
    }

    /// <summary>
    /// Adds a cross-shaped plant mesh (two intersecting quads forming an X)
    /// Based on Minecraft's block/cross.json model
    /// </summary>
    private void AddCrossPlantMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Cross mesh: 2 perpendicular quads from corner to corner
        float sqrt2 = Mathf.Sqrt(2f);
        float diagonalHalf = 0.5f * sqrt2;

        int startIdx = sb.vertices.Count;

        // First diagonal (from -X-Z to +X+Z)
        // Front face
        sb.vertices.Add(center + new Vector3(-diagonalHalf, 0, -diagonalHalf));
        sb.vertices.Add(center + new Vector3(diagonalHalf, 0, diagonalHalf));
        sb.vertices.Add(center + new Vector3(diagonalHalf, 1, diagonalHalf));
        sb.vertices.Add(center + new Vector3(-diagonalHalf, 1, -diagonalHalf));

        sb.uvs.Add(new Vector2(0, 0));
        sb.uvs.Add(new Vector2(1, 0));
        sb.uvs.Add(new Vector2(1, 1));
        sb.uvs.Add(new Vector2(0, 1));

        // Triangles (front face)
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 2); sb.tris.Add(startIdx + 1);
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 3); sb.tris.Add(startIdx + 2);

        // Back face (same vertices, reversed winding)
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 1); sb.tris.Add(startIdx + 2);
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 2); sb.tris.Add(startIdx + 3);

        startIdx = sb.vertices.Count;

        // Second diagonal (from +X-Z to -X+Z)
        // Front face
        sb.vertices.Add(center + new Vector3(diagonalHalf, 0, -diagonalHalf));
        sb.vertices.Add(center + new Vector3(-diagonalHalf, 0, diagonalHalf));
        sb.vertices.Add(center + new Vector3(-diagonalHalf, 1, diagonalHalf));
        sb.vertices.Add(center + new Vector3(diagonalHalf, 1, -diagonalHalf));

        sb.uvs.Add(new Vector2(0, 0));
        sb.uvs.Add(new Vector2(1, 0));
        sb.uvs.Add(new Vector2(1, 1));
        sb.uvs.Add(new Vector2(0, 1));

        // Triangles (front face)
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 2); sb.tris.Add(startIdx + 1);
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 3); sb.tris.Add(startIdx + 2);

        // Back face (reversed winding)
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 1); sb.tris.Add(startIdx + 2);
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 2); sb.tris.Add(startIdx + 3);
    }

    /// <summary>
    /// Adds a chain mesh (simplified vertical chain for now)
    /// TODO: Expand to support horizontal chains and proper chain models
    /// </summary>
    private void AddChainMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Simplified chain: thin vertical box
        float thickness = 0.1f;

        int startIdx = sb.vertices.Count;

        // Create a thin vertical box
        Vector3[] corners = new Vector3[8]
        {
            new Vector3(-thickness, 0f, -thickness),
            new Vector3( thickness, 0f, -thickness),
            new Vector3( thickness, 1f, -thickness),
            new Vector3(-thickness, 1f, -thickness),
            new Vector3(-thickness, 0f,  thickness),
            new Vector3( thickness, 0f,  thickness),
            new Vector3( thickness, 1f,  thickness),
            new Vector3(-thickness, 1f,  thickness)
        };

        // Add vertices
        foreach (var corner in corners)
        {
            sb.vertices.Add(center + corner);
        }

        // Add UVs (simple mapping)
        for (int i = 0; i < 8; i++)
        {
            sb.uvs.Add(new Vector2(0.5f, 0.5f));
        }

        // Add faces (all 6 faces, no culling for chains)
        // Front face
        sb.tris.Add(startIdx + 4); sb.tris.Add(startIdx + 5); sb.tris.Add(startIdx + 6);
        sb.tris.Add(startIdx + 4); sb.tris.Add(startIdx + 6); sb.tris.Add(startIdx + 7);

        // Back face
        sb.tris.Add(startIdx + 1); sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 3);
        sb.tris.Add(startIdx + 1); sb.tris.Add(startIdx + 3); sb.tris.Add(startIdx + 2);

        // Top face
        sb.tris.Add(startIdx + 7); sb.tris.Add(startIdx + 6); sb.tris.Add(startIdx + 2);
        sb.tris.Add(startIdx + 7); sb.tris.Add(startIdx + 2); sb.tris.Add(startIdx + 3);

        // Bottom face
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 1); sb.tris.Add(startIdx + 5);
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 5); sb.tris.Add(startIdx + 4);

        // Left face
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 4); sb.tris.Add(startIdx + 7);
        sb.tris.Add(startIdx + 0); sb.tris.Add(startIdx + 7); sb.tris.Add(startIdx + 3);

        // Right face
        sb.tris.Add(startIdx + 5); sb.tris.Add(startIdx + 1); sb.tris.Add(startIdx + 2);
        sb.tris.Add(startIdx + 5); sb.tris.Add(startIdx + 2); sb.tris.Add(startIdx + 6);
    }
}

// Supporting classes
public class SubmeshBuild
{
    public readonly List<Vector3> vertices = new(1024);
    public readonly List<Vector2> uvs = new(1024);
    public readonly List<int> tris = new(2048);
}

public class PreparedChunkMesh
{
    public int SubmeshCount => submeshBuilds.Count;
    public int totalVertexCount;
    public readonly List<SubmeshBuild> submeshBuilds = new();
    public readonly List<string> blockNames = new();
    public List<(Vector3 position, string blockType)> blockPositions = new();

    public static PreparedChunkMesh FromBuild(Dictionary<string, SubmeshBuild> dict)
    {
        var p = new PreparedChunkMesh();
        foreach (var kv in dict)
        {
            p.blockNames.Add(kv.Key);
            p.submeshBuilds.Add(kv.Value);
            p.totalVertexCount += kv.Value.vertices.Count;
        }
        return p;
    }
}