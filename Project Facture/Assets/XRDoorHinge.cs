using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class XRDoorHinge : MonoBehaviour
{
    [Header("Hinge axis (local)")]
    public Vector3 localHingeAxis = Vector3.up; // usually Y

    [Header("Angles")]
    public float closedAngle = 0f;
    public float openAngle = 90f;

    [Header("Animation")]
    public float animationSpeed = 2f; // Speed of opening/closing
    
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    private Rigidbody rb;

    private Quaternion baseLocalRotation;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;

    private bool isOpen = false;
    private float currentAngle;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        rb = GetComponent<Rigidbody>();

        // Kinematic setup to prevent physics issues
        rb.isKinematic = true;
        rb.useGravity = false;

        baseLocalRotation = transform.localRotation;
        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;

        // Initialize current angle logic
        currentAngle = closedAngle; 
    }

    void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
        }
    }

    void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        ToggleDoor();
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }

    void Update()
    {
        // 1. Enforce Locking to prevent drift/skew
        transform.localPosition = baseLocalPosition;
        transform.localScale = baseLocalScale;

        // 2. Animate Door
        float target = isOpen ? openAngle : closedAngle;

        // Smoothly move current angle towards target
        currentAngle = Mathf.MoveTowards(currentAngle, target, Time.deltaTime * animationSpeed * 10f); // *10 scaling for easier inspector values
        
        SetAngle(currentAngle);
    }

    void SetAngle(float angle)
    {
        Vector3 hingeAxis = localHingeAxis.sqrMagnitude > 0.0001f ? localHingeAxis.normalized : Vector3.up;
        transform.localRotation = baseLocalRotation * Quaternion.AngleAxis(angle, hingeAxis);
    }
}