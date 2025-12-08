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
        
        [Header("Thresholds (Turbulence Level 0-1+)")]
        [Tooltip("Turbulence level must exceed this to trigger fade-in")]
        public float triggerThreshold = 0f;
        
        [Tooltip("Turbulence must drop below this to start fade-out (set lower than trigger for hysteresis)")]
        public float releaseThreshold = 0f;
        
        [Header("Envelope Timing")]
        [Tooltip("Time to fade in when triggered")]
        public float fadeInTime = 1.5f;
        
        [Tooltip("Minimum time to stay at full volume before fade-out can begin")]
        public float minSustainTime = 2f;
        
        [Tooltip("Time to fade out after release")]
        public float fadeOutTime = 3f;
        
        [Header("Pitch Modulation")]
        public float minPitch = 1f;
        public float maxPitch = 1f;
        
        // Runtime state
        [HideInInspector] public AudioSource source;
        [HideInInspector] public float currentVolume;
        [HideInInspector] public float envelopeValue;
        [HideInInspector] public LayerState state;
        [HideInInspector] public float stateTimer;
    }
    
    public enum LayerState
    {
        Idle,       // Below threshold, silent
        FadingIn,   // Triggered, ramping up
        Sustaining, // Holding at full volume
        FadingOut   // Released, ramping down
    }
    
    [Header("References")]
    public TurbulentEventScheduler eventScheduler;
    public Camera mainCamera;
    
    [Header("Turbulence Calculation")]
    [Tooltip("How much nearby events contribute more than distant ones (0 = no distance weighting)")]
    [Range(0f, 1f)]
    public float proximityWeight = 0.5f;
    
    [Tooltip("Distance at which event contribution starts falling off")]
    public float proximityFalloffStart = 20f;
    
    [Tooltip("Distance beyond which events contribute minimally")]
    public float proximityFalloffEnd = 60f;
    
    [Tooltip("Smoothing for turbulence level changes")]
    [Range(0.5f, 5f)]
    public float turbulenceSmoothing = 2f;
    
    [Header("Ambient Layers")]
    [Tooltip("Ambient audio layers with envelope-based triggering")]
    public List<AmbientLayer> ambientLayers = new List<AmbientLayer>();
    
    [Header("Event Stinger")]
    [Tooltip("Sound that plays when a new turbulence event starts")]
    public AudioClip eventStingerClip;
    
    [Range(0f, 1f)]
    public float stingerVolume = 0.6f;
    
    public float stingerFadeIn = 0.3f;
    public float stingerFadeOut = 2f;
    
    [Header("Global Settings")]
    [Range(0f, 1f)]
    public float masterVolume = 0.8f;
    
    [Header("Spatial Audio")]
    [Tooltip("Add subtle stereo panning toward nearest event")]
    public bool enableSpatialHints = true;
    
    [Range(0f, 0.3f)]
    public float spatialAmount = 0.15f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Runtime state
    private float currentTurbulenceLevel = 0f;
    private float smoothedTurbulenceLevel = 0f;
    private float currentPanPosition = 0f;
    private AudioSource stingerSource;
    private float stingerTimer = 0f;
    private bool stingerPlaying = false;
    
    // Event tracking
    private HashSet<string> knownEvents = new HashSet<string>();
    private int activeEventCount = 0;
    
    void Start()
    {
        FindReferences();
        InitializeAudioSources();
        StartAmbientLayers();
    }
    
    void FindReferences()
    {
        if (eventScheduler == null)
            eventScheduler = FindObjectOfType<TurbulentEventScheduler>();
        
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    
    void InitializeAudioSources()
    {
        for (int i = 0; i < ambientLayers.Count; i++)
        {
            var layer = ambientLayers[i];
            
            GameObject sourceObj = new GameObject($"AmbientLayer_{i}_{layer.name}");
            sourceObj.transform.SetParent(transform);
            
            layer.source = sourceObj.AddComponent<AudioSource>();
            layer.source.clip = layer.clip;
            layer.source.loop = true;
            layer.source.playOnAwake = false;
            layer.source.spatialBlend = 0f;
            layer.source.volume = 0f;
            layer.source.pitch = layer.minPitch;
            
            layer.currentVolume = 0f;
            layer.envelopeValue = 0f;
            layer.state = LayerState.Idle;
            layer.stateTimer = 0f;
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
                layer.source.time = Random.Range(0f, layer.clip.length);
                layer.source.Play();
                Debug.Log($"[Ambience] Started layer: {layer.name}");
            }
        }
    }
    
    void Update()
    {
        CalculateTurbulenceLevel();
        UpdateLayerEnvelopes();
        UpdateSpatialPanning();
        UpdateStinger();
        CheckForNewEvents();
    }
    
    /// <summary>
    /// Calculate turbulence level from active events, weighted by proximity to camera
    /// </summary>
    void CalculateTurbulenceLevel()
    {
        if (eventScheduler == null)
        {
            currentTurbulenceLevel = 0f;
            smoothedTurbulenceLevel = Mathf.Lerp(smoothedTurbulenceLevel, 0f, Time.deltaTime * turbulenceSmoothing);
            return;
        }
        
        var activeEvents = eventScheduler.GetActiveEvents();
        activeEventCount = 0;
        
        Vector2 cameraPos = mainCamera != null 
            ? new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.y)
            : Vector2.zero;
        
        float totalTurbulence = 0f;
        float peakTurbulence = 0f;
        
        foreach (var evt in activeEvents)
        {
            if (!evt.isActive) continue;
            
            activeEventCount++;
            
            // Base contribution: intensity × strength (normalized)
            // strength typically ranges 1.5-4, so normalize by dividing by ~3
            float baseContribution = evt.currentIntensity * (evt.strength / 3f);
            
            // Distance weighting
            float distance = Vector2.Distance(cameraPos, evt.position);
            float proximityFactor = 1f;
            
            if (proximityWeight > 0f)
            {
                if (distance < proximityFalloffStart)
                {
                    proximityFactor = 1f;
                }
                else if (distance > proximityFalloffEnd)
                {
                    proximityFactor = 1f - proximityWeight; // Minimum contribution
                }
                else
                {
                    // Smooth falloff
                    float t = (distance - proximityFalloffStart) / (proximityFalloffEnd - proximityFalloffStart);
                    t = t * t; // Quadratic falloff
                    proximityFactor = Mathf.Lerp(1f, 1f - proximityWeight, t);
                }
            }
            
            float contribution = baseContribution * proximityFactor;
            totalTurbulence += contribution;
            peakTurbulence = Mathf.Max(peakTurbulence, contribution);
        }
        
        // Combine sum and peak for final level
        // sqrt of sum gives diminishing returns for many events
        float sumComponent = Mathf.Sqrt(totalTurbulence);
        float peakComponent = peakTurbulence;
        
        // Blend: mostly peak-driven, but multiple events still increase tension
        currentTurbulenceLevel = Mathf.Max(peakComponent, sumComponent * 0.7f);
        
        // Smooth the turbulence level
        smoothedTurbulenceLevel = Mathf.Lerp(
            smoothedTurbulenceLevel, 
            currentTurbulenceLevel, 
            Time.deltaTime * turbulenceSmoothing
        );
    }
    
    void UpdateLayerEnvelopes()
    {
        float dt = Time.deltaTime;
        
        foreach (var layer in ambientLayers)
        {
            if (layer.source == null) continue;
            
            layer.stateTimer += dt;
            
            // State machine for envelope
            switch (layer.state)
            {
                case LayerState.Idle:
                    if (smoothedTurbulenceLevel >= layer.triggerThreshold)
                    {
                        layer.state = LayerState.FadingIn;
                        layer.stateTimer = 0f;
                    }
                    layer.envelopeValue = 0f;
                    break;
                    
                case LayerState.FadingIn:
                    if (layer.fadeInTime > 0f)
                    {
                        layer.envelopeValue = Mathf.Clamp01(layer.stateTimer / layer.fadeInTime);
                    }
                    else
                    {
                        layer.envelopeValue = 1f;
                    }
                    
                    if (layer.envelopeValue >= 1f)
                    {
                        layer.state = LayerState.Sustaining;
                        layer.stateTimer = 0f;
                        layer.envelopeValue = 1f;
                    }
                    break;
                    
                case LayerState.Sustaining:
                    layer.envelopeValue = 1f;
                    
                    // Release only after minimum sustain and below release threshold
                    if (layer.stateTimer >= layer.minSustainTime && 
                        smoothedTurbulenceLevel < layer.releaseThreshold)
                    {
                        layer.state = LayerState.FadingOut;
                        layer.stateTimer = 0f;
                    }
                    // Re-trigger resets sustain timer
                    else if (smoothedTurbulenceLevel >= layer.triggerThreshold)
                    {
                        layer.stateTimer = 0f;
                    }
                    break;
                    
                case LayerState.FadingOut:
                    if (layer.fadeOutTime > 0f)
                    {
                        layer.envelopeValue = 1f - Mathf.Clamp01(layer.stateTimer / layer.fadeOutTime);
                    }
                    else
                    {
                        layer.envelopeValue = 0f;
                    }
                    
                    // Re-trigger during fade-out
                    if (smoothedTurbulenceLevel >= layer.triggerThreshold)
                    {
                        layer.state = LayerState.FadingIn;
                        // Continue from current envelope position
                        layer.stateTimer = layer.envelopeValue * layer.fadeInTime;
                    }
                    else if (layer.envelopeValue <= 0f)
                    {
                        layer.state = LayerState.Idle;
                        layer.stateTimer = 0f;
                        layer.envelopeValue = 0f;
                    }
                    break;
            }
            
            // Apply smooth easing
            float easedEnvelope = SmoothStep(layer.envelopeValue);
            
            // Final volume
            layer.currentVolume = layer.baseVolume * easedEnvelope * masterVolume;
            layer.source.volume = layer.currentVolume;
            
            // Pitch modulation based on turbulence intensity
            float pitchT = Mathf.Clamp01(smoothedTurbulenceLevel);
            float targetPitch = Mathf.Lerp(layer.minPitch, layer.maxPitch, pitchT);
            layer.source.pitch = Mathf.Lerp(layer.source.pitch, targetPitch, dt * 2f);
        }
    }
    
    float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
    
    void UpdateSpatialPanning()
    {
        if (!enableSpatialHints || eventScheduler == null || mainCamera == null) return;
        
        Vector2 cameraPos = new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.y);
        
        float targetPan = 0f;
        float nearestDist = float.MaxValue;
        
        foreach (var evt in eventScheduler.GetActiveEvents())
        {
            if (!evt.isActive) continue;
            
            Vector2 toEvent = evt.position - cameraPos;
            float dist = toEvent.magnitude;
            
            // Weight by intensity - more intense events pull harder
            float effectiveDist = dist / (evt.currentIntensity + 0.1f);
            
            if (effectiveDist < nearestDist && dist > 5f)
            {
                nearestDist = effectiveDist;
                targetPan = Mathf.Clamp(toEvent.x / 30f, -1f, 1f) * spatialAmount;
            }
        }
        
        currentPanPosition = Mathf.Lerp(currentPanPosition, targetPan, Time.deltaTime * 2f);
        
        // Apply pan to non-base layers only
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
            
            float fadeTime = stingerFadeIn + stingerFadeOut;
            float elapsed = fadeTime - stingerTimer;
            
            float volume;
            if (elapsed < stingerFadeIn)
            {
                volume = (elapsed / stingerFadeIn) * stingerVolume * masterVolume;
            }
            else
            {
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
        
        foreach (var evt in activeEvents)
        {
            if (evt.isActive && !knownEvents.Contains(evt.eventName))
            {
                knownEvents.Add(evt.eventName);
                OnNewEventStarted(evt);
            }
        }
        
        knownEvents.RemoveWhere(name => !activeEvents.Exists(e => e.eventName == name && e.isActive));
    }
    
    void OnNewEventStarted(TurbulenceEvent evt)
    {
        TriggerStinger();
        Debug.Log($"[Ambience] New event: {evt.eventName} at ({evt.position.x:F0}, {evt.position.y:F0})");
    }
    
    public void TriggerStinger()
    {
        if (eventStingerClip == null || stingerSource == null) return;
        
        stingerSource.Stop();
        stingerSource.clip = eventStingerClip;
        stingerSource.volume = 0f;
        stingerSource.Play();
        
        stingerTimer = stingerFadeIn + stingerFadeOut;
        stingerPlaying = true;
    }
    
    /// <summary>
    /// Get current turbulence level (for external systems)
    /// </summary>
    public float GetTurbulenceLevel()
    {
        return smoothedTurbulenceLevel;
    }
    
    public void FadeToSilence(float duration)
    {
        StartCoroutine(FadeAllCoroutine(0f, duration));
    }
    
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
        
        GUILayout.BeginArea(new Rect(Screen.width - 300, 150, 290, 400));
        GUI.color = new Color(0, 0, 0, 0.85f);
        GUI.Box(new Rect(0, 0, 290, 400), "");
        GUI.color = Color.white;
        
        GUILayout.Label("=== AMBIENT SOUNDSCAPE ===");
        GUILayout.Space(5);
        
        // Turbulence display
        GUILayout.Label($"Active Events: {activeEventCount}");
        GUILayout.Label($"Turbulence (raw):      {currentTurbulenceLevel:F3}");
        GUILayout.Label($"Turbulence (smoothed): {smoothedTurbulenceLevel:F3}");
        
        // Turbulence bar
        Rect turbBar = GUILayoutUtility.GetRect(270, 12);
        GUI.color = new Color(0.15f, 0.15f, 0.15f);
        GUI.DrawTexture(turbBar, Texture2D.whiteTexture);
        
        // Color gradient based on level
        float t = Mathf.Clamp01(smoothedTurbulenceLevel);
        Color turbColor = Color.Lerp(new Color(0.2f, 0.6f, 0.2f), new Color(1f, 0.3f, 0.2f), t);
        GUI.color = turbColor;
        GUI.DrawTexture(new Rect(turbBar.x, turbBar.y, turbBar.width * Mathf.Clamp01(smoothedTurbulenceLevel), turbBar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        
        GUILayout.Space(10);
        GUILayout.Label($"Stereo Pan: {currentPanPosition:F2}");
        GUILayout.Label($"Stinger: {(stingerPlaying ? "PLAYING" : "idle")}");
        
        GUILayout.Space(10);
        GUILayout.Label("--- Layers ---");
        
        foreach (var layer in ambientLayers)
        {
            GUILayout.Space(3);
            GUILayout.Label($"{layer.name} [{layer.state}]");
            GUILayout.Label($"  Trigger: {layer.triggerThreshold:F2} | Release: {layer.releaseThreshold:F2}");
            
            // Envelope bar
            Rect barRect = GUILayoutUtility.GetRect(270, 10);
            GUI.color = new Color(0.2f, 0.2f, 0.2f);
            GUI.DrawTexture(barRect, Texture2D.whiteTexture);
            GUI.color = GetStateColor(layer.state);
            GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * layer.envelopeValue, barRect.height), Texture2D.whiteTexture);
            
            // Threshold markers
            GUI.color = new Color(1f, 1f, 0f, 0.8f);
            float triggerX = barRect.x + barRect.width * Mathf.Clamp01(layer.triggerThreshold);
            GUI.DrawTexture(new Rect(triggerX, barRect.y, 2, barRect.height), Texture2D.whiteTexture);
            
            GUI.color = new Color(0f, 1f, 1f, 0.6f);
            float releaseX = barRect.x + barRect.width * Mathf.Clamp01(layer.releaseThreshold);
            GUI.DrawTexture(new Rect(releaseX, barRect.y, 2, barRect.height), Texture2D.whiteTexture);
            
            GUI.color = Color.white;
        }
        
        GUILayout.EndArea();
    }
    
    Color GetStateColor(LayerState state)
    {
        switch (state)
        {
            case LayerState.Idle: return new Color(0.3f, 0.3f, 0.3f);
            case LayerState.FadingIn: return new Color(0.2f, 0.9f, 0.3f);
            case LayerState.Sustaining: return new Color(0.3f, 0.6f, 1f);
            case LayerState.FadingOut: return new Color(1f, 0.5f, 0.2f);
            default: return Color.white;
        }
    }
    
    void OnDestroy()
    {
        foreach (var layer in ambientLayers)
        {
            if (layer.source != null)
            {
                Destroy(layer.source.gameObject);
            }
        }
    }
}