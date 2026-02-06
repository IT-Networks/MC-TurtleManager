using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Base turtle management system - handles communication, status, and command queue
/// </summary>
public class TurtleBaseManager : MonoBehaviour
{
    [Header("Server Settings")]
    public string turtleCommandUrl = "http://192.168.178.211:4999/command";
    public string turtleStatusUrl = "http://192.168.178.211:4999/status";

    [Header("Command Settings")]
    public float commandDelay = 2.1f;
    public int maxRetries = 3;
    public bool debugCommands = true;

    [Header("Turtle Settings")]
    public string defaultTurtleId = "TurtleSlave";
    public float positionUpdateDelay = 0.5f;

    // Core state
    protected Queue<TurtleCommand> commandQueue = new Queue<TurtleCommand>();
    protected bool isExecutingCommands = false;
    protected TurtleBaseStatus currentTurtleBaseStatus;
    public TurtleWorldManager worldManager;

    // Events
    public System.Action<string> OnCommandExecuted;
    public System.Action<string> OnCommandFailed;
    public System.Action<TurtleBaseStatus> OnStatusUpdated;

    protected virtual void Start()
    {
        // Allow Unity to continue running in background - critical for command processing
        Application.runInBackground = true;

        worldManager = GetComponent<TurtleWorldManager>() ?? FindFirstObjectByType<TurtleWorldManager>();
        StartCoroutine(UpdateTurtleBaseStatus());
        StartCoroutine(ProcessCommandQueue());
    }

    #region Status Management

    protected IEnumerator UpdateTurtleBaseStatus()
    {
        while (true)
        {
            yield return StartCoroutine(FetchTurtleBaseStatus());
            yield return new WaitForSeconds(1f);
        }
    }

