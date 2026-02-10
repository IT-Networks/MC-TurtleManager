using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Represents a single turtle with its status, visual representation, and assigned tasks
/// </summary>
public class TurtleObject : MonoBehaviour
{
    [Header("Turtle Info")]
    public int turtleId;
    public string turtleName = "Turtle";
    public TurtleStatus currentStatus;

    [Header("Visual")]
    public TurtleVisualizer visualizer;
    public GameObject selectionIndicator;
    public LineRenderer pathLineRenderer;
    public Color normalColor = new Color(0.5f, 0.5f, 0.5f);
    public Color selectedColor = new Color(0.2f, 0.8f, 1f);
    public Color busyColor = new Color(1f, 0.6f, 0.2f);
    public Color pathColor = new Color(1f, 1f, 0f, 0.8f); // Yellow path

    [Header("State")]
    public bool isSelected = false;
    public bool isBusy = false;
    public TurtleOperationManager.OperationType currentOperation = TurtleOperationManager.OperationType.None;

    // Computed properties for compatibility with managers
    public int fuelLevel => currentStatus?.fuel ?? 0;
    public int maxFuel => 20000; // Standard turtle fuel limit in ComputerCraft
    public int inventorySlotsUsed => 0; // TODO: Implement inventory tracking
    public List<InventoryItem> inventory => new List<InventoryItem>(); // TODO: Implement inventory

    [Header("Movement")]
    public float movementSpeed = 3f; // Units per second
    public float rotationSpeed = 360f; // Degrees per second

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isMoving = false;

    private void Start()
    {
        // Create visualizer if not present
        if (visualizer == null)
        {
            visualizer = gameObject.AddComponent<TurtleVisualizer>();
            visualizer.labelText = turtleName;
        }

        // Create selection indicator
        CreateSelectionIndicator();

        // Add box collider for click detection
        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 1.2f; // Slightly larger than visual
        collider.isTrigger = true;

        // Initialize target position and rotation
        targetPosition = transform.position;
        targetRotation = transform.rotation;

        UpdateVisuals();
    }

