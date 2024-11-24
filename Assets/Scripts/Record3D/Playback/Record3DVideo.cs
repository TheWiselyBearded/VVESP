using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using UnityEngine;
/// <summary>
/// Manages Record3D video data processing, including depth and color frame handling,
/// decompression, and buffer management for volumetric video playback.
/// </summary>
public class Record3DVideo {
    #region Public Properties
    /// <summary>
    /// Total number of frames in the video
    /// </summary>
    private int numFrames_;
    public int numFrames { get { return numFrames_; } }

    /// <summary>
    /// Frames per second of the video
    /// </summary>
    private int fps_;
    public int fps { get { return fps_; } }

    /// <summary>
    /// Width of the video frames
    /// </summary>
    private int width_;
    public int width { get { return width_; } }

    /// <summary>
    /// Height of the video frames
    /// </summary>
    private int height_;
    public int height { get { return height_; } }
    #endregion

    #region Private Fields
    /// <summary>
    /// Camera intrinsic matrix coefficients
    /// </summary>
    private float fx_, fy_, tx_, ty_;

    private ZipArchive underlyingZip_;
    private string captureTitle;
    protected IntPtr turboJPEGHandle;
    private long st, et;
    protected int loadedRGBWidth = 1440;
    protected int loadedRGBHeight = 1920;
    #endregion

    #region Public Fields
    public DataLayer DataLayer;
    public string colorChoice;

    // Buffer arrays for frame data
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

    #region Native Library Definitions
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const string JPG_LIBRARY_NAME = "turbojpeg";
        private const string LIBRARY_NAME = "record3d_unity_playback";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string JPG_LIBRARY_NAME = "libturbojpeg";
    private const string LIBRARY_NAME = "librecord3d_unity_playback.dylib";
#elif UNITY_ANDROID
        private const string JPG_LIBRARY_NAME = "jpeg-turbo";
        private const string LIBRARY_NAME = "record3d_unity_playback";
#endif
    #endregion