    protected IEnumerator FetchTurtleBaseStatus()
    {
        using UnityWebRequest request = UnityWebRequest.Get(turtleStatusUrl + "/" + defaultTurtleId);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var statusWrapper = JsonUtility.FromJson<TurtleWorldManager.StatusWrapper>(
                    "{\"entries\":[" + request.downloadHandler.text + "]}");
                    
                if (statusWrapper?.entries != null && statusWrapper.entries.Count > 0)
                {
                    var status = statusWrapper.entries[0];
                    var newStatus = new TurtleBaseStatus
                    {
                        label = status.label,
                        direction = status.direction,
                        position = new Position {
                            x = (int)status.position.x,  // Store RAW Minecraft X (conversion in GetTurtlePosition)
                            y = (int)status.position.y,  // Store RAW Minecraft Y (conversion in GetTurtlePosition)
                            z = (int)status.position.z   // Store RAW Minecraft Z (no conversion needed)
                        },
                        fuelLevel = 1000,
                        isBusy = false
                    };

                    if (HasStatusChanged(currentTurtleBaseStatus, newStatus))
                    {
                        currentTurtleBaseStatus = newStatus;
                        OnStatusUpdated?.Invoke(currentTurtleBaseStatus);
                        
                        if (debugCommands)
                        {
                            Debug.Log($"Turtle Status Update: Pos({currentTurtleBaseStatus.position.x}, " +
                                     $"{currentTurtleBaseStatus.position.y}, {currentTurtleBaseStatus.position.z}), " +
                                     $"Facing: {currentTurtleBaseStatus.direction}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to parse turtle status: {ex.Message}");
            }
        }
    }

    private bool HasStatusChanged(TurtleBaseStatus old, TurtleBaseStatus newStatus)
    {
        if (old == null) return true;
        
        return old.position.x != newStatus.position.x ||
               old.position.y != newStatus.position.y ||
               old.position.z != newStatus.position.z ||
               old.direction != newStatus.direction;
    }

    #endregion

    #region Command Processing

    protected IEnumerator ProcessCommandQueue()
    {
        while (true)
        {
            if (commandQueue.Count > 0 && !isExecutingCommands)
            {
                isExecutingCommands = true;
                TurtleCommand command = commandQueue.Dequeue();

                yield return StartCoroutine(ExecuteCommand(command));

                yield return new WaitForSeconds(commandDelay);
                isExecutingCommands = false;
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    protected IEnumerator ExecuteCommand(TurtleCommand command)
    {
        if (debugCommands)
        {
            Debug.Log($"Executing command: {command.command} for {command.turtleId}");
        }

        var commandData = new CommandData
        {
            label = command.turtleId,
            commands = new string[] { command.command }
        };

        string jsonData = JsonUtility.ToJson(commandData);

        using UnityWebRequest request = UnityWebRequest.Post(turtleCommandUrl, jsonData, "application/json");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            OnCommandExecuted?.Invoke(command.command);
            
            if (IsMovementCommand(command.command))
            {
                yield return new WaitForSeconds(positionUpdateDelay);
                yield return StartCoroutine(FetchTurtleBaseStatus());
            }
            
            if (debugCommands)
            {
                Debug.Log($"Command executed: {command.command}");
            }
        }
        else
        {
            command.retryCount++;
            if (command.retryCount < maxRetries)
            {
                Debug.LogWarning($"Command failed, retrying ({command.retryCount}/{maxRetries}): {command.command}");
                commandQueue.Enqueue(command);
            }
            else
            {
                OnCommandFailed?.Invoke(command.command);
                Debug.LogError($"Command failed after {maxRetries} attempts: {command.command} - {request.error}");
            }
        }
    }

    protected bool IsMovementCommand(string command)
    {
        return command == "forward" || command == "back" || 
               command == "up" || command == "down" ||
               command == "left" || command == "right";
    }

    #endregion

    #region Public API

    public void QueueCommand(TurtleCommand command)
    {
        commandQueue.Enqueue(command);
    }

    public void ClearCommandQueue()
    {
        commandQueue.Clear();
    }

    public Vector3 GetTurtlePosition()
    {
        if (currentTurtleBaseStatus == null) return Vector3.zero;

        // Convert RAW Minecraft coordinates to Unity coordinates
        // This MUST match TurtleObject.UpdateStatus() exactly: new Vector3(-status.position.x, status.position.y + offset, status.position.z)
        return new Vector3(
            -currentTurtleBaseStatus.position.x,  // Negate X for Unity (Minecraft +X = Unity -X)
            currentTurtleBaseStatus.position.y + MultiTurtleManager.WorldYOffset,  // Apply Y offset (Minecraft Y=-64 -> Unity Y=0)
            currentTurtleBaseStatus.position.z    // Z stays the same
        );
    }

    public TurtleBaseStatus GetCurrentStatus() => currentTurtleBaseStatus;
    public int QueuedCommands => commandQueue.Count;
    public bool IsBusy => isExecutingCommands || commandQueue.Count > 0;

    public void EmergencyStop()
    {
        StopAllCoroutines();
        ClearCommandQueue();
        isExecutingCommands = false;
        
        Debug.Log("Emergency stop executed");

        StartCoroutine(UpdateTurtleBaseStatus());
        StartCoroutine(ProcessCommandQueue());
    }

    #endregion

    protected virtual void OnDestroy()
    {
        StopAllCoroutines();
    }
}

#region Supporting Classes

[System.Serializable]
public class TurtleCommand
{
    public string command;
    public string turtleId;
    public Vector3 targetPosition;
    public string blockType;
    public int retryCount;
    public bool isOptimized;
    public bool requiresPositioning;

    public TurtleCommand(string cmd, string id = null)
    {
        command = cmd;
        turtleId = id ?? "TurtleSlave";
        retryCount = 0;
        isOptimized = false;
        requiresPositioning = false;
    }
}

[System.Serializable]
public class CommandData
{
    public string label;
    public string[] commands;
}

[System.Serializable]
public class Position
{
    public int x, y, z;
}

[System.Serializable]
public class TurtleBaseStatus
{
    public string label;
    public string direction;
    public Position position;
    public int fuelLevel;
    public bool isBusy;
}

#endregion