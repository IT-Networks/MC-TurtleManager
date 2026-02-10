using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages automatic ore mining operations for turtles
/// Features:
/// - Automatic ore detection and targeting
/// - Intelligent mining patterns
/// - Integration with inventory and fuel management
/// - Multi-ore type support
/// </summary>
public class TurtleOreMiningManager : MonoBehaviour
{
    [Header("References")]
    public TurtleObject turtle;
    public TurtleBaseManager baseManager;
    public TurtleMovementManager movementManager;
    public TurtleMiningManager miningManager;
    public TurtleInventoryManager inventoryManager;
    public TurtleWorldManager worldManager;

    [Header("Ore Detection Settings")]
    public bool autoDetectOres = true;
    public float scanRadius = 16f;
    public float scanInterval = 5f;

    [Header("Target Ores")]
    public bool mineCoal = true;
    public bool mineIron = true;
    public bool mineGold = true;
    public bool mineDiamond = true;
    public bool mineEmerald = true;
    public bool mineLapis = true;
    public bool mineRedstone = true;
    public bool mineCopper = true;

    [Header("Modded Ores (ATM10)")]
    public bool mineAllTheModium = true;
    public bool mineVibranium = true;
    public bool mineUnobtainium = true;
    public bool mineOsmium = true;
    public bool mineTin = true;
    public bool mineLead = true;
    public bool mineUranium = true;
    public bool mineZinc = true;

    [Header("Mining Behavior")]
    public bool enableVeinMining = true; // Mine connected ores
    public int maxVeinSize = 32;
    public bool returnToStartAfterMining = true;
    public bool prioritizeRareOres = true;

    [Header("Debug")]
    public bool debugMode = false;

    private List<Vector3> detectedOres = new List<Vector3>();
    private List<Vector3> minedPositions = new List<Vector3>();
    private bool isMiningOres = false;
    private float lastScanTime = 0f;
    private Vector3 startPosition;

    private Dictionary<string, int> orePriorities = new Dictionary<string, int>
    {
        // Vanilla ores
        { "minecraft:diamond_ore", 10 },
        { "minecraft:deepslate_diamond_ore", 10 },
        { "minecraft:emerald_ore", 9 },
        { "minecraft:deepslate_emerald_ore", 9 },
        { "minecraft:gold_ore", 7 },
        { "minecraft:deepslate_gold_ore", 7 },
        { "minecraft:iron_ore", 6 },
        { "minecraft:deepslate_iron_ore", 6 },
        { "minecraft:lapis_ore", 5 },
        { "minecraft:deepslate_lapis_ore", 5 },
        { "minecraft:redstone_ore", 5 },
        { "minecraft:deepslate_redstone_ore", 5 },
        { "minecraft:copper_ore", 4 },
        { "minecraft:deepslate_copper_ore", 4 },
        { "minecraft:coal_ore", 3 },
        { "minecraft:deepslate_coal_ore", 3 },

        // ATM10 modded ores
        { "allthemodium:allthemodium_ore", 15 },
        { "allthemodium:vibranium_ore", 14 },
        { "allthemodium:unobtainium_ore", 13 },
        { "mekanism:osmium_ore", 8 },
        { "mekanism:deepslate_osmium_ore", 8 },
        { "thermal:tin_ore", 6 },
        { "thermal:deepslate_tin_ore", 6 },
        { "thermal:lead_ore", 6 },
        { "thermal:deepslate_lead_ore", 6 },
        { "mekanism:uranium_ore", 7 },
        { "mekanism:deepslate_uranium_ore", 7 },
        { "create:zinc_ore", 5 },
        { "create:deepslate_zinc_ore", 5 },
    };

    void Start()
    {
        if (turtle == null) turtle = GetComponent<TurtleObject>();
        if (baseManager == null) baseManager = GetComponent<TurtleBaseManager>();
        if (movementManager == null) movementManager = GetComponent<TurtleMovementManager>();
        if (miningManager == null) miningManager = GetComponent<TurtleMiningManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<TurtleInventoryManager>();

        startPosition = transform.position;
    }

