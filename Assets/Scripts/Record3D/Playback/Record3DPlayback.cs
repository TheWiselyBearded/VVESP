using UnityEngine;
using System;
using System.IO;
using Unity.Collections;
using UnityEngine.VFX;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading.Tasks;
using System.Threading;
using Record3D;
using Timer = System.Timers.Timer;

/// <summary>
/// Manages playback of Record3D volumetric video content, handling texture initialization,
/// frame loading, and visual effects integration for point cloud rendering.
/// </summary>
public partial class Record3DPlayback : MonoBehaviour {
    #region Public Configuration
    [Header("File Settings")]
    [Tooltip("Path to the R3D file for local playback")]
    public string r3dPath;

    [Tooltip("Available color path options")]
    public string[] colorPaths;

    [Tooltip("Currently selected color choice")]
    public string colorChoice;

    [Header("Rendering")]
    [Tooltip("Visual effect components for rendering point clouds")]
    public VisualEffect[] streamEffects;

    [Tooltip("Enable saving processed data to disk")]
    public bool saveToDisk = false;

    [Tooltip("Enable data loading")]
    public bool loadData = true;

    [Header("Debug Renders")]
    public RenderTexture cR;
    public RenderTexture dR;
    #endregion

    #region Private Fields
    // Texture Management
    private Texture2D positionTex;
    private Texture2D colorTex;
    private Texture2D colorTexBG;
    private int numParticles;

    // Playback State
    private int currentFrame_;
    public bool isPlaying_;
    public Record3DVideo currentVideo_;
    private System.Timers.Timer videoFrameUpdateTimer_;
    private bool shouldRefresh_;
    private string lastLoadedVideoPath_;
    private int colorIndex;

    // Archive Management
    public ZipArchive zipArchive;

    // Threading
    private Thread consumerThread;

    // Performance Measurement
    private long st, et;
    #endregion

    #region Properties
    /// <summary>
    /// Gets the total number of frames in the current video
    /// </summary>
    public int numberOfFrames => currentVideo_?.numFrames ?? 1;

    /// <summary>
    /// Gets the frames per second of the current video
    /// </summary>
    public int fps => currentVideo_?.fps ?? 1;
    #endregion

    #region Initialization Methods
    /// <summary>
    /// Initializes or reinitializes textures based on video dimensions
    /// </summary>
    private void ReinitializeTextures(int width, int height) {
        CleanupTextures();
        CreateTextures(width, height);
        InitializeVisualEffects(width, height);
        StartConsumerThread();
    }

    /// <summary>
    /// Cleans up existing textures
    /// </summary>
    private void CleanupTextures() {
        if (positionTex) DestroyImmediate(positionTex);
        if (colorTex) DestroyImmediate(colorTex);
        if (colorTexBG) DestroyImmediate(colorTexBG);

        positionTex = colorTex = colorTexBG = null;
        Resources.UnloadUnusedAssets();
    }

    /// <summary>
    /// Creates new textures with specified dimensions
    /// </summary>
    private void CreateTextures(int width, int height) {
        positionTex = new Texture2D(width, height, TextureFormat.RGBAFloat, false) {
            filterMode = FilterMode.Point
        };

        colorTex = new Texture2D(width, height, TextureFormat.RGB24, false) {
            filterMode = FilterMode.Point
        };

        colorTexBG = new Texture2D(width, height, TextureFormat.RGB24, false) {
            filterMode = FilterMode.Point
        };

        numParticles = width * height;
    }

    /// <summary>
    /// Initializes visual effects with created textures
    /// </summary>
    private void InitializeVisualEffects(int width, int height) {
        foreach (var effect in streamEffects) {
            effect.SetInt("Number of Particles", numParticles);
            effect.SetTexture("Particle Position Texture", positionTex);
            effect.SetTexture("Particle Color Texture",
                effect == streamEffects[0] ? colorTex : colorTexBG);
        }
    }

    /// <summary>
    /// Starts the consumer thread for processing capture buffer data
    /// </summary>
    private void StartConsumerThread() {
        consumerThread = new Thread(ConsumerCaptureBufferTaskStart) {
            IsBackground = true
        };
        consumerThread.Start();
    }
    #endregion

