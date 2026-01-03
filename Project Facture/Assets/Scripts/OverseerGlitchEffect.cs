using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.VRTemplate
{
    /// <summary>
    /// Screen-space glitch effects using the Glitch.shadergraph material.
    /// Works with GlitchController and OverseerGlitchFeature for URP post-processing.
    /// Attach this to any GameObject in the scene.
    /// </summary>
    public class OverseerGlitchEffect : MonoBehaviour
    {
        [Header("Material Reference")]
        [SerializeField]
        [Tooltip("The Glitch material (using Glitch.shadergraph). If not assigned, will try to find GlitchController's material.")]
        private Material m_GlitchMaterial;

        [Header("Overseer Reference")]
        [SerializeField]
        [Tooltip("Reference to OverseerSystem. If not assigned, will auto-find.")]
        private OverseerSystem m_OverseerSystem;

        [Header("Glitch Controller Reference")]
        [SerializeField]
        [Tooltip("Reference to GlitchController. If not assigned, will auto-find.")]
        private GlitchController m_GlitchController;

        [Header("Glitch Burst Settings")]
        [SerializeField]
        [Tooltip("Enable automatic glitch bursts based on Overseer progress.")]
        private bool m_EnableAutoBursts = true;

        [SerializeField]
        [Tooltip("Minimum time between automatic glitch bursts.")]
        private float m_MinBurstInterval = 5f;

        [SerializeField]
        [Tooltip("Maximum time between automatic glitch bursts.")]
        private float m_MaxBurstInterval = 20f;

        // Internal state
        private float m_CurrentIntensity = 0f;
        private bool m_IsGlitching = false;
        private float m_GlitchTimer = 0f;
        private float m_NextGlitchTime = 0f;

        // Singleton for OverseerGlitchFeature access
        public static OverseerGlitchEffect Instance { get; private set; }
        
        /// <summary>
        /// The glitch material used for post-processing blit.
        /// </summary>
        public Material GlitchMaterial => m_GlitchMaterial;

        private void Awake()
        {
            Instance = this;
            FindReferences();
        }

        private void Start()
        {
            ValidateSetup();
        }

        private void FindReferences()
        {
            // Find OverseerSystem
            if (m_OverseerSystem == null)
            {
                m_OverseerSystem = FindFirstObjectByType<OverseerSystem>();
            }

            // Find GlitchController
            if (m_GlitchController == null)
            {
                m_GlitchController = FindFirstObjectByType<GlitchController>();
            }

            // Get material from GlitchController if not assigned
            if (m_GlitchMaterial == null && m_GlitchController != null)
            {
                m_GlitchMaterial = m_GlitchController.material;
            }
        }

        private void ValidateSetup()
        {
            if (m_GlitchMaterial == null)
            {
                Debug.LogError("[OverseerGlitchEffect] No Glitch material found! Assign the Glitch.mat or ensure GlitchController has a material assigned.");
            }
            else
            {
                Debug.Log("[OverseerGlitchEffect] Glitch material found: " + m_GlitchMaterial.name);
            }

            if (m_GlitchController == null)
            {
                Debug.LogWarning("[OverseerGlitchEffect] No GlitchController found. Shader parameters won't be automatically controlled.");
            }

            // Remind about URP Feature
            Debug.Log("[OverseerGlitchEffect] TIP: Ensure 'Overseer Glitch Feature' is added to your URP Renderer Data for screen-space effects.");
        }

        private void Update()
        {
            if (m_OverseerSystem == null) return;

            float progress = m_OverseerSystem.GetProgress();
            m_CurrentIntensity = progress;

            // Handle automatic glitch bursts
            if (m_EnableAutoBursts && progress > 0.1f)
            {
                HandleAutoBursts(progress);
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

        private void HandleAutoBursts(float progress)
        {
            if (Time.time >= m_NextGlitchTime)
            {
                TriggerGlitchBurst();
                
                // More frequent glitches as progress increases
                float intervalMultiplier = 1f - (progress * 0.7f);
                m_NextGlitchTime = Time.time + Random.Range(m_MinBurstInterval, m_MaxBurstInterval) * intervalMultiplier;
            }
        }

        private void TriggerGlitchBurst()
        {
            m_IsGlitching = true;
            m_GlitchTimer = Random.Range(0.1f, 0.5f) * m_CurrentIntensity;

            // Also trigger burst on OverseerSystem's GlitchController
            if (m_OverseerSystem != null)
            {
                m_OverseerSystem.TriggerGlitchBurst(m_GlitchTimer);
            }
        }

        /// <summary>
        /// Manually trigger a glitch effect.
        /// </summary>
        public void TriggerGlitch(float duration = 0.3f)
        {
            m_IsGlitching = true;
            m_GlitchTimer = duration;

            if (m_OverseerSystem != null)
            {
                m_OverseerSystem.TriggerGlitchBurst(duration);
            }
        }

        /// <summary>
        /// Check if currently in a glitch burst.
        /// </summary>
        public bool IsGlitching => m_IsGlitching;

        /// <summary>
        /// Current intensity based on Overseer progress.
        /// </summary>
        public float CurrentIntensity => m_CurrentIntensity;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
