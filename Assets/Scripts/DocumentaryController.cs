using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Documentary phase controller.
///
/// Layout: left panel = live simulation replay (looping), right panel = documentary video.
/// The simulation runs with no player input and loops when a session ends.
/// When the video ends (or F12 is pressed) the scene returns to Console.
/// </summary>
public class DocumentaryController : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public FlowSimulation flowSimulation;

    [Header("Video")]
    public string videoFileName = "laminar_demo.mp4";

    [Tooltip("Volume of the documentary video (0–1)")]
    [Range(0f, 1f)]
    public float videoVolume = 1f;

    [Header("Layout")]
    [Tooltip("Gap between panels and from screen edges (pixels)")]
    public float spacing = 24f;

    [Tooltip("Aspect ratio for each panel (1.778 = 16:9)")]
    public float panelAspectRatio = 1.778f;

    [Header("Transition")]
    public float fadeDuration = 1.5f;

    [Header("End Behaviour")]
    public string consoleSceneName = "Console";

    [Tooltip("Delay after video ends before returning to console")]
    public float endDelay = 1f;

    [Header("Debug")]
    public bool showDebugInfo = false;
    public KeyCode skipKey = KeyCode.F12;

    // ── UI ────────────────────────────────────────────────────────────────────
    private Canvas    canvas;
    private RawImage  leftPanel;   // replay
    private RawImage  rightPanel;  // video
    private Image     fadeOverlay;

    // ── Replay camera ─────────────────────────────────────────────────────────
    private Camera        replayCamera;
    private RenderTexture replayRT;

    // ── Video ─────────────────────────────────────────────────────────────────
    private VideoPlayer   videoPlayer;
    private AudioSource   videoAudio;
    private RenderTexture videoRT;
    private bool          videoEnded = false;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool isActive             = false;
    private bool isReturningToConsole = false;

    // Layout cached each frame for OnGUI label placement
    private float _panelX, _panelY, _panelW, _panelH;

    // GUI style for the score label
    private GUIStyle _scoreLabelStyle;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (gameManager    == null) gameManager    = FindObjectOfType<GameManager>();
        if (flowSimulation == null) flowSimulation = FindObjectOfType<FlowSimulation>();
        CreateUI();
        CreateReplayCamera();
        CreateVideoPlayer();
        canvas.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnVideoEnded;
        if (videoRT  != null) { videoRT.Release();  Destroy(videoRT);  }
        if (replayRT != null) { replayRT.Release(); Destroy(replayRT); }
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    void CreateUI()
    {
        var canvasGO = new GameObject("DocumentaryCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Black background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvas.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = Color.black;
        bgImg.raycastTarget = false;
        FullRect(bgGO);

        // Left panel — replay
        var leftGO = new GameObject("LeftPanel");
        leftGO.transform.SetParent(canvas.transform, false);
        leftPanel = leftGO.AddComponent<RawImage>();
        leftPanel.color = Color.white;
        leftPanel.raycastTarget = false;
        ZeroRect(leftGO);

        // Right panel — video
        var rightGO = new GameObject("RightPanel");
        rightGO.transform.SetParent(canvas.transform, false);
        rightPanel = rightGO.AddComponent<RawImage>();
        rightPanel.color = Color.white;
        rightPanel.raycastTarget = false;
        ZeroRect(rightGO);

        // Fade overlay (topmost)
        var fadeGO = new GameObject("FadeOverlay");
        fadeGO.transform.SetParent(canvas.transform, false);
        fadeOverlay = fadeGO.AddComponent<Image>();
        fadeOverlay.color = Color.black;
        fadeOverlay.raycastTarget = false;
        FullRect(fadeGO);
    }

    static void FullRect(GameObject go)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    // Anchor at bottom-left, size zero — UpdateLayout drives the rect each frame
    static void ZeroRect(GameObject go)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.zero;
        r.pivot     = Vector2.zero;
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = Vector2.zero;
    }

    void CreateReplayCamera()
    {
        replayRT = new RenderTexture(1920, 1080, 24);
        replayRT.name = "ReplayRT";

        var camGO = new GameObject("DocReplayCamera");
        camGO.transform.SetParent(transform);
        replayCamera = camGO.AddComponent<Camera>();
        replayCamera.enabled       = false;
        replayCamera.targetTexture = replayRT;
        replayCamera.clearFlags    = CameraClearFlags.SolidColor;
        replayCamera.backgroundColor = Color.black;

        leftPanel.texture = replayRT;
    }

    void CreateVideoPlayer()
    {
        var vpGO = new GameObject("VideoPlayer");
        vpGO.transform.SetParent(transform);

        videoPlayer = vpGO.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake     = false;
        videoPlayer.isLooping       = false;
        videoPlayer.renderMode      = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

        videoAudio             = vpGO.AddComponent<AudioSource>();
        videoAudio.playOnAwake = false;
        videoAudio.volume      = videoVolume;
        videoPlayer.SetTargetAudioSource(0, videoAudio);

        videoRT                   = new RenderTexture(1920, 1080, 0);
        videoRT.name              = "DocVideoRT";
        videoPlayer.targetTexture = videoRT;
        rightPanel.texture        = videoRT;

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        videoPlayer.url = path;
        videoPlayer.loopPointReached += OnVideoEnded;
        videoPlayer.Prepare();
    }

    // ── Public entry point ────────────────────────────────────────────────────

    public void StartDocumentary()
    {
        if (isActive) return;
        StartCoroutine(DocumentarySequence());
    }

    // ── Core sequence ─────────────────────────────────────────────────────────

    IEnumerator DocumentarySequence()
    {
        isActive = true;
        canvas.gameObject.SetActive(true);
        fadeOverlay.color = Color.black;

        DisablePlayerSystems();

        // Mirror main camera settings onto replay camera
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            replayCamera.CopyFrom(mainCam);
            replayCamera.targetTexture   = replayRT;
            replayCamera.clearFlags      = CameraClearFlags.SolidColor;
            replayCamera.backgroundColor = Color.black;
        }

        // Restart simulation in looping replay mode
        if (gameManager != null)
        {
            gameManager.IsInDocumentaryReplay = true;
            gameManager.RestartForDocumentaryReplay();
        }

        // Wait for video to finish preparing
        if (!videoPlayer.isPrepared)
            yield return new WaitUntil(() => videoPlayer.isPrepared);

        videoPlayer.Play();

        // Fade in from black
        yield return StartCoroutine(FadeTo(0f, fadeDuration));

        // Wait for video end or F12 skip
        yield return new WaitUntil(() => videoEnded || isReturningToConsole);

        if (!isReturningToConsole)
            StartCoroutine(FinishAndReturn());
    }

    void OnVideoEnded(VideoPlayer vp)
    {
        if (!isActive) return;
        videoEnded = true;
        StartCoroutine(FinishAndReturn());
    }

    IEnumerator FinishAndReturn()
    {
        if (isReturningToConsole) yield break;
        isReturningToConsole = true;

        yield return new WaitForSeconds(endDelay);
        yield return StartCoroutine(FadeTo(1f, fadeDuration));

        videoPlayer.Stop();

        if (gameManager != null)
            gameManager.IsInDocumentaryReplay = false;

        ConsoleController.SetReturningFromDocumentary();

        var op = SceneManager.LoadSceneAsync(consoleSceneName, LoadSceneMode.Single);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (videoAudio != null) videoAudio.volume = videoVolume;

        // F12 skips documentary and returns immediately to Console
        if (isActive && !isReturningToConsole && Input.GetKeyDown(skipKey))
            StartCoroutine(FinishAndReturn());

        if (!isActive) return;

        UpdateLayout();

        // Render the live simulation into the replay panel each frame
        if (replayCamera != null)
        {
            replayCamera.enabled = true;
            replayCamera.Render();
            replayCamera.enabled = false;
        }
    }

    void UpdateLayout()
    {
        float sw = Screen.width;
        float sh = Screen.height;

        float availW = sw - spacing * 3f;
        float availH = sh - spacing * 2f;

        float panelW = availW / 2f;
        float panelH = panelW / panelAspectRatio;

        if (panelH > availH)
        {
            panelH = availH;
            panelW = panelH * panelAspectRatio;
        }

        float totalW = panelW * 2f + spacing;
        float startX = (sw - totalW) / 2f;
        float startY = (sh - panelH) / 2f;

        SetPanelRect(leftPanel,  startX,                   startY, panelW, panelH);
        SetPanelRect(rightPanel, startX + panelW + spacing, startY, panelW, panelH);

        // Cache for OnGUI label placement
        _panelX = startX; _panelY = startY; _panelW = panelW; _panelH = panelH;
    }

    static void SetPanelRect(RawImage img, float x, float y, float w, float h)
    {
        var r = img.GetComponent<RectTransform>();
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta        = new Vector2(w, h);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void DisablePlayerSystems()
    {
        var playerTool = FindObjectOfType<PlayerToolController>();
        if (playerTool != null)
        {
            playerTool.SetToolEnabled(false);
            playerTool.SetDocumentaryPhase(true);
        }

        // Hide the sampling grid (dot cursor + sub-boxes + labels)
        var samplingGrid = FindObjectOfType<SamplingGrid>();
        if (samplingGrid != null) samplingGrid.gameObject.SetActive(false);

        var cam = FindObjectOfType<CameraController>();
        if (cam != null) cam.enabled = false;

        var turbUI = FindObjectOfType<TurbulenceIndicatorUI>();
        if (turbUI != null) turbUI.enabled = false;

        var gsUI = FindObjectOfType<GameStateUI>();
        if (gsUI != null) gsUI.enabled = false;

        var soundscape = FindObjectOfType<AmbientSoundscapeController>();
        if (soundscape != null) soundscape.FadeToSilence(1f);
    }

    IEnumerator FadeTo(float target, float dur)
    {
        float start = fadeOverlay.color.a;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            fadeOverlay.color = new Color(0f, 0f, 0f,
                Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / dur)));
            yield return null;
        }
        fadeOverlay.color = new Color(0f, 0f, 0f, target);
    }

    void OnGUI()
    {
        if (!isActive) return;

        // ── Convergence score above the left (replay) panel ───────────────────
        if (_panelW > 0f && flowSimulation != null)
        {
            if (_scoreLabelStyle == null)
            {
                Font f = Font.CreateDynamicFontFromOSFont(
                    new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, 13);
                _scoreLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 13,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap  = false
                };
                if (f != null) _scoreLabelStyle.font = f;
            }

            float divergence  = flowSimulation.CurrentDivergence;
            float convergence = Mathf.Clamp01(1f - divergence * 0.5f);
            string label = $"MEAN CONVERGENCE   {convergence:F3}";

            // GUI Y is measured from top; panel is measured from bottom
            float guiY = Screen.height - (_panelY + _panelH) - 22f;
            _scoreLabelStyle.normal.textColor = new Color(0.50f, 0.52f, 0.56f, 0.80f);
            GUI.Label(new Rect(_panelX, guiY, _panelW, 20f), label, _scoreLabelStyle);
        }

        if (!showDebugInfo) return;
        GUILayout.BeginArea(new Rect(10, 10, 280, 80));
        GUILayout.Label($"[Documentary] video ended={videoEnded}");
        GUILayout.Label($"returning={isReturningToConsole}");
        GUILayout.Label($"video time={videoPlayer.time:F1}s   F12=skip");
        GUILayout.EndArea();
    }
}
