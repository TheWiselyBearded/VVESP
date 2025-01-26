using System;
using System.Text;
using UnityEngine;

public partial class TcpZipDataClient
{
    private enum ServerResponseExpectation
    {
        None,
        JsonList,
        ZipFile
    }

    private ServerResponseExpectation expectedResponse = ServerResponseExpectation.None;

    /// <summary>
    /// Processes pending commands in the command queue
    /// </summary>
    private void ProcessCommandQueue()
    {
        if (commandQueue.TryDequeue(out int command))
        {
            if (command == 2 && !string.IsNullOrEmpty(filenameToRequest))
            {
                SendFileRequest(filenameToRequest);
                //filenameToRequest = null;
            }
            else
            {
                SendCommand(command);
            }
            expectedResponse = DetermineResponseExpectation(command);
        }
    }

    /// <summary>
    /// Determines the expected response type based on the command
    /// </summary>
    private ServerResponseExpectation DetermineResponseExpectation(int command) => command switch
    {
        2 => ServerResponseExpectation.ZipFile,
        1 => ServerResponseExpectation.JsonList,
        _ => ServerResponseExpectation.None
    };

    /// <summary>
    /// Sends a command to the server
    /// </summary>
    private void SendCommand(int command)
    {
        byte[] commandBytes = BitConverter.GetBytes(command);
        stream.Write(commandBytes, 0, commandBytes.Length);
        Debug.Log($"Sent command {command}");
    }

    /// <summary>
    /// Sends a file request to the server
    /// </summary>
    private void SendFileRequest(string fileName)
    {
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
    private void HandleDebugInputs()
    {
        // Press SPACE to start server thread
        if (Input.GetKeyDown(KeyCode.Space) && !isServerConnected)
        {
            StartServerThread();
        }

        // Press LeftArrow to request close
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SendCloseConnectionRequest();
        }

        // Press RightArrow to request the JSON list
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SendLoadRequest();
        }

        // Press Tab to test file loading with the current filenameToRequest
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Debug.Log("Sending file name load request");
            LoadFileRequest(filenameToRequest);
        }
    }

    /// <summary>
    /// Cleans up resources when the component is destroyed
    /// </summary>
    private void CleanupResources()
    {
        zipArchive?.Dispose();
        CloseConnection();
        StopServerThread();
    }
}

