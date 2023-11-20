using UnityEngine;
using System;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.IO.Compression;
using TMPro;

public class ReceiveZipFileFromServer : MonoBehaviour {
    public string serverIP = "127.0.0.1"; // Replace with your server's IP address
    public int serverPort = 12345;       // Replace with your server's port number
    
    public ZipArchive receivedZipArchive;
    private byte[] receivedData;
    private readonly object dataLock = new object();
    private bool isReceiving = false;
    public Record3DPlayback r3dPlayback;
    public TMP_InputField inputField;
    
    private string debugText; // field

    public string DebugText
    {
        get { return debugText; }
        set { 
            debugText= value;
            inputField.text = value;
        }
    }

    public void StartReceivingBtn() {
        StartReceiving();

    }

    private void StartReceiving() {
        isReceiving = true;

        Thread receiveThread = new Thread(() => {
            try {
                using (TcpClient client = new TcpClient(serverIP, serverPort)) {
                    using (MemoryStream dataStream = new MemoryStream()) {
                        byte[] buffer = new byte[1024];
                        int bytesRead;

                        using (NetworkStream networkStream = client.GetStream()) {
                            while (isReceiving && (bytesRead = networkStream.Read(buffer, 0, buffer.Length)) > 0) {
                                dataStream.Write(buffer, 0, bytesRead);
                            }
                        }

                        // Store received data in a thread-safe manner
                        lock (dataLock) {
                            receivedData = dataStream.ToArray();
                        }

                        Debug.Log("Received data successfully.");
                    }
                }
            } catch (Exception e) {
                Debug.LogError("Error receiving data: " + e.Message);
            }
        });

        receiveThread.Start();
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.S)) StartReceiving();
        if (Input.GetKeyDown(KeyCode.Z)) CreateZipArchive();
    }
    
    public void CreateZipArchive() {
        lock (dataLock) {
            if (receivedData == null || receivedData.Length == 0) {
                Debug.LogError("No received data to create a zip archive.");
                return;
            }

            try {
                //using (MemoryStream memoryStream = new MemoryStream(receivedData)) {
                //    receivedZipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
                //    r3dPlayback.zipArchive = receivedZipArchive;
                //}
                // Create the MemoryStream without using the 'using' statement
                MemoryStream memoryStream = new MemoryStream(receivedData);

                // Create the ZipArchive
                receivedZipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
                r3dPlayback.zipArchive = receivedZipArchive;

                Debug.Log("Zip archive created successfully.");
            } catch (Exception e) {
                Debug.LogError("Error creating zip archive: " + e.Message);
            }
        }
    }

    private void OnDestroy() {
        isReceiving = false;
    }
}
