using UnityEngine;

/// <summary>
/// Handles all physics calculations for the drone including thrust, stabilization, and rotation.
/// Supports Angle, Acro, and Horizon flight modes with PID-based stabilization.
/// </summary>
public class DronePhysics : MonoBehaviour
{
    #region Enums

    public enum FlightMode
    {
        Angle,   // Self-leveling, stick = target angle
        Acro,    // Rate-based, stick = rotation rate
        Horizon  // Hybrid: self-levels at center, acro at extremes
    }

    #endregion

    #region Configuration

    [Header("Thrust Settings")]
    [Tooltip("Multiplier for thrust above hover point. 2.0 = can generate 2x hover thrust at full throttle.")]
    [SerializeField] private float maxThrustScale = 2.0f;

    [Header("Angle Mode Settings")]
    [Tooltip("Maximum tilt angle in degrees for Angle/Horizon modes.")]
    [SerializeField] private float maxTiltAngle = 45f;
    [Tooltip("Yaw rotation rate in degrees per second.")]
    [SerializeField] private float yawRate = 180f;

    [Header("Acro Mode Settings")]
    [Tooltip("Rotation rate in degrees per second for full stick deflection.")]
    [SerializeField] private float acroRate = 360f;

    [Header("PID Tuning (Angle Mode)")]
    [SerializeField] private float anglePID_P = 5.0f;
    [SerializeField] private float anglePID_I = 0.1f;
    [SerializeField] private float anglePID_D = 0.5f;

    [Header("Horizon Mode")]
    [Tooltip("Stick deadzone where self-leveling is active (0-1).")]
    [SerializeField] private float horizonTransitionPoint = 0.5f;

    [Header("Drag Settings")]
    [SerializeField] private float baseDrag = 0.5f;
    [SerializeField] private float angleAngularDrag = 2.0f;
    [SerializeField] private float acroAngularDrag = 0.5f;

    #endregion

    #region State

    private Rigidbody rb;
    private FlightMode currentMode = FlightMode.Angle;

    // PID state for pitch and roll
    private float pitchIntegral = 0f;
    private float rollIntegral = 0f;
    private float lastPitchError = 0f;
    private float lastRollError = 0f;

    // Configured mass (can be overridden by DroneConfig)
    private float configuredMass = 1.0f;

    // Calculated hover thrust
    private float hoverThrust;
    private float thrustMultiplier;

    #endregion

    #region Properties

    public FlightMode CurrentMode
    {
        get => currentMode;
        set
        {
            if (currentMode != value)
            {
                currentMode = value;
                ResetPIDState();
                UpdateDragSettings();
            }
        }
    }

    public string ModeName => currentMode.ToString();
    public float MaxTiltAngle => maxTiltAngle;
    public float YawRate => yawRate;
    public float AcroRate => acroRate;

    #endregion

    #region Initialization

    public void Initialize(Rigidbody rigidbody)
    {
        rb = rigidbody;
        ConfigureRigidbody();
        CalculateHoverThrust();
        UpdateDragSettings();
    }

