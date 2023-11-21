using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System;

public class ReceiveAndExtractZip : MonoBehaviour {
    public string serverAddress = "127.0.0.1"; // Replace with your server address
    public int serverPort = 12345;            // Replace with your server port
    public Record3DPlayback playback;
    public VideoController videoController;
    private ZipArchive zipArchive;

    
    public void ConnectedToServer() {
        try {
            TcpClient client = new TcpClient(serverAddress, serverPort);
            NetworkStream stream = client.GetStream();

            // Create a MemoryStream to store the received data
            MemoryStream memoryStream = new MemoryStream();

            byte[] buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
                memoryStream.Write(buffer, 0, bytesRead);
            }

            // Reset the memory stream position to the beginning
            memoryStream.Seek(0, SeekOrigin.Begin);

            // Create a ZipArchive from the received data in memory
            zipArchive = new ZipArchive(memoryStream);

            // You can access files and folders within the ZIP archive here
            /*foreach (ZipArchiveEntry entry in zipArchive.Entries) {
                Debug.Log("File in ZIP: " + entry.FullName);
                // You can extract the contents of individual entries if needed
                // using entry.Open()
            }*/
            playback.LoadVid(zipArchive);
            videoController.SetReadyState();
            stream.Close();
            client.Close();
        } catch (Exception e) {
            Debug.LogError("Error: " + e.Message);
        }
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) {
            ConnectedToServer();
        }
    }

    void OnDestroy() {
        // Close the ZipArchive when it's no longer needed
        if (zipArchive != null) {
            zipArchive.Dispose();
        }
    }
}
