using UnityEngine;

namespace Unity.VRTemplate
{
    /// <summary>
    /// Attach this to any object that should be affected by the Overseer System.
    /// The object will automatically register itself with the OverseerSystem.
    /// </summary>
    public class OverseerAffectedObject : MonoBehaviour
    {
        [Header("Registration")]
        [SerializeField]
        [Tooltip("If true, automatically finds and registers with OverseerSystem on Start.")]
        private bool m_AutoRegister = true;

        [SerializeField]
        [Tooltip("Reference to OverseerSystem. If not set, will try to find it.")]
        private OverseerSystem m_OverseerSystem;

        [Header("Effect Settings")]
        [SerializeField]
        [Tooltip("If true, this object can be displaced by the Overseer.")]
        private bool m_CanBeDisplaced = true;

        [SerializeField]
        [Tooltip("If true, this object can disappear.")]
        private bool m_CanDisappear = true;

        [SerializeField]
        [Tooltip("If true, this object can have visual glitches.")]
        private bool m_CanGlitch = true;

        [SerializeField]
        [Tooltip("Priority level. Higher priority objects are affected first.")]
        [Range(0, 10)]
        private int m_Priority = 5;

        [Header("Custom Effects")]
        [SerializeField]
        [Tooltip("Optional custom effect to trigger when this object is affected.")]
        private UnityEngine.Events.UnityEvent m_OnAffected;

        [SerializeField]
        [Tooltip("Optional custom effect to trigger when this object disappears.")]
        private UnityEngine.Events.UnityEvent m_OnDisappear;

        [SerializeField]
        [Tooltip("Optional custom effect to trigger when this object reappears.")]
        private UnityEngine.Events.UnityEvent m_OnReappear;

        // Properties
        public bool CanBeDisplaced => m_CanBeDisplaced;
        public bool CanDisappear => m_CanDisappear;
        public bool CanGlitch => m_CanGlitch;
        public int Priority => m_Priority;

        private void Start()
        {
            if (m_AutoRegister)
            {
                RegisterWithOverseer();
            }
        }

        /// <summary>
        /// Registers this object with the Overseer System.
        /// </summary>
        public void RegisterWithOverseer()
        {
            if (m_OverseerSystem == null)
            {
                m_OverseerSystem = FindFirstObjectByType<OverseerSystem>();
            }

            if (m_OverseerSystem != null)
            {
                m_OverseerSystem.AddAffectedObject(gameObject);
            }
            else
            {
                Debug.LogWarning($"OverseerAffectedObject: No OverseerSystem found for {gameObject.name}");
            }
        }

        /// <summary>
        /// Called when this object is affected by an Overseer effect.
        /// </summary>
        public void TriggerAffectedEvent()
        {
            m_OnAffected?.Invoke();
        }

        /// <summary>
        /// Called when this object disappears.
        /// </summary>
        public void TriggerDisappearEvent()
        {
            m_OnDisappear?.Invoke();
        }

        /// <summary>
        /// Called when this object reappears.
        /// </summary>
        public void TriggerReappearEvent()
        {
            m_OnReappear?.Invoke();
        }
    }
}
