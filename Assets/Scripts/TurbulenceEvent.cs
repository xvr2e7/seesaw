using UnityEngine;

/// <summary>
/// Defines a turbulence event - a localized disturbance in the flow field.
/// Each pattern type metaphorically represents different forms of human gathering:
/// - Circular: peaceful assembly forming a circle
/// - Scatter: panic, people running in all directions
/// - Vortex: crowd swirling around a focal point
/// - Convergence: people gathering toward a location
/// - Divergence: dispersal from a point
/// - Wave: coordinated movement like a march
/// </summary>
[System.Serializable]
public class TurbulenceEvent
{
    public enum PatternType
    {
        Circular,       // Agents orbit around a center point
        Scatter,        // Agents pushed outward randomly
        Vortex,         // Spinning motion with inward pull
        Convergence,    // Agents pulled toward center
        Divergence,     // Agents pushed away from center
        Wave,           // Directional wave pattern
        Oscillation,    // Back-and-forth movement
        Cluster         // Agents cluster and slow down
    }
    
    [Header("Identity")]
    public string eventName = "Unnamed Event";
    public PatternType pattern = PatternType.Circular;
    
    [Header("Spatial")]
    [Tooltip("Center position in world space")]
    public Vector2 position;
    
    [Tooltip("Radius of effect")]
    public float radius = 10f;
    
    [Tooltip("Inner radius for ring-shaped effects (0 = solid circle)")]
    public float innerRadius = 0f;
    
    [Header("Timing")]
    [Tooltip("When this event starts (seconds from simulation start)")]
    public float startTime = 0f;
    
    [Tooltip("Duration of the event (-1 = infinite)")]
    public float duration = 10f;
    
    [Tooltip("Time to fade in")]
    public float fadeInTime = 1f;
    
    [Tooltip("Time to fade out")]
    public float fadeOutTime = 2f;
    
    [Header("Intensity")]
    [Tooltip("Base strength of the effect")]
    [Range(0f, 10f)]
    public float strength = 3f;
    
    [Tooltip("How quickly the pattern evolves")]
    public float frequency = 1f;
    
    [Tooltip("Direction for directional patterns (Wave)")]
    public Vector2 direction = Vector2.right;
    
    // Runtime state
    [HideInInspector] public float currentIntensity = 0f;
    [HideInInspector] public float elapsedTime = 0f;
    [HideInInspector] public bool isActive = false;
    [HideInInspector] public bool isComplete = false;
    
    /// <summary>
    /// Calculate the force to apply to an agent at the given position
    /// </summary>
    public Vector2 CalculateForce(Vector2 agentPos, float time)
    {
        Vector2 toCenter = position - agentPos;
        float dist = toCenter.magnitude;
        
        // Outside radius - no effect
        if (dist > radius) return Vector2.zero;
        
        // Inside inner radius - no effect (for ring patterns)
        if (innerRadius > 0f && dist < innerRadius) return Vector2.zero;
        
        // Calculate falloff (1 at inner edge, 0 at outer edge)
        float normalizedDist = (dist - innerRadius) / (radius - innerRadius);
        float falloff = 1f - normalizedDist;
        falloff = falloff * falloff; // Quadratic falloff for smoother edges
        
        Vector2 force = Vector2.zero;
        Vector2 dirToCenter = dist > 0.001f ? toCenter / dist : Vector2.up;
        Vector2 tangent = new Vector2(-dirToCenter.y, dirToCenter.x);
        
        float phase = time * frequency;
        
        switch (pattern)
        {
            case PatternType.Circular:
                // Orbit around center
                force = tangent * strength;
                break;
                
            case PatternType.Scatter:
                // Random outward push with noise
                float noiseAngle = Mathf.PerlinNoise(agentPos.x * 0.1f + phase, agentPos.y * 0.1f) * Mathf.PI * 2f;
                Vector2 noiseDir = new Vector2(Mathf.Cos(noiseAngle), Mathf.Sin(noiseAngle));
                force = (-dirToCenter * 0.5f + noiseDir * 0.5f).normalized * strength;
                break;
                
            case PatternType.Vortex:
                // Spiral inward while rotating
                float spiralStrength = Mathf.Sin(phase) * 0.3f + 0.7f;
                force = (tangent * 0.8f + dirToCenter * 0.2f * spiralStrength) * strength;
                break;
                
            case PatternType.Convergence:
                // Pull toward center
                force = dirToCenter * strength;
                break;
                
            case PatternType.Divergence:
                // Push away from center
                force = -dirToCenter * strength;
                break;
                
            case PatternType.Wave:
                // Sinusoidal wave in specified direction
                float wavePhase = Vector2.Dot(agentPos, direction.normalized) * 0.2f + phase;
                float waveForce = Mathf.Sin(wavePhase);
                force = direction.normalized * waveForce * strength;
                break;
                
            case PatternType.Oscillation:
                // Back and forth movement
                float oscPhase = Mathf.Sin(phase + dist * 0.1f);
                force = direction.normalized * oscPhase * strength;
                break;
                
            case PatternType.Cluster:
                // Slow down and cluster (negative force opposing velocity)
                // This is handled specially in the simulation - here we apply inward pull
                force = dirToCenter * strength * 0.5f;
                break;
        }
        
        return force * falloff * currentIntensity;
    }
    
