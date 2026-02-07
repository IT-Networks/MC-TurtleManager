using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;

/// <summary>
/// Diagnostic tool to debug HDRP visibility issues in Game View
/// Attach this to any GameObject and press 'D' in Play Mode to run diagnostics
/// </summary>
public class HDRPVisibilityDiagnostics : MonoBehaviour
{
    [Header("Diagnostics")]
    [Tooltip("Press this key in Play Mode to run diagnostics")]
    public KeyCode diagnosticKey = KeyCode.D;

    void Update()
    {
        if (Input.GetKeyDown(diagnosticKey))
        {
            RunDiagnostics();
        }
    }

    [ContextMenu("Run Diagnostics")]
    public void RunDiagnostics()
    {
        Debug.Log("=== HDRP VISIBILITY DIAGNOSTICS ===");
        Debug.Log("");

        CheckCameras();
        CheckLineRenderers();
        CheckMaterials();
        CheckVolumes();

        Debug.Log("");
        Debug.Log("=== DIAGNOSTICS COMPLETE ===");
    }

    void CheckCameras()
    {
        Debug.Log("--- CAMERA CHECK ---");
        Camera[] cameras = FindObjectsOfType<Camera>();

        foreach (var cam in cameras)
        {
            Debug.Log($"Camera: {cam.name}");
            Debug.Log($"  Enabled: {cam.enabled}");
            Debug.Log($"  Culling Mask: {cam.cullingMask} (Everything = 4294967295)");
            Debug.Log($"  Clear Flags: {cam.clearFlags}");

            // Check HDRP Additional Camera Data
            var hdCamData = cam.GetComponent<HDAdditionalCameraData>();
            if (hdCamData != null)
            {
                Debug.Log($"  HDRP Camera Data: Found");
                Debug.Log($"  Rendering Path: {hdCamData.GetType().Name}");

                // Check if transparent rendering is enabled
                var frameSettings = hdCamData.renderingPathCustomFrameSettings;
                Debug.Log($"  Frame Settings Override: {hdCamData.customRenderingSettings}");
            }
            else
            {
                Debug.LogWarning($"  HDRP Camera Data: MISSING! Add 'HD Additional Camera Data' component!");
            }

            Debug.Log("");
        }
    }

    void CheckLineRenderers()
    {
        Debug.Log("--- LINE RENDERER CHECK ---");
        LineRenderer[] lineRenderers = FindObjectsOfType<LineRenderer>();

        Debug.Log($"Found {lineRenderers.Length} LineRenderer(s)");

        foreach (var lr in lineRenderers)
        {
            Debug.Log($"LineRenderer: {lr.gameObject.name}");
            Debug.Log($"  Enabled: {lr.enabled}");
            Debug.Log($"  GameObject Active: {lr.gameObject.activeInHierarchy}");
            Debug.Log($"  Layer: {LayerMask.LayerToName(lr.gameObject.layer)} (Index: {lr.gameObject.layer})");

            // Check rendering layer mask (HDRP specific)
            var renderer = lr.GetComponent<Renderer>();
            if (renderer != null)
            {
                Debug.Log($"  Rendering Layer Mask: {renderer.renderingLayerMask}");
            }

            Debug.Log($"  Position Count: {lr.positionCount}");
            Debug.Log($"  Start Width: {lr.startWidth}");
            Debug.Log($"  End Width: {lr.endWidth}");

            if (lr.sharedMaterial != null)
            {
                Debug.Log($"  Material: {lr.sharedMaterial.name}");
                Debug.Log($"  Shader: {lr.sharedMaterial.shader.name}");
                Debug.Log($"  Render Queue: {lr.sharedMaterial.renderQueue}");

                // Check HDRP properties
                if (lr.sharedMaterial.HasProperty("_SurfaceType"))
                {
                    Debug.Log($"  _SurfaceType: {lr.sharedMaterial.GetFloat("_SurfaceType")} (0=Opaque, 1=Transparent)");
                }
                if (lr.sharedMaterial.HasProperty("_BlendMode"))
                {
                    Debug.Log($"  _BlendMode: {lr.sharedMaterial.GetFloat("_BlendMode")}");
                }

                // Check enabled keywords
                var enabledKeywords = lr.sharedMaterial.shaderKeywords;
                Debug.Log($"  Enabled Keywords: {string.Join(", ", enabledKeywords)}");
            }
            else
            {
                Debug.LogWarning($"  Material: MISSING!");
            }

            Debug.Log("");
        }
    }

    void CheckMaterials()
    {
        Debug.Log("--- MATERIAL CHECK ---");

        // Find all materials with potential transparency issues
        var materials = Resources.FindObjectsOfTypeAll<Material>();
        int transparentCount = 0;

        foreach (var mat in materials)
        {
            if (mat.shader == null) continue;
            if (mat.shader.name.Contains("HDRP") && mat.HasProperty("_SurfaceType"))
            {
                float surfaceType = mat.GetFloat("_SurfaceType");
                if (surfaceType == 1) // Transparent
                {
                    transparentCount++;

                    // Check if render queue is correct
                    if (mat.renderQueue < 3000)
                    {
                        Debug.LogWarning($"Material '{mat.name}' is transparent but has wrong render queue: {mat.renderQueue} (should be 3000+)");
                        Debug.LogWarning($"  FIX: Set material.renderQueue = 3000");
                    }
                }
            }
        }

        Debug.Log($"Found {transparentCount} transparent HDRP materials");
        Debug.Log("");
    }

    void CheckVolumes()
    {
        Debug.Log("--- VOLUME CHECK ---");
        var volumes = FindObjectsOfType<UnityEngine.Rendering.Volume>();

        Debug.Log($"Found {volumes.Length} Volume(s)");

        foreach (var volume in volumes)
        {
            Debug.Log($"Volume: {volume.gameObject.name}");
            Debug.Log($"  Is Global: {volume.isGlobal}");
            Debug.Log($"  Weight: {volume.weight}");
            Debug.Log($"  Priority: {volume.priority}");
            Debug.Log($"  Profile: {(volume.profile != null ? volume.profile.name : "NONE")}");
            Debug.Log("");
        }
    }
}
