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
using System.Drawing;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static UnityEngine.Networking.UnityWebRequest;

public class Record3DVideo
{
    private int numFrames_;
    public int numFrames { get { return numFrames_; } }

    private int fps_;
    public int fps { get { return fps_; } }

    private int width_;
    public int width { get { return width_; } }

    private int height_;
    public int height { get { return height_; } }

    /// <summary>
    /// The intrinsic matrix coefficients.
    /// </summary>
    private float fx_, fy_, tx_, ty_;


    private ZipArchive underlyingZip_;

    public byte[] rgbBuffer;
    public byte[] rgbBufferBG;

    public float[] positionsBuffer;

    [System.Serializable]
    public struct Record3DMetadata
    {
        public int w;
        public int h;
        public List<float> K;
        public int fps;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string JPG_LIBRARY_NAME = "turbojpeg";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string JPG_LIBRARY_NAME = "libturbojpeg";
#elif UNITY_ANDROID
    private const string JPG_LIBRARY_NAME = "jpeg-turbo";
#endif


    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
    private static extern int tjDecompress2(IntPtr handle, IntPtr jpegBuf, uint jpegSize, IntPtr dstBuf, int width, int pitch, int height, int pixelFormat, int flags);

    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
    public static extern IntPtr TjInitDecompress();

    //private const string LIBRARY_NAME = "record3d_unity_playback"; //"record3d_unity_playback.dll";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string LIBRARY_NAME = "record3d_unity_playback";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string LIBRARY_NAME = "librecord3d_unity_playback.dylib";
#elif UNITY_ANDROID
    private const string LIBRARY_NAME = "librecord3d_unity_playback.so"; //"record3d_unity_playback.dll";
#else
#error "Unsupported platform!"
#endif


    /*#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string LIBRARY_NAME = "librecord3d_unity_playback.dylib";

    #elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_ANDROID
        private const string LIBRARY_NAME = "librecord3d_unity_playback"; //"record3d_unity_playback.dll";

    #else
    #error "Unsupported platform!"
    #endif*/

