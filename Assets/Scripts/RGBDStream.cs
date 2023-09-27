using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;

public class RGBDStream : MonoBehaviour {
    public string serverIP = "127.0.0.1"; // Replace with the server's IP address
    public int serverPort = 12345;         // Replace with the server's port number

    [SerializeField] public Texture2D receivedTexture;
    private bool isReceiving = false;
    private ConcurrentQueue<byte[]> colorFrameQueue = new ConcurrentQueue<byte[]>();
    private ConcurrentQueue<byte[]> depthFrameQueue = new ConcurrentQueue<byte[]>();


    private void Start() {
        receivedTexture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        ConnectToServer();
    }

    private float timer;
    private float timeCheck = 0.33f;
    private bool startRendering = false;
    private void Update() {
        //if (isReceiving) { }
        if (Input.GetKeyDown(KeyCode.Space)) startRendering = true;
        if (startRendering) {
            timer += Time.deltaTime;
            if (timer > timeCheck) {
                // load textures
                timer = 0;

                // Process received color frames on the main thread
                byte[] colorFrame;
                if (colorFrameQueue.TryDequeue(out colorFrame)) {
                    receivedTexture.LoadImage(colorFrame);
                    receivedTexture.Apply(); // Apply changes to the texture
                }

                
                /*byte[] depthFrame;
                while (depthFrameQueue.TryDequeue(out depthFrame)) { // Process the depth frame data as needed
                }*/
            }
        }
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
                        byte[] colorFrameData = new byte[jpgSize];
                        int totalJpgBytesRead = 0;
                        while (totalJpgBytesRead < jpgSize) {
                            bytesRead = stream.Read(colorFrameData, totalJpgBytesRead, jpgSize - totalJpgBytesRead);
                            if (bytesRead == 0)
                                break;
                            totalJpgBytesRead += bytesRead;
                        }
                        Debug.Log($"Received .jpg file with size: {jpgSize} bytes");
                        colorFrameQueue.Enqueue(colorFrameData);



                        // Receive the size of the .depth file
                        bytesRead = stream.Read(buffer, 0, 4);
                        if (bytesRead == 0)
                            break;
                        int depthSize = BitConverter.ToInt32(buffer, 0);

                        // Receive the .depth file data
                        byte[] depthFrameData = new byte[depthSize];
                        int totalDepthBytesRead = 0;
                        while (totalDepthBytesRead < depthSize) {
                            bytesRead = stream.Read(depthFrameData, totalDepthBytesRead, depthSize - totalDepthBytesRead);
                            if (bytesRead == 0)
                                break;
                            totalDepthBytesRead += bytesRead;
                        }
                        Debug.Log($"Received .depth file with size: {depthSize} bytes");
                        depthFrameQueue.Enqueue(depthFrameData);
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