    #region Playback Control Methods
    /// <summary>
    /// Pauses video playback
    /// </summary>
    public void Pause() {
        isPlaying_ = false;
        if (videoFrameUpdateTimer_ != null)
            videoFrameUpdateTimer_.Enabled = false;
    }

    /// <summary>
    /// Starts video playback
    /// </summary>
    public void Play() {
        isPlaying_ = true;
        if (videoFrameUpdateTimer_ != null)
            videoFrameUpdateTimer_.Enabled = true;
    }

    /// <summary>
    /// Cycles through available color choices
    /// </summary>
    public void SequenceColorChoice() {
        colorIndex = (colorIndex + 1) % colorPaths.Length;
        currentVideo_.colorChoice = colorPaths[colorIndex];
    }
    #endregion

    #region Frame Loading Methods
    
    public void LoadFrame(int frameNumber) {
        if (isPlaying_ == false) return;
        //ReloadVideoIfNeeded(); // EDIT    // Load the data from the archive
        //if (currentVideo_ == null) LoadVid();

        //if (streamEffect)

        //currentVideo_.LoadFrameData(frameNumber);
        _ = currentVideo_.LoadFrameDataAsync(frameNumber);
        //currentVideo_.LoadFrameDataUncompressed(frameNumber); // dev

        currentFrame_ = frameNumber;

        //LoadFrameDataLocal(frameNumber);  // if local pc
        //LoadColorDataLocal(frameNumber);

        //var positionTexBufferSize = positionTex.width * positionTex.height * 4;
        //NativeArray<float>.Copy(currentVideo_.positionsBuffer, positionTex.GetRawTextureData<float>(), positionTexBufferSize);
        st = SystemDataFlowMeasurements.GetUnixTS();
        positionTex.SetPixelData<float>(currentVideo_.positionsBuffer, 0, 0);
        positionTex.Apply(false, false);

        et = SystemDataFlowMeasurements.GetUnixTS();

        st = SystemDataFlowMeasurements.GetUnixTS();
        const int numRGBChannels = 3;
        var colorTexBufferSize = colorTex.width * colorTex.height * numRGBChannels * sizeof(byte);

        // Assuming jpgData is your JPEG image data as a byte array
        st = SystemDataFlowMeasurements.GetUnixTS();

        //colorTex.LoadImage(currentVideo_.jpgBuffer);
        //colorTex.LoadImage(currentVideo_.rgbBuffer);
        //NativeArray<byte>.Copy(currentVideo_.rgbBuffer, colorTex.GetRawTextureData<byte>(), colorTexBufferSize);
        colorTex.SetPixelData<byte>(currentVideo_.rgbBuffer, 0, 0);
        colorTex.Apply(false, false);
        et = SystemDataFlowMeasurements.GetUnixTS();
        //Debug.Log($"Time diff color load image {et-st}");

        if (currentVideo_.rgbBufferBG != null) {
            colorTexBG.SetPixelData<byte>(currentVideo_.rgbBufferBG, 0, 0);
            colorTexBG.Apply(false, false);
        }

        ///SAVING RAW DECOMPRESSED DATA TO DISK
        //SaveFloatArrayToDisk(currentVideo_.positionsBuffer, frameNumber);
        //SaveColorArrayToDisk(currentVideo_.rgbBuffer, frameNumber);

    }

    /// <summary>
    /// Asynchronously loads a specific frame number
    /// </summary>
    public async void LoadFrameAsync(int frameNumber) {
        if (!isPlaying_) return;

        // Produce frame data on background thread
        await Task.Run(() => {
            currentVideo_.FrameDataProduce(frameNumber);
        });

        currentFrame_ = frameNumber;

        // Update textures on main thread
        LoadFrameDataMainThread();
    }

