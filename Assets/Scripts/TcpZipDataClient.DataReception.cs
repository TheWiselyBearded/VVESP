using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading.Tasks;

public partial class TcpZipDataClient
{
    private readonly MemoryStream memoryStream = new MemoryStream();

    /// <summary>
    /// Handles incoming server responses based on the expected response type
    /// </summary>
    private void HandleServerResponse()
    {
        if (expectedResponse != ServerResponseExpectation.None && stream.DataAvailable)
        {
            ReceiveServerResponse();
        }
    }

    /// <summary>
    /// Processes server responses based on the expected type (ZIP file or JSON list)
    /// </summary>
    private void ReceiveServerResponse()
    {
        try
        {
            switch (expectedResponse)
            {
                case ServerResponseExpectation.ZipFile:
                    ReceiveZipFile();
                    break;
                case ServerResponseExpectation.JsonList:
                    ReceiveJsonList();
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error receiving server response: {e.Message}");
        }
        finally
        {
            expectedResponse = ServerResponseExpectation.None;
            memoryStream.SetLength(0);
        }
    }

    #region ZIP File Reception
    private void ReceiveZipFile()
    {
        byte[] fileSizeBytes = new byte[4];
        stream.Read(fileSizeBytes, 0, 4);
        int fileSize = BitConverter.ToInt32(fileSizeBytes, 0);

        ReceiveFileData(fileSize);
        ProcessReceivedZipFile();
    }

    /// <summary>
    /// Receives file data in chunks and writes to memory stream
    /// </summary>
    private void ReceiveFileData(int fileSize)
    {
        byte[] buffer = new byte[4096];
        int totalBytesReceived = 0;
        memoryStream.SetLength(0);

        while (totalBytesReceived < fileSize)
        {
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
                throw new Exception("Connection closed while receiving data");

            memoryStream.Write(buffer, 0, bytesRead);
            totalBytesReceived += bytesRead;
        }
    }

    /// <summary>
    /// Processes the received ZIP file data and updates the playback components
    /// </summary>
    private void ProcessReceivedZipFile() {
        var zipMemoryStream = new MemoryStream(memoryStream.ToArray());
        var archive = new ZipArchive(zipMemoryStream);

        // Save to StreamingAssets if enabled
        if (saveToDisk) {
            // Ensure the filename is safe for the file system
            string sanitizedFilename = string.Join("_", filenameToRequest.Split(Path.GetInvalidFileNameChars()));

            // Append .zip if not already present
            if (!sanitizedFilename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                sanitizedFilename += ".zip";
            }

            string path = Path.Combine(Application.streamingAssetsPath, sanitizedFilename);

            try {
                // Reset the memory stream position before copying
                zipMemoryStream.Position = 0;

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write)) {
                    zipMemoryStream.CopyTo(fileStream);
                }

                Debug.Log($"Saved ZIP file to: {path}");
            } catch (Exception ex) {
                Debug.LogError($"Failed to save ZIP file: {ex.Message}");
            }
        }

        // Proceed with existing logic
        var tcs = new TaskCompletionSource<bool>();
        dispatcher.Enqueue(async () => {
            try {
                await playback.LoadVideoFromZipAsync(archive, selectCapture?.filename);
                videoController.SetReadyState();
                tcs.SetResult(true);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        });

        Task.Run(async () => {
            try {
                await tcs.Task;
                Debug.Log("Zip file loaded successfully (async).");
            } catch (Exception e) {
                Debug.LogError($"Error in loading .zip file: {e.Message}");
            }
        });
    }




    #endregion

    #region JSON Reception
    /// <summary>
    /// Receives and processes the JSON list of available captures
    /// </summary>
    private void ReceiveJsonList()
    {
        byte[] buffer = new byte[4096];
        while (stream.DataAvailable)
        {
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            memoryStream.Write(buffer, 0, bytesRead);
        }

        string json = Encoding.UTF8.GetString(memoryStream.ToArray());
        CaptureList captureList = JsonConvert.DeserializeObject<CaptureList>(json);

        ProcessCaptureList(captureList);
    }

    /// <summary>
    /// Processes the received capture list and updates the UI
    /// </summary>
    private void ProcessCaptureList(CaptureList captureList)
    {
        foreach (var capture in captureList.captures)
        {
            Debug.Log($"Found file: {capture.filename}");
            captures.Add(capture);
        }

        if (dispatcher != null && controllerOVR != null)
        {
            dispatcher.Enqueue(() =>
            {
                controllerOVR.InitializeCaptureButtons(captureList.captures);
            });
        }
    }
    #endregion
}

