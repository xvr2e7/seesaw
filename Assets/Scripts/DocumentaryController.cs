using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

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
    private Vector2       _replayCamPos;      // current smoothed camera position
    private Vector2       _replayCamTarget;   // target = recorded tool position

    // ── Video ─────────────────────────────────────────────────────────────────
    private VideoPlayer   videoPlayer;
    private AudioSource   videoAudio;
    private RenderTexture videoRT;
    private bool          videoEnded = false;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool isActive             = false;
    private bool isReturningToConsole = false;

    // ── Replay ────────────────────────────────────────────────────────────────
    private IReadOnlyList<InputFrame> _replayFrames;
    private int                       _replayIndex        = 0;
    private float                     _replayLoopDuration = 0f;
    private PlayerToolController      _replayTool;
    private float                     _cachedFinalScore   = 0f;
    private float                     _replayScore        = 0f; // interpolated score from recorded frames

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

        // Cache the player's final score before RestartForDocumentaryReplay() clears it
        _cachedFinalScore = gameManager != null ? gameManager.FinalScore : 0f;

        // Load the recorded session for replay
        _replayFrames       = null;
        _replayIndex        = 0;
        _replayLoopDuration = 0f;
        _replayScore        = _cachedFinalScore;
        _replayTool         = FindObjectOfType<PlayerToolController>();

        if (gameManager != null && gameManager.InputRecorder != null
            && gameManager.InputRecorder.HasRecording)
        {
            _replayFrames       = gameManager.InputRecorder.Frames;
            _replayLoopDuration = gameManager.InputRecorder.Duration;

            if (_replayTool != null)
                _replayTool.SetReplayMode(true);
        }

        // Restart simulation in looping replay mode
        if (gameManager != null)
        {
            gameManager.IsInDocumentaryReplay = true;
            gameManager.RestartForDocumentaryReplay();
        }

        // Configure the replay camera to match the main camera's projection.
        // It will follow the recorded tool position each frame rather than
        // copying the main camera's (now stationary) transform.
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            replayCamera.orthographic        = true;
            replayCamera.orthographicSize    = mainCam.orthographicSize;
            replayCamera.nearClipPlane       = mainCam.nearClipPlane;
            replayCamera.farClipPlane        = mainCam.farClipPlane;
            replayCamera.backgroundColor     = Color.black;
            replayCamera.clearFlags          = CameraClearFlags.SolidColor;
            replayCamera.cullingMask         = mainCam.cullingMask;
            replayCamera.targetTexture       = replayRT;

            // Seed camera position at the first recorded tool position (or world center)
            Vector2 startPos = (_replayFrames != null && _replayFrames.Count > 0)
                ? _replayFrames[0].worldPos
                : Vector2.zero;
            _replayCamPos    = startPos;
            _replayCamTarget = startPos;
            replayCamera.transform.position = new Vector3(startPos.x, startPos.y,
                                                          mainCam.transform.position.z);
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

        // Drive replay inputs — this updates _replayCamTarget via TickReplay
        if (_replayFrames != null && _replayTool != null && gameManager != null)
            TickReplay(gameManager.SessionTime);

        // Smooth-follow the recorded tool position, matching CameraController's behaviour
        if (replayCamera != null)
        {
            const float followSpeed = 3f;
            _replayCamPos = Vector2.Lerp(_replayCamPos, _replayCamTarget,
                                         followSpeed * Time.deltaTime);

            float z = replayCamera.transform.position.z;
            replayCamera.transform.position = new Vector3(_replayCamPos.x, _replayCamPos.y, z);
        }

        // Render the live simulation into the replay panel each frame
        if (replayCamera != null)
        {
            replayCamera.enabled = true;
            replayCamera.Render();
            replayCamera.enabled = false;
        }
    }

    void TickReplay(float sessionTime)
    {
        if (_replayFrames == null || _replayFrames.Count == 0) return;

        // Loop replay: map sessionTime onto [0, loopDuration)
        float t = _replayLoopDuration > 0f
            ? sessionTime % (_replayLoopDuration + 0.1f)
            : sessionTime;

        // Wrap index on loop restart
        if (_replayIndex > 0 && t < _replayFrames[_replayIndex].time)
            _replayIndex = 0;

        // Advance index to stay current with t
        while (_replayIndex < _replayFrames.Count - 1
               && _replayFrames[_replayIndex + 1].time <= t)
            _replayIndex++;

        // No recorded data yet for this t (before first frame)
        if (t < _replayFrames[0].time) return;

        // Interpolate between adjacent frames
        InputFrame a = _replayFrames[_replayIndex];
        InputFrame b = (_replayIndex + 1 < _replayFrames.Count)
            ? _replayFrames[_replayIndex + 1]
            : a;

        float frac = (b.time > a.time) ? (t - a.time) / (b.time - a.time) : 0f;

        Vector2 pos      = Vector2.Lerp(a.worldPos, b.worldPos, frac);
        float   radius   = Mathf.Lerp(a.radius, b.radius, frac);
        bool    held     = a.held;       // binary — don't interpolate
        float   strength = a.strength;

        // Interpolate the recorded convergence score for display above the panel
        _replayScore = Mathf.Lerp(a.convergenceScore, b.convergenceScore, frac);

        // Drive the replay camera target so it follows the tool position
        _replayCamTarget = pos;

        _replayTool.PlaybackFrame(pos, radius, held, strength);
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
        if (_panelW > 0f)
        {
            if (_scoreLabelStyle == null)
            {
                Font f = Font.CreateDynamicFontFromOSFont(
                    new string[] { "Space Mono", "Consolas", "Courier New", "Courier" }, 22);
                _scoreLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 22,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap  = false
                };
                if (f != null) _scoreLabelStyle.font = f;
            }

            // Show the convergence score that was recorded at this moment of gameplay
            string label = $"CONVERGENCE   {_replayScore * 100f:F1}%";

            // GUI Y is measured from top; panel is measured from bottom
            float guiY = Screen.height - (_panelY + _panelH) - 30f;
            _scoreLabelStyle.normal.textColor = new Color(0.50f, 0.52f, 0.56f, 0.80f);
            GUI.Label(new Rect(_panelX, guiY, _panelW, 28f), label, _scoreLabelStyle);
        }

        if (!showDebugInfo) return;
        GUILayout.BeginArea(new Rect(10, 10, 280, 80));
        GUILayout.Label($"[Documentary] video ended={videoEnded}");
        GUILayout.Label($"returning={isReturningToConsole}");
        GUILayout.Label($"video time={videoPlayer.time:F1}s   F12=skip");
        GUILayout.EndArea();
    }
}
