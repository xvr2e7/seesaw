using UnityEngine;

/// <summary>
/// Clean, integrated UI for game state feedback.
/// 
/// Elements:
/// - Divergence: Number + bar displayed below existing radar (top-right)
/// - Time: Simple timer below divergence
/// - Energy: Radial ring around cursor (handled in PlayerToolController)
/// - Final score: Clean centered display
/// 
/// Designed to integrate with existing TurbulenceIndicatorUI radar.
/// </summary>
public class GameStateUI : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public PlayerToolController playerTool;
    public FlowSimulation flowSimulation;
    
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
    public Color textColor = new Color(0.85f, 0.85f, 0.85f, 0.9f);
    public Color barBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    public Color barFillColorLow = new Color(0.3f, 0.7f, 0.4f, 0.9f);
    public Color barFillColorHigh = new Color(0.9f, 0.3f, 0.2f, 0.9f);
    public Color panelBackgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.75f);
    
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
    private GUIStyle timerStyle;
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
            fontSize = 11,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = textColor;
        
        timerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleRight
        };
        timerStyle.normal.textColor = textColor;
        
        scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        scoreStyle.normal.textColor = Color.white;
        
        scoreLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        scoreLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        
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
            introFadeProgress = 0f;
            stateTransitionAlpha = 1f;
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
        
        // Draw final score
        if (showingFinalScore && currentGameState == GameManager.GameState.Complete)
        {
            DrawFinalScore();
        }
    }
    
    void DrawHUD()
    {
        float sw = Screen.width;
        float panelX = sw - rightMargin - divergenceBarWidth - 20f;
        float panelY = topOffset;
        float panelWidth = divergenceBarWidth + 20f;
        float panelHeight = 60f;
        
        // Panel background
        GUI.color = panelBackgroundColor;
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), whiteTexture);
        GUI.color = Color.white;
        
        float contentX = panelX + 10f;
        float currentY = panelY + 8f;
        
        // Divergence label and value
        float divergenceValue = smoothedDivergence;
        string divergenceText = $"DIVERGENCE  {divergenceValue:F2}";
        GUI.Label(new Rect(contentX, currentY, divergenceBarWidth, 16f), divergenceText, labelStyle);
        
        currentY += 18f;
        
        // Divergence bar background
        GUI.color = barBackgroundColor;
        GUI.DrawTexture(new Rect(contentX, currentY, divergenceBarWidth, divergenceBarHeight), whiteTexture);
        
        // Divergence bar fill
        float fillRatio = Mathf.Clamp01(divergenceValue / maxDisplayDivergence);
        Color fillColor = Color.Lerp(barFillColorLow, barFillColorHigh, fillRatio);
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(contentX, currentY, divergenceBarWidth * fillRatio, divergenceBarHeight), whiteTexture);
        GUI.color = Color.white;
        
        currentY += divergenceBarHeight + 8f;
        
        // Time display
        if (gameManager != null)
        {
            float timeRemaining = gameManager.maxSessionDuration - gameManager.SessionTime;
            timeRemaining = Mathf.Max(0f, timeRemaining);
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            string timeText = $"{minutes:D2}:{seconds:D2}";
            
            GUI.Label(new Rect(contentX, currentY, divergenceBarWidth, 16f), timeText, timerStyle);
        }
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
        if (whiteTexture == null) return;
        
        float sw = Screen.width;
        float sh = Screen.height;
        
        float panelWidth = 300f;
        float panelHeight = 150f;
        float panelX = (sw - panelWidth) * 0.5f;
        float panelY = (sh - panelHeight) * 0.5f - 30f;
        
        // Panel background
        GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), whiteTexture);
        
        // Border
        GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        float borderWidth = 1f;
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, borderWidth), whiteTexture);
        GUI.DrawTexture(new Rect(panelX, panelY + panelHeight - borderWidth, panelWidth, borderWidth), whiteTexture);
        GUI.DrawTexture(new Rect(panelX, panelY, borderWidth, panelHeight), whiteTexture);
        GUI.DrawTexture(new Rect(panelX + panelWidth - borderWidth, panelY, borderWidth, panelHeight), whiteTexture);
        GUI.color = Color.white;
        
        // Score label
        GUI.Label(new Rect(panelX, panelY + 20f, panelWidth, 20f), "SCORE", scoreLabelStyle);
        
        // Score value
        string scoreText = displayedScore.ToString("F2");
        GUI.Label(new Rect(panelX, panelY + 45f, panelWidth, 60f), scoreText, scoreStyle);
        
        // Benchmark reference
        float benchmarkY = panelY + 115f;
        string benchmarkText = "BASELINE: 0.50";
        GUI.Label(new Rect(panelX, benchmarkY, panelWidth, 20f), benchmarkText, scoreLabelStyle);
    }
    
    void OnDestroy()
    {
        if (whiteTexture != null)
            Destroy(whiteTexture);
    }
}