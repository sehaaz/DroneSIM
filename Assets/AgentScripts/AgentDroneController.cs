using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

/// <summary>
/// ML-Agents drone agent that learns to fly through waypoints in sequence.
/// Uses continuous actions for throttle, pitch, roll, and yaw.
/// Designed for shared-track training: many drones use the SAME waypoints simultaneously.
/// Each drone independently tracks its own waypoint progress.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AgentDroneController : Agent
{
    #region Configuration

    [Header("References")]
    [Tooltip("The track generator containing the shared waypoints. All drones reference the SAME track — it is NOT duplicated.")]
    [SerializeField] private AgentTrackGenerator trackGenerator;

    [Header("Physics Settings")]
    [Tooltip("Mass of the drone rigidbody.")]
    [SerializeField] private float mass = 1.0f;

    [Tooltip("Maximum thrust scale relative to hover. 2.0 = full throttle produces 2x hover thrust.\nAction=0 (middle stick) always equals hover, regardless of this value.\nIncrease for snappier climb/acrobatics, decrease for gentler drones.")]
    [SerializeField] private float maxThrustScale = 2.0f;

    [Tooltip("Maximum torque for pitch and roll.")]
    [SerializeField] private float maxTorque = 20f;

    [Tooltip("Yaw torque strength.")]
    [SerializeField] private float yawTorque = 10f;

    [Tooltip("Linear drag applied to the rigidbody.")]
    [SerializeField] private float linearDrag = 0.5f;

    [Tooltip("Angular drag applied to the rigidbody.")]
    [SerializeField] private float angularDrag = 2.0f;

    [Header("Episode Settings")]
    [Tooltip("If enabled, the episode ends when the drone tilts beyond Max Tilt Before Reset.\nDISABLE for acro-mode training where flips/inversions are allowed.\nEnable for angle-mode training where the drone should stay roughly level.")]
    [SerializeField] private bool enableTiltCheck = false;

    [Tooltip("Maximum tilt angle (pitch or roll) before episode ends. Only used if Enable Tilt Check is true.")]
    [SerializeField] private float maxTiltBeforeReset = 45f;

    [Tooltip("Maximum expected distance to a waypoint for observation normalization.")]
    [SerializeField] private float maxExpectedDistance = 100f;

    [Tooltip("Maximum altitude for observation normalization.")]
    [SerializeField] private float maxAltitude = 50f;

    [Header("Spawn Settings")]
    [Tooltip("Position where the drone spawns at the start of each episode. Set by AgentTrainingManager or manually.")]
    [SerializeField] private Vector3 spawnPosition;

    [Tooltip("Rotation (Euler) where the drone spawns at the start of each episode.")]
    [SerializeField] private Vector3 spawnRotation;

    #endregion

    #region Reward Coefficients

    [Header("Sparse Rewards")]
    [Tooltip("Reward for passing through a waypoint correctly.\nRECOMMENDED: 1.0  |  RANGE: 0.5 - 3.0\nIncrease: agent prioritizes reaching waypoints more aggressively.\nDecrease: agent may explore more but reach waypoints less reliably.\nIncrease when: agent doesn't try hard enough to reach waypoints.\nNote: other rewards are usually scaled relative to this; changing it shifts the whole balance.")]
    [SerializeField] private float waypointReward = 1.0f;

    [Tooltip("Bonus reward for completing the entire track (added on top of the last waypoint reward).\nRECOMMENDED: waypointReward * waypointCount * 0.25  (ex: 20 waypoints -> 5.0)\nRANGE: 2.0 - 20.0 depending on track length\nIncrease: agent learns to finish the whole course rather than stalling near the end.\nDecrease: if agent rushes recklessly through the last gates.\nIncrease when: agent passes most waypoints but rarely finishes.")]
    [SerializeField] private float completionBonus = 2.0f;

    [Tooltip("Penalty for crashing into obstacles, ground, or walls. Episode ends.\nRECOMMENDED: -2.0  |  RANGE: -0.5 to -5.0\nIncrease magnitude: agent becomes more cautious.\nDecrease magnitude: agent takes more risks, may crash more.\nIncrease when: agent crashes often or dives into walls to reach a waypoint.\nDecrease when: agent is too timid and hovers without attempting.")]
    [SerializeField] private float crashPenalty = -2.0f;

    [Tooltip("Penalty for exceeding the maximum tilt angle. Episode ends. Only used if Enable Tilt Check is true.\nRECOMMENDED: -1.0 (angle mode) / unused in acro  |  RANGE: -0.5 to -2.0\nIncrease magnitude: agent avoids extreme angles.\nDecrease magnitude: agent may attempt aggressive maneuvers.\nIncrease when: agent frequently flips in angle-mode training.")]
    [SerializeField] private float tiltPenalty = -1.0f;

    [Tooltip("Penalty when the episode times out (MaxStep reached).\nRECOMMENDED: -0.5  |  RANGE: -0.2 to -1.5\nIncrease magnitude: agent is more motivated to finish quickly.\nDecrease magnitude: agent has less urgency.\nIncrease when: agent hovers without progressing or plays safe.\nDecrease when: agent rushes and crashes due to time pressure.")]
    [SerializeField] private float timeoutPenalty = -0.5f;

    [Tooltip("Penalty when the drone falls below altitude 0 (below ground level). Episode ends.\nRECOMMENDED: -1.0  |  RANGE: -0.5 to -2.0\nIncrease magnitude: agent avoids diving below ground.\nDecrease magnitude: agent may attempt risky low flights.\nIncrease when: agent keeps falling through the floor or drops into pits.\nNote: make sure spawn point y > 0 or drone triggers this on every spawn.")]
    [SerializeField] private float belowGroundPenalty = -1.0f;

    [Header("Dense Rewards (Per Step)")]
    [Tooltip("Scale factor for distance-reduction reward. Reward = (prevDist - curDist) * scale.\nRECOMMENDED: 0.02  |  RANGE: 0.005 - 0.1\nIncrease: stronger pull toward the waypoint each step (agent finds waypoints faster).\nDecrease: agent relies more on sparse waypoint reward (cleaner signal, slower learning).\nIncrease when: agent doesn't move toward waypoints, wanders randomly.\nDecrease when: agent hovers just outside a waypoint to farm positive delta back and forth.")]
    [SerializeField] private float distanceRewardScale = 0.02f;

    [Tooltip("Scale factor for velocity-waypoint alignment reward. reward = dot(velocity_dir, toWaypoint_dir) * scale.\nRECOMMENDED: 0.005  |  RANGE: 0.001 - 0.02\nIncrease: agent is rewarded more for flying directly toward the target.\nDecrease: less emphasis on flight direction; agent can take curved paths.\nIncrease when: agent flies sideways or backwards while still reaching waypoints (inefficient).\nDecrease when: agent can't turn sharply because it over-prioritizes facing the target.")]
    [SerializeField] private float alignmentRewardScale = 0.005f;

    [Tooltip("Fixed penalty subtracted every step to encourage faster completion.\nRECOMMENDED: 0.0005  |  RANGE: 0.0 - 0.005\nIncrease: agent rushes more.\nDecrease: agent has more time to be careful.\nIncrease when: agent hovers or moves too slowly without making progress.\nDecrease when: agent crashes from rushing.\nWarning: over 0.002 combined with MaxStep > 5000 can produce very negative totals.")]
    [SerializeField] private float timePenalty = 0.0005f;

    [Header("Altitude Reward (Dense)")]
    [Tooltip("Reward given every step when drone altitude is between min and max.\nUse this to teach the drone to STAY IN THE AIR before learning navigation.\nRECOMMENDED: 0.01 during initial training, 0 later  |  RANGE: 0 - 0.05\nIncrease: drone focuses harder on staying airborne (good for early learning).\nDecrease: reduce once drone reliably hovers; too high causes it to just hover and ignore waypoints.\nSet to 0 once drone passes waypoints consistently.")]
    [SerializeField] private float altitudeRewardScale = 0.01f;

    [Tooltip("Minimum altitude for the altitude reward to apply. Below this, no bonus.\nRECOMMENDED: 1.0  |  RANGE: 0.5 - 3.0\nKeep slightly above 0 so the drone learns to lift off and avoid ground scrape.")]
    [SerializeField] private float altitudeRewardMin = 1.0f;

    [Tooltip("Maximum altitude for the altitude reward to apply. Above this, no bonus (to prevent infinite climb farming).\nRECOMMENDED: 30.0  |  RANGE: 10.0 - 50.0\nSet this to roughly 1.5x the highest waypoint on your track.")]
    [SerializeField] private float altitudeRewardMax = 30.0f;

    [Tooltip("Penalty applied per step when drone is above Altitude Reward Max.\nUse to prevent the drone from climbing forever to farm altitude reward.\nRECOMMENDED: 0.005  |  RANGE: 0.0 - 0.02\nSet to 0 if you don't use altitude reward.")]
    [SerializeField] private float tooHighPenalty = 0.005f;

    [Header("Debug")]
    [Tooltip("Log waypoint subscription info at Initialize(), and every triggered waypoint event (including rejected ones).\nUse to diagnose why waypoints are not detected.")]
    [SerializeField] private bool debugWaypoints = false;

    #endregion

    #region Private State

    private Rigidbody rb;
    private float hoverThrust;

    private int currentWaypointIndex;
    private float previousDistanceToWaypoint;
    private bool subscribedToWaypoints;

    #endregion

    #region Public Properties

    /// <summary>Movement direction (velocity-based).</summary>
    public Vector3 MovementDirection => rb != null ? rb.velocity.normalized : Vector3.zero;

    #endregion

    #region ML-Agents Lifecycle

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.drag = linearDrag;
        rb.angularDrag = angularDrag;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        CalculateHoverThrust();

        if (spawnPosition == Vector3.zero)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.eulerAngles;
        }

        // Generate track if not already done (first drone to init triggers this)
        if (trackGenerator != null && trackGenerator.WaypointCount == 0)
        {
            trackGenerator.GenerateTrack();
        }

        SubscribeToWaypoints();
    }

    public override void OnEpisodeBegin()
    {
        // Reset drone physics — only THIS drone, others continue
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition;
        transform.rotation = Quaternion.Euler(spawnRotation);
        rb.position = spawnPosition;
        rb.rotation = Quaternion.Euler(spawnRotation);

        // Reset this drone's waypoint progress (NOT the shared waypoint objects)
        currentWaypointIndex = 0;

        if (trackGenerator != null)
        {
            if (!subscribedToWaypoints)
                SubscribeToWaypoints();

            var firstWp = trackGenerator.GetWaypoint(0);
            if (firstWp != null)
                previousDistanceToWaypoint = Vector3.Distance(spawnPosition, firstWp.transform.position);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 0-2: Local velocity (3)
        Vector3 localVelocity = transform.InverseTransformDirection(rb.velocity);
        sensor.AddObservation(localVelocity);

        // 3-5: Local angular velocity (3)
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        sensor.AddObservation(localAngularVelocity);

        // 6-8: Normalized euler angles (3)
        Vector3 euler = transform.eulerAngles;
        sensor.AddObservation(NormalizeAngle(euler.x) / 180f);
        sensor.AddObservation(NormalizeAngle(euler.y) / 180f);
        sensor.AddObservation(NormalizeAngle(euler.z) / 180f);

        // 9-11: Relative position to active waypoint (3)
        Vector3 activeRelPos = Vector3.zero;
        Vector3 nextRelPos = Vector3.zero;
        float normalizedDist = 0f;

        AgentWaypoint activeWp = trackGenerator != null ? trackGenerator.GetWaypoint(currentWaypointIndex) : null;
        AgentWaypoint nextWp = trackGenerator != null ? trackGenerator.GetWaypoint(currentWaypointIndex + 1) : null;

        if (activeWp != null)
        {
            activeRelPos = transform.InverseTransformPoint(activeWp.transform.position);
            normalizedDist = Vector3.Distance(transform.position, activeWp.transform.position) / maxExpectedDistance;
        }

        sensor.AddObservation(activeRelPos);

        // 12-14: Relative position to next waypoint (3)
        if (nextWp != null)
            nextRelPos = transform.InverseTransformPoint(nextWp.transform.position);
        else if (activeWp != null)
            nextRelPos = activeRelPos;

        sensor.AddObservation(nextRelPos);

        // 15: Normalized distance to active waypoint (1)
        sensor.AddObservation(Mathf.Clamp01(normalizedDist));

        // 16: Normalized altitude (1)
        sensor.AddObservation(Mathf.Clamp01(transform.position.y / maxAltitude));

        // Total: 17 observations
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuousActions = actions.ContinuousActions;

        float throttleAction = continuousActions[0]; // [-1, 1]
        float pitchAction = continuousActions[1];    // [-1, 1]
        float rollAction = continuousActions[2];     // [-1, 1]
        float yawAction = continuousActions[3];      // [-1, 1]

        // --- Apply Physics ---

        // Throttle mapping: action=-1 -> 0 thrust, action=0 -> hover, action=+1 -> maxThrustScale*hover
        // Formula derivation: throttle01 = (action+1)/2, thrust = throttle01 * hover * maxThrustScale
        // At action=0: throttle01=0.5, thrust = 0.5 * hover * maxThrustScale
        // To make action=0 == hover exactly, we need: 0.5 * hover * maxThrustScale = hover -> maxThrustScale = 2
        // For arbitrary maxThrustScale, use piecewise mapping so middle stick always = hover:
        float throttle01 = (throttleAction + 1f) * 0.5f;
        float thrust;
        if (throttle01 <= 0.5f)
            thrust = Mathf.Lerp(0f, hoverThrust, throttle01 * 2f);              // 0 -> hover
        else
            thrust = Mathf.Lerp(hoverThrust, hoverThrust * maxThrustScale, (throttle01 - 0.5f) * 2f); // hover -> max

        rb.AddForce(transform.up * thrust, ForceMode.Force);

        // Pitch torque (around local right axis)
        rb.AddRelativeTorque(Vector3.right * pitchAction * maxTorque, ForceMode.Acceleration);

        // Roll torque (around local forward axis)
        rb.AddRelativeTorque(Vector3.forward * rollAction * maxTorque, ForceMode.Acceleration);

        // Yaw torque (around local up axis)
        rb.AddRelativeTorque(Vector3.up * yawAction * yawTorque, ForceMode.Acceleration);

        // --- Altitude Check ---
        if (transform.position.y < 0f)
        {
            AddReward(belowGroundPenalty);
            EndEpisode();
            return;
        }

        // --- Tilt Check (only if enabled — disable for acro training) ---
        if (enableTiltCheck)
        {
            float pitch = Mathf.Abs(NormalizeAngle(transform.eulerAngles.x));
            float roll = Mathf.Abs(NormalizeAngle(transform.eulerAngles.z));

            if (pitch > maxTiltBeforeReset || roll > maxTiltBeforeReset)
            {
                AddReward(tiltPenalty);
                EndEpisode();
                return;
            }
        }

        // --- Dense Rewards ---
        AgentWaypoint activeWp = trackGenerator != null ? trackGenerator.GetWaypoint(currentWaypointIndex) : null;
        if (activeWp != null)
        {
            float currentDist = Vector3.Distance(transform.position, activeWp.transform.position);

            // Distance reduction reward
            float distReward = (previousDistanceToWaypoint - currentDist) * distanceRewardScale;
            AddReward(distReward);

            // Velocity alignment reward
            if (rb.velocity.sqrMagnitude > 0.1f)
            {
                Vector3 toWaypoint = (activeWp.transform.position - transform.position).normalized;
                float alignment = Vector3.Dot(rb.velocity.normalized, toWaypoint);
                AddReward(alignment * alignmentRewardScale);
            }

            previousDistanceToWaypoint = currentDist;
        }

        // Time penalty
        AddReward(-timePenalty);

        // --- Altitude Reward ---
        // Encourages the drone to stay airborne within a healthy altitude band.
        // Above max: small penalty to prevent infinite climb farming.
        float altitude = transform.position.y;
        if (altitude >= altitudeRewardMin && altitude <= altitudeRewardMax)
        {
            AddReward(altitudeRewardScale);
        }
        else if (altitude > altitudeRewardMax)
        {
            AddReward(-tooHighPenalty);
        }
        // Below min but above 0: no reward, no penalty (grace zone for takeoff/landing)

        // Timeout penalty when approaching MaxStep
        if (MaxStep > 0 && StepCount >= MaxStep - 1)
        {
            AddReward(timeoutPenalty);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;

        // Throttle: action=0 is hover (no key = stable hover). W climbs, S descends.
        // Using softer values (0.3) for gentler manual control, otherwise it's too twitchy.
        continuous[0] = 0f;
        if (Input.GetKey(KeyCode.W)) continuous[0] = 0.3f;   // light climb
        if (Input.GetKey(KeyCode.S)) continuous[0] = -0.3f;  // light descent
        if (Input.GetKey(KeyCode.LeftShift)) continuous[0] = 1f;  // boost
        if (Input.GetKey(KeyCode.LeftControl)) continuous[0] = -1f; // hard descent

        // Pitch
        continuous[1] = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) continuous[1] = 0.5f;
        if (Input.GetKey(KeyCode.DownArrow)) continuous[1] = -0.5f;

        // Roll
        continuous[2] = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) continuous[2] = 0.5f;
        if (Input.GetKey(KeyCode.LeftArrow)) continuous[2] = -0.5f;

        // Yaw
        continuous[3] = 0f;
        if (Input.GetKey(KeyCode.D)) continuous[3] = 0.5f;
        if (Input.GetKey(KeyCode.A)) continuous[3] = -0.5f;
    }

    #endregion

    #region Collision Handling

    private void OnCollisionEnter(Collision collision)
    {
        // NO drone-drone collision — physics layer isolation handles this.
        // Drones are on "TrainingDrone" layer which doesn't collide with itself.

        // Ground / obstacle collision
        if (collision.gameObject.CompareTag("Ground") ||
            collision.gameObject.CompareTag("World Object"))
        {
            AddReward(crashPenalty);
            EndEpisode();
        }
    }

    #endregion

    #region Waypoint Handling

    private void SubscribeToWaypoints()
    {
        if (trackGenerator == null)
        {
            if (debugWaypoints)
                Debug.LogError($"[AgentDroneController '{name}'] trackGenerator reference is NULL! Assign the AgentTrackGenerator in the Inspector.");
            return;
        }

        if (trackGenerator.WaypointCount == 0)
        {
            if (debugWaypoints)
                Debug.LogError($"[AgentDroneController '{name}'] trackGenerator '{trackGenerator.name}' has ZERO waypoints! Click 'Generate Track' in its Inspector, or ensure it has child control points.");
            return;
        }

        for (int i = 0; i < trackGenerator.WaypointCount; i++)
        {
            var wp = trackGenerator.GetWaypoint(i);
            if (wp != null)
            {
                wp.OnWaypointEntered -= OnWaypointTriggered;
                wp.OnWaypointEntered += OnWaypointTriggered;
            }
        }
        subscribedToWaypoints = true;

        if (debugWaypoints)
            Debug.Log($"[AgentDroneController '{name}'] Subscribed to {trackGenerator.WaypointCount} waypoints on track '{trackGenerator.name}'. currentWaypointIndex={currentWaypointIndex}");
    }

    private void OnWaypointTriggered(AgentWaypoint waypoint, GameObject droneRoot)
    {
        // Only respond if THIS drone triggered it
        if (droneRoot != gameObject)
        {
            if (debugWaypoints)
                Debug.Log($"[AgentDroneController '{name}'] IGNORED waypoint #{waypoint.waypointIndex}: triggered by a different drone '{droneRoot.name}'.");
            return;
        }

        // Only handle if this is THIS drone's current active waypoint
        if (waypoint.waypointIndex != currentWaypointIndex)
        {
            if (debugWaypoints)
                Debug.LogWarning($"[AgentDroneController '{name}'] SKIPPED waypoint #{waypoint.waypointIndex}: currently targeting #{currentWaypointIndex}. (out of order — drone must pass waypoints in sequence)");
            return;
        }

        if (debugWaypoints)
            Debug.Log($"[AgentDroneController '{name}'] PASSED waypoint #{waypoint.waypointIndex}! Advancing to #{currentWaypointIndex + 1}");

        // Waypoint reward
        AddReward(waypointReward);
        currentWaypointIndex++;

        // Check completion
        if (currentWaypointIndex >= trackGenerator.WaypointCount)
        {
            AddReward(completionBonus);
            EndEpisode();
            return;
        }

        // Update distance tracking for next waypoint
        var nextWp = trackGenerator.GetWaypoint(currentWaypointIndex);
        if (nextWp != null)
            previousDistanceToWaypoint = Vector3.Distance(transform.position, nextWp.transform.position);
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (trackGenerator == null) return;
        for (int i = 0; i < trackGenerator.WaypointCount; i++)
        {
            var wp = trackGenerator.GetWaypoint(i);
            if (wp != null)
                wp.OnWaypointEntered -= OnWaypointTriggered;
        }
    }

    #endregion

    #region Helpers

    private void CalculateHoverThrust()
    {
        // Force required to exactly counter gravity for this rigidbody.
        hoverThrust = rb.mass * Physics.gravity.magnitude;
    }

    private float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    /// <summary>
    /// Set spawn position at runtime (used by AgentTrainingManager).
    /// </summary>
    public void SetSpawnPosition(Vector3 position, Quaternion rotation)
    {
        spawnPosition = position;
        spawnRotation = rotation.eulerAngles;
    }

    /// <summary>
    /// Set the track generator reference at runtime.
    /// </summary>
    public void SetTrackGenerator(AgentTrackGenerator track)
    {
        trackGenerator = track;
    }

    #endregion
}
