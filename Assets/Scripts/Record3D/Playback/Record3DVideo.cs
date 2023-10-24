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

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string LIBRARY_NAME = "librecord3d_unity_playback.dylib";

#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_ANDROID
        private const string LIBRARY_NAME = "record3d_unity_playback.dll";

#else
#error "Unsupported platform!"
#endif

    [DllImport(LIBRARY_NAME)]
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
        using (var metadataStream = new StreamReader(underlyingZip_.GetEntry("dev/metadata").Open())) {
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
    public void LoadFrameData(int frameIdx)
    {
        if (frameIdx >= numFrames_)
        {
            return;
        }

        // Decompress the LZFSE depth data into a byte buffer
        //byte[] lzfseDepthBuffer;
        
        using (var lzfseDepthStream = underlyingZip_.GetEntry(String.Format("rgbd/{0}.depth", frameIdx)).Open())
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
        using (var jpgStream = underlyingZip_.GetEntry(String.Format("rgbd/{0}.jpg", frameIdx)).Open())
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
        DecompressFrame(jpgBuffer,
            (uint)jpgBuffer.Length,
            lzfseDepthBuffer,
            (uint)lzfseDepthBuffer.Length,
            this.rgbBuffer,
            this.positionsBuffer,
            this.width_, this.height_,
            this.fx_, this.fy_, this.tx_, this.ty_);
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

    static float InterpolateDepth(float[] depthData, float x, float y, int imgWidth, int imgHeight) {
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

    static void DecompressFrame(byte[] jpgBytes, byte[] lzfseDepthBytes, byte[] rgbBuffer, float[] poseBuffer, int width, int height, float fx, float fy, float tx, float ty) {
        
    }
}