    private void Update()
    {
        // Smooth movement and rotation
        if (isMoving)
        {
            // Smoothly move towards target position
            float step = movementSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

            // Smoothly rotate towards target rotation
            float rotStep = rotationSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotStep);

            // Check if we've reached the target
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f &&
                Quaternion.Angle(transform.rotation, targetRotation) < 0.1f)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                isMoving = false;
            }
        }

        // Update path visualization if selected and has path
        if (isSelected && pathLineRenderer != null && pathLineRenderer.enabled)
        {
            UpdatePathVisualization();
        }
    }

    private void UpdatePathVisualization()
    {
        TurtleMovementManager movementManager = GetComponent<TurtleMovementManager>();
        if (movementManager == null || !movementManager.HasActivePath())
        {
            // No path anymore, hide line
            if (pathLineRenderer != null)
                pathLineRenderer.enabled = false;
            return;
        }

        List<Vector3> path = movementManager.GetCurrentPath();
        if (path == null || path.Count == 0)
        {
            if (pathLineRenderer != null)
                pathLineRenderer.enabled = false;
            return;
        }

        // Update path positions
        pathLineRenderer.positionCount = path.Count + 1;
        pathLineRenderer.SetPosition(0, transform.position + Vector3.up * 0.5f);

        for (int i = 0; i < path.Count; i++)
        {
            pathLineRenderer.SetPosition(i + 1, path[i] + Vector3.up * 0.5f);
        }
    }

    private void CreateSelectionIndicator()
    {
        selectionIndicator = new GameObject("SelectionIndicator");
        selectionIndicator.transform.SetParent(transform);
        selectionIndicator.transform.localPosition = Vector3.zero;

        // Create ring around turtle
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(selectionIndicator.transform);
        ring.transform.localPosition = new Vector3(0, -0.5f, 0);
        ring.transform.localScale = new Vector3(1.5f, 0.05f, 1.5f);

        Renderer renderer = ring.GetComponent<Renderer>();
        Material mat = CreateHDRPMaterial();
        mat.color = selectedColor;

        // Set metallic and smoothness (HDRP compatible)
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 1f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 1f);
        else if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 1f);

        // Enable emission
        if (mat.HasProperty("_EmissiveColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissiveColor", selectedColor * 0.5f);
        }
        else if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", selectedColor * 0.5f);
        }

        renderer.material = mat;

        Destroy(ring.GetComponent<Collider>());

        selectionIndicator.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(selected);
        }

        // Toggle path visualization when selected
        if (selected)
        {
            ShowPathVisualization();
        }
        else
        {
            HidePathVisualization();
        }

        UpdateVisuals();
    }

    /// <summary>
    /// Show path visualization regardless of selection (for auto-show during movement)
    /// </summary>
    public void ShowPathAutomatically()
    {
        EnsurePathLineRenderer();
        if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled = true;
        }
    }

    public void SetBusy(bool busy, TurtleOperationManager.OperationType operation = TurtleOperationManager.OperationType.None)
    {
        isBusy = busy;
        currentOperation = operation;
        UpdateVisuals();
    }

    public void UpdateStatus(TurtleStatus status)
    {
        currentStatus = status;

        // Update position with world Y offset
        // MultiTurtleManager.WorldYOffset converts Minecraft Y to Unity Y (Minecraft Y=-64 becomes Unity Y=0)
        Vector3 newPos = new Vector3(-status.position.x, status.position.y + MultiTurtleManager.WorldYOffset, status.position.z);

        // Check if position has changed
        if (Vector3.Distance(transform.position, newPos) > 0.01f)
        {
            targetPosition = newPos;
            isMoving = true;
        }

        // Update rotation based on direction
        Quaternion newRotation = Quaternion.LookRotation(DirectionToVector(status.direction));
        if (Quaternion.Angle(transform.rotation, newRotation) > 0.1f)
        {
            targetRotation = newRotation;
            isMoving = true;
        }

        // Update label
        if (visualizer != null)
        {
            visualizer.SetLabel($"{turtleName}\nFuel: {status.fuel}");
        }
    }

    private void UpdateVisuals()
    {
        if (visualizer == null) return;

        Color targetColor;
        if (isSelected)
        {
            targetColor = selectedColor;
        }
        else if (isBusy)
        {
            targetColor = busyColor;
        }
        else
        {
            targetColor = normalColor;
        }

        visualizer.SetBodyColor(targetColor);
    }

    private Vector3 DirectionToVector(string direction)
    {
        return direction?.ToLower() switch
        {
            "north" => Vector3.back,
            "south" => Vector3.forward,
            "west" => Vector3.right,
            "east" => Vector3.left,
            _ => Vector3.forward
        };
    }

    /// <summary>
    /// Ensures LineRenderer is created (called automatically when path starts)
    /// </summary>
    public void EnsurePathLineRenderer()
    {
        if (pathLineRenderer == null)
        {
            GameObject lineObj = new GameObject("PathVisualization");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;

            pathLineRenderer = lineObj.AddComponent<LineRenderer>();
            pathLineRenderer.startWidth = 0.15f;
            pathLineRenderer.endWidth = 0.15f;
            pathLineRenderer.material = CreateHDRPLineMaterial();
            pathLineRenderer.startColor = pathColor;
            pathLineRenderer.endColor = pathColor;
            pathLineRenderer.numCapVertices = 5;
            pathLineRenderer.numCornerVertices = 5;

            Debug.Log($"Created PathLineRenderer for {turtleName}");
        }
    }

    private void ShowPathVisualization()
    {
        // Get current path from movement manager
        TurtleMovementManager movementManager = GetComponent<TurtleMovementManager>();
        if (movementManager == null || !movementManager.HasActivePath())
        {
            Debug.Log($"Turtle {turtleName} has no active path to visualize");
            return;
        }

        List<Vector3> path = movementManager.GetCurrentPath();
        if (path == null || path.Count == 0)
        {
            return;
        }

        // Create or get line renderer
        EnsurePathLineRenderer();

        // Set path positions
        pathLineRenderer.positionCount = path.Count + 1;
        pathLineRenderer.SetPosition(0, transform.position + Vector3.up * 0.5f); // Start from turtle

        for (int i = 0; i < path.Count; i++)
        {
            pathLineRenderer.SetPosition(i + 1, path[i] + Vector3.up * 0.5f); // Slightly above ground
        }

        pathLineRenderer.enabled = true;
        Debug.Log($"Visualizing path with {path.Count} waypoints for {turtleName}");
    }

    private void HidePathVisualization()
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled = false;
        }
    }

    private Material CreateHDRPMaterial()
    {
        // Try HDRP/Lit first
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null)
        {
            // Fallback to Standard (for non-HDRP projects)
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            // Last resort fallback
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }

    private Material CreateHDRPLineMaterial()
    {
        // Try to use HDRP/Unlit shader for line rendering
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            // Last fallback
            shader = Shader.Find("Sprites/Default");
        }

        Material mat = new Material(shader);
        mat.color = pathColor;

        // Enable transparency for line
        if (mat.HasProperty("_SurfaceType"))
        {
            // HDRP transparency settings
            mat.SetFloat("_SurfaceType", 1); // Transparent
            mat.SetFloat("_BlendMode", 0); // Alpha blend
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_AlphaCutoffEnable", 0);

            // Enable keywords for transparency
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");

            // CRITICAL: Render queue must be 3000+ for HDRP transparent objects
            mat.renderQueue = 3000;
        }

        return mat;
    }

    private void OnMouseDown()
    {
        // Notify turtle selection manager
        TurtleSelectionManager manager = FindFirstObjectByType<TurtleSelectionManager>();
        if (manager != null)
        {
            manager.SelectTurtle(this);
        }
    }

    private void OnDestroy()
    {
        if (selectionIndicator != null)
        {
            Destroy(selectionIndicator);
        }
    }
}

[System.Serializable]
public class TurtleStatus
{
    public int id;
    public string label;
    public Vector3Int position;
    public string direction;
    public int fuel;
    public string status;
}

[System.Serializable]
public class InventoryItem
{
    public string name;
    public int count;
    public int slot;
}
