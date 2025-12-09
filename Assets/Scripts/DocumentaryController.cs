using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Documentary phase controller for Laminar Flow.
/// Shows split-screen: left = gameplay replay, right = documentary video.
/// Both panels maintain 16:9 aspect ratio.
/// 
/// After documentary ends, fades back to Console scene.
/// </summary>
public class DocumentaryController : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public InputRecorder inputRecorder;
    public FlowSimulation flowSimulation;
    public FlowVisualizer flowVisualizer;
    public Camera mainCamera;
    
    [Header("Video")]
    public string videoFileName = "documentary.mp4";
    
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
    private Text endPromptText;
    
    // Video
    private VideoPlayer videoPlayer;
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
        
        // Background
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
        
        // End prompt text
        GameObject promptObj = new GameObject("EndPrompt");
        promptObj.transform.SetParent(canvas.transform);
        endPromptText = promptObj.AddComponent<Text>();
        endPromptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        endPromptText.fontSize = 18;
        endPromptText.color = new Color(0.6f, 0.6f, 0.6f, 0f);
        endPromptText.alignment = TextAnchor.LowerCenter;
        endPromptText.text = "Press ESC to return";
        
        RectTransform promptRect = promptObj.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0, 0);
        promptRect.anchorMax = new Vector2(1, 0);
        promptRect.pivot = new Vector2(0.5f, 0);
        promptRect.anchoredPosition = new Vector2(0, 30);
        promptRect.sizeDelta = new Vector2(0, 40);
        
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
        videoPlayer.isLooping = false; // Don't loop - we want to detect end
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        
        // Subscribe to video end
        videoPlayer.loopPointReached += OnVideoEnded;
        
        // Audio
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        AudioSource audioSource = obj.AddComponent<AudioSource>();
        videoPlayer.SetTargetAudioSource(0, audioSource);
        
        // Render texture
        videoRT = new RenderTexture(1920, 1080, 0);
        videoRT.name = "VideoRT";
        videoPlayer.targetTexture = videoRT;
        
        // Set URL
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        videoPlayer.url = path;
        videoPlayer.Prepare();
    }
    
    void OnVideoEnded(VideoPlayer vp)
    {
        videoEnded = true;
        Debug.Log("[Documentary] Video playback ended");
        
        if (returnToConsole)
        {
            StartCoroutine(ReturnToConsoleAfterDelay());
        }
    }
    
    IEnumerator ReturnToConsoleAfterDelay()
    {
        // Show prompt
        float promptFadeTime = 1f;
        float elapsed = 0f;
        while (elapsed < promptFadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = elapsed / promptFadeTime;
            endPromptText.color = new Color(0.6f, 0.6f, 0.6f, alpha * 0.8f);
            yield return null;
        }
        
        // Wait for delay or user input
        float waitTime = 0f;
        while (waitTime < endDelay)
        {
            if (Input.GetKeyDown(returnKey))
            {
                break;
            }
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        // Return to console
        ReturnToConsole();
    }
    
    void CreateReplayCamera()
    {
        // Render texture
        replayRT = new RenderTexture(1920, 1080, 24);
        replayRT.name = "ReplayRT";
        
        // Camera
        GameObject camObj = new GameObject("ReplayCamera");
        camObj.transform.SetParent(transform);
        replayCamera = camObj.AddComponent<Camera>();
        replayCamera.targetTexture = replayRT;
        replayCamera.enabled = false;
    }
    
    void CreateCursor()
    {
        cursorObject = new GameObject("ReplayCursor");
        cursorObject.transform.SetParent(transform);
        cursorObject.SetActive(false);
        
        cursorRing = cursorObject.AddComponent<LineRenderer>();
        cursorRing.useWorldSpace = true;
        cursorRing.loop = true;
        cursorRing.positionCount = 64;
        cursorRing.startWidth = cursorThickness;
        cursorRing.endWidth = cursorThickness;
        cursorRing.material = new Material(Shader.Find("Sprites/Default"));
        cursorRing.startColor = cursorColor;
        cursorRing.endColor = cursorColor;
        cursorRing.sortingOrder = 100;
    }
    
    #endregion
    
    #region Playback Control
    
    public void StartDocumentary()
    {
        if (isActive) return;
        
        // Get replay duration
        if (inputRecorder != null && inputRecorder.Metadata != null)
        {
            replayDuration = inputRecorder.Metadata.sessionDuration;
        }
        else if (inputRecorder != null && inputRecorder.RecordedFrames.Count > 0)
        {
            var frames = inputRecorder.RecordedFrames;
            replayDuration = frames[frames.Count - 1].timestamp;
        }
        else
        {
            replayDuration = 60f;
        }
        
        Debug.Log($"[Documentary] Starting. Replay duration: {replayDuration:F1}s");
        
        videoEnded = false;
        StartCoroutine(TransitionIn());
    }
    
    IEnumerator TransitionIn()
    {
        // Show canvas with black overlay
        canvas.gameObject.SetActive(true);
        fadeOverlay.color = Color.black;
        endPromptText.color = new Color(0.6f, 0.6f, 0.6f, 0f);
        
        // Wait a moment
        yield return new WaitForSeconds(0.2f);
        
        // Pause all game systems
        PauseGame();
        
        // Setup replay camera (copy main camera settings)
        if (mainCamera != null)
        {
            replayCamera.CopyFrom(mainCamera);
            replayCamera.targetTexture = replayRT;
        }
        
        // Assign textures to panels
        leftPanel.texture = replayRT;
        rightPanel.texture = videoRT;
        
        // Start playback
        isActive = true;
        startTime = Time.time;
        cursorObject.SetActive(true);
        
        if (videoPlayer.isPrepared)
        {
            videoPlayer.Play();
        }
        
        // Fade in
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
    
    void PauseGame()
    {
        // Disable simulation
        if (flowSimulation != null) flowSimulation.enabled = false;
        if (flowVisualizer != null) flowVisualizer.enabled = false;
        
        // Disable other systems
        var scheduler = FindObjectOfType<TurbulentEventScheduler>();
        if (scheduler != null) scheduler.enabled = false;
        
        var agentRenderer = FindObjectOfType<AgentRenderer>();
        if (agentRenderer != null) agentRenderer.enabled = false;
        
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
        
        Debug.Log("[Documentary] Game paused");
    }
    
    public void ReturnToConsole()
    {
        if (isReturningToConsole) return;
        
        isReturningToConsole = true;
        StartCoroutine(TransitionToConsole());
    }
    
    IEnumerator TransitionToConsole()
    {
        Debug.Log("[Documentary] Returning to console...");
        
        // Fade out
        float elapsed = 0f;
        while (elapsed < returnFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnFadeDuration;
            fadeOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeOverlay.color = Color.black;
        
        // Stop video
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        
        // Load console scene
        SceneManager.LoadScene(consoleSceneName);
    }
    
    #endregion
    
    #region Update
    
    void Update()
    {
        // Debug skip key
        if (!isActive && Input.GetKeyDown(skipKey))
        {
            Debug.Log("[Documentary] Skip key pressed");
            
            // Force end game if playing
            if (gameManager != null && gameManager.IsPlaying)
            {
                gameManager.ForceEndSession();
            }
            
            // Stop recording
            if (inputRecorder != null && inputRecorder.IsRecording)
            {
                inputRecorder.StopRecording();
            }
            
            StartDocumentary();
        }
        
        // Return key during documentary
        if (isActive && Input.GetKeyDown(returnKey))
        {
            ReturnToConsole();
        }
        
        if (!isActive) return;
        
        UpdateLayout();
        UpdateReplay();
    }
    
    void UpdateLayout()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        
        // Calculate panel size to fit two 16:9 panels side by side with spacing
        // Total width needed: spacing + panel + spacing + panel + spacing = 3*spacing + 2*panel
        // Total height needed: spacing + panel + spacing = 2*spacing + panel
        
        float availableWidth = sw - (spacing * 3f);
        float availableHeight = sh - (spacing * 2f);
        
        // Each panel gets half the available width
        float panelWidth = availableWidth / 2f;
        float panelHeight = panelWidth / panelAspectRatio;
        
        // If too tall, scale down
        if (panelHeight > availableHeight)
        {
            panelHeight = availableHeight;
            panelWidth = panelHeight * panelAspectRatio;
        }
        
        // Center vertically
        float totalWidth = (panelWidth * 2f) + spacing;
        float startX = (sw - totalWidth) / 2f;
        float startY = (sh - panelHeight) / 2f;
        
        // Left panel
        RectTransform leftRect = leftPanel.GetComponent<RectTransform>();
        leftRect.anchorMin = Vector2.zero;
        leftRect.anchorMax = Vector2.zero;
        leftRect.pivot = Vector2.zero;
        leftRect.anchoredPosition = new Vector2(startX, startY);
        leftRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        
        // Right panel
        RectTransform rightRect = rightPanel.GetComponent<RectTransform>();
        rightRect.anchorMin = Vector2.zero;
        rightRect.anchorMax = Vector2.zero;
        rightRect.pivot = Vector2.zero;
        rightRect.anchoredPosition = new Vector2(startX + panelWidth + spacing, startY);
        rightRect.sizeDelta = new Vector2(panelWidth, panelHeight);
    }
    
    void UpdateReplay()
    {
        if (inputRecorder == null || inputRecorder.RecordedFrames.Count == 0)
        {
            // No recorded data - just render current view
            if (replayCamera != null)
            {
                replayCamera.Render();
            }
            return;
        }
        
        // Calculate replay time (loop if video is still playing)
        float elapsed = Time.time - startTime;
        float replayTime;
        
        if (!videoEnded)
        {
            replayTime = replayDuration > 0 ? (elapsed % replayDuration) : elapsed;
        }
        else
        {
            // Hold at end
            replayTime = replayDuration;
        }
        
        // Get frame data
        InputFrame frame = inputRecorder.GetInterpolatedFrameAtTime(replayTime);
        
        // Update cursor
        UpdateCursor(frame);
        
        // Update replay camera position
        if (replayCamera != null && mainCamera != null)
        {
            if (frame.cameraViewport.width > 0)
            {
                replayCamera.transform.position = new Vector3(
                    frame.cameraPosition.x,
                    frame.cameraPosition.y,
                    mainCamera.transform.position.z
                );
            }
            
            replayCamera.orthographic = mainCamera.orthographic;
            replayCamera.orthographicSize = mainCamera.orthographicSize;
            
            // Render
            replayCamera.Render();
        }
    }
    
    void UpdateCursor(InputFrame frame)
    {
        if (cursorRing == null) return;
        
        // Color - brighter when tool was active
        Color color = cursorColor;
        if (frame.toolActive)
        {
            color.a = 0.9f;
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 8f);
            cursorRing.startWidth = cursorThickness * pulse * 1.5f;
            cursorRing.endWidth = cursorThickness * pulse * 1.5f;
        }
        else
        {
            color.a = 0.5f;
            cursorRing.startWidth = cursorThickness;
            cursorRing.endWidth = cursorThickness;
        }
        
        cursorRing.startColor = color;
        cursorRing.endColor = color;
        
        // Draw ring
        float radius = frame.toolRadius;
        for (int i = 0; i < 64; i++)
        {
            float angle = (float)i / 64f * Mathf.PI * 2f;
            float x = frame.cursorWorldPosition.x + Mathf.Cos(angle) * radius;
            float y = frame.cursorWorldPosition.y + Mathf.Sin(angle) * radius;
            cursorRing.SetPosition(i, new Vector3(x, y, -5f));
        }
    }
    
    #endregion
    
    #region Debug
    
    void OnGUI()
    {
        if (!showDebugInfo || !isActive) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 180));
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, 300, 180), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        GUILayout.Label("=== DOCUMENTARY ===");
        
        float elapsed = Time.time - startTime;
        float replayTime = replayDuration > 0 ? (elapsed % replayDuration) : elapsed;
        
        GUILayout.Label($"Elapsed: {elapsed:F1}s");
        GUILayout.Label($"Replay: {replayTime:F1}s / {replayDuration:F1}s");
        
        if (videoPlayer != null)
        {
            GUILayout.Label($"Video: {videoPlayer.time:F1}s / {videoPlayer.length:F1}s");
            GUILayout.Label($"Video Playing: {videoPlayer.isPlaying}");
            GUILayout.Label($"Video Ended: {videoEnded}");
        }
        
        if (inputRecorder != null)
        {
            GUILayout.Label($"Recorded Frames: {inputRecorder.RecordedFrames.Count}");
        }
        
        GUILayout.Label($"Press ESC to return to console");
        
        GUILayout.EndArea();
    }
    
    #endregion
}