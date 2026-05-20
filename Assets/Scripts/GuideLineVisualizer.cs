using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GuideLineVisualizer : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    public RaceManager raceManager;

    [Header("Visuals")]
    public float lineWidth = 0.1f;
    public Color lineColor = Color.green;

    private LineRenderer lineRenderer;
    private bool isVisible = true;

    public void ToggleVisibility()
    {
        isVisible = !isVisible;
    }

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        // Setup LineRenderer defaults if needed
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Basic safe material
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.positionCount = 2;

        if (raceManager == null) raceManager = FindObjectOfType<RaceManager>();

        if (playerTransform == null)
        {
            DroneController drone = FindObjectOfType<DroneController>();
            if (drone != null) playerTransform = drone.transform;
        }
    }

    private void LateUpdate()
    {
        if (lineRenderer == null || playerTransform == null || raceManager == null) return;

        if (!isVisible)
        {
            lineRenderer.enabled = false;
            return;
        }

        // Check if we have a valid gate
        int index = raceManager.CurrentGateIndex;
        if (index >= 0 && index < raceManager.gates.Count && raceManager.gates[index] != null)
        {
            RaceGate targetGate = raceManager.gates[index];

            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, playerTransform.position);
            lineRenderer.SetPosition(1, targetGate.transform.position);
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }
}
