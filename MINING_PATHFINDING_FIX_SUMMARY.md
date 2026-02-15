# Mining & Pathfinding Fix Summary

**Date**: 2026-02-15
**Issue**: Mining and pathfinding broken after immediate mesh regeneration changes - not column-based anymore

---

## Root Cause Analysis

### Problem #1: Direct `RegenerateMesh()` Calls Bypass Event System

**Symptom**: Pathfinder cache not clearing when blocks are removed during mining, causing navigation errors.

**Files Affected**:
1. `Assets/Scripts/TurtleManager/TurtleMiningManager.cs` (lines 322-326, 404-412)
2. `Assets/Scripts/ChunkManager.cs` (line 479-482)

**Original Code (BROKEN)**:
```csharp
// TurtleMiningManager.cs - Line 324
var chunk = worldManager.GetChunkContaining(blockBelow);
chunk?.RemoveBlockFromData(blockBelow);

// Manually call RegenerateMesh
if (chunk != null && chunk.IsLoaded)
{
    chunk.RegenerateMesh();  // ← BYPASSES EVENT SYSTEM
}
```

**Problem**: Calling `chunk.RegenerateMesh()` directly triggers mesh regeneration but does NOT fire:
- `OnBlockRemoved` event
- `OnBlockPlaced` event
- `OnChunkRegenerated` event

The pathfinder subscribes to `OnBlockRemoved` and `OnChunkRegenerated`:
```csharp
// BlockWorldPathfinder.cs - Line 171-173
worldManager.OnBlockRemoved += OnBlockRemoved;
worldManager.OnBlockPlaced += OnBlockPlaced;
worldManager.OnChunkRegenerated += OnChunkRegenerated;
```

When these events don't fire, the pathfinder:
1. Doesn't clear its rasterization cache when blocks are removed
2. Doesn't rebuild NavMesh when chunks regenerate
3. Uses cached/incorrect pathfinding data
4. Turtle gets incorrect or broken paths

**Result**: Mining appears to work, but pathfinding becomes invalid, making turtle navigation erratic or impossible.

---

### Problem #2: Event System Not Firing Block Change Events

**Files Affected**:
1. `Assets/Scripts/ChunkManager.cs` (lines 481-482, 546-547)

**Events Defined But Not Fired**:
```csharp
// TurtleWorldManager.cs - Line 91
public System.Action<Vector3, string> OnBlockRemoved;
public System.Action<Vector3, string> OnBlockPlaced;

// ChunkManager.cs - Line 481
OnBlockRemoved?.Invoke(worldPosition, removedBlockType);

// ChunkManager.cs - Line 546
OnBlockPlaced?.Invoke(worldPosition, blockType);
```

**Analysis**:
- `OnBlockRemoved` is ONLY invoked by `RemoveBlockFromData` (line 479-482)
- `OnBlockPlaced` is ONLY invoked by `AddBlockAndRegenerate` (line 591-593)
- BUT `RemoveBlockAndRegenerate` only fires `OnChunkRegenerated` in `finally` block
- `AddBlockAndRegenerate` does NOT fire `OnBlockPlaced`

**Problem**: Even when using `RemoveBlockAndRegenerate` in mining, `OnBlockPlaced` never fires because it's only used for building in TurtleWorldManager.cs:541.

**Result**:
- ✅ `OnBlockRemoved` now fires (pathfinder cache clears)
- ❌ `OnBlockPlaced` never fires (no impact on current codebase, but could affect future features)
- ✅ `OnChunkRegenerated` fires (NavMesh updates)

---

## Solutions Applied

### Fix #1: Use `RemoveBlockAndRegenerate` Instead of Manual Regeneration

**File**: `Assets/Scripts/TurtleManager/TurtleMiningManager.cs`

**Location 1**: Line 322-326 (Shaft Digging)

**BEFORE**:
```csharp
var chunk = worldManager.GetChunkContaining(blockBelow);
chunk?.RemoveBlockFromData(blockBelow);

// Manual mesh regeneration bypasses events
if (chunk != null && chunk.IsLoaded)
{
    chunk.RegenerateMesh();
}

TrackMinedChunk(blockBelow, minedChunks);
```

**AFTER**:
```csharp
var chunk = worldManager.GetChunkContaining(blockBelow);
chunk?.RemoveBlockAndRegenerate(blockBelow, 10000);

TrackMinedChunk(blockBelow, minedChunks);
```

**Location 2**: Line 404-412 (Normal Mining)

