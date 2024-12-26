using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEditor.Search;
using UnityEngine;

public class LocalDataSourceDebug : MonoBehaviour
{
    public Record3DPlayback playback;
    public VideoController videoController;
    public UnityMainThreadDispatcher dispatcher;

    // Start is called before the first frame update
    void Start()
    {
        if (playback == null) playback = FindObjectOfType<Record3DPlayback>();
        if (videoController == null) videoController = FindObjectOfType<VideoController>();
        if (dispatcher == null) dispatcher = FindObjectOfType<UnityMainThreadDispatcher>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L)) {
            ProcessReceivedZipFile();
        }
    }

    private void ProcessReceivedZipFile() {
        
        // Because we need to do asynchronous calls on the main thread, 
        // we can wrap this logic into an async method with a TaskCompletionSource
        var tcs = new TaskCompletionSource<bool>();

        // Enqueue the async logic on the Unity main thread
        dispatcher.Enqueue(async () => {
            try {
                // 1) Await the actual loading from the .zip:
                await playback.LoadLocalVideoAsync("fam.zip", "fam");

                // 2) Once loaded, set the video controller to ready state
                videoController.SetReadyState();

                // 3) Signal success via TCS
                tcs.SetResult(true);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        });

        // Optionally: If you are *already* inside an async method, you can do:
        // await tcs.Task;
        // or if you're in a sync method (like now), you can continue if you 
        // don't strictly need to block until it's done.
        // For demonstration, let's do it in a background Task:
        Task.Run(async () => {
            try {
                // Wait for the main-thread loading to complete
                await tcs.Task;
                Debug.Log("Zip file loaded successfully (async).");
            } catch (Exception e) {
                Debug.LogError($"Error in loading .zip file: {e.Message}");
            }
        });
    }
}
