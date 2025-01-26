using System;
using UnityEngine;

public class DepthMeshGenerator : MonoBehaviour {
    [Header("Required Components")]
    public ComputeShader computeShader;
    public Material meshMaterial;

    [Header("Depth Parameters")]
    [Tooltip("Minimum valid depth value in meters")]
    public float minDepth = 0.1f;
    [Tooltip("Maximum valid depth value in meters")]
    public float maxDepth = 10.0f;
    [Tooltip("Maximum allowed depth difference ratio between adjacent pixels")]
    [Range(0.01f, 1.0f)]
    public float maxDepthDiscontinuity = 0.15f;
    [Tooltip("Maximum angle between adjacent triangles in degrees")]
    [Range(0f, 180f)]
    public float maxSurfaceAngle = 60f; // Add reference for material
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer uvBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer depthBuffer;

    private int width, height;
    private Mesh mesh;
    private int kernel;
    private bool isInitialized = false;

    // Cache for camera parameters
    private float fx, fy, tx, ty;

    // Cached textures for streaming updates
    private RenderTexture colorRT;
    private Texture2D colorTexture;

    void Awake() {
        // Create default material if none assigned
        if (meshMaterial == null) {
            // Use URP Lit shader instead of Standard
            meshMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            meshMaterial.color = Color.white;

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null) {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }
            renderer.material = meshMaterial;
        }
    }

    public void Initialize(float fx, float fy, float tx, float ty, int width, int height) {
        if (computeShader == null) {
            Debug.LogError("Compute shader is not assigned!");
            return;
        }

        this.width = width;
        this.height = height;
        this.fx = fx;
        this.fy = fy;
        this.tx = tx;
        this.ty = ty;

        // Ensure MeshFilter exists
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        // Ensure MeshRenderer exists and has material
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null) {
            renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.material = meshMaterial;
        }

        // Create mesh if it doesn't exist
        if (mesh == null) {
            mesh = new Mesh();
            mesh.MarkDynamic(); // Optimize for frequent updates
            meshFilter.mesh = mesh;
        }

        // Calculate buffer sizes
        int quadCount = (width - 1) * (height - 1);
        int vertexCount = width * height; // One vertex per pixel
        int triangleCount = quadCount * 6; // Two triangles per quad

        // Create buffers
        vertexBuffer?.Release();
        uvBuffer?.Release();
        triangleBuffer?.Release();
        depthBuffer?.Release();

        vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        uvBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);
        triangleBuffer = new ComputeBuffer(triangleCount, sizeof(int));
        depthBuffer = new ComputeBuffer(width * height, sizeof(float));

        // Setup color texture
        if (colorRT != null) colorRT.Release();
        if (colorTexture != null) Destroy(colorTexture);

        colorRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        colorRT.enableRandomWrite = true;
        colorRT.Create();

        colorTexture = new Texture2D(width, height, TextureFormat.RGB24, false);

        // Cache kernel ID and set shader parameters
        kernel = computeShader.FindKernel("GenerateMesh");
        Debug.Log($"Found kernel index: {kernel}"); // Debug log

        // Set constant shader parameters
        computeShader.SetBuffer(kernel, "vertices", vertexBuffer);
        computeShader.SetBuffer(kernel, "uvs", uvBuffer);
        computeShader.SetBuffer(kernel, "triangles", triangleBuffer);
        computeShader.SetBuffer(kernel, "depthBuffer", depthBuffer);
        computeShader.SetTexture(kernel, "colorTexture", colorRT);

        // Set parameters
        computeShader.SetFloat("fx", fx);
        computeShader.SetFloat("fy", fy);
        computeShader.SetFloat("tx", tx);
        computeShader.SetFloat("ty", ty);
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetFloat("minDepth", minDepth);
        computeShader.SetFloat("maxDepth", maxDepth);
        computeShader.SetFloat("maxDepthDiscontinuity", maxDepthDiscontinuity);
        computeShader.SetFloat("maxSurfaceAngle", maxSurfaceAngle);

        isInitialized = true;
    }

    public void UpdateMeshFromTextures(Texture2D newColorTexture, Texture2D depthTexture) {
        if (!isInitialized) {
            Debug.LogError("DepthMeshGenerator not initialized! Call Initialize() first.");
            return;
        }

        // Update color texture
        Graphics.Blit(newColorTexture, colorRT);

        // Get depth data and update buffer
        // Assuming depthTexture is RFloat format
        RenderTexture tmpRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat);
        Graphics.Blit(depthTexture, tmpRT);

        // Store current active render texture
        RenderTexture previousActive = RenderTexture.active;

        // Read depth data
        RenderTexture.active = tmpRT;
        Texture2D tmpTex = new Texture2D(width, height, TextureFormat.RFloat, false);
        tmpTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tmpTex.Apply();

        // Restore previous active render texture
        RenderTexture.active = previousActive;

        float[] depthData = new float[width * height];
        System.Buffer.BlockCopy(tmpTex.GetRawTextureData(), 0, depthData, 0, depthData.Length * sizeof(float));

        depthBuffer.SetData(depthData);

        // Dispatch shader
        computeShader.Dispatch(kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);

        try {
            // Get data back
            Vector3[] vertices = new Vector3[vertexBuffer.count];
            Vector2[] uvs = new Vector2[uvBuffer.count];
            int[] triangles = new int[triangleBuffer.count];

            vertexBuffer.GetData(vertices);
            uvBuffer.GetData(uvs);
            triangleBuffer.GetData(triangles);

            // Validate data before applying to mesh
            if (vertices.Length == 0 || triangles.Length == 0) {
                Debug.LogWarning("No valid mesh data generated");
                return;
            }

            // Update mesh with bounds check
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support larger meshes
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Update material with color texture
            if (meshMaterial != null && colorRT != null) {
                meshMaterial.mainTexture = colorRT;
            }
        } catch (System.Exception e) {
            Debug.LogError($"Error updating mesh: {e.Message}");
        } finally {
            // Cleanup temporary resources
            if (tmpRT != null) {
                tmpRT.Release();
                RenderTexture.ReleaseTemporary(tmpRT);
            }
            if (tmpTex != null) {
                Destroy(tmpTex);
            }
        }

        return;    
    }
    
    public void UpdateMeshFromArrays(byte[] rgbData, float[] depthData) {
    if (!isInitialized) {
        Debug.LogError("DepthMeshGenerator not initialized! Call Initialize() first.");
        return;
    }

    // Update color texture
    colorTexture.LoadRawTextureData(rgbData);
    colorTexture.Apply();
    Graphics.Blit(colorTexture, colorRT);

    // Update depth buffer
    depthBuffer.SetData(depthData);

    // Dispatch shader
    computeShader.Dispatch(kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);

    // Get data back
    Vector3[] vertices = new Vector3[vertexBuffer.count];
    Vector2[] uvs = new Vector2[uvBuffer.count];
    int[] triangles = new int[triangleBuffer.count];

    vertexBuffer.GetData(vertices);
    uvBuffer.GetData(uvs);
    triangleBuffer.GetData(triangles);

    // Update mesh
    mesh.Clear();
    mesh.vertices = vertices;
    mesh.uv = uvs;
    mesh.triangles = triangles;
    mesh.RecalculateNormals();
}

private void OnDestroy() {
    if (vertexBuffer != null) vertexBuffer.Release();
    if (uvBuffer != null) uvBuffer.Release();
    if (triangleBuffer != null) triangleBuffer.Release();
    if (depthBuffer != null) depthBuffer.Release();

    if (colorRT != null) {
        colorRT.Release();
        Destroy(colorRT);
    }

    if (colorTexture != null) {
        Destroy(colorTexture);
    }

    if (mesh != null) {
        Destroy(mesh);
    }
}
}