using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

/// <summary>
/// LM Studio AI Integration for Turtle Control
/// Allows an AI model to control turtles based on their inventory and surroundings
/// </summary>
public class LMStudioManager : MonoBehaviour
{
    [Header("LM Studio Configuration")]
    public string lmStudioUrl = "http://localhost:1234/v1/chat/completions";
    public string modelName = "local-model";
    public float temperature = 0.7f;
    public int maxTokens = 500;

    [Header("Turtle Reference")]
    public TurtleObject turtle;
    public TurtleBaseManager baseManager;
    public TurtleInventoryManager inventoryManager;
    public WorldManager worldManager;

    [Header("AI Behavior")]
    public bool enableAIControl = false;
    public float aiDecisionInterval = 5f; // How often AI makes decisions
    public bool allowMining = true;
    public bool allowBuilding = true;
    public bool allowExploration = true;

    [Header("Debug")]
    public bool debugMode = false;
    public string lastAIResponse = "";
    public string lastExecutedCommand = "";

    [Header("Structure Generation")]
    public StructureManager structureManager;
    public TurtleBuildingManager buildingManager;
    public bool autoSaveGeneratedStructures = true;
    public bool autoBuildGeneratedStructures = false;

    private float lastDecisionTime = 0f;
    private List<string> conversationHistory = new List<string>();
    private bool isProcessingAIRequest = false;

    // Callbacks for structure generation
    public event System.Action<StructureData> OnStructureGenerated;
    public event System.Action<string> OnStructureGenerationFailed;

    void Start()
    {
        if (turtle == null) turtle = GetComponent<TurtleObject>();
        if (baseManager == null) baseManager = GetComponent<TurtleBaseManager>();
        if (inventoryManager == null) inventoryManager = GetComponent<TurtleInventoryManager>();

        // Add system prompt to conversation
        conversationHistory.Add(GetSystemPrompt());
    }

    void Update()
    {
        if (!enableAIControl || isProcessingAIRequest || turtle.isBusy) return;

        // Make AI decisions periodically
        if (Time.time - lastDecisionTime > aiDecisionInterval)
        {
            StartCoroutine(RequestAIDecision());
            lastDecisionTime = Time.time;
        }
    }

    /// <summary>
    /// Request AI to make a decision based on current turtle state
    /// </summary>
    public IEnumerator RequestAIDecision()
    {
        isProcessingAIRequest = true;

        if (debugMode) Debug.Log($"[AI {turtle.turtleName}] Requesting AI decision...");

        // Build context about turtle's current state
        string context = BuildTurtleContext();

        // Send request to LM Studio
        yield return StartCoroutine(SendLMStudioRequest(context));

        isProcessingAIRequest = false;
    }

    /// <summary>
    /// Build comprehensive context about turtle's current state
    /// </summary>
    private string BuildTurtleContext()
    {
        StringBuilder context = new StringBuilder();

        // Position and fuel
        context.AppendLine($"Current Position: {turtle.transform.position}");
        context.AppendLine($"Fuel Level: {turtle.fuelLevel}/{turtle.maxFuel}");
        context.AppendLine($"Fuel Percentage: {(float)turtle.fuelLevel / turtle.maxFuel * 100:F1}%");

        // Inventory status
        context.AppendLine($"\nInventory Status: {turtle.inventorySlotsUsed}/16 slots used");

        if (turtle.inventory != null && turtle.inventory.Count > 0)
        {
            context.AppendLine("\nCurrent Inventory:");
            Dictionary<string, int> itemCounts = new Dictionary<string, int>();

            foreach (var item in turtle.inventory)
            {
                if (itemCounts.ContainsKey(item.name))
                    itemCounts[item.name] += item.count;
                else
                    itemCounts[item.name] = item.count;
            }

            foreach (var kvp in itemCounts)
            {
                string itemName = kvp.Key.Replace("minecraft:", "").Replace("_", " ");
                context.AppendLine($"  - {itemName}: {kvp.Value}x");
            }
        }
        else
        {
            context.AppendLine("\nInventory: Empty");
        }

        // Nearby blocks (simplified scan)
        if (worldManager != null)
        {
            context.AppendLine("\nNearby valuable blocks:");
            List<string> nearbyOres = ScanNearbyOres(5f);
            if (nearbyOres.Count > 0)
            {
                foreach (string ore in nearbyOres)
                {
                    context.AppendLine($"  - {ore}");
                }
            }
            else
            {
                context.AppendLine("  - None detected");
            }
        }

        // Capabilities
        context.AppendLine("\nWhat I can do:");
        if (allowMining) context.AppendLine("  - Mine blocks");
        if (allowBuilding) context.AppendLine("  - Build structures");
        if (allowExploration) context.AppendLine("  - Explore area");

        // Current task
        if (turtle.currentOperation != TurtleOperationManager.OperationType.None)
        {
            context.AppendLine($"\nCurrent Task: {turtle.currentOperation}");
        }

        return context.ToString();
    }

