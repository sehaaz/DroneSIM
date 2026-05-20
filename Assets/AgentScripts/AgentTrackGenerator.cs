using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates waypoints along a path defined by child AgentTrackControlPoint components.
/// Each segment between two control points can have its own curve, waypoint count, and scale.
/// Supports editor-time generation via ContextMenu for visual verification.
/// </summary>
[ExecuteAlways]
public class AgentTrackGenerator : MonoBehaviour
{
    [Header("Defaults (fallback if a control point has no AgentTrackControlPoint component)")]
    [Tooltip("Default number of intermediate waypoints per segment when a CP has no AgentTrackControlPoint.")]
    [SerializeField] private int defaultWaypointsPerSegment = 3;

    [Tooltip("Default curve depth per segment when a CP has no AgentTrackControlPoint.")]
    [SerializeField] private float defaultCurveDepth = 0f;

    [Tooltip("Default waypoint scale when a CP has no AgentTrackControlPoint.")]
    [SerializeField] private Vector3 defaultWaypointScale = new Vector3(3f, 3f, 1f);

    [Header("Waypoint Prefab")]
    [Tooltip("Prefab to instantiate for each waypoint. Must have an AgentWaypoint component and a BoxCollider set to trigger.\nIf left null, a visible colored cube is created automatically.")]
    [SerializeField] private GameObject waypointPrefab;

    [Header("Default Visual (when no prefab is assigned)")]
    [Tooltip("Color used for auto-created waypoint cubes (default visual). Ignored if a prefab is assigned.")]
    [SerializeField] private Color defaultWaypointColor = new Color(0.2f, 1f, 0.4f, 0.6f);

    [Header("Debug")]
    [Tooltip("Draw gizmos showing the track path and generated waypoints in the editor.")]
    [SerializeField] private bool drawGizmos = true;

    [Tooltip("Number of samples per segment used to draw the curve preview in the Scene view.")]
    [SerializeField] private int gizmoCurveSamples = 24;

    private List<AgentWaypoint> generatedWaypoints = new List<AgentWaypoint>();

    /// <summary>All waypoints in order. Read-only access for the agent.</summary>
    public IReadOnlyList<AgentWaypoint> Waypoints => generatedWaypoints;

    /// <summary>Total number of waypoints on this track.</summary>
    public int WaypointCount => generatedWaypoints.Count;

    // ---------------------------------------------------------------------