    /// <summary>
    /// Loads frame data into textures on the main thread
    /// </summary>
    private void LoadFrameDataMainThread() {
        // Load position data
        st = SystemDataFlowMeasurements.GetUnixTS();
        positionTex.SetPixelData(currentVideo_.DataLayer.positionsBuffer, 0);
        positionTex.Apply(false, false);
        et = SystemDataFlowMeasurements.GetUnixTS();

        // Load color data
        st = SystemDataFlowMeasurements.GetUnixTS();
        colorTex.SetPixelData(currentVideo_.DataLayer.rgbBuffer, 0);
        colorTex.Apply(false, false);
        et = SystemDataFlowMeasurements.GetUnixTS();

        // Load background color if available
        if (currentVideo_.rgbBufferBG != null) {
            colorTexBG.SetPixelData(currentVideo_.rgbBufferBG, 0);
            colorTexBG.Apply(false, false);
        }
    }

    /// <summary>
    /// Loads frame data into textures
    /// </summary>
    private void LoadFrameData(int frameNumber) {
        LoadPositionData();
        LoadColorData();
        LoadBackgroundColorData();
    }

    /// <summary>
    /// Loads position (depth) data into position texture
    /// </summary>
    private void LoadPositionData() {
        st = SystemDataFlowMeasurements.GetUnixTS();
        positionTex.SetPixelData(currentVideo_.DataLayer.positionsBuffer, 0);
        positionTex.Apply(false, false);
        et = SystemDataFlowMeasurements.GetUnixTS();
    }

    /// <summary>
    /// Loads color data into color texture
    /// </summary>
    private void LoadColorData() {
        st = SystemDataFlowMeasurements.GetUnixTS();
        colorTex.SetPixelData(currentVideo_.DataLayer.rgbBuffer, 0);
        colorTex.Apply(false, false);
        et = SystemDataFlowMeasurements.GetUnixTS();
    }

    /// <summary>
    /// Loads background color data if available
    /// </summary>
    private void LoadBackgroundColorData() {
        if (currentVideo_.rgbBufferBG != null) {
            colorTexBG.SetPixelData(currentVideo_.rgbBufferBG, 0);
            colorTexBG.Apply(false, false);
        }
    }
    #endregion

    #region Video Loading Methods
    /// <summary>
    /// Loads video from a ZIP archive with capture information
    /// </summary>
    public void LoadVid(ZipArchive za, Capture capture) {
        if (currentVideo_ != null) {
            Pause();
            currentVideo_ = null;
        }

        currentVideo_ = new Record3DVideo(za, capture);
        zipArchive = za;

        InitializeVideoPlayback();
    }

