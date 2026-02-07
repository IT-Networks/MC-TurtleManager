using UnityEngine;

/// <summary>
/// Visualizes a Minecraft ComputerCraft Turtle in Unity
/// Creates a 3D cube representation with direction indicator
/// </summary>
public class TurtleVisualizer : MonoBehaviour
{
    [Header("Appearance")]
    [Tooltip("Color of the turtle body")]
    public Color bodyColor = new Color(0.5f, 0.5f, 0.5f); // Gray

    [Tooltip("Color of the direction indicator (front face)")]
    public Color frontColor = new Color(0.2f, 0.8f, 0.2f); // Green

    [Tooltip("Size of the turtle cube")]
    public float turtleSize = 0.9f;

    [Header("Label")]
    [Tooltip("Show turtle label above it")]
    public bool showLabel = true;

    [Tooltip("Label text (turtle name)")]
    public string labelText = "Turtle";

    [Tooltip("Label offset above turtle")]
    public float labelOffsetY = 1.5f;

    [Header("Animation")]
    [Tooltip("Enable subtle idle animation")]
    public bool enableIdleAnimation = true;

    [Tooltip("Bobbing animation speed")]
    public float bobbingSpeed = 1f;

    [Tooltip("Bobbing animation amplitude")]
    public float bobbingAmplitude = 0.05f;

    private GameObject bodyObject;
    private GameObject frontIndicator;
    private GameObject labelObject;
    private float animationTime = 0f;
    private Vector3 originalPosition;

    void Start()
    {
        CreateTurtleVisual();
        originalPosition = transform.position;
    }

    void CreateTurtleVisual()
    {
        // Create main body cube
        bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bodyObject.name = "TurtleBody";
        bodyObject.transform.SetParent(transform);
        bodyObject.transform.localPosition = Vector3.zero;
        bodyObject.transform.localScale = Vector3.one * turtleSize;

        // Setup body material (HDRP compatible)
        Renderer bodyRenderer = bodyObject.GetComponent<Renderer>();
        Material bodyMat = CreateHDRPMaterial();
        bodyMat.color = bodyColor;
        // HDRP uses _Smoothness instead of _Glossiness
        if (bodyMat.HasProperty("_Metallic"))
            bodyMat.SetFloat("_Metallic", 0.3f);
        if (bodyMat.HasProperty("_Smoothness"))
            bodyMat.SetFloat("_Smoothness", 0.6f);
        bodyRenderer.material = bodyMat;

        // Create front direction indicator
        frontIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frontIndicator.name = "FrontIndicator";
        frontIndicator.transform.SetParent(bodyObject.transform);
        frontIndicator.transform.localPosition = new Vector3(0, 0, 0.51f); // Slightly in front
        frontIndicator.transform.localScale = new Vector3(0.6f, 0.6f, 0.05f);

        // Setup front indicator material (HDRP compatible)
        Renderer frontRenderer = frontIndicator.GetComponent<Renderer>();
        Material frontMat = CreateHDRPMaterial();
        frontMat.color = frontColor;
        if (frontMat.HasProperty("_Metallic"))
            frontMat.SetFloat("_Metallic", 0.5f);
        if (frontMat.HasProperty("_Smoothness"))
            frontMat.SetFloat("_Smoothness", 0.8f);
        // Enable emission for HDRP
        if (frontMat.HasProperty("_EmissiveColor"))
        {
            frontMat.EnableKeyword("_EMISSION");
            frontMat.SetColor("_EmissiveColor", frontColor * 0.3f);
        }
        else if (frontMat.HasProperty("_EmissionColor"))
        {
            frontMat.EnableKeyword("_EMISSION");
            frontMat.SetColor("_EmissionColor", frontColor * 0.3f);
        }
        frontRenderer.material = frontMat;

        // Remove colliders (we don't need physics for visualization)
        DestroyImmediate(bodyObject.GetComponent<Collider>());
        DestroyImmediate(frontIndicator.GetComponent<Collider>());

        // Create label
        if (showLabel)
        {
            CreateLabel();
        }
    }