    void Update()
    {
        if (!autoDetectOres || isMiningOres || turtle.isBusy) return;

        // Periodic ore scanning
        if (Time.time - lastScanTime > scanInterval)
        {
            ScanForOres();
            lastScanTime = Time.time;
        }

        // Start mining if ores are detected
        if (detectedOres.Count > 0 && !isMiningOres)
        {
            StartCoroutine(MineDetectedOres());
        }
    }

    /// <summary>
    /// Scan for ores in the surrounding area
    /// </summary>
    public void ScanForOres()
    {
        if (worldManager == null)
        {
            worldManager = FindObjectOfType<TurtleWorldManager>();
            if (worldManager == null) return;
        }

        Vector3 turtlePos = transform.position;
        detectedOres.Clear();

        // Get all blocks in scan radius
        List<Vector3> nearbyBlocks = GetBlocksInRadius(turtlePos, scanRadius);

        // TODO: Implement block scanning using ChunkManager
        // TurtleWorldManager doesn't have GetBlockTypeAtPosition method
        // This functionality needs to be implemented to scan for ores

        // foreach (Vector3 blockPos in nearbyBlocks)
        // {
        //     // Skip already mined positions
        //     if (minedPositions.Contains(blockPos)) continue;
        //
        //     // Check if block is an ore
        //     string blockType = worldManager.GetBlockTypeAtPosition(blockPos);
        //     if (IsOreBlock(blockType))
        //     {
        //         detectedOres.Add(blockPos);
        //     }
        // }

        if (detectedOres.Count > 0 && debugMode)
        {
            Debug.Log($"[{turtle.turtleName}] Detected {detectedOres.Count} ore blocks");
        }
    }

    /// <summary>
    /// Check if a block type is an ore we want to mine
    /// </summary>
    private bool IsOreBlock(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return false;

        // Check vanilla ores
        if (mineCoal && blockType.Contains("coal_ore")) return true;
        if (mineIron && blockType.Contains("iron_ore")) return true;
        if (mineGold && blockType.Contains("gold_ore")) return true;
        if (mineDiamond && blockType.Contains("diamond_ore")) return true;
        if (mineEmerald && blockType.Contains("emerald_ore")) return true;
        if (mineLapis && blockType.Contains("lapis_ore")) return true;
        if (mineRedstone && blockType.Contains("redstone_ore")) return true;
        if (mineCopper && blockType.Contains("copper_ore")) return true;

        // Check modded ores
        if (mineAllTheModium && blockType.Contains("allthemodium_ore")) return true;
        if (mineVibranium && blockType.Contains("vibranium_ore")) return true;
        if (mineUnobtainium && blockType.Contains("unobtainium_ore")) return true;
        if (mineOsmium && blockType.Contains("osmium_ore")) return true;
        if (mineTin && blockType.Contains("tin_ore")) return true;
        if (mineLead && blockType.Contains("lead_ore")) return true;
        if (mineUranium && blockType.Contains("uranium_ore")) return true;
        if (mineZinc && blockType.Contains("zinc_ore")) return true;

        return false;
    }

    /// <summary>
    /// Get priority value for an ore
    /// </summary>
    private int GetOrePriority(string blockType)
    {
        if (orePriorities.ContainsKey(blockType))
        {
            return orePriorities[blockType];
        }
        return 1; // Default priority
    }

