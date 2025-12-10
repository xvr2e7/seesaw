using UnityEngine;

public class FlowSimulation : MonoBehaviour
{
    [Header("Simulation Settings")]
    public int agentCount = 800;
    
    public float worldHeight = 60f;
    public float targetAspectRatio = 1.778f; // 16:9
    
    [Header("Movement Settings")]
    public float moveSpeed = 1f;
    
    [Range(0f, 5f)]
    public float wanderStrength = 2.0f;
    
    [Range(0f, 20f)]
    public float turnSpeed = 5f;

    [Header("Dampening Physics")]
    [Tooltip("How fast agents recover from being dampened (lower = effect lasts longer)")]
    public float dampeningRecoveryRate = 0.5f; 
    
    [Header("Flow Metrics")]
    [Tooltip("Current average divergence from mean flow (turbulence indicator)")]
    [SerializeField] private float currentDivergence = 0f;
    
    [Tooltip("Smoothing for divergence calculation")]
    [Range(0.5f, 10f)]
    public float divergenceSmoothing = 5f;
    
    [Header("Debug")]
    public bool showDebugGizmos = true;
    
    // Agent data
    private Vector2[] positions;
    private Vector2[] velocities;
    private Vector2[] desiredDirections;
    private float[] dampeningFactors; // 0 = normal, 1 = fully suppressed
    
    private Vector2 worldSize;
    
    // Flow metrics
    private Vector2 meanVelocity;
    private float velocityVariance;
    
    // Public Accessors
    public Vector2[] Positions => positions;
    public Vector2[] Velocities => velocities;
    public int AgentCount => agentCount;
    public Vector2 WorldSize => worldSize;
    public Vector2 WorldCenter => Vector2.zero;
    
    /// <summary>
    /// Current divergence metric (0 = perfectly laminar, higher = more turbulent)
    /// </summary>
    public float CurrentDivergence => currentDivergence;
    
    /// <summary>
    /// Mean velocity of all agents
    /// </summary>
    public Vector2 MeanVelocity => meanVelocity;
    
    void Awake()
    {
        InitializeAgents();
    }
    
    void Update()
    {
        // Prevent NullReferenceException if initialization failed or hasn't run
        if (positions == null) return;

        float dt = Time.deltaTime;
        
        UpdateWanderDirections(dt);
        UpdateVelocities(dt);
        UpdatePositions(dt);
        HandleEdgeBouncing();
        
        // Calculate flow metrics
        UpdateFlowMetrics(dt);
    }
    
