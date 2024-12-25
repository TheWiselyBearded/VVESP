using System;
using System.Net.Sockets;
using System.Threading;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;

/// <summary>
/// Partial class handling the network connections and main server loop.
/// </summary>
public partial class TcpZipDataClient
{
    // Network-related fields
    private TcpClient client;
    private NetworkStream stream;
    private bool isServerConnected;

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
    private void InitializeNetworkConnection()
    {
        try
        {
            client = new TcpClient(serverAddress, serverPort);
            stream = client.GetStream();
            Debug.Log($"Connected to server {serverAddress}:{serverPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize network connection: {e.Message}");
        }
    }

    /// <summary>
    /// Main server communication loop that handles sending commands and receiving responses
    /// </summary>
    private void ServerThreadMain()
    {
        try
        {
            while (client.Connected)
            {
                ProcessCommandQueue();
                HandleServerResponse();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Server thread error: {e.Message}");
        }
        finally
        {
            CloseConnection();
        }
    }

    /// <summary>
    /// Closes the network connection and releases resources
    /// </summary>
    private void CloseConnection()
    {
        if (client?.Connected == true)
        {
            stream?.Close();
            SendCloseConnectionRequest(); // optional to tell the server we are done
        }
        client = null;
        isServerConnected = false;
        Debug.Log("Connection closed.");
    }
}

