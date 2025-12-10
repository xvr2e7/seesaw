using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Documentary phase controller for Laminar Flow.
/// Shows split-screen: left = gameplay replay, right = documentary video.
/// </summary>
public class DocumentaryController : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public InputRecorder inputRecorder;
    public FlowSimulation flowSimulation;
    public FlowVisualizer flowVisualizer;
    public AgentRenderer agentRenderer; 
    public Camera mainCamera;
    
    [Header("Video")]
    public string videoFileName = "documentary.mp4";

    [Tooltip("Volume of the documentary video (0-1)")]
    [Range(0f, 1f)]
    public float videoVolume = 1f;
    
    [Header("Layout")]
    [Tooltip("Spacing between panels and from screen edges")]
    public float spacing = 24f;
    
    [Tooltip("Aspect ratio for each panel (1.778 = 16:9)")]
    public float panelAspectRatio = 1.778f;
    
    [Header("Transition")]
    public float fadeDuration = 2f;
    
    [Header("Replay Cursor")]
    public Color cursorColor = new Color(1f, 0.4f, 0.3f, 0.8f);
    public float cursorThickness = 0.3f;
    
    [Header("End Behavior")]
    [Tooltip("Return to console scene after documentary ends")]
    public bool returnToConsole = true;
    
    [Tooltip("Name of the console scene")]
    public string consoleSceneName = "Console";
    
    [Tooltip("Delay after video ends before returning to console")]
    public float endDelay = 2f;
    
    [Tooltip("Fade duration when returning to console")]
    public float returnFadeDuration = 2f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    public KeyCode skipKey = KeyCode.F12;
    public KeyCode returnKey = KeyCode.Escape;
    
    // UI
    private Canvas canvas;
    private RawImage leftPanel;
    private RawImage rightPanel;
    private Image fadeOverlay;
    private GUIStyle docScoreStyle; // Style for convergence score
    
    // Video
    private VideoPlayer videoPlayer;
    private AudioSource videoAudioSource;
    private RenderTexture videoRT;
    private bool videoEnded = false;
    
    // Replay
    private RenderTexture replayRT;
    private Camera replayCamera;
    private LineRenderer cursorRing;
    private GameObject cursorObject;
    
    // State
    private bool isActive = false;
    private float startTime;
    private float replayDuration;
    private bool isReturningToConsole = false;
    
    void Start()
    {
        FindReferences();
        CreateUI();
        CreateVideoPlayer();
        CreateReplayCamera();
        CreateCursor();
        
        // Hide until needed
        canvas.gameObject.SetActive(false);
        
        // Subscribe to game end
        if (gameManager != null)
        {
            gameManager.OnStateChanged += OnGameStateChanged;
        }
    }
    
    void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnStateChanged -= OnGameStateChanged;
        }
        
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnded;
        }
        
        if (videoRT != null) { videoRT.Release(); Destroy(videoRT); }
        if (replayRT != null) { replayRT.Release(); Destroy(replayRT); }
    }
    
    void FindReferences()
    {
        if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
        if (inputRecorder == null) inputRecorder = FindObjectOfType<InputRecorder>();
        if (flowSimulation == null) flowSimulation = FindObjectOfType<FlowSimulation>();
        if (flowVisualizer == null) flowVisualizer = FindObjectOfType<FlowVisualizer>();
        if (agentRenderer == null) agentRenderer = FindObjectOfType<AgentRenderer>();
        if (mainCamera == null) mainCamera = Camera.main;
    }
    
    void OnGameStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.Complete)
        {
            StartDocumentary();
        }
    }
    
    #region Setup
    
    void CreateUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("DocumentaryCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Background (solid black)
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvas.transform);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = false;
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        
        // Left panel (replay)
        GameObject leftObj = new GameObject("LeftPanel");
        leftObj.transform.SetParent(canvas.transform);
        leftPanel = leftObj.AddComponent<RawImage>();
        leftPanel.color = Color.white;
        leftPanel.raycastTarget = false;
        
        // Right panel (video)
        GameObject rightObj = new GameObject("RightPanel");
        rightObj.transform.SetParent(canvas.transform);
        rightPanel = rightObj.AddComponent<RawImage>();
        rightPanel.color = Color.white;
        rightPanel.raycastTarget = false;
        
        // Fade overlay (on top)
        GameObject fadeObj = new GameObject("FadeOverlay");
        fadeObj.transform.SetParent(canvas.transform);
        fadeOverlay = fadeObj.AddComponent<Image>();
        fadeOverlay.color = Color.black;
        fadeOverlay.raycastTarget = false;
        RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
    }
    
    void CreateVideoPlayer()
    {
        GameObject obj = new GameObject("VideoPlayer");
        obj.transform.SetParent(transform);
        
        videoPlayer = obj.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false; 
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        
        videoPlayer.loopPointReached += OnVideoEnded;
        
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

        videoAudioSource = obj.AddComponent<AudioSource>();
        videoAudioSource.volume = videoVolume;
        videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        
        videoRT = new RenderTexture(1920, 1080, 0);
        videoRT.name = "VideoRT";
        videoPlayer.targetTexture = videoRT;
        
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        videoPlayer.url = path;
        videoPlayer.Prepare();
    }
    
    void CreateReplayCamera()
    {
        replayRT = new RenderTexture(1920, 1080, 24);
        replayRT.name = "ReplayRT";
        
        GameObject camObj = new GameObject("ReplayCamera");
        camObj.transform.SetParent(transform);
        replayCamera = camObj.AddComponent<Camera>();
        replayCamera.enabled = false;
        replayCamera.targetTexture = replayRT;
        replayCamera.clearFlags = CameraClearFlags.SolidColor;
        replayCamera.backgroundColor = Color.black;
    }
    
    void CreateCursor()
    {
        cursorObject = new GameObject("ReplayCursor");
        cursorObject.transform.SetParent(transform);
        
        cursorRing = cursorObject.AddComponent<LineRenderer>();
        cursorRing.useWorldSpace = true;
        cursorRing.loop = true;
        cursorRing.startWidth = cursorThickness;
        cursorRing.endWidth = cursorThickness;
        cursorRing.material = new Material(Shader.Find("Sprites/Default"));
        cursorRing.startColor = cursorColor;
        cursorRing.endColor = cursorColor;
        
        int segments = 32;
        cursorRing.positionCount = segments;
        
        cursorObject.SetActive(false);
    }
    
    #endregion
    
    #region Documentary Control
    
    public void StartDocumentary()
    {
        if (isActive) return;
        
        Debug.Log("[Documentary] Starting documentary phase");
        
        if (inputRecorder != null && inputRecorder.RecordedFrames.Count > 0)
        {
            replayDuration = inputRecorder.GetRecordingDuration();
        }
        else
        {
            replayDuration = 60f;
        }
        
        videoEnded = false;
        StartCoroutine(TransitionIn());
    }
    
    IEnumerator TransitionIn()
    {
        canvas.gameObject.SetActive(true);
        fadeOverlay.color = Color.black;
        
        yield return new WaitForSeconds(0.2f);
        
        DisableGameplaySystems();
        
        if (flowVisualizer != null) flowVisualizer.enabled = true;
        if (agentRenderer != null) agentRenderer.enabled = true;
        
        if (mainCamera != null)
        {
            replayCamera.CopyFrom(mainCamera);
            replayCamera.targetTexture = replayRT;
            replayCamera.clearFlags = CameraClearFlags.SolidColor;
            replayCamera.backgroundColor = Color.black;
        }
        
        leftPanel.texture = replayRT;
        rightPanel.texture = videoRT;
        
        isActive = true;
        startTime = Time.time;
        cursorObject.SetActive(true);
        
        if (videoPlayer.isPrepared)
        {
            videoPlayer.Play();
        }
        
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            fadeOverlay.color = new Color(0, 0, 0, 1f - t);
            yield return null;
        }
        fadeOverlay.color = new Color(0, 0, 0, 0);
    }
    
    void DisableGameplaySystems()
    {
        if (flowSimulation != null) flowSimulation.enabled = false;
        
        var scheduler = FindObjectOfType<TurbulentEventScheduler>();
        if (scheduler != null) scheduler.enabled = false;
        
        var playerTool = FindObjectOfType<PlayerToolController>();
        if (playerTool != null) playerTool.SetToolEnabled(false);
        
        var cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null) cameraController.enabled = false;
        
        var turbulenceUI = FindObjectOfType<TurbulenceIndicatorUI>();
        if (turbulenceUI != null) turbulenceUI.enabled = false;
        
        var gameStateUI = FindObjectOfType<GameStateUI>();
        if (gameStateUI != null) gameStateUI.enabled = false;
        
        var soundscape = FindObjectOfType<AmbientSoundscapeController>();
        if (soundscape != null) soundscape.FadeToSilence(1f);
    }
    
    void OnVideoEnded(VideoPlayer vp)
    {
        videoEnded = true;
        if (returnToConsole)
        {
            StartCoroutine(ReturnToConsoleAfterDelay());
        }
    }
    
    IEnumerator ReturnToConsoleAfterDelay()
    {
        yield return new WaitForSeconds(endDelay);
        ReturnToConsole();
    }
    
    public void ReturnToConsole()
    {
        if (isReturningToConsole) return;
        isReturningToConsole = true;
        StartCoroutine(TransitionToConsole());
    }
    
    IEnumerator TransitionToConsole()
    {
        float elapsed = 0f;
        while (elapsed < returnFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnFadeDuration;
            fadeOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeOverlay.color = Color.black;
        
        yield return null;
        
        if (videoPlayer != null) videoPlayer.Stop();
        
        ConsoleController.SetReturningFromDocumentary();
        
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(consoleSceneName, LoadSceneMode.Single);
        loadOp.allowSceneActivation = false;
        while (loadOp.progress < 0.9f) yield return null;
        loadOp.allowSceneActivation = true;
    }
    
    #endregion
    
    #region Update
    
    void Update()
    {
        if (!isActive && Input.GetKeyDown(skipKey))
        {
            if (gameManager != null && gameManager.IsPlaying) gameManager.ForceEndSession();
            if (inputRecorder != null && inputRecorder.IsRecording) inputRecorder.StopRecording();
            StartDocumentary();
        }
        
        if (isActive && Input.GetKeyDown(returnKey)) ReturnToConsole();
        
        if (videoAudioSource != null && videoAudioSource.volume != videoVolume)
            videoAudioSource.volume = videoVolume;

        if (!isActive) return;
        
        UpdateLayout();
        UpdateReplay();
    }
    
    void UpdateLayout()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        
        float availableWidth = sw - (spacing * 3f);
        float availableHeight = sh - (spacing * 2f);
        
        float panelWidth = availableWidth / 2f;
        float panelHeight = panelWidth / panelAspectRatio;
        
        if (panelHeight > availableHeight)
        {
            panelHeight = availableHeight;
            panelWidth = panelHeight * panelAspectRatio;
        }
        
        float totalWidth = (panelWidth * 2f) + spacing;
        float startX = (sw - totalWidth) / 2f;
        float startY = (sh - panelHeight) / 2f;
        
        RectTransform leftRect = leftPanel.GetComponent<RectTransform>();
        leftRect.anchorMin = Vector2.zero;
        leftRect.anchorMax = Vector2.zero;
        leftRect.pivot = Vector2.zero;
        leftRect.anchoredPosition = new Vector2(startX, startY);
        leftRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        
        RectTransform rightRect = rightPanel.GetComponent<RectTransform>();
        rightRect.anchorMin = Vector2.zero;
        rightRect.anchorMax = Vector2.zero;
        rightRect.pivot = Vector2.zero;
        rightRect.anchoredPosition = new Vector2(startX + panelWidth + spacing, startY);
        rightRect.sizeDelta = new Vector2(panelWidth, panelHeight);
    }
    
    void UpdateReplay()
    {
        if (inputRecorder == null || inputRecorder.RecordedFrames.Count == 0) return;
        
        float elapsed = Time.time - startTime;
        InputFrame frame = inputRecorder.GetInterpolatedFrameAtTime(elapsed);
        
        if (flowSimulation != null && frame.agentPositions != null)
        {
            Vector2[] simPositions = flowSimulation.Positions;
            if (simPositions != null && simPositions.Length == frame.agentPositions.Length)
            {
                System.Array.Copy(frame.agentPositions, simPositions, simPositions.Length);
            }
        }
        
        if (replayCamera != null)
        {
            replayCamera.transform.position = new Vector3(frame.cameraPosition.x, frame.cameraPosition.y, -10f);
            if (frame.cameraViewport.height > 0)
            {
                replayCamera.orthographicSize = frame.cameraViewport.height / 2f;
            }
        }

        UpdateCursor(frame);
        
        replayCamera.enabled = true;
        replayCamera.Render();
        replayCamera.enabled = false;
    }
    
    void UpdateCursor(InputFrame frame)
    {
        if (cursorRing == null) return;
        
        Color c = frame.toolActive ? cursorColor : new Color(cursorColor.r, cursorColor.g, cursorColor.b, 0.3f);
        cursorRing.startColor = c;
        cursorRing.endColor = c;
        
        float radius = frame.toolRadius;
        int segments = cursorRing.positionCount;
        
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 pos = new Vector3(
                frame.cursorWorldPosition.x + Mathf.Cos(angle) * radius,
                frame.cursorWorldPosition.y + Mathf.Sin(angle) * radius,
                -5f
            );
            cursorRing.SetPosition(i, pos);
        }
    }
    
    #endregion
    
    void OnGUI()
    {
        if (!isActive) return;
        
        // Draw Convergence Score
        DrawConvergenceScore();

        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 250, 150));
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, 250, 150), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        GUILayout.Label("=== DOCUMENTARY ===");
        GUILayout.Label($"Elapsed: {(Time.time - startTime):F1}s");
        GUILayout.Label($"Replay Duration: {replayDuration:F1}s");
        GUILayout.EndArea();
    }

    void DrawConvergenceScore()
    {
        if (inputRecorder == null || leftPanel == null) return;
        
        if (docScoreStyle == null)
        {
            docScoreStyle = new GUIStyle(GUI.skin.label);
            docScoreStyle.fontSize = 20;
            docScoreStyle.fontStyle = FontStyle.Bold;
            docScoreStyle.alignment = TextAnchor.MiddleCenter;
            docScoreStyle.normal.textColor = new Color(0.2f, 0.8f, 0.4f, 1f); // Greenish
        }

        float elapsed = Time.time - startTime;
        InputFrame frame = inputRecorder.GetInterpolatedFrameAtTime(elapsed);
        
        // Calculate Convergence Score (Inverse of Divergence)
        // Divergence is roughly 0-2+. Convergence should be 0-1 or 0-100.
        // Formula: 1 / (1 + divergence) ensures it starts at 1 (when div=0) and decreases.
        float divergence = frame.currentDivergence;
        float convergence = 1.0f / (1.0f + divergence);
        
        // Get position above the Left Panel (Replay)
        Vector3[] corners = new Vector3[4];
        leftPanel.GetComponent<RectTransform>().GetWorldCorners(corners);
        
        // Invert Y for GUI.Label (WorldCorners starts bottom-left, GUI starts top-left)
        float topY = corners[1].y; 
        float guiY = Screen.height - topY;
        
        float centerX = (corners[0].x + corners[2].x) * 0.5f;
        float width = corners[2].x - corners[0].x;
        
        string text = $"CONVERGENCE SCORE: {convergence * 100f:F0}%";
        GUI.Label(new Rect(centerX - width/2f, guiY - 40f, width, 30f), text, docScoreStyle);
    }
}