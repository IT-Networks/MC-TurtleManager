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

        // Navigate to chest
        if (movementManager != null)
        {
            yield return StartCoroutine(movementManager.MoveTo(chestPosition));
        }

        // Store items in chest
        yield return StartCoroutine(StoreItemsInChest());

        // Return to original position
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Returning to work position {returnPosition}");
        if (movementManager != null)
        {
            yield return StartCoroutine(movementManager.MoveTo(returnPosition));
        }

        isReturningToChest = false;
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Chest return cycle complete");
    }

    /// <summary>
    /// Store all items except fuel in the chest
    /// </summary>
    public IEnumerator StoreItemsInChest()
    {
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Storing items in chest at {chestPosition}");

        // Drop all items into chest (slots 1-16)
        for (int slot = 1; slot <= 16; slot++)
        {
            // Keep fuel items
            if (IsFuelItem(slot))
            {
                continue;
            }

            // Drop item from slot
            baseManager.QueueCommand($"drop:{slot}");
            yield return new WaitForSeconds(0.2f);
        }

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
    /// </summary>
    public IEnumerator RefuelTurtle()
    {
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Starting refuel process");

        // Try to refuel from existing fuel items in inventory
        for (int slot = 1; slot <= 16; slot++)
        {
            if (IsFuelItem(slot))
            {
                baseManager.QueueCommand($"select:{slot}");
                yield return new WaitForSeconds(0.1f);
                baseManager.QueueCommand("refuel:1");
                yield return new WaitForSeconds(0.2f);

                // Check if we have enough fuel now
                if (turtle.fuelLevel >= refuelAmount)
                {
                    if (debugMode) Debug.Log($"[{turtle.turtleName}] Refuel complete");
                    yield break;
                }
            }
        }

        // If still low on fuel, try to get fuel from chest
        if (turtle.fuelLevel < lowFuelThreshold && chestPosition != Vector3.zero)
        {
            if (debugMode) Debug.Log($"[{turtle.turtleName}] Getting fuel from chest");
            yield return StartCoroutine(GetFuelFromChest());
        }
    }

    /// <summary>
    /// Navigate to chest and get fuel items
    /// </summary>
    public IEnumerator GetFuelFromChest()
    {
        Vector3 originalPosition = turtle.transform.position;

        // Navigate to chest
        if (movementManager != null)
        {
            yield return StartCoroutine(movementManager.MoveTo(chestPosition));
        }

        // Take fuel from chest
        baseManager.QueueCommand("suck"); // Pull items from chest
        yield return new WaitForSeconds(0.5f);

        // Refuel
        baseManager.QueueCommand("refuel:64");
        yield return new WaitForSeconds(0.5f);

        // Return to original position
        if (movementManager != null)
        {
            yield return StartCoroutine(movementManager.MoveTo(originalPosition));
        }

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
