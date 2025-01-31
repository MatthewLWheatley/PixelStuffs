using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach this to an empty GameObject. This script generates road segments
/// ahead of the player and removes old ones.
/// </summary>
public class RoadGenerator : MonoBehaviour
{
    // Reference to your player (the car)
    public Transform playerTransform;

    // Number of segments to keep in front of player
    public int segmentsAhead = 5;

    // Segment length in "curve parameter" terms. 
    // Typically, we'll treat each segment as a Bezier or spline arc from t=0..1.
    // You can interpret "length" by controlling how you place the control points.
    public float segmentLength = 30f;

    // Width of the road
    public float roadWidth = 5f;

    // How many subdivisions per segment to generate a smooth mesh
    public int subdivisions = 20;

    // How far ahead in world space we begin the next segment
    // Usually 1.0 means we start the new segment exactly where old ended
    // If you want some overlap or gap, you can adjust
    public float continuityOffset = 1.0f;

    // Keep track of the road segments we’ve generated
    // Each segment has a reference to its end position/tangent so we can align the next segment
    private List<SegmentData> _activeSegments = new List<SegmentData>();

    // We'll keep track of how far we've generated along the "road path" for random generation
    private float _distanceSoFar = 0f;

    // Awake or Start
    private void Start()
    {
        // Generate initial segments
        for (int i = 0; i < segmentsAhead; i++)
        {
            CreateNextSegment();
        }
    }

    private void Update()
    {
        // Always ensure we have the correct number of segments in front of the player
        // Check if we are close to the end of the second-to-last segment
        // or you can do a simpler check: if the last segment is within certain distance from the player
        SegmentData lastSegment = _activeSegments[_activeSegments.Count - 1];
        float distToLastSegmentEnd = Vector3.Distance(playerTransform.position, lastSegment.endPosition);

        if (distToLastSegmentEnd < 2f * segmentLength)
        {
            // Create new segment
            CreateNextSegment();
        }

        // Optionally remove segments that are far behind the player
        // E.g. if the first segment is behind the player by more than some threshold
        SegmentData firstSegment = _activeSegments[0];
        float distFromPlayerToFirstSegmentEnd = Vector3.Distance(playerTransform.position, firstSegment.endPosition);
        if (distFromPlayerToFirstSegmentEnd > 2f * segmentLength)
        {
            // Remove that segment
            Destroy(firstSegment.segmentObject);
            _activeSegments.RemoveAt(0);
        }
    }

    /// <summary>
    /// Creates the next road segment in front of the existing ones.
    /// </summary>
    private void CreateNextSegment()
    {
        // If we have no segments yet, just start at origin with some forward direction
        Vector3 startPos = Vector3.zero;
        Vector3 startForward = Vector3.forward;

        if (_activeSegments.Count > 0)
        {
            // Align the new segment to the end of the last segment
            SegmentData lastSegment = _activeSegments[_activeSegments.Count - 1];
            startPos = lastSegment.endPosition;
            startForward = lastSegment.endTangent.normalized;
        }

        // Option 1: Randomly define the next segment’s control points
        // so that it continues in the same direction with some random variation
        // Or Option 2: You can define them systematically.

        // We'll define 4 control points (p0..p3) for a cubic Bezier
        // p0 is the start, p3 is the end, p1..p2 define the shape.

        Vector3 p0 = startPos;
        Vector3 p1 = startPos + startForward * (segmentLength * 0.3f)
                               + Random.insideUnitSphere * 5f;
        // Keep random in horizontal plane
        p1.y = p0.y;

        // define p3 end in same approximate direction
        Vector3 endPos = startPos + startForward * segmentLength;
        endPos += new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
        Vector3 p3 = endPos;

        // p2 is somewhere between p1 and p3
        Vector3 p2 = p3 - startForward * (segmentLength * 0.3f)
                               + Random.insideUnitSphere * 5f;
        p2.y = p0.y;

        // Build the actual mesh object
        GameObject segmentObj = new GameObject("RoadSegment");
        segmentObj.transform.SetParent(this.transform);

        // Generate mesh
        MeshFilter mf = segmentObj.AddComponent<MeshFilter>();
        MeshRenderer mr = segmentObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Standard"));

        Mesh roadMesh = BuildRoadMeshFromBezier(p0, p1, p2, p3, roadWidth, subdivisions);
        mf.mesh = roadMesh;

        // Store the last point/tangent for next alignment
        Vector3 endTangent = BezierTangent(p0, p1, p2, p3, 1f).normalized;

        // Position the segment at p0 so local space matches the curve’s first point
        // However, the mesh itself was generated in world space in this example,
        // so there's an assumption that p0 is near the origin or you offset accordingly.
        // Alternatively, you can generate your mesh in local coordinates, then transform
        // the segmentObj accordingly.
        segmentObj.transform.position = Vector3.zero;
        segmentObj.transform.rotation = Quaternion.identity;

        // Add a mesh collider if desired
        MeshCollider mc = segmentObj.AddComponent<MeshCollider>();
        mc.sharedMesh = roadMesh;

        // Save segment data
        SegmentData data = new SegmentData
        {
            segmentObject = segmentObj,
            startPosition = p0,
            endPosition = p3,
            endTangent = endTangent
        };
        _activeSegments.Add(data);
    }

