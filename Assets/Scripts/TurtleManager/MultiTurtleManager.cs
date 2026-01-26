using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

/// <summary>
/// Manages multiple turtles, their status updates, and lifecycle
/// </summary>
public class MultiTurtleManager : MonoBehaviour
{
    [Header("Server Settings")]
    public string statusUrl = "http://192.168.178.211:4999/status/all";
    public float updateInterval = 1.0f;

    [Header("Prefabs")]
    public GameObject turtlePrefab;

    [Header("Spawn Settings")]
    public bool autoCreateVisuals = true;
    public float spawnHeight = 0.5f;

    private Dictionary<int, TurtleObject> turtles = new Dictionary<int, TurtleObject>();
    private bool isUpdating = false;

    public System.Action<TurtleObject> OnTurtleAdded;
    public System.Action<TurtleObject> OnTurtleRemoved;
    public System.Action OnTurtlesUpdated;

    private void Start()
    {
        if (turtlePrefab == null)
        {
            CreateDefaultTurtlePrefab();
        }

        StartCoroutine(UpdateTurtlesLoop());
    }

    private void CreateDefaultTurtlePrefab()
    {
        turtlePrefab = new GameObject("TurtlePrefab");
        turtlePrefab.AddComponent<TurtleObject>();
        turtlePrefab.AddComponent<TurtleVisualizer>();
        turtlePrefab.SetActive(false);
    }

    private IEnumerator UpdateTurtlesLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            yield return StartCoroutine(FetchAndUpdateTurtles());
        }
    }

    private IEnumerator FetchAndUpdateTurtles()
    {
        if (isUpdating) yield break;
        isUpdating = true;

        using (UnityWebRequest www = UnityWebRequest.Get(statusUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string jsonText = www.downloadHandler.text;
                ProcessTurtleStatusData(jsonText);
            }
            else
            {
                Debug.LogWarning($"Failed to fetch turtle status: {www.error}");
            }
        }

        isUpdating = false;
    }

    private void ProcessTurtleStatusData(string jsonText)
    {
        try
        {
            // Parse JSON (assuming format: {"entries": [{...}]})
            TurtleStatusResponse response = JsonUtility.FromJson<TurtleStatusResponse>(jsonText);

            if (response == null || response.entries == null)
            {
                Debug.LogWarning("Invalid turtle status response");
                return;
            }

            // Track which turtles are still active
            HashSet<int> activeTurtleIds = new HashSet<int>();

            foreach (var statusData in response.entries)
            {
                int turtleId = statusData.id;
                activeTurtleIds.Add(turtleId);

                // Convert status data
                TurtleStatus status = new TurtleStatus
                {
                    id = statusData.id,
                    label = statusData.label,
                    position = new Vector3Int(statusData.position.x, statusData.position.y, statusData.position.z),
                    direction = statusData.direction,
                    fuel = statusData.fuel,
                    status = statusData.status
                };

                // Update or create turtle
                if (turtles.ContainsKey(turtleId))
                {
                    UpdateTurtle(turtleId, status);
                }
                else
                {
                    CreateTurtle(turtleId, status);
                }
            }

            // Remove turtles that are no longer active
            var inactiveTurtles = turtles.Keys.Where(id => !activeTurtleIds.Contains(id)).ToList();
            foreach (var id in inactiveTurtles)
            {
                RemoveTurtle(id);
            }

            OnTurtlesUpdated?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing turtle status: {e.Message}");
        }
    }

    private void CreateTurtle(int turtleId, TurtleStatus status)
    {
        if (turtlePrefab == null)
        {
            Debug.LogError("Turtle prefab not set!");
            return;
        }

        Vector3 spawnPos = new Vector3(-status.position.x, status.position.y + spawnHeight, status.position.z);
        GameObject turtleObj = Instantiate(turtlePrefab, spawnPos, Quaternion.identity);
        turtleObj.name = $"Turtle_{turtleId}_{status.label}";
        turtleObj.SetActive(true);

        TurtleObject turtle = turtleObj.GetComponent<TurtleObject>();
        if (turtle == null)
        {
            turtle = turtleObj.AddComponent<TurtleObject>();
        }

        turtle.turtleId = turtleId;
        turtle.turtleName = status.label;
        turtle.UpdateStatus(status);

        // Setup managers for this turtle
        SetupTurtleManagers(turtleObj, turtleId);

        turtles[turtleId] = turtle;

        Debug.Log($"Created turtle: {status.label} (ID: {turtleId})");
        OnTurtleAdded?.Invoke(turtle);
    }

    private void SetupTurtleManagers(GameObject turtleObj, int turtleId)
    {
        // Add base manager
        var baseManager = turtleObj.AddComponent<TurtleBaseManager>();
        baseManager.defaultTurtleId = turtleId.ToString();

        // Add other managers as needed
        turtleObj.AddComponent<TurtleMovementManager>();
        turtleObj.AddComponent<TurtleMiningManager>();
        turtleObj.AddComponent<TurtleBuildingManager>();
        turtleObj.AddComponent<TurtleOperationManager>();
    }

    private void UpdateTurtle(int turtleId, TurtleStatus status)
    {
        if (turtles.TryGetValue(turtleId, out TurtleObject turtle))
        {
            turtle.UpdateStatus(status);
        }
    }

    private void RemoveTurtle(int turtleId)
    {
        if (turtles.TryGetValue(turtleId, out TurtleObject turtle))
        {
            Debug.Log($"Removing turtle: {turtle.turtleName} (ID: {turtleId})");
            OnTurtleRemoved?.Invoke(turtle);
            Destroy(turtle.gameObject);
            turtles.Remove(turtleId);
        }
    }

    public TurtleObject GetTurtleById(int turtleId)
    {
        return turtles.TryGetValue(turtleId, out TurtleObject turtle) ? turtle : null;
    }

    public List<TurtleObject> GetAllTurtles()
    {
        return turtles.Values.ToList();
    }

    public List<TurtleObject> GetAvailableTurtles()
    {
        return turtles.Values.Where(t => !t.isBusy).ToList();
    }

    public int GetTurtleCount()
    {
        return turtles.Count;
    }

    public int GetAvailableTurtleCount()
    {
        return turtles.Values.Count(t => !t.isBusy);
    }

    private void OnDestroy()
    {
        // Cleanup all turtles
        foreach (var turtle in turtles.Values)
        {
            if (turtle != null)
            {
                Destroy(turtle.gameObject);
            }
        }
        turtles.Clear();
    }
}

[System.Serializable]
public class TurtleStatusResponse
{
    public List<TurtleStatusData> entries;
}

[System.Serializable]
public class TurtleStatusData
{
    public int id;
    public string label;
    public PositionData position;
    public string direction;
    public int fuel;
    public string status;
}

[System.Serializable]
public class PositionData
{
    public int x;
    public int y;
    public int z;
}
