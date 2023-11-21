using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VideoController : MonoBehaviour
{
    public bool startRendering;
    public Record3DPlayback videoPlayer;
    public int frameCounter;
    private int playbackDirection;
    // Start is called before the first frame update
    void Start()
    {
        playbackDirection = 0;
        Application.targetFrameRate = 24;
        Debug.Log($"Application.targetFrameRate {Application.targetFrameRate}");
        //Application.targetFrameRate = 30;
        startRendering = false;
        if (videoPlayer == null) videoPlayer = FindObjectOfType<Record3DPlayback>(); // gameObject.GetComponent<Record3DPlayback>();
        //videoPlayer.LoadFrame(10);
        //InvokeRepeating("NextFrame", 1.0f, 0.03f);
        //InvokeRepeating("NextFrame", 1.0f, 0.1f);
    }

    protected void PausePlay() {
        videoPlayer.isPlaying_ = !videoPlayer.isPlaying_;
    }

    public void SetReadyState() {
        startRendering = true;
        videoPlayer.isPlaying_ = true;
    }

    float t;
    private void Update() {

        //if (videoPlayer.zipArchive != null) startRendering = true;
        if (Input.GetKeyDown(KeyCode.P)) PausePlay();
        if (Input.GetKeyDown(KeyCode.LeftArrow)) SetRewind();
        if (Input.GetKeyDown(KeyCode.RightArrow)) SetForward();


        if (startRendering && videoPlayer.isPlaying_)  SequenceFrame();
        
        //NextFrame();
    }

    public void SetRewind() {
        playbackDirection = -1;
    }

    public void SetForward() {
        playbackDirection = 0;
    }

    public void SequenceFrame() {
        //Debug.Log("Begin next frame");
        //if (frameCounter > videoPlayer.numberOfFrames) frameCounter = 0;
        //if (frameCounter > 89) frameCounter = 0;
        //if (frameCounter > videoPlayer.numberOfFrames/2) frameCounter = 0;      
        
        videoPlayer.LoadFrame(frameCounter);
        if (playbackDirection == 0) {
            frameCounter++;
            frameCounter %= (videoPlayer.numberOfFrames / 2);
        } else if (playbackDirection == -1) {
            frameCounter--;
            frameCounter = (frameCounter - (videoPlayer.numberOfFrames / 2) * -1) % (videoPlayer.numberOfFrames / 2);
        }
    }

}
