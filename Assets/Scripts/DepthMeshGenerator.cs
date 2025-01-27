using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class DepthMeshGenerator : MonoBehaviour {
    [Header("Required Components")]
    public ComputeShader computeShader;
    public Material meshMaterial;

    [Header("Mesh Parameters")]
    [Tooltip("Maximum allowed edge length between vertices")]
    public float maxEdgeLength = 0.1f;
    [Tooltip("Maximum angle between adjacent triangles in degrees")]
    [Range(0f, 180f)]
    public float maxSurfaceAngle = 60f;

    private ComputeBuffer vertexBuffer;
    private ComputeBuffer uvBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer triangleCounterBuffer;

    private int width, height;
    private Mesh mesh;
    private int kernel;
    private bool isInitialized = false;

    void Awake() {
        if (meshMaterial == null) {
            meshMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            meshMaterial.color = Color.white;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null) {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }
            renderer.material = meshMaterial;
        }
    }

    public void Initialize(int width, int height) {
        if (computeShader == null) {
            Debug.LogError("Compute shader is not assigned!");
            return;
        }

        this.width = width;
        this.height = height;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) {
            renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = meshMaterial;
        }

        if (mesh == null) {
            mesh = new Mesh();
            mesh.MarkDynamic();
            meshFilter.mesh = mesh;
        }

        int vertexCount = width * height;
        int maxTriangleCount = (width - 1) * (height - 1) * 2;
        int maxIndexCount = maxTriangleCount * 3;

        vertexBuffer?.Release();
        uvBuffer?.Release();
        triangleBuffer?.Release();
        triangleCounterBuffer?.Release();

        vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        uvBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);
        triangleBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
        triangleCounterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        kernel = computeShader.FindKernel("GenerateMesh");

        computeShader.SetBuffer(kernel, "vertices", vertexBuffer);
        computeShader.SetBuffer(kernel, "uvs", uvBuffer);
        computeShader.SetBuffer(kernel, "triangles", triangleBuffer);
        computeShader.SetBuffer(kernel, "triangleCounter", triangleCounterBuffer);

        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetFloat("maxEdgeLength", maxEdgeLength);
        computeShader.SetFloat("maxSurfaceAngle", maxSurfaceAngle);

        isInitialized = true;
    }

    public void UpdateMeshFromPointCloud(float[] pointData) {
        if (!isInitialized) {
            Debug.LogError("PointCloudMeshGenerator not initialized! Call Initialize() first.");
            return;
        }

        if (pointData.Length != width * height * 4) {
            Debug.LogError($"Point cloud data length ({pointData.Length}) doesn't match initialized dimensions ({width * height * 4})");
            return;
        }

        try {
            // First pass: identify valid vertices and create mapping
            List<Vector3> validVertices = new List<Vector3>();
            List<Vector2> validUVs = new List<Vector2>();
            Dictionary<int, int> oldToNewIndex = new Dictionary<int, int>();

            for (int i = 0; i < width * height; i++) {
                int baseIndex = i * 4;
                float x = pointData[baseIndex];
                float y = pointData[baseIndex + 1];
                float z = pointData[baseIndex + 2];

                if (!float.IsNaN(x) && !float.IsNaN(y) && !float.IsNaN(z)) {
                    oldToNewIndex[i] = validVertices.Count;
                    validVertices.Add(new Vector3(x, y, z));
                    validUVs.Add(new Vector2((i % width) / (float)width, (i / width) / (float)height));
                }
            }

            //Debug.Log($"Valid vertices: {validVertices.Count} out of {width * height}");

            if (validVertices.Count == 0) {
                Debug.LogWarning("No valid vertices found in point cloud data");
                return;
            }

            // Update compute shader with valid vertices
            vertexBuffer.SetData(validVertices.ToArray());
            uvBuffer.SetData(validUVs.ToArray());

            // Reset triangle counter
            int[] counterReset = new int[] { 0 };
            triangleCounterBuffer.SetData(counterReset);

            // Dispatch compute shader
            computeShader.Dispatch(kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);

            // Get triangle count
            int[] triangleCount = new int[1];
            triangleCounterBuffer.GetData(triangleCount);

            if (triangleCount[0] > 0) {
                // Get triangles
                int[] triangles = new int[triangleCount[0]];
                triangleBuffer.GetData(triangles, 0, 0, triangleCount[0]);

                // Validate triangle indices
                bool hasInvalidIndices = false;
                List<int> validTriangles = new List<int>();

                for (int i = 0; i < triangles.Length; i += 3) {
                    // Check if all three indices of this triangle are valid
                    if (triangles[i] < validVertices.Count &&
                        triangles[i + 1] < validVertices.Count &&
                        triangles[i + 2] < validVertices.Count &&
                        triangles[i] >= 0 &&
                        triangles[i + 1] >= 0 &&
                        triangles[i + 2] >= 0) {
                        validTriangles.Add(triangles[i]);
                        validTriangles.Add(triangles[i + 1]);
                        validTriangles.Add(triangles[i + 2]);
                    } else {
                        hasInvalidIndices = true;
                    }
                }

                if (hasInvalidIndices) {
                    Debug.LogWarning($"Filtered out some invalid triangle indices. Valid triangles: {validTriangles.Count / 3} out of {triangles.Length / 3}");
                }

                if (validTriangles.Count > 0) {
                    // Keep existing mesh data if possible
                    if (mesh.vertexCount != validVertices.Count) {
                        mesh.Clear();
                        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        mesh.vertices = validVertices.ToArray();
                        mesh.uv = validUVs.ToArray();
                    }

                    try {
                        mesh.triangles = validTriangles.ToArray();
                        mesh.RecalculateNormals();
                        mesh.RecalculateBounds();
                    } catch (System.Exception e) {
                        Debug.LogError($"Error setting mesh triangles: {e.Message}");
                        // Keep the mesh but clear triangles
                        mesh.triangles = new int[0];
                    }
                } else {
                    Debug.LogWarning("No valid triangles after index validation");
                    // Keep the mesh but clear triangles
                    mesh.triangles = new int[0];
                }
            } else {
                Debug.LogWarning("No triangles generated from compute shader");
                // Keep the mesh but clear triangles
                mesh.triangles = new int[0];
            }
        } catch (System.Exception e) {
            Debug.LogError($"Error updating mesh: {e.Message}\n{e.StackTrace}");
        }
    }

    private void OnDestroy() {
        vertexBuffer?.Release();
        uvBuffer?.Release();
        triangleBuffer?.Release();
        triangleCounterBuffer?.Release();

        if (mesh != null) {
            Destroy(mesh);
        }
    }
}