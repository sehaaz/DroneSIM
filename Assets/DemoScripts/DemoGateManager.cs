using UnityEngine;

public class DemoGateManager : MonoBehaviour
{
    [SerializeField] private DemoWaypointPath waypointPath;
    [SerializeField] private DemoAutopilot autopilot;

    private DemoWaypointGate[] gates;
    private int lastHighlightedIndex = -1;

    private void Start()
    {
        if (waypointPath == null)
            waypointPath = FindObjectOfType<DemoWaypointPath>();
        if (autopilot == null)
            autopilot = FindObjectOfType<DemoAutopilot>();
        if (waypointPath == null) return;

        waypointPath.CollectWaypoints();
        gates = new DemoWaypointGate[waypointPath.Count];

        for (int i = 0; i < waypointPath.Count; i++)
        {
            Transform wp = waypointPath.GetWaypoint(i);
            if (wp != null)
                gates[i] = wp.GetComponent<DemoWaypointGate>();
        }

        UpdateGateVisuals();
    }

    private void Update()
    {
        if (autopilot == null) return;

        int current = autopilot.CurrentWaypointIndex;
        if (current != lastHighlightedIndex)
        {
            lastHighlightedIndex = current;
            UpdateGateVisuals();
        }
    }

    private void UpdateGateVisuals()
    {
        if (gates == null) return;

        int current = autopilot != null ? autopilot.CurrentWaypointIndex : 0;

        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] == null) continue;

            if (i < current)
                gates[i].SetState(DemoWaypointGate.GateVisualState.Passed);
            else if (i == current)
                gates[i].SetState(DemoWaypointGate.GateVisualState.Active);
            else
                gates[i].SetState(DemoWaypointGate.GateVisualState.Waiting);
        }
    }
}
