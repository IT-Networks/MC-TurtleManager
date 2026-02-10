using UnityEngine;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;

/// <summary>
/// Parses AI-generated structure descriptions into StructureData objects
/// Supports multiple formats: JSON, text-based descriptions, and schematic lists
/// </summary>
public static class AIStructureParser
{
    /// <summary>
    /// Parse AI response into a StructureData object
    /// Supports multiple formats automatically detected
    /// </summary>
    public static StructureData ParseAIResponse(string aiResponse, out string errorMessage)
    {
        errorMessage = "";

        if (string.IsNullOrWhiteSpace(aiResponse))
        {
            errorMessage = "AI response is empty";
            return null;
        }

        // Try JSON format first
        StructureData structure = TryParseJSON(aiResponse, out string jsonError);
        if (structure != null)
            return structure;

        // Try block-list format
        structure = TryParseBlockList(aiResponse, out string listError);
        if (structure != null)
            return structure;

        // Try natural language format
        structure = TryParseNaturalLanguage(aiResponse, out string nlError);
        if (structure != null)
            return structure;

        // All parsers failed
        errorMessage = $"Failed to parse AI response.\nJSON: {jsonError}\nBlock List: {listError}\nNatural Language: {nlError}";
        return null;
    }

    /// <summary>
    /// Parse JSON format:
    /// {
    ///   "name": "House",
    ///   "description": "Simple house",
    ///   "blocks": [
    ///     {"pos": [0,0,0], "type": "minecraft:stone"},
    ///     {"x": 1, "y": 0, "z": 0, "block": "minecraft:oak_planks"}
    ///   ]
    /// }
    /// </summary>
    private static StructureData TryParseJSON(string json, out string error)
    {
        error = "";
        try
        {
            // Simple JSON extraction (for production, use JsonUtility or Newtonsoft.Json)

            // Extract name
            string name = ExtractJSONValue(json, "name");
            if (string.IsNullOrEmpty(name))
                name = "AI Generated Structure";

            // Extract description
            string description = ExtractJSONValue(json, "description");
            if (string.IsNullOrEmpty(description))
                description = "Created by AI";

            // Extract blocks array
            string blocksArray = ExtractJSONArray(json, "blocks");
            if (string.IsNullOrEmpty(blocksArray))
            {
                error = "No blocks array found in JSON";
                return null;
            }

            // Create structure
            StructureData structure = new StructureData(name);
            structure.description = description;
            structure.author = "LM Studio AI";

            // Parse individual blocks
            int blockCount = 0;

            // Match block objects in array
            Regex blockPattern = new Regex(@"\{[^\}]+\}");
            MatchCollection blockMatches = blockPattern.Matches(blocksArray);

            foreach (Match blockMatch in blockMatches)
            {
                string blockObj = blockMatch.Value;

                Vector3Int pos = ExtractPosition(blockObj);
                string blockType = ExtractBlockType(blockObj);

                if (!string.IsNullOrEmpty(blockType))
                {
                    structure.AddBlock(pos, blockType);
                    blockCount++;
                }
            }

            if (blockCount == 0)
            {
                error = "No valid blocks found in JSON";
                return null;
            }

            structure.Normalize(); // Normalize to start at (0,0,0)
            Debug.Log($"[AIStructureParser] Parsed JSON: {blockCount} blocks");
            return structure;
        }
        catch (Exception e)
        {
            error = $"JSON parsing exception: {e.Message}";
            return null;
        }
    }

