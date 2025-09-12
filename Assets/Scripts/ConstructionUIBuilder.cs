using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Automatically builds the complete UI for the construction system
/// </summary>
public class ConstructionUIBuilder : MonoBehaviour
{
    [Header("UI Settings")]
    public bool buildUIOnStart = true;
    public Font defaultFont;
    public Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    public Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    public Color buttonHighlightColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color textColor = Color.white;
    public int defaultFontSize = 14;
    
    [Header("References")]
    public ConstructionUI constructionUI;
    
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
        
        // Build all UI panels
        BuildConstructionPanel();
        BuildStructureSelectionPanel();
        BuildOperationStatusPanel();
        
        // Setup ConstructionUI component
        SetupConstructionUIReferences();
        
        Debug.Log("Complete Construction UI built successfully!");
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
        GameObject canvasObj = new GameObject("ConstructionCanvas");
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
        
        // Add ConstructionUI component if not assigned
        if (constructionUI == null)
        {
            constructionUI = canvasObj.AddComponent<ConstructionUI>();
        }
        constructionUI.mainCanvas = mainCanvas;
    }

    private void BuildConstructionPanel()
    {
        // Create main construction panel
        GameObject panel = CreatePanel("ConstructionPanel", new Vector2(-450, 0), new Vector2(350, 500));
        constructionUI.constructionPanel = panel;
        
        // Title
        CreateText(panel, "ConstructionTitle", "Construction System", new Vector2(0, 200), new Vector2(300, 30), 18, TextAnchor.MiddleCenter);
        
        // Mode selection section
        CreateText(panel, "ModeLabel", "Selection Mode:", new Vector2(-120, 150), new Vector2(100, 25), 14, TextAnchor.MiddleLeft);
        
        // Mining mode button
        constructionUI.miningModeButton = CreateButton(panel, "MiningButton", "Mining Mode (M)", 
            new Vector2(-120, 110), new Vector2(140, 30), Color.red);
        
        // Building mode button
        constructionUI.buildingModeButton = CreateButton(panel, "BuildingButton", "Building Mode (B)", 
            new Vector2(20, 110), new Vector2(140, 30), Color.green);
        
        // Selection status
        CreateText(panel, "StatusLabel", "Status:", new Vector2(-120, 60), new Vector2(60, 25), 14, TextAnchor.MiddleLeft);
        constructionUI.selectionStatusText = CreateText(panel, "SelectionStatus", "Select a mode to start", 
            new Vector2(0, 20), new Vector2(300, 50), 12, TextAnchor.UpperCenter);
        
        // Selected blocks info
        constructionUI.selectedBlocksText = CreateText(panel, "SelectedBlocks", "Selected Blocks: 0", 
            new Vector2(0, -30), new Vector2(300, 25), 12, TextAnchor.MiddleCenter);
        
        // Action buttons
        constructionUI.executeOperationButton = CreateButton(panel, "ExecuteButton", "Execute Operation", 
            new Vector2(-80, -80), new Vector2(120, 35), new Color(0.2f, 0.6f, 0.2f));
        
        constructionUI.cancelSelectionButton = CreateButton(panel, "CancelButton", "Cancel (ESC)", 
            new Vector2(80, -80), new Vector2(120, 35), new Color(0.6f, 0.2f, 0.2f));
        
        // Controls info
        CreateText(panel, "ControlsInfo", "Controls:\nLeft Click + Drag: Select Area\nRight Click: Block Info\nTab: Toggle Info Panel", 
            new Vector2(0, -150), new Vector2(300, 80), 10, TextAnchor.UpperCenter);
        
        // Initially active
        panel.SetActive(true);
    }

    private void BuildStructureSelectionPanel()
    {
        // Create structure selection panel
        GameObject panel = CreatePanel("StructurePanel", new Vector2(0, 150), new Vector2(400, 350));
        constructionUI.structureSelectionPanel = panel;
        
        // Title
        CreateText(panel, "StructureTitle", "Structure Selection", new Vector2(0, 150), new Vector2(350, 30), 16, TextAnchor.MiddleCenter);
        
        // Dropdown for structure selection
        GameObject dropdownObj = CreateDropdown(panel, "StructureDropdown", new Vector2(0, 100), new Vector2(300, 30));
        constructionUI.structureDropdown = dropdownObj.GetComponent<Dropdown>();
        
        // Structure info text
        constructionUI.structureInfoText = CreateText(panel, "StructureInfo", "Select a structure to see details", 
            new Vector2(0, 40), new Vector2(350, 80), 12, TextAnchor.UpperCenter);
        
        // Preview buttons
        constructionUI.previewStructureButton = CreateButton(panel, "PreviewButton", "Show Preview", 
            new Vector2(-80, -20), new Vector2(120, 30), new Color(0.2f, 0.4f, 0.6f));
        
        constructionUI.clearPreviewButton = CreateButton(panel, "ClearPreviewButton", "Clear Preview", 
            new Vector2(80, -20), new Vector2(120, 30), new Color(0.6f, 0.4f, 0.2f));
        
        // Confirm building button
        constructionUI.confirmBuildButton = CreateButton(panel, "ConfirmBuildButton", "Confirm Building", 
            new Vector2(0, -70), new Vector2(200, 35), new Color(0.2f, 0.6f, 0.2f));
        
        // Initially hidden
        panel.SetActive(false);
    }

    private void BuildOperationStatusPanel()
    {
        // Create operation status panel
        GameObject panel = CreatePanel("StatusPanel", new Vector2(450, -200), new Vector2(350, 300));
        constructionUI.operationStatusPanel = panel;
        
        // Title
        CreateText(panel, "StatusTitle", "Operation Status", new Vector2(0, 120), new Vector2(300, 30), 16, TextAnchor.MiddleCenter);
        
        // Operation status text
        constructionUI.operationStatusText = CreateText(panel, "OperationStatus", "No operation running", 
            new Vector2(0, 80), new Vector2(300, 25), 14, TextAnchor.MiddleCenter);
        
        // Progress slider
        GameObject sliderObj = CreateSlider(panel, "ProgressSlider", new Vector2(0, 50), new Vector2(280, 20));
        constructionUI.operationProgressSlider = sliderObj.GetComponent<Slider>();
        
        // Turtle status
        CreateText(panel, "TurtleLabel", "Turtle Status:", new Vector2(-120, 20), new Vector2(100, 20), 12, TextAnchor.MiddleLeft);
        constructionUI.turtleStatusText = CreateText(panel, "TurtleStatus", "Turtle: Unknown\nPosition: N/A\nFacing: N/A\nQueued: 0", 
            new Vector2(0, -20), new Vector2(300, 80), 10, TextAnchor.UpperCenter);
        
        // Control buttons
        constructionUI.emergencyStopButton = CreateButton(panel, "EmergencyStopButton", "EMERGENCY STOP", 
            new Vector2(-80, -100), new Vector2(120, 30), Color.red);
        
        constructionUI.cancelOperationButton = CreateButton(panel, "CancelOperationButton", "Cancel Operation", 
            new Vector2(80, -100), new Vector2(120, 30), new Color(0.6f, 0.4f, 0.2f));
        
        // Initially hidden
        panel.SetActive(false);
    }

    private GameObject CreatePanel(string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(mainCanvas.transform, false);
        
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        
        Image image = panel.AddComponent<Image>();
        image.color = panelColor;
        image.raycastTarget = true;
        
        // Add outline
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
        
        // Setup button colors
        ColorBlock colors = button.colors;
        colors.normalColor = color ?? buttonColor;
        colors.highlightedColor = color.HasValue ? Color.Lerp(color.Value, Color.white, 0.2f) : buttonHighlightColor;
        colors.pressedColor = color.HasValue ? Color.Lerp(color.Value, Color.black, 0.2f) : Color.gray;
        colors.disabledColor = Color.gray;
        button.colors = colors;
        
        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = GetFont();
        textComponent.fontSize = defaultFontSize;
        textComponent.color = textColor;
        textComponent.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        return button;
    }

    private Text CreateText(GameObject parent, string name, string text, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize = -1, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);
        
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        
        Text textComponent = textObj.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = GetFont();
        textComponent.fontSize = fontSize > 0 ? fontSize : defaultFontSize;
        textComponent.color = textColor;
        textComponent.alignment = alignment;
        
        // Add outline for better readability
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, 1);
        
        return textComponent;
    }

    private GameObject CreateDropdown(GameObject parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject dropdownObj = new GameObject(name);
        dropdownObj.transform.SetParent(parent.transform, false);
        
        RectTransform rect = dropdownObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        
        Image image = dropdownObj.AddComponent<Image>();
        image.color = buttonColor;
        
        Dropdown dropdown = dropdownObj.AddComponent<Dropdown>();
        
        // Create Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(dropdownObj.transform, false);
        
        Text labelText = labelObj.AddComponent<Text>();
        labelText.text = "Select Structure...";
        labelText.font = GetFont();
        labelText.fontSize = defaultFontSize;
        labelText.color = textColor;
        labelText.alignment = TextAnchor.MiddleLeft;
        
        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10, 2);
        labelRect.offsetMax = new Vector2(-25, -2);
        
        // Create Arrow
        GameObject arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(dropdownObj.transform, false);
        
        Image arrowImage = arrowObj.AddComponent<Image>();
        arrowImage.color = textColor;
        // You might want to assign a dropdown arrow sprite here
        
        RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1, 0);
        arrowRect.anchorMax = new Vector2(1, 1);
        arrowRect.sizeDelta = new Vector2(20, 0);
        arrowRect.offsetMin = new Vector2(-20, 0);
        arrowRect.offsetMax = new Vector2(0, 0);
        
        // Create Template
        GameObject templateObj = new GameObject("Template");
        templateObj.transform.SetParent(dropdownObj.transform, false);
        
        RectTransform templateRect = templateObj.AddComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0, 0);
        templateRect.anchorMax = new Vector2(1, 0);
        templateRect.pivot = new Vector2(0.5f, 1);
        templateRect.anchoredPosition = new Vector2(0, 2);
        templateRect.sizeDelta = new Vector2(0, 150);
        
        Image templateImage = templateObj.AddComponent<Image>();
        templateImage.color = panelColor;
        
        ScrollRect scrollRect = templateObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        
        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(templateObj.transform, false);
        
        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        
        Image viewportImage = viewportObj.AddComponent<Image>();
        viewportImage.color = Color.clear;
        
        Mask viewportMask = viewportObj.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;
        
        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 28);
        
        // Item
        GameObject itemObj = new GameObject("Item");
        itemObj.transform.SetParent(contentObj.transform, false);
        
        RectTransform itemRect = itemObj.AddComponent<RectTransform>();
        itemRect.anchorMin = Vector2.zero;
        itemRect.anchorMax = new Vector2(1, 1);
        itemRect.offsetMin = Vector2.zero;
        itemRect.offsetMax = Vector2.zero;
        
        Toggle itemToggle = itemObj.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemObj.AddComponent<Image>();
        itemToggle.isOn = true;
        
        // Item Background
        Image itemBg = itemToggle.targetGraphic as Image;
        itemBg.color = buttonColor;
        
        // Item Checkmark  
        GameObject checkmarkObj = new GameObject("Item Checkmark");
        checkmarkObj.transform.SetParent(itemObj.transform, false);
        
        RectTransform checkRect = checkmarkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0, 0);
        checkRect.anchorMax = new Vector2(1, 1);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        
        Image checkImage = checkmarkObj.AddComponent<Image>();
        checkImage.color = Color.clear; // Hidden by default
        
        // Item Label
        GameObject itemLabelObj = new GameObject("Item Label");
        itemLabelObj.transform.SetParent(itemObj.transform, false);
        
        Text itemLabelText = itemLabelObj.AddComponent<Text>();
        itemLabelText.text = "Option A";
        itemLabelText.font = GetFont();
        itemLabelText.fontSize = defaultFontSize;
        itemLabelText.color = textColor;
        itemLabelText.alignment = TextAnchor.MiddleLeft;
        
        RectTransform itemLabelRect = itemLabelObj.GetComponent<RectTransform>();
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(10, 1);
        itemLabelRect.offsetMax = new Vector2(-10, -2);
        
        // Setup dropdown references
        dropdown.targetGraphic = image;
        dropdown.captionText = labelText;
        dropdown.itemText = itemLabelText;
        dropdown.template = templateRect;
        
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        itemToggle.graphic = checkImage;
        
        templateObj.SetActive(false);
        
        return dropdownObj;
    }

    private GameObject CreateSlider(GameObject parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent.transform, false);
        
        RectTransform rect = sliderObj.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        
        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        
        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;
        
        // Fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1, 1);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        
        Image fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);
        
        // Handle Slide Area
        GameObject handleAreaObj = new GameObject("Handle Slide Area");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);
        
        RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;
        
        // Handle
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);
        
        RectTransform handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0, 0);
        handleRect.anchorMax = new Vector2(0, 1);
        handleRect.sizeDelta = new Vector2(16, 0);
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        
        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.white;
        
        // Setup slider references
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        slider.value = 0f;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        
        return sliderObj;
    }

    private void SetupConstructionUIReferences()
    {
        if (constructionUI != null)
        {
            Debug.Log("ConstructionUI references have been automatically assigned");
        }
    }

    private Font GetFont()
    {
        if (defaultFont != null)
            return defaultFont;
        
        // Try to get Arial font from resources
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        
        return font;
    }

    // Public methods for runtime UI building
    [ContextMenu("Rebuild All UI")]
    public void RebuildAllUI()
    {
        // Clear existing UI
        if (mainCanvas != null)
        {
            DestroyImmediate(mainCanvas.gameObject);
        }
        
        // Rebuild
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

    // Theme customization
    [System.Serializable]
    public class UITheme
    {
        public Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        public Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color textColor = Color.white;
        public Color accentColor = new Color(0.2f, 0.6f, 0.2f);
        public int fontSize = 14;
    }

    public void ApplyTheme(UITheme theme)
    {
        panelColor = theme.panelColor;
        buttonColor = theme.buttonColor;
        textColor = theme.textColor;
        defaultFontSize = theme.fontSize;
        
        // Rebuild with new theme
        RebuildAllUI();
    }
}