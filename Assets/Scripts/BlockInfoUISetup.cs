using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper script to automatically create UI elements for block information display.
/// Attach this to an empty GameObject and run it once to create the UI structure.
/// </summary>
public class BlockInfoUISetup : MonoBehaviour
{
    [Header("Setup Options")]
    public bool createUIOnStart = true;
    public Canvas targetCanvas; // If null, will create new canvas
    
    [Header("UI Styling")]
    public Font textFont;
    public int fontSize = 14;
    public Color textColor = Color.white;
    public Color backgroundColor = new Color(0, 0, 0, 0.7f);
    
    void Start()
    {
        if (createUIOnStart)
        {
            CreateBlockInfoUI();
        }
    }
    
    [ContextMenu("Create Block Info UI")]
    public void CreateBlockInfoUI()
    {
        // Find or create canvas
        Canvas canvas = FindOrCreateCanvas();
        
        // Create main panel
        GameObject panel = CreatePanel(canvas, "BlockInfoPanel", new Vector2(300, 400));
        panel.transform.SetAsLastSibling();
        
        // Position panel at top-left corner
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        
        // Create title
        CreateText(panel, "BlockInfoTitle", "Block Information", new Vector2(280, 30), new Vector2(0, -10));
        
        // Create block info text
        GameObject blockInfoText = CreateText(panel, "BlockInfoText", "No block selected", new Vector2(280, 120), new Vector2(0, -50));
        
        // Create chunk info text  
        GameObject chunkInfoText = CreateText(panel, "ChunkInfoText", "No chunk selected", new Vector2(280, 120), new Vector2(0, -180));
        
        // Create analyze button
        GameObject analyzeButton = CreateButton(panel, "AnalyzeChunkButton", "Analyze Chunk", new Vector2(200, 30), new Vector2(0, -320));
        
        // Try to connect to RTSCameraController
        RTSCameraController cameraController = FindFirstObjectByType<RTSCameraController>();
        if (cameraController != null)
        {
            // Set references
            cameraController.infoCanvas = canvas;
            cameraController.blockInfoText = blockInfoText.GetComponent<Text>();
            cameraController.chunkInfoText = chunkInfoText.GetComponent<Text>();
            cameraController.analyzeChunkButton = analyzeButton.GetComponent<Button>();
            
            Debug.Log("Successfully connected UI to RTSCameraController!");
        }
        else
        {
            Debug.LogWarning("RTSCameraController not found. You'll need to manually assign UI references.");
        }
        
        Debug.Log("Block Info UI created successfully!");
    }
    
    Canvas FindOrCreateCanvas()
    {
        if (targetCanvas != null)
            return targetCanvas;
            
        // Try to find existing canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            return canvas;
        
        // Create new canvas
        GameObject canvasGO = new GameObject("BlockInfoCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        // Add CanvasScaler
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Add GraphicRaycaster
        canvasGO.AddComponent<GraphicRaycaster>();
        
        return canvas;
    }
    
    GameObject CreatePanel(Canvas parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent.transform, false);
        
        // Add Image component for background
        Image image = panel.AddComponent<Image>();
        image.color = backgroundColor;
        
        // Setup RectTransform
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        
        return panel;
    }
    
    GameObject CreateText(GameObject parent, string name, string text, Vector2 size, Vector2 position)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent.transform, false);
        
        Text textComponent = textGO.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = textFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        textComponent.fontSize = fontSize;
        textComponent.color = textColor;
        textComponent.alignment = TextAnchor.UpperLeft;
        
        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        
        return textGO;
    }
    
    GameObject CreateButton(GameObject parent, string name, string text, Vector2 size, Vector2 position)
    {
        GameObject buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent.transform, false);
        
        // Add Image for background
        Image image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.2f, 0.3f, 0.8f, 0.8f);
        
        // Add Button component
        Button button = buttonGO.AddComponent<Button>();
        
        // Setup RectTransform
        RectTransform rect = buttonGO.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        
        // Create text child
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        
        Text textComponent = textGO.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = textFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        textComponent.fontSize = fontSize;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        return buttonGO;
    }
}