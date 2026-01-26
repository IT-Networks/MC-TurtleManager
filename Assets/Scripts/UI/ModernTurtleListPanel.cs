using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Modern turtle list panel with position display, task info, and camera jump functionality
/// </summary>
public class ModernTurtleListPanel : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform turtleListContent;
    public GameObject turtleItemPrefab;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI totalCountText;
    public TextMeshProUGUI activeCountText;
    public TextMeshProUGUI idleCountText;

    [Header("Filter Buttons")]
    public Button showAllButton;
    public Button showActiveButton;
    public Button showIdleButton;
    public Button showSelectedButton;

    [Header("Actions")]
    public Button selectAllButton;
    public Button deselectAllButton;
    public Button selectIdleButton;

    [Header("Settings")]
    public float updateInterval = 0.5f;
    public bool autoSort = true;
    public TurtleSortMode sortMode = TurtleSortMode.ByName;

    private MultiTurtleManager turtleManager;
    private TurtleSelectionManager selectionManager;
    private RTSCameraController cameraController;
    private Dictionary<int, TurtleListItem> turtleItems = new Dictionary<int, TurtleListItem>();
    private float lastUpdateTime;
    private FilterMode currentFilter = FilterMode.All;

    public enum TurtleSortMode { ByName, ByDistance, ByStatus }
    public enum FilterMode { All, Active, Idle, Selected }

    public void Initialize(MultiTurtleManager manager, TurtleSelectionManager selection, RTSCameraController camera)
    {
        turtleManager = manager;
        selectionManager = selection;
        cameraController = camera;

        SetupButtons();
        RegisterEvents();
    }

    private void SetupButtons()
    {
        if (selectAllButton != null)
            selectAllButton.onClick.AddListener(() => selectionManager?.SelectAllTurtles());

        if (deselectAllButton != null)
            deselectAllButton.onClick.AddListener(() => selectionManager?.ClearSelection());

        if (selectIdleButton != null)
            selectIdleButton.onClick.AddListener(() => selectionManager?.SelectAvailableTurtles());

        // Filter buttons
        if (showAllButton != null)
            showAllButton.onClick.AddListener(() => SetFilter(FilterMode.All));

        if (showActiveButton != null)
            showActiveButton.onClick.AddListener(() => SetFilter(FilterMode.Active));

        if (showIdleButton != null)
            showIdleButton.onClick.AddListener(() => SetFilter(FilterMode.Idle));

        if (showSelectedButton != null)
            showSelectedButton.onClick.AddListener(() => SetFilter(FilterMode.Selected));
    }

    private void RegisterEvents()
    {
        if (turtleManager != null)
        {
            turtleManager.OnTurtleAdded += OnTurtleAdded;
            turtleManager.OnTurtleRemoved += OnTurtleRemoved;
            turtleManager.OnTurtlesUpdated += OnTurtlesUpdated;
        }

        if (selectionManager != null)
        {
            selectionManager.OnSelectionChanged += OnSelectionChanged;
        }
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateTurtleList();
            UpdateHeaderInfo();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateHeaderInfo()
    {
        if (turtleManager == null) return;

        var allTurtles = turtleManager.GetAllTurtles();
        int total = allTurtles.Count;
        int active = allTurtles.Count(t => t.isBusy);
        int idle = total - active;

        if (totalCountText != null)
            totalCountText.text = $"Total: {total}";

        if (activeCountText != null)
            activeCountText.text = $"Active: {active}";

        if (idleCountText != null)
            idleCountText.text = $"Idle: {idle}";

        if (headerText != null)
            headerText.text = $"Turtles ({total})";
    }

    private void UpdateTurtleList()
    {
        if (turtleManager == null || turtleListContent == null) return;

        var allTurtles = turtleManager.GetAllTurtles();
        var filteredTurtles = FilterTurtles(allTurtles);

        if (autoSort)
            filteredTurtles = SortTurtles(filteredTurtles);

        // Update or create items
        foreach (var turtle in filteredTurtles)
        {
            if (!turtleItems.ContainsKey(turtle.turtleId))
            {
                CreateTurtleItem(turtle);
            }
            else
            {
                turtleItems[turtle.turtleId].UpdateDisplay(turtle);
            }
        }

        // Remove items that don't match filter or no longer exist
        var itemsToRemove = turtleItems.Keys
            .Where(id => !filteredTurtles.Any(t => t.turtleId == id))
            .ToList();

        foreach (var id in itemsToRemove)
        {
            if (turtleItems.TryGetValue(id, out TurtleListItem item))
            {
                Destroy(item.gameObject);
                turtleItems.Remove(id);
            }
        }

        // Update sibling order for sorting
        for (int i = 0; i < filteredTurtles.Count; i++)
        {
            var turtle = filteredTurtles[i];
            if (turtleItems.TryGetValue(turtle.turtleId, out TurtleListItem item))
            {
                item.transform.SetSiblingIndex(i);
            }
        }
    }

    private List<TurtleObject> FilterTurtles(List<TurtleObject> turtles)
    {
        return currentFilter switch
        {
            FilterMode.Active => turtles.Where(t => t.isBusy).ToList(),
            FilterMode.Idle => turtles.Where(t => !t.isBusy).ToList(),
            FilterMode.Selected => turtles.Where(t => t.isSelected).ToList(),
            _ => turtles
        };
    }

    private List<TurtleObject> SortTurtles(List<TurtleObject> turtles)
    {
        return sortMode switch
        {
            TurtleSortMode.ByName => turtles.OrderBy(t => t.turtleName).ToList(),
            TurtleSortMode.ByDistance => turtles.OrderBy(t => GetDistanceToCamera(t)).ToList(),
            TurtleSortMode.ByStatus => turtles.OrderByDescending(t => t.isBusy).ThenBy(t => t.turtleName).ToList(),
            _ => turtles
        };
    }

    private float GetDistanceToCamera(TurtleObject turtle)
    {
        if (cameraController == null || turtle == null) return float.MaxValue;
        return Vector3.Distance(cameraController.transform.position, turtle.transform.position);
    }

    private void CreateTurtleItem(TurtleObject turtle)
    {
        GameObject itemObj;

        if (turtleItemPrefab != null)
        {
            itemObj = Instantiate(turtleItemPrefab, turtleListContent);
        }
        else
        {
            itemObj = CreateDefaultTurtleItem();
        }

        TurtleListItem item = itemObj.GetComponent<TurtleListItem>();
        if (item == null)
        {
            item = itemObj.AddComponent<TurtleListItem>();
        }

        item.Initialize(turtle, selectionManager, cameraController);
        turtleItems[turtle.turtleId] = item;
    }

    private GameObject CreateDefaultTurtleItem()
    {
        GameObject item = new GameObject("TurtleItem");
        item.transform.SetParent(turtleListContent, false);

        // Add layout
        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.minHeight = 80;
        layout.preferredHeight = 80;

        // Add background
        Image bg = item.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // Create content area
        GameObject content = new GameObject("Content");
        content.transform.SetParent(item.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(10, 10);
        contentRect.offsetMax = new Vector2(-10, -10);

        return item;
    }

    private void SetFilter(FilterMode mode)
    {
        currentFilter = mode;
        UpdateTurtleList();
        UpdateFilterButtonStates();
    }

    private void UpdateFilterButtonStates()
    {
        // Visual feedback for active filter
        if (showAllButton != null)
            SetButtonHighlight(showAllButton, currentFilter == FilterMode.All);

        if (showActiveButton != null)
            SetButtonHighlight(showActiveButton, currentFilter == FilterMode.Active);

        if (showIdleButton != null)
            SetButtonHighlight(showIdleButton, currentFilter == FilterMode.Idle);

        if (showSelectedButton != null)
            SetButtonHighlight(showSelectedButton, currentFilter == FilterMode.Selected);
    }

    private void SetButtonHighlight(Button button, bool highlighted)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = highlighted ? new Color(0.3f, 0.5f, 0.8f) : Color.white;
        button.colors = colors;
    }

    private void OnTurtleAdded(TurtleObject turtle)
    {
        UpdateTurtleList();
    }

    private void OnTurtleRemoved(TurtleObject turtle)
    {
        if (turtleItems.TryGetValue(turtle.turtleId, out TurtleListItem item))
        {
            Destroy(item.gameObject);
            turtleItems.Remove(turtle.turtleId);
        }
        UpdateTurtleList();
    }

    private void OnTurtlesUpdated()
    {
        UpdateTurtleList();
    }

    private void OnSelectionChanged(List<TurtleObject> selected)
    {
        UpdateTurtleList();
    }

    private void OnDestroy()
    {
        if (turtleManager != null)
        {
            turtleManager.OnTurtleAdded -= OnTurtleAdded;
            turtleManager.OnTurtleRemoved -= OnTurtleRemoved;
            turtleManager.OnTurtlesUpdated -= OnTurtlesUpdated;
        }

        if (selectionManager != null)
        {
            selectionManager.OnSelectionChanged -= OnSelectionChanged;
        }
    }
}

