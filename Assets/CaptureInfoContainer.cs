using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaptureInfoContainer : MonoBehaviour {
    [SerializeField] public Capture Capture;
    public TcpZipDataClient networkInterfacer;
    private void OnEnable() {
        if (networkInterfacer == null) networkInterfacer = FindObjectOfType<TcpZipDataClient>();
    }

    public void SetCapture(Capture capture) {
        Capture = new Capture();
        Capture.filename = capture.filename;
    }

    public void RequestCapture() {
        if (networkInterfacer == null) networkInterfacer = FindObjectOfType<TcpZipDataClient>();
        Debug.Log($"is network interface null? {networkInterfacer == null} and our capture name is {Capture.filename}");
        networkInterfacer.LoadFileRequest(Capture.filename);
    }
}

[Serializable]
public class Capture {
    public string filename;
}

[Serializable]
public class CaptureList {
    public Capture[] captures;
}