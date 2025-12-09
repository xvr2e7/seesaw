using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Surveillance console interface - the entry point to Laminar Flow.
/// Displays a 2x2 grid of camera feeds:
/// - One active feed showing a preview of Laminar Flow (clickable)
/// - Three feeds showing static/noise (inactive)
/// 
/// Handles scene transitions and return from documentary phase.
/// </summary>
public class ConsoleController : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Spacing between panels and from screen edges")]
    public float margin = 40f;
    
    [Tooltip("Gap between camera feeds")]
    public float gap = 8f;
    
    [Tooltip("Aspect ratio for each camera panel (1.778 = 16:9)")]
    public float panelAspectRatio = 1.778f;
    
    [Header("Active Feed (Feed 0)")]
    [Tooltip("Which feed slot is active (0-3, top-left to bottom-right)")]
    public int activeFeedIndex = 0;
    
    [Tooltip("Preview texture/render texture for the active feed")]
    public Texture2D previewTexture;
    
    [Tooltip("Color tint when not hovered (dimmed)")]
    public Color dimmedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    
    [Tooltip("Color tint when hovered (brightened)")]
    public Color hoveredColor = new Color(1f, 1f, 1f, 1f);
    
    [Tooltip("Hover transition speed")]
    public float hoverTransitionSpeed = 5f;
    
    [Header("Static Noise")]
    [Tooltip("Speed of static animation")]
    public float staticSpeed = 15f;
    
    [Tooltip("Static grain scale")]
    public float staticScale = 200f;
    
    [Tooltip("Base color for static (greenish CRT feel)")]
    public Color staticBaseColor = new Color(0.1f, 0.12f, 0.1f, 1f);
    
    [Tooltip("Bright color for static noise")]
    public Color staticNoiseColor = new Color(0.2f, 0.25f, 0.2f, 1f);
    
    [Header("Console Frame")]
    [Tooltip("Background color of the console")]
    public Color consoleBackgroundColor = new Color(0.02f, 0.02f, 0.02f, 1f);
    
    [Tooltip("Frame/bezel color around feeds")]
    public Color frameColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    
    [Tooltip("Frame thickness")]
    public float frameThickness = 4f;
    
    [Header("Labels")]
    [Tooltip("Show camera labels (CAM 01, etc.)")]
    public bool showLabels = true;
    
    [Tooltip("Label color")]
    public Color labelColor = new Color(0.3f, 0.5f, 0.3f, 0.8f);
    
    [Header("Transition")]
    [Tooltip("Fade duration when entering Laminar Flow")]
    public float fadeOutDuration = 1.5f;
    
    [Tooltip("Fade duration when returning from documentary")]
    public float fadeInDuration = 2f;
    
    [Header("Scene")]
    [Tooltip("Name of the Laminar Flow scene to load")]
    public string laminarFlowSceneName = "Laminar Flow";
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // UI Elements
    private Canvas canvas;
    private RawImage[] feedPanels = new RawImage[4];
    private Image[] feedFrames = new Image[4];
    private Text[] feedLabels = new Text[4];
    private Image fadeOverlay;
    private RenderTexture[] staticTextures = new RenderTexture[4];
    
    // State
    private int hoveredFeed = -1;
    private float[] feedBrightness = new float[4];
    private bool isTransitioning = false;
    private bool isReturning = false;
    private bool isBooting = true;
    private float bootTimer = 0f;
    private float bootDuration = 2f;
    
    // Preview generator
    private PreviewTextureGenerator previewGenerator;
    
    // Cached
    private Texture2D whiteTexture;
    private Material staticMaterial;
    
    // Singleton for cross-scene communication
    private static ConsoleController instance;
    public static ConsoleController Instance => instance;
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        CreateTextures();
        CreatePreviewIfNeeded();
        CreateUI();
        CreateStaticMaterial();
        
        // Initialize brightness
        for (int i = 0; i < 4; i++)
        {
            feedBrightness[i] = (i == activeFeedIndex) ? 0f : 0f;
        }
        
        // Check if returning from documentary
        if (isReturning)
        {
            isBooting = false;
            StartCoroutine(FadeIn());
        }
        else
        {
            // Normal boot sequence
            StartCoroutine(BootSequence());
        }
    }
    
    IEnumerator BootSequence()
    {
        isBooting = true;
        fadeOverlay.color = Color.black;
        
        // Hide all panels initially
        foreach (var panel in feedPanels)
        {
            if (panel != null) panel.color = new Color(0, 0, 0, 0);
        }
        foreach (var frame in feedFrames)
        {
            if (frame != null) frame.color = new Color(0, 0, 0, 0);
        }
        foreach (var label in feedLabels)
        {
            if (label != null) label.color = new Color(0, 0, 0, 0);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Fade in from black
        float fadeDur = 1f;
        float elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDur;
            fadeOverlay.color = new Color(0, 0, 0, 1f - t);
            yield return null;
        }
        fadeOverlay.color = new Color(0, 0, 0, 0);
        
        // Boot panels one by one
        for (int i = 0; i < 4; i++)
        {
            if (feedFrames[i] != null)
            {
                // Fade in frame
                float frameFade = 0.3f;
                elapsed = 0f;
                while (elapsed < frameFade)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / frameFade;
                    feedFrames[i].color = Color.Lerp(new Color(0, 0, 0, 0), frameColor, t);
                    yield return null;
                }
                feedFrames[i].color = frameColor;
            }
            
            if (feedPanels[i] != null)
            {
                // Flash then show panel
                feedPanels[i].color = Color.white;
                yield return new WaitForSeconds(0.05f);
                
                Color targetColor = (i == activeFeedIndex) ? dimmedColor : Color.white;
                feedPanels[i].color = targetColor;
            }
            
            if (feedLabels[i] != null)
            {
                feedLabels[i].color = labelColor;
            }
            
            yield return new WaitForSeconds(0.15f);
        }
        
        isBooting = false;
        bootTimer = 0f;
    }
    
    void CreatePreviewIfNeeded()
    {
        // If no preview texture assigned, generate one
        if (previewTexture == null)
        {
            GameObject genObj = new GameObject("PreviewGenerator");
            genObj.transform.SetParent(transform);
            previewGenerator = genObj.AddComponent<PreviewTextureGenerator>();
            previewGenerator.width = 480;
            previewGenerator.height = 270;
            previewGenerator.animate = true;
            previewGenerator.animationSpeed = 0.3f;
            previewGenerator.GenerateTexture();
            
            // Use generated texture
            previewTexture = previewGenerator.generatedTexture;
        }
    }
    
    void OnDestroy()
    {
        // Cleanup
        foreach (var rt in staticTextures)
        {
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
            }
        }
        
        if (whiteTexture != null) Destroy(whiteTexture);
        if (staticMaterial != null) Destroy(staticMaterial);
    }
    
    void CreateTextures()
    {
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        
        // Create static render textures
        for (int i = 0; i < 4; i++)
        {
            if (i != activeFeedIndex)
            {
                staticTextures[i] = new RenderTexture(256, 144, 0);
                staticTextures[i].name = $"StaticRT_{i}";
                staticTextures[i].Create();
            }
        }
    }
    
    void CreateStaticMaterial()
    {
        // Create a simple static noise material
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        
        staticMaterial = new Material(shader);
    }
    
    void CreateUI()
    {
        // Create canvas
        GameObject canvasObj = new GameObject("ConsoleCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvas.transform);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = consoleBackgroundColor;
        bg.raycastTarget = false;
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Create feed panels (2x2 grid)
        for (int i = 0; i < 4; i++)
        {
            CreateFeedPanel(i);
        }
        
        // Console title
        CreateConsoleTitle();
        
        // Fade overlay (on top)
        GameObject fadeObj = new GameObject("FadeOverlay");
        fadeObj.transform.SetParent(canvas.transform);
        fadeOverlay = fadeObj.AddComponent<Image>();
        fadeOverlay.color = new Color(0, 0, 0, 0);
        fadeOverlay.raycastTarget = false;
        RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
    }
    
    void CreateFeedPanel(int index)
    {
        // Calculate position (2x2 grid)
        int row = index / 2;
        int col = index % 2;
        
        // Frame container
        GameObject frameObj = new GameObject($"FeedFrame_{index}");
        frameObj.transform.SetParent(canvas.transform);
        feedFrames[index] = frameObj.AddComponent<Image>();
        feedFrames[index].color = frameColor;
        feedFrames[index].raycastTarget = false;
        
        // Feed panel (the actual image)
        GameObject panelObj = new GameObject($"FeedPanel_{index}");
        panelObj.transform.SetParent(frameObj.transform);
        feedPanels[index] = panelObj.AddComponent<RawImage>();
        feedPanels[index].raycastTarget = (index == activeFeedIndex);
        
        // Set initial texture
        if (index == activeFeedIndex)
        {
            // Will be set later once preview is ready
            feedPanels[index].color = dimmedColor;
        }
        else
        {
            feedPanels[index].texture = staticTextures[index];
            feedPanels[index].color = Color.white;
        }
        
        // Label
        if (showLabels)
        {
            GameObject labelObj = new GameObject($"FeedLabel_{index}");
            labelObj.transform.SetParent(frameObj.transform);
            feedLabels[index] = labelObj.AddComponent<Text>();
            feedLabels[index].font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            feedLabels[index].fontSize = 14;
            feedLabels[index].color = labelColor;
            feedLabels[index].alignment = TextAnchor.UpperLeft;
            
            // Camera numbering
            string status = (index == activeFeedIndex) ? "● REC" : "○ NO SIGNAL";
            feedLabels[index].text = $"CAM {index + 1:D2}  {status}";
        }
    }
    
    void CreateConsoleTitle()
    {
        GameObject titleObj = new GameObject("ConsoleTitle");
        titleObj.transform.SetParent(canvas.transform);
        
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 16;
        titleText.color = new Color(0.3f, 0.4f, 0.3f, 0.7f);
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.text = "SURVEILLANCE NETWORK v2.4.1    [RESTRICTED ACCESS]";
        
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0, 1);
        titleRect.anchoredPosition = new Vector2(margin, -10);
        titleRect.sizeDelta = new Vector2(-margin * 2, 30);
    }
    
    void Update()
    {
        if (isTransitioning || isBooting) return;
        
        // Ensure preview texture is assigned
        if (feedPanels[activeFeedIndex] != null && feedPanels[activeFeedIndex].texture == null)
        {
            if (previewGenerator != null && previewGenerator.generatedTexture != null)
            {
                feedPanels[activeFeedIndex].texture = previewGenerator.generatedTexture;
            }
            else if (previewTexture != null)
            {
                feedPanels[activeFeedIndex].texture = previewTexture;
            }
        }
        
        UpdateLayout();
        UpdateStaticNoise();
        UpdateHoverDetection();
        UpdateFeedBrightness();
        UpdateClickDetection();
        UpdateTimestamp();
    }
    
    private Text timestampText;
    
    void UpdateTimestamp()
    {
        // Update camera labels with fake timestamp
        if (feedLabels[activeFeedIndex] != null)
        {
            System.DateTime now = System.DateTime.Now;
            string timestamp = now.ToString("HH:mm:ss");
            string status = "● REC";
            feedLabels[activeFeedIndex].text = $"CAM {activeFeedIndex + 1:D2}  {status}  {timestamp}";
        }
    }
    
    void UpdateLayout()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        
        // Calculate available space for the 2x2 grid
        float availableWidth = sw - (margin * 2) - gap;
        float availableHeight = sh - (margin * 2) - gap - 40; // Extra space for title
        
        // Each panel gets half the available space (minus gap)
        float panelWidth = (availableWidth - gap) / 2f;
        float panelHeight = panelWidth / panelAspectRatio;
        
        // If too tall, scale down
        float maxPanelHeight = (availableHeight - gap) / 2f;
        if (panelHeight > maxPanelHeight)
        {
            panelHeight = maxPanelHeight;
            panelWidth = panelHeight * panelAspectRatio;
        }
        
        // Calculate grid starting position (centered)
        float totalWidth = (panelWidth * 2) + gap;
        float totalHeight = (panelHeight * 2) + gap;
        float startX = (sw - totalWidth) / 2f;
        float startY = (sh - totalHeight) / 2f + 20; // Slight offset for title
        
        // Position each panel
        for (int i = 0; i < 4; i++)
        {
            int row = i / 2;
            int col = i % 2;
            
            float x = startX + col * (panelWidth + gap);
            float y = startY + (1 - row) * (panelHeight + gap); // Flip row for screen coords
            
            // Frame
            RectTransform frameRect = feedFrames[i].GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.zero;
            frameRect.pivot = Vector2.zero;
            frameRect.anchoredPosition = new Vector2(x - frameThickness, y - frameThickness);
            frameRect.sizeDelta = new Vector2(panelWidth + frameThickness * 2, panelHeight + frameThickness * 2);
            
            // Panel (inside frame)
            RectTransform panelRect = feedPanels[i].GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(frameThickness, frameThickness);
            panelRect.offsetMax = new Vector2(-frameThickness, -frameThickness);
            
            // Label
            if (feedLabels[i] != null)
            {
                RectTransform labelRect = feedLabels[i].GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0, 1);
                labelRect.anchorMax = new Vector2(1, 1);
                labelRect.pivot = new Vector2(0, 1);
                labelRect.anchoredPosition = new Vector2(frameThickness + 8, -frameThickness - 8);
                labelRect.sizeDelta = new Vector2(-16, 24);
            }
        }
    }
    
    void UpdateStaticNoise()
    {
        // Generate static noise for inactive feeds
        for (int i = 0; i < 4; i++)
        {
            if (i == activeFeedIndex) continue;
            if (staticTextures[i] == null) continue;
            
            // Render static noise to texture
            RenderTexture.active = staticTextures[i];
            GL.Clear(true, true, staticBaseColor);
            
            // Draw noise pattern
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, staticTextures[i].width, staticTextures[i].height, 0);
            
            // Simple noise pattern
            float time = Time.time * staticSpeed;
            System.Random rng = new System.Random((int)(time * 1000) + i * 12345);
            
            for (int y = 0; y < staticTextures[i].height; y += 2)
            {
                for (int x = 0; x < staticTextures[i].width; x += 2)
                {
                    float noise = (float)rng.NextDouble();
                    
                    // Add some horizontal banding
                    float band = Mathf.Sin(y * 0.1f + time * 0.5f) * 0.1f;
                    noise += band;
                    
                    if (noise > 0.7f)
                    {
                        Color c = Color.Lerp(staticBaseColor, staticNoiseColor, (noise - 0.7f) * 3f);
                        DrawRect(x, y, 2, 2, c);
                    }
                }
            }
            
            // Occasional scan line
            int scanY = (int)((time * 50) % staticTextures[i].height);
            DrawRect(0, scanY, staticTextures[i].width, 2, staticNoiseColor * 1.5f);
            
            GL.PopMatrix();
            RenderTexture.active = null;
        }
    }
    
    void DrawRect(float x, float y, float w, float h, Color color)
    {
        GL.Begin(GL.QUADS);
        GL.Color(color);
        GL.Vertex3(x, y, 0);
        GL.Vertex3(x + w, y, 0);
        GL.Vertex3(x + w, y + h, 0);
        GL.Vertex3(x, y + h, 0);
        GL.End();
    }
    
    void UpdateHoverDetection()
    {
        hoveredFeed = -1;
        
        if (feedPanels[activeFeedIndex] == null) return;
        
        Vector2 mousePos = Input.mousePosition;
        RectTransform rect = feedPanels[activeFeedIndex].GetComponent<RectTransform>();
        
        if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos))
        {
            hoveredFeed = activeFeedIndex;
        }
    }
    
    void UpdateFeedBrightness()
    {
        float dt = Time.deltaTime * hoverTransitionSpeed;
        
        for (int i = 0; i < 4; i++)
        {
            if (i == activeFeedIndex)
            {
                float target = (hoveredFeed == i) ? 1f : 0f;
                feedBrightness[i] = Mathf.Lerp(feedBrightness[i], target, dt);
                
                Color c = Color.Lerp(dimmedColor, hoveredColor, feedBrightness[i]);
                feedPanels[i].color = c;
                
                // Update cursor
                if (hoveredFeed == i)
                {
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                }
            }
        }
    }
    
    void UpdateClickDetection()
    {
        if (Input.GetMouseButtonDown(0) && hoveredFeed == activeFeedIndex)
        {
            StartCoroutine(TransitionToLaminarFlow());
        }
    }
    
    IEnumerator TransitionToLaminarFlow()
    {
        isTransitioning = true;
        
        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            fadeOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeOverlay.color = Color.black;
        
        // Hide console UI
        canvas.gameObject.SetActive(false);
        
        // Load Laminar Flow scene additively
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(laminarFlowSceneName, LoadSceneMode.Single);
        
        while (!loadOp.isDone)
        {
            yield return null;
        }
        
        isTransitioning = false;
    }
    
    /// <summary>
    /// Called when documentary phase ends to return to console
    /// </summary>
    public void ReturnFromDocumentary()
    {
        StartCoroutine(TransitionBackToConsole());
    }
    
    IEnumerator TransitionBackToConsole()
    {
        isTransitioning = true;
        isReturning = true;
        
        // Ensure we have a fade overlay
        if (fadeOverlay != null)
        {
            fadeOverlay.color = Color.black;
        }
        
        // Load console scene
        AsyncOperation loadOp = SceneManager.LoadSceneAsync("Console", LoadSceneMode.Single);
        
        while (!loadOp.isDone)
        {
            yield return null;
        }
        
        isTransitioning = false;
    }
    
    IEnumerator FadeIn()
    {
        if (fadeOverlay == null) yield break;
        
        fadeOverlay.color = Color.black;
        
        yield return new WaitForSeconds(0.5f);
        
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / fadeInDuration);
            fadeOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeOverlay.color = new Color(0, 0, 0, 0);
        
        isReturning = false;
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 250, 100));
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, 250, 100), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        GUILayout.Label("=== CONSOLE ===");
        GUILayout.Label($"Hovered: {hoveredFeed}");
        GUILayout.Label($"Active Feed Brightness: {feedBrightness[activeFeedIndex]:F2}");
        GUILayout.Label($"Transitioning: {isTransitioning}");
        GUILayout.EndArea();
    }
}