    /// <summary>
    /// Constructs a road mesh given 4 control points for a Bezier curve and road width.
    /// The mesh is constructed in world coordinates in this example.
    /// </summary>
    private Mesh BuildRoadMeshFromBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float width, int subdiv)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float step = 1f / subdiv;
        float uvStep = 1f / subdiv;

        for (int i = 0; i <= subdiv; i++)
        {
            float t = i * step;
            Vector3 centerPos = BezierPoint(p0, p1, p2, p3, t);
            Vector3 tangent = BezierTangent(p0, p1, p2, p3, t).normalized;

            // A “road normal” is typically to the left or right in the x-z plane.
            // We can approximate by crossing with an up vector, or if your road is not on a slope,
            // you can just take a perpendicular in the horizontal plane:
            Vector3 left = Vector3.Cross(Vector3.up, tangent).normalized * (width * 0.5f);
            // For a sloped road, we might want to keep a more robust approach, e.g. cross with road's normal.

            Vector3 leftPos = centerPos + left;
            Vector3 rightPos = centerPos - left;

            // Add vertices
            vertices.Add(leftPos);
            vertices.Add(rightPos);

            // Basic UV mapping (u along width, v along length)
            // Left vertex:
            uvs.Add(new Vector2(0, t));
            // Right vertex:
            uvs.Add(new Vector2(1, t));

            // Triangles
            // Except for i=0, we make quads between i-1 and i
            if (i > 0)
            {
                // indices
                int baseIndex = vertices.Count - 2;
                int baseIndexPrev = baseIndex - 2;

                // tri 1
                triangles.Add(baseIndexPrev);
                triangles.Add(baseIndexPrev + 1);
                triangles.Add(baseIndex);

                // tri 2
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex);
                triangles.Add(baseIndexPrev + 1);
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        // Optionally recalculate normals and tangents
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Evaluate a cubic Bezier at parameter t
    /// </summary>
    private Vector3 BezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        // B(t) = u^3 * p0 + 3u^2 t p1 + 3u t^2 p2 + t^3 p3
        Vector3 p = uuu * p0;
        p += 3f * uu * t * p1;
        p += 3f * u * tt * p2;
        p += ttt * p3;

        return p;
    }

    /// <summary>
    /// Compute the tangent of a cubic Bezier at parameter t
    /// derivative of BezierPoint
    /// </summary>
    private Vector3 BezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // B'(t) = 3(1-t)^2 (p1 - p0) + 6(1-t)t (p2 - p1) + 3t^2 (p3 - p2)
        float u = 1f - t;
        Vector3 term1 = 3f * (u * u) * (p1 - p0);
        Vector3 term2 = 6f * u * t * (p2 - p1);
        Vector3 term3 = 3f * (t * t) * (p3 - p2);

        return term1 + term2 + term3;
    }
}

/// <summary>
/// Holds metadata about a road segment, including the object, the start/end positions, etc.
/// </summary>
[System.Serializable]
public class SegmentData
{
    public GameObject segmentObject;
    public Vector3 startPosition;
    public Vector3 endPosition;
    public Vector3 endTangent;
}
