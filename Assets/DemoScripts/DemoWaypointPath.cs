using UnityEngine;
using System.Collections.Generic;

public class DemoWaypointPath : MonoBehaviour
{
    [Header("Path Settings")]
    [SerializeField] private bool loop = false;
    [SerializeField] private float defaultSpeed = 8f;

    [Header("Gizmo")]
    [SerializeField] private Color pathColor = Color.cyan;
    [SerializeField] private Color waypointColor = Color.green;
    [SerializeField] private float waypointGizmoRadius = 0.5f;
    [SerializeField] private int splineSamples = 20;

    public bool Loop => loop;
    public float DefaultSpeed => defaultSpeed;

    private List<Transform> waypoints = new List<Transform>();

    public int Count => waypoints.Count;

    public void CollectWaypoints()
    {
        waypoints.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeInHierarchy)
                waypoints.Add(child);
        }
    }

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count) return null;
        return waypoints[index];
    }

    public Vector3 GetPosition(int index)
    {
        if (waypoints.Count == 0) return transform.position;
        index = Mathf.Clamp(index, 0, waypoints.Count - 1);
        return waypoints[index].position;
    }

    public Vector3 GetSplinePosition(float t)
    {
        if (waypoints.Count < 2) return waypoints.Count == 1 ? waypoints[0].position : transform.position;

        int segmentCount = loop ? waypoints.Count : waypoints.Count - 1;
        float scaledT = t * segmentCount;
        int segment = Mathf.FloorToInt(scaledT);
        float localT = scaledT - segment;

        if (loop)
            segment = segment % waypoints.Count;
        else
            segment = Mathf.Clamp(segment, 0, waypoints.Count - 2);

        Vector3 p0 = waypoints[WrapIndex(segment - 1)].position;
        Vector3 p1 = waypoints[WrapIndex(segment)].position;
        Vector3 p2 = waypoints[WrapIndex(segment + 1)].position;
        Vector3 p3 = waypoints[WrapIndex(segment + 2)].position;

        return CatmullRom(p0, p1, p2, p3, localT);
    }

    public Vector3 GetSplineTangent(float t)
    {
        float delta = 0.001f;
        Vector3 a = GetSplinePosition(Mathf.Max(0f, t - delta));
        Vector3 b = GetSplinePosition(Mathf.Min(1f, t + delta));
        return (b - a).normalized;
    }

    public float GetApproximateTForWaypoint(int waypointIndex)
    {
        if (waypoints.Count < 2) return 0f;
        int segmentCount = loop ? waypoints.Count : waypoints.Count - 1;
        return (float)waypointIndex / segmentCount;
    }

    private int WrapIndex(int i)
    {
        int count = waypoints.Count;
        if (count == 0) return 0;
        if (loop)
            return ((i % count) + count) % count;
        return Mathf.Clamp(i, 0, count - 1);
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private void OnDrawGizmos()
    {
        List<Transform> tempWps = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeInHierarchy)
                tempWps.Add(child);
        }

        if (tempWps.Count < 2) return;

        // Draw waypoint spheres and labels
        for (int i = 0; i < tempWps.Count; i++)
        {
            Gizmos.color = waypointColor;
            Gizmos.DrawWireSphere(tempWps[i].position, waypointGizmoRadius);
            Gizmos.color = new Color(waypointColor.r, waypointColor.g, waypointColor.b, 0.3f);
            Gizmos.DrawSphere(tempWps[i].position, waypointGizmoRadius * 0.5f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(tempWps[i].position + Vector3.up * 1.2f, $"WP {i}");
#endif
        }

        // Draw spline path
        // Temporarily use tempWps for drawing
        var backupWps = waypoints;
        waypoints = tempWps;

        Gizmos.color = pathColor;
        int totalSamples = splineSamples * (tempWps.Count - 1);
        Vector3 prev = GetSplinePosition(0f);
        for (int i = 1; i <= totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            Vector3 pos = GetSplinePosition(t);
            Gizmos.DrawLine(prev, pos);
            prev = pos;
        }

        // Draw forward arrows at waypoints
        for (int i = 0; i < tempWps.Count; i++)
        {
            float wt = GetApproximateTForWaypoint(i);
            Vector3 tangent = GetSplineTangent(wt);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(tempWps[i].position, tangent * 2f);
        }

        waypoints = backupWps;
    }
}
