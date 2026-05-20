using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Settings")]
    [SerializeField] private float defaultCameraAngle = 20f;
    public float DefaultCameraAngle { get { return defaultCameraAngle; } set { defaultCameraAngle = value; } }

    [SerializeField] private bool enableCameraSmoothing = true;
    public bool EnableCameraSmoothing { get { return enableCameraSmoothing; } set { enableCameraSmoothing = value; } }

    [SerializeField] private float cameraPositionSmoothTime = 0.1f;
    public float CameraPositionSmoothTime { get { return cameraPositionSmoothTime; } set { cameraPositionSmoothTime = value; } }

    [SerializeField] private float cameraRotationSmoothSpeed = 5f;
    public float CameraRotationSmoothSpeed { get { return cameraRotationSmoothSpeed; } set { cameraRotationSmoothSpeed = value; } }

    // Camera Smoothing Variables
    private Vector3 cameraStartLocalPos;
    private Vector3 currentCameraVelocity;

    private void Start()
    {
        // If target is not assigned, try to find a DroneController
        if (target == null)
        {
            var drone = FindObjectOfType<DroneController>();
            if (drone != null) target = drone.transform;
        }

        if (target != null)
        {
            // Calculate initial offset based on current relative position
            // Assuming the camera is placed where we want it relative to the drone in the scene
            // cameraStartLocalPos = transform.position - target.position;

            // However, the previous logic used localPosition of a child. 
            // If we are now separate, we need to establish the "ideal" local position.
            // Let's assume the current world distance is the offset we want to maintain relative to target's rotation?
            // Actually, the previous logic was: targetPosition = transform.TransformPoint(cameraStartLocalPos);
            // This implies cameraStartLocalPos was a local offset.
            // So we should calculate that local offset relative to the target's initial transform.

            cameraStartLocalPos = target.InverseTransformPoint(transform.position);
        }

        transform.rotation = Quaternion.Euler(defaultCameraAngle, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (enableCameraSmoothing)
        {
            // Smooth Position
            Vector3 targetPosition = target.TransformPoint(cameraStartLocalPos);
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentCameraVelocity, cameraPositionSmoothTime);

            // Smooth Rotation
            Quaternion targetRotation = target.rotation * Quaternion.Euler(defaultCameraAngle, 0f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, cameraRotationSmoothSpeed * Time.deltaTime);
        }
        else
        {
            // Hard lock if smoothing disabled
            transform.position = target.TransformPoint(cameraStartLocalPos);
            transform.rotation = target.rotation * Quaternion.Euler(defaultCameraAngle, 0f, 0f);
        }
    }
}
