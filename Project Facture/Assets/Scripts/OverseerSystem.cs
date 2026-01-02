using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.VRTemplate
{
    /// <summary>
    /// AI Overseer Takeover System
    /// Refactored to focus on Material/Shader manipulation and Atmosphere.
    /// No longer affects player movement or object physics/transforms.
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
        [Tooltip("Objects that can be affected by simple glitches (Glitch Material).")]
        private List<GameObject> m_AffectedObjects = new List<GameObject>();

        [SerializeField]
        [Tooltip("If true, automatically finds all objects with specified tags.")]
        private bool m_AutoFindAffectedObjects = true;

        [SerializeField]
        [Tooltip("Tag for standard glitch objects.")]
        private string m_AffectedObjectTag = "AffectedByOverseer";

        [Header("Propaganda Settings")]
        [SerializeField]
        [Tooltip("Tag for objects that should receive static/poster propaganda (e.g. Posters).")]
        private string m_PropagandaStaticTag = "PropagandaByOverseerStatic";

        [SerializeField]
        [Tooltip("List of 'Static' or 'Poster' materials to randomly apply to static propaganda objects.")]
        private List<Material> m_PropagandaStaticMaterials = new List<Material>();

        [SerializeField]
        [Tooltip("Tag for objects that should play the propaganda film (e.g. TVs).")]
        private string m_PropagandaFilmTag = "PropagandaByOverseerFilm";

        [SerializeField]
        [Tooltip("List of Materials containing propaganda videos/films.")]
        private List<Material> m_PropagandaFilmMaterials = new List<Material>();

        [Header("VR Control References")]
        [SerializeField]
        [Tooltip("Reference to the XR Origin/Rig.")]
        private Transform m_XROrigin;

        [SerializeField]
        [Tooltip("Reference to the main camera (player head).")]
        private Camera m_PlayerCamera;

        [SerializeField]
        [Tooltip("Left hand controller transform (for Haptics).")]
        private Transform m_LeftController;

        [SerializeField]
        [Tooltip("Right hand controller transform (for Haptics).")]
        private Transform m_RightController;

        [Header("Glitch Effects")]
        [SerializeField]
        [Tooltip("Material to apply for standard glitch effect.")]
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
        [Tooltip("Reference to the GlitchController for shader-based glitch effects.")]
        private GlitchController m_GlitchController;

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
        [SerializeField]
        [Tooltip("Heartbeat sound clip (optional).")]
        private AudioClip m_HeartbeatSound;

        // Propoganda groupings
        private List<GameObject> m_PropagandaStaticObjects = new List<GameObject>();
        private List<GameObject> m_PropagandaFilmObjects = new List<GameObject>();

        // Coroutine references
        private Coroutine m_MainLoopCoroutine;

        // Crash timing
        private float m_TakeoverCompleteTime = -1f;

        // Timing for events
        private float m_NextGlitchTime = 0f;
        private float m_NextSoundTime = 0f;

        // Phase thresholds
        private const float PHASE_VISUAL_START = 0.7f; // 70% - Glitches start here

        // Glitch Controller tracking
        private float m_GlitchBurstTimer = 0f;
        private float m_TargetNoiseAmount = 0f;
        private float m_TargetGlitchStrength = 0f;
        private float m_TargetScanLineStrength = 0f;
        private float m_GlitchLerpSpeed = 2f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (m_AutoFindAffectedObjects)
            {
                FindAllOverseerObjects();
            }
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

            // Update the glitch controller shader effects
            UpdateGlitchController();

            if (m_DebugMode)
            {
                //Debug.Log($"Overseer Progress: {m_TakeoverProgress * 100:F1}%");
            }
        }

        private void OnDestroy()
        {
            DeactivateOverseer();
        }

        #endregion

        #region Public Methods

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

        public void DeactivateOverseer()
        {
            m_IsActive = false;

            if (m_MainLoopCoroutine != null)
            {
                StopCoroutine(m_MainLoopCoroutine);
            }

            // Reset glitch controller
            if (m_GlitchController != null)
            {
                m_GlitchController.ResetGlitch();
            }

            if (m_DebugMode)
            {
                Debug.Log("OVERSEER SYSTEM DEACTIVATED - Order restored.");
            }
        }

        public float GetProgress() => m_TakeoverProgress;

        /// <summary>
        /// Restored method for backward compatibility with OverseerAffectedObject script.
        /// Registers an object to be affected by standard glitches.
        /// </summary>
        public void AddAffectedObject(GameObject obj)
        {
            if (obj != null && !m_AffectedObjects.Contains(obj))
            {
                m_AffectedObjects.Add(obj);
                if (m_DebugMode) Debug.Log($"[OverseerSystem] Added affected object: {obj.name}");
            }
        }

        #endregion

        #region Private Methods - Initialization

        private void FindAllOverseerObjects()
        {
            // 1. Standard Glitch Objects
            var standardObjs = GameObject.FindGameObjectsWithTag(m_AffectedObjectTag);
            foreach (var obj in standardObjs)
            {
                if (!m_AffectedObjects.Contains(obj)) m_AffectedObjects.Add(obj);
            }

            // 2. Propaganda Static (Posters)
            var staticObjs = GameObject.FindGameObjectsWithTag(m_PropagandaStaticTag);
            foreach (var obj in staticObjs)
            {
                if (!m_PropagandaStaticObjects.Contains(obj)) m_PropagandaStaticObjects.Add(obj);
            }

            // 3. Propaganda Film (TVs)
            var filmObjs = GameObject.FindGameObjectsWithTag(m_PropagandaFilmTag);
            foreach (var obj in filmObjs)
            {
                if (!m_PropagandaFilmObjects.Contains(obj)) m_PropagandaFilmObjects.Add(obj);
            }

            Debug.Log($"[OverseerSystem] Found:\n" +
                      $"- {m_AffectedObjects.Count} Standard Affected Objects\n" +
                      $"- {m_PropagandaStaticObjects.Count} Propaganda Static Objects\n" +
                      $"- {m_PropagandaFilmObjects.Count} Propaganda Film Objects");
        }

        private void FindVRComponents()
        {
            if (m_XROrigin == null)
            {
                var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (xrOrigin != null) m_XROrigin = xrOrigin.transform;
            }

            if (m_PlayerCamera == null) m_PlayerCamera = Camera.main;

            if (m_GlitchController == null)
            {
                m_GlitchController = FindFirstObjectByType<GlitchController>();
            }
        }

        #endregion

        #region Private Methods - Main Loop

        private IEnumerator OverseerMainLoop()
        {
            SetNextEventTimes();

            while (m_IsActive)
            {
                float currentTime = m_ElapsedTime;

                // Check for material glitch events
                if (currentTime >= m_NextGlitchTime)
                {
                    TriggerMaterialGlitch();
                    SetNextGlitchTime();
                }

                // Check for sound events
                if (currentTime >= m_NextSoundTime && m_CreepySounds != null && m_CreepySounds.Length > 0)
                {
                    PlayCreepySound();
                    SetNextSoundTime();
                }

                // Check for completion (Fake Crash)
                if (m_TakeoverProgress >= 1.0f)
                {
                    if (m_TakeoverCompleteTime < 0)
                    {
                        m_TakeoverCompleteTime = Time.time;
                        if (m_DebugMode) Debug.Log($"Takeover Complete (100%). Waiting {m_CrashDelay}s for crash...");
                    }

                    if (Time.time >= m_TakeoverCompleteTime + m_CrashDelay)
                    {
                        ExecuteFakeCrash();
                        yield break;
                    }
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        private void SetNextEventTimes()
        {
            SetNextGlitchTime();
            SetNextSoundTime();
        }

        private void SetNextGlitchTime()
        {
            // Fully random intervals based on progress
            // Low progress: 10s to 30s
            // High progress: 2s to 8s
            float minInterval = Mathf.Lerp(10f, 2f, m_TakeoverProgress);
            float maxInterval = Mathf.Lerp(30f, 8f, m_TakeoverProgress);

            m_NextGlitchTime = m_ElapsedTime + Random.Range(minInterval, maxInterval);
        }

        private void SetNextSoundTime()
        {
            float baseInterval = Mathf.Lerp(30f, 5f, m_TakeoverProgress);
            m_NextSoundTime = m_ElapsedTime + baseInterval + Random.Range(-2f, 2f);
        }

        #endregion

        #region Private Methods - Material Effects

        private void TriggerMaterialGlitch()
        {
            // Requirements:
            // - Propaganda starts at 40%
            // - Propaganda frequent from 60%
            // - Variable durations (short to long)

            if (m_TakeoverProgress < 0.4f) return; // Nothing before 40%

            // Can trigger multiple types at once in later stages
            int simultaneousGlitches = 1;

            // TOTAL CHAOS PHASE (Near 100%)
            if (m_TakeoverProgress > 0.98f)
            {
                TriggerTotalChaos();
                return;
            }

            // Scaling Intensity
            // 75%+: 12-22 objects
            if (m_TakeoverProgress > 0.75f)
            {
                simultaneousGlitches = Random.Range(12, 23);
            }
            // 50%+: 5-12 objects
            else if (m_TakeoverProgress > 0.5f)
            {
                simultaneousGlitches = Random.Range(5, 13);
            }
            // 40%+: 1-3 objects
            else if (m_TakeoverProgress > 0.4f)
            {
                simultaneousGlitches = Random.Range(1, 4);
            }

            // Calculate Propaganda Probability based on progress
            // 40% -> 20% chance
            // 60% -> 70% chance (High freq)
            // 70% + -> 95% chance
            float propagandaChance = 0.2f;
            if (m_TakeoverProgress > 0.6f) propagandaChance = 0.7f;
            if (m_TakeoverProgress > 0.7f) propagandaChance = 0.95f;

            for (int i = 0; i < simultaneousGlitches; i++)
            {
                // Determine type of glitch
                float roll = Random.value;

                // Random duration
                // < 70%: Short to Medium (5s - 15s)
                // > 70%: Long to Permanent-feeling (15s - 45s)
                float minDur = 5f;
                float maxDur = 15f;

                if (m_TakeoverProgress > 0.7f)
                {
                    minDur = 15f;
                    maxDur = 45f;
                }

                float duration = Random.Range(minDur, maxDur);

                // Priority to Propaganda based on calculated chance
                // Check Film
                if (m_PropagandaFilmObjects.Count > 0 && roll < (propagandaChance * 0.4f)) // 40% of the propaganda budget is film
                {
                    var obj = m_PropagandaFilmObjects[Random.Range(0, m_PropagandaFilmObjects.Count)];

                    // Select random film material
                    Material randomFilm = null;
                    if (m_PropagandaFilmMaterials.Count > 0)
                        randomFilm = m_PropagandaFilmMaterials[Random.Range(0, m_PropagandaFilmMaterials.Count)];

                    if (randomFilm != null)
                        StartCoroutine(ApplyReplacingMaterial(obj, randomFilm, duration, "TVframes"));
                }
                // Check Static
                else if (m_PropagandaStaticObjects.Count > 0 && roll < propagandaChance)
                {
                    var obj = m_PropagandaStaticObjects[Random.Range(0, m_PropagandaStaticObjects.Count)];
                    // Random static material
                    Material randomMat = null;
                    if (m_PropagandaStaticMaterials.Count > 0)
                        randomMat = m_PropagandaStaticMaterials[Random.Range(0, m_PropagandaStaticMaterials.Count)];

                    if (randomMat != null)
                        // Replace "posters" or "posters shop" materials
                        StartCoroutine(ApplyReplacingMaterial(obj, randomMat, duration, "posters"));
                }
                else if (m_AffectedObjects.Count > 0)
                {
                    // Standard noise glitch for non-propaganda moments
                    var obj = m_AffectedObjects[Random.Range(0, m_AffectedObjects.Count)];
                    StartCoroutine(ApplyReplacingMaterial(obj, m_GlitchMaterial, Random.Range(0.2f, 1.0f), null));
                }
            }
        }

        /// <summary>
        /// Temporarily replaces the material of an object with a glitch/propaganda material.
        /// Optional: filtered by material name string (contains).
        /// </summary>
        private IEnumerator ApplyReplacingMaterial(GameObject obj, Material newMaterial, float duration, string targetMaterialNameFilter = null)
        {
            if (obj == null || newMaterial == null) yield break;

            Renderer r = obj.GetComponent<Renderer>();
            if (r == null) yield break;

            Material[] originalMats = r.materials;
            Material[] newMats = new Material[originalMats.Length];
            bool materialWasSwapped = false;

            for (int i = 0; i < originalMats.Length; i++)
            {
                // Check if we should replace this specific material index
                bool shouldReplace = false;

                if (string.IsNullOrEmpty(targetMaterialNameFilter))
                {
                    // No filter = replace all
                    shouldReplace = true;
                    if (m_DebugMode) Debug.Log($"[Overseer] Replaced material '{originalMats[i].name}' on '{obj.name}' (No filter applied)");
                }
                else
                {
                    // Filter active: Check if name contains the target string
                    string matName = originalMats[i].name.ToLower();
                    if (matName.Contains(targetMaterialNameFilter.ToLower()))
                    {
                        shouldReplace = true;
                        if (m_DebugMode) Debug.Log($"[Overseer] Replaced material '{originalMats[i].name}' on '{obj.name}' (MATCHED filter '{targetMaterialNameFilter}')");
                    }
                    else
                    {
                        if (m_DebugMode) Debug.Log($"[Overseer] SKIPPED material '{originalMats[i].name}' on '{obj.name}' (Did NOT match filter '{targetMaterialNameFilter}')");
                    }
                }

                if (shouldReplace)
                {
                    newMats[i] = newMaterial;
                    // Auto-Scale Attempt: Reset tiling to 1,1
                    if (newMats[i].HasProperty("_BaseMapST")) newMats[i].SetVector("_BaseMapST", new Vector4(1, 1, 0, 0));
                    if (newMats[i].HasProperty("_MainTex_ST")) newMats[i].SetVector("_MainTex_ST", new Vector4(1, 1, 0, 0));

                    materialWasSwapped = true;
                }
                else
                {
                    newMats[i] = originalMats[i]; // Keep original
                }
            }

            // Only apply if we actually found something to swap
            if (materialWasSwapped)
            {
                r.materials = newMats;

                // Trigger visual burst if global progress is high enough
                if (m_TakeoverProgress >= PHASE_VISUAL_START)
                {
                    TriggerGlitchBurst(0.2f);
                }

                yield return new WaitForSeconds(duration);

                if (obj != null && r != null)
                {
                    r.materials = originalMats;
                }
            }
            else
            {
                if (m_DebugMode) Debug.Log($"[Overseer] FAILED to swap any materials on '{obj.name}'. Check keywords!");
            }
        }

        #endregion

        #region Private Methods - Audio & Haptics

        private void PlayCreepySound()
        {
            if (m_CreepySounds == null || m_CreepySounds.Length == 0) return;

            AudioClip clip = m_CreepySounds[Random.Range(0, m_CreepySounds.Length)];

            // Play 2D
            if (m_AudioSource != null)
            {
                m_AudioSource.PlayOneShot(clip);
            }
        }

        private IEnumerator HeartbeatRoutine()
        {
            // Haptic Heartbeat
            while (m_IsActive)
            {
                // Heart beat rate increases with progress (60bpm to 140bpm)
                float bpm = Mathf.Lerp(60f, 140f, m_TakeoverProgress);
                float beatInterval = 60f / bpm;

                // Only start feeling it after 30%
                if (m_TakeoverProgress > 0.3f)
                {
                    float intensity = Mathf.Lerp(0f, 0.5f, m_TakeoverProgress);

                    // Lub
                    HapticPulse(intensity * 0.7f, 0.05f);
                    if (m_HeartbeatSound != null && m_AudioSource != null) m_AudioSource.PlayOneShot(m_HeartbeatSound, intensity * 0.5f);

                    yield return new WaitForSeconds(0.1f);

                    // Dub
                    HapticPulse(intensity, 0.05f);
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

        /// <summary>
        /// Triggers total chaos on ALL objects.
        /// Mix of permanent takeover and flashing effects.
        /// </summary>
        private void TriggerTotalChaos()
        {
            // 1. Film Objects
            foreach (var obj in m_PropagandaFilmObjects)
            {
                if (obj == null) continue;

                Material randomFilm = null;
                if (m_PropagandaFilmMaterials.Count > 0)
                    randomFilm = m_PropagandaFilmMaterials[Random.Range(0, m_PropagandaFilmMaterials.Count)];

                if (randomFilm != null)
                {
                    // 50% Permanent, 50% Flashing
                    if (Random.value > 0.5f)
                        StartCoroutine(ApplyReplacingMaterial(obj, randomFilm, 999f, "TVframes")); // Permanent
                    else
                        StartCoroutine(FlashMaterialRoutine(obj, randomFilm, "TVframes")); // Flashing
                }
            }

            // 2. Static Objects
            foreach (var obj in m_PropagandaStaticObjects)
            {
                if (obj == null) continue;
                Material randomMat = null;
                if (m_PropagandaStaticMaterials.Count > 0)
                    randomMat = m_PropagandaStaticMaterials[Random.Range(0, m_PropagandaStaticMaterials.Count)];

                if (randomMat != null)
                {
                    if (Random.value > 0.5f)
                        StartCoroutine(ApplyReplacingMaterial(obj, randomMat, 999f, "posters"));
                    else
                        StartCoroutine(FlashMaterialRoutine(obj, randomMat, "posters"));
                }
            }
        }

        private IEnumerator FlashMaterialRoutine(GameObject obj, Material glitchMat, string filter)
        {
            // Rapidly swap between original and glitch
            // This loop runs until the object is destroyed or the script stops
            while (obj != null)
            {
                // Glitch ON
                yield return StartCoroutine(ApplyReplacingMaterial(obj, glitchMat, Random.Range(0.05f, 0.15f), filter));
                // Wait small random time before next glitch
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
        }

        #endregion

        #region Private Methods - Screen Glitch Logic

        private void UpdateGlitchController()
        {
            if (m_GlitchController == null) return;

            // Decrement glitch burst timer
            if (m_GlitchBurstTimer > 0f)
            {
                m_GlitchBurstTimer -= Time.deltaTime;
            }

            float baseNoiseAmount = 0f;
            float baseGlitchStrength = 0f;
            float baseScanLineStrength = 0f;

            // VISUALS ONLY START AT 70%
            if (m_TakeoverProgress >= PHASE_VISUAL_START)
            {
                // Re-map progress from 70-100% to 0-1 range for intensity calculation
                float glitchPhaseProgress = Mathf.InverseLerp(PHASE_VISUAL_START, 1.0f, m_TakeoverProgress);

                // Explosive chaos ONLY near the very end (95%+)
                // Before that, keep it subtle

                if (m_TakeoverProgress > 0.95f)
                {
                    // 95-100%: HIGH INTENSITY RAMP
                    float chaosProgress = Mathf.InverseLerp(0.95f, 1.0f, m_TakeoverProgress);
                    baseNoiseAmount = Mathf.Lerp(0.1f, 0.4f, chaosProgress);
                    baseScanLineStrength = Mathf.Lerp(0.2f, 0.8f, chaosProgress);
                    baseGlitchStrength = Mathf.Lerp(0.05f, 0.3f, chaosProgress);
                }
                else
                {
                    // 70-95%: SUBTLE / INTERFERENCE
                    baseNoiseAmount = Mathf.Lerp(0.02f, 0.08f, glitchPhaseProgress); // Very light noise
                    baseScanLineStrength = Mathf.Lerp(0.05f, 0.15f, glitchPhaseProgress); // Light scanlines
                    baseGlitchStrength = Mathf.Lerp(0.0f, 0.02f, glitchPhaseProgress); // Barely any distortion
                }
            }

            // Apply BURST (Explosive effect)
            float burstMultiplier = 1f;
            if (m_GlitchBurstTimer > 0f)
            {
                burstMultiplier = 2.5f; // Strong burst
            }

            // Calculate targets
            m_TargetNoiseAmount = Mathf.Clamp01(baseNoiseAmount * burstMultiplier);
            m_TargetScanLineStrength = Mathf.Clamp01(baseScanLineStrength * burstMultiplier);
            m_TargetGlitchStrength = Mathf.Clamp(baseGlitchStrength * burstMultiplier, 0f, 0.3f); // Hard cap for comfort

            // Interpolate
            m_GlitchController.LerpTowards(
                m_TargetNoiseAmount,
                m_TargetGlitchStrength,
                m_TargetScanLineStrength,
                m_GlitchLerpSpeed
            );
        }

        public void TriggerGlitchBurst(float duration = 0.3f)
        {
            if (m_GlitchController == null) return;
            // Only allow bursts if we are past the 70% mark, OR if it's the very end
            if (m_TakeoverProgress < PHASE_VISUAL_START && m_TakeoverProgress < 0.99f) return;

            m_GlitchBurstTimer = duration;
            m_GlitchLerpSpeed = 10f; // Snappy attack
            StartCoroutine(ResetGlitchLerpSpeed(duration));
        }

        private IEnumerator ResetGlitchLerpSpeed(float delay)
        {
            yield return new WaitForSeconds(delay);
            m_GlitchLerpSpeed = 2f; // Smooth release
        }

        #endregion

        #region Private Methods - Crash

        private void ExecuteFakeCrash()
        {
            if (m_DebugMode) Debug.Log("CRITICAL ERROR: AI TAKEOVER COMPLETE. EXECUTING FAKE CRASH.");

            if (m_GlitchController != null)
            {
                m_GlitchController.SetGlitchValues(1.0f, 0.2f, 1.0f); // Maximize noise/scanlines
            }

            // STOP ALL OTHER AUDIO
            AudioSource[] allAudio = FindObjectsOfType<AudioSource>();
            foreach (var audio in allAudio)
            {
                if (audio != m_AudioSource)
                {
                    audio.Stop();
                    audio.enabled = false;
                }
            }

            // LOOP CRASH SOUND
            if (m_AudioSource != null && m_CrashSound != null)
            {
                m_AudioSource.Stop();
                m_AudioSource.clip = m_CrashSound;
                m_AudioSource.loop = true;
                m_AudioSource.Play();
            }

            // Hide Controllers
            if (m_LeftController != null) m_LeftController.gameObject.SetActive(false);
            if (m_RightController != null) m_RightController.gameObject.SetActive(false);

            // Show UI
            if (m_CrashUI != null && m_PlayerCamera != null)
            {
                // ROBUST CRASH UI - ATTEMPT 3
                // 1. Create a dedicated container
                GameObject crashRoot = new GameObject("OverseerCrashRoot");

                // 2. Parent it to the camera (Head-Locked)
                crashRoot.transform.SetParent(m_PlayerCamera.transform);
                crashRoot.transform.localPosition = new Vector3(0f, 0f, 0.4f); // 0.4m in front (closer for better coverage)
                crashRoot.transform.localRotation = Quaternion.identity;
                crashRoot.transform.localScale = Vector3.one;

                // 3. Make the user's UI a child of this root
                m_CrashUI.SetActive(true);
                m_CrashUI.transform.SetParent(crashRoot.transform, false);

                // 4. Force Canvas settings if it is a canvas
                Canvas c = m_CrashUI.GetComponent<Canvas>();
                if (c == null) c = m_CrashUI.GetComponentInParent<Canvas>();

                if (c != null)
                {
                    c.renderMode = RenderMode.WorldSpace;
                    c.sortingOrder = 32767;

                    // Reset transform to be centered
                    c.transform.localPosition = Vector3.zero;
                    c.transform.localRotation = Quaternion.identity;
                    c.transform.localScale = Vector3.one * 0.001f; // Standard UI Scale

                    // FORCE FULL SCREEN SIZE
                    // At 0.4m distance, a 2m x 2m canvas covers >135 degrees FOV.
                    // Scale 0.001 -> 2000 units = 2 meters.
                    RectTransform rt = c.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.sizeDelta = new Vector2(2500f, 2000f); // Wide & Tall enough to cover everything
                    }
                }
                else
                {
                    // If no canvas (just a quad/object), reset its transform
                    m_CrashUI.transform.localPosition = Vector3.zero;
                    m_CrashUI.transform.localRotation = Quaternion.identity;
                    // If it's a quad, 2.5 scale might be needed
                    m_CrashUI.transform.localScale = Vector3.one * 2.5f;
                }
            }

            // Freeze game to simulate crash
            Time.timeScale = 0f;
            m_IsActive = false;
        }

        private void OnGUI()
        {
            if (!m_DebugMode) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 150), GUI.skin.box);
            GUILayout.Label($"Overseer Progress: {(m_TakeoverProgress * 100):F1}%");

            string phase = "Dormant/Subtle";
            if (m_TakeoverProgress > PHASE_VISUAL_START) phase = "VISUAL GLITCHES";
            if (m_TakeoverProgress > 0.9f) phase = "CRITICAL / CHAOS";

            GUILayout.Label($"Phase: {phase}");
            GUILayout.Label($"Next Glitch In: {(m_NextGlitchTime - m_ElapsedTime):F1}s");
            GUILayout.EndArea();
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && m_AudioSource != null)
                m_AudioSource.PlayOneShot(clip);
        }

        #endregion
    }
}