    // Import the global variables from the DLL

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void DecompressFrame(byte[] jpgBytes, UInt32 jpgBytesSize, byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, byte[] rgbBuffer, float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressFrameDepthFast", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DecompressFrameDepthFast(byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressFrameDepthReza", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DecompressFrameDepthReza(byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressColor", CallingConvention = CallingConvention.Cdecl)]
    public static extern void DecompressColor(
        byte[] jpgBytes,
        uint jpgBytesSize,
        byte[] rgbBuffer,
        out int loadedRGBWidth,
        out int loadedRGBHeight
    );

    //private static extern int DecompressDepth(byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, byte[] lzfseDecodedBytes, Int32 width, Int32 height);
    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressDepth", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong DecompressDepth(
        byte[] lzfseDepthBytes, // Byte array containing lzfseDepthBytes
        UInt32 lzfseBytesSize,    // Size of lzfseDepthBytes
        out IntPtr lzfseDecodedBytes, // Pointer to the decoded depth data
        Int32 width,
        Int32 height
    );

    [DllImport(LIBRARY_NAME, EntryPoint = "PopulatePositionBuffer", CallingConvention = CallingConvention.Cdecl)]
    private static extern void PopulatePositionBuffer(IntPtr lzfseDecodedDepthBytes, int loadedRGBWidth, int loadedRGBHeight, UInt32 lzfseBytesSize, float[] poseBuffer, UInt32 outSize, UInt32 width, UInt32 height, float fx, float fy, float tx, float ty);


    protected IntPtr turboJPEGHandle;

    // Method to convert the unmanaged array to a managed array and then free the unmanaged array.
    private float[] GetManagedArray(IntPtr unmanagedArray, int length) {
        float[] managedArray = new float[length];
        Marshal.Copy(unmanagedArray, managedArray, 0, length);
        Marshal.FreeCoTaskMem(unmanagedArray); // Assume the C++ side uses CoTaskMemAlloc or compatible for allocation.
        return managedArray;
    }

    public Record3DVideo(ZipArchive z) {
        //currentVideo_ = new Record3DVideo(path);        

        //string[] d = BetterStreamingAssets.GetFiles("\\", "momcouch.r3d", SearchOption.AllDirectories);
         // new ZipArchive( //ZipFile.Open(d[0], ZipArchiveMode.Read);
        underlyingZip_ = z;

        // Load metadata (FPS, the intrinsic matrix, dimensions)
        using (var metadataStream = new StreamReader(underlyingZip_.GetEntry("capture/metadata").Open())) {
            string jsonContents = metadataStream.ReadToEnd();
            Record3DMetadata parsedMetadata = (Record3DVideo.Record3DMetadata)JsonUtility.FromJson(jsonContents, typeof(Record3DMetadata));

            // Initialize properties
            this.fps_ = parsedMetadata.fps;
            this.width_ = parsedMetadata.w;
            this.height_ = parsedMetadata.h;

            // Init the intrinsic matrix coeffs
            this.fx_ = parsedMetadata.K[0];
            this.fy_ = parsedMetadata.K[4];
            this.tx_ = parsedMetadata.K[6];
            this.ty_ = parsedMetadata.K[7];
        }

        this.numFrames_ = underlyingZip_.Entries.Count(x => x.FullName.Contains(".depth"));     
        if (this.numFrames == 0) this.numFrames_ = (underlyingZip_.Entries.Count(x => x.FullName.Contains(".bytes")));
        //if (this.numFrames == 0) this.numFrames_ = (underlyingZip_.Entries.Count(x => x.FullName.Contains(".bytes")) / 2);
        //Debug.Log(String.Format("# Available Frames: {0}", this.numFrames_));

        rgbBuffer = new byte[width * height * 3];
        rgbBufferBG = new byte[width * height * 3];
        positionsBuffer = new float[width * height * 4];
        //string p = "jar:file://" + Application.dataPath + "!/assets/momcouch.r3d";
        Debug.Log("Init JPEG");
        turboJPEGHandle = TjInitDecompress();
    }


    public Record3DVideo(string filepath)
    {
        underlyingZip_ = ZipFile.Open(filepath, ZipArchiveMode.Read);
        // Load metadata (FPS, the intrinsic matrix, dimensions)
        using (var metadataStream = new StreamReader(underlyingZip_.GetEntry("metadata").Open()))
        {
            string jsonContents = metadataStream.ReadToEnd();
            Record3DMetadata parsedMetadata = (Record3DVideo.Record3DMetadata)JsonUtility.FromJson(jsonContents, typeof(Record3DMetadata));

            // Initialize properties
            this.fps_ = parsedMetadata.fps;
            this.width_ = parsedMetadata.w;
            this.height_ = parsedMetadata.h;

            // Init the intrinsic matrix coeffs
            this.fx_ = parsedMetadata.K[0];
            this.fy_ = parsedMetadata.K[4];
            this.tx_ = parsedMetadata.K[6];
            this.ty_ = parsedMetadata.K[7];
        }

        this.numFrames_ = underlyingZip_.Entries.Count(x => x.FullName.Contains(".depth"));
        //Debug.Log(String.Format("# Available Frames: {0}", this.numFrames_));

        rgbBuffer = new byte[width * height * 3];
        rgbBufferBG = new byte[width * height * 3];
        positionsBuffer = new float[width * height * 4];
        //turboJPEGHandle = TjInitDecompress();
    }

    byte[] lzfseDepthBuffer;
    public byte[] jpgBuffer;
    public byte[] jpgBufferBG;
    protected int loadedRGBWidth = 1440;
    protected int loadedRGBHeight = 1920;
    //MemoryStream depthStream = new MemoryStream();
    //MemoryStream colorStream = new MemoryStream();

    private long st, et;
    public void LoadFrameData(int frameIdx) {
        st = SystemDataFlowMeasurements.GetUnixTS();
        if (frameIdx >= (numFrames_)) {
            return;
        }

        using (var lzfseDepthStream = underlyingZip_.GetEntry(String.Format("capture/rgbd/{0}.depth", frameIdx)).Open()) {                      
            using (var memoryStream = new MemoryStream()) {
                lzfseDepthStream.CopyTo(memoryStream);
                //lzfseDepthBuffer = memoryStream.ToArray();
                lzfseDepthBuffer = memoryStream.GetBuffer();                
            }
        }

        using (var jpgStream = underlyingZip_.GetEntry(String.Format("capture/rgbd/{0}.jpg", frameIdx)).Open())
        {
            //jpgBuffer = colorStream.GetBuffer();
            using (var memoryStream = new MemoryStream())
            {
                jpgStream.CopyTo(memoryStream);
                //jpgBuffer = memoryStream.ToArray();
                jpgBuffer = memoryStream.GetBuffer();
            }
        }
        //using (var jpgStream = underlyingZip_.GetEntry(String.Format("capture/rgbd/fg/fgColor{0}.jpg", frameIdx)).Open()) {
        //    using (var memoryStream = new MemoryStream()) {
        //        jpgStream.CopyTo(memoryStream);
        //        jpgBuffer = memoryStream.GetBuffer();
        //    }
        //}
        using (var bgJpgStream = underlyingZip_.GetEntry(String.Format("capture/rgbd/bg/bgColor{0}.jpg", frameIdx)).Open()) {
            using (var memoryStream = new MemoryStream()) {
                bgJpgStream.CopyTo(memoryStream);
                jpgBufferBG = memoryStream.GetBuffer();
            }
        }
        //st = SystemDataFlowMeasurements.GetUnixTS();

        // Call the C++ function and pass the loadedRGBWidth and loadedRGBHeight as out parameters
        IntPtr jpgPtr = ConvertByteArrayToIntPtr(jpgBuffer);
        int result = -1;
        unsafe
        {
            fixed (byte* ptr = this.rgbBuffer)
            {
                st = SystemDataFlowMeasurements.GetUnixTS();
                result = tjDecompress2(turboJPEGHandle, jpgPtr, (uint)jpgBuffer.Length, (IntPtr)ptr, loadedRGBWidth, 0, loadedRGBHeight, 0, 0);
                et = SystemDataFlowMeasurements.GetUnixTS();
            }
        }
        if (jpgBufferBG != null)
        {
            IntPtr jpgBGPtr = ConvertByteArrayToIntPtr(jpgBufferBG);
            result = -1;
            unsafe
            {
                fixed (byte* ptr = this.rgbBufferBG)
                {
                    st = SystemDataFlowMeasurements.GetUnixTS();
                    result = tjDecompress2(turboJPEGHandle, jpgBGPtr, (uint)jpgBufferBG.Length, (IntPtr)ptr, loadedRGBWidth, 0, loadedRGBHeight, 0, 0);
                    et = SystemDataFlowMeasurements.GetUnixTS();
                }
            }
        }


        long stdcf = SystemDataFlowMeasurements.GetUnixTS();
        IntPtr decodedDepthDataPtr = IntPtr.Zero;
        ulong totalDecompressDepth = DecompressDepth(lzfseDepthBuffer,
            (uint)lzfseDepthBuffer.Length,
            out decodedDepthDataPtr,
            this.width_, this.height_);

        PopulatePositionBuffer(decodedDepthDataPtr,
            1440, 1920,
            (uint)lzfseDepthBuffer.Length,
            this.positionsBuffer,
            (uint)totalDecompressDepth,
            (uint)this.width_, (uint)this.height_,
            this.fx_, this.fy_, this.tx_, this.ty_);
    }

    public async Task LoadFrameDataAsync(int frameIdx)
    {
        st = SystemDataFlowMeasurements.GetUnixTS();
        if (frameIdx >= numFrames_)
        {
            return;
        }

        // Create a TransformBlock to read depth data into memory
        var loadDepthBlock = new TransformBlock<int, byte[]>(async idx =>
        {
            using (var lzfseDepthStream = underlyingZip_.GetEntry($"capture/rgbd/{idx}.depth").Open())
            {
                using (var memoryStream = new MemoryStream())
                {
                    await lzfseDepthStream.CopyToAsync(memoryStream);
                    return memoryStream.GetBuffer();
                }
            }
        });

        // Create an ActionBlock to decode depth data
        var decodeDepthBlock = new ActionBlock<byte[]>(async depthBuffer =>
        {
            IntPtr decodedDepthDataPtr = IntPtr.Zero;
            ulong totalDecompressDepth = DecompressDepth(depthBuffer, (uint)depthBuffer.Length, out decodedDepthDataPtr, width_, height_);

            long stPPBL = SystemDataFlowMeasurements.GetUnixTS();
            PopulatePositionBuffer(decodedDepthDataPtr, 1440, 1920, (uint)depthBuffer.Length, positionsBuffer, (uint)totalDecompressDepth, (uint)width_, (uint)height_, fx_, fy_, tx_, ty_);
            long etPPBL = SystemDataFlowMeasurements.GetUnixTS();

            // Perform further processing if needed

            //await Task.Yield(); // Simulate additional processing
        });

        // Link the TransformBlock to the ActionBlock
        loadDepthBlock.LinkTo(decodeDepthBlock, new DataflowLinkOptions { PropagateCompletion = true });
        // Post the frame index to the TransformBlock to start the processing
        loadDepthBlock.Post(frameIdx);

        // Create a TransformBlock to read color data into memory
        var loadColorBlock = new TransformBlock<int, byte[]>(async idx =>
        {
            using (var jpgStream = underlyingZip_.GetEntry($"capture/rgbd/{idx}.jpg").Open())
            {
                using (var memoryStream = new MemoryStream())
                {
                    await jpgStream.CopyToAsync(memoryStream);
                    return memoryStream.GetBuffer();
                }
            }
        });

        // Create an ActionBlock to decode color data
        var decodeColorBlock = new ActionBlock<byte[]>(async colorBuffer =>
        {
            int loadedRGBWidth = 1440;
            int loadedRGBHeight = 1920;

            IntPtr jpgPtr = ConvertByteArrayToIntPtr(colorBuffer);
            int result = -1;
            unsafe
            {
                fixed (byte* ptr = rgbBuffer)
                {
                    st = SystemDataFlowMeasurements.GetUnixTS();
                    result = tjDecompress2(turboJPEGHandle, jpgPtr, (uint)colorBuffer.Length, (IntPtr)ptr, loadedRGBWidth, 0, loadedRGBHeight, 0, 0);
                    et = SystemDataFlowMeasurements.GetUnixTS();
                }
            } 
            //await Task.Yield(); // Simulate additional processing
        });
        // Link the TransformBlock to the ActionBlock
        loadColorBlock.LinkTo(decodeColorBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Post the frame index to the TransformBlock to start the processing
        loadColorBlock.Post(frameIdx);

        // Wait for both color and depth processing to complete
        //await Task.WhenAll(decodeDepthBlock.Completion, decodeColorBlock.Completion);

        // Perform any final processing after both color and depth processing is done
        et = SystemDataFlowMeasurements.GetUnixTS();
        Debug.Log($"Time diff async {et-st}");
    }

    public static IntPtr ConvertByteArrayToIntPtr(byte[] byteArray) {
        // Check if the input array is not null
        if (byteArray == null) {
            throw new ArgumentNullException(nameof(byteArray));
        }

        // Pin the byte array in memory to prevent the garbage collector from moving it
        GCHandle handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);

        try {
            // Create an IntPtr from the pinned byte array
            return handle.AddrOfPinnedObject();
        } finally {
            // Release the GCHandle when you're done with the IntPtr
            handle.Free();
        }
    }

    public static byte[] ToByteArray(IntPtr ptr, ulong size) {
        int byteCount = (int)size; // Cast ulong to int for byte count
        byte[] byteArray = new byte[byteCount];

        // Copy data from IntPtr to byte array
        Marshal.Copy(ptr, byteArray, 0, byteCount);

        return byteArray;
    }

    public static void PopulatePositionBufferLocal(IntPtr lzfseDecodedDepthBytesPtr,
                                          int loadedRGBWidth, int loadedRGBHeight,
                                          uint lzfseBytesSize,
                                          float[] poseBuffer,
                                          int outSize,
                                          int width, int height,
                                          float fx, float fy, float tx, float ty) {
        int depthmapDecompressedSizeIfSameResolutionAsRGB = loadedRGBWidth * loadedRGBHeight * sizeof(float);
        bool isDepthTheSameSizeAsRGB = depthmapDecompressedSizeIfSameResolutionAsRGB == outSize;

        int depthWidth = loadedRGBWidth;
        int depthHeight = loadedRGBHeight;

        if (!isDepthTheSameSizeAsRGB) {
            depthWidth = 192;
            depthHeight = 256;
        }

        float ifx = 1.0f / fx;
        float ify = 1.0f / fy;
        float itx = -tx / fx;
        float ity = -ty / fy;

        float invRGBWidth = 1.0f / loadedRGBWidth;
        float invRGBHeight = 1.0f / loadedRGBHeight;

        const int numComponentsPerPointPosition = 4;
        bool needToInterpolate = loadedRGBWidth != depthWidth || loadedRGBHeight != depthHeight;

        // Calculate the number of floats in the depth data
        int numDepthDataFloats = (int)lzfseBytesSize / sizeof(float);
        float[] depthDataPtr = new float[numDepthDataFloats];

        // Copy the data from unmanaged memory to the managed array
        //System.Runtime.InteropServices.Marshal.Copy(lzfseDecodedDepthBytesPtr, depthDataPtr, 0, numDepthDataFloats);
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        Parallel.For(0, height, parallelOptions, i =>
        {
            for (int j = 0; j < width; j++) {
                int idx = loadedRGBWidth * i + j;
                int posBuffIdx = numComponentsPerPointPosition * idx;
                float depthX = invRGBWidth * depthWidth * j;
                float depthY = invRGBHeight * depthHeight * i;
                float currDepth = needToInterpolate ? InterpolateDepth(lzfseDecodedDepthBytesPtr, depthX, depthY, depthWidth, depthHeight) : Marshal.PtrToStructure<float>(IntPtr.Add(lzfseDecodedDepthBytesPtr, idx* sizeof(float))); // lzfseDecodedDepthBytesPtr[idx];

                poseBuffer[posBuffIdx + 0] = (ifx * j + itx) * currDepth;
                poseBuffer[posBuffIdx + 1] = -(ify * i + ity) * currDepth;
                poseBuffer[posBuffIdx + 2] = -currDepth;
                poseBuffer[posBuffIdx + 3] = idx;
            }
        });
    }


    void PopulatePositionBufferLocalOld(IntPtr lzfseDecodedDepthData, int loadedRGBWidth, int loadedRGBHeight,
    uint lzfseBytesSize, float[] poseBuffer, int width, int height, float fx, float fy, float tx, float ty) {
        int depthWidth = loadedRGBWidth;
        int depthHeight = loadedRGBHeight;

        // If the RGB image's resolution is different than the depth map resolution, adjust depthWidth and depthHeight
        if (loadedRGBWidth != depthWidth || loadedRGBHeight != depthHeight) {
            depthWidth = 192;
            depthHeight = 256;
        }

        float ifx = 1.0f / fx;
        float ify = 1.0f / fy;
        float itx = -tx / fx;
        float ity = -ty / fy;

        float invRGBWidth = 1.0f / loadedRGBWidth;
        float invRGBHeight = 1.0f / loadedRGBHeight;

        int numComponentsPerPointPosition = 4;

        bool needToInterpolate = loadedRGBWidth != depthWidth || loadedRGBHeight != depthHeight;

        int floatCount = (int)(lzfseBytesSize / sizeof(float));
        float[] depthDataArray = new float[floatCount];

        // Copy data from IntPtr to float array
        Marshal.Copy(lzfseDecodedDepthData, depthDataArray, 0, floatCount);

        Parallel.For(0, height, i =>
        {
            for (int j = 0; j < width; j++) {
                int idx = loadedRGBWidth * i + j;
                int posBuffIdx = numComponentsPerPointPosition * idx;
                float depthX = invRGBWidth * depthWidth * j;
                float depthY = invRGBHeight * depthHeight * i;

                // Check if idx is within the valid range
                if (idx >= 0 && idx < floatCount) {
                    float currDepth = needToInterpolate
                        ? InterpolateDepth(depthDataArray, depthX, depthY, depthWidth, depthHeight)
                        : depthDataArray[idx];

                    poseBuffer[posBuffIdx + 0] = (ifx * j + itx) * currDepth;
                    poseBuffer[posBuffIdx + 1] = -(ify * i + ity) * currDepth;
                    poseBuffer[posBuffIdx + 2] = -currDepth;
                    poseBuffer[posBuffIdx + 3] = idx;
                } else {
                    // Handle the case when idx is out of bounds
                    // You can log a message or take appropriate action here
                }
            }
        });
    }



    // Converted InterpolateDepth function
    private static float InterpolateDepth(float[] depthData, float x, float y, int imgWidth, int imgHeight) {
        int wX = (int)x;
        int wY = (int)y;
        float fracX = x - wX;
        float fracY = y - wY;

        int topLeftIdx = wY * imgWidth + wX;
        int topRightIdx = wY * imgWidth + Math.Min(wX + 1, imgWidth - 1);
        int bottomLeftIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + wX;
        int bottomRightIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + Math.Min(wX + 1, imgWidth - 1);

        float interpVal =
            (depthData[topLeftIdx] * (1.0f - fracX) + fracX * depthData[topRightIdx]) * (1.0f - fracY) +
            (depthData[bottomLeftIdx] * (1.0f - fracX) + fracX * depthData[bottomRightIdx]) * fracY; 

        return interpVal;
    }

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

        float interpVal =
            (topLeft * (1.0f - fracX) + fracX * topRight) * (1.0f - fracY) +
            (bottomLeft * (1.0f - fracX) + fracX * bottomRight) * fracY;

        return interpVal;
    }


