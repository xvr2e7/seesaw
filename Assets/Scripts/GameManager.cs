using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages the game session: timing, scoring, state transitions.
/// 
/// The "convergence score" is an abstraction — the player optimizes a number
/// without understanding its human cost. Higher scores mean more "successful"
/// suppression of turbulence (i.e., dispersal of gatherings).
/// 
/// Prepares data hooks for Phase 6 (input recording) and Phase 7 (documentary replay).
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Initializing,   // Loading, setup
        Intro,          // Brief moment before gameplay starts
        Playing,        // Active gameplay
        Ending,         // Transition out of gameplay
        Complete        // Final score display, waiting for documentary
    }
    
    [Header("References")]
    public FlowSimulation flowSimulation;
    public TurbulentEventScheduler eventScheduler;
    public PlayerToolController playerTool;
    public CameraController cameraController;
    public AmbientSoundscapeController soundscape;
    public GameStateUI gameStateUI;
    
    [Header("Session Timing")]
    [Tooltip("Maximum session duration in seconds")]
    public float maxSessionDuration = 300f; // 5 minutes
    
    [Tooltip("Duration of intro phase")]
    public float introDuration = 3f;
    
    [Tooltip("Duration of ending transition")]
    public float endingDuration = 2f;
    
    [Header("Scoring")]
    [Tooltip("How often to sample divergence for scoring (seconds)")]
    public float scoreSampleInterval = 0.5f;
    
    [Tooltip("Weight for time-averaged divergence in final score")]
    [Range(0f, 1f)]
    public float averageDivergenceWeight = 0.6f;
    
    [Tooltip("Weight for peak divergence penalty in final score")]
    [Range(0f, 1f)]
    public float peakDivergenceWeight = 0.4f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // State
    private GameState currentState = GameState.Initializing;
    private float sessionTime = 0f;
    private float stateTimer = 0f;
    private bool sessionActive = false;
    
    // Scoring metrics
    private float accumulatedDivergence = 0f;
    private int divergenceSamples = 0;
    private float peakDivergence = 0f;
    private float lastSampleTime = 0f;
    private float finalScore = 0f;
    
    // Session statistics (for Phase 6/7)
    private SessionStatistics sessionStats;
    
    // Events for other systems to hook into
    public event Action OnSessionStart;
    public event Action OnSessionEnd;
    public event Action<GameState> OnStateChanged;
    public event Action<float> OnScoreCalculated;
    
    // Public accessors
    public GameState CurrentState => currentState;
    public float SessionTime => sessionTime;
    public float SessionProgress => Mathf.Clamp01(sessionTime / maxSessionDuration);
    public float CurrentDivergence => flowSimulation != null ? flowSimulation.CurrentDivergence : 0f;
    public float FinalScore => finalScore;
    public bool IsPlaying => currentState == GameState.Playing;
    public SessionStatistics Statistics => sessionStats;
    
    void Awake()
    {
        sessionStats = new SessionStatistics();
    }
    
    void Start()
    {
        FindReferences();
        
        // Start with intro after a brief initialization
        Invoke(nameof(BeginIntro), 0.5f);
    }
    
    void FindReferences()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
        
        if (eventScheduler == null)
            eventScheduler = FindObjectOfType<TurbulentEventScheduler>();
        
        if (playerTool == null)
            playerTool = FindObjectOfType<PlayerToolController>();
        
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();
        
        if (soundscape == null)
            soundscape = FindObjectOfType<AmbientSoundscapeController>();
        
        if (gameStateUI == null)
            gameStateUI = FindObjectOfType<GameStateUI>();
    }
    
    void Update()
    {
        stateTimer += Time.deltaTime;
        
        switch (currentState)
        {
            case GameState.Initializing:
                // Waiting for BeginIntro call
                break;
                
            case GameState.Intro:
                UpdateIntro();
                break;
                
            case GameState.Playing:
                UpdatePlaying();
                break;
                
            case GameState.Ending:
                UpdateEnding();
                break;
                
            case GameState.Complete:
                // Waiting for transition to documentary (Phase 7)
                break;
        }
    }
    
    void BeginIntro()
    {
        SetState(GameState.Intro);
    }
    
    void UpdateIntro()
    {
        if (stateTimer >= introDuration)
        {
            StartSession();
        }
    }
    
    void StartSession()
    {
        sessionTime = 0f;
        accumulatedDivergence = 0f;
        divergenceSamples = 0;
        peakDivergence = 0f;
        lastSampleTime = 0f;
        sessionActive = true;
        
        // Initialize session statistics
        sessionStats = new SessionStatistics
        {
            startTime = Time.time,
            sessionDuration = 0f
        };
        
        SetState(GameState.Playing);
        OnSessionStart?.Invoke();
        
        Debug.Log("[GameManager] Session started");
    }
    
    void UpdatePlaying()
    {
        sessionTime += Time.deltaTime;
        
        // Sample divergence for scoring
        if (sessionTime - lastSampleTime >= scoreSampleInterval)
        {
            SampleDivergence();
            lastSampleTime = sessionTime;
        }
        
        // Update session statistics
        UpdateSessionStatistics();
        
        // Check end conditions
        bool timeExpired = sessionTime >= maxSessionDuration;
        bool eventsComplete = CheckEventsComplete();
        
        if (timeExpired || eventsComplete)
        {
            EndSession(timeExpired ? "Time limit reached" : "All events resolved");
        }
    }
    
    void SampleDivergence()
    {
        float currentDiv = flowSimulation.CurrentDivergence;
        
        accumulatedDivergence += currentDiv;
        divergenceSamples++;
        
        if (currentDiv > peakDivergence)
        {
            peakDivergence = currentDiv;
        }
        
        // Track for statistics
        sessionStats.divergenceSamples.Add(new DivergenceSample
        {
            timestamp = sessionTime,
            value = currentDiv
        });
    }
    
    void UpdateSessionStatistics()
    {
        sessionStats.sessionDuration = sessionTime;
        
        // Track tool usage (will be expanded in Phase 6)
        if (playerTool != null)
        {
            var toolState = playerTool.GetToolState();
            if (toolState.isActive)
            {
                sessionStats.totalToolActiveTime += Time.deltaTime;
            }
        }
    }
    
    bool CheckEventsComplete()
    {
        if (eventScheduler == null) return false;
        
        // Check if all scripted events have completed
        var activeEvents = eventScheduler.GetActiveEvents();
        
        // Must have been playing for at least 60% of max duration
        // AND no active events remaining
        // AND past initial delay period
        bool minimumTimePassed = sessionTime >= maxSessionDuration * 0.6f;
        bool noActiveEvents = activeEvents.Count == 0;
        bool pastInitialDelay = sessionTime > eventScheduler.initialDelay + 30f;
        
        return minimumTimePassed && noActiveEvents && pastInitialDelay;
    }
    
    void EndSession(string reason)
    {
        if (!sessionActive) return;
        
        sessionActive = false;
        
        // Calculate final score
        CalculateFinalScore();
        
        // Finalize statistics
        sessionStats.endTime = Time.time;
        sessionStats.finalScore = finalScore;
        sessionStats.averageDivergence = divergenceSamples > 0 ? accumulatedDivergence / divergenceSamples : 0f;
        sessionStats.peakDivergence = peakDivergence;
        
        Debug.Log($"[GameManager] Session ended: {reason}");
        Debug.Log($"[GameManager] Final Score: {finalScore:F3}, Avg Divergence: {sessionStats.averageDivergence:F3}, Peak: {peakDivergence:F3}");
        
        OnSessionEnd?.Invoke();
        
        SetState(GameState.Ending);
    }
    
    void CalculateFinalScore()
    {
        if (divergenceSamples == 0)
        {
            finalScore = 1f;
            return;
        }
        
        float avgDivergence = accumulatedDivergence / divergenceSamples;
        
        // Convert divergence to a 0-1 "coherence" score
        // Lower divergence = higher score
        // Divergence typically ranges 0-2+, so we map accordingly
        float avgCoherence = Mathf.Clamp01(1f - avgDivergence * 0.5f);
        float peakPenalty = Mathf.Clamp01(1f - peakDivergence * 0.3f);
        
        // Weighted combination
        finalScore = avgCoherence * averageDivergenceWeight + peakPenalty * peakDivergenceWeight;
        
        // Apply subtle curve for more interesting distribution
        finalScore = Mathf.Pow(finalScore, 0.8f);
        
        OnScoreCalculated?.Invoke(finalScore);
    }
    
    void UpdateEnding()
    {
        if (stateTimer >= endingDuration)
        {
            SetState(GameState.Complete);
        }
    }
    
    void SetState(GameState newState)
    {
        if (currentState == newState) return;
        
        GameState previousState = currentState;
        currentState = newState;
        stateTimer = 0f;
        
        Debug.Log($"[GameManager] State: {previousState} -> {newState}");
        
        OnStateChanged?.Invoke(newState);
        
        // Notify UI
        if (gameStateUI != null)
        {
            gameStateUI.OnGameStateChanged(newState);
        }
    }
    
    /// <summary>
    /// Force end the session (for testing or emergency)
    /// </summary>
    public void ForceEndSession()
    {
        if (currentState == GameState.Playing)
        {
            EndSession("Forced end");
        }
    }
    
    /// <summary>
    /// Restart the session
    /// </summary>
    public void RestartSession()
    {
        // Reset event scheduler
        if (eventScheduler != null)
        {
            eventScheduler.ResetAllEvents();
        }
        
        SetState(GameState.Initializing);
        Invoke(nameof(BeginIntro), 0.5f);
    }
    
    /// <summary>
    /// Proceed to documentary phase (Phase 7)
    /// </summary>
    public void TransitionToDocumentary()
    {
        // Will be implemented in Phase 7
        Debug.Log("[GameManager] Documentary transition requested (not yet implemented)");
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 400, 300, 200));
        GUILayout.Box("Game Manager");
        GUILayout.Label($"State: {currentState}");
        GUILayout.Label($"Session Time: {sessionTime:F1}s / {maxSessionDuration:F0}s");
        GUILayout.Label($"Progress: {SessionProgress * 100:F0}%");
        GUILayout.Label($"Current Divergence: {CurrentDivergence:F3}");
        GUILayout.Label($"Avg Divergence: {(divergenceSamples > 0 ? accumulatedDivergence / divergenceSamples : 0):F3}");
        GUILayout.Label($"Peak Divergence: {peakDivergence:F3}");
        GUILayout.Label($"Samples: {divergenceSamples}");
        
        if (currentState == GameState.Complete)
        {
            GUILayout.Label($"FINAL SCORE: {finalScore:F3}");
        }
        GUILayout.EndArea();
    }
}

/// <summary>
/// Session statistics for Phase 6/7 replay and analysis
/// </summary>
[System.Serializable]
public class SessionStatistics
{
    public float startTime;
    public float endTime;
    public float sessionDuration;
    
    public float finalScore;
    public float averageDivergence;
    public float peakDivergence;
    
    public float totalToolActiveTime;
    public int totalDampeningActions;
    
    public List<DivergenceSample> divergenceSamples = new List<DivergenceSample>();
}

[System.Serializable]
public struct DivergenceSample
{
    public float timestamp;
    public float value;
}
