using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Modern column-based mining optimizer for efficient multi-level excavation
/// Eliminates up/down looping by mining vertical columns systematically
/// </summary>
public class ColumnBasedMiningOptimizer : MonoBehaviour
{
    [Header("Mining Strategy")]
    [Tooltip("Mining pattern for traversing columns")]
    public MiningPattern pattern = MiningPattern.SweepLine;

    [Tooltip("Direction within each column")]
    public ColumnDirection columnDirection = ColumnDirection.TopDown;

    [Tooltip("Enable smart column grouping for faster mining")]
    public bool enableColumnGrouping = true;

    [Tooltip("Maximum distance to group adjacent columns")]
    public float columnGroupingRadius = 3f;

    [Header("Performance")]
    [Tooltip("Enable pathfinding optimization (reuse position for column)")]
    public bool optimizePathfinding = true;

    [Tooltip("Show debug visualization")]
    public bool showDebugInfo = false;

    public enum MiningPattern
    {
        SweepLine,      // Left-to-right, front-to-back sweep
        NearestColumn,  // Always mine nearest column (greedy)
        Spiral,         // Spiral from center or edge
        ZigZag          // Zig-zag pattern to minimize travel
    }

    public enum ColumnDirection
    {
        TopDown,    // Mine from top to bottom (stable, prevents cave-ins)
        BottomUp    // Mine from bottom to top
    }

    /// <summary>
    /// Represents a vertical column of blocks to mine
    /// </summary>
    [System.Serializable]
    public class MiningColumn
    {
        public Vector2Int horizontalPos; // X, Z position
        public List<Vector3> blocks;     // All blocks in this column (sorted by Y)
        public Vector3 bottomBlock;
        public Vector3 topBlock;
        public int height;
        public Vector3 miningPosition;   // Where turtle should stand to mine this column

        public MiningColumn(Vector2Int pos)
        {
            horizontalPos = pos;
            blocks = new List<Vector3>();
        }

        public void SortBlocks(ColumnDirection direction)
        {
            if (direction == ColumnDirection.TopDown)
                blocks = blocks.OrderByDescending(b => b.y).ToList();
            else
                blocks = blocks.OrderBy(b => b.y).ToList();

            bottomBlock = blocks.OrderBy(b => b.y).First();
            topBlock = blocks.OrderByDescending(b => b.y).First();
            height = blocks.Count;
        }

        public override string ToString()
        {
            return $"Column ({horizontalPos.x}, {horizontalPos.y}): {height} blocks at Y=[{bottomBlock.y:F0}-{topBlock.y:F0}]";
        }
    }

    /// <summary>
    /// Result of column-based mining optimization
    /// </summary>
    [System.Serializable]
    public class ColumnMiningPlan
    {
        public List<MiningColumn> columns = new List<MiningColumn>();
        public List<Vector3> optimizedBlockOrder = new List<Vector3>();
        public int totalBlocks;
        public int totalColumns;
        public float estimatedTime;
        public string patternUsed;

        public string GetReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== COLUMN-BASED MINING PLAN ===");
            report.AppendLine($"Pattern: {patternUsed}");
            report.AppendLine($"Total Blocks: {totalBlocks}");
            report.AppendLine($"Total Columns: {totalColumns}");
            report.AppendLine($"Estimated Time: {estimatedTime:F1}s");
            report.AppendLine($"Blocks per Column: {(totalBlocks / (float)totalColumns):F1} avg");

            report.AppendLine("\n=== COLUMN DISTRIBUTION ===");
            var heightGroups = columns.GroupBy(c => c.height).OrderBy(g => g.Key);
            foreach (var group in heightGroups)
            {
                report.AppendLine($"  Height {group.Key}: {group.Count()} columns");
            }

