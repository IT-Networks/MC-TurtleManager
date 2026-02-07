using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates Unity meshes for all Minecraft block model types
/// Efficient, generic implementation that handles vanilla and modded blocks
///
/// Based on Minecraft 1.21 JSON model specifications and ATM10 mods
/// References:
/// - https://minecraft.wiki/w/Tutorial:Models
/// - https://docs.fabricmc.net/develop/data-generation/block-models
/// - All the Mods 10 block model specifications
/// </summary>
public static class BlockMeshGenerator
{
    private const float BLOCK_SIZE = 1f;

    /// <summary>
    /// Generates a mesh for a given block type at the specified position
    /// Returns a configured GameObject with mesh, material, and collider
    /// </summary>
    public static GameObject GenerateBlockMesh(Vector3 position, string blockType, BlockModelType modelType, Material material = null)
    {
        GameObject blockObj = new GameObject($"Block_{blockType}");
        blockObj.transform.position = position;

        switch (modelType)
        {
            case BlockModelType.Cube:
                CreateCubeMesh(blockObj, material);
                break;

            case BlockModelType.CrossPlant:
            case BlockModelType.TintedCross:
                CreateCrossPlantMesh(blockObj, material);
                break;

            case BlockModelType.Slab:
                CreateSlabMesh(blockObj, material);
                break;

            case BlockModelType.Stairs:
                CreateStairsMesh(blockObj, material);
                break;

            case BlockModelType.Carpet:
                CreateCarpetMesh(blockObj, material);
                break;

            case BlockModelType.SnowLayer:
                CreateSnowLayerMesh(blockObj, material, 1); // Default 1 layer
                break;

            case BlockModelType.Fence:
                CreateFenceMesh(blockObj, material);
                break;

            case BlockModelType.Wall:
                CreateWallMesh(blockObj, material);
                break;

            case BlockModelType.Pane:
                CreatePaneMesh(blockObj, material);
                break;

            case BlockModelType.Chain:
                CreateChainMesh(blockObj, material);
                break;

            case BlockModelType.Door:
                CreateDoorMesh(blockObj, material);
                break;

            case BlockModelType.Trapdoor:
                CreateTrapdoorMesh(blockObj, material);
                break;

            case BlockModelType.FenceGate:
                CreateFenceGateMesh(blockObj, material);
                break;

            case BlockModelType.Button:
                CreateButtonMesh(blockObj, material);
                break;

            case BlockModelType.PressurePlate:
                CreatePressurePlateMesh(blockObj, material);
                break;

            case BlockModelType.Torch:
                CreateTorchMesh(blockObj, material);
                break;

            case BlockModelType.Lever:
                CreateLeverMesh(blockObj, material);
                break;

            case BlockModelType.Chest:
                CreateChestMesh(blockObj, material);
                break;

            case BlockModelType.Bed:
                CreateBedMesh(blockObj, material);
                break;

            case BlockModelType.Furniture:
                CreateFurnitureMesh(blockObj, material);
                break;

            case BlockModelType.Pipe:
                CreatePipeMesh(blockObj, material);
                break;

            case BlockModelType.Cable:
                CreateCableMesh(blockObj, material);
                break;

            case BlockModelType.Conduit:
                CreateConduitMesh(blockObj, material);
                break;

            case BlockModelType.Gear:
                CreateGearMesh(blockObj, material);
                break;

            case BlockModelType.Belt:
                CreateBeltMesh(blockObj, material);
                break;

            case BlockModelType.Mechanical:
                CreateMechanicalMesh(blockObj, material);
                break;

            case BlockModelType.Liquid:
                CreateLiquidMesh(blockObj, material);
                break;

            case BlockModelType.Air:
                // No mesh for air
                break;

            default:
                CreateCubeMesh(blockObj, material);
                break;
        }

        return blockObj;
    }

    // ========== FULL BLOCKS ==========

    private static void CreateCubeMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        // Use Unity's built-in cube mesh for efficiency
        mf.mesh = CreateUnitCube();
        obj.transform.localScale = Vector3.one * BLOCK_SIZE * 0.98f; // Slightly smaller to prevent z-fighting

        if (material != null)
            mr.material = material;

