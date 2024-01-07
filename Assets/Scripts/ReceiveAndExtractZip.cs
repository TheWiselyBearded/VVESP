using UnityEngine;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System;
using System.Threading;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Text;
using System.Linq;

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

    private ConcurrentQueue<int> commandQueue = new ConcurrentQueue<int>();
    private ConcurrentQueue<string> fileRequestQueue = new ConcurrentQueue<string>();

    // Thread-safe collection to store captures
    private ConcurrentBag<Capture> captures = new ConcurrentBag<Capture>();

    // Public variable to specify which capture to load
    public string filenameToRequest;
    private Capture selectCapture;

    public VVESP_UI_Controller controllerOVR;

    private enum ServerResponseExpectation
    {
        None,
        JsonList,
        ZipFile
    }

    private ServerResponseExpectation expectedResponse = ServerResponseExpectation.None;

    private void Awake()
    {
        if (dispatcher == null) dispatcher = FindObjectOfType<UnityMainThreadDispatcher>();
        client = new TcpClient(serverAddress, serverPort);
        stream = client.GetStream();
        memoryStream = new MemoryStream();
        //commandQueue = new ConcurrentQueue<int>();
        //fileRequestQueue = new ConcurrentQueue<string>();
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


    /// <summary>
    /// invoked via collision/button press
    /// </summary>
    public void StartServerThread()
    {
        //serverThread = new Thread(ConnectedToServer);
        serverThread = new Thread(ServerThreadMain);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    public void StopServerThread()
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
            }
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SendCloseConnectionRequest();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SendLoadRequest();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Debug.Log("Sending file name load req");
            LoadFileRequest(filenameToRequest);
        }
    }

    private void ServerThreadMain()
    {
        try
        {
            while (client.Connected)
            {
                if (commandQueue.TryDequeue(out int command))
                {
                    // If the command is 2, also send the associated filename
                    if (command == 2 && !string.IsNullOrEmpty(filenameToRequest))
                    {
                        SendFileRequest(filenameToRequest);
                        filenameToRequest = null; // Reset after sending
                    } else
                    {
                        SendCommand(command);
                    }
                    expectedResponse = DetermineResponseExpectation(command);
                }

                if (expectedResponse != ServerResponseExpectation.None && stream.DataAvailable)
                {
                    ReceiveServerResponse();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in server thread: {e.Message}");
        }
        finally
        {
            CloseConnection();
        }
    }

    private ServerResponseExpectation DetermineResponseExpectation(int command)
    {
        // Define the expected response based on the command sent
        switch (command)
        {
            case 2:
                return ServerResponseExpectation.ZipFile;
            case 1: // Load Request
                return ServerResponseExpectation.JsonList;
            case -1: // Close Connection
                return ServerResponseExpectation.None;
            default:
                return ServerResponseExpectation.None;
        }
    }

    private void ReceiveServerResponse()
    {
        byte[] buffer = new byte[4096];
        int bytesRead;

        try
        {
            if (expectedResponse == ServerResponseExpectation.ZipFile)
            {
                Debug.Log("Receiving zip file");

                // First, receive the 4-byte integer representing the file size
                byte[] fileSizeBytes = new byte[4];
                int bytesReceived = stream.Read(fileSizeBytes, 0, 4);
                if (bytesReceived != 4)
                {
                    throw new Exception("Failed to receive the full file size data.");
                }
                int fileSize = BitConverter.ToInt32(fileSizeBytes, 0);

                // Receive the file data
                int totalBytesReceived = 0;
                memoryStream.SetLength(0); // Ensure the memory stream is empty
                while (totalBytesReceived < fileSize)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        throw new Exception("Received 0 bytes from stream. Connection may have been closed.");
                    }
                    memoryStream.Write(buffer, 0, bytesRead);
                    totalBytesReceived += bytesRead;
                }

                // Create a new MemoryStream as a copy of the existing memoryStream
                var zipMemoryStream = new MemoryStream(memoryStream.ToArray());

                // Create a ZipArchive from the copied MemoryStream
                var zipArchive = new ZipArchive(zipMemoryStream);

                dispatcher.Enqueue(() =>
                {
                    playback.LoadVid(zipArchive, selectCapture);
                    videoController.SetReadyState();
                });
            }
            else if (expectedResponse == ServerResponseExpectation.JsonList)
            {
                Debug.Log("Receiving JSON list");

                // Receive JSON data using the original approach
                while (stream.DataAvailable && (bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memoryStream.Write(buffer, 0, bytesRead);
                }
                memoryStream.Seek(0, SeekOrigin.Begin);

                // Deserialize the JSON data
                string json = Encoding.UTF8.GetString(memoryStream.ToArray());
                CaptureList captureList = JsonConvert.DeserializeObject<CaptureList>(json);

                foreach (var capture in captureList.captures)
                {
                    Debug.Log($"Found file: {capture.filename}");
                    captures.Add(capture);
                }

                dispatcher.Enqueue(() => {
                    if (controllerOVR != null) {
                        controllerOVR.InitializeCaptureButtons(captureList.captures);
                    }
                });                
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error receiving server response: {e.Message}");
        }

        expectedResponse = ServerResponseExpectation.None;
        memoryStream.SetLength(0); // Clear the memory stream for next use
    }


    public void SendCloseConnectionRequest()
    {
        commandQueue.Enqueue(-1);
    }

    public void SendLoadRequest()
    {
        Debug.Log("Send load request");
        commandQueue.Enqueue(1);
    }

    private void SendCommand(int command)
    {
        byte[] commandBytes = BitConverter.GetBytes(command);
        stream.Write(commandBytes, 0, commandBytes.Length);
        Debug.Log($"Sent command {command}");
    }

    // Method to initiate a file load request
    public void LoadFileRequest(string fileName)
    {
        selectCapture = captures.FirstOrDefault(capture => capture.filename.Contains(fileName));
        Debug.Log($"Select capture {selectCapture.filename}");
        filenameToRequest = fileName; // Set the filename
        commandQueue.Enqueue(2); // Enqueue command '2' for file load
    }

    private void SendFileRequest(string fileName)
    {
        byte[] _command = BitConverter.GetBytes(2);
        byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName);
        byte[] payload = new byte[_command.Length + fileNameBytes.Length];
        Buffer.BlockCopy(_command, 0, payload, 0, _command.Length);
        Buffer.BlockCopy(fileNameBytes, 0, payload, _command.Length, fileNameBytes.Length);
        stream.Write(payload, 0, payload.Length);
    }


    private void CloseConnection()
    {
        if (client != null)
        {
            if (client.Connected)
            {
                stream.Close();
                SendCloseConnectionRequest();                
                //client.Close();
            }
            client = null;
        }
    }

    void OnDestroy()
    {
        // Close the ZipArchive when it's no longer needed
        if (zipArchive != null)
        {
            zipArchive.Dispose();
        }
        CloseConnection();
        StopServerThread();
    }
}


[Serializable]
public class Capture
{
    public string filename;
}

[Serializable]
public class CaptureList
{
    public Capture[] captures;
}
