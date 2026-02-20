using System.Collections;
using UnityEngine;

/// <summary>
/// Three-tool controller:
///   1 – SCAN  : hold LMB, continuous area dampening, energy-limited
///   2 – PULSE : tap LMB, instant radial burst, 8s cooldown
///   3 – LOCK  : tap LMB, freeze agents for 2s, 14s cooldown, small radius
///
/// Raises OnScanLineRequested for DampeningParticleEffect to draw a sweep line.
/// </summary>
public class PlayerToolController : MonoBehaviour
{
    // ── Tool identity ──────────────────────────────────────────────────────────
    public enum ToolType { Scan = 0, Pulse = 1, Lock = 2 }

    // ── Inspector references ──────────────────────────────────────────────────
    [Header("References")]
    public FlowSimulation flowSimulation;
    public Camera mainCamera;

    [Header("Component References")]
    public SamplingGrid samplingGrid;
    public ToolEnergySystem energySystem;
    public PerformanceTracker performanceTracker;
    public TurbulenceClassifier turbulenceClassifier;

    // ── SCAN ──────────────────────────────────────────────────────────────────
    [Header("Scan Tool")]
    [Tooltip("Default radius for SCAN")]
    public float scanRadius = 12f;

    [Tooltip("Dampening strength applied per second while SCAN is held")]
    public float scanDampeningStrength = 0.4f;

    [Tooltip("Time to reach maximum scan dampening strength")]
    [Range(0.1f, 5f)]
    public float scanRampUpTime = 1.5f;

    [Tooltip("Base dampening fraction at ramp start")]
    [Range(0.1f, 1f)]
    public float scanBaseFraction = 0.3f;

    // ── PULSE ─────────────────────────────────────────────────────────────────
    [Header("Pulse Tool")]
    [Tooltip("Default radius for PULSE")]
    public float pulseRadius = 12f;

    [Tooltip("One-shot dampening strength for PULSE burst")]
    public float pulseDampeningStrength = 1.8f;

    [Tooltip("Cooldown in seconds after a PULSE fires")]
    public float pulseCooldown = 8f;

    // ── LOCK ──────────────────────────────────────────────────────────────────
    [Header("Lock Tool")]
    [Tooltip("Default radius for LOCK")]
    public float lockRadius = 4f;

    [Tooltip("How long agents are frozen after a LOCK fires")]
    public float lockFreezeDuration = 2f;

    [Tooltip("Cooldown in seconds after a LOCK fires")]
    public float lockCooldown = 14f;

    // ── Shared ────────────────────────────────────────────────────────────────
    [Header("Shared Tool Settings")]
    public float minRadius = 2f;
    public float maxRadius = 25f;
    public float scrollSensitivity = 0.5f;

    [Header("HUD Font")]
    [Tooltip("Assign the Space Mono font asset here")]
    public Font hudFont;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // ── Scan-line event (consumed by DampeningParticleEffect) ─────────────────
    public delegate void ScanLineTrigger(Vector2 boxMin, Vector2 boxMax, float duration);
    public event ScanLineTrigger OnScanLineRequested;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private ToolType _activeTool = ToolType.Scan;
    private Vector2  _worldPos;
    private bool     _toolEnabled = true;
    private float    _currentRadius;

    // SCAN
    private bool  _scanActive       = false;
    private float _scanHoldDuration = 0f;

    // PULSE / LOCK cooldowns (count down to zero = ready)
    private float _pulseCooldownTimer = 0f;
    private float _lockCooldownTimer  = 0f;

    // Single-frame fire flag (keeps GetToolState().isActive meaningful for PULSE/LOCK)
    private bool _firedThisFrame = false;

    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        ValidateReferences();
        AutoCreateComponents();
        _currentRadius = performanceTracker != null ? performanceTracker.CurrentRadius : scanRadius;

