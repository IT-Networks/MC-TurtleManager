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
    public Color normalColor = new Color(0.5f, 0.5f, 0.5f);
    public Color selectedColor = new Color(0.2f, 0.8f, 1f);
    public Color busyColor = new Color(1f, 0.6f, 0.2f);

    [Header("State")]
    public bool isSelected = false;
    public bool isBusy = false;
    public TurtleOperationManager.OperationType currentOperation = TurtleOperationManager.OperationType.None;

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

        UpdateVisuals();
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
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = selectedColor;
        mat.SetFloat("_Metallic", 1f);
        mat.SetFloat("_Glossiness", 1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", selectedColor * 0.5f);
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
        UpdateVisuals();
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
        transform.position = newPos;

        // Update rotation based on direction
        transform.rotation = Quaternion.LookRotation(DirectionToVector(status.direction));

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
