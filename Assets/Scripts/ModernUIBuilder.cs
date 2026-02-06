using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Modern UI Builder - Creates all UI panels for ModernUIManager
/// Replaces the old ConstructionUIBuilder with a cleaner, ModernUIManager-focused approach
/// </summary>
public class ModernUIBuilder : MonoBehaviour
{
    [Header("UI Settings")]
    public bool buildUIOnStart = true;
    public Font defaultFont;
    public TMP_FontAsset tmpFont;

    [Header("Theme")]
    public Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    public Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color buttonHighlightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color textColor = Color.white;
    public int defaultFontSize = 14;

    [Header("References")]
    public ModernUIManager modernUIManager;

    private Canvas mainCanvas;
    private GameObject eventSystem;

    private void Start()
    {
        if (buildUIOnStart)
        {
            BuildCompleteUI();
        }
    }

    [ContextMenu("Build Complete UI")]
    public void BuildCompleteUI()
    {
        // Create Event System if it doesn't exist
        CreateEventSystem();

        // Create main canvas
        CreateMainCanvas();

        // Build all Modern UI panels
        BuildTurtleListPanel();
        BuildTaskQueuePanel();
        BuildStructureSelectionPanel();
        BuildContextMenuPanel();
        BuildQuickActionsPanel();

        // Setup ModernUIManager references
        SetupModernUIManagerReferences();

        Debug.Log("Complete Modern UI built successfully!");
    }

