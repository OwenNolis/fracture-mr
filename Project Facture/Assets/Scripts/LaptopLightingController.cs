using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI; 
using Unity.VRTemplate; // Namespace for OverseerSystem

public class LaptopLightingController : MonoBehaviour
{
    [Tooltip("The Light component to toggle on after delay.")]
    public Light laptopLight;

    [Tooltip("The MeshRenderer of the laptop that has the emissive material.")]
    public Renderer laptopRenderer;

    [Tooltip("Time in seconds to wait before turning on the light.")]
    public float delaySeconds = 5.0f;

    [Tooltip("The AudioSource to play (bootup sound).")]
    public AudioSource bootupAudio;

    [Tooltip("How many seconds before the light turns on should the audio start?")]
    public float audioLeadTime = 0.5f;

    [Header("UI Interaction")]
    [Tooltip("The world space canvas on the laptop screen.")]
    public GameObject laptopCanvas;
    [Tooltip("The panel containing the Y/N buttons.")]
    public GameObject optionsPanel;
    [Tooltip("The panel showing the post-selection feedback.")]
    public GameObject feedbackPanel;

    [Header("Overseer Integration")]
    [Tooltip("Reference to the Overseer System. Will try to auto-find if empty.")]
    public OverseerSystem overseerSystem;

    [Header("Visual Effects")]
    [Tooltip("Duration in seconds for the initial bright flash.")]
    public float flashDuration = 1.0f;
    [Tooltip("Intensity multiplier for the dimmed state (0-1).")]
    public float dimmedIntensity = 0.15f;

    // Common shader property for emission color is "_EmissionColor"
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private Color _originalEmissionColor;
    private float _originalLightIntensity;

    void Start()
    {
        // Auto-find Overseer System if not assigned
        if (overseerSystem == null)
        {
            overseerSystem = FindFirstObjectByType<OverseerSystem>();
            if (overseerSystem == null)
            {
                Debug.LogWarning("LaptopLightingController: No OverseerSystem found in scene!");
            }
        }

        // Initial state: Dark
        if (laptopLight != null)
        {
            _originalLightIntensity = laptopLight.intensity;
            laptopLight.enabled = false;
        }

        if (laptopRenderer != null)
        {
            // Store the original emission color from the material to restore it later
            _originalEmissionColor = laptopRenderer.material.GetColor(EmissionColorProperty);
            
            // Set emission color to black (off)
            laptopRenderer.material.SetColor(EmissionColorProperty, Color.black);
            DynamicGI.SetEmissive(laptopRenderer, Color.black);
        }

        if (laptopCanvas != null)
        {
            laptopCanvas.SetActive(false);
        }
        if (optionsPanel != null) optionsPanel.SetActive(true);
        if (feedbackPanel != null) feedbackPanel.SetActive(false);

        StartCoroutine(TurnOnAfterDelay());
    }

    IEnumerator TurnOnAfterDelay()
    {
        // Calculate when to start the audio: (TotalDelay - LeadTime), but not less than 0
        float timeUntilAudio = Mathf.Max(0f, delaySeconds - audioLeadTime);
        yield return new WaitForSeconds(timeUntilAudio);

        if (bootupAudio != null)
        {
            bootupAudio.Play();
        }

        // Wait the remaining time (if any) until the full delay is reached
        float remainingTime = delaySeconds - timeUntilAudio;
        yield return new WaitForSeconds(remainingTime);

        // --- FLASH STAGE (Full Brightness) ---
        if (laptopLight != null)
        {
            laptopLight.enabled = true;
            laptopLight.intensity = _originalLightIntensity;
        }

        if (laptopRenderer != null)
        {
            // Restore the original emission color (Full Brightness)
            laptopRenderer.material.SetColor(EmissionColorProperty, _originalEmissionColor);
            DynamicGI.SetEmissive(laptopRenderer, _originalEmissionColor);
        }

        if (laptopCanvas != null)
        {
            laptopCanvas.SetActive(true);
        }

        // Wait for the flash duration
        yield return new WaitForSeconds(flashDuration);

        // --- DIMMED STAGE (Readability) ---
        if (laptopLight != null)
        {
            laptopLight.intensity = _originalLightIntensity * dimmedIntensity;
        }

        if (laptopRenderer != null)
        {
            Color dimmedColor = _originalEmissionColor * dimmedIntensity;
            laptopRenderer.material.SetColor(EmissionColorProperty, dimmedColor);
            DynamicGI.SetEmissive(laptopRenderer, dimmedColor);
        }
    }

    /// <summary>
    /// Call this method from the UI Buttons (On Click).
    /// </summary>
    public void OnOptionSelected()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (feedbackPanel != null) feedbackPanel.SetActive(true);

        // Trigger the Overseer System
        if (overseerSystem != null)
        {
            overseerSystem.ActivateOverseer();
        }
        else
        {
            Debug.LogError("LaptopLightingController: Cannot activate Overseer - System not found!");
        }
    }
}
