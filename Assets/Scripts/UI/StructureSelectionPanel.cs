using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// UI panel for selecting structures to build in the main game
/// Integrates with the build menu and structure manager
/// </summary>
public class StructureSelectionPanel : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform structureListContent;
    public GameObject structureItemPrefab;
    public TMP_InputField searchField;
    public TMP_Dropdown categoryFilter;
    public TextMeshProUGUI headerText;
    public Button closeButton;
    public Button refreshButton;
    public Button openEditorButton;

    [Header("Preview")]
    public RawImage previewImage;
    public TextMeshProUGUI previewNameText;
    public TextMeshProUGUI previewDescriptionText;
    public TextMeshProUGUI previewBlockCountText;
    public TextMeshProUGUI previewSizeText;
    public Button selectButton;

    [Header("References")]
    public StructureManager structureManager;
    public BuildModeManager buildModeManager;

    private List<StructureData> displayedStructures = new List<StructureData>();
    private Dictionary<string, GameObject> structureItems = new Dictionary<string, GameObject>();
    private StructureData selectedStructure;
    private string currentSearchTerm = "";
    private string currentCategory = "All";

    private void Start()
    {
        InitializeReferences();
        SetupUI();
        LoadStructures();
    }

    private void InitializeReferences()
    {
        if (structureManager == null)
            structureManager = StructureManager.Instance;

        if (buildModeManager == null)
            buildModeManager = FindFirstObjectByType<BuildModeManager>();
    }

    private void SetupUI()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(LoadStructures);

        if (selectButton != null)
            selectButton.onClick.AddListener(SelectCurrentStructure);

        if (openEditorButton != null)
            openEditorButton.onClick.AddListener(OpenStructureEditor);

        if (searchField != null)
            searchField.onValueChanged.AddListener(OnSearchChanged);

        if (categoryFilter != null)
        {
            categoryFilter.onValueChanged.AddListener(OnCategoryChanged);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        LoadStructures();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void LoadStructures()
    {
        if (structureManager == null)
        {
            Debug.LogWarning("StructureManager not found");
            return;
        }

        // Reload from disk
        structureManager.LoadAllStructures();

        // Update category dropdown
        UpdateCategoryDropdown();

        // Display structures
        RefreshStructureList();
    }

    private void UpdateCategoryDropdown()
    {
        if (categoryFilter == null || structureManager == null) return;

        List<string> categories = new List<string> { "All" };
        categories.AddRange(structureManager.GetAllCategories());

        categoryFilter.ClearOptions();
        categoryFilter.AddOptions(categories);
    }

    private void RefreshStructureList()
    {
        // Clear existing items
        foreach (var item in structureItems.Values)
        {
            if (item != null)
                Destroy(item);
        }
        structureItems.Clear();

        if (structureManager == null || structureListContent == null) return;

        // Filter structures
        displayedStructures = FilterStructures();

        // Create list items
        foreach (var structure in displayedStructures)
        {
            GameObject itemObj = CreateStructureListItem(structure);
            structureItems[structure.name] = itemObj;
        }

        if (headerText != null)
        {
            headerText.text = $"Structures ({displayedStructures.Count})";
        }
    }

    private List<StructureData> FilterStructures()
    {
        if (structureManager == null)
            return new List<StructureData>();

        List<StructureData> filtered = structureManager.loadedStructures;

        // Category filter
        if (currentCategory != "All")
        {
            filtered = filtered.Where(s => s.category == currentCategory).ToList();
        }

        // Search filter
        if (!string.IsNullOrEmpty(currentSearchTerm))
        {
            string search = currentSearchTerm.ToLower();
            filtered = filtered.Where(s =>
                s.name.ToLower().Contains(search) ||
                s.description.ToLower().Contains(search) ||
                s.category.ToLower().Contains(search)
            ).ToList();
        }

        return filtered.OrderBy(s => s.name).ToList();
    }

    private GameObject CreateStructureListItem(StructureData structure)
    {
        GameObject itemObj;

        if (structureItemPrefab != null)
        {
            itemObj = Instantiate(structureItemPrefab, structureListContent);
        }
        else
        {
            itemObj = CreateDefaultListItem(structure);
        }

        // Set up click handler
        Button button = itemObj.GetComponent<Button>();
        if (button == null)
            button = itemObj.AddComponent<Button>();

        button.onClick.AddListener(() => OnStructureClicked(structure));

        // Update display
        UpdateStructureListItem(itemObj, structure);

        return itemObj;
    }

    private GameObject CreateDefaultListItem(StructureData structure)
    {
        GameObject item = new GameObject($"StructureItem_{structure.name}");
        item.transform.SetParent(structureListContent, false);

        // Layout
        LayoutElement layout = item.AddComponent<LayoutElement>();
        layout.minHeight = 60;
        layout.preferredHeight = 60;

        // Background
        Image bg = item.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        // Content container
        GameObject content = new GameObject("Content");
        content.transform.SetParent(item.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(10, 5);
        contentRect.offsetMax = new Vector2(-10, -5);

        // Name text
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(content.transform, false);

        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyles.Bold;

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.5f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0, 1);
        nameRect.anchoredPosition = Vector2.zero;

        // Info text
        GameObject infoObj = new GameObject("Info");
        infoObj.transform.SetParent(content.transform, false);

        TextMeshProUGUI infoText = infoObj.AddComponent<TextMeshProUGUI>();
        infoText.fontSize = 12;
        infoText.color = new Color(0.7f, 0.7f, 0.7f);

        RectTransform infoRect = infoObj.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0, 0);
        infoRect.anchorMax = new Vector2(1, 0.5f);
        infoRect.pivot = new Vector2(0, 0);
        infoRect.anchoredPosition = Vector2.zero;

        return item;
    }

    private void UpdateStructureListItem(GameObject itemObj, StructureData structure)
    {
        TextMeshProUGUI[] texts = itemObj.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length >= 1)
        {
            texts[0].text = structure.name;
        }

        if (texts.Length >= 2)
        {
            Vector3Int size = structure.GetSize();
            texts[1].text = $"{structure.category} | {structure.blockCount} blocks | {size.x}x{size.y}x{size.z}";
        }

        // Highlight if selected
        Image bg = itemObj.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = (selectedStructure == structure)
                ? new Color(0.2f, 0.4f, 0.6f, 0.95f)
                : new Color(0.15f, 0.15f, 0.15f, 0.95f);
        }
    }

    private void OnStructureClicked(StructureData structure)
    {
        selectedStructure = structure;
        UpdatePreview(structure);
        RefreshStructureList(); // Update highlights
    }

    private void UpdatePreview(StructureData structure)
    {
        if (structure == null) return;

        if (previewNameText != null)
            previewNameText.text = structure.name;

        if (previewDescriptionText != null)
            previewDescriptionText.text = string.IsNullOrEmpty(structure.description)
                ? "No description"
                : structure.description;

        if (previewBlockCountText != null)
            previewBlockCountText.text = $"Blocks: {structure.blockCount}";

        if (previewSizeText != null)
        {
            Vector3Int size = structure.GetSize();
            previewSizeText.text = $"Size: {size.x} x {size.y} x {size.z}";
        }

        // TODO: Generate preview image
        if (previewImage != null)
        {
            // Could render a 3D preview of the structure
        }

        if (selectButton != null)
            selectButton.interactable = true;
    }

    private void SelectCurrentStructure()
    {
        if (selectedStructure == null)
        {
            Debug.LogWarning("No structure selected");
            return;
        }

        if (buildModeManager != null)
        {
            buildModeManager.SetStructureToBuild(selectedStructure);
            Debug.Log($"Selected structure for building: {selectedStructure.name}");
            Hide();
        }
        else
        {
            Debug.LogError("BuildModeManager not found");
        }
    }

    private void OpenStructureEditor()
    {
        // TODO: Load structure editor scene
        Debug.Log("Opening structure editor...");
        UnityEngine.SceneManagement.SceneManager.LoadScene("StructureEditor");
    }

    private void OnSearchChanged(string searchText)
    {
        currentSearchTerm = searchText;
        RefreshStructureList();
    }

    private void OnCategoryChanged(int index)
    {
        if (categoryFilter != null && categoryFilter.options.Count > index)
        {
            currentCategory = categoryFilter.options[index].text;
            RefreshStructureList();
        }
    }

    private void OnDestroy()
    {
        foreach (var item in structureItems.Values)
        {
            if (item != null)
                Destroy(item);
        }
        structureItems.Clear();
    }
}
