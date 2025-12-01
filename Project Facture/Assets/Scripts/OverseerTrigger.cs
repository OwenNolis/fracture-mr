using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Unity.VRTemplate
{
    /// <summary>
    /// Attach this script to an interactable object (like a computer) to trigger the Overseer System
    /// when the player interacts with it.
    /// </summary>
    [RequireComponent(typeof(XRBaseInteractable))]
    public class OverseerTrigger : MonoBehaviour
    {
        [Header("Overseer Reference")]
        [SerializeField]
        [Tooltip("Reference to the OverseerSystem. If not assigned, will try to find it in the scene.")]
        private OverseerSystem m_OverseerSystem;

        [Header("Trigger Settings")]
        [SerializeField]
        [Tooltip("Type of interaction that triggers the Overseer.")]
        private TriggerType m_TriggerType = TriggerType.OnSelect;

        [SerializeField]
        [Tooltip("Delay in seconds before the Overseer activates after interaction.")]
        private float m_ActivationDelay = 0f;

        [SerializeField]
        [Tooltip("If true, only triggers once. If false, can be triggered multiple times.")]
        private bool m_TriggerOnce = true;

        [SerializeField]
        [Tooltip("If true, shows visual feedback when interacted.")]
        private bool m_ShowFeedback = true;

        [Header("Audio Feedback")]
        [SerializeField]
        [Tooltip("Sound to play when the trigger is activated.")]
        private AudioClip m_ActivationSound;

        [SerializeField]
        [Tooltip("Audio source for playing sounds. If not assigned, will try to get or add one.")]
        private AudioSource m_AudioSource;

        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Optional particle effect to play on activation.")]
        private ParticleSystem m_ActivationEffect;

        [SerializeField]
        [Tooltip("Optional light to flicker on activation.")]
        private Light m_TriggerLight;

        public enum TriggerType
        {
            OnSelect,       // When player grabs/selects the object
            OnHover,        // When player points at the object
            OnActivate,     // When player uses the activate action (usually trigger button)
            OnCollision,    // When player physically touches the object
        }

        private XRBaseInteractable m_Interactable;
        private bool m_HasTriggered = false;
        private bool m_IsActivating = false;

        private void Awake()
        {
            // Get or find components
            m_Interactable = GetComponent<XRBaseInteractable>();

            if (m_OverseerSystem == null)
            {
                m_OverseerSystem = FindFirstObjectByType<OverseerSystem>();
            }

            if (m_AudioSource == null)
            {
                m_AudioSource = GetComponent<AudioSource>();
                if (m_AudioSource == null && m_ActivationSound != null)
                {
                    m_AudioSource = gameObject.AddComponent<AudioSource>();
                    m_AudioSource.playOnAwake = false;
                    m_AudioSource.spatialBlend = 1f; // 3D sound
                }
            }
        }

        private void OnEnable()
        {
            // Subscribe to interaction events based on trigger type
            if (m_Interactable != null)
            {
                switch (m_TriggerType)
                {
                    case TriggerType.OnSelect:
                        m_Interactable.selectEntered.AddListener(OnSelectEntered);
                        break;
                    case TriggerType.OnHover:
                        m_Interactable.hoverEntered.AddListener(OnHoverEntered);
                        break;
                    case TriggerType.OnActivate:
                        m_Interactable.activated.AddListener(OnActivated);
                        break;
                    case TriggerType.OnCollision:
                        // Collision is handled in OnCollisionEnter/OnTriggerEnter
                        break;
                }
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from events
            if (m_Interactable != null)
            {
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
                m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
                m_Interactable.activated.RemoveListener(OnActivated);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            TryActivateOverseer();
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            TryActivateOverseer();
        }

        private void OnActivated(ActivateEventArgs args)
        {
            TryActivateOverseer();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (m_TriggerType == TriggerType.OnCollision)
            {
                // Check if it's a hand/controller
                if (collision.gameObject.CompareTag("Player") ||
                    collision.gameObject.layer == LayerMask.NameToLayer("Hands"))
                {
                    TryActivateOverseer();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_TriggerType == TriggerType.OnCollision)
            {
                // Check if it's a hand/controller
                if (other.CompareTag("Player") ||
                    other.gameObject.layer == LayerMask.NameToLayer("Hands"))
                {
                    TryActivateOverseer();
                }
            }
        }

        private void TryActivateOverseer()
        {
            // Check if already triggered (if trigger once is enabled)
            if (m_TriggerOnce && m_HasTriggered)
            {
                return;
            }

            // Check if currently activating
            if (m_IsActivating)
            {
                return;
            }

            // Check if Overseer exists
            if (m_OverseerSystem == null)
            {
                Debug.LogWarning("OverseerTrigger: No OverseerSystem found in scene!");
                return;
            }

            m_HasTriggered = true;
            m_IsActivating = true;

            // Show feedback
            if (m_ShowFeedback)
            {
                PlayActivationFeedback();
            }

            // Activate with delay
            if (m_ActivationDelay > 0)
            {
                StartCoroutine(ActivateWithDelay());
            }
            else
            {
                ActivateOverseer();
            }
        }

        private System.Collections.IEnumerator ActivateWithDelay()
        {
            yield return new WaitForSeconds(m_ActivationDelay);
            ActivateOverseer();
        }

        private void ActivateOverseer()
        {
            m_OverseerSystem.ActivateOverseer();
            m_IsActivating = false;

            Debug.Log("OverseerTrigger: Overseer System Activated!");
        }

        private void PlayActivationFeedback()
        {
            // Play sound
            if (m_AudioSource != null && m_ActivationSound != null)
            {
                m_AudioSource.PlayOneShot(m_ActivationSound);
            }

            // Play particles
            if (m_ActivationEffect != null)
            {
                m_ActivationEffect.Play();
            }

            // Flicker light
            if (m_TriggerLight != null)
            {
                StartCoroutine(FlickerLight());
            }
        }

        private System.Collections.IEnumerator FlickerLight()
        {
            float originalIntensity = m_TriggerLight.intensity;

            for (int i = 0; i < 5; i++)
            {
                m_TriggerLight.intensity = Random.Range(0.1f, originalIntensity * 2f);
                yield return new WaitForSeconds(0.1f);
            }

            m_TriggerLight.intensity = originalIntensity;
        }

        /// <summary>
        /// Manually reset the trigger so it can be activated again.
        /// </summary>
        public void ResetTrigger()
        {
            m_HasTriggered = false;
        }

        /// <summary>
        /// Manually trigger the Overseer activation (useful for testing or scripted events).
        /// </summary>
        public void ManualTrigger()
        {
            m_HasTriggered = false;
            TryActivateOverseer();
        }
    }
}