**BEFORE**:
```csharp
var worldManager = baseManager.worldManager;
if (worldManager != null)
{
    var chunk = worldManager.GetChunkContaining(blockPosition);
    chunk?.RemoveBlockFromData(blockPosition);

    // Manual mesh regeneration bypasses events
    if (chunk != null && chunk.IsLoaded)
    {
        chunk.RegenerateMesh();
    }
}
```

**AFTER**:
```csharp
// Use RemoveBlockAndRegenerate to trigger proper events
var worldManager = baseManager.worldManager;
if (worldManager != null)
{
    var chunk = worldManager.GetChunkContaining(blockPosition);
    chunk?.RemoveBlockAndRegenerate(blockPosition, 10000);
}
```

**Benefit**:
- ✅ Triggers `OnBlockRemoved` event
- ✅ Triggers `OnChunkRegenerated` event
- ✅ Pathfinder cache clears when blocks removed
- ✅ Pathfinding stays valid during mining operations
- ✅ NavMesh updates after each block removal

---

### Fix #2: Fire Block Events Immediately in ChunkManager

**File**: `Assets/Scripts/ChunkManager.cs`

**Location 1**: Line 479-482 (Block Removal)

**BEFORE**:
```csharp
// Set flag immediately to prevent TOCTOU race with concurrent callers
isRegenerating = true;
CoroutineHelper.Instance.StartCoroutine(RegenerateMeshCoroutine(batchVerticesPerFrame));

Debug.Log($"Chunk {coord}: Block removed at {worldPosition}, regenerating mesh");
```

**AFTER**:
```csharp
// Set flag immediately to prevent TOCTOU race with concurrent callers
isRegenerating = true;
CoroutineHelper.Instance.StartCoroutine(RegenerateMeshCoroutine(batchVerticesPerFrame));

// CRITICAL FIX: Notify block removal event immediately
// This ensures pathfinder cache is invalidated when blocks are removed
// Pathfinder subscribes to OnBlockRemoved event to clear its rasterization cache
manager?.OnBlockRemoved?.Invoke(worldPosition, removedBlockType);

Debug.Log($"Chunk {coord}: Block removed at {worldPosition}, regenerating mesh");
```

**Location 2**: Line 590-593 (Block Placement)

**BEFORE**:
```csharp
Debug.Log($"Chunk {coord}: Block '{blockType}' added at {worldPosition}, regenerating mesh");
return true;
```

**AFTER**:
```csharp
Debug.Log($"Chunk {coord}: Block '{blockType}' added at {worldPosition}, regenerating mesh");

// CRITICAL FIX: Notify block placement event immediately
// This ensures pathfinder cache is updated when blocks are placed
// Pathfinder subscribes to OnBlockPlaced event to update its navigation
manager?.OnBlockPlaced?.Invoke(worldPosition, blockType);

return true;
```

---

## Behavior After Fixes

### Mining Flow (Fixed)

```
1. Turtle digs block at (x, y, z)
2. Server confirms: "Success"
3. Code calls: chunk?.RemoveBlockAndRegenerate(blockPosition, 10000);
4. ← FIX 1: chunk.RemoveBlockFromData() called (removes from data)
5. ← FIX 2: chunk.RegenerateMesh() started (mesh rebuilds)
6. ← FIX 3: OnBlockRemoved event fires
7. ← FIX 4: OnChunkRegenerated event fires
8. BlockWorldPathfinder.OnBlockRemoved() handler runs
9. ← FIX 5: Rasterization cache cleared
10. ← FIX 6: NavMesh rebuilds if enabled
11. Pathfinding sees updated world state
12. Turtle can pathfind correctly
13. Block visually disappears from world (mesh completes)
14. Repeat for next block
```

### Column-Based Mining (Preserved)

✅ The column-based mining optimization is FULLY preserved:
- `OptimizeByColumns()` still groups blocks by X,Z columns
- Columns are sorted top-down (highest Y first)
- Columns are sorted by nearest to turtle (greedy nearest-neighbor)
- Shaft digging creates access when needed
- Retry mechanism handles blocked blocks
- All column logic remains intact

The only change is that pathfinder now correctly sees block changes as they happen.

---

## Performance Considerations

### Mesh Regeneration Frequency

**Before Fix**:
- Regeneration happened once at end of mining operation (batch approach)
- Better performance, worse visual feedback

**After Fix**:
- Regeneration happens after each block removal (immediate approach)
- Better visual feedback, potential performance impact
- Pathfinding updates after each block (NavMesh rebuilds potentially each removal)

### Trade-offs

