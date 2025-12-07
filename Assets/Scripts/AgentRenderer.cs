using UnityEngine;

public class AgentRenderer : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    
    [Header("Rendering")]
    public Mesh agentMesh;
    public Material agentMaterial;
    
    [Tooltip("Size of each agent")]
    public float agentSize = 0.25f;
    
    [Tooltip("Z position for rendering (should be in front of flow quad)")]
    public float renderHeight = 0f;
    
    [Header("Appearance")]
    [Tooltip("Base alpha/opacity of agents")]
    [Range(0f, 1f)]
    public float agentOpacity = 0.6f;
    
    [Tooltip("Whether to color agents by their velocity")]
    public bool colorByVelocity = true;
    
    [Tooltip("Hue offset to match flow visualization (degrees)")]
    [Range(0f, 360f)]
    public float hueOffset = 0f;
    
    [Tooltip("Saturation of velocity-based coloring")]
    [Range(0f, 1f)]
    public float saturation = 0.85f;
    
    [Tooltip("Brightness/value of velocity-based coloring")]
    [Range(0f, 1f)]
    public float brightness = 0.95f;
    
    [Header("Fallback Color")]
    public Color fallbackColor = new Color(0.9f, 0.9f, 0.9f, 0.6f);
    
    // Instancing data
    private Matrix4x4[] matrices;
    private Vector4[] colors;
    private MaterialPropertyBlock propertyBlock;
    
    // Shader property IDs (cached for performance)
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorsArrayProperty = Shader.PropertyToID("_Colors");
    
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
        
        UpdateMatricesAndColors();
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
            agentMesh = CreateQuadMesh();
        }
        
        if (agentMaterial == null)
        {
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
        colors = new Vector4[count];
        propertyBlock = new MaterialPropertyBlock();
    }
    
    void UpdateMatricesAndColors()
    {
        Vector2[] positions = flowSimulation.Positions;
        Vector2[] velocities = flowSimulation.Velocities;
        int count = flowSimulation.AgentCount;
        float maxSpeed = flowSimulation.moveSpeed * 2f;
        
        // Ensure arrays match
        if (matrices == null || matrices.Length != count)
        {
            matrices = new Matrix4x4[count];
            colors = new Vector4[count];
        }
        
        Vector3 scale = Vector3.one * agentSize;
        
        for (int i = 0; i < count; i++)
        {
            Vector3 position = new Vector3(positions[i].x, positions[i].y, renderHeight);
            
            // Rotate agents to face their velocity direction
            Quaternion rotation = Quaternion.identity;
            if (velocities[i].sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(velocities[i].y, velocities[i].x) * Mathf.Rad2Deg;
                rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
            
            matrices[i] = Matrix4x4.TRS(position, rotation, scale);
            
            // Calculate color from velocity
            if (colorByVelocity)
            {
                Color velColor = VelocityToColor(velocities[i], maxSpeed);
                colors[i] = new Vector4(velColor.r, velColor.g, velColor.b, velColor.a);
            }
            else
            {
                colors[i] = new Vector4(fallbackColor.r, fallbackColor.g, fallbackColor.b, fallbackColor.a * agentOpacity);
            }
        }
    }
    
    /// <summary>
    /// Converts velocity to HSV color matching the optical flow visualization
    /// </summary>
    Color VelocityToColor(Vector2 velocity, float maxSpeed)
    {
        float magnitude = velocity.magnitude;
        
        // Calculate hue from direction (same formula as shader)
        float angle = Mathf.Atan2(velocity.y, velocity.x);
        float hue = (angle / (2f * Mathf.PI)) + 0.5f;
        hue = (hue + hueOffset / 360f) % 1f;
        if (hue < 0f) hue += 1f;
        
        // Scale saturation and value by speed
        float speedRatio = Mathf.Clamp01(magnitude / maxSpeed);
        float sat = Mathf.Lerp(0.3f, saturation, speedRatio);
        float val = Mathf.Lerp(0.5f, brightness, speedRatio);
        
        Color rgb = HSVToRGB(hue, sat, val);
        rgb.a = agentOpacity;
        
        return rgb;
    }
    
    /// <summary>
    /// HSV to RGB conversion
    /// </summary>
    Color HSVToRGB(float h, float s, float v)
    {
        h = h % 1f;
        if (h < 0f) h += 1f;
        
        float c = v * s;
        float x = c * (1f - Mathf.Abs((h * 6f) % 2f - 1f));
        float m = v - c;
        
        float r, g, b;
        
        if (h < 1f / 6f)
        {
            r = c; g = x; b = 0f;
        }
        else if (h < 2f / 6f)
        {
            r = x; g = c; b = 0f;
        }
        else if (h < 3f / 6f)
        {
            r = 0f; g = c; b = x;
        }
        else if (h < 4f / 6f)
        {
            r = 0f; g = x; b = c;
        }
        else if (h < 5f / 6f)
        {
            r = x; g = 0f; b = c;
        }
        else
        {
            r = c; g = 0f; b = x;
        }
        
        return new Color(r + m, g + m, b + m, 1f);
    }
    
    void DrawAgents()
    {
        int count = flowSimulation.AgentCount;
        
        // Draw in batches (GPU instancing limit is 1023 per call)
        for (int batchStart = 0; batchStart < count; batchStart += BATCH_SIZE)
        {
            int batchCount = Mathf.Min(BATCH_SIZE, count - batchStart);
            
            // Create batch arrays
            Matrix4x4[] batchMatrices = new Matrix4x4[batchCount];
            System.Array.Copy(matrices, batchStart, batchMatrices, 0, batchCount);
            
            // For per-instance colors, we need to set them individually
            // Since MaterialPropertyBlock doesn't support per-instance colors easily,
            // we'll use the average color for the batch (or first color)
            // For true per-instance colors, we'd need a custom shader with instanced properties
            
            // Use the color of the first agent in the batch as representative
            // (This is a simplification - for true per-instance colors, shader modification needed)
            propertyBlock.SetColor(ColorProperty, colors[batchStart]);
            
            Graphics.DrawMeshInstanced(
                agentMesh,
                0,
                agentMaterial,
                batchMatrices,
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
        Shader shader = Shader.Find("LaminarFlow/AgentCircle");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        
        Material mat = new Material(shader);
        mat.enableInstancing = true;
        mat.SetColor("_BaseColor", fallbackColor);
        
        return mat;
    }
    
    public void SetOpacity(float opacity)
    {
        agentOpacity = Mathf.Clamp01(opacity);
    }
    
    public void SetColorByVelocity(bool enabled)
    {
        colorByVelocity = enabled;
    }
}