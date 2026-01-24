using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// UI for turtle selection and task assignment in multi-turtle environment
/// </summary>
public class TurtleSelectionUI : MonoBehaviour
{
    [Header("References")]
    public MultiTurtleManager turtleManager;
    public TurtleSelectionManager selectionManager;

    [Header("UI Panels")]
    public GameObject selectionPanel;
    public GameObject turtleListPanel;
    public GameObject taskAssignmentPanel;

    [Header("UI Elements")]
    public TextMeshProUGUI selectionCountText;
    public TextMeshProUGUI totalTurtlesText;
    public TextMeshProUGUI availableTurtlesText;
    public Transform turtleListContent;
    public GameObject turtleListItemPrefab;

    [Header("Task Buttons")]
    public Button assignMiningButton;
    public Button assignBuildingButton;
    public Button assignMoveButton;
    public Button selectAllButton;
    public Button selectAvailableButton;
    public Button clearSelectionButton;

    [Header("Settings")]
    public bool showTurtleList = true;
    public bool autoUpdate = true;
    public float updateInterval = 0.5f;

    private float lastUpdateTime;
    private Dictionary<int, GameObject> turtleListItems = new Dictionary<int, GameObject>();

    private void Start()
    {
        if (turtleManager == null)
            turtleManager = FindFirstObjectByType<MultiTurtleManager>();

        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<TurtleSelectionManager>();

        SetupButtons();
        RegisterEvents();
    }

    private void SetupButtons()
    {
        if (selectAllButton != null)
            selectAllButton.onClick.AddListener(() => selectionManager?.SelectAllTurtles());

        if (selectAvailableButton != null)
            selectAvailableButton.onClick.AddListener(() => selectionManager?.SelectAvailableTurtles());

        if (clearSelectionButton != null)
            clearSelectionButton.onClick.AddListener(() => selectionManager?.ClearSelection());

        // Task buttons are enabled/disabled based on selection
        UpdateTaskButtonStates();
    }

    private void RegisterEvents()
    {
        if (selectionManager != null)
        {
            selectionManager.OnSelectionChanged += OnSelectionChanged;
        }

        if (turtleManager != null)
        {
            turtleManager.OnTurtleAdded += OnTurtleAdded;
            turtleManager.OnTurtleRemoved += OnTurtleRemoved;
            turtleManager.OnTurtlesUpdated += OnTurtlesUpdated;
        }
    }

    private void Update()
    {
        if (autoUpdate && Time.time - lastUpdateTime > updateInterval)
        {
            UpdateUI();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateUI()
    {
        UpdateStatusText();
        UpdateTurtleList();
        UpdateTaskButtonStates();
    }

    private void UpdateStatusText()
    {
        if (turtleManager != null)
        {
            int total = turtleManager.GetTurtleCount();
            int available = turtleManager.GetAvailableTurtleCount();

            if (totalTurtlesText != null)
                totalTurtlesText.text = $"Total: {total}";

            if (availableTurtlesText != null)
                availableTurtlesText.text = $"Available: {available}";
        }

        if (selectionManager != null)
        {
            int selected = selectionManager.GetSelectionCount();
            if (selectionCountText != null)
                selectionCountText.text = $"Selected: {selected}";
        }
    }

    private void UpdateTurtleList()
    {
        if (!showTurtleList || turtleListContent == null || turtleManager == null)
            return;

        var allTurtles = turtleManager.GetAllTurtles();

        // Update existing items and create new ones
        foreach (var turtle in allTurtles)
        {
            if (!turtleListItems.ContainsKey(turtle.turtleId))
            {
                CreateTurtleListItem(turtle);
            }
            else
            {
                UpdateTurtleListItem(turtle);
            }
        }

        // Remove items for turtles that no longer exist
        var removedTurtles = turtleListItems.Keys
            .Where(id => !allTurtles.Any(t => t.turtleId == id))
            .ToList();

        foreach (var id in removedTurtles)
        {
            if (turtleListItems.TryGetValue(id, out GameObject item))
            {
                Destroy(item);
                turtleListItems.Remove(id);
            }
        }
    }

    private void CreateTurtleListItem(TurtleObject turtle)
    {
        if (turtleListItemPrefab == null)
        {
            CreateDefaultTurtleListItem(turtle);
            return;
        }

        GameObject item = Instantiate(turtleListItemPrefab, turtleListContent);
        item.name = $"TurtleItem_{turtle.turtleId}";

        // Setup click handler
        Button button = item.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => selectionManager?.SelectTurtleById(turtle.turtleId));
        }

        turtleListItems[turtle.turtleId] = item;
        UpdateTurtleListItem(turtle);
    }