    /// <summary>
    /// Mine all detected ores
    /// </summary>
    public IEnumerator MineDetectedOres()
    {
        if (detectedOres.Count == 0) yield break;

        isMiningOres = true;

        // Sort ores by priority if enabled
        if (prioritizeRareOres)
        {
            // TODO: Implement block type checking for ore prioritization
            // detectedOres = detectedOres.OrderByDescending(pos =>
            // {
            //     string blockType = worldManager.GetBlockTypeAtPosition(pos);
            //     return GetOrePriority(blockType);
            // }).ToList();
        }

        if (debugMode) Debug.Log($"[{turtle.turtleName}] Starting ore mining operation for {detectedOres.Count} ores");

        foreach (Vector3 orePos in detectedOres.ToList())
        {
            // Check inventory before mining
            if (inventoryManager != null && inventoryManager.IsInventoryNearlyFull())
            {
                if (debugMode) Debug.Log($"[{turtle.turtleName}] Inventory full, pausing ore mining");
                yield return StartCoroutine(inventoryManager.ReturnToChestAndBack());
            }

            // Check fuel
            if (inventoryManager != null && inventoryManager.IsFuelLow())
            {
                if (debugMode) Debug.Log($"[{turtle.turtleName}] Fuel low, refueling");
                yield return StartCoroutine(inventoryManager.RefuelTurtle());
            }

            // Mine the ore
            if (enableVeinMining)
            {
                yield return StartCoroutine(MineOreVein(orePos));
            }
            else
            {
                yield return StartCoroutine(MineSingleOre(orePos));
            }

            minedPositions.Add(orePos);
        }

        // Return to start position if enabled
        if (returnToStartAfterMining && movementManager != null)
        {
            if (debugMode) Debug.Log($"[{turtle.turtleName}] Returning to start position");
            yield return StartCoroutine(movementManager.MoveTurtleToExactPosition(startPosition));
        }

        detectedOres.Clear();
        isMiningOres = false;

        if (debugMode) Debug.Log($"[{turtle.turtleName}] Ore mining operation complete");
    }

