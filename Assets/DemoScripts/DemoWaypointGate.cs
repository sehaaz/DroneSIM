using UnityEngine;

public class DemoWaypointGate : MonoBehaviour
{
    public enum GateVisualState
    {
        Waiting,
        Active,
        Passed
    }

    [Header("Visual")]
    [SerializeField] private Renderer gateRenderer;
    [SerializeField] private Color waitingColor = new Color(0.3f, 0.3f, 0.5f, 0.1f);
    [SerializeField] private Color activeColor = new Color(0.1f, 1f, 0.4f, 0.2f);
    [SerializeField] private Color passedColor = new Color(0.2f, 0.3f, 0.8f, 0.06f);

    [Header("Edge Colors")]
    [SerializeField] private Color edgeWaitingColor = new Color(0.4f, 0.4f, 0.6f, 0.4f);
    [SerializeField] private Color edgeActiveColor = new Color(0.2f, 1f, 0.5f, 0.8f);
    [SerializeField] private Color edgePassedColor = new Color(0.3f, 0.3f, 0.7f, 0.25f);

    [Header("Pulse")]
    [SerializeField] private bool pulseWhenActive = true;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseIntensity = 0.4f;

    private GateVisualState state = GateVisualState.Waiting;
    private Material matInstance;
    private Material[] edgeMaterials;

    private void Awake()
    {
        if (gateRenderer == null)
            gateRenderer = GetComponentInChildren<Renderer>();

        if (gateRenderer != null)
        {
            matInstance = new Material(gateRenderer.material);
            gateRenderer.material = matInstance;
        }

        // Collect edge bar renderers (children named Edge*)
        var allRenderers = GetComponentsInChildren<Renderer>();
        int edgeCount = 0;
        foreach (var r in allRenderers)
        {
            if (r != gateRenderer && r.name.StartsWith("Edge"))
                edgeCount++;
        }
        edgeMaterials = new Material[edgeCount];
        int idx = 0;
        foreach (var r in allRenderers)
        {
            if (r != gateRenderer && r.name.StartsWith("Edge"))
            {
                edgeMaterials[idx] = new Material(r.material);
                r.material = edgeMaterials[idx];
                idx++;
            }
        }

        SetState(GateVisualState.Waiting);
    }

    private void Update()
    {
        if (state != GateVisualState.Active || !pulseWhenActive) return;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;

        if (matInstance != null)
        {
            Color pulsed = activeColor * pulse;
            pulsed.a = activeColor.a;
            matInstance.SetColor("_EmissionColor", new Color(activeColor.r, activeColor.g, activeColor.b) * pulse * 0.4f);
        }

        if (edgeMaterials != null)
        {
            Color edgePulsed = edgeActiveColor * pulse;
            edgePulsed.a = edgeActiveColor.a;
            foreach (var m in edgeMaterials)
            {
                if (m != null)
                    m.SetColor("_EmissionColor", new Color(edgeActiveColor.r, edgeActiveColor.g, edgeActiveColor.b) * pulse * 0.7f);
            }
        }
    }

    public void SetState(GateVisualState newState)
    {
        state = newState;

        Color color, edgeColor;
        switch (state)
        {
            case GateVisualState.Active:
                color = activeColor;
                edgeColor = edgeActiveColor;
                break;
            case GateVisualState.Passed:
                color = passedColor;
                edgeColor = edgePassedColor;
                break;
            default:
                color = waitingColor;
                edgeColor = edgeWaitingColor;
                break;
        }

        if (matInstance != null)
        {
            matInstance.color = color;
            matInstance.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 0.3f);
            if (matInstance.HasProperty("_BaseColor"))
                matInstance.SetColor("_BaseColor", color);
        }

        if (edgeMaterials != null)
        {
            foreach (var m in edgeMaterials)
            {
                if (m == null) continue;
                m.color = edgeColor;
                m.SetColor("_EmissionColor", new Color(edgeColor.r, edgeColor.g, edgeColor.b) * 0.5f);
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", edgeColor);
            }
        }
    }

    private void OnDestroy()
    {
        if (matInstance != null)
            Destroy(matInstance);
        if (edgeMaterials != null)
        {
            foreach (var m in edgeMaterials)
                if (m != null) Destroy(m);
        }
    }
}
