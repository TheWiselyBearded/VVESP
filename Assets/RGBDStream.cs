using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

public class RGBDStream : MonoBehaviour {
    public string serverIP = "127.0.0.1"; // Replace with the server's IP address
    public int serverPort = 12345;         // Replace with the server's port number

    [SerializeField] public Texture2D receivedTexture;
    private bool isReceiving = false;

    private void Start() {
        receivedTexture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        ConnectToServer();
    }

    private void ConnectToServer() {
        isReceiving = true;

        Thread receiveThread = new Thread(() => {
            try {
                // Create a TCP client socket
                TcpClient client = new TcpClient(serverIP, serverPort);
                Debug.Log("Connected to server.");

                using (NetworkStream stream = client.GetStream()) {
                    byte[] buffer = new byte[1024];
                    int bytesRead;

                    while (isReceiving) {
                        // Receive the size of the .jpg file
                        bytesRead = stream.Read(buffer, 0, 4);
                        if (bytesRead == 0)
                            break;
                        int jpgSize = BitConverter.ToInt32(buffer, 0);

                        // Receive the .jpg file data
                        byte[] jpgData = new byte[jpgSize];
                        int totalJpgBytesRead = 0;
                        while (totalJpgBytesRead < jpgSize) {
                            bytesRead = stream.Read(jpgData, totalJpgBytesRead, jpgSize - totalJpgBytesRead);
                            if (bytesRead == 0)
                                break;
                            totalJpgBytesRead += bytesRead;
                        }
                        Debug.Log($"Received .jpg file with size: {jpgSize} bytes");
                        // Load the .jpg data into the Texture2D
                        receivedTexture.LoadImage(jpgData);
                        receivedTexture.Apply();
                        // Receive the size of the .depth file
                        bytesRead = stream.Read(buffer, 0, 4);
                        if (bytesRead == 0)
                            break;
                        int depthSize = BitConverter.ToInt32(buffer, 0);

                        // Receive the .depth file data
                        byte[] depthData = new byte[depthSize];
                        int totalDepthBytesRead = 0;
                        while (totalDepthBytesRead < depthSize) {
                            bytesRead = stream.Read(depthData, totalDepthBytesRead, depthSize - totalDepthBytesRead);
                            if (bytesRead == 0)
                                break;
                            totalDepthBytesRead += bytesRead;
                        }
                        Debug.Log($"Received .depth file with size: {depthSize} bytes");
                    }
                }

                // Close the client socket
                client.Close();
            } catch (Exception e) {
                Debug.LogError("Error: " + e.Message);
            }
        });

        receiveThread.Start();
    }

    private void OnDestroy() {
        isReceiving = false;
    }
}
