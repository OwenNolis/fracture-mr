using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Light))]
public class LampFlicker : MonoBehaviour
{
    [Header("Electrical Audio")]
    public AudioClip[] electricalSounds;
    public float soundChance = 0.3f;
    public float volumeMin = 0.15f;
    public float volumeMax = 0.35f;

    [Header("Base Light")]
    public float baseIntensity = 2.8f;

    [Header("Micro Flicker")]
    public float microFlickerMin = 1.8f;
    public float microFlickerMax = 3.2f;
    public float microFlickerChance = 0.6f;

    [Header("Power Drop")]
    public float powerOffChance = 0.15f;
    public float powerOffDurationMin = 0.05f;
    public float powerOffDurationMax = 0.25f;

    [Header("Timing")]
    public float minDelay = 2f;
    public float maxDelay = 9f;

    [Header("AI Pulse")]
    public bool enableAIPulse = true;
    public float pulseSpeed = 0.5f;
    public float pulseStrength = 0.15f;

    private Light lamp;
    private float pulseOffset;
    private AudioSource audioSource;

    void Awake()
    {
        lamp = GetComponent<Light>();
        audioSource = GetComponent<AudioSource>();
        baseIntensity = lamp.intensity;
        pulseOffset = Random.Range(0f, 10f);

        StartCoroutine(FlickerLoop());
    }

    void Update()
    {
        if (!enableAIPulse) return;

        float pulse =
            Mathf.Sin(Time.time * pulseSpeed + pulseOffset) * pulseStrength;

        lamp.intensity = Mathf.Clamp(
            lamp.intensity + pulse,
            0f,
            baseIntensity * 1.1f
        );
    }

    IEnumerator FlickerLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            float roll = Random.value;

            if (roll < powerOffChance)
            {
                TryPlaySound();

                lamp.intensity = 0f;
                yield return new WaitForSeconds(
                    Random.Range(powerOffDurationMin, powerOffDurationMax)
                );
                lamp.intensity = baseIntensity;
            }
            else if (roll < microFlickerChance)
            {
                TryPlaySound();

                lamp.intensity = Random.Range(
                    microFlickerMin,
                    microFlickerMax
                );

                yield return new WaitForSeconds(
                    Random.Range(0.03f, 0.1f)
                );

                lamp.intensity = baseIntensity;
            }
        }
    }

    void TryPlaySound()
    {
        if (electricalSounds == null || electricalSounds.Length == 0) return;
        if (audioSource == null) return;
        if (Random.value > soundChance) return;

        AudioClip clip =
            electricalSounds[Random.Range(0, electricalSounds.Length)];

        audioSource.pitch = Random.Range(0.9f, 1.1f);
        audioSource.PlayOneShot(
            clip,
            Random.Range(volumeMin, volumeMax)
        );
    }
}