using UnityEngine;

/// <summary>
/// Simple smooth follow camera using only `offset` (local-space) for position
/// and `lookOffset` (world-space) for the look target. All manual/orbit/zoom
/// input and unused fields removed for clarity.
/// </summary>
[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow")]
    [Tooltip("Local-space offset from the target used to compute desired camera position")]
    public Vector3 offset = new Vector3(0f, 5f, -8f);

    [Tooltip("World-space additional offset applied to the look target (fine control)")]
    public Vector3 lookOffset = new Vector3(0f, 2.5f, 0f);

    [Header("Smoothing")]
    [Tooltip("Smooth time for position smoothing")]
    public float positionSmoothTime = 0.1f;
    [Tooltip("Rotation smoothing factor (0..1) where larger is faster)")]
    public float rotationSmoothTime = 0.08f;

    // internal smoothing helper
    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired world position using the target's local-space offset
        Vector3 desiredPosition = target.TransformPoint(offset);

        // Smoothly move the camera to the desired position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

        // Smoothly rotate camera to look at the target's look point
        Vector3 lookPoint = target.position + lookOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Mathf.Clamp01(Time.deltaTime / rotationSmoothTime));
    }
}