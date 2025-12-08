using UnityEngine;
using System.Collections.Generic;

public class AmbientSoundscapeController : MonoBehaviour
{
    [System.Serializable]
    public class AmbientLayer
    {
        public string name = "Layer";
        public AudioClip clip;
        
        [Range(0f, 1f)]
        public float baseVolume = 0.5f;
        
        [Tooltip("Minimum divergence to start fading in (0 = always on)")]
        public float minDivergence = 0f;
        
        [Tooltip("Divergence at which layer reaches full volume")]
        public float maxDivergence = 1f;
        
        [Tooltip("Volume curve for fading")]
        public AnimationCurve volumeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        
        [Tooltip("Pitch shift based on divergence (1 = no shift)")]
        public float minPitch = 1f;
        public float maxPitch = 1f;
        
        [HideInInspector] public AudioSource source;
        [HideInInspector] public float currentVolume;
        [HideInInspector] public float targetVolume;
    }
    
    [Header("References")]
    public FlowSimulation flowSimulation;
    public TurbulentEventScheduler eventScheduler;
    
    [Header("Ambient Layers")]
    [Tooltip("Ambient audio layers that blend based on game state")]
    public List<AmbientLayer> ambientLayers = new List<AmbientLayer>();
    
    [Header("Event Stinger")]
    [Tooltip("Sound that swells when a new turbulence event starts")]
    public AudioClip eventStingerClip;
    
    [Range(0f, 1f)]
    public float stingerVolume = 0.6f;
    
    [Tooltip("How long the stinger fades in")]
    public float stingerFadeIn = 0.3f;
    
    [Tooltip("How long the stinger fades out")]
    public float stingerFadeOut = 2f;
    
    [Header("Global Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 0.8f;
    
    [Tooltip("How quickly layers fade (higher = faster response)")]
    [Range(0.5f, 5f)]
    public float fadeSpeed = 1.5f;
    
    [Tooltip("Smoothing for divergence input")]
    [Range(0.5f, 5f)]
    public float divergenceSmoothing = 2f;
    
    [Header("Spatial Audio")]
    [Tooltip("Add subtle stereo movement based on nearest event direction")]
    public bool enableSpatialHints = true;
    
    [Range(0f, 0.3f)]
    public float spatialAmount = 0.15f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Runtime state
    private float smoothedDivergence = 0f;
    private float currentPanPosition = 0f;
    private AudioSource stingerSource;
    private float stingerTimer = 0f;
    private bool stingerPlaying = false;
    
    // Event tracking
    private HashSet<string> knownEvents = new HashSet<string>();
    private int lastActiveEventCount = 0;
    
    void Start()
    {
        FindReferences();
        InitializeAudioSources();
        StartAmbientLayers();
    }
    
    void FindReferences()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
        
        if (eventScheduler == null)
            eventScheduler = FindObjectOfType<TurbulentEventScheduler>();
    }
    
    void InitializeAudioSources()
    {
        // Create audio sources for each layer
        for (int i = 0; i < ambientLayers.Count; i++)
        {
            var layer = ambientLayers[i];
            
            GameObject sourceObj = new GameObject($"AmbientLayer_{i}_{layer.name}");
            sourceObj.transform.SetParent(transform);
            
            layer.source = sourceObj.AddComponent<AudioSource>();
            layer.source.clip = layer.clip;
            layer.source.loop = true;
            layer.source.playOnAwake = false;
            layer.source.spatialBlend = 0f; // 2D audio
            layer.source.volume = 0f;
            layer.source.pitch = layer.minPitch;
            
            layer.currentVolume = 0f;
            layer.targetVolume = 0f;
        }
        
        // Create stinger source
        GameObject stingerObj = new GameObject("EventStinger");
        stingerObj.transform.SetParent(transform);
        stingerSource = stingerObj.AddComponent<AudioSource>();
        stingerSource.clip = eventStingerClip;
        stingerSource.loop = false;
        stingerSource.playOnAwake = false;
        stingerSource.spatialBlend = 0f;
        stingerSource.volume = 0f;
    }
    
    void StartAmbientLayers()
    {
        foreach (var layer in ambientLayers)
        {
            if (layer.clip != null && layer.source != null)
            {
                // Start with random offset for variety
                layer.source.time = Random.Range(0f, layer.clip.length);
                layer.source.Play();
                
                Debug.Log($"[Ambience] Started layer: {layer.name}");
            }
        }
    }
    
    void Update()
    {
        UpdateDivergence();
        UpdateLayerVolumes();
        UpdateSpatialPanning();
        UpdateStinger();
        CheckForNewEvents();
    }
    
    void UpdateDivergence()
    {
        if (flowSimulation == null) return;
        
        float targetDiv = flowSimulation.CurrentDivergence;
        smoothedDivergence = Mathf.Lerp(smoothedDivergence, targetDiv, Time.deltaTime * divergenceSmoothing);
    }
    
    void UpdateLayerVolumes()
    {
        foreach (var layer in ambientLayers)
        {
            if (layer.source == null) continue;
            
            // Calculate target volume based on divergence
            float divRange = layer.maxDivergence - layer.minDivergence;
            float normalizedDiv = 0f;
            
            if (divRange > 0.001f)
            {
                normalizedDiv = Mathf.Clamp01((smoothedDivergence - layer.minDivergence) / divRange);
            }
            else if (smoothedDivergence >= layer.minDivergence)
            {
                normalizedDiv = 1f;
            }
            
            // Apply volume curve
            float curveValue = layer.volumeCurve.Evaluate(normalizedDiv);
            layer.targetVolume = layer.baseVolume * curveValue * masterVolume;
            
            // Smooth volume transition
            layer.currentVolume = Mathf.Lerp(layer.currentVolume, layer.targetVolume, Time.deltaTime * fadeSpeed);
            layer.source.volume = layer.currentVolume;
            
            // Pitch shift
            float targetPitch = Mathf.Lerp(layer.minPitch, layer.maxPitch, normalizedDiv);
            layer.source.pitch = Mathf.Lerp(layer.source.pitch, targetPitch, Time.deltaTime * fadeSpeed);
        }
    }
    
    void UpdateSpatialPanning()
    {
        if (!enableSpatialHints || eventScheduler == null) return;
        
        // Find direction to nearest off-screen event
        Vector2 cameraPos = Camera.main != null ? 
            new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.y) : 
            Vector2.zero;
        
        float targetPan = 0f;
        float nearestDist = float.MaxValue;
        
        foreach (var evt in eventScheduler.GetActiveEvents())
        {
            if (!evt.isActive) continue;
            
            Vector2 toEvent = evt.position - cameraPos;
            float dist = toEvent.magnitude;
            
            if (dist < nearestDist && dist > 5f) // Only pan for off-screen events
            {
                nearestDist = dist;
                // Normalize X direction for pan (-1 to 1)
                targetPan = Mathf.Clamp(toEvent.x / 30f, -1f, 1f) * spatialAmount;
            }
        }
        
        // Smooth pan transition
        currentPanPosition = Mathf.Lerp(currentPanPosition, targetPan, Time.deltaTime * 2f);
        
        // Apply subtle pan to tension layers (not base layer)
        for (int i = 1; i < ambientLayers.Count; i++)
        {
            if (ambientLayers[i].source != null)
            {
                ambientLayers[i].source.panStereo = currentPanPosition;
            }
        }
    }
    
