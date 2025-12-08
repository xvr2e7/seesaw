using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Displays visual cues for turbulence events:
/// - Edge arrows pointing to off-screen events
/// - On-screen pulsing indicators for visible events
/// - Mini-map radar showing event locations
/// </summary>
public class TurbulenceIndicatorUI : MonoBehaviour
{
    [Header("References")]
    public TurbulentEventScheduler eventScheduler;
    public CameraController cameraController;
    public FlowSimulation flowSimulation;
    public Camera mainCamera;
    
    [Header("Edge Indicators")]
    [Tooltip("Distance from screen edge for indicators")]
    public float edgeMargin = 50f;
    
    [Tooltip("Size of edge indicators")]
    public float indicatorSize = 40f;
    
    [Tooltip("Color for edge indicators (off-screen events)")]
    public Color offScreenColor = new Color(1f, 0.3f, 0.2f, 0.9f);
    
    [Tooltip("Color for on-screen event markers")]
    public Color onScreenColor = new Color(1f, 0.8f, 0.2f, 0.8f);
    
    [Tooltip("Pulse speed for indicators")]
    public float pulseSpeed = 3f;

    // Runtime UI elements
    private Canvas uiCanvas;
    private List<EdgeIndicator> edgeIndicators = new List<EdgeIndicator>();

    // Tracking
    private HashSet<string> knownEvents = new HashSet<string>();
    
    // Cached screen dimensions
    private float screenWidth;
    private float screenHeight;
    
    // GUI Styles (cached)
    private GUIStyle labelStyle;
    private bool stylesInitialized = false;
    
    void Start()
    {
        FindReferences();
        SetupCanvas();

        screenWidth = Screen.width;
        screenHeight = Screen.height;
    }
    
    void FindReferences()
    {
        if (eventScheduler == null)
            eventScheduler = FindObjectOfType<TurbulentEventScheduler>();
        
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();
        
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
        
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    
    void SetupCanvas()
    {
        // Create UI Canvas
        GameObject canvasObj = new GameObject("TurbulenceIndicatorCanvas");
        canvasObj.transform.SetParent(transform);
        
        uiCanvas = canvasObj.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 100;
        
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
    }
    
    void InitializeStyles()
    {
        if (stylesInitialized) return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12
        };

        stylesInitialized = true;
    }
    
    void Update()
    {
        if (eventScheduler == null || mainCamera == null) return;
        
        UpdateScreenDimensions();
        UpdateEdgeIndicators();
    }

    void UpdateScreenDimensions()
    {
        screenWidth = Screen.width;
        screenHeight = Screen.height;
    }

    void UpdateEdgeIndicators()
    {
        List<TurbulenceEvent> activeEvents = eventScheduler.GetActiveEvents();
        Rect visibleBounds = cameraController != null ? cameraController.GetVisibleBounds() : new Rect(-50, -30, 100, 60);
        
        // Check for new events and trigger alerts
        foreach (var evt in activeEvents)
        {
            if (evt.isActive && !knownEvents.Contains(evt.eventName))
            {
                knownEvents.Add(evt.eventName);
                OnNewEventStarted(evt);
            }
        }
        
        // Clean up known events that are no longer active
        knownEvents.RemoveWhere(name => !activeEvents.Exists(e => e.eventName == name));
        
        // Update or create indicators for each active event
        // First, mark all existing indicators as unused
        foreach (var indicator in edgeIndicators)
        {
            indicator.isUsed = false;
        }
        
        foreach (var evt in activeEvents)
        {
            if (!evt.isActive) continue;
            
            bool isVisible = visibleBounds.Contains(evt.position);
            
            // Find or create indicator for this event
            EdgeIndicator indicator = GetOrCreateIndicator(evt.eventName);
            indicator.isUsed = true;
            indicator.targetEvent = evt;
            indicator.isOnScreen = isVisible;
            
            if (!isVisible)
            {
                // Calculate edge position
                UpdateOffScreenIndicator(indicator, evt.position, visibleBounds);
            }
            
            // Update indicator visuals
            UpdateIndicatorVisuals(indicator);
        }
        
        // Hide unused indicators
        foreach (var indicator in edgeIndicators)
        {
            if (!indicator.isUsed && indicator.gameObject != null)
            {
                indicator.gameObject.SetActive(false);
            }
        }
    }
    
