using System;
using System.Runtime.InteropServices;

public static class Record3DNative
{
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

    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
    public static extern int tjDecompress2(
        IntPtr handle,
        IntPtr jpegBuf,
        uint jpegSize,
        IntPtr dstBuf,
        int width,
        int pitch,
        int height,
        int pixelFormat,
        int flags
    );

    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
    public static extern IntPtr TjInitDecompress();

    [DllImport(LIBRARY_NAME, EntryPoint = "DecompressDepth", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong DecompressDepth(
        byte[] lzfseDepthBytes,
        UInt32 lzfseBytesSize,
        out IntPtr lzfseDecodedBytes,
        Int32 width,
        Int32 height
    );

    [DllImport(LIBRARY_NAME, EntryPoint = "PopulatePositionBuffer", CallingConvention = CallingConvention.Cdecl)]
    public static extern void PopulatePositionBuffer(
        IntPtr lzfseDecodedDepthBytes,
        int loadedRGBWidth,
        int loadedRGBHeight,
        UInt32 lzfseBytesSize,
        float[] poseBuffer,
        UInt32 outSize,
        UInt32 width,
        UInt32 height,
        float fx,
        float fy,
        float tx,
        float ty
    );

    // Additional native methods from original code can be placed here as needed.
}

