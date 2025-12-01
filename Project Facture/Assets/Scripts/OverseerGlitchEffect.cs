using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.VRTemplate
{
    /// <summary>
    /// Screen-space glitch effects that can be controlled by the OverseerSystem.
    /// Attach this to the main camera or a post-processing volume.
    /// </summary>
    public class OverseerGlitchEffect : MonoBehaviour
    {
        [Header("Overseer Reference")]
        [SerializeField]
        private OverseerSystem m_OverseerSystem;

        [Header("Chromatic Aberration")]
        [SerializeField]
        [Tooltip("Enable chromatic aberration effect.")]
        private bool m_EnableChromaticAberration = true;

        [SerializeField]
        [Tooltip("Maximum chromatic aberration intensity.")]
        [Range(0f, 1f)]
        private float m_MaxChromaticIntensity = 0.5f;

        [Header("Screen Distortion")]
        [SerializeField]
        [Tooltip("Enable screen distortion.")]
        private bool m_EnableDistortion = true;

        [SerializeField]
        [Tooltip("Maximum distortion amount.")]
        [Range(0f, 0.1f)]
        private float m_MaxDistortion = 0.02f;

        [Header("Scanlines")]
        [SerializeField]
        [Tooltip("Enable scanline effect.")]
        private bool m_EnableScanlines = true;

        [SerializeField]
        [Tooltip("Scanline density.")]
        [Range(100f, 1000f)]
        private float m_ScanlineDensity = 300f;

        [Header("Flicker")]
        [SerializeField]
        [Tooltip("Enable screen flicker.")]
        private bool m_EnableFlicker = true;

        [SerializeField]
        [Tooltip("Maximum flicker intensity.")]
        [Range(0f, 0.5f)]
        private float m_MaxFlickerIntensity = 0.2f;

        [Header("Color Shift")]
        [SerializeField]
        [Tooltip("Enable color shift/corruption.")]
        private bool m_EnableColorShift = true;

        [SerializeField]
        [Tooltip("Color to shift towards during glitches.")]
        private Color m_GlitchColor = new Color(1f, 0f, 0f, 0.1f);

        [Header("Static Noise")]
        [SerializeField]
        [Tooltip("Enable static noise overlay.")]
        private bool m_EnableNoise = true;

        [SerializeField]
        [Tooltip("Maximum noise intensity.")]
        [Range(0f, 1f)]
        private float m_MaxNoiseIntensity = 0.3f;

        // Internal state
        private float m_CurrentIntensity = 0f;
        private Material m_GlitchMaterial;
        private bool m_IsGlitching = false;
        private float m_GlitchTimer = 0f;
        private float m_NextGlitchTime = 0f;

        // Shader property IDs
        private static readonly int ChromaticIntensity = Shader.PropertyToID("_ChromaticIntensity");
        private static readonly int DistortionAmount = Shader.PropertyToID("_DistortionAmount");
        private static readonly int ScanlineIntensity = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int FlickerIntensity = Shader.PropertyToID("_FlickerIntensity");
        private static readonly int NoiseIntensity = Shader.PropertyToID("_NoiseIntensity");
        private static readonly int GlitchColorProperty = Shader.PropertyToID("_GlitchColor");
        private static readonly int TimeProperty = Shader.PropertyToID("_Time");

        private void Awake()
        {
            if (m_OverseerSystem == null)
            {
                m_OverseerSystem = FindFirstObjectByType<OverseerSystem>();
            }

            // Create glitch material if shader exists
            Shader glitchShader = Shader.Find("Hidden/OverseerGlitch");
            if (glitchShader != null)
            {
                m_GlitchMaterial = new Material(glitchShader);
            }
        }

        private void Update()
        {
            if (m_OverseerSystem == null) return;

            float progress = m_OverseerSystem.GetProgress();
            m_CurrentIntensity = progress;

            // Trigger random glitch bursts
            if (Time.time >= m_NextGlitchTime && progress > 0.1f)
            {
                TriggerGlitchBurst();
                // More frequent glitches as progress increases
                m_NextGlitchTime = Time.time + Random.Range(5f, 20f) * (1f - progress * 0.7f);
            }

            // Update glitch timer
            if (m_IsGlitching)
            {
                m_GlitchTimer -= Time.deltaTime;
                if (m_GlitchTimer <= 0)
                {
                    m_IsGlitching = false;
                }
            }
        }

        private void TriggerGlitchBurst()
        {
            m_IsGlitching = true;
            m_GlitchTimer = Random.Range(0.1f, 0.5f) * m_CurrentIntensity;
        }

        /// <summary>
        /// Manually trigger a glitch effect.
        /// </summary>
        public void TriggerGlitch(float duration = 0.3f)
        {
            m_IsGlitching = true;
            m_GlitchTimer = duration;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (m_GlitchMaterial == null || m_CurrentIntensity <= 0)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Calculate effect multiplier (higher during glitch bursts)
            float effectMultiplier = m_IsGlitching ? 
                Mathf.Lerp(1f, 3f, m_CurrentIntensity) : 
                m_CurrentIntensity;

            // Set shader properties
            if (m_EnableChromaticAberration)
            {
                m_GlitchMaterial.SetFloat(ChromaticIntensity, 
                    m_MaxChromaticIntensity * effectMultiplier * (m_IsGlitching ? Random.Range(0.5f, 1.5f) : 1f));
            }

            if (m_EnableDistortion)
            {
                m_GlitchMaterial.SetFloat(DistortionAmount, 
                    m_MaxDistortion * effectMultiplier);
            }

            if (m_EnableScanlines)
            {
                m_GlitchMaterial.SetFloat(ScanlineIntensity, 
                    m_CurrentIntensity * 0.5f);
            }

            if (m_EnableFlicker && m_IsGlitching)
            {
                m_GlitchMaterial.SetFloat(FlickerIntensity, 
                    m_MaxFlickerIntensity * Random.Range(0f, 1f));
            }
            else
            {
                m_GlitchMaterial.SetFloat(FlickerIntensity, 0f);
            }

            if (m_EnableNoise)
            {
                m_GlitchMaterial.SetFloat(NoiseIntensity, 
                    m_MaxNoiseIntensity * effectMultiplier * (m_IsGlitching ? 1f : 0.3f));
            }

            if (m_EnableColorShift)
            {
                Color shiftedColor = m_GlitchColor;
                shiftedColor.a *= effectMultiplier;
                m_GlitchMaterial.SetColor(GlitchColorProperty, shiftedColor);
            }

            m_GlitchMaterial.SetFloat(TimeProperty, Time.time);

            Graphics.Blit(source, destination, m_GlitchMaterial);
        }

        private void OnDestroy()
        {
            if (m_GlitchMaterial != null)
            {
                Destroy(m_GlitchMaterial);
            }
        }
    }
}
