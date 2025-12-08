using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Records all player interactions for documentary replay (Phase 7).
/// 
/// Captures:
/// - Cursor world position
/// - Camera position and viewport
/// - Tool state (active, radius, strength)
/// - Timestamps synchronized to session time
/// 
/// Recording starts when gameplay begins and stops when session ends.
/// Data is kept in memory for same-session replay.
/// </summary>
public class InputRecorder : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public PlayerToolController playerTool;
    public CameraController cameraController;
    public FlowSimulation flowSimulation;
    
    [Header("Recording Settings")]
    [Tooltip("Record every N frames (1 = every frame, 2 = every other, etc.)")]
    [Range(1, 10)]
    public int recordingInterval = 2;
    
    [Tooltip("Also record when tool state changes, regardless of interval")]
    public bool recordOnToolStateChange = true;
    
    [Tooltip("Maximum frames to store (memory limit)")]
    public int maxFrames = 50000;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    
    // Recording state
    private bool isRecording = false;
    private int frameCounter = 0;
    private InputFrame lastFrame;
    private bool lastToolActive = false;
    
    // Recorded data
    private List<InputFrame> recordedFrames = new List<InputFrame>();
    private RecordingMetadata metadata;
    
    // Public accessors
    public bool IsRecording => isRecording;
    public int FrameCount => recordedFrames.Count;
    public List<InputFrame> RecordedFrames => recordedFrames;
    public RecordingMetadata Metadata => metadata;
    
    void Start()
    {
        FindReferences();
        SubscribeToEvents();
    }
    
    void FindReferences()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
        
        if (playerTool == null)
            playerTool = FindObjectOfType<PlayerToolController>();
        
        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();
        
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();
    }
    
    void SubscribeToEvents()
    {
        if (gameManager != null)
        {
            gameManager.OnSessionStart += OnSessionStart;
            gameManager.OnSessionEnd += OnSessionEnd;
        }
    }
    
    void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnSessionStart -= OnSessionStart;
            gameManager.OnSessionEnd -= OnSessionEnd;
        }
    }
    
    void OnSessionStart()
    {
        StartRecording();
    }
    
    void OnSessionEnd()
    {
        StopRecording();
    }
    
    void Update()
    {
        if (!isRecording) return;
        
        frameCounter++;
        
        bool shouldRecord = (frameCounter % recordingInterval == 0);
        
        // Also record on tool state changes
        if (recordOnToolStateChange && playerTool != null)
        {
            var toolState = playerTool.GetToolState();
            if (toolState.isActive != lastToolActive)
            {
                shouldRecord = true;
                lastToolActive = toolState.isActive;
            }
        }
        
        if (shouldRecord && recordedFrames.Count < maxFrames)
        {
            RecordFrame();
        }
    }
    
    void RecordFrame()
    {
        InputFrame frame = new InputFrame();
        
        // Timestamp
        frame.timestamp = gameManager != null ? gameManager.SessionTime : Time.time;
        frame.frameNumber = frameCounter;
        
        // Tool state
        if (playerTool != null)
        {
            var toolState = playerTool.GetToolState();
            frame.cursorWorldPosition = toolState.worldPosition;
            frame.toolRadius = toolState.radius;
            frame.toolStrength = toolState.strength;
            frame.toolActive = toolState.isActive;
            frame.toolEnergy = playerTool.GetCurrentEnergy();
        }
        
        // Camera state
        if (cameraController != null)
        {
            frame.cameraPosition = new Vector2(
                cameraController.transform.position.x,
                cameraController.transform.position.y
            );
            frame.cameraViewport = cameraController.GetVisibleBounds();
        }
        
        // Flow state (for replay synchronization)
        if (flowSimulation != null)
        {
            frame.currentDivergence = flowSimulation.CurrentDivergence;
            frame.meanVelocity = flowSimulation.MeanVelocity;
        }
        
        recordedFrames.Add(frame);
        lastFrame = frame;
    }
    
    public void StartRecording()
    {
        if (isRecording) return;
        
        // Clear previous recording
        recordedFrames.Clear();
        frameCounter = 0;
        lastToolActive = false;
        
        // Initialize metadata
        metadata = new RecordingMetadata
        {
            recordingStartTime = Time.time,
            worldSize = flowSimulation != null ? flowSimulation.WorldSize : Vector2.one * 100f,
            agentCount = flowSimulation != null ? flowSimulation.AgentCount : 0
        };
        
        isRecording = true;
        
        Debug.Log("[InputRecorder] Recording started");
    }
    
    public void StopRecording()
    {
        if (!isRecording) return;
        
        isRecording = false;
        
        // Finalize metadata
        metadata.recordingEndTime = Time.time;
        metadata.totalFrames = recordedFrames.Count;
        metadata.sessionDuration = gameManager != null ? gameManager.SessionTime : 0f;
        
        // Calculate tool usage statistics
        float totalToolTime = 0f;
        int toolActivations = 0;
        bool wasActive = false;
        
        foreach (var frame in recordedFrames)
        {
            if (frame.toolActive)
            {
                totalToolTime += recordingInterval * Time.fixedDeltaTime;
                
                if (!wasActive)
                {
                    toolActivations++;
                }
            }
            wasActive = frame.toolActive;
        }
        
        metadata.totalToolActiveTime = totalToolTime;
        metadata.toolActivationCount = toolActivations;
        
        Debug.Log($"[InputRecorder] Recording stopped. Frames: {recordedFrames.Count}, Duration: {metadata.sessionDuration:F1}s");
    }
    
    /// <summary>
    /// Get frame at specific timestamp (for replay)
    /// </summary>
    public InputFrame GetFrameAtTime(float timestamp)
    {
        if (recordedFrames.Count == 0)
            return new InputFrame();
        
        // Binary search for closest frame
        int low = 0;
        int high = recordedFrames.Count - 1;
        
        while (low < high)
        {
            int mid = (low + high) / 2;
            
            if (recordedFrames[mid].timestamp < timestamp)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        
        return recordedFrames[low];
    }
    
    /// <summary>
    /// Get interpolated frame at specific timestamp
    /// </summary>
    public InputFrame GetInterpolatedFrameAtTime(float timestamp)
    {
        if (recordedFrames.Count == 0)
            return new InputFrame();
        
        if (recordedFrames.Count == 1)
            return recordedFrames[0];
        
        // Find bracketing frames
        int low = 0;
        int high = recordedFrames.Count - 1;
        
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            
            if (recordedFrames[mid].timestamp < timestamp)
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        
        InputFrame a = recordedFrames[low];
        InputFrame b = recordedFrames[high];
        
        float t = (timestamp - a.timestamp) / (b.timestamp - a.timestamp);
        t = Mathf.Clamp01(t);
        
        return InputFrame.Lerp(a, b, t);
    }
    
    /// <summary>
    /// Clear recorded data
    /// </summary>
    public void ClearRecording()
    {
        recordedFrames.Clear();
        frameCounter = 0;
        isRecording = false;
    }
    
    /// <summary>
    /// Get recording duration
    /// </summary>
    public float GetRecordingDuration()
    {
        if (recordedFrames.Count < 2)
            return 0f;
        
        return recordedFrames[recordedFrames.Count - 1].timestamp - recordedFrames[0].timestamp;
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 600, 300, 150));
        GUILayout.Box("Input Recorder");
        GUILayout.Label($"Recording: {isRecording}");
        GUILayout.Label($"Frames: {recordedFrames.Count} / {maxFrames}");
        GUILayout.Label($"Frame Counter: {frameCounter}");
        
        if (recordedFrames.Count > 0)
        {
            GUILayout.Label($"Duration: {GetRecordingDuration():F1}s");
            GUILayout.Label($"Last Tool Active: {lastFrame.toolActive}");
        }
        
        if (metadata != null)
        {
            GUILayout.Label($"Tool Activations: {metadata.toolActivationCount}");
        }
        GUILayout.EndArea();
    }
}

