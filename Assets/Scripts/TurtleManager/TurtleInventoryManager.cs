using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages turtle inventory operations including chest storage, fuel management, and item sorting
/// </summary>
public class TurtleInventoryManager : MonoBehaviour
{
    [Header("References")]
    public TurtleObject turtle;
    public TurtleBaseManager baseManager;
    public TurtleMovementManager movementManager;

    [Header("Storage Settings")]
    public Vector3 chestPosition = Vector3.zero;
    public bool autoReturnToChest = true;
    public int inventoryFullThreshold = 14; // Return to chest when this many slots are used

    [Header("Fuel Settings")]
    public int lowFuelThreshold = 500;
    public int refuelAmount = 1000;
    public bool autoRefuel = true;

    [Header("Debug")]
    public bool debugMode = false;

    private bool isReturningToChest = false;
    private Vector3 returnPosition; // Position to return to after storing items

    void Start()
    {
        if (turtle == null) turtle = GetComponent<TurtleObject>();
        if (baseManager == null) baseManager = GetComponent<TurtleBaseManager>();
        if (movementManager == null) movementManager = GetComponent<TurtleMovementManager>();
    }

    void Update()
    {
        if (turtle == null || baseManager == null) return;

        // Check if inventory is full
        if (autoReturnToChest && !isReturningToChest && IsInventoryNearlyFull())
        {
            if (debugMode) Debug.Log($"[{turtle.turtleName}] Inventory nearly full, returning to chest");
            StartCoroutine(ReturnToChestAndBack());
        }

        // Check fuel level
        if (autoRefuel && !turtle.isBusy && IsFuelLow())
        {
            if (debugMode) Debug.Log($"[{turtle.turtleName}] Fuel low, attempting refuel");
            StartCoroutine(RefuelTurtle());
        }
    }

    /// <summary>
    /// Check if inventory is nearly full
    /// </summary>
    public bool IsInventoryNearlyFull()
    {
        return turtle.inventorySlotsUsed >= inventoryFullThreshold;
    }

    /// <summary>
    /// Check if fuel is low
    /// </summary>
    public bool IsFuelLow()
    {
        return turtle.fuelLevel < lowFuelThreshold;
    }

    /// <summary>
    /// Return to chest, store items, and return to original position
    /// Unity sends commands to turtle, turtle executes them
    /// </summary>
    public IEnumerator ReturnToChestAndBack()
    {
        if (isReturningToChest)
        {
            if (debugMode) Debug.Log($"[{turtle.turtleName}] Already returning to chest");
            yield break;
        }

        isReturningToChest = true;
        returnPosition = turtle.transform.position;

        if (debugMode) Debug.Log($"[{turtle.turtleName}] Starting chest return from {returnPosition}");

        // Send navigation commands to chest
        yield return StartCoroutine(SendNavigationCommands(turtle.transform.position, chestPosition));

        // Store items in chest
        yield return StartCoroutine(StoreItemsInChest());

        // Return to original position
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Returning to work position {returnPosition}");
        yield return StartCoroutine(SendNavigationCommands(turtle.transform.position, returnPosition));

        isReturningToChest = false;
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Chest return cycle complete");
    }

    /// <summary>
    /// Send navigation commands to move turtle from current position to target
    /// </summary>
    private IEnumerator SendNavigationCommands(Vector3 from, Vector3 to)
    {
        Vector3 current = from;

        // Move vertically first
        while (current.y != to.y)
        {
            if (current.y < to.y)
            {
                baseManager.QueueCommand(new TurtleCommand("up"));
                current += Vector3.up;
            }
            else
            {
                baseManager.QueueCommand(new TurtleCommand("down"));
                current += Vector3.down;
            }
            yield return new WaitForSeconds(0.3f);
        }

        // Move along X axis
        while (current.x != to.x)
        {
            // Rotate turtle to face the correct direction and move forward
            baseManager.QueueCommand(new TurtleCommand("forward"));
            if (current.x < to.x)
                current += Vector3.right;
            else
                current += Vector3.left;
            yield return new WaitForSeconds(0.3f);
        }

        // Move along Z axis
        while (current.z != to.z)
        {
            baseManager.QueueCommand(new TurtleCommand("forward"));
            if (current.z < to.z)
                current += Vector3.forward;
            else
                current += Vector3.back;
            yield return new WaitForSeconds(0.3f);
        }
    }

