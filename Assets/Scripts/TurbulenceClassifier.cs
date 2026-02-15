using UnityEngine;

/// <summary>
/// Detects and classifies turbulence events at a given position.
/// Provides the event name for minimal UI display.
/// </summary>
public class TurbulenceClassifier : MonoBehaviour
{
    [Header("References")]
    public TurbulentEventScheduler scheduler;

    [Header("Detection Settings")]
    [Tooltip("How far inside event radius to trigger detection")]
    [Range(0f, 1f)]
    public float detectionThreshold = 0.8f;

    [Tooltip("Minimum intensity for event to be detectable")]
    [Range(0f, 1f)]
    public float minimumIntensity = 0.3f;

    // Cached detection result
    private TurbulenceEvent currentEvent;
    private string currentEventName;
    private float lastDetectionTime;
    private const float DETECTION_CACHE_TIME = 0.1f;

    void Start()
    {
        if (scheduler == null)
        {
            scheduler = FindObjectOfType<TurbulentEventScheduler>();
        }

        if (scheduler == null)
        {
            Debug.LogWarning("[TurbulenceClassifier] No TurbulentEventScheduler found!");
        }
    }

    /// <summary>
    /// Detect turbulence event at position. Returns event name or null if none detected.
    /// </summary>
    public string DetectEventAt(Vector2 position)
    {
        if (scheduler == null) return null;

        // Use cached result if recent
        if (Time.time - lastDetectionTime < DETECTION_CACHE_TIME)
        {
            return currentEventName;
        }

        var activeEvents = scheduler.GetActiveEvents();
        if (activeEvents == null || activeEvents.Count == 0)
        {
            currentEvent = null;
            currentEventName = null;
            lastDetectionTime = Time.time;
            return null;
        }

        // Find strongest event at this position
        TurbulenceEvent strongestEvent = null;
        float strongestInfluence = 0f;

        foreach (var evt in activeEvents)
        {
            if (!evt.isActive || evt.currentIntensity < minimumIntensity)
                continue;

            // Check if position is within detection radius
            float distance = Vector2.Distance(position, evt.position);
            float effectiveRadius = evt.radius * detectionThreshold;

            if (distance <= effectiveRadius)
            {
                // Calculate influence (closer = stronger)
                float normalizedDist = distance / effectiveRadius;
                float influence = (1f - normalizedDist) * evt.currentIntensity;

                if (influence > strongestInfluence)
                {
                    strongestInfluence = influence;
                    strongestEvent = evt;
                }
            }
        }

        // Update cache
        currentEvent = strongestEvent;
        currentEventName = strongestEvent != null ? strongestEvent.eventName : null;
        lastDetectionTime = Time.time;

        return currentEventName;
    }

    /// <summary>
    /// Get the currently detected event (may be null)
    /// </summary>
    public TurbulenceEvent GetCurrentEvent()
    {
        return currentEvent;
    }

    /// <summary>
    /// Get the intensity of turbulence at position (0 to 1)
    /// </summary>
    public float GetTurbulenceIntensityAt(Vector2 position)
    {
        if (scheduler == null) return 0f;

        return scheduler.GetTurbulenceIntensityAt(position);
    }

    /// <summary>
    /// Check if position is within any turbulence event
    /// </summary>
    public bool IsInTurbulence(Vector2 position)
    {
        return DetectEventAt(position) != null;
    }
}
