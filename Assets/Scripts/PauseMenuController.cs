using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// In-game pause menu.  ESC shows a modal with: CONTINUE / SETTINGS / EXIT TO MENU.
/// Matches the Console screen's visual language (dark palette, TMP, procedural cursor).
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Scene")]
    public string consoleSceneName = "Console";

    [Header("Transition")]
    public float fadeInDuration  = 0.25f;
    public float fadeOutDuration = 0.4f;

    [Header("Custom Font")]
    public TMP_FontAsset customFont;

    // ─── Palette (mirrors ConsoleController) ──────────────────────────────────
    private static readonly Color BgColor        = new Color(0.015f, 0.015f, 0.018f, 0.92f);
    private static readonly Color TextDim        = new Color(0.38f, 0.40f, 0.43f, 1f);
    private static readonly Color TextBright     = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color DividerColor   = new Color(0.22f, 0.24f, 0.27f, 1f);

    // ─── Runtime state ────────────────────────────────────────────────────────

    private enum PauseScreen { Root, Controls, Settings }
    private PauseScreen currentScreen = PauseScreen.Root;

    private Canvas          canvas;
    private CanvasGroup     modalGroup;
    private GameObject      rootScreenObj;
    private GameObject      controlsScreenObj;
    private GameObject      settingsScreenObj;
    private RectTransform   cursorRect;
    private Image           cursorDotImage;

    private readonly string[] menuLabels    = { "CONTINUE", "CONTROLS", "SETTINGS", "EXIT TO MENU" };
    private TextMeshProUGUI[] menuTexts;
    private float[]           itemBrightness;
    private int               hoveredItem   = -1;

    // Settings (audio)
    private float          masterVolume;
    private Image          volumeBarFill;
    private RectTransform  volumeTrackRect;
    private TextMeshProUGUI volumeValueTMP;
    private bool           isDraggingVolume = false;

    private bool isPaused        = false;
    private bool isAnimating     = false;
    private bool isSubAnimating  = false;

    private TMP_FontAsset  resolvedFont;

    // Cursor scale
    private float cursorScale       = 1f;
    private float cursorTargetScale = 1f;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        ResolveFont();
        BuildUI();
        // Modal starts hidden
        canvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isPaused)
                Open();
            else if (currentScreen == PauseScreen.Controls || currentScreen == PauseScreen.Settings)
                SwitchToRoot();
            else
                Close();
            return;
        }

        if (!isPaused || isAnimating) return;

        UpdateCursor();

        if (!isSubAnimating)
        {
            if (currentScreen == PauseScreen.Root)
            {
                UpdateHover();
                UpdateClick();
            }
            UpdateVolumeSlider();
        }
    }

    // ─── Open / Close ─────────────────────────────────────────────────────────

    void Open()
    {
        isPaused = true;
        Time.timeScale = 0f;
        canvas.gameObject.SetActive(true);
        Cursor.visible = false;
        currentScreen  = PauseScreen.Root;
        rootScreenObj.SetActive(true);
        controlsScreenObj.SetActive(false);
        settingsScreenObj.SetActive(false);
        SetGameHUDVisible(false);
        StartCoroutine(AnimateIn());
        GameManager.SavePauseState();
    }

    void Close()
    {
        StartCoroutine(AnimateOut(() =>
        {
            isPaused = false;
            Time.timeScale = 1f;
            canvas.gameObject.SetActive(false);
            SetGameHUDVisible(true);
        }));
    }

    void SetGameHUDVisible(bool visible)
    {
        FindObjectOfType<TurbulenceIndicatorUI>()?.SetPaused(!visible);
        FindObjectOfType<GameStateUI>()?.SetPaused(!visible);
        FindObjectOfType<PlayerToolController>()?.SetPaused(!visible);
        FindObjectOfType<SamplingGrid>()?.SetPaused(!visible);
    }

    // ─── Animations ───────────────────────────────────────────────────────────

    IEnumerator AnimateIn()
    {
        isAnimating = true;
        modalGroup.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            modalGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        modalGroup.alpha = 1f;
        isAnimating = false;
    }

    IEnumerator AnimateOut(System.Action onDone)
    {
        isAnimating = true;
        float elapsed = 0f;
        float startAlpha = modalGroup.alpha;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            modalGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
            yield return null;
        }
        modalGroup.alpha = 0f;
        isAnimating = false;
        onDone?.Invoke();
    }

    // ─── Screen switching ─────────────────────────────────────────────────────

    void SwitchToControls()
    {
        currentScreen = PauseScreen.Controls;
        StartCoroutine(CrossFadeScreens(rootScreenObj, controlsScreenObj));
    }

    void SwitchToSettings()
    {
        currentScreen = PauseScreen.Settings;
        StartCoroutine(CrossFadeScreens(rootScreenObj, settingsScreenObj));
        // Refresh slider to current value
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        UpdateVolumeUI();
    }

    void SwitchToRoot()
    {
        GameObject fromScreen = currentScreen == PauseScreen.Controls ? controlsScreenObj : settingsScreenObj;
        currentScreen = PauseScreen.Root;
        StartCoroutine(CrossFadeScreens(fromScreen, rootScreenObj));
    }

    IEnumerator CrossFadeScreens(GameObject from, GameObject to)
    {
        isSubAnimating = true;
        float dur = 0.2f;

        // Fade modal group out slightly
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            modalGroup.alpha = Mathf.Lerp(1f, 0.3f, elapsed / dur);
            yield return null;
        }

        from.SetActive(false);
        to.SetActive(true);

        elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            modalGroup.alpha = Mathf.Lerp(0.3f, 1f, elapsed / dur);
            yield return null;
        }
        modalGroup.alpha = 1f;
        isSubAnimating = false;
    }

    // ─── Hover / Click ────────────────────────────────────────────────────────

    void UpdateHover()
    {
        hoveredItem = -1;
        for (int i = 0; i < menuTexts.Length; i++)
        {
            if (menuTexts[i] == null) continue;
            var rect = menuTexts[i].GetComponent<RectTransform>();
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, null, out local))
            {
                if (rect.rect.Contains(local))
                    hoveredItem = i;
            }
        }

        float dt = Time.unscaledDeltaTime * 6f;
        for (int i = 0; i < menuTexts.Length; i++)
        {
            float target = (i == hoveredItem) ? 1f : 0f;
            itemBrightness[i] = Mathf.Lerp(itemBrightness[i], target, dt);
            menuTexts[i].color = Color.Lerp(TextDim, TextBright, itemBrightness[i]);
        }
    }

    void UpdateClick()
    {
        if (!Input.GetMouseButtonDown(0) || hoveredItem < 0) return;
        switch (hoveredItem)
        {
            case 0: Close(); break;
            case 1: SwitchToControls(); break;
            case 2: SwitchToSettings(); break;
            case 3: ExitToMenu(); break;
        }
    }

    // ─── Exit To Menu ─────────────────────────────────────────────────────────

    void ExitToMenu()
    {
        // State is already saved via GameManager.SavePauseState() on Open()
        StartCoroutine(ExitSequence());
    }

    IEnumerator ExitSequence()
    {
        isAnimating = true;
        // Fade modal to black
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            modalGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        // Also fade the SceneTransitionHandler overlay to black
        var transitionHandler = FindObjectOfType<SceneTransitionHandler>();
        if (transitionHandler != null)
        {
            // Use unscaled time coroutine since timeScale = 0
            yield return StartCoroutine(FadeOverlayUnscaled(transitionHandler.GetFadeOverlay(), 1f, fadeOutDuration));
        }

        Time.timeScale = 1f;
        ConsoleController.SetReturningFromGame();
        SceneManager.LoadSceneAsync(consoleSceneName, LoadSceneMode.Single);
    }

    IEnumerator FadeOverlayUnscaled(Image overlay, float target, float dur)
    {
        if (overlay == null) yield break;
        overlay.gameObject.SetActive(true);
        // ensure canvas it's on is active
        var c = overlay.GetComponentInParent<Canvas>();
        if (c != null) c.gameObject.SetActive(true);
        float start   = overlay.color.a;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, target, elapsed / dur);
            overlay.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, target);
    }

    // ─── Volume slider ────────────────────────────────────────────────────────

    void UpdateVolumeSlider()
    {
        if (currentScreen != PauseScreen.Settings) return;
        if (volumeTrackRect == null) return;

        bool mouseDown = Input.GetMouseButton(0);
        bool mouseUp   = Input.GetMouseButtonUp(0);

        Vector2 localPoint;
        bool inTrack = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            volumeTrackRect, Input.mousePosition, null, out localPoint);

        if (mouseDown && inTrack) isDraggingVolume = true;
        if (mouseUp)
        {
            isDraggingVolume = false;
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            // Apply immediately to in-game audio
            ApplyVolumeToGameAudio();
        }

        if (isDraggingVolume)
        {
            float halfW = volumeTrackRect.rect.width * 0.5f;
            float t     = Mathf.InverseLerp(-halfW, halfW, localPoint.x);
            masterVolume = Mathf.Clamp01(t);
            UpdateVolumeUI();
            ApplyVolumeToGameAudio();
        }
    }

    void ApplyVolumeToGameAudio()
    {
        var soundscape = FindObjectOfType<AmbientSoundscapeController>();
        if (soundscape != null)
            soundscape.masterVolume = masterVolume;
    }

    void UpdateVolumeUI()
    {
        if (volumeBarFill == null) return;
        var fillRect = volumeBarFill.GetComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(240f * masterVolume, 0f);
        if (volumeValueTMP != null)
            volumeValueTMP.text = Mathf.RoundToInt(masterVolume * 100f) + "%";
    }

    // ─── Cursor ───────────────────────────────────────────────────────────────

    void UpdateCursor()
    {
        if (cursorRect == null) return;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, Input.mousePosition, null, out localPoint);
        cursorRect.anchoredPosition = localPoint;

        bool overItem = (hoveredItem >= 0);
        cursorTargetScale = overItem ? 1.4f : 1f;
        cursorScale = Mathf.Lerp(cursorScale, cursorTargetScale, Time.unscaledDeltaTime * 8f);
        cursorRect.localScale = Vector3.one * cursorScale;

        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 2.1f);
        if (cursorDotImage != null)
            cursorDotImage.color = new Color(0.85f, 0.87f, 0.9f, 0.9f * pulse);
    }

    // ─── Font ─────────────────────────────────────────────────────────────────

    void ResolveFont()
    {
        if (customFont != null) { resolvedFont = customFont; return; }
        resolvedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }

    // ─── UI Construction ──────────────────────────────────────────────────────

    void BuildUI()
    {
        // Root canvas — above everything in the game scene
        var canvasGO = new GameObject("PauseCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Modal group — used for fade in/out
        var modalGO = new GameObject("Modal");
        modalGO.transform.SetParent(canvasGO.transform, false);
        modalGroup = modalGO.AddComponent<CanvasGroup>();
        var modalRect = modalGO.AddComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.offsetMin = Vector2.zero;
        modalRect.offsetMax = Vector2.zero;

        // Full-screen background — same dark fill as the console view, fully opaque
        var overlayImg = CreateFullRect(modalGO.transform, "Overlay");
        overlayImg.color = new Color(BgColor.r, BgColor.g, BgColor.b, 1f);
        overlayImg.raycastTarget = true; // block clicks to game underneath

        // Build root (menu items), controls, and settings screens directly on the modal
        rootScreenObj     = BuildRootScreen(modalGO.transform);
        controlsScreenObj = BuildControlsScreen(modalGO.transform);
        controlsScreenObj.SetActive(false);
        settingsScreenObj = BuildSettingsScreen(modalGO.transform);
        settingsScreenObj.SetActive(false);

        // Cursor — topmost
        BuildCursor(canvasGO.transform);
    }

    GameObject BuildRootScreen(Transform parent)
    {
        var go = new GameObject("RootScreen");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Single line at top of screen
        var lineGO  = new GameObject("TopLine");
        lineGO.transform.SetParent(go.transform, false);
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = DividerColor;
        lineImg.raycastTarget = false;
        var lineRect = lineGO.GetComponent<RectTransform>();
        lineRect.anchorMin        = new Vector2(0f, 1f);
        lineRect.anchorMax        = new Vector2(1f, 1f);
        lineRect.pivot            = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta        = new Vector2(0f, 1f);

        // Menu items — vertically centered as a block
        menuTexts      = new TextMeshProUGUI[menuLabels.Length];
        itemBrightness = new float[menuLabels.Length];
        float itemHeight  = 52f;
        float totalHeight = menuLabels.Length * itemHeight;
        float startY      = totalHeight * 0.5f - itemHeight * 0.5f;
        for (int i = 0; i < menuLabels.Length; i++)
        {
            float yOff = startY - i * itemHeight;
            menuTexts[i] = AddMenuItem(go.transform, menuLabels[i],
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, yOff));
            itemBrightness[i] = 0f;
        }

        return go;
    }

    GameObject BuildControlsScreen(Transform parent)
    {
        var go = new GameObject("ControlsScreen");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Top divider line
        var lineGO  = new GameObject("TopLine");
        lineGO.transform.SetParent(go.transform, false);
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = DividerColor;
        lineImg.raycastTarget = false;
        var lineRect = lineGO.GetComponent<RectTransform>();
        lineRect.anchorMin        = new Vector2(0f, 1f);
        lineRect.anchorMax        = new Vector2(1f, 1f);
        lineRect.pivot            = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta        = new Vector2(0f, 1f);

        // Body — anchored to top, left-aligned
        const string controlsText =
            "Colored disruptions appear in the flow. Your job is to suppress them before they spread.\n\n" +
            "Move your cursor over a disruption. Hold LEFT CLICK to dampen it.\n" +
            "Watch the agents return to gray. That is the goal.\n\n" +
            "MOUSE         aim the detection field\n" +
            "LEFT CLICK    hold to suppress\n" +
            "ESC           pause\n\n" +
            "SUPPRESS                   LEAVE\n" +
            "--------                   -----\n" +
            "DISPERSAL                  ASSEMBLY\n" +
            "DISTURBANCE                SPIRAL\n" +
            "BLOCKADE                   MARCH\n\n" +
            "The divergence bar (top-left) measures disorder. Keep it low.";

        var bodyGO  = new GameObject("Body");
        bodyGO.transform.SetParent(go.transform, false);
        var bodyTMP = bodyGO.AddComponent<TextMeshProUGUI>();
        bodyTMP.text          = controlsText;
        bodyTMP.font          = resolvedFont;
        bodyTMP.fontSize      = 16f;
        bodyTMP.color         = new Color(0.55f, 0.57f, 0.6f, 1f);
        bodyTMP.alignment     = TextAlignmentOptions.Left;
        bodyTMP.lineSpacing   = 8f;
        bodyTMP.raycastTarget = false;
        var bodyRect = bodyGO.GetComponent<RectTransform>();
        bodyRect.anchorMin        = new Vector2(0.5f, 1f);
        bodyRect.anchorMax        = new Vector2(0.5f, 1f);
        bodyRect.pivot            = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -100f);
        bodyRect.sizeDelta        = new Vector2(700f, 520f);

        return go;
    }

    GameObject BuildSettingsScreen(Transform parent)
    {
        var go = new GameObject("SettingsScreen");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Single line at top of screen
        var lineGO  = new GameObject("TopLine");
        lineGO.transform.SetParent(go.transform, false);
        var lineImg = lineGO.AddComponent<Image>();
        lineImg.color = DividerColor;
        lineImg.raycastTarget = false;
        var lineRect = lineGO.GetComponent<RectTransform>();
        lineRect.anchorMin        = new Vector2(0f, 1f);
        lineRect.anchorMax        = new Vector2(1f, 1f);
        lineRect.pivot            = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta        = new Vector2(0f, 1f);

        // Volume row — centered
        BuildVolumeRow(go.transform, Vector2.zero);

        return go;
    }

    void BuildVolumeRow(Transform parent, Vector2 pos)
    {
        var rowGO   = new GameObject("VolumeRow");
        rowGO.transform.SetParent(parent, false);
        var rowRect = rowGO.AddComponent<RectTransform>();
        rowRect.anchorMin        = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax        = new Vector2(0.5f, 0.5f);
        rowRect.pivot            = new Vector2(0.5f, 0.5f);
        rowRect.anchoredPosition = pos;
        rowRect.sizeDelta        = new Vector2(370f, 44f);

        // Label
        var labelGO  = new GameObject("Label");
        labelGO.transform.SetParent(rowGO.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text             = "AUDIO";
        labelTMP.font             = resolvedFont;
        labelTMP.fontSize         = 12f;
        labelTMP.color            = new Color(0.5f, 0.52f, 0.55f, 1f);
        labelTMP.characterSpacing = 3f;
        labelTMP.alignment        = TextAlignmentOptions.Left;
        labelTMP.raycastTarget    = false;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin        = new Vector2(0f, 0.5f);
        labelRect.anchorMax        = new Vector2(0f, 0.5f);
        labelRect.pivot            = new Vector2(1f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-10f, 0f);
        labelRect.sizeDelta        = new Vector2(80f, 30f);

        // Track
        var trackGO  = new GameObject("Track");
        trackGO.transform.SetParent(rowGO.transform, false);
        var trackImg = trackGO.AddComponent<Image>();
        trackImg.color = new Color(0.1f, 0.11f, 0.12f, 1f);
        trackImg.raycastTarget = true;
        volumeTrackRect = trackGO.GetComponent<RectTransform>();
        volumeTrackRect.anchorMin        = new Vector2(0f, 0.5f);
        volumeTrackRect.anchorMax        = new Vector2(0f, 0.5f);
        volumeTrackRect.pivot            = new Vector2(0f, 0.5f);
        volumeTrackRect.anchoredPosition = new Vector2(0f, 0f);
        volumeTrackRect.sizeDelta        = new Vector2(240f, 3f);

        // Fill
        var fillGO  = new GameObject("Fill");
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
        var valGO  = new GameObject("Value");
        valGO.transform.SetParent(rowGO.transform, false);
        volumeValueTMP = valGO.AddComponent<TextMeshProUGUI>();
        volumeValueTMP.font      = resolvedFont;
        volumeValueTMP.fontSize  = 11f;
        volumeValueTMP.color     = new Color(0.4f, 0.42f, 0.45f, 1f);
        volumeValueTMP.alignment = TextAlignmentOptions.Left;
        volumeValueTMP.raycastTarget = false;
        var valRect = valGO.GetComponent<RectTransform>();
        valRect.anchorMin        = new Vector2(0f, 0.5f);
        valRect.anchorMax        = new Vector2(0f, 0.5f);
        valRect.pivot            = new Vector2(0f, 0.5f);
        valRect.anchoredPosition = new Vector2(254f, 0f);
        valRect.sizeDelta        = new Vector2(50f, 30f);

        UpdateVolumeUI();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    Image CreateFullRect(Transform parent, string name)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img  = go.AddComponent<Image>();
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return img;
    }

    void AddLabel(Transform parent, string text, float fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        var go  = new GameObject("Label_" + text);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.font             = resolvedFont;
        tmp.fontSize         = fontSize;
        tmp.color            = color;
        tmp.characterSpacing = 4f;
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.raycastTarget    = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = size;
    }

    void AddDivider(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size)
    {
        var go   = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var img  = go.AddComponent<Image>();
        img.color = DividerColor;
        img.raycastTarget = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = size;
    }

    TextMeshProUGUI AddMenuItem(Transform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
    {
        var go   = new GameObject("Item_" + label);
        go.transform.SetParent(parent, false);
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = label;
        tmp.font             = resolvedFont;
        tmp.fontSize         = 24f;
        tmp.color            = TextDim;
        tmp.characterSpacing = 3f;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.raycastTarget    = true;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(400f, 44f);
        return tmp;
    }

    void BuildCursor(Transform parent)
    {
        var go   = new GameObject("PauseCursor");
        go.transform.SetParent(parent, false);
        cursorRect = go.AddComponent<RectTransform>();
        cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
        cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot     = new Vector2(0.5f, 0.5f);
        cursorRect.sizeDelta = new Vector2(24f, 24f);

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

        // Cross arms
        AddCursorArm(go.transform, new Vector2(2f, 6f),  new Vector2(0f,  4f));
        AddCursorArm(go.transform, new Vector2(2f, 6f),  new Vector2(0f, -4f));
        AddCursorArm(go.transform, new Vector2(6f, 2f),  new Vector2(-4f, 0f));
        AddCursorArm(go.transform, new Vector2(6f, 2f),  new Vector2( 4f, 0f));
    }

    void AddCursorArm(Transform parent, Vector2 size, Vector2 offset)
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
}