    // Converted PopulatePositionBuffer function
    public static void PopulatePositionBufferLocal(byte[] lzfseDecodedDepthBytes,
                                              int loadedRGBWidth, int loadedRGBHeight,
                                              uint lzfseBytesSize,
                                              float[] poseBuffer,
                                              int outSize,
                                              int width, int height,
                                              float fx, float fy, float tx, float ty) {
        int depthmapDecompressedSizeIfSameResolutionAsRGB = loadedRGBWidth * loadedRGBHeight * sizeof(float);
        bool isDepthTheSameSizeAsRGB = depthmapDecompressedSizeIfSameResolutionAsRGB == outSize;

        int depthWidth = loadedRGBWidth;
        int depthHeight = loadedRGBHeight;

        if (!isDepthTheSameSizeAsRGB) {
            depthWidth = 192;
            depthHeight = 256;
        }

        float ifx = 1.0f / fx;
        float ify = 1.0f / fy;
        float itx = -tx / fx;
        float ity = -ty / fy;

        float invRGBWidth = 1.0f / loadedRGBWidth;
        float invRGBHeight = 1.0f / loadedRGBHeight;

        const int numComponentsPerPointPosition = 4;
        bool needToInterpolate = loadedRGBWidth != depthWidth || loadedRGBHeight != depthHeight;

        // Converting byte array to float array
        float[] depthDataPtr = new float[lzfseDecodedDepthBytes.Length / sizeof(float)];
        Buffer.BlockCopy(lzfseDecodedDepthBytes, 0, depthDataPtr, 0, lzfseDecodedDepthBytes.Length);

        for (int i = 0; i < height; i++) {
            for (int j = 0; j < width; j++) {
                int idx = loadedRGBWidth * i + j;
                int posBuffIdx = numComponentsPerPointPosition * idx;
                float depthX = invRGBWidth * depthWidth * j;
                float depthY = invRGBHeight * depthHeight * i;
                Debug.Log($"dX {depthX} dY {depthY}");
                float currDepth = needToInterpolate ? InterpolateDepth(depthDataPtr, depthX, depthY, depthWidth, depthHeight) : depthDataPtr[idx];

                poseBuffer[posBuffIdx + 0] = (ifx * j + itx) * currDepth;
                poseBuffer[posBuffIdx + 1] = -(ify * i + ity) * currDepth;
                poseBuffer[posBuffIdx + 2] = -currDepth;
                poseBuffer[posBuffIdx + 3] = idx;
            }
        }
    }


