using UnityEngine;

public class AgentRenderer : MonoBehaviour
{
    [Header("References")]

    public FlowSimulation flowSimulation;
    
    [Header("Rendering")]
    public Mesh agentMesh;
    public Material agentMaterial;
    public float agentSize = 0.3f;
    public float renderHeight = 0.1f;
    
    [Header("Appearance")]
    public Color baseColor = new Color(0.9f, 0.9f, 0.9f, 0.8f);
    
    // Instancing data
    private Matrix4x4[] matrices;
    private MaterialPropertyBlock propertyBlock;
    
    // Shader property IDs (cached for performance)
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
    
    // GPU instancing batch limit
    private const int BATCH_SIZE = 1023;
    
    void Start()
    {
        ValidateSetup();
        InitializeRenderData();
    }
    
    void LateUpdate()
    {
        if (flowSimulation == null || flowSimulation.Positions == null) return;
        
        UpdateMatrices();
        DrawAgents();
    }
    
    void ValidateSetup()
    {
        if (flowSimulation == null)
        {
            flowSimulation = FindObjectOfType<FlowSimulation>();
        }
        
        if (agentMesh == null)
        {
            // Create a simple quad mesh
            agentMesh = CreateQuadMesh();
        }
        
        if (agentMaterial == null)
        {
            // Try to create a basic instanced material
            agentMaterial = CreateDefaultMaterial();
        }
        
        // Verify material supports instancing
        if (!agentMaterial.enableInstancing)
        {
            agentMaterial.enableInstancing = true;
        }
    }
    
    void InitializeRenderData()
    {
        int count = flowSimulation.AgentCount;
        matrices = new Matrix4x4[count];
        propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(ColorProperty, baseColor);
    }
    
    void UpdateMatrices()
    {
        Vector2[] positions = flowSimulation.Positions;
        Vector2[] velocities = flowSimulation.Velocities;
        int count = flowSimulation.AgentCount;
        
        // Ensure arrays match
        if (matrices == null || matrices.Length != count)
        {
            matrices = new Matrix4x4[count];
        }
        
        Vector3 scale = Vector3.one * agentSize;
        
        for (int i = 0; i < count; i++)
        {
            Vector3 position = new Vector3(positions[i].x, positions[i].y, renderHeight);
            
            // rotate agents to face their velocity direction
            Quaternion rotation = Quaternion.identity;
            if (velocities[i].sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(velocities[i].y, velocities[i].x) * Mathf.Rad2Deg;
                rotation = Quaternion.Euler(0f, 0f, angle - 90f); // -90 to align "forward" with up
            }
            
            matrices[i] = Matrix4x4.TRS(position, rotation, scale);
        }
    }
    
    void DrawAgents()
    {
        int count = flowSimulation.AgentCount;
        
        // Draw in batches (GPU instancing limit is 1023 per call)
        for (int i = 0; i < count; i += BATCH_SIZE)
        {
            int batchCount = Mathf.Min(BATCH_SIZE, count - i);
            
            // Create a slice of matrices for this batch
            Matrix4x4[] batch;
            if (batchCount == BATCH_SIZE && i == 0)
            {
                // First full batch can use the array directly if it fits
                batch = matrices;
                if (count > BATCH_SIZE)
                {
                    batch = new Matrix4x4[batchCount];
                    System.Array.Copy(matrices, i, batch, 0, batchCount);
                }
            }
            else
            {
                batch = new Matrix4x4[batchCount];
                System.Array.Copy(matrices, i, batch, 0, batchCount);
            }
            
            Graphics.DrawMeshInstanced(
                agentMesh,
                0,
                agentMaterial,
                batch,
                batchCount,
                propertyBlock,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false
            );
        }
    }
    
    Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "AgentQuad";
        
        // Simple quad vertices (1x1, centered)
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
        // Try to find URP Lit shader first, fall back to standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        
        Material mat = new Material(shader);
        mat.enableInstancing = true;
        mat.SetColor("_BaseColor", baseColor);
        
        return mat;
    }

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        if (propertyBlock != null)
        {
            propertyBlock.SetColor(ColorProperty, baseColor);
        }
    }
}
