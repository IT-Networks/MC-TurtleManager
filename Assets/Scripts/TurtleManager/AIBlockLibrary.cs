using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Comprehensive block library for AI structure generation
/// Contains all available blocks (Vanilla Minecraft, Create Mod, ATM10 mods)
/// Used to inform the AI what blocks it can use when designing structures
/// </summary>
public static class AIBlockLibrary
{
    /// <summary>
    /// All available blocks organized by category
    /// </summary>
    public static readonly Dictionary<string, List<string>> BlockCategories = new Dictionary<string, List<string>>
    {
        // === BASIC BUILDING MATERIALS ===
        {
            "Stone & Bricks", new List<string>
            {
                "minecraft:stone",
                "minecraft:cobblestone",
                "minecraft:stone_bricks",
                "minecraft:mossy_stone_bricks",
                "minecraft:cracked_stone_bricks",
                "minecraft:chiseled_stone_bricks",
                "minecraft:smooth_stone",
                "minecraft:andesite",
                "minecraft:polished_andesite",
                "minecraft:diorite",
                "minecraft:polished_diorite",
                "minecraft:granite",
                "minecraft:polished_granite",
                "minecraft:deepslate",
                "minecraft:deepslate_bricks",
                "minecraft:cracked_deepslate_bricks",
                "minecraft:deepslate_tiles",
                "minecraft:cracked_deepslate_tiles"
            }
        },
        {
            "Wood & Planks", new List<string>
            {
                "minecraft:oak_planks",
                "minecraft:oak_log",
                "minecraft:stripped_oak_log",
                "minecraft:spruce_planks",
                "minecraft:spruce_log",
                "minecraft:birch_planks",
                "minecraft:birch_log",
                "minecraft:jungle_planks",
                "minecraft:jungle_log",
                "minecraft:acacia_planks",
                "minecraft:acacia_log",
                "minecraft:dark_oak_planks",
                "minecraft:dark_oak_log",
                "minecraft:mangrove_planks",
                "minecraft:mangrove_log",
                "minecraft:cherry_planks",
                "minecraft:cherry_log",
                "minecraft:crimson_planks",
                "minecraft:crimson_stem",
                "minecraft:warped_planks",
                "minecraft:warped_stem"
            }
        },
        {
            "Glass & Transparent", new List<string>
            {
                "minecraft:glass",
                "minecraft:white_stained_glass",
                "minecraft:light_gray_stained_glass",
                "minecraft:gray_stained_glass",
                "minecraft:black_stained_glass",
                "minecraft:brown_stained_glass",
                "minecraft:red_stained_glass",
                "minecraft:orange_stained_glass",
                "minecraft:yellow_stained_glass",
                "minecraft:lime_stained_glass",
                "minecraft:green_stained_glass",
                "minecraft:cyan_stained_glass",
                "minecraft:light_blue_stained_glass",
                "minecraft:blue_stained_glass",
                "minecraft:purple_stained_glass",
                "minecraft:magenta_stained_glass",
                "minecraft:pink_stained_glass",
                "minecraft:glass_pane",
                "minecraft:iron_bars"
            }
        },
        {
            "Concrete & Terracotta", new List<string>
            {
                "minecraft:white_concrete",
                "minecraft:light_gray_concrete",
                "minecraft:gray_concrete",
                "minecraft:black_concrete",
                "minecraft:brown_concrete",
                "minecraft:red_concrete",
                "minecraft:orange_concrete",
                "minecraft:yellow_concrete",
                "minecraft:lime_concrete",
                "minecraft:green_concrete",
                "minecraft:cyan_concrete",
                "minecraft:light_blue_concrete",
                "minecraft:blue_concrete",
                "minecraft:purple_concrete",
                "minecraft:magenta_concrete",
                "minecraft:pink_concrete",
                "minecraft:white_terracotta",
                "minecraft:terracotta"
            }
        },
        {
            "Wool & Carpet", new List<string>
            {
                "minecraft:white_wool",
                "minecraft:light_gray_wool",
                "minecraft:gray_wool",
                "minecraft:black_wool",
                "minecraft:brown_wool",
                "minecraft:red_wool",
                "minecraft:orange_wool",
                "minecraft:yellow_wool",
                "minecraft:lime_wool",
                "minecraft:green_wool",
                "minecraft:cyan_wool",
                "minecraft:light_blue_wool",
                "minecraft:blue_wool",
                "minecraft:purple_wool",
                "minecraft:magenta_wool",
                "minecraft:pink_wool"
            }
        },

        // === DECORATIVE BLOCKS ===
        {
            "Slabs", new List<string>
            {
                "minecraft:stone_slab",
                "minecraft:stone_brick_slab",
                "minecraft:oak_slab",
                "minecraft:spruce_slab",
                "minecraft:birch_slab",
                "minecraft:jungle_slab",
                "minecraft:acacia_slab",
                "minecraft:dark_oak_slab",
                "minecraft:smooth_stone_slab",
                "minecraft:andesite_slab",
                "minecraft:diorite_slab",
                "minecraft:granite_slab"
            }
        },
        {
            "Stairs", new List<string>
            {
                "minecraft:stone_stairs",
                "minecraft:stone_brick_stairs",
                "minecraft:oak_stairs",
                "minecraft:spruce_stairs",
                "minecraft:birch_stairs",
                "minecraft:jungle_stairs",
                "minecraft:acacia_stairs",
                "minecraft:dark_oak_stairs",
                "minecraft:andesite_stairs",
                "minecraft:diorite_stairs",
                "minecraft:granite_stairs"
            }
        },
        {
            "Fences & Walls", new List<string>
            {
                "minecraft:oak_fence",
                "minecraft:spruce_fence",
                "minecraft:birch_fence",
                "minecraft:jungle_fence",
                "minecraft:acacia_fence",
                "minecraft:dark_oak_fence",
                "minecraft:nether_brick_fence",
                "minecraft:cobblestone_wall",
                "minecraft:mossy_cobblestone_wall",
                "minecraft:stone_brick_wall",
                "minecraft:andesite_wall",
                "minecraft:diorite_wall",
                "minecraft:granite_wall"
            }
        },
        {
            "Doors & Gates", new List<string>
            {
                "minecraft:oak_door",
                "minecraft:spruce_door",
                "minecraft:birch_door",
                "minecraft:jungle_door",
                "minecraft:acacia_door",
                "minecraft:dark_oak_door",
                "minecraft:iron_door",
                "minecraft:oak_fence_gate",
                "minecraft:spruce_fence_gate",
                "minecraft:birch_fence_gate",
                "minecraft:jungle_fence_gate",
                "minecraft:acacia_fence_gate",
                "minecraft:dark_oak_fence_gate",
                "minecraft:oak_trapdoor",
                "minecraft:spruce_trapdoor",
                "minecraft:birch_trapdoor",
                "minecraft:iron_trapdoor"
            }
        },

        // === FUNCTIONAL BLOCKS ===
        {
            "Storage & Crafting", new List<string>
            {
                "minecraft:chest",
                "minecraft:barrel",
                "minecraft:crafting_table",
                "minecraft:furnace",
                "minecraft:smoker",
                "minecraft:blast_furnace",
                "minecraft:anvil",
                "minecraft:enchanting_table",
                "minecraft:bookshelf",
                "minecraft:lectern"
            }
        },
        {
            "Lighting", new List<string>
            {
                "minecraft:torch",
                "minecraft:soul_torch",
                "minecraft:lantern",
                "minecraft:soul_lantern",
                "minecraft:glowstone",
                "minecraft:sea_lantern",
                "minecraft:redstone_lamp",
                "minecraft:shroomlight",
                "minecraft:end_rod"
            }
        },

        // === CREATE MOD - MECHANICAL COMPONENTS ===
        {
            "Create: Kinetic Power", new List<string>
            {
                "create:cogwheel",
                "create:large_cogwheel",
                "create:shaft",
                "create:gearbox",
                "create:gearshift",
                "create:clutch",
                "create:encased_chain_drive",
                "create:adjustable_chain_gearshift",
                "create:rotation_speed_controller"
            }
        },
        {
            "Create: Generators & Motors", new List<string>
            {
                "create:water_wheel",
                "create:windmill_bearing",
                "create:mechanical_bearing",
                "create:steam_engine",
                "create:motor",
                "create:creative_motor"
            }
        },
        {
            "Create: Logistics", new List<string>
            {
                "create:mechanical_arm",
                "create:deployer",
                "create:mechanical_drill",
                "create:mechanical_saw",
                "create:mechanical_harvester",
                "create:mechanical_plough",
                "create:andesite_funnel",
                "create:brass_funnel",
                "create:andesite_tunnel",
                "create:brass_tunnel"
            }
        },
        {
            "Create: Conveyor Belts", new List<string>
            {
                "create:belt_connector",
                "create:mechanical_belt",
                "create:belt_support"
            }
        },
        {
            "Create: Processing", new List<string>
            {
                "create:millstone",
                "create:crushing_wheel",
                "create:mechanical_press",
                "create:mechanical_mixer",
                "create:blaze_burner",
                "create:basin",
                "create:item_drain",
                "create:spout"
            }
        },
        {
            "Create: Storage & Containers", new List<string>
            {
                "create:item_vault",
                "create:fluid_tank",
                "create:creative_fluid_tank",
                "create:hose_pulley"
            }
        },
        {
            "Create: Redstone", new List<string>
            {
                "create:analog_lever",
                "create:powered_toggle_latch",
                "create:powered_latch",
                "create:redstone_link",
                "create:nixie_tube",
                "create:sequenced_gearshift",
                "create:speedometer",
                "create:stressometer"
            }
        },
        {
            "Create: Decoration", new List<string>
            {
                "create:copper_shingles",
                "create:copper_tiles",
                "create:layered_andesite",
                "create:layered_brass",
                "create:layered_copper",
                "create:metal_girder",
                "create:metal_scaffold",
                "create:industrial_iron_block"
            }
        },

        // === ATM10 MODS - PIPES & CABLES ===
        {
            "Pipez: Item Transport", new List<string>
            {
                "pipez:item_pipe",
                "pipez:gas_pipe",
                "pipez:fluid_pipe",
                "pipez:energy_pipe",
                "pipez:universal_pipe"
            }
        },
        {
            "Mekanism: Logistics", new List<string>
            {
                "mekanism:logistical_transporter",
                "mekanism:mechanical_pipe",
                "mekanism:pressurized_tube",
                "mekanism:universal_cable",
                "mekanism:thermodynamic_conductor"
            }
        },
        {
            "Thermal: Ducts", new List<string>
            {
                "thermal:fluid_duct",
                "thermal:energy_duct",
                "thermal:item_duct"
            }
        },

        // === ATM10 MODS - MACHINES ===
        {
            "Mekanism: Machines", new List<string>
            {
                "mekanism:metallurgic_infuser",
                "mekanism:enrichment_chamber",
                "mekanism:crusher",
                "mekanism:energized_smelter",
                "mekanism:purification_chamber",
                "mekanism:chemical_injection_chamber",
                "mekanism:electrolytic_separator",
                "mekanism:digital_miner"
            }
        },
        {
            "Industrial Foregoing", new List<string>
            {
                "industrialforegoing:material_stonework_factory",
                "industrialforegoing:plant_gatherer",
                "industrialforegoing:plant_sower",
                "industrialforegoing:mob_crusher",
                "industrialforegoing:laser_drill"
            }
        },

        // === ATM10 MODS - DECORATION ===
        {
            "Chipped: Decorative Variants", new List<string>
            {
                "chipped:carved_oak_planks",
                "chipped:tiled_stone_bricks",
                "chipped:polished_stone"
            }
        },
        {
            "Handcrafted: Furniture", new List<string>
            {
                "handcrafted:oak_chair",
                "handcrafted:oak_table",
                "handcrafted:oak_bench",
                "handcrafted:oak_counter",
                "handcrafted:oak_desk"
            }
        }
    };

