using UnityEngine;

public class FlowSimulation : MonoBehaviour
{
    [Header("Simulation Settings")]
    public int agentCount = 300;
    
    public float worldHeight = 60f;
    public float targetAspectRatio = 1.778f; // 16:9
    
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    
    [Range(0f, 5f)]
    public float wanderStrength = 1.5f;
    
    [Range(0f, 10f)]
    public float turnSpeed = 3f;
    
    [Header("Debug")]
    public bool showDebugGizmos = true;
    
    // Agent data
    private Vector2[] positions;
    private Vector2[] velocities;
    private Vector2[] desiredDirections;
    
    private Vector2 worldSize;
    
    public Vector2[] Positions => positions;
    public Vector2[] Velocities => velocities;
    public int AgentCount => agentCount;
    public Vector2 WorldSize => worldSize;
    public Vector2 WorldCenter => Vector2.zero;
    
    // Ensure data is ready before other scripts access it
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
        }
        
        Debug.Log($"[FlowSimulation] Initialized {agentCount} agents in {worldSize.x:F0}x{worldSize.y:F0}");
    }
    
    void UpdateWanderDirections(float dt)
    {
        for (int i = 0; i < agentCount; i++)
        {
            float noiseAngle = (Random.value - 0.5f) * 2f * wanderStrength * dt;
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
            Vector2 targetVelocity = desiredDirections[i] * moveSpeed;
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

    public void ApplyForceInRadius(Vector2 center, float radius, Vector2 force)
    {
        float radiusSqr = radius * radius;
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                float falloff = 1f - (distSqr / radiusSqr);
                velocities[i] += force * falloff;
            }
        }
    }
    
    public void DampenInRadius(Vector2 center, float radius, float dampening)
    {
        float radiusSqr = radius * radius;
        for (int i = 0; i < agentCount; i++)
        {
            float distSqr = (positions[i] - center).sqrMagnitude;
            if (distSqr < radiusSqr)
            {
                float falloff = 1f - (distSqr / radiusSqr);
                velocities[i] *= (1f - dampening * falloff);
            }
        }
    }
}