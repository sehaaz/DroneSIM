using UnityEngine;

/// <summary>
/// Main drone controller that orchestrates input handling, physics, and state management.
/// Supports Keyboard, Joystick, and UDP input sources with Mode 2 control scheme.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DronePhysics))]
public class DroneController : MonoBehaviour
{
    #region Enums

    public enum InputSource
    {
        Keyboard,
        Joystick,
        UDP
    }

    #endregion

    #region Configuration

    [Header("Input Source")]
    [SerializeField] private InputSource inputSource = InputSource.Keyboard;
    [SerializeField] private UDPReceiver udpReceiver;

    [Header("Joystick Axes (Mode 2)")]
    [Tooltip("Left stick Y axis for throttle.")]
    [SerializeField] private int throttleAxis = 2;
    [Tooltip("Left stick X axis for yaw.")]
    [SerializeField] private int yawAxis = 1;
    [Tooltip("Right stick Y axis for pitch.")]
    [SerializeField] private int pitchAxis = 5;
    [Tooltip("Right stick X axis for roll.")]
    [SerializeField] private int rollAxis = 4;
    [SerializeField] private bool invertPitch = false;
    [SerializeField] private bool invertRoll = false;
    [SerializeField] private bool invertThrottle = false;

    [Header("Input Settings")]
    [Tooltip("Deadzone applied to all inputs.")]
    [SerializeField] private float deadzone = 0.1f;
    [Tooltip("Speed of throttle change per second (keyboard/joystick sticky throttle).")]
    [SerializeField] private float throttleSpeed = 1.5f;

    [Header("God Mode")]
    [SerializeField] private bool godMode = false;
    [SerializeField] private float godModeSpeed = 10f;
    [SerializeField] private float godModeRotationSpeed = 60f;
    [SerializeField] private KeyCode godModeKey = KeyCode.G;

    [Header("Control Keys")]
    [SerializeField] private KeyCode modeSwitchKey = KeyCode.X;
    [SerializeField] private KeyCode resetKey = KeyCode.R;

    [Header("Sensitivity")]
    public float sensitivityMultiplier = 1f;

    [Header("Start Position (Reset Point)")]
    [Tooltip("Enable to use custom start position set below. If disabled, uses the drone's initial position at game start.")]
    [SerializeField] private bool useCustomStartPosition = false;
    [Tooltip("Custom start position for reset. Only used if 'Use Custom Start Position' is enabled.")]
    [SerializeField] private Vector3 customStartPosition = Vector3.zero;
    [Tooltip("Custom start rotation (Euler angles) for reset. Only used if 'Use Custom Start Position' is enabled.")]
    [SerializeField] private Vector3 customStartRotation = Vector3.zero;

    #endregion

    #region State

    // Raw inputs (-1 to 1)
    private float throttleInput;
    private float yawInput;
    private Vector2 cyclicInput; // x = roll, y = pitch

    // Processed values
    private float currentThrottle; // 0 to 1 (sticky)

    // Components
    private Rigidbody rb;
    private DronePhysics physics;

    // Reset state (actual values used for reset)
    private Vector3 startPosition;
    private Quaternion startRotation;

    // God mode movement direction (for gate direction checks since velocity is zero)
    private Vector3 godModeMovementDirection = Vector3.zero;

    #endregion

    #region Public Properties (for UI compatibility)

    /// <summary>Current throttle value 0-1.</summary>
    public float ThrottleValue => currentThrottle;

    /// <summary>Current yaw input -1 to 1.</summary>
    public float YawInput => yawInput;

    /// <summary>Current cyclic input. X = roll, Y = pitch (-1 to 1).</summary>
    public Vector2 CyclicInput => cyclicInput;

    /// <summary>Current flight mode.</summary>
    public DronePhysics.FlightMode CurrentMode => physics != null ? physics.CurrentMode : DronePhysics.FlightMode.Angle;

    /// <summary>Current flight mode name as string.</summary>
    public string CurrentModeName => physics != null ? physics.ModeName : "Angle";

    /// <summary>Current speed in m/s.</summary>
    public float CurrentSpeed => rb != null ? rb.velocity.magnitude : 0f;

    /// <summary>Current vertical speed in m/s.</summary>
    public float VerticalSpeed => rb != null ? rb.velocity.y : 0f;

    /// <summary>Is god mode enabled.</summary>
    public bool IsGodMode => godMode;

    /// <summary>Current input source.</summary>
    public InputSource CurrentInputSource => inputSource;

    /// <summary>
    /// Movement direction for gate checks. Uses velocity normally, or calculated direction in god mode.
    /// </summary>
    public Vector3 MovementDirection => godMode ? godModeMovementDirection : (rb != null ? rb.velocity.normalized : Vector3.zero);

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        physics = GetComponent<DronePhysics>();