    /// <summary>
    /// Generate the track waypoints. Safe to call at runtime and in the editor.
    /// </summary>
    [ContextMenu("Generate Track")]
    public void GenerateTrack()
    {
        ClearWaypoints();

        List<Transform> controlPoints = CollectControlPoints();
        if (controlPoints.Count < 2)
        {
            Debug.LogError("[AgentTrackGenerator] Need at least 2 control points as children of this object!");
            return;
        }

        // Container for generated waypoints (keeps hierarchy clean)
        GameObject container = new GameObject("_GeneratedWaypoints");
        container.transform.SetParent(transform, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        int wpIndex = 0;

        for (int seg = 0; seg < controlPoints.Count - 1; seg++)
        {
            Transform cpA = controlPoints[seg];
            Transform cpB = controlPoints[seg + 1];

            AgentTrackControlPoint settingsA = cpA.GetComponent<AgentTrackControlPoint>();
            AgentTrackControlPoint settingsB = cpB.GetComponent<AgentTrackControlPoint>();

            // Segment settings come from the STARTING control point (A)
            int intermediateCount = settingsA != null ? settingsA.waypointsToNext : defaultWaypointsPerSegment;
            float curveDepth = settingsA != null ? settingsA.curveDepth : defaultCurveDepth;
            Vector3 curveDir = settingsA != null ? settingsA.curveDirection : Vector3.up;
            Vector3 segmentScale = settingsA != null ? settingsA.segmentWaypointScale : defaultWaypointScale;

            Vector3 start = cpA.position;
            Vector3 end = cpB.position;

            // Calculate control point for quadratic Bezier (midpoint with offset)
            Vector3 midpoint = (start + end) * 0.5f;
            if (Mathf.Abs(curveDepth) > 0.0001f)
            {
                Vector3 segDir = (end - start).normalized;
                Vector3 perpendicular;

                if (curveDir.sqrMagnitude < 0.0001f)
                {
                    // Auto-pick a perpendicular direction
                    perpendicular = Vector3.Cross(segDir, Vector3.right).normalized;
                    if (perpendicular.sqrMagnitude < 0.01f)
                        perpendicular = Vector3.Cross(segDir, Vector3.forward).normalized;
                }
                else
                {
                    // Project curveDir onto plane perpendicular to segment
                    Vector3 projected = curveDir - Vector3.Dot(curveDir, segDir) * segDir;
                    if (projected.sqrMagnitude < 0.0001f)
                    {
                        perpendicular = Vector3.Cross(segDir, Vector3.right).normalized;
                        if (perpendicular.sqrMagnitude < 0.01f)
                            perpendicular = Vector3.Cross(segDir, Vector3.forward).normalized;
                    }
                    else
                    {
                        perpendicular = projected.normalized;
                    }
                }

                midpoint += perpendicular * curveDepth;
            }

            // --- Place waypoint AT starting control point (if enabled) ---
            if (seg == 0 && settingsA != null && settingsA.placeWaypointHere)
            {
                Vector3 tangentAtStart = QuadraticBezierTangent(start, midpoint, end, 0f).normalized;
                if (tangentAtStart.sqrMagnitude < 0.001f)
                    tangentAtStart = (end - start).normalized;

                Vector3 scale = settingsA.useSegmentScale ? segmentScale : settingsA.waypointScaleOverride;
                CreateWaypoint(container.transform, start, Quaternion.LookRotation(tangentAtStart, Vector3.up), scale, wpIndex++);
            }
            else if (seg == 0 && settingsA == null)
            {
                // No component — no waypoint at CP (user can add a component to opt in)
            }

            // --- Intermediate waypoints along the curve ---
            for (int i = 1; i <= intermediateCount; i++)
            {
                float t = (float)i / (intermediateCount + 1);
                Vector3 pos = QuadraticBezier(start, midpoint, end, t);
                Vector3 tangent = QuadraticBezierTangent(start, midpoint, end, t).normalized;
                if (tangent.sqrMagnitude < 0.001f)
                    tangent = (end - start).normalized;

                CreateWaypoint(container.transform, pos, Quaternion.LookRotation(tangent, Vector3.up), segmentScale, wpIndex++);
            }

            // --- Place waypoint AT ending control point (if enabled) ---
            // Only placed here if we're on the last segment OR the CP has "placeWaypointHere" enabled.
            // For middle CPs, this ensures the CP waypoint is placed exactly once (at the start of the next segment is also valid, but we do it at end of current).
            if (settingsB != null && settingsB.placeWaypointHere)
            {
                // Use the tangent at the END of this segment for forward direction
                Vector3 tangentAtEnd = QuadraticBezierTangent(start, midpoint, end, 1f).normalized;
                if (tangentAtEnd.sqrMagnitude < 0.001f)
                    tangentAtEnd = (end - start).normalized;

                // For middle CPs, blend with next segment's starting direction for smoother flow
                if (seg < controlPoints.Count - 2)
                {
                    Vector3 nextDir = (controlPoints[seg + 2].position - cpB.position).normalized;
                    tangentAtEnd = ((tangentAtEnd + nextDir) * 0.5f).normalized;
                    if (tangentAtEnd.sqrMagnitude < 0.001f)
                        tangentAtEnd = (end - start).normalized;
                }

                Vector3 scale = settingsB.useSegmentScale
                    ? (settingsB != null ? settingsB.segmentWaypointScale : segmentScale)
                    : settingsB.waypointScaleOverride;

                CreateWaypoint(container.transform, end, Quaternion.LookRotation(tangentAtEnd, Vector3.up), scale, wpIndex++);
            }
        }

        Debug.Log($"[AgentTrackGenerator] Generated {generatedWaypoints.Count} waypoints from {controlPoints.Count} control points on '{name}'.");
    }

    /// <summary>
    /// Destroy all generated waypoints. Safe at runtime and in the editor.
    /// </summary>
    [ContextMenu("Clear Track")]
    public void ClearWaypoints()
    {
        // Destroy existing container(s) in the editor or runtime
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "_GeneratedWaypoints")
            {
                DestroySafe(child.gameObject);
            }
        }
        generatedWaypoints.Clear();
    }

    public AgentWaypoint GetWaypoint(int index)
    {
        if (index >= 0 && index < generatedWaypoints.Count)
            return generatedWaypoints[index];
        return null;
    }

    // ---------------------------------------------------------------------
    // Internals

    private List<Transform> CollectControlPoints()
    {
        List<Transform> cps = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "_GeneratedWaypoints") continue;
            cps.Add(child);
        }
        return cps;
    }

    private void CreateWaypoint(Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, int index)
    {
        GameObject wpObj;

        if (waypointPrefab != null)
        {
            wpObj = Instantiate(waypointPrefab, pos, rot, parent);
        }
        else
        {
            wpObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wpObj.transform.SetParent(parent, true);
            wpObj.transform.position = pos;
            wpObj.transform.rotation = rot;

            // Replace the default collider with a trigger
            var defaultCol = wpObj.GetComponent<Collider>();
            if (defaultCol != null) DestroySafe(defaultCol);
            var box = wpObj.AddComponent<BoxCollider>();
            box.isTrigger = true;

            // Apply a visible default color so the waypoint is actually visible
            var renderer = wpObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", defaultWaypointColor);
                mpb.SetColor("_BaseColor", defaultWaypointColor);
                mpb.SetColor("_EmissionColor", defaultWaypointColor * 0.6f);
                renderer.SetPropertyBlock(mpb);
            }
        }

        wpObj.name = $"Waypoint_{index:D3}";
        wpObj.transform.localScale = scale;

        AgentWaypoint wp = wpObj.GetComponent<AgentWaypoint>();
        if (wp == null) wp = wpObj.AddComponent<AgentWaypoint>();

        wp.waypointIndex = index;

        generatedWaypoints.Add(wp);
    }

    private void DestroySafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    private Vector3 QuadraticBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return 2f * u * (p1 - p0) + 2f * t * (p2 - p1);
    }

    // ---------------------------------------------------------------------
    // Gizmos

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        List<Transform> cps = CollectControlPoints();
        if (cps.Count < 2) return;

        // Draw curve previews per segment
        for (int seg = 0; seg < cps.Count - 1; seg++)
        {
            Transform cpA = cps[seg];
            Transform cpB = cps[seg + 1];
            AgentTrackControlPoint settingsA = cpA.GetComponent<AgentTrackControlPoint>();

            float curveDepth = settingsA != null ? settingsA.curveDepth : defaultCurveDepth;
            Vector3 curveDir = settingsA != null ? settingsA.curveDirection : Vector3.up;

            Vector3 start = cpA.position;
            Vector3 end = cpB.position;
            Vector3 midpoint = (start + end) * 0.5f;

            if (Mathf.Abs(curveDepth) > 0.0001f)
            {
                Vector3 segDir = (end - start).normalized;
                Vector3 perpendicular;
                if (curveDir.sqrMagnitude < 0.0001f)
                {
                    perpendicular = Vector3.Cross(segDir, Vector3.right).normalized;
                    if (perpendicular.sqrMagnitude < 0.01f)
                        perpendicular = Vector3.Cross(segDir, Vector3.forward).normalized;
                }
                else
                {
                    Vector3 projected = curveDir - Vector3.Dot(curveDir, segDir) * segDir;
                    if (projected.sqrMagnitude < 0.0001f)
                    {
                        perpendicular = Vector3.Cross(segDir, Vector3.right).normalized;
                        if (perpendicular.sqrMagnitude < 0.01f)
                            perpendicular = Vector3.Cross(segDir, Vector3.forward).normalized;
                    }
                    else
                    {
                        perpendicular = projected.normalized;
                    }
                }
                midpoint += perpendicular * curveDepth;
            }

            Gizmos.color = Color.yellow;
            Vector3 prev = start;
            int samples = Mathf.Max(2, gizmoCurveSamples);
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 pt = QuadraticBezier(start, midpoint, end, t);
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }

            // Draw line from start to midpoint for debugging curve handle
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawLine(start, midpoint);
            Gizmos.DrawLine(midpoint, end);
        }

        // Draw already-generated waypoints
        if (generatedWaypoints != null)
        {
            foreach (var wp in generatedWaypoints)
            {
                if (wp == null) continue;
                Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.6f);
                Gizmos.matrix = wp.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(wp.transform.position, wp.transform.forward * 2f);
            }
        }
    }
}
