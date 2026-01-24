using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Anno-style circular context menu for mining and building operations
/// Appears on right-click and provides intuitive action selection
/// </summary>
public class AnnoStyleContextMenu : MonoBehaviour
{
    [Header("Menu Elements")]
    public GameObject menuPanel;
    public Button miningButton;
    public Button buildingButton;
    public Button moveButton;
    public Button cancelButton;

    [Header("Visual Settings")]
    public float buttonRadius = 80f;
    public float buttonSize = 60f;
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    public Color highlightColor = new Color(0.3f, 0.5f, 0.8f, 0.9f);
    public Color miningColor = new Color(0.8f, 0.3f, 0.2f, 0.9f);
    public Color buildingColor = new Color(0.2f, 0.8f, 0.3f, 0.9f);

    [Header("Icons")]
    public Sprite miningIcon;
    public Sprite buildingIcon;
    public Sprite moveIcon;
    public Sprite cancelIcon;

    private ModernUIManager uiManager;
    private Vector3 worldPosition;
    private AreaSelectionManager areaManager;
    private bool isOpen;

    public void Initialize(ModernUIManager manager)
    {
        uiManager = manager;
        areaManager = FindFirstObjectByType<AreaSelectionManager>();
        SetupButtons();
        Hide();
    }

    private void SetupButtons()
    {
        if (miningButton != null)
        {
            miningButton.onClick.AddListener(OnMiningButtonClicked);
            SetButtonIcon(miningButton, miningIcon);
            SetButtonColor(miningButton, miningColor);
        }

        if (buildingButton != null)
        {
            buildingButton.onClick.AddListener(OnBuildingButtonClicked);
            SetButtonIcon(buildingButton, buildingIcon);
            SetButtonColor(buildingButton, buildingColor);
        }

        if (moveButton != null)
        {
            moveButton.onClick.AddListener(OnMoveButtonClicked);
            SetButtonIcon(moveButton, moveIcon);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Hide);
            SetButtonIcon(cancelButton, cancelIcon);
        }

        ArrangeButtonsInCircle();
    }

    private void ArrangeButtonsInCircle()
    {
        List<Button> buttons = new List<Button>();
        if (miningButton != null) buttons.Add(miningButton);
        if (buildingButton != null) buttons.Add(buildingButton);
        if (moveButton != null) buttons.Add(moveButton);
        if (cancelButton != null) buttons.Add(cancelButton);

        int count = buttons.Count;
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep - 90f; // Start from top
            float rad = angle * Mathf.Deg2Rad;

            RectTransform rect = buttons[i].GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(
                    Mathf.Cos(rad) * buttonRadius,
                    Mathf.Sin(rad) * buttonRadius
                );
                rect.sizeDelta = new Vector2(buttonSize, buttonSize);
            }
        }
    }

    private void SetButtonIcon(Button button, Sprite icon)
    {
        if (button == null || icon == null) return;

        Image img = button.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = icon;
        }
    }

    private void SetButtonColor(Button button, Color color)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.3f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        button.colors = colors;
    }

    public void Show(Vector3 position)
    {
        worldPosition = position;
        isOpen = true;

        if (menuPanel != null)
        {
            menuPanel.SetActive(true);

            // Position at mouse cursor
            RectTransform rect = menuPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.position = Input.mousePosition;
            }
        }
    }

    public void Hide()
    {
        isOpen = false;
        if (menuPanel != null)
            menuPanel.SetActive(false);
    }

    private void OnMiningButtonClicked()
    {
        Debug.Log("Mining mode selected");

        if (areaManager != null)
        {
            areaManager.ToggleMode(AreaSelectionManager.SelectionMode.Mining);
        }

        Hide();
    }

    private void OnBuildingButtonClicked()
    {
        Debug.Log("Building mode selected - opening structure selection");

        // Open structure selection panel
        if (uiManager != null)
        {
            uiManager.ShowStructureSelection();
        }
        else
        {
            // Fallback - activate building mode directly
            if (areaManager != null)
            {
                areaManager.ToggleMode(AreaSelectionManager.SelectionMode.Building);
            }
        }

        Hide();
    }

    private void OnMoveButtonClicked()
    {
        Debug.Log("Move mode selected");
        // TODO: Implement turtle movement to position
        Hide();
    }

    private void Update()
    {
        // Close menu on ESC or click outside
        if (isOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
            {
                // Check if click is outside menu
                if (Input.GetMouseButtonDown(0) && !IsMouseOverMenu())
                {
                    Hide();
                }
            }
        }
    }

    private bool IsMouseOverMenu()
    {
        if (menuPanel == null || !menuPanel.activeSelf)
            return false;

        RectTransform rect = menuPanel.GetComponent<RectTransform>();
        if (rect == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition);
    }
}
