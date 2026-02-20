using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
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
        Intro,          // Brief moment before gameplay starts (legacy, kept for compat)
        Guidance,       // Interactive onboarding — no scoring, simulationTime frozen
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
    public DocumentaryController documentaryController;

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
    
    [Header("Guidance")]
    [Tooltip("Total pixel distance the mouse must travel to complete step 0")]
    public float guidanceMouseTravelThreshold = 200f;
    [Tooltip("World-unit radius used when spawning guidance events")]
    public float guidanceEventRadius = 10f;
    [Tooltip("Seconds the player must hover in the CIRCULAR zone (no LMB) to pass step 1")]
    public float guidanceHoverDuration = 3f;
    [Tooltip("Seconds the player must hold LMB over the SCATTER zone to pass step 2")]
    public float guidanceSuppressDuration = 1.5f;
    [Tooltip("Duration of 'learn which ones to leave' text — step 3")]
    public float guidanceStep3Duration = 3f;
    [Tooltip("Duration of 'session begins' text — step 4")]
    public float guidanceStep4Duration = 2f;

    [Header("Score Screen")]
    [Tooltip("Seconds to display the final score before transitioning to documentary")]
    public float scoreScreenDuration = 5f;

    [Header("Debug")]
    public bool showDebugInfo = false;

    // ─── Guidance runtime ──────────────────────────────────────────────────────
    private int   guidanceStep = 0;
    private bool  guidanceActive = false;
    private float guidanceStepTimer = 0f;

    // Step 0 — mouse travel
    private Vector2 guidanceLastMousePos;
    private float   guidanceMouseTravelAccum = 0f;

    // Step 1 — hover over circular event without LMB
    private float   guidanceHoverAccum = 0f;
    private Vector2 guidanceEventPosition;

    // Step 2 — suppress scatter with LMB
    private float   guidanceSuppressAccum = 0f;
    private Vector2 guidanceScatterPosition;

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

    // ─── Terminal intro ───────────────────────────────────────────────────────
    public enum TerminalPhase { None, Boot, Briefing }

    private bool          terminalIntroActive  = false;
    private TerminalPhase terminalPhase        = TerminalPhase.None;
    private bool          terminalAwaitSpace   = false;
    private bool          terminalSpacePressed = false;

    // Lines fully typed (shown above the active line)
    public System.Collections.Generic.List<string> TerminalLines     { get; } = new System.Collections.Generic.List<string>();
    // The line currently being typed (no trailing cursor — renderer adds it)
    public string         TerminalActiveLine   { get; private set; } = "";
    // True while a line is mid-type
    public bool           TerminalTyping       { get; private set; } = false;
    // How many completed lines belong to the boot phase
    public int            TerminalBootLineCount { get; private set; } = 0;

    public bool          IsTerminalIntro      => terminalIntroActive;
    public TerminalPhase CurrentTerminalPhase => terminalPhase;
    public bool          TerminalAwaitSpace   => terminalAwaitSpace;

    // Static flag — cheap check for any component that needs to suppress its own UI
    public static bool TerminalActive { get; private set; } = false;

    // ─── Pause / Save keys ────────────────────────────────────────────────────
    private const string PREF_HAS_SAVE      = "HasSavedGame";
    private const string PREF_SESSION_TIME  = "SavedSessionTime";
    private const string PREF_ACC_DIV       = "SavedAccDivergence";
    private const string PREF_DIV_SAMPLES   = "SavedDivergenceSamples";
    private const string PREF_PEAK_DIV      = "SavedPeakDivergence";
    private const string PREF_LAST_SAMPLE   = "SavedLastSampleTime";
    private const string PREF_BEST_SCORE    = "BestConvergenceScore";

    public static float GetBestScore() => PlayerPrefs.GetFloat("BestConvergenceScore", -1f);
    public static bool  HasBestScore()  => PlayerPrefs.HasKey("BestConvergenceScore");
    
    // Public accessors
    public GameState CurrentState => currentState;
    public float SessionTime => sessionTime;
    public float SessionProgress => Mathf.Clamp01(sessionTime / maxSessionDuration);
    public float CurrentDivergence => flowSimulation != null ? flowSimulation.CurrentDivergence : 0f;
    public float FinalScore => finalScore;
    public bool IsPlaying => currentState == GameState.Playing;
    public SessionStatistics Statistics => sessionStats;
    public int  GuidanceStep   => guidanceStep;
    public bool GuidanceActive => guidanceActive;

    public static bool HasSavedGame() => PlayerPrefs.GetInt("HasSavedGame", 0) == 1;

    /// <summary>Snapshot current session progress to PlayerPrefs.</summary>
    public static void SavePauseState()
    {
        // Find the instance and persist its runtime values
        var gm = FindObjectOfType<GameManager>();
        if (gm == null) return;
        PlayerPrefs.SetInt   ("HasSavedGame",          1);
        PlayerPrefs.SetFloat ("SavedSessionTime",      gm.sessionTime);
        PlayerPrefs.SetFloat ("SavedAccDivergence",    gm.accumulatedDivergence);
        PlayerPrefs.SetInt   ("SavedDivergenceSamples",gm.divergenceSamples);
        PlayerPrefs.SetFloat ("SavedPeakDivergence",   gm.peakDivergence);
        PlayerPrefs.SetFloat ("SavedLastSampleTime",   gm.lastSampleTime);
        PlayerPrefs.Save();
    }

    /// <summary>Clear saved game (called when a new game is started or session completes).</summary>
    public static void ClearSavedGame()
    {
        PlayerPrefs.DeleteKey("HasSavedGame");
        PlayerPrefs.Save();
    }
    
    void Awake()
    {
        sessionStats = new SessionStatistics();
    }
    
    void Start()
    {
        FindReferences();
        StartCoroutine(TerminalIntroCoroutine());
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

        if (documentaryController == null)
            documentaryController = FindObjectOfType<DocumentaryController>();
    }
    
    void Update()
    {
        if (terminalIntroActive)
        {
            if (terminalAwaitSpace && Input.GetKeyDown(KeyCode.Space))
                terminalSpacePressed = true;
        }

        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case GameState.Initializing:
                // Waiting for BeginGuidance call
                break;

            case GameState.Intro:
                UpdateIntro();
                break;

            case GameState.Guidance:
                UpdateGuidance();
                break;

            case GameState.Playing:
                UpdatePlaying();
                break;

            case GameState.Ending:
                UpdateEnding();
                break;

            case GameState.Complete:
                UpdateComplete();
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

    // ─── Terminal intro ────────────────────────────────────────────────────────

    // Type one line char-by-char into TerminalActiveLine; commit it to TerminalLines when done.
    IEnumerator TypeTerminalLine(string line, float charDelay)
    {
        TerminalTyping = true;
        TerminalActiveLine = "";
        for (int i = 0; i < line.Length; i++)
        {
            TerminalActiveLine = line.Substring(0, i + 1);
            yield return new WaitForSeconds(charDelay);
        }
        // Commit
        TerminalLines.Add(line);
        TerminalActiveLine = "";
        TerminalTyping = false;
    }

    IEnumerator TerminalIntroCoroutine()
    {
        terminalIntroActive   = true;
        TerminalActive        = true;
        terminalAwaitSpace    = false;
        terminalSpacePressed  = false;
        terminalPhase         = TerminalPhase.Boot;
        TerminalLines.Clear();
        TerminalActiveLine    = "";

        yield return new WaitForSeconds(0.6f);

        // ── Phase 1: Neural-net style boot ────────────────────────────────────
        var rng = new System.Random(UnityEngine.Random.Range(0, 99999));
        string RandHex(int len) {
            const string h = "0123456789abcdef";
            var sb = new System.Text.StringBuilder(len);
            for (int i = 0; i < len; i++) sb.Append(h[rng.Next(h.Length)]);
            return sb.ToString();
        }
        string RandF() => (rng.NextDouble() * 0.9 + 0.05).ToString("F6");

        var bootLines = new (string text, float pause)[]
        {
            ($"",                                                             0.08f),
            ($"  seed    0x{RandHex(8)}   dtype  float32   device  GPU:0",  0.10f),
            ($"  w[0]    mu={RandF()}   sigma={RandF()}",                   0.06f),
            ($"  w[1]    mu={RandF()}   sigma={RandF()}",                   0.06f),
            ($"  suppression gain  {(rng.NextDouble()*0.3+0.7):F4}   convergence threshold  {(rng.NextDouble()*0.04+0.01):F5}", 0.12f),
            ($"",                                                             0.06f),
            ($"  allocating agent buffer (1000) ...   [ OK ]",              0.06f),
            ($"  initializing optical flow kernel ...  [ OK ]",             0.06f),
            ($"  operator station online",                                   0.00f),
        };

        const float bootCharDelay = 0.013f;
        foreach (var (line, pause) in bootLines)
        {
            yield return StartCoroutine(TypeTerminalLine(line, bootCharDelay));
            if (pause > 0f) yield return new WaitForSeconds(pause);
        }

        yield return new WaitForSeconds(0.4f);

        // ── Phase 2: Briefing ─────────────────────────────────────────────────
        TerminalBootLineCount = TerminalLines.Count;
        terminalPhase         = TerminalPhase.Briefing;

        var briefLines = new (string text, float pause)[]
        {
            ($"  -----------------------------------------------",          0.15f),
            ($"",                                                             0.06f),
            ($"  imagine watching 1000 points of movement.",                0.20f),
            ($"  sometimes they drift.  sometimes they cluster.",            0.22f),
            ($"  your job is to keep them moving together.",                 0.22f),
            ($"",                                                             0.10f),
            ($"  when the field brightens, divergence is rising.",           0.18f),
            ($"  watch the bar at the top-left.",                            0.18f),
            ($"  hold it down.",                                             0.22f),
            ($"",                                                             0.07f),
            ($"  MOUSE         move the sensor over the field",             0.07f),
            ($"  LEFT CLICK    hold over a disturbance to suppress it",     0.18f),
            ($"",                                                             0.10f),
            ($"  not every disturbance needs to be suppressed.",             0.20f),
            ($"  learn to read which ones to leave.",                        0.22f),
            ($"",                                                             0.07f),
            ($"  -----------------------------------------------",          0.15f),
            ($"",                                                             0.10f),
            ($"  headphones recommended.",                                   0.30f),
        };

        const float briefCharDelay = 0.017f;
        foreach (var (line, pause) in briefLines)
        {
            yield return StartCoroutine(TypeTerminalLine(line, briefCharDelay));
            if (pause > 0f) yield return new WaitForSeconds(pause);
        }

        // Wait for SPACE
        terminalAwaitSpace   = true;
        terminalSpacePressed = false;
        yield return new WaitUntil(() => terminalSpacePressed);

        terminalAwaitSpace   = false;
        terminalIntroActive  = false;
        TerminalActive       = false;
        TerminalLines.Clear();
        TerminalActiveLine   = "";
        terminalPhase        = TerminalPhase.None;

        BeginGuidance();
    }

    // ─── Guidance phase ────────────────────────────────────────────────────────

    void BeginGuidance()
    {
        guidanceStep              = 0;
        guidanceActive            = true;
        guidanceStepTimer         = 0f;
        guidanceSkipGraceTimer    = 0f;
        guidanceMouseTravelAccum  = 0f;
        guidanceLastMousePos      = Input.mousePosition;
        guidanceHoverAccum        = 0f;
        guidanceSuppressAccum     = 0f;

        // Freeze simulationTime so scripted events don't fire during guidance
        if (eventScheduler != null)
            eventScheduler.SetGuidancePause(true);

        SetState(GameState.Guidance);
    }

    // How long after guidance begins before a keypress can skip it.
    // Prevents the SPACE from the terminal intro bleeding through.
    private float guidanceSkipGraceTimer = 0f;
    private const float GUIDANCE_SKIP_GRACE = 0.5f;

    void UpdateGuidance()
    {
        guidanceSkipGraceTimer += Time.deltaTime;

        // Any key skips guidance, but only after a brief grace period
        // so the SPACE from the terminal screen doesn't immediately skip it.
        if (guidanceSkipGraceTimer >= GUIDANCE_SKIP_GRACE && Input.anyKeyDown)
        {
            CompleteGuidance();
            return;
        }

        guidanceStepTimer += Time.deltaTime;

        switch (guidanceStep)
        {
            case 0: UpdateGuidanceStep0(); break;
            case 1: UpdateGuidanceStep1(); break;
            case 2: UpdateGuidanceStep2(); break;
            case 3: UpdateGuidanceStep3(); break;
            case 4: UpdateGuidanceStep4(); break;
        }
    }

    void UpdateGuidanceStep0()
    {
        Vector2 currentMousePos = Input.mousePosition;
        guidanceMouseTravelAccum += Vector2.Distance(currentMousePos, guidanceLastMousePos);
        guidanceLastMousePos = currentMousePos;

        if (guidanceMouseTravelAccum >= guidanceMouseTravelThreshold)
            AdvanceGuidanceToStep1();
    }

    void AdvanceGuidanceToStep1()
    {
        guidanceStep      = 1;
        guidanceStepTimer = 0f;
        guidanceHoverAccum = 0f;

        if (flowSimulation != null && eventScheduler != null)
        {
            Vector2 worldSize = flowSimulation.WorldSize;
            guidanceEventPosition = new Vector2(worldSize.x * 0.25f, 0f);
            eventScheduler.SpawnGuidanceEvent(
                TurbulenceEvent.PatternType.Circular,
                guidanceEventPosition,
                radius:   guidanceEventRadius,
                duration: 30f
            );
        }
    }

    void UpdateGuidanceStep1()
    {
        if (playerTool == null) return;

        var toolState = playerTool.GetToolState();
        float dist   = Vector2.Distance(toolState.worldPosition, guidanceEventPosition);
        bool inZone  = dist <= guidanceEventRadius;
        bool pressing = Input.GetMouseButton(0);

        if (inZone && !pressing)
            guidanceHoverAccum += Time.deltaTime;
        else
            guidanceHoverAccum = Mathf.Max(0f, guidanceHoverAccum - Time.deltaTime * 2f);

        if (guidanceHoverAccum >= guidanceHoverDuration)
            AdvanceGuidanceToStep2();
    }

    void AdvanceGuidanceToStep2()
    {
        guidanceStep          = 2;
        guidanceStepTimer     = 0f;
        guidanceSuppressAccum = 0f;

        if (flowSimulation != null && eventScheduler != null)
        {
            Vector2 worldSize = flowSimulation.WorldSize;
            guidanceScatterPosition = new Vector2(-worldSize.x * 0.2f, worldSize.y * 0.25f);
            eventScheduler.SpawnGuidanceEvent(
                TurbulenceEvent.PatternType.Scatter,
                guidanceScatterPosition,
                radius:   guidanceEventRadius,
                duration: 40f
            );
        }
    }

    void UpdateGuidanceStep2()
    {
        if (playerTool == null) return;

        var toolState = playerTool.GetToolState();
        float dist   = Vector2.Distance(toolState.worldPosition, guidanceScatterPosition);
        bool inZone  = dist <= guidanceEventRadius * 1.5f;
        bool holding = Input.GetMouseButton(0);

        if (inZone && holding)
            guidanceSuppressAccum += Time.deltaTime;
        else
            guidanceSuppressAccum = Mathf.Max(0f, guidanceSuppressAccum - Time.deltaTime);

        if (guidanceSuppressAccum >= guidanceSuppressDuration)
            AdvanceGuidanceToStep3();
    }

    void AdvanceGuidanceToStep3()
    {
        guidanceStep      = 3;
        guidanceStepTimer = 0f;

        if (eventScheduler != null)
            eventScheduler.ClearGuidanceEvents();
    }

    void UpdateGuidanceStep3()
    {
        if (guidanceStepTimer >= guidanceStep3Duration)
        {
            guidanceStep      = 4;
            guidanceStepTimer = 0f;
        }
    }

    void UpdateGuidanceStep4()
    {
        if (guidanceStepTimer >= guidanceStep4Duration)
            CompleteGuidance();
    }

    void CompleteGuidance()
    {
        guidanceActive = false;

        if (eventScheduler != null)
            eventScheduler.SetGuidancePause(false);

        StartSession();
    }

    void StartSession()
    {
        // Restore snapshot if one exists (player resumed from pause menu)
        if (PlayerPrefs.GetInt("HasSavedGame", 0) == 1)
        {
            sessionTime           = PlayerPrefs.GetFloat("SavedSessionTime",       0f);
            accumulatedDivergence = PlayerPrefs.GetFloat("SavedAccDivergence",     0f);
            divergenceSamples     = PlayerPrefs.GetInt  ("SavedDivergenceSamples", 0);
            peakDivergence        = PlayerPrefs.GetFloat("SavedPeakDivergence",    0f);
            lastSampleTime        = PlayerPrefs.GetFloat("SavedLastSampleTime",    0f);
            Debug.Log($"[GameManager] Resuming saved session at {sessionTime:F1}s");
        }
        else
        {
            sessionTime = 0f;
            accumulatedDivergence = 0f;
            divergenceSamples = 0;
            peakDivergence = 0f;
            lastSampleTime = 0f;
        }

        sessionActive = true;

        sessionStats = new SessionStatistics
        {
            startTime       = Time.time,
            sessionDuration = sessionTime
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

        // Per GAMEPLAY_DESCRIPTION.md: session ends early only when ALL THREE hold:
        //   1. All scripted events have completed
        //   2. At least 3 minutes (180s) have elapsed
        //   3. No events currently active
        bool allScriptedDone = eventScheduler.AllScriptedEventsComplete;
        bool minimumTimePassed = sessionTime >= 180f;
        bool noActiveEvents = eventScheduler.GetActiveEvents().Count == 0;

        return allScriptedDone && minimumTimePassed && noActiveEvents;
    }
    
    void EndSession(string reason)
    {
        if (!sessionActive) return;

        sessionActive = false;
        ClearSavedGame(); // session finished naturally — no save needed

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

        // Persist best score across sessions
        float prev = PlayerPrefs.GetFloat(PREF_BEST_SCORE, -1f);
        if (finalScore > prev)
        {
            PlayerPrefs.SetFloat(PREF_BEST_SCORE, finalScore);
            PlayerPrefs.Save();
        }
    }
    
    void UpdateEnding()
    {
        if (stateTimer >= endingDuration)
        {
            SetState(GameState.Complete);
        }
    }

    void UpdateComplete()
    {
        // During documentary replay the session loops automatically — no prompt
        if (IsInDocumentaryReplay)
        {
            if (stateTimer >= 1.5f)
                RestartForDocumentaryReplay();
            return;
        }

        // Wait until the score has had a moment to appear, then prompt for Space
        if (stateTimer >= 1.5f && Input.GetKeyDown(KeyCode.Space))
        {
            if (documentaryController != null)
                documentaryController.StartDocumentary();
            else
                TransitionToDocumentary(); // fallback if no documentary controller
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

        // Hide tool bar during documentary phase
        if (playerTool != null)
        {
            playerTool.SetDocumentaryPhase(newState == GameState.Complete);
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
        if (eventScheduler != null)
            eventScheduler.ResetAllEvents();

        SetState(GameState.Initializing);
        Invoke(nameof(BeginGuidance), 0.5f);
    }

    /// <summary>
    /// Restart the simulation directly into Playing — no terminal, no guidance.
    /// Used by DocumentaryController to loop the replay.
    /// </summary>
    public void RestartForDocumentaryReplay()
    {
        // Reset scoring state
        sessionTime           = 0f;
        accumulatedDivergence = 0f;
        divergenceSamples     = 0;
        peakDivergence        = 0f;
        lastSampleTime        = 0f;
        finalScore            = 0f;

        if (eventScheduler != null)
            eventScheduler.ResetAllEvents();

        StartSession();
    }

    public bool IsInDocumentaryReplay { get; set; } = false;
    
    /// <summary>
    /// Proceed to documentary phase — loads the Console scene with return flag set.
    /// </summary>
    public void TransitionToDocumentary()
    {
        Debug.Log("[GameManager] Transitioning to documentary (returning to menu)");
        ConsoleController.SetReturningFromGame();
        SceneManager.LoadScene("Console");
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
