using UnityEngine;
using UnityEngine.UI;

public class NewStickVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DroneController droneController;

    [Header("Left Stick (Throttle/Yaw)")]
    [SerializeField] private RectTransform leftStickKnob;
    [SerializeField] private float leftStickRange = 50f; // Distance from center

    [Header("Right Stick (Pitch/Roll)")]
    [SerializeField] private RectTransform rightStickKnob;
    [SerializeField] private float rightStickRange = 50f;

    private void Start()
    {
        if (droneController == null)
        {
            droneController = FindObjectOfType<DroneController>();
        }
    }

    private void Update()
    {
        if (droneController == null) return;

        // --- Left Stick ---
        // X: Yaw (Standard -1 to 1)
        float yaw = droneController.YawInput;

        // Y: Throttle 
        // We visualize the "Virtual" Sticky Throttle value because the physical stick 
        // just snaps back to center (0) which isn't informative for power level.
        // ThrottleValue is 0.0 to 1.0.
        // We map 0.0 -> -1.0 (Bottom) and 1.0 -> 1.0 (Top)
        float throttleVal = droneController.ThrottleValue; // 0..1
        float throttlePos = (throttleVal * 2f) - 1f; // -1..1

        if (leftStickKnob != null)
        {
            leftStickKnob.anchoredPosition = new Vector2(yaw, throttlePos) * leftStickRange;
        }

        // --- Right Stick ---
        // Standard Cyclic (Pitch/Roll) -1 to 1
        Vector2 cyclic = droneController.CyclicInput;

        if (rightStickKnob != null)
        {
            rightStickKnob.anchoredPosition = cyclic * rightStickRange;
        }
    }
}
