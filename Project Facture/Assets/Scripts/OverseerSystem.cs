using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.InputSystem;

namespace Unity.VRTemplate
{
    /// <summary>
    /// AI Overseer Takeover System - Creates a progressively unsettling horror experience
    /// where an AI slowly takes control of the VR environment over time.
    /// Objects glitch, disappear, move slightly, and eventually the AI takes control of VR inputs.
    /// </summary>
    public class OverseerSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Activation Settings")]
        [SerializeField]
        [Tooltip("If true, the system starts immediately. If false, use ActivateOverseer() method.")]
        private bool m_AutoStart = false;

        [SerializeField]
        [Tooltip("Total duration in seconds for the full takeover (default 5 minutes = 300 seconds).")]
        private float m_TakeoverDuration = 300f;

        [Header("Affected Objects")]
        [SerializeField]
        [Tooltip("Objects that can be affected by glitches, displacement, and disappearance.")]
        private List<GameObject> m_AffectedObjects = new List<GameObject>();

        [SerializeField]
        [Tooltip("If true, automatically finds all objects with 'Affected' tag.")]
        private bool m_AutoFindAffectedObjects = true;

        [SerializeField]
        [Tooltip("Tag to use when auto-finding affected objects.")]
        private string m_AffectedObjectTag = "AffectedByOverseer";

        [Header("VR Control References")]
        [SerializeField]
        [Tooltip("Reference to the XR Origin/Rig.")]
        private Transform m_XROrigin;

        [SerializeField]
        [Tooltip("Reference to the main camera (player head).")]
        private Camera m_PlayerCamera;

        [SerializeField]
        [Tooltip("Left hand controller transform.")]
        private Transform m_LeftController;

        [SerializeField]
        [Tooltip("Right hand controller transform.")]
        private Transform m_RightController;

        [SerializeField]
        [Tooltip("Reference to movement provider for locomotion takeover.")]
        private ContinuousMoveProvider m_MoveProvider;

        [SerializeField]
        [Tooltip("Reference to turn provider for rotation takeover.")]
        private ContinuousTurnProvider m_TurnProvider;

        [Header("Glitch Effects")]
        [SerializeField]
        [Tooltip("Material to apply for glitch effect (optional).")]
        private Material m_GlitchMaterial;

        [SerializeField]
        [Tooltip("Audio source for creepy sounds.")]
        private AudioSource m_AudioSource;

        [SerializeField]
        [Tooltip("Array of creepy audio clips to play.")]
        private AudioClip[] m_CreepySounds;

        [Header("Visual Effects")]
        [SerializeField]
        [Tooltip("Post-processing volume for screen effects (optional).")]
        private GameObject m_PostProcessVolume;

        [SerializeField]
        [Tooltip("Color to tint the screen during intense moments.")]
        private Color m_GlitchColor = new Color(1f, 0f, 0f, 0.1f);

        [Header("Debug")]
        [SerializeField]
        private bool m_DebugMode = false;

        #endregion

        #region Private Variables

        private bool m_IsActive = false;
        private float m_ElapsedTime = 0f;
        private float m_TakeoverProgress = 0f; // 0 to 1

        // Object tracking
        private Dictionary<GameObject, Vector3> m_OriginalPositions = new Dictionary<GameObject, Vector3>();
        private Dictionary<GameObject, Quaternion> m_OriginalRotations = new Dictionary<GameObject, Quaternion>();
        private Dictionary<GameObject, Vector3> m_OriginalScales = new Dictionary<GameObject, Vector3>();
        private Dictionary<GameObject, Material[]> m_OriginalMaterials = new Dictionary<GameObject, Material[]>();
        private List<GameObject> m_DisappearedObjects = new List<GameObject>();

        // Coroutine references
        private Coroutine m_MainLoopCoroutine;
        private Coroutine m_ControlTakeoverCoroutine;

        // Control takeover
        private bool m_IsControllingPlayer = false;
        private float m_ControlIntensity = 0f;
        private Vector3 m_ForcedMovementDirection;
        private float m_ForcedRotation;

        // Timing for events
        private float m_NextGlitchTime = 0f;
        private float m_NextDisplacementTime = 0f;
        private float m_NextDisappearTime = 0f;
        private float m_NextSoundTime = 0f;
        private float m_NextControlTakeoverTime = 0f;

