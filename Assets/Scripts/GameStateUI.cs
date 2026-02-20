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
    [Tooltip("Step 0 — shown until mouse has moved enough")]
    public string guidanceStep0Text = "move your cursor across the field";
    [Tooltip("Step 1 — shown while CIRCULAR event is live; player must hover without clicking")]
    public string guidanceStep1Text = "some events are not threats";
    [Tooltip("Step 2 — shown while SCATTER event is live; player must suppress it")]
    public string guidanceStep2Text = "hold LEFT CLICK to suppress";
    [Tooltip("Step 3 — brief acknowledgement after suppression")]
    public string guidanceStep3Text = "learn which ones to leave";
    [Tooltip("Step 4 — session transition text")]
    public string guidanceStep4Text = "session begins";
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

    // Terminal intro
    private GUIStyle terminalLineStyle;   // monospace line style
    private GUIStyle terminalPromptStyle; // "PRESS SPACE" hint
    private float    terminalFadeOut = 0f; // 0=visible, 1=gone

    private const int   TERMINAL_FONT_SIZE  = 18;
    private const float TERMINAL_LINE_H     = 26f; // px per line at 18pt
    private const int   TERMINAL_MAX_LINES  = 28;  // visible scroll window

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

        Font resolvedFont = customFont;
        if (resolvedFont == null)
            resolvedFont = Font.CreateDynamicFontFromOSFont(
                new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, 18);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        if (resolvedFont != null) labelStyle.font = resolvedFont;
        labelStyle.normal.textColor = textColor;

        valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 58,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        if (resolvedFont != null) valueStyle.font = resolvedFont;
        valueStyle.normal.textColor = new Color(0.55f, 0.58f, 0.62f, 0.90f);

        scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 42,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        if (resolvedFont != null) scoreStyle.font = resolvedFont;
        scoreStyle.normal.textColor = new Color(0.75f, 0.75f, 0.78f, 1f);

        Font scoreLabelFont = customFont;
        if (scoreLabelFont == null)
            scoreLabelFont = Font.CreateDynamicFontFromOSFont(
                new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, 22);

        scoreLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 22,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter
        };
        if (scoreLabelFont != null) scoreLabelStyle.font = scoreLabelFont;
        scoreLabelStyle.normal.textColor = new Color(0.40f, 0.42f, 0.45f, 0.9f);

        stylesInitialized = true;
    }
    
    void Update()
    {
        UpdateDivergence();
        UpdateStateTransitions();

        if (gameManager != null && !gameManager.IsTerminalIntro && terminalFadeOut < 1f)
            terminalFadeOut += Time.deltaTime / 0.6f;

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
            case GameManager.GameState.Guidance:
                // Fade in from black at scene start
                introFadeProgress += Time.deltaTime / fadeInDuration;
                introFadeProgress = Mathf.Clamp01(introFadeProgress);
                stateTransitionAlpha = 1f - introFadeProgress;
                break;

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

        if (newState == GameManager.GameState.Guidance)
        {
            // If we're coming from the terminal intro the screen is already
            // fading to black via DrawTerminalIntro — skip the separate fade-in.
            bool fromTerminal = gameManager != null && terminalFadeOut < 1f;
            if (fromTerminal)
            {
                introFadeProgress    = 1f;
                stateTransitionAlpha = 0f;
            }
            else
            {
                // Cold start (e.g. resume): start fully black, fade in
                introFadeProgress    = 0f;
                stateTransitionAlpha = 1f;
            }
        }
        else if (newState == GameManager.GameState.Intro)
        {
            introFadeProgress    = 1f;
            stateTransitionAlpha = 0f;
        }
        else if (newState == GameManager.GameState.Complete)
        {
            showingFinalScore  = true;
            scoreAnimationTime = 0f;
            displayedScore     = 0f;
        }
    }
    
    private bool isPaused = false;
    public void SetPaused(bool paused) { isPaused = paused; }

    void OnGUI()
    {
        if (isPaused) return;
        InitStyles();

        // Terminal intro runs before guidance — draws over everything
        bool terminalVisible = gameManager != null && (gameManager.IsTerminalIntro || terminalFadeOut < 1f);
        if (terminalVisible)
        {
            DrawTerminalIntro();
            return; // nothing else shown yet
        }

        // HUD not shown during guidance or score screen
        if (currentGameState != GameManager.GameState.Complete &&
            currentGameState != GameManager.GameState.Guidance)
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
    
    void DrawTerminalIntro()
    {
        float bgAlpha = gameManager.IsTerminalIntro ? 1f : Mathf.Clamp01(1f - terminalFadeOut);
        GUI.color = new Color(0f, 0f, 0f, bgAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
        GUI.color = Color.white;

        if (bgAlpha <= 0.01f) return;

        Font tFont = customFont ?? Font.CreateDynamicFontFromOSFont(
            new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, TERMINAL_FONT_SIZE);

        if (terminalLineStyle == null)
        {
            terminalLineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = TERMINAL_FONT_SIZE,
                alignment = TextAnchor.UpperLeft,
                wordWrap  = false,
                richText  = false
            };
            if (tFont != null) terminalLineStyle.font = tFont;
        }

        if (terminalPromptStyle == null)
        {
            terminalPromptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = TERMINAL_FONT_SIZE,
                alignment = TextAnchor.MiddleCenter,
                wordWrap  = false,
                richText  = false
            };
            if (tFont != null) terminalPromptStyle.font = tFont;
        }

        bool isBriefing = gameManager.CurrentTerminalPhase == GameManager.TerminalPhase.Briefing;

        // Build the visible line list: completed lines + active line (with blinking cursor)
        var   lines     = gameManager.TerminalLines;
        string active   = gameManager.TerminalActiveLine;
        bool  typing    = gameManager.TerminalTyping;
        float cursor    = typing ? (Mathf.Sin(Time.time * 8f) > 0f ? 1f : 0f) : 0f;
        string activeFull = active + (cursor > 0.5f ? "_" : " ");

        // Determine scroll window — show latest TERMINAL_MAX_LINES
        int totalCount  = lines.Count + (typing || active.Length > 0 ? 1 : 0);
        int startIdx    = Mathf.Max(0, totalCount - TERMINAL_MAX_LINES);

        float padX = Mathf.Round(Screen.width  * 0.083f);
        float padY = Mathf.Round(Screen.height * 0.093f);
        float colW = Screen.width  * 0.84f;
        float lineH = TERMINAL_LINE_H * (Screen.height / 1080f); // scale to resolution

        int bootLineCount = gameManager.TerminalBootLineCount;

        // Draw each visible line
        int drawIdx = 0;
        for (int i = startIdx; i < lines.Count; i++, drawIdx++)
        {
            // Boot lines dim out once briefing starts
            bool isBootLine = i < bootLineCount;
            float b = (isBootLine && isBriefing) ? 0.38f : 0.70f;
            terminalLineStyle.normal.textColor = new Color(b, b + 0.03f, b + 0.07f, bgAlpha * 0.92f);

            float y = padY + drawIdx * lineH;
            GUI.Label(new Rect(padX, y, colW, lineH + 2f), lines[i], terminalLineStyle);
        }

        // Draw the active (currently typing) line
        if (active.Length > 0 || typing)
        {
            float b = isBriefing ? 0.88f : 0.72f;
            terminalLineStyle.normal.textColor = new Color(b, b, b + 0.05f, bgAlpha);
            float y = padY + drawIdx * lineH;
            GUI.Label(new Rect(padX, y, colW, lineH + 2f), activeFull, terminalLineStyle);
        }

        // ── "PRESS SPACE" prompt ───────────────────────────────────────────────
        if (gameManager.TerminalAwaitSpace)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
            terminalPromptStyle.normal.textColor = new Color(0.52f, 0.58f, 0.64f, bgAlpha * pulse);
            GUI.Label(new Rect(0f, Screen.height - 58f, Screen.width, 40f),
                      "PRESS  SPACE  TO  CONTINUE", terminalPromptStyle);
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
        if (gameManager == null || whiteTexture == null) return;

        // Draw a solid black rect on top of the partial-black fade overlay
        GUI.color = new Color(0f, 0f, 0f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTexture);
        GUI.color = Color.white;

        // Delay text reveal by 0.5s after entering Complete state
        float scoreRevealAlpha = Mathf.Clamp01((scoreAnimationTime - 0.5f) / 1.2f);
        if (scoreRevealAlpha <= 0.005f) return;

        // Line 1: "MEAN CONVERGENCE" label
        scoreLabelStyle.normal.textColor = new Color(0.40f, 0.42f, 0.45f, scoreRevealAlpha * 0.9f);
        float line1H = 36f;
        float line1Y = Screen.height * 0.5f - 70f;
        GUI.Label(new Rect(0, line1Y, Screen.width, line1H), "MEAN CONVERGENCE", scoreLabelStyle);

        // Line 2: animated numeric score
        scoreStyle.normal.textColor = new Color(0.75f, 0.75f, 0.78f, scoreRevealAlpha);
        string scoreStr = displayedScore.ToString("F3");
        float line2H = 60f;
        float line2Y = line1Y + line1H + 10f;
        GUI.Label(new Rect(0, line2Y, Screen.width, line2H), scoreStr, scoreStyle);

        // "PRESS SPACE TO CONTINUE" — appears after score is fully revealed
        float promptAlpha = Mathf.Clamp01((scoreAnimationTime - scoreTransitionDuration - 0.5f) / 0.8f) * scoreRevealAlpha;
        if (promptAlpha > 0.005f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
            scoreLabelStyle.normal.textColor = new Color(0.40f, 0.42f, 0.45f, promptAlpha * pulse);
            GUI.Label(new Rect(0f, Screen.height - 58f, Screen.width, 40f),
                      "PRESS  SPACE  TO  CONTINUE", scoreLabelStyle);
        }
    }
    
    void DrawGuidanceOverlay()
    {
        if (currentGameState != GameManager.GameState.Guidance) return;
        if (gameManager == null) return;

        int step = gameManager.GuidanceStep;
        string message = step switch
        {
            0 => guidanceStep0Text,
            1 => guidanceStep1Text,
            2 => guidanceStep2Text,
            3 => guidanceStep3Text,
            4 => guidanceStep4Text,
            _ => null
        };
        if (message == null) return;

        if (guidanceStyle == null)
        {
            guidanceStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = guidanceFontSize,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            Font gFont = customFont ?? Font.CreateDynamicFontFromOSFont(
                new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, guidanceFontSize);
            if (gFont != null) guidanceStyle.font = gFont;
        }

        Color col = new Color(0.70f, 0.72f, 0.76f, guidanceMaxAlpha);
        guidanceStyle.normal.textColor = col;
        guidanceStyle.hover.textColor  = col;

        float labelH = 28f;
        GUI.Label(new Rect(0f, Screen.height * 0.5f - labelH * 0.5f, Screen.width, labelH),
                  message, guidanceStyle);
    }

    void DrawRevelationText()
    {
        // Suppressed — replaced by score screen on Complete state
        return;
#pragma warning disable CS0162
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
            Font rFont = customFont ?? Font.CreateDynamicFontFromOSFont(
                new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, revelationFontSize);
            if (rFont != null) revelationStyle.font = rFont;
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