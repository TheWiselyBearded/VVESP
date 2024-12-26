using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;

public static class ZipUtility {
    /// <summary>
    /// Asynchronously loads a ZIP file from StreamingAssets and returns its byte data.
    /// </summary>
    /// <param name="zipFileName">The name of the ZIP file to load.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the byte array of the ZIP file.</returns>
    public static async Task<byte[]> LoadZipBytesAsync(string zipFileName) {
        string filePath = Path.Combine(Application.streamingAssetsPath, zipFileName);

#if UNITY_EDITOR
        // Use standard file I/O in the Unity Editor
        if (File.Exists(filePath)) {
            Debug.Log($"Successfully loaded {zipFileName} in Unity Editor.");
            return await File.ReadAllBytesAsync(filePath);
        } else {
            Debug.LogError($"ZIP file not found at path: {filePath} in Unity Editor.");
            return null;
        }
#elif UNITY_ANDROID
        // On Android, use UnityWebRequest to access StreamingAssets
        using (UnityWebRequest request = UnityWebRequest.Get(filePath))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error loading ZIP file on Android: {request.error}");
                return null;
            }
            else
            {
                Debug.Log($"Successfully loaded {zipFileName} on Android.");
                return request.downloadHandler.data;
            }
        }
#else
        // Use standard file I/O for other platforms
        if (File.Exists(filePath))
        {
            Debug.Log($"Successfully loaded {zipFileName} on non-Android platform.");
            return await File.ReadAllBytesAsync(filePath);
        }
        else
        {
            Debug.LogError($"ZIP file not found at path: {filePath} on non-Android platform.");
            return null;
        }
#endif
        }

    /// <summary>
    /// Asynchronously loads a ZIP file from StreamingAssets and returns a ZipArchive instance.
    /// </summary>
    /// <param name="zipFileName">The name of the ZIP file to load.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ZipArchive instance of the ZIP file.</returns>
    public static async Task<ZipArchive> LoadZipArchiveAsync(string zipFileName) {
        byte[] zipData = await LoadZipBytesAsync(zipFileName);
        if (zipData != null) {
            MemoryStream zipStream = new MemoryStream(zipData);
            return new ZipArchive(zipStream, ZipArchiveMode.Read);
        } else {
            return null;
        }
    }
}