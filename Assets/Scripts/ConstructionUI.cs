using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updated UI Manager for the new modular turtle construction system
/// </summary>
public class ConstructionUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas mainCanvas;
    public GameObject constructionPanel;
    public GameObject structureSelectionPanel;
    public GameObject operationStatusPanel;
    
    [Header("Area Selection UI")]
    public Button miningModeButton;
    public Button buildingModeButton;
    public Button executeOperationButton;
    public Button cancelSelectionButton;
    public Text selectionStatusText;
    public Text selectedBlocksText;
    
    [Header("Structure Selection UI")]
    public Dropdown structureDropdown;
    public Button previewStructureButton;
    public Button clearPreviewButton;
    public Button confirmBuildButton;
    public Text structureInfoText;
    public RawImage structurePreviewImage;
    
    [Header("Operation Status UI")]
    public Text operationStatusText;
    public Text turtleStatusText;
    public Slider operationProgressSlider;
    public Button emergencyStopButton;
    public Button cancelOperationButton;
    public Text operationDetailsText;
    
    [Header("References - New Architecture")]
    public AreaSelectionManager areaManager;
    public StructureManager structureManager;
    public TurtleMainController turtleMainController; // Updated reference
    
    // State
    private Dictionary<string, StructureData> availableStructures;
    private string selectedStructureName;
    private bool showingPreview = false;
    
    private void Start()
    {
        SetupUI();
        SetupEventListeners();
        UpdateUI();
    }
    
    private void SetupUI()
    {
        // Initially show only construction panel
        if (constructionPanel != null) constructionPanel.SetActive(true);
        if (structureSelectionPanel != null) structureSelectionPanel.SetActive(false);
        if (operationStatusPanel != null) operationStatusPanel.SetActive(false);
        
        // Setup button states
        if (executeOperationButton != null) executeOperationButton.interactable = false;
        if (confirmBuildButton != null) confirmBuildButton.interactable = false;
        if (previewStructureButton != null) previewStructureButton.interactable = false;
    }
    
    private void SetupEventListeners()
    {
        // Area selection buttons
        if (miningModeButton != null)
            miningModeButton.onClick.AddListener(() => ToggleSelectionMode(AreaSelectionManager.SelectionMode.Mining));
        
        if (buildingModeButton != null)
            buildingModeButton.onClick.AddListener(() => ToggleSelectionMode(AreaSelectionManager.SelectionMode.Building));
        
        if (executeOperationButton != null)
            executeOperationButton.onClick.AddListener(ExecuteSelectedOperation);
        
        if (cancelSelectionButton != null)
            cancelSelectionButton.onClick.AddListener(CancelSelection);
        
        // Structure selection buttons
        if (structureDropdown != null)
            structureDropdown.onValueChanged.AddListener(OnStructureSelected);
        
        if (previewStructureButton != null)
            previewStructureButton.onClick.AddListener(PreviewSelectedStructure);
        
        if (clearPreviewButton != null)
            clearPreviewButton.onClick.AddListener(ClearStructurePreview);
        
        if (confirmBuildButton != null)
            confirmBuildButton.onClick.AddListener(ConfirmBuilding);
        
        // Operation control buttons
        if (emergencyStopButton != null)
            emergencyStopButton.onClick.AddListener(EmergencyStop);
        
        if (cancelOperationButton != null)
            cancelOperationButton.onClick.AddListener(CancelCurrentOperation);
        
        // Event subscriptions - Updated for new architecture
        if (areaManager != null)
        {
            areaManager.OnAreaSelected += OnAreaSelected;
            areaManager.OnSelectionCleared += OnSelectionCleared;
            areaManager.OnSelectionStatsUpdated += OnSelectionStatsUpdated;
        }
        
        if (structureManager != null)
        {
            // structureManager.OnStructuresLoaded += OnStructuresLoaded; // Removed - new architecture
        }
        
        if (turtleMainController != null)
        {
            turtleMainController.OnOperationStarted += OnOperationStarted;
            turtleMainController.OnOperationCompleted += OnOperationCompleted;
            turtleMainController.OnProgressUpdate += OnProgressUpdate;
        }
    }
    
    private void Update()
    {
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        UpdateSelectionUI();
        UpdateStructureUI();
        UpdateOperationStatusUI();
    }
    
    private void UpdateSelectionUI()
    {
        if (areaManager == null) return;
        
        // Update mode button states
        if (miningModeButton != null)
        {
            ColorBlock colors = miningModeButton.colors;
            colors.normalColor = areaManager.CurrentMode == AreaSelectionManager.SelectionMode.Mining ? 
                Color.red : Color.white;
            miningModeButton.colors = colors;
        }
        
        if (buildingModeButton != null)
        {
            ColorBlock colors = buildingModeButton.colors;
            colors.normalColor = areaManager.CurrentMode == AreaSelectionManager.SelectionMode.Building ? 
                Color.green : Color.white;
            buildingModeButton.colors = colors;
        }
        
        // Update status texts
        if (selectionStatusText != null)
        {
            string status = areaManager.CurrentMode switch
            {
                AreaSelectionManager.SelectionMode.Mining => "MINING MODE - Click and drag to select blocks to mine",
                AreaSelectionManager.SelectionMode.Building => "BUILDING MODE - Click to select build location",
                _ => "Select a mode (M for Mining, B for Building)"
            };
            selectionStatusText.text = status;
        }
        
        if (selectedBlocksText != null)
        {
            int blockCount = areaManager.SelectedBlocks.Count;
            int validCount = areaManager.ValidBlocks.Count;
            selectedBlocksText.text = $"Selected: {blockCount} blocks ({validCount} valid)";
        }
        
        // Update execute button
        if (executeOperationButton != null)
        {
            bool canExecute = areaManager.SelectedBlocks.Count > 0 && 
                             (areaManager.CurrentMode == AreaSelectionManager.SelectionMode.Mining ||
                              (areaManager.CurrentMode == AreaSelectionManager.SelectionMode.Building && !string.IsNullOrEmpty(selectedStructureName)));
            executeOperationButton.interactable = canExecute && !IsTurtleBusy();
        }
    }
    
    private void UpdateStructureUI()
    {
        if (structureManager == null) return;
        
        // Show/hide structure panel based on mode
        bool showStructurePanel = areaManager != null && areaManager.CurrentMode == AreaSelectionManager.SelectionMode.Building;
        if (structureSelectionPanel != null)
            structureSelectionPanel.SetActive(showStructurePanel);
        
        // Update structure info
        if (structureInfoText != null && !string.IsNullOrEmpty(selectedStructureName))
        {
            var structure = structureManager.GetStructure(selectedStructureName);
            if (structure != null)
            {
                string info = $"Structure: {structure.name}\n";
                info += $"Description: {structure.description}\n";
                info += $"Blocks: {structure.blocks.Count}\n";
                info += $"Size: {structure.GetSize()}";
                structureInfoText.text = info;
            }
        }
        
        // Update button states
        if (previewStructureButton != null)
            previewStructureButton.interactable = !string.IsNullOrEmpty(selectedStructureName) && areaManager?.SelectedBlocks.Count > 0;
        
        if (confirmBuildButton != null)
            confirmBuildButton.interactable = !string.IsNullOrEmpty(selectedStructureName) && 
                                            areaManager?.SelectedBlocks.Count > 0 && 
                                            !IsTurtleBusy();
    }
    
    private void UpdateOperationStatusUI()
    {
        bool showStatus = turtleMainController != null && turtleMainController.IsBusy();
        if (operationStatusPanel != null)
            operationStatusPanel.SetActive(showStatus);
        
        if (!showStatus) return;
        
        // Update operation status
        if (operationStatusText != null)
        {
            string status = turtleMainController.GetCurrentOperation() switch
            {
                TurtleOperationManager.OperationType.Mining => "Mining in progress...",
                TurtleOperationManager.OperationType.Building => "Building in progress...",
                TurtleOperationManager.OperationType.Moving => "Turtle moving...",
                _ => "Operation in progress..."
            };
            operationStatusText.text = status;
        }
        
        // Update turtle status
        if (turtleStatusText != null)
        {
            var status = turtleMainController.GetTurtleStatus();
            if (status != null)
            {
                string info = $"Turtle: {status.label}\n";
                info += $"Position: {status.position.x}, {status.position.y}, {status.position.z}\n";
                info += $"Facing: {status.direction}\n";
                info += $"System: {turtleMainController.GetStatusString()}";
                turtleStatusText.text = info;
            }
        }
        
        // Update progress
        if (operationProgressSlider != null)
        {
            float progress = turtleMainController.GetOperationProgress();
            operationProgressSlider.value = progress;
        }
        
        // Update detailed operation info
        if (operationDetailsText != null)
        {
            operationDetailsText.text = turtleMainController.GetOperationSummary();
        }
    }
    
    // Event handlers
    private void ToggleSelectionMode(AreaSelectionManager.SelectionMode mode)
    {
        if (areaManager != null)
        {
            areaManager.ToggleMode(mode);
        }
    }
    
    private void ExecuteSelectedOperation()
    {
        if (areaManager == null) return;
        
        switch (areaManager.CurrentMode)
        {
            case AreaSelectionManager.SelectionMode.Mining:
                ExecuteOptimizedMining();
                break;
            case AreaSelectionManager.SelectionMode.Building:
                ConfirmBuilding();
                break;
        }
    }
    
    private void ExecuteOptimizedMining()
    {
        if (turtleMainController == null)
        {
            Debug.LogError("TurtleMainController not found!");
            return;
        }

        //areaManager.ExecuteTopDownMining();
        // Use the new extension method for optimized mining
        turtleMainController.StartOptimizedMining(areaManager.SelectedBlocks);
    }
    
    private void CancelSelection()
    {
        if (areaManager != null)
        {
            areaManager.ToggleMode(AreaSelectionManager.SelectionMode.None);
        }
        ClearStructurePreview();
    }
    
    private void OnAreaSelected(List<Vector3> blocks, AreaSelectionManager.SelectionMode mode)
    {
        Debug.Log($"Area selected: {blocks.Count} blocks in {mode} mode");
        
        // Show mining report for mining operations
        if (mode == AreaSelectionManager.SelectionMode.Mining && turtleMainController != null)
        {
            string report = turtleMainController.GetMiningReport(blocks);
            Debug.Log(report);
        }
    }
    
    private void OnSelectionCleared()
    {
        Debug.Log("Selection cleared");
        ClearStructurePreview();
    }
    
    private void OnSelectionStatsUpdated(AreaSelectionManager.SelectionStats stats)
    {
        // Update UI with selection statistics
        if (selectedBlocksText != null)
        {
            selectedBlocksText.text = $"Selected: {stats.totalSelected} ({stats.validBlocks} valid, {stats.invalidBlocks} invalid)";
        }
    }
    
    private void OnStructuresLoaded(Dictionary<string, StructureData> structures)
    {
        availableStructures = structures;
        PopulateStructureDropdown();
    }
    
    private void PopulateStructureDropdown()
    {
        if (structureDropdown == null || availableStructures == null) return;
        
        structureDropdown.ClearOptions();
        
        List<string> options = new List<string> { "Select Structure..." };
        options.AddRange(availableStructures.Keys);
        
        structureDropdown.AddOptions(options);
    }
    
    private void OnStructureSelected(int index)
    {
        if (structureDropdown == null || index <= 0) 
        {
            selectedStructureName = null;
            return;
        }
        
        var options = structureDropdown.options;
        if (index < options.Count)
        {
            selectedStructureName = options[index].text;
            Debug.Log($"Selected structure: {selectedStructureName}");
        }
    }
    
    private void PreviewSelectedStructure()
    {
        if (string.IsNullOrEmpty(selectedStructureName) || areaManager?.SelectedBlocks.Count == 0) return;
        
        Vector3 buildPos = areaManager.SelectedBlocks[0];
        // structureManager.ShowStructurePreview(selectedStructureName, buildPos); // Removed - new architecture
        showingPreview = true;
        
        if (clearPreviewButton != null)
            clearPreviewButton.gameObject.SetActive(true);
    }
    
    private void ClearStructurePreview()
    {
        if (structureManager != null)
        {
            // structureManager.ClearPreview(); // Removed - new architecture
        }
        showingPreview = false;
        
        if (clearPreviewButton != null)
            clearPreviewButton.gameObject.SetActive(false);
    }
    
    private void ConfirmBuilding()
    {
        if (string.IsNullOrEmpty(selectedStructureName) || areaManager?.SelectedBlocks.Count == 0) return;
        
        var structure = structureManager?.GetStructure(selectedStructureName);
        if (structure != null && areaManager != null && turtleMainController != null)
        {
            Vector3 buildOrigin = areaManager.SelectedBlocks[0];
            
            // Use the new validated building method
            turtleMainController.StartValidatedBuilding(buildOrigin, structure);
        }
    }
    
    private void OnOperationStarted(TurtleOperationManager.OperationType operation)
    {
        Debug.Log($"Operation started: {operation}");
    }
    
    private void OnOperationCompleted(TurtleOperationManager.OperationType operation, OperationStats stats)
    {
        Debug.Log($"Operation completed: {operation} - Stats: {stats}");
    }
    
    private void OnProgressUpdate(OperationStats stats)
    {
        // Update progress display
        if (operationProgressSlider != null)
        {
            operationProgressSlider.value = stats.Progress;
        }
    }
    
    private void EmergencyStop()
    {
        if (turtleMainController != null)
        {
            turtleMainController.ExecuteEmergencyRecovery();
        }
        CancelSelection();
    }
    
    private void CancelCurrentOperation()
    {
        if (turtleMainController != null)
        {
            turtleMainController.CancelCurrentOperation();
        }
    }
    
    private bool IsTurtleBusy()
    {
        return turtleMainController != null && turtleMainController.IsBusy();
    }
    
    // Keyboard shortcuts
    private void OnGUI()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            switch (e.keyCode)
            {
                case KeyCode.M:
                    ToggleSelectionMode(AreaSelectionManager.SelectionMode.Mining);
                    break;
                case KeyCode.B:
                    ToggleSelectionMode(AreaSelectionManager.SelectionMode.Building);
                    break;
                case KeyCode.Return:
                    if (executeOperationButton != null && executeOperationButton.interactable)
                        ExecuteSelectedOperation();
                    break;
                case KeyCode.Escape:
                    CancelSelection();
                    break;
                case KeyCode.R:
                    // Show system health report
                    if (turtleMainController != null)
                    {
                        Debug.Log(turtleMainController.GetSystemHealthReport());
                    }
                    break;
            }
        }
    }
    
    private void OnDestroy()
    {
        // Cleanup event subscriptions
        if (areaManager != null)
        {
            areaManager.OnAreaSelected -= OnAreaSelected;
            areaManager.OnSelectionCleared -= OnSelectionCleared;
            areaManager.OnSelectionStatsUpdated -= OnSelectionStatsUpdated;
        }
        
        if (structureManager != null)
        {
            // structureManager.OnStructuresLoaded -= OnStructuresLoaded; // Removed - new architecture
        }
        
        if (turtleMainController != null)
        {
            turtleMainController.OnOperationStarted -= OnOperationStarted;
            turtleMainController.OnOperationCompleted -= OnOperationCompleted;
            turtleMainController.OnProgressUpdate -= OnProgressUpdate;
        }
    }
}