    public void LoadFrameDataUncompressed(int frameIdx) {
        //if (frameIdx >= numFrames_) {
        //    return;
        //}

        
        using (var lzfseDepthStream = underlyingZip_.GetEntry(String.Format("dev/rgbd/d/d{0}.bytes", frameIdx)).Open()) {
            using (var memoryStream = new MemoryStream()) {
                lzfseDepthStream.CopyTo(memoryStream);
                lzfseDepthBuffer = memoryStream.GetBuffer();
                int numFloats = lzfseDepthBuffer.Length; // / sizeof(float);
                if (positionsBuffer == null) positionsBuffer = new float[numFloats];
                float[] p = ConvertByteArrayToFloatArray(lzfseDepthBuffer);
                System.Buffer.BlockCopy(p, 0, positionsBuffer, 0, p.Length);
                //Debug.Log($"Record3DVideo::Size of depth buffer {lzfseDepthBuffer.Length}");
            }
        }

        // Decompress the JPG image into a byte buffer
        using (var jpgStream = underlyingZip_.GetEntry(String.Format("dev/rgbd/c/c{0}.bytes", frameIdx)).Open()) {
            using (var memoryStream = new MemoryStream()) {
                jpgStream.CopyTo(memoryStream);
                jpgBuffer = memoryStream.GetBuffer();
                //Debug.Log($"Record3DVideo::Size of depth buffer {jpgBuffer.Length}");
                rgbBuffer = new byte[jpgBuffer.Length];
                Buffer.BlockCopy(jpgBuffer, 0, rgbBuffer, 0, jpgBuffer.Length);
            }
        }

        return;
    }

