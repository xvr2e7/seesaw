using UnityEngine;

/// <summary>
/// Tracks player performance and adjusts tool capabilities dynamically.
/// Controls grid density as reward/punishment based on flow coherence.
/// </summary>
public class PerformanceTracker : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    public TurbulentEventScheduler scheduler;

    [Header("Grid Density Settings")]
    [Tooltip("Minimum grid size (stressed)")]
    public int minGridSize = 3;

    [Tooltip("Default grid size (baseline)")]
    public int defaultGridSize = 5;

    [Tooltip("Maximum grid size (excelling)")]
    public int maxGridSize = 7;

    [Header("Performance Thresholds")]
    [Tooltip("Coherence threshold for upgrading grid")]
    [Range(0.6f, 0.9f)]
    public float upgradeThreshold = 0.75f;

    [Tooltip("Coherence threshold for downgrading grid")]
    [Range(0.2f, 0.5f)]
    public float downgradeThreshold = 0.35f;

    [Tooltip("Time required at threshold before changing grid size")]
    [Range(2f, 10f)]
    public float changeDelay = 4f;

    [Header("Smoothing")]
    [Tooltip("How quickly grid size transitions")]
    [Range(0.5f, 3f)]
    public float transitionSpeed = 1.5f;

    // Runtime state
    private int targetGridSize;
    private float currentGridSizeFloat;
    private float timeAboveUpgradeThreshold;
    private float timeBelowDowngradeThreshold;

    // Performance metrics
    private float currentCoherence;
    private float turbulencePressure;

    // Public accessors
    public int CurrentGridSize => Mathf.RoundToInt(currentGridSizeFloat);
    public float GridSizeProgress => currentGridSizeFloat; // For smooth interpolation
    public float CurrentCoherence => currentCoherence;
    public float TurbulencePressure => turbulencePressure;

    void Start()
    {
        ValidateReferences();

        // Initialize to default
        targetGridSize = defaultGridSize;
        currentGridSizeFloat = defaultGridSize;
    }

    void ValidateReferences()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();

        if (scheduler == null)
            scheduler = FindObjectOfType<TurbulentEventScheduler>();

        if (flowSimulation == null)
            Debug.LogWarning("[PerformanceTracker] No FlowSimulation found!");

        if (scheduler == null)
            Debug.LogWarning("[PerformanceTracker] No TurbulentEventScheduler found!");
    }

    void Update()
    {
        float dt = Time.deltaTime;

        UpdatePerformanceMetrics();
        UpdateGridSizeTarget(dt);
        UpdateGridSizeSmooth(dt);
    }

    void UpdatePerformanceMetrics()
    {
        // Calculate flow coherence (simplified)
        if (flowSimulation != null && flowSimulation.Velocities != null)
        {
            currentCoherence = CalculateFlowCoherence();
        }

        // Calculate turbulence pressure
        if (scheduler != null)
        {
            var activeEvents = scheduler.GetActiveEvents();
            turbulencePressure = 0f;

            foreach (var evt in activeEvents)
            {
                if (evt.isActive)
                {
                    turbulencePressure += evt.strength * evt.currentIntensity;
                }
            }

            // Normalize to 0-1 range (assuming max ~300 total strength)
            turbulencePressure = Mathf.Clamp01(turbulencePressure / 300f);
        }
    }

    float CalculateFlowCoherence()
    {
        // Calculate how aligned the flow field is
        // High coherence = laminar, low coherence = turbulent

        Vector2[] velocities = flowSimulation.Velocities;
        int count = flowSimulation.AgentCount;

        if (count == 0) return 1f;

        // Sample subset for performance (every 10th agent)
        int sampleCount = Mathf.Max(1, count / 10);
        float totalSpeed = 0f;
        Vector2 meanDirection = Vector2.zero;

        for (int i = 0; i < count; i += 10)
        {
            float speed = velocities[i].magnitude;
            totalSpeed += speed;

            if (speed > 0.1f)
            {
                meanDirection += velocities[i] / speed; // Normalized direction
            }
        }

        if (sampleCount == 0) return 1f;

        // Calculate alignment with mean direction
        meanDirection /= sampleCount;
        float meanAlignment = meanDirection.magnitude / Mathf.Max(1f, sampleCount);

        // Higher alignment = higher coherence
        return Mathf.Clamp01(meanAlignment);
    }

    void UpdateGridSizeTarget(float dt)
    {
        // Check if we should upgrade
        if (currentCoherence >= upgradeThreshold && targetGridSize < maxGridSize)
        {
            timeAboveUpgradeThreshold += dt;
            timeBelowDowngradeThreshold = 0f;

            if (timeAboveUpgradeThreshold >= changeDelay)
            {
                targetGridSize = Mathf.Min(targetGridSize + 2, maxGridSize); // Jump by 2 (3→5, 5→7)
                timeAboveUpgradeThreshold = 0f;
            }
        }
        // Check if we should downgrade
        else if (currentCoherence <= downgradeThreshold && targetGridSize > minGridSize)
        {
            timeBelowDowngradeThreshold += dt;
            timeAboveUpgradeThreshold = 0f;

            if (timeBelowDowngradeThreshold >= changeDelay)
            {
                targetGridSize = Mathf.Max(targetGridSize - 2, minGridSize); // Jump by 2 (7→5, 5→3)
                timeBelowDowngradeThreshold = 0f;
            }
        }
        // In middle zone - reset timers
        else
        {
            timeAboveUpgradeThreshold = 0f;
            timeBelowDowngradeThreshold = 0f;
        }

        // Additional pressure from high turbulence
        if (turbulencePressure > 0.7f && targetGridSize > minGridSize)
        {
            // High turbulence pressure can force downgrade faster
            timeBelowDowngradeThreshold += dt * turbulencePressure;
        }
    }

    void UpdateGridSizeSmooth(float dt)
    {
        // Smooth interpolation toward target
        currentGridSizeFloat = Mathf.Lerp(
            currentGridSizeFloat,
            targetGridSize,
            dt * transitionSpeed
        );
    }

    /// <summary>
    /// Force grid size to specific value (for testing or special events)
    /// </summary>
    public void SetGridSize(int size)
    {
        targetGridSize = Mathf.Clamp(size, minGridSize, maxGridSize);
        currentGridSizeFloat = targetGridSize;
    }

    /// <summary>
    /// Reset to baseline performance
    /// </summary>
    public void ResetPerformance()
    {
        targetGridSize = defaultGridSize;
        currentGridSizeFloat = defaultGridSize;
        timeAboveUpgradeThreshold = 0f;
        timeBelowDowngradeThreshold = 0f;
    }

    /// <summary>
    /// Get progress toward next upgrade (0 to 1)
    /// </summary>
    public float GetUpgradeProgress()
    {
        if (targetGridSize >= maxGridSize) return 0f;
        return timeAboveUpgradeThreshold / changeDelay;
    }

    /// <summary>
    /// Get progress toward next downgrade (0 to 1)
    /// </summary>
    public float GetDowngradeProgress()
    {
        if (targetGridSize <= minGridSize) return 0f;
        return timeBelowDowngradeThreshold / changeDelay;
    }
}