/// <summary>
/// Individual turtle item in the list
/// </summary>
public class TurtleListItem : MonoBehaviour
{
    private TurtleObject turtle;
    private TurtleSelectionManager selectionManager;
    private RTSCameraController cameraController;

    private TextMeshProUGUI nameText;
    private TextMeshProUGUI positionText;
    private TextMeshProUGUI statusText;
    private Button selectButton;
    private Button jumpButton;
    private Image background;

    public void Initialize(TurtleObject turtleObj, TurtleSelectionManager selection, RTSCameraController camera)
    {
        turtle = turtleObj;
        selectionManager = selection;
        cameraController = camera;

        SetupUI();
        UpdateDisplay(turtle);
    }

    private void SetupUI()
    {
        // Find or create UI elements
        nameText = GetOrCreateText("NameText", new Vector2(10, -10), new Vector2(200, 25));
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyles.Bold;

        positionText = GetOrCreateText("PositionText", new Vector2(10, -30), new Vector2(200, 20));
        positionText.fontSize = 12;
        positionText.color = new Color(0.8f, 0.8f, 0.8f);

        statusText = GetOrCreateText("StatusText", new Vector2(10, -50), new Vector2(200, 20));
        statusText.fontSize = 12;

        // Create buttons
        jumpButton = CreateButton("JumpButton", "Jump To", new Vector2(-90, -15), new Vector2(80, 30));
        jumpButton.onClick.AddListener(OnJumpToTurtle);

        selectButton = CreateButton("SelectButton", "Select", new Vector2(-90, -50), new Vector2(80, 25));
        selectButton.onClick.AddListener(OnSelectTurtle);

        background = GetComponent<Image>();
    }

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
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return text;
    }

    private Button CreateButton(string name, string label, Vector2 position, Vector2 size)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(transform, false);

        Image img = buttonObj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.5f, 0.8f);

        Button button = buttonObj.AddComponent<Button>();

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        // Button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return button;
    }

    public void UpdateDisplay(TurtleObject turtleObj)
    {
        turtle = turtleObj;

        if (nameText != null)
        {
            string selectedMark = turtle.isSelected ? "✓ " : "";
            nameText.text = $"{selectedMark}{turtle.turtleName} (ID: {turtle.turtleId})";
        }

        if (positionText != null)
        {
            Vector3 pos = turtle.transform.position;
            positionText.text = $"Pos: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})";
        }

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

        // Update background color
        if (background != null)
        {
            if (turtle.isSelected)
                background.color = new Color(0.2f, 0.4f, 0.7f, 0.95f);
            else if (turtle.isBusy)
                background.color = new Color(0.4f, 0.3f, 0.15f, 0.95f);
            else
                background.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);
        }
    }

    private void OnSelectTurtle()
    {
        if (selectionManager != null && turtle != null)
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                // Multi-select
                selectionManager.SelectTurtle(turtle);
            }
            else
            {
                // Single select (clear others)
                selectionManager.ClearSelection();
                selectionManager.SelectTurtle(turtle);
            }
        }
    }

    private void OnJumpToTurtle()
    {
        if (cameraController != null && turtle != null)
        {
            // Jump camera to turtle position
            cameraController.JumpToPosition(turtle.transform.position);
        }
    }
}
