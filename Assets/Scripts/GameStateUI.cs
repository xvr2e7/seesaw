using UnityEngine;

/// <summary>
/// Clean, integrated UI for game state feedback.
/// </summary>
public class GameStateUI : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public PlayerToolController playerTool;
    public FlowSimulation flowSimulation;

    [Header("Font")]
    [Tooltip("Space Mono or other custom font for HUD labels")]
    public Font customFont;
    
    [Header("Layout")]
    [Tooltip("X position from right edge")]
    public float rightMargin = 20f;
    
    [Tooltip("Y position from top (below radar which is ~160px)")]
    public float topOffset = 170f;
    
    [Header("Divergence Display")]
    public float divergenceBarWidth = 420f;
    public float divergenceBarHeight = 8f;
    public float maxDisplayDivergence = 2f;
    
    [Header("Colors")]
    public Color textColor = new Color(0.50f, 0.52f, 0.56f, 0.85f);
    public Color barBackgroundColor = new Color(0.08f, 0.08f, 0.09f, 0.9f);
    public Color barFillColorLow = new Color(0.38f, 0.44f, 0.50f, 0.85f);
    public Color barFillColorHigh = new Color(0.55f, 0.40f, 0.38f, 0.85f);
    public Color panelBackgroundColor = new Color(0.02f, 0.02f, 0.025f, 0.82f);
    
    [Header("Final Score")]
    public float scoreTransitionDuration = 1.5f;
    
    [Header("Intro/Outro")]
    public float fadeInDuration = 1.5f;
    public float fadeOutDuration = 1f;

    [Header("Guidance Overlay")]
    [Tooltip("Phase A message, shown t=3-8s — before first event")]
    public string guidancePhaseA  = "colored disruptions will appear  ·  move your cursor over them";
    [Tooltip("Phase B message, shown t=8-18s — as first event starts")]
    public string guidancePhaseB  = "hold LEFT CLICK to suppress  ·  keep the field gray";
    [Tooltip("Phase C — tool reminder, shown t=55-65s")]
    public string guidancePhaseC1 = "1 SCAN   2 PULSE   3 LOCK  ·  scroll to resize";
    public int guidanceFontSize = 16;
    [Range(0f, 1f)] public float guidanceMaxAlpha = 0.55f;

    [Header("Revelation")]
    [Tooltip("First line shown centered over the ending fade")]
    public string revelationLine1 = "you have been managing crowd suppression";
    [Tooltip("Second line (dimmer). Leave empty for single-line.")]
    public string revelationLine2 = "every point of coherence was a dispersal";
    public int revelationFontSize = 18;
    [Range(0f, 1f)] public float revelationMaxAlpha = 0.80f;

    // Runtime state
    private float smoothedDivergence = 0f;
    private float displayedScore = 0f;
    private float scoreAnimationTime = 0f;
    private bool showingFinalScore = false;
    
    private GameManager.GameState currentGameState = GameManager.GameState.Initializing;
    private float stateTransitionAlpha = 0f;
    private float introFadeProgress = 0f;
    
    // Guidance / revelation runtime
    private GUIStyle guidanceStyle;
    private GUIStyle revelationStyle;
    private bool     revelationStyleInit = false;

    // Cached
    private Texture2D whiteTexture;
    private GUIStyle labelStyle;
    private GUIStyle scoreStyle;
    private GUIStyle scoreLabelStyle;
    private bool stylesInitialized = false;
    
    void Start()
    {
        FindReferences();
        CreateTextures();
    }
    
    void FindReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
        
        if (playerTool == null)
            playerTool = FindObjectOfType<PlayerToolController>();
        
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
    }
    
    void CreateTextures()
    {
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }
    
    private GUIStyle valueStyle;

    void InitStyles()
    {
        if (stylesInitialized) return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        if (customFont != null) labelStyle.font = customFont;
        labelStyle.normal.textColor = textColor;

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 58,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        if (customFont != null) valueStyle.font = customFont;
        valueStyle.normal.textColor = new Color(0.55f, 0.58f, 0.62f, 0.90f);

        scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 42,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        if (customFont != null) scoreStyle.font = customFont;
        scoreStyle.normal.textColor = new Color(0.75f, 0.75f, 0.78f, 1f);

        scoreLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        if (customFont != null) scoreLabelStyle.font = customFont;
        scoreLabelStyle.normal.textColor = new Color(0.40f, 0.42f, 0.45f, 0.9f);

        stylesInitialized = true;
    }
    
    void Update()
    {
        UpdateDivergence();
        UpdateStateTransitions();
        
        if (showingFinalScore)
        {
            UpdateScoreAnimation();
        }
    }
    
    void UpdateDivergence()
    {
        if (flowSimulation == null) return;
        
        float target = flowSimulation.CurrentDivergence;
        smoothedDivergence = Mathf.Lerp(smoothedDivergence, target, Time.deltaTime * 5f);
    }
    
    void UpdateStateTransitions()
    {
        switch (currentGameState)
        {
            case GameManager.GameState.Intro:
                introFadeProgress += Time.deltaTime / fadeInDuration;
                introFadeProgress = Mathf.Clamp01(introFadeProgress);
                stateTransitionAlpha = 1f - introFadeProgress;
                break;
                
            case GameManager.GameState.Playing:
                stateTransitionAlpha = Mathf.Lerp(stateTransitionAlpha, 0f, Time.deltaTime * 3f);
                break;
                
            case GameManager.GameState.Ending:
                stateTransitionAlpha += Time.deltaTime / fadeOutDuration;
                stateTransitionAlpha = Mathf.Clamp(stateTransitionAlpha, 0f, 0.85f);
                break;
                
            case GameManager.GameState.Complete:
                stateTransitionAlpha = 0.85f;
                break;
        }
    }
    
    void UpdateScoreAnimation()
    {
        if (gameManager == null) return;
        
        scoreAnimationTime += Time.deltaTime;
        float t = Mathf.Clamp01(scoreAnimationTime / scoreTransitionDuration);
        
        // Ease out cubic
        t = 1f - Mathf.Pow(1f - t, 3f);
        
        displayedScore = Mathf.Lerp(0f, gameManager.FinalScore, t);
    }
    
    public void OnGameStateChanged(GameManager.GameState newState)
    {
        currentGameState = newState;
        
        if (newState == GameManager.GameState.Intro)
        {
            introFadeProgress = 1f; 
            stateTransitionAlpha = 0f;
        }
        else if (newState == GameManager.GameState.Complete)
        {
            showingFinalScore = true;
            scoreAnimationTime = 0f;
            displayedScore = 0f;
        }
    }
    
    private bool isPaused = false;
    public void SetPaused(bool paused) { isPaused = paused; }

    void OnGUI()
    {
        if (isPaused) return;
        InitStyles();
        
        // Draw HUD during gameplay
        if (currentGameState == GameManager.GameState.Playing || 
            currentGameState == GameManager.GameState.Ending)
        {
            DrawHUD();
        }
        
        DrawGuidanceOverlay();

        // Draw fade overlay
        DrawFadeOverlay();

        DrawRevelationText();

        // Draw final score (now suppressed)
        if (showingFinalScore && currentGameState == GameManager.GameState.Complete)
        {
            DrawFinalScore();
        }
    }
    
    void DrawHUD()
    {
        float panelWidth  = divergenceBarWidth;
        float panelX      = 20f;
        float panelY      = 20f;

        float barWidth    = panelWidth - 16f;
        float padTop      = 14f;
        float rowLabel    = 26f;
        float rowValue    = 76f;
        float rowBar      = divergenceBarHeight;
        float padBottom   = 10f;
        float panelHeight = padTop + rowLabel + rowValue + rowBar + padBottom;

        // Panel background
        GUI.color = panelBackgroundColor;
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), whiteTexture);
        GUI.color = Color.white;

        // Border — all four sides
        GUI.color = new Color(0.22f, 0.24f, 0.27f, 0.65f);
        float bw = 1f;
        GUI.DrawTexture(new Rect(panelX,                   panelY,                    panelWidth, bw), whiteTexture);
        GUI.DrawTexture(new Rect(panelX,                   panelY + panelHeight - bw, panelWidth, bw), whiteTexture);
        GUI.DrawTexture(new Rect(panelX,                   panelY,                    bw, panelHeight), whiteTexture);
        GUI.DrawTexture(new Rect(panelX + panelWidth - bw, panelY,                    bw, panelHeight), whiteTexture);
        GUI.color = Color.white;

        float contentX = panelX + 8f;
        float currentY = panelY + padTop;

        // Row 1: label
        GUI.Label(new Rect(contentX, currentY, barWidth, rowLabel), "DIVERGENCE", labelStyle);
        currentY += rowLabel;

        // Row 2: large numeric value, color-coded
        float divergenceValue = smoothedDivergence;
        float fillRatio = Mathf.Clamp01(divergenceValue / maxDisplayDivergence);
        Color fillColor = Color.Lerp(barFillColorLow, barFillColorHigh, fillRatio);

        valueStyle.normal.textColor = Color.Lerp(
            new Color(0.55f, 0.58f, 0.62f, 0.90f),
            new Color(0.72f, 0.52f, 0.46f, 0.90f),
            fillRatio);

        GUI.Label(new Rect(contentX, currentY, barWidth, rowValue), $"{divergenceValue:F2}", valueStyle);
        currentY += rowValue;

        // Row 3: progress bar
        GUI.color = barBackgroundColor;
        GUI.DrawTexture(new Rect(contentX, currentY, barWidth, rowBar), whiteTexture);
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(contentX, currentY, barWidth * fillRatio, rowBar), whiteTexture);
        GUI.color = Color.white;
    }
    
    void DrawFadeOverlay()
    {
        if (whiteTexture == null || stateTransitionAlpha <= 0.001f) return;
        
        GUI.color = new Color(0f, 0f, 0f, stateTransitionAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
        GUI.color = Color.white;
    }
    
    void DrawFinalScore()
    {
        // Suppressed as per request (score shown in Documentary phase now)
        return;
    }
    
    void DrawGuidanceOverlay()
    {
        if (currentGameState != GameManager.GameState.Playing) return;
        if (gameManager == null) return;

        float  t       = gameManager.SessionTime;
        string message = null;
        float  alpha   = 0f;

        // A: t=3–8s  — before first event (t=10s), tell them what to look for
        if      (t >= 3f  && t < 8f)  { message = guidancePhaseA;  alpha = GuidanceAlpha(t,  3f,  4f,  7f,  8f); }
        // B: t=8–18s — first event is live at t=10s, tell them how to act
        else if (t >= 8f  && t < 18f) { message = guidancePhaseB;  alpha = GuidanceAlpha(t,  8f,  9f, 16f, 18f); }
        // C: t=55–65s — light tool reminder after the escalation begins
        else if (t >= 55f && t < 65f) { message = guidancePhaseC1; alpha = GuidanceAlpha(t, 55f, 57f, 63f, 65f); }

        if (message == null || alpha <= 0.005f) return;

        if (guidanceStyle == null)
        {
            guidanceStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = guidanceFontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            if (customFont != null) guidanceStyle.font = customFont;
        }

        Color col = new Color(0.70f, 0.72f, 0.76f, alpha * guidanceMaxAlpha);
        guidanceStyle.normal.textColor = col;
        guidanceStyle.hover.textColor  = col;

        float labelH = 28f;
        GUI.Label(new Rect(0f, Screen.height * 0.5f - labelH * 0.5f, Screen.width, labelH),
                  message, guidanceStyle);
    }

    float GuidanceAlpha(float t, float fadeStart, float holdStart, float holdEnd, float fadeEnd)
    {
        if (t < fadeStart || t >= fadeEnd) return 0f;
        if (t < holdStart) return Mathf.Clamp01((t - fadeStart) / (holdStart - fadeStart));
        if (t < holdEnd)   return 1f;
        return Mathf.Clamp01(1f - (t - holdEnd) / (fadeEnd - holdEnd));
    }

    void DrawRevelationText()
    {
        bool show = currentGameState == GameManager.GameState.Ending ||
                    currentGameState == GameManager.GameState.Complete;
        if (!show) return;

        float textAlpha = Mathf.Clamp01((stateTransitionAlpha - 0.45f) / 0.40f) * revelationMaxAlpha;
        if (textAlpha <= 0.005f) return;

        if (!revelationStyleInit)
        {
            revelationStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = revelationFontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = false
            };
            if (customFont != null) revelationStyle.font = customFont;
            revelationStyleInit = true;
        }

        float lineH  = revelationFontSize * 2.0f;
        bool  two    = !string.IsNullOrEmpty(revelationLine2);
        float totalH = two ? lineH * 2f + 12f : lineH;
        float startY = Screen.height * 0.5f - totalH * 0.5f;
        float padX   = 24f;
        float w      = Screen.width - padX * 2f;

        Color line1Col = new Color(0.68f, 0.70f, 0.74f, textAlpha);
        revelationStyle.normal.textColor = line1Col;
        revelationStyle.hover.textColor  = line1Col;
        GUI.Label(new Rect(padX, startY, w, lineH), revelationLine1, revelationStyle);

        if (two)
        {
            Color line2Col = new Color(0.50f, 0.52f, 0.55f, textAlpha * 0.75f);
            revelationStyle.normal.textColor = line2Col;
            revelationStyle.hover.textColor  = line2Col;
            GUI.Label(new Rect(padX, startY + lineH + 12f, w, lineH), revelationLine2, revelationStyle);
        }
    }

    void OnDestroy()
    {
        if (whiteTexture != null)
            Destroy(whiteTexture);
    }
}