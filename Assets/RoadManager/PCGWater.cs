using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PCGWATER : MonoBehaviour
{
    [Header("Water Settings")]
    public int gridSizeX = 20;
    public int gridSizeZ = 20;
    public float gridRes = 100;
    public float gridSpacing = 1f;
    public float smoothness = 1f;
    public Vector2 wind;
    public float windSpeed;
    public Material material;

    [Header("Perlin Noise Wave Settings")]
    public float waveSpeed = 1f;
    public float waveHeight = 1f;
    public float noiseScale = 0.2f;

    private Mesh mesh;
    private Vector3[] vertices;
    private Vector3[] baseVertices; // Store original positions


    void Start()
    {
        GenerateWaterMesh();
        //material.SetFloat("GridWidth", gridSizeX / gridRes);
        //material.SetFloat("GridHeight", gridSizeZ / gridRes);
        this.GetComponent<MeshRenderer>().material = material;
    }

    void Update()
    {
        AnimateWaves();
    }

    void GenerateWaterMesh()
    {
        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        vertices = new Vector3[(gridSizeX + 1) * (gridSizeZ + 1)];
        Vector2[] uvs = new Vector2[vertices.Length];
        baseVertices = new Vector3[vertices.Length];

        int[] triangles = new int[gridSizeX * gridSizeZ * 6];

        for (int z = 0; z <= gridSizeZ; z++)
        {
            for (int x = 0; x <= gridSizeX; x++)
            {
                int index = z * (gridSizeX + 1) + x;
                vertices[index] = new Vector3(x * gridSpacing, 0, z * gridSpacing);
                baseVertices[index] = vertices[index]; // Store base position
                uvs[index] = new Vector2((float)x / gridSizeX, (float)z / gridSizeZ);
            }
        }

        int triIndex = 0;
        for (int z = 0; z < gridSizeZ; z++)
        {
            for (int x = 0; x < gridSizeX; x++)
            {
                int bottomLeft = z * (gridSizeX + 1) + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + (gridSizeX + 1);
                int topRight = topLeft + 1;

                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomRight;

                triangles[triIndex++] = bottomRight;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = topRight;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    void AnimateWaves()
    {
        if (vertices == null) return;

        float gridWidth = (float)gridSizeX / gridRes;
        float gridHeight = (float)gridSizeZ / gridRes;

        // Use material instead of sharedMaterial
        Material mat = GetComponent<MeshRenderer>().material;
        mat.SetFloat("_GridWidth", gridWidth);
        mat.SetFloat("_GridHeight", gridHeight);
        mat.SetFloat("_waveSpeed", waveSpeed);
        mat.SetFloat("_waveHeight", waveHeight);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_dx", wind.x);
        mat.SetFloat("_dy", wind.y);
        mat.SetFloat("_WindSpeed", windSpeed);
        return;
        if (vertices == null) return;

        Vector3[] modifiedVertices = new Vector3[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = baseVertices[i]; // Use original positions
            float wave = Mathf.PerlinNoise(v.x * noiseScale + Time.time * waveSpeed,
                                           v.z * noiseScale + Time.time * waveSpeed)
                         * waveHeight;
            modifiedVertices[i] = new Vector3(v.x, wave, v.z);
        }

        mesh.vertices = modifiedVertices;
        mesh.RecalculateNormals();
    }
}
