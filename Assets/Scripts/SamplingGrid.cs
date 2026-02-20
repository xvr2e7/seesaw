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
    // (min, max, count, inTurbulence, divergenceScore)
    private List<(Vector2 min, Vector2 max, int count, bool inTurb, float score)> _clusterBoxes
        = new List<(Vector2, Vector2, int, bool, float)>();

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

        // Energy state encoding per GAMEPLAY_DESCRIPTION.md:
        //   Full energy  : solid cool blue-gray dots
        //   Draining     : dims and shifts toward amber as energy depletes
        //   Low (<30%)   : amber-brown, noticeably dim
        //   Depleted (0) : dots hidden — tool offline
        Color dotColor;
        float dotAlpha;

        if (energyRatio <= 0f)
        {
            // Depleted — hide all dots
            for (int i = 0; i < MAX_SAMPLE_DOTS; i++)
                _sampleDots[i].SetActive(false);
            return;
        }
        else if (energyRatio < 0.3f)
        {
            // Low energy: amber-brown, noticeably dim
            dotColor = new Color(0.55f, 0.40f, 0.22f, 1f);
            dotAlpha = sampleDotAlpha * Mathf.Lerp(0.35f, 0.55f, energyRatio / 0.3f);
        }
        else
        {
            // Full → draining: cool blue-gray fading to amber
            float t  = 1f - energyRatio; // 0 = full, 1 = just entered low zone
            Color fullColor  = new Color(0.42f, 0.46f, 0.52f, 1f); // cool blue-gray
            Color drainColor = new Color(0.60f, 0.46f, 0.26f, 1f); // amber
            dotColor = Color.Lerp(fullColor, drainColor, t);
            dotAlpha = Mathf.Lerp(sampleDotAlpha, sampleDotAlpha * 0.6f, t);
        }

        dotColor.a = dotAlpha;

        for (int i = 0; i < MAX_SAMPLE_DOTS; i++)
        {
            if (i < activeCount)
            {
                _sampleDots[i].SetActive(true);
                _sampleDots[i].transform.position  = new Vector3(samplePositions[i].x, samplePositions[i].y, -1.2f);
                _sampleDots[i].transform.localScale = Vector3.one * sampleDotSize;
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

        // Grid half-span — matches the visible dot-grid footprint exactly
        int   gridSize = performanceTracker != null ? performanceTracker.CurrentGridSize : 5;
        float halfSpan = (gridSize - 1) * 0.5f * dotSpacing;

        // ── Step 1: bucket agents inside the grid square ──────────────────────
        for (int i = 0; i < agentCount; i++)
        {
            Vector2 delta = positions[i] - currentWorldPos;
            if (Mathf.Abs(delta.x) > halfSpan || Mathf.Abs(delta.y) > halfSpan) continue;

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

        // ── Step 2: compute square AABB per qualifying bucket ─────────────────
        float[] turbInfluence = flowSimulation != null ? flowSimulation.TurbulenceInfluence : null;

        foreach (var kv in _cellBuckets)
        {
            List<int> bucket = kv.Value;
            int count = bucket.Count;
            if (count < clusterMinAgents || count > clusterMaxAgents) continue;
            if (_clusterBoxes.Count >= maxSubBoxes) break;

            Vector2 min = new Vector2( float.MaxValue,  float.MaxValue);
            Vector2 max = new Vector2(-float.MaxValue, -float.MaxValue);
            float turbSum = 0f;

            for (int j = 0; j < count; j++)
            {
                int idx = bucket[j];
                Vector2 p = positions[idx];
                if (p.x < min.x) min.x = p.x;
                if (p.y < min.y) min.y = p.y;
                if (p.x > max.x) max.x = p.x;
                if (p.y > max.y) max.y = p.y;
                if (turbInfluence != null && idx < turbInfluence.Length)
                    turbSum += turbInfluence[idx];
            }

            float pad = 0.4f;
            min -= Vector2.one * pad;
            max += Vector2.one * pad;

            // Force true square: expand shorter axis to match longer axis
            Vector2 boxCenter = (min + max) * 0.5f;
            float   halfW     = (max.x - min.x) * 0.5f;
            float   halfH     = (max.y - min.y) * 0.5f;
            float   halfSide  = Mathf.Max(halfW, halfH);
            min = boxCenter - Vector2.one * halfSide;
            max = boxCenter + Vector2.one * halfSide;

            // Divergence probability: logistic function on mean turbulence influence
            float meanTurb = turbSum / count;
            float score = 1f / (1f + Mathf.Exp(-10f * (meanTurb - 0.4f)));

            bool inTurb = false;
            if (classifier != null)
            {
                inTurb = classifier.IsInTurbulence(boxCenter);
            }

            _clusterBoxes.Add((min, max, count, inTurb, score));
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
            var (min, max, count, inTurb, score) = _clusterBoxes[b];
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
        if (mainCamera == null) return;

        // Pattern label — shown when cursor overlaps an active event zone
        {
            string keyword = SamplePatternLabel();

            if (!string.IsNullOrEmpty(keyword))
            {
                // Top-left corner of the dot grid
                int gridSize = performanceTracker != null ? performanceTracker.CurrentGridSize : 5;
                float halfSpan = (gridSize - 1) * 0.5f * dotSpacing;
                Vector3 worldPos  = new Vector3(
                    currentWorldPos.x - halfSpan,
                    currentWorldPos.y + halfSpan + eventNameOffset,
                    0f);
                Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
                screenPos.y       = Screen.height - screenPos.y;

                // Left-aligned, just above the top-left dot
                Rect rect = new Rect(screenPos.x, screenPos.y - eventNameFontSize, 220, eventNameFontSize + 4);

                Color nameColor = eventNameColor;
                if (isActive) nameColor.a *= 0.9f + Mathf.Sin(Time.time * 8f) * 0.1f;
                eventNameStyle.normal.textColor = nameColor;
                eventNameStyle.alignment = TextAnchor.UpperLeft;
                GUI.Label(rect, keyword, eventNameStyle);
                eventNameStyle.alignment = TextAnchor.MiddleCenter;
            }
        }

        // Sub-box labels: score in top-left, ×N count at bottom
        foreach (var (min, max, count, inTurb, score) in _clusterBoxes)
        {
            Color labelCol = inTurb ? subBoxTurbulenceColor : subBoxColor;
            labelCol.a = Mathf.Max(labelCol.a, 0.55f);

            const int SUB_FONT = 16;
            const int LABEL_H  = 20;
            const int LABEL_W  = 64;

            Color prev    = eventNameStyle.normal.textColor;
            int prevSize  = eventNameStyle.fontSize;

            // Score label — top-left corner of sub-box
            Vector3 scoreWorldPos = new Vector3(min.x, max.y, 0f);
            Vector3 scoreScreen   = mainCamera.WorldToScreenPoint(scoreWorldPos);
            scoreScreen.y = Screen.height - scoreScreen.y;

            Rect scoreRect = new Rect(scoreScreen.x, scoreScreen.y - LABEL_H, LABEL_W, LABEL_H);

            eventNameStyle.fontSize  = SUB_FONT;
            eventNameStyle.alignment = TextAnchor.UpperLeft;
            eventNameStyle.normal.textColor = labelCol;
            GUI.Label(scoreRect, score.ToString("F2"), eventNameStyle);

            // Count label — bottom-right corner of sub-box
            Vector3 countWorldPos = new Vector3(max.x, min.y, 0f);
            Vector3 countScreen   = mainCamera.WorldToScreenPoint(countWorldPos);
            countScreen.y = Screen.height - countScreen.y;

            Rect countRect = new Rect(countScreen.x - LABEL_W, countScreen.y, LABEL_W, LABEL_H);
            eventNameStyle.alignment = TextAnchor.LowerRight;
            GUI.Label(countRect, $"\u00d7{count}", eventNameStyle);

            // Restore style
            eventNameStyle.fontSize  = prevSize;
            eventNameStyle.alignment = TextAnchor.MiddleCenter;
            eventNameStyle.normal.textColor = prev;
        }
    }

    /// <summary>
    /// Returns the pattern keyword when cursor is inside an active event zone
    /// (within 80% of radius, intensity ≥ 0.3), or null when outside all events.
    /// Labels match the gameplay description table exactly.
    /// </summary>
    string SamplePatternLabel()
    {
        if (classifier == null) return null;

        var scheduler = classifier.scheduler;
        if (scheduler == null) return null;

        var activeEvents = scheduler.GetActiveEvents();
        if (activeEvents == null || activeEvents.Count == 0) return null;

        TurbulenceEvent strongest = null;
        float bestInfluence = 0f;

        foreach (var evt in activeEvents)
        {
            if (!evt.isActive || evt.currentIntensity < 0.3f) continue;

            float dist          = Vector2.Distance(currentWorldPos, evt.position);
            float effectiveRadius = evt.radius * 0.8f;
            if (dist > effectiveRadius) continue;

            float normalizedDist = dist / effectiveRadius;
            float influence      = (1f - normalizedDist) * evt.currentIntensity;
            if (influence > bestInfluence)
            {
                bestInfluence = influence;
                strongest     = evt;
            }
        }

        if (strongest == null) return null;

        switch (strongest.pattern)
        {
            case TurbulenceEvent.PatternType.Circular:    return "ASSEMBLY";
            case TurbulenceEvent.PatternType.Scatter:     return "DISPERSAL";
            case TurbulenceEvent.PatternType.Vortex:      return "SPIRAL";
            case TurbulenceEvent.PatternType.Wave:        return "MARCH";
            case TurbulenceEvent.PatternType.Oscillation: return "DISTURBANCE";
            case TurbulenceEvent.PatternType.Cluster:     return "BLOCKADE";
            default: return null;
        }
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
