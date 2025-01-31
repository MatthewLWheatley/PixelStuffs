using UnityEngine;

/// <summary>
/// A small debugging component that stores and optionally visualizes
/// the control points and computed data for one road segment.
/// </summary>
public class SegmentDebugInfo : MonoBehaviour
{
    [Header("Segment Index")]
    [SerializeField] private int segmentIndex;

    [Header("Catmull/Bezier Control Points")]
    [SerializeField] private Vector3 p0;
    [SerializeField] private Vector3 p1;
    [SerializeField] private Vector3 p2;
    [SerializeField] private Vector3 p3;

    [Header("Computed Segment Data")]
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private Vector3 endPosition;
    [SerializeField] private Vector3 endTangent;

    // If you want to store random values used (like distance or turn angle):
    [Header("Random Parameters")]
    [SerializeField] private float distanceUsed;
    [SerializeField] private float turnAngleUsed;
    [SerializeField] private float heightOffsetUsed;

    /// <summary>
    /// Assign all the relevant data for later debugging.
    /// </summary>
    public void SetData(
        int index,
        Vector3 cp0, Vector3 cp1, Vector3 cp2, Vector3 cp3,
        Vector3 segStartPos, Vector3 segEndPos, Vector3 segEndTangent,
        float distUsed = 0f, float angleUsed = 0f, float heightUsed = 0f
    )
    {
        segmentIndex = index;
        p0 = cp0;
        p1 = cp1;
        p2 = cp2;
        p3 = cp3;

        startPosition = segStartPos;
        endPosition = segEndPos;
        endTangent = segEndTangent;

        distanceUsed = distUsed;
        turnAngleUsed = angleUsed;
        heightOffsetUsed = heightUsed;
    }

    /// <summary>
    /// Optional: draw gizmos in the scene view to visualize the control points.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;

        // Draw small spheres at the control points
        Gizmos.DrawSphere(p0, 0.3f);
        Gizmos.DrawSphere(p1, 0.3f);
        Gizmos.DrawSphere(p2, 0.3f);
        Gizmos.DrawSphere(p3, 0.3f);

        // Draw lines to visualize them
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);

        // Also draw from startPosition to endPosition to see the segment span
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPosition, endPosition);
    }
}