    /// <summary>
    /// Scan for nearby valuable blocks (ores)
    /// </summary>
    private List<string> ScanNearbyOres(float radius)
    {
        List<string> ores = new HashSet<string>().ToList();
        Vector3 turtlePos = turtle.transform.position;

        int r = Mathf.CeilToInt(radius);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int z = -r; z <= r; z++)
                {
                    Vector3 checkPos = turtlePos + new Vector3(x, y, z);
                    string blockType = worldManager.GetBlockTypeAtPosition(checkPos);

                    if (blockType != null && blockType.Contains("ore") && !ores.Contains(blockType))
                    {
                        ores.Add(blockType.Replace("minecraft:", "").Replace("_", " "));
                    }
                }
            }
        }

        return ores;
    }

    /// <summary>
    /// Send request to LM Studio API
    /// </summary>
    private IEnumerator SendLMStudioRequest(string userMessage)
    {
        // Build request
        var messages = new List<Dictionary<string, string>>();

        // Add system prompt
        messages.Add(new Dictionary<string, string>
        {
            { "role", "system" },
            { "content", conversationHistory[0] }
        });

        // Add user message
        messages.Add(new Dictionary<string, string>
        {
            { "role", "user" },
            { "content", userMessage }
        });

        var requestData = new Dictionary<string, object>
        {
            { "model", modelName },
            { "messages", messages },
            { "temperature", temperature },
            { "max_tokens", maxTokens }
        };

        string jsonRequest = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

        UnityWebRequest request = new UnityWebRequest(lmStudioUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        if (debugMode) Debug.Log($"[AI] Sending request to LM Studio: {userMessage.Substring(0, Mathf.Min(100, userMessage.Length))}...");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string response = request.downloadHandler.text;
            if (debugMode) Debug.Log($"[AI] Received response: {response}");

            // Parse response
            ParseAndExecuteAIResponse(response);
        }
        else
        {
            Debug.LogError($"[AI] LM Studio request failed: {request.error}");
            Debug.LogError($"[AI] Response: {request.downloadHandler.text}");
        }
    }

    /// <summary>
    /// Parse AI response and execute commands
    /// </summary>
    private void ParseAndExecuteAIResponse(string jsonResponse)
    {
        try
        {
            // Simple JSON parsing (in production, use JsonUtility or Newtonsoft.Json)
            // Expected format: {"choices":[{"message":{"content":"COMMAND:forward\nREASON:..."}}]}

            int contentStart = jsonResponse.IndexOf("\"content\":\"") + 11;
            int contentEnd = jsonResponse.IndexOf("\"", contentStart);
            string content = jsonResponse.Substring(contentStart, contentEnd - contentStart);

            // Unescape JSON
            content = content.Replace("\\n", "\n").Replace("\\\"", "\"");

            lastAIResponse = content;

            if (debugMode) Debug.Log($"[AI] AI Response: {content}");

            // Parse commands from response
            string[] lines = content.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("COMMAND:"))
                {
                    string command = line.Replace("COMMAND:", "").Trim().ToLower();
                    ExecuteAICommand(command);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AI] Failed to parse AI response: {e.Message}");
        }
    }

    /// <summary>
    /// Execute command from AI
    /// </summary>
    private void ExecuteAICommand(string command)
    {
        lastExecutedCommand = command;
        if (debugMode) Debug.Log($"[AI] Executing command: {command}");

        // Parse command and parameters
        string[] parts = command.Split(' ');
        string action = parts[0];

        switch (action)
        {
            // Movement
            case "forward":
            case "back":
            case "up":
            case "down":
            case "left":
            case "right":
                baseManager.QueueCommand(action);
                break;

            // Mining
            case "mine":
                if (allowMining)
                {
                    baseManager.QueueCommand("dig");
                }
                break;

            case "mine_up":
                if (allowMining)
                {
                    baseManager.QueueCommand("digup");
                }
                break;

            case "mine_down":
                if (allowMining)
                {
                    baseManager.QueueCommand("digdown");
                }
                break;

            // Inventory management
            case "store_items":
                if (inventoryManager != null)
                {
                    StartCoroutine(inventoryManager.ReturnToChestAndBack());
                }
                break;

            case "refuel":
                if (inventoryManager != null)
                {
                    StartCoroutine(inventoryManager.RefuelTurtle());
                }
                break;

            // Utility
            case "scan":
                baseManager.QueueCommand("scan");
                break;

            case "wait":
                // Do nothing, just wait
                break;

            default:
                Debug.LogWarning($"[AI] Unknown command: {action}");
                break;
        }
    }

    /// <summary>
    /// Get system prompt for AI
    /// </summary>
    private string GetSystemPrompt()
    {
        return @"You are an AI controlling a Minecraft turtle (robot). Your goal is to help the player by mining resources, building structures, and managing your inventory efficiently.

AVAILABLE COMMANDS:
Movement:
- forward, back, up, down, left, right

Mining:
- mine (mine block in front)
- mine_up (mine block above)
- mine_down (mine block below)

Inventory:
- store_items (return to chest, store items, return)
- refuel (refuel from inventory or chest)

Utility:
- scan (scan surroundings for blocks)
- wait (do nothing this turn)

DECISION MAKING RULES:
1. FUEL MANAGEMENT: If fuel < 20%, prioritize refueling
2. INVENTORY: If inventory > 14/16 slots, store items in chest
3. MINING: Mine valuable ores (diamond, gold, iron) when detected
4. EFFICIENCY: Minimize unnecessary movement to save fuel
5. SAFETY: Don't mine yourself into a hole you can't escape

RESPONSE FORMAT:
You must respond with:
COMMAND:<command>
REASON:<why you chose this command>

Example:
COMMAND:mine
REASON:Detected diamond ore in front, mining to collect valuable resource

CONTEXT:
You will receive updates about your:
- Current position
- Fuel level
- Inventory contents
- Nearby valuable blocks
- Current task

Make smart decisions based on this information. Prioritize:
1. Survival (fuel and safety)
2. Resource gathering (ores)
3. Efficiency (minimize wasted movement)

Always explain your reasoning to help the player understand your decision.";
    }

    /// <summary>
    /// Manually request AI decision (for testing)
    /// </summary>
    public void RequestManualAIDecision()
    {
        StartCoroutine(RequestAIDecision());
    }

    /// <summary>
    /// Enable/disable AI control
    /// </summary>
    public void SetAIControl(bool enabled)
    {
        enableAIControl = enabled;
        if (debugMode) Debug.Log($"[AI] AI Control {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Send custom prompt to AI
    /// </summary>
    public void SendCustomPrompt(string prompt)
    {
        StartCoroutine(SendLMStudioRequest(prompt));
    }

    // ========== STRUCTURE GENERATION ==========

    /// <summary>
    /// Request AI to generate a structure based on a text prompt
    /// Examples: "Build a 5x5 house with door and windows"
    ///           "Create a mechanical workshop with Create mod gears and belts"
    ///           "Design a small castle tower"
    /// </summary>
    public void GenerateStructureFromPrompt(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            Debug.LogError("[AI] Cannot generate structure from empty prompt");
            OnStructureGenerationFailed?.Invoke("Prompt is empty");
            return;
        }

        StartCoroutine(RequestStructureGeneration(userPrompt));
    }

    /// <summary>
    /// Coroutine to request structure generation from AI
    /// </summary>
    private IEnumerator RequestStructureGeneration(string userPrompt)
    {
        isProcessingAIRequest = true;

        if (debugMode) Debug.Log($"[AI] Requesting structure generation: {userPrompt}");

        // Build comprehensive prompt for structure generation
        string fullPrompt = BuildStructureGenerationPrompt(userPrompt);

        // Send request to LM Studio
        yield return StartCoroutine(SendStructureGenerationRequest(fullPrompt));

        isProcessingAIRequest = false;
    }

    /// <summary>
    /// Build comprehensive prompt for structure generation
    /// </summary>
    private string BuildStructureGenerationPrompt(string userPrompt)
    {
        StringBuilder prompt = new StringBuilder();

        prompt.AppendLine("=== MINECRAFT STRUCTURE GENERATION REQUEST ===");
        prompt.AppendLine();
        prompt.AppendLine($"USER REQUEST: {userPrompt}");
        prompt.AppendLine();

        // Add block library (concise version to save tokens)
        prompt.AppendLine(AIBlockLibrary.GetConciseBlockLibrary());
        prompt.AppendLine();

        // Add format instructions
        prompt.AppendLine(AIStructureParser.GetExampleFormat());
        prompt.AppendLine();

        prompt.AppendLine("IMPORTANT INSTRUCTIONS:");
        prompt.AppendLine("1. Design a structure that matches the user's request");
        prompt.AppendLine("2. Use ONLY blocks from the provided block library");
        prompt.AppendLine("3. Use realistic proportions (houses are typically 5-10 blocks wide/deep, 3-5 blocks tall)");
        prompt.AppendLine("4. Include functional details (doors, windows, lighting)");
        prompt.AppendLine("5. For Create mod builds, ensure mechanical connections make sense");
        prompt.AppendLine("6. Coordinates are relative - will be placed at turtle's location");
        prompt.AppendLine("7. Build from bottom-up (Y=0 is ground level)");
        prompt.AppendLine();

        prompt.AppendLine("Respond with ONLY the JSON or block list format. No additional commentary.");

        return prompt.ToString();
    }

    /// <summary>
    /// Send structure generation request to LM Studio
    /// </summary>
    private IEnumerator SendStructureGenerationRequest(string prompt)
    {
        var messages = new List<Dictionary<string, string>>();

        // System prompt for structure generation
        messages.Add(new Dictionary<string, string>
        {
            { "role", "system" },
            { "content", GetStructureGenerationSystemPrompt() }
        });

        // User request
        messages.Add(new Dictionary<string, string>
        {
            { "role", "user" },
            { "content", prompt }
        });

        var requestData = new Dictionary<string, object>
        {
            { "model", modelName },
            { "messages", messages },
            { "temperature", 0.7f },  // Slightly creative but not random
            { "max_tokens", 2000 }    // Allow for larger structures
        };

        string jsonRequest = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonRequest);

        UnityWebRequest request = new UnityWebRequest(lmStudioUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        if (debugMode) Debug.Log($"[AI] Sending structure generation request...");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string response = request.downloadHandler.text;
            if (debugMode) Debug.Log($"[AI] Received structure response");

            ProcessStructureGenerationResponse(response);
        }
        else
        {
            string errorMsg = $"LM Studio request failed: {request.error}";
            Debug.LogError($"[AI] {errorMsg}");
            OnStructureGenerationFailed?.Invoke(errorMsg);
        }
    }

    /// <summary>
    /// Process AI response and create structure
    /// </summary>
    private void ProcessStructureGenerationResponse(string jsonResponse)
    {
        try
        {
            // Extract content from JSON response
            int contentStart = jsonResponse.IndexOf("\"content\":\"") + 11;
            int contentEnd = jsonResponse.IndexOf("\"", contentStart);

            if (contentStart < 11 || contentEnd <= contentStart)
            {
                OnStructureGenerationFailed?.Invoke("Failed to extract content from AI response");
                return;
            }

            string content = jsonResponse.Substring(contentStart, contentEnd - contentStart);
            content = content.Replace("\\n", "\n").Replace("\\\"", "\"");

            lastAIResponse = content;

            if (debugMode) Debug.Log($"[AI] Parsing structure from response:\n{content.Substring(0, Mathf.Min(200, content.Length))}...");

            // Parse structure
            StructureData structure = AIStructureParser.ParseAIResponse(content, out string error);

            if (structure == null)
            {
                string errorMsg = $"Failed to parse structure: {error}";
                Debug.LogError($"[AI] {errorMsg}");
                OnStructureGenerationFailed?.Invoke(errorMsg);
                return;
            }

            // Validate structure
            if (!AIStructureParser.ValidateStructure(structure, out string validationError))
            {
                string errorMsg = $"Structure validation failed: {validationError}";
                Debug.LogError($"[AI] {errorMsg}");
                OnStructureGenerationFailed?.Invoke(errorMsg);
                return;
            }

            Debug.Log($"[AI] Successfully generated structure: {structure.name} ({structure.blockCount} blocks)");

            // Save structure if enabled
            if (autoSaveGeneratedStructures && structureManager != null)
            {
                structureManager.SaveStructure(structure);
                Debug.Log($"[AI] Structure saved: {structure.name}");
            }

            // Invoke success callback
            OnStructureGenerated?.Invoke(structure);

            // Auto-build if enabled
            if (autoBuildGeneratedStructures && buildingManager != null && turtle != null)
            {
                Vector3 buildPosition = turtle.transform.position;
                buildingManager.BuildStructureAtPosition(structure, buildPosition);
                Debug.Log($"[AI] Auto-building structure at {buildPosition}");
            }
        }
        catch (Exception e)
        {
            string errorMsg = $"Exception processing structure response: {e.Message}";
            Debug.LogError($"[AI] {errorMsg}");
            OnStructureGenerationFailed?.Invoke(errorMsg);
        }
    }

    /// <summary>
    /// System prompt for structure generation mode
    /// </summary>
    private string GetStructureGenerationSystemPrompt()
    {
        return @"You are a Minecraft architect AI. Your task is to design structures for Minecraft turtles to build.

CAPABILITIES:
- Design buildings (houses, towers, castles, etc.)
- Create mechanical systems using Create mod (gears, belts, conveyor systems)
- Design industrial setups with pipes and cables from ATM10 mods
- Combine vanilla blocks with modded components

DESIGN PRINCIPLES:
1. REALISTIC PROPORTIONS: Houses are 5-10 blocks wide, 3-5 blocks tall
2. FUNCTIONAL DESIGN: Include doors, windows, proper lighting
3. MECHANICAL LOGIC: Create mod components must connect properly
4. STRUCTURAL INTEGRITY: Build solid foundations
5. AESTHETIC BALANCE: Mix materials for visual appeal

TECHNICAL REQUIREMENTS:
- Use ONLY blocks from the provided block library
- Coordinates are relative (0,0,0 is ground level)
- Y-axis: Build upward from 0
- Include structural supports for large builds
- Ensure turtles can physically build it (bottom-up construction)

OUTPUT FORMAT:
Respond with ONLY valid JSON or block list format. No explanations, just the structure data.

Be creative but practical. Design structures that are both beautiful and buildable.";
    }
}
