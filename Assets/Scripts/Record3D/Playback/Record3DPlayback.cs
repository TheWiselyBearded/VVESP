using UnityEngine;
using UnityEngine.VFX;
using System.IO.Compression;
using System.Threading;
using System.Timers;

public partial class Record3DPlayback : MonoBehaviour {
    #region Public Configuration
    [Header("File Settings")]
    public string r3dPath;

    [Tooltip("Available color path options")]
    public string[] colorPaths;

    [Tooltip("Currently selected color choice")]
    public string colorChoice;

    [Header("Rendering")]
    public VisualEffect[] streamEffects;
    public bool saveToDisk = false;
    public bool loadData = true;

    [Header("Debug Renders")]
    public RenderTexture cR;
    public RenderTexture dR;
    #endregion

    #region Private Fields
    private Texture2D positionTex;
    private Texture2D colorTex;
    private Texture2D colorTexBG;
    private int numParticles;

    private int currentFrame_;
    public bool isPlaying_;
    public Record3DVideo currentVideo_;
    private System.Timers.Timer videoFrameUpdateTimer_;
    private bool shouldRefresh_;
    private string lastLoadedVideoPath_;
    private int colorIndex;

    public ZipArchive zipArchive;
    private Thread consumerThread;

    private long st, et;
    #endregion

    #region Properties
    public int numberOfFrames => currentVideo_?.numFrames ?? 1;
    public int fps => currentVideo_?.fps ?? 1;
    #endregion

    #region Playback Control Methods
    public void Pause() {
        isPlaying_ = false;
        if (videoFrameUpdateTimer_ != null)
            videoFrameUpdateTimer_.Enabled = false;
    }

    public void Play() {
        isPlaying_ = true;
        if (videoFrameUpdateTimer_ != null)
            videoFrameUpdateTimer_.Enabled = true;
    }

    public void SequenceColorChoice() {
        colorIndex = (colorIndex + 1) % colorPaths.Length;
        if (currentVideo_ != null)
            currentVideo_.colorChoice = colorPaths[colorIndex];
    }
    #endregion

    #region Video Loading Methods
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
        if (!force && path == lastLoadedVideoPath_)
            return;

        var wasPlaying = isPlaying_;
        Pause();

        string streamingAssetsPath = Application.streamingAssetsPath;
        path = System.IO.Path.Combine(streamingAssetsPath, path);

        currentVideo_ = new Record3DVideo(path);
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);

        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new System.Timers.Timer(1000.0 / currentVideo_.fps) {
            AutoReset = true
        };
        videoFrameUpdateTimer_.Elapsed += this.OnTimerTick;

        if (wasPlaying)
            Play();

        lastLoadedVideoPath_ = path;
    }

    private void InitializeVideoPlayback() {
        ReinitializeTextures(currentVideo_.width, currentVideo_.height);

        currentFrame_ = 0;
        videoFrameUpdateTimer_ = new System.Timers.Timer(1000.0 / currentVideo_.fps) {
            AutoReset = true
        };
        videoFrameUpdateTimer_.Elapsed += OnTimerTick;

        currentVideo_.colorChoice = colorPaths[0];
    }
    #endregion
}
