using UnityEngine;

/// <summary>
/// Comprehensive block model type system for Minecraft 1.21 and modded blocks (ATM10)
/// Handles all block rendering variants: cubes, slabs, stairs, fences, pipes, etc.
///
/// Based on Minecraft block model JSON formats and ATM10 mod specifications
/// </summary>
public enum BlockModelType
{
    // === FULL BLOCKS ===
    Cube,              // Standard full block (stone, dirt, wood, etc.)

    // === PLANTS & VEGETATION ===
    CrossPlant,        // X-shaped plants (flowers, saplings, crops)
    TintedCross,       // Tinted X-shaped plants (grass, ferns)

    // === PARTIAL BLOCKS ===
    Slab,              // Half-height block (0.5 blocks tall)
    Stairs,            // Stair blocks with various orientations
    Carpet,            // Very thin blocks (1/16 height)
    SnowLayer,         // Variable height snow layers (1-8 layers)

    // === CONNECTABLE BLOCKS (Multipart) ===
    Fence,             // Fence with post + 4 optional sides
    Wall,              // Wall similar to fence but different model
    Pane,              // Glass panes, iron bars (thin vertical)
    Chain,             // Chains (vertical or horizontal)

    // === DOORS & GATES ===
    Door,              // Full-height door (2 blocks tall)
    Trapdoor,          // Horizontal door
    FenceGate,         // Fence gate (opens/closes)

    // === SMALL BLOCKS ===
    Button,            // Small button on wall
    PressurePlate,     // Thin plate on floor
    Torch,             // Torch with stick + flame
    Lever,             // Lever on wall

    // === FURNITURE & DECORATION (ATM10 Mods) ===
    Chest,             // Chests, barrels
    Bed,               // Beds (2 blocks long)
    Furniture,         // Macaw's/Handcrafted furniture

    // === TECHNICAL BLOCKS (ATM10 Mods) ===
    Pipe,              // Pipes (Pipez, Mekanism) - connectable
    Cable,             // Cables (energy/fluid) - thin connectable
    Conduit,           // Conduits (LaserIO) - complex routing

    // === CREATE MOD ===
    Gear,              // Gears, cogwheels (circular)
    Belt,              // Conveyor belts
    Mechanical,        // Generic mechanical components

    // === SPECIAL ===
    Liquid,            // Water, lava (transparent block)
    Air                // Empty/invisible block
}

/// <summary>
/// Static utility class for detecting block model types from block names
/// Supports vanilla Minecraft 1.21 and All the Mods 10 modpack
/// </summary>
public static class BlockModelDetector
{
    /// <summary>
    /// Determines the appropriate model type for a given block name
    /// Case-insensitive, supports both minecraft: and modded namespaces
    /// </summary>
    public static BlockModelType GetModelType(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return BlockModelType.Air;

        string lower = blockName.ToLowerInvariant();

        // === AIR & LIQUIDS ===
        if (lower.Contains("air"))
            return BlockModelType.Air;

        if (lower.Contains("water") || lower.Contains("lava"))
            return BlockModelType.Liquid;

        // === PLANTS (Cross-shaped) ===
        if (IsCrossPlant(lower))
            return BlockModelType.CrossPlant;

        if (IsTintedCross(lower))
            return BlockModelType.TintedCross;

        // === SLABS ===
        if (lower.Contains("slab"))
            return BlockModelType.Slab;

        // === STAIRS ===
        if (lower.Contains("stairs"))
            return BlockModelType.Stairs;

        // === CARPETS ===
        if (lower.Contains("carpet"))
            return BlockModelType.Carpet;

        // === SNOW LAYERS ===
        if (lower.Contains("snow") && !lower.Contains("block"))
            return BlockModelType.SnowLayer;

        // === FENCES ===
        if (lower.Contains("fence") && !lower.Contains("gate"))
            return BlockModelType.Fence;

        if (lower.Contains("fence_gate"))
            return BlockModelType.FenceGate;

        // === WALLS ===
        if (lower.Contains("wall") && !lower.Contains("sign"))
            return BlockModelType.Wall;

        // === PANES (Glass panes, iron bars) ===
        if (lower.Contains("pane") || lower.Contains("bars"))
            return BlockModelType.Pane;

        // === DOORS ===
        if (lower.Contains("door") && !lower.Contains("trapdoor"))
            return BlockModelType.Door;

        if (lower.Contains("trapdoor"))
            return BlockModelType.Trapdoor;

        // === CHAINS ===
        if (lower.Contains("chain"))
            return BlockModelType.Chain;

        // === SMALL BLOCKS ===
        if (lower.Contains("button"))
            return BlockModelType.Button;

        if (lower.Contains("pressure_plate") || lower.Contains("weighted"))
            return BlockModelType.PressurePlate;

        if (lower.Contains("torch"))
            return BlockModelType.Torch;

        if (lower.Contains("lever"))
            return BlockModelType.Lever;

        // === FURNITURE & STORAGE ===
        if (lower.Contains("chest") || lower.Contains("barrel"))
            return BlockModelType.Chest;

        if (lower.Contains("bed"))
            return BlockModelType.Bed;

        // Macaw's and Handcrafted furniture
        if (lower.Contains("chair") || lower.Contains("table") ||
            lower.Contains("sofa") || lower.Contains("counter") ||
            lower.Contains("shelf") || lower.Contains("desk"))
            return BlockModelType.Furniture;

        // === PIPES & CABLES (ATM10 Mods) ===
        // Pipez, Mekanism, Thermal, etc.
        if (lower.Contains("pipe") || lower.Contains("tube"))
            return BlockModelType.Pipe;

        // Energy/fluid cables
        if (lower.Contains("cable") || lower.Contains("wire") ||
            lower.Contains("conduit") && !lower.Contains("laser"))
            return BlockModelType.Cable;

        // LaserIO conduits
        if (lower.Contains("laser") && lower.Contains("conduit"))
            return BlockModelType.Conduit;

        // === CREATE MOD ===
        if (lower.Contains("cogwheel") || lower.Contains("gear") ||
            lower.Contains("large_cogwheel"))
            return BlockModelType.Gear;

        if (lower.Contains("belt") || lower.Contains("conveyor"))
            return BlockModelType.Belt;

        if (lower.Contains("mechanical") || lower.Contains("kinetic") ||
            lower.Contains("gearshift") || lower.Contains("gearbox") ||
            lower.Contains("shaft") || lower.Contains("clutch"))
            return BlockModelType.Mechanical;

        // === DEFAULT: CUBE ===
        return BlockModelType.Cube;
    }

