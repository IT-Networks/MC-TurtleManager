using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds Unity meshes from chunk data with face culling and batched processing.
/// </summary>
public class ChunkMeshBuilder
{
    private readonly ChunkUVProvider uvProvider;

    public ChunkMeshBuilder(ChunkUVProvider uvProvider = null)
    {
        this.uvProvider = uvProvider ?? new ChunkUVProvider(1);
    }

    public PreparedChunkMesh BuildMeshFromData(ChunkMeshData data, ChunkInfo chunkInfo = null)
    {
        var submeshes = new Dictionary<string, SubmeshBuild>();

        for (int x = 0; x < data.chunkSize; x++)
        {
            for (int y = 0; y < data.maxHeight; y++)
            {
                for (int z = 0; z < data.chunkSize; z++)
                {
                    string blockName = data.GetBlock(x, y, z);
                    if (blockName == null) continue;

                    if (!submeshes.TryGetValue(blockName, out var sb))
                        submeshes[blockName] = sb = new SubmeshBuild();

                    Vector3 worldPos = new Vector3(
                        -(data.coord.x * data.chunkSize + x),
                        y,
                        data.coord.y * data.chunkSize + z);

                    // Add block to ChunkInfo if provided
                    if (chunkInfo != null)
                    {
                        float wx = -(data.coord.x * data.chunkSize + x);
                        float wz = data.coord.y * data.chunkSize + z;
                        chunkInfo.AddBlock(new Vector3(wx, y - 128, wz), blockName);
                    }

                    // Check adjacent blocks for face culling
                    bool[] visibleFaces = GetVisibleFaces(data, x, y, z);
                    AddCubeFaces(sb, worldPos, visibleFaces, blockName);
                }
            }
        }

        return submeshes.Count == 0 ? new PreparedChunkMesh() : PreparedChunkMesh.FromBuild(submeshes);
    }

    private bool[] GetVisibleFaces(ChunkMeshData data, int x, int y, int z)
    {
        return new bool[6]
        {
            // Front (Z+)
            z == data.chunkSize - 1 || !data.HasBlock(x, y, z + 1),
            // Back (Z-)
            z == 0 || !data.HasBlock(x, y, z - 1),
            // Top (Y+)
            y == data.maxHeight - 1 || !data.HasBlock(x, y + 1, z),
            // Bottom (Y-)
            y == 0 || !data.HasBlock(x, y - 1, z),
            // Left (X-) - KORRIGIERT: War x == 0, jetzt x == data.chunkSize - 1
            x == data.chunkSize - 1 || !data.HasBlock(x + 1, y, z),
            // Right (X+) - KORRIGIERT: War x == data.chunkSize - 1, jetzt x == 0
            x == 0 || !data.HasBlock(x - 1, y, z)
        };
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