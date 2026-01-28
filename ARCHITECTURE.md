# MC-TurtleManager Architecture Documentation

## Manager System Overview

This document outlines the manager architecture for the MC-TurtleManager Unity project. It clarifies responsibilities and prevents redundancy.

---

## Core Managers (Essential - Do Not Remove)

### 1. TurtleWorldManager
**Location:** `Assets/Scripts/TurtleManager/TurtleWorldManager.cs`
**Responsibility:** Manages the Minecraft world representation
**Key Functions:**
- Chunk management and loading
- Block type definitions and materials
- World-to-chunk coordinate conversion
- Block queries and world state

**Dependencies:**
- ChunkManager (per-chunk data)
- ChunkMeshBuilder (mesh generation)

---

### 2. MultiTurtleManager
**Location:** `Assets/Scripts/TurtleManager/MultiTurtleManager.cs`
**Responsibility:** Manages multiple turtle instances
**Key Functions:**
- Turtle registration and lifecycle
- Turtle list management
- Events for turtle add/remove/update
- Multi-turtle coordination

**Dependencies:**
- TurtleObject (individual turtle data)

**Used By:**
- ModernUIManager (for turtle list display)
- TurtleSelectionManager (for selection)

---

### 3. ModernUIManager
**Location:** `Assets/Scripts/UI/ModernUIManager.cs`
**Responsibility:** Central UI coordination and control
**Key Functions:**
- Panel visibility management
- Hotkey handling
- UI event routing
- Quick actions setup

**Dependencies:**
- MultiTurtleManager (turtle data)
- AreaSelectionManager (selection modes)
- TurtleSelectionManager (turtle selection)
- StructureManager (building)
- BuildModeManager (structure preview)

**UI Components:**
- AnnoStyleContextMenu
- ModernTurtleListPanel
- TaskQueuePanel
- StructureSelectionPanel

---

### 4. IntegrationManager
**Location:** `Assets/Scripts/IntegrationManager.cs`
**Responsibility:** System initialization and component wiring
**Key Functions:**
- Auto-setup of all managers
- Reference passing between systems
- Validation and health checks
- System status reporting

**Initializes:**
- All turtle sub-managers
- Construction system components
- Modern UI system

---

### 5. StructureManager
**Location:** `Assets/Scripts/Structures/StructureManager.cs`
**Responsibility:** Structure data management
**Key Functions:**
- Structure loading and storage
- Structure definitions (houses, bridges, etc.)
- Structure queries

**Used By:**
- BuildModeManager (structure preview)
- StructureSelectionPanel (structure list)

---

## Specialized Managers (Keep - Specific Purposes)

### 6. AreaSelectionManager
**Location:** `Assets/Scripts/Selection/AreaSelectionManager.cs`
**Responsibility:** Area selection for mining and building operations
**Key Functions:**
- Box selection (start/end drag)
- Selection mode switching (Mining/Building/None)
- Block validation
- Mining order optimization
- Work area visualization

**Selection Modes:**
- `None` - No active selection
- `Mining` - Select blocks to mine
- `Building` - Select area to build in

**Works With:**
- BuildModeManager (for structure placement)
- AreaSelectionVisualizer (visual feedback)

---

### 7. BuildModeManager
**Location:** `Assets/Scripts/UI/BuildModeManager.cs`
**Responsibility:** Structure placement preview and validation
**Key Functions:**
- Real-time structure preview at mouse position
- Collision checking for valid placement
- Grid snapping
- Placement confirmation/cancellation

**Relationship with AreaSelectionManager:**
- **NOT redundant** - complementary functionality
- AreaSelectionManager: Area selection
- BuildModeManager: Structure preview while placing

**Used By:**
- StructureSelectionPanel (when structure selected)

---

### 8. TurtleSelectionManager
**Location:** `Assets/Scripts/TurtleManager/TurtleSelectionManager.cs`
**Responsibility:** Turtle selection and task assignment
**Key Functions:**
- Single/multi turtle selection
- Selection persistence
- Task assignment to selected turtles
- Selection events

**Used By:**
- ModernTurtleListPanel (display selection)
- TaskQueuePanel (assign tasks)

---

