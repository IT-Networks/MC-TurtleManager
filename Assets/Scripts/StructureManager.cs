using System.Collections.Generic;
using UnityEngine;
using System.IO;

/// <summary>
/// Manages structure definitions and loading from files
/// </summary>
public class StructureManager : MonoBehaviour
{
    [Header("Settings")]
    public string structuresFolder = "Structures";
    
    [Header("Visual Preview")]
    public Material previewMaterial;
    public Color previewColor = new Color(0, 1, 0, 0.5f);
    
    private Dictionary<string, StructureData> loadedStructures = new Dictionary<string, StructureData>();
    private GameObject previewObject;
    private List<GameObject> previewBlocks = new List<GameObject>();
    
    // Events
    public System.Action<Dictionary<string, StructureData>> OnStructuresLoaded;
    
    private void Start()
    {
        LoadAllStructures();
    }
    
    private void LoadAllStructures()
    {
        string structuresPath = Path.Combine(Application.streamingAssetsPath, structuresFolder);
        
        if (!Directory.Exists(structuresPath))
        {
            Debug.LogWarning($"Structures folder not found at: {structuresPath}");
            Directory.CreateDirectory(structuresPath);
            CreateSampleStructures(structuresPath);
            return;
        }
        
        string[] jsonFiles = Directory.GetFiles(structuresPath, "*.json");
        
        foreach (string filePath in jsonFiles)
        {
            LoadStructure(filePath);
        }
        
        Debug.Log($"Loaded {loadedStructures.Count} structures");
        OnStructuresLoaded?.Invoke(new Dictionary<string, StructureData>(loadedStructures));
    }
    
    private void LoadStructure(string filePath)
    {
        try
        {
            string json = File.ReadAllText(filePath);
            StructureData structure = JsonUtility.FromJson<StructureData>(json);
            
            if (structure != null && !string.IsNullOrEmpty(structure.name))
            {
                loadedStructures[structure.name] = structure;
                Debug.Log($"Loaded structure: {structure.name} ({structure.blocks.Count} blocks)");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load structure from {filePath}: {ex.Message}");
        }
    }
    
    private void CreateSampleStructures(string structuresPath)
    {
        // Create sample house structure
        StructureData house = new StructureData
        {
            name = "Simple House",
            description = "A basic 5x5x4 house structure",
            blocks = new List<StructureData.StructureBlock>()
        };
        
        // Foundation (5x5)
        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                house.blocks.Add(new StructureData.StructureBlock
                {
                    position = new Vector3Int(x, 0, z),
                    blockType = "minecraft:cobblestone"
                });
            }
        }
        
        // Walls
        for (int y = 1; y <= 3; y++)
        {
            // Front and back walls
            for (int x = 0; x < 5; x++)
            {
                if (y != 2 || x != 2) // Door opening
                {
                    house.blocks.Add(new StructureData.StructureBlock
                    {
                        position = new Vector3Int(x, y, 0),
                        blockType = "minecraft:oak_planks"
                    });
                }
                house.blocks.Add(new StructureData.StructureBlock
                {
                    position = new Vector3Int(x, y, 4),
                    blockType = "minecraft:oak_planks"
                });
            }
            
            // Side walls
            for (int z = 1; z < 4; z++)
            {
                house.blocks.Add(new StructureData.StructureBlock
                {
                    position = new Vector3Int(0, y, z),
                    blockType = "minecraft:oak_planks"
                });
                house.blocks.Add(new StructureData.StructureBlock
                {
                    position = new Vector3Int(4, y, z),
                    blockType = "minecraft:oak_planks"
                });
            }
        }
        
        // Roof
        for (int x = 0; x < 5; x++)
        {
            for (int z = 0; z < 5; z++)
            {
                house.blocks.Add(new StructureData.StructureBlock
                {
                    position = new Vector3Int(x, 4, z),
                    blockType = "minecraft:oak_planks"
                });
            }
        }
        
        SaveStructure(house, Path.Combine(structuresPath, "simple_house.json"));
        
