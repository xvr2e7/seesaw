using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Handles smooth scene transitions by providing a fade overlay.
/// Attach to a GameObject in the Laminar Flow scene.
/// Starts with a black overlay and fades in when the scene loads.
/// This prevents the brief flash of the default skybox/background.
/// </summary>
public class SceneTransitionHandler : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("Duration of fade-in when scene loads")]
    public float fadeInDuration = 1.5f;
    
    [Tooltip("Initial delay before starting fade")]
    public float fadeInDelay = 0.1f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // UI
    private Canvas canvas;
    private Image fadeOverlay;
    
    // State
    private bool isFading = false;
    private static bool shouldFadeIn = true;
    
    /// <summary>
    /// Call this before loading a scene to skip the fade-in
    /// (e.g., when returning from documentary and handling fade manually)
    /// </summary>
    public static void SkipNextFadeIn()
    {
        shouldFadeIn = false;
    }
    
    void Awake()
    {
        CreateUI();
        
        // Start with black overlay
        if (fadeOverlay != null)
        {
            fadeOverlay.color = Color.black;
        }
    }
    
    void Start()
    {
        if (shouldFadeIn)
        {
            StartCoroutine(FadeIn());
        }
        else
        {
            // Skip fade, just make overlay transparent immediately
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0, 0, 0, 0);
            }
            shouldFadeIn = true; // Reset for next time
        }
    }
    
    void CreateUI()
    {
        // Create canvas for fade overlay
        GameObject canvasObj = new GameObject("TransitionCanvas");
        canvasObj.transform.SetParent(transform);
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // On top of everything
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Create fade overlay
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
    
    IEnumerator FadeIn()
    {
        isFading = true;
        
        // Ensure we start fully black
        fadeOverlay.color = Color.black;
        
        // Wait for delay (allows scene to fully initialize)
        yield return new WaitForSeconds(fadeInDelay);
        
        // Fade in
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeInDuration;
            float alpha = 1f - t;
            fadeOverlay.color = new Color(0, 0, 0, alpha);
            yield return null;
        }
        
        fadeOverlay.color = new Color(0, 0, 0, 0);
        isFading = false;
        
        // Optionally disable the canvas when done to save rendering
        canvas.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Fade out to black (call before loading another scene)
    /// </summary>
    public IEnumerator FadeOut(float duration)
    {
        if (fadeOverlay == null) yield break;
        
        // Ensure canvas is active
        canvas.gameObject.SetActive(true);
        
        isFading = true;
        fadeOverlay.color = new Color(0, 0, 0, 0);
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            fadeOverlay.color = new Color(0, 0, 0, t);
            yield return null;
        }
        
        fadeOverlay.color = Color.black;
        isFading = false;
    }
    
    /// <summary>
    /// Get the current fade overlay for external control
    /// </summary>
    public Image GetFadeOverlay()
    {
        return fadeOverlay;
    }
    
    /// <summary>
    /// Check if currently fading
    /// </summary>
    public bool IsFading => isFading;
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(Screen.width - 210, 10, 200, 60));
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, 200, 60), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        GUILayout.Label("=== TRANSITION ===");
        GUILayout.Label($"Fading: {isFading}");
        if (fadeOverlay != null)
        {
            GUILayout.Label($"Alpha: {fadeOverlay.color.a:F2}");
        }
        GUILayout.EndArea();
    }
}