    private void CreateEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            Debug.Log("Created EventSystem for UI input");
        }
    }

    private void CreateMainCanvas()
    {
        GameObject canvasObj = new GameObject("ModernUICanvas");
        canvasObj.transform.SetParent(transform);

        mainCanvas = canvasObj.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        if (modernUIManager != null)
            modernUIManager.mainUICanvas = canvasObj;
    }

    #region Turtle List Panel

    private void BuildTurtleListPanel()
    {
        GameObject panel = CreatePanel("TurtleListPanel", new Vector2(-450, -300), new Vector2(380, 500));

        if (modernUIManager != null)
            modernUIManager.turtleListPanel = panel;

        // Add ModernTurtleListPanel component
        ModernTurtleListPanel listPanel = panel.AddComponent<ModernTurtleListPanel>();

        // Header
        listPanel.headerText = CreateTMPText(panel, "HeaderText", "Available Turtles (T)",
            new Vector2(0, 220), new Vector2(350, 30), 18, TextAlignmentOptions.Center);

        // Counters
        GameObject countersContainer = new GameObject("Counters");
        countersContainer.transform.SetParent(panel.transform, false);
        RectTransform countersRect = countersContainer.AddComponent<RectTransform>();
        countersRect.anchoredPosition = new Vector2(0, 185);
        countersRect.sizeDelta = new Vector2(350, 25);

        listPanel.totalCountText = CreateTMPText(countersContainer, "TotalCount", "Total: 0",
            new Vector2(-115, 0), new Vector2(100, 25), 12, TextAlignmentOptions.Left);
        listPanel.activeCountText = CreateTMPText(countersContainer, "ActiveCount", "Active: 0",
            new Vector2(0, 0), new Vector2(100, 25), 12, TextAlignmentOptions.Center);
        listPanel.idleCountText = CreateTMPText(countersContainer, "IdleCount", "Idle: 0",
            new Vector2(115, 0), new Vector2(100, 25), 12, TextAlignmentOptions.Right);

        // Filter Buttons
        GameObject filtersContainer = new GameObject("Filters");
        filtersContainer.transform.SetParent(panel.transform, false);
        RectTransform filtersRect = filtersContainer.AddComponent<RectTransform>();
        filtersRect.anchoredPosition = new Vector2(0, 150);
        filtersRect.sizeDelta = new Vector2(350, 30);

        listPanel.showAllButton = CreateButton(filtersContainer, "ShowAll", "All",
            new Vector2(-130, 0), new Vector2(75, 25), new Color(0.2f, 0.4f, 0.6f));
        listPanel.showActiveButton = CreateButton(filtersContainer, "ShowActive", "Active",
            new Vector2(-45, 0), new Vector2(75, 25), new Color(0.2f, 0.6f, 0.2f));
        listPanel.showIdleButton = CreateButton(filtersContainer, "ShowIdle", "Idle",
            new Vector2(40, 0), new Vector2(75, 25), new Color(0.6f, 0.6f, 0.2f));
        listPanel.showSelectedButton = CreateButton(filtersContainer, "ShowSelected", "Selected",
            new Vector2(125, 0), new Vector2(75, 25), new Color(0.6f, 0.2f, 0.6f));

        // Scroll View for turtle list
        GameObject scrollView = CreateScrollView(panel, "TurtleScrollView", new Vector2(0, 20), new Vector2(350, 280));
        listPanel.turtleListContent = scrollView.transform.Find("Viewport/Content");

        // Add placeholder info text in scroll view
        CreateTMPText(listPanel.turtleListContent.gameObject, "PlaceholderInfo",
            "Waiting for turtles...\n\nTurtles will appear here when:\n• Connected to server\n• Turtles are registered\n• MultiTurtleManager is active",
            new Vector2(0, -50), new Vector2(330, 200), 12, TextAlignmentOptions.Center);

        // Action Buttons
        GameObject actionsContainer = new GameObject("Actions");
        actionsContainer.transform.SetParent(panel.transform, false);
        RectTransform actionsRect = actionsContainer.AddComponent<RectTransform>();
        actionsRect.anchoredPosition = new Vector2(0, -200);
        actionsRect.sizeDelta = new Vector2(350, 30);

        listPanel.selectAllButton = CreateButton(actionsContainer, "SelectAll", "Select All",
            new Vector2(-85, 0), new Vector2(100, 25), new Color(0.2f, 0.6f, 0.2f));
        listPanel.deselectAllButton = CreateButton(actionsContainer, "DeselectAll", "Deselect All",
            new Vector2(0, 0), new Vector2(100, 25), new Color(0.6f, 0.2f, 0.2f));
        listPanel.selectIdleButton = CreateButton(actionsContainer, "SelectIdle", "Select Idle",
            new Vector2(85, 0), new Vector2(100, 25), new Color(0.2f, 0.4f, 0.6f));

        // Assign to ModernUIManager
        if (modernUIManager != null)
            modernUIManager.turtleList = listPanel;

        // Initially hidden - user can toggle with T key or button
        panel.SetActive(false);
    }

    #endregion

    #region Task Queue Panel

    private void BuildTaskQueuePanel()
    {
        GameObject panel = CreatePanel("TaskQueuePanel", new Vector2(450, 200), new Vector2(380, 500));

        if (modernUIManager != null)
            modernUIManager.taskQueuePanel = panel;

        // Add TaskQueuePanel component
        TaskQueuePanel queuePanel = panel.AddComponent<TaskQueuePanel>();

        // Header
        queuePanel.headerText = CreateTMPText(panel, "HeaderText", "Task Queue (Q)",
            new Vector2(0, 220), new Vector2(350, 30), 18, TextAlignmentOptions.Center);

        // Counters
        GameObject countersContainer = new GameObject("Counters");
        countersContainer.transform.SetParent(panel.transform, false);
        RectTransform countersRect = countersContainer.AddComponent<RectTransform>();
        countersRect.anchoredPosition = new Vector2(0, 185);
        countersRect.sizeDelta = new Vector2(350, 25);

        queuePanel.pendingCountText = CreateTMPText(countersContainer, "PendingCount", "Pending: 0",
            new Vector2(-115, 0), new Vector2(100, 25), 12, TextAlignmentOptions.Left);
        queuePanel.activeCountText = CreateTMPText(countersContainer, "ActiveCount", "Active: 0",
            new Vector2(0, 0), new Vector2(100, 25), 12, TextAlignmentOptions.Center);
        queuePanel.completedCountText = CreateTMPText(countersContainer, "CompletedCount", "Completed: 0",
            new Vector2(115, 0), new Vector2(100, 25), 12, TextAlignmentOptions.Right);

        // Filter Toggles
        GameObject filtersContainer = new GameObject("Filters");
        filtersContainer.transform.SetParent(panel.transform, false);
        RectTransform filtersRect = filtersContainer.AddComponent<RectTransform>();
        filtersRect.anchoredPosition = new Vector2(0, 150);
        filtersRect.sizeDelta = new Vector2(350, 30);

        queuePanel.showPendingToggle = CreateToggle(filtersContainer, "ShowPending", "Pending",
            new Vector2(-100, 0), new Vector2(100, 25));
        queuePanel.showActiveToggle = CreateToggle(filtersContainer, "ShowActive", "Active",
            new Vector2(0, 0), new Vector2(100, 25));
        queuePanel.showCompletedToggle = CreateToggle(filtersContainer, "ShowCompleted", "Completed",
            new Vector2(100, 0), new Vector2(100, 25));

        // Scroll View for task list
        GameObject scrollView = CreateScrollView(panel, "TaskScrollView", new Vector2(0, 20), new Vector2(350, 280));
        queuePanel.taskListContent = scrollView.transform.Find("Viewport/Content");

        // Add placeholder info text in scroll view
        CreateTMPText(queuePanel.taskListContent.gameObject, "PlaceholderInfo",
            "No tasks queued\n\nTasks will appear here when:\n• Mining operations start\n• Building operations start\n• Turtle commands queued\n\nUse Mining (M) or Building (B) modes to create tasks.",
            new Vector2(0, -80), new Vector2(330, 250), 12, TextAlignmentOptions.Center);

        // Action Buttons
        GameObject actionsContainer = new GameObject("Actions");
        actionsContainer.transform.SetParent(panel.transform, false);
        RectTransform actionsRect = actionsContainer.AddComponent<RectTransform>();
        actionsRect.anchoredPosition = new Vector2(0, -200);
        actionsRect.sizeDelta = new Vector2(350, 30);

        queuePanel.clearCompletedButton = CreateButton(actionsContainer, "ClearCompleted", "Clear Completed",
            new Vector2(-85, 0), new Vector2(140, 25), new Color(0.6f, 0.4f, 0.2f));
        queuePanel.cancelAllButton = CreateButton(actionsContainer, "CancelAll", "Cancel All",
            new Vector2(85, 0), new Vector2(140, 25), new Color(0.6f, 0.2f, 0.2f));

        // Assign to ModernUIManager
        if (modernUIManager != null)
            modernUIManager.taskQueue = queuePanel;

        panel.SetActive(false);
    }

    #endregion

    #region Structure Selection Panel

    private void BuildStructureSelectionPanel()
    {
        GameObject panel = CreatePanel("StructureSelectionPanel", new Vector2(0, 0), new Vector2(600, 700));

        if (modernUIManager != null)
            modernUIManager.structureSelectionPanel = panel;

        // Add StructureSelectionPanel component
        StructureSelectionPanel structPanel = panel.AddComponent<StructureSelectionPanel>();

        // Header
        structPanel.headerText = CreateTMPText(panel, "HeaderText", "Structure Selection (B)",
            new Vector2(0, 320), new Vector2(550, 35), 20, TextAlignmentOptions.Center);

        // Search and Filter
        GameObject searchContainer = new GameObject("SearchContainer");
        searchContainer.transform.SetParent(panel.transform, false);
        RectTransform searchRect = searchContainer.AddComponent<RectTransform>();
        searchRect.anchoredPosition = new Vector2(0, 275);
        searchRect.sizeDelta = new Vector2(550, 35);

        structPanel.searchField = CreateTMPInputField(searchContainer, "SearchField", "Search structures...",
            new Vector2(-125, 0), new Vector2(280, 30));
        structPanel.categoryFilter = CreateTMPDropdown(searchContainer, "CategoryFilter",
            new Vector2(125, 0), new Vector2(220, 30));

        // Scroll View for structure list
        GameObject scrollView = CreateScrollView(panel, "StructureScrollView", new Vector2(-150, 50), new Vector2(250, 400));
        structPanel.structureListContent = scrollView.transform.Find("Viewport/Content");

        // Add placeholder for structures
        CreateTMPText(structPanel.structureListContent.gameObject, "StructurePlaceholder",
            "No structures found\n\nStructures will appear here when:\n• Loaded from files\n• Created in Structure Editor\n\nUse 'Open Editor' to create new structures.",
            new Vector2(0, -80), new Vector2(230, 300), 11, TextAlignmentOptions.Center);

        // Preview Area
        GameObject previewPanel = CreatePanel("PreviewPanel", new Vector2(135, 50), new Vector2(280, 400), panel.transform);
        previewPanel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.9f);

        structPanel.previewNameText = CreateTMPText(previewPanel, "PreviewName", "Select a structure",
            new Vector2(0, 175), new Vector2(260, 30), 16, TextAlignmentOptions.Center);
        structPanel.previewDescriptionText = CreateTMPText(previewPanel, "PreviewDescription", "Description will appear here",
            new Vector2(0, 100), new Vector2(260, 100), 11, TextAlignmentOptions.TopLeft);
        structPanel.previewBlockCountText = CreateTMPText(previewPanel, "PreviewBlockCount", "Blocks: 0",
            new Vector2(0, -50), new Vector2(260, 25), 12, TextAlignmentOptions.Center);
        structPanel.previewSizeText = CreateTMPText(previewPanel, "PreviewSize", "Size: 0x0x0",
            new Vector2(0, -75), new Vector2(260, 25), 12, TextAlignmentOptions.Center);

        structPanel.selectButton = CreateButton(previewPanel, "SelectButton", "Select & Build",
            new Vector2(0, -160), new Vector2(200, 35), new Color(0.2f, 0.6f, 0.2f));

        // Bottom Buttons
        GameObject buttonContainer = new GameObject("Buttons");
        buttonContainer.transform.SetParent(panel.transform, false);
        RectTransform buttonRect = buttonContainer.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(0, -310);
        buttonRect.sizeDelta = new Vector2(550, 35);

        structPanel.refreshButton = CreateButton(buttonContainer, "RefreshButton", "Refresh",
            new Vector2(-170, 0), new Vector2(100, 30), new Color(0.2f, 0.4f, 0.6f));
        structPanel.openEditorButton = CreateButton(buttonContainer, "OpenEditor", "Open Editor",
            new Vector2(-50, 0), new Vector2(120, 30), new Color(0.4f, 0.2f, 0.6f));
        structPanel.closeButton = CreateButton(buttonContainer, "CloseButton", "Close (ESC)",
            new Vector2(90, 0), new Vector2(120, 30), new Color(0.6f, 0.2f, 0.2f));

        // Assign to ModernUIManager
        if (modernUIManager != null)
            modernUIManager.structureSelection = structPanel;

        panel.SetActive(false);
    }

    #endregion

    #region Context Menu Panel

    private void BuildContextMenuPanel()
    {
        GameObject panel = CreatePanel("ContextMenuPanel", new Vector2(0, 0), new Vector2(280, 280));

        if (modernUIManager != null)
            modernUIManager.contextMenuPanel = panel;

        // Make panel circular background
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

        // Add AnnoStyleContextMenu component
        AnnoStyleContextMenu contextMenu = panel.AddComponent<AnnoStyleContextMenu>();

        // Create center area for context display
        GameObject centerArea = new GameObject("CenterArea");
        centerArea.transform.SetParent(panel.transform, false);
        RectTransform centerRect = centerArea.AddComponent<RectTransform>();
        centerRect.anchoredPosition = Vector2.zero;
        centerRect.sizeDelta = new Vector2(100, 100);

        Image centerImage = centerArea.AddComponent<Image>();
        centerImage.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        CreateTMPText(centerArea, "ContextInfo", "Right-Click\nContext",
            Vector2.zero, new Vector2(90, 90), 12, TextAlignmentOptions.Center);

        // Create circular buttons
        contextMenu.miningButton = CreateCircularButton(panel, "MiningButton", "Mine",
            new Vector2(0, 90), new Vector2(70, 70), new Color(0.8f, 0.3f, 0.2f));

        contextMenu.buildingButton = CreateCircularButton(panel, "BuildingButton", "Build",
            new Vector2(90, 0), new Vector2(70, 70), new Color(0.2f, 0.8f, 0.3f));

        contextMenu.moveButton = CreateCircularButton(panel, "MoveButton", "Move",
            new Vector2(0, -90), new Vector2(70, 70), new Color(0.2f, 0.4f, 0.8f));

        contextMenu.cancelButton = CreateCircularButton(panel, "CancelButton", "Cancel",
            new Vector2(-90, 0), new Vector2(70, 70), new Color(0.6f, 0.2f, 0.2f));

        // Assign panel reference
        contextMenu.menuPanel = panel;

        // Assign to ModernUIManager
        if (modernUIManager != null)
            modernUIManager.contextMenu = contextMenu;

        panel.SetActive(false);
    }

    private Button CreateCircularButton(GameObject parent, string name, string label, Vector2 position, Vector2 size, Color color)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent.transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = buttonObj.AddComponent<Image>();
        image.color = color;
        image.type = Image.Type.Simple;

        Button button = buttonObj.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.3f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f);
        button.colors = colors;

        // Add label text
        CreateTMPText(buttonObj, "Label", label, Vector2.zero, size, 14, TextAlignmentOptions.Center, true);

        return button;
    }

    #endregion

    #region Quick Actions Panel

    private void BuildQuickActionsPanel()
    {
        GameObject panel = CreatePanel("QuickActionsPanel", new Vector2(0, -450), new Vector2(900, 90));

        if (modernUIManager != null)
            modernUIManager.quickActionsPanel = panel;

        CreateTMPText(panel, "QuickActionsLabel", "Quick Actions",
            new Vector2(-380, 25), new Vector2(120, 20), 12, TextAlignmentOptions.Left);

        // Status text showing selection info
        TextMeshProUGUI statusText = CreateTMPText(panel, "SelectionStatusText", "No selection - Press M for Mining Mode",
            new Vector2(0, 25), new Vector2(500, 20), 12, TextAlignmentOptions.Center);
        statusText.color = new Color(0.8f, 0.8f, 0.2f); // Yellow

        // Assign to ModernUIManager
        if (modernUIManager != null)
            modernUIManager.selectionStatusText = statusText;

        GameObject buttonsContainer = new GameObject("ButtonsContainer");
        buttonsContainer.transform.SetParent(panel.transform, false);
        RectTransform buttonsRect = buttonsContainer.AddComponent<RectTransform>();
        buttonsRect.anchoredPosition = new Vector2(-50, -15);
        buttonsRect.sizeDelta = new Vector2(800, 40);

        // Create buttons - event handlers will be set up by ModernUIManager.SetupQuickActionsButtons()
        CreateButton(buttonsContainer, "MiningMode", "Mining (M)",
            new Vector2(-330, 0), new Vector2(100, 35), new Color(0.8f, 0.2f, 0.2f));
        CreateButton(buttonsContainer, "BuildingMode", "Building (B)",
            new Vector2(-215, 0), new Vector2(100, 35), new Color(0.2f, 0.6f, 0.2f));
        CreateButton(buttonsContainer, "TurtleList", "Turtles (T)",
            new Vector2(-100, 0), new Vector2(100, 35), new Color(0.2f, 0.4f, 0.6f));
        CreateButton(buttonsContainer, "TaskQueue", "Tasks (Q)",
            new Vector2(15, 0), new Vector2(100, 35), new Color(0.6f, 0.4f, 0.2f));
        CreateButton(buttonsContainer, "ExecuteSelection", "Execute (Enter)",
            new Vector2(130, 0), new Vector2(120, 35), new Color(0.2f, 0.8f, 0.2f));
        CreateButton(buttonsContainer, "CancelSelection", "Cancel (Esc)",
            new Vector2(265, 0), new Vector2(110, 35), new Color(0.8f, 0.2f, 0.2f));

        panel.SetActive(true);
    }

    #endregion

    #region UI Component Helpers

    private GameObject CreatePanel(string name, Vector2 anchoredPosition, Vector2 sizeDelta, Transform parent = null)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent != null ? parent : mainCanvas.transform, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        Image image = panel.AddComponent<Image>();
        image.color = panelColor;
        image.raycastTarget = true;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = Color.gray;
        outline.effectDistance = new Vector2(2, 2);

        return panel;
    }

    private Button CreateButton(GameObject parent, string name, string text, Vector2 anchoredPosition, Vector2 sizeDelta, Color? color = null)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent.transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = buttonObj.AddComponent<Image>();
        image.color = color ?? buttonColor;

        Button button = buttonObj.AddComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = color ?? buttonColor;
        colors.highlightedColor = color.HasValue ? Color.Lerp(color.Value, Color.white, 0.2f) : buttonHighlightColor;
        colors.pressedColor = color.HasValue ? Color.Lerp(color.Value, Color.black, 0.2f) : Color.gray;
        colors.disabledColor = Color.gray;
        button.colors = colors;

        CreateTMPText(buttonObj, "Text", text, Vector2.zero, sizeDelta, defaultFontSize, TextAlignmentOptions.Center, true);

        return button;
    }

    private TextMeshProUGUI CreateTMPText(GameObject parent, string name, string text, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize, TextAlignmentOptions alignment, bool fillParent = false)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        if (fillParent)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);
        }
        else
        {
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;

        // Try to get a TMP font, fallback to default
        TMP_FontAsset font = GetTMPFont();
        if (font != null)
            tmp.font = font;

        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;

        return tmp;
    }

    private TMP_InputField CreateTMPInputField(GameObject parent, string name, string placeholder, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject fieldObj = new GameObject(name);
        fieldObj.transform.SetParent(parent.transform, false);

        RectTransform rect = fieldObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = fieldObj.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        TMP_InputField inputField = fieldObj.AddComponent<TMP_InputField>();

        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(fieldObj.transform, false);
        RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(5, 2);
        textAreaRect.offsetMax = new Vector2(-5, -2);

        TextMeshProUGUI textComponent = CreateTMPText(textArea, "Text", "", Vector2.zero, Vector2.zero, defaultFontSize, TextAlignmentOptions.Left, true);
        TextMeshProUGUI placeholderComponent = CreateTMPText(textArea, "Placeholder", placeholder, Vector2.zero, Vector2.zero, defaultFontSize, TextAlignmentOptions.Left, true);
        placeholderComponent.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        inputField.textViewport = textAreaRect;
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholderComponent;

        return inputField;
    }

    private TMP_Dropdown CreateTMPDropdown(GameObject parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject dropdownObj = new GameObject(name);
        dropdownObj.transform.SetParent(parent.transform, false);

        RectTransform rect = dropdownObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = dropdownObj.AddComponent<Image>();
        image.color = buttonColor;

        TMP_Dropdown dropdown = dropdownObj.AddComponent<TMP_Dropdown>();

        // Label
        TextMeshProUGUI labelText = CreateTMPText(dropdownObj, "Label", "Select...", Vector2.zero, sizeDelta, defaultFontSize, TextAlignmentOptions.Left, true);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.offsetMin = new Vector2(10, 2);
        labelRect.offsetMax = new Vector2(-25, -2);

        dropdown.captionText = labelText;
        dropdown.options.Add(new TMP_Dropdown.OptionData("All"));

        return dropdown;
    }

    private Toggle CreateToggle(GameObject parent, string name, string label, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject toggleObj = new GameObject(name);
        toggleObj.transform.SetParent(parent.transform, false);

        RectTransform rect = toggleObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(toggleObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.5f);
        bgRect.anchorMax = new Vector2(0, 0.5f);
        bgRect.anchoredPosition = new Vector2(10, 0);
        bgRect.sizeDelta = new Vector2(20, 20);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Checkmark
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(bgObj.transform, false);
        RectTransform checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        Image checkImage = checkObj.AddComponent<Image>();
        checkImage.color = new Color(0.2f, 0.8f, 0.2f);

        // Label
        CreateTMPText(toggleObj, "Label", label, new Vector2(40, 0), new Vector2(sizeDelta.x - 30, sizeDelta.y), 12, TextAlignmentOptions.Left);

        toggle.graphic = checkImage;
        toggle.isOn = true;

        return toggle;
    }

    private GameObject CreateScrollView(GameObject parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject scrollViewObj = new GameObject(name);
        scrollViewObj.transform.SetParent(parent.transform, false);

        RectTransform rect = scrollViewObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Image image = scrollViewObj.AddComponent<Image>();
        image.color = new Color(0.05f, 0.05f, 0.05f, 0.8f);

        ScrollRect scrollRect = scrollViewObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollViewObj.transform, false);

        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewportObj.AddComponent<Image>().color = Color.clear;
        viewportObj.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);

        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 8;  // Increased spacing
        layout.padding = new RectOffset(10, 10, 10, 10);  // More padding
        layout.childAlignment = TextAnchor.UpperCenter;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return scrollViewObj;
    }

    #endregion

    private void SetupModernUIManagerReferences()
    {
        if (modernUIManager != null)
        {
            Debug.Log("ModernUIManager references have been automatically assigned");
            Debug.Log("All Modern UI panels created successfully:");
            Debug.Log("- Turtle List Panel");
            Debug.Log("- Task Queue Panel");
            Debug.Log("- Structure Selection Panel");
            Debug.Log("- Context Menu Panel");
            Debug.Log("- Quick Actions Panel");
        }
    }

    private TMP_FontAsset GetTMPFont()
    {
        if (tmpFont != null)
            return tmpFont;

        // Try to get default TMP font
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    [ContextMenu("Rebuild All UI")]
    public void RebuildAllUI()
    {
        if (mainCanvas != null)
        {
            DestroyImmediate(mainCanvas.gameObject);
        }

        BuildCompleteUI();
    }

    public void DestroyUI()
    {
        if (mainCanvas != null)
        {
            DestroyImmediate(mainCanvas.gameObject);
        }

        if (eventSystem != null)
        {
            DestroyImmediate(eventSystem);
        }
    }
}