    /// <summary>
    /// Get the dampening factor for Cluster pattern (slows agents)
    /// </summary>
    public float GetDampeningFactor(Vector2 agentPos)
    {
        if (pattern != PatternType.Cluster) return 0f;
        
        Vector2 toCenter = position - agentPos;
        float dist = toCenter.magnitude;
        
        if (dist > radius) return 0f;
        if (innerRadius > 0f && dist < innerRadius) return 0f;
        
        float normalizedDist = (dist - innerRadius) / (radius - innerRadius);
        float falloff = 1f - normalizedDist;
        
        return falloff * currentIntensity * 0.8f; // Up to 80% velocity reduction
    }
    
    /// <summary>
    /// Update event timing and intensity
    /// </summary>
    public void UpdateTiming(float simulationTime)
    {
        if (isComplete) return;
        
        // Check if event should start
        if (simulationTime < startTime)
        {
            isActive = false;
            currentIntensity = 0f;
            return;
        }
        
        isActive = true;
        elapsedTime = simulationTime - startTime;
        
        // Check if event is finished
        if (duration > 0f && elapsedTime > duration)
        {
            isComplete = true;
            isActive = false;
            currentIntensity = 0f;
            return;
        }
        
        // Calculate intensity with fade in/out
        float fadeInProgress = fadeInTime > 0f ? Mathf.Clamp01(elapsedTime / fadeInTime) : 1f;
        
        float fadeOutProgress = 1f;
        if (duration > 0f && fadeOutTime > 0f)
        {
            float timeUntilEnd = duration - elapsedTime;
            fadeOutProgress = Mathf.Clamp01(timeUntilEnd / fadeOutTime);
        }
        
        // Smooth easing
        fadeInProgress = fadeInProgress * fadeInProgress * (3f - 2f * fadeInProgress);
        fadeOutProgress = fadeOutProgress * fadeOutProgress * (3f - 2f * fadeOutProgress);
        
        currentIntensity = fadeInProgress * fadeOutProgress;
    }
    
    /// <summary>
    /// Reset event to initial state
    /// </summary>
    public void Reset()
    {
        currentIntensity = 0f;
        elapsedTime = 0f;
        isActive = false;
        isComplete = false;
    }
    
    /// <summary>
    /// Create a copy of this event
    /// </summary>
    public TurbulenceEvent Clone()
    {
        return new TurbulenceEvent
        {
            eventName = eventName,
            pattern = pattern,
            position = position,
            radius = radius,
            innerRadius = innerRadius,
            startTime = startTime,
            duration = duration,
            fadeInTime = fadeInTime,
            fadeOutTime = fadeOutTime,
            strength = strength,
            frequency = frequency,
            direction = direction
        };
    }
}