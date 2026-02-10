using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager für Turtle-Erzabbau-Operationen
/// </summary>
public class TurtleOreMiningManager : TurtleBaseManager
{
    [Header("Ore Mining Settings")]
    [SerializeField] private int miningDepth = 64;
    [SerializeField] private int tunnelSpacing = 3;
    [SerializeField] private bool returnToSurface = true;

    [Header("Ore Detection")]
    [SerializeField] private string[] targetOres = { "diamond_ore", "iron_ore", "gold_ore", "copper_ore" };

    private bool isMiningOperation = false;
    private Vector3Int currentMiningPosition;

    protected override void Start()
    {
        base.Start();
    }

    /// <summary>
    /// Startet eine Erzabbau-Operation
    /// </summary>
    public void StartOreMining(Vector3Int startPosition)
    {
        if (isMiningOperation)
        {
            Debug.LogWarning("Ore mining operation already in progress");
            return;
        }

        currentMiningPosition = startPosition;
        StartCoroutine(ExecuteOreMining());
    }

    private IEnumerator ExecuteOreMining()
    {
        isMiningOperation = true;

        Debug.Log($"Starting ore mining at position {currentMiningPosition}");

        // Mining logic hier implementieren
        yield return new WaitForSeconds(1f);

        isMiningOperation = false;
        Debug.Log("Ore mining operation completed");
    }

    /// <summary>
    /// Überprüft ob ein Block ein Erz ist
    /// </summary>
    private bool IsOreBlock(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return false;

        foreach (var ore in targetOres)
        {
            if (blockType.Contains(ore))
                return true;
        }

        return false;
    }

    public bool IsMiningActive()
    {
        return isMiningOperation;
    }

    /// <summary>
    /// Überprüft ob genug Fuel für Mining vorhanden ist
    /// </summary>
    public bool HasSufficientFuel(int requiredFuel)
    {
        var status = GetCurrentStatus();
        return status != null && status.fuelLevel >= requiredFuel;
    }

    /// <summary>
    /// Gibt den aktuellen Fuel-Level zurück
    /// </summary>
    public int GetCurrentFuelLevel()
    {
        var status = GetCurrentStatus();
        return status?.fuelLevel ?? 0;
    }
}