        if (samplingGrid != null)
        {
            samplingGrid.SetRadius(_currentRadius);
            samplingGrid.SetActiveTool((int)_activeTool);
        }
    }

    void ValidateReferences()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (flowSimulation == null)
            Debug.LogError("[PlayerToolController] No FlowSimulation found!");
        if (mainCamera == null)
            Debug.LogError("[PlayerToolController] No Camera found!");
    }

    void AutoCreateComponents()
    {
        if (samplingGrid == null)
        {
            var gridObj = new GameObject("SamplingGrid");
            gridObj.transform.SetParent(transform);
            samplingGrid = gridObj.AddComponent<SamplingGrid>();
            samplingGrid.mainCamera  = mainCamera;
            samplingGrid.baseRadius  = scanRadius;
            samplingGrid.flowSimulation = flowSimulation;
        }

        if (energySystem == null)
            energySystem = gameObject.AddComponent<ToolEnergySystem>();

        if (performanceTracker == null)
        {
            var trackerObj = new GameObject("PerformanceTracker");
            trackerObj.transform.SetParent(transform);
            performanceTracker = trackerObj.AddComponent<PerformanceTracker>();
            performanceTracker.flowSimulation = flowSimulation;
        }

        if (turbulenceClassifier == null)
        {
            var classifierObj = new GameObject("TurbulenceClassifier");
            classifierObj.transform.SetParent(transform);
            turbulenceClassifier = classifierObj.AddComponent<TurbulenceClassifier>();

            var scheduler = FindObjectOfType<TurbulentEventScheduler>();
            if (scheduler != null)
                turbulenceClassifier.scheduler = scheduler;
        }

        // Link components
        if (samplingGrid != null)
        {
            samplingGrid.classifier        = turbulenceClassifier;
            samplingGrid.performanceTracker = performanceTracker;
            samplingGrid.flowSimulation    = flowSimulation;
            samplingGrid.baseRadius        = _currentRadius;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (flowSimulation == null || mainCamera == null) return;

        _firedThisFrame = false;

        UpdateWorldPosition();
        UpdateTrackerRadius();
        HandleToolSwitch();
        TickCooldowns();

        switch (_activeTool)
        {
            case ToolType.Scan:  UpdateScan();  break;
            case ToolType.Pulse: UpdatePulse(); break;
            case ToolType.Lock:  UpdateLock();  break;
        }

        UpdateVisuals();
    }

    // ── Input helpers ─────────────────────────────────────────────────────────

    void UpdateWorldPosition()
    {
        Vector3 mouse    = Input.mousePosition;
        mouse.z          = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouse);
        _worldPos        = new Vector2(worldPos.x, worldPos.y);
    }

    void UpdateTrackerRadius()
    {
        if (performanceTracker == null) return;

        float trackerRadius = performanceTracker.CurrentRadius;

        // LOCK keeps its fixed small radius; SCAN/PULSE use the tracker-driven radius
        _currentRadius = _activeTool == ToolType.Lock ? lockRadius : trackerRadius;

        if (samplingGrid != null)
            samplingGrid.SetRadius(_currentRadius);
    }

    void HandleToolSwitch()
    {
        ToolType requested = _activeTool;

        if (Input.GetKeyDown(KeyCode.Alpha1)) requested = ToolType.Scan;
        if (Input.GetKeyDown(KeyCode.Alpha2)) requested = ToolType.Pulse;
        if (Input.GetKeyDown(KeyCode.Alpha3)) requested = ToolType.Lock;

        if (requested == _activeTool) return;

        // Cancel any active scan before switching
        if (_activeTool == ToolType.Scan)
        {
            _scanActive       = false;
            _scanHoldDuration = 0f;
        }

        _activeTool = requested;

        float trackerRadius = performanceTracker != null ? performanceTracker.CurrentRadius : scanRadius;
        _currentRadius = _activeTool switch
        {
            ToolType.Lock => lockRadius,
            _             => trackerRadius
        };

        if (samplingGrid != null)
        {
            samplingGrid.SetRadius(_currentRadius);
            samplingGrid.SetActiveTool((int)_activeTool);
        }
    }

    void TickCooldowns()
    {
        if (_pulseCooldownTimer > 0f)
            _pulseCooldownTimer = Mathf.Max(0f, _pulseCooldownTimer - Time.deltaTime);
        if (_lockCooldownTimer > 0f)
            _lockCooldownTimer  = Mathf.Max(0f, _lockCooldownTimer  - Time.deltaTime);
    }

    // ── Per-tool update ───────────────────────────────────────────────────────

    void UpdateScan()
    {
        bool canActivate = _toolEnabled
            && energySystem != null
            && energySystem.CanActivate;

        if (Input.GetMouseButton(0) && canActivate)
        {
            if (!_scanActive)
            {
                _scanActive       = true;
                _scanHoldDuration = 0f;
            }
            else
            {
                _scanHoldDuration += Time.deltaTime;
            }

            if (!energySystem.ConsumeEnergy(Time.deltaTime))
            {
                _scanActive       = false;
                _scanHoldDuration = 0f;
                return;
            }

            // Smoothstep ramp-up
            float ramp     = Mathf.Clamp01(_scanHoldDuration / scanRampUpTime);
            ramp            = ramp * ramp * (3f - 2f * ramp);
            float strength = Mathf.Lerp(
                scanDampeningStrength * scanBaseFraction,
                scanDampeningStrength,
                ramp
            ) * energySystem.GetEnergyStrengthModifier();

            // Route through FlowSimulation so scoring is properly reported
            flowSimulation.DampenInRadius(_worldPos, _currentRadius, strength);

            // Request repeating scan-line sweep (DampeningParticleEffect ignores
            // this if a sweep is already in progress)
            RaiseScanLine(0.25f);
        }
        else
        {
            _scanActive       = false;
            _scanHoldDuration = 0f;
        }
    }

    void UpdatePulse()
    {
        if (!_toolEnabled) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (_pulseCooldownTimer > 0f) return;

        _pulseCooldownTimer = pulseCooldown;
        _firedThisFrame     = true;

        flowSimulation.DampenInRadius(_worldPos, _currentRadius, pulseDampeningStrength);
        RaiseScanLine(0.15f);
    }

    void UpdateLock()
    {
        if (!_toolEnabled) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (_lockCooldownTimer > 0f) return;

        _lockCooldownTimer = lockCooldown;
        _firedThisFrame    = true;

        StartCoroutine(LockCoroutine(_worldPos, _currentRadius));
        RaiseScanLine(0.15f);
    }

    IEnumerator LockCoroutine(Vector2 center, float radius)
    {
        float   radiusSqr       = radius * radius;
        float[] dampeningFactors = flowSimulation.DampeningFactors;
        Vector2[] positions     = flowSimulation.Positions;
        int     count           = flowSimulation.AgentCount;

        // Pin all agents in radius to fully dampened
        for (int i = 0; i < count; i++)
        {
            if ((positions[i] - center).sqrMagnitude < radiusSqr)
                dampeningFactors[i] = 1f;
        }

        // Report to scoring (one-shot, equivalent to a strong pulse)
        flowSimulation.ReportDampening(pulseDampeningStrength);

        // Natural recovery: dampeningRecoveryRate = 0.5/s, so factor 1 → 0 in ~2s.
        // No explicit cleanup needed — FlowSimulation.UpdateVelocities handles it.
        yield return new WaitForSeconds(lockFreezeDuration);
    }

    void RaiseScanLine(float duration)
    {
        Vector2 boxMin = _worldPos - Vector2.one * _currentRadius;
        Vector2 boxMax = _worldPos + Vector2.one * _currentRadius;
        OnScanLineRequested?.Invoke(boxMin, boxMax, duration);
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    void UpdateVisuals()
    {
        if (samplingGrid == null) return;

        bool isActive = _scanActive || _firedThisFrame;
        samplingGrid.SetActive(isActive);
        samplingGrid.SetActiveTool((int)_activeTool);

        if (energySystem != null)
            samplingGrid.SetEnergyRatio(energySystem.EnergyRatio);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetToolEnabled(bool enabled)
    {
        _toolEnabled = enabled;

        if (!enabled && _scanActive)
        {
            _scanActive       = false;
            _scanHoldDuration = 0f;
        }
    }

    public ToolState GetToolState()
    {
        return new ToolState
        {
            worldPosition      = _worldPos,
            isActive           = _scanActive || _firedThisFrame,
            strength           = _scanActive ? scanDampeningStrength : 0f,
            radius             = _currentRadius,
            activeTool         = (int)_activeTool,
            pulseCooldownRatio = pulseCooldown > 0f ? _pulseCooldownTimer / pulseCooldown : 0f,
            lockCooldownRatio  = lockCooldown  > 0f ? _lockCooldownTimer  / lockCooldown  : 0f
        };
    }

    [System.Serializable]
    public struct ToolState
    {
        public Vector2 worldPosition;
        public bool    isActive;
        public float   strength;
        public float   radius;
        public int     activeTool;          // 0=Scan 1=Pulse 2=Lock
        public float   pulseCooldownRatio;  // 0=ready 1=just fired
        public float   lockCooldownRatio;
    }

    // ── HUD + debug ───────────────────────────────────────────────────────────

    // Cached textures for HUD drawing
    private Texture2D _hudWhiteTex;
    private Material  _hudGLMat;
    private bool      _hudPaused = false;
    private bool      _inDocumentaryPhase = false;

    public void SetPaused(bool paused) { _hudPaused = paused; }
    public void SetDocumentaryPhase(bool documentary) { _inDocumentaryPhase = documentary; }

    void OnGUI()
    {
        if (_hudPaused) return;
        if (_inDocumentaryPhase) return;
        EnsureHUDResources();
        DrawToolHUD();

        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 360, 300, 260));
        GUILayout.Box("Tool Controller");
        GUILayout.Label($"Active Tool: {_activeTool}");
        GUILayout.Label($"Position: ({_worldPos.x:F1}, {_worldPos.y:F1})");
        GUILayout.Label($"Radius: {_currentRadius:F1}");
        GUILayout.Label($"Scan Active: {_scanActive}");

        if (energySystem != null)
        {
            GUILayout.Space(5);
            GUILayout.Label($"Energy: {energySystem.CurrentEnergy:F1} / {energySystem.maxEnergy:F0}");
            GUILayout.Label($"Can Activate: {energySystem.CanActivate}");
        }

        GUILayout.Space(5);
        GUILayout.Label($"Pulse CD: {_pulseCooldownTimer:F1}s / {pulseCooldown:F0}s");
        GUILayout.Label($"Lock  CD: {_lockCooldownTimer:F1}s  / {lockCooldown:F0}s");

        if (performanceTracker != null)
        {
            GUILayout.Space(5);
            GUILayout.Label($"Grid Size:  {performanceTracker.CurrentGridSize}");
            GUILayout.Label($"Coherence:  {performanceTracker.CurrentCoherence:F2}");
        }
        GUILayout.EndArea();
    }

    void EnsureHUDResources()
    {
        if (_hudWhiteTex == null)
        {
            _hudWhiteTex = new Texture2D(1, 1);
            _hudWhiteTex.SetPixel(0, 0, Color.white);
            _hudWhiteTex.Apply();
        }
        if (_hudGLMat == null)
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) shader = Shader.Find("UI/Default");
            _hudGLMat = new Material(shader);
            _hudGLMat.hideFlags = HideFlags.HideAndDontSave;
            _hudGLMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _hudGLMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _hudGLMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            _hudGLMat.SetInt("_ZWrite",   0);
        }
    }

    void DrawToolHUD()
    {
        // ── Layout constants ──────────────────────────────────────────────────
        const float BOX_W  = 150f;
        const float BOX_H  = 96f;
        const float GAP    = 10f;
        const float TOP_Y  = 18f;
        const float BADGE  = 28f;   // key-badge square side
        const float BPAD   = 6f;    // badge inset from corner
        const float BORDER = 1f;

        float totalW = BOX_W * 3f + GAP * 2f;
        float startX = Screen.width * 0.5f - totalW * 0.5f;

        // ── Tool states ───────────────────────────────────────────────────────
        float scanEnergyRatio = energySystem != null ? energySystem.EnergyRatio : 1f;
        // shade = fraction of cooldown/restore remaining (1 = just fired/depleted, 0 = ready)
        float scanShade  = Mathf.Clamp01(1f - scanEnergyRatio);
        float pulseShade = _pulseCooldownTimer > 0f ? _pulseCooldownTimer / pulseCooldown : 0f;
        float lockShade  = _lockCooldownTimer  > 0f ? _lockCooldownTimer  / lockCooldown  : 0f;

        string[] labels = { "SCAN", "PULSE", "LOCK" };
        string[] keys   = { "1",    "2",     "3"    };
        float[]  shades = { scanShade, pulseShade, lockShade };
        int      active = (int)_activeTool;

        // ── Font: use inspector-assigned asset, else fall back to OS font ─────
        Font resolvedFont = hudFont;
        if (resolvedFont == null)
            resolvedFont = Font.CreateDynamicFontFromOSFont(
                new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, 20);

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 20,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Normal
        };
        if (resolvedFont != null) labelStyle.font = resolvedFont;

        GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter
        };
        if (resolvedFont != null) badgeStyle.font = resolvedFont;

        // ── Draw each box ─────────────────────────────────────────────────────
        for (int i = 0; i < 3; i++)
        {
            float bx = startX + i * (BOX_W + GAP);
            float by = TOP_Y;
            bool  isActive  = (i == active);
            bool  restoring = shades[i] > 0.005f;

            // ── Background ───────────────────────────────────────────────────
            Color bgCol = isActive ? new Color(0.07f, 0.08f, 0.09f, 0.95f)
                                   : new Color(0.03f, 0.03f, 0.035f, 0.90f);
            GUI.color = bgCol;
            GUI.DrawTexture(new Rect(bx, by, BOX_W, BOX_H), _hudWhiteTex);
            GUI.color = Color.white;

            // ── Clockwise wipe overlay ────────────────────────────────────────
            if (restoring && Event.current.type == EventType.Repaint)
            {
                Color wipeCol = new Color(0.55f, 0.50f, 0.42f, 0.55f);
                DrawClockwiseShade(bx, by, BOX_W, BOX_H, shades[i], wipeCol);
            }

            // ── Border ───────────────────────────────────────────────────────
            Color borderCol = isActive  ? new Color(0.68f, 0.72f, 0.78f, 0.95f)
                            : restoring ? new Color(0.40f, 0.34f, 0.22f, 0.75f)
                            :             new Color(0.22f, 0.24f, 0.27f, 0.55f);
            GUI.color = borderCol;
            GUI.DrawTexture(new Rect(bx,               by,                   BOX_W,  BORDER), _hudWhiteTex);
            GUI.DrawTexture(new Rect(bx,               by + BOX_H - BORDER,  BOX_W,  BORDER), _hudWhiteTex);
            GUI.DrawTexture(new Rect(bx,               by,                   BORDER, BOX_H),  _hudWhiteTex);
            GUI.DrawTexture(new Rect(bx + BOX_W - BORDER, by,               BORDER, BOX_H),  _hudWhiteTex);
            GUI.color = Color.white;

            // ── Key badge (top-left) ──────────────────────────────────────────
            float badgeX = bx + BPAD;
            float badgeY = by + BPAD;

            Color badgeBg = isActive  ? new Color(0.18f, 0.20f, 0.24f, 0.95f)
                          : restoring ? new Color(0.12f, 0.09f, 0.05f, 0.90f)
                          :             new Color(0.08f, 0.09f, 0.10f, 0.90f);
            GUI.color = badgeBg;
            GUI.DrawTexture(new Rect(badgeX, badgeY, BADGE, BADGE), _hudWhiteTex);
            GUI.color = borderCol;
            GUI.DrawTexture(new Rect(badgeX,               badgeY,                BADGE,  BORDER), _hudWhiteTex);
            GUI.DrawTexture(new Rect(badgeX,               badgeY + BADGE - BORDER, BADGE, BORDER), _hudWhiteTex);
            GUI.DrawTexture(new Rect(badgeX,               badgeY,                BORDER, BADGE),  _hudWhiteTex);
            GUI.DrawTexture(new Rect(badgeX + BADGE - BORDER, badgeY,            BORDER, BADGE),  _hudWhiteTex);
            GUI.color = Color.white;

            badgeStyle.normal.textColor = isActive  ? new Color(0.90f, 0.92f, 0.96f, 1.00f)
                                        : restoring ? new Color(0.55f, 0.44f, 0.28f, 0.90f)
                                        :             new Color(0.48f, 0.51f, 0.55f, 0.80f);
            GUI.Label(new Rect(badgeX, badgeY, BADGE, BADGE), keys[i], badgeStyle);

            // ── Tool label ────────────────────────────────────────────────────
            labelStyle.normal.textColor = isActive  ? new Color(0.90f, 0.92f, 0.96f, 1.00f)
                                        : restoring ? new Color(0.55f, 0.44f, 0.28f, 0.85f)
                                        :             new Color(0.50f, 0.53f, 0.57f, 0.85f);

            float labelY = by + BADGE + BPAD * 2f + 2f;
            float labelH = BOX_H - (labelY - by);
            GUI.Label(new Rect(bx, labelY, BOX_W, labelH), labels[i], labelStyle);
        }

        GUI.color = Color.white;
    }

    /// <summary>
    /// Draws a clockwise-sweeping polygon from 12 o'clock covering fillRatio*360°.
    /// fillRatio 1 = fully covered, 0 = nothing drawn.
    /// As the tool restores, fillRatio shrinks and the wipe retreats clockwise.
    /// </summary>
    void DrawClockwiseShade(float bx, float by, float boxW, float boxH, float fillRatio, Color col)
    {
        if (_hudGLMat == null) return;
        fillRatio = Mathf.Clamp01(fillRatio);
        if (fillRatio <= 0f) return;

        // Center in GUI space (top-left origin, matching GL.LoadPixelMatrix below)
        float cx = bx + boxW * 0.5f;
        float cy = by + boxH * 0.5f;

        // Radius large enough to always reach all four corners
        float R = Mathf.Sqrt(boxW * boxW + boxH * boxH) * 0.5f + 2f;

        // Box bounds — same coordinate space as GUI
        float glL = bx;
        float glR = bx + boxW;
        float glB = by + boxH;
        float glT = by;

        float angleDeg = fillRatio * 360f;
        int   segs     = Mathf.Max(2, Mathf.CeilToInt(angleDeg / 4f));
        float step     = angleDeg / segs;

        // 12 o'clock = -90° in screen space (y-down). Clockwise = increasing angle.
        float startDeg = -90f;

        Vector2 center = new Vector2(cx, cy);

        _hudGLMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
        GL.Begin(GL.TRIANGLES);
        GL.Color(col);

        for (int s = 0; s < segs; s++)
        {
            float a0 = (startDeg + s       * step) * Mathf.Deg2Rad;
            float a1 = (startDeg + (s + 1) * step) * Mathf.Deg2Rad;

            Vector2 p0 = ClipToBox(center,
                new Vector2(cx + Mathf.Cos(a0) * R, cy + Mathf.Sin(a0) * R),
                glL, glR, glB, glT);
            Vector2 p1 = ClipToBox(center,
                new Vector2(cx + Mathf.Cos(a1) * R, cy + Mathf.Sin(a1) * R),
                glL, glR, glB, glT);

            GL.Vertex3(cx,   cy,   0f);
            GL.Vertex3(p0.x, p0.y, 0f);
            GL.Vertex3(p1.x, p1.y, 0f);
        }

        GL.End();
        GL.PopMatrix();
    }

    /// <summary>Clamp a point to be within the box by scaling the ray from center.</summary>
    Vector2 ClipToBox(Vector2 center, Vector2 point, float left, float right, float bottom, float top)
    {
        Vector2 dir = point - center;
        float tMin = 1f;

        // Check each wall and find minimum t to keep inside
        if (Mathf.Abs(dir.x) > 0.0001f)
        {
            float tL = (left  - center.x) / dir.x;
            float tR = (right - center.x) / dir.x;
            float t  = dir.x < 0 ? tL : tR;
            if (t > 0f && t < tMin) tMin = t;
        }
        if (Mathf.Abs(dir.y) > 0.0001f)
        {
            float tB = (bottom - center.y) / dir.y;
            float tT = (top    - center.y) / dir.y;
            float t  = dir.y < 0 ? tT : tB;  // y-down: negative dir.y goes toward top (smaller y)
            if (t > 0f && t < tMin) tMin = t;
        }

        return center + dir * tMin;
    }

    void OnDestroy()
    {
        if (_hudWhiteTex != null) Destroy(_hudWhiteTex);
        if (_hudGLMat    != null) Destroy(_hudGLMat);
    }
}
