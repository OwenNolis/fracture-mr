using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        [SerializeField]
        [Tooltip("If true, Poltergeist events will physically throw objects. If false, they will just rattle in place.")]
        private bool m_AllowPoltergeistThrow = false;

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

        [SerializeField]
        [Tooltip("Reference to Teleportation provider (if used).")]
        private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider m_TeleportProvider;

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

        [Header("Fake Crash Event")]
        [SerializeField]
        [Tooltip("UI Object (Canvas/Panel) to show when the 'crash' happens.")]
        private GameObject m_CrashUI;

        [SerializeField]
        [Tooltip("Windows error sound effect.")]
        private AudioClip m_CrashSound;

        [SerializeField]
        [Tooltip("Time in seconds to wait AFTER reaching 100% before crashing.")]
        private float m_CrashDelay = 60f;

        [Header("Debug")]
        [SerializeField]
        private bool m_DebugMode = false;

        #endregion

        #region Private Variables

        private bool m_IsActive = false;
        private float m_ElapsedTime = 0f;
        private float m_TakeoverProgress = 0f; // 0 to 1

        // Heartbeat
        private float m_LastHeartbeatTime = 0f;
        [SerializeField]
        [Tooltip("Heartbeat sound clip (optional).")]
        private AudioClip m_HeartbeatSound;

        // Object tracking
        private Dictionary<GameObject, Vector3> m_OriginalPositions = new Dictionary<GameObject, Vector3>();
        private Dictionary<GameObject, Quaternion> m_OriginalRotations = new Dictionary<GameObject, Quaternion>();
        private Dictionary<GameObject, Vector3> m_OriginalScales = new Dictionary<GameObject, Vector3>();
        private Dictionary<GameObject, Material[]> m_OriginalMaterials = new Dictionary<GameObject, Material[]>();
        private List<GameObject> m_DisappearedObjects = new List<GameObject>();

        // Coroutine references
        private Coroutine m_MainLoopCoroutine;
        private Coroutine m_ControlTakeoverCoroutine;

        // Crash timing
        private float m_TakeoverCompleteTime = -1f;

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
        private float m_NextPoltergeistTime = 0f;

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
             
            // Start heartbeat
            StartCoroutine(HeartbeatRoutine());

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

            // Restore movement provider if it was disabled
            if (m_MoveProvider != null)
            {
                m_MoveProvider.enabled = true;
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
            
            Debug.Log($"[OverseerSystem] Found {m_AffectedObjects.Count} affected objects. Tag: {m_AffectedObjectTag}");
            if (m_AffectedObjects.Count == 0)
            {
                Debug.LogWarning("[OverseerSystem] Warning: No affected objects found! Glitches and poltergeist effects will not work. Tag objects with '" + m_AffectedObjectTag + "' or assign them manually.");
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

            // Try to find teleport provider
            if (m_TeleportProvider == null)
            {
                m_TeleportProvider = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
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

                // Check for Poltergeist (throwing objects) - Phase 2+
                if (m_TakeoverProgress >= PHASE_2_END && currentTime >= m_NextPoltergeistTime)
                {
                    TriggerPoltergeist();
                    SetNextPoltergeistTime();
                }

                // Check for completion (Fake Crash)
                if (m_TakeoverProgress >= 1.0f)
                {
                    // Delay the crash by m_CrashDelay seconds after takeover is complete
                    if (m_TakeoverCompleteTime < 0)
                    {
                        m_TakeoverCompleteTime = Time.time;
                        if (m_DebugMode) Debug.Log($"Takeover Complete (100%). Waiting {m_CrashDelay}s for crash...");
                    }

                    if (Time.time >= m_TakeoverCompleteTime + m_CrashDelay)
                    {
                        ExecuteFakeCrash();
                        yield break; // Stop the loop
                    }
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
            SetNextPoltergeistTime();
        }

        private void SetNextGlitchTime()
        {
            // More frequent glitches as takeover progresses, but AT LEAST 30s apart per request
            float baseInterval = Mathf.Lerp(60f, 30f, m_TakeoverProgress);
            float randomVariation = Random.Range(0f, 15f); // Always add positive variation
            m_NextGlitchTime = m_ElapsedTime + Mathf.Max(30f, baseInterval + randomVariation);
        }

        private void SetNextDisplacementTime()
        {
            // More frequent displacements as takeover progresses, but AT LEAST 30s apart
            float baseInterval = Mathf.Lerp(60f, 30f, m_TakeoverProgress);
            float randomVariation = Random.Range(0f, 15f);
            m_NextDisplacementTime = m_ElapsedTime + Mathf.Max(30f, baseInterval + randomVariation);
        }

        private void SetNextDisappearTime()
        {
            // Disappearances start after Phase 1, min 30s delay
            float baseInterval = Mathf.Lerp(90f, 45f, m_TakeoverProgress);
            float randomVariation = Random.Range(0f, 15f);
            m_NextDisappearTime = m_ElapsedTime + Mathf.Max(30f, baseInterval + randomVariation);
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

        private void SetNextPoltergeistTime()
        {
            float baseInterval = Mathf.Lerp(45f, 15f, m_TakeoverProgress);
            m_NextPoltergeistTime = m_ElapsedTime + baseInterval + Random.Range(0f, 10f);
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

            // Material Swap
            Renderer renderer = obj.GetComponent<Renderer>();
            Material[] runtimeOriginalMaterials = null;
            if (renderer != null && m_GlitchMaterial != null)
            {
                runtimeOriginalMaterials = renderer.materials;
                // Swap all materials to glitch
                Material[] glitchMats = new Material[runtimeOriginalMaterials.Length];
                for(int m=0; m<glitchMats.Length; m++) glitchMats[m] = m_GlitchMaterial;
                renderer.materials = glitchMats;
            }

            for (int i = 0; i < glitchSteps; i++)
            {
                // Random displacement
                Vector3 glitchOffset = new Vector3(
                    Random.Range(-glitchIntensity, glitchIntensity),
                    Random.Range(-glitchIntensity, glitchIntensity),
                    Random.Range(-glitchIntensity, glitchIntensity)
                );
                
                Vector3 targetPos = originalPos + glitchOffset;
                
                // Clamp to max 1.0f meter from original position
                if (Vector3.Distance(targetPos, originalPos) > 1.0f)
                {
                    targetPos = originalPos + (targetPos - originalPos).normalized * 1.0f;
                }

                // Random rotation glitch
                Quaternion glitchRot = Quaternion.Euler(
                    Random.Range(-5f * m_TakeoverProgress, 5f * m_TakeoverProgress),
                    Random.Range(-5f * m_TakeoverProgress, 5f * m_TakeoverProgress),
                    Random.Range(-5f * m_TakeoverProgress, 5f * m_TakeoverProgress)
                );
                Quaternion targetRot = originalRot * glitchRot;

                // Apply using physics if possible to avoid phasing
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.MovePosition(targetPos);
                    rb.MoveRotation(targetRot);
                }
                else
                {
                    obj.transform.position = targetPos;
                    obj.transform.rotation = targetRot;
                }

                // Scale flicker
                float scaleFlicker = Random.Range(0.95f, 1.05f);
                obj.transform.localScale = originalScale * scaleFlicker;

                // Flicker visibility
                if (Random.value < 0.3f * m_TakeoverProgress)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                        yield return new WaitForSeconds(0.02f);
                        renderer.enabled = true;
                    }
                }

                yield return new WaitForSeconds(glitchDuration / glitchSteps);
            }

            // Restore Materials
            if (renderer != null && runtimeOriginalMaterials != null)
            {
                renderer.materials = runtimeOriginalMaterials;
            }

            // Return to original or slightly offset position
            if (m_TakeoverProgress < PHASE_2_END)
            {
                // Return to exact original
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.MovePosition(originalPos);
                    rb.MoveRotation(originalRot);
                }
                else
                {
                    obj.transform.position = originalPos;
                    obj.transform.rotation = originalRot;
                }
                obj.transform.localScale = originalScale;
            }
            else
            {
                // Leave slightly displaced but CLAMPED
                float permanentOffset = glitchIntensity * 0.3f;
                Vector3 finalOffset = new Vector3(
                    Random.Range(-permanentOffset, permanentOffset),
                    0,
                    Random.Range(-permanentOffset, permanentOffset)
                );
                
                Vector3 targetFinalPos = originalPos + finalOffset;
                 if (Vector3.Distance(targetFinalPos, originalPos) > 1.0f)
                {
                    targetFinalPos = originalPos + (targetFinalPos - originalPos).normalized * 1.0f;
                }

                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.MovePosition(targetFinalPos);
                }
                else
                {
                    obj.transform.position = targetFinalPos;
                }
            }
        }

        private void TriggerDisplacement()
        {
            List<GameObject> availableObjects = new List<GameObject>(m_AffectedObjects);
            availableObjects.RemoveAll(obj => obj == null || m_DisappearedObjects.Contains(obj));

            if (availableObjects.Count == 0) return;

            // "Don't Blink" Mechanic: Prioritize objects that are NOT visible
            // We split the list into visible and invisible objects
            List<GameObject> hiddenObjects = new List<GameObject>();
            List<GameObject> visibleObjects = new List<GameObject>();

            foreach (var obj in availableObjects)
            {
                if (!IsObjectVisible(obj))
                    hiddenObjects.Add(obj);
                else
                    visibleObjects.Add(obj);
            }

            GameObject targetObj = null;

            // 80% chance to pick a hidden object if any differ
            if (hiddenObjects.Count > 0 && Random.value < 0.8f)
            {
                targetObj = hiddenObjects[Random.Range(0, hiddenObjects.Count)];
            }
            else if (visibleObjects.Count > 0)
            {
                targetObj = visibleObjects[Random.Range(0, visibleObjects.Count)];
            }
            else if (availableObjects.Count > 0) 
            {
                // Fallback for edge cases
                targetObj = availableObjects[Random.Range(0, availableObjects.Count)];
            }

            if (targetObj == null) return;

            // Displacement amount increases with progress
            float displacementAmount = Mathf.Lerp(0.01f, 0.15f, m_TakeoverProgress);

            // In early phases, displacement is very subtle
            if (m_TakeoverProgress < PHASE_1_END)
            {
                displacementAmount *= 0.2f;
            }

            // Execute displacement logic
            ExecuteDisplacement(targetObj, displacementAmount, m_TakeoverProgress >= PHASE_2_END);
            
            // Spatial Audio: sound comes from the object's original position (ghostly) or new position
            PlayCreepySound(targetObj.transform);

            if (m_DebugMode)
            {
                Debug.Log($"Displacement triggered: {targetObj.name} (Hidden: {hiddenObjects.Contains(targetObj)})");
            }
        }

        private bool IsObjectVisible(GameObject obj)
        {
            if (m_PlayerCamera == null || obj == null) return false;

            Vector3 viewportPos = m_PlayerCamera.WorldToViewportPoint(obj.transform.position);
            
            // Check if within viewport (taking a bit of margin to be safe)
            bool inViewport = viewportPos.x >= -0.1f && viewportPos.x <= 1.1f &&
                              viewportPos.y >= -0.1f && viewportPos.y <= 1.1f &&
                              viewportPos.z > 0;

            if (!inViewport) return false;

            // Optional: Raycast check for occlusion could go here, but viewport is enough for "peripheral" logic
            return true;
        }

        private void ExecuteDisplacement(GameObject obj, float amount, bool instant)
        {
             if (obj == null) return;
             
             Vector3 originalPos = m_OriginalPositions[obj];
             Vector3 displacement = new Vector3(
                Random.Range(-amount, amount),
                0,
                Random.Range(-amount, amount)
            );
            
            Vector3 targetPos = obj.transform.position + displacement;

            // Clamp to 1m from ORIGINAL position
            if (Vector3.Distance(targetPos, originalPos) > 1.0f)
            {
                Vector3 dir = (targetPos - originalPos).normalized;
                targetPos = originalPos + dir * 1.0f;
            }
            
            // "The Watcher" Mechanic: Object rotates to face the player
            Quaternion targetRot = obj.transform.rotation;
            if (Random.value < 0.4f + (m_TakeoverProgress * 0.4f)) // Chance increases with progress
            {
                Vector3 directionToPlayer = m_PlayerCamera.transform.position - targetPos;
                directionToPlayer.y = 0; // Keep upright
                if (directionToPlayer != Vector3.zero)
                {
                    targetRot = Quaternion.LookRotation(directionToPlayer);
                    // e.g. for chairs, might need 180 flip depending on model, assuming forward is front
                }
            }
            else if (instant && Random.value < 0.5f)
            {
                 // Small random jitter instead of look at
                 targetRot = obj.transform.rotation * Quaternion.Euler(0, Random.Range(-15f, 15f), 0);
            }
            
            if (!instant)
            {
                 StartCoroutine(SmoothDisplacement(obj, targetPos, targetRot, 2f));
            }
            else
            {
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    rb.MovePosition(targetPos);
                    rb.MoveRotation(targetRot);
                }
                else
                {
                    obj.transform.position = targetPos;
                    obj.transform.rotation = targetRot;
                }
            }
        }

        private IEnumerator SmoothDisplacement(GameObject obj, Vector3 targetPos, Quaternion targetRot, float duration)
        {
            Vector3 startPos = obj.transform.position;
            Quaternion startRot = obj.transform.rotation;
            float elapsed = 0f;

            Rigidbody rb = obj.GetComponent<Rigidbody>();

            while (elapsed < duration)
            {
                if (obj == null) yield break;

                float t = elapsed / duration;
                // Cubic ease out
                t = 1f - Mathf.Pow(1f - t, 3f);

                Vector3 newPos = Vector3.Lerp(startPos, targetPos, t);
                Quaternion newRot = Quaternion.Lerp(startRot, targetRot, t);

                if (rb != null && !rb.isKinematic)
                {
                    rb.MovePosition(newPos);
                    rb.MoveRotation(newRot);
                }
                else
                {
                    obj.transform.position = newPos;
                    obj.transform.rotation = newRot;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Ensure final pose
            if (obj != null)
            {
                if (rb != null && !rb.isKinematic)
                {
                    rb.MovePosition(targetPos);
                    rb.MoveRotation(targetRot);
                }
                else
                {
                    obj.transform.position = targetPos;
                    obj.transform.rotation = targetRot;
                }
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

        private void PlayCreepySound(Transform source = null)
        {
            if (m_CreepySounds == null || m_CreepySounds.Length == 0) return;

            AudioClip clip = m_CreepySounds[Random.Range(0, m_CreepySounds.Length)];
            
            // Spatial Audio Hallucination: Play from specific object source if provided
            if (source != null)
            {
                 AudioSource.PlayClipAtPoint(clip, source.position, 1.0f);
            }
            else if (m_AudioSource != null)
            {
                 // Fallback to 2D / Head
                m_AudioSource.PlayOneShot(clip);
            }
        }

        private IEnumerator HeartbeatRoutine()
        {
            // Haptic Heartbeat: Simulate stress by vibrating controllers
            while (m_IsActive)
            {
                // Heart beat rate increases with progress (60bpm to 140bpm)
                float bpm = Mathf.Lerp(60f, 140f, m_TakeoverProgress);
                float beatInterval = 60f / bpm;

                // Only start feeling it after phase 1
                if (m_TakeoverProgress > PHASE_1_END)
                {
                    float intensity = Mathf.Lerp(0f, 0.5f, m_TakeoverProgress);
                    
                    // Lub
                    HapticPulse(intensity * 0.7f, 0.05f);
                    if(m_HeartbeatSound != null && m_AudioSource != null) m_AudioSource.PlayOneShot(m_HeartbeatSound, intensity * 0.5f);
                    
                    yield return new WaitForSeconds(0.1f);
                    
                    // Dub
                    HapticPulse(intensity, 0.05f);
                    // Optional: Second heartbeat sound usually softer or same
                }

                yield return new WaitForSeconds(beatInterval - 0.1f);
            }
        }

        private void HapticPulse(float amplitude, float duration)
        {
            if (m_LeftController != null)
            {
                var inputDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
                inputDevice.SendHapticImpulse(0, amplitude, duration);
            }
            if (m_RightController != null)
            {
                var inputDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
                inputDevice.SendHapticImpulse(0, amplitude, duration);
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

            // Lock movement
            if (m_MoveProvider != null)
            {
                m_MoveProvider.enabled = false;
            }

            yield return new WaitForSeconds(duration);

            m_IsControllingPlayer = false;
            m_ControlIntensity = 0f;

            // Unlock movement
            if (m_MoveProvider != null)
            {
                m_MoveProvider.enabled = true;
            }

            if (m_DebugMode)
            {
                Debug.Log("Control returned to player.");
            }
        }

        private void ApplyControlTakeover()
        {
            if (m_XROrigin == null) return;

            // Apply forced movement
            // REMOVED: Player wandering is disabled per request.
            // Only rotation is allowed? User update: "dont move the charachter around" -> Disable rotation too.
            // Vector3 movement = m_ForcedMovementDirection * m_ControlIntensity * Time.deltaTime * 0.5f;
            // m_XROrigin.position += movement;

            // Apply forced rotation
            // REMOVED: User requested no movement implies no rotation either.
            // float rotation = m_ForcedRotation * m_ControlIntensity * Time.deltaTime;
            // m_XROrigin.Rotate(0, rotation, 0);

            // In full takeover phase, also mess with hand positions occasionally
            if (m_TakeoverProgress >= PHASE_3_END)
            {
                ApplyHandGlitch();
            }
        }

        // Tracks active hand glitch coroutines to prevent recursion/drift
        private Coroutine m_LeftHandGlitchCoroutine;
        private Coroutine m_RightHandGlitchCoroutine;

        private void ApplyHandGlitch()
        {
            // Random chance to offset controller visuals
            if (Random.value < 0.02f * m_TakeoverProgress)
            {
                if (m_LeftController != null && m_LeftHandGlitchCoroutine == null)
                {
                    m_LeftHandGlitchCoroutine = StartCoroutine(TemporaryHandOffset(m_LeftController, true));
                }
                if (m_RightController != null && m_RightHandGlitchCoroutine == null && Random.value < 0.5f)
                {
                    m_RightHandGlitchCoroutine = StartCoroutine(TemporaryHandOffset(m_RightController, false));
                }
            }
        }

        private void ExecuteFakeCrash()
        {
            if (m_DebugMode)
            {
                Debug.Log("CRITICAL ERROR: AI TAKEOVER COMPLETE. EXECUTING FAKE CRASH.");
            }

            // Play Windows Error Sound
            if (m_AudioSource != null && m_CrashSound != null)
            {
                m_AudioSource.PlayOneShot(m_CrashSound, 1.0f);
            }
            else if (m_CrashSound != null && m_PlayerCamera != null)
            {
                // Fallback: create temporary audio source if needed, or just warn
                AudioSource.PlayClipAtPoint(m_CrashSound, m_PlayerCamera.transform.position, 1.0f);
            }

            // Disable Locomotion
            if (m_MoveProvider != null) m_MoveProvider.enabled = false;
            if (m_TurnProvider != null) m_TurnProvider.enabled = false;
            if (m_TeleportProvider != null) m_TeleportProvider.enabled = false;

            // Hide Controllers (Hands)
            if (m_LeftController != null) m_LeftController.gameObject.SetActive(false);
            if (m_RightController != null) m_RightController.gameObject.SetActive(false);

            // Head-Lock Crash UI & Full Cover
            if (m_CrashUI != null && m_PlayerCamera != null)
            {
                // Force Camera to Solid Color (BSOD effect) to hide the world
                m_PlayerCamera.clearFlags = CameraClearFlags.SolidColor;
                m_PlayerCamera.backgroundColor = new Color(0.0f, 0.0f, 0.5f); // Dark Blue (Classic BSOD)
                // Remove Culling Mask to hide world geometry if SolidColor isn't enough (usually it is if it clears depth)
                // Actually, SolidColor just clears the background. Geometry is still drawn ON TOP.
                // To hide the world, we must set culling mask to ONLY UI.
                
                // Assuming CrashUI is on "UI" layer (index 5) or similar. 
                // Let's just create a "Blackout" effect by changing the Culling Mask.
                // NOTE: This might hide the CrashUI if it's not on the layer we pick.
                // Safer bet: Move CrashUI to "Default" or check its layer? 
                // Let's rely on the Canvas PlaneDistance 0.3 to be in front of everything, 
                // and the Background Image of the Canvas to be opaque.
                
                // Since user said "only see text", their background image is missing/transparent.
                // Let's try to add a background fallback or just rely on the Camera Clear Flags + Culling Mask = Nothing.
                m_PlayerCamera.cullingMask = 0; // Render NOTHING
                // Wait, if we render NOTHING, we won't see the Canvas!
                // We need to render the layer the Canvas is on. Usually "UI" (5).
                m_PlayerCamera.cullingMask = 1 << 5; // User *must* have UI on UI layer.
                
                // To be safe, let's just use the Solid Color and hope they fix their UI background?
                // Or better: Use the Camera Clear Flag, but since we can't easily manipulate the Canvas content here...
                
                // Let's stick to the visual: Screen Space Camera.
                m_CrashUI.SetActive(true);
                
                Canvas crashCanvas = m_CrashUI.GetComponent<Canvas>();
                if (crashCanvas != null)
                {
                    crashCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    crashCanvas.worldCamera = m_PlayerCamera;
                    crashCanvas.planeDistance = 0.3f; // Close to eyes
                }
                else
                {
                    m_CrashUI.transform.position = m_PlayerCamera.transform.position + m_PlayerCamera.transform.forward * 0.5f;
                    m_CrashUI.transform.LookAt(m_PlayerCamera.transform);
                    m_CrashUI.transform.Rotate(0, 180, 0);
                }
            }

            // Pause the game time to simulate freeze
            Time.timeScale = 0f;

            // Stop all further logic
            m_IsActive = false;
        }

        private IEnumerator TemporaryHandOffset(Transform hand, bool isLeft)
        {
            Vector3 originalLocalPos = hand.localPosition;
            Quaternion originalLocalRot = hand.localRotation;

            // Validate transform data to prevent AABB errors
            if (float.IsNaN(originalLocalPos.x) || float.IsInfinity(originalLocalPos.x)) 
            {
                if (isLeft) m_LeftHandGlitchCoroutine = null; else m_RightHandGlitchCoroutine = null;
                yield break;
            }

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
                
                // Use a safe lerp
                hand.localPosition = Vector3.Lerp(originalLocalPos, originalLocalPos + offset, Mathf.Sin(t * Mathf.PI));

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Restore
            hand.localPosition = originalLocalPos;
            hand.localRotation = originalLocalRot;

            // Clear the flag
            if (isLeft) m_LeftHandGlitchCoroutine = null;
            else m_RightHandGlitchCoroutine = null;
        }

        #endregion

        #region Private Methods - Poltergeist

        private void TriggerPoltergeist()
        {
            List<GameObject> moveableObjects = new List<GameObject>();
            foreach(var obj in m_AffectedObjects)
            {
                if(obj == null || !obj.activeInHierarchy) continue;
                // We now accept ANY affected object for Poltergeist, not just non-kinematic RBs
                moveableObjects.Add(obj);
            }

            if(moveableObjects.Count == 0) return;

            GameObject target = moveableObjects[Random.Range(0, moveableObjects.Count)];
            Rigidbody rb = target.GetComponent<Rigidbody>();
            
            PlayCreepySound(target.transform);

            // If allowed to throw AND has a rigidbody that can move...
            if(m_AllowPoltergeistThrow && rb != null && !rb.isKinematic)
            {
                // Throw direction: Up and random side
                Vector3 force = Vector3.up * Random.Range(2f, 5f) + Random.insideUnitSphere * 2f;
                rb.AddForce(force, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
                if(m_DebugMode) Debug.Log($"Poltergeist threw {target.name}");
            }
            else
            {
                // Just Rattle In Place (Violent Glitch)
                StartCoroutine(RattleObject(target));
                if(m_DebugMode) Debug.Log($"Poltergeist rattled {target.name}");
            }
        }

        private IEnumerator RattleObject(GameObject obj)
        {
            Vector3 originalPos = obj.transform.position;
            Quaternion originalRot = obj.transform.rotation;
            float duration = 1.0f;
            float elapsed = 0f;
            float intensity = 0.05f; // Shake amount

            while(elapsed < duration)
            {
                // Random shake
                obj.transform.position = originalPos + Random.insideUnitSphere * intensity;
                obj.transform.rotation = originalRot * Quaternion.Euler(Random.insideUnitSphere * intensity * 100f);
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Restore
            obj.transform.position = originalPos;
            obj.transform.rotation = originalRot;
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
