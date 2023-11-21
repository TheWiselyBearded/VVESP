using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Record3D;
using System;
using System.IO;
using Unity.Collections;
using UnityEngine.VFX;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using UnityEngine.Networking;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine.UIElements;


//[ExecuteInEditMode]
public partial class Record3DPlayback : MonoBehaviour
{
    public string r3dPath;

    public VisualEffect streamEffect;
    private Texture2D positionTex;
    private Texture2D colorTex;

    public RenderTexture cR;
    public RenderTexture dR;

    // Playback
    private int currentFrame_ = 0;
    public bool isPlaying_ = false;
    private Record3DVideo currentVideo_ = null;
    private Timer videoFrameUpdateTimer_ = null;
    private bool shouldRefresh_ = false;
    private string lastLoadedVideoPath_ = null;
    public int numParticles;
    public ZipArchive zipArchive;

    void ReinitializeTextures(int width, int height)
    {
        DestroyImmediate(positionTex);
        DestroyImmediate(colorTex);
        positionTex = null;
        colorTex = null;
        Resources.UnloadUnusedAssets();

        positionTex = new Texture2D(width, height, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Point
        };

        colorTex = new Texture2D(width, height, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Point
        };

        if (numParticles == 0) numParticles = width * height;
        if (streamEffect == null)
        {
            streamEffect = gameObject.GetComponent<VisualEffect>();
        }
        streamEffect.SetInt("Number of Particles", numParticles);
        streamEffect.SetTexture("Particle Position Texture", positionTex);
        streamEffect.SetTexture("Particle Color Texture", colorTex);
    }

    void Update()
    {
        /*if (isPlaying_ && (currentVideo_ != null) && shouldRefresh_)
        {
            shouldRefresh_ = false;
            LoadFrame(currentFrame_);
            currentFrame_ = (currentFrame_ + 1) % currentVideo_.numFrames;
        }*/

    }

    public void OnTimerTick(object sender, ElapsedEventArgs e)
    {
        shouldRefresh_ = true;
    }
}


public partial class Record3DPlayback
{
    public int numberOfFrames
    {
        get
        {
            //ReloadVideoIfNeeded();
            return currentVideo_ == null ? 1 : currentVideo_.numFrames;
        }
    }

    public int fps
    {
        get
        {
            //ReloadVideoIfNeeded();
            return currentVideo_ == null ? 1 : currentVideo_.fps;
        }
    }


    public void LoadVideo(string path, bool force = false)
    {
        if (!force && path == lastLoadedVideoPath_)
        {
            return;
        }

        var wasPlaying = isPlaying_;
        Pause();

        string streamingAssetsPath = Application.streamingAssetsPath;
        path = Path.Combine(streamingAssetsPath, path);


        currentVideo_ = new Record3DVideo(path);
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);

        // Reset the playback and load timer
        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new Timer(1000.0 / currentVideo_.fps);
        videoFrameUpdateTimer_.AutoReset = true;
        videoFrameUpdateTimer_.Elapsed += this.OnTimerTick;

        if (wasPlaying)
        {
            Play();
        }

