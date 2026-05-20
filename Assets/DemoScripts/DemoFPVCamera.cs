using UnityEngine;

public class DemoFPVCamera : MonoBehaviour
{
    public enum CameraMode
    {
        FPV,
        Chase,
        Cinematic
    }

    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Mode")]
    [SerializeField] private CameraMode mode = CameraMode.FPV;
    [SerializeField] private KeyCode toggleKey = KeyCode.C;

    [Header("FPV Settings")]
    [SerializeField] private Vector3 fpvOffset = new Vector3(0f, 0.05f, 0.15f);
    [SerializeField] private float fpvTiltAngle = 15f;

    [Header("Chase Settings")]
    [SerializeField] private float chaseDistance = 4f;
    [SerializeField] private float chaseHeight = 1.5f;
    [SerializeField] private float chaseSmoothTime = 0.15f;
    [SerializeField] private float chaseRotationSpeed = 8f;

    [Header("Cinematic Settings")]
    [SerializeField] private float cinematicDistance = 12f;
    [SerializeField] private float cinematicHeight = 5f;
    [SerializeField] private float cinematicOrbitSpeed = 15f;
    [SerializeField] private float cinematicSmoothTime = 0.3f;

    private Vector3 currentVelocity;
    private float orbitAngle;

    private void LateUpdate()
    {
        if (target == null)
        {
            var autopilot = FindObjectOfType<DemoAutopilot>();
            if (autopilot != null) target = autopilot.transform;
        }
        if (target == null) return;

        if (Input.GetKeyDown(toggleKey))
        {
            mode = (CameraMode)(((int)mode + 1) % 3);
        }

        switch (mode)
        {
            case CameraMode.FPV:
                UpdateFPV();
                break;
            case CameraMode.Chase:
                UpdateChase();
                break;
            case CameraMode.Cinematic:
                UpdateCinematic();
                break;
        }
    }

    private void UpdateFPV()
    {
        transform.position = target.TransformPoint(fpvOffset);
        transform.rotation = target.rotation * Quaternion.Euler(fpvTiltAngle, 0f, 0f);
    }

    private void UpdateChase()
    {
        Vector3 flatForward = target.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 desiredPos = target.position - flatForward * chaseDistance + Vector3.up * chaseHeight;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentVelocity, chaseSmoothTime);

        Quaternion targetRot = Quaternion.LookRotation(target.position - transform.position + Vector3.up * 0.3f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, chaseRotationSpeed * Time.deltaTime);
    }

    private void UpdateCinematic()
    {
        orbitAngle += cinematicOrbitSpeed * Time.deltaTime;
        float rad = orbitAngle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(rad) * cinematicDistance,
            cinematicHeight,
            Mathf.Cos(rad) * cinematicDistance
        );

        Vector3 desiredPos = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentVelocity, cinematicSmoothTime);

        transform.LookAt(target.position + Vector3.up * 0.5f);
    }
}
