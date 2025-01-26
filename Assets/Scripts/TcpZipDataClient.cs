using UnityEngine;
using PimDeWitte.UnityMainThreadDispatcher;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System;

/// <summary>
/// Handles network communication with a server to receive and process RGBD video data in ZIP format.
/// This component manages TCP connections, command sending, and data reception for volumetric video playback.
/// 
/// MonoBehaviour portion only includes references, Unity lifecycle methods, and high-level housekeeping.
/// </summary>
public partial class TcpZipDataClient : MonoBehaviour {
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

    [Header("Download Options")]
    [Tooltip("If enabled, downloaded ZIP files will be saved to the StreamingAssets folder.")]
    public bool saveToDisk = false;


    #endregion

    #region Private Fields
    // Keep references to our managed thread
    private Thread serverThread;
    private ZipArchive zipArchive;
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
}