### 9. ChunkNavMeshManager
**Location:** `Assets/Scripts/Chunk/ChunkNavMeshManager.cs`
**Responsibility:** Navigation mesh generation for pathfinding
**Key Functions:**
- NavMesh generation from chunk data
- Obstacle detection
- Pathfinding support

**Used By:**
- BlockWorldPathfinder

---

### 10. StructureEditorManager
**Location:** `Assets/Scripts/Structures/StructureEditorManager.cs`
**Responsibility:** In-game structure editor
**Key Functions:**
- Structure creation and editing
- Block palette management
- Structure saving/loading

**Dependencies:**
- BlockPaletteUI
- StructureManager

---

## Turtle Sub-Managers (Keep - Modular Architecture)

These managers are part of the TurtleMainController modular system:

### 11. TurtleBaseManager
**Responsibility:** Basic turtle functions (status, fuel, inventory)

### 12. TurtleMovementManager
**Responsibility:** Movement commands and pathfinding

### 13. TurtleMiningManager
**Responsibility:** Mining operations and validation

### 14. TurtleBuildingManager
**Responsibility:** Building operations and placement

### 15. TurtleOperationManager
**Responsibility:** Operation queue and progress tracking

**Architecture:**
All sub-managers are coordinated by **TurtleMainController**, which provides:
- Unified API for turtle operations
- Capability checking
- Status reporting
- Error handling

---

## Optional/Debug Managers (Keep - Useful Functionality)

### 16. ServerUpdateManager
**Location:** `Assets/Scripts/ServerUpdateManager.cs`
**Responsibility:** Server block update monitoring with visual feedback
**Key Functions:**
- Polls server for block updates
- Visual gizmo display for changes
- Update history tracking

**Usage:** Limited - only `ClearAllGizmos()` called from IntegrationManager
**Status:** Keep for debugging/monitoring purposes
**Note:** Could be disabled in production builds

---

## Removed/Deprecated Components

### ❌ ConstructionUI (DELETED)
- Replaced by ModernUIManager
- Old files renamed to .old and deleted

### ❌ ConstructionUIBuilder (DELETED)
- Replaced by ModernUIBuilder
- Old files renamed to .old and deleted

### ❌ TurtleSelectionUI (DELETED)
- Replaced by ModernTurtleListPanel
- Provided duplicate functionality

---

## Manager Initialization Order

1. **Core System** (IntegrationManager.Awake)
   - TurtleWorldManager
   - RTSCameraController

2. **Turtle System** (IntegrationManager.Start)
   - TurtleMainController
   - Turtle Sub-Managers (Base, Movement, Mining, Building, Operation)
   - BlockWorldPathfinder

3. **Construction System**
   - AreaSelectionManager
   - StructureManager
   - ServerUpdateManager (optional)

4. **UI System**
   - ModernUIManager
   - ModernUIBuilder (builds UI)
   - MultiTurtleManager reference connection

5. **Integration** (IntegrationManager.SetupIntegrations)
   - Connect all cross-references
   - Initialize UI panels with complete references
   - Validate system health

---

## Manager Communication Patterns

### Event-Driven Communication
- **MultiTurtleManager**: Fires `OnTurtleAdded`, `OnTurtleRemoved`, `OnTurtlesUpdated`
- **TurtleSelectionManager**: Fires `OnSelectionChanged`
- **AreaSelectionManager**: Fires `OnAreaSelected`, `OnSelectionCleared`
- **TurtleMainController**: Fires `OnProgressUpdate`, `OnOperationCompleted`

### Direct Reference Communication
- **IntegrationManager** → All managers (initialization)
- **ModernUIManager** → Selection/Display managers (UI updates)
- **AreaSelectionManager** → TurtleMainController (operation execution)

### Singleton Pattern
- **ModernUIManager**: Static `Instance` property for global access
- **StructureManager**: Static `Instance` property

---

## Common Pitfalls to Avoid

### ❌ DON'T: Remove managers thinking they're duplicate
- BuildModeManager vs AreaSelectionManager - **Complementary, not duplicate!**
- ServerUpdateManager - **Debugging tool, keep it!**

### ❌ DON'T: Initialize UI before managers are ready
- Always initialize managers first
- Then pass references to UI components
- Finally call UI initialization methods

