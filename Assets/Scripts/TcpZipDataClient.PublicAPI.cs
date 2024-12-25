using System.Linq;
using System.Threading;
using UnityEngine;

public partial class TcpZipDataClient
{
    // Thread-safe collections
    private readonly System.Collections.Concurrent.ConcurrentQueue<int> commandQueue
        = new System.Collections.Concurrent.ConcurrentQueue<int>();
    private readonly System.Collections.Concurrent.ConcurrentBag<Capture> captures
        = new System.Collections.Concurrent.ConcurrentBag<Capture>();

    // For file requests
    private Capture selectCapture;
    public string filenameToRequest;

    /// <summary>
    /// Initiates the server connection thread
    /// </summary>
    public void StartServerThread()
    {
        if (serverThread == null || !serverThread.IsAlive)
        {
            serverThread = new Thread(ServerThreadMain) { IsBackground = true };
            serverThread.Start();
            isServerConnected = true;
            Debug.Log("Server thread started.");
        }
    }

    /// <summary>
    /// Stops the server connection thread
    /// </summary>
    public void StopServerThread()
    {
        if (serverThread?.IsAlive == true)
        {
            serverThread.Abort();
            serverThread = null;
        }
    }

    /// <summary>
    /// Requests to close the server connection
    /// </summary>
    public void SendCloseConnectionRequest()
    {
        commandQueue.Enqueue(-1);
    }

    /// <summary>
    /// Sends a request to load the capture list
    /// </summary>
    public void SendLoadRequest()
    {
        Debug.Log("Sending load request.");
        commandQueue.Enqueue(1);
    }

    /// <summary>
    /// Initiates a request to load a specific capture file
    /// </summary>
    public void LoadFileRequest(string fileName)
    {
        selectCapture = captures.FirstOrDefault(c => c.filename.Contains(fileName));
        Debug.Log($"Select capture {selectCapture?.filename}");
        filenameToRequest = fileName;
        commandQueue.Enqueue(2);
    }
}

