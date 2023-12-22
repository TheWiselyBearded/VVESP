using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System;
using System.Threading;
using PimDeWitte.UnityMainThreadDispatcher;

public class ReceiveAndExtractZip : MonoBehaviour
{
    public string serverAddress = "127.0.0.1"; // Replace with your server address
    public int serverPort = 12345;            // Replace with your server port
    public Record3DPlayback playback;
    public VideoController videoController;
    private ZipArchive zipArchive;
    private Thread serverThread;

    private bool isServerConnected = false;
    public TcpClient client;
    public NetworkStream stream;
    public MemoryStream memoryStream;

    public UnityMainThreadDispatcher dispatcher;

    private void Awake()
    {
        if (dispatcher == null) dispatcher = FindObjectOfType<UnityMainThreadDispatcher>();
    }


    public void ConnectedToServer()
    {
        try
        {
            client = new TcpClient(serverAddress, serverPort);
            stream = client.GetStream();
            memoryStream = new MemoryStream();

            byte[] buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
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

            //playback.LoadVid(zipArchive);
            //videoController.SetReadyState();
            // Enqueue actions onto the main thread for Unity-specific operations
            dispatcher.Enqueue(() =>
            {
                playback.LoadVid(zipArchive);
                videoController.SetReadyState();
            });



            isServerConnected = true;
        }
        catch (Exception e)
        {
            Debug.LogError("Error: " + e.Message);
            isServerConnected = false;
        }
    }

    private void StartServerThread()
    {
        serverThread = new Thread(ConnectedToServer);
        serverThread.IsBackground = true;
        
    }

    private void StopServerThread()
    {

        if (serverThread != null && serverThread.IsAlive)
        {
            serverThread.Abort();
            serverThread = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!isServerConnected)
            {
                StartServerThread();
                serverThread.Start();
            }
        }
    }

    void OnDestroy()
    {
        // Close the ZipArchive when it's no longer needed
        if (zipArchive != null)
        {
            zipArchive.Dispose();
        }

        StopServerThread();
    }
}
