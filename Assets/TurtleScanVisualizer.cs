using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using SimpleJSON;
using System;

public class TurtleScanVisualizer : MonoBehaviour
{
    public string scanUrl = "http://192.168.178.211:4999/report";
    public GameObject cubePrefab;
    public float blockScale = 0.5f;
    public float scanInterval = 10f;
    private List<GameObject> spawnedBlocks = new List<GameObject>();

    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    public Material defaultMaterial;

    void Start() => InvokeRepeating(nameof(UpdateScan), 0, scanInterval);

    void UpdateScan() => StartCoroutine(FetchAndRenderScan());

    IEnumerator FetchAndRenderScan()
    {
        UnityWebRequest req = UnityWebRequest.Get(scanUrl);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            ClearOldBlocks();
            var json = JSON.Parse(req.downloadHandler.text);
            foreach (JSONNode block in json.AsArray)
            {
                Vector3 pos = new Vector3(block["x"], block["y"], block["z"]) * blockScale;
                GameObject go = Instantiate(cubePrefab, pos, Quaternion.identity);
                go.transform.localScale = Vector3.one * blockScale;
                string name = block["name"].ToString().Replace("minecraft:", "").Replace("\"", "");
                go.GetComponent<Renderer>().material = GetMaterialForBlock(name);
                if (name.Contains("bigreactors")){
                    //breakpoint for debugging
                    Debug.Log("Big Reactors block detected: " + name);
                }
                go.name = name;
                spawnedBlocks.Add(go);
            }
        }
    }

    void ClearOldBlocks()
    {
        foreach (var go in spawnedBlocks) Destroy(go);
        spawnedBlocks.Clear();
    }

    Material GetMaterialForBlock(string blockType)
{
    if (materialCache.ContainsKey(blockType))
        return materialCache[blockType];

    Texture2D texture = Resources.Load<Texture2D>("Textures/Minecraft/" + blockType);
    if (texture != null)
    {
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = texture;
        materialCache[blockType] = mat;
        return mat;
    }

    return defaultMaterial; // z. B. grauer Cube
}

}
