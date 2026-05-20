using UnityEngine;
using TMPro;

public class RaceDistanceHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player/drone object to track.")]
    public Transform playerTransform;
    [Tooltip("The RaceManager to get gate info from.")]
    public RaceManager raceManager;

    [Header("Toggles")]
    public GameObject cameraContainer;
    public GuideLineVisualizer guideLine;

    [Header("UI Elements")]
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI greenDistText;
    public TextMeshProUGUI yellowDistText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI verticalSpeedText;
    public TextMeshProUGUI modeText;

    [Header("Status Feedback")]
    public TextMeshProUGUI statusText;

    private System.Collections.IEnumerator clearMessageCoroutine;

    // Singleton-ish access or just public method
    public void ShowMessage(string message, float duration = 2f)
    {
        if (statusText == null) return;

        statusText.text = message;
        statusText.gameObject.SetActive(true);

        if (clearMessageCoroutine != null) StopCoroutine(clearMessageCoroutine);
        clearMessageCoroutine = ClearMessageAfterDelay(duration);
        StartCoroutine(clearMessageCoroutine);
    }

    private System.Collections.IEnumerator ClearMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        statusText.text = "";
        // Optional: statusText.gameObject.SetActive(false);
    }

    private void Start()
    {
        // Auto-find references if not assigned
        if (raceManager == null)
        {
            raceManager = FindObjectOfType<RaceManager>();
            if (raceManager == null) Debug.LogError("[RaceDistanceHUD] No RaceManager found!");
        }

        if (playerTransform == null)
        {
            DroneController drone = FindObjectOfType<DroneController>();
            if (drone != null) playerTransform = drone.transform;
            else Debug.LogError("[RaceDistanceHUD] No DroneController/Player found!");
        }
    }
    private void Update()
    {
        // Toggle Logic (Tab OR Gamepad Start - Joystick Button 7)
        bool toggle = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.JoystickButton7);

        if (toggle)
        {
            if (cameraContainer != null) cameraContainer.SetActive(!cameraContainer.activeSelf);
            if (guideLine != null) guideLine.ToggleVisibility();
        }

        if (playerTransform == null && raceManager != null)
        {
            // Try to find via DroneController
            DroneController droneRef = FindObjectOfType<DroneController>();
            if (droneRef != null) playerTransform = droneRef.transform;
        }

        if (playerTransform == null) return;

        DroneController drone = playerTransform.GetComponent<DroneController>();

        // 0. New Stats (Time, Speed, Mode)
        if (drone != null)
        {
            if (speedText != null) speedText.text = $"Speed: {drone.CurrentSpeed:F1} m/s";
            if (verticalSpeedText != null) verticalSpeedText.text = $"V. Speed: {drone.VerticalSpeed:F1} m/s";
            if (modeText != null) modeText.text = $"Mode: {drone.CurrentModeName}";
        }

        if (raceManager != null)
        {
            if (timeText != null)
            {
                // Format time mm:ss.ms
                float t = raceManager.RaceTime;
                System.TimeSpan ts = System.TimeSpan.FromSeconds(t);
                timeText.text = $"Time: {ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
            }
        }

        // 1. Altitude
        float altitude = playerTransform.position.y;
        if (altitudeText != null)
        {
            altitudeText.text = $"Altitude: {altitude:F1}m";
        }

        // Check race manager
        if (raceManager == null || raceManager.gates.Count == 0) return;

        int currentIndex = raceManager.CurrentGateIndex;

        // 2. Distance to Green (Current Active Gate)
        // Safety check: index within bounds
        if (currentIndex >= 0 && currentIndex < raceManager.gates.Count)
        {
            RaceGate greenGate = raceManager.gates[currentIndex];
            if (greenGate != null)
            {
                float dist = Vector3.Distance(playerTransform.position, greenGate.transform.position);
                if (greenDistText != null) greenDistText.text = $"Green: {dist:F1}m"; // "G: " prefix optional in UI text object
            }
        }
        else
        {
            if (greenDistText != null) greenDistText.text = "-";
        }

        // 3. Distance to Yellow (Next Gate)
        // Wraps around using modulo for looping tracks
        int nextIndex = (currentIndex + 1) % raceManager.gates.Count;
        RaceGate yellowGate = raceManager.gates[nextIndex];

        if (yellowGate != null && nextIndex != currentIndex) // Ensure we have at least 2 gates for a 'next'
        {
            float dist = Vector3.Distance(playerTransform.position, yellowGate.transform.position);
            if (yellowDistText != null) yellowDistText.text = $"Yellow: {dist:F1}m";
        }
        else
        {
            // If only 1 gate or error
            if (yellowDistText != null) yellowDistText.text = "-";
        }
    }
}
