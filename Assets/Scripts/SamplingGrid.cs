using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders object-detection style bounding boxes that reveal machine vision.
/// Shows rectangular grid overlays with pattern keywords (like CV object detection).
/// Supports multiple simultaneous detection boxes.
/// </summary>
public class SamplingGrid : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public TurbulenceClassifier classifier;
    public PerformanceTracker performanceTracker;

    [Header("Box Appearance")]
    [Tooltip("Base radius of the detection box")]
    public float baseRadius = 8f;

    [Tooltip("Line width of bounding box")]
    [Range(0.05f, 0.3f)]
    public float lineWidth = 0.15f;

    [Tooltip("Corner marker size")]
    [Range(0.3f, 1.5f)]
    public float cornerSize = 0.8f;

    [Tooltip("Color when idle")]
    public Color idleColor = new Color(0.40f, 0.44f, 0.50f, 0.55f);

    [Tooltip("Color when actively correcting")]
    public Color activeColor = new Color(0.52f, 0.56f, 0.62f, 0.80f);

    [Tooltip("Color when low energy")]
    public Color lowEnergyColor = new Color(0.50f, 0.42f, 0.36f, 0.45f);

    [Header("Pulse Effects")]
    [Tooltip("Idle pulse speed")]
    [Range(1f, 5f)]
    public float idlePulseSpeed = 2f;

    [Tooltip("Active pulse speed")]
    [Range(5f, 15f)]
    public float activePulseSpeed = 10f;

    [Tooltip("Pulse intensity")]
    [Range(0f, 0.5f)]
    public float pulseIntensity = 0.12f;

    [Header("Event Name Display")]
    [Tooltip("Custom font for event name label (assign Space Mono Regular)")]
    public Font customFont;

    [Tooltip("Font size for event name")]
    public int eventNameFontSize = 14;

    [Tooltip("Distance below grid center")]
    public float eventNameOffset = 1.5f;

    [Tooltip("Event name color")]
    public Color eventNameColor = new Color(0.52f, 0.54f, 0.58f, 0.80f);

    // Runtime state
    private Vector2 currentWorldPos;
    private bool isActive = false;
    private float energyRatio = 1f;

    // Rendering
    private LineRenderer[] boxLines;
    private GameObject[] cornerMarkers;
    private const int LINES_PER_BOX = 4; // Top, bottom, left, right
    private const int CORNERS_PER_BOX = 4;
    private GUIStyle eventNameStyle;
    private Mesh cornerMesh;
    private Material lineMaterial;

    // Grid sample positions (for external use)
    private Vector2[] samplePositions;
    public Vector2[] SamplePositions => samplePositions;

    void Start()
    {
        ValidateReferences();
        CreateBoundingBox();
        SetupGUIStyle();
        UpdateSamplePositions();
    }

    void ValidateReferences()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (classifier == null)
            classifier = FindObjectOfType<TurbulenceClassifier>();

        if (performanceTracker == null)
            performanceTracker = FindObjectOfType<PerformanceTracker>();
    }

    void CreateBoundingBox()
    {
        // Create line material
        lineMaterial = new Material(Shader.Find("Sprites/Default"));

        // Create 4 line renderers for box edges
        boxLines = new LineRenderer[LINES_PER_BOX];
        for (int i = 0; i < LINES_PER_BOX; i++)
        {
            GameObject lineObj = new GameObject($"BoxEdge_{i}");
            lineObj.transform.SetParent(transform);

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.sortingOrder = 100;

            boxLines[i] = line;
        }

        // Create corner markers
        cornerMesh = CreateSquareMesh();
        cornerMarkers = new GameObject[CORNERS_PER_BOX];

        for (int i = 0; i < CORNERS_PER_BOX; i++)
        {
            GameObject corner = new GameObject($"Corner_{i}");
            corner.transform.SetParent(transform);

            MeshRenderer renderer = corner.AddComponent<MeshRenderer>();
            MeshFilter filter = corner.AddComponent<MeshFilter>();

            renderer.material = lineMaterial;
            filter.mesh = cornerMesh;

            cornerMarkers[i] = corner;
        }
    }

    void SetupGUIStyle()
    {
        eventNameStyle = new GUIStyle();
        eventNameStyle.fontSize = eventNameFontSize;
        eventNameStyle.alignment = TextAnchor.MiddleCenter;
        eventNameStyle.normal.textColor = eventNameColor;

        if (customFont != null)
            eventNameStyle.font = customFont;
        else
            eventNameStyle.font = Font.CreateDynamicFontFromOSFont("Consolas", eventNameFontSize);
    }

    Mesh CreateSquareMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "CornerMarker";

        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };

        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void Update()
    {
        UpdateWorldPosition();
        UpdateBoundingBoxVisuals();
        UpdateSamplePositions();
    }

    void UpdateWorldPosition()
    {
        if (mainCamera == null) return;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        currentWorldPos = new Vector2(worldPos.x, worldPos.y);
    }

    void UpdateBoundingBoxVisuals()
    {
        if (boxLines == null || boxLines.Length == 0) return;

        float time = Time.time;
        float pulseSpeed = isActive ? activePulseSpeed : idlePulseSpeed;
        float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseIntensity;

        // Color based on state
        Color baseColor;
        if (energyRatio < 0.3f)
        {
            baseColor = lowEnergyColor;
        }
        else if (isActive)
        {
            baseColor = activeColor;
        }
        else
        {
            baseColor = idleColor;
        }

        // Apply energy modulation
        Color finalColor = baseColor * energyRatio;
        finalColor.a = baseColor.a * pulse;

        // Calculate box bounds
        float halfSize = baseRadius;
        Vector2 topLeft = currentWorldPos + new Vector2(-halfSize, halfSize);
        Vector2 topRight = currentWorldPos + new Vector2(halfSize, halfSize);
        Vector2 bottomLeft = currentWorldPos + new Vector2(-halfSize, -halfSize);
        Vector2 bottomRight = currentWorldPos + new Vector2(halfSize, -halfSize);

        // Draw bounding box lines
        // Top
        boxLines[0].SetPosition(0, new Vector3(topLeft.x, topLeft.y, -1f));
        boxLines[0].SetPosition(1, new Vector3(topRight.x, topRight.y, -1f));
        boxLines[0].startColor = finalColor;
        boxLines[0].endColor = finalColor;

        // Bottom
        boxLines[1].SetPosition(0, new Vector3(bottomLeft.x, bottomLeft.y, -1f));
        boxLines[1].SetPosition(1, new Vector3(bottomRight.x, bottomRight.y, -1f));
        boxLines[1].startColor = finalColor;
        boxLines[1].endColor = finalColor;

        // Left
        boxLines[2].SetPosition(0, new Vector3(topLeft.x, topLeft.y, -1f));
        boxLines[2].SetPosition(1, new Vector3(bottomLeft.x, bottomLeft.y, -1f));
        boxLines[2].startColor = finalColor;
        boxLines[2].endColor = finalColor;

        // Right
        boxLines[3].SetPosition(0, new Vector3(topRight.x, topRight.y, -1f));
        boxLines[3].SetPosition(1, new Vector3(bottomRight.x, bottomRight.y, -1f));
        boxLines[3].startColor = finalColor;
        boxLines[3].endColor = finalColor;

        // Update corner markers (L-shaped corners typical of detection boxes)
        Vector2[] corners = { topLeft, topRight, bottomRight, bottomLeft };
        for (int i = 0; i < cornerMarkers.Length; i++)
        {
            Vector3 pos = new Vector3(corners[i].x, corners[i].y, -1.5f);
            cornerMarkers[i].transform.position = pos;
            cornerMarkers[i].transform.localScale = Vector3.one * cornerSize * pulse;

            var renderer = cornerMarkers[i].GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Color cornerColor = finalColor;
                cornerColor.a *= 1.5f; // Corners more visible
                renderer.material.color = cornerColor;
            }
        }
    }

    void UpdateSamplePositions()
    {
        // Generate sample grid positions within box
        int gridSize = performanceTracker != null ? performanceTracker.CurrentGridSize : 5;
        int pointCount = gridSize * gridSize;

        if (samplePositions == null || samplePositions.Length != pointCount)
        {
            samplePositions = new Vector2[pointCount];
        }

        float spacing = (baseRadius * 2f) / (gridSize + 1);

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                float localX = (x - (gridSize - 1) * 0.5f) * spacing;
                float localY = (y - (gridSize - 1) * 0.5f) * spacing;

                int index = y * gridSize + x;
                samplePositions[index] = currentWorldPos + new Vector2(localX, localY);
            }
        }
    }

    void OnGUI()
    {
        if (classifier == null || mainCamera == null) return;

        // Detect event at cursor
        string eventName = classifier.DetectEventAt(currentWorldPos);

        if (!string.IsNullOrEmpty(eventName))
        {
            // Extract keyword from event name (e.g., "Spiral_Formation" → "VORTEX")
            string keyword = ExtractPatternKeyword(eventName);

            // Convert world position to screen space (above box)
            Vector3 worldPos = new Vector3(currentWorldPos.x, currentWorldPos.y + baseRadius + eventNameOffset, 0f);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            // Flip Y for GUI coordinates
            screenPos.y = Screen.height - screenPos.y;

            // Draw keyword with minimal styling
            Rect rect = new Rect(
                screenPos.x - 100,
                screenPos.y - eventNameFontSize / 2,
                200,
                eventNameFontSize + 10
            );

            // Slight fade based on active state
            Color nameColor = eventNameColor;
            if (isActive)
            {
                nameColor.a *= 0.9f + Mathf.Sin(Time.time * 8f) * 0.1f; // Subtle pulse when active
            }
            eventNameStyle.normal.textColor = nameColor;

            GUI.Label(rect, keyword, eventNameStyle);
        }
    }

    string ExtractPatternKeyword(string eventName)
    {
        // Convert event names to pattern keywords
        if (eventName.Contains("Circular") || eventName.Contains("Assembly") || eventName.Contains("Gather"))
            return "CIRCULAR";
        if (eventName.Contains("Scatter") || eventName.Contains("Panic"))
            return "SCATTER";
        if (eventName.Contains("Vortex") || eventName.Contains("Spiral"))
            return "VORTEX";
        if (eventName.Contains("Wave") || eventName.Contains("March"))
            return "WAVE";
        if (eventName.Contains("Oscillation"))
            return "OSCILLATION";
        if (eventName.Contains("Cluster") || eventName.Contains("Blockade") || eventName.Contains("Aftermath"))
            return "CLUSTER";

        // Fallback: uppercase first word
        int underscoreIndex = eventName.IndexOf('_');
        if (underscoreIndex > 0)
        {
            return eventName.Substring(0, underscoreIndex).ToUpper();
        }

        return eventName.ToUpper();
    }

    /// <summary>
    /// Set tool active state (affects visual behavior)
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
    }

    /// <summary>
    /// Set energy ratio (affects brightness and color)
    /// </summary>
    public void SetEnergyRatio(float ratio)
    {
        energyRatio = Mathf.Clamp01(ratio);
    }

    /// <summary>
    /// Set grid radius
    /// </summary>
    public void SetRadius(float radius)
    {
        baseRadius = radius;
    }

    /// <summary>
    /// Trigger dramatic expansion effect (on successful smoothing)
    /// </summary>
    public void TriggerExpansion()
    {
        // Could add particle burst, line thickness pulse, etc.
        // For now, just a strong visual pulse
        StartCoroutine(ExpansionEffect());
    }

    System.Collections.IEnumerator ExpansionEffect()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;

            // Pulse corner markers
            foreach (var corner in cornerMarkers)
            {
                if (corner != null)
                {
                    corner.transform.localScale = Vector3.one * cornerSize * scale;
                }
            }

            yield return null;
        }
    }

    void OnDestroy()
    {
        if (boxLines != null)
        {
            foreach (var line in boxLines)
            {
                if (line != null)
                    Destroy(line.gameObject);
            }
        }

        if (cornerMarkers != null)
        {
            foreach (var corner in cornerMarkers)
            {
                if (corner != null)
                    Destroy(corner);
            }
        }

        if (cornerMesh != null)
            Destroy(cornerMesh);

        if (lineMaterial != null)
            Destroy(lineMaterial);
    }
}