        lastLoadedVideoPath_ = path;
    }

    public void LoadVid() {
        //BetterStreamingAssets.Initialize();
        /*string path;
        string streamingAssetsPath = Application.streamingAssetsPath;
        path = Path.Combine(streamingAssetsPath, "momcouch.r3d");
        currentVideo_ = new Record3DVideo(path);
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);
        // Reset the playback and load timer
        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new Timer(1000.0 / currentVideo_.fps);
        videoFrameUpdateTimer_.AutoReset = true;
        videoFrameUpdateTimer_.Elapsed += this.OnTimerTick;
        lastLoadedVideoPath_ = path;*/

        /*var loadingRequest = UnityWebRequest.Get(Path.Combine(Application.streamingAssetsPath, "momcouch.r3d"));
        loadingRequest.SendWebRequest();
        while (!loadingRequest.isDone) {
            if (loadingRequest.isNetworkError || loadingRequest.isHttpError) {
                break;
            }
        }
        if (loadingRequest.isNetworkError || loadingRequest.isHttpError) {

        } else {
            //currentVideo_ = new Record3DVideo(path);
            string p = "jar:file://" + Application.dataPath + "!/assets/momcouch.r3d";
            currentVideo_ = new Record3DVideo(p);
            ReinitializeTextures(currentVideo_.width, currentVideo_.height);

            // Reset the playback and load timer
            currentFrame_ = 0;
            videoFrameUpdateTimer_ = new Timer(1000.0 / currentVideo_.fps);
            videoFrameUpdateTimer_.AutoReset = true;
            videoFrameUpdateTimer_.Elapsed += this.OnTimerTick;


            lastLoadedVideoPath_ = p;
        }*/

        //string p = "jar:file://" + Application.dataPath + "!/assets/momcouch.r3d";
        //currentVideo_ = new Record3DVideo(p);
        currentVideo_ = new Record3DVideo(zipArchive);
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);

        // Reset the playback and load timer
        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new Timer(1000.0 / currentVideo_.fps);
        videoFrameUpdateTimer_.AutoReset = true;
        videoFrameUpdateTimer_.Elapsed += this.OnTimerTick;


        //lastLoadedVideoPath_ = p;
    }

    public void LoadVid(ZipArchive za) {
        
        currentVideo_ = new Record3DVideo(za);
        zipArchive = za;
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);

        // Reset the playback and load timer
        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new Timer(1000.0 / currentVideo_.fps);
        videoFrameUpdateTimer_.AutoReset = true;
        videoFrameUpdateTimer_.Elapsed += this.OnTimerTick;


        //lastLoadedVideoPath_ = p;
    }

    public void Pause()
    {
        isPlaying_ = false;
        if (videoFrameUpdateTimer_ != null) videoFrameUpdateTimer_.Enabled = false;
    }

    public void Play()
    {
        isPlaying_ = true;
        if (videoFrameUpdateTimer_ != null) videoFrameUpdateTimer_.Enabled = true;
    }

    public bool saveToDisk = false;
    public bool loadData = true;
    private void ReloadVideoIfNeeded()
    {
        if (currentVideo_ == null && !loadData)
        {
            LoadVideo(string.IsNullOrEmpty(lastLoadedVideoPath_) ? r3dPath : lastLoadedVideoPath_, force: true);
        } else if (currentVideo_ == null && loadData) {
            LoadVid();
        }

    }


    unsafe void SetNativeVertexArrays() {        
        // pin the mesh's vertex buffer in place...
        fixed (void* vertexBufferPointer = currentVideo_.positionsBuffer) {
            // ...and use memcpy to copy the Vector3[] into a NativeArray<floar3> without casting. whould be fast!
            UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(positionTex.GetRawTextureData<float>()),
                vertexBufferPointer, currentVideo_.positionsBuffer.Length * (long)UnsafeUtility.SizeOf<float>());
        }
    }

    public unsafe void SetPositionBuffer() {
        unsafe {
            fixed (float* ptr = currentVideo_.positionsBuffer) {
                IntPtr intPtr = (IntPtr)ptr;
                positionTex.LoadRawTextureData(intPtr, positionTex.width * positionTex.height * 24);
            }
        }
    }


    private long st, et;
    public void LoadFrame(int frameNumber)
    {
        if (isPlaying_ == false) return;
        //ReloadVideoIfNeeded(); // EDIT    // Load the data from the archive
        //if (currentVideo_ == null) LoadVid();

        //if (streamEffect)
        //Debug.Log($"Load frame {frameNumber}");
        currentVideo_.LoadFrameData(frameNumber);
        //currentVideo_.LoadFrameDataUncompressed(frameNumber); // dev
        currentFrame_ = frameNumber;

        //LoadFrameDataLocal(frameNumber);  // if local pc
        //LoadColorDataLocal(frameNumber);

        cR.enableRandomWrite = true;
        dR.enableRandomWrite = true;

        //SetNativeVertexArrays();

        //var positionTexBufferSize = positionTex.width * positionTex.height * 4;
        //NativeArray<float>.Copy(currentVideo_.positionsBuffer, positionTex.GetRawTextureData<float>(), positionTexBufferSize);
        st = SystemDataFlowMeasurements.GetUnixTS();
        positionTex.SetPixelData<float>(currentVideo_.positionsBuffer, 0, 0);
        //SetPositionBuffer();
        positionTex.Apply(false, false);
        //Graphics.Blit(positionTex, dR);

        et = SystemDataFlowMeasurements.GetUnixTS();
        //Debug.Log($"Time diff depth {et-st}, size {currentVideo_.positionsBuffer.Length}");

        st = SystemDataFlowMeasurements.GetUnixTS();
        const int numRGBChannels = 3;
        var colorTexBufferSize = colorTex.width * colorTex.height * numRGBChannels * sizeof(byte);

        // Assuming jpgData is your JPEG image data as a byte array
        st = SystemDataFlowMeasurements.GetUnixTS();
        //colorTex.LoadImage(currentVideo_.jpgBuffer);

        colorTex.SetPixelData<byte>(currentVideo_.rgbBuffer, 0, 0);
        //colorTex.LoadImage(currentVideo_.rgbBuffer);
        colorTex.Apply(false, false);
        //ImageConversion.LoadImage(colorTex, currentVideo_.jpgBuffer);
        //NativeArray<byte>.Copy(currentVideo_.rgbBuffer, colorTex.GetRawTextureData<byte>(), colorTexBufferSize);
        et = SystemDataFlowMeasurements.GetUnixTS();
        //Debug.Log($"Time diff color load image {et-st}");
        //RotateImage(colorTex, 180f);

        //colorTex.Apply(false, false);
        //Graphics.Blit(colorTex, cR);


        //st = SystemDataFlowMeasurements.GetUnixTS();
        //ReverseTextureRows(colorTex);
        //et = SystemDataFlowMeasurements.GetUnixTS();
        //Debug.Log($"reverse textures {et - st}");

        //Debug.Log($"Time diff color {et - st}, size {currentVideo_.rgbBuffer.Length}");

        ///SAVING RAW DECOMPRESSED DATA TO DISK
        //SaveFloatArrayToDisk(currentVideo_.positionsBuffer, frameNumber);
        //SaveColorArrayToDisk(currentVideo_.rgbBuffer, frameNumber);

    }

    public static void RotateImage(Texture2D tex, float angleDegrees) {
        int width = tex.width;
        int height = tex.height;
        float halfHeight = height * 0.5f;
        float halfWidth = width * 0.5f;

        // Get the raw texture data as bytes (3 bytes per pixel)
        var texels = tex.GetRawTextureData<byte>();
        var copy = System.Buffers.ArrayPool<byte>.Shared.Rent(texels.Length);
        Unity.Collections.NativeArray<byte>.Copy(texels, copy, texels.Length);

        float phi = Mathf.Deg2Rad * angleDegrees;
        float cosPhi = Mathf.Cos(phi);
        float sinPhi = Mathf.Sin(phi);

        int bytesPerPixel = 3; // 3 bytes per pixel

        int address = 0;
        for (int newY = 0; newY < height; newY++) {
            for (int newX = 0; newX < width; newX++) {
                float cX = newX - halfWidth;
                float cY = newY - halfHeight;
                int oldX = Mathf.RoundToInt(cosPhi * cX + sinPhi * cY + halfWidth);
                int oldY = Mathf.RoundToInt(-sinPhi * cX + cosPhi * cY + halfHeight);

                // Check if the oldX and oldY are within bounds of the copy array
                if (oldX >= 0 && oldX < width && oldY >= 0 && oldY < height) {
                    // Calculate the byte offsets for both the source and destination
                    int srcOffset = (oldY * width + oldX) * bytesPerPixel;
                    int destOffset = address * bytesPerPixel;

                    // Copy 3 bytes (RGB) at a time
                    for (int i = 0; i < bytesPerPixel; i++) {
                        texels[destOffset + i] = copy[srcOffset + i];
                    }
                } else {
                    // Set the destination bytes to 0 for pixels outside the source bounds
                    for (int i = 0; i < bytesPerPixel; i++) {
                        texels[address * bytesPerPixel + i] = 0;
                    }
                }

                address++;
            }
        }

        // No need to reinitialize or SetPixels - data is already in-place.
        tex.Apply(true);

        System.Buffers.ArrayPool<byte>.Shared.Return(copy);
    }



    // Assuming you have a Texture2D loaded and assigned to 'texture'
    void ReverseTextureRows(Texture2D texture) {
        int width = texture.width;
        int height = texture.height;
        Color[] pixels = texture.GetPixels();

        // Create a temporary array to hold the reversed pixels
        Color[] reversedPixels = new Color[pixels.Length];

        for (int i = 0; i < height; i++) {
            int srcRow = height - 1 - i;
            int destRow = i;

            for (int x = 0; x < width; x++) {
                int srcIndex = srcRow * width + x;
                int destIndex = destRow * width + x;
                reversedPixels[destIndex] = pixels[srcIndex];
            }
        }

        // Set the reversed pixels back to the texture
        texture.SetPixels(reversedPixels);
        texture.Apply();
    }

    /// <summary>
    /// Load depth data
    /// </summary>
    /// <param name="frameNumber"></param>
    public void LoadFrameDataLocal(int frameNumber) {
        string depthFileName = $"dev/d{frameNumber}.bytes";
        string depthFilePath = Path.Combine(Application.streamingAssetsPath, depthFileName);

        // Check if the color file exists
        if (File.Exists(depthFilePath)) {
            byte[] byteArray = File.ReadAllBytes(depthFilePath);
            int numFloats = byteArray.Length / sizeof(float);
            float[] floatArray = new float[numFloats];
            System.Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);

            var positionTexBufferSize = positionTex.width * positionTex.height * 4;
            NativeArray<float>.Copy(floatArray, positionTex.GetRawTextureData<float>(), positionTexBufferSize);
            positionTex.Apply(false, false);
            
            Debug.Log($"Size of float depth array {floatArray.Length}");
        }
    }

    public void LoadColorDataLocal(int frameNumber) {
        string colorFileName = $"dev/c{frameNumber}.bytes";
        string colorFilePath = Path.Combine(Application.streamingAssetsPath, colorFileName);

        // Check if the color file exists
        if (File.Exists(colorFilePath)) {
            byte[] byteArray = File.ReadAllBytes(colorFilePath);

            const int numRGBChannels = 3;
            var colorTexBufferSize = colorTex.width * colorTex.height * numRGBChannels * sizeof(byte);

            NativeArray<byte>.Copy(byteArray, colorTex.GetRawTextureData<byte>(), colorTexBufferSize);
            colorTex.Apply(false, false);

            Debug.Log($"Size of color array {byteArray.Length}");
        }
    }

    public void SaveFloatArrayToDisk(float[] floatArray, int frameNumber) {
        string fileName = $"dev/d{frameNumber}.bytes";
        // Convert the float array to bytes
        byte[] byteArray = new byte[floatArray.Length * sizeof(float)];
        System.Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);

        // Define the path for saving the file (in the StreamingAssets folder)
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

        // Write the byte data to the file
        File.WriteAllBytes(filePath, byteArray);

        Debug.Log($"Saved float array to {fileName}");
    }

    public void SaveColorArrayToDisk(byte[] colorArray, int frameNumber) {
        string fileName = $"dev/c{frameNumber}.bytes";
        // Convert the float array to bytes
        byte[] byteArray = new byte[colorArray.Length];
        System.Buffer.BlockCopy(colorArray, 0, byteArray, 0, byteArray.Length);

        // Define the path for saving the file (in the StreamingAssets folder)
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

        // Write the byte data to the file
        File.WriteAllBytes(filePath, byteArray);

        Debug.Log($"Saved color array to {fileName}");
    }

}