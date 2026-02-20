using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;
using TMPro;

/// <summary>
/// Game opening menu — Laminar Flow.
///
/// Main screen with: Start, Controls, Settings, Artist Statement, Credits, Quit.
/// Version shown in corner. Background: slow-drifting particles on very dark field.
/// Audio: underwater.wav looped as ambient.
/// Procedural cursor drawn via UI canvas element.
/// </summary>
public class ConsoleController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Scene")]
    public string laminarFlowSceneName = "Laminar Flow";
    public string versionString = "v1.4.6";

    [Header("Audio")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)]
    public float musicVolume = 0.35f;

    [Header("Transition")]
    public float fadeInDuration  = 2f;
    public float fadeOutDuration = 1.5f;

    [Header("Custom Font")]
    public TMP_FontAsset customFont;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // ─── Content ──────────────────────────────────────────────────────────────

    // Artist Statement
    private const string ARTIST_STATEMENT =
        "A work in progress. Thanks for play-testing.\n\n" +
        "See/Saw is an interactive experience that reveals the operational layer of computer vision algorithms." +
        "You become complicit in the act of automated perception and control.\n\n" +
        "The interface exposes the normally hidden apparatus of machine vision: " +
        "detection grids, classification labels, and the mechanical process of imposing order on chaos.\n\n" +
        "The simulation you inhabit places you in the role of an operator whose task " +
        "is convergence — the drawing of dispersed motion into coherence.\n\n" +
        "What counts as order? What makes a gathering legible as threat?\n\n" +
        "See/Saw offers no answer. It holds the question open.";

    private const string CONTROLS_TEXT =
        "Colored disruptions appear in the flow. Your job is to suppress them before they spread.\n\n" +
        "Move your cursor over a disruption. Hold LEFT CLICK to dampen it.\n" +
        "Watch the agents return to gray. That is the goal.\n\n" +
        "MOUSE         aim the detection field\n" +
        "LEFT CLICK    activate selected tool\n" +
        "SCROLL        resize detection area\n" +
        "ESC           pause\n\n" +
        "1  SCAN        hold to suppress  ·  limited energy, recharges\n" +
        "2  PULSE       instant burst     ·  8s cooldown, no energy cost\n" +
        "3  LOCK        freeze a cluster  ·  14s cooldown, small radius\n\n" +
        "The divergence bar (top-left) measures disorder. Keep it low.";

    private const string CREDITS_TEXT =
        "Concept & Design\n" +
        "    — Ziyan Xie\n\n" +
        "Sound\n" +
        "    Fabrice Choudry\n\n" +
        "Special Thanks\n" +   
        "    Steve Anderson";

    // ─── Runtime state ────────────────────────────────────────────────────────

    private enum MenuScreen { Main, Controls, Settings, ArtistStatement, Credits }
    private MenuScreen currentScreen = MenuScreen.Main;

    private Canvas          canvas;
    private Image           fadeOverlay;
    private GameObject      mainScreenObj;
    private GameObject      subScreenObj;
    private TextMeshProUGUI subScreenTitle;
    private TextMeshProUGUI subScreenBody;
    private GameObject      subScreenTitleGO;
    private GameObject      subScreenDividerGO;

    // Menu items (Main screen) — built dynamically in CreateMainScreen
    private string[]          menuLabels;
    private TextMeshProUGUI[] menuTexts;
    private int               hoveredItem = -1;
    private float[]           itemBrightness;
    private bool              hasSavedGame = false; // whether CONTINUE is shown

    // Settings
    private float masterVolume = 1f;
    private Image  volumeBarFill;
    private bool   isDraggingVolume = false;
    private RectTransform volumeTrackRect;

    // Particles
    private struct Particle
    {
        public Vector2 pos;   // 0-1 normalized
        public Vector2 vel;   // slow drift
        public float   alpha;
        public float   size;
        public float   phase; // for opacity breathing
    }
    private const int PARTICLE_COUNT = 70;
    private Particle[]  particles;
    private Image[]     particleImages;

    // Sun — outer halo is static; only inner core tracks cursor
    private RectTransform sunCoreRect;   // inner, follows cursor
    private Vector2       sunCoreBase = new Vector2(0f, 20f);
    private Vector2       sunCoreSmoothed;

    // Procedural cursor
    private RectTransform cursorRect;
    private Image         cursorRingImage;
    private Image         cursorDotImage;
    private float         cursorScale = 1f;
    private float         cursorTargetScale = 1f;

    // Audio
    private AudioSource bgAudioSource;

    // Sub-screen transition
    private bool isTransitioning   = false;
    private bool isSubTransitioning = false;

    // Singleton / cross-scene
    private static ConsoleController instance;
    public  static ConsoleController Instance => instance;
    private static bool returningFromDocumentary = false;
    public  static void SetReturningFromDocumentary() => returningFromDocumentary = true;

    private static bool returningFromGame = false;
    public  static void SetReturningFromGame() => returningFromGame = true;

    // Right column (shown after documentary)
    private bool           showRightColumn = false;
    private GameObject     rightColumnObj;
    private TextMeshProUGUI bestRunText;      // shows score on hover
    private TextMeshProUGUI watchText;
    private int            rightHoveredItem  = -1; // 0=BEST RUN, 1=WATCH
    private float[]        rightItemBrightness = new float[2];
    private bool           rightColumnVisible  = false;

    // In-console video playback screen
    [Header("Video")]
    public string videoFileName = "laminar_demo.mp4";
    private GameObject     videoScreenObj;
    private RawImage       videoRawImage;
    private RenderTexture  videoRT;
    private VideoPlayer    videoPlayer;
    private AudioSource    videoAudio;
    private bool           isPlayingVideo = false;

    // Font (resolved in Start)
    private TMP_FontAsset resolvedFont;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        instance = this;
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
    }

    void Start()
    {
        ResolveFont();
        SetupAudio();

        // Show right column if the player has ever completed a run
        showRightColumn = GameManager.HasBestScore();

        if (returningFromDocumentary)
        {
            returningFromDocumentary = false;
            CreateUI();
            Cursor.visible = false;
            StartCoroutine(FadeIn(fadeInDuration));
        }
        else if (returningFromGame)
        {
            returningFromGame = false;
            CreateUI();
            Cursor.visible = false;
            StartCoroutine(FadeIn(fadeInDuration));
        }
        else
        {
            CreateUI();
            Cursor.visible = false;
            StartCoroutine(IntroSequence());
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPlayingVideo)
            {
                StartCoroutine(StopVideoAndReturn());
                return;
            }
            if (currentScreen != MenuScreen.Main)
                ShowScreen(MenuScreen.Main);
            else
                QuitGame();
            return;
        }

        UpdateParticles();
        UpdateSunCore();
        UpdateCursor();

        if (!isTransitioning && !isSubTransitioning)
        {
            if (currentScreen == MenuScreen.Main)
            {
                UpdateMenuHover();
                UpdateMenuClick();
                UpdateVolumeSlider();
                if (rightColumnVisible)
                {
                    UpdateRightColumnHover();
                    UpdateRightColumnClick();
                }
            }
            else
            {
                UpdateVolumeSlider();
            }
        }

        ApplyVolumeToAudio();
    }

    void OnDestroy()
    {
        Cursor.visible = true;
    }

    // ─── Font ─────────────────────────────────────────────────────────────────

    void ResolveFont()
    {
        if (customFont != null)
        {
            resolvedFont = customFont;
            return;
        }
        // Use TMP's bundled LiberationSans SDF (always present after TMP import)
        resolvedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    // ─── Audio ────────────────────────────────────────────────────────────────

    void SetupAudio()
    {
        bgAudioSource = gameObject.AddComponent<AudioSource>();
        bgAudioSource.clip        = backgroundMusic;
        bgAudioSource.loop        = true;
        bgAudioSource.volume      = 0f; // fades in
        bgAudioSource.spatialBlend = 0f;
        bgAudioSource.playOnAwake = false;
        if (backgroundMusic != null) bgAudioSource.Play();
    }

    void ApplyVolumeToAudio()
    {
        if (bgAudioSource != null)
            bgAudioSource.volume = Mathf.MoveTowards(bgAudioSource.volume, masterVolume * musicVolume, Time.deltaTime * 0.5f);
    }

    // ─── UI Construction ──────────────────────────────────────────────────────

    void CreateUI()
    {
        // Root canvas
        var canvasGO = new GameObject("MenuCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Background
        CreateBackground(canvasGO.transform);

        // Particles
        CreateParticles(canvasGO.transform);

        // Main menu screen
        mainScreenObj = CreateMainScreen(canvasGO.transform);

        // Sub screen (hidden initially)
        subScreenObj = CreateSubScreen(canvasGO.transform);
        subScreenObj.SetActive(false);

        // Video playback screen (hidden until WATCH is clicked)
        CreateVideoScreen(canvasGO.transform);

        // Procedural cursor (topmost)
        CreateCursor(canvasGO.transform);

        // Fade overlay (above everything except cursor)
        fadeOverlay = CreateFullscreenImage(canvasGO.transform, "FadeOverlay", Color.black);
        fadeOverlay.raycastTarget = false;

        // Move cursor to top of hierarchy
        cursorRect.parent.SetAsLastSibling();

        Canvas.ForceUpdateCanvases();
    }

    void CreateVideoScreen(Transform parent)
    {
        videoScreenObj = new GameObject("VideoScreen");
        videoScreenObj.transform.SetParent(parent, false);
        var cg = videoScreenObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        var bgImg = videoScreenObj.AddComponent<Image>();
        bgImg.color = Color.black;
        bgImg.raycastTarget = true;
        var bgRect = videoScreenObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // RawImage to display the render texture
        var rawGO  = new GameObject("VideoImage");
        rawGO.transform.SetParent(videoScreenObj.transform, false);
        videoRawImage = rawGO.AddComponent<RawImage>();
        videoRawImage.color = Color.white;
        videoRawImage.raycastTarget = false;
        var rawRect = rawGO.GetComponent<RectTransform>();
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.offsetMin = Vector2.zero;
        rawRect.offsetMax = Vector2.zero;

        // VideoPlayer
        var vpGO    = new GameObject("VideoPlayer");
        vpGO.transform.SetParent(videoScreenObj.transform, false);
        videoPlayer = vpGO.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake     = false;
        videoPlayer.isLooping       = false;
        videoPlayer.renderMode      = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoAudio  = vpGO.AddComponent<AudioSource>();
        videoAudio.playOnAwake = false;
        videoPlayer.SetTargetAudioSource(0, videoAudio);
        videoPlayer.loopPointReached += _ => StartCoroutine(StopVideoAndReturn());
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, videoFileName);
        videoPlayer.url = path;
        videoPlayer.Prepare();

        videoScreenObj.SetActive(false);
    }

    private Texture2D sunGlowTexture;

    void CreateBackground(Transform parent)
    {
        // Base dark fill
        var bg = CreateFullscreenImage(parent, "Background", new Color(0.008f, 0.008f, 0.010f, 1f));
        bg.raycastTarget = false;

        sunGlowTexture = CreateSunTexture(512, 512);

        // Outer halo — fixed, does not follow cursor
        AddSunLayer(parent, "SunHalo", sunCoreBase, new Vector2(820f, 820f), new Color(0.40f, 0.22f, 0.06f, 0.11f));

        // Inner core — tracks cursor
        sunCoreSmoothed = sunCoreBase;
        sunCoreRect = AddSunLayer(parent, "SunCore", sunCoreBase, new Vector2(320f, 320f), new Color(0.62f, 0.38f, 0.12f, 0.26f));
    }

    RectTransform AddSunLayer(Transform parent, string name, Vector2 pos, Vector2 size, Color tint)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<RawImage>();
        img.texture       = sunGlowTexture;
        img.raycastTarget = false;

        var mat = new Material(Shader.Find("UI/Default"));
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite",   0);
        img.material = mat;
        img.color    = tint;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta        = size;
        return rect;
    }

    Texture2D CreateSunTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        // Trilinear + high res eliminates banding from radial gradients
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.anisoLevel = 9;
        var pixels = new Color[w * h];

        float invW = 1f / (w - 1);
        float invH = 1f / (h - 1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x * invW * 2f - 1f;
                float v = y * invH * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);

                // Wide gaussian: fuzzy sphere, bright centre, dissolves well before edge.
                // No power exponent — keeps the curve monotone and banding-free.
                float g = Mathf.Exp(-d * d / (2f * 0.45f * 0.45f));
                // Clamp to zero past 90% radius so texture boundary is always black
                g *= Mathf.Clamp01((1f - d / 0.9f) * 8f);

                pixels[y * w + x] = new Color(g, g, g, g);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true); // generateMipMaps=true — mips smooth out banding further
        return tex;
    }

    Image CreateFullscreenImage(Transform parent, string name, Color color)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img  = go.AddComponent<Image>();
        img.color = color;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return img;
    }

    // ─── Particles ────────────────────────────────────────────────────────────

    void CreateParticles(Transform parent)
    {
        particles      = new Particle[PARTICLE_COUNT];
        particleImages = new Image[PARTICLE_COUNT];

        var container = new GameObject("Particles");
        container.transform.SetParent(parent, false);
        var cRect = container.AddComponent<RectTransform>();
        cRect.anchorMin = Vector2.zero;
        cRect.anchorMax = Vector2.one;
        cRect.offsetMin = Vector2.zero;
        cRect.offsetMax = Vector2.zero;

        // Add CanvasGroup so we can fade the whole layer
        container.AddComponent<CanvasGroup>().blocksRaycasts = false;

        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            particles[i] = RandomParticle();

            var go   = new GameObject($"P{i}");
            go.transform.SetParent(container.transform, false);
            var img  = go.AddComponent<Image>();
            img.raycastTarget = false;

            // Tiny circle texture via a 1x1 white pixel (UI Image with aspect-fit looks like a dot)
            img.color = new Color(0.7f, 0.75f, 0.8f, 0f);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot     = new Vector2(0.5f, 0.5f);
            float sz    = particles[i].size;
            r.sizeDelta = new Vector2(sz, sz);

            particleImages[i] = img;
        }
    }

    Particle RandomParticle()
    {
        // Bias spawn toward center so dust clouds around the sun
        float angle  = Random.value * Mathf.PI * 2f;
        float radius = Mathf.Pow(Random.value, 0.5f) * 0.42f; // 0..0.42 from center
        Vector2 pos  = new Vector2(0.5f + Mathf.Cos(angle) * radius,
                                   0.5f + Mathf.Sin(angle) * radius);

        // Mostly random walk with only a faint outward tendency
        float   speed   = Random.Range(0.0004f, 0.002f);
        float   randAngle = Random.value * Mathf.PI * 2f;
        Vector2 outward = (pos - new Vector2(0.5f, 0.5f)).normalized;
        Vector2 random  = new Vector2(Mathf.Cos(randAngle), Mathf.Sin(randAngle));
        // 25% outward bias, 75% pure random direction
        Vector2 vel     = Vector2.Lerp(random, outward, 0.25f).normalized * speed;

        return new Particle
        {
            pos   = pos,
            vel   = vel,
            alpha = Random.Range(0.04f, 0.22f),
            size  = Random.Range(1.2f, 3.5f),
            phase = Random.value * Mathf.PI * 2f
        };
    }

    // Warm dust color near sun core
    private static readonly Color DustWarm = new Color(0.82f, 0.68f, 0.42f, 1f);
    // Cool mote color at outer edge
    private static readonly Color DustCool = new Color(0.55f, 0.58f, 0.65f, 1f);

    void UpdateSunCore()
    {
        if (sunCoreRect == null) return;

        // Map mouse to canvas-space offset from center, then scale way down
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float cw = canvasRect.rect.width;
        float ch = canvasRect.rect.height;

        // Normalised mouse: -0.5..0.5 from screen center
        float mx = (Input.mousePosition.x / Screen.width)  - 0.5f;
        float my = (Input.mousePosition.y / Screen.height) - 0.5f;

        // Drift range and tracking speed
        const float maxDrift = 36f;
        Vector2 target = sunCoreBase + new Vector2(mx, my) * maxDrift * 2f;

        // Responsive but still smooth — not instant
        sunCoreSmoothed = Vector2.Lerp(sunCoreSmoothed, target, Time.deltaTime * 3.5f);
        sunCoreRect.anchoredPosition = sunCoreSmoothed;
    }

    void UpdateParticles()
    {
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float cw = canvasRect.rect.width;
        float ch = canvasRect.rect.height;
        float t  = Time.time;

        for (int i = 0; i < PARTICLE_COUNT; i++)
        {
            ref Particle p = ref particles[i];

            p.pos += p.vel * Time.deltaTime * 60f;

            // Respawn at center when drifted too far out
            float distFromCenter = Vector2.Distance(p.pos, new Vector2(0.5f, 0.5f));
            if (distFromCenter > 0.62f)
            {
                p = RandomParticle();
            }

            // Distance-based color: warm at center, cool further out
            float warmT  = Mathf.Clamp01(1f - distFromCenter / 0.45f);
            Color tint   = Color.Lerp(DustCool, DustWarm, warmT);

            // Breathing alpha — slower near core (heavier dust)
            float breatheSpeed = Mathf.Lerp(0.25f, 0.55f, 1f - warmT);
            float breathe      = Mathf.Sin(t * breatheSpeed + p.phase) * 0.5f + 0.5f;
            float alpha        = p.alpha * (0.4f + 0.6f * breathe);
            // Particles very close to center are nearly invisible (occluded by glow)
            float coreFade     = Mathf.Clamp01((distFromCenter - 0.04f) / 0.08f);
            alpha *= coreFade;

            var img = particleImages[i];
            img.color = new Color(tint.r, tint.g, tint.b, alpha);

            var rect = img.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(p.pos.x * cw - cw * 0.5f, p.pos.y * ch - ch * 0.5f);
        }
    }

    // ─── Main Screen ──────────────────────────────────────────────────────────

    GameObject CreateMainScreen(Transform parent)
    {
        var go = new GameObject("MainScreen");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(go.transform, false);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "See/Saw";
        titleTMP.font      = resolvedFont;
        titleTMP.fontSize  = 60f;
        titleTMP.fontStyle = FontStyles.Normal;
        titleTMP.color     = new Color(0.88f, 0.88f, 0.88f, 1f);
        titleTMP.characterSpacing = 18f;
        titleTMP.alignment = TextAlignmentOptions.Left;
        titleTMP.raycastTarget = false;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0f, 0f);
        titleRect.anchorMax        = new Vector2(1f, 1f);
        titleRect.pivot            = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(160f, -120f);
        titleRect.sizeDelta        = new Vector2(-200f, 80f);

        // Subtitle / tagline
        var subGO   = new GameObject("Subtitle");
        subGO.transform.SetParent(go.transform, false);
        var subTMP  = subGO.AddComponent<TextMeshProUGUI>();
        subTMP.text     = "simulating machine vision";
        subTMP.font     = resolvedFont;
        subTMP.fontSize = 16f;
        subTMP.color    = new Color(0.45f, 0.47f, 0.5f, 1f);
        subTMP.characterSpacing = 4f;
        subTMP.alignment = TextAlignmentOptions.Left;
        subTMP.raycastTarget = false;
        var subRect = subGO.GetComponent<RectTransform>();
        subRect.anchorMin        = new Vector2(0f, 0f);
        subRect.anchorMax        = new Vector2(1f, 1f);
        subRect.pivot            = new Vector2(0f, 1f);
        subRect.anchoredPosition = new Vector2(163f, -196f);
        subRect.sizeDelta        = new Vector2(-200f, 30f);

        // Divider line
        CreateDivider(go.transform, new Vector2(160f, -224f), 280f);

        // Menu items — label list depends on whether a save exists
        hasSavedGame = GameManager.HasSavedGame();
        if (hasSavedGame)
            menuLabels = new string[] { "CONTINUE", "NEW GAME", "CONTROLS", "SETTINGS", "ARTIST STATEMENT", "CREDITS", "QUIT" };
        else
            menuLabels = new string[] { "START", "CONTROLS", "SETTINGS", "ARTIST STATEMENT", "CREDITS", "QUIT" };

        menuTexts      = new TextMeshProUGUI[menuLabels.Length];
        itemBrightness = new float[menuLabels.Length];

        for (int i = 0; i < menuLabels.Length; i++)
        {
            float yOffset = -284f - i * 52f;
            menuTexts[i] = CreateMenuItem(go.transform, menuLabels[i], new Vector2(160f, yOffset), i);
        }

        // Version label (bottom-left)
        var verGO  = new GameObject("Version");
        verGO.transform.SetParent(go.transform, false);
        var verTMP = verGO.AddComponent<TextMeshProUGUI>();
        verTMP.text      = versionString;
        verTMP.font      = resolvedFont;
        verTMP.fontSize  = 13f;
        verTMP.color     = new Color(0.3f, 0.32f, 0.35f, 1f);
        verTMP.alignment = TextAlignmentOptions.Left;
        verTMP.raycastTarget = false;
        var verRect = verGO.GetComponent<RectTransform>();
        verRect.anchorMin        = new Vector2(0f, 0f);
        verRect.anchorMax        = new Vector2(0f, 0f);
        verRect.pivot            = new Vector2(0f, 0f);
        verRect.anchoredPosition = new Vector2(30f, 22f);
        verRect.sizeDelta        = new Vector2(200f, 24f);

        // Right column — persistent once player has completed a run
        if (showRightColumn)
            CreateRightColumn(go.transform);

        return go;
    }

    // Right column layout — mirrored from the left:
    // Left column: anchor (0,1), x=160 from left edge, width 280, text left-aligned
    // Right column: anchor (1,1), x=-160 from right edge, width 280, text right-aligned
    private const float RC_MARGIN   = -160f; // anchoredPosition x from right anchor
    private const float RC_WIDTH    = 280f;
    private const float RC_DIV_Y    = -224f; // same y as left divider
    private const float RC_ITEM0_Y  = -284f; // BEST RUN — same y as left item 0
    private const float RC_ITEM1_Y  = -336f; // WATCH    — same y as left item 1

    void CreateRightColumn(Transform parent)
    {
        // Divider — right edge at x=-160, extends leftward 280px, same y as left divider
        var divGO  = new GameObject("RightColumnDivider");
        divGO.transform.SetParent(parent, false);
        var divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(0.22f, 0.24f, 0.27f, 1f);
        divImg.raycastTarget = false;
        var divRect = divGO.GetComponent<RectTransform>();
        divRect.anchorMin        = new Vector2(1f, 1f);
        divRect.anchorMax        = new Vector2(1f, 1f);
        divRect.pivot            = new Vector2(1f, 0.5f); // right-anchored
        divRect.anchoredPosition = new Vector2(RC_MARGIN, RC_DIV_Y);
        divRect.sizeDelta        = new Vector2(RC_WIDTH, 1f);

        // Row 0 — BEST RUN + inline score (fades in on hover)
        bestRunText = CreateRightItem(parent, "BEST RUN", RC_ITEM0_Y);

        // Score — sits to the LEFT of the label (in screen space: label is right-aligned,
        // score appears as a dim suffix fading in on hover, right-aligned in same row)
        var scoreGO  = new GameObject("BestScore");
        scoreGO.transform.SetParent(parent, false);
        var scoreTMP = scoreGO.AddComponent<TextMeshProUGUI>();
        float best   = GameManager.GetBestScore();
        scoreTMP.text             = GameManager.HasBestScore()
            ? $"{Mathf.RoundToInt(best * 100f):D3}"
            : "—";
        scoreTMP.font             = resolvedFont;
        scoreTMP.fontSize         = 14f;
        scoreTMP.color            = new Color(0.38f, 0.55f, 0.45f, 0f); // invisible until hover
        scoreTMP.characterSpacing = 2f;
        scoreTMP.alignment        = TextAlignmentOptions.Right;
        scoreTMP.raycastTarget    = false;
        var scoreRect = scoreGO.GetComponent<RectTransform>();
        // Score appears just left of the label.
        // Label: pivot right, anchoredPos (RC_MARGIN, y), width RC_WIDTH
        //   → label's left screen edge is at RC_MARGIN - RC_WIDTH from the right anchor
        // Score: pivot right, right edge flush with label's left edge
        scoreRect.anchorMin        = new Vector2(1f, 1f);
        scoreRect.anchorMax        = new Vector2(1f, 1f);
        scoreRect.pivot            = new Vector2(1f, 0.5f);
        scoreRect.anchoredPosition = new Vector2(RC_MARGIN - RC_WIDTH - 10f, RC_ITEM0_Y);
        scoreRect.sizeDelta        = new Vector2(80f, 40f);
        _bestScoreTMP = scoreTMP;

        // Row 1 — WATCH
        watchText = CreateRightItem(parent, "WATCH", RC_ITEM1_Y);

        rightColumnVisible = true;
        rightItemBrightness[0] = 0f;
        rightItemBrightness[1] = 0f;
    }

    private TextMeshProUGUI _bestScoreTMP;

    // Items are right-anchored, right-aligned, right edge at x = RC_MARGIN from right
    TextMeshProUGUI CreateRightItem(Transform parent, string label, float anchoredY)
    {
        var go   = new GameObject($"RightItem_{label}");
        go.transform.SetParent(parent, false);
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = label;
        tmp.font             = resolvedFont;
        tmp.fontSize         = 18f;
        tmp.color            = new Color(0.45f, 0.47f, 0.5f, 1f);
        tmp.characterSpacing = 3f;
        tmp.alignment        = TextAlignmentOptions.Right;
        tmp.raycastTarget    = true;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(1f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(RC_MARGIN, anchoredY);
        rect.sizeDelta        = new Vector2(RC_WIDTH, 40f);
        return tmp;
    }

    GameObject CreateDivider(Transform parent, Vector2 anchoredPos, float width)
    {
        var go   = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var img  = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.24f, 0.27f, 1f);
        img.raycastTarget = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(0f, 1f);
        rect.pivot            = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(width, 1f);
        return go;
    }

    TextMeshProUGUI CreateMenuItem(Transform parent, string label, Vector2 anchoredPos, int index)
    {
        var go   = new GameObject($"MenuItem_{index}");
        go.transform.SetParent(parent, false);
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = label;
        tmp.font     = resolvedFont;
        tmp.fontSize = 18f;
        tmp.color    = new Color(0.45f, 0.47f, 0.5f, 1f);
        tmp.characterSpacing = 3f;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = true;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(0.5f, 1f);
        rect.pivot            = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(0f, 40f);
        return tmp;
    }

    // ─── Sub Screen ───────────────────────────────────────────────────────────

    GameObject CreateSubScreen(Transform parent)
    {
        var go = new GameObject("SubScreen");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Back hint
        var backGO  = new GameObject("BackHint");
        backGO.transform.SetParent(go.transform, false);
        var backTMP = backGO.AddComponent<TextMeshProUGUI>();
        backTMP.text      = "[ ESC ] BACK";
        backTMP.font      = resolvedFont;
        backTMP.fontSize  = 11f;
        backTMP.color     = new Color(0.3f, 0.32f, 0.35f, 1f);
        backTMP.characterSpacing = 2f;
        backTMP.alignment = TextAlignmentOptions.Left;
        backTMP.raycastTarget = false;
        var backRect = backGO.GetComponent<RectTransform>();
        backRect.anchorMin        = new Vector2(0f, 0f);
        backRect.anchorMax        = new Vector2(0f, 0f);
        backRect.pivot            = new Vector2(0f, 0f);
        backRect.anchoredPosition = new Vector2(30f, 22f);
        backRect.sizeDelta        = new Vector2(200f, 24f);

        // Title
        var titleGO  = new GameObject("SubTitle");
        titleGO.transform.SetParent(go.transform, false);
        subScreenTitleGO = titleGO;
        subScreenTitle = titleGO.AddComponent<TextMeshProUGUI>();
        subScreenTitle.font      = resolvedFont;
        subScreenTitle.fontSize  = 26f;
        subScreenTitle.color     = new Color(0.78f, 0.78f, 0.78f, 1f);
        subScreenTitle.characterSpacing = 8f;
        subScreenTitle.alignment = TextAlignmentOptions.Left;
        subScreenTitle.raycastTarget = false;
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0f, 1f);
        titleRect.anchorMax        = new Vector2(1f, 1f);
        titleRect.pivot            = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(160f, -120f);
        titleRect.sizeDelta        = new Vector2(-200f, 50f);

        // Divider
        subScreenDividerGO = CreateDivider(go.transform, new Vector2(160f, -182f), 360f);

        // Body text
        var bodyGO  = new GameObject("SubBody");
        bodyGO.transform.SetParent(go.transform, false);
        subScreenBody = bodyGO.AddComponent<TextMeshProUGUI>();
        subScreenBody.font      = resolvedFont;
        subScreenBody.fontSize  = 16f;
        subScreenBody.color     = new Color(0.55f, 0.57f, 0.6f, 1f);
        subScreenBody.alignment = TextAlignmentOptions.Left;
        subScreenBody.lineSpacing = 8f;
        subScreenBody.raycastTarget = false;
        var bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin        = new Vector2(0f, 0f);
        bodyRect.anchorMax        = new Vector2(0.6f, 1f);
        bodyRect.offsetMin        = new Vector2(160f, 80f);
        bodyRect.offsetMax        = new Vector2(-40f, -210f);

        // Settings volume row (hidden unless Settings screen)
        BuildSettingsRow(go.transform);

        return go;
    }

    // ─── Settings ─────────────────────────────────────────────────────────────

    private GameObject settingsRowObj;

    void BuildSettingsRow(Transform parent)
    {
        settingsRowObj = new GameObject("SettingsRow");
        settingsRowObj.transform.SetParent(parent, false);
        settingsRowObj.SetActive(false);

        var rowRect = settingsRowObj.AddComponent<RectTransform>();
        rowRect.anchorMin        = new Vector2(0f, 1f);
        rowRect.anchorMax        = new Vector2(0f, 1f);
        rowRect.pivot            = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(160f, -220f);
        rowRect.sizeDelta        = new Vector2(400f, 44f);

        // Label
        var labelGO  = new GameObject("VolumeLabel");
        labelGO.transform.SetParent(settingsRowObj.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = "AUDIO";
        labelTMP.font      = resolvedFont;
        labelTMP.fontSize  = 12f;
        labelTMP.color     = new Color(0.5f, 0.52f, 0.55f, 1f);
        labelTMP.characterSpacing = 3f;
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelTMP.raycastTarget = false;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot     = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 0f);
        labelRect.sizeDelta = new Vector2(80f, 30f);

        // Track background
        var trackGO  = new GameObject("VolumeTrack");
        trackGO.transform.SetParent(settingsRowObj.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.1f, 0.11f, 0.12f, 1f);
        trackImg.raycastTarget = true;
        volumeTrackRect = trackGO.GetComponent<RectTransform>();
        volumeTrackRect.anchorMin        = new Vector2(0f, 0.5f);
        volumeTrackRect.anchorMax        = new Vector2(0f, 0.5f);
        volumeTrackRect.pivot            = new Vector2(0f, 0.5f);
        volumeTrackRect.anchoredPosition = new Vector2(100f, 0f);
        volumeTrackRect.sizeDelta        = new Vector2(240f, 3f);

        // Fill
        var fillGO  = new GameObject("VolumeFill");
        fillGO.transform.SetParent(trackGO.transform, false);
        volumeBarFill = fillGO.AddComponent<Image>();
        volumeBarFill.color = new Color(0.5f, 0.6f, 0.65f, 0.8f);
        volumeBarFill.raycastTarget = false;
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot     = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = new Vector2(240f * masterVolume, 0f);

        // Value label
        var valGO   = new GameObject("VolumeValue");
        valGO.transform.SetParent(settingsRowObj.transform, false);
        var valTMP  = valGO.AddComponent<TextMeshProUGUI>();
        valTMP.font     = resolvedFont;
        valTMP.fontSize = 11f;
        valTMP.color    = new Color(0.4f, 0.42f, 0.45f, 1f);
        valTMP.alignment = TextAlignmentOptions.Left;
        valTMP.raycastTarget = false;
        var valRect = valGO.GetComponent<RectTransform>();
        valRect.anchorMin        = new Vector2(0f, 0.5f);
        valRect.anchorMax        = new Vector2(0f, 0.5f);
        valRect.pivot            = new Vector2(0f, 0.5f);
        valRect.anchoredPosition = new Vector2(354f, 0f);
        valRect.sizeDelta        = new Vector2(50f, 30f);

        // Store ref for update
        _volumeValueTMP = valTMP;
        UpdateVolumeUI();
    }

    private TextMeshProUGUI _volumeValueTMP;

    void UpdateVolumeUI()
    {
        if (volumeBarFill == null) return;
        var fillRect = volumeBarFill.GetComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(240f * masterVolume, 0f);
        if (_volumeValueTMP != null)
            _volumeValueTMP.text = Mathf.RoundToInt(masterVolume * 100f) + "%";
    }

    void UpdateVolumeSlider()
    {
        if (currentScreen != MenuScreen.Settings) return;
        if (volumeTrackRect == null) return;

        bool mouseDown = Input.GetMouseButton(0);
        bool mouseUp   = Input.GetMouseButtonUp(0);

        Vector2 localPoint;
        bool inTrack = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            volumeTrackRect, Input.mousePosition, null, out localPoint);

        if (mouseDown && inTrack) isDraggingVolume = true;
        if (mouseUp) { isDraggingVolume = false; PlayerPrefs.SetFloat("MasterVolume", masterVolume); }

        if (isDraggingVolume)
        {
            float halfW = volumeTrackRect.rect.width * 0.5f;
            float t     = Mathf.InverseLerp(-halfW, halfW, localPoint.x);
            masterVolume = Mathf.Clamp01(t);
            UpdateVolumeUI();
        }
    }

    // ─── Cursor ───────────────────────────────────────────────────────────────

    void CreateCursor(Transform parent)
    {
        var go   = new GameObject("Cursor");
        go.transform.SetParent(parent, false);
        cursorRect = go.AddComponent<RectTransform>();
        cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
        cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot     = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(24f, 24f);

        // Thin ring
        var ringGO  = new GameObject("Ring");
        ringGO.transform.SetParent(go.transform, false);
        cursorRingImage = ringGO.AddComponent<Image>();
        cursorRingImage.color = new Color(0.75f, 0.78f, 0.82f, 0.75f);
        cursorRingImage.raycastTarget = false;
        var ringRect = ringGO.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = Vector2.zero;
        ringRect.offsetMax = Vector2.zero;

        // Center dot
        var dotGO  = new GameObject("Dot");
        dotGO.transform.SetParent(go.transform, false);
        cursorDotImage = dotGO.AddComponent<Image>();
        cursorDotImage.color = new Color(0.85f, 0.87f, 0.9f, 0.9f);
        cursorDotImage.raycastTarget = false;
        var dotRect = dotGO.GetComponent<RectTransform>();
        dotRect.anchorMin        = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax        = new Vector2(0.5f, 0.5f);
        dotRect.pivot            = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta        = new Vector2(3f, 3f);

        // Draw ring as four thin lines via child Images (cross hair style)
        CreateCursorArm(go.transform, new Vector2(0f, 1f),   new Vector2(2f, 6f),  new Vector2(0f, 4f));
        CreateCursorArm(go.transform, new Vector2(0f, -1f),  new Vector2(2f, 6f),  new Vector2(0f, -4f));
        CreateCursorArm(go.transform, new Vector2(-1f, 0f),  new Vector2(6f, 2f),  new Vector2(-4f, 0f));
        CreateCursorArm(go.transform, new Vector2(1f, 0f),   new Vector2(6f, 2f),  new Vector2(4f, 0f));

        // Hide the ring image since we use arms instead
        cursorRingImage.enabled = false;
    }

    void CreateCursorArm(Transform parent, Vector2 dir, Vector2 size, Vector2 offset)
    {
        var go   = new GameObject("Arm");
        go.transform.SetParent(parent, false);
        var img  = go.AddComponent<Image>();
        img.color = new Color(0.8f, 0.82f, 0.86f, 0.8f);
        img.raycastTarget = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta        = size;
    }

    void UpdateCursor()
    {
        if (cursorRect == null) return;

        // Convert mouse position to canvas space
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, Input.mousePosition, null, out localPoint);

        cursorRect.anchoredPosition = localPoint;

        // Scale breathe: bigger on hover
        bool overItem = (hoveredItem >= 0);
        cursorTargetScale = overItem ? 1.4f : 1f;
        cursorScale = Mathf.Lerp(cursorScale, cursorTargetScale, Time.deltaTime * 8f);
        cursorRect.localScale = Vector3.one * cursorScale;

        // Subtle opacity pulse
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.1f);
        cursorDotImage.color = new Color(0.85f, 0.87f, 0.9f, 0.9f * pulse);
    }

    // ─── Menu Hover / Click ───────────────────────────────────────────────────

    void UpdateMenuHover()
    {
        hoveredItem = -1;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        for (int i = 0; i < menuTexts.Length; i++)
        {
            if (menuTexts[i] == null) continue;
            var rect = menuTexts[i].GetComponent<RectTransform>();
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rect, Input.mousePosition, null, out local))
            {
                if (rect.rect.Contains(local))
                {
                    hoveredItem = i;
                }
            }
        }

        // Animate brightness
        float dt = Time.deltaTime * 6f;
        for (int i = 0; i < menuTexts.Length; i++)
        {
            float target = (i == hoveredItem) ? 1f : 0f;
            itemBrightness[i] = Mathf.Lerp(itemBrightness[i], target, dt);
            menuTexts[i].color = Color.Lerp(
                new Color(0.38f, 0.4f, 0.43f, 1f),
                new Color(0.92f, 0.93f, 0.95f, 1f),
                itemBrightness[i]);
        }
    }

    void UpdateMenuClick()
    {
        if (!Input.GetMouseButtonDown(0) || hoveredItem < 0) return;

        if (hasSavedGame)
        {
            // CONTINUE / NEW GAME / CONTROLS / SETTINGS / ARTIST STATEMENT / CREDITS / QUIT
            switch (hoveredItem)
            {
                case 0: StartCoroutine(TransitionToLaminarFlow(resume: true));  break; // CONTINUE
                case 1: StartCoroutine(TransitionToLaminarFlow(resume: false)); break; // NEW GAME
                case 2: ShowScreen(MenuScreen.Controls);                        break;
                case 3: ShowScreen(MenuScreen.Settings);                        break;
                case 4: ShowScreen(MenuScreen.ArtistStatement);                 break;
                case 5: ShowScreen(MenuScreen.Credits);                         break;
                case 6: QuitGame();                                             break;
            }
        }
        else
        {
            // START / CONTROLS / SETTINGS / ARTIST STATEMENT / CREDITS / QUIT
            switch (hoveredItem)
            {
                case 0: StartCoroutine(TransitionToLaminarFlow(resume: false)); break;
                case 1: ShowScreen(MenuScreen.Controls);                        break;
                case 2: ShowScreen(MenuScreen.Settings);                        break;
                case 3: ShowScreen(MenuScreen.ArtistStatement);                 break;
                case 4: ShowScreen(MenuScreen.Credits);                         break;
                case 5: QuitGame();                                             break;
            }
        }
    }

    // ─── Right Column ─────────────────────────────────────────────────────────

    void UpdateRightColumnHover()
    {
        var items = new TextMeshProUGUI[] { bestRunText, watchText };
        rightHoveredItem = -1;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null) continue;
            var rect = items[i].GetComponent<RectTransform>();
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, null, out local))
            {
                if (rect.rect.Contains(local))
                    rightHoveredItem = i;
            }
        }

        float dt = Time.deltaTime * 6f;
        for (int i = 0; i < items.Length; i++)
        {
            float target = (i == rightHoveredItem) ? 1f : 0f;
            rightItemBrightness[i] = Mathf.Lerp(rightItemBrightness[i], target, dt);
            items[i].color = Color.Lerp(
                new Color(0.38f, 0.4f, 0.43f, 1f),
                new Color(0.92f, 0.93f, 0.95f, 1f),
                rightItemBrightness[i]);
        }

        // Fade score line in/out on BEST RUN hover
        if (_bestScoreTMP != null)
        {
            float scoreTarget = (rightHoveredItem == 0) ? 1f : 0f;
            Color c = _bestScoreTMP.color;
            c.a = Mathf.Lerp(c.a, scoreTarget, Time.deltaTime * 6f);
            _bestScoreTMP.color = c;
        }
    }

    void UpdateRightColumnClick()
    {
        if (!Input.GetMouseButtonDown(0) || rightHoveredItem != 1) return;
        StartCoroutine(PlayVideoCoroutine());
    }

    IEnumerator PlayVideoCoroutine()
    {
        if (isTransitioning || isPlayingVideo) yield break;
        isTransitioning = true;

        // Fade to black
        yield return StartCoroutine(FadeOverlayTo(1f, fadeOutDuration));

        // Build/resize render texture to match screen
        if (videoRT != null) { videoRT.Release(); Object.Destroy(videoRT); }
        videoRT = new RenderTexture(Screen.width, Screen.height, 0);
        videoPlayer.targetTexture = videoRT;
        videoRawImage.texture     = videoRT;

        // Swap screens while black
        mainScreenObj.SetActive(false);
        videoScreenObj.SetActive(true);
        var vcg = videoScreenObj.GetComponent<CanvasGroup>();
        vcg.alpha = 1f;
        vcg.blocksRaycasts = true;

        isPlayingVideo = true;
        videoPlayer.Play();

        // Fade from black
        yield return StartCoroutine(FadeOverlayTo(0f, fadeInDuration));

        isTransitioning = false;
    }

    IEnumerator StopVideoAndReturn()
    {
        if (!isPlayingVideo) yield break;
        isPlayingVideo  = false;
        isTransitioning = true;

        videoPlayer.Stop();

        // Fade to black
        yield return StartCoroutine(FadeOverlayTo(1f, fadeOutDuration));

        // Swap back while black
        var vcg = videoScreenObj.GetComponent<CanvasGroup>();
        vcg.alpha = 0f;
        vcg.blocksRaycasts = false;
        videoScreenObj.SetActive(false);
        mainScreenObj.SetActive(true);

        // Fade in menu
        yield return StartCoroutine(FadeOverlayTo(0f, fadeInDuration));

        isTransitioning = false;
    }

    // ─── Screen Switching ─────────────────────────────────────────────────────

    void ShowScreen(MenuScreen screen)
    {
        currentScreen = screen;
        StartCoroutine(SwitchScreens(screen));
    }

    IEnumerator SwitchScreens(MenuScreen screen)
    {
        isSubTransitioning = true;
        float dur = 0.3f;

        // Fade out current visible screen
        if (screen == MenuScreen.Main)
        {
            // Sub → Main
            var cg = subScreenObj.GetComponent<CanvasGroup>();
            yield return StartCoroutine(FadeCanvasGroup(cg, 0f, dur));
            subScreenObj.SetActive(false);
            mainScreenObj.SetActive(true);
        }
        else
        {
            // Main → Sub
            if (mainScreenObj.activeSelf)
            {
                // Quick fade via overlay
                yield return StartCoroutine(FadeOverlayTo(0.5f, dur * 0.5f));
            }

            // Configure sub screen content
            ConfigureSubScreen(screen);
            subScreenObj.SetActive(true);
            mainScreenObj.SetActive(false);

            var cg = subScreenObj.GetComponent<CanvasGroup>();
            cg.alpha = 0f;

            yield return StartCoroutine(FadeOverlayTo(0f, dur * 0.5f));
            yield return StartCoroutine(FadeCanvasGroup(cg, 1f, dur));
        }

        isSubTransitioning = false;
    }

    void ConfigureSubScreen(MenuScreen screen)
    {
        settingsRowObj.SetActive(false);
        subScreenBody.gameObject.SetActive(true);

        if (subScreenTitleGO  != null) subScreenTitleGO.SetActive(true);
        if (subScreenDividerGO != null) subScreenDividerGO.SetActive(true);

        switch (screen)
        {
            case MenuScreen.Controls:
                subScreenTitle.text = "CONTROLS";
                subScreenBody.text  = CONTROLS_TEXT;
                break;

            case MenuScreen.Settings:
                subScreenTitle.text = "SETTINGS";
                subScreenBody.text  = "";
                subScreenBody.gameObject.SetActive(false);
                settingsRowObj.SetActive(true);
                UpdateVolumeUI();
                break;

            case MenuScreen.ArtistStatement:
                subScreenTitle.text = "ARTIST STATEMENT";
                subScreenBody.text  = ARTIST_STATEMENT;
                break;

            case MenuScreen.Credits:
                subScreenTitle.text = "CREDITS";
                subScreenBody.text  = CREDITS_TEXT;
                break;
        }
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float target, float dur)
    {
        float start   = cg.alpha;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, target, elapsed / dur);
            yield return null;
        }
        cg.alpha = target;
    }

    // ─── Scene Transitions ────────────────────────────────────────────────────

    IEnumerator TransitionToLaminarFlow(bool resume = false)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        if (!resume)
            GameManager.ClearSavedGame();

        yield return StartCoroutine(FadeOverlayTo(1f, fadeOutDuration));

        var op = SceneManager.LoadSceneAsync(laminarFlowSceneName, LoadSceneMode.Single);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;

        isTransitioning = false;
    }

    IEnumerator IntroSequence()
    {
        // Start from black, slow fade in
        fadeOverlay.color = Color.black;
        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(FadeOverlayTo(0f, fadeInDuration));
    }

    IEnumerator FadeIn(float dur)
    {
        fadeOverlay.color = Color.black;
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(FadeOverlayTo(0f, dur));
    }

    IEnumerator FadeOverlayTo(float targetAlpha, float dur)
    {
        if (fadeOverlay == null) yield break;
        float startAlpha = fadeOverlay.color.a;
        float elapsed    = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            fadeOverlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }
        fadeOverlay.color = new Color(0f, 0f, 0f, targetAlpha);
    }

    // ─── Utility ──────────────────────────────────────────────────────────────

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;
        GUILayout.BeginArea(new Rect(10, 10, 200, 60));
        GUILayout.Label($"Screen: {currentScreen}");
        GUILayout.Label($"Hovered: {hoveredItem}");
        GUILayout.Label($"Volume: {masterVolume:F2}");
        GUILayout.EndArea();
    }
}
