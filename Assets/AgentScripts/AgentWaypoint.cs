using UnityEngine;

/// <summary>
/// A single waypoint on a shared track. Multiple drones can use the same waypoints simultaneously.
/// Does NOT track per-drone state — each AgentDroneController manages its own waypoint index.
/// Simply fires an event when any drone enters this trigger, with direction validation.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class AgentWaypoint : MonoBehaviour
{
    /// <summary>Index within the track. Set by AgentTrackGenerator.</summary>
    [HideInInspector] public int waypointIndex;

    [Header("Debug")]
    [Tooltip("If enabled, logs every trigger event, including rejections (wrong tag, wrong direction).\nUse to diagnose why a waypoint is not detecting the drone.")]
    [SerializeField] private bool debugLog = false;

    [Tooltip("If enabled, bypasses the direction (dot product) check. Use when testing with a drone that hovers or moves slowly.")]
    [SerializeField] private bool ignoreDirectionCheck = false;

    /// <summary>
    /// Event fired when any drone enters this waypoint from the correct direction.
    /// Parameters: (this waypoint, the root GameObject that entered).
    /// Each AgentDroneController checks if this is their current target waypoint.
    /// </summary>
    public System.Action<AgentWaypoint, GameObject> OnWaypointEntered;

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (debugLog)
            Debug.Log($"[AgentWaypoint #{waypointIndex}] Trigger entered by '{other.name}' (tag: '{other.tag}', layer: {LayerMask.LayerToName(other.gameObject.layer)})");

        // Only respond to objects tagged "Player" (drone child colliders)
        if (!other.CompareTag("Player"))
        {
            if (debugLog)
                Debug.LogWarning($"[AgentWaypoint #{waypointIndex}] REJECTED: '{other.name}' has tag '{other.tag}', expected 'Player'.");
            return;
        }

        // Direction check: drone must be moving through the waypoint's forward direction
        Rigidbody rb = other.attachedRigidbody;
        if (!ignoreDirectionCheck && rb != null && rb.velocity.sqrMagnitude > 0.01f)
        {
            float dot = Vector3.Dot(transform.forward, rb.velocity.normalized);
            if (dot < 0f)
            {
                if (debugLog)
                    Debug.LogWarning($"[AgentWaypoint #{waypointIndex}] REJECTED: Wrong direction. dot={dot:F2}, fwd={transform.forward}, vel={rb.velocity}");
                return;
            }
        }

        // Fire event — let each drone controller decide if this is their active waypoint
        GameObject droneRoot = rb != null ? rb.gameObject : other.gameObject;

        if (debugLog)
        {
            int subscriberCount = OnWaypointEntered != null ? OnWaypointEntered.GetInvocationList().Length : 0;
            Debug.Log($"[AgentWaypoint #{waypointIndex}] ACCEPTED. Firing event to {subscriberCount} subscriber(s). Drone root: '{droneRoot.name}'.");
        }

        OnWaypointEntered?.Invoke(this, droneRoot);
    }
}
