using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Simplified mining validator that focuses on basic accessibility and layered mining
/// Avoids complex dependency analysis to prevent performance issues
/// </summary>
public class MiningBlockValidator : MonoBehaviour
{
    [Header("Mining Settings")]
    public bool enableSequentialValidation = true;
    public bool optimizeForLayeredMining = true;
    public float blockDependencyRadius = 20f;
    public int maxDependencyDepth = 5; // Reduced from 10
    
    [Header("Mining Strategy")]
    public MiningStrategy strategy = MiningStrategy.TopDown;
    public bool allowDiagonalAccess = false;
    public bool requireGroundAccess = false;
    
    [Header("References")]
    public TurtleWorldManager worldManager;
    public BlockWorldPathfinder pathfinder;
    
    // Simplified tracking
    private HashSet<Vector3> processedBlocks = new HashSet<Vector3>();
    
    public enum MiningStrategy
    {
        TopDown,        // Mine from top to bottom (gravity-aware)
        BottomUp,       // Mine from bottom to top
        LayerByLayer,   // Mine layer by layer
        Nearest,        // Mine nearest blocks first
        Sequential      // Mine based on dependencies
    }
    
    [System.Serializable]
    public class SequentialValidationResult
    {
        public List<Vector3> validBlocks = new List<Vector3>();
        public List<Vector3> dependentBlocks = new List<Vector3>();
        public List<MiningPhase> miningPhases = new List<MiningPhase>();
        public Dictionary<Vector3, List<Vector3>> dependencies = new Dictionary<Vector3, List<Vector3>>();
        
        public int TotalValidBlocks => validBlocks.Count + dependentBlocks.Count;
        public int PhaseCount => miningPhases.Count;
    }
    
    [System.Serializable]
    public class MiningPhase
    {
        public int phaseNumber;
        public List<Vector3> blocks = new List<Vector3>();
        public string description;
        public float estimatedTime;
        
        public override string ToString()
        {
            return $"Phase {phaseNumber}: {blocks.Count} blocks - {description}";
        }
    }
    
    [System.Serializable]
    public class ValidationResult
    {
        public bool isValid;
        public bool isDependentOnOthers;
        public List<Vector3> dependsOnBlocks = new List<Vector3>();
        public int accessibilityPhase = 0;
        public string reason;
    }

    /// <summary>
    /// Simplified validation that focuses on layers rather than complex dependencies
    /// </summary>
    public SequentialValidationResult ValidateBlocksForMining(List<Vector3> blockPositions, Vector3 turtlePosition)
    {
        var result = new SequentialValidationResult();
        
        if (!enableSequentialValidation || blockPositions.Count == 0)
        {
            result.validBlocks = ValidateBlocksSimple(blockPositions, turtlePosition);
            return result;
        }
        
        try
        {
            // Filter out invalid blocks first
            var validBlocks = FilterValidBlocks(blockPositions, turtlePosition);
            
            if (validBlocks.Count == 0)
            {
                Debug.LogWarning("No valid blocks found for mining");
                return result;
            }
            
            // Create phases based on simple layer analysis
            CreateSimplifiedPhases(validBlocks, turtlePosition, result);
            
            // Optimize order within phases
            OptimizePhasesSimple(result, turtlePosition);
            
            Debug.Log($"Mining validation completed: {result.TotalValidBlocks} blocks in {result.PhaseCount} phases");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error during mining validation: {ex.Message}");
            // Fallback to simple validation
            result.validBlocks = ValidateBlocksSimple(blockPositions, turtlePosition);
        }
        
        return result;
    }
    
    public SequentialValidationResult ValidateBlocksForMining(Vector3 blockPosition, Vector3 turtlePosition)
    {
       return ValidateBlocksForMining(new List<Vector3> { blockPosition }, turtlePosition);
    }
    
