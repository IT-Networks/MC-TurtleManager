using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class TurtleMaster : MonoBehaviour
{
    public string serverUrl = "http://192.168.178.211:4999/command";
    public float moveDistance = 1f;
    public float stepTime = 0.5f;
    public float turnTime = 0.3f;

    private bool isBusy = false;

    void Update()
    {
        if (!isBusy)
        {
            if (Input.GetKeyDown(KeyCode.W))
                StartCoroutine(DoMove("forward", Vector3.forward));
            else if (Input.GetKeyDown(KeyCode.S))
                StartCoroutine(DoMove("back", Vector3.back));
            else if (Input.GetKeyDown(KeyCode.Space))
                StartCoroutine(DoMove("up", Vector3.up));
            else if (Input.GetKeyDown(KeyCode.LeftShift))
                StartCoroutine(DoMove("down", Vector3.down));
            else if (Input.GetKeyDown(KeyCode.A))
                StartCoroutine(DoTurnAndMove("left", -90f));
            else if (Input.GetKeyDown(KeyCode.D))
                StartCoroutine(DoTurnAndMove("right", 90f));
        }
    }

    IEnumerator DoMove(string command, Vector3 direction)
    {
        isBusy = true;

        Vector3 worldDir = transform.TransformDirection(direction);
        Vector3 targetPos = transform.position + worldDir * moveDistance;
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < stepTime)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / stepTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPos;

        yield return SendCommand(command);
        isBusy = false;
    }

    IEnumerator DoTurnAndMove(string turnCommand, float angle)
    {
        isBusy = true;

        // Drehung
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, angle, 0);
        float elapsed = 0f;

        while (elapsed < turnTime)
        {
            transform.rotation = Quaternion.Slerp(startRot, endRot, elapsed / turnTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = endRot;

        yield return SendCommand(turnCommand);

        // Nach der Drehung automatisch vorwärts bewegen
        yield return DoMove("forward", Vector3.forward);
    }

    IEnumerator SendCommand(string command)
    {
        UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");
        string jsonData = "{\"command\": \"" + command + "\"}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("Command sent: " + command);
        }
        else
        {
            Debug.LogWarning("Error sending command: " + request.error);
        }
    }
}
