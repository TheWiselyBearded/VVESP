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
        //Debug.Log("Begin next frame");
        //if (frameCounter > videoPlayer.numberOfFrames) frameCounter = 0;
        //if (frameCounter > 89) frameCounter = 0;
        //if (frameCounter > videoPlayer.numberOfFrames/2) frameCounter = 0;      

        //videoPlayer.LoadFrame(frameCounter);
        videoPlayer.LoadFrameAsync(frameCounter);
        if (playbackDirection == 0) {
            frameCounter++;
            frameCounter %= (videoPlayer.numberOfFrames);
        } else if (playbackDirection == -1) {
            frameCounter--;
            frameCounter = (frameCounter - (videoPlayer.numberOfFrames) * -1) % (videoPlayer.numberOfFrames);
        }
        //Debug.Log($"Frame Counter {frameCounter}");
    }

}