    private void CreateDefaultTurtleListItem(TurtleObject turtle)
    {
        GameObject item = new GameObject($"TurtleItem_{turtle.turtleId}");
        item.transform.SetParent(turtleListContent, false);

        // Add layout element
        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.minHeight = 40;

        // Add button
        Button button = item.AddComponent<Button>();
        button.onClick.AddListener(() => selectionManager?.SelectTurtleById(turtle.turtleId));

        // Add image for button
        Image image = item.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(item.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.margin = new Vector4(10, 0, 10, 0);
        text.fontSize = 14;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        turtleListItems[turtle.turtleId] = item;
        UpdateTurtleListItem(turtle);
    }

    private void UpdateTurtleListItem(TurtleObject turtle)
    {
        if (!turtleListItems.TryGetValue(turtle.turtleId, out GameObject item))
            return;

        // Update text
        TextMeshProUGUI text = item.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            string status = turtle.isBusy ? $"[{turtle.currentOperation}]" : "[Idle]";
            string selected = turtle.isSelected ? "✓ " : "";
            text.text = $"{selected}{turtle.turtleName} {status}";
        }

        // Update color based on state
        Image image = item.GetComponent<Image>();
        if (image != null)
        {
            if (turtle.isSelected)
                image.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            else if (turtle.isBusy)
                image.color = new Color(1f, 0.5f, 0.2f, 0.8f);
            else
                image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        }
    }

    private void UpdateTaskButtonStates()
    {
        bool hasSelection = selectionManager != null && selectionManager.HasSelection();

        if (assignMiningButton != null)
            assignMiningButton.interactable = hasSelection;

        if (assignBuildingButton != null)
            assignBuildingButton.interactable = hasSelection;

        if (assignMoveButton != null)
            assignMoveButton.interactable = hasSelection;
    }

    private void OnSelectionChanged(List<TurtleObject> selected)
    {
        UpdateUI();
    }

    private void OnTurtleAdded(TurtleObject turtle)
    {
        UpdateUI();
    }

    private void OnTurtleRemoved(TurtleObject turtle)
    {
        if (turtleListItems.TryGetValue(turtle.turtleId, out GameObject item))
        {
            Destroy(item);
            turtleListItems.Remove(turtle.turtleId);
        }
        UpdateUI();
    }

    private void OnTurtlesUpdated()
    {
        UpdateUI();
    }

    public void ShowSelectionPanel(bool show)
    {
        if (selectionPanel != null)
            selectionPanel.SetActive(show);
    }

    public void ShowTurtleListPanel(bool show)
    {
        showTurtleList = show;
        if (turtleListPanel != null)
            turtleListPanel.SetActive(show);
    }

    public void ShowTaskAssignmentPanel(bool show)
    {
        if (taskAssignmentPanel != null)
            taskAssignmentPanel.SetActive(show);
    }

    private void OnDestroy()
    {
        // Unregister events
        if (selectionManager != null)
        {
            selectionManager.OnSelectionChanged -= OnSelectionChanged;
        }

        if (turtleManager != null)
        {
            turtleManager.OnTurtleAdded -= OnTurtleAdded;
            turtleManager.OnTurtleRemoved -= OnTurtleRemoved;
            turtleManager.OnTurtlesUpdated -= OnTurtlesUpdated;
        }

        // Cleanup list items
        foreach (var item in turtleListItems.Values)
        {
            if (item != null) Destroy(item);
        }
        turtleListItems.Clear();
    }
}
