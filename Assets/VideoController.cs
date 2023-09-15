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
        if (videoPlayer == null) videoPlayer = FindObjectOfType<Record3DPlayback>(); // gameObject.GetComponent<Record3DPlayback>();
        InvokeRepeating("NextFrame", 1.0f, 0.25f);
    }

    public void NextFrame() {
        videoPlayer.LoadFrame(frameCounter);
        frameCounter++;
    }

}