    public void LoadVideo(string path, bool force = false) {
        if (!force && path == lastLoadedVideoPath_) {
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
        videoFrameUpdateTimer_ = new System.Timers.Timer(1000.0 / currentVideo_.fps);
        videoFrameUpdateTimer_.AutoReset = true;
        videoFrameUpdateTimer_.Elapsed += this.OnTimerTick;

        if (wasPlaying) {
            Play();
        }

        lastLoadedVideoPath_ = path;
    }

    /// <summary>
    /// Initializes video playback settings and timers
    /// </summary>
    private void InitializeVideoPlayback() {
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);

        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new Timer(1000.0 / currentVideo_.fps) {
            AutoReset = true
        };
        videoFrameUpdateTimer_.Elapsed += OnTimerTick;

        currentVideo_.colorChoice = colorPaths[0];
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// Handles timer tick events for frame updates
    /// </summary>
    public void OnTimerTick(object sender, ElapsedEventArgs e) {
        shouldRefresh_ = true;
    }

    /// <summary>
    /// Handles depth data loading events
    /// </summary>
    public void OnLoadDepthEvent(float[] positions) {
        st = SystemDataFlowMeasurements.GetUnixTS();
        positionTex.SetPixelData(positions, 0);
        positionTex.Apply(false, false);
        et = SystemDataFlowMeasurements.GetUnixTS();
    }

    /// <summary>
    /// Handles color data loading events
    /// </summary>
    public void OnLoadColorEvent(byte[] colors) {
        st = SystemDataFlowMeasurements.GetUnixTS();
        colorTex.SetPixelData(colors, 0);
        colorTex.Apply(false, false);
        et = SystemDataFlowMeasurements.GetUnixTS();
    }
    #endregion

    #region Threading Methods
    /// <summary>
    /// Starts the consumer capture buffer task
    /// </summary>
    protected void ConsumerCaptureBufferTaskStart() {
        var consumerCaptureTask = Task.Run(() =>
            currentVideo_.DataLayer.ConsumerCaptureData().Wait());
    }

    /// <summary>
    /// Stops the server thread
    /// </summary>
    private void StopServerThread() {
        if (consumerThread?.IsAlive == true) {
            consumerThread.Abort();
            consumerThread = null;
        }
        if (currentVideo_?.DataLayer != null) {
            currentVideo_.DataLayer.encodedBuffer = null;
        }
    }
    #endregion

    #region Unity Lifecycle Methods
    private void OnDestroy() {
        StopServerThread();
    }
    #endregion

    #region Image Processing Methods
    /// <summary>
    /// Rotates an image texture by a specified angle
    /// </summary>
    public static void RotateImage(Texture2D tex, float angleDegrees) {
        int width = tex.width;
        int height = tex.height;
        float halfHeight = height * 0.5f;
        float halfWidth = width * 0.5f;

        var texels = tex.GetRawTextureData<byte>();
        var copy = System.Buffers.ArrayPool<byte>.Shared.Rent(texels.Length);
        NativeArray<byte>.Copy(texels, copy, texels.Length);

        ProcessImageRotation(tex, texels, copy, width, height, halfWidth, halfHeight, angleDegrees);

        tex.Apply(true);
        System.Buffers.ArrayPool<byte>.Shared.Return(copy);
    }

    /// <summary>
    /// Processes the rotation of image data
    /// </summary>
    private static void ProcessImageRotation(Texture2D tex, NativeArray<byte> texels, byte[] copy,
        int width, int height, float halfWidth, float halfHeight, float angleDegrees) {
        float phi = Mathf.Deg2Rad * angleDegrees;
        float cosPhi = Mathf.Cos(phi);
        float sinPhi = Mathf.Sin(phi);
        const int bytesPerPixel = 3;

        for (int newY = 0, address = 0; newY < height; newY++) {
            for (int newX = 0; newX < width; newX++, address++) {
                ProcessPixelRotation(texels, copy, address, newX, newY,
                    width, height, halfWidth, halfHeight, cosPhi, sinPhi);
            }
        }
    }

    /// <summary>
    /// Processes rotation for a single pixel
    /// </summary>
    private static void ProcessPixelRotation(NativeArray<byte> texels, byte[] copy, int address,
        int newX, int newY, int width, int height, float halfWidth, float halfHeight,
        float cosPhi, float sinPhi) {
        const int bytesPerPixel = 3;
        float cX = newX - halfWidth;
        float cY = newY - halfHeight;
        int oldX = Mathf.RoundToInt(cosPhi * cX + sinPhi * cY + halfWidth);
        int oldY = Mathf.RoundToInt(-sinPhi * cX + cosPhi * cY + halfHeight);

        int destOffset = address * bytesPerPixel;
        if (IsPixelInBounds(oldX, oldY, width, height)) {
            int srcOffset = (oldY * width + oldX) * bytesPerPixel;
            CopyPixelData(texels, copy, srcOffset, destOffset);
        } else {
            ClearPixelData(texels, destOffset);
        }
    }

    private static bool IsPixelInBounds(int x, int y, int width, int height) {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private static void CopyPixelData(NativeArray<byte> texels, byte[] copy, int srcOffset, int destOffset) {
        const int bytesPerPixel = 3;
        for (int i = 0; i < bytesPerPixel; i++) {
            texels[destOffset + i] = copy[srcOffset + i];
        }
    }

    private static void ClearPixelData(NativeArray<byte> texels, int destOffset) {
        const int bytesPerPixel = 3;
        for (int i = 0; i < bytesPerPixel; i++) {
            texels[destOffset + i] = 0;
        }
    }
    #endregion
}