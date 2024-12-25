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

        t += Time.deltaTime;
        if (t >= 0.10)
        {
            if (startRendering && videoPlayer.isPlaying_)  SequenceFrame();
            t = 0;
        } 
        
        //NextFrame();
    }

    public void SetRewind() {
        playbackDirection = -1;
        Debug.Log("Set rewind");
    }

    public void SetForward() {
        playbackDirection = 0;
        Debug.Log("Set forward");
    }

    public void SetPlay() => videoPlayer.isPlaying_ = true;
    public void SetPause() => videoPlayer.isPlaying_ = false;

    public void SequenceFrame() {   
        videoPlayer.LoadFrameAsync(frameCounter);
        if (playbackDirection == 0) {
            frameCounter++;
            frameCounter %= (videoPlayer.currentVideo_.numFrames);
        } else if (playbackDirection == -1) {
            frameCounter--;
            frameCounter = (frameCounter - (videoPlayer.currentVideo_.numFrames) * -1) % (videoPlayer.currentVideo_.numFrames);
        }
        //Debug.Log($"Frame Counter {frameCounter}");
    }

}