    /// <summary>
    /// Filters out blocks that are clearly invalid for mining
    /// </summary>
    private List<Vector3> FilterValidBlocks(List<Vector3> blockPositions, Vector3 turtlePosition)
    {
        var validBlocks = new List<Vector3>();
        
        foreach (var blockPos in blockPositions)
        {
            // Distance check
            if (Vector3.Distance(blockPos, turtlePosition) > blockDependencyRadius)
            {
                continue;
            }
            
            // Block exists check
            if (!IsBlockSolid(blockPos))
            {
                continue;
            }
            
            validBlocks.Add(blockPos);
        }
        
        Debug.Log($"Filtered blocks: {blockPositions.Count} -> {validBlocks.Count}");
        return validBlocks;
    }
    
    /// <summary>
    /// Creates mining phases based on Y-level layers (simplified approach)
    /// </summary>
    private void CreateSimplifiedPhases(List<Vector3> blocks, Vector3 turtlePosition, SequentialValidationResult result)
    {
        // Group blocks by Y level
        var blocksByY = blocks.GroupBy(b => (int)b.y).OrderByDescending(g => g.Key);
        
        int phaseNumber = 1;
        var allBlocksSet = new HashSet<Vector3>(blocks);
        
        foreach (var yGroup in blocksByY)
        {
            var layerBlocks = yGroup.ToList();
            
            // For top-down strategy, higher layers are mined first
            if (strategy == MiningStrategy.TopDown)
            {
                CreatePhaseForLayer(layerBlocks, phaseNumber, result, allBlocksSet, turtlePosition);
                phaseNumber++;
            }
            else if (strategy == MiningStrategy.BottomUp)
            {
                // We'll reverse the order later
                CreatePhaseForLayer(layerBlocks, phaseNumber, result, allBlocksSet, turtlePosition);
                phaseNumber++;
            }
            else
            {
                // For other strategies, treat each Y level as a separate phase
                CreatePhaseForLayer(layerBlocks, phaseNumber, result, allBlocksSet, turtlePosition);
                phaseNumber++;
            }
            
            // Prevent too many phases
            if (phaseNumber > maxDependencyDepth)
            {
                Debug.LogWarning($"Limiting phases to {maxDependencyDepth}. Combining remaining layers.");
                var remainingBlocks = blocksByY.Skip(phaseNumber - 1).SelectMany(g => g).ToList();
                if (remainingBlocks.Count > 0)
                {
                    CreatePhaseForLayer(remainingBlocks, phaseNumber, result, allBlocksSet, turtlePosition);
                }
                break;
            }
        }
        
        // Reverse order for bottom-up strategy
        if (strategy == MiningStrategy.BottomUp)
        {
            result.miningPhases.Reverse();
            for (int i = 0; i < result.miningPhases.Count; i++)
            {
                result.miningPhases[i].phaseNumber = i + 1;
            }
        }
    }
    
    /// <summary>
    /// Creates a single phase for a layer of blocks
    /// </summary>
    private void CreatePhaseForLayer(List<Vector3> layerBlocks, int phaseNumber, SequentialValidationResult result, 
                                   HashSet<Vector3> allBlocks, Vector3 turtlePosition)
    {
        if (layerBlocks.Count == 0) return;
        
        // Simple accessibility check for this layer
        var accessibleBlocks = new List<Vector3>();
        var blockedBlocks = new List<Vector3>();
        
        foreach (var block in layerBlocks)
        {
            if (IsBlockAccessible(block, allBlocks, turtlePosition))
            {
                accessibleBlocks.Add(block);
                result.validBlocks.Add(block);
            }
            else
            {
                blockedBlocks.Add(block);
                result.dependentBlocks.Add(block);
            }
        }
        
        // Create phase with all blocks from this layer
        var allLayerBlocks = accessibleBlocks.Concat(blockedBlocks).ToList();
        
        var phase = new MiningPhase
        {
            phaseNumber = phaseNumber,
            blocks = SortBlocksInPhase(allLayerBlocks),
            description = GetPhaseDescription(phaseNumber, allLayerBlocks.Count, layerBlocks[0].y),
            estimatedTime = EstimatePhaseTime(allLayerBlocks)
        };
        
        result.miningPhases.Add(phase);
        
        // Add simple dependencies (blocks depend on blocks above them)
        foreach (var block in blockedBlocks)
        {
            var dependencies = GetSimpleDependencies(block, allBlocks);
            result.dependencies[block] = dependencies;
        }
    }
    