        // Phase thresholds (0-1 range)
        private const float PHASE_1_END = 0.3f;      // 0-30% - Very subtle
        private const float PHASE_2_END = 0.6f;      // 30-60% - Noticeable
        private const float PHASE_3_END = 0.85f;     // 60-85% - Intense
        // 85-100% - Full takeover

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (m_AutoFindAffectedObjects)
            {
                FindAffectedObjects();
            }
            StoreOriginalStates();
            FindVRComponents();
        }

        private void Start()
        {
            if (m_AutoStart)
            {
                ActivateOverseer();
            }
        }

        private void Update()
        {
            if (!m_IsActive) return;

            m_ElapsedTime += Time.deltaTime;
            m_TakeoverProgress = Mathf.Clamp01(m_ElapsedTime / m_TakeoverDuration);

            // Apply forced movement/rotation if controlling player
            if (m_IsControllingPlayer)
            {
                ApplyControlTakeover();
            }

            if (m_DebugMode)
            {
                Debug.Log($"Overseer Progress: {m_TakeoverProgress * 100:F1}% | Phase: {GetCurrentPhase()}");
            }
        }

        private void OnDestroy()
        {
            DeactivateOverseer();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Activates the Overseer system. Call this when player interacts with trigger object.
        /// </summary>
        public void ActivateOverseer()
        {
            if (m_IsActive)
            {
                Debug.LogWarning("Overseer is already active!");
                return;
            }

            m_IsActive = true;
            m_ElapsedTime = 0f;
            m_TakeoverProgress = 0f;

            m_MainLoopCoroutine = StartCoroutine(OverseerMainLoop());

            if (m_DebugMode)
            {
                Debug.Log("OVERSEER SYSTEM ACTIVATED - The takeover begins...");
            }
        }

        /// <summary>
        /// Deactivates the Overseer system and restores everything to normal.
        /// </summary>
        public void DeactivateOverseer()
        {
            m_IsActive = false;
            m_IsControllingPlayer = false;

            if (m_MainLoopCoroutine != null)
            {
                StopCoroutine(m_MainLoopCoroutine);
            }

            if (m_ControlTakeoverCoroutine != null)
            {
                StopCoroutine(m_ControlTakeoverCoroutine);
            }

            RestoreAllObjects();

            if (m_DebugMode)
            {
                Debug.Log("OVERSEER SYSTEM DEACTIVATED - Order restored.");
            }
        }

        /// <summary>
        /// Adds an object to be affected by the Overseer.
        /// </summary>
        public void AddAffectedObject(GameObject obj)
        {
            if (!m_AffectedObjects.Contains(obj))
            {
                m_AffectedObjects.Add(obj);
                StoreObjectState(obj);
            }
        }

        /// <summary>
        /// Gets the current takeover progress (0-1).
        /// </summary>
        public float GetProgress()
        {
            return m_TakeoverProgress;
        }

        /// <summary>
        /// Gets the current phase name.
        /// </summary>
        public string GetCurrentPhase()
        {
            if (m_TakeoverProgress < PHASE_1_END) return "Subtle Intrusion";
            if (m_TakeoverProgress < PHASE_2_END) return "Growing Presence";
            if (m_TakeoverProgress < PHASE_3_END) return "Active Manipulation";
            return "Full Takeover";
        }

        #endregion

        #region Private Methods - Initialization

        private void FindAffectedObjects()
        {
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(m_AffectedObjectTag);
            foreach (var obj in taggedObjects)
            {
                if (!m_AffectedObjects.Contains(obj))
                {
                    m_AffectedObjects.Add(obj);
                }
            }
        }

        private void FindVRComponents()
        {
            // Try to find XR Origin if not assigned
            if (m_XROrigin == null)
            {
                var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOrigin != null)
                {
                    m_XROrigin = xrOrigin.transform;
                }
            }

            // Try to find camera
            if (m_PlayerCamera == null)
            {
                m_PlayerCamera = Camera.main;
            }

            // Try to find move provider
            if (m_MoveProvider == null)
            {
                m_MoveProvider = FindFirstObjectByType<ContinuousMoveProvider>();
            }

            // Try to find turn provider
            if (m_TurnProvider == null)
            {
                m_TurnProvider = FindFirstObjectByType<ContinuousTurnProvider>();
            }
        }

        private void StoreOriginalStates()
        {
            foreach (var obj in m_AffectedObjects)
            {
                StoreObjectState(obj);
            }
        }

        private void StoreObjectState(GameObject obj)
        {
            if (obj == null) return;

            m_OriginalPositions[obj] = obj.transform.position;
            m_OriginalRotations[obj] = obj.transform.rotation;
            m_OriginalScales[obj] = obj.transform.localScale;

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                m_OriginalMaterials[obj] = renderer.materials;
            }
        }

        #endregion

        #region Private Methods - Main Loop

        private IEnumerator OverseerMainLoop()
        {
            // Initialize timing
            SetNextEventTimes();

            while (m_IsActive)
            {
                float currentTime = m_ElapsedTime;

                // Check for glitch events
                if (currentTime >= m_NextGlitchTime)
                {
                    TriggerGlitchEffect();
                    SetNextGlitchTime();
                }

                // Check for displacement events
                if (currentTime >= m_NextDisplacementTime)
                {
                    TriggerDisplacement();
                    SetNextDisplacementTime();
                }

                // Check for disappearance events (only after Phase 1)
                if (m_TakeoverProgress >= PHASE_1_END && currentTime >= m_NextDisappearTime)
                {
                    TriggerDisappearance();
                    SetNextDisappearTime();
                }

                // Check for sound events
                if (currentTime >= m_NextSoundTime && m_CreepySounds != null && m_CreepySounds.Length > 0)
                {
                    PlayCreepySound();
                    SetNextSoundTime();
                }

                // Check for control takeover (only after Phase 2)
                if (m_TakeoverProgress >= PHASE_2_END && currentTime >= m_NextControlTakeoverTime)
                {
                    StartControlTakeover();
                    SetNextControlTakeoverTime();
                }

                // Apply progressive screen effects
                ApplyScreenEffects();

                yield return new WaitForSeconds(0.1f);
            }
        }

        private void SetNextEventTimes()
        {
            SetNextGlitchTime();
            SetNextDisplacementTime();
            SetNextDisappearTime();
            SetNextSoundTime();
            SetNextControlTakeoverTime();
        }

        private void SetNextGlitchTime()
        {
            // More frequent glitches as takeover progresses
            float baseInterval = Mathf.Lerp(15f, 2f, m_TakeoverProgress);
            float randomVariation = Random.Range(-baseInterval * 0.3f, baseInterval * 0.3f);
            m_NextGlitchTime = m_ElapsedTime + baseInterval + randomVariation;
        }

        private void SetNextDisplacementTime()
        {
            // More frequent displacements as takeover progresses
            float baseInterval = Mathf.Lerp(20f, 3f, m_TakeoverProgress);
            float randomVariation = Random.Range(-baseInterval * 0.3f, baseInterval * 0.3f);
            m_NextDisplacementTime = m_ElapsedTime + baseInterval + randomVariation;
        }

        private void SetNextDisappearTime()
        {
            // Disappearances start after Phase 1
            float baseInterval = Mathf.Lerp(45f, 8f, m_TakeoverProgress);
            float randomVariation = Random.Range(-baseInterval * 0.3f, baseInterval * 0.3f);
            m_NextDisappearTime = m_ElapsedTime + baseInterval + randomVariation;
        }

        private void SetNextSoundTime()
        {
            float baseInterval = Mathf.Lerp(30f, 5f, m_TakeoverProgress);
            float randomVariation = Random.Range(-baseInterval * 0.5f, baseInterval * 0.5f);
            m_NextSoundTime = m_ElapsedTime + baseInterval + randomVariation;
        }

        private void SetNextControlTakeoverTime()
        {
            // Control takeover events become more frequent in later phases
            float baseInterval = Mathf.Lerp(60f, 10f, m_TakeoverProgress);
            float randomVariation = Random.Range(-baseInterval * 0.3f, baseInterval * 0.3f);
            m_NextControlTakeoverTime = m_ElapsedTime + baseInterval + randomVariation;
        }

        #endregion

        #region Private Methods - Effects

        private void TriggerGlitchEffect()
        {
            if (m_AffectedObjects.Count == 0) return;

            // Select random object(s) to glitch
            int numObjectsToGlitch = Mathf.CeilToInt(m_AffectedObjects.Count * m_TakeoverProgress * 0.3f);
            numObjectsToGlitch = Mathf.Max(1, numObjectsToGlitch);

            List<GameObject> availableObjects = new List<GameObject>(m_AffectedObjects);
            availableObjects.RemoveAll(obj => obj == null || m_DisappearedObjects.Contains(obj));

            for (int i = 0; i < numObjectsToGlitch && availableObjects.Count > 0; i++)
            {
                int index = Random.Range(0, availableObjects.Count);
                GameObject obj = availableObjects[index];
                availableObjects.RemoveAt(index);

                StartCoroutine(GlitchObject(obj));
            }

            if (m_DebugMode)
            {
                Debug.Log($"Glitch triggered on {numObjectsToGlitch} object(s)");
            }
        }

        private IEnumerator GlitchObject(GameObject obj)
        {
            if (obj == null) yield break;

            float glitchDuration = Mathf.Lerp(0.1f, 0.5f, m_TakeoverProgress);
            float glitchIntensity = Mathf.Lerp(0.02f, 0.2f, m_TakeoverProgress);
            int glitchSteps = Random.Range(3, 8);

            Vector3 originalPos = obj.transform.position;
            Quaternion originalRot = obj.transform.rotation;
            Vector3 originalScale = obj.transform.localScale;

            for (int i = 0; i < glitchSteps; i++)
            {
                // Random displacement
                Vector3 glitchOffset = new Vector3(
                    Random.Range(-glitchIntensity, glitchIntensity),
                    Random.Range(-glitchIntensity, glitchIntensity),
                    Random.Range(-glitchIntensity, glitchIntensity)
                );
                obj.transform.position = originalPos + glitchOffset;

                // Random rotation glitch
                Quaternion glitchRot = Quaternion.Euler(
                    Random.Range(-5f * m_TakeoverProgress, 5f * m_TakeoverProgress),
                    Random.Range(-5f * m_TakeoverProgress, 5f * m_TakeoverProgress),
                    Random.Range(-5f * m_TakeoverProgress, 5f * m_TakeoverProgress)
                );
                obj.transform.rotation = originalRot * glitchRot;

                // Scale flicker
                float scaleFlicker = Random.Range(0.95f, 1.05f);
                obj.transform.localScale = originalScale * scaleFlicker;

                // Flicker visibility
                if (Random.value < 0.3f * m_TakeoverProgress)
                {
                    var renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                        yield return new WaitForSeconds(0.02f);
                        renderer.enabled = true;
                    }
                }

                yield return new WaitForSeconds(glitchDuration / glitchSteps);
            }

            // Return to original or slightly offset position (based on progress)
            if (m_TakeoverProgress < PHASE_2_END)
            {
                obj.transform.position = originalPos;
                obj.transform.rotation = originalRot;
                obj.transform.localScale = originalScale;
            }
            else
            {
                // Leave slightly displaced after Phase 2
                float permanentOffset = glitchIntensity * 0.3f;
                obj.transform.position = originalPos + new Vector3(
                    Random.Range(-permanentOffset, permanentOffset),
                    0,
                    Random.Range(-permanentOffset, permanentOffset)
                );
            }
        }

        private void TriggerDisplacement()
        {
            List<GameObject> availableObjects = new List<GameObject>(m_AffectedObjects);
            availableObjects.RemoveAll(obj => obj == null || m_DisappearedObjects.Contains(obj));

            if (availableObjects.Count == 0) return;

            GameObject obj = availableObjects[Random.Range(0, availableObjects.Count)];

            // Displacement amount increases with progress
            float displacementAmount = Mathf.Lerp(0.01f, 0.15f, m_TakeoverProgress);

            // In early phases, displacement is very subtle
            if (m_TakeoverProgress < PHASE_1_END)
            {
                displacementAmount *= 0.2f;
            }

            Vector3 displacement = new Vector3(
                Random.Range(-displacementAmount, displacementAmount),
                0, // Keep Y stable for now
                Random.Range(-displacementAmount, displacementAmount)
            );

            // Apply displacement smoothly or instantly based on phase
            if (m_TakeoverProgress < PHASE_2_END)
            {
                // Smooth, subtle movement
                StartCoroutine(SmoothDisplacement(obj, displacement, 2f));
            }
            else
            {
                // Instant, jarring displacement
                obj.transform.position += displacement;

                // Sometimes also rotate slightly
                if (Random.value < 0.5f)
                {
                    obj.transform.Rotate(0, Random.Range(-15f, 15f), 0);
                }
            }

            if (m_DebugMode)
            {
                Debug.Log($"Displacement triggered: {obj.name} moved by {displacement.magnitude:F3}m");
            }
        }

        private IEnumerator SmoothDisplacement(GameObject obj, Vector3 displacement, float duration)
        {
            if (obj == null) yield break;

            Vector3 startPos = obj.transform.position;
            Vector3 endPos = startPos + displacement;
            float elapsed = 0f;

            while (elapsed < duration && obj != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                obj.transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }
        }

        private void TriggerDisappearance()
        {
            List<GameObject> availableObjects = new List<GameObject>(m_AffectedObjects);
            availableObjects.RemoveAll(obj => obj == null || m_DisappearedObjects.Contains(obj));

            if (availableObjects.Count == 0) return;

            // Don't disappear too many objects
            if (m_DisappearedObjects.Count >= m_AffectedObjects.Count * 0.4f) return;

            GameObject obj = availableObjects[Random.Range(0, availableObjects.Count)];

            // Chance of disappearance increases with progress
            float disappearChance = Mathf.Lerp(0.1f, 0.6f, m_TakeoverProgress);

            if (Random.value < disappearChance)
            {
                StartCoroutine(DisappearObject(obj));
            }
        }

        private IEnumerator DisappearObject(GameObject obj)
        {
            if (obj == null) yield break;

            m_DisappearedObjects.Add(obj);

            // Dramatic disappearance in later phases
            if (m_TakeoverProgress >= PHASE_2_END)
            {
                // Flicker before disappearing
                var renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        renderer.enabled = !renderer.enabled;
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            }

            obj.SetActive(false);

            if (m_DebugMode)
            {
                Debug.Log($"Object disappeared: {obj.name}");
            }

            // In later phases, objects might reappear in wrong places
            if (m_TakeoverProgress >= PHASE_3_END && Random.value < 0.3f)
            {
                yield return new WaitForSeconds(Random.Range(10f, 30f));

                if (obj != null)
                {
                    // Reappear in a different location
                    Vector3 newPos = m_OriginalPositions[obj] + new Vector3(
                        Random.Range(-2f, 2f),
                        0,
                        Random.Range(-2f, 2f)
                    );
                    obj.transform.position = newPos;
                    obj.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    obj.SetActive(true);
                    m_DisappearedObjects.Remove(obj);

                    if (m_DebugMode)
                    {
                        Debug.Log($"Object reappeared in new location: {obj.name}");
                    }
                }
            }
        }

        private void PlayCreepySound()
        {
            if (m_AudioSource == null || m_CreepySounds == null || m_CreepySounds.Length == 0) return;

            AudioClip clip = m_CreepySounds[Random.Range(0, m_CreepySounds.Length)];

            // Volume increases with progress
            float volume = Mathf.Lerp(0.1f, 0.6f, m_TakeoverProgress);

            m_AudioSource.PlayOneShot(clip, volume);

            if (m_DebugMode)
            {
                Debug.Log($"Playing creepy sound: {clip.name} at volume {volume:F2}");
            }
        }

        private void ApplyScreenEffects()
        {
            // Screen effects intensity based on progress
            if (m_PostProcessVolume != null)
            {
                // Gradually increase post-processing effects
                // This would need to be customized based on your post-processing setup
            }

            // Optional: Camera shake in intense moments
            if (m_TakeoverProgress >= PHASE_3_END && Random.value < 0.01f)
            {
                StartCoroutine(CameraShake());
            }
        }

        private IEnumerator CameraShake()
        {
            if (m_PlayerCamera == null) yield break;

            float duration = 0.3f;
            float magnitude = 0.05f * m_TakeoverProgress;
            float elapsed = 0f;

            Vector3 originalPos = m_PlayerCamera.transform.localPosition;

            while (elapsed < duration)
            {
                float x = Random.Range(-magnitude, magnitude);
                float y = Random.Range(-magnitude, magnitude);

                m_PlayerCamera.transform.localPosition = originalPos + new Vector3(x, y, 0);

                elapsed += Time.deltaTime;
                yield return null;
            }

            m_PlayerCamera.transform.localPosition = originalPos;
        }

        #endregion

        #region Private Methods - Control Takeover

        private void StartControlTakeover()
        {
            if (m_ControlTakeoverCoroutine != null)
            {
                StopCoroutine(m_ControlTakeoverCoroutine);
            }

            m_ControlTakeoverCoroutine = StartCoroutine(ControlTakeoverSequence());
        }

        private IEnumerator ControlTakeoverSequence()
        {
            m_IsControllingPlayer = true;

            // Duration and intensity based on progress
            float duration = Mathf.Lerp(1f, 5f, m_TakeoverProgress);
            m_ControlIntensity = Mathf.Lerp(0.2f, 1f, m_TakeoverProgress);

            // Random forced movement direction
            m_ForcedMovementDirection = new Vector3(
                Random.Range(-1f, 1f),
                0,
                Random.Range(-1f, 1f)
            ).normalized;

            // Random forced rotation
            m_ForcedRotation = Random.Range(-30f, 30f) * m_ControlIntensity;

            if (m_DebugMode)
            {
                Debug.Log($"Control takeover started! Duration: {duration:F1}s, Intensity: {m_ControlIntensity:F2}");
            }

            yield return new WaitForSeconds(duration);

            m_IsControllingPlayer = false;
            m_ControlIntensity = 0f;

            if (m_DebugMode)
            {
                Debug.Log("Control returned to player.");
            }
        }

        private void ApplyControlTakeover()
        {
            if (m_XROrigin == null) return;

            // Apply forced movement
            Vector3 movement = m_ForcedMovementDirection * m_ControlIntensity * Time.deltaTime * 0.5f;
            m_XROrigin.position += movement;

            // Apply forced rotation
            float rotation = m_ForcedRotation * m_ControlIntensity * Time.deltaTime;
            m_XROrigin.Rotate(0, rotation, 0);

            // In full takeover phase, also mess with hand positions occasionally
            if (m_TakeoverProgress >= PHASE_3_END)
            {
                ApplyHandGlitch();
            }
        }

        private void ApplyHandGlitch()
        {
            // Random chance to offset controller visuals
            if (Random.value < 0.02f * m_TakeoverProgress)
            {
                if (m_LeftController != null)
                {
                    StartCoroutine(TemporaryHandOffset(m_LeftController));
                }
                if (m_RightController != null && Random.value < 0.5f)
                {
                    StartCoroutine(TemporaryHandOffset(m_RightController));
                }
            }
        }

        private IEnumerator TemporaryHandOffset(Transform hand)
        {
            Vector3 originalLocalPos = hand.localPosition;
            Quaternion originalLocalRot = hand.localRotation;

            float duration = Random.Range(0.1f, 0.3f);
            float elapsed = 0f;

            Vector3 offset = new Vector3(
                Random.Range(-0.1f, 0.1f),
                Random.Range(-0.1f, 0.1f),
                Random.Range(-0.1f, 0.1f)
            );

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                hand.localPosition = Vector3.Lerp(originalLocalPos, originalLocalPos + offset,
                    Mathf.Sin(t * Mathf.PI));

                elapsed += Time.deltaTime;
                yield return null;
            }

            hand.localPosition = originalLocalPos;
            hand.localRotation = originalLocalRot;
        }

        #endregion

        #region Private Methods - Restoration

        private void RestoreAllObjects()
        {
            foreach (var obj in m_AffectedObjects)
            {
                if (obj == null) continue;

                // Restore position, rotation, scale
                if (m_OriginalPositions.ContainsKey(obj))
                    obj.transform.position = m_OriginalPositions[obj];

                if (m_OriginalRotations.ContainsKey(obj))
                    obj.transform.rotation = m_OriginalRotations[obj];

                if (m_OriginalScales.ContainsKey(obj))
                    obj.transform.localScale = m_OriginalScales[obj];

                // Restore materials
                if (m_OriginalMaterials.ContainsKey(obj))
                {
                    var renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.materials = m_OriginalMaterials[obj];
                    }
                }

                // Re-enable if disappeared
                obj.SetActive(true);
            }

            m_DisappearedObjects.Clear();
        }

        #endregion
    }
}
