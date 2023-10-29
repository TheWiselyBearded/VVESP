using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class NativePluginTest : MonoBehaviour {
    [DllImport("record3d_unity_playback")]
    private static extern void DecompressFrame(byte[] jpgBytes, UInt32 jpgBytesSize, byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, byte[] rgbBuffer, float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);
    //public static extern int SomeFunctionFromYourLibrary();

    //void Start() {
    //    int result = SomeFunctionFromYourLibrary();
    //    Debug.Log("Result from native library: " + result);
    //}
}

