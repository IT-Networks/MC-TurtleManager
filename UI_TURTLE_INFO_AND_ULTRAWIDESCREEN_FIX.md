# UI Turtle Information & Ultra Widescreen Fix

**Date**: 2026-02-16
**Issues**:
1. Turtle information not visible in task view or turtle view
2. UI not visible on ultra widescreen displays

---

## Root Cause Analysis

### Issue 1: Canvas Scaler Configuration

**File**: `Assets/OutdoorsScene.unity`
**Canvas Component**: mainUICanvas (fileID: 978573711)

**Current Configuration**:
```yaml
m_UiScaleMode: 0  # Constant Pixel Size mode
m_ReferenceResolution: {x: 800, y: 600}  # Very small resolution!
```

**Problem**:
- Canvas uses "Constant Pixel Size" mode with 800x600 reference resolution
- On ultra widescreen (e.g., 3440x1440), UI elements appear tiny or off-screen
- No dynamic scaling for different aspect ratios
- UI does not adapt to screen size changes

**Expected for Ultra Widescreen**:
```
Should use: Scale With Screen Size mode
Reference Resolution: {x: 1920, y: 1080} (Full HD)
Screen Match Mode: Match Width or Height (depending on orientation)
```

---

### Issue 2: Turtle Information Not Displaying

**File**: `Assets/Scripts/UI/ModernTurtleListPanel.cs`
**Method**: `TurtleListItem.UpdateDisplay()` (lines 438-478)

**Current Code** (lines 454-466):
```csharp
if (statusText != null)
{
    if (turtle.isBusy)
    {
        statusText.text = $"Status: {turtle.currentOperation}";
        statusText.color = new Color(1f, 0.7f, 0.2f);
    }
    else
    {
        statusText.text = "Status: Idle";
        statusText.color = new Color(0.2f, 1f, 0.3f);
    }
}
```

**Potential Issues**:

1. **Operation Not Updated**:
   - `turtle.currentOperation` defaults to `TurtleOperationManager.OperationType.None`
   - No code in `TurtleObject` or managers updates this field automatically
   - Operation managers (Mining, Building) may not be calling `SetBusy()` properly

2. **Panel Initialization Timing**:
   - `ModernTurtleListPanel.Initialize()` is called from `IntegrationManager.SetupIntegrations()` (line 263-266)
   - But `MultiTurtleManager.OnTurtlesUpdated` event may fire before initialization
   - Turtles might not be in the list when events fire

3. **UI Element Creation**:
   - `TurtleListItem.SetupUI()` (lines 350-372) creates UI elements programmatically
   - Uses `GetComponent<RectTransform>()` after adding components
   - May fail if RectTransform not properly initialized

4. **Canvas Scaler Affecting Visibility**:
   - With 800x600 reference on 3440x1440 screen, elements are ~23% of intended size
   - Panel may be positioned outside visible area
   - Text and buttons may be too small to see

---

## Solutions

### Fix 1: Canvas Scaler Configuration

**Action**: Modify Canvas to use "Scale With Screen Size" mode

**Steps in Unity Editor**:
1. Open `Assets/OutdoorsScene.unity`
2. Select `mainUICanvas` GameObject
3. Find `Canvas Scaler` component
4. Change settings:
   - **UI Scale Mode**: `Scale With Screen Size`
   - **Reference Resolution**: `X: 1920`, `Y: 1080`
   - **Screen Match Mode**: `Match Width Or Height`
   - **Match**: `0.5` (balance between width/height)

**Alternative (Programmatic Fix in ModernUIManager)**:
```csharp
private void Awake()
{
    // Fix canvas scaling programmatically
    if (mainUICanvas != null)
    {
        CanvasScaler scaler = mainUICanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Debug.Log("Fixed Canvas Scaler for proper screen scaling");
        }
    }
}
```

---

### Fix 2: Ensure Turtle Operations Are Updated

