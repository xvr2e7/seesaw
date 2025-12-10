using UnityEngine;

/// <summary>
/// Handles player input for flow manipulation tools.
/// Click and drag to apply dampening effect that smooths local velocity.
/// Strength ramps up the longer the mouse is held.
/// Scroll wheel adjusts tool radius.
/// 
/// Energy system: Tool depletes energy while active, regenerates when idle.
/// </summary>
public class PlayerToolController : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    public Camera mainCamera;
    
    [Header("Tool Settings")]
    [Tooltip("Radius of effect in world units")]
    public float toolRadius = 8f;
    
    [Tooltip("Minimum tool radius")]
    public float minRadius = 2f;
    
    [Tooltip("Maximum tool radius")]
    public float maxRadius = 25f;
    
    [Tooltip("How fast scroll wheel changes radius")]
    public float scrollSensitivity = 0.5f;
    
    [Tooltip("Base dampening strength")]
    [Range(0.1f, 1f)]
    public float baseDampeningStrength = 0.3f;
    
    [Tooltip("Maximum dampening strength after full ramp-up")]
    [Range(0.5f, 1f)]
    public float maxDampeningStrength = 0.85f;
    
    [Tooltip("Time in seconds to reach maximum strength")]
    [Range(0.1f, 5f)]
    public float rampUpTime = 1.5f;
    
    [Header("Energy System")]
    [Tooltip("Maximum energy pool")]
    public float maxEnergy = 100f;
    
    [Tooltip("Energy consumed per second while tool is active")]
    public float energyDrainRate = 20f;
    
    [Tooltip("Energy regenerated per second while tool is inactive")]
    public float energyRegenRate = 8f;
    
    [Tooltip("Delay before energy starts regenerating after use")]
    public float regenDelay = 0.5f;
    
    [Tooltip("Minimum energy required to activate tool")]
    public float minActivationEnergy = 5f;
    
    [Tooltip("Strength multiplier when energy is low")]
    public AnimationCurve energyStrengthCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);
    
    [Header("Cursor Appearance")]
    public Color ringColorFull = new Color(0.9f, 0.95f, 1f, 0.8f);
    public Color ringColorLow = new Color(1f, 0.6f, 0.3f, 0.6f);
    public Color ringColorDepleted = new Color(0.5f, 0.3f, 0.3f, 0.3f);
    public Color energyRingColor = new Color(0.4f, 0.8f, 0.5f, 0.7f);
    public Color energyRingColorLow = new Color(0.9f, 0.4f, 0.2f, 0.7f);
    
    [Tooltip("Ring thickness as fraction of radius")]
    [Range(0.01f, 0.1f)]
    public float ringThicknessFraction = 0.03f;
    
    [Tooltip("Energy ring offset from main ring")]
    public float energyRingOffset = 0.4f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Runtime state
    private Vector2 currentWorldPos;
    private bool isApplying = false;
    private float holdDuration = 0f;
    private float currentStrength = 0f;
    private int agentsAffectedLastFrame = 0;
    
    // Energy state
    private float currentEnergy;
    private float timeSinceLastUse = 0f;
    private bool energyDepleted = false;
    
    // Cursor rendering
    private LineRenderer ringLine;
    private LineRenderer energyRingLine;
    private const int RING_SEGMENTS = 64;
    
    // Tool enabled state (can be disabled by GameManager)
    private bool toolEnabled = true;
    
    void Start()
    {
        ValidateReferences();
        CreateRingCursor();
        
        // Initialize energy
        currentEnergy = maxEnergy;
        timeSinceLastUse = regenDelay; // Allow immediate use
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
    
    void CreateRingCursor()
    {
        // Main tool radius ring
        GameObject ringObj = new GameObject("ToolCursorRing");
        ringObj.transform.SetParent(transform);
        
        ringLine = ringObj.AddComponent<LineRenderer>();
        ringLine.useWorldSpace = true;
        ringLine.loop = true;
        ringLine.positionCount = RING_SEGMENTS;
        
        // Simple unlit material
        ringLine.material = new Material(Shader.Find("Sprites/Default"));
        ringLine.startColor = ringColorFull;
        ringLine.endColor = ringColorFull;
        
        UpdateRingWidth();
        ringLine.sortingOrder = 100;
        
        // Energy arc ring (slightly larger, shows energy as arc)
        GameObject energyObj = new GameObject("ToolEnergyRing");
        energyObj.transform.SetParent(transform);
        
        energyRingLine = energyObj.AddComponent<LineRenderer>();
        energyRingLine.useWorldSpace = true;
        energyRingLine.loop = false; // Not looped - it's an arc
        energyRingLine.positionCount = RING_SEGMENTS + 1;
        
        energyRingLine.material = new Material(Shader.Find("Sprites/Default"));
        energyRingLine.startColor = energyRingColor;
        energyRingLine.endColor = energyRingColor;
        energyRingLine.sortingOrder = 99;
    }
    
    void UpdateRingWidth()
    {
        float width = toolRadius * ringThicknessFraction;
        ringLine.startWidth = width;
        ringLine.endWidth = width;
    }
    
    void Update()
    {
        if (flowSimulation == null || mainCamera == null) return;
        
        UpdateWorldPosition();
        UpdateScrollWheel();
        UpdateEnergy();
        UpdateToolInput();
        UpdateRingCursor();
        
        if (isApplying)
        {
            ApplyDampening();
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
            toolRadius += scroll * scrollSensitivity;
            toolRadius = Mathf.Clamp(toolRadius, minRadius, maxRadius);
            UpdateRingWidth();
        }
    }
    
    void UpdateEnergy()
    {
        if (isApplying)
        {
            // Drain energy while tool is active
            currentEnergy -= energyDrainRate * Time.deltaTime;
            currentEnergy = Mathf.Max(0f, currentEnergy);
            timeSinceLastUse = 0f;
            
            // Check for depletion
            if (currentEnergy <= 0f)
            {
                energyDepleted = true;
            }
        }
        else
        {
            // Regenerate energy when idle (after delay)
            timeSinceLastUse += Time.deltaTime;
            
            if (timeSinceLastUse >= regenDelay)
            {
                currentEnergy += energyRegenRate * Time.deltaTime;
                currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
                
                // Reset depleted flag when we have enough energy
                if (currentEnergy >= minActivationEnergy)
                {
                    energyDepleted = false;
                }
            }
        }
    }
    
    void UpdateToolInput()
    {
        // Check if tool can be used
        bool canActivate = toolEnabled && 
                          currentEnergy >= minActivationEnergy && 
                          !energyDepleted;
        
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
            
            // Calculate strength with ramp-up
            float rampProgress = Mathf.Clamp01(holdDuration / rampUpTime);
            rampProgress = rampProgress * rampProgress * (3f - 2f * rampProgress); // Smoothstep
            float baseStrength = Mathf.Lerp(baseDampeningStrength, maxDampeningStrength, rampProgress);
            
            // Apply energy modifier
            float energyRatio = currentEnergy / maxEnergy;
            float energyModifier = energyStrengthCurve.Evaluate(energyRatio);
            
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
    
    void ApplyDampening()
    {
        agentsAffectedLastFrame = ApplyDampeningWithCount(currentWorldPos, toolRadius, currentStrength);
    }
    
    int ApplyDampeningWithCount(Vector2 center, float radius, float dampening)
    {
        if (flowSimulation.Positions == null) return 0;
        
        Vector2[] positions = flowSimulation.Positions;
        Vector2[] velocities = flowSimulation.Velocities;
        int count = flowSimulation.AgentCount;
        
        float radiusSqr = radius * radius;
        int affected = 0;
        
        for (int i = 0; i < count; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                float falloff = 1f - (distSqr / radiusSqr);
                velocities[i] *= (1f - dampening * falloff * Time.deltaTime * 10f);
                affected++;
            }
        }
        
        return affected;
    }
    
    void UpdateRingCursor()
    {
        if (ringLine == null) return;
        
        // Update main ring color based on state
        Color ringColor;
        float energyRatio = currentEnergy / maxEnergy;
        
        if (energyDepleted || currentEnergy < minActivationEnergy)
        {
            ringColor = ringColorDepleted;
        }
        else if (energyRatio < 0.3f)
        {
            ringColor = Color.Lerp(ringColorDepleted, ringColorLow, energyRatio / 0.3f);
        }
        else
        {
            ringColor = Color.Lerp(ringColorLow, ringColorFull, (energyRatio - 0.3f) / 0.7f);
        }
        
        // Brighten when active
        if (isApplying)
        {
            ringColor = Color.Lerp(ringColor, Color.white, 0.3f);
            ringColor.a = Mathf.Min(1f, ringColor.a * 1.3f);
        }
        
        ringLine.startColor = ringColor;
        ringLine.endColor = ringColor;
        
        // Draw main circle at cursor position
        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            float angle = (float)i / RING_SEGMENTS * Mathf.PI * 2f;
            float x = currentWorldPos.x + Mathf.Cos(angle) * toolRadius;
            float y = currentWorldPos.y + Mathf.Sin(angle) * toolRadius;
            ringLine.SetPosition(i, new Vector3(x, y, -1f));
        }
        
        // Draw energy arc (radial progress indicator)
        if (energyRingLine != null)
        {
            float energyRadius = toolRadius + energyRingOffset;
            
            // Energy arc color
            Color arcColor = Color.Lerp(energyRingColorLow, energyRingColor, energyRatio);
            if (energyDepleted)
            {
                arcColor.a *= 0.3f;
            }
            
            // Pulse when low
            if (energyRatio < 0.3f && !energyDepleted)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 5f);
                arcColor.a *= pulse;
            }
            
            energyRingLine.startColor = arcColor;
            energyRingLine.endColor = arcColor;
            
            float energyWidth = toolRadius * ringThicknessFraction * 1.5f;
            energyRingLine.startWidth = energyWidth;
            energyRingLine.endWidth = energyWidth;
            
            // Draw arc from top, going clockwise based on energy
            int arcSegments = Mathf.Max(1, Mathf.FloorToInt(RING_SEGMENTS * energyRatio));
            energyRingLine.positionCount = arcSegments + 1;
            
            for (int i = 0; i <= arcSegments; i++)
            {
                // Start from top (-90 degrees), go clockwise
                float t = (float)i / RING_SEGMENTS;
                float angle = -Mathf.PI * 0.5f + t * Mathf.PI * 2f;
                float x = currentWorldPos.x + Mathf.Cos(angle) * energyRadius;
                float y = currentWorldPos.y + Mathf.Sin(angle) * energyRadius;
                energyRingLine.SetPosition(i, new Vector3(x, y, -1f));
            }
        }
    }
    
    void OnDestroy()
    {
        if (ringLine != null)
            Destroy(ringLine.gameObject);
        
        if (energyRingLine != null)
            Destroy(energyRingLine.gameObject);
    }
    
    // Public API
    
    /// <summary>
    /// Get current energy (0 to maxEnergy)
    /// </summary>
    public float GetCurrentEnergy()
    {
        return currentEnergy;
    }
    
    /// <summary>
    /// Get current energy as ratio (0 to 1)
    /// </summary>
    public float GetEnergyRatio()
    {
        return currentEnergy / maxEnergy;
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

        // Hide both ring renderers when disabled
        if (ringLine != null)
        {
            ringLine.enabled = enabled;
        }

        if (energyRingLine != null)
        {
            energyRingLine.enabled = enabled;
        }
    }
    
    public void SetToolRadius(float radius)
    {
        toolRadius = Mathf.Clamp(radius, minRadius, maxRadius);
        UpdateRingWidth();
    }
    
    /// <summary>
    /// Restore energy (e.g., from power-up)
    /// </summary>
    public void RestoreEnergy(float amount)
    {
        currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);
        
        if (currentEnergy >= minActivationEnergy)
        {
            energyDepleted = false;
        }
    }
    
    public ToolState GetToolState()
    {
        return new ToolState
        {
            worldPosition = currentWorldPos,
            isActive = isApplying,
            strength = currentStrength,
            radius = toolRadius
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
        
        GUILayout.BeginArea(new Rect(10, 360, 300, 220));
        GUILayout.Box("Tool Controller");
        GUILayout.Label($"Position: ({currentWorldPos.x:F1}, {currentWorldPos.y:F1})");
        GUILayout.Label($"Radius: {toolRadius:F1} [scroll to change]");
        GUILayout.Label($"Applying: {isApplying}");
        GUILayout.Label($"Strength: {currentStrength:F2}");
        GUILayout.Label($"Agents Affected: {agentsAffectedLastFrame}");
        GUILayout.Space(10);
        GUILayout.Label($"Energy: {currentEnergy:F1} / {maxEnergy:F0}");
        GUILayout.Label($"Energy Ratio: {GetEnergyRatio() * 100:F0}%");
        GUILayout.Label($"Depleted: {energyDepleted}");
        GUILayout.Label($"Time Since Use: {timeSinceLastUse:F1}s");
        GUILayout.EndArea();
    }
}