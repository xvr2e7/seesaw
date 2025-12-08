using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Schedules and manages turbulence events in the simulation.
/// Handles both pre-scripted events and dynamic event spawning.
/// 
/// Clinical terminology masks the true nature:
/// - "Anomaly" = peaceful gathering
/// - "Entropy spike" = panic/flight  
/// - "Vector divergence" = dispersal
/// - "Flow obstruction" = protest blocking movement
/// </summary>
public class TurbulentEventScheduler : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    
    [Header("Scheduling Mode")]
    [Tooltip("Use pre-defined event sequence")]
    public bool useScriptedEvents = true;
    
    [Tooltip("Also spawn random events")]
    public bool enableRandomEvents = true;
    
    [Header("Random Event Settings")]
    [Tooltip("Minimum time between random events")]
    public float minEventInterval = 8f;
    
    [Tooltip("Maximum time between random events")]
    public float maxEventInterval = 20f;
    
    [Tooltip("Delay before first event")]
    public float initialDelay = 15f;
    
    [Tooltip("Maximum simultaneous random events")]
    public int maxSimultaneousEvents = 3;
    
    [Header("Event Parameters")]
    [Tooltip("Minimum radius for random events")]
    public float minRadius = 5f;
    
    [Tooltip("Maximum radius for random events")]
    public float maxRadius = 15f;
    
    [Tooltip("Minimum duration for random events")]
    public float minDuration = 5f;
    
    [Tooltip("Maximum duration for random events")]
    public float maxDuration = 15f;
    
    [Tooltip("Base strength multiplier")]
    [Range(0.5f, 3f)]
    public float strengthMultiplier = 1f;
    
    [Header("Difficulty Scaling")]
    [Tooltip("Increase event frequency over time")]
    public bool scaleDifficulty = true;
    
    [Tooltip("How quickly difficulty increases")]
    [Range(0f, 0.1f)]
    public float difficultyRamp = 0.02f;
    
    [Tooltip("Maximum difficulty multiplier")]
    public float maxDifficultyMultiplier = 2f;
    
    [Header("Scripted Events")]
    [Tooltip("Pre-defined event sequence")]
    public List<TurbulenceEvent> scriptedEvents = new List<TurbulenceEvent>();
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    public bool showEventGizmos = true;
    
    // Runtime state
    private List<TurbulenceEvent> activeEvents = new List<TurbulenceEvent>();
    private float simulationTime = 0f;
    private float nextRandomEventTime = 0f;
    private int totalEventsSpawned = 0;
    private float currentDifficulty = 1f;
    
    // Event tracking
    private int activeRandomEventCount = 0;
    
    void Start()
    {
        if (flowSimulation == null)
        {
            flowSimulation = FindObjectOfType<FlowSimulation>();
        }
        
        if (flowSimulation == null)
        {
            Debug.LogError("[TurbulentEventScheduler] No FlowSimulation found!");
            enabled = false;
            return;
        }
        
        // Initialize scripted events
        foreach (var evt in scriptedEvents)
        {
            evt.Reset();
        }
        
        // Set initial random event time
        nextRandomEventTime = initialDelay;
        
        // If no scripted events, create default sequence
        if (scriptedEvents.Count == 0 && useScriptedEvents)
        {
            CreateDefaultEventSequence();
        }
        
        Debug.Log($"[TurbulentEventScheduler] Initialized with {scriptedEvents.Count} scripted events");
    }
    
    void Update()
    {
        float dt = Time.deltaTime;
        simulationTime += dt;
        
        // Update difficulty
        if (scaleDifficulty)
        {
            currentDifficulty = Mathf.Min(
                1f + simulationTime * difficultyRamp,
                maxDifficultyMultiplier
            );
        }
        
        // Update scripted events
        if (useScriptedEvents)
        {
            UpdateScriptedEvents();
        }
        
        // Spawn random events
        if (enableRandomEvents)
        {
            UpdateRandomEvents();
        }
        
        // Apply all active events to simulation
        ApplyTurbulenceToSimulation(dt);
        
        // Clean up completed events
        CleanupCompletedEvents();
    }
    
    void UpdateScriptedEvents()
    {
        foreach (var evt in scriptedEvents)
        {
            evt.UpdateTiming(simulationTime);
            
            // Add to active list if just activated
            if (evt.isActive && !activeEvents.Contains(evt))
            {
                activeEvents.Add(evt);
                OnEventStarted(evt);
            }
        }
    }
    
    void UpdateRandomEvents()
    {
        // Count active random events
        activeRandomEventCount = 0;
        foreach (var evt in activeEvents)
        {
            if (evt.eventName.StartsWith("Random"))
            {
                activeRandomEventCount++;
            }
        }
        
        // Check if we should spawn a new random event
        if (simulationTime >= nextRandomEventTime && activeRandomEventCount < maxSimultaneousEvents)
        {
            SpawnRandomEvent();
            
            // Schedule next event (shorter intervals at higher difficulty)
            float interval = Random.Range(minEventInterval, maxEventInterval) / currentDifficulty;
            nextRandomEventTime = simulationTime + interval;
        }
    }
    
    void SpawnRandomEvent()
    {
        TurbulenceEvent evt = CreateRandomEvent();
        evt.startTime = simulationTime; // Start immediately
        evt.Reset();
        evt.UpdateTiming(simulationTime);
        
        scriptedEvents.Add(evt); // Add to list for persistence
        activeEvents.Add(evt);
        
        totalEventsSpawned++;
        OnEventStarted(evt);
    }
    
    TurbulenceEvent CreateRandomEvent()
    {
        // Get world bounds from simulation
        Vector2 worldSize = flowSimulation.WorldSize;
        Vector2 halfSize = worldSize * 0.5f;
        
        // Random position within world bounds (with margin)
        float margin = maxRadius;
        Vector2 position = new Vector2(
            Random.Range(-halfSize.x + margin, halfSize.x - margin),
            Random.Range(-halfSize.y + margin, halfSize.y - margin)
        );
        
        // Random pattern (weighted toward more visually interesting ones)
        TurbulenceEvent.PatternType pattern = GetWeightedRandomPattern();
        
        // Random parameters scaled by difficulty
        float radius = Random.Range(minRadius, maxRadius) * (0.8f + currentDifficulty * 0.2f);
        float duration = Random.Range(minDuration, maxDuration);
        float strength = Random.Range(1.5f, 3.5f) * strengthMultiplier * currentDifficulty;
        
        // Direction for directional patterns
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
        
        return new TurbulenceEvent
        {
            eventName = $"Random_{pattern}_{totalEventsSpawned}",
            pattern = pattern,
            position = position,
            radius = radius,
            innerRadius = pattern == TurbulenceEvent.PatternType.Circular ? radius * 0.3f : 0f,
            startTime = simulationTime,
            duration = duration,
            fadeInTime = 1.5f,
            fadeOutTime = 2f,
            strength = strength,
            frequency = Random.Range(0.5f, 2f),
            direction = direction
        };
    }
    
    TurbulenceEvent.PatternType GetWeightedRandomPattern()
    {
        // Weighted random selection - favor visually distinct patterns
        float roll = Random.value;
        
        if (roll < 0.25f) return TurbulenceEvent.PatternType.Circular;      // 25% - peaceful assembly
        if (roll < 0.40f) return TurbulenceEvent.PatternType.Vortex;        // 15% - spiral gathering
        if (roll < 0.55f) return TurbulenceEvent.PatternType.Scatter;       // 15% - panic
        if (roll < 0.70f) return TurbulenceEvent.PatternType.Convergence;   // 15% - gathering
        if (roll < 0.80f) return TurbulenceEvent.PatternType.Cluster;       // 10% - sit-in/blockade
        if (roll < 0.90f) return TurbulenceEvent.PatternType.Wave;          // 10% - march
        return TurbulenceEvent.PatternType.Divergence;                       // 10% - dispersal
    }
    
    void ApplyTurbulenceToSimulation(float dt)
    {
        if (flowSimulation.Positions == null) return;
        
        Vector2[] positions = flowSimulation.Positions;
        Vector2[] velocities = flowSimulation.Velocities;
        int count = flowSimulation.AgentCount;
        
        // Apply each active event
        foreach (var evt in activeEvents)
        {
            if (!evt.isActive) continue;
            
            for (int i = 0; i < count; i++)
            {
                // Calculate and apply force
                Vector2 force = evt.CalculateForce(positions[i], simulationTime);
                velocities[i] += force * dt;
                
                // Apply dampening for Cluster pattern
                float dampen = evt.GetDampeningFactor(positions[i]);
                if (dampen > 0f)
                {
                    velocities[i] *= (1f - dampen * dt * 3f);
                }
            }
        }
    }
    
    void CleanupCompletedEvents()
    {
        for (int i = activeEvents.Count - 1; i >= 0; i--)
        {
            if (activeEvents[i].isComplete)
            {
                OnEventEnded(activeEvents[i]);
                activeEvents.RemoveAt(i);
            }
        }
    }
    
    void OnEventStarted(TurbulenceEvent evt)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[Turbulence] EVENT STARTED: {evt.eventName} ({evt.pattern}) at ({evt.position.x:F1}, {evt.position.y:F1})");
        }
    }
    
    void OnEventEnded(TurbulenceEvent evt)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[Turbulence] Event ended: {evt.eventName}");
        }
    }
    
    /// <summary>
    /// Creates a default event sequence for testing
    /// </summary>
    void CreateDefaultEventSequence()
    {
        Vector2 worldSize = flowSimulation.WorldSize;
        Vector2 halfSize = worldSize * 0.5f;
        
        // Event 1: Circular gathering at 15 seconds (peaceful assembly)
        scriptedEvents.Add(new TurbulenceEvent
        {
            eventName = "Initial_Assembly",
            pattern = TurbulenceEvent.PatternType.Circular,
            position = new Vector2(halfSize.x * 0.3f, 0f),
            radius = 12f,
            innerRadius = 4f,
            startTime = 15f,
            duration = 20f,
            fadeInTime = 2f,
            fadeOutTime = 3f,
            strength = 2.5f,
            frequency = 0.8f
        });
        
        // Event 2: Convergence at 25 seconds (crowd gathering)
        scriptedEvents.Add(new TurbulenceEvent
        {
            eventName = "Gathering_Point",
            pattern = TurbulenceEvent.PatternType.Convergence,
            position = new Vector2(-halfSize.x * 0.4f, halfSize.y * 0.3f),
            radius = 10f,
            startTime = 25f,
            duration = 15f,
            fadeInTime = 1.5f,
            fadeOutTime = 2f,
            strength = 3f,
            frequency = 1f
        });
        
        // Event 3: Vortex at 40 seconds (spiral formation)
        scriptedEvents.Add(new TurbulenceEvent
        {
            eventName = "Spiral_Formation",
            pattern = TurbulenceEvent.PatternType.Vortex,
            position = new Vector2(0f, -halfSize.y * 0.3f),
            radius = 15f,
            startTime = 40f,
            duration = 18f,
            fadeInTime = 2f,
            fadeOutTime = 2.5f,
            strength = 2.8f,
            frequency = 1.2f
        });
        
        // Event 4: Scatter at 60 seconds (panic event)
        scriptedEvents.Add(new TurbulenceEvent
        {
            eventName = "Panic_Scatter",
            pattern = TurbulenceEvent.PatternType.Scatter,
            position = new Vector2(halfSize.x * 0.2f, halfSize.y * 0.4f),
            radius = 14f,
            startTime = 60f,
            duration = 12f,
            fadeInTime = 0.5f, // Fast onset - sudden panic
            fadeOutTime = 3f,
            strength = 4f,
            frequency = 2f
        });
        
        // Event 5: Cluster at 75 seconds (sit-in/blockade)
        scriptedEvents.Add(new TurbulenceEvent
        {
            eventName = "Blockade",
            pattern = TurbulenceEvent.PatternType.Cluster,
            position = new Vector2(-halfSize.x * 0.2f, -halfSize.y * 0.2f),
            radius = 10f,
            startTime = 75f,
            duration = 25f,
            fadeInTime = 3f,
            fadeOutTime = 2f,
            strength = 2f,
            frequency = 0.5f
        });
        
        // Event 6: Wave at 90 seconds (march)
        scriptedEvents.Add(new TurbulenceEvent
        {
            eventName = "March_Wave",
            pattern = TurbulenceEvent.PatternType.Wave,
            position = Vector2.zero,
            radius = 25f,
            startTime = 90f,
            duration = 20f,
            fadeInTime = 2f,
            fadeOutTime = 3f,
            strength = 3f,
            frequency = 0.7f,
            direction = new Vector2(1f, 0.3f).normalized
        });
        
        Debug.Log($"[TurbulentEventScheduler] Created {scriptedEvents.Count} default events");
    }
    
    /// <summary>
    /// Manually trigger a turbulence event at a position
    /// </summary>
    public void TriggerEventAt(Vector2 position, TurbulenceEvent.PatternType pattern, float radius = 10f, float duration = 10f)
    {
        var evt = new TurbulenceEvent
        {
            eventName = $"Manual_{pattern}_{totalEventsSpawned}",
            pattern = pattern,
            position = position,
            radius = radius,
            startTime = simulationTime,
            duration = duration,
            fadeInTime = 1f,
            fadeOutTime = 2f,
            strength = 3f * strengthMultiplier,
            frequency = 1f
        };
        
        evt.Reset();
        evt.UpdateTiming(simulationTime);
        
        scriptedEvents.Add(evt);
        activeEvents.Add(evt);
        totalEventsSpawned++;
        
        OnEventStarted(evt);
    }
    
    /// <summary>
    /// Get the total turbulence intensity at a point (for UI/metrics)
    /// </summary>
    public float GetTurbulenceIntensityAt(Vector2 position)
    {
        float totalIntensity = 0f;
        
        foreach (var evt in activeEvents)
        {
            if (!evt.isActive) continue;
            
            Vector2 force = evt.CalculateForce(position, simulationTime);
            totalIntensity += force.magnitude;
        }
        
        return totalIntensity;
    }
    
    /// <summary>
    /// Get list of currently active events
    /// </summary>
    public List<TurbulenceEvent> GetActiveEvents()
    {
        return new List<TurbulenceEvent>(activeEvents);
    }
    
    /// <summary>
    /// Reset all events (for game restart)
    /// </summary>
    public void ResetAllEvents()
    {
        activeEvents.Clear();
        simulationTime = 0f;
        nextRandomEventTime = initialDelay;
        totalEventsSpawned = 0;
        currentDifficulty = 1f;
        
        // Reset scripted events but keep random ones removed
        for (int i = scriptedEvents.Count - 1; i >= 0; i--)
        {
            if (scriptedEvents[i].eventName.StartsWith("Random") || 
                scriptedEvents[i].eventName.StartsWith("Manual"))
            {
                scriptedEvents.RemoveAt(i);
            }
            else
            {
                scriptedEvents[i].Reset();
            }
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(320, 10, 300, 250));
        GUILayout.Box("Turbulence Scheduler");
        GUILayout.Label($"Simulation Time: {simulationTime:F1}s");
        GUILayout.Label($"Difficulty: {currentDifficulty:F2}x");
        GUILayout.Label($"Active Events: {activeEvents.Count}");
        GUILayout.Label($"Total Spawned: {totalEventsSpawned}");
        GUILayout.Label($"Next Random: {nextRandomEventTime - simulationTime:F1}s");
        
        GUILayout.Space(5);
        GUILayout.Label("Active Events:");
        foreach (var evt in activeEvents)
        {
            GUILayout.Label($"  • {evt.eventName}: {evt.currentIntensity:F2}");
        }
        GUILayout.EndArea();
    }
    
    void OnDrawGizmos()
    {
        if (!showEventGizmos || activeEvents == null) return;
        
        foreach (var evt in activeEvents)
        {
            if (!evt.isActive) continue;
            
            // Color based on pattern type
            Color eventColor = GetPatternColor(evt.pattern);
            eventColor.a = evt.currentIntensity * 0.5f;
            
            Gizmos.color = eventColor;
            
            // Draw outer radius
            DrawGizmoCircle(evt.position, evt.radius, 32);
            
            // Draw inner radius if present
            if (evt.innerRadius > 0f)
            {
                Gizmos.color = new Color(eventColor.r, eventColor.g, eventColor.b, eventColor.a * 0.5f);
                DrawGizmoCircle(evt.position, evt.innerRadius, 24);
            }
            
            // Draw center point
            Gizmos.color = eventColor;
            Gizmos.DrawSphere(new Vector3(evt.position.x, evt.position.y, 0f), 0.5f);
            
            // Draw direction for directional patterns
            if (evt.pattern == TurbulenceEvent.PatternType.Wave || 
                evt.pattern == TurbulenceEvent.PatternType.Oscillation)
            {
                Vector3 center = new Vector3(evt.position.x, evt.position.y, 0f);
                Vector3 dir = new Vector3(evt.direction.x, evt.direction.y, 0f) * evt.radius * 0.5f;
                Gizmos.DrawLine(center, center + dir);
            }
        }
    }
    
    void DrawGizmoCircle(Vector2 center, float radius, int segments)
    {
        Vector3 prevPoint = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 point = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius,
                0f
            );
            
            if (i > 0)
            {
                Gizmos.DrawLine(prevPoint, point);
            }
            prevPoint = point;
        }
    }
    
    Color GetPatternColor(TurbulenceEvent.PatternType pattern)
    {
        switch (pattern)
        {
            case TurbulenceEvent.PatternType.Circular:    return new Color(0.2f, 0.8f, 0.2f);  // Green - peaceful
            case TurbulenceEvent.PatternType.Scatter:     return new Color(1f, 0.3f, 0.2f);    // Red - panic
            case TurbulenceEvent.PatternType.Vortex:      return new Color(0.8f, 0.4f, 0.8f);  // Purple - spiral
            case TurbulenceEvent.PatternType.Convergence: return new Color(0.2f, 0.6f, 1f);   // Blue - gathering
            case TurbulenceEvent.PatternType.Divergence:  return new Color(1f, 0.6f, 0.2f);   // Orange - dispersal
            case TurbulenceEvent.PatternType.Wave:        return new Color(0.2f, 1f, 0.8f);   // Cyan - march
            case TurbulenceEvent.PatternType.Oscillation: return new Color(1f, 1f, 0.2f);    // Yellow
            case TurbulenceEvent.PatternType.Cluster:     return new Color(0.6f, 0.6f, 0.6f); // Gray - blockade
            default: return Color.white;
        }
    }
}