using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // follow the tank
    public Transform target;

    // Local-space offset from the target (e.g. (0, 5, -8) places camera behind and above
    public Vector3 offset = new Vector3(0f, 5f, -8f);

    // Smooth time for position smoothing
    public float smoothTime = 0.1f;

    // Rotation smoothing speed when looking at target
    public float rotationSpeed = 20f;

    // vertical offset for the look-at point (camera angle)
    public float lookAtHeight = 2.5f;

    private Vector3 velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        // Desired world position using the target's local-space offset
        Vector3 desiredPosition = target.TransformPoint(offset);

        // Smoothly move the camera to the desired position
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);

        // Smoothly rotate camera to look at the target's look point
        Vector3 lookPoint = target.position + Vector3.up * lookAtHeight;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
    }
}