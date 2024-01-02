using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using UnityEngine;
using static Record3DVideo;

public class DataLayer
{
    public BufferBlock<ValueTuple<byte[], byte[]>> encodedBuffer;

    public byte[] rgbBuffer;
    public float[] positionsBuffer;

    protected int loadedRGBWidth = 1440;
    protected int loadedRGBHeight = 1920;

    /// <summary>
    /// The intrinsic matrix coefficients.
    /// </summary>
    private float fx_, fy_, tx_, ty_;
    private int width_;
    public int width { get { return width_; } }

    private int height_;
    public int height { get { return height_; } }

    private long st, et;

    #region EXTERNAL_LIBRARY

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string JPG_LIBRARY_NAME = "turbojpeg";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string JPG_LIBRARY_NAME = "libturbojpeg";
#elif UNITY_ANDROID
    private const string JPG_LIBRARY_NAME = "jpeg-turbo";
#endif


#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string LIBRARY_NAME = "record3d_unity_playback";
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private const string LIBRARY_NAME = "librecord3d_unity_playback.dylib";
#elif UNITY_ANDROID
    private const string LIBRARY_NAME = "record3d_unity_playback"; //"record3d_unity_playback.dll";
#else
#error "Unsupported platform!"
#endif

    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
    private static extern int tjDecompress2(IntPtr handle, IntPtr jpegBuf, uint jpegSize, IntPtr dstBuf, int width, int pitch, int height, int pixelFormat, int flags);

    [DllImport(JPG_LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
    public static extern IntPtr TjInitDecompress();

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

    #endregion

    public DataLayer()
    {
        DataflowBlockOptions dataflowBlockOptions = new DataflowBlockOptions();
        dataflowBlockOptions.BoundedCapacity = 30;
        encodedBuffer = new BufferBlock<ValueTuple<byte[], byte[]>>(dataflowBlockOptions);
        Init();
    }

    public void Init()
    {
        Debug.Log("Init JPEG");
        turboJPEGHandle = TjInitDecompress();
    }

    public void SetCameraMetadata(Record3DMetadata parsedMetadata)
    {
        // Initialize properties
        //this.fps_ = parsedMetadata.fps;
        this.width_ = parsedMetadata.w;
        this.height_ = parsedMetadata.h;

        // Init the intrinsic matrix coeffs
        this.fx_ = parsedMetadata.K[0];
        this.fy_ = parsedMetadata.K[4];
        this.tx_ = parsedMetadata.K[6];
        this.ty_ = parsedMetadata.K[7];

        rgbBuffer = new byte[width * height * 3];
        positionsBuffer = new float[width * height * 4];
    }

    /// <summary>
    /// thread started asynchronously from a parent component
    /// </summary>
    /// <returns></returns>
    public async Task ConsumerCaptureData()
    {
        while (await encodedBuffer.OutputAvailableAsync())
        {    // subscribe for buffer write 

            while (encodedBuffer.TryReceive(out ValueTuple<byte[], byte[]> frameDatablock))
            {
                //Debug.Log($"Color Data Length {frameDatablock.Item1.Length}, Depth Data Length {frameDatablock.Item2.Length}");
                /*IntPtr jpgPtr = ConvertByteArrayToIntPtr(frameDatablock.Item1);
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
                

                long stdcf = SystemDataFlowMeasurements.GetUnixTS();
                IntPtr decodedDepthDataPtr = IntPtr.Zero;
                ulong totalDecompressDepth = DecompressDepth(frameDatablock.Item2,
                    (uint)frameDatablock.Item2.Length,
                    out decodedDepthDataPtr,
                    this.width_, this.height_);

                PopulatePositionBuffer(decodedDepthDataPtr,
                    1440, 1920,
                    (uint)frameDatablock.Item2.Length,
                    this.positionsBuffer,
                    (uint)totalDecompressDepth,
                    (uint)this.width_, (uint)this.height_,
                    this.fx_, this.fy_, this.tx_, this.ty_);

                await Task.Delay(33);*/

                try
                {
                    var decodeColorBlock = new ActionBlock<byte[]>(async colorBuffer =>
                    {
                        int loadedRGBWidth = 1440;
                        int loadedRGBHeight = 1920;
                        //await Task.Delay(33);
                        IntPtr jpgPtr = VVP_Utilities.ConvertByteArrayToIntPtr(colorBuffer);
                        int result = -1;
                        unsafe
                        {
                            fixed (byte* ptr = rgbBuffer)
                            {
                                st = SystemDataFlowMeasurements.GetUnixTS();
                                result = tjDecompress2(turboJPEGHandle, jpgPtr, (uint)colorBuffer.Length, (IntPtr)ptr, loadedRGBWidth, 0, loadedRGBHeight, 0, 0);
                                //var colorDecodeT = await Task.Run(() => result = tjDecompress2(turboJPEGHandle, jpgPtr, (uint)colorBuffer.Length, (IntPtr)ptr, loadedRGBWidth, 0, loadedRGBHeight, 0, 0));
                                et = SystemDataFlowMeasurements.GetUnixTS();
                            }
                        }
                        //await Task.Yield(); // Simulate additional processing
                    });
                    decodeColorBlock.Post(frameDatablock.Item1);
                }
                catch (DllNotFoundException ex) { Debug.Log("Whoops, error!"); }
                catch (EntryPointNotFoundException ex) { Debug.Log("Whoops, error!"); }
                catch (Exception ex) { Debug.Log("Whoops, error!"); }


                // Create an ActionBlock to decode depth data
                var decodeDepthBlock = new ActionBlock<byte[]>(async depthBuffer =>
                {
                    IntPtr decodedDepthDataPtr = IntPtr.Zero;
                    //ulong totalDecompressDepth = 0;
                    //var decodeT = await Task.Run(() => totalDecompressDepth = DecompressDepth(depthBuffer, (uint)depthBuffer.Length, out decodedDepthDataPtr, width_, height_));
                    try
                    {
                        ulong totalDecompressDepth = DecompressDepth(depthBuffer, (uint)depthBuffer.Length, out decodedDepthDataPtr, width_, height_);
                        long stPPBL = SystemDataFlowMeasurements.GetUnixTS();
                        PopulatePositionBuffer(decodedDepthDataPtr, 1440, 1920, (uint)depthBuffer.Length, positionsBuffer, (uint)totalDecompressDepth, (uint)width_, (uint)height_, fx_, fy_, tx_, ty_);
                        long etPPBL = SystemDataFlowMeasurements.GetUnixTS();
                    }
                    catch (DllNotFoundException ex) { Debug.Log("Whoops, error!"); }
                    catch (EntryPointNotFoundException ex) { Debug.Log("Whoops, error!"); }
                    catch (Exception ex) { Debug.Log("Whoops, error!"); }

                    //await Task.Yield(); // Simulate additional processing
                });
                decodeDepthBlock.Post(frameDatablock.Item2);
            }
        }
    }
}
