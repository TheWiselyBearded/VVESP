using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

public class ImageDecoder : MonoBehaviour {

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string LIBRARY_NAME = "turbojpeg";

#elif UNITY_ANDROID
    private const string LIBRARY_NAME = "jpeg-turbo";
#endif

    //[DllImport("turbojpeg")]
    //private static extern int tj3Decompress8(byte[] jpegBuf, ulong jpegSize, byte[] dstBuf, int pitch, int pixelFormat);

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjDecompress2")]
    private static extern int tjDecompress2(IntPtr handle, IntPtr jpegBuf, uint jpegSize, IntPtr dstBuf, int width, int pitch, int height, int pixelFormat, int flags);
    //private static extern int tjDecompress2(IntPtr handle, IntPtr jpegBuf, uint jpegSize, IntPtr dstBuf, out int width, out int pitch, out int height, int pixelFormat, int flags);

    //[DllImport("turbojpeg")]
    //private static extern IntPtr tj3Init();

    [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tjInitDecompress")]
    public static extern IntPtr TjInitDecompress();

    // Input texture assigned via the Inspector
    public Texture2D inputTexture;

    // Output texture to store the decompressed image
    public Texture2D outputTexture;

    public RawImage encoded;
    public RawImage decoded;
    public TextMeshProUGUI debugText;
    public string uiTextBuilder;

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            DecodeImgTurbo();
        }
    }

    // Start is called before the first frame update

    public void DecodeImgTurbo() {
        if (inputTexture == null) {
            Debug.LogError("Input texture is not assigned.");
            return;
        }

        byte[] jpegData = inputTexture.EncodeToJPG();
        //byte[] jpegData = inputTexture.GetRawTextureData<byte>().ToArray();
        //Texture2D t = encoded.texture as Texture2D;
        //byte[] jpegData = t.EncodeToJPG();
        // Initialize TurboJPEG
        //IntPtr turboJPEGHandle = tj3Init();
        uiTextBuilder = debugText.text;
        uiTextBuilder += "\nAttempt tp init decoder";
        debugText.SetText(uiTextBuilder);
        IntPtr turboJPEGHandle = TjInitDecompress();

        if (turboJPEGHandle == IntPtr.Zero) {
            Debug.Log("TurboJPEG initialization failed.");
            uiTextBuilder += "\nTurboJPEG initialization failed.";
            debugText.SetText(uiTextBuilder);
            return;
        } else {
            uiTextBuilder += "\nInit decoder successfully.";
            debugText.SetText(uiTextBuilder);
        }

        // Define the output buffer for the decompressed image
        int width = inputTexture.width;
        int height = inputTexture.height;
        int pixelFormat = (int)0; //TJPixelFormat.TJPF_RGB; // Adjust as needed
        int bytesPerPixel = 3; // 3 bytes per pixel for RGB

        byte[] decompressedData = new byte[width * height * bytesPerPixel];

        // Call the decompression function
        //int result = tj3Decompress8(jpegData, (ulong)jpegData.Length, decompressedData, 0, pixelFormat);
        //int result = tjDecompress2(turboJPEGHandle, jpegData, (ulong)jpegData.Length, decompressedData, 0, pixelFormat);
        IntPtr jpgPtr = ConvertByteArrayToIntPtr(jpegData);
        int result = -1;
        long st, et;
        unsafe {
            fixed (byte* ptr = decompressedData) {
                st = SystemDataFlowMeasurements.GetUnixTS();
                result = tjDecompress2(turboJPEGHandle, jpgPtr, (uint) jpegData.Length, (IntPtr)ptr, width, 0, height, pixelFormat, 0);                
                et = SystemDataFlowMeasurements.GetUnixTS();
            }
        }

        if (result != 0) {
            Debug.LogError("TurboJPEG decompression failed.");
            uiTextBuilder += "\nTurboJPEG decompression failed.";
            debugText.SetText(uiTextBuilder);
            return;
        } else {
            uiTextBuilder += $"\ndecoded image successfully in {et - st}";
            debugText.SetText(uiTextBuilder);
        }

        // Create an output texture from the decompressed data
        outputTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        outputTexture.LoadRawTextureData(decompressedData);
        outputTexture.Apply();

        decoded.texture = outputTexture;

        // Display or use the decompressed texture as needed
        //GetComponent<Renderer>().material.mainTexture = outputTexture;
    }

    public void DecodeImgUnity() {
        if (inputTexture == null) {
            Debug.LogError("Input texture is not assigned.");
            return;
        }

        // Convert the input texture to a JPEG byte array (assuming it's a valid JPEG)
        byte[] jpegData = inputTexture.EncodeToJPG();
        //byte[] jpegData = inputTexture.GetRawTextureData<byte>().ToArray();

        // Initialize TurboJPEG
        //IntPtr turboJPEGHandle = tj3Init();

        int width = inputTexture.width;
        int height = inputTexture.height;
        int bytesPerPixel = 3; // 3 bytes per pixel for RGB

        // Create an output texture from the decompressed data
        outputTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        long st, et;
        st = SystemDataFlowMeasurements.GetUnixTS();
        outputTexture.LoadImage(jpegData);
        et = SystemDataFlowMeasurements.GetUnixTS();
        outputTexture.Apply();

        uiTextBuilder += $"\ndecoded image successfully in {et - st}";
        debugText.SetText(uiTextBuilder);

        decoded.texture = outputTexture;

        // Display or use the decompressed texture as needed
        //GetComponent<Renderer>().material.mainTexture = outputTexture;
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



}