    private void ConfigureRigidbody()
    {
        rb.mass = configuredMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void CalculateHoverThrust()
    {
        // Hover thrust = mass * gravity magnitude
        hoverThrust = rb.mass * Physics.gravity.magnitude;
        // Scale so 50% throttle = hover
        thrustMultiplier = hoverThrust / 0.5f;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Apply thrust force based on throttle input.
    /// </summary>
    /// <param name="throttle">Throttle value 0-1 where 0.5 is hover.</param>
    public void ApplyThrust(float throttle)
    {
        float thrust = throttle * thrustMultiplier * maxThrustScale;
        Vector3 thrustForce = transform.up * thrust;
        rb.AddForce(thrustForce, ForceMode.Force);
    }

    /// <summary>
    /// Apply rotation based on cyclic input (pitch/roll) and yaw input.
    /// </summary>
    /// <param name="cyclic">X = roll (-1 to 1), Y = pitch (-1 to 1)</param>
    /// <param name="yaw">Yaw input -1 to 1</param>
    public void ApplyRotation(Vector2 cyclic, float yaw)
    {
        switch (currentMode)
        {
            case FlightMode.Angle:
                ApplyAngleModeRotation(cyclic, yaw);
                break;
            case FlightMode.Acro:
                ApplyAcroModeRotation(cyclic, yaw);
                break;
            case FlightMode.Horizon:
                ApplyHorizonModeRotation(cyclic, yaw);
                break;
        }
    }

    /// <summary>
    /// Cycle to the next flight mode.
    /// </summary>
    public void CycleMode()
    {
        int nextMode = ((int)currentMode + 1) % 3;
        CurrentMode = (FlightMode)nextMode;
    }

    /// <summary>
    /// Set the drone mass (call before Initialize).
    /// </summary>
    public void SetMass(float mass)
    {
        configuredMass = mass;
    }

    /// <summary>
    /// Apply a DroneConfig ScriptableObject to override all physics parameters.
    /// Call before Initialize().
    /// </summary>
    public void ApplyConfig(DroneConfig config)
    {
        if (config == null) return;

        maxThrustScale = config.maxThrustScale;
        maxTiltAngle = config.maxTiltAngle;
        yawRate = config.yawRate;
        acroRate = config.acroRate;
        anglePID_P = config.pidP;
        anglePID_I = config.pidI;
        anglePID_D = config.pidD;
        baseDrag = config.baseDrag;
        angleAngularDrag = config.angleAngularDrag;
        acroAngularDrag = config.acroAngularDrag;
        horizonTransitionPoint = config.horizonTransitionPoint;
        currentMode = config.defaultFlightMode;
    }

    /// <summary>
    /// Reset PID integrators (call on drone reset or mode change).
    /// </summary>
    public void ResetPIDState()
    {
        pitchIntegral = 0f;
        rollIntegral = 0f;
        lastPitchError = 0f;
        lastRollError = 0f;
    }

    #endregion

    #region Angle Mode

    private void ApplyAngleModeRotation(Vector2 cyclic, float yaw)
    {
        float dt = Time.fixedDeltaTime;

        // Yaw is rate-based even in angle mode
        ApplyYawRate(yaw);

        // Calculate target angles from stick input
        float targetPitch = cyclic.y * maxTiltAngle;
        float targetRoll = -cyclic.x * maxTiltAngle; // Invert for correct feel

        // Get current angles
        Vector3 currentEuler = transform.eulerAngles;
        float currentPitch = NormalizeAngle(currentEuler.x);
        float currentRoll = NormalizeAngle(currentEuler.z);

        // Calculate errors
        float pitchError = targetPitch - currentPitch;
        float rollError = targetRoll - currentRoll;

        // PID calculations for pitch
        pitchIntegral += pitchError * dt;
        pitchIntegral = Mathf.Clamp(pitchIntegral, -10f, 10f); // Anti-windup
        float pitchDerivative = (pitchError - lastPitchError) / dt;
        float pitchCorrection = anglePID_P * pitchError + anglePID_I * pitchIntegral + anglePID_D * pitchDerivative;
        lastPitchError = pitchError;

        // PID calculations for roll
        rollIntegral += rollError * dt;
        rollIntegral = Mathf.Clamp(rollIntegral, -10f, 10f); // Anti-windup
        float rollDerivative = (rollError - lastRollError) / dt;
        float rollCorrection = anglePID_P * rollError + anglePID_I * rollIntegral + anglePID_D * rollDerivative;
        lastRollError = rollError;

        // Apply torque
        Vector3 torque = new Vector3(pitchCorrection, 0f, rollCorrection);
        rb.AddRelativeTorque(torque, ForceMode.Acceleration);
    }

    #endregion

    #region Acro Mode

    private void ApplyAcroModeRotation(Vector2 cyclic, float yaw)
    {
        // Apply yaw
        ApplyYawRate(yaw);

        // Stick position = rotation rate
        float pitchRate = cyclic.y * acroRate;
        float rollRate = -cyclic.x * acroRate; // Invert for correct feel

        Vector3 torque = new Vector3(pitchRate, 0f, rollRate);
        rb.AddRelativeTorque(torque * Time.fixedDeltaTime, ForceMode.VelocityChange);
    }

    #endregion

    #region Horizon Mode

    private void ApplyHorizonModeRotation(Vector2 cyclic, float yaw)
    {
        // Apply yaw
        ApplyYawRate(yaw);

        float stickMagnitude = cyclic.magnitude;

        if (stickMagnitude < horizonTransitionPoint)
        {
            // Near center: blend towards angle mode (self-leveling)
            float blendFactor = stickMagnitude / horizonTransitionPoint;
            ApplyHorizonBlendedRotation(cyclic, blendFactor);
        }
        else
        {
            // At extremes: full acro behavior
            float pitchRate = cyclic.y * acroRate;
            float rollRate = -cyclic.x * acroRate;

            Vector3 torque = new Vector3(pitchRate, 0f, rollRate);
            rb.AddRelativeTorque(torque * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }
    }

    private void ApplyHorizonBlendedRotation(Vector2 cyclic, float blendFactor)
    {
        float dt = Time.fixedDeltaTime;

        // Calculate target angles (scaled by blend factor for smooth transition)
        float targetPitch = cyclic.y * maxTiltAngle;
        float targetRoll = -cyclic.x * maxTiltAngle;

        // Get current angles
        Vector3 currentEuler = transform.eulerAngles;
        float currentPitch = NormalizeAngle(currentEuler.x);
        float currentRoll = NormalizeAngle(currentEuler.z);

        // Self-leveling component (stronger at center)
        float levelingStrength = 1f - blendFactor;
        float pitchError = (targetPitch - currentPitch) * levelingStrength;
        float rollError = (targetRoll - currentRoll) * levelingStrength;

        // Simplified P controller for horizon mode
        float pitchCorrection = anglePID_P * pitchError;
        float rollCorrection = anglePID_P * rollError;

        // Rate component (stronger at extremes)
        float pitchRate = cyclic.y * acroRate * blendFactor;
        float rollRate = -cyclic.x * acroRate * blendFactor;

        // Combine both components
        Vector3 levelingTorque = new Vector3(pitchCorrection, 0f, rollCorrection);
        Vector3 rateTorque = new Vector3(pitchRate, 0f, rollRate) * dt;

        rb.AddRelativeTorque(levelingTorque, ForceMode.Acceleration);
        rb.AddRelativeTorque(rateTorque, ForceMode.VelocityChange);
    }

    #endregion

    #region Common

    private void ApplyYawRate(float yaw)
    {
        float yawTorque = yaw * yawRate * Time.fixedDeltaTime;
        rb.AddRelativeTorque(Vector3.up * yawTorque, ForceMode.VelocityChange);
    }

    private void UpdateDragSettings()
    {
        if (rb == null) return;

        rb.drag = baseDrag;
        rb.angularDrag = currentMode == FlightMode.Acro ? acroAngularDrag : angleAngularDrag;
    }

    /// <summary>
    /// Normalize angle to -180 to 180 range.
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    #endregion
}
