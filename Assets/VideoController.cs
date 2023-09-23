using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VideoController : MonoBehaviour
{
    public Record3DPlayback videoPlayer;
    public int frameCounter;
    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 30;
        if (videoPlayer == null) videoPlayer = FindObjectOfType<Record3DPlayback>(); // gameObject.GetComponent<Record3DPlayback>();
        //videoPlayer.LoadFrame(10);
        //InvokeRepeating("NextFrame", 1.0f, 0.03f);
        //InvokeRepeating("NextFrame", 1.0f, 0.1f);
    }

    float t;
    private void Update() {
        //t += Time.deltaTime;
        //if (t > 0.03) {

        //}
        NextFrame();
    }

    public void NextFrame() {
        //Debug.Log("Begin next frame");
        if (frameCounter > videoPlayer.numberOfFrames) frameCounter = 0;
        videoPlayer.LoadFrame(frameCounter);
        frameCounter++;
        //Debug.Log("complete next frame");
    }

}