        // Set start position based on configuration
        if (useCustomStartPosition)
        {
            startPosition = customStartPosition;
            startRotation = Quaternion.Euler(customStartRotation);
        }
        else
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        // Apply drone selection from GameSession (if coming from selection scene)
        if (GameSession.HasSelection && GameSession.SelectedDroneConfig != null)
        {
            physics.SetMass(GameSession.SelectedDroneConfig.mass);
            physics.ApplyConfig(GameSession.SelectedDroneConfig);
            inputSource = GameSession.SelectedInputSource;
        }

        // Initialize physics component
        physics.Initialize(rb);

        // Load saved settings
        sensitivityMultiplier = PlayerPrefs.GetFloat("ControlSensitivity", 1f);
    }

    private void OnEnable()
    {
        if (udpReceiver != null)
        {
            udpReceiver.OnResetCommand += ResetDrone;
            udpReceiver.OnModeCommand += HandleUDPModeCommand;
        }
    }

    private void OnDisable()
    {
        if (udpReceiver != null)
        {
            udpReceiver.OnResetCommand -= ResetDrone;
            udpReceiver.OnModeCommand -= HandleUDPModeCommand;
        }
    }

    private void HandleUDPModeCommand(string modeName)
    {
        // Parse mode name and set flight mode
        switch (modeName.ToUpper())
        {
            case "ANGLE":
                SetFlightMode(DronePhysics.FlightMode.Angle);
                break;
            case "ACRO":
                SetFlightMode(DronePhysics.FlightMode.Acro);
                break;
            case "HORIZON":
                SetFlightMode(DronePhysics.FlightMode.Horizon);
                break;
            default:
                Debug.LogWarning($"Unknown flight mode: {modeName}");
                return;
        }

        Debug.Log($"Flight mode set to {modeName} via UDP");
        var hud = FindObjectOfType<RaceDistanceHUD>();
        if (hud != null) hud.ShowMessage($"{physics.ModeName} Mode", 1f);
    }

    private void Update()
    {
        ReadInput();
        HandleModeSwitch();
        HandleReset();
        HandleGodModeToggle();
    }

    private void FixedUpdate()
    {
        if (godMode)
        {
            ApplyGodModeMovement();
        }
        else
        {
            ApplyFlightPhysics();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (godMode) return;

        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("World Object"))
        {
            var hud = FindObjectOfType<RaceDistanceHUD>();
            if (hud != null) hud.ShowMessage("CRASHED!", 2f);
            ResetDrone();
        }
    }

    #endregion

    #region Input Reading

    private void ReadInput()
    {
        switch (inputSource)
        {
            case InputSource.Keyboard:
                ReadKeyboardInput();
                break;
            case InputSource.Joystick:
                ReadJoystickInput();
                break;
            case InputSource.UDP:
                ReadUDPInput();
                break;
        }
    }

    private void ReadKeyboardInput()
    {
        // Left Stick Equivalent: W/S = Throttle rate, A/D = Yaw
        float rawThrottle = 0f;
        if (Input.GetKey(KeyCode.W)) rawThrottle = 1f;
        else if (Input.GetKey(KeyCode.S)) rawThrottle = -1f;

        throttleInput = ApplyDeadzone(rawThrottle);
        currentThrottle = Mathf.Clamp01(currentThrottle + throttleInput * throttleSpeed * Time.deltaTime);

        float rawYaw = 0f;
        if (Input.GetKey(KeyCode.D)) rawYaw = 1f;
        else if (Input.GetKey(KeyCode.A)) rawYaw = -1f;

        yawInput = ApplyDeadzone(rawYaw) * sensitivityMultiplier;

        // Right Stick Equivalent: Arrow keys = Pitch/Roll
        float rawPitch = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) rawPitch = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) rawPitch = -1f;

        float rawRoll = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) rawRoll = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow)) rawRoll = -1f;

        cyclicInput = new Vector2(
            ApplyDeadzone(rawRoll) * sensitivityMultiplier,
            ApplyDeadzone(rawPitch) * sensitivityMultiplier
        );
    }

    private void ReadJoystickInput()
    {
        // Left Stick: Throttle (Y) and Yaw (X) - Mode 2
        string throttleAxisName = "Joystick Axis " + throttleAxis;
        string yawAxisName = "Joystick Axis " + yawAxis;

        float rawThrottle = GetJoystickAxis(throttleAxis);
        if (invertThrottle) rawThrottle = -rawThrottle;
        throttleInput = ApplyDeadzone(rawThrottle);
        currentThrottle = Mathf.Clamp01(currentThrottle + throttleInput * throttleSpeed * Time.deltaTime);

        float rawYaw = GetJoystickAxis(yawAxis);
        yawInput = ApplyDeadzone(rawYaw) * sensitivityMultiplier;

        // Right Stick: Pitch (Y) and Roll (X)
        float rawPitch = GetJoystickAxis(pitchAxis);
        if (invertPitch) rawPitch = -rawPitch;

        float rawRoll = GetJoystickAxis(rollAxis);
        if (invertRoll) rawRoll = -rawRoll;

        cyclicInput = new Vector2(
            ApplyDeadzone(rawRoll) * sensitivityMultiplier,
            ApplyDeadzone(rawPitch) * sensitivityMultiplier
        );
    }

    private void ReadUDPInput()
    {
        if (udpReceiver == null) return;

        // Get thread-safe snapshot of values
        var snapshot = udpReceiver.GetInputSnapshot();

        // Store raw throttle input (needed for god mode)
        throttleInput = ApplyDeadzone(snapshot.throttle);

        // Throttle: UDP sends -1 to 1, map to 0 to 1 for normal flight
        currentThrottle = (snapshot.throttle + 1f) * 0.5f;

        // Yaw: -1 to 1
        yawInput = ApplyDeadzone(snapshot.yaw) * sensitivityMultiplier;

        // Cyclic: Roll/Pitch -1 to 1
        cyclicInput = new Vector2(
            ApplyDeadzone(snapshot.roll) * sensitivityMultiplier,
            ApplyDeadzone(snapshot.pitch) * sensitivityMultiplier
        );
    }

    private float GetJoystickAxis(int axisNumber)
    {
        // Unity joystick axes are named "Joystick Axis 1", "Joystick Axis 2", etc.
        // But we need to use the generic axis names that should be set up in Input Manager
        try
        {
            // Try the specific joystick axis name first
            string axisName = $"Joy Axis {axisNumber}";
            return Input.GetAxis(axisName);
        }
        catch
        {
            // Fallback: try reading raw joystick input
            // Axis numbers in Unity Input.GetAxis are 1-based for joysticks
            string fallbackName = axisNumber switch
            {
                1 => "Horizontal",        // Left stick X
                2 => "Vertical",          // Left stick Y
                4 => "Mouse X",           // Right stick X (fallback)
                5 => "Mouse Y",           // Right stick Y (fallback)
                _ => "Horizontal"
            };
            return Input.GetAxis(fallbackName);
        }
    }

    private float ApplyDeadzone(float value)
    {
        if (Mathf.Abs(value) < deadzone) return 0f;

        // Rescale to maintain full range after deadzone
        float sign = Mathf.Sign(value);
        float magnitude = Mathf.Abs(value);
        return sign * (magnitude - deadzone) / (1f - deadzone);
    }

    #endregion

    #region Mode and Reset Handling

    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(modeSwitchKey) || Input.GetKeyDown(KeyCode.JoystickButton2))
        {
            physics.CycleMode();
            Debug.Log($"Switched to {physics.ModeName} Mode");

            // Show feedback on HUD if available
            var hud = FindObjectOfType<RaceDistanceHUD>();
            if (hud != null) hud.ShowMessage($"{physics.ModeName} Mode", 1f);
        }
    }

    private void HandleReset()
    {
        if (Input.GetKeyDown(resetKey) || Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            Debug.Log($"[DroneController] Reset key '{resetKey}' or Joystick Button 1 pressed.");
            ResetDrone();
        }
    }

    private void HandleGodModeToggle()
    {
        if (Input.GetKeyDown(godModeKey) || Input.GetKeyDown(KeyCode.JoystickButton6))
        {
            godMode = !godMode;
            Debug.Log($"God Mode: {godMode}");

            if (godMode)
            {
                EnterGodMode();
            }
            else
            {
                ExitGodMode();
            }

            var hud = FindObjectOfType<RaceDistanceHUD>();
            if (hud != null) hud.ShowMessage(godMode ? "God Mode ON" : "God Mode OFF", 1f);
        }
    }

    private void EnterGodMode()
    {
        // Zero velocities BEFORE setting kinematic (can't set on kinematic bodies)
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.useGravity = false;
        // Keep detectCollisions = true so triggers (gates) still work
        rb.detectCollisions = true;
        rb.isKinematic = true;

        // Level the drone
        Vector3 currentEuler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);
    }

    private void ExitGodMode()
    {
        rb.useGravity = true;
        rb.detectCollisions = true;
        rb.isKinematic = false;
        rb.WakeUp();
    }

    /// <summary>
    /// Reset the drone to its starting position.
    /// </summary>
    public void ResetDrone()
    {
        Debug.Log($"[DroneController] Resetting drone to start position: {startPosition}, GodMode: {godMode}");

        if (rb == null) rb = GetComponent<Rigidbody>();

        if (godMode)
        {
            // God mode reset: simple position/rotation reset (already kinematic)
            // Note: Don't set velocity/angularVelocity on kinematic bodies
            rb.MovePosition(startPosition);
            rb.MoveRotation(startRotation);
            transform.position = startPosition;
            transform.rotation = startRotation;
            godModeMovementDirection = Vector3.zero;
        }
        else
        {
            // Normal mode reset
            if (rb != null)
            {
                // Temporarily disable physics to ensure clean reset
                bool wasKinematic = rb.isKinematic;
                rb.isKinematic = true;

                // Zero out velocities
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Set position via Rigidbody (more reliable than transform)
                rb.position = startPosition;
                rb.rotation = startRotation;

                // Also set transform to ensure immediate visual update
                transform.position = startPosition;
                transform.rotation = startRotation;

                // Restore physics state
                rb.isKinematic = wasKinematic;

                // Wake up the rigidbody to ensure it processes the new state
                if (!rb.isKinematic)
                {
                    rb.WakeUp();
                }
            }
            else
            {
                // Fallback if no rigidbody (shouldn't happen due to RequireComponent)
                transform.position = startPosition;
                transform.rotation = startRotation;
            }
        }

        // Reset throttle and inputs
        currentThrottle = 0f;
        throttleInput = 0f;
        yawInput = 0f;
        cyclicInput = Vector2.zero;

        // Reset PID state
        if (physics != null)
        {
            physics.ResetPIDState();
        }
        else
        {
            physics = GetComponent<DronePhysics>();
            if (physics != null) physics.ResetPIDState();
        }

        // Reset race timer if available
        var raceManager = FindObjectOfType<RaceManager>();
        if (raceManager != null)
        {
            raceManager.ResetTimer();
        }

        Debug.Log($"[DroneController] Reset complete. Position: {transform.position}");
    }

    #endregion

    #region Physics Application

    private void ApplyFlightPhysics()
    {
        physics.ApplyThrust(currentThrottle);
        physics.ApplyRotation(cyclicInput, yawInput);
    }

    private void ApplyGodModeMovement()
    {
        // Note: Don't set rb.velocity/angularVelocity on kinematic bodies (causes warning)
        // Kinematic bodies are moved via MovePosition/MoveRotation instead

        float dt = Time.fixedDeltaTime;

        // Vertical movement from throttle input (not sticky in god mode)
        Vector3 verticalMove = Vector3.up * throttleInput * godModeSpeed * dt;

        // Horizontal movement from cyclic (relative to drone facing)
        Vector3 forwardMove = transform.forward * cyclicInput.y * godModeSpeed * dt;
        Vector3 sideMove = transform.right * cyclicInput.x * godModeSpeed * dt;

        // Calculate and store movement direction for gate direction checks
        godModeMovementDirection = (verticalMove + forwardMove + sideMove).normalized;

        Vector3 targetPos = rb.position + verticalMove + forwardMove + sideMove;
        rb.MovePosition(targetPos);

        // Yaw rotation only
        float yawAmount = yawInput * godModeRotationSpeed * dt;
        Quaternion rotationChange = Quaternion.Euler(0f, yawAmount, 0f);
        Quaternion newRot = rb.rotation * rotationChange;

        // Enforce level orientation (no pitch/roll)
        Vector3 newEuler = newRot.eulerAngles;
        newRot = Quaternion.Euler(0f, newEuler.y, 0f);

        rb.MoveRotation(newRot);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set the input source at runtime.
    /// </summary>
    public void SetInputSource(InputSource source)
    {
        inputSource = source;
    }

    /// <summary>
    /// Set the flight mode at runtime.
    /// </summary>
    public void SetFlightMode(DronePhysics.FlightMode mode)
    {
        physics.CurrentMode = mode;
    }

    /// <summary>
    /// Set a new start position for reset at runtime.
    /// </summary>
    public void SetStartPosition(Vector3 position, Quaternion rotation)
    {
        startPosition = position;
        startRotation = rotation;
        Debug.Log($"[DroneController] Start position updated to: {position}");
    }

    /// <summary>
    /// Get the current start position.
    /// </summary>
    public Vector3 GetStartPosition()
    {
        return startPosition;
    }

    #endregion

#if UNITY_EDITOR
    /// <summary>
    /// Editor helper: Set the custom start position to the drone's current position.
    /// Right-click on the DroneController component and select this option.
    /// </summary>
    [ContextMenu("Set Current Position as Start Position")]
    private void SetCurrentAsStartPosition()
    {
        customStartPosition = transform.position;
        customStartRotation = transform.rotation.eulerAngles;
        useCustomStartPosition = true;
        Debug.Log($"[DroneController] Custom start position set to current position: {customStartPosition}");
    }
#endif
}
