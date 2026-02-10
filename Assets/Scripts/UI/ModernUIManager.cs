using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Modern UI Manager - Central hub for the new intuitive UI system
/// Coordinates all UI panels and provides a clean, modern interface
/// </summary>
public class ModernUIManager : MonoBehaviour
{
    [Header("Main UI Panels")]
    public GameObject mainUICanvas;
    public GameObject contextMenuPanel;
    public GameObject turtleListPanel;
    public GameObject taskQueuePanel;
    public GameObject quickActionsPanel;
    public GameObject structureSelectionPanel;
    public GameObject aiPromptPanel;

    [Header("References")]
    public MultiTurtleManager turtleManager;
    public TurtleSelectionManager selectionManager;
    public AreaSelectionManager areaSelectionManager;
    public RTSCameraController cameraController;
    public StructureManager structureManager;
    public BuildModeManager buildModeManager;

    [Header("UI Components")]
    public AnnoStyleContextMenu contextMenu;
    public ModernTurtleListPanel turtleList;
    public TaskQueuePanel taskQueue;
    public StructureSelectionPanel structureSelection;
    public AIPromptPanel aiPrompt;

    [Header("Settings")]
    public KeyCode contextMenuKey = KeyCode.Mouse1; // Right-click
    public bool enableHotkeys = true;

    [Header("Status Display")]
    public TextMeshProUGUI selectionStatusText;

    // Hotkey Reference:
    // M - Toggle Mining Mode
    // B - Toggle Building Mode / Structure Selection
    // T - Toggle Turtle List Panel
    // Q - Toggle Task Queue Panel
    // I - Toggle AI Prompt Panel (Structure Generation)
    // ESC - Close all panels / Cancel selection
    // Right-Click - Context Menu

