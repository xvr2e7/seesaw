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

    [Header("Turbulence-Based Coloring")]
    [Tooltip("Enable gray baseline with colorful turbulence highlighting")]
    public bool useTurbulenceColoring = true;

    [Tooltip("Base color for normal (non-turbulent) agents")]
    public Color normalAgentColor = new Color(0.28f, 0.28f, 0.30f, 0.55f);

    [Tooltip("Color for dampened agents")]
    public Color dampenedAgentColor = new Color(0.42f, 0.46f, 0.52f, 0.65f);

    [Header("Fallback Color")]
    public Color fallbackColor = new Color(0.9f, 0.9f, 0.9f, 0.6f);
    
    // Instancing data
    private Matrix4x4[] matrices;
    private Vector4[] colors;
    private MaterialPropertyBlock propertyBlock;
    
    // Shader property IDs (cached for performance)
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
        float[] turbulenceInfluence = flowSimulation.TurbulenceInfluence;
        int[] turbulencePattern = flowSimulation.TurbulencePattern;
        float[] dampeningFactors = flowSimulation.DampeningFactors;
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

            // Calculate color
            Color agentColor;
            if (colorByVelocity && useTurbulenceColoring)
            {
                agentColor = GetTurbulenceBasedColor(velocities[i], turbulenceInfluence[i], turbulencePattern[i], dampeningFactors[i], maxSpeed);
            }
            else if (colorByVelocity)
            {
                agentColor = VelocityToColorClassic(velocities[i], maxSpeed);
            }
            else
            {
                agentColor = fallbackColor;
                agentColor.a *= agentOpacity;
            }

            colors[i] = new Vector4(agentColor.r, agentColor.g, agentColor.b, agentColor.a);
        }
    }
    
    /// <summary>
    /// Turbulence-based coloring per GAMEPLAY_DESCRIPTION.md:
    ///   - Slow agents: RGB(0.25, 0.25, 0.25) dark gray
    ///   - Fast agents: RGB(0.65, 0.65, 0.65) medium gray
    ///   - Pattern colors blended in via: pow(saturate((turbulence-0.05)/0.45), 0.5)
    ///   - SCAN active: shift toward blue-tinted light gray RGB(0.7, 0.7, 0.75)
    /// </summary>
    Color GetTurbulenceBasedColor(Vector2 velocity, float turbulence, int pattern, float dampening, float maxSpeed)
    {
        float magnitude = velocity.magnitude;
        float speedRatio = Mathf.Clamp01(magnitude / maxSpeed);

        // Base gray: slow=0.25 dark gray, fast=0.65 medium gray (doc spec)
        float grayValue = Mathf.Lerp(0.25f, 0.65f, speedRatio);
        Color grayColor = new Color(grayValue, grayValue, grayValue, agentOpacity);

        // Pattern-specific colors per doc spec (intentionally desaturated)
        // 1=Circular, 2=Scatter, 3=Vortex, 4=Wave, 5=Oscillation, 6=Cluster
        Color patternColor = grayColor; // default: no tint

        switch (pattern)
        {
            case 1: patternColor = new Color(0.40f, 0.75f, 0.50f, agentOpacity); break; // Circular: sage green
            case 2: patternColor = new Color(0.85f, 0.45f, 0.45f, agentOpacity); break; // Scatter: dull rose
            case 3: patternColor = new Color(0.65f, 0.50f, 0.75f, agentOpacity); break; // Vortex: muted lavender
            case 4: patternColor = new Color(0.40f, 0.60f, 0.75f, agentOpacity); break; // Wave: slate blue
            case 5: patternColor = new Color(0.80f, 0.75f, 0.35f, agentOpacity); break; // Oscillation: straw yellow
            case 6: patternColor = new Color(0.55f, 0.55f, 0.60f, agentOpacity); break; // Cluster: cool gray
        }

        // SCAN dampening: shift toward blue-tinted light gray (doc: RGB 0.7, 0.7, 0.75)
        if (dampening > 0.2f)
        {
            return Color.Lerp(
                grayColor,
                new Color(0.70f, 0.70f, 0.75f, agentOpacity),
                dampening
            );
        }

        // Blend gray → pattern color using doc formula:
        // turbulenceFactor = pow(saturate((turbulence − 0.05) / 0.45), 0.5)
        float t = Mathf.Clamp01((turbulence - 0.05f) / 0.45f);
        float turbulenceFactor = Mathf.Pow(t, 0.5f);

        return Color.Lerp(grayColor, patternColor, turbulenceFactor);
    }

    /// <summary>
    /// Classic velocity-based coloring (fallback)
    /// </summary>
    Color VelocityToColorClassic(Vector2 velocity, float maxSpeed)
    {
        float magnitude = velocity.magnitude;

        // Calculate hue from direction
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
            Vector4[] batchColors = new Vector4[batchCount];

            System.Array.Copy(matrices, batchStart, batchMatrices, 0, batchCount);
            System.Array.Copy(colors, batchStart, batchColors, 0, batchCount);

            // Pass per-instance colors using SetVectorArray
            // The shader indexes into _Colors array using instanceID
            propertyBlock.SetVectorArray(ColorsArrayProperty, batchColors);

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