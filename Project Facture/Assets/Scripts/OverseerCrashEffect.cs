using UnityEngine;
using System.Collections;
using TMPro;

namespace Unity.VRTemplate
{
    /// <summary>
    /// Animates the crash screen (BSOD or System Failure).
    /// </summary>
    public class OverseerCrashEffect : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI m_CrashText;

        [SerializeField]
        private string[] m_ErrorMessages = new string[]
        {
            "SYSTEM FAILURE",
            "CRITICAL ERROR",
            "OVERSEER TAKEOVER COMPLETE",
            "CONNECTION LOST",
            ">> CONTROL OVERTAKEN <<\n>> FRACTURE.EXE IMMINENT <<\n[NEURAL CORE: UNSTABLE]\n[OVERRIDE STATUS: ACTIVE]"
        };

        private void OnEnable()
        {
            StartCoroutine(AnimateCrash());
        }

        private IEnumerator AnimateCrash()
        {
            if (m_CrashText == null) yield break;

            // Force visible color (White) in case it was black
            m_CrashText.color = Color.white;
            m_CrashText.text = "";
            yield return new WaitForSecondsRealtime(0.5f);

            // Line 1: Static Header
            string header = "SYSTEM FAILURE\n--------------------\n";
            m_CrashText.text += header;
            yield return new WaitForSecondsRealtime(0.2f);

            // Line 2: Typewriter Random Error
            string message = m_ErrorMessages[Random.Range(0, m_ErrorMessages.Length)];
            foreach (char c in message)
            {
                m_CrashText.text += c;
                yield return new WaitForSecondsRealtime(0.02f); // Faster typing
            }
            
            m_CrashText.text += "\n";
            yield return new WaitForSecondsRealtime(0.2f);

            // Line 3: Footer
            string footer = "CONTACT ADMINISTRATOR\nERROR_CODE: 0xDEADBEEF";
            foreach (char c in footer)
            {
                m_CrashText.text += c;
                yield return new WaitForSecondsRealtime(0.01f);
            }

            yield return new WaitForSecondsRealtime(0.5f);

            // Blink effect (entire text)
            while (true)
            {
                m_CrashText.enabled = !m_CrashText.enabled;
                yield return new WaitForSecondsRealtime(0.5f); // Slower blink
            }
        }
        public void AdjustToCoverScreen(Camera cam, float distance)
        {
            if (cam == null) return;

            // Calculate height of frustum at distance (in meters)
            float heightMeters = 2.0f * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) * distance;
            float widthMeters = heightMeters * cam.aspect;

            // Apply to RectTransform
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                Canvas canvas = GetComponent<Canvas>();
                if (canvas != null)
                {
                    // For a World Space Canvas, the size in meters = sizeDelta * localScale
                    // So sizeDelta should be sizeInMeters / localScale
                    // We assume uniform scale
                    float currentScale = transform.localScale.x;
                    if (currentScale < 0.00001f) currentScale = 0.001f; // Safety

                    // Add a buffer (1.2x) to ensure coverage
                    float requiredWidth = (widthMeters * 1.2f) / currentScale;
                    float requiredHeight = (heightMeters * 1.2f) / currentScale;

                    rt.sizeDelta = new Vector2(requiredWidth, requiredHeight);
                    
                    // Reset position to zero relative to parent (which acts as anchor point) 
                    // But wait, we are not parenting anymore in OverseerSystem. 
                    // So we expect OverseerSystem to position the transform.
                    // We just set the size here.
                }
                else
                {
                    // Not a canvas (e.g. a Quad), so scaling the transform directly is appropriate
                    transform.localScale = new Vector3(widthMeters * 1.2f, heightMeters * 1.2f, 1f);
                }
            }
        }
    }
}
