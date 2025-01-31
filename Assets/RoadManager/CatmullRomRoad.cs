using UnityEngine;
using System.Collections.Generic;




public class RoadGeneratorCatmull : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;

    [Header("Road Dimensions")]
    [Tooltip("Width of the road surface.")]
    public float roadWidth = 5f;

    [Tooltip("Number of subdivisions (vertical cuts) for each road segment mesh.")]
    public int subdivisions = 20;

    [Header("Segment Control")]
    [Tooltip("How many segments to keep in front of the player at all times.")]
    public int segmentsAhead = 5;

    [Tooltip("Minimum random distance between new control points.")]
    public float minSegmentDistance = 25f;

    [Tooltip("Maximum random distance between new control points.")]
    public float maxSegmentDistance = 35f;

    [Header("Curve Randomization")]
    [Tooltip("Maximum angle (in degrees) to turn left/right when generating the next control point.")]
    public float maxTurnAngle = 30f;

    [Tooltip("Maximum amount the road can move up/down from its last y-position (before clamping).")]
    public float maxHeightVariation = 2f;

    [Header("Global Height Clamp")]
    [Tooltip("Minimum global Y position for control points.")]
    public float minGlobalHeight = -5f;

    [Tooltip("Maximum global Y position for control points.")]
    public float maxGlobalHeight = 10f;

    // The list of control points for Catmull-Rom
    private List<Vector3> controlPoints = new List<Vector3>();

    // Keep track of generated road segments
    private List<SegmentData> activeSegments = new List<SegmentData>();

    // Which spline segment index is next to generate
    private int currentSegmentIndex = 1;

    void Start()
    {
        // Initialize 4 control points so we can form the first segments.
        // Here, we start them in a straight line. Adjust to your preference.
        controlPoints.Add(new Vector3(0, 0, -0.3f));
        controlPoints.Add(new Vector3(0, 0, -0.2f));
        controlPoints.Add(new Vector3(0, 0, -0.1f));
        controlPoints.Add(new Vector3(0, 0, -0.0f));

        // Generate a few segments so there is road in front from the start
        for (int i = 0; i < segmentsAhead; i++)
        {
            CreateNextSegment();
        }
    }

    void Update()
    {
        // If the last segment end is not too far ahead, create another segment
        SegmentData lastSeg = activeSegments[activeSegments.Count - 1];
        float distToLastEnd = Vector3.Distance(playerTransform.position, lastSeg.endPosition);
        // You can tweak this threshold to something bigger or smaller:
        if (distToLastEnd < 2f * maxSegmentDistance)
        {
            CreateNextSegment();
        }

        // Optionally remove segments that are far behind the player
        SegmentData firstSeg = activeSegments[0];
        float distBehind = Vector3.Distance(playerTransform.position, firstSeg.endPosition);
        if (distBehind > 2f * maxSegmentDistance)
        {
            Destroy(firstSeg.segmentObject);
            activeSegments.RemoveAt(0);
        }
    }

    /// <summary>
    /// Creates the next road segment by building a Catmull-Rom segment
    /// from controlPoints[currentSegmentIndex] to controlPoints[currentSegmentIndex+1].
    /// p0..p3 are used for the catmull calculation.
    /// If we don't have enough points, we call AddControlPoint().
    /// </summary>
    private void CreateNextSegment()
    {
        RoadRandomData rand = new RoadRandomData(0.0f, 0.0f, 0.0f);
        // Ensure we have p0, p1, p2, p3 available
        while (currentSegmentIndex + 2 >= controlPoints.Count)
        {
            rand = AddControlPoint();
        }

        int i0 = currentSegmentIndex - 1;
        int i1 = currentSegmentIndex;
        int i2 = currentSegmentIndex + 1;
        int i3 = currentSegmentIndex + 2;

        Vector3 p0 = controlPoints[i0];
        Vector3 p1 = controlPoints[i1];
        Vector3 p2 = controlPoints[i2];
        Vector3 p3 = controlPoints[i3];

        // Build mesh for segment p1->p2
        Mesh roadMesh = BuildRoadMeshFromCatmull(p0, p1, p2, p3, roadWidth, subdivisions);

        // Create segment GameObject
        GameObject segmentObj = new GameObject("RoadSegment_CRM_" + currentSegmentIndex);
        segmentObj.transform.SetParent(this.transform, worldPositionStays: false);

        // Add MeshFilter + Renderer
        MeshFilter mf = segmentObj.AddComponent<MeshFilter>();
        MeshRenderer mr = segmentObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(Shader.Find("Standard"));
        mf.mesh = roadMesh;

        // Optional collider
        MeshCollider mc = segmentObj.AddComponent<MeshCollider>();
        mc.sharedMesh = roadMesh;

        // Compute end position/tangent from t=1
        Vector3 endPos = CatmullRomPos(p0, p1, p2, p3, 1f);
        Vector3 endTangent = CatmullRomTangent(p0, p1, p2, p3, 1f).normalized;

        // Store segment info
        SegmentData segData = new SegmentData()
        {
            segmentObject = segmentObj,
            startPosition = CatmullRomPos(p0, p1, p2, p3, 0f),
            endPosition = endPos,
            endTangent = endTangent
        };

        SegmentDebugInfo debugInfo = segmentObj.AddComponent<SegmentDebugInfo>();
        debugInfo.SetData(
            currentSegmentIndex,
            p0, p1, p2, p3,
            segData.startPosition,
            segData.endPosition,
            segData.endTangent,
            rand.distanceUsed,
            rand.turnAngleUsed,
            rand.heightOffsetUsed
        );

        activeSegments.Add(segData);

        currentSegmentIndex++;
    }

    /// <summary>
    /// Adds a new control point at the end of the list,
    /// continuing from the last direction but with random variation.
    /// 
    /// We clamp the final Y to [minGlobalHeight, maxGlobalHeight].
    /// </summary>
    private RoadRandomData AddControlPoint()
    {
        // The last two points in the list
        Vector3 last = controlPoints[controlPoints.Count - 1];
        Vector3 prev = controlPoints[controlPoints.Count - 2];

        // direction from prev->last
        Vector3 dir = (last - prev).normalized;

        // Random distance within [minSegmentDistance, maxSegmentDistance]
        float randomDist = Random.Range(minSegmentDistance, maxSegmentDistance);

        // Random rotation (left/right) around Y
        float turnAngle = Random.Range(-maxTurnAngle, maxTurnAngle);
        Quaternion rot = Quaternion.Euler(0, turnAngle, 0);
        Vector3 offsetDirection = rot * dir;

        // Build the new point's XZ
        Vector3 newPoint = last + offsetDirection * randomDist;

        // Adjust Y with a random offset, then clamp
        float newY = last.y + Random.Range(-maxHeightVariation, maxHeightVariation);
        newY = Mathf.Clamp(newY, minGlobalHeight, maxGlobalHeight);

        newPoint.y = newY;

        controlPoints.Add(newPoint);

        RoadRandomData rand = new RoadRandomData(randomDist, turnAngle, newY - last.y);
        return rand;
    }

    /// <summary>
    /// Builds a mesh along the Catmull-Rom curve from p1->p2, using p0..p3 for shape.
    /// Subdiv = how many segments to subdivide for the mesh.
    /// </summary>
    private Mesh BuildRoadMeshFromCatmull(
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        float width, int subdiv)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float step = 1f / subdiv;

        for (int i = 0; i <= subdiv; i++)
        {
            float t = i * step;
            Vector3 centerPos = CatmullRomPos(p0, p1, p2, p3, t);
            Vector3 tangent = CatmullRomTangent(p0, p1, p2, p3, t).normalized;

            // A basic left vector in horizontal plane (assuming mostly flat roads).
            Vector3 left = Vector3.Cross(Vector3.up, tangent).normalized * (width * 0.5f);

            Vector3 leftPos = centerPos + left;
            Vector3 rightPos = centerPos - left;

            // Add vertices
            vertices.Add(leftPos);
            vertices.Add(rightPos);

            // UV
            uvs.Add(new Vector2(0, t));
            uvs.Add(new Vector2(1, t));

            // Triangles
            if (i > 0)
            {
                int baseIndex = vertices.Count - 2;
                int prevBaseIndex = baseIndex - 2;

                // tri 1
                triangles.Add(prevBaseIndex);
                triangles.Add(prevBaseIndex + 1);
                triangles.Add(baseIndex);

                // tri 2
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex);
                triangles.Add(prevBaseIndex + 1);
            }
        }

        // Construct mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        // Recalculate
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Standard Catmull-Rom position formula for t in [0..1] from p1->p2, using p0..p3.
    /// </summary>
    private Vector3 CatmullRomPos(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    /// <summary>
    /// Catmull-Rom derivative for tangent, used to orient the mesh (0..1).
    /// </summary>
    private Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * (
            (-p0 + p2) +
            2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
            3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
        );
    }
}

[System.Serializable]
public struct RoadRandomData
{
    public float distanceUsed;
    public float turnAngleUsed;
    public float heightOffsetUsed;

    public RoadRandomData(float _distanceUsed, float _turnAngleUsed, float _heightOffsetUsed)
    {
        this.distanceUsed = _distanceUsed;
        this.turnAngleUsed = _turnAngleUsed;
        this.heightOffsetUsed = _heightOffsetUsed;
    }
}