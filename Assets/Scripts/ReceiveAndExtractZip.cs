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

/// <summary>
/// Handles network communication with a server to receive and process RGBD video data in ZIP format.
/// This component manages TCP connections, command sending, and data reception for volumetric video playback.
/// </summary>
public class ReceiveAndExtractZip : MonoBehaviour {
    #region Configuration
    [Header("Network Settings")]
    [Tooltip("Enable for local development testing")]
    public bool localDebug = false;

    [Tooltip("Server IP address for connection")]
    public string serverAddress = "127.0.0.1";

    [Tooltip("Server port number")]
    public int serverPort = 12345;
    #endregion

    #region Component References
    [Header("Required Components")]
    public Record3DPlayback playback;
    public VideoController videoController;
    public VVESP_UI_Controller controllerOVR;
    public UnityMainThreadDispatcher dispatcher;
    #endregion

    #region Private Fields
    // Network-related fields
    private TcpClient client;
    private NetworkStream stream;
    private readonly MemoryStream memoryStream = new MemoryStream();
    private ZipArchive zipArchive;
    private Thread serverThread;
    private bool isServerConnected;

    // Thread-safe collections for command and data management
    private readonly ConcurrentQueue<int> commandQueue = new ConcurrentQueue<int>();
    private readonly ConcurrentQueue<string> fileRequestQueue = new ConcurrentQueue<string>();
    private readonly ConcurrentBag<Capture> captures = new ConcurrentBag<Capture>();

    // File handling
    public string filenameToRequest; // TODO: Consider making this private with a public property
    private Capture selectCapture;
    #endregion

    #region Server Response Types
    private enum ServerResponseExpectation {
        None,
        JsonList,
        ZipFile
    }
    private ServerResponseExpectation expectedResponse = ServerResponseExpectation.None;
    #endregion

    #region Unity Lifecycle Methods
    private void Awake() {
        InitializeComponents();
        InitializeNetworkConnection();
    }

    private void Update() {
        HandleDebugInputs();
    }

    private void OnDestroy() {
        CleanupResources();
    }
    #endregion

    #region Initialization Methods
    /// <summary>
    /// Initializes required components and validates dependencies
    /// </summary>
    private void InitializeComponents() {
        if (localDebug) serverAddress = "127.0.0.1";
        if (dispatcher == null) dispatcher = FindObjectOfType<UnityMainThreadDispatcher>();
    }

    /// <summary>
    /// Establishes initial network connection and prepares streams
    /// </summary>
    private void InitializeNetworkConnection() {
        try {
            client = new TcpClient(serverAddress, serverPort);
            stream = client.GetStream();
        } catch (Exception e) {
            Debug.LogError($"Failed to initialize network connection: {e.Message}");
        }
    }
    #endregion

    #region Server Communication
    /// <summary>
    /// Main server communication loop that handles sending commands and receiving responses
    /// </summary>
    private void ServerThreadMain() {
        try {
            while (client.Connected) {
                ProcessCommandQueue();
                HandleServerResponse();
            }
        } catch (Exception e) {
            Debug.LogError($"Server thread error: {e.Message}");
        } finally {
            CloseConnection();
        }
    }

    /// <summary>
    /// Processes pending commands in the command queue
    /// </summary>
    private void ProcessCommandQueue() {
        if (commandQueue.TryDequeue(out int command)) {
            if (command == 2 && !string.IsNullOrEmpty(filenameToRequest)) {
                SendFileRequest(filenameToRequest);
                filenameToRequest = null;
            } else {
                SendCommand(command);
            }
            expectedResponse = DetermineResponseExpectation(command);
        }
    }

    /// <summary>
    /// Handles incoming server responses based on the expected response type
    /// </summary>
    private void HandleServerResponse() {
        if (expectedResponse != ServerResponseExpectation.None && stream.DataAvailable) {
            ReceiveServerResponse();
        }
    }

    /// <summary>
    /// Processes server responses based on the expected type (ZIP file or JSON list)
    /// </summary>
    private void ReceiveServerResponse() {
        try {
            switch (expectedResponse) {
                case ServerResponseExpectation.ZipFile:
                    ReceiveZipFile();
                    break;
                case ServerResponseExpectation.JsonList:
                    ReceiveJsonList();
                    break;
            }
        } catch (Exception e) {
            Debug.LogError($"Error receiving server response: {e.Message}");
        } finally {
            expectedResponse = ServerResponseExpectation.None;
            memoryStream.SetLength(0);
        }
    }
    #endregion

    #region Data Reception Methods
    /// <summary>
    /// Receives and processes a ZIP file containing RGBD data
    /// </summary>
    private void ReceiveZipFile() {
        byte[] fileSizeBytes = new byte[4];
        stream.Read(fileSizeBytes, 0, 4);
        int fileSize = BitConverter.ToInt32(fileSizeBytes, 0);

        ReceiveFileData(fileSize);
        ProcessReceivedZipFile();
    }