### ❌ DON'T: Create new managers without documentation
- Update this document when adding new managers
- Define clear responsibilities
- Check for overlaps with existing managers

### ✅ DO: Follow initialization order
- Core → Turtle → Construction → UI → Integration
- Respect dependency chains

### ✅ DO: Use events for loose coupling
- Prefer events over direct method calls
- Makes systems more modular and testable

### ✅ DO: Centralize references in IntegrationManager
- Single place for system wiring
- Easy to debug initialization issues

---

## Architecture Diagrams

### Manager Dependency Graph
```
IntegrationManager (Root)
├── TurtleWorldManager
│   └── ChunkManager
├── TurtleMainController
│   ├── TurtleBaseManager
│   ├── TurtleMovementManager
│   ├── TurtleMiningManager
│   ├── TurtleBuildingManager
│   ├── TurtleOperationManager
│   └── BlockWorldPathfinder
├── MultiTurtleManager
├── AreaSelectionManager
│   └── AreaSelectionVisualizer
├── TurtleSelectionManager
├── StructureManager
├── BuildModeManager
├── ServerUpdateManager
└── ModernUIManager
    ├── AnnoStyleContextMenu
    ├── ModernTurtleListPanel
    ├── TaskQueuePanel
    └── StructureSelectionPanel
```

### UI Interaction Flow
```
User Input (Hotkey/Click)
    ↓
ModernUIManager (Input handling)
    ↓
[Branch based on action]
    ├→ Mining Mode → AreaSelectionManager.ToggleMode(Mining)
    ├→ Building Mode → AreaSelectionManager.ToggleMode(Building)
    │                  + BuildModeManager.SetStructure()
    ├→ Turtle List → ModernTurtleListPanel.Show()
    │                 ↓
    │                 MultiTurtleManager.GetAllTurtles()
    └→ Task Queue → TaskQueuePanel.Show()
                     ↓
                     TurtleOperationManager.GetQueue()
```

### Operation Execution Flow
```
User selects area (AreaSelectionManager)
    ↓
Validates blocks (TurtleMainController.ValidateMiningBlocks)
    ↓
Optimizes order (TurtleMiningManager.OptimizeMiningOrder)
    ↓
Creates task (ModernUIManager.CreateMiningTask)
    ↓
Assigns to turtle (MultiTurtleManager or TurtleSelectionManager)
    ↓
Executes operation (TurtleMainController.StartOptimizedMining)
    ↓
Progress updates (via events)
    ↓
UI updates (ModernTurtleListPanel, TaskQueuePanel)
```

---

## Troubleshooting Guide

### Problem: UI buttons don't work
**Solution:** Check ModernUIManager.SetupQuickActionsButtons() is called after all references are set

### Problem: Turtle list is empty
**Solution:** Ensure MultiTurtleManager is connected to ModernUIManager in IntegrationManager.SetupIntegrations()

### Problem: Selection doesn't work
**Solution:** Check AreaSelectionManager has TurtleMainController reference and is in correct mode

### Problem: Building preview not showing
**Solution:** Verify BuildModeManager has StructureManager reference and structure is set

### Problem: "Manager not found" errors
**Solution:** Check IntegrationManager initialization order and autoSetupComponents is enabled

---

## Version History

- **v1.0** (2026-01-28): Initial architecture documentation
  - Documented all 16 managers
  - Defined clear responsibilities
  - Added initialization order
  - Created troubleshooting guide
  - Removed deprecated components (ConstructionUI, TurtleSelectionUI)

---

## Maintenance Guidelines

1. **When adding a new manager:**
   - Add to this document with clear responsibility
   - Update dependency graph
   - Add to IntegrationManager initialization
   - Consider if it duplicates existing functionality

2. **When removing a manager:**
   - Check all references in codebase
   - Update IntegrationManager
   - Update this documentation
   - Test all dependent systems

3. **When refactoring:**
   - Keep single responsibility principle
   - Maintain event-driven communication
   - Document breaking changes
   - Update architecture diagrams

---

*Last Updated: 2026-01-28*
*Project: MC-TurtleManager*
*Unity Version: 2022.3+ (HDRP)*
