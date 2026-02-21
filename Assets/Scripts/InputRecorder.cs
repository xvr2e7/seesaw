using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records player tool actions at 10 Hz during a gameplay session.
/// Consumed by DocumentaryController to replay the player's inputs
/// over the left-panel simulation during the documentary phase.
/// </summary>
public class InputRecorder : MonoBehaviour
{
    [Tooltip("How often to capture a frame (seconds). 0.1 = 10 Hz.")]
    public float sampleInterval = 0.1f;

    private readonly List<InputFrame> _frames = new List<InputFrame>(4096);
    private float _nextSampleTime = 0f;
    private bool  _recording      = false;

    public IReadOnlyList<InputFrame> Frames      => _frames;
    public bool                      HasRecording => _frames.Count > 0;
    public float                     Duration     => _frames.Count > 0 ? _frames[_frames.Count - 1].time : 0f;

    public void StartRecording()
    {
        _frames.Clear();
        _nextSampleTime = 0f;
        _recording      = true;
        Debug.Log("[InputRecorder] Recording started");
    }

    public void StopRecording()
    {
        _recording = false;
        Debug.Log($"[InputRecorder] Recording stopped. Frames: {_frames.Count}, Duration: {Duration:F1}s");
    }

    /// <summary>
    /// Called every frame from PlayerToolController.
    /// Stores a frame if the sample interval has elapsed.
    /// </summary>
    public void TrySample(float sessionTime, Vector2 worldPos, bool held, float radius, float strength, float convergenceScore)
    {
        if (!_recording) return;
        if (sessionTime < _nextSampleTime) return;

        _frames.Add(new InputFrame
        {
            time             = sessionTime,
            worldPos         = worldPos,
            held             = held,
            radius           = radius,
            strength         = strength,
            convergenceScore = convergenceScore
        });

        _nextSampleTime = sessionTime + sampleInterval;
    }
}

[System.Serializable]
public struct InputFrame
{
    public float   time;             // session time when recorded
    public Vector2 worldPos;         // tool world position
    public bool    held;             // LMB held (scan active)
    public float   radius;           // scan radius at this moment
    public float   strength;         // effective dampening strength
    public float   convergenceScore; // convergence score at this moment (0–1)
}