        // Create sample tower structure
        StructureData tower = new StructureData
        {
            name = "Stone Tower",
            description = "A 3x3x8 stone tower",
            blocks = new List<StructureData.StructureBlock>()
        };
        
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    // Hollow out the middle (except bottom)
                    if (y == 0 || x == 0 || x == 2 || z == 0 || z == 2)
                    {
                        tower.blocks.Add(new StructureData.StructureBlock
                        {
                            position = new Vector3Int(x, y, z),
                            blockType = "minecraft:stone_bricks"
                        });
                    }
                }
            }
        }
        
        SaveStructure(tower, Path.Combine(structuresPath, "stone_tower.json"));
        
        loadedStructures[house.name] = house;
        loadedStructures[tower.name] = tower;
    }
    
    private void SaveStructure(StructureData structure, string filePath)
    {
        try
        {
            string json = JsonUtility.ToJson(structure, true);
            File.WriteAllText(filePath, json);
            Debug.Log($"Saved structure to: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save structure to {filePath}: {ex.Message}");
        }
    }
    
    public void ShowStructurePreview(string structureName, Vector3 worldPosition)
    {
        ClearPreview();
        
        if (!loadedStructures.TryGetValue(structureName, out StructureData structure))
        {
            Debug.LogWarning($"Structure not found: {structureName}");
            return;
        }
        
        previewObject = new GameObject($"StructurePreview_{structureName}");
        previewObject.transform.position = worldPosition;
        
        foreach (var block in structure.blocks)
        {
            Vector3 blockWorldPos = worldPosition + (Vector3)block.position;
            GameObject previewBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewBlock.transform.position = blockWorldPos + Vector3.one * 0.5f;
            previewBlock.transform.localScale = Vector3.one * 0.9f;
            previewBlock.transform.SetParent(previewObject.transform);
            previewBlock.name = $"PreviewBlock_{block.blockType}";
            
            // Remove collider
            DestroyImmediate(previewBlock.GetComponent<Collider>());
            
            // Setup preview material
            Renderer renderer = previewBlock.GetComponent<Renderer>();
            if (previewMaterial != null)
            {
                renderer.material = previewMaterial;
            }
            else
            {
                Material mat = new Material(Shader.Find("Standard"));
                mat.SetFloat("_Mode", 3); // Transparent mode
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                mat.color = previewColor;
                renderer.material = mat;
            }
            
            previewBlocks.Add(previewBlock);
        }
        
        Debug.Log($"Showing preview for {structureName} with {structure.blocks.Count} blocks at {worldPosition}");
    }
    
    public void ClearPreview()
    {
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
            previewObject = null;
        }
        
        foreach (GameObject block in previewBlocks)
        {
            if (block != null)
                DestroyImmediate(block);
        }
        previewBlocks.Clear();
    }
    
    // Public API
    public Dictionary<string, StructureData> GetAllStructures()
    {
        return new Dictionary<string, StructureData>(loadedStructures);
    }
    
    public StructureData GetStructure(string name)
    {
        loadedStructures.TryGetValue(name, out StructureData structure);
        return structure;
    }
    
    public List<string> GetStructureNames()
    {
        return new List<string>(loadedStructures.Keys);
    }
    
    private void OnDestroy()
    {
        ClearPreview();
    }
}

/// <summary>
/// Data structure for storing building structures
/// </summary>
[System.Serializable]
public class StructureData
{
    public string name;
    public string description;
    public List<StructureBlock> blocks = new List<StructureBlock>();
    
    [System.Serializable]
    public class StructureBlock
    {
        public Vector3Int position;
        public string blockType;
        
        public StructureBlock() { }
        
        public StructureBlock(Vector3Int pos, string type)
        {
            position = pos;
            blockType = type;
        }
    }
    
    public Vector3Int GetSize()
    {
        if (blocks.Count == 0) return Vector3Int.zero;
        
        Vector3Int min = blocks[0].position;
        Vector3Int max = blocks[0].position;
        
        foreach (var block in blocks)
        {
            min = Vector3Int.Min(min, block.position);
            max = Vector3Int.Max(max, block.position);
        }
        
        return max - min + Vector3Int.one;
    }
    
    public Vector3Int GetMinPosition()
    {
        if (blocks.Count == 0) return Vector3Int.zero;
        
        Vector3Int min = blocks[0].position;
        foreach (var block in blocks)
        {
            min = Vector3Int.Min(min, block.position);
        }
        return min;
    }
}