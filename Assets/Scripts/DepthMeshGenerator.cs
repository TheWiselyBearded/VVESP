using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
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
    private ComputeBuffer colorBuffer;

    private int width, height;
    private Mesh mesh;
    private int kernel;
    private bool isInitialized = false;

    // Cached arrays to prevent reallocations
    private Vector3[] cachedVertices;
    private Vector2[] cachedUVs;
    private Color32[] cachedColors;
    private Vector3[] cachedColorFloat3;
    private int[] cachedTriangles;

    private ComputeBuffer normalBuffer;
    private ComputeBuffer validVertexBuffer;
    private Vector3[] cachedNormals;
    private int normalizeKernel;


    // Job struct for parallel vertex processing
    [BurstCompile]
    private struct ProcessVerticesJob : IJobParallelFor {
        [ReadOnly] public NativeArray<float> pointData;
        [ReadOnly] public NativeArray<byte> colorData;
        public NativeArray<Vector3> vertices;
        public NativeArray<Vector2> uvs;
        public NativeArray<Color32> colors;
        public NativeArray<int> validityMask;
        public int width;
        public int height;

        public void Execute(int index) {
            int baseIndex = index * 4;
            int colorIndex = index * 3;

            float x = pointData[baseIndex];
            float y = pointData[baseIndex + 1];
            float z = pointData[baseIndex + 2];

            if (!float.IsNaN(x) && !float.IsNaN(y) && !float.IsNaN(z)) {
                vertices[index] = new Vector3(x, y, z);
                uvs[index] = new Vector2((index % width) / (float)width, (index / width) / (float)height);
                colors[index] = new Color32(
                    colorData[colorIndex],
                    colorData[colorIndex + 1],
                    colorData[colorIndex + 2],
                    255
                );
                validityMask[index] = 1;
            } else {
                validityMask[index] = 0;
            }
        }
    }

    void Awake() {
        InitializeMaterial();
    }

    private void InitializeMaterial() {
        if (meshMaterial == null) {
            meshMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            meshMaterial.color = Color.white;
            meshMaterial.enableInstancing = true;
            meshMaterial.EnableKeyword("_VERTEX_COLORS");
            meshMaterial.SetFloat("_UseVertexColor", 1.0f);

            var renderer = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
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

        InitializeMeshComponents();
        InitializeBuffers();
        SetupComputeShader();

        isInitialized = true;
    }

    private void InitializeMeshComponents() {
        var meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();

        if (mesh == null) {
            mesh = new Mesh();
            mesh.MarkDynamic();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.mesh = mesh;
        }

        // Pre-allocate cached arrays
        int vertexCount = width * height;
        cachedVertices = new Vector3[vertexCount];
        cachedUVs = new Vector2[vertexCount];
        cachedColors = new Color32[vertexCount];
        cachedColorFloat3 = new Vector3[vertexCount];
        cachedTriangles = new int[(width - 1) * (height - 1) * 6]; // Maximum possible triangles
    }

    private void InitializeBuffers() {
        int vertexCount = width * height;
        int maxTriangleCount = (width - 1) * (height - 1) * 2;
        int maxIndexCount = maxTriangleCount * 3;

        ReleaseBuffers();

        vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        normalBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        uvBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);
        triangleBuffer = new ComputeBuffer(maxIndexCount, sizeof(int));
        triangleCounterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        colorBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        validVertexBuffer = new ComputeBuffer(vertexCount, sizeof(int));

        // Pre-allocate cached arrays
        cachedNormals = new Vector3[vertexCount];
    }

    private void SetupComputeShader() {
        kernel = computeShader.FindKernel("GenerateMesh");
        normalizeKernel = computeShader.FindKernel("NormalizeMesh");

        // Set buffers for main kernel
        computeShader.SetBuffer(kernel, "vertices", vertexBuffer);
        computeShader.SetBuffer(kernel, "normals", normalBuffer);
        computeShader.SetBuffer(kernel, "uvs", uvBuffer);
        computeShader.SetBuffer(kernel, "triangles", triangleBuffer);
        computeShader.SetBuffer(kernel, "triangleCounter", triangleCounterBuffer);
        computeShader.SetBuffer(kernel, "colors", colorBuffer);
        computeShader.SetBuffer(kernel, "validVertexMask", validVertexBuffer);

        // Set buffers for normalize kernel
        computeShader.SetBuffer(normalizeKernel, "normals", normalBuffer);
        computeShader.SetBuffer(normalizeKernel, "validVertexMask", validVertexBuffer);

        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetFloat("maxEdgeLength", maxEdgeLength);
        computeShader.SetFloat("maxSurfaceAngle", maxSurfaceAngle);
    }

    public void UpdateMeshFromPointCloud(float[] pointData, byte[] colorData) {
        if (!ValidateInput(pointData, colorData)) return;

        try {
            ProcessPointCloudData(pointData, colorData);
        } catch (System.Exception e) {
            Debug.LogError($"Error updating mesh: {e.Message}\n{e.StackTrace}");
        }
    }

    private bool ValidateInput(float[] pointData, byte[] colorData) {
        if (!isInitialized) {
            Debug.LogError("PointCloudMeshGenerator not initialized! Call Initialize() first.");
            return false;
        }

        if (pointData.Length != width * height * 4 || colorData.Length != width * height * 3) {
            Debug.LogError($"Data length mismatch. Expected points: {width * height * 4}, got: {pointData.Length}. " +
                         $"Expected colors: {width * height * 3}, got: {colorData.Length}");
            return false;
        }

        return true;
    }

    private void ProcessPointCloudData(float[] pointData, byte[] colorData) {
        var vertexCount = width * height;

        // Create native arrays for the job
        var nativePointData = new NativeArray<float>(pointData, Allocator.TempJob);
        var nativeColorData = new NativeArray<byte>(colorData, Allocator.TempJob);
        var nativeVertices = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
        var nativeUVs = new NativeArray<Vector2>(vertexCount, Allocator.TempJob);
        var nativeColors = new NativeArray<Color32>(vertexCount, Allocator.TempJob);
        var validityMask = new NativeArray<int>(vertexCount, Allocator.TempJob);

        try {
            // Schedule the parallel job
            var processJob = new ProcessVerticesJob {
                pointData = nativePointData,
                colorData = nativeColorData,
                vertices = nativeVertices,
                uvs = nativeUVs,
                colors = nativeColors,
                validityMask = validityMask,
                width = width,
                height = height
            };

            var jobHandle = processJob.Schedule(vertexCount, 64);
            jobHandle.Complete();

            // Get valid vertex count and create mapping
            int validVertexCount = 0;
            var oldToNewIndex = new Dictionary<int, int>();

            for (int i = 0; i < vertexCount; i++) {
                if (validityMask[i] == 1) {
                    oldToNewIndex[i] = validVertexCount++;
                }
            }

            if (validVertexCount == 0) {
                Debug.LogWarning("No valid vertices found in point cloud data");
                return;
            }

            // Copy valid vertices to cached arrays
            int currentIndex = 0;
            for (int i = 0; i < vertexCount; i++) {
                if (validityMask[i] == 1) {
                    cachedVertices[currentIndex] = nativeVertices[i];
                    cachedUVs[currentIndex] = nativeUVs[i];
                    cachedColors[currentIndex] = nativeColors[i];
                    cachedColorFloat3[currentIndex] = new Vector3(
                        nativeColors[i].r / 255f,
                        nativeColors[i].g / 255f,
                        nativeColors[i].b / 255f
                    );
                    currentIndex++;
                }
            }

            UpdateMeshBuffers(validVertexCount);
            GenerateAndUpdateMesh(validVertexCount);
        } finally {
            // Dispose native arrays
            nativePointData.Dispose();
            nativeColorData.Dispose();
            nativeVertices.Dispose();
            nativeUVs.Dispose();
            nativeColors.Dispose();
            validityMask.Dispose();
        }
    }

    private void UpdateMeshBuffers(int validVertexCount) {
        // Clear buffers
        var clearNormals = new Vector3[validVertexCount];
        var clearValidMask = new int[validVertexCount];
        normalBuffer.SetData(clearNormals);
        validVertexBuffer.SetData(clearValidMask);

        // Set vertex data
        vertexBuffer.SetData(cachedVertices, 0, 0, validVertexCount);
        uvBuffer.SetData(cachedUVs, 0, 0, validVertexCount);
        colorBuffer.SetData(cachedColorFloat3, 0, 0, validVertexCount);

        // Reset triangle counter
        int[] counterReset = { 0 };
        triangleCounterBuffer.SetData(counterReset);

        // Generate mesh and calculate initial normals
        computeShader.Dispatch(kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);

        // Normalize the normals
        computeShader.Dispatch(normalizeKernel, Mathf.CeilToInt(validVertexCount / 64f), 1, 1);

        // Get computed normals
        normalBuffer.GetData(cachedNormals, 0, 0, validVertexCount);
    }

    private void GenerateAndUpdateMesh(int validVertexCount) {
        int[] triangleCount = new int[1];
        triangleCounterBuffer.GetData(triangleCount);

        if (triangleCount[0] > 0) {
            triangleBuffer.GetData(cachedTriangles, 0, 0, triangleCount[0]);

            // Validate and update mesh
            if (ValidateAndUpdateMeshTriangles(validVertexCount, triangleCount[0])) {
                //mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
        } else {
            Debug.LogWarning("No triangles generated from compute shader");
            mesh.triangles = new int[0];
        }
    }

    private bool ValidateAndUpdateMeshTriangles(int validVertexCount, int triangleCount) {
        if (triangleCount > 0) {
            try {
                if (mesh.vertexCount != validVertexCount) {
                    mesh.Clear();
                    mesh.vertices = cachedVertices;
                    mesh.colors32 = cachedColors;
                    mesh.uv = cachedUVs;
                    mesh.normals = cachedNormals;  // Set the computed normals
                }

                mesh.triangles = cachedTriangles;
                mesh.RecalculateBounds();
                // Remove mesh.RecalculateNormals() since we're computing them in the shader
                return true;
            } catch (System.Exception e) {
                Debug.LogError($"Error setting mesh triangles: {e.Message}");
                mesh.triangles = new int[0];
            }
        }
        return false;
    }

    private void ReleaseBuffers() {
        vertexBuffer?.Release();
        normalBuffer?.Release();
        uvBuffer?.Release();
        triangleBuffer?.Release();
        triangleCounterBuffer?.Release();
        colorBuffer?.Release();
        validVertexBuffer?.Release();
    }

    private void OnDestroy() {
        ReleaseBuffers();
        if (mesh != null) {
            Destroy(mesh);
        }
    }
}