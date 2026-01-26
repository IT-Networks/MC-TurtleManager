using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Task queue panel for managing and assigning tasks to turtles
/// Shows all pending, active, and completed tasks
/// </summary>
public class TaskQueuePanel : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform taskListContent;
    public GameObject taskItemPrefab;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI pendingCountText;
    public TextMeshProUGUI activeCountText;
    public TextMeshProUGUI completedCountText;

    [Header("Filter")]
    public Toggle showPendingToggle;
    public Toggle showActiveToggle;
    public Toggle showCompletedToggle;

    [Header("Actions")]
    public Button clearCompletedButton;
    public Button cancelAllButton;

    [Header("Settings")]
    public int maxCompletedTasks = 10;
    public float updateInterval = 0.5f;

    private MultiTurtleManager turtleManager;
    private TurtleSelectionManager selectionManager;
    private List<TaskInfo> tasks = new List<TaskInfo>();
    private Dictionary<int, TaskQueueItem> taskItems = new Dictionary<int, TaskQueueItem>();
    private float lastUpdateTime;
    private int nextTaskId = 0;

    private bool showPending = true;
    private bool showActive = true;
    private bool showCompleted = true;

    public void Initialize(MultiTurtleManager manager, TurtleSelectionManager selection)
    {
        turtleManager = manager;
        selectionManager = selection;

        SetupButtons();
        SetupToggles();
    }

    private void SetupButtons()
    {
        if (clearCompletedButton != null)
            clearCompletedButton.onClick.AddListener(ClearCompletedTasks);

        if (cancelAllButton != null)
            cancelAllButton.onClick.AddListener(CancelAllTasks);
    }

    private void SetupToggles()
    {
        if (showPendingToggle != null)
        {
            showPendingToggle.isOn = showPending;
            showPendingToggle.onValueChanged.AddListener(value => { showPending = value; UpdateTaskList(); });
        }

        if (showActiveToggle != null)
        {
            showActiveToggle.isOn = showActive;
            showActiveToggle.onValueChanged.AddListener(value => { showActive = value; UpdateTaskList(); });
        }

        if (showCompletedToggle != null)
        {
            showCompletedToggle.isOn = showCompleted;
            showCompletedToggle.onValueChanged.AddListener(value => { showCompleted = value; UpdateTaskList(); });
        }
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateTaskList();
            UpdateHeaderInfo();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateHeaderInfo()
    {
        int pending = tasks.Count(t => t.status == TaskStatus.Pending);
        int active = tasks.Count(t => t.status == TaskStatus.Active);
        int completed = tasks.Count(t => t.status == TaskStatus.Completed);

        if (pendingCountText != null)
            pendingCountText.text = $"Pending: {pending}";

        if (activeCountText != null)
            activeCountText.text = $"Active: {active}";

        if (completedCountText != null)
            completedCountText.text = $"Completed: {completed}";

        if (headerText != null)
            headerText.text = $"Task Queue ({tasks.Count})";
    }

    private void UpdateTaskList()
    {
        if (taskListContent == null) return;

        var filteredTasks = FilterTasks();

        // Update or create items
        foreach (var task in filteredTasks)
        {
            if (!taskItems.ContainsKey(task.id))
            {
                CreateTaskItem(task);
            }
            else
            {
                taskItems[task.id].UpdateDisplay(task);
            }
        }

        // Remove items that don't match filter
        var itemsToRemove = taskItems.Keys
            .Where(id => !filteredTasks.Any(t => t.id == id))
            .ToList();

        foreach (var id in itemsToRemove)
        {
            if (taskItems.TryGetValue(id, out TaskQueueItem item))
            {
                Destroy(item.gameObject);
                taskItems.Remove(id);
            }
        }
    }

    private List<TaskInfo> FilterTasks()
    {
        return tasks.Where(t =>
            (showPending && t.status == TaskStatus.Pending) ||
            (showActive && t.status == TaskStatus.Active) ||
            (showCompleted && t.status == TaskStatus.Completed)
        ).ToList();
    }

    private void CreateTaskItem(TaskInfo task)
    {
        GameObject itemObj;

        if (taskItemPrefab != null)
        {
            itemObj = Instantiate(taskItemPrefab, taskListContent);
        }
        else
        {
            itemObj = CreateDefaultTaskItem();
        }

        TaskQueueItem item = itemObj.GetComponent<TaskQueueItem>();
        if (item == null)
        {
            item = itemObj.AddComponent<TaskQueueItem>();
        }

        item.Initialize(task, this, selectionManager);
        taskItems[task.id] = item;
    }

    private GameObject CreateDefaultTaskItem()
    {
        GameObject item = new GameObject("TaskItem");
        item.transform.SetParent(taskListContent, false);

        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.minHeight = 100;
        layout.preferredHeight = 100;

        Image bg = item.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        return item;
    }

    // Public API for task management
    public void AddMiningTask(List<Vector3> blocks)
    {
        TaskInfo task = new TaskInfo
        {
            id = nextTaskId++,
            type = TaskType.Mining,
            status = TaskStatus.Pending,
            blocks = new List<Vector3>(blocks),
            createdTime = Time.time
        };

        tasks.Add(task);
        Debug.Log($"Added mining task with {blocks.Count} blocks");
    }

    public void AddBuildingTask(Vector3 position, StructureData structure)
    {
        TaskInfo task = new TaskInfo
        {
            id = nextTaskId++,
            type = TaskType.Building,
            status = TaskStatus.Pending,
            position = position,
            structureName = structure?.name ?? "Unknown",
            createdTime = Time.time
        };

        tasks.Add(task);
        Debug.Log($"Added building task: {task.structureName}");
    }

    public void AssignTaskToTurtles(TaskInfo task, List<TurtleObject> turtles)
    {
        if (task == null || turtles == null || turtles.Count == 0)
            return;

        task.status = TaskStatus.Active;
        task.assignedTurtles = new List<int>(turtles.Select(t => t.turtleId));

        Debug.Log($"Assigned task {task.id} to {turtles.Count} turtle(s)");

        // TODO: Actually send task to turtles via TurtleSelectionManager
        if (selectionManager != null)
        {
            switch (task.type)
            {
                case TaskType.Mining:
                    selectionManager.AssignMiningTask(task.blocks);
                    break;
                case TaskType.Building:
                    // TODO: Implement building task assignment
                    break;
            }
        }
    }

    public void CancelTask(TaskInfo task)
    {
        if (task != null)
        {
            tasks.Remove(task);
            if (taskItems.TryGetValue(task.id, out TaskQueueItem item))
            {
                Destroy(item.gameObject);
                taskItems.Remove(task.id);
            }
        }
    }

    public void CompleteTask(TaskInfo task)
    {
        if (task != null)
        {
            task.status = TaskStatus.Completed;
            task.completedTime = Time.time;

            // Auto-cleanup old completed tasks
            CleanupOldCompletedTasks();
        }
    }

    private void ClearCompletedTasks()
    {
        var completed = tasks.Where(t => t.status == TaskStatus.Completed).ToList();
        foreach (var task in completed)
        {
            CancelTask(task);
        }
    }

    private void CancelAllTasks()
    {
        var tasksToCancel = new List<TaskInfo>(tasks);
        foreach (var task in tasksToCancel)
        {
            CancelTask(task);
        }
    }

    private void CleanupOldCompletedTasks()
    {
        var completedTasks = tasks.Where(t => t.status == TaskStatus.Completed)
                                  .OrderBy(t => t.completedTime)
                                  .ToList();

        while (completedTasks.Count > maxCompletedTasks)
        {
            var oldest = completedTasks[0];
            CancelTask(oldest);
            completedTasks.RemoveAt(0);
        }
    }
}