    /// <summary>
    /// Parse block-list format:
    /// STRUCTURE: House
    /// DESCRIPTION: Simple house
    /// BLOCKS:
    /// 0,0,0:minecraft:stone
    /// 1,0,0:minecraft:oak_planks
    /// 0,1,0:create:cogwheel
    /// </summary>
    private static StructureData TryParseBlockList(string text, out string error)
    {
        error = "";
        try
        {
            string[] lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            string name = "AI Generated Structure";
            string description = "Created by AI";
            StructureData structure = null;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                // Extract metadata
                if (trimmed.StartsWith("STRUCTURE:") || trimmed.StartsWith("NAME:"))
                {
                    name = trimmed.Split(':')[1].Trim();
                    continue;
                }

                if (trimmed.StartsWith("DESCRIPTION:") || trimmed.StartsWith("DESC:"))
                {
                    description = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                    continue;
                }

                if (trimmed.StartsWith("BLOCKS:"))
                {
                    structure = new StructureData(name);
                    structure.description = description;
                    structure.author = "LM Studio AI";
                    continue;
                }

                // Parse block lines: "x,y,z:blocktype" or "x,y,z=blocktype"
                if (structure != null)
                {
                    Match blockMatch = Regex.Match(trimmed, @"(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*[:=]\s*(.+)");
                    if (blockMatch.Success)
                    {
                        int x = int.Parse(blockMatch.Groups[1].Value);
                        int y = int.Parse(blockMatch.Groups[2].Value);
                        int z = int.Parse(blockMatch.Groups[3].Value);
                        string blockType = blockMatch.Groups[4].Value.Trim();

                        if (AIBlockLibrary.IsValidBlock(blockType))
                        {
                            structure.AddBlock(new Vector3Int(x, y, z), blockType);
                        }
                        else
                        {
                            Debug.LogWarning($"[AIStructureParser] Invalid block type: {blockType}");
                        }
                    }
                }
            }

            if (structure == null || structure.blockCount == 0)
            {
                error = "No valid blocks found in block list format";
                return null;
            }

            structure.Normalize();
            Debug.Log($"[AIStructureParser] Parsed block list: {structure.blockCount} blocks");
            return structure;
        }
        catch (Exception e)
        {
            error = $"Block list parsing exception: {e.Message}";
            return null;
        }
    }

    /// <summary>
    /// Parse natural language format (more flexible)
    /// Looks for coordinate patterns and block names in text
    /// </summary>
    private static StructureData TryParseNaturalLanguage(string text, out string error)
    {
        error = "";
        try
        {
            StructureData structure = new StructureData("AI Generated Structure");
            structure.description = "Parsed from natural language";
            structure.author = "LM Studio AI";

            // Find all patterns like: "place stone at 0,0,0" or "0,0,0 = oak_planks"
            Regex pattern = new Regex(@"(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+).{0,50}?(minecraft:\w+|create:\w+|mekanism:\w+|\w+:\w+)");
            MatchCollection matches = pattern.Matches(text);

            foreach (Match match in matches)
            {
                int x = int.Parse(match.Groups[1].Value);
                int y = int.Parse(match.Groups[2].Value);
                int z = int.Parse(match.Groups[3].Value);
                string blockType = match.Groups[4].Value;

                // Add minecraft: prefix if missing
                if (!blockType.Contains(":"))
                    blockType = "minecraft:" + blockType;

                structure.AddBlock(new Vector3Int(x, y, z), blockType);
            }

            if (structure.blockCount == 0)
            {
                error = "No coordinate/block patterns found in text";
                return null;
            }

            structure.Normalize();
            Debug.Log($"[AIStructureParser] Parsed natural language: {structure.blockCount} blocks");
            return structure;
        }
        catch (Exception e)
        {
            error = $"Natural language parsing exception: {e.Message}";
            return null;
        }
    }

    // === JSON Helper Functions ===

    private static string ExtractJSONValue(string json, string key)
    {
        Regex regex = new Regex($"\"{key}\"\\s*:\\s*\"([^\"]+)\"");
        Match match = regex.Match(json);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string ExtractJSONArray(string json, string key)
    {
        Regex regex = new Regex($"\"{key}\"\\s*:\\s*\\[([^\\]]+)\\]", RegexOptions.Singleline);
        Match match = regex.Match(json);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static Vector3Int ExtractPosition(string blockObj)
    {
        // Try "pos": [x,y,z] format
        Regex posArrayRegex = new Regex(@"""pos""\s*:\s*\[\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\]");
        Match match = posArrayRegex.Match(blockObj);
        if (match.Success)
        {
            return new Vector3Int(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value)
            );
        }

        // Try "x": 0, "y": 0, "z": 0 format
        Regex xRegex = new Regex(@"""x""\s*:\s*(-?\d+)");
        Regex yRegex = new Regex(@"""y""\s*:\s*(-?\d+)");
        Regex zRegex = new Regex(@"""z""\s*:\s*(-?\d+)");

        Match xMatch = xRegex.Match(blockObj);
        Match yMatch = yRegex.Match(blockObj);
        Match zMatch = zRegex.Match(blockObj);

        if (xMatch.Success && yMatch.Success && zMatch.Success)
        {
            return new Vector3Int(
                int.Parse(xMatch.Groups[1].Value),
                int.Parse(yMatch.Groups[1].Value),
                int.Parse(zMatch.Groups[1].Value)
            );
        }

        return Vector3Int.zero;
    }

    private static string ExtractBlockType(string blockObj)
    {
        // Try "type": "blockname"
        string blockType = ExtractJSONValue(blockObj, "type");
        if (!string.IsNullOrEmpty(blockType))
            return blockType;

        // Try "block": "blockname"
        blockType = ExtractJSONValue(blockObj, "block");
        if (!string.IsNullOrEmpty(blockType))
            return blockType;

        // Try "blockType": "blockname"
        blockType = ExtractJSONValue(blockObj, "blockType");
        return blockType;
    }

    /// <summary>
    /// Validate parsed structure
    /// </summary>
    public static bool ValidateStructure(StructureData structure, out string validationError)
    {
        validationError = "";

        if (structure == null)
        {
            validationError = "Structure is null";
            return false;
        }

        if (structure.blockCount == 0)
        {
            validationError = "Structure has no blocks";
            return false;
        }

        if (structure.blockCount > 10000)
        {
            validationError = $"Structure is too large ({structure.blockCount} blocks, max 10000)";
            return false;
        }

        // Check for invalid block types
        int invalidBlocks = 0;
        foreach (var block in structure.blocks)
        {
            if (!AIBlockLibrary.IsValidBlock(block.blockType))
            {
                Debug.LogWarning($"[AIStructureParser] Invalid block type in structure: {block.blockType}");
                invalidBlocks++;
            }
        }

        if (invalidBlocks > 0)
        {
            Debug.LogWarning($"[AIStructureParser] Structure contains {invalidBlocks} invalid block types");
        }

        return true;
    }

    /// <summary>
    /// Get example format for AI prompt
    /// </summary>
    public static string GetExampleFormat()
    {
        return @"EXPECTED RESPONSE FORMAT:

Option 1 - JSON Format (Recommended):
{
  ""name"": ""Simple House"",
  ""description"": ""A 5x5 house with door and windows"",
  ""blocks"": [
    {""pos"": [0,0,0], ""type"": ""minecraft:stone""},
    {""pos"": [1,0,0], ""type"": ""minecraft:oak_planks""},
    {""pos"": [0,1,0], ""type"": ""minecraft:glass""}
  ]
}

Option 2 - Block List Format:
STRUCTURE: Mechanical Workshop
DESCRIPTION: Create mod workshop with gears
BLOCKS:
0,0,0:minecraft:stone
1,0,0:minecraft:oak_planks
0,1,0:create:cogwheel
1,1,0:create:shaft

Use only blocks from the provided block library.
Coordinates are relative (will be normalized to start at 0,0,0).";
    }
}
