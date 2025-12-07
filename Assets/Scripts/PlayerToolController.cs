using UnityEngine;

/// <summary>
/// Handles player input for flow manipulation tools.
/// Click and drag to apply dampening effect that smooths local velocity.
/// Strength ramps up the longer the mouse is held.
/// Scroll wheel adjusts tool radius.
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
    public float scrollSensitivity = 2f;
    
    [Tooltip("Base dampening strength")]
    [Range(0.1f, 1f)]
    public float baseDampeningStrength = 0.3f;
    
    [Tooltip("Maximum dampening strength after full ramp-up")]
    [Range(0.5f, 1f)]
    public float maxDampeningStrength = 0.85f;
    
    [Tooltip("Time in seconds to reach maximum strength")]
    [Range(0.1f, 5f)]
    public float rampUpTime = 1.5f;
    
    [Header("Cursor Appearance")]
    public Color ringColor = new Color(1f, 1f, 1f, 0.7f);
    
    [Tooltip("Ring thickness as fraction of radius")]
    [Range(0.01f, 0.1f)]
    public float ringThicknessFraction = 0.03f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Runtime state
    private Vector2 currentWorldPos;
    private bool isApplying = false;
    private float holdDuration = 0f;
    private float currentStrength = 0f;
    private int agentsAffectedLastFrame = 0;
    
    // Cursor rendering - simple world-space ring
    private LineRenderer ringLine;
    private const int RING_SEGMENTS = 64;
    
    void Start()
    {
        ValidateReferences();
        CreateRingCursor();
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
        GameObject ringObj = new GameObject("ToolCursorRing");
        ringObj.transform.SetParent(transform);
        
        ringLine = ringObj.AddComponent<LineRenderer>();
        ringLine.useWorldSpace = true;
        ringLine.loop = true;
        ringLine.positionCount = RING_SEGMENTS;
        
        // Simple unlit material
        ringLine.material = new Material(Shader.Find("Sprites/Default"));
        ringLine.startColor = ringColor;
        ringLine.endColor = ringColor;
        
        // Thin line
        UpdateRingWidth();
        
        ringLine.sortingOrder = 100;
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
    
    void UpdateToolInput()
    {
        if (Input.GetMouseButton(0))
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
            
            float rampProgress = Mathf.Clamp01(holdDuration / rampUpTime);
            rampProgress = rampProgress * rampProgress * (3f - 2f * rampProgress);
            currentStrength = Mathf.Lerp(baseDampeningStrength, maxDampeningStrength, rampProgress);
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
        
        // Update ring color
        ringLine.startColor = ringColor;
        ringLine.endColor = ringColor;
        
        // Draw circle at cursor position
        for (int i = 0; i < RING_SEGMENTS; i++)
        {
            float angle = (float)i / RING_SEGMENTS * Mathf.PI * 2f;
            float x = currentWorldPos.x + Mathf.Cos(angle) * toolRadius;
            float y = currentWorldPos.y + Mathf.Sin(angle) * toolRadius;
            ringLine.SetPosition(i, new Vector3(x, y, -1f));
        }
    }
    
    void OnDestroy()
    {
        if (ringLine != null)
            Destroy(ringLine.gameObject);
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 360, 300, 180));
        GUILayout.Box("Tool Controller");
        GUILayout.Label($"Position: ({currentWorldPos.x:F1}, {currentWorldPos.y:F1})");
        GUILayout.Label($"Radius: {toolRadius:F1} [scroll to change]");
        GUILayout.Label($"Applying: {isApplying}");
        GUILayout.Label($"Strength: {currentStrength:F2}");
        GUILayout.Label($"Agents Affected: {agentsAffectedLastFrame}");
        GUILayout.EndArea();
    }
    
    public void SetToolRadius(float radius)
    {
        toolRadius = Mathf.Clamp(radius, minRadius, maxRadius);
        UpdateRingWidth();
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
}