    /// <summary>
    /// Store all items except fuel in the chest
    /// Sends commands to turtle to drop items
    /// </summary>
    public IEnumerator StoreItemsInChest()
    {
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Storing items in chest at {chestPosition}");

        // Send command to drop all non-fuel items into chest below
        // The turtle should be positioned above the chest
        baseManager.QueueCommand(new TurtleCommand("dropdown")); // drops all non-fuel items
        yield return new WaitForSeconds(1.0f);

        if (debugMode) Debug.Log($"[{turtle.turtleName}] Items stored in chest");
    }

    /// <summary>
    /// Check if a slot contains a fuel item
    /// </summary>
    private bool IsFuelItem(int slot)
    {
        if (turtle.inventory == null || turtle.inventory.Count == 0) return false;

        var item = turtle.inventory.FirstOrDefault(i => i.slot == slot);
        if (item == null) return false;

        // Common fuel items
        string[] fuelItems = {
            "minecraft:coal",
            "minecraft:charcoal",
            "minecraft:coal_block",
            "minecraft:lava_bucket",
            "minecraft:blaze_powder",
            "minecraft:blaze_rod"
        };

        return fuelItems.Contains(item.name);
    }

    /// <summary>
    /// Refuel the turtle from inventory
    /// Sends refuel command to turtle
    /// </summary>
    public IEnumerator RefuelTurtle()
    {
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Starting refuel process");

        // Send refuel command - turtle will try to refuel from inventory
        baseManager.QueueCommand(new TurtleCommand("refuel:64"));
        yield return new WaitForSeconds(1.0f);

        // If still low on fuel after 2 seconds, try to get fuel from chest
        if (turtle.fuelLevel < lowFuelThreshold && chestPosition != Vector3.zero)
        {
            if (debugMode) Debug.Log($"[{turtle.turtleName}] Getting fuel from chest");
            yield return StartCoroutine(GetFuelFromChest());
        }
    }

    /// <summary>
    /// Navigate to chest and get fuel items
    /// Sends commands to turtle
    /// </summary>
    public IEnumerator GetFuelFromChest()
    {
        Vector3 originalPosition = turtle.transform.position;

        // Navigate to chest
        yield return StartCoroutine(SendNavigationCommands(turtle.transform.position, chestPosition));

        // Take fuel from chest (turtle is above chest)
        baseManager.QueueCommand(new TurtleCommand("suckdown"));
        yield return new WaitForSeconds(0.5f);

        // Refuel
        baseManager.QueueCommand(new TurtleCommand("refuel:64"));
        yield return new WaitForSeconds(0.5f);

        // Return to original position
        yield return StartCoroutine(SendNavigationCommands(turtle.transform.position, originalPosition));

        if (debugMode) Debug.Log($"[{turtle.turtleName}] Fuel restocked from chest");
    }

    /// <summary>
    /// Set the chest position for storage
    /// </summary>
    public void SetChestPosition(Vector3 position)
    {
        chestPosition = position;
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Chest position set to {position}");
    }

    /// <summary>
    /// Get current inventory status
    /// </summary>
    public Dictionary<string, int> GetInventorySummary()
    {
        Dictionary<string, int> summary = new Dictionary<string, int>();

        if (turtle.inventory != null)
        {
            foreach (var item in turtle.inventory)
            {
                if (summary.ContainsKey(item.name))
                {
                    summary[item.name] += item.count;
                }
                else
                {
                    summary[item.name] = item.count;
                }
            }
        }

        return summary;
    }

    /// <summary>
    /// Count specific item in inventory
    /// </summary>
    public int CountItem(string itemName)
    {
        if (turtle.inventory == null) return 0;

        return turtle.inventory
            .Where(i => i.name == itemName)
            .Sum(i => i.count);
    }

    /// <summary>
    /// Check if turtle has enough fuel for a distance
    /// </summary>
    public bool HasEnoughFuel(int distance)
    {
        return turtle.fuelLevel >= distance + lowFuelThreshold;
    }

    /// <summary>
    /// Get estimated moves remaining with current fuel
    /// </summary>
    public int GetMovesRemaining()
    {
        return Mathf.Max(0, turtle.fuelLevel - lowFuelThreshold);
    }

    void OnDrawGizmosSelected()
    {
        if (chestPosition != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(chestPosition, Vector3.one);
            Gizmos.DrawLine(transform.position, chestPosition);
        }
    }
}
