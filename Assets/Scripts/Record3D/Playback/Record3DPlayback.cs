using UnityEngine;
using UnityEngine.VFX;
using System.IO.Compression;
using System.Timers;
using System.Threading.Tasks;
using System.Threading;

public partial class Record3DPlayback : MonoBehaviour {
    [Header("Rendering")]
    public VisualEffect[] streamEffects;

    // Playback state
    private int currentFrame_;
    public bool isPlaying_;
    public Record3DVideo currentVideo_;
    private System.Timers.Timer videoFrameUpdateTimer_;
    private bool shouldRefresh_;

    protected Thread consumerThread;

    // Texture management
    private Texture2D positionTex;
    private Texture2D colorTex;
    private Texture2D colorTexBG;
    private int numParticles;

    #region Loading from Zip
    /// <summary>
    /// Loads a video from a ZipArchive asynchronously, sets up Record3DVideo, etc.
    /// </summary>
    public async Task LoadVideoFromZipAsync(ZipArchive za, string captureTitle = "") {
        // 1. Create data source
        var zipSource = new ZipVolumetricVideoSource(za, captureTitle);
        // 2. Initialize it (reads metadata, sets up internal fields)
        await zipSource.InitializeSourceAsync();

        // 3. Build Record3DVideo with this data source
        currentVideo_ = new Record3DVideo(zipSource);

        // 4. Reinitialize textures
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);

        // 5. Start at frame 0, create a timer at [1000/fps] for playback
        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new System.Timers.Timer(1000.0 / currentVideo_.fps) {
            AutoReset = true
        };
        videoFrameUpdateTimer_.Elapsed += OnTimerTick;
    }

    // If we want a direct local file source
    public void LoadVideoFromLocalDisk(string path) {
        Debug.LogError("Not implemented local file storage yet");
        //var fileSource = new LocalFileVolumetricVideoSource(path); // hypothetical
        //currentVideo_ = new Record3DVideo(fileSource);
        //// ...
    }
    #endregion

    #region Playback Controls
    public void Play() {
        if (currentVideo_ == null) {
            Debug.LogWarning("No video loaded, cannot play.");
            return;
        }
        isPlaying_ = true;
        if (videoFrameUpdateTimer_ != null)
            videoFrameUpdateTimer_.Enabled = true;
    }

    public void Pause() {
        isPlaying_ = false;
        if (videoFrameUpdateTimer_ != null)
            videoFrameUpdateTimer_.Enabled = false;
    }

    public void StopAndReset() {
        Pause();
        currentFrame_ = 0;
    }
    #endregion


    #region Unity Update
    private void Update() {
        if (shouldRefresh_) {
            UpdateTexturesFromCurrentVideo();
            shouldRefresh_ = false;
        }
    }
    #endregion

}
