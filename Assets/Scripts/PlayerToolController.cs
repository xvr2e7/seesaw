using UnityEngine;

/// <summary>
/// Refactored tool controller that orchestrates modular components.
/// Reveals machine vision's operational layer through bounding box visualization.
/// </summary>
public class PlayerToolController : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    public Camera mainCamera;

    [Header("Component References")]
    public SamplingGrid samplingGrid;
    public ToolEnergySystem energySystem;
    public PerformanceTracker performanceTracker;
    public TurbulenceClassifier turbulenceClassifier;

    [Header("Tool Settings")]
    [Tooltip("Base radius of effect")]
    public float baseRadius = 8f;

    [Tooltip("Minimum tool radius")]
    public float minRadius = 2f;

    [Tooltip("Maximum tool radius")]
    public float maxRadius = 25f;

    [Tooltip("How fast scroll wheel changes radius")]
    public float scrollSensitivity = 0.5f;

    [Header("Smoothing Settings")]
    [Tooltip("Base dampening strength")]
    [Range(0.1f, 1f)]
    public float baseDampeningStrength = 0.3f;

    [Tooltip("Maximum dampening strength after full ramp-up")]
    [Range(0.5f, 1f)]
    public float maxDampeningStrength = 0.85f;

    [Tooltip("Time in seconds to reach maximum strength")]
    [Range(0.1f, 5f)]
    public float rampUpTime = 1.5f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // Runtime state
    private Vector2 currentWorldPos;
    private bool isApplying = false;
    private float holdDuration = 0f;
    private float currentStrength = 0f;
    private float currentRadius;
    private bool toolEnabled = true;

    void Start()
    {
        ValidateReferences();
        AutoCreateComponents();
        currentRadius = baseRadius;

        Debug.Log($"[PlayerToolController] Started. SamplingGrid: {samplingGrid != null}, EnergySystem: {energySystem != null}");
    }

    void ValidateReferences()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (flowSimulation == null)
            Debug.LogError("[PlayerToolController_New] No FlowSimulation found!");

        if (mainCamera == null)
            Debug.LogError("[PlayerToolController_New] No Camera found!");
    }

    void AutoCreateComponents()
    {
        // Auto-create components if not assigned
        if (samplingGrid == null)
        {
            var gridObj = new GameObject("SamplingGrid");
            gridObj.transform.SetParent(transform);
            samplingGrid = gridObj.AddComponent<SamplingGrid>();
            samplingGrid.mainCamera = mainCamera;
            samplingGrid.baseRadius = baseRadius;
        }

        if (energySystem == null)
        {
            energySystem = gameObject.AddComponent<ToolEnergySystem>();
        }

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

            // Auto-find scheduler
            var scheduler = FindObjectOfType<TurbulentEventScheduler>();
            if (scheduler != null)
            {
                turbulenceClassifier.scheduler = scheduler;
            }
        }

        // Link components
        if (samplingGrid != null)
        {
            samplingGrid.classifier = turbulenceClassifier;
            samplingGrid.performanceTracker = performanceTracker;
            samplingGrid.baseRadius = currentRadius;
        }
    }

    void Update()
    {
        if (flowSimulation == null || mainCamera == null) return;

        UpdateWorldPosition();
        UpdateScrollWheel();
        UpdateToolInput();
        UpdateVisuals();

        if (isApplying)
        {
            ApplySmoothing();
        }
    }

    void UpdateWorldPosition()
    {
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);
        currentWorldPos = new Vector2(worldPos.x, worldPos.y);
    }

    void UpdateScrollWheel()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentRadius += scroll * scrollSensitivity;
            currentRadius = Mathf.Clamp(currentRadius, minRadius, maxRadius);

            if (samplingGrid != null)
            {
                samplingGrid.SetRadius(currentRadius);
            }
        }
    }

    void UpdateToolInput()
    {
        bool canActivate = toolEnabled &&
                          energySystem != null &&
                          energySystem.CanActivate;

        if (Input.GetMouseButton(0) && canActivate)
        {
            if (!isApplying)
            {
                isApplying = true;
                holdDuration = 0f;
            }
            else
            {
                holdDuration += Time.deltaTime;
            }

            // Consume energy
            if (!energySystem.ConsumeEnergy(Time.deltaTime))
            {
                // Energy depleted
                isApplying = false;
                holdDuration = 0f;
                currentStrength = 0f;
                return;
            }

            // Calculate strength with ramp-up
            float rampProgress = Mathf.Clamp01(holdDuration / rampUpTime);
            rampProgress = rampProgress * rampProgress * (3f - 2f * rampProgress); // Smoothstep
            float baseStrength = Mathf.Lerp(baseDampeningStrength, maxDampeningStrength, rampProgress);

            // Apply energy modifier
            float energyModifier = energySystem.GetEnergyStrengthModifier();

            currentStrength = baseStrength * energyModifier;
        }
        else
        {
            if (isApplying)
            {
                isApplying = false;
                holdDuration = 0f;
                currentStrength = 0f;
            }
        }
    }

    void UpdateVisuals()
    {
        if (samplingGrid != null)
        {
            samplingGrid.SetActive(isApplying);

            if (energySystem != null)
            {
                samplingGrid.SetEnergyRatio(energySystem.EnergyRatio);
            }
        }
    }

    void ApplySmoothing()
    {
        if (flowSimulation.Positions == null) return;

        Vector2[] positions = flowSimulation.Positions;
        Vector2[] velocities = flowSimulation.Velocities;
        int count = flowSimulation.AgentCount;

        float radiusSqr = currentRadius * currentRadius;

        for (int i = 0; i < count; i++)
        {
            float distSqr = (positions[i] - currentWorldPos).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                float falloff = 1f - (distSqr / radiusSqr);
                velocities[i] *= (1f - currentStrength * falloff * Time.deltaTime * 10f);
            }
        }
    }

    /// <summary>
    /// Enable or disable the tool
    /// </summary>
    public void SetToolEnabled(bool enabled)
    {
        toolEnabled = enabled;

        if (!enabled && isApplying)
        {
            isApplying = false;
            holdDuration = 0f;
            currentStrength = 0f;
        }
    }

    /// <summary>
    /// Get current tool state
    /// </summary>
    public ToolState GetToolState()
    {
        return new ToolState
        {
            worldPosition = currentWorldPos,
            isActive = isApplying,
            strength = currentStrength,
            radius = currentRadius
        };
    }

    [System.Serializable]
    public struct ToolState
    {
        public Vector2 worldPosition;
        public bool isActive;
        public float strength;
        public float radius;
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 360, 300, 240));
        GUILayout.Box("Tool Controller");
        GUILayout.Label($"Position: ({currentWorldPos.x:F1}, {currentWorldPos.y:F1})");
        GUILayout.Label($"Radius: {currentRadius:F1}");
        GUILayout.Label($"Applying: {isApplying}");
        GUILayout.Label($"Strength: {currentStrength:F2}");

        if (energySystem != null)
        {
            GUILayout.Space(10);
            GUILayout.Label($"Energy: {energySystem.CurrentEnergy:F1} / {energySystem.maxEnergy:F0}");
            GUILayout.Label($"Can Activate: {energySystem.CanActivate}");
        }

        if (performanceTracker != null)
        {
            GUILayout.Space(10);
            GUILayout.Label($"Grid Size: {performanceTracker.CurrentGridSize}");
            GUILayout.Label($"Coherence: {performanceTracker.CurrentCoherence:F2}");
        }

        GUILayout.EndArea();
    }
}