    void OnNewEventStarted(TurbulenceEvent evt)
    {
        Debug.Log($"[TurbulenceUI] New event detected: {evt.eventName} ({evt.pattern})");
    }
    
    EdgeIndicator GetOrCreateIndicator(string eventName)
    {
        // Find existing
        foreach (var indicator in edgeIndicators)
        {
            if (indicator.eventName == eventName)
                return indicator;
        }
        
        // Find unused
        foreach (var indicator in edgeIndicators)
        {
            if (!indicator.isUsed)
            {
                indicator.eventName = eventName;
                return indicator;
            }
        }
        
        // Create new
        EdgeIndicator newIndicator = CreateEdgeIndicator(eventName);
        edgeIndicators.Add(newIndicator);
        return newIndicator;
    }
    
    EdgeIndicator CreateEdgeIndicator(string eventName)
    {
        GameObject indicatorObj = new GameObject($"EdgeIndicator_{eventName}");
        indicatorObj.transform.SetParent(uiCanvas.transform);
        
        // Create arrow image
        Image arrowImage = indicatorObj.AddComponent<Image>();
        arrowImage.color = offScreenColor;
        
        // Create triangle sprite procedurally
        arrowImage.sprite = CreateTriangleSprite();
        
        RectTransform rect = indicatorObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(indicatorSize, indicatorSize);
        
        // Add distance text
        GameObject textObj = new GameObject("DistanceText");
        textObj.transform.SetParent(indicatorObj.transform);
        
        Text distText = textObj.AddComponent<Text>();
        distText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        distText.fontSize = 12;
        distText.color = offScreenColor;
        distText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchoredPosition = new Vector2(0f, -20f);
        textRect.sizeDelta = new Vector2(60f, 20f);
        
        EdgeIndicator indicator = new EdgeIndicator
        {
            eventName = eventName,
            gameObject = indicatorObj,
            image = arrowImage,
            distanceText = distText,
            rectTransform = rect
        };
        
        return indicator;
    }
    
