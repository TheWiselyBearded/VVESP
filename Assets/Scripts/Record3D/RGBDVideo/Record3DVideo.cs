using System;
using System.IO;
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
    private readonly IVolumetricVideoSource dataSource;

    protected IntPtr turboJPEGHandle;
    protected int loadedRGBWidth = 1440;
    protected int loadedRGBHeight = 1920;

    // [Optional] For debugging or naming
    private string captureTitle;
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
    /// Constructor that takes an IVolumetricVideoSource (async initialization must happen outside).
    /// </summary>
    public Record3DVideo(IVolumetricVideoSource source) {
        dataSource = source ?? throw new ArgumentNullException(nameof(source));
        InitComponents();

        // Copy metadata from dataSource
        numFrames_ = dataSource.FrameCount;
        fps_ = dataSource.FPS;
        width_ = dataSource.Width;
        height_ = dataSource.Height;
        fx_ = dataSource.Fx;
        fy_ = dataSource.Fy;
        tx_ = dataSource.Tx;
        ty_ = dataSource.Ty;

        // Let the DataLayer know about our metadata
        var parsedMetadata = new Record3DMetadata {
            w = width_,
            h = height_,
            fps = fps_,
            K = new System.Collections.Generic.List<float>
            {
                fx_, 0, 0, 0,
                fy_, 0, 0,
                tx_, ty_
            }
        };
        DataLayer.SetCameraMetadata(parsedMetadata);

        InitializeBuffers();
    }
    #endregion

    #region Initialization Methods
    public void InitComponents() {
        DataLayer = new DataLayer();
        turboJPEGHandle = Record3DNative.TjInitDecompress();
    }

    private void InitializeBuffers() {
        rgbBuffer = new byte[width_ * height_ * 3];
        rgbBufferBG = new byte[width_ * height_ * 3];
        positionsBuffer = new float[width_ * height_ * 4];
    }
    #endregion

    #region Frame Processing Methods
    /// <summary>
    /// An example method to fully load one frame: 
    /// fetch depth/color data from the data source, then store them in the class buffers.
    /// </summary>
    public async Task LoadFrame(int frameIdx) {
        if (frameIdx < 0 || frameIdx >= numFrames_)
            throw new IndexOutOfRangeException($"Frame {frameIdx} out of range");

        // 1. Retrieve buffers asynchronously
        byte[] depthBytes = await dataSource.GetDepthBufferAsync(frameIdx);
        byte[] colorBytes = await dataSource.GetColorBufferAsync(frameIdx);

        // 2. Store them in our local buffers (for TPL dataflow or subsequent decompression)
        lzfseDepthBuffer = depthBytes;
        jpgBuffer = colorBytes;

        // 3. (Optional) Post into a TPL block for further parallel decoding
        DataLayer.encodedBuffer.Post((jpgBuffer, lzfseDepthBuffer));

        // 4. If you want to do synchronous decoding here, you can:
        // e.g. Decompress depth into positionsBuffer or color into rgbBuffer
        // then fire OnLoadDepth?.Invoke(positionsBuffer); 
        // or OnLoadColor?.Invoke(rgbBuffer);
    }

    // If you still want TPL Dataflow for advanced parallel operations, 
    // you can adapt your old "FrameDataProduce" or "LoadFrameDataAsync" 
    // to call dataSource.*Async() under the hood.
    
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

    #region Cleanup
    /// <summary>
    /// Closes the underlying data source (e.g. disposing .zip, etc.).
    /// </summary>
    public void CloseVideo() {
        dataSource.CloseSource();
    }
    #endregion
}