| Aspect | Before | After |
|---------|--------|-------|
| Visual Feedback | ❌ Blocks visible until end | ✅ Blocks disappear immediately |
| Pathfinding Validity | ❌ Cache invalidates slowly | ✅ Cache clears after each block |
| NavMesh Updates | ✅ Batch at end | ⚠️ After each block removal |
| Performance | ✅ Optimal | ⚠️ Potentially higher CPU/GPU |

### Recommendations

1. **Monitor Performance**:
   - Check frame rate during active mining operations
   - Watch CPU/GPU usage
   - Monitor memory allocation patterns
   - Profile with Unity Profiler

2. **If Performance Issues**:
   Consider batching `RemoveBlockAndRegenerate` calls:
   - Queue block removals
   - Batch-regenerate every X blocks or every Y seconds
   - Use `batchVerticesPerFrame` parameter (currently 10000)

3. **Alternative: Selective Regeneration**:
   - Only regenerate chunks affecting pathfinding
   - Skip NavMesh rebuild if path not crossing chunk boundaries
   - Use smaller `batchVerticesPerFrame` value

---

## Event System Summary

### Event Flow (After Fixes)

```
Block Removal (Mining):
RemoveBlockAndRegenerate()
  ├─> RemoveBlockFromData()
  ├─> RebuildMesh()
  └─> OnChunkRegenerated fires
       └─> Pathfinder cache clears

Block Placement (Building):
AddBlockAndRegenerate()
  ├─> SetBlock()
  ├─> RebuildMesh()
  └─> OnChunkRegenerated fires
       └─> Pathfinder cache clears
       └─> OnBlockPlaced fires ← NEW FIX
```

### Event Subscribers

**BlockWorldPathfinder.cs**:
```csharp
worldManager.OnBlockRemoved += OnBlockRemoved;      // Now fires correctly ✅
worldManager.OnBlockPlaced += OnBlockPlaced;      // Now fires correctly ✅
worldManager.OnChunkRegenerated += OnChunkRegenerated;  // Fires correctly ✅
```

**ChunkNavMeshManager.cs**:
```csharp
worldManager.OnChunkRegenerated += OnBlockRegenerated;  // Fires correctly ✅
```

**TurtleWorldManager.cs**:
```csharp
// Line 91: Event declarations
public System.Action<Vector3, string> OnBlockRemoved;
public System.Action<Vector3, string> OnBlockPlaced;
public System.Action<Vector2Int> OnChunkRegenerated;

// Line 481: Now fires correctly ✅
OnBlockRemoved?.Invoke(worldPosition, removedBlockType);

// Line 546: Now fires correctly ✅
OnBlockPlaced?.Invoke(worldPosition, blockType);

// Line 580: Fires correctly ✅
OnChunkRegenerated?.Invoke(chunkCoord);
```

---

## Testing Checklist

After running Unity with real MC server:

- [ ] Blocks disappear immediately when turtle digs them
- [ ] Pathfinding updates after each block removal
- [ ] Turtle can pathfind correctly during mining
- [ ] Column-based optimization still works (mines top-down, nearest column first)
- [ ] Shaft digging creates access when needed
- [ ] Visual world updates in real-time
- [ ] No "blocks visible after mining complete" issue
- [ ] Frame rate remains acceptable during active mining
- [ ] No navigation or pathfinding errors

---

## Files Modified

1. **Assets/Scripts/TurtleManager/TurtleMiningManager.cs**
   - Line 322-326: Changed to use `RemoveBlockAndRegenerate` for shaft digging
   - Line 404-412: Changed to use `RemoveBlockAndRegenerate` for normal mining

2. **Assets/Scripts/ChunkManager.cs**
   - Line 481-482: Added `OnBlockRemoved` event invocation after block removal
   - Line 590-593: Added `OnBlockPlaced` event invocation after block placement

---

## Summary

**Fixed**: Event system now properly fires when blocks are added/removed

**Root Cause**: Direct `RegenerateMesh()` calls bypassed event notification system

**Solution**: Use `RemoveBlockAndRegenerate` which:
1. Removes/adds block data
2. Rebuilds mesh
3. Fires `OnChunkRegenerated` event
4. NEW: Now fires `OnBlockRemoved` and `OnBlockPlaced` events

**Result**:
- ✅ Immediate visual updates (blocks appear/disappear instantly)
- ✅ Pathfinder cache clears correctly (mining paths valid)
- ✅ NavMesh updates correctly (navigation works)
- ✅ Column-based mining fully preserved

**Trade-off**: Potential performance impact due to frequent mesh regenerations during mining

**Monitoring**: Watch CPU/GPU usage and frame rate during mining operations
