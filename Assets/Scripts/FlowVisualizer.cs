using UnityEngine;

/// <summary>
/// Samples agent velocities from FlowSimulation into a grid texture.
/// The texture stores velocity as RG channels (normalized and biased to 0-1 range).
/// A shader then converts this to HSV optical flow colors.
/// </summary>
public class FlowVisualizer : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    
    [Header("Grid Settings")]
    [Tooltip("Resolution of the velocity grid texture")]
    public int gridResolution = 128;
    
    [Tooltip("How quickly the flow field responds to changes")]
    [Range(0.5f, 20f)]
    public float temporalSmoothing = 8f;
    
    [Header("Visualization")]
    public Material flowMaterial;
    
    [Tooltip("Multiplier for velocity magnitude affecting color saturation/brightness")]
    [Range(0.1f, 5f)]
    public float velocityScale = 1.5f;
    
    [Header("Appearance")]
    [Range(0f, 1f)]
    public float saturationMin = 0.4f;
    
    [Range(0f, 1f)]
    public float saturationMax = 0.95f;
    
    [Range(0f, 1f)]
    public float valueMin = 0.2f;
    
    [Range(0f, 1f)]
    public float valueMax = 0.9f;
    
    [Tooltip("Hue rotation offset in degrees")]
    [Range(0f, 360f)]
    public float hueOffset = 0f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Internal references
    private GameObject flowQuadGO;
    private MeshRenderer flowQuadRenderer;
    
    // Internal texture and data
    private Texture2D velocityTexture;
    private Color[] velocityPixels;
    private Vector2[] velocityAccumulator;
    private float[] weightAccumulator;
    
    // Cached values
    private Vector2 worldSize;
    private Vector2 worldMin;
    private Vector2 worldMax;
    private float cellSizeX;
    private float cellSizeY;
    private bool isInitialized = false;
    
    // Shader property IDs
    private static readonly int VelocityTexProperty = Shader.PropertyToID("_VelocityTex");
    private static readonly int VelocityScaleProperty = Shader.PropertyToID("_VelocityScale");
    private static readonly int SaturationRangeProperty = Shader.PropertyToID("_SaturationRange");
    private static readonly int ValueRangeProperty = Shader.PropertyToID("_ValueRange");
    private static readonly int HueOffsetProperty = Shader.PropertyToID("_HueOffset");
    
    void Start()
    {
        Initialize();
    }
    
    void Initialize()
    {
        // Find FlowSimulation
        if (flowSimulation == null)
        {
            flowSimulation = FindObjectOfType<FlowSimulation>();
        }
        
        if (flowSimulation == null)
        {
            Debug.LogError("[FlowVisualizer] No FlowSimulation found!");
            return;
        }
        
        // Wait for simulation to initialize
        if (flowSimulation.WorldSize == Vector2.zero)
        {
            Debug.Log("[FlowVisualizer] Waiting for FlowSimulation to initialize...");
            Invoke(nameof(Initialize), 0.1f);
            return;
        }
        
        worldSize = flowSimulation.WorldSize;
        
        // World is centered at origin, so bounds are symmetric
        worldMin = -worldSize * 0.5f;
        worldMax = worldSize * 0.5f;
        
        // Cell sizes for X and Y (in case aspect ratio differs from 1:1)
        cellSizeX = worldSize.x / gridResolution;
        cellSizeY = worldSize.y / gridResolution;
        
        Debug.Log($"[FlowVisualizer] World: {worldMin} to {worldMax}, Cell size: ({cellSizeX:F3}, {cellSizeY:F3})");
        
        InitializeTexture();
        CreateOrFindMaterial();
        SetupFlowQuad();
        
        isInitialized = true;
    }
    
    void InitializeTexture()
    {
        // Create the velocity texture (RG = velocity XY, biased to 0-1)
        velocityTexture = new Texture2D(gridResolution, gridResolution, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "VelocityField"
        };
        
        velocityPixels = new Color[gridResolution * gridResolution];
        velocityAccumulator = new Vector2[gridResolution * gridResolution];
        weightAccumulator = new float[gridResolution * gridResolution];
        
        // Initialize to neutral (0.5, 0.5 = zero velocity)
        for (int i = 0; i < velocityPixels.Length; i++)
        {
            velocityPixels[i] = new Color(0.5f, 0.5f, 0f, 1f);
        }
        velocityTexture.SetPixels(velocityPixels);
        velocityTexture.Apply();
        
        Debug.Log($"[FlowVisualizer] Created {gridResolution}x{gridResolution} velocity texture");
    }
    
    void CreateOrFindMaterial()
    {
        if (flowMaterial != null)
        {
            Debug.Log("[FlowVisualizer] Using assigned material");
            return;
        }
        
        // Try to find the OpticalFlowHSV shader
        Shader flowShader = Shader.Find("LaminarFlow/OpticalFlowHSV");
        
        if (flowShader == null)
        {
            flowShader = Shader.Find("Universal Render Pipeline/Unlit");
            
            if (flowShader == null)
            {
                flowShader = Shader.Find("Unlit/Color");
            }
        }
        
        flowMaterial = new Material(flowShader);
        flowMaterial.name = "FlowVisualization_Runtime";
        
        // Set default values
        flowMaterial.SetTexture(VelocityTexProperty, velocityTexture);
        flowMaterial.SetFloat(VelocityScaleProperty, velocityScale);
        flowMaterial.SetVector(SaturationRangeProperty, new Vector4(saturationMin, saturationMax, 0f, 0f));
        flowMaterial.SetVector(ValueRangeProperty, new Vector4(valueMin, valueMax, 0f, 0f));
        flowMaterial.SetFloat(HueOffsetProperty, hueOffset / 360f);
    }
    
    void SetupFlowQuad()
    {
        // Create a quad to display the flow visualization
        flowQuadGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flowQuadGO.name = "FlowVisualizationQuad";
        flowQuadGO.transform.SetParent(transform);
        
        // Remove collider
        Collider col = flowQuadGO.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        
        flowQuadRenderer = flowQuadGO.GetComponent<MeshRenderer>();
        
        // Position the quad at world center, at Z=1 (behind agents which are at Z=0.1)
        // The quad's local scale will match the world size exactly
        flowQuadGO.transform.position = new Vector3(0f, 0f, 1f);
        flowQuadGO.transform.rotation = Quaternion.identity;
        flowQuadGO.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
        
        // Assign material
        flowQuadRenderer.material = flowMaterial;
        flowQuadRenderer.material.SetTexture(VelocityTexProperty, velocityTexture);
        
        // Disable shadows
        flowQuadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flowQuadRenderer.receiveShadows = false;
        
        Debug.Log($"[FlowVisualizer] Created flow quad at (0,0,1), scale=({worldSize.x}, {worldSize.y})");
    }
    
    void Update()
    {
        if (!isInitialized) return;
        if (flowSimulation == null || flowSimulation.Positions == null) return;
        
        // Sample velocities into grid
        SampleVelocitiesToGrid();
        
        // Update texture
        UpdateTexture();
        
        // Update shader properties
        UpdateShaderProperties();
    }
    
    void SampleVelocitiesToGrid()
    {
        // Clear accumulators
        System.Array.Clear(velocityAccumulator, 0, velocityAccumulator.Length);
        System.Array.Clear(weightAccumulator, 0, weightAccumulator.Length);
        
        Vector2[] positions = flowSimulation.Positions;
        Vector2[] velocities = flowSimulation.Velocities;
        int agentCount = flowSimulation.AgentCount;
        
        // Accumulate velocities into grid cells with bilinear splatting
        for (int i = 0; i < agentCount; i++)
        {
            Vector2 pos = positions[i];
            Vector2 vel = velocities[i];
            
            // Convert world position to normalized grid coordinates (0 to gridResolution)
            // World goes from worldMin to worldMax
            // Grid goes from 0 to gridResolution
            float normalizedX = (pos.x - worldMin.x) / worldSize.x;  // 0 to 1
            float normalizedY = (pos.y - worldMin.y) / worldSize.y;  // 0 to 1
            
            float gx = normalizedX * gridResolution;
            float gy = normalizedY * gridResolution;
            
            // Bilinear interpolation indices
            int x0 = Mathf.FloorToInt(gx);
            int y0 = Mathf.FloorToInt(gy);
            int x1 = x0 + 1;
            int y1 = y0 + 1;
            
            float fx = gx - x0;
            float fy = gy - y0;
            
            // Clamp to grid bounds
            x0 = Mathf.Clamp(x0, 0, gridResolution - 1);
            x1 = Mathf.Clamp(x1, 0, gridResolution - 1);
            y0 = Mathf.Clamp(y0, 0, gridResolution - 1);
            y1 = Mathf.Clamp(y1, 0, gridResolution - 1);
            
            // Bilinear weights
            float w00 = (1f - fx) * (1f - fy);
            float w10 = fx * (1f - fy);
            float w01 = (1f - fx) * fy;
            float w11 = fx * fy;
            
            // Splat velocity to four neighboring cells
            int idx00 = y0 * gridResolution + x0;
            int idx10 = y0 * gridResolution + x1;
            int idx01 = y1 * gridResolution + x0;
            int idx11 = y1 * gridResolution + x1;
            
            velocityAccumulator[idx00] += vel * w00;
            velocityAccumulator[idx10] += vel * w10;
            velocityAccumulator[idx01] += vel * w01;
            velocityAccumulator[idx11] += vel * w11;
            
            weightAccumulator[idx00] += w00;
            weightAccumulator[idx10] += w10;
            weightAccumulator[idx01] += w01;
            weightAccumulator[idx11] += w11;
        }
    }
    
    void UpdateTexture()
    {
        float dt = Time.deltaTime;
        float smoothFactor = 1f - Mathf.Exp(-temporalSmoothing * dt);
        
        float maxExpectedSpeed = flowSimulation.moveSpeed * 2f;
        
        for (int i = 0; i < velocityPixels.Length; i++)
        {
            Vector2 newVel = Vector2.zero;
            
            if (weightAccumulator[i] > 0.001f)
            {
                newVel = velocityAccumulator[i] / weightAccumulator[i];
            }
            
            // Get current velocity from pixel (convert from 0-1 back to -1 to 1 range)
            Vector2 currentVel = new Vector2(
                (velocityPixels[i].r - 0.5f) * 2f * maxExpectedSpeed,
                (velocityPixels[i].g - 0.5f) * 2f * maxExpectedSpeed
            );
            
            // Temporal smoothing
            Vector2 smoothedVel = Vector2.Lerp(currentVel, newVel, smoothFactor);
            
            // Store velocity magnitude in blue channel for shader use
            float magnitude = smoothedVel.magnitude;
            
            // Convert velocity to 0-1 range for texture storage
            Vector2 normalizedVel = smoothedVel / maxExpectedSpeed;
            
            // Has data if there's any velocity or weight
            float hasData = (weightAccumulator[i] > 0.001f || magnitude > 0.01f) ? 1f : 0.5f;
            
            velocityPixels[i] = new Color(
                Mathf.Clamp01(normalizedVel.x * 0.5f + 0.5f),  // R: velocity X
                Mathf.Clamp01(normalizedVel.y * 0.5f + 0.5f),  // G: velocity Y
                Mathf.Clamp01(magnitude / maxExpectedSpeed),    // B: magnitude
                hasData                                          // A: data presence
            );
        }
        
        velocityTexture.SetPixels(velocityPixels);
        velocityTexture.Apply();
    }
    
    void UpdateShaderProperties()
    {
        if (flowMaterial == null) return;
        
        flowMaterial.SetFloat(VelocityScaleProperty, velocityScale);
        flowMaterial.SetVector(SaturationRangeProperty, new Vector4(saturationMin, saturationMax, 0f, 0f));
        flowMaterial.SetVector(ValueRangeProperty, new Vector4(valueMin, valueMax, 0f, 0f));
        flowMaterial.SetFloat(HueOffsetProperty, hueOffset / 360f);
    }
    
    void OnDestroy()
    {
        if (velocityTexture != null)
        {
            Destroy(velocityTexture);
        }
        if (flowQuadGO != null)
        {
            Destroy(flowQuadGO);
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 170, 350, 180));
        GUILayout.Box("Flow Visualizer Debug");
        GUILayout.Label($"Initialized: {isInitialized}");
        GUILayout.Label($"Grid: {gridResolution}x{gridResolution}");
        GUILayout.Label($"Cell Size: ({cellSizeX:F3}, {cellSizeY:F3})");
        GUILayout.Label($"World Bounds: ({worldMin.x:F1},{worldMin.y:F1}) to ({worldMax.x:F1},{worldMax.y:F1})");
        GUILayout.Label($"Material: {(flowMaterial != null ? flowMaterial.shader.name : "NULL")}");
        GUILayout.Label($"Quad: {(flowQuadGO != null ? "Created" : "NULL")}");
        
        if (flowQuadGO != null)
        {
            GUILayout.Label($"Quad Pos: {flowQuadGO.transform.position}");
            GUILayout.Label($"Quad Scale: {flowQuadGO.transform.localScale}");
        }
        GUILayout.EndArea();
    }
    
    /// <summary>
    /// Returns the average velocity in a world-space region (for gameplay use)
    /// </summary>
    public Vector2 GetAverageVelocityInRadius(Vector2 worldPos, float radius)
    {
        if (!isInitialized) return Vector2.zero;
        
        Vector2 accumVel = Vector2.zero;
        float accumWeight = 0f;
        
        int radiusCellsX = Mathf.CeilToInt(radius / cellSizeX);
        int radiusCellsY = Mathf.CeilToInt(radius / cellSizeY);
        
        float normalizedX = (worldPos.x - worldMin.x) / worldSize.x;
        float normalizedY = (worldPos.y - worldMin.y) / worldSize.y;
        
        int cx = Mathf.FloorToInt(normalizedX * gridResolution);
        int cy = Mathf.FloorToInt(normalizedY * gridResolution);
        
        float maxExpectedSpeed = flowSimulation.moveSpeed * 2f;
        
        int maxRadius = Mathf.Max(radiusCellsX, radiusCellsY);
        
        for (int dy = -maxRadius; dy <= maxRadius; dy++)
        {
            for (int dx = -maxRadius; dx <= maxRadius; dx++)
            {
                int gx = cx + dx;
                int gy = cy + dy;
                
                if (gx < 0 || gx >= gridResolution || gy < 0 || gy >= gridResolution)
                    continue;
                
                int idx = gy * gridResolution + gx;
                Color pixel = velocityPixels[idx];
                
                Vector2 vel = new Vector2(
                    (pixel.r - 0.5f) * 2f * maxExpectedSpeed,
                    (pixel.g - 0.5f) * 2f * maxExpectedSpeed
                );
                
                // Convert cell offset back to world distance
                float worldDistX = dx * cellSizeX;
                float worldDistY = dy * cellSizeY;
                float dist = Mathf.Sqrt(worldDistX * worldDistX + worldDistY * worldDistY);
                
                if (dist <= radius)
                {
                    float weight = 1f - (dist / radius);
                    accumVel += vel * weight;
                    accumWeight += weight;
                }
            }
        }
        
        if (accumWeight > 0.001f)
        {
            return accumVel / accumWeight;
        }
        return Vector2.zero;
    }
}