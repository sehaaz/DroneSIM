using UnityEngine;
using System.Collections.Generic;

public class RaceManager : MonoBehaviour
{
    [Header("Track Setup")]
    [Tooltip("Drag Gates here in the order you want them flown.")]
    public List<RaceGate> gates = new List<RaceGate>();

    [Header("Rules")]
    [Tooltip("Max distance allowed from the current gate before reset.")]
    public float maxDistanceToGate = 50f;

    // Timer Logic
    public float RaceTime { get; private set; }
    public bool IsRaceActive { get; private set; }
    public bool IsRaceFinished { get; private set; }

    private int currentGateIndex = 0;
    public int CurrentGateIndex => currentGateIndex; // Public read-access

    // Cooldown to prevent spamming "LOST SIGNAL!" message
    private float lostSignalCooldown = 0f;
    private const float LOST_SIGNAL_COOLDOWN_TIME = 3f;

    private void Start()
    {
        if (gates == null || gates.Count == 0)
        {
            Debug.LogError("[RaceManager] No Gates assigned! Please drag RaceGate objects into the 'Gates' list in the Inspector.");
            return;
        }

        // Initialize Race
        StartRace();
    }

    private DroneController droneController;

    private void Update()
    {
        if (droneController == null)
        {
            droneController = FindObjectOfType<DroneController>();
            return;
        }

        // Update cooldown timer
        if (lostSignalCooldown > 0f)
            lostSignalCooldown -= Time.deltaTime;

        // Check distance to current gate
        if (gates.Count > 0 && currentGateIndex < gates.Count)
        {
            RaceGate currentGate = gates[currentGateIndex];
            if (currentGate != null)
            {
                float dist = Vector3.Distance(droneController.transform.position, currentGate.transform.position);
                if (dist > maxDistanceToGate && lostSignalCooldown <= 0f)
                {
                    Debug.Log($"[RaceManager] Resetting: Too far from Gate {currentGateIndex} ({dist:F1}m > {maxDistanceToGate}m)");
                    droneController.ResetDrone();

                    RaceDistanceHUD hud = FindObjectOfType<RaceDistanceHUD>();
                    if (hud != null) hud.ShowMessage("LOST SIGNAL!", 2f);

                    lostSignalCooldown = LOST_SIGNAL_COOLDOWN_TIME;
                }
            }
        }

        // Timer Logic
        if (!IsRaceActive && !IsRaceFinished)
        {
            // Start if moving
            if (droneController.CurrentSpeed > 0.1f)
            {
                IsRaceActive = true;
            }
        }

        if (IsRaceActive)
        {
            RaceTime += Time.deltaTime;
        }
    }

    public void ResetTimer()
    {
        RaceTime = 0f;
        IsRaceActive = false;
        IsRaceFinished = false;
        currentGateIndex = 0; // Ensure gate reset too
        lostSignalCooldown = 0f; // Reset cooldown on manual reset
        UpdateAllGateStates();
    }

    [ContextMenu("Restart Race")]
    public void StartRace()
    {
        // Don't fully reset timer here if we want to loop, 
        // but the user requirement is "stop at end". 
        // If we loop, we probably want to restart? 
        // Let's assume loop = restart race.
        currentGateIndex = 0;
        // Note: We don't ResetTimer() here automatically if we want to see the result time for a moment.
        // But since this is called by 'Start' and 'Loop', let's stick to the basics:
        // If loop, we just reset gates. Timer stops.
        // Waiting for reset (R) to zero it? Or movement?
        // Let's rely on 'R' to zero it as requested.

        UpdateAllGateStates();
    }

    public void OnGatePassed(RaceGate gate)
    {
        // Double check validation (Gate already checks state, but good to be safe)
        if (gates[currentGateIndex] != gate)
        {
            Debug.LogWarning($"[RaceManager] Received pass from wrong gate: {gate.name}. Expected: {gates[currentGateIndex].name}");
            return;
        }

        Debug.Log($"[RaceManager] Gate {currentGateIndex} ({gate.name}) Cleared!");

        // Move to next
        currentGateIndex++;

        // Check Win
        if (currentGateIndex >= gates.Count)
        {
            Debug.Log("[RaceManager] RACE FINISHED! Loop complete.");
            IsRaceActive = false;
            IsRaceFinished = true;
            StartRace(); // Loop functionality (resets indices, but keeps 'Finished' flag for UI until next move?)
                         // Actually, StartRace resets everything. Let's adjust logic.
                         // If we want the time to freeze, we shouldn't reset immediately or we should store the final time.
                         // For now, let's keep it simple: Loop restarts race, but maybe we pause timer?
        }
        else
        {
            UpdateAllGateStates();
        }
    }

    private void UpdateAllGateStates()
    {
        for (int i = 0; i < gates.Count; i++)
        {
            if (gates[i] == null) continue;

            // Sync index for debug visualization
            gates[i].gateIndex = i;

            if (i == currentGateIndex)
            {
                gates[i].SetState(RaceGate.GateState.Active);
            }
            else if (i == (currentGateIndex + 1) % gates.Count)
            {
                gates[i].SetState(RaceGate.GateState.Next);
            }
            else if (i < currentGateIndex)
            {
                gates[i].SetState(RaceGate.GateState.Passed);
            }
            else
            {
                gates[i].SetState(RaceGate.GateState.Inactive);
            }
        }
    }
}
