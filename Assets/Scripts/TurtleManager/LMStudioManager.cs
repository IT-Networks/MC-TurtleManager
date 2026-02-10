using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manager für LMStudio-Integration
/// Ermöglicht KI-gesteuerte Turtle-Operationen über LMStudio
/// </summary>
public class LMStudioManager : MonoBehaviour
{
    [Header("LMStudio Settings")]
    [SerializeField] private string lmStudioUrl = "http://localhost:1234/v1/chat/completions";
    [SerializeField] private string modelName = "local-model";
    [SerializeField] private float temperature = 0.7f;
    [SerializeField] private int maxTokens = 500;

    [Header("Integration Settings")]
    [SerializeField] private bool enableAutoPrompts = true;
    [SerializeField] private float promptInterval = 5f;

    [Header("World Reference")]
    [SerializeField] private TurtleWorldManager worldManager;

    [Header("Turtle References")]
    [SerializeField] private TurtleBaseManager turtleBaseManager;

    private Queue<string> responseQueue = new Queue<string>();
    private bool isProcessingRequest = false;

    private void Start()
    {
        // Initialize world manager reference
        if (worldManager == null)
        {
            worldManager = FindFirstObjectByType<TurtleWorldManager>();
        }

        if (worldManager == null)
        {
            Debug.LogError("LMStudioManager: TurtleWorldManager not found!");
        }

        // Initialize turtle base manager
        if (turtleBaseManager == null)
        {
            turtleBaseManager = GetComponent<TurtleBaseManager>() ?? FindFirstObjectByType<TurtleBaseManager>();
        }

        if (enableAutoPrompts)
        {
            StartCoroutine(AutoPromptLoop());
        }
    }

    /// <summary>
    /// Sendet einen Prompt an LMStudio
    /// </summary>
    public void SendPrompt(string prompt)
    {
        if (isProcessingRequest)
        {
            Debug.LogWarning("LMStudio request already in progress");
            return;
        }

        StartCoroutine(SendLMStudioRequest(prompt));
    }

    private IEnumerator SendLMStudioRequest(string prompt)
    {
        isProcessingRequest = true;

        // Erstelle Request-Body
        var requestBody = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = temperature,
            max_tokens = maxTokens
        };

        string jsonBody = JsonUtility.ToJson(requestBody);

        using (UnityWebRequest request = UnityWebRequest.Post(lmStudioUrl, jsonBody, "application/json"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                responseQueue.Enqueue(response);
                Debug.Log($"LMStudio response received: {response}");
            }
            else
            {
                Debug.LogError($"LMStudio request failed: {request.error}");
            }
        }

        isProcessingRequest = false;
    }

    private IEnumerator AutoPromptLoop()
    {
        while (enableAutoPrompts)
        {
            yield return new WaitForSeconds(promptInterval);

            if (!isProcessingRequest && worldManager != null && turtleBaseManager != null)
            {
                // Generiere automatischen Prompt basierend auf aktuellem Status
                string autoPrompt = GenerateContextAwarePrompt();
                SendPrompt(autoPrompt);
            }
        }
    }

    private string GenerateContextAwarePrompt()
    {
        // Beispiel-Prompt basierend auf Weltkontext
        return "What should the turtle do next?";
    }

    public bool HasPendingResponses()
    {
        return responseQueue.Count > 0;
    }

    public string GetNextResponse()
    {
        return responseQueue.Count > 0 ? responseQueue.Dequeue() : null;
    }
}
