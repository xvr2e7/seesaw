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
/// After documentary ends, automatically fades back to Console scene.
/// </summary>
public class DocumentaryController : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public InputRecorder inputRecorder;
    public FlowSimulation flowSimulation;
    public FlowVisualizer flowVisualizer;
    public AgentRenderer agentRenderer; // Added reference to renderer
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
        videoPlayer.isLooping = false; // Don't loop - we want to detect end
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        
        // Subscribe to video end
        videoPlayer.loopPointReached += OnVideoEnded;
        
        // Audio
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

        videoAudioSource = obj.AddComponent<AudioSource>();
        videoAudioSource.volume = videoVolume;
        videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        
        // Render texture
        videoRT = new RenderTexture(1920, 1080, 0);
        videoRT.name = "VideoRT";
        videoPlayer.targetTexture = videoRT;
        
        // Set URL
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        videoPlayer.url = path;
        videoPlayer.Prepare();
    }
    
    void CreateReplayCamera()
    {
        // Create render texture for replay
        replayRT = new RenderTexture(1920, 1080, 24);
        replayRT.name = "ReplayRT";
        
        // Create camera
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
        
        // Create circle
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
        
        // Get replay duration from recorder
        if (inputRecorder != null && inputRecorder.RecordedFrames.Count > 0)
        {
            replayDuration = inputRecorder.GetRecordingDuration();
        }
        else
        {
            replayDuration = 60f; // Default fallback
        }
        
        Debug.Log($"[Documentary] Replay duration: {replayDuration:F1}s");
        
        videoEnded = false;
        StartCoroutine(TransitionIn());
    }
    
    IEnumerator TransitionIn()
    {
        // Show canvas with black overlay
        canvas.gameObject.SetActive(true);
        fadeOverlay.color = Color.black;
        
        // Wait a moment
        yield return new WaitForSeconds(0.2f);
        
        // Disable gameplay systems (physics, input), but KEEP VISUALS ENABLED
        DisableGameplaySystems();
        
        // Ensure visualizers are enabled for replay
        if (flowVisualizer != null) flowVisualizer.enabled = true;
        if (agentRenderer != null) agentRenderer.enabled = true;
        
        // Setup replay camera (copy main camera settings)
        if (mainCamera != null)
        {
            replayCamera.CopyFrom(mainCamera);
            replayCamera.targetTexture = replayRT;
            replayCamera.clearFlags = CameraClearFlags.SolidColor;
            replayCamera.backgroundColor = Color.black;
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
    
    void DisableGameplaySystems()
    {
        // Disable simulation physics only
        if (flowSimulation != null) flowSimulation.enabled = false;
        
        // Do NOT disable visualizers here (handled in TransitionIn)
        
        // Disable other systems
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
        
        Debug.Log("[Documentary] Gameplay systems disabled for replay");
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
        // Wait for the specified delay
        yield return new WaitForSeconds(endDelay);
        
        // Auto-return to console
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
        Debug.Log("[Documentary] Returning to console...");
        
        // Fade out to black
        float elapsed = 0f;
        while (elapsed < returnFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnFadeDuration;
            fadeOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        fadeOverlay.color = Color.black;
        
        // Wait a frame to ensure black is rendered
        yield return null;
        
        // Stop video
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
        
        // Notify ConsoleController that we're returning
        ConsoleController.SetReturningFromDocumentary();
        
        // Load console scene
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(consoleSceneName, LoadSceneMode.Single);
        loadOp.allowSceneActivation = false;
        
        // Wait until scene is ready
        while (loadOp.progress < 0.9f)
        {
            yield return null;
        }
        
        // Activate the scene
        loadOp.allowSceneActivation = true;
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
        
        if (videoAudioSource != null && videoAudioSource.volume != videoVolume)
        {
            videoAudioSource.volume = videoVolume;
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
        
        // If too tall, scale down to fit height
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
        if (inputRecorder == null || inputRecorder.RecordedFrames.Count == 0) return;
        
        float elapsed = Time.time - startTime;
        
        // Get replay frame
        InputFrame frame = inputRecorder.GetInterpolatedFrameAtTime(elapsed);
        
        // --- 1. REPLAY BACKGROUND (Agents) ---
        // Inject recorded positions into the simulation data
        // The simulation physics is disabled, but visualizers read from this array
        if (flowSimulation != null && frame.agentPositions != null)
        {
            Vector2[] simPositions = flowSimulation.Positions;
            if (simPositions != null && simPositions.Length == frame.agentPositions.Length)
            {
                System.Array.Copy(frame.agentPositions, simPositions, simPositions.Length);
            }
        }
        
        // --- 2. REPLAY CAMERA ---
        if (replayCamera != null)
        {
            // Apply position
            replayCamera.transform.position = new Vector3(frame.cameraPosition.x, frame.cameraPosition.y, -10f);
            
            // Apply zoom (Ortho size is half of visible height)
            if (frame.cameraViewport.height > 0)
            {
                replayCamera.orthographicSize = frame.cameraViewport.height / 2f;
            }
        }

        // --- 3. REPLAY CURSOR ---
        UpdateCursor(frame);
        
        // Render replay frame
        replayCamera.enabled = true;
        replayCamera.Render();
        replayCamera.enabled = false;
    }
    
    void UpdateCursor(InputFrame frame)
    {
        if (cursorRing == null) return;
        
        // Set cursor color based on tool state
        Color c = frame.toolActive ? cursorColor : new Color(cursorColor.r, cursorColor.g, cursorColor.b, 0.3f);
        cursorRing.startColor = c;
        cursorRing.endColor = c;
        
        // Update circle positions
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
        if (!showDebugInfo || !isActive) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 250, 150));
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, 250, 150), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        GUILayout.Label("=== DOCUMENTARY ===");
        GUILayout.Label($"Active: {isActive}");
        GUILayout.Label($"Video Ended: {videoEnded}");
        GUILayout.Label($"Elapsed: {(Time.time - startTime):F1}s");
        GUILayout.Label($"Replay Duration: {replayDuration:F1}s");
        GUILayout.Label($"Press {returnKey} to return");
        GUILayout.EndArea();
    }
}