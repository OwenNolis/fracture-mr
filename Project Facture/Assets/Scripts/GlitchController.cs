using UnityEngine;

/// <summary>
/// Controls the Glitch shader effect parameters.
/// This script can be controlled externally by the OverseerSystem to create
/// evolving glitch effects during the AI takeover.
/// </summary>
public class GlitchController : MonoBehaviour
{
    [Header("Material Reference")]
    [SerializeField]
    [Tooltip("The material using the Glitch shader. If not assigned, will try to find 'Glitch' material.")]
    public Material material;

    [Header("Effect Parameters")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Amount of noise/static overlay (0 = none, 1 = maximum).")]
    public float noiseAmount = 0f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Strength of the glitch distortion effect (0 = none, 1 = maximum).")]
    public float glitchStrength = 0f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Intensity of scanline effect (0 = none, 1 = maximum).")]
    public float scanLineStrength = 0f;

    [Header("Auto-Glitch Settings")]
    [SerializeField]
    [Tooltip("If true, enables random micro-glitches independent of OverseerSystem.")]
    private bool enableAutoGlitch = false;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Base intensity for auto-glitch mode.")]
    private float autoGlitchIntensity = 0.1f;

    [SerializeField]
    [Tooltip("How often auto-glitches occur (in seconds).")]
    private float autoGlitchInterval = 5f;

    // Shader property IDs for performance
    private static readonly int NoiseAmountID = Shader.PropertyToID("_NoiseAmount");
    private static readonly int GlitchStrengthID = Shader.PropertyToID("_GlitchStrength");
    private static readonly int ScanLineStrengthID = Shader.PropertyToID("_ScanLineStrength");

    // Internal state
    private float nextAutoGlitchTime = 0f;
    private float autoGlitchTimer = 0f;
    private bool isAutoGlitching = false;
    private float savedNoiseAmount = 0f;
    private float savedGlitchStrength = 0f;
    private float savedScanLineStrength = 0f;

    private void Awake()
    {
        // Try to find the Glitch material if not assigned
        if (material == null)
        {
            material = Resources.Load<Material>("Glitch");

            // If not in Resources, try to find by name in loaded materials
            if (material == null)
            {
                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat.name.Contains("Glitch"))
                        {
                            material = mat;
                            break;
                        }
                    }
                }
            }
        }

        if (material == null)
        {
            Debug.LogWarning("[GlitchController] No material assigned! Please assign the Glitch material in the inspector.");
        }
    }

    private void Update()
    {
        if (material == null) return;

        // Handle auto-glitch mode
        if (enableAutoGlitch)
        {
            HandleAutoGlitch();
        }

        // Apply current values to shader
        material.SetFloat(NoiseAmountID, noiseAmount);
        material.SetFloat(GlitchStrengthID, glitchStrength);
        material.SetFloat(ScanLineStrengthID, scanLineStrength);
    }

    private void HandleAutoGlitch()
    {
        // Check if it's time for a new auto-glitch
        if (!isAutoGlitching && Time.time >= nextAutoGlitchTime)
        {
            StartAutoGlitch();
        }

        // Update auto-glitch
        if (isAutoGlitching)
        {
            autoGlitchTimer -= Time.deltaTime;
            if (autoGlitchTimer <= 0f)
            {
                EndAutoGlitch();
            }
        }
    }

    private void StartAutoGlitch()
    {
        isAutoGlitching = true;
        autoGlitchTimer = Random.Range(0.1f, 0.3f);

        // Save current values
        savedNoiseAmount = noiseAmount;
        savedGlitchStrength = glitchStrength;
        savedScanLineStrength = scanLineStrength;

        // Apply auto-glitch boost
        noiseAmount = Mathf.Clamp01(noiseAmount + autoGlitchIntensity * Random.Range(0.5f, 1.5f));
        glitchStrength = Mathf.Clamp01(glitchStrength + autoGlitchIntensity * Random.Range(0.3f, 1f));
        scanLineStrength = Mathf.Clamp01(scanLineStrength + autoGlitchIntensity * Random.Range(0.2f, 0.8f));
    }

    private void EndAutoGlitch()
    {
        isAutoGlitching = false;
        nextAutoGlitchTime = Time.time + autoGlitchInterval + Random.Range(-autoGlitchInterval * 0.3f, autoGlitchInterval * 0.5f);

        // Restore saved values
        noiseAmount = savedNoiseAmount;
        glitchStrength = savedGlitchStrength;
        scanLineStrength = savedScanLineStrength;
    }

    /// <summary>
    /// Sets all glitch parameters at once.
    /// </summary>
    public void SetGlitchValues(float noise, float glitch, float scanLines)
    {
        noiseAmount = Mathf.Clamp01(noise);
        glitchStrength = Mathf.Clamp01(glitch);
        scanLineStrength = Mathf.Clamp01(scanLines);
    }

    /// <summary>
    /// Resets all glitch effects to zero.
    /// </summary>
    public void ResetGlitch()
    {
        noiseAmount = 0f;
        glitchStrength = 0f;
        scanLineStrength = 0f;
    }

    /// <summary>
    /// Sets all glitch effects to maximum.
    /// </summary>
    public void MaxGlitch()
    {
        noiseAmount = 1f;
        glitchStrength = 1f;
        scanLineStrength = 1f;
    }

    /// <summary>
    /// Lerp all values toward target values.
    /// </summary>
    public void LerpTowards(float targetNoise, float targetGlitch, float targetScanLines, float speed)
    {
        noiseAmount = Mathf.Lerp(noiseAmount, targetNoise, Time.deltaTime * speed);
        glitchStrength = Mathf.Lerp(glitchStrength, targetGlitch, Time.deltaTime * speed);
        scanLineStrength = Mathf.Lerp(scanLineStrength, targetScanLines, Time.deltaTime * speed);
    }

    private void OnDisable()
    {
        // Reset shader values when disabled to prevent persistent glitch
        if (material != null)
        {
            material.SetFloat(NoiseAmountID, 0f);
            material.SetFloat(GlitchStrengthID, 0f);
            material.SetFloat(ScanLineStrengthID, 0f);
        }
    }

    private void OnDestroy()
    {
        // Ensure clean state when destroyed
        if (material != null)
        {
            material.SetFloat(NoiseAmountID, 0f);
            material.SetFloat(GlitchStrengthID, 0f);
            material.SetFloat(ScanLineStrengthID, 0f);
        }
    }
}
