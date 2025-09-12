using System;
using UnityEngine;

/// <summary>
/// Manages operation statistics, progress tracking, and operation lifecycle
/// </summary>
public class TurtleOperationManager : MonoBehaviour
{
    public enum OperationType
    {
        None,
        Mining,
        Building,
        Moving
    }

    [Header("Operation Settings")]
    public bool enableDetailedLogging = true;

    // Current operation state
    private OperationType currentOperation = OperationType.None;
    private OperationStats currentStats;

    // Events
    public System.Action<OperationType> OnOperationStarted;
    public System.Action<OperationType, OperationStats> OnOperationCompleted;
    public System.Action<OperationStats> OnProgressUpdate;

    #region Operation Management

    /// <summary>
    /// Start new operation with given type and total count
    /// </summary>
    public void StartOperation(OperationType type, int totalBlocks)
    {
        if (currentOperation != OperationType.None)
        {
            Debug.LogWarning($"Starting {type} operation while {currentOperation} is active - cancelling previous");
            CancelOperation();
        }

        currentOperation = type;
        currentStats = new OperationStats
        {
            operationType = type,
            totalBlocks = totalBlocks,
            startTime = Time.time
        };

        if (enableDetailedLogging)
        {
            Debug.Log($"Started {type} operation with {totalBlocks} blocks");
        }

        OnOperationStarted?.Invoke(type);
    }

    /// <summary>
    /// Complete current operation
    /// </summary>
    public void CompleteOperation()
    {
        if (currentStats == null) return;

        currentStats.endTime = Time.time;
        
        if (enableDetailedLogging)
        {
            Debug.Log($"Completed {currentOperation} operation: {currentStats}");
        }

        OnOperationCompleted?.Invoke(currentOperation, currentStats);
        
        currentOperation = OperationType.None;
        currentStats = null;
    }

    /// <summary>
    /// Cancel current operation
    /// </summary>
    public void CancelOperation()
    {
        if (currentStats != null && enableDetailedLogging)
        {
            Debug.Log($"Cancelled {currentOperation} operation - Progress: {currentStats.Progress:P}");
        }

        currentOperation = OperationType.None;
        currentStats = null;
    }

    #endregion

    #region Progress Tracking

    /// <summary>
    /// Increment processed blocks count
    /// </summary>
    public void IncrementProcessed()
    {
        if (currentStats == null) return;
        
        currentStats.processedBlocks++;
        OnProgressUpdate?.Invoke(currentStats);
        
        if (enableDetailedLogging && currentStats.processedBlocks % 10 == 0)
        {
            Debug.Log($"Progress: {currentStats.Progress:P} ({currentStats.processedBlocks}/{currentStats.totalBlocks})");
        }
    }

    /// <summary>
    /// Increment skipped blocks count
    /// </summary>
    public void IncrementSkipped()
    {
        if (currentStats == null) return;
        
        currentStats.skippedBlocks++;
        OnProgressUpdate?.Invoke(currentStats);
    }

    /// <summary>
    /// Increment failed blocks count
    /// </summary>
    public void IncrementFailed()
    {
        if (currentStats == null) return;
        
        currentStats.failedBlocks++;
        OnProgressUpdate?.Invoke(currentStats);
        
        if (enableDetailedLogging)
        {
            Debug.LogWarning($"Block operation failed - Total failures: {currentStats.failedBlocks}");
        }
    }

    /// <summary>
    /// Add distance to total travel distance
    /// </summary>
    public void AddDistance(float distance)
    {
        if (currentStats == null) return;
        
        currentStats.totalDistance += distance;
    }

    /// <summary>
    /// Set optimization savings percentage
    /// </summary>
    public void SetOptimizationSavings(float savings)
    {
        if (currentStats == null) return;
        
        currentStats.optimizationSavings = savings;
    }

    #endregion

    #region Public Properties

    public OperationType CurrentOperation => currentOperation;
    public OperationStats CurrentStats => currentStats;
    public bool IsOperationActive => currentOperation != OperationType.None;
    public float Progress => currentStats?.Progress ?? 0f;

    public float EstimatedTimeRemaining
    {
        get
        {
            if (currentStats == null || currentStats.Progress <= 0f)
                return 0f;

            float elapsed = currentStats.ElapsedTime;
            float estimatedTotal = elapsed / currentStats.Progress;
            return Mathf.Max(0f, estimatedTotal - elapsed);
        }
    }

    #endregion

    #region Operation Summary

    /// <summary>
    /// Get detailed operation summary
    /// </summary>
    public string GetOperationSummary()
    {
        if (currentStats == null)
            return "No active operation";

        return $"Operation: {currentOperation}\n" +
               $"Progress: {currentStats.Progress:P} ({currentStats.processedBlocks}/{currentStats.totalBlocks})\n" +
               $"Elapsed: {currentStats.ElapsedTime:F1}s\n" +
               $"Estimated Remaining: {EstimatedTimeRemaining:F1}s\n" +
               $"Skipped: {currentStats.skippedBlocks}\n" +
               $"Failed: {currentStats.failedBlocks}\n" +
               $"Distance: {currentStats.totalDistance:F1}\n" +
               $"Optimization: {currentStats.optimizationSavings:F1}%";
    }

    /// <summary>
    /// Get compact progress string
    /// </summary>
    public string GetProgressString()
    {
        if (currentStats == null)
            return "Idle";

        return $"{currentOperation}: {currentStats.Progress:P} " +
               $"({currentStats.processedBlocks}/{currentStats.totalBlocks})";
    }

    #endregion
}

#region Operation Stats Class

[System.Serializable]
public class OperationStats
{
    public TurtleOperationManager.OperationType operationType;
    public int totalBlocks;
    public int processedBlocks;
    public int skippedBlocks;
    public int failedBlocks;
    public float startTime;
    public float endTime;
    public float totalDistance;
    public float optimizationSavings;

    public float Progress => totalBlocks > 0 ? (float)processedBlocks / totalBlocks : 0f;
    public float ElapsedTime => (endTime > 0 ? endTime : Time.time) - startTime;
    public bool IsCompleted => endTime > 0;
    
    public int SuccessfulBlocks => processedBlocks;
    public int TotalAttempted => processedBlocks + failedBlocks;
    public float SuccessRate => TotalAttempted > 0 ? (float)SuccessfulBlocks / TotalAttempted : 0f;

    public override string ToString()
    {
        return $"{operationType}: {Progress:P} - " +
               $"Blocks: {processedBlocks}/{totalBlocks} " +
               $"(Skipped: {skippedBlocks}, Failed: {failedBlocks}), " +
               $"Distance: {totalDistance:F1}, " +
               $"Time: {ElapsedTime:F1}s, " +
               $"Success Rate: {SuccessRate:P}";
    }
}

#endregion