    /// <summary>
    /// Receives file data in chunks and writes to memory stream
    /// </summary>
    private void ReceiveFileData(int fileSize) {
        byte[] buffer = new byte[4096];
        int totalBytesReceived = 0;
        memoryStream.SetLength(0);

        while (totalBytesReceived < fileSize) {
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) throw new Exception("Connection closed while receiving data");

            memoryStream.Write(buffer, 0, bytesRead);
            totalBytesReceived += bytesRead;
        }
    }

    /// <summary>
    /// Processes the received ZIP file data and updates the playback components
    /// </summary>
    private void ProcessReceivedZipFile() {
        var zipMemoryStream = new MemoryStream(memoryStream.ToArray());
        var zipArchive = new ZipArchive(zipMemoryStream);

        dispatcher.Enqueue(() => {
            playback.LoadVid(zipArchive, selectCapture);
            videoController.SetReadyState();
        });
    }

    /// <summary>
    /// Receives and processes the JSON list of available captures
    /// </summary>
    private void ReceiveJsonList() {
        byte[] buffer = new byte[4096];
        while (stream.DataAvailable) {
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
    private void ProcessCaptureList(CaptureList captureList) {
        foreach (var capture in captureList.captures) {
            Debug.Log($"Found file: {capture.filename}");
            captures.Add(capture);
        }

        dispatcher.Enqueue(() => {
            if (controllerOVR != null) {
                controllerOVR.InitializeCaptureButtons(captureList.captures);
            }
        });
    }
    #endregion

    #region Public Interface Methods
    /// <summary>
    /// Initiates the server connection thread
    /// </summary>
    public void StartServerThread() {
        serverThread = new Thread(ServerThreadMain) {
            IsBackground = true
        };
        serverThread.Start();
    }

    /// <summary>
    /// Stops the server connection thread
    /// </summary>
    public void StopServerThread() {
        if (serverThread?.IsAlive == true) {
            serverThread.Abort();
            serverThread = null;
        }
    }

    /// <summary>
    /// Requests to close the server connection
    /// </summary>
    public void SendCloseConnectionRequest() {
        commandQueue.Enqueue(-1);
    }

    /// <summary>
    /// Sends a request to load the capture list
    /// </summary>
    public void SendLoadRequest() {
        Debug.Log("Send load request");
        commandQueue.Enqueue(1);
    }

    /// <summary>
    /// Initiates a request to load a specific capture file
    /// </summary>
    public void LoadFileRequest(string fileName) {
        selectCapture = captures.FirstOrDefault(capture => capture.filename.Contains(fileName));
        Debug.Log($"Select capture {selectCapture?.filename}");
        filenameToRequest = fileName;
        commandQueue.Enqueue(2);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Determines the expected response type based on the command
    /// </summary>
    private ServerResponseExpectation DetermineResponseExpectation(int command) => command switch {
        2 => ServerResponseExpectation.ZipFile,
        1 => ServerResponseExpectation.JsonList,
        _ => ServerResponseExpectation.None
    };

    /// <summary>
    /// Sends a command to the server
    /// </summary>
    private void SendCommand(int command) {
        byte[] commandBytes = BitConverter.GetBytes(command);
        stream.Write(commandBytes, 0, commandBytes.Length);
        Debug.Log($"Sent command {command}");
    }

    /// <summary>
    /// Sends a file request to the server
    /// </summary>
    private void SendFileRequest(string fileName) {
        byte[] commandBytes = BitConverter.GetBytes(2);
        byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName);
        byte[] payload = new byte[commandBytes.Length + fileNameBytes.Length];

        Buffer.BlockCopy(commandBytes, 0, payload, 0, commandBytes.Length);
        Buffer.BlockCopy(fileNameBytes, 0, payload, commandBytes.Length, fileNameBytes.Length);

        stream.Write(payload, 0, payload.Length);
    }

    /// <summary>
    /// Handles debug input commands
    /// </summary>
    private void HandleDebugInputs() {
        if (Input.GetKeyDown(KeyCode.Space) && !isServerConnected) {
            StartServerThread();
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) {
            SendCloseConnectionRequest();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow)) {
            SendLoadRequest();
        }
        if (Input.GetKeyDown(KeyCode.Tab)) {
            Debug.Log("Sending file name load req");
            LoadFileRequest(filenameToRequest);
        }
    }

    /// <summary>
    /// Closes the network connection and releases resources
    /// </summary>
    private void CloseConnection() {
        if (client?.Connected == true) {
            stream?.Close();
            SendCloseConnectionRequest();
        }
        client = null;
    }

    /// <summary>
    /// Cleans up resources when the component is destroyed
    /// </summary>
    private void CleanupResources() {
        zipArchive?.Dispose();
        CloseConnection();
        StopServerThread();
    }
    #endregion
}

/// <summary>
/// Represents a single capture file entry
/// </summary>
[Serializable]
public class Capture {
    public string filename;
}

/// <summary>
/// Represents a list of capture files
/// </summary>
[Serializable]
public class CaptureList {
    public Capture[] captures;
}