    private static ModernUIManager _instance;
    public static ModernUIManager Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        InitializeUI();
        SetupReferences();
        RegisterHotkeys();
    }

    private void InitializeUI()
    {
        // Find or create UI components
        if (contextMenu == null)
            contextMenu = GetComponentInChildren<AnnoStyleContextMenu>();

        if (turtleList == null)
            turtleList = GetComponentInChildren<ModernTurtleListPanel>();

        if (taskQueue == null)
            taskQueue = GetComponentInChildren<TaskQueuePanel>();

        if (structureSelection == null)
            structureSelection = GetComponentInChildren<StructureSelectionPanel>();

        if (aiPrompt == null)
            aiPrompt = GetComponentInChildren<AIPromptPanel>();

        // Initially hide panels
        if (contextMenuPanel != null)
            contextMenuPanel.SetActive(false);

        if (structureSelectionPanel != null)
            structureSelectionPanel.SetActive(false);

        if (aiPromptPanel != null)
            aiPromptPanel.SetActive(false);
    }

    private void SetupReferences()
    {
        // Auto-find references if not set
        if (turtleManager == null)
            turtleManager = FindFirstObjectByType<MultiTurtleManager>();

        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<TurtleSelectionManager>();

        if (areaSelectionManager == null)
            areaSelectionManager = FindFirstObjectByType<AreaSelectionManager>();

        if (cameraController == null)
            cameraController = FindFirstObjectByType<RTSCameraController>();

        // Pass references to sub-components
        if (contextMenu != null)
        {
            contextMenu.Initialize(this);
        }

        if (turtleList != null)
        {
            turtleList.Initialize(turtleManager, selectionManager, cameraController);
        }

        if (taskQueue != null)
        {
            taskQueue.Initialize(turtleManager, selectionManager);
        }

        if (structureSelection != null)
        {
            if (structureManager == null)
                structureManager = StructureManager.Instance;

            if (buildModeManager == null)
                buildModeManager = FindFirstObjectByType<BuildModeManager>();
        }

        // Setup Quick Actions buttons if panel exists
        SetupQuickActionsButtons();
    }

    public void SetupQuickActionsButtons()
    {
        if (quickActionsPanel == null) return;

        // Find selection status text
        if (selectionStatusText == null)
        {
            selectionStatusText = quickActionsPanel.GetComponentInChildren<TextMeshProUGUI>();
        }

        // Find all buttons in QuickActionsPanel
        UnityEngine.UI.Button[] buttons = quickActionsPanel.GetComponentsInChildren<UnityEngine.UI.Button>(true);

        foreach (var btn in buttons)
        {
            // Clear existing listeners to avoid duplicates
            btn.onClick.RemoveAllListeners();

            // Setup based on button name
            switch (btn.name)
            {
                case "MiningMode":
                    btn.onClick.AddListener(() => {
                        if (areaSelectionManager != null)
                        {
                            areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.Mining);
                            UpdateSelectionStatus();
                        }
                    });
                    break;

                case "BuildingMode":
                    btn.onClick.AddListener(() => {
                        ToggleStructureSelection();
                        if (areaSelectionManager != null)
                        {
                            areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.Building);
                            UpdateSelectionStatus();
                        }
                    });
                    break;

                case "TurtleList":
                    btn.onClick.AddListener(() => ToggleTurtleList());
                    break;

                case "TaskQueue":
                    btn.onClick.AddListener(() => ToggleTaskQueue());
                    break;

                case "ExecuteSelection":
                    btn.onClick.AddListener(() => {
                        if (areaSelectionManager != null)
                        {
                            areaSelectionManager.ExecuteSelectedOperation();
                            UpdateSelectionStatus();
                        }
                    });
                    break;

                case "CancelSelection":
                    btn.onClick.AddListener(() => {
                        if (areaSelectionManager != null)
                        {
                            areaSelectionManager.CancelSelection();
                            UpdateSelectionStatus();
                        }
                    });
                    break;

                case "StructureEditor":
                    btn.onClick.AddListener(() => {
                        Debug.Log("Structure Editor - Not yet implemented");
                    });
                    break;

                case "Settings":
                    btn.onClick.AddListener(() => {
                        Debug.Log("Settings - Not yet implemented");
                    });
                    break;
            }
        }

        Debug.Log($"Setup {buttons.Length} Quick Actions buttons");
    }

    private void RegisterHotkeys()
    {
        if (!enableHotkeys) return;

        // Hotkeys will be handled in Update
    }

    private void Update()
    {
        HandleHotkeys();
        UpdateUIState();
    }

    private void HandleHotkeys()
    {
        if (!enableHotkeys) return;

        // Context menu toggle (right-click)
        if (Input.GetKeyDown(contextMenuKey))
        {
            ToggleContextMenu();
        }

        // Quick actions
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleTurtleList();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            ToggleTaskQueue();
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            // Toggle mining mode in area selection manager
            if (areaSelectionManager != null)
            {
                areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.Mining);
            }
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleStructureSelection();
            // Also toggle building mode in area selection manager
            if (areaSelectionManager != null)
            {
                areaSelectionManager.ToggleMode(AreaSelectionManager.SelectionMode.Building);
            }
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleAIPrompt();
        }

        // ESC to close all panels
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseAllPanels();
            // Also cancel selection
            if (areaSelectionManager != null)
            {
                areaSelectionManager.CancelSelection();
            }
        }
    }

    private void UpdateUIState()
    {
        // Update selection status display
        UpdateSelectionStatus();
    }

    private void UpdateSelectionStatus()
    {
        if (selectionStatusText == null || areaSelectionManager == null) return;

        var mode = areaSelectionManager.CurrentMode;
        var selectedCount = areaSelectionManager.SelectedBlocks.Count;
        var validCount = areaSelectionManager.ValidBlocks.Count;

        if (mode == AreaSelectionManager.SelectionMode.None)
        {
            selectionStatusText.text = "No selection - Press M for Mining, B for Building";
            selectionStatusText.color = new Color(0.6f, 0.6f, 0.6f);
        }
        else if (selectedCount == 0)
        {
            selectionStatusText.text = $"{mode} Mode - Click and drag to select area";
            selectionStatusText.color = new Color(0.8f, 0.8f, 0.2f);
        }
        else
        {
            selectionStatusText.text = $"{selectedCount} blocks selected ({validCount} valid) - Press ENTER or click Execute";
            selectionStatusText.color = new Color(0.2f, 1.0f, 0.2f);
        }
    }

    // Public API for UI control
    public void ToggleContextMenu()
    {
        if (contextMenuPanel != null)
        {
            bool isActive = !contextMenuPanel.activeSelf;
            contextMenuPanel.SetActive(isActive);

            if (isActive && contextMenu != null)
            {
                // Position at mouse
                Vector2 mousePos = Input.mousePosition;
                RectTransform rect = contextMenuPanel.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.position = mousePos;
                }
            }
        }
    }

    public void ShowContextMenu(Vector3 worldPosition)
    {
        if (contextMenu != null)
        {
            contextMenu.Show(worldPosition);
        }
    }

    public void HideContextMenu()
    {
        if (contextMenuPanel != null)
            contextMenuPanel.SetActive(false);
    }

    public void ToggleTurtleList()
    {
        if (turtleListPanel != null)
            turtleListPanel.SetActive(!turtleListPanel.activeSelf);
    }

    public void ToggleTaskQueue()
    {
        if (taskQueuePanel != null)
            taskQueuePanel.SetActive(!taskQueuePanel.activeSelf);
    }

    public void ToggleStructureSelection()
    {
        if (structureSelectionPanel != null)
        {
            bool isActive = !structureSelectionPanel.activeSelf;
            structureSelectionPanel.SetActive(isActive);

            if (isActive && structureSelection != null)
            {
                structureSelection.Show();
            }
        }
    }

    public void ShowStructureSelection()
    {
        if (structureSelection != null)
            structureSelection.Show();
    }

    public void HideStructureSelection()
    {
        if (structureSelection != null)
            structureSelection.Hide();
    }

    public void ToggleAIPrompt()
    {
        if (aiPromptPanel != null)
        {
            bool isActive = !aiPromptPanel.activeSelf;
            aiPromptPanel.SetActive(isActive);

            if (isActive && aiPrompt != null)
            {
                aiPrompt.ShowPanel();
            }
        }
    }

    public void ShowAIPrompt()
    {
        if (aiPrompt != null)
            aiPrompt.ShowPanel();
    }

    public void HideAIPrompt()
    {
        if (aiPrompt != null)
            aiPrompt.HidePanel();
    }

    public void CloseAllPanels()
    {
        if (contextMenuPanel != null) contextMenuPanel.SetActive(false);
        if (structureSelectionPanel != null) structureSelectionPanel.SetActive(false);
        if (aiPromptPanel != null) aiPromptPanel.SetActive(false);
    }

    // Task creation
    public void CreateMiningTask(List<Vector3> blocks)
    {
        if (taskQueue != null)
        {
            taskQueue.AddMiningTask(blocks);
        }
    }

    public void CreateBuildingTask(Vector3 position, StructureData structure)
    {
        if (taskQueue != null)
        {
            taskQueue.AddBuildingTask(position, structure);
        }
    }

    // UI Notifications
    public void ShowNotification(string message, float duration = 3f)
    {
        Debug.Log($"[UI Notification] {message}");
        // TODO: Implement toast notification system
    }

    public void ShowError(string error)
    {
        Debug.LogError($"[UI Error] {error}");
        // TODO: Implement error dialog
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
