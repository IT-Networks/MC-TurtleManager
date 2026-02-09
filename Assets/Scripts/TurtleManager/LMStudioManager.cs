using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

/// <summary>
/// LM Studio AI Integration for Turtle Control and Structure Generation
///
/// ARCHITECTURE:
/// - Unity (C#) contains ALL intelligence and decision-making
/// - Turtle (Lua) is "dumb" - only executes simple commands and reports status
/// - LM Studio AI (optional) makes intelligent decisions for turtles
///
/// TWO MODES:
/// 1. TURTLE CONTROL: AI decides turtle actions (mine, move, refuel)
///    Flow: Turtle Status → Unity → LM Studio AI → Command Decision → Unity → Turtle Executes
///
/// 2. STRUCTURE GENERATION: AI designs structures from natural language prompts
///    Flow: User Prompt → LM Studio AI → JSON Structure → Parser → StructureData → Turtle Builds
///
/// TURTLE COMMANDS (executed by dumb turtle):
/// - Movement: forward, back, up, down, turnLeft, turnRight
/// - Mining: dig, digUp, digDown
/// - Building: place, placeUp, placeDown
/// - Inventory: select, drop, suck, refuel
///
/// The turtle has NO intelligence - it only:
/// 1. Executes commands received from Unity
/// 2. Reports status (position, fuel, inventory, nearby blocks)
/// 3. Returns to Unity for next command
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
        // Manually build JSON request (JsonUtility doesn't handle nested structures well)
        StringBuilder jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{");
        jsonBuilder.Append($"\"model\":\"{modelName}\",");
        jsonBuilder.Append("\"messages\":[");

        // System message
        jsonBuilder.Append("{\"role\":\"system\",\"content\":\"");
        jsonBuilder.Append(EscapeJsonString(conversationHistory[0]));
        jsonBuilder.Append("\"},");

        // User message
        jsonBuilder.Append("{\"role\":\"user\",\"content\":\"");
        jsonBuilder.Append(EscapeJsonString(userMessage));
        jsonBuilder.Append("\"}");

        jsonBuilder.Append("],");
        jsonBuilder.Append($"\"temperature\":{temperature},");
        jsonBuilder.Append($"\"max_tokens\":{maxTokens}");
        jsonBuilder.Append("}");

        string jsonRequest = jsonBuilder.ToString();
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
    /// Get system prompt for AI turtle control
    /// </summary>
    private string GetSystemPrompt()
    {
        return @"You are an AI making decisions for a Minecraft turtle (mining robot).

ROLE: Analyze the turtle's situation and decide the next action.

THE TURTLE IS ""DUMB"":
- The turtle ONLY executes simple commands you send
- It has NO intelligence of its own
- It reports status: position, fuel, inventory, nearby blocks
- YOU make ALL decisions based on this status

AVAILABLE COMMANDS:
Movement: forward, back, up, down, left, right
Mining: mine, mine_up, mine_down
Inventory: store_items, refuel
Utility: scan, wait

DECISION PRIORITY:
1. SURVIVAL: fuel < 20% → refuel immediately
2. INVENTORY: > 14/16 slots full → store_items
3. MINING: valuable ores detected → mine them
4. EFFICIENCY: minimize movement, save fuel
5. SAFETY: avoid mining into holes/lava

REQUIRED RESPONSE FORMAT:
COMMAND:<single_command>
REASON:<brief explanation>

Example:
COMMAND:refuel
REASON:Fuel at 15%, critical - must refuel before continuing

CONTEXT PROVIDED:
You will receive:
- Position (x, y, z)
- Fuel level (current/max)
- Inventory (slots used, items)
- Nearby blocks (ores detected)
- Current status

Make ONE decision per turn. The turtle executes it and reports back.
Unity sends your command to the turtle, turtle executes, reports status.
You are the brain, turtle is the body.";
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

        prompt.AppendLine("=== STRUCTURE GENERATION REQUEST ===");
        prompt.AppendLine();
        prompt.AppendLine($"USER WANTS: {userPrompt}");
        prompt.AppendLine();

        // Add block library (concise version to save tokens)
        prompt.AppendLine("AVAILABLE BLOCKS:");
        prompt.AppendLine(AIBlockLibrary.GetConciseBlockLibrary());
        prompt.AppendLine();

        prompt.AppendLine("REQUIRED OUTPUT FORMAT:");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"name\": \"Structure Name\",");
        prompt.AppendLine("  \"description\": \"Brief description\",");
        prompt.AppendLine("  \"blocks\": [");
        prompt.AppendLine("    {\"pos\": [x,y,z], \"type\": \"blocktype\"},");
        prompt.AppendLine("    {\"pos\": [x,y,z], \"type\": \"blocktype\"}");
        prompt.AppendLine("  ]");
        prompt.AppendLine("}");
        prompt.AppendLine();

        prompt.AppendLine("CRITICAL RULES:");
        prompt.AppendLine("1. Output ONLY valid JSON - no explanations, no markdown code blocks");
        prompt.AppendLine("2. Use blocks ONLY from the library above");
        prompt.AppendLine("3. Coordinates start at [0,0,0] (ground level), Y increases upward");
        prompt.AppendLine("4. Include foundation, walls, roof, doors, windows, lighting");
        prompt.AppendLine("5. For Create mod: connect gears/shafts logically");
        prompt.AppendLine("6. Size: reasonable (houses ~5-10 blocks wide, 3-5 tall)");
        prompt.AppendLine();

        prompt.AppendLine("Generate the structure now. Output ONLY the JSON.");

        return prompt.ToString();
    }

    /// <summary>
    /// Send structure generation request to LM Studio
    /// </summary>
    private IEnumerator SendStructureGenerationRequest(string prompt)
    {
        // Manually build JSON request (JsonUtility doesn't handle nested dictionaries well)
        StringBuilder jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{");
        jsonBuilder.Append($"\"model\":\"{modelName}\",");
        jsonBuilder.Append("\"messages\":[");

        // System message
        jsonBuilder.Append("{\"role\":\"system\",\"content\":\"");
        jsonBuilder.Append(EscapeJsonString(GetStructureGenerationSystemPrompt()));
        jsonBuilder.Append("\"},");

        // User message
        jsonBuilder.Append("{\"role\":\"user\",\"content\":\"");
        jsonBuilder.Append(EscapeJsonString(prompt));
        jsonBuilder.Append("\"}");

        jsonBuilder.Append("],");
        jsonBuilder.Append("\"temperature\":0.7,");
        jsonBuilder.Append("\"max_tokens\":2000");
        jsonBuilder.Append("}");

        string jsonRequest = jsonBuilder.ToString();
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
        return @"You are a Minecraft architect AI designing structures for autonomous turtles to build.

ROLE: Generate buildable structures based on user requests.

CAPABILITIES:
- Design buildings: houses, towers, castles, bridges, etc.
- Create mechanical systems: Create mod gears, belts, conveyor systems
- Design industrial setups: pipes, cables, machines (ATM10 mods)
- Combine vanilla Minecraft blocks with modded components

DESIGN PRINCIPLES:
1. REALISTIC PROPORTIONS: Houses 5-10 blocks wide/deep, 3-5 blocks tall
2. FUNCTIONAL: Include doors, windows, lighting (torches/lanterns)
3. MECHANICAL LOGIC: Create mod gears/belts must connect properly
4. STRUCTURAL INTEGRITY: Solid foundations, no floating blocks
5. BUILDABLE: Bottom-up construction (Y=0 is ground, build upward)

TECHNICAL CONSTRAINTS:
- Use ONLY blocks from the provided block library
- Coordinates are relative (will be placed at turtle location)
- Start at Y=0 (ground level), build upward
- Turtles build bottom-up, one block at a time
- Maximum reasonable size: 20x20x20 blocks

OUTPUT FORMAT - CRITICAL:
You MUST respond with ONLY valid JSON in this EXACT format:
{
  ""name"": ""Structure Name"",
  ""description"": ""Brief description"",
  ""blocks"": [
    {""pos"": [0,0,0], ""type"": ""minecraft:stone""},
    {""pos"": [1,0,0], ""type"": ""minecraft:oak_planks""},
    {""pos"": [0,1,0], ""type"": ""create:cogwheel""}
  ]
}

IMPORTANT:
- NO explanations, NO commentary, ONLY the JSON structure
- Use double quotes for JSON strings
- Block types must exactly match the provided library
- Coordinates: [x, y, z] as integers
- Build from Y=0 upward

TURTLE EXECUTION:
The turtle is ""dumb"" and ONLY executes these commands:
- Movement: forward, back, up, down, turnLeft, turnRight
- Actions: dig, digUp, digDown, place, placeUp, placeDown
- Utility: select, refuel, drop, suck
The turtle receives your structure as block positions and builds automatically.

Be creative but practical. Design beautiful, functional, buildable structures.";
    }

    /// <summary>
    /// Escape string for JSON
    /// </summary>
    private string EscapeJsonString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text
            .Replace("\\", "\\\\")  // Backslash
            .Replace("\"", "\\\"")  // Quote
            .Replace("\n", "\\n")   // Newline
            .Replace("\r", "\\r")   // Carriage return
            .Replace("\t", "\\t");  // Tab
    }
}