**Files to Modify**:
- `Assets/Scripts/TurtleManager/TurtleBaseManager.cs`
- `Assets/Scripts/TurtleManager/TurtleMiningManager.cs`
- `Assets/Scripts/TurtleManager/TurtleBuildingManager.cs`

**Action**: Ensure `turtleObject.SetBusy()` is called with proper operation type

**Example - TurtleMiningManager.cs** (line 78):
```csharp
public void StartMiningOperation(List<Vector3> blockPositions)
{
    // ... existing code ...

    operationManager.StartOperation(TurtleOperationManager.OperationType.Mining, blockPositions.Count);

    // CRITICAL: Update turtle object with current operation
    if (baseManager.turtleObject != null)
    {
        baseManager.turtleObject.SetBusy(true, TurtleOperationManager.OperationType.Mining);
    }

    StartCoroutine(ExecuteMiningOperation(optimizedBlocks));
}
```

**Example - TurtleBuildingManager.cs**:
```csharp
public void StartBuildingOperation(Vector3 buildOrigin, StructureData structure)
{
    // ... existing code ...

    operationManager.StartOperation(TurtleOperationManager.OperationType.Building, structure.blocks.Count);

    // CRITICAL: Update turtle object with current operation
    if (baseManager.turtleObject != null)
    {
        baseManager.turtleObject.SetBusy(true, TurtleOperationManager.OperationType.Building);
    }

    StartCoroutine(ExecuteBuildingOperation(...));
}
```

**Example - TurtleOperationManager.cs**:
```csharp
public void StartOperation(OperationType type, int totalItems)
{
    currentOperation = type;
    totalItems = totalItems;
    processedItems = 0;
    failedItems = 0;

    // CRITICAL: Notify turtle object of operation
    TurtleBaseManager baseManager = FindFirstObjectByType<TurtleBaseManager>();
    if (baseManager != null && baseManager.turtleObject != null)
    {
        baseManager.turtleObject.SetBusy(true, type);
    }

    Debug.Log($"Started operation: {type} with {totalItems} items");
}
```

---

### Fix 3: Ensure Panel Initialization Order

**File**: `Assets/Scripts/IntegrationManager.cs`
**Method**: `SetupIntegrations()` (lines 233-287)

**Current Issue**:
- Panel initialization happens in `Start()` (line 65)
- But turtles may be spawned before panel is ready
- Event handlers may miss initial turtle updates

**Solution**:
```csharp
private void SetupIntegrations()
{
    // Find MultiTurtleManager if not assigned
    if (multiTurtleManager == null)
    {
        multiTurtleManager = FindFirstObjectByType<MultiTurtleManager>();
    }

    // Connect area selection to new turtle main controller
    if (areaSelectionManager != null && turtleMainController != null)
    {
        areaSelectionManager.turtleMainController = turtleMainController;
    }

    // Connect Modern UI Manager to all managers
    if (modernUIManager != null)
    {
        modernUIManager.areaSelectionManager = areaSelectionManager;
        modernUIManager.structureManager = structureManager;
        modernUIManager.cameraController = cameraController;
        modernUIManager.turtleManager = multiTurtleManager;

        // Find TurtleSelectionManager
        TurtleSelectionManager selectionMgr = FindFirstObjectByType<TurtleSelectionManager>();
        if (selectionMgr != null)
        {
            modernUIManager.selectionManager = selectionMgr;
        }

        // CRITICAL FIX: Initialize panels AFTER all references are set
        // This ensures turtles spawned before panels are handled correctly

        if (modernUIManager.turtleList != null && multiTurtleManager != null)
        {
            modernUIManager.turtleList.Initialize(multiTurtleManager, selectionMgr, cameraController);

            // CRITICAL: Force refresh after a short delay to catch any already-spawned turtles
            StartCoroutine(RefreshTurtleListDelayed());
        }

        if (modernUIManager.taskQueue != null && multiTurtleManager != null)
        {
            modernUIManager.taskQueue.Initialize(multiTurtleManager, selectionMgr);
        }

        if (modernUIManager.contextMenu != null)
        {
            modernUIManager.contextMenu.Initialize(modernUIManager);
        }

        // Setup quick actions buttons
        modernUIManager.SetupQuickActionsButtons();
    }

    Debug.Log("Modern UI system integration completed!");
}

private IEnumerator RefreshTurtleListDelayed()
{
    // Wait for all turtles to spawn (from MultiTurtleManager's UpdateTurtlesLoop)
    yield return new WaitForSeconds(2f);

    // Force refresh turtle list
    if (modernUIManager != null && modernUIManager.turtleList != null)
    {
        // Manually trigger update
        modernUIManager.turtleList.SendMessage("UpdateTurtleList");
        Debug.Log("Delayed turtle list refresh completed");
    }
}
```

