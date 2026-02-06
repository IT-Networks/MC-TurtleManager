using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Updated integration manager that connects the new modular turtle construction system components
/// This should be attached to a main GameObject in your scene
/// </summary>
public class IntegrationManager : MonoBehaviour
{
    [Header("Core Components")]
    public TurtleWorldManager worldManager;
    public RTSCameraController cameraController;
    
    [Header("New Turtle System Architecture")]
    public TurtleMainController turtleMainController; // Primary controller
    public TurtleBaseManager turtleBaseManager;
    public TurtleMovementManager turtleMovementManager;
    public TurtleMiningManager turtleMiningManager;
    public TurtleBuildingManager turtleBuildingManager;
    public TurtleOperationManager turtleOperationManager;
    public BlockWorldPathfinder pathfinder;
    
    [Header("Construction System")]
    public AreaSelectionManager areaSelectionManager;
    public StructureManager structureManager;
    public ModernUIManager modernUIManager;
    public MultiTurtleManager multiTurtleManager;

    [Header("Auto-Setup")]
    public bool autoSetupComponents = true;
    public bool createUICanvas = true;
    public bool setupModularTurtleSystem = true;
    
    private void Awake()
    {
        if (autoSetupComponents)
        {
            AutoSetupComponents();
        }
    }
    
    private void Start()
    {
        ValidateSetup();
        SetupIntegrations();
    }
    
    private void AutoSetupComponents()
    {
        // Find or create core components
        FindOrCreateCoreComponents();
        
        // Setup new modular turtle system
        if (setupModularTurtleSystem)
        {
            SetupModularTurtleSystem();
        }
        
        // Setup construction system components
        SetupConstructionComponents();

        // Setup modern UI
        SetupModernUI();
    }

