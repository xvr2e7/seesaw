using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders object-detection style bounding boxes that reveal machine vision.
/// Shows a main outer box at the cursor plus CV-style cluster sub-boxes for
/// agent groups detected inside the tool area.
/// </summary>
public class SamplingGrid : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public TurbulenceClassifier classifier;
    public PerformanceTracker performanceTracker;
    public FlowSimulation flowSimulation;

    [Header("Box Appearance")]
    [Tooltip("Base radius of the detection box")]
    public float baseRadius = 8f;

    [Tooltip("Line width of outer bounding box")]
    [Range(0.05f, 0.3f)]
    public float lineWidth = 0.15f;

    [Tooltip("Corner marker size")]
    [Range(0.3f, 1.5f)]
    public float cornerSize = 0.8f;

    [Header("Per-Tool Colors")]
    [Tooltip("Outer box color for SCAN tool (0)")]
    public Color scanColor     = new Color(0.40f, 0.44f, 0.50f, 0.55f);
    [Tooltip("Outer box color for PULSE tool (1)")]
    public Color pulseColor    = new Color(0.62f, 0.50f, 0.30f, 0.60f);
    [Tooltip("Outer box color for LOCK tool (2)")]
    public Color lockColor     = new Color(0.58f, 0.34f, 0.30f, 0.55f);
    [Tooltip("Color when SCAN energy is low (<30%)")]
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

    [Header("Cluster Sub-boxes")]
    [Tooltip("Grid cell size for spatial bucketing (world units)")]
    public float clusterCellSize = 3f;

    [Tooltip("Min agents in a cell to draw a sub-box")]
    public int clusterMinAgents = 3;

    [Tooltip("Max agents in a cell to draw a sub-box (above = too dense, skip)")]
    public int clusterMaxAgents = 8;

    [Tooltip("Sub-box edge line width")]
    [Range(0.02f, 0.15f)]
    public float subBoxLineWidth = 0.05f;

    [Tooltip("Sub-box color (dim, subtle)")]
    public Color subBoxColor = new Color(0.35f, 0.38f, 0.44f, 0.30f);

    [Tooltip("Sub-box color when cluster overlaps a turbulence zone")]
    public Color subBoxTurbulenceColor = new Color(0.55f, 0.48f, 0.35f, 0.45f);

    [Tooltip("Maximum sub-boxes rendered simultaneously")]
    public int maxSubBoxes = 12;

    [Header("Event Name Display")]
    [Tooltip("Custom font for event name label (assign Space Mono Regular)")]
    public Font customFont;

    [Tooltip("Font size for event name")]
    public int eventNameFontSize = 22;

    [Tooltip("Distance above grid center for event label")]
    public float eventNameOffset = 0.3f;

    [Tooltip("Event name color")]
    public Color eventNameColor = new Color(0.52f, 0.54f, 0.58f, 0.80f);

    // ── Runtime state ──────────────────────────────────────────────────────────
    private Vector2 currentWorldPos;
    private bool isActive = false;
    private float energyRatio = 1f;
    private int _activeToolIndex = 0;   // 0=Scan, 1=Pulse, 2=Lock

    // ── Outer box rendering ────────────────────────────────────────────────────
    private LineRenderer[] boxLines;
    private GameObject[] cornerMarkers;
    private const int LINES_PER_BOX   = 4;
    private const int CORNERS_PER_BOX = 4;
    private GUIStyle eventNameStyle;
    private Mesh cornerMesh;
    private Material lineMaterial;

    // ── Sub-box pool ──────────────────────────────────────────────────────────
    private LineRenderer[] _subBoxLines;   // maxSubBoxes * 4 LineRenderers

    // ── Cluster detection (persistent to avoid per-frame allocs) ──────────────
    private Dictionary<int, List<int>> _cellBuckets = new Dictionary<int, List<int>>();
    // (min, max, count, inTurbulence)
    private List<(Vector2 min, Vector2 max, int count, bool inTurb)> _clusterBoxes
        = new List<(Vector2, Vector2, int, bool)>();

    // ── Grid sample positions (external consumers) ────────────────────────────
    private Vector2[] samplePositions;
    public Vector2[] SamplePositions => samplePositions;

    // ── Sample dot visuals ────────────────────────────────────────────────────
    [Header("Sample Dot Visuals")]
    [Tooltip("Size of each sample point dot")]
    [Range(0.05f, 0.4f)]
    public float sampleDotSize = 0.12f;

    [Tooltip("Opacity of sample dots (dim — they're scan points, not decorations)")]
    [Range(0f, 1f)]
    public float sampleDotAlpha = 0.35f;

    [Tooltip("Fixed world-unit gap between adjacent sample dots. Keep small so the cluster never exceeds the viewport.")]
    [Range(0.3f, 3f)]
    public float dotSpacing = 0.8f;

    private const int MAX_SAMPLE_DOTS = 49; // 7×7
    private GameObject[] _sampleDots;

    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        ValidateReferences();
        CreateBoundingBox();
        CreateSubBoxPool();
        CreateSampleDotPool();
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

        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
    }

    void CreateBoundingBox()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));

        // 4 edge LineRenderers for outer box
        boxLines = new LineRenderer[LINES_PER_BOX];
        for (int i = 0; i < LINES_PER_BOX; i++)
        {
            GameObject lineObj = new GameObject($"BoxEdge_{i}");
            lineObj.transform.SetParent(transform);

            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material        = lineMaterial;
            line.startWidth      = lineWidth;
            line.endWidth        = lineWidth;
            line.positionCount   = 2;
            line.useWorldSpace   = true;
            line.sortingOrder    = 100;

                boxLines[i] = line;
            line.enabled = false;   // box hidden — dots are the cursor
        }

        // 4 corner square markers (kept for mesh/material reuse; hidden)
        cornerMesh    = CreateSquareMesh();
        cornerMarkers = new GameObject[CORNERS_PER_BOX];

        for (int i = 0; i < CORNERS_PER_BOX; i++)
        {
            GameObject corner = new GameObject($"Corner_{i}");
            corner.transform.SetParent(transform);

            MeshRenderer renderer = corner.AddComponent<MeshRenderer>();
            MeshFilter   filter   = corner.AddComponent<MeshFilter>();

            renderer.material = lineMaterial;
            filter.mesh       = cornerMesh;

            cornerMarkers[i] = corner;
            corner.SetActive(false);   // hidden
        }
    }

    void CreateSubBoxPool()
    {
        _subBoxLines = new LineRenderer[maxSubBoxes * 4];

        for (int i = 0; i < maxSubBoxes * 4; i++)
        {
            GameObject lineObj = new GameObject($"SubBoxEdge_{i}");
            lineObj.transform.SetParent(transform);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material        = lineMaterial;
            lr.startWidth      = subBoxLineWidth;
            lr.endWidth        = subBoxLineWidth;
            lr.positionCount   = 2;
            lr.useWorldSpace   = true;
            lr.sortingOrder    = 99;   // just below outer box
            lr.enabled         = false;

            _subBoxLines[i] = lr;
        }
    }

    void CreateSampleDotPool()
    {
        _sampleDots = new GameObject[MAX_SAMPLE_DOTS];
        for (int i = 0; i < MAX_SAMPLE_DOTS; i++)
        {
            GameObject dot = new GameObject($"SampleDot_{i}");
            dot.transform.SetParent(transform);

            MeshRenderer rend = dot.AddComponent<MeshRenderer>();
            MeshFilter   filt = dot.AddComponent<MeshFilter>();
            rend.material = lineMaterial;
            filt.mesh     = cornerMesh;

            dot.SetActive(false);
            _sampleDots[i] = dot;
        }
    }

    void SetupGUIStyle()
    {
        eventNameStyle           = new GUIStyle();
        eventNameStyle.fontSize  = eventNameFontSize;
        eventNameStyle.alignment = TextAnchor.MiddleCenter;
        eventNameStyle.normal.textColor = eventNameColor;

        if (customFont != null)
            eventNameStyle.font = customFont;
        else
        {
            Font spaceMonoFont = Font.CreateDynamicFontFromOSFont("Space Mono", eventNameFontSize);
            eventNameStyle.font = spaceMonoFont != null
                ? spaceMonoFont
                : Font.CreateDynamicFontFromOSFont("Consolas", eventNameFontSize);
        }
    }

    Mesh CreateSquareMesh()
    {
        Mesh mesh   = new Mesh();
        mesh.name   = "CornerMarker";
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 1f), new Vector2(1f, 1f)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ──────────────────────────────────────────────────────────────────────────
    void Update()
    {
        UpdateWorldPosition();
        UpdateBoundingBoxVisuals();
        UpdateSamplePositions();
        UpdateClusterSubBoxes();
    }

    void UpdateWorldPosition()
    {
        if (mainCamera == null) return;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        currentWorldPos  = new Vector2(worldPos.x, worldPos.y);
    }

    void UpdateBoundingBoxVisuals()
    {
        // Box and corners are hidden; this pass only keeps energyRatio/toolIndex
        // state current so UpdateSampleDots can read the right color.
    }

    void UpdateSamplePositions()
    {
        int gridSize   = performanceTracker != null ? performanceTracker.CurrentGridSize : 5;
        int pointCount = gridSize * gridSize;

        if (samplePositions == null || samplePositions.Length != pointCount)
            samplePositions = new Vector2[pointCount];

        float spacing = dotSpacing;

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                float localX = (x - (gridSize - 1) * 0.5f) * spacing;
                float localY = (y - (gridSize - 1) * 0.5f) * spacing;
                samplePositions[y * gridSize + x] = currentWorldPos + new Vector2(localX, localY);
            }
        }

        UpdateSampleDots(pointCount);
    }

    void UpdateSampleDots(int activeCount)
    {
        if (_sampleDots == null) return;

        // Derive dot color from current box color (same tint, fixed dim alpha)
        Color toolColor = _activeToolIndex switch
        {
            1 => pulseColor,
            2 => lockColor,
            _ => (_activeToolIndex == 0 && energyRatio < 0.3f) ? lowEnergyColor : scanColor
        };
        Color dotColor  = toolColor;
        dotColor.a      = sampleDotAlpha;

        for (int i = 0; i < MAX_SAMPLE_DOTS; i++)
        {
            if (i < activeCount)
            {
                _sampleDots[i].SetActive(true);
                _sampleDots[i].transform.position   = new Vector3(samplePositions[i].x, samplePositions[i].y, -1.2f);
                _sampleDots[i].transform.localScale  = Vector3.one * sampleDotSize;
                _sampleDots[i].GetComponent<MeshRenderer>().material.color = dotColor;
            }
            else
            {
                _sampleDots[i].SetActive(false);
            }
        }
    }

    // ── Cluster sub-box detection ─────────────────────────────────────────────

    void UpdateClusterSubBoxes()
    {
        _clusterBoxes.Clear();

        // Recycle bucket lists rather than clearing the dict outright — avoids
        // re-allocating the inner List<int> objects every frame.
        foreach (var kv in _cellBuckets) kv.Value.Clear();

        if (flowSimulation == null) goto renderSubBoxes;

        Vector2[] positions = flowSimulation.Positions;
        int       agentCount = flowSimulation.AgentCount;
        float     radiusSqr  = baseRadius * baseRadius;

        // ── Step 1: bucket agents inside tool radius ──────────────────────────
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - currentWorldPos).sqrMagnitude;
            if (distSqr >= radiusSqr) continue;

            int cx  = Mathf.FloorToInt(positions[i].x / clusterCellSize);
            int cy  = Mathf.FloorToInt(positions[i].y / clusterCellSize);
            int key = cx * 73856093 ^ cy * 19349663;   // spatial hash

            if (!_cellBuckets.TryGetValue(key, out List<int> bucket))
            {
                bucket = new List<int>(8);
                _cellBuckets[key] = bucket;
            }
            bucket.Add(i);
        }

        // ── Step 2: compute AABB per qualifying bucket ────────────────────────
        foreach (var kv in _cellBuckets)
        {
            List<int> bucket = kv.Value;
            int count = bucket.Count;
            if (count < clusterMinAgents || count > clusterMaxAgents) continue;
            if (_clusterBoxes.Count >= maxSubBoxes) break;

            Vector2 min = new Vector2( float.MaxValue,  float.MaxValue);
            Vector2 max = new Vector2(-float.MaxValue, -float.MaxValue);

            for (int j = 0; j < count; j++)
            {
                Vector2 p = positions[bucket[j]];
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
            }

            float pad = 0.4f;
            min -= Vector2.one * pad;
            max += Vector2.one * pad;

            bool inTurb = false;
            if (classifier != null)
            {
                Vector2 center = (min + max) * 0.5f;
                inTurb = classifier.IsInTurbulence(center);
            }

            _clusterBoxes.Add((min, max, count, inTurb));
        }

        renderSubBoxes:
        RenderSubBoxes();
    }

    void RenderSubBoxes()
    {
        // Disable all pool slots first
        for (int i = 0; i < _subBoxLines.Length; i++)
            _subBoxLines[i].enabled = false;

        int boxCount = Mathf.Min(_clusterBoxes.Count, maxSubBoxes);
        for (int b = 0; b < boxCount; b++)
        {
            var (min, max, count, inTurb) = _clusterBoxes[b];
            Color color = inTurb ? subBoxTurbulenceColor : subBoxColor;
            float z     = -0.9f;
            int   baseIdx = b * 4;

            SetSubLine(baseIdx + 0, new Vector3(min.x, max.y, z), new Vector3(max.x, max.y, z), color); // top
            SetSubLine(baseIdx + 1, new Vector3(min.x, min.y, z), new Vector3(max.x, min.y, z), color); // bottom
            SetSubLine(baseIdx + 2, new Vector3(min.x, min.y, z), new Vector3(min.x, max.y, z), color); // left
            SetSubLine(baseIdx + 3, new Vector3(max.x, min.y, z), new Vector3(max.x, max.y, z), color); // right
        }
    }

    void SetSubLine(int idx, Vector3 a, Vector3 b, Color color)
    {
        if (idx >= _subBoxLines.Length) return;
        LineRenderer lr = _subBoxLines[idx];
        lr.enabled     = true;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startColor  = color;
        lr.endColor    = color;
    }

    // ── OnGUI: event name label + cluster count labels ────────────────────────

    void OnGUI()
    {
        if (classifier == null || mainCamera == null) return;

        // Pattern label above outer box
        string eventName = classifier.DetectEventAt(currentWorldPos);
        if (!string.IsNullOrEmpty(eventName))
        {
            string keyword = ExtractPatternKeyword(eventName);

            Vector3 worldPos   = new Vector3(currentWorldPos.x, currentWorldPos.y + baseRadius + eventNameOffset, 0f);
            Vector3 screenPos  = mainCamera.WorldToScreenPoint(worldPos);
            screenPos.y        = Screen.height - screenPos.y;

            Rect rect = new Rect(screenPos.x - 160, screenPos.y - eventNameFontSize / 2, 320, eventNameFontSize + 10);

            Color nameColor = eventNameColor;
            if (isActive) nameColor.a *= 0.9f + Mathf.Sin(Time.time * 8f) * 0.1f;
            eventNameStyle.normal.textColor = nameColor;

            GUI.Label(rect, keyword, eventNameStyle);
        }

        // Cluster count labels — only for turbulent clusters
        foreach (var (min, max, count, inTurb) in _clusterBoxes)
        {
            if (!inTurb) continue;

            Vector2 center    = (min + max) * 0.5f;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(
                new Vector3(center.x, max.y + 0.3f, 0f));
            screenPos.y = Screen.height - screenPos.y;

            Rect labelRect = new Rect(screenPos.x - 20, screenPos.y - 8, 40, 16);

            Color prev = eventNameStyle.normal.textColor;
            eventNameStyle.normal.textColor = subBoxTurbulenceColor;
            GUI.Label(labelRect, $"\u00d7{count}", eventNameStyle);
            eventNameStyle.normal.textColor = prev;
        }
    }

    string ExtractPatternKeyword(string eventName)
    {
        if (eventName.Contains("Circular") || eventName.Contains("Assembly") || eventName.Contains("Gather"))
            return "ASSEMBLY";
        if (eventName.Contains("Scatter") || eventName.Contains("Panic"))
            return "DISPERSAL";
        if (eventName.Contains("Vortex") || eventName.Contains("Spiral"))
            return "SPIRAL FORMATION";
        if (eventName.Contains("Wave") || eventName.Contains("March"))
            return "MARCH";
        if (eventName.Contains("Oscillation"))
            return "DISTURBANCE";
        if (eventName.Contains("Cluster") || eventName.Contains("Blockade") || eventName.Contains("Aftermath"))
            return "BLOCKADE";

        int underscoreIndex = eventName.IndexOf('_');
        if (underscoreIndex > 0)
            return eventName.Substring(0, underscoreIndex).ToUpper();

        return eventName.ToUpper();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetActive(bool active)
    {
        isActive = active;
    }

    public void SetEnergyRatio(float ratio)
    {
        energyRatio = Mathf.Clamp01(ratio);
    }

    public void SetRadius(float radius)
    {
        baseRadius = radius;
    }

    /// <summary>Set active tool index: 0=Scan, 1=Pulse, 2=Lock (drives outer box color).</summary>
    public void SetActiveTool(int toolIndex)
    {
        _activeToolIndex = toolIndex;
    }

    /// <summary>Trigger dramatic corner-pulse expansion effect.</summary>
    public void TriggerExpansion()
    {
        StartCoroutine(ExpansionEffect());
    }

    System.Collections.IEnumerator ExpansionEffect()
    {
        float duration = 0.3f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;

            foreach (var corner in cornerMarkers)
            {
                if (corner != null)
                    corner.transform.localScale = Vector3.one * cornerSize * scale;
            }

            yield return null;
        }
    }

    void OnDestroy()
    {
        if (boxLines != null)
        {
            foreach (var line in boxLines)
                if (line != null) Destroy(line.gameObject);
        }

        if (cornerMarkers != null)
        {
            foreach (var corner in cornerMarkers)
                if (corner != null) Destroy(corner);
        }

        if (_subBoxLines != null)
        {
            foreach (var lr in _subBoxLines)
                if (lr != null) Destroy(lr.gameObject);
        }

        if (_sampleDots != null)
        {
            foreach (var dot in _sampleDots)
                if (dot != null) Destroy(dot);
        }

        if (cornerMesh   != null) Destroy(cornerMesh);
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
