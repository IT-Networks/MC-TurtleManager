using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Manages turtle selection and task assignment for multi-turtle operations
/// </summary>
public class TurtleSelectionManager : MonoBehaviour
{
    [Header("Selection Settings")]
    public bool allowMultiSelect = true;
    public KeyCode multiSelectKey = KeyCode.LeftControl;

    [Header("References")]
    public MultiTurtleManager turtleManager;

    private List<TurtleObject> selectedTurtles = new List<TurtleObject>();

    public System.Action<List<TurtleObject>> OnSelectionChanged;

    private void Start()
    {
        if (turtleManager == null)
        {
            turtleManager = FindFirstObjectByType<MultiTurtleManager>();
        }
    }

    public void SelectTurtle(TurtleObject turtle)
    {
        if (turtle == null) return;

        bool multiSelect = allowMultiSelect && Input.GetKey(multiSelectKey);

        if (multiSelect)
        {
            // Toggle selection
            if (selectedTurtles.Contains(turtle))
            {
                DeselectTurtle(turtle);
            }
            else
            {
                AddToSelection(turtle);
            }
        }
        else
        {
            // Single selection - clear others
            ClearSelection();
            AddToSelection(turtle);
        }

        OnSelectionChanged?.Invoke(selectedTurtles);
    }

    public void SelectTurtleById(int turtleId)
    {
        if (turtleManager == null) return;

        var turtle = turtleManager.GetTurtleById(turtleId);
        if (turtle != null)
        {
            SelectTurtle(turtle);
        }
    }

    public void SelectAllTurtles()
    {
        if (turtleManager == null) return;

        ClearSelection();
        foreach (var turtle in turtleManager.GetAllTurtles())
        {
            AddToSelection(turtle);
        }

        OnSelectionChanged?.Invoke(selectedTurtles);
    }

    public void SelectAvailableTurtles()
    {
        if (turtleManager == null) return;

        ClearSelection();
        foreach (var turtle in turtleManager.GetAllTurtles())
        {
            if (!turtle.isBusy)
            {
                AddToSelection(turtle);
            }
        }

        OnSelectionChanged?.Invoke(selectedTurtles);
    }

    private void AddToSelection(TurtleObject turtle)
    {
        if (!selectedTurtles.Contains(turtle))
        {
            selectedTurtles.Add(turtle);
            turtle.SetSelected(true);
        }
    }

    private void DeselectTurtle(TurtleObject turtle)
    {
        if (selectedTurtles.Contains(turtle))
        {
            selectedTurtles.Remove(turtle);
            turtle.SetSelected(false);
        }
    }

    public void ClearSelection()
    {
        foreach (var turtle in selectedTurtles)
        {
            turtle.SetSelected(false);
        }
        selectedTurtles.Clear();
        OnSelectionChanged?.Invoke(selectedTurtles);
    }

    public List<TurtleObject> GetSelectedTurtles()
    {
        return new List<TurtleObject>(selectedTurtles);
    }

    public TurtleObject GetPrimarySelection()
    {
        return selectedTurtles.Count > 0 ? selectedTurtles[0] : null;
    }

    public int GetSelectionCount()
    {
        return selectedTurtles.Count;
    }

    public bool HasSelection()
    {
        return selectedTurtles.Count > 0;
    }

    // Task assignment methods
    public void AssignMiningTask(List<Vector3> blocks)
    {
        AssignMiningTaskInternal(blocks, skipValidation: false);
    }

    /// <summary>
    /// Assign pre-validated mining task (skips chunk validation)
    /// Use this when blocks have already been validated to avoid chunk-loading issues
    /// </summary>
    public void AssignPreValidatedMiningTask(List<Vector3> validatedBlocks)
    {
        AssignMiningTaskInternal(validatedBlocks, skipValidation: true);
    }

    private void AssignMiningTaskInternal(List<Vector3> blocks, bool skipValidation)
    {
        if (selectedTurtles.Count == 0)
        {
            Debug.LogWarning("No turtles selected for mining task");
            return;
        }

        if (selectedTurtles.Count == 1)
        {
            // Single turtle gets all blocks
            var turtle = selectedTurtles[0];
            var miningManager = turtle.GetComponent<TurtleMiningManager>();
            if (miningManager != null)
            {
                if (skipValidation)
                    miningManager.StartPreValidatedMiningOperation(blocks);
                else
                    miningManager.StartMiningOperation(blocks);

                turtle.SetBusy(true, TurtleOperationManager.OperationType.Mining);
            }
        }
        else
        {
            // Distribute blocks among selected turtles
            DistributeBlocksAmongTurtles(blocks, skipValidation);
        }
    }

    private void DistributeBlocksAmongTurtles(List<Vector3> blocks, bool skipValidation = false)
    {
        int turtleCount = selectedTurtles.Count;
        int blocksPerTurtle = Mathf.CeilToInt(blocks.Count / (float)turtleCount);

        Debug.Log($"Distributing {blocks.Count} blocks among {turtleCount} turtles ({blocksPerTurtle} blocks per turtle)");

        for (int i = 0; i < turtleCount; i++)
        {
            var turtle = selectedTurtles[i];
            var turtleBlocks = blocks.Skip(i * blocksPerTurtle).Take(blocksPerTurtle).ToList();

            if (turtleBlocks.Count > 0)
            {
                var miningManager = turtle.GetComponent<TurtleMiningManager>();
                if (miningManager != null)
                {
                    if (skipValidation)
                        miningManager.StartPreValidatedMiningOperation(turtleBlocks);
                    else
                        miningManager.StartMiningOperation(turtleBlocks);

                    turtle.SetBusy(true, TurtleOperationManager.OperationType.Mining);
                    Debug.Log($"{turtle.turtleName} assigned {turtleBlocks.Count} blocks");
                }
            }
        }
    }

    public void AssignBuildingTask(Vector3 origin, StructureData structure)
    {
        var turtle = GetPrimarySelection();
        if (turtle == null)
        {
            Debug.LogWarning("No turtle selected for building task");
            return;
        }

        var buildingManager = turtle.GetComponent<TurtleBuildingManager>();
        if (buildingManager != null)
        {
            buildingManager.StartBuildingOperation(origin, structure);
            turtle.SetBusy(true, TurtleOperationManager.OperationType.Building);
        }
    }

    public void AssignMoveTask(Vector3 targetPosition)
    {
        foreach (var turtle in selectedTurtles)
        {
            var movementManager = turtle.GetComponent<TurtleMovementManager>();
            if (movementManager != null)
            {
                StartCoroutine(movementManager.MoveTurtleToExactPosition(targetPosition));
                turtle.SetBusy(true, TurtleOperationManager.OperationType.None);
            }
        }
    }

    private void Update()
    {
        // Deselect on ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearSelection();
        }

        // Select all on Ctrl+A
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.A))
        {
            SelectAllTurtles();
        }
    }
}
