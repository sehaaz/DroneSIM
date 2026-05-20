using UnityEngine;

[RequireComponent(typeof(DemoDronePhysics))]
public class DemoAutopilot : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private DemoWaypointPath waypointPath;

    [Header("Flight Profile")]
    [SerializeField] private float cruiseSpeed = 10f;
    [SerializeField] private float maxSpeed = 18f;
    [SerializeField] private float waypointReachDistance = 3.0f;
    [SerializeField] private float lookAheadDistance = 8f;
    [SerializeField] private float slowDownAngleThreshold = 40f;

    [Header("Velocity Control")]
    [SerializeField] private float velP = 3.0f;
    [SerializeField] private float maxHorizontalAccel = 12f;

    [Header("Attitude Control")]
    [SerializeField] private float attP = 12.0f;
    [SerializeField] private float attD = 6.0f;

    [Header("Altitude Control (output = m/s²)")]
    [SerializeField] private float altP = 3.0f;
    [SerializeField] private float altI = 0.5f;
    [SerializeField] private float altD = 2.0f;
    [SerializeField] private float maxVerticalAccel = 8f;

    [Header("Constraints")]
    [SerializeField] private float maxTiltAngle = 50f;

    [Header("Startup")]
    [SerializeField] private float takeoffAltitude = 5f;
    [SerializeField] private float startDelay = 1.0f;

    [Header("Events")]
    [SerializeField] private bool stopAtEnd = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private DemoDronePhysics drone;
    private Rigidbody rb;

    private int currentWaypointIndex;
    private bool isFlying;
    private bool isFinished;
    private float startTimer;

    private float altitudeIntegral;
    private float lastAltitudeError;
    private bool altPidInitialized;
    private int diagFrameCount;

    private Vector3 debugFlyTarget;
    private Vector3 debugDesiredThrustDir;

    public int CurrentWaypointIndex => currentWaypointIndex;
    public bool IsFlying => isFlying;
    public bool IsFinished => isFinished;

    private void Start()
    {
        drone = GetComponent<DemoDronePhysics>();
        drone.Init();
        rb = drone.Rb;

        // Auto-find waypointPath if not assigned
        if (waypointPath == null)
        {
            waypointPath = FindObjectOfType<DemoWaypointPath>();
            Debug.LogWarning($"[DemoAutopilot] waypointPath was null, auto-found: {(waypointPath != null ? waypointPath.name : "NONE")}");
        }

        if (waypointPath != null)
        {
            waypointPath.CollectWaypoints();
            Debug.Log($"[DemoAutopilot] Path '{waypointPath.name}' has {waypointPath.Count} waypoints. Children: {waypointPath.transform.childCount}");
            for (int i = 0; i < waypointPath.Count; i++)
            {
                Transform wp = waypointPath.GetWaypoint(i);
                if (wp != null)
                    Debug.Log($"  WP[{i}] = {wp.name} at {wp.position}");
            }
        }
        else
        {
            Debug.LogError("[DemoAutopilot] No DemoWaypointPath found in scene!");
        }

        startTimer = startDelay;
        currentWaypointIndex = 0;
    }

    private void FixedUpdate()
    {
        if (isFinished)
        {
            StabilizedHover(takeoffAltitude);
            return;
        }

        startTimer -= Time.fixedDeltaTime;
        if (startTimer > 0f)
        {
            StabilizedHover(takeoffAltitude);
            return;
        }

        if (!isFlying)
        {
            isFlying = true;
            ResetAltPid();
            Debug.Log($"[DemoAutopilot] Navigation started! waypointPath={(waypointPath != null ? waypointPath.name : "NULL")}, count={(waypointPath != null ? waypointPath.Count : 0)}");
        }

        if (waypointPath == null || waypointPath.Count < 2)
        {
            Debug.LogWarning($"[DemoAutopilot] Cannot navigate! waypointPath={(waypointPath != null ? "exists" : "NULL")}, count={(waypointPath != null ? waypointPath.Count : 0)}");
            StabilizedHover(takeoffAltitude);
            return;
        }

        NavigateToWaypoint();
    }

    private void NavigateToWaypoint()
    {
        Vector3 targetPos = waypointPath.GetPosition(currentWaypointIndex);
        Vector3 toTarget = targetPos - transform.position;
        float distToTarget = toTarget.magnitude;

        if (distToTarget < waypointReachDistance)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypointPath.Count)
            {
                if (waypointPath.Loop)
                    currentWaypointIndex = 0;
                else
                {
                    isFinished = true;
                    return;
                }
            }
            targetPos = waypointPath.GetPosition(currentWaypointIndex);
            toTarget = targetPos - transform.position;
            distToTarget = toTarget.magnitude;
        }

        // Look-ahead: blend current target with next waypoint for smoother turns
        Vector3 flyTarget = targetPos;
        int nextIdx = GetNextIndex();
        if (nextIdx >= 0)
        {
            Vector3 nextPos = waypointPath.GetPosition(nextIdx);
            if (distToTarget < lookAheadDistance)
            {
                float blend = 1f - (distToTarget / lookAheadDistance);
                flyTarget = Vector3.Lerp(targetPos, nextPos, blend * 0.5f);
            }
        }

        debugFlyTarget = flyTarget;

        // Speed control
        float desiredSpeed = cruiseSpeed;
        if (nextIdx >= 0)
        {
            Vector3 nextPos = waypointPath.GetPosition(nextIdx);
            Vector3 dirCurrent = (targetPos - transform.position).normalized;
            Vector3 dirNext = (nextPos - targetPos).normalized;
            float turnAngle = Vector3.Angle(dirCurrent, dirNext);
            if (turnAngle > slowDownAngleThreshold)
            {
                float slowFactor = Mathf.InverseLerp(slowDownAngleThreshold, 150f, turnAngle);
                desiredSpeed = Mathf.Lerp(cruiseSpeed, cruiseSpeed * 0.4f, slowFactor);
            }
        }
        desiredSpeed = Mathf.Min(desiredSpeed, maxSpeed);

        // --- Velocity controller -> desired horizontal acceleration ---
        Vector3 toFlyTarget = flyTarget - transform.position;
        Vector3 desiredVelocity = toFlyTarget.normalized * desiredSpeed;
        Vector3 velError = desiredVelocity - rb.velocity;
        Vector3 desiredAccel = new Vector3(velError.x, 0f, velError.z) * velP;
        desiredAccel = Vector3.ClampMagnitude(desiredAccel, maxHorizontalAccel);

        // --- Convert desired horizontal accel to desired thrust direction ---
        float g = Physics.gravity.magnitude;
        Vector3 desiredThrustDir = (Vector3.up * g + desiredAccel).normalized;

        float tiltAngle = Vector3.Angle(Vector3.up, desiredThrustDir);
        if (tiltAngle > maxTiltAngle)
            desiredThrustDir = Vector3.Slerp(Vector3.up, desiredThrustDir, maxTiltAngle / tiltAngle);

        debugDesiredThrustDir = desiredThrustDir;

        // --- Desired yaw: face toward fly target ---
        Vector3 flatForward = new Vector3(toFlyTarget.x, 0f, toFlyTarget.z);
        if (flatForward.sqrMagnitude < 0.01f)
            flatForward = transform.forward;
        flatForward.Normalize();

        // --- Build desired rotation: up=desiredThrustDir (primary), forward≈flatForward (secondary) ---
        Vector3 right = Vector3.Cross(desiredThrustDir, flatForward).normalized;
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(desiredThrustDir, Vector3.forward).normalized;
        Vector3 actualForward = Vector3.Cross(right, desiredThrustDir).normalized;
        Quaternion desiredRotation = Quaternion.LookRotation(actualForward, desiredThrustDir);
        ApplyAttitudeControl(desiredRotation);

        // --- Thrust: physically correct model ---
        float targetAlt = flyTarget.y;
        float vertAccelCmd = ComputeAltitudePID(targetAlt);
        ApplyThrust(vertAccelCmd);

        // Diagnostic: log first 3 frames of navigation
        diagFrameCount++;
        if (diagFrameCount <= 3)
        {
            Debug.Log($"[NAV DIAG #{diagFrameCount}] " +
                $"pos={transform.position:F1} target={targetPos:F1} dist={distToTarget:F1}\n" +
                $"  desiredAccel={desiredAccel:F2} mag={desiredAccel.magnitude:F2}\n" +
                $"  desiredThrustDir={desiredThrustDir:F3} tiltAngle={tiltAngle:F1}°\n" +
                $"  vel={rb.velocity:F2} angVel={rb.angularVelocity:F2}\n" +
                $"  vertAccelCmd={vertAccelCmd:F2} tiltCos={Vector3.Dot(transform.up, Vector3.up):F3}\n" +
                $"  rb.isKinematic={rb.isKinematic} rb.mass={rb.mass} rb.drag={rb.drag} rb.angularDrag={rb.angularDrag}");
        }
    }

    private float ComputeAltitudePID(float targetAltitude)
    {
        float altError = targetAltitude - transform.position.y;

        if (!altPidInitialized)
        {
            lastAltitudeError = altError;
            altPidInitialized = true;
        }

        altitudeIntegral += altError * Time.fixedDeltaTime;
        altitudeIntegral = Mathf.Clamp(altitudeIntegral, -3f, 3f);

        float altDeriv = (altError - lastAltitudeError) / Time.fixedDeltaTime;
        lastAltitudeError = altError;

        float cmd = altP * altError + altI * altitudeIntegral + altD * altDeriv;
        return Mathf.Clamp(cmd, -maxVerticalAccel, maxVerticalAccel);
    }

    private void ApplyThrust(float verticalAccelCmd)
    {
        float g = Physics.gravity.magnitude;

        // Required upward force = mass * (g + verticalAccelCmd)
        // Thrust along transform.up, vertical component = thrust * cos(tilt)
        // So thrust * cos(tilt) = mass * (g + vertAccelCmd)
        // throttle = thrust / (hoverThrust * maxThrustMult)
        //          = mass * (g + vertAccelCmd) / (cos(tilt) * mass * g * maxThrustMult)
        //          = (g + vertAccelCmd) / (cos(tilt) * g * maxThrustMult)

        float tiltCos = Vector3.Dot(transform.up, Vector3.up);
        tiltCos = Mathf.Max(tiltCos, 0.25f);

        float throttle = (g + verticalAccelCmd) / (tiltCos * g * drone.MaxThrustMultiplier);
        throttle = Mathf.Clamp(throttle, 0.05f, 1.0f);

        drone.ApplyThrust(throttle);
    }

    private void ApplyAttitudeControl(Quaternion desiredRotation)
    {
        Quaternion rotErr = desiredRotation * Quaternion.Inverse(transform.rotation);
        rotErr.ToAngleAxis(out float errAngle, out Vector3 errAxis);

        if (errAngle > 180f) errAngle -= 360f;
        if (errAxis.sqrMagnitude < 0.001f) errAxis = Vector3.up;
        errAxis.Normalize();

        Vector3 torque = errAxis * (errAngle * Mathf.Deg2Rad) * attP - rb.angularVelocity * attD;
        rb.AddTorque(torque, ForceMode.Acceleration);
    }

    private void StabilizedHover(float targetAlt)
    {
        float vertAccelCmd = ComputeAltitudePID(targetAlt);
        ApplyThrust(vertAccelCmd);

        // Level the drone, keep current yaw
        Vector3 flatFwd = transform.forward;
        flatFwd.y = 0f;
        if (flatFwd.sqrMagnitude < 0.01f)
            flatFwd = Vector3.forward;
        flatFwd.Normalize();

        Quaternion desiredRot = Quaternion.LookRotation(flatFwd, Vector3.up);
        ApplyAttitudeControl(desiredRot);
    }

    private int GetNextIndex()
    {
        if (waypointPath.Loop)
            return (currentWaypointIndex + 1) % waypointPath.Count;
        if (currentWaypointIndex + 1 < waypointPath.Count)
            return currentWaypointIndex + 1;
        return -1;
    }

    private void ResetAltPid()
    {
        altitudeIntegral = 0f;
        lastAltitudeError = 0f;
        altPidInitialized = false;
    }

    public void ResetFlight()
    {
        currentWaypointIndex = 0;
        isFlying = false;
        isFinished = false;
        startTimer = startDelay;
        ResetAltPid();
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug || !Application.isPlaying) return;

        // Current target
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(debugFlyTarget, 1f);
        Gizmos.DrawLine(transform.position, debugFlyTarget);

        // Desired thrust direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, debugDesiredThrustDir * 3f);

        // Actual up
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.up * 2f);

        // Velocity
        if (rb != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, rb.velocity * 0.3f);
        }
    }
}
