using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
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
    [Tooltip("Margin from screen edges")]
    public float margin = 40f;
    
    [Tooltip("Gap between camera feeds")]
    public float gap = 16f;
    
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
    private Image fadeOverlay;
    private Texture2D[] staticTextures = new Texture2D[4];
    
    // State
    private int hoveredFeed = -1;
    private float[] feedBrightness = new float[4];
    private bool isTransitioning = false;
    private bool isReturning = false;
    private bool isBooting = true;
    
    // Preview generator
    private PreviewTextureGenerator previewGenerator;
    
    // Singleton for cross-scene communication
    private static ConsoleController instance;
    public static ConsoleController Instance => instance;
    
    // Flag to indicate we're returning from documentary
    private static bool returningFromDocumentary = false;
    
    public static void SetReturningFromDocumentary()
    {
        returningFromDocumentary = true;
    }
    
    void Awake()
    {
        // Don't use singleton pattern anymore - allow fresh instances
        instance = this;
    }
    
    void Start()
    {
        CreateStaticTextures();
        CreatePreviewIfNeeded();
        CreateUI();
        
        // Initialize brightness
        for (int i = 0; i < 4; i++)
        {
            feedBrightness[i] = (i == activeFeedIndex) ? 0f : 0f;
        }
        
        // Check if returning from documentary
        if (returningFromDocumentary)
        {
            returningFromDocumentary = false;
            isBooting = false;
            isReturning = true;
            StartCoroutine(FadeIn());
        }
        else
        {
            // Normal boot sequence
            StartCoroutine(BootSequence());
        }
    }
    
    void CreateStaticTextures()
    {
        // Create static noise textures for inactive feeds (CPU-based, no GL commands)
        for (int i = 0; i < 4; i++)
        {
            if (i != activeFeedIndex)
            {
                staticTextures[i] = new Texture2D(128, 72, TextureFormat.RGB24, false);
                staticTextures[i].filterMode = FilterMode.Point;
                staticTextures[i].wrapMode = TextureWrapMode.Clamp;
                staticTextures[i].name = $"StaticNoise_{i}";
                UpdateStaticTexture(i);
            }
        }
    }
    
    void CreatePreviewIfNeeded()
    {
        // Check if we have a preview texture
        if (previewTexture == null)
        {
            // Create preview generator
            GameObject genObj = new GameObject("PreviewGenerator");
            genObj.transform.SetParent(transform);
            previewGenerator = genObj.AddComponent<PreviewTextureGenerator>();
        }
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
        scaler.matchWidthOrHeight = 0.5f; // Balance between width and height matching
        
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
        
        // Force initial layout update
        Canvas.ForceUpdateCanvases();
        UpdateLayout();
    }
    
    void CreateFeedPanel(int index)
    {
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
    }
    
    void Update()
    {
        // Check for Escape key to quit the game
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
            return;
        }

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
    }

    void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    void UpdateLayout()
    {
        // Get the canvas RectTransform for proper sizing (accounts for CanvasScaler)
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float sw = canvasRect.rect.width;
        float sh = canvasRect.rect.height;
        
        // Fallback to screen size if canvas rect is not ready
        if (sw <= 0 || sh <= 0)
        {
            sw = Screen.width;
            sh = Screen.height;
        }
        
        // Calculate available space for the 2x2 grid (with margins on all sides)
        float availableWidth = sw - (margin * 2f);
        float availableHeight = sh - (margin * 2f);
        
        // Calculate panel dimensions to fill the available space while maintaining 16:9 aspect ratio
        // We have 2 columns and 2 rows with a gap between them
        // Total grid width = 2 * panelWidth + gap
        // Total grid height = 2 * panelHeight + gap
        
        // Try fitting by width first
        float panelWidth = (availableWidth - gap) / 2f;
        float panelHeight = panelWidth / panelAspectRatio;
        float totalGridHeight = (panelHeight * 2f) + gap;
        
        // If too tall, fit by height instead
        if (totalGridHeight > availableHeight)
        {
            panelHeight = (availableHeight - gap) / 2f;
            panelWidth = panelHeight * panelAspectRatio;
        }
        
        // Calculate grid starting position (centered in available space)
        float totalGridWidth = (panelWidth * 2f) + gap;
        totalGridHeight = (panelHeight * 2f) + gap;
        float startX = (sw - totalGridWidth) / 2f;
        float startY = (sh - totalGridHeight) / 2f;
        
        // Position each panel
        for (int i = 0; i < 4; i++)
        {
            int row = i / 2;    // 0 for top row (indices 0,1), 1 for bottom row (indices 2,3)
            int col = i % 2;    // 0 for left column, 1 for right column
            
            float x = startX + col * (panelWidth + gap);
            // Flip row so index 0,1 are at top (higher y in screen coords)
            float y = startY + (1 - row) * (panelHeight + gap);
            
            // Frame (includes frame thickness)
            RectTransform frameRect = feedFrames[i].GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.zero;
            frameRect.pivot = Vector2.zero;
            frameRect.anchoredPosition = new Vector2(x - frameThickness, y - frameThickness);
            frameRect.sizeDelta = new Vector2(panelWidth + frameThickness * 2f, panelHeight + frameThickness * 2f);
            
            // Panel (inside frame, filling it minus the frame thickness)
            RectTransform panelRect = feedPanels[i].GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(frameThickness, frameThickness);
            panelRect.offsetMax = new Vector2(-frameThickness, -frameThickness);
        }
    }
    
    void UpdateStaticNoise()
    {
        // Update static noise textures for inactive feeds (CPU-based, no GL)
        for (int i = 0; i < 4; i++)
        {
            if (i == activeFeedIndex) continue;
            if (staticTextures[i] == null) continue;
            
            // Only update every few frames to save performance
            if (Time.frameCount % 2 == i % 2)
            {
                UpdateStaticTexture(i);
            }
        }
    }
    
    void UpdateStaticTexture(int index)
    {
        Texture2D tex = staticTextures[index];
        if (tex == null) return;
        
        Color[] pixels = tex.GetPixels();
        int seed = (int)(Time.time * staticSpeed * 1000) + index * 12345;
        System.Random rng = new System.Random(seed);
        
        for (int i = 0; i < pixels.Length; i++)
        {
            float noise = (float)rng.NextDouble();
            pixels[i] = Color.Lerp(staticBaseColor, staticNoiseColor, noise * 0.5f);
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
    }
    
    void UpdateHoverDetection()
    {
        hoveredFeed = -1;
        
        // Check if mouse is over active feed
        if (feedPanels[activeFeedIndex] != null)
        {
            RectTransform rect = feedPanels[activeFeedIndex].GetComponent<RectTransform>();
            Vector2 localPoint;
            
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, Input.mousePosition, null, out localPoint))
            {
                if (rect.rect.Contains(localPoint))
                {
                    hoveredFeed = activeFeedIndex;
                }
            }
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
        
        // Fade out to black
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;
            fadeOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeOverlay.color = Color.black;
        
        // Wait a frame to ensure fade is rendered
        yield return null;
        
        // Load Laminar Flow scene - the fade overlay will persist until scene loads
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(laminarFlowSceneName, LoadSceneMode.Single);
        loadOp.allowSceneActivation = false;
        
        // Wait until scene is ready
        while (loadOp.progress < 0.9f)
        {
            yield return null;
        }
        
        // Activate the scene - this will destroy this object
        loadOp.allowSceneActivation = true;
        
        isTransitioning = false;
    }
    
    IEnumerator FadeIn()
    {
        if (fadeOverlay == null) yield break;
        
        fadeOverlay.color = Color.black;
        
        yield return new WaitForSeconds(0.5f);
        
        // Show panels instantly (no boot sequence when returning)
        for (int i = 0; i < 4; i++)
        {
            if (feedFrames[i] != null)
            {
                feedFrames[i].color = frameColor;
            }
            if (feedPanels[i] != null)
            {
                feedPanels[i].color = (i == activeFeedIndex) ? dimmedColor : Color.white;
            }
        }
        
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
            
            yield return new WaitForSeconds(0.15f);
        }
        
        isBooting = false;
    }
    
    void OnDestroy()
    {
        // Cleanup static textures
        for (int i = 0; i < 4; i++)
        {
            if (staticTextures[i] != null)
            {
                Destroy(staticTextures[i]);
            }
        }
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