    void UpdateStinger()
    {
        if (stingerTimer > 0f)
        {
            stingerTimer -= Time.deltaTime;
            
            // Fade envelope
            float fadeTime = stingerFadeIn + stingerFadeOut;
            float elapsed = fadeTime - stingerTimer;
            
            float volume;
            if (elapsed < stingerFadeIn)
            {
                // Fade in
                volume = (elapsed / stingerFadeIn) * stingerVolume * masterVolume;
            }
            else
            {
                // Fade out
                float fadeOutElapsed = elapsed - stingerFadeIn;
                volume = (1f - fadeOutElapsed / stingerFadeOut) * stingerVolume * masterVolume;
            }
            
            stingerSource.volume = Mathf.Max(0f, volume);
            
            if (stingerTimer <= 0f)
            {
                stingerSource.Stop();
                stingerPlaying = false;
            }
        }
    }
    
    void CheckForNewEvents()
    {
        if (eventScheduler == null) return;
        
        var activeEvents = eventScheduler.GetActiveEvents();
        
        // Check for new events
        foreach (var evt in activeEvents)
        {
            if (evt.isActive && !knownEvents.Contains(evt.eventName))
            {
                knownEvents.Add(evt.eventName);
                OnNewEventStarted(evt);
            }
        }
        
        // Clean up ended events
        knownEvents.RemoveWhere(name => !activeEvents.Exists(e => e.eventName == name && e.isActive));
        
        lastActiveEventCount = activeEvents.Count;
    }
    
    void OnNewEventStarted(TurbulenceEvent evt)
    {
        // Trigger stinger
        TriggerStinger();
        
        Debug.Log($"[Ambience] New event detected: {evt.eventName}, triggering stinger");
    }
    
    /// <summary>
    /// Trigger the event stinger sound
    /// </summary>
    public void TriggerStinger()
    {
        if (eventStingerClip == null || stingerSource == null) return;
        
        // Reset and play stinger
        stingerSource.Stop();
        stingerSource.clip = eventStingerClip;
        stingerSource.volume = 0f;
        stingerSource.Play();
        
        stingerTimer = stingerFadeIn + stingerFadeOut;
        stingerPlaying = true;
    }
    
    /// <summary>
    /// Manually set divergence (for testing or cutscenes)
    /// </summary>
    public void SetDivergenceOverride(float divergence)
    {
        smoothedDivergence = divergence;
    }
    
    /// <summary>
    /// Fade all audio to silence
    /// </summary>
    public void FadeToSilence(float duration)
    {
        StartCoroutine(FadeAllCoroutine(0f, duration));
    }
    
    /// <summary>
    /// Fade all audio back to normal
    /// </summary>
    public void FadeIn(float duration)
    {
        StartCoroutine(FadeAllCoroutine(masterVolume, duration));
    }
    
    private System.Collections.IEnumerator FadeAllCoroutine(float targetMaster, float duration)
    {
        float startMaster = masterVolume;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            masterVolume = Mathf.Lerp(startMaster, targetMaster, elapsed / duration);
            yield return null;
        }
        
        masterVolume = targetMaster;
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(Screen.width - 250, 150, 240, 300));
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.Box(new Rect(0, 0, 240, 300), "");
        GUI.color = Color.white;
        
        GUILayout.Label("=== AMBIENT SOUNDSCAPE ===");
        GUILayout.Label($"Divergence: {smoothedDivergence:F3}");
        GUILayout.Label($"Pan: {currentPanPosition:F2}");
        GUILayout.Label($"Stinger: {(stingerPlaying ? "Playing" : "Idle")}");
        GUILayout.Space(10);
        
        foreach (var layer in ambientLayers)
        {
            string status = layer.clip != null ? $"{layer.currentVolume:F2}" : "NO CLIP";
            GUILayout.Label($"{layer.name}: {status}");
        }
        
        GUILayout.EndArea();
    }
    
    void OnDestroy()
    {
        // Cleanup audio sources
        foreach (var layer in ambientLayers)
        {
            if (layer.source != null)
            {
                Destroy(layer.source.gameObject);
            }
        }
    }
}
