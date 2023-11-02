using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VideoController : MonoBehaviour
{
    public bool startRendering;
    public Record3DPlayback videoPlayer;
    public int frameCounter;
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 24;
        Debug.Log($"Application.targetFrameRate {Application.targetFrameRate}");
        //Application.targetFrameRate = 30;
        startRendering = false;
        if (videoPlayer == null) videoPlayer = FindObjectOfType<Record3DPlayback>(); // gameObject.GetComponent<Record3DPlayback>();
        //videoPlayer.LoadFrame(10);
        //InvokeRepeating("NextFrame", 1.0f, 0.03f);
        //InvokeRepeating("NextFrame", 1.0f, 0.1f);
    }


    float t;
    private void Update() {

        //if (videoPlayer.zipArchive != null) startRendering = true;
        if (startRendering)  NextFrame();
        //NextFrame();
    }

    public void NextFrame() {
        //Debug.Log("Begin next frame");
        //if (frameCounter > videoPlayer.numberOfFrames) frameCounter = 0;
        //if (frameCounter > 89) frameCounter = 0;
        if (frameCounter > videoPlayer.numberOfFrames) return;
        videoPlayer.LoadFrame(frameCounter);
        frameCounter++;
        //Debug.Log("complete next frame");
    }

}
