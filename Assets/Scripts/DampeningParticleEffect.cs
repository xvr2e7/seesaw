using UnityEngine;

/// <summary>
/// Draws an integrated scan-line sweep across the tool bounding box whenever
/// the player activates SCAN, PULSE, or LOCK. Replaces the disconnected
/// outward-burst particle effect.
///
/// Subscribes to PlayerToolController.OnScanLineRequested.
/// Renders using GL immediate mode in OnRenderObject.
/// </summary>
public class DampeningParticleEffect : MonoBehaviour
{
    [Header("References")]
    public FlowSimulation flowSimulation;   // kept for legacy API compatibility
    public PlayerToolController playerTool;

    [Header("Scan Line")]
    [Tooltip("Colour of the main scan line")]
    public Color scanLineColor = new Color(0.68f, 0.72f, 0.80f, 0.85f);

    [Tooltip("Soft thickness: number of offset lines drawn on each side")]
    [Range(0, 3)]
    public int scanLineThickness = 2;

    // ── Runtime sweep state ───────────────────────────────────────────────────
    private bool    _sweepActive   = false;
    private float   _sweepProgress = 0f;   // 0 = top of box, 1 = bottom
    private float   _sweepDuration = 0.25f;
    private Vector2 _sweepBoxMin;
    private Vector2 _sweepBoxMax;

    // ── GL material ───────────────────────────────────────────────────────────
    private Material _glMaterial;

    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        if (flowSimulation == null)
            flowSimulation = FindObjectOfType<FlowSimulation>();

        if (playerTool == null)
            playerTool = FindObjectOfType<PlayerToolController>();

        CreateGLMaterial();

        if (playerTool != null)
            playerTool.OnScanLineRequested += HandleScanLineRequest;
        else
            Debug.LogWarning("[DampeningParticleEffect] No PlayerToolController found — scan line disabled.");
    }

    void OnDestroy()
    {
        if (playerTool != null)
            playerTool.OnScanLineRequested -= HandleScanLineRequest;

        if (_glMaterial != null)
            Destroy(_glMaterial);
    }

    void CreateGLMaterial()
    {
        // Hidden/Internal-Colored supports alpha blending via GL immediate mode
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            Debug.LogWarning("[DampeningParticleEffect] Hidden/Internal-Colored shader not found.");
            return;
        }

        _glMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _glMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMaterial.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _glMaterial.SetInt("_ZWrite",   0);
    }

    // ── Sweep lifecycle ───────────────────────────────────────────────────────

    void HandleScanLineRequest(Vector2 boxMin, Vector2 boxMax, float duration)
    {
        // Ignore stacked requests — one sweep at a time.
        if (_sweepActive) return;

        _sweepActive   = true;
        _sweepProgress = 0f;
        _sweepDuration = duration;
        _sweepBoxMin   = boxMin;
        _sweepBoxMax   = boxMax;
    }

    void Update()
    {
        if (!_sweepActive) return;

        _sweepProgress += Time.deltaTime / _sweepDuration;

        if (_sweepProgress >= 1f)
        {
            _sweepProgress = 0f;
            _sweepActive   = false;
        }
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    void OnRenderObject()
    {
        if (!_sweepActive) return;
        if (_glMaterial == null) return;
        if (playerTool == null) return;

        Camera cam = playerTool.mainCamera;
        if (cam == null || Camera.current != cam) return;

        // Interpolate Y from top (boxMax.y) to bottom (boxMin.y)
        float worldY = Mathf.Lerp(_sweepBoxMax.y, _sweepBoxMin.y, _sweepProgress);

        // Screen positions of the line's endpoints
        Vector3 leftWorld  = new Vector3(_sweepBoxMin.x, worldY, 0f);
        Vector3 rightWorld = new Vector3(_sweepBoxMax.x, worldY, 0f);

        Vector3 leftScreen  = cam.WorldToScreenPoint(leftWorld);
        Vector3 rightScreen = cam.WorldToScreenPoint(rightWorld);

        // Alpha envelope: sin arc so the line fades in from the top and out at the bottom
        float alpha = Mathf.Sin(_sweepProgress * Mathf.PI);
        Color mainColor = new Color(
            scanLineColor.r, scanLineColor.g, scanLineColor.b,
            scanLineColor.a * alpha);
        Color softColor = new Color(
            scanLineColor.r, scanLineColor.g, scanLineColor.b,
            scanLineColor.a * alpha * 0.35f);

        GL.PushMatrix();
        GL.LoadPixelMatrix();
        _glMaterial.SetPass(0);

        // Main line
        GL.Begin(GL.LINES);
        GL.Color(mainColor);
        GL.Vertex3(leftScreen.x,  leftScreen.y,  0f);
        GL.Vertex3(rightScreen.x, rightScreen.y, 0f);
        GL.End();

        // Soft fringe lines above and below for perceived thickness
        for (int offset = 1; offset <= scanLineThickness; offset++)
        {
            float fade = 1f - (float)offset / (scanLineThickness + 1f);
            Color fringeColor = new Color(
                scanLineColor.r, scanLineColor.g, scanLineColor.b,
                softColor.a * fade);

            GL.Begin(GL.LINES);
            GL.Color(fringeColor);
            GL.Vertex3(leftScreen.x,  leftScreen.y  + offset, 0f);
            GL.Vertex3(rightScreen.x, rightScreen.y + offset, 0f);
            GL.End();

            GL.Begin(GL.LINES);
            GL.Color(fringeColor);
            GL.Vertex3(leftScreen.x,  leftScreen.y  - offset, 0f);
            GL.Vertex3(rightScreen.x, rightScreen.y - offset, 0f);
            GL.End();
        }

        GL.PopMatrix();
    }
}
