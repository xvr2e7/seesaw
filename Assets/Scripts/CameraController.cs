using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    
    [Header("Viewport Settings")]
    [Range(0.1f, 0.5f)]
    public float viewportFraction = 0.18f;
    
    [Header("Movement")]
    [Range(0.5f, 10f)]
    public float followWeight = 1.5f;
    
    public float edgePadding = 2f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    [Header("References")]
    public Camera cam;
    
    private Vector2 targetPosition;
    private Vector2 currentPosition;
    private Vector2 worldSize;
    private float orthoSize;
    
    void Start()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        if (flowSimulation == null)
        {
            flowSimulation = FindObjectOfType<FlowSimulation>();
        }
        
        // Ensure orthographic
        cam.orthographic = true;
        
        // Initialize position at world center
        currentPosition = Vector2.zero;
        targetPosition = Vector2.zero;
    }
    
    void Update()
    {
        if (flowSimulation == null) return;
        
        // Get world size from simulation
        worldSize = flowSimulation.WorldSize;
        
        // Ensure worldSize is initialized and valid before proceeding
        if (worldSize.x <= 0.1f || worldSize.y <= 0.1f) return;
        
        // Calculate orthographic size based on viewport fraction
        // orthoSize is half the vertical view height
        // We want to see viewportFraction of the world width
        float visibleWidth = worldSize.x * viewportFraction;
        float aspectRatio = (float)Screen.width / Screen.height;
        
        // Ensure we don't divide by zero if screen dimensions are weird during init
        if (aspectRatio <= 0.001f) aspectRatio = 1f;

        orthoSize = (visibleWidth / aspectRatio) * 0.5f;
        
        // Clamp to a minimum value to prevent frustum errors
        orthoSize = Mathf.Max(orthoSize, 0.1f);
        
        cam.orthographicSize = orthoSize;
        
        // Update target position based on mouse
        UpdateTargetFromMouse();
        
        // Smoothly follow target with weighted response
        UpdateCameraPosition();
    }
    
    void UpdateTargetFromMouse()
    {
        // Get mouse position in viewport space (0-1)
        Vector2 mouseViewport = new Vector2(
            Input.mousePosition.x / Screen.width,
            Input.mousePosition.y / Screen.height
        );
        
        // Remap from viewport (0-1) to world position
        // When mouse is at screen center (0.5, 0.5), camera targets center of explorable area
        // When mouse is at screen edge, camera targets edge of explorable area
        
        // Calculate the area the camera can explore (world bounds minus what's visible minus padding)
        float visibleWidth = orthoSize * 2f * cam.aspect;
        float visibleHeight = orthoSize * 2f;
        
        float explorableWidth = worldSize.x - visibleWidth - (edgePadding * 2f);
        float explorableHeight = worldSize.y - visibleHeight - (edgePadding * 2f);
        
        // Clamp explorable area to be non-negative
        explorableWidth = Mathf.Max(0f, explorableWidth);
        explorableHeight = Mathf.Max(0f, explorableHeight);
        
        // Map mouse viewport position to target world position
        // Viewport 0 -> -explorable/2, Viewport 1 -> +explorable/2
        targetPosition = new Vector2(
            (mouseViewport.x - 0.5f) * explorableWidth,
            (mouseViewport.y - 0.5f) * explorableHeight
        );
    }
    
    void UpdateCameraPosition()
    {
        // Weighted smoothing
        float smoothing = followWeight * Time.deltaTime;
        currentPosition = Vector2.Lerp(currentPosition, targetPosition, smoothing);
        
        // Apply to camera transform (keep Z position for proper rendering)
        cam.transform.position = new Vector3(currentPosition.x, currentPosition.y, cam.transform.position.z);
    }
    
    public Rect GetVisibleBounds()
    {
        float visibleWidth = orthoSize * 2f * cam.aspect;
        float visibleHeight = orthoSize * 2f;
        
        return new Rect(
            currentPosition.x - visibleWidth * 0.5f,
            currentPosition.y - visibleHeight * 0.5f,
            visibleWidth,
            visibleHeight
        );
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label($"World Size: {worldSize.x:F0} x {worldSize.y:F0}");
        GUILayout.Label($"Viewport: {viewportFraction * 100:F0}% visible");
        GUILayout.Label($"Ortho Size: {orthoSize:F1}");
        GUILayout.Label($"Camera Pos: ({currentPosition.x:F1}, {currentPosition.y:F1})");
        
        Rect visible = GetVisibleBounds();
        GUILayout.Label($"Visible: {visible.width:F0} x {visible.height:F0}");
        GUILayout.EndArea();
    }
    
    void OnDrawGizmos()
    {
        if (flowSimulation == null) return;
        
        // Draw visible area
        Rect visible = GetVisibleBounds();
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireCube(
            new Vector3(visible.center.x, visible.center.y, 0f),
            new Vector3(visible.width, visible.height, 0.1f)
        );
    }
}