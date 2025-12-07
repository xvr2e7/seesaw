using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;
    
    [Header("Viewport Settings")]
    [Tooltip("Fraction of world width visible at once (aperture size)")]
    [Range(0.1f, 1.0f)]
    public float viewportFraction = 0.25f;
    
    [Header("Movement")]
    [Tooltip("How quickly camera follows mouse")]
    [Range(0.5f, 10f)]
    public float followSpeed = 3f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    [Header("References")]
    public Camera cam;
    
    private Vector2 targetPosition;
    private Vector2 currentPosition;
    private Vector2 worldSize;
    private Vector2 worldHalfSize;
    private float orthoSize;
    private float visibleWidth;
    private float visibleHeight;
    private bool isInitialized = false;
    
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
        
        worldHalfSize = worldSize * 0.5f;
        
        // Calculate orthographic size based on viewport fraction
        float aspectRatio = (float)Screen.width / Screen.height;
        if (aspectRatio <= 0.001f) aspectRatio = 1f;
        
        // viewportFraction determines how much of the world width we see
        visibleWidth = worldSize.x * viewportFraction;
        visibleHeight = visibleWidth / aspectRatio;
        
        // If visible height exceeds world height, adjust to fit
        if (visibleHeight > worldSize.y)
        {
            visibleHeight = worldSize.y;
            visibleWidth = visibleHeight * aspectRatio;
        }
        
        // orthoSize is half the vertical view height
        orthoSize = visibleHeight * 0.5f;
        orthoSize = Mathf.Max(orthoSize, 0.1f);
        
        cam.orthographicSize = orthoSize;
        
        isInitialized = true;
        
        // Update target position based on mouse
        UpdateTargetFromMouse();
        
        // Smoothly follow target
        UpdateCameraPosition();
        
        // Hard clamp to ensure we never exceed world bounds
        ClampCameraToWorldBounds();
    }
    
    void UpdateTargetFromMouse()
    {
        // Get mouse position in viewport space (0-1)
        Vector2 mouseViewport = new Vector2(
            Mathf.Clamp01(Input.mousePosition.x / Screen.width),
            Mathf.Clamp01(Input.mousePosition.y / Screen.height)
        );
        
        // Calculate the explorable range for camera center
        // Camera center can move from (minX + visibleWidth/2) to (maxX - visibleWidth/2)
        float explorableHalfWidth = worldHalfSize.x - (visibleWidth * 0.5f);
        float explorableHalfHeight = worldHalfSize.y - (visibleHeight * 0.5f);
        
        // Clamp to non-negative (if viewport is larger than world, camera stays centered)
        explorableHalfWidth = Mathf.Max(0f, explorableHalfWidth);
        explorableHalfHeight = Mathf.Max(0f, explorableHalfHeight);
        
        // Map mouse viewport position to target world position
        // Viewport center (0.5, 0.5) -> world center (0, 0)
        // Viewport edges -> explorable edges
        targetPosition = new Vector2(
            (mouseViewport.x - 0.5f) * 2f * explorableHalfWidth,
            (mouseViewport.y - 0.5f) * 2f * explorableHalfHeight
        );
    }
    
    void UpdateCameraPosition()
    {
        // Smooth follow
        float smoothing = followSpeed * Time.deltaTime;
        currentPosition = Vector2.Lerp(currentPosition, targetPosition, smoothing);
    }
    
    void ClampCameraToWorldBounds()
    {
        // Calculate the bounds for camera center position
        // The camera center must stay far enough from edges so the viewport doesn't exceed world bounds
        float minX = -worldHalfSize.x + (visibleWidth * 0.5f);
        float maxX = worldHalfSize.x - (visibleWidth * 0.5f);
        float minY = -worldHalfSize.y + (visibleHeight * 0.5f);
        float maxY = worldHalfSize.y - (visibleHeight * 0.5f);
        
        // Handle case where viewport is larger than world (center the camera)
        if (minX > maxX)
        {
            currentPosition.x = 0f;
        }
        else
        {
            currentPosition.x = Mathf.Clamp(currentPosition.x, minX, maxX);
        }
        
        if (minY > maxY)
        {
            currentPosition.y = 0f;
        }
        else
        {
            currentPosition.y = Mathf.Clamp(currentPosition.y, minY, maxY);
        }
        
        // Apply to camera transform (keep Z position for proper rendering)
        cam.transform.position = new Vector3(currentPosition.x, currentPosition.y, cam.transform.position.z);
    }
    
    /// <summary>
    /// Returns the currently visible world-space bounds
    /// </summary>
    public Rect GetVisibleBounds()
    {
        return new Rect(
            currentPosition.x - visibleWidth * 0.5f,
            currentPosition.y - visibleHeight * 0.5f,
            visibleWidth,
            visibleHeight
        );
    }
    
    /// <summary>
    /// Converts screen position to world position
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        if (cam == null) return Vector2.zero;
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        return new Vector2(worldPos.x, worldPos.y);
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 180));
        GUILayout.Box("Camera Debug");
        GUILayout.Label($"World Size: {worldSize.x:F0} x {worldSize.y:F0}");
        GUILayout.Label($"Viewport: {viewportFraction * 100:F0}% of world width");
        GUILayout.Label($"Visible: {visibleWidth:F1} x {visibleHeight:F1}");
        GUILayout.Label($"Ortho Size: {orthoSize:F2}");
        GUILayout.Label($"Camera Pos: ({currentPosition.x:F1}, {currentPosition.y:F1})");
        GUILayout.Label($"Target Pos: ({targetPosition.x:F1}, {targetPosition.y:F1})");
        
        Rect bounds = GetVisibleBounds();
        GUILayout.Label($"Bounds: ({bounds.xMin:F1},{bounds.yMin:F1}) to ({bounds.xMax:F1},{bounds.yMax:F1})");
        GUILayout.EndArea();
    }
    
    void OnDrawGizmos()
    {
        if (!isInitialized) return;
        
        // Draw visible area
        Rect visible = GetVisibleBounds();
        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        Gizmos.DrawWireCube(
            new Vector3(visible.center.x, visible.center.y, 0f),
            new Vector3(visible.width, visible.height, 0.1f)
        );
        
        // Draw world bounds
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(worldSize.x, worldSize.y, 0.1f));
    }
}