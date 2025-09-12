using UnityEngine;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

public class RTSController : MonoBehaviour
{
    private List<Vector3> optimizedPath = new List<Vector3>();
    private bool isSelected = false;
    private Renderer rend;
    public Color selectedColor = Color.green;
    private Color originalColor;
    public string serverUrl = "http://192.168.178.211:4999";
    public bool isMoving = false;
    public bool isRefueling = false;
    public Vector3Int? lastTargetPos = null;

    [SerializeField] private float rotationSpeed = 180f; // Degrees per second
    [SerializeField] private float moveSpeed = .9f;      // Units per second
    [SerializeField] private float moveHeight = 0.5f;   // Height adjustment for movement

    public Vector3Int refuelPos = new Vector3Int(1022, -397, -5);
    public bool useTeleportForRefuel = true;
    private Pathfinding3D pathfinder;

    private bool readStatus;
    // Status der Turtle
    public TurtleStatus status = new TurtleStatus();

    void Start()
    {
        refuelPos = new Vector3Int(1040, 66, -378);
       // pathfinder = new Pathfinding3D(FindFirstObjectByType<TurtleWorldManager>().spawnedBlocks);
        rend = GetComponent<Renderer>();
        originalColor = rend.material.color;
        StartCoroutine(UpdateStatusLoop());
    }