    #region Native Methods
    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
    private static extern int tjDecompress2(IntPtr handle, IntPtr jpegBuf, uint jpegSize,
        IntPtr dstBuf, int width, int pitch, int height, int pixelFormat, int flags);

    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
    public static extern IntPtr TjInitDecompress();

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void DecompressFrame(byte[] jpgBytes, UInt32 jpgBytesSize,
        byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, byte[] rgbBuffer, float[] poseBuffer,
        Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressFrameDepthFast", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DecompressFrameDepthFast(byte[] lzfseDepthBytes, UInt32 lzfseBytesSize,
        float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressFrameDepthReza", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DecompressFrameDepthReza(byte[] lzfseDepthBytes, UInt32 lzfseBytesSize,
        float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressColor", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DecompressColor(byte[] jpgBytes, uint jpgBytesSize,
        byte[] rgbBuffer, out int loadedRGBWidth, out int loadedRGBHeight);

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressDepth", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong DecompressDepth(byte[] lzfseDepthBytes, UInt32 lzfseBytesSize,
        out IntPtr lzfseDecodedBytes, Int32 width, Int32 height);

    [DllImport(LIBRARY_NAME, EntryPoint = "PopulatePositionBuffer", CallingConvention = CallingConvention.Cdecl)]
    private static extern void PopulatePositionBuffer(IntPtr lzfseDecodedDepthBytes,
        int loadedRGBWidth, int loadedRGBHeight, UInt32 lzfseBytesSize, float[] poseBuffer,
        UInt32 outSize, UInt32 width, UInt32 height, float fx, float fy, float tx, float ty);
    #endregion

    #region Metadata Structure
    [System.Serializable]
    public struct Record3DMetadata {
        public int w;
        public int h;
        public List<float> K;
        public int fps;
    }
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
    /// <summary>
    /// Initializes core components and resources.
    /// </summary>
    public void InitComponents() {
        DataLayer = new DataLayer();
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

            // Init the intrinsic matrix coeffs
            fx_ = parsedMetadata.K[0];
            fy_ = parsedMetadata.K[4];
            tx_ = parsedMetadata.K[6];
            ty_ = parsedMetadata.K[7];

            DataLayer.SetCameraMetadata(parsedMetadata);
        }

        numFrames_ = underlyingZip_.Entries.Count(x => x.FullName.Contains(".depth"));
        if (numFrames_ == 0) numFrames_ = underlyingZip_.Entries.Count(x => x.FullName.Contains(".bytes"));
    }

    /// <summary>
    /// Initializes buffer arrays for frame data.
    /// </summary>
    private void InitializeBuffers() {
        rgbBuffer = new byte[width * height * 3];
        rgbBufferBG = new byte[width * height * 3];
        positionsBuffer = new float[width * height * 4];
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
    /// Loads depth data for a specific frame.
    /// </summary>
    private void LoadDepthData(int frameIdx) {
        using (var lzfseDepthStream = underlyingZip_.GetEntry($"{captureTitle}/rgbd/{frameIdx}.depth").Open())
        using (var memoryStream = new MemoryStream()) {
            lzfseDepthStream.CopyTo(memoryStream);
            lzfseDepthBuffer = memoryStream.GetBuffer();
        }
    }

    /// <summary>
    /// Loads color data for a specific frame.
    /// </summary>
    private void LoadColorData(int frameIdx) {
        using (var jpgStream = underlyingZip_.GetEntry($"{captureTitle}/rgbd/{frameIdx}.jpg").Open())
        using (var memoryStream = new MemoryStream()) {
            jpgStream.CopyTo(memoryStream);
            jpgBuffer = memoryStream.GetBuffer();
        }
    }

    /// <summary>
    /// Asynchronously loads and processes frame data using TPL Dataflow.
    /// </summary>
    public async Task LoadFrameDataAsync(int frameIdx) {
        if (frameIdx >= numFrames_) return;

        // Create and configure the TPL Dataflow blocks
        var blocks = ConfigureDataflowBlocks();

        // Post the frame index to start processing
        blocks.depthBlock.Post(frameIdx);
        blocks.colorBlock.Post(frameIdx);
        blocks.colorBGBlock.Post(frameIdx);

        // Optional: Wait for completion
        //await Task.WhenAll(blocks.depthDecodeBlock.Completion, blocks.colorDecodeBlock.Completion);
    }

    /// <summary>
    /// Configures the TPL Dataflow blocks for parallel processing.
    /// </summary>
    private (TransformBlock<int, byte[]> depthBlock, ActionBlock<byte[]> depthDecodeBlock,
             TransformBlock<int, byte[]> colorBlock, ActionBlock<byte[]> colorDecodeBlock,
             TransformBlock<int, byte[]> colorBGBlock, ActionBlock<byte[]> colorBGDecodeBlock)
    ConfigureDataflowBlocks() {
        // Configure depth processing blocks
        var loadDepthBlock = new TransformBlock<int, byte[]>(async idx => {
            using (var stream = underlyingZip_.GetEntry($"{captureTitle}/rgbd/{idx}.depth").Open())
            using (var memoryStream = new MemoryStream()) {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.GetBuffer();
            }
        });

        var decodeDepthBlock = new ActionBlock<byte[]>(async depthBuffer => {
            IntPtr decodedDepthDataPtr = IntPtr.Zero;
            ulong totalDecompressDepth = DecompressDepth(depthBuffer, (uint)depthBuffer.Length,
                out decodedDepthDataPtr, width_, height_);

            PopulatePositionBuffer(decodedDepthDataPtr, 1440, 1920, (uint)depthBuffer.Length,
                positionsBuffer, (uint)totalDecompressDepth, (uint)width_, (uint)height_,
                fx_, fy_, tx_, ty_);

            await Task.Yield();
        });

        // Configure color processing blocks
        var loadColorBlock = new TransformBlock<int, byte[]>(async idx => {
            using (var stream = underlyingZip_.GetEntry(String.Format(colorChoice, idx)).Open())
            using (var memoryStream = new MemoryStream()) {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.GetBuffer();
            }
        });

        var decodeColorBlock = new ActionBlock<byte[]>(async colorBuffer => {
            unsafe {
                fixed (byte* ptr = rgbBuffer) {
                    IntPtr jpgPtr = VVP_Utilities.ConvertByteArrayToIntPtr(colorBuffer);
                    tjDecompress2(turboJPEGHandle, jpgPtr, (uint)colorBuffer.Length,
                        (IntPtr)ptr, loadedRGBWidth, 0, loadedRGBHeight, 0, 0);
                }
            }
            await Task.Yield();
        });

        // Configure background color processing blocks
        var loadColorBGBlock = new TransformBlock<int, byte[]>(async idx => {
            using (var stream = underlyingZip_.GetEntry($"{captureTitle}/rgbd/bg/bgColor{idx}.jpg").Open())
            using (var memoryStream = new MemoryStream()) {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.GetBuffer();
            }
        });

        var decodeColorBGBlock = new ActionBlock<byte[]>(async colorBuffer => {
            unsafe {
                fixed (byte* ptr = rgbBufferBG) {
                    IntPtr jpgPtr = VVP_Utilities.ConvertByteArrayToIntPtr(colorBuffer);
                    tjDecompress2(turboJPEGHandle, jpgPtr, (uint)colorBuffer.Length,
                        (IntPtr)ptr, loadedRGBWidth, 0, loadedRGBHeight, 0, 0);
                }
            }
            await Task.Yield();
        });

        // Link the blocks
        loadDepthBlock.LinkTo(decodeDepthBlock, new DataflowLinkOptions { PropagateCompletion = true });
        loadColorBlock.LinkTo(decodeColorBlock, new DataflowLinkOptions { PropagateCompletion = true });
        loadColorBGBlock.LinkTo(decodeColorBGBlock, new DataflowLinkOptions { PropagateCompletion = true });
        // Return the configured blocks
        return (loadDepthBlock, decodeDepthBlock,
                loadColorBlock, decodeColorBlock,
                loadColorBGBlock, decodeColorBGBlock);
    }
    #endregion

    #region Interpolation Methods
    /// <summary>
    /// Interpolates depth values for a given position using bilinear interpolation.
    /// </summary>
    private static float InterpolateDepth(IntPtr depthDataPtr, float x, float y, int imgWidth, int imgHeight) {
        int wX = (int)x;
        int wY = (int)y;
        float fracX = x - wX;
        float fracY = y - wY;

        int topLeftIdx = wY * imgWidth + wX;
        int topRightIdx = wY * imgWidth + Math.Min(wX + 1, imgWidth - 1);
        int bottomLeftIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + wX;
        int bottomRightIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + Math.Min(wX + 1, imgWidth - 1);

        // Getting the values from the unmanaged memory
        float topLeft = Marshal.PtrToStructure<float>(IntPtr.Add(depthDataPtr, topLeftIdx * sizeof(float)));
        float topRight = Marshal.PtrToStructure<float>(IntPtr.Add(depthDataPtr, topRightIdx * sizeof(float)));
        float bottomLeft = Marshal.PtrToStructure<float>(IntPtr.Add(depthDataPtr, bottomLeftIdx * sizeof(float)));
        float bottomRight = Marshal.PtrToStructure<float>(IntPtr.Add(depthDataPtr, bottomRightIdx * sizeof(float)));

        return (topLeft * (1.0f - fracX) + fracX * topRight) * (1.0f - fracY) +
               (bottomLeft * (1.0f - fracX) + fracX * bottomRight) * fracY;
    }

    /// <summary>
    /// Interpolates depth values for a given position using managed arrays.
    /// </summary>
    private static float InterpolateDepth(float[] depthData, float x, float y, int imgWidth, int imgHeight) {
        int wX = (int)x;
        int wY = (int)y;
        float fracX = x - wX;
        float fracY = y - wY;

        int topLeftIdx = wY * imgWidth + wX;
        int topRightIdx = wY * imgWidth + Math.Min(wX + 1, imgWidth - 1);
        int bottomLeftIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + wX;
        int bottomRightIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + Math.Min(wX + 1, imgWidth - 1);

        return (depthData[topLeftIdx] * (1.0f - fracX) + fracX * depthData[topRightIdx]) * (1.0f - fracY) +
               (depthData[bottomLeftIdx] * (1.0f - fracX) + fracX * depthData[bottomRightIdx]) * fracY;
    }
    #endregion

    #region Position Buffer Population Methods
    /// <summary>
    /// Populates position buffer using parallel processing for improved performance.
    /// </summary>
    public static void PopulatePositionBufferLocal(IntPtr lzfseDecodedDepthBytesPtr,
        int loadedRGBWidth, int loadedRGBHeight,
        uint lzfseBytesSize,
        float[] poseBuffer,
        int outSize,
        int width, int height,
        float fx, float fy, float tx, float ty) {
        int depthmapDecompressedSizeIfSameResolutionAsRGB = loadedRGBWidth * loadedRGBHeight * sizeof(float);
        bool isDepthTheSameSizeAsRGB = depthmapDecompressedSizeIfSameResolutionAsRGB == outSize;

        int depthWidth = isDepthTheSameSizeAsRGB ? loadedRGBWidth : 192;
        int depthHeight = isDepthTheSameSizeAsRGB ? loadedRGBHeight : 256;

        float ifx = 1.0f / fx;
        float ify = 1.0f / fy;
        float itx = -tx / fx;
        float ity = -ty / fy;

        float invRGBWidth = 1.0f / loadedRGBWidth;
        float invRGBHeight = 1.0f / loadedRGBHeight;

        const int numComponentsPerPointPosition = 4;
        bool needToInterpolate = loadedRGBWidth != depthWidth || loadedRGBHeight != depthHeight;

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        Parallel.For(0, height, parallelOptions, i => {
            for (int j = 0; j < width; j++) {
                int idx = loadedRGBWidth * i + j;
                int posBuffIdx = numComponentsPerPointPosition * idx;
                float depthX = invRGBWidth * depthWidth * j;
                float depthY = invRGBHeight * depthHeight * i;
                float currDepth = needToInterpolate
                    ? InterpolateDepth(lzfseDecodedDepthBytesPtr, depthX, depthY, depthWidth, depthHeight)
                    : Marshal.PtrToStructure<float>(IntPtr.Add(lzfseDecodedDepthBytesPtr, idx * sizeof(float)));

                poseBuffer[posBuffIdx + 0] = (ifx * j + itx) * currDepth;
                poseBuffer[posBuffIdx + 1] = -(ify * i + ity) * currDepth;
                poseBuffer[posBuffIdx + 2] = -currDepth;
                poseBuffer[posBuffIdx + 3] = idx;
            }
        });
    }

    /// <summary>
    /// Populates position buffer using managed arrays.
    /// </summary>
    public static void PopulatePositionBufferLocal(byte[] lzfseDecodedDepthBytes,
        int loadedRGBWidth, int loadedRGBHeight,
        uint lzfseBytesSize,
        float[] poseBuffer,
        int outSize,
        int width, int height,
        float fx, float fy, float tx, float ty) {
        int depthmapDecompressedSizeIfSameResolutionAsRGB = loadedRGBWidth * loadedRGBHeight * sizeof(float);
        bool isDepthTheSameSizeAsRGB = depthmapDecompressedSizeIfSameResolutionAsRGB == outSize;

        int depthWidth = isDepthTheSameSizeAsRGB ? loadedRGBWidth : 192;
        int depthHeight = isDepthTheSameSizeAsRGB ? loadedRGBHeight : 256;

        float ifx = 1.0f / fx;
        float ify = 1.0f / fy;
        float itx = -tx / fx;
        float ity = -ty / fy;

        float invRGBWidth = 1.0f / loadedRGBWidth;
        float invRGBHeight = 1.0f / loadedRGBHeight;

        const int numComponentsPerPointPosition = 4;
        bool needToInterpolate = loadedRGBWidth != depthWidth || loadedRGBHeight != depthHeight;

        float[] depthDataPtr = new float[lzfseDecodedDepthBytes.Length / sizeof(float)];
        Buffer.BlockCopy(lzfseDecodedDepthBytes, 0, depthDataPtr, 0, lzfseDecodedDepthBytes.Length);

        for (int i = 0; i < height; i++) {
            for (int j = 0; j < width; j++) {
                int idx = loadedRGBWidth * i + j;
                int posBuffIdx = numComponentsPerPointPosition * idx;
                float depthX = invRGBWidth * depthWidth * j;
                float depthY = invRGBHeight * depthHeight * i;
                float currDepth = needToInterpolate
                    ? InterpolateDepth(depthDataPtr, depthX, depthY, depthWidth, depthHeight)
                    : depthDataPtr[idx];

                poseBuffer[posBuffIdx + 0] = (ifx * j + itx) * currDepth;
                poseBuffer[posBuffIdx + 1] = -(ify * i + ity) * currDepth;
                poseBuffer[posBuffIdx + 2] = -currDepth;
                poseBuffer[posBuffIdx + 3] = idx;
            }
        }
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
            System.Buffer.BlockCopy(p, 0, positionsBuffer, 0, p.Length);
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