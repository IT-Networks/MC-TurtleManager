using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// AI Prompt Panel - UI for entering natural language commands for AI structure generation
/// Allows users to type prompts like "Build a house" or "Create a mechanical workshop"
/// </summary>
public class AIPromptPanel : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot;
    public TMP_InputField promptInputField;
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI examplesText;

    [Header("Buttons")]
    public Button generateButton;
    public Button buildButton;
    public Button saveButton;
    public Button closeButton;
    public Button clearButton;

    [Header("Settings")]
    public Toggle autoSaveToggle;
    public Toggle autoBuildToggle;

    [Header("AI Integration")]
    public LMStudioManager lmStudioManager;
    public StructureManager structureManager;
    public TurtleBuildingManager buildingManager;
    public TurtleObject selectedTurtle;

    private StructureData lastGeneratedStructure;
    private bool isGenerating = false;

    void Start()
    {
        SetupEventListeners();
        UpdateUIState();

        // Subscribe to AI events
        if (lmStudioManager != null)
        {
            lmStudioManager.OnStructureGenerated += OnStructureGenerated;
            lmStudioManager.OnStructureGenerationFailed += OnStructureGenerationFailed;
        }

        // Hide panel initially
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (lmStudioManager != null)
        {
            lmStudioManager.OnStructureGenerated -= OnStructureGenerated;
            lmStudioManager.OnStructureGenerationFailed -= OnStructureGenerationFailed;
        }
    }

    void Update()
    {
        // Toggle panel with I key (I for "Instructions" or "AI")
        if (Input.GetKeyDown(KeyCode.I))
        {
            TogglePanel();
        }

        // Submit prompt with Enter (if input field is focused)
        if (panelRoot.activeSelf && Input.GetKeyDown(KeyCode.Return) && !isGenerating)
        {
            OnGenerateClicked();
        }
    }

    private void SetupEventListeners()
    {
        if (generateButton != null)
            generateButton.onClick.AddListener(OnGenerateClicked);

        if (buildButton != null)
            buildButton.onClick.AddListener(OnBuildClicked);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);

        if (clearButton != null)
            clearButton.onClick.AddListener(OnClearClicked);

        if (autoSaveToggle != null)
            autoSaveToggle.onValueChanged.AddListener(OnAutoSaveToggled);

        if (autoBuildToggle != null)
            autoBuildToggle.onValueChanged.AddListener(OnAutoBuildToggled);
    }

    // === UI Actions ===

    private void OnGenerateClicked()
    {
        if (isGenerating)
        {
            UpdateStatus("Already generating structure, please wait...", Color.yellow);
            return;
        }

        if (promptInputField == null || string.IsNullOrWhiteSpace(promptInputField.text))
        {
            UpdateStatus("Please enter a prompt", Color.red);
            return;
        }

        if (lmStudioManager == null)
        {
            UpdateStatus("LM Studio Manager not found", Color.red);
            return;
        }

        string prompt = promptInputField.text.Trim();
        UpdateStatus($"Generating structure: \"{prompt}\"...", Color.cyan);

        isGenerating = true;
        lmStudioManager.GenerateStructureFromPrompt(prompt);

        // Disable generate button while processing
        if (generateButton != null)
            generateButton.interactable = false;
    }

    private void OnBuildClicked()
    {
        if (lastGeneratedStructure == null)
        {
            UpdateStatus("No structure to build. Generate one first.", Color.red);
            return;
        }

        if (selectedTurtle == null)
        {
            UpdateStatus("No turtle selected. Select a turtle first.", Color.red);
            return;
        }

        if (buildingManager == null)
        {
            UpdateStatus("Building Manager not found", Color.red);
            return;
        }

        Vector3 buildPosition = selectedTurtle.transform.position;
        buildingManager.BuildStructureAtPosition(lastGeneratedStructure, buildPosition);

        UpdateStatus($"Building '{lastGeneratedStructure.name}' at {buildPosition}", Color.green);
    }

    private void OnSaveClicked()
    {
        if (lastGeneratedStructure == null)
        {
            UpdateStatus("No structure to save. Generate one first.", Color.red);
            return;
        }

        if (structureManager == null)
        {
            UpdateStatus("Structure Manager not found", Color.red);
            return;
        }

        structureManager.SaveStructure(lastGeneratedStructure);
        UpdateStatus($"Saved structure: {lastGeneratedStructure.name}", Color.green);
    }

    private void OnClearClicked()
    {
        if (promptInputField != null)
            promptInputField.text = "";

        lastGeneratedStructure = null;
        UpdateStatus("Ready for new prompt", Color.white);
        UpdateUIState();
    }

    private void OnCloseClicked()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnAutoSaveToggled(bool enabled)
    {
        if (lmStudioManager != null)
            lmStudioManager.autoSaveGeneratedStructures = enabled;
    }

    private void OnAutoBuildToggled(bool enabled)
    {
        if (lmStudioManager != null)
            lmStudioManager.autoBuildGeneratedStructures = enabled;
    }

    // === AI Callbacks ===

    private void OnStructureGenerated(StructureData structure)
    {
        lastGeneratedStructure = structure;
        isGenerating = false;

        UpdateStatus($"✓ Generated: {structure.name} ({structure.blockCount} blocks)", Color.green);
        UpdateUIState();

        // Re-enable generate button
        if (generateButton != null)
            generateButton.interactable = true;
    }

    private void OnStructureGenerationFailed(string error)
    {
        isGenerating = false;
        UpdateStatus($"✗ Generation failed: {error}", Color.red);

        // Re-enable generate button
        if (generateButton != null)
            generateButton.interactable = true;
    }

    // === Helper Methods ===

    public void TogglePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(!panelRoot.activeSelf);

            // Focus input field when opening
            if (panelRoot.activeSelf && promptInputField != null)
            {
                promptInputField.Select();
                promptInputField.ActivateInputField();
            }
        }
    }

    public void ShowPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            if (promptInputField != null)
            {
                promptInputField.Select();
                promptInputField.ActivateInputField();
            }
        }
    }

    public void HidePanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }

        Debug.Log($"[AIPromptPanel] {message}");
    }

    private void UpdateUIState()
    {
        // Enable/disable buttons based on state
        if (buildButton != null)
            buildButton.interactable = (lastGeneratedStructure != null && selectedTurtle != null);

        if (saveButton != null)
            saveButton.interactable = (lastGeneratedStructure != null);

        if (generateButton != null)
            generateButton.interactable = !isGenerating;

        // Update header with structure info
        if (lastGeneratedStructure != null && headerText != null)
        {
            headerText.text = $"AI Structure Generator - Last: {lastGeneratedStructure.name}";
        }
    }

    public void SetSelectedTurtle(TurtleObject turtle)
    {
        selectedTurtle = turtle;
        UpdateUIState();
    }

    /// <summary>
    /// Set example prompts text
    /// </summary>
    public void SetExamplePrompts()
    {
        if (examplesText != null)
        {
            examplesText.text = @"Example Prompts:
• Build a 5x5 house with door and windows
• Create a mechanical workshop with gears
• Design a small castle tower
• Make a modern concrete building
• Build a Create mod conveyor belt system
• Create a pipe network with storage
• Design a cozy cottage with fireplace
• Build a watchtower with ladder";
        }
    }
}
