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
            samplingGrid.mainCamera     = mainCamera;
            samplingGrid.baseRadius     = scanRadius;
            samplingGrid.flowSimulation = flowSimulation;
            samplingGrid.customFont     = hudFont;
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

    private bool _hudPaused = false;
    private bool _inDocumentaryPhase = false;

    public void SetPaused(bool paused) { _hudPaused = paused; }
    public void SetDocumentaryPhase(bool documentary) { _inDocumentaryPhase = documentary; }

    void OnGUI()
    {
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

    void OnDestroy()
    {
    }
}
