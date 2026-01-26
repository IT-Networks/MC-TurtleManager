using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Manages all available structures in the game
/// Handles loading, saving, and accessing structure library
/// </summary>
public class StructureManager : MonoBehaviour
{
    [Header("Settings")]
    public string structuresFolderName = "Structures";
    public bool autoLoadOnStart = true;

    [Header("Runtime")]
    public List<StructureData> loadedStructures = new List<StructureData>();

    private string structuresPath;
    private Dictionary<string, StructureData> structureCache = new Dictionary<string, StructureData>();

    private static StructureManager _instance;
    public static StructureManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<StructureManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("StructureManager");
                    _instance = go.AddComponent<StructureManager>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePaths();
    }

    private void Start()
    {
        if (autoLoadOnStart)
        {
            LoadAllStructures();
        }
    }

    private void InitializePaths()
    {
        structuresPath = Path.Combine(Application.persistentDataPath, structuresFolderName);

        // Create directory if it doesn't exist
        if (!Directory.Exists(structuresPath))
        {
            Directory.CreateDirectory(structuresPath);
            Debug.Log($"Created structures directory: {structuresPath}");
        }

        Debug.Log($"Structures path: {structuresPath}");
    }

    /// <summary>
    /// Loads all structures from the structures folder
    /// </summary>
    public void LoadAllStructures()
    {
        loadedStructures.Clear();
        structureCache.Clear();

        if (!Directory.Exists(structuresPath))
        {
            Debug.LogWarning($"Structures directory not found: {structuresPath}");
            return;
        }

        string[] jsonFiles = Directory.GetFiles(structuresPath, "*.json");

        foreach (string filePath in jsonFiles)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                StructureData structure = JsonUtility.FromJson<StructureData>(json);

                if (structure != null && structure.isValid)
                {
                    loadedStructures.Add(structure);
                    structureCache[structure.name] = structure;
                }
                else
                {
                    Debug.LogWarning($"Invalid structure data in file: {filePath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load structure from {filePath}: {e.Message}");
            }
        }

        Debug.Log($"Loaded {loadedStructures.Count} structures from {structuresPath}");
    }

    /// <summary>
    /// Saves a structure to disk
    /// </summary>
    public bool SaveStructure(StructureData structure)
    {
        if (structure == null || !structure.isValid)
        {
            Debug.LogError("Cannot save invalid structure");
            return false;
        }

        try
        {
            structure.Normalize(); // Ensure positions start at 0,0,0

            string fileName = SanitizeFileName(structure.name) + ".json";
            string filePath = Path.Combine(structuresPath, fileName);

            string json = JsonUtility.ToJson(structure, true);
            File.WriteAllText(filePath, json);

            // Update cache
            if (!structureCache.ContainsKey(structure.name))
            {
                loadedStructures.Add(structure);
            }
            structureCache[structure.name] = structure;

            Debug.Log($"Saved structure '{structure.name}' to {filePath}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save structure '{structure.name}': {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a structure from disk
    /// </summary>
    public bool DeleteStructure(string structureName)
    {
        try
        {
            string fileName = SanitizeFileName(structureName) + ".json";
            string filePath = Path.Combine(structuresPath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);

                // Remove from cache
                loadedStructures.RemoveAll(s => s.name == structureName);
                structureCache.Remove(structureName);

                Debug.Log($"Deleted structure '{structureName}'");
                return true;
            }
            else
            {
                Debug.LogWarning($"Structure file not found: {filePath}");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to delete structure '{structureName}': {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets a structure by name
    /// </summary>
    public StructureData GetStructure(string structureName)
    {
        if (structureCache.TryGetValue(structureName, out StructureData structure))
        {
            return structure;
        }

        return null;
    }

    /// <summary>
    /// Gets all structures in a specific category
    /// </summary>
    public List<StructureData> GetStructuresByCategory(string category)
    {
        return loadedStructures.Where(s => s.category == category).ToList();
    }

    /// <summary>
    /// Gets all unique categories
    /// </summary>
    public List<string> GetAllCategories()
    {
        return loadedStructures.Select(s => s.category).Distinct().ToList();
    }

    /// <summary>
    /// Checks if a structure with the given name exists
    /// </summary>
    public bool StructureExists(string structureName)
    {
        return structureCache.ContainsKey(structureName);
    }

    /// <summary>
    /// Creates example structures for testing
    /// </summary>
    public void CreateExampleStructures()
    {
        // Example 1: Simple platform
        StructureData platform = new StructureData("Platform 3x3");
        platform.description = "A simple 3x3 platform";
        platform.category = "Basics";
        platform.author = "System";

        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                platform.AddBlock(new Vector3Int(x, 0, z), "minecraft:stone");
            }
        }

        SaveStructure(platform);

        // Example 2: Simple wall
        StructureData wall = new StructureData("Wall 5x3");
        wall.description = "A 5 block wide, 3 block tall wall";
        wall.category = "Basics";
        wall.author = "System";

        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                wall.AddBlock(new Vector3Int(x, y, 0), "minecraft:cobblestone");
            }
        }

        SaveStructure(wall);

        // Example 3: Simple tower
        StructureData tower = new StructureData("Tower 2x2x5");
        tower.description = "A simple tower";
        tower.category = "Buildings";
        tower.author = "System";

        for (int x = 0; x < 2; x++)
        {
            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < 5; y++)
                {
                    // Hollow inside except corners
                    if (y == 0 || y == 4 || x == 0 || x == 1 || z == 0 || z == 1)
                    {
                        tower.AddBlock(new Vector3Int(x, y, z), "minecraft:stone_bricks");
                    }
                }
            }
        }

        SaveStructure(tower);

        Debug.Log("Created 3 example structures");
        LoadAllStructures();
    }

    private string SanitizeFileName(string fileName)
    {
        string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        foreach (char c in invalid)
        {
            fileName = fileName.Replace(c.ToString(), "_");
        }
        return fileName;
    }

    public string GetStructuresPath()
    {
        return structuresPath;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
