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


//[ExecuteInEditMode]
public partial class Record3DPlayback : MonoBehaviour
{
    public string r3dPath;

    public VisualEffect streamEffect;
    private Texture2D positionTex;
    private Texture2D colorTex;

    // Playback
    private int currentFrame_ = 0;
    private bool isPlaying_ = false;
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

    private void ReloadVideoIfNeeded()
    {
        if (currentVideo_ == null)
        {
            LoadVideo(string.IsNullOrEmpty(lastLoadedVideoPath_) ? r3dPath : lastLoadedVideoPath_, force: true);
        }
    }

    public bool saveToDisk = false;
    public bool loadData = true;
    public void LoadFrame(int frameNumber)
    {                
        ReloadVideoIfNeeded(); // EDIT    // Load the data from the archive
        //if (currentVideo_ == null) LoadVid();

        //if (streamEffect)

        currentVideo_.LoadFrameData(frameNumber);
        currentFrame_ = frameNumber;

        LoadFrameDataLocal(frameNumber);
        LoadColorDataLocal(frameNumber);

        //var positionTexBufferSize = positionTex.width * positionTex.height * 4;
        //NativeArray<float>.Copy(currentVideo_.positionsBuffer, positionTex.GetRawTextureData<float>(), positionTexBufferSize);
        //positionTex.Apply(false, false);        

        //const int numRGBChannels = 3;
        //var colorTexBufferSize = colorTex.width * colorTex.height * numRGBChannels * sizeof(byte);
        //NativeArray<byte>.Copy(currentVideo_.rgbBuffer, colorTex.GetRawTextureData<byte>(), colorTexBufferSize);
        //colorTex.Apply(false, false);

        //SaveFloatArrayToDisk(currentVideo_.positionsBuffer, frameNumber);
        //SaveColorArrayToDisk(currentVideo_.rgbBuffer, frameNumber);

        /*// Assuming dirtyRect is a Rect that contains the area of the texture that needs updating
        Rect dirtyRect = new Rect(100, 100, 300, 300);        
        int dirtyRegionSize = (int)(dirtyRect.width * dirtyRect.height * 4); // 4 is for float
        // Create a temporary NativeArray to hold the data for the dirty region
        NativeArray<float> tempArray = new NativeArray<float>(dirtyRegionSize, Allocator.Temp);
        // Copy only the relevant data into tempArray        
        NativeArray<float>.Copy(currentVideo_.positionsBuffer, tempArray, dirtyRegionSize);
        positionTex.Apply(false, false);
        // Dispose of the temporary NativeArray
        tempArray.Dispose();*/
    }

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
            
            Debug.Log($"Size of float array {floatArray.Length}");
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

            Debug.Log($"Size of float array {byteArray.Length}");
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