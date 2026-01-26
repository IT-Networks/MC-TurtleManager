using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

/// <summary>
/// UI for selecting block types in the structure editor
/// Displays a palette of available Minecraft blocks
/// </summary>
public class BlockPaletteUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform paletteContent;
    public GameObject blockButtonPrefab;
    public TMP_InputField searchField;
    public Toggle favoritesOnlyToggle;

    [Header("Categories")]
    public Transform categoryTabsContainer;
    public GameObject categoryTabPrefab;

    [Header("Settings")]
    public int buttonsPerRow = 4;
    public float buttonSize = 80f;
    public bool showBlockNames = true;
    public bool showBlockIcons = true;

    [Header("Block Data")]
    public List<BlockDefinition> availableBlocks = new List<BlockDefinition>();

    public event Action<string> OnBlockSelected;

    private string selectedBlockType;
    private string currentCategory = "All";
    private HashSet<string> favoriteBlocks = new HashSet<string>();
    private Dictionary<string, GameObject> blockButtons = new Dictionary<string, GameObject>();

    private void Start()
    {
        InitializeDefaultBlocks();
        SetupUI();
        PopulatePalette();
    }

    private void InitializeDefaultBlocks()
    {
        if (availableBlocks.Count == 0)
        {
            // Building Blocks
            availableBlocks.Add(new BlockDefinition("minecraft:stone", "Stone", "Building", Color.gray));
            availableBlocks.Add(new BlockDefinition("minecraft:cobblestone", "Cobblestone", "Building", new Color(0.4f, 0.4f, 0.4f)));
            availableBlocks.Add(new BlockDefinition("minecraft:stone_bricks", "Stone Bricks", "Building", new Color(0.5f, 0.5f, 0.5f)));
            availableBlocks.Add(new BlockDefinition("minecraft:granite", "Granite", "Building", new Color(0.6f, 0.4f, 0.3f)));
            availableBlocks.Add(new BlockDefinition("minecraft:diorite", "Diorite", "Building", new Color(0.9f, 0.9f, 0.9f)));
            availableBlocks.Add(new BlockDefinition("minecraft:andesite", "Andesite", "Building", new Color(0.5f, 0.5f, 0.5f)));

            // Wood
            availableBlocks.Add(new BlockDefinition("minecraft:oak_planks", "Oak Planks", "Wood", new Color(0.7f, 0.5f, 0.3f)));
            availableBlocks.Add(new BlockDefinition("minecraft:spruce_planks", "Spruce Planks", "Wood", new Color(0.5f, 0.35f, 0.2f)));
            availableBlocks.Add(new BlockDefinition("minecraft:birch_planks", "Birch Planks", "Wood", new Color(0.8f, 0.7f, 0.5f)));
            availableBlocks.Add(new BlockDefinition("minecraft:oak_log", "Oak Log", "Wood", new Color(0.6f, 0.4f, 0.2f)));

            // Decorative
            availableBlocks.Add(new BlockDefinition("minecraft:bricks", "Bricks", "Decorative", new Color(0.7f, 0.3f, 0.2f)));
            availableBlocks.Add(new BlockDefinition("minecraft:glass", "Glass", "Decorative", new Color(0.8f, 0.9f, 1f, 0.5f)));
            availableBlocks.Add(new BlockDefinition("minecraft:white_wool", "White Wool", "Decorative", Color.white));
            availableBlocks.Add(new BlockDefinition("minecraft:quartz_block", "Quartz", "Decorative", new Color(0.95f, 0.95f, 0.95f)));

            // Natural
            availableBlocks.Add(new BlockDefinition("minecraft:dirt", "Dirt", "Natural", new Color(0.6f, 0.4f, 0.2f)));
            availableBlocks.Add(new BlockDefinition("minecraft:grass_block", "Grass Block", "Natural", new Color(0.3f, 0.7f, 0.3f)));
            availableBlocks.Add(new BlockDefinition("minecraft:sand", "Sand", "Natural", new Color(0.9f, 0.9f, 0.6f)));
            availableBlocks.Add(new BlockDefinition("minecraft:sandstone", "Sandstone", "Natural", new Color(0.85f, 0.8f, 0.6f)));

            // Special
            availableBlocks.Add(new BlockDefinition("minecraft:glowstone", "Glowstone", "Special", new Color(1f, 0.9f, 0.6f)));
            availableBlocks.Add(new BlockDefinition("minecraft:obsidian", "Obsidian", "Special", new Color(0.1f, 0.05f, 0.2f)));
            availableBlocks.Add(new BlockDefinition("minecraft:gold_block", "Gold Block", "Special", new Color(1f, 0.84f, 0f)));
            availableBlocks.Add(new BlockDefinition("minecraft:iron_block", "Iron Block", "Special", new Color(0.85f, 0.85f, 0.85f)));
            availableBlocks.Add(new BlockDefinition("minecraft:diamond_block", "Diamond Block", "Special", new Color(0.3f, 0.9f, 0.9f)));
        }
    }

    private void SetupUI()
    {
        if (searchField != null)
        {
            searchField.onValueChanged.AddListener(OnSearchChanged);
        }

        if (favoritesOnlyToggle != null)
        {
            favoritesOnlyToggle.onValueChanged.AddListener(OnFavoritesToggled);
        }

        CreateCategoryTabs();
    }

    private void CreateCategoryTabs()
    {
        if (categoryTabsContainer == null) return;

        // Get unique categories
        HashSet<string> categories = new HashSet<string> { "All" };
        foreach (var block in availableBlocks)
        {
            categories.Add(block.category);
        }

        // Create tab buttons
        foreach (var category in categories)
        {
            GameObject tabObj = categoryTabPrefab != null
                ? Instantiate(categoryTabPrefab, categoryTabsContainer)
                : CreateDefaultTab(category);

            Button button = tabObj.GetComponent<Button>();
            TextMeshProUGUI text = tabObj.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
                text.text = category;

            string cat = category; // Capture for lambda
            if (button != null)
            {
                button.onClick.AddListener(() => SelectCategory(cat));
            }
        }
    }

    private GameObject CreateDefaultTab(string categoryName)
    {
        GameObject tab = new GameObject($"Tab_{categoryName}");
        tab.transform.SetParent(categoryTabsContainer, false);

        Image img = tab.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f);

        Button button = tab.AddComponent<Button>();

        RectTransform rect = tab.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100, 40);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(tab.transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = categoryName;
        text.fontSize = 14;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return tab;
    }

    private void PopulatePalette()
    {
        // Clear existing buttons
        foreach (var button in blockButtons.Values)
        {
            if (button != null)
                Destroy(button);
        }
        blockButtons.Clear();

        if (paletteContent == null) return;

        // Filter blocks
        List<BlockDefinition> filteredBlocks = FilterBlocks();

        // Create buttons
        foreach (var block in filteredBlocks)
        {
            GameObject buttonObj = CreateBlockButton(block);
            blockButtons[block.id] = buttonObj;
        }
    }

    private List<BlockDefinition> FilterBlocks()
    {
        List<BlockDefinition> filtered = new List<BlockDefinition>();

        string searchTerm = searchField != null ? searchField.text.ToLower() : "";
        bool favoritesOnly = favoritesOnlyToggle != null && favoritesOnlyToggle.isOn;

        foreach (var block in availableBlocks)
        {
            // Category filter
            if (currentCategory != "All" && block.category != currentCategory)
                continue;

            // Favorites filter
            if (favoritesOnly && !favoriteBlocks.Contains(block.id))
                continue;

            // Search filter
            if (!string.IsNullOrEmpty(searchTerm))
            {
                if (!block.displayName.ToLower().Contains(searchTerm) &&
                    !block.id.ToLower().Contains(searchTerm))
                    continue;
            }

            filtered.Add(block);
        }

        return filtered;
    }

    private GameObject CreateBlockButton(BlockDefinition block)
    {
        GameObject buttonObj;

        if (blockButtonPrefab != null)
        {
            buttonObj = Instantiate(blockButtonPrefab, paletteContent);
        }
        else
        {
            buttonObj = CreateDefaultBlockButton(block);
        }

        // Set up button functionality
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => SelectBlock(block.id));
        }

        // Set visual appearance
        Image buttonImage = buttonObj.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = block.color;
        }

        // Set text
        TextMeshProUGUI text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null && showBlockNames)
        {
            text.text = block.displayName;
        }

        return buttonObj;
    }

    private GameObject CreateDefaultBlockButton(BlockDefinition block)
    {
        GameObject button = new GameObject($"BlockButton_{block.id}");
        button.transform.SetParent(paletteContent, false);

        Image img = button.AddComponent<Image>();
        img.color = block.color;

        Button btn = button.AddComponent<Button>();

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(buttonSize, buttonSize);

        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(button.transform, false);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = block.displayName;
        text.fontSize = 10;
        text.alignment = TextAlignmentOptions.Bottom;
        text.color = Color.white;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5, 5);
        textRect.offsetMax = new Vector2(-5, -5);

        // Add tooltip on hover
        BlockTooltip tooltip = button.AddComponent<BlockTooltip>();
        tooltip.blockInfo = $"{block.displayName}\n{block.id}";

        return button;
    }

    private void SelectBlock(string blockId)
    {
        selectedBlockType = blockId;
        OnBlockSelected?.Invoke(blockId);

        Debug.Log($"Selected block: {blockId}");

        // Highlight selected button
        UpdateButtonHighlights();
    }

    private void SelectCategory(string category)
    {
        currentCategory = category;
        PopulatePalette();

        Debug.Log($"Selected category: {category}");
    }

    private void UpdateButtonHighlights()
    {
        foreach (var kvp in blockButtons)
        {
            Button button = kvp.Value.GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = kvp.Key == selectedBlockType
                    ? new Color(0.5f, 0.8f, 1f)
                    : Color.white;
                button.colors = colors;
            }
        }
    }

    private void OnSearchChanged(string searchText)
    {
        PopulatePalette();
    }

    private void OnFavoritesToggled(bool isOn)
    {
        PopulatePalette();
    }

    public void ToggleFavorite(string blockId)
    {
        if (favoriteBlocks.Contains(blockId))
            favoriteBlocks.Remove(blockId);
        else
            favoriteBlocks.Add(blockId);

        // TODO: Save favorites to PlayerPrefs
    }

    public BlockDefinition GetBlockDefinition(string blockId)
    {
        return availableBlocks.Find(b => b.id == blockId);
    }
}

/// <summary>
/// Definition of a block type for the palette
/// </summary>
[Serializable]
public class BlockDefinition
{
    public string id;
    public string displayName;
    public string category;
    public Color color;
    public Sprite icon;

    public BlockDefinition(string id, string displayName, string category, Color color)
    {
        this.id = id;
        this.displayName = displayName;
        this.category = category;
        this.color = color;
    }
}

/// <summary>
/// Simple tooltip component for block buttons
/// </summary>
public class BlockTooltip : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public string blockInfo;
    private GameObject tooltipObject;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        // TODO: Show tooltip
        Debug.Log($"Hover: {blockInfo}");
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        // TODO: Hide tooltip
    }
}
