using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.IO;

public class FileTransferClient : MonoBehaviour {
    public string serverIP = "127.0.0.1"; // Replace with the server's IP address
    public int serverPort = 12345;         // Replace with the server's port number

    private void Start() {
        ConnectToServer();
    }

    private void ConnectToServer() {
        try {
            string savePath = @"C:\Users\alire\Documents\Projects\R3D\Assets\StreamingAssets\momcouchdownload.r3d";

            // Create a TCP client socket
            TcpClient client = new TcpClient(serverIP, serverPort);
            Debug.Log("Connected to server.");

            // Receive the file data
            using (NetworkStream stream = client.GetStream()) {
                byte[] buffer = new byte[1024];
                int bytesRead;

                // Ensure the target directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));

                using (FileStream fileStream = File.Create(savePath)) {
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0) {
                        fileStream.Write(buffer, 0, bytesRead);
                    }

                    Debug.Log("File received and saved: " + savePath);
                }
            }

            // Close the client socket
            client.Close();
        } catch (Exception e) {
            Debug.LogError("Error: " + e.Message);
        }
    }
}