    /// <summary>
    /// Mine a single ore block by sending commands to turtle
    /// </summary>
    private IEnumerator MineSingleOre(Vector3 orePos)
    {
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Mining ore at {orePos}");

        // Calculate path to ore
        Vector3 currentPos = turtle.transform.position;
        List<Vector3> path = CalculatePathToTarget(currentPos, orePos);

        // Send movement commands
        foreach (Vector3 waypoint in path)
        {
            Vector3 direction = waypoint - currentPos;

            // Dig if there's a block in the way
            // TODO: Implement block type checking
            // For now, always try to dig when moving
            {
                // Determine dig direction
                if (direction.y > 0)
                    baseManager.QueueCommand(new TurtleCommand("digup"));
                else if (direction.y < 0)
                    baseManager.QueueCommand(new TurtleCommand("digdown"));
                else
                    baseManager.QueueCommand(new TurtleCommand("dig"));

                yield return new WaitForSeconds(0.5f);
            }

            // Move in direction
            if (direction.y > 0)
                baseManager.QueueCommand(new TurtleCommand("up"));
            else if (direction.y < 0)
                baseManager.QueueCommand(new TurtleCommand("down"));
            else if (direction.z > 0 || direction.x > 0 || direction.z < 0 || direction.x < 0)
                baseManager.QueueCommand(new TurtleCommand("forward"));

            currentPos = waypoint;
            yield return new WaitForSeconds(0.3f);
        }

        // Mine the ore block itself
        baseManager.QueueCommand(new TurtleCommand("dig"));
        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// Calculate simple path from current position to target
    /// </summary>
    private List<Vector3> CalculatePathToTarget(Vector3 from, Vector3 to)
    {
        List<Vector3> path = new List<Vector3>();
        Vector3 current = from;

        // Simple path: move Y first, then X, then Z
        // Move vertically
        while (current.y != to.y)
        {
            if (current.y < to.y)
                current += Vector3.up;
            else
                current += Vector3.down;
            path.Add(current);
        }

        // Move along X axis
        while (current.x != to.x)
        {
            if (current.x < to.x)
                current += Vector3.right;
            else
                current += Vector3.left;
            path.Add(current);
        }

        // Move along Z axis
        while (current.z != to.z)
        {
            if (current.z < to.z)
                current += Vector3.forward;
            else
                current += Vector3.back;
            path.Add(current);
        }

        return path;
    }

    /// <summary>
    /// Mine an entire ore vein (connected ores)
    /// </summary>
    private IEnumerator MineOreVein(Vector3 startOrePos)
    {
        List<Vector3> vein = new List<Vector3>();
        Queue<Vector3> toCheck = new Queue<Vector3>();
        HashSet<Vector3> checkedPositions = new HashSet<Vector3>();

        toCheck.Enqueue(startOrePos);

        // Find all connected ore blocks
        while (toCheck.Count > 0 && vein.Count < maxVeinSize)
        {
            Vector3 current = toCheck.Dequeue();
            if (checkedPositions.Contains(current)) continue;

            checkedPositions.Add(current);

            // TODO: Implement block type checking for vein detection
            // string blockType = worldManager.GetBlockTypeAtPosition(current);
            // if (IsOreBlock(blockType))
            // For now, just add the position
            {
                vein.Add(current);

                // Check adjacent blocks
                Vector3[] neighbors = {
                    current + Vector3.up,
                    current + Vector3.down,
                    current + Vector3.forward,
                    current + Vector3.back,
                    current + Vector3.left,
                    current + Vector3.right
                };

                foreach (Vector3 neighbor in neighbors)
                {
                    if (!checkedPositions.Contains(neighbor))
                    {
                        toCheck.Enqueue(neighbor);
                    }
                }
            }
        }

        if (debugMode) Debug.Log($"[{turtle.turtleName}] Found ore vein with {vein.Count} blocks");

        // Mine all blocks in vein
        foreach (Vector3 orePos in vein)
        {
            yield return StartCoroutine(MineSingleOre(orePos));
            minedPositions.Add(orePos);
        }
    }

    /// <summary>
    /// Get all blocks within a radius
    /// </summary>
    private List<Vector3> GetBlocksInRadius(Vector3 center, float radius)
    {
        List<Vector3> blocks = new List<Vector3>();

        int r = Mathf.CeilToInt(radius);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int z = -r; z <= r; z++)
                {
                    Vector3 pos = center + new Vector3(x, y, z);
                    if (Vector3.Distance(center, pos) <= radius)
                    {
                        blocks.Add(pos);
                    }
                }
            }
        }

        return blocks;
    }

    /// <summary>
    /// Start automatic ore mining
    /// </summary>
    public void StartOreMining()
    {
        autoDetectOres = true;
        startPosition = transform.position;
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Automatic ore mining enabled");
    }

    /// <summary>
    /// Stop automatic ore mining
    /// </summary>
    public void StopOreMining()
    {
        autoDetectOres = false;
        StopAllCoroutines();
        isMiningOres = false;
        if (debugMode) Debug.Log($"[{turtle.turtleName}] Automatic ore mining disabled");
    }

    /// <summary>
    /// Get statistics about mined ores
    /// </summary>
    public Dictionary<string, int> GetMiningStatistics()
    {
        Dictionary<string, int> stats = new Dictionary<string, int>();
        stats["total_mined"] = minedPositions.Count;
        stats["current_detected"] = detectedOres.Count;
        stats["is_mining"] = isMiningOres ? 1 : 0;
        return stats;
    }

    void OnDrawGizmosSelected()
    {
        // Draw scan radius
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, scanRadius);

        // Draw detected ores
        Gizmos.color = Color.yellow;
        foreach (Vector3 ore in detectedOres)
        {
            Gizmos.DrawCube(ore, Vector3.one * 0.8f);
        }

        // Draw mined positions
        Gizmos.color = Color.gray;
        foreach (Vector3 mined in minedPositions)
        {
            Gizmos.DrawWireCube(mined, Vector3.one * 0.5f);
        }

        // Draw start position
        if (startPosition != Vector3.zero)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(startPosition, Vector3.one);
        }
    }
}