    private void FindOrCreateCoreComponents()
    {
        if (worldManager == null)
        {
            worldManager = FindFirstObjectByType<TurtleWorldManager>();
            if (worldManager == null)
            {
                Debug.LogWarning("TurtleWorldManager not found. Please assign manually.");
            }
        }
        
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<RTSCameraController>();
            if (cameraController == null)
            {
                Debug.LogWarning("RTSCameraController not found. Please assign manually.");
            }
        }
    }
    
    /// <summary>
    /// Sets up the new modular turtle system architecture
    /// </summary>
    private void SetupModularTurtleSystem()
    {
        // Create main turtle controller if not exists
        if (turtleMainController == null)
        {
            GameObject mainControllerObj = new GameObject("TurtleMainController");
            mainControllerObj.transform.SetParent(transform);
            turtleMainController = mainControllerObj.AddComponent<TurtleMainController>();
        }
        
        // Setup base manager
        if (turtleBaseManager == null)
        {
            GameObject baseManagerObj = new GameObject("TurtleBaseManager");
            baseManagerObj.transform.SetParent(turtleMainController.transform);
            turtleBaseManager = baseManagerObj.AddComponent<TurtleBaseManager>();
        }
        
        // Setup movement manager
        if (turtleMovementManager == null)
        {
            GameObject movementObj = new GameObject("TurtleMovementManager");
            movementObj.transform.SetParent(turtleMainController.transform);
            turtleMovementManager = movementObj.AddComponent<TurtleMovementManager>();
        }
        
        // Setup mining manager
        if (turtleMiningManager == null)
        {
            GameObject miningObj = new GameObject("TurtleMiningManager");
            miningObj.transform.SetParent(turtleMainController.transform);
            turtleMiningManager = miningObj.AddComponent<TurtleMiningManager>();
        }
        
        // Setup building manager
        if (turtleBuildingManager == null)
        {
            GameObject buildingObj = new GameObject("TurtleBuildingManager");
            buildingObj.transform.SetParent(turtleMainController.transform);
            turtleBuildingManager = buildingObj.AddComponent<TurtleBuildingManager>();
        }
        
        // Setup operation manager
        if (turtleOperationManager == null)
        {
            GameObject operationObj = new GameObject("TurtleOperationManager");
            operationObj.transform.SetParent(turtleMainController.transform);
            turtleOperationManager = operationObj.AddComponent<TurtleOperationManager>();
        }

        // Setup pathfinder
        if (pathfinder == null)
        {
            GameObject pathfinderObj = new GameObject("BlockWorldPathfinder");
            pathfinderObj.transform.SetParent(turtleMainController.transform);
            pathfinder = pathfinderObj.AddComponent<BlockWorldPathfinder>();
        }
        
        // Connect references in main controller
        ConnectTurtleSystemReferences();
    }
    
    private void ConnectTurtleSystemReferences()
    {
        if (turtleMainController != null)
        {
            turtleMainController.baseManager = turtleBaseManager;
            turtleMainController.movementManager = turtleMovementManager;
            turtleMainController.miningManager = turtleMiningManager;
            turtleMainController.buildingManager = turtleBuildingManager;
            turtleMainController.operationManager = turtleOperationManager;
            turtleMainController.worldManager = worldManager;

            // Set up cross-references
            if (pathfinder != null)
            {
                pathfinder.worldManager = worldManager;
            }

            Debug.Log("Modular turtle system references connected successfully");
        }
    }
    
    private void SetupConstructionComponents()
    {
        SetupAreaSelectionManager();
        SetupStructureManager();
    }
    
    private void SetupAreaSelectionManager()
    {
        if (areaSelectionManager == null)
        {
            GameObject areaSelectionObj = new GameObject("AreaSelectionManager");
            areaSelectionObj.transform.SetParent(transform);
            areaSelectionManager = areaSelectionObj.AddComponent<AreaSelectionManager>();
        }
        
        // Setup references for new architecture
        areaSelectionManager.cameraController = cameraController;
        areaSelectionManager.turtleMainController = turtleMainController; // Updated reference
        areaSelectionManager.pathfinder = pathfinder;
    }
    
    private void SetupStructureManager()
    {
        if (structureManager == null)
        {
            GameObject structureObj = new GameObject("StructureManager");
            structureObj.transform.SetParent(transform);
            structureManager = structureObj.AddComponent<StructureManager>();
        }
    }
    
    private void SetupModernUI()
    {
        if (modernUIManager == null && createUICanvas)
        {
            // Create ModernUIManager GameObject
            GameObject uiManagerObj = new GameObject("ModernUIManager");
            uiManagerObj.transform.SetParent(transform);
            modernUIManager = uiManagerObj.AddComponent<ModernUIManager>();

            // Create ModernUIBuilder GameObject
            GameObject uiBuilderObj = new GameObject("ModernUIBuilder");
            uiBuilderObj.transform.SetParent(uiManagerObj.transform);
            ModernUIBuilder uiBuilder = uiBuilderObj.AddComponent<ModernUIBuilder>();

            // Setup references
            uiBuilder.modernUIManager = modernUIManager;
            uiBuilder.buildUIOnStart = false; // We'll build it manually

            // Build the UI
            uiBuilder.BuildCompleteUI();

            Debug.Log("Modern UI system created successfully!");
        }
        else if (modernUIManager != null)
        {
            Debug.Log("ModernUIManager already assigned.");
        }
    }
    
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

            // Re-initialize UI panels with complete references
            if (modernUIManager.turtleList != null && multiTurtleManager != null)
            {
                modernUIManager.turtleList.Initialize(multiTurtleManager, selectionMgr, cameraController);
                Debug.Log("TurtleList initialized with MultiTurtleManager");
            }

            if (modernUIManager.taskQueue != null && multiTurtleManager != null)
            {
                modernUIManager.taskQueue.Initialize(multiTurtleManager, selectionMgr);
                Debug.Log("TaskQueue initialized with MultiTurtleManager");
            }

            if (modernUIManager.contextMenu != null)
            {
                modernUIManager.contextMenu.Initialize(modernUIManager);
                Debug.Log("ContextMenu initialized");
            }

            // CRITICAL: Setup button event handlers NOW that all references are set
            modernUIManager.SetupQuickActionsButtons();
            Debug.Log("Quick Actions buttons event handlers setup completed");
        }

        Debug.Log("Modern UI system integration with new turtle architecture completed!");
    }
    
    private void ValidateSetup()
    {
        bool hasErrors = false;
        
        if (worldManager == null)
        {
            Debug.LogError("TurtleWorldManager is required but not assigned!");
            hasErrors = true;
        }
        
        // Validate new turtle system
        if (turtleMainController == null)
        {
            Debug.LogError("TurtleMainController is required but not found!");
            hasErrors = true;
        }
        else
        {
            // Check main controller's component references
            var capabilities = turtleMainController.GetCapabilities();
            foreach (var capability in capabilities)
            {
                if (!capability.Value)
                {
                    Debug.LogWarning($"Turtle capability '{capability.Key}' is not available - missing component");
                }
            }
        }
        
        if (areaSelectionManager == null)
        {
            Debug.LogError("AreaSelectionManager is required but not found!");
            hasErrors = true;
        }
        
        if (structureManager == null)
        {
            Debug.LogWarning("StructureManager not found - building functionality will be limited");
        }

        if (modernUIManager == null)
        {
            Debug.LogWarning("ModernUIManager not found - no UI interface available");
        }
        
        if (hasErrors)
        {
            Debug.LogError("Construction system setup has errors! Please fix the missing components.");
        }
        else
        {
            Debug.Log("Construction system validation passed!");
            
            // Log system health report
            if (turtleMainController != null)
            {
                Debug.Log(turtleMainController.GetSystemHealthReport());
            }
        }
    }
    
    // Public API for external control - Updated for new architecture
    public void StartMiningMode()
    {
        if (areaSelectionManager != null)
        {
            areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.Mining);
        }
    }
    
    public void StartBuildingMode()
    {
        if (areaSelectionManager != null)
        {
            areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.Building);
        }
    }
    
    public void CancelAllOperations()
    {
        if (areaSelectionManager != null)
        {
            areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.None);
        }
        
        if (turtleMainController != null)
        {
            turtleMainController.CancelCurrentOperation();
        }
    }
    
    public void EmergencyStop()
    {
        if (turtleMainController != null)
        {
            turtleMainController.ExecuteEmergencyRecovery();
        }
        
        CancelAllOperations();
        Debug.Log("Emergency stop executed on all construction systems!");
    }
    
    // Status queries - Updated for new architecture
    public bool IsSystemBusy()
    {
        return (turtleMainController != null && turtleMainController.IsBusy()) ||
               (areaSelectionManager != null && areaSelectionManager.IsSelecting);
    }
    
    public string GetSystemStatus()
    {
        if (turtleMainController != null && turtleMainController.IsBusy())
        {
            return turtleMainController.GetStatusString();
        }
        
        if (areaSelectionManager != null && areaSelectionManager.IsSelecting)
        {
            return $"Selection Mode: {areaSelectionManager.CurrentMode} ({areaSelectionManager.SelectedBlocks.Count} blocks selected)";
        }
        
        return "System Ready";
    }
    
    /// <summary>
    /// Get comprehensive system status including new turtle architecture
    /// </summary>
    public string GetComprehensiveSystemStatus()
    {
        var status = new System.Text.StringBuilder();
        status.AppendLine("=== CONSTRUCTION SYSTEM STATUS ===");
        
        // Core system status
        status.AppendLine($"System Busy: {IsSystemBusy()}");
        status.AppendLine($"Current Status: {GetSystemStatus()}");
        
        // Turtle system status
        if (turtleMainController != null)
        {
            status.AppendLine("\n--- TURTLE SYSTEM ---");
            var turtleStatus = turtleMainController.GetComprehensiveStatus();
            status.AppendLine($"Ready: {turtleStatus.isReady}");
            status.AppendLine($"Position: {turtleStatus.position}");
            status.AppendLine($"Operation: {turtleStatus.currentOperation}");
            status.AppendLine($"Progress: {turtleStatus.progress:P}");
            
            if (turtleStatus.estimatedTimeRemaining > 0)
            {
                status.AppendLine($"Time Remaining: {turtleStatus.estimatedTimeRemaining:F1}s");
            }
            
            // Capabilities
            status.AppendLine("\n--- CAPABILITIES ---");
            foreach (var capability in turtleStatus.capabilities)
            {
                status.AppendLine($"{capability.Key}: {(capability.Value ? "Available" : "Missing")}");
            }
        }
        
        // Selection system status
        if (areaSelectionManager != null)
        {
            status.AppendLine("\n--- SELECTION SYSTEM ---");
            status.AppendLine($"Mode: {areaSelectionManager.CurrentMode}");
            status.AppendLine($"Selected Blocks: {areaSelectionManager.SelectedBlocks.Count}");
            status.AppendLine($"Valid Blocks: {areaSelectionManager.ValidBlocks.Count}");
            
            if (areaSelectionManager.CurrentStats != null)
            {
                status.AppendLine($"Optimization Savings: {areaSelectionManager.CurrentStats:F1}%");
            }
        }
        
        return status.ToString();
    }
    
    /// <summary>
    /// Execute mining operation with comprehensive validation and reporting
    /// </summary>
    public void ExecuteOptimizedMining(List<Vector3> blocks)
    {
        if (turtleMainController == null)
        {
            Debug.LogError("Cannot execute mining - TurtleMainController not available");
            return;
        }
        
        if (!turtleMainController.CheckSystemReadiness())
        {
            Debug.LogError("Cannot execute mining - system not ready");
            return;
        }
        
        // Get mining plan
        var plan = turtleMainController.PrepareMiningOperation(blocks);
        
        if (!plan.isValid)
        {
            Debug.LogError($"Mining plan validation failed: {plan.errorMessage}");
            return;
        }
        
        Debug.Log($"Executing mining plan: {plan}");
        turtleMainController.StartOptimizedMining(blocks);
    }
    
    /// <summary>
    /// Execute building operation with comprehensive validation
    /// </summary>
    public void ExecuteValidatedBuilding(Vector3 buildOrigin, StructureData structure)
    {
        if (turtleMainController == null)
        {
            Debug.LogError("Cannot execute building - TurtleMainController not available");
            return;
        }
        
        turtleMainController.StartValidatedBuilding(buildOrigin, structure);
    }
    
    /// <summary>
    /// Force system health check and report any issues
    /// </summary>
    public void PerformSystemHealthCheck()
    {
        Debug.Log("=== SYSTEM HEALTH CHECK ===");
        
        if (turtleMainController != null)
        {
            Debug.Log(turtleMainController.GetSystemHealthReport());
        }
        else
        {
            Debug.LogError("TurtleMainController is missing!");
        }
        
        Debug.Log(GetComprehensiveSystemStatus());
        
        // Check for common issues
        if (worldManager == null)
        {
            Debug.LogError("TurtleWorldManager is missing - world data unavailable");
        }
        
        if (areaSelectionManager == null)
        {
            Debug.LogError("AreaSelectionManager is missing - selection functionality unavailable");
        }

        if (modernUIManager == null)
        {
            Debug.LogWarning("ModernUIManager is missing - no user interface available");
        }
    }
    
    /// <summary>
    /// Get recommended actions based on current system state
    /// </summary>
    public List<string> GetRecommendedActions()
    {
        var recommendations = new List<string>();
        
        if (turtleMainController == null)
        {
            recommendations.Add("Set up TurtleMainController to enable turtle operations");
            return recommendations;
        }
        
        if (!turtleMainController.IsReady())
        {
            recommendations.Add("Wait for turtle system to initialize");
        }
        
        if (areaSelectionManager != null && areaSelectionManager.SelectedBlocks.Count > 0)
        {
            if (areaSelectionManager.CurrentMode == AreaSelectionManager.SelectionMode.Mining)
            {
                var plan = turtleMainController.PrepareMiningOperation(areaSelectionManager.SelectedBlocks);
                if (plan.isValid)
                {
                    recommendations.Add($"Execute mining operation ({plan.validBlockCount} valid blocks)");
                }
                else
                {
                    recommendations.Add($"Fix mining issues: {plan.errorMessage}");
                }
            }
            else if (areaSelectionManager.CurrentMode == AreaSelectionManager.SelectionMode.Building)
            {
                recommendations.Add("Select a structure to build");
            }
        }
        else
        {
            recommendations.Add("Select blocks for mining or building operations");
        }
        
        if (recommendations.Count == 0)
        {
            recommendations.Add("System ready - select operation mode (M for Mining, B for Building)");
        }
        
        return recommendations;
    }
}

/// <summary>
/// Extension methods for easier integration manager usage
/// </summary>
public static class IntegrationManagerExtensions
{
    /// <summary>
    /// Quick setup for the entire construction system
    /// </summary>
    public static void QuickSetup(this IntegrationManager manager)
    {
        manager.PerformSystemHealthCheck();
        
        var recommendations = manager.GetRecommendedActions();
        Debug.Log("=== RECOMMENDED ACTIONS ===");
        foreach (var recommendation in recommendations)
        {
            Debug.Log($"- {recommendation}");
        }
    }
    
    /// <summary>
    /// Execute the most appropriate action based on current state
    /// </summary>
    public static void ExecuteRecommendedAction(this IntegrationManager manager)
    {
        var recommendations = manager.GetRecommendedActions();
        
        if (recommendations.Count > 0)
        {
            var action = recommendations[0];
            Debug.Log($"Executing recommended action: {action}");
            
            if (action.Contains("mining operation"))
            {
                manager.areaSelectionManager?.ExecuteSelectedOperation();
            }
            else if (action.Contains("Select operation mode"))
            {
                manager.StartMiningMode();
            }
        }
    }
}