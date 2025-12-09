using UnityEngine;

/// <summary>
/// Generates a procedural preview texture for the Console scene's active feed.
/// Creates an HSV optical flow-like visualization similar to the actual game.
/// This is used as a placeholder when no actual render texture is available.
/// </summary>
public class PreviewTextureGenerator : MonoBehaviour
{
    [Header("Texture Settings")]
    public int width = 480;
    public int height = 270;
    
    [Header("Animation")]
    public bool animate = true;
    public float animationSpeed = 0.5f;
    
    [Header("Colors")]
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
    public float saturation = 0.7f;
    public float brightness = 0.8f;
    
    [Header("Output")]
    public Texture2D generatedTexture;
    
    // Internal
    private Color[] pixels;
    private float animationTime = 0f;
    
    void Awake()
    {
        GenerateTexture();
    }
    
    void Update()
    {
        if (animate)
        {
            animationTime += Time.deltaTime * animationSpeed;
            UpdateTexture();
        }
    }
    
    public void GenerateTexture()
    {
        generatedTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        generatedTexture.filterMode = FilterMode.Bilinear;
        generatedTexture.wrapMode = TextureWrapMode.Clamp;
        generatedTexture.name = "LaminarFlowPreview";
        
        pixels = new Color[width * height];
        
        UpdateTexture();
    }
    
    void UpdateTexture()
    {
        if (generatedTexture == null || pixels == null) return;
        
        float time = animationTime;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;
                float v = (float)y / height;
                
                // Create flowing HSV pattern similar to optical flow visualization
                Color color = CalculateFlowColor(u, v, time);
                
                pixels[y * width + x] = color;
            }
        }
        
        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply();
    }
    
    Color CalculateFlowColor(float u, float v, float time)
    {
        // Create swirling flow patterns
        float cx = 0.5f + 0.2f * Mathf.Sin(time * 0.3f);
        float cy = 0.5f + 0.2f * Mathf.Cos(time * 0.4f);
        
        float dx = u - cx;
        float dy = v - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        
        // Base flow angle (circular motion)
        float baseAngle = Mathf.Atan2(dy, dx);
        
        // Add some noise/variation
        float noise1 = Mathf.PerlinNoise(u * 5f + time, v * 5f) * 2f - 1f;
        float noise2 = Mathf.PerlinNoise(u * 3f, v * 3f + time * 0.5f) * 2f - 1f;
        
        // Combine into final flow direction
        float flowAngle = baseAngle + noise1 * 0.5f + noise2 * 0.3f;
        
        // Add some turbulent patches
        float turbulence = 0f;
        
        // Turbulent region 1
        float t1x = 0.3f + 0.1f * Mathf.Sin(time * 0.7f);
        float t1y = 0.6f + 0.1f * Mathf.Cos(time * 0.5f);
        float t1dist = Mathf.Sqrt((u - t1x) * (u - t1x) + (v - t1y) * (v - t1y));
        if (t1dist < 0.15f)
        {
            turbulence += (1f - t1dist / 0.15f) * 0.5f;
            flowAngle += Mathf.Sin(time * 3f + dist * 10f) * turbulence;
        }
        
        // Turbulent region 2
        float t2x = 0.7f + 0.1f * Mathf.Cos(time * 0.6f);
        float t2y = 0.4f + 0.1f * Mathf.Sin(time * 0.8f);
        float t2dist = Mathf.Sqrt((u - t2x) * (u - t2x) + (v - t2y) * (v - t2y));
        if (t2dist < 0.12f)
        {
            turbulence += (1f - t2dist / 0.12f) * 0.4f;
            flowAngle += Mathf.Cos(time * 4f + dist * 8f) * turbulence;
        }
        
        // Convert flow angle to hue (0-1)
        float hue = (flowAngle / (2f * Mathf.PI)) + 0.5f;
        hue = hue - Mathf.Floor(hue); // Wrap to 0-1
        
        // Magnitude affects saturation and value
        float magnitude = 0.5f + 0.5f * Mathf.Sin(dist * 5f - time) + turbulence;
        magnitude = Mathf.Clamp01(magnitude);
        
        float sat = Mathf.Lerp(0.3f, saturation, magnitude);
        float val = Mathf.Lerp(0.2f, brightness, magnitude);
        
        // HSV to RGB
        Color rgb = HSVToRGB(hue, sat, val);
        
        // Add vignette
        float vignette = 1f - ((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 0.5f;
        rgb *= vignette;
        
        // Blend with background for low-velocity areas
        rgb = Color.Lerp(backgroundColor, rgb, magnitude * 0.8f + 0.2f);
        
        return rgb;
    }
    
    Color HSVToRGB(float h, float s, float v)
    {
        h = h - Mathf.Floor(h);
        
        float c = v * s;
        float x = c * (1f - Mathf.Abs((h * 6f) % 2f - 1f));
        float m = v - c;
        
        float r, g, b;
        
        if (h < 1f / 6f)
        {
            r = c; g = x; b = 0f;
        }
        else if (h < 2f / 6f)
        {
            r = x; g = c; b = 0f;
        }
        else if (h < 3f / 6f)
        {
            r = 0f; g = c; b = x;
        }
        else if (h < 4f / 6f)
        {
            r = 0f; g = x; b = c;
        }
        else if (h < 5f / 6f)
        {
            r = x; g = 0f; b = c;
        }
        else
        {
            r = c; g = 0f; b = x;
        }
        
        return new Color(r + m, g + m, b + m, 1f);
    }
    
    void OnDestroy()
    {
        if (generatedTexture != null)
        {
            Destroy(generatedTexture);
        }
    }
    
    /// <summary>
    /// Get the generated texture (for external use)
    /// </summary>
    public Texture2D GetTexture()
    {
        if (generatedTexture == null)
        {
            GenerateTexture();
        }
        return generatedTexture;
    }
    
    /// <summary>
    /// Create a static (non-animated) preview texture
    /// </summary>
    public static Texture2D CreateStaticPreview(int w, int h)
    {
        GameObject temp = new GameObject("TempPreviewGen");
        PreviewTextureGenerator gen = temp.AddComponent<PreviewTextureGenerator>();
        gen.width = w;
        gen.height = h;
        gen.animate = false;
        gen.GenerateTexture();
        
        Texture2D result = gen.generatedTexture;
        gen.generatedTexture = null; // Prevent destruction
        
        Destroy(temp);
        
        return result;
    }
}
