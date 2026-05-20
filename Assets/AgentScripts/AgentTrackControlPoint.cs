using UnityEngine;

/// <summary>
/// Control point for AgentTrackGenerator. Attach this to each child of a track.
/// Defines per-segment settings: curve, waypoint count, scale, and whether to place a waypoint at this CP.
/// Settings here affect the segment going FROM this control point TO the next one.
/// </summary>
public class AgentTrackControlPoint : MonoBehaviour
{
    [Header("This Control Point")]
    [Tooltip("If true, a waypoint will be placed exactly at this control point's position.\nUse this to guarantee the drone must fly through this exact spot.\nDisable if you only want curve/generation points without a waypoint at the CP itself.")]
    public bool placeWaypointHere = true;

    [Tooltip("Override the waypoint scale at this control point (only used if 'Place Waypoint Here' is enabled).\nIf 'Use Segment Scale' is true, this is ignored and the segment scale is used instead.")]
    public Vector3 waypointScaleOverride = new Vector3(3f, 3f, 1f);

    [Tooltip("If true, use the segment's waypoint scale for this CP's waypoint. If false, use the override above.")]
    public bool useSegmentScale = true;

    [Header("Segment To Next Control Point")]
    [Tooltip("Number of intermediate waypoints to generate between this control point and the next one.\nDoes NOT include waypoints at the control points themselves — those are controlled by 'Place Waypoint Here'.\n0 = no intermediate waypoints (just direct line).")]
    [Min(0)]
    public int waypointsToNext = 3;

    [Tooltip("Curve depth for this segment. 0 = straight line.\nHigher values = deeper bend. The curve bends perpendicular to the segment direction.\nEach segment can have its own curve depth independently.")]
    public float curveDepth = 0f;

    [Tooltip("Direction the curve bends in. Default is (0,1,0) = upward.\nTry (1,0,0) for sideways, or any direction — will be projected perpendicular to segment.\nIf (0,0,0), an automatic perpendicular direction is chosen.")]
    public Vector3 curveDirection = Vector3.up;

    [Tooltip("Scale applied to the intermediate waypoints of this segment (X=width, Y=height, Z=depth of trigger zone).")]
    public Vector3 segmentWaypointScale = new Vector3(3f, 3f, 1f);

    [Header("Debug")]
    [Tooltip("Color of the gizmo for this control point in the Scene view.")]
    public Color gizmoColor = Color.cyan;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 0.8f);
        Gizmos.DrawSphere(transform.position, 0.25f);

        // Label with name (editor only)
#if UNITY_EDITOR
        UnityEditor.Handles.color = gizmoColor;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, name);
#endif
    }
}
