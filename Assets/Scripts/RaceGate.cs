using UnityEngine;

public class RaceGate : MonoBehaviour
{
    public enum GateState
    {
        Inactive,
        Active,   // The current target (Green)
        Passed,   // Already cleared (Red/Dim)
        Next      // The upcoming target (Yellow)
    }

    [Header("Identity")]
    // We no longer rely on this index for logic, but helpful for debug
    public int gateIndex;

    [Header("Visuals")]
    [SerializeField] private Renderer gateRenderer;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color nextColor = Color.yellow; // Optional: logical next
    [SerializeField] private Color passedColor = Color.red;
    [SerializeField] private Color inactiveColor = Color.grey;

    private GateState currentState = GateState.Inactive;
    private RaceManager raceManager;

    private void Start()
    {
        raceManager = FindObjectOfType<RaceManager>();
    }

    /// <summary>
    /// Called by RaceManager to set the visual and logical state.
    /// </summary>
    public void SetState(GateState newState)
    {
        currentState = newState;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (gateRenderer == null) return;

        Color targetColor = inactiveColor;

        switch (currentState)
        {
            case GateState.Active:
                targetColor = activeColor;
                break;
            case GateState.Next:
                targetColor = nextColor;
                break;
            case GateState.Passed:
                targetColor = passedColor;
                break;
            case GateState.Inactive:
                targetColor = inactiveColor;
                break;
        }

        gateRenderer.material.SetColor("_EmissionColor", targetColor);
        // Also set albedo/base color if using a standard shader
        gateRenderer.material.color = targetColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Only care if we are the ACTIVE gate
        if (currentState != GateState.Active) return;

        // 2. Only Player
        if (!other.CompareTag("Player")) return;

        // 3. Direction Check - use MovementDirection from DroneController (works in god mode too)
        DroneController droneController = other.GetComponentInParent<DroneController>();
        if (droneController != null)
        {
            Vector3 movementDir = droneController.MovementDirection;

            // Skip direction check if not moving (allow stationary gate passes)
            if (movementDir.sqrMagnitude > 0.01f)
            {
                float dot = Vector3.Dot(transform.forward, movementDir);
                if (dot < 0)
                {
                    Debug.LogWarning($"[RaceGate] WRONG WAY! You entered Gate {gateIndex} from behind.");
                    return;
                }
            }
        }
        else
        {
            // Fallback to rigidbody velocity if no DroneController found
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null && rb.velocity.sqrMagnitude > 0.01f)
            {
                float dot = Vector3.Dot(transform.forward, rb.velocity.normalized);
                if (dot < 0)
                {
                    Debug.LogWarning($"[RaceGate] WRONG WAY! You entered Gate {gateIndex} from behind.");
                    return;
                }
            }
        }

        // 4. Success
        Debug.Log($"[RaceGate] Gate {gateIndex} Passed Correctly!");
        raceManager.OnGatePassed(this);
    }
}