    /// <summary>
    /// Simple check if a block is accessible (turtle can reach an adjacent position)
    /// </summary>
    private bool IsBlockAccessible(Vector3 block, HashSet<Vector3> allBlocks, Vector3 turtlePosition)
    {
        // Check if turtle can reach any adjacent position
        var adjacentPositions = GetAdjacentPositions(block);
        
        foreach (var adjacentPos in adjacentPositions)
        {
            // Position must not be occupied by another mining block
            if (allBlocks.Contains(adjacentPos)) continue;
            
            // Position must not have a solid block
            if (IsBlockSolid(adjacentPos)) continue;
            
            // Simple distance check (more sophisticated pathfinding can be added later)
            if (Vector3.Distance(adjacentPos, turtlePosition) <= blockDependencyRadius)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets simple dependencies (blocks that are above this block)
    /// </summary>
    private List<Vector3> GetSimpleDependencies(Vector3 block, HashSet<Vector3> allBlocks)
    {
        var dependencies = new List<Vector3>();
        
        // Check blocks directly above
        for (int i = 1; i <= 3; i++) // Only check 3 blocks above
        {
            Vector3 above = block + Vector3.up * i;
            if (allBlocks.Contains(above))
            {
                dependencies.Add(above);
            }
        }
        
        return dependencies;
    }
    
    /// <summary>
    /// Gets positions adjacent to a block
    /// </summary>
    private List<Vector3> GetAdjacentPositions(Vector3 block)
    {
        var positions = new List<Vector3>
        {
            block + Vector3.right,   // East
            block + Vector3.left,    // West  
            block + Vector3.forward, // North
            block + Vector3.back,    // South
            block + Vector3.up,      // Above
            block + Vector3.down     // Below
        };
        
        return positions;
    }
    
    /// <summary>
    /// Checks if there's a solid block at the given position
    /// </summary>
    private bool IsBlockSolid(Vector3 position)
    {
        if (worldManager == null) return false;
        
        var chunk = worldManager.GetChunkContaining(position);
        if (chunk == null || !chunk.IsLoaded) return false;
        
        var chunkInfo = chunk.GetChunkInfo();
        if (chunkInfo == null) return false;
        
        var blockType = chunkInfo.GetBlockTypeAt(position);
        return !string.IsNullOrEmpty(blockType) && !IsAirBlock(blockType);
    }
    
    private bool IsAirBlock(string blockType)
    {
        if (string.IsNullOrEmpty(blockType)) return true;
        string lower = blockType.ToLowerInvariant();
        return lower.Contains("air") || lower.Equals("minecraft:air");
    }
    
    /// <summary>
    /// Sorts blocks within a phase based on mining strategy
    /// </summary>
    private List<Vector3> SortBlocksInPhase(List<Vector3> blocks)
    {
        if (blocks.Count <= 1) return blocks;
        
        switch (strategy)
        {
            case MiningStrategy.TopDown:
                return blocks.OrderByDescending(b => b.y)
                           .ThenBy(b => b.x)
                           .ThenBy(b => b.z)
                           .ToList();
                
            case MiningStrategy.BottomUp:
                return blocks.OrderBy(b => b.y)
                           .ThenBy(b => b.x)
                           .ThenBy(b => b.z)
                           .ToList();
                
            case MiningStrategy.LayerByLayer:
                return blocks.OrderBy(b => b.y)
                           .ThenBy(b => Vector3.Distance(b, blocks[0]))
                           .ToList();
                
            case MiningStrategy.Nearest:
                var reference = blocks[0];
                return blocks.OrderBy(b => Vector3.Distance(b, reference)).ToList();
                
            case MiningStrategy.Sequential:
            default:
                return OptimizeBlockOrder(blocks);
        }
    }
    
    /// <summary>
    /// Optimizes block order using nearest-neighbor approach
    /// </summary>
    private List<Vector3> OptimizeBlockOrder(List<Vector3> blocks)
    {
        if (blocks.Count <= 1) return blocks;
        
        var optimized = new List<Vector3>();
        var remaining = new List<Vector3>(blocks);
        
        // Start with first block
        optimized.Add(remaining[0]);
        remaining.RemoveAt(0);
        
        // Use nearest-neighbor approach
        while (remaining.Count > 0)
        {
            Vector3 current = optimized[optimized.Count - 1];
            
            // Find nearest unprocessed block
            int nearestIndex = 0;
            float nearestDistance = Vector3.Distance(current, remaining[0]);
            
            for (int i = 1; i < remaining.Count; i++)
            {
                float distance = Vector3.Distance(current, remaining[i]);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }
            
            optimized.Add(remaining[nearestIndex]);
            remaining.RemoveAt(nearestIndex);
        }
        
        return optimized;
    }
    
    private string GetPhaseDescription(int phaseNumber, int blockCount, float yLevel)
    {
        return $"Layer Y={yLevel:F0} - {blockCount} blocks";
    }
    
    private float EstimatePhaseTime(List<Vector3> blocks)
    {
        // Simple estimation
        return blocks.Count * 2.5f + 3f;
    }
    
    /// <summary>
    /// Simple optimization of phases
    /// </summary>
    private void OptimizePhasesSimple(SequentialValidationResult result, Vector3 turtlePosition)
    {
        if (!optimizeForLayeredMining) return;
        
        foreach (var phase in result.miningPhases)
        {
            if (phase.blocks.Count <= 1) continue;
            
            // Optimize block order within phase
            phase.blocks = OptimizeBlockOrder(phase.blocks);
        }
    }
    
    /// <summary>
    /// Simple validation fallback
    /// </summary>
    private List<Vector3> ValidateBlocksSimple(List<Vector3> blockPositions, Vector3 turtlePosition)
    {
        return blockPositions.Where(block => 
        {
            // Only include solid blocks that are within range
            if (!IsBlockSolid(block)) return false;
            
            float distance = Vector3.Distance(block, turtlePosition);
            return distance <= blockDependencyRadius;
        }).ToList();
    }
    
    /// <summary>
    /// Get a flat list of all blocks in mining order
    /// </summary>
    public List<Vector3> GetOptimizedMiningOrder(SequentialValidationResult result)
    {
        var order = new List<Vector3>();
        
        foreach (var phase in result.miningPhases)
        {
            order.AddRange(phase.blocks);
        }
        
        return order;
    }
    
    /// <summary>
    /// Get simplified mining report
    /// </summary>
    public string GetMiningReport(SequentialValidationResult result)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== SIMPLIFIED MINING ANALYSIS ===");
        report.AppendLine($"Strategy: {strategy}");
        report.AppendLine($"Total Blocks: {result.TotalValidBlocks}");
        report.AppendLine($"Immediately Accessible: {result.validBlocks.Count}");
        report.AppendLine($"Dependent Blocks: {result.dependentBlocks.Count}");
        report.AppendLine($"Mining Phases: {result.PhaseCount}");
        
        float totalTime = 0;
        report.AppendLine("\n=== MINING PHASES ===");
        foreach (var phase in result.miningPhases)
        {
            report.AppendLine($"{phase} (Est. {phase.estimatedTime:F1}s)");
            totalTime += phase.estimatedTime;
            
            // Show Y-levels in each phase
            if (phase.blocks.Count > 0)
            {
                var yLevels = phase.blocks.Select(b => b.y).Distinct().OrderByDescending(y => y);
                report.AppendLine($"  Y-Levels: {string.Join(", ", yLevels.Select(y => y.ToString("F0")))}");
            }
        }
        
        report.AppendLine($"\nTotal Estimated Time: {totalTime:F1} seconds");
        
        return report.ToString();
    }

    internal string GetValidationReport(List<Vector3> blocks, Vector3 turtlePos)
    {
        var result = ValidateBlocksForMining(blocks, turtlePos);
        return GetMiningReport(result);
    }
}
