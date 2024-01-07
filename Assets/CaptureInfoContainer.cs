using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaptureInfoContainer : MonoBehaviour {
    [SerializeField] public Capture Capture;
    public ReceiveAndExtractZip networkInterfacer;
    private void OnEnable() {
        if (networkInterfacer == null) networkInterfacer = FindObjectOfType<ReceiveAndExtractZip>();
    }

    public void SetCapture(Capture capture) {
        Capture = new Capture();
        Capture.filename = capture.filename;
    }

    public void RequestCapture() {
        if (networkInterfacer == null) networkInterfacer = FindObjectOfType<ReceiveAndExtractZip>();
        Debug.Log($"is network interface null? {networkInterfacer == null} and our capture name is {Capture.filename}");
        networkInterfacer.LoadFileRequest(Capture.filename);
    }
}