/// <summary>
/// Single frame of recorded input
/// </summary>
[System.Serializable]
public struct InputFrame
{
    // Timing
    public float timestamp;
    public int frameNumber;
    
    // Tool state
    public Vector2 cursorWorldPosition;
    public float toolRadius;
    public float toolStrength;
    public bool toolActive;
    public float toolEnergy;
    
    // Camera state
    public Vector2 cameraPosition;
    public Rect cameraViewport;
    
    // Flow state (for context)
    public float currentDivergence;
    public Vector2 meanVelocity;
    
    /// <summary>
    /// Interpolate between two frames
    /// </summary>
    public static InputFrame Lerp(InputFrame a, InputFrame b, float t)
    {
        return new InputFrame
        {
            timestamp = Mathf.Lerp(a.timestamp, b.timestamp, t),
            frameNumber = t < 0.5f ? a.frameNumber : b.frameNumber,
            
            cursorWorldPosition = Vector2.Lerp(a.cursorWorldPosition, b.cursorWorldPosition, t),
            toolRadius = Mathf.Lerp(a.toolRadius, b.toolRadius, t),
            toolStrength = Mathf.Lerp(a.toolStrength, b.toolStrength, t),
            toolActive = t < 0.5f ? a.toolActive : b.toolActive,
            toolEnergy = Mathf.Lerp(a.toolEnergy, b.toolEnergy, t),
            
            cameraPosition = Vector2.Lerp(a.cameraPosition, b.cameraPosition, t),
            cameraViewport = new Rect(
                Mathf.Lerp(a.cameraViewport.x, b.cameraViewport.x, t),
                Mathf.Lerp(a.cameraViewport.y, b.cameraViewport.y, t),
                Mathf.Lerp(a.cameraViewport.width, b.cameraViewport.width, t),
                Mathf.Lerp(a.cameraViewport.height, b.cameraViewport.height, t)
            ),
            
            currentDivergence = Mathf.Lerp(a.currentDivergence, b.currentDivergence, t),
            meanVelocity = Vector2.Lerp(a.meanVelocity, b.meanVelocity, t)
        };
    }
}

/// <summary>
/// Metadata about the recording session
/// </summary>
[System.Serializable]
public class RecordingMetadata
{
    public float recordingStartTime;
    public float recordingEndTime;
    public float sessionDuration;
    public int totalFrames;
    
    public Vector2 worldSize;
    public int agentCount;
    
    public float totalToolActiveTime;
    public int toolActivationCount;
}