    void InitializeAgents()
    {
        // Calculate world size based on target aspect ratio
        worldSize = new Vector2(worldHeight * targetAspectRatio, worldHeight);
        
        // Prevent zero-size world issues
        if (worldSize.x < 1f) worldSize.x = 1f;
        if (worldSize.y < 1f) worldSize.y = 1f;

        positions = new Vector2[agentCount];
        velocities = new Vector2[agentCount];
        desiredDirections = new Vector2[agentCount];
        dampeningFactors = new float[agentCount];
        
        Vector2 halfSize = worldSize * 0.5f;
        
        for (int i = 0; i < agentCount; i++)
        {
            positions[i] = new Vector2(
                Random.Range(-halfSize.x, halfSize.x),
                Random.Range(-halfSize.y, halfSize.y)
            );
            
            float angle = Random.Range(0f, Mathf.PI * 2f);
            desiredDirections[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            velocities[i] = desiredDirections[i] * moveSpeed;
            dampeningFactors[i] = 0f;
        }
        
        Debug.Log($"[FlowSimulation] Initialized {agentCount} agents in {worldSize.x:F0}x{worldSize.y:F0}");
    }
    
    void UpdateWanderDirections(float dt)
    {
        for (int i = 0; i < agentCount; i++)
        {
            // If heavily dampened, don't change direction much
            float effectiveWander = wanderStrength * (1f - dampeningFactors[i]);

            float noiseAngle = (Random.value - 0.5f) * 2f * effectiveWander * dt;
            float cos = Mathf.Cos(noiseAngle);
            float sin = Mathf.Sin(noiseAngle);
            Vector2 dir = desiredDirections[i];
            
            desiredDirections[i] = new Vector2(
                dir.x * cos - dir.y * sin,
                dir.x * sin + dir.y * cos
            ).normalized;
        }
    }
    
    void UpdateVelocities(float dt)
    {
        for (int i = 0; i < agentCount; i++)
        {
            // Recover from dampening
            if (dampeningFactors[i] > 0f)
            {
                dampeningFactors[i] -= dampeningRecoveryRate * dt;
                if (dampeningFactors[i] < 0f) dampeningFactors[i] = 0f;
            }

            Vector2 targetVelocity = desiredDirections[i] * moveSpeed;
            
            // Dampened behavior: target velocity is ZERO. 
            // This prevents them from immediately accelerating back to full speed.
            if (dampeningFactors[i] > 0f)
            {
                targetVelocity = Vector2.Lerp(targetVelocity, Vector2.zero, dampeningFactors[i]);
            }

            velocities[i] = Vector2.Lerp(velocities[i], targetVelocity, turnSpeed * dt);
        }
    }

    void UpdatePositions(float dt)
    {
        for (int i = 0; i < agentCount; i++)
        {
            positions[i] += velocities[i] * dt;
        }
    }
    
    void HandleEdgeBouncing()
    {
        Vector2 halfSize = worldSize * 0.5f;
        
        for (int i = 0; i < agentCount; i++)
        {
            Vector2 pos = positions[i];
            Vector2 vel = velocities[i];
            Vector2 dir = desiredDirections[i];
            bool bounced = false;
            
            if (pos.x < -halfSize.x) 
            { 
                pos.x = -halfSize.x;
                vel.x = Mathf.Abs(vel.x);
                dir.x = Mathf.Abs(dir.x);
                bounced = true; 
            }
            else if (pos.x > halfSize.x) 
            { 
                pos.x = halfSize.x; 
                vel.x = -Mathf.Abs(vel.x); 
                dir.x = -Mathf.Abs(dir.x); 
                bounced = true; 
            }
            
            if (pos.y < -halfSize.y) 
            { 
                pos.y = -halfSize.y; 
                vel.y = Mathf.Abs(vel.y); 
                dir.y = Mathf.Abs(dir.y); 
                bounced = true; 
            }
            else if (pos.y > halfSize.y) 
            { 
                pos.y = halfSize.y; 
                vel.y = -Mathf.Abs(vel.y); 
                dir.y = -Mathf.Abs(dir.y); 
                bounced = true; 
            }
            
            if (bounced)
            {
                positions[i] = pos;
                velocities[i] = vel;
                desiredDirections[i] = dir.normalized;
            }
        }
    }
    
    /// <summary>
    /// Calculate flow metrics for divergence/turbulence measurement
    /// </summary>
    void UpdateFlowMetrics(float dt)
    {
        if (velocities == null || agentCount == 0) return;
        
        // Calculate mean velocity
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < agentCount; i++)
        {
            sum += velocities[i];
        }
        Vector2 newMean = sum / agentCount;
        
        // Calculate variance (divergence from mean)
        float varianceSum = 0f;
        for (int i = 0; i < agentCount; i++)
        {
            Vector2 diff = velocities[i] - newMean;
            varianceSum += diff.sqrMagnitude;
        }
        float newVariance = varianceSum / agentCount;
        
        // Smooth the metrics
        float smoothFactor = 1f - Mathf.Exp(-divergenceSmoothing * dt);
        meanVelocity = Vector2.Lerp(meanVelocity, newMean, smoothFactor);
        velocityVariance = Mathf.Lerp(velocityVariance, newVariance, smoothFactor);
        
        // Revised Metric: Normalize based on speed
        // 0.0 = Perfectly uniform motion
        // > 1.0 = Chaotic
        currentDivergence = Mathf.Sqrt(velocityVariance) / moveSpeed;
        
        // Visual clamp
        currentDivergence = Mathf.Clamp(currentDivergence, 0f, 2f);
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(worldSize.x, worldSize.y, 0.1f));
        
