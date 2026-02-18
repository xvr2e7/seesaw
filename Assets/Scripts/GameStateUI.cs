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
    public float divergenceBarWidth = 140f;
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
    
    // Runtime state
    private float smoothedDivergence = 0f;
    private float displayedScore = 0f;
    private float scoreAnimationTime = 0f;
    private bool showingFinalScore = false;
    
    private GameManager.GameState currentGameState = GameManager.GameState.Initializing;
    private float stateTransitionAlpha = 0f;
    private float introFadeProgress = 0f;
    
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
    
    void InitStyles()
    {
        if (stylesInitialized) return;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        if (customFont != null) labelStyle.font = customFont;
        labelStyle.normal.textColor = textColor;

        scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        if (customFont != null) scoreStyle.font = customFont;
        scoreStyle.normal.textColor = new Color(0.75f, 0.75f, 0.78f, 1f);

        scoreLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
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
    
    void OnGUI()
    {
        InitStyles();
        
        // Draw HUD during gameplay
        if (currentGameState == GameManager.GameState.Playing || 
            currentGameState == GameManager.GameState.Ending)
        {
            DrawHUD();
        }
        
        // Draw fade overlay
        DrawFadeOverlay();
        
        // Draw final score (now suppressed)
        if (showingFinalScore && currentGameState == GameManager.GameState.Complete)
        {
            DrawFinalScore();
        }
    }
    
    void DrawHUD()
    {
        float sw = Screen.width;
        float panelX = sw - rightMargin - divergenceBarWidth - 16f;
        float panelY = topOffset;
        float panelWidth = divergenceBarWidth + 16f;
        float panelHeight = 36f;

        // Panel background — very dark, minimal
        GUI.color = panelBackgroundColor;
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), whiteTexture);
        GUI.color = Color.white;

        // Thin top border line for definition
        GUI.color = new Color(0.22f, 0.24f, 0.27f, 0.6f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, 1f), whiteTexture);
        GUI.color = Color.white;

        float contentX = panelX + 8f;
        float currentY = panelY + 7f;

        // Divergence label and value
        float divergenceValue = smoothedDivergence;
        string divergenceText = $"DIVERGENCE   {divergenceValue:F2}";
        GUI.Label(new Rect(contentX, currentY, divergenceBarWidth, 14f), divergenceText, labelStyle);

        currentY += 14f;

        // Divergence bar background
        GUI.color = barBackgroundColor;
        GUI.DrawTexture(new Rect(contentX, currentY, divergenceBarWidth, divergenceBarHeight), whiteTexture);

        // Divergence bar fill
        float fillRatio = Mathf.Clamp01(divergenceValue / maxDisplayDivergence);
        Color fillColor = Color.Lerp(barFillColorLow, barFillColorHigh, fillRatio);
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(contentX, currentY, divergenceBarWidth * fillRatio, divergenceBarHeight), whiteTexture);
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
    
    void OnDestroy()
    {
        if (whiteTexture != null)
            Destroy(whiteTexture);
    }
}