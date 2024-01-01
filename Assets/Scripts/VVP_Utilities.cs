using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class VVP_Utilities
{
    public static IntPtr ConvertByteArrayToIntPtr(byte[] byteArray)
    {
        // Check if the input array is not null
        if (byteArray == null)
        {
            throw new ArgumentNullException(nameof(byteArray));
        }

        // Pin the byte array in memory to prevent the garbage collector from moving it
        GCHandle handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);

        try
        {
            // Create an IntPtr from the pinned byte array
            return handle.AddrOfPinnedObject();
        }
        finally
        {
            // Release the GCHandle when you're done with the IntPtr
            handle.Free();
        }
    }

    public static void ReleaseNativeMemory(IntPtr memoryPtr)
    {
        // Check if the memory pointer is valid (not null or IntPtr.Zero)
        if (memoryPtr != IntPtr.Zero)
        {
            // Free the native memory using Marshal.FreeHGlobal
            Marshal.FreeHGlobal(memoryPtr);
        }
    }

    public static byte[] ToByteArray(IntPtr ptr, ulong size)
    {
        int byteCount = (int)size; // Cast ulong to int for byte count
        byte[] byteArray = new byte[byteCount];

        // Copy data from IntPtr to byte array
        Marshal.Copy(ptr, byteArray, 0, byteCount);

        return byteArray;
    }

    public static float[] ConvertByteArrayToFloatArray(byte[] byteArray)
    {
        if (byteArray.Length % sizeof(float) != 0)
        {
            Debug.LogError("Byte array length is not a multiple of float size.");
            return null;
        }

        float[] floatArray = new float[byteArray.Length / sizeof(float)];

        Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);

        return floatArray;
    }


    // Method to convert the unmanaged array to a managed array and then free the unmanaged array.
    public static float[] GetManagedArray(IntPtr unmanagedArray, int length)
    {
        float[] managedArray = new float[length];
        Marshal.Copy(unmanagedArray, managedArray, 0, length);
        Marshal.FreeCoTaskMem(unmanagedArray); // Assume the C++ side uses CoTaskMemAlloc or compatible for allocation.
        return managedArray;
    }
}
