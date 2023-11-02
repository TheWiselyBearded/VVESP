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

    public float[] positionsBuffer;

    [System.Serializable]
    public struct Record3DMetadata
    {
        public int w;
        public int h;
        public List<float> K;
        public int fps;
    }

    //private const string LIBRARY_NAME = "record3d_unity_playback"; //"record3d_unity_playback.dll";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string LIBRARY_NAME = "record3d_unity_playback";

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

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void DecompressFrame(byte[] jpgBytes, UInt32 jpgBytesSize, byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, byte[] rgbBuffer, float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    public Record3DVideo(ZipArchive z) {
        /*string path = Path.Combine(Application.streamingAssetsPath, "momcouch.r3d");
        var loadingRequest = UnityWebRequest.Get(path);
        loadingRequest.SendWebRequest();
        while (!loadingRequest.isDone && !loadingRequest.isNetworkError && !loadingRequest.isHttpError) ;
        string result = System.Text.Encoding.UTF8.GetString(loadingRequest.downloadHandler.data);
        Debug.Log($"RESULT {result}");*/

        /*string filePath = Path.Combine("jar:file://" + Application.dataPath + "!assets/", "momcouch.r3d");
        Debug.Log($"ATTEMPT {filePath}");
        var www = new WWW(filePath);
        //yield return www;
        if (!string.IsNullOrEmpty(www.error)) {
            Debug.LogError("Can't read");
        }
        Debug.Log($"FILEPATH {filePath}");*/

        //currentVideo_ = new Record3DVideo(path);        

        //string[] d = BetterStreamingAssets.GetFiles("\\", "momcouch.r3d", SearchOption.AllDirectories);
         // new ZipArchive( //ZipFile.Open(d[0], ZipArchiveMode.Read);
        underlyingZip_ = z;

        // Load metadata (FPS, the intrinsic matrix, dimensions)
        using (var metadataStream = new StreamReader(underlyingZip_.GetEntry("test/metadata").Open())) {
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
        if (this.numFrames == 0) this.numFrames_ = (underlyingZip_.Entries.Count(x => x.FullName.Contains(".bytes")) / 2);
        //Debug.Log(String.Format("# Available Frames: {0}", this.numFrames_));

        rgbBuffer = new byte[width * height * 3];
        positionsBuffer = new float[width * height * 4];
        //string p = "jar:file://" + Application.dataPath + "!/assets/momcouch.r3d";

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
        positionsBuffer = new float[width * height * 4];
    }

    byte[] lzfseDepthBuffer;
    byte[] jpgBuffer;
    //MemoryStream depthStream = new MemoryStream();
    //MemoryStream colorStream = new MemoryStream();

    private long st, et;
    public void LoadFrameData(int frameIdx)
    {
        if (frameIdx >= numFrames_)
        {
            return;
        }

        // Decompress the LZFSE depth data into a byte buffer
        //byte[] lzfseDepthBuffer;
        
        using (var lzfseDepthStream = underlyingZip_.GetEntry(String.Format("test/rgbd/{0}.depth", frameIdx)).Open())
        {
            //lzfseDepthStream.CopyTo(depthStream);
            //lzfseDepthBuffer = depthStream.GetBuffer();            
            using (var memoryStream = new MemoryStream()) {
                lzfseDepthStream.CopyTo(memoryStream);
                //lzfseDepthBuffer = memoryStream.ToArray();
                lzfseDepthBuffer = memoryStream.GetBuffer();
                //Debug.Log($"Record3DVideo::Size of depth buffer {lzfseDepthBuffer.Length}");
            }
            
            //if (lzfseDepthBuffer == null) lzfseDepthBuffer = new byte[57636];
            //lzfseDepthStream.Read(lzfseDepthBuffer);
        }

        // Decompress the JPG image into a byte buffer
        //byte[] jpgBuffer;
        using (var jpgStream = underlyingZip_.GetEntry(String.Format("test/rgbd/{0}.jpg", frameIdx)).Open())
        {
            //jpgStream.CopyTo(colorStream);
            //jpgBuffer = colorStream.GetBuffer();
            using (var memoryStream = new MemoryStream()) {
                jpgStream.CopyTo(memoryStream);
                //jpgBuffer = memoryStream.ToArray();
                jpgBuffer = memoryStream.GetBuffer();
            }
        }
        // Decompress the LZFSE depth map archive, create point cloud and load the JPEG image        
        /*DecompressFrameData(jpgBuffer,
            (uint)jpgBuffer.Length,
            lzfseDepthBuffer,
            (uint)lzfseDepthBuffer.Length,
            out rgbBuffer,
            out positionsBuffer,
            this.width_, this.height_,
            this.fx_, this.fy_, this.tx_, this.ty_);*/
        st = SystemDataFlowMeasurements.GetUnixTS();
        DecompressFrame(jpgBuffer,
            (uint)jpgBuffer.Length,
            lzfseDepthBuffer,
            (uint)lzfseDepthBuffer.Length,
            this.rgbBuffer,
            this.positionsBuffer,
            this.width_, this.height_,
            this.fx_, this.fy_, this.tx_, this.ty_);
        et = SystemDataFlowMeasurements.GetUnixTS();
        //Debug.Log($"decompression time {et - st}");
        //Debug.Log($"Out positions size {positionsBuffer.Length-lzfseDepthBuffer.Length}");
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

    public float InterpolateDepth(float[] depthData, float x, float y, int imgWidth, int imgHeight) {
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

    //byte[] jpgBytes, UInt32 jpgBytesSize, byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, byte[] rgbBuffer, float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty
    public void DecompressFrameData(byte[] jpgBytes,
                                        UInt32 jpgBytesSize,
                                       byte[] lzfseDepthBytes,
                                       UInt32 lzfseBytesSize,
                                       out byte[] rgbBuffer,
                                       out float[] poseBuffer,
                                       int width, int height,
                                       float fx, float fy, float tx, float ty) {
        // 1. Decompress JPG bytes (You need to implement the JPG decoding yourself or use a library)
        // This part is dependent on your specific needs and the source of the JPG bytes.
        // Assuming the JPG bytes are already in the correct format and size for this example.
        int loadedRGBWidth = width;    // Set these to the correct values for your image
        int loadedRGBHeight = height;  // Set these to the correct values for your image
        int loadedChannels = 3;        // Assuming RGB

        rgbBuffer = jpgBytes;  // This is a simplification
        Buffer.BlockCopy(jpgBytes, 0, rgbBuffer, 0, (int) jpgBytesSize);

        // 2. Decompress the depth map using LZFSE
        int decompressedDepthMapSize = width * height * sizeof(float);
        byte[] depthMapBytes = LZFSESharp.LZFSE.Decompress(lzfseDepthBytes, decompressedDepthMapSize); // new byte[decompressedDepthMapSize];
        int outSize = depthMapBytes.Length;
        //byte[] depthMapBytes = new byte[decompressedDepthMapSize];
        //int outSize = LzfseCompressor.Decompress(lzfseDepthBytes, depthMapBytes);
        //Debug.Log($"Out size {outSize}");

        // Convert byte array to float array
        float[] depthMap = new float[decompressedDepthMapSize];
        Buffer.BlockCopy(depthMapBytes, 0, depthMap, 0, outSize);


        int depthmapDecompressedSizeIfSameResolutionAsRGB = loadedRGBWidth * loadedRGBHeight * sizeof(float);
        bool isDepthTheSameSizeAsRGB = depthmapDecompressedSizeIfSameResolutionAsRGB == outSize;

        int depthWidth = loadedRGBWidth;
        int depthHeight = loadedRGBHeight;

        // If the RGB image's resolution is different than the depth map resolution, then we are most probably working with
        // a higher-quality LiDAR video (RGB: 720x960 px, Depth: 192x256 px)
        if (!isDepthTheSameSizeAsRGB) {
            depthWidth = 192;
            depthHeight = 256;
        }

        // 3. Populate the pose buffer
        poseBuffer = new float[width * height * 4];

        float ifx = 1.0f / fx;
        float ify = 1.0f / fy;
        float itx = -tx / fx;
        float ity = -ty / fy;

        float invRGBWidth = 1.0f / loadedRGBWidth;
        float invRGBHeight = 1.0f / loadedRGBHeight;

        const int numComponentsPerPointPosition = 4;

        bool needToInterpolate = loadedRGBWidth != width || loadedRGBHeight != height;
        for (int i = 0; i < height; i++) {
            for (int j = 0; j < width; j++) {
                int idx = loadedRGBWidth * i + j;
                int posBuffIdx = numComponentsPerPointPosition * idx;
                float depthX = invRGBWidth * width * j;
                float depthY = invRGBHeight * height * i;
                float currDepth = needToInterpolate ? InterpolateDepth(depthMap, depthX, depthY, width, height) : depthMap[idx];

                poseBuffer[posBuffIdx + 0] = (ifx * j + itx) * currDepth;
                poseBuffer[posBuffIdx + 1] = -(ify * i + ity) * currDepth;
                poseBuffer[posBuffIdx + 2] = -currDepth;
                poseBuffer[posBuffIdx + 3] = idx;
            }
        }
    }
}