    /// <summary>
    /// Get all available blocks as a flat list
    /// </summary>
    public static List<string> GetAllBlocks()
    {
        List<string> allBlocks = new List<string>();
        foreach (var category in BlockCategories.Values)
        {
            allBlocks.AddRange(category);
        }
        return allBlocks;
    }

    /// <summary>
    /// Get formatted block library for AI prompt (grouped by category)
    /// </summary>
    public static string GetFormattedBlockLibrary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("AVAILABLE BLOCKS FOR CONSTRUCTION:");
        sb.AppendLine();

        foreach (var category in BlockCategories)
        {
            sb.AppendLine($"## {category.Key}");
            foreach (var block in category.Value)
            {
                // Remove minecraft: prefix for readability
                string displayName = block.Replace("minecraft:", "").Replace("create:", "").Replace("_", " ");
                sb.AppendLine($"  - {block}  ({displayName})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Get concise block library for AI prompt (comma-separated by category)
    /// More token-efficient for LLM context
    /// </summary>
    public static string GetConciseBlockLibrary()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("AVAILABLE BLOCKS:");
        sb.AppendLine();

        foreach (var category in BlockCategories)
        {
            sb.Append($"{category.Key}: ");
            sb.AppendLine(string.Join(", ", category.Value));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Get only Create mod blocks (for mechanical builds)
    /// </summary>
    public static List<string> GetCreateModBlocks()
    {
        List<string> createBlocks = new List<string>();
        foreach (var category in BlockCategories)
        {
            if (category.Key.StartsWith("Create:"))
            {
                createBlocks.AddRange(category.Value);
            }
        }
        return createBlocks;
    }

    /// <summary>
    /// Get blocks by category name
    /// </summary>
    public static List<string> GetBlocksByCategory(string categoryName)
    {
        if (BlockCategories.ContainsKey(categoryName))
        {
            return new List<string>(BlockCategories[categoryName]);
        }
        return new List<string>();
    }

    /// <summary>
    /// Check if a block exists in the library
    /// </summary>
    public static bool IsValidBlock(string blockName)
    {
        foreach (var category in BlockCategories.Values)
        {
            if (category.Contains(blockName))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get category for a specific block
    /// </summary>
    public static string GetBlockCategory(string blockName)
    {
        foreach (var category in BlockCategories)
        {
            if (category.Value.Contains(blockName))
                return category.Key;
        }
        return "Unknown";
    }
}