---

### Fix 4: Improve UI Element Positioning

**File**: `Assets/Scripts/UI/ModernTurtleListPanel.cs`
**Method**: `TurtleListItem.SetupUI()` (lines 350-372)

**Issue**: UI elements positioned using hardcoded offsets that may not scale

**Solution**:
```csharp
private void SetupUI()
{
    // Find or create UI elements
    nameText = GetOrCreateText("NameText", new Vector2(10, -10), new Vector2(0, 25)); // Use relative width
    nameText.fontSize = 16;
    nameText.fontStyle = FontStyles.Bold;

    positionText = GetOrCreateText("PositionText", new Vector2(10, -35), new Vector2(0, 20));
    positionText.fontSize = 12;
    positionText.color = new Color(0.8f, 0.8f, 0.8f);

    statusText = GetOrCreateText("StatusText", new Vector2(10, -50), new Vector2(0, 20));
    statusText.fontSize = 12;

    // Create buttons
    jumpButton = CreateButton("JumpButton", "Jump To", new Vector2(-10, -15), new Vector2(80, 30));
    jumpButton.onClick.AddListener(OnJumpToTurtle);

    selectButton = CreateButton("SelectButton", "Select", new Vector2(-10, -50), new Vector2(80, 25));
    selectButton.onClick.AddListener(OnSelectTurtle);

    background = GetComponent<Image>();
}
```

And update `GetOrCreateText` and `CreateButton` to use proper anchoring:

```csharp
private TextMeshProUGUI GetOrCreateText(string name, Vector2 position, Vector2 size)
{
    Transform existing = transform.Find(name);
    GameObject textObj;

    if (existing != null)
    {
        textObj = existing.gameObject;
    }
    else
    {
        textObj = new GameObject(name);
        textObj.transform.SetParent(transform, false);
    }

    TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
    if (text == null)
        text = textObj.AddComponent<TextMeshProUGUI>();

    RectTransform rect = textObj.GetComponent<RectTransform>();

    // CRITICAL FIX: Use Stretch anchors for responsive layout
    rect.anchorMin = new Vector2(0, 1);
    rect.anchorMax = new Vector2(0, 1);
    rect.pivot = new Vector2(0, 1);
    rect.anchoredPosition = position;

    // If size.x is 0, stretch horizontally
    if (size.x == 0)
    {
        rect.offsetMin = new Vector2(position.x, size.y);
        rect.offsetMax = new Vector2(-10, -position.y); // Leave right padding
    }
    else
    {
        rect.sizeDelta = size;
    }

    return text;
}
```

---

### Fix 5: Add Canvas Safe Area Support

**File**: `Assets/Scripts/UI/ModernUIManager.cs`
**Method**: `InitializeUI()` (lines 72-99)

