using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;



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

    [Tooltip("Material for the road.")]
    public Material roadMaterial;

    [Header("River Settings")]
    [Tooltip("Number of subdivisions for the river mesh.")]
    public int riverSubdivisions = 50;
    [Tooltip("Number of subHozdivisions for the river mesh.")]
    public int riverHozSubdivisions = 50;

    [Tooltip("Material for the river surface.")]
    public Material riverMaterial;

    [Tooltip("Width of the river.")]
    public float riverWidth = 10f;

    public float riverRoadDistance = 0.0f;

    // The list of control points for Catmull-Rom
    private List<Vector3> controlPoints = new List<Vector3>();

    // Keep track of generated road segments
    public List<SegmentData> activeSegments = new List<SegmentData>();
    private List<GameObject> activeRiverSegments = new List<GameObject>();

    // Which spline segment index is next to generate
    private int currentSegmentIndex = 1;

    private Vector3[] lastRiverRowVertices = null;

    public Vector2 globalWindDirection = new Vector2(1.6f, 1.63f); // Default wind direction
    public float windLerpSpeed = 0.1f; // Controls smooth transition speed
    public Vector2 targetWindDirection;
    public GameObject closestSegment = null;

    void Start()
    {
        // Initialize 4 control points so we can form the first segments.
        // Here, we start them in a straight line. Adjust to your preference.
        controlPoints.Add(new Vector3(0, 0, -60f));
        controlPoints.Add(new Vector3(0, 0, 0.0f));
        controlPoints.Add(new Vector3(0, 0, 150f));
        controlPoints.Add(new Vector3(0, 0, 300.0f));

        // Generate a few segments so there is road in front from the start
        for (int i = 0; i < segmentsAhead; i++)
        {
            CreateNextSegment();
        }
    }

    void Update()
    {
        UpdateWindDirection();

        // Continue existing segment handling
        if (activeSegments.Count > 0)
        {
            SegmentData lastSeg = activeSegments[activeSegments.Count - 1];
            float distToLastEnd = Vector3.Distance(playerTransform.position, lastSeg.endPosition);

            if (distToLastEnd < 2f * maxSegmentDistance)
            {
                CreateNextSegment();
            }

            SegmentData firstSeg = activeSegments[0];
            float distBehind = Vector3.Distance(playerTransform.position, firstSeg.endPosition);
            if (distBehind > 2f * maxSegmentDistance)
            {
                if (activeSegments.Count > 0)
                {
                    Destroy(firstSeg.segmentObject);
                    activeSegments.RemoveAt(0);
                }
                if (activeRiverSegments.Count > 0)
                {
                    Destroy(activeRiverSegments[0]);
                    activeRiverSegments.RemoveAt(0);
                }
            }
        }
    }

    void UpdateWindDirection()
    {
        if (controlPoints.Count < 2) return; // Need at least 2 points for a valid direction

        // Find the closest control point index
        int closestIndex = FindClosestControlPoint(playerTransform.position);

        // Use the next control point to determine wind direction
        int nextIndex = Mathf.Min(closestIndex + 1, controlPoints.Count - 1);

        Vector3 closestPoint = controlPoints[closestIndex];
        Vector3 nextPoint = controlPoints[nextIndex];

        // Compute forward direction (wind direction should follow the river flow)
        Vector3 windDir = (nextPoint - closestPoint).normalized;

        // Project onto XZ plane to avoid vertical wind movement
        targetWindDirection = new Vector2(windDir.x, windDir.z).normalized;

        // Smoothly interpolate wind direction
        globalWindDirection = Vector2.Lerp(globalWindDirection, targetWindDirection, windLerpSpeed * Time.deltaTime);

        // Debugging
        Debug.Log($"Closest CP: {closestPoint}, Next CP: {nextPoint}, Target Wind: {targetWindDirection}, Global Wind: {globalWindDirection}");

        // Apply wind direction to all river segments
        foreach (var segment in activeRiverSegments)
        {
            Renderer rend = segment.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                rend.sharedMaterial.SetFloat("_dx", globalWindDirection.x);
                rend.sharedMaterial.SetFloat("_dy", globalWindDirection.y);
            }
        }
    }

    int FindClosestControlPoint(Vector3 playerPos)
    {
        int closestIndex = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < controlPoints.Count; i++)
        {
            float dist = Vector3.Distance(playerPos, controlPoints[i]);
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private Mesh BuildRiverMesh(List<Vector3> controlPoints, float width, int lengthSubdiv, int widthSubdiv, Vector3[] lastRowVertices)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        float stepLength = 1f / lengthSubdiv;
        float stepWidth = 1f / widthSubdiv;

        Vector3[] firstRow = lastRowVertices; // Use last row of previous segment if available

        for (int i = 0; i < controlPoints.Count; i++)
        {
            Vector3 centerPos = controlPoints[i];

            Vector3 tangent = (i < controlPoints.Count - 1) ?
                (controlPoints[i + 1] - controlPoints[i]).normalized :
                (controlPoints[i] - controlPoints[i - 1]).normalized;

            Vector3 stableLeft = -Vector3.Cross(Vector3.up, tangent).normalized;

            Vector3[] currentRow = new Vector3[widthSubdiv + 1];

            for (int j = 0; j <= widthSubdiv; j++)
            {
                float widthFactor = (float)j / widthSubdiv - 0.5f;
                Vector3 vertexPos = centerPos + stableLeft * (widthFactor * width);
                vertexPos.y -= 0.5f; // Lower the river slightly

                if (i == 0 && firstRow != null)
                {
                    vertexPos = firstRow[j]; // Use previous segment's last row
                }

                vertices.Add(vertexPos);
                uvs.Add(new Vector2((float)j / widthSubdiv, (float)i / lengthSubdiv));

                currentRow[j] = vertexPos;

                if (i > 0 && j > 0)
                {
                    int baseIndex = i * (widthSubdiv + 1) + j;
                    int prevBaseIndex = (i - 1) * (widthSubdiv + 1) + j;

                    triangles.Add(prevBaseIndex - 1);
                    triangles.Add(prevBaseIndex);
                    triangles.Add(baseIndex - 1);

                    triangles.Add(baseIndex - 1);
                    triangles.Add(prevBaseIndex);
                    triangles.Add(baseIndex);
                }
            }

            firstRow = currentRow; // Update last row to match the next segment
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void GenerateParallelRoad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        List<Vector3> parallelPoints = new List<Vector3>();
        int subdiv = riverSubdivisions;
        float step = 1f / subdiv;

        // Build the full list of positions along the river
        for (int i = 0; i <= subdiv; i++)
        {
            float t = i * step;
            Vector3 centerPos = CatmullRomPos(p0, p1, p2, p3, t);
            Vector3 tangent = CatmullRomTangent(p0, p1, p2, p3, t).normalized;
            Vector3 nextTangent = (i < subdiv) ? CatmullRomTangent(p0, p1, p2, p3, t + step).normalized : tangent;
            Vector3 avgTangent = ((tangent + nextTangent) * 0.5f).normalized;
            Vector3 flatTangent = new Vector3(avgTangent.x, 0, avgTangent.z);
            if (flatTangent.magnitude > 0.01f)
                flatTangent.Normalize();
            Vector3 stableLeft = -Vector3.Cross(Vector3.up, flatTangent).normalized;
            float riverOffset = ((riverWidth + roadWidth) * 0.5f + riverRoadDistance);
            Vector3 parallelPos = centerPos + stableLeft * riverOffset;
            parallelPos.y = -1.0f; // Lower the river slightly
            parallelPoints.Add(parallelPos);
        }

        // Now, split the parallelPoints into smaller chunks.
        int maxRowsPerChunk = 50; // Change as needed or make this a public parameter.
        int totalRows = parallelPoints.Count;
        int startIndex = 0;

        while (startIndex < totalRows - 1)
        {
            // Determine how many rows to include in this chunk.
            int chunkRowCount = Mathf.Min(maxRowsPerChunk, totalRows - startIndex);
            if (chunkRowCount < 2)
                break; // Need at least 2 rows to form a mesh.

            // Extract a chunk from the full list. We use GetRange to get a subset.
            List<Vector3> chunkPoints = parallelPoints.GetRange(startIndex, chunkRowCount);

            // Build a mesh for this chunk.
            // Note: We pass in the last row vertices only for the very first chunk
            // if you want to stitch it to a previous segment.
            Mesh chunkMesh = BuildRiverMesh(chunkPoints, riverWidth, chunkPoints.Count - 1, riverHozSubdivisions,
                                              (startIndex == 0) ? lastRiverRowVertices : null);
            if (chunkMesh == null || chunkMesh.vertexCount == 0)
            {
                Debug.LogError("River mesh generation failed for a chunk!");
                break;
            }

            // Create a GameObject for this chunk.
            GameObject chunkObj = new GameObject("RiverSegment_Chunk");
            chunkObj.transform.SetParent(this.transform, false);
            MeshFilter mf = chunkObj.AddComponent<MeshFilter>();
            mf.mesh = chunkMesh;
            MeshRenderer mr = chunkObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = riverMaterial ?? new Material(Shader.Find("Standard"));
            MeshCollider mc = chunkObj.AddComponent<MeshCollider>();
            mc.sharedMesh = chunkMesh;
            activeRiverSegments.Add(chunkObj);

            // Store the last row of vertices for stitching, if needed.
            lastRiverRowVertices = ExtractLastRowVertices(chunkMesh);

            // Advance the start index.
            // We overlap the last row to ensure continuity between chunks.
            if (startIndex + chunkRowCount < totalRows)
                startIndex += chunkRowCount - 1;
            else
                break;
        }
    }

    private Vector3[] ExtractLastRowVertices(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount == 0)
            return null;

        int widthSubdiv = riverHozSubdivisions; // Must match width subdivisions used in BuildRiverMesh
        Vector3[] lastRow = new Vector3[widthSubdiv + 1];

        // The last row is stored in the last (widthSubdiv + 1) indices
        for (int i = 0; i <= widthSubdiv; i++)
        {
            lastRow[i] = mesh.vertices[mesh.vertices.Length - (widthSubdiv + 1) + i];
        }

        return lastRow;
    }

    private void CreateNextSegment()
    {
        RoadRandomData rand = new RoadRandomData(0.0f, 0.0f, 0.0f);

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

        Mesh roadMesh = BuildRoadMeshFromCatmull(p0, p1, p2, p3, roadWidth, subdivisions);

        GameObject segmentObj = new GameObject("RoadSegment_CRM_" + currentSegmentIndex);
        segmentObj.transform.SetParent(this.transform, worldPositionStays: false);


        MeshFilter mf = segmentObj.AddComponent<MeshFilter>();
        MeshRenderer mr = segmentObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = roadMaterial;
        mf.mesh = roadMesh;

        MeshCollider mc = segmentObj.AddComponent<MeshCollider>();
        mc.sharedMesh = roadMesh;

        Vector3 endPos = CatmullRomPos(p0, p1, p2, p3, 1f);
        Vector3 endTangent = CatmullRomTangent(p0, p1, p2, p3, 1f).normalized;

        SegmentData segData = new SegmentData()
        {
            segmentObject = segmentObj,
            startPosition = CatmullRomPos(p0, p1, p2, p3, 0f),
            endPosition = endPos,
            endTangent = endTangent
        };

        SegmentDebugInfo debugInfo = segmentObj.AddComponent<SegmentDebugInfo>();
        debugInfo.SetData(
            currentSegmentIndex, p0, p1, p2, p3,
            segData.startPosition, segData.endPosition, segData.endTangent,
            rand.distanceUsed, rand.turnAngleUsed, rand.heightOffsetUsed
        );

        activeSegments.Add(segData);

        // Generate parallel road on the left
        GenerateParallelRoad(p0, p1, p2, p3);

        currentSegmentIndex++;
    }

    private RoadRandomData AddControlPoint()
    {
        // The last two points in the list
        Vector3 last = controlPoints[controlPoints.Count - 1];
        Vector3 prev = controlPoints[controlPoints.Count - 2];

        // direction from prev->last
        Vector3 dir = (last - prev).normalized;

        // Random distance within [minSegmentDistance, maxSegmentDistance]
        float randomDist = Random.Range(minSegmentDistance, maxSegmentDistance);
        //riverSubdivisions = (int)randomDist;

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