            return report.ToString();
        }
    }

    /// <summary>
    /// Optimizes mining blocks into column-based execution plan
    /// </summary>
    public ColumnMiningPlan OptimizeMining(List<Vector3> blockPositions, Vector3 turtlePosition)
    {
        var plan = new ColumnMiningPlan();
        plan.patternUsed = pattern.ToString();

        if (blockPositions == null || blockPositions.Count == 0)
        {
            Debug.LogWarning("No blocks to optimize for mining");
            return plan;
        }

        // Step 1: Group blocks into vertical columns
        var columns = GroupIntoColumns(blockPositions);

        // Step 2: Sort columns by chosen pattern
        columns = SortColumnsByPattern(columns, turtlePosition);

        // Step 3: Generate optimized block order
        plan.optimizedBlockOrder = GenerateBlockOrder(columns);
        plan.columns = columns;
        plan.totalBlocks = blockPositions.Count;
        plan.totalColumns = columns.Count;
        plan.estimatedTime = EstimateMiningTime(plan);

        if (showDebugInfo)
        {
            Debug.Log(plan.GetReport());
        }

        return plan;
    }

    /// <summary>
    /// Groups blocks into vertical columns based on X,Z position
    /// </summary>
    private List<MiningColumn> GroupIntoColumns(List<Vector3> blocks)
    {
        var columnDict = new Dictionary<Vector2Int, MiningColumn>();

        foreach (var block in blocks)
        {
            // Round to integer coordinates
            Vector2Int horizontalPos = new Vector2Int(
                Mathf.RoundToInt(block.x),
                Mathf.RoundToInt(block.z)
            );

            if (!columnDict.ContainsKey(horizontalPos))
            {
                columnDict[horizontalPos] = new MiningColumn(horizontalPos);
            }

            columnDict[horizontalPos].blocks.Add(block);
        }

        // Sort blocks within each column
        var columns = columnDict.Values.ToList();
        foreach (var column in columns)
        {
            column.SortBlocks(columnDirection);
        }

        if (showDebugInfo)
        {
            Debug.Log($"Grouped {blocks.Count} blocks into {columns.Count} columns");
        }

        return columns;
    }

    /// <summary>
    /// Sorts columns based on chosen mining pattern
    /// </summary>
    private List<MiningColumn> SortColumnsByPattern(List<MiningColumn> columns, Vector3 turtlePos)
    {
        switch (pattern)
        {
            case MiningPattern.SweepLine:
                return SortBySweepLine(columns);

            case MiningPattern.NearestColumn:
                return SortByNearest(columns, turtlePos);

            case MiningPattern.Spiral:
                return SortBySpiral(columns, turtlePos);

            case MiningPattern.ZigZag:
                return SortByZigZag(columns);

            default:
                return columns;
        }
    }

    /// <summary>
    /// Sweep-line pattern: Mine left-to-right, front-to-back
    /// </summary>
    private List<MiningColumn> SortBySweepLine(List<MiningColumn> columns)
    {
        // Sort by Z first (rows), then X (columns within row)
        return columns.OrderBy(c => c.horizontalPos.y)
                     .ThenBy(c => c.horizontalPos.x)
                     .ToList();
    }

    /// <summary>
    /// Greedy nearest-column approach
    /// </summary>
    private List<MiningColumn> SortByNearest(List<MiningColumn> columns, Vector3 startPos)
    {
        if (columns.Count == 0) return columns;

        var sorted = new List<MiningColumn>();
        var remaining = new List<MiningColumn>(columns);

        // Start with nearest column to turtle
        Vector2Int currentPos = new Vector2Int(
            Mathf.RoundToInt(startPos.x),
            Mathf.RoundToInt(startPos.z)
        );

        while (remaining.Count > 0)
        {
            // Find nearest column
            var nearest = remaining.OrderBy(c =>
                Vector2Int.Distance(c.horizontalPos, currentPos)
            ).First();

            sorted.Add(nearest);
            remaining.Remove(nearest);
            currentPos = nearest.horizontalPos;
        }

        return sorted;
    }

    /// <summary>
    /// Spiral pattern from turtle position outward
    /// </summary>
    private List<MiningColumn> SortBySpiral(List<MiningColumn> columns, Vector3 startPos)
    {
        Vector2Int center = new Vector2Int(
            Mathf.RoundToInt(startPos.x),
            Mathf.RoundToInt(startPos.z)
        );

        // Sort by distance from center, then by angle
        return columns.OrderBy(c => Vector2Int.Distance(c.horizontalPos, center))
                     .ThenBy(c => Mathf.Atan2(
                         c.horizontalPos.y - center.y,
                         c.horizontalPos.x - center.x))
                     .ToList();
    }

    /// <summary>
    /// Zig-zag pattern: Alternate direction each row
    /// </summary>
    private List<MiningColumn> SortByZigZag(List<MiningColumn> columns)
    {
        // Group by Z (rows)
        var rows = columns.GroupBy(c => c.horizontalPos.y)
                         .OrderBy(g => g.Key)
                         .ToList();

        var sorted = new List<MiningColumn>();
        bool leftToRight = true;

        foreach (var row in rows)
        {
            var rowColumns = leftToRight
                ? row.OrderBy(c => c.horizontalPos.x).ToList()
                : row.OrderByDescending(c => c.horizontalPos.x).ToList();

            sorted.AddRange(rowColumns);
            leftToRight = !leftToRight; // Alternate direction
        }

        return sorted;
    }

    /// <summary>
    /// Generates final block order from sorted columns
    /// </summary>
    private List<Vector3> GenerateBlockOrder(List<MiningColumn> columns)
    {
        var blockOrder = new List<Vector3>();

        foreach (var column in columns)
        {
            // Add all blocks from this column in sorted order
            blockOrder.AddRange(column.blocks);
        }

        return blockOrder;
    }

    /// <summary>
    /// Estimates total mining time
    /// </summary>
    private float EstimateMiningTime(ColumnMiningPlan plan)
    {
        // Base time per block
        float blockTime = 2.5f; // seconds

        // Movement time between columns
        float columnSwitchTime = 3.0f; // seconds

        // Vertical movement within column is faster (already positioned)
        float verticalTime = 1.0f; // seconds per level

        float total = 0f;

        foreach (var column in plan.columns)
        {
            // First block in column requires positioning
            total += blockTime + columnSwitchTime;

            // Subsequent blocks in same column are faster
            total += (column.height - 1) * verticalTime;
        }

        return total;
    }

    /// <summary>
    /// Gets the best adjacent position for mining a column
    /// </summary>
    public Vector3 GetOptimalMiningPosition(MiningColumn column, Vector3 turtlePos)
    {
        // For vertical columns, turtle should be adjacent to the bottom-most block
        // This allows mining upward or downward efficiently

        Vector3 baseBlock = columnDirection == ColumnDirection.TopDown
            ? column.topBlock
            : column.bottomBlock;

        // Possible adjacent positions (cardinal directions)
        Vector3[] adjacentOffsets = new Vector3[]
        {
            Vector3.right,   // +X
            Vector3.left,    // -X
            Vector3.forward, // +Z
            Vector3.back     // -Z
        };

        // Find closest adjacent position to current turtle position
        Vector3 bestPos = baseBlock + adjacentOffsets[0];
        float bestDist = Vector3.Distance(bestPos, turtlePos);

        for (int i = 1; i < adjacentOffsets.Length; i++)
        {
            Vector3 candidatePos = baseBlock + adjacentOffsets[i];
            float dist = Vector3.Distance(candidatePos, turtlePos);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestPos = candidatePos;
            }
        }

        return bestPos;
    }

    /// <summary>
    /// Determines if turtle needs to reposition between blocks
    /// </summary>
    public bool NeedsRepositioning(Vector3 currentBlock, Vector3 nextBlock, Vector3 turtlePos)
    {
        if (!optimizePathfinding) return true;

        // Check if blocks are in same column
        bool sameColumn =
            Mathf.RoundToInt(currentBlock.x) == Mathf.RoundToInt(nextBlock.x) &&
            Mathf.RoundToInt(currentBlock.z) == Mathf.RoundToInt(nextBlock.z);

        if (!sameColumn) return true; // Different column = reposition needed

        // In same column - check if turtle is still adjacent
        float distance = Vector3.Distance(turtlePos, nextBlock);
        return distance > 2f; // If too far, reposition
    }

    void OnDrawGizmos()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        // Visualization would go here (draw columns, mining order, etc.)
    }
}