/// <summary>
/// Individual task item in the queue
/// </summary>
public class TaskQueueItem : MonoBehaviour
{
    private TaskInfo task;
    private TaskQueuePanel queuePanel;
    private TurtleSelectionManager selectionManager;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI detailsText;
    private TextMeshProUGUI statusText;
    private Button assignButton;
    private Button cancelButton;
    private Image background;

    public void Initialize(TaskInfo taskInfo, TaskQueuePanel panel, TurtleSelectionManager selection)
    {
        task = taskInfo;
        queuePanel = panel;
        selectionManager = selection;

        SetupUI();
        UpdateDisplay(task);
    }

    private void SetupUI()
    {
        // Create UI elements
        titleText = CreateText("TitleText", new Vector2(10, -10), new Vector2(250, 25));
        titleText.fontSize = 16;
        titleText.fontStyle = FontStyles.Bold;

        detailsText = CreateText("DetailsText", new Vector2(10, -35), new Vector2(250, 40));
        detailsText.fontSize = 12;

        statusText = CreateText("StatusText", new Vector2(10, -75), new Vector2(150, 20));
        statusText.fontSize = 11;

        assignButton = CreateButton("AssignButton", "Assign", new Vector2(-90, -15), new Vector2(80, 30));
        assignButton.onClick.AddListener(OnAssignTask);

        cancelButton = CreateButton("CancelButton", "Cancel", new Vector2(-90, -50), new Vector2(80, 25));
        cancelButton.onClick.AddListener(OnCancelTask);

        background = GetComponent<Image>();
    }