    void Update()
    {
        if (readStatus)
        {
            HandleSelection();

            if (status.fuelLevel <= 2000 && !isRefueling && !isMoving)
            {
                // Wenn Fuel-Level niedrig, starte Refuel
                if (useTeleportForRefuel)
                {
                    Debug.Log("Teleportiere zur Tankstelle...");
                    transform.position = refuelPos;
                    isRefueling = true;
                    StartCoroutine(SendRefuelCommand());
                }
                else
                {
                    isRefueling = true;
                    Debug.Log("Starte Refuel-Prozess...");
                    StartCoroutine(SendRefuelCommand());
                }
            }      
            if (isSelected && Input.GetMouseButtonDown(1) && !isMoving && !isRefueling)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    StartCoroutine(MoveAndSendCommands(hit.point));
                }
            }
        }    
    }
    private void OnDrawGizmos()
{

    if (optimizedPath == null || optimizedPath.Count < 2) return;
    
    Gizmos.color = Color.green;
    
    // Draw each step separately
    for (int i = 0; i < optimizedPath.Count - 1; i++)
    {
        Vector3 start = optimizedPath[i];
        Vector3 end = optimizedPath[i+1];
        
        // Break movement into axis-aligned segments
        if (Mathf.Abs(start.x - end.x) > 0.1f)
        {
            // X movement
            Vector3 intermediate = new Vector3(end.x, start.y, start.z);
            Gizmos.DrawLine(start, intermediate);
            Gizmos.DrawSphere(intermediate, 0.1f);
            Gizmos.DrawLine(intermediate, end);
        }
        else if (Mathf.Abs(start.z - end.z) > 0.1f)
        {
            // Z movement
            Vector3 intermediate = new Vector3(start.x, start.y, end.z);
            Gizmos.DrawLine(start, intermediate);
            Gizmos.DrawSphere(intermediate, 0.1f);
            Gizmos.DrawLine(intermediate, end);
        }
        else
        {
            // Y movement
            Gizmos.DrawLine(start, end);
        }
        
        Gizmos.DrawSphere(start, 0.15f);
    }
    
    Gizmos.DrawSphere(optimizedPath[optimizedPath.Count-1], 0.15f);
}

    void HandleSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == this.transform)
                {
                    isSelected = true;
                    rend.material.color = selectedColor;
                }
                else
                {
                    isSelected = false;
                    rend.material.color = originalColor;
                }
            }
        }
    }

    IEnumerator MoveAndSendCommands(Vector3 target)
    {   
        isMoving = true;
        lastTargetPos = Vector3Int.RoundToInt(target);
        Vector3Int current = Vector3Int.RoundToInt(transform.position);
        Vector3Int destination = Vector3Int.RoundToInt(target);
        //DirectionUtils.SetFacingDirection(gameObject, status.direction);

        List<Vector3> rawpath = pathfinder.FindPath(current, destination);
        if(rawpath == null || rawpath.Count == 0)
        {
            Debug.LogWarning("Kein Pfad gefunden!");
            isMoving = false;
            yield break;
        }
        optimizedPath = pathfinder.CleanPath(rawpath);
        List<string> pathListinverted = CommandConverter3D.ConvertPathToCommands(optimizedPath, status.direction, true);

        yield return StartCoroutine(SendBatchCommands(pathListinverted));

        foreach (var cmd in optimizedPath)
            yield return MoveOneBlock(cmd);

        isMoving = false;
    }

    IEnumerator SendRefuelCommand()
    {
        if (isRefueling || isMoving) yield break;
        isRefueling = true;

        Debug.Log("Refuel gestartet...");
        yield return StartCoroutine(MoveAndSendCommands(refuelPos));

        // Command an Server senden, um Refuel durchzuführen (Lua übernimmt den Rest)
        using (HttpClient client = new HttpClient())
        {
            var command = new
            {
                label = gameObject.name,
                command = "refuel"
            };

            string json = JsonUtility.ToJson(command);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            Task<HttpResponseMessage> task = client.PostAsync(serverUrl + "/command", content);
            while (!task.IsCompleted) yield return null;

            if (!task.Result.IsSuccessStatusCode)
            {
                Debug.LogWarning("Refuel-Befehl fehlgeschlagen: " + task.Result.StatusCode);
                isRefueling = false;
                yield break;
            }
        }

        // Warten, bis Turtle genug getankt hat (Timeout: 20s)
        float timer = 0f;
        while (status.fuelLevel <= 2500 && timer < 20f)
        {
            yield return GetStatusFromServer();
            yield return new WaitForSeconds(1f);
            timer += 1f;
        }

        Debug.Log("Refuel abgeschlossen. Aktuelles Fuel: " + status.fuelLevel);

        // Zur letzten Zielposition zurück (falls vorhanden)
        if (lastTargetPos.HasValue)
        {
            Debug.Log("Zurück zur letzten Position: " + lastTargetPos.Value);
            yield return StartCoroutine(MoveAndSendCommands(lastTargetPos.Value));
        }

        isRefueling = false;
    }

            public IEnumerator MoveOneBlock(Vector3 targetPos)
    {
        // 1. Calculate direction to target (ignore Y for rotation)
        Vector3 direction = targetPos - transform.position;
        direction.y = 0;
        
        // 2. Snap to nearest cardinal direction (N/S/E/W)
        Vector3 moveDirection = SnapToCardinal(direction);
        
        // 3. Face the movement direction
        yield return FaceDirection(moveDirection);
        
        // 4. Calculate final position (1 block away, keeping target Y)
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + moveDirection;
        endPos.y = targetPos.y; // Use the target's Y position
        
        // 5. Move in a straight line to the target
        yield return MoveStraight(endPos);
    }

    private Vector3 SnapToCardinal(Vector3 direction)
    {
        if (direction.magnitude < 0.1f) return Vector3.forward;
        
        // Choose the dominant axis
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            return new Vector3(Mathf.Sign(direction.x), 0, 0); // East/West
        else
            return new Vector3(0, 0, Mathf.Sign(direction.z)); // North/South
    }

    private IEnumerator FaceDirection(Vector3 direction)
    {
        if (direction == Vector3.zero) yield break;
        
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(direction);
        float angle = Quaternion.Angle(startRot, targetRot);
        float duration = angle / rotationSpeed;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(startRot, targetRot, elapsed/duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private IEnumerator MoveStraight(Vector3 endPos)
    {
        Vector3 startPos = transform.position;
        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / moveSpeed;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, elapsed/duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = endPos;
    }



    IEnumerator SendBatchCommands(List<string> cmds)
    {
        using (HttpClient client = new HttpClient())
        {
            var payload = new CommandPayload
            {
                label = gameObject.name,
                commands = cmds
            };

            string json = JsonUtility.ToJson(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            Task<HttpResponseMessage> task = client.PostAsync(serverUrl + "/commands", content);
            while (!task.IsCompleted) yield return null;

            if (!task.Result.IsSuccessStatusCode)
                Debug.LogWarning("Command send failed: " + task.Result.StatusCode);
        }
    }

    // --- STATUS-ABFRAGE ---
    IEnumerator UpdateStatusLoop()
    {
        while (true)
        {
            yield return GetStatusFromServer();
            readStatus = true;
            yield return new WaitForSeconds(2f); // Alle 2 Sekunden aktualisieren
        }
    }

    IEnumerator GetStatusFromServer()
    {
        using (HttpClient client = new HttpClient())
        {
            Task<HttpResponseMessage> task = client.GetAsync(serverUrl + "/status/" + gameObject.name);
            while (!task.IsCompleted) yield return null;

            if (task.Result.IsSuccessStatusCode)
            {
                Task<string> readTask = task.Result.Content.ReadAsStringAsync();
                while (!readTask.IsCompleted) yield return null;

                string json = readTask.Result;
                status = JsonUtility.FromJson<TurtleStatus>(json);
            }
            else
            {
                Debug.LogWarning("Status konnte nicht abgerufen werden.");
            }
        }
    }

    public bool IsBusy() => status.isBusy;
}

[System.Serializable]
public class CommandPayload
{
    public string label;
    public List<string> commands;
}
