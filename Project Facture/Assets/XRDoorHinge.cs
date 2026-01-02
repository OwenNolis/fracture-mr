using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class XRDoorHinge : MonoBehaviour
{
    [Header("Hinge axis (local)")]
    public Vector3 localHingeAxis = Vector3.up; // usually Y

    [Header("Angles")]
    public float closedAngle = 0f;
    public float openAngle = 90f;
    public float minAngle = 0f;
    public float maxAngle = 100f;

    [Header("Snap")]
    public float snapSpeed = 8f;
    public float snapThreshold = 10f;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;
    private Rigidbody rb;

    private Quaternion baseLocalRotation;
    private bool isSnapping;
    private float targetAngle;

    void Awake()
    {
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        // Door should not be thrown around by physics
        rb.isKinematic = true;
        rb.useGravity = false;

        baseLocalRotation = transform.localRotation;
    }

    void OnEnable()
    {
        if (grab != null)
            grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        if (grab != null)
            grab.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        StartSnap();
    }

    void Update()
    {
        if (grab == null) return;

        if (grab.isSelected)
        {
            float angle = Mathf.Clamp(GetSignedAngle(), minAngle, maxAngle);
            SetAngle(angle);
            isSnapping = false;
        }
        else if (isSnapping)
        {
            float angle = GetSignedAngle();
            float next = Mathf.LerpAngle(angle, targetAngle, Time.deltaTime * snapSpeed);
            SetAngle(next);

            if (Mathf.Abs(Mathf.DeltaAngle(next, targetAngle)) < 0.5f)
            {
                SetAngle(targetAngle);
                isSnapping = false;
            }
        }
    }

    void StartSnap()
    {
        float angle = GetSignedAngle();

        float distToClosed = Mathf.Abs(Mathf.DeltaAngle(angle, closedAngle));
        float distToOpen = Mathf.Abs(Mathf.DeltaAngle(angle, openAngle));

        if (distToClosed < snapThreshold) { targetAngle = closedAngle; isSnapping = true; }
        else if (distToOpen < snapThreshold) { targetAngle = openAngle; isSnapping = true; }
    }

    float GetSignedAngle()
    {
        Quaternion rel = Quaternion.Inverse(baseLocalRotation) * transform.localRotation;
        rel.ToAngleAxis(out float angle, out Vector3 axis);

        angle = Mathf.DeltaAngle(0f, angle);

        Vector3 hingeAxis = localHingeAxis.sqrMagnitude > 0.0001f ? localHingeAxis.normalized : Vector3.up;
        float sign = Mathf.Sign(Vector3.Dot(axis, hingeAxis));
        return angle * sign;
    }

    void SetAngle(float angle)
    {
        Vector3 hingeAxis = localHingeAxis.sqrMagnitude > 0.0001f ? localHingeAxis.normalized : Vector3.up;
        transform.localRotation = baseLocalRotation * Quaternion.AngleAxis(angle, hingeAxis);
    }
}