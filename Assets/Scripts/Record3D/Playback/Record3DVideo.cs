using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using UnityEngine;

public partial class Record3DVideo {
    #region Public Properties
    private int numFrames_;
    public int numFrames => numFrames_;

    private int fps_;
    public int fps => fps_;

    private int width_;
    public int width => width_;

    private int height_;
    public int height => height_;
    #endregion

    #region Private Fields
    private float fx_, fy_, tx_, ty_;
    private ZipArchive underlyingZip_;
    private string captureTitle;

    // For demonstration, these remain here. Could also be in a "private constants" partial.
    protected IntPtr turboJPEGHandle;
    protected int loadedRGBWidth = 1440;
    protected int loadedRGBHeight = 1920;
    #endregion

    #region Public Fields
    public DataLayer DataLayer;
    public string colorChoice;

    // Frame data buffers
    public byte[] rgbBuffer;
    public byte[] rgbBufferBG;
    public float[] positionsBuffer;
    public byte[] lzfseDepthBuffer;
    public byte[] jpgBuffer;
    public byte[] jpgBufferBG;
    #endregion

    #region Events
    public delegate void LoadDepthAction(float[] pos);
    public static event LoadDepthAction OnLoadDepth;

    public delegate void LoadColorAction(byte[] colors);
    public static event LoadColorAction OnLoadColor;
    #endregion

    #region Constructors
    /// <summary>
    /// Initializes a new Record3DVideo instance from a ZIP archive.
    /// </summary>
    public Record3DVideo(ZipArchive z) {
        underlyingZip_ = z;
        InitComponents();
        InitializeFromMetadata("capture/metadata");
        InitializeBuffers();
    }

    /// <summary>
    /// Initializes a new Record3DVideo instance from a ZIP archive with specific capture information.
    /// </summary>
    public Record3DVideo(ZipArchive z, Capture capture) {
        underlyingZip_ = z;
        InitComponents();
        captureTitle = capture.filename.Replace(".zip", "");
        Debug.Log($"Capture title loaded {captureTitle}");

        InitializeFromMetadata($"{captureTitle}/metadata");
        InitializeBuffers();
    }

    /// <summary>
    /// Initializes a new Record3DVideo instance from a local file path.
    /// </summary>
    public Record3DVideo(string filepath) {
        underlyingZip_ = ZipFile.Open(filepath, ZipArchiveMode.Read);
        InitializeFromMetadata("metadata");
        InitializeBuffers();
    }
    #endregion

    #region Initialization Methods
    public void InitComponents() {
        DataLayer = new DataLayer();
        // Could also initialize your TurboJPEG handle once here
        turboJPEGHandle = Record3DNative.TjInitDecompress();
    }

    /// <summary>
    /// Initializes video parameters from metadata file.
    /// </summary>
    private void InitializeFromMetadata(string metadataPath) {
        using (var metadataStream = new StreamReader(underlyingZip_.GetEntry(metadataPath).Open())) {
            string jsonContents = metadataStream.ReadToEnd();
            Record3DMetadata parsedMetadata = JsonUtility.FromJson<Record3DMetadata>(jsonContents);

            // Initialize properties
            fps_ = parsedMetadata.fps;
            width_ = parsedMetadata.w;
            height_ = parsedMetadata.h;

            // Intrinsic matrix
            fx_ = parsedMetadata.K[0];
            fy_ = parsedMetadata.K[4];
            tx_ = parsedMetadata.K[6];
            ty_ = parsedMetadata.K[7];

            DataLayer.SetCameraMetadata(parsedMetadata);
        }

        // Attempt to infer the number of frames from entries containing ".depth" or ".bytes"
        numFrames_ = underlyingZip_.Entries.Count(x => x.FullName.Contains(".depth"));
        if (numFrames_ == 0)
            numFrames_ = underlyingZip_.Entries.Count(x => x.FullName.Contains(".bytes"));
    }

    /// <summary>
    /// Initializes buffer arrays for frame data.
    /// </summary>
    private void InitializeBuffers() {
        rgbBuffer = new byte[width_ * height_ * 3];
        rgbBufferBG = new byte[width_ * height_ * 3];
        positionsBuffer = new float[width_ * height_ * 4];
    }
    #endregion

    #region Frame Processing Methods
    /// <summary>
    /// Produces frame data for processing by reading from the ZIP archive.
    /// </summary>
    public void FrameDataProduce(int frameIdx) {
        if (frameIdx >= numFrames_) return;

        LoadDepthData(frameIdx);
        LoadColorData(frameIdx);

        // Post into buffer for processing
        DataLayer.encodedBuffer.Post((jpgBuffer, lzfseDepthBuffer));
    }

    /// <summary>
    /// Asynchronously loads and processes frame data using TPL Dataflow.
    /// </summary>
    public async Task LoadFrameDataAsync(int frameIdx) {
        if (frameIdx >= numFrames_) return;
        var blocks = ConfigureDataflowBlocks();
        blocks.depthBlock.Post(frameIdx);
        blocks.colorBlock.Post(frameIdx);
        blocks.colorBGBlock.Post(frameIdx);
        // Optionally, you could await completions
        // await Task.WhenAll(blocks.depthDecodeBlock.Completion, blocks.colorDecodeBlock.Completion);
    }
    #endregion

    #region Uncompressed Data Loading Methods
    /// <summary>
    /// Loads frame data directly from uncompressed files for development purposes.
    /// </summary>
    public void LoadFrameDataUncompressed(int frameIdx) {
        using (var lzfseDepthStream = underlyingZip_.GetEntry($"dev/rgbd/d/d{frameIdx}.bytes").Open())
        using (var memoryStream = new MemoryStream()) {
            lzfseDepthStream.CopyTo(memoryStream);
            lzfseDepthBuffer = memoryStream.GetBuffer();
            float[] p = VVP_Utilities.ConvertByteArrayToFloatArray(lzfseDepthBuffer);
            Buffer.BlockCopy(p, 0, positionsBuffer, 0, lzfseDepthBuffer.Length);
        }

        using (var jpgStream = underlyingZip_.GetEntry($"dev/rgbd/c/c{frameIdx}.bytes").Open())
        using (var memoryStream = new MemoryStream()) {
            jpgStream.CopyTo(memoryStream);
            jpgBuffer = memoryStream.GetBuffer();
            rgbBuffer = new byte[jpgBuffer.Length];
            Buffer.BlockCopy(jpgBuffer, 0, rgbBuffer, 0, jpgBuffer.Length);
        }
    }
    #endregion
}