    Sprite CreateTriangleSprite()
    {
        // Create a simple triangle texture
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Triangle pointing up
                float fx = (float)x / size - 0.5f;
                float fy = (float)y / size;
                
                bool inside = fy > Mathf.Abs(fx) * 2f && fy < 0.9f;
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    void UpdateOffScreenIndicator(EdgeIndicator indicator, Vector2 eventWorldPos, Rect visibleBounds)
    {
        // Calculate direction from camera center to event
        Vector2 cameraCenter = visibleBounds.center;
        Vector2 toEvent = eventWorldPos - cameraCenter;
        float distance = toEvent.magnitude;
        Vector2 direction = toEvent.normalized;
        
        // Convert to screen position at edge
        Vector2 screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
        
        // Find intersection with screen edge
        float angle = Mathf.Atan2(direction.y, direction.x);
        
        float halfWidth = screenWidth * 0.5f - edgeMargin;
        float halfHeight = screenHeight * 0.5f - edgeMargin;
        
        // Calculate edge position
        float edgeX, edgeY;
        
        float tanAngle = Mathf.Tan(angle);
        
        if (Mathf.Abs(direction.x) * halfHeight > Mathf.Abs(direction.y) * halfWidth)
        {
            // Hit left or right edge
            edgeX = Mathf.Sign(direction.x) * halfWidth;
            edgeY = edgeX * tanAngle;
            edgeY = Mathf.Clamp(edgeY, -halfHeight, halfHeight);
        }
        else
        {
            // Hit top or bottom edge
            edgeY = Mathf.Sign(direction.y) * halfHeight;
            edgeX = Mathf.Abs(tanAngle) > 0.001f ? edgeY / tanAngle : 0f;
            edgeX = Mathf.Clamp(edgeX, -halfWidth, halfWidth);
        }
        
        indicator.screenPosition = screenCenter + new Vector2(edgeX, edgeY);
        indicator.rotation = angle * Mathf.Rad2Deg - 90f; // Point arrow toward event
        indicator.distance = distance;
    }
    
    void UpdateIndicatorVisuals(EdgeIndicator indicator)
    {
        if (indicator.gameObject == null) return;
        
        if (indicator.isOnScreen)
        {
            // On-screen: show pulsing marker at world position
            indicator.gameObject.SetActive(true);
            
            Vector3 screenPos = mainCamera.WorldToScreenPoint(
                new Vector3(indicator.targetEvent.position.x, indicator.targetEvent.position.y, 0f)
            );
            
            indicator.rectTransform.position = screenPos;
            indicator.rectTransform.rotation = Quaternion.identity;
            
            // Pulse effect
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * pulseSpeed);
            float intensityPulse = indicator.targetEvent.currentIntensity * pulse;
            
            indicator.image.color = new Color(
                onScreenColor.r,
                onScreenColor.g,
                onScreenColor.b,
                onScreenColor.a * intensityPulse
            );
            
            // Scale by intensity
            float scale = 1f + indicator.targetEvent.currentIntensity * 0.5f;
            indicator.rectTransform.localScale = Vector3.one * scale * pulse;
            
            indicator.distanceText.text = "";
        }
        else
        {
            // Off-screen: show edge arrow
            indicator.gameObject.SetActive(true);
            
            indicator.rectTransform.position = indicator.screenPosition;
            indicator.rectTransform.rotation = Quaternion.Euler(0f, 0f, indicator.rotation);
            indicator.rectTransform.localScale = Vector3.one;
            
            // Pulse based on distance (closer = faster pulse)
            float urgency = Mathf.Clamp01(30f / indicator.distance);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * (1f + urgency * 2f));
            
            indicator.image.color = new Color(
                offScreenColor.r,
                offScreenColor.g,
                offScreenColor.b,
                offScreenColor.a * (0.5f + pulse * 0.5f) * indicator.targetEvent.currentIntensity
            );
            
            // Show distance
            indicator.distanceText.text = $"{indicator.distance:F0}";
            indicator.distanceText.color = offScreenColor;
        }
    }

    void OnGUI()
    {
        InitializeStyles();

        // Draw mini-map radar
        DrawEventRadar();
    }

    void DrawEventRadar()
    {
        if (eventScheduler == null || cameraController == null) return;
        
        // Mini radar in corner
        float radarSize = 140f;
        float radarX = screenWidth - radarSize - 20f;
        float radarY = 20f;
        
        // Background
        GUI.color = new Color(0.03f, 0.06f, 0.03f, 0.9f);
        GUI.DrawTexture(new Rect(radarX, radarY, radarSize, radarSize), Texture2D.whiteTexture);
        
        // Border
        GUI.color = new Color(0.3f, 0.5f, 0.3f, 0.8f);
        float borderWidth = 1f;
        GUI.DrawTexture(new Rect(radarX, radarY, radarSize, borderWidth), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(radarX, radarY + radarSize - borderWidth, radarSize, borderWidth), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(radarX, radarY, borderWidth, radarSize), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(radarX + radarSize - borderWidth, radarY, borderWidth, radarSize), Texture2D.whiteTexture);
        
        // Grid lines
        // GUI.color = new Color(0.2f, 0.3f, 0.2f, 0.3f);
        // GUI.DrawTexture(new Rect(radarX + radarSize * 0.5f, radarY + 18, 1, radarSize - 18), Texture2D.whiteTexture);
        // GUI.DrawTexture(new Rect(radarX, radarY + radarSize * 0.5f + 9, radarSize, 1), Texture2D.whiteTexture);
        
        Vector2 worldSize = flowSimulation.WorldSize;
        Vector2 radarCenter = new Vector2(radarX + radarSize * 0.5f, radarY + radarSize * 0.5f + 10f);
        float radarScale = (radarSize - 30f) / Mathf.Max(worldSize.x, worldSize.y);
        
        // Draw camera viewport
        Rect visibleBounds = cameraController.GetVisibleBounds();
        GUI.color = new Color(0.3f, 0.6f, 0.3f, 0.3f);
        
        float viewX = radarCenter.x + visibleBounds.center.x * radarScale - visibleBounds.width * radarScale * 0.5f;
        float viewY = radarCenter.y - visibleBounds.center.y * radarScale - visibleBounds.height * radarScale * 0.5f;
        float viewW = visibleBounds.width * radarScale;
        float viewH = visibleBounds.height * radarScale;
        
        GUI.DrawTexture(new Rect(viewX, viewY, viewW, viewH), Texture2D.whiteTexture);
        
        // Viewport border
        GUI.color = new Color(0.4f, 0.8f, 0.4f, 0.6f);
        GUI.DrawTexture(new Rect(viewX, viewY, viewW, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(viewX, viewY + viewH, viewW, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(viewX, viewY, 1, viewH), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(viewX + viewW, viewY, 1, viewH), Texture2D.whiteTexture);
        
        // Draw events as blips
        var activeEvents = eventScheduler.GetActiveEvents();
        foreach (var evt in activeEvents)
        {
            if (!evt.isActive) continue;
            
            float blipX = radarCenter.x + evt.position.x * radarScale;
            float blipY = radarCenter.y - evt.position.y * radarScale; // Flip Y
            
            float blipSize = 6f + evt.currentIntensity * 6f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f + evt.position.x * 0.1f);
            
            // Color based on whether it's in view
            bool inView = visibleBounds.Contains(evt.position);
            
            if (inView)
            {
                GUI.color = new Color(0.3f, 1f, 0.4f, pulse); // Green if in view
            }
            else
            {
                GUI.color = new Color(1f, 0.3f, 0.2f, 0.6f + pulse * 0.4f); // Red if off-screen - more visible
            }
            
            // Draw blip
            GUI.DrawTexture(new Rect(blipX - blipSize * 0.5f, blipY - blipSize * 0.5f, blipSize, blipSize), Texture2D.whiteTexture);
            
            // Draw pulsing ring for off-screen events
            if (!inView)
            {
                GUI.color = new Color(1f, 0.3f, 0.2f, (1f - pulse) * 0.5f);
                float ringSize = blipSize + pulse * 8f;
                // Simple ring approximation with 4 rectangles
                GUI.DrawTexture(new Rect(blipX - ringSize * 0.5f, blipY - 1, ringSize, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(blipX - 1, blipY - ringSize * 0.5f, 2, ringSize), Texture2D.whiteTexture);
            }
        }
        
        GUI.color = Color.white;
    }
    
    void OnDestroy()
    {
        // Cleanup created textures
        foreach (var indicator in edgeIndicators)
        {
            if (indicator.image != null && indicator.image.sprite != null)
            {
                if (indicator.image.sprite.texture != null)
                {
                    Destroy(indicator.image.sprite.texture);
                }
                Destroy(indicator.image.sprite);
            }
            if (indicator.gameObject != null)
            {
                Destroy(indicator.gameObject);
            }
        }
    }
    
    // Helper class for edge indicators
    private class EdgeIndicator
    {
        public string eventName;
        public GameObject gameObject;
        public Image image;
        public Text distanceText;
        public RectTransform rectTransform;
        public TurbulenceEvent targetEvent;
        
        public bool isUsed;
        public bool isOnScreen;
        public Vector2 screenPosition;
        public float rotation;
        public float distance;
    }
}