using UnityEngine;

/// <summary>
/// Creates gratifying particle effects when the player uses the dampening tool.
/// Spawns particles at affected agent positions to provide crisp visual feedback.
/// </summary>
public class DampeningParticleEffect : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    public PlayerToolController playerTool;

    [Header("Particle Settings")]
    [Tooltip("Number of particles to spawn per affected agent")]
    [Range(1, 5)]
    public int particlesPerAgent = 2;

    [Tooltip("Chance to spawn particles for an affected agent (lower = less dense)")]
    [Range(0f, 1f)]
    public float spawnChance = 0.3f;

    [Tooltip("Maximum particles to spawn per frame")]
    public int maxParticlesPerFrame = 50;

    [Header("Particle Appearance")]
    public Color particleColor = new Color(0.6f, 0.9f, 1f, 0.8f);
    public Color particleColorEnd = new Color(0.3f, 0.6f, 0.9f, 0f);

    [Tooltip("Particle lifetime in seconds")]
    [Range(0.1f, 2f)]
    public float particleLifetime = 0.6f;

    [Tooltip("Particle size")]
    [Range(0.05f, 0.5f)]
    public float particleSize = 0.15f;

    [Tooltip("Speed particles move outward")]
    [Range(0.5f, 10f)]
    public float particleSpeed = 3f;

    [Header("Rendering")]
    public Mesh particleMesh;
    public Material particleMaterial;

    // Particle data
    private struct Particle
    {
        public Vector2 position;
        public Vector2 velocity;
        public float lifetime;
        public float age;
    }

    private Particle[] particles;
    private int activeParticleCount = 0;
    private const int MAX_PARTICLES = 500;

    // Rendering
    private Matrix4x4[] matrices;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        ValidateReferences();
        InitializeParticles();
    }

    void ValidateReferences()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();

        if (playerTool == null)
            playerTool = FindObjectOfType<PlayerToolController>();

        if (particleMesh == null)
            particleMesh = CreateQuadMesh();

        if (particleMaterial == null)
            particleMaterial = CreateDefaultMaterial();
    }

    void InitializeParticles()
    {
        particles = new Particle[MAX_PARTICLES];
        matrices = new Matrix4x4[MAX_PARTICLES];
        propertyBlock = new MaterialPropertyBlock();
        activeParticleCount = 0;
    }

    void Update()
    {
        if (flowSimulation == null || playerTool == null) return;

        float dt = Time.deltaTime;

        // Spawn particles if tool is active
        var toolState = playerTool.GetToolState();
        if (toolState.isActive && toolState.strength > 0.1f)
        {
            SpawnParticlesAtAffectedAgents(toolState);
        }

        // Update existing particles
        UpdateParticles(dt);

        // Render particles
        RenderParticles();
    }

    void SpawnParticlesAtAffectedAgents(PlayerToolController.ToolState toolState)
    {
        if (flowSimulation.Positions == null) return;

        Vector2[] positions = flowSimulation.Positions;
        int count = flowSimulation.AgentCount;
        float radiusSqr = toolState.radius * toolState.radius;

        int spawnedThisFrame = 0;

        for (int i = 0; i < count && spawnedThisFrame < maxParticlesPerFrame; i++)
        {
            float distSqr = (positions[i] - toolState.worldPosition).sqrMagnitude;

            // Check if agent is within dampening radius
            if (distSqr < radiusSqr)
            {
                // Random chance to spawn particles (avoids overwhelming visual noise)
                if (Random.value < spawnChance * Time.deltaTime * 30f)
                {
                    float falloff = 1f - (distSqr / radiusSqr);

                    for (int p = 0; p < particlesPerAgent; p++)
                    {
                        SpawnParticle(positions[i], falloff);
                        spawnedThisFrame++;

                        if (spawnedThisFrame >= maxParticlesPerFrame)
                            break;
                    }
                }
            }
        }
    }

    void SpawnParticle(Vector2 position, float intensity)
    {
        if (activeParticleCount >= MAX_PARTICLES) return;

        // Random outward direction
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        particles[activeParticleCount] = new Particle
        {
            position = position + direction * Random.Range(0f, 0.3f),
            velocity = direction * particleSpeed * Random.Range(0.5f, 1.5f),
            lifetime = particleLifetime * Random.Range(0.8f, 1.2f),
            age = 0f
        };

        activeParticleCount++;
    }

    void UpdateParticles(float dt)
    {
        for (int i = activeParticleCount - 1; i >= 0; i--)
        {
            particles[i].age += dt;

            // Remove dead particles
            if (particles[i].age >= particles[i].lifetime)
            {
                // Swap with last particle and reduce count
                particles[i] = particles[activeParticleCount - 1];
                activeParticleCount--;
                continue;
            }

            // Update position
            particles[i].position += particles[i].velocity * dt;

            // Decelerate particles over time
            particles[i].velocity *= 1f - (dt * 2f);
        }
    }

    void RenderParticles()
    {
        if (activeParticleCount == 0) return;

        for (int i = 0; i < activeParticleCount; i++)
        {
            float t = particles[i].age / particles[i].lifetime;

            // Particle fades out and shrinks over lifetime
            float scale = particleSize * (1f - t * 0.5f);
            Color color = Color.Lerp(particleColor, particleColorEnd, t);

            Vector3 position = new Vector3(particles[i].position.x, particles[i].position.y, -0.5f);
            matrices[i] = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * scale);

            // Note: GPU instancing with per-instance colors requires shader support
            // For simplicity, we'll batch render with interpolated color
        }

        // Render all particles (simple approach - could be optimized with instancing)
        for (int i = 0; i < activeParticleCount; i++)
        {
            float t = particles[i].age / particles[i].lifetime;
            Color color = Color.Lerp(particleColor, particleColorEnd, t);

            propertyBlock.SetColor(ColorProperty, color);

            Graphics.DrawMesh(
                particleMesh,
                matrices[i],
                particleMaterial,
                0,
                null,
                0,
                propertyBlock
            );
        }
    }

    Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ParticleQuad";

        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };

        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("LaminarFlow/AgentCircle");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", particleColor);
            return mat;
        }

        return null;
    }

    void OnDestroy()
    {
        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
        }

        if (particleMesh != null)
        {
            Destroy(particleMesh);
        }
    }
}