    private float[] ConvertByteArrayToFloatArray(byte[] byteArray) {
        if (byteArray.Length % sizeof(float) != 0) {
            Debug.LogError("Byte array length is not a multiple of float size.");
            return null;
        }

        float[] floatArray = new float[byteArray.Length / sizeof(float)];

        Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);

        return floatArray;
    }

    public float InterpolateDepthOld(float[] depthData, float x, float y, int imgWidth, int imgHeight) {
        int wX = (int)x;
        int wY = (int)y;
        float fracX = x - (float)wX;
        float fracY = y - (float)wY;

        int topLeftIdx = wY * imgWidth + wX;
        int topRightIdx = wY * imgWidth + Math.Min(wX + 1, imgWidth - 1);
        int bottomLeftIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + wX;
        int bottomRightIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + Math.Min(wX + 1, imgWidth - 1);

        float interpVal =
            (depthData[topLeftIdx] * (1.0f - fracX) + fracX * depthData[topRightIdx]) * (1.0f - fracY) +
            (depthData[bottomLeftIdx] * (1.0f - fracX) + fracX * depthData[bottomRightIdx]) * fracY;

        return interpVal;
    }

    public float InterpolateDepth(byte[] depthDataBytes, float x, float y, int imgWidth, int imgHeight) {
        // Convert the byte array to a float array for interpolation
        float[] depthData = new float[depthDataBytes.Length / sizeof(float)];
        Buffer.BlockCopy(depthDataBytes, 0, depthData, 0, depthDataBytes.Length);

        int wX = (int)x;
        int wY = (int)y;
        float fracX = x - (float)wX;
        float fracY = y - (float)wY;

        int topLeftIdx = wY * imgWidth + wX;
        int topRightIdx = wY * imgWidth + Math.Min(wX + 1, imgWidth - 1);
        int bottomLeftIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + wX;
        int bottomRightIdx = Math.Min(wY + 1, imgHeight - 1) * imgWidth + Math.Min(wX + 1, imgWidth - 1);

        float interpVal =
            (depthData[topLeftIdx] * (1.0f - fracX) + fracX * depthData[topRightIdx]) * (1.0f - fracY) +
            (depthData[bottomLeftIdx] * (1.0f - fracX) + fracX * depthData[bottomRightIdx]) * fracY;

        return interpVal;
    }



}