        if (positions != null && Application.isPlaying)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.5f);
            for (int i = 0; i < Mathf.Min(agentCount, 100); i++)
            {
                Vector3 pos3D = new Vector3(positions[i].x, positions[i].y, 0f);
                Gizmos.DrawSphere(pos3D, 0.15f);
                Gizmos.color = new Color(0.8f, 0.4f, 0.2f, 0.5f);
                Gizmos.DrawLine(pos3D, pos3D + new Vector3(velocities[i].x, velocities[i].y, 0f) * 0.3f);
                Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.5f);
            }
        }
    }

    /// <summary>
    /// Apply a force to all agents within a radius
    /// </summary>
    public void ApplyForceInRadius(Vector2 center, float radius, Vector2 force)
    {
        float radiusSqr = radius * radius;
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                float falloff = 1f - (distSqr / radiusSqr);
                
                // Breaking the dampening lock when force is applied allows turbulence to "win"
                dampeningFactors[i] *= 0.8f; 
                velocities[i] += force * falloff;
            }
        }
    }
    
    /// <summary>
    /// Dampen velocities within a radius (player's smoothing tool)
    /// </summary>
    public void DampenInRadius(Vector2 center, float radius, float dampening)
    {
        float radiusSqr = radius * radius;
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                float falloff = 1f - (distSqr / radiusSqr);
                
                // Add to persistent dampening factor
                dampeningFactors[i] = Mathf.Min(dampeningFactors[i] + dampening * falloff * 0.1f, 1f);
                
                // Also apply immediate velocity cut
                velocities[i] *= (1f - dampening * falloff * 0.2f);
            }
        }
    }
    
    /// <summary>
    /// Inject turbulence: circular/orbital motion around a point
    /// </summary>
    public void InjectCircularTurbulence(Vector2 center, float radius, float strength, float dt)
    {
        float radiusSqr = radius * radius;
        float adjustedStrength = strength * 5f; // Buffed for dramatic effect
        
        for (int i = 0; i < agentCount; i++)
        {
            Vector2 toCenter = center - positions[i];
            float distSqr = toCenter.sqrMagnitude;
            
            if (distSqr < radiusSqr && distSqr > 0.01f)
            {
                float dist = Mathf.Sqrt(distSqr);
                float falloff = 1f - (dist / radius);
                falloff *= falloff;
                
                // Tangent direction (perpendicular to center)
                Vector2 tangent = new Vector2(-toCenter.y, toCenter.x).normalized;
                velocities[i] += tangent * adjustedStrength * falloff * dt;
            }
        }
    }
    
    /// <summary>
    /// Inject turbulence: scatter/divergent motion from a point
    /// </summary>
    public void InjectScatterTurbulence(Vector2 center, float radius, float strength, float dt)
    {
        float radiusSqr = radius * radius;
        float adjustedStrength = strength * 8f; // Buffed for dramatic effect

        for (int i = 0; i < agentCount; i++)
        {
            Vector2 fromCenter = positions[i] - center;
            float distSqr = fromCenter.sqrMagnitude;
            
            if (distSqr < radiusSqr && distSqr > 0.01f)
            {
                float dist = Mathf.Sqrt(distSqr);
                float falloff = 1f - (dist / radius);
                falloff *= falloff;
                
                // Outward direction with some randomness
                Vector2 outward = fromCenter.normalized;
                float noiseAngle = (Mathf.PerlinNoise(positions[i].x * 0.1f, positions[i].y * 0.1f) - 0.5f) * Mathf.PI;
                Vector2 noisy = new Vector2(
                    outward.x * Mathf.Cos(noiseAngle) - outward.y * Mathf.Sin(noiseAngle),
                    outward.x * Mathf.Sin(noiseAngle) + outward.y * Mathf.Cos(noiseAngle)
                );
                
                velocities[i] += noisy * adjustedStrength * falloff * dt;
            }
        }
    }
    
    /// <summary>
    /// Inject turbulence: vortex/spiral motion toward a point
    /// </summary>
    public void InjectVortexTurbulence(Vector2 center, float radius, float strength, float inwardPull, float dt)
    {
        float radiusSqr = radius * radius;
        float adjustedStrength = strength * 6f; // Buffed

        for (int i = 0; i < agentCount; i++)
        {
            Vector2 toCenter = center - positions[i];
            float distSqr = toCenter.sqrMagnitude;
            
            if (distSqr < radiusSqr && distSqr > 0.01f)
            {
                float dist = Mathf.Sqrt(distSqr);
                float falloff = 1f - (dist / radius);
                falloff *= falloff;
                
                Vector2 dirToCenter = toCenter / dist;
                Vector2 tangent = new Vector2(-dirToCenter.y, dirToCenter.x);
                
                // Spiral: mostly tangent with some inward pull
                Vector2 spiral = tangent + dirToCenter * inwardPull;
                velocities[i] += spiral.normalized * adjustedStrength * falloff * dt;
            }
        }
    }
    
    /// <summary>
    /// Get the average velocity in a region (for measuring local turbulence)
    /// </summary>
    public Vector2 GetAverageVelocityInRadius(Vector2 center, float radius)
    {
        float radiusSqr = radius * radius;
        Vector2 sum = Vector2.zero;
        int count = 0;
        
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                sum += velocities[i];
                count++;
            }
        }
        
        return count > 0 ? sum / count : Vector2.zero;
    }
    
    /// <summary>
    /// Get local divergence in a region (for measuring local turbulence)
    /// </summary>
    public float GetLocalDivergence(Vector2 center, float radius)
    {
        float radiusSqr = radius * radius;
        Vector2 sum = Vector2.zero;
        int count = 0;
        
        // First pass: calculate local mean
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                sum += velocities[i];
                count++;
            }
        }
        
        if (count < 2) return 0f;
        
        Vector2 localMean = sum / count;
        
        // Second pass: calculate variance
        float variance = 0f;
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                Vector2 diff = velocities[i] - localMean;
                variance += diff.sqrMagnitude;
            }
        }
        
        variance /= count;
        return Mathf.Sqrt(variance) / moveSpeed;
    }
    
    /// <summary>
    /// Count agents within a radius
    /// </summary>
    public int CountAgentsInRadius(Vector2 center, float radius)
    {
        float radiusSqr = radius * radius;
        int count = 0;
        
        for (int i = 0; i < agentCount; i++)
        {
            if ((positions[i] - center).sqrMagnitude < radiusSqr)
            {
                count++;
            }
        }
        
        return count;
    }
}