    void CreateLabel()
    {
        labelObject = new GameObject("TurtleLabel");
        labelObject.transform.SetParent(transform);
        labelObject.transform.localPosition = new Vector3(0, labelOffsetY, 0);

        // Create text mesh
        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = labelText;
        textMesh.fontSize = 24;
        textMesh.color = Color.white;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.1f;

        // Make label always face camera
        labelObject.AddComponent<Billboard>();

        // Add subtle background
        GameObject labelBg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        labelBg.name = "LabelBackground";
        labelBg.transform.SetParent(labelObject.transform);
        labelBg.transform.localPosition = new Vector3(0, 0, 0.01f);
        labelBg.transform.localScale = new Vector3(labelText.Length * 0.12f, 0.3f, 1f);

        Renderer bgRenderer = labelBg.GetComponent<Renderer>();
        Material bgMat = CreateHDRPUnlitMaterial();
        bgMat.color = new Color(0, 0, 0, 0.7f);
        bgRenderer.material = bgMat;

        DestroyImmediate(labelBg.GetComponent<Collider>());
    }

    /// <summary>
    /// Creates an HDRP-compatible material (Lit shader)
    /// </summary>
    Material CreateHDRPMaterial()
    {
        // Try HDRP/Lit first
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null)
        {
            // Fallback to Standard (for non-HDRP projects)
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            // Last resort fallback
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }

    /// <summary>
    /// Creates an HDRP-compatible unlit material (for UI elements)
    /// </summary>
    Material CreateHDRPUnlitMaterial()
    {
        // Try HDRP/Unlit first (better for transparent overlays)
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
        {
            // Fallback to Standard
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            // Last resort
            shader = Shader.Find("Unlit/Color");
        }

        Material mat = new Material(shader);

        // Enable transparency for HDRP
        if (mat.HasProperty("_SurfaceType"))
        {
            mat.SetFloat("_SurfaceType", 1); // Transparent
            mat.SetFloat("_BlendMode", 0); // Alpha blend
            mat.SetFloat("_AlphaCutoffEnable", 0);

            // CRITICAL FOR VISIBILITY: Enable double-sided rendering
            if (mat.HasProperty("_DoubleSidedEnable"))
            {
                mat.SetFloat("_DoubleSidedEnable", 1);
            }
            if (mat.HasProperty("_CullMode"))
            {
                mat.SetFloat("_CullMode", 0); // Off (show both sides)
            }

            // Enable keywords
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
        }
        else
        {
            // Standard pipeline transparency setup
            mat.SetFloat("_Mode", 3); // Transparent mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull", 0); // Disable culling for visibility
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        return mat;
    }

    void Update()
    {
        if (enableIdleAnimation)
        {
            // Subtle bobbing animation
            animationTime += Time.deltaTime * bobbingSpeed;
            float yOffset = Mathf.Sin(animationTime) * bobbingAmplitude;
            transform.position = originalPosition + new Vector3(0, yOffset, 0);
        }
    }

    /// <summary>
    /// Updates the turtle's label text
    /// </summary>
    public void SetLabel(string text)
    {
        labelText = text;
        if (labelObject != null)
        {
            TextMesh textMesh = labelObject.GetComponent<TextMesh>();
            if (textMesh != null)
            {
                textMesh.text = text;
            }
        }
        else if (showLabel)
        {
            CreateLabel();
        }
    }

    /// <summary>
    /// Updates the turtle's body color
    /// </summary>
    public void SetBodyColor(Color color)
    {
        bodyColor = color;
        if (bodyObject != null)
        {
            Renderer renderer = bodyObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }
    }

    /// <summary>
    /// Updates the turtle's front indicator color
    /// </summary>
    public void SetFrontColor(Color color)
    {
        frontColor = color;
        if (frontIndicator != null)
        {
            Renderer renderer = frontIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                renderer.material.SetColor("_EmissionColor", color * 0.3f);
            }
        }
    }

    /// <summary>
    /// Sets the original position for animation reference
    /// </summary>
    public void SetOriginalPosition(Vector3 pos)
    {
        originalPosition = pos;
    }

    /// <summary>
    /// Toggle label visibility
    /// </summary>
    public void SetLabelVisible(bool visible)
    {
        showLabel = visible;
        if (labelObject != null)
        {
            labelObject.SetActive(visible);
        }
    }

    void OnDestroy()
    {
        // Cleanup created objects
        if (bodyObject != null) Destroy(bodyObject);
        if (frontIndicator != null) Destroy(frontIndicator);
        if (labelObject != null) Destroy(labelObject);
    }
}

/// <summary>
/// Simple billboard component to make labels face the camera
/// </summary>
public class Billboard : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void LateUpdate()
    {
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
