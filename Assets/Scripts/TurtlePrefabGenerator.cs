using UnityEngine;

/// <summary>
/// Generates a default turtle prefab at runtime if none is assigned
/// Automatically integrates with TurtleWorldManager
/// </summary>
[ExecuteInEditMode]
public class TurtlePrefabGenerator : MonoBehaviour
{
    [Header("Auto-Generate Prefab")]
    [Tooltip("Automatically create turtle prefab if none is assigned")]
    public bool autoGeneratePrefab = true;

    [Header("Generated Prefab Settings")]
    public Color turtleBodyColor = new Color(0.4f, 0.4f, 0.4f);
    public Color turtleFrontColor = new Color(0.2f, 0.8f, 0.2f);
    public float turtleSize = 0.9f;

    void Start()
    {
        if (!autoGeneratePrefab) return;

        TurtleWorldManager worldManager = GetComponent<TurtleWorldManager>();
        if (worldManager != null && worldManager.turtlePrefab == null)
        {
            Debug.Log("No turtle prefab assigned. Creating default turtle prefab...");
            worldManager.turtlePrefab = CreateDefaultTurtlePrefab();
            Debug.Log("Default turtle prefab created successfully!");
        }
    }

    /// <summary>
    /// Creates a default turtle prefab with TurtleVisualizer
    /// </summary>
    public GameObject CreateDefaultTurtlePrefab()
    {
        // Create the root object
        GameObject turtlePrefab = new GameObject("TurtlePrefab");

        // Add TurtleVisualizer component
        TurtleVisualizer visualizer = turtlePrefab.AddComponent<TurtleVisualizer>();
        visualizer.bodyColor = turtleBodyColor;
        visualizer.frontColor = turtleFrontColor;
        visualizer.turtleSize = turtleSize;
        visualizer.showLabel = true;
        visualizer.labelText = "Turtle";
        visualizer.enableIdleAnimation = true;

        // Movement is now handled by TurtleMainController system
        // No need to add RTSController (old system removed)

        // Don't save this as an actual prefab, just use it at runtime
        // Mark it as DontDestroyOnLoad so it persists
        // DontDestroyOnLoad(turtlePrefab);

        return turtlePrefab;
    }

    /// <summary>
    /// Manual method to generate prefab (can be called from editor)
    /// </summary>
    [ContextMenu("Generate Turtle Prefab")]
    public void GeneratePrefabManually()
    {
        TurtleWorldManager worldManager = GetComponent<TurtleWorldManager>();
        if (worldManager != null)
        {
            worldManager.turtlePrefab = CreateDefaultTurtlePrefab();
            Debug.Log("Turtle prefab generated manually!");
        }
        else
        {
            Debug.LogError("TurtleWorldManager component not found on this GameObject!");
        }
    }

    /// <summary>
    /// Creates a simple turtle prefab variant with custom colors
    /// </summary>
    public GameObject CreateCustomTurtlePrefab(Color bodyColor, Color frontColor, string label)
    {
        GameObject turtlePrefab = CreateDefaultTurtlePrefab();

        TurtleVisualizer visualizer = turtlePrefab.GetComponent<TurtleVisualizer>();
        if (visualizer != null)
        {
            visualizer.bodyColor = bodyColor;
            visualizer.frontColor = frontColor;
            visualizer.labelText = label;
        }

        return turtlePrefab;
    }
}