    /// <summary>
    /// Checks if block is a cross-shaped plant
    /// Based on Minecraft's block/cross.json model
    /// </summary>
    private static bool IsCrossPlant(string lower)
    {
        // Flowers
        if (lower.Contains("poppy") || lower.Contains("dandelion") ||
            lower.Contains("orchid") || lower.Contains("allium") ||
            lower.Contains("tulip") || lower.Contains("daisy") ||
            lower.Contains("cornflower") || lower.Contains("lily_of_the_valley") ||
            lower.Contains("wither_rose") || lower.Contains("torchflower") ||
            lower.Contains("pink_petals"))
            return true;

        // Saplings
        if (lower.Contains("sapling"))
            return true;

        // Crops
        if (lower.Contains("wheat") || lower.Contains("carrots") ||
            lower.Contains("potatoes") || lower.Contains("beetroots") ||
            lower.Contains("nether_wart") || lower.Contains("sweet_berry"))
            return true;

        // Mushrooms (not blocks or stems)
        if ((lower.Contains("mushroom") || lower.Contains("fungus")) &&
            !lower.Contains("block") && !lower.Contains("stem"))
            return true;

        // Sugar cane, bamboo
        if (lower.Contains("sugar_cane") || lower.Contains("bamboo_sapling"))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if block is a tinted cross-shaped plant
    /// Based on Minecraft's block/tinted_cross.json model
    /// </summary>
    private static bool IsTintedCross(string lower)
    {
        // Grass and ferns (not grass block)
        if ((lower.Contains("tall_grass") || lower.Contains("fern") ||
             lower.Contains("dead_bush") || lower.Contains("warped_roots") ||
             lower.Contains("crimson_roots")) && !lower.Contains("block"))
            return true;

        return false;
    }

    /// <summary>
    /// Gets a simplified description of the model type for debugging
    /// </summary>
    public static string GetModelDescription(BlockModelType type)
    {
        switch (type)
        {
            case BlockModelType.Cube: return "Full block";
            case BlockModelType.CrossPlant: return "Cross-shaped plant (X)";
            case BlockModelType.TintedCross: return "Tinted cross plant";
            case BlockModelType.Slab: return "Half-height block";
            case BlockModelType.Stairs: return "Stair block";
            case BlockModelType.Carpet: return "Thin carpet (1/16)";
            case BlockModelType.SnowLayer: return "Snow layer";
            case BlockModelType.Fence: return "Fence (post + sides)";
            case BlockModelType.Wall: return "Wall (multipart)";
            case BlockModelType.Pane: return "Glass pane/bars";
            case BlockModelType.Chain: return "Chain (vertical/horizontal)";
            case BlockModelType.Door: return "Door (2 blocks tall)";
            case BlockModelType.Trapdoor: return "Horizontal door";
            case BlockModelType.FenceGate: return "Fence gate";
            case BlockModelType.Button: return "Small button";
            case BlockModelType.PressurePlate: return "Pressure plate";
            case BlockModelType.Torch: return "Torch";
            case BlockModelType.Lever: return "Lever";
            case BlockModelType.Chest: return "Storage block";
            case BlockModelType.Bed: return "Bed (2 blocks)";
            case BlockModelType.Furniture: return "Furniture (mod)";
            case BlockModelType.Pipe: return "Pipe (connectable)";
            case BlockModelType.Cable: return "Cable (thin)";
            case BlockModelType.Conduit: return "Conduit (routing)";
            case BlockModelType.Gear: return "Gear (Create mod)";
            case BlockModelType.Belt: return "Conveyor belt";
            case BlockModelType.Mechanical: return "Mechanical component";
            case BlockModelType.Liquid: return "Liquid (water/lava)";
            case BlockModelType.Air: return "Air/empty";
            default: return "Unknown";
        }
    }
}
