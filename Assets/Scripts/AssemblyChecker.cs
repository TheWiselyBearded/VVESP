using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public class AssemblyChecker : MonoBehaviour {
    [DllImport("record3d_unity_playback")]
    private static extern void DecompressFrame(byte[] jpgBytes, UInt32 jpgBytesSize, byte[] lzfseDepthBytes, UInt32 lzfseBytesSize, byte[] rgbBuffer, float[] poseBuffer, Int32 width, Int32 height, float fx, float fy, float tx, float ty);

    public string checkAssembly;
    void Start() {
        if (checkAssembly == string.Empty || checkAssembly == "") checkAssembly = "librecord3d_unity_playback.so";
        CheckIfAssemblyIsLoaded(checkAssembly);
    }

    void CheckIfAssemblyIsLoaded(string assemblyName) {
        Assembly loadedAssembly = null;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            if (assembly.GetName().Name == assemblyName) {
                loadedAssembly = assembly;
                break;
            }
        }

        if (loadedAssembly != null) {
            Debug.Log($"Assembly {assemblyName} is loaded!");
        } else {
            Debug.LogError($"Assembly {assemblyName} is not loaded.");
        }
    }
}