        // Add collider for raycasting
        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = Vector3.one * BLOCK_SIZE;
    }

    // ========== PLANTS ==========

    private static void CreateCrossPlantMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "CrossPlantMesh";

        float sqrt2 = Mathf.Sqrt(2f);
        float diagonalHalf = 0.5f * sqrt2;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // First diagonal (Z: -0.7 to +0.7, X: -0.7 to +0.7)
        AddQuad(vertices, uvs, triangles,
            new Vector3(-diagonalHalf, 0, -diagonalHalf),
            new Vector3(diagonalHalf, 0, diagonalHalf),
            new Vector3(diagonalHalf, 1, diagonalHalf),
            new Vector3(-diagonalHalf, 1, -diagonalHalf),
            true); // double-sided

        // Second diagonal (perpendicular)
        AddQuad(vertices, uvs, triangles,
            new Vector3(diagonalHalf, 0, -diagonalHalf),
            new Vector3(-diagonalHalf, 0, diagonalHalf),
            new Vector3(-diagonalHalf, 1, diagonalHalf),
            new Vector3(diagonalHalf, 1, -diagonalHalf),
            true); // double-sided

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        // Small box collider for raycasting
        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.5f, 1f, 0.5f);
        collider.center = new Vector3(0, 0.5f, 0);
    }

    // ========== PARTIAL BLOCKS ==========

    private static void CreateSlabMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 0.5f, 1f));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.5f, 1f);
        collider.center = new Vector3(0, 0.25f, 0);
    }

    private static void CreateStairsMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        // Simplified stairs: lower slab + upper slab offset
        Mesh mesh = new Mesh();
        mesh.name = "StairsMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Lower step (full width, half height)
        AddBox(vertices, uvs, triangles,
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f));

        // Upper step (half width, full height)
        AddBox(vertices, uvs, triangles,
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 1f, 0.5f));

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        // Composite collider
        MeshCollider collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private static void CreateCarpetMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 0.0625f, 1f)); // 1/16 height
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.0625f, 1f);
        collider.center = new Vector3(0, 0.03125f, 0);
    }

    private static void CreateSnowLayerMesh(GameObject obj, Material material, int layers = 1)
    {
        float height = layers / 8f; // 1-8 layers = 1/8 to 1 height

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, height, 1f));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, height, 1f);
        collider.center = new Vector3(0, height * 0.5f, 0);
    }

    // ========== CONNECTABLE BLOCKS (Multipart) ==========

    private static void CreateFenceMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "FenceMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Center post (always present)
        float postThickness = 0.125f; // 2/16 pixels
        AddBox(vertices, uvs, triangles,
            new Vector3(-postThickness, 0f, -postThickness),
            new Vector3(postThickness, 1.5f, postThickness));

        // For simplicity, add all 4 side rails (should be conditional in full implementation)
        float railHeight = 0.75f;
        float railThickness = 0.0625f;
        float railWidth = 0.5f;

        // North rail
        AddBox(vertices, uvs, triangles,
            new Vector3(-railThickness, railHeight, 0.5f - railThickness),
            new Vector3(railThickness, railHeight + 0.125f, 0.5f));

        // South rail
        AddBox(vertices, uvs, triangles,
            new Vector3(-railThickness, railHeight, -0.5f),
            new Vector3(railThickness, railHeight + 0.125f, -0.5f + railThickness));

        // East rail
        AddBox(vertices, uvs, triangles,
            new Vector3(0.5f - railThickness, railHeight, -railThickness),
            new Vector3(0.5f, railHeight + 0.125f, railThickness));

        // West rail
        AddBox(vertices, uvs, triangles,
            new Vector3(-0.5f, railHeight, -railThickness),
            new Vector3(-0.5f + railThickness, railHeight + 0.125f, railThickness));

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        MeshCollider collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private static void CreateWallMesh(GameObject obj, Material material)
    {
        // Similar to fence but thicker and shorter
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "WallMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Center post
        float postThickness = 0.25f;
        AddBox(vertices, uvs, triangles,
            new Vector3(-postThickness, 0f, -postThickness),
            new Vector3(postThickness, 1f, postThickness));

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        MeshCollider collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private static void CreatePaneMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "PaneMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Thin vertical pane (2/16 pixels thick)
        float thickness = 0.0625f;

        // Center pane (vertical)
        AddQuad(vertices, uvs, triangles,
            new Vector3(-0.5f, 0f, -thickness),
            new Vector3(0.5f, 0f, -thickness),
            new Vector3(0.5f, 1f, -thickness),
            new Vector3(-0.5f, 1f, -thickness),
            true); // double-sided

        AddQuad(vertices, uvs, triangles,
            new Vector3(-0.5f, 0f, thickness),
            new Vector3(0.5f, 0f, thickness),
            new Vector3(0.5f, 1f, thickness),
            new Vector3(-0.5f, 1f, thickness),
            true); // double-sided

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 1f, thickness * 2);
        collider.center = Vector3.zero;
    }

    private static void CreateChainMesh(GameObject obj, Material material)
    {
        // Thin vertical box
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        float thickness = 0.1f;
        Mesh mesh = CreateBox(new Vector3(thickness, 1f, thickness));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(thickness, 1f, thickness);
        collider.center = new Vector3(0, 0.5f, 0);
    }

    // ========== DOORS & GATES ==========

    private static void CreateDoorMesh(GameObject obj, Material material)
    {
        // Thin vertical plane (door is 2 blocks tall in full implementation)
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 2f, 0.1875f)); // 3/16 thick
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 2f, 0.1875f);
        collider.center = new Vector3(0, 1f, 0);
    }

    private static void CreateTrapdoorMesh(GameObject obj, Material material)
    {
        // Thin horizontal plane
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 0.1875f, 1f)); // 3/16 thick
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.1875f, 1f);
        collider.center = new Vector3(0, 0.09375f, 0);
    }

    private static void CreateFenceGateMesh(GameObject obj, Material material)
    {
        // Simplified fence gate (similar to fence but with gate)
        CreateFenceMesh(obj, material); // Reuse fence mesh for now
    }

    // ========== SMALL BLOCKS ==========

    private static void CreateButtonMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(0.375f, 0.25f, 0.125f)); // Small button
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.375f, 0.25f, 0.125f);
        collider.center = new Vector3(0, 0.5f, -0.4375f); // On wall
    }

    private static void CreatePressurePlateMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 0.0625f, 1f)); // Very thin
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.0625f, 1f);
        collider.center = new Vector3(0, 0.03125f, 0);
    }

    private static void CreateTorchMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh();
        mesh.name = "TorchMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Stick
        float stickThickness = 0.0625f;
        AddBox(vertices, uvs, triangles,
            new Vector3(-stickThickness, 0f, -stickThickness),
            new Vector3(stickThickness, 0.625f, stickThickness));

        // Flame (small cube on top)
        AddBox(vertices, uvs, triangles,
            new Vector3(-stickThickness * 1.5f, 0.625f, -stickThickness * 1.5f),
            new Vector3(stickThickness * 1.5f, 0.75f, stickThickness * 1.5f));

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(stickThickness * 2, 0.75f, stickThickness * 2);
        collider.center = new Vector3(0, 0.375f, 0);
    }

    private static void CreateLeverMesh(GameObject obj, Material material)
    {
        // Similar to button but with stick
        CreateButtonMesh(obj, material);
    }

    // ========== FURNITURE & STORAGE ==========

    private static void CreateChestMesh(GameObject obj, Material material)
    {
        // Slightly smaller cube
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(0.875f, 0.875f, 0.875f));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.875f, 0.875f, 0.875f);
        collider.center = new Vector3(0, 0.4375f, 0);
    }

    private static void CreateBedMesh(GameObject obj, Material material)
    {
        // 2 blocks long, low height
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 0.5625f, 2f));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.5625f, 2f);
        collider.center = new Vector3(0, 0.28125f, 0);
    }

    private static void CreateFurnitureMesh(GameObject obj, Material material)
    {
        // Generic furniture: slightly varied cube
        CreateCubeMesh(obj, material);
    }

    // ========== TECHNICAL BLOCKS (Mods) ==========

    private static void CreatePipeMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        float thickness = 0.25f;
        Mesh mesh = CreateBox(new Vector3(thickness, 1f, thickness));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(thickness, 1f, thickness);
        collider.center = new Vector3(0, 0.5f, 0);
    }

    private static void CreateCableMesh(GameObject obj, Material material)
    {
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        float thickness = 0.125f;
        Mesh mesh = CreateBox(new Vector3(thickness, 1f, thickness));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(thickness, 1f, thickness);
        collider.center = new Vector3(0, 0.5f, 0);
    }

    private static void CreateConduitMesh(GameObject obj, Material material)
    {
        // Similar to pipe but with different appearance
        CreatePipeMesh(obj, material);
    }

    // ========== CREATE MOD ==========

    private static void CreateGearMesh(GameObject obj, Material material)
    {
        // Simplified: Use cylinder for now (proper gear teeth would be complex)
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        GameObject tempCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mf.mesh = tempCylinder.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(tempCylinder);

        obj.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);

        if (material != null)
            mr.material = material;

        CapsuleCollider collider = obj.AddComponent<CapsuleCollider>();
        collider.radius = 0.5f;
        collider.height = 0.2f;
        collider.direction = 1; // Y-axis
    }

    private static void CreateBeltMesh(GameObject obj, Material material)
    {
        // Flat horizontal surface
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 0.125f, 1f));
        mf.mesh = mesh;

        if (material != null)
            mr.material = material;

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.125f, 1f);
        collider.center = new Vector3(0, 0.0625f, 0);
    }

    private static void CreateMechanicalMesh(GameObject obj, Material material)
    {
        // Generic mechanical: use cube
        CreateCubeMesh(obj, material);
    }

    // ========== SPECIAL ==========

    private static void CreateLiquidMesh(GameObject obj, Material material)
    {
        // Slightly smaller cube, semi-transparent
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();

        Mesh mesh = CreateBox(new Vector3(1f, 0.9f, 1f));
        mf.mesh = mesh;

        if (material != null)
        {
            mr.material = material;
            // Ensure transparency
            if (mr.material.HasProperty("_SurfaceType"))
                mr.material.SetFloat("_SurfaceType", 1);
        }

        BoxCollider collider = obj.AddComponent<BoxCollider>();
        collider.size = new Vector3(1f, 0.9f, 1f);
        collider.center = new Vector3(0, 0.45f, 0);
    }

    // ========== HELPER FUNCTIONS ==========

    private static Mesh CreateUnitCube()
    {
        Mesh mesh = new Mesh();
        mesh.name = "UnitCube";

        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f)
        };

        mesh.triangles = new int[]
        {
            0, 2, 1, 0, 3, 2, // Back
            4, 5, 6, 4, 6, 7, // Front
            0, 1, 5, 0, 5, 4, // Bottom
            3, 7, 6, 3, 6, 2, // Top
            0, 4, 7, 0, 7, 3, // Left
            1, 2, 6, 1, 6, 5  // Right
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static Mesh CreateBox(Vector3 size)
    {
        Mesh mesh = new Mesh();
        mesh.name = "Box";

        Vector3 halfSize = size * 0.5f;

        mesh.vertices = new Vector3[]
        {
            new Vector3(-halfSize.x, 0, -halfSize.z),
            new Vector3( halfSize.x, 0, -halfSize.z),
            new Vector3( halfSize.x, size.y, -halfSize.z),
            new Vector3(-halfSize.x, size.y, -halfSize.z),
            new Vector3(-halfSize.x, 0,  halfSize.z),
            new Vector3( halfSize.x, 0,  halfSize.z),
            new Vector3( halfSize.x, size.y,  halfSize.z),
            new Vector3(-halfSize.x, size.y,  halfSize.z)
        };

        mesh.triangles = new int[]
        {
            0, 2, 1, 0, 3, 2, // Back
            4, 5, 6, 4, 6, 7, // Front
            0, 1, 5, 0, 5, 4, // Bottom
            3, 7, 6, 3, 6, 2, // Top
            0, 4, 7, 0, 7, 3, // Left
            1, 2, 6, 1, 6, 5  // Right
        };

        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void AddBox(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 min, Vector3 max)
    {
        int startIdx = vertices.Count;

        vertices.Add(new Vector3(min.x, min.y, min.z));
        vertices.Add(new Vector3(max.x, min.y, min.z));
        vertices.Add(new Vector3(max.x, max.y, min.z));
        vertices.Add(new Vector3(min.x, max.y, min.z));
        vertices.Add(new Vector3(min.x, min.y, max.z));
        vertices.Add(new Vector3(max.x, min.y, max.z));
        vertices.Add(new Vector3(max.x, max.y, max.z));
        vertices.Add(new Vector3(min.x, max.y, max.z));

        for (int i = 0; i < 8; i++)
            uvs.Add(new Vector2(0.5f, 0.5f));

        // 6 faces
        int[] faceIndices = {
            0, 2, 1, 0, 3, 2, // Back
            4, 5, 6, 4, 6, 7, // Front
            0, 1, 5, 0, 5, 4, // Bottom
            3, 7, 6, 3, 6, 2, // Top
            0, 4, 7, 0, 7, 3, // Left
            1, 2, 6, 1, 6, 5  // Right
        };

        foreach (int idx in faceIndices)
            triangles.Add(startIdx + idx);
    }

    private static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, bool doubleSided = false)
    {
        int startIdx = vertices.Count;

        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));

        // Front face
        triangles.Add(startIdx + 0);
        triangles.Add(startIdx + 2);
        triangles.Add(startIdx + 1);
        triangles.Add(startIdx + 0);
        triangles.Add(startIdx + 3);
        triangles.Add(startIdx + 2);

        if (doubleSided)
        {
            // Back face (reversed winding)
            triangles.Add(startIdx + 0);
            triangles.Add(startIdx + 1);
            triangles.Add(startIdx + 2);
            triangles.Add(startIdx + 0);
            triangles.Add(startIdx + 2);
            triangles.Add(startIdx + 3);
        }
    }
}