**Add**:
```csharp
private void InitializeUI()
{
    // ... existing code ...

    // CRITICAL FIX: Add Safe Area support for notched devices and ultra wide screens
    SetupSafeArea();

    // Initially hide panels
    if (contextMenuPanel != null)
        contextMenuPanel.SetActive(false);

    if (structureSelectionPanel != null)
        structureSelectionPanel.SetActive(false);

    if (aiPromptPanel != null)
        aiPromptPanel.SetActive(false);
}

private void SetupSafeArea()
{
    // Find all RectTransforms in the UI and apply safe area
    RectTransform[] allRects = mainUICanvas.GetComponentsInChildren<RectTransform>(true);

    Rect safeArea = Screen.safeArea;
    Vector2 anchorMin = safeArea.position;
    Vector2 anchorMax = safeArea.position + safeArea.size;

    anchorMin.x /= Screen.width;
    anchorMin.y /= Screen.height;
    anchorMax.x /= Screen.width;
    anchorMax.y /= Screen.height;

    foreach (RectTransform rect in allRects)
    {
        // Skip if this is the canvas itself
        if (rect.parent == null) continue;

        // Apply safe area anchors
        rect.anchorMin = new Vector2(
            Mathf.Clamp(rect.anchorMin.x, anchorMin.x, anchorMax.x),
            Mathf.Clamp(rect.anchorMin.y, anchorMin.y, anchorMax.y)
        );
        rect.anchorMax = new Vector2(
            Mathf.Clamp(rect.anchorMax.x, anchorMin.x, anchorMax.x),
            Mathf.Clamp(rect.anchorMax.y, anchorMin.y, anchorMax.y)
        );

        // Reset anchored position (we want safe area to handle positioning)
        rect.anchoredPosition = Vector2.zero;
    }

    Debug.Log($"Safe area setup: {safeArea} (anchors: min={anchorMin}, max={anchorMax})");
}
```

---

## Verification Steps

### Test 1: Canvas Scaling
1. Open project in Unity
2. Play game on ultra widescreen monitor (3440x1440 or similar)
3. Verify UI is visible and properly scaled
4. Check text is readable, buttons are clickable

### Test 2: Turtle Information Display
1. Start game with turtles spawned
2. Open Turtle List panel (press T)
3. Verify each turtle shows:
   - Turtle name and ID
   - Position (X, Y, Z)
   - Status (Idle, Mining, Building, etc.)
   - Selection indicator
4. Click on a turtle to select it
5. Verify status updates when turtle performs operations

### Test 3: Task View
1. Create mining task by selecting blocks
2. Open Task Queue panel (press Q)
3. Verify task shows:
   - Task type (Mining)
   - Block count
   - Status (Pending/Active/Completed)
   - Assigned turtle count

---

## Summary

**Issues Identified**:
1. **Canvas Scaler**: Using Constant Pixel Size (800x600) - fails on ultra widescreen
2. **Operation Not Updated**: Turtle managers not calling `SetBusy()` with operation type
3. **Initialization Order**: Panels may miss turtles spawned before panel is ready
4. **UI Positioning**: Hardcoded offsets don't scale with screen size

**Fixes Required**:
1. Change Canvas Scaler to Scale With Screen Size mode (1920x1080 reference)
2. Add `SetBusy()` calls in all manager scripts with proper operation types
3. Add delayed refresh for turtle list after initialization
4. Use responsive anchoring for UI elements (Stretch anchors)
5. Add Safe Area support for notched/ultra wide screens

**Files to Modify**:
- `Assets/OutdoorsScene.unity` (Canvas Scaler component)
- `Assets/Scripts/TurtleManager/TurtleBaseManager.cs` (add SetBusy calls)
- `Assets/Scripts/TurtleManager/TurtleMiningManager.cs` (add SetBusy call)
- `Assets/Scripts/TurtleManager/TurtleBuildingManager.cs` (add SetBusy call)
- `Assets/Scripts/TurtleManager/TurtleOperationManager.cs` (add SetBusy call)
- `Assets/Scripts/IntegrationManager.cs` (add delayed refresh)
- `Assets/Scripts/UI/ModernUIManager.cs` (add Canvas fix, Safe Area support)
- `Assets/Scripts/UI/ModernTurtleListPanel.cs` (improve anchoring)

**Expected Results**:
- UI visible and properly scaled on all screen sizes including ultra widescreen
- Turtle information displays correctly in both Turtle List and Task Queue panels
- Status updates in real-time as turtles perform operations
- Text readable, buttons clickable on all displays
