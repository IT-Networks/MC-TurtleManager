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

    // Precomputed model type grid — avoids repeated string parsing in face culling.
    // Built once at the start of BuildMeshFromData, indexed as [x, y, z].
    private BlockModelType[,,] _modelTypeGrid;

    public ChunkMeshBuilder(ChunkUVProvider uvProvider = null)
    {
        this.uvProvider = uvProvider ?? new ChunkUVProvider(1);
    }

    public PreparedChunkMesh BuildMeshFromData(ChunkMeshData data, Dictionary<Vector2Int, ChunkMeshData> adjacentChunkData = null)
    {
        _adjacentChunkData = adjacentChunkData;
        var submeshes = new Dictionary<string, SubmeshBuild>();
        var blockPositions = new List<(Vector3 position, string blockType)>();

        // Precompute model types for all blocks in one pass.
        // This avoids repeated GetModelType() string parsing during face culling
        // (6 neighbor lookups per block), reducing ~180K string ops to ~65K.
        _modelTypeGrid = new BlockModelType[data.chunkSize, data.maxHeight, data.chunkSize];
        for (int px = 0; px < data.chunkSize; px++)
            for (int py = 0; py < data.maxHeight; py++)
                for (int pz = 0; pz < data.chunkSize; pz++)
                {
                    string name = data.GetBlock(px, py, pz);
                    _modelTypeGrid[px, py, pz] = name != null ? BlockModelDetector.GetModelType(name) : BlockModelType.Air;
                }

        for (int x = 0; x < data.chunkSize; x++)
        {
            for (int y = 0; y < data.maxHeight; y++)
            {
                for (int z = 0; z < data.chunkSize; z++)
                {
                    string blockName = data.GetBlock(x, y, z);
                    if (blockName == null) continue;

                    BlockModelType modelType = _modelTypeGrid[x, y, z];

                    // Skip turtle blocks and air
                    if (modelType == BlockModelType.Air) continue;
                    if (IsTurtleBlock(blockName)) continue;

                    if (!submeshes.TryGetValue(blockName, out var sb))
                        submeshes[blockName] = sb = new SubmeshBuild();

                    float wx = -(data.coord.x * data.chunkSize + x);
                    float wy = y - 128;
                    float wz = data.coord.y * data.chunkSize + z;

                    Vector3 worldPos = new Vector3(wx, wy, wz);

                    blockPositions.Add((worldPos, blockName));
                    switch (modelType)
                    {
                        case BlockModelType.Air:
                            break;
                        case BlockModelType.CrossPlant:
                        case BlockModelType.TintedCross:
                            AddCrossPlantMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Chain:
                            AddChainMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Slab:
                            AddSlabMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Stairs:
                            AddStairsMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Door:
                            AddDoorMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Trapdoor:
                            AddTrapdoorMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Fence:
                        case BlockModelType.FenceGate:
                            AddFenceMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Wall:
                            AddWallMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Pane:
                            AddPaneMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Carpet:
                            AddFlatMesh(sb, worldPos, blockName, 0.0625f);
                            break;
                        case BlockModelType.PressurePlate:
                            AddFlatMesh(sb, worldPos, blockName, 0.0625f);
                            break;
                        case BlockModelType.SnowLayer:
                            AddFlatMesh(sb, worldPos, blockName, 0.125f);
                            break;
                        case BlockModelType.Button:
                            AddButtonMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Torch:
                        case BlockModelType.Lever:
                            AddTorchMesh(sb, worldPos, blockName);
                            break;
                        case BlockModelType.Chest:
                            AddShrunkCubeMesh(sb, worldPos, blockName, 0.875f);
                            break;
                        case BlockModelType.Pipe:
                        case BlockModelType.Cable:
                        case BlockModelType.Conduit:
                            AddPipeMesh(sb, worldPos, blockName, modelType);
                            break;
                        case BlockModelType.Gear:
                            AddFlatMesh(sb, worldPos, blockName, 0.2f);
                            break;
                        case BlockModelType.Belt:
                            AddFlatMesh(sb, worldPos, blockName, 0.125f);
                            break;
                        case BlockModelType.Liquid:
                            AddLiquidMesh(sb, worldPos, blockName, data, x, y, z);
                            break;
                        default: // Cube, Mechanical, Furniture, Bed, etc.
                            bool[] visibleFaces = GetVisibleFaces(data, x, y, z);
                            AddCubeFaces(sb, worldPos, visibleFaces, blockName);
                            break;
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
        // Non-full blocks (fences, torches, etc.) should NOT cause neighbor faces to be culled.
        // Uses precomputed _modelTypeGrid for O(1) lookups instead of string parsing.
        return new bool[6]
        {
            // Front (Z+)
            z == data.chunkSize - 1 ? !IsFullBlockInAdjacentChunk(data, x, y, z, 0, 0, 1) : !IsFullBlockType(_modelTypeGrid[x, y, z + 1]),
            // Back (Z-)
            z == 0 ? !IsFullBlockInAdjacentChunk(data, x, y, z, 0, 0, -1) : !IsFullBlockType(_modelTypeGrid[x, y, z - 1]),
            // Top (Y+)
            y == data.maxHeight - 1 || !IsFullBlockType(_modelTypeGrid[x, y + 1, z]),
            // Bottom (Y-)
            y == 0 || !IsFullBlockType(_modelTypeGrid[x, y - 1, z]),
            // Left (X-)
            x == data.chunkSize - 1 ? !IsFullBlockInAdjacentChunk(data, x, y, z, 1, 0, 0) : !IsFullBlockType(_modelTypeGrid[x + 1, y, z]),
            // Right (X+)
            x == 0 ? !IsFullBlockInAdjacentChunk(data, x, y, z, -1, 0, 0) : !IsFullBlockType(_modelTypeGrid[x - 1, y, z])
        };
    }

    private static bool IsFullBlockType(BlockModelType type)
    {
        return type == BlockModelType.Cube || type == BlockModelType.Mechanical ||
               type == BlockModelType.Furniture || type == BlockModelType.Bed;
    }

    /// <summary>
    /// Cross-chunk face culling: checks if the adjacent chunk has a full opaque block.
    /// Falls back to string-based GetModelType only for the single cross-chunk boundary block.
    /// </summary>
    private bool IsFullBlockInAdjacentChunk(ChunkMeshData data, int x, int y, int z, int chunkOffsetX, int chunkOffsetY, int chunkOffsetZ)
    {
        if (_adjacentChunkData == null)
            return false;

        Vector2Int adjacentChunkCoord = new Vector2Int(
            data.coord.x + chunkOffsetX,
            data.coord.y + chunkOffsetZ
        );

        if (!_adjacentChunkData.TryGetValue(adjacentChunkCoord, out var adjData))
            return false;

        int adjX = (x + (chunkOffsetX < 0 ? data.chunkSize - 1 : chunkOffsetX > 0 ? -data.chunkSize + 1 : 0)) % data.chunkSize;
        int adjZ = (z + (chunkOffsetZ < 0 ? data.chunkSize - 1 : chunkOffsetZ > 0 ? -data.chunkSize + 1 : 0)) % data.chunkSize;

        if (adjX < 0) adjX += data.chunkSize;
        if (adjZ < 0) adjZ += data.chunkSize;

        string blockName = adjData.GetBlock(adjX, y, adjZ);
        if (blockName == null) return false;
        return IsFullBlockType(BlockModelDetector.GetModelType(blockName));
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

    // ========================================================================
    // Reusable box helper — building block for slabs, doors, fences, etc.
    // min/max are local offsets from center (e.g. -0.5..0.5 = full block).
    // ========================================================================

    private void AddBoxToSubmesh(SubmeshBuild sb, Vector3 center, Vector3 min, Vector3 max)
    {
        int si = sb.vertices.Count;

        // 8 corners of the box
        Vector3 v0 = center + new Vector3(min.x, min.y, min.z);
        Vector3 v1 = center + new Vector3(max.x, min.y, min.z);
        Vector3 v2 = center + new Vector3(max.x, max.y, min.z);
        Vector3 v3 = center + new Vector3(min.x, max.y, min.z);
        Vector3 v4 = center + new Vector3(min.x, min.y, max.z);
        Vector3 v5 = center + new Vector3(max.x, min.y, max.z);
        Vector3 v6 = center + new Vector3(max.x, max.y, max.z);
        Vector3 v7 = center + new Vector3(min.x, max.y, max.z);

        // Calculate UV scale based on box dimensions (map texture proportionally)
        float sx = max.x - min.x;
        float sy = max.y - min.y;
        float sz = max.z - min.z;

        // Front face (Z+): v4 v5 v6 v7
        AddQuad(sb, v4, v5, v6, v7, 0f, sx, 0f, sy);
        // Back face (Z-): v1 v0 v3 v2
        AddQuad(sb, v1, v0, v3, v2, 0f, sx, 0f, sy);
        // Top face (Y+): v7 v6 v2 v3
        AddQuad(sb, v7, v6, v2, v3, 0f, sx, 0f, sz);
        // Bottom face (Y-): v0 v1 v5 v4
        AddQuad(sb, v0, v1, v5, v4, 0f, sx, 0f, sz);
        // Left face (X-): v0 v4 v7 v3
        AddQuad(sb, v0, v4, v7, v3, 0f, sz, 0f, sy);
        // Right face (X+): v5 v1 v2 v6
        AddQuad(sb, v5, v1, v2, v6, 0f, sz, 0f, sy);
    }

    private void AddQuad(SubmeshBuild sb, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                         float u0, float u1, float v0, float v1)
    {
        int si = sb.vertices.Count;
        sb.vertices.Add(a);
        sb.vertices.Add(b);
        sb.vertices.Add(c);
        sb.vertices.Add(d);

        sb.uvs.Add(new Vector2(u0, v0));
        sb.uvs.Add(new Vector2(u1, v0));
        sb.uvs.Add(new Vector2(u1, v1));
        sb.uvs.Add(new Vector2(u0, v1));

        sb.tris.Add(si + 0); sb.tris.Add(si + 2); sb.tris.Add(si + 1);
        sb.tris.Add(si + 0); sb.tris.Add(si + 3); sb.tris.Add(si + 2);
    }

    // ========================================================================
    // Non-cube mesh generators
    // ========================================================================

    private void AddSlabMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Bottom slab by default: half-height box in the lower half of the block
        bool isTop = blockName.ToLowerInvariant().Contains("top");
        if (isTop)
            AddBoxToSubmesh(sb, center, new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));
        else
            AddBoxToSubmesh(sb, center, new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0f, 0.5f));
    }

    private void AddStairsMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Bottom slab (full width, half height)
        AddBoxToSubmesh(sb, center, new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0f, 0.5f));
        // Upper step (half width in Z, upper half height)
        AddBoxToSubmesh(sb, center, new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f));
    }

    private void AddDoorMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Thin tall box: 3/16 thick (0.1875), full height, full width
        float half = 3f / 32f; // half of 3/16
        AddBoxToSubmesh(sb, center, new Vector3(-0.5f, -0.5f, -half), new Vector3(0.5f, 0.5f, half));
    }

    private void AddTrapdoorMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Thin horizontal plate: 3/16 thick, sitting on the bottom of the block
        float thickness = 3f / 16f;
        AddBoxToSubmesh(sb, center, new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f + thickness, 0.5f));
    }

    private void AddFenceMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Center post: 4/16 wide, full height
        float postHalf = 2f / 16f;
        AddBoxToSubmesh(sb, center, new Vector3(-postHalf, -0.5f, -postHalf), new Vector3(postHalf, 0.5f, postHalf));

        // Add 4 horizontal rails (simplified: always connect all 4 directions)
        // In a full implementation, connectivity would be checked against neighbors.
        float railHalf = 1f / 16f;
        float railTop = 0.4375f;    // 7/16 from center
        float railBottom = 0.1875f;  // 3/16 from center

        // +Z rail
        AddBoxToSubmesh(sb, center, new Vector3(-railHalf, railBottom - 0.5f, postHalf), new Vector3(railHalf, railTop - 0.5f + 0.5f, 0.5f));
        // -Z rail
        AddBoxToSubmesh(sb, center, new Vector3(-railHalf, railBottom - 0.5f, -0.5f), new Vector3(railHalf, railTop - 0.5f + 0.5f, -postHalf));
        // +X rail
        AddBoxToSubmesh(sb, center, new Vector3(postHalf, railBottom - 0.5f, -railHalf), new Vector3(0.5f, railTop - 0.5f + 0.5f, railHalf));
        // -X rail
        AddBoxToSubmesh(sb, center, new Vector3(-0.5f, railBottom - 0.5f, -railHalf), new Vector3(-postHalf, railTop - 0.5f + 0.5f, railHalf));
    }

    private void AddWallMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Center post: 8/16 wide (thicker than fence), full height
        float postHalf = 4f / 16f;
        AddBoxToSubmesh(sb, center, new Vector3(-postHalf, -0.5f, -postHalf), new Vector3(postHalf, 0.5f, postHalf));

        // Side extensions (simplified: always connect all 4 directions)
        float wallHalf = 4f / 16f;
        // +Z
        AddBoxToSubmesh(sb, center, new Vector3(-wallHalf, -0.5f, postHalf), new Vector3(wallHalf, 0.4375f, 0.5f));
        // -Z
        AddBoxToSubmesh(sb, center, new Vector3(-wallHalf, -0.5f, -0.5f), new Vector3(wallHalf, 0.4375f, -postHalf));
        // +X
        AddBoxToSubmesh(sb, center, new Vector3(postHalf, -0.5f, -wallHalf), new Vector3(0.5f, 0.4375f, wallHalf));
        // -X
        AddBoxToSubmesh(sb, center, new Vector3(-0.5f, -0.5f, -wallHalf), new Vector3(-postHalf, 0.4375f, wallHalf));
    }

    private void AddPaneMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Thin vertical pane in the center of the block (Z axis), full height, full width
        // 2/16 thick, double-sided (rendered via both winding orders)
        float half = 1f / 16f;

        int si = sb.vertices.Count;

        // Front face (Z+)
        Vector3 v0 = center + new Vector3(-0.5f, -0.5f, half);
        Vector3 v1 = center + new Vector3(0.5f, -0.5f, half);
        Vector3 v2 = center + new Vector3(0.5f, 0.5f, half);
        Vector3 v3 = center + new Vector3(-0.5f, 0.5f, half);

        sb.vertices.Add(v0); sb.vertices.Add(v1); sb.vertices.Add(v2); sb.vertices.Add(v3);
        sb.uvs.Add(new Vector2(0, 0)); sb.uvs.Add(new Vector2(1, 0));
        sb.uvs.Add(new Vector2(1, 1)); sb.uvs.Add(new Vector2(0, 1));

        // Front-facing triangles
        sb.tris.Add(si + 0); sb.tris.Add(si + 2); sb.tris.Add(si + 1);
        sb.tris.Add(si + 0); sb.tris.Add(si + 3); sb.tris.Add(si + 2);
        // Back-facing triangles (reversed winding)
        sb.tris.Add(si + 0); sb.tris.Add(si + 1); sb.tris.Add(si + 2);
        sb.tris.Add(si + 0); sb.tris.Add(si + 2); sb.tris.Add(si + 3);

        si = sb.vertices.Count;

        // Back face (Z-)
        Vector3 v4 = center + new Vector3(-0.5f, -0.5f, -half);
        Vector3 v5 = center + new Vector3(0.5f, -0.5f, -half);
        Vector3 v6 = center + new Vector3(0.5f, 0.5f, -half);
        Vector3 v7 = center + new Vector3(-0.5f, 0.5f, -half);

        sb.vertices.Add(v4); sb.vertices.Add(v5); sb.vertices.Add(v6); sb.vertices.Add(v7);
        sb.uvs.Add(new Vector2(0, 0)); sb.uvs.Add(new Vector2(1, 0));
        sb.uvs.Add(new Vector2(1, 1)); sb.uvs.Add(new Vector2(0, 1));

        sb.tris.Add(si + 0); sb.tris.Add(si + 1); sb.tris.Add(si + 2);
        sb.tris.Add(si + 0); sb.tris.Add(si + 2); sb.tris.Add(si + 3);
        sb.tris.Add(si + 0); sb.tris.Add(si + 2); sb.tris.Add(si + 1);
        sb.tris.Add(si + 0); sb.tris.Add(si + 3); sb.tris.Add(si + 2);
    }

    private void AddFlatMesh(SubmeshBuild sb, Vector3 center, string blockName, float height)
    {
        // Thin block sitting on the bottom of the block space
        AddBoxToSubmesh(sb, center, new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f + height, 0.5f));
    }

    private void AddButtonMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Small box attached to the south face of the block
        float w = 3f / 16f; // half-width
        float h = 1f / 16f; // depth (how far it sticks out)
        float t = 2f / 16f; // half-height
        AddBoxToSubmesh(sb, center, new Vector3(-w, -t, -0.5f), new Vector3(w, t, -0.5f + h));
    }

    private void AddTorchMesh(SubmeshBuild sb, Vector3 center, string blockName)
    {
        // Thin vertical stick
        float stickHalf = 1f / 16f;
        AddBoxToSubmesh(sb, center, new Vector3(-stickHalf, -0.5f, -stickHalf), new Vector3(stickHalf, 0.125f, stickHalf));
        // Small flame/head on top
        float headHalf = 1.5f / 16f;
        AddBoxToSubmesh(sb, center, new Vector3(-headHalf, 0.125f, -headHalf), new Vector3(headHalf, 0.3125f, headHalf));
    }

    private void AddShrunkCubeMesh(SubmeshBuild sb, Vector3 center, string blockName, float scale)
    {
        // Slightly smaller cube centered in the block
        float half = scale * 0.5f;
        AddBoxToSubmesh(sb, center, new Vector3(-half, -0.5f, -half), new Vector3(half, -0.5f + scale, half));
    }

    private void AddPipeMesh(SubmeshBuild sb, Vector3 center, string blockName, BlockModelType modelType)
    {
        // Vertical pipe/cable — thickness varies by type
        float half;
        switch (modelType)
        {
            case BlockModelType.Pipe:   half = 0.125f; break;  // 4/16 diameter
            case BlockModelType.Cable:  half = 0.0625f; break; // 2/16 diameter
            case BlockModelType.Conduit: half = 0.09375f; break; // 3/16 diameter
            default: half = 0.125f; break;
        }
        AddBoxToSubmesh(sb, center, new Vector3(-half, -0.5f, -half), new Vector3(half, 0.5f, half));
    }

    private void AddLiquidMesh(SubmeshBuild sb, Vector3 center, string blockName, ChunkMeshData data, int x, int y, int z)
    {
        // Only render the top surface of liquid — no sides/bottom.
        // Skip entirely if there's liquid above (this block is submerged).
        bool liquidAbove = y + 1 < data.maxHeight && _modelTypeGrid[x, y + 1, z] == BlockModelType.Liquid;
        if (liquidAbove) return;

        float top = 0.375f; // 14/16 height — slightly below full block
        Vector3 v0 = center + new Vector3(-0.5f, top, -0.5f);
        Vector3 v1 = center + new Vector3(0.5f, top, -0.5f);
        Vector3 v2 = center + new Vector3(0.5f, top, 0.5f);
        Vector3 v3 = center + new Vector3(-0.5f, top, 0.5f);

        // Top face only, double-sided so it's visible from below too
        int si = sb.vertices.Count;
        sb.vertices.Add(v3); sb.vertices.Add(v2); sb.vertices.Add(v1); sb.vertices.Add(v0);
        sb.uvs.Add(new Vector2(0, 1)); sb.uvs.Add(new Vector2(1, 1));
        sb.uvs.Add(new Vector2(1, 0)); sb.uvs.Add(new Vector2(0, 0));

        // Top-facing
        sb.tris.Add(si + 0); sb.tris.Add(si + 2); sb.tris.Add(si + 1);
        sb.tris.Add(si + 0); sb.tris.Add(si + 3); sb.tris.Add(si + 2);
        // Bottom-facing (visible from underwater)
        sb.tris.Add(si + 0); sb.tris.Add(si + 1); sb.tris.Add(si + 2);
        sb.tris.Add(si + 0); sb.tris.Add(si + 2); sb.tris.Add(si + 3);
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