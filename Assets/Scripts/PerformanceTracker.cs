using UnityEngine;

/// <summary>
/// Tracks accumulated player dampening and grows/shrinks the tool grid.
/// Grid starts at 3×3 / small radius. Sustained suppression grows it to 5×5,
/// then 7×7, and also expands the physical radius in two additional steps.
/// Active turbulence events erode the accumulator — fall behind and the grid shrinks.
/// </summary>
public class PerformanceTracker : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    public TurbulentEventScheduler scheduler;

    [Header("Growth Thresholds (accumulated dampening)")]
    [Tooltip("Dampening needed to grow from 3×3 small → 3×3 medium radius")]
    public float threshold1 = 15f;

    [Tooltip("Dampening needed to grow to 5×5 density")]
    public float threshold2 = 35f;

    [Tooltip("Dampening needed to grow to 5×5 + larger radius")]
    public float threshold3 = 65f;

    [Tooltip("Dampening needed to grow to 7×7 density")]
    public float threshold4 = 100f;

    [Tooltip("Dampening needed to grow to 7×7 + largest radius")]
    public float threshold5 = 150f;

    [Header("Radius Per Stage")]
    [Tooltip("Stage 0 — starting radius")]
    public float radius0 = 5f;

    [Tooltip("Stage 1 — first radius expansion")]
    public float radius1 = 8f;

    [Tooltip("Stage 2 — 5×5 radius")]
    public float radius2 = 10f;

    [Tooltip("Stage 3 — second radius expansion")]
    public float radius3 = 14f;

    [Tooltip("Stage 4 — 7×7 radius")]
    public float radius4 = 18f;

    [Tooltip("Stage 5 — final radius")]
    public float radius5 = 22f;

    [Header("Erosion")]
    [Tooltip("How strongly active turbulence erodes the dampening accumulator per unit of strength×intensity per second. Tune against dampening accumulation rate.")]
    [Range(0f, 2f)]
    public float turbulenceErodeRate = 0.3f;

    [Header("Transition")]
    [Tooltip("How quickly grid size float interpolates to target")]
    [Range(0.5f, 4f)]
    public float transitionSpeed = 2f;

    [Tooltip("How quickly the physical radius interpolates to target")]
    [Range(0.5f, 4f)]
    public float radiusTransitionSpeed = 1.5f;

    // ── Runtime state ──────────────────────────────────────────────────────────
    private float _accumulatedDampening = 0f;
    private int   _stage = 0;              // 0–5
    private int   _targetGridSize = 3;
    private float _currentGridSizeFloat = 3f;
    private float _targetRadius;
    private float _currentRadius;

    // ── Public accessors ───────────────────────────────────────────────────────
    public int   CurrentGridSize    => Mathf.RoundToInt(_currentGridSizeFloat);
    public float GridSizeProgress   => _currentGridSizeFloat;
    public float CurrentRadius      => _currentRadius;
    public float AccumulatedDampening => _accumulatedDampening;
    public int   Stage              => _stage;

    // Legacy accessors kept for anything still referencing them
    public float CurrentCoherence   => 1f;
    public float TurbulencePressure => 0f;

    void Start()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
        if (scheduler == null)
            scheduler = FindObjectOfType<TurbulentEventScheduler>();

        _targetRadius        = radius0;
        _currentRadius       = radius0;
        _currentGridSizeFloat = 3f;
        _targetGridSize      = 3;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Pull dampening reported this frame from FlowSimulation
        if (flowSimulation != null)
        {
            float reported = flowSimulation.ConsumeReportedDampening();
            _accumulatedDampening += reported;
        }

        // Active turbulence erodes the accumulator
        if (scheduler != null && turbulenceErodeRate > 0f)
        {
            float pressure = 0f;
            foreach (var evt in scheduler.GetActiveEvents())
                if (evt.isActive) pressure += evt.strength * evt.currentIntensity;

            _accumulatedDampening -= pressure * turbulenceErodeRate * dt;
            _accumulatedDampening  = Mathf.Max(0f, _accumulatedDampening);
        }

        EvaluateStage();
        SmoothTransition(dt);
    }

    void EvaluateStage()
    {
        int newStage;

        if      (_accumulatedDampening >= threshold5) newStage = 5;
        else if (_accumulatedDampening >= threshold4) newStage = 4;
        else if (_accumulatedDampening >= threshold3) newStage = 3;
        else if (_accumulatedDampening >= threshold2) newStage = 2;
        else if (_accumulatedDampening >= threshold1) newStage = 1;
        else                                          newStage = 0;

        if (newStage == _stage) return;

        _stage = newStage;

        _targetGridSize = _stage switch
        {
            0 => 3,
            1 => 3,
            2 => 5,
            3 => 5,
            4 => 7,
            5 => 7,
            _ => 7
        };

        _targetRadius = _stage switch
        {
            0 => radius0,
            1 => radius1,
            2 => radius2,
            3 => radius3,
            4 => radius4,
            5 => radius5,
            _ => radius5
        };
    }

    void SmoothTransition(float dt)
    {
        _currentGridSizeFloat = Mathf.Lerp(_currentGridSizeFloat, _targetGridSize, dt * transitionSpeed);
        _currentRadius        = Mathf.Lerp(_currentRadius,        _targetRadius,   dt * radiusTransitionSpeed);
    }

    /// <summary>Force a specific stage (for testing).</summary>
    public void SetStage(int stage)
    {
        _stage = Mathf.Clamp(stage, 0, 5);
        _targetGridSize = _stage switch { 2 or 3 => 5, >= 4 => 7, _ => 3 };
        _targetRadius   = _stage switch
        {
            1 => radius1, 2 => radius2, 3 => radius3,
            4 => radius4, 5 => radius5, _ => radius0
        };
    }

    public void ResetPerformance()
    {
        _accumulatedDampening = 0f;
        _stage                = 0;
        _targetGridSize       = 3;
        _targetRadius         = radius0;
        _currentGridSizeFloat = 3f;
        _currentRadius        = radius0;
    }

    // Legacy stubs
    public float GetUpgradeProgress()   => 0f;
    public float GetDowngradeProgress() => 0f;
    public void  SetGridSize(int size)  { }
}