    private TextMeshProUGUI CreateText(string name, Vector2 position, Vector2 size)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();

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

    public void UpdateDisplay(TaskInfo taskInfo)
    {
        task = taskInfo;

        if (titleText != null)
        {
            titleText.text = $"Task #{task.id}: {task.type}";
        }

        if (detailsText != null)
        {
            string details = task.type switch
            {
                TaskType.Mining => $"Mining {task.blocks?.Count ?? 0} blocks",
                TaskType.Building => $"Building: {task.structureName}\nAt: {task.position}",
                TaskType.Moving => $"Move to: {task.position}",
                _ => "Unknown task"
            };
            detailsText.text = details;
        }

        if (statusText != null)
        {
            string statusStr = task.status.ToString();
            if (task.assignedTurtles != null && task.assignedTurtles.Count > 0)
            {
                statusStr += $" ({task.assignedTurtles.Count} turtle(s))";
            }
            statusText.text = $"Status: {statusStr}";

            statusText.color = task.status switch
            {
                TaskStatus.Pending => new Color(0.8f, 0.8f, 0.8f),
                TaskStatus.Active => new Color(1f, 0.7f, 0.2f),
                TaskStatus.Completed => new Color(0.2f, 1f, 0.3f),
                _ => Color.white
            };
        }

        // Update buttons
        if (assignButton != null)
        {
            assignButton.gameObject.SetActive(task.status == TaskStatus.Pending);
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(task.status != TaskStatus.Completed);
        }

        // Update background
        if (background != null)
        {
            background.color = task.status switch
            {
                TaskStatus.Pending => new Color(0.15f, 0.15f, 0.2f, 0.95f),
                TaskStatus.Active => new Color(0.2f, 0.15f, 0.1f, 0.95f),
                TaskStatus.Completed => new Color(0.1f, 0.2f, 0.15f, 0.95f),
                _ => new Color(0.15f, 0.15f, 0.15f, 0.95f)
            };
        }
    }

    private void OnAssignTask()
    {
        if (selectionManager == null || queuePanel == null) return;

        var selectedTurtles = selectionManager.GetSelectedTurtles();
        if (selectedTurtles.Count == 0)
        {
            Debug.LogWarning("No turtles selected. Select turtles first.");
            return;
        }

        queuePanel.AssignTaskToTurtles(task, selectedTurtles);
        UpdateDisplay(task);
    }

    private void OnCancelTask()
    {
        if (queuePanel != null)
        {
            queuePanel.CancelTask(task);
        }
    }
}

// Data structures
public enum TaskType { Mining, Building, Moving, Custom }
public enum TaskStatus { Pending, Active, Completed, Cancelled }

[System.Serializable]
public class TaskInfo
{
    public int id;
    public TaskType type;
    public TaskStatus status;
    public List<Vector3> blocks;
    public Vector3 position;
    public string structureName;
    public List<int> assignedTurtles;
    public float createdTime;
    public float completedTime;
}
