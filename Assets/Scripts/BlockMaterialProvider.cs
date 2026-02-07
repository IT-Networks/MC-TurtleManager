using UnityEngine;

/// <summary>
/// Provides appropriate materials for different block model types
/// Handles the difference between multi-texture cube shaders and single-texture materials
/// </summary>
public static class BlockMaterialProvider
{
    /// <summary>
    /// Gets the appropriate material for a block based on its model type
    ///
    /// - Cube blocks: Use TurtleWorldManager's multi-texture material system
    /// - Other blocks: Create single-texture material for correct rendering
    /// </summary>
    public static Material GetMaterialForBlock(string blockType, BlockModelType modelType, TurtleWorldManager worldManager)
    {
        if (worldManager == null)
            return CreateFallbackMaterial(blockType);

        // For cube blocks, use the existing multi-texture system
        if (modelType == BlockModelType.Cube)
        {
            Material cubeMaterial = worldManager.GetMaterialForBlock(blockType);
            if (cubeMaterial != null)
                return cubeMaterial;
        }

        // For all other block types, create a single-texture material
        // This is necessary because:
        // 1. The multi-texture shader expects 6 specific faces (cube)
        // 2. Slabs, stairs, fences, etc. have different geometry
        // 3. UVs need to map correctly to a single texture
        return CreateSingleTextureMaterial(blockType, modelType, worldManager);
    }

    /// <summary>
    /// Creates a material that uses a single main texture (not multi-texture)
    /// Suitable for slabs, stairs, fences, plants, etc.
    /// </summary>
    private static Material CreateSingleTextureMaterial(string blockType, BlockModelType modelType, TurtleWorldManager worldManager)
    {
        // Try to get the base material to extract texture
        Material baseMaterial = worldManager.GetMaterialForBlock(blockType);

        // Create new material with standard shader
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");

        Material mat = new Material(shader);

        // Extract main texture from base material
        if (baseMaterial != null && baseMaterial.mainTexture != null)
        {
            mat.mainTexture = baseMaterial.mainTexture;
        }
        else
        {
            // Try to load texture directly
            Texture2D texture = LoadSingleBlockTexture(blockType, worldManager);
            if (texture != null)
                mat.mainTexture = texture;
        }

        // Special settings for transparent blocks
        if (IsTransparentBlockType(modelType))
        {
            // Enable alpha cutout for plants
            if (modelType == BlockModelType.CrossPlant || modelType == BlockModelType.TintedCross)
            {
                mat.SetFloat("_Mode", 1); // Cutout
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 2450;
            }
            // Enable transparency for panes, liquid
            else if (modelType == BlockModelType.Pane || modelType == BlockModelType.Liquid)
            {
                mat.SetFloat("_Mode", 3); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
        }

        return mat;
    }

    /// <summary>
    /// Loads a single texture for a block type
    /// Tries to find the most appropriate texture (top, side, or base)
    /// </summary>
    private static Texture2D LoadSingleBlockTexture(string blockType, TurtleWorldManager worldManager)
    {
        string lower = blockType.ToLowerInvariant();
        string[] parts = lower.Split(':');
        string baseName = parts.Length > 1 ? parts[1] : parts[0];

        // Try different texture paths
        string[] paths = {
            $"BlockTextures/{baseName}",
            $"BlockTextures/{baseName}_top",
            $"BlockTextures/{baseName}_side",
            $"BlockTextures/{baseName}_front"
        };

        foreach (string path in paths)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null)
                return texture;
        }

        return null;
    }

    /// <summary>
    /// Checks if a block model type requires transparency
    /// </summary>
    private static bool IsTransparentBlockType(BlockModelType modelType)
    {
        switch (modelType)
        {
            case BlockModelType.CrossPlant:
            case BlockModelType.TintedCross:
            case BlockModelType.Pane:
            case BlockModelType.Liquid:
            case BlockModelType.Trapdoor:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Creates a simple fallback material when WorldManager is not available
    /// </summary>
    private static Material CreateFallbackMaterial(string blockType)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.color = GetFallbackColor(blockType);

        return mat;
    }

    /// <summary>
    /// Gets a fallback color for common block types
    /// </summary>
    private static Color GetFallbackColor(string blockType)
    {
        string lower = blockType.ToLowerInvariant();

        // Common block colors
        if (lower.Contains("stone")) return new Color(0.5f, 0.5f, 0.5f);
        if (lower.Contains("dirt")) return new Color(0.6f, 0.4f, 0.2f);
        if (lower.Contains("grass")) return new Color(0.3f, 0.7f, 0.3f);
        if (lower.Contains("wood") || lower.Contains("plank")) return new Color(0.6f, 0.4f, 0.2f);
        if (lower.Contains("cobblestone")) return new Color(0.4f, 0.4f, 0.4f);
        if (lower.Contains("brick")) return new Color(0.7f, 0.3f, 0.2f);
        if (lower.Contains("glass")) return new Color(0.8f, 0.9f, 1f, 0.5f);
        if (lower.Contains("sand")) return new Color(0.9f, 0.9f, 0.7f);
        if (lower.Contains("water")) return new Color(0.2f, 0.4f, 0.8f, 0.5f);
        if (lower.Contains("lava")) return new Color(1f, 0.4f, 0f);

        // Plant colors
        if (lower.Contains("poppy")) return new Color(0.8f, 0.1f, 0.1f);
        if (lower.Contains("dandelion")) return new Color(1f, 0.9f, 0.2f);
        if (lower.Contains("grass") || lower.Contains("fern")) return new Color(0.3f, 0.7f, 0.3f);
        if (lower.Contains("sapling")) return new Color(0.4f, 0.6f, 0.2f);

        // Technical blocks (mods)
        if (lower.Contains("pipe") || lower.Contains("cable")) return new Color(0.4f, 0.4f, 0.4f);
        if (lower.Contains("gear") || lower.Contains("cogwheel")) return new Color(0.3f, 0.3f, 0.3f);

        return Color.white; // Default
    }
}
